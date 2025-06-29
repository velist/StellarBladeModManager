# GitHub备份脚本
Write-Host "=== UEModManager GitHub备份脚本 ===" -ForegroundColor Green
Write-Host "时间: $(Get-Date)" -ForegroundColor Yellow

# 检查Git状态
Write-Host "检查Git状态..." -ForegroundColor Yellow
git status

# 显示最近的提交
Write-Host "`n最近的提交:" -ForegroundColor Yellow
git log --oneline -5

# 尝试推送到GitHub
Write-Host "`n正在推送到GitHub..." -ForegroundColor Yellow
$maxRetries = 3
$retryCount = 0

while ($retryCount -lt $maxRetries) {
    try {
        git push origin v1.9
        if ($LASTEXITCODE -eq 0) {
            Write-Host "成功推送到GitHub!" -ForegroundColor Green
            break
        } else {
            throw "推送失败，退出代码: $LASTEXITCODE"
        }
    } catch {
        $retryCount++
        Write-Host "推送失败 (尝试 $retryCount/$maxRetries): $($_.Exception.Message)" -ForegroundColor Red
        
        if ($retryCount -lt $maxRetries) {
            Write-Host "等待10秒后重试..." -ForegroundColor Yellow
            Start-Sleep -Seconds 10
        } else {
            Write-Host "所有重试都失败了。" -ForegroundColor Red
            Write-Host "请稍后手动执行: git push origin v1.9" -ForegroundColor Yellow
            
            # 显示备用方案
            Write-Host "`n备用方案:" -ForegroundColor Cyan
            Write-Host "1. 检查网络连接" -ForegroundColor White
            Write-Host "2. 尝试使用VPN或更换网络" -ForegroundColor White
            Write-Host "3. 稍后重新运行此脚本" -ForegroundColor White
            Write-Host "4. 手动执行: git push origin v1.9" -ForegroundColor White
        }
    }
}

Write-Host "`n当前分支信息:" -ForegroundColor Yellow
git branch -v

Write-Host "`n远程仓库信息:" -ForegroundColor Yellow
git remote -v

Read-Host "`n按任意键退出" 