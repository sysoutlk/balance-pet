// Single-image 2D mesh rig.
//
// A normal PNG is turned into a small deformable grid instead of being cut into
// body/head layers. The head area rotates around a virtual neck, the body breathes,
// the eye box can blink, and the whole mesh leans toward the cursor. This is the
// 1.2.0 "one image is enough" path; it deliberately avoids requiring separate art.
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace BalancePet
{
    internal sealed class MeshPetView : Grid
    {
        private const int Cols = 14;
        private const int Rows = 16;

        private readonly Config cfg;
        private readonly string role;
        private readonly double aspect;
        private readonly MeshGeometry3D mesh = new MeshGeometry3D();
        private readonly double[] baseU;
        private readonly double[] baseV;
        private readonly Viewport3D viewport;
        private double clock;
        private double nextBlink;
        private double blinkStart = -10;
        private double blink;
        private double gazeX;
        private double gazeY;

        public MeshPetView(BitmapSource bitmap, Config config, string role)
        {
            cfg = config;
            this.role = role;
            aspect = Math.Max(0.2, bitmap.PixelWidth / (double)Math.Max(1, bitmap.PixelHeight));
            int count = (Cols + 1) * (Rows + 1);
            baseU = new double[count];
            baseV = new double[count];
            for (int row = 0; row <= Rows; row++)
            {
                for (int col = 0; col <= Cols; col++)
                {
                    int index = row * (Cols + 1) + col;
                    double u = col / (double)Cols;
                    double v = row / (double)Rows;
                    baseU[index] = u;
                    baseV[index] = v;
                    mesh.Positions.Add(new Point3D((u - 0.5) * aspect, 0.5 - v, 0));
                    mesh.TextureCoordinates.Add(new Point(u, v));
                }
            }
            for (int row = 0; row < Rows; row++)
            {
                for (int col = 0; col < Cols; col++)
                {
                    int topLeft = row * (Cols + 1) + col;
                    int topRight = topLeft + 1;
                    int bottomLeft = topLeft + (Cols + 1);
                    int bottomRight = bottomLeft + 1;
                    mesh.TriangleIndices.Add(topLeft);
                    mesh.TriangleIndices.Add(bottomLeft);
                    mesh.TriangleIndices.Add(topRight);
                    mesh.TriangleIndices.Add(topRight);
                    mesh.TriangleIndices.Add(bottomLeft);
                    mesh.TriangleIndices.Add(bottomRight);
                }
            }

            ImageBrush brush = new ImageBrush(bitmap);
            brush.Stretch = Stretch.Fill;
            brush.ViewportUnits = BrushMappingMode.RelativeToBoundingBox;
            brush.ViewboxUnits = BrushMappingMode.RelativeToBoundingBox;
            brush.Viewport = new Rect(0, 0, 1, 1);
            brush.Viewbox = new Rect(0, 0, 1, 1);
            DiffuseMaterial material = new DiffuseMaterial(brush);
            GeometryModel3D model = new GeometryModel3D(mesh, material);
            model.BackMaterial = material;
            Model3DGroup group = new Model3DGroup();
            group.Children.Add(new AmbientLight(Colors.White));
            group.Children.Add(model);

            viewport = new Viewport3D();
            viewport.IsHitTestVisible = false;
            viewport.Camera = new OrthographicCamera
            {
                Position = new Point3D(0, 0, 2),
                LookDirection = new Vector3D(0, 0, -1),
                UpDirection = new Vector3D(0, 1, 0),
                Width = aspect
            };
            viewport.Children.Add(new ModelVisual3D { Content = group });
            Children.Add(viewport);
            nextBlink = 2.0 + Program.Rng.NextDouble() * 3.0;
        }

        public void Update(double dt, string state, bool sleeping, double tuck, string edgeSide)
        {
            clock += dt;
            double intensity = RigSettings.GetNumber(cfg, role, "intensity", 1.0);
            if (intensity < 0) intensity = 0;
            if (intensity > 2) intensity = 2;

            UpdateBlink(sleeping, RigSettings.GetFlag(cfg, role, "blink", true));
            UpdateGaze(sleeping, tuck, edgeSide, RigSettings.GetFlag(cfg, role, "gaze", true), dt);

            double headAngle = gazeX * 0.055 + Math.Sin(clock * 0.7) * 0.012;
            if (state == "sleep") headAngle += 0.10;
            if (state == "wave" || state == "greet") headAngle += Math.Sin(clock * 7.5) * 0.035;
            if (state == "walk") headAngle += Math.Sin(clock * 5.5) * 0.012;
            headAngle *= intensity;

            double faceX = RigSettings.GetNumber(cfg, role, "faceX", 0.5);
            double faceY = RigSettings.GetNumber(cfg, role, "faceY", 0.25);
            double faceW = RigSettings.GetNumber(cfg, role, "faceW", 0.34);
            double faceH = RigSettings.GetNumber(cfg, role, "faceH", 0.20);
            double neckY = 0.5 - 0.45;   // normalized v=0.45 -> mesh y
            for (int i = 0; i < baseU.Length; i++)
            {
                double u = baseU[i];
                double v = baseV[i];
                double x = (u - 0.5) * aspect;
                double y = 0.5 - v;

                double headWeight = 1.0 - SmoothStep(0.34, 0.60, v);
                double lowerWeight = SmoothStep(0.66, 1.0, v);
                double breath = Math.Sin(clock * 1.8) * 0.012 * intensity * (1.0 - headWeight);
                double bodyPivot = 0.5 - 0.66;
                y = bodyPivot + (y - bodyPivot) * (1.0 + breath);
                x = x * (1.0 - breath * 0.45);
                x += Math.Sin(clock * 1.1 + 1.3) * 0.007 * intensity * lowerWeight;

                if (headWeight > 0.001)
                {
                    double dx = x;
                    double dy = y - neckY;
                    x = dx * Math.Cos(headAngle * headWeight) - dy * Math.Sin(headAngle * headWeight);
                    y = neckY + dx * Math.Sin(headAngle * headWeight) + dy * Math.Cos(headAngle * headWeight);
                    x += gazeX * 0.012 * intensity * headWeight;
                    y -= gazeY * 0.008 * intensity * headWeight;
                }

                double eyeWeight = FaceWeight(u, v, faceX, faceY, faceW, faceH);
                if (eyeWeight > 0.001 && (blink > 0.001 || Math.Abs(gazeX) > 0.001))
                {
                    double eyeCenterY = 0.5 - faceY;
                    double dy = y - eyeCenterY;
                    y = eyeCenterY + dy * (1.0 - 0.55 * blink * eyeWeight);
                    x += gazeX * 0.015 * intensity * eyeWeight;
                    y -= gazeY * 0.010 * intensity * eyeWeight;
                }

                if (state == "walk")
                {
                    y += Math.Sin(clock * 8.0) * 0.005 * intensity;
                }
                else if (state == "hop" || state == "bounce")
                {
                    y += Math.Sin(clock * 10.0) * 0.003 * intensity;
                }

                mesh.Positions[i] = new Point3D(x, y, 0);
            }
        }

        private void UpdateBlink(bool sleeping, bool enabled)
        {
            if (sleeping)
            {
                blink = 1;
                return;
            }
            if (!enabled)
            {
                blink = 0;
                return;
            }
            if (clock >= nextBlink)
            {
                blinkStart = clock;
                nextBlink = clock + 2.2 + Program.Rng.NextDouble() * 4.0;
            }
            double t = clock - blinkStart;
            blink = t >= 0 && t < 0.18 ? Math.Sin(Math.PI * t / 0.18) : 0;
        }

        private void UpdateGaze(bool sleeping, double tuck, string edgeSide, bool enabled, double dt)
        {
            double targetX = 0;
            double targetY = 0;
            if (enabled && !sleeping && tuck < 0.15 && string.IsNullOrEmpty(edgeSide))
            {
                try
                {
                    Point center = PointToScreen(new Point(ActualWidth / 2, ActualHeight / 2));
                    System.Drawing.Point cursor = System.Windows.Forms.Cursor.Position;
                    double dx = cursor.X - center.X;
                    double dy = cursor.Y - center.Y;
                    targetX = Math.Max(-1, Math.Min(1, dx / 520.0));
                    targetY = Math.Max(-1, Math.Min(1, dy / 420.0));
                }
                catch
                {
                    targetX = 0;
                    targetY = 0;
                }
            }
            double follow = Math.Max(0.02, Math.Min(0.35, dt * 4.0));
            gazeX += (targetX - gazeX) * follow;
            gazeY += (targetY - gazeY) * follow;
        }

        private static double FaceWeight(double u, double v, double x, double y, double w, double h)
        {
            double ax = 1.0 - SmoothStep(w * 0.5, w * 0.78, Math.Abs(u - x));
            double ay = 1.0 - SmoothStep(h * 0.5, h * 0.78, Math.Abs(v - y));
            if (ax < 0) ax = 0;
            if (ay < 0) ay = 0;
            return ax * ay;
        }

        private static double SmoothStep(double edge0, double edge1, double value)
        {
            if (edge1 <= edge0) return value >= edge1 ? 1.0 : 0.0;
            double t = (value - edge0) / (edge1 - edge0);
            if (t < 0) t = 0;
            if (t > 1) t = 1;
            return t * t * (3.0 - 2.0 * t);
        }
    }
}
