// 特效层：烟花、爱心、闪光、撒花。全部用代码画的粒子，不需要任何素材文件。
//
// 每个桌宠挂一个独立的小透明窗（点它不挡鼠标：WS_EX_TRANSPARENT），窗口跟着桌宠走，
// 粒子在窗口里画。动作（"fireworks" / "hearts" / "sparkles" / "confetti"）触发后
// 持续发射一段时间，然后自己消失。
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace DshPet
{
    internal sealed class Particle
    {
        public int Kind;            // 0 火花 1 升空的火箭 2 爱心 3 星星 4 纸屑
        public double X, Y, VX, VY;
        public double Life, MaxLife;
        public double Size;
        public double Angle, Spin;
        public Color Color;
        public double Fuse;         // 火箭：还有多久炸开
        public double Sway;         // 爱心左右飘的相位
        public double Gravity = 320;
    }

    /// <summary>画粒子的那块画布（挂在小透明窗里）。</summary>
    internal sealed class FxCanvas : FrameworkElement
    {
        public readonly List<Particle> Particles = new List<Particle>();
        private static readonly Geometry HeartGeometry = Geometry.Parse(
            "M 0,3 C 0,1 1.5,-1 3.6,-1 C 5.4,-1 6.4,0.4 6.5,1.2 C 6.6,0.4 7.6,-1 9.4,-1 C 11.5,-1 13,1 13,3 " +
            "C 13,6 9,9.5 6.5,11.5 C 4,9.5 0,6 0,3 Z");
        private static readonly Geometry StarGeometry = Geometry.Parse(
            "M 0,-7 L 1.8,-1.8 L 7,0 L 1.8,1.8 L 0,7 L -1.8,1.8 L -7,0 L -1.8,-1.8 Z");

        protected override void OnRender(DrawingContext dc)
        {
            foreach (Particle p in Particles)
            {
                double fade = p.Life / p.MaxLife;
                if (fade > 1) fade = 1;
                if (fade < 0) fade = 0;
                byte alpha = (byte)Math.Round(255 * fade);
                if (alpha < 8) continue;
                Color color = Color.FromArgb(alpha, p.Color.R, p.Color.G, p.Color.B);
                SolidColorBrush brush = new SolidColorBrush(color);
                brush.Freeze();
                switch (p.Kind)
                {
                    case 0:   // 火花：亮点 + 外圈
                        dc.DrawEllipse(brush, null, new Point(p.X, p.Y), p.Size, p.Size);
                        break;
                    case 1:   // 火箭：小圆点 + 尾巴
                        Pen tail = new Pen(brush, Math.Max(1, p.Size * 0.8));
                        tail.Freeze();
                        dc.DrawLine(tail, new Point(p.X, p.Y), new Point(p.X, p.Y + p.Size * 4));
                        dc.DrawEllipse(brush, null, new Point(p.X, p.Y), p.Size * 1.4, p.Size * 1.4);
                        break;
                    case 2:   // 爱心
                        dc.PushTransform(new TranslateTransform(p.X - 6.5 * p.Size / 8, p.Y - 5 * p.Size / 8));
                        dc.PushTransform(new ScaleTransform(p.Size / 8, p.Size / 8));
                        dc.DrawGeometry(brush, null, HeartGeometry);
                        dc.Pop();
                        dc.Pop();
                        break;
                    case 3:   // 星星
                        dc.PushTransform(new TranslateTransform(p.X, p.Y));
                        dc.PushTransform(new RotateTransform(p.Angle));
                        dc.PushTransform(new ScaleTransform(p.Size / 7, p.Size / 7));
                        dc.DrawGeometry(brush, null, StarGeometry);
                        dc.Pop();
                        dc.Pop();
                        dc.Pop();
                        break;
                    default:  // 纸屑
                        dc.PushTransform(new TranslateTransform(p.X, p.Y));
                        dc.PushTransform(new RotateTransform(p.Angle));
                        dc.DrawRectangle(brush, null, new Rect(-p.Size / 2, -p.Size / 4, p.Size, p.Size / 2));
                        dc.Pop();
                        dc.Pop();
                        break;
                }
            }
        }
    }

    /// <summary>一个桌宠的特效层：负责生成/推进粒子，并把窗口贴在桌宠身上。</summary>
    internal sealed class FxLayer
    {
        public const double WindowSize = 420;
        private readonly FxCanvas canvas = new FxCanvas();
        private Window window;
        private readonly List<Particle> particles = new List<Particle>();
        private readonly Random rng = new Random();
        private string mode = "";
        private double left;             // 本次特效还要发射多久
        private double spawnTimer;
        private double burstTimer;
        private double swayClock;
        private double spawnX;      // 发射点相对中心偏移（贴边喷灰用）
        private double spawnY;

        public bool Busy { get { return mode.Length > 0 || particles.Count > 0; } }

        /// <summary>开始一段特效（mode: fireworks / hearts / sparkles / confetti）。</summary>
        public void Start(string kind, double seconds)
        {
            Start(kind, seconds, 0, 0);
        }

        public void Start(string kind, double seconds, double dx, double dy)
        {
            if (window == null)
            {
                window = new Window();
                window.WindowStyle = WindowStyle.None;
                window.AllowsTransparency = true;
                window.Background = null;
                window.Topmost = true;
                window.ShowInTaskbar = false;
                window.ShowActivated = false;
                window.ResizeMode = ResizeMode.NoResize;
                window.Width = WindowSize;
                window.Height = WindowSize;
                window.IsHitTestVisible = false;
                window.Content = canvas;
                canvas.Width = WindowSize;
                canvas.Height = WindowSize;
                window.SourceInitialized += delegate
                {
                    // 点特效不挡鼠标：整窗透明穿透 + 不抢焦点 + 不进 Alt-Tab。
                    IntPtr handle = new WindowInteropHelper(window).Handle;
                    int style = Native.GetWindowLong(handle, Native.GWL_EXSTYLE);
                    style |= Native.WS_EX_NOACTIVATE | Native.WS_EX_TOOLWINDOW | Native.WS_EX_TRANSPARENT;
                    Native.SetWindowLong(handle, Native.GWL_EXSTYLE, style);
                };
            }
            mode = kind;
            left = seconds;
            spawnTimer = 0;
            burstTimer = 0.35;
            spawnX = dx;
            spawnY = dy;
            if (!window.IsVisible) window.Show();
            canvas.InvalidateVisual();
        }

        public void Stop()
        {
            mode = "";
            left = 0;
            particles.Clear();
            if (window != null && window.IsVisible) window.Hide();
        }

        public void Close()
        {
            if (window == null) return;
            try { window.Close(); } catch { }
            window = null;
        }

        /// <summary>把特效窗口贴到桌宠身上（桌宠每动一下就调一次）。</summary>
        public void Follow(double petX, double petY, double petW, double petH)
        {
            if (window == null || !window.IsVisible) return;
            window.Left = Math.Round(petX + petW / 2 - WindowSize / 2, 1);
            window.Top = Math.Round(petY + petH / 2 - WindowSize / 2, 1);
        }

        /// <summary>推进粒子；有东西在动就返回 true（引擎据此保持高帧率）。</summary>
        public bool Update(double dt, double petW, double petH)
        {
            if (dt <= 0) return false;
            if (dt > 0.2) dt = 0.2;
            swayClock += dt;
            bool alive = false;
            if (mode.Length > 0)
            {
                left -= dt;
                spawnTimer -= dt;
                burstTimer -= dt;
                Spawn(petW, petH);
                if (left <= 0) mode = "";
            }
            double cx = WindowSize / 2;
            double cy = WindowSize / 2;
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                Particle p = particles[i];
                p.Life -= dt;
                if (p.Life <= 0)
                {
                    particles.RemoveAt(i);
                    continue;
                }
                if (p.Kind == 1)
                {
                    p.Fuse -= dt;
                    if (p.Fuse <= 0) { Explode(p); particles.RemoveAt(i); continue; }
                }
                p.VY += p.Gravity * dt;
                p.X += p.VX * dt;
                p.Y += p.VY * dt;
                if (p.Kind == 2) p.X += Math.Sin(swayClock * 3 + p.Sway) * 26 * dt;   // 爱心左右飘
                p.Angle += p.Spin * dt;
                alive = true;
            }
            canvas.Particles.Clear();
            canvas.Particles.AddRange(particles);
            if (alive || mode.Length > 0)
            {
                canvas.InvalidateVisual();
                return true;
            }
            if (window != null && window.IsVisible) window.Hide();
            return false;
        }

        private void Spawn(double petW, double petH)
        {
            double cx = WindowSize / 2;
            double cy = WindowSize / 2;
            switch (mode)
            {
                case "fireworks":
                    if (burstTimer <= 0)
                    {
                        burstTimer = 0.75 + rng.NextDouble() * 0.45;
                        Particle rocket = new Particle();
                        rocket.Kind = 1;
                        rocket.X = cx + (rng.NextDouble() * 2 - 1) * petW * 0.6;
                        rocket.Y = cy + petH * 0.25;
                        rocket.VX = (rng.NextDouble() * 2 - 1) * 40;
                        rocket.VY = -(320 + rng.NextDouble() * 120);
                        rocket.Gravity = 60;
                        rocket.Fuse = 0.55 + rng.NextDouble() * 0.25;
                        rocket.Life = 4;
                        rocket.MaxLife = 4;
                        rocket.Size = 3.2;
                        rocket.Color = HueColor(rng.NextDouble() * 360);
                        particles.Add(rocket);
                    }
                    break;
                case "hearts":
                    if (spawnTimer <= 0)
                    {
                        spawnTimer = 0.22;
                        Particle heart = new Particle();
                        heart.Kind = 2;
                        heart.X = cx + (rng.NextDouble() * 2 - 1) * petW * 0.45;
                        heart.Y = cy + petH * 0.1;
                        heart.VY = -(60 + rng.NextDouble() * 40);
                        heart.Gravity = -6;
                        heart.Size = 9 + rng.NextDouble() * 8;
                        heart.Life = 1.5 + rng.NextDouble() * 0.5;
                        heart.MaxLife = heart.Life;
                        heart.Sway = rng.NextDouble() * 6;
                        heart.Color = rng.NextDouble() < 0.5
                            ? Color.FromRgb(0xFF, 0x6E, 0xA8)
                            : Color.FromRgb(0xFF, 0x47, 0x6B);
                        particles.Add(heart);
                    }
                    break;
                case "sparkles":
                    if (spawnTimer <= 0)
                    {
                        spawnTimer = 0.10;
                        double angle = rng.NextDouble() * Math.PI * 2;
                        double radius = petW * 0.35 + rng.NextDouble() * petW * 0.45;
                        Particle star = new Particle();
                        star.Kind = 3;
                        star.X = cx + Math.Cos(angle) * radius;
                        star.Y = cy + Math.Sin(angle) * radius * 0.8;
                        star.Gravity = 0;
                        star.VY = -12;
                        star.Size = 6 + rng.NextDouble() * 9;
                        star.Life = 0.5 + rng.NextDouble() * 0.5;
                        star.MaxLife = star.Life;
                        star.Angle = rng.NextDouble() * 360;
                        star.Spin = (rng.NextDouble() * 2 - 1) * 120;
                        star.Color = rng.NextDouble() < 0.4
                            ? Color.FromRgb(0xFF, 0xF3, 0xBF)
                            : HueColor(rng.NextDouble() * 360);
                        particles.Add(star);
                    }
                    break;
                case "confetti":
                    if (spawnTimer <= 0)
                    {
                        spawnTimer = 0.05;
                        Particle chip = new Particle();
                        chip.Kind = 4;
                        chip.X = cx + (rng.NextDouble() * 2 - 1) * petW * 1.1;
                        chip.Y = cy - petH * 0.8 - rng.NextDouble() * 60;
                        chip.VX = (rng.NextDouble() * 2 - 1) * 30;
                        chip.VY = 70 + rng.NextDouble() * 70;
                        chip.Gravity = 40;
                        chip.Size = 6 + rng.NextDouble() * 6;
                        chip.Life = 1.6 + rng.NextDouble() * 0.8;
                        chip.MaxLife = chip.Life;
                        chip.Angle = rng.NextDouble() * 360;
                        chip.Spin = (rng.NextDouble() * 2 - 1) * 260;
                        chip.Color = HueColor(rng.NextDouble() * 360);
                        particles.Add(chip);
                    }
                    break;
                case "puff":
                    if (spawnTimer <= 0)
                    {
                        spawnTimer = 0.045;
                        Particle puff = new Particle();
                        puff.Kind = 0;
                        puff.X = cx + spawnX + (rng.NextDouble() * 2 - 1) * 8;
                        puff.Y = cy + spawnY + (rng.NextDouble() * 2 - 1) * 8;
                        puff.VX = (rng.NextDouble() * 2 - 1) * 60 + spawnX * 0.6;
                        puff.VY = (rng.NextDouble() * 2 - 1) * 60 + spawnY * 0.6;
                        puff.Gravity = -20;
                        puff.Size = 5 + rng.NextDouble() * 8;
                        puff.Life = 0.35 + rng.NextDouble() * 0.25;
                        puff.MaxLife = puff.Life;
                        byte grey = (byte)(190 + rng.Next(50));
                        puff.Color = Color.FromRgb(grey, grey, grey);
                        particles.Add(puff);
                    }
                    break;
            }
        }

        private void Explode(Particle rocket)
        {
            int sparks = 26 + rng.Next(10);
            bool warm = rng.NextDouble() < 0.5;
            for (int i = 0; i < sparks; i++)
            {
                double angle = rng.NextDouble() * Math.PI * 2;
                double speed = 90 + rng.NextDouble() * 240;
                Particle spark = new Particle();
                spark.Kind = 0;
                spark.X = rocket.X;
                spark.Y = rocket.Y;
                spark.VX = Math.Cos(angle) * speed;
                spark.VY = Math.Sin(angle) * speed;
                spark.Gravity = 260;
                spark.Size = 2 + rng.NextDouble() * 2.4;
                spark.Life = 0.8 + rng.NextDouble() * 0.7;
                spark.MaxLife = spark.Life;
                spark.Color = warm
                    ? HueColor(20 + rng.NextDouble() * 60)
                    : HueColor(180 + rng.NextDouble() * 140);
                particles.Add(spark);
            }
            for (int i = 0; i < 8; i++)   // 白色闪星，让爆开更亮
            {
                double angle = rng.NextDouble() * Math.PI * 2;
                double speed = 40 + rng.NextDouble() * 120;
                Particle twinkle = new Particle();
                twinkle.Kind = 3;
                twinkle.X = rocket.X;
                twinkle.Y = rocket.Y;
                twinkle.VX = Math.Cos(angle) * speed;
                twinkle.VY = Math.Sin(angle) * speed;
                twinkle.Gravity = 200;
                twinkle.Size = 5 + rng.NextDouble() * 6;
                twinkle.Life = 0.5 + rng.NextDouble() * 0.4;
                twinkle.MaxLife = twinkle.Life;
                twinkle.Angle = rng.NextDouble() * 360;
                twinkle.Spin = (rng.NextDouble() * 2 - 1) * 200;
                twinkle.Color = Colors.White;
                particles.Add(twinkle);
            }
        }

        private static Color HueColor(double degrees)
        {
            double h = (degrees % 360 + 360) % 360 / 60.0;
            double x = 1 - Math.Abs(h % 2 - 1);
            double r = 0, g = 0, b = 0;
            if (h < 1) { r = 1; g = x; }
            else if (h < 2) { r = x; g = 1; }
            else if (h < 3) { g = 1; b = x; }
            else if (h < 4) { g = x; b = 1; }
            else if (h < 5) { r = x; b = 1; }
            else { r = 1; b = x; }
            return Color.FromRgb((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
        }
    }
}
