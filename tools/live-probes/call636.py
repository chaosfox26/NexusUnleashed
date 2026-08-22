import frida, sys, time
import os as _os
LOG = _os.path.expandvars(r"%TEMP%\claude\call636.log")
JS = r"""
const base = Process.getModuleByName('WildStar64.exe').base;
const s = base.add(0xC65898).readPointer();
const unit = s.add(120).readPointer();
const guid = unit.add(8).readU32();
const cont = s.add(25744).readPointer();
send('session=' + s + ' unit=' + unit + ' guid=0x' + guid.toString(16) + ' container(+25744)=' + cont);
if (cont.isNull()) { send('container NULL - handler would no-op'); }
// build a2 = [unitId, flag=1, playerGuid]
const a2 = Memory.alloc(16);
a2.writeU32(guid); a2.add(4).writeU32(1); a2.add(8).writeU32(guid);
const fn = new NativeFunction(base.add(0x57A630), 'int64', ['pointer','pointer','double','double']);
send('calling sub_14057A630(0x636 handler)...');
try { const r = fn(s, a2, 0.0, 0.0); send('returned ' + r + ' | standstate+440=' + unit.add(440).readU32() + ' +4896=' + unit.add(4896).readU32()); }
catch(e){ send('call err ' + e); }
"""
def main():
    dev = frida.get_local_device()
    p = [x for x in dev.enumerate_processes() if x.name == 'WildStar64.exe']
    if not p: print("no client"); return 2
    f = open(LOG, 'w', encoding='utf-8')
    ses = dev.attach(p[0].pid); sc = ses.create_script(JS)
    sc.on('message', lambda m, d: (f.write(str(m.get('payload', m)) + "\n"), f.flush()))
    sc.load(); time.sleep(4); f.close(); print("done")
if __name__ == '__main__': sys.exit(main())
