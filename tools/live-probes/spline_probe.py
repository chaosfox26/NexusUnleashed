import frida, sys, time
import os as _os
LOG = _os.path.expandvars(r"%TEMP%\claude\spline_probe.log")
JS = r"""
const base = Process.getModuleByName('WildStar64.exe').base;
const s = base.add(0xC65898).readPointer();
const unit = s.add(120).readPointer();
function pos(){ return unit.add(4576).readFloat().toFixed(1)+','+unit.add(4580).readFloat().toFixed(1)+','+unit.add(4584).readFloat().toFixed(1); }
// spline subsystem is unit+3936; its node-list ptrs at +128/+136 => unit+4064 / unit+4072
send('pos=' + pos());
for (let sub of [3904,3936,4080,4144,4256,4320,4208,4232]) {
  try {
    const p1 = unit.add(sub+128).readPointer();
    const p2 = unit.add(sub+136).readPointer();
    if (!p1.isNull() || !p2.isNull()) send('subsys +'+sub+': node@+128='+p1+' node@+136='+p2+'  <-- ACTIVE');
  } catch(e){}
}
// try shoving position +15 on X, watch if it holds
const x0 = unit.add(4576).readFloat();
send('writing X ' + x0.toFixed(1) + ' -> ' + (x0+15).toFixed(1));
for (let i=0;i<8;i++){
  unit.add(4576).writeFloat(x0+15);
  Thread.sleep(0.25);
  send('  t'+i+' pos='+pos());
}
send('done');
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
