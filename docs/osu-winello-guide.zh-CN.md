# 在 osu-winello（Linux / Wine）中运行 OsuCatch Editor Realtime Viewer（32 位版）并与 osu! 同时启动

本文面向通过 [osu-winello](https://github.com/NelloKudo/osu-winello) 在 Linux 上安装 osu! stable 的用户，介绍如何把本查看器（32 位版）放进 osu! 所在的 Wine prefix，并让它在 osu! 启动时一起启动，从而实时监控 osu! 的编辑器（editor）。

## 为什么必须用 32 位版、必须用同一个 prefix

- osu! stable 本身是 32 位程序。查看器通过 `ReadProcessMemory` 直接读取 osu!.exe 进程的内存（与 Mapping Tools、gosumemory 读取 osu! 的方式同源），并按 32 位指针模型解析数据。
- Wine 的 prefix 就是一个独立的"虚拟 Windows 环境"。只有**同一个 prefix** 里的进程才能通过同一套 wineserver 互相访问，因此查看器必须和 osu! 在同一个 prefix 下启动。
- 查看器自带的原生加速库 `StableCompatLib.dll` 也是按进程位数提供的：32 位发布包（`release-x86.zip`）里已经包含 x86 版本，无需额外处理。

> osu-winello 的 prefix 是 64 位（win64），但完全兼容运行 32 位程序；用 32 位查看器是为了和 32 位 osu! 进程的指针模型保持一致，这也是官方同时发布 x86/x64 两个包的原因。

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
2. 下载 **`release-x86.zip`**。
3. 解压到 `~/.local/share/osuconfig/osucatch-viewer/`：

```bash
mkdir -p ~/.local/share/osuconfig/osucatch-viewer
unzip release-x86.zip -d ~/.local/share/osuconfig/osucatch-viewer
```

解压后应能看到这些关键文件：

```text
osucatch-viewer/
├── OsuCatch-Editor-RealtimeViewer.exe   ← 主程序
├── OsuCatch-Editor-RealtimeViewer.dll
├── StableCompatLib.dll                  ← x86 原生库
├── OpenTK.dll / OpenTK.GLControl.dll
└── img/ zh-Hans/
```

为什么放在这里：

- `~/.local/share/osuconfig` 位于 `XDG_DATA_HOME` 下，osu-winello 的 Steam Runtime 容器会把它以读写方式挂载，查看器一定能被 Wine 访问到；
- 它不在 prefix 的 C: 盘里，`osu-wine --fixprefix` 重装 prefix 时不会被清掉。

## 第二步：安装 .NET 8 Desktop Runtime（x86）

官方发布包是"框架依赖"版本，不含 .NET 运行时，因此 prefix 里必须装有 **.NET 8 Windows Desktop Runtime（x86）**（查看器需要 `Microsoft.NETCore.App` 与 `Microsoft.WindowsDesktop.App` 8.0）。

### 方法 A（推荐）：用 osu-winello 自带的 winetricks

```bash
osu-wine n --winetricks dotnetdesktop8
```

- 该动词会下载并静默安装 .NET 8 Desktop Runtime 8.0.x 的 x86 版本；由于 prefix 是 64 位，还会顺带装 x64 版本（不影响使用）。
- 如果提示找不到 `dotnetdesktop8` 动词，说明 winetricks 太旧，先更新再重试：

```bash
osu-wine n --winetricks --self-update
osu-wine n --winetricks dotnetdesktop8
```

- 验证安装结果：

```bash
osu-wine n --wine 'C:\Program Files\dotnet\dotnet.exe' --list-runtimes
```

输出里应能看到 `Microsoft.NETCore.App 8.0.x` 和 `Microsoft.WindowsDesktop.App 8.0.x`。

### 方法 B（兜底）：手动解压运行时

如果 winetricks 安装失败，可以手动把运行时解压进 prefix：

1. 打开 <https://dotnet.microsoft.com/en-us/download/dotnet/8.0>；
2. 选择 **Windows → x86 → 下载 ".zip"**（Windows Desktop Runtime 8.0.x win-x86 的 zip 包）；
3. 解压到 prefix 的 C: 盘：

```bash
unzip windowsdesktop-runtime-8.0.x-win-x86.zip \
    -d ~/.local/share/wineprefixes/osu-wineprefix/drive_c/dotnet8
```

4. 让 Wine 里的程序能找到它。新建 `~/.local/share/osuconfig/configs/dotnet8.cfg`：

```bash
echo -e 'DOTNET_ROOT="C:\\dotnet8"\nDOTNET_ROOT_X86="C:\\dotnet8"' \
    > ~/.local/share/osuconfig/configs/dotnet8.cfg
```

> 注意：手动解压在 `drive_c` 里的运行时会在 `osu-wine --fixprefix` 重装 prefix 后被删除，需要重做本步；方法 A 也一样，重装 prefix 后要重新执行一次。查看器本体放在 `osuconfig` 下则不受影响。

## 第三步：创建"osu! + 查看器"联合启动批处理

在 osu! 目录（即 `osu-wine --info` 显示的 osu! folder）里新建 `launch_with_viewer.bat`，内容如下：

```bat
@echo off
cd /d "%~dp0"
start "" osu!.exe %*
start "" "Z:\home\你的用户名\.local\share\osuconfig\osucatch-viewer\OsuCatch-Editor-RealtimeViewer.exe"

:loop
tasklist | find "osu!.exe" >nul
if ERRORLEVEL 1 (
    taskkill /F /IM OsuCatch-Editor-RealtimeViewer.exe
    exit
)
ping -n 5 127.0.0.1 >nul
goto loop
```

把里面的 `你的用户名` 换成实际的 Linux 用户名（`echo $USER` 查看）。

几点说明：

- 文件**不要**叫 `launch_with_memory.bat`——那是 osu-winello 给 gosumemory/tosu 用的文件名，会被它的相关功能覆盖或删除；
- 批处理放在 osu! 目录里，是为了用 `%~dp0` 直接定位 `osu!.exe`，不依赖 C:/D: 盘符映射；
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

- **启动即闪退，或提示找不到 .NET / 缺少运行时**：第二步没装好，用方法 A 重装或改用方法 B；也可以直接看 `logs/` 里的崩溃报告。
- **日志里反复出现 `No Osu!.exe found`**：确认查看器是在同一 prefix 下启动的（用第三步的批处理启动即可），并确认 osu! 已经打开。
- **提示 `No active editor found.`**：先确认已进入编辑器（窗口标题以 `.osu` 结尾）。osu! 更新后内存布局可能变化，请更新查看器到最新发布版。
- **查看器窗口出现但画面不刷新**：编辑器不在前台或鼠标静止时刷新会按低频间隔走，这是正常设计；进入编辑器并移动鼠标即可看到实时刷新。
- **上次异常退出后性能相关设置被自动调整**：程序检测到上次非正常退出时会自动关闭批量渲染，可在设置里重新打开。
- **`osu-wine --fixprefix` 之后查看器起不来**：prefix 被重装，按第二步重新安装 .NET 运行时即可（查看器本体不受影响）。

## 附录（进阶）：自己发布自包含 x86 版，免装运行时

如果不想往 prefix 里装运行时，可以在 Linux 上用 .NET 8 SDK 交叉发布自包含版本：

```bash
git clone https://github.com/Exsper/osucatch-editor-realtimeviewer.git
cd osucatch-editor-realtimeviewer

# StableCompatLib.dll（x86）没有提交到仓库，先从官方 release-x86.zip 里取一份
mkdir -p StableCompatLib/x86
unzip -j release-x86.zip StableCompatLib.dll -d StableCompatLib/x86

dotnet publish osucatch-editor-realtimeviewer \
    -c Release -r win-x86 --self-contained true -p:Platform=x86 \
    -o ~/osucatch-viewer-x86
```

注意：`dotnet publish` 的发布结果**不会自动带上** `StableCompatLib.dll`，需要手动补一份：

```bash
unzip -j release-x86.zip StableCompatLib.dll -d ~/osucatch-viewer-x86
```

最后把发布目录覆盖到查看器目录（保留批处理里的路径不变）：

```bash
cp -r ~/osucatch-viewer-x86/. ~/.local/share/osuconfig/osucatch-viewer/
```

自包含版不需要第二步的任何操作，直接按第三步启动即可。

## 相关链接

- 查看器仓库：<https://github.com/Exsper/osucatch-editor-realtimeviewer>
- 查看器发布页（`release-x86.zip`）：<https://github.com/Exsper/osucatch-editor-realtimeviewer/releases/latest>
- osu-winello：<https://github.com/NelloKudo/osu-winello>
- .NET 8 下载页：<https://dotnet.microsoft.com/en-us/download/dotnet/8.0>
