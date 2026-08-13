#if NET35
using System;
using System.Collections.Generic;
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

namespace PetFriends
{
    internal sealed class PetWorld
    {
        private enum ActivityMode
        {
            Focus,
            FullScreen
        }

        private const double BasePetSize = 160;
        private readonly PetWindow _cat = new PetWindow("????", "cat.png", BasePetSize);
        private readonly PetWindow _dog = new PetWindow("????", "dog.png", BasePetSize);
        private readonly LegacyScheduler _scheduler = new LegacyScheduler();
        private readonly DispatcherTimer _motionTimer = new DispatcherTimer();
        private readonly DispatcherTimer _lifeTimer = new DispatcherTimer();
        private readonly DispatcherTimer _interactionTimer = new DispatcherTimer();
        private readonly DispatcherTimer _proximityTimer = new DispatcherTimer();
        private readonly string[] _catLines =
        {
            "??????????", "??????????", "??????????", "????????", "???????????",
            "?????????", "?????????", "?????????", "??????????", "??????",
            "????????", "?????????", "????????", "?????????", "???????"
        };
        private readonly string[] _dogLines =
        {
            "??????????", "????????", "????????", "???????????", "?????????",
            "????????", "?????????", "??????????", "???????????", "??????????",
            "??????????", "??????????", "????????", "?????????", "?????????"
        };

        private Forms.NotifyIcon _tray;
        private Forms.ToolStripMenuItem _trayAutostartItem;
        private CuddleWindow _cuddleWindow;
        private bool _quiet;
        private bool _isCuddling;
        private bool _isPairActivity;
        private bool _wereClose;
        private int _pairToken;
        private ActivityMode _activityMode = ActivityMode.Focus;
        private Point? _focusAnchor;
        private double _scale = 1;
        private DateTime _nextInteraction = DateTime.UtcNow.AddSeconds(5);
        private DateTime _nextAdventure = DateTime.UtcNow.AddSeconds(8);
        private IntPtr _lastHostWindow;

        public PetWorld()
        {
            _motionTimer.Interval = TimeSpan.FromMilliseconds(67);
            _lifeTimer.Interval = TimeSpan.FromSeconds(4);
            _interactionTimer.Interval = TimeSpan.FromSeconds(7);
            _proximityTimer.Interval = TimeSpan.FromMilliseconds(700);
        }

        public void Start()
        {
            Rect area = GetFocusArea();
            _cat.Left = area.Right - _cat.Width * 2 - 90;
            _dog.Left = area.Right - _dog.Width - 28;
            _cat.Top = area.Bottom - _cat.Height + 24;
            _dog.Top = area.Bottom - _dog.Height + 24;

            Configure(_cat);
            Configure(_dog);
            _cat.Show();
            _dog.Show();
            _cat.Speak("???????????", 3400);
            _dog.Speak("????????????", 3400);

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
            pet.ActivityCancelled = CancelForUser;
            pet.MenuFactory = BuildMenu;
        }

        private void CancelForUser(PetWindow pet)
        {
            if (_isPairActivity)
            {
                _pairToken++;
                _isPairActivity = false;
                _cat.IsBusy = false;
                _dog.IsBusy = false;
                RestorePetLayer(_cat);
                RestorePetLayer(_dog);
            }
            if (pet.IsBusy) EndSoloActivity(pet);
        }

        private void Petted(PetWindow pet)
        {
            if (_isCuddling || _isPairActivity) return;
            StopMotion(pet);
            pet.Hop(true);
            string line = pet == _cat
                ? Pick("????????", "?????????", "?????????", "?????+1 ?", "?????????")
                : Pick("????????", "????????", "???????", "??????? ?", "??????????");
            pet.Speak(line);
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
            if (DistanceBetweenPets() < 185 && !_quiet && !_isCuddling && !_isPairActivity)
            {
                _nextInteraction = DateTime.UtcNow.AddSeconds(9);
                TriggerRandomPairInteraction();
            }
        }

        private void MovePets(object sender, EventArgs e)
        {
            if (_quiet || _isCuddling) return;
            PetWindow[] pets = { _cat, _dog };
            for (int index = 0; index < pets.Length; index++)
            {
                PetWindow pet = pets[index];
                if (pet.IsDragging || pet.IsBusy || (Math.Abs(pet.MotionX) < .05 && Math.Abs(pet.MotionY) < .05)) continue;
                if (DateTime.UtcNow > pet.MotionUntil)
                {
                    bool returnToFocus = pet.IgnoreActivityBounds;
                    pet.IgnoreActivityBounds = false;
                    StopMotion(pet);
                    if (returnToFocus && _activityMode == ActivityMode.Focus) FollowToFocusArea(pet);
                    continue;
                }
                Rect area = pet.IgnoreActivityBounds ? SystemParameters.WorkArea : GetActivityArea();
                double nextLeft = pet.Left + pet.MotionX;
                double nextTop = pet.Top + pet.MotionY;
                if (nextLeft <= area.Left - pet.Width * .18 || nextLeft >= area.Right - pet.Width * .82)
                {
                    pet.MotionX *= -1;
                    pet.FaceDirection(pet.MotionX);
                }
                if (nextTop <= area.Top || nextTop >= area.Bottom - pet.Height + 24) pet.MotionY *= -1;
                pet.Left += pet.MotionX;
                pet.Top += pet.MotionY;
                ClampPetToArea(pet, area, pet.IgnoreActivityBounds || _activityMode == ActivityMode.FullScreen);
            }
        }

        private void LifeTick(object sender, EventArgs e)
        {
            if (_quiet || _isCuddling || _isPairActivity) return;
            PetWindow pet = Compat.Random.Next(2) == 0 ? _cat : _dog;
            if (pet.IsDragging || pet.IsBusy) return;
            double roll = Compat.Random.NextDouble();
            if (DateTime.UtcNow >= _nextAdventure && roll < .22)
            {
                _nextAdventure = DateTime.UtcNow.AddSeconds(Compat.Random.Next(14, 24));
                if (_activityMode == ActivityMode.Focus) StartFreeRun(pet, false);
                else
                {
                    switch (Compat.Random.Next(5))
                    {
                        case 0: StartFreeRun(pet, false); break;
                        case 1: HideAtScreenEdge(pet, false); break;
                        case 2: PerchOnWindow(pet, false); break;
                        case 3: HideBehindCurrentWindow(pet, false); break;
                        default: PeekFromCurrentWindowEdge(pet, false); break;
                    }
                }
            }
            else if (roll < .40) pet.Wiggle();
            else if (roll < .58) pet.Hop();
            else if (roll < .76) pet.Speak(Pick(pet == _cat ? _catLines : _dogLines), 2400);
            else StartFreeRun(pet, false);
        }

