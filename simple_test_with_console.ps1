# 简单测试脚本：启动UEModManager并显示控制台输出
param(
    [int]$TimeoutSeconds = 60
)

$exePath = "D:\cursor\2.0\UEModManager\bin\Debug\net8.0-windows\UEModManager.exe"

Write-Host "========== 启动UEModManager程序 ==========" -ForegroundColor Green
Write-Host "程序路径: $exePath" -ForegroundColor Yellow
Write-Host "超时时间: $TimeoutSeconds 秒" -ForegroundColor Yellow
Write-Host ""

if (-not (Test-Path $exePath)) {
    Write-Host "程序文件不存在，正在编译..." -ForegroundColor Red
    Set-Location "D:\cursor\2.0"
    dotnet build UEModManager.sln --configuration Debug
    if ($LASTEXITCODE -ne 0) {
        Write-Host "编译失败！" -ForegroundColor Red
        exit 1
    }
    Write-Host "编译成功！" -ForegroundColor Green
}

Write-Host "启动程序..." -ForegroundColor Green
Write-Host "注意：程序的Console.WriteLine输出会显示在下方" -ForegroundColor Cyan
Write-Host "请在程序中点击游戏选择下拉框来测试事件响应" -ForegroundColor Cyan
Write-Host "===============================================" -ForegroundColor Blue

# 启动程序（会阻塞直到程序退出或超时）
try {
    $process = Start-Process -FilePath $exePath -WorkingDirectory "D:\cursor\2.0" -Wait -PassThru -WindowStyle Normal
    
    if ($process.ExitCode -eq 0) {
        Write-Host "程序正常退出" -ForegroundColor Green
    } else {
        Write-Host "程序异常退出，退出码: $($process.ExitCode)" -ForegroundColor Red
    }
} catch {
    Write-Host "启动程序时出错: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "测试完成" -ForegroundColor Green 