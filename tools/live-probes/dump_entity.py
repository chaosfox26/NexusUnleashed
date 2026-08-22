import frida, sys, time
import os as _os
LOG = _os.path.expandvars(r"%TEMP%\claude\dump_entity.log")
JS = r"""
const base = Process.getModuleByName('WildStar64.exe').base;
const fn = base.add(0x96FA0);  // sub_140096FA0 entity reader(a1,a2,a3)
let n = 0;
Interceptor.attach(fn, {
  onEnter(args){ this.a3 = args[2]; },
  onLeave(ret){
    if (n >= 6) return;
    const a3 = this.a3; if (a3.isNull()) return;
    const rd = (o,sz)=>{ try{ return sz===8? a3.add(o).readU64().toString(): a3.add(o).readU32(); }catch(e){ return 'err'; } };
    const guid = a3.readU32();
    const kind = a3.add(4).readU32();
    let s = 'ENT#'+n+' guid=0x'+guid.toString(16)+' kind='+kind;
    s += ' +128='+rd(128)+' propCnt(+129)='+rd(129);
    s += ' +144='+rd(144)+' movCnt(+148)='+rd(148);
    s += ' +160cnt='+rd(160)+' visCnt(+176)='+rd(176)+' +192cnt='+rd(192);
    s += ' +208='+rd(208)+' fac1(+212)='+rd(212)+' fac2(+216)='+rd(216)+' +220='+rd(220);
    s += ' sel232='+rd(232)+' +236='+rd(236)+' sel240='+rd(240)+' +248='+rd(248)+' sel264='+rd(264)+' +268='+rd(268);
    s += ' +276='+rd(276)+' +280='+rd(280)+' +284='+rd(284);
    send(s);
    // movement element 0
    try { const mp = a3.add(152).readPointer(); if(!mp.isNull()){ send('  mov[0] type='+mp.readU32()+' d1='+mp.add(8).readU32()+' d2='+mp.add(12).readU32()+' d3='+mp.add(16).readU32()); } } catch(e){}
    // property element 0
    try { const pp = a3.add(136).readPointer(); if(!pp.isNull()){ send('  prop[0] a='+pp.readU32()+' b='+pp.add(4).readU32()+' c='+pp.add(8).readU32()+' d='+pp.add(12).readU32()); } } catch(e){}
    n++;
  }
});
send('dump_entity armed on sub_140096FA0');
"""
def main():
    dev = frida.get_local_device()
    p = [x for x in dev.enumerate_processes() if x.name == 'WildStar64.exe']
    if not p: print("no client"); return 2
    f = open(LOG, 'w', encoding='utf-8')
    ses = dev.attach(p[0].pid); sc = ses.create_script(JS)
    sc.on('message', lambda m, d: (f.write(str(m.get('payload', m)) + "\n"), f.flush()))
    sc.load()
    dur = int(sys.argv[1]) if len(sys.argv)>1 else 20
    time.sleep(dur); f.close(); print("done")
if __name__ == '__main__': sys.exit(main())
