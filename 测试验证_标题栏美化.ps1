# 测试验证_标题栏美化.ps1
# 此脚本用于验证标题栏美化的改动

Write-Host "开始测试验证 - 标题栏美化" -ForegroundColor Green

# 1. 启动应用程序
Write-Host "正在启动应用程序..." -ForegroundColor Yellow
$process = Start-Process -FilePath "dotnet" -ArgumentList "run --project UEModManager" -PassThru

# 等待应用程序启动
Start-Sleep -Seconds 5
Write-Host "应用程序已启动" -ForegroundColor Green

# 2. 测试步骤说明
Write-Host "`n测试步骤1 - 标题栏外观:" -ForegroundColor Cyan
Write-Host "1. 观察程序顶部标题栏的外观" -ForegroundColor White
Write-Host "2. 确认标题栏已改为暗色(#1A2332)，与整体UI协调" -ForegroundColor White
Write-Host "3. 确认标题栏已实现扁平化设计" -ForegroundColor White
Write-Host "4. 确认标题栏左侧显示程序图标和标题" -ForegroundColor White
Write-Host "5. 确认标题栏右侧有最小化、最大化和关闭按钮" -ForegroundColor White

# 3. 预期结果1
Write-Host "`n预期结果1:" -ForegroundColor Cyan
Write-Host "- 标题栏颜色为暗灰色(#1A2332)，与整体UI协调" -ForegroundColor White
Write-Host "- 标题栏采用扁平化设计，没有传统Windows标题栏的立体感" -ForegroundColor White
Write-Host "- 标题栏左侧显示程序图标和标题文字" -ForegroundColor White
Write-Host "- 标题栏右侧有三个控制按钮(最小化、最大化、关闭)" -ForegroundColor White

# 4. 测试步骤2
Write-Host "`n测试步骤2 - 按钮功能:" -ForegroundColor Cyan
Write-Host "1. 点击标题栏右侧的最小化按钮" -ForegroundColor White
Write-Host "2. 确认窗口最小化到任务栏" -ForegroundColor White
Write-Host "3. 从任务栏恢复窗口" -ForegroundColor White
Write-Host "4. 点击最大化按钮" -ForegroundColor White
Write-Host "5. 确认窗口最大化" -ForegroundColor White
Write-Host "6. 再次点击最大化按钮(现在变为还原按钮)" -ForegroundColor White
Write-Host "7. 确认窗口还原到原始大小" -ForegroundColor White

# 5. 预期结果2
Write-Host "`n预期结果2:" -ForegroundColor Cyan
Write-Host "- 最小化按钮可以将窗口最小化到任务栏" -ForegroundColor White
Write-Host "- 最大化按钮可以将窗口最大化" -ForegroundColor White
Write-Host "- 当窗口最大化时，最大化按钮变为还原按钮" -ForegroundColor White
Write-Host "- 还原按钮可以将窗口还原到原始大小" -ForegroundColor White

# 6. 测试步骤3
Write-Host "`n测试步骤3 - 按钮悬停效果:" -ForegroundColor Cyan
Write-Host "1. 将鼠标悬停在最小化按钮上" -ForegroundColor White
Write-Host "2. 确认按钮背景变为深灰色(#2A3441)" -ForegroundColor White
Write-Host "3. 将鼠标悬停在关闭按钮上" -ForegroundColor White
Write-Host "4. 确认关闭按钮背景变为红色(#E11D48)" -ForegroundColor White

# 7. 预期结果3
Write-Host "`n预期结果3:" -ForegroundColor Cyan
Write-Host "- 最小化和最大化按钮悬停时背景变为深灰色" -ForegroundColor White
Write-Host "- 关闭按钮悬停时背景变为红色" -ForegroundColor White

# 8. 测试验证说明
Write-Host "`n测试验证说明:" -ForegroundColor Yellow
Write-Host "请手动验证以上测试步骤，确认标题栏美化效果符合预期。" -ForegroundColor White
Write-Host "测试完成后，可以关闭应用程序。" -ForegroundColor White

# 等待用户输入
Write-Host "`n按任意键继续..." -ForegroundColor Green
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")

# 结束测试
Write-Host "`n测试结束" -ForegroundColor Green 