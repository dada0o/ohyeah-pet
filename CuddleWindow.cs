using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using Image = System.Windows.Controls.Image;
using Point = System.Windows.Point;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;

namespace PetFriends;

internal sealed class CuddleWindow : Window
{
    private readonly TextBlock _caption;
    private readonly ScaleTransform _scale;

    public CuddleWindow()
    {
        Width = 320;
        Height = 305;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowActivated = false;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        IsHitTestVisible = false;

        var grid = new Grid();
        _scale = new ScaleTransform(.92, .92);
        var image = new Image
        {
            Source = PetWindow.LoadImage("cuddle.png"),
            Stretch = Stretch.Uniform,
            RenderTransformOrigin = new Point(.5, .75),
            RenderTransform = _scale
        };
        RenderOptions.SetBitmapScalingMode(
            image,
            Compat.UseSafeRendering ? BitmapScalingMode.LowQuality : BitmapScalingMode.HighQuality);
        grid.Children.Add(image);

        _caption = new TextBlock
        {
            Text = "贴贴时间 ♥",
            FontFamily = new FontFamily("Microsoft YaHei UI"),
            FontSize = 15,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(184, 90, 120)),
            Background = new SolidColorBrush(Color.FromArgb(224, 255, 248, 251)),
            Padding = new Thickness(14, 6, 14, 6),
            HorizontalAlignment = WpfHorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 12, 0, 0)
        };
        grid.Children.Add(_caption);
        Content = grid;
    }

    public void Play(string caption)
    {
        _caption.Text = caption;
        Opacity = 0;
        Show();
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)));
        var pulse = new DoubleAnimation(.94, 1.035, TimeSpan.FromMilliseconds(680))
        {
            AutoReverse = true,
            RepeatBehavior = new RepeatBehavior(3)
        };
        _scale.BeginAnimation(ScaleTransform.ScaleXProperty, pulse);
        _scale.BeginAnimation(ScaleTransform.ScaleYProperty, pulse);
    }
}
