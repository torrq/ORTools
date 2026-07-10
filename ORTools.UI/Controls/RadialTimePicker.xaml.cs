using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ORTools.UI.Controls
{
    /// <summary>
    /// Compact, dependency-free radial time picker.
    /// Full circle == Maximum. Drag the ring, use the quick buttons elsewhere in the
    /// view, or click the center readout to type an exact minute value.
    /// </summary>
    public partial class RadialTimePicker : UserControl
    {
        private const double CenterX = 115;
        private const double CenterY = 115;
        private const double Radius = 95;

        private bool _isDragging;

        public RadialTimePicker()
        {
            InitializeComponent();
        }

        #region Dependency Properties

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(int), typeof(RadialTimePicker),
                new FrameworkPropertyMetadata(60, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

        public int Value
        {
            get => (int)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register(nameof(Minimum), typeof(int), typeof(RadialTimePicker),
                new PropertyMetadata(1, OnRangeChanged));

        public int Minimum
        {
            get => (int)GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }

        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register(nameof(Maximum), typeof(int), typeof(RadialTimePicker),
                new PropertyMetadata(540, OnRangeChanged));

        public int Maximum
        {
            get => (int)GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        /// <summary>Drag snaps to the nearest multiple of this many minutes. Click-to-edit ignores it.</summary>
        public static readonly DependencyProperty SnapMinutesProperty =
            DependencyProperty.Register(nameof(SnapMinutes), typeof(int), typeof(RadialTimePicker),
                new PropertyMetadata(5));

        public int SnapMinutes
        {
            get => (int)GetValue(SnapMinutesProperty);
            set => SetValue(SnapMinutesProperty, value);
        }

        /// <summary>Optional: tint the ring differently while the timer is actively running.</summary>
        public static readonly DependencyProperty IsRunningProperty =
            DependencyProperty.Register(nameof(IsRunning), typeof(bool), typeof(RadialTimePicker),
                new PropertyMetadata(false, OnIsRunningChanged));

        public bool IsRunning
        {
            get => (bool)GetValue(IsRunningProperty);
            set => SetValue(IsRunningProperty, value);
        }

        /// <summary>Combined countdown text (e.g. "2h 15m 42s"), shown under the main time while running.</summary>
        public static readonly DependencyProperty RemainingTextProperty =
            DependencyProperty.Register(nameof(RemainingText), typeof(string), typeof(RadialTimePicker),
                new PropertyMetadata(string.Empty));

        public string RemainingText
        {
            get => (string)GetValue(RemainingTextProperty);
            set => SetValue(RemainingTextProperty, value);
        }

        #endregion

        #region Property Changed Callbacks

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((RadialTimePicker)d).UpdateVisual();

        private static void OnRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (RadialTimePicker)d;
            ctrl.BuildTicks();
            ctrl.UpdateVisual();
        }

        private static void OnIsRunningChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (RadialTimePicker)d;
            if (ctrl.ProgressArc == null) return;

            // SetResourceReference (not a one-time TryFindResource lookup) so the stroke
            // keeps tracking the active theme dictionary even if ThemeService swaps it later.
            string resourceKey = (bool)e.NewValue ? "AppDangerBrush" : "AppPrimaryBrush";
            ctrl.ProgressArc.SetResourceReference(Shape.StrokeProperty, resourceKey);
        }

        #endregion

        private void Root_Loaded(object sender, RoutedEventArgs e)
        {
            BuildTicks();
            UpdateVisual();
        }

        #region Drawing

        private static Point PointOnCircle(double angleDegrees, double radius)
        {
            double rad = angleDegrees * Math.PI / 180.0;
            return new Point(CenterX + radius * Math.Sin(rad), CenterY - radius * Math.Cos(rad));
        }

        private void UpdateVisual()
        {
            if (ProgressArc == null || CenterText == null || ThumbTransform == null) return;

            double range = Math.Max(1, Maximum - Minimum);
            double fraction = Math.Max(0, Math.Min(1, (double)(Value - Minimum) / range));
            double angleDeg = fraction * 360.0;

            if (angleDeg <= 0.15)
            {
                ProgressArc.Data = null;
            }
            else
            {
                Point startPoint = PointOnCircle(0.0001, Radius);
                Point endPoint = PointOnCircle(Math.Max(angleDeg, 0.2), Radius);
                bool isLargeArc = angleDeg > 180;

                var figure = new PathFigure { StartPoint = startPoint, IsClosed = false };
                figure.Segments.Add(new ArcSegment(endPoint, new Size(Radius, Radius), 0,
                    isLargeArc, SweepDirection.Clockwise, true));

                var geometry = new PathGeometry();
                geometry.Figures.Add(figure);
                ProgressArc.Data = geometry;
            }

            Point thumbPoint = PointOnCircle(angleDeg, Radius);
            ThumbTransform.X = thumbPoint.X - CenterX;
            ThumbTransform.Y = thumbPoint.Y - CenterY;

            CenterText.Text = FormatMinutes(Value);
        }

        private void BuildTicks()
        {
            if (TickCanvas == null) return;

            TickCanvas.Children.Clear();
            if (Maximum <= 0) return;

            int hourCount = Maximum / 60;

            for (int h = 0; h <= hourCount; h++)
            {
                double minutesAtTick = h * 60;
                if (minutesAtTick > Maximum) break;

                double angleDeg = 360.0 * minutesAtTick / Maximum;
                double outer = 103;
                double inner = 91;

                Point p1 = PointOnCircle(angleDeg, outer);
                Point p2 = PointOnCircle(angleDeg, inner);

                var line = new Line
                {
                    X1 = p1.X,
                    Y1 = p1.Y,
                    X2 = p2.X,
                    Y2 = p2.Y,
                    StrokeThickness = 3,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round
                };
                // SetResourceReference instead of a frozen SolidColorBrush so ticks follow
                // theme switches (light/dark, color family) made after the control loads.
                line.SetResourceReference(Shape.StrokeProperty, "AppSubtleBrush");
                TickCanvas.Children.Add(line);
            }
        }

        private static string FormatMinutes(int totalMinutes)
        {
            int hours = totalMinutes / 60;
            int minutes = totalMinutes % 60;
            return hours > 0 ? $"{hours}h {minutes}m" : $"{minutes}m";
        }

        #endregion

        #region Drag Interaction

        private void Dial_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDragging = true;
            Mouse.Capture((UIElement)sender);
            UpdateValueFromMouse(e.GetPosition((UIElement)sender));
        }

        private void Dial_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging && e.LeftButton == MouseButtonState.Pressed)
                UpdateValueFromMouse(e.GetPosition((UIElement)sender));
        }

        private void Dial_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDragging) return;
            _isDragging = false;
            Mouse.Capture(null);
        }

        private void UpdateValueFromMouse(Point p)
        {
            double dx = p.X - CenterX;
            double dy = p.Y - CenterY;

            // Angle measured clockwise from 12 o'clock.
            double angle = Math.Atan2(dx, -dy) * 180.0 / Math.PI;
            if (angle < 0) angle += 360;

            double range = Maximum - Minimum;
            double raw = Minimum + angle / 360.0 * range;

            int snap = Math.Max(1, SnapMinutes);
            int snapped = (int)(Math.Round(raw / snap) * snap);
            snapped = Math.Max(Minimum, Math.Min(Maximum, snapped));

            // SetCurrentValue keeps the existing binding (e.g. Delay=150) intact,
            // rather than overwriting it the way SetValue would.
            SetCurrentValue(ValueProperty, snapped);
        }

        #endregion

        #region Click-to-Edit

        private void CenterText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Stop this from bubbling up and starting a drag on the dial.
            e.Handled = true;

            CenterEditBox.Text = Value.ToString();
            CenterText.Visibility = Visibility.Collapsed;
            CenterEditBox.Visibility = Visibility.Visible;
            CenterEditBox.Focus();
            CenterEditBox.SelectAll();
        }

        private void CommitEdit()
        {
            if (int.TryParse(CenterEditBox.Text, out int minutes))
            {
                int clamped = Math.Max(Minimum, Math.Min(Maximum, minutes));
                SetCurrentValue(ValueProperty, clamped);
            }

            CenterEditBox.Visibility = Visibility.Collapsed;
            CenterText.Visibility = Visibility.Visible;
        }

        private void CancelEdit()
        {
            CenterEditBox.Visibility = Visibility.Collapsed;
            CenterText.Visibility = Visibility.Visible;
        }

        private void CenterEditBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) CommitEdit();
            else if (e.Key == Key.Escape) CancelEdit();
        }

        private void CenterEditBox_LostFocus(object sender, RoutedEventArgs e) => CommitEdit();

        #endregion
    }
}
