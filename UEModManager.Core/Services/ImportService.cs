using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UEModManager.Core.Models;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace UEModManager.Core.Services
{
    /// <summary>
    /// MOD导入服务
    /// </summary>
    public class ImportService
    {
        private readonly ILogger<ImportService> _logger;
        private readonly ModService _modService;
        private readonly ArchiveService _archiveService;
        private readonly GameService _gameService;

        public ImportService(ILogger<ImportService> logger, ModService modService, ArchiveService archiveService, GameService gameService)
        {
            _logger = logger;
            _modService = modService;
            _archiveService = archiveService;
            _gameService = gameService;
        }

        /// <summary>
        /// 支持的压缩包格式
        /// </summary>
        public static readonly string[] SupportedArchiveFormats = { ".zip", ".rar", ".7z", ".tar", ".gz" };

        /// <summary>
        /// 支持的MOD文件格式
        /// </summary>
        public static readonly string[] SupportedModFormats = { ".pak", ".ucas", ".utoc", ".uasset", ".umap" };

        /// <summary>
        /// 导入文件（支持单个文件或文件夹）
        /// </summary>
        public async Task<ImportResult> ImportAsync(string filePath, string? customName = null)
        {
            try
            {
                _logger.LogInformation($"开始导入: {filePath}");

                if (File.Exists(filePath))
                {
                    return await ImportFileAsync(filePath, customName);
                }
                else if (Directory.Exists(filePath))
                {
                    return await ImportDirectoryAsync(filePath, customName);
                }
                else
                {
                    return new ImportResult { Success = false, ErrorMessage = "指定的文件或文件夹不存在" };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"导入失败: {filePath}");
                return new ImportResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        /// <summary>
        /// 批量导入文件
        /// </summary>
        public async Task<List<ImportResult>> ImportBatchAsync(IEnumerable<string> filePaths)
        {
            var results = new List<ImportResult>();
            
            foreach (var filePath in filePaths)
            {
                var result = await ImportAsync(filePath);
                results.Add(result);
            }

            return results;
        }

        /// <summary>
        /// 导入压缩包
        /// </summary>
        public async Task<ImportResult> ImportArchiveAsync(string archivePath, string? customName = null, bool installDirectly = false)
        {
            try
            {
                _logger.LogInformation($"导入压缩包: {archivePath}");

                // 检查是否为支持的压缩包格式
                var extension = Path.GetExtension(archivePath).ToLowerInvariant();
                if (!SupportedArchiveFormats.Contains(extension))
                {
                    return new ImportResult { Success = false, ErrorMessage = "不支持的压缩包格式" };
                }

                // 创建临时解压目录
                var tempDir = Path.Combine(Path.GetTempPath(), "UEModManager", Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempDir);

                try
                {
                    // 解压文件
                    var extractSuccess = await _archiveService.ExtractArchiveAsync(archivePath, tempDir);
                    
                    if (!extractSuccess)
                    {
                        return new ImportResult { Success = false, ErrorMessage = "压缩包为空或解压失败" };
                    }

                    // 分析压缩包内容
                    var analysis = AnalyzeExtractedContent(tempDir);
                    
                    if (analysis.ModFiles.Count == 0 && analysis.SubDirectories.Count == 0)
                    {
                        return new ImportResult { Success = false, ErrorMessage = "压缩包中未找到有效的MOD文件" };
                    }

                    // 生成MOD名称
                    var modName = customName ?? Path.GetFileNameWithoutExtension(archivePath);
                    
                    if (installDirectly)
                    {
                        // 直接安装MOD
                        var installResult = await InstallModFromTempAsync(tempDir, modName, analysis);
                        return installResult;
                    }
                    else
                    {
                        // 返回分析结果供用户确认
                        return new ImportResult
                        {
                            Success = true,
                            ModName = modName,
                            TempPath = tempDir,
                            Analysis = analysis,
                            RequiresUserInput = true
                        };
                    }
                }
                finally
                {
                    // 如果不需要保留临时文件，则清理
                    if (installDirectly && Directory.Exists(tempDir))
                    {
                        try
                        {
                            Directory.Delete(tempDir, true);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, $"清理临时目录失败: {tempDir}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"导入压缩包失败: {archivePath}");
                return new ImportResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        /// <summary>
        /// 从临时目录安装MOD
        /// </summary>
        public async Task<ImportResult> InstallModFromTempAsync(string tempPath, string modName, ContentAnalysis? analysis = null)
        {
            try
            {
                // 如果没有提供分析结果，重新分析
                analysis ??= AnalyzeExtractedContent(tempPath);

                // 创建MOD安装目录
                var modInstallPath = Path.Combine(GetModInstallDirectory(), SanitizeFileName(modName));
                
                // 如果目录已存在，添加数字后缀
                var counter = 1;
                var originalPath = modInstallPath;
                while (Directory.Exists(modInstallPath))
                {
                    modInstallPath = $"{originalPath}_{counter}";
                    counter++;
                }

                Directory.CreateDirectory(modInstallPath);

                // 复制文件
                await CopyDirectoryAsync(tempPath, modInstallPath);

                // 创建MOD信息
                var modInfo = await _modService.AddModAsync(modName, modInstallPath, $"从压缩包导入: {Path.GetFileName(tempPath)}");
                
                // 设置分类
                if (analysis.SuggestedCategories.Any())
                {
                    modInfo.Categories = analysis.SuggestedCategories.ToList();
                    await _modService.UpdateModAsync(modInfo);
                }

                // 清理临时目录
                if (Directory.Exists(tempPath))
                {
                    try
                    {
                        Directory.Delete(tempPath, true);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"清理临时目录失败: {tempPath}");
                    }
                }

                _logger.LogInformation($"MOD安装成功: {modName} -> {modInstallPath}");
                
                return new ImportResult
                {
                    Success = true,
                    ModName = modName,
                    InstallPath = modInstallPath,
                    ModInfo = modInfo
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"从临时目录安装MOD失败: {tempPath}");
                return new ImportResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        /// <summary>
        /// 导入单个文件
        /// </summary>
        private async Task<ImportResult> ImportFileAsync(string filePath, string? customName = null)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            
            // 如果是压缩包，使用压缩包导入
            if (SupportedArchiveFormats.Contains(extension))
            {
                return await ImportArchiveAsync(filePath, customName, false);
            }
            
            // 如果是MOD文件，直接复制
            if (SupportedModFormats.Contains(extension))
            {
                return await ImportModFileAsync(filePath, customName);
            }

            return new ImportResult { Success = false, ErrorMessage = "不支持的文件格式" };
        }

        /// <summary>
        /// 导入文件夹
        /// </summary>
        private async Task<ImportResult> ImportDirectoryAsync(string directoryPath, string? customName = null)
        {
            try
            {
                var modName = customName ?? Path.GetFileName(directoryPath);
                var modInstallPath = Path.Combine(GetModInstallDirectory(), SanitizeFileName(modName));

                // 如果目录已存在，添加数字后缀
                var counter = 1;
                var originalPath = modInstallPath;
                while (Directory.Exists(modInstallPath))
                {
                    modInstallPath = $"{originalPath}_{counter}";
                    counter++;
                }

                // 复制整个目录
                await CopyDirectoryAsync(directoryPath, modInstallPath);

                // 创建MOD信息
                var modInfo = await _modService.AddModAsync(modName, modInstallPath, $"从文件夹导入: {directoryPath}");

                return new ImportResult
                {
                    Success = true,
                    ModName = modName,
                    InstallPath = modInstallPath,
                    ModInfo = modInfo
                };
            }
            catch (Exception ex)
            {
                return new ImportResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        /// <summary>
        /// 导入单个MOD文件
        /// </summary>
        private async Task<ImportResult> ImportModFileAsync(string filePath, string? customName = null)
        {
            try
            {
                var fileName = Path.GetFileNameWithoutExtension(filePath);
                var modName = customName ?? fileName;
                var modInstallPath = Path.Combine(GetModInstallDirectory(), SanitizeFileName(modName));

                Directory.CreateDirectory(modInstallPath);

                var destFilePath = Path.Combine(modInstallPath, Path.GetFileName(filePath));
                File.Copy(filePath, destFilePath, true);

                var modInfo = await _modService.AddModAsync(modName, modInstallPath, $"从文件导入: {Path.GetFileName(filePath)}");

                return new ImportResult
                {
                    Success = true,
                    ModName = modName,
                    InstallPath = modInstallPath,
                    ModInfo = modInfo
                };
            }
            catch (Exception ex)
            {
                return new ImportResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        /// <summary>
        /// 分析解压后的内容
        /// </summary>
        private ContentAnalysis AnalyzeExtractedContent(string directoryPath)
        {
            var analysis = new ContentAnalysis();

            try
            {
                // 递归分析所有文件
                AnalyzeDirectory(directoryPath, analysis, 0);

                // 基于文件内容推荐分类
                analysis.SuggestedCategories = SuggestCategories(analysis);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"分析目录内容失败: {directoryPath}");
            }

            return analysis;
        }

        /// <summary>
        /// 递归分析目录
        /// </summary>
        private void AnalyzeDirectory(string directoryPath, ContentAnalysis analysis, int depth)
        {
            if (depth > 10) return; // 防止过深的递归

            var dirInfo = new DirectoryInfo(directoryPath);
            
            foreach (var file in dirInfo.GetFiles())
            {
                var extension = file.Extension.ToLowerInvariant();
                
                if (SupportedModFormats.Contains(extension))
                {
                    analysis.ModFiles.Add(file.FullName);
                }
                else if (IsImageFile(extension))
                {
                    analysis.ImageFiles.Add(file.FullName);
                }
                else if (IsDocumentFile(extension))
                {
                    analysis.DocumentFiles.Add(file.FullName);
                }
                
                analysis.TotalFiles++;
                analysis.TotalSize += file.Length;
            }

            foreach (var subDir in dirInfo.GetDirectories())
            {
                analysis.SubDirectories.Add(subDir.FullName);
                AnalyzeDirectory(subDir.FullName, analysis, depth + 1);
            }
        }

        /// <summary>
        /// 基于内容推荐分类
        /// </summary>
        private List<string> SuggestCategories(ContentAnalysis analysis)
        {
            var categories = new List<string>();

            // 基于文件数量和类型推荐分类
            if (analysis.ModFiles.Count > 10)
            {
                categories.Add("大型MOD");
            }
            else if (analysis.ModFiles.Count > 0)
            {
                categories.Add("常规MOD");
            }

            if (analysis.ImageFiles.Count > 5)
            {
                categories.Add("美化");
            }

            // 基于文件路径推荐分类
            var pathText = string.Join(" ", analysis.ModFiles.Concat(analysis.SubDirectories)).ToLowerInvariant();
            
            if (pathText.Contains("weapon") || pathText.Contains("gun"))
                categories.Add("武器");
            else if (pathText.Contains("character") || pathText.Contains("skin"))
                categories.Add("角色");
            else if (pathText.Contains("map") || pathText.Contains("level"))
                categories.Add("地图");
            else if (pathText.Contains("ui") || pathText.Contains("interface"))
                categories.Add("界面");

            return categories.Any() ? categories : new List<string> { "其他" };
        }

        /// <summary>
        /// 检查是否为图片文件
        /// </summary>
        private bool IsImageFile(string extension)
        {
            return new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tga", ".dds" }.Contains(extension);
        }

        /// <summary>
        /// 检查是否为文档文件
        /// </summary>
        private bool IsDocumentFile(string extension)
        {
            return new[] { ".txt", ".md", ".readme", ".pdf", ".doc", ".docx" }.Contains(extension);
        }

        /// <summary>
        /// 异步复制目录
        /// </summary>
        private async Task CopyDirectoryAsync(string sourceDir, string destDir)
        {
            await Task.Run(() =>
            {
                var source = new DirectoryInfo(sourceDir);
                var dest = new DirectoryInfo(destDir);

                CopyDirectoryRecursive(source, dest);
            });
        }

        /// <summary>
        /// 递归复制目录
        /// </summary>
        private void CopyDirectoryRecursive(DirectoryInfo source, DirectoryInfo dest)
        {
            if (!dest.Exists)
            {
                dest.Create();
            }

            foreach (var file in source.GetFiles())
            {
                var destFilePath = Path.Combine(dest.FullName, file.Name);
                file.CopyTo(destFilePath, true);
            }

            foreach (var subDir in source.GetDirectories())
            {
                var destSubDir = new DirectoryInfo(Path.Combine(dest.FullName, subDir.Name));
                CopyDirectoryRecursive(subDir, destSubDir);
            }
        }

        /// <summary>
        /// 获取MOD安装目录
        /// </summary>
        private string GetModInstallDirectory()
        {
            // 从当前游戏配置中获取MOD路径
            if (_gameService.CurrentGame != null && !string.IsNullOrEmpty(_gameService.CurrentGame.ModPath))
            {
                var modDir = _gameService.CurrentGame.ModPath;
                Directory.CreateDirectory(modDir);
                return modDir;
            }

            // 如果没有配置游戏，使用默认路径
            var baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UEModManager", "Mods");
            Directory.CreateDirectory(baseDir);
            return baseDir;
        }

        /// <summary>
        /// 清理文件名，移除非法字符
        /// </summary>
        private string SanitizeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            foreach (var invalidChar in invalidChars)
            {
                fileName = fileName.Replace(invalidChar, '_');
            }
            return fileName.Trim();
        }
    }

    /// <summary>
    /// 导入结果
    /// </summary>
    public class ImportResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string? ModName { get; set; }
        public string? InstallPath { get; set; }
        public string? TempPath { get; set; }
        public ModInfo? ModInfo { get; set; }
        public ContentAnalysis? Analysis { get; set; }
        public bool RequiresUserInput { get; set; }
    }

    /// <summary>
    /// 内容分析结果
    /// </summary>
    public class ContentAnalysis
    {
        public List<string> ModFiles { get; set; } = new();
        public List<string> ImageFiles { get; set; } = new();
        public List<string> DocumentFiles { get; set; } = new();
        public List<string> SubDirectories { get; set; } = new();
        public List<string> SuggestedCategories { get; set; } = new();
        public int TotalFiles { get; set; }
        public long TotalSize { get; set; }
    }
}