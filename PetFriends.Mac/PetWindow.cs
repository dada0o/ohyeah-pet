using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace PetFriends.Mac;

internal sealed class PetWindow : Window
{
    public const double BubbleHeight = 56;

    private readonly Image _petImage;
    private readonly Border _bubble;
    private readonly TextBlock _speech;
    private readonly Canvas _effects;
    private readonly ScaleTransform _scale = new(1, 1);
    private readonly RotateTransform _rotate = new(0);
    private readonly TranslateTransform _translate = new(0, 0);
    private PixelPoint _pointerDownScreen;
    private PixelPoint _windowDown;
    private DateTime _pointerDownAt;
    private int _speechVersion;
    private int _hopVersion;
    private int _rotateVersion;
    private double _direction = 1;
    private double _restingAngle;

    public string PetName { get; }
    public bool IsDragging { get; private set; }
    public bool LastActionWasDrag { get; private set; }
    public bool IsBusy { get; set; }
    public bool IgnoreActivityBounds { get; set; }
    public int ActivityVersion { get; set; }
    public double MotionX { get; set; }
    public double MotionY { get; set; }
    public DateTime MotionUntil { get; set; }
    public Action<PetWindow>? Petted { get; set; }
    public Action<PetWindow>? DragFinished { get; set; }
    public Func<PetWindow, ContextMenu>? MenuFactory { get; set; }

