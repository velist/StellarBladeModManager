# B1区MOD排序功能测试脚本
# 测试内容：验证排序工具栏和各种排序方式

Write-Host "=== B1区MOD排序功能测试 ===" -ForegroundColor Cyan

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
    Write-Host "1. 检查排序工具栏" -ForegroundColor Yellow
    Write-Host "   - 观察B1区是否显示排序工具栏"
    Write-Host "   - 确认有4个排序按钮：📝名称、📅时间、📊大小、🏷️类型"
    Write-Host "   - 确认右侧有排序方向按钮：🔽(降序)/🔼(升序)"
    Write-Host "   - 确认默认选中'名称'排序，按钮高亮为绿色"
    Write-Host ""
    Write-Host "2. 测试名称排序" -ForegroundColor Yellow
    Write-Host "   - 点击'📝 名称'按钮"
    Write-Host "   - 观察MOD列表是否按名称字母顺序排列"
    Write-Host "   - 点击排序方向按钮，观察排序是否反转"
    Write-Host "   - 再次点击'名称'按钮，确认排序方向切换"
    Write-Host ""
    Write-Host "3. 测试时间排序" -ForegroundColor Yellow
    Write-Host "   - 点击'📅 时间'按钮"
    Write-Host "   - 观察MOD列表是否按导入时间排列"
    Write-Host "   - 确认按钮高亮状态切换到'时间'按钮"
    Write-Host "   - 测试升序/降序切换"
    Write-Host ""
    Write-Host "4. 测试大小排序" -ForegroundColor Yellow
    Write-Host "   - 点击'📊 大小'按钮"
    Write-Host "   - 观察MOD列表是否按文件大小排列"
    Write-Host "   - 确认KB、MB、GB单位正确识别"
    Write-Host "   - 测试升序/降序切换"
    Write-Host ""
    Write-Host "5. 测试类型排序" -ForegroundColor Yellow
    Write-Host "   - 点击'🏷️ 类型'按钮"
    Write-Host "   - 观察MOD列表是否按类型分组排列"
    Write-Host "   - 确认同类型MOD聚集在一起"
    Write-Host "   - 测试升序/降序切换"
    Write-Host ""
    Write-Host "6. 测试排序持久化" -ForegroundColor Yellow
    Write-Host "   - 选择一种排序方式"
    Write-Host "   - 进行搜索或切换分类"
    Write-Host "   - 确认排序设置保持不变"
    Write-Host "   - 导入新MOD，确认新MOD按当前排序插入正确位置"
    Write-Host ""
    Write-Host "7. 测试UI响应性" -ForegroundColor Yellow
    Write-Host "   - 快速点击不同排序按钮"
    Write-Host "   - 确认界面响应流畅，无卡顿"
    Write-Host "   - 确认按钮状态正确切换"
    Write-Host ""
    
    Write-Host "=== 预期结果 ===" -ForegroundColor Cyan
    Write-Host "✓ 排序工具栏美观整洁，样式与整体UI协调" -ForegroundColor Green
    Write-Host "✓ 四种排序方式功能正常，排序逻辑正确" -ForegroundColor Green
    Write-Host "✓ 升序/降序切换正常，图标状态正确显示" -ForegroundColor Green
    Write-Host "✓ 按钮高亮状态正确反映当前排序字段" -ForegroundColor Green
    Write-Host "✓ 排序设置在过滤和搜索时保持一致" -ForegroundColor Green
    Write-Host "✓ 界面响应流畅，用户体验良好" -ForegroundColor Green
    Write-Host "✓ 参考Windows文件管理器的交互逻辑" -ForegroundColor Green
    Write-Host ""
    
    # 等待用户测试
    Write-Host "请完成上述测试后按任意键继续..." -ForegroundColor Magenta
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    
    Write-Host "`n=== 测试完成 ===" -ForegroundColor Cyan
    Write-Host "如果所有功能正常，MOD排序功能已成功实现！" -ForegroundColor Green
    
} catch {
    Write-Host "✗ 启动失败: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host "`n测试脚本执行完成！" -ForegroundColor Cyan 