import frida, time
import os as _os
LOG = _os.path.expandvars(r"%TEMP%\claude\getdesc.log")
JS = r"""
const base = Process.getModuleByName('WildStar64.exe').base;
let done = false;
Interceptor.attach(base.add(0x331990), { onEnter(a){
  if (done) return; done = true;
  try {
    const mgr = this.context.rcx;           // a1 = message manager
    const vt = mgr.readPointer();
    const lookup = new NativeFunction(vt.add(304).readPointer(), 'pointer', ['pointer','uint']);
    [0x111, 0x17F, 0x110, 0x1A0].forEach(function(op){
      const desc = lookup(mgr, op);
      if (desc.isNull()) { send('op 0x'+op.toString(16)+' desc=NULL'); return; }
      const structSize = desc.add(8).readU64();
      const readFn = desc.add(32).readPointer();
      send('op 0x'+op.toString(16)+' desc='+desc+' structSize='+structSize+' readFn=module+0x'+readFn.sub(base).toString(16));
    });
  } catch(e){ send('err '+e); }
}});
send('armed - waiting for next inbound message to grab mgr');
"""
def main():
    dev = frida.get_local_device()
    p = [x for x in dev.enumerate_processes() if x.name=='WildStar64.exe']
    if not p: print("no client"); return
    f=open(LOG,'w',encoding='utf-8')
    ses=dev.attach(p[0].pid); sc=ses.create_script(JS)
    sc.on('message',lambda m,d:(f.write(str(m.get('payload',m))+"\n"),f.flush()))
    sc.load(); time.sleep(4); f.close(); print("done")
main()
