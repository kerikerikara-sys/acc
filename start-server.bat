@echo off
chcp 65001 >nul
title Noxxer Licensing Server + Discord Bot
echo ============================================
echo    AC Noxxer - Licensing Server + Discord Bot
echo ============================================
echo.

REM --- Matar cualquier instancia antigua que este usando los puertos 3000 o 30120 ---
echo [*] Comprobando puertos 3000 y 30120...
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$p = @();" ^
  "foreach ($port in @(3000,30120)) {" ^
  "  $conns = Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique;" ^
  "  if ($conns) { $p += $conns ; Write-Host ('[-] Puerto ' + $port + ' ocupado') -ForegroundColor Yellow }" ^
  "  else { Write-Host ('[+] Puerto ' + $port + ' libre') -ForegroundColor Green }" ^
  "};" ^
  "$p = $p | Select-Object -Unique;" ^
  "if ($p) { $p | ForEach-Object { try { Stop-Process -Id $_ -Force ; Write-Host ('[-] Cerrado proceso (PID ' + $_ + ')') -ForegroundColor Yellow } catch {} } }" ^
  " else { Write-Host '[+] Sin procesos que cerrar' -ForegroundColor Green }"

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
