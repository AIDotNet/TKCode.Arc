using System.Collections.Frozen;

namespace StreamMark.Core.Parsing;

/// <summary>
/// 常用 GitHub / Unicode Emoji 短码表（对应 Markdig <c>UseEmojiAndSmiley(enableSmileys: false)</c>）。
/// 静态只读 <see cref="FrozenDictionary{TKey, TValue}"/>，零反射、NativeAOT 安全。
/// </summary>
internal static class EmojiShortcodeTable
{
    private static readonly FrozenDictionary<string, string> s_emojis = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        // Faces & expressions
        ["smile"] = "😄",
        ["grinning"] = "😀",
        ["smiley"] = "😃",
        ["grin"] = "😁",
        ["laughing"] = "😆",
        ["satisfied"] = "😆",
        ["sweat_smile"] = "😅",
        ["rofl"] = "🤣",
        ["joy"] = "😂",
        ["slightly_smiling_face"] = "🙂",
        ["upside_down_face"] = "🙃",
        ["wink"] = "😉",
        ["blush"] = "😊",
        ["innocent"] = "😇",
        ["heart_eyes"] = "😍",
        ["star_struck"] = "🤩",
        ["kissing_heart"] = "😘",
        ["yum"] = "😋",
        ["stuck_out_tongue"] = "😛",
        ["stuck_out_tongue_winking_eye"] = "😜",
        ["zany_face"] = "🤪",
        ["thinking"] = "🤔",
        ["zipper_mouth_face"] = "🤐",
        ["raised_eyebrow"] = "🤨",
        ["neutral_face"] = "😐",
        ["expressionless"] = "😑",
        ["no_mouth"] = "😶",
        ["smirk"] = "😏",
        ["unamused"] = "😒",
        ["roll_eyes"] = "🙄",
        ["grimacing"] = "😬",
        ["relieved"] = "😌",
        ["pensive"] = "😔",
        ["sleepy"] = "😪",
        ["sleeping"] = "😴",
        ["mask"] = "😷",
        ["sunglasses"] = "😎",
        ["nerd_face"] = "🤓",
        ["confused"] = "😕",
        ["worried"] = "😟",
        ["slightly_frowning_face"] = "🙁",
        ["frowning_face"] = "☹️",
        ["open_mouth"] = "😮",
        ["hushed"] = "😯",
        ["astonished"] = "😲",
        ["flushed"] = "😳",
        ["pleading_face"] = "🥺",
        ["cry"] = "😢",
        ["sob"] = "😭",
        ["scream"] = "😱",
        ["sweat"] = "😓",
        ["disappointed"] = "😞",
        ["angry"] = "😠",
        ["rage"] = "😡",
        ["triumph"] = "😤",
        ["exploding_head"] = "🤯",
        ["partying_face"] = "🥳",
        ["skull"] = "💀",
        ["poop"] = "💩",
        ["hankey"] = "💩",
        ["ghost"] = "👻",
        ["robot"] = "🤖",

        // Hands & gestures
        ["+1"] = "👍",
        ["thumbsup"] = "👍",
        ["-1"] = "👎",
        ["thumbsdown"] = "👎",
        ["ok_hand"] = "👌",
        ["wave"] = "👋",
        ["clap"] = "👏",
        ["raised_hands"] = "🙌",
        ["pray"] = "🙏",
        ["handshake"] = "🤝",
        ["muscle"] = "💪",
        ["point_right"] = "👉",
        ["point_left"] = "👈",
        ["point_up"] = "☝️",
        ["point_down"] = "👇",
        ["v"] = "✌️",
        ["crossed_fingers"] = "🤞",
        ["eyes"] = "👀",
        ["brain"] = "🧠",

