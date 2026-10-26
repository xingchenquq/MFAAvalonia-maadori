using Avalonia.Media;
using Avalonia.Threading;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

    /// <summary>
    /// 当前未开放的任务 entry（来自 title_status.json 的 closed 列表）。
    /// task 列表项在构造时读它，所以「重载资源后重建的列表」也能带上标记。
    /// </summary>
    private static readonly HashSet<string> Closed = new(StringComparer.OrdinalIgnoreCase);

    public static bool IsClosed(string entry) => Closed.Contains(entry);

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

    /// <summary>
    /// 读文件（纯数据，只有字符串）→ **回到 UI 线程**构造画刷并写 ViewModel。
    ///
    /// 两步必须分开：Timer 回调跑在线程池上，而 Avalonia 属性变更与
    /// <see cref="SolidColorBrush"/> 的构造都要求 UI 线程 —— 早先整段都在后台线程，
    /// 结果被框架拦成 <c>Call from invalid thread</c>，标题纹丝不动（2026-10-26 实机）。
    /// </summary>
    private static void Apply()
    {
        try
        {
            var (text, colorHex, closed) = Read();
            ApplyTaskAvailability(closed);
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    IBrush? brush = Color.TryParse(colorHex, out var parsed)
                        ? new SolidColorBrush(parsed)
                        : null;
                    Instances.RootViewModel.SetActivityTitle(text, brush);
                }
                catch (Exception ex)
                {
                    LoggerHelper.Warning($"活动标题应用失败：{ex.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            LoggerHelper.Warning($"活动标题应用失败：{ex.Message}");
        }
    }

    private static (string Text, string ColorHex, List<string> Closed) Read()
    {
        try
        {
            var file = Path.Combine(AppContext.BaseDirectory, FolderName, FileName);
            if (!File.Exists(file)) return (string.Empty, string.Empty, new List<string>());

            var json = JObject.Parse(File.ReadAllText(file));
            var text = (json["text"]?.ToString() ?? string.Empty).Trim();
            var color = (json["color"]?.ToString() ?? string.Empty).Trim();
            var closed = json["closed"]?.Values<string>()
                ?.Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v!.Trim())
                .ToList() ?? new List<string>();
            return text.Length == 0
                ? (string.Empty, string.Empty, closed)
                : (text, color, closed);
        }
        catch (Exception ex)
        {
            // 文件正在被写（半截 JSON）也算异常——静默降级，下次事件会再来一遍。
            LoggerHelper.Warning($"活动标题读取失败（本次不显示）：{ex.Message}");
            return (string.Empty, string.Empty, new List<string>());
        }
    }

    /// <summary>
    /// 把 closed 列表落到任务列表上（UI 线程）。标记为不可用的任务在界面上变淡 + 加删除线。
    ///
    /// 只改视觉、不禁用勾选：真要强跑的用户还有 `activity.strict: false` 这条路，
    /// 而任务入口的 ActivityGate 仍会在 3 秒内拦下未开放的任务。
    /// </summary>
    private static void ApplyTaskAvailability(List<string> closed)
    {
        Closed.Clear();
        foreach (var entry in closed) Closed.Add(entry);

        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                var task = Instances.InstanceTabBarViewModel.ActiveTab?.TaskQueueViewModel;
                if (task == null) return;
                foreach (var item in task.TaskItemViewModels)
                {
                    var entry = item.InterfaceItem?.Entry;
                    item.IsUnavailable = !string.IsNullOrEmpty(entry) && Closed.Contains(entry);
                }
            }
            catch (Exception ex)
            {
                LoggerHelper.Warning($"任务可用性标记失败：{ex.Message}");
            }
        });
    }
}
