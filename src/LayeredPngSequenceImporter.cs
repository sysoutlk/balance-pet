// Converts a named PNG animation folder into this app's pet-pack format.
//
// The converter only reads a user-selected local asset folder. Delayed PNG
// sequences are expanded into 8 fps slots and mapped to our own action table.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BalancePet
{
    internal static class LayeredPngSequenceImporter
    {
        private const double Fps = 8.0;

        private sealed class Binding
        {
            public string Key;
            public string Label;
            public string Category;
            public string[] Folders;
            public bool CompositeLayers;
            public bool? WalkingOverride;

            public Binding(string key, string label, string category, params string[] folders)
            {
                Key = key;
                Label = label;
                Category = category;
                Folders = folders;
            }

            public static Binding Layered(string key, string label, string category,
                bool walking, string backFolder, string frontFolder)
            {
                Binding binding = new Binding(key, label, category, backFolder, frontFolder);
                binding.CompositeLayers = true;
                binding.WalkingOverride = walking;
                return binding;
            }
        }

        private static readonly Binding[] States = new Binding[]
        {
            new Binding("idle", "待机", "state", "Default/Nomal/1"),
            new Binding("walk", "走路", "state", "MOVE/walk.right/A_Nomal"),
            new Binding("drag", "被提起", "state", "Pinch/Nomal/A"),
            new Binding("click", "摸头", "state", "Touch_Head/Happy/A"),
            new Binding("sleep", "睡觉", "state", "Sleep/A_Nomal"),
            new Binding("edge", "贴边", "state", "SideHide_Left_Main/Nomal/A"),
            new Binding("peek", "探头", "state", "SideHide_Left_Rise/Nomal/A")
        };

        private static readonly Binding[] Actions = new Binding[]
        {
            new Binding("birthday", "生日庆祝", "idle", "BDay/B"),
            Binding.Layered("drink", "喝水", "idle", false,
                "Drink/Nomal/back_lay", "Drink/front_lay"),
            Binding.Layered("eat", "吃东西", "idle", false,
                "Eat/Nomal/back_lay", "Eat/Nomal/front_lay"),
            Binding.Layered("gift", "拆礼物", "idle", false,
                "Gift/Nomal/back_lay", "Gift/Nomal/front_lay"),
            new Binding("level_up", "升级", "fx", "LevelUP/Nomal"),
            new Binding("music", "听音乐", "idle", "Music/Single/Nomal"),
            new Binding("raise", "被举起", "idle",
                "Raise/Raised_Static/A_Nomal",
                "Raise/Raised_Static/B_Nomal",
                "Raise/Raised_Static/C_Nomal"),
            new Binding("say_self", "自语", "idle",
                "Say/Self/A", "Say/Self/B_1", "Say/Self/C"),
            new Binding("say_serious", "认真说话", "idle",
                "Say/Serious/A", "Say/Serious/B", "Say/Serious/C"),
            new Binding("say_shining", "闪亮登场", "fx",
                "Say/Shining/A", "Say/Shining/B_1", "Say/Shining/C"),
            new Binding("say_shy", "害羞", "idle",
                "Say/Shy/A", "Say/Shy/B", "Say/Shy/C"),
            new Binding("shutdown", "关机", "idle", "Shutdown/Nomal_1"),
            new Binding("startup", "启动", "fx", "StartUP/Nomal"),
            new Binding("state_one", "状态一", "idle",
                "State/StateONE/A_Nomal", "State/StateONE/B_Nomal/1", "State/StateONE/C_Nomal"),
            new Binding("state_two", "状态二", "idle",
                "State/StateTWO/A_Nomal", "State/StateTWO/B_Nomal", "State/StateTWO/C_Nomal"),
            new Binding("switch_up", "切换向上", "move", "Switch/Up/Nomal"),
            new Binding("switch_down", "切换向下", "move", "Switch/Down/Nomal"),
            new Binding("think", "思考", "idle",
                "Think/Nomal/A", "Think/Nomal/B", "Think/Nomal/B_2", "Think/Nomal/C"),
            new Binding("touch_body", "摸身体", "idle", "Touch_Body/B_Happy/tb1"),
            new Binding("touch_head", "摸头回应", "idle",
                "Touch_Head/Happy/A", "Touch_Head/Happy/B", "Touch_Head/Happy/C"),
            new Binding("calligraphy", "写书法", "idle", "WORK/Calligraphy/Nomal/A"),
            new Binding("fix_menu", "修屏幕", "idle", "WORK/FixMenu/Nomal/A"),
            new Binding("cooking", "做烧烤", "idle", "WORK/GrilledSausage/Nomal/A"),
            new Binding("play_game", "玩游戏", "idle", "WORK/PlayONE/Nomal/A"),
            new Binding("play_water", "玩水", "idle", "WORK/PlayWater/Nomal/A"),
            new Binding("clean", "打扫", "idle",
                "WORK/WorkClean/Nomal/A", "WORK/WorkClean/Nomal/B_3", "WORK/WorkClean/Nomal/C"),
            new Binding("computer", "用电脑", "idle", "WORK/WorkONE/A_Nomal"),
            new Binding("study", "学习", "idle", "WORK/Study/A_Nomal"),
            new Binding("paint", "画画", "idle", "WORK/StudyPaint/Nomal/A"),
            new Binding("rope_skipping", "跳绳", "move",
                "WORK/RopeSkipping/Nomal/A", "WORK/RopeSkipping/Nomal/B/1", "WORK/RopeSkipping/Nomal/C"),
            new Binding("idle_bubbles", "吐泡泡", "idle", "IDEL/Bubbles/A"),
            new Binding("idle_meow", "喵喵", "idle", "IDEL/Meow/Nomal/1"),
            new Binding("idle_squat", "蹲下", "idle",
                "IDEL/Squat/A_Nomal", "IDEL/Squat/B_Nomal/1", "IDEL/Squat/C_Nomal"),
            new Binding("idle_tennis", "打网球", "move",
                "IDEL/Tennis/Nomal/A", "IDEL/Tennis/Nomal/B", "IDEL/Tennis/Nomal/C"),
            new Binding("idle_aside", "东张西望", "idle",
                "IDEL/aside/Nomal/A", "IDEL/aside/Nomal/B",
                "IDEL/aside/Nomal/B_2", "IDEL/aside/Nomal/C"),
            new Binding("idle_yawning", "打哈欠", "idle", "IDEL/yawning/Nomal"),
            new Binding("climb_left", "向左爬", "move",
                "MOVE/climb.left/A_Nomal", "MOVE/climb.left/B_Nomal", "MOVE/climb.left/C_Nomal"),
            new Binding("climb_right", "向右爬", "move",
                "MOVE/climb.right/A_Nomal", "MOVE/climb.right/B_Nomal", "MOVE/climb.right/C_Nomal"),
            new Binding("crawl_left", "向左匍匐", "move",
                "MOVE/crawl.left/A_Nomal", "MOVE/crawl.left/B_Nomal", "MOVE/crawl.left/C_Nomal"),
            new Binding("crawl_right", "向右匍匐", "move",
                "MOVE/crawl.right/A_Nomal", "MOVE/crawl.right/B_Nomal", "MOVE/crawl.right/C_Nomal")
        };

        public static bool TryLocate(string source, out string sequenceDir, out string suggestedName)
        {
            sequenceDir = "";
            suggestedName = "layered-png-sequence";
            List<string> candidates = new List<string>();
            string clean = (source ?? "").TrimEnd('\\', '/');
            if (string.IsNullOrEmpty(clean) || !Directory.Exists(clean)) return false;
            candidates.Add(clean);
            candidates.Add(Path.Combine(clean, "pet"));
            try
            {
                foreach (string dir in Directory.GetDirectories(clean))
                {
                    if (!candidates.Contains(dir)) candidates.Add(dir);
                }
            }
            catch
            {
            }
            foreach (string candidate in candidates)
            {
                if (HasAnimationSet(candidate))
                {
                    sequenceDir = Path.GetFullPath(candidate);
                    return true;
                }
            }
            try
            {
                foreach (string marker in Directory.GetFiles(clean, "*.lps", SearchOption.AllDirectories))
                {
                    string parent = Path.GetDirectoryName(marker);
                    if (HasAnimationSet(parent))
                    {
                        sequenceDir = Path.GetFullPath(parent);
                        return true;
                    }
                    foreach (string child in Directory.GetDirectories(parent))
                    {
                        if (!HasAnimationSet(child)) continue;
                        sequenceDir = Path.GetFullPath(child);
                        return true;
                    }
                }
            }
            catch
            {
            }
            return false;
        }

        public static bool Import(string sequenceDir, string target, out string note, out string error)
        {
            note = "";
            error = "";
            try
            {
                if (!HasAnimationSet(sequenceDir))
                {
                    error = "PNG 动画目录无效";
                    return false;
                }
                Directory.CreateDirectory(target);
                string clipsDir = Path.Combine(target, "clips");
                Directory.CreateDirectory(clipsDir);
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                Dictionary<string, object> clips = new Dictionary<string, object>();
                Dictionary<string, object> states = new Dictionary<string, object>();
                Dictionary<string, object> actions = new Dictionary<string, object>();
                int totalSlots = 0;
                int actionCount = 0;
                bool hasIdle = false;

                foreach (Binding binding in States)
                {
                    int slots;
                    if (!AddClip(sequenceDir, clipsDir, binding, true, clips, out slots, out error))
                    {
                        if (string.Equals(binding.Key, "idle", StringComparison.OrdinalIgnoreCase))
                            return false;
                        error = "";
                        continue;
                    }
                    states[binding.Key] = new string[] { binding.Key };
                    if (string.Equals(binding.Key, "idle", StringComparison.OrdinalIgnoreCase))
                        hasIdle = true;
                    totalSlots += slots;
                }
                if (!hasIdle)
                {
                    error = "分层序列缺少 idle 动画";
                    return false;
                }
                foreach (Binding binding in Actions)
                {
                    int slots;
                    if (!AddClip(sequenceDir, clipsDir, binding, false, clips, out slots, out error))
                    {
                        error = "";
                        continue;
                    }
                    Dictionary<string, object> action = new Dictionary<string, object>();
                    action["name"] = binding.Label;
                    action["clips"] = new string[] { binding.Key };
                    action["seconds"] = Math.Round(Math.Max(0.4, slots / Fps), 3);
                    action["weight"] = 10;
                    action["walking"] = binding.WalkingOverride.HasValue
                        ? binding.WalkingOverride.Value
                        : !string.Equals(binding.Category, "move", StringComparison.OrdinalIgnoreCase);
                    action["category"] = binding.Category;
                    actions[binding.Key] = action;
                    actionCount++;
                    totalSlots += slots;
                }

                Dictionary<string, object> display = new Dictionary<string, object>();
                display["height"] = 220.0;
                Dictionary<string, object> manifest = new Dictionary<string, object>();
                manifest["id"] = Path.GetFileName(target.TrimEnd('\\', '/'));
                manifest["name"] = "分层 PNG 序列宠物";
                manifest["display"] = display;
                manifest["states"] = states;
                manifest["clips"] = clips;
                manifest["actions"] = actions;
                File.WriteAllText(Path.Combine(target, "pet.json"), serializer.Serialize(manifest),
                    new UTF8Encoding(false));
                note = "PNG 序列转宠物包（" + actionCount.ToString(CultureInfo.InvariantCulture)
                    + " 个动作，" + totalSlots.ToString(CultureInfo.InvariantCulture) + " 帧）";
                File.WriteAllText(Path.Combine(target, "import-source.txt"),
                    "source=PNG 序列" + Environment.NewLine + "mode=" + note + Environment.NewLine,
                    new UTF8Encoding(false));
                Log.Write("PNG sequence import: " + note);
                return true;
            }
            catch (Exception ex)
            {
                error = "PNG 序列导入失败：" + ex.Message;
                return false;
            }
        }

        private static bool AddClip(string sequenceDir, string clipsDir, Binding binding, bool loop,
            Dictionary<string, object> clips, out int slots, out string error)
        {
            slots = 0;
            error = "";
            if (binding.CompositeLayers)
            {
                return AddLayeredClip(sequenceDir, clipsDir, binding, loop,
                    clips, out slots, out error);
            }
            List<string> files = new List<string>();
            foreach (string folder in binding.Folders)
            {
                string source = Path.Combine(sequenceDir, folder.Replace('/', Path.DirectorySeparatorChar));
                if (!Directory.Exists(source))
                {
                    error = "缺少动画目录：" + folder;
                    return false;
                }
                string[] found = Directory.GetFiles(source, "*.png");
                Array.Sort(found, StringComparer.OrdinalIgnoreCase);
                files.AddRange(found);
            }
            if (files.Count == 0)
            {
                error = "动画目录里没有 PNG：" + binding.Key;
                return false;
            }

            string outputDir = Path.Combine(clipsDir, binding.Key);
            Directory.CreateDirectory(outputDir);
            int index = 0;
            foreach (string file in files)
            {
                int delayMs = 125;
                Match match = Regex.Match(Path.GetFileNameWithoutExtension(file), "_(\\d+)$");
                if (match.Success)
                {
                    int parsed;
                    if (int.TryParse(match.Groups[1].Value, NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out parsed)) delayMs = parsed;
                }
                if (delayMs < 40) delayMs = 40;
                if (delayMs > 2000) delayMs = 2000;
                int repeat = Math.Max(1, (int)Math.Round(delayMs / 125.0));
                for (int copy = 0; copy < repeat; copy++)
                {
                    string targetFile = Path.Combine(outputDir,
                        index.ToString("0000", CultureInfo.InvariantCulture) + ".png");
                    File.Copy(file, targetFile, true);
                    index++;
                }
            }
            Dictionary<string, object> clip = new Dictionary<string, object>();
            clip["path"] = "clips/" + binding.Key;
            clip["fps"] = Fps;
            clip["loop"] = loop;
            clips[binding.Key] = clip;
            slots = index;
            return true;
        }

        private static bool AddLayeredClip(string sequenceDir, string clipsDir, Binding binding,
            bool loop, Dictionary<string, object> clips, out int slots, out string error)
        {
            slots = 0;
            error = "";
            try
            {
                List<string> back = ExpandFrames(sequenceDir, binding.Folders[0]);
                List<string> front = ExpandFrames(sequenceDir, binding.Folders[1]);
                if (back.Count == 0 || front.Count == 0)
                {
                    error = "分层动画缺少帧：" + binding.Key;
                    return false;
                }
                string outputDir = Path.Combine(clipsDir, binding.Key);
                Directory.CreateDirectory(outputDir);
                int count = Math.Max(back.Count, front.Count);
                for (int i = 0; i < count; i++)
                {
                    BitmapSource backFrame = LoadFrame(back[Math.Min(i, back.Count - 1)]);
                    BitmapSource frontFrame = LoadFrame(front[Math.Min(i, front.Count - 1)]);
                    BitmapSource merged = ComposeFrames(backFrame, frontFrame);
                    string output = Path.Combine(outputDir,
                        i.ToString("0000", CultureInfo.InvariantCulture) + ".png");
                    SavePng(merged, output);
                }
                Dictionary<string, object> clip = new Dictionary<string, object>();
                clip["path"] = "clips/" + binding.Key;
                clip["fps"] = Fps;
                clip["loop"] = loop;
                clips[binding.Key] = clip;
                slots = count;
                return true;
            }
            catch (Exception ex)
            {
                error = "分层动画转换失败：" + binding.Key + " " + ex.Message;
                return false;
            }
        }

        private static List<string> ExpandFrames(string sequenceDir, string folder)
        {
            string source = Path.Combine(sequenceDir, folder.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(source)) return new List<string>();
            string[] found = Directory.GetFiles(source, "*.png");
            Array.Sort(found, StringComparer.OrdinalIgnoreCase);
            List<string> expanded = new List<string>();
            foreach (string file in found)
            {
                int delayMs = 125;
                Match match = Regex.Match(Path.GetFileNameWithoutExtension(file), "_(\\d+)$");
                int parsed;
                if (match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out parsed))
                {
                    delayMs = parsed;
                }
                if (delayMs < 40) delayMs = 40;
                if (delayMs > 2000) delayMs = 2000;
                int repeat = Math.Max(1, (int)Math.Round(delayMs / 125.0));
                for (int i = 0; i < repeat; i++) expanded.Add(file);
            }
            return expanded;
        }

        private static BitmapSource LoadFrame(string path)
        {
            BitmapImage bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(path);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }

        private static BitmapSource ComposeFrames(BitmapSource back, BitmapSource front)
        {
            int width = Math.Max(back.PixelWidth, front.PixelWidth);
            int height = Math.Max(back.PixelHeight, front.PixelHeight);
            DrawingVisual visual = new DrawingVisual();
            using (DrawingContext context = visual.RenderOpen())
            {
                double backX = (width - back.PixelWidth) / 2.0;
                double backY = (height - back.PixelHeight) / 2.0;
                double frontX = (width - front.PixelWidth) / 2.0;
                double frontY = (height - front.PixelHeight) / 2.0;
                context.DrawImage(back, new Rect(backX, backY, back.PixelWidth, back.PixelHeight));
                context.DrawImage(front, new Rect(frontX, frontY, front.PixelWidth, front.PixelHeight));
            }
            RenderTargetBitmap output = new RenderTargetBitmap(
                width, height, 96, 96, PixelFormats.Pbgra32);
            output.Render(visual);
            output.Freeze();
            return output;
        }

        private static void SavePng(BitmapSource image, string path)
        {
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                encoder.Save(stream);
            }
        }

        private static bool HasAnimationSet(string directory)
        {
            return !string.IsNullOrEmpty(directory)
                && Directory.Exists(directory)
                && Directory.Exists(Path.Combine(directory, "Default"))
                && Directory.Exists(Path.Combine(directory, "MOVE"));
        }
    }
}
