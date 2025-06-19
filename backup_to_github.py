import os
import sys
import subprocess
import shutil
from pathlib import Path
from datetime import datetime

# 要备份的文件列表
ESSENTIAL_FILES = [
    "main.py",
    "run_new_ui.py",
    "utils/mod_manager.py", 
    "utils/config_manager.py",
    "utils/game_locator.py",
    "ui/main_window.py",
    "ui/new_main_window.py",
    "ui/style.qss",
    "ui/new_style.qss",
    "config.json",
    "requirements.txt",
    "README.md",
    "LICENSE"
]

# 要备份的目录列表
ESSENTIAL_DIRS = [
    "icons"
]

def run_command(command, cwd=None):
    """运行命令行命令并返回输出"""
    try:
        result = subprocess.run(
            command,
            shell=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            cwd=cwd
        )
        print(result.stdout)
        if result.stderr:
            print(f"错误: {result.stderr}")
        return result.returncode == 0
    except Exception as e:
        print(f"命令执行失败: {e}")
        return False

def backup_files():
    """备份文件到'testbackup'目录"""
    print("开始备份文件")
    
    # 创建备份目录
    backup_dir = Path("test_backup")
    if backup_dir.exists():
        shutil.rmtree(backup_dir)
    backup_dir.mkdir()
    
    # 复制必要文件
    print("复制必要文件...")
    for file_path in ESSENTIAL_FILES:
        src = Path(file_path)
        dst = backup_dir / file_path
        
        if not src.exists():
            print(f"警告: 文件不存在: {src}")
            continue
            
        # 确保目标目录存在
        dst.parent.mkdir(parents=True, exist_ok=True)
        
        # 复制文件
        shutil.copy2(src, dst)
        print(f"已复制: {file_path}")
    
    # 复制必要目录
    for dir_path in ESSENTIAL_DIRS:
        src = Path(dir_path)
        dst = backup_dir / dir_path
        
        if not src.exists():
            print(f"警告: 目录不存在: {src}")
            continue
            
        # 复制整个目录
        shutil.copytree(src, dst)
        print(f"已复制目录: {dir_path}")
    
    print(f"备份完成! 文件已保存到 {backup_dir} 目录")
    return True

def create_zip_backup():
    """创建ZIP备份文件"""
    import zipfile
    
    backup_dir = Path("test_backup")
    if not backup_dir.exists():
        print("备份目录不存在，请先运行备份")
        return False
        
    # 创建ZIP文件名，包含时间戳
    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    zip_filename = f"1.6.4_test_backup_{timestamp}.zip"
    
    print(f"创建ZIP备份: {zip_filename}")
    
    # 创建ZIP文件
    with zipfile.ZipFile(zip_filename, 'w', zipfile.ZIP_DEFLATED) as zipf:
        for root, dirs, files in os.walk(backup_dir):
            for file in files:
                file_path = Path(root) / file
                arc_name = file_path.relative_to(backup_dir)
                zipf.write(file_path, arc_name)
                print(f"添加到ZIP: {arc_name}")
    
    print(f"ZIP备份创建成功: {zip_filename}")
    return zip_filename

if __name__ == "__main__":
    try:
        if backup_files():
            create_zip_backup()
            print("\n备份过程完成!")
            print("您现在可以将生成的ZIP文件手动上传到GitHub仓库")
    except Exception as e:
        print(f"\n备份过程中出错: {str(e)}")
        import traceback
        traceback.print_exc() 