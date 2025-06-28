using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;

namespace UEModManager.Views
{
    public partial class GamePathDialog : Window, INotifyPropertyChanged
    {
        private string _gameName = "";
        private string _gamePath = "";
        private string _modPath = "";
        private string _backupPath = "";
        private bool _isPathsValid;

        public string GameName
        {
            get => _gameName;
            set
            {
                _gameName = value;
                OnPropertyChanged(nameof(GameName));
            }
        }

        public string GamePath
        {
            get => _gamePath;
            set
            {
                _gamePath = value;
                OnPropertyChanged(nameof(GamePath));
                ValidatePaths();
            }
        }

        public string ModPath
        {
            get => _modPath;
            set
            {
                _modPath = value;
                OnPropertyChanged(nameof(ModPath));
                ValidatePaths();
            }
        }

        public string BackupPath
        {
            get => _backupPath;
            set
            {
                _backupPath = value;
                OnPropertyChanged(nameof(BackupPath));
                ValidatePaths();
            }
        }

        public bool IsPathsValid
        {
            get => _isPathsValid;
            set
            {
                _isPathsValid = value;
                OnPropertyChanged(nameof(IsPathsValid));
            }
        }

        public GamePathDialog(string gameName)
        {
            InitializeComponent();
            DataContext = this;
            GameName = gameName;
            
            // 自动搜索路径
            _ = Task.Run(AutoSearchPaths);
        }

