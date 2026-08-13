#if NET35
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Cursors = System.Windows.Input.Cursors;
using ContextMenu = System.Windows.Controls.ContextMenu;
using FontFamily = System.Windows.Media.FontFamily;
using Image = System.Windows.Controls.Image;
using Point = System.Windows.Point;

namespace PetFriends
{
    internal sealed class PetWindow : Window
    {
        public const double BubbleHeight = 56;
        private readonly Image _petImage;
        private readonly Border _bubble;
        private readonly TextBlock _speech;
        private readonly Canvas _effects;
        private readonly ScaleTransform _scale;
        private readonly RotateTransform _rotate;
        private readonly TranslateTransform _translate;
        private Point _windowDown;
        private DateTime _mouseDownAt;

        public string PetName { get; private set; }
        public bool IsDragging { get; private set; }
        public bool LastActionWasDrag { get; private set; }
        public bool IsBusy { get; set; }
        public bool IgnoreActivityBounds { get; set; }
        public int ActivityVersion { get; set; }
        public double MotionX { get; set; }
        public double MotionY { get; set; }
        public DateTime MotionUntil { get; set; }
        public Action<PetWindow> Petted { get; set; }
        public Action<PetWindow> DragFinished { get; set; }
        public Action<PetWindow> ActivityCancelled { get; set; }
        public Func<PetWindow, ContextMenu> MenuFactory { get; set; }

        public PetWindow(string petName, string assetName, double size)
        {
            PetName = petName;
            Width = size;
            Height = size + BubbleHeight;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;
            SnapsToDevicePixels = true;

            Grid root = new Grid();
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
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Visibility = Visibility.Hidden
            };
            Grid.SetRow(_bubble, 0);
            root.Children.Add(_bubble);

            Grid stage = new Grid();
            Grid.SetRow(stage, 1);
            root.Children.Add(stage);

            _scale = new ScaleTransform(1, 1);
            _rotate = new RotateTransform(0);
            _translate = new TranslateTransform(0, 0);
            TransformGroup transforms = new TransformGroup();
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
                ToolTip = petName + "：单击摸摸，按住拖动，右键看菜单"
            };
            RenderOptions.SetBitmapScalingMode(_petImage, BitmapScalingMode.LowQuality);
            _petImage.MouseLeftButtonDown += OnMouseLeftButtonDown;
            _petImage.MouseRightButtonUp += OnMouseRightButtonUp;
            stage.Children.Add(_petImage);

