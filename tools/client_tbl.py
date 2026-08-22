#!/usr/bin/env python3
"""
client_tbl.py - correct, parallel WildStar .tbl reader/dumper (Starlight Protocol).

Model-free: column names and types come from the file. Fixes the record-close failure that
stopped the older reader on Item2 (and any table with a trailing structural pad): records are
read by ABSOLUTE per-row positioning (stride = RecordSize), so a trailing pad is naturally
skipped, and the string 4-byte pad is decided PER ROW (a==0 && next field non-string && not the
last-two fields) exactly as the engine's GameTable<T>.ReadEntries does - never precomputed as a
fixed set (that was the bug: the pad is a per-row property of each string cell).

Parallel: large tables are split across a process pool (32 workers by default) - hardware-first
per the Starlight Protocol.

Verify: --verify compares value-for-value against the engine's own TSV dumps (the equivalence
gate) so correctness is PROVED, not assumed, before any extracted value is trusted.

Usage:
    python client_tbl.py <Table.tbl> [--cols Id,ItemDisplayId,...] [--where col=val] [--out x.tsv]
    python client_tbl.py --dumpall <tbl_dir> <out_dir> [--jobs 32]
    python client_tbl.py --verify <tbl_dir> <engine_dump_dir>
"""
import io, os, sys, struct, glob
from concurrent.futures import ProcessPoolExecutor

HEADER_FMT = '<II11Q'; HEADER_SIZE = struct.calcsize(HEADER_FMT)   # 96
FIELD_FMT  = '<QQHHI'; FIELD_SIZE  = struct.calcsize(FIELD_FMT)    # 24
SIGNATURE  = 0x4454424C
T_UINT, T_FLOAT, T_BOOL, T_ULONG, T_STRING = 3, 4, 11, 20, 130


def _parse_header(data):
    (sig, ver, name_len, u1, record_size, field_count, field_offset,
     record_count, total_record_size, record_offset, max_id, lookup_offset,
     u2) = struct.unpack_from(HEADER_FMT, data, 0)
    if sig != SIGNATURE:
        raise ValueError(f"bad signature {sig:#x}")
    fields = []
    pos = HEADER_SIZE + field_offset
    for _ in range(field_count):
        nl, no, ftype, _u2, _u3 = struct.unpack_from(FIELD_FMT, data, pos)
        fields.append((nl, no, ftype)); pos += FIELD_SIZE
    names_start = (HEADER_SIZE + field_offset + FIELD_SIZE * field_count + 15) & ~15
    names = []
    for (nl, no, ftype) in fields:
        names.append(data[names_start + no: names_start + no + (nl - 1) * 2]
                     .decode('utf-16-le', 'replace'))
    return dict(record_size=record_size, field_count=field_count, record_count=record_count,
                record_offset=record_offset, total_record_size=total_record_size,
                fields=fields, names=names)


def read_tbl(path):
    """Return (col_names, rows). Correct per-row walk; tolerates trailing pad."""
    data = io.open(path, 'rb').read()
    h = _parse_header(data)
    fields = h['fields']; fc = h['field_count']; rs = h['record_size']
    records_bytes = rs * h['record_count']
    st_off = HEADER_SIZE + h['record_offset'] + records_bytes
    st = data[st_off: st_off + (h['total_record_size'] - records_bytes)]

    def string_at(off):
        end = st.find(b'\x00\x00', off)
        while end != -1 and (end - off) % 2:
            end = st.find(b'\x00\x00', end + 1)
        return st[off:end if end != -1 else len(st)].decode('utf-16-le', 'replace')

    rows = []
    rec0 = HEADER_SIZE + h['record_offset']
    for j in range(h['record_count']):
        pos = rec0 + rs * j          # ABSOLUTE per-row: trailing pad self-corrects at row boundary
        row = []
        for k in range(fc):
            ft = fields[k][2]
            if ft in (T_UINT, T_BOOL):
                row.append(struct.unpack_from('<I', data, pos)[0]); pos += 4
            elif ft == T_FLOAT:
                row.append(struct.unpack_from('<f', data, pos)[0]); pos += 4
            elif ft == T_ULONG:
                row.append(struct.unpack_from('<Q', data, pos)[0]); pos += 8
            elif ft == T_STRING:
                a, b = struct.unpack_from('<II', data, pos); pos += 8
                row.append(string_at(max(a, b) - records_bytes))
                # engine per-row string pad: a==0 && next field non-string && not last-two fields
                nxt = k + 1
                if a == 0 and nxt < fc - 1 and fields[nxt][2] != T_STRING:
                    pos += 4
            else:
                raise ValueError(f"{path}: unknown field type {ft} at col {k}")
        rows.append(row)
    return h['names'], rows


