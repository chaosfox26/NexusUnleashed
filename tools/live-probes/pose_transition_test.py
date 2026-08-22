import frida, sys, time
import os as _os
LOG = _os.path.expandvars(r"%TEMP%\claude\pose_transition_test.log")
JS = r"""
const base = Process.getModuleByName('WildStar64.exe').base;
const s = base.add(0xC65898).readPointer();
const unit = s.add(120).readPointer();
const setStand = new NativeFunction(base.add(0x45BF30), 'int64', ['pointer','int','uint']);
send('unit=' + unit + ' +440(before)=' + unit.add(440).readU32());
// Force a real transition: Stand->Sit->Stand, then Stand->LyingDown->Stand
setStand(unit, 1, 0); send('set Sit(1) -> +440=' + unit.add(440).readU32() + ' +4896=' + unit.add(4896).readU32());
Thread.sleep(1.2);
setStand(unit, 0, 0); send('set Stand(0) -> +440=' + unit.add(440).readU32() + ' +4896=' + unit.add(4896).readU32());
Thread.sleep(1.2);
send('done-A');
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
