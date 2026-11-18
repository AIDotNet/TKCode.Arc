#Requires -Version 7.0
[CmdletBinding()]
param(
    [string] $OutputRoot = (Join-Path ([IO.Path]::GetTempPath()) ('arc-trim-audit-v2-' + [guid]::NewGuid().ToString('N'))),
    [ValidateSet('full', 'selected-with-languages', 'selected', 'missing-token')]
    [string[]] $Variants = @('full', 'selected-with-languages', 'selected', 'missing-token')
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not $IsWindows -or [Runtime.InteropServices.RuntimeInformation]::OSArchitecture -ne 'X64') {
    throw 'This fixture publishes and renders a Windows x64 desktop window.'
}
if (@($Variants | Select-Object -Unique).Count -ne $Variants.Count) { throw 'Duplicate variants are not supported.' }
$repository = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$contractPath = Join-Path $PSScriptRoot 'selection-contract.json'
$contract = Get-Content -LiteralPath $contractPath -Raw | ConvertFrom-Json
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
if ($OutputRoot.StartsWith($repository + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'OutputRoot must be outside this checkout.'
}
if (Test-Path -LiteralPath $OutputRoot) { throw 'Choose a new directory; previous output is never reused or deleted.' }
Get-Command git, dotnet -ErrorAction Stop | Out-Null
New-Item -ItemType Directory -Path $OutputRoot | Out-Null
$logs = New-Item -ItemType Directory -Path (Join-Path $OutputRoot 'logs')
$sdkJson = @{ sdk = @{ version = $contract.Sdk; rollForward = 'disable'; allowPrerelease = $false } } | ConvertTo-Json
$sdkJson | Set-Content -LiteralPath (Join-Path $OutputRoot 'global.json')
foreach ($name in @('Directory.Build.props', 'Directory.Build.targets', 'Directory.Packages.props')) {
    '<Project />' | Set-Content -LiteralPath (Join-Path $OutputRoot $name)
}
'<configuration><packageSources><clear /><add key="nuget.org" value="https://api.nuget.org/v3/index.json" /></packageSources></configuration>' |
    Set-Content -LiteralPath (Join-Path $OutputRoot 'NuGet.Config')
function Invoke-Tool([string] $Tool, [string[]] $Arguments, [string] $LogFile) {
    & $Tool @Arguments *> $LogFile
    if ($LASTEXITCODE -ne 0) {
        Get-Content -LiteralPath $LogFile -Tail 25 | Write-Host
        throw "$Tool failed; see $LogFile"
    }
}
function Hash([string] $Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash }
function Copy-Host([string] $Name, [string] $Destination) {
    New-Item -ItemType Directory -Force -Path $Destination | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "$Name/$Name.csproj.template") -Destination (Join-Path $Destination "$Name.csproj")
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "$Name/Program.cs") -Destination $Destination
}
Push-Location $OutputRoot
try {
    $sdk = & dotnet --version
    if ($LASTEXITCODE -ne 0 -or $sdk -ne $contract.Sdk) { throw "Exact SDK $($contract.Sdk) is required." }
    $installedRuntimes = @(& dotnet --list-runtimes)
    if (-not ($installedRuntimes -match ('^Microsoft.NETCore.App ' + [regex]::Escape($contract.Runtime) + ' '))) {
        throw "Install the exact .NET $($contract.Runtime) runtime for the inspector."
    }
    Invoke-Tool dotnet @('--info') (Join-Path $logs 'dotnet-info.txt')
    $resolvedCommit = & git -C $repository rev-parse --verify ($contract.SourceCommit + '^{commit}')
    if ($LASTEXITCODE -ne 0 -or $resolvedCommit -ne $contract.SourceCommit) { throw 'Fetch the pinned upstream commit first.' }
    foreach ($property in $contract.SourceBlobs.PSObject.Properties) {
        $blob = & git -C $repository rev-parse ($contract.SourceCommit + ':' + $property.Name)
        if ($LASTEXITCODE -ne 0 -or $blob -ne $property.Value) { throw "Reviewed closure changed: $($property.Name)" }
    }
    $archive = Join-Path $OutputRoot 'source.zip'
    Invoke-Tool git @('-C', $repository, 'archive', '--format=zip', "--output=$archive", $resolvedCommit) (Join-Path $logs 'archive.log')
    $inspectorRoot = Join-Path $OutputRoot 'inspector'
    Copy-Host Inspect $inspectorRoot
    Invoke-Tool dotnet @('build', (Join-Path $inspectorRoot 'Inspect.csproj'), '-c', 'Release', '--nologo') (Join-Path $logs 'inspector-build.log')
    $inspector = Join-Path $inspectorRoot 'bin/Release/net10.0/Inspect.dll'
    $inputHashes = [ordered]@{}
    foreach ($name in @('run.ps1', 'experiment.patch', 'selection-contract.json', 'Probe/Program.cs', 'Probe/Probe.csproj.template', 'Inspect/Program.cs', 'Inspect/Inspect.csproj.template')) {
        $inputHashes[$name] = Hash (Join-Path $PSScriptRoot $name)
    }
    $results = [Collections.Generic.List[object]]::new()
    $report = [ordered]@{
        SchemaVersion = 2; SourceCommit = $resolvedCommit; UpstreamSplitPR = 'https://github.com/AIDotNet/TKCode.Arc/pull/138'
        Environment = [ordered]@{ Sdk = $sdk; RuntimePack = $contract.Runtime; Rid = $contract.Rid; OS = [Runtime.InteropServices.RuntimeInformation]::OSDescription; SdkPolicy = ($sdkJson | ConvertFrom-Json) }
        Inputs = $inputHashes; SelectionContract = $contract; Results = @()
    }
    foreach ($variant in $Variants) {
        Write-Host "Publishing and rendering $variant..."
        # Entire source trees, generators, obj/bin, hosts, and publish directories are independent.
        $variantRoot = Join-Path $OutputRoot "variants/$variant"
        $sourceRoot = Join-Path $variantRoot 'source'
        Expand-Archive -LiteralPath $archive -DestinationPath $sourceRoot
        $sdkJson | Set-Content -LiteralPath (Join-Path $sourceRoot 'global.json')
        $themeFiles = @(Get-ChildItem -LiteralPath (Join-Path $sourceRoot 'src/TKCode.Arc') -Recurse -Filter '*Theme.axaml' | Where-Object { $_.Directory.Name -eq 'Themes' })
        $themeTypes = @($themeFiles | ForEach-Object {
            $document = [xml](Get-Content -LiteralPath $_.FullName -Raw)
            $class = $document.DocumentElement.GetAttribute('Class', 'http://schemas.microsoft.com/winfx/2006/xaml')
            if (-not $class) { throw "Theme class is missing: $($_.Name)" }
            $class
        } | Sort-Object)
        if ($themeTypes -notcontains $contract.SelectedTheme -or @($themeTypes | Select-Object -Unique).Count -ne $themeTypes.Count) {
            throw 'The source theme manifest no longer matches the fixture.'
        }
        $buttonTheme = Get-Content -LiteralPath (Join-Path $sourceRoot 'src/TKCode.Arc/Buttons/Button/Themes/ButtonTheme.axaml') -Raw
        $tokenIdentities = @([regex]::Matches($buttonTheme, '\{arc:(\w+)TokenResource') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
        if (($tokenIdentities -join ',') -ne 'ArcDesign,Button') { throw 'Button template token dependencies changed.' }
        $patch = Join-Path $PSScriptRoot 'experiment.patch'
        Invoke-Tool git @('-C', $sourceRoot, 'apply', '--check', $patch) (Join-Path $logs "$variant-patch-check.log")
        Invoke-Tool git @('-C', $sourceRoot, 'apply', $patch) (Join-Path $logs "$variant-patch.log")
        $manifest = [ordered]@{
            ThemeTypes = $themeTypes
            ControlTypes = @('TKCode.Arc.Button', 'TKCode.Arc.Calendar', 'TKCode.Arc.BarChart', 'TKCode.Arc.ColorPicker', 'TKCode.Arc.PropertyGrid', 'TKCode.Arc.CodeBlock')
        }
        $manifestPath = Join-Path $variantRoot 'source-types.json'
        $manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $manifestPath
        $hostRoot = Join-Path $variantRoot 'host'
        Copy-Host Probe $hostRoot
        $publish = Join-Path $variantRoot 'publish'
        $selected = $variant -ne 'full'
        $dropLanguages = $variant -in @('selected', 'missing-token')
        $publishArgs = @('publish', (Join-Path $hostRoot 'Probe.csproj'), '-c', 'Release', '-o', $publish,
            "-p:ArcSourceRoot=$sourceRoot", "-p:ArcAuditSelected=$selected", "-p:ArcAuditDropLanguages=$dropLanguages",
            "-p:ArcAuditMissingToken=$($variant -eq 'missing-token')")
        Invoke-Tool dotnet $publishArgs (Join-Path $logs "$variant-publish.log")
        $beforePath = Join-Path $hostRoot 'bin/Release/net10.0/win-x64/TKCode.Arc.dll'
        $beforeJson = & dotnet $inspector $manifestPath $beforePath
        if ($LASTEXITCODE -ne 0) { throw 'Untrimmed assembly inspection failed.' }
        $before = $beforeJson | ConvertFrom-Json
        if ($before.SourceThemeCount -ne $themeTypes.Count -or $before.TrackedTypes.PSObject.Properties.Value -contains $false) {
            throw 'Tracked identities are missing before trimming; do not interpret renaming as trimming.'
        }
        $afterJson = & dotnet $inspector $manifestPath (Join-Path $publish 'TKCode.Arc.dll')
        if ($LASTEXITCODE -ne 0) { throw 'Trimmed assembly inspection failed.' }
        $after = $afterJson | ConvertFrom-Json
        $expectedThemes = if ($selected) { 1 } else { $themeTypes.Count }
        if ($after.SourceThemeCount -ne $expectedThemes) { throw 'Unexpected retained theme identities.' }
        $evidence = Join-Path $variantRoot 'evidence'
        & (Join-Path $publish 'Probe.exe') $evidence *> (Join-Path $logs "$variant-probe.log")
        $exitCode = $LASTEXITCODE
        $validation = Get-Content -LiteralPath (Join-Path $evidence 'validation.json') -Raw | ConvertFrom-Json
        $negative = $variant -eq 'missing-token'
        if ($negative) {
            if ($exitCode -eq 0 -or $validation.Passed -or $validation.Error -notlike 'CHECK FAILED: resource Button.*') {
                throw 'Negative control failed to detect the deliberately omitted Button token registration.'
            }
        }
        else {
            if ($exitCode -ne 0 -or -not $validation.Passed) { throw "Visual validation failed for $variant`: $($validation.Error)" }
            if ($validation.Frames.Count -ne 8) { throw 'Not all eight render states were exercised.' }
            if ($dropLanguages -and $validation.Catalogs.Count -ne 0) { throw 'Unneeded language catalogs remain registered.' }
            if ($selected -and ($validation.TokenIdentities -join ',') -ne 'ArcDesign,Button') { throw 'Unexpected token registration closure.' }
        }
        $files = @(Get-ChildItem -LiteralPath $publish -File -Recurse | Where-Object Extension -NotIn @('.pdb', '.xml'))
        if ($files.Name -match 'StreamMark|Markdig|TextMate|onig') { throw 'Unexpected heavy dependencies after the upstream split.' }
        $dependencies = Get-Content -LiteralPath (Join-Path $publish 'Probe.deps.json') -Raw | ConvertFrom-Json
        $runtimePacks = @($dependencies.libraries.PSObject.Properties.Name | Where-Object { $_ -match 'Microsoft.NETCore.App.Runtime' })
        if ($runtimePacks.Count -ne 1 -or $runtimePacks[0] -notlike ('*/' + $contract.Runtime)) { throw 'Unexpected runtime pack.' }
        $assets = Get-Content -LiteralPath (Join-Path $hostRoot 'obj/project.assets.json') -Raw | ConvertFrom-Json
        $packages = @($assets.libraries.PSObject.Properties | Where-Object { $_.Value.type -eq 'package' } | ForEach-Object { [ordered]@{ Identity = $_.Name; Sha512 = $_.Value.sha512 } })
        $results.Add([pscustomobject][ordered]@{
            Variant = $variant; NegativeControl = $negative; Before = $before; After = $after
            Payload = [ordered]@{ Bytes = [long](($files | Measure-Object Length -Sum).Sum); ExcludedExtensions = @('.pdb', '.xml'); Files = @($files | Sort-Object Name | ForEach-Object { [ordered]@{ Path = [IO.Path]::GetRelativePath($publish, $_.FullName); Bytes = $_.Length; Sha256 = Hash $_.FullName } }) }
            Validation = $validation; ResolvedPackages = $packages; FrameComparison = 'Not compared'
        })
        $report.Results = $results.ToArray()
        $report | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $OutputRoot 'results.json')
    }
    $baseline = @($results | Where-Object Variant -eq 'full')
    if ($baseline.Count -eq 1) {
        foreach ($result in $results | Where-Object { $_.Variant -ne 'full' -and -not $_.NegativeControl }) {
            foreach ($frame in $baseline[0].Validation.Frames) {
                $other = @($result.Validation.Frames | Where-Object Name -eq $frame.Name)
                if ($other.Count -ne 1 -or $other[0].PixelSha256 -ne $frame.PixelSha256) { throw "Rendered pixels differ: $($result.Variant)/$($frame.Name)" }
            }
            $result.FrameComparison = 'All eight pixel buffers match full registration'
        }
    }
    $report.Results = $results.ToArray()
    $report | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $OutputRoot 'results.json')
    $results | Select-Object Variant, NegativeControl, @{ Name = 'MiB'; Expression = { [math]::Round($_.Payload.Bytes / 1MB, 4) } }, FrameComparison | Format-Table -AutoSize
    Write-Host "Results, logs, and PNG evidence: $OutputRoot"
}
finally { Pop-Location }
