using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Media.Imaging;
namespace ORTools.UI.Controls
{
    /// <summary>
    /// Compact, dependency-free radial time picker.
    ///
    /// Two independent things share this ring:
    ///  - Value: the editable target for the NEXT run. Always draggable, regardless of
    ///    whether a timer is currently running. Its angle is scaled against Minimum/Maximum.
    ///  - The live countdown (RunningRemainingSeconds / RunningTotalMinutes): shown on the
    ///    same thick ring while IsRunning, scaled against the ACTIVE run's own duration
    ///    (kitchen-timer convention: the ring always drains from a full circle to empty
    ///    over ITS OWN duration, not the global Minimum/Maximum range).
    ///
    /// Dragging never stops or alters the active countdown — it only ever changes Value,
    /// which is entirely separate from RunningRemainingSeconds/RunningTotalMinutes.
    /// </summary>
    public partial class RadialTimePicker : UserControl
    {
        private const double CenterX = 115;
        private const double CenterY = 115;
        private const double Radius = 95;
        private const double SecondHandRadius = 111;

        // Ring occupies roughly radius 88.5-101.5 (Radius ± half the 13px stroke). Clicks
        // starting closer to center than this (over the gif/text/etc.) never begin a drag —
        // only the ring itself and its outer padding are interactive. A small buffer keeps
        // grabs right at the ring's inner edge feeling natural rather than needing pixel precision.
        private const double InnerDeadZoneRadius = 82;

        private bool _isDragging;

        private static readonly BitmapImage[] _clockFrames = new BitmapImage[8];

        static RadialTimePicker()
        {
            for (int i = 0; i < 8; i++)
            {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.UriSource = new Uri($"pack://application:,,,/Assets/Icons/UI/Clock/frame_{i}.png");
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.EndInit();
                bi.Freeze();
                _clockFrames[i] = bi;
            }
        }

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

        /// <summary>Whether a timer is actively counting down right now.</summary>
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

        /// <summary>Seconds left in the ACTIVE run. Independent of Value; only meaningful while IsRunning.</summary>
        public static readonly DependencyProperty RunningRemainingSecondsProperty =
            DependencyProperty.Register(nameof(RunningRemainingSeconds), typeof(int), typeof(RadialTimePicker),
                new PropertyMetadata(0, OnLiveCountdownChanged));

        public int RunningRemainingSeconds
        {
            get => (int)GetValue(RunningRemainingSecondsProperty);
            set => SetValue(RunningRemainingSecondsProperty, value);
        }

        /// <summary>
        /// Total duration (minutes) of the ACTIVE run, snapshotted when it started.
        /// Independent of Value — editing Value while running never changes this.
        /// </summary>
        public static readonly DependencyProperty RunningTotalMinutesProperty =
            DependencyProperty.Register(nameof(RunningTotalMinutes), typeof(int), typeof(RadialTimePicker),
                new PropertyMetadata(0, OnLiveCountdownChanged));

        public int RunningTotalMinutes
        {
            get => (int)GetValue(RunningTotalMinutesProperty);
            set => SetValue(RunningTotalMinutesProperty, value);
        }

        /// <summary>Whether the active countdown is currently paused. Freezes the clock gif's animation when true.</summary>
        public static readonly DependencyProperty IsPausedProperty =
            DependencyProperty.Register(nameof(IsPaused), typeof(bool), typeof(RadialTimePicker),
                new PropertyMetadata(false, OnIsPausedChanged));

        public bool IsPaused
        {
            get => (bool)GetValue(IsPausedProperty);
            set => SetValue(IsPausedProperty, value);
        }

        #endregion

        #region Property Changed Callbacks

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((RadialTimePicker)d).UpdateVisual();

        private static void OnLiveCountdownChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((RadialTimePicker)d).UpdateVisual();

        private static void OnIsPausedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // Pause state just prevents running seconds from updating in the backend,
            // so UpdateVisual isn't called, freezing the clock automatically.
        }

