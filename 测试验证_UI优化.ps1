# 测试验证_UI优化.ps1
# 此脚本用于验证UI优化的改动

Write-Host "开始测试验证 - UI优化" -ForegroundColor Green

# 1. 启动应用程序
Write-Host "正在启动应用程序..." -ForegroundColor Yellow
$process = Start-Process -FilePath "dotnet" -ArgumentList "run --project UEModManager" -PassThru

# 等待应用程序启动
Start-Sleep -Seconds 5
Write-Host "应用程序已启动" -ForegroundColor Green

# 2. 测试步骤说明
Write-Host "`n测试步骤1 - MOD合集按钮弹窗优化:" -ForegroundColor Cyan
Write-Host "1. 在应用程序中，点击顶部的'剑星MOD合集'按钮" -ForegroundColor White
Write-Host "2. 观察弹出的窗口中的两个云盘图标" -ForegroundColor White
Write-Host "3. 确认鼠标悬停在图标上时不会有任何变化" -ForegroundColor White
Write-Host "4. 确认鼠标指针不会变成手型" -ForegroundColor White
Write-Host "5. 确认点击图标不会有任何反应" -ForegroundColor White

# 3. 预期结果1
Write-Host "`n预期结果1:" -ForegroundColor Cyan
Write-Host "- 两个云盘图标不再有鼠标悬停效果" -ForegroundColor White
Write-Host "- 鼠标指针不会变成手型" -ForegroundColor White
Write-Host "- 点击图标不会弹出任何提示" -ForegroundColor White

# 4. 测试步骤2
Write-Host "`n测试步骤2 - 捐赠悬停优化:" -ForegroundColor Cyan
Write-Host "1. 在应用程序底部，找到右下角的'支持捐赠'文字" -ForegroundColor White
Write-Host "2. 将鼠标悬停在'支持捐赠'文字上" -ForegroundColor White
Write-Host "3. 观察弹出的二维码图片" -ForegroundColor White
Write-Host "4. 确认二维码下方有'如果对你有帮助，可以请我喝一杯蜜雪冰城'文字" -ForegroundColor White

# 5. 预期结果2
Write-Host "`n预期结果2:" -ForegroundColor Cyan
Write-Host "- 二维码下方显示'如果对你有帮助，可以请我喝一杯蜜雪冰城'文字" -ForegroundColor White
Write-Host "- 文字布局整齐，可以清晰阅读" -ForegroundColor White

# 等待用户确认
Write-Host "`n按任意键结束测试并关闭应用程序..." -ForegroundColor Magenta
$null = $host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")

# 6. 关闭应用程序
if (-not $process.HasExited) {
    Write-Host "正在关闭应用程序..." -ForegroundColor Yellow
    Stop-Process -Id $process.Id -Force
    Write-Host "应用程序已关闭" -ForegroundColor Green
}

Write-Host "`n测试验证完成" -ForegroundColor Green 