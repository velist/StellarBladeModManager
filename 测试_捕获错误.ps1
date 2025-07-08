try {
    # 设置错误输出文件路径
    $errorLogPath = ".\program_error_log.txt"
    
    # 清空之前的错误日志
    if (Test-Path $errorLogPath) {
        Remove-Item $errorLogPath -Force
    }
    
    # 运行程序并重定向标准错误输出
    $process = Start-Process -FilePath ".\UEModManager\bin\Release\net8.0-windows\UEModManager.exe" -RedirectStandardError $errorLogPath -PassThru -NoNewWindow
    
    # 等待5秒
    Start-Sleep -Seconds 5
    
    # 如果程序仍在运行，则终止它
    if (!$process.HasExited) {
        Write-Host "程序仍在运行，正在终止..." -ForegroundColor Yellow
        $process.Kill()
    }
    
    # 显示错误日志内容（如果有）
    if (Test-Path $errorLogPath) {
        $errorContent = Get-Content $errorLogPath -Raw
        if ($errorContent) {
            Write-Host "程序错误输出:" -ForegroundColor Red
            Write-Host $errorContent
        } else {
            Write-Host "程序没有错误输出" -ForegroundColor Green
        }
    } else {
        Write-Host "没有生成错误日志文件" -ForegroundColor Yellow
    }
} catch {
    Write-Host "执行测试脚本时出错: $_" -ForegroundColor Red
} 