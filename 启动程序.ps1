#!/usr/bin/env pwsh

# UEModManager 启动脚本
Write-Host "=== UEModManager 启动脚本 ===" -ForegroundColor Green
Write-Host "正在启动 UEModManager..." -ForegroundColor Yellow

# 切换到UEModManager目录
Set-Location "UEModManager"

# 检查是否存在编译后的可执行文件
$exePath = ".\bin\Debug\net8.0-windows\UEModManager.exe"
if (Test-Path $exePath) {
    Write-Host "找到可执行文件，直接启动..." -ForegroundColor Green
    Start-Process $exePath
} else {
    Write-Host "可执行文件不存在，开始编译..." -ForegroundColor Yellow
    
    # 编译项目
    Write-Host "正在编译项目..." -ForegroundColor Cyan
    dotnet build
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "编译成功，启动程序..." -ForegroundColor Green
        Start-Process $exePath
    } else {
        Write-Host "编译失败，请检查错误信息" -ForegroundColor Red
        Read-Host "按回车键退出"
        exit 1
    }
}

Write-Host "程序已启动！" -ForegroundColor Green
Write-Host "如果程序没有显示，请检查任务栏或稍等片刻。" -ForegroundColor Yellow

# 等待用户确认
Read-Host "按回车键退出启动脚本" 