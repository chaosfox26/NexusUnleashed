import frida, sys, time
import os as _os
LOG = _os.path.expandvars(r"%TEMP%\claude\uimon.log")
JS = r"""
const base = Process.getModuleByName('WildStar64.exe').base;
function ts(){ return '['+(Date.now()%100000)+'] '; }
// every Lua event fired (sub_1400EA3E0(mgr, name, data)) - rdx = name
Interceptor.attach(base.add(0xEA3E0), { onEnter(a){
  try { let n=this.context.rdx.readUtf8String(); if(n) send(ts()+'EVENT '+n); } catch(e){}
}});
// world dispatch opcode (sub_1403EC6A0) - r8
Interceptor.attach(base.add(0x3EC6A0), { onEnter(a){
  let op=this.context.r8.toUInt32()&0xffff; send(ts()+'W-DISP 0x'+op.toString(16));
}});
// realm dispatch opcode (sub_140020EA0) - r8
Interceptor.attach(base.add(0x20EA0), { onEnter(a){
  let op=this.context.r8.toUInt32()&0xffff; send(ts()+'R-DISP 0x'+op.toString(16));
}});
// 0x25E handler
Interceptor.attach(base.add(0x3B5F80), { onEnter(a){ send(ts()+'>>> 0x25E handler (CharacterData)'); }});
send(ts()+'UIMON armed (events + world/realm dispatch + 0x25E handler)');
"""
def main():
    dur = int(sys.argv[1]) if len(sys.argv) > 1 else 3600
    dev = frida.get_local_device()
    # wait for the client if not up yet
    for _ in range(60):
        p = [x for x in dev.enumerate_processes() if x.name == 'WildStar64.exe']
        if p: break
        time.sleep(1)
    if not p: print("no client"); return
    f = open(LOG, 'w', encoding='utf-8')
    ses = dev.attach(p[0].pid); sc = ses.create_script(JS)
    sc.on('message', lambda m, d: (f.write(str(m.get('payload', m)) + "\n"), f.flush()))
    sc.load()
    time.sleep(dur)
    f.close()
main()
