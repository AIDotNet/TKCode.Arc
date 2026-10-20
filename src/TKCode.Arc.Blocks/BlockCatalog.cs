namespace TKCode.Arc;

/// <summary>A Blocks group: the sidebar category a block is filed under, with its code and English and Chinese titles.</summary>
/// <param name="Code">The group's folder name under <c>Blocks/</c>.</param>
public sealed record BlockGroup(string Code, string Title, string ChineseTitle);

/// <summary>The Blocks taxonomy, in sidebar order. The groups exist as folders; no block is implemented yet.</summary>
public static class BlockCatalog
{
    public static readonly BlockGroup[] Groups =
    [
        new("Analytics", "Analytics", "分析"),
        new("Finance", "Finance", "财务"),
        new("LineCharts", "Line charts", "折线图"),
        new("Ai", "AI", "AI"),
        new("AppShell", "App shell", "应用外壳"),
        new("Auth", "Auth", "认证"),
        new("Settings", "Settings", "设置"),
        new("People", "People", "人员"),
        new("Developer", "Developer", "开发者"),
        new("Pricing", "Pricing", "定价"),
        new("Commerce", "Commerce", "电商"),
        new("Support", "Support", "支持"),
        new("Media", "Media", "媒体"),
        new("Projects", "Projects", "项目"),
        new("Heroes", "Heroes", "首屏"),
        new("Features", "Features", "功能"),
        new("SocialProof", "Social proof", "社会证明"),
        new("Content", "Content", "内容"),
        new("Showpieces", "Showpieces", "展示"),
    ];
}