    public PetWindow(string petName, string assetName, double size)
    {
        PetName = petName;
        Title = petName;
        Width = size;
        Height = size + BubbleHeight;
        WindowDecorations = Avalonia.Controls.WindowDecorations.None;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Background = Brushes.Transparent;
        Topmost = true;
        ShowActivated = false;
        ShowInTaskbar = false;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.Manual;

        var root = new Grid
        {
            Background = Brushes.Transparent,
            RowDefinitions = new RowDefinitions($"{BubbleHeight},*")
        };

        _speech = new TextBlock
        {
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(82, 68, 76)),
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = size - 40
        };
        _bubble = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(244, 255, 250, 252)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(218, 174, 190)),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(15),
            Padding = new Thickness(12, 6),
            Child = _speech,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom,
            IsVisible = false
        };
        Grid.SetRow(_bubble, 0);
        root.Children.Add(_bubble);

        var stage = new Grid { Background = Brushes.Transparent };
        Grid.SetRow(stage, 1);
        root.Children.Add(stage);

        var transforms = new TransformGroup();
        transforms.Children.Add(_scale);
        transforms.Children.Add(_rotate);
        transforms.Children.Add(_translate);

        using var stream = AssetLoader.Open(new Uri($"avares://PetFriends.Mac/Assets/{assetName}"));
        _petImage = new Image
        {
            Source = new Bitmap(stream),
            Stretch = Stretch.Uniform,
            RenderTransformOrigin = new RelativePoint(.5, .82, RelativeUnit.Relative),
            RenderTransform = transforms,
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        ToolTip.SetTip(_petImage, $"{petName}：单击摸摸，按住拖动，右键看菜单");
        _petImage.PointerPressed += OnPointerPressed;
        _petImage.PointerMoved += OnPointerMoved;
        _petImage.PointerReleased += OnPointerReleased;
        stage.Children.Add(_petImage);

        _effects = new Canvas
        {
            IsHitTestVisible = false,
            ClipToBounds = false
        };
        stage.Children.Add(_effects);
        Content = root;
    }

    public double PixelWidth => Math.Max(1, Width * RenderScaling);
    public double PixelHeight => Math.Max(1, Height * RenderScaling);
    public Point Center => new(Position.X + PixelWidth / 2, Position.Y + PixelHeight / 2);

    public void SetPetSize(double size)
    {
        Width = size;
        Height = size + BubbleHeight;
        _speech.MaxWidth = Math.Max(80, size - 40);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var properties = e.GetCurrentPoint(_petImage).Properties;
        if (properties.IsRightButtonPressed)
        {
            var menu = MenuFactory?.Invoke(this);
            if (menu is not null)
            {
                menu.Open(_petImage);
            }
            e.Handled = true;
            return;
        }
        if (!properties.IsLeftButtonPressed) return;

        ActivityVersion++;
        IsBusy = false;
        MotionX = 0;
        MotionY = 0;
        SetEdgePeekPose(false, true);
        _pointerDownScreen = this.PointToScreen(e.GetPosition(this));
        _windowDown = Position;
        _pointerDownAt = DateTime.UtcNow;
        LastActionWasDrag = false;
        IsDragging = true;
        e.Pointer.Capture(_petImage);
        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!IsDragging) return;
        var current = this.PointToScreen(e.GetPosition(this));
        var deltaX = current.X - _pointerDownScreen.X;
        var deltaY = current.Y - _pointerDownScreen.Y;
        Position = new PixelPoint(_windowDown.X + deltaX, _windowDown.Y + deltaY);
        LastActionWasDrag |= Math.Abs(deltaX) + Math.Abs(deltaY) >= Math.Max(4, 4 * RenderScaling);
        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!IsDragging || e.InitialPressMouseButton != MouseButton.Left) return;
        e.Pointer.Capture(null);
        IsDragging = false;
        var heldFor = (DateTime.UtcNow - _pointerDownAt).TotalMilliseconds;
        if (!LastActionWasDrag && heldFor < 900)
        {
            Petted?.Invoke(this);
        }
        DragFinished?.Invoke(this);
        e.Handled = true;
    }

    public void Speak(string text, int milliseconds = 2600)
    {
        var version = ++_speechVersion;
        _speech.Text = text;
        _bubble.IsVisible = true;
        _bubble.Opacity = 1;
        _ = HideSpeechAsync(version, milliseconds);
    }

    private async Task HideSpeechAsync(int version, int milliseconds)
    {
        await Task.Delay(milliseconds);
        if (version != _speechVersion) return;
        for (var step = 1; step <= 18; step++)
        {
            if (version != _speechVersion) return;
            _bubble.Opacity = 1 - step / 18d;
            await Task.Delay(20);
        }
        if (version == _speechVersion) _bubble.IsVisible = false;
    }

    public void Hop(bool hearts = false)
    {
        var version = ++_hopVersion;
        if (hearts) Burst("♥", Color.FromRgb(230, 114, 145));
        _ = HopAsync(version, 620, 28);
    }

    public void BounceTwice(string? glyph = null)
    {
        var version = ++_hopVersion;
        if (!string.IsNullOrWhiteSpace(glyph)) Burst(glyph, Color.FromRgb(100, 158, 196));
        _ = BounceTwiceAsync(version);
        Wiggle();
    }

    private async Task HopAsync(int version, int milliseconds, double height)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds < milliseconds)
        {
            if (version != _hopVersion) return;
            var progress = stopwatch.ElapsedMilliseconds / (double)milliseconds;
            var y = progress < .36
                ? -height * EaseOut(progress / .36)
                : -height * (1 - EaseOut((progress - .36) / .64));
            var magnitude = 1 + .07 * Math.Sin(progress * Math.PI);
            _translate.Y = y;
            SetScaleMagnitude(magnitude);
            await Task.Delay(16);
        }
        if (version != _hopVersion) return;
        _translate.Y = 0;
        SetScaleMagnitude(1);
    }

    private async Task BounceTwiceAsync(int version)
    {
        const int milliseconds = 920;
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds < milliseconds)
        {
            if (version != _hopVersion) return;
            var progress = stopwatch.ElapsedMilliseconds / (double)milliseconds;
            _translate.Y = progress switch
            {
                < .2 => -20 * Math.Sin(progress / .2 * Math.PI),
                < .48 => 0,
                < .72 => -16 * Math.Sin((progress - .48) / .24 * Math.PI),
                _ => 0
            };
            await Task.Delay(16);
        }
        if (version == _hopVersion) _translate.Y = 0;
    }

    public void Wiggle()
    {
        var version = ++_rotateVersion;
        _ = WiggleAsync(version);
    }

    private async Task WiggleAsync(int version)
    {
        const int milliseconds = 700;
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds < milliseconds)
        {
            if (version != _rotateVersion) return;
            var progress = stopwatch.ElapsedMilliseconds / (double)milliseconds;
            _rotate.Angle = _restingAngle + Math.Sin(progress * Math.PI * 5) * 7 * (1 - progress);
            await Task.Delay(16);
        }
        if (version == _rotateVersion) _rotate.Angle = _restingAngle;
    }

    public void Burst(string glyph, Color color)
    {
        for (var index = 0; index < 5; index++)
        {
            var mark = new TextBlock
            {
                Text = glyph,
                Foreground = new SolidColorBrush(color),
                FontSize = 22 + Random.Shared.Next(0, 10),
                FontWeight = FontWeight.Bold,
                Opacity = .95
            };
            Canvas.SetLeft(mark, Width * (.22 + Random.Shared.NextDouble() * .56));
            var startTop = Width * (.28 + Random.Shared.NextDouble() * .2);
            Canvas.SetTop(mark, startTop);
            _effects.Children.Add(mark);
            _ = RiseAndRemoveAsync(mark, startTop, 78 + Random.Shared.Next(0, 44));
        }
    }

    private async Task RiseAndRemoveAsync(TextBlock mark, double startTop, double distance)
    {
        const int milliseconds = 1120;
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds < milliseconds)
        {
            var progress = stopwatch.ElapsedMilliseconds / (double)milliseconds;
            Canvas.SetTop(mark, startTop - distance * EaseOut(progress));
            mark.Opacity = 1 - progress;
            await Task.Delay(16);
        }
        _effects.Children.Remove(mark);
    }

    public void FaceDirection(double direction)
    {
        if (Math.Abs(direction) < .01) return;
        _direction = direction < 0 ? -1 : 1;
        _scale.ScaleX = _direction * Math.Abs(_scale.ScaleX);
    }

    public void SetEdgePeekPose(bool enabled, bool fromLeft)
    {
        _rotateVersion++;
        _restingAngle = enabled ? (fromLeft ? -18 : 18) : 0;
        _rotate.Angle = _restingAngle;
        if (enabled) FaceDirection(fromLeft ? 1 : -1);
    }

    private void SetScaleMagnitude(double magnitude)
    {
        _scale.ScaleX = _direction * magnitude;
        _scale.ScaleY = magnitude;
    }

    private static double EaseOut(double progress)
    {
        progress = Math.Clamp(progress, 0, 1);
        return 1 - Math.Pow(1 - progress, 3);
    }
}
