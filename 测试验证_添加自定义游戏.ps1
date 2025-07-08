# 添加自定义游戏功能测试脚本
# 作用: 编译并启动程序，用于测试自定义游戏添加功能
Write-Host "=== UEModManager 自定义游戏添加功能测试 ===" -ForegroundColor Green
Write-Host "启动时间: $(Get-Date -Format "MM/dd/yyyy HH:mm:ss")" -ForegroundColor Yellow

# 显示当前目录
$currentDir = Get-Location
Write-Host "当前目录: $currentDir" -ForegroundColor Cyan

# 检查是否包含UEModManager项目文件
$projectFile = Join-Path -Path $currentDir -ChildPath "UEModManager\UEModManager.csproj"
if (-not (Test-Path $projectFile)) {
    Write-Host "错误: 找不到项目文件 $projectFile" -ForegroundColor Red
    exit 1
}

# 编译项目
Write-Host "开始编译项目..." -ForegroundColor Yellow
dotnet build UEModManager.sln --configuration Debug

# 检查编译结果
if ($LASTEXITCODE -eq 0) {
    Write-Host "编译成功！" -ForegroundColor Green
} else {
    Write-Host "编译失败，退出代码: $LASTEXITCODE" -ForegroundColor Red
    exit $LASTEXITCODE
}

# 查找可执行文件
$exePath = Join-Path -Path $currentDir -ChildPath "UEModManager\bin\Debug\net8.0-windows\UEModManager.exe"
if (-not (Test-Path $exePath)) {
    Write-Host "错误: 找不到可执行文件 $exePath" -ForegroundColor Red
    exit 1
}

# 启动程序
Write-Host "正在启动程序..." -ForegroundColor Green
try {
    # 记录启动日志
    $logFile = Join-Path -Path $currentDir -ChildPath "custom_game_test_log.txt"
    Start-Process -FilePath $exePath -ArgumentList "--debug" -RedirectStandardOutput $logFile
    Write-Host "程序已启动，请测试自定义游戏添加功能" -ForegroundColor Cyan
    Write-Host "日志文件: $logFile" -ForegroundColor Cyan
} catch {
    Write-Host "启动失败: $_" -ForegroundColor Red
    exit 1
}

Write-Host "测试步骤:" -ForegroundColor Yellow
Write-Host "1. 从下拉列表选择'添加新游戏...'" -ForegroundColor White
Write-Host "2. 输入自定义游戏名称并确认" -ForegroundColor White
Write-Host "3. 配置游戏路径" -ForegroundColor White
Write-Host "4. 验证自定义游戏是否添加到下拉列表" -ForegroundColor White
Write-Host "5. 关闭程序后重新打开，确认自定义游戏是否保留" -ForegroundColor White 