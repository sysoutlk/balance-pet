// 把一张静态图片变成"逐帧动画角色"。
//
// 输出结构（引擎已支持，见 Animations.cs）：
//   roles\<名字>\frame_00.png ...          空闲帧（放在根目录 = 默认/空闲状态）
//   roles\<名字>\walk\frame_00.png ...     走路
//   roles\<名字>\wave\frame_00.png ...     招手（每个动作一个子目录）
//
// 帧是程序化"形变"出来的：把整张图切成若干横条，按状态做挤压/拉伸/摇摆/扭转，
// 越靠下的横条相位越滞后，于是动起来有果冻感、呼吸感——静态贴图也能看着是活的。
// 生成后可以直接在画图软件里逐帧精修，或者用 AI 生成的图替换掉某一状态的帧。
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DshPet
{
    internal static class AnimMaker
    {
        private const int Slices = 8;       // 横向切片数：越多越柔，越少越硬朗
        private const double Fit = 0.94;    // 整体缩一点，给形变留出不裁切的空间

        /// <summary>要生成的状态：空闲/走路/睡觉 + 全部动作键 + 被撞。</summary>
        public static readonly string[] StateNames = new string[]
        {
            "idle", "walk", "sleep", "spin", "peek",
            "hop", "bounce", "roll", "wobble", "nod", "shake", "wave", "clap",
            "stretch", "yawn", "shy", "startle", "look", "greet", "lie", "pace",
            "stroll", "dash", "follow", "bump"
        };

        private static int FrameCount(string state)
        {
            switch (state)
            {
                case "idle": return 16;
                case "sleep": return 12;
                case "bounce": return 10;
                default: return 8;
            }
        }

        /// <summary>生成整套动画，返回写入的帧数（失败返回 -1）。</summary>
        public static int Build(string sourcePath, string roleName, double maxPixels, Action<string> log)
        {
            try
            {
                BitmapSource source = Load(sourcePath, maxPixels);
                if (source == null) return -1;
                string root = Path.Combine(AppPaths.RolesDir, roleName);
                Directory.CreateDirectory(root);
                int written = 0;
                foreach (string state in StateNames)
                {
                    int count = FrameCount(state);
                    string folder = state == "idle" ? root : Path.Combine(root, state);
                    Directory.CreateDirectory(folder);
                    for (int i = 0; i < count; i++)
                    {
                        BitmapSource frame = Warp(source, state, i / (double)count);
                        string file = Path.Combine(folder,
                            "frame_" + i.ToString("00", CultureInfo.InvariantCulture) + ".png");
                        Save(frame, file);
                        written++;
                    }
                    if (log != null) log(state);
                }
                return written;
            }
            catch (Exception ex)
            {
                if (log != null) log("error: " + ex.Message);
                return -1;
            }
        }

        private static BitmapSource Load(string path, double maxPixels)
        {
            // 命令行传进来的可能是相对路径：转成绝对路径再交给 BitmapImage。
            path = Path.GetFullPath(path);
            if (!File.Exists(path)) return null;
            BitmapImage bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(path);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            double w = bitmap.PixelWidth;
            double h = bitmap.PixelHeight;
            if (maxPixels > 0 && Math.Max(w, h) > maxPixels)
            {
                double scale = maxPixels / Math.Max(w, h);
                TransformedBitmap scaled = new TransformedBitmap(bitmap, new ScaleTransform(scale, scale));
                scaled.Freeze();
                return scaled;
            }
            return bitmap;
        }

        private static void Save(BitmapSource frame, string path)
        {
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(frame));
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                encoder.Save(stream);
            }
        }

        /// <summary>按状态把图切片形变出一帧（phase = 0..1 的一个循环位置）。</summary>
        private static BitmapSource Warp(BitmapSource source, string state, double phase)
        {
            double w = source.PixelWidth;
            double h = source.PixelHeight;
            double tau = Math.PI * 2;
            double p = phase;

            double sx = 1, sy = 1;        // 整体挤压/拉伸
            double sway = 0;              // 左右摆（相对宽度）
            double bob = 0;               // 上下弹（相对高度）
            double twist = 0;             // 切片扭转（度）
            double jiggle = 0;            // 切片内左右摆（相对宽度）
            double lag = 0.6;             // 下方切片滞后多少（越大越"软"）

            switch (state)
            {
                case "idle":
                    sy = 1 + 0.022 * Math.Sin(tau * p);
                    sx = 1 - 0.012 * Math.Sin(tau * p);
                    jiggle = 0.010;
                    lag = 0.8;
                    break;
                case "walk":
                    sy = 1 + 0.038 * Math.Sin(tau * 2 * p);
                    sx = 1 - 0.028 * Math.Sin(tau * 2 * p);
                    bob = -0.018 * Math.Abs(Math.Sin(tau * 2 * p));
                    twist = 2.4 * Math.Sin(tau * 2 * p);
                    lag = 0.9;
                    break;
                case "sleep":
                    sy = 1 + 0.032 * Math.Sin(tau * p);
                    sx = 1 - 0.016 * Math.Sin(tau * p);
                    jiggle = 0.008;
                    lag = 1.1;
                    break;
                case "spin":
                case "roll":
                    jiggle = 0.020;
                    twist = 5.0 * Math.Sin(tau * p);
                    lag = 1.3;
                    break;
                case "peek":
                    sx = 1 - 0.03 * Math.Sin(Math.PI * p);
                    jiggle = 0.012;
                    break;
                case "hop":
                    {
                        double up = Math.Sin(Math.PI * p);
                        sy = 1 + 0.11 * up;
                        sx = 1 - 0.07 * up;
                        bob = -0.05 * up;
                        lag = 1.4;
                        break;
                    }
                case "bounce":
                    {
                        double hit = Math.Abs(Math.Sin(tau * p));
                        sy = 1 + 0.09 * hit;
                        sx = 1 - 0.06 * hit;
                        bob = -0.035 * hit;
                        lag = 1.5;
                        break;
                    }
                case "wobble":
                    twist = 4.5 * Math.Sin(tau * p);
                    sway = 0.02 * Math.Sin(tau * p + 0.6);
                    lag = 1.2;
                    break;
                case "nod":
                    twist = 3.0 * Math.Sin(tau * p);
                    bob = 0.012 * Math.Sin(tau * p);
                    lag = 1.0;
                    break;
                case "shake":
                    sway = 0.028 * Math.Sin(tau * 2 * p);
                    jiggle = 0.018;
                    lag = 1.6;
                    break;
                case "wave":
                    twist = 4.0 * Math.Sin(tau * p);
                    sway = 0.014 * Math.Sin(tau * p);
                    jiggle = 0.016;
                    break;
                case "clap":
                    sy = 1 + 0.05 * Math.Abs(Math.Sin(tau * p));
                    sx = 1 - 0.05 * Math.Abs(Math.Sin(tau * p));
                    lag = 1.5;
                    break;
                case "stretch":
                    {
                        double s = Math.Sin(Math.PI * p);
                        sy = 1 + 0.16 * s;
                        sx = 1 - 0.09 * s;
                        lag = 1.2;
                        break;
                    }
                case "yawn":
                    {
                        double s = Math.Sin(Math.PI * p);
                        sy = 1 + 0.13 * s;
                        sx = 1 - 0.06 * s;
                        bob = -0.012 * s;
                        lag = 1.0;
                        break;
                    }
                case "shy":
                    {
                        double s = Math.Sin(Math.PI * p);
                        sy = 1 - 0.10 * s;
                        sx = 1 + 0.05 * s;
                        twist = -3.0 * s;
                        break;
                    }
                case "startle":
                    {
                        double s = Math.Exp(-3.2 * p) * Math.Sin(Math.PI * p);
                        sy = 1 + 0.14 * s;
                        sx = 1 - 0.08 * s;
                        lag = 1.6;
                        break;
                    }
                case "look":
                    twist = 2.0 * Math.Sin(tau * p);
                    jiggle = 0.014;
                    lag = 0.9;
                    break;
                case "greet":
                    sy = 1 + 0.06 * Math.Abs(Math.Sin(tau * p));
                    twist = 3.5 * Math.Sin(tau * p);
                    lag = 1.2;
                    break;
                case "lie":
                    sy = 1 + 0.030 * Math.Sin(tau * p);
                    sx = 1 - 0.015 * Math.Sin(tau * p);
                    jiggle = 0.010;
                    lag = 1.1;
                    break;
                case "pace":
                    twist = 2.6 * Math.Sin(tau * p);
                    sway = 0.012 * Math.Sin(tau * p);
                    break;
                case "stroll":
                    sy = 1 + 0.030 * Math.Sin(tau * 2 * p);
                    sx = 1 - 0.022 * Math.Sin(tau * 2 * p);
                    bob = -0.012 * Math.Abs(Math.Sin(tau * 2 * p));
                    twist = 1.8 * Math.Sin(tau * 2 * p);
                    lag = 0.9;
                    break;
                case "dash":
                    sy = 1 - 0.06;
                    sx = 1 + 0.07;
                    twist = 5.0 * Math.Sin(tau * 2 * p);
                    bob = -0.02 * Math.Abs(Math.Sin(tau * 2 * p));
                    lag = 1.5;
                    break;
                case "follow":
                    sy = 1 + 0.026 * Math.Sin(tau * p);
                    bob = -0.012 * Math.Abs(Math.Sin(tau * p));
                    jiggle = 0.010;
                    break;
                case "bump":
                    {
                        double s = Math.Exp(-4.0 * p) * Math.Sin(tau * p * 0.9 + 0.4);
                        sx = 1 + 0.10 * s;
                        sy = 1 - 0.10 * s;
                        twist = 4.0 * s;
                        lag = 1.7;
                        break;
                    }
            }

            DrawingVisual visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
            {
                dc.PushTransform(new ScaleTransform(Fit, Fit, w / 2, h));
                double sliceHeight = h / Slices;
                for (int i = 0; i < Slices; i++)
                {
                    double t = Slices <= 1 ? 0.5 : i / (double)(Slices - 1);   // 0 = 最上条
                    double top = i * sliceHeight;
                    // 源片段上下各多取 1px，拼接处不会露缝。
                    double sourceTop = Math.Max(0, top - 1);
                    double sourceBottom = Math.Min(h, top + sliceHeight + 1);
                    int cropTop = (int)Math.Floor(sourceTop);
                    int cropHeight = Math.Max(1, (int)Math.Ceiling(sourceBottom) - cropTop);
                    if (cropTop + cropHeight > (int)h) cropHeight = (int)h - cropTop;
                    CroppedBitmap slice = new CroppedBitmap(source, new Int32Rect(0, cropTop, (int)w, cropHeight));
                    slice.Freeze();

                    double localPhase = p - lag * 0.10 * (1 - t) * Math.PI;   // 下方条滞后
                    double localSy = 1 + (sy - 1) * (0.55 + 0.9 * t);
                    double localSx = 1 + (sx - 1) * (1.0 - 0.35 * t);
                    double x = w * sway * (0.3 + 1.4 * t) + w * jiggle * Math.Sin(localPhase * 2);
                    double y = h * bob * (0.5 + (1 - t));
                    double rot = twist * (t - 0.5) * 1.7;

                    double centerX = w / 2 + x;
                    double centerY = top + sliceHeight / 2 + y;
                    // Push/Pop 必须成对：条件性的变换要么都推、要么都不推，按计数弹出。
                    int pushes = 1;
                    dc.PushTransform(new TranslateTransform(centerX, centerY));
                    if (Math.Abs(rot) > 0.001)
                    {
                        dc.PushTransform(new RotateTransform(rot));
                        pushes++;
                    }
                    if (Math.Abs(localSx - 1) > 0.0001 || Math.Abs(localSy - 1) > 0.0001)
                    {
                        dc.PushTransform(new ScaleTransform(localSx, localSy));
                        pushes++;
                    }
                    dc.DrawImage(slice, new Rect(-w / 2, -cropHeight / 2.0, w, cropHeight));
                    for (int pop = 0; pop < pushes; pop++) dc.Pop();
                }
                dc.Pop();
            }

            RenderTargetBitmap target = new RenderTargetBitmap(
                Math.Max(1, (int)Math.Round(w)), Math.Max(1, (int)Math.Round(h)), 96, 96, PixelFormats.Pbgra32);
            target.Render(visual);
            target.Freeze();
            return target;
        }
    }
}
