// Shared bubble design tokens and frame measurements.
// The live bubble and the settings preview both consume these values so a fix in one
// cannot leave the other with different contrast, opacity or safe-area rules.
using System;
using System.Windows.Media;

namespace BalancePet
{
    internal sealed class BubbleTokens
    {
        public Color Surface;
        public Color Title;
        public Color Primary;
        public Color Secondary;
        public Color Divider;
        public Color Track;
        public Color Accent;
        public Color Error;
        public Color Warning;
        public Color Success;
        public bool Dark;

        public static BubbleTokens Resolve(ThemePalette palette, Color accent)
        {
            BubbleTokens tokens = new BubbleTokens();
            tokens.Dark = palette.Dark;
            tokens.Surface = palette.PreserveAlpha ? palette.Card : Opaque(palette.Card);
            tokens.Title = palette.Title;
            tokens.Primary = palette.Big;
            tokens.Secondary = palette.Dark
                ? BubbleEffects.Mix(palette.Muted, Colors.White, 0.18)
                : BubbleEffects.Mix(palette.Muted, Colors.Black, 0.28);
            tokens.Divider = WithAlpha(palette.Border, palette.Dark ? (byte)105 : (byte)75);
            tokens.Track = palette.BarEmpty;
            tokens.Accent = accent;
            tokens.Error = Semantic("#D9515A", palette.Dark);
            tokens.Warning = Semantic("#D88214", palette.Dark);
            tokens.Success = Semantic("#2FA24C", palette.Dark);
            return tokens;
        }

        public static Color Opaque(Color color)
        {
            return color.A == 255 ? color : Color.FromRgb(color.R, color.G, color.B);
        }

        public static Color Semantic(string hex, bool dark)
        {
            Color color = (Color)ColorConverter.ConvertFromString(hex);
            return dark ? BubbleEffects.Mix(color, Colors.White, 0.24) : color;
        }

        public static Color WithAlpha(Color color, byte alpha)
        {
            return Color.FromArgb(alpha, color.R, color.G, color.B);
        }
    }

    internal sealed class BubbleFrameMetrics
    {
        public double CardWidth;
        public double CardHeight;
        public double ContentWidth;
        public double ContentHeight;
        public double RightSafe;
        public double BottomSafe;

        public static BubbleFrameMetrics Resolve(BubbleLayoutSpec layout, double radius,
            bool minimal)
        {
            BubbleFrameMetrics metrics = new BubbleFrameMetrics();
            metrics.CardWidth = layout.Width;
            metrics.CardHeight = layout.Height;
            // The card padding is already symmetric. Keep only a tiny guard for
            // pixel rounding; a large global safe area squeezes real content.
            metrics.RightSafe = 2;
            metrics.BottomSafe = 3;
            metrics.ContentWidth = Math.Max(1, layout.ContentWidth - metrics.RightSafe);
            metrics.ContentHeight = Math.Max(1, layout.ContentHeight - metrics.BottomSafe);
            return metrics;
        }

        public double HeaderTitleWidth(bool hasBadge)
        {
            double iconBudget = 31;       // 24px icon + 7px gap
            double badgeBudget = hasBadge ? 81 : 0;
            return Math.Max(56, ContentWidth - iconBudget - badgeBudget);
        }

        public double FooterSourceWidth
        {
            get { return Math.Max(64, ContentWidth - 41); }
        }
    }
}
