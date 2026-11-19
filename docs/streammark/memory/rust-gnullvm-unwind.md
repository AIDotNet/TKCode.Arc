---
name: rust-gnullvm-unwind
description: Rust 1.100 起 win-arm64（windows-gnullvm）用 cargo-zigbuild 链接缺 _Unwind_* 符号，发布构建因此固定 Rust 版本
type: project
updated: 2026-11-19
source: session
evidence: [native/toolchain.json:rust, tools/streammark/build-ratex.sh, .github/workflows/ci.yml:native, "CI run 44579859613 Native (linux)"]
---
2026-11-19 第一次在 CI 上发布 RaTeX（`v1.3.0-preview.2`）时，`Native (linux)` 在 win-arm64（`aarch64-pc-windows-gnullvm`）链接失败：`lld-link: error: undefined symbol: _Unwind_Resume`、`_GCC_specific_handler` 等。原因是 CI 装的 stable 已是 Rust 1.100.0，它给 gnullvm 传 `--unwindlib=libunwind` 并期望外部提供 libunwind，而 zig cc 没有带上；Rust 1.99.1 正常，与 cargo-zigbuild（0.23.4 / 0.24.1）和 Zig（0.16 / 0.17）的版本无关，本机用 1.100.0 可复现。之前本机验证通过，是因为本机还是 1.99。

**Why:** 发布构建跟着 stable 浮动，上游一次升级就让发版失败，而且只在发版时才跑到。
**How to apply:** Rust 与 cargo-zigbuild 的版本固定在 `native/toolchain.json`，`build-ratex.sh <rid>` 与 CI 的 native 作业都按它选用。升级 Rust 前先在本机用新版本跑 `tools/streammark/build-ratex.sh win-arm64`；能链接再改 pin。
