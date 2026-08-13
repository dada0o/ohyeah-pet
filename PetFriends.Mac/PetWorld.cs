using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;

namespace PetFriends.Mac;

internal sealed class PetWorld
{
    private enum ActivityMode
    {
        Focus,
        FullScreen
    }

    private sealed record PairScene(string CatLine, string DogLine, string Glyph, Color Color, bool Bounce = false);
    private readonly record struct Area(double Left, double Top, double Width, double Height)
    {
        public double Right => Left + Width;
        public double Bottom => Top + Height;
    }

    private const double BasePetSize = 160;
    private readonly IClassicDesktopStyleApplicationLifetime _desktop;
    private readonly PetWindow _cat = new("小欧公爵", "cat.png", BasePetSize);
    private readonly PetWindow _dog = new("小耶牧师", "dog.png", BasePetSize);
    private readonly DispatcherTimer _motionTimer = new() { Interval = TimeSpan.FromMilliseconds(40) };
    private readonly DispatcherTimer _lifeTimer = new() { Interval = TimeSpan.FromSeconds(4) };
    private readonly DispatcherTimer _interactionTimer = new() { Interval = TimeSpan.FromSeconds(7) };
    private readonly DispatcherTimer _proximityTimer = new() { Interval = TimeSpan.FromMilliseconds(600) };
    private readonly string[] _catLines =
    [
        "本公爵允许你摸一下。", "今天也要优雅地发呆。", "小耶，你的眼镜歪啦。", "窗外有没有小鸟？",
        "再摸一下也不是不行……", "忙累了就休息一下。", "本公爵在这里陪着。", "要保持从容。",
        "这个位置很舒服。", "偶尔发呆也很重要。", "先喝口水再继续。", "今天也有好好努力。"
    ];
    private readonly string[] _dogLines =
    [
        "愿今天有好多好心情～", "小欧，等等我呀！", "要不要休息一下？", "发现一位认真工作的人！",
        "摸摸可以补充元气～", "我会乖乖陪着你的。", "休息一分钟也可以哦。", "偷偷送你一点元气～",
        "今天也会顺顺利利！", "好像闻到了点心的香味。", "再坚持一下就好啦。", "有需要就叫我呀。"
    ];
    private readonly PairScene[] _pairScenes =
    [
        new("碰碰鼻子。", "碰到啦！好运传递～", "✦", Color.FromRgb(100, 158, 196), true),
        new("爪子给我，别走丢了。", "牵好啦！一起走～", "∞", Color.FromRgb(113, 162, 195)),
        new("小耶，过来。", "啵～送你一个脸颊亲亲！", "♥", Color.FromRgb(235, 112, 145), true),
        new("……再蹭一下也可以。", "蹭蹭小欧～", "♥", Color.FromRgb(221, 133, 162)),
        new("巧了，本公爵也是。要保密。", "悄悄告诉你：我最喜欢和你玩。", "♥", Color.FromRgb(220, 121, 151)),
        new("别动，发饰有一点歪。", "谢谢小欧！我也帮你理理毛～", "✦", Color.FromRgb(116, 165, 199)),
        new("配合得不错，击掌！", "啪！今天也默契满分～", "★", Color.FromRgb(225, 171, 88), true),
        new("就跳一小会儿。", "一、二、转圈圈～", "♪", Color.FromRgb(120, 145, 205), true),
        new("这份点心分你一半。", "一起吃果然更香～", "●", Color.FromRgb(211, 166, 101), true),
        new("有你在，已经好多了。", "不开心的话，可以告诉我哦。", "♥", Color.FromRgb(228, 129, 154)),
        new("安静一点，午睡时间。", "好～我靠着你睡。", "Z", Color.FromRgb(116, 146, 184)),
        new("眼光不错，小耶也很可爱。", "小欧今天特别帅气！", "★", Color.FromRgb(225, 171, 88), true)
    ];

    private TrayIcon? _tray;
    private bool _quiet;
    private bool _isCuddling;
    private bool _isPairActivity;
    private bool _wereClose;
    private bool _exiting;
    private ActivityMode _activityMode = ActivityMode.Focus;
    private Point? _focusAnchor;
    private double _petScale = 1;
    private DateTime _nextInteraction = DateTime.UtcNow.AddSeconds(5);
    private DateTime _nextAdventure = DateTime.UtcNow.AddSeconds(8);

