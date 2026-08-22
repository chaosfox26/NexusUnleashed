sleep 50
powershell -ExecutionPolicy Bypass -File "$TEMP/claude/wslogin.ps1" >/dev/null 2>&1
sleep 13
powershell -ExecutionPolicy Bypass -File "$TEMP/claude/wsclick.ps1" 1276 1388 >/dev/null 2>&1
sleep 35
powershell -ExecutionPolicy Bypass -File "$TEMP/claude/ws-shot.ps1" "%TEMP%\claude\grove_pose.png" >/dev/null 2>&1
echo "flow done"
