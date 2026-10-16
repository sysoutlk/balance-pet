// The speech bubble: a small card with a pointer tail towards the pet.
//
// Layout:  accent dot + title  /  peak badge  /  big number  /  detail rows  /  7-day chart
//          /  quip box  /  source line + page dots
//
// Look is configurable: bubble.style (light|dark), bubble.accent (#RRGGBB),
// bubble.trend (mini chart on/off) and bubble.tail (pointer on/off).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace DshPet
{
    internal sealed class BubbleView
    {
        public string Title = "";
        public string Big = "";
        public double BigSize = 26;
        public string BigColor = "";
        public string Sub = "";
        public List<KeyValuePair<string, string>> Rows = new List<KeyValuePair<string, string>>();
        public string PeakText = "";
        public bool PeakNow;
        public bool ShowTrend;
        public List<DayUsage> Trend = new List<DayUsage>();
        public string Quip = "";
        public string Note = "";
        public bool NoteError;
        public string Source = "";
        public int PageIndex = -1;
        public int PageCount;
    }

    internal sealed class BubbleWindow : Window
    {
        // Light and dark palettes.
        private const string LightCard = "#F7FFFFFF";
        private const string LightBorder = "#1E2C3A6E";
        private const string LightTitle = "#2A3C74";
        private const string LightBig = "#1D2F66";
        private const string LightMuted = "#8A94B2";
        private const string LightRowName = "#7C87A8";
        private const string LightRowValue = "#26346B";
        private const string LightQuipBg = "#F1F4FF";
        private const string LightQuipText = "#33478F";
        private const string LightSource = "#A9B3CC";
        private const string LightBar = "#D3DBF0";
        private const string LightBarEmpty = "#E9EDF8";

        private const string DarkCard = "#F2181E2E";
        private const string DarkBorder = "#33FFFFFF";
        private const string DarkTitle = "#E7EDFF";
        private const string DarkBig = "#FFFFFF";
        private const string DarkMuted = "#93A0C0";
        private const string DarkRowName = "#93A0C0";
        private const string DarkRowValue = "#E2E9FC";
        private const string DarkQuipBg = "#242C46";
        private const string DarkQuipText = "#C4D0F2";
        private const string DarkSource = "#7C86A2";
        private const string DarkBar = "#3C4666";
        private const string DarkBarEmpty = "#2A3247";

        private readonly Grid root;
        private readonly Grid tailTop;
        private readonly Grid tailBottom;
        private readonly Border tailTopShape;
        private readonly Border tailBottomShape;
        private readonly Border card;
        private readonly Ellipse titleDot;
        private readonly TextBlock title;
        private readonly Border badge;
        private readonly Ellipse badgeDot;
        private readonly TextBlock badgeText;
        private readonly TextBlock amount;
        private readonly TextBlock sub;
        private readonly StackPanel rows;
        private readonly StackPanel trend;
        private readonly List<Border> bars = new List<Border>();
        private readonly Border quipBox;
        private readonly TextBlock quip;
        private readonly TextBlock note;
        private readonly TextBlock source;
        private readonly StackPanel dots;

        private bool dark;
        private ThemePalette palette = BubbleTheme.Resolve("light");
        private Color accent = (Color)ColorConverter.ConvertFromString("#4B6BFB");
        private Color baseAccent = (Color)ColorConverter.ConvertFromString("#4B6BFB");
        private Color secondColor = (Color)ColorConverter.ConvertFromString("#A855F7");
        private double speedFactor = 1.0;
        private double effectDepth = 1.0;
        private bool effectReverse;
        private double fontScale = 1.0;
        private readonly List<KeyValuePair<TextBlock, double>> scalable = new List<KeyValuePair<TextBlock, double>>();
        private StackPanel body;
        private DropShadowEffect shadow;
        private bool tracking = true;
        private string dynamicMode = "";
        private double phase;
        private BubbleView lastView;
        private readonly DispatcherTimer rainbowTimer = new DispatcherTimer();
        private bool showTrend = true;
        private bool showTail = true;

        public PetWindow Anchor;
        public DateTime HideAt = DateTime.MinValue;
        private double anchorX = double.MinValue;
        private double anchorY = double.MinValue;

        public BubbleWindow()
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = null;
            Topmost = true;
            ShowInTaskbar = false;
            ShowActivated = false;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.WidthAndHeight;
            FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI");

            titleDot = new Ellipse();
            titleDot.Width = 7;
            titleDot.Height = 7;
            titleDot.VerticalAlignment = VerticalAlignment.Center;
            titleDot.Margin = new Thickness(0, 0, 7, 0);
            title = MakeText(12.5, "#2A3C74", "");
            title.FontWeight = FontWeights.SemiBold;
            title.VerticalAlignment = VerticalAlignment.Center;
            StackPanel head = new StackPanel();
            head.Orientation = Orientation.Horizontal;
            head.Children.Add(titleDot);
            head.Children.Add(title);

            badgeDot = new Ellipse();
            badgeDot.Width = 6;
            badgeDot.Height = 6;
            badgeDot.VerticalAlignment = VerticalAlignment.Center;
            badgeDot.Margin = new Thickness(0, 0, 6, 0);
            badgeText = MakeText(10.5, "#1F8A4C", "");
            badgeText.FontWeight = FontWeights.SemiBold;
            badgeText.VerticalAlignment = VerticalAlignment.Center;
            StackPanel badgeContent = new StackPanel();
            badgeContent.Orientation = Orientation.Horizontal;
            badgeContent.Children.Add(badgeDot);
            badgeContent.Children.Add(badgeText);
            badge = new Border();
            badge.CornerRadius = new CornerRadius(9);
            badge.Padding = new Thickness(8, 3, 9, 3);
            badge.Margin = new Thickness(0, 8, 0, 0);
            badge.HorizontalAlignment = HorizontalAlignment.Left;
            badge.Child = badgeContent;

            amount = MakeText(26, "#1D2F66", "--");
            amount.FontWeight = FontWeights.Bold;
            amount.Margin = new Thickness(0, 9, 0, 0);
            TextOptions.SetTextFormattingMode(amount, TextFormattingMode.Display);
            sub = MakeText(11, "#8A94B2", "");
            sub.Margin = new Thickness(0, 3, 0, 0);
            sub.TextWrapping = TextWrapping.Wrap;
            sub.MaxWidth = 226;
            sub.Visibility = Visibility.Collapsed;

            rows = new StackPanel();
            rows.Margin = new Thickness(0, 6, 0, 0);

            trend = new StackPanel();
            trend.Orientation = Orientation.Horizontal;
            trend.Height = 32;
            trend.Margin = new Thickness(0, 9, 0, 0);
            trend.VerticalAlignment = VerticalAlignment.Bottom;
            for (int i = 0; i < 7; i++)
            {
                Border bar = new Border();
                bar.Width = 13;
                bar.Height = 3;
                bar.Margin = new Thickness(0, 0, 4, 0);
                bar.VerticalAlignment = VerticalAlignment.Bottom;
                bar.CornerRadius = new CornerRadius(3);
                bars.Add(bar);
                trend.Children.Add(bar);
            }

            quip = MakeText(11, "#33478F", "");
            quip.TextWrapping = TextWrapping.Wrap;
            quip.MaxWidth = 214;
            quipBox = new Border();
            quipBox.CornerRadius = new CornerRadius(8);
            quipBox.Padding = new Thickness(9, 6, 9, 6);
            quipBox.Margin = new Thickness(0, 9, 0, 0);
            quipBox.Child = quip;
            quipBox.Visibility = Visibility.Collapsed;

            note = MakeText(10, "#8A94B2", "");
            note.TextWrapping = TextWrapping.Wrap;
            note.MaxWidth = 226;
            note.Margin = new Thickness(0, 7, 0, 0);
            note.Visibility = Visibility.Collapsed;

            dockDots = new StackPanel();
            dockDots.Orientation = Orientation.Horizontal;
            dockDots.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(dockDots, Dock.Right);
            source = MakeText(9, "#A9B3CC", "");
            DockPanel footer = new DockPanel();
            footer.Margin = new Thickness(0, 8, 0, 0);
            footer.Children.Add(dockDots);
            footer.Children.Add(source);

            StackPanel panel = new StackPanel();
            panel.MinWidth = 196;
            panel.Children.Add(head);
            panel.Children.Add(badge);
            panel.Children.Add(amount);
            panel.Children.Add(sub);
            panel.Children.Add(rows);
            panel.Children.Add(trend);
            panel.Children.Add(quipBox);
            panel.Children.Add(note);
            panel.Children.Add(footer);

            card = new Border();
            card.CornerRadius = new CornerRadius(16);
            card.Padding = new Thickness(16, 13, 16, 12);
            shadow = new DropShadowEffect();
            shadow.BlurRadius = 22;
            shadow.ShadowDepth = 5;
            shadow.Opacity = 0.28;
            shadow.Color = Colors.Black;
            card.Effect = shadow;
            card.Child = panel;
            body = panel;

            tailTopShape = MakeTail();
            tailTop = new Grid();
            tailTop.Height = 7;
            tailTop.Children.Add(tailTopShape);
            tailTop.Visibility = Visibility.Collapsed;
            tailBottomShape = MakeTail();
            tailBottom = new Grid();
            tailBottom.Height = 7;
            tailBottom.Children.Add(tailBottomShape);

            root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(tailTop, 0);
            Grid.SetRow(card, 1);
            Grid.SetRow(tailBottom, 1);   // corrected in Position(): tail sits under or above the card
            root.Children.Add(tailTop);
            root.Children.Add(card);
            root.Children.Add(tailBottom);
            Content = root;
            MouseLeftButtonDown += delegate { HideNow(); };
            rainbowTimer.Interval = TimeSpan.FromMilliseconds(110);
            rainbowTimer.Tick += delegate
            {
                if (dynamicMode.Length == 0 || Visibility != Visibility.Visible) return;
                phase += 0.045 * speedFactor;
                ApplyAccentBrushes();
            };
            rainbowTimer.Start();
        }

        private readonly StackPanel dockDots;

        private static Border MakeTail()
        {
            Border shape = new Border();
            shape.Width = 13;
            shape.Height = 13;
            shape.RenderTransform = new RotateTransform(45);
            shape.HorizontalAlignment = HorizontalAlignment.Center;
            shape.VerticalAlignment = VerticalAlignment.Center;
            shape.Margin = new Thickness(0, 0, 0, 0);
            return shape;
        }

        private TextBlock MakeText(double size, string colour, string text)
        {
            TextBlock block = new TextBlock();
            block.FontSize = size;
            block.Foreground = Brush(colour);
            block.Text = text;
            if (tracking) scalable.Add(new KeyValuePair<TextBlock, double>(block, size));
            return block;
        }

        private static SolidColorBrush Brush(string hex)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }

        private static SolidColorBrush FromColor(Color color)
        {
            return new SolidColorBrush(color);
        }

        private static Color HueColor(double degrees)
        {
            double h = (degrees % 360 + 360) % 360 / 60.0;
            double c = 1.0;
            double x = c * (1 - Math.Abs(h % 2 - 1));
            double r = 0, g = 0, b = 0;
            if (h < 1) { r = c; g = x; }
            else if (h < 2) { r = x; g = c; }
            else if (h < 3) { g = c; b = x; }
            else if (h < 4) { g = x; b = c; }
            else if (h < 5) { r = x; b = c; }
            else { r = c; b = x; }
            return Color.FromRgb((byte)Math.Round(r * 235), (byte)Math.Round(g * 235), (byte)Math.Round(b * 255));
        }

        private void ApplyAccentBrushes()
        {
            titleDot.Fill = CurrentBrush();
            foreach (object child in dockDots.Children)
            {
                Border dot = child as Border;
                if (dot == null) continue;
                if (dot.Width > 4.5 && Equals(dot.Tag, "active")) dot.Background = FromColor(accent);
            }
            if (lastView != null && showTrend && lastView.ShowTrend) UpdateTrend(lastView.Trend);
        }

        private static readonly string[] DynamicModes = new string[]
        {
            "rainbow", "aurora", "breathe", "duo", "flow", "orbit", "shimmer"
        };

        private Color DynamicColor()
        {
            switch (dynamicMode)
            {
                case "rainbow":
                    return HueColor(phase * 260);
                case "aurora":
                    {
                        Color c = HueColor(Hue(baseAccent) + 60 * effectDepth * Math.Sin(Direction() * phase * 0.9));
                        double k = 0.82 + 0.18 * effectDepth * Math.Sin(Direction() * phase * 1.7);
                        return Color.FromRgb((byte)(c.R * k), (byte)(c.G * k), (byte)(c.B * k));
                    }
                case "breathe":
                    {
                        double k = 1 - effectDepth * (0.5 + 0.5 * Math.Sin(Direction() * phase * 1.4));
                        return Color.FromRgb((byte)(baseAccent.R * k), (byte)(baseAccent.G * k), (byte)(baseAccent.B * k));
                    }
                case "duo":
                    return Blend(baseAccent, secondColor, PingPong(1.2));
                case "flow":
                    return Blend(baseAccent, secondColor, (phase * 0.5) % 1.0);
                case "orbit":
                    {
                        double span = 180 * effectDepth;
                        return HueColor(Hue(baseAccent) + span * Math.Sin(Direction() * phase * 1.1));
                    }
                case "shimmer":
                    {
                        double k = 1 - effectDepth * (0.45 + 0.45 * Math.Sin(Direction() * phase * 2.6));
                        return Color.FromRgb(
                            (byte)Math.Min(255, baseAccent.R + (255 - baseAccent.R) * (1 - k) * 0.55),
                            (byte)Math.Min(255, baseAccent.G + (255 - baseAccent.G) * (1 - k) * 0.55),
                            (byte)Math.Min(255, baseAccent.B + (255 - baseAccent.B) * (1 - k) * 0.55));
                    }
                default:
                    return accent;
            }
        }

        /// <summary>0..1 ping-pong wave; depth scales how far it swings, reverse flips the phase.</summary>
        private double PingPong(double rate)
        {
            double wave = 0.5 + 0.5 * Math.Sin(Direction() * phase * rate);
            return 0.5 + (wave - 0.5) * (0.35 + 0.65 * effectDepth);
        }

        private double Direction()
        {
            return effectReverse ? -1 : 1;
        }

        /// <summary>Solid colour for most modes; "flow" uses a sliding gradient.</summary>
        private Brush CurrentBrush()
        {
            if (dynamicMode.Length == 0) return FromColor(accent);
            if (dynamicMode != "flow") return FromColor(DynamicColor());
            Color a = DynamicColor();
            Color b = secondColor;
            LinearGradientBrush brush = new LinearGradientBrush();
            double t = (phase * 0.35) % 1.0;
            brush.StartPoint = new Point(t, 0);
            brush.EndPoint = new Point(t + 1, 1);
            brush.GradientStops.Add(new GradientStop(a, 0));
            brush.GradientStops.Add(new GradientStop(b, 0.5));
            brush.GradientStops.Add(new GradientStop(a, 1));
            return brush;
        }

        private static double Hue(Color color)
        {
            double r = color.R / 255.0, g = color.G / 255.0, b = color.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double d = max - min;
            if (d < 0.0001) return 210;
            double h;
            if (max == r) h = 60 * (((g - b) / d) % 6);
            else if (max == g) h = 60 * ((b - r) / d + 2);
            else h = 60 * ((r - g) / d + 4);
            return (h % 360 + 360) % 360;
        }

        private static Color Blend(Color a, Color b, double t)
        {
            if (t < 0) t = 0;
            if (t > 1) t = 1;
            return Color.FromRgb(
                (byte)Math.Round(a.R + (b.R - a.R) * t),
                (byte)Math.Round(a.G + (b.G - a.G) * t),
                (byte)Math.Round(a.B + (b.B - a.B) * t));
        }

        private static Color ParseColor(string value, string fallbackHex)
        {
            try
            {
                if (!string.IsNullOrEmpty(value) && value.StartsWith("#", StringComparison.Ordinal))
                {
                    return (Color)ColorConverter.ConvertFromString(value);
                }
            }
            catch
            {
            }
            try
            {
                if (!string.IsNullOrEmpty(fallbackHex) && fallbackHex.StartsWith("#", StringComparison.Ordinal))
                {
                    return (Color)ColorConverter.ConvertFromString(fallbackHex);
                }
            }
            catch
            {
            }
            return (Color)ColorConverter.ConvertFromString("#4B6BFB");
        }

        /// <summary>Reads the look settings from pet-config.json (called on every refresh).</summary>
        public void ApplyStyle(Config cfg)
        {
            if (cfg == null) return;
            string theme = cfg.GetString("bubble", "theme", "");
            if (string.IsNullOrEmpty(theme)) theme = cfg.GetString("bubble", "style", "light");
            palette = BubbleTheme.Resolve(theme);
            dark = palette.Dark;
            string accentText = cfg.GetString("bubble", "accent", "#4B6BFB");
            // Effect + colour pair. bubble.accent stays supported as the "primary colour" alias
            // (and older values like "rainbow"/"炫彩" still pick the matching effect).
            string effect = cfg.GetString("bubble", "effect", "off");
            if (string.IsNullOrEmpty(effect) || effect == "off")
            {
                effect = "";
                foreach (string mode in DynamicModes)
                {
                    if (string.Equals(accentText, mode, StringComparison.OrdinalIgnoreCase)) effect = mode;
                }
                if (effect.Length == 0 && accentText == "炫彩") effect = "rainbow";
                if (effect == "aurora") effect = "duo";
            }
            dynamicMode = effect;
            baseAccent = ParseColor(cfg.GetString("bubble", "color1", ""), accentText);
            secondColor = ParseColor(cfg.GetString("bubble", "color2", "#A855F7"), "#A855F7");
            accent = baseAccent;
            // Monet (and any theme that brings its own accent) tints the widget unless the user
            // picked colours explicitly.
            if (theme == "monet" && baseAccent == (Color)ColorConverter.ConvertFromString("#4B6BFB"))
            {
                baseAccent = palette.Accent;
                accent = palette.Accent;
            }
            speedFactor = cfg.GetNumber("bubble", "speed", 1.0);
            if (speedFactor < 0.1) speedFactor = 0.1;
            if (speedFactor > 5) speedFactor = 5;
            effectDepth = cfg.GetNumber("bubble", "effectDepth", 1.0);
            if (effectDepth < 0) effectDepth = 0;
            if (effectDepth > 1) effectDepth = 1;
            effectReverse = cfg.GetFlag("bubble", "effectReverse", false);
            fontScale = cfg.GetNumber("bubble", "fontScale", 1.0);
            if (fontScale < 0.6) fontScale = 0.6;
            if (fontScale > 2.0) fontScale = 2.0;
            if (dynamicMode.Length == 0) phase = 0;
            showTrend = cfg.GetFlag("bubble", "trend", true);
            showTail = cfg.GetFlag("bubble", "tail", true);

            card.Background = new SolidColorBrush(palette.Card);
            card.BorderBrush = new SolidColorBrush(palette.Border);
            string cardColor = cfg.GetString("bubble", "cardColor", "");
            if (!string.IsNullOrEmpty(cardColor) && cardColor.StartsWith("#", StringComparison.Ordinal))
            {
                try { card.Background = Brush(cardColor); }
                catch { }
            }
            double borderWidth = cfg.GetNumber("bubble", "borderWidth", 1);
            if (borderWidth < 0) borderWidth = 0;
            card.BorderThickness = new Thickness(borderWidth);
            double radius = cfg.GetNumber("bubble", "radius", 16);
            card.CornerRadius = new CornerRadius(Math.Max(0, radius));
            double tailSize = cfg.GetNumber("bubble", "tailSize", 13);
            if (tailSize < 6) tailSize = 6;
            if (tailSize > 26) tailSize = 26;
            tailTopShape.Width = tailSize;
            tailTopShape.Height = tailSize;
            tailBottomShape.Width = tailSize;
            tailBottomShape.Height = tailSize;
            tailTop.Height = tailSize * 0.55;
            tailBottom.Height = tailSize * 0.55;
            double boxWidth = cfg.GetNumber("bubble", "width", 196);
            if (boxWidth < 140) boxWidth = 140;
            body.MinWidth = boxWidth;
            Opacity = cfg.GetNumber("bubble", "opacity", 1.0);
            shadow.Opacity = cfg.GetNumber("bubble", "shadow", palette.ShadowOpacity);
            foreach (KeyValuePair<TextBlock, double> pair in scalable)
            {
                pair.Key.FontSize = Math.Round(pair.Value * fontScale, 1);
            }
            tailTopShape.Background = card.Background;
            tailTopShape.BorderBrush = card.BorderBrush;
            tailTopShape.BorderThickness = card.BorderThickness;
            tailBottomShape.Background = card.Background;
            tailBottomShape.BorderBrush = card.BorderBrush;
            tailBottomShape.BorderThickness = card.BorderThickness;
            titleDot.Fill = CurrentBrush();
            title.Foreground = new SolidColorBrush(palette.Title);
            amount.Foreground = new SolidColorBrush(palette.Big);
            sub.Foreground = new SolidColorBrush(palette.Muted);
            quipBox.Background = new SolidColorBrush(palette.QuipBackground);
            quip.Foreground = new SolidColorBrush(palette.QuipText);
            source.Foreground = new SolidColorBrush(palette.Source);
            foreach (Border bar in bars)
            {
                if (bar.Tag == null) bar.Background = new SolidColorBrush(palette.BarEmpty);
            }
            ApplyAccentBrushes();
        }

        public void UpdateFrom(BubbleView view)
        {
            lastView = view;
            title.Text = view.Title;
            amount.Text = view.Big;
            amount.FontSize = view.BigSize;
            amount.Foreground = string.IsNullOrEmpty(view.BigColor)
                ? Brush(dark ? DarkBig : LightBig)
                : Brush(view.BigColor);
            if (!string.IsNullOrEmpty(view.Sub))
            {
                sub.Text = view.Sub;
                sub.Visibility = Visibility.Visible;
            }
            else
            {
                sub.Visibility = Visibility.Collapsed;
            }

            rows.Children.Clear();
            foreach (KeyValuePair<string, string> row in view.Rows)
            {
                Grid line = new Grid();
                line.Margin = new Thickness(0, 3, 0, 0);
                line.ColumnDefinitions.Add(new ColumnDefinition());
                line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                TextBlock name = MakeText(10.5, "#000000", row.Key);
                name.Foreground = new SolidColorBrush(palette.Muted);
                name.VerticalAlignment = VerticalAlignment.Center;
                name.TextTrimming = TextTrimming.CharacterEllipsis;
                Grid.SetColumn(name, 0);
                TextBlock value = MakeText(11.5, "#000000", row.Value);
                value.Foreground = new SolidColorBrush(palette.RowValue);
                value.FontWeight = FontWeights.SemiBold;
                value.TextAlignment = TextAlignment.Right;
                value.Margin = new Thickness(10, 0, 0, 0);
                Grid.SetColumn(value, 1);
                line.Children.Add(name);
                line.Children.Add(value);
                rows.Children.Add(line);
            }
            rows.Visibility = view.Rows.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

            RefreshPeak(view.PeakText, view.PeakNow);
            trend.Visibility = (showTrend && view.ShowTrend) ? Visibility.Visible : Visibility.Collapsed;
            if (showTrend && view.ShowTrend) UpdateTrend(view.Trend);

            if (!string.IsNullOrEmpty(view.Quip))
            {
                quip.Text = view.Quip;
                quipBox.Visibility = Visibility.Visible;
            }
            else
            {
                quipBox.Visibility = Visibility.Collapsed;
            }
            if (!string.IsNullOrEmpty(view.Note))
            {
                note.Text = view.Note;
                note.Visibility = Visibility.Visible;
                note.Foreground = Brush(view.NoteError ? "#E0433F" : (dark ? DarkMuted : LightMuted));
            }
            else
            {
                note.Visibility = Visibility.Collapsed;
            }
            source.Text = view.Source;
            UpdateDots(view.PageIndex, view.PageCount);
        }

        private void UpdateDots(int index, int count)
        {
            dockDots.Children.Clear();
            if (index < 0 || count <= 1) return;
            for (int i = 0; i < count; i++)
            {
                Border dot = new Border();
                dot.Width = 5;
                dot.Height = 5;
                dot.CornerRadius = new CornerRadius(3);
                dot.Margin = new Thickness(3, 0, 0, 0);
                dot.VerticalAlignment = VerticalAlignment.Center;
                dot.Tag = i == index ? "active" : null;
                dot.Background = i == index ? FromColor(accent) : Brush(dark ? "#4A5570" : "#D3DBF0");
                dockDots.Children.Add(dot);
            }
        }

        public void RefreshPeak(string peakText, bool peakNow)
        {
            if (string.IsNullOrEmpty(peakText))
            {
                badge.Visibility = Visibility.Collapsed;
                return;
            }
            badge.Visibility = Visibility.Visible;
            badgeText.Text = peakText;
            if (dark)
            {
                badge.Background = Brush(peakNow ? "#3A2022" : "#16301F");
                badgeText.Foreground = Brush(peakNow ? "#FF9C93" : "#7ADFA0");
                badgeDot.Fill = Brush(peakNow ? "#FF6B5E" : "#39C46F");
            }
            else
            {
                badge.Background = Brush(peakNow ? "#FDECEA" : "#E9F8EE");
                badgeText.Foreground = Brush(peakNow ? "#C93A34" : "#1F8A4C");
                badgeDot.Fill = Brush(peakNow ? "#E0433F" : "#2FA24C");
            }
        }

        private void UpdateTrend(List<DayUsage> history)
        {
            double max = 0;
            foreach (DayUsage item in history)
            {
                if (item.Usage > max) max = item.Usage;
            }
            int count = bars.Count;
            for (int i = 0; i < count; i++)
            {
                int index = history.Count - count + i;
                Border bar = bars[i];
                bool empty = index < 0 || index >= history.Count;
                double usage = empty ? 0 : history[index].Usage;
                bar.Tag = empty ? null : (object)usage;
                bar.Height = max > 0 && !empty ? Math.Max(3, Math.Round(26 * usage / max)) : 3;
                bool today = !empty && index == history.Count - 1;
                if (today) bar.Background = FromColor(accent);
                else if (usage > 0) bar.Background = new SolidColorBrush(palette.Bar);
                else bar.Background = new SolidColorBrush(palette.BarEmpty);
            }
        }

        public void ShowFor(PetWindow pet, BubbleView view)
        {
            Anchor = pet;
            UpdateFrom(view);
            Visibility = Visibility.Visible;
            UpdateLayout();
            Position(true);
        }

        public void HideNow()
        {
            HideAt = DateTime.MinValue;
            Visibility = Visibility.Hidden;
        }

        public void Position(bool force)
        {
            if (Visibility != Visibility.Visible || Anchor == null) return;
            if (!force)
            {
                double moved = Math.Abs(Anchor.X - anchorX) + Math.Abs(Anchor.Y - anchorY);
                if (moved < 2) return;
            }
            anchorX = Anchor.X;
            anchorY = Anchor.Y;
            ScreenArea area = ScreenArea.ForPoint(Anchor.X + Anchor.Width / 2, Anchor.Y + Anchor.Height / 2);
            double width = ActualWidth;
            double height = ActualHeight;
            double x = Anchor.X + Anchor.Width / 2 - width / 2;
            x = Math.Max(area.Left + 4, Math.Min(x, area.Right - width - 4));
            double y = Anchor.Y - height - 6;
            bool above = true;
            if (y < area.Top + 4)
            {
                y = Anchor.Y + Anchor.Height + 6;
                above = false;
            }
            if (y + height > area.Bottom - 4) y = Math.Max(area.Top + 4, area.Bottom - height - 4);

            bool tail = showTail;
            tailTop.Visibility = tail && !above ? Visibility.Visible : Visibility.Collapsed;
            tailBottom.Visibility = tail && above ? Visibility.Visible : Visibility.Collapsed;
            Grid.SetRow(tailTop, above ? 2 : 0);
            Grid.SetRow(card, 1);
            Grid.SetRow(tailBottom, above ? 2 : 0);
            Left = Math.Round(x, 1);
            Top = Math.Round(y, 1);
        }
    }
}
