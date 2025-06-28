using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Extensions.Logging;
using UEModManager.Core.Models;
using System.Text.Json;

namespace UEModManager.Core.Services
{
    /// <summary>
    /// 游戏管理服务
    /// </summary>
    public partial class GameService
    {
        private readonly ILogger<GameService> _logger;
        private readonly ObservableCollection<GameConfig> _games = new();
        private GameConfig? _currentGame;
        private string _dataPath = string.Empty;

        public ObservableCollection<GameConfig> Games => _games;
        public GameConfig? CurrentGame => _currentGame;

        public event EventHandler<GameConfig>? GameAdded;
        public event EventHandler<GameConfig>? GameRemoved;
        public event EventHandler<GameConfig>? CurrentGameChanged;

        public GameService(ILogger<GameService> logger)
        {
            _logger = logger;
            _dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UEModManager");
            Directory.CreateDirectory(_dataPath);
        }

        /// <summary>
        /// 初始化服务
        /// </summary>
        public async Task InitializeAsync()
        {
            await LoadGamesAsync();
            
            // 设置默认游戏
            if (_games.Any())
            {
                var lastUsedGame = _games.OrderByDescending(g => g.LastUsed).First();
                await SetCurrentGameAsync(lastUsedGame.Id);
            }
        }

        /// <summary>
        /// 检查是否为首次启动
        /// </summary>
        public bool IsFirstTimeStartup()
        {
            var configPath = Path.Combine(_dataPath, "games.json");
            return !File.Exists(configPath) || !_games.Any();
        }

        /// <summary>
        /// 设置当前游戏
        /// </summary>
        public async Task<bool> SetCurrentGameAsync(string gameId)
        {
            var game = _games.FirstOrDefault(g => g.Id == gameId);
            if (game == null) return false;

            // 更新之前的游戏状态
            if (_currentGame != null)
            {
                _currentGame.IsActive = false;
            }

            _currentGame = game;
            _currentGame.IsActive = true;
            _currentGame.LastUsed = DateTime.Now;

            await SaveGamesAsync();
            CurrentGameChanged?.Invoke(this, _currentGame);
            
            _logger.LogInformation($"切换当前游戏: {_currentGame.Name}");
            return true;
        }

        /// <summary>
        /// 添加游戏配置
        /// </summary>
        public async Task<GameConfig> AddGameAsync(string name, string gamePath, string modPath, string executableName = "")
        {
            var game = new GameConfig
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                GamePath = gamePath,
                ModPath = modPath,
                ExecutableName = executableName,
                LastUsed = DateTime.Now,
                SupportedModTypes = new List<string> { ".pak", ".zip", ".rar", ".7z" }
            };

            _games.Add(game);
            GameAdded?.Invoke(this, game);
            
            await SaveGamesAsync();
            _logger.LogInformation($"添加游戏配置: {game.Name}");
            
            return game;
        }

        /// <summary>
        /// 移除游戏配置
        /// </summary>
        public async Task<bool> RemoveGameAsync(string gameId)
        {
            var game = _games.FirstOrDefault(g => g.Id == gameId);
            if (game == null) return false;

            _games.Remove(game);
            GameRemoved?.Invoke(this, game);

            // 如果移除的是当前游戏，切换到其他游戏
            if (_currentGame?.Id == gameId)
            {
                _currentGame = null;
                if (_games.Any())
                {
                    await SetCurrentGameAsync(_games.First().Id);
                }
            }

            await SaveGamesAsync();
            _logger.LogInformation($"移除游戏配置: {game.Name}");
            
            return true;
        }

        /// <summary>
        /// 更新游戏配置
        /// </summary>
        public async Task UpdateGameAsync(GameConfig game)
        {
            await SaveGamesAsync();
            _logger.LogInformation($"更新游戏配置: {game.Name}");
        }

        /// <summary>
        /// 验证游戏路径
        /// </summary>
        public bool ValidateGamePath(string gamePath, string executableName)
        {
            if (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath))
                return false;

            if (!string.IsNullOrEmpty(executableName))
            {
                var exePath = Path.Combine(gamePath, executableName);
                return File.Exists(exePath);
            }

