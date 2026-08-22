Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Proc {
  [StructLayout(LayoutKind.Sequential)] public struct STARTUPINFO {
    public int cb; public string r1,r2,r3; public int dwX,dwY,dwXSize,dwYSize,dwXCountChars,dwYCountChars,dwFillAttribute,dwFlags;
    public short wShowWindow,cbReserved2; public IntPtr lpReserved2,hStdInput,hStdOutput,hStdError;
  }
  [StructLayout(LayoutKind.Sequential)] public struct PROCESS_INFORMATION { public IntPtr hProcess,hThread; public int dwProcessId,dwThreadId; }
  [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
  public static extern bool CreateProcessW(string app, string cmd, IntPtr pa, IntPtr ta, bool inherit,
    uint flags, IntPtr env, string cwd, ref STARTUPINFO si, out PROCESS_INFORMATION pi);
}
"@
$exe = "%USERPROFILE%\OneDrive\Desktop\realm-portable\clients\Wildstar\Client64\WildStar64.exe"
$cwd = Split-Path $exe
# cmdline MUST begin with /auth, NO exe token (client reads GetCommandLineW)
$cmd = "/auth localhost /authNc localhost /lang en /patcher localhost /SettingsKey WildStar /realmDataCenterId 9"
$si = New-Object Proc+STARTUPINFO; $si.cb = [Runtime.InteropServices.Marshal]::SizeOf($si)
$pi = New-Object Proc+PROCESS_INFORMATION
$ok = [Proc]::CreateProcessW($exe, $cmd, [IntPtr]::Zero, [IntPtr]::Zero, $false, 0, [IntPtr]::Zero, $cwd, [ref]$si, [ref]$pi)
if ($ok) { "launched pid $($pi.dwProcessId)" } else { "CreateProcessW FAILED err=$([Runtime.InteropServices.Marshal]::GetLastWin32Error())" }