    public PetWorld(IClassicDesktopStyleApplicationLifetime desktop)
    {
        _desktop = desktop;
    }

    public void Start()
    {
        Configure(_cat);
        Configure(_dog);
        _cat.Show();
        _dog.Show();

        var area = GetFocusArea();
        SetPosition(_cat, area.Right - _cat.PixelWidth * 2 - ScalePixels(60), area.Bottom - _cat.PixelHeight + ScalePixels(24));
        SetPosition(_dog, area.Right - _dog.PixelWidth - ScalePixels(20), area.Bottom - _dog.PixelHeight + ScalePixels(24));
        _cat.Speak("我是小欧公爵，摸摸看？", 3400);
        _dog.Speak("我是小耶牧师，请多关照～", 3400);

        _motionTimer.Tick += MovePets;
        _lifeTimer.Tick += LifeTick;
        _interactionTimer.Tick += InteractionTick;
        _proximityTimer.Tick += ProximityTick;
        _motionTimer.Start();
        _lifeTimer.Start();
        _interactionTimer.Start();
        _proximityTimer.Start();
        CreateTrayIcon();
    }

    private void Configure(PetWindow pet)
    {
        pet.Petted = Petted;
        pet.DragFinished = DragFinished;
        pet.MenuFactory = BuildMenu;
    }

    private void Petted(PetWindow pet)
    {
        if (_isCuddling || _isPairActivity) return;
        StopMotion(pet);
        pet.Hop(hearts: true);
        pet.Speak(pet == _cat
            ? Pick("嗯……手法尚可。", "呼噜……再来一下。", "本公爵心情+1 ♥", "耳朵也可以摸一下。", "这次摸得很舒服。")
            : Pick("好舒服～谢谢你！", "汪呜！元气满满！", "再摸摸耳朵嘛～", "送你一个小爪印 ♥", "今天的元气补满啦！"));

        if (DistanceBetweenPets() < ScalePixels(205) && Random.Shared.NextDouble() < .42)
        {
            var other = pet == _cat ? _dog : _cat;
            other.Speak(pet == _cat ? "小耶也想要摸摸！" : "本公爵也在这里。", 2200);
            other.Wiggle();
        }
    }

    private void DragFinished(PetWindow pet)
    {
        if (pet.LastActionWasDrag) _focusAnchor = pet.Center;
        ClampToActivityArea(pet);
        StopMotion(pet);
        if (_activityMode == ActivityMode.Focus && pet.LastActionWasDrag)
        {
            FollowToFocusArea(pet == _cat ? _dog : _cat);
        }
        if (DistanceBetweenPets() < ScalePixels(185) && !_quiet && !_isCuddling && !_isPairActivity)
        {
            _nextInteraction = DateTime.UtcNow.AddSeconds(9);
            TriggerRandomPairInteraction();
        }
    }

    private void MovePets(object? sender, EventArgs e)
    {
        if (_quiet || _isCuddling) return;
        foreach (var pet in new[] { _cat, _dog })
        {
            if (pet.IsDragging || pet.IsBusy || (Math.Abs(pet.MotionX) < .05 && Math.Abs(pet.MotionY) < .05)) continue;
            if (DateTime.UtcNow > pet.MotionUntil)
            {
                var shouldReturn = pet.IgnoreActivityBounds && _activityMode == ActivityMode.Focus;
                pet.IgnoreActivityBounds = false;
                StopMotion(pet);
                if (shouldReturn) FollowToFocusArea(pet);
                continue;
            }

            var area = pet.IgnoreActivityBounds ? GetWorkArea() : GetActivityArea();
            var nextLeft = pet.Position.X + pet.MotionX;
            var nextTop = pet.Position.Y + pet.MotionY;
            if (nextLeft <= area.Left - pet.PixelWidth * .18 || nextLeft >= area.Right - pet.PixelWidth * .82)
            {
                pet.MotionX *= -1;
                pet.FaceDirection(pet.MotionX);
            }
            if (nextTop <= area.Top || nextTop >= area.Bottom - pet.PixelHeight + ScalePixels(24))
            {
                pet.MotionY *= -1;
            }
            SetPosition(pet, pet.Position.X + pet.MotionX, pet.Position.Y + pet.MotionY);
            ClampPetToArea(pet, area, pet.IgnoreActivityBounds || _activityMode == ActivityMode.FullScreen);
        }
    }

