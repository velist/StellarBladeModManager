import sys
import os
import traceback
import logging
from pathlib import Path
from PySide6.QtWidgets import QApplication, QMainWindow, QMessageBox, QFileDialog, QWidget
from PySide6.QtCore import Qt, QTranslator, QLocale
from PySide6.QtGui import QIcon, QFont
from ui.main_window import MainWindow
from utils.config_manager import ConfigManager
from utils.mod_manager import ModManager, cleanup_temp_directories
from utils.game_locator import GameLocator

# 配置日志
def setup_logging():
    """设置日志"""
    logging.basicConfig(
        level=logging.INFO,
        format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
        handlers=[
            logging.FileHandler("mod_manager.log", encoding='utf-8'),
            logging.StreamHandler()
        ]
    )

def resource_path(relative_path):
    """获取资源的绝对路径，用于处理打包后的资源访问"""
    try:
        base_path = sys._MEIPASS
    except Exception:
        base_path = os.path.abspath(".")
    return os.path.join(base_path, relative_path)

def setup_paths():
    """设置程序运行时需要的路径"""
    # 创建备份目录
    backup_dir = Path("modbackup")
    backup_dir.mkdir(exist_ok=True)
    
    # 创建错误日志目录
    log_dir = Path("logs")
    log_dir.mkdir(exist_ok=True)

def setup_qt_application():
    """设置QT应用程序"""
    if hasattr(Qt, 'AA_EnableHighDpiScaling'):  # 5.6.0 之前
        QApplication.setAttribute(Qt.AA_EnableHighDpiScaling, True)
    if hasattr(Qt, 'AA_UseHighDpiPixmaps'):
        QApplication.setAttribute(Qt.AA_UseHighDpiPixmaps, True)

    app = QApplication(sys.argv)
    
    # 设置应用程序图标，使用实际存在的图标文件
    icon_path = Path("icons/app.ico")
    if icon_path.exists():
        app.setWindowIcon(QIcon(str(icon_path)))
    else:
        # 尝试使用其他图标
        alternative_icons = ["11.ico", "4.ico", "icons/4.png"]
        for alt_icon in alternative_icons:
            if Path(alt_icon).exists():
                app.setWindowIcon(QIcon(alt_icon))
                print(f"使用替代图标: {alt_icon}")
                break

    # 不尝试加载字体和样式表，简化启动过程
    return app

