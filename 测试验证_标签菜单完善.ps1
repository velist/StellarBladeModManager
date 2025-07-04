# C1区MOD卡片标签菜单完善测试验证脚本
Write-Host "=== UEModManager C1区标签菜单完善测试 ===" -ForegroundColor Green
Write-Host "测试时间: $(Get-Date)" -ForegroundColor Yellow

Write-Host "`n🔍 修复内容说明:" -ForegroundColor Cyan
Write-Host "✅ 添加了全局Popup跟踪变量_currentTypeSelectionPopup" -ForegroundColor White
Write-Host "✅ 优化ShowTypeSelectionMenu方法，确保同时只有一个弹窗打开" -ForegroundColor White
Write-Host "✅ 添加CloseCurrentTypeSelectionPopup方法统一管理弹窗关闭" -ForegroundColor White
Write-Host "✅ 添加全局PreviewMouseDown事件监听，支持点击空白处关闭" -ForegroundColor White
Write-Host "✅ 选择标签后自动关闭弹窗，无需手动点击原标签" -ForegroundColor White

Write-Host "`n🔍 测试项目清单:" -ForegroundColor Cyan
Write-Host "1. 程序正常启动和基本功能" -ForegroundColor White
Write-Host "2. C1区MOD卡片左下角标签点击展开效果" -ForegroundColor White
Write-Host "3. 点击其他标签后菜单是否自动关闭" -ForegroundColor White
Write-Host "4. 选择标签类型后菜单是否自动关闭" -ForegroundColor White
Write-Host "5. 点击空白处菜单是否自动关闭" -ForegroundColor White
Write-Host "6. 连续点击不同标签的切换效果" -ForegroundColor White
Write-Host "7. 标签类型更改是否正确生效" -ForegroundColor White

Write-Host "`n📋 测试步骤:" -ForegroundColor Cyan
Write-Host "步骤1: 启动程序并确保有MOD数据" -ForegroundColor Yellow
Write-Host "步骤2: 在C1区找到MOD卡片，点击左下角的标签" -ForegroundColor Yellow
Write-Host "步骤3: 确认菜单正常展开，显示所有标签选项" -ForegroundColor Yellow
Write-Host "步骤4: 选择一个不同的标签类型" -ForegroundColor Yellow
Write-Host "步骤5: 验证选择后菜单是否自动关闭" -ForegroundColor Yellow
Write-Host "步骤6: 再次点击同一个或其他标签，测试连续操作" -ForegroundColor Yellow
Write-Host "步骤7: 点击标签后，尝试点击空白处关闭菜单" -ForegroundColor Yellow
Write-Host "步骤8: 验证标签类型的更改是否正确反映在界面上" -ForegroundColor Yellow

Write-Host "`n⚠️  修复前后对比:" -ForegroundColor Cyan
Write-Host "❌ 修复前: 选择其他标签后菜单不关闭，必须点击原标签才能关闭" -ForegroundColor Red
Write-Host "❌ 修复前: 点击空白处菜单不关闭" -ForegroundColor Red
Write-Host "❌ 修复前: 连续点击不同标签可能导致多个菜单同时打开" -ForegroundColor Red
Write-Host ""
Write-Host "✅ 修复后: 选择任何标签后菜单自动关闭" -ForegroundColor Green
Write-Host "✅ 修复后: 点击空白处菜单自动关闭" -ForegroundColor Green
Write-Host "✅ 修复后: 同时只能有一个标签菜单打开" -ForegroundColor Green
Write-Host "✅ 修复后: 操作逻辑更符合用户预期" -ForegroundColor Green

Write-Host "`n🚀 启动程序进行测试..." -ForegroundColor Green

# 启动程序
try {
    $exePath = ".\UEModManager\bin\Debug\net8.0-windows\UEModManager.exe"
    if (Test-Path $exePath) {
        Start-Process -FilePath $exePath
        Write-Host "✅ 程序已启动，请进行测试" -ForegroundColor Green
    } else {
        Write-Host "❌ 程序文件不存在，请先编译程序" -ForegroundColor Red
        Write-Host "执行以下命令编译:" -ForegroundColor Yellow
        Write-Host "dotnet build UEModManager.sln --configuration Debug" -ForegroundColor Gray
        exit 1
    }
} catch {
    Write-Host "❌ 启动程序失败: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host "`n请在程序中进行以下具体测试：" -ForegroundColor Yellow
Write-Host "1. 确认程序正常启动，界面显示正常" -ForegroundColor White
Write-Host "2. 如果没有MOD，请先导入一些MOD文件" -ForegroundColor White
Write-Host "3. 找到C1区的MOD卡片，点击左下角的类型标签（如'👥 面部'、'👤 人物'等）" -ForegroundColor White
Write-Host "4. 验证菜单展开效果，应该显示6个标签选项" -ForegroundColor White
Write-Host "5. 选择一个不同的标签类型，观察菜单是否立即关闭" -ForegroundColor White
Write-Host "6. 再次点击标签，然后点击程序的空白处，验证菜单是否关闭" -ForegroundColor White
Write-Host "7. 连续点击不同MOD的标签，验证是否只有一个菜单打开" -ForegroundColor White
Write-Host "8. 确认标签类型更改后，MOD卡片显示的标签确实已更新" -ForegroundColor White

Write-Host "`n📊 预期测试结果:" -ForegroundColor Cyan
Write-Host "✅ 点击标签后菜单正常展开" -ForegroundColor Green
Write-Host "✅ 选择标签后菜单自动关闭" -ForegroundColor Green
Write-Host "✅ 点击空白处菜单自动关闭" -ForegroundColor Green
Write-Host "✅ 同时只能有一个标签菜单打开" -ForegroundColor Green
Write-Host "✅ 标签类型更改正确生效" -ForegroundColor Green
Write-Host "✅ 操作流畅，符合用户预期" -ForegroundColor Green

Write-Host "`n测试完成后，请在聊天中反馈测试结果" -ForegroundColor Cyan
Write-Host "如果测试通过，我将提交这次修复" -ForegroundColor Cyan

# 显示当前Git状态
Write-Host "`n📊 当前Git状态:" -ForegroundColor Cyan
git status --short

Write-Host "`n等待用户测试反馈..." -ForegroundColor Yellow 