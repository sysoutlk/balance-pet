// Engine: owns the pets, the bubble, the settings card and the render loop.
//
// The loop is adaptive: while a pet is walking, acting, being dragged, thrown or sliding
// to the screen edge (or the bubble is visible) it ticks at ~25 fps; otherwise it drops to
// ~4 fps and only keeps the breathing animation and the walk/action schedule alive.
//
// Clicking a pet cycles through four DeepSeek-centric views: balance, today's spend,
// peak/off-peak, data sources.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace BalancePet
{
    internal sealed class Engine
    {
        public const int SafeCloneLimit = 20;
        private readonly Config cfg;
        private readonly DispatcherTimer timer = new DispatcherTimer();
        public readonly DataService Data;
        public readonly List<PetWindow> Pets = new List<PetWindow>();
        public readonly BubbleWindow Bubble = new BubbleWindow();
        private readonly SettingsPanel panel;
        private DateTime lastTick = DateTime.UtcNow;
        private DateTime lastPositionSave = DateTime.UtcNow;
        private DateTime nextCloneStepAt = DateTime.MinValue;
        private bool cloneFalling;
        private DateTime demoActionAt = DateTime.MinValue;
        private DateTime lastBumpQuipAt = DateTime.MinValue;
        private sealed class SuspendedPet
        {
            public int Slot;
            public string Role;
            public double Scale;
            public double X;
            public double Y;
        }
        private readonly List<SuspendedPet> suspended = new List<SuspendedPet>();
        private bool? lastPeak;
        private readonly HashSet<string> balanceLatched = new HashSet<string>();
        private readonly HashSet<string> budgetLatched = new HashSet<string>();
        private readonly HashSet<string> quotaLatched = new HashSet<string>();
        private int bubblePage;
        private bool bubbleIsMessage;
        private bool bubbleIsAlert;
        private string alertTitle = "";
        private string alertText = "";
        private string messageTitle = "";
        private string messageText = "";
        private string alertNote = "";
        private string bubbleQuip = "";
        public bool Demo;
        public bool Quitting;

        public Engine(Config config, bool demo)
        {
            cfg = config;
            Demo = demo;
            Data = new DataService(cfg);
            panel = new SettingsPanel(cfg, this);
        }

        public void Start()
        {
            Log.Write("start data-root=" + AppPaths.DataRoot);
            panel.Build();
            BuildPets();
            Data.Updated += OnDataUpdated;
            Data.Start();
            timer.Interval = TimeSpan.FromMilliseconds(60);
            timer.Tick += OnTick;
            // 先要 1ms 定时精度，帧率设置（frames.*.PerSecond）才不会被系统默认的
            // ~15.6ms 调度精度磨掉一半（进程退出自动恢复，不需要手动还回去）。
            Native.RaiseTimerResolution(1);
            timer.Start();
            if (Demo && Pets.Count > 0)
            {
                bubblePage = Program.DemoPageIndex;
                bubbleQuip = QuipFor("greeting");
                ShowView(Pets[0], BuildPage(bubblePage), 3600);
                demoActionAt = DateTime.UtcNow.AddSeconds(1);
            }
        }

        // ------------------------------------------------------------ 初始化
        private void BuildPets()
        {
            string role = PickRole("");
            List<Dictionary<string, object>> saved = cfg.GetObjects("", "pets");
            if (saved.Count == 0)
            {
                Dictionary<string, object> legacy = cfg.GetObject("", "lastPosition");
                if (legacy.Count > 0) saved.Add(legacy);
            }
            // 每次启动只保留一只，之后由"自动分裂"慢慢长出来（不再恢复上次的分身们）。
            int count = 1;
            for (int i = 0; i < count; i++)
            {
                PetWindow pet = new PetWindow(cfg);
                pet.ApplyRole(role);
                pet.ResizePet();
                double x = -1;
                double y = -1;
                if (i < saved.Count)
                {
                    x = Json.ToNumber(Json.Get(saved[i], "x"), -1);
                    y = Json.ToNumber(Json.Get(saved[i], "y"), -1);
                }
                if (x < 0 || y < 0)
                {
                    System.Drawing.Rectangle area = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea;
                    x = area.Right - pet.Width - 28;
                    y = area.Bottom - pet.Height - 20;
                }
                pet.Place(x, y);
                // Snap on startup too, so a pet saved against an edge hides itself later.
                pet.SetIdle();
                // 启动时不沿用上次的贴边记录，避免"一打开就全部贴边"。
                pet.EdgeSide = "";
                pet.EdgeHideAt = DateTime.MaxValue;
                WirePet(pet);
                Pets.Add(pet);
                pet.Show();
            }
        }

        private void WirePet(PetWindow pet)
        {
            pet.Clicked += OnPetClicked;
            pet.ContextRequested += OnPetContextRequested;
            pet.WheelZoomed += OnPetWheelZoomed;
            pet.Hovered += OnPetHovered;
            pet.Moved += OnPetMoved;
        }

        // ------------------------------------------------------------ 主循环
        private void OnTick(object sender, EventArgs e)
        {
            DateTime now = DateTime.UtcNow;
            double dt = (now - lastTick).TotalSeconds;
            lastTick = now;
            if (dt < 0.005) dt = 0.005;
            if (dt > 0.6) dt = 0.6;

            bool active = false;
            double walkSpeed = cfg.GetNumber("walk", "speedPxPerSec", 72);
            foreach (PetWindow pet in Pets)
            {
                UpdatePet(pet, now, dt);
                if (pet.Mode != "idle" || pet.Action.Length > 0) active = true;
            }
            // 多桌宠碰撞：互相推开 + 把速度带给被撞的那只（撞到第三只还能继续串）
            if (Pets.Count > 1 && ResolveCollisions(now, dt, walkSpeed)) active = true;

            if (AutoSplitDue(now)) active = true;

            if (Demo)
            {
                if (now >= demoActionAt)
                {
                    demoActionAt = now.AddSeconds(2.6);
                    PerformAction(Pets.Count > 0 ? Pets[0] : null, "random", false);
                }
                active = true;
            }

            if (Bubble.Visibility == Visibility.Visible)
            {
                Bubble.Position(false);
                Bubble.RefreshPeak(PeakBubbleText(), Data.Current.IsPeak);
                if (!Demo && now >= Bubble.HideAt) Bubble.HideNow();
                active = true;
            }

            // 帧率可调：动作/拖动/扔出去时按 frames.activePerSecond，闲下来按 frames.idlePerSecond
            TimeSpan interval = active
                ? FrameInterval("frames.activePerSecond", 25, 4)
                : FrameInterval("frames.idlePerSecond", 4, 4);
            if (timer.Interval != interval) timer.Interval = interval;

            if ((now - lastPositionSave).TotalSeconds >= 30)
            {
                lastPositionSave = now;
                SavePositions();
            }
        }

        /// <summary>把「每秒多少帧」换算成定时器间隔（毫秒）：帧率可调（frames.*），带上下限兜底。</summary>
        private TimeSpan FrameInterval(string key, double fallback, double minMs)
        {
            double fps = cfg.GetNumberAt(key, fallback);
            if (fps < 1) fps = fallback;
            // 上限就是显示器刷新率：比屏幕还快没有意义，白白耗电。
            int deviceHz = Native.RefreshRate();
            if (fps > deviceHz) fps = deviceHz;
            double ms = 1000.0 / fps;
            if (ms < minMs) ms = minMs;
            if (ms > 600) ms = 600;
            return TimeSpan.FromMilliseconds(Math.Round(ms));
        }

        private void UpdatePet(PetWindow pet, DateTime now, double dt)
        {
            double speed = cfg.GetNumber("walk", "speedPxPerSec", 72);
            bool sleeping = pet.Sleeping;
            bool pinnedMode = !string.IsNullOrEmpty(pet.PinnedActionKey());
            AdvanceFrames(pet, dt, sleeping);

            if (pet.Mode == "drag")
            {
                pet.Bob.Y = 0;
                return;
            }

            if (pet.Mode == "thrown")
            {
                double friction = cfg.GetNumber("throw", "frictionPerSec", 0.35);
                double bounce = cfg.GetNumber("throw", "bounce", 0.55);
                double stopSpeed = cfg.GetNumber("throw", "stopSpeedPxPerSec", 40);
                pet.VelocityX -= pet.VelocityX * friction * dt;
                pet.VelocityY -= pet.VelocityY * friction * dt;
                pet.X += pet.VelocityX * dt;
                pet.Y += pet.VelocityY * dt;
                ScreenArea area = ScreenArea.ForPoint(pet.X + pet.Width / 2, pet.Y + pet.Height / 2);
                if (pet.X < area.Left) { pet.X = area.Left; pet.VelocityX = -pet.VelocityX * bounce; }
                if (pet.X + pet.Width > area.Right) { pet.X = area.Right - pet.Width; pet.VelocityX = -pet.VelocityX * bounce; }
                if (pet.Y < area.Top) { pet.Y = area.Top; pet.VelocityY = -pet.VelocityY * bounce; }
                if (pet.Y + pet.Height > area.Bottom) { pet.Y = area.Bottom - pet.Height; pet.VelocityY = -pet.VelocityY * bounce; }
                if (!pet.IsPetPack) pet.Spin.Angle += pet.VelocityX * dt * 0.06;
                pet.ApplyPosition();
                double speedNow = Math.Sqrt(pet.VelocityX * pet.VelocityX + pet.VelocityY * pet.VelocityY);
                if (speedNow < stopSpeed)
                {
                    pet.Spin.Angle = 0;
                    pet.SetIdleAfterThrow();   // 扔到哪就停哪，不再被吸到屏幕边上
                    ScheduleEdgeHide(pet, now);
                }
                return;
            }

            if (pinnedMode)
            {
                UpdatePinnedAction(pet, now);
                pet.ApplyPosition();
                return;
            }

            if (pet.Mode == "edge")
            {
                UpdateEdgeSlide(pet, now, dt);
                return;
            }

            if (pet.Mode == "walk")
            {
                // 关掉「持续散步」后立刻停在原地；已经走完的路不再继续。
                if (!cfg.GetFlag("walk", "enabled", true) || sleeping)
                {
                    pet.SetIdle();
                    return;
                }
                double dx = pet.TargetX - pet.X;
                double dy = pet.TargetY - pet.Y;
                double distance = Math.Sqrt(dx * dx + dy * dy);
                if (distance < 4)
                {
                    if (pet.NextLeg()) return;   // 折线散步：接着走下一段，别直接收工
                    pet.SetIdle();
                    ScheduleEdgeHide(pet, now);
                    return;
                }
                double step = speed * pet.SpeedFactor * dt;
                if (step > distance) step = distance;
                // 蛇形摆动：朝路点的方向再叠一点左右摆幅（越接近路点收得越紧），路径弯弯扭扭。
                double wobble = 0.32 * Math.Min(1, distance / 70.0)
                    * Math.Sin(now.TimeOfDay.TotalSeconds * 2.1 + pet.WobbleSeed);
                double heading = Math.Atan2(dy, dx) + wobble;
                pet.X += Math.Cos(heading) * step;
                pet.Y += Math.Sin(heading) * step;
                if (Math.Abs(dx) > 6) pet.SetFacing(dx > 0 ? 1 : -1);

                if (!string.IsNullOrEmpty(pet.Action))
                {
                    // 散步途中叠加动作：身体继续前进，姿态动画照常播放。
                    double total = (pet.ActionUntil - pet.ActionStart).TotalSeconds;
                    double p = total <= 0 ? 1 : Math.Min(1, (now - pet.ActionStart).TotalSeconds / total);
                    AnimateAction(pet, now, p);
                    if (now >= pet.ActionUntil) EndAction(pet, now, true);
                }
                else
                {
                    double rate = pet.SpeedFactor >= 2 ? 16 : 9;
                    if (!pet.IsPetPack)
                    {
                        pet.Bob.Y = Math.Abs(Math.Sin(now.TimeOfDay.TotalSeconds * rate)) * -3;
                        if (pet.SpeedFactor >= 2) pet.Spin.Angle = pet.Facing * 7;
                    }
                    else
                    {
                        pet.Bob.Y = 0;
                        pet.Spin.Angle = 0;
                    }
                    if (now >= pet.NextActionAt && cfg.GetFlag("actions", "enabled", true)
                        && !(panel != null && panel.IsVisible))
                    {
                        string walkingAction = PickWalkingAction(pet);
                        if (string.IsNullOrEmpty(walkingAction))
                        {
                            // 动作池为空或全是原地动作：别每帧重试。
                            pet.NextActionAt = now.AddSeconds(30);
                        }
                        else
                        {
                            pet.NextActionAt = DateTime.MaxValue;
                            Log.Write("walk action " + walkingAction);
                            pet.StartAction(walkingAction);
                        }
                    }
                }
                pet.ApplyPosition();
                return;
            }

            if (!string.IsNullOrEmpty(pet.Action))
            {
                double total = (pet.ActionUntil - pet.ActionStart).TotalSeconds;
                double p = total <= 0 ? 1 : Math.Min(1, (now - pet.ActionStart).TotalSeconds / total);
                AnimateAction(pet, now, p);
                if (now >= pet.ActionUntil) EndAction(pet, now, false);
                return;
            }

            // idle
            pet.Spin.Angle = 0;
            pet.Bob.Y = pet.IsPetPack ? 0 : Math.Sin(now.TimeOfDay.TotalSeconds * 1.4 + pet.Phase) * (sleeping ? 0.8 : 1.6);
            if (pinnedMode && (pet.EdgeHidden || pet.Tuck > 0.01))
            {
                pet.Untuck();
                pet.EdgeHideAt = DateTime.MaxValue;
            }
            if (!pinnedMode && pet.EdgeHidden)
            {
                if (!EdgeHideEnabled(pet))
                {
                    pet.StartEdgeSlide(false);
                    return;
                }
                // Stays tucked in (with its own animation) until the user drags it out.
                return;
            }
            if (!pinnedMode && pet.Dropped && EdgeHideEnabled(pet))
            {
                // Dropped against an edge: tuck in right away instead of waiting for the timer.
                pet.Dropped = false;
                pet.StartEdgeSlide(true);
                return;
            }
            pet.Dropped = false;
            if (!pinnedMode && pet.Tuck > 0.01) return;   // mid-tuck: no walking, no random actions
            RefreshEdgeAttachment(pet);
            if (!pinnedMode && !string.IsNullOrEmpty(pet.EdgeSide))
            {
                pet.StopActionForEdge();
                if (EdgeHideEnabled(pet) && pet.EdgeHideAt == DateTime.MaxValue)
                {
                    ScheduleEdgeHide(pet, now);
                }
                else if (EdgeHideEnabled(pet) && now >= pet.EdgeHideAt)
                {
                    pet.StartEdgeSlide(true);
                }
                return;
            }
            if (sleeping && !pinnedMode) return;
            if (!pinnedMode && now >= pet.NextWalkAt && cfg.GetFlag("walk", "enabled", true))
            {
                pet.StartWalk();
                return;
            }
            if (pinnedMode && pet.NextActionAt > now) pet.NextActionAt = now;
            if (now >= pet.NextActionAt && cfg.GetFlag("actions", "enabled", true))
            {
                // 调设置/看预览时先不自动演，免得动作和气泡打断操作。
                if (!pinnedMode && panel != null && panel.IsVisible) return;
                pet.StartAction("random");
                if (string.IsNullOrEmpty(pet.LastActionKey)) pet.NextActionAt = now.AddSeconds(30);
            }
        }

        private void UpdatePinnedAction(PetWindow pet, DateTime now)
        {
            string pinned = pet.PinnedActionKey();
            if (string.IsNullOrEmpty(pinned)) return;
            pet.Untuck();
            pet.EdgeSide = "";
            pet.EdgeHidden = false;
            pet.EdgeHideAt = DateTime.MaxValue;
            pet.NextWalkAt = DateTime.MaxValue;
            pet.NextActionAt = now;
            if (pet.Mode != "idle") pet.SetIdle();

            if (!string.Equals(pet.Action, pinned, StringComparison.Ordinal))
            {
                pet.Action = "";
                pet.StartAction("random");
                return;
            }

            double total = (pet.ActionUntil - pet.ActionStart).TotalSeconds;
            double progress = total <= 0
                ? 1
                : Math.Min(1, (now - pet.ActionStart).TotalSeconds / total);
            AnimateAction(pet, now, progress);
            if (now < pet.ActionUntil) return;

            pet.Action = "";
            pet.Spin.Angle = 0;
            pet.Squash.ScaleX = 1;
            pet.Squash.ScaleY = 1;
            pet.Bob.X = 0;
            pet.Bob.Y = 0;
            pet.StartAction("random");
        }

        /// <summary>
        /// 多桌宠碰撞：先把叠在一起的两只推开，再把速度沿碰撞方向递给对方——被撞的那只
        /// 会被带着滑一程，撞上第三只还能继续串下去；被用户拖着的由用户掌着，只挤开别人。
        /// </summary>
        private bool ResolveCollisions(DateTime now, double dt, double walkSpeed)
        {
            bool touched = false;
            double carryFactor = cfg.GetNumber("throw", "carryFactor", 0.72);
            for (int i = 0; i < Pets.Count; i++)
            {
                for (int j = i + 1; j < Pets.Count; j++)
                {
                    PetWindow a = Pets[i];
                    PetWindow b = Pets[j];
                    // 收起 / 正在收放的宠物不参与：它们贴在屏幕边上，被推会穿帮。
                    if (a.EdgeHidden || b.EdgeHidden || a.Tuck > 0.01 || b.Tuck > 0.01) continue;
                    if (a.Mode == "edge" || b.Mode == "edge") continue;
                    double avx, avy, bvx, bvy;
                    MotionOf(a, walkSpeed, out avx, out avy);
                    MotionOf(b, walkSpeed, out bvx, out bvy);
                    // 按相对速度把检测盒撑开：扔得快的宠物不会一帧"穿"过对方。
                    double growX = Math.Min(40, Math.Abs(avx - bvx) * dt * 0.5);
                    double growY = Math.Min(40, Math.Abs(avy - bvy) * dt * 0.5);
                    double overlapX = Math.Min(a.X + a.Width + growX, b.X + b.Width + growX) - Math.Max(a.X - growX, b.X - growX);
                    if (overlapX <= 0) continue;
                    double overlapY = Math.Min(a.Y + a.Height + growY, b.Y + b.Height + growY) - Math.Max(a.Y - growY, b.Y - growY);
                    if (overlapY <= 0) continue;
                    bool aFree = a.Mode != "drag";
                    bool bFree = b.Mode != "drag";
                    if (!aFree && !bFree) continue;
                    double shareA = aFree && bFree ? 0.5 : (aFree ? 1.0 : 0.0);
                    double shareB = 1.0 - shareA;
                    double dirX = 0;
                    double dirY = 0;
                    if (overlapX <= overlapY)
                    {
                        dirX = b.X + b.Width / 2 >= a.X + a.Width / 2 ? 1.0 : -1.0;
                        a.X -= dirX * overlapX * shareA;
                        b.X += dirX * overlapX * shareB;
                    }
                    else
                    {
                        dirY = b.Y + b.Height / 2 >= a.Y + a.Height / 2 ? 1.0 : -1.0;
                        a.Y -= dirY * overlapY * shareA;
                        b.Y += dirY * overlapY * shareB;
                    }
                    double pushB = Carry(a, b, dirX, dirY, shareB, carryFactor);
                    double pushA = Carry(b, a, -dirX, -dirY, shareA, carryFactor);
                    if (shareA > 0 && a.Mode == "walk") a.StartWalk();   // 撞上了就换条路走
                    if (shareB > 0 && b.Mode == "walk") b.StartWalk();
                    ClampToScreen(a);
                    ClampToScreen(b);
                    a.ApplyPosition();
                    b.ApplyPosition();
                    touched = true;
                    // 撞得狠才有台词，并且限流，不然两只挨着会一直叭叭。
                    double push = Math.Max(pushA, pushB);
                    if (push >= 450 && (now - lastBumpQuipAt).TotalSeconds > 9 && Program.Rng.NextDouble() < 0.7)
                    {
                        a.SetEmotion("startle", 1.8);
                        b.SetEmotion("angry", 2.2);
                        a.PlayPackOneShot("bump", 1.1);
                        b.PlayPackOneShot("bump", 1.1);
                        string quip = QuipFor("bump");
                        if (!string.IsNullOrEmpty(quip))
                        {
                            lastBumpQuipAt = now;
                            ShowBubbleText(pushB >= pushA ? b : a, quip);
                        }
                    }
                }
            }
            return touched;
        }

        /// <summary>碰撞推挤后别把宠物推出屏幕。</summary>
        private static void ClampToScreen(PetWindow pet)
        {
            ScreenArea area = ScreenArea.ForPoint(pet.X + pet.Width / 2, pet.Y + pet.Height / 2);
            pet.X = Math.Max(area.Left, Math.Min(area.Right - pet.Width, pet.X));
            pet.Y = Math.Max(area.Top, Math.Min(area.Bottom - pet.Height, pet.Y));
        }

        /// <summary>估算一只宠物当前的移动速度（拖拽中的由鼠标决定，碰撞里不推挤它）。</summary>
        private static void MotionOf(PetWindow pet, double walkSpeed, out double vx, out double vy)
        {
            if (pet.Mode == "thrown")
            {
                vx = pet.VelocityX;
                vy = pet.VelocityY;
                return;
            }
            if (pet.Mode == "walk")
            {
                double dx = pet.TargetX - pet.X;
                double dy = pet.TargetY - pet.Y;
                double distance = Math.Sqrt(dx * dx + dy * dy);
                if (distance > 1)
                {
                    vx = dx / distance * walkSpeed * pet.SpeedFactor;
                    vy = dy / distance * walkSpeed * pet.SpeedFactor;
                    return;
                }
            }
            vx = 0;
            vy = 0;
        }

        /// <summary>
        /// 把 from 的动量沿碰撞方向递给 to：够快才会把对方带着滑出去；对方动不了（被用户
        /// 拖着）时，撞人者像撞到墙一样弹回来。返回实际递出去的推力（0 = 没撞实）。
        /// </summary>
        private static double Carry(PetWindow from, PetWindow to, double dirX, double dirY, double share, double factor)
        {
            if (from.Mode != "thrown") return 0;
            double along = from.VelocityX * dirX + from.VelocityY * dirY;
            if (along <= 90) return 0;   // 擦着/蹭着而已，不算撞上
            if (share <= 0)
            {
                from.VelocityX -= 1.55 * along * dirX;
                from.VelocityY -= 1.55 * along * dirY;
                return 0;
            }
            double push = Math.Min(along * factor, 1500);
            to.VelocityX = ClampSpeed(to.VelocityX + dirX * push);
            to.VelocityY = ClampSpeed(to.VelocityY + dirY * push);
            to.Mode = "thrown";
            to.Bob.Y = 0;
            if (dirX != 0) to.SetFacing(dirX > 0 ? 1 : -1);
            // 撞人者吃掉一部分动量，像真的撞到了东西。
            from.VelocityX -= 0.35 * along * dirX;
            from.VelocityY -= 0.35 * along * dirY;
            return push;
        }

        private static double ClampSpeed(double value)
        {
            if (value > 2000) return 2000;
            if (value < -2000) return -2000;
            return value;
        }

        /// <summary>Maps the current mode/action to an animation state and advances frames.</summary>
        private static void AdvanceFrames(PetWindow pet, double dt, bool sleeping)
        {
            string state = "idle";
            double rate = 1.0;
            if (!string.IsNullOrEmpty(pet.Action))
            {
                state = pet.Action;
                rate = pet.Action == "sleep" ? 0.45 : (pet.Action == "lie" ? 0.6 : 1.0);
            }
            else if (pet.Mode == "walk")
            {
                state = "walk";
                rate = Math.Max(0.6, pet.SpeedFactor);
            }
            else if (pet.Mode == "edge")
            {
                if (pet.IsPetPack)
                {
                    string edgeState = pet.HasPackState("edge") ? "edge" : "walk";
                    state = pet.Tuck >= 0.5 && pet.HasPackState("peek") ? "peek" : edgeState;
                }
                else
                {
                    state = pet.Tuck >= 0.5 ? "peek" : "walk";
                }
                rate = 1.3;
            }
            else if (pet.Mode == "thrown")
            {
                if (!pet.IsPetPack) state = "spin";
                else if (pet.HasPackState("fall")) state = "fall";
                else if (pet.HasPackState("spin")) state = "spin";
                else if (pet.HasPackState("drag")) state = "drag";
                else state = "walk";
                rate = 1.5;
            }
            else if (pet.Mode == "drag")
            {
                state = !pet.IsPetPack || pet.HasPackState("drag") ? "drag" : "walk";
                rate = 1.5;
            }
            else if (sleeping)
            {
                state = "sleep";
                rate = 0.35;
            }
            else if (pet.EdgeHidden)
            {
                if (!pet.IsPetPack) state = "peek";
                else if (pet.HasPackState("peek")) state = "peek";
                else state = pet.HasPackState("edge") ? "edge" : "idle";
                rate = 0.7;
            }
            pet.SetAnimState(state);
            pet.Advance(dt, rate);
        }

        /// <summary>
        /// Edge-hide transition: the pet squeezes itself against the screen edge (narrower
        /// window, whole sprite compressed) instead of being clipped by the screen border.
        /// </summary>
        private void UpdateEdgeSlide(PetWindow pet, DateTime now, double dt)
        {
            double step = dt / 0.45;
            if (pet.Tuck < pet.TargetTuck) pet.Tuck = Math.Min(pet.TargetTuck, pet.Tuck + step);
            else if (pet.Tuck > pet.TargetTuck) pet.Tuck = Math.Max(pet.TargetTuck, pet.Tuck - step);
            pet.ApplyTuck(pet.Tuck);

            ScreenArea area = ScreenArea.ForPoint(pet.EdgeShownX + pet.BaseWidth / 2, pet.EdgeShownY + pet.BaseHeight / 2);
            double tuckX = pet.EdgeShownX;
            double tuckY = pet.EdgeShownY;
            // 收进去 = 整只滑到屏幕外面，只在屏幕边上留一条（不做压缩变形）。
            // 留多少按贴边方向的尺寸取比例：留得太少只剩一条缝，多了才看得清是"头探出来"。
            double sliverRatio = cfg.GetNumberAt("edgeHide.sliverRatio", 0.32);
            if (sliverRatio < 0.04) sliverRatio = 0.04;
            if (sliverRatio > 0.8) sliverRatio = 0.8;
            double sideSize = (pet.EdgeSide == "left" || pet.EdgeSide == "right") ? pet.Width : pet.Height;
            double sliver = Math.Round(sideSize * sliverRatio);
            if (sliver < 10) sliver = 10;
            if (pet.EdgeSide == "left") tuckX = area.Left - pet.Width + sliver;
            else if (pet.EdgeSide == "right") tuckX = area.Right - sliver;
            else if (pet.EdgeSide == "top") tuckY = area.Top - pet.Height + sliver;
            else if (pet.EdgeSide == "bottom") tuckY = area.Bottom - sliver;
            // 落位回弹/呼吸：往屏幕里外挪几像素（TuckOffset 正值 = 更深）
            if (pet.EdgeSide == "left") tuckX -= pet.TuckOffset;
            else if (pet.EdgeSide == "right") tuckX += pet.TuckOffset;
            else if (pet.EdgeSide == "top") tuckY -= pet.TuckOffset;
            else if (pet.EdgeSide == "bottom") tuckY += pet.TuckOffset;
            double eased = pet.Tuck * pet.Tuck * (3 - 2 * pet.Tuck);   // 缓入缓出，滑动不愣
            pet.X = pet.EdgeShownX + (tuckX - pet.EdgeShownX) * eased;
            pet.Y = pet.EdgeShownY + (tuckY - pet.EdgeShownY) * eased;
            pet.ApplyPosition();

            if (Math.Abs(pet.Tuck - pet.TargetTuck) > 0.001) return;

            bool hidden = pet.TargetTuck >= 0.999;
            if (hidden) pet.EdgeHidden = true;
            else if (pet.TargetTuck <= 0.001) pet.EdgeHidden = false;
            pet.OnTuckSettled(hidden);   // 回弹一下（收进去时再喷一小团灰）
            pet.Mode = "idle";
            ScheduleEdgeHide(pet, now);
        }

        // ------------------------------------------------------------ 动作动画
        private void AnimateAction(PetWindow pet, DateTime now, double p)
        {
            if (pet.IsPetPack) return;
            switch (pet.Action)
            {
                case "hop":
                    pet.Bob.Y = -30 * Math.Sin(Math.PI * p);
                    break;
                case "bounce":
                    pet.Bob.Y = -26 * Math.Abs(Math.Sin(3 * Math.PI * p));
                    break;
                case "spin":
                    pet.Spin.Angle = 360 * p;
                    break;
                case "roll":
                    pet.Spin.Angle = -720 * p;
                    pet.Bob.Y = -14 * Math.Sin(Math.PI * p);
                    break;
                case "wobble":
                    pet.Spin.Angle = 14 * Math.Sin(6 * Math.PI * p);
                    break;
                case "nod":
                    pet.Bob.Y = -6 * Math.Abs(Math.Sin(3 * Math.PI * p));
                    pet.Spin.Angle = 3 * Math.Sin(6 * Math.PI * p);
                    break;
                case "shake":
                    pet.Bob.X = 7 * Math.Sin(6 * Math.PI * p);
                    pet.Spin.Angle = 4 * Math.Sin(6 * Math.PI * p);
                    break;
                case "wave":
                    pet.Spin.Angle = 11 * Math.Sin(8 * Math.PI * p);
                    pet.Bob.Y = -4 * Math.Abs(Math.Sin(4 * Math.PI * p));
                    break;
                case "clap":
                    {
                        double k = Math.Abs(Math.Sin(8 * Math.PI * p));
                        pet.Squash.ScaleX = 1 + 0.12 * k;
                        pet.Bob.Y = -3 * k;
                        break;
                    }
                case "stretch":
                    {
                        double k = Math.Sin(Math.PI * p);
                        pet.Squash.ScaleY = 1 + 0.18 * k;
                        pet.Squash.ScaleX = 1 - 0.10 * k;
                        break;
                    }
                case "yawn":
                    {
                        double k = Math.Sin(Math.PI * p);
                        pet.Squash.ScaleY = 1 + 0.22 * k;
                        pet.Bob.Y = -5 * k;
                        break;
                    }
                case "shy":
                    {
                        double k = Math.Sin(Math.PI * p);
                        pet.Squash.ScaleY = 1 - 0.14 * k;
                        pet.Spin.Angle = -6 * k;
                        pet.Bob.Y = 2 * Math.Sin(2 * Math.PI * p);
                        break;
                    }
                case "startle":
                    {
                        double k = Math.Sin(Math.PI * p);
                        pet.Squash.ScaleX = 1 + 0.14 * k;
                        pet.Squash.ScaleY = 1 + 0.14 * k;
                        pet.Bob.Y = -18 * k;
                        pet.Spin.Angle = 3 * Math.Sin(10 * Math.PI * p);
                        break;
                    }
                case "lie":
                case "sleep":
                    {
                        double down = Math.Min(1, p / 0.15);
                        double back = p > 0.85 ? Math.Min(1, (p - 0.85) / 0.15) : 0;
                        double amount = Math.Max(0, down - back);
                        pet.Spin.Angle = 76 * amount;
                        pet.Bob.Y = 6 * amount + Math.Sin(now.TimeOfDay.TotalSeconds * 1.6) * 1.2 * amount;
                        break;
                    }
                case "look":
                    {
                        bool left = (p > 0.2 && p < 0.5) || (p > 0.7 && p < 0.9);
                        pet.SetFacing(left ? -1 : 1);
                        break;
                    }
                case "greet":
                    if (p < 0.4)
                    {
                        pet.Bob.Y = -24 * Math.Sin(Math.PI * (p / 0.4));
                    }
                    else
                    {
                        double q = (p - 0.4) / 0.6;
                        pet.Spin.Angle = 11 * Math.Sin(8 * Math.PI * q);
                        pet.Bob.Y = -4 * Math.Abs(Math.Sin(4 * Math.PI * q));
                    }
                    break;
                case "pace":
                    {
                        pet.X = pet.ActionOriginX + 130 * Math.Sin(2 * Math.PI * p);
                        pet.SetFacing(Math.Cos(2 * Math.PI * p) > 0 ? 1 : -1);
                        pet.Bob.Y = -2 * Math.Abs(Math.Sin(10 * Math.PI * p));
                        pet.ApplyPosition();
                        break;
                    }
                case "peek":
                    {
                        double out1 = Math.Min(1, p / 0.35);
                        double back2 = p > 0.65 ? Math.Min(1, (p - 0.65) / 0.35) : 0;
                        double amount = Math.Max(0, out1 - back2);
                        ScreenArea area = ScreenArea.ForPoint(pet.X + pet.Width / 2, pet.Y + pet.Height / 2);
                        bool towardsLeft = pet.ActionOriginX + pet.Width / 2 < (area.Left + area.Right) / 2;
                        double peekRatio = cfg.GetNumberAt("edgeHide.sliverRatio", 0.32);
                        if (peekRatio < 0.04) peekRatio = 0.04;
                        if (peekRatio > 0.8) peekRatio = 0.8;
                        double sliverPeek = Math.Max(10, Math.Round(pet.Width * peekRatio));
                        double hidden = towardsLeft ? area.Left - pet.Width + sliverPeek : area.Right - sliverPeek;
                        pet.X = pet.ActionOriginX + (hidden - pet.ActionOriginX) * amount;
                        pet.Bob.Y = -2 * Math.Abs(Math.Sin(8 * Math.PI * p)) * (1 - amount);
                        pet.ApplyPosition();
                        break;
                    }
                case "follow":
                    {
                        System.Drawing.Point cursor = System.Windows.Forms.Cursor.Position;
                        double dx = cursor.X - (pet.X + pet.Width / 2);
                        double dy = cursor.Y - (pet.Y + pet.Height / 2);
                        double distance = Math.Sqrt(dx * dx + dy * dy);
                        if (distance < 90)
                        {
                            pet.ActionUntil = DateTime.UtcNow.AddMilliseconds(-1);
                        }
                        else
                        {
                            double followSpeed = cfg.GetNumber("walk", "speedPxPerSec", 72) * 2.2;
                            pet.X += dx / distance * followSpeed * (1.0 / 25.0);
                            pet.Y += dy / distance * followSpeed * (1.0 / 25.0);
                            if (Math.Abs(dx) > 6) pet.SetFacing(dx > 0 ? 1 : -1);
                            pet.Bob.Y = -Math.Abs(Math.Sin(now.TimeOfDay.TotalSeconds * 10)) * 3;
                            pet.ApplyPosition();
                        }
                        break;
                    }
            }
        }

        private void ScheduleNextAction(PetWindow pet, DateTime now)
        {
            pet.NextActionAt = now.AddSeconds(ActionTiming.PauseSeconds(cfg));
        }

        /// <summary>散步时只叠加不抢位置的姿态动作；pace/peek/follow 这类会改坐标的动作
        /// 仍然留给原地表演，避免和行走路径打架。</summary>
        private static bool CanWalkAction(PetWindow pet, string key)
        {
            if (pet.IsPetPack) return pet.UsesPackActions && pet.PackActionCanWalk(key);
            return key != "pace" && key != "peek" && key != "follow" && key != "lie" && key != "sleep";
        }

        private string PickWalkingAction(PetWindow pet)
        {
            if (pet.IsPetPack) return pet.UsesPackActions ? pet.PickPackAction(true) : "";
            List<string> usable = new List<string>();
            foreach (string key in cfg.GetStrings("actions", "pool"))
            {
                if (CanWalkAction(pet, key)) usable.Add(key);
            }
            if (usable.Count == 0) return "";
            if (usable.Count > 1) usable.Remove(pet.LastActionKey);
            return usable[Program.Rng.Next(usable.Count)];
        }

        private void EndAction(PetWindow pet, DateTime now, bool keepWalking)
        {
            string finished = pet.Action;
            pet.Action = "";
            // pace/peek 会自己移动，结束后回到动作起点；其它动作不重置位置，
            // 否则散步途中做完动作会被拽回原地。
            if (!keepWalking && !pet.IsPetPack && (finished == "pace" || finished == "peek"))
            {
                pet.X = pet.ActionOriginX;
                pet.ApplyPosition();
            }
            pet.Spin.Angle = 0;
            pet.Squash.ScaleX = 1;
            pet.Squash.ScaleY = 1;
            pet.Bob.X = 0;
            pet.Bob.Y = 0;
            if (keepWalking)
            {
                pet.Mode = "walk";
                ScheduleNextAction(pet, now);
            }
            else
            {
                pet.SetIdle();
            }
        }

        // ------------------------------------------------------------ 贴边隐藏
        private bool EdgeHideEnabled(PetWindow pet)
        {
            if (!cfg.GetFlagAt("edgeHide.enabled", true)) return false;
            return !string.IsNullOrEmpty(pet.EdgeSide);
        }

        private void ScheduleEdgeHide(PetWindow pet, DateTime now)
        {
            if (!string.IsNullOrEmpty(pet.PinnedActionKey()))
            {
                pet.EdgeHideAt = DateTime.MaxValue;
                return;
            }
            if (!EdgeHideEnabled(pet) || pet.EdgeHidden)
            {
                pet.EdgeHideAt = DateTime.MaxValue;
                return;
            }
            double delay = cfg.GetNumberAt("edgeHide.delaySec", 10);
            if (delay < 1) delay = 1;
            pet.EdgeHideAt = now.AddSeconds(delay);
        }

        private void RevealPet(PetWindow pet)
        {
            // Also pull back a half-finished tuck: hovering must never let it finish hiding.
            if (!pet.EdgeHidden && pet.Tuck < 0.05) return;
            pet.StartEdgeSlide(false);
            pet.EdgeHideAt = DateTime.MaxValue;
        }

        /// <summary>
        /// Drops a stale edge attachment once the pet is no longer sitting against that edge
        /// (e.g. it walked away or was clicked in the middle of the screen). Without this the
        /// pet would later slide back to the remembered edge and tuck itself in.
        /// </summary>
        private static void RefreshEdgeAttachment(PetWindow pet)
        {
            if (string.IsNullOrEmpty(pet.EdgeSide)) return;
            ScreenArea area = ScreenArea.ForPoint(pet.X + pet.Width / 2, pet.Y + pet.Height / 2);
            bool attached =
                (pet.EdgeSide == "left" && pet.X <= area.Left + 10) ||
                (pet.EdgeSide == "right" && pet.X + pet.Width >= area.Right - 10) ||
                (pet.EdgeSide == "top" && pet.Y <= area.Top + 10) ||
                (pet.EdgeSide == "bottom" && pet.Y + pet.Height >= area.Bottom - 10);
            if (!attached)
            {
                pet.EdgeSide = "";
                pet.EdgeHideAt = DateTime.MaxValue;
            }
        }

        // ------------------------------------------------------------ 交互
        private void OnPetClicked(object sender, EventArgs e)
        {
            PetWindow pet = sender as PetWindow;
            if (pet == null) return;
            bool snapped = pet.EdgeHidden || pet.Tuck > 0.01 || !string.IsNullOrEmpty(pet.EdgeSide);
            if (snapped)
            {
                // Snapped pets can still show pages, but never untuck or play a click action.
                string edgeQuip = QuipFor(pet.Sleeping ? "sleep" : "greeting");
                int edgePage = pet.PageIndex % PageCount();
                pet.PageIndex = (edgePage + 1) % PageCount();
                bubbleIsMessage = false;
                bubbleIsAlert = false;
                bubblePage = edgePage;
                bubbleQuip = edgeQuip;
                ShowView(pet, BuildPage(edgePage), BubbleSeconds());
                return;
            }
            RefreshEdgeAttachment(pet);   // a click stops it in place instead of re-attaching
            ScheduleEdgeHide(pet, DateTime.UtcNow);
            pet.SetEmotion("happy", 2.5);
            pet.PlayPackOneShot("click", 1.8);
            string quip = QuipFor(pet.Sleeping ? "sleep" : "greeting");
            int page = pet.PageIndex % PageCount();
            pet.PageIndex = (page + 1) % PageCount();
            bubbleIsMessage = false;
            bubbleIsAlert = false;
            bubblePage = page;
            bubbleQuip = quip;
            ShowView(pet, BuildPage(page), BubbleSeconds());
        }

        private void OnPetContextRequested(object sender, EventArgs e)
        {
            TogglePanel(sender as PetWindow);
        }

        private void OnPetHovered(object sender, EventArgs e)
        {
            // 悬停不再自动弹出：收起的桌宠只有被拖拽才会出来。
        }

        private void OnPetMoved(object sender, EventArgs e)
        {
            Bubble.Position(false);
        }

        private void OnPetWheelZoomed(object sender, bool up)
        {
            PetWindow pet = sender as PetWindow;
            if (pet == null && Pets.Count > 0) pet = Pets[0];
            if (pet == null) return;
            double scale = pet.Scale;
            scale = up ? scale * 1.07 : scale / 1.07;
            pet.SetScale(scale, true);
            cfg.Save();
            Bubble.Position(true);
            if (panel.IsVisible) panel.Update();
        }

        public void ShowPanel()
        {
            // Explicitly requested (--settings or the second-instance wake-up): keep the card
            // open until it is closed, instead of hiding when it loses focus.
            panel.Pinned = true;
            panel.ShowFor(Pets.Count > 0 ? Pets[0] : null);
        }

        private void TogglePanel(PetWindow pet)
        {
            // 面板开着时，右键任意一只桌宠都收起面板。这样贴边只露一条、
            // 或者面板对应的那只已经被循环涨落换掉时，也不会出现关不掉的情况。
            // 失焦自动隐藏会在右键事件之前发生，所以“刚隐藏”也要按关闭处理，
            // 否则会出现收起后立刻又被右键重新打开。
            bool recentlyHidden = (DateTime.UtcNow - panel.LastHiddenAt).TotalMilliseconds < 350;
            if (panel.IsVisible || recentlyHidden)
            {
                panel.HidePanel();
                return;
            }
            if (pet != null) panel.ShowFor(pet);
        }

        public void PerformAction(PetWindow pet, string key, bool speak)
        {
            if (pet == null && Pets.Count > 0) pet = Pets[0];
            if (pet == null) return;
            if (pet.EdgeHidden) RevealPet(pet);
            if (pet.Mode == "walk")
            {
                if (key == "random") key = PickWalkingAction(pet);
                else if (!CanWalkAction(pet, key)) return;
                if (string.IsNullOrEmpty(key)) return;
                pet.NextActionAt = DateTime.MaxValue;
            }
            pet.StartAction(key);
            if (string.IsNullOrEmpty(pet.LastActionKey)) return;
            string done = pet.LastActionKey;
            if (done == "fireworks" || done == "hearts" || done == "sparkles" || done == "confetti"
                || done == "wave" || done == "greet")
            {
                pet.SetEmotion("happy", 3.0);
            }
            else if (done == "sleep" || done == "lie") pet.SetEmotion("sleepy", 5.0);
            else if (done == "startle") pet.SetEmotion("startle", 1.5);
            else if (done == "shy") pet.SetEmotion("shy", 2.5);
            if (speak && cfg.GetFlag("alert", "quipsOn", true))
            {
                ShowBubbleText(pet, QuipFor(pet.LastActionKey));
            }
        }

        // ------------------------------------------------------------ 分身
        public int CloneCap()
        {
            double max = cfg.GetNumberAt("clones.max", cfg.GetNumber("", "maxClones", 6));
            int configured = (int)max;
            if (configured <= 0) return SafeCloneLimit;
            return Math.Min(configured, SafeCloneLimit);
        }

        public int ConfiguredCloneCap()
        {
            double max = cfg.GetNumberAt("clones.max", cfg.GetNumber("", "maxClones", 6));
            int configured = (int)max;
            return configured <= 0 ? 0 : Math.Min(configured, SafeCloneLimit);
        }

        public int CloneFloor()
        {
            int floor = (int)cfg.GetNumberAt("clones.min", 1);
            int cap = CloneCap();
            if (floor < 1) floor = 1;
            return floor > cap ? cap : floor;
        }

        /// <summary>Picks the look for a new pet: random when roles.random is on (clones prefer
        /// something other than their parent), otherwise the configured role.</summary>
        public static string PickRole(string avoid)
        {
            Config config = Program.CurrentConfig;
            string configured = config == null ? "" : config.GetString("", "role", "");
            if (config == null || !config.GetFlagAt("roles.random", true)) return configured;
            List<string> pool = AppPaths.RoleFiles();
            if (pool.Count == 0) return configured;
            if (pool.Count < 6) pool.Add("");   // small libraries: let the built-in whale join in
            string picked = pool[Program.Rng.Next(pool.Count)];
            int guard = 0;
            while (picked == avoid && guard < 8)
            {
                picked = pool[Program.Rng.Next(pool.Count)];
                guard++;
            }
            return picked;
        }

        private bool AutoSplitDue(DateTime now)
        {
            if (!cfg.GetFlagAt("clones.auto", true))
            {
                nextCloneStepAt = DateTime.MinValue;
                cloneFalling = false;
                return false;
            }
            if (Pets.Count == 0) return false;
            int cap = CloneCap();
            int floor = CloneFloor();
            if (cap > 0 && floor > cap) floor = cap;
            bool cycle = cfg.GetFlagAt("clones.cycle", true);
            if (!cycle) cloneFalling = false;

            // 到顶/到底时只切换一次阶段，并留一个间隔作为停顿；
            // 阶段确定后按同一节奏逐只收或逐只涨，不会在顶上反复横跳。
            bool phaseChanged = false;
            if (cap > 0 && Pets.Count >= cap)
            {
                if (!cycle)
                {
                    nextCloneStepAt = DateTime.MaxValue;
                    return false;
                }
                if (!cloneFalling)
                {
                    cloneFalling = true;
                    phaseChanged = true;
                }
            }
            else if (Pets.Count <= floor)
            {
                if (cloneFalling)
                {
                    cloneFalling = false;
                    phaseChanged = true;
                }
            }
            if (phaseChanged)
            {
                nextCloneStepAt = now.AddSeconds(SplitInterval());
                return false;
            }

            if (nextCloneStepAt == DateTime.MinValue || nextCloneStepAt == DateTime.MaxValue)
            {
                nextCloneStepAt = now.AddSeconds(SplitInterval());
                return false;
            }
            if (now < nextCloneStepAt) return false;

            if (cloneFalling)
            {
                if (Pets.Count > floor)
                {
                    RemoveClone(true);
                    nextCloneStepAt = now.AddSeconds(SplitInterval());
                    return true;
                }
                cloneFalling = false;
                nextCloneStepAt = now.AddSeconds(SplitInterval());
                return false;
            }

            if (cap > 0 && Pets.Count >= cap)
            {
                cloneFalling = true;
                nextCloneStepAt = now.AddSeconds(SplitInterval());
                return false;
            }
            AddClone();
            nextCloneStepAt = now.AddSeconds(SplitInterval());
            return true;
        }

        private double SplitInterval()
        {
            double seconds = cfg.GetNumberAt("clones.pauseMinSec", 60);
            if (seconds < 5) seconds = 5;
            double jitter = cfg.GetNumberAt("clones.jitter", 0.5);
            if (jitter < 0) jitter = 0;
            if (jitter > 3) jitter = 3;
            return seconds * (1 + Program.Rng.NextDouble() * jitter);
        }

        public void AddClone()
        {
            int cap = CloneCap();
            if (cap > 0 && Pets.Count >= cap) return;
            if (Pets.Count == 0) return;
            SuspendedPet revive = null;
            if (suspended.Count > 0)
            {
                revive = suspended[suspended.Count - 1];
                suspended.RemoveAt(suspended.Count - 1);
            }
            PetWindow source = Pets[Program.Rng.Next(Pets.Count)];
            PetWindow pet = new PetWindow(cfg);
            int cloneSlot = revive == null ? NextFreeCloneSlot() : revive.Slot;
            pet.Slot = cloneSlot;
            if (revive != null)
            {
                Log.Write("clone revive slot=" + cloneSlot.ToString(CultureInfo.InvariantCulture)
                    + " role=" + (revive.Role == "" ? "__mascot" : revive.Role)
                    + " scale=" + revive.Scale.ToString("0.###", CultureInfo.InvariantCulture));
                // 循环涨落收回的分身原样回来：同一角色、同一大小、同一位置，
                // 不会再随机换一只新的。
                pet.ApplyRole(revive.Role);
                pet.SetScale(revive.Scale, false);
                pet.ResizePet();
                ScreenArea area = ScreenArea.ForPoint(revive.X + pet.Width / 2, revive.Y + pet.Height / 2);
                double x = Math.Max(area.Left, Math.Min(revive.X, area.Right - pet.Width));
                double y = Math.Max(area.Top, Math.Min(revive.Y, area.Bottom - pet.Height));
                pet.Place(x, y);
            }
            else
            {
                pet.ApplyRole(PickCloneRole(source.Role, cloneSlot));
                Log.Write("clone new slot=" + cloneSlot.ToString(CultureInfo.InvariantCulture)
                    + " role=" + (pet.Role == "" ? "__mascot" : pet.Role)
                    + " scale=" + pet.Scale.ToString("0.###", CultureInfo.InvariantCulture));
                pet.ResizePet();
                pet.Place(source.X + 60 + Program.Rng.Next(0, 60), source.Y - 30 - Program.Rng.Next(0, 60));
            }
            WirePet(pet);
            Pets.Add(pet);
            pet.Show();
            pet.SetIdle();
            ScheduleEdgeHide(pet, DateTime.UtcNow);
        }

        private int NextFreeCloneSlot()
        {
            HashSet<int> used = new HashSet<int>();
            foreach (PetWindow pet in Pets) used.Add(pet.Slot);
            for (int slot = 1; ; slot++)
            {
                if (!used.Contains(slot)) return slot;
            }
        }

        private string PickCloneRole(string sourceRole, int slot)
        {
            string mode = cfg.GetString("clones", "roleMode", "random");
            if (string.Equals(mode, "slots", StringComparison.OrdinalIgnoreCase))
            {
                Dictionary<string, object> selected = cfg.GetObject("", "cloneSlots");
                object value;
                if (selected.TryGetValue(slot.ToString(CultureInfo.InvariantCulture), out value))
                {
                    string chosen = value == null ? "" : Convert.ToString(value, CultureInfo.InvariantCulture);
                    return string.Equals(chosen, "@current", StringComparison.OrdinalIgnoreCase)
                        ? sourceRole
                        : chosen;
                }
                return sourceRole;
            }
            if (string.Equals(mode, "manual", StringComparison.OrdinalIgnoreCase))
            {
                return cfg.GetString("clones", "role", "");
            }
            if (string.Equals(mode, "current", StringComparison.OrdinalIgnoreCase))
            {
                return sourceRole;
            }
            return PickRole(sourceRole);
        }

        public void RemoveClone()
        {
            RemoveClone(false);
        }

        /// <summary>suspend=true 用于循环涨落：收回时记住这只，涨回时原样恢复。</summary>
        public void RemoveClone(bool suspend)
        {
            if (Pets.Count <= 1) return;
            PetWindow pet = Pets[Pets.Count - 1];
            if (suspend)
            {
                Log.Write("clone suspend slot=" + pet.Slot.ToString(CultureInfo.InvariantCulture)
                    + " role=" + (pet.Role == "" ? "__mascot" : pet.Role)
                    + " scale=" + pet.Scale.ToString("0.###", CultureInfo.InvariantCulture));
                suspended.Add(new SuspendedPet
                {
                    Slot = pet.Slot,
                    Role = pet.Role,
                    Scale = pet.Scale,
                    X = pet.EdgeHidden ? pet.EdgeShownX : pet.X,
                    Y = pet.EdgeHidden ? pet.EdgeShownY : pet.Y
                });
            }
            Pets.RemoveAt(Pets.Count - 1);
            pet.Close();
            pet = null;
            GC.Collect(2, GCCollectionMode.Optimized, false);
        }

        // ------------------------------------------------------------ 气泡
        public void ShowBubble(PetWindow pet, string text)
        {
            if (pet == null && Pets.Count > 0) pet = Pets[0];
            ShowBubbleText(pet, text);
        }

        /// <summary>Shows a short message bubble (quips, alerts, peak transitions).</summary>
        private void ShowBubbleText(PetWindow pet, string text, int seconds)
        {
            if (pet == null) return;
            if (pet.EdgeHidden) RevealPet(pet);
            bubbleIsMessage = true;
            bubbleIsAlert = false;
            messageTitle = "";
            messageText = text;
            bubbleQuip = text;
            ShowView(pet, BuildMessageView(text), seconds);
        }

        private void ShowBubbleText(PetWindow pet, string text)
        {
            ShowBubbleText(pet, text, BubbleSeconds());
        }

        private void ShowAlert(PetWindow pet, string title, string text)
        {
            ShowAlert(pet, title, text, "");
        }

        /// <summary>
        /// 提醒气泡：大数字槽位只放短值（金额/百分比/条数），长解释放备注栏换行显示，
        /// 否则会被 Viewbox 缩成蚂蚁字。
        /// </summary>
        private void ShowAlert(PetWindow pet, string title, string text, string note)
        {
            if (pet == null) return;
            pet.SetEmotion("worried", 8.0);
            bubbleIsMessage = true;
            bubbleIsAlert = true;
            alertTitle = title;
            alertText = text;
            alertNote = note;
            messageTitle = title;
            messageText = string.IsNullOrEmpty(note) ? text : text + " " + note;
            bubbleQuip = "";
            // 明细越长，气泡停得越久，够看完。
            int seconds = 8 + Math.Min(8, note.Length / 24);
            ShowView(pet, BuildAlertView(title, text, note), seconds);
        }

        private void ShowView(PetWindow pet, BubbleView view, int seconds)
        {
            if (pet == null) return;
            Bubble.ApplyStyle(cfg);
            Bubble.HideAt = DateTime.UtcNow.AddSeconds(Demo ? 3600 : seconds);
            Bubble.ShowFor(pet, view);
        }

        private int BubbleSeconds()
        {
            double seconds = cfg.GetNumber("bubble", "seconds", 7);
            if (seconds < 3) seconds = 3;
            return (int)seconds;
        }

        private BubbleView BuildMessageView(string text)
        {
            Snapshot data = Data.Current;
            ProviderState primary = data.Primary;
            BubbleView view = new BubbleView();
            view.Mode = "message";
            view.Title = primary == null ? "提示" : primary.Name + " · 提示";
            view.Quip = text;
            view.PeakText = PeakBubbleText();
            view.PeakNow = data.IsPeak;
            view.Source = SourceLine(data);
            return view;
        }

        private BubbleView BuildAlertView(string title, string text)
        {
            return BuildAlertView(title, text, "");
        }

        private BubbleView BuildAlertView(string title, string text, string note)
        {
            Snapshot data = Data.Current;
            BubbleView view = new BubbleView();
            view.Mode = "alert";
            view.Title = string.IsNullOrEmpty(title) ? "提醒" : title;
            view.Big = text;
            view.BigSize = 15;
            view.BigColor = "#e0433f";
            if (!string.IsNullOrEmpty(note))
            {
                view.Note = note;
                view.NoteError = true;
            }
            if (!string.IsNullOrEmpty(title)
                && title.IndexOf("今日预算", StringComparison.Ordinal) >= 0)
            {
                view.AlertUsed = FirstMoneyValue(text);
                view.AlertLimit = LastMoneyValue(note);
            }
            view.PeakText = PeakBubbleText();
            view.PeakNow = data.IsPeak;
            view.Source = SourceLine(data);
            return view;
        }

        private static double? FirstMoneyValue(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            int symbol = text.IndexOf('¥');
            if (symbol < 0) symbol = text.IndexOf('￥');
            return symbol < 0 ? (double?)null : MoneyValueAt(text, symbol + 1);
        }

        private static double? LastMoneyValue(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            double? result = null;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] != '¥' && text[i] != '￥') continue;
                double? value = MoneyValueAt(text, i + 1);
                if (value.HasValue) result = value;
            }
            return result;
        }

        private static double? MoneyValueAt(string text, int start)
        {
            int index = start;
            while (index < text.Length && char.IsWhiteSpace(text[index])) index++;
            int begin = index;
            while (index < text.Length && (char.IsDigit(text[index]) || text[index] == '.')) index++;
            double value;
            return index > begin
                && double.TryParse(text.Substring(begin, index - begin),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                ? (double?)value : null;
        }

        private string SourceLine(Snapshot data)
        {
            string stamp = data.AtMs > 0
                ? new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(data.AtMs).ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture)
                : "--";
            if (stamp == "--") return "未更新";
            return data.OkCount > 1 ? data.OkCount + " 接口 · " + stamp : stamp;
        }

        /// <summary>Single-topic views (balance, usage, trend, peak, providers and plans).</summary>
        private BubbleView BuildPage(int index)
        {
            Snapshot data = Data.Current;
            ProviderState primary = data.Primary;
            List<string> keys = PageKeys(data);
            if (index < 0 || index >= keys.Count) index = 0;
            string page = keys[index];
            BubbleView view = new BubbleView();
            view.Quip = bubbleQuip;
            view.PageIndex = index;
            view.PageCount = keys.Count;
            view.PeakText = PeakBubbleText();
            view.PeakNow = data.IsPeak;
            view.Source = SourceLine(data);
            bool exactDeepSeekUsage = primary != null && primary.Id == "deepseek"
                && primary.UsageExact && primary.TodayTotalUsage.HasValue;
            bool showKeyUsage = cfg.GetFlagAt("providers.deepseek.showKeyUsage", true);

            if (page == "balance")
            {
                view.Mode = "balance";
                view.Title = primary == null ? "余额" : primary.Name + " 余额";
                view.Big = primary == null ? "--" : Fmt.Money(primary.Balance, primary.Currency);
                view.BigSize = 30;
                if (primary != null && primary.Details.Count > 0)
                {
                    view.Sub = string.Join(" · ", primary.Details.ToArray());
                }
                string localUsageNote = LocalUsageNote(primary);
                if (primary != null && !primary.Ok && !string.IsNullOrEmpty(primary.Error))
                {
                    view.Note = string.IsNullOrEmpty(localUsageNote)
                        ? primary.Error : primary.Error + "；" + localUsageNote;
                    view.NoteError = true;
                }
                else if (!string.IsNullOrEmpty(localUsageNote))
                {
                    view.Note = localUsageNote;
                }
                return view;
            }

            if (page == "today")
            {
                view.Mode = "spend";
                view.Title = primary == null ? "消耗统计" : primary.Name + " 消耗";
                view.Big = primary == null ? "--" : Fmt.Money(primary.TodayUsage, primary.Currency);
                view.BigSize = 28;
                double yesterday = 0;
                double sum = 0;
                int days = 0;
                List<DayUsage> history = data.History;
                Dictionary<string, double> usageByDay = new Dictionary<string, double>(StringComparer.Ordinal);
                foreach (DayUsage item in history)
                {
                    if (item != null && !string.IsNullOrEmpty(item.Day))
                        usageByDay[item.Day] = item.Usage;
                }
                DateTime todayDate = DataService.BeijingNow().Date;
                string yesterdayKey = todayDate.AddDays(-1)
                    .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                usageByDay.TryGetValue(yesterdayKey, out yesterday);
                for (int offset = 1; offset <= 7; offset++)
                {
                    string key = todayDate.AddDays(-offset)
                        .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    double value;
                    if (usageByDay.TryGetValue(key, out value))
                    {
                        sum += value;
                        days++;
                    }
                }
                string currency = primary == null ? "CNY" : primary.Currency;
                view.Trend = new List<DayUsage>();
                foreach (DayUsage item in data.History)
                {
                    if (item == null || string.IsNullOrEmpty(item.Day)) continue;
                    DayUsage copy = new DayUsage();
                    copy.Day = item.Day;
                    copy.Usage = item.Usage;
                    view.Trend.Add(copy);
                }
                view.TrendCurrency = currency;
                view.TrendExact = exactDeepSeekUsage;
                if (exactDeepSeekUsage)
                {
                    view.Rows.Add(new KeyValuePair<string, string>("全部 Key",
                        Fmt.Money(primary.TodayTotalUsage, primary.Currency)));
                    if (showKeyUsage)
                    {
                        view.Rows.Add(new KeyValuePair<string, string>("当前 Key",
                            primary.TodayKeyUsage.HasValue ? Fmt.Money(primary.TodayKeyUsage, primary.Currency) : "未匹配"));
                    }
                    view.Sub = "官方精确成本";
                }
                else
                {
                    view.Rows.Add(new KeyValuePair<string, string>("昨日",
                        Fmt.Money(yesterday, currency)));
                    if (days > 0)
                    {
                        view.Rows.Add(new KeyValuePair<string, string>("近 7 日均",
                            Fmt.Money(sum / days, currency)));
                    }
                    view.Sub = "本机估算";
                }
                if (!exactDeepSeekUsage && primary != null && primary.Id == "deepseek")
                {
                    string platformNote = string.IsNullOrEmpty(primary.UsageError)
                        ? "官方余额接口不含分 Key 用量；填写平台 Token 后可读取精确成本"
                        : "平台成本查询失败：" + primary.UsageError;
                    string localUsageNote = LocalUsageNote(primary);
                    view.Note = string.IsNullOrEmpty(localUsageNote)
                        ? platformNote
                        : platformNote + "；" + localUsageNote;
                    view.NoteError = !string.IsNullOrEmpty(primary.UsageError);
                }
                else if (exactDeepSeekUsage && !string.IsNullOrEmpty(primary.UsageError))
                {
                    view.Note = primary.UsageError;
                }
                else
                {
                    string localUsageNote = LocalUsageNote(primary);
                    if (!string.IsNullOrEmpty(localUsageNote)) view.Note = localUsageNote;
                }
                return view;
            }

            if (page == "trend")
            {
                view.Mode = "trend";
                int days = (int)cfg.GetNumberAt("bubble.trendDays", 7);
                if (days != 14 && days != 30) days = 7;
                Dictionary<string, double> byDay = new Dictionary<string, double>(StringComparer.Ordinal);
                foreach (DayUsage item in data.History)
                {
                    if (item != null && !string.IsNullOrEmpty(item.Day)) byDay[item.Day] = item.Usage;
                }
                double total = 0;
                DateTime today = DataService.BeijingNow().Date;
                for (int offset = days - 1; offset >= 0; offset--)
                {
                    double value;
                    if (byDay.TryGetValue(today.AddDays(-offset).ToString("yyyy-MM-dd",
                            CultureInfo.InvariantCulture), out value))
                        total += value;
                }
                string currency = primary == null ? "CNY" : primary.Currency;
                view.Title = "近 " + days.ToString(CultureInfo.InvariantCulture) + " 天趋势";
                view.Big = Fmt.Money(total, currency);
                view.BigSize = 21;
                view.Sub = Fmt.Money(total / days, currency)
                    + (primary != null && primary.UsageExact ? " · 官方" : " · 本机");
                view.ShowTrend = true;
                view.Trend = data.History;
                view.TrendCurrency = currency;
                view.TrendExact = primary != null && primary.UsageExact;
                string localUsageNote = LocalUsageNote(primary);
                if (!string.IsNullOrEmpty(localUsageNote)) view.Note = localUsageNote;
                return view;
            }

            if (page == "peak")
            {
                view.Mode = "status";
                view.Title = "计价时段";
                view.Big = data.IsPeak ? "标准价" : "优惠价";
                view.BigSize = 21;
                view.BigColor = data.IsPeak ? "#e0433f" : "#2fa24c";
                view.Sub = CountdownText(data.NextChangeAtMs);
                return view;
            }

            if (page.StartsWith("provider:", StringComparison.Ordinal))
            {
                view.Mode = "provider";
                int providerIndex;
                if (!int.TryParse(page.Substring("provider:".Length), NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out providerIndex)
                    || providerIndex < 0 || providerIndex >= data.Providers.Count)
                    return view;
                ProviderState provider = data.Providers[providerIndex];
                view.Title = provider.Name;
                view.BigSize = 25;
                if (!provider.Supported)
                {
                    view.Big = "无公开接口";
                    view.BigSize = 19;
                    view.Sub = "当前平台不提供余额查询";
                }
                else if (!provider.Ok)
                {
                    view.Big = "查询失败";
                    view.BigSize = 21;
                    view.BigColor = "#e0433f";
                    view.Note = provider.Error;
                    view.NoteError = true;
                }
                else
                {
                    view.Big = Fmt.Money(provider.Balance, provider.Currency);
                    view.Sub = provider.SourceText;
                }
                return view;
            }

            if (page.StartsWith("plan:", StringComparison.Ordinal))
            {
                view.Mode = "quota";
                string[] parts = page.Split(':');
                int planIndex;
                if (parts.Length < 3
                    || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out planIndex)
                    || planIndex < 0 || planIndex >= data.Plans.Count)
                    return view;
                PlanState plan = data.Plans[planIndex];
                if (parts[2] == "status" || !plan.Ok || plan.Windows.Count == 0)
                {
                    view.Title = plan.Name;
                    view.Big = plan.Unsupported ? "不可查询" : "查询失败";
                    view.BigSize = 20;
                    view.BigColor = plan.Unsupported ? "" : "#e0433f";
                    view.Note = string.IsNullOrEmpty(plan.ErrorDetail) ? plan.Error : plan.ErrorDetail;
                    view.NoteError = !plan.Unsupported;
                    return view;
                }
                int windowIndex;
                if (!int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out windowIndex)
                    || windowIndex < 0 || windowIndex >= plan.Windows.Count)
                    return view;
                PlanWindow planWindow = plan.Windows[windowIndex];
                view.Title = plan.Name + " · " + planWindow.Label;
                view.Big = planWindow.Percent.ToString("0.#", CultureInfo.InvariantCulture) + "%";
                view.BigSize = 26;
                view.BigColor = planWindow.Percent >= 90 ? "#e0433f"
                    : (planWindow.Percent >= 70 ? "#d88214" : "#2fa24c");
                view.Sub = planWindow.Detail;
                view.ResetText = planWindow.ResetDisplay();
                return view;
            }
            return view;
        }

        private static string LocalUsageNote(ProviderState provider)
        {
            if (provider == null || provider.UsageExact || provider.LocalUsageSinceMs <= 0) return "";
            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime started = epoch.AddMilliseconds(provider.LocalUsageSinceMs).AddHours(8);
            DateTime today = DataService.BeijingNow().Date;
            if (started.Date == today)
            {
                return "本机从 " + started.ToString("HH:mm", CultureInfo.InvariantCulture)
                    + " 开始统计；此前未运行期间无法还原";
            }
            return "本机只统计程序运行期间，停机期间无法还原";
        }

        /// <summary>
        /// 点击气泡循环的页签：计价时段页只在支持错峰计价的主接口（DeepSeek）时出现，
        /// 套餐页有启用套餐时才有——页数随数据变化，索引用取模兜底。
        /// </summary>
        private List<string> PageKeys(Snapshot data)
        {
            List<string> keys = new List<string>();
            keys.Add("balance");
            keys.Add("today");
            if (data.History.Count > 0 && cfg.GetFlag("bubble", "trend", true))
                keys.Add("trend");
            if (data.HasPeak) keys.Add("peak");
            ProviderState primary = data.Primary;
            for (int i = 0; i < data.Providers.Count; i++)
            {
                ProviderState provider = data.Providers[i];
                if (!provider.Enabled || ReferenceEquals(provider, primary)) continue;
                keys.Add("provider:" + i.ToString(CultureInfo.InvariantCulture));
            }
            for (int i = 0; i < data.Plans.Count; i++)
            {
                PlanState plan = data.Plans[i];
                if (!plan.Enabled) continue;
                if (!plan.Ok || plan.Windows.Count == 0)
                {
                    keys.Add("plan:" + i.ToString(CultureInfo.InvariantCulture) + ":status");
                    continue;
                }
                for (int window = 0; window < plan.Windows.Count; window++)
                {
                    keys.Add("plan:" + i.ToString(CultureInfo.InvariantCulture) + ":"
                        + window.ToString(CultureInfo.InvariantCulture));
                }
            }
            return keys;
        }

        private int PageCount()
        {
            return PageKeys(Data.Current).Count;
        }

        internal int DiagnosticPageCount()
        {
            return PageCount();
        }

        internal BubbleView DiagnosticBuildPage(int index, string quip)
        {
            bubbleQuip = quip;
            return BuildPage(index);
        }

        private static string StampOf(long timeMs)
        {
            if (timeMs <= 0) return "--";
            return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(timeMs).ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);
        }

        private static string CountdownText(long nextChangeAtMs)
        {
            long leftMs = nextChangeAtMs - DataService.NowMs();
            if (leftMs < 0) leftMs = 0;
            TimeSpan left = TimeSpan.FromMilliseconds(leftMs);
            return ((int)left.TotalHours).ToString(CultureInfo.InvariantCulture)
                + ":" + left.Minutes.ToString("00", CultureInfo.InvariantCulture)
                + ":" + left.Seconds.ToString("00", CultureInfo.InvariantCulture);
        }

        public string QuipFor(string key)
        {
            if (!cfg.GetFlag("alert", "quipsOn", true)) return "";
            List<string> pool = cfg.GetStrings("quips", key);
            if (pool.Count == 0) pool = cfg.GetStrings("quips", "greeting");
            if (pool.Count == 0) return "";
            return pool[Program.Rng.Next(pool.Count)];
        }

        public string PeakText()
        {
            Snapshot data = Data.Current;
            if (!data.HasPeak) return "";   // 非 DeepSeek 主接口：没有错峰计价，别硬套
            return data.IsPeak ? "标准时段（原价）" : "优惠时段（半价）";
        }

        public string PeakLine()
        {
            Snapshot data = Data.Current;
            if (!data.HasPeak) return "";
            long leftMs = data.NextChangeAtMs - DataService.NowMs();
            if (leftMs < 0) leftMs = 0;
            TimeSpan left = TimeSpan.FromMilliseconds(leftMs);
            string countdown = left.TotalHours >= 1
                ? ((int)left.TotalHours) + " 小时 " + left.Minutes + " 分"
                : Math.Max(1, (int)Math.Ceiling(left.TotalMinutes)) + " 分钟";
            return (data.IsPeak ? "标准价 · " : "优惠价（半价）· ") + countdown + "后切换";
        }

        public string PeakBubbleText()
        {
            Snapshot data = Data.Current;
            if (!data.HasPeak) return "";
            return data.IsPeak ? "标准价 ×2" : "优惠 50%";
        }

        public static string ActionName(string key)
        {
            switch (key)
            {
                case "random": return "随机";
                case "hop": return "蹦一下";
                case "bounce": return "连蹦";
                case "spin": return "转圈";
                case "roll": return "打滚";
                case "wobble": return "摇摆";
                case "nod": return "点头";
                case "shake": return "摇头";
                case "wave": return "招手";
                case "clap": return "鼓掌";
                case "stretch": return "伸懒腰";
                case "yawn": return "打哈欠";
                case "shy": return "害羞";
                case "startle": return "吓一跳";
                case "lie": return "趴一会";
                case "sleep": return "睡觉";
                case "look": return "左看右看";
                case "pace": return "踱步";
                case "peek": return "探头";
                case "stroll": return "溜达";
                case "dash": return "冲刺";
                case "follow": return "跟随";
                case "greet": return "打招呼";
                case "fireworks": return "放烟花";
                case "hearts": return "比心";
                case "sparkles": return "闪光";
                case "confetti": return "撒花";
                default: return key;
            }
        }

        // ------------------------------------------------------------ 数据回调
        private void OnDataUpdated(object sender, EventArgs e)
        {
            Application app = Application.Current;
            if (app == null) return;
            app.Dispatcher.BeginInvoke(new Action(ApplyData));
        }

        private void ApplyData()
        {
            if (Quitting) return;
            Snapshot data = Data.Current;
            if (!data.HasPeak)
            {
                lastPeak = null;   // 主接口不分标准/优惠时段：不提示切换
            }
            else
            {
                if (lastPeak.HasValue && lastPeak.Value != data.IsPeak && Pets.Count > 0)
                {
                    string text = data.IsPeak ? "转标准价了，价格 ×2，悠着点烧" : "转优惠价啦，半价";
                    bubbleIsMessage = true;
                    bubbleIsAlert = false;
                    messageTitle = "";
                    messageText = text;
                    bubbleQuip = text;
                    ShowView(Pets[0], BuildMessageView(text), 6);
                }
                lastPeak = data.IsPeak;
            }
            CheckAlerts(data);
            if (Bubble.Visibility == Visibility.Visible)
            {
                Bubble.UpdateFrom(BuildActiveBubbleView());
            }
            if (panel.IsVisible) panel.UpdateDataText();
        }

        public void RefreshBubblePresentation()
        {
            if (Bubble.Visibility == Visibility.Visible)
            {
                Bubble.UpdateFrom(BuildActiveBubbleView());
                Bubble.Position(true);
            }
        }

        public void CommitDraftConfig(Config draft)
        {
            if (draft == null) return;
            cfg.CopyFrom(draft);
            cfg.Save();
            Bubble.ApplyStyle(cfg);
            foreach (PetWindow pet in Pets)
            {
                pet.ResizePet();
                pet.RefreshProp();
            }
            Data.Refresh();
        }

        private BubbleView BuildActiveBubbleView()
        {
            Bubble.ApplyStyle(cfg);
            if (bubbleIsAlert) return BuildAlertView(alertTitle, alertText, alertNote);
            if (bubbleIsMessage) return BuildMessageView(bubbleQuip);
            return BuildPage(bubblePage);
        }

        private void CheckAlerts(Snapshot data)
        {
            Thresholds limits = data.Limits;
            if (limits == null || Pets.Count == 0) return;
            string day = DataService.TodayKey();
            List<string> headlines = new List<string>();
            List<string> titles = new List<string>();
            List<string> notes = new List<string>();
            foreach (ProviderState provider in data.Providers)
            {
                if (!provider.Enabled) continue;
                double below;
                if (limits.AlertOn && provider.Ok && provider.Balance.HasValue
                    && limits.Below.TryGetValue(provider.Id, out below))
                {
                    bool hit = provider.Balance.Value < below;
                    bool latched = balanceLatched.Contains(provider.Id);
                    if (hit && !latched)
                    {
                        balanceLatched.Add(provider.Id);
                        headlines.Add(Fmt.Money(provider.Balance, provider.Currency));
                        titles.Add(provider.Name + " 余额预警");
                        notes.Add(provider.Name + " 余额已低于 " + Fmt.Money(below, provider.Currency));
                    }
                    else if (!hit && latched)
                    {
                        balanceLatched.Remove(provider.Id);   // 充钱回来了，下次还能再提醒
                    }
                }
                double budget;
                if (limits.BudgetOn && provider.TodayUsage.HasValue
                    && limits.Budget.TryGetValue(provider.Id, out budget))
                {
                    string key = provider.Id + "|" + day;
                    if (provider.TodayUsage.Value >= budget && !budgetLatched.Contains(key))
                    {
                        budgetLatched.Add(key);
                        Log.Write("budget alert " + provider.Id
                            + " balance=" + (provider.Balance.HasValue
                                ? provider.Balance.Value.ToString("0.00", CultureInfo.InvariantCulture) : "--")
                            + " today=" + provider.TodayUsage.Value.ToString("0.00", CultureInfo.InvariantCulture)
                            + " budget=" + budget.ToString("0.00", CultureInfo.InvariantCulture));
                        headlines.Add(Fmt.Money(provider.TodayUsage, provider.Currency));
                        titles.Add(provider.Name + " 今日预算");
                        notes.Add(provider.Name + " 今日已用超过预算 " + Fmt.Money(budget, provider.Currency));
                    }
                }
            }
            // 套餐额度：5 小时 / 7 天窗口用量到阈值也提醒（窗口自己会重置，掉回阈值下就能再次提醒）
            if (limits.QuotaOn)
            {
                foreach (PlanState plan in data.Plans)
                {
                    if (!plan.Enabled || !plan.Ok) continue;
                    foreach (PlanWindow window in plan.Windows)
                    {
                        int kind = window.Kind();
                        double threshold;
                        bool has = kind == 1
                            ? limits.FiveHour.TryGetValue(plan.Id, out threshold)
                            : limits.SevenDay.TryGetValue(plan.Id, out threshold);
                        if (!has) continue;
                        string key = plan.Id + "|" + kind;
                        bool hit = window.Percent >= threshold;
                        bool latched = quotaLatched.Contains(key);
                        if (hit && !latched)
                        {
                            quotaLatched.Add(key);
                            headlines.Add(window.Percent.ToString("0.#", CultureInfo.InvariantCulture) + "%");
                            titles.Add(plan.Name + " 额度提醒");
                            notes.Add(plan.Name + " " + window.Label + "用量已达 "
                                + threshold.ToString("0.#", CultureInfo.InvariantCulture) + "% 阈值");
                        }
                        else if (!hit && latched)
                        {
                            quotaLatched.Remove(key);
                        }
                    }
                }
            }
            // 大数字槽位只放短值；一次踩中多个平台时，明细并到备注里换行显示。
            if (notes.Count == 1)
            {
                ShowAlert(Pets[0], titles[0], headlines[0], notes[0]);
            }
            else if (notes.Count > 1)
            {
                ShowAlert(Pets[0], "多平台提醒", notes.Count + " 条", string.Join("\n", notes.ToArray()));
            }
        }

        // ------------------------------------------------------------ 退出
        public void SavePositions()
        {
            List<object> list = new List<object>();
            foreach (PetWindow pet in Pets)
            {
                Dictionary<string, object> node = new Dictionary<string, object>();
                double x = pet.EdgeHidden ? pet.EdgeShownX : pet.X;
                node["x"] = Math.Round(x);
                node["y"] = Math.Round(pet.Y);
                list.Add(node);
            }
            cfg.Set("", "pets", list.ToArray());
            cfg.Save();
        }

        public void Quit()
        {
            if (Quitting) return;
            Quitting = true;
            SavePositions();
            Data.Stop();
            panel.HidePanel();
            Bubble.HideNow();
            foreach (PetWindow pet in Pets) pet.Close();
            Application app = Application.Current;
            if (app != null) app.Shutdown();
        }

        public string Report()
        {
            Snapshot data = Data.Current;
            StringBuilder sb = new StringBuilder();
            sb.Append("pets=" + Pets.Count);
            sb.Append(" peak=" + (data.IsPeak ? "peak" : "offpeak"));
            ProviderState primary = data.Primary;
            sb.Append(" primary=" + (primary == null ? "-" : primary.Id + ":" + Fmt.Money(primary.Balance, primary.Currency) + "(" + primary.SourceText + ")"));
            sb.Append(" providers=[");
            bool first = true;
            foreach (ProviderState provider in data.Providers)
            {
                if (!provider.Enabled) continue;
                if (!first) sb.Append(", ");
                first = false;
                sb.Append(provider.Id + "=" + (provider.Ok ? Fmt.Money(provider.Balance, provider.Currency) : "ERR"));
            }
            sb.Append("]");
            return sb.ToString();
        }
    }
}
