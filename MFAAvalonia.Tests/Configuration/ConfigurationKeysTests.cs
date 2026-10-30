using MFAAvalonia.Configuration;
using Xunit;

namespace MFAAvalonia.Tests.Configuration;

/// <summary>
/// 实例作用域键的分类。
///
/// 这几个键一旦从 <see cref="ConfigurationKeys.InstanceScopedKeys"/> 里漏掉，
/// 多实例就会写同一份全局配置，表现为「切个页签，另一个实例的控制器/资源被改掉」。
/// 这类问题在界面上看不出、日志里也没有，只能靠回归测试挡。
/// </summary>
public class ConfigurationKeysTests
{
    [Theory]
    [InlineData(ConfigurationKeys.TaskItems)]
    [InlineData(ConfigurationKeys.CurrentTasks)]
    [InlineData(ConfigurationKeys.Resource)]
    [InlineData(ConfigurationKeys.AdbDevice)]
    [InlineData(ConfigurationKeys.CurrentController)]
    [InlineData(ConfigurationKeys.ControllerOptionItems)]
    [InlineData(ConfigurationKeys.ResourceOptionItems)]
    public void CriticalKeys_AreInstanceScoped(string key)
    {
        Assert.True(ConfigurationKeys.IsInstanceScoped(key), $"'{key}' 必须按实例隔离");
    }

    [Theory]
    [InlineData("")]
    [InlineData("Not.A.Real.Key")]
    [InlineData("Instances.List")]
    [InlineData("Instances.Order")]
    public void NonInstanceKeys_AreNotScoped(string key)
    {
        Assert.False(ConfigurationKeys.IsInstanceScoped(key));
    }

    [Fact]
    public void InstanceScopedKeys_ContainNoBlankEntries()
    {
        // 空键会被所有 key 命中（如果 key 也传空），先挡掉
        Assert.All(ConfigurationKeys.InstanceScopedKeys, key => Assert.False(string.IsNullOrWhiteSpace(key)));
    }
}
