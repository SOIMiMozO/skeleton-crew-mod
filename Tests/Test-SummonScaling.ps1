$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$source = Join-Path $PSScriptRoot '..\PoESummonMod\ChanterSummonScaling.cs'
$harness = Join-Path $PSScriptRoot 'ScalingRegression.cs'
$output = Join-Path $PSScriptRoot '..\PoESummonMod\obj\ScalingRegression.exe'
New-Item -ItemType Directory -Path (Split-Path $output) -Force | Out-Null
$origin = Join-Path $PSScriptRoot '..\PoESummonMod\ChanterSummonOrigin.cs'
$equipment = Join-Path $PSScriptRoot '..\PoESummonMod\ChanterSummonEquipment.cs'
$burn = Join-Path $PSScriptRoot '..\PoESummonMod\ChanterWurmBurn.cs'
$presentation = Join-Path $PSScriptRoot '..\PoESummonMod\ChanterWurmBurnPresentation.cs'
& $compiler /nologo "/out:$output" $source $origin $equipment $burn $presentation $harness
if ($LASTEXITCODE -ne 0) { throw 'Regression harness compilation failed.' }
& $output
if ($LASTEXITCODE -ne 0) { throw 'Summon scaling regression failed.' }