            _effects = new Canvas { IsHitTestVisible = false, ClipToBounds = false };
            stage.Children.Add(_effects);
            Content = root;
        }

        public static BitmapImage LoadImage(string assetName)
        {
            return new BitmapImage(new Uri("pack://application:,,,/Assets/" + assetName, UriKind.Absolute));
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (ActivityCancelled != null) ActivityCancelled(this);
            ActivityVersion++;
            IsBusy = false;
            MotionX = 0;
            MotionY = 0;
            SetEdgePeekPose(false, true, true, false);
            SetHeadPeek(false);
            _windowDown = new Point(Left, Top);
            _mouseDownAt = DateTime.UtcNow;
            IsDragging = true;
            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
            }
            finally
            {
                IsDragging = false;
                double windowMoved = (new Point(Left, Top) - _windowDown).Length;
                double heldFor = (DateTime.UtcNow - _mouseDownAt).TotalMilliseconds;
                LastActionWasDrag = windowMoved >= 4;
                if (windowMoved < 4 && heldFor < 900 && Petted != null) Petted(this);
                if (DragFinished != null) DragFinished(this);
            }
        }

        private void OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            ContextMenu menu = MenuFactory == null ? null : MenuFactory(this);
            if (menu == null) return;
            menu.PlacementTarget = _petImage;
            menu.IsOpen = true;
            e.Handled = true;
        }

        public void Speak(string text, int milliseconds)
        {
            _speech.Text = text;
            _bubble.Visibility = Visibility.Visible;
            _bubble.Opacity = 1;
            DoubleAnimation hide = new DoubleAnimation
            {
                From = 1,
                To = 0,
                BeginTime = TimeSpan.FromMilliseconds(milliseconds),
                Duration = TimeSpan.FromMilliseconds(350),
                FillBehavior = FillBehavior.Stop
            };
            hide.Completed += delegate { _bubble.Visibility = Visibility.Hidden; };
            _bubble.BeginAnimation(OpacityProperty, hide);
        }

        public void Speak(string text)
        {
            Speak(text, 2600);
        }

        public void Hop(bool hearts)
        {
            DoubleAnimationUsingKeyFrames hop = new DoubleAnimationUsingKeyFrames();
            hop.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(0)));
            hop.KeyFrames.Add(new LinearDoubleKeyFrame(-26, KeyTime.FromPercent(.36)));
            hop.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(1)));
            hop.Duration = TimeSpan.FromMilliseconds(620);
            _translate.BeginAnimation(TranslateTransform.YProperty, hop);

            DoubleAnimationUsingKeyFrames squash = new DoubleAnimationUsingKeyFrames();
            squash.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromPercent(0)));
            squash.KeyFrames.Add(new LinearDoubleKeyFrame(1.07, KeyTime.FromPercent(.36)));
            squash.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromPercent(1)));
            squash.Duration = TimeSpan.FromMilliseconds(620);
            _scale.BeginAnimation(ScaleTransform.ScaleYProperty, squash);
            if (hearts) Burst("♥", Color.FromRgb(230, 114, 145));
        }

        public void Hop()
        {
            Hop(false);
        }

        public void Wiggle()
        {
            DoubleAnimationUsingKeyFrames wiggle = new DoubleAnimationUsingKeyFrames();
            wiggle.Duration = TimeSpan.FromMilliseconds(700);
            wiggle.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(0)));
            wiggle.KeyFrames.Add(new LinearDoubleKeyFrame(-7, KeyTime.FromPercent(.2)));
            wiggle.KeyFrames.Add(new LinearDoubleKeyFrame(7, KeyTime.FromPercent(.45)));
            wiggle.KeyFrames.Add(new LinearDoubleKeyFrame(-4, KeyTime.FromPercent(.7)));
            wiggle.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(1)));
            _rotate.BeginAnimation(RotateTransform.AngleProperty, wiggle);
        }

        public void BounceTwice(string glyph)
        {
            DoubleAnimationUsingKeyFrames bounce = new DoubleAnimationUsingKeyFrames();
            bounce.Duration = TimeSpan.FromMilliseconds(920);
            bounce.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(0)));
            bounce.KeyFrames.Add(new LinearDoubleKeyFrame(-20, KeyTime.FromPercent(.18)));
            bounce.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(.38)));
            bounce.KeyFrames.Add(new LinearDoubleKeyFrame(-16, KeyTime.FromPercent(.58)));
            bounce.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(1)));
            _translate.BeginAnimation(TranslateTransform.YProperty, bounce);
            Wiggle();
            if (!Compat.IsNullOrWhiteSpace(glyph)) Burst(glyph, Color.FromRgb(100, 158, 196));
        }

        public void Burst(string glyph, Color color)
        {
            Random random = Compat.Random;
            for (int index = 0; index < 3; index++)
            {
                TextBlock mark = new TextBlock
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
                DoubleAnimation rise = new DoubleAnimation(0, -80 - random.Next(0, 45), TimeSpan.FromMilliseconds(1150));
                DoubleAnimation fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(1150));
                fade.Completed += delegate { _effects.Children.Remove(mark); };
                ((TranslateTransform)mark.RenderTransform).BeginAnimation(TranslateTransform.YProperty, rise);
                mark.BeginAnimation(OpacityProperty, fade);
            }
        }

        public void FaceDirection(double direction)
        {
            double target = direction < 0 ? -1d : 1d;
            _scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(target, TimeSpan.FromMilliseconds(180)));
        }

        public void SetHeadPeek(bool enabled)
        {
            if (!enabled) _petImage.Clip = null;
        }

        public void SetEdgePeekPose(bool enabled, bool fromLeft, bool showPaws, bool reverseLean)
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
            double angle = fromLeft ? -18d : 18d;
            _rotate.Angle = reverseLean ? -angle : angle;
            FaceDirection(fromLeft ? 1 : -1);
        }

        public void SetEdgePeekPose(bool enabled, bool fromLeft)
        {
            SetEdgePeekPose(enabled, fromLeft, true, false);
        }

        public Point Center
        {
            get { return new Point(Left + Width / 2, Top + Height / 2); }
        }
    }
}
#endif
