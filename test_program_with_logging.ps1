# PowerShell脚本：测试UEModManager程序并记录详细日志
# 作者：AI助手
# 日期：$(Get-Date)

param(
    [string]$LogPath = "program_test_log.txt"
)

# 配置日志函数
function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss.fff"
    $logEntry = "[$timestamp] [$Level] $Message"
    Write-Host $logEntry -ForegroundColor $(
        switch ($Level) {
            "INFO" { "White" }
            "WARN" { "Yellow" }
            "ERROR" { "Red" }
            "SUCCESS" { "Green" }
            "DEBUG" { "Cyan" }
            default { "White" }
        }
    )
    Add-Content -Path $LogPath -Value $logEntry
}

# 程序路径
$exePath = "D:\cursor\2.0\UEModManager\bin\Debug\net8.0-windows\UEModManager.exe"
$workingDir = "D:\cursor\2.0"

Write-Log "========== UEModManager 程序测试开始 ==========" "INFO"
Write-Log "测试程序路径: $exePath" "INFO"
Write-Log "工作目录: $workingDir" "INFO"

# 检查程序文件是否存在
if (-not (Test-Path $exePath)) {
    Write-Log "错误：程序文件不存在：$exePath" "ERROR"
    Write-Log "尝试重新编译程序..." "INFO"
    
    try {
        Set-Location $workingDir
        Write-Log "执行编译命令：dotnet build UEModManager.sln --configuration Debug" "INFO"
        $buildResult = dotnet build UEModManager.sln --configuration Debug 2>&1
        Write-Log "编译结果：$buildResult" "DEBUG"
        
        if ($LASTEXITCODE -eq 0) {
            Write-Log "编译成功" "SUCCESS"
        } else {
            Write-Log "编译失败，退出码：$LASTEXITCODE" "ERROR"
            exit 1
        }
    } catch {
        Write-Log "编译过程发生异常：$($_.Exception.Message)" "ERROR"
        exit 1
    }
}

# 检查编译后程序是否存在
if (-not (Test-Path $exePath)) {
    Write-Log "编译后程序文件仍不存在，请检查编译过程" "ERROR"
    exit 1
}

Write-Log "程序文件确认存在，准备启动..." "SUCCESS"

# 启动程序并监控
try {
    Write-Log "启动程序：$exePath" "INFO"
    
    # 使用Start-Process启动程序，不等待
    $process = Start-Process -FilePath $exePath -WorkingDirectory $workingDir -PassThru
    
    if ($process) {
        Write-Log "程序已启动，进程ID: $($process.Id)" "SUCCESS"
        Write-Log "程序名称: $($process.ProcessName)" "INFO"
        Write-Log "程序窗口标题: $($process.MainWindowTitle)" "INFO"
        
        # 等待程序完全启动
        Write-Log "等待程序窗口完全加载..." "INFO"
        Start-Sleep -Seconds 3
        
        # 检查程序是否仍在运行
        $runningProcess = Get-Process -Id $process.Id -ErrorAction SilentlyContinue
        if ($runningProcess) {
            Write-Log "程序运行正常，窗口标题: $($runningProcess.MainWindowTitle)" "SUCCESS"
            Write-Log "内存使用: $([math]::Round($runningProcess.WorkingSet64 / 1MB, 2)) MB" "INFO"
            
            Write-Log "======= 测试指引 =======" "INFO"
            Write-Log "请在程序界面中执行以下测试步骤：" "INFO"
            Write-Log "1. 点击游戏选择下拉框" "INFO"
            Write-Log "2. 尝试选择不同的游戏选项" "INFO"
            Write-Log "3. 观察界面是否有响应" "INFO"
            Write-Log "4. 查看控制台是否有错误输出" "INFO"
            Write-Log "========================" "INFO"
            
            # 持续监控程序状态
            $monitorCount = 0
            while ($runningProcess -and -not $runningProcess.HasExited -and $monitorCount -lt 300) { # 监控5分钟
                Start-Sleep -Seconds 1
                $monitorCount++
                
                # 每30秒记录一次状态
                if ($monitorCount % 30 -eq 0) {
                    $currentProcess = Get-Process -Id $process.Id -ErrorAction SilentlyContinue
                    if ($currentProcess) {
                        Write-Log "程序运行状态检查 - 内存: $([math]::Round($currentProcess.WorkingSet64 / 1MB, 2)) MB, CPU: $($currentProcess.TotalProcessorTime)" "DEBUG"
                    }
                }
                
                $runningProcess = Get-Process -Id $process.Id -ErrorAction SilentlyContinue
            }
            
            if ($runningProcess -and -not $runningProcess.HasExited) {
                Write-Log "监控时间结束，程序仍在运行" "INFO"
            } else {
                Write-Log "程序已退出" "WARN"
            }
            
        } else {
            Write-Log "程序启动后立即退出，可能存在启动问题" "ERROR"
        }
        
    } else {
        Write-Log "无法启动程序，请检查程序文件和权限" "ERROR"
    }
    
} catch {
    Write-Log "启动程序时发生异常：$($_.Exception.Message)" "ERROR"
    Write-Log "异常详情：$($_.Exception.StackTrace)" "DEBUG"
}

Write-Log "========== 程序测试结束 ==========" "INFO"
Write-Log "日志文件保存在：$(Resolve-Path $LogPath)" "INFO"

# 检查是否有Console输出文件
$consoleLogPath = Join-Path $workingDir "console_output.log"
if (Test-Path $consoleLogPath) {
    Write-Log "发现控制台输出日志：$consoleLogPath" "INFO"
} else {
    Write-Log "未发现额外的控制台输出日志" "INFO"
} 