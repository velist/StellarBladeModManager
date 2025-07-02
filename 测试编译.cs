// 编译测试文件 - 修改版本
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using UEModManager.Core.Models;
using UEModManager.Resources;
using UEModManager.ViewModels;
using UEModManager.Views;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Path = System.IO.Path;
using IOPath = System.IO.Path;
using DispatcherTimer = System.Windows.Threading.DispatcherTimer;
using System.Threading;
using System.Windows.Documents;
using System.Net.Http;
using System.Text.Json;
using System.Diagnostics.CodeAnalysis;

namespace UEModManager
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer? statsTimer;
        private Mod? selectedMod;
        private List<Mod> allMods = new List<Mod>();
        private ObservableCollection<Category> categories = new ObservableCollection<Category>();
        private Mod? _lastSelectedMod; // 用于Shift多选
        private string currentGamePath = "";
        private string currentModPath = "";
        private string currentBackupPath = "";
        private string currentGameName = "";
        private string currentExecutableName = "";  // 添加执行程序名称字段
        private string configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
        private readonly List<string> modTags = new List<string> { "面部", "人物", "武器", "修改", "其他" };
        
        private bool isEnglishMode = false;
        
        private UEModManager.Core.Services.CategoryService? _categoryService;
        private UEModManager.Core.Services.ModService? _modService;
        private ILogger<MainWindow>? _logger;
        
        private bool _isDragging = false;
        private object? _draggedCategory = null;
        private Point _startPoint;
        private string? _modStatusFilter;

        public MainWindow()
        {
            try
            {
                InitializeComponent();
                InitializeData();
                SetupEventHandlers();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 初始化配置
            LoadConfiguration();
            
            // 初始化服务
            InitializeServices();
            
            // 检查和恢复游戏配置
            CheckAndRestoreGameConfiguration();
        }

        // 添加视图切换事件处理程序
        private void CardViewBtn_Click(object sender, RoutedEventArgs e)
        {
            ModsCardViewScroller.Visibility = Visibility.Visible;
            ModsListView.Visibility = Visibility.Collapsed;
            
            // 更新按钮样式
            CardViewBtn.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00D4AA"));
            CardViewBtn.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00D4AA"));
            CardViewBtn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A3332"));
            
            ListViewBtn.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A5568"));
            ListViewBtn.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F5F9"));
            ListViewBtn.Background = Brushes.Transparent;
        }
        
        private void ListViewBtn_Click(object sender, RoutedEventArgs e)
        {
            ModsCardViewScroller.Visibility = Visibility.Collapsed;
            ModsListView.Visibility = Visibility.Visible;
            
            // 更新按钮样式
            ListViewBtn.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00D4AA"));
            ListViewBtn.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00D4AA"));
            ListViewBtn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A3332"));
            
            CardViewBtn.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A5568"));
            CardViewBtn.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F5F9"));
            CardViewBtn.Background = Brushes.Transparent;
        }
        
        // 预览图区域点击事件
        private void ModDetailPreviewImage_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (selectedMod != null)
            {
                ChangePreviewForMod(selectedMod);
            }
        }
        
        // 让B1区分类列表支持鼠标滚轮滚动
        private void CategoryList_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // 查找外层ScrollViewer
            var scrollViewer = FindParent<ScrollViewer>(CategoryList);
            var mainContentScrollViewer = MainContentScrollViewer;
            
            if (scrollViewer != null)
            {
                if (e.Delta != 0)
                {
                    scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
                    
                    // 允许滚轮事件传递到主内容区
                    if (mainContentScrollViewer != null && 
                        (scrollViewer.VerticalOffset == 0 && e.Delta > 0 || 
                         scrollViewer.VerticalOffset >= scrollViewer.ScrollableHeight && e.Delta < 0))
                    {
                        mainContentScrollViewer.ScrollToVerticalOffset(mainContentScrollViewer.VerticalOffset - e.Delta);
                    }
                    
                    e.Handled = true;
                }
            }
        }
        
        private T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            
            if (parentObject is T parent)
                return parent;
            else
                return FindParent<T>(parentObject);
        }
    }
    
    public class Game
    {
        public string Name { get; set; } = "";
        public bool IsActive { get; set; }
    }
    
    public class Category : INotifyPropertyChanged
    {
        private string _name = "";
        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }
        
        private int _count;
        public int Count
        {
            get => _count;
            set
            {
                if (_count != value)
                {
                    _count = value;
                    OnPropertyChanged(nameof(Count));
                }
            }
        }
        
        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    
    public class Mod : INotifyPropertyChanged
    {
        public string Name { get; set; } = "";
        public string RealName { get; set; } = ""; // MOD的真实名称，用于文件操作
        public string Description { get; set; } = "";
        public string Type { get; set; } = "";
        
        private string _status = "";
        public string Status 
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged(nameof(Status));
                }
            }
        }
        
        private List<string> _categories = new List<string> { "未分类" };
        public List<string> Categories
        {
            get => _categories;
            set
            {
                if (_categories != value)
                {
                    _categories = value;
                    OnPropertyChanged(nameof(Categories));
                }
            }
        }
        
        private bool _isSelected = false;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }
        
        public string Icon { get; set; } = "";
        public string Size { get; set; } = "";
        public string ImportDate { get; set; } = "";
        public int UsageCount { get; set; }
        public double Rating { get; set; }
        
        
        private string _previewImagePath = "";
        public string PreviewImagePath 
        {
            get => _previewImagePath;
            set
            {
                if (_previewImagePath != value)
                {
                    _previewImagePath = value;
                    OnPropertyChanged(nameof(PreviewImagePath));
                }
            }
        }
        
        private ImageSource? _previewImageSource;
        [System.Text.Json.Serialization.JsonIgnore]
        public ImageSource? PreviewImageSource 
        {
            get => _previewImageSource;
            set
            {
                if (_previewImageSource != value)
                {
                    _previewImageSource = value;
                    OnPropertyChanged(nameof(PreviewImageSource));
                }
            }
        }
        
        internal void SetPreviewImageSourceDirect(ImageSource? imageSource)
        {
            _previewImageSource = imageSource;
        }
        
        public event PropertyChangedEventHandler? PropertyChanged;
        
        public virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    
    public class AppConfig
    {
        public string? GameName { get; set; }
        public string? GamePath { get; set; }
        public string? ModPath { get; set; }
        public string? BackupPath { get; set; }
        public string? ExecutableName { get; set; }
    }
    
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null ? Visibility.Visible : Visibility.Collapsed;
        }
        
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    
    public class InverseNullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null ? Visibility.Collapsed : Visibility.Visible;
        }
        
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    
    public class CategoryTypeVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Category ? Visibility.Visible : Visibility.Collapsed;
        }
        
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    
    public class CategoryItemTypeVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is CategoryItem ? Visibility.Visible : Visibility.Collapsed;
        }
        
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 