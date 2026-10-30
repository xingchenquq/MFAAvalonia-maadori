using Avalonia.Media;
using Avalonia.Threading;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
    private const string FileName = "title_status.json";

    /// <summary>文件写入常触发多次事件（大小 + 最后写入时间），300ms 内合并成一次。</summary>
    private const int DebounceMs = 300;

    private static readonly IReadOnlySet<string> EmptyClosed =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 当前未开放的任务 entry（来自 title_status.json 的 closed 列表）。
    ///
    /// **读写线程不同，所以用不可变快照整体替换，而不是就地 Clear/Add。**
    /// 写入在 Timer 回调（线程池），读取在 <see cref="DragItemViewModel"/> 构造（UI 线程）；
    /// 就地改 <see cref="HashSet{T}"/> 会让读者偶发看到清空到一半的集合，
    /// 表现为「标记莫名其妙闪一下或整个消失」（2026-10-30 修复）。
    /// </summary>
    private static volatile IReadOnlySet<string> _closed = EmptyClosed;

    /// <summary>活动配色画刷缓存。只在 UI 线程访问，无需加锁。</summary>
    private static readonly Dictionary<string, IBrush?> BrushCache = new(StringComparer.OrdinalIgnoreCase);

    public static bool IsClosed(string entry) => _closed.Contains(entry);

    private static FileSystemWatcher? _watcher;
    private static Timer? _debounce;
    private static readonly object Gate = new();
    private static bool _tabBarHooked;

    public static void Start()
    {
        if (_watcher != null) return;

        lock (Gate)
        {
            if (_watcher != null) return;

            HookActiveTabChanged();

            try
            {
                var folder = AppPaths.CacheDirectory;
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                _watcher = new FileSystemWatcher(folder, FileName)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                    EnableRaisingEvents = true
                };
                _watcher.Changed += (_, __) => ScheduleApply();
                _watcher.Created += (_, __) => ScheduleApply();
                _watcher.Renamed += (_, __) => ScheduleApply();
                LoggerHelper.Info($"已启动活动标题监听：{FileName}");
            }
            catch (Exception ex)
            {
                LoggerHelper.Warning($"活动标题监听启动失败（标题将不显示活动）：{ex.Message}");
            }
        }

        Apply();
    }

    /// <summary>
    /// 停掉监听与防抖定时器。静态持有的 <see cref="FileSystemWatcher"/> / <see cref="Timer"/>
    /// 都是 <see cref="IDisposable"/>，此前没有释放入口，做「重启 GUI」这类操作会留下
    /// 一串失效的 watcher（2026-10-30 补）。
    /// </summary>
    public static void Stop()
    {
        lock (Gate)
        {
            _debounce?.Dispose();
            _debounce = null;

            if (_watcher != null)
            {
                try { _watcher.EnableRaisingEvents = false; } catch { /* 已释放 */ }
                _watcher.Dispose();
                _watcher = null;
            }

            if (_tabBarHooked)
            {
                try { Instances.InstanceTabBarViewModel.PropertyChanged -= OnTabBarPropertyChanged; }
                catch { /* 服务已销毁，忽略 */ }
                _tabBarHooked = false;
            }
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

            // closed 为 null 表示「本次没读到有效内容」（半截 JSON / 文件被占用），
            // 此时保留上一次的任务标记，不要清空。
            if (closed != null)
                ApplyTaskAvailability(closed);

            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    Instances.RootViewModel.SetActivityTitle(text, ResolveBrush(colorHex));
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

    /// <summary>取缓存的画刷。此前每次 Apply 都 new 一个，值没变也会触发重绘。</summary>
    private static IBrush? ResolveBrush(string colorHex)
    {
        if (!BrushCache.TryGetValue(colorHex, out var brush))
        {
            brush = Color.TryParse(colorHex, out var parsed) ? new SolidColorBrush(parsed) : null;
            BrushCache[colorHex] = brush;
        }

        return brush;
    }

    /// <remarks>
    /// 返回的 <c>Closed</c> 为 <c>null</c> = 「读取失败，别动现有标记」；
    /// 非 null（可能是空集）= 「读到了，按这个结果落地」，文件不存在算空集。
    /// </remarks>
    private static (string Text, string ColorHex, IReadOnlySet<string>? Closed) Read()
    {
        try
        {
            var file = Path.Combine(AppPaths.CacheDirectory, FileName);
            if (!File.Exists(file)) return (string.Empty, string.Empty, EmptyClosed);

            var json = JObject.Parse(File.ReadAllText(file));
            var text = (json["text"]?.ToString() ?? string.Empty).Trim();
            var color = (json["color"]?.ToString() ?? string.Empty).Trim();
            var closed = new HashSet<string>(
                json["closed"]?.Values<string>()
                    ?.Where(v => !string.IsNullOrWhiteSpace(v))
                    .Select(v => v!.Trim())
                ?? Enumerable.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);

            return text.Length == 0
                ? (string.Empty, string.Empty, closed)
                : (text, color, closed);
        }
        catch (Exception ex)
        {
            // 文件正在被写（半截 JSON）也算异常。此时**保留上一次的标记**：
            // 早先返回空列表会走到 Closed.Clear()，agent 每次写文件的瞬间
            // 都会把所有「变淡 + 删除线」抹掉再恢复，肉眼可见地闪（2026-10-30 修复）。
            LoggerHelper.Warning($"活动标题读取失败（保留上次任务标记）：{ex.Message}");
            return (string.Empty, string.Empty, null);
        }
    }

    /// <summary>
    /// 把 closed 列表落到**所有实例**的任务列表上（UI 线程）。标记为不可用的任务在界面上变淡 + 加删除线。
    ///
    /// 只改视觉、不禁用勾选：真要强跑的用户还有 `activity.strict: false` 这条路，
    /// 而任务入口的 ActivityGate 仍会在 3 秒内拦下未开放的任务。
    /// </summary>
    private static void ApplyTaskAvailability(IReadOnlySet<string> closed)
    {
        _closed = closed;

        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                // 遍历所有 instance tab，而不是只刷 ActiveTab：此前切到另一个
                // 实例页签时，那边的列表没有标记（2026-10-30 修复）。
                foreach (var tab in Instances.InstanceTabBarViewModel.Tabs.ToList())
                {
                    var queue = tab.TaskQueueViewModel;
                    if (queue == null) continue;

                    foreach (var item in queue.TaskItemViewModels.ToList())
                    {
                        var entry = item.InterfaceItem?.Entry;
                        item.IsUnavailable = !string.IsNullOrEmpty(entry) && closed.Contains(entry);
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerHelper.Warning($"任务可用性标记失败：{ex.Message}");
            }
        });
    }

    /// <summary>
    /// 切换实例页签时补一次标记：新建的 <see cref="DragItemViewModel"/> 会在构造时自检，
    /// 但已经存在的项不会重算，切过去就是「旧的没标记、新的有标记」的混合状态（2026-10-30 修复）。
    /// </summary>
    private static void HookActiveTabChanged()
    {
        if (_tabBarHooked) return;

        try
        {
            Instances.InstanceTabBarViewModel.PropertyChanged += OnTabBarPropertyChanged;
            _tabBarHooked = true;
        }
        catch (Exception ex)
        {
            // 启动早期 tab bar 可能还没解析出来；此时靠 DragItemViewModel 构造时自检兜底。
            LoggerHelper.Debug($"活动标记：未能订阅页签切换（{ex.Message}）");
        }
    }

    private static void OnTabBarPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // 用 Instances.xxx.ActiveTab 取名字，避免为了 nameof 再引一个 ViewModels 命名空间
        // ——Helper 层对 ViewModels 的依赖是待清理的历史债，不要再加新的。
        if (e.PropertyName != nameof(Instances.InstanceTabBarViewModel.ActiveTab)) return;
        ApplyTaskAvailability(_closed);
    }
}
