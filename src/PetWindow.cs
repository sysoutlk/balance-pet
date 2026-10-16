// The pet itself: a transparent, always-on-top, non-activating window that walks,
// snaps to screen edges, can be thrown with the mouse and performs little actions.
//
// The visual is either a user-supplied character image from roles\ or the built-in
// vector mascot, both animated through the same transform group.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DshPet
{
    internal sealed class PetWindow : Window
    {
        public static readonly string[] ActionKeys = new string[]
        {
            "hop", "spin", "wobble", "stretch", "dash", "nap", "follow", "greet"
        };

        private readonly Config cfg;
        private readonly TransformGroup group = new TransformGroup();
        private FrameworkElement visual;

        public readonly TranslateTransform Bob = new TranslateTransform();
        public readonly ScaleTransform Flip = new ScaleTransform(1, 1);
        public readonly ScaleTransform Squash = new ScaleTransform(1, 1);
        public readonly RotateTransform Spin = new RotateTransform(0);

        public string Role = "";
        public double BaseWidth = Mascot.BaseWidth;
        public double BaseHeight = Mascot.BaseHeight;
        public double X;
        public double Y;
        public double TargetX;
        public double TargetY;
        public double SpeedFactor = 1.0;
        public double VelocityX;
        public double VelocityY;
        public double Phase;
        public string Mode = "idle";
        public int Facing = 1;
        public DateTime NextWalkAt = DateTime.UtcNow.AddSeconds(3);
        public DateTime NextActionAt = DateTime.UtcNow.AddSeconds(12);
        public string Action = "";
        public string LastActionKey = "";
        public DateTime ActionStart = DateTime.MinValue;
        public DateTime ActionUntil = DateTime.MinValue;
        public IntPtr Handle = IntPtr.Zero;

        private int appliedX = int.MinValue;
        private int appliedY = int.MinValue;
        private DragState drag;

        private sealed class DragState
        {
            public Point Start;
            public double OriginX;
            public double OriginY;
            public double LastX;
            public double LastY;
            public DateTime LastAt = DateTime.UtcNow;
            public double VX;
            public double VY;
            public bool Moved;
            public string PreviousMode = "idle";
        }

        public PetWindow(Config config)
        {
            cfg = config;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = null;
            Topmost = true;
            ShowInTaskbar = false;
            ShowActivated = false;
            ResizeMode = ResizeMode.NoResize;
            RenderTransformOrigin = new Point(0.5, 0.5);
            Phase = Program.Rng.NextDouble() * Math.PI * 2;

            group.Children.Add(Spin);
            group.Children.Add(Squash);
            group.Children.Add(Flip);
            group.Children.Add(Bob);

            UseMascot();

            SourceInitialized += OnSourceInitialized;
            MouseLeftButtonDown += OnMouseDown;
            MouseMove += OnMouseMove;
            MouseLeftButtonUp += OnMouseUp;
            MouseDoubleClick += OnDoubleClick;
            MouseRightButtonUp += OnRightClick;
            MouseWheel += OnWheel;
        }

        private void AttachVisual(FrameworkElement element)
        {
            visual = element;
            visual.RenderTransform = group;
            visual.RenderTransformOrigin = new Point(0.5, 0.5);
            Content = visual;
        }

        private void UseMascot()
        {
            Role = "";
            BaseWidth = Mascot.BaseWidth;
            BaseHeight = Mascot.BaseHeight;
            AttachVisual(Mascot.CreateVisual());
        }

        /// <summary>Loads roles\&lt;role&gt;, falling back to the built-in mascot when the file
        /// is missing (fresh clones have no character art).</summary>
        public bool ApplyRole(string role)
        {
            if (string.IsNullOrEmpty(role))
            {
                UseMascot();
                return false;
            }
            try
            {
                string path = Path.Combine(AppPaths.RolesDir, role);
                if (!File.Exists(path))
                {
                    UseMascot();
                    return false;
                }
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(path);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                Image image = new Image();
                image.Stretch = Stretch.Fill;
                image.Source = bitmap;
                Role = role;
                BaseWidth = bitmap.PixelWidth;
                BaseHeight = bitmap.PixelHeight;
                AttachVisual(image);
                ResizePet();
                return true;
            }
            catch
            {
                UseMascot();
                return false;
            }
        }

        public void ResizePet()
        {
            double scale = cfg.GetNumber("", "scale", 1.0);
            Width = Math.Max(40, Math.Round(BaseWidth * scale));
            Height = Math.Max(40, Math.Round(BaseHeight * scale));
            Opacity = cfg.GetNumber("", "opacity", 1.0);
            Left = Math.Round(X, 1);
            Top = Math.Round(Y, 1);
            appliedX = (int)Math.Round(X);
            appliedY = (int)Math.Round(Y);
        }

        public void Place(double x, double y)
        {
            X = x;
            Y = y;
            ScreenArea area = ScreenArea.ForPoint(X + Width / 2, Y + Height / 2);
            X = Math.Max(area.Left, Math.Min(X, area.Right - Width));
            Y = Math.Max(area.Top, Math.Min(Y, area.Bottom - Height));
            ApplyPosition();
        }

        public void ApplyPosition()
        {
            int x = (int)Math.Round(X);
            int y = (int)Math.Round(Y);
            if (Handle != IntPtr.Zero)
            {
                if (x != appliedX || y != appliedY)
                {
                    Native.SetWindowPos(Handle, IntPtr.Zero, x, y, 0, 0,
                        Native.SWP_NOSIZE | Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
                    appliedX = x;
                    appliedY = y;
                }
                return;
            }
            Left = X;
            Top = Y;
            appliedX = x;
            appliedY = y;
        }

        public void SetFacing(int value)
        {
            if (value == Facing) return;
            Facing = value;
            Flip.ScaleX = value;
        }

        public bool Sleeping
        {
            get
            {
                if (!cfg.GetFlag("sleep", "enabled", true)) return false;
                int hour = DateTime.Now.Hour;
                int from = (int)cfg.GetNumber("sleep", "fromHour", 23);
                int to = (int)cfg.GetNumber("sleep", "toHour", 7);
                if (from <= to) return hour >= from && hour < to;
                return hour >= from || hour < to;
            }
        }

        public void SetIdle()
        {
            Mode = "idle";
            Bob.Y = 0;
            int min = (int)cfg.GetNumber("walk", "pauseMinSec", 8);
            int max = (int)cfg.GetNumber("walk", "pauseMaxSec", 26);
            if (max <= min) max = min + 1;
            NextWalkAt = DateTime.UtcNow.AddSeconds(Program.Rng.Next(min, max));
            int actionMin = (int)cfg.GetNumber("actions", "pauseMinSec", 14);
            int actionMax = Math.Max(actionMin + 1, (int)cfg.GetNumber("actions", "pauseMaxSec", 45));
            NextActionAt = DateTime.UtcNow.AddSeconds(Program.Rng.Next(actionMin, actionMax));
            if (cfg.GetFlag("snap", "enabled", true)) Snap();
        }

        public void Snap()
        {
            ScreenArea area = ScreenArea.ForPoint(X + Width / 2, Y + Height / 2);
            double margin = cfg.GetNumber("snap", "marginPx", 6);
            double distance = cfg.GetNumber("snap", "distancePx", 140);
            string best = "";
            double bestGap = double.MaxValue;
            double gapLeft = X - area.Left;
            double gapRight = area.Right - (X + Width);
            double gapTop = Y - area.Top;
            double gapBottom = area.Bottom - (Y + Height);
            if (gapLeft <= distance && gapLeft < bestGap) { best = "left"; bestGap = gapLeft; }
            if (gapRight <= distance && gapRight < bestGap) { best = "right"; bestGap = gapRight; }
            if (gapTop <= distance && gapTop < bestGap) { best = "top"; bestGap = gapTop; }
            if (gapBottom <= distance && gapBottom < bestGap) { best = "bottom"; bestGap = gapBottom; }
            if (best == "left") { X = area.Left + margin; SetFacing(1); }
            else if (best == "right") { X = area.Right - Width - margin; SetFacing(-1); }
            else if (best == "top") { Y = area.Top + margin; }
            else if (best == "bottom") { Y = area.Bottom - Height - margin; }
            else return;
            ApplyPosition();
        }

        public void StartWalk()
        {
            if (!cfg.GetFlag("walk", "enabled", true)) return;
            ScreenArea area = ScreenArea.ForPoint(X + Width / 2, Y + Height / 2);
            double margin = cfg.GetNumber("walk", "marginPx", 12);
            double minX = area.Left + margin;
            double maxX = Math.Max(minX, area.Right - Width - margin);
            double minY = area.Top + margin;
            double maxY = Math.Max(minY, area.Bottom - Height - margin);
            TargetX = minX + Program.Rng.NextDouble() * (maxX - minX);
            TargetY = minY + Program.Rng.NextDouble() * (maxY - minY);
            if (Math.Abs(TargetX - X) < 40)
            {
                TargetX = TargetX > X ? Math.Min(maxX, X + 160) : Math.Max(minX, X - 160);
            }
            SpeedFactor = 1.0;
            Mode = "walk";
        }

        public void StartAction(string action)
        {
            if (action == "random")
            {
                List<string> pool = cfg.GetStrings("actions", "pool");
                if (pool.Count == 0) return;
                action = pool[Program.Rng.Next(pool.Count)];
            }
            DateTime now = DateTime.UtcNow;
            LastActionKey = action;
            switch (action)
            {
                case "hop": Action = "hop"; ActionUntil = now.AddMilliseconds(420); break;
                case "spin": Action = "spin"; ActionUntil = now.AddMilliseconds(620); break;
                case "wobble": Action = "wobble"; ActionUntil = now.AddMilliseconds(1000); break;
                case "stretch": Action = "stretch"; ActionUntil = now.AddMilliseconds(760); break;
                case "dash":
                    StartWalk();
                    if (Mode == "walk") SpeedFactor = 3.0;
                    Action = "dash";
                    ActionUntil = now.AddSeconds(3);
                    break;
                case "nap": Action = "nap"; ActionUntil = now.AddSeconds(20); break;
                case "follow": Action = "follow"; ActionUntil = now.AddSeconds(12); break;
                case "greet": Action = "hop"; ActionUntil = now.AddMilliseconds(420); break;
                default: return;
            }
            ActionStart = now;
        }

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            Handle = new WindowInteropHelper(this).Handle;
            int extended = Native.GetWindowLong(Handle, Native.GWL_EXSTYLE);
            extended = extended | Native.WS_EX_NOACTIVATE | Native.WS_EX_TOOLWINDOW;
            Native.SetWindowLong(Handle, Native.GWL_EXSTYLE, extended);
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            drag = new DragState();
            drag.Start = e.GetPosition(this);
            drag.OriginX = X;
            drag.OriginY = Y;
            drag.LastX = X;
            drag.LastY = Y;
            drag.PreviousMode = Mode;
            Mode = "drag";
            CaptureMouse();
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (drag == null) return;
            Point point = e.GetPosition(this);
            X = drag.OriginX + (point.X - drag.Start.X);
            Y = drag.OriginY + (point.Y - drag.Start.Y);
            ScreenArea area = ScreenArea.ForPoint(X + Width / 2, Y + Height / 2);
            X = Math.Max(area.Left - 8, Math.Min(X, area.Right - Width + 8));
            Y = Math.Max(area.Top - 8, Math.Min(Y, area.Bottom - Height + 8));
            ApplyPosition();
            DateTime now = DateTime.UtcNow;
            double span = (now - drag.LastAt).TotalSeconds;
            if (span > 0.02)
            {
                drag.VX = (X - drag.LastX) / span;
                drag.VY = (Y - drag.LastY) / span;
                drag.LastX = X;
                drag.LastY = Y;
                drag.LastAt = now;
            }
            if (Math.Abs(point.X - drag.Start.X) > 4 || Math.Abs(point.Y - drag.Start.Y) > 4) drag.Moved = true;
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (drag == null) return;
            ReleaseMouseCapture();
            DragState state = drag;
            drag = null;
            double speed = Math.Sqrt(state.VX * state.VX + state.VY * state.VY);
            if (speed > 320)
            {
                VelocityX = Math.Max(-2400, Math.Min(2400, state.VX));
                VelocityY = Math.Max(-2400, Math.Min(2400, state.VY));
                Mode = "thrown";
                return;
            }
            if (state.Moved)
            {
                SetIdle();
                return;
            }
            Mode = state.PreviousMode == "drag" ? "idle" : state.PreviousMode;
            EventHandler handler = Clicked;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void OnDoubleClick(object sender, MouseButtonEventArgs e)
        {
            EventHandler handler = DoubleClicked;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void OnRightClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            EventHandler handler = ContextRequested;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void OnWheel(object sender, MouseWheelEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
            Action<object, bool> handler = WheelZoomed;
            if (handler != null) handler(this, e.Delta > 0);
        }

        public event EventHandler Clicked;
        public event EventHandler DoubleClicked;
        public event EventHandler ContextRequested;
        public event Action<object, bool> WheelZoomed;
    }
}
