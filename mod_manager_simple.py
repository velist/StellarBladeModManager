import os
import logging
from pathlib import Path

def cleanup_temp_directories():
    pass

class ModManager:
    def __init__(self, config_manager):
        self.config = config_manager
        self.mods_path = Path(config_manager.get_mods_path() or "")
        self.backup_path = Path(config_manager.get_backup_path() or "")
    
    def import_mod(self, file_path):
        return {"name": "测试MOD", "enabled": True}
    
    def scan_mods_directory(self):
        return []
    
    def backup_mod(self, mod_name, mod_info=None):
        return True
    
    def enable_mod(self, mod_id):
        return True
    
    def disable_mod(self, mod_id):
        return True
    
    def set_preview_image(self, mod_id, image_path):
        return True 