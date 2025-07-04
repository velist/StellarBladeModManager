$ErrorActionPreference = "Stop"

# 定义颜色函数
function Write-ColorOutput($ForegroundColor) {
    $fc = $host.UI.RawUI.ForegroundColor
    $host.UI.RawUI.ForegroundColor = $ForegroundColor
    if ($args) {
        Write-Output $args
    }
    $host.UI.RawUI.ForegroundColor = $fc
}

function Write-Success($message) {
    Write-ColorOutput Green "✅ $message"
}

function Write-Info($message) {
    Write-ColorOutput Cyan "ℹ️ $message"
}

function Write-Warning($message) {
    Write-ColorOutput Yellow "⚠️ $message"
}

function Write-Error($message) {
    Write-ColorOutput Red "❌ $message"
}

# 检查捐赠图片是否存在
$baseDir = $PSScriptRoot
$donationImagePath = Join-Path -Path $baseDir -ChildPath "捐赠.png"
$projectDonationPath = Join-Path -Path $baseDir -ChildPath "UEModManager\捐赠.png"

Write-Info "正在检查捐赠图片..."
if (Test-Path $donationImagePath) {
    Write-Success "根目录捐赠图片存在: $donationImagePath"
} else {
    Write-Warning "根目录捐赠图片不存在: $donationImagePath"
}

if (Test-Path $projectDonationPath) {
    Write-Success "项目目录捐赠图片存在: $projectDonationPath"
} else {
    Write-Warning "项目目录捐赠图片不存在: $projectDonationPath"
}

# 编译项目
Write-Info "正在编译项目..."
try {
    dotnet build "$baseDir\UEModManager.sln" -c Debug
    Write-Success "项目编译成功"
} catch {
    Write-Error "项目编译失败: $_"
    exit 1
}

# 运行应用程序
Write-Info "正在启动应用程序..."
$exePath = Join-Path -Path $baseDir -ChildPath "UEModManager\bin\Debug\net8.0-windows\UEModManager.exe"

if (Test-Path $exePath) {
    Write-Success "找到可执行文件: $exePath"
    Write-Info "启动应用程序，请检查捐赠按钮的ToolTip是否正确显示二维码..."
    Write-Info "测试步骤:"
    Write-Info "1. 将鼠标悬停在底部中间的'支持捐赠'按钮上"
    Write-Info "2. 检查是否显示捐赠二维码"
    Write-Info "3. 关闭应用程序以继续测试"
    
    # 启动应用程序
    try {
        Start-Process -FilePath $exePath -Wait
        Write-Info "应用程序已关闭"
        Write-Info "捐赠二维码显示正常吗? (Y/N)"
        $response = Read-Host
        if ($response -eq "Y" -or $response -eq "y") {
            Write-Success "测试成功! 捐赠二维码显示正常"
        } else {
            Write-Warning "测试失败! 捐赠二维码显示异常"
        }
    } catch {
        Write-Error "启动应用程序失败: $_"
    }
} else {
    Write-Error "找不到可执行文件: $exePath"
}

Write-Info "测试完成，按任意键退出..."
$null = $host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown") 