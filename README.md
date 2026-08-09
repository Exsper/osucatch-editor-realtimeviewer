# Osucatch Editor Realtime Viewer

A realtime beatmap viewer for beatmap editing (osu!stable editor) in osu!catch.

[![License: MIT](https://img.shields.io/github/license/Exsper/osucatch-editor-realtimeviewer)](LICENSE.txt)

## Download

Download the latest release (x64 / x86 builds):

- <https://github.com/Exsper/osucatch-editor-realtimeviewer/releases/latest>

> The official releases are framework-dependent. The x86 build is intended for 32-bit osu! stable, e.g. when running under Wine on Linux.

## Guides

- [Running under osu-winello on Linux / Wine (English)](docs/osu-winello-guide.en.md)
- [在 Linux / Wine（osu-winello）中运行（中文）](docs/osu-winello-guide.zh-CN.md)

These guides explain how to add the 32-bit build to the osu-winello Wine prefix and launch it together with osu! to monitor the editor in real time.

## Features

- Real-time beatmap preview while editing in the osu!stable editor (osu!catch)
- Reads hit objects, timeline position and selection directly from the editor
- Configurable refresh intervals with foreground/mouse-move aware updates
- Template and bookmark tools, including an optional global hotkey for bookmarks
- Unified user settings and crash logs under a fixed `%LocalAppData%` path

## Requirements

- Windows 10 or later (x64 or x86), or Linux via Wine (see [Guides](#guides))
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) (x86/x64) for framework-dependent official releases
- OpenGL-capable graphics driver
- osu! stable

## Workflow

Editor --(Editor Reader)--> Beatmap --(Osu BeatmapParser)--> HitObjects --(OpenGL)--> Frames

## Used Packages

### for beatmap parser

- System.Numerics.Tensors

### for drawing

- OpenTK
- OpenTK.GLControl

## Used Source Code

### for reading editor

- [Editor Reader](https://github.com/Karoo13/EditorReader)

### for parsing beatmap

- [osu](https://github.com/ppy/osu)
- [osu-framework](https://github.com/ppy/osu-framework)
- [osuTK](https://github.com/ppy/osuTK)

## Contributors

- [Exsper](https://github.com/Exsper) (osu! ID: [Candy](https://osu.ppy.sh/u/2360046))
- [zhangjunyan2580](https://github.com/zhangjunyan2580) (osu! ID: [zhangjunyan](https://osu.ppy.sh/users/12729608))
- [Trent](https://osu.ppy.sh/users/3438241)

## Configuration & Logs

User settings are stored in:

- `%LocalAppData%\OsuCatch-Editor-RealtimeViewer\user.config`

Logs (including crash reports) are written to:

- `%LocalAppData%\OsuCatch-Editor-RealtimeViewer\logs\`
