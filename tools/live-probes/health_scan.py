import frida, sys, time
import os as _os
LOG = _os.path.expandvars(r"%TEMP%\claude\health_scan.log")
JS = r"""
const base = Process.getModuleByName('WildStar64.exe').base;
const s = base.add(0xC65898).readPointer();
const unit = s.add(120).readPointer();
send('unit=' + unit);
// find every offset in unit[0..8192] whose u32 == 250 (the health we sent), and show neighbors
let hits250 = [];
for (let o = 0; o < 8192; o += 4) {
  try { if (unit.add(o).readU32() === 250) hits250.push(o); } catch(e){}
}
send('offsets==250: ' + hits250.join(','));
hits250.forEach(function(o){
  let ctx = [];
  for (let d = -8; d <= 12; d += 4) { try { ctx.push('+'+(o+d)+'='+unit.add(o+d).readU32()); } catch(e){} }
  send('  @'+o+': ' + ctx.join(' '));
});
// also look for a 'model sequence' style small enum region: dump some known state offsets
send('standstate+440=' + unit.add(440).readU32() + ' +4896=' + unit.add(4896).readU32());
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
