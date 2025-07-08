# 测试验证 - 游戏路径确认弹窗修复
# 用于验证游戏路径确认弹窗的显示问题修复

Write-Host "==========================================="`n -ForegroundColor Green
Write-Host "       游戏路径确认弹窗修复验证脚本" -ForegroundColor Green  
Write-Host "==========================================="`n -ForegroundColor Green

Write-Host "测试目标:" -ForegroundColor Yellow
Write-Host "1. 验证游戏切换确认弹窗完整显示" -ForegroundColor White
Write-Host "2. 验证弹窗按钮功能正常" -ForegroundColor White
Write-Host ""

Write-Host "========== 修复内容 ==========" -ForegroundColor Cyan
Write-Host "✓ 修改了弹窗的WindowStyle为SingleBorderWindow" -ForegroundColor Green
Write-Host "✓ 移除了自定义标题栏，使用系统标准标题栏" -ForegroundColor Green  
Write-Host "✓ 更新了弹窗布局和背景设置" -ForegroundColor Green
Write-Host ""

Write-Host "========== 测试指南 ==========" -ForegroundColor Cyan
Write-Host "【弹窗显示测试】" -ForegroundColor Yellow
Write-Host "1. 启动程序" -ForegroundColor White
Write-Host "2. 点击下拉菜单选择另一个游戏" -ForegroundColor White  
Write-Host "3. 观察弹出的确认对话框是否完整显示" -ForegroundColor White
Write-Host "4. 确认弹窗的标题栏和按钮是否可见" -ForegroundColor White
Write-Host ""

Write-Host "【按钮功能测试】" -ForegroundColor Yellow
Write-Host "1. 在弹窗中点击各个按钮，验证是否有响应" -ForegroundColor White
Write-Host "2. 点击取消按钮，验证是否正确关闭弹窗" -ForegroundColor White  
Write-Host "3. 再次触发弹窗，点击确认按钮，验证是否正确切换游戏" -ForegroundColor White
Write-Host ""

# 启动程序
$exePath = ".\UEModManager\bin\Debug\net8.0-windows\UEModManager.exe"
if (Test-Path $exePath) {
    try {
        # 启动程序
        Start-Process -FilePath $exePath
        Write-Host "程序已启动，请进行测试..." -ForegroundColor Green
    } catch {
        Write-Host "启动程序失败: $($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host "错误: 找不到可执行文件 $exePath" -ForegroundColor Red
    Write-Host "请先编译项目，再运行此测试脚本" -ForegroundColor Red
}

Write-Host ""
Write-Host "按任意键关闭测试脚本..." -ForegroundColor Yellow
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown") 