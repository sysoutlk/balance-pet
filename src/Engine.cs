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

namespace DshPet
{
    internal sealed class Engine
    {
        private readonly Config cfg;
        private readonly DispatcherTimer timer = new DispatcherTimer();
        public readonly DataService Data;
        public readonly List<PetWindow> Pets = new List<PetWindow>();
        public readonly BubbleWindow Bubble = new BubbleWindow();
        private readonly SettingsPanel panel;
        private DateTime lastTick = DateTime.UtcNow;
        private DateTime lastPositionSave = DateTime.UtcNow;
        private DateTime nextSplitAt = DateTime.MinValue;
        private DateTime demoActionAt = DateTime.MinValue;
        private bool? lastPeak;
        private bool balanceLatched;
        private string budgetLatchedDay = "";
        private int bubblePage;
        private bool bubbleIsMessage;
        private bool bubbleIsAlert;
        private string alertTitle = "";
        private string alertText = "";
        private string messageTitle = "";
        private string messageText = "";
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
            panel.Build();
            BuildPets();
            Data.Updated += OnDataUpdated;
            Data.Start();
            timer.Interval = TimeSpan.FromMilliseconds(60);
            timer.Tick += OnTick;
            timer.Start();
            if (Demo && Pets.Count > 0)
            {
                bubblePage = 0;
                bubbleQuip = QuipFor("greeting");
                ShowView(Pets[0], BuildPage(0), 3600);
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
            int count = Math.Max(1, saved.Count);
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
                WirePet(pet);
                Pets.Add(pet);
                pet.Show();
            }
        }

        private void WirePet(PetWindow pet)
        {
            pet.Clicked += OnPetClicked;
            pet.DoubleClicked += OnPetDoubleClicked;
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
            foreach (PetWindow pet in Pets)
            {
                UpdatePet(pet, now, dt);
                if (pet.Mode != "idle" || pet.Action.Length > 0) active = true;
            }

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

            TimeSpan interval = active ? TimeSpan.FromMilliseconds(40) : TimeSpan.FromMilliseconds(260);
            if (timer.Interval != interval) timer.Interval = interval;

            if ((now - lastPositionSave).TotalSeconds >= 30)
            {
                lastPositionSave = now;
                SavePositions();
            }
        }

        private void UpdatePet(PetWindow pet, DateTime now, double dt)
        {
            double speed = cfg.GetNumber("walk", "speedPxPerSec", 72);
            bool sleeping = pet.Sleeping;
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
                pet.Spin.Angle += pet.VelocityX * dt * 0.06;
                pet.ApplyPosition();
                double speedNow = Math.Sqrt(pet.VelocityX * pet.VelocityX + pet.VelocityY * pet.VelocityY);
                if (speedNow < stopSpeed)
                {
                    pet.Spin.Angle = 0;
                    pet.SetIdle();
                    ScheduleEdgeHide(pet, now);
                }
                return;
            }

            if (pet.Mode == "edge")
            {
                UpdateEdgeSlide(pet, now, dt);
                return;
            }

            if (!string.IsNullOrEmpty(pet.Action))
            {
                double total = (pet.ActionUntil - pet.ActionStart).TotalSeconds;
                double p = total <= 0 ? 1 : Math.Min(1, (now - pet.ActionStart).TotalSeconds / total);
                AnimateAction(pet, now, p);
                if (now >= pet.ActionUntil) EndAction(pet);
                return;
            }

            if (pet.Mode == "walk")
            {
                double dx = pet.TargetX - pet.X;
                double dy = pet.TargetY - pet.Y;
                double distance = Math.Sqrt(dx * dx + dy * dy);
                if (distance < 4)
                {
                    pet.SetIdle();
                    ScheduleEdgeHide(pet, now);
                    return;
                }
                double step = speed * pet.SpeedFactor * dt;
                if (step > distance) step = distance;
                pet.X += dx / distance * step;
                pet.Y += dy / distance * step;
                if (Math.Abs(dx) > 6) pet.SetFacing(dx > 0 ? 1 : -1);
                double rate = pet.SpeedFactor >= 2 ? 16 : 9;
                pet.Bob.Y = Math.Abs(Math.Sin(now.TimeOfDay.TotalSeconds * rate)) * -3;
                if (pet.SpeedFactor >= 2) pet.Spin.Angle = pet.Facing * 7;
                pet.ApplyPosition();
                return;
            }

