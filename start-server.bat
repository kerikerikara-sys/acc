@echo off
chcp 65001 >nul
title Noxxer Licensing Server + Discord Bot
echo ============================================
echo    AC Noxxer - Licensing Server + Discord Bot
echo ============================================
echo.

REM --- Matar cualquier instancia antigua que este usando el puerto 3000 ---
echo [*] Comprobando puerto 3000...
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$p = Get-NetTCPConnection -LocalPort 3000 -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique;" ^
  "if ($p) { $p | ForEach-Object { try { Stop-Process -Id $_ -Force ; Write-Host ('[-] Cerrada instancia antigua (PID ' + $_ + ')') -ForegroundColor Yellow } catch {} } }" ^
  " else { Write-Host '[+] Puerto 3000 libre' -ForegroundColor Green }"

timeout /t 1 /nobreak >nul

cd /d "%~dp0server"
echo.
echo [+] Iniciando servidor desde: %CD%
echo [+] Para cerrar: cierra esta ventana o pulsa Ctrl+C
echo.
node index.js
echo.
echo.
echo [!] El servidor se ha cerrado.
pause