    private void LifeTick(object? sender, EventArgs e)
    {
        if (_quiet || _isCuddling || _isPairActivity) return;
        var pet = Random.Shared.Next(2) == 0 ? _cat : _dog;
        if (pet.IsDragging || pet.IsBusy) return;
        var roll = Random.Shared.NextDouble();
        if (DateTime.UtcNow >= _nextAdventure && roll < .22)
        {
            _nextAdventure = DateTime.UtcNow.AddSeconds(Random.Shared.Next(14, 24));
            if (_activityMode == ActivityMode.FullScreen && Random.Shared.Next(3) == 0) HideAtScreenEdge(force: false);
            else StartFreeRun(pet);
        }
        else if (roll < .4) pet.Wiggle();
        else if (roll < .56) pet.Hop();
        else if (roll < .76) pet.Speak(Pick(pet == _cat ? _catLines : _dogLines), 2400);
        else StartFreeRun(pet);
    }

    private void InteractionTick(object? sender, EventArgs e)
    {
        if (_quiet || _isCuddling || _isPairActivity || _cat.IsBusy || _dog.IsBusy || DateTime.UtcNow < _nextInteraction) return;
        _nextInteraction = DateTime.UtcNow.AddSeconds(Random.Shared.Next(10, 19));
        if (DistanceBetweenPets() < ScalePixels(220)) TriggerRandomPairInteraction();
        else GatherForInteraction();
    }

    private void ProximityTick(object? sender, EventArgs e)
    {
        if (_quiet || _isCuddling || _isPairActivity || _cat.IsBusy || _dog.IsBusy || _cat.IsDragging || _dog.IsDragging) return;
        var close = DistanceBetweenPets() < ScalePixels(185);
        if (close && !_wereClose && DateTime.UtcNow >= _nextInteraction)
        {
            _nextInteraction = DateTime.UtcNow.AddSeconds(9);
            TriggerRandomPairInteraction();
        }
        _wereClose = close;
    }

    private async void GatherForInteraction()
    {
        if (!BeginPairActivity()) return;
        var area = GetActivityArea();
        var centerX = Math.Clamp((_cat.Center.X + _dog.Center.X) / 2, area.Left + ScalePixels(160), area.Right - ScalePixels(160));
        var centerY = Math.Clamp((_cat.Center.Y + _dog.Center.Y) / 2, area.Top + ScalePixels(80), area.Bottom - ScalePixels(80));
        _dog.Speak(Pick("小欧，我来找你啦～", "一起玩一会儿吧！", "小欧，靠近一点嘛。"), 2500);
        _cat.Speak(Pick("慢一点，本公爵在这里。", "正好，本公爵也想找你。", "过来吧，小耶。"), 2500);
        var catVersion = PreparePairPet(_cat);
        var dogVersion = PreparePairPet(_dog);
        await Task.WhenAll(
            GlideTo(_cat, centerX - _cat.PixelWidth + ScalePixels(14), centerY - _cat.PixelHeight / 2, 880, catVersion),
            GlideTo(_dog, centerX - ScalePixels(14), centerY - _dog.PixelHeight / 2, 880, dogVersion));
        EndPairActivity();
        TriggerRandomPairInteraction();
    }

    private void TriggerRandomPairInteraction()
    {
        if (Random.Shared.Next(8) == 0)
        {
            BeginCuddle();
            return;
        }
        PlayPairScene(_pairScenes[Random.Shared.Next(_pairScenes.Length)]);
    }

