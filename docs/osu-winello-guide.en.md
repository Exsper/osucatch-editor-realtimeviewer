# Running OsuCatch Editor Realtime Viewer (32-bit) with osu! under osu-winello (Linux / Wine)

This guide is for users who installed osu! stable on Linux with [osu-winello](https://github.com/NelloKudo/osu-winello). It explains how to add this viewer (32-bit build) to the same Wine prefix that osu! uses, launch it together with osu!, and monitor the osu! editor in real time.

> Why the 32-bit **legacy** self-contained build and the same prefix: osu! stable is a 32-bit program and the viewer reads its memory directly via `ReadProcessMemory`, so the bitness must match and the viewer must run inside the same Wine prefix as osu!. The current EditorReader implementation cannot locate the editor under Wine, so the `-legacy` package ships the previous EditorReader implementation instead; it also bundles the .NET 8 runtime and the GDI+ fix, so no runtime installation into the prefix is needed.

## Quick reference: default paths

First, confirm your own paths with this command (you will need them in the steps below):

```bash
osu-wine --info
```

osu-winello's defaults are as follows (verify with `osu-wine --info`):

| Item | Default location |
| --- | --- |
| Wine prefix | `~/.local/share/wineprefixes/osu-wineprefix` |
| osu! folder | see `cat ~/.local/share/osuconfig/osupath` |
| Wine (yawl container wrapper) | `~/.local/share/osuconfig/yawl-winello` |
| Config files directory | `~/.local/share/osuconfig/configs/` |

---

## Step 1: Download and extract the 32-bit build

1. Open the releases page: <https://github.com/Exsper/osucatch-editor-realtimeviewer/releases/latest>
2. Download **`release-x86-self-contained-legacy.zip`**. osu-winello / Wine on Linux must use this package: it contains the **previous EditorReader implementation**, because the current one does not work under Wine. The `release-x86.zip` (framework-dependent) and `release-x86-self-contained.zip` packages are for Windows only.
3. Extract it to `~/.local/share/osuconfig/osucatch-viewer/`:

```bash
mkdir -p ~/.local/share/osuconfig/osucatch-viewer
unzip release-x86-self-contained-legacy.zip -d ~/.local/share/osuconfig/osucatch-viewer
```

After extraction you should see these key files:

```text
osucatch-viewer/
├── OsuCatch-Editor-RealtimeViewer.exe   ← main program (legacy EditorReader)
├── OsuCatch-Editor-RealtimeViewer.dll
├── StableCompatLib.dll                  ← x86 native library
├── GdiPlus.dll                          ← Windows 7 GDI+ (GDI+ 1.1), bypasses the legacy GDI+ in the prefix
├── OpenTK.dll / OpenTK.GLControl.dll
└── img/ zh-Hans/
```

Why this location:

- `~/.local/share/osuconfig` lives under `XDG_DATA_HOME`, which osu-winello's Steam Runtime container mounts read-write, so Wine is guaranteed to be able to access the viewer;
- It is not inside the prefix's C: drive, so it survives `osu-wine --fixprefix` reinstalls.
- The `GdiPlus.dll` next to the executable is loaded first (the application directory takes precedence over system directories), so the viewer uses it instead of the legacy GDI+ installed by osu-winello; this is why the self-contained build is required under osu-winello.

## Step 2: Create a combined "osu! + viewer" launcher batch file

In the osu! folder (the "osu! folder" shown by `osu-wine --info`), create `launch_with_viewer.bat` with the following content:

```bat
@echo off
cd /d "%~dp0"
start "" osu!.exe %*
start "" "Z:\home\YOUR_USERNAME\.local\share\osuconfig\osucatch-viewer\OsuCatch-Editor-RealtimeViewer.exe"
```

Replace `YOUR_USERNAME` with your actual Linux username (check with `echo $USER`).

A few notes:

- Do **not** name the file `launch_with_memory.bat` — that name is used by osu-winello for gosumemory/tosu and can be overwritten or removed by its features;
- The batch file lives in the osu! folder so that `%~dp0` can locate `osu!.exe` directly, without relying on C:/D: drive mappings;
- The bundled `GdiPlus.dll` (Windows 7 GDI+ 1.1, included in the self-contained legacy build) is loaded first at startup, bypassing the legacy GDI+ (`gdiplus_winxp`) that osu-winello installs into the prefix for osu!. Do **not** set `WINEDLLOVERRIDES=gdiplus=b` — that would force Wine's built-in GDI+ and bypass the bundled fix DLL;
- Do **not** add a `/D` switch to the viewer's `start` line — Wine's cmd has parsing issues with `start /d "path"` that can prevent the viewer from launching. Newer builds resolve textures relative to the executable directory, so the working directory does not matter.
- If your HOME is not `/home/<username>` (for example if you customized it), get the viewer's real path inside Wine first, then use it in the batch:

```bash
osu-wine n --wine winepath -w \
    ~/.local/share/osuconfig/osucatch-viewer/OsuCatch-Editor-RealtimeViewer.exe
```

### Launch: start osu! and the viewer together with one command

```bash
osu-wine n --wine "$(cat ~/.local/share/osuconfig/osupath)/launch_with_viewer.bat"
```

This starts osu! and the viewer in the same prefix and the same Wine session; when you close osu!, the batch file automatically shuts the viewer down as well.

### Optional: wrap it into a regular command

Create `~/.local/bin/osu-with-viewer`:

```bash
#!/usr/bin/env bash
exec osu-wine n --wine "$(cat ~/.local/share/osuconfig/osupath)/launch_with_viewer.bat"
```

Then:

```bash
chmod +x ~/.local/bin/osu-with-viewer
```

From now on, just run `osu-with-viewer`.

## Usage

### First launch: select the osu! folder

On first launch, if the program cannot find the osu! path automatically (via the registry), it shows a **Select osu! Folder** dialog (the selected folder must contain `osu!.exe`). Under osu-wine, the osu! install folder is mapped by osu-winello to the virtual **D: drive**, so simply select **D:\**. You can also browse through **Z:** and pick the real Linux path (e.g. `~/.local/share/osu`). The path is saved in the settings and the dialog will not appear again; if you move osu! later with `osu-wine --changedir`, reselect it in Settings → osu! folder.

> Backup, bookmarks (BookmarkPlus) and templates depend on this osu path.

### Normal usage

1. Launch with the command above (or run `osu-wine` and start the viewer from another terminal — anything works as long as it is under the same prefix);
2. Enter the editor for any beatmap in osu!;
3. The viewer window will start rendering the live preview automatically. It only reads data while the window title ends with `.osu` (i.e. the editor screen); it does not work on the normal gameplay screen.

## Configuration and logs (for troubleshooting)

The viewer's settings and logs are written under Wine's `%LocalAppData%`, which maps to:

```text
~/.local/share/wineprefixes/osu-wineprefix/drive_c/users/<YOUR_USERNAME>/AppData/Local/OsuCatch-Editor-RealtimeViewer/
├── user.config   ← settings
└── logs/         ← run logs, crash reports
```

## FAQ

- **The viewer crashes on startup, or reports missing .NET / missing runtime**: use the legacy self-contained package `release-x86-self-contained-legacy.zip` (the .NET runtime is bundled); if you are still using the framework-dependent build, install the .NET 8 Desktop Runtime (x86) in the prefix yourself (`osu-wine n --winetricks dotnetdesktop8`). You can also check the crash report under `logs/`.
- **The viewer says `No active editor found.`, or the log keeps showing `Editor needs Reload.`, and no preview is ever drawn**: you are most likely running a build with the current EditorReader, which does not work under Wine. Download `release-x86-self-contained-legacy.zip` (legacy EditorReader) and replace your existing files with it. Also confirm that you are actually in the editor (window title ending in `.osu`).
- **The viewer freezes / becomes unresponsive**: first try Settings → **Restart Program**. If it is still stuck after a restart, this is the known freeze issue of the legacy EditorReader, which is **currently unfixed under Wine** — we are sorry about this; the freeze fix could only be applied to the Windows builds. Please open an issue and attach the log files (see the last entry), as reports are what may eventually make a Wine fix possible.
- **The window fills the whole desktop on startup, the title bar does not show maximized, and it cannot be resized**: older builds could accidentally save the maximized window size as the normal size, so the next launch opened the window at an oversized value. This is fixed (the size is clamped to the virtual screen bounds at startup, and maximized sizes are no longer saved). If you are already affected, close the viewer and edit `user.config` — set `Window_Width`/`Window_Height` back to smaller values (e.g. 250 / 750) and `Window_Maximized` to `False`; or delete the file to restore defaults.
- **The log keeps showing `No Osu!.exe found`**: make sure the viewer is launched from the same prefix (use the batch file from Step 2) and that osu! is already running.
- **The viewer crashes on startup with `Current version of GDI+ does not support this feature` (or a `Gdip` type-initializer exception)**: the legacy GDI+ (`gdiplus_winxp`, GDI+ 1.0) installed by osu-winello in the prefix is incompatible with .NET 8's System.Drawing (which requires GDI+ 1.1). Use the legacy self-contained package `release-x86-self-contained-legacy.zip` (bundles the Windows 7 `GdiPlus.dll`) and make sure `GdiPlus.dll` is present next to the executable; do not set `WINEDLLOVERRIDES=gdiplus=b`.
- **`No active editor found.` is shown**: check the entry about `Editor needs Reload.` above — under Wine this usually means you are running a build with the current EditorReader instead of the legacy one. After an osu! update the in-memory layout may change as well, in which case update to the latest release.
- **The viewer gets stuck at `Try fetch editor` with no further log output**: this is a known memory-scan compatibility issue of the legacy EditorReader under Wine, and it is currently unfixed. As a workaround, try Settings → **Restart Program**; if that does not help, restart both osu! and the viewer. Logs from a reproduction are very welcome.
- **The viewer exits immediately with no logs when cachy (wine-osu-cachy) is enabled**: osu-winello's cachy wine (an experimental wow64 build) is incompatible with .NET 8, so the viewer (a .NET 8 self-contained build) cannot start. Keep the default wine-osu and do **not** set `WINE_USE_CACHY="true"` (that option is only recommended for tools like Mapping Tools, which are based on .NET 6).
- **The viewer window appears but the picture does not refresh**: a beatmap is static, so the only moving thing on screen is the object you are dragging/placing — and its current position can only be read from the editor's memory, so the `Full read` interval is effectively the frame rate of that object following your mouse. While the editor is not in the foreground or the mouse is idle, full reads drop to the low-frequency interval — this is by design; enter the editor and move the mouse (for example drag an object) to see real-time updates. The status bar tells you which state the viewer is in: `Editor is not running` (test mode / song select — normal, the last known map stays on screen), `Re-binding editor...` (the editor address is being re-scanned in the background), or `Editor read failed, retrying` (reads keep failing; the viewer retries with backoff and keeps drawing the last good data).
- **Performance-related settings were changed automatically after an abnormal exit**: the program detects a previous unclean shutdown and automatically disables batch rendering; you can re-enable it in the settings.
- **The viewer fails to start after `osu-wine --fixprefix`**: the prefix reinstall does not affect the viewer itself (it lives under `osuconfig`, so its bundled runtime and `GdiPlus.dll` are not removed); if it still fails to start, make sure you are using the legacy self-contained package.
- **How to report a problem**: attach the files under `logs/` — `log_YYYYMMDD.log` (the run log for that day) and `crash_*.log` (crash reports). The folder is Wine's `%LocalAppData%\OsuCatch-Editor-RealtimeViewer\logs\`, which maps to `~/.local/share/wineprefixes/osu-wineprefix/drive_c/users/<YOUR_USERNAME>/AppData/Local/OsuCatch-Editor-RealtimeViewer/logs/`. If the problem is about reading the editor (for example repeated `Periodic task error: ... FetchEditor error.` lines), please also enable **Settings → Log → Editor Reader** (and set the log level to Debug) before reproducing: without that switch the log only contains the short summary and not the reason (which read failed, which region stalled, which snapshot was rejected).

## Related links

- Viewer repository: <https://github.com/Exsper/osucatch-editor-realtimeviewer>
- osu-winello: <https://github.com/NelloKudo/osu-winello>
