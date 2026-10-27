/* TKCode overlay 扩展（native/ghostty/overlays/0002，docs/terminal/02-core-ghostty.md §2.4）。
 * 声明格式与上游头文件一致，供 tools/terminal/ghostty_bindgen.py 生成绑定。 */
#ifndef TKCODE_GHOSTTY_H
#define TKCODE_GHOSTTY_H

#include <ghostty/vt.h>

#ifdef __cplusplus
extern "C" {
#endif

/** O3：开启后，OSC 52 读剪贴板的回调返回时如未应答，不再自动回空剪贴板，由宿主稍后自行写回应答。 */
GHOSTTY_API void tkcode_ghostty_set_clipboard_read_deferred(GhosttyTerminal terminal, bool deferred);

/** O3：最近一次剪贴板读取请求的终止符：0 = ST，1 = BEL。 */
GHOSTTY_API uint8_t tkcode_ghostty_clipboard_read_terminator(GhosttyTerminal terminal);

/** O4：xterm modifyOtherKeys 2 是否开启（CSI > 4 ; 2 m）。 */
GHOSTTY_API bool tkcode_ghostty_modify_other_keys_2(GhosttyTerminal terminal);

#ifdef __cplusplus
}
#endif

#endif