    private async void PlayPairScene(PairScene scene)
    {
        if (!BeginPairActivity()) return;
        await MoveCloseTogether(540);
        if (!_isPairActivity) return;
        _cat.Speak(scene.CatLine, 3000);
        _dog.Speak(scene.DogLine, 3000);
        _cat.Burst(scene.Glyph, scene.Color);
        _dog.Burst(scene.Glyph, scene.Color);
        if (scene.Bounce)
        {
            _cat.BounceTwice(scene.Glyph);
            await Task.Delay(220);
            _dog.BounceTwice(scene.Glyph);
        }
        else
        {
            _cat.Wiggle();
            _dog.Wiggle();
        }
        await Task.Delay(2800);
        EndPairActivity();
    }

    private async Task MoveCloseTogether(int milliseconds)
    {
        var area = GetActivityArea();
        var center = Math.Clamp((_cat.Center.X + _dog.Center.X) / 2, area.Left + ScalePixels(160), area.Right - ScalePixels(160));
        var targetTop = Math.Clamp(Math.Max(_cat.Position.Y, _dog.Position.Y), area.Top, area.Bottom - Math.Max(_cat.PixelHeight, _dog.PixelHeight) + ScalePixels(24));
        _cat.FaceDirection(1);
        _dog.FaceDirection(-1);
        var catVersion = PreparePairPet(_cat);
        var dogVersion = PreparePairPet(_dog);
        await Task.WhenAll(
            GlideTo(_cat, center - _cat.PixelWidth + ScalePixels(14), targetTop, milliseconds, catVersion),
            GlideTo(_dog, center - ScalePixels(14), targetTop, milliseconds, dogVersion));
    }

    private bool BeginPairActivity()
    {
        if (_isCuddling || _isPairActivity) return false;
        CancelActivity(_cat);
        CancelActivity(_dog);
        _isPairActivity = true;
        _nextInteraction = DateTime.UtcNow.AddSeconds(9);
        return true;
    }

    private void EndPairActivity()
    {
        StopMotion(_cat);
        StopMotion(_dog);
        _cat.IsBusy = false;
        _dog.IsBusy = false;
        _isPairActivity = false;
    }

    private async void BeginCuddle()
    {
        if (_isCuddling || _isPairActivity) return;
        _isCuddling = true;
        StopMotion(_cat);
        StopMotion(_dog);
        var centerX = (_cat.Center.X + _dog.Center.X) / 2;
        var bottom = Math.Max(_cat.Position.Y + _cat.PixelHeight, _dog.Position.Y + _dog.PixelHeight);
        _cat.Hide();
        _dog.Hide();
        var cuddle = new CuddleWindow();
        cuddle.Play(Pick("贴贴时间 ♥", "最喜欢和你一起！", "友情充电中……"));
        var area = GetActivityArea();
        SetPosition(cuddle,
            Math.Clamp(centerX - cuddle.Width * cuddle.RenderScaling / 2, area.Left, area.Right - cuddle.Width * cuddle.RenderScaling),
            Math.Clamp(bottom - cuddle.Height * cuddle.RenderScaling, area.Top, area.Bottom - cuddle.Height * cuddle.RenderScaling));
        await Task.Delay(4300);
        cuddle.Close();
        SetPosition(_cat, Math.Clamp(centerX - _cat.PixelWidth + ScalePixels(26), area.Left, area.Right - _cat.PixelWidth), Math.Clamp(bottom - _cat.PixelHeight, area.Top, area.Bottom - _cat.PixelHeight + ScalePixels(24)));
        SetPosition(_dog, Math.Clamp(centerX - ScalePixels(25), area.Left, area.Right - _dog.PixelWidth), Math.Clamp(bottom - _dog.PixelHeight, area.Top, area.Bottom - _dog.PixelHeight + ScalePixels(24)));
        _cat.Show();
        _dog.Show();
        _cat.Hop(hearts: true);
        _dog.Hop(hearts: true);
        _isCuddling = false;
    }

