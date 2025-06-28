using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using UEModManager.Core.Models;
using UEModManager.Core.Services;

namespace UEModManager.ViewModels
{
    /// <summary>
    /// 游戏路径确认对话框视图模型
    /// </summary>
    public partial class GamePathConfirmViewModel : ObservableObject
    {
        private readonly GameService _gameService;
        private readonly ILogger<GamePathConfirmViewModel> _logger;

        [ObservableProperty]
        private string _gameName = string.Empty;

        [ObservableProperty]
        private string _gamePath = string.Empty;

        [ObservableProperty]
        private string _modPath = string.Empty;

        [ObservableProperty]
        private string _backupPath = string.Empty;

        [ObservableProperty]
        private string _gamePathStatus = string.Empty;

        [ObservableProperty]
        private string _modPathStatus = string.Empty;

        [ObservableProperty]
        private string _backupPathStatus = string.Empty;

        [ObservableProperty]
        private bool _useDefaultBackupPath = true;

        [ObservableProperty]
        private bool _autoScanOnGameSwitch = true;

        [ObservableProperty]
        private bool _autoBackupMods = true;

        [ObservableProperty]
        private bool _scanSubfolders = true;

        /// <summary>
        /// 关闭窗口事件
        /// </summary>
        public event EventHandler<bool?>? CloseRequested;

        /// <summary>
        /// 配置结果
        /// </summary>
        public GameConfig? GameConfig { get; private set; }

        public GamePathConfirmViewModel(GameService gameService, ILogger<GamePathConfirmViewModel> logger)
        {
            _gameService = gameService;
            _logger = logger;

            // 监听属性变化
            PropertyChanged += OnPropertyChanged;
        }

        /// <summary>
        /// 初始化游戏配置
        /// </summary>
        public void InitializeWithGame(GameConfig gameConfig)
        {
            GameName = gameConfig.DisplayName ?? gameConfig.Name;
            
            // 如果游戏路径为空，尝试自动检测
            if (string.IsNullOrEmpty(gameConfig.GamePath) || string.IsNullOrEmpty(gameConfig.ModPath))
            {
                _logger.LogInformation($"游戏路径为空，尝试自动检测: {GameName}");
                AutoDetectGamePaths(gameConfig);
            }
            else
            {
                GamePath = gameConfig.GamePath;
                ModPath = gameConfig.ModPath;
            }
            
            // 设置默认备份路径
            if (UseDefaultBackupPath || string.IsNullOrEmpty(gameConfig.BackupPath))
            {
                BackupPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                                        "UEModManager", "Backups", gameConfig.Id);
            }
            else
            {
                BackupPath = gameConfig.BackupPath;
            }

            ValidatePaths();
        }