        private void InteractionTick(object sender, EventArgs e)
        {
            if (_quiet || _isCuddling || _isPairActivity || _cat.IsBusy || _dog.IsBusy || DateTime.UtcNow < _nextInteraction) return;
            _nextInteraction = DateTime.UtcNow.AddSeconds(Compat.Random.Next(10, 19));
            if (DistanceBetweenPets() < 220) TriggerRandomPairInteraction();
            else GatherForInteraction();
        }

        private void ProximityTick(object sender, EventArgs e)
        {
            TrackForegroundWindow();
            if (_quiet || _isCuddling || _isPairActivity || _cat.IsBusy || _dog.IsBusy || _cat.IsDragging || _dog.IsDragging) return;
            bool close = DistanceBetweenPets() < 185;
            if (close && !_wereClose && DateTime.UtcNow >= _nextInteraction)
            {
                _nextInteraction = DateTime.UtcNow.AddSeconds(9);
                TriggerRandomPairInteraction();
            }
            _wereClose = close;
        }

        private void GatherForInteraction()
        {
            int token;
            if (!BeginPairActivity(out token)) return;
            Rect area = GetActivityArea();
            double centerX = Compat.Clamp((_cat.Center.X + _dog.Center.X) / 2, area.Left + BasePetSize, area.Right - BasePetSize);
            double centerY = Compat.Clamp((_cat.Center.Y + _dog.Center.Y) / 2, area.Top + PetWindow.BubbleHeight, area.Bottom - BasePetSize / 2);
            _dog.Speak(Pick("?????????", "????????", "?????????"), 2500);
            _cat.Speak(Pick("???????????", "???????????", "???????"), 2500);
            GlidePair(centerX - _cat.Width + 14, centerY - _cat.Height / 2, centerX - 14, centerY - _dog.Height / 2, 880, token,
                delegate(bool completed)
                {
                    if (!completed || !IsPairCurrent(token)) return;
                    EndPairActivity(token);
                    TriggerRandomPairInteraction();
                });
        }

