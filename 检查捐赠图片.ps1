$baseDir = $PSScriptRoot
$donationImagePath = Join-Path -Path $baseDir -ChildPath "捐赠.png"

Write-Host "基础目录: $baseDir"
Write-Host "捐赠图片路径: $donationImagePath"

if (Test-Path $donationImagePath) {
    Write-Host "✅ 图片文件存在" -ForegroundColor Green
    
    # 获取文件信息
    $fileInfo = Get-Item $donationImagePath
    Write-Host "文件大小: $([Math]::Round($fileInfo.Length / 1KB, 2)) KB" -ForegroundColor Cyan
    Write-Host "创建时间: $($fileInfo.CreationTime)" -ForegroundColor Cyan
    Write-Host "修改时间: $($fileInfo.LastWriteTime)" -ForegroundColor Cyan
} else {
    Write-Host "❌ 图片文件不存在!" -ForegroundColor Red
    
    # 检查UEModManager/bin目录下是否有此文件
    $binPath = Join-Path -Path $baseDir -ChildPath "UEModManager\bin\Debug"
    if (Test-Path $binPath) {
        $binDonationPath = Join-Path -Path $binPath -ChildPath "捐赠.png"
        if (Test-Path $binDonationPath) {
            Write-Host "📁 在bin目录下找到了捐赠图片: $binDonationPath" -ForegroundColor Yellow
        } else {
            Write-Host "❌ bin目录下也没有找到捐赠图片" -ForegroundColor Red
        }
    }
    
    # 在整个目录结构中搜索捐赠图片
    Write-Host "🔍 正在搜索捐赠图片文件..." -ForegroundColor Yellow
    $foundFiles = Get-ChildItem -Path $baseDir -Filter "捐赠.png" -Recurse -ErrorAction SilentlyContinue
    
    if ($foundFiles.Count -gt 0) {
        Write-Host "✅ 找到 $($foundFiles.Count) 个捐赠图片文件:" -ForegroundColor Green
        foreach ($file in $foundFiles) {
            Write-Host "  - $($file.FullName)" -ForegroundColor Cyan
        }
    } else {
        Write-Host "❌ 在整个项目中未找到捐赠图片文件" -ForegroundColor Red
    }
}

Write-Host "`n按任意键退出..." -ForegroundColor Gray
$null = $host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown") 