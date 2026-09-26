[CmdletBinding()]
param([string]$MSBuildPath)

$ErrorActionPreference = 'Stop'
if (-not $MSBuildPath) {
    $command = Get-Command MSBuild.exe -ErrorAction SilentlyContinue
    if ($command) {
        $MSBuildPath = $command.Source
    } else {
        $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
        if (Test-Path -LiteralPath $vswhere) {
            $MSBuildPath = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' |
                Select-Object -First 1
        }
    }
}
if (-not $MSBuildPath -or -not (Test-Path -LiteralPath $MSBuildPath)) {
    throw 'MSBuild was not found. Install Visual Studio Build Tools or supply -MSBuildPath.'
}

$project = Join-Path $PSScriptRoot 'PoESummonMod\SkeletonCrew.csproj'
& $MSBuildPath $project /t:Rebuild /p:Configuration=Release /verbosity:minimal /nologo
if ($LASTEXITCODE -ne 0) { throw "Release build failed with exit code $LASTEXITCODE." }

$output = Join-Path $PSScriptRoot 'PoESummonMod\bin\Release'
$manifestPath = Join-Path $output 'Info.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.AssemblyName -ne 'SkeletonCrew.dll' -or $manifest.Id -ne 'SkeletonCrew') {
    throw 'Unexpected mod ID or assembly name in Info.json.'
}
$assemblyPath = Join-Path $output $manifest.AssemblyName
if (-not (Test-Path -LiteralPath $assemblyPath -PathType Leaf)) {
    throw "Missing required mod assembly: $assemblyPath"
}
$assemblyName = [Reflection.AssemblyName]::GetAssemblyName($assemblyPath)
if ($assemblyName.Name -ne 'SkeletonCrew') { throw 'The output is not the SkeletonCrew assembly.' }
if ($manifest.Version -notmatch '^\d+(\.\d+){1,3}$') { throw 'Invalid release version.' }

# Package exactly the two required files together; never distribute Info.json alone.
# Generated archives stay under the already-ignored build output directory.
$archivePath = Join-Path $output ("SkeletonCrew-" + $manifest.Version + '.zip')
Compress-Archive -LiteralPath @($manifestPath, $assemblyPath) -DestinationPath $archivePath -Force
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($archivePath)
try {
    $entries = @($archive.Entries | ForEach-Object { $_.FullName })
    if ($entries.Count -ne 2 -or $entries -notcontains 'Info.json' -or
        $entries -notcontains $manifest.AssemblyName) {
        throw 'Release archive is incomplete or has an incorrect directory layout.'
    }
    foreach ($entry in $archive.Entries) {
        if ($entry.Length -eq 0) { throw "Empty release file: $($entry.FullName)" }
    }
} finally {
    $archive.Dispose()
}
Write-Output "Validated release archive: $archivePath"
