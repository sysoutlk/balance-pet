// Converts a folder-per-action asset set into our pet-pack format.
// Known folder names map to stable neutral keys; unknown folders become
// action_01, action_02 and never expose their source names in the UI.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace BalancePet
{
    internal static class ActionFolderPackConverter
    {
        private const double Fps = 8.0;

        private sealed class ActionFolder
        {
            public string SourceName;
            public string Key;
            public string Label;
            public string Category;
            public bool StateOnly;
            public bool Loop;
            public bool Walking;
            public List<string> Files = new List<string>();
        }

        public static bool TryLocate(string source, out string folder, out string suggestedName)
        {
            folder = "";
            suggestedName = "action-folder-pet";
            if (string.IsNullOrEmpty(source) || !Directory.Exists(source)) return false;
            string root = Path.GetFullPath(source).TrimEnd('\\', '/');
            if (File.Exists(Path.Combine(root, "pet.json"))) return false;
            try
            {
                int actionFolders = 0;
                bool hasIdle = false;
                foreach (string dir in Directory.GetDirectories(root))
                {
                    string name = Path.GetFileName(dir);
                    if (name.StartsWith(".")) continue;
                    List<string> files = ReadImages(dir);
                    if (files.Count == 0) continue;
                    actionFolders++;
                    string key = KnownKey(name);
                    if (key == "idle") hasIdle = true;
                }
                if (actionFolders < 2 || !hasIdle) return false;
                folder = root;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool Convert(string source, string target, out string note, out string error)
        {
            note = "";
            error = "";
            try
            {
                List<ActionFolder> folders = new List<ActionFolder>();
                HashSet<string> usedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                int actionNumber = 0;
                string[] directories = Directory.GetDirectories(source);
                Array.Sort(directories, StringComparer.OrdinalIgnoreCase);
                foreach (string dir in directories)
                {
                    string sourceName = Path.GetFileName(dir);
                    if (sourceName.StartsWith(".")) continue;
                    List<string> files = ReadImages(dir);
                    if (files.Count == 0) continue;
                    string key = KnownKey(sourceName);
                    string label = KnownLabel(sourceName);
                    if (string.IsNullOrEmpty(key))
                    {
                        do
                        {
                            actionNumber++;
                            key = "action_" + actionNumber.ToString("00", CultureInfo.InvariantCulture);
                        }
                        while (usedKeys.Contains(key));
                        label = "动作 " + actionNumber.ToString("00", CultureInfo.InvariantCulture);
                    }
                    if (usedKeys.Contains(key)) continue;
                    ActionFolder item = new ActionFolder();
                    item.SourceName = sourceName;
                    item.Key = key;
                    item.Label = label;
                    item.StateOnly = IsState(key);
                    item.Category = IsMove(key) ? "move" : "idle";
                    item.Loop = IsLoopingState(key);
                    item.Walking = !item.StateOnly && !IsMove(key);
                    item.Files.AddRange(files);
                    folders.Add(item);
                    usedKeys.Add(key);
                }

                bool hasIdle = false;
                foreach (ActionFolder item in folders)
                {
                    if (item.Key == "idle") hasIdle = true;
                }
                if (!hasIdle)
                {
                    error = "动作文件夹里没有 idle、stand 或“待机”目录";
                    return false;
                }

                Directory.CreateDirectory(target);
                string clipsDir = Path.Combine(target, "clips");
                Directory.CreateDirectory(clipsDir);
                Dictionary<string, object> clips = new Dictionary<string, object>();
                Dictionary<string, object> states = new Dictionary<string, object>();
                Dictionary<string, object> actions = new Dictionary<string, object>();
                int totalFrames = 0;
                foreach (ActionFolder item in folders)
                {
                    int frames;
                    string path;
                    if (!CopyAction(item, clipsDir, out frames, out path, out error)) return false;
                    Dictionary<string, object> clip = new Dictionary<string, object>();
                    clip["path"] = path;
                    clip["fps"] = Fps;
                    clip["loop"] = item.Loop;
                    clips[item.Key] = clip;
                    totalFrames += frames;

                    if (item.StateOnly)
                    {
                        states[item.Key] = new string[] { item.Key };
                    }
                    else
                    {
                        Dictionary<string, object> action = new Dictionary<string, object>();
                        action["name"] = item.Label;
                        action["clips"] = new string[] { item.Key };
                        action["seconds"] = Math.Round(Math.Max(0.25, frames / Fps), 3);
                        action["weight"] = 10;
                        action["walking"] = item.Walking;
                        action["category"] = item.Category;
                        actions[item.Key] = action;
                    }
                }

                Dictionary<string, object> display = new Dictionary<string, object>();
                display["height"] = 200.0;
                Dictionary<string, object> manifest = new Dictionary<string, object>();
                manifest["id"] = Path.GetFileName(target.TrimEnd('\\', '/'));
                manifest["name"] = "动作文件夹宠物";
                manifest["display"] = display;
                manifest["states"] = states;
                manifest["clips"] = clips;
                manifest["actions"] = actions;
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                File.WriteAllText(Path.Combine(target, "pet.json"),
                    serializer.Serialize(manifest), new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(target, "import-source.txt"),
                    "source=动作文件夹" + Environment.NewLine
                    + "frames=" + totalFrames.ToString(CultureInfo.InvariantCulture) + Environment.NewLine,
                    new UTF8Encoding(false));
                note = "动作文件夹转宠物包（" + actions.Count.ToString(CultureInfo.InvariantCulture)
                    + " 个动作，" + totalFrames.ToString(CultureInfo.InvariantCulture) + " 帧）";
                Log.Write("action folder import: " + note);
                return true;
            }
            catch (Exception ex)
            {
                error = "动作文件夹导入失败：" + ex.Message;
                return false;
            }
        }

        private static bool CopyAction(ActionFolder item, string clipsDir,
            out int frames, out string path, out string error)
        {
            frames = 0;
            path = "";
            error = "";
            int gifCount = 0;
            foreach (string file in item.Files)
            {
                if (string.Equals(Path.GetExtension(file), ".gif", StringComparison.OrdinalIgnoreCase))
                    gifCount++;
            }
            if (gifCount > 0 && item.Files.Count != 1)
            {
                error = "动作 " + item.Key + " 含有多个文件且包含 GIF；请改成纯图片序列或单个 GIF";
                return false;
            }
            if (gifCount == 1)
            {
                string gifTarget = Path.Combine(clipsDir, item.Key + ".gif");
                File.Copy(item.Files[0], gifTarget, true);
                path = "clips/" + item.Key + ".gif";
                frames = GifFrameCount(gifTarget);
                if (frames < 1)
                {
                    error = "GIF 无法解码：" + item.SourceName;
                    return false;
                }
                return true;
            }

            string outputDir = Path.Combine(clipsDir, item.Key);
            Directory.CreateDirectory(outputDir);
            int index = 0;
            foreach (string file in item.Files)
            {
                string extension = Path.GetExtension(file).ToLowerInvariant();
                if (extension != ".png" && extension != ".jpg" && extension != ".jpeg"
                    && extension != ".bmp")
                    continue;
                string output = Path.Combine(outputDir,
                    index.ToString("0000", CultureInfo.InvariantCulture) + extension);
                File.Copy(file, output, true);
                index++;
            }
            frames = index;
            path = "clips/" + item.Key;
            if (frames == 0)
            {
                error = "动作目录没有可用图片：" + item.SourceName;
                return false;
            }
            return true;
        }

        private static List<string> ReadImages(string directory)
        {
            List<string> files = new List<string>();
            string[] patterns = new string[] { "*.png", "*.gif", "*.jpg", "*.jpeg", "*.bmp" };
            foreach (string pattern in patterns) files.AddRange(Directory.GetFiles(directory, pattern));
            files.Sort(StringComparer.OrdinalIgnoreCase);
            return files;
        }

        private static int GifFrameCount(string path)
        {
            try
            {
                System.Windows.Media.Imaging.GifBitmapDecoder decoder =
                    new System.Windows.Media.Imaging.GifBitmapDecoder(new Uri(path),
                        System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat,
                        System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
                return decoder.Frames.Count;
            }
            catch
            {
                return 0;
            }
        }

        private static string Normalize(string name)
        {
            StringBuilder value = new StringBuilder();
            foreach (char ch in name ?? "")
            {
                if (char.IsLetterOrDigit(ch)) value.Append(char.ToLowerInvariant(ch));
            }
            return value.ToString();
        }

        private static string KnownKey(string name)
        {
            string key = Normalize(name);
            if (key == "idle" || key == "stand" || key == "default" || key == "待机"
                || key == "默认")
                return "idle";
            if (key == "walk" || key == "move" || key == "行走" || key == "走路")
                return "walk";
            if (key == "run" || key == "奔跑")
                return "run";
            if (key == "drag" || key == "pinch" || key == "拖拽" || key == "被提起")
                return "drag";
            if (key == "click" || key == "touch" || key == "点击" || key == "摸头")
                return "click";
            if (key == "sleep" || key == "tired" || key == "睡觉" || key == "睡眠")
                return "sleep";
            if (key == "edge" || key == "side" || key == "贴边")
                return "edge";
            if (key == "peek" || key == "look" || key == "探头")
                return "peek";
            if (key == "wave" || key == "挥手")
                return "wave";
            if (key == "jump" || key == "跳跃")
                return "jump";
            if (key == "fall" || key == "下落")
                return "fall";
            if (key == "eat" || key == "吃东西")
                return "eat";
            if (key == "drink" || key == "喝水")
                return "drink";
            if (key == "gift" || key == "拆礼物")
                return "gift";
            return "";
        }

        private static string KnownLabel(string name)
        {
            switch (KnownKey(name))
            {
                case "idle": return "待机";
                case "walk": return "行走";
                case "run": return "奔跑";
                case "drag": return "拖拽";
                case "click": return "点击回应";
                case "sleep": return "睡觉";
                case "edge": return "贴边";
                case "peek": return "探头";
                case "wave": return "挥手";
                case "jump": return "跳跃";
                case "fall": return "下落";
                case "eat": return "吃东西";
                case "drink": return "喝水";
                case "gift": return "拆礼物";
                default: return "";
            }
        }

        private static bool IsState(string key)
        {
            return key == "idle" || key == "walk" || key == "drag" || key == "click"
                || key == "sleep" || key == "edge" || key == "peek";
        }

        private static bool IsMove(string key)
        {
            return key == "walk" || key == "run";
        }

        private static bool IsLoopingState(string key)
        {
            return key == "idle" || key == "walk" || key == "sleep" || key == "edge"
                || key == "peek";
        }
    }
}