            // idle
            pet.Spin.Angle = 0;
            pet.Bob.Y = Math.Sin(now.TimeOfDay.TotalSeconds * 1.4 + pet.Phase) * (sleeping ? 0.8 : 1.6);
            if (pet.EdgeHidden)
            {
                if (!EdgeHideEnabled(pet))
                {
                    pet.StartEdgeSlide(false);
                    return;
                }
                // Stays tucked in (with its own animation) until the user drags it out.
                return;
            }
            if (pet.Dropped && EdgeHideEnabled(pet))
            {
                // Dropped against an edge: tuck in right away instead of waiting for the timer.
                pet.Dropped = false;
                pet.StartEdgeSlide(true);
                return;
            }
            pet.Dropped = false;
            if (pet.Tuck > 0.01) return;   // mid-tuck: no walking, no random actions
            RefreshEdgeAttachment(pet);
            if (EdgeHideEnabled(pet))
            {
                if (pet.EdgeHideAt == DateTime.MaxValue)
                {
                    ScheduleEdgeHide(pet, now);
                }
                else if (now >= pet.EdgeHideAt)
                {
                    pet.StartEdgeSlide(true);
                    return;
                }
            }
            if (sleeping) return;
            if (now >= pet.NextWalkAt && cfg.GetFlag("walk", "enabled", true))
            {
                pet.StartWalk();
                return;
            }
            if (now >= pet.NextActionAt && cfg.GetFlag("actions", "enabled", true))
            {
                pet.StartAction("random");
                if (Program.Rng.NextDouble() < 0.35)
                {
                    ShowBubbleText(pet, QuipFor(pet.LastActionKey));
                }
            }
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
                state = pet.Tuck >= 0.5 ? "peek" : "walk";
                rate = 1.3;
            }
            else if (pet.Mode == "thrown")
            {
                state = "spin";
                rate = 1.5;
            }
            else if (pet.Mode == "drag")
            {
                state = "walk";
                rate = 1.5;
            }
            else if (sleeping)
            {
                state = "sleep";
                rate = 0.35;
            }
            else if (pet.EdgeHidden)
            {
                state = "peek";
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
            if (pet.EdgeSide == "left") tuckX = area.Left + 1;
            else if (pet.EdgeSide == "right") tuckX = area.Right - pet.Width - 1;
            else if (pet.EdgeSide == "top") tuckY = area.Top + 1;
            else if (pet.EdgeSide == "bottom") tuckY = area.Bottom - pet.Height - 1;
            pet.X = pet.EdgeShownX + (tuckX - pet.EdgeShownX) * pet.Tuck;
            pet.Y = pet.EdgeShownY + (tuckY - pet.EdgeShownY) * pet.Tuck;
            pet.ApplyPosition();

            if (Math.Abs(pet.Tuck - pet.TargetTuck) > 0.001) return;

            if (pet.TargetTuck >= 0.999) pet.EdgeHidden = true;
            else if (pet.TargetTuck <= 0.001) pet.EdgeHidden = false;
            pet.Mode = "idle";
            ScheduleEdgeHide(pet, now);
        }

        // ------------------------------------------------------------ 动作动画
        private void AnimateAction(PetWindow pet, DateTime now, double p)
        {
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
                        double hidden = towardsLeft ? area.Left - pet.Width + 18 : area.Right - 18;
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

        private static void EndAction(PetWindow pet)
        {
            pet.Action = "";
            pet.X = pet.ActionOriginX;
            pet.ApplyPosition();
            pet.Spin.Angle = 0;
            pet.Squash.ScaleX = 1;
            pet.Squash.ScaleY = 1;
            pet.Bob.X = 0;
            pet.Bob.Y = 0;
            pet.SetIdle();
        }

        // ------------------------------------------------------------ 贴边隐藏
        private bool EdgeHideEnabled(PetWindow pet)
        {
            if (!cfg.GetFlagAt("edgeHide.enabled", true)) return false;
            return !string.IsNullOrEmpty(pet.EdgeSide);
        }

        private void ScheduleEdgeHide(PetWindow pet, DateTime now)
        {
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
            if (pet.EdgeHidden)
            {
                // 收起的桌宠不再响应点击（防误触）：拖它才会出来。
                return;
            }
            if (pet.Tuck > 0.5) return;   // 正在收起时点击也不打断
            RefreshEdgeAttachment(pet);   // a click stops it in place instead of re-attaching
            ScheduleEdgeHide(pet, DateTime.UtcNow);
            string quip = QuipFor(pet.Sleeping ? "sleep" : "greeting");
            int page = pet.PageIndex % PageCount();
            pet.PageIndex = (page + 1) % PageCount();
            bubbleIsMessage = false;
            bubbleIsAlert = false;
            bubblePage = page;
            bubbleQuip = quip;
            ShowView(pet, BuildPage(page), BubbleSeconds());
        }

        private void OnPetDoubleClicked(object sender, EventArgs e)
        {
            PerformAction(sender as PetWindow, "random", true);
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
            double scale = cfg.GetNumber("", "scale", 1.0);
            scale = up ? scale * 1.07 : scale / 1.07;
            scale = Math.Max(0.4, Math.Min(2.6, Math.Round(scale, 3)));
            cfg.Set("", "scale", scale);
            cfg.Save();
            foreach (PetWindow pet in Pets) pet.ResizePet();
            panel.Update();
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
            if (panel.IsVisible && panel.ActivePet == pet) panel.HidePanel();
            else panel.ShowFor(pet);
        }

        public void PerformAction(PetWindow pet, string key, bool speak)
        {
            if (pet == null && Pets.Count > 0) pet = Pets[0];
            if (pet == null) return;
            if (pet.EdgeHidden) RevealPet(pet);
            pet.StartAction(key);
            if (string.IsNullOrEmpty(pet.LastActionKey)) return;
            if (speak && cfg.GetFlag("alert", "quipsOn", true))
            {
                ShowBubbleText(pet, QuipFor(pet.LastActionKey));
            }
        }

        // ------------------------------------------------------------ 分身
        public int CloneCap()
        {
            double max = cfg.GetNumberAt("clones.max", cfg.GetNumber("", "maxClones", 6));
            return (int)max;   // 0 或负数 = 不限
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
            if (!cfg.GetFlagAt("clones.auto", true)) return false;
            if (Pets.Count == 0) return false;
            if (nextSplitAt == DateTime.MinValue)
            {
                nextSplitAt = now.AddSeconds(SplitInterval());
                return false;
            }
            if (now < nextSplitAt) return false;
            nextSplitAt = now.AddSeconds(SplitInterval());
            int cap = CloneCap();
            if (cap > 0 && Pets.Count >= 1 + cap) return false;
            AddClone();
            return true;
        }

        private double SplitInterval()
        {
            double min = cfg.GetNumberAt("clones.pauseMinSec", 240);
            double max = cfg.GetNumberAt("clones.pauseMaxSec", 600);
            if (max <= min) max = min + 60;
            return min + Program.Rng.NextDouble() * (max - min);
        }

        public void AddClone()
        {
            int cap = CloneCap();
            if (cap > 0 && Pets.Count >= 1 + cap) return;
            if (Pets.Count == 0) return;
            PetWindow source = Pets[Program.Rng.Next(Pets.Count)];
            PetWindow pet = new PetWindow(cfg);
            pet.ApplyRole(PickRole(source.Role));
            pet.ResizePet();
            pet.Place(source.X + 60 + Program.Rng.Next(0, 60), source.Y - 30 - Program.Rng.Next(0, 60));
            WirePet(pet);
            Pets.Add(pet);
            pet.Show();
            pet.SetIdle();
            ScheduleEdgeHide(pet, DateTime.UtcNow);
        }

        public void RemoveClone()
        {
            if (Pets.Count <= 1) return;
            PetWindow pet = Pets[Pets.Count - 1];
            Pets.RemoveAt(Pets.Count - 1);
            pet.Close();
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
            if (pet == null) return;
            bubbleIsMessage = true;
            bubbleIsAlert = true;
            alertTitle = title;
            alertText = text;
            messageTitle = title;
            messageText = text;
            bubbleQuip = "";
            ShowView(pet, BuildAlertView(title, text), 8);
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
            view.Title = primary == null ? "DeepSeek 余额" : primary.Name + " 余额";
            view.Big = primary == null ? "--" : Fmt.Money(primary.Balance, primary.Currency);
            view.BigColor = "#203170";
            view.Quip = text;
            view.PeakText = PeakBubbleText();
            view.PeakNow = data.IsPeak;
            view.Source = SourceLine(data);
            return view;
        }

        private BubbleView BuildAlertView(string title, string text)
        {
            Snapshot data = Data.Current;
            BubbleView view = new BubbleView();
            view.Title = string.IsNullOrEmpty(title) ? "提醒" : title;
            view.Big = text;
            view.BigSize = 15;
            view.BigColor = "#e0433f";
            view.PeakText = PeakBubbleText();
            view.PeakNow = data.IsPeak;
            view.Source = SourceLine(data);
            return view;
        }

        private string SourceLine(Snapshot data)
        {
            string stamp = data.AtMs > 0
                ? new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(data.AtMs).ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture)
                : "--";
            ProviderState primary = data.Primary;
            string sourceText = primary == null ? "无数据源" : primary.SourceText;
            if (data.OkCount > 1) sourceText = data.OkCount + " 个数据源";
            return sourceText + " · " + stamp + " 更新";
        }

        /// <summary>Four DeepSeek-centric views, cycled by clicking the pet.</summary>
        private BubbleView BuildPage(int index)
        {
            Snapshot data = Data.Current;
            ProviderState primary = data.Primary;
            BubbleView view = new BubbleView();
            view.PageIndex = index;
            view.PageCount = PageCount();
            view.Quip = bubbleQuip;
            view.PeakText = PeakBubbleText();
            view.PeakNow = data.IsPeak;
            view.Source = SourceLine(data);

            if (index == 0)
            {
                view.Title = primary == null ? "DeepSeek 余额" : primary.Name + " 余额";
                view.Big = primary == null ? "--" : Fmt.Money(primary.Balance, primary.Currency);
                if (primary != null && primary.Details.Count > 0)
                {
                    view.Sub = string.Join(" · ", primary.Details.ToArray());
                }
                view.Rows.Add(new KeyValuePair<string, string>("今日已用",
                    primary == null ? "--" : Fmt.Money(primary.TodayUsage, primary.Currency)));
                view.ShowTrend = true;
                view.Trend = data.History;
                if (primary != null && !primary.Ok && !string.IsNullOrEmpty(primary.Error))
                {
                    view.Note = primary.Error;
                    view.NoteError = true;
                }
                return view;
            }

            if (index == 1)
            {
                view.Title = "今日消耗";
                view.Big = primary == null ? "--" : Fmt.Money(primary.TodayUsage, primary.Currency);
                view.Rows.Add(new KeyValuePair<string, string>("当前余额",
                    primary == null ? "--" : Fmt.Money(primary.Balance, primary.Currency)));
                double yesterday = 0;
                double sum = 0;
                int days = 0;
                List<DayUsage> history = data.History;
                for (int i = 0; i < history.Count; i++)
                {
                    if (i == history.Count - 2) yesterday = history[i].Usage;
                    if (i < history.Count - 1)
                    {
                        sum += history[i].Usage;
                        days++;
                    }
                }
                view.Rows.Add(new KeyValuePair<string, string>("昨日", Fmt.Money(yesterday, primary == null ? "CNY" : primary.Currency)));
                if (days > 0)
                {
                    view.Rows.Add(new KeyValuePair<string, string>("近 7 日均值", Fmt.Money(sum / days, primary == null ? "CNY" : primary.Currency)));
                }
                view.Rows.Add(new KeyValuePair<string, string>("更新时间", StampOf(data.AtMs)));
                view.ShowTrend = true;
                view.Trend = history;
                return view;
            }

            if (index == 2)
            {
                view.Title = "峰谷时段（北京时间）";
                view.Big = data.IsPeak ? "高峰时段" : "空闲时段";
                view.BigSize = 21;
                view.BigColor = data.IsPeak ? "#e0433f" : "#2fa24c";
                view.Rows.Add(new KeyValuePair<string, string>("计价", data.IsPeak ? "高峰价格（空闲价 x2）" : "空闲价格（高峰价的一半）"));
                view.Rows.Add(new KeyValuePair<string, string>("高峰时间", "工作日 09:00-12:00 / 14:00-18:00"));
                view.Rows.Add(new KeyValuePair<string, string>("空闲时间", "其余时段（含周末与节假日）"));
                view.Rows.Add(new KeyValuePair<string, string>("当前时间", DataService.BeijingNow().ToString("HH:mm:ss", CultureInfo.InvariantCulture)));
                view.Rows.Add(new KeyValuePair<string, string>("距切换", CountdownText(data.NextChangeAtMs)));
                return view;
            }

            if (index == 3)
            {
                view.Title = "数据源";
                view.Big = data.OkCount + " / " + EnabledCount(data);
                view.BigSize = 22;
                foreach (ProviderState provider in data.Providers)
                {
                    if (!provider.Enabled) continue;
                    if (!provider.Supported)
                    {
                        view.Rows.Add(new KeyValuePair<string, string>(provider.Name, "无公开接口"));
                        continue;
                    }
                    view.Rows.Add(new KeyValuePair<string, string>(provider.Name,
                        provider.Ok ? Fmt.Money(provider.Balance, provider.Currency) : "查询失败"));
                }
                view.Rows.Add(new KeyValuePair<string, string>("更新时间", StampOf(data.AtMs)));
                return view;
            }

            // Page 4: coding-plan usage windows (5-hour / weekly).
            view.Title = "套餐用量（5 小时 / 7 天）";
            view.BigSize = 20;
            int okPlans = 0;
            foreach (PlanState plan in data.Plans)
            {
                if (plan.Ok) okPlans++;
            }
            view.Big = okPlans + " / " + data.Plans.Count + " 套餐在线";
            foreach (PlanState plan in data.Plans)
            {
                if (!plan.Ok)
                {
                    view.Rows.Add(new KeyValuePair<string, string>(plan.Name,
                        plan.Unsupported ? "不可查询" : "查询失败"));
                    continue;
                }
                foreach (PlanWindow window in plan.Windows)
                {
                    view.Rows.Add(new KeyValuePair<string, string>(plan.Name + " · " + window.Label,
                        window.Percent.ToString("0.#", CultureInfo.InvariantCulture) + "%"));
                }
            }
            return view;
        }

        private int PageCount()
        {
            return Data.Current.Plans.Count > 0 ? 5 : 4;
        }

        private static int EnabledCount(Snapshot data)
        {
            int count = 0;
            foreach (ProviderState provider in data.Providers)
            {
                if (provider.Enabled && provider.Supported) count++;
            }
            return count;
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
            if (left.TotalHours >= 1)
            {
                return ((int)left.TotalHours) + " 小时 " + left.Minutes + " 分 " + left.Seconds + " 秒";
            }
            return left.Minutes + " 分 " + left.Seconds + " 秒";
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
            return Data.Current.IsPeak ? "高峰时段" : "谷时（半价）";
        }

        public string PeakLine()
        {
            Snapshot data = Data.Current;
            long leftMs = data.NextChangeAtMs - DataService.NowMs();
            if (leftMs < 0) leftMs = 0;
            TimeSpan left = TimeSpan.FromMilliseconds(leftMs);
            string countdown = left.TotalHours >= 1
                ? ((int)left.TotalHours) + " 小时 " + left.Minutes + " 分"
                : Math.Max(1, (int)Math.Ceiling(left.TotalMinutes)) + " 分钟";
            return (data.IsPeak ? "高峰时段 · 距谷时 " : "谷时半价 · 距高峰 ") + countdown;
        }

        public string PeakBubbleText()
        {
            Snapshot data = Data.Current;
            long leftMs = data.NextChangeAtMs - DataService.NowMs();
            if (leftMs < 0) leftMs = 0;
            TimeSpan left = TimeSpan.FromMilliseconds(leftMs);
            string countdown = left.TotalHours >= 1
                ? ((int)left.TotalHours) + ":" + left.Minutes.ToString("00") + ":" + left.Seconds.ToString("00")
                : left.Minutes + ":" + left.Seconds.ToString("00");
            return (data.IsPeak ? "高峰时段 · 价格 x2" : "谷时 · 半价") + "  还有 " + countdown;
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
            if (lastPeak.HasValue && lastPeak.Value != data.IsPeak && Pets.Count > 0)
            {
                string text = data.IsPeak ? "进高峰了，价格 x2，悠着点烧" : "谷时到了，半价啦";
                bubbleIsMessage = true;
                bubbleIsAlert = false;
                messageTitle = "";
                messageText = text;
                bubbleQuip = text;
                ShowView(Pets[0], BuildMessageView(text), 6);
            }
            lastPeak = data.IsPeak;
            CheckAlerts(data);
            if (Bubble.Visibility == Visibility.Visible)
            {
                Bubble.ApplyStyle(cfg);
                BubbleView view;
                if (bubbleIsAlert) view = BuildAlertView(alertTitle, alertText);
                else if (bubbleIsMessage) view = BuildMessageView(bubbleQuip);
                else view = BuildPage(bubblePage);
                Bubble.UpdateFrom(view);
            }
            if (panel.IsVisible) panel.UpdateDataText();
        }

        private void CheckAlerts(Snapshot data)
        {
            ProviderState primary = data.Primary;
            if (cfg.GetFlag("alert", "balanceWarn", true)
                && data.Limits != null && data.Limits.AlertBelow.HasValue
                && primary != null && primary.Ok && primary.Balance.HasValue && Pets.Count > 0)
            {
                bool below = primary.Balance.Value < data.Limits.AlertBelow.Value;
                if (below && !balanceLatched)
                {
                    balanceLatched = true;
                    ShowAlert(Pets[0], "余额预警",
                        "余额 " + Fmt.Money(primary.Balance, primary.Currency) + " 已低于 " + Fmt.Money(data.Limits.AlertBelow, primary.Currency));
                }
                else if (!below)
                {
                    balanceLatched = false;
                }
            }
            if (cfg.GetFlag("alert", "budgetWarn", true)
                && data.Limits != null && data.Limits.Budget.HasValue
                && primary != null && primary.TodayUsage.HasValue && Pets.Count > 0)
            {
                bool over = primary.TodayUsage.Value >= data.Limits.Budget.Value;
                string day = DataService.TodayKey();
                if (over && budgetLatchedDay != day)
                {
                    budgetLatchedDay = day;
                    ShowAlert(Pets[0], "今日预算",
                        "今日已用 " + Fmt.Money(primary.TodayUsage, primary.Currency) + "，超过预算 " + Fmt.Money(data.Limits.Budget, primary.Currency));
                }
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