    private ContextMenu BuildMenu(PetWindow pet)
    {
        var menu = new ContextMenu();
        menu.Items.Add(MenuItem($"摸摸{pet.PetName}", (_, _) => Petted(pet)));
        menu.Items.Add(MenuItem("让他们贴贴", (_, _) => GatherAndCuddle()));
        menu.Items.Add(MenuItem("送一份小零食", (_, _) => FeedBoth()));

        var playMenu = new MenuItem { Header = "互动小游戏" };
        playMenu.Items.Add(MenuItem("随机互动", (_, _) => TriggerRandomPairInteraction()));
        for (var index = 0; index < _pairScenes.Length; index++)
        {
            var scene = _pairScenes[index];
            var header = index switch
            {
                0 => "碰碰鼻子",
                1 => "牵爪",
                2 => "亲一下脸颊",
                3 => "蹭蹭脸",
                4 => "说悄悄话",
                5 => "互相整理毛发",
                6 => "击掌",
                7 => "同步跳舞",
                8 => "分享点心",
                9 => "互相安慰",
                10 => "一起打盹",
                _ => "互相夸奖"
            };
            playMenu.Items.Add(MenuItem(header, (_, _) => PlayPairScene(scene)));
        }
        menu.Items.Add(playMenu);

        var activityMenu = new MenuItem { Header = "活动范围" };
        activityMenu.Items.Add(MenuItem("专注陪伴（拖到哪里就在哪里）", (_, _) => SetActivityMode(ActivityMode.Focus)));
        activityMenu.Items.Add(MenuItem("全屏撒欢", (_, _) => SetActivityMode(ActivityMode.FullScreen)));
        menu.Items.Add(activityMenu);

        var roamMenu = new MenuItem { Header = "自由活动动作" };
        roamMenu.Items.Add(MenuItem("到处跑跑", (_, _) => StartFreeRun(pet, allowFullScreen: true)));
        roamMenu.Items.Add(MenuItem("一起躲到屏幕边缘", (_, _) => HideAtScreenEdge(force: true)));
        roamMenu.Items.Add(MenuItem("回到桌面右下角", (_, _) => BringBack()));
        menu.Items.Add(roamMenu);
        menu.Items.Add(new Separator());

        var quietItem = new MenuItem
        {
            Header = "安静模式",
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = _quiet
        };
        quietItem.Click += (_, _) =>
        {
            _quiet = quietItem.IsChecked == true;
            StopMotion(_cat);
            StopMotion(_dog);
            if (_quiet)
            {
                _cat.Speak("那就安静陪着你。", 2300);
                _dog.Speak("嘘～专心时间。", 2300);
            }
        };
        menu.Items.Add(quietItem);

        var sizeMenu = new MenuItem { Header = "桌宠大小" };
        sizeMenu.Items.Add(MenuItem("迷你", (_, _) => SetScale(.78)));
        sizeMenu.Items.Add(MenuItem("刚刚好（默认）", (_, _) => SetScale(1)));
        sizeMenu.Items.Add(MenuItem("大一点", (_, _) => SetScale(1.3)));
        menu.Items.Add(sizeMenu);
        menu.Items.Add(new Separator());
        menu.Items.Add(MenuItem("退出桌宠", (_, _) => Exit()));
        return menu;
    }

    private static MenuItem MenuItem(string header, EventHandler<RoutedEventArgs> action)
    {
        var item = new MenuItem { Header = header };
        item.Click += action;
        return item;
    }

    private async void SetActivityMode(ActivityMode mode)
    {
        if (_activityMode == mode) return;
        _activityMode = mode;
        CancelActivity(_cat);
        CancelActivity(_dog);
        if (mode == ActivityMode.Focus)
        {
            _focusAnchor ??= new Point((_cat.Center.X + _dog.Center.X) / 2, (_cat.Center.Y + _dog.Center.Y) / 2);
            var area = GetFocusArea();
            _cat.Speak("专注陪伴模式：就在这里陪你。", 3200);
            _dog.Speak("我们会在你放的位置附近活动～", 3200);
            await Task.WhenAll(
                GlideSolo(_cat, area.Right - _cat.PixelWidth * 2 - ScalePixels(35), area.Bottom - _cat.PixelHeight + ScalePixels(24), 750),
                GlideSolo(_dog, area.Right - _dog.PixelWidth - ScalePixels(12), area.Bottom - _dog.PixelHeight + ScalePixels(24), 750));
        }
        else
        {
            _cat.Speak("全屏撒欢模式，出发！", 2600);
            _dog.Speak("可以到处探险啦～", 2600);
            _cat.Hop();
            _dog.Hop();
        }
    }