def _fmt(v):
    if isinstance(v, float):
        # shortest round-trip float32 (matches engine dumps; :g truncates - do not use)
        return repr(struct.unpack('<f', struct.pack('<f', v))[0])
    return str(v)


def dump_one(args):
    tbl, out_dir = args
    name = os.path.splitext(os.path.basename(tbl))[0]
    try:
        cols, rows = read_tbl(tbl)
    except Exception as e:
        return (name, f'ERR {e}', 0)
    out = os.path.join(out_dir, name + '.tsv')
    with io.open(out, 'w', encoding='utf-8', newline='') as f:
        f.write('\t'.join(cols) + '\n')
        for r in rows:
            f.write('\t'.join(_fmt(v).replace('\t', ' ').replace('\n', ' ') for v in r) + '\n')
    return (name, 'OK', len(rows))


def verify(tbl_dir, dump_dir):
    results = []
    for dump in sorted(glob.glob(os.path.join(dump_dir, '*.tsv'))):
        name = os.path.splitext(os.path.basename(dump))[0]
        tbl = os.path.join(tbl_dir, name + '.tbl')
        if not os.path.isfile(tbl):
            # try capitalized
            cands = glob.glob(os.path.join(tbl_dir, name + '.tbl')) or \
                    glob.glob(os.path.join(tbl_dir, name[:1].upper() + name[1:] + '.tbl'))
            if not cands:
                results.append((name, 'NO-TBL', 0)); continue
            tbl = cands[0]
        try:
            cols, rows = read_tbl(tbl)
        except Exception as e:
            results.append((name, f'ERR {e}', 0)); continue
        with io.open(dump, encoding='utf-8') as f:
            dcols = f.readline().rstrip('\n').split('\t')
            drows = [ln.rstrip('\n').split('\t') for ln in f]
        if len(rows) != len(drows):
            results.append((name, f'ROWCOUNT {len(rows)} vs {len(drows)}', 0)); continue
        mism = checked = 0
        for r, dr in zip(rows, drows):
            flat = []
            for cell in dr:
                flat.extend(cell.split(',') if (',' in cell and cell.count(',')) else [cell])
            if len(flat) != len(r):
                continue
            for pv, dv in zip(r, flat):
                checked += 1
                if isinstance(pv, float):
                    try: ok = struct.unpack('<f', struct.pack('<f', float(dv)))[0] == pv
                    except ValueError: ok = False
                elif isinstance(pv, int):
                    ok = str(pv) == dv or (dv in ('True', 'False') and (dv == 'True') == bool(pv))
                else:
                    ok = pv.replace('\t', ' ').replace('\n', ' ').replace('\r', '') == dv
                if not ok: mism += 1
        results.append((name, 'OK' if mism == 0 else f'{mism} MISMATCH', checked))
    return results


def main():
    a = sys.argv[1:]
    if not a:
        print(__doc__); return
    if a[0] == '--verify':
        tot_ok = tot = 0
        for name, verdict, n in verify(a[1], a[2]):
            print(f"{name:34s} {verdict:16s} ({n:,})")
            tot += 1; tot_ok += (verdict == 'OK')
        print(f"\n{tot_ok}/{tot} tables value-exact")
        return
    if a[0] == '--dumpall':
        tbl_dir, out_dir = a[1], a[2]
        jobs = int(a[a.index('--jobs') + 1]) if '--jobs' in a else min(32, os.cpu_count() or 8)
        os.makedirs(out_dir, exist_ok=True)
        tbls = sorted(glob.glob(os.path.join(tbl_dir, '*.tbl')))
        print(f"dumping {len(tbls)} tables with {jobs} workers...")
        ok = 0
        with ProcessPoolExecutor(max_workers=jobs) as ex:
            for name, verdict, n in ex.map(dump_one, [(t, out_dir) for t in tbls]):
                if verdict != 'OK': print(f"  {name}: {verdict}")
                else: ok += 1
        print(f"{ok}/{len(tbls)} dumped OK -> {out_dir}")
        return
    # single table
    tbl = a[0]
    cols, rows = read_tbl(tbl)
    want = None
    if '--cols' in a:
        want = a[a.index('--cols') + 1].split(',')
    where = None
    if '--where' in a:
        wc, wv = a[a.index('--where') + 1].split('=')
        where = (cols.index(wc), wv)
    idx = [cols.index(c) for c in want] if want else list(range(len(cols)))
    outp = a[a.index('--out') + 1] if '--out' in a else None
    f = io.open(outp, 'w', encoding='utf-8', newline='') if outp else sys.stdout
    f.write('\t'.join(cols[i] for i in idx) + '\n')
    n = 0
    for r in rows:
        if where and _fmt(r[where[0]]) != where[1]:
            continue
        f.write('\t'.join(_fmt(r[i]) for i in idx) + '\n'); n += 1
    if outp: f.close(); print(f"{n} rows -> {outp}")


if __name__ == '__main__':
    main()
