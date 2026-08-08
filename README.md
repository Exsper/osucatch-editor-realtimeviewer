# Osucatch Editor Realtime Viewer

A realtime beatmap viewer for beatmap editing (osu!stable editor) in osu!catch.

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

## 日志 / 崩溃日志

程序每次运行都会把启动过程和日志写入 `%LocalAppData%\OsuCatch-Editor-RealtimeViewer\logs\`（不依赖“显示控制台”开关）：

- `log_yyyyMMdd.log`：当天运行日志，包含启动里程碑（即使不开控制台也记录），卡死/崩溃后能看出程序走到了哪一步。
- `crash_yyyyMMdd_HHmmss_fff.log`：发生未处理异常时生成的崩溃报告，包含系统信息、当前设置、异常与堆栈、崩溃前的最近日志。
- `crash_latest.log`：最近一次崩溃报告的副本，方便直接打开。

崩溃时程序会弹窗提示日志位置。把 `logs` 目录里的相关文件发给开发者即可定位问题；旧报告最多保留 20 份，单日日志超 5MB 自动截断尾部。
