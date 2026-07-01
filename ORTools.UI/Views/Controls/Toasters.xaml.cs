using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ORTools.UI.Views.Controls;

public partial class Toasters : UserControl
{
    private List<Prop> _props = new();
    private Random _rng = new();
    private Stopwatch _stopwatch = new();
    private TimeSpan _lastRenderTime;
    private readonly TimeSpan _frameTarget = TimeSpan.FromMilliseconds(1000.0 / 60.0); // 60 fps smooth moving

    public double MinOpacity { get; set; } = 0.10;
    public double MaxOpacity { get; set; } = 0.5;
    public int MaxProps { get; set; } = 20;
    public double SpecialFrequency { get; set; } = 0.12;

    private List<BitmapImage>? _cachedImages;
    private List<BitmapImage>? _specialImages;

    private class Prop
    {
        public Image ImageElement { get; set; } = null!;
        public TranslateTransform Transform { get; set; } = null!;
        public double X;
        public double Y;
        public double SpeedX;
        public double SpeedY;
    }

    public Toasters()
    {
        InitializeComponent();
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (_cachedImages == null)
        {
            _cachedImages = new List<BitmapImage>();
            _specialImages = new List<BitmapImage>();

            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                string resourceName = assembly.GetName().Name + ".g.resources";
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    using var reader = new System.Resources.ResourceReader(stream);
                    foreach (System.Collections.DictionaryEntry entry in reader)
                    {
                        string path = entry.Key.ToString() ?? "";
                        if ((path.StartsWith("icons/skills/") || path.StartsWith("icons/items/")) && path.EndsWith(".png"))
                        {
                            var bi = new BitmapImage(new Uri("pack://application:,,,/" + path));
                            bi.Freeze(); // Crucial for performance across threads/controls
                            _cachedImages.Add(bi);
                        }
                        else if (path == "icons/ui/aus.png")
                        {
                            var bi = new BitmapImage(new Uri("pack://application:,,,/" + path));
                            bi.Freeze();
                            _specialImages.Add(bi);
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Fallback if loading resources fails
            }
        }

        ContainerCanvas.Children.Clear();
        _props.Clear();

        if (_cachedImages.Count == 0) return; // No icons to display

        // Spawn some initial props
        for (int i = 0; i < MaxProps; i++)
        {
            var p = CreateProp();
            ResetProp(p, true);
            _props.Add(p);
        }

        _stopwatch.Restart();
        _lastRenderTime = _stopwatch.Elapsed;
        CompositionTarget.Rendering += OnRendering;
    }

    private void UserControl_Unloaded(object sender, RoutedEventArgs e)
    {
        CompositionTarget.Rendering -= OnRendering;
        _stopwatch.Stop();
    }

    private Prop CreateProp()
    {
        var img = new Image
        {
            RenderTransformOrigin = new Point(0.5, 0.5)
        };

        var transform = new TranslateTransform();
        img.RenderTransform = transform;

        RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);

        ContainerCanvas.Children.Add(img);

        return new Prop
        {
            ImageElement = img,
            Transform = transform
        };
    }

    private void ResetProp(Prop p, bool randomPosition)
    {
        if (_cachedImages == null || _cachedImages.Count == 0) return;

        bool isSpecial = _specialImages != null && _specialImages.Count > 0 && _rng.NextDouble() < SpecialFrequency;

        if (isSpecial)
        {
            p.ImageElement.Source = _specialImages![_rng.Next(_specialImages.Count)];
            p.ImageElement.Width = double.NaN;
            p.ImageElement.Height = double.NaN;
        }
        else
        {
            p.ImageElement.Source = _cachedImages[_rng.Next(_cachedImages.Count)];
            p.ImageElement.Width = 32;
            p.ImageElement.Height = 32;
        }

        p.ImageElement.Opacity = MinOpacity + _rng.NextDouble() * (MaxOpacity - MinOpacity);

        double startX, startY;

        if (randomPosition)
        {
            startX = _rng.NextDouble() * (ActualWidth > 0 ? ActualWidth : 800);
            startY = _rng.NextDouble() * (ActualHeight > 0 ? ActualHeight : 600);
        }
        else
        {
            // Spawn off-screen right OR off-screen top to fill the whole screen
            if (_rng.NextDouble() < 0.5)
            {
                // Spawn on the right edge
                startX = (ActualWidth > 0 ? ActualWidth : 800) + 50;
                startY = -50 + _rng.NextDouble() * ((ActualHeight > 0 ? ActualHeight : 600) + 100);
            }
            else
            {
                // Spawn on the top edge
                startX = -50 + _rng.NextDouble() * ((ActualWidth > 0 ? ActualWidth : 800) + 100);
                startY = -50;
            }
        }

        p.X = startX;
        p.Y = startY;
        p.Transform.X = startX;
        p.Transform.Y = startY;

        // Move left and slightly down
        p.SpeedX = -(15.0 + _rng.NextDouble() * 30.0);
        p.SpeedY = 5.0 + _rng.NextDouble() * 15.0;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var now = _stopwatch.Elapsed;
        var dt = now - _lastRenderTime;

        if (dt < _frameTarget) return;

        double deltaSeconds = dt.TotalSeconds;
        _lastRenderTime = now;

        for (int i = 0; i < _props.Count; i++)
        {
            var p = _props[i];
            p.X += p.SpeedX * deltaSeconds;
            p.Y += p.SpeedY * deltaSeconds;

            p.Transform.X = p.X;
            p.Transform.Y = p.Y;

            // Recycle if it goes off-screen left or bottom
            if (p.X < -50 || p.Y > (ActualHeight > 0 ? ActualHeight : 600) + 50)
            {
                ResetProp(p, false);
            }
        }

        // Spawn new props to maintain density dynamically
        while (_props.Count < MaxProps)
        {
            var p = CreateProp();
            ResetProp(p, false);
            _props.Add(p);
        }

        // Remove excess props if MaxProps was lowered dynamically
        while (_props.Count > MaxProps)
        {
            var p = _props[_props.Count - 1];
            ContainerCanvas.Children.Remove(p.ImageElement);
            _props.RemoveAt(_props.Count - 1);
        }
    }
}
