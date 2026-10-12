param([switch]$StopOnly)
$ErrorActionPreference = 'Stop'
$appPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot 'dist/Screenline.exe')).Path
Add-Type @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class ScreenlineRestart {
    delegate bool EnumProc(IntPtr h,IntPtr p);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback,IntPtr parameter);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h,out uint process);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr h,StringBuilder text,int count);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h,int message,IntPtr w,IntPtr l);
    public static void Close(int process) {
        EnumWindows(delegate(IntPtr h,IntPtr p){uint id;GetWindowThreadProcessId(h,out id);if(id!=(uint)process)return true;var title=new StringBuilder(256);GetWindowText(h,title,256);if(title.ToString()=="Screenline · 截图晾台")PostMessage(h,0x10,IntPtr.Zero,IntPtr.Zero);return true;},IntPtr.Zero);
    }
}
'@
foreach ($appProcess in @(Get-Process -Name Screenline -ErrorAction SilentlyContinue)) {
    if ($appProcess.Path -ne $appPath) { continue }
    [ScreenlineRestart]::Close($appProcess.Id)
    if (!$appProcess.WaitForExit(5000)) { throw 'Screenline is still completing an operation; no forced shutdown was used.' }
}
if (!$StopOnly) {
    $newProcess = Start-Process -FilePath $appPath -WorkingDirectory (Split-Path -Parent $appPath) -PassThru
    Start-Sleep -Milliseconds 1000
    $newProcess.Refresh()
    if ($newProcess.HasExited) { throw 'Screenline exited during startup' }
    [PSCustomObject]@{ ProcessId=$newProcess.Id; Responding=$newProcess.Responding; Version=(Get-Item -LiteralPath $appPath).VersionInfo.FileVersion }
}
