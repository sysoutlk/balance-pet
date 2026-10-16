// The speech bubble: primary provider balance, the other providers as compact rows,
// today's usage, peak/off-peak phase with a countdown, a 7-day mini chart, one quip
// line and the data source tag.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace DshPet
{
    internal sealed class BubbleWindow : Window
    {
        private readonly Border card;
        private readonly TextBlock title;
        private readonly TextBlock amount;
        private readonly TextBlock details;
        private readonly StackPanel others;
        private readonly TextBlock today;
        private readonly TextBlock peak;
        private readonly TextBlock quip;
        private readonly TextBlock note;
        private readonly TextBlock source;
        private readonly StackPanel trend;
        private readonly List<Border> bars = new List<Border>();

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

            card = new Border();
            card.CornerRadius = new CornerRadius(14);
            card.Background = Brush("#F7FFFFFF");
            card.BorderBrush = Brush("#22203170");
            card.BorderThickness = new Thickness(1);
            card.Padding = new Thickness(15, 11, 15, 11);
            DropShadowEffect shadow = new DropShadowEffect();
            shadow.BlurRadius = 18;
            shadow.ShadowDepth = 4;
            shadow.Opacity = 0.3;
            shadow.Color = Colors.Black;
            card.Effect = shadow;

            StackPanel panel = new StackPanel();
            panel.MinWidth = 186;
            title = MakeText(12, "#203170", "DeepSeek 余额");
            title.FontWeight = FontWeights.SemiBold;
            amount = MakeText(25, "#203170", "--");
            amount.FontWeight = FontWeights.Bold;
            amount.Margin = new Thickness(0, 2, 0, 2);
            details = MakeText(11, "#8d6fd0", "");
            details.Margin = new Thickness(0, 1, 0, 0);
            details.Visibility = Visibility.Collapsed;

            others = new StackPanel();
            others.Margin = new Thickness(0, 6, 0, 0);
            others.Visibility = Visibility.Collapsed;

            today = MakeText(12, "#7d8bb0", "今日已用 --");
            today.Margin = new Thickness(0, 6, 0, 0);
            peak = MakeText(12, "#e0433f", "峰谷 --");
            peak.FontWeight = FontWeights.SemiBold;
            peak.Margin = new Thickness(0, 3, 0, 0);

            trend = new StackPanel();
            trend.Orientation = Orientation.Horizontal;
            trend.Height = 30;
            trend.Margin = new Thickness(0, 7, 0, 0);
            for (int i = 0; i < 7; i++)
            {
                Border bar = new Border();
                bar.Width = 12;
                bar.Height = 2;
                bar.Margin = new Thickness(0, 0, 3, 0);
                bar.VerticalAlignment = VerticalAlignment.Bottom;
                bar.CornerRadius = new CornerRadius(2);
                bar.Background = Brush("#9fb0d9");
                bars.Add(bar);
                trend.Children.Add(bar);
            }

            quip = MakeText(11, "#2f4488", "");
            quip.TextWrapping = TextWrapping.Wrap;
            quip.MaxWidth = 215;
            quip.Margin = new Thickness(0, 7, 0, 0);
            quip.Visibility = Visibility.Collapsed;
            note = MakeText(10, "#a8b3cc", "");
            note.TextWrapping = TextWrapping.Wrap;
            note.MaxWidth = 215;
            note.Margin = new Thickness(0, 5, 0, 0);
            note.Visibility = Visibility.Collapsed;
            source = MakeText(9, "#a8b3cc", "");
            source.Margin = new Thickness(0, 5, 0, 0);

            panel.Children.Add(title);
            panel.Children.Add(amount);
            panel.Children.Add(details);
            panel.Children.Add(others);
            panel.Children.Add(today);
            panel.Children.Add(peak);
            panel.Children.Add(trend);
            panel.Children.Add(quip);
            panel.Children.Add(note);
            panel.Children.Add(source);
            card.Child = panel;
            Content = card;
            MouseLeftButtonDown += delegate { HideNow(); };
        }

        private static TextBlock MakeText(double size, string colour, string text)
        {
            TextBlock block = new TextBlock();
            block.FontSize = size;
            block.Foreground = Brush(colour);
            block.Text = text;
            return block;
        }

        private static SolidColorBrush Brush(string hex)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }

        public void UpdateFrom(Snapshot data, string quipText, string overrideText, string peakText, bool peakNow)
        {
            ProviderState primary = data.Primary;
            title.Text = primary == null ? "余额" : primary.Name + " 余额";
            amount.Text = primary == null ? "--" : Fmt.Money(primary.Balance, primary.Currency);
            if (primary != null && primary.Details.Count > 0)
            {
                details.Text = string.Join(" · ", primary.Details.ToArray());
                details.Visibility = Visibility.Visible;
            }
            else
            {
                details.Visibility = Visibility.Collapsed;
            }
            UpdateOthers(data, primary);
            today.Text = primary == null
                ? "今日已用 --"
                : "今日已用 " + Fmt.Money(primary.TodayUsage, primary.Currency);
            RefreshPeak(peakText, peakNow);
            RefreshNote(data, primary);
            string shown = !string.IsNullOrEmpty(overrideText) ? overrideText : quipText;
            if (!string.IsNullOrEmpty(shown))
            {
                quip.Text = shown;
                quip.Visibility = Visibility.Visible;
            }
            else
            {
                quip.Visibility = Visibility.Collapsed;
            }
            string stamp = data.AtMs > 0
                ? new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(data.AtMs).ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture)
                : "--";
            int okCount = data.OkCount;
            string sourceText = primary == null ? "无数据源" : primary.SourceText;
            if (okCount > 1) sourceText = okCount + " 个数据源";
            source.Text = sourceText + " · " + stamp + " 更新";
            UpdateTrend(data.History);
        }

        private void UpdateOthers(Snapshot data, ProviderState primary)
        {
            others.Children.Clear();
            foreach (ProviderState p in data.Providers)
            {
                if (primary != null && p.Id == primary.Id) continue;
                if (!p.Enabled || !p.Supported) continue;
                DockPanel row = new DockPanel();
                row.Margin = new Thickness(0, 2, 0, 0);
                TextBlock value = MakeText(11.5, p.Ok ? "#203170" : "#b98a8a", p.Ok ? Fmt.Money(p.Balance, p.Currency) : "查询失败");
                value.FontWeight = FontWeights.SemiBold;
                value.TextAlignment = TextAlignment.Right;
                DockPanel.SetDock(value, Dock.Right);
                row.Children.Add(value);
                TextBlock name = MakeText(10.5, "#7d8bb0", p.Name);
                name.VerticalAlignment = VerticalAlignment.Center;
                row.Children.Add(name);
                others.Children.Add(row);
            }
            others.Visibility = others.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RefreshNote(Snapshot data, ProviderState primary)
        {
            string text = "";
            if (primary != null && !primary.Ok && !string.IsNullOrEmpty(primary.Error))
            {
                text = primary.Error;
            }
            else if (primary != null && primary.Ok && !string.IsNullOrEmpty(primary.Error))
            {
                text = primary.Error;
            }
            else if (data.Providers.Count > 0 && data.OkCount == 0 && !string.IsNullOrEmpty(data.Error))
            {
                text = data.Error;
            }
            if (!string.IsNullOrEmpty(text))
            {
                note.Text = text;
                note.Visibility = Visibility.Visible;
                note.Foreground = Brush(data.Ok ? "#a8b3cc" : "#e0433f");
            }
            else
            {
                note.Visibility = Visibility.Collapsed;
            }
        }

        public void RefreshPeak(string peakText, bool peakNow)
        {
            peak.Text = peakText;
            peak.Foreground = Brush(peakNow ? "#e0433f" : "#2fa24c");
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
                if (index < 0 || index >= history.Count)
                {
                    bar.Height = 2;
                    continue;
                }
                double usage = history[index].Usage;
                bar.Height = max > 0 ? Math.Max(2, Math.Round(26 * usage / max)) : 2;
                string colour = index == history.Count - 1 ? "#e0433f" : (usage > 0 ? "#9fb0d9" : "#4a5570");
                bar.Background = Brush(colour);
            }
        }

        public void ShowFor(PetWindow pet, Snapshot data, string overrideText, string peakText, bool peakNow)
        {
            Anchor = pet;
            UpdateFrom(data, "", overrideText, peakText, peakNow);
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
            if (y < area.Top + 4) y = Anchor.Y + Anchor.Height + 6;
            if (y + height > area.Bottom - 4) y = Math.Max(area.Top + 4, area.Bottom - height - 4);
            Left = Math.Round(x, 1);
            Top = Math.Round(y, 1);
        }
    }
}
