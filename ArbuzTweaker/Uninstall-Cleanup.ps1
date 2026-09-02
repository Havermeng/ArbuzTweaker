# Запускается установщиком при ПОЛНОМ удалении ArbuzTweaker (при обновлении — нет).
# Убирает то, что MSI сам не трогает: задачи планировщика, их скрипты в Roaming и служебный
# мусор (Updates, Logs). Настройки (Configs) и бэкапы (Backups) намеренно остаются —
# бэкапы нужны, чтобы откатить твики реестра даже после удаления программы.
$ErrorActionPreference = 'SilentlyContinue'

$taskFolder = 'ArbuzTweaker'
$taskNames = @(
    'Restart NVIDIA Overlay',
    'Restart NVIDIA Overlay for selected apps',
    'Set Dota 2 and SCP SL to realtime priority'
)

foreach ($taskName in $taskNames) {
    & schtasks.exe /Delete /TN "\$taskFolder\$taskName" /F 2>$null | Out-Null
}

# Опустевшую папку задач тоже убираем.
try {
    $service = New-Object -ComObject 'Schedule.Service'
    $service.Connect()
    $service.GetFolder('\').DeleteFolder($taskFolder, 0)
} catch { }

# Аудит создания процессов (событие 4688) — ГЛОБАЛЬНАЯ политика Windows. Твикер включал его
# для задач «по запуску игры» и помечал это stamp-файлом. Выключаем только если включали мы:
# иначе после удаления Security-лог продолжал бы пухнуть от 4688 навсегда.
$auditStamp = Join-Path $env:LOCALAPPDATA 'ArbuzTweaker\ProcessAuditEnabledByArbuz.stamp'
if (Test-Path -LiteralPath $auditStamp) {
    $auditPol = Join-Path $env:SystemRoot 'System32\auditpol.exe'
    & $auditPol /set '/subcategory:{0CCE922B-69AE-11D9-BED3-505054503030}' /success:disable | Out-Null
    Remove-Item -LiteralPath $auditStamp -Force
}
# Служебная метка-дебаунс задачи перезапуска оверлея — тоже мусор.
Remove-Item -LiteralPath (Join-Path $env:LOCALAPPDATA 'ArbuzTweaker\NvidiaOverlayProcessRestart.stamp') -Force

Remove-Item -LiteralPath (Join-Path $env:APPDATA 'ArbuzTweaker') -Recurse -Force
Remove-Item -LiteralPath (Join-Path $env:LOCALAPPDATA 'ArbuzTweaker\Updates') -Recurse -Force
Remove-Item -LiteralPath (Join-Path $env:LOCALAPPDATA 'ArbuzTweaker\Logs') -Recurse -Force