            return true;
        }

        /// <summary>
        /// 验证MOD路径
        /// </summary>
        public bool ValidateModPath(string modPath)
        {
            return !string.IsNullOrEmpty(modPath) && Directory.Exists(modPath);
        }

        /// <summary>
        /// 自动检测已安装的游戏
        /// </summary>
        public async Task<List<GameConfig>> AutoDetectGamesAsync()
        {
            var detectedGames = new List<GameConfig>();
            
            try
            {
                _logger.LogInformation("开始自动检测游戏...");
                
                // 1. 检测Steam游戏
                var steamGames = await DetectSteamGamesAsync();
                detectedGames.AddRange(steamGames);
                
                // 2. 检测Epic Games游戏
                var epicGames = await DetectEpicGamesAsync();
                detectedGames.AddRange(epicGames);
                
                // 3. 检测GOG游戏
                var gogGames = await DetectGOGGamesAsync();
                detectedGames.AddRange(gogGames);
                
                // 4. 检测Origin/EA Play游戏
                var originGames = await DetectOriginGamesAsync();
                detectedGames.AddRange(originGames);
                
                // 5. 回退到文件系统扫描
                var fallbackGames = await DetectGamesByFileSystemAsync();
                
                // 去除重复游戏
                foreach (var fallbackGame in fallbackGames)
                {
                    if (!detectedGames.Any(g => 
                        g.GamePath.Equals(fallbackGame.GamePath, StringComparison.OrdinalIgnoreCase)))
                    {
                        detectedGames.Add(fallbackGame);
                    }
                }
                
                _logger.LogInformation($"自动检测完成，找到 {detectedGames.Count} 个游戏");
                
                // 详细显示检测到的游戏
                if (detectedGames.Any())
                {
                    _logger.LogInformation("检测到的游戏列表:");
                    foreach (var game in detectedGames)
                    {
                        _logger.LogInformation($"  - {game.DisplayName}");
                        _logger.LogInformation($"    游戏路径: {game.GamePath}");
                        _logger.LogInformation($"    MOD路径: {game.ModPath}");
                        _logger.LogInformation($"    可执行文件: {game.ExecutableName}");
                    }
                }
                else
                {
                    _logger.LogInformation("未检测到任何支持的游戏");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "自动检测游戏时发生错误");
            }
            
            return detectedGames;
        }

        /// <summary>
        /// 检测Steam游戏
        /// </summary>
        private async Task<List<GameConfig>> DetectSteamGamesAsync()
        {
            var games = new List<GameConfig>();
            
            try
            {
                _logger.LogInformation("检测Steam游戏...");
                
                // 从注册表获取Steam安装路径
                var steamPath = GetSteamInstallPath();
                if (string.IsNullOrEmpty(steamPath))
                {
                    _logger.LogWarning("未找到Steam安装路径");
                    return games;
                }
                
                _logger.LogInformation($"Steam安装路径: {steamPath}");
                
                // 解析Steam库文件夹
                var libraryFolders = GetSteamLibraryFolders(steamPath);
                _logger.LogInformation($"找到 {libraryFolders.Count} 个Steam库文件夹");
                
                // 在每个库文件夹中查找游戏
                var knownGames = GetKnownUnrealEngineGames();
                foreach (var libraryPath in libraryFolders)
                {
                    var steamAppsPath = Path.Combine(libraryPath, "steamapps", "common");
                    if (!Directory.Exists(steamAppsPath)) continue;
                    
                    foreach (var gameInfo in knownGames)
                    {
                        foreach (var folderName in gameInfo.FolderNames)
                        {
                            var gamePath = Path.Combine(steamAppsPath, folderName);
                            if (Directory.Exists(gamePath))
                            {
                                var exePath = Path.Combine(gamePath, gameInfo.ExeRelativePath);
                                if (File.Exists(exePath))
                                {
                                    var modPath = Path.Combine(gamePath, gameInfo.ModRelativePath);
                                    EnsureDirectoryExists(modPath);
                                    
                                    var game = new GameConfig
                                    {
                                        Id = Guid.NewGuid().ToString(),
                                        Name = gameInfo.EnglishName ?? gameInfo.DisplayName,
                                        DisplayName = gameInfo.DisplayName,
                                        GameEmoji = gameInfo.GameEmoji,
                                        GamePath = gamePath,
                                        ModPath = modPath,
                                        ExecutableName = Path.GetFileName(exePath),
                                        SupportedModTypes = gameInfo.SupportedModTypes,
                                        Platform = "Steam"
                                    };
                                    
                                    if (!games.Any(g => g.GamePath.Equals(gamePath, StringComparison.OrdinalIgnoreCase)))
                                    {
                                        games.Add(game);
                                        _logger.LogInformation($"检测到Steam游戏: {gameInfo.DisplayName} -> {gamePath}");
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "检测Steam游戏时发生错误");
            }
            
            return games;
        }

        /// <summary>
        /// 检测Epic Games游戏
        /// </summary>
        private async Task<List<GameConfig>> DetectEpicGamesAsync()
        {
            var games = new List<GameConfig>();
            
            try
            {
                _logger.LogInformation("检测Epic Games游戏...");
                
                // 从注册表获取Epic Games Launcher数据路径
                var epicDataPath = GetEpicGamesDataPath();
                if (string.IsNullOrEmpty(epicDataPath))
                {
                    _logger.LogWarning("未找到Epic Games Launcher数据路径");
                    return games;
                }
                
                var manifestsPath = Path.Combine(epicDataPath, "Manifests");
                if (!Directory.Exists(manifestsPath))
                {
                    _logger.LogWarning($"Epic Games清单目录不存在: {manifestsPath}");
                    return games;
                }
                
                var knownGames = GetKnownUnrealEngineGames();
                var manifestFiles = Directory.GetFiles(manifestsPath, "*.item");
                
                foreach (var manifestFile in manifestFiles)
                {
                    try
                    {
                        var manifestContent = await File.ReadAllTextAsync(manifestFile);
                        var manifest = System.Text.Json.JsonSerializer.Deserialize<EpicGameManifest>(manifestContent);
                        
                        if (manifest != null && !string.IsNullOrEmpty(manifest.InstallLocation))
                        {
                            // 匹配已知游戏
                            var gameInfo = knownGames.FirstOrDefault(g => 
                                g.FolderNames.Any(name => 
                                    manifest.DisplayName.Contains(name, StringComparison.OrdinalIgnoreCase) ||
                                    manifest.AppName.Contains(name, StringComparison.OrdinalIgnoreCase)));
                            
                            if (gameInfo != null)
                            {
                                var exePath = Path.Combine(manifest.InstallLocation, gameInfo.ExeRelativePath);
                                if (File.Exists(exePath))
                                {
                                    var modPath = Path.Combine(manifest.InstallLocation, gameInfo.ModRelativePath);
                                    EnsureDirectoryExists(modPath);
                                    
                                    var game = new GameConfig
                                    {
                                        Id = Guid.NewGuid().ToString(),
                                        Name = gameInfo.EnglishName ?? gameInfo.DisplayName,
                                        DisplayName = gameInfo.DisplayName,
                                        GameEmoji = gameInfo.GameEmoji,
                                        GamePath = manifest.InstallLocation,
                                        ModPath = modPath,
                                        ExecutableName = Path.GetFileName(exePath),
                                        SupportedModTypes = gameInfo.SupportedModTypes,
                                        Platform = "Epic Games"
                                    };
                                    
                                    if (!games.Any(g => g.GamePath.Equals(manifest.InstallLocation, StringComparison.OrdinalIgnoreCase)))
                                    {
                                        games.Add(game);
                                        _logger.LogInformation($"检测到Epic Games游戏: {gameInfo.DisplayName} -> {manifest.InstallLocation}");
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"解析Epic Games清单文件失败: {manifestFile}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "检测Epic Games游戏时发生错误");
            }
            
            return games;
        }

        /// <summary>
        /// 检测GOG游戏
        /// </summary>
        private async Task<List<GameConfig>> DetectGOGGamesAsync()
        {
            var games = new List<GameConfig>();
            
            try
            {
                _logger.LogInformation("检测GOG游戏...");
                
                // 从注册表检测GOG游戏
                var gogGames = GetGOGGamesFromRegistry();
                var knownGames = GetKnownUnrealEngineGames();
                
                foreach (var gogGame in gogGames)
                {
                    var gameInfo = knownGames.FirstOrDefault(g => 
                        g.FolderNames.Any(name => 
                            gogGame.Name.Contains(name, StringComparison.OrdinalIgnoreCase) ||
                            Path.GetFileName(gogGame.Path).Contains(name, StringComparison.OrdinalIgnoreCase)));
                    
                    if (gameInfo != null)
                    {
                        var exePath = Path.Combine(gogGame.Path, gameInfo.ExeRelativePath);
                        if (File.Exists(exePath))
                        {
                            var modPath = Path.Combine(gogGame.Path, gameInfo.ModRelativePath);
                            EnsureDirectoryExists(modPath);
                            
                            var game = new GameConfig
                            {
                                Id = Guid.NewGuid().ToString(),
                                Name = gameInfo.EnglishName ?? gameInfo.DisplayName,
                                DisplayName = gameInfo.DisplayName,
                                GameEmoji = gameInfo.GameEmoji,
                                GamePath = gogGame.Path,
                                ModPath = modPath,
                                ExecutableName = Path.GetFileName(exePath),
                                SupportedModTypes = gameInfo.SupportedModTypes,
                                Platform = "GOG"
                            };
                            
                            if (!games.Any(g => g.GamePath.Equals(gogGame.Path, StringComparison.OrdinalIgnoreCase)))
                            {
                                games.Add(game);
                                _logger.LogInformation($"检测到GOG游戏: {gameInfo.DisplayName} -> {gogGame.Path}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "检测GOG游戏时发生错误");
            }
            
            return games;
        }

        /// <summary>
        /// 检测Origin/EA Play游戏
        /// </summary>
        private async Task<List<GameConfig>> DetectOriginGamesAsync()
        {
            var games = new List<GameConfig>();
            
            try
            {
                _logger.LogInformation("检测Origin/EA Play游戏...");
                
                var originLocalContentPath = @"C:\ProgramData\Origin\LocalContent";
                if (!Directory.Exists(originLocalContentPath))
                {
                    _logger.LogWarning($"Origin LocalContent目录不存在: {originLocalContentPath}");
                    return games;
                }
                
                var knownGames = GetKnownUnrealEngineGames();
                var gameDirectories = Directory.GetDirectories(originLocalContentPath);
                
                foreach (var gameDir in gameDirectories)
                {
                    try
                    {
                        var mfstFiles = Directory.GetFiles(gameDir, "*.mfst");
                        foreach (var mfstFile in mfstFiles)
                        {
                            var content = await File.ReadAllTextAsync(mfstFile);
                            var installPathMatch = System.Text.RegularExpressions.Regex.Match(content, @"dipinstallpath=([^&]+)");
                            
                            if (installPathMatch.Success)
                            {
                                var installPath = Uri.UnescapeDataString(installPathMatch.Groups[1].Value.Replace("%3a", ":"));
                                
                                var gameInfo = knownGames.FirstOrDefault(g => 
                                    g.FolderNames.Any(name => 
                                        installPath.Contains(name, StringComparison.OrdinalIgnoreCase)));
                                
                                if (gameInfo != null)
                                {
                                    var exePath = Path.Combine(installPath, gameInfo.ExeRelativePath);
                                    if (File.Exists(exePath))
                                    {
                                        var modPath = Path.Combine(installPath, gameInfo.ModRelativePath);
                                        EnsureDirectoryExists(modPath);
                                        
                                        var game = new GameConfig
                                        {
                                            Id = Guid.NewGuid().ToString(),
                                            Name = gameInfo.EnglishName ?? gameInfo.DisplayName,
                                            DisplayName = gameInfo.DisplayName,
                                            GameEmoji = gameInfo.GameEmoji,
                                            GamePath = installPath,
                                            ModPath = modPath,
                                            ExecutableName = Path.GetFileName(exePath),
                                            SupportedModTypes = gameInfo.SupportedModTypes,
                                            Platform = "Origin"
                                        };
                                        
                                        if (!games.Any(g => g.GamePath.Equals(installPath, StringComparison.OrdinalIgnoreCase)))
                                        {
                                            games.Add(game);
                                            _logger.LogInformation($"检测到Origin游戏: {gameInfo.DisplayName} -> {installPath}");
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"解析Origin游戏目录失败: {gameDir}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "检测Origin游戏时发生错误");
            }
            
            return games;
        }

        /// <summary>
        /// 通过文件系统检测游戏（回退方法）
        /// </summary>
        private async Task<List<GameConfig>> DetectGamesByFileSystemAsync()
        {
            var detectedGames = new List<GameConfig>();

            // 常见游戏安装路径
            var commonPaths = new List<string>();

            // Steam 路径
            var steamPaths = new[]
            {
                @"C:\Program Files (x86)\Steam\steamapps\common",
                @"C:\Program Files\Steam\steamapps\common",
                @"D:\Program Files (x86)\Steam\steamapps\common",
                @"D:\Program Files\Steam\steamapps\common",
                @"D:\Steam\steamapps\common",
                @"E:\Steam\steamapps\common",
                @"F:\Steam\steamapps\common"
            };

            // Epic Games 路径
            var epicPaths = new[]
            {
                @"C:\Program Files\Epic Games",
                @"D:\Program Files\Epic Games",
                @"D:\Epic Games",
                @"E:\Epic Games",
                @"F:\Epic Games"
            };

            // Xbox Game Pass 路径
            var xboxPaths = new[]
            {
                @"C:\Program Files\WindowsApps",
                @"D:\Program Files\WindowsApps"
            };

            commonPaths.AddRange(steamPaths.Where(Directory.Exists));
            commonPaths.AddRange(epicPaths.Where(Directory.Exists));
            // 跳过受保护的WindowsApps目录，避免权限错误

            // 预定义的主流虚幻引擎游戏信息
            var knownGames = GetKnownUnrealEngineGames();

            foreach (var basePath in commonPaths)
            {
                foreach (var gameInfo in knownGames)
                {
                    try
                    {
                        var gameDirs = Directory.GetDirectories(basePath, "*", SearchOption.TopDirectoryOnly)
                            .Where(dir => gameInfo.FolderNames.Any(folderName => 
                                Path.GetFileName(dir).Contains(folderName, StringComparison.OrdinalIgnoreCase)));

                        foreach (var gameDir in gameDirs)
                        {
                            var exePath = Path.Combine(gameDir, gameInfo.ExeRelativePath);
                            if (File.Exists(exePath))
                            {
                                var modPath = Path.Combine(gameDir, gameInfo.ModRelativePath);
                                EnsureDirectoryExists(modPath);
                                
                                // 检查是否已经添加了相同的游戏（避免重复）
                                var existingGame = detectedGames.FirstOrDefault(g => 
                                    g.GamePath.Equals(gameDir, StringComparison.OrdinalIgnoreCase));
                                
                                if (existingGame == null)
                                {
                                    // 如果是演示版，在名称中标注
                                    var gameName = gameInfo.DisplayName;
                                    if (gameDir.ToLower().Contains("demo"))
                                    {
                                        gameName += " (演示版)";
                                    }
                                    
                                    detectedGames.Add(new GameConfig
                                    {
                                        Id = Guid.NewGuid().ToString(),
                                        Name = !string.IsNullOrEmpty(gameInfo.EnglishName) ? gameInfo.EnglishName : gameName,
                                        DisplayName = gameName,
                                        GameEmoji = gameInfo.GameEmoji,
                                        GamePath = gameDir,
                                        ModPath = modPath,
                                        ExecutableName = Path.GetFileName(gameInfo.ExeRelativePath),
                                        SupportedModTypes = gameInfo.SupportedModTypes
                                    });
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"检测游戏时出错: {gameInfo.DisplayName}");
                    }
                }
            }

            return detectedGames;
        }

        /// <summary>
        /// 获取已知的虚幻引擎游戏配置
        /// </summary>
        private List<UnrealGameInfo> GetKnownUnrealEngineGames()
        {
            return new List<UnrealGameInfo>
            {
                // 剑星 (Stellar Blade)
                new UnrealGameInfo
                {
                    DisplayName = "剑星",
                    EnglishName = "Stellar Blade",
                    GameEmoji = "⚔️",
                    FolderNames = new[] { "StellarBlade" },
                    ExeRelativePath = @"SB\Binaries\Win64\SB-Win64-Shipping.exe",
                    ModRelativePath = @"SB\Content\Paks\~mods",
                    SupportedModTypes = new List<string> { ".pak", ".ucas", ".utoc" }
                },

                // 黑神话悟空 (Black Myth: Wukong)
                new UnrealGameInfo
                {
                    DisplayName = "黑神话悟空",
                    EnglishName = "Black Myth: Wukong",
                    GameEmoji = "🐒",
                    FolderNames = new[] { "BlackMythWukong", "BlackMyth", "Wukong" },
                    ExeRelativePath = @"b1\Binaries\Win64\b1-Win64-Shipping.exe",
                    ModRelativePath = @"b1\Content\Paks\~mods",
                    SupportedModTypes = new List<string> { ".pak", ".ucas", ".utoc" }
                },

                // 霍格沃茨之遗 (Hogwarts Legacy)
                new UnrealGameInfo
                {
                    DisplayName = "霍格沃茨之遗",
                    EnglishName = "Hogwarts Legacy",
                    GameEmoji = "🧙‍♂️",
                    FolderNames = new[] { "Hogwarts Legacy", "HogwartsLegacy" },
                    ExeRelativePath = @"Phoenix\Binaries\Win64\HogwartsLegacy.exe",
                    ModRelativePath = @"Phoenix\Content\Paks\~mods",
                    SupportedModTypes = new List<string> { ".pak", ".ucas", ".utoc" }
                },

                // 最终幻想7重制版 (Final Fantasy VII Remake)
                new UnrealGameInfo
                {
                    DisplayName = "最终幻想7重制版",
                    EnglishName = "Final Fantasy VII Remake",
                    GameEmoji = "🗡️",
                    FolderNames = new[] { "FINAL FANTASY VII REMAKE", "FF7Remake", "EndWalkerBootstrap" },
                    ExeRelativePath = @"End\Binaries\Win64\ff7remake_.exe",
                    ModRelativePath = @"End\Content\Paks\~mods",
                    SupportedModTypes = new List<string> { ".pak", ".ucas", ".utoc" }
                },

                // Satisfactory
                new UnrealGameInfo
                {
                    DisplayName = "幸福工厂",
                    EnglishName = "Satisfactory",
                    GameEmoji = "🏭",
                    FolderNames = new[] { "Satisfactory" },
                    ExeRelativePath = @"FactoryGame.exe",
                    ModRelativePath = @"FactoryGame\Mods",
                    SupportedModTypes = new List<string> { ".smod", ".pak", ".zip" }
                },

                // Deep Rock Galactic
                new UnrealGameInfo
                {
                    DisplayName = "Deep Rock Galactic",
                    FolderNames = new[] { "Deep Rock Galactic", "DeepRockGalactic" },
                    ExeRelativePath = @"FSD\Binaries\Win64\FSD-Win64-Shipping.exe",
                    ModRelativePath = @"FSD\Mods",
                    SupportedModTypes = new List<string> { ".pak", ".zip" }
                },

                // Dead by Daylight
                new UnrealGameInfo
                {
                    DisplayName = "Dead by Daylight",
                    FolderNames = new[] { "Dead by Daylight", "DeadByDaylight" },
                    ExeRelativePath = @"DeadByDaylight\Binaries\Win64\DeadByDaylight-Win64-Shipping.exe",
                    ModRelativePath = @"DeadByDaylight\Content\Paks\~mods",
                    SupportedModTypes = new List<string> { ".pak", ".ucas", ".utoc" }
                },

                // Fortnite (创意模式)
                new UnrealGameInfo
                {
                    DisplayName = "Fortnite",
                    FolderNames = new[] { "Fortnite" },
                    ExeRelativePath = @"FortniteGame\Binaries\Win64\FortniteClient-Win64-Shipping.exe",
                    ModRelativePath = @"FortniteGame\Content\Paks\~mods",
                    SupportedModTypes = new List<string> { ".pak", ".ucas", ".utoc" }
                },

                // Rocket League
                new UnrealGameInfo
                {
                    DisplayName = "Rocket League",
                    FolderNames = new[] { "rocketleague" },
                    ExeRelativePath = @"Binaries\Win64\RocketLeague.exe",
                    ModRelativePath = @"TAGame\CookedPCConsole\mods",
                    SupportedModTypes = new List<string> { ".upk", ".pak" }
                },

                // Gears 5
                new UnrealGameInfo
                {
                    DisplayName = "Gears 5",
                    FolderNames = new[] { "Gears 5", "Gears5" },
                    ExeRelativePath = @"Gears5\Binaries\Win64\Gears5.exe",
                    ModRelativePath = @"Gears5\Content\Paks\~mods",
                    SupportedModTypes = new List<string> { ".pak", ".ucas", ".utoc" }
                },

                // Borderlands 3
                new UnrealGameInfo
                {
                    DisplayName = "Borderlands 3",
                    FolderNames = new[] { "Borderlands3" },
                    ExeRelativePath = @"OakGame\Binaries\Win64\Borderlands3.exe",
                    ModRelativePath = @"OakGame\Content\Paks\~mods",
                    SupportedModTypes = new List<string> { ".pak", ".ucas", ".utoc" }
                },

                // Control
                new UnrealGameInfo
                {
                    DisplayName = "Control",
                    FolderNames = new[] { "Control" },
                    ExeRelativePath = @"Control_DX11.exe",
                    ModRelativePath = @"Control\Content\Paks\~mods",
                    SupportedModTypes = new List<string> { ".pak", ".ucas", ".utoc" }
                },

                // Subnautica
                new UnrealGameInfo
                {
                    DisplayName = "Subnautica",
                    FolderNames = new[] { "Subnautica" },
                    ExeRelativePath = @"Subnautica.exe",
                    ModRelativePath = @"Subnautica_Data\Managed\mods",
                    SupportedModTypes = new List<string> { ".dll", ".pak" }
                },

                // Subnautica: Below Zero
                new UnrealGameInfo
                {
                    DisplayName = "Subnautica: Below Zero",
                    FolderNames = new[] { "SubnauticaZero", "Subnautica Below Zero" },
                    ExeRelativePath = @"SubnauticaZero.exe",
                    ModRelativePath = @"SubnauticaZero_Data\Managed\mods",
                    SupportedModTypes = new List<string> { ".dll", ".pak" }
                },

                // Palworld
                new UnrealGameInfo
                {
                    DisplayName = "Palworld",
                    FolderNames = new[] { "Palworld" },
                    ExeRelativePath = @"Pal\Binaries\Win64\Palworld-Win64-Shipping.exe",
                    ModRelativePath = @"Pal\Content\Paks\~mods",
                    SupportedModTypes = new List<string> { ".pak", ".ucas", ".utoc" }
                },

                // Lies of P
                new UnrealGameInfo
                {
                    DisplayName = "Lies of P",
                    FolderNames = new[] { "Lies of P", "LiesOfP" },
                    ExeRelativePath = @"LiesofP\Binaries\Win64\LiesofP-Win64-Shipping.exe",
                    ModRelativePath = @"LiesofP\Content\Paks\~mods",
                    SupportedModTypes = new List<string> { ".pak", ".ucas", ".utoc" }
                }
            };
        }

        /// <summary>
        /// 启动游戏
        /// </summary>
        public async Task<bool> LaunchGameAsync(string gameId)
        {
            var game = _games.FirstOrDefault(g => g.Id == gameId);
            if (game == null || !ValidateGamePath(game.GamePath, game.ExecutableName))
                return false;

            try
            {
                var processStartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = game.FullExecutablePath,
                    WorkingDirectory = game.GamePath,
                    UseShellExecute = true
                };

                System.Diagnostics.Process.Start(processStartInfo);
                _logger.LogInformation($"启动游戏: {game.Name}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"启动游戏失败: {game.Name}");
                return false;
            }
        }

        /// <summary>
        /// 保存游戏配置
        /// </summary>
        private async Task SaveGamesAsync()
        {
            try
            {
                var filePath = Path.Combine(_dataPath, "games.json");
                var json = JsonSerializer.Serialize(_games.ToList(), new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(filePath, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "保存游戏配置失败");
            }
        }

        /// <summary>
        /// 加载游戏配置
        /// </summary>
        private async Task LoadGamesAsync()
        {
            try
            {
                var filePath = Path.Combine(_dataPath, "games.json");
                if (!File.Exists(filePath)) return;

                var json = await File.ReadAllTextAsync(filePath);
                var games = JsonSerializer.Deserialize<List<GameConfig>>(json);
                
                // 使用临时集合避免线程安全问题
                var tempGames = games?.ToList() ?? new List<GameConfig>();
                
                // 在主线程上操作，先清空再添加
                _games.Clear();
                foreach (var game in tempGames)
                {
                    _games.Add(game);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载游戏配置失败");
            }
        }

        /// <summary>
        /// 添加预设游戏
        /// </summary>
        private async Task AddPredefinedGamesAsync()
        {
            var detectedGames = await AutoDetectGamesAsync();
            foreach (var game in detectedGames)
            {
                _games.Add(game);
            }

            if (_games.Any())
            {
                await SaveGamesAsync();
            }
        }

        /// <summary>
        /// 从注册表获取Steam安装路径
        /// </summary>
        private string GetSteamInstallPath()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                return key?.GetValue("SteamPath")?.ToString() ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "无法从注册表获取Steam路径");
                
                // 尝试默认路径
                var defaultPaths = new[]
                {
                    @"C:\Program Files (x86)\Steam",
                    @"C:\Program Files\Steam",
                    @"D:\Steam",
                    @"E:\Steam"
                };
                
                foreach (var path in defaultPaths)
                {
                    if (Directory.Exists(path) && File.Exists(Path.Combine(path, "steam.exe")))
                    {
                        return path;
                    }
                }
                
                return string.Empty;
            }
        }

        /// <summary>
        /// 获取Steam库文件夹列表
        /// </summary>
        private List<string> GetSteamLibraryFolders(string steamPath)
        {
            var libraryFolders = new List<string> { steamPath };
            
            try
            {
                var libraryFoldersVdf = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
                if (File.Exists(libraryFoldersVdf))
                {
                    var content = File.ReadAllText(libraryFoldersVdf);
                    var pathMatches = System.Text.RegularExpressions.Regex.Matches(content, @"""path""\s*""([^""]+)""");
                    
                    foreach (System.Text.RegularExpressions.Match match in pathMatches)
                    {
                        var path = match.Groups[1].Value.Replace(@"\\", @"\");
                        if (Directory.Exists(path) && !libraryFolders.Contains(path))
                        {
                            libraryFolders.Add(path);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "解析Steam库文件夹配置失败");
            }
            
            return libraryFolders;
        }

        /// <summary>
        /// 从注册表获取Epic Games Launcher数据路径
        /// </summary>
        private string GetEpicGamesDataPath()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Epic Games\EpicGamesLauncher");
                return key?.GetValue("AppDataPath")?.ToString() ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "无法从注册表获取Epic Games数据路径");
                
                // 尝试默认路径
                var defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Epic");
                return Directory.Exists(defaultPath) ? defaultPath : string.Empty;
            }
        }

        /// <summary>
        /// 从注册表获取GOG游戏列表
        /// </summary>
        private List<GOGGameInfo> GetGOGGamesFromRegistry()
        {
            var games = new List<GOGGameInfo>();
            
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\GOG.com\Games");
                if (key != null)
                {
                    foreach (var subKeyName in key.GetSubKeyNames())
                    {
                        using var gameKey = key.OpenSubKey(subKeyName);
                        if (gameKey != null)
                        {
                            var gameName = gameKey.GetValue("GAMENAME")?.ToString();
                            var gamePath = gameKey.GetValue("PATH")?.ToString();
                            
                            if (!string.IsNullOrEmpty(gameName) && !string.IsNullOrEmpty(gamePath) && Directory.Exists(gamePath))
                            {
                                games.Add(new GOGGameInfo { Name = gameName, Path = gamePath });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "无法从注册表获取GOG游戏");
            }
            
            return games;
        }

        /// <summary>
        /// 确保目录存在
        /// </summary>
        private void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
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
    }

    /// <summary>
    /// 虚幻引擎游戏信息辅助类
    /// </summary>
    internal class UnrealGameInfo
    {
        /// <summary>
        /// 游戏显示名称（中文优先）
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// 游戏英文名称
        /// </summary>
        public string EnglishName { get; set; } = string.Empty;

        /// <summary>
        /// 游戏表情符号图标
        /// </summary>
        public string GameEmoji { get; set; } = "🎮";

        /// <summary>
        /// 可能的文件夹名称
        /// </summary>
        public string[] FolderNames { get; set; } = Array.Empty<string>();

        /// <summary>
        /// 游戏可执行文件相对路径
        /// </summary>
        public string ExeRelativePath { get; set; } = string.Empty;

        /// <summary>
        /// MOD文件夹相对路径
        /// </summary>
        public string ModRelativePath { get; set; } = string.Empty;

        /// <summary>
        /// 支持的MOD文件类型
        /// </summary>
        public List<string> SupportedModTypes { get; set; } = new();
    }

    /// <summary>
    /// Epic Games清单文件数据结构
    /// </summary>
    internal class EpicGameManifest
    {
        public string AppName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string InstallLocation { get; set; } = string.Empty;
        public string LaunchExecutable { get; set; } = string.Empty;
    }

    /// <summary>
    /// GOG游戏信息
    /// </summary>
    internal class GOGGameInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
    }
}
