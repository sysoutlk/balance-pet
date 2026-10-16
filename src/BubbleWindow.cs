// The speech bubble. It renders a BubbleView: a title, one big number, optional detail
// rows, the peak/off-peak line with a countdown, an optional 7-day mini chart, one quip
// line, a note line and the source tag. Clicking the pet cycles through several views.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace DshPet
{
    internal sealed class BubbleView
    {
        public string Title = "";
        public string Big = "";
        public double BigSize = 25;
        public string BigColor = "#203170";
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
        private readonly Border card;
        private readonly TextBlock title;
        private readonly TextBlock amount;
        private readonly TextBlock sub;
        private readonly StackPanel rows;
        private readonly TextBlock peak;
        private readonly StackPanel trend;
        private readonly List<Border> bars = new List<Border>();
        private readonly TextBlock quip;
        private readonly TextBlock note;
        private readonly TextBlock source;
        private readonly StackPanel dots;

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
            panel.MinWidth = 190;
            title = MakeText(12, "#203170", "");
            title.FontWeight = FontWeights.SemiBold;
            amount = MakeText(25, "#203170", "--");
            amount.FontWeight = FontWeights.Bold;
            amount.Margin = new Thickness(0, 2, 0, 2);
            sub = MakeText(11, "#8d6fd0", "");
            sub.Margin = new Thickness(0, 1, 0, 0);
            sub.Visibility = Visibility.Collapsed;
            rows = new StackPanel();
            rows.Margin = new Thickness(0, 4, 0, 0);
            peak = MakeText(12, "#e0433f", "");
            peak.FontWeight = FontWeights.SemiBold;
            peak.Margin = new Thickness(0, 4, 0, 0);
            peak.TextWrapping = TextWrapping.Wrap;
            peak.MaxWidth = 220;

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
            quip.MaxWidth = 220;
            quip.Margin = new Thickness(0, 7, 0, 0);
            quip.Visibility = Visibility.Collapsed;
            note = MakeText(10, "#a8b3cc", "");
            note.TextWrapping = TextWrapping.Wrap;
            note.MaxWidth = 220;
            note.Margin = new Thickness(0, 5, 0, 0);
            note.Visibility = Visibility.Collapsed;

            DockPanel footer = new DockPanel();
            footer.Margin = new Thickness(0, 5, 0, 0);
            dots = new StackPanel();
            dots.Orientation = Orientation.Horizontal;
            dots.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(dots, Dock.Right);
            footer.Children.Add(dots);
            source = MakeText(9, "#a8b3cc", "");
            footer.Children.Add(source);

            panel.Children.Add(title);
            panel.Children.Add(amount);
            panel.Children.Add(sub);
            panel.Children.Add(rows);
            panel.Children.Add(peak);
            panel.Children.Add(trend);
            panel.Children.Add(quip);
            panel.Children.Add(note);
            panel.Children.Add(footer);
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

        public void UpdateFrom(BubbleView view)
        {
            title.Text = view.Title;
            amount.Text = view.Big;
            amount.FontSize = view.BigSize;
            amount.Foreground = Brush(view.BigColor);
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
                DockPanel line = new DockPanel();
                line.Margin = new Thickness(0, 2, 0, 0);
                TextBlock value = MakeText(11.5, "#203170", row.Value);
                value.FontWeight = FontWeights.SemiBold;
                value.TextAlignment = TextAlignment.Right;
                DockPanel.SetDock(value, Dock.Right);
                line.Children.Add(value);
                TextBlock name = MakeText(10.5, "#7d8bb0", row.Key);
                name.VerticalAlignment = VerticalAlignment.Center;
                line.Children.Add(name);
                rows.Children.Add(line);
            }
            rows.Visibility = view.Rows.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

            RefreshPeak(view.PeakText, view.PeakNow);
            trend.Visibility = view.ShowTrend ? Visibility.Visible : Visibility.Collapsed;
            if (view.ShowTrend) UpdateTrend(view.Trend);

            if (!string.IsNullOrEmpty(view.Quip))
            {
                quip.Text = view.Quip;
                quip.Visibility = Visibility.Visible;
            }
            else
            {
                quip.Visibility = Visibility.Collapsed;
            }
            if (!string.IsNullOrEmpty(view.Note))
            {
                note.Text = view.Note;
                note.Visibility = Visibility.Visible;
                note.Foreground = Brush(view.NoteError ? "#e0433f" : "#a8b3cc");
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
            dots.Children.Clear();
            if (index < 0 || count <= 1) return;
            for (int i = 0; i < count; i++)
            {
                Border dot = new Border();
                dot.Width = 5;
                dot.Height = 5;
                dot.CornerRadius = new CornerRadius(3);
                dot.Margin = new Thickness(3, 0, 0, 0);
                dot.VerticalAlignment = VerticalAlignment.Center;
                dot.Background = Brush(i == index ? "#4B6BFB" : "#C9D4EE");
                dots.Children.Add(dot);
            }
        }

        public void RefreshPeak(string peakText, bool peakNow)
        {
            peak.Text = peakText;
            peak.Foreground = Brush(peakNow ? "#e0433f" : "#2fa24c");
            peak.Visibility = string.IsNullOrEmpty(peakText) ? Visibility.Collapsed : Visibility.Visible;
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
            if (y < area.Top + 4) y = Anchor.Y + Anchor.Height + 6;
            if (y + height > area.Bottom - 4) y = Math.Max(area.Top + 4, area.Bottom - height - 4);
            Left = Math.Round(x, 1);
            Top = Math.Round(y, 1);
        }
    }
}
