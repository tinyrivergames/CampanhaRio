@echo off
rem Opens an asset version for review: the preview sheet in the default image viewer and the .blend in the Blender GUI
rem (both non-blocking). Usage: open_for_review.cmd <Family> <Asset> [v001]   (no version = the latest)
setlocal
set "BLENDER=C:\Program Files\Blender Foundation\Blender 5.2\blender.exe"
set "DIR=%~dp0..\%~1\%~2"
set "STEM="
if not "%~3"=="" set "STEM=%~2_%~3"
if "%STEM%"=="" for /f "delims=" %%f in ('dir /b /o:n "%DIR%\%~2_v???.blend" 2^>nul') do set "STEM=%%~nf"
if "%STEM%"=="" (echo No version of %~1/%~2 found in %DIR% & exit /b 1)
if not exist "%DIR%\%STEM%_preview.png" (echo Missing %DIR%\%STEM%_preview.png & exit /b 1)
echo Opening %STEM%: preview sheet + .blend
start "" "%DIR%\%STEM%_preview.png"
start "" "%BLENDER%" "%DIR%\%STEM%.blend"
exit /b 0