        // Hearts & symbols
        ["heart"] = "❤️",
        ["orange_heart"] = "🧡",
        ["yellow_heart"] = "💛",
        ["green_heart"] = "💚",
        ["blue_heart"] = "💙",
        ["purple_heart"] = "💜",
        ["black_heart"] = "🖤",
        ["white_heart"] = "🤍",
        ["broken_heart"] = "💔",
        ["sparkling_heart"] = "💖",
        ["100"] = "💯",
        ["sparkles"] = "✨",
        ["star"] = "⭐",
        ["star2"] = "🌟",
        ["zap"] = "⚡",
        ["boom"] = "💥",
        ["fire"] = "🔥",
        ["rainbow"] = "🌈",
        ["sunny"] = "☀️",
        ["cloud"] = "☁️",
        ["umbrella"] = "☔",
        ["snowflake"] = "❄️",

        // Status, checkmarks & alerts
        ["white_check_mark"] = "✅",
        ["heavy_check_mark"] = "✔️",
        ["ballot_box_with_check"] = "☑️",
        ["x"] = "❌",
        ["negative_squared_cross_mark"] = "❎",
        ["heavy_multiplication_x"] = "✖️",
        ["warning"] = "⚠️",
        ["no_entry"] = "⛔",
        ["no_entry_sign"] = "🚫",
        ["prohibited"] = "🚫",
        ["question"] = "❓",
        ["grey_question"] = "❔",
        ["exclamation"] = "❗",
        ["heavy_exclamation_mark"] = "❗",
        ["grey_exclamation"] = "❕",
        ["bangbang"] = "‼️",
        ["interrobang"] = "⁉️",
        ["information_source"] = "ℹ️",
        ["sos"] = "🆘",
        ["red_circle"] = "🔴",
        ["orange_circle"] = "🟠",
        ["yellow_circle"] = "🟡",
        ["green_circle"] = "🟢",
        ["blue_circle"] = "🔵",
        ["purple_circle"] = "🟣",
        ["black_circle"] = "⚫",
        ["white_circle"] = "⚪",
        ["large_blue_diamond"] = "🔷",
        ["large_orange_diamond"] = "🔶",
        ["small_blue_diamond"] = "🔹",
        ["small_orange_diamond"] = "🔸",

