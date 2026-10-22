// The speech bubble: a small card with a pointer tail towards the pet.
//
// Layout:  a shared header/footer frame with one page-specific content area. Balance, spend,
// trend, peak, provider, quota, message and alert pages each use their own information structure.
//
// Look is configurable: bubble.theme (light|dim|dark|system|glass|frost|monet),
// bubble.accent (#RRGGBB),
// bubble.trend (mini chart on/off) and bubble.tail (pointer on/off).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace BalancePet
{
    internal sealed class BubbleView
    {
        public string Mode = "data";
        public string Title = "";
        public string Big = "";
        public double BigSize = 26;
        public string BigColor = "";
        public string Sub = "";
        public string ResetText = "";
        public List<KeyValuePair<string, string>> Rows = new List<KeyValuePair<string, string>>();
        public string PeakText = "";
        public bool PeakNow;
        public bool ShowTrend;
        public List<DayUsage> Trend = new List<DayUsage>();
        public string TrendCurrency = "CNY";
        public bool TrendExact;
        public string Quip = "";
        public string Note = "";
        public bool NoteError;
        public double? AlertUsed;
        public double? AlertLimit;
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
        private readonly TailShape tailTopShape;
        private readonly TailShape tailBottomShape;
        private readonly Border card;
        private readonly Ellipse titleDot;
        private readonly ContentControl pageIconHost;
        private readonly TextBlock title;
        private readonly Border badge;
        private readonly Ellipse badgeDot;
        private readonly TextBlock badgeText;
        private readonly Viewbox amountBox;
        private readonly TextBlock amount;
        private readonly TextBlock sub;
        private readonly UniformGrid rows;
        private readonly Grid trend;
        private readonly Canvas trendCanvas;
        private readonly UniformGrid trendHits;
        private readonly TextBlock trendSummary;
        private string trendDefaultText = "";
        private readonly Border quipBox;
        private readonly TextBlock quip;
        private readonly TextBlock note;
        private readonly TextBlock source;
        private readonly TextBlock quipLine;
        private readonly Grid quipRow;
        private readonly Grid body;
        private readonly DockPanel header;
        private readonly ContentControl pageHost;
        private readonly Border footerRule;
        private readonly Grid contentGrid;
        private readonly StackPanel trendPanel;
        private readonly StackPanel textPanel;

        private bool dark;
        private ThemePalette palette = BubbleTheme.Resolve("light");
        private Color accent = (Color)ColorConverter.ConvertFromString("#4B6BFB");
        private BubbleTokens design = BubbleTokens.Resolve(
            BubbleTheme.Resolve("light"), (Color)ColorConverter.ConvertFromString("#4B6BFB"));
        private BubbleFrameMetrics frame = BubbleFrameMetrics.Resolve(
            BubbleLayouts.Resolve(null), 16, false);
        private double pageHeight = 120;
        private Color baseAccent = (Color)ColorConverter.ConvertFromString("#4B6BFB");
        private Color secondColor = (Color)ColorConverter.ConvertFromString("#A855F7");
        private readonly List<Color> blendColors = new List<Color>();
        private double speedFactor = 1.0;
        private double effectDepth = 1.0;
        private bool effectReverse;
        private double fontScale = 1.0;
        // 逐元素文字色覆盖：bubble.textColors.<元素>，留空则跟随主题。
        private readonly Dictionary<string, string> textOverrides = new Dictionary<string, string>();
        private readonly List<KeyValuePair<TextBlock, double>> scalable = new List<KeyValuePair<TextBlock, double>>();
        private readonly List<Action<Brush>> accentAppliers = new List<Action<Brush>>();
        private DropShadowEffect shadow;
        private Border shadowShape;          // 阴影本体：和卡片同形状，放在卡片下面
        private Grid shadowLayer;            // 裁剪层：把卡片形状挖掉，只让阴影露在卡片外面
        private const double ShadowPad = 56; // 阴影层四周留的余量（够 blur + 偏移用）
        // 窗口也要留同样一圈：否则阴影一超出窗口就被边界切掉，四角会留下硬边。
        private const double BubbleShadowPad = 56;
        private bool tracking = true;
        private string dynamicMode = "";
        private double phase;
        private BubbleView lastView;
        private readonly DispatcherTimer rainbowTimer = new DispatcherTimer();
        private int trendHoverIndex = -1;
        private Point trendHoverScreen;
        private bool trendHoverScreenValid;
        private bool showTrend = true;
        private bool showTail = true;
        private Config config;
        private BubbleLayoutSpec layout = BubbleLayouts.Resolve(null);
        private string viewMode = "data";
        private string layoutVariant = "standard";

        private double ContentWidth
        {
            get
            {
                return body != null && body.Width > 0 && !double.IsNaN(body.Width)
                    ? body.Width : frame.ContentWidth;
            }
        }

        private sealed class TailShape : Grid
        {
            private readonly Path fillPath;
            private readonly Path outlinePath;

            public TailShape(bool pointUp)
            {
                IsHitTestVisible = false;
                UseLayoutRounding = true;
                SnapsToDevicePixels = true;

                fillPath = new Path();
                fillPath.Data = Geometry.Parse("M 0,0 L 0.5,1 L 1,0 Z");
                fillPath.Stretch = Stretch.Fill;
                fillPath.IsHitTestVisible = false;

                outlinePath = new Path();
                outlinePath.Data = Geometry.Parse("M 0,0 L 0.5,1 L 1,0");
                outlinePath.Stretch = Stretch.Fill;
                outlinePath.StrokeLineJoin = PenLineJoin.Round;
                outlinePath.StrokeStartLineCap = PenLineCap.Flat;
                outlinePath.StrokeEndLineCap = PenLineCap.Flat;
                outlinePath.IsHitTestVisible = false;

                Children.Add(fillPath);
                Children.Add(outlinePath);
                if (pointUp)
                {
                    RenderTransformOrigin = new Point(0.5, 0.5);
                    RenderTransform = new ScaleTransform(1, -1);
                }
            }

            public Brush FillBrush
            {
                set { fillPath.Fill = value; }
            }

            public Brush StrokeBrush
            {
                set { outlinePath.Stroke = value; }
            }

            public double StrokeWidth
            {
                set { outlinePath.StrokeThickness = value; }
            }
        }

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
            SizeToContent = SizeToContent.Manual;
            FontFamily = SystemFonts.MessageFontFamily;

            titleDot = new Ellipse();
            titleDot.Width = 7;
            titleDot.Height = 7;
            titleDot.VerticalAlignment = VerticalAlignment.Center;
            titleDot.Margin = new Thickness(0, 0, 7, 0);
            title = MakeText(11.5, "#2A3C74", "");
            title.FontWeight = FontWeights.SemiBold;
            title.VerticalAlignment = VerticalAlignment.Center;
            title.MinWidth = 42;
            pageIconHost = new ContentControl();
            pageIconHost.Width = 24;
            pageIconHost.Height = 24;
            pageIconHost.Margin = new Thickness(0, 0, 7, 0);
            pageIconHost.VerticalAlignment = VerticalAlignment.Center;

            StackPanel head = new StackPanel();
            head.Orientation = Orientation.Horizontal;
            head.Children.Add(pageIconHost);
            head.Children.Add(title);

            badgeDot = new Ellipse();
            badgeDot.Width = 6;
            badgeDot.Height = 6;
            badgeDot.VerticalAlignment = VerticalAlignment.Center;
            badgeDot.Margin = new Thickness(0, 0, 6, 0);
            badgeText = MakeText(8.8, "#1F8A4C", "");
            badgeText.FontWeight = FontWeights.SemiBold;
            badgeText.VerticalAlignment = VerticalAlignment.Center;
            badgeText.TextTrimming = TextTrimming.CharacterEllipsis;
            badgeText.MaxWidth = 54;
            StackPanel badgeContent = new StackPanel();
            badgeContent.Orientation = Orientation.Horizontal;
            badgeContent.Children.Add(badgeDot);
            badgeContent.Children.Add(badgeText);
            badge = new Border();
            badge.CornerRadius = new CornerRadius(9);
            badge.Padding = new Thickness(5, 1, 6, 1);
            badge.Margin = new Thickness(0, 8, 0, 0);
            badge.HorizontalAlignment = HorizontalAlignment.Left;
            badge.MaxWidth = 74;
            badge.Child = badgeContent;

            amount = MakeText(26, "#1D2F66", "--");
            amount.FontWeight = FontWeights.Bold;
            amount.Margin = new Thickness(0, 9, 0, 0);
            TextOptions.SetTextFormattingMode(amount, TextFormattingMode.Display);
            sub = MakeText(11, "#8A94B2", "");
            sub.Margin = new Thickness(0, 3, 0, 0);
            sub.TextWrapping = TextWrapping.Wrap;
            sub.MaxWidth = 226;
            sub.MaxHeight = 24;
            sub.Visibility = Visibility.Collapsed;
            title.TextTrimming = TextTrimming.CharacterEllipsis;
            title.MaxWidth = 226;
            sub.TextTrimming = TextTrimming.CharacterEllipsis;

            rows = new UniformGrid();
            rows.Columns = 2;
            rows.Margin = new Thickness(0, 4, 0, 0);

            trendSummary = MakeText(9.5, "#A9B3CC", "");
            trendSummary.Margin = new Thickness(0, 9, 0, 0);
            trendSummary.MaxWidth = 228;
            trendSummary.MinHeight = 18;
            trendSummary.TextWrapping = TextWrapping.NoWrap;
            trendSummary.TextTrimming = TextTrimming.CharacterEllipsis;
            trendSummary.Cursor = System.Windows.Input.Cursors.Hand;
            trendSummary.ToolTip = "点击切换近 7 / 14 / 30 天统计";
            trendSummary.MouseLeftButtonDown += delegate(object sender, System.Windows.Input.MouseButtonEventArgs e)
            {
                CycleTrendRange();
                e.Handled = true;
            };

            trendCanvas = new Canvas();
            trendCanvas.Width = 196;
            trendCanvas.Height = 44;
            trendCanvas.IsHitTestVisible = false;

            trendHits = new UniformGrid();
            trendHits.Width = 196;
            trendHits.Height = 44;
            trendHits.Background = Brushes.Transparent;

            trend = new Grid();
            trend.Width = 196;
            trend.Height = 44;
            trend.Margin = new Thickness(0, 3, 0, 0);
            trend.VerticalAlignment = VerticalAlignment.Bottom;
            trend.Children.Add(trendCanvas);
            trend.Children.Add(trendHits);

            quip = MakeText(11, "#33478F", "");
            quip.TextWrapping = TextWrapping.Wrap;
            quip.TextTrimming = TextTrimming.WordEllipsis;
            quip.MaxWidth = 214;
            quipBox = new Border();
            quipBox.CornerRadius = new CornerRadius(8);
            quipBox.Padding = new Thickness(9, 6, 9, 6);
            quipBox.Margin = new Thickness(0, 5, 0, 0);
            quipBox.Child = quip;
            quipBox.Visibility = Visibility.Collapsed;

            note = MakeText(10, "#8A94B2", "");
            note.TextWrapping = TextWrapping.Wrap;
            note.TextTrimming = TextTrimming.WordEllipsis;
            note.MaxWidth = 226;
            note.Margin = new Thickness(0, 5, 0, 0);
            note.Visibility = Visibility.Collapsed;

            dockDots = new StackPanel();
            dockDots.Orientation = Orientation.Horizontal;
            dockDots.VerticalAlignment = VerticalAlignment.Center;
            dockDots.HorizontalAlignment = HorizontalAlignment.Right;
            source = MakeText(9, "#A9B3CC", "");
            source.TextTrimming = TextTrimming.CharacterEllipsis;
            source.VerticalAlignment = VerticalAlignment.Center;
            source.Margin = new Thickness(0, 0, 7, 0);
            quipLine = MakeText(8.8, "#33478F", "");
            quipLine.TextTrimming = TextTrimming.CharacterEllipsis;
            quipLine.VerticalAlignment = VerticalAlignment.Center;
            quipRow = new Grid();
            quipRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            quipRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            TextBlock quoteMark = new TextBlock();
            quoteMark.Text = "“";
            quoteMark.FontFamily = new FontFamily("Georgia");
            quoteMark.FontSize = 14;
            quoteMark.Margin = new Thickness(0, -2, 5, 0);
            quoteMark.VerticalAlignment = VerticalAlignment.Center;
            quoteMark.Foreground = CurrentBrush();
            quipRow.Children.Add(quoteMark);
            Grid.SetColumn(quipLine, 1);
            quipRow.Children.Add(quipLine);
            quipRow.Margin = new Thickness(0, 4, 0, 1);
            quipRow.Visibility = Visibility.Collapsed;
            Grid footer = new Grid();
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
            footer.Children.Add(source);
            Grid.SetColumn(dockDots, 1);
            footer.Children.Add(dockDots);
            footerRule = new Border();
            footerRule.BorderThickness = new Thickness(0, 1, 0, 0);
            footerRule.Padding = new Thickness(0, 5, 0, 1);
            footerRule.Margin = new Thickness(0, 0, 0, 1);
            footerRule.HorizontalAlignment = HorizontalAlignment.Stretch;
            footerRule.Child = footer;

            badge.Margin = new Thickness(0);
            badge.VerticalAlignment = VerticalAlignment.Center;
            header = new DockPanel();
            header.LastChildFill = true;
            header.Margin = new Thickness(0, 0, 0, 2);
            DockPanel.SetDock(badge, Dock.Right);
            header.Children.Add(badge);
            header.Children.Add(head);

            trendPanel = new StackPanel();
            trendPanel.Children.Add(trendSummary);
            trendPanel.Children.Add(trend);
            textPanel = new StackPanel();
            textPanel.Children.Add(quipBox);
            textPanel.Children.Add(note);
            contentGrid = new Grid();
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(rows, 0);
            Grid.SetRow(trendPanel, 1);
            Grid.SetRow(textPanel, 2);
            contentGrid.Children.Add(rows);
            contentGrid.Children.Add(trendPanel);
            contentGrid.Children.Add(textPanel);

            pageHost = new ContentControl();
            pageHost.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            pageHost.VerticalContentAlignment = VerticalAlignment.Stretch;
            pageHost.Margin = new Thickness(0, 5, 0, 4);

            Grid panel = new Grid();
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(header, 0);
            Grid.SetRow(pageHost, 1);
            Grid.SetRow(quipRow, 2);
            Grid.SetRow(footerRule, 3);
            panel.Children.Add(header);
            // 大数字放进 Viewbox：位数多时自动缩小字号，不再溢出/压到别的行。
            amountBox = new Viewbox();
            amountBox.Stretch = Stretch.Uniform;
            amountBox.StretchDirection = StretchDirection.DownOnly;
            amountBox.HorizontalAlignment = HorizontalAlignment.Left;
            amountBox.MaxWidth = 228;
            amountBox.Child = amount;
            amount.Margin = new Thickness(0, 6, 0, 0);
            panel.Children.Add(pageHost);
            panel.Children.Add(quipRow);
            panel.Children.Add(footerRule);

            card = new Border();
            card.CornerRadius = new CornerRadius(16);
            card.Padding = new Thickness(16, 13, 16, 12);
            card.ClipToBounds = true;
            shadow = new DropShadowEffect();
            shadow.BlurRadius = 22;
            shadow.ShadowDepth = 5;
            shadow.Opacity = 0.28;
            shadow.Color = Colors.Black;
            // 阴影不直接挂在卡片上：那样圆角一大，卡片四角的缺口里就会透出阴影色，看着割裂。
            // 改成单独一层，用「整块减掉卡片圆角矩形」的裁剪，阴影只出现在卡片轮廓之外。
            shadowShape = new Border();
            shadowShape.CornerRadius = new CornerRadius(16);
            shadowShape.Background = Brushes.Black;   // 只有轮廓参与投影，本体被裁掉看不见
            shadowShape.Margin = new Thickness(ShadowPad);
            shadowShape.Effect = shadow;
            shadowLayer = new Grid();
            shadowLayer.Margin = new Thickness(-ShadowPad);
            shadowLayer.IsHitTestVisible = false;
            shadowLayer.Children.Add(shadowShape);
            card.Child = panel;
            body = panel;
            card.SizeChanged += delegate { UpdateShadowClip(); };

            tailTopShape = MakeTail(true);
            tailTop = new Grid();
            tailTop.Height = 7;
            tailTop.Children.Add(tailTopShape);
            tailTop.Visibility = Visibility.Collapsed;
            tailBottomShape = MakeTail(false);
            tailBottom = new Grid();
            tailBottom.Height = 7;
            tailBottom.Children.Add(tailBottomShape);
            tailBottom.Visibility = Visibility.Collapsed;

            root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(tailTop, 0);
            Grid.SetRow(card, 1);
            Grid.SetRow(tailBottom, 2);
            Grid.SetRow(shadowLayer, 1);
            root.Children.Add(tailTop);
            root.Children.Add(shadowLayer);
            root.Children.Add(card);
            root.Children.Add(tailBottom);
            // 四周留出阴影余量：窗口紧贴卡片的话，阴影会被窗口边界裁成硬边。
            root.Margin = new Thickness(BubbleShadowPad);
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

        private static TailShape MakeTail(bool pointUp)
        {
            TailShape shape = new TailShape(pointUp);
            shape.Width = 13;
            shape.Height = 13;
            shape.HorizontalAlignment = HorizontalAlignment.Left;
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

        /// <summary>
        /// 阴影层裁剪：整块区域减去卡片的圆角矩形，只留卡片外面那一圈给阴影。
        /// 不这样做的话，圆角越大、卡片四角缺口越大，阴影色就会从缺口里透出来。
        /// </summary>
        private void UpdateShadowClip()
        {
            if (shadowLayer == null || card == null) return;
            double w = card.ActualWidth;
            double h = card.ActualHeight;
            if (w <= 0 || h <= 0)
            {
                shadowLayer.Clip = null;
                return;
            }
            double radius = card.CornerRadius.TopLeft;
            shadowShape.CornerRadius = new CornerRadius(radius);
            RectangleGeometry outer = new RectangleGeometry(new Rect(0, 0, w + ShadowPad * 2, h + ShadowPad * 2));
            RectangleGeometry inner = new RectangleGeometry(new Rect(ShadowPad, ShadowPad, w, h), radius, radius);
            shadowLayer.Clip = new CombinedGeometry(GeometryCombineMode.Exclude, outer, inner);
        }

        private void ApplyAccentBrushes()
        {
            Brush brush = CurrentBrush();
            titleDot.Fill = brush;
            // 动效只作用于标记、进度和轮廓；正文始终使用主题文字色，保证对比度。
            // 描边也跟动效走，尾巴随即同步（尾巴本身是卡片描边的复制品）。
            card.BorderBrush = new SolidColorBrush(WithAlpha(DynamicColor(), 150));
            tailTopShape.StrokeBrush = card.BorderBrush;
            tailBottomShape.StrokeBrush = card.BorderBrush;
            foreach (object child in dockDots.Children)
            {
                Border dot = child as Border;
                if (dot == null) continue;
                if (dot.Width > 4.5 && Equals(dot.Tag, "active")) dot.Background = FromColor(accent);
            }
            foreach (Action<Brush> apply in accentAppliers)
            {
                apply(brush);
            }
        }

        private static Color WithAlpha(Color color, byte alpha)
        {
            return Color.FromArgb(alpha, color.R, color.G, color.B);
        }

        /// <summary>取用户覆盖色，没有就跟随主题。</summary>
        private SolidColorBrush Override(string key, Color fallback)
        {
            string value;
            if (textOverrides.TryGetValue(key, out value))
            {
                try
                {
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
                }
                catch
                {
                }
            }
            return new SolidColorBrush(fallback);
        }

        // 动效是独立维度：颜色怎么混（bubble.colors）与动效怎么动互不绑定，可任意组合。
        private static readonly string[] DynamicModes = new string[]
        {
            "flow", "swirl", "breathe", "shimmer", "shift", "rainbow"
        };

        private Color DynamicColor()
        {
            switch (dynamicMode)
            {
                case "rainbow":
                    return HueColor(phase * 260);
                case "breathe":
                    {
                        double k = 1 - effectDepth * 0.5 * (0.5 + 0.5 * Math.Sin(Direction() * phase * 3.2));
                        return Modulate(BlendCenter(), k);
                    }
                case "shift":
                    return BubbleEffects.RotateHue(BlendCenter(), phase * 90 * effectDepth * Direction());
                case "shimmer":
                    {
                        double k = 0.15 + 0.75 * (0.5 + 0.5 * Math.Sin(Direction() * phase * 2.4)) * effectDepth;
                        return BubbleEffects.Mix(BlendCenter(), Colors.White, Math.Min(0.8, k));
                    }
                case "flow":
                case "swirl":
                    return BlendCenter();
                default:
                    return accent;
            }
        }

        private Color BlendCenter()
        {
            List<Color> colors = blendColors;
            if (colors.Count == 0) return baseAccent;
            if (colors.Count == 1) return colors[0];
            double t = (phase * 0.25) % 1.0;
            int index = (int)Math.Floor(t * colors.Count);
            double local = t * colors.Count - index;
            return Blend(colors[index % colors.Count], colors[(index + 1) % colors.Count], local);
        }

        private static Color Modulate(Color color, double factor)
        {
            return Color.FromRgb(
                (byte)Math.Min(255, Math.Max(0, color.R * factor)),
                (byte)Math.Min(255, Math.Max(0, color.G * factor)),
                (byte)Math.Min(255, Math.Max(0, color.B * factor)));
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

        /// <summary>当前动效的画刷：算法与设置里的实时预览共用（BubbleEffects）。</summary>
        private Brush CurrentBrush()
        {
            if (dynamicMode.Length == 0) return FromColor(accent);
            return BubbleEffects.Brush(dynamicMode, blendColors, baseAccent, secondColor, phase, effectDepth, effectReverse);
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

        /// <summary>Uses the requested family, or the system UI font when it is missing.</summary>
        private static FontFamily SafeFont(string name)
        {
            try
            {
                FontFamily family = new FontFamily(name);
                if (string.IsNullOrEmpty(family.Source)) return SystemFonts.MessageFontFamily;
                return family;
            }
            catch
            {
                return SystemFonts.MessageFontFamily;
            }
        }

        /// <summary>Reads the look settings from pet-config.json (called on every refresh).</summary>
        public void ApplyStyle(Config cfg)
        {
            if (cfg == null) return;
            config = cfg;
            string theme = cfg.GetString("bubble", "theme", "");
            if (string.IsNullOrEmpty(theme)) theme = cfg.GetString("bubble", "style", "light");
            palette = BubbleTheme.Resolve(theme, config);
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
            blendColors.Clear();
            // 只取最近 bubble.mixMax 个（默认 6）：超出上限的旧色自动让位，不给气泡留一长串颜色。
            foreach (string hex in BubbleTheme.MixColors(cfg))
            {
                blendColors.Add(ParseColor(hex, "#4B6BFB"));
            }
            if (blendColors.Count == 0)
            {
                // 没配 colors 时，用主/副色再派生两色，形成 4 色混搭。
                blendColors.Add(baseAccent);
                blendColors.Add(secondColor);
                blendColors.Add(HueColor(Hue(baseAccent) + 45));
                blendColors.Add(HueColor(Hue(secondColor) - 40));
            }
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
            layout = BubbleLayouts.ResolveAdaptive(cfg);
            showTrend = cfg.GetFlag("bubble", "trend", true) && layout.ShowChart;
            showTail = cfg.GetFlag("bubble", "tail", true);

            card.BorderBrush = new SolidColorBrush(palette.Border);
            footerRule.BorderBrush = new SolidColorBrush(WithAlpha(palette.Border,
                dark ? (byte)95 : (byte)70));
            double borderWidth = cfg.GetNumber("bubble", "borderWidth", 1);
            if (borderWidth < 0) borderWidth = 0;
            card.BorderThickness = new Thickness(borderWidth);
            double radius = cfg.GetNumber("bubble", "radius", 16);
            frame = BubbleFrameMetrics.Resolve(layout, radius, layout.Id == "minimal");
            design = BubbleTokens.Resolve(palette, accent);
            card.Background = new SolidColorBrush(design.Surface);
            string cardColor = cfg.GetString("bubble", "cardColor", "");
            if (!string.IsNullOrEmpty(cardColor) && cardColor.StartsWith("#", StringComparison.Ordinal))
            {
                try { card.Background = Brush(cardColor); }
                catch { }
            }
            card.CornerRadius = new CornerRadius(Math.Max(0, radius));
            card.Width = layout.Width;
            card.Height = layout.Height;
            card.Padding = new Thickness(layout.PaddingLeft, layout.PaddingTop,
                layout.PaddingRight + frame.RightSafe, layout.PaddingBottom);
            body.Width = frame.ContentWidth;
            body.Height = frame.ContentHeight;
            footerRule.Width = ContentWidth;
            title.MaxWidth = frame.HeaderTitleWidth(false);
            sub.MaxWidth = ContentWidth;
            sub.MaxHeight = ScaledTextHeight(layout.Id == "minimal" ? 20 : 24);
            amountBox.MaxWidth = ContentWidth;
            trendSummary.MaxWidth = ContentWidth;
            quip.MaxWidth = ContentWidth;
            quip.MaxHeight = ScaledTextHeight(layout.Id == "minimal" ? 48
                : (layout.Id == "dashboard" ? 92 : 72));
            note.MaxWidth = ContentWidth;
            source.MaxWidth = frame.FooterSourceWidth;
            dockDots.Width = 34;
            dockDots.ClipToBounds = true;
            note.MaxHeight = ScaledTextHeight(layout.Id == "minimal" ? 38
                : (layout.Id == "dashboard" ? 62 : 48));
            double chartHeight = layout.Id == "wide" ? 26
                : (layout.Id == "dashboard" ? 42 : 32);
            trend.Height = chartHeight;
            trendCanvas.Height = chartHeight;
            trendHits.Height = chartHeight;
            // 阴影跟着圆角走：直角配紧一点的阴影，越圆阴影越柔、偏移越大
            //（radius=16 时正好是原来的 22 / 5，老配置观感不变）。
            double shadowRadius = Math.Max(0, Math.Min(40, radius));
            shadow.BlurRadius = 14 + shadowRadius * 0.5;
            shadow.ShadowDepth = 3 + shadowRadius * 0.15;
            UpdateShadowClip();
            double tailSize = cfg.GetNumber("bubble", "tailSize", 13);
            if (tailSize < 6) tailSize = 6;
            if (tailSize > 26) tailSize = 26;
            tailTopShape.Width = tailSize;
            tailTopShape.Height = tailSize;
            tailBottomShape.Width = tailSize;
            tailBottomShape.Height = tailSize;
            tailTop.Height = tailSize;
            tailBottom.Height = tailSize;
            Width = layout.Width + BubbleShadowPad * 2;
            Height = layout.Height + (showTail ? tailSize : 0) + BubbleShadowPad * 2 + 2;
            Opacity = cfg.GetNumber("bubble", "opacity", 1.0);
            shadow.Opacity = cfg.GetNumber("bubble", "shadow", palette.ShadowOpacity);
            foreach (KeyValuePair<TextBlock, double> pair in scalable)
            {
                pair.Key.FontSize = Math.Round(pair.Value * fontScale, 1);
            }
            // Font adaptation: follow the system UI font by default, or a chosen family;
            // the big number can use a monospaced face for stable digit widths.
            string fontName = cfg.GetString("bubble", "font", "");
            FontFamily family = string.IsNullOrEmpty(fontName) ? SystemFonts.MessageFontFamily : SafeFont(fontName);
            FontFamily = family;
            string numberFont = cfg.GetString("bubble", "numberFont", "");
            amount.FontFamily = string.IsNullOrEmpty(numberFont) ? family : SafeFont(numberFont);
            // 一切皆可自定义：逐元素文字色覆盖（留空 = 跟随主题）。
            textOverrides.Clear();
            foreach (KeyValuePair<string, object> pair in cfg.GetObject("bubble", "textColors"))
            {
                string value = Convert.ToString(pair.Value, CultureInfo.InvariantCulture);
                if (!string.IsNullOrEmpty(value) && value.StartsWith("#", StringComparison.Ordinal))
                {
                    textOverrides[pair.Key] = value;
                }
            }
            title.Foreground = Override("title", palette.Title);
            amount.Foreground = Override("big", palette.Big);
            sub.Foreground = Override("sub", palette.Muted);
            quip.Foreground = Override("quip", palette.QuipText);
            quipLine.Foreground = Override("quip", SecondaryTextColor);
            source.Foreground = Override("source", SecondaryTextColor);
            titleDot.Fill = CurrentBrush();
            tailTopShape.FillBrush = card.Background;
            tailTopShape.StrokeBrush = card.BorderBrush;
            tailTopShape.StrokeWidth = card.BorderThickness.Left;
            tailBottomShape.FillBrush = card.Background;
            tailBottomShape.StrokeBrush = card.BorderBrush;
            tailBottomShape.StrokeWidth = card.BorderThickness.Left;
            titleDot.Fill = CurrentBrush();
            title.Foreground = new SolidColorBrush(palette.Title);
            amount.Foreground = new SolidColorBrush(palette.Big);
            sub.Foreground = new SolidColorBrush(palette.Muted);
            quipBox.Background = new SolidColorBrush(palette.QuipBackground);
            quip.Foreground = new SolidColorBrush(palette.QuipText);
            quipLine.Foreground = new SolidColorBrush(SecondaryTextColor);
            source.Foreground = new SolidColorBrush(SecondaryTextColor);
            ApplyAccentBrushes();
        }

        private void ApplyViewMode(string mode)
        {
            viewMode = string.IsNullOrEmpty(mode) ? "data" : mode;
            amountBox.Visibility = Visibility.Visible;
            amountBox.HorizontalAlignment = HorizontalAlignment.Left;
            amountBox.Width = double.NaN;
            amount.HorizontalAlignment = HorizontalAlignment.Left;
            amount.TextAlignment = TextAlignment.Left;
            amount.Margin = new Thickness(0, 6, 0, 0);
            sub.TextAlignment = TextAlignment.Left;
            sub.HorizontalAlignment = HorizontalAlignment.Stretch;
            sub.Margin = new Thickness(0, 3, 0, 0);
            rows.Margin = new Thickness(0, 4, 0, 0);
            textPanel.VerticalAlignment = VerticalAlignment.Top;
            trendPanel.Visibility = Visibility.Visible;
            trendPanel.VerticalAlignment = VerticalAlignment.Top;
            quipBox.HorizontalAlignment = HorizontalAlignment.Stretch;
            quip.TextAlignment = TextAlignment.Left;
            note.TextAlignment = TextAlignment.Left;

            bool centeredHero = viewMode == "balance" || viewMode == "spend"
                || viewMode == "provider" || viewMode == "quota"
                || viewMode == "status" || viewMode == "trend";
            if (centeredHero)
            {
                amountBox.HorizontalAlignment = HorizontalAlignment.Center;
                amountBox.Width = layout.ContentWidth;
                amount.HorizontalAlignment = HorizontalAlignment.Center;
                amount.TextAlignment = TextAlignment.Center;
                amount.Margin = new Thickness(0, 10, 0, 0);
                sub.HorizontalAlignment = HorizontalAlignment.Center;
                sub.TextAlignment = TextAlignment.Center;
                sub.Margin = new Thickness(0, 5, 0, 0);
            }
            if (viewMode == "message")
            {
                amountBox.Visibility = Visibility.Collapsed;
                rows.Visibility = Visibility.Collapsed;
                trendPanel.Visibility = Visibility.Collapsed;
                textPanel.VerticalAlignment = VerticalAlignment.Center;
                quipBox.HorizontalAlignment = HorizontalAlignment.Stretch;
                quip.TextAlignment = TextAlignment.Center;
                note.TextAlignment = TextAlignment.Center;
            }
            else if (viewMode == "trend")
            {
                trendPanel.VerticalAlignment = VerticalAlignment.Stretch;
            }
        }

        public void UpdateFrom(BubbleView view)
        {
            lastView = view;
            viewMode = string.IsNullOrEmpty(view.Mode) ? "data" : view.Mode;
            title.Text = view.Title;
            title.ToolTip = view.Title;
            bool showPeakBadge = viewMode == "balance" && !string.IsNullOrEmpty(view.PeakText);
            if (showPeakBadge) RefreshPeak(view.PeakText, view.PeakNow);
            else badge.Visibility = Visibility.Collapsed;
            title.MaxWidth = frame.HeaderTitleWidth(showPeakBadge);

            layoutVariant = ResolveLayoutVariant();
            bool minimalShell = layoutVariant == "minimal";
            bool customShell = layoutVariant == "custom";
            bool showHeader = customShell
                ? config.GetFlag("bubble", "customHeader", true) : !minimalShell;
            bool showFooter = customShell
                ? config.GetFlag("bubble", "customFooter", true) : !minimalShell;
            header.Visibility = showHeader ? Visibility.Visible : Visibility.Collapsed;
            footerRule.Visibility = showFooter ? Visibility.Visible : Visibility.Collapsed;
            pageIconHost.Visibility = showHeader
                && (!customShell || config.GetFlag("bubble", "customIcon", true))
                ? Visibility.Visible : Visibility.Collapsed;
            bool showQuip = !minimalShell
                && viewMode != "message" && viewMode != "alert"
                && !string.IsNullOrEmpty(view.Quip)
                && (!customShell || config.GetFlag("bubble", "customQuip", true));
            double quipWidth = Math.Max(40, ContentWidth - 22);
            bool quipWraps = showQuip
                && MeasureTextWidth(view.Quip, 8.8, FontWeights.Normal) > quipWidth;
            quipRow.Visibility = showQuip ? Visibility.Visible : Visibility.Collapsed;
            quipLine.Text = showQuip ? view.Quip : "";
            quipLine.ToolTip = showQuip ? view.Quip : null;
            quipLine.TextWrapping = quipWraps ? TextWrapping.Wrap : TextWrapping.NoWrap;
            quipLine.TextTrimming = quipWraps
                ? TextTrimming.WordEllipsis : TextTrimming.CharacterEllipsis;
            quipLine.MaxHeight = ScaledTextHeight(quipWraps ? 34 : 18);
            header.MinHeight = ScaledTextHeight(24);
            quipRow.MinHeight = showQuip ? ScaledTextHeight(quipWraps ? 34 : 18) : 0;
            footerRule.MinHeight = minimalShell ? 0 : ScaledTextHeight(18);
            if (minimalShell)
            {
                pageHeight = frame.ContentHeight;
                double radius = card.CornerRadius.TopLeft;
                double cornerInset = radius > 18 ? Math.Min(9, (radius - 18) * 0.65) : 0;
                pageHost.Margin = new Thickness(cornerInset, 2, cornerInset, 5);
            }
            else
            {
                double reserved = (showHeader ? header.MinHeight : 0)
                    + (showQuip ? quipRow.MinHeight : 0)
                    + (showFooter ? footerRule.MinHeight : 0)
                    + ScaledTextHeight(9);
                pageHeight = Math.Max(64, frame.ContentHeight - reserved);
                pageHost.Margin = new Thickness(0, 5, 0, 4);
            }

            source.Text = view.Source;
            source.ToolTip = string.IsNullOrEmpty(view.Note)
                ? view.Source : view.Source + Environment.NewLine + view.Note;
            pageHost.Content = BuildPageContent(view);
            pageIconHost.Content = BuildPageTypeIcon(view.Mode, 22);
            UpdateDots(view.PageIndex, view.PageCount);
        }

        private FrameworkElement BuildPageContent(BubbleView view)
        {
            accentAppliers.Clear();
            if (layoutVariant == "minimal") return BuildMinimalPage(view);
            if (layoutVariant == "compact") return BuildCompactPage(view);
            if (layoutVariant == "custom") return BuildCustomPage(view);
            if (layoutVariant == "dashboard" && view.Mode == "spend")
                return BuildDashboardSpendPage(view);
            if (layoutVariant == "wide" && fontScale >= 1.45 && view.Mode == "status")
                return BuildCompactPeakPage(view);
            if (layoutVariant == "wide") return BuildWidePage(view);
            switch (view.Mode)
            {
                case "balance": return BuildBalancePage(view);
                case "spend": return BuildSpendPage(view);
                case "trend": return BuildTrendPage(view);
                case "status": return BuildPeakPage(view);
                case "provider": return BuildProviderPage(view);
                case "quota": return BuildQuotaPage(view);
                case "message": return BuildMessagePage(view);
                case "alert": return BuildAlertPage(view);
                default: return BuildFallbackPage(view);
            }
        }

        private string ResolveLayoutVariant()
        {
            string id = layout == null ? "standard" : layout.Id;
            if (id == "minimal" || id == "compact" || id == "wide" || id == "dashboard")
                return id;
            if (id == "custom")
                return "custom";
            return "standard";
        }

        private Color ResolveBigColor(BubbleView view, Color fallback)
        {
            Color color = string.IsNullOrEmpty(view.BigColor)
                ? fallback : ParseColor(view.BigColor, PaletteHex(fallback));
            return SemanticTextColor(color);
        }

        private Color SemanticTextColor(Color color)
        {
            return dark ? BubbleEffects.Mix(color, Colors.White, 0.24) : color;
        }

        private Color ErrorColor
        {
            get { return design.Error; }
        }

        private Color SuccessColor
        {
            get { return design.Success; }
        }

        private Color SecondaryTextColor
        {
            get { return design.Secondary; }
        }

        private FrameworkElement BuildPageTypeIcon(string mode, double size)
        {
            Border tile = new Border();
            tile.Width = size;
            tile.Height = size;
            tile.CornerRadius = new CornerRadius(Math.Max(5, size * 0.3));
            tile.Background = new SolidColorBrush(WithAlpha(accent, dark ? (byte)52 : (byte)28));
            tile.BorderBrush = new SolidColorBrush(WithAlpha(accent, dark ? (byte)88 : (byte)48));
            tile.BorderThickness = new Thickness(1);
            tile.HorizontalAlignment = HorizontalAlignment.Center;
            tile.VerticalAlignment = VerticalAlignment.Center;
            accentAppliers.Add(delegate(Brush brush)
            {
                Color dynamic = DynamicColor();
                tile.Background = new SolidColorBrush(WithAlpha(dynamic, dark ? (byte)52 : (byte)28));
                tile.BorderBrush = new SolidColorBrush(WithAlpha(dynamic, dark ? (byte)88 : (byte)48));
            });

            Canvas art = new Canvas();
            art.Width = 14;
            art.Height = 14;
            Color ink = palette.Title;
            Color mark = SemanticTextColor(accent);
            if (mode == "balance")
            {
                AddIconBox(art, 1, 4, 12, 8, ink, false, 2);
                AddIconEllipse(art, 8, 6.5, 2.2, 2.2, mark);
            }
            else if (mode == "spend")
            {
                AddIconBox(art, 2, 7, 3, 5, ink, true, 1);
                AddIconBox(art, 6, 4, 3, 8, ink, true, 1);
                AddIconBox(art, 10, 2, 3, 10, mark, true, 1);
            }
            else if (mode == "trend")
            {
                AddIconLine(art, 1, 12, 13, 12, WithAlpha(ink, 90), 1);
                AddIconBox(art, 1.5, 7, 2.5, 4, ink, true, 1);
                AddIconBox(art, 5.7, 4, 2.5, 7, ink, true, 1);
                AddIconBox(art, 9.9, 1.5, 2.5, 9.5, mark, true, 1);
            }
            else if (mode == "status")
            {
                AddIconEllipse(art, 1.5, 1.5, 11, 11, ink, false, 1.4);
                AddIconLine(art, 7, 4, 7, 7.4, ink, 1.3);
                AddIconLine(art, 7, 7.4, 9.4, 8.7, mark, 1.3);
            }
            else if (mode == "provider")
            {
                AddIconLine(art, 4, 5, 10, 9, ink, 1.2);
                AddIconLine(art, 4, 9.5, 10, 5.5, WithAlpha(ink, 110), 1);
                AddIconEllipse(art, 1, 2.5, 4, 4, mark, true, 0);
                AddIconEllipse(art, 9, 7.5, 4, 4, ink, true, 0);
            }
            else if (mode == "quota")
            {
                AddIconEllipse(art, 2, 2, 10, 10, ink, false, 1.4);
                AddIconEllipse(art, 6.2, 6.2, 1.6, 1.6, mark, true, 0);
                AddIconLine(art, 7, 7, 10, 3.5, mark, 1.4);
            }
            else if (mode == "message")
            {
                AddIconBox(art, 1.5, 2.5, 11, 7.5, ink, false, 2);
                Path tail = new Path();
                tail.Data = Geometry.Parse("M 5,10 L 4,13 L 8,10");
                tail.Fill = new SolidColorBrush(ink);
                art.Children.Add(tail);
                AddIconLine(art, 4, 5.2, 10, 5.2, mark, 1);
                AddIconLine(art, 4, 7.2, 8.5, 7.2, WithAlpha(ink, 120), 1);
            }
            else if (mode == "alert")
            {
                Path triangle = new Path();
                triangle.Data = Geometry.Parse("M 7,1 L 13,12 L 1,12 Z");
                triangle.Fill = new SolidColorBrush(WithAlpha(ErrorColor, 42));
                triangle.Stroke = new SolidColorBrush(ErrorColor);
                triangle.StrokeThickness = 1.2;
                art.Children.Add(triangle);
                AddIconLine(art, 7, 4.2, 7, 8, ErrorColor, 1.3);
                AddIconEllipse(art, 6.3, 9.4, 1.4, 1.4, ErrorColor, true, 0);
            }
            else
            {
                AddIconEllipse(art, 4, 4, 6, 6, mark, true, 0);
            }

            Viewbox view = new Viewbox();
            view.Width = Math.Max(9, size - 7);
            view.Height = Math.Max(9, size - 7);
            view.Stretch = Stretch.Uniform;
            view.Child = art;
            tile.Child = view;
            tile.ToolTip = PageIconLabel(mode);
            return tile;
        }

        private static string PageIconLabel(string mode)
        {
            switch (mode)
            {
                case "balance": return "余额";
                case "spend": return "消耗";
                case "trend": return "趋势";
                case "status": return "计价时段";
                case "provider": return "接口";
                case "quota": return "套餐额度";
                case "message": return "消息";
                case "alert": return "提醒";
                default: return "状态";
            }
        }

        private static void AddIconBox(Canvas canvas, double left, double top, double width,
            double height, Color color, bool fill, double radius)
        {
            Border box = new Border();
            box.Width = width;
            box.Height = height;
            box.CornerRadius = new CornerRadius(radius);
            if (fill) box.Background = new SolidColorBrush(color);
            else box.BorderBrush = new SolidColorBrush(color);
            if (!fill) box.BorderThickness = new Thickness(1.2);
            Canvas.SetLeft(box, left);
            Canvas.SetTop(box, top);
            canvas.Children.Add(box);
        }

        private static void AddIconEllipse(Canvas canvas, double left, double top,
            double width, double height, Color color, bool fill = true, double stroke = 1.2)
        {
            Ellipse ellipse = new Ellipse();
            ellipse.Width = width;
            ellipse.Height = height;
            if (fill) ellipse.Fill = new SolidColorBrush(color);
            else
            {
                ellipse.Stroke = new SolidColorBrush(color);
                ellipse.StrokeThickness = stroke;
            }
            Canvas.SetLeft(ellipse, left);
            Canvas.SetTop(ellipse, top);
            canvas.Children.Add(ellipse);
        }

        private static void AddIconLine(Canvas canvas, double x1, double y1,
            double x2, double y2, Color color, double thickness)
        {
            Line line = new Line();
            line.X1 = x1;
            line.Y1 = y1;
            line.X2 = x2;
            line.Y2 = y2;
            line.Stroke = new SolidColorBrush(color);
            line.StrokeThickness = thickness;
            line.StrokeStartLineCap = PenLineCap.Round;
            line.StrokeEndLineCap = PenLineCap.Round;
            canvas.Children.Add(line);
        }

        private string PrimaryLabel(BubbleView view)
        {
            switch (view.Mode)
            {
                case "balance": return "可用余额";
                case "spend": return "今日已消耗";
                case "provider": return "接口余额";
                case "quota": return "窗口用量";
                case "status": return "当前计价";
                case "trend": return "区间合计";
                case "alert": return "当前提醒";
                default: return "";
            }
        }

        private string RowsInline(BubbleView view)
        {
            StringBuilder text = new StringBuilder();
            for (int i = 0; i < view.Rows.Count && i < 2; i++)
            {
                if (text.Length > 0) text.Append(" · ");
                text.Append(view.Rows[i].Key).Append(" ").Append(view.Rows[i].Value);
            }
            if (text.Length == 0) text.Append(view.Sub);
            return text.ToString();
        }

        private FrameworkElement BuildMinimalPage(BubbleView view)
        {
            if (view.Mode == "spend") return BuildMinimalSpendPage(view);
            Grid page = new Grid();
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid heading = new Grid();
            heading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            heading.Margin = new Thickness(0, 0, 5, 0);
            bool showTopPager = view.PageCount > 1 && fontScale < 1.45;
            double pagerWidth = showTopPager
                ? MeasureTextWidth((view.PageIndex + 1).ToString(CultureInfo.InvariantCulture)
                    + " / " + view.PageCount.ToString(CultureInfo.InvariantCulture),
                    8.1, FontWeights.SemiBold) + 14 : 0;
            StackPanel headingText = new StackPanel();
            headingText.Orientation = Orientation.Horizontal;
            FrameworkElement icon = BuildPageTypeIcon(view.Mode, 18);
            icon.Margin = new Thickness(0, 0, 6, 0);
            headingText.Children.Add(icon);
            TextBlock title = PageText(9.4, palette.RowValue, view.Title);
            title.FontWeight = FontWeights.SemiBold;
            title.VerticalAlignment = VerticalAlignment.Center;
            title.MaxWidth = Math.Max(70, ContentWidth - 30 - pagerWidth);
            headingText.Children.Add(title);
            heading.Children.Add(headingText);
            if (showTopPager)
            {
                TextBlock pager = PageText(8.1, SecondaryTextColor,
                    (view.PageIndex + 1).ToString(CultureInfo.InvariantCulture)
                    + " / " + view.PageCount.ToString(CultureInfo.InvariantCulture));
                pager.FontWeight = FontWeights.SemiBold;
                pager.Margin = new Thickness(8, 0, 3, 0);
                pager.HorizontalAlignment = HorizontalAlignment.Right;
                pager.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(pager, 1);
                heading.Children.Add(pager);
            }
            page.Children.Add(heading);

            string primary = view.Big;
            string secondary = view.Sub;
            Color primaryColor = palette.Big;
            double primarySize = 27;
            bool message = view.Mode == "message";
            if (view.Mode == "trend")
            {
                int days;
                double total;
                double average;
                TrendTotals(view, out days, out total, out average);
                primary = Fmt.Money(total, view.TrendCurrency);
                secondary = "日均 " + Fmt.Money(average, view.TrendCurrency)
                    + " · 最近 " + days.ToString(CultureInfo.InvariantCulture) + " 天";
            }
            else if (view.Mode == "spend")
            {
                primaryColor = ResolveBigColor(view, palette.Big);
                secondary = MiniMetricSummary(view);
            }
            else if (view.Mode == "quota")
            {
                primaryColor = ResolveBigColor(view, palette.Big);
                secondary = string.IsNullOrEmpty(view.ResetText) ? "重置时间未提供" : view.ResetText;
            }
            else if (view.Mode == "status")
            {
                primaryColor = ResolveBigColor(view, palette.Big);
                secondary = string.IsNullOrEmpty(view.Sub) ? "" : view.Sub + " 后切换";
            }
            else if (view.Mode == "alert")
            {
                primaryColor = ErrorColor;
                secondary = view.Note;
            }
            else if (message)
            {
                primary = string.IsNullOrEmpty(view.Quip) ? view.Big : view.Quip;
                secondary = "";
                primaryColor = palette.Big;
                primarySize = 12.8;
            }
            else
            {
                primaryColor = ResolveBigColor(view, palette.Big);
            }

            FrameworkElement hero = message
                ? (FrameworkElement)BuildMinimalMessage(primary, primaryColor)
                : PageAmount(string.IsNullOrEmpty(primary) ? "--" : primary,
                    primarySize, primaryColor, 35);
            hero.VerticalAlignment = VerticalAlignment.Center;
            hero.Margin = new Thickness(0, 2, 0, 2);
            Grid.SetRow(hero, 1);
            page.Children.Add(hero);

            if (!string.IsNullOrEmpty(view.Quip) && view.Mode != "spend"
                && view.Mode != "message" && view.Mode != "alert")
            {
                secondary = view.Quip;
            }
            Grid footer = new Grid();
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            footer.Margin = new Thickness(0, 0, 5, 0);
            StackPanel footerCopy = new StackPanel();
            if (!string.IsNullOrEmpty(secondary))
            {
                TextBlock summary = PageText(8.6, palette.RowValue, secondary);
                summary.MaxWidth = ContentWidth;
                summary.MaxHeight = ScaledTextHeight(34);
                summary.TextWrapping = TextWrapping.Wrap;
                summary.TextTrimming = TextTrimming.None;
                summary.Margin = new Thickness(0, 0, 4, 3);
                summary.ToolTip = secondary;
                footerCopy.Children.Add(summary);
            }
            if (view.Mode == "spend" && !string.IsNullOrEmpty(view.Quip))
            {
                TextBlock quip = PageText(8.2, SecondaryTextColor, "“ " + view.Quip);
                quip.Margin = new Thickness(0, 1, 4, 1);
                quip.TextWrapping = TextWrapping.Wrap;
                quip.TextTrimming = TextTrimming.None;
                quip.MaxHeight = ScaledTextHeight(28);
                quip.ToolTip = view.Quip;
                footerCopy.Children.Add(quip);
            }
            footer.Children.Add(footerCopy);
            if (view.PageCount > 1 && !showTopPager)
            {
                TextBlock pager = PageText(8.1, SecondaryTextColor,
                    (view.PageIndex + 1).ToString(CultureInfo.InvariantCulture)
                    + " / " + view.PageCount.ToString(CultureInfo.InvariantCulture));
                pager.FontWeight = FontWeights.SemiBold;
                pager.VerticalAlignment = VerticalAlignment.Bottom;
                pager.Margin = new Thickness(7, 0, 1, 2);
                Grid.SetColumn(pager, 1);
                footer.Children.Add(pager);
            }
            Grid.SetRow(footer, 2);
            page.Children.Add(footer);
            return page;
        }

        private string MiniMetricSummary(BubbleView view)
        {
            if (view.Rows.Count < 2) return view.Sub;
            return "昨 " + view.Rows[0].Value + "   ·   7 日均 " + view.Rows[1].Value;
        }

        private FrameworkElement BuildMinimalSpendPage(BubbleView view)
        {
            Grid page = new Grid();
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            Grid heading = new Grid();
            heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            heading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            FrameworkElement icon = BuildPageTypeIcon("spend", 18);
            icon.Margin = new Thickness(0, 0, 6, 0);
            heading.Children.Add(icon);
            TextBlock title = PageText(9.4, palette.RowValue, view.Title);
            title.FontWeight = FontWeights.SemiBold;
            title.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(title, 1);
            heading.Children.Add(title);
            page.Children.Add(heading);

            Viewbox amount = PageAmount(string.IsNullOrEmpty(view.Big) ? "--" : view.Big,
                27, ResolveBigColor(view, palette.Big), 35);
            amount.Margin = new Thickness(0, 2, 0, 1);
            Grid.SetRow(amount, 1);
            page.Children.Add(amount);

            StackPanel metrics = new StackPanel();
            if (view.Rows.Count >= 2)
            {
                metrics.Children.Add(BuildMinimalMetricLine("昨日", view.Rows[0].Value));
                metrics.Children.Add(BuildMinimalMetricLine("7 日均", view.Rows[1].Value));
            }
            Grid.SetRow(metrics, 2);
            page.Children.Add(metrics);

            Grid footer = new Grid();
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            TextBlock quip = PageText(8.1, SecondaryTextColor,
                string.IsNullOrEmpty(view.Quip) ? "" : "“ " + view.Quip);
            quip.TextWrapping = TextWrapping.Wrap;
            quip.TextTrimming = TextTrimming.WordEllipsis;
            quip.MaxHeight = ScaledTextHeight(30);
            quip.VerticalAlignment = VerticalAlignment.Bottom;
            quip.ToolTip = view.Quip;
            footer.Children.Add(quip);
            TextBlock pager = PageText(8.1, SecondaryTextColor,
                (view.PageIndex + 1).ToString(CultureInfo.InvariantCulture)
                + " / " + view.PageCount.ToString(CultureInfo.InvariantCulture));
            pager.FontWeight = FontWeights.SemiBold;
            pager.VerticalAlignment = VerticalAlignment.Bottom;
            pager.Margin = new Thickness(8, 0, 2, 0);
            Grid.SetColumn(pager, 1);
            footer.Children.Add(pager);
            Grid.SetRow(footer, 3);
            page.Children.Add(footer);
            return page;
        }

        private Grid BuildMinimalMetricLine(string labelText, string valueText)
        {
            Grid line = new Grid();
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            TextBlock label = PageText(8.2, palette.Muted, labelText);
            label.VerticalAlignment = VerticalAlignment.Center;
            line.Children.Add(label);
            TextBlock value = PageText(10.2, palette.RowValue, valueText);
            value.FontWeight = FontWeights.SemiBold;
            value.TextAlignment = TextAlignment.Right;
            value.TextTrimming = TextTrimming.None;
            value.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(value, 1);
            line.Children.Add(value);
            return line;
        }

        private TextBlock BuildMinimalMessage(string message, Color color)
        {
            TextBlock text = PageText(12.8, color, message);
            text.TextWrapping = TextWrapping.Wrap;
            text.TextAlignment = TextAlignment.Center;
            text.MaxHeight = Math.Max(ScaledTextHeight(34),
                layout.ContentHeight - ScaledTextHeight(34));
            return text;
        }

        private FrameworkElement BuildCompactPage(BubbleView view)
        {
            if (view.Mode == "trend") return BuildCompactTrendPage(view);
            if (view.Mode == "status") return BuildCompactPeakPage(view);
            if (view.Mode == "quota") return BuildCompactQuotaPage(view);
            return BuildCompactHeroPage(view);
        }

        private FrameworkElement BuildCompactHeroPage(BubbleView view)
        {
            Grid page = new Grid();
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            string labelText = PrimaryLabel(view);
            if (!string.IsNullOrEmpty(labelText))
            {
                TextBlock label = PageText(8.6, palette.Muted, labelText);
                page.Children.Add(label);
            }

            string primary = view.Mode == "message"
                ? (string.IsNullOrEmpty(view.Quip) ? view.Big : view.Quip) : view.Big;
            Color primaryColor = view.Mode == "alert"
                ? ErrorColor : ResolveBigColor(view, palette.Big);
            FrameworkElement hero;
            if (view.Mode == "message")
            {
                hero = BuildMinimalMessage(primary, primaryColor);
            }
            else if (view.Mode == "alert")
            {
                TextBlock alert = PageText(14.2, primaryColor,
                    string.IsNullOrEmpty(primary) ? "出现异常" : primary);
                alert.FontWeight = FontWeights.SemiBold;
                alert.TextWrapping = TextWrapping.Wrap;
                alert.MaxHeight = ScaledTextHeight(42);
                hero = alert;
            }
            else
            {
                hero = PageAmount(string.IsNullOrEmpty(primary) ? "--" : primary,
                    view.Mode == "provider" || view.Mode == "balance" ? 26 : 28,
                    primaryColor, 40);
            }
            hero.Margin = new Thickness(0, 2, 0, 0);
            Grid.SetRow(hero, 1);
            page.Children.Add(hero);

            FrameworkElement detail;
            if (view.Mode == "spend" && view.Rows.Count >= 2)
            {
                detail = fontScale >= 1.45
                    ? (FrameworkElement)BuildCompactMetricRows(view.Rows[0], view.Rows[1])
                    : BuildMetricColumns(view.Rows[0], view.Rows[1]);
            }
            else
            {
                string text = view.Mode == "alert" ? view.Note : view.Sub;
                if (string.IsNullOrEmpty(text) && view.Rows.Count > 0) text = RowsInline(view);
                TextBlock block = PageText(8.7, palette.Muted, string.IsNullOrEmpty(text) ? "" : text);
                block.TextWrapping = TextWrapping.Wrap;
                block.MaxHeight = ScaledTextHeight(31);
                block.ToolTip = text;
                detail = block;
            }
            detail.Margin = new Thickness(0, 5, 0, 0);
            Grid.SetRow(detail, 2);
            page.Children.Add(detail);

            if (view.NoteError && !string.IsNullOrEmpty(view.Note) && view.Mode != "alert")
            {
                FrameworkElement error = BuildErrorBand(view.Note);
                error.Margin = new Thickness(0, 5, 0, 0);
                Grid.SetRow(error, 3);
                page.Children.Add(error);
            }
            return page;
        }

        private StackPanel BuildCompactMetricRows(
            KeyValuePair<string, string> left,
            KeyValuePair<string, string> right)
        {
            StackPanel rows = new StackPanel();
            rows.Children.Add(BuildCompactMetricRow("昨日", left.Value));
            rows.Children.Add(BuildCompactMetricRow("7 日均", right.Value));
            return rows;
        }

        private Grid BuildCompactMetricRow(string labelText, string valueText)
        {
            Grid row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            TextBlock label = PageText(8.5, palette.Muted, labelText);
            label.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(label);
            TextBlock value = PageText(10.4, palette.RowValue, valueText);
            value.FontWeight = FontWeights.SemiBold;
            value.TextAlignment = TextAlignment.Right;
            value.TextTrimming = TextTrimming.None;
            value.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(value, 1);
            row.Children.Add(value);
            return row;
        }

        private FrameworkElement BuildCompactTrendPage(BubbleView view)
        {
            int days;
            double total;
            double average;
            TrendTotals(view, out days, out total, out average);
            Grid page = new Grid();
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            Grid summary = new Grid();
            summary.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            double averageColumnWidth = 66 + Math.Max(0, fontScale - 1) * 24;
            summary.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(averageColumnWidth) });
            summary.Margin = new Thickness(0, 0, 3, 0);
            StackPanel left = new StackPanel();
            left.Children.Add(PageText(8.2, palette.Muted, "区间合计"));
            Viewbox amount = PageAmount(Fmt.Money(total, view.TrendCurrency), 20, palette.Big, 28);
            amount.Width = double.NaN;
            amount.HorizontalAlignment = HorizontalAlignment.Left;
            left.Children.Add(amount);
            summary.Children.Add(left);
            StackPanel right = new StackPanel();
            right.HorizontalAlignment = HorizontalAlignment.Right;
            right.Margin = new Thickness(5, 0, 0, 0);
            TextBlock averageLabel = PageText(7.8, palette.Muted, "日均");
            averageLabel.HorizontalAlignment = HorizontalAlignment.Right;
            TextBlock averageValue = PageText(8.8, palette.RowValue,
                Fmt.Money(average, view.TrendCurrency));
            averageValue.TextAlignment = TextAlignment.Right;
            Viewbox averageBox = new Viewbox();
            averageBox.Width = averageColumnWidth - 8;
            averageBox.Height = ScaledTextHeight(14);
            averageBox.HorizontalAlignment = HorizontalAlignment.Right;
            averageBox.Stretch = Stretch.Uniform;
            averageBox.StretchDirection = StretchDirection.DownOnly;
            averageBox.Child = averageValue;
            right.Children.Add(averageLabel);
            right.Children.Add(averageBox);
            Grid.SetColumn(right, 1);
            summary.Children.Add(right);
            page.Children.Add(summary);

            FrameworkElement chart = BuildTrendBars(view, 34);
            chart.Margin = new Thickness(0, 4, 0, 0);
            chart.Cursor = System.Windows.Input.Cursors.Hand;
            chart.MouseLeftButtonDown += delegate(object sender, System.Windows.Input.MouseButtonEventArgs e)
            {
                CycleTrendRange();
                e.Handled = true;
            };
            Grid.SetRow(chart, 1);
            page.Children.Add(chart);
            return page;
        }

        private FrameworkElement BuildCompactPeakPage(BubbleView view)
        {
            Color statusColor = ResolveBigColor(view, palette.Big);
            Grid page = new Grid();
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            Grid state = new Grid();
            state.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            double compactStatusMax = Math.Max(84, ContentWidth
                - MeasureTextWidth(view.Big, 16, FontWeights.SemiBold) - 10);
            double compactPillWidth = CountdownPillWidth(view.Sub, compactStatusMax);
            state.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(compactPillWidth)
            });
            TextBlock current = PageText(16, statusColor, view.Big);
            current.FontWeight = FontWeights.SemiBold;
            current.VerticalAlignment = VerticalAlignment.Center;
            state.Children.Add(current);
            Border countdown = BuildCountdownPill(view.Sub, statusColor,
                compactPillWidth, Math.Max(19, ScaledTextHeight(19)));
            countdown.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(countdown, 1);
            state.Children.Add(countdown);
            page.Children.Add(state);

            Grid timeline = BuildPeakTimeline(statusColor, ContentWidth);
            timeline.Margin = new Thickness(0, 3, 0, 0);
            Grid.SetRow(timeline, 1);
            page.Children.Add(timeline);

            Grid legend = new Grid();
            legend.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.45, GridUnitType.Star) });
            legend.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            legend.Margin = new Thickness(0, 3, 0, 0);
            StackPanel standardLegend = CompactLegend(
                SemanticTextColor(ParseColor("#D9515A", "#D9515A")),
                BuildPeakWindowLine());
            legend.Children.Add(standardLegend);
            StackPanel discountLegend = CompactLegend(SuccessColor, "半价");
            Grid.SetColumn(discountLegend, 1);
            legend.Children.Add(discountLegend);
            Grid.SetRow(legend, 2);
            page.Children.Add(legend);
            return page;
        }

        private StackPanel CompactLegend(Color color, string text)
        {
            StackPanel row = new StackPanel();
            row.Orientation = Orientation.Horizontal;
            Ellipse dot = new Ellipse();
            dot.Width = 5;
            dot.Height = 5;
            dot.Fill = new SolidColorBrush(color);
            dot.VerticalAlignment = VerticalAlignment.Center;
            dot.Margin = new Thickness(0, 0, 5, 0);
            row.Children.Add(dot);
            TextBlock label = PageText(8.2, palette.RowValue, text);
            label.FontWeight = FontWeights.SemiBold;
            label.VerticalAlignment = VerticalAlignment.Center;
            label.TextTrimming = TextTrimming.CharacterEllipsis;
            row.Children.Add(label);
            return row;
        }

        private FrameworkElement BuildCompactQuotaPage(BubbleView view)
        {
            Color amountColor = ResolveBigColor(view, palette.Big);
            Grid page = new Grid();
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            FrameworkElement ring = BuildQuotaRing(view, amountColor,
                Math.Min(50, Math.Max(42, pageHeight - 18)));
            ring.VerticalAlignment = VerticalAlignment.Center;
            page.Children.Add(ring);
            TextBlock reset = PageText(8.2, palette.RowValue,
                string.IsNullOrEmpty(view.ResetText) ? "重置时间未提供" : view.ResetText);
            reset.FontWeight = FontWeights.SemiBold;
            reset.Margin = new Thickness(0, 4, 0, 0);
            reset.TextAlignment = TextAlignment.Center;
            reset.HorizontalAlignment = HorizontalAlignment.Center;
            reset.ToolTip = reset.Text;
            Grid.SetRow(reset, 1);
            page.Children.Add(reset);
            return page;
        }

        private FrameworkElement BuildWidePage(BubbleView view)
        {
            if (view.Mode == "message")
                return BuildMessagePage(view);
            if (view.Mode == "alert")
                return BuildAlertPage(view);

            Grid page = new Grid();
            double heroWidth = Math.Min(ContentWidth - 96,
                Math.Max(108, ContentWidth * (0.38 + Math.Max(0, fontScale - 1) * 0.08)));
            heroWidth = Math.Max(96, heroWidth);
            page.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(heroWidth) });
            page.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            StackPanel hero = new StackPanel();
            string labelText = PrimaryLabel(view);
            if (!string.IsNullOrEmpty(labelText))
                hero.Children.Add(PageText(8.4, palette.Muted, labelText));
            string primary = view.Mode == "trend"
                ? TrendTotalText(view) : view.Big;
            Color primaryColor = ResolveBigColor(view, palette.Big);
            FrameworkElement amount = view.Mode == "quota"
                ? BuildQuotaRing(view, primaryColor, 52)
                : (FrameworkElement)PageAmount(string.IsNullOrEmpty(primary) ? "--" : primary,
                    view.Mode == "status" ? 22 : 24, primaryColor, 38,
                    Math.Max(88, heroWidth - 4));
            amount.Margin = new Thickness(0, 2, 0, 0);
            amount.HorizontalAlignment = HorizontalAlignment.Left;
            hero.Children.Add(amount);
            if (view.Mode == "status")
            {
                double countdownWidth = CountdownPillWidth(view.Sub,
                    Math.Max(84, heroWidth - 6));
                Border countdown = BuildCountdownPill(view.Sub, primaryColor,
                    countdownWidth, Math.Max(20, ScaledTextHeight(20)));
                countdown.HorizontalAlignment = HorizontalAlignment.Left;
                countdown.Margin = new Thickness(0, 5, 0, 0);
                hero.Children.Add(countdown);
            }
            string heroDetail = "";
            if (view.Mode == "trend")
                heroDetail = view.Sub;
            if (!string.IsNullOrEmpty(heroDetail))
            {
                TextBlock detail = PageText(8.1, palette.Muted, heroDetail);
                detail.TextWrapping = TextWrapping.Wrap;
                detail.MaxHeight = ScaledTextHeight(27);
                detail.Margin = new Thickness(0, 4, 0, 0);
                detail.ToolTip = heroDetail;
                hero.Children.Add(detail);
            }
            page.Children.Add(hero);

            StackPanel side = new StackPanel();
            side.Margin = new Thickness(12, 0, 0, 0);
            double sideWidth = Math.Max(84, ContentWidth - heroWidth - 12);
            if (view.Mode == "trend")
            {
                FrameworkElement chart = BuildTrendBars(view, Math.Max(48, layout.ContentHeight - 54), sideWidth);
                side.Children.Add(chart);
            }
            else if (view.Mode == "status")
            {
                side.Children.Add(BuildPeakTimeline(primaryColor, sideWidth));
                Border line = PageHairline();
                line.Margin = new Thickness(0, 5, 0, 5);
                side.Children.Add(line);
                side.Children.Add(BuildScheduleRow(SemanticTextColor(ParseColor("#D9515A", "#D9515A")),
                    "标准", BuildPeakWindowLine()));
                side.Children.Add(BuildScheduleRow(SuccessColor,
                    "半价", "其余时间"));
            }
            else if (view.Mode == "quota")
            {
                TextBlock resetLabel = PageText(8, palette.Muted, "重置");
                TextBlock reset = PageText(9.2, palette.RowValue,
                    string.IsNullOrEmpty(view.ResetText) ? "接口未提供" : view.ResetText);
                reset.FontWeight = FontWeights.SemiBold;
                reset.TextWrapping = TextWrapping.Wrap;
                reset.MaxHeight = ScaledTextHeight(30);
                reset.ToolTip = reset.Text;
                side.Children.Add(resetLabel);
                side.Children.Add(reset);
                if (!string.IsNullOrEmpty(view.Sub))
                {
                    TextBlock detail = PageText(8.2, SecondaryTextColor, view.Sub);
                    detail.TextTrimming = TextTrimming.CharacterEllipsis;
                    detail.Margin = new Thickness(0, 5, 0, 0);
                    detail.ToolTip = view.Sub;
                    side.Children.Add(detail);
                }
            }
            else if (view.Mode == "spend" && view.Rows.Count >= 2)
            {
                side.Children.Add(BuildMetricColumns(view.Rows[0], view.Rows[1], sideWidth));
            }
            else if (view.Mode == "provider")
            {
                Grid state = new Grid();
                state.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                state.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                Ellipse dot = new Ellipse();
                dot.Width = 6;
                dot.Height = 6;
                dot.Fill = new SolidColorBrush(SuccessColor);
                dot.VerticalAlignment = VerticalAlignment.Center;
                dot.Margin = new Thickness(0, 0, 7, 0);
                state.Children.Add(dot);
                TextBlock detail = PageText(9, palette.RowValue,
                    string.IsNullOrEmpty(view.Sub) ? "接口正常" : view.Sub);
                detail.FontWeight = FontWeights.SemiBold;
                detail.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(detail, 1);
                state.Children.Add(detail);
                side.Children.Add(state);
            }
            else
            {
                TextBlock detail = PageText(8.8, palette.Muted,
                    string.IsNullOrEmpty(view.Sub) ? "官方接口" : view.Sub);
                detail.TextWrapping = TextWrapping.Wrap;
                detail.MaxHeight = ScaledTextHeight(48);
                detail.ToolTip = detail.Text;
                side.Children.Add(detail);
            }
            Grid.SetColumn(side, 1);
            page.Children.Add(side);
            return page;
        }

        private TextBlock PageText(double size, Color color, string text)
        {
            TextBlock block = new TextBlock();
            block.FontSize = Math.Round(size * fontScale, 1);
            block.Foreground = new SolidColorBrush(color);
            block.Text = text;
            block.TextTrimming = TextTrimming.CharacterEllipsis;
            TextOptions.SetTextFormattingMode(block, TextFormattingMode.Display);
            return block;
        }

        private double ScaledTextHeight(double baseHeight)
        {
            double scale = Math.Max(0.7, Math.Min(1.8, fontScale));
            return Math.Round(baseHeight * Math.Max(1.0, 0.55 + 0.45 * scale), 1);
        }

        private double MeasureTextWidth(string text, double baseSize, FontWeight weight)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            try
            {
                FontFamily family = FontFamily ?? SystemFonts.MessageFontFamily;
                Typeface typeface = new Typeface(family, FontStyles.Normal, weight, FontStretches.Normal);
                FormattedText formatted = new FormattedText(
                    text,
                    CultureInfo.CurrentUICulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    Math.Round(baseSize * fontScale, 1),
                    Brushes.Black,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);
                return formatted.WidthIncludingTrailingWhitespace;
            }
            catch
            {
                return text.Length * Math.Round(baseSize * fontScale, 1) * 0.62;
            }
        }

        private Viewbox PageAmount(string text, double size, Color color, double height)
        {
            return PageAmount(text, size, color, height, ContentWidth);
        }

        private Viewbox PageAmount(string text, double size, Color color, double height, double width)
        {
            TextBlock block = PageText(size, color, text);
            block.FontWeight = FontWeights.Bold;
            block.TextWrapping = TextWrapping.NoWrap;
            block.TextTrimming = TextTrimming.None;
            Viewbox box = new Viewbox();
            box.Height = ScaledTextHeight(height);
            box.Width = width;
            box.HorizontalAlignment = HorizontalAlignment.Left;
            box.VerticalAlignment = VerticalAlignment.Top;
            box.Stretch = Stretch.Uniform;
            box.StretchDirection = StretchDirection.DownOnly;
            box.Child = block;
            return box;
        }

        private Border PageHairline()
        {
            Border line = new Border();
            line.Height = 1;
            line.Background = new SolidColorBrush(WithAlpha(palette.Border, dark ? (byte)115 : (byte)90));
            return line;
        }

        private Border PageAccentRule()
        {
            Border rule = new Border();
            rule.Width = 28;
            rule.Height = 2;
            rule.CornerRadius = new CornerRadius(1);
            rule.HorizontalAlignment = HorizontalAlignment.Left;
            accentAppliers.Add(delegate(Brush brush) { rule.Background = brush; });
            rule.Background = CurrentBrush();
            return rule;
        }

        private FrameworkElement BuildBalancePage(BubbleView view)
        {
            Grid page = new Grid();
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            StackPanel main = new StackPanel();
            main.VerticalAlignment = VerticalAlignment.Center;
            TextBlock label = PageText(9.2, palette.Muted, "可用余额");
            label.Margin = new Thickness(0, 0, 0, 3);
            main.Children.Add(label);

            Color amountColor = ResolveBigColor(view, palette.Big);
            Viewbox amount = PageAmount(string.IsNullOrEmpty(view.Big) ? "--" : view.Big,
                31, amountColor, 43);
            main.Children.Add(amount);

            StackPanel lower = new StackPanel();
            lower.Margin = new Thickness(0, 7, 0, 0);
            lower.Children.Add(PageAccentRule());
            if (!string.IsNullOrEmpty(view.Sub))
            {
                TextBlock detail = PageText(9.3, palette.Muted, view.Sub);
                detail.Margin = new Thickness(0, 6, 0, 0);
                detail.ToolTip = view.Sub;
                lower.Children.Add(detail);
            }
            main.Children.Add(lower);
            page.Children.Add(main);

            if (view.NoteError && !string.IsNullOrEmpty(view.Note))
            {
                FrameworkElement error = BuildErrorBand(view.Note);
                error.Margin = new Thickness(0, 5, 0, 0);
                Grid.SetRow(error, 1);
                page.Children.Add(error);
            }
            return page;
        }

        private FrameworkElement BuildCustomPage(BubbleView view)
        {
            if (view.Mode == "message") return BuildMessagePage(view);
            if (view.Mode == "alert") return BuildAlertPage(view);

            bool split = config.GetFlag("bubble", "customSplit", false);
            bool showHero = config.GetFlag("bubble", "customHero", true);
            bool showDetail = config.GetFlag("bubble", "customDetail", true);
            bool showMetrics = config.GetFlag("bubble", "customMetrics", true);
            bool showChart = config.GetFlag("bubble", "customChart", true);
            double heroWidth = split ? Math.Max(96, ContentWidth * 0.52) : ContentWidth;
            double sideWidth = split ? Math.Max(72, ContentWidth - heroWidth - 10) : ContentWidth;

            FrameworkElement hero = showHero ? BuildCustomHero(view, heroWidth) : null;
            FrameworkElement detail = showDetail ? BuildCustomDetail(view, sideWidth) : null;
            FrameworkElement metrics = showMetrics
                ? BuildCustomMetrics(view, sideWidth) : null;
            FrameworkElement chart = showChart
                ? BuildCustomChart(view, sideWidth) : null;

            StackPanel right = new StackPanel();
            AddCustomModule(right, detail, false);
            AddCustomModule(right, metrics, right.Children.Count > 0);
            AddCustomModule(right, chart, right.Children.Count > 0);

            if (split && hero != null && right.Children.Count > 0)
            {
                Grid page = new Grid();
                page.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = new GridLength(heroWidth)
                });
                page.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = new GridLength(1, GridUnitType.Star)
                });
                hero.VerticalAlignment = VerticalAlignment.Top;
                page.Children.Add(hero);
                right.Margin = new Thickness(10, 0, 0, 0);
                right.VerticalAlignment = VerticalAlignment.Top;
                Grid.SetColumn(right, 1);
                page.Children.Add(right);
                return page;
            }

            StackPanel stack = new StackPanel();
            AddCustomModule(stack, hero, false);
            while (right.Children.Count > 0)
            {
                UIElement child = right.Children[0];
                right.Children.RemoveAt(0);
                FrameworkElement element = child as FrameworkElement;
                if (element != null)
                {
                    element.Margin = new Thickness(0, stack.Children.Count > 0 ? 6 : 0, 0, 0);
                }
                stack.Children.Add(child);
            }
            if (stack.Children.Count == 0)
            {
                TextBlock empty = PageText(9, palette.Muted, "未选择显示模块");
                empty.HorizontalAlignment = HorizontalAlignment.Center;
                empty.VerticalAlignment = VerticalAlignment.Center;
                return empty;
            }
            return stack;
        }

        private FrameworkElement BuildCustomHero(BubbleView view, double width)
        {
            Color color = ResolveBigColor(view, palette.Big);
            if (view.Mode == "quota")
            {
                double size = Math.Min(72, Math.Max(46, Math.Min(width, pageHeight)));
                FrameworkElement ring = BuildQuotaRing(view, color, size);
                ring.HorizontalAlignment = HorizontalAlignment.Center;
                return ring;
            }
            string text = view.Mode == "trend" ? TrendTotalText(view) : view.Big;
            double sizeValue = view.Mode == "status" ? 22 : 30;
            Viewbox amount = PageAmount(string.IsNullOrEmpty(text) ? "--" : text,
                sizeValue, color, 44, Math.Max(44, width));
            amount.HorizontalAlignment = HorizontalAlignment.Left;
            return amount;
        }

        private FrameworkElement BuildCustomDetail(BubbleView view, double width)
        {
            if (view.Mode == "status")
            {
                StackPanel details = new StackPanel();
                details.Children.Add(BuildPeakTimeline(
                    ResolveBigColor(view, accent), width));
                details.Children.Add(BuildScheduleRow(
                    ErrorColor, "标准", BuildPeakWindowLine()));
                details.Children.Add(BuildScheduleRow(
                    SuccessColor, "半价", "其余时间"));
                return details;
            }
            if (view.Mode == "quota")
            {
                string resetText = string.IsNullOrEmpty(view.ResetText)
                    ? "接口未提供重置时间" : view.ResetText;
                TextBlock reset = PageText(8.8, palette.RowValue, resetText);
                reset.TextWrapping = TextWrapping.Wrap;
                reset.ToolTip = resetText;
                return reset;
            }
            string text = view.Sub;
            if (string.IsNullOrEmpty(text) && view.Rows.Count > 0) text = RowsInline(view);
            if (string.IsNullOrEmpty(text)) return null;
            TextBlock detail = PageText(8.8, SecondaryTextColor, text);
            detail.TextWrapping = TextWrapping.Wrap;
            detail.TextTrimming = TextTrimming.WordEllipsis;
            detail.MaxHeight = ScaledTextHeight(42);
            detail.ToolTip = text;
            return detail;
        }

        private FrameworkElement BuildCustomMetrics(BubbleView view, double width)
        {
            if (view.Rows.Count < 2) return null;
            return BuildMetricColumns(view.Rows[0], view.Rows[1], width);
        }

        private FrameworkElement BuildCustomChart(BubbleView view, double width)
        {
            if (view.Mode == "status")
                return BuildPeakTimeline(ResolveBigColor(view, accent), width);
            if (view.Mode == "trend"
                || (view.Mode == "spend" && view.Trend.Count > 0))
            {
                double height = Math.Max(32, Math.Min(78, pageHeight - 18));
                return BuildTrendBars(view, height, width);
            }
            return null;
        }

        private static void AddCustomModule(StackPanel panel,
            FrameworkElement element, bool addGap)
        {
            if (element == null) return;
            if (addGap) element.Margin = new Thickness(0, 6, 0, 0);
            panel.Children.Add(element);
        }

        private FrameworkElement BuildSpendPage(BubbleView view)
        {
            Grid page = new Grid();
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid heading = new Grid();
            double headingLabelWidth = MeasureTextWidth("今日已消耗", 9.2, FontWeights.Normal);
            double headingSourceWidth = MeasureTextWidth(view.Sub, 8.1, FontWeights.Normal);
            bool headingInline = string.IsNullOrEmpty(view.Sub)
                || headingLabelWidth + headingSourceWidth + 18 <= ContentWidth;
            if (headingInline)
            {
                heading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            }
            else
            {
                heading.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                heading.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }
            TextBlock label = PageText(9.2, palette.Muted, "今日已消耗");
            label.Margin = new Thickness(0, 0, 0, 2);
            heading.Children.Add(label);
            if (!string.IsNullOrEmpty(view.Sub))
            {
                TextBlock source = PageText(8.1, SecondaryTextColor, view.Sub);
                source.Margin = new Thickness(7, 0, 2, 2);
                source.HorizontalAlignment = HorizontalAlignment.Right;
                source.ToolTip = view.Note;
                if (headingInline)
                {
                    Grid.SetColumn(source, 1);
                }
                else
                {
                    source.HorizontalAlignment = HorizontalAlignment.Left;
                    source.Margin = new Thickness(0, 0, 0, 2);
                    Grid.SetRow(source, 1);
                }
                heading.Children.Add(source);
            }
            Grid.SetRow(heading, 0);
            page.Children.Add(heading);

            Color amountColor = ResolveBigColor(view, palette.Big);
            Viewbox amount = PageAmount(string.IsNullOrEmpty(view.Big) ? "--" : view.Big,
                30, amountColor, 44);
            Grid.SetRow(amount, 1);
            page.Children.Add(amount);

            FrameworkElement metrics;
            if (view.Rows.Count >= 2)
                metrics = BuildMetricColumns(view.Rows[0], view.Rows[1], ContentWidth);
            else
            {
                TextBlock text = PageText(9.4, palette.Muted,
                    string.IsNullOrEmpty(view.Sub) ? "暂无对比数据" : view.Sub);
                text.TextWrapping = TextWrapping.Wrap;
                text.Margin = new Thickness(0, 11, 0, 0);
                text.ToolTip = view.Sub;
                metrics = text;
            }
            metrics.Margin = new Thickness(0, 7, 0, 0);
            Grid.SetRow(metrics, 2);
            page.Children.Add(metrics);

            if (view.NoteError && !string.IsNullOrEmpty(view.Note))
            {
                FrameworkElement error = BuildErrorBand(view.Note);
                error.Margin = new Thickness(0, 6, 0, 0);
                Grid.SetRow(error, 3);
                page.Children.Add(error);
            }
            return page;
        }

        private FrameworkElement BuildDashboardSpendPage(BubbleView view)
        {
            Grid page = new Grid();
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid heading = new Grid();
            heading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            TextBlock label = PageText(9.2, palette.Muted, "今日已消耗");
            heading.Children.Add(label);
            if (!string.IsNullOrEmpty(view.Sub))
            {
                Border sourceBadge = new Border();
                sourceBadge.CornerRadius = new CornerRadius(7);
                sourceBadge.Background = new SolidColorBrush(WithAlpha(accent,
                    dark ? (byte)38 : (byte)18));
                sourceBadge.Padding = new Thickness(5, 1, 5, 1);
                sourceBadge.HorizontalAlignment = HorizontalAlignment.Right;
                sourceBadge.ToolTip = view.Sub
                    + (string.IsNullOrEmpty(view.Note) ? "" : Environment.NewLine + view.Note);
                string sourceLabel = string.Equals(view.Sub, "本机估算", StringComparison.Ordinal)
                    ? "估算" : view.Sub;
                TextBlock source = PageText(7.8, SecondaryTextColor, sourceLabel);
                source.TextTrimming = TextTrimming.None;
                sourceBadge.Child = source;
                Grid.SetColumn(sourceBadge, 1);
                heading.Children.Add(sourceBadge);
            }
            page.Children.Add(heading);

            Grid body = new Grid();
            body.Margin = new Thickness(0, 5, 0, 0);
            double heroWidth = Math.Max(100, ContentWidth * 0.44);
            double sideWidth = Math.Max(92, ContentWidth - heroWidth - 10);
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(heroWidth) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Color amountColor = ResolveBigColor(view, palette.Big);
            Viewbox amount = PageAmount(string.IsNullOrEmpty(view.Big) ? "--" : view.Big,
                30, amountColor, 44, heroWidth - 5);
            amount.HorizontalAlignment = HorizontalAlignment.Left;
            Grid.SetColumn(amount, 0);
            body.Children.Add(amount);
            if (view.Rows.Count >= 2)
            {
                FrameworkElement metrics = BuildMetricColumns(view.Rows[0], view.Rows[1], sideWidth);
                metrics.Margin = new Thickness(10, 1, 0, 0);
                Grid.SetColumn(metrics, 1);
                body.Children.Add(metrics);
            }
            Grid.SetRow(body, 1);
            page.Children.Add(body);

            if (view.Rows.Count >= 2)
            {
                double yesterday = ParseMetricNumber(view.Rows[0].Value);
                double average = ParseMetricNumber(view.Rows[1].Value);
                if (yesterday >= 0 && average > 0)
                {
                    double change = (yesterday - average) / average * 100;
                    string insight = Math.Abs(change) < 0.5
                        ? "昨日接近 7 日均"
                        : (change < 0
                            ? "昨日比 7 日均低 " + Math.Abs(Math.Round(change)).ToString("0", CultureInfo.InvariantCulture) + "%"
                            : "昨日比 7 日均高 " + Math.Round(change).ToString("0", CultureInfo.InvariantCulture) + "%");
                    Grid trend = new Grid();
                    trend.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    trend.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    Border rule = new Border();
                    rule.Width = 3;
                    rule.Height = ScaledTextHeight(14);
                    rule.CornerRadius = new CornerRadius(2);
                    rule.Background = CurrentBrush();
                    rule.Margin = new Thickness(0, 0, 7, 0);
                    trend.Children.Add(rule);
                    TextBlock text = PageText(8.6, SecondaryTextColor, insight);
                    text.VerticalAlignment = VerticalAlignment.Center;
                    Grid.SetColumn(text, 1);
                    trend.Children.Add(text);
                    trend.Margin = new Thickness(0, 7, 0, 0);
                    Grid.SetRow(trend, 2);
                    page.Children.Add(trend);
                }
            }
            return page;
        }

        private FrameworkElement BuildMetricColumns(
            KeyValuePair<string, string> left,
            KeyValuePair<string, string> right,
            double availableWidth = -1)
        {
            double leftValue = ParseMetricNumber(left.Value);
            double rightValue = ParseMetricNumber(right.Value);
            double maxValue = Math.Max(leftValue, rightValue);
            if (availableWidth <= 0) availableWidth = ContentWidth;
            availableWidth = Math.Max(52, Math.Min(ContentWidth, availableWidth));
            double gap = 12;
            double columnWidth = Math.Max(20, (availableWidth - gap) / 2);
            bool horizontal = MetricColumnFits(left, columnWidth)
                && MetricColumnFits(right, columnWidth);
            if (!horizontal && layoutVariant == "compact")
            {
                KeyValuePair<string, string> shortLeft =
                    new KeyValuePair<string, string>("昨日", left.Value);
                KeyValuePair<string, string> shortRight =
                    new KeyValuePair<string, string>("7 日均", right.Value);
                if (MetricColumnFits(shortLeft, columnWidth)
                    && MetricColumnFits(shortRight, columnWidth))
                {
                    left = shortLeft;
                    right = shortRight;
                    horizontal = true;
                }
            }
            if (!horizontal) return BuildMetricRows(left, right, maxValue, availableWidth);

            Grid metrics = new Grid();
            metrics.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            metrics.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            StackPanel first = MetricColumn(left, maxValue, columnWidth);
            first.Margin = new Thickness(0, 0, gap / 2, 0);
            Grid.SetColumn(first, 0);
            metrics.Children.Add(first);

            StackPanel second = MetricColumn(right, maxValue, columnWidth);
            second.Margin = new Thickness(gap / 2, 0, 0, 0);
            Grid.SetColumn(second, 1);
            metrics.Children.Add(second);
            return metrics;
        }

        private bool MetricColumnFits(KeyValuePair<string, string> item, double width)
        {
            double labelWidth = MeasureTextWidth(item.Key, 8.4, FontWeights.Normal);
            double valueWidth = MeasureTextWidth(item.Value, 11.3, FontWeights.SemiBold);
            return Math.Max(labelWidth, valueWidth) + 2 <= width;
        }

        private FrameworkElement BuildMetricRows(KeyValuePair<string, string> left,
            KeyValuePair<string, string> right, double maxValue, double availableWidth)
        {
            StackPanel rows = new StackPanel();
            AddMetricRow(rows, left, maxValue, availableWidth, false);
            AddMetricRow(rows, right, maxValue, availableWidth, true);
            return rows;
        }

        private void AddMetricRow(StackPanel parent, KeyValuePair<string, string> item,
            double maxValue, double availableWidth, bool addTopGap)
        {
            StackPanel row = new StackPanel();
            if (addTopGap) row.Margin = new Thickness(0, 5, 0, 0);
            TextBlock label = PageText(8.4, palette.Muted, item.Key);
            label.ToolTip = item.Key;
            row.Children.Add(label);
            FrameworkElement value = MetricValueBlock(item.Value, availableWidth,
                HorizontalAlignment.Left);
            value.Margin = new Thickness(0, 2, 0, 0);
            row.Children.Add(value);
            Grid bar = BuildMetricBar(item.Value, maxValue);
            if (bar != null)
            {
                bar.Margin = new Thickness(0, 4, 0, 0);
                row.Children.Add(bar);
            }
            parent.Children.Add(row);
        }

        private StackPanel MetricColumn(KeyValuePair<string, string> item,
            double maxValue, double maxWidth)
        {
            StackPanel column = new StackPanel();
            TextBlock label = PageText(8.4, palette.Muted, item.Key);
            label.ToolTip = item.Key;
            FrameworkElement value = MetricValueBlock(item.Value, maxWidth,
                HorizontalAlignment.Left);
            value.Margin = new Thickness(0, 2, 0, 0);
            column.Children.Add(label);
            column.Children.Add(value);
            Grid bar = BuildMetricBar(item.Value, maxValue);
            if (bar != null)
            {
                bar.Margin = new Thickness(0, 4, 2, 0);
                column.Children.Add(bar);
            }
            return column;
        }

        private FrameworkElement MetricValueBlock(string text, double maxWidth,
            HorizontalAlignment alignment)
        {
            TextBlock block = PageText(11.3, palette.RowValue, text);
            block.FontWeight = FontWeights.SemiBold;
            block.TextWrapping = TextWrapping.NoWrap;
            block.TextTrimming = TextTrimming.None;
            block.HorizontalAlignment = alignment;
            block.ToolTip = text;
            double measured = MeasureTextWidth(text, 11.3, FontWeights.SemiBold);
            if (measured <= maxWidth) return block;

            Viewbox fit = new Viewbox();
            fit.Width = Math.Max(20, maxWidth);
            fit.Height = ScaledTextHeight(16);
            fit.Stretch = Stretch.Uniform;
            fit.StretchDirection = StretchDirection.DownOnly;
            fit.HorizontalAlignment = alignment;
            fit.Child = block;
            fit.ToolTip = text;
            return fit;
        }

        private Grid BuildMetricBar(string value, double maxValue)
        {
            double numeric = ParseMetricNumber(value);
            if (numeric < 0 || maxValue <= 0) return null;
            Grid bar = new Grid();
            bar.Height = Math.Max(3, ScaledTextHeight(3));
            bar.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(Math.Max(0.1, numeric), GridUnitType.Star)
            });
            bar.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(Math.Max(0.1, maxValue - numeric), GridUnitType.Star)
            });
            Border fill = new Border();
            fill.CornerRadius = new CornerRadius(1);
            fill.Background = CurrentBrush();
            accentAppliers.Add(delegate(Brush brush) { fill.Background = brush; });
            bar.Children.Add(fill);
            Border rest = new Border();
            rest.CornerRadius = new CornerRadius(1);
            rest.Background = new SolidColorBrush(WithAlpha(palette.BarEmpty,
                dark ? (byte)170 : (byte)210));
            Grid.SetColumn(rest, 1);
            bar.Children.Add(rest);
            return bar;
        }

        private static double ParseMetricNumber(string value)
        {
            if (string.IsNullOrEmpty(value)) return -1;
            StringBuilder number = new StringBuilder();
            bool started = false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsDigit(c) || (c == '.' && started))
                {
                    number.Append(c);
                    started = true;
                }
                else if (started)
                {
                    break;
                }
            }
            double parsed;
            return number.Length > 0
                && double.TryParse(number.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out parsed)
                ? parsed : -1;
        }

        private string TrendTotalText(BubbleView view)
        {
            int days;
            double total;
            double average;
            TrendTotals(view, out days, out total, out average);
            return Fmt.Money(total, view.TrendCurrency);
        }

        private Grid BuildQuotaProgress(BubbleView view, Color color, double height)
        {
            return BuildQuotaProgress(view, color, height, ContentWidth);
        }

        private FrameworkElement BuildQuotaRing(BubbleView view, Color color, double size)
        {
            double percent = QuotaPercent(view);
            Grid ring = new Grid();
            ring.Width = size;
            ring.Height = size;
            ring.HorizontalAlignment = HorizontalAlignment.Center;
            double ringSize = Math.Max(12, size - 8);
            Ellipse track = new Ellipse();
            track.Width = ringSize;
            track.Height = ringSize;
            track.StrokeThickness = 8;
            track.HorizontalAlignment = HorizontalAlignment.Center;
            track.VerticalAlignment = VerticalAlignment.Center;
            track.Stroke = new SolidColorBrush(WithAlpha(palette.BarEmpty,
                dark ? (byte)180 : (byte)220));
            ring.Children.Add(track);

            if (percent > 0)
            {
                double radius = ringSize / 2 - 4;
                double center = size / 2;
                double endAngle = -90 + 360 * percent / 100.0;
                Point start = new Point(
                    center + radius * Math.Cos(-90 * Math.PI / 180),
                    center + radius * Math.Sin(-90 * Math.PI / 180));
                Point end = new Point(
                    center + radius * Math.Cos(endAngle * Math.PI / 180),
                    center + radius * Math.Sin(endAngle * Math.PI / 180));
                StreamGeometry geometry = new StreamGeometry();
                using (StreamGeometryContext context = geometry.Open())
                {
                    context.BeginFigure(start, false, false);
                    context.ArcTo(end, new Size(radius, radius), 0,
                        percent > 50, SweepDirection.Clockwise, true, false);
                }
                Path arc = new Path();
                arc.Data = geometry;
                arc.Stroke = new SolidColorBrush(color);
                arc.StrokeThickness = 8;
                arc.StrokeStartLineCap = PenLineCap.Round;
                arc.StrokeEndLineCap = PenLineCap.Round;
                ring.Children.Add(arc);
            }

            TextBlock value = PageText(Math.Max(14, size * 0.24), color,
                percent.ToString("0.#", CultureInfo.InvariantCulture) + "%");
            value.FontWeight = FontWeights.Bold;
            value.HorizontalAlignment = HorizontalAlignment.Center;
            value.VerticalAlignment = VerticalAlignment.Center;
            value.TextAlignment = TextAlignment.Center;
            ring.Children.Add(value);
            ring.ToolTip = string.IsNullOrEmpty(view.Sub) ? view.Big : view.Sub;
            return ring;
        }

        private static double QuotaPercent(BubbleView view)
        {
            double percent = 0;
            string numeric = (view.Big ?? "").Replace("%", "");
            double.TryParse(numeric, NumberStyles.Float, CultureInfo.InvariantCulture, out percent);
            if (percent < 0) percent = 0;
            if (percent > 100) percent = 100;
            return percent;
        }

        private Grid BuildQuotaProgress(BubbleView view, Color color, double height, double width)
        {
            double percent = QuotaPercent(view);
            Grid progress = new Grid();
            Border track = new Border();
            track.Height = height;
            track.CornerRadius = new CornerRadius(height / 2);
            track.Background = new SolidColorBrush(WithAlpha(palette.BarEmpty,
                dark ? (byte)190 : (byte)225));
            progress.Children.Add(track);
            Border fill = new Border();
            fill.Height = height;
            fill.CornerRadius = new CornerRadius(height / 2);
            fill.HorizontalAlignment = HorizontalAlignment.Left;
            fill.Background = new SolidColorBrush(color);
            fill.Width = Math.Max(height, width * percent / 100.0);
            progress.Children.Add(fill);
            return progress;
        }

        private FrameworkElement BuildTrendPage(BubbleView view)
        {
            int trendDays;
            double trendTotal;
            double trendAverage;
            TrendTotals(view, out trendDays, out trendTotal, out trendAverage);
            Grid page = new Grid();
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid summary = new Grid();
            summary.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            double averageColumnWidth = 80 + Math.Max(0, fontScale - 1) * 26;
            summary.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(averageColumnWidth) });
            summary.Margin = new Thickness(0, 0, 3, 0);

            StackPanel total = new StackPanel();
            TextBlock totalLabel = PageText(8.8, palette.Muted, "区间合计");
            Viewbox totalValue = PageAmount(Fmt.Money(trendTotal, view.TrendCurrency),
                22, palette.Big, 30);
            totalValue.Width = double.NaN;
            totalValue.HorizontalAlignment = HorizontalAlignment.Left;
            total.Children.Add(totalLabel);
            total.Children.Add(totalValue);
            summary.Children.Add(total);

            StackPanel average = new StackPanel();
            average.HorizontalAlignment = HorizontalAlignment.Right;
            average.Margin = new Thickness(6, 0, 0, 0);
            TextBlock averageLabel = PageText(8.2, palette.Muted, "日均");
            averageLabel.HorizontalAlignment = HorizontalAlignment.Right;
            string averageText = Fmt.Money(trendAverage, view.TrendCurrency);
            TextBlock averageValue = PageText(9.1, palette.RowValue, averageText);
            averageValue.TextAlignment = TextAlignment.Right;
            averageValue.ToolTip = "日均 " + averageText
                + (view.TrendExact ? " · 官方数据" : " · 本机统计");
            Viewbox averageBox = new Viewbox();
            averageBox.Width = averageColumnWidth - 12;
            averageBox.Height = ScaledTextHeight(15);
            averageBox.HorizontalAlignment = HorizontalAlignment.Right;
            averageBox.Stretch = Stretch.Uniform;
            averageBox.StretchDirection = StretchDirection.DownOnly;
            averageBox.Child = averageValue;
            average.Children.Add(averageLabel);
            average.Children.Add(averageBox);
            Grid.SetColumn(average, 1);
            summary.Children.Add(average);
            Grid.SetRow(summary, 0);
            page.Children.Add(summary);

            FrameworkElement chart = BuildTrendBars(view);
            chart.Margin = new Thickness(0, 5, 0, 2);
            chart.VerticalAlignment = VerticalAlignment.Top;
            chart.Cursor = System.Windows.Input.Cursors.Hand;
            chart.ToolTip = "点击切换近 7 / 14 / 30 天统计";
            chart.MouseLeftButtonDown += delegate(object sender, System.Windows.Input.MouseButtonEventArgs e)
            {
                CycleTrendRange();
                e.Handled = true;
            };
            Grid.SetRow(chart, 1);
            page.Children.Add(chart);

            Grid axis = new Grid();
            axis.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            axis.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            TextBlock start = PageText(7.8, SecondaryTextColor, "最早");
            TextBlock end = PageText(7.8, SecondaryTextColor, "今天");
            start.Margin = new Thickness(2, 0, 0, 0);
            end.Margin = new Thickness(0, 0, 5, 0);
            Grid.SetColumn(end, 1);
            axis.Children.Add(start);
            axis.Children.Add(end);
            axis.Margin = new Thickness(0, 4, 4, 0);
            Grid.SetRow(axis, 2);
            page.Children.Add(axis);
            return page;
        }

        private void TrendTotals(BubbleView view, out int days, out double total, out double average)
        {
            days = config == null ? 7 : (int)config.GetNumberAt("bubble.trendDays", 7);
            if (days != 14 && days != 30) days = 7;
            Dictionary<string, double> byDay = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (DayUsage item in view.Trend)
            {
                if (item != null && !string.IsNullOrEmpty(item.Day)) byDay[item.Day] = item.Usage;
            }
            total = 0;
            DateTime today = DataService.BeijingNow().Date;
            for (int offset = days - 1; offset >= 0; offset--)
            {
                double value;
                if (byDay.TryGetValue(today.AddDays(-offset).ToString("yyyy-MM-dd",
                        CultureInfo.InvariantCulture), out value))
                    total += value;
            }
            average = days <= 0 ? 0 : total / days;
        }

        private FrameworkElement BuildTrendBars(BubbleView view)
        {
            return BuildTrendBars(view,
                Math.Max(28, Math.Min(64, pageHeight - 70)),
                ContentWidth);
        }

        private FrameworkElement BuildTrendBars(BubbleView view, double height)
        {
            return BuildTrendBars(view, height, ContentWidth);
        }

        private FrameworkElement BuildTrendBars(BubbleView view, double height, double width)
        {
            int days = config == null ? 7 : (int)config.GetNumberAt("bubble.trendDays", 7);
            if (days != 14 && days != 30) days = 7;
            Dictionary<string, double> byDay = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (DayUsage item in view.Trend)
            {
                if (item != null && !string.IsNullOrEmpty(item.Day)) byDay[item.Day] = item.Usage;
            }

            List<double> values = new List<double>();
            DateTime today = DataService.BeijingNow().Date;
            double max = 0;
            for (int offset = days - 1; offset >= 0; offset--)
            {
                double value;
                byDay.TryGetValue(today.AddDays(-offset).ToString("yyyy-MM-dd",
                    CultureInfo.InvariantCulture), out value);
                if (value < 0) value = 0;
                values.Add(value);
                if (value > max) max = value;
            }

            Canvas chart = new Canvas();
            chart.Width = width;
            chart.Height = Math.Max(24, height);
            chart.ClipToBounds = true;

            for (int i = 1; i <= 3; i++)
            {
                Border gridLine = new Border();
                gridLine.Background = new SolidColorBrush(WithAlpha(palette.Border, 40));
                gridLine.Height = 1;
                gridLine.Width = chart.Width;
                Canvas.SetTop(gridLine, chart.Height * i / 4.0);
                chart.Children.Add(gridLine);
            }

            double gap = days <= 7 ? 6 : (days <= 14 ? 3 : 1.5);
            double barWidth = Math.Max(2.5, (chart.Width - gap * (days + 1)) / days);
            double drawable = Math.Max(12, chart.Height - 9);
            for (int i = 0; i < values.Count; i++)
            {
                double barHeight = max <= 0 ? 1.5 : Math.Max(1.5, values[i] / max * drawable);
                Border bar = new Border();
                bar.Width = barWidth;
                bar.Height = barHeight;
                bar.CornerRadius = new CornerRadius(Math.Min(3, barWidth / 2));
                Color barColor = i == values.Count - 1
                    ? accent : WithAlpha(accent, dark ? (byte)145 : (byte)105);
                bar.Background = new SolidColorBrush(barColor);
                bar.ToolTip = today.AddDays(i - values.Count + 1).ToString("MM-dd",
                    CultureInfo.InvariantCulture) + "  " + Fmt.Money(values[i], view.TrendCurrency);
                Canvas.SetLeft(bar, gap + i * (barWidth + gap));
                Canvas.SetTop(bar, chart.Height - barHeight - 8);
                chart.Children.Add(bar);
            }

            return chart;
        }

        private FrameworkElement BuildPeakPage(BubbleView view)
        {
            Grid page = new Grid();
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            Color statusColor = ResolveBigColor(view, accent);
            Grid statusTop = new Grid();
            statusTop.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(0.9, GridUnitType.Star)
            });
            statusTop.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(1.4, GridUnitType.Star)
            });
            TextBlock currentLabel = PageText(8.6, palette.Muted, "当前计价");
            currentLabel.VerticalAlignment = VerticalAlignment.Center;
            statusTop.Children.Add(currentLabel);
            Border countdown = BuildCountdownPill(view.Sub, statusColor,
                double.NaN, Math.Max(20, ScaledTextHeight(20)));
            countdown.HorizontalAlignment = HorizontalAlignment.Stretch;
            countdown.Margin = new Thickness(8, 0, 4, 0);
            Grid.SetColumn(countdown, 1);
            statusTop.Children.Add(countdown);
            page.Children.Add(statusTop);

            TextBlock currentValue = PageText(20, statusColor,
                string.IsNullOrEmpty(view.Big) ? "--" : view.Big);
            currentValue.FontWeight = FontWeights.SemiBold;
            currentValue.Margin = new Thickness(0, 2, 0, 0);
            Grid.SetRow(currentValue, 1);
            page.Children.Add(currentValue);

            Grid timeline = BuildPeakTimeline(statusColor);
            timeline.Margin = new Thickness(0, 4, 0, 0);
            Grid.SetRow(timeline, 2);
            page.Children.Add(timeline);

            StackPanel schedule = new StackPanel();
            schedule.Margin = new Thickness(0, 4, 0, 0);
            schedule.Children.Add(BuildScheduleRow(
                ErrorColor,
                "标准",
                BuildPeakWindowLine()));
            schedule.Children.Add(BuildScheduleRow(
                SuccessColor,
                "半价",
                "其余时间"));
            Grid.SetRow(schedule, 3);
            page.Children.Add(schedule);
            return page;
        }

        private Border BuildCountdownPill(string text, Color statusColor,
            double width, double height)
        {
            Border pill = new Border();
            if (!double.IsNaN(width) && width > 0) pill.Width = width;
            pill.Height = height;
            pill.CornerRadius = new CornerRadius(height / 2);
            pill.Background = new SolidColorBrush(WithAlpha(statusColor,
                dark ? (byte)46 : (byte)20));
            pill.BorderBrush = new SolidColorBrush(WithAlpha(statusColor,
                dark ? (byte)78 : (byte)38));
            pill.BorderThickness = new Thickness(1);
            pill.Padding = new Thickness(7, 2, 7, 2);
            TextBlock value = PageText(8.8, palette.RowValue,
                string.IsNullOrEmpty(text) ? "-- 后切换" : text + " 后切换");
            value.FontWeight = FontWeights.SemiBold;
            value.VerticalAlignment = VerticalAlignment.Center;
            value.ToolTip = value.Text;
            Viewbox fit = new Viewbox();
            fit.Stretch = Stretch.Uniform;
            fit.StretchDirection = StretchDirection.DownOnly;
            fit.Child = value;
            fit.VerticalAlignment = VerticalAlignment.Center;
            pill.Child = fit;
            return pill;
        }

        private double CountdownPillWidth(string text, double maxWidth)
        {
            string fullText = (string.IsNullOrEmpty(text) ? "--" : text) + " 后切换";
            double measured = MeasureTextWidth(fullText, 8.8, FontWeights.SemiBold) + 18;
            return Math.Max(72, Math.Min(maxWidth, measured));
        }

        private Grid BuildPeakTimeline(Color statusColor)
        {
            return BuildPeakTimeline(statusColor, ContentWidth);
        }

        private Grid BuildPeakTimeline(Color statusColor, double width)
        {
            Grid area = new Grid();
            area.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            area.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Canvas rail = new Canvas();
            rail.Width = width;
            rail.Height = 14;
            Border track = new Border();
            track.Width = rail.Width;
            track.Height = 6;
            track.CornerRadius = new CornerRadius(3);
            track.Background = new SolidColorBrush(WithAlpha(palette.BarEmpty,
                dark ? (byte)190 : (byte)220));
            Canvas.SetTop(track, 4);
            rail.Children.Add(track);

            for (int i = 0; i < DataService.PeakWindowCount; i++)
            {
                int startHour = DataService.PeakWindowStart(i);
                int endHour = DataService.PeakWindowEnd(i);
                Border segment = new Border();
                segment.Height = 6;
                segment.Width = rail.Width * (endHour - startHour) / 24.0;
                segment.CornerRadius = new CornerRadius(3);
                segment.Background = new SolidColorBrush(WithAlpha(statusColor, 220));
                Canvas.SetLeft(segment, rail.Width * startHour / 24.0);
                Canvas.SetTop(segment, 4);
                rail.Children.Add(segment);
            }

            DateTime now = DataService.BeijingNow();
            double markerX = rail.Width * (now.Hour + now.Minute / 60.0) / 24.0;
            Border marker = new Border();
            marker.Width = 2;
            marker.Height = 13;
            marker.CornerRadius = new CornerRadius(1);
            marker.Background = new SolidColorBrush(palette.Big);
            Canvas.SetLeft(marker, Math.Max(1, Math.Min(rail.Width - 3, markerX - 1)));
            Canvas.SetTop(marker, 0);
            rail.Children.Add(marker);
            Grid.SetRow(rail, 0);
            area.Children.Add(rail);

            Grid labels = new Grid();
            labels.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            labels.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            labels.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            TextBlock start = PageText(7.3, SecondaryTextColor, "00");
            TextBlock noon = PageText(7.3, SecondaryTextColor, "12");
            noon.HorizontalAlignment = HorizontalAlignment.Center;
            TextBlock end = PageText(7.3, SecondaryTextColor, "24");
            end.HorizontalAlignment = HorizontalAlignment.Right;
            end.Margin = new Thickness(0, 0, 4, 0);
            Grid.SetColumn(noon, 1);
            Grid.SetColumn(end, 2);
            labels.Children.Add(start);
            labels.Children.Add(noon);
            labels.Children.Add(end);
            labels.Margin = new Thickness(0, -2, 0, 0);
            Grid.SetRow(labels, 1);
            area.Children.Add(labels);
            return area;
        }

        private static string BuildPeakWindowLine()
        {
            StringBuilder result = new StringBuilder();
            for (int i = 0; i < DataService.PeakWindowCount; i++)
            {
                if (i > 0) result.Append(" · ");
                result.Append(DataService.PeakWindowStart(i).ToString("00", CultureInfo.InvariantCulture));
                result.Append("-");
                result.Append(DataService.PeakWindowEnd(i).ToString("00", CultureInfo.InvariantCulture));
            }
            return result.ToString();
        }

        private Grid BuildScheduleRow(Color dotColor, string labelText, string valueText)
        {
            Grid row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Ellipse dot = new Ellipse();
            dot.Width = 5;
            dot.Height = 5;
            dot.Fill = new SolidColorBrush(dotColor);
            dot.VerticalAlignment = VerticalAlignment.Center;
            dot.Margin = new Thickness(0, 1, 7, 0);
            row.Children.Add(dot);

            Grid copy = new Grid();
            copy.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(35) });
            copy.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            TextBlock label = PageText(8.4, palette.Muted, labelText);
            label.VerticalAlignment = VerticalAlignment.Center;
            TextBlock value = PageText(9.6, palette.RowValue, valueText);
            value.FontWeight = FontWeights.SemiBold;
            value.VerticalAlignment = VerticalAlignment.Center;
            value.ToolTip = valueText;
            Grid.SetColumn(value, 1);
            copy.Children.Add(label);
            copy.Children.Add(value);
            Grid.SetColumn(copy, 1);
            row.Children.Add(copy);
            row.Margin = new Thickness(0, 1, 0, 1);
            return row;
        }

        private FrameworkElement BuildProviderPage(BubbleView view)
        {
            Grid page = new Grid();
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            TextBlock label = PageText(9.2, palette.Muted, "接口余额");
            Grid.SetRow(label, 0);
            page.Children.Add(label);

            Color amountColor = ResolveBigColor(view,
                view.NoteError ? ErrorColor : palette.Big);
            Viewbox amount = PageAmount(string.IsNullOrEmpty(view.Big) ? "--" : view.Big,
                27, amountColor, 41);
            amount.Margin = new Thickness(0, 2, 0, 0);
            Grid.SetRow(amount, 1);
            page.Children.Add(amount);

            StackPanel detail = new StackPanel();
            detail.Margin = new Thickness(0, 7, 0, 0);
            if (view.NoteError && !string.IsNullOrEmpty(view.Note))
            {
                detail.Children.Add(BuildErrorBand(view.Note));
            }
            else
            {
                Grid state = new Grid();
                state.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                state.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                Ellipse dot = new Ellipse();
                dot.Width = 5;
                dot.Height = 5;
                dot.Fill = new SolidColorBrush(SuccessColor);
                dot.VerticalAlignment = VerticalAlignment.Center;
                dot.Margin = new Thickness(0, 1, 7, 0);
                state.Children.Add(dot);
                TextBlock text = PageText(9.2, palette.Muted,
                    string.IsNullOrEmpty(view.Sub) ? "接口正常" : view.Sub);
                text.ToolTip = view.Sub;
                Grid.SetColumn(text, 1);
                state.Children.Add(text);
                detail.Children.Add(state);
            }
            Grid.SetRow(detail, 2);
            page.Children.Add(detail);
            return page;
        }

        private FrameworkElement BuildQuotaPage(BubbleView view)
        {
            Grid page = new Grid();
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Color amountColor = ResolveBigColor(view, palette.Big);
            FrameworkElement ring = BuildQuotaRing(view, amountColor,
                Math.Min(72, Math.Max(54, pageHeight - 52)));
            ring.VerticalAlignment = VerticalAlignment.Center;
            page.Children.Add(ring);

            string detailText = view.Sub;
            string normalizedPercent = view.Big == null ? "" : view.Big.Replace(" ", "");
            if (!string.IsNullOrEmpty(detailText)
                && detailText.Length <= normalizedPercent.Length + 4
                && detailText.IndexOf("%", StringComparison.Ordinal) >= 0)
            {
                detailText = "";
            }
            string resetText = string.IsNullOrEmpty(view.ResetText) ? "接口未提供重置时间" : view.ResetText;
            Border resetBadge = new Border();
            resetBadge.CornerRadius = new CornerRadius(9);
            resetBadge.Background = new SolidColorBrush(WithAlpha(amountColor,
                dark ? (byte)40 : (byte)20));
            resetBadge.Padding = new Thickness(9, 3, 9, 3);
            resetBadge.HorizontalAlignment = HorizontalAlignment.Center;
            resetBadge.Margin = new Thickness(0, 5, 0, 0);
            TextBlock resetValue = PageText(8.7, palette.RowValue, resetText);
            resetValue.FontWeight = FontWeights.SemiBold;
            resetValue.TextAlignment = TextAlignment.Center;
            resetValue.ToolTip = resetText;
            resetBadge.Child = resetValue;
            Grid.SetRow(resetBadge, 1);
            page.Children.Add(resetBadge);

            if (!string.IsNullOrEmpty(detailText))
            {
                TextBlock detail = PageText(8.2, SecondaryTextColor, detailText);
                detail.TextAlignment = TextAlignment.Center;
                detail.TextTrimming = TextTrimming.CharacterEllipsis;
                detail.Margin = new Thickness(0, 5, 0, 0);
                detail.ToolTip = detailText;
                Grid.SetRow(detail, 2);
                page.Children.Add(detail);
            }
            if (view.NoteError && !string.IsNullOrEmpty(view.Note))
            {
                FrameworkElement error = BuildErrorBand(view.Note);
                error.Margin = new Thickness(0, 6, 0, 0);
                Grid.SetRow(error, 2);
                page.Children.Add(error);
            }
            return page;
        }

        private FrameworkElement BuildMessagePage(BubbleView view)
        {
            Grid page = new Grid();
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            TextBlock quote = PageText(32, WithAlpha(accent, dark ? (byte)95 : (byte)58), "“");
            quote.FontFamily = new FontFamily("Georgia");
            quote.HorizontalAlignment = HorizontalAlignment.Left;
            quote.VerticalAlignment = VerticalAlignment.Top;
            quote.Margin = new Thickness(1, -8, 0, 0);
            page.Children.Add(quote);

            TextBlock message = PageText(13.4, palette.Big,
                string.IsNullOrEmpty(view.Quip) ? view.Big : view.Quip);
            message.TextWrapping = TextWrapping.Wrap;
            message.TextAlignment = TextAlignment.Center;
            message.TextTrimming = TextTrimming.WordEllipsis;
            message.MaxHeight = Math.Max(ScaledTextHeight(36),
                layout.ContentHeight - ScaledTextHeight(30));
            message.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetRow(message, 1);
            page.Children.Add(message);
            return page;
        }

        private FrameworkElement BuildAlertPage(BubbleView view)
        {
            Grid page = new Grid();
            StackPanel content = new StackPanel();
            content.VerticalAlignment = VerticalAlignment.Center;
            page.Children.Add(content);

            bool budgetAlert = view.AlertUsed.HasValue && view.AlertLimit.HasValue
                && view.AlertLimit.Value > 0;
            Border status = new Border();
            status.CornerRadius = new CornerRadius(9);
            status.Background = new SolidColorBrush(WithAlpha(ErrorColor, dark ? (byte)46 : (byte)20));
            status.Padding = new Thickness(8, 3, 9, 3);
            status.HorizontalAlignment = HorizontalAlignment.Left;
            StackPanel statusLine = new StackPanel();
            statusLine.Orientation = Orientation.Horizontal;
            Border statusIcon = new Border();
            double iconSize = Math.Max(14, ScaledTextHeight(14));
            statusIcon.Width = iconSize;
            statusIcon.Height = iconSize;
            statusIcon.CornerRadius = new CornerRadius(iconSize / 2);
            statusIcon.Background = new SolidColorBrush(ErrorColor);
            statusIcon.Margin = new Thickness(0, 0, 6, 0);
            TextBlock mark = PageText(8.6, Colors.White, "!");
            mark.FontWeight = FontWeights.Bold;
            mark.TextAlignment = TextAlignment.Center;
            mark.VerticalAlignment = VerticalAlignment.Center;
            statusIcon.Child = mark;
            statusLine.Children.Add(statusIcon);
            TextBlock statusText = PageText(8.6, ErrorColor,
                budgetAlert ? "预算已超线" : "需要关注");
            statusText.FontWeight = FontWeights.SemiBold;
            statusText.VerticalAlignment = VerticalAlignment.Center;
            statusLine.Children.Add(statusText);
            status.Child = statusLine;
            content.Children.Add(status);

            Viewbox amount = PageAmount(
                string.IsNullOrEmpty(view.Big) ? "出现异常" : view.Big,
                24, ErrorColor, 38);
            amount.HorizontalAlignment = HorizontalAlignment.Left;
            amount.Margin = new Thickness(0, 6, 0, 0);
            content.Children.Add(amount);

            if (budgetAlert)
            {
                double used = view.AlertUsed.Value;
                double limit = view.AlertLimit.Value;
                double actualRatio = Math.Max(0, used / limit);
                double ratio = Math.Min(1, actualRatio);
                Grid budgetMeta = new Grid();
                budgetMeta.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                budgetMeta.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                TextBlock budgetLabel = PageText(8.4, SecondaryTextColor, "预算上限");
                budgetMeta.Children.Add(budgetLabel);
                TextBlock budgetValue = PageText(9.4, palette.RowValue,
                    Fmt.Money(limit, "CNY"));
                budgetValue.FontWeight = FontWeights.SemiBold;
                budgetValue.HorizontalAlignment = HorizontalAlignment.Right;
                Grid.SetColumn(budgetValue, 1);
                budgetMeta.Children.Add(budgetValue);
                budgetMeta.Margin = new Thickness(0, 8, 0, 0);
                content.Children.Add(budgetMeta);

                Grid progress = new Grid();
                progress.Height = Math.Max(4, ScaledTextHeight(4));
                progress.Margin = new Thickness(0, 5, 0, 0);
                Border track = new Border();
                track.CornerRadius = new CornerRadius(progress.Height / 2);
                track.Background = new SolidColorBrush(WithAlpha(ErrorColor, dark ? (byte)54 : (byte)26));
                progress.Children.Add(track);
                Grid fill = new Grid();
                fill.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = new GridLength(Math.Max(0.01, ratio), GridUnitType.Star)
                });
                fill.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = new GridLength(Math.Max(0.0001, 1 - ratio), GridUnitType.Star)
                });
                Border fillBar = new Border();
                fillBar.CornerRadius = new CornerRadius(progress.Height / 2);
                fillBar.Background = new SolidColorBrush(ErrorColor);
                fill.Children.Add(fillBar);
                progress.Children.Add(fill);
                content.Children.Add(progress);

                Grid detail = new Grid();
                detail.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                detail.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                TextBlock over = PageText(8.8, ErrorColor,
                    "超出 " + Fmt.Money(Math.Max(0, used - limit), "CNY"));
                over.FontWeight = FontWeights.SemiBold;
                detail.Children.Add(over);
                TextBlock percent = PageText(8.4, SecondaryTextColor,
                    Math.Round(actualRatio * 100).ToString("0", CultureInfo.InvariantCulture) + "%");
                percent.HorizontalAlignment = HorizontalAlignment.Right;
                Grid.SetColumn(percent, 1);
                detail.Children.Add(percent);
                detail.Margin = new Thickness(0, 4, 0, 0);
                content.Children.Add(detail);
            }
            else if (!string.IsNullOrEmpty(view.Note))
            {
                FrameworkElement note = BuildErrorBand(view.Note);
                note.Margin = new Thickness(0, 8, 0, 0);
                content.Children.Add(note);
            }
            return page;
        }

        private FrameworkElement BuildFallbackPage(BubbleView view)
        {
            Grid page = new Grid();
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            TextBlock label = PageText(9, palette.Muted, "当前状态");
            page.Children.Add(label);
            Color amountColor = ResolveBigColor(view, palette.Big);
            Viewbox amount = PageAmount(string.IsNullOrEmpty(view.Big) ? view.Title : view.Big,
                24, amountColor, 40);
            amount.Margin = new Thickness(0, 3, 0, 0);
            Grid.SetRow(amount, 1);
            page.Children.Add(amount);
            if (!string.IsNullOrEmpty(view.Sub))
            {
                TextBlock detail = PageText(9.2, palette.Muted, view.Sub);
                detail.TextWrapping = TextWrapping.Wrap;
                detail.Margin = new Thickness(0, 7, 0, 0);
                Grid.SetRow(detail, 2);
                page.Children.Add(detail);
            }
            return page;
        }

        private FrameworkElement BuildErrorBand(string message)
        {
            Color error = ErrorColor;
            Border band = new Border();
            band.CornerRadius = new CornerRadius(6);
            band.Background = new SolidColorBrush(WithAlpha(error, dark ? (byte)42 : (byte)24));
            band.Padding = new Thickness(7, 5, 7, 5);
            TextBlock text = PageText(8.8, error, message);
            text.TextWrapping = TextWrapping.Wrap;
            text.TextTrimming = TextTrimming.WordEllipsis;
            text.MaxHeight = ScaledTextHeight(30);
            text.ToolTip = message;
            band.Child = text;
            return band;
        }

        private static string PaletteHex(Color color)
        {
            return string.Format("#{0:X2}{1:X2}{2:X2}", color.R, color.G, color.B);
        }

        private void UpdateDots(int index, int count)
        {
            dockDots.Children.Clear();
            if (index < 0 || count <= 1) return;
            if (count > 6)
            {
                TextBlock pager = new TextBlock();
                pager.Text = (index + 1).ToString(CultureInfo.InvariantCulture) + " / "
                    + count.ToString(CultureInfo.InvariantCulture);
                pager.FontSize = 8.5;
                pager.Foreground = new SolidColorBrush(SecondaryTextColor);
                pager.Width = 32;
                pager.TextAlignment = TextAlignment.Right;
                pager.VerticalAlignment = VerticalAlignment.Center;
                dockDots.Children.Add(pager);
                return;
            }
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
            if (string.IsNullOrEmpty(peakText) || viewMode != "balance")
            {
                badge.Visibility = Visibility.Collapsed;
                return;
            }
            badge.Visibility = Visibility.Visible;
            badgeText.Text = peakText;
            badge.ToolTip = peakText;
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

        private void CycleTrendRange()
        {
            if (config == null) return;
            int days = (int)config.GetNumberAt("bubble.trendDays", 7);
            int next = days <= 7 ? 14 : (days <= 14 ? 30 : 7);
            config.SetAt("bubble.trendDays", (double)next);
            config.Save();
            if (lastView != null && viewMode == "trend")
            {
                lastView.Title = "近 " + next.ToString(CultureInfo.InvariantCulture) + " 天趋势";
                pageHost.Content = BuildPageContent(lastView);
                title.Text = lastView.Title;
            }
        }

        private void UpdateTrend(List<DayUsage> history, string currency, bool exact)
        {
            int days = config == null ? 7 : (int)config.GetNumberAt("bubble.trendDays", 7);
            if (days != 14 && days != 30) days = 7;
            Dictionary<string, double> byDay = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (DayUsage item in history)
            {
                if (item != null && !string.IsNullOrEmpty(item.Day)) byDay[item.Day] = item.Usage;
            }

            List<DayUsage> window = new List<DayUsage>();
            DateTime todayDate = DataService.BeijingNow().Date;
            for (int offset = days - 1; offset >= 0; offset--)
            {
                DayUsage item = new DayUsage();
                item.Day = todayDate.AddDays(-offset).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                double usage;
                item.Usage = byDay.TryGetValue(item.Day, out usage) ? usage : 0;
                window.Add(item);
            }

            double max = 0;
            double total = 0;
            foreach (DayUsage item in window)
            {
                if (item.Usage > max) max = item.Usage;
                total += item.Usage;
            }

            trendCanvas.Children.Clear();
            trendHits.Children.Clear();
            trendHits.Columns = 1;
            trendHoverIndex = -1;
            trendHoverScreenValid = false;
            double chartWidth = layout == null ? 196 : layout.ContentWidth;
            if (chartWidth < 140) chartWidth = 140;
            if (chartWidth > 336) chartWidth = 336;
            trend.Width = chartWidth;
            trendCanvas.Width = chartWidth;
            trendHits.Width = chartWidth;
            trendSummary.Width = chartWidth;
            double chartHeight = trend.Height;
            double leftPad = 4;
            double rightPad = 4;
            double topPad = 7;
            double bottomPad = 5;
            double baselineY = chartHeight - bottomPad;

            Line baseline = new Line();
            baseline.X1 = leftPad;
            baseline.X2 = chartWidth - rightPad;
            baseline.Y1 = baselineY;
            baseline.Y2 = baselineY;
            baseline.Stroke = new SolidColorBrush(palette.BarEmpty);
            baseline.StrokeThickness = 1;
            trendCanvas.Children.Add(baseline);

            PointCollection points = new PointCollection();
            double usableHeight = Math.Max(1, chartHeight - topPad - bottomPad);
            double stepX = days <= 1 ? 0 : (chartWidth - leftPad - rightPad) / (days - 1);
            for (int i = 0; i < window.Count; i++)
            {
                double usage = window[i].Usage;
                double x = leftPad + stepX * i;
                double y = max > 0 && usage > 0
                    ? baselineY - Math.Max(1, usage / max * usableHeight)
                    : baselineY;
                points.Add(new Point(x, y));
            }

            if (points.Count > 1)
            {
                StreamGeometry area = new StreamGeometry();
                using (StreamGeometryContext context = area.Open())
                {
                    context.BeginFigure(new Point(points[0].X, baselineY), true, true);
                    foreach (Point point in points) context.LineTo(point, true, false);
                    context.LineTo(new Point(points[points.Count - 1].X, baselineY), true, false);
                }
                area.Freeze();
                Path areaPath = new Path();
                areaPath.Data = area;
                LinearGradientBrush fill = new LinearGradientBrush();
                fill.StartPoint = new Point(0.5, 0);
                fill.EndPoint = new Point(0.5, 1);
                fill.GradientStops.Add(new GradientStop(
                    Color.FromArgb(dark ? (byte)72 : (byte)48, accent.R, accent.G, accent.B), 0));
                fill.GradientStops.Add(new GradientStop(
                    Color.FromArgb(0, accent.R, accent.G, accent.B), 1));
                areaPath.Fill = fill;
                trendCanvas.Children.Add(areaPath);

                Polyline line = new Polyline();
                line.Points = points;
                line.Stroke = FromColor(accent);
                line.StrokeThickness = 1.6;
                line.StrokeLineJoin = PenLineJoin.Round;
                trendCanvas.Children.Add(line);

                Point last = points[points.Count - 1];
                Ellipse current = new Ellipse();
                current.Width = 5;
                current.Height = 5;
                current.Fill = FromColor(accent);
                current.Stroke = new SolidColorBrush(palette.Card);
                current.StrokeThickness = 1;
                Canvas.SetLeft(current, last.X - 2.5);
                Canvas.SetTop(current, last.Y - 2.5);
                trendCanvas.Children.Add(current);
            }

            Border hoverSurface = new Border();
            hoverSurface.Background = Brushes.Transparent;
            hoverSurface.MouseMove += delegate(object sender, System.Windows.Input.MouseEventArgs e)
            {
                if (window.Count == 0) return;
                Point screen = new Point(
                    System.Windows.Forms.Cursor.Position.X,
                    System.Windows.Forms.Cursor.Position.Y);
                if (trendHoverScreenValid
                    && Math.Abs(screen.X - trendHoverScreen.X) < 0.5
                    && Math.Abs(screen.Y - trendHoverScreen.Y) < 0.5) return;
                trendHoverScreen = screen;
                trendHoverScreenValid = true;
                double mx = e.GetPosition(hoverSurface).X;
                int index = (int)Math.Floor(mx / chartWidth * window.Count);
                if (index < 0) index = 0;
                if (index >= window.Count) index = window.Count - 1;
                if (index == trendHoverIndex) return;
                trendHoverIndex = index;
                DayUsage hoverItem = window[index];
                trendSummary.Text = hoverItem.Day.Substring(5) + " · "
                    + (exact ? "官方 " : "本机 ") + Fmt.Money(hoverItem.Usage, currency);
            };
            trendHits.Children.Add(hoverSurface);
            trendDefaultText = "点击切换 7 / 14 / 30 天统计 ▾";
            trendSummary.Text = trendDefaultText;
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
            DpiScale dpi = VisualTreeHelper.GetDpi(Anchor);
            double dpiX = dpi.DpiScaleX > 0.01 ? dpi.DpiScaleX : 1.0;
            double dpiY = dpi.DpiScaleY > 0.01 ? dpi.DpiScaleY : 1.0;
            double anchorCenterX = Anchor.X + Anchor.Width / 2;
            double anchorCenterY = Anchor.Y + Anchor.Height / 2;
            ScreenArea area = ScreenArea.ForPoint(anchorCenterX * dpiX, anchorCenterY * dpiY);
            double areaLeft = area.Left / dpiX;
            double areaTop = area.Top / dpiY;
            double areaRight = area.Right / dpiX;
            double areaBottom = area.Bottom / dpiY;
            double width = Width > 0 ? Width : ActualWidth;
            double height = Height > 0 ? Height : ActualHeight;
            double x = anchorCenterX - width / 2;
            x = Math.Max(areaLeft + 4, Math.Min(x, areaRight - width - 4));
            // 窗口带了一圈阴影余量，贴到宠物身边时要把它减掉，不然气泡会离宠物远一截。
            double pad = BubbleShadowPad;
            double y = Anchor.Y - height - 6 + pad;
            bool above = true;
            if (y < areaTop + 4)
            {
                y = Anchor.Y + Anchor.Height + 6 - pad;
                above = false;
            }
            if (y + height > areaBottom - 4)
                y = Math.Max(areaTop + 4, areaBottom - height - 4);

            bool tail = showTail;
            double tailSize = tailTopShape.Width;
            double contentWidth = Math.Max(0, width - BubbleShadowPad * 2);
            double anchorCenter = Anchor.X + Anchor.Width / 2;
            double localCenter = anchorCenter - x - BubbleShadowPad;
            double minCenter = tailSize / 2 + 2;
            double maxCenter = Math.Max(minCenter, contentWidth - tailSize / 2 - 2);
            double tailCenter = Math.Max(minCenter, Math.Min(localCenter, maxCenter));
            double tailLeft = tailCenter - tailSize / 2;
            tailTopShape.Margin = new Thickness(tailLeft, 0, 0, 0);
            tailBottomShape.Margin = new Thickness(tailLeft, 0, 0, 0);
            tailTop.Margin = new Thickness(0, 1, 0, 0);
            tailBottom.Margin = new Thickness(0, -1, 0, 0);
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
