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
        private string _executableName = "";
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

        /// <summary>
        /// 游戏执行程序名称
        /// </summary>
        public string ExecutableName
        {
            get => _executableName;
            set
            {
                _executableName = value;
                OnPropertyChanged(nameof(ExecutableName));
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
                        
                        // 设置执行程序名称
                        ExecutableName = Path.GetFileName(foundExePath);
                        
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
            
            // 提取游戏名称的核心部分（去掉括号内容）
            var coreGameName = gameName.Split('(')[0].Trim();
            
            // 根据游戏名称查找最匹配的exe
            var gameSpecificExe = coreGameName switch
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
                         var gameNameLower = coreGameName.ToLower();
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
            try
            {
                if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                    return "";

                var exeDir = Path.GetDirectoryName(exePath);
                var exeFileName = Path.GetFileName(exePath);
                
                Console.WriteLine($"[DEBUG] 推导MOD路径 - exe: {exeFileName}, 目录: {exeDir}");

                // 定义虚幻引擎游戏的MOD路径模式
                var modPathPatterns = new List<string>();

                // 检测exe是否在 Binaries\Win64 目录中
                if (exeDir.EndsWith("Binaries\\Win64", StringComparison.OrdinalIgnoreCase))
                {
                    // 获取游戏根目录（往上两级：Binaries\Win64 -> 项目根目录）
                    var gameProjectDir = Directory.GetParent(exeDir)?.Parent?.FullName;
                    
                    if (!string.IsNullOrEmpty(gameProjectDir))
                    {
                        // 标准UE MOD路径模式
                        modPathPatterns.AddRange(new[]
                        {
                            Path.Combine(gameProjectDir, "Content", "Paks", "~mods"),      // 标准MOD路径
                            Path.Combine(gameProjectDir, "Content", "Paks", "Mods"),       // 变体1
                            Path.Combine(gameProjectDir, "Content", "Paks", "mods"),       // 变体2
                            Path.Combine(gameProjectDir, "Content", "Paks"),               // 基础Paks目录
                        });
                        
                        Console.WriteLine($"[DEBUG] UE结构检测到游戏项目目录: {gameProjectDir}");
                    }
                    
                    // 检查是否是子目录结构（如：StellarBlade\SB\Binaries\Win64）
                    var parentDir = Directory.GetParent(gameProjectDir)?.FullName;
                    if (!string.IsNullOrEmpty(parentDir))
                    {
                        // 查找兄弟目录或其他子目录的Content\Paks
                        try
                        {
                            var siblingDirs = Directory.GetDirectories(parentDir);
                            foreach (var siblingDir in siblingDirs)
                            {
                                var contentPaks = Path.Combine(siblingDir, "Content", "Paks");
                                if (Directory.Exists(contentPaks))
                                {
                                    modPathPatterns.AddRange(new[]
                                    {
                                        Path.Combine(contentPaks, "~mods"),
                                        Path.Combine(contentPaks, "Mods"),
                                        Path.Combine(contentPaks, "mods"),
                                        contentPaks
                                    });
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[DEBUG] 搜索兄弟目录时出错: {ex.Message}");
                        }
                    }
                }

                // 特定游戏的MOD路径规则
                var gameSpecificModPaths = new Dictionary<string, List<string>>
                {
                    ["剑星"] = new List<string>
                    {
                        "SB\\Content\\Paks\\~mods",
                        "StellarBlade\\SB\\Content\\Paks\\~mods",
                        "SB\\Content\\Paks\\Mods",
                        "SB\\Content\\Paks"
                    },
                    ["Stellar Blade"] = new List<string>
                    {
                        "SB\\Content\\Paks\\~mods",
                        "StellarBlade\\SB\\Content\\Paks\\~mods",
                        "SB\\Content\\Paks\\Mods",
                        "SB\\Content\\Paks"
                    },
                    ["黑神话悟空"] = new List<string>
                    {
                        "b1\\Content\\Paks\\~mods",
                        "BlackMythWukong\\b1\\Content\\Paks\\~mods",
                        "b1\\Content\\Paks\\Mods",
                        "b1\\Content\\Paks"
                    },
                    ["黑神话·悟空"] = new List<string>
                    {
                        "b1\\Content\\Paks\\~mods",
                        "BlackMythWukong\\b1\\Content\\Paks\\~mods",
                        "b1\\Content\\Paks\\Mods",
                        "b1\\Content\\Paks"
                    },
                    ["Black Myth Wukong"] = new List<string>
                    {
                        "b1\\Content\\Paks\\~mods",
                        "BlackMythWukong\\b1\\Content\\Paks\\~mods",
                        "b1\\Content\\Paks\\Mods",
                        "b1\\Content\\Paks"
                    }
                };

                // 提取游戏名称的核心部分（去掉括号内容）
                var coreGameName = gameName.Split('(')[0].Trim();
                
                // 添加特定游戏的路径模式
                if (gameSpecificModPaths.ContainsKey(coreGameName) || gameSpecificModPaths.ContainsKey(gameName))
                {
                    var gameRootDir = FindGameRootDirectory(exePath);
                    if (!string.IsNullOrEmpty(gameRootDir))
                    {
                        foreach (var relativePath in gameSpecificModPaths[coreGameName] ?? gameSpecificModPaths[gameName])
                        {
                            var fullPath = Path.Combine(gameRootDir, relativePath);
                            modPathPatterns.Add(fullPath);
                        }
                    }
                }

                // 测试所有可能的MOD路径
                foreach (var modPath in modPathPatterns)
                {
                    try
                    {
                        var normalizedPath = Path.GetFullPath(modPath);
                        Console.WriteLine($"[DEBUG] 测试MOD路径: {normalizedPath}");
                        
                        // 如果目录存在，直接返回
                        if (Directory.Exists(normalizedPath))
                        {
                            Console.WriteLine($"[SUCCESS] 找到现有MOD目录: {normalizedPath}");
                            return normalizedPath;
                        }
                        
                        // 如果父目录存在且是Content/Paks，这个路径很可能是正确的
                        var parentDir = Path.GetDirectoryName(normalizedPath);
                        if (Directory.Exists(parentDir) && 
                            (parentDir.EndsWith("Paks", StringComparison.OrdinalIgnoreCase) ||
                             parentDir.EndsWith("Content", StringComparison.OrdinalIgnoreCase)))
                        {
                            Console.WriteLine($"[SUCCESS] 推导出可能的MOD路径: {normalizedPath}");
                            return normalizedPath;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[DEBUG] 测试路径 {modPath} 时出错: {ex.Message}");
                    }
                }

                Console.WriteLine($"[WARNING] 未能推导出MOD路径");
                return "";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 推导MOD路径失败: {ex.Message}");
                return "";
            }
        }

        // 查找游戏根目录
        private string FindGameRootDirectory(string exePath)
        {
            try
            {
                var currentDir = Path.GetDirectoryName(exePath);
                
                // 向上查找，直到找到看起来像游戏根目录的地方
                while (!string.IsNullOrEmpty(currentDir))
                {
                    var dirName = Path.GetFileName(currentDir);
                    
                    // 如果是Steam游戏目录或其他游戏分发平台的特征
                    var parentDirName = Path.GetFileName(Path.GetDirectoryName(currentDir)) ?? "";
                    
                    if (parentDirName.Equals("steamapps", StringComparison.OrdinalIgnoreCase) ||
                        parentDirName.Equals("common", StringComparison.OrdinalIgnoreCase) ||
                        dirName.Contains("StellarBlade") ||
                        dirName.Contains("BlackMythWukong") ||
                        dirName.Contains("Wukong"))
                    {
                        return currentDir;
                    }

                    currentDir = Path.GetDirectoryName(currentDir);
                }
                
                // 如果没找到特殊标记，返回exe文件往上3级的目录 (Win64/Binaries/ProjectDir)
                var fallbackDir = Path.GetDirectoryName(exePath);
                for (int i = 0; i < 3 && !string.IsNullOrEmpty(fallbackDir); i++)
                {
                    fallbackDir = Path.GetDirectoryName(fallbackDir);
                }
                
                return fallbackDir ?? "";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 查找游戏根目录失败: {ex.Message}");
                return "";
            }
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
            // 提取游戏名称的核心部分（去掉括号内容）
            var coreGameName = gameName.Split('(')[0].Trim();
            
            return coreGameName switch
            {
                "剑星" => new[] { "Stellar Blade", "StellarBlade", "Stellarblade" },
                "黑神话·悟空" => new[] { "Black Myth Wukong", "BlackMythWukong", "Black Myth- Wukong", "b1-win64-shipping" },
                "光与影：33号远征队" => new[] { "Enshrouded" },
                "艾尔登法环" => new[] { "Elden Ring", "EldenRing" },
                "赛博朋克2077" => new[] { "Cyberpunk 2077", "Cyberpunk2077" },
                "巫师3" => new[] { "The Witcher 3 Wild Hunt", "Witcher3" },
                _ => new[] { coreGameName, coreGameName.Replace(" ", ""), coreGameName.Replace(" ", "_"), coreGameName.Replace("·", " "), gameName }
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