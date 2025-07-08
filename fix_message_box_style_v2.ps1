# 统一弹窗样式，替换MessageBox.Show为ShowCustomMessageBox（改进版）
# 创建于: 2025-07-07

$ErrorActionPreference = "Stop"
Write-Host "开始统一弹窗样式（改进版）..." -ForegroundColor Cyan

# 设置工作目录
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptPath
Write-Host "当前工作目录: $scriptPath" -ForegroundColor Gray

# 要处理的文件
$filePath = Join-Path $scriptPath "UEModManager\MainWindow.xaml.cs"

if (Test-Path $filePath) {
    Write-Host "处理文件: MainWindow.xaml.cs" -ForegroundColor Yellow
    
    # 读取文件内容
    $content = Get-Content -Path $filePath -Raw
    
    # 备份文件
    $backupPath = "$filePath.bak.$(Get-Date -Format 'yyyyMMdd_HHmmss')"
    Copy-Item -Path $filePath -Destination $backupPath -Force
    Write-Host "已创建备份: $backupPath" -ForegroundColor Gray
    
    # 使用正则表达式替换MessageBox.Show调用
    # 这个模式更精确地匹配MessageBox.Show调用，包括多行参数
    $pattern = 'MessageBox\.Show\s*\(\s*([^)]+?)\s*\)'
    
    # 执行替换
    $replacedContent = $content
    $matches = [regex]::Matches($content, $pattern)
    $replacedCount = 0
    
    foreach ($match in $matches) {
        $originalCall = $match.Value
        $parameters = $match.Groups[1].Value
        $newCall = "ShowCustomMessageBox($parameters)"
        
        # 替换当前匹配
        $replacedContent = $replacedContent.Replace($originalCall, $newCall)
        $replacedCount++
    }
    
    # 保存修改后的内容
    Set-Content -Path $filePath -Value $replacedContent
    
    Write-Host "替换完成，共替换了 $replacedCount 处MessageBox.Show调用" -ForegroundColor Green
}
else {
    Write-Host "文件不存在: $filePath" -ForegroundColor Red
}

Write-Host "统一弹窗样式完成！" -ForegroundColor Cyan 