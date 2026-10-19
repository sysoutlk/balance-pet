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

namespace BalancePet
{
    internal sealed class PetWindow : Window
    {
        public static readonly string[] ActionKeys = new string[]
        {
            "hop", "bounce", "spin", "roll", "wobble", "nod", "shake", "wave",
            "clap", "stretch", "yawn", "shy", "startle", "look", "greet",
            "lie", "sleep", "pace", "peek", "stroll", "dash", "follow",
            "fireworks", "hearts", "sparkles", "confetti"
        };

        private readonly Config cfg;
        private readonly TransformGroup group = new TransformGroup();
        private FrameworkElement visual;
        private Grid host;
        private Canvas propLayer;           // 装扮层（帽子等挂件，画在角色上面）
        private Image propImage;            // 当前挂件
        private string propLoaded = "";     // 已加载的挂件路径，避免反复解码
        private FxLayer fx;                 // 特效层（烟花/爱心/闪光/撒花）
        private RoleAnim animation;
        private WhaleAnimator whale;
        private string animState = "idle";
        private FrameSet activeSet;
        private double animTime;

        public readonly TranslateTransform Bob = new TranslateTransform();
        public readonly ScaleTransform Flip = new ScaleTransform(1, 1);
        public readonly ScaleTransform Squash = new ScaleTransform(1, 1);
        public readonly RotateTransform Spin = new RotateTransform(0);
        // 贴边专用：朝墙倾一点 + 挤压时鼓一下再落回（和动作动画用的那几组互不打扰）
        public readonly ScaleTransform TuckShape = new ScaleTransform(1, 1);
        public readonly RotateTransform TuckSpin = new RotateTransform(0);
        private double tuckTurn;            // 贴边时翻转的角度（让头朝屏幕里）
        private double tuckProgress;        // 翻转进度（缓入缓出后的 t）
        private double tuckWobble;          // 落位后的回弹
        private double tuckWriggle;         // 收着的时候偶尔扭一下
        private double tuckIdleClock;
        private double nextWriggle;
        public double TuckOffset;           // 贴边时的位移小动作（正值 = 更深地缩进边缘）

        public string Role = "";
        public double BaseWidth = Mascot.BaseWidth;
        public double BaseHeight = Mascot.BaseHeight;
        public double X;
        public double Y;
        public double TargetX;
        public double TargetY;
        public double FinalX;                // 这次散步的最终目的地（折线一段段绕过去）
        public double FinalY;
        public int WalkLegs;                 // 还剩几段折线
        public double WobbleSeed = Program.Rng.NextDouble() * Math.PI * 2;   // 蛇形摆动的相位种子
        private double walkMinX, walkMaxX, walkMinY, walkMaxY;
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
        public double ActionOriginX;
        public int PageIndex;
        public string EdgeSide = "";
        public bool EdgeHidden;
        public DateTime EdgeHideAt = DateTime.MaxValue;
        public double EdgeShownX;
        public double EdgeShownY;
        public double Tuck;                 // 0 = fully out, 1 = squeezed against the edge
        public double TargetTuck;
        public bool Dropped;                // the user just let go of the pet
        public IntPtr Handle = IntPtr.Zero;

        private int appliedX = int.MinValue;
        private int appliedY = int.MinValue;
        private DragState drag;
        private bool suppressNextClick;

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
            public bool NativeLoop;
        }

        public PetWindow(Config config)
        {
            cfg = config;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            // A 1/255 alpha background keeps the whole sprite rectangle hit-testable, so
            // clicks and drags also work on the transparent margins of the artwork.
            Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
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
            group.Children.Add(TuckShape);
            group.Children.Add(TuckSpin);

            UseMascot();

            SourceInitialized += OnSourceInitialized;
            MouseLeftButtonDown += OnMouseDown;
            MouseMove += OnMouseMove;
            MouseLeftButtonUp += OnMouseUp;
            MouseDoubleClick += OnDoubleClick;
            MouseRightButtonUp += OnRightClick;
            MouseWheel += OnWheel;
            MouseEnter += OnMouseEnter;
            LocationChanged += OnLocationChanged;
        }

