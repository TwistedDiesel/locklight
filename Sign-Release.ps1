# Copyright (C) 2026 Lawrence Shafer — https://shaferlabs.com
# SPDX-License-Identifier: GPL-3.0-or-later
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$CertificateThumbprint,
    [string]$SignToolPath,
    [string]$TimestampUrl = 'http://timestamp.digicert.com',
    [switch]$LocalMachineStore
)
$ErrorActionPreference = 'Stop'
$exe = Join-Path $PSScriptRoot 'Locklight.exe'
if (!(Test-Path -LiteralPath $exe)) { throw 'Build Locklight.exe using build.ps1 first.' }
if (!$SignToolPath) {
    $command = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($command) { $SignToolPath = $command.Source }
    else {
        $kits = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
        $tool = Get-ChildItem -Path "$kits\*\x64\signtool.exe" -ErrorAction SilentlyContinue | Sort-Object FullName -Descending | Select-Object -First 1
        if ($tool) { $SignToolPath = $tool.FullName }
    }
}
if (!$SignToolPath -or !(Test-Path -LiteralPath $SignToolPath)) { throw 'Install Windows SDK Signing Tools, or supply -SignToolPath.' }
$thumbprint = $CertificateThumbprint.Replace(' ', '')
$store = if ($LocalMachineStore) { 'Cert:\LocalMachine\My' } else { 'Cert:\CurrentUser\My' }
$cert = Get-Item -LiteralPath "$store\$thumbprint"
if (!$cert.HasPrivateKey) { throw 'The selected certificate has no accessible private key.' }
if ($cert.NotAfter -lt (Get-Date)) { throw 'The selected certificate has expired.' }
$argsForSigning = @('sign','/sha1',$thumbprint,'/s','My','/fd','SHA256','/tr',$TimestampUrl,'/td','SHA256','/d','Locklight','/du','https://shaferlabs.com')
if ($LocalMachineStore) { $argsForSigning += '/sm' }
$argsForSigning += $exe
& $SignToolPath @argsForSigning
if ($LASTEXITCODE -ne 0) { throw 'Signing failed; no release archive was created.' }
& $SignToolPath verify /pa /all /v $exe
if ($LASTEXITCODE -ne 0) { throw 'Signature verification failed; no release archive was created.' }
$release = Join-Path $PSScriptRoot 'Release'
New-Item -ItemType Directory -Path $release -Force | Out-Null
Copy-Item -LiteralPath $exe -Destination $release
foreach ($name in @('COPYING','NOTICE.txt','BUILD.txt')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $release }
$source = Join-Path $release 'Source'
New-Item -ItemType Directory -Path $source -Force | Out-Null
foreach ($name in @('Locklight.cs','app.manifest','build.ps1','Sign-Release.ps1','COPYING','NOTICE.txt','BUILD.txt','SIGNING.txt')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $source }
$archive = Join-Path $PSScriptRoot 'Locklight-Signed-Portable.zip'
Compress-Archive -Path "$release\*" -DestinationPath $archive -Force
Get-FileHash -LiteralPath $exe -Algorithm SHA256 | Format-List
Write-Host "Signed release created: $archive"
Write-Host 'A valid signature does not override organization-specific application-control policies. Test the signed release on target PCs.'
