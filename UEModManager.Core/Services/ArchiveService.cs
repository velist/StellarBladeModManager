using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Writers;

namespace UEModManager.Core.Services
{
    /// <summary>
    /// 压缩包处理服务
    /// </summary>
    public class ArchiveService
    {
        private readonly ILogger<ArchiveService> _logger;

        public ArchiveService(ILogger<ArchiveService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 解压文件到指定目录
        /// </summary>
        public async Task<bool> ExtractArchiveAsync(string archivePath, string destinationPath, IProgress<int>? progress = null)
        {
            try
            {
                if (!File.Exists(archivePath))
                {
                    _logger.LogError($"压缩包文件不存在: {archivePath}");
                    return false;
                }

                Directory.CreateDirectory(destinationPath);

                using var archive = ArchiveFactory.Open(archivePath);
                var totalEntries = archive.Entries.Count();
                var extractedEntries = 0;

                foreach (var entry in archive.Entries)
                {
                    if (!entry.IsDirectory)
                    {
                        var entryPath = Path.Combine(destinationPath, entry.Key);
                        var entryDirectory = Path.GetDirectoryName(entryPath);
                        
                        if (!string.IsNullOrEmpty(entryDirectory))
                        {
                            Directory.CreateDirectory(entryDirectory);
                        }

                        await Task.Run(() =>
                        {
                            entry.WriteToFile(entryPath, new ExtractionOptions
                            {
                                ExtractFullPath = true,
                                Overwrite = true
                            });
                        });

                        extractedEntries++;
                        progress?.Report((int)((double)extractedEntries / totalEntries * 100));
                    }
                }

                _logger.LogInformation($"成功解压文件: {archivePath} -> {destinationPath}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"解压文件失败: {archivePath}");
                return false;
            }
        }

        /// <summary>
        /// 检查是否为支持的压缩包格式
        /// </summary>
        public bool IsSupportedArchive(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".zip" => true,
                ".rar" => true,
                ".7z" => true,
                ".tar" => true,
                ".gz" => true,
                _ => false
            };
        }

        /// <summary>
        /// 获取压缩包信息
        /// </summary>
        public async Task<ArchiveInfo?> GetArchiveInfoAsync(string archivePath)
        {
            try
            {
                if (!File.Exists(archivePath))
                    return null;

                return await Task.Run(() =>
                {
                    using var archive = ArchiveFactory.Open(archivePath);
                    
                    var info = new ArchiveInfo
                    {
                        FilePath = archivePath,
                        EntryCount = archive.Entries.Count(),
                        TotalSize = 0
                    };

                    foreach (var entry in archive.Entries)
                    {
                        if (!entry.IsDirectory)
                        {
                            info.TotalSize += entry.Size;
                        }
                    }

                    return info;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"获取压缩包信息失败: {archivePath}");
                return null;
            }
        }

        /// <summary>
        /// 创建压缩包
        /// </summary>
        public async Task<bool> CreateArchiveAsync(string sourceDirectory, string archivePath, IProgress<int>? progress = null)
        {
            try
            {
                var sourceDir = new DirectoryInfo(sourceDirectory);
                if (!sourceDir.Exists)
                {
                    _logger.LogError($"源目录不存在: {sourceDirectory}");
                    return false;
                }

                await Task.Run(() =>
                {
                    using var archive = WriterFactory.Open(File.Create(archivePath), ArchiveType.Zip, new WriterOptions(CompressionType.Deflate));
                    
                    var files = sourceDir.GetFiles("*", SearchOption.AllDirectories);
                    var totalFiles = files.Length;
                    var processedFiles = 0;

                    foreach (var file in files)
                    {
                        var relativePath = Path.GetRelativePath(sourceDirectory, file.FullName);
                        using var fileStream = file.OpenRead();
                        archive.Write(relativePath, fileStream);
                        
                        processedFiles++;
                        progress?.Report((int)((double)processedFiles / totalFiles * 100));
                    }
                });

                _logger.LogInformation($"成功创建压缩包: {sourceDirectory} -> {archivePath}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"创建压缩包失败: {sourceDirectory}");
                return false;
            }
        }

        /// <summary>
        /// 验证压缩包完整性
        /// </summary>
        public async Task<bool> ValidateArchiveAsync(string archivePath)
        {
            try
            {
                return await Task.Run(() =>
                {
                    using var archive = ArchiveFactory.Open(archivePath);
                    
                    foreach (var entry in archive.Entries)
                    {
                        if (!entry.IsDirectory)
                        {
                            using var stream = entry.OpenEntryStream();
                            // 尝试读取一小部分数据来验证
                            var buffer = new byte[1024];
                            stream.Read(buffer, 0, buffer.Length);
                        }
                    }
                    
                    return true;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"验证压缩包失败: {archivePath}");
                return false;
            }
        }
    }

    /// <summary>
    /// 压缩包信息
    /// </summary>
    public class ArchiveInfo
    {
        public string FilePath { get; set; } = string.Empty;
        public int EntryCount { get; set; }
        public long TotalSize { get; set; }
        public string FormattedSize => FormatFileSize(TotalSize);

        private static string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
        }
    }
} 