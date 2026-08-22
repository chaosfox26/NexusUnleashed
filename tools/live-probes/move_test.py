import frida, sys, time
import os as _os
LOG = _os.path.expandvars(r"%TEMP%\claude\move_test.log")
JS = r"""
const base = Process.getModuleByName('WildStar64.exe').base;
const s = base.add(0xC65898).readPointer();
const unit = s.add(120).readPointer();
function pos(){ return unit.add(4576).readFloat().toFixed(2)+','+unit.add(4580).readFloat().toFixed(2)+','+unit.add(4584).readFloat().toFixed(2); }
for (let i=0;i<10;i++){ send('t'+i+' pos='+pos()+' +440='+unit.add(440).readU32()+' +4896='+unit.add(4896).readU32()); Thread.sleep(0.4); }
send('done');
"""
def main():
    dev = frida.get_local_device()
    p = [x for x in dev.enumerate_processes() if x.name == 'WildStar64.exe']
    if not p: print("no client"); return 2
    f = open(LOG, 'w', encoding='utf-8')
    ses = dev.attach(p[0].pid); sc = ses.create_script(JS)
    sc.on('message', lambda m, d: (f.write(str(m.get('payload', m)) + "\n"), f.flush()))
    sc.load(); time.sleep(5); f.close(); print("done")
if __name__ == '__main__': sys.exit(main())
