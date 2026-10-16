// Built-in vector mascot: a small blue whale drawn with WPF shapes. It is the default
// "role" so the pet works out of the box without bundling any character art, and it is
// also the source of the application icon (--make-icon).
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace DshPet
{
    internal static class Mascot
    {
        public const double BaseWidth = 224;
        public const double BaseHeight = 192;

        /// <summary>Full-size mascot for the pet window (scales with the window).</summary>
        public static FrameworkElement CreateVisual()
        {
            Viewbox box = new Viewbox();
            box.Child = (Canvas)BuildParts()[0];
            box.Stretch = Stretch.Fill;
            return box;
        }

        /// <summary>Fixed-size thumbnail for the settings panel.</summary>
        public static FrameworkElement CreateThumbnail(double width, double height)
        {
            FrameworkElement visual = CreateVisual();
            visual.Width = width;
            visual.Height = height;
            return visual;
        }

        private static Canvas BuildCanvas()
        {
            return (Canvas)BuildParts()[0];
        }

        /// <summary>
        /// Builds the whale once and hands the animatable parts back to the caller:
        /// [0] canvas, [1] upper fluke, [2] lower fluke, [3] eye, [4] glint,
        /// [5..7] spout drops.
        /// </summary>
        public static object[] BuildParts()
        {
            Canvas canvas = new Canvas();
            canvas.Width = BaseWidth;
            canvas.Height = BaseHeight;

            LinearGradientBrush bodyBrush = new LinearGradientBrush();
            bodyBrush.StartPoint = new Point(0, 0);
            bodyBrush.EndPoint = new Point(0.6, 1);
            bodyBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#7C93FF"), 0));
            bodyBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#3D5AE8"), 1));

            LinearGradientBrush tailBrush = new LinearGradientBrush();
            tailBrush.StartPoint = new Point(0, 0);
            tailBrush.EndPoint = new Point(0.4, 1);
            tailBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#5F79F2"), 0));
            tailBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#2F49C8"), 1));

            SolidColorBrush ink = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#17255F"));
            SolidColorBrush outline = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2B3F9E"));

            // Tail flukes (facing right, tail on the left).
            Path upperFluke = new Path();
            upperFluke.Data = Geometry.Parse("M 52,110 C 24,100 10,74 16,50 C 34,68 52,82 68,90 Z");
            upperFluke.Fill = tailBrush;
            upperFluke.Stroke = outline;
            upperFluke.StrokeThickness = 2;
            upperFluke.StrokeLineJoin = PenLineJoin.Round;
            canvas.Children.Add(upperFluke);

            Path lowerFluke = new Path();
            lowerFluke.Data = Geometry.Parse("M 52,120 C 28,128 16,148 22,166 C 38,150 54,140 68,134 Z");
            lowerFluke.Fill = tailBrush;
            lowerFluke.Stroke = outline;
            lowerFluke.StrokeThickness = 2;
            lowerFluke.StrokeLineJoin = PenLineJoin.Round;
            canvas.Children.Add(lowerFluke);

            // Body.
            Path body = new Path();
            body.Data = Geometry.Parse("M 46,114 C 46,76 86,50 132,50 C 178,50 208,78 208,110 C 208,138 180,156 142,156 C 104,156 66,146 54,132 C 48,126 46,120 46,114 Z");
            body.Fill = bodyBrush;
            body.Stroke = outline;
            body.StrokeThickness = 2;
            body.StrokeLineJoin = PenLineJoin.Round;
            canvas.Children.Add(body);

            // Belly patch.
            Path belly = new Path();
            belly.Data = Geometry.Parse("M 78,146 C 108,158 152,150 186,124 C 170,146 132,158 98,152 C 88,150 82,148 78,146 Z");
            belly.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DCE4FF"));
            belly.Opacity = 0.85;
            canvas.Children.Add(belly);

            // Flipper.
            Path flipper = new Path();
            flipper.Data = Geometry.Parse("M 128,130 C 122,146 132,158 148,154 C 138,148 133,140 134,130 Z");
            flipper.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2F49C8"));
            flipper.Stroke = outline;
            flipper.StrokeThickness = 1.6;
            flipper.StrokeLineJoin = PenLineJoin.Round;
            canvas.Children.Add(flipper);

            // Blush.
            Ellipse blush = new Ellipse();
            blush.Width = 14;
            blush.Height = 8;
            blush.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF9AA9"));
            blush.Opacity = 0.55;
            Canvas.SetLeft(blush, 174);
            Canvas.SetTop(blush, 101);
            canvas.Children.Add(blush);

            // Eye + glint.
            Ellipse eye = new Ellipse();
            eye.Width = 14;
            eye.Height = 14;
            eye.Fill = ink;
            Canvas.SetLeft(eye, 156);
            Canvas.SetTop(eye, 86);
            canvas.Children.Add(eye);

            Ellipse glint = new Ellipse();
            glint.Width = 5;
            glint.Height = 5;
            glint.Fill = Brushes.White;
            Canvas.SetLeft(glint, 159.5);
            Canvas.SetTop(glint, 82.5);
            canvas.Children.Add(glint);

            // Smile.
            Path smile = new Path();
            smile.Data = Geometry.Parse("M 168,112 C 173,118 181,118 186,111");
            smile.Stroke = ink;
            smile.StrokeThickness = 2.4;
            smile.StrokeStartLineCap = PenLineCap.Round;
            smile.StrokeEndLineCap = PenLineCap.Round;
            smile.Fill = null;
            canvas.Children.Add(smile);

            // Spout.
            Ellipse drop1 = new Ellipse();
            drop1.Width = 11;
            drop1.Height = 15;
            drop1.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9FB2FF"));
            drop1.Opacity = 0.85;
            Canvas.SetLeft(drop1, 100);
            Canvas.SetTop(drop1, 26);
            canvas.Children.Add(drop1);

            Ellipse drop2 = new Ellipse();
            drop2.Width = 8;
            drop2.Height = 11;
            drop2.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B7C5FF"));
            drop2.Opacity = 0.75;
            Canvas.SetLeft(drop2, 86);
            Canvas.SetTop(drop2, 18);
            canvas.Children.Add(drop2);

            Ellipse drop3 = new Ellipse();
            drop3.Width = 8;
            drop3.Height = 11;
            drop3.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B7C5FF"));
            drop3.Opacity = 0.75;
            Canvas.SetLeft(drop3, 116);
            Canvas.SetTop(drop3, 20);
            canvas.Children.Add(drop3);

            object[] parts = new object[8];
            parts[0] = canvas;
            parts[1] = upperFluke;
            parts[2] = lowerFluke;
            parts[3] = eye;
            parts[4] = glint;
            parts[5] = drop1;
            parts[6] = drop2;
            parts[7] = drop3;
            return parts;
        }
    }
}
