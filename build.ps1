$ErrorActionPreference='Stop'
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $csc /nologo /target:winexe /platform:anycpu /optimize+ /out:"$PSScriptRoot\Locklight.exe" /reference:System.Drawing.dll /reference:System.Windows.Forms.dll "$PSScriptRoot\Locklight.cs"
if($LASTEXITCODE -ne 0){throw 'Build failed'}