        /// <summary>
        /// 自动检测游戏路径
        /// </summary>
        private async void AutoDetectGamePaths(GameConfig gameConfig)
        {
            try
            {
                _logger.LogInformation($"开始自动检测游戏路径: {gameConfig.Name}");

                // 1. 尝试自动检测游戏安装路径
                var detectedGames = await _gameService.AutoDetectGamesAsync();
                var matchedGame = detectedGames.FirstOrDefault(g => 
                    g.Name.Equals(gameConfig.Name, StringComparison.OrdinalIgnoreCase) ||
                    g.DisplayName.Equals(gameConfig.DisplayName, StringComparison.OrdinalIgnoreCase));

                if (matchedGame != null)
                {
                    _logger.LogInformation($"通过自动检测找到游戏: {matchedGame.GamePath}");
                    GamePath = matchedGame.GamePath;
                    ModPath = matchedGame.ModPath;
                }
                else if (string.IsNullOrEmpty(GamePath))
                {
                    // 2. 如果自动检测失败，尝试智能路径推导
                    var smartDetectedPath = SmartDetectGamePath(gameConfig);
                    if (!string.IsNullOrEmpty(smartDetectedPath))
                    {
                        GamePath = smartDetectedPath;
                        ModPath = DeduceModPath(smartDetectedPath, gameConfig.Name);
                    }
                }
                else
                {
                    // 3. 如果已有游戏路径，智能推导MOD路径
                    if (string.IsNullOrEmpty(ModPath))
                    {
                        ModPath = DeduceModPath(GamePath, gameConfig.Name);
                    }
                }

                // 4. 确保目录存在
                EnsureDirectoryExists(ModPath);
                
                ValidatePaths();
                _logger.LogInformation($"游戏路径检测完成: 游戏={GamePath}, MOD={ModPath}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "自动检测游戏路径失败");
            }
        }

        /// <summary>
        /// 智能检测游戏路径
        /// </summary>
        private string SmartDetectGamePath(GameConfig gameConfig)
        {
            var commonBasePaths = new[]
            {
                @"C:\Program Files (x86)\Steam\steamapps\common",
                @"C:\Program Files\Steam\steamapps\common", 
                @"D:\Steam\steamapps\common",
                @"E:\Steam\steamapps\common",
                @"C:\Program Files\Epic Games",
                @"D:\Epic Games",
                @"E:\Epic Games"
            };

            // 根据游戏名称推测可能的文件夹名称
            var possibleFolderNames = GeneratePossibleGameFolders(gameConfig.Name, gameConfig.DisplayName);

            foreach (var basePath in commonBasePaths.Where(Directory.Exists))
            {
                foreach (var folderName in possibleFolderNames)
                {
                    var fullPath = Path.Combine(basePath, folderName);
                    if (Directory.Exists(fullPath))
                    {
                        // 检查是否有游戏可执行文件
                        var exeFiles = Directory.GetFiles(fullPath, "*.exe", SearchOption.AllDirectories);
                        if (exeFiles.Length > 0)
                        {
                            _logger.LogInformation($"智能检测找到游戏路径: {fullPath}");
                            return fullPath;
                        }
                    }
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// 生成可能的游戏文件夹名称
        /// </summary>
        private string[] GeneratePossibleGameFolders(string gameName, string displayName)
        {
            var folderNames = new List<string>();
            
            if (!string.IsNullOrEmpty(gameName))
            {
                folderNames.Add(gameName);
                folderNames.Add(gameName.Replace(" ", ""));
                folderNames.Add(gameName.Replace(" ", "_"));
                folderNames.Add(gameName.Replace(" ", "-"));
            }
            
            if (!string.IsNullOrEmpty(displayName) && displayName != gameName)
            {
                folderNames.Add(displayName);
                folderNames.Add(displayName.Replace(" ", ""));
                folderNames.Add(displayName.Replace(" ", "_"));
                folderNames.Add(displayName.Replace(" ", "-"));
            }

            // 特殊游戏名称映射
            var specialMappings = new Dictionary<string, string[]>
            {
                ["剑星"] = new[] { "StellarBlade", "Stellar Blade" },
                ["黑神话悟空"] = new[] { "Black Myth Wukong", "BlackMythWukong", "BlackMyth Wukong" },
                ["艾尔登法环"] = new[] { "Elden Ring", "EldenRing" },
                ["赛博朋克2077"] = new[] { "Cyberpunk 2077", "Cyberpunk2077" },
                ["巫师3"] = new[] { "The Witcher 3 Wild Hunt", "Witcher3" }
            };

            foreach (var mapping in specialMappings)
            {
                if (gameName?.Contains(mapping.Key) == true || displayName?.Contains(mapping.Key) == true)
                {
                    folderNames.AddRange(mapping.Value);
                }
            }

            return folderNames.Distinct().ToArray();
        }

        /// <summary>
        /// 根据游戏路径推导MOD路径
        /// </summary>
        private string DeduceModPath(string gamePath, string gameName)
        {
            if (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath))
                return string.Empty;

            // 虚幻引擎游戏的常见MOD路径模式
            var modPathPatterns = new[]
            {
                // 最常见：Game\Content\Paks\~mods\
                Path.Combine(gamePath, "Game", "Content", "Paks", "~mods"),
                // 常见变种：Game\Content\Mods\
                Path.Combine(gamePath, "Game", "Content", "Mods"),
                // 常见变种：Content\Paks\~mods\
                Path.Combine(gamePath, "Content", "Paks", "~mods"),
                // 简单版本：Mods\
                Path.Combine(gamePath, "Mods"),
                // 特殊：直接在Paks目录
                Path.Combine(gamePath, "Game", "Content", "Paks")
            };

            // 按优先级尝试路径
            foreach (var pattern in modPathPatterns)
            {
                if (Directory.Exists(pattern))
                {
                    _logger.LogInformation($"找到现有MOD目录: {pattern}");
                    return pattern;
                }
                
                // 检查父目录是否存在（可以创建）
                var parentDir = Directory.GetParent(pattern)?.FullName;
                if (!string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir))
                {
                    _logger.LogInformation($"推导MOD路径: {pattern}");
                    return pattern;
                }
            }

            // 如果都不存在，返回最推荐的路径
            var recommendedPath = Path.Combine(gamePath, "Game", "Content", "Paks", "~mods");
            _logger.LogInformation($"使用推荐MOD路径: {recommendedPath}");
            return recommendedPath;
        }

        /// <summary>
        /// 确保目录存在
        /// </summary>
        private void EnsureDirectoryExists(string path)
        {
            if (!string.IsNullOrEmpty(path) && !Directory.Exists(path))
            {
                try
                {
                    Directory.CreateDirectory(path);
                    _logger.LogInformation($"已创建目录: {path}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"无法创建目录: {path}");
                }
            }
        }

        /// <summary>
        /// 浏览游戏路径命令
        /// </summary>
        [RelayCommand]
        private void BrowseGamePath()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择游戏可执行文件",
                Filter = "可执行文件 (*.exe)|*.exe|所有文件 (*.*)|*.*",
                InitialDirectory = !string.IsNullOrEmpty(GamePath) ? Path.GetDirectoryName(GamePath) : null
            };

            if (dialog.ShowDialog() == true)
            {
                GamePath = Path.GetDirectoryName(dialog.FileName) ?? string.Empty;
            }
        }

        /// <summary>
        /// 浏览MOD路径命令
        /// </summary>
        [RelayCommand]
        private void BrowseModPath()
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "选择MOD安装目录",
                SelectedPath = ModPath,
                ShowNewFolderButton = true
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ModPath = dialog.SelectedPath;
            }
        }

        /// <summary>
        /// 浏览备份路径命令
        /// </summary>
        [RelayCommand]
        private void BrowseBackupPath()
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "选择MOD备份目录",
                SelectedPath = BackupPath,
                ShowNewFolderButton = true
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                BackupPath = dialog.SelectedPath;
                UseDefaultBackupPath = false;
            }
        }

        /// <summary>
        /// 确认命令
        /// </summary>
        [RelayCommand]
        private void Confirm()
        {
            try
            {
                // 验证路径
                if (!ValidatePaths())
                {
                    MessageBox.Show("请修正路径配置中的错误", "配置错误", 
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 尝试创建必要的目录
                if (!Directory.Exists(ModPath))
                {
                    try
                    {
                        Directory.CreateDirectory(ModPath);
                        _logger.LogInformation($"已创建MOD目录: {ModPath}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"创建MOD目录失败: {ModPath}");
                        MessageBox.Show($"无法创建MOD目录：\n{ModPath}\n\n{ex.Message}", 
                                      "创建目录失败", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                if (!Directory.Exists(BackupPath))
                {
                    try
                    {
                        Directory.CreateDirectory(BackupPath);
                        _logger.LogInformation($"已创建备份目录: {BackupPath}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"创建备份目录失败: {BackupPath}");
                        MessageBox.Show($"无法创建备份目录：\n{BackupPath}\n\n{ex.Message}", 
                                      "创建目录失败", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                // 创建游戏配置
                GameConfig = new GameConfig
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = GameName,
                    DisplayName = GameName,
                    GamePath = GamePath,
                    ModPath = ModPath,
                    BackupPath = BackupPath,
                    AutoScanOnGameSwitch = AutoScanOnGameSwitch,
                    AutoBackupMods = AutoBackupMods,
                    ScanSubfolders = ScanSubfolders
                };

                _logger.LogInformation($"用户确认游戏配置: {GameName}");
                CloseRequested?.Invoke(this, true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "确认游戏配置失败");
                MessageBox.Show($"配置保存失败：\n{ex.Message}", "错误", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 取消命令
        /// </summary>
        [RelayCommand]
        private void Cancel()
        {
            _logger.LogInformation("用户取消游戏路径配置");
            CloseRequested?.Invoke(this, false);
        }

        /// <summary>
        /// 属性变化处理
        /// </summary>
        private void OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(GamePath):
                case nameof(ModPath):
                case nameof(BackupPath):
                    ValidatePaths();
                    break;
                case nameof(UseDefaultBackupPath):
                    if (UseDefaultBackupPath)
                    {
                        BackupPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                                                "UEModManager", "Backups", GameName);
                    }
                    break;
            }
        }

        /// <summary>
        /// 验证路径配置
        /// </summary>
        private bool ValidatePaths()
        {
            bool isValid = true;

            // 验证游戏路径
            if (string.IsNullOrEmpty(GamePath))
            {
                GamePathStatus = "❌ 请选择游戏安装路径";
                isValid = false;
            }
            else if (!Directory.Exists(GamePath))
            {
                GamePathStatus = "❌ 游戏路径不存在";
                isValid = false;
            }
            else
            {
                // 检查是否有可执行文件
                var exeFiles = Directory.GetFiles(GamePath, "*.exe", SearchOption.AllDirectories);
                if (exeFiles.Length > 0)
                {
                    GamePathStatus = $"✅ 路径有效，找到 {exeFiles.Length} 个可执行文件";
                }
                else
                {
                    GamePathStatus = "⚠️ 路径有效，但未找到可执行文件";
                }
            }

            // 验证MOD路径
            if (string.IsNullOrEmpty(ModPath))
            {
                ModPathStatus = "❌ 请选择MOD安装路径";
                isValid = false;
            }
            else
            {
                var modDir = new DirectoryInfo(ModPath);
                if (modDir.Exists)
                {
                    var pakFiles = modDir.GetFiles("*.pak", SearchOption.AllDirectories);
                    ModPathStatus = $"✅ 路径有效，找到 {pakFiles.Length} 个PAK文件";
                }
                else
                {
                    ModPathStatus = "⚠️ 路径不存在，将在确认时创建";
                }
            }

            // 验证备份路径
            if (string.IsNullOrEmpty(BackupPath))
            {
                BackupPathStatus = "❌ 请选择备份路径";
                isValid = false;
            }
            else
            {
                var backupDir = new DirectoryInfo(BackupPath);
                if (backupDir.Exists)
                {
                    var backupFolders = backupDir.GetDirectories();
                    BackupPathStatus = $"✅ 路径有效，找到 {backupFolders.Length} 个备份文件夹";
                }
                else
                {
                    BackupPathStatus = "⚠️ 路径不存在，将在确认时创建";
                }
            }

            return isValid;
        }
    }
} 