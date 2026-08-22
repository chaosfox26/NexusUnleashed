param([int]$vk=0x1B, [int]$holdMs=150)
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class WV {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk,byte sc,uint f,IntPtr e);
  [DllImport("user32.dll")] public static extern uint MapVirtualKey(uint uCode, uint uMapType);
}
"@
$p = Get-Process WildStar64 -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if(-not $p){ "no client"; exit 1 }
[WV]::SetForegroundWindow($p.MainWindowHandle) | Out-Null
Start-Sleep -Milliseconds 400
# use a proper scancode so RawInput/DirectInput games (WildStar gameplay) see it
$sc = [byte]([WV]::MapVirtualKey([uint32]$vk, 0))
[WV]::keybd_event([byte]$vk, $sc, 0, [IntPtr]::Zero)   # KEYDOWN (0), with scancode
Start-Sleep -Milliseconds $holdMs
[WV]::keybd_event([byte]$vk, $sc, 2, [IntPtr]::Zero)   # KEYUP (2)
"vk=0x{0:X2} sc=0x{1:X2} held ${holdMs}ms" -f $vk, $sc
