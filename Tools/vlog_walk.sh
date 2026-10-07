#!/bin/bash
# Dev vlog clip: runs the build with -cc-script vlogwalk (the player walks the agency's trails, every frame saved)
# and turns the frames into DevVlog/videos/<date>_<name>.mp4.   usage: Tools/vlog_walk.sh <name>
set -e
ROOT=$(cd "$(dirname "$0")/.." && pwd)
NAME=${1:-caminhada}
FFMPEG=$(command -v ffmpeg || ls "$LOCALAPPDATA"/Microsoft/WinGet/Packages/Gyan.FFmpeg*/ffmpeg-*/bin/ffmpeg.exe | head -1)
WORK=$(mktemp -d)
taskkill //F //IM CampanhaRio.exe >/dev/null 2>&1 || true
cd "$ROOT/Builds/CampanhaRio"
timeout 400 ./CampanhaRio.exe -cc-script vlogwalk -cc-savedir "$(cygpath -w "$WORK")" -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 \
  -logFile "$(cygpath -w "$WORK/log.txt")" >/dev/null 2>&1 || true
grep "\[Test\] vlogwalk" "$WORK/log.txt"
mkdir -p "$ROOT/DevVlog/videos"
OUT="$ROOT/DevVlog/videos/$(date +%Y-%m-%d_%H%M)_$NAME.mp4"
"$FFMPEG" -y -loglevel error -framerate 30 -i "$WORK/frames/f_%04d.jpg" -c:v libx264 -pix_fmt yuv420p -crf 18 "$OUT"
rm -rf "$WORK"
echo "$OUT"
