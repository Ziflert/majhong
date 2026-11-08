Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class NativeWindow {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
  [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
  [DllImport("user32.dll")] public static extern uint GetCurrentThreadId();
  [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);
}
'@
$unity = Get-Process Unity -ErrorAction SilentlyContinue | Where-Object { $_.Path -like '*6000.0.85f1*' } | Select-Object -First 1
if (-not $unity) { Write-Error 'Unity editor process not found'; exit 2 }
$foreground = [NativeWindow]::GetForegroundWindow()
[uint32]$foregroundPid = 0
$foregroundThread = [NativeWindow]::GetWindowThreadProcessId($foreground, [ref]$foregroundPid)
$currentThread = [NativeWindow]::GetCurrentThreadId()
[NativeWindow]::AttachThreadInput($currentThread, $foregroundThread, $true) | Out-Null
[NativeWindow]::ShowWindow($unity.MainWindowHandle, 9) | Out-Null
[NativeWindow]::BringWindowToTop($unity.MainWindowHandle) | Out-Null
[NativeWindow]::SetForegroundWindow($unity.MainWindowHandle) | Out-Null
[NativeWindow]::AttachThreadInput($currentThread, $foregroundThread, $false) | Out-Null
Write-Output "Focused Unity: $($unity.MainWindowTitle) HWND=$($unity.MainWindowHandle)"
