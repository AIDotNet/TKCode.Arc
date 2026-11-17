# Vendored Zig packages

Copies of ghostty's build dependencies whose hosts rate-limit or time out on CI runners. `tools/terminal/build-ghostty.sh`
puts each one into the Zig package cache (`zig fetch <file>`) before it builds, so `zig build` finds the package by its hash
and does not download it.

Each file is named after the Zig package hash in ghostty's `build.zig.zon`. Zig checks that hash when it fetches the
file, so a copy that does not match the pinned dependency fails loudly instead of building something else.

| File | Package | Upstream | License |
|---|---|---|---|
| `translate_c-0.0.0-Q_BUWhVNBwDOEcIqub4VFPJPB6D9dgwzUMHTX5KWr8Xr.tar.gz` | translate-c (`pkg/translate-c` of ghostty) | `https://codeberg.org/vancluever/translate-c/archive/4e879eb8aba615de112eabd1231ea6e01920cead.tar.gz` | MIT (Zig contributors) |

These are build-time tools only: nothing from them ships in a package. When the ghostty pin
(`native/ghostty/ghostty-upstream.json`) moves to a dependency with a new hash, replace the file: copy it from a local Zig
cache (`$ZIG_GLOBAL_CACHE_DIR/p/<hash>.tar.gz`) after a build that could reach the host.
