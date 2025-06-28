using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UEModManager.Core.Models;
using System.Text.Json;

namespace UEModManager.Core.Services
{
    /// <summary>
    /// MOD管理服务
    /// </summary>
    public class ModService
    {
        private readonly ILogger<ModService> _logger;
        private readonly ObservableCollection<ModInfo> _mods = new();
        private string _currentGameId = string.Empty;
        private string _dataPath = string.Empty;

        public ObservableCollection<ModInfo> Mods => _mods;

        public event EventHandler<ModInfo>? ModAdded;
        public event EventHandler<ModInfo>? ModRemoved;
        public event EventHandler<ModInfo>? ModUpdated;
        public event EventHandler<string>? GameChanged;

        public ModService(ILogger<ModService> logger)
        {
            _logger = logger;
            _dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UEModManager");
            Directory.CreateDirectory(_dataPath);
        }

        /// <summary>
        /// 设置当前游戏
        /// </summary>
        public async Task SetCurrentGameAsync(string gameId)
        {
            if (_currentGameId == gameId) return;

            // 保存当前游戏的MOD数据
            if (!string.IsNullOrEmpty(_currentGameId))
            {
                await SaveModsAsync();
            }

            _currentGameId = gameId;
            
            // 加载新游戏的MOD数据
            await LoadModsAsync();
            
            GameChanged?.Invoke(this, gameId);
            _logger.LogInformation($"切换到游戏: {gameId}");
        }

