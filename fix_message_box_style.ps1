# 统一弹窗样式，替换MessageBox.Show为ShowCustomMessageBox
# 创建于: 2025-07-07

$ErrorActionPreference = "Stop"
Write-Host "开始统一弹窗样式..." -ForegroundColor Cyan

# 设置工作目录
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptPath
Write-Host "当前工作目录: $scriptPath" -ForegroundColor Gray

# 要处理的文件列表
$filesToProcess = @(
    "UEModManager\MainWindow.xaml.cs",
    "UEModManager\Views\GamePathDialog.xaml.cs",
    "UEModManager\Views\GamePathConfirmDialog.xaml.cs",
    "UEModManager\Views\WallpaperSettingsDialog.xaml.cs"
)

# 替换计数器
$totalReplaced = 0

# 处理每个文件
foreach ($file in $filesToProcess) {
    $filePath = Join-Path $scriptPath $file
    
    if (Test-Path $filePath) {
        Write-Host "处理文件: $file" -ForegroundColor Yellow
        
        # 读取文件内容
        $content = Get-Content -Path $filePath -Raw
        
        # 备份文件
        $backupPath = "$filePath.bak"
        Copy-Item -Path $filePath -Destination $backupPath -Force
        Write-Host "已创建备份: $backupPath" -ForegroundColor Gray
        
        # 使用正则表达式替换MessageBox.Show调用
        $pattern = 'MessageBox\.Show\((.*?)\)'
        $replacement = 'ShowCustomMessageBox($1)'
        
        # 执行替换
        $newContent = $content -replace $pattern, $replacement
        
        # 计算替换次数
        $replacedCount = ([regex]::Matches($content, $pattern)).Count
        $totalReplaced += $replacedCount
        
        # 保存修改后的内容
        Set-Content -Path $filePath -Value $newContent
        
        Write-Host "替换完成，共替换了 $replacedCount 处MessageBox.Show调用" -ForegroundColor Green
    }
    else {
        Write-Host "文件不存在: $filePath" -ForegroundColor Red
    }
}

Write-Host "所有文件处理完成，总共替换了 $totalReplaced 处MessageBox.Show调用" -ForegroundColor Cyan

