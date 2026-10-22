// Bubble themes. "system" follows the Windows app theme, "glass"/"frost" are translucent
// glassmorphism looks (a real backdrop blur is impossible while the window uses WPF
// transparency, so these simulate it with alpha, highlights and a softer shadow) and
// "monet" samples the desktop wallpaper and tint the whole card with its dominant colour.
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace BalancePet
{
    internal sealed class ThemePalette
    {
        public Color Card = Colors.White;
        public Color Border = Colors.Gray;
        public Color Title = Colors.Black;
        public Color Big = Colors.Black;
        public Color Muted = Colors.Gray;
        public Color RowValue = Colors.Black;
        public Color QuipBackground = Colors.WhiteSmoke;
        public Color QuipText = Colors.Black;
        public Color Source = Colors.Gray;
        public Color Bar = Colors.LightGray;
        public Color BarEmpty = Colors.Gainsboro;
        public Color Accent = Colors.RoyalBlue;
        public double ShadowOpacity = 0.28;
        public bool Dark;
        public bool PreserveAlpha;
    }

    internal static class BubbleTheme
    {
        public static ThemePalette Resolve(string theme, Config cfg = null)
        {
            if (string.Equals(theme, "system", StringComparison.OrdinalIgnoreCase))
            {
                theme = SystemPrefersDark() ? "dark" : "light";
            }
            if (string.Equals(theme, "glass", StringComparison.OrdinalIgnoreCase)) return Glass(cfg);
            if (string.Equals(theme, "frost", StringComparison.OrdinalIgnoreCase)) return Frost();
            if (string.Equals(theme, "monet", StringComparison.OrdinalIgnoreCase)) return Monet(cfg);
            if (string.Equals(theme, "dim", StringComparison.OrdinalIgnoreCase)) return DimDark();
            return string.Equals(theme, "dark", StringComparison.OrdinalIgnoreCase) ? Dark() : Light();
        }

        public static bool SystemPrefersDark()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    object value = key == null ? null : key.GetValue("AppsUseLightTheme");
                    if (value != null) return Convert.ToInt32(value) == 0;
                }
            }
            catch
            {
            }
            return false;
        }

        private static ThemePalette Light()
        {
            ThemePalette p = new ThemePalette();
            p.Card = Alpha("#FFFFFF", 247);
            p.Border = Alpha("#2C3A6E", 30);
            p.Title = FromHex("#2A3C74");
            p.Big = FromHex("#1D2F66");
            p.Muted = FromHex("#8A94B2");
            p.RowValue = FromHex("#26346B");
            p.QuipBackground = FromHex("#F1F4FF");
            p.QuipText = FromHex("#33478F");
            p.Source = FromHex("#A9B3CC");
            p.Bar = FromHex("#D3DBF0");
            p.BarEmpty = FromHex("#E9EDF8");
            p.ShadowOpacity = 0.28;
            return p;
        }

        private static ThemePalette Dark()
        {
            ThemePalette p = new ThemePalette();
            p.Dark = true;
            p.Card = Alpha("#181E2E", 247);
            p.Border = Alpha("#FFFFFF", 51);
            p.Title = FromHex("#E7EDFF");
            p.Big = Colors.White;
            p.Muted = FromHex("#93A0C0");
            p.RowValue = FromHex("#E2E9FC");
            p.QuipBackground = FromHex("#242C46");
            p.QuipText = FromHex("#C4D0F2");
            p.Source = FromHex("#7C86A2");
            p.Bar = FromHex("#3C4666");
            p.BarEmpty = FromHex("#2A3247");
            p.ShadowOpacity = 0.45;
            return p;
        }

        private static ThemePalette DimDark()
        {
            ThemePalette p = new ThemePalette();
            p.Dark = true;
            p.Card = Alpha("#252B3B", 247);
            p.Border = Alpha("#FFFFFF", 42);
            p.Title = FromHex("#EEF2FF");
            p.Big = Colors.White;
            p.Muted = FromHex("#A4AEC8");
            p.RowValue = FromHex("#E8EDFB");
            p.QuipBackground = FromHex("#333B55");
            p.QuipText = FromHex("#D2DAF3");
            p.Source = FromHex("#8A94AE");
            p.Bar = FromHex("#4A5472");
            p.BarEmpty = FromHex("#363F58");
            p.ShadowOpacity = 0.38;
            return p;
        }

        /// <summary>Liquid glass: translucent card with tunable opacity, highlight and rim.</summary>
        private static ThemePalette Glass(Config cfg)
        {
            ThemePalette p = Light();
            double opacity = cfg == null ? 0.745 : cfg.GetNumber("bubble", "glassOpacity", 0.745);
            double highlight = cfg == null ? 0.47 : cfg.GetNumber("bubble", "glassHighlight", 0.47);
            double border = cfg == null ? 0.59 : cfg.GetNumber("bubble", "glassBorder", 0.59);
            opacity = Math.Max(0.15, Math.Min(1.0, opacity));
            highlight = Math.Max(0, Math.Min(1.0, highlight));
            border = Math.Max(0, Math.Min(1.0, border));
            p.Card = Alpha("#FFFFFF", (byte)Math.Round(opacity * 255));
            p.Border = Alpha("#FFFFFF", (byte)Math.Round(border * 255));
            p.QuipBackground = Alpha("#FFFFFF", (byte)Math.Round(70 + highlight * 130));
            p.ShadowOpacity = 0.22;
            p.Bar = Alpha("#7C87A8", (byte)Math.Round(80 + highlight * 100));
            p.BarEmpty = Alpha("#7C87A8", (byte)Math.Round(25 + highlight * 55));
            p.PreserveAlpha = true;
            return p;
        }

        /// <summary>Frosted: darker translucent card, reads well over bright wallpapers.</summary>
        private static ThemePalette Frost()
        {
            ThemePalette p = Light();
            p.Card = Alpha("#F2F5FF", 205);
            p.Border = Alpha("#FFFFFF", 170);
            p.QuipBackground = Alpha("#DCE4FF", 150);
            p.ShadowOpacity = 0.3;
            return p;
        }

        /// <summary>Monet: tint everything with the wallpaper's dominant colour.</summary>
        private static ThemePalette Monet(Config cfg)
        {
            double lightness;
            Color baseColor = MonetSourceColor(cfg, out lightness);
            double hue = Hue(baseColor);
            bool darkWallpaper = lightness < 0.5;
            ThemePalette p = darkWallpaper ? Dark() : Light();
            p.Dark = darkWallpaper;
            p.Card = darkWallpaper ? Hsl(hue, 0.30, 0.13, 244) : Hsl(hue, 0.38, 0.96, 246);
            p.Border = darkWallpaper ? Alpha("#FFFFFF", 46) : Hsl(hue, 0.35, 0.62, 60);
            p.Title = darkWallpaper ? Hsl(hue, 0.35, 0.90) : Hsl(hue, 0.55, 0.32);
            p.Big = darkWallpaper ? Colors.White : Hsl(hue, 0.60, 0.22);
            p.Muted = darkWallpaper ? Hsl(hue, 0.22, 0.72) : Hsl(hue, 0.30, 0.52);
            p.RowValue = darkWallpaper ? Hsl(hue, 0.30, 0.93) : Hsl(hue, 0.50, 0.28);
            p.QuipBackground = darkWallpaper ? Hsl(hue, 0.32, 0.20) : Hsl(hue, 0.55, 0.92);
            p.QuipText = darkWallpaper ? Hsl(hue, 0.45, 0.88) : Hsl(hue, 0.55, 0.30);
            p.Source = darkWallpaper ? Hsl(hue, 0.18, 0.62) : Hsl(hue, 0.25, 0.62);
            p.Bar = darkWallpaper ? Hsl(hue, 0.28, 0.40) : Hsl(hue, 0.45, 0.78);
            p.BarEmpty = darkWallpaper ? Hsl(hue, 0.25, 0.26) : Hsl(hue, 0.40, 0.90);
            p.Accent = Hsl(hue, 0.68, darkWallpaper ? 0.64 : 0.50);
            p.ShadowOpacity = darkWallpaper ? 0.42 : 0.26;
            return p;
        }

        private static Color MonetSourceColor(Config cfg, out double lightness)
        {
            lightness = 0.5;
            string custom = cfg == null ? ""
                : cfg.GetString("bubble", "monetCustomColor", "");
            if (!string.IsNullOrEmpty(custom) && custom.StartsWith("#", StringComparison.Ordinal))
            {
                try
                {
                    Color color = (Color)ColorConverter.ConvertFromString(custom);
                    double max = Math.Max(color.R, Math.Max(color.G, color.B)) / 255.0;
                    double min = Math.Min(color.R, Math.Min(color.G, color.B)) / 255.0;
                    lightness = (max + min) / 2.0;
                    return color;
                }
                catch
                {
                }
            }
            return WallpaperColor(out lightness);
        }

        // ------------------------------------------------------------ 壁纸取色
        private static Color WallpaperColor(out double lightness)
        {
            lightness = 0.5;
            try
            {
                string path = Convert.ToString(Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "WallPaper", ""));
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return FromHex("#4B6BFB");
                BitmapImage image = new BitmapImage();
                image.BeginInit();
                image.UriSource = new Uri(path);
                image.DecodePixelWidth = 48;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.EndInit();
                FormatConvertedBitmap converted = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
                int width = converted.PixelWidth;
                int height = converted.PixelHeight;
                if (width < 1 || height < 1) return FromHex("#4B6BFB");
                byte[] pixels = new byte[width * height * 4];
                converted.CopyPixels(pixels, width * 4, 0);
                double sumR = 0, sumG = 0, sumB = 0, sumLight = 0, sumWeight = 0;
                int samples = 0;
                for (int i = 0; i + 3 < pixels.Length; i += 4)
                {
                    double b = pixels[i], g = pixels[i + 1], r = pixels[i + 2];
                    double max = Math.Max(r, Math.Max(g, b));
                    double min = Math.Min(r, Math.Min(g, b));
                    double light = (max + min) / 510.0;
                    if (light < 0.10 || light > 0.94) continue;
                    double saturation = max <= 0 ? 0 : (max - min) / max;
                    double weight = 0.3 + saturation;
                    sumR += r * weight;
                    sumG += g * weight;
                    sumB += b * weight;
                    sumLight += light;
                    sumWeight += weight;
                    samples++;
                }
                if (sumWeight <= 0 || samples == 0) return FromHex("#4B6BFB");
                lightness = sumLight / samples;
                return Color.FromRgb((byte)(sumR / sumWeight), (byte)(sumG / sumWeight), (byte)(sumB / sumWeight));
            }
            catch
            {
                return FromHex("#4B6BFB");
            }
        }

        // ------------------------------------------------------------ 颜色助手
        private static Color FromHex(string hex)
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }

        private static Color Alpha(string hex, byte alpha)
        {
            Color color = FromHex(hex);
            return Color.FromArgb(alpha, color.R, color.G, color.B);
        }

        /// <summary>
        /// 混合配色（bubble.colors）统一入口：最多保留 bubble.mixMax 个（默认 6），超出时顶掉
        /// 最早设置的颜色——"加了新的就别再长长一条"。
        /// </summary>
        public static List<string> MixColors(Config cfg)
        {
            List<string> list = new List<string>();
            foreach (string hex in cfg.GetStrings("bubble", "colors"))
            {
                if (!string.IsNullOrEmpty(hex) && hex.StartsWith("#", StringComparison.Ordinal)) list.Add(hex);
            }
            int max = (int)cfg.GetNumberAt("bubble.mixMax", 6);
            if (max < 2) max = 2;
            if (max > 12) max = 12;
            while (list.Count > max) list.RemoveAt(0);
            return list;
        }

        private static double Hue(Color color)
        {
            double r = color.R / 255.0, g = color.G / 255.0, b = color.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double d = max - min;
            if (d < 0.0001) return 220;
            double h;
            if (max == r) h = 60 * (((g - b) / d) % 6);
            else if (max == g) h = 60 * ((b - r) / d + 2);
            else h = 60 * ((r - g) / d + 4);
            return (h % 360 + 360) % 360;
        }

        private static Color Hsl(double hueDegrees, double saturation, double lightness, byte alpha = 255)
        {
            double h = (hueDegrees % 360 + 360) % 360 / 60.0;
            double c = (1 - Math.Abs(2 * lightness - 1)) * saturation;
            double x = c * (1 - Math.Abs(h % 2 - 1));
            double m = lightness - c / 2;
            double r = 0, g = 0, b = 0;
            if (h < 1) { r = c; g = x; }
            else if (h < 2) { r = x; g = c; }
            else if (h < 3) { g = c; b = x; }
            else if (h < 4) { g = x; b = c; }
            else if (h < 5) { r = x; b = c; }
            else { r = c; b = x; }
            return Color.FromArgb(alpha,
                (byte)Math.Round((r + m) * 255),
                (byte)Math.Round((g + m) * 255),
                (byte)Math.Round((b + m) * 255));
        }
    }

    /// <summary>
    /// 气泡动效的画刷工厂：气泡本体与设置里的实时预览共用同一套算法，两边速度、幅度一致。
    /// 每一档都调到"一眼看得出来"：旋转 = 转轴快速旋转，呼吸 = 明暗 + 透明起伏，
    /// 闪动 = 一道高光来回扫过，漂移 = 色相持续平移；流动 / 彩虹沿用滑移与全色环。
    /// </summary>
    internal static class BubbleEffects
    {
        public static Brush Brush(string mode, List<Color> colors, Color colorA, Color colorB, double phase, double depth, bool reverse)
        {
            double direction = reverse ? -1 : 1;
            List<Color> list = new List<Color>();
            if (colors != null) list.AddRange(colors);
            if (list.Count == 0)
            {
                list.Add(colorA);
                list.Add(colorB);
            }
            else if (list.Count == 1)
            {
                list.Add(colorB);
            }
            switch (mode)
            {
                case "rainbow":
                    {
                        LinearGradientBrush brush = Axis(phase * 0.2, 0.6);
                        for (int i = 0; i <= 5; i++)
                        {
                            brush.GradientStops.Add(new GradientStop(HueColor(phase * 260 + i * 72), i / 5.0));
                        }
                        return brush;
                    }
                case "flow":
                    {
                        LinearGradientBrush brush = Axis(phase * 0.35, 0.6);
                        for (int i = 0; i < list.Count; i++)
                        {
                            double offset = ((i / (double)list.Count) + phase * 0.32 * direction) % 1.0;
                            brush.GradientStops.Add(new GradientStop(list[i], (offset % 1 + 1) % 1));
                        }
                        brush.GradientStops.Add(new GradientStop(list[0], 1.0));
                        return brush;
                    }
                case "swirl":
                    {
                        // 约 4 秒转一整圈的转轴 + 色标轻微错位，看着就是在打转。
                        LinearGradientBrush brush = Axis(phase * 1.9 * direction, 0.65);
                        for (int i = 0; i < list.Count; i++)
                        {
                            double offset = ((i / (double)list.Count) + phase * 0.07 * direction) % 1.0;
                            brush.GradientStops.Add(new GradientStop(list[i], (offset % 1 + 1) % 1));
                        }
                        brush.GradientStops.Add(new GradientStop(list[0], 1.0));
                        return brush;
                    }
                case "breathe":
                    {
                        // 亮度和透明度一起起伏（约 5 秒一个来回），像在呼吸。
                        double wave = 0.5 + 0.5 * Math.Sin(direction * phase * 3.2);
                        double factor = 1 - depth * 0.5 * wave;
                        byte alpha = (byte)Math.Round(255 - 130 * depth * wave);
                        LinearGradientBrush brush = Axis(phase * 0.2, 0.6);
                        for (int i = 0; i < list.Count; i++)
                        {
                            Color c = Scale(list[i], factor);
                            brush.GradientStops.Add(new GradientStop(Color.FromArgb(alpha, c.R, c.G, c.B), i / (double)list.Count));
                        }
                        Color last = Scale(list[0], factor);
                        brush.GradientStops.Add(new GradientStop(Color.FromArgb(alpha, last.R, last.G, last.B), 1.0));
                        return brush;
                    }
                case "shimmer":
                    {
                        // 一道加白的高光沿轴来回扫，带宽足够才看得见。
                        double band = 0.08 + 0.84 * (0.5 + 0.5 * Math.Sin(direction * phase * 2.4));
                        LinearGradientBrush brush = Axis(phase * 0.35, 0.65);
                        List<GradientStop> stops = new List<GradientStop>();
                        for (int i = 0; i < list.Count; i++)
                        {
                            stops.Add(new GradientStop(list[i], i / (double)list.Count));
                        }
                        stops.Add(new GradientStop(list[0], 1.0));
                        stops.Add(new GradientStop(Mix(list[0], Colors.White, 0.6), Math.Max(0.02, band - 0.24)));
                        stops.Add(new GradientStop(Mix(list[0], Colors.White, 0.92), band));
                        stops.Add(new GradientStop(Mix(list[0], Colors.White, 0.6), Math.Min(0.98, band + 0.24)));
                        stops.Sort(delegate(GradientStop x, GradientStop y) { return x.Offset.CompareTo(y.Offset); });
                        foreach (GradientStop stop in stops) brush.GradientStops.Add(stop);
                        return brush;
                    }
                case "shift":
                    {
                        // 色相持续平移（约 10 秒转一圈），不会停在原地。
                        double degrees = phase * 90 * depth * direction;
                        LinearGradientBrush brush = Axis(phase * 0.25, 0.6);
                        for (int i = 0; i < list.Count; i++)
                        {
                            brush.GradientStops.Add(new GradientStop(RotateHue(list[i], degrees), i / (double)list.Count));
                        }
                        brush.GradientStops.Add(new GradientStop(RotateHue(list[0], degrees), 1.0));
                        return brush;
                    }
                default:
                    return new SolidColorBrush(colorA);
            }
        }

        private static LinearGradientBrush Axis(double angle, double radius)
        {
            LinearGradientBrush brush = new LinearGradientBrush();
            brush.StartPoint = new Point(0.5 - radius * Math.Cos(angle), 0.5 - radius * Math.Sin(angle));
            brush.EndPoint = new Point(0.5 + radius * Math.Cos(angle), 0.5 + radius * Math.Sin(angle));
            return brush;
        }

        public static Color Mix(Color a, Color b, double t)
        {
            if (t < 0) t = 0;
            if (t > 1) t = 1;
            return Color.FromRgb(
                (byte)Math.Round(a.R + (b.R - a.R) * t),
                (byte)Math.Round(a.G + (b.G - a.G) * t),
                (byte)Math.Round(a.B + (b.B - a.B) * t));
        }

        public static Color Scale(Color color, double factor)
        {
            return Color.FromRgb(
                (byte)Math.Min(255, Math.Max(0, color.R * factor)),
                (byte)Math.Min(255, Math.Max(0, color.G * factor)),
                (byte)Math.Min(255, Math.Max(0, color.B * factor)));
        }

        public static Color RotateHue(Color color, double degrees)
        {
            double hue, saturation, value;
            ToHsv(color, out hue, out saturation, out value);
            return FromHsv(hue + degrees, saturation, value);
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
            return Color.FromRgb((byte)(r * 235), (byte)(g * 235), (byte)(b * 255));
        }

        private static void ToHsv(Color color, out double hue, out double saturation, out double value)
        {
            double r = color.R / 255.0, g = color.G / 255.0, b = color.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double d = max - min;
            value = max;
            saturation = max <= 0 ? 0 : d / max;
            if (d < 0.0001) { hue = 210; return; }
            if (max == r) hue = 60 * (((g - b) / d) % 6);
            else if (max == g) hue = 60 * ((b - r) / d + 2);
            else hue = 60 * ((r - g) / d + 4);
            if (hue < 0) hue += 360;
        }

        private static Color FromHsv(double hue, double saturation, double value)
        {
            double c = value * saturation;
            double h = (hue % 360 + 360) % 360 / 60.0;
            double x = c * (1 - Math.Abs(h % 2 - 1));
            double m = value - c;
            double r = 0, g = 0, b = 0;
            if (h < 1) { r = c; g = x; }
            else if (h < 2) { r = x; g = c; }
            else if (h < 3) { g = c; b = x; }
            else if (h < 4) { g = x; b = c; }
            else if (h < 5) { r = x; b = c; }
            else { r = c; b = x; }
            return Color.FromRgb(
                (byte)Math.Round((r + m) * 255),
                (byte)Math.Round((g + m) * 255),
                (byte)Math.Round((b + m) * 255));
        }
    }
}
