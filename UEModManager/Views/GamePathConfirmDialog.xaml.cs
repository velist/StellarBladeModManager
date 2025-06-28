using System.Windows;
using UEModManager.ViewModels;

namespace UEModManager.Views
{
    /// <summary>
    /// GamePathConfirmDialog.xaml 的交互逻辑
    /// </summary>
    public partial class GamePathConfirmDialog : Window
    {
        public GamePathConfirmDialog(GamePathConfirmViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            
            // 绑定关闭事件
            viewModel.CloseRequested += (sender, result) =>
            {
                DialogResult = result;
                Close();
            };
        }
    }
} 