        private static void OnRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (RadialTimePicker)d;
            ctrl.BuildTicks();
            ctrl.UpdateVisual();
        }

        private static void OnIsRunningChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (RadialTimePicker)d;
            // No color swap here anymore — the ring's Stroke stays on AppPrimaryBrush (set via
            // DynamicResource in XAML) whether running or not, so it keeps the current theme's
            // own accent color instead of jumping to a hardcoded "danger" red.
            ctrl.UpdateVisual();
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

        private static void DrawArc(Path path, double angleDeg, double radius)
        {
            if (angleDeg <= 0.15)
            {
                path.Data = null;
                return;
            }

            Point startPoint = PointOnCircle(0.0001, radius);
            Point endPoint = PointOnCircle(Math.Max(angleDeg, 0.2), radius);
            bool isLargeArc = angleDeg > 180;

            var figure = new PathFigure { StartPoint = startPoint, IsClosed = false };
            figure.Segments.Add(new ArcSegment(endPoint, new Size(radius, radius), 0,
                isLargeArc, SweepDirection.Clockwise, true));

            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            path.Data = geometry;
        }

        private void UpdateVisual()
        {
            if (ProgressArc == null || CenterText == null || ThumbTransform == null
                || NextArc == null || NextLabel == null || SecondHandDot == null || SecondHandTransform == null)
                return;

            double range = Math.Max(1, Maximum - Minimum);
            double valueFraction = Math.Max(0, Math.Min(1, (double)(Value - Minimum) / range));
            double valueAngleDeg = valueFraction * 360.0;

            bool showLiveCountdown = IsRunning && RunningTotalMinutes > 0;

            // Primary thick ring: while running, this is the ACTIVE countdown (its own 0-100%
            // scale, kitchen-timer style — full circle = the full duration of THIS run).
            // While idle, it's simply the editable target, same as before.
            double liveAngleDeg = valueAngleDeg;
            if (showLiveCountdown)
            {
                double totalSeconds = RunningTotalMinutes * 60.0;
                double liveFraction = totalSeconds > 0
                    ? Math.Max(0, Math.Min(1, RunningRemainingSeconds / totalSeconds))
                    : 0;
                liveAngleDeg = liveFraction * 360.0;
            }
            DrawArc(ProgressArc, liveAngleDeg, Radius);

            // Thin dashed "next" preview: only while running, and only when the pending edit
            // actually differs from what's live — otherwise it's just visual noise sitting on
            // top of a ring that already agrees with it.
            bool showNextPreview = showLiveCountdown && Math.Abs(Value - RunningTotalMinutes) >= 1;
            if (showNextPreview)
            {
                DrawArc(NextArc, valueAngleDeg, Radius);
                NextArc.Visibility = Visibility.Visible;
                string nextStr = ORTools.UI.Services.LanguageService.Get("S.AutoOff.Next");
                NextLabel.Text = $"{nextStr}: {FormatMinutes(Value)}";
                NextLabel.Visibility = Visibility.Visible;
            }
            else
            {
                NextArc.Visibility = Visibility.Collapsed;
                NextLabel.Visibility = Visibility.Collapsed;
            }

            // The draggable thumb always tracks Value — regardless of running state, dragging
            // only ever moves this, never the live countdown arc above.
            Point thumbPoint = PointOnCircle(valueAngleDeg, Radius);
            ThumbTransform.X = thumbPoint.X - CenterX;
            ThumbTransform.Y = thumbPoint.Y - CenterY;

            CenterText.Text = FormatMinutes(Value);

            // Second hand: orbits once per minute of wall-clock time within the active
            // countdown, like a physical clock's second hand ticking forward.
            if (showLiveCountdown)
            {
                int secondsIntoMinute = (60 - (RunningRemainingSeconds % 60)) % 60;
                double secondAngle = secondsIntoMinute * 6.0;
                Point secPoint = PointOnCircle(secondAngle, SecondHandRadius);
                SecondHandTransform.X = secPoint.X - CenterX;
                SecondHandTransform.Y = secPoint.Y - CenterY;
                SecondHandDot.Visibility = Visibility.Visible;
                
                ClockImage.Source = _clockFrames[secondsIntoMinute % 8];
            }
            else
            {
                SecondHandDot.Visibility = Visibility.Collapsed;
            }
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
            Point p = e.GetPosition((UIElement)sender);
            if (!IsInInteractiveZone(p))
                return; // click landed inside the empty center — ignore it entirely

            _isDragging = true;
            Mouse.Capture((UIElement)sender);
            UpdateValueFromMouse(p);
        }

        private void Dial_MouseMove(object sender, MouseEventArgs e)
        {
            // No zone check here on purpose: once a drag has actually started, the pointer
            // is free to move anywhere (including briefly over the center) without interrupting it.
            if (_isDragging && e.LeftButton == MouseButtonState.Pressed)
                UpdateValueFromMouse(e.GetPosition((UIElement)sender));
        }

        private static bool IsInInteractiveZone(Point p)
        {
            double dx = p.X - CenterX;
            double dy = p.Y - CenterY;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            return distance >= InnerDeadZoneRadius;
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

            // SetCurrentValue keeps the existing binding (e.g. Delay=150) intact, rather than
            // overwriting it the way SetValue would. This ONLY ever edits Value — the next
            // run's target — and never touches the active countdown or stops anything running.
            SetCurrentValue(ValueProperty, snapped);
        }

        #endregion

        #region Click-to-Edit

        private void CenterText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Stop this from bubbling up and starting a drag on the dial.
            e.Handled = true;

            CenterEditBox.Text = Value.ToString();
            // Hidden (not Collapsed): keeps this slot's layout size constant so the gif above
            // and countdown line below never shift/squish when the edit box swaps in.
            CenterText.Visibility = Visibility.Hidden;
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

            CenterEditBox.Visibility = Visibility.Hidden;
            CenterText.Visibility = Visibility.Visible;
        }

        private void CancelEdit()
        {
            CenterEditBox.Visibility = Visibility.Hidden;
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
