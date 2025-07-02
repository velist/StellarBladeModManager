Write-Host "开始测试 - 右键菜单'重命名'改为'编辑'及编辑框调整" -ForegroundColor Green

# 构建并启动应用程序
dotnet build UEModManager
if ($LASTEXITCODE -ne 0) {
    Write-Host "构建失败，请检查代码" -ForegroundColor Red
    exit 1
}

Write-Host "应用程序已成功构建，准备启动..." -ForegroundColor Yellow
Write-Host "测试步骤：" -ForegroundColor Cyan
Write-Host "1. 在MOD列表中右键点击任意MOD，确认菜单中显示'编辑'而不是'重命名'" -ForegroundColor White
Write-Host "2. 点击'编辑'选项，确认弹出的是编辑对话框，可以同时修改名称和描述" -ForegroundColor White
Write-Host "3. 检查编辑框的大小是否合适，文本是否完整显示" -ForegroundColor White
Write-Host "4. 在右侧详情面板中，点击'编辑'按钮，确认功能与右键菜单的'编辑'一致" -ForegroundColor White
Write-Host "5. 确认描述文本在详情面板中显示完整" -ForegroundColor White

# 启动应用程序
Write-Host "正在启动应用程序..." -ForegroundColor Yellow
Start-Process "dotnet" -ArgumentList "run --project UEModManager" -NoNewWindow

Write-Host "测试完成后请关闭应用程序" -ForegroundColor Green 