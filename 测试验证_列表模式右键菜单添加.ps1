$ErrorActionPreference = "Stop"

# 设置编译的配置
$BuildConfiguration = "Release"
$ProjectPath = ".\UEModManager\UEModManager.csproj"
$OutputPath = ".\UEModManager\bin\$BuildConfiguration"

# 显示编译信息
Write-Host "开始编译项目：$ProjectPath" -ForegroundColor Green
Write-Host "配置：$BuildConfiguration" -ForegroundColor Green
Write-Host "输出目录：$OutputPath" -ForegroundColor Green

try {
    # 编译项目
    Write-Host "正在编译项目..." -ForegroundColor Yellow
    dotnet build $ProjectPath -c $BuildConfiguration
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "编译失败，错误代码：$LASTEXITCODE" -ForegroundColor Red
        exit $LASTEXITCODE
    }
    
    Write-Host "编译成功！" -ForegroundColor Green
    
    # 启动应用程序
    $ExePath = Join-Path $OutputPath "net8.0-windows\UEModManager.exe"
    Write-Host "正在启动应用程序：$ExePath" -ForegroundColor Yellow
    Start-Process $ExePath
    
    Write-Host "测试要点：" -ForegroundColor Cyan
    Write-Host "1. 点击列表视图按钮切换到列表模式" -ForegroundColor Yellow
    Write-Host "2. 在列表模式下右键点击MOD，验证右键菜单是否显示" -ForegroundColor Yellow
    Write-Host "3. 点击右键菜单中的'编辑'选项，验证弹窗是否包含名称和描述编辑功能" -ForegroundColor Yellow
    Write-Host "4. 验证编辑功能是否正常保存MOD的名称和描述信息" -ForegroundColor Yellow
    Write-Host "5. 验证列表模式下右键菜单的其他功能是否正常工作" -ForegroundColor Yellow
    
} catch {
    Write-Host "发生错误：$($_.Exception.Message)" -ForegroundColor Red
    exit 1
} 