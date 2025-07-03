$baseDir = $PSScriptRoot

# 捐赠图片的当前位置
$sourcePath = Join-Path -Path $baseDir -ChildPath "UEModManager\bin\Debug\net8.0-windows\捐赠.png"

if (Test-Path $sourcePath) {
    # 复制到项目根目录
    $targetPath1 = Join-Path -Path $baseDir -ChildPath "捐赠.png"
    Copy-Item -Path $sourcePath -Destination $targetPath1 -Force
    Write-Host "✅ 已复制捐赠图片到项目根目录: $targetPath1" -ForegroundColor Green
    
    # 复制到UEModManager项目目录
    $targetPath2 = Join-Path -Path $baseDir -ChildPath "UEModManager\捐赠.png"
    Copy-Item -Path $sourcePath -Destination $targetPath2 -Force
    Write-Host "✅ 已复制捐赠图片到UEModManager项目目录: $targetPath2" -ForegroundColor Green
    
    # 创建一个.csproj文件修改建议
    $projFilePath = Join-Path -Path $baseDir -ChildPath "UEModManager\UEModManager.csproj"
    if (Test-Path $projFilePath) {
        Write-Host "`n建议修改项目文件，将捐赠图片添加为资源。在UEModManager.csproj文件中添加以下内容:" -ForegroundColor Yellow
        Write-Host '  <ItemGroup>' -ForegroundColor Cyan
        Write-Host '    <None Update="捐赠.png">' -ForegroundColor Cyan
        Write-Host '      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>' -ForegroundColor Cyan
        Write-Host '    </None>' -ForegroundColor Cyan
        Write-Host '  </ItemGroup>' -ForegroundColor Cyan
    }
} else {
    Write-Host "❌ 找不到捐赠图片源文件: $sourcePath" -ForegroundColor Red
    
    # 尝试全局搜索捐赠图片
    Write-Host "🔍 正在搜索捐赠图片文件..." -ForegroundColor Yellow
    $foundFiles = Get-ChildItem -Path $baseDir -Filter "捐赠.png" -Recurse -ErrorAction SilentlyContinue
    
    if ($foundFiles.Count -gt 0) {
        Write-Host "✅ 找到 $($foundFiles.Count) 个捐赠图片文件:" -ForegroundColor Green
        foreach ($file in $foundFiles) {
            Write-Host "  - $($file.FullName)" -ForegroundColor Cyan
        }
        
        # 使用找到的第一个图片作为源
        $sourcePath = $foundFiles[0].FullName
        
        # 复制到项目根目录
        $targetPath1 = Join-Path -Path $baseDir -ChildPath "捐赠.png"
        Copy-Item -Path $sourcePath -Destination $targetPath1 -Force
        Write-Host "✅ 已复制捐赠图片到项目根目录: $targetPath1" -ForegroundColor Green
        
        # 复制到UEModManager项目目录
        $targetPath2 = Join-Path -Path $baseDir -ChildPath "UEModManager\捐赠.png"
        Copy-Item -Path $sourcePath -Destination $targetPath2 -Force
        Write-Host "✅ 已复制捐赠图片到UEModManager项目目录: $targetPath2" -ForegroundColor Green
    } else {
        Write-Host "❌ 在整个项目中未找到捐赠图片文件" -ForegroundColor Red
    }
}

Write-Host "`n按任意键退出..." -ForegroundColor Gray
$null = $host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown") 