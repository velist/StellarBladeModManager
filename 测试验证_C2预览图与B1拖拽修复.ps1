# C2区预览图与B1区分类拖拽功能修复测试验证脚本
Write-Host "=== UEModManager C2预览图与B1拖拽修复测试 ===" -ForegroundColor Green
Write-Host "测试时间: $(Get-Date)" -ForegroundColor Yellow

Write-Host "`n🔍 修复内容说明:" -ForegroundColor Cyan
Write-Host "✅ 修复C2区预览图上传后提示文字浮在图片上方的问题" -ForegroundColor White
Write-Host "✅ 为PreviewPlaceholder添加Visibility控制逻辑" -ForegroundColor White
Write-Host "✅ 优化B1区分类拖拽手柄，改为六个点图标(⋮⋮)" -ForegroundColor White
Write-Host "✅ 实现完整的WPF拖拽机制，支持分类重排序" -ForegroundColor White
Write-Host "✅ 禁止'全部'、'已启用'、'已禁用'分类的拖拽功能" -ForegroundColor White
Write-Host "✅ 添加拖拽手柄鼠标悬停效果，显示四个方向箭头" -ForegroundColor White
Write-Host "✅ 修复类型转换问题，支持Category和CategoryItem类型" -ForegroundColor White

Write-Host "`n🔍 测试项目清单:" -ForegroundColor Cyan
Write-Host "【C2区预览图测试】✅" -ForegroundColor Yellow
Write-Host "1. 选择一个无预览图的MOD，观察C2区是否显示提示文字" -ForegroundColor White
Write-Host "2. 点击C2区上传预览图，选择一张图片" -ForegroundColor White
Write-Host "3. 图片显示后，提示文字应该完全消失，不再浮在图片上方" -ForegroundColor White
Write-Host "4. 再次选择无预览图的MOD，提示文字应该重新显示" -ForegroundColor White

Write-Host "`n【B1区分类拖拽测试】🔧" -ForegroundColor Yellow
Write-Host "5. 观察B1区分类列表，自定义分类右侧应显示⋮⋮拖拽手柄图标" -ForegroundColor White
Write-Host "6. 默认分类('全部'、'已启用'、'已禁用')不应显示拖拽手柄" -ForegroundColor White
Write-Host "7. 鼠标悬停在拖拽手柄上时:" -ForegroundColor White
Write-Host "   - 光标应变为四个方向的箭头(SizeAll)" -ForegroundColor Gray
Write-Host "   - 拖拽手柄图标颜色应变为绿色(#00D4AA)" -ForegroundColor Gray
Write-Host "8. 拖拽操作测试:" -ForegroundColor White
Write-Host "   - 按住拖拽手柄并拖动，应该能重新排序分类位置" -ForegroundColor Gray
Write-Host "   - 拖拽时应启动WPF拖拽操作，显示拖拽效果" -ForegroundColor Gray
Write-Host "   - 释放鼠标后分类应保持在新位置" -ForegroundColor Gray
Write-Host "   - 默认分类仍然保持在顶部，不受拖拽影响" -ForegroundColor Gray

Write-Host "`n📋 详细拖拽操作步骤:" -ForegroundColor Cyan
Write-Host "步骤1: 找到任意一个自定义分类(如'服装'、'面部'、'其他'等)" -ForegroundColor White
Write-Host "步骤2: 将鼠标悬停在该分类右侧的⋮⋮图标上" -ForegroundColor White
Write-Host "步骤3: 确认光标变为四个方向箭头，图标变为绿色" -ForegroundColor White
Write-Host "步骤4: 按住鼠标左键，开始拖拽操作" -ForegroundColor White
Write-Host "步骤5: 拖拽到另一个分类位置并释放鼠标" -ForegroundColor White
Write-Host "步骤6: 观察分类顺序是否发生变化" -ForegroundColor White
Write-Host "步骤7: 重复测试其他自定义分类的拖拽功能" -ForegroundColor White

Write-Host "`n⚠️  特别注意:" -ForegroundColor Red
Write-Host "- 拖拽手柄图标大小已优化为12号字体，应该更容易看到" -ForegroundColor Yellow
Write-Host "- 拖拽手柄具有最小宽度20px，确保足够的点击区域" -ForegroundColor Yellow
Write-Host "- 如果拖拽手柄仍然不可见，请检查分类数据类型是否正确" -ForegroundColor Yellow
Write-Host "- 控制台会输出拖拽操作的调试信息，可以帮助诊断问题" -ForegroundColor Yellow

Write-Host "`n🚀 启动程序进行测试..." -ForegroundColor Green
Start-Process -FilePath ".\UEModManager\bin\Release\net8.0-windows\UEModManager.exe"

Write-Host "`n✨ 测试完成后请反馈:" -ForegroundColor Magenta
Write-Host "1. C2区预览图提示文字是否正确显示和隐藏" -ForegroundColor White
Write-Host "2. B1区拖拽手柄是否正确显示" -ForegroundColor White
Write-Host "3. 拖拽功能是否正常工作" -ForegroundColor White
Write-Host "4. 是否有任何错误或异常情况" -ForegroundColor White 