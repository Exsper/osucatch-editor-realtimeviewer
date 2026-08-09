# Running OsuCatch Editor Realtime Viewer (32-bit) with osu! under osu-winello (Linux / Wine)

This guide is for users who installed osu! stable on Linux with [osu-winello](https://github.com/NelloKudo/osu-winello). It explains how to add this viewer (32-bit build) to the same Wine prefix that osu! uses, launch it together with osu!, and monitor the osu! editor in real time.

## Why you need the 32-bit build and the same prefix

- osu! stable itself is a 32-bit program. The viewer reads the memory of the osu!.exe process directly via `ReadProcessMemory` (the same approach used by Mapping Tools and gosumemory) and parses the data using a 32-bit pointer model.
- A Wine prefix is a self-contained "virtual Windows environment". Only processes inside the **same prefix** can talk to each other through the same wineserver, so the viewer must be launched from the same prefix as osu!.
- The viewer's native acceleration library, `StableCompatLib.dll`, is also provided per process bitness: the 32-bit release package (`release-x86.zip`) already contains the x86 version, so no extra work is needed.

> osu-winello's prefix is 64-bit (win64) but fully supports running 32-bit programs. Using the 32-bit viewer keeps the pointer model aligned with the 32-bit osu! process, which is also why the project publishes both x86 and x64 packages.

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
2. Download **`release-x86.zip`**.
3. Extract it to `~/.local/share/osuconfig/osucatch-viewer/`:

```bash
mkdir -p ~/.local/share/osuconfig/osucatch-viewer
unzip release-x86.zip -d ~/.local/share/osuconfig/osucatch-viewer
```

After extraction you should see these key files:

```text
osucatch-viewer/
├── OsuCatch-Editor-RealtimeViewer.exe   ← main program
├── OsuCatch-Editor-RealtimeViewer.dll
├── StableCompatLib.dll                  ← x86 native library
├── OpenTK.dll / OpenTK.GLControl.dll
└── img/ zh-Hans/
```

Why this location:

- `~/.local/share/osuconfig` lives under `XDG_DATA_HOME`, which osu-winello's Steam Runtime container mounts read-write, so Wine is guaranteed to be able to access the viewer;
- It is not inside the prefix's C: drive, so it survives `osu-wine --fixprefix` reinstalls.

## Step 2: Install the .NET 8 Desktop Runtime (x86)

> Skip-ahead option: if you do not want to install the .NET runtime into the prefix, download the official **`release-x86-self-contained.zip`** (a self-contained build with the .NET 8 runtime bundled) instead of `release-x86.zip` in Step 1, then jump straight to Step 3.

The official release is a framework-dependent build and does not bundle the .NET runtime, so the prefix must have the **.NET 8 Windows Desktop Runtime (x86)** installed (the viewer needs `Microsoft.NETCore.App` and `Microsoft.WindowsDesktop.App` 8.0).

### Method A (recommended): use osu-winello's bundled winetricks

```bash
osu-wine n --winetricks dotnetdesktop8
```

- This verb downloads and silently installs the .NET 8 Desktop Runtime 8.0.x x86 version; since the prefix is 64-bit, it also installs the x64 version alongside it (no impact on usage).
- If it says the `dotnetdesktop8` verb is unknown, your winetricks is too old — update it first and try again:

```bash
osu-wine n --winetricks --self-update
osu-wine n --winetricks dotnetdesktop8
```

- Verify the installation:

```bash
osu-wine n --wine 'C:\Program Files\dotnet\dotnet.exe' --list-runtimes
```

The output should show `Microsoft.NETCore.App 8.0.x` and `Microsoft.WindowsDesktop.App 8.0.x`.

### Method B (fallback): extract the runtime manually

If winetricks fails to install it, you can extract the runtime into the prefix manually:

1. Open <https://dotnet.microsoft.com/en-us/download/dotnet/8.0>;
2. Choose **Windows → x86 → download the ".zip"** (the Windows Desktop Runtime 8.0.x win-x86 zip package);
3. Extract it to the prefix's C: drive:

```bash
unzip windowsdesktop-runtime-8.0.x-win-x86.zip \
    -d ~/.local/share/wineprefixes/osu-wineprefix/drive_c/dotnet8
```

4. Make it discoverable by Windows programs running under Wine. Create `~/.local/share/osuconfig/configs/dotnet8.cfg`:

```bash
echo -e 'DOTNET_ROOT="C:\\dotnet8"\nDOTNET_ROOT_X86="C:\\dotnet8"' \
    > ~/.local/share/osuconfig/configs/dotnet8.cfg
```

> Note: a runtime manually extracted into `drive_c` will be deleted when `osu-wine --fixprefix` reinstalls the prefix, so you will have to redo this step; the same applies to Method A. The viewer itself, being in `osuconfig`, is unaffected.

## Step 3: Create a combined "osu! + viewer" launcher batch file

In the osu! folder (the "osu! folder" shown by `osu-wine --info`), create `launch_with_viewer.bat` with the following content:

```bat
@echo off
cd /d "%~dp0"
start "" osu!.exe %*
start "" "Z:\home\YOUR_USERNAME\.local\share\osuconfig\osucatch-viewer\OsuCatch-Editor-RealtimeViewer.exe"

:loop
tasklist | find "osu!.exe" >nul
if ERRORLEVEL 1 (
    taskkill /F /IM OsuCatch-Editor-RealtimeViewer.exe
    exit
)
ping -n 5 127.0.0.1 >nul
goto loop
```

Replace `YOUR_USERNAME` with your actual Linux username (check with `echo $USER`).

A few notes:

- Do **not** name the file `launch_with_memory.bat` — that name is used by osu-winello for gosumemory/tosu and can be overwritten or removed by its features;
- The batch file lives in the osu! folder so that `%~dp0` can locate `osu!.exe` directly, without relying on C:/D: drive mappings;
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

- **The viewer crashes on startup, or reports missing .NET / missing runtime**: Step 2 was not completed properly. Reinstall with Method A or switch to Method B; you can also check the crash report under `logs/`.
- **The log keeps showing `No Osu!.exe found`**: make sure the viewer is launched from the same prefix (use the batch file from Step 3) and that osu! is already running.
- **`No active editor found.` is shown**: first confirm you are actually in the editor (window title ending in `.osu`). After an osu! update the in-memory layout may change, so update the viewer to the latest release.
- **The viewer window appears but the picture does not refresh**: while the editor is not in the foreground or the mouse is idle, refresh runs on a low-frequency interval — this is by design; enter the editor and move the mouse to see real-time updates.
- **Performance-related settings were changed automatically after an abnormal exit**: the program detects a previous unclean shutdown and automatically disables batch rendering; you can re-enable it in the settings.
- **The viewer fails to start after `osu-wine --fixprefix`**: the prefix was reinstalled; reinstall the .NET runtime following Step 2 (the viewer itself is unaffected).

## Appendix (advanced): publish a self-contained x86 build, no runtime installation needed

> The official releases page also provides `release-x86-self-contained.zip` (self-contained build), so you usually do not need to build it yourself. The steps below are only for building from the latest source code.

If you would rather not install a runtime into the prefix, you can cross-publish a self-contained build on Linux with the .NET 8 SDK:

```bash
git clone https://github.com/Exsper/osucatch-editor-realtimeviewer.git
cd osucatch-editor-realtimeviewer

# StableCompatLib.dll (x86) is not committed to the repository; grab one from the official release-x86.zip
mkdir -p StableCompatLib/x86
unzip -j release-x86.zip StableCompatLib.dll -d StableCompatLib/x86

dotnet publish osucatch-editor-realtimeviewer \
    -c Release -r win-x86 --self-contained true -p:Platform=x86 \
    -o ~/osucatch-viewer-x86
```

Note: the output of `dotnet publish` will **not** include `StableCompatLib.dll` automatically, so copy one in manually:

```bash
unzip -j release-x86.zip StableCompatLib.dll -d ~/osucatch-viewer-x86
```

Finally, overwrite the viewer folder with the published output (the path in the batch file stays the same):

```bash
cp -r ~/osucatch-viewer-x86/. ~/.local/share/osuconfig/osucatch-viewer/
```

The self-contained build needs none of the steps in Step 2; just launch it as described in Step 3.

## Related links

- Viewer repository: <https://github.com/Exsper/osucatch-editor-realtimeviewer>
- Viewer releases page (`release-x86.zip`): <https://github.com/Exsper/osucatch-editor-realtimeviewer/releases/latest>
- osu-winello: <https://github.com/NelloKudo/osu-winello>
- .NET 8 download page: <https://dotnet.microsoft.com/en-us/download/dotnet/8.0>