def initialize_config(config):
    """初始化配置，设置游戏路径、MOD路径和备份路径"""
    try:
        print("[调试] 首次运行，进行初始化")
        
        # 首次运行，自动定位游戏路径或请求用户手动选择
        locator = GameLocator()
        game_paths = locator.find_game_paths()
        
        if game_paths:
            print(f"[调试] 找到游戏路径: {game_paths}")
            config.set_game_path(game_paths[0])  # 使用第一个找到的路径
            game_dir = os.path.dirname(game_paths[0])
            
            # 尝试设置MOD路径
            mods_path = os.path.join(game_dir, "SB", "Content", "Paks", "~mods")
            mods_path_dir = Path(mods_path)
            if not mods_path_dir.exists():
                try:
                    mods_path_dir.mkdir(parents=True, exist_ok=True)
                    print(f"[调试] 创建MOD目录: {mods_path}")
                except Exception as e:
                    print(f"[警告] 创建MOD目录失败: {str(e)}")
            
            config.set_mods_path(mods_path)
            print(f"[调试] 设置MOD文件夹: {mods_path}")
            
            # 设置备份路径 - 先使用默认路径
            default_backup_path = os.path.join(os.getcwd(), "modbackup")
            backup_dir = Path(default_backup_path)
            if not backup_dir.exists():
                backup_dir.mkdir(parents=True, exist_ok=True)
            
            # 提示用户选择备份目录或使用默认目录
            msg = QMessageBox()
            msg.setIcon(QMessageBox.Question)
            msg.setText("是否使用默认备份目录？")
            msg.setInformativeText(f"默认备份目录: {default_backup_path}\n\n选择\"是\"使用默认备份目录\n选择\"否\"手动选择备份目录")
            msg.setWindowTitle("备份目录设置")
            msg.setStandardButtons(QMessageBox.Yes | QMessageBox.No)
            msg.button(QMessageBox.Yes).setText('是')
            msg.button(QMessageBox.No).setText('否')
            
            reply = msg.exec()
            
            if reply == QMessageBox.No:
                # 用户选择手动设置备份目录
                backup_path = QFileDialog.getExistingDirectory(
                    None,
                    "选择MOD备份文件夹",
                    os.getcwd()
                )
                
                if not backup_path:
                    # 如果用户取消选择，使用默认路径并提示
                    backup_path = default_backup_path
                    QMessageBox.information(None, "提示", f"未选择备份目录，将使用默认备份目录:\n{default_backup_path}")
            else:
                # 用户选择使用默认备份目录
                backup_path = default_backup_path
                QMessageBox.information(None, "提示", f"将使用默认备份目录:\n{default_backup_path}")
            
            config.set_backup_path(backup_path)
            print(f"[调试] 设置备份路径: {backup_path}")
            
            # 标记为已初始化
            config.set_initialized(True)
            return True
        else:
            # 如果自动查找失败，请求用户手动选择
            print("[调试] 未找到游戏路径，请求用户手动选择")
            
            # 提示用户选择游戏可执行文件
            msg = QMessageBox()
            msg.setIcon(QMessageBox.Information)
            msg.setText("未能自动找到游戏路径，请手动选择游戏可执行文件 (SB.exe)")
            msg.setWindowTitle("选择游戏路径")
            msg.exec()
            
            # 打开文件选择对话框
            game_path, _ = QFileDialog.getOpenFileName(
                None, 
                "选择游戏可执行文件", 
                "", 
                "可执行文件 (*.exe)"
            )
            
            if game_path:
                config.set_game_path(game_path)
                print(f"[调试] 手动设置游戏路径: {game_path}")
                
                # 尝试设置MOD路径
                game_dir = os.path.dirname(game_path)
                mods_path = os.path.join(game_dir, "SB", "Content", "Paks", "~mods")
                mods_path_dir = Path(mods_path)
                if not mods_path_dir.exists():
                    try:
                        mods_path_dir.mkdir(parents=True, exist_ok=True)
                        print(f"[调试] 创建MOD目录: {mods_path}")
                    except Exception as e:
                        print(f"[错误] 创建MOD目录失败: {str(e)}")
                        # 尝试请求用户手动选择MOD目录
                        mods_path = QFileDialog.getExistingDirectory(
                            None, 
                            "选择MOD文件夹"
                        )
                
                config.set_mods_path(mods_path)
                print(f"[调试] 设置MOD文件夹: {mods_path}")
                
                # 设置备份路径 - 先使用默认路径
                default_backup_path = os.path.join(os.getcwd(), "modbackup")
                backup_dir = Path(default_backup_path)
                if not backup_dir.exists():
                    backup_dir.mkdir(parents=True, exist_ok=True)
                
                # 提示用户选择备份目录或使用默认目录
                msg = QMessageBox()
                msg.setIcon(QMessageBox.Question)
                msg.setText("是否使用默认备份目录？")
                msg.setInformativeText(f"默认备份目录: {default_backup_path}\n\n选择\"是\"使用默认备份目录\n选择\"否\"手动选择备份目录")
                msg.setWindowTitle("备份目录设置")
                msg.setStandardButtons(QMessageBox.Yes | QMessageBox.No)
                msg.button(QMessageBox.Yes).setText('是')
                msg.button(QMessageBox.No).setText('否')
                
                reply = msg.exec()
                
                if reply == QMessageBox.No:
                    # 用户选择手动设置备份目录
                    backup_path = QFileDialog.getExistingDirectory(
                        None,
                        "选择MOD备份文件夹",
                        os.getcwd()
                    )
                    
                    if not backup_path:
                        # 如果用户取消选择，使用默认路径并提示
                        backup_path = default_backup_path
                        QMessageBox.information(None, "提示", f"未选择备份目录，将使用默认备份目录:\n{default_backup_path}")
                else:
                    # 用户选择使用默认备份目录
                    backup_path = default_backup_path
                    QMessageBox.information(None, "提示", f"将使用默认备份目录:\n{default_backup_path}")
                
                config.set_backup_path(backup_path)
                print(f"[调试] 设置备份路径: {backup_path}")
                
                # 标记为已初始化
                config.set_initialized(True)
                return True
            else:
                print("[错误] 用户未选择游戏路径")
                QMessageBox.critical(None, "错误", "未选择游戏路径，程序将退出。")
                return False
    except Exception as e:
        print(f"[错误] 初始化配置失败: {str(e)}")
        traceback.print_exc()
        QMessageBox.critical(None, "错误", f"初始化配置失败:\n{str(e)}")
        return False

