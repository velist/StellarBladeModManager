Write-Host "开始测试弹窗颜色方案..." -ForegroundColor Cyan
$currentDir = Get-Location
Write-Host "当前工作目录: $currentDir" -ForegroundColor Yellow

# 编译项目
Write-Host "正在编译项目..." -ForegroundColor Green
dotnet build

# 运行程序
Write-Host "正在启动程序..." -ForegroundColor Green
Start-Process -FilePath ".\UEModManager\bin\Debug\net8.0-windows\UEModManager.exe"

# 提示用户检查颜色方案
Write-Host "测试完成！" -ForegroundColor Green
Write-Host "请检查以下内容:" -ForegroundColor Yellow
Write-Host "1. 添加新游戏弹窗的颜色是否为黄绿色系" -ForegroundColor White
Write-Host "2. 编辑MOD信息弹窗的颜色是否为黄绿色系" -ForegroundColor White
Write-Host "3. 所有弹窗的颜色方案是否统一" -ForegroundColor White
Write-Host "4. 按钮悬停效果是否正常" -ForegroundColor White 