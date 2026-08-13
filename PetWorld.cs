using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;
using Point = System.Windows.Point;

namespace PetFriends;

internal sealed class PetWorld
{
    private enum ActivityMode
    {
        Focus,
        FullScreen
    }

    private const double BasePetSize = 160;
    private readonly PetWindow _cat = new("小欧公爵", "cat.png", BasePetSize);
    private readonly PetWindow _dog = new("小耶牧师", "dog.png", BasePetSize);
    private readonly DispatcherTimer _motionTimer = new() { Interval = TimeSpan.FromMilliseconds(Compat.IsLegacyWindows ? 67 : 40) };
    private readonly DispatcherTimer _lifeTimer = new() { Interval = TimeSpan.FromSeconds(4) };
    private readonly DispatcherTimer _interactionTimer = new() { Interval = TimeSpan.FromSeconds(7) };
    private readonly DispatcherTimer _proximityTimer = new() { Interval = TimeSpan.FromMilliseconds(600) };
    private readonly string[] _catLines =
    {
        "本公爵允许你摸一下。", "今天也要优雅地发呆。", "小耶，你的眼镜歪啦。", "窗外有没有小鸟？", "再摸一下也不是不行……",
        "忙累了就休息一下。", "本公爵在这里陪着。", "今天的空气还不错。", "小耶又跑到哪里去了？", "要保持从容。",
        "刚才好像听见小鸟叫。", "这个位置很舒服。", "偶尔发呆也很重要。", "本公爵正在认真观察。", "你的工作完成多少了？",
        "先喝口水再继续。", "小耶应该快来找我了。", "安静待着也很好。", "今天也有好好努力。", "再伸个懒腰吧。"
    };
    private readonly string[] _dogLines =
    {
        "愿今天有好多好心情～", "小欧，等等我呀！", "要不要休息一下？", "发现一位认真工作的人！", "摸摸可以补充元气～",
        "今天也要笑一笑～", "我会乖乖陪着你的。", "小欧现在在想什么呢？", "空气里有好心情的味道！", "休息一分钟也可以哦。",
        "要不要一起伸个懒腰？", "我刚刚看见一朵云～", "这里暖暖的，好舒服。", "认真工作的人最闪亮！", "小欧一定又在装酷。",
        "有需要就叫我呀。", "偷偷送你一点元气～", "今天也会顺顺利利！", "好像闻到了点心的香味。", "再坚持一下就好啦。"
    };
    private Forms.NotifyIcon? _tray;
    private bool _quiet;
    private bool _isCuddling;
    private bool _isPairActivity;
    private bool _wereClose;
    private ActivityMode _activityMode = ActivityMode.Focus;
    private Point? _focusAnchor;
    private double _scale = 1;
    private DateTime _nextInteraction = DateTime.UtcNow.AddSeconds(5);
    private DateTime _nextAdventure = DateTime.UtcNow.AddSeconds(8);
    private IntPtr _lastHostWindow;

    public void Start()
    {
        var area = GetFocusArea();
        _cat.Left = area.Right - _cat.Width * 2 - 90;
        _dog.Left = area.Right - _dog.Width - 28;
        _cat.Top = area.Bottom - _cat.Height + 24;
        _dog.Top = area.Bottom - _dog.Height + 24;

        Configure(_cat);
        Configure(_dog);
        _cat.Show();
        _dog.Show();
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
        pet.MotionX = 0;
        pet.MotionY = 0;
        pet.Hop(hearts: true);
        var line = pet == _cat
            ? Pick("嗯……手法尚可。", "呼噜……再来一下。", "今日份宠爱，收到！", "本公爵心情+1 ♥", "耳朵也可以摸一下。", "勉强再陪你一会儿。", "这次摸得很舒服。", "本公爵记住你啦。")
            : Pick("好舒服～谢谢你！", "汪呜！元气满满！", "再摸摸耳朵嘛～", "送你一个小爪印 ♥", "再摸一下好不好？", "最喜欢温柔的摸摸啦！", "开心得尾巴要摇起来了～", "今天的元气补满啦！");
        pet.Speak(line);

        if (DistanceBetweenPets() < 205 && Compat.Random.NextDouble() < .42)
        {
            var other = pet == _cat ? _dog : _cat;
            other.Speak(pet == _cat ? "小耶也想要摸摸！" : "本公爵也在这里。", 2200);
            other.Wiggle();
        }
    }