def main():
    try:
        # 设置日志
        setup_logging()
        
        print("[调试] 启动程序...")
        
        # 显示当前工作目录和可执行文件路径
        print(f"[调试] 当前工作目录: {os.getcwd()}")
        if getattr(sys, 'frozen', False):
            print(f"[调试] 可执行文件路径: {sys.executable}")
            print(f"[调试] 可执行文件目录: {os.path.dirname(sys.executable)}")
        
        # 设置路径
        setup_paths()
        
        # 初始化应用
        app = setup_qt_application()
        
        # 为程序异常添加全局处理
        def exception_hook(exctype, value, tb):
            # 确保所有临时文件被清理
            cleanup_temp_directories()
            
            # 记录异常到日志文件
            error_msg = ''.join(traceback.format_exception(exctype, value, tb))
            logging.error(f"未捕获的异常: {error_msg}")
            
            # 将详细错误写入单独的错误日志文件
            timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
            error_log_path = Path(f"logs/error_{timestamp}.log")
            
            try:
                with open(error_log_path, "w", encoding="utf-8") as f:
                    f.write(f"时间: {datetime.now()}\n")
                    f.write(f"错误类型: {exctype.__name__}\n")
                    f.write(f"错误消息: {value}\n\n")
                    f.write("详细错误信息:\n")
                    f.write(error_msg)
                    f.write("\n\n系统信息:\n")
                    f.write(f"Python版本: {sys.version}\n")
                    f.write(f"操作系统: {os.name} {sys.platform}\n")
                    if hasattr(os, 'uname'):
                        f.write(f"详细系统信息: {os.uname()}\n")
            except Exception as e:
                logging.error(f"写入错误日志文件失败: {e}")
            
            # 显示错误对话框
            if QApplication.instance():
                msg = QMessageBox()
                msg.setIcon(QMessageBox.Critical)
                msg.setText("程序发生错误，需要退出")
                msg.setInformativeText(f"错误类型: {exctype.__name__}\n错误消息: {str(value)}")
                msg.setDetailedText(error_msg)
                msg.setWindowTitle("程序错误")
                msg.setStandardButtons(QMessageBox.Ok)
                
                # 添加保存错误日志的按钮
                save_btn = msg.addButton("保存错误日志", QMessageBox.ActionRole)
                
                msg.exec()
                
                if msg.clickedButton() == save_btn:
                    save_path, _ = QFileDialog.getSaveFileName(
                        None,
                        "保存错误日志",
                        str(error_log_path),
                        "日志文件 (*.log)"
                    )
                    if save_path:
                        try:
                            shutil.copy2(error_log_path, save_path)
                            QMessageBox.information(None, "保存成功", f"错误日志已保存至：\n{save_path}")
                        except Exception as e:
                            QMessageBox.warning(None, "保存失败", f"保存错误日志失败：\n{e}")
        
        # 设置全局异常处理钩子
        sys.excepthook = exception_hook
        
        # 初始化配置管理器
        print("[调试] 初始化配置管理器")
        config = ConfigManager()
        
        # 确保备份目录存在
        backup_path = config.get_backup_path()
        if backup_path:
            backup_dir = Path(backup_path)
            if not backup_dir.exists():
                print(f"[调试] 创建备份目录: {backup_dir}")
                backup_dir.mkdir(parents=True, exist_ok=True)
        
        # 清理无效的MOD记录
        print("[调试] 清理无效的MOD记录")
        cleaned_count = config.clean_invalid_mods()
        if cleaned_count > 0:
            print(f"[调试] 已清理 {cleaned_count} 条无效的MOD记录")
        
        print("[调试] 检查是否首次运行")
        if not config.is_initialized():
            # 如果配置未初始化，进行初始化
            if not initialize_config(config):
                print("[错误] 初始化配置失败，程序退出")
                return 1
        
        # 创建主窗口
        print("[调试] 创建主窗口")
        main_window = MainWindow(config)
        main_window.show()
        
        print("[调试] 进入主事件循环")
        exit_code = app.exec()
        
        # 在程序退出前清理所有临时文件
        cleanup_temp_directories()
        
        return exit_code
    except Exception as e:
        app = QApplication(sys.argv) if 'app' not in locals() else app
        print("应用启动失败:", str(e))
        traceback.print_exc()
        QMessageBox.critical(None, "错误", f"应用程序启动失败:\n{str(e)}")
        
        # 确保临时文件被清理
        cleanup_temp_directories()
        return 1

if __name__ == "__main__":
    from datetime import datetime
    import shutil
    
    try:
        sys.exit(main())
    finally:
        # 最后的清理操作，确保所有临时文件都被删除
        cleanup_temp_directories() 