        private void TriggerRandomPairInteraction()
        {
            if (_isCuddling || _isPairActivity || _cat.IsBusy || _dog.IsBusy) return;
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

        private bool BeginPairActivity(out int token)
        {
            token = 0;
            if (_isCuddling || _isPairActivity || _cat.IsDragging || _dog.IsDragging) return false;
            CancelSoloActivity(_cat);
            CancelSoloActivity(_dog);
            _isPairActivity = true;
            token = ++_pairToken;
            _cat.IsBusy = true;
            _dog.IsBusy = true;
            _cat.ActivityVersion++;
            _dog.ActivityVersion++;
            StopMotion(_cat);
            StopMotion(_dog);
            _nextInteraction = DateTime.UtcNow.AddSeconds(9);
            return true;
        }

        private bool IsPairCurrent(int token)
        {
            return _isPairActivity && token == _pairToken && _cat.IsBusy && _dog.IsBusy;
        }

        private void EndPairActivity(int token)
        {
            if (token != _pairToken) return;
            StopMotion(_cat);
            StopMotion(_dog);
            _cat.IsBusy = false;
            _dog.IsBusy = false;
            _isPairActivity = false;
        }

        private void PairAfter(int token, int milliseconds, Action action)
        {
            _scheduler.After(milliseconds, delegate
            {
                if (IsPairCurrent(token)) action();
            });
        }

        private void MoveCloseTogether(int token, int milliseconds, Action<bool> completed)
        {
            Rect area = GetActivityArea();
            double center = Compat.Clamp((_cat.Center.X + _dog.Center.X) / 2, area.Left + BasePetSize, area.Right - BasePetSize);
            _cat.FaceDirection(1);
            _dog.FaceDirection(-1);
            GlidePair(center - _cat.Width + 14, _cat.Top, center - 14, _dog.Top, milliseconds, token, completed);
        }

        private void ClosePairEffect(int milliseconds, int holdMilliseconds, Action effect)
        {
            int token;
            if (!BeginPairActivity(out token)) return;
            MoveCloseTogether(token, milliseconds, delegate(bool completed)
            {
                if (!completed || !IsPairCurrent(token)) return;
                effect();
                PairAfter(token, holdMilliseconds, delegate { EndPairActivity(token); });
            });
        }

        private void PairDialogue()
        {
            int token;
            if (!BeginPairActivity(out token)) return;
            int dialogue = Compat.Random.Next(5);
            if (dialogue == 0)
            {
                _cat.Speak("???????????", 3300);
                _dog.Speak("?????????", 3300);
            }
            else if (dialogue == 1)
            {
                _dog.Speak("??????????", 3300);
                _cat.Speak("????????", 3300);
            }
            else if (dialogue == 2)
            {
                _cat.Speak("????????", 3300);
                _dog.Speak("???????", 3300);
            }
            else if (dialogue == 3)
            {
                _dog.Speak("???????????", 3300);
                _cat.Speak("??????????", 3300);
            }
            else
            {
                _dog.Speak("???????????", 3300);
                _cat.Speak("???????????", 3300);
            }
            _cat.Burst("?", System.Windows.Media.Color.FromRgb(205, 96, 126));
            _dog.Burst("?", System.Windows.Media.Color.FromRgb(205, 96, 126));
            _cat.Wiggle();
            _dog.Wiggle();
            PairAfter(token, 3400, delegate { EndPairActivity(token); });
        }

        private void KissCheek()
        {
            ClosePairEffect(560, 2700, delegate
            {
                _dog.Speak("???????????", 2800);
                _cat.Speak("??????????", 2800);
                _cat.Burst("?", System.Windows.Media.Color.FromRgb(235, 112, 145));
                _dog.Burst("?", System.Windows.Media.Color.FromRgb(235, 112, 145));
                _cat.Hop(true);
                _dog.Wiggle();
            });
        }

        private void TouchNoses()
        {
            ClosePairEffect(520, 2500, delegate
            {
                _cat.Speak("?????", 2200);
                _dog.Speak("?????????", 2600);
                _cat.BounceTwice("?");
                _dog.BounceTwice("?");
            });
        }

        private void RubCheeks()
        {
            ClosePairEffect(540, 2800, delegate
            {
                _dog.Speak("?????", 2600);
                _cat.Speak("??????????", 2800);
                _cat.Wiggle();
                _dog.Wiggle();
                _cat.Burst("?", System.Windows.Media.Color.FromRgb(221, 133, 162));
                _dog.Burst("?", System.Windows.Media.Color.FromRgb(221, 133, 162));
            });
        }

        private void HoldPaws()
        {
            ClosePairEffect(520, 2800, delegate
            {
                _cat.Speak("??????????", 3000);
                _dog.Speak("????????", 3000);
                _cat.Burst("?", System.Windows.Media.Color.FromRgb(113, 162, 195));
                _dog.Burst("?", System.Windows.Media.Color.FromRgb(113, 162, 195));
                _cat.Hop();
                _scheduler.After(260, delegate { _dog.Hop(); });
            });
        }

        private void WhisperSecret()
        {
            ClosePairEffect(500, 3600, delegate
            {
                _dog.Speak("??????????????", 3400);
                _scheduler.After(1000, delegate { _cat.Speak("?????????????", 3000); });
                _cat.Burst("?", System.Windows.Media.Color.FromRgb(220, 121, 151));
                _dog.Wiggle();
            });
        }

        private void GroomEachOther()
        {
            ClosePairEffect(520, 3100, delegate
            {
                _cat.Speak("??????????", 3000);
                _dog.Speak("?????????????", 3200);
                _cat.Wiggle();
                _scheduler.After(480, delegate { _dog.Wiggle(); });
                _cat.Burst("?", System.Windows.Media.Color.FromRgb(116, 165, 199));
                _dog.Burst("?", System.Windows.Media.Color.FromRgb(116, 165, 199));
            });
        }

        private void ComplimentEachOther()
        {
            ClosePairEffect(460, 3100, delegate
            {
                _dog.Speak("?????????", 3000);
                _cat.Speak("?????????", 3000);
                _cat.Burst("?", System.Windows.Media.Color.FromRgb(225, 171, 88));
                _dog.Burst("?", System.Windows.Media.Color.FromRgb(225, 171, 88));
                _cat.Hop();
                _dog.Hop();
            });
        }

        private void HighFive()
        {
            int token;
            if (!BeginPairActivity(out token)) return;
            _cat.Speak("?????????", 2400);
            _dog.Speak("??????????", 2400);
            _cat.BounceTwice("?");
            _dog.BounceTwice("?");
            PairAfter(token, 1800, delegate { EndPairActivity(token); });
        }

        private void DanceTogether()
        {
            int token;
            if (!BeginPairActivity(out token)) return;
            _cat.Speak("???????", 2800);
            _dog.Speak("????????", 2800);
            DanceBeat(token, 0);
        }

        private void DanceBeat(int token, int beat)
        {
            if (!IsPairCurrent(token)) return;
            if (beat >= 3)
            {
                EndPairActivity(token);
                return;
            }
            _cat.BounceTwice("?");
            PairAfter(token, 240, delegate
            {
                _dog.BounceTwice("?");
                PairAfter(token, 620, delegate { DanceBeat(token, beat + 1); });
            });
        }

        private void ShareSnack()
        {
            int token;
            if (!BeginPairActivity(out token)) return;
            _dog.Speak("??????????", 3000);
            _cat.Speak("????????????", 3000);
            _cat.Burst("?", System.Windows.Media.Color.FromRgb(211, 166, 101));
            _dog.Burst("?", System.Windows.Media.Color.FromRgb(211, 166, 101));
            _cat.Hop();
            _dog.Hop();
            PairAfter(token, 2400, delegate
            {
                _dog.Speak("????????", 2200);
                EndPairActivity(token);
            });
        }

        private void ComfortEachOther()
        {
            int token;
            if (!BeginPairActivity(out token)) return;
            _dog.Speak("?????????????", 3300);
            _cat.Speak("??????????", 3300);
            _cat.Burst("?", System.Windows.Media.Color.FromRgb(228, 129, 154));
            _dog.Burst("?", System.Windows.Media.Color.FromRgb(228, 129, 154));
            _cat.Wiggle();
            _dog.Wiggle();
            PairAfter(token, 3100, delegate { EndPairActivity(token); });
        }

        private void NapTogether()
        {
            int token;
            if (!BeginPairActivity(out token)) return;
            _cat.Speak("??????????", 3000);
            _dog.Speak("????????", 3000);
            _cat.Burst("Z", System.Windows.Media.Color.FromRgb(116, 146, 184));
            _dog.Burst("z", System.Windows.Media.Color.FromRgb(116, 146, 184));
            PairAfter(token, 3600, delegate
            {
                _cat.Speak("???????", 1800);
                _dog.Speak("??????", 1800);
                EndPairActivity(token);
            });
        }

        private void PlayChase()
        {
            int token;
            if (!BeginPairActivity(out token)) return;
            Rect area = GetActivityArea();
            double targetX = Compat.Clamp(_dog.Left + Compat.Random.Next(-340, 341), area.Left + 20, area.Right - _dog.Width - 20);
            double targetY = Compat.Clamp(_dog.Top + Compat.Random.Next(-260, 261), area.Top + 20, area.Bottom - _dog.Height);
            _dog.Speak("????????", 2300);
            _cat.Speak("??????", 2300);
            _cat.IsBusy = false;
            _dog.IsBusy = false;
            StartRunToward(_dog, targetX, targetY, 2800);
            _scheduler.After(280, delegate
            {
                if (token != _pairToken || !_isPairActivity) return;
                StartRunToward(_cat, targetX - 28, targetY + 18, 2800);
            });
            _scheduler.After(3100, delegate
            {
                if (token != _pairToken || !_isPairActivity) return;
                StopMotion(_cat);
                StopMotion(_dog);
                _cat.IsBusy = true;
                _dog.IsBusy = true;
                _dog.Speak("????????", 2100);
                _cat.Speak("????????", 2100);
                EndPairActivity(token);
            });
        }

        private void BeginCuddle()
        {
            if (_isCuddling || _isPairActivity || _cat.IsBusy || _dog.IsBusy) return;
            _isCuddling = true;
            int token = ++_pairToken;
            StopMotion(_cat);
            StopMotion(_dog);
            _cat.IsBusy = true;
            _dog.IsBusy = true;
            double centerX = (_cat.Center.X + _dog.Center.X) / 2;
            double bottom = Math.Max(_cat.Top + _cat.Height, _dog.Top + _dog.Height);
            _cat.Hide();
            _dog.Hide();
            CuddleWindow cuddle = new CuddleWindow();
            _cuddleWindow = cuddle;
            cuddle.Left = centerX - 160;
            cuddle.Top = bottom - 295;
            Rect area = GetActivityArea();
            cuddle.Left = Compat.Clamp(cuddle.Left, area.Left, area.Right - cuddle.Width);
            cuddle.Top = Compat.Clamp(cuddle.Top, area.Top, area.Bottom - cuddle.Height);
            cuddle.Play(Pick("???? ?", "????????", "???????"));
            _scheduler.After(4300, delegate
            {
                if (!_isCuddling || token != _pairToken) return;
                cuddle.Close();
                _cuddleWindow = null;
                _cat.Left = Compat.Clamp(centerX - _cat.Width + 26, area.Left, area.Right - _cat.Width);
                _dog.Left = Compat.Clamp(centerX - 25, area.Left, area.Right - _dog.Width);
                _cat.Top = Compat.Clamp(bottom - _cat.Height, area.Top, area.Bottom - _cat.Height + 24);
                _dog.Top = Compat.Clamp(bottom - _dog.Height, area.Top, area.Bottom - _dog.Height + 24);
                _cat.Show();
                _dog.Show();
                _cat.IsBusy = false;
                _dog.IsBusy = false;
                _cat.Hop(true);
                _dog.Hop(true);
                _isCuddling = false;
            });
        }

        private void GatherAndCuddle()
        {
            if (_isCuddling || _isPairActivity || _cat.IsBusy || _dog.IsBusy) return;
            Rect area = GetActivityArea();
            double center = Compat.Clamp((_cat.Center.X + _dog.Center.X) / 2, area.Left + 190, area.Right - 190);
            _cat.Left = center - _cat.Width + 45;
            _dog.Left = center - 45;
            double top = area.Bottom - Math.Max(_cat.Height, _dog.Height) + 24;
            _cat.Top = top;
            _dog.Top = top;
            _cat.Speak("??????", 1300);
            _dog.Speak("?????", 1300);
            _scheduler.After(900, BeginCuddle);
        }

        private void FeedBoth()
        {
            if (_isCuddling || _isPairActivity) return;
            StopMotion(_cat);
            StopMotion(_dog);
            _cat.Speak("????????????", 3000);
            _dog.Speak("????????", 3000);
            _cat.Hop(true);
            _dog.Hop(true);
        }

        private ContextMenu BuildMenu(PetWindow pet)
        {
            ContextMenu menu = new ContextMenu
            {
                FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei UI"),
                FontSize = 14
            };
            menu.Items.Add(CreateMenuItem("??" + pet.PetName, delegate { Petted(pet); }));
            menu.Items.Add(CreateMenuItem("?????", GatherAndCuddle));
            menu.Items.Add(CreateMenuItem("??????", FeedBoth));

            MenuItem playMenu = new MenuItem { Header = "?????" };
            playMenu.Items.Add(CreateMenuItem("????", TriggerRandomPairInteraction));
            playMenu.Items.Add(CreateMenuItem("?????", KissCheek));
            playMenu.Items.Add(CreateMenuItem("????", TouchNoses));
            playMenu.Items.Add(CreateMenuItem("???", RubCheeks));
            playMenu.Items.Add(CreateMenuItem("??", HoldPaws));
            playMenu.Items.Add(CreateMenuItem("????", WhisperSecret));
            playMenu.Items.Add(CreateMenuItem("??????", GroomEachOther));
            playMenu.Items.Add(CreateMenuItem("????", ComplimentEachOther));
            playMenu.Items.Add(new Separator());
            playMenu.Items.Add(CreateMenuItem("??", HighFive));
            playMenu.Items.Add(CreateMenuItem("????", DanceTogether));
            playMenu.Items.Add(CreateMenuItem("????", PlayChase));
            playMenu.Items.Add(CreateMenuItem("????", ShareSnack));
            playMenu.Items.Add(CreateMenuItem("????", ComfortEachOther));
            playMenu.Items.Add(CreateMenuItem("????", NapTogether));
            playMenu.Items.Add(CreateMenuItem("???", PairDialogue));
            menu.Items.Add(playMenu);

            MenuItem activityMenu = new MenuItem { Header = "????" };
            MenuItem focusItem = new MenuItem { Header = "??????????????", IsCheckable = true, IsChecked = _activityMode == ActivityMode.Focus };
            MenuItem fullItem = new MenuItem { Header = "????", IsCheckable = true, IsChecked = _activityMode == ActivityMode.FullScreen };
            focusItem.Click += delegate { SetActivityMode(ActivityMode.Focus); };
            fullItem.Click += delegate { SetActivityMode(ActivityMode.FullScreen); };
            activityMenu.Items.Add(focusItem);
            activityMenu.Items.Add(fullItem);
            menu.Items.Add(activityMenu);

            MenuItem roamMenu = new MenuItem { Header = "??????" };
            roamMenu.Items.Add(CreateMenuItem("????", delegate { StartFreeRun(pet, true); }));
            roamMenu.Items.Add(CreateMenuItem("??????", delegate { HideAtScreenEdge(pet, true); }));
            roamMenu.Items.Add(CreateMenuItem("????????", delegate { PerchOnWindow(pet, true); }));
            roamMenu.Items.Add(CreateMenuItem("????????", delegate { HideBehindCurrentWindow(pet, true); }));
            roamMenu.Items.Add(CreateMenuItem("?????????", delegate { PeekFromCurrentWindowEdge(pet, true); }));
            menu.Items.Add(roamMenu);
            menu.Items.Add(new Separator());

            MenuItem quietItem = new MenuItem { Header = "????", IsCheckable = true, IsChecked = _quiet };
            quietItem.Click += delegate
            {
                _quiet = quietItem.IsChecked;
                StopMotion(_cat);
                StopMotion(_dog);
                if (_quiet)
                {
                    _cat.Speak("????????", 2300);
                    _dog.Speak("???????", 2300);
                }
            };
            menu.Items.Add(quietItem);

            MenuItem autostartItem = new MenuItem
            {
                Header = "??????",
                IsCheckable = true,
                IsChecked = AutostartService.IsEnabled
            };
            autostartItem.Click += delegate
            {
                bool requested = autostartItem.IsChecked;
                if (!SetAutostart(requested)) autostartItem.IsChecked = !requested;
            };
            menu.Items.Add(autostartItem);

            MenuItem sizeMenu = new MenuItem { Header = "????" };
            sizeMenu.Items.Add(CreateMenuItem("??", delegate { SetScale(.78); }));
            sizeMenu.Items.Add(CreateMenuItem("???????", delegate { SetScale(1); }));
            sizeMenu.Items.Add(CreateMenuItem("???", delegate { SetScale(1.3); }));
            menu.Items.Add(sizeMenu);
            menu.Items.Add(new Separator());
            menu.Items.Add(CreateMenuItem("????", Exit));
            return menu;
        }

        private static MenuItem CreateMenuItem(string header, Action action)
        {
            MenuItem item = new MenuItem { Header = header };
            item.Click += delegate { action(); };
            return item;
        }

        private void SetActivityMode(ActivityMode mode)
        {
            if (_activityMode == mode) return;
            CancelAllActivities();
            _activityMode = mode;
            StopMotion(_cat);
            StopMotion(_dog);
            if (mode == ActivityMode.Focus)
            {
                if (!_focusAnchor.HasValue)
                {
                    _focusAnchor = new Point((_cat.Center.X + _dog.Center.X) / 2, (_cat.Center.Y + _dog.Center.Y) / 2);
                }
                Rect area = GetFocusArea();
                _cat.Speak("??????????????", 3200);
                _dog.Speak("??????????????", 3200);
                int token;
                if (!BeginPairActivity(out token)) return;
                GlidePair(area.Right - _cat.Width * 2 - 35, area.Bottom - _cat.Height + 24,
                    area.Right - _dog.Width - 12, area.Bottom - _dog.Height + 24, 750, token,
                    delegate(bool completed)
                    {
                        if (completed && IsPairCurrent(token)) EndPairActivity(token);
                    });
            }
            else
            {
                _cat.Speak("??????????", 2600);
                _dog.Speak("????????", 2600);
                _cat.Hop();
                _dog.Hop();
            }
        }

        private void FollowToFocusArea(PetWindow pet)
        {
            if (_activityMode != ActivityMode.Focus || pet.IsDragging || pet.IsBusy) return;
            Rect area = GetFocusArea();
            if (IsInsideArea(pet, area)) return;
            int version;
            if (!BeginSoloActivity(pet, out version)) return;
            double targetLeft = Compat.Clamp(area.Right - pet.Width - 18, area.Left + 10, area.Right - pet.Width - 10);
            double targetTop = Compat.Clamp(area.Bottom - pet.Height + 24, area.Top + 10, area.Bottom - pet.Height + 24);
            pet.Speak(Pick("??????", "???????????", "???????"), 2400);
            GlideTo(pet, targetLeft, targetTop, 760, version, delegate(bool completed)
            {
                if (completed && IsSameActivity(pet, version)) EndSoloActivity(pet);
            });
        }

        private static bool IsInsideArea(PetWindow pet, Rect area)
        {
            return pet.Left >= area.Left && pet.Top >= area.Top &&
                   pet.Left + pet.Width <= area.Right && pet.Top + pet.Height <= area.Bottom + 24;
        }

        private void SetScale(double scale)
        {
            if (Math.Abs(scale - _scale) < .01) return;
            PetWindow[] pets = { _cat, _dog };
            for (int index = 0; index < pets.Length; index++)
            {
                PetWindow pet = pets[index];
                double bottom = pet.Top + pet.Height;
                double center = pet.Left + pet.Width / 2;
                pet.Width = BasePetSize * scale;
                pet.Height = (BasePetSize + PetWindow.BubbleHeight) * scale;
                pet.Left = center - pet.Width / 2;
                pet.Top = bottom - pet.Height;
                ClampToActivityArea(pet);
            }
            _scale = scale;
        }

        private static void StartRunToward(PetWindow pet, double targetLeft, double targetTop, int milliseconds)
        {
            double deltaX = targetLeft - pet.Left;
            double deltaY = targetTop - pet.Top;
            double distance = Math.Max(1, Math.Sqrt(deltaX * deltaX + deltaY * deltaY));
            double speed = Compat.Clamp(distance / Math.Max(1, milliseconds / 67d), 1.2, 3.2);
            pet.MotionX = deltaX / distance * speed;
            pet.MotionY = deltaY / distance * speed;
            pet.MotionUntil = DateTime.UtcNow.AddMilliseconds(milliseconds);
            pet.FaceDirection(deltaX);
        }

        private void StartFreeRun(PetWindow pet, bool allowFullScreen)
        {
            if (pet.IsBusy || pet.IsDragging) return;
            Rect roamingArea = allowFullScreen ? SystemParameters.WorkArea : GetActivityArea();
            double angle = Compat.Random.NextDouble() * Math.PI * 2;
            double speed = 1.25 + Compat.Random.NextDouble() * 1.75;
            pet.MotionX = Math.Cos(angle) * speed;
            pet.MotionY = Math.Sin(angle) * speed * .72;
            if (Math.Abs(pet.MotionY) < .45) pet.MotionY = Compat.Random.Next(2) == 0 ? -.75 : .75;
            pet.MotionUntil = DateTime.UtcNow.AddMilliseconds(Compat.Random.Next(1800, 3800));
            if (allowFullScreen)
            {
                pet.IgnoreActivityBounds = true;
                double targetX = roamingArea.Left + Compat.Random.NextDouble() * Math.Max(1, roamingArea.Width - pet.Width);
                double targetY = roamingArea.Top + Compat.Random.NextDouble() * Math.Max(1, roamingArea.Height - pet.Height);
                StartRunToward(pet, targetX, targetY, Compat.Random.Next(2200, 4200));
            }
            pet.FaceDirection(pet.MotionX);
            pet.Speak(_activityMode == ActivityMode.Focus
                ? Pick("??????", "??????", "???????")
                : Pick("??????", "?????", "?????????"), 1800);
        }

        private void HideAtScreenEdge(PetWindow pet, bool force)
        {
            if (_activityMode != ActivityMode.FullScreen && !force)
            {
                pet.Speak("????????????", 2200);
                StartFreeRun(pet, false);
                return;
            }
            int token;
            if (!BeginPairActivity(out token)) return;
            Rect area = SystemParameters.WorkArea;
            bool hideLeft = (_cat.Center.X + _dog.Center.X) / 2 < (area.Left + area.Right) / 2;
            double hiddenLeft = hideLeft ? area.Left - _cat.Width * .52 : area.Right - _cat.Width * .48;
            double groupTop = Compat.Clamp((_cat.Top + _dog.Top) / 2 - 80, area.Top + 24, area.Bottom - _dog.Height - 145);
            double catTop = groupTop;
            double dogTop = groupTop + 145;
            _cat.SetEdgePeekPose(true, hideLeft, true, true);
            _dog.SetEdgePeekPose(true, hideLeft, true, true);
            _cat.Speak(Pick("?????????", "??????", "?????"), 2300);
            _dog.Speak(Pick("???????", "?????????", "?????????"), 2300);
            GlidePair(hiddenLeft, catTop, hiddenLeft, dogTop, 900, token, delegate(bool completed)
            {
                if (!completed || !IsPairCurrent(token)) return;
                _cat.Burst("?", System.Windows.Media.Color.FromRgb(124, 137, 158));
                _dog.Burst("?", System.Windows.Media.Color.FromRgb(124, 137, 158));
                PairAfter(token, Compat.Random.Next(2600, 4100), delegate
                {
                    double emergeLeft = hideLeft ? area.Left + 10 : area.Right - _cat.Width - 10;
                    _cat.SetEdgePeekPose(false, hideLeft);
                    _dog.SetEdgePeekPose(false, hideLeft);
                    GlidePair(emergeLeft, catTop, emergeLeft, dogTop, 650, token, delegate(bool emerged)
                    {
                        if (!emerged || !IsPairCurrent(token)) return;
                        _cat.Hop(true);
                        _dog.Hop(true);
                        EndPairActivity(token);
                    });
                });
            });
        }

        private void PerchOnWindow(PetWindow pet, bool force)
        {
            if (_activityMode != ActivityMode.FullScreen && !force)
            {
                pet.Speak("?????????????????", 2600);
                return;
            }
            int version;
            if (!BeginSoloActivity(pet, out version)) return;
            IntPtr hostWindow;
            NativeRect perch;
            if (!TryGetPreferredHostWindow(out hostWindow, out perch))
            {
                EndSoloActivity(pet);
                StartFreeRun(pet, false);
                return;
            }
            Rect workArea = SystemParameters.WorkArea;
            double targetX = Compat.Clamp(perch.Left + 28 + Compat.Random.NextDouble() * Math.Max(30, perch.Width - pet.Width - 56), workArea.Left, workArea.Right - pet.Width);
            bool enoughRoomAbove = perch.Top - workArea.Top >= pet.Height * .52;
            double targetY = enoughRoomAbove ? perch.Top - pet.Height + 30 : perch.Top - PetWindow.BubbleHeight + 8;
            pet.Speak(Pick("????", "??????", "???????", "?????"), 2600);
            GlideTo(pet, targetX, targetY, 1100, version, delegate(bool completed)
            {
                if (!completed || !IsSameActivity(pet, version)) return;
                pet.BounceTwice("?");
                _scheduler.After(Compat.Random.Next(3500, 6200), delegate
                {
                    if (!IsSameActivity(pet, version)) return;
                    pet.Speak(Pick("?????", "??????", "?????"), 2000);
                    EndSoloActivity(pet);
                });
            });
        }

        private void HideBehindCurrentWindow(PetWindow pet, bool force)
        {
            if (_activityMode != ActivityMode.FullScreen && !force)
            {
                pet.Speak("??????????????????", 2600);
                return;
            }
            int version;
            if (!BeginSoloActivity(pet, out version)) return;
            IntPtr hostWindow;
            NativeRect bounds;
            if (!TryGetPreferredHostWindow(out hostWindow, out bounds))
            {
                pet.Speak("????????????????", 2400);
                EndSoloActivity(pet);
                return;
            }
            Rect workArea = SystemParameters.WorkArea;
            bool canReallyHide = bounds.Top - workArea.Top >= 58 && !IsZoomed(hostWindow);
            double targetLeft = Compat.Clamp(bounds.Left + 36 + Compat.Random.NextDouble() * Math.Max(30, bounds.Width - pet.Width - 72), workArea.Left, workArea.Right - pet.Width);
            double targetTop = canReallyHide
                ? bounds.Top - 54
                : Compat.Clamp(bounds.Top - PetWindow.BubbleHeight, workArea.Top - PetWindow.BubbleHeight, workArea.Bottom - pet.Height);
            pet.Speak(Pick("?????????", "????????", "???????"), 2500);
            GlideTo(pet, targetLeft, targetTop, 850, version, delegate(bool completed)
            {
                if (!completed || !IsSameActivity(pet, version)) return;
                if (canReallyHide)
                {
                    pet.Topmost = false;
                    SetWindowBehind(pet, hostWindow);
                }
                else pet.SetHeadPeek(true);
                pet.Burst("?", System.Windows.Media.Color.FromRgb(122, 136, 158));
                _scheduler.After(Compat.Random.Next(2800, 4600), delegate
                {
                    if (!IsSameActivity(pet, version)) return;
                    RestorePetLayer(pet);
                    pet.Speak("?????", 1900);
                    pet.Hop(true);
                    EndSoloActivity(pet);
                });
            });
        }

        private void PeekFromCurrentWindowEdge(PetWindow pet, bool force)
        {
            if (_activityMode != ActivityMode.FullScreen && !force)
            {
                pet.Speak("??????????????????", 2400);
                return;
            }
            int token;
            if (!BeginPairActivity(out token)) return;
            IntPtr hostWindow;
            NativeRect bounds;
            if (!TryGetPreferredHostWindow(out hostWindow, out bounds))
            {
                _cat.Speak("??????????????", 2200);
                _dog.Speak("?????????????", 2200);
                EndPairActivity(token);
                return;
            }
            Rect workArea = SystemParameters.WorkArea;
            double leftRoom = Math.Max(0, bounds.Left - workArea.Left);
            double rightRoom = Math.Max(0, workArea.Right - bounds.Right);
            double pairCenterX = (_cat.Center.X + _dog.Center.X) / 2;
            double windowCenterX = (bounds.Left + bounds.Right) / 2.0;
            bool fromLeft = Math.Abs(leftRoom - rightRoom) > 28 ? leftRoom > rightRoom : pairCenterX <= windowCenterX;
            double exteriorRoom = fromLeft ? leftRoom : rightRoom;
            bool naturalOcclusion = exteriorRoom >= 18 && !IsZoomed(hostWindow);
            double targetLeft = fromLeft ? bounds.Left - _cat.Width * .38 : bounds.Right - _cat.Width * .62;
            if (!naturalOcclusion) targetLeft = fromLeft ? workArea.Left - _cat.Width * .64 : workArea.Right - _cat.Width * .36;
            double verticalGap = Compat.Clamp(bounds.Height * .22, 112, 138);
            double pairHeight = _dog.Height + verticalGap;
            double topMin = Math.Max(workArea.Top + 16, bounds.Top + 28);
            double topMax = Math.Min(workArea.Bottom - pairHeight - 12, bounds.Bottom - pairHeight - 28);
            double catTop = topMax >= topMin
                ? Compat.Clamp(bounds.Top + bounds.Height * .46 - pairHeight / 2, topMin, topMax)
                : Compat.Clamp(bounds.Top + bounds.Height / 2.0 - pairHeight / 2.0, workArea.Top + 8, Math.Max(workArea.Top + 8, workArea.Bottom - pairHeight - 8));
            double dogTop = catTop + verticalGap;
            _cat.SetEdgePeekPose(true, fromLeft, true, false);
            _dog.SetEdgePeekPose(true, fromLeft, true, false);
            _cat.Speak(Pick("????????????", "??????????", "??????????"), 2600);
            _dog.Speak(Pick("???????", "????????????", "???????"), 2600);
            GlidePair(targetLeft, catTop, targetLeft, dogTop, 880, token, delegate(bool completed)
            {
                if (!completed || !IsPairCurrent(token)) return;
                _cat.SetEdgePeekPose(true, fromLeft, true, false);
                _dog.SetEdgePeekPose(true, fromLeft, true, false);
                if (naturalOcclusion)
                {
                    _cat.Topmost = false;
                    _dog.Topmost = false;
                    SetWindowBehind(_cat, hostWindow);
                    SetWindowBehind(_dog, hostWindow);
                }
                _cat.Burst("?", System.Windows.Media.Color.FromRgb(126, 139, 162));
                _dog.Burst("?", System.Windows.Media.Color.FromRgb(126, 139, 162));
                PairAfter(token, Compat.Random.Next(2700, 4300), delegate
                {
                    RestorePetLayer(_cat);
                    RestorePetLayer(_dog);
                    _cat.SetEdgePeekPose(false, fromLeft);
                    _dog.SetEdgePeekPose(false, fromLeft);
                    double emergeLeft = fromLeft
                        ? Compat.Clamp(bounds.Left + 10, workArea.Left + 6, workArea.Right - _cat.Width - 6)
                        : Compat.Clamp(bounds.Right - _cat.Width - 10, workArea.Left + 6, workArea.Right - _cat.Width - 6);
                    GlidePair(emergeLeft, catTop, emergeLeft, dogTop, 600, token, delegate(bool emerged)
                    {
                        if (!emerged || !IsPairCurrent(token)) return;
                        _cat.Hop(true);
                        _dog.Hop(true);
                        EndPairActivity(token);
                    });
                });
            });
        }

        private void GlidePair(double catLeft, double catTop, double dogLeft, double dogTop, int milliseconds, int token, Action<bool> completed)
        {
            int remaining = 2;
            bool allCompleted = true;
            Action<bool> oneCompleted = delegate(bool result)
            {
                if (!result) allCompleted = false;
                remaining--;
                if (remaining == 0) completed(allCompleted && IsPairCurrent(token));
            };
            GlideTo(_cat, catLeft, catTop, milliseconds, _cat.ActivityVersion, oneCompleted);
            GlideTo(_dog, dogLeft, dogTop, milliseconds, _dog.ActivityVersion, oneCompleted);
        }

        private void GlideTo(PetWindow pet, double targetLeft, double targetTop, int milliseconds, int activityVersion, Action<bool> completed)
        {
            double startLeft = pet.Left;
            double startTop = pet.Top;
            pet.FaceDirection(targetLeft - startLeft);
            _scheduler.Tween(milliseconds,
                delegate { return pet.IsBusy && pet.ActivityVersion == activityVersion; },
                delegate(double progress)
                {
                    pet.Left = startLeft + (targetLeft - startLeft) * progress;
                    pet.Top = startTop + (targetTop - startTop) * progress;
                },
                completed);
        }

        private bool BeginSoloActivity(PetWindow pet, out int version)
        {
            version = 0;
            if (_isPairActivity || _isCuddling || pet.IsBusy || pet.IsDragging) return false;
            pet.IsBusy = true;
            version = ++pet.ActivityVersion;
            StopMotion(pet);
            return true;
        }

        private static bool IsSameActivity(PetWindow pet, int version)
        {
            return pet.IsBusy && pet.ActivityVersion == version;
        }

        private void EndSoloActivity(PetWindow pet)
        {
            StopMotion(pet);
            pet.IsBusy = false;
            pet.SetEdgePeekPose(false, true);
            RestorePetLayer(pet);
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

        private void CancelAllActivities()
        {
            _pairToken++;
            _isPairActivity = false;
            _isCuddling = false;
            if (_cuddleWindow != null)
            {
                _cuddleWindow.Close();
                _cuddleWindow = null;
            }
            CancelSoloActivity(_cat);
            CancelSoloActivity(_dog);
            _cat.Show();
            _dog.Show();
        }

        private static void StopMotion(PetWindow pet)
        {
            pet.MotionX = 0;
            pet.MotionY = 0;
        }

        private void TrackForegroundWindow()
        {
            IntPtr foreground = GetForegroundWindow();
            if (IsUsableHostWindow(foreground)) _lastHostWindow = foreground;
        }

        private bool TryGetPreferredHostWindow(out IntPtr window, out NativeRect rectangle)
        {
            TrackForegroundWindow();
            if (IsUsableHostWindow(_lastHostWindow) && TryGetVisibleWindowBounds(_lastHostWindow, out rectangle))
            {
                window = _lastHostWindow;
                return true;
            }
            List<NativeRect> rectangles = new List<NativeRect>();
            List<IntPtr> handles = new List<IntPtr>();
            IntPtr shell = GetShellWindow();
            EnumWindows(delegate(IntPtr candidate, IntPtr parameter)
            {
                if (candidate == shell || !IsWindowVisible(candidate) || IsIconic(candidate)) return true;
                uint processId;
                GetWindowThreadProcessId(candidate, out processId);
                if (processId == (uint)Compat.ProcessId) return true;
                int length = GetWindowTextLength(candidate);
                if (length <= 0) return true;
                StringBuilder title = new StringBuilder(length + 1);
                GetWindowText(candidate, title, title.Capacity);
                if (Compat.IsNullOrWhiteSpace(title.ToString())) return true;
                NativeRect bounds;
                if (!TryGetVisibleWindowBounds(candidate, out bounds)) return true;
                if (bounds.Width < 360 || bounds.Height < 220) return true;
                Rect area = SystemParameters.WorkArea;
                if (bounds.Right <= area.Left || bounds.Left >= area.Right || bounds.Bottom <= area.Top || bounds.Top >= area.Bottom) return true;
                rectangles.Add(bounds);
                handles.Add(candidate);
                return true;
            }, IntPtr.Zero);
            if (rectangles.Count == 0)
            {
                window = IntPtr.Zero;
                rectangle = new NativeRect();
                return false;
            }
            int index = Compat.Random.Next(rectangles.Count);
            window = handles[index];
            rectangle = rectangles[index];
            _lastHostWindow = window;
            return true;
        }

        private static bool IsUsableHostWindow(IntPtr window)
        {
            if (window == IntPtr.Zero || window == GetShellWindow() || !IsWindowVisible(window) || IsIconic(window)) return false;
            uint processId;
            GetWindowThreadProcessId(window, out processId);
            if (processId == (uint)Compat.ProcessId) return false;
            NativeRect bounds;
            return GetWindowTextLength(window) > 0 && TryGetVisibleWindowBounds(window, out bounds) && bounds.Width >= 360 && bounds.Height >= 220;
        }

        private static bool TryGetVisibleWindowBounds(IntPtr window, out NativeRect bounds)
        {
            NativeRect physicalBounds;
            int hr = DwmGetWindowAttribute(window, DwmwaExtendedFrameBounds, out physicalBounds, Marshal.SizeOf(typeof(NativeRect)));
            if (hr != 0 || physicalBounds.Width <= 0 || physicalBounds.Height <= 0)
            {
                if (!GetWindowRect(window, out physicalBounds))
                {
                    bounds = new NativeRect();
                    return false;
                }
            }
            bounds = physicalBounds;
            return true;
        }

        private static void SetWindowBehind(PetWindow pet, IntPtr hostWindow)
        {
            IntPtr petHandle = new WindowInteropHelper(pet).Handle;
            SetWindowPos(petHandle, hostWindow, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
        }

        private static void RestorePetLayer(PetWindow pet)
        {
            if (!pet.IsLoaded) return;
            pet.SetHeadPeek(false);
            pet.SetEdgePeekPose(false, true);
            pet.Topmost = true;
            IntPtr petHandle = new WindowInteropHelper(pet).Handle;
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
            Rect workArea = SystemParameters.WorkArea;
            double width = Math.Min(580, workArea.Width);
            double height = Math.Min(380, workArea.Height);
            Point defaultCenter = new Point(workArea.Right - width / 2, workArea.Bottom - height / 2);
            Point anchor = _focusAnchor.HasValue ? _focusAnchor.Value : defaultCenter;
            double left = Compat.Clamp(anchor.X - width / 2, workArea.Left, workArea.Right - width);
            double top = Compat.Clamp(anchor.Y - height / 2, workArea.Top, workArea.Bottom - height);
            return new Rect(left, top, width, height);
        }

        private void ClampToActivityArea(PetWindow pet)
        {
            ClampPetToArea(pet, GetActivityArea(), _activityMode == ActivityMode.FullScreen);
        }

        private static void ClampPetToArea(PetWindow pet, Rect area, bool allowSidePeek)
        {
            double sidePeek = allowSidePeek ? pet.Width * .18 : 0;
            pet.Left = Compat.Clamp(pet.Left, area.Left - sidePeek, area.Right - pet.Width + sidePeek);
            pet.Top = Compat.Clamp(pet.Top, area.Top, area.Bottom - pet.Height + 24);
        }

        private static string Pick(params string[] values)
        {
            return values[Compat.Random.Next(values.Length)];
        }

        private delegate bool EnumWindowsDelegate(IntPtr window, IntPtr parameter);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
            public int Width { get { return Right - Left; } }
            public int Height { get { return Bottom - Top; } }
        }

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsDelegate callback, IntPtr parameter);
        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll")]
        private static extern IntPtr GetShellWindow();
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr window, StringBuilder text, int maxCount);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextLength(IntPtr window);
        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr window, out NativeRect rectangle);
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
        [DllImport("user32.dll")]
        private static extern bool IsZoomed(IntPtr window);
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out NativeRect value, int size);

        private static readonly IntPtr HwndTopmost = new IntPtr(-1);
        private const uint SwpNoSize = 0x0001;
        private const uint SwpNoMove = 0x0002;
        private const uint SwpNoActivate = 0x0010;
        private const int DwmwaExtendedFrameBounds = 9;

        private void CreateTrayIcon()
        {
            _tray = new Forms.NotifyIcon();
            _tray.Icon = Icon.ExtractAssociatedIcon(Compat.ProcessPath);
            _tray.Text = "?????????";
            _tray.Visible = true;
            Forms.ContextMenuStrip menu = new Forms.ContextMenuStrip();
            menu.Items.Add("????", null, delegate { Dispatch(BringBack); });
            menu.Items.Add("??????", null, delegate { Dispatch(delegate { SetActivityMode(ActivityMode.Focus); }); });
            menu.Items.Add("??????", null, delegate { Dispatch(delegate { SetActivityMode(ActivityMode.FullScreen); }); });
            menu.Items.Add("?????", null, delegate { Dispatch(GatherAndCuddle); });
            menu.Items.Add("?????", null, delegate { Dispatch(KissCheek); });
            menu.Items.Add("????", null, delegate { Dispatch(TriggerRandomPairInteraction); });
            menu.Items.Add(new Forms.ToolStripSeparator());
            _trayAutostartItem = new Forms.ToolStripMenuItem("??????");
            _trayAutostartItem.CheckOnClick = true;
            _trayAutostartItem.Checked = AutostartService.IsEnabled;
            _trayAutostartItem.Click += delegate
            {
                Dispatch(delegate
                {
                    bool requested = _trayAutostartItem.Checked;
                    if (!SetAutostart(requested)) _trayAutostartItem.Checked = !requested;
                });
            };
            menu.Items.Add(_trayAutostartItem);
            menu.Items.Add("??", null, delegate { Dispatch(Exit); });
            _tray.ContextMenuStrip = menu;
            _tray.DoubleClick += delegate { Dispatch(BringBack); };
        }

        private bool SetAutostart(bool enabled)
        {
            string error;
            if (!AutostartService.TrySetEnabled(enabled, out error))
            {
                _cat.Speak("?????????????????", 3200);
                _dog.Speak("??????????", 3200);
                return false;
            }

            if (_trayAutostartItem != null) _trayAutostartItem.Checked = enabled;
            _cat.Speak(enabled ? "?????????" : "??????????", 2600);
            _dog.Speak(enabled ? "?????????" : "?????????", 2600);
            return true;
        }

        private static void Dispatch(Action action)
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, action);
        }

        private void BringBack()
        {
            CancelAllActivities();
            Rect area = GetActivityArea();
            _cat.Left = area.Right - _cat.Width * 2 - 65;
            _dog.Left = area.Right - _dog.Width - 20;
            _cat.Top = area.Bottom - _cat.Height + 24;
            _dog.Top = area.Bottom - _dog.Height + 24;
            _cat.Show();
            _dog.Show();
            _cat.Speak("??????");
            _dog.Hop(true);
        }

        private void Exit()
        {
            _motionTimer.Stop();
            _lifeTimer.Stop();
            _interactionTimer.Stop();
            _proximityTimer.Stop();
            _scheduler.Stop();
            if (_tray != null)
            {
                _tray.Visible = false;
                _tray.Dispose();
            }
            _cat.Close();
            _dog.Close();
            System.Windows.Application.Current.Shutdown();
        }
    }
}
#endif