        private async Task AutoSearchPaths()
        {
            await Task.Delay(500); // 模拟搜索延迟
            
            await Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    // 自动搜索游戏路径
                    var gameSearchPaths = GetGameSearchPaths(GameName);
                    string foundGamePath = "";
                    string foundExePath = "";
                    
                    // 查找实际的exe文件位置
                    foreach (var searchPath in gameSearchPaths)
                    {
                        if (Directory.Exists(searchPath))
                        {
                            var exeFiles = Directory.GetFiles(searchPath, "*.exe", SearchOption.AllDirectories);
                            if (exeFiles.Length > 0)
                            {
                                // 找到主要的游戏exe文件（排除一些辅助工具）
                                var mainExe = FindMainGameExecutable(exeFiles, GameName);
                                if (!string.IsNullOrEmpty(mainExe))
                                {
                                    foundExePath = mainExe;
                                    foundGamePath = Path.GetDirectoryName(mainExe);
                                    break;
                                }
                            }
                        }
                    }
                    
                    if (!string.IsNullOrEmpty(foundGamePath) && !string.IsNullOrEmpty(foundExePath))
                    {
                        GamePath = foundGamePath;
                        GamePathTextBox.Text = foundGamePath;
                        
                        // 从exe路径智能推导MOD路径
                        var deducedModPath = DeduceModPathFromExe(foundExePath, GameName);
                        if (!string.IsNullOrEmpty(deducedModPath))
                        {
                            ModPath = deducedModPath;
                            ModPathTextBox.Text = deducedModPath;
                        }
                        
                        SearchStatusText.Text = $"✅ 找到游戏: {Path.GetFileName(foundExePath)}";
                    }
                    else
                    {
                        // 如果没找到exe，仍尝试根目录
                        var fallbackGamePath = gameSearchPaths.FirstOrDefault(Directory.Exists);
                        if (!string.IsNullOrEmpty(fallbackGamePath))
                        {
                            GamePath = fallbackGamePath;
                            GamePathTextBox.Text = fallbackGamePath;
                            
                            // 使用原有的MOD路径推导
                            var modSearchPaths = GetModSearchPaths(fallbackGamePath);
                            var foundModPath = modSearchPaths.FirstOrDefault(Directory.Exists);
                            
                            if (!string.IsNullOrEmpty(foundModPath))
                            {
                                ModPath = foundModPath;
                                ModPathTextBox.Text = foundModPath;
                            }
                            else
                            {
                                var defaultModPath = DeduceModPathFromExe(fallbackGamePath, GameName);
                                try
                                {
                                    Directory.CreateDirectory(defaultModPath);
                                    ModPath = defaultModPath;
                                    ModPathTextBox.Text = defaultModPath;
                                }
                                catch
                                {
                                    ModPath = fallbackGamePath;
                                    ModPathTextBox.Text = fallbackGamePath;
                                }
                            }
                            
                            SearchStatusText.Text = "⚠️ 找到游戏目录，但未定位到exe文件";
                        }
                        else
                        {
                            SearchStatusText.Text = "❌ 未找到游戏，请手动选择";
                        }
                    }
                    
                    // 设置备份路径
                    var currentDir = AppDomain.CurrentDomain.BaseDirectory;
                    var backupDir = Path.Combine(currentDir, $"{GameName}_备份");
                    try
                    {
                        Directory.CreateDirectory(backupDir);
                        BackupPath = backupDir;
                        BackupPathTextBox.Text = backupDir;
                    }
                    catch
                    {
                        BackupPath = currentDir;
                        BackupPathTextBox.Text = currentDir;
                    }
                }
                catch (Exception ex)
                {
                    SearchStatusText.Text = $"搜索失败: {ex.Message}";
                }
            });
        }

        /// <summary>
        /// 找到主要的游戏可执行文件
        /// </summary>
        private string FindMainGameExecutable(string[] exeFiles, string gameName)
        {
            // 排除常见的辅助工具和安装程序
            var excludeKeywords = new[] { "unins", "setup", "launcher", "updater", "installer", "redist", "vcredist", "directx" };
            
            var validExes = exeFiles.Where(exe =>
            {
                var fileName = Path.GetFileName(exe).ToLower();
                return !excludeKeywords.Any(keyword => fileName.Contains(keyword));
            }).ToArray();
            
            if (validExes.Length == 0) return "";
            if (validExes.Length == 1) return validExes[0];
            
            // 根据游戏名称查找最匹配的exe
            var gameSpecificExe = gameName switch
            {
                "剑星" => validExes.FirstOrDefault(exe => Path.GetFileName(exe).ToLower().Contains("sb-win64-shipping")) ??
                         validExes.FirstOrDefault(exe => Path.GetFileName(exe).ToLower().Contains("stellarblade")) ??
                         validExes.FirstOrDefault(exe => Path.GetFileName(exe).ToLower().Contains("stellar")),
                
                "黑神话·悟空" => validExes.FirstOrDefault(exe => Path.GetFileName(exe).ToLower().Contains("b1-win64-shipping")) ??
                               validExes.FirstOrDefault(exe => Path.GetFileName(exe).ToLower().Contains("wukong")) ??
                               validExes.FirstOrDefault(exe => Path.GetFileName(exe).ToLower().Contains("blackmyth")),
                
                _ => validExes.FirstOrDefault(exe => 
                     {
                         var fileName = Path.GetFileNameWithoutExtension(exe).ToLower();
                         var gameNameLower = gameName.ToLower();
                         return fileName.Contains(gameNameLower) || gameNameLower.Contains(fileName);
                     })
            };
            
            if (!string.IsNullOrEmpty(gameSpecificExe))
                return gameSpecificExe;
            
            // 选择最大的exe文件（通常是主程序）
            return validExes.OrderByDescending(exe => new FileInfo(exe).Length).First();
        }

        /// <summary>
        /// 从exe路径推导MOD路径
        /// </summary>
        private string DeduceModPathFromExe(string exePath, string gameName)
        {
            var exeDir = Path.GetDirectoryName(exePath);
            if (string.IsNullOrEmpty(exeDir)) return "";
            
            // 从exe目录向上查找游戏根目录
            var gameRoot = FindGameRootFromExe(exeDir);
            
            // 虚幻引擎MOD路径模式
            var modPathPatterns = new[]
            {
                Path.Combine(gameRoot, "Game", "Content", "Paks", "~mods"),
                Path.Combine(gameRoot, "Game", "Content", "Paks", "Mods"),
                Path.Combine(gameRoot, "Content", "Paks", "~mods"),
                Path.Combine(gameRoot, "Content", "Paks", "Mods"),
                Path.Combine(gameRoot, "Mods"),
                Path.Combine(gameRoot, "Game", "Content", "Paks"),
                gameRoot
            };
            
            // 返回第一个可以创建的路径
            foreach (var pattern in modPathPatterns)
            {
                var parentDir = Directory.GetParent(pattern)?.FullName;
                if (!string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir))
                {
                    return pattern;
                }
            }
            
            // 默认返回游戏根目录下的~mods文件夹
            return Path.Combine(gameRoot, "Game", "Content", "Paks", "~mods");
        }

        /// <summary>
        /// 从exe目录向上查找游戏根目录
        /// </summary>
        private string FindGameRootFromExe(string exeDir)
        {
            var currentDir = new DirectoryInfo(exeDir);
            
            // 向上查找，直到找到包含Content或Game文件夹的目录，或者到达steamapps/common的直接子目录
            while (currentDir != null && currentDir.Parent != null)
            {
                // 如果当前目录包含Game或Content文件夹，可能是游戏根目录
                if (Directory.Exists(Path.Combine(currentDir.FullName, "Game")) ||
                    Directory.Exists(Path.Combine(currentDir.FullName, "Content")))
                {
                    return currentDir.FullName;
                }
                
                // 如果父目录是steamapps/common，那么当前目录就是游戏根目录
                if (currentDir.Parent.Name.ToLower() == "common" && 
                    currentDir.Parent.Parent?.Name.ToLower() == "steamapps")
                {
                    return currentDir.FullName;
                }
                
                currentDir = currentDir.Parent;
            }
            
            // 如果没找到，返回exe所在目录的最上层
            return exeDir;
        }

        private string[] GetGameSearchPaths(string gameName)
        {
            var searchPaths = new List<string>();
            
            // 1. Steam路径检测
            var steamPaths = GetSteamGamePaths(gameName);
            searchPaths.AddRange(steamPaths);
            
            // 2. Epic Games路径检测
            var epicPaths = GetEpicGamePaths(gameName);
            searchPaths.AddRange(epicPaths);
            
            // 3. 通用游戏路径
            var commonPaths = new[]
            {
                @"C:\Program Files (x86)\Steam\steamapps\common",
                @"C:\Program Files\Steam\steamapps\common",
                @"D:\Steam\steamapps\common",
                @"E:\Steam\steamapps\common",
                @"C:\Program Files\Epic Games",
                @"D:\Epic Games",
                @"E:\Epic Games",
                @"C:\Program Files",
                @"C:\Program Files (x86)",
                @"D:\Games",
                @"E:\Games"
            };

            var gameKeywords = gameName switch
            {
                "剑星" => new[] { "Stellar Blade", "StellarBlade", "剑星" },
                "黑神话·悟空" => new[] { "Black Myth Wukong", "BlackMythWukong", "Black Myth- Wukong", "Wukong", "黑神话", "悟空" },
                "光与影：33号远征队" => new[] { "Enshrouded", "光与影", "33号远征队" },
                "艾尔登法环" => new[] { "Elden Ring", "EldenRing", "艾尔登法环" },
                "赛博朋克2077" => new[] { "Cyberpunk 2077", "Cyberpunk2077", "赛博朋克" },
                "巫师3" => new[] { "The Witcher 3 Wild Hunt", "Witcher3", "巫师3" },
                _ => new[] { gameName, gameName.Replace(" ", ""), gameName.Replace(" ", "_") }
            };

            foreach (var basePath in commonPaths)
            {
                if (Directory.Exists(basePath))
                {
                    foreach (var keyword in gameKeywords)
                    {
                        try
                        {
                            var directories = Directory.GetDirectories(basePath, $"*{keyword}*", SearchOption.TopDirectoryOnly);
                            foreach (var dir in directories)
                            {
                                // 验证是否确实是游戏目录（包含exe文件）
                                if (HasGameExecutable(dir))
                                {
                                    searchPaths.Add(dir);
                                }
                            }
                        }
                        catch { }
                    }
                }
            }

            return searchPaths.Distinct().ToArray();
        }

        private string[] GetSteamGamePaths(string gameName)
        {
            var steamPaths = new List<string>();
            
            try
            {
                // 从注册表获取Steam路径
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                var steamPath = key?.GetValue("SteamPath")?.ToString();
                
                if (!string.IsNullOrEmpty(steamPath))
                {
                    // 检查主Steam库
                    var commonPath = Path.Combine(steamPath, "steamapps", "common");
                    if (Directory.Exists(commonPath))
                    {
                        var gameFolders = GetGameKeywords(gameName);
                        foreach (var folder in gameFolders)
                        {
                            var gamePath = Path.Combine(commonPath, folder);
                            if (Directory.Exists(gamePath) && HasGameExecutable(gamePath))
                            {
                                steamPaths.Add(gamePath);
                            }
                        }
                    }
                    
                    // 检查其他Steam库
                    var libraryFoldersFile = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
                    if (File.Exists(libraryFoldersFile))
                    {
                        var content = File.ReadAllText(libraryFoldersFile);
                        var pathMatches = System.Text.RegularExpressions.Regex.Matches(content, @"""path""\s*""([^""]+)""");
                        
                        foreach (System.Text.RegularExpressions.Match match in pathMatches)
                        {
                            var libraryPath = match.Groups[1].Value.Replace(@"\\", @"\");
                            var libraryCommon = Path.Combine(libraryPath, "steamapps", "common");
                            
                            if (Directory.Exists(libraryCommon))
                            {
                                var gameFolders = GetGameKeywords(gameName);
                                foreach (var folder in gameFolders)
                                {
                                    var gamePath = Path.Combine(libraryCommon, folder);
                                    if (Directory.Exists(gamePath) && HasGameExecutable(gamePath))
                                    {
                                        steamPaths.Add(gamePath);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            
            return steamPaths.ToArray();
        }

        private string[] GetEpicGamePaths(string gameName)
        {
            var epicPaths = new List<string>();
            
            try
            {
                var epicDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Epic", "EpicGamesLauncher", "Data", "Manifests");
                
                if (Directory.Exists(epicDataPath))
                {
                    var manifestFiles = Directory.GetFiles(epicDataPath, "*.item");
                    var gameKeywords = GetGameKeywords(gameName);
                    
                    foreach (var manifestFile in manifestFiles)
                    {
                        try
                        {
                            var content = File.ReadAllText(manifestFile);
                            var manifest = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(content);
                            
                            if (manifest.TryGetValue("InstallLocation", out var installLocationObj) &&
                                manifest.TryGetValue("DisplayName", out var displayNameObj))
                            {
                                var installLocation = installLocationObj.ToString();
                                var displayName = displayNameObj.ToString();
                                
                                if (!string.IsNullOrEmpty(installLocation) && !string.IsNullOrEmpty(displayName))
                                {
                                    foreach (var keyword in gameKeywords)
                                    {
                                        if (displayName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                                            Path.GetFileName(installLocation).Contains(keyword, StringComparison.OrdinalIgnoreCase))
                                        {
                                            if (Directory.Exists(installLocation) && HasGameExecutable(installLocation))
                                            {
                                                epicPaths.Add(installLocation);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
            
            return epicPaths.ToArray();
        }

        private string[] GetGameKeywords(string gameName)
        {
            return gameName switch
            {
                "剑星" => new[] { "Stellar Blade", "StellarBlade", "Stellarblade" },
                "黑神话·悟空" => new[] { "Black Myth Wukong", "BlackMythWukong", "Black Myth- Wukong", "b1-win64-shipping" },
                "光与影：33号远征队" => new[] { "Enshrouded" },
                "艾尔登法环" => new[] { "Elden Ring", "EldenRing" },
                "赛博朋克2077" => new[] { "Cyberpunk 2077", "Cyberpunk2077" },
                "巫师3" => new[] { "The Witcher 3 Wild Hunt", "Witcher3" },
                _ => new[] { gameName, gameName.Replace(" ", ""), gameName.Replace(" ", "_"), gameName.Replace("·", " ") }
            };
        }

        private bool HasGameExecutable(string gamePath)
        {
            try
            {
                var exeFiles = Directory.GetFiles(gamePath, "*.exe", SearchOption.AllDirectories);
                return exeFiles.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        private string[] GetModSearchPaths(string gamePath)
        {
            return new[]
            {
                // 最常见的虚幻引擎MOD路径
                Path.Combine(gamePath, "Game", "Content", "Paks", "~mods"),
                Path.Combine(gamePath, "Game", "Content", "Paks", "Mods"),
                Path.Combine(gamePath, "Content", "Paks", "~mods"),
                Path.Combine(gamePath, "Content", "Paks", "Mods"),
                // 简单路径
                Path.Combine(gamePath, "Mods"),
                // 直接在Paks目录（备选）
                Path.Combine(gamePath, "Game", "Content", "Paks"),
                Path.Combine(gamePath, "Content", "Paks"),
                // 游戏根目录（最后选择）
                gamePath
            };
        }

        private void ValidatePaths()
        {
            IsPathsValid = !string.IsNullOrEmpty(GamePath) && 
                          !string.IsNullOrEmpty(ModPath) && 
                          !string.IsNullOrEmpty(BackupPath) &&
                          Directory.Exists(GamePath);
        }

        private void BrowseGamePath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "选择游戏安装目录",
                UseDescriptionForTitle = true,
                SelectedPath = GamePath
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                GamePath = dialog.SelectedPath;
                GamePathTextBox.Text = dialog.SelectedPath;
            }
        }

        private void BrowseModPath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "选择MOD目录",
                UseDescriptionForTitle = true,
                SelectedPath = ModPath
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ModPath = dialog.SelectedPath;
                ModPathTextBox.Text = dialog.SelectedPath;
            }
        }

        private void BrowseBackupPath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "选择备份目录",
                UseDescriptionForTitle = true,
                SelectedPath = BackupPath
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                BackupPath = dialog.SelectedPath;
                BackupPathTextBox.Text = dialog.SelectedPath;
            }
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            if (IsPathsValid)
            {
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("请确保所有路径都已正确设置", "路径无效", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
} 