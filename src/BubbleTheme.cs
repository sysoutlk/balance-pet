// Bubble themes. "system" follows the Windows app theme, "glass"/"frost" are translucent
// glassmorphism looks (a real backdrop blur is impossible while the window uses WPF
// transparency, so these simulate it with alpha, highlights and a softer shadow) and
// "monet" samples the desktop wallpaper and tint the whole card with its dominant colour.
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace DshPet
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
    }

    internal static class BubbleTheme
    {
        public static ThemePalette Resolve(string theme)
        {
            if (string.Equals(theme, "system", StringComparison.OrdinalIgnoreCase))
            {
                theme = SystemPrefersDark() ? "dark" : "light";
            }
            if (string.Equals(theme, "glass", StringComparison.OrdinalIgnoreCase)) return Glass();
            if (string.Equals(theme, "frost", StringComparison.OrdinalIgnoreCase)) return Frost();
            if (string.Equals(theme, "monet", StringComparison.OrdinalIgnoreCase)) return Monet();
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

        /// <summary>Liquid glass: very translucent card with a bright rim and a soft shadow.</summary>
        private static ThemePalette Glass()
        {
            ThemePalette p = Light();
            p.Card = Alpha("#FFFFFF", 190);
            p.Border = Alpha("#FFFFFF", 150);
            p.QuipBackground = Alpha("#FFFFFF", 120);
            p.ShadowOpacity = 0.22;
            p.Bar = Alpha("#7C87A8", 130);
            p.BarEmpty = Alpha("#7C87A8", 55);
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
        private static ThemePalette Monet()
        {
            double lightness;
            Color baseColor = WallpaperColor(out lightness);
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
}