# 检查是否需要添加ShowCustomMessageBox方法
foreach ($file in $filesToProcess) {
    $filePath = Join-Path $scriptPath $file
    
    if (Test-Path $filePath) {
        $content = Get-Content -Path $filePath -Raw
        
        # 检查文件中是否已经有ShowCustomMessageBox方法
        if ($content -notmatch 'private\s+MessageBoxResult\s+ShowCustomMessageBox') {
            Write-Host "文件 $file 中没有找到ShowCustomMessageBox方法，需要添加" -ForegroundColor Yellow
            
            # 添加ShowCustomMessageBox方法的代码
            $methodCode = @'

        /// <summary>
        /// 自定义深色主题MessageBox
        /// </summary>
        private MessageBoxResult ShowCustomMessageBox(string message, string title, MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None)
        {
            double width = 400;
            double height = 200;

            // 对于短消息，减小高度
            if (icon == MessageBoxImage.Information && message.Length < 50)
            {
                height = 180;
            }

            var messageWindow = new Window
            {
                Title = title,
                Width = width,
                Height = height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F1A2E")), // 稍微更亮的背景色
                WindowStyle = WindowStyle.None,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A3441")), // 更明显的边框
                BorderThickness = new Thickness(1)
            };

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 标题栏
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 内容
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 按钮

            // 自定义标题栏
            var titleBar = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A3441")), // 更明显的标题栏颜色
                Padding = new Thickness(15),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3A4451")), // 更明显的边框
                BorderThickness = new Thickness(0, 0, 0, 1)
            };

            var titleGrid = new Grid();
            var titleText = new TextBlock
            {
                Text = title,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF")), // 纯白色文本提高对比度
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center
            };

            var closeButton = new Button
            {
                Content = "✕",
                Width = 30,
                Height = 30,
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9CA3AF")),
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Right,
                FontSize = 14,
                Cursor = Cursors.Hand
            };

            titleGrid.Children.Add(titleText);
            titleGrid.Children.Add(closeButton);
            titleBar.Child = titleGrid;
            Grid.SetRow(titleBar, 0);

            // 内容区域
            var contentGrid = new Grid();
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // 图标
            string iconText = icon switch
            {
                MessageBoxImage.Information => "ℹ️",
                MessageBoxImage.Warning => "⚠️",
                MessageBoxImage.Error => "❌",
                MessageBoxImage.Question => "❓",
                _ => "💬"
            };

            var iconBlock = new TextBlock
            {
                Text = iconText,
                FontSize = 32,
                Margin = new Thickness(20, 20, 15, 20),
                VerticalAlignment = VerticalAlignment.Top
            };
            Grid.SetColumn(iconBlock, 0);

            // 消息文本
            var messageText = new TextBlock
            {
                Text = message,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF")), // 纯白色文本提高对比度
                FontSize = 14,
                Margin = new Thickness(0, 20, 20, 20),
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(messageText, 1);

            contentGrid.Children.Add(iconBlock);
            contentGrid.Children.Add(messageText);
            Grid.SetRow(contentGrid, 1);

            // 按钮区域
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(20, 0, 20, 20),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F1B2E"))
            };

            MessageBoxResult result = MessageBoxResult.None;

            // 根据按钮类型创建按钮
            switch (buttons)
            {
                case MessageBoxButton.OK:
                    var okBtn = CreateMessageBoxButton("确定", true);
                    okBtn.Click += (s, e) => { result = MessageBoxResult.OK; messageWindow.Close(); };
                    buttonPanel.Children.Add(okBtn);
                    break;

                case MessageBoxButton.OKCancel:
                    var cancelBtn1 = CreateMessageBoxButton("取消", false);
                    var okBtn1 = CreateMessageBoxButton("确定", true);
                    cancelBtn1.Click += (s, e) => { result = MessageBoxResult.Cancel; messageWindow.Close(); };
                    okBtn1.Click += (s, e) => { result = MessageBoxResult.OK; messageWindow.Close(); };
                    buttonPanel.Children.Add(cancelBtn1);
                    buttonPanel.Children.Add(okBtn1);
                    break;

                case MessageBoxButton.YesNo:
                    var noBtn = CreateMessageBoxButton("否", false);
                    var yesBtn = CreateMessageBoxButton("是", true);
                    noBtn.Click += (s, e) => { result = MessageBoxResult.No; messageWindow.Close(); };
                    yesBtn.Click += (s, e) => { result = MessageBoxResult.Yes; messageWindow.Close(); };
                    buttonPanel.Children.Add(noBtn);
                    buttonPanel.Children.Add(yesBtn);
                    break;

                case MessageBoxButton.YesNoCancel:
                    var cancelBtn2 = CreateMessageBoxButton("取消", false);
                    var noBtn2 = CreateMessageBoxButton("否", false);
                    var yesBtn2 = CreateMessageBoxButton("是", true);
                    cancelBtn2.Click += (s, e) => { result = MessageBoxResult.Cancel; messageWindow.Close(); };
                    noBtn2.Click += (s, e) => { result = MessageBoxResult.No; messageWindow.Close(); };
                    yesBtn2.Click += (s, e) => { result = MessageBoxResult.Yes; messageWindow.Close(); };
                    buttonPanel.Children.Add(cancelBtn2);
                    buttonPanel.Children.Add(noBtn2);
                    buttonPanel.Children.Add(yesBtn2);
                    break;
            }

            Grid.SetRow(buttonPanel, 2);

            // 关闭按钮事件
            closeButton.Click += (s, e) => { result = MessageBoxResult.Cancel; messageWindow.Close(); };

            // 添加键盘支持
            messageWindow.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    result = MessageBoxResult.Cancel;
                    messageWindow.Close();
                }
                else if (e.Key == Key.Enter && buttons == MessageBoxButton.OK)
                {
                    result = MessageBoxResult.OK;
                    messageWindow.Close();
                }
            };

            mainGrid.Children.Add(titleBar);
            mainGrid.Children.Add(contentGrid);
            mainGrid.Children.Add(buttonPanel);

            messageWindow.Content = mainGrid;
            messageWindow.ShowDialog();

            return result;
        }

        /// <summary>
        /// 创建MessageBox按钮
        /// </summary>
        private Button CreateMessageBoxButton(string text, bool isPrimary)
        {
            var button = new Button
            {
                Content = text,
                Width = 80,
                Height = 32,
                Margin = new Thickness(10, 0, 0, 0),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                FontSize = 13
            };

            if (isPrimary)
            {
                // 使用更高对比度的颜色
                button.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));
                button.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#000000"));
                button.FontWeight = FontWeights.Bold;
            }
            else
            {
                // 使用更高对比度的颜色
                button.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A5568"));
                button.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
            }

            // 添加鼠标悬停效果
            button.MouseEnter += (s, e) =>
            {
                if (isPrimary)
                {
                    button.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FBBF24"));
                }
                else
                {
                    button.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280"));
                }
            };

            button.MouseLeave += (s, e) =>
            {
                if (isPrimary)
                {
                    button.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));
                }
                else
                {
                    button.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A5568"));
                }
            };

            return button;
        }
'@
            
            # 查找适合插入方法的位置（在类的末尾，最后一个方法之后）
            $lastMethodMatch = [regex]::Match($content, 'private\s+void\s+\w+\([^\)]*\)[^{]*{[^}]*}(?!\s*private|\s*public|\s*protected)')
            if ($lastMethodMatch.Success) {
                $insertPosition = $lastMethodMatch.Index + $lastMethodMatch.Length
                $newContent = $content.Insert($insertPosition, $methodCode)
                
                # 保存修改后的内容
                Set-Content -Path $filePath -Value $newContent
                Write-Host "已添加ShowCustomMessageBox方法到文件: $file" -ForegroundColor Green
            }
            else {
                Write-Host "无法找到适合插入方法的位置，请手动添加ShowCustomMessageBox方法到文件: $file" -ForegroundColor Red
            }
        }
        else {
            Write-Host "文件 $file 已包含ShowCustomMessageBox方法，无需添加" -ForegroundColor Green
        }
    }
}

Write-Host "统一弹窗样式完成！" -ForegroundColor Cyan 