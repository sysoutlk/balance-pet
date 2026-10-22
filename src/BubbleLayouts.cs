// Fixed bubble layouts and visual presets.
// A layout owns the card size; content density never changes the outer window.
using System;

namespace BalancePet
{
    internal sealed class BubbleLayoutSpec
    {
        public string Id;
        public string Label;
        public double Width;
        public double Height;
        public bool ShowChart;
        public bool ShowMetrics;
        public int MaxRows;
        public double PaddingLeft;
        public double PaddingTop;
        public double PaddingRight;
        public double PaddingBottom;

        public double ContentWidth
        {
            get { return Math.Max(120, Width - PaddingLeft - PaddingRight); }
        }

        public double ContentHeight
        {
            get { return Math.Max(54, Height - PaddingTop - PaddingBottom); }
        }
    }

    internal sealed class BubbleStylePreset
    {
        public string Id;
        public string Label;
        public string Layout;
        public string Theme;
        public string Effect;
        public double Radius;
    }

    internal static class BubbleLayouts
    {
        private static readonly BubbleLayoutSpec[] LayoutItems = new BubbleLayoutSpec[]
        {
            Make("minimal", "极简", 210, 156, false, false, 0),
            Make("compact", "紧凑", 228, 166, false, true, 4),
            Make("standard", "原版", 228, 220, true, true, 4),
            Make("dashboard", "数据面板", 248, 246, true, true, 6),
            Make("wide", "宽屏", 286, 188, true, true, 4)
        };

        private static readonly BubbleStylePreset[] PresetItems = new BubbleStylePreset[]
        {
            Preset("soft", "柔和卡片", "standard", "light", "off", 16),
            Preset("night", "夜间卡片", "compact", "dark", "off", 16),
            Preset("glass", "液态玻璃", "compact", "glass", "breathe", 20),
            Preset("data", "数据面板", "dashboard", "frost", "flow", 14),
            Preset("wide", "宽屏状态", "wide", "light", "shimmer", 14),
            Preset("minimal", "极简提示", "minimal", "light", "off", 12)
        };

        public static BubbleLayoutSpec[] Layouts()
        {
            return LayoutItems;
        }

        public static BubbleStylePreset[] Presets()
        {
            return PresetItems;
        }

        public static string[] LayoutValues()
        {
            string[] values = new string[LayoutItems.Length + 1];
            for (int i = 0; i < LayoutItems.Length; i++) values[i] = LayoutItems[i].Id;
            values[LayoutItems.Length] = "custom";
            return values;
        }

        public static string[] LayoutLabels()
        {
            string[] labels = new string[LayoutItems.Length + 1];
            for (int i = 0; i < LayoutItems.Length; i++) labels[i] = LayoutItems[i].Label;
            labels[LayoutItems.Length] = "自定义";
            return labels;
        }

        public static BubbleLayoutSpec Resolve(Config cfg)
        {
            string id = cfg == null ? "standard" : cfg.GetString("bubble", "layout", "standard");
            if (string.Equals(id, "custom", StringComparison.OrdinalIgnoreCase))
                return Custom(cfg);
            foreach (BubbleLayoutSpec item in LayoutItems)
            {
                if (string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase))
                    return item;
            }
            return LayoutItems[2];
        }

        /// <summary>
        /// The visual preset owns the base size, but typography still needs real room.
        /// Growing the frame only as text gets larger lets every layout keep its own
        /// composition instead of silently shrinking or clipping the selected font.
        /// </summary>
        public static BubbleLayoutSpec ResolveAdaptive(Config cfg)
        {
            BubbleLayoutSpec baseLayout = Resolve(cfg);
            BubbleLayoutSpec item = Clone(baseLayout);
            double scale = cfg == null ? 1.0 : cfg.GetNumber("bubble", "fontScale", 1.0);
            if (scale < 0.7) scale = 0.7;
            if (scale > 1.8) scale = 1.8;
            double extra = Math.Max(0, scale - 1.0);
            if (extra <= 0.0001) return item;

            double widthGrowth;
            double heightGrowth;
            switch (item.Id)
            {
                case "minimal":
                    widthGrowth = 22;
                    heightGrowth = 58;
                    break;
                case "compact":
                    widthGrowth = 20;
                    heightGrowth = 155;
                    break;
                case "dashboard":
                    widthGrowth = 30;
                    heightGrowth = 74;
                    break;
                case "wide":
                    widthGrowth = 40;
                    heightGrowth = 68;
                    break;
                case "custom":
                    widthGrowth = 18;
                    heightGrowth = 72;
                    break;
                default:
                    widthGrowth = 28;
                    heightGrowth = 82;
                    break;
            }
            item.Width = Math.Min(420, item.Width + extra * widthGrowth);
            item.Height = Math.Min(360, item.Height + extra * heightGrowth);
            return item;
        }

