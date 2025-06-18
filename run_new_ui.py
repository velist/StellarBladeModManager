import sys
from PySide6.QtWidgets import QApplication
from ui.new_main_window import NewMainWindow

if __name__ == '__main__':
    app = QApplication(sys.argv)
    window = NewMainWindow()
    window.show()
    sys.exit(app.exec()) 