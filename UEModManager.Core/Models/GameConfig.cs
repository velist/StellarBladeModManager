using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace UEModManager.Core.Models
{
    /// <summary>
    /// 游戏配置模型
    /// </summary>
    public class GameConfig : INotifyPropertyChanged
    {
        private string _id = string.Empty;
        private string _name = string.Empty;
        private string _displayName = string.Empty;
        private string _gamePath = string.Empty;
        private string _modPath = string.Empty;
        private string _backupPath = string.Empty;
        private string _executableName = string.Empty;
        private string _gameIcon = string.Empty;
        private string _gameEmoji = "🎮";
        private bool _isActive = false;
        private bool _autoScanOnGameSwitch = true;
        private bool _autoBackupMods = true;
        private bool _scanSubfolders = true;
        private DateTime _lastUsed = DateTime.Now;
        private List<string> _supportedModTypes = new();
        private string _platform = string.Empty;

        /// <summary>
        /// 游戏唯一标识符
        /// </summary>
        public string Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        /// <summary>
        /// 游戏名称（英文原名）
        /// </summary>
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        /// <summary>
        /// 游戏显示名称（中文优先）
        /// </summary>
        public string DisplayName
        {
            get => string.IsNullOrEmpty(_displayName) ? _name : _displayName;
            set => SetProperty(ref _displayName, value);
        }

        /// <summary>
        /// 游戏图标路径
        /// </summary>
        public string GameIcon
        {
            get => _gameIcon;
            set => SetProperty(ref _gameIcon, value);
        }

        /// <summary>
        /// 游戏表情符号图标
        /// </summary>
        public string GameEmoji
        {
            get => _gameEmoji;
            set => SetProperty(ref _gameEmoji, value);
        }

        /// <summary>
        /// 游戏平台（Steam, Epic Games, GOG等）
        /// </summary>
        public string Platform
        {
            get => _platform;
            set => SetProperty(ref _platform, value);
        }

        /// <summary>
        /// 是否有自定义图标
        /// </summary>
        public bool HasCustomIcon => !string.IsNullOrEmpty(_gameIcon) && System.IO.File.Exists(_gameIcon);

        /// <summary>
        /// 游戏安装路径
        /// </summary>
        public string GamePath
        {
            get => _gamePath;
            set => SetProperty(ref _gamePath, value);
        }

        /// <summary>
        /// MOD安装路径
        /// </summary>
        public string ModPath
        {
            get => _modPath;
            set => SetProperty(ref _modPath, value);
        }

        /// <summary>
        /// MOD备份路径
        /// </summary>
        public string BackupPath
        {
            get => _backupPath;
            set => SetProperty(ref _backupPath, value);
        }

        /// <summary>
        /// 游戏可执行文件名
        /// </summary>
        public string ExecutableName
        {
            get => _executableName;
            set => SetProperty(ref _executableName, value);
        }

        /// <summary>
        /// 是否为当前活动游戏
        /// </summary>
        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        /// <summary>
        /// 切换游戏时自动扫描MOD
        /// </summary>
        public bool AutoScanOnGameSwitch
        {
            get => _autoScanOnGameSwitch;
            set => SetProperty(ref _autoScanOnGameSwitch, value);
        }

        /// <summary>
        /// 自动备份导入的MOD
        /// </summary>
        public bool AutoBackupMods
        {
            get => _autoBackupMods;
            set => SetProperty(ref _autoBackupMods, value);
        }

        /// <summary>
        /// 允许扫描子文件夹
        /// </summary>
        public bool ScanSubfolders
        {
            get => _scanSubfolders;
            set => SetProperty(ref _scanSubfolders, value);
        }

        /// <summary>
        /// 最后使用时间
        /// </summary>
        public DateTime LastUsed
        {
            get => _lastUsed;
            set => SetProperty(ref _lastUsed, value);
        }

        /// <summary>
        /// 支持的MOD类型
        /// </summary>
        public List<string> SupportedModTypes
        {
            get => _supportedModTypes;
            set => SetProperty(ref _supportedModTypes, value);
        }

        /// <summary>
        /// 游戏完整路径（包含可执行文件）
        /// </summary>
        public string FullExecutablePath => System.IO.Path.Combine(_gamePath, _executableName);

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
} 