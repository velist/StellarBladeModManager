using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace UEModManager.Core.Models
{
    /// <summary>
    /// 分类项模型
    /// </summary>
    public class CategoryItem : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _displayName = string.Empty;
        private int _modCount = 0;
        private bool _isExpanded = true;
        private bool _isSelected = false;
        private CategoryItem? _parent;

        /// <summary>
        /// 分类名称（用于存储）
        /// </summary>
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        /// <summary>
        /// 显示名称
        /// </summary>
        public string DisplayName
        {
            get => string.IsNullOrEmpty(_displayName) ? _name : _displayName;
            set => SetProperty(ref _displayName, value);
        }

        /// <summary>
        /// MOD数量
        /// </summary>
        public int ModCount
        {
            get => _modCount;
            set => SetProperty(ref _modCount, value);
        }

        /// <summary>
        /// 是否展开
        /// </summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        /// <summary>
        /// 是否选中
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        /// <summary>
        /// 父分类
        /// </summary>
        public CategoryItem? Parent
        {
            get => _parent;
            set => SetProperty(ref _parent, value);
        }

        /// <summary>
        /// 子分类集合
        /// </summary>
        public ObservableCollection<CategoryItem> Children { get; } = new();

        /// <summary>
        /// 完整路径
        /// </summary>
        public string FullPath
        {
            get
            {
                if (_parent == null) return _name;
                return $"{_parent.FullPath}/{_name}";
            }
        }

        /// <summary>
        /// 层级深度
        /// </summary>
        public int Level
        {
            get
            {
                int level = 0;
                var current = _parent;
                while (current != null)
                {
                    level++;
                    current = current.Parent;
                }
                return level;
            }
        }

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

        /// <summary>
        /// 添加子分类
        /// </summary>
        public void AddChild(CategoryItem child)
        {
            child.Parent = this;
            Children.Add(child);
        }

        /// <summary>
        /// 移除子分类
        /// </summary>
        public void RemoveChild(CategoryItem child)
        {
            child.Parent = null;
            Children.Remove(child);
        }
    }
} 