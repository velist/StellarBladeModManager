using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace UEModManager
{
    public class DragAdorner : Adorner
    {
        private readonly Image _child;
        private Point _currentPosition;

        public DragAdorner(UIElement adornedElement, ImageSource feedbackImage, Size feedbackSize) : base(adornedElement)
        {
            // Adorner层默认是不可交互的，这样鼠标事件可以"穿透"到下面的控件。
            IsHitTestVisible = false;

            _child = new Image
            {
                Source = feedbackImage,
                Width = feedbackSize.Width,
                Height = feedbackSize.Height,
                Opacity = 0.7
            };
        }

        /// <summary>
        /// 更新Adorner的位置。
        /// </summary>
        public void UpdatePosition(Point newPosition)
        {
            _currentPosition = newPosition;
            // 通知Adorner层重新渲染。
            var adornerLayer = Parent as AdornerLayer;
            adornerLayer?.Update(AdornedElement);
        }

        protected override int VisualChildrenCount => 1;

        protected override Visual GetVisualChild(int index)
        {
            return _child;
        }

        protected override Size MeasureOverride(Size constraint)
        {
            _child.Measure(constraint);
            return _child.DesiredSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            _child.Arrange(new Rect(finalSize));
            return finalSize;
        }

        public override GeneralTransform GetDesiredTransform(GeneralTransform transform)
        {
            var result = new GeneralTransformGroup();
            result.Children.Add(base.GetDesiredTransform(transform));
            // 使用TranslateTransform来移动Adorner到鼠标当前位置。
            result.Children.Add(new TranslateTransform(_currentPosition.X, _currentPosition.Y));
            return result;
        }
    }
} 