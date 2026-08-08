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

## Configuration & Logs

User settings are stored in:

- `%LocalAppData%\OsuCatch-Editor-RealtimeViewer\user.config`

Logs (including crash reports) are written to:

- `%LocalAppData%\OsuCatch-Editor-RealtimeViewer\logs\`