        // Dev, tools, objects & celebration
        ["rocket"] = "🚀",
        ["tada"] = "🎉",
        ["confetti_ball"] = "🎊",
        ["trophy"] = "🏆",
        ["medal_sports"] = "🏅",
        ["crown"] = "👑",
        ["gem"] = "💎",
        ["bulb"] = "💡",
        ["memo"] = "📝",
        ["pencil"] = "📝",
        ["pencil2"] = "✏️",
        ["book"] = "📖",
        ["books"] = "📚",
        ["bookmark"] = "🔖",
        ["link"] = "🔗",
        ["paperclip"] = "📎",
        ["pushpin"] = "📌",
        ["round_pushpin"] = "📍",
        ["scissors"] = "✂️",
        ["file_folder"] = "📁",
        ["open_file_folder"] = "📂",
        ["page_facing_up"] = "📄",
        ["clipboard"] = "📋",
        ["calendar"] = "📆",
        ["bar_chart"] = "📊",
        ["chart_with_upwards_trend"] = "📈",
        ["chart_with_downwards_trend"] = "📉",
        ["package"] = "📦",
        ["inbox_tray"] = "📥",
        ["outbox_tray"] = "📤",
        ["email"] = "✉️",
        ["envelope"] = "✉️",
        ["speech_balloon"] = "💬",
        ["thought_balloon"] = "💭",
        ["bell"] = "🔔",
        ["no_bell"] = "🔕",
        ["loudspeaker"] = "📢",
        ["mega"] = "📣",
        ["mag"] = "🔍",
        ["mag_right"] = "🔎",
        ["lock"] = "🔒",
        ["unlock"] = "🔓",
        ["key"] = "🔑",
        ["shield"] = "🛡️",
        ["wrench"] = "🔧",
        ["hammer"] = "🔨",
        ["hammer_and_wrench"] = "🛠️",
        ["gear"] = "⚙️",
        ["nut_and_bolt"] = "🔩",
        ["bug"] = "🐛",
        ["beetle"] = "🪲",
        ["computer"] = "💻",
        ["desktop_computer"] = "🖥️",
        ["keyboard"] = "⌨️",
        ["iphone"] = "📱",
        ["globe_with_meridians"] = "🌐",
        ["earth_americas"] = "🌎",
        ["earth_asia"] = "🌏",
        ["earth_africa"] = "🌍",
        ["hourglass"] = "⌛",
        ["hourglass_flowing_sand"] = "⏳",
        ["stopwatch"] = "⏱️",
        ["alarm_clock"] = "⏰",
        ["clock1"] = "🕐",
        ["construction"] = "🚧",
        ["rotating_light"] = "🚨",
        ["recycle"] = "♻️",
        ["arrow_right"] = "➡️",
        ["arrow_left"] = "⬅️",
        ["arrow_up"] = "⬆️",
        ["arrow_down"] = "⬇️",
        ["arrow_upper_right"] = "↗️",
        ["arrow_lower_right"] = "↘️",
        ["arrows_counterclockwise"] = "🔄",
        ["fast_forward"] = "⏩",
        ["rewind"] = "⏪",
        ["play_or_pause_button"] = "⏯️",
        ["stop_button"] = "⏹️",
        ["checkered_flag"] = "🏁",
        ["triangular_flag_on_post"] = "🚩",
        ["coffee"] = "☕",
        ["beer"] = "🍺",
        ["pizza"] = "🍕",
        ["apple"] = "🍎",
        ["cake"] = "🎂",
        ["gift"] = "🎁",
        ["balloon"] = "🎈",
        ["art"] = "🎨",
        ["musical_note"] = "🎵",
        ["notes"] = "🎶",
        ["camera"] = "📷",
        ["video_camera"] = "📹",
        ["tv"] = "📺",
        ["battery"] = "🔋",
        ["electric_plug"] = "🔌",
        ["moneybag"] = "💰",
        ["dollar"] = "💵",
        ["credit_card"] = "💳",
        ["dart"] = "🎯",
        ["seedling"] = "🌱",
        ["evergreen_tree"] = "🌲",
        ["herb"] = "🌿",
        ["four_leaf_clover"] = "🍀",
        ["dog"] = "🐶",
        ["cat"] = "🐱",
        ["penguin"] = "🐧",
        ["whale"] = "🐳",
        ["unicorn"] = "🦄",
        ["bee"] = "🐝",
        ["octopus"] = "🐙",
        ["zap_fast"] = "⚡",
        ["new"] = "🆕",
        ["free"] = "🆓",
        ["cool"] = "🆒",
        ["ok"] = "🆗",
        ["up"] = "🆙",
        ["zero"] = "0️⃣",
        ["one"] = "1️⃣",
        ["two"] = "2️⃣",
        ["three"] = "3️⃣",
        ["four"] = "4️⃣",
        ["five"] = "5️⃣",
        ["six"] = "6️⃣",
        ["seven"] = "7️⃣",
        ["eight"] = "8️⃣",
        ["nine"] = "9️⃣",
        ["keycap_ten"] = "🔟",
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>尝试在 <paramref name="slice"/> 开头（以 ':' 开始）匹配一个 Emoji 短码（如 <c>:smile:</c>）。</summary>
    public static bool TryMatch(ReadOnlySpan<char> slice, out string emoji, out int consumed)
    {
        emoji = string.Empty;
        consumed = 0;
        if (slice.Length < 3 || slice[0] != ':')
        {
            return false;
        }
        var max = Math.Min(slice.Length, 40);
        var close = -1;
        for (var i = 1; i < max; i++)
        {
            var c = slice[i];
            if (c == ':')
            {
                close = i;
                break;
            }
            if (!(char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '+'))
            {
                return false;
            }
        }
        if (close <= 1)
        {
            return false;
        }
        var name = slice.Slice(1, close - 1);
#if NET9_0_OR_GREATER
        if (s_emojis.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(name, out var found))
        {
            emoji = found;
            consumed = close + 1;
            return true;
        }
#else
        if (s_emojis.TryGetValue(name.ToString(), out var found))
        {
            emoji = found;
            consumed = close + 1;
            return true;
        }
#endif
        return false;
    }
}
