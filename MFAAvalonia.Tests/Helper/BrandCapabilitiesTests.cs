using System;
using MFAAvalonia.Helper;
using Xunit;

namespace MFAAvalonia.Tests.Helper;

/// <summary>
/// 品牌能力表。这是唯一能在「任何构建参数下都成立」的断言集合：
/// 测试进程读到的 <see cref="BrandCapabilities.Brand"/> 取决于
/// 被测程序集的 <c>AssemblyMetadata(ProductBrand)</c>，因此断言不写死具体品牌，
/// 只校验「不变量」。
/// </summary>
public class BrandCapabilitiesTests
{
    [Fact]
    public void Brand_IsNullOrKnownValue()
    {
        var brand = BrandCapabilities.Brand;

        // 空串 = 兼容模式（不带 -p:Brand 的构建）；非空必须是合法品牌之一
        Assert.True(
            brand.Length == 0
            || brand == "Maadori"
            || brand == "Maaon"
            || brand == "MaaEOV",
            $"未知品牌 '{brand}'：说明 AssemblyMetadata(ProductBrand) 注入了未登记的值");
    }

    [Fact]
    public void QqBotNotify_IsOnForCompatibilityModeAndMaadori()
    {
        var brand = BrandCapabilities.Brand;
        var expected = brand.Length == 0
                       || string.Equals(brand, "Maadori", StringComparison.OrdinalIgnoreCase);

        Assert.Equal(expected, BrandCapabilities.QqBotNotify);
    }

    /// <summary>
    /// ResultImagePush / LogImage 都依赖 QQ 通知通道，不能让它们独立于 QqBotNotify 打开
    /// —— 否则会出现在没有通知渠道的产物里推送图片的空跑逻辑。
    /// </summary>
    [Fact]
    public void DerivedCapabilities_FollowQqBotNotify()
    {
        Assert.Equal(BrandCapabilities.QqBotNotify, BrandCapabilities.ResultImagePush);
        Assert.Equal(BrandCapabilities.QqBotNotify, BrandCapabilities.LogImage);
    }
}
