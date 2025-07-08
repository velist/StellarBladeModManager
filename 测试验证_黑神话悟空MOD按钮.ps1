# 测试验证黑神话悟空MOD按钮功能
Write-Host "开始测试黑神话悟空MOD按钮功能..." -ForegroundColor Cyan

# 获取当前目录
$currentDir = Get-Location
Write-Host "当前目录: $currentDir" -ForegroundColor Gray

# 检查必要的图片文件是否存在
$xunleiImage = Join-Path $currentDir "黑神话悟空MOD-迅雷云盘.png"
$baiduImage = Join-Path $currentDir "黑神话悟空MOD-百度网盘.png"

if (Test-Path $xunleiImage) {
    Write-Host "✅ 黑神话悟空MOD-迅雷云盘.png 文件存在" -ForegroundColor Green
} else {
    Write-Host "❌ 黑神话悟空MOD-迅雷云盘.png 文件不存在" -ForegroundColor Red
    Write-Host "    尝试复制迅雷云盘.png作为替代..." -ForegroundColor Yellow
    try {
        Copy-Item "迅雷云盘.png" "黑神话悟空MOD-迅雷云盘.png" -ErrorAction Stop
        Write-Host "    ✅ 成功创建黑神话悟空MOD-迅雷云盘.png" -ForegroundColor Green
    } catch {
        Write-Host "    ❌ 创建黑神话悟空MOD-迅雷云盘.png失败: $_" -ForegroundColor Red
    }
}

if (Test-Path $baiduImage) {
    Write-Host "✅ 黑神话悟空MOD-百度网盘.png 文件存在" -ForegroundColor Green
} else {
    Write-Host "❌ 黑神话悟空MOD-百度网盘.png 文件不存在" -ForegroundColor Red
    Write-Host "    尝试复制百度网盘.png作为替代..." -ForegroundColor Yellow
    try {
        Copy-Item "百度网盘.png" "黑神话悟空MOD-百度网盘.png" -ErrorAction Stop
        Write-Host "    ✅ 成功创建黑神话悟空MOD-百度网盘.png" -ForegroundColor Green
    } catch {
        Write-Host "    ❌ 创建黑神话悟空MOD-百度网盘.png失败: $_" -ForegroundColor Red
    }
}

# 检查日志文件
$logFile = Join-Path $currentDir "黑神话悟空MOD按钮测试.log"
if (Test-Path $logFile) {
    Remove-Item $logFile -Force
}

# 创建日志文件
"[$(Get-Date)] 开始测试黑神话悟空MOD按钮功能" | Out-File -FilePath $logFile -Append

# 启动程序进行测试
Write-Host "`n正在启动程序进行测试..." -ForegroundColor Yellow
Write-Host "请执行以下操作：" -ForegroundColor Yellow
Write-Host "1. 在游戏选择下拉框中选择'黑神话悟空'" -ForegroundColor Yellow
Write-Host "2. 确认A区是否显示'黑猴MOD'按钮" -ForegroundColor Yellow
Write-Host "3. 点击'黑猴MOD'按钮，检查是否显示正确的二维码" -ForegroundColor Yellow
Write-Host "4. 测试完成后，请关闭程序窗口" -ForegroundColor Yellow

# 启动应用程序
try {
    $exePath = Join-Path $currentDir "UEModManager\bin\Debug\net8.0-windows\UEModManager.exe"
    if (Test-Path $exePath) {
        "[$(Get-Date)] 启动程序: $exePath" | Out-File -FilePath $logFile -Append
        $process = Start-Process $exePath -PassThru
        Write-Host "`n✅ 程序已启动，请按上述步骤进行测试" -ForegroundColor Green
        
        # 等待程序退出
        Write-Host "`n等待程序退出..." -ForegroundColor Gray
        $process.WaitForExit()
        
        # 检查控制台输出日志
        $consoleLogPath = Join-Path $currentDir "UEModManager\bin\Debug\net8.0-windows\console.log"
        if (Test-Path $consoleLogPath) {
            Write-Host "`n正在分析控制台日志..." -ForegroundColor Yellow
            $consoleLog = Get-Content $consoleLogPath -Tail 100
            $gameNameDetection = $consoleLog | Where-Object { $_ -match "游戏名称识别" }
            if ($gameNameDetection) {
                Write-Host "找到游戏名称识别日志:" -ForegroundColor Green
                $gameNameDetection | ForEach-Object { Write-Host "  $_" -ForegroundColor Green }
                $gameNameDetection | Out-File -FilePath $logFile -Append
            } else {
                Write-Host "未找到游戏名称识别日志" -ForegroundColor Red
                "未找到游戏名称识别日志" | Out-File -FilePath $logFile -Append
            }
            
            $buttonDisplay = $consoleLog | Where-Object { $_ -match "显示黑神话悟空专属功能按钮" }
            if ($buttonDisplay) {
                Write-Host "找到黑猴MOD按钮显示日志:" -ForegroundColor Green
                $buttonDisplay | ForEach-Object { Write-Host "  $_" -ForegroundColor Green }
                $buttonDisplay | Out-File -FilePath $logFile -Append
            } else {
                Write-Host "未找到黑猴MOD按钮显示日志" -ForegroundColor Red
                "未找到黑猴MOD按钮显示日志" | Out-File -FilePath $logFile -Append
            }
        } else {
            Write-Host "`n❌ 未找到控制台日志文件: $consoleLogPath" -ForegroundColor Red
            "未找到控制台日志文件: $consoleLogPath" | Out-File -FilePath $logFile -Append
        }
    } else {
        Write-Host "`n❌ 程序文件不存在: $exePath" -ForegroundColor Red
        Write-Host "请先编译程序后再测试" -ForegroundColor Red
        "程序文件不存在: $exePath" | Out-File -FilePath $logFile -Append
    }
} catch {
    Write-Host "`n❌ 启动程序失败: $_" -ForegroundColor Red
    "启动程序失败: $_" | Out-File -FilePath $logFile -Append
}

Write-Host "`n测试完成，测试日志已保存到: $logFile" -ForegroundColor Cyan 