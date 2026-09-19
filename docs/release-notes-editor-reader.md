# Which file should I download?

| Your setup | Download | EditorReader |
|---|---|---|
| **Windows**, 64-bit | `release-x64.zip` | New |
| **Windows**, 32-bit | `release-x86.zip` | New |
| **Wine** (Linux / macOS, osu-winello, etc.) | `release-x86-self-contained-legacy.zip` | **Legacy** |

Wine users: please use the `-legacy` package. It is self-contained (no .NET runtime install needed) and bundles the runtime libraries Wine requires (`StableCompatLib.dll`, `GdiPlus.dll`).

---

## What changed in this release

This release focuses on the **EditorReader** module — the part that reads the osu! editor's state directly out of the game's memory.

The previous implementation had a long-standing problem where the viewer could **freeze permanently** while you were working in the editor. Once it got into that state, retrying, re-binding, or even restarting the viewer would not recover it — the editor data simply never came back.

The cause turned out to be a combination of issues in how memory was being read:

- 32-bit process pointers were read as **signed** values. Once the editor's heap grew past the 2 GB boundary, every object address above `0x80000000` was sign-extended into an invalid address, so those objects could never be read.
- Object structs were always read as a fixed 336 bytes. An object sitting at the end of a memory page could be permanently unreadable, and one unreadable object was enough to fail the entire read.
- Transient inconsistencies while the editor was loading (for example when switching difficulties) were treated as hard failures, which could escalate into a full 30-second memory re-scan at exactly the moment you were waiting for the screen to update.
- A stale editor object could be kept in use after osu! rebuilt it, causing a permanent read-failure loop.

These have been addressed: pointer width is now handled correctly, reads are segmented so a partially-unreadable object no longer breaks the whole frame, snapshot-level failures are retried in place instead of escalating, and a rebuilt editor is detected properly.

**On Windows this resolves the freeze.** The viewer no longer gets stuck in the "retrying" state and recovers correctly when you switch difficulties, create or save beatmaps, or exit and re-enter the editor.

---

## About Wine — please read

The same optimization does **not work under Wine**.

While investigating, it became clear that Wine does not expose osu!'s memory the same way real Windows does. The new memory-scanning code cannot locate the editor object under Wine, so the viewer reports repeated `Editor needs Reload.` and never reads any data. We were not able to determine the exact reason, and we could **not find a way to make the new implementation work under Wine**.

To keep Wine usable, this release ships a separate package that contains the **previous EditorReader implementation**:

- `release-x86-self-contained-legacy.zip` — Wine only, legacy EditorReader.

This package restores the behaviour you had before: **it works under Wine, but it does not contain the freeze fix.**

We are sorry about this. Wine users still have the old freezing problem, and at the moment we have no fix for it. The Windows builds are fixed, but that fix does not carry over to Wine.

---

## Summary

- **Windows users** — download `release-x64.zip` (64-bit) or `release-x86.zip` (32-bit). Your freeze issue is fixed.
- **Wine users** — download `release-x86-self-contained-legacy.zip`. It works, but keeps the old freeze issue, which is currently unfixed. Our apologies.

If you are on Wine and can help investigate why the new implementation fails there, reports are very welcome — the diagnostics that were added during this work can be run against a live osu! process and would help a lot.