    private async Task GlideSolo(PetWindow pet, double left, double top, int milliseconds)
    {
        pet.IsBusy = true;
        var version = ++pet.ActivityVersion;
        await GlideTo(pet, left, top, milliseconds, version);
        if (pet.ActivityVersion == version) pet.IsBusy = false;
    }

    private async void FollowToFocusArea(PetWindow pet)
    {
        if (_activityMode != ActivityMode.Focus || pet.IsDragging) return;
        var area = GetFocusArea();
        if (IsInsideArea(pet, area)) return;
        CancelActivity(pet);
        pet.Speak(Pick("我也过来啦～", "等等我，一起待在这里。", "换到这边陪你。"), 2400);
        await GlideSolo(pet, area.Right - pet.PixelWidth - ScalePixels(18), area.Bottom - pet.PixelHeight + ScalePixels(24), 760);
        ClampToActivityArea(pet);
    }

    private async void GatherAndCuddle()
    {
        if (_isCuddling || _isPairActivity) return;
        var area = GetActivityArea();
        var center = Math.Clamp((_cat.Center.X + _dog.Center.X) / 2, area.Left + ScalePixels(190), area.Right - ScalePixels(190));
        var top = area.Bottom - Math.Max(_cat.PixelHeight, _dog.PixelHeight) + ScalePixels(24);
        SetPosition(_cat, center - _cat.PixelWidth + ScalePixels(45), top);
        SetPosition(_dog, center - ScalePixels(45), top);
        _cat.Speak("过来，小耶。", 1300);
        _dog.Speak("来啦来啦～", 1300);
        await Task.Delay(900);
        BeginCuddle();
    }

    private void FeedBoth()
    {
        StopMotion(_cat);
        StopMotion(_dog);
        _cat.Speak("这份点心很合本公爵心意。", 3000);
        _dog.Speak("好吃！谢谢招待～", 3000);
        _cat.Hop(hearts: true);
        _dog.Hop(hearts: true);
    }

    private void SetScale(double scale)
    {
        if (Math.Abs(scale - _petScale) < .01) return;
        foreach (var pet in new[] { _cat, _dog })
        {
            var bottom = pet.Position.Y + pet.PixelHeight;
            var center = pet.Position.X + pet.PixelWidth / 2;
            pet.SetPetSize(BasePetSize * scale);
            SetPosition(pet, center - pet.PixelWidth / 2, bottom - pet.PixelHeight);
            ClampToActivityArea(pet);
        }
        _petScale = scale;
    }

    private void StartFreeRun(PetWindow pet, bool allowFullScreen = false)
    {
        if (pet.IsBusy || pet.IsDragging) return;
        var area = allowFullScreen ? GetWorkArea() : GetActivityArea();
        var targetX = area.Left + Random.Shared.NextDouble() * Math.Max(1, area.Width - pet.PixelWidth);
        var targetY = area.Top + Random.Shared.NextDouble() * Math.Max(1, area.Height - pet.PixelHeight);
        var deltaX = targetX - pet.Position.X;
        var deltaY = targetY - pet.Position.Y;
        var distance = Math.Max(1, Math.Sqrt(deltaX * deltaX + deltaY * deltaY));
        var duration = Random.Shared.Next(2200, 4200);
        var speed = Math.Clamp(distance / Math.Max(1, duration / 40d), ScalePixels(1.2), ScalePixels(3.2));
        pet.MotionX = deltaX / distance * speed;
        pet.MotionY = deltaY / distance * speed;
        pet.MotionUntil = DateTime.UtcNow.AddMilliseconds(duration);
        pet.IgnoreActivityBounds = allowFullScreen;
        pet.FaceDirection(deltaX);
        pet.Speak(_activityMode == ActivityMode.Focus
            ? Pick("在这里陪你～", "小范围巡逻。", "不打扰你工作。")
            : Pick("去那边看看！", "巡逻时间～", "换个地方待一会儿。"), 1800);
    }