        /// <summary>
        /// 扫描指定路径的MOD文件（基于旧版本逻辑）
        /// </summary>
        public async Task<List<ModInfo>> ScanModDirectoryAsync(string modPath)
        {
            if (string.IsNullOrEmpty(modPath) || !Directory.Exists(modPath))
            {
                _logger.LogWarning($"MOD路径不存在或无效: {modPath}");
                return new List<ModInfo>();
            }

            _logger.LogInformation($"开始扫描MOD目录: {modPath}");
            var foundMods = new List<ModInfo>();
            var modNamesSeen = new HashSet<string>(); // 用于跟踪已经看到的MOD名称
            var existingMods = _mods.ToDictionary(m => m.InstallPath, m => m); // 现有MOD映射

            try
            {
                var modDirectory = new DirectoryInfo(modPath);
                
                // 递归查找所有pak文件
                var pakFiles = modDirectory.GetFiles("*.pak", SearchOption.AllDirectories);
                
                foreach (var pakFile in pakFiles)
                {
                    var ucasFile = new FileInfo(Path.ChangeExtension(pakFile.FullName, ".ucas"));
                    var utocFile = new FileInfo(Path.ChangeExtension(pakFile.FullName, ".utoc"));
                    
                    // 必须同目录下有同名ucas/utoc才算完整MOD
                    if (ucasFile.Exists && utocFile.Exists)
                    {
                        var modName = Path.GetFileNameWithoutExtension(pakFile.Name);
                        var folderName = pakFile.Directory?.Name ?? "";
                        
                        // 创建MOD ID，优先使用mod_name，如果已存在则使用folder_name_mod_name
                        var modId = modName;
                        if (modNamesSeen.Contains(modName))
                        {
                            // 如果MOD名称已存在，使用文件夹名称+MOD名称作为唯一标识
                            if (folderName != "~mods" && !string.IsNullOrEmpty(folderName))
                            {
                                modId = $"{folderName}_{modName}";
                                _logger.LogDebug($"MOD名称重复，使用文件夹名称作为标识: {modId}");
                            }
                        }
                        
                        modNamesSeen.Add(modName);
                        _logger.LogDebug($"找到完整MOD {modName} in {pakFile.Directory}, MOD ID: {modId}");
                        
                        // 确定文件相对路径
                        var modRootPath = new DirectoryInfo(modPath);
                        var relPath = Path.GetRelativePath(modRootPath.FullName, pakFile.Directory!.FullName);
                        
                        // 收集同目录下的所有相关文件
                        var allFiles = new List<string>();
                        var modFiles = pakFile.Directory.GetFiles()
                            .Where(f => f.Name.StartsWith(modName) || 
                                       IsModRelatedFile(f.Name))
                            .ToList();
                        
                        foreach (var file in modFiles)
                        {
                            var relativePath = relPath == "." 
                                ? file.Name 
                                : Path.Combine(relPath, file.Name);
                            allFiles.Add(relativePath);
                        }
                        
                        // 检查是否是已知的MOD（通过安装路径匹配）
                        var installPath = pakFile.Directory.FullName;
                        ModInfo? modInfo = null;
                        
                        if (existingMods.TryGetValue(installPath, out var existingMod))
                        {
                            // 如果是已知MOD，更新其信息但保留用户自定义数据
                            _logger.LogDebug($"匹配到已知MOD: {existingMod.Id}");
                            
                            modInfo = existingMod;
                            modInfo.FileSize = CalculateDirectorySize(installPath);
                            modInfo.LastModified = GetLastModifiedTime(modFiles);
                            
                            // 更新文件列表但保留其他用户自定义属性
                            // 保留用户可能修改的DisplayName, Description等
                        }
                        else
                        {
                            // 新发现的MOD
                            modInfo = new ModInfo
                            {
                                Id = Guid.NewGuid().ToString(),
                                Name = modId,
                                DisplayName = modName,
                                InstallPath = installPath,
                                InstallDate = DateTime.Now,
                                IsEnabled = true,
                                FileSize = CalculateDirectorySize(installPath),
                                LastModified = GetLastModifiedTime(modFiles),
                                Categories = new List<string> { DetermineModCategory(modName, folderName) },
                                Tags = GenerateModTags(modName, folderName),
                                Description = $"从 {(relPath == "." ? "根目录" : relPath)} 自动检测的MOD"
                            };
                            
                            // 尝试查找预览图片
                            await TryFindPreviewImageAsync(modInfo);
                        }
                        
                        if (modInfo != null)
                        {
                            foundMods.Add(modInfo);
                        }
                    }
                    else
                    {
                        _logger.LogDebug($"MOD文件不完整，缺少对应的ucas或utoc文件: {pakFile.FullName}");
                    }
                }
                
                _logger.LogInformation($"扫描完成，找到 {foundMods.Count} 个MOD");
                return foundMods;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"扫描MOD目录失败: {modPath}");
                throw new InvalidOperationException($"扫描MOD目录失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 刷新当前游戏的MOD列表
        /// </summary>
        public async Task RefreshModsAsync(string modPath)
        {
            try
            {
                var scannedMods = await ScanModDirectoryAsync(modPath);
                
                // 使用更智能的同步策略
                await SynchronizeModListAsync(scannedMods);
                
                await SaveModsAsync();
                _logger.LogInformation($"MOD列表已刷新，当前共有 {_mods.Count} 个MOD");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "刷新MOD列表失败");
                throw;
            }
        }

        /// <summary>
        /// 同步MOD列表（智能合并新旧数据）
        /// </summary>
        private async Task SynchronizeModListAsync(List<ModInfo> scannedMods)
        {
            var existingMods = _mods.ToList();
            var modsToRemove = new List<ModInfo>();
            var modsToAdd = new List<ModInfo>();
            var modsToUpdate = new List<ModInfo>();
            
            // 标记不再存在的MOD
            foreach (var existingMod in existingMods)
            {
                var stillExists = scannedMods.Any(sm => 
                    sm.InstallPath.Equals(existingMod.InstallPath, StringComparison.OrdinalIgnoreCase));
                
                if (!stillExists)
                {
                    modsToRemove.Add(existingMod);
                }
            }
            
            // 处理扫描到的MOD
            foreach (var scannedMod in scannedMods)
            {
                var existingMod = existingMods.FirstOrDefault(em => 
                    em.InstallPath.Equals(scannedMod.InstallPath, StringComparison.OrdinalIgnoreCase));
                
                if (existingMod == null)
                {
                    // 新MOD
                    modsToAdd.Add(scannedMod);
                }
                else if (existingMod.LastModified != scannedMod.LastModified || 
                         existingMod.FileSize != scannedMod.FileSize)
                {
                    // MOD已更新，合并数据
                    existingMod.FileSize = scannedMod.FileSize;
                    existingMod.LastModified = scannedMod.LastModified;
                    // 保留用户自定义的DisplayName, Description, Categories等
                    modsToUpdate.Add(existingMod);
                }
            }
            
            // 应用更改
            foreach (var mod in modsToRemove)
            {
                _mods.Remove(mod);
                ModRemoved?.Invoke(this, mod);
                _logger.LogInformation($"移除不存在的MOD: {mod.Name}");
            }
            
            foreach (var mod in modsToAdd)
            {
                _mods.Add(mod);
                ModAdded?.Invoke(this, mod);
                _logger.LogInformation($"添加新MOD: {mod.Name}");
            }
            
            foreach (var mod in modsToUpdate)
            {
                ModUpdated?.Invoke(this, mod);
                _logger.LogDebug($"更新MOD信息: {mod.Name}");
            }
        }

        /// <summary>
        /// 判断是否为MOD相关文件
        /// </summary>
        private static bool IsModRelatedFile(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            var relatedExtensions = new[] { ".pak", ".ucas", ".utoc", ".txt", ".md", ".readme", ".png", ".jpg", ".jpeg" };
            return relatedExtensions.Contains(extension);
        }

        /// <summary>
        /// 确定MOD分类
        /// </summary>
        private static string DetermineModCategory(string modName, string folderName)
        {
            var name = $"{modName} {folderName}".ToLowerInvariant();
            
            if (name.Contains("character") || name.Contains("outfit") || name.Contains("costume") || name.Contains("suit"))
                return "角色/服装";
            if (name.Contains("weapon") || name.Contains("sword") || name.Contains("blade"))
                return "武器";
            if (name.Contains("ui") || name.Contains("interface") || name.Contains("hud"))
                return "界面";
            if (name.Contains("audio") || name.Contains("sound") || name.Contains("music"))
                return "音频";
            if (name.Contains("texture") || name.Contains("visual") || name.Contains("graphic"))
                return "视觉效果";
            if (name.Contains("gameplay") || name.Contains("mechanic"))
                return "游戏机制";
            
            return "其他";
        }

        /// <summary>
        /// 生成MOD标签
        /// </summary>
        private static List<string> GenerateModTags(string modName, string folderName)
        {
            var tags = new List<string>();
            var name = $"{modName} {folderName}".ToLowerInvariant();
            
            if (name.Contains("nude") || name.Contains("nsfw"))
                tags.Add("成人内容");
            if (name.Contains("hd") || name.Contains("4k") || name.Contains("high"))
                tags.Add("高清");
            if (name.Contains("realistic"))
                tags.Add("写实");
            if (name.Contains("anime"))
                tags.Add("动漫风");
            if (name.Contains("fix") || name.Contains("bug"))
                tags.Add("修复");
            if (name.Contains("enhance") || name.Contains("improve"))
                tags.Add("增强");
                
            return tags;
        }

        /// <summary>
        /// 获取文件列表的最后修改时间
        /// </summary>
        private static DateTime GetLastModifiedTime(IEnumerable<FileInfo> files)
        {
            return files.Any() ? files.Max(f => f.LastWriteTime) : DateTime.MinValue;
        }

        /// <summary>
        /// 添加MOD
        /// </summary>
        public async Task<ModInfo> AddModAsync(string name, string installPath, string? description = null)
        {
            var mod = new ModInfo
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                InstallPath = installPath,
                Description = description ?? string.Empty,
                InstallDate = DateTime.Now,
                Categories = new List<string> { "其他" }
            };

            // 计算文件大小
            if (Directory.Exists(installPath))
            {
                mod.FileSize = CalculateDirectorySize(installPath);
            }

            // 寻找预览图片
            await TryFindPreviewImageAsync(mod);

            _mods.Add(mod);
            ModAdded?.Invoke(this, mod);
            
            await SaveModsAsync();
            _logger.LogInformation($"添加MOD: {mod.Name}");
            
            return mod;
        }

