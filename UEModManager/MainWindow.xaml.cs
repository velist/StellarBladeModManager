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
using System.Windows.Media.Imaging;
using System.Windows.Controls.Primitives;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Windows.Threading;
using Microsoft.Win32;
using System.Text.Json;
using System.Diagnostics;
using UEModManager.ViewModels;
using System.Collections;
using System.Threading;
using System.Globalization;
using System.Threading.Tasks;
using UEModManager.Core.Services;
using UEModManager.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// 解决Path命名冲突
using IOPath = System.IO.Path;

namespace UEModManager
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
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
        // 添加CategoryService支持
        private CategoryService? _categoryService;
        private ModService? _modService;
        private ILogger<MainWindow>? _logger;

        // 主构造函数
        public MainWindow()
        {
            try
            {
                // 分配控制台窗口以便调试（仅在Debug模式下）
                #if DEBUG
                AllocConsole(); // 启用控制台窗口
                Console.WriteLine("=== UEModManager Debug Console ===");
                Console.WriteLine($"程序启动时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                #endif

                InitializeComponent();
                
                // 启用拖拽功能
                AllowDrop = true;
                DragEnter += MainWindow_DragEnter;
                DragOver += MainWindow_DragOver;
                Drop += MainWindow_Drop;

                // 立即加载配置，不延迟
                LoadConfiguration();
                InitializeData();
                SetupEventHandlers();
                
                // 改为窗口加载完成后立即同步检查配置
                this.Loaded += (s, e) => {
                    CheckAndRestoreGameConfiguration();
                };
                
                // 添加关闭事件处理，保存分类数据
                this.Closing += MainWindow_Closing;
                
                StartStatsTimer();
                
                // Console.WriteLine("MainWindow 初始化完成");
            }
            catch (Exception ex)
            {
                // Console.WriteLine($"主窗口初始化失败: {ex.Message}");
                ShowCustomMessageBox($"主窗口初始化失败: {ex.Message}", "初始化错误", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        // Win32 API 用于分配控制台窗口
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool AllocConsole();

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
                                ExecutableName = currentExecutableName
                            };
                            var updatedJson = JsonSerializer.Serialize(updatedConfig, new JsonSerializerOptions { WriteIndented = true });
                            File.WriteAllText(configFilePath, updatedJson);
                            Console.WriteLine("[DEBUG] 配置文件已更新为正确的备份路径");
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
                var config = new AppConfig 
                { 
                    GameName = currentGameName,
                    GamePath = currentGamePath,
                    ModPath = currentModPath,
                    BackupPath = currentBackupPath,
                    ExecutableName = executableName
                };
                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(configFilePath, json);
                
                Console.WriteLine($"[DEBUG] 配置已保存 - 游戏: {currentGameName}, 执行程序: {executableName}");
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
            ModsGrid.ItemsSource = allMods;

            // 清空详情面板
            ClearModDetails();
            
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
                    
                    if (gameName != "请选择游戏" && gameName != currentGameName)
                    {
                        Console.WriteLine($"[DEBUG] 准备切换游戏从 '{currentGameName}' 到 '{gameName}'");
                        
                        // 如果已经有游戏配置，显示切换确认
                        if (!string.IsNullOrEmpty(currentGameName))
                        {
                            Console.WriteLine($"[DEBUG] 显示切换确认对话框");
                            var result = MessageBox.Show(
                                $"您即将从 '{currentGameName}' 切换到 '{gameName}'。\n\n" +
                                "这将重新配置游戏路径并重新扫描MOD文件。\n" +
                                "当前的MOD状态会被保存。\n\n" +
                                "是否确认切换？",
                                "切换游戏确认",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Question
                            );
                            
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
                    
                    MessageBox.Show(
                        $"游戏 '{gameName}' 配置完成！\n\n" +
                        $"游戏路径: {currentGamePath}{executableInfo}\n" +
                        $"MOD路径: {currentModPath}\n" +
                        $"备份路径: {currentBackupPath}\n\n" +
                        $"已扫描到 {allMods.Count} 个MOD",
                        "配置成功",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
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
                ModsGrid.ItemsSource = null;
                
                // 强制多次垃圾回收，确保释放文件锁定
                for (int i = 0; i < 3; i++)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
                
                // 强制界面更新
                ModsGrid.UpdateLayout();
                
                // 重新加载所有MOD的预览图
                foreach (var mod in allMods)
                {
                    if (!string.IsNullOrEmpty(mod.PreviewImagePath))
                    {
                        LoadModPreviewImage(mod);
                    }
                }
                
                // 重新设置数据源
                ModsGrid.ItemsSource = allMods;
                
                // 强制重新绘制
                ModsGrid.InvalidateVisual();
                
                UpdateModCountDisplay();
                
                Console.WriteLine($"MOD显示刷新完成，共显示 {allMods.Count} 个MOD");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"刷新MOD显示失败: {ex.Message}");
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
                            // 删除备份目录中的文件
                            if (!string.IsNullOrEmpty(currentBackupPath))
                            {
                                var backupFiles = Directory.GetFiles(currentBackupPath, $"{mod.Name}.*", SearchOption.TopDirectoryOnly);
                                foreach (var file in backupFiles)
                                {
                                    File.Delete(file);
                                }
                            }

                            // 删除MOD目录中的文件
                            if (!string.IsNullOrEmpty(currentModPath))
                            {
                                var modFiles = Directory.GetFiles(currentModPath, $"{mod.Name}.*", SearchOption.TopDirectoryOnly);
                                foreach (var file in modFiles)
                                {
                                    File.Delete(file);
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
                Width = 400,
                Height = 200,
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
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetRow(promptLabel, 0);

            var inputBox = new TextBox
            {
                Text = defaultValue,
                Margin = new Thickness(20, 0, 20, 10),
                Padding = new Thickness(8),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A2433")),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A3441")),
                BorderThickness = new Thickness(1)
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
                Width = 80,
                Height = 30,
                Margin = new Thickness(0, 0, 10, 0),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FBBF24")),
                Foreground = Brushes.Black,
                BorderThickness = new Thickness(0)
            };

            var cancelButton = new Button
            {
                Content = "取消",
                Width = 80,
                Height = 30,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A5568")),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0)
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
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0B1426")),
                WindowStyle = WindowStyle.None,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A2332")),
                BorderThickness = new Thickness(1)
            };

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 标题栏
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 内容
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 按钮

            // 自定义标题栏
            var titleBar = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A2332")),
                Padding = new Thickness(15),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A3441")),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };

            var titleGrid = new Grid();
            var titleText = new TextBlock
            {
                Text = title,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F5F9")),
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center
            };

            var closeButton = new Button
            {
                Content = "✕",
                Width = 30,
                Height = 30,
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9CA3AF")),
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Right,
                FontSize = 14,
                Cursor = Cursors.Hand
            };

            titleGrid.Children.Add(titleText);
            titleGrid.Children.Add(closeButton);
            titleBar.Child = titleGrid;
            Grid.SetRow(titleBar, 0);

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
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F5F9")),
                FontSize = 14,
                Margin = new Thickness(0, 20, 20, 20),
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(messageText, 1);

            contentGrid.Children.Add(iconBlock);
            contentGrid.Children.Add(messageText);
            Grid.SetRow(contentGrid, 1);

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

            Grid.SetRow(buttonPanel, 2);

            // 关闭按钮事件
            closeButton.Click += (s, e) => { result = MessageBoxResult.Cancel; messageWindow.Close(); };

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

            mainGrid.Children.Add(titleBar);
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
                button.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FBBF24"));
                button.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E2A3A"));
                button.FontWeight = FontWeights.Bold;
            }
            else
            {
                button.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A5568"));
                button.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F5F9"));
            }

            // 添加鼠标悬停效果
            button.MouseEnter += (s, e) =>
            {
                if (isPrimary)
                {
                    button.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));
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
                    button.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FBBF24"));
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
                            MessageBox.Show($"导入文件 {IOPath.GetFileName(file)} 失败: {fileEx.Message}", "导入失败", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"拖拽导入失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show($"切换MOD状态失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    MessageBox.Show("请先选择一个MOD", "重命名失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var newName = ShowInputDialog("重命名MOD", "请输入新的显示名称:", selectedMod.Name);
                if (!string.IsNullOrWhiteSpace(newName) && newName != selectedMod.Name)
                {
                    Console.WriteLine($"重命名MOD显示名称: {selectedMod.Name} -> {newName} (真实名称保持: {selectedMod.RealName})");
                    
                    // 只更改显示名称，RealName保持不变用于文件操作
                    selectedMod.Name = newName;
                    
                    // 更新详情显示
                    UpdateModDetails(selectedMod);
                    
                    // 刷新MOD列表显示
                    RefreshModDisplay();
                    
                    MessageBox.Show($"MOD显示名称已更改为: {newName}\n(文件操作仍使用原名称: {selectedMod.RealName})", 
                                  "重命名成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    Console.WriteLine($"MOD重命名完成，显示名: {selectedMod.Name}, 真实名: {selectedMod.RealName}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"重命名MOD失败: {ex.Message}");
                MessageBox.Show($"重命名失败: {ex.Message}", "重命名失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ChangePreviewButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (selectedMod == null)
                {
                    MessageBox.Show("请先选择一个MOD", "修改预览图失败", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                MessageBox.Show($"修改预览图失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show($"禁用MOD失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                            // 删除备份目录中的文件
                            if (!string.IsNullOrEmpty(currentBackupPath))
                            {
                                var backupFiles = Directory.GetFiles(currentBackupPath, $"{selectedMod.Name}.*", SearchOption.TopDirectoryOnly);
                                foreach (var file in backupFiles)
                                {
                                    File.Delete(file);
                                }
                            }

                            // 删除MOD目录中的文件
                            if (!string.IsNullOrEmpty(currentModPath))
                            {
                                var modFiles = Directory.GetFiles(currentModPath, $"{selectedMod.Name}.*", SearchOption.TopDirectoryOnly);
                                foreach (var file in modFiles)
                                {
                                    File.Delete(file);
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
                var result = MessageBox.Show("确定要禁用所有MOD吗？", "确认禁用", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    foreach (var mod in allMods.ToList())
                    {
                        if (mod.Status == "已启用")
                        {
                            DisableMod(mod);
                        }
                    }
                    RefreshModDisplay();
                    if (selectedMod != null)
                    {
                        UpdateModDetails(selectedMod);
                    }
                    UpdateCategoryCount();
                    MessageBox.Show("已禁用所有MOD", "禁用成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"批量禁用失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EnableAllButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                foreach (var mod in allMods.ToList())
                {
                    if (mod.Status == "已禁用")
                    {
                        EnableMod(mod);
                    }
                }
                RefreshModDisplay();
                if (selectedMod != null)
                {
                    UpdateModDetails(selectedMod);
                }
                UpdateCategoryCount();
                MessageBox.Show("已启用所有MOD", "启用成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"批量启用失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    MessageBox.Show("没有启用的MOD可删除", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var result = MessageBox.Show($"确定要删除 {enabledMods.Count} 个已启用的MOD吗？", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    foreach (var mod in enabledMods.ToList())
                    {
                        try
                        {
                            // 删除备份目录中的文件
                            if (!string.IsNullOrEmpty(currentBackupPath))
                            {
                                var backupFiles = Directory.GetFiles(currentBackupPath, $"{mod.Name}.*", SearchOption.TopDirectoryOnly);
                                foreach (var file in backupFiles)
                                {
                                    File.Delete(file);
                                }
                            }

                            // 删除MOD目录中的文件
                            if (!string.IsNullOrEmpty(currentModPath))
                            {
                                var modFiles = Directory.GetFiles(currentModPath, $"{mod.Name}.*", SearchOption.TopDirectoryOnly);
                                foreach (var file in modFiles)
                                {
                                    File.Delete(file);
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
                    
                    MessageBox.Show($"已删除 {enabledMods.Count} 个MOD", "删除成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"批量删除失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    Height = 420,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0B1426")),
                    WindowStyle = WindowStyle.None,
                    AllowsTransparency = true,
                    ResizeMode = ResizeMode.NoResize
                };

                var mainBorder = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0B1426")),
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A3441")),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8)
                };

                var grid = new Grid();
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(45) }); // 标题栏
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 名称输入
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 描述输入
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(55) }); // 按钮区域

                // 标题栏
                var titleBar = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A2332")),
                    CornerRadius = new CornerRadius(8, 8, 0, 0)
                };
                Grid.SetRow(titleBar, 0);

                var titleGrid = new Grid();
                var titleText = new TextBlock
                {
                    Text = "编辑MOD",
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F5F9")),
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(20, 0, 0, 0)
                };

                var closeButton = new Button
                {
                    Content = "✕",
                    Width = 30,
                    Height = 30,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9CA3AF")),
                    FontSize = 14,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 10, 0),
                    Cursor = Cursors.Hand
                };
                closeButton.Click += (s, e) => dialog.Close();

                titleGrid.Children.Add(titleText);
                titleGrid.Children.Add(closeButton);
                titleBar.Child = titleGrid;

                // 名称输入区域
                var namePanel = new StackPanel
                {
                    Margin = new Thickness(20, 15, 20, 8)
                };
                Grid.SetRow(namePanel, 1);

                var nameLabel = new TextBlock
                {
                    Text = "MOD名称:",
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F5F9")),
                    FontSize = 14,
                    Margin = new Thickness(0, 0, 0, 5)
                };

                var nameTextBox = new TextBox
                {
                    Text = mod.Name,
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A2433")),
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F5F9")),
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A3441")),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(10),
                    FontSize = 14,
                    Height = 35
                };

                namePanel.Children.Add(nameLabel);
                namePanel.Children.Add(nameTextBox);

                // 描述输入区域
                var descPanel = new StackPanel
                {
                    Margin = new Thickness(20, 8, 20, 12)
                };
                Grid.SetRow(descPanel, 2);

                var descLabel = new TextBlock
                {
                    Text = "MOD描述:",
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F5F9")),
                    FontSize = 14,
                    Margin = new Thickness(0, 0, 0, 5)
                };

                var descTextBox = new TextBox
                {
                    Text = mod.Description,
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A2433")),
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F5F9")),
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A3441")),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(10),
                    FontSize = 14,
                    TextWrapping = TextWrapping.Wrap,
                    AcceptsReturn = true,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                };

                descPanel.Children.Add(descLabel);
                descPanel.Children.Add(descTextBox);

                // 按钮区域
                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(20, 12, 20, 15)
                };
                Grid.SetRow(buttonPanel, 3);

                var cancelButton = CreateMessageBoxButton("取消", false);
                cancelButton.Click += (s, e) => dialog.Close();
                cancelButton.Margin = new Thickness(0, 0, 10, 0);

                var saveButton = CreateMessageBoxButton("保存", true);
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

                grid.Children.Add(titleBar);
                grid.Children.Add(namePanel);
                grid.Children.Add(descPanel);
                grid.Children.Add(buttonPanel);

                mainBorder.Child = grid;
                dialog.Content = mainBorder;

                // 支持ESC关闭
                dialog.KeyDown += (s, e) =>
                {
                    if (e.Key == Key.Escape)
                    {
                        dialog.Close();
                    }
                };

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
            SearchPlaceholder.Visibility = Visibility.Collapsed;
        }

        private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var searchBox = sender as TextBox;
            var placeholder = FindName("SearchPlaceholder") as TextBlock;
            if (searchBox != null && placeholder != null && string.IsNullOrWhiteSpace(searchBox.Text))
            {
                placeholder.Visibility = Visibility.Visible;
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
            ModsGrid.ItemsSource = null;
            ModsGrid.ItemsSource = filteredMods;
            
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
                // 使用Popup而不是ContextMenu来避免立即关闭问题
                var popup = new Popup
                {
                    PlacementTarget = element,
                    Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
                    AllowsTransparency = true,
                    PopupAnimation = PopupAnimation.Fade,
                    StaysOpen = true  // 改为true，手动控制关闭
                };

                var border = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(42, 52, 65)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(75, 85, 99)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(5)
                };

                var stackPanel = new StackPanel();
                var types = new[] { "👥 面部", "👤 人物", "⚔️ 武器", "👕 服装", "🔧 修改", "📦 其他" };
                
                foreach (var type in types)
                {
                    var typeText = type.Substring(2).Trim(); // 移除emoji前缀并清理空格
                    var button = new Button
                    {
                        Content = type,
                        Background = mod.Type == typeText ? 
                            new SolidColorBrush(Color.FromRgb(16, 185, 129)) : 
                            Brushes.Transparent,
                        Foreground = Brushes.White,
                        BorderThickness = new Thickness(0),
                        Padding = new Thickness(10, 5, 10, 5),
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        HorizontalContentAlignment = HorizontalAlignment.Left,
                        FontWeight = mod.Type == typeText ? FontWeights.Bold : FontWeights.Normal,
                        Cursor = Cursors.Hand
                    };
                    
                    // 鼠标悬停效果
                    button.MouseEnter += (s, e) =>
                    {
                        if (mod.Type != typeText)
                        {
                            button.Background = new SolidColorBrush(Color.FromRgb(75, 85, 99));
                        }
                    };
                    button.MouseLeave += (s, e) =>
                    {
                        if (mod.Type != typeText)
                        {
                            button.Background = Brushes.Transparent;
                        }
                    };
                    
                    button.Click += (s, e) =>
                    {
                        try
                        {
                            Console.WriteLine($"[DEBUG] 更改MOD {mod.Name} 的类型从 '{mod.Type}' 到 '{typeText}'");
                            
                            // 更新MOD的类型
                            mod.Type = typeText;
                            
                            // 同时更新MOD的分类，将类型作为分类
                            mod.Categories.Clear();
                            mod.Categories.Add(typeText);
                            
                            // 关闭弹窗
                            popup.IsOpen = false;
                            
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
                    
                    stackPanel.Children.Add(button);
                }

                border.Child = stackPanel;
                popup.Child = border;
                
                // 添加弹窗关闭事件处理
                popup.Closed += (s, e) => {
                    Console.WriteLine("[DEBUG] 类型选择弹窗已关闭");
                };
                
                // 添加点击外部关闭功能
                popup.MouseDown += (s, e) => {
                    if (e.OriginalSource == popup)
                    {
                        popup.IsOpen = false;
                    }
                };
                
                // 添加失去焦点关闭功能（延迟执行避免立即关闭）
                DispatcherTimer closeTimer = null;
                popup.LostFocus += (s, e) => {
                    closeTimer?.Stop();
                    closeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
                    closeTimer.Tick += (sender, args) => {
                        closeTimer.Stop();
                        if (!popup.IsKeyboardFocusWithin && !popup.IsMouseOver)
                        {
                            popup.IsOpen = false;
                        }
                    };
                    closeTimer.Start();
                };
                
                // 鼠标进入时取消关闭
                popup.MouseEnter += (s, e) => {
                    closeTimer?.Stop();
                };
                
                popup.IsOpen = true;
                Console.WriteLine($"[DEBUG] 显示类型选择弹窗，当前MOD类型: {mod.Type}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 显示类型选择弹窗失败: {ex.Message}");
                MessageBox.Show($"显示类型选择弹窗失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
        private void AddSubCategoryMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var categoryName = ShowInputDialog("请输入子分类名称:", "新增子分类");
            if (!string.IsNullOrEmpty(categoryName))
            {
                var selectedCategory = CategoryList.SelectedItem as Category;
                var parentName = selectedCategory?.Name ?? "全部";
                var fullName = parentName == "全部" ? categoryName : $"{parentName} > {categoryName}";
                
                categories.Add(new Category { Name = fullName, Count = 0 });
                RefreshCategoryDisplay();
            }
        }

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
                var newName = ShowInputDialog("请输入新的MOD名称:", "重命名MOD", mod.Name);
                if (!string.IsNullOrEmpty(newName) && newName != mod.Name)
                {
                    mod.Name = newName;
                    RefreshModDisplay();
                    if (selectedMod == mod)
                    {
                        UpdateModDetails(mod);
                    }
                }
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
                    MessageBox.Show("暂无可用分类，请先创建分类", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 获取自定义分类列表
                var categoryNames = _categoryService.Categories
                    .Where(c => !new[] { "全部", "已启用", "已禁用" }.Contains(c.Name))
                    .Select(c => c.Name)
                    .ToList();

                if (!categoryNames.Any())
                {
                    MessageBox.Show("暂无自定义分类，请先创建分类", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
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
                    MessageBox.Show($"MOD '{mod.Name}' 已移动到分类 '{selectedCategory}'", "移动成功", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else if (!string.IsNullOrEmpty(selectedCategory))
                {
                    MessageBox.Show($"分类 '{selectedCategory}' 不存在，请输入有效的分类名称", "分类不存在", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"显示移动分类对话框失败: {ex.Message}");
                MessageBox.Show($"显示分类选择失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    // 删除备份文件夹中的文件
                    if (Directory.Exists(currentBackupPath))
                    {
                        var backupFiles = Directory.GetFiles(currentBackupPath, $"{mod.Name}.*");
                        foreach (var file in backupFiles)
                        {
                            File.Delete(file);
                        }
                    }
                    
                    // 删除MOD文件夹中的文件
                    if (Directory.Exists(currentModPath))
                    {
                        var modFiles = Directory.GetFiles(currentModPath, $"{mod.Name}.*");
                        foreach (var file in modFiles)
                        {
                            File.Delete(file);
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

                var visibleMods = (ModsGrid.ItemsSource as IEnumerable<Mod>)?.ToList();
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
                var currentMods = ModsGrid.ItemsSource as IEnumerable<Mod>;
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
                    ModsGrid.InvalidateVisual();
                    ModsGrid.UpdateLayout();
                    
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
                MessageBox.Show($"刷新失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                if (ModCountText != null)
                {
                    var selectedItem = CategoryList.SelectedItem;
                    string categoryName = "全部";
                    int modCount = allMods.Count;
                    
                    if (selectedItem is Category category)
                    {
                        categoryName = category.Name;
                        modCount = category.Count;
                    }
                    else if (selectedItem is UEModManager.Core.Models.CategoryItem categoryItem)
                    {
                        categoryName = categoryItem.Name;
                        modCount = categoryItem.ModCount;
                    }
                    
                    // 获取当前显示的MOD数量（考虑筛选结果）
                    var currentMods = ModsGrid.ItemsSource as IEnumerable<Mod>;
                    if (currentMods != null)
                    {
                        modCount = currentMods.Count();
                    }
                    
                    ModCountText.Text = $"{categoryName} ({modCount})";
                    Console.WriteLine($"[DEBUG] 更新C1区标题: {categoryName} ({modCount})");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"更新MOD数量显示失败: {ex.Message}");
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
                    MessageBox.Show($"找不到MOD '{mod.Name}' 的备份文件。\n备份目录: {modBackupDir}", 
                        "启用失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 获取备份目录中的所有MOD文件（排除预览图）
                var backupFiles = Directory.GetFiles(modBackupDir, "*.*", SearchOption.AllDirectories)
                    .Where(f => !IOPath.GetFileName(f).StartsWith("preview"))
                    .ToList();

                if (backupFiles.Count == 0)
                {
                    Console.WriteLine($"[ERROR] 备份目录中没有找到MOD文件: {modBackupDir}");
                    MessageBox.Show($"MOD '{mod.Name}' 的备份目录中没有找到MOD文件。", 
                        "启用失败", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                MessageBox.Show($"MOD '{mod.Name}' 已启用！\n复制了 {copiedCount} 个文件。", 
                    "启用成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 启用MOD失败: {ex.Message}");
                Console.WriteLine($"[ERROR] 堆栈跟踪: {ex.StackTrace}");
                MessageBox.Show($"启用MOD '{mod.Name}' 失败: {ex.Message}", 
                    "启用失败", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    MessageBox.Show($"MOD '{mod.Name}' 已禁用。\n(游戏目录中未找到MOD文件)", 
                        "禁用完成", MessageBoxButton.OK, MessageBoxImage.Information);
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
                MessageBox.Show($"MOD '{mod.Name}' 已禁用！\n已删除 {filesToDelete.Length} 个文件。", 
                    "禁用成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"[ERROR] 禁用MOD失败 - 访问被拒绝: {ex.Message}");
                MessageBox.Show($"禁用MOD '{mod.Name}' 失败：文件被占用或权限不足。\n请关闭游戏后重试。", 
                    "禁用失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (DirectoryNotFoundException ex)
            {
                Console.WriteLine($"[WARN] MOD目录已不存在: {ex.Message}");
                mod.Status = "已禁用";
                MessageBox.Show($"MOD '{mod.Name}' 已禁用。\n(目录已不存在)", 
                    "禁用完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 禁用MOD失败: {ex.Message}");
                Console.WriteLine($"[ERROR] 堆栈跟踪: {ex.StackTrace}");
                MessageBox.Show($"禁用MOD '{mod.Name}' 失败: {ex.Message}", 
                    "禁用失败", MessageBoxButton.OK, MessageBoxImage.Error);
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
                statsTimer.Interval = TimeSpan.FromSeconds(1);
                statsTimer.Tick += (s, e) => {
                    // 更新统计信息
                    UpdateModCountDisplay();
                };
                statsTimer.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"启动统计计时器失败: {ex.Message}");
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
                if (CategoryList.SelectedItem == null)
                {
                    // 没有选中分类，显示所有MOD
                    ModsGrid.ItemsSource = allMods;
                    Console.WriteLine($"[DEBUG] 未选中分类，显示所有MOD: {allMods.Count} 个");
                    return;
                }

                var selectedItem = CategoryList.SelectedItem;
                List<Mod> filteredMods = new List<Mod>();

                if (selectedItem is Category category)
                {
                    // 处理默认分类
                    switch (category.Name)
                    {
                        case "全部":
                            filteredMods = allMods.ToList();
                            break;
                        default:
                            // 其他分类，基于MOD的Type筛选
                            filteredMods = allMods.Where(m => m.Type == category.Name || 
                                (m.Categories != null && m.Categories.Contains(category.Name))).ToList();
                            break;
                    }
                }
                else if (selectedItem is UEModManager.Core.Models.CategoryItem categoryItem)
                {
                    // 处理CategoryService分类
                    if (categoryItem.Name == "全部")
                    {
                        filteredMods = allMods.ToList();
                    }
                    else
                    {
                        // 根据MOD的分类属性筛选
                        filteredMods = allMods.Where(m => m.Categories.Contains(categoryItem.Name)).ToList();
                        Console.WriteLine($"[DEBUG] 选中分类 '{categoryItem.Name}'，找到 {filteredMods.Count} 个MOD");
                    }
                }

                ModsGrid.ItemsSource = filteredMods;
                Console.WriteLine($"[DEBUG] 按分类筛选MOD完成，显示 {filteredMods.Count} 个MOD");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"按分类筛选MOD失败: {ex.Message}");
            }
        }

        // 搜索框文本变化事件
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                // 处理搜索文本变化
                RefreshModDisplay();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"搜索文本变化处理失败: {ex.Message}");
            }
        }
        
        // 全选CheckBox选中事件
        private void SelectAllCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                SetAllModsSelection(true);
                Console.WriteLine("[DEBUG] 全选MOD");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"全选MOD失败: {ex.Message}");
            }
        }
        
        // 全选CheckBox取消选中事件  
        private void SelectAllCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            try
            {
                SetAllModsSelection(false);
                Console.WriteLine("[DEBUG] 取消全选MOD");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"取消全选MOD失败: {ex.Message}");
            }
        }
        
        // 设置所有MOD的选中状态
        private void SetAllModsSelection(bool isSelected)
        {
            try
            {
                var currentMods = ModsGrid.ItemsSource as IEnumerable<Mod>;
                if (currentMods != null)
                {
                    foreach (var mod in currentMods)
                    {
                        mod.IsSelected = isSelected;
                    }
                    Console.WriteLine($"[DEBUG] 设置 {currentMods.Count()} 个MOD的选中状态为: {isSelected}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"设置MOD选中状态失败: {ex.Message}");
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
                MessageBox.Show($"导入MOD失败: {ex.Message}", "导入错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    MessageBox.Show("文件不存在", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    MessageBox.Show($"不支持的文件格式: {fileExtension}\n支持的格式: .pak, .ucas, .utoc, .zip, .rar, .7z", 
                        "格式错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (importSuccess)
                {
                    // 重新扫描MOD，更新列表
                    InitializeModsForGame();
                    
                    MessageBox.Show($"MOD '{fileName}' 导入成功！", "导入成功", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    Console.WriteLine($"[DEBUG] MOD导入成功: {fileName}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"从文件导入MOD失败: {ex.Message}");
                Console.WriteLine($"[ERROR] 堆栈跟踪: {ex.StackTrace}");
                MessageBox.Show($"导入失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                        MessageBox.Show("解压文件失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return false;
                    }

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
                        MessageBox.Show("压缩文件中未找到有效的MOD文件", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return false;
                    }

                    Console.WriteLine($"[DEBUG] 在压缩文件中找到 {modFiles.Count} 个MOD文件");

                    // 复制MOD文件到备份目录
                    foreach (var modFile in modFiles)
                    {
                        var fileName = Path.GetFileName(modFile);
                        var targetPath = Path.Combine(targetDir, fileName);
                        File.Copy(modFile, targetPath, true);
                        Console.WriteLine($"[DEBUG] 复制解压的MOD文件: {targetPath}");
                    }

                    // 查找并复制预览图
                    var imageFiles = Directory.GetFiles(tempDir, "*.*", SearchOption.AllDirectories)
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
                        var previewPath = Path.Combine(targetDir, $"preview{imageExt}");
                        File.Copy(previewImage, previewPath, true);
                        Console.WriteLine($"[DEBUG] 复制预览图: {previewPath}");
                    }

                    // 复制到MOD目录（如果是启用状态）
                    if (!string.IsNullOrEmpty(currentModPath) && Directory.Exists(currentModPath))
                    {
                        var modSubDir = Path.Combine(currentModPath, modName);
                        if (!Directory.Exists(modSubDir))
                        {
                            Directory.CreateDirectory(modSubDir);
                        }

                        foreach (var modFile in modFiles)
                        {
                            var fileName = Path.GetFileName(modFile);
                            var modFilePath = Path.Combine(modSubDir, fileName);
                            File.Copy(modFile, modFilePath, true);
                            Console.WriteLine($"[DEBUG] 复制到MOD目录: {modFilePath}");
                        }
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

        // 解压压缩文件
        private bool ExtractCompressedFile(string filePath, string extractPath)
        {
            try
            {
                var fileExtension = Path.GetExtension(filePath).ToLower();
                
                if (fileExtension == ".zip")
                {
                    System.IO.Compression.ZipFile.ExtractToDirectory(filePath, extractPath, true);
                    return true;
                }
                else if (fileExtension == ".rar" || fileExtension == ".7z")
                {
                    // 简化实现：显示提示信息，建议用户手动解压
                    MessageBox.Show($"检测到 {fileExtension} 格式的压缩文件。\n\n" +
                        "请手动解压此文件，然后导入解压后的MOD文件（.pak, .ucas, .utoc）。\n\n" +
                        "支持直接拖拽解压后的文件到程序窗口进行导入。", 
                        "需要手动解压", MessageBoxButton.OK, MessageBoxImage.Information);
                    return false;
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
                    MessageBox.Show("请先选择游戏路径", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!Directory.Exists(currentGamePath))
                {
                    MessageBox.Show("游戏路径不存在，请重新配置游戏路径", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    
                    MessageBox.Show($"游戏 '{currentGameName}' 启动成功！\n\n" +
                                  $"执行程序: {Path.GetFileName(gameExecutablePath)}", 
                                  "启动成功", 
                                  MessageBoxButton.OK, 
                                  MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"无法找到游戏可执行文件。\n\n" +
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
                MessageBox.Show($"启动游戏失败: {ex.Message}\n\n" +
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

                // Create a temporary list with the "All" category
                var newCategories = new List<Category>
                {
                    new Category { Name = "全部", Count = allMods.Count }
                };

                // Group mods by their categories and count them
                var categoryCounts = allMods
                    .Where(m => m.Categories != null)
                    .SelectMany(m => m.Categories)
                    .GroupBy(c => c)
                    .ToDictionary(g => g.Key, g => g.Count());

                foreach (var kvp in categoryCounts.OrderBy(kvp => kvp.Key))
                {
                    if (kvp.Key != "全部")
                    {
                        newCategories.Add(new Category { Name = kvp.Key, Count = kvp.Value });
                    }
                }

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
                        ShowCustomMessageBox($"分类 '{categoryName}' 添加成功！", "添加分类", MessageBoxButton.OK, MessageBoxImage.Information);
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
                var selectedItem = CategoryList.SelectedItem;
                var defaultCategories = new[] { "全部", "已启用", "已禁用" };
                string? categoryName = null;
                CategoryItem? selectedCategoryItem = null;
                
                if (selectedItem is CategoryItem ci)
                {
                    categoryName = ci.Name;
                    selectedCategoryItem = ci;
                }
                else if (selectedItem is Category c)
                {
                    categoryName = c.Name;
                }
                
                if (string.IsNullOrEmpty(categoryName))
                {
                    ShowCustomMessageBox("请先选择要删除的自定义分类", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                if (defaultCategories.Contains(categoryName))
                {
                    ShowCustomMessageBox("系统分类不能删除，只能删除自定义分类", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                if (_categoryService == null)
                {
                    ShowCustomMessageBox("分类服务未初始化", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                var result = ShowCustomMessageBox($"确定要删除分类 '{categoryName}' 吗？\n\n此操作将同时删除所有子分类。", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    // 如果有CategoryItem对象，直接用；否则用名称查找
                    if (selectedCategoryItem == null)
                    {
                        selectedCategoryItem = _categoryService.Categories.FirstOrDefault(x => x.Name == categoryName);
                    }
                    if (selectedCategoryItem != null)
                    {
                        await _categoryService.RemoveCategoryAsync(selectedCategoryItem);
                        RefreshCategoryDisplay();
                        Console.WriteLine($"[DEBUG] 成功删除分类: {categoryName}");
                        ShowCustomMessageBox("分类删除成功！", "删除分类", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        ShowCustomMessageBox($"未找到要删除的分类: {categoryName}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 删除分类失败: {ex.Message}");
                ShowCustomMessageBox($"删除分类失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                        MessageBox.Show("默认分类不能重命名", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
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
                            MessageBox.Show("分类重命名成功！", "重命名分类", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            MessageBox.Show("重命名失败，分类名称可能已存在", "重命名分类", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                    MessageBox.Show("请先选择要重命名的分类", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 重命名分类失败: {ex.Message}");
                MessageBox.Show($"重命名分类失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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

        /// <summary>
        /// 分类列表拖拽悬停事件
        /// </summary>
        private void CategoryList_DragOver(object sender, DragEventArgs e)
        {
            try
            {
                if (e.Data.GetDataPresent("SelectedMods"))
                {
                    e.Effects = DragDropEffects.Move;
                }
                else
                {
                    e.Effects = DragDropEffects.None;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 分类列表拖拽悬停事件失败: {ex.Message}");
                e.Effects = DragDropEffects.None;
            }
        }

        /// <summary>
        /// 分类列表拖拽放置事件
        /// </summary>
        private void CategoryList_Drop(object sender, DragEventArgs e)
        {
            try
            {
                if (e.Data.GetDataPresent("SelectedMods"))
                {
                    var selectedMods = e.Data.GetData("SelectedMods") as List<Mod>;
                    if (selectedMods?.Any() == true)
                    {
                        // 获取拖拽目标分类
                        var targetCategory = GetDropTargetCategory(e);
                        if (targetCategory != null)
                        {
                            // 移动MOD到目标分类
                            MoveModsToCategory(selectedMods, targetCategory);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 分类列表拖拽放置事件失败: {ex.Message}");
                MessageBox.Show($"移动MOD到分类失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
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
        private async void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            try
            {
                // 保存分类数据
                if (_categoryService != null && !string.IsNullOrEmpty(currentGameName))
                {
                    Console.WriteLine("[DEBUG] 保存分类数据...");
                    // CategoryService会在SetCurrentGameAsync中自动保存，这里我们手动触发一次保存
                    await _categoryService.SetCurrentGameAsync(currentGameName);
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
                    MessageBox.Show("不能将MOD移动到'全部'分类", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
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
                
                MessageBox.Show($"成功将 {mods.Count} 个MOD移动到分类 '{categoryName}'", "移动完成", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 移动MOD到分类失败: {ex.Message}");
                MessageBox.Show($"移动MOD到分类失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
            return value is UEModManager.Core.Models.CategoryItem ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }


} 
