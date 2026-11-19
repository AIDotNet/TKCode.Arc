# Builds libratex_ffi on Windows (MSVC) from the commit pinned in native/ratex/ratex-upstream.json, with its patches,
# into artifacts/native/<rid>/. Release packages take the Windows libraries from tools/streammark/build-ratex.sh (cargo-zigbuild).
# Usage: tools/streammark/build-ratex.ps1 [-Rid win-x64] [-Target x86_64-pc-windows-msvc]
param([string]$Rid = "win-x64", [string]$Target = "")
$ErrorActionPreference = "Stop"
$root = Resolve-Path "$PSScriptRoot/../.."
$manifest = Get-Content (Join-Path $root "native/ratex/ratex-upstream.json") -Raw | ConvertFrom-Json
$commit = $manifest.commit
$src = Join-Path $root "artifacts/src/ratex-$($commit.Substring(0, 12))"
if (-not (Test-Path (Join-Path $src ".git"))) {
    git init -q $src
    git -C $src remote add origin $manifest.repo
}
git -C $src cat-file -e "$commit^{commit}" 2>$null
if ($LASTEXITCODE -ne 0) { git -C $src fetch -q --depth 1 origin $commit }
git -C $src checkout -q --force $commit
git -C $src clean -qfd
foreach ($patch in $manifest.patches) {
    $file = Join-Path $root "native/ratex/$($patch.file)"
    git -C $src apply --check $file
    if ($LASTEXITCODE -ne 0) { throw "patch does not apply: $($patch.file)" }
    git -C $src apply $file
    Write-Host "applied $($patch.file)"
}
$env:CARGO_TARGET_DIR = Join-Path $root "artifacts/cargo"
$cargoArgs = @("build", "--manifest-path", (Join-Path $src "Cargo.toml"), "-p", $manifest.crate, "--release")
if ($Target) { $cargoArgs += @("--target", $Target) }
& cargo @cargoArgs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$dir = if ($Target) { Join-Path $env:CARGO_TARGET_DIR "$Target/release" } else { Join-Path $env:CARGO_TARGET_DIR "release" }
$out = Join-Path $root "artifacts/native/$Rid"
New-Item -ItemType Directory -Force -Path $out | Out-Null
Copy-Item (Join-Path $dir "ratex_ffi.dll") $out
Set-Content -Path (Join-Path $out "ratex-commit.txt") -Value $commit
Get-ChildItem $out -Filter "*ratex*"