    private async void HideAtScreenEdge(bool force)
    {
        if (_activityMode != ActivityMode.FullScreen && !force)
        {
            StartFreeRun(Random.Shared.Next(2) == 0 ? _cat : _dog);
            return;
        }
        if (!BeginPairActivity()) return;
        var area = GetWorkArea();
        var fromLeft = (_cat.Center.X + _dog.Center.X) / 2 < (area.Left + area.Right) / 2;
        var hiddenLeft = fromLeft ? area.Left - _cat.PixelWidth * .55 : area.Right - _cat.PixelWidth * .45;
        var groupTop = Math.Clamp((_cat.Position.Y + _dog.Position.Y) / 2 - ScalePixels(80), area.Top + ScalePixels(24), area.Bottom - _dog.PixelHeight - ScalePixels(145));
        var catVersion = PreparePairPet(_cat);
        var dogVersion = PreparePairPet(_dog);
        _cat.SetEdgePeekPose(true, fromLeft);
        _dog.SetEdgePeekPose(true, fromLeft);
        _cat.Speak(Pick("嘘，小耶跟紧一点。", "我们藏好啦。", "先别出声。"), 2300);
        _dog.Speak(Pick("我在小欧下面～", "一上一下，刚刚好！", "嘿嘿，偷偷看一眼。"), 2300);
        await Task.WhenAll(
            GlideTo(_cat, hiddenLeft, groupTop, 900, catVersion),
            GlideTo(_dog, hiddenLeft, groupTop + ScalePixels(145), 900, dogVersion));
        if (!_isPairActivity) return;
        _cat.Burst("…", Color.FromRgb(124, 137, 158));
        _dog.Burst("…", Color.FromRgb(124, 137, 158));
        await Task.Delay(Random.Shared.Next(2600, 4100));
        if (!_isPairActivity) return;
        _cat.SetEdgePeekPose(false, fromLeft);
        _dog.SetEdgePeekPose(false, fromLeft);
        var emergeLeft = fromLeft ? area.Left + ScalePixels(10) : area.Right - _cat.PixelWidth - ScalePixels(10);
        await Task.WhenAll(
            GlideTo(_cat, emergeLeft, groupTop, 650, catVersion),
            GlideTo(_dog, emergeLeft, groupTop + ScalePixels(145), 650, dogVersion));
        _cat.Hop(hearts: true);
        _dog.Hop(hearts: true);
        EndPairActivity();
    }

    private static async Task GlideTo(PetWindow pet, double targetLeft, double targetTop, int milliseconds, int activityVersion)
    {
        var startLeft = pet.Position.X;
        var startTop = pet.Position.Y;
        pet.FaceDirection(targetLeft - startLeft);
        var steps = Math.Max(12, milliseconds / 35);
        for (var step = 1; step <= steps; step++)
        {
            if (!pet.IsBusy || pet.ActivityVersion != activityVersion) return;
            var progress = step / (double)steps;
            var eased = 1 - Math.Pow(1 - progress, 2);
            SetPosition(pet, startLeft + (targetLeft - startLeft) * eased, startTop + (targetTop - startTop) * eased);
            await Task.Delay(Math.Max(16, milliseconds / steps));
        }
    }

    private static int PreparePairPet(PetWindow pet)
    {
        pet.IsBusy = true;
        return ++pet.ActivityVersion;
    }

    private static void CancelActivity(PetWindow pet)
    {
        pet.ActivityVersion++;
        pet.IsBusy = false;
        pet.IgnoreActivityBounds = false;
        StopMotion(pet);
        pet.SetEdgePeekPose(false, true);
    }

    private static void StopMotion(PetWindow pet)
    {
        pet.MotionX = 0;
        pet.MotionY = 0;
    }

    private double DistanceBetweenPets()
    {
        var deltaX = _cat.Center.X - _dog.Center.X;
        var deltaY = _cat.Center.Y - _dog.Center.Y;
        return Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
    }

    private Area GetActivityArea() => _activityMode == ActivityMode.Focus ? GetFocusArea() : GetWorkArea();

    private Area GetFocusArea()
    {
        var workArea = GetWorkArea();
        var width = Math.Min(ScalePixels(580), workArea.Width);
        var height = Math.Min(ScalePixels(380), workArea.Height);
        var defaultCenter = new Point(workArea.Right - width / 2, workArea.Bottom - height / 2);
        var anchor = _focusAnchor ?? defaultCenter;
        var left = Math.Clamp(anchor.X - width / 2, workArea.Left, workArea.Right - width);
        var top = Math.Clamp(anchor.Y - height / 2, workArea.Top, workArea.Bottom - height);
        return new Area(left, top, width, height);
    }

