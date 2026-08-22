import frida, sys, time
import os as _os
LOG = _os.path.expandvars(r"%TEMP%\claude\anim_tick.log")
JS = r"""
const base = Process.getModuleByName('WildStar64.exe').base;
const s = base.add(0xC65898).readPointer();
const unit = s.add(120).readPointer();
send('unit=' + unit);
let animUpd = 0, animUpdOther = 0, animApply = 0;
// sub_1405B5070(a1=unit, a2) per-frame anim blend update
Interceptor.attach(base.add(0x5B5070), { onEnter(a){ if (a[0].equals(unit)) animUpd++; else animUpdOther++; }});
// sub_140474400(a1=unit, animId, flag) animation apply
Interceptor.attach(base.add(0x474400), { onEnter(a){ if (a[0].equals(unit)) { animApply++; send('  ANIM APPLY id='+a[1].toInt32()+' flag='+a[2].toInt32()); } }});
setTimeout(function(){ send('over ~3s: animUpd(player)='+animUpd+' animUpd(other)='+animUpdOther+' animApply(player)='+animApply); }, 3000);
"""
def main():
    dev = frida.get_local_device()
    p = [x for x in dev.enumerate_processes() if x.name == 'WildStar64.exe']
    if not p: print("no client"); return 2
    f = open(LOG, 'w', encoding='utf-8')
    ses = dev.attach(p[0].pid); sc = ses.create_script(JS)
    sc.on('message', lambda m, d: (f.write(str(m.get('payload', m)) + "\n"), f.flush()))
    sc.load(); time.sleep(4.5); f.close(); print("done")
if __name__ == '__main__': sys.exit(main())
