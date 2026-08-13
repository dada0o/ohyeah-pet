using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Cursors = System.Windows.Input.Cursors;
using ContextMenu = System.Windows.Controls.ContextMenu;
using FontFamily = System.Windows.Media.FontFamily;
using Image = System.Windows.Controls.Image;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;

namespace PetFriends;

internal sealed class PetWindow : Window
{
    public const double BubbleHeight = 56;
    private const int MaxParticles = 18;
    private readonly Image _petImage;
    private readonly Border _bubble;
    private readonly TextBlock _speech;
    private readonly Canvas _effects;
    private readonly ScaleTransform _scale;
    private readonly RotateTransform _rotate;
    private readonly TranslateTransform _translate;
    private Point _mouseDown;
    private Point _windowDown;
    private DateTime _mouseDownAt;
    private bool _pointerPressed;

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
        Width = size;
        Height = size + BubbleHeight;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowActivated = false;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(BubbleHeight) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        _speech = new TextBlock
        {
            FontFamily = new FontFamily("Microsoft YaHei UI"),
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(82, 68, 76)),
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = size - 46,
            Margin = new Thickness(10, 6, 10, 6)
        };
        _bubble = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(242, 255, 250, 252)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(218, 174, 190)),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(15),
            Child = _speech,
            HorizontalAlignment = WpfHorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Visibility = Visibility.Hidden
        };
        if (!Compat.UseSafeRendering)
        {
            _bubble.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 8,
                Opacity = .18,
                ShadowDepth = 2
            };
        }
        Grid.SetRow(_bubble, 0);
        root.Children.Add(_bubble);

        var stage = new Grid { Background = null };
        Grid.SetRow(stage, 1);
        root.Children.Add(stage);

        _scale = new ScaleTransform(1, 1);
        _rotate = new RotateTransform(0);
        _translate = new TranslateTransform(0, 0);
        var transforms = new TransformGroup();
        transforms.Children.Add(_scale);
        transforms.Children.Add(_rotate);
        transforms.Children.Add(_translate);

        _petImage = new Image
        {
            Source = LoadImage(assetName),
            Stretch = Stretch.Uniform,
            RenderTransformOrigin = new Point(.5, .82),
            RenderTransform = transforms,
            Cursor = Cursors.Hand,
            ToolTip = $"{petName}：单击摸摸，按住拖动，右键看菜单"
        };
        RenderOptions.SetBitmapScalingMode(
            _petImage,
            Compat.UseSafeRendering ? BitmapScalingMode.LowQuality : BitmapScalingMode.HighQuality);
        _petImage.MouseLeftButtonDown += OnMouseLeftButtonDown;
        _petImage.MouseMove += OnMouseMove;
        _petImage.MouseLeftButtonUp += OnMouseLeftButtonUp;
        _petImage.MouseRightButtonUp += OnMouseRightButtonUp;
        stage.Children.Add(_petImage);

        _effects = new Canvas { IsHitTestVisible = false, ClipToBounds = false };
        stage.Children.Add(_effects);
        Content = root;
    }

    public static BitmapImage LoadImage(string assetName)
    {
        return new BitmapImage(new Uri($"pack://application:,,,/Assets/{assetName}", UriKind.Absolute));
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        ActivityVersion++;
        IsBusy = false;
        MotionX = 0;
        MotionY = 0;
        SetEdgePeekPose(false, true);
        SetHeadPeek(false);
        _mouseDown = PointToScreen(e.GetPosition(this));
        _windowDown = new Point(Left, Top);
        _mouseDownAt = DateTime.UtcNow;
        _pointerPressed = true;
        IsDragging = true;
        LastActionWasDrag = false;
        _petImage.CaptureMouse();
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_pointerPressed || e.LeftButton != MouseButtonState.Pressed) return;
        var current = PointToScreen(e.GetPosition(this));
        var delta = current - _mouseDown;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        _pointerPressed = false;
        if (_petImage.IsMouseCaptured) _petImage.ReleaseMouseCapture();
        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // The button may have been released before WPF starts DragMove.
        }
        finally
        {
            IsDragging = false;
            var windowMoved = (new Point(Left, Top) - _windowDown).Length;
            LastActionWasDrag = windowMoved >= 4;
            DragFinished?.Invoke(this);
        }
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || !_pointerPressed) return;
        _pointerPressed = false;
        if (_petImage.IsMouseCaptured) _petImage.ReleaseMouseCapture();
        IsDragging = false;
        LastActionWasDrag = false;
        var heldFor = (DateTime.UtcNow - _mouseDownAt).TotalMilliseconds;
        if (heldFor < 900)
        {
            Petted?.Invoke(this);
        }
        DragFinished?.Invoke(this);
        e.Handled = true;
    }

    private void OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        var menu = MenuFactory?.Invoke(this);
        if (menu is null) return;
        menu.PlacementTarget = _petImage;
        menu.IsOpen = true;
        e.Handled = true;
    }

    public void Speak(string text, int milliseconds = 2600)
    {
        _speech.Text = text;
        _bubble.Visibility = Visibility.Visible;
        _bubble.Opacity = 1;
        var hide = new DoubleAnimation
        {
            From = 1,
            To = 0,
            BeginTime = TimeSpan.FromMilliseconds(milliseconds),
            Duration = TimeSpan.FromMilliseconds(350),
            FillBehavior = FillBehavior.Stop
        };
        hide.Completed += (_, _) => _bubble.Visibility = Visibility.Hidden;
        _bubble.BeginAnimation(OpacityProperty, hide);
    }

    public void Hop(bool hearts = false)
    {
        var hop = new DoubleAnimationUsingKeyFrames();
        hop.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromPercent(0)));
        hop.KeyFrames.Add(new EasingDoubleKeyFrame(-28, KeyTime.FromPercent(.35), new QuadraticEase { EasingMode = EasingMode.EaseOut }));
        hop.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromPercent(1), new BounceEase { Bounces = 1, Bounciness = 2 }));
        hop.Duration = TimeSpan.FromMilliseconds(620);
        _translate.BeginAnimation(TranslateTransform.YProperty, hop);

        var squash = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(620) };
        squash.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromPercent(0)));
        squash.KeyFrames.Add(new EasingDoubleKeyFrame(1.07, KeyTime.FromPercent(.35)));
        squash.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromPercent(1)));
        _scale.BeginAnimation(ScaleTransform.ScaleXProperty, squash);
        _scale.BeginAnimation(ScaleTransform.ScaleYProperty, squash);
        if (hearts) Burst("♥", Color.FromRgb(230, 114, 145));
    }

    public void Wiggle()
    {
        var wiggle = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(700) };
        wiggle.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(0)));
        wiggle.KeyFrames.Add(new EasingDoubleKeyFrame(-7, KeyTime.FromPercent(.2)));
        wiggle.KeyFrames.Add(new EasingDoubleKeyFrame(7, KeyTime.FromPercent(.45)));
        wiggle.KeyFrames.Add(new EasingDoubleKeyFrame(-4, KeyTime.FromPercent(.7)));
        wiggle.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromPercent(1)));
        _rotate.BeginAnimation(RotateTransform.AngleProperty, wiggle);
    }

    public void BounceTwice(string? glyph = null)
    {
        var bounce = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(920) };
        bounce.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(0)));
        bounce.KeyFrames.Add(new EasingDoubleKeyFrame(-20, KeyTime.FromPercent(.18)));
        bounce.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromPercent(.38)));
        bounce.KeyFrames.Add(new EasingDoubleKeyFrame(-16, KeyTime.FromPercent(.58)));
        bounce.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromPercent(1)));
        _translate.BeginAnimation(TranslateTransform.YProperty, bounce);
        Wiggle();
        if (!string.IsNullOrWhiteSpace(glyph))
        {
            Burst(glyph!, Color.FromRgb(100, 158, 196));
        }
    }

    public void Burst(string glyph, Color color)
    {
        var random = Compat.Random;
        var particleCount = Compat.UseSafeRendering ? 3 : 5;
        for (var index = 0; index < particleCount; index++)
        {
            while (_effects.Children.Count >= MaxParticles)
            {
                _effects.Children.RemoveAt(0);
            }
            var mark = new TextBlock
            {
                Text = glyph,
                Foreground = new SolidColorBrush(color),
                FontSize = 22 + random.Next(0, 10),
                FontWeight = FontWeights.Bold,
                RenderTransform = new TranslateTransform(),
                Opacity = .95
            };
            Canvas.SetLeft(mark, Width * .3 + random.NextDouble() * Width * .4);
            Canvas.SetTop(mark, Height * .34 + random.NextDouble() * Height * .18);
            _effects.Children.Add(mark);
            var rise = new DoubleAnimation(0, -80 - random.Next(0, 45), TimeSpan.FromMilliseconds(1150));
            var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(1150));
            fade.Completed += (_, _) => _effects.Children.Remove(mark);
            ((TranslateTransform)mark.RenderTransform).BeginAnimation(TranslateTransform.YProperty, rise);
            mark.BeginAnimation(OpacityProperty, fade);
        }
    }

    public void FaceDirection(double direction)
    {
        var target = direction < 0 ? -1d : 1d;
        _scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(target, TimeSpan.FromMilliseconds(180)));
    }

    public void SetHeadPeek(bool enabled)
    {
        // Visibility is controlled only by screen/window occlusion. Never crop the artwork.
        if (!enabled) _petImage.Clip = null;
    }

    public void SetEdgePeekPose(bool enabled, bool fromLeft, bool showPaws = true, bool reverseLean = false)
    {
        if (!enabled)
        {
            _petImage.Clip = null;
            _rotate.BeginAnimation(RotateTransform.AngleProperty, null);
            _rotate.Angle = 0;
            return;
        }
        _petImage.Clip = null;
        _rotate.BeginAnimation(RotateTransform.AngleProperty, null);
        // Window-edge peeks lean outward: left is counterclockwise, right is clockwise.
        // Screen-edge hiding uses the opposite lean so the pets follow their motion
        // into the screen boundary: left is clockwise, right is counterclockwise.
        var angle = fromLeft ? -18d : 18d;
        _rotate.Angle = reverseLean ? -angle : angle;
        FaceDirection(fromLeft ? 1 : -1);
    }

    public Point Center => new(Left + Width / 2, Top + Height / 2);
}
