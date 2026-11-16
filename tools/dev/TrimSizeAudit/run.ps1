#Requires -Version 7.0
[CmdletBinding()]
param(
    [string] $SourceCommit = '57e42d28dd56b0d91237b523271cabeb36eafed4',
    [string] $OutputRoot = (Join-Path ([IO.Path]::GetTempPath()) ('arc-trim-audit-' + [guid]::NewGuid().ToString('N'))),
    [ValidateSet('baseline', 'core', 'nuget', 'reference', 'source-full', 'selective', 'no-heavy', 'combined', 'no-charts', 'no-editors')]
    [string[]] $Variants = @('baseline', 'core', 'nuget', 'reference', 'source-full', 'selective', 'no-heavy', 'combined', 'no-charts', 'no-editors')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not $IsWindows -or [Runtime.InteropServices.RuntimeInformation]::OSArchitecture -ne 'X64') {
    throw 'This experiment publishes and runs win-x64 desktop executables; use Windows x64.'
}
Get-Command git, dotnet -ErrorAction Stop | Out-Null
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
if (Test-Path -LiteralPath $OutputRoot) {
    throw "Choose a new output directory to avoid stale publish files: $OutputRoot"
}
New-Item -ItemType Directory -Path $OutputRoot | Out-Null
$logs = New-Item -ItemType Directory -Path (Join-Path $OutputRoot 'logs')
$publishRoot = New-Item -ItemType Directory -Path (Join-Path $OutputRoot 'publish')
$sourceRoot = Join-Path $OutputRoot 'source'
$archive = Join-Path $OutputRoot 'source.zip'

Push-Location $repositoryRoot
try {
    $resolvedCommit = & git rev-parse --verify "$SourceCommit^{commit}"
    if ($LASTEXITCODE -ne 0) {
        throw 'Source commit is unavailable locally. Fetch the upstream history first.'
    }
    & git archive --format=zip "--output=$archive" $resolvedCommit
    if ($LASTEXITCODE -ne 0) { throw 'Could not export the committed source snapshot.' }
    Expand-Archive -LiteralPath $archive -DestinationPath $sourceRoot
    $patch = Join-Path $PSScriptRoot 'experiment.patch'
    & git -C $sourceRoot apply --check $patch
    if ($LASTEXITCODE -ne 0) { throw 'Experiment patch does not apply to this source commit.' }
    & git -C $sourceRoot apply $patch
    if ($LASTEXITCODE -ne 0) { throw 'Could not apply the experiment patch.' }

    foreach ($project in @('Inspect')) {
        $destination = New-Item -ItemType Directory -Path (Join-Path $OutputRoot $project)
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot "$project/$project.csproj"),
            (Join-Path $PSScriptRoot "$project/Program.cs") -Destination $destination
    }
    $sdk = & dotnet --version
    if ($LASTEXITCODE -ne 0) { throw 'Could not read the selected SDK.' }
    & dotnet --info > (Join-Path $logs 'dotnet-info.txt')
    $inspectorProject = Join-Path $OutputRoot 'Inspect/Inspect.csproj'
    & dotnet build $inspectorProject -c Release --nologo *> (Join-Path $logs 'inspect-build.log')
    if ($LASTEXITCODE -ne 0) { throw "Inspector build failed; see $logs" }
    $inspector = Join-Path $OutputRoot 'Inspect/bin/Release/net10.0/Inspect.dll'
    $rows = [Collections.Generic.List[object]]::new()
    foreach ($variant in $Variants) {
        # Restore/build intermediates must be isolated too: the package graph differs between variants.
        $hostRoot = New-Item -ItemType Directory -Force -Path (Join-Path $OutputRoot "hosts/$variant")
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Probe/Probe.csproj'),
            (Join-Path $PSScriptRoot 'Probe/Program.cs') -Destination $hostRoot
        $probeProject = Join-Path $hostRoot 'Probe.csproj'
        $source = $variant -in @('source-full', 'selective', 'no-heavy', 'combined', 'no-charts', 'no-editors')
        $mode = if ($source -or $variant -eq 'nuget') { 'arc' } else { $variant }
        $destination = Join-Path $publishRoot $variant
        $publishLog = Join-Path $logs "$variant-publish.log"
        $publishArgs = @('publish', $probeProject, '-c', 'Release', "-p:AuditMode=$mode", '-o', $destination,
            "-p:UseSource=$($source.ToString().ToLowerInvariant())", "-p:ArcSourceRoot=$sourceRoot",
            "-p:ArcAuditButtonOnly=$($variant -in @('selective', 'combined'))",
            "-p:ArcAuditNoHeavy=$($variant -in @('no-heavy', 'combined'))",
            "-p:ArcAuditNoCharts=$($variant -eq 'no-charts')",
            "-p:ArcAuditNoEditors=$($variant -eq 'no-editors')")
        Write-Host "Publishing $variant..."
        & dotnet @publishArgs *> $publishLog
        if ($LASTEXITCODE -ne 0) {
            Get-Content -LiteralPath $publishLog -Tail 30 | Write-Host
            throw "Publish failed for $variant; see $publishLog"
        }
        $runtimeLog = Join-Path $logs "$variant-run.log"
        $runtimeOutput = @(& (Join-Path $destination 'Probe.exe') 2>&1)
        $runExitCode = $LASTEXITCODE
        $runtimeOutput | Set-Content -LiteralPath $runtimeLog
        if ($runExitCode -ne 0 -or $runtimeOutput -notcontains 'OK') {
            throw "Startup failed for $variant; see $runtimeLog"
        }
        if ($mode -eq 'arc' -and $runtimeOutput -notcontains 'button theme found=True; loaded after button=1') {
            throw "Button theme lookup failed for $variant; see $runtimeLog"
        }
        $files = @(Get-ChildItem -LiteralPath $destination -File -Recurse |
            Where-Object Extension -NotIn @('.pdb', '.xml'))
        $bytes = [long](($files | Measure-Object Length -Sum).Sum)
        $assembly = Join-Path $destination 'TKCode.Arc.dll'
        $inspection = $null
        if (Test-Path -LiteralPath $assembly) {
            $inspectionJson = & dotnet $inspector $assembly
            if ($LASTEXITCODE -ne 0) { throw "Assembly inspection failed for $variant" }
            $inspection = $inspectionJson | ConvertFrom-Json
        }
        $dependencies = Get-Content -LiteralPath (Join-Path $destination 'Probe.deps.json') -Raw | ConvertFrom-Json
        $runtimePack = @($dependencies.libraries.PSObject.Properties.Name |
            Where-Object { $_ -match 'Microsoft.NETCore.App.Runtime' })
        $rows.Add([pscustomobject]@{
            Variant = $variant
            Bytes = $bytes
            MiB = [math]::Round($bytes / 1MB, 4)
            RuntimePack = $runtimePack
            RunOutput = $runtimeOutput
            Assembly = $inspection
            Files = @($files | Sort-Object FullName | ForEach-Object {
                [pscustomobject]@{ Path = [IO.Path]::GetRelativePath($destination, $_.FullName); Bytes = $_.Length }
            })
        })
        # Preserve completed results if a later variant fails.
        [pscustomobject]@{ SourceCommit = $resolvedCommit; Sdk = $sdk; Rid = 'win-x64'; Results = $rows.ToArray() } |
            ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $OutputRoot 'results.json')
    }
    $rows | Select-Object Variant, Bytes, MiB | Format-Table -AutoSize
    Write-Host "Results and logs: $OutputRoot"
}
finally {
    Pop-Location
}
