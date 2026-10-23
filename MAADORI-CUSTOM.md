# MFAAvalonia（Maadori 定制版）

本仓库是 [Maadori](https://github.com/xingchenquq/Maadori)（邦多利国服自动清火助手）所使用的图形界面外壳源码。

Maadori 的分发包内含 `MFAAvalonia.Core.dll`（约 3.3 MB），是在上游 [MFAAvalonia](https://github.com/MaaXYZ/MFAAvalonia) 基础上定制编译的产物。按上游 **GPLv3** 许可要求，修改后的源码在此公开。

## 基线

| 项目 | 值 |
|---|---|
| 上游 | `MaaXYZ/MFAAvalonia` |
| 基线版本 | `v2.16.1`（commit `4f11c81`） |
| 定制分支 | `maadori-custom` |

基线 commit 与 Maadori 运行版的 `ProductVersion` hash 一致。升级上游前请先确认这一点。

## 定制内容

### 外部通知

- 新增 **QQ 机器人**渠道（AppID + AppSecret，凭据本地加密存储，代码中无硬编码密钥）
- 通知文案结构化：标题与正文分两行，按渠道分别格式化
- 任务结束 / 出错时附带截图
- 运行日志渲染成图片发送，规避文字消息的字数上限（文字 25 行 / 900 字 → 图片 200 行 / 30000 字）

### 成绩图推送

- 成绩图监听循环改为**静态单例、应用生命周期常驻**。原实现的循环挂在实例上，多 `instanceId` 场景下同一张成绩图会被每个实例各推一次（2026-10-12 实测重复发送的根因）
- `sent/`、`failed/` 归档目录 7 天 TTL 修剪（每小时节流），每曲 PNG 约 1–2 MB，不修剪会无限增长
- 画面缓存：最小更新间隔 1.5 s、任务期每 3 s 主动刷新。任务结束后 controller 会被内核置为 inactive、届时取不到画面，故保留最近一次成功截图的字节缓存

### 其它

- 启动设置项、四语本地化（简体为中性资源，无 `zh-Hans` 卫星目录）、应用图标替换

详细变更记录见 [`docs/开发记录.md`](docs/开发记录.md)。

## 构建

```bash
dotnet build MFAAvalonia.Desktop/MFAAvalonia.Desktop.csproj -c Release
```

产物在仓库根 `bin/AnyCPU/Release/`。

**注意**：真正生效的程序集是 `MFAAvalonia.Core.dll`（约 3.3 MB）；`MFAAvalonia.dll`（约 179 KB）只是启动器壳。验证改动是否编译进去，必须搜前者。本地化资源在 `MFAAvalonia.Core.resources.dll`（按 `en-US/`、`ja-JP/`、`zh-Hant/` 分子目录），简体中文编进主 dll。

## 同步上游

```bash
git fetch origin            # origin 指向上游 MaaXYZ/MFAAvalonia
git rebase origin/master    # 在 maadori-custom 分支上
```

`mine` remote 指向本仓库。上游改动了 `MaaProcessor.cs` / `ExternalNotificationHelper.cs` 这类文件时冲突概率较高——这两处正是定制最集中的地方。

## 许可

上游为 **GPLv3**，本定制版沿用同一许可，全文见 [LICENSE](LICENSE)。分发编译产物时须同时提供本源码。
