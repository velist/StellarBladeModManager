import os
import sys
from PySide6.QtWidgets import (
    QMainWindow, QWidget, QVBoxLayout, QHBoxLayout,
    QTreeWidget, QTreeWidgetItem, QLabel, QPushButton,
    QFrame, QSplitter, QListWidget, QListWidgetItem, QLineEdit
)
from PySide6.QtCore import Qt, QSize
from PySide6.QtGui import QIcon, QPixmap

def resource_path(relative_path):
    """ Get absolute path to resource, works for dev and for PyInstaller """
    try:
        # PyInstaller creates a temp folder and stores path in _MEIPASS
        base_path = sys._MEIPASS
    except Exception:
        base_path = os.path.abspath(".")

    return os.path.join(base_path, relative_path)

class ModListItemWidget(QWidget):
    """
    Custom widget for items in the MOD list.
    Displays MOD name and its status (enabled/disabled) with colors.
    """
    def __init__(self, mod_name, is_enabled, parent=None):
        super().__init__(parent)
        self.setObjectName("modItemWidget")

        layout = QHBoxLayout(self)
        layout.setContentsMargins(10, 8, 10, 8)
        layout.setSpacing(10)

        # MOD Name
        name_label = QLabel(mod_name)
        name_label.setObjectName("modNameLabel")
        
        # Status Label
        status_label = QLabel("已启用" if is_enabled else "已禁用")
        status_label.setObjectName(f"statusLabel_{'enabled' if is_enabled else 'disabled'}")
        
        layout.addWidget(name_label)
        layout.addStretch()
        layout.addWidget(status_label)

        self.setLayout(layout)

class NewMainWindow(QMainWindow):
    def __init__(self):
        super().__init__()
        self.setWindowTitle("爱酱剑星MOD管理器 (新UI演示)")
        self.setMinimumSize(1280, 800)
        self.load_style()
        self.init_ui()
        self.populate_dummy_data()

    def load_style(self):
        """Loads the stylesheet for the new UI."""
        style_file = os.path.join(os.path.dirname(__file__), 'new_style.qss')
        try:
            with open(style_file, 'r', encoding='utf-8') as f:
                self.setStyleSheet(f.read())
        except Exception as e:
            print(f"[ERROR] Failed to load stylesheet: {e}")

    def init_ui(self):
        """Initializes the user interface."""
        central_widget = QWidget()
        self.setCentralWidget(central_widget)
        
        main_layout = QVBoxLayout(central_widget)
        main_layout.setContentsMargins(0, 0, 0, 0)
        main_layout.setSpacing(0)

        # Top Area (A)
        top_area = self.create_top_area()
        
        # Main Content Area (B + C)
        content_area = QSplitter(Qt.Horizontal)
        content_area.setHandleWidth(1)

        # Left Frame (B - Categories)
        left_frame = self.create_left_frame()

        # Right Frame (C - MODs)
        right_frame = self.create_right_frame()

        # Add widgets to splitter
        content_area.addWidget(left_frame)
        content_area.addWidget(right_frame)
        content_area.setStretchFactor(0, 1)
        content_area.setStretchFactor(1, 3) # C area is larger

        # Add to main layout
        main_layout.addWidget(top_area)
        main_layout.addWidget(content_area)

    def create_top_area(self):
        """Creates the top bar with title, buttons, and search."""
        top_area = QFrame()
        top_area.setObjectName("topArea")
        top_area.setFixedHeight(80)
        top_layout = QHBoxLayout(top_area)
        top_layout.setContentsMargins(20, 0, 20, 0)

        # Title
        title_label = QLabel("爱酱剑星MOD管理器")
        title_label.setStyleSheet("font-size: 18pt; font-weight: bold; color: #cba6f7;")

        # Launch Button
        self.launch_game_btn = QPushButton("启动游戏")
        self.launch_game_btn.setObjectName("launchGameBtn")
        self.launch_game_btn.setFixedSize(160, 44)

        # Search Box
        self.search_box = QLineEdit()
        self.search_box.setPlaceholderText("搜索MOD...")
        self.search_box.setFixedWidth(250)
        
        # Settings Button
        settings_btn = QPushButton("设置")
        settings_btn.setFixedSize(80, 36)

        top_layout.addWidget(title_label)
        top_layout.addStretch()
        top_layout.addWidget(self.launch_game_btn)
        top_layout.addSpacing(20)
        top_layout.addWidget(self.search_box)
        top_layout.addWidget(settings_btn)

        return top_area

    def create_left_frame(self):
        """Creates the left panel for categories."""
        left_frame = QFrame()
        left_frame.setObjectName("leftFrame")
        left_layout = QVBoxLayout(left_frame)
        left_layout.setContentsMargins(10, 20, 10, 10)
        
        category_title = QLabel("MOD 分类")
        category_title.setStyleSheet("font-size: 14pt; font-weight: bold; margin-left: 5px;")
        
        self.tree = QTreeWidget()
        self.tree.setHeaderHidden(True)
        self.tree.setIndentation(15)

        left_layout.addWidget(category_title)
        left_layout.addWidget(self.tree)
        return left_frame

    def create_right_frame(self):
        """Creates the right panel for the MOD list and details."""
        right_frame = QFrame()
        right_frame.setObjectName("rightFrame")
        right_layout = QVBoxLayout(right_frame)
        right_layout.setContentsMargins(10, 20, 10, 10)

        self.mod_list = QListWidget()
        self.mod_list.setVerticalScrollBarPolicy(Qt.ScrollBarAsNeeded)
        self.mod_list.setHorizontalScrollBarPolicy(Qt.ScrollBarAlwaysOff)
        # Disable selection highlight as we handle hover in QSS
        self.mod_list.setStyleSheet("QListWidget::item:selected { background-color: transparent; }")


        right_layout.addWidget(self.mod_list)
        return right_frame

    def populate_dummy_data(self):
        """Fills the UI with sample data for demonstration."""
        # --- Populate Categories ---
        self.tree.clear()
        categories = {
            "角色外观": ["清凉", "战斗服", "日常"],
            "武器外观": [],
            "UI & HUD": ["图标", "字体"],
            "默认分类": []
        }
        for cat_name, sub_cats in categories.items():
            cat_item = QTreeWidgetItem([cat_name])
            self.tree.addTopLevelItem(cat_item)
            for sub_cat_name in sub_cats:
                sub_item = QTreeWidgetItem([sub_cat_name])
                cat_item.addChild(sub_item)
        
        self.tree.expandAll()

        # --- Populate MOD List ---
        self.mod_list.clear()
        mods = [
            ("清凉夏日比基尼", True),
            ("赛博朋克风战斗夹克", True),
            ("优雅晚礼服", False),
            ("黄金沙鹰", True),
            ("黑曜石太刀", False),
            ("高清重制UI图标", True),
            ("极简血条", False),
            ("可爱的猫耳装饰", True)
        ]

        for name, is_enabled in mods:
            item = QListWidgetItem(self.mod_list)
            item_widget = ModListItemWidget(name, is_enabled)
            
            # Set size hint for the item to fit the custom widget
            item.setSizeHint(item_widget.sizeHint())
            
            self.mod_list.addItem(item)
            self.mod_list.setItemWidget(item, item_widget) 