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
    Write-Host "1. 切换游戏时的确认弹窗是否显示完整" -ForegroundColor Yellow
    Write-Host "2. 确认弹窗的信息是否清晰可见" -ForegroundColor Yellow
    Write-Host "3. 确认弹窗按钮是否正常工作" -ForegroundColor Yellow
    
} catch {
    Write-Host "发生错误：$($_.Exception.Message)" -ForegroundColor Red
    exit 1
} 