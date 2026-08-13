using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace PetFriends.Mac;

internal sealed class CuddleWindow : Window
{
    private readonly TextBlock _caption;
    private readonly ScaleTransform _scale = new(.94, .94);

    public CuddleWindow()
    {
        Width = 320;
        Height = 305;
        WindowDecorations = Avalonia.Controls.WindowDecorations.None;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Background = Brushes.Transparent;
        Topmost = true;
        ShowActivated = false;
        ShowInTaskbar = false;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.Manual;

        using var stream = AssetLoader.Open(new Uri("avares://PetFriends.Mac/Assets/cuddle.png"));
        var image = new Image
        {
            Source = new Bitmap(stream),
            Stretch = Stretch.Uniform,
            RenderTransformOrigin = new RelativePoint(.5, .75, RelativeUnit.Relative),
            RenderTransform = _scale
        };

        _caption = new TextBlock
        {
            Text = "贴贴时间 ♥",
            FontSize = 15,
            FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(184, 90, 120)),
            Background = new SolidColorBrush(Color.FromArgb(230, 255, 248, 251)),
            Padding = new Thickness(14, 6),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
            Margin = new Thickness(0, 12, 0, 0)
        };

        var grid = new Grid { IsHitTestVisible = false };
        grid.Children.Add(image);
        grid.Children.Add(_caption);
        Content = grid;
    }

    public void Play(string caption)
    {
        _caption.Text = caption;
        Opacity = 0;
        Show();
        _ = AnimateAsync();
    }

    private async Task AnimateAsync()
    {
        const int fadeMilliseconds = 220;
        var fade = Stopwatch.StartNew();
        while (fade.ElapsedMilliseconds < fadeMilliseconds)
        {
            var progress = fade.ElapsedMilliseconds / (double)fadeMilliseconds;
            Opacity = Math.Clamp(progress, 0, 1);
            await Task.Delay(16);
        }
        Opacity = 1;

        const int pulseMilliseconds = 4080;
        var pulse = Stopwatch.StartNew();
        while (pulse.ElapsedMilliseconds < pulseMilliseconds)
        {
            var progress = pulse.ElapsedMilliseconds / 680d;
            var magnitude = .94 + .095 * (.5 - .5 * Math.Cos(progress * Math.PI));
            _scale.ScaleX = magnitude;
            _scale.ScaleY = magnitude;
            await Task.Delay(16);
        }
    }
}
