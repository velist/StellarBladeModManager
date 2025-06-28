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
    /// 分类管理服务
    /// </summary>
    public class CategoryService
    {
        private readonly ILogger<CategoryService> _logger;
        private readonly ObservableCollection<CategoryItem> _categories = new();
        private string _currentGameId = string.Empty;
        private string _dataPath = string.Empty;

        public ObservableCollection<CategoryItem> Categories => _categories;

        public event EventHandler<CategoryItem>? CategoryAdded;
        public event EventHandler<CategoryItem>? CategoryRemoved;
        public event EventHandler<CategoryItem>? CategoryUpdated;

        public CategoryService(ILogger<CategoryService> logger)
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

            // 保存当前游戏的分类数据
            if (!string.IsNullOrEmpty(_currentGameId))
            {
                await SaveCategoriesAsync();
            }

            _currentGameId = gameId;
            
            // 加载新游戏的分类数据
            await LoadCategoriesAsync();
            
            _logger.LogInformation($"切换分类到游戏: {gameId}");
        }

        /// <summary>
        /// 添加分类
        /// </summary>
        public async Task<CategoryItem> AddCategoryAsync(string name, CategoryItem? parent = null)
        {
            var category = new CategoryItem
            {
                Name = name,
                DisplayName = name,
                ModCount = 0
            };

            if (parent != null)
            {
                parent.AddChild(category);
            }
            else
            {
                _categories.Add(category);
            }

            CategoryAdded?.Invoke(this, category);
            await SaveCategoriesAsync();
            
            _logger.LogInformation($"添加分类: {category.FullPath}");
            return category;
        }

        /// <summary>
        /// 移除分类
        /// </summary>
        public async Task<bool> RemoveCategoryAsync(CategoryItem category)
        {
            if (category.Parent != null)
            {
                category.Parent.RemoveChild(category);
            }
            else
            {
                _categories.Remove(category);
            }

            CategoryRemoved?.Invoke(this, category);
            await SaveCategoriesAsync();
            
            _logger.LogInformation($"移除分类: {category.FullPath}");
            return true;
        }

        /// <summary>
        /// 更新分类
        /// </summary>
        public async Task UpdateCategoryAsync(CategoryItem category)
        {
            CategoryUpdated?.Invoke(this, category);
            await SaveCategoriesAsync();
            
            _logger.LogInformation($"更新分类: {category.FullPath}");
        }

        /// <summary>
        /// 重命名分类
        /// </summary>
        public async Task<bool> RenameCategoryAsync(CategoryItem category, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName) || category.Name == newName)
                return false;

            category.Name = newName;
            category.DisplayName = newName;
            
            CategoryUpdated?.Invoke(this, category);
            await SaveCategoriesAsync();
            
            _logger.LogInformation($"重命名分类: {category.FullPath}");
            return true;
        }

        /// <summary>
        /// 更新分类MOD数量
        /// </summary>
        public void UpdateCategoryModCounts(IEnumerable<ModInfo> mods)
        {
            try
            {
                // 重置所有分类的MOD数量
                ResetModCounts(_categories);

                // 统计每个分类的MOD数量 - 使用ToList()复制集合避免枚举时修改
                var modList = mods.ToList();
                foreach (var mod in modList)
                {
                    var categories = mod.Categories?.ToList() ?? new List<string>();
                    foreach (var categoryPath in categories)
                    {
                        var category = FindCategoryByPath(categoryPath);
                        if (category != null)
                        {
                            category.ModCount++;
                        }
                    }
                }

                // 更新父分类的MOD数量（包含子分类）
                UpdateParentModCounts(_categories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新分类MOD数量失败");
            }
        }

        /// <summary>
        /// 根据路径查找分类
        /// </summary>
        public CategoryItem? FindCategoryByPath(string path)
        {
            var pathParts = path.Split('/');
            var current = _categories.AsEnumerable();

            foreach (var part in pathParts)
            {
                var category = current.FirstOrDefault(c => c.Name == part);
                if (category == null) return null;
                
                if (part == pathParts.Last())
                    return category;
                
                current = category.Children;
            }

            return null;
        }

        /// <summary>
        /// 获取所有分类的平铺列表
        /// </summary>
        public IEnumerable<CategoryItem> GetFlatCategories()
        {
            return GetFlatCategoriesRecursive(_categories);
        }

        /// <summary>
        /// 初始化默认分类
        /// </summary>
        public async Task InitializeDefaultCategoriesAsync()
        {
            if (_categories.Any()) return;

            var defaultCategories = new[]
            {
                "全部",
                "已启用", 
                "已禁用",
                "服装",
                "面部",
                "武器",
                "环境",
                "其他"
            };

            foreach (var categoryName in defaultCategories)
            {
                await AddCategoryAsync(categoryName);
            }

            _logger.LogInformation("初始化默认分类完成");
        }

        /// <summary>
        /// 保存分类数据
        /// </summary>
        private async Task SaveCategoriesAsync()
        {
            if (string.IsNullOrEmpty(_currentGameId)) return;

            try
            {
                var filePath = Path.Combine(_dataPath, $"{_currentGameId}_categories.json");
                var json = JsonSerializer.Serialize(_categories.ToList(), new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(filePath, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "保存分类数据失败");
            }
        }

        /// <summary>
        /// 加载分类数据
        /// </summary>
        private async Task LoadCategoriesAsync()
        {
            if (string.IsNullOrEmpty(_currentGameId)) return;

            try
            {
                var filePath = Path.Combine(_dataPath, $"{_currentGameId}_categories.json");
                if (!File.Exists(filePath))
                {
                    await InitializeDefaultCategoriesAsync();
                    return;
                }

                var json = await File.ReadAllTextAsync(filePath);
                var categories = JsonSerializer.Deserialize<List<CategoryItem>>(json);
                
                _categories.Clear();
                if (categories != null)
                {
                    foreach (var category in categories)
                    {
                        _categories.Add(category);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载分类数据失败");
                await InitializeDefaultCategoriesAsync();
            }
        }

        /// <summary>
        /// 重置MOD数量
        /// </summary>
        private void ResetModCounts(IEnumerable<CategoryItem> categories)
        {
            foreach (var category in categories)
            {
                category.ModCount = 0;
                ResetModCounts(category.Children);
            }
        }

        /// <summary>
        /// 更新父分类MOD数量
        /// </summary>
        private void UpdateParentModCounts(IEnumerable<CategoryItem> categories)
        {
            foreach (var category in categories)
            {
                UpdateParentModCounts(category.Children);
                
                // 父分类的MOD数量包含所有子分类的MOD数量
                category.ModCount += category.Children.Sum(c => c.ModCount);
            }
        }

        /// <summary>
        /// 递归获取平铺分类列表
        /// </summary>
        private IEnumerable<CategoryItem> GetFlatCategoriesRecursive(IEnumerable<CategoryItem> categories)
        {
            foreach (var category in categories)
            {
                yield return category;
                
                foreach (var child in GetFlatCategoriesRecursive(category.Children))
                {
                    yield return child;
                }
            }
        }
    }
} 