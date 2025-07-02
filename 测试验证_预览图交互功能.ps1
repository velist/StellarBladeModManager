# C2区预览图交互功能测试脚本
# 测试内容：无预览图时显示提示文字，支持点击导入

Write-Host "=== C2区预览图交互功能测试 ===" -ForegroundColor Cyan

# 1. 构建项目
Write-Host "`n1. 构建项目..." -ForegroundColor Yellow
try {
    Set-Location "UEModManager"
    dotnet build --configuration Release
    if ($LASTEXITCODE -ne 0) {
        throw "构建失败"
    }
    Set-Location ".."
    Write-Host "✓ 构建成功" -ForegroundColor Green
} catch {
    Write-Host "✗ 构建失败: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# 2. 启动应用程序
Write-Host "`n2. 启动应用程序进行测试..." -ForegroundColor Yellow
try {
    $processName = "UEModManager"
    
    # 结束已运行的实例
    Get-Process -Name $processName -ErrorAction SilentlyContinue | ForEach-Object {
        Write-Host "结束已运行的程序实例..." -ForegroundColor Yellow
        $_.Kill()
        Start-Sleep -Seconds 2
    }
    
    # 启动新实例
    $process = Start-Process -FilePath "UEModManager\bin\Release\net8.0-windows\UEModManager.exe" -PassThru
    Write-Host "✓ 程序已启动 (PID: $($process.Id))" -ForegroundColor Green
    
    Write-Host "`n=== 测试步骤 ===" -ForegroundColor Cyan
    Write-Host "请按照以下步骤进行测试：" -ForegroundColor White
    Write-Host ""
    Write-Host "1. 检查C2区预览图区域显示" -ForegroundColor Yellow
    Write-Host "   - 选择一个没有预览图的MOD"
    Write-Host "   - 观察C2区是否显示：'请上传图片，建议比例1:1或16:9'"
    Write-Host "   - 观察是否显示：'点击此处导入预览图'"
    Write-Host "   - 观察是否有📷图标显示"
    Write-Host ""
    Write-Host "2. 测试点击导入功能" -ForegroundColor Yellow
    Write-Host "   - 点击C2区的预览图区域"
    Write-Host "   - 检查是否弹出文件选择对话框"
    Write-Host "   - 选择一张图片进行导入"
    Write-Host "   - 确认图片是否正确显示"
    Write-Host ""
    Write-Host "3. 测试无预览图状态" -ForegroundColor Yellow
    Write-Host "   - 选择其他没有预览图的MOD"
    Write-Host "   - 确认是否重新显示提示文字和图标"
    Write-Host ""
    Write-Host "4. 测试有预览图状态" -ForegroundColor Yellow
    Write-Host "   - 选择已设置预览图的MOD"
    Write-Host "   - 确认提示文字是否隐藏，只显示预览图"
    Write-Host "   - 点击预览图区域，确认是否可以修改预览图"
    Write-Host ""
    Write-Host "5. 验证与MOD拖拽导入功能无冲突" -ForegroundColor Yellow
    Write-Host "   - 尝试拖拽MOD文件到主界面"
    Write-Host "   - 确认MOD导入功能正常工作"
    Write-Host "   - 确认不会出现多个弹窗"
    Write-Host ""
    
    Write-Host "=== 预期结果 ===" -ForegroundColor Cyan
    Write-Host "✓ 无预览图时显示友好的提示文字和图标" -ForegroundColor Green
    Write-Host "✓ 点击预览图区域可以导入/修改预览图" -ForegroundColor Green
    Write-Host "✓ 有预览图时隐藏提示文字，正常显示图片" -ForegroundColor Green
    Write-Host "✓ 与全局MOD拖拽导入功能无冲突" -ForegroundColor Green
    Write-Host "✓ UI交互流畅，用户体验良好" -ForegroundColor Green
    Write-Host ""
    
    # 等待用户测试
    Write-Host "请完成上述测试后按任意键继续..." -ForegroundColor Magenta
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    
    Write-Host "`n=== 测试完成 ===" -ForegroundColor Cyan
    Write-Host "如果所有功能正常，预览图交互功能已成功实现！" -ForegroundColor Green
    
} catch {
    Write-Host "✗ 启动失败: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host "`n测试脚本执行完成！" -ForegroundColor Cyan 