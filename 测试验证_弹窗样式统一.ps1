# 测试验证弹窗样式统一
# 创建于: 2025-07-07

$ErrorActionPreference = "Stop"
Write-Host "开始测试弹窗样式统一..." -ForegroundColor Cyan

# 设置工作目录
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptPath
Write-Host "当前工作目录: $scriptPath" -ForegroundColor Gray

# 编译项目
Write-Host "正在编译项目..." -ForegroundColor Yellow
dotnet build UEModManager.sln
if ($LASTEXITCODE -ne 0) {
    Write-Host "编译失败，请检查代码" -ForegroundColor Red
    exit 1
}

# 运行程序
Write-Host "正在启动程序..." -ForegroundColor Yellow
Start-Process -FilePath ".\UEModManager\bin\Debug\net8.0-windows\UEModManager.exe" -Wait

Write-Host "测试完成！" -ForegroundColor Green
Write-Host "请检查以下内容:" -ForegroundColor Yellow
Write-Host "1. 切换游戏确认的弹窗样式是否为深色主题" -ForegroundColor White
Write-Host "2. 禁用启用的弹窗样式是否为深色主题" -ForegroundColor White
Write-Host "3. 其他操作的弹窗样式是否为深色主题" -ForegroundColor White
Write-Host "4. 所有弹窗的样式是否统一" -ForegroundColor White 