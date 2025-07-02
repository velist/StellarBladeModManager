# 测试验证弹窗样式统一
# 验证导入成功、禁用/启用的弹窗样式是否已统一为深色主题

# 测试内容：
# 1. 所有MessageBox.Show替换为ShowCustomMessageBox
# 2. 包括以下弹窗：
#    - 导入成功弹窗
#    - 启用/禁用MOD弹窗
#    - 批量启用/禁用MOD弹窗
#    - 游戏配置成功弹窗
#    - 拖拽导入相关弹窗
#    - 重命名MOD相关弹窗
#    - 修改预览图相关弹窗
#    - 游戏启动相关弹窗
# 3. 添加了GamePathConfirmViewModel.cs和GamePathDialog.xaml.cs中的自定义弹窗方法

# 切换到项目目录
cd $PSScriptRoot

# 编译并运行项目
dotnet run --project UEModManager 