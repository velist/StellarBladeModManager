# 清空当前文件夹，从备份恢复，编译运行测试
# 创建于: 2025-07-07

$ErrorActionPreference = "Stop"
Write-Host "开始清空当前文件夹并从备份恢复..." -ForegroundColor Cyan

# 设置工作目录
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptPath
Write-Host "当前工作目录: $scriptPath" -ForegroundColor Gray

# 检查备份目录
$backupPath = "D:\cursor\2.0backup\StellarBladeModManager"
if (-not (Test-Path $backupPath)) {
    Write-Host "错误: 找不到备份目录: $backupPath" -ForegroundColor Red
    Read-Host "按任意键退出"
    exit 1
}

# 停止现有进程
try {
    Get-Process -Name "UEModManager" -ErrorAction SilentlyContinue | ForEach-Object { 
        $_.Kill()
        $_.WaitForExit(3000)
    }
    Write-Host "已停止所有UEModManager进程" -ForegroundColor Green
} catch {
    Write-Host "停止进程时出错: $_" -ForegroundColor Red
}

# 清空当前文件夹（保留当前脚本）
Write-Host "正在清空当前文件夹..." -ForegroundColor Yellow
$currentScript = $MyInvocation.MyCommand.Path
$currentScriptName = Split-Path $currentScript -Leaf
$itemsToRemove = Get-ChildItem -Path $scriptPath | Where-Object { $_.Name -ne $currentScriptName }

foreach ($item in $itemsToRemove) {
    try {
        if ($item.PSIsContainer) {
            Remove-Item $item.FullName -Recurse -Force -ErrorAction SilentlyContinue
        } else {
            Remove-Item $item.FullName -Force -ErrorAction SilentlyContinue
        }
    } catch {
        Write-Host "无法删除 $($item.FullName): $_" -ForegroundColor Red
    }
}
Write-Host "当前文件夹已清空（保留当前脚本）" -ForegroundColor Green

# 从备份恢复文件
Write-Host "正在从备份恢复文件..." -ForegroundColor Yellow
$itemsToRestore = Get-ChildItem -Path $backupPath

foreach ($item in $itemsToRestore) {
    try {
        if ($item.PSIsContainer) {
            Copy-Item $item.FullName -Destination $scriptPath -Recurse -Force
        } else {
            Copy-Item $item.FullName -Destination $scriptPath -Force
        }
    } catch {
        Write-Host "无法复制 $($item.FullName): $_" -ForegroundColor Red
    }
}
Write-Host "文件已从备份恢复" -ForegroundColor Green

# 尝试编译项目
Write-Host "正在尝试编译项目..." -ForegroundColor Yellow
$solutionPath = Join-Path $scriptPath "UEModManager.sln"

if (Test-Path $solutionPath) {
    try {
        # 尝试使用dotnet命令编译
        dotnet build $solutionPath --configuration Debug
        if ($LASTEXITCODE -eq 0) {
            Write-Host "编译成功" -ForegroundColor Green
        } else {
            Write-Host "编译失败，错误代码: $LASTEXITCODE" -ForegroundColor Red
        }
    } catch {
        Write-Host "编译时出错: $_" -ForegroundColor Red
    }
} else {
    Write-Host "错误: 找不到解决方案文件: $solutionPath" -ForegroundColor Red
}

# 查找并运行编译后的程序
$programPath = Join-Path $scriptPath "UEModManager\bin\Debug\net8.0-windows\UEModManager.exe"
if (Test-Path $programPath) {
    Write-Host "正在启动程序..." -ForegroundColor Green
    try {
        Start-Process $programPath
        Write-Host "程序已启动，请检查窗口是否显示并能正常交互" -ForegroundColor Yellow
    } catch {
        Write-Host "启动程序时出错: $_" -ForegroundColor Red
    }
} else {
    # 尝试查找备份中的已编译程序
    $backupExePath = Join-Path $backupPath "UEModManager\bin\Debug\net8.0-windows\UEModManager.exe"
    if (Test-Path $backupExePath) {
        Write-Host "找到备份中的已编译程序，正在启动..." -ForegroundColor Green
        try {
            Start-Process $backupExePath
            Write-Host "备份程序已启动，请检查窗口是否显示并能正常交互" -ForegroundColor Yellow
        } catch {
            Write-Host "启动备份程序时出错: $_" -ForegroundColor Red
        }
    } else {
        Write-Host "错误: 找不到已编译的程序" -ForegroundColor Red
    }
}

Write-Host "清空、恢复和测试过程完成" -ForegroundColor Cyan 