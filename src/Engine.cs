// Engine: owns the pets, the bubble, the settings panel and the render loop.
//
// The loop is adaptive: while a pet is walking, performing an action, being dragged or
// thrown (or the bubble is visible) it ticks at ~25 fps; otherwise it drops to ~4 fps and
// only keeps the breathing animation and the walk/action schedule alive.
using System;
using System.Collections.Generic;
using System.IO;
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
        private bool? lastPeak;
        private bool balanceLatched;
        private string budgetLatchedDay = "";
        private string bubbleOverride = "";
        private DateTime demoActionAt = DateTime.MinValue;
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
                ShowBubble(Pets[0], "");
                demoActionAt = DateTime.UtcNow.AddSeconds(1);
            }
        }

        // ------------------------------------------------------------ 初始化
        private void BuildPets()
        {
            string role = cfg.GetString("", "role", "");
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

            if (Demo)
            {
                if (now >= demoActionAt)
                {
                    demoActionAt = now.AddSeconds(2.6);
                    PerformAction(Pets.Count > 0 ? Pets[0] : null, "random");
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
                }
                return;
            }

            if (!string.IsNullOrEmpty(pet.Action))
            {
                double total = (pet.ActionUntil - pet.ActionStart).TotalSeconds;
                double progress = total <= 0 ? 1 : Math.Min(1, (now - pet.ActionStart).TotalSeconds / total);
                switch (pet.Action)
                {
                    case "hop":
                        pet.Bob.Y = -30 * Math.Sin(Math.PI * progress);
                        break;
                    case "spin":
                        pet.Spin.Angle = 360 * progress;
                        break;
                    case "wobble":
                        pet.Spin.Angle = Math.Sin(progress * Math.PI * 6) * 14;
                        break;
                    case "stretch":
                        {
                            double k = Math.Sin(Math.PI * progress);
                            pet.Squash.ScaleY = 1 + 0.16 * k;
                            pet.Squash.ScaleX = 1 - 0.10 * k;
                            break;
                        }
                    case "dash":
                        pet.Bob.Y = -Math.Abs(Math.Sin(progress * Math.PI * 8)) * 3;
                        pet.Spin.Angle = pet.Facing * 7;
                        break;
                    case "nap":
                        pet.Bob.Y = Math.Sin(now.TimeOfDay.TotalSeconds * 0.9) * 1.4;
                        break;
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
                                double follow = speed * 2.2;
                                pet.X += dx / distance * follow * dt;
                                pet.Y += dy / distance * follow * dt;
                                if (Math.Abs(dx) > 6) pet.SetFacing(dx > 0 ? 1 : -1);
                                pet.Bob.Y = -Math.Abs(Math.Sin(now.TimeOfDay.TotalSeconds * 10)) * 3;
                                pet.ApplyPosition();
                            }
                            break;
                        }
                }
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
                    return;
                }
                double step = speed * pet.SpeedFactor * dt;
                if (step > distance) step = distance;
                pet.X += dx / distance * step;
                pet.Y += dy / distance * step;
                if (Math.Abs(dx) > 6) pet.SetFacing(dx > 0 ? 1 : -1);
                pet.Bob.Y = Math.Abs(Math.Sin(now.TimeOfDay.TotalSeconds * 9)) * -3;
                pet.ApplyPosition();
                return;
            }

            // idle
            pet.Bob.Y = Math.Sin(now.TimeOfDay.TotalSeconds * 1.4 + pet.Phase) * (sleeping ? 0.8 : 1.6);
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
                    ShowBubble(pet, QuipFor(pet.LastActionKey), 2);
                }
            }
        }

        private static void EndAction(PetWindow pet)
        {
            pet.Action = "";
            pet.Spin.Angle = 0;
            pet.Squash.ScaleX = 1;
            pet.Squash.ScaleY = 1;
            pet.Bob.Y = 0;
            pet.SetIdle();
        }

        // ------------------------------------------------------------ 交互
        private void OnPetClicked(object sender, EventArgs e)
        {
            PetWindow pet = sender as PetWindow;
            if (pet == null) return;
            ShowBubble(pet, QuipFor(pet.Sleeping ? "sleep" : "greeting"));
        }

        private void OnPetDoubleClicked(object sender, EventArgs e)
        {
            PerformAction(sender as PetWindow, "random");
        }

        private void OnPetContextRequested(object sender, EventArgs e)
        {
            TogglePanel(sender as PetWindow);
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

        public void PerformAction(PetWindow pet, string key)
        {
            if (pet == null && Pets.Count > 0) pet = Pets[0];
            if (pet == null) return;
            pet.StartAction(key);
            string label = string.IsNullOrEmpty(pet.LastActionKey) ? key : pet.LastActionKey;
            if (cfg.GetFlag("alert", "quipsOn", true))
            {
                ShowBubble(pet, QuipFor(label), 3);
            }
        }

        public void AddClone()
        {
            int max = (int)cfg.GetNumber("", "maxClones", 6);
            if (Pets.Count >= 1 + max || Pets.Count == 0) return;
            PetWindow source = Pets[0];
            PetWindow pet = new PetWindow(cfg);
            pet.ApplyRole(source.Role);
            pet.ResizePet();
            pet.Place(source.X + 70, source.Y - 50);
            WirePet(pet);
            Pets.Add(pet);
            pet.Show();
        }

        public void RemoveClone()
        {
            if (Pets.Count <= 1) return;
            PetWindow pet = Pets[Pets.Count - 1];
            Pets.RemoveAt(Pets.Count - 1);
            pet.Close();
        }

        // ------------------------------------------------------------ 气泡 / 台词
        public void ShowBubble(PetWindow pet, string text)
        {
            if (pet == null && Pets.Count > 0) pet = Pets[0];
            ShowBubble(pet, text, Demo ? 3600 : BubbleSeconds());
        }

        private void ShowBubble(PetWindow pet, string text, int seconds)
        {
            if (pet == null) return;
            bubbleOverride = text;
            Bubble.HideAt = DateTime.UtcNow.AddSeconds(seconds);
            Bubble.ShowFor(pet, Data.Current, text, PeakBubbleText(), Data.Current.IsPeak);
        }

        private int BubbleSeconds()
        {
            double seconds = cfg.GetNumber("bubble", "seconds", 7);
            if (seconds < 2) seconds = 2;
            return (int)seconds;
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
            Snapshot s = Data.Current;
            long leftMs = s.NextChangeAtMs - DataService.NowMs();
            if (leftMs < 0) leftMs = 0;
            TimeSpan left = TimeSpan.FromMilliseconds(leftMs);
            string countdown = left.TotalHours >= 1
                ? ((int)left.TotalHours).ToString() + " 小时 " + left.Minutes.ToString() + " 分"
                : Math.Max(1, (int)Math.Ceiling(left.TotalMinutes)) + " 分钟";
            return (s.IsPeak ? "高峰时段 · 距谷时 " : "谷时半价 · 距高峰 ") + countdown;
        }

        public string PeakBubbleText()
        {
            Snapshot s = Data.Current;
            long leftMs = s.NextChangeAtMs - DataService.NowMs();
            if (leftMs < 0) leftMs = 0;
            TimeSpan left = TimeSpan.FromMilliseconds(leftMs);
            string countdown = left.TotalHours >= 1
                ? ((int)left.TotalHours).ToString() + ":" + left.Minutes.ToString("00") + ":" + left.Seconds.ToString("00")
                : left.Minutes.ToString() + ":" + left.Seconds.ToString("00");
            return (s.IsPeak ? "高峰时段 · 价格 x2" : "谷时 · 半价") + "  还有 " + countdown;
        }

        public static string ActionName(string key)
        {
            switch (key)
            {
                case "hop": return "跳一下";
                case "spin": return "转圈";
                case "wobble": return "摇摆";
                case "stretch": return "伸懒腰";
                case "dash": return "冲刺";
                case "nap": return "打盹";
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
            Snapshot s = Data.Current;
            if (lastPeak.HasValue && lastPeak.Value != s.IsPeak && Pets.Count > 0)
            {
                ShowBubble(Pets[0], s.IsPeak
                    ? "进高峰了，价格 x2，悠着点烧"
                    : "谷时到了，半价啦", 6);
            }
            lastPeak = s.IsPeak;
            CheckAlerts(s);
            if (Bubble.Visibility == Visibility.Visible)
            {
                Bubble.UpdateFrom(s, QuipFor("greeting"), bubbleOverride, PeakBubbleText(), s.IsPeak);
            }
            if (panel.IsVisible) panel.UpdateDataText();
        }

        private void CheckAlerts(Snapshot s)
        {
            ProviderState primary = s.Primary;
            if (cfg.GetFlag("alert", "balanceWarn", true)
                && s.Limits != null && s.Limits.AlertBelow.HasValue
                && primary != null && primary.Ok && primary.Balance.HasValue && Pets.Count > 0)
            {
                bool below = primary.Balance.Value < s.Limits.AlertBelow.Value;
                if (below && !balanceLatched)
                {
                    balanceLatched = true;
                    ShowBubble(Pets[0], "余额跌破 " + Fmt.Money(s.Limits.AlertBelow, primary.Currency) + " 了，该充钱啦！", 8);
                }
                else if (!below)
                {
                    balanceLatched = false;
                }
            }
            if (cfg.GetFlag("alert", "budgetWarn", true)
                && s.Limits != null && s.Limits.Budget.HasValue
                && primary != null && primary.TodayUsage.HasValue && Pets.Count > 0)
            {
                bool over = primary.TodayUsage.Value >= s.Limits.Budget.Value;
                string day = DataService.TodayKey();
                if (over && budgetLatchedDay != day)
                {
                    budgetLatchedDay = day;
                    ShowBubble(Pets[0], "今日花销已经超过 " + Fmt.Money(s.Limits.Budget, primary.Currency) + " 啦！", 8);
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
                node["x"] = Math.Round(pet.X);
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
            Snapshot s = Data.Current;
            StringBuilder sb = new StringBuilder();
            sb.Append("pets=" + Pets.Count);
            sb.Append(" peak=" + (s.IsPeak ? "peak" : "offpeak"));
            ProviderState primary = s.Primary;
            sb.Append(" primary=" + (primary == null ? "-" : primary.Id + ":" + Fmt.Money(primary.Balance, primary.Currency) + "(" + primary.SourceText + ")"));
            sb.Append(" providers=[");
            bool first = true;
            foreach (ProviderState p in s.Providers)
            {
                if (!p.Enabled) continue;
                if (!first) sb.Append(", ");
                first = false;
                sb.Append(p.Id + "=" + (p.Ok ? Fmt.Money(p.Balance, p.Currency) : "ERR"));
            }
            sb.Append("]");
            return sb.ToString();
        }
    }
}
