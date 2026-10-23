using System;
using System.Linq;
using System.Reflection;

namespace MFAAvalonia.Helper;

/// <summary>
/// 品牌能力表：按构建时的 <c>-p:Brand=&lt;X&gt;</c> 决定哪些定制功能对当前产物可用。
/// </summary>
/// <remarks>
/// 品牌值来自程序集元数据 <c>ProductBrand</c>（由 <c>Directory.Build.props</c> 注入），
/// 运行时读取，**不是**编译期 <c>#if</c> 开关。原因见 <c>docs/构建与品牌约定.md</c>：
/// 需要区分的只有"界面是否出现某功能 + 该功能是否生效"，用运行期判定即可，
/// 而 <c>#if</c> 要动 4 个文件（含无法条件编译的 XAML），维护成本远高于收益。
///
/// 空 <c>Brand</c>（不带 <c>-p:Brand</c> 的默认构建）表示**兼容模式**：
/// 保持引入品牌化之前的行为，即所有定制能力全开。这保证老构建脚本与老产物链路不断。
/// </remarks>
public static class BrandCapabilities
{
    /// <summary>当前产物所属品牌；空串表示默认（兼容）构建。</summary>
    public static string Brand { get; } = ReadBrand();

    /// <summary>QQ 机器人通知渠道（Maadori 定制）。</summary>
    public static bool QqBotNotify { get; } =
        Brand.Length == 0 || string.Equals(Brand, "Maadori", StringComparison.OrdinalIgnoreCase);

    /// <summary>演出成绩图推送（依赖 QQ 通道）。</summary>
    public static bool ResultImagePush => QqBotNotify;

    /// <summary>运行日志渲染成图片发送（依赖 QQ 通道）。</summary>
    public static bool LogImage => QqBotNotify;

    private static string ReadBrand()
    {
        try
        {
            return typeof(BrandCapabilities).Assembly
                       .GetCustomAttributes<AssemblyMetadataAttribute>()
                       .FirstOrDefault(a => string.Equals(a.Key, "ProductBrand", StringComparison.Ordinal))
                       ?.Value
                   ?? string.Empty;
        }
        catch
        {
            // 元数据缺失（理论上不该发生）按兼容模式处理，宁可多显示也不要让功能凭空消失
            return string.Empty;
        }
    }
}
