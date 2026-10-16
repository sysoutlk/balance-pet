// Frame animation support.
//
// A role can be:
//   * a .gif  -> every GIF frame plays in every state (state changes only alter the speed)
//   * a folder roles\<name>\ -> every image is a frame; subfolders named after states
//     (idle / walk / wave / ...) give each state its own frames
//   * a .png sprite sheet described in pet-config.json (roles.sheets) with rows per state
//   * a single .png / .jpg -> one static frame (transforms only, as before)
//
// The built-in whale has no bitmap frames; it is animated in vector code by WhaleAnimator.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BalancePet
{
    internal sealed class FrameSet
    {
        public List<BitmapSource> Frames = new List<BitmapSource>();
        public double Fps = 8;
        public int Index;
        private double accumulated;

        public bool Empty { get { return Frames.Count == 0; } }

        /// <summary>Advances the playhead and returns the frame that should be shown.</summary>
        public BitmapSource Advance(double seconds, double speed)
        {
            if (Frames.Count == 0) return null;
            if (Frames.Count == 1) return Frames[0];
            double fps = Fps;
            if (fps < 1) fps = 1;
            if (speed < 0.1) speed = 0.1;
            accumulated += seconds * fps * speed;
            int steps = (int)accumulated;
            if (steps > 0)
            {
                accumulated -= steps;
                Index = ((Index + steps) % Frames.Count + Frames.Count) % Frames.Count;
            }
            return Frames[Index];
        }

        public void Reset()
        {
            Index = 0;
            accumulated = 0;
        }
    }

    internal sealed class RoleAnim
    {
        public Dictionary<string, FrameSet> States = new Dictionary<string, FrameSet>();
        public FrameSet Default;

        public bool Animated
        {
            get { return Default != null && Default.Frames.Count > 1; }
        }

        public FrameSet For(string state)
        {
            FrameSet set;
            if (!string.IsNullOrEmpty(state) && States.TryGetValue(state, out set) && !set.Empty) return set;
            return Default;
        }

        public static RoleAnim Load(Config cfg, string role)
        {
            if (string.IsNullOrEmpty(role)) return null;
            try
            {
                string path = Path.Combine(AppPaths.RolesDir, role);
                if (Directory.Exists(path)) return LoadFolder(cfg, path);
                if (!File.Exists(path)) return null;
                string extension = Path.GetExtension(path).ToLowerInvariant();
                if (extension == ".gif") return LoadGif(path);
                RoleAnim sheet = LoadSprite(cfg, role, path);
                if (sheet != null) return sheet;
                return LoadStatic(path);
            }
            catch (Exception ex)
            {
                Log.Write("role load failed: " + ex.Message);
                return null;
            }
        }

        private static BitmapImage LoadBitmap(string path)
        {
            BitmapImage bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(path);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }

        private static RoleAnim LoadStatic(string path)
        {
            RoleAnim anim = new RoleAnim();
            FrameSet set = new FrameSet();
            set.Fps = 1;
            set.Frames.Add(LoadBitmap(path));
            anim.Default = set;
            return anim;
        }

        private static RoleAnim LoadGif(string path)
        {
            GifBitmapDecoder decoder = new GifBitmapDecoder(new Uri(path), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            FrameSet set = new FrameSet();
            set.Fps = 10;
            foreach (BitmapFrame frame in decoder.Frames)
            {
                BitmapSource source = frame;
                source.Freeze();
                set.Frames.Add(source);
            }
            // Prefer the per-frame delays the GIF carries (1/100 s units).
            try
            {
                BitmapFrame first = decoder.Frames[0];
                BitmapMetadata metadata = first.Metadata as BitmapMetadata;
                if (metadata != null && metadata.ContainsQuery("/grctlext/Delay"))
                {
                    object delay = metadata.GetQuery("/grctlext/Delay");
                    double hundredths = Convert.ToDouble(delay, CultureInfo.InvariantCulture);
                    if (hundredths > 0) set.Fps = 100.0 / hundredths;
                }
            }
            catch
            {
            }
            if (set.Frames.Count == 0) return null;
            RoleAnim anim = new RoleAnim();
            anim.Default = set;
            anim.States["idle"] = set;
            return anim;
        }

        private static RoleAnim LoadFolder(Config cfg, string folder)
        {
            RoleAnim anim = new RoleAnim();
            double fps = cfg.GetNumberAt("roles.fps", 8);
            if (fps < 1) fps = 8;
            List<string> files = new List<string>();
            string[] patterns = new string[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif" };
            foreach (string pattern in patterns) files.AddRange(Directory.GetFiles(folder, pattern));
            files.Sort(StringComparer.OrdinalIgnoreCase);
            if (files.Count > 0)
            {
                FrameSet set = new FrameSet();
                set.Fps = fps;
                foreach (string file in files)
                {
                    FrameSet single = Path.GetExtension(file).ToLowerInvariant() == ".gif"
                        ? LoadGif(file).Default
                        : null;
                    if (single != null) set.Frames.AddRange(single.Frames);
                    else set.Frames.Add(LoadBitmap(file));
                }
                anim.Default = set;
                anim.States["idle"] = set;
            }
            foreach (string directory in Directory.GetDirectories(folder))
            {
                string state = Path.GetFileName(directory).ToLowerInvariant();
                FrameSet set = new FrameSet();
                set.Fps = fps;
                List<string> frames = new List<string>();
                foreach (string pattern in patterns) frames.AddRange(Directory.GetFiles(directory, pattern));
                frames.Sort(StringComparer.OrdinalIgnoreCase);
                foreach (string file in frames) set.Frames.Add(LoadBitmap(file));
                if (!set.Empty) anim.States[state] = set;
            }
            if (anim.Default == null && anim.States.Count > 0)
            {
                foreach (FrameSet set in anim.States.Values)
                {
                    anim.Default = set;
                    break;
                }
            }
            return anim.Default == null ? null : anim;
        }

        /// <summary>
        /// Sprite sheet via pet-config.json, e.g.
        ///   "roles": { "sheets": { "cat.png": {
        ///       "cols": 4, "rows": 5, "fps": 8,
        ///       "states": { "idle": [0, 0, 3], "walk": [1, 0, 3], "wave": [2, 0, 3] } } } }
        /// where each state is [row, firstColumn, lastColumn].
        /// </summary>
        private static RoleAnim LoadSprite(Config cfg, string role, string path)
        {
            Dictionary<string, object> sheets = cfg.GetObject("roles", "sheets");
            string key = role;
            object entry = null;
            if (!sheets.TryGetValue(key, out entry) && !sheets.TryGetValue(Path.GetFileName(role), out entry)) return null;
            Dictionary<string, object> sheet = Json.AsObject(entry);
            if (sheet.Count == 0) return null;
            BitmapImage bitmap = LoadBitmap(path);
            int cols = (int)Json.ToNumber(Json.Get(sheet, "cols"), 4);
            int rows = (int)Json.ToNumber(Json.Get(sheet, "rows"), 1);
            if (cols < 1) cols = 1;
            if (rows < 1) rows = 1;
            int frameWidth = bitmap.PixelWidth / cols;
            int frameHeight = bitmap.PixelHeight / rows;
            if (frameWidth < 1 || frameHeight < 1) return null;
            double fps = Json.ToNumber(Json.Get(sheet, "fps"), 8);
            if (fps < 1) fps = 8;
            RoleAnim anim = new RoleAnim();
            Dictionary<string, object> states = Json.AsObject(Json.Get(sheet, "states"));
            foreach (KeyValuePair<string, object> pair in states)
            {
                List<object> range = Json.AsArray(pair.Value);
                if (range.Count < 3) continue;
                int row = (int)Json.ToNumber(range[0], 0);
                int from = (int)Json.ToNumber(range[1], 0);
                int to = (int)Json.ToNumber(range[2], 0);
                FrameSet set = new FrameSet();
                set.Fps = fps;
                for (int col = from; col <= to; col++)
                {
                    if (row < 0 || row >= rows || col < 0 || col >= cols) continue;
                    CroppedBitmap crop = new CroppedBitmap(bitmap, new Int32Rect(col * frameWidth, row * frameHeight, frameWidth, frameHeight));
                    crop.Freeze();
                    set.Frames.Add(crop);
                }
                if (!set.Empty) anim.States[pair.Key.ToLowerInvariant()] = set;
            }
            if (anim.States.Count == 0) return null;
            FrameSet idle;
            if (anim.States.TryGetValue("idle", out idle)) anim.Default = idle;
            else
            {
                foreach (FrameSet set in anim.States.Values)
                {
                    anim.Default = set;
                    break;
                }
            }
            return anim;
        }
    }

    /// <summary>Vector animation for the built-in whale (tail wag, blink, spout bubbles).</summary>
    internal sealed class WhaleAnimator
    {
        private readonly FrameworkElement root;
        private readonly RotateTransform tailUpper = new RotateTransform(0, 52, 110);
        private readonly RotateTransform tailLower = new RotateTransform(0, 52, 120);
        private readonly ScaleTransform blink = new ScaleTransform(1, 1);
        private readonly List<System.Windows.Shapes.Ellipse> spout = new List<System.Windows.Shapes.Ellipse>();
        private double energy = 1;

        public WhaleAnimator()
        {
            Viewbox box = new Viewbox();
            box.Stretch = Stretch.Fill;
            object[] parts = Mascot.BuildParts();
            Canvas canvas = (Canvas)parts[0];
            tailUpperShape = (System.Windows.Shapes.Path)parts[1];
            tailLowerShape = (System.Windows.Shapes.Path)parts[2];
            eyeShape = (System.Windows.Shapes.Ellipse)parts[3];
            glintShape = (System.Windows.Shapes.Ellipse)parts[4];
            spout.Add((System.Windows.Shapes.Ellipse)parts[5]);
            spout.Add((System.Windows.Shapes.Ellipse)parts[6]);
            spout.Add((System.Windows.Shapes.Ellipse)parts[7]);
            tailUpperShape.RenderTransform = tailUpper;
            tailLowerShape.RenderTransform = tailLower;
            eyeShape.RenderTransform = blink;
            eyeShape.RenderTransformOrigin = new Point(0.5, 0.5);
            glintShape.RenderTransform = blink;
            glintShape.RenderTransformOrigin = new Point(0.5, 0.5);
            box.Child = canvas;
            root = box;
        }

        private readonly System.Windows.Shapes.Path tailUpperShape;
        private readonly System.Windows.Shapes.Path tailLowerShape;
        private readonly System.Windows.Shapes.Ellipse eyeShape;
        private readonly System.Windows.Shapes.Ellipse glintShape;

        public FrameworkElement Root { get { return root; } }

        public void Update(double time, double energyLevel)
        {
            energy = Math.Max(0.2, Math.Min(2.2, energyLevel));
            double wag = Math.Sin(time * 2.4 * energy) * (10 + 6 * energy);
            tailUpper.Angle = wag;
            tailLower.Angle = wag * 0.8;

            // Blink roughly every three seconds, for about 130 ms.
            double cycle = time % 3.4;
            double squash = cycle > 3.15 ? 0.15 : 1;
            blink.ScaleY = squash;

            for (int i = 0; i < spout.Count; i++)
            {
                System.Windows.Shapes.Ellipse drop = spout[i];
                double phase = time * 0.9 + i * 0.9;
                drop.Opacity = 0.55 + 0.35 * Math.Abs(Math.Sin(phase));
                Canvas.SetTop(drop, 24 - i * 4 - Math.Abs(Math.Sin(phase)) * 7);
            }
        }
    }
}
