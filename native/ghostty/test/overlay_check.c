// 验证 overlay O1–O4 的行为（M0 spike；之后由 TKCode.Terminal.Core.Tests 覆盖）。
// 构建：clang -I<prefix>/include overlay_check.c -L<prefix>/lib -lghostty-vt -Wl,-rpath,<prefix>/lib
#include <ghostty/vt.h>
#include <stdio.h>
#include <string.h>

bool tkcode_ghostty_modify_other_keys_2(GhosttyTerminal t);
void tkcode_ghostty_set_clipboard_read_deferred(GhosttyTerminal t, bool deferred);
uint8_t tkcode_ghostty_clipboard_read_terminator(GhosttyTerminal t);

static char out[4096];
static size_t out_len;
static int clipboard_requests;

static void on_write(GhosttyTerminal t, void *ud, const uint8_t *d, size_t n) {
    (void)t; (void)ud;
    if (out_len + n < sizeof out) { memcpy(out + out_len, d, n); out_len += n; }
}
static void on_clipboard_read(GhosttyTerminal t, void *ud, const GhosttyClipboardRead *r) {
    (void)t; (void)ud; (void)r;
    clipboard_requests++;  // 不应答：模拟"稍后由 host 应答"
}

static void write_str(GhosttyTerminal t, const char *s) { ghostty_terminal_vt_write(t, (const uint8_t *)s, strlen(s)); }
static bool mode(GhosttyTerminal t, uint16_t m) {
    GhosttyTerminalModeConfig c = { .mode = ghostty_mode_new(m, false) };
    ghostty_terminal_get(t, GHOSTTY_TERMINAL_DATA_MODE, &c);
    return c.value;
}
static int failures;
static void expect(bool ok, const char *what) { printf("%s %s\n", ok ? "PASS" : "FAIL", what); if (!ok) failures++; }
static bool reply_is(const char *s) { bool ok = out_len == strlen(s) && memcmp(out, s, out_len) == 0; out_len = 0; return ok; }

int main(void) {
    GhosttyTerminal t;
    if (ghostty_terminal_new(NULL, &t, 80, 24) != GHOSTTY_SUCCESS) return 2;
    ghostty_terminal_set(t, GHOSTTY_TERMINAL_OPT_WRITE_PTY, (const void *)on_write);

    // O1：win32-input-mode 9001
    expect(!mode(t, 9001), "O1 9001 默认关闭");
    write_str(t, "\x1b[?9001h");
    expect(mode(t, 9001), "O1 CSI ?9001h 后为开");
    write_str(t, "\x1b[?9001$p");
    expect(reply_is("\x1b[?9001;1$y"), "O1 DECRQM 9001 → 1");
    write_str(t, "\x1b" "c");
    expect(!mode(t, 9001), "O1 RIS 后为关");

    // O2：Kitty 粘贴事件 5522 被禁用（即使安装了剪贴板读取回调）
    ghostty_terminal_set(t, GHOSTTY_TERMINAL_OPT_CLIPBOARD_READ, (const void *)on_clipboard_read);
    write_str(t, "\x1b[?5522h");
    expect(!mode(t, 5522), "O2 CSI ?5522h 不生效");
    write_str(t, "\x1b[?5522$p");
    expect(reply_is("\x1b[?5522;0$y"), "O2 DECRQM 5522 → 0（未识别）");

    // O3：延后应答
    write_str(t, "\x1b]52;c;?\x07");
    expect(clipboard_requests == 1 && reply_is("\x1b]52;c;\x07"), "O3 默认：回调未应答时库回空剪贴板");
    tkcode_ghostty_set_clipboard_read_deferred(t, true);
    write_str(t, "\x1b]52;c;?\x1b\\");
    expect(clipboard_requests == 2 && out_len == 0, "O3 deferred：回调未应答时不输出");
    expect(tkcode_ghostty_clipboard_read_terminator(t) == 0, "O3 记录终止符 ST");
    write_str(t, "\x1b]52;c;?\x07");
    expect(tkcode_ghostty_clipboard_read_terminator(t) == 1, "O3 记录终止符 BEL");
    out_len = 0;

    // O4：modifyOtherKeys 2
    expect(!tkcode_ghostty_modify_other_keys_2(t), "O4 默认关");
    write_str(t, "\x1b[>4;2m");
    expect(tkcode_ghostty_modify_other_keys_2(t), "O4 CSI >4;2m 后为开");
    write_str(t, "\x1b[>4;0m");
    expect(!tkcode_ghostty_modify_other_keys_2(t), "O4 CSI >4;0m 后为关");

    ghostty_terminal_free(t);
    printf("%s (%d failures)\n", failures ? "FAILED" : "ALL PASSED", failures);
    return failures ? 1 : 0;
}