        /// <summary>
        /// 移除MOD
        /// </summary>
        public async Task<bool> RemoveModAsync(string modId)
        {
            var mod = _mods.FirstOrDefault(m => m.Id == modId);
            if (mod == null) return false;

            _mods.Remove(mod);
            ModRemoved?.Invoke(this, mod);
            
            await SaveModsAsync();
            _logger.LogInformation($"移除MOD: {mod.Name}");
            
            return true;
        }

        /// <summary>
        /// 启用/禁用MOD
        /// </summary>
        public async Task<bool> ToggleModAsync(string modId)
        {
            var mod = _mods.FirstOrDefault(m => m.Id == modId);
            if (mod == null) return false;

            mod.IsEnabled = !mod.IsEnabled;
            ModUpdated?.Invoke(this, mod);
            
            await SaveModsAsync();
            _logger.LogInformation($"切换MOD状态: {mod.Name} -> {mod.StatusText}");
            
            return true;
        }

        /// <summary>
        /// 批量启用/禁用MOD
        /// </summary>
        public async Task BatchToggleModsAsync(IEnumerable<string> modIds, bool enabled)
        {
            var modified = false;
            foreach (var modId in modIds)
            {
                var mod = _mods.FirstOrDefault(m => m.Id == modId);
                if (mod != null && mod.IsEnabled != enabled)
                {
                    mod.IsEnabled = enabled;
                    ModUpdated?.Invoke(this, mod);
                    modified = true;
                }
            }

            if (modified)
            {
                await SaveModsAsync();
                _logger.LogInformation($"批量{(enabled ? "启用" : "禁用")}了 {modIds.Count()} 个MOD");
            }
        }

