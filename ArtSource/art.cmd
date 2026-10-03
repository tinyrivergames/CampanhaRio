@echo off
rem CampanhaRio art pipeline (Blender 5.2, scripted). Run from anywhere:
rem   art build  Test\pebble_gen.py --asset Pebble [--seed 3]   new version (never overwrites) + preview sheet + NOTES entry
rem   art preview Test\Pebble\Pebble_v001.blend                 re-render the preview sheet of a version
rem   art export  Test\Pebble\Pebble_v001.blend                 FBX + material sidecar into Assets/_Project/Art/Models/Test
rem   art review  Test Pebble [v001]                            open the preview PNG + the .blend in Blender (latest if no version)
setlocal
set "BLENDER=C:\Program Files\Blender Foundation\Blender 5.2\blender.exe"
set "ROOT=%~dp0"
set "CMD=%~1"
if /i "%CMD%"=="build"   goto build
if /i "%CMD%"=="preview" goto preview
if /i "%CMD%"=="export"  goto export
if /i "%CMD%"=="review"  goto review
echo usage: art build^|preview^|export^|review ...  (see the top of art.cmd)
exit /b 1

:build
set "GEN=%~2"
shift & shift
set "REST="
:collect
if "%~1"=="" goto run_build
set "REST=%REST% %1"
shift
goto collect
:run_build
"%BLENDER%" --background --factory-startup --python "%ROOT%%GEN%" -- %REST%
exit /b %ERRORLEVEL%

:preview
"%BLENDER%" --background "%ROOT%%~2" --python "%ROOT%pipeline\preview.py"
exit /b %ERRORLEVEL%

:export
"%BLENDER%" --background "%ROOT%%~2" --python "%ROOT%pipeline\export.py"
exit /b %ERRORLEVEL%

:review
call "%ROOT%pipeline\open_for_review.cmd" %2 %3 %4
exit /b %ERRORLEVEL%
