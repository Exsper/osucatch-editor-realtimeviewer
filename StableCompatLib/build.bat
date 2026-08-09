@echo off
setlocal EnableDelayedExpansion
cd /d "%~dp0"

if not exist "x64" mkdir "x64"
if not exist "x86" mkdir "x86"

rem ------------------------------------------------------------------
rem  Find the 64-bit MinGW-w64 compiler (x86_64-w64-mingw32-g++)
rem ------------------------------------------------------------------
set "X64_GXX="
for /f "delims=" %%W in ('where x86_64-w64-mingw32-g++ 2^>nul') do if not defined X64_GXX set "X64_GXX=%%W"
if not defined X64_GXX (
  for %%R in (C:\msys64 C:\msys2 D:\msys64 D:\msys2 E:\msys64) do (
    if not defined X64_GXX if exist "%%R\mingw64\bin\x86_64-w64-mingw32-g++.exe" set "X64_GXX=%%R\mingw64\bin\x86_64-w64-mingw32-g++.exe"
  )
)
if not defined X64_GXX (
  for %%P in (C:\mingw64\bin\x86_64-w64-mingw32-g++.exe D:\mingw64\bin\x86_64-w64-mingw32-g++.exe C:\mingw32\bin\x86_64-w64-mingw32-g++.exe D:\mingw32\bin\x86_64-w64-mingw32-g++.exe) do (
    if not defined X64_GXX if exist "%%P" set "X64_GXX=%%P"
  )
)

rem ------------------------------------------------------------------
rem  Find the 32-bit MinGW-w64 compiler (i686-w64-mingw32-g++)
rem ------------------------------------------------------------------
set "X86_GXX="
for /f "delims=" %%W in ('where i686-w64-mingw32-g++ 2^>nul') do if not defined X86_GXX set "X86_GXX=%%W"
if not defined X86_GXX (
  for %%R in (C:\msys64 C:\msys2 D:\msys64 D:\msys2 E:\msys64) do (
    if not defined X86_GXX if exist "%%R\mingw32\bin\i686-w64-mingw32-g++.exe" set "X86_GXX=%%R\mingw32\bin\i686-w64-mingw32-g++.exe"
  )
)
if not defined X86_GXX (
  for %%P in (C:\mingw32\bin\i686-w64-mingw32-g++.exe D:\mingw32\bin\i686-w64-mingw32-g++.exe C:\mingw64\bin\i686-w64-mingw32-g++.exe D:\mingw64\bin\i686-w64-mingw32-g++.exe) do (
    if not defined X86_GXX if exist "%%P" set "X86_GXX=%%P"
  )
)

echo.
if defined X64_GXX (echo [64-bit] compiler: !X64_GXX!) else (echo [64-bit] compiler not found)
if defined X86_GXX (echo [32-bit] compiler: !X86_GXX!) else (echo [32-bit] compiler not found)
echo.

rem  Put the toolchain bin dirs on PATH so cc1plus/ld/as and their DLLs are found
if defined X64_GXX for %%D in ("!X64_GXX!") do set "X64_BIN=%%~dpD"
if defined X64_BIN set "PATH=!X64_BIN!;%PATH%"
if defined X86_GXX for %%D in ("!X86_GXX!") do set "X86_BIN=%%~dpD"
if defined X86_BIN set "PATH=!X86_BIN!;%PATH%"

set "LINK_FLAGS=-shared -static -static-libgcc -static-libstdc++ -Wl,--kill-at -lm"
set "ANYFAILED="

rem ------------------------------------------------------------------
rem  Build x64
rem ------------------------------------------------------------------
if not defined X64_GXX goto :build_x86
echo Building x64\StableCompatLib.dll ...
"!X64_GXX!" -c StableCompatLib.cpp -o x64\StableCompatLib-x64.o
if errorlevel 1 set "ANYFAILED=1"
if not defined ANYFAILED "!X64_GXX!" %LINK_FLAGS% -o x64\StableCompatLib.dll x64\StableCompatLib-x64.o
if errorlevel 1 set "ANYFAILED=1"
if defined ANYFAILED (echo [ERROR] x64 build failed.) else (echo [OK] x64\StableCompatLib.dll)

rem ------------------------------------------------------------------
rem  Build x86
rem ------------------------------------------------------------------
:build_x86
set "X86FAILED="
if not defined X86_GXX goto :summary
echo Building x86\StableCompatLib.dll ...
"!X86_GXX!" -c StableCompatLib.cpp -o x86\StableCompatLib-x86.o
if errorlevel 1 set "X86FAILED=1"
if not defined X86FAILED "!X86_GXX!" %LINK_FLAGS% -o x86\StableCompatLib.dll x86\StableCompatLib-x86.o
if errorlevel 1 set "X86FAILED=1"
if defined X86FAILED (
  echo [ERROR] x86 build failed.
  set "ANYFAILED=1"
) else (
  echo [OK] x86\StableCompatLib.dll
)

rem ------------------------------------------------------------------
rem  Summary
rem ------------------------------------------------------------------
:summary
echo.
set "MISSING="
if not defined X64_GXX (
  echo [ERROR] 64-bit compiler not found.
  set "MISSING=1"
)
if not defined X86_GXX (
  echo [ERROR] 32-bit compiler not found. To build the x86 DLL, install mingw-w64-i686-gcc:
  echo         D:\msys64\usr\bin\bash.exe -lc "pacman -S --needed mingw-w64-i686-gcc"
  echo         Adjust D:\msys64 to your MSYS2 installation path, or use C:\msys64.
  set "MISSING=1"
)
if defined MISSING exit /b 1
if defined ANYFAILED exit /b 1
exit /b 0
