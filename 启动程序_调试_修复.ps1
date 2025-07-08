# 启动程序并捕获详细错误信息
Write-Host "=== UEModManager 调试启动脚本 ===" -ForegroundColor Green
Write-Host "启动时间: $(Get-Date)" -ForegroundColor Yellow

# 设置当前目录
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptPath
Write-Host "脚本目录: $scriptPath" -ForegroundColor Yellow

# 检查可执行文件是否存在
$exePath = ".\UEModManager\bin\Debug\net8.0-windows\UEModManager.exe"
if (Test-Path $exePath) {
    Write-Host "找到可执行文件: $exePath" -ForegroundColor Green
    
    # 获取文件信息
    $fileInfo = Get-Item $exePath
    Write-Host "文件大小: $($fileInfo.Length) 字节" -ForegroundColor Yellow
    Write-Host "修改时间: $($fileInfo.LastWriteTime)" -ForegroundColor Yellow
} else {
    Write-Host "错误: 找不到可执行文件 $exePath" -ForegroundColor Red
    exit 1
}

# 尝试启动程序并捕获输出
Write-Host "正在启动程序..." -ForegroundColor Yellow

try {
    # 使用Start-Process启动程序并等待
    $process = Start-Process -FilePath $exePath -PassThru
    Write-Host "程序已启动，进程ID: $($process.Id)" -ForegroundColor Green
} catch {
    Write-Host "启动程序时发生错误: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "错误详情: $($_.Exception.ToString())" -ForegroundColor Red
}

# 检查Windows事件日志中的应用程序错误
Write-Host "检查Windows事件日志..." -ForegroundColor Yellow
try {
    $recentErrors = Get-WinEvent -FilterHashtable @{LogName='Application'; Level=2; StartTime=(Get-Date).AddMinutes(-5)} -MaxEvents 10 -ErrorAction SilentlyContinue | 
        Where-Object { $_.ProviderName -like "*UEModManager*" -or $_.Message -like "*UEModManager*" }
    
    if ($recentErrors) {
        Write-Host "发现相关错误日志:" -ForegroundColor Red
        foreach ($errorLog in $recentErrors) {
            Write-Host "时间: $($errorLog.TimeCreated)" -ForegroundColor Yellow
            Write-Host "消息: $($errorLog.Message)" -ForegroundColor Red
            Write-Host "---" -ForegroundColor Gray
        }
    } else {
        Write-Host "未在事件日志中找到相关错误" -ForegroundColor Green
    }
} catch {
    Write-Host "无法访问事件日志: $($_.Exception.Message)" -ForegroundColor Yellow
}

Write-Host "调试启动完成" -ForegroundColor Green 