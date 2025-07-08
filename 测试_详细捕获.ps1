try {
    # 设置输出文件路径
    $stdoutLogPath = ".\program_stdout.txt"
    $stderrLogPath = ".\program_stderr.txt"
    
    # 清空之前的日志
    if (Test-Path $stdoutLogPath) { Remove-Item $stdoutLogPath -Force }
    if (Test-Path $stderrLogPath) { Remove-Item $stderrLogPath -Force }
    
    Write-Host "正在尝试直接运行程序并捕获输出..." -ForegroundColor Yellow
    
    # 方法1：使用Start-Process
    try {
        $process = Start-Process -FilePath ".\UEModManager\bin\Release\net8.0-windows\UEModManager.exe" `
                                -RedirectStandardOutput $stdoutLogPath `
                                -RedirectStandardError $stderrLogPath `
                                -PassThru -NoNewWindow
        
        # 等待5秒
        Start-Sleep -Seconds 5
        
        # 如果程序仍在运行，则终止它
        if (!$process.HasExited) {
            Write-Host "程序仍在运行，正在终止..." -ForegroundColor Yellow
            $process.Kill()
        }
    }
    catch {
        Write-Host "方法1执行失败: $_" -ForegroundColor Red
    }
    
    # 显示输出日志内容（如果有）
    if (Test-Path $stdoutLogPath) {
        $stdoutContent = Get-Content $stdoutLogPath -Raw
        if ($stdoutContent) {
            Write-Host "程序标准输出:" -ForegroundColor Green
            Write-Host $stdoutContent
        } else {
            Write-Host "程序没有标准输出" -ForegroundColor Yellow
        }
    }
    
    if (Test-Path $stderrLogPath) {
        $stderrContent = Get-Content $stderrLogPath -Raw
        if ($stderrContent) {
            Write-Host "程序错误输出:" -ForegroundColor Red
            Write-Host $stderrContent
        } else {
            Write-Host "程序没有错误输出" -ForegroundColor Green
        }
    }
    
    # 方法2：使用cmd /c
    Write-Host "`n尝试使用cmd运行程序..." -ForegroundColor Yellow
    $cmdOutput = cmd /c ".\UEModManager\bin\Release\net8.0-windows\UEModManager.exe 2>&1"
    if ($cmdOutput) {
        Write-Host "CMD输出:" -ForegroundColor Cyan
        Write-Host $cmdOutput
    } else {
        Write-Host "CMD没有输出" -ForegroundColor Yellow
    }
    
    # 方法3：检查事件日志
    Write-Host "`n检查应用程序事件日志..." -ForegroundColor Yellow
    $appEvents = Get-EventLog -LogName Application -Newest 10 -ErrorAction SilentlyContinue
    if ($appEvents) {
        $relevantEvents = $appEvents | Where-Object { $_.Source -like "*UEModManager*" -or $_.Message -like "*UEModManager*" }
        if ($relevantEvents) {
            Write-Host "找到相关事件日志:" -ForegroundColor Cyan
            $relevantEvents | Format-Table TimeGenerated, EntryType, Source, Message -AutoSize -Wrap
        } else {
            Write-Host "没有找到相关事件日志" -ForegroundColor Yellow
        }
    } else {
        Write-Host "无法访问事件日志" -ForegroundColor Red
    }
    
} catch {
    Write-Host "执行测试脚本时出错: $_" -ForegroundColor Red
} 