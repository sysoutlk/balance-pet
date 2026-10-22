// Converts a single-image sprite atlas into this app's pet-pack format.
// The importer accepts PNG/WebP/BMP atlases with a simple manifest and keeps
// all public state names neutral.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BalancePet
{
    internal static class SpriteAtlasPackConverter
    {
        private const double Fps = 8.0;

        private sealed class Row
        {
            public string Key;
            public string Label;
            public string Category;
            public bool Loop;
            public bool Walking;
            public int SourceRow;
            public List<Int32Rect> Cells = new List<Int32Rect>();
        }

        public static bool TryLocate(string source, out string manifestPath, out string imagePath,
            out string suggestedName)
        {
            manifestPath = "";
            imagePath = "";
            suggestedName = "sprite-atlas";
            if (string.IsNullOrEmpty(source) || !Directory.Exists(source)) return false;
            string root = Path.GetFullPath(source).TrimEnd('\\', '/');
            string nativeManifestPath = Path.Combine(root, "pet.json");
            if (File.Exists(nativeManifestPath))
            {
                Dictionary<string, object> nativeManifest = ReadManifest(nativeManifestPath);
                if (nativeManifest != null && LooksLikeNativePack(nativeManifest)) return false;
            }
            string[] manifests = new string[]
            {
                Path.Combine(root, "pet.json"),
                Path.Combine(root, "atlas.json"),
                Path.Combine(root, "spritesheet.json"),
                Path.Combine(root, "meta.json")
            };
            foreach (string candidate in manifests)
            {
                if (!File.Exists(candidate)) continue;
                Dictionary<string, object> manifest = ReadManifest(candidate);
                if (manifest == null || LooksLikeNativePack(manifest)) continue;
                string declared = FirstString(manifest, "spritesheetPath", "spritesheet",
                    "image", "atlas", "file");
                if (string.IsNullOrEmpty(declared)) continue;
                string fullImage = ResolveInside(root, declared);
                if (string.IsNullOrEmpty(fullImage) || !IsImage(fullImage)) continue;
                manifestPath = candidate;
                imagePath = fullImage;
                suggestedName = SafeId(FirstString(manifest, "id"), Path.GetFileName(root));
                return true;
            }

            string[] direct = new string[]
            {
                Path.Combine(root, "spritesheet.webp"),
                Path.Combine(root, "spritesheet.png"),
                Path.Combine(root, "atlas.webp"),
                Path.Combine(root, "atlas.png")
            };
            foreach (string candidate in direct)
            {
                if (!File.Exists(candidate)) continue;
                imagePath = candidate;
                suggestedName = SafeId("", Path.GetFileName(root));
                return true;
            }
            return false;
        }

        public static bool Convert(string manifestPath, string imagePath, string target,
            out string note, out string error)
        {
            note = "";
            error = "";
            try
            {
                Dictionary<string, object> manifest = string.IsNullOrEmpty(manifestPath)
                    ? new Dictionary<string, object>() : ReadManifest(manifestPath);
                if (manifest == null)
                {
                    error = "图集清单无法解析";
                    return false;
                }
                BitmapSource sheet = LoadBitmap(imagePath);
                if (sheet == null || sheet.PixelWidth < 1 || sheet.PixelHeight < 1)
                {
                    error = "图集图片无法解码";
                    return false;
                }

                int cols;
                int rows;
                int frameWidth;
                int frameHeight;
                double fps;
                if (!ReadGrid(manifest, sheet, out cols, out rows, out frameWidth,
                        out frameHeight, out fps))
                {
                    error = "图集尺寸无法识别；需要可整除的网格、单元尺寸或 8 列图集";
                    return false;
                }

                List<Row> rowMap = BuildRows();
                foreach (Row row in rowMap)
                {
                    row.Cells = ReadUsedCells(sheet, row.SourceRow, cols,
                        frameWidth, frameHeight);
                }

                Directory.CreateDirectory(target);
                string clipsDir = Path.Combine(target, "clips");
                Directory.CreateDirectory(clipsDir);
                Dictionary<string, object> clips = new Dictionary<string, object>();
                Dictionary<string, object> states = new Dictionary<string, object>();
                Dictionary<string, object> actions = new Dictionary<string, object>();
                int totalFrames = 0;
                foreach (Row row in rowMap)
                {
                    if (row.Cells.Count == 0) continue;
                    string clipDir = Path.Combine(clipsDir, row.Key);
                    Directory.CreateDirectory(clipDir);
                    int index = 0;
                    foreach (Int32Rect rect in row.Cells)
                    {
                        CroppedBitmap crop = new CroppedBitmap(sheet, rect);
                        crop.Freeze();
                        SavePng(crop, Path.Combine(clipDir,
                            index.ToString("0000", CultureInfo.InvariantCulture) + ".png"));
                        index++;
                    }
                    totalFrames += index;
                    Dictionary<string, object> clip = new Dictionary<string, object>();
                    clip["path"] = "clips/" + row.Key;
                    clip["fps"] = fps;
                    clip["loop"] = row.Loop;
                    clips[row.Key] = clip;

                    if (IsState(row.Key))
                    {
                        states[row.Key] = new string[] { row.Key };
                    }
                    else
                    {
                        Dictionary<string, object> action = new Dictionary<string, object>();
                        action["name"] = row.Label;
                        action["clips"] = new string[] { row.Key };
                        action["seconds"] = Math.Round(Math.Max(0.25, index / fps), 3);
                        action["weight"] = 10;
                        action["walking"] = row.Walking;
                        action["category"] = row.Category;
                        actions[row.Key] = action;
                    }
                }

                if (!states.ContainsKey("idle"))
                {
                    error = "图集第一行没有可用帧";
                    return false;
                }
                if (!states.ContainsKey("walk"))
                {
                    states["walk"] = states["idle"];
                }
                if (clips.ContainsKey("wave"))
                {
                    states["click"] = new string[] { "wave" };
                }

                Dictionary<string, object> display = new Dictionary<string, object>();
                display["width"] = frameWidth;
                display["height"] = Math.Min(200, frameHeight);
                display["upscale"] = frameWidth < 128;
                Dictionary<string, object> result = new Dictionary<string, object>();
                result["id"] = Path.GetFileName(target.TrimEnd('\\', '/'));
                result["name"] = FirstString(manifest, "displayName", "display_name", "name");
                if (string.IsNullOrEmpty(System.Convert.ToString(result["name"], CultureInfo.InvariantCulture)))
                    result["name"] = "图集精灵";
                result["display"] = display;
                result["states"] = states;
                result["clips"] = clips;
                result["actions"] = actions;

                JavaScriptSerializer serializer = new JavaScriptSerializer();
                File.WriteAllText(Path.Combine(target, "pet.json"),
                    serializer.Serialize(result), new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(target, "import-source.txt"),
                    "source=WebP 图集" + Environment.NewLine
                    + "grid=" + cols.ToString(CultureInfo.InvariantCulture) + "x"
                    + rows.ToString(CultureInfo.InvariantCulture) + Environment.NewLine
                    + "frames=" + totalFrames.ToString(CultureInfo.InvariantCulture) + Environment.NewLine,
                    new UTF8Encoding(false));
                note = "WebP 图集转宠物包（" + actions.Count.ToString(CultureInfo.InvariantCulture)
                    + " 个动作，" + totalFrames.ToString(CultureInfo.InvariantCulture) + " 帧）";
                Log.Write("sprite atlas import: " + note);
                return true;
            }
            catch (Exception ex)
            {
                error = "WebP 图集导入失败：" + ex.Message;
                return false;
            }
        }

        private static List<Row> BuildRows()
        {
            List<Row> rows = new List<Row>();
            rows.Add(State("idle", "待机", 0, true));
            rows.Add(State("walk", "行走", 1, true));
            rows.Add(Move("walk_left", "向左行走", 2));
            rows.Add(Action("wave", "挥手", "idle", 3));
            rows.Add(Action("jump", "跳跃", "idle", 4));
            rows.Add(Action("failed", "失落", "idle", 5));
            rows.Add(Action("waiting", "等待", "idle", 6));
            rows.Add(Move("run", "奔跑", 7));
            rows.Add(Action("review", "专注", "idle", 8));
            return rows;
        }

        private static Row State(string key, string label, int sourceRow, bool loop)
        {
            Row row = new Row();
            row.Key = key;
            row.Label = label;
            row.Category = "state";
            row.SourceRow = sourceRow;
            row.Loop = loop;
            row.Walking = true;
            return row;
        }

        private static Row Action(string key, string label, string category, int sourceRow)
        {
            Row row = State(key, label, sourceRow, false);
            row.Category = category;
            row.Walking = true;
            return row;
        }

        private static Row Move(string key, string label, int sourceRow)
        {
            Row row = State(key, label, sourceRow, true);
            row.Category = "move";
            row.Walking = false;
            return row;
        }

        private static bool IsState(string key)
        {
            return key == "idle" || key == "walk";
        }

        private static List<Int32Rect> ReadUsedCells(BitmapSource sheet, int row,
            int cols, int frameWidth, int frameHeight)
        {
            List<Int32Rect> result = new List<Int32Rect>();
            if (row < 0 || row * frameHeight >= sheet.PixelHeight) return result;
            for (int col = 0; col < cols; col++)
            {
                Int32Rect rect = new Int32Rect(col * frameWidth, row * frameHeight,
                    frameWidth, frameHeight);
                CroppedBitmap crop = new CroppedBitmap(sheet, rect);
                crop.Freeze();
                if (!HasVisiblePixel(crop)) break;
                result.Add(rect);
            }
            return result;
        }

        private static bool HasVisiblePixel(BitmapSource source)
        {
            FormatConvertedBitmap converted = new FormatConvertedBitmap(
                source, PixelFormats.Bgra32, null, 0);
            int stride = converted.PixelWidth * 4;
            byte[] pixels = new byte[stride * converted.PixelHeight];
            converted.CopyPixels(pixels, stride, 0);
            for (int i = 3; i < pixels.Length; i += 4)
            {
                if (pixels[i] != 0) return true;
            }
            return false;
        }

        private static bool ReadGrid(Dictionary<string, object> manifest, BitmapSource sheet,
            out int cols, out int rows, out int frameWidth, out int frameHeight, out double fps)
        {
            cols = (int)Json.ToNumber(FirstValue(manifest, "cols", "columns"), 0);
            rows = (int)Json.ToNumber(FirstValue(manifest, "rows"), 0);
            frameWidth = (int)Json.ToNumber(FirstValue(manifest,
                "frameWidth", "cellWidth", "width"), 0);
            frameHeight = (int)Json.ToNumber(FirstValue(manifest,
                "frameHeight", "cellHeight", "height"), 0);
            fps = Json.ToNumber(FirstValue(manifest, "fps", "frameRate"), Fps);
            if (fps < 1) fps = Fps;
            if (fps > 60) fps = 60;
            if (sheet.PixelWidth == 1536 && sheet.PixelHeight == 1872)
            {
                cols = 8;
                rows = 9;
                frameWidth = 192;
                frameHeight = 208;
            }
            else if (sheet.PixelWidth == 1536 && sheet.PixelHeight == 2288)
            {
                cols = 8;
                rows = 11;
                frameWidth = 192;
                frameHeight = 208;
            }

            Dictionary<string, object> validation = Json.AsObject(
                FirstValue(manifest, "validationReport", "validation"));
            if (frameWidth < 1 || frameHeight < 1)
            {
                string cellSize = System.Convert.ToString(Json.Get(validation, "cellSize"),
                    CultureInfo.InvariantCulture);
                int split = cellSize.IndexOf('x');
                int parsedWidth;
                int parsedHeight;
                if (split > 0
                    && int.TryParse(cellSize.Substring(0, split), out parsedWidth)
                    && int.TryParse(cellSize.Substring(split + 1), out parsedHeight))
                {
                    frameWidth = parsedWidth;
                    frameHeight = parsedHeight;
                }
            }

            if (frameWidth > 0 && sheet.PixelWidth % frameWidth == 0)
                cols = sheet.PixelWidth / frameWidth;
            if (frameHeight > 0 && sheet.PixelHeight % frameHeight == 0)
                rows = sheet.PixelHeight / frameHeight;
            if (cols < 1)
            {
                if (sheet.PixelWidth == 1536) cols = 8;
                else if (sheet.PixelWidth % 8 == 0) cols = 8;
            }
            if (frameWidth < 1 && cols > 0 && sheet.PixelWidth % cols == 0)
                frameWidth = sheet.PixelWidth / cols;
            if (rows < 1 && frameHeight > 0 && sheet.PixelHeight % frameHeight == 0)
                rows = sheet.PixelHeight / frameHeight;
            if (rows < 1 && sheet.PixelWidth == 1536)
                rows = sheet.PixelHeight == 2288 ? 11 : 9;
            if (frameHeight < 1 && rows > 0 && sheet.PixelHeight % rows == 0)
                frameHeight = sheet.PixelHeight / rows;
            return cols > 0 && rows > 0 && frameWidth > 0 && frameHeight > 0
                && cols * frameWidth <= sheet.PixelWidth
                && rows * frameHeight <= sheet.PixelHeight;
        }

        private static Dictionary<string, object> ReadManifest(string path)
        {
            try
            {
                object parsed = Json.ReadAny(File.ReadAllText(path, Encoding.UTF8));
                return parsed as Dictionary<string, object>;
            }
            catch
            {
                return null;
            }
        }

        private static bool LooksLikeNativePack(Dictionary<string, object> manifest)
        {
            return Json.Get(manifest, "clips") != null || Json.Get(manifest, "states") != null;
        }

        private static object FirstValue(Dictionary<string, object> manifest, params string[] keys)
        {
            foreach (string key in keys)
            {
                object value = Json.Get(manifest, key);
                if (value != null) return value;
            }
            return null;
        }

        private static string FirstString(Dictionary<string, object> manifest, params string[] keys)
        {
            foreach (string key in keys)
            {
                string value = System.Convert.ToString(Json.Get(manifest, key), CultureInfo.InvariantCulture);
                if (!string.IsNullOrEmpty(value)) return value.Trim();
            }
            return "";
        }

        private static string ResolveInside(string root, string relative)
        {
            try
            {
                string fullRoot = Path.GetFullPath(root).TrimEnd('\\', '/');
                string full = Path.GetFullPath(Path.Combine(fullRoot,
                    relative.Replace('/', Path.DirectorySeparatorChar)));
                if (!full.StartsWith(fullRoot + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase)) return "";
                return full;
            }
            catch
            {
                return "";
            }
        }

        private static bool IsImage(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext == ".webp" || ext == ".png" || ext == ".bmp" || ext == ".jpg"
                || ext == ".jpeg";
        }

        private static string SafeId(string declared, string fallback)
        {
            string value = string.IsNullOrEmpty(declared) ? fallback : declared;
            StringBuilder result = new StringBuilder();
            foreach (char ch in value ?? "")
            {
                if ((ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z')
                    || (ch >= '0' && ch <= '9'))
                    result.Append(char.ToLowerInvariant(ch));
                else if ((ch == '-' || ch == '_' || ch == ' ' || ch == '.')
                    && result.Length > 0 && result[result.Length - 1] != '-')
                    result.Append('-');
            }
            string clean = result.ToString().Trim('-');
            return string.IsNullOrEmpty(clean) ? "sprite-atlas" : clean;
        }

        private static BitmapSource LoadBitmap(string path)
        {
            BitmapDecoder decoder = BitmapDecoder.Create(new Uri(path),
                BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count == 0) return null;
            BitmapFrame frame = decoder.Frames[0];
            frame.Freeze();
            return frame;
        }

        private static void SavePng(BitmapSource image, string path)
        {
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            using (FileStream stream = File.Create(path))
            {
                encoder.Save(stream);
            }
        }
    }
}
