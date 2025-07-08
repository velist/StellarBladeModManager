# 测试验证 - 标题栏最大化修复验证脚本
# 用于验证标题栏在窗口最大化时不会消失

Write-Host "==========================================="`n -ForegroundColor Green
Write-Host "       标题栏最大化修复验证脚本" -ForegroundColor Green  
Write-Host "==========================================="`n -ForegroundColor Green

Write-Host "测试目标:" -ForegroundColor Yellow
Write-Host "1. 验证窗口最大化时标题栏不会消失" -ForegroundColor White
Write-Host "2. 验证标题栏按钮功能正常" -ForegroundColor White
Write-Host "3. 验证多显示器环境下正确显示" -ForegroundColor White
Write-Host ""

Write-Host "========== 修复内容 ==========" -ForegroundColor Cyan
Write-Host "✓ 添加了窗口消息处理 (WM_GETMINMAXINFO)" -ForegroundColor Green
Write-Host "✓ 正确计算最大化窗口位置和大小" -ForegroundColor Green  
Write-Host "✓ 添加了DPI缩放相关属性" -ForegroundColor Green
Write-Host "✓ 修复了标题栏在最大化时被裁剪的问题" -ForegroundColor Green
Write-Host ""

Write-Host "========== 测试指南 ==========" -ForegroundColor Cyan
Write-Host "【标题栏最大化测试】" -ForegroundColor Yellow
Write-Host "1. 启动程序，观察窗口和标题栏显示是否正常" -ForegroundColor White
Write-Host "2. 点击最大化按钮，观察窗口最大化后标题栏是否仍然可见" -ForegroundColor White  
Write-Host "3. 最大化后，测试标题栏上的按钮(最小化、恢复、关闭)是否正常工作" -ForegroundColor White
Write-Host "4. 从最大化状态恢复到正常大小，再次观察标题栏是否正常" -ForegroundColor White
Write-Host ""

Write-Host "【DPI缩放测试】" -ForegroundColor Yellow
Write-Host "1. 如果有高DPI显示器，验证标题栏和按钮渲染是否清晰" -ForegroundColor White
Write-Host "2. 如果有多个显示器，将窗口拖到不同显示器测试最大化行为" -ForegroundColor White  
Write-Host ""

Write-Host "【问题检查】" -ForegroundColor Yellow
Write-Host "1. 最大化窗口时，标题栏是否完全可见" -ForegroundColor Red
Write-Host "2. 标题栏按钮是否清晰且可点击" -ForegroundColor Red
Write-Host "3. 窗口是否正确遵循工作区边界(不覆盖任务栏)" -ForegroundColor Red
Write-Host ""

# 启动程序
$exePath = ".\UEModManager\bin\Debug\net8.0-windows\UEModManager.exe"
if (Test-Path $exePath) {
    Write-Host "程序已在后台运行，请手动进行以上测试..." -ForegroundColor Green
    Write-Host "测试完成后请在此窗口报告结果" -ForegroundColor Green
    
    # 获取文件信息
    $fileInfo = Get-Item $exePath
    Write-Host "程序信息:" -ForegroundColor Yellow
    Write-Host "文件大小: $($fileInfo.Length) 字节" -ForegroundColor Yellow
    Write-Host "修改时间: $($fileInfo.LastWriteTime)" -ForegroundColor Yellow
    
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