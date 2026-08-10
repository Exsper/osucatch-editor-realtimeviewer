# 在 osu-winello（Linux / Wine）中运行 OsuCatch Editor Realtime Viewer（32 位版）并与 osu! 同时启动

本文面向通过 [osu-winello](https://github.com/NelloKudo/osu-winello) 在 Linux 上安装 osu! stable 的用户，介绍如何把本查看器（32 位版）放进 osu! 所在的 Wine prefix，并让它在 osu! 启动时一起启动，从而实时监控 osu! 的编辑器（editor）。

## 默认路径速查

先用下面的命令确认你自己的路径（后续步骤会用到）：

```bash
osu-wine --info
```

osu-winello 默认的路径如下（可用 `osu-wine --info` 输出确认）：

| 项目 | 默认位置 |
| --- | --- |
| Wine prefix | `~/.local/share/wineprefixes/osu-wineprefix` |
| osu! 目录 | 见 `cat ~/.local/share/osuconfig/osupath` |
| Wine（yawl 容器包装） | `~/.local/share/osuconfig/yawl-winello` |
| 配置文件目录 | `~/.local/share/osuconfig/configs/` |

---

## 第一步：下载并解压 32 位版

1. 打开发布页：<https://github.com/Exsper/osucatch-editor-realtimeviewer/releases/latest>
2. 下载 **`release-x86-self-contained.zip`**（自包含版）。osu-winello / Linux Wine 用户必须使用自包含版；`release-x86.zip`（框架依赖版）只用于 Windows 本机。
3. 解压到 `~/.local/share/osuconfig/osucatch-viewer/`：

```bash
mkdir -p ~/.local/share/osuconfig/osucatch-viewer
unzip release-x86-self-contained.zip -d ~/.local/share/osuconfig/osucatch-viewer
```

解压后应能看到这些关键文件：

```text
osucatch-viewer/
├── OsuCatch-Editor-RealtimeViewer.exe   ← 主程序
├── OsuCatch-Editor-RealtimeViewer.dll
├── StableCompatLib.dll                  ← x86 原生库
├── GdiPlus.dll                          ← Win7 版 GDI+（GDI+ 1.1），绕过 prefix 里的旧版 GDI+
├── OpenTK.dll / OpenTK.GLControl.dll
└── img/ zh-Hans/
```

为什么放在这里：

- `~/.local/share/osuconfig` 位于 `XDG_DATA_HOME` 下，osu-winello 的 Steam Runtime 容器会把它以读写方式挂载，查看器一定能被 Wine 访问到；
- 它不在 prefix 的 C: 盘里，`osu-wine --fixprefix` 重装 prefix 时不会被清掉。
- 与 exe 同目录的 `GdiPlus.dll` 会被优先加载（程序目录优先于系统目录），所以查看器会用它而不是 prefix 里 osu-winello 装的旧版 GDI+；这也是 osu-winello 下必须用自包含版的原因。

## 第二步：创建"osu! + 查看器"联合启动批处理

在 osu! 目录（即 `osu-wine --info` 显示的 osu! folder）里新建 `launch_with_viewer.bat`，内容如下：

```bat
@echo off
cd /d "%~dp0"
start "" osu!.exe %*
start "" "Z:\home\你的用户名\.local\share\osuconfig\osucatch-viewer\OsuCatch-Editor-RealtimeViewer.exe"
```

把里面的 `你的用户名` 换成实际的 Linux 用户名（`echo $USER` 查看）。

几点说明：

- 文件**不要**叫 `launch_with_memory.bat`——那是 osu-winello 给 gosumemory/tosu 用的文件名，会被它的相关功能覆盖或删除；
- 批处理放在 osu! 目录里，是为了用 `%~dp0` 直接定位 `osu!.exe`，不依赖 C:/D: 盘符映射；
- 查看器目录里自带的 `GdiPlus.dll`（Win7 版 GDI+ 1.1，自包含版已包含）会在启动时被优先加载，从而绕开 prefix 里 osu-winello 为 osu! 安装的旧版 GDI+（`gdiplus_winxp`）。
- 如果你的 HOME 不是 `/home/<用户名>`（比如自定义过），先用这条命令查出查看器在 Wine 里的真实路径，再填进批处理：

```bash
osu-wine n --wine winepath -w \
    ~/.local/share/osuconfig/osucatch-viewer/OsuCatch-Editor-RealtimeViewer.exe
```

### 启动：一条命令同时拉起 osu! 和查看器

```bash
osu-wine n --wine "$(cat ~/.local/share/osuconfig/osupath)/launch_with_viewer.bat"
```

这样 osu! 和查看器会在同一个 prefix、同一个 Wine 会话里启动；关闭 osu! 后，批处理会自动把查看器一并结束。

### 可选：封装成常用命令

新建 `~/.local/bin/osu-with-viewer`：

```bash
#!/usr/bin/env bash
exec osu-wine n --wine "$(cat ~/.local/share/osuconfig/osupath)/launch_with_viewer.bat"
```

然后：

```bash
chmod +x ~/.local/bin/osu-with-viewer
```

以后直接运行 `osu-with-viewer` 即可。

## 使用说明

1. 用上面的命令启动（或直接运行 `osu-wine` 后手动另开终端启动查看器，只要在同一 prefix 下都可以）；
2. 在 osu! 中进入任意一张图的编辑器（editor）；
3. 查看器窗口会自动开始实时渲染预览；只有窗口标题以 `.osu` 结尾（即编辑器界面）时它才会读取数据，正常游玩界面不工作。

## 配置与日志（排错用）

查看器的配置和日志写在 Wine 的 `%LocalAppData%` 下，对应 Linux 路径：

```text
~/.local/share/wineprefixes/osu-wineprefix/drive_c/users/<你的用户名>/AppData/Local/OsuCatch-Editor-RealtimeViewer/
├── user.config   ← 设置
└── logs/         ← 运行日志、崩溃报告
```

## 常见问题

- **启动即闪退，或提示找不到 .NET / 缺少运行时**：请改用自包含版 `release-x86-self-contained.zip`（已内置 .NET 运行时）；如果仍在使用框架依赖版，则按第二步安装运行时。也可以直接看 `logs/` 里的崩溃报告。
- **日志里反复出现 `No Osu!.exe found`**：确认查看器是在同一 prefix 下启动的（用第三步的批处理启动即可），并确认 osu! 已经打开。
- **启动查看器报 `Current version of GDI+ does not support this feature`（或 `Gdip` 类型初始化异常）后闪退**：osu-winello prefix 里的 `gdiplus_winxp`（旧版 GDI+ 1.0）与 .NET 8 的 System.Drawing（需要 GDI+ 1.1）不兼容。请使用自包含版 `release-x86-self-contained.zip`（已内置 Win7 版 `GdiPlus.dll`），并确认查看器目录里有 `GdiPlus.dll`；不要设置 `WINEDLLOVERRIDES=gdiplus=b`。
- **提示 `No active editor found.`**：先确认已进入编辑器（窗口标题以 `.osu` 结尾）。osu! 更新后内存布局可能变化，请更新查看器到最新发布版。
- **查看器窗口出现但画面不刷新**：编辑器不在前台或鼠标静止时刷新会按低频间隔走，这是正常设计；进入编辑器并移动鼠标即可看到实时刷新。
- **上次异常退出后性能相关设置被自动调整**：程序检测到上次非正常退出时会自动关闭批量渲染，可在设置里重新打开。
- **`osu-wine --fixprefix` 之后查看器起不来**：prefix 被重装不影响查看器本体（它位于 `osuconfig` 下，自带的运行时与 `GdiPlus.dll` 也不会被清掉）；若仍启动失败，确认使用的是自包含版。

## 相关链接

- 查看器仓库：<https://github.com/Exsper/osucatch-editor-realtimeviewer>
- osu-winello：<https://github.com/NelloKudo/osu-winello>
