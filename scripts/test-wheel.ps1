# Test de bout en bout : coller du texte dans le Bloc-notes via la roue.
param()

$ErrorActionPreference = "Continue"
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class MWheel {
  [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
  [StructLayout(LayoutKind.Sequential)] public struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public UIntPtr dwExtraInfo; }
  [StructLayout(LayoutKind.Explicit)] public struct INPUT { [FieldOffset(0)] public uint type; [FieldOffset(8)] public MOUSEINPUT mi; }
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern uint SendInput(uint n, INPUT[] i, int size);
  [DllImport("user32.dll")] public static extern int GetSystemMetrics(int n);
  public static void Send(uint flags, int dx, int dy) {
    var inp = new INPUT[1]; inp[0].type = 0; inp[0].mi.dwFlags = flags; inp[0].mi.dx = dx; inp[0].mi.dy = dy;
    SendInput(1, inp, Marshal.SizeOf(typeof(INPUT)));
  }
}
"@

cmd /c "echo RTM-OK-1234| clip"
Start-Process notepad | Out-Null
Start-Sleep -Seconds 2

$sw = [MWheel]::GetSystemMetrics(0); $sh = [MWheel]::GetSystemMetrics(1)
$cx = [int]($sw/2); $cy = [int]($sh/2)
[MWheel]::SetCursorPos($cx, $cy) | Out-Null
Start-Sleep -Milliseconds 400

Write-Host "1. appui molette"
[MWheel]::Send(0x0020, 0, 0)
Start-Sleep -Milliseconds 300

Write-Host "2. glisser vers Presse-papiers"
for ($i = 1; $i -le 7; $i++) {
  $nx = [int](65535 * ($cx - $i * 6) / ($sw - 1)); $ny = [int](65535 * ($cy + $i * 13) / ($sh - 1))
  [MWheel]::Send(0x8001, $nx, $ny); Start-Sleep -Milliseconds 55
}
Start-Sleep -Milliseconds 400

Write-Host "3. glisser vers Coller (sous-menu)"
$sx = $cx - 7*6; $sy = $cy + 7*13
for ($i = 1; $i -le 6; $i++) {
  $nx = [int](65535 * ($sx + $i * 9) / ($sw - 1)); $ny = [int](65535 * ($sy + $i * 12) / ($sh - 1))
  [MWheel]::Send(0x8001, $nx, $ny); Start-Sleep -Milliseconds 55
}
Start-Sleep -Milliseconds 300

$bmp = New-Object System.Drawing.Bitmap(900, 700)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen(($cx - 450), ($cy - 350), 0, 0, (New-Object System.Drawing.Size(900, 700)))
$g.Dispose(); $bmp.Save("$PSScriptRoot\..\shots\notepad-avant.png"); $bmp.Dispose()

Write-Host "4. relacher -> Coller (Ctrl+V)"
[MWheel]::Send(0x0040, 0, 0)
Start-Sleep -Milliseconds 1200

$bmp2 = New-Object System.Drawing.Bitmap(900, 700)
$g2 = [System.Drawing.Graphics]::FromImage($bmp2)
$g2.CopyFromScreen(($cx - 450), ($cy - 350), 0, 0, (New-Object System.Drawing.Size(900, 700)))
$g2.Dispose(); $bmp2.Save("$PSScriptRoot\..\shots\notepad-apres.png"); $bmp2.Dispose()
Write-Host "TERMINE"
