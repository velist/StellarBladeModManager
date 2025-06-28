using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace UEModManager.Core.Models
{
    /// <summary>
    /// MOD信息模型
    /// </summary>
    public class ModInfo : INotifyPropertyChanged
    {
        private string _id = string.Empty;
        private string _name = string.Empty;
        private string _displayName = string.Empty;
        private string _description = string.Empty;
        private string _version = string.Empty;
        private string _author = string.Empty;
        private bool _isEnabled = false;
        private string _installPath = string.Empty;
        private string _previewImagePath = string.Empty;
        private List<string> _categories = new();
        private DateTime _installDate = DateTime.Now;
        private DateTime _lastModified = DateTime.MinValue;
        private long _fileSize = 0;
        private List<string> _tags = new();

        /// <summary>
        /// 唯一标识符
        /// </summary>
        public string Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        /// <summary>
        /// MOD原始名称
        /// </summary>
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        /// <summary>
        /// 自定义显示名称
        /// </summary>
        public string DisplayName
        {
            get => string.IsNullOrEmpty(_displayName) ? _name : _displayName;
            set => SetProperty(ref _displayName, value);
        }

        /// <summary>
        /// 描述信息
        /// </summary>
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        /// <summary>
        /// 版本号
        /// </summary>
        public string Version
        {
            get => _version;
            set => SetProperty(ref _version, value);
        }

        /// <summary>
        /// 作者
        /// </summary>
        public string Author
        {
            get => _author;
            set => SetProperty(ref _author, value);
        }

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }

        /// <summary>
        /// 安装路径
        /// </summary>
        public string InstallPath
        {
            get => _installPath;
            set => SetProperty(ref _installPath, value);
        }

        /// <summary>
        /// 预览图片路径
        /// </summary>
        public string PreviewImagePath
        {
            get => _previewImagePath;
            set => SetProperty(ref _previewImagePath, value);
        }

        /// <summary>
        /// 分类路径（多级）
        /// </summary>
        public List<string> Categories
        {
            get => _categories;
            set => SetProperty(ref _categories, value);
        }

        /// <summary>
        /// 安装日期
        /// </summary>
        public DateTime InstallDate
        {
            get => _installDate;
            set => SetProperty(ref _installDate, value);
        }

        /// <summary>
        /// 最后修改时间
        /// </summary>
        public DateTime LastModified
        {
            get => _lastModified;
            set => SetProperty(ref _lastModified, value);
        }

        /// <summary>
        /// 文件大小（字节）
        /// </summary>
        public long FileSize
        {
            get => _fileSize;
            set => SetProperty(ref _fileSize, value);
        }

        /// <summary>
        /// 标签
        /// </summary>
        public List<string> Tags
        {
            get => _tags;
            set => SetProperty(ref _tags, value);
        }

        /// <summary>
        /// 格式化的文件大小
        /// </summary>
        public string FormattedFileSize
        {
            get
            {
                if (_fileSize < 1024) return $"{_fileSize} B";
                if (_fileSize < 1024 * 1024) return $"{_fileSize / 1024.0:F1} KB";
                if (_fileSize < 1024 * 1024 * 1024) return $"{_fileSize / (1024.0 * 1024):F1} MB";
                return $"{_fileSize / (1024.0 * 1024 * 1024):F1} GB";
            }
        }

        /// <summary>
        /// 状态文本
        /// </summary>
        public string StatusText => _isEnabled ? "已启用" : "已禁用";

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