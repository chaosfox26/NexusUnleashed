import frida, sys, time
import os as _os
LOG = _os.path.expandvars(r"%TEMP%\claude\pose_hammer.log")
JS = r"""
const base = Process.getModuleByName('WildStar64.exe').base;
const s = base.add(0xC65898).readPointer();
const unit = s.add(120).readPointer();
send('unit=' + unit + ' +4896(before)=' + unit.add(4896).readU32() + ' +440=' + unit.add(440).readU32());
// hammer +4896=0 as fast as possible for 6s
let n = 0;
const t0 = Date.now();
const iv = setInterval(function(){
  try { unit.add(4896).writeU32(0); n++; } catch(e){}
  if (Date.now() - t0 > 6000) { clearInterval(iv); send('hammered ' + n + ' writes; +4896 now=' + unit.add(4896).readU32()); }
}, 1);
"""
def main():
    dev = frida.get_local_device()
    p = [x for x in dev.enumerate_processes() if x.name == 'WildStar64.exe']
    if not p: print("no client"); return 2
    f = open(LOG, 'w', encoding='utf-8')
    ses = dev.attach(p[0].pid); sc = ses.create_script(JS)
    sc.on('message', lambda m, d: (f.write(str(m.get('payload', m)) + "\n"), f.flush()))
    sc.load(); time.sleep(8); f.close(); print("done")
if __name__ == '__main__': sys.exit(main())
