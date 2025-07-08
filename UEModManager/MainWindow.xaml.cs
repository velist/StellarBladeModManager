using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Controls.Primitives;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Windows.Threading;
using Microsoft.Win32;
using System.Text.Json;
using System.Diagnostics;
using SharpCompress.Archives;
using SharpCompress.Common;
using UEModManager.ViewModels;
using System.Collections;
using System.Threading;
using System.Globalization;
using System.Threading.Tasks;
using UEModManager.Core.Services;
using UEModManager.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices; // 添加引用
using System.Windows.Interop; // 添加引用

// 解决Path命名冲突
using IOPath = System.IO.Path;

namespace UEModManager
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // 添加Win32 API结构和常量
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        private const int WM_GETMINMAXINFO = 0x0024;

        private DispatcherTimer? statsTimer;
        private Mod? selectedMod;
        private List<Mod> allMods = new List<Mod>();
        private string XunleiImageName = "迅雷云盘.png";
        private string BaiduImageName = "百度网盘.png";
        // 游戏类型枚举
        private enum GameType
        {
            Other,
            StellarBlade,
            Enshrouded,
            BlackMythWukong
        }
        private GameType currentGameType = GameType.Other;
        private ObservableCollection<Category> categories = new ObservableCollection<Category>();
        private Mod? _lastSelectedMod; // 用于Shift多选
        private Point _startPoint;
        private string currentGamePath = "";
        private string currentModPath = "";
        private string currentBackupPath = "";
        private string currentGameName = "";
        private string currentExecutableName = "";  // 添加执行程序名称字段
        private string configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
        private readonly List<string> modTags = new List<string> { "面部", "人物", "武器", "修改", "其他" };
        
        // 语言支持字段
        private bool isEnglishMode = false;
        // 添加CategoryService支持
        private CategoryService? _categoryService;
        private ModService? _modService;
        private ILogger<MainWindow>? _logger;

        // 搜索防抖计时器
        private DispatcherTimer? searchDebounceTimer;

        // 分类拖拽相关字段
        private bool _isDragging = false;

        // 添加全局Popup跟踪变量

       

        // 主构造函数
        public MainWindow()
        {
            AllocConsole(); // 启用控制台日志输出
            RedirectConsoleOutput(); // 重定向控制台输出到文件
            InitializeComponent();
            
            // 确保拖拽功能正确设置
            AllowDrop = true;
            DragEnter += MainWindow_DragEnter;
            DragOver += MainWindow_DragOver;
            Drop += MainWindow_Drop;

            // 立即加载配置，不延迟
            LoadConfiguration();
            InitializeData();
            SetupEventHandlers();
            
            // 改为窗口加载完成后立即同步检查配置
            this.Loaded += MainWindow_Loaded;
            
            // 添加关闭事件处理，保存分类数据
            this.Closing += MainWindow_Closing;
            
            StartStatsTimer();

            // 分类列表初始化绑定
            CategoryList.ItemsSource = categories;
            CategoryList.Drop += CategoryList_Drop;
            CategoryList.DragOver += CategoryList_DragOver;
            
            CategoryList.Drop += CategoryList_Drop;
            CategoryList.DragOver += CategoryList_DragOver;
            
            Console.WriteLine("MainWindow 初始化完成");
        }

        // Win32 API 用于分配控制台窗口和重定向输出
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool AllocConsole();
        
        // 重定向控制台输出到文件
        private void RedirectConsoleOutput()
        {
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "console.log");
                FileStream fileStream = new FileStream(logPath, FileMode.Create, FileAccess.Write);
                StreamWriter writer = new StreamWriter(fileStream) { AutoFlush = true };
                Console.SetOut(writer);
                Console.WriteLine($"[{DateTime.Now}] 控制台日志开始记录");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"重定向控制台输出失败: {ex.Message}");
            }
        }

        // 添加窗口初始化方法，修复最大化时标题栏消失问题
        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            // 添加钩子处理窗口消息
            IntPtr handle = new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(handle)?.AddHook(WindowProc);
            
            Console.WriteLine("[DEBUG] 已安装窗口处理钩子，将修复最大化时标题栏消失问题");
        }

        private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // 处理窗口最大化时的位置和大小
            if (msg == WM_GETMINMAXINFO)
            {
                try
                {
                    // 获取当前屏幕信息（考虑多显示器）
                    MINMAXINFO mmi = (MINMAXINFO)Marshal.PtrToStructure(lParam, typeof(MINMAXINFO));
                    
                    // 获取当前显示器工作区
                    System.Windows.Forms.Screen screen = System.Windows.Forms.Screen.FromHandle(hwnd);
                    System.Drawing.Rectangle workingArea = screen.WorkingArea;
                    System.Drawing.Rectangle screenBounds = screen.Bounds;
                    
                    // 设置最大化时不覆盖任务栏
                    mmi.ptMaxPosition.x = Math.Abs(workingArea.Left - screenBounds.Left);
                    mmi.ptMaxPosition.y = Math.Abs(workingArea.Top - screenBounds.Top);
                    mmi.ptMaxSize.x = workingArea.Width;
                    mmi.ptMaxSize.y = workingArea.Height;
                    
                    Marshal.StructureToPtr(mmi, lParam, true);
                    Console.WriteLine("[DEBUG] 已调整最大化窗口位置，修复标题栏显示");
                    
                    handled = true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] 处理窗口最大化时出错: {ex.Message}");
                }
            }
            
            return IntPtr.Zero;
        }

        // === 配置管理 ===
        private void LoadConfiguration()
        {
            try
            {
                if (File.Exists(configFilePath))
                {
                    var json = File.ReadAllText(configFilePath);
                    var config = JsonSerializer.Deserialize<AppConfig>(json);
                    if (config != null)
                    {
                        currentGameName = config.GameName ?? "";
                        currentGamePath = config.GamePath ?? "";
                        currentModPath = config.ModPath ?? "";
                        currentBackupPath = config.BackupPath ?? "";
                        currentExecutableName = config.ExecutableName ?? "";  // 加载执行程序名称
                        
                        // 修复备份路径：如果指向错误的.NET版本目录，自动修正
                        if (!string.IsNullOrEmpty(currentBackupPath) && currentBackupPath.Contains("net6.0-windows"))
                        {
                            Console.WriteLine($"[DEBUG] 检测到旧版本备份路径: {currentBackupPath}");
                            currentBackupPath = currentBackupPath.Replace("net6.0-windows", "net8.0-windows");
                            Console.WriteLine($"[DEBUG] 自动修正为新备份路径: {currentBackupPath}");
                            
                            // 保存修正后的配置
                            var updatedConfig = new AppConfig 
                            { 
                                GameName = currentGameName,
                                GamePath = currentGamePath,
                                ModPath = currentModPath,
                                BackupPath = currentBackupPath,
                                ExecutableName = currentExecutableName,
                                CustomGames = config.CustomGames ?? new List<string>()
                            };
                            var updatedJson = JsonSerializer.Serialize(updatedConfig, new JsonSerializerOptions { WriteIndented = true });
                            File.WriteAllText(configFilePath, updatedJson);
                            Console.WriteLine("[DEBUG] 配置文件已更新为正确的备份路径");
                        }
                        
                        // 加载自定义游戏列表
                        if (config.CustomGames != null && config.CustomGames.Count > 0)
                        {
                            Console.WriteLine($"[DEBUG] 从配置文件中加载 {config.CustomGames.Count} 个自定义游戏");
                            
                            // 将自定义游戏添加到下拉列表，插入位置在最后一项（添加新游戏）之前
                            int insertPosition = GameList.Items.Count - 1;
                            foreach (var gameName in config.CustomGames)
                            {
                                if (!string.IsNullOrEmpty(gameName))
                                {
                                    GameList.Items.Insert(insertPosition, new ComboBoxItem { Content = gameName });
                                    insertPosition++;
                                    Console.WriteLine($"[DEBUG] 已添加自定义游戏: {gameName}");
                                }
                            }
                        }
                        
                        Console.WriteLine($"配置加载成功: 游戏={currentGameName}, 路径={currentGamePath}");
                        Console.WriteLine($"MOD路径={currentModPath}, 备份路径={currentBackupPath}");
                        Console.WriteLine($"执行程序={currentExecutableName}");
                    }
                }
                else
                {
                    Console.WriteLine("配置文件不存在，使用默认设置");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载配置失败: {ex.Message}");
                ShowCustomMessageBox($"加载配置失败: {ex.Message}\n将使用默认设置。", "配置加载错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        
        private void CheckAndRestoreGameConfiguration()
        {
            try
            {
                Console.WriteLine("检查并恢复游戏配置...");
                
                // 如果有配置的游戏，立即恢复显示和MOD
                if (!string.IsNullOrEmpty(currentGameName) && !string.IsNullOrEmpty(currentBackupPath))
                {
                    Console.WriteLine($"恢复上次选择的游戏: {currentGameName}");
                    
                    // 更新游戏列表显示
                    UpdateGamePathDisplay();
                    
                    // 立即初始化MOD
                    InitializeModsForGame();
                    
                    // 更新剑星专属功能显示
                    UpdateStellarBladeFeatures();
                    
                    Console.WriteLine($"游戏配置恢复完成，共加载 {allMods.Count} 个MOD");
                }
                else
                {
                    Console.WriteLine("没有找到有效的游戏配置，设置默认选择");
                    // 没有配置时，设置默认选择为"请选择游戏"
                    GameList.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"恢复游戏配置失败: {ex.Message}");
                // 出错时也设置默认选择
                GameList.SelectedIndex = 0;
            }
        }

        private void SaveConfiguration(string executableName)
        {
            try
            {
                // 收集所有自定义游戏名称
                List<string> customGames = new List<string>();
                
                // 从第1个（索引0是"请选择游戏"）到倒数第二个（倒数第一是"添加新游戏"）
                for (int i = 1; i < GameList.Items.Count - 1; i++)
                {
                    var item = GameList.Items[i] as ComboBoxItem;
                    string gameName = item?.Content.ToString() ?? "";
                    
                    // 跳过内置游戏选项（剑星、黑神话、光与影）
                    if (gameName.Contains("剑星") || gameName.Contains("黑神话") || gameName.Contains("光与影"))
                    {
                        continue;
                    }
                    
                    if (!string.IsNullOrEmpty(gameName))
                    {
                        customGames.Add(gameName);
                    }
                }
                
                var config = new AppConfig 
                { 
                    GameName = currentGameName,
                    GamePath = currentGamePath,
                    ModPath = currentModPath,
                    BackupPath = currentBackupPath,
                    ExecutableName = executableName,
                    CustomGames = customGames
                };
                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(configFilePath, json);
                
                Console.WriteLine($"[DEBUG] 配置已保存 - 游戏: {currentGameName}, 执行程序: {executableName}");
                Console.WriteLine($"[DEBUG] 已保存 {customGames.Count} 个自定义游戏");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"保存配置失败: {ex.Message}");
            }
        }

        private void UpdateGamePathDisplay()
        {
            Console.WriteLine($"[DEBUG] UpdateGamePathDisplay 开始，当前游戏名称: '{currentGameName}'");
            Console.WriteLine($"[DEBUG] GameList.Items.Count: {GameList.Items.Count}");
            
            // 更新GameList显示当前选择的游戏
            if (!string.IsNullOrEmpty(currentGameName))
            {
                // 查找对应的ComboBoxItem并选中
                for (int i = 0; i < GameList.Items.Count; i++)
                {
                    if (GameList.Items[i] is ComboBoxItem item)
                    {
                        var itemContent = item.Content?.ToString() ?? "";
                        Console.WriteLine($"[DEBUG] 检查游戏项 [{i}]: '{itemContent}' vs '{currentGameName}'");
                        
                        if (itemContent == currentGameName)
                        {
                            Console.WriteLine($"[DEBUG] 找到匹配的游戏项，设置选中索引: {i}");
                            
                            // 临时移除事件处理，避免触发选择更改
                            GameList.SelectionChanged -= GameList_SelectionChanged;
                            GameList.SelectedIndex = i;
                            GameList.SelectionChanged += GameList_SelectionChanged;
                            
                            Console.WriteLine($"[DEBUG] 游戏选择已设置完成");
                            return;
                        }
                    }
                }
                
                Console.WriteLine($"[DEBUG] 未找到匹配的游戏项 '{currentGameName}'");
                
                // 如果是自定义游戏但尚未在列表中，添加它
                bool isStandardGame = currentGameName.Contains("剑星") || 
                                      currentGameName.Contains("黑神话") || 
                                      currentGameName.Contains("光与影") ||
                                      string.IsNullOrEmpty(currentGameName);
                
                if (!isStandardGame)
                {
                    Console.WriteLine($"[DEBUG] 添加当前自定义游戏到下拉列表: {currentGameName}");
                    // 在倒数第二的位置添加新游戏（"添加新游戏"选项之前）
                    int insertIndex = GameList.Items.Count - 1;
                    var newGameItem = new ComboBoxItem { Content = currentGameName };
                    GameList.Items.Insert(insertIndex, newGameItem);
                    
                    // 临时移除事件处理，避免触发选择更改
                    GameList.SelectionChanged -= GameList_SelectionChanged;
                    GameList.SelectedItem = newGameItem;
                    GameList.SelectionChanged += GameList_SelectionChanged;
                    
                    Console.WriteLine($"[DEBUG] 已添加并选择自定义游戏");
                    return;
                }
            }
            
            // 如果没有找到或没有当前游戏，选择默认项
            Console.WriteLine($"[DEBUG] 设置默认选择索引 0");
            GameList.SelectionChanged -= GameList_SelectionChanged;
            GameList.SelectedIndex = 0;
            GameList.SelectionChanged += GameList_SelectionChanged;
        }

        private void InitializeData()
        {
            // 初始化服务
            InitializeServices();
            
            // 初始化游戏列表 - 先不设置选中项，等待配置恢复
            // GameList.SelectedIndex = 0; // 移除这行，让配置恢复来设置

            // 初始化分类 - 首次打开只有全部分类
            categories.Clear();
            categories.Add(new Category { Name = "全部", Count = 0 });

            // 初始化空的MOD列表
            allMods = new List<Mod>();

            // 移除重复的游戏配置恢复逻辑，让CheckAndRestoreGameConfiguration专门处理
            // 这里只做基础的UI初始化

            // 更新分类计数并显示
            UpdateCategoryCount();
            CategoryList.ItemsSource = categories;
            CategoryList.SelectedIndex = 0;

            // 显示MOD列表（初始为空）
            ModsCardView.ItemsSource = allMods;
            ModsListView.ItemsSource = allMods;

            // 清空详情面板
            ClearModDetails();
            
            // 初始化剑星专属功能（默认隐藏）
            if (StellarBladePanel != null)
            {
                StellarBladePanel.Visibility = Visibility.Collapsed;
            }
            
            Console.WriteLine("基础数据初始化完成，等待配置恢复...");
        }

        private void SetupEventHandlers()
        {
            try
            {
                // 游戏列表事件
                GameList.SelectionChanged += GameList_SelectionChanged;
                
                // 分类列表事件
                CategoryList.SelectionChanged += CategoryList_SelectionChanged;
                CategoryList.DragEnter += CategoryList_DragEnter;
                CategoryList.DragOver += CategoryList_DragOver;
                CategoryList.Drop += CategoryList_Drop;
                
                // 搜索框事件
                SearchBox.TextChanged += SearchBox_TextChanged;
                
                // 导入MOD按钮事件
                ImportModBtn.Click += (s, e) => ImportMod();
                ImportModBtn2.Click += (s, e) => ImportMod();
                
                // 启动游戏按钮事件
                LaunchGameBtn.Click += (s, e) => LaunchGame();
                
                // 筛选按钮事件
                EnabledFilterBtn.Click += EnabledFilterBtn_Click;
                DisabledFilterBtn.Click += DisabledFilterBtn_Click;
                
                // 列表视图选择变更事件处理
                ModsListView.SelectionChanged += ModsListView_SelectionChanged;
                
                Console.WriteLine("事件处理器设置完成");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"设置事件处理器失败: {ex.Message}");
            }
        }

        // === 游戏选择事件 ===
        private void GameList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                Console.WriteLine($"[DEBUG] GameList_SelectionChanged 事件触发");
                Console.WriteLine($"[DEBUG] Sender: {sender?.GetType().Name}");
                Console.WriteLine($"[DEBUG] GameList.SelectedIndex: {GameList?.SelectedIndex}");
                Console.WriteLine($"[DEBUG] GameList.SelectedItem: {GameList?.SelectedItem}");
                
                if (GameList.SelectedItem is ComboBoxItem selectedItem)
                {
                    var gameName = selectedItem.Content.ToString();
                    Console.WriteLine($"[DEBUG] 选中的游戏名称: {gameName}");
                    Console.WriteLine($"[DEBUG] 当前游戏名称: {currentGameName}");
                    
                    // 处理添加新游戏的特殊选项
                    if (gameName == "添加新游戏...")
                    {
                        Console.WriteLine($"[DEBUG] 用户选择了添加新游戏选项");
                        
                        // 恢复到原来的选择（先取消事件以避免循环）
                        GameList.SelectionChanged -= GameList_SelectionChanged;
                        
                        if (string.IsNullOrEmpty(currentGameName))
                        {
                            GameList.SelectedIndex = 0; // 选择"请选择游戏"
                        }
                        else
                        {
                            GameList.SelectedItem = GameList.Items.Cast<ComboBoxItem>()
                                .FirstOrDefault(item => item.Content.ToString() == currentGameName) ?? GameList.Items[0];
                        }
                        
                        GameList.SelectionChanged += GameList_SelectionChanged;
                        
                        // 显示添加游戏对话框
                        var addGameDialog = new Views.AddCustomGameDialog();
                        addGameDialog.Owner = this;
                        
                        if (addGameDialog.ShowDialog() == true)
                        {
                            string newGameName = addGameDialog.GameName;
                            Console.WriteLine($"[DEBUG] 用户添加了新游戏: {newGameName}");
                            
                            // 在倒数第二的位置添加新游戏
                            int insertIndex = GameList.Items.Count - 1;
                            var newGameItem = new ComboBoxItem { Content = newGameName };
                            GameList.Items.Insert(insertIndex, newGameItem);
                            
                            // 选择新添加的游戏
                            GameList.SelectionChanged -= GameList_SelectionChanged;
                            GameList.SelectedItem = newGameItem;
                            GameList.SelectionChanged += GameList_SelectionChanged;
                            
                            // 弹出配置对话框
                            ShowGamePathDialog(newGameName);
                        }
                        
                        return;
                    }
                    
                    if (gameName != "请选择游戏" && gameName != currentGameName)
                    {
                        Console.WriteLine($"[DEBUG] 准备切换游戏从 '{currentGameName}' 到 '{gameName}'");
                        
                        // 如果已经有游戏配置，显示切换确认
                        if (!string.IsNullOrEmpty(currentGameName))
                        {
                            Console.WriteLine($"[DEBUG] 显示切换确认对话框");
                            var result = ShowCustomMessageBox($"您即将从 '{currentGameName}' 切换到 '{gameName}'。\n\n" +
                                "这将重新配置游戏路径并重新扫描MOD文件。\n" +
                                "当前的MOD状态会被保存。\n\n" +
                                "是否确认切换？",
                                "切换游戏确认",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Question);
                            
                            if (result == MessageBoxResult.No)
                            {
                                Console.WriteLine($"[DEBUG] 用户取消切换，恢复原选择");
                                // 恢复到原来的选择
                                GameList.SelectionChanged -= GameList_SelectionChanged;
                                GameList.SelectedItem = GameList.Items.Cast<ComboBoxItem>()
                                    .FirstOrDefault(item => item.Content.ToString() == currentGameName) ?? GameList.Items[0];
                                GameList.SelectionChanged += GameList_SelectionChanged;
                                return;
                            }
                            Console.WriteLine($"[DEBUG] 用户确认切换");
                        }
                        
                        Console.WriteLine($"[DEBUG] 调用ShowGamePathDialog");
                        ShowGamePathDialog(gameName);
                    }
                    else
                    {
                        Console.WriteLine($"[DEBUG] 游戏选择未变化或选择了默认项，不执行切换操作");
                        
                        // 如果是选择了剑星，确保显示剑星专属功能
                        if (gameName.Contains("剑星") || gameName.Contains("Stellar"))
                        {
                            UpdateStellarBladeFeatures();
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"[DEBUG] SelectedItem 不是 ComboBoxItem 类型: {GameList?.SelectedItem?.GetType().Name}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 游戏选择失败: {ex.Message}");
                Console.WriteLine($"[ERROR] 堆栈跟踪: {ex.StackTrace}");
            }
        }
        
        // 更新游戏专属功能的显示状态
        private void UpdateStellarBladeFeatures()
        {
            try
            {
                if (StellarBladePanel != null && CollectionToolButton != null && StellarModCollectionButton != null)
                {
                    // 获取当前选择的游戏
                    var selectedItem = GameList.SelectedItem as ComboBoxItem;
                    var gameName = selectedItem?.Content.ToString() ?? "";
                    
                    // 检查是否为剑星或光与影或黑神话悟空
                    bool isStellarBlade = gameName.Contains("剑星") || gameName.Contains("Stellar");
                    bool isEnshrouded = gameName.Contains("光与影") || gameName.Contains("33号远征队") || gameName.Contains("Enshrouded");
                    bool isBlackMythWukong = gameName.Contains("黑神话") || gameName.Contains("悟空") || gameName.Contains("Black Myth") || gameName.Contains("Wukong");
                    
                    Console.WriteLine($"[DEBUG] 游戏名称识别: {gameName}, isStellarBlade={isStellarBlade}, isEnshrouded={isEnshrouded}, isBlackMythWukong={isBlackMythWukong}");
                    
                    // 更新当前游戏类型
                    if (isStellarBlade)
                    {
                        currentGameType = GameType.StellarBlade;
                    }
                    else if (isEnshrouded)
                    {
                        currentGameType = GameType.Enshrouded;
                    }
                    else if (isBlackMythWukong)
                    {
                        currentGameType = GameType.BlackMythWukong;
                    }
                    else
                    {
                        currentGameType = GameType.Other;
                    }
                    
                    // 根据游戏类型显示不同的按钮
                    if (currentGameType == GameType.StellarBlade)
                    {
                        // 剑星游戏显示两个按钮
                        StellarBladePanel.Visibility = Visibility.Visible;
                        CollectionToolButton.Visibility = Visibility.Visible;
                        StellarModCollectionButton.Visibility = Visibility.Visible;
                        
                        // 剑星使用默认网盘图片
                        XunleiImageName = "迅雷云盘.png";
                        BaiduImageName = "百度网盘.png";
                        
                        Console.WriteLine("[DEBUG] 显示剑星专属功能按钮（两个按钮）");
                    }
                    else if (currentGameType == GameType.Enshrouded)
                    {
                        // 光与影游戏只显示MOD合集按钮，隐藏收集工具箱按钮
                        StellarBladePanel.Visibility = Visibility.Visible;
                        CollectionToolButton.Visibility = Visibility.Collapsed;
                        StellarModCollectionButton.Visibility = Visibility.Visible;
                        
                        // 光与影使用专属的网盘图片
                        XunleiImageName = "光与影迅雷云盘mod.png";
                        BaiduImageName = "光与影百度网盘mod.png";
                        
                        Console.WriteLine("[DEBUG] 显示光与影专属功能按钮（仅MOD合集按钮）");
                    }
                    else if (currentGameType == GameType.BlackMythWukong)
                    {
                        // 黑神话悟空游戏显示"黑猴MOD"按钮
                        StellarBladePanel.Visibility = Visibility.Visible;
                        CollectionToolButton.Visibility = Visibility.Collapsed;
                        StellarModCollectionButton.Visibility = Visibility.Visible;
                        StellarModCollectionButton.Content = "黑猴MOD";
                        
                        // 黑神话悟空使用专属的网盘图片
                        XunleiImageName = "黑神话悟空MOD-迅雷云盘.png";
                        BaiduImageName = "黑神话悟空MOD-百度网盘.png";
                        
                        Console.WriteLine("[DEBUG] 显示黑神话悟空专属功能按钮（黑猴MOD按钮）");
                    }
                    else
                    {
                        // 其他游戏不显示任何专属按钮
                        StellarBladePanel.Visibility = Visibility.Collapsed;
                        Console.WriteLine("[DEBUG] 隐藏游戏专属功能按钮");
                    }
                }
                
                // 更新按钮文本语言
                UpdateStellarButtonLanguage();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"更新游戏专属功能失败: {ex.Message}");
            }
        }

        private void ShowGamePathDialog(string gameName)
        {
            var dialog = new Views.GamePathDialog(gameName);
            dialog.Owner = this;
            if (dialog.ShowDialog() == true)
            {
                currentGameName = gameName;
                currentGamePath = dialog.GamePath;
                currentModPath = dialog.ModPath;
                currentBackupPath = dialog.BackupPath;
                
                // 获取对话框找到的执行程序名称，如果没有则自动查找
                var executableName = !string.IsNullOrEmpty(dialog.ExecutableName) 
                    ? dialog.ExecutableName 
                    : AutoDetectGameExecutable(currentGamePath, gameName);
                
                // 更新当前执行程序名称
                currentExecutableName = executableName;
                
                SaveConfiguration(executableName);
                UpdateGamePathDisplay();
                
                // 更新剑星专属功能显示
                UpdateStellarBladeFeatures();
                
                // 显示扫描进度
                this.IsEnabled = false;
                this.Cursor = Cursors.Wait;
                
                try
                {
                    InitializeModsForGame();
                    InitializeCategoriesForGame();
                    
                    var executableInfo = !string.IsNullOrEmpty(executableName) 
                        ? $"\n游戏程序: {executableName}" 
                        : "\n游戏程序: 未找到可执行文件";
                    
                    ShowCustomMessageBox($"游戏 '{gameName}' 配置完成！\n\n" +
                        $"游戏路径: {currentGamePath}{executableInfo}\n" +
                        $"MOD路径: {currentModPath}\n" +
                        $"备份路径: {currentBackupPath}\n\n" +
                        $"已扫描到 {allMods.Count} 个MOD",
                        "配置成功",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                finally
                {
                    this.IsEnabled = true;
                    this.Cursor = Cursors.Arrow;
                }
            }
            else
            {
                // 取消选择，恢复到原来的游戏或默认状态
                GameList.SelectionChanged -= GameList_SelectionChanged;
                if (!string.IsNullOrEmpty(currentGameName))
                {
                    GameList.SelectedItem = GameList.Items.Cast<ComboBoxItem>()
                        .FirstOrDefault(item => item.Content.ToString() == currentGameName) ?? GameList.Items[0];
                }
                else
                {
                    GameList.SelectedIndex = 0;
                }
                GameList.SelectionChanged += GameList_SelectionChanged;
            }
        }

        /// <summary>
        /// 自动检测游戏执行程序
        /// </summary>
        private string AutoDetectGameExecutable(string gamePath, string gameName)
        {
            try
            {
                if (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath))
                {
                    Console.WriteLine($"[DEBUG] 游戏路径无效: {gamePath}");
                    return "";
                }

                Console.WriteLine($"[DEBUG] 开始查找游戏执行程序，游戏路径: {gamePath}, 游戏名称: {gameName}");

                // 查找所有exe文件
                var exeFiles = Directory.GetFiles(gamePath, "*.exe", SearchOption.AllDirectories);
                Console.WriteLine($"[DEBUG] 找到 {exeFiles.Length} 个可执行文件");

                if (exeFiles.Length == 0)
                {
                    Console.WriteLine($"[DEBUG] 未找到任何可执行文件");
                    return "";
                }

                // 排除常见的辅助工具和安装程序
                var excludeKeywords = new[] { "unins", "setup", "launcher", "updater", "installer", "redist", "vcredist", "directx", "crashreporter" };
                
                var validExes = exeFiles.Where(exe =>
                {
                    var fileName = Path.GetFileName(exe).ToLower();
                    return !excludeKeywords.Any(keyword => fileName.Contains(keyword));
                }).ToArray();

                Console.WriteLine($"[DEBUG] 排除辅助工具后剩余 {validExes.Length} 个有效可执行文件");
                foreach (var exe in validExes)
                {
                    Console.WriteLine($"[DEBUG] 有效exe: {Path.GetFileName(exe)}");
                }

                if (validExes.Length == 0) return "";
                if (validExes.Length == 1) 
                {
                    var singleExe = Path.GetFileName(validExes[0]);
                    Console.WriteLine($"[DEBUG] 只有一个有效exe，选择: {singleExe}");
                    return singleExe;
                }

                // 根据游戏名称查找最匹配的exe
                var gameSpecificExe = gameName switch
                {
                    "剑星" => validExes.FirstOrDefault(exe => Path.GetFileName(exe).ToLower().Contains("sb-win64-shipping")) ??
                             validExes.FirstOrDefault(exe => Path.GetFileName(exe).ToLower().Contains("stellarblade")) ??
                             validExes.FirstOrDefault(exe => Path.GetFileName(exe).ToLower().Contains("stellar")),
                     
                    var name when name.StartsWith("剑星") => validExes.FirstOrDefault(exe => Path.GetFileName(exe).ToLower().Contains("sb-win64-shipping")) ??
                             validExes.FirstOrDefault(exe => Path.GetFileName(exe).ToLower().Contains("stellarblade")) ??
                             validExes.FirstOrDefault(exe => Path.GetFileName(exe).ToLower().Contains("stellar")),
                     
                    "黑神话·悟空" => validExes.FirstOrDefault(exe => Path.GetFileName(exe).ToLower().Contains("b1-win64-shipping")) ??
                                   validExes.FirstOrDefault(exe => Path.GetFileName(exe).ToLower().Contains("wukong")) ??
                                   validExes.FirstOrDefault(exe => Path.GetFileName(exe).ToLower().Contains("blackmyth")),

                    var name when name.StartsWith("黑神话") => validExes.FirstOrDefault(exe => Path.GetFileName(exe).ToLower().Contains("b1-win64-shipping")) ??
                                   validExes.FirstOrDefault(exe => Path.GetFileName(exe).ToLower().Contains("wukong")) ??
                                   validExes.FirstOrDefault(exe => Path.GetFileName(exe).ToLower().Contains("blackmyth")),

                    "光与影：33号远征队" => validExes.FirstOrDefault(exe => Path.GetFileName(exe).ToLower().Contains("enshrouded")),

                    "艾尔登法环" => validExes.FirstOrDefault(exe => Path.GetFileName(exe).ToLower().Contains("eldenring")),

                    "赛博朋克2077" => validExes.FirstOrDefault(exe => Path.GetFileName(exe).ToLower().Contains("cyberpunk2077")),

                    "巫师3" => validExes.FirstOrDefault(exe => Path.GetFileName(exe).ToLower().Contains("witcher3")),
                    
                    _ => validExes.FirstOrDefault(exe => 
                         {
                             var fileName = Path.GetFileNameWithoutExtension(exe).ToLower();
                             var coreGameName = gameName.Split('(')[0].Trim();
                             var gameNameLower = coreGameName.ToLower().Replace("：", "").Replace("·", "").Replace(" ", "");
                             return fileName.Contains(gameNameLower) || gameNameLower.Contains(fileName);
                         })
                };

                if (!string.IsNullOrEmpty(gameSpecificExe))
                {
                    var specificExeName = Path.GetFileName(gameSpecificExe);
                    Console.WriteLine($"[DEBUG] 通过游戏名称匹配找到exe: {specificExeName}");
                    return specificExeName;
                }

                // 选择最大的exe文件（通常是主程序）
                var largestExe = validExes.OrderByDescending(exe => new FileInfo(exe).Length).First();
                var largestExeName = Path.GetFileName(largestExe);
                Console.WriteLine($"[DEBUG] 选择最大的exe文件: {largestExeName} ({FormatFileSize(new FileInfo(largestExe).Length)})");
                
                return largestExeName;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 自动检测游戏执行程序失败: {ex.Message}");
                return "";
            }
        }

        private void InitializeModsForGame()
        {
            try
            {
                allMods.Clear();

                if (!Directory.Exists(currentBackupPath))
                {
                    Directory.CreateDirectory(currentBackupPath);
                }
                
                // 1. 先扫描MOD目录中的已启用MOD
                if (Directory.Exists(currentModPath))
                {
                    ScanModsInDirectory(currentModPath, true);
                }

                // 2. 再扫描备份目录，为已加载的MOD补充信息或加载禁用的MOD
                if (Directory.Exists(currentBackupPath))
                {
                    ScanModsInBackupDirectory();
                }

                RefreshModDisplay();
                
                // 修复：在MOD加载后立即刷新分类显示
                RefreshCategoryDisplay();
                
                UpdateCategoryCount();
                
                // 初始化分类系统 (此方法可能负责更复杂的分类逻辑，暂时保留)
                InitializeCategoriesForGame();
                
                selectedMod = null;
                ClearModDetails();
                
                Console.WriteLine($"扫描完成，找到 {allMods.Count} 个MOD文件");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"初始化游戏MOD失败: {ex.Message}");
            }
        }

        private void ScanModsInDirectory(string directory, bool isEnabled)
        {
            Console.WriteLine($"[DEBUG] 扫描目录: {directory}, 启用状态: {isEnabled}");
            
            if (isEnabled)
            {
                // 扫描~mods目录下的子目录（新的组织结构）
                var modSubDirectories = Directory.GetDirectories(directory);
                Console.WriteLine($"[DEBUG] 找到 {modSubDirectories.Length} 个MOD子目录");
                
                foreach (var modDir in modSubDirectories)
                {
                    var modName = new DirectoryInfo(modDir).Name;
                    Console.WriteLine($"[DEBUG] 扫描启用的MOD目录: {modDir} (名称: {modName})");
                    
                    // 查找目录中的MOD文件
                    var modFiles = Directory.GetFiles(modDir, "*.pak", SearchOption.AllDirectories)
                        .Concat(Directory.GetFiles(modDir, "*.ucas", SearchOption.AllDirectories))
                        .Concat(Directory.GetFiles(modDir, "*.utoc", SearchOption.AllDirectories))
                        .ToList();
                    
                    Console.WriteLine($"[DEBUG] MOD {modName} 找到 {modFiles.Count} 个文件");
                    
                    if (modFiles.Count > 0 && !allMods.Any(m => m.RealName == modName))
                    {
                        var firstFile = modFiles.First();
                        var modType = DetermineModType(modName, modFiles);
                        var mod = new Mod
                        {
                            Name = modName,
                            RealName = modName,
                            Status = "已启用",
                            Type = modType,
                            Categories = new List<string> { modType }, // 修复：立即初始化分类
                            Size = FormatFileSize(modFiles.Sum(f => new FileInfo(f).Length)),
                            ImportDate = Directory.GetCreationTime(modDir).ToString("yyyy-MM-dd"),
                            Icon = GetModIcon(IOPath.GetExtension(firstFile)),
                        };
                        
                        // 从备份目录查找预览图
                        var backupDir = IOPath.Combine(currentBackupPath, modName);
                        Console.WriteLine($"[DEBUG] 查找MOD {modName} 的备份目录: {backupDir}");
                        if (Directory.Exists(backupDir))
                        {
                            var allFiles = Directory.GetFiles(backupDir, "*.*", SearchOption.TopDirectoryOnly);
                            Console.WriteLine($"[DEBUG] 备份目录中的文件: {string.Join(", ", allFiles.Select(f => IOPath.GetFileName(f)))}");
                            
                            var previewImage = allFiles.FirstOrDefault(f => 
                                IOPath.GetFileName(f).ToLower().StartsWith("preview") ||
                                new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" }.Contains(IOPath.GetExtension(f).ToLower()));
                            
                            if (previewImage != null)
                            {
                                mod.PreviewImagePath = previewImage;
                                Console.WriteLine($"[DEBUG] 为MOD {modName} 找到预览图: {previewImage}");
                            }
                            else
                            {
                                Console.WriteLine($"[DEBUG] MOD {modName} 的备份目录中未找到预览图");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"[DEBUG] MOD {modName} 的备份目录不存在");
                        }
                        
                        // 确保此MOD有备份（如果没有，创建备份）
                        BackupModFilesForScan(modName, firstFile);
                        
                        LoadModPreviewImage(mod);
                        allMods.Add(mod);
                        Console.WriteLine($"[DEBUG] 添加启用的MOD: {modName}");
                    }
                }
            }
            else
            {
                // 对于传统的扫描方式（兼容性保留，但主要依赖备份目录扫描）
                var modFiles = Directory.GetFiles(directory, "*.pak", SearchOption.AllDirectories)
                    .Concat(Directory.GetFiles(directory, "*.ucas", SearchOption.AllDirectories))
                    .Concat(Directory.GetFiles(directory, "*.utoc", SearchOption.AllDirectories))
                    .ToList();

                var groupedFiles = modFiles.GroupBy(f =>
                {
                    var fileName = IOPath.GetFileNameWithoutExtension(f);
                    // 移除可能的后缀，如 _P, _1, etc.
                    return System.Text.RegularExpressions.Regex.Replace(fileName, @"(_\d*|_P)$", "");
                });

                foreach (var group in groupedFiles)
                {
                    var modName = group.Key;
                    var firstFile = group.First();

                    if (!allMods.Any(m => m.Name == modName))
                    {
                        var modType = DetermineModType(modName, group.ToList());
                        var mod = new Mod
                        {
                            Name = modName,
                            RealName = modName,
                            Status = "已禁用",
                            Type = modType,
                            Categories = new List<string> { modType }, // 修复：立即初始化分类
                            Size = FormatFileSize(group.Sum(f => new FileInfo(f).Length)),
                            ImportDate = File.GetCreationTime(firstFile).ToString("yyyy-MM-dd"),
                            Icon = GetModIcon(IOPath.GetExtension(firstFile)),
                        };
                        
                        // 在同一目录下查找预览图
                        var modDirectory = IOPath.GetDirectoryName(firstFile);
                        if (!string.IsNullOrEmpty(modDirectory))
                        {
                            var allFilesInDir = Directory.GetFiles(modDirectory);
                            var previewImage = allFilesInDir.FirstOrDefault(f => 
                                IOPath.GetFileName(f).ToLower().Contains("preview") ||
                                IOPath.GetFileName(f).ToLower().StartsWith("preview") ||
                                (new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" }.Contains(IOPath.GetExtension(f).ToLower()) &&
                                 IOPath.GetFileNameWithoutExtension(f).ToLower().Contains(modName.ToLower())));
                            if (previewImage != null)
                            {
                                mod.PreviewImagePath = previewImage;
                            }
                        }
                        
                        LoadModPreviewImage(mod);
                        allMods.Add(mod);
                    }
                }
            }
        }

        private void ScanModsInBackupDirectory()
        {
            if (string.IsNullOrEmpty(currentBackupPath) || !Directory.Exists(currentBackupPath))
            {
                return;
            }

            var modDirectories = Directory.GetDirectories(currentBackupPath);
            foreach (var dir in modDirectories)
            {
                var modName = new DirectoryInfo(dir).Name;
                Console.WriteLine($"[DEBUG] 扫描备份目录: {dir}, MOD名称: {modName}");
                
                if (!allMods.Any(m => m.Name == modName))
                {
                    var files = Directory.GetFiles(dir);
                    Console.WriteLine($"[DEBUG] 目录 {dir} 中的文件: {string.Join(", ", files.Select(f => IOPath.GetFileName(f)))}");
                    
                    var modType = DetermineModType(modName, files.ToList());
                    var mod = new Mod
                    {
                        Name = modName,
                        RealName = modName,
                        Status = "已禁用",
                        Type = modType,
                        Categories = new List<string> { modType }, // 修复：立即初始化分类
                        Size = FormatFileSize(files.Sum(f => new FileInfo(f).Length)),
                        ImportDate = Directory.GetCreationTime(dir).ToString("yyyy-MM-dd"),
                        Icon = "📁",
                    };
                    
                    // 检查是否有预览图并预加载
                    var imageFiles = files.Where(f => new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" }.Contains(IOPath.GetExtension(f).ToLower())).ToList();
                    Console.WriteLine($"[DEBUG] 在目录中找到 {imageFiles.Count} 个图片文件: {string.Join(", ", imageFiles.Select(f => IOPath.GetFileName(f)))}");
                    
                    var previewImage = files.FirstOrDefault(f => 
                        IOPath.GetFileName(f).ToLower().Contains("preview") ||
                        IOPath.GetFileName(f).ToLower().StartsWith("preview")) 
                        ?? imageFiles.FirstOrDefault(); // 如果没找到preview，就用第一个图片文件
                    
                    Console.WriteLine($"[DEBUG] MOD {modName} 预览图查找结果: {previewImage ?? "未找到"}");
                    
                    if (previewImage != null)
                    {
                        mod.PreviewImagePath = previewImage;
                        Console.WriteLine($"[DEBUG] 设置预览图路径: {previewImage}");
                    }
                    LoadModPreviewImage(mod);

                    allMods.Add(mod);
                }
                else
                {
                    // 如果MOD已存在（从~mods目录加载），则更新其预览图路径
                    var existingMod = allMods.First(m => m.Name == modName);
                    var files = Directory.GetFiles(dir);
                    Console.WriteLine($"[DEBUG] 为已存在的MOD {modName} 查找预览图，文件: {string.Join(", ", files.Select(f => IOPath.GetFileName(f)))}");
                    
                    var imageFiles = files.Where(f => new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" }.Contains(IOPath.GetExtension(f).ToLower())).ToList();
                    Console.WriteLine($"[DEBUG] 为已存在MOD {modName} 找到 {imageFiles.Count} 个图片文件: {string.Join(", ", imageFiles.Select(f => IOPath.GetFileName(f)))}");
                    
                    var previewImage = files.FirstOrDefault(f => 
                        IOPath.GetFileName(f).ToLower().Contains("preview") ||
                        IOPath.GetFileName(f).ToLower().StartsWith("preview")) 
                        ?? imageFiles.FirstOrDefault(); // 如果没找到preview，就用第一个图片文件
                    
                    Console.WriteLine($"[DEBUG] 已存在MOD {modName} 预览图查找结果: {previewImage ?? "未找到"}");
                    
                    if (previewImage != null && string.IsNullOrEmpty(existingMod.PreviewImagePath))
                    {
                        existingMod.PreviewImagePath = previewImage;
                        Console.WriteLine($"[DEBUG] 更新已存在MOD的预览图路径: {previewImage}");
                        LoadModPreviewImage(existingMod);
                    }
                }
            }
        }

        /// <summary>
        /// 扫描时备份MOD文件（适配新的子目录组织结构）
        /// </summary>
        private bool BackupModFilesForScan(string modName, string sampleModFile)
        {
            try
            {
                Console.WriteLine($"[BACKUP] 开始备份扫描到的MOD: {modName}");
                
                // 在备份目录中为此MOD创建独立文件夹
                var modBackupDir = IOPath.Combine(currentBackupPath, modName);
                
                // 如果备份文件夹已存在且有MOD文件，跳过备份（避免重复备份）
                if (Directory.Exists(modBackupDir))
                {
                    var existingFiles = Directory.GetFiles(modBackupDir, "*.*", SearchOption.AllDirectories)
                        .Where(f => !IOPath.GetFileName(f).StartsWith("preview"))
                        .ToList();
                    
                    if (existingFiles.Count > 0)
                    {
                        Console.WriteLine($"[BACKUP] MOD {modName} 已有备份({existingFiles.Count}个文件)，跳过");
                        return true;
                    }
                }
                
                // 创建或确保MOD专属备份目录存在
                if (!Directory.Exists(modBackupDir))
                {
                    Directory.CreateDirectory(modBackupDir);
                    Console.WriteLine($"[BACKUP] 创建MOD备份目录: {modBackupDir}");
                }
                
                // 确定MOD文件的来源目录（应该是~mods/mod_name/）
                var modFileDirectory = IOPath.GetDirectoryName(sampleModFile);
                if (string.IsNullOrEmpty(modFileDirectory))
                {
                    Console.WriteLine($"[BACKUP] 无法确定MOD文件目录: {sampleModFile}");
                    return false;
                }
                
                // 检查是否是新的子目录结构
                var parentDir = IOPath.GetDirectoryName(modFileDirectory);
                bool isSubDirectoryStructure = parentDir != null && IOPath.GetFileName(parentDir) == "~mods";
                
                List<string> modFiles = new List<string>();
                
                if (isSubDirectoryStructure)
                {
                    // 新结构：~mods/mod_name/ - 备份整个子目录的内容
                    Console.WriteLine($"[BACKUP] 检测到新的子目录结构，备份目录: {modFileDirectory}");
                    modFiles = Directory.GetFiles(modFileDirectory, "*.*", SearchOption.AllDirectories)
                        .Where(f => IsModRelatedFile(f, modName))
                        .ToList();
                }
                else
                {
                    // 传统结构：直接在~mods目录下的文件
                    Console.WriteLine($"[BACKUP] 检测到传统文件结构，备份目录: {modFileDirectory}");
                    var allFilesInDir = Directory.GetFiles(modFileDirectory, "*", SearchOption.TopDirectoryOnly);
                    
                    foreach (var file in allFilesInDir)
                    {
                        if (IsModRelatedFile(file, modName))
                        {
                            modFiles.Add(file);
                        }
                    }
                }
                
                Console.WriteLine($"[BACKUP] 找到 {modFiles.Count} 个需要备份的文件");
                
                // 复制MOD文件到备份目录
                int successCount = 0;
                foreach (var modFile in modFiles)
                {
                    try
                    {
                        string backupPath;
                        
                        if (isSubDirectoryStructure)
                        {
                            // 保持相对路径结构
                            var relativePath = IOPath.GetRelativePath(modFileDirectory, modFile);
                            backupPath = IOPath.Combine(modBackupDir, relativePath);
                            
                            // 确保目标目录存在
                            var backupFileDir = IOPath.GetDirectoryName(backupPath);
                            if (!Directory.Exists(backupFileDir))
                            {
                                Directory.CreateDirectory(backupFileDir);
                            }
                        }
                        else
                        {
                            // 传统结构，直接复制到备份根目录
                            var fileName = IOPath.GetFileName(modFile);
                            backupPath = IOPath.Combine(modBackupDir, fileName);
                        }
                        
                        File.Copy(modFile, backupPath, true);
                        successCount++;
                        Console.WriteLine($"[BACKUP] 备份文件: {IOPath.GetFileName(modFile)} -> {IOPath.GetRelativePath(modBackupDir, backupPath)}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[BACKUP] 备份文件失败 {modFile}: {ex.Message}");
                    }
                }
                
                var success = successCount > 0;
                if (success)
                {
                    Console.WriteLine($"[BACKUP] MOD {modName} 备份成功，共备份 {successCount} 个文件");
                }
                else
                {
                    Console.WriteLine($"[BACKUP] MOD {modName} 备份失败，没有成功备份任何文件");
                }
                
                return success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BACKUP] 备份MOD {modName} 时发生异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 判断文件是否与指定MOD相关
        /// </summary>
        private bool IsModRelatedFile(string filePath, string modName)
        {
            var fileName = IOPath.GetFileName(filePath);
            var extension = IOPath.GetExtension(filePath).ToLower();
            
            // 常见的MOD相关文件扩展名
            var modExtensions = new[] { ".pak", ".ucas", ".utoc", ".txt", ".md", ".readme", ".png", ".jpg", ".jpeg", ".bmp", ".gif" };
            
            if (!modExtensions.Contains(extension))
            {
                return false;
            }
            
            // 检查文件名是否包含MOD名称
            var fileNameLower = fileName.ToLower();
            var modNameLower = modName.ToLower();
            
            return fileNameLower.Contains(modNameLower) || 
                   fileNameLower.StartsWith("preview") ||
                   fileNameLower.Contains("readme") ||
                   fileNameLower.Contains("description");
        }

        /// <summary>
        /// 备份MOD文件到备份目录的独立文件夹中（兼容性方法）
        /// </summary>
        private bool BackupModFiles(string modName, List<string> modFiles)
        {
            try
            {
                Console.WriteLine($"[BACKUP] 开始备份MOD文件列表: {modName}");
                
                // 在备份目录中为此MOD创建独立文件夹
                var modBackupDir = IOPath.Combine(currentBackupPath, modName);
                
                // 如果备份文件夹已存在，清空它
                if (Directory.Exists(modBackupDir))
                {
                    Directory.Delete(modBackupDir, true);
                    Console.WriteLine($"[BACKUP] 清空已存在的备份目录: {modBackupDir}");
                }
                
                // 创建MOD专属备份目录
                Directory.CreateDirectory(modBackupDir);
                Console.WriteLine($"[BACKUP] 创建MOD备份目录: {modBackupDir}");
                
                // 复制MOD文件到备份目录
                int successCount = 0;
                foreach (var modFile in modFiles)
                {
                    try
                    {
                        var fileName = IOPath.GetFileName(modFile);
                        var backupPath = IOPath.Combine(modBackupDir, fileName);
                        
                        File.Copy(modFile, backupPath, true);
                        successCount++;
                        Console.WriteLine($"[BACKUP] 备份文件: {modFile} -> {backupPath}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[BACKUP] 备份文件失败 {modFile}: {ex.Message}");
                    }
                }
                
                var success = successCount > 0;
                if (success)
                {
                    Console.WriteLine($"[BACKUP] MOD {modName} 备份成功，共备份 {successCount} 个文件");
                }
                else
                {
                    Console.WriteLine($"[BACKUP] MOD {modName} 备份失败，没有成功备份任何文件");
                    // 如果备份失败，删除空的备份目录
                    if (Directory.Exists(modBackupDir) && !Directory.GetFiles(modBackupDir, "*", SearchOption.AllDirectories).Any())
                    {
                        Directory.Delete(modBackupDir, true);
                    }
                }
                
                return success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BACKUP] 备份MOD失败 {modName}: {ex.Message}");
                return false;
            }
        }

        private string DetermineModType(string modName, List<string> modFiles)
        {
            var name = modName.ToLower();
            
            // 根据MOD名称智能分类
            if (name.Contains("face") || name.Contains("facial") || name.Contains("face") || name.Contains("脸") || name.Contains("面部"))
                return "面部";
            if (name.Contains("character") || name.Contains("body") || name.Contains("skin") || name.Contains("人物") || name.Contains("角色") || name.Contains("身体"))
                return "人物";
            if (name.Contains("weapon") || name.Contains("sword") || name.Contains("gun") || name.Contains("武器") || name.Contains("剑") || name.Contains("刀"))
                return "武器";
            if (name.Contains("outfit") || name.Contains("cloth") || name.Contains("suit") || name.Contains("服装") || name.Contains("衣服") || name.Contains("套装"))
                return "服装";
            if (name.Contains("hair") || name.Contains("头发") || name.Contains("发型"))
                return "发型";
            
            return "其他";
        }

        /// <summary>
        /// 从文件加载图片到MOD对象的PreviewImageSource属性
        /// </summary>
        private void LoadModPreviewImage(Mod mod)
        {
            Console.WriteLine($"[DEBUG] 开始加载预览图: MOD={mod.Name}, Path={mod.PreviewImagePath}");
            
            if (string.IsNullOrEmpty(mod.PreviewImagePath))
            {
                Console.WriteLine($"[DEBUG] 预览图路径为空: MOD={mod.Name}");
                mod.PreviewImageSource = null;
                mod.OnPropertyChanged(nameof(Mod.PreviewImageSource));
                return;
            }
            
            if (!File.Exists(mod.PreviewImagePath))
            {
                Console.WriteLine($"[DEBUG] 预览图文件不存在: {mod.PreviewImagePath}");
                mod.PreviewImageSource = null;
                mod.OnPropertyChanged(nameof(Mod.PreviewImageSource));
                return;
            }

            try
            {
                Console.WriteLine($"[DEBUG] 开始加载图片文件: {mod.PreviewImagePath}");
                Console.WriteLine($"[DEBUG] 文件大小: {new FileInfo(mod.PreviewImagePath).Length} 字节");
                
                // 使用Uri方式创建BitmapImage，避免缓存问题和文件锁定
                var uri = new Uri(mod.PreviewImagePath, UriKind.Absolute);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = uri;
                bitmap.CacheOption = BitmapCacheOption.OnLoad; // 将数据加载到内存
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache; // 忽略系统缓存
                bitmap.EndInit();
                bitmap.Freeze(); // 冻结以支持跨线程访问
                
                Console.WriteLine($"[DEBUG] BitmapImage创建成功: 宽度={bitmap.PixelWidth}, 高度={bitmap.PixelHeight}");
                
                // 设置PreviewImageSource属性，触发UI更新
                mod.PreviewImageSource = bitmap;
                
                Console.WriteLine($"[DEBUG] 成功加载预览图: {mod.Name}, ImageSource已设置");
                
                // 强制通知属性变更
                mod.OnPropertyChanged(nameof(Mod.PreviewImageSource));
                Console.WriteLine($"[DEBUG] 已触发PropertyChanged事件: PreviewImageSource");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] 加载预览图失败: {mod.PreviewImagePath}");
                Console.WriteLine($"[DEBUG] 错误详情: {ex.Message}");
                Console.WriteLine($"[DEBUG] 堆栈跟踪: {ex.StackTrace}");
                mod.PreviewImageSource = null;
                mod.OnPropertyChanged(nameof(Mod.PreviewImageSource));
            }
        }

        private void RefreshModDisplay()
        {
            try
            {
                Console.WriteLine("开始刷新MOD显示...");
                
                // 强制释放所有图片资源和文件锁定
                foreach (var mod in allMods)
                {
                    if (mod.PreviewImageSource != null)
                    {
                        mod.PreviewImageSource = null;
                        mod.OnPropertyChanged(nameof(Mod.PreviewImageSource));
                    }
                }
                
                // 清空UI数据源
                ModsCardView.ItemsSource = null;
                ModsListView.ItemsSource = null;
                
                // 强制多次垃圾回收，确保释放文件锁定
                for (int i = 0; i < 3; i++)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
                
                // 强制界面更新
                ModsCardView.UpdateLayout();
                ModsListView.UpdateLayout();
                
                // 获取过滤后的MOD列表
                var filteredMods = GetFilteredMods();
                
                // 重新加载显示MOD的预览图
                foreach (var mod in filteredMods)
                {
                    if (!string.IsNullOrEmpty(mod.PreviewImagePath))
                    {
                        LoadModPreviewImage(mod);
                    }
                }
                
                // 重新设置数据源为过滤后的数据
                ModsCardView.ItemsSource = filteredMods;
                ModsListView.ItemsSource = filteredMods;
                
                // 强制重新绘制
                ModsCardView.InvalidateVisual();
                ModsListView.InvalidateVisual();
                
                UpdateModCountDisplay();
                
                Console.WriteLine($"MOD显示刷新完成，共显示 {filteredMods.Count} 个MOD");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"刷新MOD显示失败: {ex.Message}");
            }
        }

        // 获取过滤后的MOD列表（搜索+分类过滤）
        private List<Mod> GetFilteredMods()
        {
            try
            {
                var filteredMods = allMods.AsEnumerable();
                
                // 应用搜索过滤
                var searchText = SearchBox?.Text?.Trim();
                if (!string.IsNullOrEmpty(searchText))
                {
                    filteredMods = filteredMods.Where(mod => 
                        (mod.Name?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (mod.RealName?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (mod.Description?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false)
                    );
                    Console.WriteLine($"[DEBUG] 搜索关键词: '{searchText}'");
                }
                
                // 应用分类过滤
                var selectedItem = CategoryList?.SelectedItem;
                if (selectedItem is Category category)
                {
                    switch (category.Name)
                    {
                        case "全部":
                        case "All":
                            // 显示所有MOD，无需过滤
                            break;
                        case "已启用":
                        case "Enabled":
                            filteredMods = filteredMods.Where(mod => mod.Status == "已启用" || mod.Status == "Enabled");
                            break;
                        case "已禁用":
                        case "Disabled":
                            filteredMods = filteredMods.Where(mod => mod.Status == "已禁用" || mod.Status == "Disabled");
                            break;
                        default:
                            // 按类型过滤
                            filteredMods = filteredMods.Where(mod => mod.Type == category.Name);
                            break;
                    }
                }
                else if (selectedItem is UEModManager.Core.Models.CategoryItem categoryItem)
                {
                    // 按自定义分类过滤
                    filteredMods = filteredMods.Where(mod => mod.Categories.Contains(categoryItem.Name));
                }
                
                var result = filteredMods.ToList();
                Console.WriteLine($"[DEBUG] 过滤后MOD数量: {result.Count}");
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"过滤MOD列表失败: {ex.Message}");
                return allMods;
            }
        }

        private void ClearModDetails()
        {
            try
            {
                if (ModNameText != null) ModNameText.Text = "选择一个MOD以查看...";
                if (ModStatusText != null) ModStatusText.Text = "未选择";
                if (ModOriginalNameText != null) ModOriginalNameText.Text = "-";
                if (ModImportDateText != null) ModImportDateText.Text = "-";
                if (ModSizeText != null) ModSizeText.Text = "-";
                if (ModDescriptionText != null) ModDescriptionText.Text = "请选择一个MOD查看详情";

                // 重置滑块到禁用状态
                UpdateToggleState(false);

                // 设置默认图标
                if (ModDetailIcon != null) ModDetailIcon.Text = "📦";

                // 设置状态颜色为灰色
                if (ModStatusText != null)
                {
                    ModStatusText.Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128)); // #6B7280
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"清空MOD详情失败: {ex.Message}");
            }
        }

        private void DeleteModButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                var mod = button?.DataContext as Mod;
                if (mod != null)
                {
                    var result = ShowCustomMessageBox($"确定要删除MOD \"{mod.Name}\" 吗？\n这将同时删除备份文件和MOD目录中的文件。", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (result == MessageBoxResult.Yes)
                    {
                        try
                        {
                            // 删除备份目录中的文件和文件夹
                            if (!string.IsNullOrEmpty(currentBackupPath))
                            {
                                var modBackupDir = Path.Combine(currentBackupPath, mod.Name);
                                if (Directory.Exists(modBackupDir))
                                {
                                    Directory.Delete(modBackupDir, true);
                                    Console.WriteLine($"[DEBUG] 已删除MOD备份目录: {modBackupDir}");
                                }
                                
                                // 同时删除可能存在的同名文件
                                var backupFiles = Directory.GetFiles(currentBackupPath, $"{mod.Name}.*", SearchOption.TopDirectoryOnly);
                                foreach (var file in backupFiles)
                                {
                                    File.Delete(file);
                                    Console.WriteLine($"[DEBUG] 已删除MOD备份文件: {file}");
                                }
                            }

                            // 删除MOD目录中的文件和文件夹
                            if (!string.IsNullOrEmpty(currentModPath))
                            {
                                var modDir = Path.Combine(currentModPath, mod.Name);
                                if (Directory.Exists(modDir))
                                {
                                    Directory.Delete(modDir, true);
                                    Console.WriteLine($"[DEBUG] 已删除MOD目录: {modDir}");
                                }
                                
                                // 同时删除可能存在的同名文件
                                var modFiles = Directory.GetFiles(currentModPath, $"{mod.Name}.*", SearchOption.TopDirectoryOnly);
                                foreach (var file in modFiles)
                                {
                                    File.Delete(file);
                                    Console.WriteLine($"[DEBUG] 已删除MOD文件: {file}");
                                }
                            }

                            // 从列表中移除
                            allMods.Remove(mod);
                            RefreshModDisplay();
                            UpdateCategoryCount();
                            
                            // 如果删除的是当前选中的MOD，清除详情面板
                            if (selectedMod == mod)
                            {
                                selectedMod = null;
                                ClearModDetails();
                            }
                            
                            ShowCustomMessageBox($"已删除MOD: {mod.Name}", "删除成功", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        catch (Exception deleteEx)
                        {
                            ShowCustomMessageBox($"删除MOD文件时发生错误: {deleteEx.Message}", "删除失败", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ShowCustomMessageBox($"删除MOD失败: {ex.Message}", "删除失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // === 输入对话框 ===
        private string ShowInputDialog(string prompt, string title, string defaultValue = "")
        {
            var inputWindow = new Window
            {
                Title = title,
                Width = 450,
                Height = 220,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0B1426")),
                WindowStyle = WindowStyle.ToolWindow
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var promptLabel = new TextBlock
            {
                Text = prompt,
                Foreground = Brushes.White,
                Margin = new Thickness(20),
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 14,
                FontWeight = FontWeights.Medium
            };
            Grid.SetRow(promptLabel, 0);

            var inputBox = new TextBox
            {
                Text = defaultValue,
                Margin = new Thickness(20, 0, 20, 10),
                Padding = new Thickness(10),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A2433")),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00D4AA")),
                BorderThickness = new Thickness(2),
                FontSize = 14
            };
            Grid.SetRow(inputBox, 1);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(20, 0, 20, 20)
            };

            var okButton = new Button
            {
                Content = "确定",
                Width = 90,
                Height = 36,
                Margin = new Thickness(0, 0, 10, 0),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00D4AA")),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontSize = 14,
                FontWeight = FontWeights.Bold
            };

            var cancelButton = new Button
            {
                Content = "取消",
                Width = 90,
                Height = 36,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A5568")),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontSize = 14
            };

            string result = "";
            bool okClicked = false;

            okButton.Click += (s, e) =>
            {
                result = inputBox.Text;
                okClicked = true;
                inputWindow.Close();
            };

            cancelButton.Click += (s, e) =>
            {
                inputWindow.Close();
            };

            inputBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    result = inputBox.Text;
                    okClicked = true;
                    inputWindow.Close();
                }
                else if (e.Key == Key.Escape)
                {
                    inputWindow.Close();
                }
            };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            Grid.SetRow(buttonPanel, 2);

            grid.Children.Add(promptLabel);
            grid.Children.Add(inputBox);
            grid.Children.Add(buttonPanel);

            inputWindow.Content = grid;
            inputBox.Focus();
            inputBox.SelectAll();

            inputWindow.ShowDialog();

            return okClicked ? result : "";
        }

        // === 自定义深色主题MessageBox ===
        private MessageBoxResult ShowCustomMessageBox(string message, string title, MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None)
        {
            // 根据消息长度和类型决定窗口尺寸
            int width = 450;
            int height = 250;
            
            // 对于简短的成功/信息消息，使用更小的尺寸
            if (icon == MessageBoxImage.Information && message.Length < 50)
            {
                width = 350;
                height = 200;
            }
            // 对于较长的消息（如系统状态），使用更大的尺寸
            else if (message.Length > 200)
            {
                width = 550;
                height = 350;
            }
            
            var messageWindow = new Window
            {
                Title = title,
                Width = width,
                Height = height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F1A2E")), // 稍微更亮的背景色
                WindowStyle = WindowStyle.SingleBorderWindow,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A3441")), // 更明显的边框
                BorderThickness = new Thickness(1)
            };

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 内容
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 按钮

            // 内容区域
            var contentGrid = new Grid();
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // 图标
            string iconText = icon switch
            {
                MessageBoxImage.Information => "ℹ️",
                MessageBoxImage.Warning => "⚠️",
                MessageBoxImage.Error => "❌",
                MessageBoxImage.Question => "❓",
                _ => "💬"
            };

            var iconBlock = new TextBlock
            {
                Text = iconText,
                FontSize = 32,
                Margin = new Thickness(20, 20, 15, 20),
                VerticalAlignment = VerticalAlignment.Top
            };
            Grid.SetColumn(iconBlock, 0);

            // 消息文本
            var messageText = new TextBlock
            {
                Text = message,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF")), // 纯白色文本提高对比度
                FontSize = 14,
                Margin = new Thickness(0, 20, 20, 20),
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(messageText, 1);

            contentGrid.Children.Add(iconBlock);
            contentGrid.Children.Add(messageText);
            Grid.SetRow(contentGrid, 0);

            // 按钮区域
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(20, 0, 20, 20),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F1B2E"))
            };

            MessageBoxResult result = MessageBoxResult.None;

            // 根据按钮类型创建按钮
            switch (buttons)
            {
                case MessageBoxButton.OK:
                    var okBtn = CreateMessageBoxButton("确定", true);
                    okBtn.Click += (s, e) => { result = MessageBoxResult.OK; messageWindow.Close(); };
                    buttonPanel.Children.Add(okBtn);
                    break;

                case MessageBoxButton.OKCancel:
                    var cancelBtn1 = CreateMessageBoxButton("取消", false);
                    var okBtn1 = CreateMessageBoxButton("确定", true);
                    cancelBtn1.Click += (s, e) => { result = MessageBoxResult.Cancel; messageWindow.Close(); };
                    okBtn1.Click += (s, e) => { result = MessageBoxResult.OK; messageWindow.Close(); };
                    buttonPanel.Children.Add(cancelBtn1);
                    buttonPanel.Children.Add(okBtn1);
                    break;

                case MessageBoxButton.YesNo:
                    var noBtn = CreateMessageBoxButton("否", false);
                    var yesBtn = CreateMessageBoxButton("是", true);
                    noBtn.Click += (s, e) => { result = MessageBoxResult.No; messageWindow.Close(); };
                    yesBtn.Click += (s, e) => { result = MessageBoxResult.Yes; messageWindow.Close(); };
                    buttonPanel.Children.Add(noBtn);
                    buttonPanel.Children.Add(yesBtn);
                    break;

                case MessageBoxButton.YesNoCancel:
                    var cancelBtn2 = CreateMessageBoxButton("取消", false);
                    var noBtn2 = CreateMessageBoxButton("否", false);
                    var yesBtn2 = CreateMessageBoxButton("是", true);
                    cancelBtn2.Click += (s, e) => { result = MessageBoxResult.Cancel; messageWindow.Close(); };
                    noBtn2.Click += (s, e) => { result = MessageBoxResult.No; messageWindow.Close(); };
                    yesBtn2.Click += (s, e) => { result = MessageBoxResult.Yes; messageWindow.Close(); };
                    buttonPanel.Children.Add(cancelBtn2);
                    buttonPanel.Children.Add(noBtn2);
                    buttonPanel.Children.Add(yesBtn2);
                    break;
            }

            Grid.SetRow(buttonPanel, 1);

            // 添加键盘支持
            messageWindow.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    result = MessageBoxResult.Cancel;
                    messageWindow.Close();
                }
                else if (e.Key == Key.Enter && buttons == MessageBoxButton.OK)
                {
                    result = MessageBoxResult.OK;
                    messageWindow.Close();
                }
            };

            mainGrid.Children.Add(contentGrid);
            mainGrid.Children.Add(buttonPanel);

            messageWindow.Content = mainGrid;
            messageWindow.ShowDialog();

            return result;
        }

        private Button CreateMessageBoxButton(string text, bool isPrimary)
        {
            var button = new Button
            {
                Content = text,
                Width = 80,
                Height = 32,
                Margin = new Thickness(10, 0, 0, 0),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                FontSize = 13
            };

            if (isPrimary)
            {
                // 使用更高对比度的颜色
                button.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));
                button.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#000000"));
                button.FontWeight = FontWeights.Bold;
            }
            else
            {
                // 使用更高对比度的颜色
                button.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A5568"));
                button.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
            }

            // 添加鼠标悬停效果
            button.MouseEnter += (s, e) =>
            {
                if (isPrimary)
                {
                    button.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FBBF24"));
                }
                else
                {
                    button.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280"));
                }
            };

            button.MouseLeave += (s, e) =>
            {
                if (isPrimary)
                {
                    button.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));
                }
                else
                {
                    button.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A5568"));
                }
            };

            return button;
        }

        // === 拖拽事件处理 ===
        private void MainWindow_DragEnter(object sender, DragEventArgs e)
        {
            // 检查拖拽数据是否包含文件
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                
                // 检查是否有支持的文件格式
                var supportedExtensions = new[] { ".zip", ".rar", ".7z" };
                var hasValidFiles = files.Any(file => 
                {
                    var extension = IOPath.GetExtension(file).ToLowerInvariant();
                    return supportedExtensions.Contains(extension);
                });

                if (hasValidFiles)
                {
                    e.Effects = DragDropEffects.Copy;
                }
                else
                {
                    e.Effects = DragDropEffects.None;
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void MainWindow_DragOver(object sender, DragEventArgs e)
        {
            // 在拖拽过程中保持效果
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void MainWindow_Drop(object sender, DragEventArgs e)
        {
            try
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                    foreach (string file in files)
                    {
                        try
                        {
                            ImportModFromFile(file);
                        }
                        catch (Exception fileEx)
                        {
                            ShowCustomMessageBox($"导入文件 {IOPath.GetFileName(file)} 失败: {fileEx.Message}", "导入失败", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ShowCustomMessageBox($"拖拽导入失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // === 自定义滑块事件处理 ===
        private void CustomToggle_MouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (selectedMod == null)
                {
                    Console.WriteLine("[DEBUG] 没有选中的MOD，无法切换状态");
                    return;
                }

                bool currentlyEnabled = selectedMod.Status == "已启用";
                Console.WriteLine($"[DEBUG] 滑动开关点击: MOD={selectedMod.Name}, 当前状态={selectedMod.Status}, 将切换为={(!currentlyEnabled ? "启用" : "禁用")}");

                if (currentlyEnabled)
                {
                    // 当前已启用，切换为禁用
                    DisableMod(selectedMod);
                }
                else
                {
                    // 当前已禁用，切换为启用
                    EnableMod(selectedMod);
                }

                // 更新详情面板显示
                UpdateModDetails(selectedMod);
                
                // 刷新MOD列表显示
                RefreshModDisplay();
                
                // 更新分类计数
                UpdateCategoryCount();
                
                Console.WriteLine($"[DEBUG] 滑动开关切换完成: MOD={selectedMod.Name}, 新状态={selectedMod.Status}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"滑动开关切换失败: {ex.Message}");
                ShowCustomMessageBox($"切换MOD状态失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateToggleState(bool isEnabled)
        {
            try
            {
                var customToggle = FindName("CustomToggle") as Border;
                var toggleThumb = FindName("ToggleThumb") as Border;
                
                if (customToggle != null && toggleThumb != null)
                {
                    if (isEnabled)
                    {
                        customToggle.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FBBF24"));
                        toggleThumb.Margin = new Thickness(22, 0, 0, 0);
                    }
                    else
                    {
                        customToggle.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A5568"));
                        toggleThumb.Margin = new Thickness(2, 0, 0, 0);
                    }
                }
            }
            catch (Exception ex)
            {
                // 避免UI更新异常导致闪退
                System.Diagnostics.Debug.WriteLine($"UpdateToggleState error: {ex.Message}");
            }
        }

        // === 右侧详情面板按钮事件 ===
        private void RenameModButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (selectedMod == null)
                {
                    ShowCustomMessageBox("请先选择一个MOD", "编辑失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 使用ShowModEditDialog方法显示完整的编辑弹窗
                ShowModEditDialog(selectedMod);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"编辑MOD失败: {ex.Message}");
                ShowCustomMessageBox($"编辑失败: {ex.Message}", "编辑失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ChangePreviewButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (selectedMod == null)
                {
                    ShowCustomMessageBox("请先选择一个MOD", "修改预览图失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var openFileDialog = new OpenFileDialog
                {
                    Filter = "图片文件 (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|所有文件|*.*",
                    Title = "选择预览图"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    SetModPreviewImage(selectedMod, openFileDialog.FileName);
                }
            }
            catch (Exception ex)
            {
                ShowCustomMessageBox($"修改预览图失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DisableModButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (selectedMod != null)
                {
                    DisableMod(selectedMod);
                    UpdateModDetails(selectedMod);
                    RefreshModDisplay();
                    UpdateCategoryCount();
                }
            }
            catch (Exception ex)
            {
                ShowCustomMessageBox($"禁用MOD失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteCurrentModButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (selectedMod != null)
                {
                    var result = ShowCustomMessageBox($"确定要删除MOD \"{selectedMod.Name}\" 吗？\n这将同时删除备份文件和MOD目录中的文件。", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (result == MessageBoxResult.Yes)
                    {
                        try
                        {
                            // 删除备份目录中的文件和文件夹
                            if (!string.IsNullOrEmpty(currentBackupPath))
                            {
                                var modBackupDir = Path.Combine(currentBackupPath, selectedMod.Name);
                                if (Directory.Exists(modBackupDir))
                                {
                                    Directory.Delete(modBackupDir, true);
                                    Console.WriteLine($"[DEBUG] 已删除MOD备份目录: {modBackupDir}");
                                }
                                
                                // 同时删除可能存在的同名文件
                                var backupFiles = Directory.GetFiles(currentBackupPath, $"{selectedMod.Name}.*", SearchOption.TopDirectoryOnly);
                                foreach (var file in backupFiles)
                                {
                                    File.Delete(file);
                                    Console.WriteLine($"[DEBUG] 已删除MOD备份文件: {file}");
                                }
                            }

                            // 删除MOD目录中的文件和文件夹
                            if (!string.IsNullOrEmpty(currentModPath))
                            {
                                var modDir = Path.Combine(currentModPath, selectedMod.Name);
                                if (Directory.Exists(modDir))
                                {
                                    Directory.Delete(modDir, true);
                                    Console.WriteLine($"[DEBUG] 已删除MOD目录: {modDir}");
                                }
                                
                                // 同时删除可能存在的同名文件
                                var modFiles = Directory.GetFiles(currentModPath, $"{selectedMod.Name}.*", SearchOption.TopDirectoryOnly);
                                foreach (var file in modFiles)
                                {
                                    File.Delete(file);
                                    Console.WriteLine($"[DEBUG] 已删除MOD文件: {file}");
                                }
                            }

                            // 从列表中移除
                            allMods.Remove(selectedMod);
                            RefreshModDisplay();
                            UpdateCategoryCount();
                            
                            // 清除详情面板
                            selectedMod = null;
                            ClearModDetails();
                            
                            ShowCustomMessageBox("已删除MOD", "删除成功", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        catch (Exception deleteEx)
                        {
                            ShowCustomMessageBox($"删除MOD文件时发生错误: {deleteEx.Message}", "删除失败", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ShowCustomMessageBox($"删除MOD失败: {ex.Message}", "删除失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // === 批量操作功能 ===
        private void DisableAllButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = ShowCustomMessageBox("确定要禁用所有MOD吗？", "确认禁用", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    int disabledCount = 0;
                    var enabledMods = allMods.Where(m => m.Status == "已启用").ToList();
                    
                    if (enabledMods.Count == 0)
                    {
                        ShowCustomMessageBox("没有需要禁用的MOD", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                    
                    // 设置批量操作标志，避免每个MOD都弹窗
                    IsInBatchOperation = true;
                    
                    try
                    {
                        foreach (var mod in enabledMods)
                        {
                            try
                            {
                                DisableMod(mod);
                                disabledCount++;
                            }
                            catch (Exception modEx)
                            {
                                Console.WriteLine($"禁用MOD {mod.Name} 失败: {modEx.Message}");
                            }
                        }
                    }
                    finally
                    {
                        // 确保无论如何都会重置批量操作标志
                        IsInBatchOperation = false;
                    }
                    
                    RefreshModDisplay();
                    if (selectedMod != null)
                    {
                        UpdateModDetails(selectedMod);
                    }
                    UpdateCategoryCount();
                    
                    // 只显示一个总结弹窗
                    ShowCustomMessageBox($"已禁用 {disabledCount} 个MOD", "禁用成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                // 确保异常时也重置批量操作标志
                IsInBatchOperation = false;
                ShowCustomMessageBox($"批量禁用失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EnableAllButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = ShowCustomMessageBox("确定要启用所有MOD吗？", "确认启用", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    int enabledCount = 0;
                    var disabledMods = allMods.Where(m => m.Status == "已禁用").ToList();
                    
                    if (disabledMods.Count == 0)
                    {
                        ShowCustomMessageBox("没有需要启用的MOD", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                    
                    // 设置批量操作标志，避免每个MOD都弹窗
                    IsInBatchOperation = true;
                    
                    try
                    {
                        foreach (var mod in disabledMods)
                        {
                            try
                            {
                                EnableMod(mod);
                                enabledCount++;
                            }
                            catch (Exception modEx)
                            {
                                Console.WriteLine($"启用MOD {mod.Name} 失败: {modEx.Message}");
                            }
                        }
                    }
                    finally
                    {
                        // 确保无论如何都会重置批量操作标志
                        IsInBatchOperation = false;
                    }
                    
                    RefreshModDisplay();
                    if (selectedMod != null)
                    {
                        UpdateModDetails(selectedMod);
                    }
                    UpdateCategoryCount();
                    
                    // 只显示一个总结弹窗
                    ShowCustomMessageBox($"已启用 {enabledCount} 个MOD", "启用成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                // 确保异常时也重置批量操作标志
                IsInBatchOperation = false;
                ShowCustomMessageBox($"批量启用失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 简化版：删除所有已启用的MOD
                var enabledMods = allMods.Where(m => m.Status == "已启用").ToList();
                if (enabledMods.Count == 0)
                {
                    ShowCustomMessageBox("没有启用的MOD可删除", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var result = ShowCustomMessageBox($"确定要删除 {enabledMods.Count} 个已启用的MOD吗？", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    foreach (var mod in enabledMods.ToList())
                    {
                        try
                        {
                            // 删除备份目录中的文件和文件夹
                            if (!string.IsNullOrEmpty(currentBackupPath))
                            {
                                var modBackupDir = Path.Combine(currentBackupPath, mod.Name);
                                if (Directory.Exists(modBackupDir))
                                {
                                    Directory.Delete(modBackupDir, true);
                                    Console.WriteLine($"[DEBUG] 已删除MOD备份目录: {modBackupDir}");
                                }
                                
                                // 同时删除可能存在的同名文件
                                var backupFiles = Directory.GetFiles(currentBackupPath, $"{mod.Name}.*", SearchOption.TopDirectoryOnly);
                                foreach (var file in backupFiles)
                                {
                                    File.Delete(file);
                                    Console.WriteLine($"[DEBUG] 已删除MOD备份文件: {file}");
                                }
                            }

                            // 删除MOD目录中的文件和文件夹
                            if (!string.IsNullOrEmpty(currentModPath))
                            {
                                var modDir = Path.Combine(currentModPath, mod.Name);
                                if (Directory.Exists(modDir))
                                {
                                    Directory.Delete(modDir, true);
                                    Console.WriteLine($"[DEBUG] 已删除MOD目录: {modDir}");
                                }
                                
                                // 同时删除可能存在的同名文件
                                var modFiles = Directory.GetFiles(currentModPath, $"{mod.Name}.*", SearchOption.TopDirectoryOnly);
                                foreach (var file in modFiles)
                                {
                                    File.Delete(file);
                                    Console.WriteLine($"[DEBUG] 已删除MOD文件: {file}");
                                }
                            }
                            
                            // 从列表中移除
                            allMods.Remove(mod);
                        }
                        catch (Exception fileEx)
                        {
                            Console.WriteLine($"删除MOD {mod.Name} 失败: {fileEx.Message}");
                        }
                    }
                    
                    RefreshModDisplay();
                    UpdateCategoryCount();
                    
                    // 如果删除的包含当前选中的MOD，清除详情面板
                    if (selectedMod != null && enabledMods.Contains(selectedMod))
                    {
                        selectedMod = null;
                        ClearModDetails();
                    }
                    
                    ShowCustomMessageBox($"已删除 {enabledMods.Count} 个MOD", "删除成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                ShowCustomMessageBox($"批量删除失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // === MOD卡片按钮事件 ===
        private void EditModButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                var mod = button?.Tag as Mod ?? button?.DataContext as Mod;
                if (mod != null)
                {
                    ShowModEditDialog(mod);
                }
            }
            catch (Exception ex)
            {
                ShowCustomMessageBox($"编辑MOD失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 显示MOD编辑对话框
        private void ShowModEditDialog(Mod mod)
        {
            try
            {
                var dialog = new Window
                {
                    Title = "编辑MOD",
                    Width = 550,
                    Height = 450,
                    MinWidth = 500,
                    MinHeight = 400,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                    AllowsTransparency = true,
                    WindowStyle = WindowStyle.None,
                    Background = Brushes.Transparent,
                    ResizeMode = ResizeMode.CanResize
                };

                var mainBorder = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0f172a")),
                    CornerRadius = new CornerRadius(8),
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#374151")),
                    BorderThickness = new Thickness(1),
                    Effect = new DropShadowEffect
                    {
                        Color = Colors.Black,
                        Direction = 315,
                        ShadowDepth = 3,
                        Opacity = 0.5,
                        BlurRadius = 10
                    }
                };

                var grid = new Grid();
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                // 自定义标题栏
                var titleBar = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1f2937")),
                    CornerRadius = new CornerRadius(8, 8, 0, 0)
                };
                titleBar.MouseDown += (s, e) => { if (e.LeftButton == MouseButtonState.Pressed) dialog.DragMove(); };
                Grid.SetRow(titleBar, 0);

                var titleGrid = new Grid();
                titleGrid.Children.Add(new TextBlock
                {
                    Text = "编辑MOD",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#d1d5db")),
                    FontSize = 16
                });

                var closeButton = new Button
                {
                    Content = "✕",
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center,
                    Width = 40,
                    Height = 40,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9ca3af"))
                };
                closeButton.Click += (s, e) => dialog.Close();
                titleGrid.Children.Add(closeButton);
                titleBar.Child = titleGrid;

                var contentGrid = new Grid { Margin = new Thickness(20) };
                Grid.SetRow(contentGrid, 1);
                
                contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 标题
                contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 名称
                contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 描述
                contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 按钮

                // 弹窗内容标题
                var titlePanel = new StackPanel
                {
                    Margin = new Thickness(0, 0, 0, 20)
                };
                Grid.SetRow(titlePanel, 0);

                titlePanel.Children.Add(new TextBlock
                {
                    Text = "编辑MOD信息",
                    FontSize = 20,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#84cc16")),
                    Margin = new Thickness(0, 0, 0, 10)
                });
                titlePanel.Children.Add(new TextBlock
                {
                    Text = "请修改MOD的名称和描述",
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9ca3af")),
                    TextWrapping = TextWrapping.Wrap
                });

                // 名称输入
                var nameGroupBox = new GroupBox
                {
                    Header = "MOD名称",
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#d1d5db")),
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#374151")),
                    Margin = new Thickness(0, 0, 0, 15)
                };
                ApplyGroupBoxStyle(nameGroupBox);
                Grid.SetRow(nameGroupBox, 1);

                var nameTextBox = new TextBox
                {
                    Text = mod.Name,
                };
                ApplyTextBoxStyle(nameTextBox);
                nameGroupBox.Content = nameTextBox;

                // 描述输入区域
                var descGroupBox = new GroupBox
                {
                    Header = "MOD描述",
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#d1d5db")),
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#374151")),
                    Margin = new Thickness(0, 0, 0, 15)
                };
                ApplyGroupBoxStyle(descGroupBox);
                Grid.SetRow(descGroupBox, 2);

                var descTextBox = new TextBox
                {
                    Text = mod.Description,
                    TextWrapping = TextWrapping.Wrap,
                    AcceptsReturn = true,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    MinHeight = 150
                };
                ApplyTextBoxStyle(descTextBox);
                descGroupBox.Content = descTextBox;

                // 按钮区域
                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 15, 0, 0)
                };
                Grid.SetRow(buttonPanel, 3);

                var cancelButton = new Button
                {
                    Content = "取消",
                    Width = 80,
                    Margin = new Thickness(0, 0, 10, 0)
                };
                ApplyButtonStyle(cancelButton, false);
                cancelButton.Click += (s, e) => dialog.Close();

                var saveButton = new Button
                {
                    Content = "确认",
                    Width = 80
                };
                ApplyButtonStyle(saveButton, true);
                saveButton.Click += (s, e) =>
                {
                    try
                    {
                        var newName = nameTextBox.Text.Trim();
                        var newDesc = descTextBox.Text.Trim();

                        if (string.IsNullOrEmpty(newName))
                        {
                            ShowCustomMessageBox("MOD名称不能为空", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        bool hasChanges = false;
                        if (newName != mod.Name)
                        {
                            mod.Name = newName;
                            hasChanges = true;
                        }

                        if (newDesc != mod.Description)
                        {
                            mod.Description = newDesc;
                            hasChanges = true;
                        }

                        if (hasChanges)
                        {
                            if (selectedMod == mod)
                            {
                                UpdateModDetails(mod);
                            }
                            RefreshModDisplay();
                            ShowCustomMessageBox("MOD信息已更新", "编辑成功", MessageBoxButton.OK, MessageBoxImage.Information);
                        }

                        dialog.Close();
                    }
                    catch (Exception ex)
                    {
                        ShowCustomMessageBox($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                };

                buttonPanel.Children.Add(cancelButton);
                buttonPanel.Children.Add(saveButton);

                contentGrid.Children.Add(titlePanel);
                contentGrid.Children.Add(nameGroupBox);
                contentGrid.Children.Add(descGroupBox);
                contentGrid.Children.Add(buttonPanel);

                grid.Children.Add(titleBar);
                grid.Children.Add(contentGrid);
                
                // 添加大小调整装饰器
                var resizeGrid = new Grid();
                resizeGrid.Children.Add(mainBorder);
                
                // 添加右下角调整大小的装饰器
                var resizeGrip = new ResizeGrip
                {
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Opacity = 0.7,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748b"))
                };
                resizeGrid.Children.Add(resizeGrip);
                
                mainBorder.Child = grid;
                dialog.Content = resizeGrid;

                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                ShowCustomMessageBox($"打开编辑对话框失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // === 搜索框焦点事件 ===
        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (SearchPlaceholder != null)
            {
                SearchPlaceholder.Visibility = Visibility.Collapsed;
            }
        }

        private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var searchBox = sender as TextBox;
            if (searchBox != null && SearchPlaceholder != null && string.IsNullOrWhiteSpace(searchBox.Text))
            {
                SearchPlaceholder.Visibility = Visibility.Visible;
            }
        }

        // === 状态过滤按钮事件 ===
        private void EnabledFilterBtn_Click(object sender, RoutedEventArgs e)
        {
            FilterModsByStatus("已启用");
        }

        private void DisabledFilterBtn_Click(object sender, RoutedEventArgs e)
        {
            FilterModsByStatus("已禁用");
        }

        private void FilterModsByStatus(string status)
        {
            var filteredMods = allMods.Where(m => m.Status == status).ToList();
            ModsCardView.ItemsSource = null;
            ModsListView.ItemsSource = null;
            ModsCardView.ItemsSource = filteredMods;
            ModsListView.ItemsSource = filteredMods;
            
            // 更新标题显示
            if (ModCountText != null)
            {
                ModCountText.Text = $"{status} MOD ({filteredMods.Count})";
            }
        }

        // === 标签切换事件 ===
        private void TypeTag_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // 防止事件冒泡
            e.Handled = true;
            
            if (sender is Border border && border.Tag is Mod mod)
            {
                ShowTypeSelectionMenu(border, mod);
            }
        }

        private void ShowTypeSelectionMenu(FrameworkElement element, Mod mod)
        {
            try
            {
                // 先关闭已存在的弹窗
                CloseCurrentTypeSelectionPopup();

                // 创建ContextMenu并设置更精确的样式，防止白色背景溢出
                var contextMenu = new ContextMenu
                {
                    PlacementTarget = element,
                    Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
                    HorizontalOffset = 0, // 确保水平位置精确对齐
                    VerticalOffset = 2,   // 小幅垂直偏移避免重叠
                    StaysOpen = false,    // 允许自动关闭
                    Background = new SolidColorBrush(Color.FromRgb(42, 52, 65)), // 深色背景
                    BorderBrush = new SolidColorBrush(Color.FromRgb(75, 85, 99)), // 深灰边框
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(0), // 移除内边距避免溢出
                    HasDropShadow = true,        // 启用阴影增强视觉效果
                    // 设置更精确的样式模板防止背景溢出
                    Template = CreateContextMenuTemplate()
                };

                var types = new[] { "👥 面部", "👤 人物", "⚔️ 武器", "👕 服装", "🔧 修改", "📦 其他" };
                
                foreach (var type in types)
                {
                    var typeText = type.Substring(2).Trim(); // 移除emoji前缀并清理空格
                    var menuItem = new MenuItem
                    {
                        Header = type,
                        Background = mod.Type == typeText ? 
                            new SolidColorBrush(Color.FromRgb(16, 185, 129)) : 
                            Brushes.Transparent,
                        Foreground = Brushes.White,
                        FontWeight = mod.Type == typeText ? FontWeights.Bold : FontWeights.Normal,
                        Padding = new Thickness(12, 6, 12, 6), // 调整内边距
                        Height = 32, // 固定高度确保一致性
                        // 设置MenuItem样式防止背景溢出
                        Template = CreateMenuItemTemplate()
                    };
                    
                    menuItem.Click += (s, e) =>
                    {
                        try
                        {
                            Console.WriteLine($"[DEBUG] 更改MOD {mod.Name} 的类型从 '{mod.Type}' 到 '{typeText}'");
                            
                            // 更新MOD的类型
                            mod.Type = typeText;
                            
                            // 同时更新MOD的分类，将类型作为分类
                            mod.Categories.Clear();
                            mod.Categories.Add(typeText);
                            
                            // 关闭菜单
                            contextMenu.IsOpen = false;
                            
                            // 刷新显示
                            RefreshModDisplay();
                            RefreshCategoryDisplay();
                            
                            Console.WriteLine($"[DEBUG] MOD类型更新完成: {mod.Name} -> {typeText}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[ERROR] 更新MOD类型失败: {ex.Message}");
                        }
                    };
                    
                    contextMenu.Items.Add(menuItem);
                }

                // 设置当前菜单引用
                element.ContextMenu = contextMenu;
                
                // 立即显示菜单
                contextMenu.IsOpen = true;
                
                Console.WriteLine($"[DEBUG] 显示类型选择菜单，当前MOD类型: {mod.Type}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 显示类型选择菜单失败: {ex.Message}");
                ShowCustomMessageBox($"显示类型选择菜单失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 创建ContextMenu的控件模板，防止背景溢出
        private ControlTemplate CreateContextMenuTemplate()
        {
            var template = new ControlTemplate(typeof(ContextMenu));
            
            // 创建Border作为根元素
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(42, 52, 65)));
            border.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(75, 85, 99)));
            border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            border.SetValue(Border.PaddingProperty, new Thickness(2));
            
            // 创建StackPanel容纳MenuItem
            var stackPanel = new FrameworkElementFactory(typeof(StackPanel));
            stackPanel.SetValue(StackPanel.BackgroundProperty, Brushes.Transparent);
            
            // 创建ItemsPresenter显示菜单项
            var itemsPresenter = new FrameworkElementFactory(typeof(ItemsPresenter));
            stackPanel.AppendChild(itemsPresenter);
            
            border.AppendChild(stackPanel);
            template.VisualTree = border;
            
            return template;
        }

        // 创建MenuItem的控件模板，防止背景溢出
        private ControlTemplate CreateMenuItemTemplate()
        {
            var template = new ControlTemplate(typeof(MenuItem));
            
            // 创建Border作为MenuItem的容器
            var border = new FrameworkElementFactory(typeof(Border));
            border.Name = "Border";
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(MenuItem.BackgroundProperty));
            border.SetValue(Border.BorderThicknessProperty, new Thickness(0));
            border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(MenuItem.PaddingProperty));
            
            // 创建ContentPresenter显示Header内容
            var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(MenuItem.HeaderProperty));
            contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            
            border.AppendChild(contentPresenter);
            
            // 添加鼠标悬停触发器
            var trigger = new Trigger
            {
                Property = MenuItem.IsMouseOverProperty,
                Value = true
            };
            trigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(55, 65, 81)), "Border"));
            
            template.Triggers.Add(trigger);
            template.VisualTree = border;
            
            return template;
        }

        // 关闭当前类型选择菜单
        private void CloseCurrentTypeSelectionPopup()
        {
            try
            {
                Console.WriteLine("[DEBUG] 开始关闭标签菜单");
                
                // 关闭所有可能打开的ContextMenu
                CloseAllContextMenus();
                
                Console.WriteLine("[DEBUG] 标签菜单关闭完成");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 关闭菜单时出错: {ex.Message}");
            }
        }
        
        // 关闭所有ContextMenu
        private void CloseAllContextMenus()
        {
            try
            {
                // 从主窗口查找所有元素的ContextMenu
                CloseContextMenusInElement(this);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 关闭上下文菜单时出错: {ex.Message}");
            }
        }
        
        // 递归关闭元素及其子元素的ContextMenu
        private void CloseContextMenusInElement(DependencyObject element)
        {
            if (element == null) return;
            
            // 如果是FrameworkElement且有ContextMenu，关闭它
            if (element is FrameworkElement fe && fe.ContextMenu != null && fe.ContextMenu.IsOpen)
            {
                Console.WriteLine("[DEBUG] 关闭找到的ContextMenu");
                fe.ContextMenu.IsOpen = false;
            }
            
            // 递归处理子元素
            int childCount = VisualTreeHelper.GetChildrenCount(element);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(element, i);
                CloseContextMenusInElement(child);
            }
        }


        private void ChangeModType_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is string newType)
            {
                if (selectedMod != null)
                {
                    selectedMod.Type = newType;
                    RefreshModDisplay();
                    UpdateCategoryCount();
                }
            }
        }

        // === 右键菜单事件处理 ===
        
        // 分类右键菜单

        private void RenameCategoryMenuItem_Click(object sender, RoutedEventArgs e)
        {
            RenameCategoryButton_Click(sender, e);
        }

        private void DeleteCategoryMenuItem_Click(object sender, RoutedEventArgs e)
        {
            DeleteCategoryButton_Click(sender, e);
        }

        // MOD右键菜单
        private void EnableModMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && GetModFromContextMenu(menuItem) is Mod mod)
            {
                EnableMod(mod);
            }
        }

        private void DisableModMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && GetModFromContextMenu(menuItem) is Mod mod)
            {
                DisableMod(mod);
            }
        }

        private void RenameModMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && GetModFromContextMenu(menuItem) is Mod mod)
            {
                ShowModEditDialog(mod);
            }
        }

        private void ChangePreviewMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && GetModFromContextMenu(menuItem) is Mod mod)
            {
                ChangePreviewForMod(mod);
            }
        }

        private void DeleteModMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && GetModFromContextMenu(menuItem) is Mod mod)
            {
                DeleteSpecificMod(mod);
            }
        }
        
        // 移动MOD到分类的菜单项点击事件
        private void MoveToCategoryMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is MenuItem menuItem && GetModFromContextMenu(menuItem) is Mod mod)
                {
                    ShowMoveToCategoryDialog(mod);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"移动到分类失败: {ex.Message}");
                ShowCustomMessageBox($"移动到分类失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        // 显示移动到分类的对话框
        private void ShowMoveToCategoryDialog(Mod mod)
        {
            try
            {
                if (_categoryService == null || !_categoryService.Categories.Any())
                {
                    ShowCustomMessageBox("暂无可用分类，请先创建分类", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 获取自定义分类列表
                var categoryNames = _categoryService.Categories
                    .Where(c => !new[] { "全部", "已启用", "已禁用" }.Contains(c.Name))
                    .Select(c => c.Name)
                    .ToList();

                if (!categoryNames.Any())
                {
                    ShowCustomMessageBox("暂无自定义分类，请先创建分类", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 简单的输入对话框方式选择分类
                var categoryList = string.Join(", ", categoryNames);
                var selectedCategory = ShowInputDialog($"可用分类: {categoryList}\n\n请输入要移动到的分类名称:", "移动到分类", mod.Categories?.FirstOrDefault() ?? "");
                
                if (!string.IsNullOrEmpty(selectedCategory) && categoryNames.Contains(selectedCategory))
                {
                    // 更新MOD的分类
                    mod.Categories = new List<string> { selectedCategory };
                    
                    // 刷新分类显示以更新数量
                    RefreshCategoryDisplay();
                    
                    Console.WriteLine($"[DEBUG] MOD {mod.Name} 已移动到分类: {selectedCategory}");
                    ShowCustomMessageBox($"MOD '{mod.Name}' 已移动到分类 '{selectedCategory}'", "移动成功", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else if (!string.IsNullOrEmpty(selectedCategory))
                {
                    ShowCustomMessageBox($"分类 '{selectedCategory}' 不存在，请输入有效的分类名称", "分类不存在", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"显示移动分类对话框失败: {ex.Message}");
                ShowCustomMessageBox($"显示分类选择失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private Mod? GetModFromContextMenu(MenuItem menuItem)
        {
            // 从ContextMenu获取对应的MOD
            MenuItem currentItem = menuItem;
            
            // 向上查找到根ContextMenu
            while (currentItem.Parent is MenuItem parentMenuItem)
            {
                currentItem = parentMenuItem;
            }
            
            if (currentItem.Parent is ContextMenu contextMenu && 
                contextMenu.PlacementTarget is FrameworkElement element)
            {
                return element.DataContext as Mod;
            }
            
            // 备用方法：直接从menuItem的Tag中获取
            if (menuItem.Tag is Mod mod)
            {
                return mod;
            }
            
            return null;
        }

        private void ChangePreviewForMod(Mod mod)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "选择预览图片",
                Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp|所有文件|*.*",
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() == true)
            {
                SetModPreviewImage(mod, openFileDialog.FileName);
            }
        }

        private void DeleteSpecificMod(Mod mod)
        {
            var result = ShowCustomMessageBox($"确定要删除MOD '{mod.Name}' 吗？\n\n这将同时删除MOD文件夹和备份文件夹中的相关文件。", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // 删除备份文件夹中的文件和文件夹
                    if (Directory.Exists(currentBackupPath))
                    {
                        var modBackupDir = Path.Combine(currentBackupPath, mod.Name);
                        if (Directory.Exists(modBackupDir))
                        {
                            Directory.Delete(modBackupDir, true);
                            Console.WriteLine($"[DEBUG] 已删除MOD备份目录: {modBackupDir}");
                        }
                        
                        // 同时删除可能存在的同名文件
                        var backupFiles = Directory.GetFiles(currentBackupPath, $"{mod.Name}.*", SearchOption.TopDirectoryOnly);
                        foreach (var file in backupFiles)
                        {
                            File.Delete(file);
                            Console.WriteLine($"[DEBUG] 已删除MOD备份文件: {file}");
                        }
                    }
                    
                    // 删除MOD文件夹中的文件和文件夹
                    if (Directory.Exists(currentModPath))
                    {
                        var modDir = Path.Combine(currentModPath, mod.Name);
                        if (Directory.Exists(modDir))
                        {
                            Directory.Delete(modDir, true);
                            Console.WriteLine($"[DEBUG] 已删除MOD目录: {modDir}");
                        }
                        
                        // 同时删除可能存在的同名文件
                        var modFiles = Directory.GetFiles(currentModPath, $"{mod.Name}.*", SearchOption.TopDirectoryOnly);
                        foreach (var file in modFiles)
                        {
                            File.Delete(file);
                            Console.WriteLine($"[DEBUG] 已删除MOD文件: {file}");
                        }
                    }
                    
                    // 从列表中移除
                    allMods.Remove(mod);
                    RefreshModDisplay();
                    UpdateCategoryCount();
                    
                    // 如果删除的是当前选中的MOD，清空详情
                    if (selectedMod == mod)
                    {
                        selectedMod = null;
                        ClearModDetails();
                    }
                    
                    ShowCustomMessageBox("MOD删除成功", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    ShowCustomMessageBox($"删除MOD失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // === MOD选中和详情显示 ===
        private void ModCard_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border card && card.DataContext is Mod clickedMod)
            {
                if (e.RightButton == MouseButtonState.Pressed) return;

                var visibleMods = (ModsCardView.Visibility == Visibility.Visible ? 
                    ModsCardView.ItemsSource as IEnumerable<Mod> : 
                    ModsListView.ItemsSource as IEnumerable<Mod>)?.ToList();
                if (visibleMods == null || !visibleMods.Contains(clickedMod)) return;

                // Handle Shift-click for range selection
                if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                {
                    if (_lastSelectedMod != null && visibleMods.Contains(_lastSelectedMod))
                    {
                        int lastIndex = visibleMods.IndexOf(_lastSelectedMod);
                        int currentIndex = visibleMods.IndexOf(clickedMod);

                        // If Ctrl is not down, clear the previous selection.
                        if (!Keyboard.IsKeyDown(Key.LeftCtrl) && !Keyboard.IsKeyDown(Key.RightCtrl))
                        {
                            foreach (var mod in allMods)
                            {
                                mod.IsSelected = false;
                            }
                        }

                        int startIndex = Math.Min(lastIndex, currentIndex);
                        int endIndex = Math.Max(lastIndex, currentIndex);

                        for (int i = startIndex; i <= endIndex; i++)
                        {
                            visibleMods[i].IsSelected = true;
                        }
                    }
                    else // If there's no anchor, treat as a single click
                    {
                        foreach (var mod in allMods) mod.IsSelected = false;
                        clickedMod.IsSelected = true;
                        _lastSelectedMod = clickedMod;
                    }
                }
                // Handle Ctrl-click for toggling selection
                else if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                {
                    clickedMod.IsSelected = !clickedMod.IsSelected;
                    if (clickedMod.IsSelected)
                    {
                        _lastSelectedMod = clickedMod;
                    }
                    else if (_lastSelectedMod == clickedMod)
                    {
                        _lastSelectedMod = allMods.LastOrDefault(m => m.IsSelected);
                    }
                }
                // Handle normal click
                else
                {
                    bool wasSelected = clickedMod.IsSelected;
                    foreach (var mod in allMods.Where(m => m != clickedMod))
                    {
                        mod.IsSelected = false;
                    }
                    clickedMod.IsSelected = !wasSelected;
                    _lastSelectedMod = clickedMod.IsSelected ? clickedMod : null;
                }

                // Update details and UI
                if (allMods.Count(m => m.IsSelected) == 1)
                {
                    UpdateModDetails(allMods.First(m => m.IsSelected));
                }
                else
                {
                    ClearModDetails();
                }

                UpdateSelectAllCheckBoxState();
            }
        }
        
        private void MainContentArea_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            Console.WriteLine($"[DEBUG] MainContentArea_PreviewMouseDown triggered. Source: {e.OriginalSource.GetType().Name}");

            // 首先检查是否需要关闭标签菜单（ContextMenu会自动处理点击外部关闭）
            // 这里只需要在点击其他地方时主动关闭
            var clickedElement = e.OriginalSource as DependencyObject;
            bool isClickOnTypeTag = false;
            
            // 检查是否点击在标签上（避免重复打开）
            if (clickedElement != null)
            {
                var current = clickedElement;
                while (current != null)
                {
                    if (current is TextBlock textBlock && textBlock.Name == "TypeTag")
                    {
                        isClickOnTypeTag = true;
                        break;
                    }
                    current = VisualTreeHelper.GetParent(current);
                }
            }
            
            // 如果不是点击标签，关闭可能打开的菜单
            if (!isClickOnTypeTag)
            {
                CloseCurrentTypeSelectionPopup();
            }

            // 检查是否点击在MOD卡片上
            var source = e.OriginalSource as DependencyObject;
            bool isOnModCard = false;
            
            // 遍历视觉树查找是否点击在MOD卡片上
            var currentElement = source;
            while (currentElement != null)
            {
                if (currentElement is Border border && border.DataContext is Mod)
                {
                    isOnModCard = true;
                    Console.WriteLine("[DEBUG] Click was on a Mod Card. Not clearing selection.");
                    break;
                }
                currentElement = VisualTreeHelper.GetParent(currentElement);
            }

            // 如果不是点击在MOD卡片上，清除选择
            if (!isOnModCard)
            {
                Console.WriteLine("[DEBUG] Click was outside a Mod Card. Clearing selection.");
                foreach (var mod in allMods.Where(m => m.IsSelected))
                {
                    mod.IsSelected = false;
                }
                _lastSelectedMod = null;
                ClearModDetails();
                UpdateSelectAllCheckBoxState();
            }
        }

        private void MainContentArea_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Console.WriteLine($"[DEBUG] MainContentArea_MouseDown triggered. Source: {e.OriginalSource.GetType().Name}");

            // 检查是否点击在MOD卡片上
            var source = e.OriginalSource as DependencyObject;
            bool isOnModCard = false;
            
            // 遍历视觉树查找是否点击在MOD卡片上
            var currentElement = source;
            while (currentElement != null)
            {
                if (currentElement is Border border && border.DataContext is Mod)
                {
                    isOnModCard = true;
                    Console.WriteLine("[DEBUG] Click was on a Mod Card. Not clearing selection.");
                    break;
                }
                currentElement = VisualTreeHelper.GetParent(currentElement);
            }

            // 如果不是点击在MOD卡片上，清除选择
            if (!isOnModCard)
            {
                Console.WriteLine("[DEBUG] Click was outside a Mod Card. Clearing selection.");
                foreach (var mod in allMods.Where(m => m.IsSelected))
                {
                    mod.IsSelected = false;
                }
                _lastSelectedMod = null;
                ClearModDetails();
                UpdateSelectAllCheckBoxState();
                
                // 标记事件已处理，防止进一步传播
                e.Handled = true;
            }
        }

        private void CategoryArea_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            Console.WriteLine($"[DEBUG] CategoryArea_PreviewMouseDown triggered. Source: {e.OriginalSource.GetType().Name}");
            
            // 关闭可能打开的标签菜单
            CloseCurrentTypeSelectionPopup();
            
            // 新增：如果点击的是按钮或其子元素，不清除选中
            if (e.OriginalSource is DependencyObject depObj)
            {
                var parentButton = FindParent<Button>(depObj);
                if (parentButton != null)
                {
                    Console.WriteLine("[DEBUG] Click was on a Button, skip clearing selection.");
                    return;
                }
            }
            // 检查是否点击在分类列表项上
            var source = e.OriginalSource as DependencyObject;
            bool isOnCategoryItem = false;
            
            // 遍历视觉树查找是否点击在分类项上
            var currentElement = source;
            while (currentElement != null)
            {
                if (currentElement is ListBoxItem)
                {
                    isOnCategoryItem = true;
                    Console.WriteLine("[DEBUG] Click was on a Category ListBoxItem. Not clearing selection.");
                    break;
                }
                currentElement = VisualTreeHelper.GetParent(currentElement);
            }

            // 如果不是点击在分类项上，清除选择
            if (!isOnCategoryItem)
            {
                Console.WriteLine("[DEBUG] Click was outside a Category ListBoxItem. Clearing selection.");
                CategoryList.UnselectAll();
            }
        }

        private void CategoryArea_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Console.WriteLine($"[DEBUG] CategoryArea_MouseDown triggered. Source: {e.OriginalSource.GetType().Name}");
            
            // 检查是否点击在分类列表项上
            var source = e.OriginalSource as DependencyObject;
            bool isOnCategoryItem = false;
            
            // 遍历视觉树查找是否点击在分类项上
            var currentElement = source;
            while (currentElement != null)
            {
                if (currentElement is ListBoxItem)
                {
                    isOnCategoryItem = true;
                    Console.WriteLine("[DEBUG] Click was on a Category ListBoxItem. Not clearing selection.");
                    break;
                }
                currentElement = VisualTreeHelper.GetParent(currentElement);
            }

            // 如果不是点击在分类项上，清除选择
            if (!isOnCategoryItem)
            {
                Console.WriteLine("[DEBUG] Click was outside a Category ListBoxItem. Clearing selection.");
                CategoryList.UnselectAll();
                
                // 标记事件已处理，防止进一步传播
                e.Handled = true;
            }
        }

        // 清除所有MOD的选中状态
        private void ClearAllModsSelection()
        {
            try
            {
                foreach (var mod in allMods)
                {
                    mod.IsSelected = false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"清除MOD选中状态失败: {ex.Message}");
            }
        }
        
        // 更新全选CheckBox的状态
        private void UpdateSelectAllCheckBoxState()
        {
            try
            {
                IEnumerable<Mod>? currentMods = null;
                
                // 根据当前视图选择正确的控件
                if (ModsCardView.Visibility == Visibility.Visible)
                {
                    currentMods = ModsCardView.ItemsSource as IEnumerable<Mod>;
                }
                else
                {
                    currentMods = ModsListView.ItemsSource as IEnumerable<Mod>;
                }
                
                if (currentMods != null && currentMods.Any())
                {
                    bool allSelected = currentMods.All(m => m.IsSelected);
                    bool noneSelected = currentMods.All(m => !m.IsSelected);
                    
                    if (SelectAllCheckBox != null)
                    {
                        if (allSelected)
                        {
                            SelectAllCheckBox.IsChecked = true;
                        }
                        else if (noneSelected)
                        {
                            SelectAllCheckBox.IsChecked = false;
                        }
                        else
                        {
                            SelectAllCheckBox.IsChecked = null; // 部分选中状态
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"更新全选CheckBox状态失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 高亮选中的MOD卡片
        /// </summary>
        private void HighlightSelectedCard(Border? selectedCard)
        {
            try
            {
                // 可以在这里添加视觉反馈逻辑
                // 目前只是更新详情面板，卡片高亮通过CSS样式实现
                if (selectedCard != null)
                {
                    Console.WriteLine("MOD卡片已选中");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"高亮卡片失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 清除所有卡片的高亮状态
        /// </summary>
        private void ClearAllCardHighlights()
        {
            try
            {
                // 简化实现：通过刷新数据绑定来重置所有卡片状态
                RefreshModDisplay();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"清除卡片高亮失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 为MOD设置预览图 - 按照老版本成功逻辑重新设计，确保C1和C2都显示
        /// </summary>
        private void SetModPreviewImage(Mod mod, string imagePath)
        {
            try
            {
                Console.WriteLine($"[DEBUG] 为MOD {mod.Name} 设置预览图: {imagePath}");
                
                // 确保备份目录存在
                if (!Directory.Exists(currentBackupPath))
                {
                    Directory.CreateDirectory(currentBackupPath);
                }
                
                // 创建MOD专属备份目录（使用RealName作为目录名）
                var modBackupDir = IOPath.Combine(currentBackupPath, mod.RealName);
                if (!Directory.Exists(modBackupDir))
                {
                    Directory.CreateDirectory(modBackupDir);
                    Console.WriteLine($"[DEBUG] 创建MOD备份目录: {modBackupDir}");
                }
                
                // 获取图片扩展名并生成预览图文件名
                var imageExtension = IOPath.GetExtension(imagePath);
                var previewImageName = "preview" + imageExtension;
                var previewImagePath = IOPath.Combine(modBackupDir, previewImageName);
                
                // 强制释放现有图片资源
                if (mod.PreviewImageSource != null)
                {
                    mod.PreviewImageSource = null;
                    mod.OnPropertyChanged(nameof(Mod.PreviewImageSource));
                    Console.WriteLine($"[DEBUG] 清空MOD {mod.Name} 的现有PreviewImageSource");
                }
                
                // 强制垃圾回收释放文件锁定
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                Thread.Sleep(300); // 增加等待时间确保文件锁定释放
                
                // 删除旧的预览图文件
                if (File.Exists(previewImagePath))
                {
                    try
                    {
                        File.Delete(previewImagePath);
                        Console.WriteLine($"[DEBUG] 删除旧预览图: {previewImagePath}");
                    }
                    catch (Exception deleteEx)
                    {
                        Console.WriteLine($"[DEBUG] 删除旧预览图失败，尝试重命名: {deleteEx.Message}");
                        var backupName = previewImagePath + ".old." + DateTime.Now.Ticks;
                        try
                        {
                            File.Move(previewImagePath, backupName);
                            Console.WriteLine($"[DEBUG] 旧预览图已重命名为: {backupName}");
                        }
                        catch
                        {
                            Console.WriteLine($"[DEBUG] 无法处理旧预览图文件，强制继续");
                        }
                    }
                }
                
                // 复制新的预览图到备份目录
                File.Copy(imagePath, previewImagePath, true);
                Console.WriteLine($"[DEBUG] 预览图已复制到: {previewImagePath}");
                
                // 更新MOD的预览图路径
                mod.PreviewImagePath = previewImagePath;
                mod.Icon = "🖼️";
                
                // 重新加载预览图
                LoadModPreviewImage(mod);
                Console.WriteLine($"[DEBUG] 重新加载预览图完成, ImageSource = {mod.PreviewImageSource != null}");
                
                // 强制UI更新
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // 强制刷新当前MOD的数据绑定
                    mod.OnPropertyChanged(nameof(Mod.PreviewImageSource));
                    mod.OnPropertyChanged(nameof(Mod.PreviewImagePath));
                    
                    // 如果当前选中的是这个MOD，立即更新详情面板
                    if (selectedMod?.RealName == mod.RealName)
                    {
                        UpdateModDetailPreview(mod);
                        Console.WriteLine($"[DEBUG] 更新详情面板预览图完成");
                    }
                    
                    // 强制刷新MOD列表显示（仅刷新UI，不重新加载数据）
                    if (ModsCardView.Visibility == Visibility.Visible)
                    {
                        ModsCardView.InvalidateVisual();
                        ModsCardView.UpdateLayout();
                    }
                    else
                    {
                        ModsListView.InvalidateVisual();
                        ModsListView.UpdateLayout();
                    }
                    
                    Console.WriteLine($"[DEBUG] UI强制更新完成");
                });
                
                // 短暂延迟后再次确认
                Task.Delay(100).ContinueWith(_ =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (mod.PreviewImageSource == null)
                        {
                            Console.WriteLine($"[WARN] 预览图源仍为空，尝试重新加载");
                            LoadModPreviewImage(mod);
                        }
                    });
                });
                
                ShowCustomMessageBox("预览图设置成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                Console.WriteLine($"[DEBUG] MOD {mod.Name} 预览图设置完成，路径: {previewImagePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 设置预览图失败: {ex.Message}");
                Console.WriteLine($"[ERROR] 堆栈跟踪: {ex.StackTrace}");
                ShowCustomMessageBox($"设置预览图失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// 刷新MOD列表并保持选中状态 - 参考旧版本实现
        /// </summary>
        private void RefreshModListKeepSelected()
        {
            try
            {
                var selectedModName = selectedMod?.Name;
                Console.WriteLine($"刷新MOD列表，保持选中: {selectedModName}");
                
                // 强制垃圾回收，释放图片文件锁定
                GC.Collect();
                GC.WaitForPendingFinalizers();
                
                // 重新扫描MOD
                InitializeModsForGame();
                
                // 恢复选中状态
                if (!string.IsNullOrEmpty(selectedModName))
                {
                    var modToSelect = allMods.FirstOrDefault(m => m.Name == selectedModName);
                    if (modToSelect != null)
                    {
                        selectedMod = modToSelect;
                        UpdateModDetails(modToSelect);
                        Console.WriteLine($"已恢复选中MOD: {selectedModName}");
                    }
                }
                
                Console.WriteLine("MOD列表刷新完成，选中状态已保持");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"刷新MOD列表失败: {ex.Message}");
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Console.WriteLine("刷新按钮被点击");
                InitializeModsForGame();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"刷新失败: {ex.Message}");
                ShowCustomMessageBox($"刷新失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void NotificationButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var message = $"🎉 虚幻引擎MOD管理器 v1.9\n\n" +
                            $"✅ 当前已加载 {allMods.Count} 个MOD\n" +
                            $"📊 已启用MOD: {allMods.Count(m => m.Status == "已启用")} 个\n" +
                            $"⏸️ 已禁用MOD: {allMods.Count(m => m.Status == "已禁用")} 个\n\n" +
                            $"🎮 当前游戏: {(string.IsNullOrEmpty(currentGameName) ? "未选择" : currentGameName)}\n" +
                            $"📁 MOD目录: {currentModPath}\n" +
                            $"💾 备份目录: {currentBackupPath}\n\n" +
                            $"💡 提示：定期备份您的存档和MOD文件！";
                
                ShowCustomMessageBox(message, "系统状态", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ShowCustomMessageBox($"获取系统状态失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var settingsDialog = ShowSettingsDialog();
                if (settingsDialog == MessageBoxResult.OK)
                {
                    // 保存设置并重新加载
                    SaveConfiguration(currentExecutableName);
                    ShowCustomMessageBox("设置已保存！", "设置", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                ShowCustomMessageBox($"打开设置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 设置菜单按钮点击事件
        private void SettingsMenuButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                if (button?.ContextMenu != null)
                {
                    button.ContextMenu.PlacementTarget = button;
                    button.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                    button.ContextMenu.IsOpen = true;
                }
            }
            catch (Exception ex)
            {
                ShowCustomMessageBox($"打开设置菜单失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private MessageBoxResult ShowSettingsDialog()
        {
            var currentSettings = $"当前设置：\n\n" +
                                $"🎮 游戏名称: {currentGameName}\n" +
                                $"📁 游戏路径: {currentGamePath}\n" +
                                $"📦 MOD路径: {currentModPath}\n" +
                                $"💾 备份路径: {currentBackupPath}\n\n" +
                                $"是否要重新配置游戏路径？";
            
            var result = ShowCustomMessageBox(currentSettings, "设置 - 虚幻引擎MOD管理器", 
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes && !string.IsNullOrEmpty(currentGameName))
            {
                ShowGamePathDialog(currentGameName);
                return MessageBoxResult.OK;
            }
            
            return result;
        }

        // 新的设置菜单项事件处理器
        private void PathSettingsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(currentGameName))
                {
                    ShowGamePathDialog(currentGameName);
                }
                else
                {
                    ShowCustomMessageBox("请先选择一个游戏再配置路径。", "路径设置", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                ShowCustomMessageBox($"打开路径设置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LanguageMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                isEnglishMode = !isEnglishMode;
                UpdateLanguage();
                
                string message = isEnglishMode ? 
                    "Language switched to English successfully!" : 
                    "语言已切换为中文！";
                string title = isEnglishMode ? "Language Settings" : "语言设置";
                
                ShowCustomMessageBox(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ShowCustomMessageBox($"切换语言失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ShowAboutDialog();
            }
            catch (Exception ex)
            {
                ShowCustomMessageBox($"打开关于对话框失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CheckForUpdates();
            }
            catch (Exception ex)
            {
                ShowCustomMessageBox($"检查更新失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        // 使用说明菜单项点击事件
        private void UserManualMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ShowUserManualDialog();
            }
            catch (Exception ex)
            {
                ShowCustomMessageBox($"打开使用说明失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        // 显示使用说明对话框
        private void ShowUserManualDialog()
        {
            var dialog = new Window
            {
                Title = isEnglishMode ? "User Manual" : "使用说明",
                Width = 900,
                Height = 700,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new SolidColorBrush(Color.FromRgb(15, 27, 46)),
                WindowStyle = WindowStyle.ToolWindow,
                ResizeMode = ResizeMode.CanResize
            };

            var mainBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(15, 27, 46)),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(30)
            };

            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            
            var stackPanel = new StackPanel
            {
                Margin = new Thickness(0, 0, 10, 0)
            };

            // 标题
            var titleText = new TextBlock
            {
                Text = isEnglishMode ? "UE MOD Manager User Manual" : "虚幻引擎MOD管理器使用说明",
                Foreground = Brushes.White,
                FontSize = 30,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 25)
            };
            stackPanel.Children.Add(titleText);

            // 添加使用说明内容
            AddUserManualContent(stackPanel);

            // 关闭按钮
            var closeButton = new Button
            {
                Content = isEnglishMode ? "Close" : "关闭",
                Width = 150,
                Height = 45,
                Background = new SolidColorBrush(Color.FromRgb(75, 85, 99)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontSize = 18,
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 30, 0, 0)
            };
            
            // 添加按钮悬停效果
            closeButton.MouseEnter += (s, e) => {
                closeButton.Background = new SolidColorBrush(Color.FromRgb(95, 105, 119));
            };
            closeButton.MouseLeave += (s, e) => {
                closeButton.Background = new SolidColorBrush(Color.FromRgb(75, 85, 99));
            };
            
            closeButton.Click += (s, e) => dialog.Close();
            stackPanel.Children.Add(closeButton);

            scrollViewer.Content = stackPanel;
            mainBorder.Child = scrollViewer;
            dialog.Content = mainBorder;

            dialog.ShowDialog();
        }
        
        // 添加使用说明内容
        private void AddUserManualContent(StackPanel container)
        {
            if (isEnglishMode)
            {
                // 英文版使用说明
                AddMainSection(container, "1. Introduction", 
                    "UE MOD Manager is a powerful tool designed to help you manage mods for Unreal Engine games. " +
                    "It supports various games including Stellar Blade, Black Myth: Wukong, Enshrouded, and other UE games.");
                
                AddMainSection(container, "2. Interface Overview", 
                    "The interface is divided into several sections:");
                    
                AddSubSection(container, "2.1 Section A: Top Navigation", 
                    "• Game selection dropdown\n" +
                    "• Import MOD button\n" + 
                    "• Launch Game button\n" +
                    "• Settings and other utility buttons");
                    
                AddSubSection(container, "2.2 Section B: Left Sidebar", 
                    "• Category management\n" +
                    "• Add/Delete/Rename categories\n" +
                    "• Filter MODs by category");
                    
                AddSubSection(container, "2.3 Section C: Main Content", 
                    "• MOD list (card or list view)\n" +
                    "• MOD details and preview\n" +
                    "• Enable/Disable controls");
                    
                AddSubSection(container, "2.4 Section D: Status Bar", 
                    "• System information\n" +
                    "• MOD statistics\n" +
                    "• Storage usage");
                
                AddMainSection(container, "3. Getting Started", null);
                
                AddSubSection(container, "3.1 Select a Game", 
                    "Choose your game from the dropdown menu at the top of the interface.");
                    
                AddSubSection(container, "3.2 Configure Game Paths", 
                    "When prompted, set the paths for:\n" +
                    "• Game installation directory\n" +
                    "• MOD storage directory\n" +
                    "• Backup directory (optional)");
                    
                AddSubSection(container, "3.3 Import MODs", 
                    "Click the Import MOD button to add mods from files or folders.");
                
                AddMainSection(container, "4. Managing MODs", null);
                
                AddSubSection(container, "4.1 Import MODs", 
                    "• Click the Import MOD button\n" +
                    "• Select MOD files or folders\n" +
                    "• Supported formats: .pak, .ucas, .utoc, .zip, .rar, .7z\n" +
                    "• MODs will be added to your library");
                
                AddSubSection(container, "4.2 Enable/Disable MODs", 
                    "• Click the toggle switch next to a MOD\n" +
                    "• Use the Enable All or Disable All buttons for batch operations\n" +
                    "• Filter by enabled/disabled status using the filter buttons");
                
                AddSubSection(container, "4.3 Delete MODs", 
                    "• Select one or more MODs\n" +
                    "• Click the Delete button\n" +
                    "• Confirm deletion when prompted");
                
                AddMainSection(container, "5. Category Management", null);
                
                AddSubSection(container, "5.1 Create Categories", 
                    "• Click the + button in the category area\n" +
                    "• Enter a name for the new category\n" +
                    "• Choose whether to create a root category or subcategory");
                
                AddSubSection(container, "5.2 Move MODs Between Categories", 
                    "• Right-click on a MOD\n" +
                    "• Select Move to Category\n" +
                    "• Choose the destination category");
                
                AddSubSection(container, "5.3 Manage Categories", 
                    "• Rename: Select a category and click the rename button\n" +
                    "• Delete: Select a category and click the delete button\n" +
                    "• Reorder: Drag and drop categories to change their order");
                
                AddMainSection(container, "6. Tips and Tricks", 
                    "• Use the search box to quickly find MODs\n" +
                    "• Filter MODs by enabled/disabled status\n" +
                    "• Use keyboard shortcuts (Ctrl+Click, Shift+Click) for multiple selection\n" +
                    "• Regularly backup your MODs and game saves\n" +
                    "• Switch between card view and list view for different browsing experiences");
            }
            else
            {
                // 中文版使用说明
                AddMainSection(container, "1. 简介", 
                    "虚幻引擎MOD管理器是一款强大的工具，专为帮助您管理虚幻引擎游戏的MOD而设计。" +
                    "它支持多种游戏，包括剑星、黑神话·悟空、光与影：33号远征队以及其他虚幻引擎游戏。");
                
                AddMainSection(container, "2. 界面概览", 
                    "界面分为几个主要区域：");
                    
                AddSubSection(container, "2.1 A区：顶部导航", 
                    "• 游戏选择下拉菜单\n" +
                    "• 导入MOD按钮\n" + 
                    "• 启动游戏按钮\n" +
                    "• 设置和其他实用工具按钮");
                    
                AddSubSection(container, "2.2 B区：左侧边栏", 
                    "• 分类管理\n" +
                    "• 添加/删除/重命名分类\n" +
                    "• 按分类筛选MOD");
                    
                AddSubSection(container, "2.3 C区：主内容区", 
                    "• MOD列表（卡片或列表视图）\n" +
                    "• MOD详情和预览\n" +
                    "• 启用/禁用控件");
                    
                AddSubSection(container, "2.4 D区：状态栏", 
                    "• 系统信息\n" +
                    "• MOD统计数据\n" +
                    "• 存储使用情况");
                
                AddMainSection(container, "3. 开始使用", null);
                
                AddSubSection(container, "3.1 选择游戏", 
                    "从界面顶部的下拉菜单中选择您的游戏。");
                    
                AddSubSection(container, "3.2 配置游戏路径", 
                    "根据提示设置以下路径：\n" +
                    "• 游戏安装目录\n" +
                    "• MOD存储目录\n" +
                    "• 备份目录（可选）");
                    
                AddSubSection(container, "3.3 导入MOD", 
                    "点击导入MOD按钮从文件或文件夹添加模组。");
                
                AddMainSection(container, "4. 管理MOD", null);
                
                AddSubSection(container, "4.1 导入MOD", 
                    "• 点击导入MOD按钮\n" +
                    "• 选择MOD文件或文件夹\n" +
                    "• 支持的格式：.pak、.ucas、.utoc、.zip、.rar、.7z\n" +
                    "• MOD将被添加到您的库中");
                
                AddSubSection(container, "4.2 启用/禁用MOD", 
                    "• 点击MOD旁边的开关\n" +
                    "• 使用启用全部或禁用全部按钮进行批量操作\n" +
                    "• 使用过滤按钮按启用/禁用状态筛选");
                
                AddSubSection(container, "4.3 删除MOD", 
                    "• 选择一个或多个MOD\n" +
                    "• 点击删除按钮\n" +
                    "• 确认删除");
                
                AddMainSection(container, "5. 分类管理", null);
                
                AddSubSection(container, "5.1 创建分类", 
                    "• 点击分类区域中的+按钮\n" +
                    "• 输入新分类的名称\n" +
                    "• 选择创建根分类或子分类");
                
                AddSubSection(container, "5.2 将MOD移动到分类", 
                    "• 右键点击MOD\n" +
                    "• 选择移动到分类\n" +
                    "• 选择目标分类");
                
                AddSubSection(container, "5.3 管理分类", 
                    "• 重命名：选择一个分类并点击重命名按钮\n" +
                    "• 删除：选择一个分类并点击删除按钮\n" +
                    "• 重排序：拖放分类来改变它们的顺序");
                
                AddMainSection(container, "6. 使用技巧", 
                    "• 使用搜索框快速查找MOD\n" +
                    "• 通过启用/禁用状态过滤MOD\n" +
                    "• 使用键盘快捷键（Ctrl+点击、Shift+点击）进行多选\n" +
                    "• 定期备份您的MOD和游戏存档\n" +
                    "• 在卡片视图和列表视图之间切换以获得不同的浏览体验");
            }
        }
        
        // 添加一个章节到使用说明
        private void AddSection(StackPanel container, string title, string content)
        {
            // 添加标题
            var titleBlock = new TextBlock
            {
                Text = title,
                Foreground = Brushes.White,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 15, 0, 5)
            };
            container.Children.Add(titleBlock);
            
            // 添加分隔线
            var separator = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(75, 85, 99)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Margin = new Thickness(0, 0, 0, 10)
            };
            container.Children.Add(separator);
            
            // 添加内容
            var contentBlock = new TextBlock
            {
                Text = content,
                Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(10, 0, 0, 15)
            };
            container.Children.Add(contentBlock);
        }
        
        // 添加一个主要章节到使用说明
        private void AddMainSection(StackPanel container, string title, string? content)
        {
            // 添加标题
            var titleBlock = new TextBlock
            {
                Text = title,
                Foreground = Brushes.White,
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 25, 0, 8)
            };
            container.Children.Add(titleBlock);
            
            // 添加分隔线
            var separator = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(75, 85, 99)),
                BorderThickness = new Thickness(0, 0, 0, 2),
                Margin = new Thickness(0, 0, 0, 10)
            };
            container.Children.Add(separator);
            
            // 如果有内容，则添加内容
            if (!string.IsNullOrEmpty(content))
            {
                // 添加内容
                var contentBlock = new TextBlock
                {
                    Text = content,
                    Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                    FontSize = 15,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(10, 0, 0, 10)
                };
                container.Children.Add(contentBlock);
            }
        }
        
        // 添加一个子章节到使用说明
        private void AddSubSection(StackPanel container, string title, string content)
        {
            // 添加标题
            var titleBlock = new TextBlock
            {
                Text = title,
                Foreground = Brushes.White,
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(20, 15, 0, 5)
            };
            container.Children.Add(titleBlock);
            
            // 添加分隔线
            var separator = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(75, 85, 99)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Margin = new Thickness(20, 0, 0, 5),
                Width = 200,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            container.Children.Add(separator);
            
            // 添加内容
            var contentBlock = new TextBlock
            {
                Text = content,
                Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(30, 0, 0, 15)
            };
            container.Children.Add(contentBlock);
        }

        // 语言切换功能实现 - 全局切换
        private void UpdateLanguage()
        {
            try
            {
                if (isEnglishMode)
                {
                    // 切换到英文
                    this.Title = "UE MOD Manager";
                    
                    // 更新主界面UI元素
                    if (SelectAllCheckBox != null) SelectAllCheckBox.Content = "Select All";
                    if (ImportModBtn != null) ImportModBtn.Content = "📥 Import MOD";
                    if (ImportModBtn2 != null) ImportModBtn2.Content = "📥 Import MOD";
                    if (LaunchGameBtn != null) LaunchGameBtn.Content = "▶️ Launch Game";
                    
                    // 更新游戏选择器中的选项
                    UpdateGameSelectorLanguage();
                    
                    // 更新搜索框占位符
                    UpdateSearchPlaceholder();
                    
                    // 更新过滤按钮
                    if (EnabledFilterBtn != null) EnabledFilterBtn.Content = "Enabled";
                    if (DisabledFilterBtn != null) DisabledFilterBtn.Content = "Disabled";
                    
                    // 更新操作按钮
                    UpdateOperationButtonsLanguage();
                    
                    // 更新右侧详情面板
                    UpdateDetailsPanelLanguage();
                    
                    // 更新分类相关文本
                    foreach (var category in categories)
                    {
                        switch (category.Name)
                        {
                            case "全部": category.Name = "All"; break;
                            case "已启用": category.Name = "Enabled"; break;
                            case "已禁用": category.Name = "Disabled"; break;
                            case "未分类": category.Name = "Uncategorized"; break;
                            case "服装": category.Name = "Outfits"; break;
                            case "其他": category.Name = "Others"; break;
                            case "人物": category.Name = "Characters"; break;
                            case "面部": category.Name = "Face"; break;
                            case "武器": category.Name = "Weapons"; break;
                            case "修改": category.Name = "Modifications"; break;
                        }
                    }
                    
                    // 更新MOD状态和类型文本
                    foreach (var mod in allMods)
                    {
                        // 状态翻译
                        if (mod.Status == "已启用") mod.Status = "Enabled";
                        else if (mod.Status == "已禁用") mod.Status = "Disabled";
                        
                        // 类型翻译
                        switch (mod.Type)
                        {
                            case "服装": mod.Type = "Outfits"; break;
                            case "其他": mod.Type = "Others"; break;
                            case "人物": mod.Type = "Characters"; break;
                            case "面部": mod.Type = "Face"; break;
                            case "武器": mod.Type = "Weapons"; break;
                            case "修改": mod.Type = "Modifications"; break;
                        }
                        
                        // 描述翻译
                        if (string.IsNullOrEmpty(mod.Description) || mod.Description == "暂无描述")
                        {
                            mod.Description = "No description available";
                        }
                    }
                }
                else
                {
                    // 切换到中文
                    this.Title = "爱酱MOD管理器";
                    
                    // 更新主界面UI元素
                    if (SelectAllCheckBox != null) SelectAllCheckBox.Content = "全选";
                    if (ImportModBtn != null) ImportModBtn.Content = "📥 导入MOD";
                    if (ImportModBtn2 != null) ImportModBtn2.Content = "📥 导入MOD";
                    if (LaunchGameBtn != null) LaunchGameBtn.Content = "▶️ 启动游戏";
                    
                    // 更新游戏选择器中的选项
                    UpdateGameSelectorLanguage();
                    
                    // 更新搜索框占位符
                    UpdateSearchPlaceholder();
                    
                    // 更新过滤按钮
                    if (EnabledFilterBtn != null) EnabledFilterBtn.Content = "已启用";
                    if (DisabledFilterBtn != null) DisabledFilterBtn.Content = "已禁用";
                    
                    // 更新操作按钮
                    UpdateOperationButtonsLanguage();
                    
                    // 更新右侧详情面板
                    UpdateDetailsPanelLanguage();
                    
                    // 更新分类相关文本
                    foreach (var category in categories)
                    {
                        switch (category.Name)
                        {
                            case "All": category.Name = "全部"; break;
                            case "Enabled": category.Name = "已启用"; break;
                            case "Disabled": category.Name = "已禁用"; break;
                            case "Uncategorized": category.Name = "未分类"; break;
                            case "Outfits": category.Name = "服装"; break;
                            case "Others": category.Name = "其他"; break;
                            case "Characters": category.Name = "人物"; break;
                            case "Face": category.Name = "面部"; break;
                            case "Weapons": category.Name = "武器"; break;
                            case "Modifications": category.Name = "修改"; break;
                        }
                    }
                    
                    // 更新MOD状态和类型文本
                    foreach (var mod in allMods)
                    {
                        // 状态翻译
                        if (mod.Status == "Enabled") mod.Status = "已启用";
                        else if (mod.Status == "Disabled") mod.Status = "已禁用";
                        
                        // 类型翻译
                        switch (mod.Type)
                        {
                            case "Outfits": mod.Type = "服装"; break;
                            case "Others": mod.Type = "其他"; break;
                            case "Characters": mod.Type = "人物"; break;
                            case "Face": mod.Type = "面部"; break;
                            case "Weapons": mod.Type = "武器"; break;
                            case "Modifications": mod.Type = "修改"; break;
                        }
                        
                        // 描述翻译
                        if (string.IsNullOrEmpty(mod.Description) || mod.Description == "No description available")
                        {
                            mod.Description = "暂无描述";
                        }
                    }
                }
                
                // 刷新显示
                RefreshModDisplay();
                RefreshCategoryDisplay();
                UpdateModCountDisplay();
                
                // 更新设置菜单文本
                UpdateSettingsMenuLanguage();
                
                // 更新剑星专属按钮
                UpdateStellarButtonLanguage();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"更新语言失败: {ex.Message}");
            }
        }
        
        // 更新游戏专属按钮的语言显示
        private void UpdateStellarButtonLanguage()
        {
            try
            {
                // 根据当前游戏类型设置按钮文本
                if (currentGameType == GameType.StellarBlade)
                {
                    // 剑星游戏按钮文本
                    if (CollectionToolButton != null)
                    {
                        CollectionToolButton.Content = isEnglishMode ? "📋 Collection Tools" : "📋 收集工具箱";
                        CollectionToolButton.ToolTip = isEnglishMode ? "Stellar Blade Collection Tools" : "剑星收集工具";
                    }
                    
                    if (StellarModCollectionButton != null)
                    {
                        StellarModCollectionButton.Content = isEnglishMode ? "🗂️ Stellar MOD Collection" : "🗂️ 剑星MOD合集";
                        StellarModCollectionButton.ToolTip = isEnglishMode ? "Access Stellar Blade MOD cloud collection" : "访问剑星MOD云盘合集";
                    }
                }
                else if (currentGameType == GameType.Enshrouded)
                {
                    // 光与影游戏按钮文本
                    if (StellarModCollectionButton != null)
                    {
                        StellarModCollectionButton.Content = isEnglishMode ? "🗂️ Enshrouded MOD Collection" : "🗂️ 光与影MOD";
                        StellarModCollectionButton.ToolTip = isEnglishMode ? "Access Enshrouded MOD cloud collection" : "访问光与影MOD云盘合集";
                    }
                }
                else if (currentGameType == GameType.BlackMythWukong)
                {
                    // 黑神话悟空游戏按钮文本
                    if (StellarModCollectionButton != null)
                    {
                        StellarModCollectionButton.Content = isEnglishMode ? "🗂️ Black Myth MOD" : "🗂️ 黑猴MOD";
                        StellarModCollectionButton.ToolTip = isEnglishMode ? "Access Black Myth: Wukong MOD cloud collection" : "访问黑神话悟空MOD云盘合集";
                    }
                }
                
                // 更新收集工具子菜单（仅剑星游戏使用）
                if (CollectionToolMenu != null && currentGameType == GameType.StellarBlade)
                {
                    foreach (MenuItem item in CollectionToolMenu.Items)
                    {
                        switch (item.Header?.ToString())
                        {
                            case "物品收集":
                            case "Item Collection":
                                item.Header = isEnglishMode ? "Item Collection" : "物品收集";
                                break;
                            case "衣服收集":
                            case "Clothing Collection":
                                item.Header = isEnglishMode ? "Clothing Collection" : "衣服收集";
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"更新游戏专属按钮语言失败: {ex.Message}");
            }
        }

        // 更新设置菜单的语言
        private void UpdateSettingsMenuLanguage()
        {
            try
            {
                if (SettingsContextMenu != null)
                {
                    // 检查是否已经存在使用说明菜单项
                    bool hasUserManualItem = false;
                    foreach (MenuItem item in SettingsContextMenu.Items)
                    {
                        if (item.Header?.ToString() == "使用说明" || item.Header?.ToString() == "User Manual")
                        {
                            hasUserManualItem = true;
                            break;
                        }
                    }
                    
                    // 如果不存在，添加使用说明菜单项
                    if (!hasUserManualItem)
                    {
                        MenuItem userManualItem = new MenuItem
                        {
                            Header = isEnglishMode ? "User Manual" : "使用说明",
                            Icon = new TextBlock { Text = "📖", FontSize = 14 }
                        };
                        userManualItem.Click += UserManualMenuItem_Click;
                        
                        // 添加到菜单的第二个位置（路径设置后面）
                        if (SettingsContextMenu.Items.Count > 0)
                        {
                            SettingsContextMenu.Items.Insert(1, userManualItem);
                        }
                        else
                        {
                            SettingsContextMenu.Items.Add(userManualItem);
                        }
                    }
                    
                    // 更新所有菜单项的语言
                    foreach (MenuItem item in SettingsContextMenu.Items)
                    {
                        if (isEnglishMode)
                        {
                            switch (item.Header?.ToString())
                            {
                                case "路径设置": item.Header = "Path Settings"; break;
                                case "使用说明": item.Header = "User Manual"; break;
                                case "切换英文 (Language)": item.Header = "切换中文 (Language)"; break;
                                case "关于爱酱MOD管理器": item.Header = "About UE MOD Manager"; break;
                                case "检查更新": item.Header = "Check Updates"; break;
                            }
                        }
                        else
                        {
                            switch (item.Header?.ToString())
                            {
                                case "Path Settings": item.Header = "路径设置"; break;
                                case "User Manual": item.Header = "使用说明"; break;
                                case "切换中文 (Language)": item.Header = "切换英文 (Language)"; break;
                                case "About UE MOD Manager": item.Header = "关于爱酱MOD管理器"; break;
                                case "Check Updates": item.Header = "检查更新"; break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"更新设置菜单语言失败: {ex.Message}");
            }
        }

        // 更新游戏选择器的语言
        private void UpdateGameSelectorLanguage()
        {
            try
            {
                if (GameList != null)
                {
                    var savedSelection = GameList.SelectedIndex;
                    
                    // 暂时取消事件监听以避免触发SelectionChanged
                    GameList.SelectionChanged -= GameList_SelectionChanged;
                    
                    for (int i = 0; i < GameList.Items.Count; i++)
                    {
                        if (GameList.Items[i] is ComboBoxItem item)
                        {
                            if (isEnglishMode)
                            {
                                switch (item.Content?.ToString())
                                {
                                    case "请选择游戏": item.Content = "Please Select Game"; break;
                                    case "剑星 (Stellar Blade)": item.Content = "Stellar Blade"; break;
                                    case "黑神话·悟空": item.Content = "Black Myth: Wukong"; break;
                                    case "光与影：33号远征队": item.Content = "Enshrouded"; break;
                                    case "其他虚幻引擎游戏": item.Content = "Other UE Games"; break;
                                }
                            }
                            else
                            {
                                switch (item.Content?.ToString())
                                {
                                    case "Please Select Game": item.Content = "请选择游戏"; break;
                                    case "Stellar Blade": item.Content = "剑星 (Stellar Blade)"; break;
                                    case "Black Myth: Wukong": item.Content = "黑神话·悟空"; break;
                                    case "Enshrouded": item.Content = "光与影：33号远征队"; break;
                                    case "Other UE Games": item.Content = "其他虚幻引擎游戏"; break;
                                }
                            }
                        }
                    }
                    
                    // 恢复选择状态和事件监听
                    GameList.SelectedIndex = savedSelection;
                    GameList.SelectionChanged += GameList_SelectionChanged;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"更新游戏选择器语言失败: {ex.Message}");
            }
        }

        // 更新搜索框占位符
        private void UpdateSearchPlaceholder()
        {
            try
            {
                if (SearchPlaceholder != null)
                {
                    if (isEnglishMode)
                    {
                        SearchPlaceholder.Text = "Enter MOD name or description keywords...";
                    }
                    else
                    {
                        SearchPlaceholder.Text = "输入MOD名称或描述关键词...";
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"更新搜索框占位符失败: {ex.Message}");
            }
        }

        // 更新操作按钮的语言
        private void UpdateOperationButtonsLanguage()
        {
            try
            {
                // 查找XAML中的按钮并更新其文本
                var mainGrid = this.Content as Grid;
                if (mainGrid != null)
                {
                    // 查找所有按钮并更新
                    UpdateButtonTextInVisualTree(mainGrid);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"更新操作按钮语言失败: {ex.Message}");
            }
        }

        // 递归更新可视化树中的按钮文本
        private void UpdateButtonTextInVisualTree(DependencyObject parent)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is Button button)
                {
                    if (isEnglishMode)
                    {
                        switch (button.Content?.ToString())
                        {
                            case "新增": button.Content = "Add"; break;
                            case "删除": button.Content = "Delete"; break;
                            case "重命名": button.Content = "Rename"; break;
                            case "🚫 禁用全部": button.Content = "🚫 Disable All"; break;
                            case "✅ 启用全部": button.Content = "✅ Enable All"; break;
                            case "🗑️ 删除所选": button.Content = "🗑️ Delete Selected"; break;
                        }
                    }
                    else
                    {
                        switch (button.Content?.ToString())
                        {
                            case "Add": button.Content = "新增"; break;
                            case "Delete": button.Content = "删除"; break;
                            case "Rename": button.Content = "重命名"; break;
                            case "🚫 Disable All": button.Content = "🚫 禁用全部"; break;
                            case "✅ Enable All": button.Content = "✅ 启用全部"; break;
                            case "🗑️ Delete Selected": button.Content = "🗑️ 删除所选"; break;
                        }
                    }
                }
                else if (child is TextBlock textBlock)
                {
                    // 更新特定的TextBlock
                    if (isEnglishMode)
                    {
                        switch (textBlock.Text)
                        {
                            case "虚幻引擎MOD管理器": textBlock.Text = "Unreal Engine MOD Manager"; break;
                            case "MOD分类": textBlock.Text = "MOD Categories"; break;
                        }
                    }
                    else
                    {
                        switch (textBlock.Text)
                        {
                            case "Unreal Engine MOD Manager": textBlock.Text = "轻松管理你的游戏MOD"; break;
                            case "MOD Categories": textBlock.Text = "MOD分类"; break;
                        }
                    }
                }
                
                // 递归处理子元素
                UpdateButtonTextInVisualTree(child);
            }
        }

        // 更新右侧详情面板的语言
        private void UpdateDetailsPanelLanguage()
        {
            try
            {
                // 查找右侧面板中的所有TextBlock和Button
                var mainGrid = this.Content as Grid;
                if (mainGrid != null)
                {
                    UpdateDetailsPanelInVisualTree(mainGrid);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"更新详情面板语言失败: {ex.Message}");
            }
        }

        // 递归更新详情面板中的元素
        private void UpdateDetailsPanelInVisualTree(DependencyObject parent)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is TextBlock textBlock)
                {
                    if (isEnglishMode)
                    {
                        switch (textBlock.Text)
                        {
                            case "状态:": textBlock.Text = "Status:"; break;
                            case "原始名称:": textBlock.Text = "Original Name:"; break;
                            case "导入日期:": textBlock.Text = "Import Date:"; break;
                            case "文件大小:": textBlock.Text = "File Size:"; break;
                            case "描述:": textBlock.Text = "Description:"; break;
                            case "未选择": textBlock.Text = "Not Selected"; break;
                            case "请选择一个MOD查看详情": textBlock.Text = "Please select a MOD to view details"; break;
                            case "✏️ 重命名": textBlock.Text = "✏️ Rename"; break;
                            case "🖼️ 修改预览图": textBlock.Text = "🖼️ Change Preview"; break;
                            case "⛔ 禁用MOD": textBlock.Text = "⛔ Disable MOD"; break;
                            case "🗑️ 删除MOD": textBlock.Text = "🗑️ Delete MOD"; break;
                        }
                    }
                    else
                    {
                        switch (textBlock.Text)
                        {
                            case "Status:": textBlock.Text = "状态:"; break;
                            case "Original Name:": textBlock.Text = "原始名称:"; break;
                            case "Import Date:": textBlock.Text = "导入日期:"; break;
                            case "File Size:": textBlock.Text = "文件大小:"; break;
                            case "Description:": textBlock.Text = "描述:"; break;
                            case "Not Selected": textBlock.Text = "未选择"; break;
                            case "Please select a MOD to view details": textBlock.Text = "请选择一个MOD查看详情"; break;
                            case "✏️ Rename": textBlock.Text = "✏️ 重命名"; break;
                            case "🖼️ Change Preview": textBlock.Text = "🖼️ 修改预览图"; break;
                            case "⛔ Disable MOD": textBlock.Text = "⛔ 禁用MOD"; break;
                            case "🗑️ Delete MOD": textBlock.Text = "🗑️ 删除MOD"; break;
                        }
                    }
                }
                
                // 递归处理子元素
                UpdateDetailsPanelInVisualTree(child);
            }
        }

        // 关于对话框功能
        private void ShowAboutDialog()
        {
            try
            {
                // 创建自定义对话框
                var dialog = new Window
                {
                    Title = "关于爱酱MOD管理器",
                    Width = 500,
                    Height = 600,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                    ResizeMode = ResizeMode.NoResize,
                    Background = new SolidColorBrush(Color.FromRgb(15, 27, 46)),
                    WindowStyle = WindowStyle.ToolWindow
                };

                var mainPanel = new StackPanel
                {
                    Margin = new Thickness(20),
                    Orientation = Orientation.Vertical
                };

                // 标题
                var titleText = new TextBlock
                {
                    Text = "爱酱剑星MOD管理器 v1.7",
                    FontSize = 20,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(0, 212, 170)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 10)
                };

                // 免费声明
                var freeText = new TextBlock
                {
                    Text = "本管理器完全免费",
                    FontSize = 14,
                    Foreground = new SolidColorBrush(Colors.White),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 20)
                };

                // B站链接
                var biliText = new TextBlock
                {
                    FontSize = 14,
                    Foreground = new SolidColorBrush(Colors.White),
                    Margin = new Thickness(0, 0, 0, 10)
                };
                biliText.Inlines.Add(new Run("B站: "));
                var biliLink = new Hyperlink(new Run("空竹竹竹"))
                {
                    NavigateUri = new Uri("https://space.bilibili.com/232926208"),
                    Foreground = new SolidColorBrush(Color.FromRgb(0, 212, 170))
                };
                biliLink.RequestNavigate += (s, e) => {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = e.Uri.ToString(),
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        ShowCustomMessageBox($"无法打开链接: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                };
                biliText.Inlines.Add(biliLink);

                // QQ群链接
                var qqText = new TextBlock
                {
                    FontSize = 14,
                    Foreground = new SolidColorBrush(Colors.White),
                    Margin = new Thickness(0, 0, 0, 10)
                };
                qqText.Inlines.Add(new Run("QQ群: "));
                var qqLink = new Hyperlink(new Run("682707942"))
                {
                    NavigateUri = new Uri("https://qm.qq.com/q/sYnTmQRdOo"),
                    Foreground = new SolidColorBrush(Color.FromRgb(0, 212, 170))
                };
                qqLink.RequestNavigate += (s, e) => {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = e.Uri.ToString(),
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        ShowCustomMessageBox($"无法打开链接: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                };
                qqText.Inlines.Add(qqLink);

                // 欢迎文本
                var welcomeText = new TextBlock
                {
                    Text = "欢迎加入QQ群获取最新MOD和反馈建议!",
                    FontSize = 14,
                    Foreground = new SolidColorBrush(Colors.White),
                    Margin = new Thickness(0, 0, 0, 20),
                    TextWrapping = TextWrapping.Wrap
                };

                // 捐赠图片区域
                var donationImage = CreateDonationImageControl();

                // 捐赠文本
                var donationText = new TextBlock
                {
                    Text = "如果对你有帮助，可以请我喝一杯蜜雪冰城~",
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Colors.LightGray),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 20),
                    TextWrapping = TextWrapping.Wrap
                };

                // 感谢名单
                var thanksText = new TextBlock
                {
                    Text = "捐赠感谢:\n胖虎、YUki\n春告鳥、蘭\n神秘不保底男\n文铭、阪、林墨\nDaisuke、虎子哥\n爱酱游戏群全体群友",
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Colors.LightGray),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 20),
                    TextWrapping = TextWrapping.Wrap
                };

                // 关闭按钮
                var closeButton = new Button
                {
                    Content = "关闭",
                    Width = 100,
                    Height = 35,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Background = new SolidColorBrush(Color.FromRgb(0, 212, 170)),
                    Foreground = new SolidColorBrush(Colors.White),
                    BorderThickness = new Thickness(0),
                    FontSize = 14
                };
                closeButton.Click += (s, e) => dialog.Close();

                // 组装界面
                mainPanel.Children.Add(titleText);
                mainPanel.Children.Add(freeText);
                mainPanel.Children.Add(biliText);
                mainPanel.Children.Add(qqText);
                mainPanel.Children.Add(welcomeText);
                mainPanel.Children.Add(donationImage);
                mainPanel.Children.Add(donationText);
                mainPanel.Children.Add(thanksText);
                mainPanel.Children.Add(closeButton);

                dialog.Content = mainPanel;
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                ShowCustomMessageBox($"显示关于对话框失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 创建捐赠图片控件
        private Border CreateDonationImageControl()
        {
            try
            {
                // 使用嵌入式资源加载捐赠图片
                var resourceUri = new Uri("pack://application:,,,/UEModManager;component/捐赠.png", UriKind.Absolute);
                var imageSource = new BitmapImage(resourceUri);
                
                var image = new Image
                {
                    Source = imageSource,
                Width = 200,
                    Height = 200,
                    Stretch = Stretch.Uniform
                };
                
                return new Border
                {
                    Width = 200,
                    Height = 200,
                    CornerRadius = new CornerRadius(10),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 10),
                    ClipToBounds = true,
                    Child = image
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"创建捐赠图片控件失败: {ex.Message}");
                
                // 出错时返回简单占位符
                return new Border
                {
                    Width = 200,
                    Height = 200,
                    Background = new SolidColorBrush(Color.FromRgb(26, 52, 77)),
                    CornerRadius = new CornerRadius(10),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 10),
                    Child = new StackPanel
                    {
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = "💰",
                                FontSize = 32,
                                Foreground = new SolidColorBrush(Colors.White),
                                HorizontalAlignment = HorizontalAlignment.Center,
                                Margin = new Thickness(0, 0, 0, 10)
                            },
                            new TextBlock
                            {
                                Text = "捐赠二维码",
                FontSize = 14,
                                Foreground = new SolidColorBrush(Colors.White),
                                HorizontalAlignment = HorizontalAlignment.Center,
                                TextAlignment = TextAlignment.Center
                            },
                            new TextBlock
                            {
                                Text = "(请将捐赠.png添加为资源)",
                                FontSize = 10,
                                Foreground = new SolidColorBrush(Colors.LightGray),
                                HorizontalAlignment = HorizontalAlignment.Center,
                                TextAlignment = TextAlignment.Center,
                                Margin = new Thickness(0, 5, 0, 0)
                            }
                        }
                    }
                };
            }
        }

        // 检查更新功能
        private async void CheckForUpdates()
        {
            try
            {
                ShowCustomMessageBox("正在检查更新，请稍候...", "检查更新", MessageBoxButton.OK, MessageBoxImage.Information);
                
                // 使用GitHub API检查最新版本
                using (var client = new System.Net.Http.HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "UEModManager");
                    
                    var response = await client.GetStringAsync("https://api.github.com/repos/velist/StellarBladeModManager/releases/latest");
                    
                    // 解析JSON响应
                    var jsonDoc = System.Text.Json.JsonDocument.Parse(response);
                    var root = jsonDoc.RootElement;
                    
                    var latestVersion = root.GetProperty("tag_name").GetString();
                    var downloadUrl = root.GetProperty("html_url").GetString();
                    var releaseNotes = root.GetProperty("body").GetString();
                    
                    var currentVersion = "v1.9"; // 当前版本号
                    
                    if (latestVersion != currentVersion)
                    {
                        var updateMessage = $"发现新版本！\n\n" +
                                          $"当前版本: {currentVersion}\n" +
                                          $"最新版本: {latestVersion}\n\n" +
                                          $"更新内容:\n{releaseNotes}\n\n" +
                                          $"是否打开下载页面？";
                        
                        var result = ShowCustomMessageBox(updateMessage, "发现新版本", MessageBoxButton.YesNo, MessageBoxImage.Information);
                        
                        if (result == MessageBoxResult.Yes && !string.IsNullOrEmpty(downloadUrl))
                        {
                            try
                            {
                                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                {
                                    FileName = downloadUrl,
                                    UseShellExecute = true
                                });
                            }
                            catch (Exception ex)
                            {
                                ShowCustomMessageBox($"无法打开下载页面: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                    }
                    else
                    {
                        ShowCustomMessageBox("当前已是最新版本！", "检查更新", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (System.Net.Http.HttpRequestException)
            {
                ShowUpdateFailedDialog();
            }
            catch (Exception ex)
            {
                ShowUpdateFailedDialog();
            }
        }

        // 显示更新失败对话框，提供QQ群链接
        private void ShowUpdateFailedDialog()
        {
            try
            {
                // 创建自定义对话框
                var dialog = new Window
                {
                    Title = "检查更新失败",
                    Width = 400,
                    Height = 250,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                    ResizeMode = ResizeMode.NoResize,
                    Background = new SolidColorBrush(Color.FromRgb(15, 27, 46)),
                    WindowStyle = WindowStyle.ToolWindow
                };

                var mainPanel = new StackPanel
                {
                    Margin = new Thickness(20),
                    Orientation = Orientation.Vertical
                };

                // 错误信息
                var errorText = new TextBlock
                {
                    Text = "无法连接到更新服务器",
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Colors.Orange),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 20)
                };

                // 提示文本
                var hintText = new TextBlock
                {
                    Text = "请检查网络连接，或加入QQ群获取最新版本：",
                    FontSize = 14,
                    Foreground = new SolidColorBrush(Colors.White),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 15),
                TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center
                };

                // QQ群链接
                var qqGroupText = new TextBlock
                {
                    FontSize = 16,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 20)
                };
                var qqGroupLink = new Hyperlink(new Run("QQ群: 682707942"))
                {
                    NavigateUri = new Uri("https://qm.qq.com/q/sYnTmQRdOo"),
                    Foreground = new SolidColorBrush(Color.FromRgb(0, 212, 170)),
                    FontWeight = FontWeights.Bold
                };
                qqGroupLink.RequestNavigate += (s, e) => {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = e.Uri.ToString(),
                            UseShellExecute = true
                        });
                        dialog.Close();
                    }
                    catch (Exception ex)
                    {
                        ShowCustomMessageBox($"无法打开QQ群链接: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                };
                qqGroupText.Inlines.Add(qqGroupLink);

                // 按钮面板
                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                // 重试按钮
                var retryButton = new Button
                {
                    Content = "重试",
                    Width = 80,
                    Height = 35,
                    Margin = new Thickness(0, 0, 10, 0),
                    Background = new SolidColorBrush(Color.FromRgb(0, 212, 170)),
                    Foreground = new SolidColorBrush(Colors.White),
                    BorderThickness = new Thickness(0),
                    FontSize = 14
                };
                retryButton.Click += (s, e) => {
                    dialog.Close();
                    CheckForUpdates();
                };

                // 关闭按钮
                var closeButton = new Button
                {
                    Content = "关闭",
                    Width = 80,
                    Height = 35,
                    Background = new SolidColorBrush(Color.FromRgb(75, 85, 99)),
                    Foreground = new SolidColorBrush(Colors.White),
                    BorderThickness = new Thickness(0),
                    FontSize = 14
                };
                closeButton.Click += (s, e) => dialog.Close();

                buttonPanel.Children.Add(retryButton);
                buttonPanel.Children.Add(closeButton);

                // 组装界面
                mainPanel.Children.Add(errorText);
                mainPanel.Children.Add(hintText);
                mainPanel.Children.Add(qqGroupText);
                mainPanel.Children.Add(buttonPanel);

                dialog.Content = mainPanel;
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                ShowCustomMessageBox($"显示更新失败对话框出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ToggleModStatus(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                var mod = button?.Tag as Mod;
                if (mod != null)
                {
                    bool isCurrentlyEnabled = mod.Status == "已启用";
                    
                    if (isCurrentlyEnabled)
                    {
                        DisableMod(mod);
                    }
                    else
                    {
                        EnableMod(mod);
                    }
                    
                    RefreshModDisplay();
                    UpdateCategoryCount();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"切换MOD状态失败: {ex.Message}");
            }
        }

        // MOD卡片滑块开关点击事件
        private void ModToggle_MouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var border = sender as Border;
                var mod = border?.Tag as Mod;
                if (mod != null)
                {
                    bool isCurrentlyEnabled = mod.Status == "已启用";
                    
                    if (isCurrentlyEnabled)
                    {
                        DisableMod(mod);
                    }
                    else
                    {
                        EnableMod(mod);
                    }
                    
                    RefreshModDisplay();
                    UpdateCategoryCount();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"切换MOD状态失败: {ex.Message}");
                ShowCustomMessageBox($"切换MOD状态失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateModCountDisplay()
        {
            try
            {
                var filteredMods = GetFilteredMods();
                int totalCount = allMods.Count;
                int filteredCount = filteredMods.Count;
                int selectedCount = 0;
                
                // 获取当前视图中的选中项数量
                IEnumerable<Mod>? currentMods = null;
                if (ModsCardView.Visibility == Visibility.Visible)
                {
                    currentMods = ModsCardView.ItemsSource as IEnumerable<Mod>;
                }
                else
                {
                    currentMods = ModsListView.ItemsSource as IEnumerable<Mod>;
                }
                
                if (currentMods != null)
                {
                    selectedCount = currentMods.Count(m => m.IsSelected);
                }
                
                // 更新显示文本
                string countText = "";
                if (selectedCount > 0)
                {
                    countText = isEnglishMode 
                        ? $"MODs ({selectedCount} selected / {filteredCount} filtered / {totalCount} total)"
                        : $"全部 MOD (已选择 {selectedCount} / 筛选 {filteredCount} / 总计 {totalCount})";
                }
                else if (filteredCount != totalCount)
                {
                    countText = isEnglishMode 
                        ? $"MODs ({filteredCount} filtered / {totalCount} total)"
                        : $"全部 MOD (筛选 {filteredCount} / 总计 {totalCount})";
                }
                else
                {
                    countText = isEnglishMode 
                        ? $"All MODs ({totalCount})"
                        : $"全部 MOD ({totalCount})";
                }
                
                ModCountText.Text = countText;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 更新MOD计数显示失败: {ex.Message}");
            }
        }

        private void EnableMod(Mod mod)
        {
            try
            {
                Console.WriteLine($"[DEBUG] 开始启用MOD: {mod.Name} (RealName: {mod.RealName})");

                // 检查备份目录是否存在
                var modBackupDir = IOPath.Combine(currentBackupPath, mod.RealName);
                if (!Directory.Exists(modBackupDir))
                {
                    Console.WriteLine($"[ERROR] MOD备份目录不存在: {modBackupDir}");
                    
                    // 在批量操作中不显示单个MOD的消息框
                    if (!IsInBatchOperation)
                    {
                        ShowCustomMessageBox($"找不到MOD '{mod.Name}' 的备份文件。\n备份目录: {modBackupDir}", 
                            "启用失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    return;
                }

                // 获取备份目录中的所有MOD文件（排除预览图）
                var backupFiles = Directory.GetFiles(modBackupDir, "*.*", SearchOption.AllDirectories)
                    .Where(f => !IOPath.GetFileName(f).StartsWith("preview"))
                    .ToList();

                if (backupFiles.Count == 0)
                {
                    Console.WriteLine($"[ERROR] 备份目录中没有找到MOD文件: {modBackupDir}");
                    
                    // 在批量操作中不显示单个MOD的消息框
                    if (!IsInBatchOperation)
                    {
                        ShowCustomMessageBox($"MOD '{mod.Name}' 的备份目录中没有找到MOD文件。", 
                            "启用失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    return;
                }

                Console.WriteLine($"[DEBUG] 找到 {backupFiles.Count} 个备份文件");

                // 确保~mods目录存在
                if (!Directory.Exists(currentModPath))
                {
                    Directory.CreateDirectory(currentModPath);
                    Console.WriteLine($"[DEBUG] 创建MOD目录: {currentModPath}");
                }

                // 创建MOD专属子目录（使用RealName作为文件夹名）
                var modTargetDir = IOPath.Combine(currentModPath, mod.RealName);
                if (Directory.Exists(modTargetDir))
                {
                    // 如果目录已存在，先清空
                    Console.WriteLine($"[DEBUG] 清空现有MOD目录: {modTargetDir}");
                    Directory.Delete(modTargetDir, true);
                }
                Directory.CreateDirectory(modTargetDir);

                // 从备份目录复制所有文件到MOD目录
                int copiedCount = 0;
                foreach (var backupFile in backupFiles)
                {
                    // 计算相对于备份目录的路径
                    var relativePath = IOPath.GetRelativePath(modBackupDir, backupFile);
                    var targetFile = IOPath.Combine(modTargetDir, relativePath);

                    // 确保目标目录存在
                    var targetFileDir = IOPath.GetDirectoryName(targetFile);
                    if (!Directory.Exists(targetFileDir))
                    {
                        Directory.CreateDirectory(targetFileDir);
                    }

                    // 复制文件
                    File.Copy(backupFile, targetFile, true);
                    Console.WriteLine($"[DEBUG] 复制文件: {IOPath.GetFileName(backupFile)} -> {relativePath}");
                    copiedCount++;
                }

                // 更新MOD状态
                mod.Status = "已启用";
                
                Console.WriteLine($"[DEBUG] MOD '{mod.Name}' 启用成功，复制了 {copiedCount} 个文件到 {modTargetDir}");
                
                // 在批量操作中不显示单个MOD的消息框
                if (!IsInBatchOperation)
                {
                    ShowCustomMessageBox($"MOD '{mod.Name}' 已启用！\n复制了 {copiedCount} 个文件。", 
                        "启用成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 启用MOD失败: {ex.Message}");
                Console.WriteLine($"[ERROR] 堆栈跟踪: {ex.StackTrace}");
                
                // 在批量操作中不显示单个MOD的消息框
                if (!IsInBatchOperation)
                {
                    ShowCustomMessageBox($"启用MOD '{mod.Name}' 失败: {ex.Message}", 
                        "启用失败", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void DisableMod(Mod mod)
        {
            try
            {
                Console.WriteLine($"[DEBUG] 开始禁用MOD: {mod.Name} (RealName: {mod.RealName})");

                // 构建MOD目录路径（~mods/mod_real_name/）
                var modTargetDir = IOPath.Combine(currentModPath, mod.RealName);
                
                // 检查MOD目录是否存在
                if (!Directory.Exists(modTargetDir))
                {
                    Console.WriteLine($"[WARN] MOD目录不存在: {modTargetDir}");
                    // 即使目录不存在，也更新状态为已禁用
                    mod.Status = "已禁用";
                    
                    // 在批量操作中不显示单个MOD的消息框
                    if (!IsInBatchOperation)
                    {
                        ShowCustomMessageBox($"MOD '{mod.Name}' 已禁用。\n(游戏目录中未找到MOD文件)", 
                            "禁用完成", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    return;
                }

                // 验证备份是否存在（安全检查）
                var modBackupDir = IOPath.Combine(currentBackupPath, mod.RealName);
                if (!Directory.Exists(modBackupDir))
                {
                    Console.WriteLine($"[WARN] 备份目录不存在: {modBackupDir}，但继续禁用操作");
                }

                // 删除MOD目录及其所有内容
                Console.WriteLine($"[DEBUG] 删除MOD目录: {modTargetDir}");
                
                // 获取要删除的文件列表（用于显示）
                var filesToDelete = Directory.GetFiles(modTargetDir, "*.*", SearchOption.AllDirectories);
                Console.WriteLine($"[DEBUG] 将删除 {filesToDelete.Length} 个文件");

                // 删除整个MOD目录
                Directory.Delete(modTargetDir, true);

                // 更新MOD状态
                mod.Status = "已禁用";
                
                Console.WriteLine($"[DEBUG] MOD '{mod.Name}' 禁用成功，已删除 {filesToDelete.Length} 个文件");
                
                // 在批量操作中不显示单个MOD的消息框
                if (!IsInBatchOperation)
                {
                    ShowCustomMessageBox($"MOD '{mod.Name}' 已禁用！\n已删除 {filesToDelete.Length} 个文件。", 
                        "禁用成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"[ERROR] 禁用MOD失败 - 访问被拒绝: {ex.Message}");
                
                // 在批量操作中不显示单个MOD的消息框
                if (!IsInBatchOperation)
                {
                    ShowCustomMessageBox($"禁用MOD '{mod.Name}' 失败：文件被占用或权限不足。\n请关闭游戏后重试。", 
                        "禁用失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (DirectoryNotFoundException ex)
            {
                Console.WriteLine($"[WARN] MOD目录已不存在: {ex.Message}");
                mod.Status = "已禁用";
                
                // 在批量操作中不显示单个MOD的消息框
                if (!IsInBatchOperation)
                {
                    ShowCustomMessageBox($"MOD '{mod.Name}' 已禁用。\n(目录已不存在)", 
                        "禁用完成", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 禁用MOD失败: {ex.Message}");
                Console.WriteLine($"[ERROR] 堆栈跟踪: {ex.StackTrace}");
                
                // 在批量操作中不显示单个MOD的消息框
                if (!IsInBatchOperation)
                {
                    ShowCustomMessageBox($"禁用MOD '{mod.Name}' 失败: {ex.Message}", 
                        "禁用失败", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void UpdateCategoryCount()
        {
            try
            {
                // 更新旧版本分类的计数
                foreach (var category in categories)
                {
                    switch (category.Name)
                    {
                        case "全部":
                            category.Count = allMods.Count;
                            break;
                        case "已启用":
                            category.Count = allMods.Count(m => m.Status == "已启用");
                            break;
                        case "已禁用":
                            category.Count = allMods.Count(m => m.Status == "已禁用");
                            break;
                        default:
                            // 其他分类暂时设为0，后续可以根据MOD的分类属性来计算
                            category.Count = 0;
                            break;
                    }
                }
                
                Console.WriteLine($"[DEBUG] 更新分类计数完成: 全部={allMods.Count}, 已启用={allMods.Count(m => m.Status == "已启用")}, 已禁用={allMods.Count(m => m.Status == "已禁用")}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"更新分类计数失败: {ex.Message}");
            }
        }

        // 启动统计计时器
        private void StartStatsTimer()
        {
            try
            {
                statsTimer = new DispatcherTimer();
                statsTimer.Interval = TimeSpan.FromSeconds(2);
                statsTimer.Tick += (s, e) => {
                    // 更新统计信息
                    UpdateModCountDisplay();
                    UpdateStatusBarInfo();
                };
                statsTimer.Start();
                
                // 立即更新一次状态栏
                UpdateStatusBarInfo();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"启动统计计时器失败: {ex.Message}");
            }
        }

        // 更新底部状态栏信息
        private void UpdateStatusBarInfo()
        {
            try
            {
                // 更新MOD目录显示
                if (ModDirectoryText != null)
                {
                    if (!string.IsNullOrEmpty(currentModPath))
                    {
                        var displayPath = currentModPath;
                        if (displayPath.Length > 80)
                        {
                            displayPath = "..." + displayPath.Substring(displayPath.Length - 77);
                        }
                        
                        if (isEnglishMode)
                        {
                            ModDirectoryText.Text = $"MOD Directory: {displayPath}";
                        }
                        else
                        {
                            ModDirectoryText.Text = $"MOD目录: {displayPath}";
                        }
                    }
                    else
                    {
                        ModDirectoryText.Text = isEnglishMode ? "MOD Directory: Not Configured" : "MOD目录: 未配置";
                    }
                }

                // 更新系统信息
                if (SystemInfoText != null)
                {
                    var enabledCount = allMods.Count(m => m.Status == "已启用" || m.Status == "Enabled");
                    var totalCount = allMods.Count;
                    
                    // 计算MOD文件总大小（近似）
                    var totalSizeMB = CalculateModsSize();
                    
                    if (isEnglishMode)
                    {
                        SystemInfoText.Text = $"Loaded MODs: {enabledCount}/{totalCount} | Memory Usage: {totalSizeMB:F1}MB";
                    }
                    else
                    {
                        SystemInfoText.Text = $"已加载MOD: {enabledCount}/{totalCount} | 内存占用: {totalSizeMB:F1}MB";
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"更新状态栏信息失败: {ex.Message}");
            }
        }

        // 计算MOD文件总大小（MB）
        private double CalculateModsSize()
        {
            try
            {
                double totalBytes = 0;
                
                if (!string.IsNullOrEmpty(currentModPath) && Directory.Exists(currentModPath))
                {
                    var modFiles = Directory.GetFiles(currentModPath, "*.*", SearchOption.AllDirectories)
                        .Where(f => f.EndsWith(".pak") || f.EndsWith(".ucas") || f.EndsWith(".utoc"));
                    
                    foreach (var file in modFiles)
                    {
                        try
                        {
                            var fileInfo = new FileInfo(file);
                            totalBytes += fileInfo.Length;
                        }
                        catch
                        {
                            // 忽略无法访问的文件
                        }
                    }
                }
                
                return totalBytes / (1024 * 1024); // 转换为MB
            }
            catch
            {
                return 0;
            }
        }

        // 分类列表选择变化事件
        private void CategoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                // 处理分类选择变化，根据选中的分类筛选MOD
                FilterModsByCategory();
                
                // 更新C1区标题显示
                UpdateModCountDisplay();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"分类选择变化处理失败: {ex.Message}");
            }
        }
        
        // 根据分类筛选MOD
        private void FilterModsByCategory()
        {
            try
            {
                var filteredMods = GetFilteredMods();
                
                // 应用到两个视图上
                ModsCardView.ItemsSource = filteredMods;
                ModsListView.ItemsSource = filteredMods;
                
                // 更新MOD计数显示
                UpdateModCountDisplay();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 按分类筛选MOD失败: {ex.Message}");
            }
        }

        // 搜索框文本变化事件
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                // 延迟执行搜索，防止频繁刷新UI
                if (searchDebounceTimer != null)
                {
                    searchDebounceTimer.Stop();
                }
                else
                {
                    searchDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
                    searchDebounceTimer.Tick += (s, args) =>
                    {
                        searchDebounceTimer.Stop();
                        var filteredMods = GetFilteredMods();
                        
                        // 应用到两个视图上
                        ModsCardView.ItemsSource = filteredMods;
                        ModsListView.ItemsSource = filteredMods;
                        
                        UpdateModCountDisplay();
                    };
                }
                searchDebounceTimer.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 搜索框文本更改处理失败: {ex.Message}");
            }
        }
        
        // 全选状态标志
        private bool _isAllSelected = false;
        
        // 处理全选复选框的鼠标按下事件
        private void SelectAllCheckBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var checkBox = sender as CheckBox;
                if (checkBox != null)
                {
                    // 切换全选状态
                    _isAllSelected = !_isAllSelected;
                    
                    // 设置复选框状态
                    checkBox.IsChecked = _isAllSelected;
                    
                    // 设置所有MOD的选中状态
                    SetAllModsSelection(_isAllSelected);
                    
                    if (_isAllSelected)
                        Console.WriteLine("[DEBUG] 全选MOD");
                    else
                        Console.WriteLine("[DEBUG] 取消全选MOD");
                    
                    // 阻止事件继续传播，防止默认行为
                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"处理全选复选框点击失败: {ex.Message}");
            }
        }
        
        // 设置所有MOD的选中状态
        private void SetAllModsSelection(bool isSelected)
        {
            try
            {
                // 获取当前视图的Mods
                IEnumerable<Mod>? currentMods = null;
                if (ModsCardView.Visibility == Visibility.Visible)
                {
                    currentMods = ModsCardView.ItemsSource as IEnumerable<Mod>;
                }
                else
                {
                    currentMods = ModsListView.ItemsSource as IEnumerable<Mod>;
                }
                
                if (currentMods != null)
                {
                    foreach (var mod in currentMods)
                    {
                        mod.IsSelected = isSelected;
                    }
                    
                    // 更新计数显示
                    UpdateModCountDisplay();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 设置所有MOD选择状态失败: {ex.Message}");
            }
        }

        // 导入MOD
        private void ImportMod()
        {
            try
            {
                var openFileDialog = new OpenFileDialog
                {
                    Filter = "压缩文件 (*.zip;*.rar;*.7z)|*.zip;*.rar;*.7z|所有文件 (*.*)|*.*",
                    Multiselect = true
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    foreach (string file in openFileDialog.FileNames)
                    {
                        ImportModFromFile(file);
                    }
                }
            }
            catch (Exception ex)
            {
                ShowCustomMessageBox($"导入MOD失败: {ex.Message}", "导入错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 从文件导入MOD
        private void ImportModFromFile(string filePath)
        {
            try
            {
                Console.WriteLine($"开始导入MOD文件: {filePath}");
                
                if (!File.Exists(filePath))
                {
                    ShowCustomMessageBox("文件不存在", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var fileInfo = new FileInfo(filePath);
                var fileName = Path.GetFileNameWithoutExtension(filePath);
                var fileExtension = Path.GetExtension(filePath).ToLower();

                Console.WriteLine($"[DEBUG] 导入文件: {fileName}, 扩展名: {fileExtension}, 大小: {fileInfo.Length} 字节");

                // 创建MOD专用的备份目录
                var modBackupDir = Path.Combine(currentBackupPath, fileName);
                if (!Directory.Exists(modBackupDir))
                {
                    Directory.CreateDirectory(modBackupDir);
                    Console.WriteLine($"[DEBUG] 创建MOD备份目录: {modBackupDir}");
                }

                bool importSuccess = false;

                if (fileExtension == ".pak" || fileExtension == ".ucas" || fileExtension == ".utoc")
                {
                    // 直接复制MOD文件
                    importSuccess = ImportDirectModFiles(filePath, fileName, modBackupDir);
                }
                else if (fileExtension == ".zip" || fileExtension == ".rar" || fileExtension == ".7z")
                {
                    // 解压缩并导入
                    importSuccess = ImportCompressedMod(filePath, fileName, modBackupDir);
                }
                else
                {
                    ShowCustomMessageBox($"不支持的文件格式: {fileExtension}\n支持的格式: .pak, .ucas, .utoc, .zip, .rar, .7z", 
                        "格式错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (importSuccess)
                {
                    // 重新扫描MOD，更新列表
                    InitializeModsForGame();
                    
                    ShowCustomMessageBox($"MOD '{fileName}' 导入成功！", "导入成功", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    Console.WriteLine($"[DEBUG] MOD导入成功: {fileName}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"从文件导入MOD失败: {ex.Message}");
                Console.WriteLine($"[ERROR] 堆栈跟踪: {ex.StackTrace}");
                ShowCustomMessageBox($"导入失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 导入直接的MOD文件
        private bool ImportDirectModFiles(string filePath, string modName, string targetDir)
        {
            try
            {
                var fileName = Path.GetFileName(filePath);
                var targetPath = Path.Combine(targetDir, fileName);
                
                // 复制到备份目录
                File.Copy(filePath, targetPath, true);
                Console.WriteLine($"[DEBUG] 复制文件到备份目录: {targetPath}");

                // 同时复制到MOD目录（如果是已启用的MOD）
                if (!string.IsNullOrEmpty(currentModPath) && Directory.Exists(currentModPath))
                {
                    var modSubDir = Path.Combine(currentModPath, modName);
                    if (!Directory.Exists(modSubDir))
                    {
                        Directory.CreateDirectory(modSubDir);
                    }
                    
                    var modFilePath = Path.Combine(modSubDir, fileName);
                    File.Copy(filePath, modFilePath, true);
                    Console.WriteLine($"[DEBUG] 复制文件到MOD目录: {modFilePath}");
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 导入直接MOD文件失败: {ex.Message}");
                return false;
            }
        }

        // 导入压缩的MOD文件
        private bool ImportCompressedMod(string filePath, string modName, string targetDir)
        {
            try
            {
                Console.WriteLine($"[DEBUG] 开始解压MOD压缩文件: {filePath}");
                
                // 创建临时解压目录
                var tempDir = Path.Combine(Path.GetTempPath(), $"mod_temp_{Guid.NewGuid()}");
                Directory.CreateDirectory(tempDir);
                
                try
                {
                    // 解压文件
                    if (!ExtractCompressedFile(filePath, tempDir))
                    {
                        ShowCustomMessageBox("解压文件失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return false;
                    }

                    // 处理嵌套的压缩包
                    ProcessNestedArchives(tempDir);

                    // 查找MOD文件
                    var modFiles = Directory.GetFiles(tempDir, "*.*", SearchOption.AllDirectories)
                        .Where(f => 
                        {
                            var ext = Path.GetExtension(f).ToLower();
                            return ext == ".pak" || ext == ".ucas" || ext == ".utoc";
                        })
                        .ToList();

                    if (modFiles.Count == 0)
                    {
                        ShowCustomMessageBox("压缩文件中未找到有效的MOD文件", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return false;
                    }

                    Console.WriteLine($"[DEBUG] 在压缩文件中找到 {modFiles.Count} 个MOD文件");

                    // 按文件名前缀分组MOD文件
                    var modGroups = GroupModFilesByPrefix(modFiles);
                    Console.WriteLine($"[DEBUG] 识别出 {modGroups.Count} 个不同的MOD");

                    // 为每个MOD组创建单独的文件夹
                    int modIndex = 1;
                    foreach (var group in modGroups)
                    {
                        string groupModName = group.Key;
                        List<string> groupFiles = group.Value;
                        
                        // 生成MOD名称
                        // 如果只有一个MOD组，使用原始MOD名称，否则添加序号
                        string finalModName;
                        if (modGroups.Count == 1)
                        {
                            finalModName = modName;
                        }
                        else
                        {
                            // 如果组名是数字，使用它作为MOD名称的一部分
                            if (int.TryParse(groupModName, out _))
                            {
                                finalModName = $"{modName}_{groupModName}";
                            }
                            else
                            {
                                finalModName = $"{modName}_{modIndex}";
                            }
                        }
                        
                        Console.WriteLine($"[DEBUG] 处理MOD组 {modIndex}/{modGroups.Count}: {finalModName}, 包含 {groupFiles.Count} 个文件");
                        
                        // 创建MOD专用的备份子目录
                        var modBackupSubDir = Path.Combine(currentBackupPath, finalModName);
                        if (!Directory.Exists(modBackupSubDir))
                        {
                            Directory.CreateDirectory(modBackupSubDir);
                            Console.WriteLine($"[DEBUG] 创建MOD备份子目录: {modBackupSubDir}");
                        }
                        
                        // 复制MOD文件到备份目录
                        foreach (var modFile in groupFiles)
                        {
                            var fileName = Path.GetFileName(modFile);
                            var targetPath = Path.Combine(modBackupSubDir, fileName);
                            File.Copy(modFile, targetPath, true);
                            Console.WriteLine($"[DEBUG] 复制解压的MOD文件: {targetPath}");
                        }
                        
                        // 查找相关的预览图
                        var modFileDir = Path.GetDirectoryName(groupFiles.First()) ?? tempDir;
                        var imageFiles = Directory.GetFiles(modFileDir, "*.*", SearchOption.TopDirectoryOnly)
                            .Where(f => 
                            {
                                var ext = Path.GetExtension(f).ToLower();
                                return ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".bmp" || ext == ".gif";
                            })
                            .ToList();

                        if (imageFiles.Count > 0)
                        {
                            // 优先选择名称包含preview的图片
                            var previewImage = imageFiles.FirstOrDefault(f => 
                                Path.GetFileNameWithoutExtension(f).ToLower().Contains("preview")) 
                                ?? imageFiles.First();

                            var imageExt = Path.GetExtension(previewImage);
                            var previewPath = Path.Combine(modBackupSubDir, $"preview{imageExt}");
                            File.Copy(previewImage, previewPath, true);
                            Console.WriteLine($"[DEBUG] 复制预览图: {previewPath}");
                        }

                        // 复制到MOD目录（如果是启用状态）
                        if (!string.IsNullOrEmpty(currentModPath) && Directory.Exists(currentModPath))
                        {
                            var modSubDir = Path.Combine(currentModPath, finalModName);
                            if (!Directory.Exists(modSubDir))
                            {
                                Directory.CreateDirectory(modSubDir);
                            }

                            foreach (var modFile in groupFiles)
                            {
                                var fileName = Path.GetFileName(modFile);
                                var modFilePath = Path.Combine(modSubDir, fileName);
                                File.Copy(modFile, modFilePath, true);
                                Console.WriteLine($"[DEBUG] 复制到MOD目录: {modFilePath}");
                            }
                        }
                        
                        modIndex++;
                    }

                    return true;
                }
                finally
                {
                    // 清理临时目录
                    try
                    {
                        if (Directory.Exists(tempDir))
                        {
                            Directory.Delete(tempDir, true);
                            Console.WriteLine($"[DEBUG] 清理临时目录: {tempDir}");
                        }
                    }
                    catch (Exception cleanupEx)
                    {
                        Console.WriteLine($"[DEBUG] 清理临时目录失败: {cleanupEx.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 导入压缩MOD失败: {ex.Message}");
                return false;
            }
        }

        // 按文件名前缀分组MOD文件
        private Dictionary<string, List<string>> GroupModFilesByPrefix(List<string> modFiles)
        {
            var result = new Dictionary<string, List<string>>();
            
            // 首先尝试根据完整文件名前缀分组（例如"1.pak"和"1.ucas"应该在同一组）
            foreach (var modFile in modFiles)
            {
                // 获取文件名（不含扩展名）
                string fileName = Path.GetFileNameWithoutExtension(modFile);
                
                // 查找是否已有这个前缀的组
                bool found = false;
                
                // 先尝试精确匹配完整文件名
                if (result.ContainsKey(fileName))
                {
                    result[fileName].Add(modFile);
                    found = true;
                }
                else
                {
                    // 尝试查找数字前缀匹配（如"1.pak"和"1.ucas"）
                    foreach (var key in result.Keys)
                    {
                        // 如果文件名是纯数字，则按数字匹配
                        if (int.TryParse(fileName, out int fileNumber) && int.TryParse(key, out int keyNumber) && fileNumber == keyNumber)
                        {
                            result[key].Add(modFile);
                            found = true;
                            break;
                        }
                        // 否则检查前缀是否相同
                        else if (fileName.Length > 0 && key.Length > 0)
                        {
                            // 提取可能的数字前缀
                            string filePrefix = ExtractNumberPrefix(fileName);
                            string keyPrefix = ExtractNumberPrefix(key);
                            
                            if (!string.IsNullOrEmpty(filePrefix) && filePrefix == keyPrefix)
                            {
                                result[key].Add(modFile);
                                found = true;
                                break;
                            }
                        }
                    }
                }
                
                // 如果没找到匹配的组，创建新组
                if (!found)
                {
                    result[fileName] = new List<string> { modFile };
                }
            }
            
            return result;
        }
        
        // 提取文件名中的数字前缀
        private string ExtractNumberPrefix(string fileName)
        {
            // 匹配开头的数字部分
            var match = System.Text.RegularExpressions.Regex.Match(fileName, @"^\d+");
            return match.Success ? match.Value : string.Empty;
        }

        // 处理嵌套的压缩包
        private void ProcessNestedArchives(string directoryPath)
        {
            try
            {
                // 查找所有压缩文件
                var archiveFiles = Directory.GetFiles(directoryPath, "*.*", SearchOption.AllDirectories)
                    .Where(f => 
                    {
                        var ext = Path.GetExtension(f).ToLower();
                        return ext == ".zip" || ext == ".rar" || ext == ".7z";
                    })
                    .ToList();
                
                if (archiveFiles.Count == 0)
                {
                    return; // 没有嵌套的压缩包
                }
                
                Console.WriteLine($"[DEBUG] 发现 {archiveFiles.Count} 个嵌套的压缩包");
                
                foreach (var archiveFile in archiveFiles)
                {
                    try
                    {
                        // 为嵌套压缩包创建单独的解压目录
                        var nestedDir = Path.Combine(
                            Path.GetDirectoryName(archiveFile) ?? directoryPath,
                            Path.GetFileNameWithoutExtension(archiveFile) + "_extracted");
                        
                        // 如果目录已存在，先删除
                        if (Directory.Exists(nestedDir))
                        {
                            Directory.Delete(nestedDir, true);
                        }
                        
                        Directory.CreateDirectory(nestedDir);
                        
                        // 解压嵌套压缩包
                        if (ExtractCompressedFile(archiveFile, nestedDir))
                        {
                            Console.WriteLine($"[DEBUG] 成功解压嵌套压缩包: {archiveFile} -> {nestedDir}");
                            
                            // 检查解压后的目录中是否有MOD文件
                            var modFiles = Directory.GetFiles(nestedDir, "*.*", SearchOption.AllDirectories)
                                .Where(f => 
                                {
                                    var ext = Path.GetExtension(f).ToLower();
                                    return ext == ".pak" || ext == ".ucas" || ext == ".utoc";
                                })
                                .ToList();
                            
                            if (modFiles.Count > 0)
                            {
                                Console.WriteLine($"[DEBUG] 在嵌套压缩包中找到 {modFiles.Count} 个MOD文件");
                            }
                            
                            // 递归处理可能存在的更深层次嵌套
                            ProcessNestedArchives(nestedDir);
                        }
                        else
                        {
                            Console.WriteLine($"[DEBUG] 解压嵌套压缩包失败: {archiveFile}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] 处理嵌套压缩包失败: {archiveFile}, 错误: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 处理嵌套压缩包过程中出错: {directoryPath}, 错误: {ex.Message}");
            }
        }

        // 解压压缩文件
        private bool ExtractCompressedFile(string filePath, string extractPath)
        {
            try
            {
                var fileExtension = Path.GetExtension(filePath).ToLower();
                Console.WriteLine($"[DEBUG] 解压文件: {filePath}, 格式: {fileExtension}");
                
                if (fileExtension == ".zip")
                {
                    System.IO.Compression.ZipFile.ExtractToDirectory(filePath, extractPath, true);
                    return true;
                }
                else if (fileExtension == ".rar" || fileExtension == ".7z")
                {
                    try
                    {
                        // 尝试使用SharpCompress库解压
                        using (var archive = SharpCompress.Archives.ArchiveFactory.Open(filePath))
                        {
                            foreach (var entry in archive.Entries)
                            {
                                if (!entry.IsDirectory)
                                {
                                    var entryPath = Path.Combine(extractPath, entry.Key);
                                    var entryDirectory = Path.GetDirectoryName(entryPath);
                                    
                                    if (!string.IsNullOrEmpty(entryDirectory))
                                    {
                                        Directory.CreateDirectory(entryDirectory);
                                    }
                                    
                                    entry.WriteToFile(entryPath, new SharpCompress.Common.ExtractionOptions
                                    {
                                        ExtractFullPath = true,
                                        Overwrite = true
                                    });
                                }
                            }
                        }
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] 使用SharpCompress解压失败: {ex.Message}");
                        
                        // 如果SharpCompress解压失败，显示提示信息
                        ShowCustomMessageBox($"解压 {fileExtension} 格式文件失败。\n\n" +
                            "请手动解压此文件，然后导入解压后的MOD文件（.pak, .ucas, .utoc）。\n\n" +
                            "支持直接拖拽解压后的文件到程序窗口进行导入。", 
                            "需要手动解压", MessageBoxButton.OK, MessageBoxImage.Information);
                        return false;
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 解压文件失败: {ex.Message}");
                return false;
            }
        }

        // 启动游戏
        private void LaunchGame()
        {
            try
            {
                if (string.IsNullOrEmpty(currentGamePath))
                {
                    ShowCustomMessageBox("请先选择游戏路径", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!Directory.Exists(currentGamePath))
                {
                    ShowCustomMessageBox("游戏路径不存在，请重新配置游戏路径", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string gameExecutablePath = "";

                // 1. 优先使用保存的执行程序名称
                if (!string.IsNullOrEmpty(currentExecutableName))
                {
                    gameExecutablePath = Path.Combine(currentGamePath, currentExecutableName);
                    Console.WriteLine($"[DEBUG] 尝试使用保存的执行程序: {gameExecutablePath}");
                    
                    if (File.Exists(gameExecutablePath))
                    {
                        Console.WriteLine($"[DEBUG] 找到保存的执行程序文件: {currentExecutableName}");
                    }
                    else
                    {
                        Console.WriteLine($"[DEBUG] 保存的执行程序文件不存在，需要重新查找");
                        gameExecutablePath = "";
                    }
                }

                // 2. 如果没有保存的执行程序或文件不存在，自动查找
                if (string.IsNullOrEmpty(gameExecutablePath))
                {
                    Console.WriteLine($"[DEBUG] 开始自动查找游戏执行程序...");
                    var detectedExecutableName = AutoDetectGameExecutable(currentGamePath, currentGameName);
                    
                    if (!string.IsNullOrEmpty(detectedExecutableName))
                    {
                        gameExecutablePath = Path.Combine(currentGamePath, detectedExecutableName);
                        Console.WriteLine($"[DEBUG] 自动查找到执行程序: {detectedExecutableName}");
                        
                        // 更新并保存配置
                        currentExecutableName = detectedExecutableName;
                        SaveConfiguration(currentExecutableName);
                        Console.WriteLine($"[DEBUG] 已更新并保存执行程序配置");
                    }
                }

                // 3. 启动游戏
                if (!string.IsNullOrEmpty(gameExecutablePath) && File.Exists(gameExecutablePath))
                {
                    Console.WriteLine($"[DEBUG] 启动游戏: {gameExecutablePath}");
                    
                    var processStartInfo = new ProcessStartInfo
                    {
                        FileName = gameExecutablePath,
                        WorkingDirectory = currentGamePath,
                        UseShellExecute = true
                    };
                    
                    Process.Start(processStartInfo);
                    
                    ShowCustomMessageBox($"游戏 '{currentGameName}' 启动成功！\n\n" +
                                  $"执行程序: {Path.GetFileName(gameExecutablePath)}", 
                                  "启动成功", 
                                  MessageBoxButton.OK, 
                                  MessageBoxImage.Information);
                }
                else
                {
                    ShowCustomMessageBox($"无法找到游戏可执行文件。\n\n" +
                                  $"游戏路径: {currentGamePath}\n" +
                                  $"请检查游戏是否正确安装，或手动重新配置游戏路径。", 
                                  "启动失败", 
                                  MessageBoxButton.OK, 
                                  MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 启动游戏失败: {ex.Message}");
                ShowCustomMessageBox($"启动游戏失败: {ex.Message}\n\n" +
                              $"游戏: {currentGameName}\n" +
                              $"路径: {currentGamePath}", 
                              "启动错误", 
                              MessageBoxButton.OK, 
                              MessageBoxImage.Error);
            }
        }

        // 格式化文件大小
        private string FormatFileSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number = number / 1024;
                counter++;
            }
            return string.Format("{0:n1}{1}", number, suffixes[counter]);
        }

        // 获取MOD图标
        private string GetModIcon(string modType)
        {
            return modType switch
            {
                "面部" => "👤",
                "人物" => "👥",
                "武器" => "⚔️",
                "修改" => "🔧",
                _ => "📦"
            };
        }

        // 更新MOD详情
        private void UpdateModDetails(Mod mod)
        {
            try
            {
                if (mod == null) return;

                selectedMod = mod;
                
                // 更新详情面板
                if (ModNameText != null) ModNameText.Text = mod.Name;
                if (ModDescriptionText != null) ModDescriptionText.Text = mod.Description;
                if (ModDetailIcon != null) ModDetailIcon.Text = GetModIcon(mod.Type);
                if (ModStatusText != null) ModStatusText.Text = mod.Status;
                if (ModSizeText != null) ModSizeText.Text = mod.Size;
                if (ModOriginalNameText != null) ModOriginalNameText.Text = mod.RealName;
                if (ModImportDateText != null) ModImportDateText.Text = mod.ImportDate;

                // 根据MOD状态更新滑动开关
                bool isEnabled = mod.Status == "已启用";
                UpdateToggleState(isEnabled);
                Console.WriteLine($"[DEBUG] 更新滑动开关状态: MOD={mod.Name}, 状态={mod.Status}, 开关={isEnabled}");

                // 更新状态文字颜色
                if (ModStatusText != null)
                {
                    if (isEnabled)
                    {
                        ModStatusText.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129)); // #10B981 绿色
                    }
                    else
                    {
                        ModStatusText.Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128)); // #6B7280 灰色
                    }
                }

                // 更新预览图
                UpdateModDetailPreview(mod);
                
                Console.WriteLine($"[DEBUG] 详情面板更新完成: {mod.Name}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"更新MOD详情失败: {ex.Message}");
            }
        }

        // 更新MOD详情预览图
        private void UpdateModDetailPreview(Mod mod)
        {
            try
            {
                Console.WriteLine($"[DEBUG] 更新详情面板预览图: MOD={mod?.Name}, HasImageSource={mod?.PreviewImageSource != null}");
                
                // 首先尝试使用已加载的ImageSource
                if (mod?.PreviewImageSource != null && ModDetailPreviewImage != null)
                {
                    ModDetailPreviewImage.Source = mod.PreviewImageSource;
                    ModDetailPreviewImage.Visibility = Visibility.Visible;
                    
                    // 重要：隐藏提示占位符，防止文字浮在图片上方
                    if (PreviewPlaceholder != null)
                        PreviewPlaceholder.Visibility = Visibility.Collapsed;
                    if (ModDetailIconContainer != null)
                        ModDetailIconContainer.Visibility = Visibility.Collapsed;
                        
                    Console.WriteLine($"[DEBUG] 使用PreviewImageSource显示预览图: {mod.Name}");
                    return;
                }
                
                // 备用方案：如果ImageSource为空但路径有效，尝试重新加载
                if (!string.IsNullOrEmpty(mod?.PreviewImagePath) && File.Exists(mod.PreviewImagePath) && ModDetailPreviewImage != null)
                {
                    try
                    {
                        Console.WriteLine($"[DEBUG] 尝试从文件重新加载预览图: {mod.PreviewImagePath}");
                        
                        // 使用Uri方式加载图片，避免缓存问题
                        var uri = new Uri(mod.PreviewImagePath, UriKind.Absolute);
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = uri;
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                        bitmap.EndInit();
                        bitmap.Freeze();
                        
                        ModDetailPreviewImage.Source = bitmap;
                        ModDetailPreviewImage.Visibility = Visibility.Visible;
                        
                        // 重要：隐藏提示占位符，防止文字浮在图片上方
                        if (PreviewPlaceholder != null)
                            PreviewPlaceholder.Visibility = Visibility.Collapsed;
                        if (ModDetailIconContainer != null)
                            ModDetailIconContainer.Visibility = Visibility.Collapsed;
                            
                        Console.WriteLine($"[DEBUG] 成功从文件重新加载预览图: {mod.Name}");
                        return;
                    }
                    catch (Exception loadEx)
                    {
                        Console.WriteLine($"[DEBUG] 从文件加载预览图失败: {loadEx.Message}");
                    }
                }
                
                // 没有预览图，显示图标占位符
                Console.WriteLine($"[DEBUG] 显示图标占位符");
                if (ModDetailPreviewImage != null)
                {
                    ModDetailPreviewImage.Source = null;
                    ModDetailPreviewImage.Visibility = Visibility.Collapsed;
                }
                
                // 显示提示占位符
                if (PreviewPlaceholder != null)
                    PreviewPlaceholder.Visibility = Visibility.Visible;
                if (ModDetailIconContainer != null)
                    ModDetailIconContainer.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"更新MOD详情预览图失败: {ex.Message}");
                // 发生异常时，回退到显示图标
                if (ModDetailPreviewImage != null)
                {
                    ModDetailPreviewImage.Source = null;
                    ModDetailPreviewImage.Visibility = Visibility.Collapsed;
                }
                
                // 显示提示占位符
                if (PreviewPlaceholder != null)
                    PreviewPlaceholder.Visibility = Visibility.Visible;
                if (ModDetailIconContainer != null)
                    ModDetailIconContainer.Visibility = Visibility.Visible;
            }
        }

        // 刷新分类显示
        private void RefreshCategoryDisplay()
        {
            try
            {
                var selectedCategoryName = (CategoryList.SelectedItem as Category)?.Name;

                // 新建分类后，优先显示所有自定义分类（包括没有MOD的）
                var newCategories = new List<Category>
                {
                    new Category { Name = "全部", Count = allMods.Count }
                };

                // 统计每个分类下的MOD数量
                var categoryCounts = allMods
                    .Where(m => m.Categories != null)
                    .SelectMany(m => m.Categories)
                    .GroupBy(c => c)
                    .ToDictionary(g => g.Key, g => g.Count());

                // 加入所有自定义分类（包括没有MOD的），排除"已启用/已禁用"
                if (_categoryService != null && _categoryService.Categories != null)
                {
                    foreach (var catItem in _categoryService.Categories)
                    {
                        if (catItem.Name == "全部" || catItem.Name == "已启用" || catItem.Name == "已禁用") continue;
                        int count = categoryCounts.ContainsKey(catItem.Name) ? categoryCounts[catItem.Name] : 0;
                        newCategories.Add(new Category { Name = catItem.Name, Count = count });
                    }
                }
                else
                {
                    // 兼容旧逻辑，防止空指针
                    foreach (var kvp in categoryCounts.OrderBy(kvp => kvp.Key))
                    {
                        if (kvp.Key != "全部" && kvp.Key != "已启用" && kvp.Key != "已禁用")
                        {
                            newCategories.Add(new Category { Name = kvp.Key, Count = kvp.Value });
                        }
                    }
                }

                // 去重（防止同名分类重复）
                newCategories = newCategories
                    .GroupBy(c => c.Name)
                    .Select(g => g.First())
                    .ToList();

                // Update the ObservableCollection on the UI thread
                Dispatcher.Invoke(() =>
                {
                    categories.Clear();
                    foreach (var cat in newCategories)
                    {
                        categories.Add(cat);
                    }

                    // Restore selection
                    if (selectedCategoryName != null)
                    {
                        var newSelection = categories.FirstOrDefault(c => c.Name == selectedCategoryName);
                        CategoryList.SelectedItem = newSelection ?? categories.FirstOrDefault();
                    }
                    else if (categories.Any())
                    {
                        CategoryList.SelectedIndex = 0;
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"刷新分类显示失败: {ex.Message}");
            }
        }

        private async void AddCategoryButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string categoryName = ShowInputDialog("请输入分类名称:", "添加分类");
                if (!string.IsNullOrEmpty(categoryName))
                {
                    // 确保CategoryService已初始化
                    if (_categoryService == null)
                    {
                        InitializeServices();
                        if (!string.IsNullOrEmpty(currentGameName))
                        {
                            await _categoryService?.SetCurrentGameAsync(currentGameName);
                        }
                    }
                    
                    if (_categoryService != null)
                    {
                        // 检查是否要添加为子分类
                        CategoryItem? parentCategory = null;
                        var selectedItem = CategoryList.SelectedItem;
                        
                        // 如果选中的是CategoryItem且不是默认分类，可以作为父分类
                        if (selectedItem is CategoryItem selectedCategory && 
                            !new[] { "全部", "已启用", "已禁用" }.Contains(selectedCategory.Name))
                        {
                            var result = ShowCustomMessageBox($"是否要将 '{categoryName}' 添加为 '{selectedCategory.Name}' 的子分类？\n\n" +
                                "点击'是'添加为子分类，点击'否'添加为根分类。", 
                                "添加分类", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                            
                            if (result == MessageBoxResult.Cancel)
                                return;
                            
                            if (result == MessageBoxResult.Yes)
                                parentCategory = selectedCategory;
                        }
                        
                        // 使用CategoryService添加分类
                        var newCategory = await _categoryService.AddCategoryAsync(categoryName, parentCategory);
                        
                        // 立即刷新分类显示
                        RefreshCategoryDisplay();
                        
                        Console.WriteLine($"[DEBUG] 成功添加分类: {newCategory.FullPath}");
                    }
                    else
                    {
                        ShowCustomMessageBox("分类服务初始化失败，无法添加分类", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 添加分类失败: {ex.Message}");
                ShowCustomMessageBox($"添加分类失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 删除分类按钮点击事件
        private async void DeleteCategoryButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 获取所有选中的项
                var selectedItems = CategoryList.SelectedItems;
                if (selectedItems == null || selectedItems.Count == 0)
                {
                    ShowCustomMessageBox("请先选择要删除的自定义分类", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var defaultCategories = new[] { "全部", "已启用", "已禁用" };
                List<CategoryItem> categoriesToDelete = new List<CategoryItem>();
                
                // 检查是否所有选中项都是有效的可删除分类
                foreach (var item in selectedItems)
                {
                    string? categoryName = null;
                    CategoryItem? categoryItem = null;
                    
                    if (item is CategoryItem ci)
                    {
                        categoryName = ci.Name;
                        categoryItem = ci;
                    }
                    else if (item is Category c)
                    {
                        categoryName = c.Name;
                    }
                    
                    if (string.IsNullOrEmpty(categoryName) || defaultCategories.Contains(categoryName))
                    {
                        // 跳过无效的项或系统分类
                        continue;
                    }
                    
                    if (categoryItem == null && _categoryService != null)
                    {
                        categoryItem = _categoryService.Categories.FirstOrDefault(x => 
                            x.Name == categoryName && x.Name != "已启用" && x.Name != "已禁用");
                    }
                    
                    if (categoryItem != null)
                    {
                        categoriesToDelete.Add(categoryItem);
                    }
                }
                
                if (categoriesToDelete.Count == 0)
                {
                    ShowCustomMessageBox("没有选择有效的自定义分类，系统分类不能删除", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                // 显示确认对话框
                string confirmMessage = categoriesToDelete.Count == 1 
                    ? $"确定要删除分类 '{categoriesToDelete[0].Name}' 吗？\n\n此操作将同时删除所有子分类。"
                    : $"确定要删除这 {categoriesToDelete.Count} 个分类吗？\n\n此操作将同时删除所有子分类。";
                    
                var result = ShowCustomMessageBox(confirmMessage, "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes && _categoryService != null)
                {
                    // 批量删除所有选中的分类
                    foreach (var categoryItem in categoriesToDelete)
                    {
                        await _categoryService.RemoveCategoryAsync(categoryItem);
                        Console.WriteLine($"[DEBUG] 成功删除分类: {categoryItem.Name}");
                    }
                    
                    RefreshCategoryDisplay();
                    Console.WriteLine($"[DEBUG] 成功删除 {categoriesToDelete.Count} 个分类");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 删除分类失败: {ex.Message}");
                ShowCustomMessageBox($"删除分类失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void InitializeServices()
        {
            try
            {
                // 设置依赖注入容器
                var services = new ServiceCollection();
                services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
                services.AddTransient<CategoryService>();
                services.AddTransient<ModService>();
                
                var serviceProvider = services.BuildServiceProvider();
                
                _categoryService = serviceProvider.GetService<CategoryService>();
                _modService = serviceProvider.GetService<ModService>();
                _logger = serviceProvider.GetService<ILogger<MainWindow>>();
                
                Console.WriteLine("[DEBUG] CategoryService和ModService初始化完成");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 初始化服务失败: {ex.Message}");
            }
        }

        private async void InitializeCategoriesForGame()
        {
            try
            {
                if (_categoryService != null && !string.IsNullOrEmpty(currentGameName))
                {
                    // 为当前游戏设置分类服务
                    await _categoryService.SetCurrentGameAsync(currentGameName);
                    
                    // 如果没有分类，初始化默认分类
                    if (!_categoryService.Categories.Any())
                    {
                        await _categoryService.InitializeDefaultCategoriesAsync();
                    }
                    
                    // 刷新分类显示
                    RefreshCategoryDisplay();
                    
                    Console.WriteLine($"[DEBUG] 为游戏 {currentGameName} 初始化分类完成，共 {_categoryService.Categories.Count} 个分类");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 初始化游戏分类失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 在分类列表中选中指定分类
        /// </summary>
        private void SelectCategoryInList(CategoryItem targetCategory)
        {
            try
            {
                if (CategoryList.ItemsSource is List<object> categories)
                {
                    for (int i = 0; i < categories.Count; i++)
                    {
                        if (categories[i] is CategoryItem categoryItem && categoryItem.Name == targetCategory.Name)
                        {
                            CategoryList.SelectedIndex = i;
                            CategoryList.ScrollIntoView(categories[i]);
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 选中分类失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 分类列表拖拽进入事件
        /// </summary>
        private void CategoryList_DragEnter(object sender, DragEventArgs e)
        {
            try
            {
                if (e.Data.GetDataPresent("SelectedMods"))
                {
                    e.Effects = DragDropEffects.Move;
                }
                else if (e.Data.GetDataPresent("CategoryDragData"))
                {
                    // 支持分类重排序拖拽
                    e.Effects = DragDropEffects.Move;
                }
                else
                {
                    e.Effects = DragDropEffects.None;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 分类列表拖拽进入事件失败: {ex.Message}");
                e.Effects = DragDropEffects.None;
            }
        }       
        /// 获取拖拽目标分类
        /// </summary>
        private object? GetDropTargetCategory(DragEventArgs e)
        {
            try
            {
                var position = e.GetPosition(CategoryList);
                var hitTest = VisualTreeHelper.HitTest(CategoryList, position);
                
                if (hitTest?.VisualHit != null)
                {
                    var listBoxItem = FindParent<ListBoxItem>(hitTest.VisualHit);
                    if (listBoxItem?.DataContext != null)
                    {
                        return listBoxItem.DataContext;
                    }
                }
                
                // 如果没有命中特定项，返回当前选中的分类
                return CategoryList.SelectedItem;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 获取拖拽目标分类失败: {ex.Message}");
                return CategoryList.SelectedItem;
            }
        }

        /// <summary>
        /// 查找父级控件
        /// </summary>
        private T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parent = VisualTreeHelper.GetParent(child);
            if (parent == null) return null;
            if (parent is T) return parent as T;
            return FindParent<T>(parent);
        }

        /// <summary>
        /// 窗口关闭事件处理
        /// </summary>
        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            try
            {
                // 保存分类数据
                if (_categoryService != null && !string.IsNullOrEmpty(currentGameName))
                {
                    Console.WriteLine("[DEBUG] 保存分类数据...");
                    // 同步等待保存完成，避免程序在此处退出
                    _categoryService.SetCurrentGameAsync(currentGameName).GetAwaiter().GetResult();
                }
                
                Console.WriteLine("[DEBUG] 程序正常退出");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 保存数据时出错: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 移动MOD到指定分类
        /// </summary>
        private void MoveModsToCategory(List<Mod> mods, object targetCategory)
        {
            try
            {
                string categoryName = "未分类";
                
                if (targetCategory is CategoryItem categoryItem)
                {
                    categoryName = categoryItem.Name;
                }
                else if (targetCategory is Category category)
                {
                    categoryName = category.Name;
                }
                
                // 特殊处理"全部"分类
                if (categoryName == "全部")
                {
                    ShowCustomMessageBox("不能将MOD移动到'全部'分类", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                // 更新MOD的分类
                foreach (var mod in mods)
                {
                    if (!mod.Categories.Contains(categoryName))
                    {
                        mod.Categories.Clear();
                        mod.Categories.Add(categoryName);
                        Console.WriteLine($"[DEBUG] MOD '{mod.Name}' 已移动到分类 '{categoryName}'");
                    }
                }
                
                // 清除选中状态
                foreach (var mod in mods)
                {
                    mod.IsSelected = false;
                }
                
                // 刷新显示
                RefreshCategoryDisplay();
                FilterModsByCategory();
                
                ShowCustomMessageBox($"成功将 {mods.Count} 个MOD移动到分类 '{categoryName}'", "移动完成", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 移动MOD到分类失败: {ex.Message}");
                ShowCustomMessageBox($"移动MOD到分类失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 右键菜单打开时动态生成分类子菜单
        private void ModContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is ContextMenu contextMenu)
                {
                    // 调试：列出所有菜单项
                    Console.WriteLine($"[DEBUG] 右键菜单打开，总菜单项数: {contextMenu.Items.Count}");
                    foreach (var item in contextMenu.Items.OfType<MenuItem>())
                    {
                        Console.WriteLine($"[DEBUG] 菜单项: Name='{item.Name}', Header='{item.Header}'");
                    }
                    
                    // 找到移动到分类的菜单项
                    var moveToCategoryMenuItem = contextMenu.Items.OfType<MenuItem>()
                        .FirstOrDefault(m => m.Name == "MoveToCategoryMenuItem");
                    
                    Console.WriteLine($"[DEBUG] 找到移动分类菜单项: {moveToCategoryMenuItem != null}");
                    Console.WriteLine($"[DEBUG] CategoryService状态: {(_categoryService != null ? "已初始化" : "未初始化")}");
                    
                    if (moveToCategoryMenuItem != null)
                    {
                        // 清空现有的子菜单
                        moveToCategoryMenuItem.Items.Clear();
                        
                        // 确保CategoryService已初始化
                        if (_categoryService == null)
                        {
                            InitializeServices();
                            if (!string.IsNullOrEmpty(currentGameName))
                            {
                                _categoryService?.SetCurrentGameAsync(currentGameName);
                            }
                        }
                        
                        // 获取当前MOD - 从ContextMenu的PlacementTarget获取
                        Mod? mod = null;
                        if (contextMenu.PlacementTarget is FrameworkElement element)
                        {
                            mod = element.DataContext as Mod;
                        }
                        Console.WriteLine($"[DEBUG] 获取到的MOD: {mod?.Name ?? "null"}");
                        if (mod == null) 
                        {
                            Console.WriteLine("[DEBUG] 无法获取MOD对象，退出");
                            return;
                        }
                        
                        // 获取当前显示的分类列表
                        var availableCategories = new List<string>();
                        
                        // 从当前CategoryList获取所有可用分类
                        if (CategoryList.ItemsSource != null)
                        {
                            foreach (var item in CategoryList.ItemsSource)
                            {
                                if (item is Category category)
                                {
                                    // 排除系统默认分类
                                    if (!new[] { "全部", "已启用", "已禁用" }.Contains(category.Name))
                                    {
                                        availableCategories.Add(category.Name);
                                    }
                                }
                                else if (item is UEModManager.Core.Models.CategoryItem categoryItem)
                                {
                                    // 排除系统默认分类
                                    if (!new[] { "全部", "已启用", "已禁用" }.Contains(categoryItem.Name))
                                    {
                                        availableCategories.Add(categoryItem.Name);
                                    }
                                }
                            }
                        }
                        
                        // 如果没有从CategoryList获取到分类，尝试从CategoryService获取
                        if (!availableCategories.Any() && _categoryService != null && _categoryService.Categories.Any())
                        {
                            availableCategories = _categoryService.Categories
                                .Where(c => !new[] { "全部", "已启用", "已禁用" }.Contains(c.Name))
                                .Select(c => c.Name)
                                .ToList();
                        }
                        
                        Console.WriteLine($"[DEBUG] 找到 {availableCategories.Count} 个可用分类");
                        
                        if (availableCategories.Any())
                        {
                            foreach (var categoryName in availableCategories)
                            {
                                var categoryMenuItem = new MenuItem
                                {
                                    Header = categoryName,
                                    Style = (Style)FindResource("DarkMenuItem"),
                                    Tag = mod // 将MOD信息附加到Tag中
                                };
                                
                                // 添加图标
                                var icon = new TextBlock
                                {
                                    Text = "📂",
                                    FontSize = 12,
                                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9CA3AF"))
                                };
                                categoryMenuItem.Icon = icon;
                                
                                // 添加点击事件
                                categoryMenuItem.Click += (s, args) => MoveToCategorySubMenuItem_Click(s, args, categoryName);
                                
                                moveToCategoryMenuItem.Items.Add(categoryMenuItem);
                                Console.WriteLine($"[DEBUG] 添加分类菜单项: {categoryName}");
                            }
                            
                            // 强制刷新菜单UI
                            moveToCategoryMenuItem.InvalidateVisual();
                            moveToCategoryMenuItem.UpdateLayout();
                            
                            // 确保HasItems属性正确
                            Console.WriteLine($"[DEBUG] 菜单项数量: {moveToCategoryMenuItem.Items.Count}, HasItems: {moveToCategoryMenuItem.HasItems}");
                        }
                        else
                        {
                            // 没有可用分类时显示提示
                            var noCategories = new MenuItem
                            {
                                Header = "暂无自定义分类，请先在左侧添加分类",
                                Style = (Style)FindResource("DarkMenuItem"),
                                IsEnabled = false
                            };
                            moveToCategoryMenuItem.Items.Add(noCategories);
                            Console.WriteLine("[DEBUG] 显示无分类提示");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 生成分类子菜单失败: {ex.Message}");
                Console.WriteLine($"[ERROR] 堆栈跟踪: {ex.StackTrace}");
            }
        }
        
        // 分类子菜单点击事件
        private void MoveToCategorySubMenuItem_Click(object sender, RoutedEventArgs e, string categoryName)
        {
            try
            {
                if (sender is MenuItem menuItem && menuItem.Tag is Mod mod)
                {
                    // 更新MOD的分类
                    mod.Categories = new List<string> { categoryName };
                    
                    // 刷新分类显示以更新数量
                    RefreshCategoryDisplay();
                    
                    Console.WriteLine($"[DEBUG] MOD {mod.Name} 已移动到分类: {categoryName}");
                    ShowCustomMessageBox($"MOD '{mod.Name}' 已移动到分类 '{categoryName}'", "移动成功", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"移动到分类失败: {ex.Message}");
                ShowCustomMessageBox($"移动到分类失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 支持捐赠相关事件
        // 现在使用ToolTip来显示捐赠信息，不再需要Popup
        private void DonationText_MouseEnter(object sender, MouseEventArgs e)
        {
            try
            {
                // 鼠标悬浮效果通过按钮样式和ToolTip自动处理
                Console.WriteLine("[DEBUG] 鼠标悬浮在捐赠按钮上");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"捐赠按钮鼠标悬浮处理失败: {ex.Message}");
            }
        }

        private void DonationText_MouseLeave(object sender, MouseEventArgs e)
        {
            try
            {
                // 鼠标离开效果通过按钮样式自动处理
                Console.WriteLine("[DEBUG] 鼠标离开捐赠按钮");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"捐赠按钮鼠标离开处理失败: {ex.Message}");
            }
        }

        private void DonationText_MouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                // 点击打开关于窗口
                ShowAboutDialog();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"打开关于窗口失败: {ex.Message}");
            }
        }

        // 剑星MOD合集点击事件
        private void ModCollectionText_MouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                ShowModCollectionDialog();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"显示MOD合集窗口失败: {ex.Message}");
            }
        }

        // 显示MOD合集窗口
        private void ShowModCollectionDialog()
        {
            try
            {
                // 根据当前游戏类型设置窗口标题和内容
                string title, titleText;
                if (currentGameType == GameType.StellarBlade)
                {
                    title = isEnglishMode ? "Stellar Blade MOD Collection" : "剑星MOD合集";
                    titleText = isEnglishMode ? "Stellar Blade MOD Collection" : "剑星MOD合集";
                }
                else if (currentGameType == GameType.Enshrouded)
                {
                    title = isEnglishMode ? "Enshrouded MOD Collection" : "光与影：33号远征队";
                    titleText = isEnglishMode ? "Enshrouded MOD Collection" : "光与影：33号远征队";
                }
                else if (currentGameType == GameType.BlackMythWukong)
                {
                    title = isEnglishMode ? "Black Myth Wukong MOD Collection" : "黑神话悟空MOD合集";
                    titleText = isEnglishMode ? "Black Myth Wukong MOD Collection" : "黑神话悟空MOD合集";
                }
                else
                {
                    title = isEnglishMode ? "MOD Collection" : "MOD合集";
                    titleText = isEnglishMode ? "MOD Collection" : "MOD合集";
                }
                
                var dialog = new Window
                {
                    Title = title,
                    Width = 500,
                    Height = 400,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                    Background = new SolidColorBrush(Color.FromRgb(15, 27, 46)),
                    WindowStyle = WindowStyle.ToolWindow,
                    ResizeMode = ResizeMode.NoResize
                };

                var mainBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(15, 27, 46)),
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(30)
                };

                var stackPanel = new StackPanel();

                // 标题
                var titleTextBlock = new TextBlock
                {
                    Text = titleText,
                    Foreground = Brushes.White,
                    FontSize = 24,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 30)
                };
                stackPanel.Children.Add(titleTextBlock);

                // 描述文字
                var descText = new TextBlock
                {
                    Text = isEnglishMode ? 
                        "Access our cloud storage collection with hundreds of high-quality MODs" : 
                        "访问我们的云盘合集，获取数百个精品MOD资源",
                    Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175)),
                    FontSize = 14,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 30)
                };
                stackPanel.Children.Add(descText);

                // 网盘选项
                var cloudPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 20)
                };

                // 迅雷云盘
                var xunleiButton = CreateCloudButton(
                    isEnglishMode ? "Thunder Cloud" : "迅雷云盘", 
                    XunleiImageName);
                xunleiButton.Margin = new Thickness(0, 0, 20, 0);
                cloudPanel.Children.Add(xunleiButton);

                // 百度网盘
                var baiduButton = CreateCloudButton(
                    isEnglishMode ? "Baidu Cloud" : "百度网盘", 
                    BaiduImageName);
                cloudPanel.Children.Add(baiduButton);

                stackPanel.Children.Add(cloudPanel);

                // 关闭按钮
                var closeButton = new Button
                {
                    Content = isEnglishMode ? "Close" : "关闭",
                    Width = 100,
                    Height = 35,
                    Background = new SolidColorBrush(Color.FromRgb(75, 85, 99)),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    FontSize = 14,
                    Cursor = Cursors.Hand,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 30, 0, 0)
                };
                closeButton.Click += (s, e) => dialog.Close();
                stackPanel.Children.Add(closeButton);

                mainBorder.Child = stackPanel;
                dialog.Content = mainBorder;

                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"显示MOD合集窗口失败: {ex.Message}");
                ShowCustomMessageBox($"显示MOD合集窗口失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 创建云盘按钮
        private Border CreateCloudButton(string title, string imageName)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(26, 35, 50)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(75, 85, 99)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(20),
                Width = 180,
                Height = 200
            };

            var stackPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center
            };

            // 尝试加载图片
            var imagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, imageName);
            if (File.Exists(imagePath))
            {
                var image = new Image
                {
                    Source = new BitmapImage(new Uri(imagePath)),
                    Width = 100,
                    Height = 100,
                    Margin = new Thickness(0, 0, 0, 15)
                };
                stackPanel.Children.Add(image);
            }
            else
            {
                var placeholder = new TextBlock
                {
                    Text = "📁",
                    FontSize = 48,
                    Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 15)
                };
                stackPanel.Children.Add(placeholder);
            }

            var titleText = new TextBlock
            {
                Text = title,
                Foreground = Brushes.White,
                FontSize = 16,
                FontWeight = FontWeights.Medium,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            stackPanel.Children.Add(titleText);

            border.Child = stackPanel;

            return border;
        }

        #region 剑星专属功能按钮事件

        // 收集工具箱按钮点击事件
        private void CollectionToolButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 显示下拉菜单
                if (CollectionToolButton != null && CollectionToolMenu != null)
                {
                    CollectionToolMenu.PlacementTarget = CollectionToolButton;
                    CollectionToolMenu.IsOpen = true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"收集工具箱按钮点击失败: {ex.Message}");
            }
        }

        // 物品收集菜单项点击事件
        private void ItemCollectionMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 打开物品收集网页
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://codepen.io/aigame/full/MYwXoGq",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"打开物品收集网页失败: {ex.Message}");
                ShowCustomMessageBox($"打开物品收集网页失败: {ex.Message}\n\n请手动访问: https://codepen.io/aigame/full/MYwXoGq",
                    "打开网页失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // 衣服收集菜单项点击事件
        private void ClothingCollectionMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 打开衣服收集网页
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://codepen.io/aigame/full/xbGaqpx",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"打开衣服收集网页失败: {ex.Message}");
                ShowCustomMessageBox($"打开衣服收集网页失败: {ex.Message}\n\n请手动访问: https://codepen.io/aigame/full/xbGaqpx",
                    "打开网页失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // MOD合集按钮点击事件
        private void StellarModCollectionButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 显示MOD合集对话框
                ShowModCollectionDialog();
            }
            catch (Exception ex)
            {
                string gameName = "MOD合集";
                if (currentGameType == GameType.StellarBlade)
                    gameName = "剑星MOD合集";
                else if (currentGameType == GameType.Enshrouded)
                    gameName = "光与影MOD合集";
                else if (currentGameType == GameType.BlackMythWukong)
                    gameName = "黑神话悟空MOD合集";
                
                Console.WriteLine($"打开{gameName}失败: {ex.Message}");
            }
        }

        #endregion

        // 让B1区分类列表支持鼠标滚轮滚动
        private void CategoryList_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            try
            {
                // 查找外层ScrollViewer
                var scrollViewer = FindParent<ScrollViewer>(CategoryList);
                if (scrollViewer != null && e.Delta != 0)
                {
                    // 仅滚动B1区自己的内容，不影响C1区
                    scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
                    
                    // 标记事件已处理，防止冒泡到父级容器
                    e.Handled = true;
                    
                    Console.WriteLine("[DEBUG] B1区滚轮滚动");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] B1区滚轮事件处理失败: {ex.Message}");
            }
        }

        // 重命名分类按钮点击事件
        private async void RenameCategoryButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 优先使用CategoryService的分类
                if (_categoryService != null && CategoryList.SelectedItem is CategoryItem selectedCategoryItem)
                {
                    // 检查是否是默认分类
                    var defaultCategories = new[] { "全部", "已启用", "已禁用" };
                    if (defaultCategories.Contains(selectedCategoryItem.Name))
                    {
                        ShowCustomMessageBox("默认分类不能重命名", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                    string newName = ShowInputDialog("请输入新的分类名称:", "重命名分类", selectedCategoryItem.Name);
                    if (!string.IsNullOrEmpty(newName) && newName != selectedCategoryItem.Name)
                    {
                        bool success = await _categoryService.RenameCategoryAsync(selectedCategoryItem, newName);
                        if (success)
                        {
                            // 刷新分类显示
                            RefreshCategoryDisplay();
                            Console.WriteLine($"[DEBUG] 成功重命名分类: {selectedCategoryItem.Name} -> {newName}");
                        }
                        else
                        {
                            ShowCustomMessageBox("重命名失败，分类名称可能已存在", "重命名分类", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                }
                else if (CategoryList.SelectedItem is Category selectedCategory)
                {
                    // 回退到旧方式
                    string newName = ShowInputDialog("请输入新的分类名称:", "重命名分类", selectedCategory.Name);
                    if (!string.IsNullOrEmpty(newName) && newName != selectedCategory.Name)
                    {
                        selectedCategory.Name = newName;
                        RefreshCategoryDisplay();
                    }
                }
                else
                {
                    ShowCustomMessageBox("请先选择要重命名的分类", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 重命名分类失败: {ex.Message}");
                ShowCustomMessageBox($"重命名分类失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 新增：用于阻止按钮冒泡
        private void OperationButton_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }
        
        // === 分类拖拽排序功能 ===
        
        /// <summary>
        /// 拖拽手柄鼠标按下事件
        /// </summary>
        private void CategoryDragHandle_MouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (e.LeftButton == MouseButtonState.Pressed && sender is Border border)
                {
                    // 获取对应的分类项
                    var listBoxItem = FindParent<ListBoxItem>(border);
                    if (listBoxItem?.DataContext != null)
                    {
                        var cat = listBoxItem.DataContext as Category;
                        if (cat == null) return;
                        _draggedCategory = cat;
                        _startPoint = e.GetPosition(CategoryList);
                        _isDragging = true;
                        
                        // 捕获鼠标，开始拖拽
                        border.CaptureMouse();
                        
                        Console.WriteLine($"[DEBUG] 开始拖拽分类: {cat.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"拖拽手柄鼠标按下处理失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 拖拽手柄鼠标进入事件 - 显示拖拽提示
        /// </summary>
        private void CategoryDragHandle_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Border border && border.Child is TextBlock textBlock)
            {
                // 获取对应的分类项，检查是否是默认分类
                var listBoxItem = FindParent<ListBoxItem>(border);
                if (listBoxItem?.DataContext != null)
                {
                    var categoryName = GetCategoryName(listBoxItem.DataContext);
                    
                    // 只有非默认分类才显示悬停效果
                    if (categoryName != "全部" && categoryName != "已启用" && categoryName != "已禁用")
                    {
                        // 改变图标颜色为高亮状态并显示四个方向箭头光标
                        textBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00D4AA"));
                        border.Cursor = Cursors.SizeAll;
                    }
                }
            }
        }
        
        /// <summary>
        /// 拖拽手柄鼠标离开事件 - 恢复正常状态
        /// </summary>
        private void CategoryDragHandle_MouseLeave(object sender, MouseEventArgs e)
        {
            if (!_isDragging && sender is Border border && border.Child is TextBlock textBlock)
            {
                // 恢复图标颜色为正常状态
                textBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280"));
                border.Cursor = Cursors.Arrow;
            }
        }
        
        /// <summary>
        /// 获取分类名称用于显示
        /// </summary>
        private string GetCategoryName(object categoryObj)
        {
            return categoryObj switch
            {
                Category category => category.Name ?? "",
                UEModManager.Core.Models.CategoryItem categoryItem => categoryItem.Name ?? "",
                _ => ""
            };
        }      
      
        /// <summary>
        /// 重新排序分类项
        /// </summary>
        private void ReorderCategories(object draggedCategory, object targetCategory)
        {
            try
            {
                // 支持多种类型的分类集合
                if (CategoryList?.ItemsSource is ObservableCollection<Category> categories)
                {
                    // 处理Category类型的集合
                    if (draggedCategory is Category draggedCat && targetCategory is Category targetCat)
                    {
                        int draggedIndex = categories.IndexOf(draggedCat);
                        int targetIndex = categories.IndexOf(targetCat);
                        
                        if (draggedIndex != -1 && targetIndex != -1 && draggedIndex != targetIndex)
                        {
                            // 移除拖拽的项目
                            categories.RemoveAt(draggedIndex);
                            
                            // 重新计算目标索引（因为移除了一个项目）
                            if (draggedIndex < targetIndex)
                            {
                                targetIndex--;
                            }
                            
                            // 在目标位置插入
                            categories.Insert(targetIndex, draggedCat);
                            
                            // 保持选中状态
                            CategoryList.SelectedItem = draggedCat;
                            
                            Console.WriteLine($"[DEBUG] 分类重新排序: {GetCategoryName(draggedCategory)} 移动到 {GetCategoryName(targetCategory)} 位置");
                        }
                    }
                }
                else if (CategoryList?.ItemsSource is ObservableCollection<UEModManager.Core.Models.CategoryItem> categoryItems)
                {
                    // 处理CategoryItem类型的集合
                    if (draggedCategory is UEModManager.Core.Models.CategoryItem draggedItem && 
                        targetCategory is UEModManager.Core.Models.CategoryItem targetItem)
                    {
                        int draggedIndex = categoryItems.IndexOf(draggedItem);
                        int targetIndex = categoryItems.IndexOf(targetItem);
                        
                        if (draggedIndex != -1 && targetIndex != -1 && draggedIndex != targetIndex)
                        {
                            // 移除拖拽的项目
                            categoryItems.RemoveAt(draggedIndex);
                            
                            // 重新计算目标索引（因为移除了一个项目）
                            if (draggedIndex < targetIndex)
                            {
                                targetIndex--;
                            }
                            
                            // 在目标位置插入
                            categoryItems.Insert(targetIndex, draggedItem);
                            
                            // 保持选中状态
                            CategoryList.SelectedItem = draggedItem;
                            
                            Console.WriteLine($"[DEBUG] 分类重新排序: {GetCategoryName(draggedCategory)} 移动到 {GetCategoryName(targetCategory)} 位置");
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"[ERROR] 不支持的ItemsSource类型: {CategoryList?.ItemsSource?.GetType()}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"重新排序分类失败: {ex.Message}");
            }
        }

        // Window加载事件
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 初始化配置
            LoadConfiguration();
            
            // 初始化服务
            InitializeServices();
            
            // 检查和恢复游戏配置
            CheckAndRestoreGameConfiguration();
            
            // 为关闭按钮添加事件处理
            Console.WriteLine("[DEBUG] 尝试查找并注册关闭按钮事件...");
            var closeButton = Template.FindName("CloseButton", this) as Button;
            if (closeButton != null)
            {
                closeButton.Click += CloseButton_Click;
                Console.WriteLine("[DEBUG] 成功注册关闭按钮事件处理");
            }
            else
            {
                Console.WriteLine("[ERROR] 无法找到关闭按钮！");
            }
            
            // 为最小化按钮添加事件处理
            Console.WriteLine("[DEBUG] 尝试查找并注册最小化按钮事件...");
            var minimizeButton = Template.FindName("MinimizeButton", this) as Button;
            if (minimizeButton != null)
            {
                minimizeButton.Click += MinimizeButton_Click;
                Console.WriteLine("[DEBUG] 成功注册最小化按钮事件处理");
            }
            else
            {
                Console.WriteLine("[ERROR] 无法找到最小化按钮！");
            }
            
            // 为最大化按钮添加事件处理
            Console.WriteLine("[DEBUG] 尝试查找并注册最大化按钮事件...");
            var maximizeButton = Template.FindName("MaximizeButton", this) as Button;
            if (maximizeButton != null)
            {
                maximizeButton.Click += MaximizeButton_Click;
                Console.WriteLine("[DEBUG] 成功注册最大化按钮事件处理");
            }
            else
            {
                Console.WriteLine("[ERROR] 无法找到最大化按钮！");
            }
        }
        
        // 添加视图切换事件处理程序
        private void CardViewBtn_Click(object sender, RoutedEventArgs e)
        {
            ModsCardView.Visibility = Visibility.Visible;
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
            ModsCardView.Visibility = Visibility.Collapsed;
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
        
        // 分类列表鼠标移动事件 - 用于拖拽
        private void CategoryList_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging && e.LeftButton == MouseButtonState.Pressed && _draggedCategory != null)
            {
                Point currentPosition = e.GetPosition(CategoryList);
                
                // 检查鼠标是否移动了足够的距离开始实际拖拽
                Vector diff = _startPoint - currentPosition;
                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    try
                    {
                        // 启动WPF拖拽操作
                        var dragData = new DataObject("CategoryDragData", _draggedCategory);
                        DragDrop.DoDragDrop(CategoryList, dragData, DragDropEffects.Move);
                        
                        Console.WriteLine($"[DEBUG] 启动拖拽操作: {GetCategoryName(_draggedCategory)}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] 拖拽操作失败: {ex.Message}");
                    }
                    finally
                    {
                        // 重置拖拽状态
                        _isDragging = false;
                        _draggedCategory = null;
                    }
                }
            }
        }
        
        // 分类列表鼠标释放事件 - 结束拖拽
        private void CategoryList_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                // 释放鼠标捕获
                if (e.OriginalSource is DependencyObject depObj)
                {
                    var border = FindParent<Border>(depObj);
                    border?.ReleaseMouseCapture();
                }
                
                // 重置拖拽状态
                _isDragging = false;
                _draggedCategory = null;
                
                Console.WriteLine("[DEBUG] 拖拽分类结束");
            }
        }
        
        // 列表视图滚轮事件处理
        private void ModsListView_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            try
            {
                var scrollViewer = FindParent<ScrollViewer>(ModsListView);
                if (scrollViewer != null && e.Delta != 0)
                {
                    scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 列表视图滚轮事件处理失败: {ex.Message}");
            }
        }



        private void Card_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // 现在卡片视图直接使用主ScrollViewer，不需要特殊处理
            // 让事件正常冒泡到主ScrollViewer即可
        }

        // 列表视图选择变更事件处理
        private void ModsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (ModsListView.SelectedItem is Mod selectedMod)
                {
                    // 更新所有MOD的选中状态
                    foreach (var mod in allMods)
                    {
                        mod.IsSelected = ModsListView.SelectedItems.Contains(mod);
                    }

                    // 更新详情面板
                    UpdateModDetails(selectedMod);
                    _lastSelectedMod = selectedMod;
                    
                    // 更新全选框状态
                    UpdateSelectAllCheckBoxState();
                }
                else if (ModsListView.SelectedItems.Count == 0)
                {
                    // 清除详情面板
                    ClearModDetails();
                    _lastSelectedMod = null;
                    
                    // 更新全选框状态
                    UpdateSelectAllCheckBoxState();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"列表视图选择变更处理失败: {ex.Message}");
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("[DEBUG] 关闭按钮被点击，正在关闭窗口...");
            this.Close();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("[DEBUG] 最小化按钮被点击，正在最小化窗口...");
            this.WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("[DEBUG] 最大化按钮被点击，正在切换窗口状态...");
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
            }
            else
            {
                this.WindowState = WindowState.Maximized;
            }
        }

        // 全局鼠标点击事件处理，用于关闭标签菜单  
        private void MainWindow_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                Console.WriteLine("[DEBUG] 全局点击检测，尝试关闭所有打开的标签菜单");
                
                // 简单地关闭所有可能打开的ContextMenu
                CloseCurrentTypeSelectionPopup();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 处理全局点击事件时出错: {ex.Message}");
            }
        }

        // 拖拽排序相关
        private Point _dragStartPoint;
        private Category? _draggedCategory;
        private DragAdorner? _dragAdorner;
        private Point _dragStartPointOnItem;

        private void DragHandle_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var border = sender as Border;
            if (border == null) return;
            var listBoxItem = FindParent<ListBoxItem>(border);
            if (listBoxItem == null) return;
            var category = listBoxItem.DataContext as Category;

            if (category == null || IsDefaultCategory(category)) return;

            // 1. 为被拖拽的项创建一张位图快照
            var bmp = new RenderTargetBitmap((int)listBoxItem.ActualWidth, (int)listBoxItem.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(listBoxItem);
            bmp.Freeze(); 

            // 2. 获取Adorner层
            var adornerLayer = AdornerLayer.GetAdornerLayer(CategoryList);
            if (adornerLayer == null) return;

            // 3. 创建并添加使用位图快照的Adorner
            _dragAdorner = new DragAdorner(CategoryList, bmp, listBoxItem.RenderSize);
            adornerLayer.Add(_dragAdorner);
            
            // 4. 更新Adorner的初始位置，使其跟随鼠标
            _dragStartPointOnItem = e.GetPosition(listBoxItem);
            Point initialPosition = e.GetPosition(CategoryList);
            _dragAdorner.UpdatePosition(new Point(initialPosition.X - _dragStartPointOnItem.X, initialPosition.Y - _dragStartPointOnItem.Y));

            // 5. 现在可以安全地隐藏原始项
            listBoxItem.Visibility = Visibility.Hidden;
            
            DragDrop.DoDragDrop(listBoxItem, category, DragDropEffects.Move);

            // ----- 拖拽结束后执行清理 -----
            
            if (_dragAdorner != null)
            {
                adornerLayer.Remove(_dragAdorner);
                _dragAdorner = null;
            }

            listBoxItem.Visibility = Visibility.Visible;
            
            e.Handled = true;
        }

        private void CategoryList_DragOver(object sender, DragEventArgs e)
        {
            if (_dragAdorner != null)
            {
                Point currentPosition = e.GetPosition(CategoryList);
                // 更新Adorner的位置，减去起始点偏移，使拖拽更自然
                _dragAdorner.UpdatePosition(new Point(currentPosition.X - _dragStartPointOnItem.X, currentPosition.Y - _dragStartPointOnItem.Y));
            }

            var target = GetCategoryItemAtPosition(e.GetPosition(CategoryList)) as Category;
            var dragged = e.Data.GetData(typeof(Category)) as Category;
            
            // 默认不允许放置
            e.Effects = DragDropEffects.None;

            if (target != null && dragged != null && target != dragged && !IsDefaultCategory(target))
            {
                // 仅当目标有效时，才允许移动
                e.Effects = DragDropEffects.Move;
            }

            e.Handled = true;
        }

        private void CategoryList_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(typeof(Category))) return;
            var dragged = e.Data.GetData(typeof(Category)) as Category;
            var target = GetCategoryItemAtPosition(e.GetPosition(CategoryList)) as Category;
            if (dragged == null || target == null || dragged == target) return;
            if (IsDefaultCategory(target)) return;
            int oldIndex = categories.IndexOf(dragged);
            int newIndex = categories.IndexOf(target);
            if (oldIndex < 0 || newIndex < 0 || oldIndex == newIndex) return;
            categories.Move(oldIndex, newIndex);
        }

        private object? GetCategoryItemAtPosition(Point position)
        {
            var element = CategoryList.InputHitTest(position) as DependencyObject;
            while (element != null && !(element is ListBoxItem))
            {
                element = VisualTreeHelper.GetParent(element);
            }
            return (element as ListBoxItem)?.DataContext;
        }

        private bool IsDefaultCategory(Category category)
        {
            return category.Name == "全部" || category.Name == "已启用" || category.Name == "已禁用";
        }

        // 添加批量操作标志
        private bool IsInBatchOperation = false;
        
        // 统一应用GroupBox样式
        private void ApplyGroupBoxStyle(GroupBox groupBox)
        {
            groupBox.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1f2937"));
            groupBox.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#374151"));
            groupBox.BorderThickness = new Thickness(1);
            groupBox.Padding = new Thickness(10);
        }
        
        // 统一应用TextBox样式
        private void ApplyTextBoxStyle(TextBox textBox)
        {
            textBox.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1e293b"));
            textBox.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f1f5f9"));
            textBox.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#475569"));
            textBox.BorderThickness = new Thickness(1);
            textBox.Padding = new Thickness(8);
            textBox.FontSize = 14;
        }
        
        // 统一应用按钮样式
        private void ApplyButtonStyle(Button button, bool isPrimary = false)
        {
            // 设置基本样式
            button.BorderThickness = new Thickness(0);
            button.Padding = new Thickness(15, 8, 15, 8);
            button.FontSize = 14;
            
            // 根据按钮类型设置颜色
            if (isPrimary)
            {
                button.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#84cc16"));
            }
            else
            {
                button.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#475569"));
            }
            
            button.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f1f5f9"));
            
            // 添加圆角效果
            button.Template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.Name = "border";
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
            
            var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            contentPresenter.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
            
            border.AppendChild(contentPresenter);
            
            var template = new ControlTemplate(typeof(Button));
            template.VisualTree = border;
            
            // 添加鼠标悬停效果
            var trigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            if (isPrimary)
            {
                trigger.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#a3e635"))));
            }
            else
            {
                trigger.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748b"))));
            }
            template.Triggers.Add(trigger);
            
            button.Template = template;
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
                _status = value;
                OnPropertyChanged(nameof(Status));
            }
        }
        
        // 新增：MOD所属分类
        private List<string> _categories = new List<string> { "未分类" };
        public List<string> Categories
        {
            get => _categories;
            set
            {
                _categories = value ?? new List<string> { "未分类" };
                OnPropertyChanged(nameof(Categories));
            }
        }
        
        // 新增：是否被选中状态
        private bool _isSelected = false;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }
        
        public string Icon { get; set; } = "";
        public string Size { get; set; } = "";
        public string ImportDate { get; set; } = "";
        public int UsageCount { get; set; }
        public double Rating { get; set; }
        
        // 新增预览图路径属性
        private string _previewImagePath = "";
        public string PreviewImagePath 
        { 
            get => _previewImagePath; 
            set
            {
                _previewImagePath = value;
                OnPropertyChanged(nameof(PreviewImagePath));
            }
        }

        private ImageSource? _previewImageSource;
        [System.Text.Json.Serialization.JsonIgnore]
        public ImageSource? PreviewImageSource
        {
            get => _previewImageSource;
            set
            {
                _previewImageSource = value;
                OnPropertyChanged(nameof(PreviewImageSource));
            }
        }
        
        // 内部方法：直接设置预览图源，不触发属性更改通知
        internal void SetPreviewImageSourceDirect(ImageSource? imageSource)
        {
            _previewImageSource = imageSource;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        
        public virtual void OnPropertyChanged(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                Console.WriteLine("[WARNING] OnPropertyChanged called with null or empty propertyName");
                return;
            }
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // === 配置类 ===
    public class AppConfig
    {
        public string? GameName { get; set; }
        public string? GamePath { get; set; }
        public string? ModPath { get; set; }
        public string? BackupPath { get; set; }
        public string? ExecutableName { get; set; }
        public List<string> CustomGames { get; set; } = new List<string>(); // 存储自定义添加的游戏
    }

    // 转换器类定义
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value == null ? Visibility.Collapsed : Visibility.Visible;
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
            return value == null ? Visibility.Visible : Visibility.Collapsed;
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
            Console.WriteLine($"[DEBUG] CategoryTypeVisibilityConverter: value={value}, type={value?.GetType()?.Name}");
            var result = value is Category ? Visibility.Visible : Visibility.Collapsed;
            Console.WriteLine($"[DEBUG] CategoryTypeVisibilityConverter: result={result}");
            return result;
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
            Console.WriteLine($"[DEBUG] CategoryItemTypeVisibilityConverter: value={value}, type={value?.GetType()?.Name}");
            var result = value is UEModManager.Core.Models.CategoryItem ? Visibility.Visible : Visibility.Collapsed;
            Console.WriteLine($"[DEBUG] CategoryItemTypeVisibilityConverter: result={result}");
            return result;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }


} 