    private Area GetWorkArea()
    {
        var bounds = _cat.Screens.ScreenFromPoint(_cat.Position)?.WorkingArea
                     ?? _cat.Screens.Primary?.WorkingArea
                     ?? new PixelRect(0, 0, 1440, 900);
        return new Area(bounds.X, bounds.Y, bounds.Width, bounds.Height);
    }

    private void ClampToActivityArea(PetWindow pet)
    {
        ClampPetToArea(pet, GetActivityArea(), _activityMode == ActivityMode.FullScreen);
    }

    private void ClampPetToArea(PetWindow pet, Area area, bool allowSidePeek)
    {
        var sidePeek = allowSidePeek ? pet.PixelWidth * .18 : 0;
        SetPosition(pet,
            Math.Clamp(pet.Position.X, area.Left - sidePeek, area.Right - pet.PixelWidth + sidePeek),
            Math.Clamp(pet.Position.Y, area.Top, area.Bottom - pet.PixelHeight + ScalePixels(24)));
    }

    private static bool IsInsideArea(PetWindow pet, Area area)
    {
        return pet.Position.X >= area.Left && pet.Position.Y >= area.Top &&
               pet.Position.X + pet.PixelWidth <= area.Right && pet.Position.Y + pet.PixelHeight <= area.Bottom + 24 * pet.RenderScaling;
    }

    private double ScalePixels(double value) => value * Math.Max(1, _cat.RenderScaling);

    private static void SetPosition(Window window, double left, double top)
    {
        window.Position = new PixelPoint((int)Math.Round(left), (int)Math.Round(top));
    }

    private static string Pick(params string[] values) => values[Random.Shared.Next(values.Length)];

    private void CreateTrayIcon()
    {
        using var stream = AssetLoader.Open(new Uri("avares://PetFriends.Mac/Assets/cat.png"));
        var menu = new NativeMenu();
        menu.Items.Add(NativeMenuItem("叫回桌面", BringBack));
        menu.Items.Add(NativeMenuItem("专注陪伴模式", () => SetActivityMode(ActivityMode.Focus)));
        menu.Items.Add(NativeMenuItem("全屏撒欢模式", () => SetActivityMode(ActivityMode.FullScreen)));
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(NativeMenuItem("让他们贴贴", GatherAndCuddle));
        menu.Items.Add(NativeMenuItem("随机互动", TriggerRandomPairInteraction));
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(NativeMenuItem("退出", Exit));

        _tray = new TrayIcon
        {
            Icon = new WindowIcon(stream),
            ToolTipText = "小欧公爵和小耶牧师",
            Menu = menu,
            IsVisible = true
        };
        _tray.Clicked += (_, _) => BringBack();
    }

    private static NativeMenuItem NativeMenuItem(string header, Action action)
    {
        var item = new NativeMenuItem(header);
        item.Click += (_, _) => action();
        return item;
    }

    private void BringBack()
    {
        if (_isCuddling) return;
        CancelActivity(_cat);
        CancelActivity(_dog);
        _isPairActivity = false;
        var area = GetActivityArea();
        SetPosition(_cat, area.Right - _cat.PixelWidth * 2 - ScalePixels(55), area.Bottom - _cat.PixelHeight + ScalePixels(24));
        SetPosition(_dog, area.Right - _dog.PixelWidth - ScalePixels(18), area.Bottom - _dog.PixelHeight + ScalePixels(24));
        _cat.Show();
        _dog.Show();
        _cat.Speak("我们回来啦！");
        _dog.Hop(hearts: true);
    }

    private void Exit()
    {
        if (_exiting) return;
        _exiting = true;
        _motionTimer.Stop();
        _lifeTimer.Stop();
        _interactionTimer.Stop();
        _proximityTimer.Stop();
        if (_tray is not null)
        {
            _tray.IsVisible = false;
            _tray.Dispose();
        }
        _cat.Close();
        _dog.Close();
        _desktop.Shutdown();
    }
}