        /// <summary>
        /// 更新MOD信息
        /// </summary>
        public async Task UpdateModAsync(ModInfo mod)
        {
            ModUpdated?.Invoke(this, mod);
            await SaveModsAsync();
            _logger.LogInformation($"更新MOD信息: {mod.Name}");
        }

        /// <summary>
        /// 搜索MOD
        /// </summary>
        public IEnumerable<ModInfo> SearchMods(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return _mods;

            keyword = keyword.ToLowerInvariant();
            return _mods.Where(mod =>
                mod.Name.ToLowerInvariant().Contains(keyword) ||
                mod.DisplayName.ToLowerInvariant().Contains(keyword) ||
                mod.Description.ToLowerInvariant().Contains(keyword) ||
                mod.Author.ToLowerInvariant().Contains(keyword) ||
                mod.Tags.Any(tag => tag.ToLowerInvariant().Contains(keyword))
            );
        }

        /// <summary>
        /// 按分类筛选MOD
        /// </summary>
        public IEnumerable<ModInfo> FilterByCategory(string category)
        {
            if (string.IsNullOrEmpty(category) || category == "全部")
                return _mods;

            if (category == "已启用")
                return _mods.Where(m => m.IsEnabled);

            if (category == "已禁用")
                return _mods.Where(m => !m.IsEnabled);

            return _mods.Where(mod => mod.Categories.Any(c => c == category));
        }

        /// <summary>
        /// 获取统计信息
        /// </summary>
        public (int Total, int Enabled, long TotalSize) GetStatistics()
        {
            return (_mods.Count, _mods.Count(m => m.IsEnabled), _mods.Sum(m => m.FileSize));
        }

        /// <summary>
        /// 保存MOD数据
        /// </summary>
        private async Task SaveModsAsync()
        {
            if (string.IsNullOrEmpty(_currentGameId)) return;

            try
            {
                var filePath = Path.Combine(_dataPath, $"{_currentGameId}_mods.json");
                var json = JsonSerializer.Serialize(_mods.ToList(), new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(filePath, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "保存MOD数据失败");
            }
        }

        /// <summary>
        /// 加载MOD数据
        /// </summary>
        private async Task LoadModsAsync()
        {
            if (string.IsNullOrEmpty(_currentGameId)) return;

            try
            {
                var filePath = Path.Combine(_dataPath, $"{_currentGameId}_mods.json");
                if (!File.Exists(filePath)) return;

                var json = await File.ReadAllTextAsync(filePath);
                var mods = JsonSerializer.Deserialize<List<ModInfo>>(json);
                
                _mods.Clear();
                if (mods != null)
                {
                    foreach (var mod in mods)
                    {
                        _mods.Add(mod);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载MOD数据失败");
            }
        }

        /// <summary>
        /// 计算目录大小
        /// </summary>
        private static long CalculateDirectorySize(string directoryPath)
        {
            try
            {
                var dir = new DirectoryInfo(directoryPath);
                return dir.GetFiles("*", SearchOption.AllDirectories).Sum(file => file.Length);
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 尝试找到预览图片
        /// </summary>
        private async Task TryFindPreviewImageAsync(ModInfo mod)
        {
            try
            {
                var imageExtensions = new[] { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp" };
                var searchPaths = new[]
                {
                    mod.InstallPath,
                    Path.Combine(mod.InstallPath, "preview"),
                    Path.Combine(mod.InstallPath, "images"),
                    Path.Combine(mod.InstallPath, "screenshots")
                };

                foreach (var searchPath in searchPaths)
                {
                    if (!Directory.Exists(searchPath)) continue;

                    var imageFiles = Directory.GetFiles(searchPath, "*.*", SearchOption.TopDirectoryOnly)
                        .Where(file => imageExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                        .OrderBy(f => f)
                        .ToList();

                    if (imageFiles.Any())
                    {
                        mod.PreviewImagePath = imageFiles.First();
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"查找预览图片失败: {mod.Name}");
            }
        }
    }
} 