        public static BubbleStylePreset MatchingPreset(Config cfg)
        {
            if (cfg == null) return null;
            string layout = cfg.GetString("bubble", "layout", "standard");
            string theme = cfg.GetString("bubble", "theme", "");
            if (string.IsNullOrEmpty(theme)) theme = cfg.GetString("bubble", "style", "light");
            string effect = cfg.GetString("bubble", "effect", "off");
            double radius = cfg.GetNumber("bubble", "radius", 16);
            foreach (BubbleStylePreset item in PresetItems)
            {
                if (string.Equals(item.Layout, layout, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(item.Theme, theme, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(item.Effect, effect, StringComparison.OrdinalIgnoreCase)
                    && Math.Abs(item.Radius - radius) < 0.6)
                    return item;
            }
            return null;
        }

        private static BubbleLayoutSpec Custom(Config cfg)
        {
            BubbleLayoutSpec item = Make("custom", "自定义",
                cfg == null ? 228 : cfg.GetNumber("bubble", "customWidth", 228),
                cfg == null ? 220 : cfg.GetNumber("bubble", "customHeight", 220),
                cfg == null || cfg.GetFlag("bubble", "trend", true), true, 4);
            if (item.Width < 190) item.Width = 190;
            if (item.Width > 420) item.Width = 420;
            if (item.Height < 112) item.Height = 112;
            if (item.Height > 280) item.Height = 280;
            return item;
        }

        private static BubbleLayoutSpec Make(string id, string label, double width,
            double height, bool showChart, bool showMetrics, int maxRows)
        {
            BubbleLayoutSpec item = new BubbleLayoutSpec();
            item.Id = id;
            item.Label = label;
            item.Width = width;
            item.Height = height;
            item.ShowChart = showChart;
            item.ShowMetrics = showMetrics;
            item.MaxRows = maxRows;
            ApplyVariantPadding(item, id);
            return item;
        }

        private static void ApplyVariantPadding(BubbleLayoutSpec item, string variant)
        {
            bool minimal = string.Equals(variant, "minimal", StringComparison.OrdinalIgnoreCase);
            item.PaddingLeft = minimal ? 13 : 16;
            item.PaddingTop = minimal ? 10 : 13;
            item.PaddingRight = minimal ? 13 : 16;
            item.PaddingBottom = minimal ? 9 : 12;
        }

        private static BubbleLayoutSpec Clone(BubbleLayoutSpec source)
        {
            BubbleLayoutSpec item = new BubbleLayoutSpec();
            item.Id = source.Id;
            item.Label = source.Label;
            item.Width = source.Width;
            item.Height = source.Height;
            item.ShowChart = source.ShowChart;
            item.ShowMetrics = source.ShowMetrics;
            item.MaxRows = source.MaxRows;
            item.PaddingLeft = source.PaddingLeft;
            item.PaddingTop = source.PaddingTop;
            item.PaddingRight = source.PaddingRight;
            item.PaddingBottom = source.PaddingBottom;
            return item;
        }

        private static BubbleStylePreset Preset(string id, string label, string layout,
            string theme, string effect, double radius)
        {
            BubbleStylePreset item = new BubbleStylePreset();
            item.Id = id;
            item.Label = label;
            item.Layout = layout;
            item.Theme = theme;
            item.Effect = effect;
            item.Radius = radius;
            return item;
        }
    }
}
