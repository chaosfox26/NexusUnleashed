param([int]$x, [int]$y)
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class WC {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x,int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f,uint x,uint y,uint d,IntPtr e);
}
"@
$p = Get-Process WildStar64 -ErrorAction SilentlyContinue
if(-not $p){ "no client"; exit 1 }
[WC]::SetForegroundWindow($p.MainWindowHandle) | Out-Null; Start-Sleep -Milliseconds 400
[WC]::SetCursorPos($x,$y); Start-Sleep -Milliseconds 200
[WC]::mouse_event(0x02,0,0,0,[IntPtr]::Zero); [WC]::mouse_event(0x04,0,0,0,[IntPtr]::Zero)
Start-Sleep -Milliseconds 600
"clicked $x $y"
