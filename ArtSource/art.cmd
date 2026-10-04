@echo off
rem CampanhaRio art pipeline (Blender 5.2, scripted). Run from anywhere:
rem   art build  Test\pebble_gen.py --asset Pebble [--seed 3]   new version (never overwrites) + preview sheet + NOTES entry
rem   art preview Test\Pebble\Pebble_v001.blend                 re-render the preview sheet of a version
rem   art export  Test\Pebble\Pebble_v001.blend                 FBX + material sidecar into Assets/_Project/Art/Models/Test
rem   art review  Test Pebble [v001]                            open the preview PNG + the .blend in Blender (latest if no version)
rem   art compare Test Pebble <Unity Pebble_views.png>      Blender views over the Unity LookDev views (final check)
rem   art look    Test/Pebble sunset                            open the latest version in Blender lit for afternoon|golden|sunset|dusk
setlocal
set "BLENDER=C:\Program Files\Blender Foundation\Blender 5.2\blender.exe"
set "ROOT=%~dp0"
set "CMD=%~1"
if /i "%CMD%"=="build"   goto build
if /i "%CMD%"=="preview" goto preview
if /i "%CMD%"=="export"  goto export
if /i "%CMD%"=="review"  goto review
if /i "%CMD%"=="compare" goto compare
if /i "%CMD%"=="look"    goto look
echo usage: art build^|preview^|export^|review^|compare^|look ...  (see the top of art.cmd)
exit /b 1

:look
"%BLENDER%" --background --factory-startup --python "%ROOT%pipeline\review_scene.py" -- --asset %2 --time %3
if errorlevel 1 exit /b 1
start "" "%BLENDER%" "%ROOT%_tmp\review_%3.blend" --python "%ROOT%pipeline\viewport_rendered.py"
exit /b 0

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

:compare
"%BLENDER%" --background --factory-startup --python "%ROOT%pipeline\compare.py" -- %2 %3 %4 %5
exit /b %ERRORLEVEL%

:review
call "%ROOT%pipeline\open_for_review.cmd" %2 %3 %4
exit /b %ERRORLEVEL%
