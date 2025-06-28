using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Windows.Threading;
using Microsoft.Win32;
using System.Text.Json;
using System.Diagnostics;
using UEModManager.ViewModels;
using System.Collections;

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
        private List<Category> categories = new List<Category>();
        private string currentGamePath = "";
        private string currentModPath = "";
        private string currentBackupPath = "";
        private string currentGameName = "";
        private string configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
        private readonly List<string> modTags = new List<string> { "面部", "人物", "武器", "修改", "其他" };
        // 主构造函数
        public MainWindow()
        {
            try
            {
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
                
                StartStatsTimer();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"主窗口初始化失败: {ex.Message}", "初始化错误", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
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
                                BackupPath = currentBackupPath
                            };
                            var updatedJson = JsonSerializer.Serialize(updatedConfig, new JsonSerializerOptions { WriteIndented = true });
                            File.WriteAllText(configFilePath, updatedJson);
                            Console.WriteLine("[DEBUG] 配置文件已更新为正确的备份路径");
                        }
                        
                        Console.WriteLine($"配置加载成功: 游戏={currentGameName}, 路径={currentGamePath}");
                        Console.WriteLine($"MOD路径={currentModPath}, 备份路径={currentBackupPath}");
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
                MessageBox.Show($"加载配置失败: {ex.Message}\n将使用默认设置。", "配置加载错误", MessageBoxButton.OK, MessageBoxImage.Warning);
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

        private void SaveConfiguration()
        {
            try
            {
                var config = new AppConfig 
                { 
                    GameName = currentGameName,
                    GamePath = currentGamePath,
                    ModPath = currentModPath,
                    BackupPath = currentBackupPath
                };
                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(configFilePath, json);
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
            // 初始化游戏列表 - 先不设置选中项，等待配置恢复
            // GameList.SelectedIndex = 0; // 移除这行，让配置恢复来设置

            // 初始化分类 - 首次打开只有全部分类
            categories = new List<Category>
            {
                new Category { Name = "全部", Count = 0 }
            };

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
                if (GameList.SelectedItem is ComboBoxItem selectedItem)
                {
                    var gameName = selectedItem.Content.ToString();
                    if (gameName != "请选择游戏" && gameName != currentGameName)
                    {
                        // 如果已经有游戏配置，显示切换确认
                        if (!string.IsNullOrEmpty(currentGameName))
                        {
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
                                // 恢复到原来的选择
                                GameList.SelectionChanged -= GameList_SelectionChanged;
                                GameList.SelectedItem = GameList.Items.Cast<ComboBoxItem>()
                                    .FirstOrDefault(item => item.Content.ToString() == currentGameName) ?? GameList.Items[0];
                                GameList.SelectionChanged += GameList_SelectionChanged;
                                return;
                            }
                        }
                        
                        ShowGamePathDialog(gameName);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"游戏选择失败: {ex.Message}");
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
                
                SaveConfiguration();
                UpdateGamePathDisplay();
                
                // 显示扫描进度
                this.IsEnabled = false;
                this.Cursor = Cursors.Wait;
                
                try
                {
                    InitializeModsForGame();
                    
                    MessageBox.Show(
                        $"游戏 '{gameName}' 配置完成！\n\n" +
                        $"游戏路径: {currentGamePath}\n" +
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
                UpdateCategoryCount();
                
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
                    var mod = new Mod
                    {
                        Name = modName,
                        RealName = modName,
                        Status = isEnabled ? "已启用" : "已禁用",
                        Type = DetermineModType(modName, group.ToList()),
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
                    
                    var mod = new Mod
                    {
                        Name = modName,
                        RealName = modName,
                        Status = "已禁用",
                        Type = "未知",
                        Size = FormatFileSize(files.Sum(f => new FileInfo(f).Length)),
                        ImportDate = Directory.GetCreationTime(dir).ToString("yyyy-MM-dd"),
                        Icon = "📁",
                    };
                    
                    // 检查是否有预览图并预加载
                    var previewImage = files.FirstOrDefault(f => 
                        IOPath.GetFileName(f).ToLower().Contains("preview") ||
                        IOPath.GetFileName(f).ToLower().StartsWith("preview") ||
                        new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" }.Contains(IOPath.GetExtension(f).ToLower()));
                    
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
                    
                    var previewImage = files.FirstOrDefault(f => 
                        IOPath.GetFileName(f).ToLower().Contains("preview") ||
                        IOPath.GetFileName(f).ToLower().StartsWith("preview") ||
                        new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" }.Contains(IOPath.GetExtension(f).ToLower()));
                    
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
        /// 备份MOD文件到备份目录的独立文件夹中
        /// </summary>
        private bool BackupModFiles(string modName, List<string> modFiles)
        {
            try
            {
                Console.WriteLine($"开始备份MOD: {modName}");
                
                // 在备份目录中为此MOD创建独立文件夹
                var modBackupDir = IOPath.Combine(currentBackupPath, modName);
                
                // 如果备份文件夹已存在，清空它
                if (Directory.Exists(modBackupDir))
                {
                    Directory.Delete(modBackupDir, true);
                    Console.WriteLine($"清空已存在的备份目录: {modBackupDir}");
                }
                
                // 创建MOD专属备份目录
                Directory.CreateDirectory(modBackupDir);
                Console.WriteLine($"创建MOD备份目录: {modBackupDir}");
                
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
                        Console.WriteLine($"备份文件: {modFile} -> {backupPath}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"备份文件失败 {modFile}: {ex.Message}");
                    }
                }
                
                var success = successCount > 0;
                if (success)
                {
                    Console.WriteLine($"MOD {modName} 备份成功，共备份 {successCount} 个文件");
                }
                else
                {
                    Console.WriteLine($"MOD {modName} 备份失败，没有成功备份任何文件");
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
                Console.WriteLine($"备份MOD失败 {modName}: {ex.Message}");
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
            
            if (string.IsNullOrEmpty(mod.PreviewImagePath) || !File.Exists(mod.PreviewImagePath))
            {
                Console.WriteLine($"[DEBUG] 预览图路径无效或文件不存在: {mod.PreviewImagePath}");
                mod.PreviewImageSource = null;
                return;
            }

            try
            {
                Console.WriteLine($"[DEBUG] 开始加载图片文件: {mod.PreviewImagePath}");
                
                // 使用FileStream方式加载，避免缓存和锁定问题
                using (var fileStream = new FileStream(mod.PreviewImagePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                    bitmap.StreamSource = fileStream;
                    bitmap.EndInit();
                    bitmap.Freeze(); // 跨线程访问必须冻结
                    
                    // 正确设置PreviewImageSource属性，触发UI更新
                    mod.PreviewImageSource = bitmap;
                    
                    Console.WriteLine($"[DEBUG] 成功加载预览图: {mod.Name}, ImageSource已设置");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] 预加载图片失败: {mod.PreviewImagePath}, 错误: {ex.Message}");
                mod.PreviewImageSource = null;
            }
        }

        private void RefreshModDisplay()
        {
            try
            {
                Console.WriteLine("开始刷新MOD显示...");
                
                // 强制垃圾回收，释放图片缓存和文件锁定
                GC.Collect();
                GC.WaitForPendingFinalizers();
                
                // 强制刷新数据绑定
                ModsGrid.ItemsSource = null;
                
                // 强制界面更新
                ModsGrid.UpdateLayout();
                
                // 触发所有MOD的预览图属性更改通知
                foreach (var mod in allMods)
                {
                    mod.OnPropertyChanged(nameof(Mod.PreviewImageSource));
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
                    var result = MessageBox.Show($"确定要删除MOD \"{mod.Name}\" 吗？\n这将同时删除备份文件和MOD目录中的文件。", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);
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
                            
                            MessageBox.Show($"已删除MOD: {mod.Name}", "删除成功", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        catch (Exception deleteEx)
                        {
                            MessageBox.Show($"删除MOD文件时发生错误: {deleteEx.Message}", "删除失败", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"删除MOD失败: {ex.Message}", "删除失败", MessageBoxButton.OK, MessageBoxImage.Error);
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
                if (selectedMod != null)
                {
                    bool isCurrentlyEnabled = selectedMod.Status == "已启用";
                    
                    if (isCurrentlyEnabled)
                    {
                        DisableMod(selectedMod);
                    }
                    else
                    {
                        EnableMod(selectedMod);
                    }
                    
                    // 更新滑块状态
                    UpdateToggleState(!isCurrentlyEnabled);
                    
                    // 更新其他UI
                    UpdateModDetails(selectedMod);
                    RefreshModDisplay();
                    UpdateCategoryCount();
                    
                    Console.WriteLine($"MOD {selectedMod.Name} 状态已切换为: {selectedMod.Status}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"切换MOD状态时发生错误: {ex.Message}");
                MessageBox.Show($"切换MOD状态时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                    var result = MessageBox.Show($"确定要删除MOD \"{selectedMod.Name}\" 吗？\n这将同时删除备份文件和MOD目录中的文件。", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);
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
                            
                            MessageBox.Show("已删除MOD", "删除成功", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        catch (Exception deleteEx)
                        {
                            MessageBox.Show($"删除MOD文件时发生错误: {deleteEx.Message}", "删除失败", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"删除MOD失败: {ex.Message}", "删除失败", MessageBoxButton.OK, MessageBoxImage.Error);
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
                var mod = button?.DataContext as Mod;
                if (mod != null)
                {
                    var newDescription = ShowInputDialog("请输入MOD描述:", "编辑MOD", mod.Description);
                    if (!string.IsNullOrWhiteSpace(newDescription) && newDescription != mod.Description)
                    {
                        mod.Description = newDescription;
                        if (selectedMod == mod)
                        {
                            UpdateModDetails(mod);
                        }
                        RefreshModDisplay();
                        MessageBox.Show($"已更新MOD描述", "编辑成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"编辑MOD失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
            if (sender is Border border && border.Tag is Mod mod)
            {
                ShowTypeSelectionMenu(border, mod);
            }
        }

        private void ShowTypeSelectionMenu(FrameworkElement element, Mod mod)
        {
            var contextMenu = new ContextMenu
            {
                Background = new SolidColorBrush(Color.FromRgb(42, 52, 65)),
                Foreground = Brushes.White
            };

            var types = new[] { "👥 面部", "👤 人物", "⚔️ 武器", "👕 服装", "🔧 修改", "📦 其他" };
            
            foreach (var type in types)
            {
                var menuItem = new MenuItem
                {
                    Header = type,
                    Tag = mod
                };
                menuItem.Click += (s, e) =>
                {
                    var typeText = type.Substring(2); // 移除emoji前缀
                    mod.Type = typeText;
                    RefreshModDisplay();
                    UpdateCategoryCount();
                };
                contextMenu.Items.Add(menuItem);
            }

            contextMenu.PlacementTarget = element;
            contextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            contextMenu.IsOpen = true;
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

        private Mod? GetModFromContextMenu(MenuItem menuItem)
        {
            // 从ContextMenu获取对应的MOD
            if (menuItem.Parent is ContextMenu contextMenu && 
                contextMenu.PlacementTarget is FrameworkElement element)
            {
                return element.DataContext as Mod;
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
            var result = MessageBox.Show($"确定要删除MOD '{mod.Name}' 吗？\n\n这将同时删除MOD文件夹和备份文件夹中的相关文件。", 
                "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            
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
                    
                    MessageBox.Show("MOD删除成功", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"删除MOD失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // === MOD选中和详情显示 ===
        private void ModCard_MouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var border = sender as Border;
                var mod = border?.DataContext as Mod;
                
                if (mod != null)
                {
                    // 更新选中的MOD
                    selectedMod = mod;
                    
                    // 更新详情面板
                    UpdateModDetails(mod);
                    
                    // 视觉反馈：高亮选中的卡片
                    HighlightSelectedCard(border);
                    
                    Console.WriteLine($"选中MOD: {mod.Name}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"选中MOD失败: {ex.Message}");
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
        /// 为MOD设置预览图 - 参考旧版本的成功实现
        /// </summary>
        private void SetModPreviewImage(Mod mod, string imagePath)
        {
            try
            {
                Console.WriteLine($"为MOD {mod.Name} 设置预览图: {imagePath}");
                
                var modBackupDir = IOPath.Combine(currentBackupPath, mod.RealName);
                if (!Directory.Exists(modBackupDir))
                {
                    Directory.CreateDirectory(modBackupDir);
                }
                
                var imageExtension = IOPath.GetExtension(imagePath);
                var previewImageName = "preview" + imageExtension;
                var previewImagePath = IOPath.Combine(modBackupDir, previewImageName);
                
                if (File.Exists(previewImagePath))
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    File.Delete(previewImagePath);
                }
                
                File.Copy(imagePath, previewImagePath, true);
                
                mod.PreviewImagePath = previewImagePath;
                mod.Icon = "🖼️";
                
                // 直接加载图片到ImageSource，不再需要刷新整个列表
                LoadModPreviewImage(mod);
                
                // 同时更新详情面板的预览图
                UpdateModDetailPreview(mod);
                
                MessageBox.Show("预览图设置成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"设置预览图失败: {ex.Message}");
                MessageBox.Show($"设置预览图失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                
                MessageBox.Show(message, "系统状态", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"获取系统状态失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    SaveConfiguration();
                    MessageBox.Show("设置已保存！", "设置", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开设置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
            
            var result = MessageBox.Show(currentSettings, "设置 - 虚幻引擎MOD管理器", 
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

        private void UpdateModCountDisplay()
        {
            try
            {
                if (ModCountText != null)
                {
                    ModCountText.Text = $"全部 MOD ({allMods.Count})";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"更新MOD数量显示失败: {ex.Message}");
            }
        }

        private void EnableMod(Mod mod)
        {
            // 实现启用MOD的逻辑
            mod.Status = "已启用";
            Console.WriteLine($"MOD {mod.Name} 已启用");
        }

        private void DisableMod(Mod mod)
        {
            // 实现禁用MOD的逻辑
            mod.Status = "已禁用";
            Console.WriteLine($"MOD {mod.Name} 已禁用");
        }

        private void UpdateCategoryCount()
        {
            // 更新分类计数的逻辑
            Console.WriteLine("更新分类计数");
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
                // 处理分类选择变化
                RefreshModDisplay();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"分类选择变化处理失败: {ex.Message}");
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
                
                // 这里应该实现MOD文件的导入逻辑
                // 暂时只显示消息
                MessageBox.Show($"MOD导入功能正在开发中\n文件: {filePath}", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"从文件导入MOD失败: {ex.Message}");
                MessageBox.Show($"导入失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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

                // 查找游戏可执行文件
                string[] exeFiles = Directory.GetFiles(currentGamePath, "*.exe", SearchOption.AllDirectories);
                if (exeFiles.Length > 0)
                {
                    Process.Start(exeFiles[0]);
                }
                else
                {
                    MessageBox.Show("在游戏目录中找不到可执行文件", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"启动游戏失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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

                // 更新预览图
                UpdateModDetailPreview(mod);
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
                if (mod?.PreviewImageSource != null && ModDetailPreviewImage != null)
                {
                    ModDetailPreviewImage.Source = mod.PreviewImageSource;
                    ModDetailPreviewImage.Visibility = Visibility.Visible;
                    if (ModDetailIconContainer != null)
                        ModDetailIconContainer.Visibility = Visibility.Collapsed;
                }
                else if (!string.IsNullOrEmpty(mod?.PreviewImagePath) && File.Exists(mod.PreviewImagePath) && ModDetailPreviewImage != null)
                {
                    // 备用方案：直接从文件加载
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(mod.PreviewImagePath, UriKind.Absolute);
                    bitmap.EndInit();
                    ModDetailPreviewImage.Source = bitmap;
                    ModDetailPreviewImage.Visibility = Visibility.Visible;
                    if (ModDetailIconContainer != null)
                        ModDetailIconContainer.Visibility = Visibility.Collapsed;
                }
                else
                {
                    // 没有预览图，显示图标占位符
                    if (ModDetailPreviewImage != null)
                    {
                        ModDetailPreviewImage.Source = null;
                        ModDetailPreviewImage.Visibility = Visibility.Collapsed;
                    }
                    if (ModDetailIconContainer != null)
                        ModDetailIconContainer.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"更新MOD详情预览图失败: {ex.Message}");
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
                // 更新分类计数
                UpdateCategoryCount();
                
                // 刷新MOD显示
                RefreshModDisplay();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"刷新分类显示失败: {ex.Message}");
            }
        }

        // 添加分类按钮点击事件
        private void AddCategoryButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string categoryName = ShowInputDialog("请输入分类名称:", "添加分类");
                if (!string.IsNullOrEmpty(categoryName))
                {
                    var newCategory = new Category { Name = categoryName, Count = 0 };
                    categories.Add(newCategory);
                    RefreshCategoryDisplay();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"添加分类失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 删除分类按钮点击事件
        private void DeleteCategoryButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (CategoryList.SelectedItem is Category selectedCategory)
                {
                    var result = MessageBox.Show($"确定要删除分类 '{selectedCategory.Name}' 吗？", 
                        "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    
                    if (result == MessageBoxResult.Yes)
                    {
                        categories.Remove(selectedCategory);
                        RefreshCategoryDisplay();
                    }
                }
                else
                {
                    MessageBox.Show("请先选择要删除的分类", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"删除分类失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 重命名分类按钮点击事件
        private void RenameCategoryButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (CategoryList.SelectedItem is Category selectedCategory)
                {
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
                MessageBox.Show($"重命名分类失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class Game
    {
        public string Name { get; set; } = "";
        public bool IsActive { get; set; }
    }

    public class Category
    {
        public string Name { get; set; } = "";
        public int Count { get; set; }
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
    }
} 
