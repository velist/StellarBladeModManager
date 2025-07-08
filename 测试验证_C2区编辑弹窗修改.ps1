# C2区编辑弹窗样式统一测试验证脚本
Write-Host "=== UEModManager C2区编辑弹窗修改测试 ===" -ForegroundColor Cyan

# 切换到项目目录
cd $PSScriptRoot

# 定义颜色
$successColor = "Green"
$errorColor = "Red"
$infoColor = "Yellow"
$promptColor = "Magenta"

# 项目路径
$solutionPath = ".\UEModManager.sln"
$buildConfig = "Debug"

Write-Host ""
Write-Host "1. 编译项目..." -ForegroundColor $infoColor
try {
    # 使用MSBuild编译项目
    msbuild $solutionPath /p:Configuration=$buildConfig /verbosity:minimal
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ 编译成功" -ForegroundColor $successColor
    } else {
        Write-Host "✗ 编译失败，错误代码: $LASTEXITCODE" -ForegroundColor $errorColor
        exit 1
    }
} catch {
    Write-Host "✗ 编译过程中发生错误: $_" -ForegroundColor $errorColor
    exit 1
}

Write-Host ""
Write-Host "2. 运行程序进行测试..." -ForegroundColor $infoColor
Write-Host "请手动执行以下测试步骤：" -ForegroundColor $promptColor
Write-Host "  1. 启动后选择任意游戏" -ForegroundColor $promptColor
Write-Host "  2. 选择一个MOD查看C2区详情" -ForegroundColor $promptColor 
Write-Host "  3. 点击C2区详情区域中的编辑按钮" -ForegroundColor $promptColor
Write-Host "  4. 验证编辑弹窗样式是否与游戏切换窗口一致" -ForegroundColor $promptColor
Write-Host "  5. 验证描述输入框是否足够大，文字是否显示完整" -ForegroundColor $promptColor
Write-Host "  6. 验证窗口是否可以调整大小" -ForegroundColor $promptColor
Write-Host "  7. 测试编辑并保存功能是否正常" -ForegroundColor $promptColor
Write-Host ""

# 启动程序
try {
    $exePath = ".\UEModManager\bin\$buildConfig\net6.0-windows\UEModManager.exe"
    if (Test-Path $exePath) {
        Write-Host "正在启动程序: $exePath" -ForegroundColor $infoColor
        Start-Process $exePath
        Write-Host "✓ 程序已启动，请进行手动测试" -ForegroundColor $successColor
    } else {
        Write-Host "✗ 找不到程序可执行文件: $exePath" -ForegroundColor $errorColor
        exit 1
    }
} catch {
    Write-Host "✗ 启动程序时发生错误: $_" -ForegroundColor $errorColor
    exit 1
}

Write-Host ""
Write-Host "测试要点：" -ForegroundColor $infoColor
Write-Host "1. C2区编辑弹窗的样式是否与游戏切换窗口一致" -ForegroundColor $infoColor
Write-Host "2. 弹窗是否包含标题和副标题" -ForegroundColor $infoColor
Write-Host "3. 名称和描述输入区域是否使用GroupBox分组" -ForegroundColor $infoColor
Write-Host "4. 描述输入框是否足够大，文字显示是否完整" -ForegroundColor $infoColor
Write-Host "5. 确认和取消按钮是否风格统一" -ForegroundColor $infoColor
Write-Host "6. 窗口是否支持调整大小" -ForegroundColor $infoColor
Write-Host "7. 编辑保存功能是否正常" -ForegroundColor $infoColor

Write-Host ""
Write-Host "请测试完成后手动关闭程序，然后回到此窗口确认测试结果" -ForegroundColor $promptColor 