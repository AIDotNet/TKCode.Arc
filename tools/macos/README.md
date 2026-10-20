# macOS packaging, signing and notarization

`.github/workflows/gallery-macos.yml` builds the Gallery desktop app (`gallery/TKCode.Arc.Gallery.Desktop`) for
`osx-arm64` and `osx-x64`, signs it with a Developer ID certificate, notarizes and staples it, and ships a `.dmg`.
The scripts here do the work, so the same flow runs locally.

| File | Purpose |
|---|---|
| `bundle.sh` | `dotnet publish` output (single file) to an unsigned `.app` with `Info.plist`. |
| `sign-notarize.sh` | Signs every Mach-O file and the bundle (hardened runtime), builds and signs the `.dmg`, notarizes and staples it. |
| `Entitlements.plist` | Hardened runtime exceptions a self-contained .NET app needs (JIT, executable memory). |

The app is published as a single file on purpose: `codesign` treats every file in `Contents/MacOS` as nested code, so
loose managed `.dll` files would each need their own signature. `bundle.sh` fails if anything but Mach-O files is left there.

## Parameters

Non-secret values are plain `env:` entries at the top of `gallery-macos.yml`:

| Variable | Value | Meaning |
|---|---|---|
| `APPLE_ID` | `token@routin.ai` | Apple ID that `notarytool` signs in with. It must belong to the developer team below. |
| `APPLE_TEAM_ID` | `42B564FHYL` | Team ID of the certificate. |
| `MACOS_SIGNING_IDENTITY` | `Developer ID Application: jiale he (42B564FHYL)` | Must match the certificate in the `.p12`. |
| `APP_NAME` | `TKCode Arc Gallery` | Bundle name and display name. |
| `BUNDLE_ID` | `ai.routin.tkcode.arc.gallery` | `CFBundleIdentifier`. |

## Secrets

Add these under Settings > Secrets and variables > Actions (never commit the `.p12`; `*.p12` is git-ignored):

| Secret | Content |
|---|---|
| `MACOS_CERTIFICATE_P12_BASE64` | The Developer ID Application certificate with its private key, exported as `.p12`, base64-encoded. |
| `MACOS_CERTIFICATE_PASSWORD` | The password set when exporting the `.p12`. |
| `APPLE_APP_SPECIFIC_PASSWORD` | An app-specific password for `APPLE_ID` (appleid.apple.com > Sign-In and Security). |

With the GitHub CLI, from a clone of the repository:

```bash
base64 -i /path/to/certificate.p12 | gh secret set MACOS_CERTIFICATE_P12_BASE64
gh secret set MACOS_CERTIFICATE_PASSWORD          # prompts for the value
gh secret set APPLE_APP_SPECIFIC_PASSWORD         # prompts for the value
```

## When it runs

| Trigger | Result |
|---|---|
| Push of a `v*` tag | Signed and notarized dmgs attached to a GitHub release (`-` in the tag marks it a prerelease). The run fails if a secret is missing, so an unsigned build is never released. |
| Manual run (`workflow_dispatch`) | Dmgs as workflow artifacts. Signed when the secrets exist; the `notarize` input turns notarization off. |
| Pull request touching `tools/macos/**` or the workflow | Unsigned dry run. Pull requests never receive the secrets. |

To release: bump `ArcVersion` in `build/Versions.props` (it becomes the app version), then push a matching tag, for example
`git tag v0.1.0-preview.1 && git push origin v0.1.0-preview.1`.

## Local dry run

Ad-hoc signing (`-`) exercises the whole script without a certificate or notarization:

```bash
dotnet publish gallery/TKCode.Arc.Gallery.Desktop -c Release -f net10.0 -r osx-arm64 \
    --self-contained true -p:PublishSingleFile=true -o /tmp/arc-publish
app="$(tools/macos/bundle.sh /tmp/arc-publish /tmp/arc-app | tail -n 1)"
MACOS_SIGNING_IDENTITY=- tools/macos/sign-notarize.sh "$app" /tmp/arc-app/Gallery.dmg
```

With the real certificate in your login keychain, set `MACOS_SIGNING_IDENTITY` to its name instead; add `APPLE_ID`,
`APPLE_TEAM_ID` and `APPLE_APP_SPECIFIC_PASSWORD` to notarize as well.
