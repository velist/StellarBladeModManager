# 测试验证：分类排序和视图切换功能
# 日期：2024-07-03

Write-Host "=== 虚幻引擎MOD管理器 - 分类排序和视图切换功能测试 ===" -ForegroundColor Cyan
Write-Host ""

# 检查项目结构
Write-Host "1. 检查项目文件结构..." -ForegroundColor Yellow
$projectFiles = @(
    "UEModManager.sln",
    "UEModManager/MainWindow.xaml",
    "UEModManager/MainWindow.xaml.cs"
)

foreach ($file in $projectFiles) {
    if (Test-Path $file) {
        Write-Host "  ✓ $file 存在" -ForegroundColor Green
    } else {
        Write-Host "  ✗ $file 缺失" -ForegroundColor Red
        exit 1
    }
}

# 编译项目
Write-Host ""
Write-Host "2. 编译项目..." -ForegroundColor Yellow
try {
    $buildResult = dotnet build UEModManager.sln --configuration Release --verbosity quiet
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  ✓ 项目编译成功" -ForegroundColor Green
    } else {
        Write-Host "  ✗ 项目编译失败" -ForegroundColor Red
        Write-Host "构建输出:" -ForegroundColor Red
        Write-Host $buildResult
        exit 1
    }
} catch {
    Write-Host "  ✗ 编译过程中发生错误: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# 启动应用程序
Write-Host ""
Write-Host "3. 启动应用程序..." -ForegroundColor Yellow
try {
    $exePath = "UEModManager/bin/Release/net8.0-windows/UEModManager.exe"
    if (Test-Path $exePath) {
        Write-Host "  ✓ 找到可执行文件：$exePath" -ForegroundColor Green
        
        # 启动程序（后台运行）
        $process = Start-Process -FilePath $exePath -PassThru
        Write-Host "  ✓ 应用程序已启动（进程ID: $($process.Id)）" -ForegroundColor Green
        
        # 等待程序完全启动
        Start-Sleep -Seconds 3
        
        # 检查程序是否仍在运行
        if (!$process.HasExited) {
            Write-Host "  ✓ 程序正常运行中" -ForegroundColor Green
        } else {
            Write-Host "  ✗ 程序启动后立即退出，可能存在错误" -ForegroundColor Red
            exit 1
        }
    } else {
        Write-Host "  ✗ 找不到可执行文件：$exePath" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "  ✗ 启动应用程序时发生错误: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# 显示测试说明
Write-Host ""
Write-Host "=== 手动测试项目 ===" -ForegroundColor Cyan
Write-Host ""

Write-Host "【B1区分类排序功能测试】" -ForegroundColor Yellow
Write-Host "1. 检查B1区（左侧分类面板）是否显示分类排序控制栏" -ForegroundColor White
Write-Host "   - 应在分类操作按钮下方看到排序控制栏" -ForegroundColor Gray
Write-Host "   - 包含'排序:'标签、'名称'和'数量'按钮、排序方向按钮" -ForegroundColor Gray
Write-Host ""

Write-Host "2. 测试分类名称排序功能" -ForegroundColor White
Write-Host "   - 点击'名称'按钮，观察分类列表是否按名称排序" -ForegroundColor Gray
Write-Host "   - 再次点击'名称'按钮，观察排序方向是否翻转" -ForegroundColor Gray
Write-Host "   - 按钮样式应该高亮显示当前激活的排序方式" -ForegroundColor Gray
Write-Host ""

Write-Host "3. 测试分类数量排序功能" -ForegroundColor White
Write-Host "   - 点击'数量'按钮，观察分类列表是否按MOD数量排序" -ForegroundColor Gray
Write-Host "   - 验证有MOD的分类排在前面，空分类排在后面" -ForegroundColor Gray
Write-Host ""

Write-Host "4. 测试排序方向切换" -ForegroundColor White
Write-Host "   - 点击排序方向按钮（🔽/🔼），观察列表顺序是否颠倒" -ForegroundColor Gray
Write-Host "   - 图标应在升序（🔽）和降序（🔼）之间切换" -ForegroundColor Gray
Write-Host ""

Write-Host "【C1区视图切换功能测试】" -ForegroundColor Yellow
Write-Host "5. 检查C1区（中间MOD列表）工具栏" -ForegroundColor White
Write-Host "   - 应在MOD数量显示下方看到工具栏" -ForegroundColor Gray
Write-Host "   - 左侧包含排序选项：📝名称、📅时间、📊大小和方向按钮" -ForegroundColor Gray
Write-Host "   - 右侧包含视图切换：视图标签、⊞卡片视图、☰列表视图" -ForegroundColor Gray
Write-Host ""

Write-Host "6. 测试卡片视图（默认）" -ForegroundColor White
Write-Host "   - 默认应显示卡片视图，⊞按钮高亮" -ForegroundColor Gray
Write-Host "   - MOD以卡片形式网格排列显示" -ForegroundColor Gray
Write-Host "   - 每个卡片显示预览图、名称、描述等信息" -ForegroundColor Gray
Write-Host ""

Write-Host "7. 测试切换到列表视图" -ForegroundColor White
Write-Host "   - 点击☰列表视图按钮" -ForegroundColor Gray
Write-Host "   - 界面应切换为表格式列表显示" -ForegroundColor Gray
Write-Host "   - 每行显示：缩略图、名称、状态、日期、大小、操作按钮" -ForegroundColor Gray
Write-Host "   - ☰按钮应高亮，⊞按钮恢复普通样式" -ForegroundColor Gray
Write-Host ""

Write-Host "8. 测试切换回卡片视图" -ForegroundColor White
Write-Host "   - 点击⊞卡片视图按钮" -ForegroundColor Gray
Write-Host "   - 界面应切换回卡片显示模式" -ForegroundColor Gray
Write-Host "   - ⊞按钮重新高亮，☰按钮恢复普通样式" -ForegroundColor Gray
Write-Host ""

Write-Host "9. 测试列表视图中的操作" -ForegroundColor White
Write-Host "   - 在列表视图中点击MOD项目，应能正常选中" -ForegroundColor Gray
Write-Host "   - 右侧详情面板应正常显示选中MOD的信息" -ForegroundColor Gray
Write-Host "   - 列表中的操作按钮（滑块开关、编辑、删除）应正常工作" -ForegroundColor Gray
Write-Host ""

Write-Host "10. 测试视图切换时的数据一致性" -ForegroundColor White
Write-Host "    - 在一个视图中选中某个MOD" -ForegroundColor Gray
Write-Host "    - 切换到另一个视图，选中状态应保持" -ForegroundColor Gray
Write-Host "    - 排序设置在两个视图间应保持一致" -ForegroundColor Gray
Write-Host ""

Write-Host "【综合功能测试】" -ForegroundColor Yellow
Write-Host "11. 测试排序功能在不同视图中的表现" -ForegroundColor White
Write-Host "    - 在卡片视图中进行排序，然后切换到列表视图" -ForegroundColor Gray
Write-Host "    - 排序结果应在两个视图中保持一致" -ForegroundColor Gray
Write-Host ""

Write-Host "12. 测试分类筛选与视图切换的兼容性" -ForegroundColor White
Write-Host "    - 选择不同分类，在两种视图间切换" -ForegroundColor Gray
Write-Host "    - MOD筛选结果应在两个视图中保持一致" -ForegroundColor Gray
Write-Host ""

Write-Host "=== 预期结果 ===" -ForegroundColor Cyan
Write-Host "✓ B1区分类排序功能正常工作" -ForegroundColor Green
Write-Host "✓ C1区视图可以在卡片和列表模式间自由切换" -ForegroundColor Green
Write-Host "✓ 列表视图显示详细的表格信息" -ForegroundColor Green
Write-Host "✓ 按钮样式正确反映当前状态" -ForegroundColor Green
Write-Host "✓ 数据在不同视图间保持一致性" -ForegroundColor Green
Write-Host "✓ 所有交互功能在两种视图中都正常工作" -ForegroundColor Green
Write-Host ""

Write-Host "程序正在运行中，请按照上述步骤进行测试..." -ForegroundColor Cyan
Write-Host "测试完成后，请手动关闭程序窗口。" -ForegroundColor Yellow
Write-Host ""
Write-Host "按任意键结束测试脚本..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")

# 尝试优雅关闭程序
try {
    if (!$process.HasExited) {
        Write-Host "正在关闭应用程序..." -ForegroundColor Yellow
        $process.CloseMainWindow()
        Start-Sleep -Seconds 2
        
        if (!$process.HasExited) {
            Write-Host "强制终止应用程序..." -ForegroundColor Yellow
            $process.Kill()
        }
        Write-Host "应用程序已关闭" -ForegroundColor Green
    }
} catch {
    Write-Host "关闭程序时发生错误: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "测试脚本执行完成！" -ForegroundColor Green 