import frida, sys, time
import os as _os
LOG = _os.path.expandvars(r"%TEMP%\claude\watch_live.log")
JS = r"""
const base = Process.getModuleByName('WildStar64.exe').base;
const s = base.add(0xC65898).readPointer();
const unit = s.add(120).readPointer();
const names = ['Stand','Sit','LyingDown','State0','State1','State2','Looting','Emote','StillPose','DeathPose','Burrowed','State3','Chair','Mannequin'];
function ts(){ return '['+((Date.now()/1000)%10000).toFixed(1)+'] '; }

// Hook SetStandState so any pose transition is logged with source
Interceptor.attach(base.add(0x45BF30), { onEnter(a){
  try {
    const e = a[0]; const st = a[1].toInt32(); const sd = a[2].toUInt32();
    if (e.equals(unit)) {
      const bt = Thread.backtrace(this.context, Backtracer.ACCURATE)
        .map(x=>{ let m=Process.findModuleByAddress(x); return m?('+0x'+x.sub(m.base).toString(16)):null;})
        .filter(x=>x).slice(0,6).join(' ');
      send(ts()+'SetStandState -> '+st+' ('+(names[st]||'?')+') data='+sd+' | '+bt);
    }
  } catch(e){}
}});

// Sample key fields, report only on change
let prev = '';
function pos(){ try { return unit.add(4576).readFloat().toFixed(1)+','+unit.add(4580).readFloat().toFixed(1)+','+unit.add(4584).readFloat().toFixed(1); } catch(e){ return '?'; } }
setInterval(function(){
  try {
    const cur = 'pos='+pos()+' stand(+440)='+unit.add(440).readU32()+' +4896='+unit.add(4896).readU32()
      +' +4932='+unit.add(4932).readU32()+' +5160='+unit.add(5160).readU32()+' vel='+unit.add(4448).readFloat().toFixed(2)+','+unit.add(4456).readFloat().toFixed(2);
    if (cur !== prev) { send(ts()+cur); prev = cur; }
  } catch(e){}
}, 300);
send(ts()+'WATCH LIVE armed (unit='+unit+')');
"""
def main():
    dur = int(sys.argv[1]) if len(sys.argv)>1 else 240
    dev = frida.get_local_device()
    p = [x for x in dev.enumerate_processes() if x.name == 'WildStar64.exe']
    if not p: print("no client"); return 2
    f = open(LOG, 'w', encoding='utf-8')
    ses = dev.attach(p[0].pid); sc = ses.create_script(JS)
    sc.on('message', lambda m, d: (f.write(str(m.get('payload', m)) + "\n"), f.flush()))
    sc.load(); time.sleep(dur); f.close(); print("done")
if __name__ == '__main__': sys.exit(main())