        private void AttachVisual(FrameworkElement element)
        {
            visual = element;
            visual.RenderTransform = group;
            visual.RenderTransformOrigin = new Point(0.5, 0.5);
            // 视觉树：角色 → 装扮层（帽子等）→ 特效层（烟花等，在独立窗口里）
            host = new Grid();
            host.Children.Add(visual);
            propLayer = new Canvas();
            propLayer.IsHitTestVisible = false;
            host.Children.Add(propLayer);
            Content = host;
            RefreshProp();
        }

        /// <summary>装扮层：按角色盒子的相对坐标把帽子之类的挂件摆上去（留空 = 不戴）。</summary>
        public void RefreshProp()
        {
            if (propLayer == null) return;
            propLayer.Children.Clear();
            propImage = null;
            propLoaded = "";
            string name = cfg.GetStringAt("props.hat", "");
            if (string.IsNullOrEmpty(name)) return;
            string path = AppPaths.ResolveProp(name);
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(path);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                propImage = new Image();
                propImage.Source = bitmap;
                propImage.Stretch = Stretch.Uniform;
                propLoaded = path;
                ApplyPropLayout();
                propLayer.Children.Add(propImage);
            }
            catch
            {
                propImage = null;
            }
        }

        private void ApplyPropLayout()
        {
            if (propImage == null) return;
            BitmapSource source = propImage.Source as BitmapSource;
            if (source == null || source.PixelWidth <= 0) return;
            double size = Math.Max(0.05, Math.Min(1.6, cfg.GetNumberAt("props.hatSize", 0.55)));
            double w = Width * size;
            double h = w * source.PixelHeight / source.PixelWidth;
            propImage.Width = w;
            propImage.Height = h;
            double px = Math.Max(-0.5, Math.Min(1.5, cfg.GetNumberAt("props.hatX", 0.5)));
            double py = Math.Max(-0.5, Math.Min(1.5, cfg.GetNumberAt("props.hatY", 0.04)));
            Canvas.SetLeft(propImage, Width * px - w / 2);
            Canvas.SetTop(propImage, Height * py - h / 2);
        }

        private void UseMascot()
        {
            Role = "";
            BaseWidth = Mascot.BaseWidth;
            BaseHeight = Mascot.BaseHeight;
            animation = null;
            whale = new WhaleAnimator();
            AttachVisual(whale.Root);
        }

        /// <summary>Loads roles\&lt;role&gt;, falling back to the built-in mascot when the file
        /// is missing (fresh clones have no character art).</summary>
        public bool ApplyRole(string role)
        {
            RoleAnim loaded = RoleAnim.Load(cfg, role);
            if (loaded == null)
            {
                UseMascot();
                return false;
            }
            try
            {
                FrameSet idle = loaded.For("idle");
                BitmapSource first = idle == null ? null : idle.Advance(0, 1);
                if (first == null)
                {
                    UseMascot();
                    return false;
                }
                Image image = new Image();
                image.Stretch = Stretch.Fill;
                image.Source = first;
                Role = role;
                BaseWidth = first.PixelWidth;
                BaseHeight = first.PixelHeight;
                animation = loaded;
                whale = null;
                activeSet = null;
                animState = "idle";
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

        /// <summary>特效层：没有特效时什么都不做，有就推进粒子（烟花等）。</summary>
        private void UpdateFx(double dt)
        {
            if (fx == null || !fx.Busy) return;
            fx.Update(dt, Width, Height);
        }

        /// <summary>播一段特效（fireworks / hearts / sparkles / confetti）。</summary>
        public void PlayFx(string kind, double seconds)
        {
            PlayFx(kind, seconds, 0, 0);
        }

        /// <summary>播一段特效，dx/dy 是发射点相对桌宠中心的偏移（贴边喷灰用）。</summary>
        public void PlayFx(string kind, double seconds, double dx, double dy)
        {
            if (!cfg.GetFlagAt("fx.enabled", true)) return;
            if (fx == null) fx = new FxLayer();
            fx.Follow(X, Y, Width, Height);
            fx.Start(kind, seconds, dx, dy);
        }

        public bool FxBusy
        {
            get { return fx != null && fx.Busy; }
        }

        /// <summary>Selects the animation state (idle / walk / one action key / sleep ...).</summary>
        public void SetAnimState(string state)
        {
            if (state == animState) return;
            animState = state;
            activeSet = null;
        }

        /// <summary>Advances the frame animation (or the vector mascot) by one tick.</summary>
        public void Advance(double dt, double speed)
        {
            animTime += dt;
            UpdateFx(dt);
            UpdateTuckMotion(dt);
            if (whale != null)
            {
                whale.Update(animTime, speed);
                return;
            }
            if (animation == null) return;
            FrameSet set = animation.For(animState);
            if (set == null || set.Empty) return;
            if (!ReferenceEquals(set, activeSet))
            {
                activeSet = set;
                set.Reset();
            }
            BitmapSource frame = set.Advance(dt, speed);
            Image image = visual as Image;
            if (image != null && frame != null && !ReferenceEquals(image.Source, frame)) image.Source = frame;
        }

        public void ResizePet()
        {
            double scale = cfg.GetNumber("", "scale", 1.0);
            Width = Math.Max(40, Math.Round(BaseWidth * scale));
            Height = Math.Max(40, Math.Round(BaseHeight * scale));
            ApplyPropLayout();
            Opacity = cfg.GetNumber("", "opacity", 1.0);
            ApplyTuck(Tuck);
            Left = Math.Round(X, 1);
            Top = Math.Round(Y, 1);
            appliedX = (int)Math.Round(X);
            appliedY = (int)Math.Round(Y);
        }

        /// <summary>
        /// 贴边：整只滑到屏幕外面去，只在屏幕边上留一小条，**不做任何压缩变形**。
        /// 关键在于"不管贴哪条边，露出来的都是头"：贴左右边时把角色侧躺 90°滑出去，
        /// 贴上边时倒过来 180°，贴下边保持原样——这样留在边上的那一条永远是头/上半身。
        /// t = 0 完全露出，t = 1 完全收进去；位置由引擎的 UpdateEdgeSlide 插值，
        /// 外加落位回弹/呼吸（TuckOffset，只有位移没有形变）。
        /// </summary>
        public void ApplyTuck(double t)
        {
            if (t < 0) t = 0;
            if (t > 1) t = 1;
            Tuck = t;
            double scale = cfg.GetNumber("", "scale", 1.0);
            double baseW = Math.Max(40, Math.Round(BaseWidth * scale));
            double baseH = Math.Max(40, Math.Round(BaseHeight * scale));
            // 头朝向屏幕里：左边 +90°（头朝右）、右边 -90°（头朝左）、上边 180°（头朝下）
            if (EdgeSide == "left") tuckTurn = 90;
            else if (EdgeSide == "right") tuckTurn = -90;
            else if (EdgeSide == "top") tuckTurn = 180;
            else tuckTurn = 0;
            tuckProgress = t * t * (3 - 2 * t);   // 缓入缓出，翻转不愣
            if (visual != null)
            {
                // 角色按原始长宽渲染（不拉伸），旋转后由窗口包住
                visual.Width = baseW;
                visual.Height = baseH;
            }
            bool sideways = EdgeSide == "left" || EdgeSide == "right";
            Width = sideways ? baseH : baseW;
            Height = sideways ? baseW : baseH;
            if (propLayer != null)
            {
                // 贴边过程中把装扮层收起来（不然帽子会孤零零飘着）
                propLayer.Visibility = t > 0.05 ? Visibility.Collapsed : Visibility.Visible;
            }
            UpdateTuckLook();
            Left = Math.Round(X, 1);
            Top = Math.Round(Y, 1);
            appliedX = (int)Math.Round(X);
            appliedY = (int)Math.Round(Y);
        }

        /// <summary>贴边只有"转身 + 位移"：把翻转到当前进度，形变一律复位。</summary>
        private void UpdateTuckLook()
        {
            TuckShape.ScaleX = 1;
            TuckShape.ScaleY = 1;
            TuckSpin.Angle = tuckTurn * tuckProgress;
        }

        /// <summary>
        /// 贴着边时的小动作，全部用位移表达（不动形变）：
        /// 收进去的一瞬间冲过头再回弹；收好之后每隔几秒往屏幕外/里轻轻探一下（呼吸感）。
        /// </summary>
        private void UpdateTuckMotion(double dt)
        {
            bool tucked = Tuck > 0.001 || tuckWobble > 0;
            if (!tucked) return;
            tuckIdleClock += dt;
            if (tuckWobble > 0)
            {
                tuckWobble -= dt;
                if (tuckWobble < 0) tuckWobble = 0;
            }
            if (tuckWriggle > 0)
            {
                tuckWriggle -= dt;
                if (tuckWriggle < 0) tuckWriggle = 0;
            }
            if (Tuck > 0.999 && tuckWobble <= 0 && tuckWriggle <= 0 && tuckIdleClock >= nextWriggle)
            {
                tuckWriggle = 0.9;                                  // 收着的时候隔几秒探一下头
                nextWriggle = tuckIdleClock + 5 + Program.Rng.NextDouble() * 6;
            }
            // 全部换算成"往屏幕里/外挪多少像素"：正值 = 更深地缩进边缘
            double offset = 0;
            if (tuckWobble > 0) offset -= Math.Sin((0.55 - tuckWobble) * 30) * tuckWobble * 16;
            if (tuckWriggle > 0) offset += Math.Sin((0.9 - tuckWriggle) * 16) * tuckWriggle * 6;
            if (Tuck > 0.999 && tuckWobble <= 0) offset += Math.Sin(tuckIdleClock * 2.1) * 2.2;
            TuckOffset = offset;
            // 回弹/探头时轻轻晃一下角度（仍然是"转身"，不是拉扁）
            TuckSpin.Angle = tuckTurn * tuckProgress
                + (tuckWobble > 0 ? Math.Sin((0.55 - tuckWobble) * 30) * tuckWobble * 4 : 0)
                + (tuckWriggle > 0 ? Math.Sin((0.9 - tuckWriggle) * 16) * tuckWriggle * 2.5 : 0);
        }

        /// <summary>贴边落位/弹出时的收尾：回弹一下，收进去时再喷一小团灰（有特效层才喷）。</summary>
        public void OnTuckSettled(bool hidden)
        {
            tuckIdleClock = 0;
            nextWriggle = 4.5 + Program.Rng.NextDouble() * 5;
            tuckWobble = hidden ? 0.55 : 0.4;
            if (!hidden) return;
            double dx = EdgeSide == "left" ? -Width * 0.45 : (EdgeSide == "right" ? Width * 0.45 : 0);
            double dy = EdgeSide == "top" ? -Height * 0.45 : (EdgeSide == "bottom" ? Height * 0.45 : 0);
            PlayFx("puff", 0.45, dx, dy);
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
            if (fx != null && fx.Busy) fx.Follow(X, Y, Width, Height);   // 特效窗口跟着桌宠走
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
            SetIdle(true);
        }

        /// <summary>被扔出去/被撞飞的收尾：就地停下，不吸附边缘（不然扔起来的乐趣全被吸走了）。</summary>
        public void SetIdleAfterThrow()
        {
            SetIdle(false);
        }

        private void SetIdle(bool snapToEdge)
        {
            Mode = "idle";
            // 拖离边缘后彻底解除贴边并恢复姿态；仍贴在边缘时保持收起状态。
            if (Tuck != 0 && !IsAgainstOwnEdge())
            {
                TargetTuck = 0;
                ApplyTuck(0);
                EdgeSide = "";
                EdgeHideAt = DateTime.MaxValue;
                EdgeHidden = false;
            }
            Bob.Y = 0;
            int min = (int)cfg.GetNumber("walk", "pauseMinSec", 8);
            int max = (int)cfg.GetNumber("walk", "pauseMaxSec", 26);
            if (max <= min) max = min + 1;
            NextWalkAt = DateTime.UtcNow.AddSeconds(Program.Rng.Next(min, max));
            int actionMin = (int)cfg.GetNumber("actions", "pauseMinSec", 14);
            int actionMax = Math.Max(actionMin + 1, (int)cfg.GetNumber("actions", "pauseMaxSec", 45));
            NextActionAt = DateTime.UtcNow.AddSeconds(Program.Rng.Next(actionMin, actionMax));
            if (snapToEdge && cfg.GetFlag("snap", "enabled", true)) Snap();
        }

        /// <summary>是否仍然紧贴着自己记录的那条边。</summary>
        public bool IsAgainstOwnEdge()
        {
            if (string.IsNullOrEmpty(EdgeSide)) return false;
            ScreenArea area = ScreenArea.ForPoint(X + Width / 2, Y + Height / 2);
            if (EdgeSide == "left") return X <= area.Left + 10;
            if (EdgeSide == "right") return X + Width >= area.Right - 10;
            if (EdgeSide == "top") return Y <= area.Top + 10;
            if (EdgeSide == "bottom") return Y + Height >= area.Bottom - 10;
            return false;
        }

        /// <summary>立刻解除贴边并恢复原始姿态（拖拽开始时调用，不再播放收起动画）。</summary>
        public void Untuck()
        {
            TargetTuck = 0;
            ApplyTuck(0);
            EdgeSide = "";
            EdgeHideAt = DateTime.MaxValue;
            EdgeHidden = false;
        }

        public void Snap()
        {
            ScreenArea area = ScreenArea.ForPoint(X + Width / 2, Y + Height / 2);
            double margin = cfg.GetNumber("snap", "marginPx", 6);
            double distance = cfg.GetNumber("snap", "distancePx", 56);
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
            else
            {
                // Not near any edge: forget the previous attachment, otherwise the pet would
                // later stroll back to that edge and tuck itself in.
                EdgeSide = "";
                EdgeHideAt = DateTime.MaxValue;
                return;
            }
            ApplyPosition();
            EdgeSide = best;
            EdgeShownX = X;
            EdgeShownY = Y;
            EdgeHidden = false;
        }

        /// <summary>Slides the pet out of (or back from) the screen edge it is snapped to.</summary>
        public void StartEdgeSlide(bool hide)
        {
            if (string.IsNullOrEmpty(EdgeSide)) return;
            if (hide)
            {
                EdgeShownX = X;
                EdgeShownY = Y;
                TargetTuck = 1;
            }
            else
            {
                TargetTuck = 0;
            }
            SpeedFactor = 1.0;
            Mode = "edge";
        }

        public void StartWalk()
        {
            if (!cfg.GetFlag("walk", "enabled", true)) return;
            ScreenArea area = ScreenArea.ForPoint(X + Width / 2, Y + Height / 2);
            double margin = cfg.GetNumber("walk", "marginPx", 12);
            // Keep normal strolls out of the snap zone: only a user drag (or a throw) should
            // park the pet against an edge, otherwise it would tuck itself in on its own.
            double snapDistance = cfg.GetFlag("snap", "enabled", true) ? cfg.GetNumber("snap", "distancePx", 56) : 0;
            margin += snapDistance;
            double minX = area.Left + margin;
            double maxX = Math.Max(minX, area.Right - Width - margin);
            double minY = area.Top + margin;
            double maxY = Math.Max(minY, area.Bottom - Height - margin);
            walkMinX = minX;
            walkMaxX = maxX;
            walkMinY = minY;
            walkMaxY = maxY;
            FinalX = minX + Program.Rng.NextDouble() * (maxX - minX);
            FinalY = minY + Program.Rng.NextDouble() * (maxY - minY);
            if (Math.Abs(FinalX - X) < 40)
            {
                FinalX = FinalX > X ? Math.Min(maxX, X + 160) : Math.Max(minX, X - 160);
            }
            // 折线散步：一段一段绕过去（每段带侧向偏移）+ 行进时蛇形摆动，不再是直线冲终点。
            WalkLegs = 2 + Program.Rng.Next(0, 3);
            NextWaypoint();
            SpeedFactor = 0.9 + Program.Rng.NextDouble() * 0.35;
            Mode = "walk";
        }

        /// <summary>下一段路点：落在"当前位置 → 终点"的 30%~75% 处，再叠一点垂直方向的偏移。</summary>
        private void NextWaypoint()
        {
            double dx = FinalX - X;
            double dy = FinalY - Y;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            if (distance < 10)
            {
                TargetX = FinalX;
                TargetY = FinalY;
                return;
            }
            double fraction = 0.3 + Program.Rng.NextDouble() * 0.45;
            double px = X + dx * fraction;
            double py = Y + dy * fraction;
            double side = (Program.Rng.NextDouble() * 2 - 1) * Math.Min(130, distance * 0.55);
            px += -dy / distance * side;
            py += dx / distance * side;
            TargetX = Math.Max(walkMinX, Math.Min(walkMaxX, px));
            TargetY = Math.Max(walkMinY, Math.Min(walkMaxY, py));
        }

        /// <summary>走完一段：还有剩的就接着绕（返回 true），路走完了返回 false = 本次散步结束。</summary>
        public bool NextLeg()
        {
            if (WalkLegs <= 0) return false;
            WalkLegs--;
            NextWaypoint();
            return true;
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
            ActionOriginX = X;
            LastActionKey = action;
            switch (action)
            {
                case "hop": Action = "hop"; ActionUntil = now.AddMilliseconds(480); break;
                case "bounce": Action = "bounce"; ActionUntil = now.AddMilliseconds(950); break;
                case "spin": Action = "spin"; ActionUntil = now.AddMilliseconds(680); break;
                case "roll": Action = "roll"; ActionUntil = now.AddMilliseconds(1200); break;
                case "wobble": Action = "wobble"; ActionUntil = now.AddMilliseconds(1100); break;
                case "nod": Action = "nod"; ActionUntil = now.AddMilliseconds(1200); break;
                case "shake": Action = "shake"; ActionUntil = now.AddMilliseconds(950); break;
                case "wave": Action = "wave"; ActionUntil = now.AddMilliseconds(1300); break;
                case "clap": Action = "clap"; ActionUntil = now.AddMilliseconds(950); break;
                case "stretch": Action = "stretch"; ActionUntil = now.AddMilliseconds(950); break;
                case "yawn": Action = "yawn"; ActionUntil = now.AddMilliseconds(1700); break;
                case "shy": Action = "shy"; ActionUntil = now.AddMilliseconds(1500); break;
                case "startle": Action = "startle"; ActionUntil = now.AddMilliseconds(600); break;
                case "look": Action = "look"; ActionUntil = now.AddMilliseconds(1900); break;
                case "greet": Action = "greet"; ActionUntil = now.AddMilliseconds(1500); break;
                case "lie": Action = "lie"; ActionUntil = now.AddSeconds(6); break;
                case "sleep": Action = "sleep"; ActionUntil = now.AddSeconds(25); break;
                case "pace": Action = "pace"; ActionUntil = now.AddMilliseconds(2600); break;
                case "peek": Action = "peek"; ActionUntil = now.AddSeconds(4); break;
                case "stroll":
                    StartWalk();
                    SpeedFactor = 0.55;
                    Action = "";
                    ActionUntil = DateTime.MinValue;
                    break;
                case "dash":
                    StartWalk();
                    SpeedFactor = 3.0;
                    Action = "";
                    ActionUntil = DateTime.MinValue;
                    break;
                case "follow": Action = "follow"; ActionUntil = now.AddSeconds(12); break;
                // 特效动作：角色自己保持小幅度姿态，真正的"内容"在特效层（粒子）
                case "fireworks": Action = "hop"; ActionUntil = now.AddMilliseconds(600); PlayFx("fireworks", 3.2); break;
                case "hearts": Action = "shy"; ActionUntil = now.AddMilliseconds(900); PlayFx("hearts", 2.8); break;
                case "sparkles": Action = "nod"; ActionUntil = now.AddMilliseconds(700); PlayFx("sparkles", 2.6); break;
                case "confetti": Action = "bounce"; ActionUntil = now.AddMilliseconds(900); PlayFx("confetti", 3.0); break;
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
            Closed += delegate { if (fx != null) fx.Close(); };   // 桌宠关了，特效窗也别留着
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            drag = new DragState();
            drag.Start = e.GetPosition(this);
            drag.OriginX = X;
            drag.OriginY = Y;
            drag.LastX = X;
            drag.LastY = Y;
            CaptureMouse();
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (drag == null) return;
            Point point = e.GetPosition(this);
            double dx = point.X - drag.Start.X;
            double dy = point.Y - drag.Start.Y;
            // 9px 起步才算拖动：双击时手的轻微抖动不会把点击吞掉。
            if (Math.Abs(dx) > 9 || Math.Abs(dy) > 9) drag.Moved = true;
            if (!drag.Moved || drag.NativeLoop) return;

            // Hand the move over to the OS once the mouse is clearly dragging. The native
            // move loop is smooth (no fighting with WPF's own window positioning) and keeps
            // the pet glued to the cursor; positions are sampled through LocationChanged.
            drag.NativeLoop = true;
            drag.LastX = X;
            drag.LastY = Y;
            drag.LastAt = DateTime.UtcNow;
            Mode = "drag";
            Untuck();   // 一拖动就立刻还原，不等松手
            Native.ReleaseCapture();
            Native.SendMessage(Handle, Native.WM_NCLBUTTONDOWN, (IntPtr)Native.HTCAPTION, IntPtr.Zero);
            FinishDrag(drag);
        }

        private void OnLocationChanged(object sender, EventArgs e)
        {
            if (drag == null || !drag.NativeLoop) return;
            X = Left;
            Y = Top;
            DateTime now = DateTime.UtcNow;
            double span = (now - drag.LastAt).TotalSeconds;
            if (span > 0.02)
            {
                drag.VX = (X - drag.LastX) / span * 0.6 + drag.VX * 0.4;
                drag.VY = (Y - drag.LastY) / span * 0.6 + drag.VY * 0.4;
                drag.LastX = X;
                drag.LastY = Y;
                drag.LastAt = now;
            }
            EventHandler handler = Moved;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (drag == null) return;
            ReleaseMouseCapture();
            FinishDrag(drag);
        }

        private void FinishDrag(DragState state)
        {
            drag = null;
            double moved = Math.Abs(X - state.OriginX) + Math.Abs(Y - state.OriginY);
            if (moved <= 9)
            {
                Mode = "idle";
                if (suppressNextClick)
                {
                    // The second press of a double click: the DoubleClicked handler already ran.
                    suppressNextClick = false;
                    return;
                }
                EventHandler handler = Clicked;
                if (handler != null) handler(this, EventArgs.Empty);
                return;
            }
            double speed = Math.Sqrt(state.VX * state.VX + state.VY * state.VY);
            Dropped = true;
            if (Tuck > 0.01 || EdgeHidden) Untuck();
            // 抛出要"又快又远"才算：慢速松手只是放下，避免拖出来时误触发抛出。
            double throwSpeed = cfg.GetNumber("throw", "minSpeedPxPerSec", 900);
            if (throwSpeed <= 0) throwSpeed = double.MaxValue;   // 配 0 = 关闭抛出
            double travel = Math.Abs(X - state.OriginX) + Math.Abs(Y - state.OriginY);
            if (travel > 90 && speed > throwSpeed)
            {
                VelocityX = Math.Max(-2400, Math.Min(2400, state.VX));
                VelocityY = Math.Max(-2400, Math.Min(2400, state.VY));
                Mode = "thrown";
                return;
            }
            SetIdle();
        }

        private void OnMouseEnter(object sender, MouseEventArgs e)
        {
            EventHandler handler = Hovered;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void OnDoubleClick(object sender, MouseButtonEventArgs e)
        {
            suppressNextClick = true;
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
        public event EventHandler Hovered;
        public event EventHandler Moved;
        public event Action<object, bool> WheelZoomed;
    }
}
