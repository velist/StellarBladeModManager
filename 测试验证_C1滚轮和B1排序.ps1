# 测试验证脚本 - 验证C1区滚轮和移除B1区排序按钮的修复
# 作者: Claude 3.7
# 日期: 2023-11-08

Write-Host "开始验证 C1区滚轮和移除B1区排序按钮修复..." -ForegroundColor Cyan

# 检查修复步骤
$mainWindowXaml = Get-Content -Path ".\UEModManager\MainWindow.xaml" -Raw
$mainWindowCs = Get-Content -Path ".\UEModManager\MainWindow.xaml.cs" -Raw

$fixes = @{
    "移除B1区排序按钮" = $mainWindowXaml -notmatch "SortByNameBtn|SortByDateBtn|SortBySizeBtn|SortOrderBtn"
    "捐赠提示文字添加" = $mainWindowXaml -match "DonationHintText"
    "C1区滚轮处理" = $mainWindowCs -match "mainContentScrollViewer.ScrollToVerticalOffset"
}

# 显示修复验证结果
Write-Host "`n修复验证结果:" -ForegroundColor Yellow
foreach ($fix in $fixes.GetEnumerator()) {
    if ($fix.Value) {
        Write-Host "✅ $($fix.Key) - 已修复" -ForegroundColor Green
    } else {
        Write-Host "❌ $($fix.Key) - 未修复" -ForegroundColor Red
    }
}

# 编译运行
Write-Host "`n正在编译运行项目测试修复效果..." -ForegroundColor Cyan
cd .\UEModManager
$buildResult = dotnet build
if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ 编译成功，请手动启动并测试以下功能:" -ForegroundColor Green
    Write-Host "  1. C1区鼠标滚轮是否能正常在列表区域滚动" -ForegroundColor Yellow
    Write-Host "  2. B1区是否已移除排序按钮" -ForegroundColor Yellow
    Write-Host "  3. 右下角捐赠悬停时是否显示蜜雪冰城提示文字" -ForegroundColor Yellow
} else {
    Write-Host "❌ 编译失败，请检查代码修改" -ForegroundColor Red
}

Write-Host "`n测试完成！" -ForegroundColor Cyan 