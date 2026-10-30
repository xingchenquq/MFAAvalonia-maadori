using System.IO;
using MFAAvalonia.Helper;
using Xunit;

namespace MFAAvalonia.Tests.Helper;

/// <summary>
/// 路径约定。
///
/// 目录一律由 <see cref="AppPaths"/> 派生自 <see cref="AppPaths.DataRoot"/>，
/// 不允许各处硬拼 <see cref="System.AppContext.BaseDirectory"/> —— 一旦
/// <c>DataRoot</c> 支持独立目录（代码里已有 <c>IsUsingIndependentDataRoot</c>），
/// 硬拼的路径会静默读到错误位置，而且不报错。
/// </summary>
public class AppPathsTests
{
    [Fact]
    public void CacheDirectory_IsUnderDataRoot()
    {
        Assert.Equal(Path.Combine(AppPaths.DataRoot, "cache"), AppPaths.CacheDirectory);
    }

    [Fact]
    public void StandardDirectories_AreUnderDataRoot()
    {
        Assert.Equal(Path.Combine(AppPaths.DataRoot, "resource"), AppPaths.ResourceDirectory);
        Assert.Equal(Path.Combine(AppPaths.DataRoot, "agent"), AppPaths.AgentDirectory);
        Assert.Equal(Path.Combine(AppPaths.DataRoot, "interface.json"), AppPaths.InterfaceJsonPath);
    }

    [Fact]
    public void DataRoot_DefaultsToInstallRoot()
    {
        // Initialize() 之前两者必须一致；Initialize 之后 DataRoot 才可能被改写
        Assert.False(string.IsNullOrWhiteSpace(AppPaths.InstallRoot));
        Assert.False(string.IsNullOrWhiteSpace(AppPaths.DataRoot));
        Assert.StartsWith(AppPaths.DataRoot, AppPaths.CacheDirectory, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InstanceConfigPath_UsesConfigInstancesDirectory()
    {
        Assert.EndsWith(Path.Combine("config", "instances"), AppPaths.InstancesDirectory, System.StringComparison.OrdinalIgnoreCase);
    }
}
