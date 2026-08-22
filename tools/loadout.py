#!/usr/bin/env python3
"""
loadout.py - authoritative starter-loadout + appearance resolver (Starlight Protocol).

Resolves, purely from the client's own tables (via client_tbl.py), the CORRECT starter gear
and its rendered appearance for any race/class/sex - so a fresh character looks and is equipped
exactly as the client intends, first time. No hand-picked ids, no NF.

Chain (all client-derived):
  CharacterCreation(classId,raceId,sex,enabled)          -> starter itemId list
  Item2(id).itemSourceId, item2TypeId                    -> per item
  ItemDisplaySourceEntry(itemSourceId,item2TypeId,level) -> itemDisplayId  (the rendered visual)
  Item2Type(item2TypeId).itemSlotId                      -> equip/visual slot (ItemSlot enum)

Outputs:
  --items    <class> <race> <sex>         list the starter item ids
  --appearance <class> <race> <sex> [lvl] print slot->displayId (equipment visual rows)
  --sql <charId> <class> <race> <sex>     emit SQL: characterdb.item + character_appearance rows
The equipment character_appearance rows render the body dressed in BOTH char-select (char list)
and in-game (0x0262), which both read character_appearance. Body-customization rows (slots 24-70)
are left untouched.
"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import client_tbl as t

TBL = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'assets', 'tbl')


def _load(name):
    cols, rows = t.read_tbl(os.path.join(TBL, name + '.tbl'))
    ci = {n: i for i, n in enumerate(cols)}
    return ci, rows


def starter_items(classId, raceId, sex):
    ci, rows = _load('CharacterCreation')
    best = None
    for r in rows:
        if (r[ci['classId']] == classId and r[ci['raceId']] == raceId and
                r[ci['sex']] == sex and r[ci['enabled']] == 1):
            items = [r[ci['itemId0']]] + [r[ci['itemId0%d' % k]] for k in range(1, 16)]
            items = [x for x in items if x]
            # prefer the plainest enabled set (startEnum 3 = standard)
            if best is None or r[ci['characterCreationStartEnum']] == 3:
                best = items
    return best or []


def resolve_appearance(items, level=1):
    """-> list of (slot, displayId) for items that carry a visual."""
    i2, i2r = _load('Item2'); item = {r[i2['ID']]: r for r in i2r}
    ds, dsr = _load('ItemDisplaySourceEntry')
    ty, tyr = _load('Item2Type'); typ = {r[ty['ID']]: r for r in tyr}
    out = []
    for iid in items:
        r = item.get(iid)
        if not r:
            continue
        src = r[i2['itemSourceId']]; t2 = r[i2['item2TypeId']]
        disp = 0
        for e in dsr:
            if (e[ds['itemSourceId']] == src and e[ds['item2TypeId']] == t2 and
                    e[ds['itemMinLevel']] <= level <= e[ds['itemMaxLevel']]):
                disp = e[ds['itemDisplayId']]; break
        slot = typ.get(t2, [0] * 3)[ty['itemSlotId']] if t2 in typ else 0
        if disp and slot:
            out.append((slot, disp))
    return out


def item_slots(items):
    """-> list of (itemId, itemSlotId) for DB seeding (location 0 = equipped)."""
    i2, i2r = _load('Item2'); item = {r[i2['ID']]: r for r in i2r}
    ty, tyr = _load('Item2Type'); typ = {r[ty['ID']]: r for r in tyr}
    out = []
    for iid in items:
        r = item.get(iid)
        if not r:
            continue
        t2 = r[i2['item2TypeId']]
        slot = typ.get(t2, [0] * 3)[ty['itemSlotId']] if t2 in typ else 0
        out.append((iid, slot, t2))
    return out


def main():
    a = sys.argv[1:]
    if not a:
        print(__doc__); return
    mode = a[0]
    if mode == '--items':
        cls, race, sex = int(a[1]), int(a[2]), int(a[3])
        print(starter_items(cls, race, sex))
    elif mode == '--appearance':
        cls, race, sex = int(a[1]), int(a[2]), int(a[3])
        lvl = int(a[4]) if len(a) > 4 else 1
        items = starter_items(cls, race, sex)
        print(f"items: {items}")
        for slot, disp in resolve_appearance(items, lvl):
            print(f"  slot {slot:2d} -> display {disp}")
    elif mode == '--sql':
        charId, cls, race, sex = int(a[1]), int(a[2]), int(a[3]), int(a[4])
        items = starter_items(cls, race, sex)
        slots = item_slots(items)
        app = resolve_appearance(items)
        base = 900000 + charId * 100
        print(f"-- starter loadout for char {charId} (class {cls} race {race} sex {sex})")
        print(f"DELETE FROM item WHERE ownerId={charId};")
        vals = []
        for i, (iid, slot, t2) in enumerate(slots):
            # equipped items go to location 0 at their equip slot (weapon slot 16 in DB space);
            # DB bagIndex uses the equip-slot index space (0=chest..16=weapon), mapped from ItemSlot.
            vals.append(f"({base+i},{charId},{iid},0,{slot},1,0,1,0)")
        print("INSERT INTO item (id,ownerId,itemId,location,bagIndex,stackCount,charges,durability,expirationTimeLeft) VALUES\n  " +
              ",\n  ".join(vals) + ";")
        print(f"DELETE FROM character_appearance WHERE id={charId} AND slot IN (" +
              ",".join(str(s) for s, _ in app) + ");")
        print("INSERT INTO character_appearance (id,slot,displayId) VALUES\n  " +
              ",\n  ".join(f"({charId},{s},{d})" for s, d in app) + ";")


if __name__ == '__main__':
    main()