    private void DragFinished(PetWindow pet)
    {
        if (pet.LastActionWasDrag)
        {
            _focusAnchor = pet.Center;
        }
        ClampToActivityArea(pet);
        pet.MotionX = 0;
        pet.MotionY = 0;
        if (_activityMode == ActivityMode.Focus && pet.LastActionWasDrag)
        {
            var other = pet == _cat ? _dog : _cat;
            FollowToFocusArea(other);
        }
        if (DistanceBetweenPets() < 185 && !_quiet && !_isCuddling && !_isPairActivity)
        {
            StopMotion(_cat);
            StopMotion(_dog);
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
                var wasManualFullScreenRun = pet.IgnoreActivityBounds;
                pet.IgnoreActivityBounds = false;
                StopMotion(pet);
                if (wasManualFullScreenRun && _activityMode == ActivityMode.Focus)
                {
                    FollowToFocusArea(pet);
                }
                continue;
            }
            var area = pet.IgnoreActivityBounds ? SystemParameters.WorkArea : GetActivityArea();
            var nextLeft = pet.Left + pet.MotionX;
            var nextTop = pet.Top + pet.MotionY;
            if (nextLeft <= area.Left - pet.Width * .18 || nextLeft >= area.Right - pet.Width * .82)
            {
                pet.MotionX *= -1;
                pet.FaceDirection(pet.MotionX);
            }
            if (nextTop <= area.Top || nextTop >= area.Bottom - pet.Height + 24)
            {
                pet.MotionY *= -1;
            }
            pet.Left += pet.MotionX;
            pet.Top += pet.MotionY;
            ClampPetToArea(pet, area, pet.IgnoreActivityBounds || _activityMode == ActivityMode.FullScreen);
        }
    }

    private void LifeTick(object? sender, EventArgs e)
    {
        if (_quiet || _isCuddling || _isPairActivity) return;
        var pet = Compat.Random.Next(2) == 0 ? _cat : _dog;
        if (pet.IsDragging || pet.IsBusy) return;
        var roll = Compat.Random.NextDouble();
        if (DateTime.UtcNow >= _nextAdventure && roll < .22)
        {
            _nextAdventure = DateTime.UtcNow.AddSeconds(Compat.Random.Next(14, 24));
            if (_activityMode == ActivityMode.Focus)
            {
                StartFreeRun(pet);
            }
            else
            {
                switch (Compat.Random.Next(5))
                {
                    case 0: StartFreeRun(pet); break;
                    case 1: HideAtScreenEdge(pet); break;
                    case 2: PerchOnWindow(pet); break;
                    case 3: HideBehindCurrentWindow(pet); break;
                    default: PeekFromCurrentWindowEdge(pet); break;
                }
            }
        }
        else if (roll < .36)
        {
            pet.Wiggle();
        }
        else if (roll < .53)
        {
            pet.Hop();
        }
        else if (roll < .73)
        {
            pet.Speak(Pick(pet == _cat ? _catLines : _dogLines), 2400);
        }
        else
        {
            StartFreeRun(pet);
        }
    }

    private void InteractionTick(object? sender, EventArgs e)
    {
        if (_quiet || _isCuddling || _isPairActivity || _cat.IsBusy || _dog.IsBusy || DateTime.UtcNow < _nextInteraction) return;
        _nextInteraction = DateTime.UtcNow.AddSeconds(Compat.Random.Next(10, 19));
        var distance = DistanceBetweenPets();
        if (distance < 220)
        {
            TriggerRandomPairInteraction();
        }
        else
        {
            GatherForInteraction();
        }
    }

    private async void GatherForInteraction()
    {
        if (!BeginPairActivity()) return;
        var area = GetActivityArea();
        var centerX = Compat.Clamp((_cat.Center.X + _dog.Center.X) / 2, area.Left + BasePetSize, area.Right - BasePetSize);
        var centerY = Compat.Clamp((_cat.Center.Y + _dog.Center.Y) / 2, area.Top + PetWindow.BubbleHeight, area.Bottom - BasePetSize / 2);
        _dog.Speak(Pick("小欧，我来找你啦～", "一起玩一会儿吧！", "小欧，靠近一点嘛。"), 2500);
        _cat.Speak(Pick("慢一点，本公爵在这里。", "正好，本公爵也想找你。", "过来吧，小耶。"), 2500);
        await Task.WhenAll(
            GlidePairPet(_cat, centerX - _cat.Width + 14, centerY - _cat.Height / 2, 880),
            GlidePairPet(_dog, centerX - 14, centerY - _dog.Height / 2, 880));
        EndPairActivity();
        TriggerRandomPairInteraction();
    }

    private static Task GlidePairPet(PetWindow pet, double left, double top, int milliseconds)
    {
        pet.IsBusy = true;
        pet.ActivityVersion++;
        return GlidePairPetCore(pet, left, top, milliseconds);
    }

    private static async Task GlidePairPetCore(PetWindow pet, double left, double top, int milliseconds)
    {
        await GlideTo(pet, left, top, milliseconds);
        pet.IsBusy = false;
    }

    private void ProximityTick(object? sender, EventArgs e)
    {
        TrackForegroundWindow();
        if (_quiet || _isCuddling || _isPairActivity || _cat.IsBusy || _dog.IsBusy || _cat.IsDragging || _dog.IsDragging) return;
        var close = DistanceBetweenPets() < 185;
        if (close && !_wereClose && DateTime.UtcNow >= _nextInteraction)
        {
            _nextInteraction = DateTime.UtcNow.AddSeconds(9);
            TriggerRandomPairInteraction();
        }
        _wereClose = close;
    }

    private void PairDialogue()
    {
        var dialogue = Compat.Random.Next(14);
        if (dialogue == 0)
        {
            _cat.Speak("小耶，今天也一起玩吧。", 3300);
            _dog.Speak("好呀！一直在一起～", 3300);
        }
        else if (dialogue == 1)
        {
            _dog.Speak("小欧，送你一颗星星！", 3300);
            _cat.Speak("那本公爵收下了。", 3300);
        }
        else if (dialogue == 2)
        {
            _cat.Speak("累了就靠过来吧。", 3300);
            _dog.Speak("嘿嘿，贴一下～", 3300);
        }
        else if (dialogue == 3)
        {
            _dog.Speak("小欧，你今天也很帅气！", 3300);
            _cat.Speak("眼光不错，小耶也很可爱。", 3300);
        }
        else if (dialogue == 4)
        {
            _cat.Speak("窗边的位置给你留着。", 3300);
            _dog.Speak("那我们一起晒太阳～", 3300);
        }
        else if (dialogue == 5)
        {
            _dog.Speak("偷偷告诉你：我很开心！", 3300);
            _cat.Speak("巧了，本公爵也是。", 3300);
        }
        else if (dialogue == 6)
        {
            _cat.Speak("今天的任务：好好休息。", 3300);
            _dog.Speak("遵命！休息也要认真～", 3300);
        }
        else if (dialogue == 7)
        {
            _dog.Speak("我们来比谁先眨眼！", 3300);
            _cat.Speak("……本公爵才没输。", 3300);
        }
        else if (dialogue == 8)
        {
            _cat.Speak("小耶，今天过得怎么样？", 3300);
            _dog.Speak("有小欧陪着，特别好～", 3300);
        }
        else if (dialogue == 9)
        {
            _dog.Speak("小欧，你会一直在吗？", 3300);
            _cat.Speak("当然，本公爵说话算话。", 3300);
        }
        else if (dialogue == 10)
        {
            _cat.Speak("你刚才是不是偷吃点心了？", 3300);
            _dog.Speak("只、只吃了一小口～", 3300);
        }
        else if (dialogue == 11)
        {
            _dog.Speak("我们一起给主人加油吧！", 3300);
            _cat.Speak("准了。今天也要顺利。", 3300);
        }
        else if (dialogue == 12)
        {
            _cat.Speak("小耶，别跑太远。", 3300);
            _dog.Speak("知道啦，我会回来找你～", 3300);
        }
        else
        {
            _dog.Speak("今天要不要多贴一会儿？", 3300);
            _cat.Speak("……可以，只多一会儿。", 3300);
        }
        _cat.Burst("♥", System.Windows.Media.Color.FromRgb(205, 96, 126));
        _dog.Burst("♥", System.Windows.Media.Color.FromRgb(205, 96, 126));
        _cat.Wiggle();
        _dog.Wiggle();
    }

    private void TriggerRandomPairInteraction()
    {
        CancelSoloActivity(_cat);
        CancelSoloActivity(_dog);
        switch (Compat.Random.Next(15))
        {
            case 0: BeginCuddle(); break;
            case 1: PairDialogue(); break;
            case 2: HighFive(); break;
            case 3: DanceTogether(); break;
            case 4: ShareSnack(); break;
            case 5: ComfortEachOther(); break;
            case 6: NapTogether(); break;
            case 7: PlayChase(); break;
            case 8: KissCheek(); break;
            case 9: TouchNoses(); break;
            case 10: RubCheeks(); break;
            case 11: HoldPaws(); break;
            case 12: WhisperSecret(); break;
            case 13: GroomEachOther(); break;
            default: ComplimentEachOther(); break;
        }
    }

    private async void KissCheek()
    {
        if (!BeginPairActivity()) return;
        await MoveCloseTogether(560);
        if (Compat.Random.Next(2) == 0)
        {
            _dog.Speak("小欧，靠近一点点～", 2000);
            await Task.Delay(520);
            _cat.Speak("啵！只许高兴，不许害羞。", 2800);
        }
        else
        {
            _cat.Speak("小耶，过来。", 1800);
            await Task.Delay(480);
            _dog.Speak("啵～送你一个脸颊亲亲！", 2800);
        }
        _cat.Burst("♥", System.Windows.Media.Color.FromRgb(235, 112, 145));
        _dog.Burst("♥", System.Windows.Media.Color.FromRgb(235, 112, 145));
        _cat.Hop(hearts: true);
        _dog.Wiggle();
        await Task.Delay(2500);
        EndPairActivity();
    }

    private async void TouchNoses()
    {
        if (!BeginPairActivity()) return;
        await MoveCloseTogether(520);
        _cat.Speak("碰碰鼻子。", 2200);
        _dog.Speak("碰到啦！好运传递～", 2600);
        _cat.BounceTwice("✦");
        _dog.BounceTwice("✦");
        await Task.Delay(2400);
        EndPairActivity();
    }

    private async void RubCheeks()
    {
        if (!BeginPairActivity()) return;
        await MoveCloseTogether(540);
        _dog.Speak("蹭蹭小欧～", 2600);
        _cat.Speak("……再蹭一下也可以。", 2800);
        for (var count = 0; count < 2; count++)
        {
            _cat.Wiggle();
            _dog.Wiggle();
            await Task.Delay(620);
        }
        _cat.Burst("♥", System.Windows.Media.Color.FromRgb(221, 133, 162));
        _dog.Burst("♥", System.Windows.Media.Color.FromRgb(221, 133, 162));
        await Task.Delay(1500);
        EndPairActivity();
    }

    private async void HoldPaws()
    {
        if (!BeginPairActivity()) return;
        await MoveCloseTogether(520);
        _cat.Speak("爪子给我，别走丢了。", 3000);
        _dog.Speak("牵好啦！一起走～", 3000);
        _cat.Burst("∞", System.Windows.Media.Color.FromRgb(113, 162, 195));
        _dog.Burst("∞", System.Windows.Media.Color.FromRgb(113, 162, 195));
        _cat.Hop();
        await Task.Delay(260);
        _dog.Hop();
        await Task.Delay(2400);
        EndPairActivity();
    }

    private async void WhisperSecret()
    {
        if (!BeginPairActivity()) return;
        await MoveCloseTogether(500);
        _dog.Speak("悄悄告诉你：我最喜欢和你玩。", 3400);
        await Task.Delay(1050);
        _cat.Speak("巧了，本公爵也是。要保密。", 3400);
        _cat.Burst("♥", System.Windows.Media.Color.FromRgb(220, 121, 151));
        _dog.Wiggle();
        await Task.Delay(2800);
        EndPairActivity();
    }

    private async void GroomEachOther()
    {
        if (!BeginPairActivity()) return;
        await MoveCloseTogether(520);
        _cat.Speak("别动，发饰有一点歪。", 3000);
        _dog.Speak("谢谢小欧！我也帮你理理毛～", 3200);
        _cat.Wiggle();
        await Task.Delay(480);
        _dog.Wiggle();
        _cat.Burst("✦", System.Windows.Media.Color.FromRgb(116, 165, 199));
        _dog.Burst("✦", System.Windows.Media.Color.FromRgb(116, 165, 199));
        await Task.Delay(2600);
        EndPairActivity();
    }

    private async void ComplimentEachOther()
    {
        if (!BeginPairActivity()) return;
        await MoveCloseTogether(460);
        var lines = Compat.Random.Next(3);
        if (lines == 0)
        {
            _dog.Speak("小欧今天特别帅气！", 3000);
            _cat.Speak("小耶今天也很可爱。", 3000);
        }
        else if (lines == 1)
        {
            _cat.Speak("你的笑容很有感染力。", 3000);
            _dog.Speak("因为看见小欧就开心呀～", 3000);
        }
        else
        {
            _dog.Speak("小欧总是很可靠！", 3000);
            _cat.Speak("有小耶在，本公爵更可靠。", 3000);
        }
        _cat.Burst("★", System.Windows.Media.Color.FromRgb(225, 171, 88));
        _dog.Burst("★", System.Windows.Media.Color.FromRgb(225, 171, 88));
        _cat.Hop();
        _dog.Hop();
        await Task.Delay(2900);
        EndPairActivity();
    }

    private async Task MoveCloseTogether(int milliseconds)
    {
        var area = GetActivityArea();
        var center = Compat.Clamp((_cat.Center.X + _dog.Center.X) / 2, area.Left + BasePetSize, area.Right - BasePetSize);
        var catTarget = center - _cat.Width + 14;
        var dogTarget = center - 14;
        var catStart = _cat.Left;
        var dogStart = _dog.Left;
        _cat.FaceDirection(1);
        _dog.FaceDirection(-1);
        const int steps = 12;
        for (var step = 1; step <= steps; step++)
        {
            var progress = step / (double)steps;
            var eased = 1 - Math.Pow(1 - progress, 2);
            _cat.Left = catStart + (catTarget - catStart) * eased;
            _dog.Left = dogStart + (dogTarget - dogStart) * eased;
            await Task.Delay(Math.Max(16, milliseconds / steps));
        }
    }

    private async void HighFive()
    {
        if (!BeginPairActivity()) return;
        _cat.Speak("配合得不错，击掌！", 2400);
        _dog.Speak("啪！今天也默契满分～", 2400);
        _cat.BounceTwice("★");
        _dog.BounceTwice("★");
        await Task.Delay(1700);
        EndPairActivity();
    }

    private async void DanceTogether()
    {
        if (!BeginPairActivity()) return;
        _cat.Speak("就跳一小会儿。", 2800);
        _dog.Speak("一、二、转圈圈～", 2800);
        for (var beat = 0; beat < 3; beat++)
        {
            _cat.BounceTwice("♪");
            await Task.Delay(240);
            _dog.BounceTwice("♫");
            await Task.Delay(620);
        }
        EndPairActivity();
    }

    private async void ShareSnack()
    {
        if (!BeginPairActivity()) return;
        _dog.Speak("这个小饼干分你一半！", 3000);
        _cat.Speak("……那本公爵也分你一半。", 3000);
        _cat.Burst("●", System.Windows.Media.Color.FromRgb(211, 166, 101));
        _dog.Burst("●", System.Windows.Media.Color.FromRgb(211, 166, 101));
        _cat.Hop();
        _dog.Hop();
        await Task.Delay(2400);
        _dog.Speak("一起吃果然更香～", 2200);
        EndPairActivity();
    }

    private async void ComfortEachOther()
    {
        if (!BeginPairActivity()) return;
        _dog.Speak("不开心的话，可以告诉我哦。", 3300);
        _cat.Speak("有你在，已经好多了。", 3300);
        _cat.Burst("♥", System.Windows.Media.Color.FromRgb(228, 129, 154));
        _dog.Burst("♥", System.Windows.Media.Color.FromRgb(228, 129, 154));
        _cat.Wiggle();
        _dog.Wiggle();
        await Task.Delay(3000);
        EndPairActivity();
    }

    private async void NapTogether()
    {
        if (!BeginPairActivity()) return;
        _cat.Speak("安静一点，午睡时间。", 3000);
        _dog.Speak("好～我靠着你睡。", 3000);
        _cat.Burst("Z", System.Windows.Media.Color.FromRgb(116, 146, 184));
        _dog.Burst("z", System.Windows.Media.Color.FromRgb(116, 146, 184));
        await Task.Delay(3600);
        _cat.Speak("……精神恢复。", 1800);
        _dog.Speak("睡得好香呀～", 1800);
        EndPairActivity();
    }

    private async void PlayChase()
    {
        if (!BeginPairActivity()) return;
        var area = GetActivityArea();
        var targetX = Compat.Clamp(_dog.Left + Compat.Random.Next(-340, 341), area.Left + 20, area.Right - _dog.Width - 20);
        var targetY = Compat.Clamp(_dog.Top + Compat.Random.Next(-260, 261), area.Top + 20, area.Bottom - _dog.Height);
        _dog.Speak("小欧，来追我呀！", 2300);
        _cat.Speak("站住，小耶！", 2300);
        StartRunToward(_dog, targetX, targetY, 2800);
        await Task.Delay(280);
        StartRunToward(_cat, targetX - 28, targetY + 18, 2800);
        await Task.Delay(2900);
        StopMotion(_cat);
        StopMotion(_dog);
        _dog.Speak("嘿嘿，被追到了～", 2100);
        _cat.Speak("这次算你跑得快。", 2100);
        EndPairActivity();
    }

    private bool BeginPairActivity()
    {
        if (_isCuddling || _isPairActivity) return false;
        CancelSoloActivity(_cat);
        CancelSoloActivity(_dog);
        _isPairActivity = true;
        StopMotion(_cat);
        StopMotion(_dog);
        _nextInteraction = DateTime.UtcNow.AddSeconds(9);
        return true;
    }

    private void EndPairActivity()
    {
        StopMotion(_cat);
        StopMotion(_dog);
        _isPairActivity = false;
    }

    private async void BeginCuddle()
    {
        if (_isCuddling || _isPairActivity) return;
        _isCuddling = true;
        _cat.MotionX = 0;
        _dog.MotionX = 0;
        var centerX = (_cat.Center.X + _dog.Center.X) / 2;
        var bottom = Math.Max(_cat.Top + _cat.Height, _dog.Top + _dog.Height);
        _cat.Hide();
        _dog.Hide();
        var cuddle = new CuddleWindow
        {
            Left = centerX - 160,
            Top = bottom - 295
        };
        var area = GetActivityArea();
        cuddle.Left = Compat.Clamp(cuddle.Left, area.Left, area.Right - cuddle.Width);
        cuddle.Top = Compat.Clamp(cuddle.Top, area.Top, area.Bottom - cuddle.Height);
        cuddle.Play(Pick("贴贴时间 ♥", "最喜欢和你一起！", "友情充电中……"));
        await Task.Delay(4300);
        cuddle.Close();
        _cat.Left = Compat.Clamp(centerX - _cat.Width + 26, area.Left, area.Right - _cat.Width);
        _dog.Left = Compat.Clamp(centerX - 25, area.Left, area.Right - _dog.Width);
        _cat.Top = Compat.Clamp(bottom - _cat.Height, area.Top, area.Bottom - _cat.Height + 24);
        _dog.Top = Compat.Clamp(bottom - _dog.Height, area.Top, area.Bottom - _dog.Height + 24);
        _cat.Show();
        _dog.Show();
        _cat.Hop(hearts: true);
        _dog.Hop(hearts: true);
        _isCuddling = false;
    }

    private ContextMenu BuildMenu(PetWindow pet)
    {
        var menu = new ContextMenu
        {
            FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei UI"),
            FontSize = 14
        };
        menu.Items.Add(MenuItem($"摸摸{pet.PetName}", (_, _) => Petted(pet)));
        menu.Items.Add(MenuItem("让他们贴贴", (_, _) => GatherAndCuddle()));
        menu.Items.Add(MenuItem("送一份小零食", (_, _) => FeedBoth()));

        var playMenu = new MenuItem { Header = "互动小游戏" };
        playMenu.Items.Add(MenuItem("随机互动", (_, _) => TriggerRandomPairInteraction()));
        playMenu.Items.Add(MenuItem("亲一下脸颊", (_, _) => KissCheek()));
        playMenu.Items.Add(MenuItem("碰碰鼻子", (_, _) => TouchNoses()));
        playMenu.Items.Add(MenuItem("蹭蹭脸", (_, _) => RubCheeks()));
        playMenu.Items.Add(MenuItem("牵爪", (_, _) => HoldPaws()));
        playMenu.Items.Add(MenuItem("说悄悄话", (_, _) => WhisperSecret()));
        playMenu.Items.Add(MenuItem("互相整理毛发", (_, _) => GroomEachOther()));
        playMenu.Items.Add(MenuItem("互相夸奖", (_, _) => ComplimentEachOther()));
        playMenu.Items.Add(new Separator());
        playMenu.Items.Add(MenuItem("击掌", (_, _) => HighFive()));
        playMenu.Items.Add(MenuItem("同步跳舞", (_, _) => DanceTogether()));
        playMenu.Items.Add(MenuItem("追逐游戏", (_, _) => PlayChase()));
        playMenu.Items.Add(MenuItem("分享点心", (_, _) => ShareSnack()));
        playMenu.Items.Add(MenuItem("互相安慰", (_, _) => ComfortEachOther()));
        playMenu.Items.Add(MenuItem("一起打盹", (_, _) => NapTogether()));
        playMenu.Items.Add(MenuItem("聊聊天", (_, _) => PairDialogue()));
        menu.Items.Add(playMenu);

        var activityModeMenu = new MenuItem { Header = "活动范围" };
        var focusItem = new MenuItem { Header = "专注陪伴（拖到哪里就在哪里）", IsCheckable = true, IsChecked = _activityMode == ActivityMode.Focus };
        var fullScreenItem = new MenuItem { Header = "全屏撒欢", IsCheckable = true, IsChecked = _activityMode == ActivityMode.FullScreen };
        focusItem.Click += (_, _) => SetActivityMode(ActivityMode.Focus);
        fullScreenItem.Click += (_, _) => SetActivityMode(ActivityMode.FullScreen);
        activityModeMenu.Items.Add(focusItem);
        activityModeMenu.Items.Add(fullScreenItem);
        menu.Items.Add(activityModeMenu);

        var roamMenu = new MenuItem { Header = "自由活动动作" };
        roamMenu.Items.Add(MenuItem("到处跑跑", (_, _) => StartFreeRun(pet, allowFullScreen: true)));
        roamMenu.Items.Add(MenuItem("躲到屏幕边缘", (_, _) => HideAtScreenEdge(pet, force: true)));
        roamMenu.Items.Add(MenuItem("坐到当前窗口上面", (_, _) => PerchOnWindow(pet, force: true)));
        roamMenu.Items.Add(MenuItem("躲到当前窗口后面", (_, _) => HideBehindCurrentWindow(pet, force: true)));
        roamMenu.Items.Add(MenuItem("从当前窗口边缘探头", (_, _) => PeekFromCurrentWindowEdge(pet, force: true)));
        menu.Items.Add(roamMenu);
        menu.Items.Add(new Separator());

        var quietItem = new MenuItem { Header = "安静模式", IsCheckable = true, IsChecked = _quiet };
        quietItem.Click += (_, _) =>
        {
            _quiet = quietItem.IsChecked;
            _cat.MotionX = _dog.MotionX = 0;
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

    private static MenuItem MenuItem(string header, RoutedEventHandler action)
    {
        var item = new MenuItem { Header = header };
        item.Click += action;
        return item;
    }

    private async void SetActivityMode(ActivityMode mode)
    {
        if (_activityMode == mode) return;
        _activityMode = mode;
        CancelSoloActivity(_cat);
        CancelSoloActivity(_dog);
        StopMotion(_cat);
        StopMotion(_dog);
        if (mode == ActivityMode.Focus)
        {
            if (_focusAnchor is null)
            {
                _focusAnchor = new Point((_cat.Center.X + _dog.Center.X) / 2, (_cat.Center.Y + _dog.Center.Y) / 2);
            }
            var area = GetFocusArea();
            _cat.Speak("专注陪伴模式：就在这里陪你。", 3200);
            _dog.Speak("我们会在你放的位置附近活动～", 3200);
            await Task.WhenAll(
                GlideToModeArea(_cat, area.Right - _cat.Width * 2 - 35, area.Bottom - _cat.Height + 24, 750),
                GlideToModeArea(_dog, area.Right - _dog.Width - 12, area.Bottom - _dog.Height + 24, 750));
        }
        else
        {
            _cat.Speak("全屏撒欢模式，出发！", 2600);
            _dog.Speak("可以到处探险啦～", 2600);
            _cat.Hop();
            _dog.Hop();
        }
    }

    private static async Task GlideToModeArea(PetWindow pet, double targetLeft, double targetTop, int milliseconds)
    {
        pet.IsBusy = true;
        pet.ActivityVersion++;
        await GlideTo(pet, targetLeft, targetTop, milliseconds);
        pet.IsBusy = false;
    }

    private async void FollowToFocusArea(PetWindow pet)
    {
        if (_activityMode != ActivityMode.Focus || pet.IsDragging) return;
        var area = GetFocusArea();
        if (IsInsideArea(pet, area)) return;
        CancelSoloActivity(pet);
        var targetLeft = Compat.Clamp(area.Right - pet.Width - 18, area.Left + 10, area.Right - pet.Width - 10);
        var targetTop = Compat.Clamp(area.Bottom - pet.Height + 24, area.Top + 10, area.Bottom - pet.Height + 24);
        pet.Speak(Pick("我也过来啦～", "等等我，一起待在这里。", "换到这边陪你。"), 2400);
        await GlideToModeArea(pet, targetLeft, targetTop, 760);
        ClampToActivityArea(pet);
    }

    private static bool IsInsideArea(PetWindow pet, Rect area)
    {
        return pet.Left >= area.Left && pet.Top >= area.Top &&
               pet.Left + pet.Width <= area.Right && pet.Top + pet.Height <= area.Bottom + 24;
    }

    private async void GatherAndCuddle()
    {
        if (_isCuddling || _isPairActivity) return;
        var area = GetActivityArea();
        var center = Compat.Clamp((_cat.Center.X + _dog.Center.X) / 2, area.Left + 190, area.Right - 190);
        _cat.Left = center - _cat.Width + 45;
        _dog.Left = center - 45;
        var top = area.Bottom - Math.Max(_cat.Height, _dog.Height) + 24;
        _cat.Top = top;
        _dog.Top = top;
        _cat.Speak("过来，小耶。", 1300);
        _dog.Speak("来啦来啦～", 1300);
        await Task.Delay(900);
        BeginCuddle();
    }

    private void FeedBoth()
    {
        _cat.MotionX = _dog.MotionX = 0;
        _cat.Speak("这份点心很合本公爵心意。", 3000);
        _dog.Speak("好吃！谢谢招待～", 3000);
        _cat.Hop(hearts: true);
        _dog.Hop(hearts: true);
    }

    private void SetScale(double scale)
    {
        if (Math.Abs(scale - _scale) < .01) return;
        foreach (var pet in new[] { _cat, _dog })
        {
            var bottom = pet.Top + pet.Height;
            var center = pet.Left + pet.Width / 2;
            pet.Width = BasePetSize * scale;
            pet.Height = (BasePetSize + PetWindow.BubbleHeight) * scale;
            pet.Left = center - pet.Width / 2;
            pet.Top = bottom - pet.Height;
            ClampToActivityArea(pet);
        }
        _scale = scale;
    }

    private void StartStroll(PetWindow pet, double direction, int milliseconds)
    {
        pet.MotionX = direction * 1.7;
        pet.MotionY = 0;
        pet.MotionUntil = DateTime.UtcNow.AddMilliseconds(milliseconds);
        pet.FaceDirection(direction);
    }

    private static void StartRunToward(PetWindow pet, double targetLeft, double targetTop, int milliseconds)
    {
        var deltaX = targetLeft - pet.Left;
        var deltaY = targetTop - pet.Top;
        var distance = Math.Max(1, Math.Sqrt(deltaX * deltaX + deltaY * deltaY));
        var speed = Compat.Clamp(distance / Math.Max(1, milliseconds / 40d), 1.2, 3.2);
        pet.MotionX = deltaX / distance * speed;
        pet.MotionY = deltaY / distance * speed;
        pet.MotionUntil = DateTime.UtcNow.AddMilliseconds(milliseconds);
        pet.FaceDirection(deltaX);
    }

    private void StartFreeRun(PetWindow pet, bool allowFullScreen = false)
    {
        if (pet.IsBusy || pet.IsDragging) return;
        var roamingArea = allowFullScreen ? SystemParameters.WorkArea : GetActivityArea();
        var angle = Compat.Random.NextDouble() * Math.PI * 2;
        var speed = 1.25 + Compat.Random.NextDouble() * 1.75;
        pet.MotionX = Math.Cos(angle) * speed;
        pet.MotionY = Math.Sin(angle) * speed * .72;
        if (Math.Abs(pet.MotionY) < .45)
        {
            pet.MotionY = Compat.Random.Next(2) == 0 ? -.75 : .75;
        }
        pet.MotionUntil = DateTime.UtcNow.AddMilliseconds(Compat.Random.Next(1800, 3800));
        if (allowFullScreen)
        {
            pet.IgnoreActivityBounds = true;
            var targetX = roamingArea.Left + Compat.Random.NextDouble() * Math.Max(1, roamingArea.Width - pet.Width);
            var targetY = roamingArea.Top + Compat.Random.NextDouble() * Math.Max(1, roamingArea.Height - pet.Height);
            StartRunToward(pet, targetX, targetY, Compat.Random.Next(2200, 4200));
        }
        pet.FaceDirection(pet.MotionX);
        pet.Speak(_activityMode == ActivityMode.Focus
            ? Pick("在这里陪你～", "小范围巡逻。", "不打扰你工作。")
            : Pick("去那边看看！", "巡逻时间～", "换个地方待一会儿。"), 1800);
    }

    private async void HideAtScreenEdge(PetWindow pet, bool force = false)
    {
        if (_activityMode != ActivityMode.FullScreen && !force)
        {
            pet.Speak("专注模式下就在这里陪你。", 2200);
            StartFreeRun(pet);
            return;
        }
        if (!BeginPairActivity()) return;
        var area = SystemParameters.WorkArea;
        var hideLeft = (_cat.Center.X + _dog.Center.X) / 2 < (area.Left + area.Right) / 2;
        var hiddenLeft = hideLeft ? area.Left - _cat.Width * .52 : area.Right - _cat.Width * .48;
        var groupTop = Compat.Clamp((_cat.Top + _dog.Top) / 2 - 80, area.Top + 24, area.Bottom - _dog.Height - 145);
        var catTop = groupTop;
        var dogTop = groupTop + 145;
        _cat.IsBusy = _dog.IsBusy = true;
        _cat.ActivityVersion++;
        _dog.ActivityVersion++;
        _cat.SetEdgePeekPose(true, hideLeft, showPaws: true, reverseLean: true);
        _dog.SetEdgePeekPose(true, hideLeft, showPaws: true, reverseLean: true);
        _cat.Speak(Pick("嘘，小耶跟紧一点。", "我们藏好啦。", "先别出声。"), 2300);
        _dog.Speak(Pick("我在小欧下面～", "一上一下，刚刚好！", "嘿嘿，偷偷看一眼。"), 2300);
        await Task.WhenAll(
            GlideTo(_cat, hiddenLeft, catTop, 900),
            GlideTo(_dog, hiddenLeft, dogTop, 900));
        if (!_isPairActivity || !_cat.IsBusy || !_dog.IsBusy)
        {
            _cat.SetEdgePeekPose(false, hideLeft);
            _dog.SetEdgePeekPose(false, hideLeft);
            _cat.IsBusy = _dog.IsBusy = false;
            EndPairActivity();
            return;
        }
        _cat.Burst("…", System.Windows.Media.Color.FromRgb(124, 137, 158));
        _dog.Burst("…", System.Windows.Media.Color.FromRgb(124, 137, 158));
        await Task.Delay(Compat.Random.Next(2600, 4100));
        if (!_isPairActivity || !_cat.IsBusy || !_dog.IsBusy)
        {
            _cat.SetEdgePeekPose(false, hideLeft);
            _dog.SetEdgePeekPose(false, hideLeft);
            _cat.IsBusy = _dog.IsBusy = false;
            EndPairActivity();
            return;
        }
        var emergeLeft = hideLeft ? area.Left + 10 : area.Right - _cat.Width - 10;
        _cat.Speak(Pick("本公爵先出来。", "被发现了吗？", "一起出来吧。"), 2000);
        _dog.Speak(Pick("小耶也出来啦～", "找到我们啦！", "登场～"), 2000);
        _cat.SetEdgePeekPose(false, hideLeft);
        _dog.SetEdgePeekPose(false, hideLeft);
        await Task.WhenAll(
            GlideTo(_cat, emergeLeft, catTop, 650),
            GlideTo(_dog, emergeLeft, dogTop, 650));
        _cat.IsBusy = _dog.IsBusy = false;
        _cat.Hop(hearts: true);
        _dog.Hop(hearts: true);
        EndPairActivity();
    }

    private async void PerchOnWindow(PetWindow pet, bool force = false)
    {
        if (_activityMode != ActivityMode.FullScreen && !force)
        {
            pet.Speak("切换到全屏撒欢，才能去窗口上坐哦。", 2600);
            return;
        }
        if (!BeginSoloActivity(pet)) return;
        var activityVersion = pet.ActivityVersion;
        if (!TryGetPreferredHostWindow(out var hostWindow, out var perch))
        {
            EndSoloActivity(pet);
            StartFreeRun(pet);
            return;
        }
        var workArea = SystemParameters.WorkArea;
        var targetX = Compat.Clamp(perch.Left + 28 + Compat.Random.NextDouble() * Math.Max(30, perch.Width - pet.Width - 56), workArea.Left, workArea.Right - pet.Width);
        var enoughRoomAbove = perch.Top - workArea.Top >= pet.Height * .52;
        var targetY = enoughRoomAbove
            ? perch.Top - pet.Height + 30
            : perch.Top - PetWindow.BubbleHeight + 8;
        pet.Speak(Pick("坐一下～", "这里好舒服。", "休息一小会儿～", "就待在这里吧。", "伸个懒腰～"), 2600);
        await GlideTo(pet, targetX, targetY, 1100);
        if (!IsSameActivity(pet, activityVersion)) return;
        pet.BounceTwice("★");
        await Task.Delay(Compat.Random.Next(3500, 6200));
        if (!IsSameActivity(pet, activityVersion)) return;
        pet.Speak(Pick("休息好啦！", "再活动一下。", "刚才好舒服～", "精神满满。"), 2000);
        EndSoloActivity(pet);
    }

    private async void HideBehindCurrentWindow(PetWindow pet, bool force = false)
    {
        if (_activityMode != ActivityMode.FullScreen && !force)
        {
            pet.Speak("切换到全屏撒欢，才会躲到应用后面哦。", 2600);
            return;
        }
        if (!BeginSoloActivity(pet)) return;
        var activityVersion = pet.ActivityVersion;
        if (!TryGetPreferredHostWindow(out var hostWindow, out var bounds))
        {
            pet.Speak("现在没有找到可以躲藏的应用窗口。", 2400);
            EndSoloActivity(pet);
            return;
        }
        var workArea = SystemParameters.WorkArea;
        var canReallyHide = bounds.Top - workArea.Top >= 58 && !IsZoomed(hostWindow);
        pet.Speak(Pick("我藏到窗口后面啦。", "嘘，只露一点点～", "猜猜我在哪里？"), 2500);
        if (canReallyHide)
        {
            var targetLeft = Compat.Clamp(bounds.Left + 36 + Compat.Random.NextDouble() * Math.Max(30, bounds.Width - pet.Width - 72), workArea.Left, workArea.Right - pet.Width);
            var targetTop = bounds.Top - 54;
            await GlideTo(pet, targetLeft, targetTop, 850);
            if (!IsSameActivity(pet, activityVersion)) return;
            pet.Topmost = false;
            SetWindowBehind(pet, hostWindow);
        }
        else
        {
            var targetLeft = Compat.Clamp(bounds.Left + 24 + Compat.Random.NextDouble() * Math.Max(30, bounds.Width - pet.Width - 48), workArea.Left, workArea.Right - pet.Width);
            var targetTop = Compat.Clamp(bounds.Top - PetWindow.BubbleHeight, workArea.Top - PetWindow.BubbleHeight, workArea.Bottom - pet.Height);
            await GlideTo(pet, targetLeft, targetTop, 800);
            if (!IsSameActivity(pet, activityVersion)) return;
            pet.SetHeadPeek(true);
            pet.Topmost = true;
        }
        pet.Burst("…", System.Windows.Media.Color.FromRgb(122, 136, 158));
        await Task.Delay(Compat.Random.Next(2800, 4600));
        if (!IsSameActivity(pet, activityVersion)) return;
        RestorePetLayer(pet);
        pet.Speak("找到我啦！", 1900);
        pet.Hop(hearts: true);
        EndSoloActivity(pet);
    }

    private async void PeekFromCurrentWindowEdge(PetWindow pet, bool force = false)
    {
        if (_activityMode != ActivityMode.FullScreen && !force)
        {
            pet.Speak("全屏撒欢时才会自动去应用旁边探头哦。", 2400);
            return;
        }
        if (!BeginPairActivity()) return;
        if (!TryGetPreferredHostWindow(out var hostWindow, out var bounds))
        {
            _cat.Speak("现在没有找到可以探头的地方。", 2200);
            _dog.Speak("那我们晚一点再一起去看看～", 2200);
            EndPairActivity();
            return;
        }

        var workArea = SystemParameters.WorkArea;
        var leftRoom = Math.Max(0, bounds.Left - workArea.Left);
        var rightRoom = Math.Max(0, workArea.Right - bounds.Right);
        var pairCenterX = (_cat.Center.X + _dog.Center.X) / 2;
        var windowCenterX = (bounds.Left + bounds.Right) / 2d;

        // Prefer the edge with more visible space outside the host window. When both
        // sides are similar, use the side the pair is already closer to.
        var fromLeft = Math.Abs(leftRoom - rightRoom) > 28
            ? leftRoom > rightRoom
            : pairCenterX <= windowCenterX;
        var exteriorRoom = fromLeft ? leftRoom : rightRoom;
        var useNaturalWindowOcclusion = exteriorRoom >= 18 && !IsZoomed(hostWindow);

        // Behind a normal host window, only this outside sliver remains visible.
        // If the host reaches the screen edge, the screen boundary supplies the
        // equivalent natural occlusion while the complete artwork stays intact.
        var targetLeft = fromLeft
            ? bounds.Left - _cat.Width * .38
            : bounds.Right - _cat.Width * .62;
        if (!useNaturalWindowOcclusion)
        {
            targetLeft = fromLeft
                ? workArea.Left - _cat.Width * .64
                : workArea.Right - _cat.Width * .36;
        }

        var verticalGap = Compat.Clamp(bounds.Height * .22, 112, 138);
        var pairHeight = _dog.Height + verticalGap;
        var topMin = Math.Max(workArea.Top + 16, bounds.Top + 28);
        var topMax = Math.Min(workArea.Bottom - pairHeight - 12, bounds.Bottom - pairHeight - 28);
        double catTop;
        if (topMax >= topMin)
        {
            var preferredTop = bounds.Top + bounds.Height * .46 - pairHeight / 2;
            catTop = Compat.Clamp(preferredTop, topMin, topMax);
        }
        else
        {
            catTop = Compat.Clamp(
                bounds.Top + bounds.Height / 2d - pairHeight / 2d,
                workArea.Top + 8,
                Math.Max(workArea.Top + 8, workArea.Bottom - pairHeight - 8));
        }
        var dogTop = catTop + verticalGap;

        _cat.IsBusy = _dog.IsBusy = true;
        var catVersion = ++_cat.ActivityVersion;
        var dogVersion = ++_dog.ActivityVersion;
        _cat.SetEdgePeekPose(true, fromLeft, showPaws: true);
        _dog.SetEdgePeekPose(true, fromLeft, showPaws: true);
        _cat.Speak(Pick("小耶，跟本公爵一起看看。", "嘘，一上一下探个头。", "我们从这边悄悄看看。"), 2600);
        _dog.Speak(Pick("我在小欧下面～", "一起探头，不要被发现啦！", "小耶准备好啦～"), 2600);

        await Task.WhenAll(
            GlideTo(_cat, targetLeft, catTop, 880),
            GlideTo(_dog, targetLeft, dogTop, 880));
        if (!IsCurrentPairPeek(catVersion, dogVersion))
        {
            FinishPairPeek(fromLeft);
            return;
        }
        // GlideTo faces the travel direction; re-apply the final inward-looking
        // peek pose after arrival so both pets lean the correct way at this edge.
        _cat.SetEdgePeekPose(true, fromLeft, showPaws: true);
        _dog.SetEdgePeekPose(true, fromLeft, showPaws: true);

        if (useNaturalWindowOcclusion)
        {
            _cat.Topmost = _dog.Topmost = false;
            SetWindowBehind(_cat, hostWindow);
            SetWindowBehind(_dog, hostWindow);
        }
        _cat.Burst("…", System.Windows.Media.Color.FromRgb(126, 139, 162));
        _dog.Burst("…", System.Windows.Media.Color.FromRgb(126, 139, 162));
        await Task.Delay(Compat.Random.Next(2700, 4300));
        if (!IsCurrentPairPeek(catVersion, dogVersion))
        {
            FinishPairPeek(fromLeft);
            return;
        }

        RestorePetLayer(_cat);
        RestorePetLayer(_dog);
        _cat.SetEdgePeekPose(false, fromLeft);
        _dog.SetEdgePeekPose(false, fromLeft);
        _cat.Speak(Pick("一起出来吧。", "侦察完毕。", "被发现了吗？"), 2000);
        _dog.Speak(Pick("小耶也出来啦～", "我们看到你啦！", "探头行动完成～"), 2000);
        var emergeLeft = fromLeft
            ? Compat.Clamp(bounds.Left + 10, workArea.Left + 6, workArea.Right - _cat.Width - 6)
            : Compat.Clamp(bounds.Right - _cat.Width - 10, workArea.Left + 6, workArea.Right - _cat.Width - 6);
        await Task.WhenAll(
            GlideTo(_cat, emergeLeft, catTop, 600),
            GlideTo(_dog, emergeLeft, dogTop, 600));
        if (!IsCurrentPairPeek(catVersion, dogVersion))
        {
            FinishPairPeek(fromLeft);
            return;
        }
        _cat.Hop(hearts: true);
        _dog.Hop(hearts: true);
        FinishPairPeek(fromLeft);
    }

    private bool IsCurrentPairPeek(int catVersion, int dogVersion)
    {
        return _isPairActivity
            && IsSameActivity(_cat, catVersion)
            && IsSameActivity(_dog, dogVersion);
    }

    private void FinishPairPeek(bool fromLeft)
    {
        RestorePetLayer(_cat);
        RestorePetLayer(_dog);
        _cat.SetEdgePeekPose(false, fromLeft);
        _dog.SetEdgePeekPose(false, fromLeft);
        _cat.IsBusy = _dog.IsBusy = false;
        EndPairActivity();
        if (_activityMode == ActivityMode.Focus)
        {
            FollowToFocusArea(_cat);
            FollowToFocusArea(_dog);
        }
    }

    private static async Task GlideTo(PetWindow pet, double targetLeft, double targetTop, int milliseconds)
    {
        var startLeft = pet.Left;
        var startTop = pet.Top;
        var activityVersion = pet.ActivityVersion;
        pet.FaceDirection(targetLeft - startLeft);
        var steps = Math.Max(12, milliseconds / 35);
        for (var step = 1; step <= steps; step++)
        {
            if (!pet.IsBusy || pet.ActivityVersion != activityVersion) return;
            var progress = step / (double)steps;
            var eased = 1 - Math.Pow(1 - progress, 2);
            pet.Left = startLeft + (targetLeft - startLeft) * eased;
            pet.Top = startTop + (targetTop - startTop) * eased;
            await Task.Delay(Math.Max(16, milliseconds / steps));
        }
    }

    private static bool BeginSoloActivity(PetWindow pet)
    {
        if (pet.IsBusy || pet.IsDragging) return false;
        pet.IsBusy = true;
        pet.ActivityVersion++;
        StopMotion(pet);
        return true;
    }

    private static bool IsSameActivity(PetWindow pet, int activityVersion)
    {
        return pet.IsBusy && pet.ActivityVersion == activityVersion;
    }

    private void EndSoloActivity(PetWindow pet)
    {
        StopMotion(pet);
        pet.IsBusy = false;
        pet.SetEdgePeekPose(false, true);
        ClampToActivityArea(pet);
    }

    private static void CancelSoloActivity(PetWindow pet)
    {
        pet.ActivityVersion++;
        pet.IsBusy = false;
        StopMotion(pet);
        pet.SetEdgePeekPose(false, true);
        RestorePetLayer(pet);
    }

    private static void StopMotion(PetWindow pet)
    {
        pet.MotionX = 0;
        pet.MotionY = 0;
    }

    private void TrackForegroundWindow()
    {
        var foreground = GetForegroundWindow();
        if (IsUsableHostWindow(foreground))
        {
            _lastHostWindow = foreground;
        }
    }

    private bool TryGetPreferredHostWindow(out IntPtr window, out NativeRect rectangle)
    {
        TrackForegroundWindow();
        if (IsUsableHostWindow(_lastHostWindow) && TryGetVisibleWindowBounds(_lastHostWindow, out rectangle))
        {
            window = _lastHostWindow;
            return true;
        }
        var candidates = new List<NativeRect>();
        var handles = new List<IntPtr>();
        var shell = GetShellWindow();
        EnumWindows((candidate, _) =>
        {
            if (candidate == shell || !IsWindowVisible(candidate) || IsIconic(candidate)) return true;
            GetWindowThreadProcessId(candidate, out var processId);
            if (processId == (uint)Compat.ProcessId) return true;
            var length = GetWindowTextLength(candidate);
            if (length <= 0) return true;
            var title = new StringBuilder(length + 1);
            GetWindowText(candidate, title, title.Capacity);
            if (string.IsNullOrWhiteSpace(title.ToString())) return true;
            if (!TryGetVisibleWindowBounds(candidate, out var bounds)) return true;
            if (bounds.Width < 360 || bounds.Height < 220) return true;
            var area = SystemParameters.WorkArea;
            if (bounds.Right <= area.Left || bounds.Left >= area.Right || bounds.Bottom <= area.Top || bounds.Top >= area.Bottom) return true;
            candidates.Add(bounds);
            handles.Add(candidate);
            return true;
        }, IntPtr.Zero);
        if (candidates.Count == 0)
        {
            window = IntPtr.Zero;
            rectangle = default;
            return false;
        }
        var index = Compat.Random.Next(candidates.Count);
        window = handles[index];
        rectangle = candidates[index];
        _lastHostWindow = window;
        return true;
    }

    private static bool IsUsableHostWindow(IntPtr window)
    {
        if (window == IntPtr.Zero || window == GetShellWindow() || !IsWindowVisible(window) || IsIconic(window)) return false;
        GetWindowThreadProcessId(window, out var processId);
        if (processId == (uint)Compat.ProcessId) return false;
        return GetWindowTextLength(window) > 0 && TryGetVisibleWindowBounds(window, out var bounds) && bounds.Width >= 360 && bounds.Height >= 220;
    }

    private static bool TryGetVisibleWindowBounds(IntPtr window, out NativeRect bounds)
    {
        var hr = DwmGetWindowAttribute(window, DwmwaExtendedFrameBounds, out var physicalBounds, Marshal.SizeOf<NativeRect>());
        if (hr != 0 || physicalBounds.Width <= 0 || physicalBounds.Height <= 0)
        {
            if (!GetWindowRect(window, out physicalBounds))
            {
                bounds = default;
                return false;
            }
        }
        var dpi = TryGetWindowDpi(window);
        var scale = dpi > 0 ? dpi / 96d : 1d;
        bounds = new NativeRect
        {
            Left = (int)Math.Round(physicalBounds.Left / scale),
            Top = (int)Math.Round(physicalBounds.Top / scale),
            Right = (int)Math.Round(physicalBounds.Right / scale),
            Bottom = (int)Math.Round(physicalBounds.Bottom / scale)
        };
        return true;
    }

    private static uint TryGetWindowDpi(IntPtr window)
    {
        // GetDpiForWindow was introduced in Windows 10 1607. Calling it on
        // Windows 7 throws EntryPointNotFoundException, so use 96 DPI there.
        if (Compat.IsLegacyWindows) return 96;
        try
        {
            return GetDpiForWindow(window);
        }
        catch (EntryPointNotFoundException)
        {
            return 96;
        }
    }

    private static void SetWindowBehind(PetWindow pet, IntPtr hostWindow)
    {
        var petHandle = new WindowInteropHelper(pet).Handle;
        SetWindowPos(petHandle, hostWindow, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
    }

    private static void RestorePetLayer(PetWindow pet)
    {
        if (!pet.IsLoaded) return;
        pet.SetHeadPeek(false);
        pet.SetEdgePeekPose(false, true);
        pet.Topmost = true;
        var petHandle = new WindowInteropHelper(pet).Handle;
        SetWindowPos(petHandle, HwndTopmost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
    }

    private double DistanceBetweenPets()
    {
        return (_cat.Center - _dog.Center).Length;
    }

    private Rect GetActivityArea()
    {
        return _activityMode == ActivityMode.Focus ? GetFocusArea() : SystemParameters.WorkArea;
    }

    private Rect GetFocusArea()
    {
        var workArea = SystemParameters.WorkArea;
        var width = Math.Min(580, workArea.Width);
        var height = Math.Min(380, workArea.Height);
        var defaultCenter = new Point(workArea.Right - width / 2, workArea.Bottom - height / 2);
        var anchor = _focusAnchor ?? defaultCenter;
        var left = Compat.Clamp(anchor.X - width / 2, workArea.Left, workArea.Right - width);
        var top = Compat.Clamp(anchor.Y - height / 2, workArea.Top, workArea.Bottom - height);
        return new Rect(left, top, width, height);
    }

    private void ClampToActivityArea(PetWindow pet)
    {
        var area = GetActivityArea();
        ClampPetToArea(pet, area, _activityMode == ActivityMode.FullScreen);
    }

    private static void ClampPetToArea(PetWindow pet, Rect area, bool allowSidePeek)
    {
        var sidePeek = allowSidePeek ? pet.Width * .18 : 0;
        pet.Left = Compat.Clamp(pet.Left, area.Left - sidePeek, area.Right - pet.Width + sidePeek);
        pet.Top = Compat.Clamp(pet.Top, area.Top, area.Bottom - pet.Height + 24);
    }

    private static string Pick(params string[] values) => values[Compat.Random.Next(values.Length)];

    private delegate bool EnumWindowsDelegate(IntPtr window, IntPtr parameter);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsDelegate callback, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] private static extern IntPtr GetShellWindow();
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr window, StringBuilder text, int maxCount);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowTextLength(IntPtr window);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out NativeRect rectangle);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll")] private static extern bool IsZoomed(IntPtr window);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out NativeRect value, int size);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr window);

    private static readonly IntPtr HwndTopmost = new(-1);
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private const int DwmwaExtendedFrameBounds = 9;

    private void CreateTrayIcon()
    {
        _tray = new Forms.NotifyIcon
        {
            Icon = Icon.ExtractAssociatedIcon(Compat.ProcessPath),
            Text = "小欧公爵和小耶牧师",
            Visible = true
        };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("叫回桌面", null, (_, _) => System.Windows.Application.Current.Dispatcher.Invoke(BringBack));
        menu.Items.Add("专注陪伴模式", null, (_, _) => System.Windows.Application.Current.Dispatcher.Invoke(() => SetActivityMode(ActivityMode.Focus)));
        menu.Items.Add("全屏撒欢模式", null, (_, _) => System.Windows.Application.Current.Dispatcher.Invoke(() => SetActivityMode(ActivityMode.FullScreen)));
        menu.Items.Add("让他们贴贴", null, (_, _) => System.Windows.Application.Current.Dispatcher.Invoke(GatherAndCuddle));
        menu.Items.Add("亲一下脸颊", null, (_, _) => System.Windows.Application.Current.Dispatcher.Invoke(KissCheek));
        menu.Items.Add("随机互动", null, (_, _) => System.Windows.Application.Current.Dispatcher.Invoke(TriggerRandomPairInteraction));
        menu.Items.Add("退出", null, (_, _) => System.Windows.Application.Current.Dispatcher.Invoke(Exit));
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => System.Windows.Application.Current.Dispatcher.Invoke(BringBack);
    }

    private void BringBack()
    {
        if (_isCuddling || _isPairActivity) return;
        var area = GetActivityArea();
        _cat.Left = area.Right - _cat.Width * 2 - 65;
        _dog.Left = area.Right - _dog.Width - 20;
        _cat.Top = area.Bottom - _cat.Height + 24;
        _dog.Top = area.Bottom - _dog.Height + 24;
        _cat.Show();
        _dog.Show();
        _cat.Speak("我们回来啦！");
        _dog.Hop(hearts: true);
    }

    private void Exit()
    {
        _motionTimer.Stop();
        _lifeTimer.Stop();
        _interactionTimer.Stop();
        _proximityTimer.Stop();
        if (_tray is not null)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }
        _cat.Close();
        _dog.Close();
        System.Windows.Application.Current.Shutdown();
    }
}
