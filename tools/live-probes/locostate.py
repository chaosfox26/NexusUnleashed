import frida, sys, time
import os as _os
LOG = _os.path.expandvars(r"%TEMP%\claude\locostate.log")
JS = r"""
const base = Process.getModuleByName('WildStar64.exe').base;
const s = base.add(0xC65898).readPointer();
const unit = s.add(120).readPointer();
const cont = s.add(25744).readPointer();
send('unit=' + unit + ' container=' + cont + ' (same=' + unit.equals(cont) + ')');
const offs = [128,440,444,460,464,3408,3416,3568,3572,3648,4232,4896,4904,4908,4932,4992,5008,5160,5164];
let out = [];
offs.forEach(function(o){ try{ out.push('+'+o+'='+unit.add(o).readU32()); }catch(e){ out.push('+'+o+'=err'); } });
send(out.join('  '));
"""
def main():
    dev = frida.get_local_device()
    p = [x for x in dev.enumerate_processes() if x.name == 'WildStar64.exe']
    if not p: print("no client"); return 2
    f = open(LOG, 'w', encoding='utf-8')
    ses = dev.attach(p[0].pid); sc = ses.create_script(JS)
    sc.on('message', lambda m, d: (f.write(str(m.get('payload', m)) + "\n"), f.flush()))
    sc.load(); time.sleep(3); f.close(); print("done")
if __name__ == '__main__': sys.exit(main())
