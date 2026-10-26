using Avalonia.Media;
using MFAAvalonia.Extensions.MaaFW;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Threading;

namespace MFAAvalonia.Helper;

/// <summary>
/// 活动标题徽标（Maadori 定制，2026-10-26）。
///
/// 在窗口标题栏 <c>custom_title</c> 后面显示「当期活动名 · 剩余时间」，并用活动配色着色。
///
/// 为什么数据从文件读、而不是写进 interface.json：agent 是 MFA 启动后拉起的子进程，
/// 它拿到档期时标题早已渲染完。用 <see cref="FileSystemWatcher"/> 盯着
/// <c>cache/title_status.json</c>，agent 一写标题就刷新，不必等下次启动。
/// 文件缺失 / 内容损坏 / 文本为空 一律视为「不显示徽标」，绝不抛错影响主界面。
/// </summary>
public static class ActivityTitleWatcher
{
    private const string FolderName = "cache";
    private const string FileName = "title_status.json";

    /// <summary>文件写入常触发多次事件（大小 + 最后写入时间），300ms 内合并成一次。</summary>
    private const int DebounceMs = 300;

    private static FileSystemWatcher? _watcher;
    private static Timer? _debounce;
    private static readonly object Gate = new();

    public static void Start()
    {
        Apply();
        if (_watcher != null) return;
        try
        {
            var folder = Path.Combine(AppContext.BaseDirectory, FolderName);
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            _watcher = new FileSystemWatcher(folder, FileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                EnableRaisingEvents = true
            };
            _watcher.Changed += (_, __) => ScheduleApply();
            _watcher.Created += (_, __) => ScheduleApply();
            _watcher.Renamed += (_, __) => ScheduleApply();
            LoggerHelper.Info("已启动活动标题监听：cache/title_status.json");
        }
        catch (Exception ex)
        {
            LoggerHelper.Warning($"活动标题监听启动失败（标题将不显示活动）：{ex.Message}");
        }
    }

    private static void ScheduleApply()
    {
        lock (Gate)
        {
            _debounce?.Dispose();
            _debounce = new Timer(_ => Apply(), null, DebounceMs, Timeout.Infinite);
        }
    }

    private static void Apply()
    {
        try
        {
            var (text, brush) = Read();
            Instances.RootViewModel.SetActivityTitle(text, brush);
        }
        catch (Exception ex)
        {
            LoggerHelper.Warning($"活动标题应用失败：{ex.Message}");
        }
    }

    private static (string Text, IBrush? Brush) Read()
    {
        try
        {
            var file = Path.Combine(AppContext.BaseDirectory, FolderName, FileName);
            if (!File.Exists(file)) return (string.Empty, null);

            var json = JObject.Parse(File.ReadAllText(file));
            var text = (json["text"]?.ToString() ?? string.Empty).Trim();
            if (text.Length == 0) return (string.Empty, null);

            var color = (json["color"]?.ToString() ?? string.Empty).Trim();
            IBrush? brush = Color.TryParse(color, out var parsed) ? new SolidColorBrush(parsed) : null;
            return (text, brush);
        }
        catch (Exception ex)
        {
            // 文件正在被写（半截 JSON）也算异常——静默降级，下次事件会再来一遍。
            LoggerHelper.Warning($"活动标题读取失败（本次不显示）：{ex.Message}");
            return (string.Empty, null);
        }
    }
}
