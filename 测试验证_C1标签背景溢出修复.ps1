# C1区MOD卡片标签背景溢出修复测试验证脚本
Write-Host "=== UEModManager C1区标签背景溢出修复测试 ===" -ForegroundColor Green
Write-Host "测试时间: $(Get-Date)" -ForegroundColor Yellow

Write-Host "`n🔍 修复内容说明:" -ForegroundColor Cyan
Write-Host "✅ 优化ContextMenu样式，设置精确的位置偏移防止溢出" -ForegroundColor White
Write-Host "✅ 创建自定义ContextMenu模板，确保背景色一致性" -ForegroundColor White
Write-Host "✅ 创建自定义MenuItem模板，防止白色背景显示" -ForegroundColor White
Write-Host "✅ 为标签Border添加ClipToBounds属性防止内容溢出" -ForegroundColor White
Write-Host "✅ 添加鼠标悬停效果，增强交互体验" -ForegroundColor White
Write-Host "✅ 设置固定高度和精确内边距，确保视觉一致性" -ForegroundColor White

Write-Host "`n🔍 测试项目清单:" -ForegroundColor Cyan
Write-Host "1. 程序正常启动和基本功能" -ForegroundColor White
Write-Host "2. C1区MOD卡片标签显示是否正常" -ForegroundColor White
Write-Host "3. 点击标签展开菜单是否有白色背景溢出" -ForegroundColor White
Write-Host "4. 菜单边框和背景颜色是否统一" -ForegroundColor White
Write-Host "5. 菜单项鼠标悬停效果是否正常" -ForegroundColor White
Write-Host "6. 菜单位置是否精确对齐" -ForegroundColor White
Write-Host "7. 标签鼠标悬停效果是否正常" -ForegroundColor White
Write-Host "8. 选择标签后菜单关闭是否正常" -ForegroundColor White

Write-Host "`n📋 测试步骤:" -ForegroundColor Cyan
Write-Host "步骤1: 启动程序并确保有MOD数据" -ForegroundColor Yellow
Write-Host "步骤2: 在C1区找到MOD卡片，观察左下角标签的显示" -ForegroundColor Yellow
Write-Host "步骤3: 鼠标悬停在标签上，观察悬停效果" -ForegroundColor Yellow
Write-Host "步骤4: 点击标签，观察菜单展开效果" -ForegroundColor Yellow
Write-Host "步骤5: 检查菜单是否有白色背景溢出" -ForegroundColor Yellow
Write-Host "步骤6: 鼠标悬停在菜单项上，观察悬停效果" -ForegroundColor Yellow
Write-Host "步骤7: 选择一个标签类型，验证功能正常" -ForegroundColor Yellow
Write-Host "步骤8: 测试多个不同的MOD卡片标签" -ForegroundColor Yellow

Write-Host "`n⚠️  重点关注问题:" -ForegroundColor Cyan
Write-Host "❌ 修复前: 标签菜单展开时左侧有白色背景溢出" -ForegroundColor Red
Write-Host "✅ 修复后: 菜单背景应该是统一的深色，无白色溢出" -ForegroundColor Green
Write-Host "✅ 菜单边框应该是深灰色，圆角边缘整齐" -ForegroundColor Green
Write-Host "✅ 菜单项悬停时应该有深灰色背景变化" -ForegroundColor Green

Write-Host "`n🚀 启动程序进行测试..." -ForegroundColor Green

# 启动程序
try {
    Start-Process -FilePath ".\UEModManager\bin\Release\net8.0-windows\UEModManager.exe" -ErrorAction Stop
    Write-Host "✅ 程序启动成功" -ForegroundColor Green
}
catch {
    Write-Host "❌ 程序启动失败，尝试其他路径..." -ForegroundColor Red
    try {
        Start-Process -FilePath ".\UEModManager\bin\Debug\net8.0-windows\UEModManager.exe" -ErrorAction Stop
        Write-Host "✅ 程序启动成功(Debug版本)" -ForegroundColor Green
    }
    catch {
        Write-Host "❌ 无法找到可执行文件，请先编译程序" -ForegroundColor Red
        Write-Host "请运行: dotnet build UEModManager.sln -c Release" -ForegroundColor Yellow
    }
}

Write-Host "`n请在程序中进行以下测试：" -ForegroundColor Yellow
Write-Host "1. 确认程序正常启动并显示界面" -ForegroundColor White
Write-Host "2. 如果没有MOD，请先导入一些MOD文件" -ForegroundColor White
Write-Host "3. 在C1区找到MOD卡片，观察左下角的标签样式" -ForegroundColor White
Write-Host "4. 鼠标悬停在标签上，应该看到颜色变化效果" -ForegroundColor White
Write-Host "5. 点击标签展开菜单，重点检查是否有白色背景溢出" -ForegroundColor White
Write-Host "6. 菜单应该显示为深色背景，边框整齐" -ForegroundColor White
Write-Host "7. 鼠标悬停在菜单项上，应该有颜色变化" -ForegroundColor White
Write-Host "8. 选择一个标签类型，验证功能正常且菜单正确关闭" -ForegroundColor White

Write-Host "`n📊 预期测试结果:" -ForegroundColor Cyan
Write-Host "✅ 标签显示正常，无背景异常" -ForegroundColor Green
Write-Host "✅ 标签悬停效果正常" -ForegroundColor Green
Write-Host "✅ 菜单展开无白色背景溢出" -ForegroundColor Green
Write-Host "✅ 菜单背景和边框颜色统一" -ForegroundColor Green
Write-Host "✅ 菜单项悬停效果正常" -ForegroundColor Green
Write-Host "✅ 菜单位置精确对齐" -ForegroundColor Green
Write-Host "✅ 标签选择功能正常" -ForegroundColor Green

Write-Host "`n测试完成后，请在聊天中反馈测试结果" -ForegroundColor Cyan
Write-Host "如果测试通过，我将提交这次修复" -ForegroundColor Cyan

# 显示当前Git状态
Write-Host "`n📊 当前Git状态:" -ForegroundColor Cyan
git status --short

Write-Host "`n等待用户测试反馈..." -ForegroundColor Yellow 