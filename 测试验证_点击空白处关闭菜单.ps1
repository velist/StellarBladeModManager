# 点击空白处关闭标签菜单功能测试验证脚本
Write-Host "=== UEModManager 点击空白处关闭标签菜单测试 ===" -ForegroundColor Green
Write-Host "测试时间: $(Get-Date -Format 'MM/dd/yyyy HH:mm:ss')"
Write-Host ""

Write-Host "🔍 本次修复内容:" -ForegroundColor Yellow
Write-Host "✅ 在MainContentArea_PreviewMouseDown中添加标签菜单检测" -ForegroundColor Green
Write-Host "✅ 在CategoryArea_PreviewMouseDown中添加快速关闭逻辑" -ForegroundColor Green
Write-Host "✅ 优化CloseCurrentTypeSelectionPopup方法，支持关闭所有标签菜单" -ForegroundColor Green
Write-Host "✅ 添加全局Popup扫描，防止多个菜单同时展开" -ForegroundColor Green
Write-Host "✅ 添加标签菜单识别机制，确保只关闭相关弹窗" -ForegroundColor Green
Write-Host "✅ 完善的调试输出和异常处理" -ForegroundColor Green
Write-Host ""

Write-Host "🔍 测试重点:" -ForegroundColor Yellow
Write-Host "1. 点击标签正常展开菜单" -ForegroundColor White
Write-Host "2. 点击菜单内选项正常选择并关闭" -ForegroundColor White
Write-Host "3. 点击菜单外空白区域自动关闭菜单" -ForegroundColor White
Write-Host "4. 点击其他MOD卡片时菜单自动关闭" -ForegroundColor White
Write-Host "5. 点击左侧分类区域时菜单自动关闭" -ForegroundColor White
Write-Host "6. 点击右侧详情区域时菜单自动关闭" -ForegroundColor White
Write-Host "7. 点击顶部工具栏时菜单自动关闭" -ForegroundColor White
Write-Host "8. 🆕 快速连续点击多个标签，确保不会同时展开多个菜单" -ForegroundColor Yellow
Write-Host ""

Write-Host "📋 详细测试步骤:" -ForegroundColor Cyan
Write-Host "步骤1: 启动程序，确保有MOD数据可测试" -ForegroundColor White
Write-Host "步骤2: 在C1区找到任意MOD卡片，点击左下角标签" -ForegroundColor White
Write-Host "步骤3: 确认菜单正常展开，显示6个标签选项" -ForegroundColor White
Write-Host "步骤4: 点击菜单中的某个选项，确认菜单关闭且标签更新" -ForegroundColor White
Write-Host "步骤5: 再次点击标签展开菜单" -ForegroundColor White
Write-Host "步骤6: 点击C1区的空白处（卡片之间的间隙）" -ForegroundColor White
Write-Host "步骤7: 点击左侧B1区分类列表" -ForegroundColor White
Write-Host "步骤8: 点击右侧详情面板" -ForegroundColor White
Write-Host "步骤9: 点击顶部状态栏或按钮区域" -ForegroundColor White
Write-Host "步骤10: 点击其他MOD卡片的非标签区域" -ForegroundColor White
Write-Host "步骤11: 🆕 快速连续点击3-4个不同MOD的标签，确认只有最后一个菜单显示" -ForegroundColor Yellow
Write-Host ""

Write-Host "⚠️  问题排查指引:" -ForegroundColor Red
Write-Host "如果点击空白处仍然不能关闭菜单，请注意:" -ForegroundColor Yellow
Write-Host "• 查看控制台输出，确认是否有调试信息" -ForegroundColor White
Write-Host "• 点击弹窗内部应显示'确认点击在标签菜单内部，保持菜单打开'" -ForegroundColor White
Write-Host "• 点击弹窗外部应显示'确认点击在标签菜单外部，关闭标签菜单'" -ForegroundColor White
Write-Host "• 点击分类区域应显示'点击分类区域，关闭标签菜单'" -ForegroundColor White
Write-Host "• 如果有错误信息，会显示'处理标签菜单点击事件时出错'" -ForegroundColor White
Write-Host "• 🆕 连续打开多个菜单应显示'关闭当前跟踪的弹窗'和'关闭遗留的标签菜单弹窗'" -ForegroundColor Yellow
Write-Host ""

Write-Host "🚀 启动程序进行测试..." -ForegroundColor Green
try {
    # 启动程序
    Start-Process -FilePath "UEModManager\bin\Debug\net8.0-windows\UEModManager.exe" -WorkingDirectory $PWD
    Write-Host "✅ 程序已启动，请进行测试" -ForegroundColor Green
}
catch {
    Write-Host "❌ 启动失败: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "📝 测试要点提醒:" -ForegroundColor Cyan
Write-Host "1. 主要测试点击空白处是否能关闭菜单" -ForegroundColor White
Write-Host "2. 注意观察控制台窗口的调试输出信息" -ForegroundColor White
Write-Host "3. 测试不同区域的点击效果（左侧、中间、右侧、顶部）" -ForegroundColor White
Write-Host "4. 确认菜单关闭后不影响其他功能" -ForegroundColor White
Write-Host "5. 🆕 重点测试快速连续点击多个标签的情况" -ForegroundColor Yellow
Write-Host "6. 🆕 确认同时只能有一个标签菜单打开" -ForegroundColor Yellow
Write-Host ""

Write-Host "💡 预期结果:" -ForegroundColor Green
Write-Host "✅ 点击标签正常展开菜单" -ForegroundColor Green
Write-Host "✅ 点击菜单选项正常关闭并更新" -ForegroundColor Green
Write-Host "✅ 点击任何空白区域都能关闭菜单" -ForegroundColor Green
Write-Host "✅ 控制台有相应的调试输出" -ForegroundColor Green
Write-Host "✅ 无异常或错误信息" -ForegroundColor Green
Write-Host "✅ 🆕 同时只能有一个标签菜单展开" -ForegroundColor Yellow
Write-Host "✅ 🆕 快速连续点击会自动关闭之前的菜单" -ForegroundColor Yellow
Write-Host ""

Write-Host "等待测试结果反馈..." -ForegroundColor Cyan
Write-Host "测试完成后，请告诉我:" -ForegroundColor White
Write-Host "• 点击空白处是否能关闭菜单" -ForegroundColor White
Write-Host "• 控制台是否有调试信息输出" -ForegroundColor White
Write-Host "• 是否有任何错误或异常" -ForegroundColor White
Write-Host "• 🆕 是否还会出现多个菜单同时展开的问题" -ForegroundColor Yellow
Write-Host "• 🆕 快速连续点击标签的表现如何" -ForegroundColor Yellow 