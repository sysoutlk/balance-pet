// Converts a generic XML sprite animation package into our pet-pack format.
// The parser intentionally uses only neutral action names and file structure.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Xml;

namespace BalancePet
{
    internal static class XmlSpritePackConverter
    {
        private const double Fps = 8.0;
        private const double DefaultFrameMs = 125.0;

        private sealed class ParsedAction
        {
            public string Name;
            public string Key;
            public string Label;
            public string Type;
            public List<Pose> Poses = new List<Pose>();
        }

        private sealed class Pose
        {
            public string Image;
            public double DurationMs;
        }

        public static bool TryLocate(string source, out string actionsXml, out string imageDir,
            out string suggestedName)
        {
            actionsXml = "";
            imageDir = "";
            suggestedName = "xml-sprite";
            if (string.IsNullOrEmpty(source) || !Directory.Exists(source)) return false;
            string clean = Path.GetFullPath(source).TrimEnd('\\', '/');
            string folderName = Path.GetFileName(clean);
            if (folderName.StartsWith("sprite-", StringComparison.OrdinalIgnoreCase))
                suggestedName = folderName.ToLowerInvariant();
            List<string> xmlCandidates = new List<string>();
            xmlCandidates.Add(Path.Combine(clean, "actions.xml"));
            xmlCandidates.Add(Path.Combine(clean, "conf", "actions.xml"));
            DirectoryInfo cursor = new DirectoryInfo(clean);
            for (int depth = 0; cursor != null && depth < 4; depth++)
            {
                xmlCandidates.Add(Path.Combine(cursor.FullName, "conf", "actions.xml"));
                xmlCandidates.Add(Path.Combine(cursor.FullName, "actions.xml"));
                cursor = cursor.Parent;
            }
            foreach (string candidate in xmlCandidates)
            {
                if (!File.Exists(candidate)) continue;
                actionsXml = Path.GetFullPath(candidate);
                break;
            }
            if (string.IsNullOrEmpty(actionsXml))
            {
                try
                {
                    string[] found = Directory.GetFiles(clean, "actions.xml", SearchOption.AllDirectories);
                    if (found.Length > 0)
                    {
                        Array.Sort(found, StringComparer.OrdinalIgnoreCase);
                        actionsXml = found[0];
                    }
                }
                catch
                {
                }
            }
            if (string.IsNullOrEmpty(actionsXml)) return false;

            imageDir = FindImageDirectory(clean, actionsXml);
            if (string.IsNullOrEmpty(imageDir)) return false;
            return true;
        }

        public static bool Convert(string actionsXml, string imageDir, string target,
            out string note, out string error)
        {
            note = "";
            error = "";
            try
            {
                List<ParsedAction> source = ReadActions(actionsXml);
                if (source.Count == 0)
                {
                    error = "XML 里没有可用的精灵动作";
                    return false;
                }
                Directory.CreateDirectory(target);
                string clipsDir = Path.Combine(target, "clips");
                Directory.CreateDirectory(clipsDir);
                Dictionary<string, string> imageCache =
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                Dictionary<string, object> clips = new Dictionary<string, object>();
                Dictionary<string, object> actions = new Dictionary<string, object>();
                int totalFrames = 0;
                foreach (ParsedAction action in source)
                {
                    List<string> files = new List<string>();
                    foreach (Pose pose in action.Poses)
                    {
                        string file = ResolveImage(imageDir, pose.Image, imageCache);
                        if (string.IsNullOrEmpty(file)) continue;
                        double duration = pose.DurationMs;
                        if (duration < 40) duration = DefaultFrameMs;
                        if (duration > 4000) duration = 4000;
                        int repeat = Math.Max(1, (int)Math.Round(duration / DefaultFrameMs));
                        for (int i = 0; i < repeat; i++) files.Add(file);
                    }
                    if (files.Count == 0) continue;
                    string clipDir = Path.Combine(clipsDir, action.Key);
                    Directory.CreateDirectory(clipDir);
                    for (int i = 0; i < files.Count; i++)
                    {
                        string output = Path.Combine(clipDir,
                            i.ToString("0000", CultureInfo.InvariantCulture) + ".png");
                        File.Copy(files[i], output, true);
                    }
                    Dictionary<string, object> clip = new Dictionary<string, object>();
                    clip["path"] = "clips/" + action.Key;
                    clip["fps"] = Fps;
                    clip["loop"] = IsLoopingState(action.Name);
                    clips[action.Key] = clip;
                    totalFrames += files.Count;

                    if (IsStateOnly(action.Name)) continue;
                    Dictionary<string, object> item = new Dictionary<string, object>();
                    item["name"] = action.Label;
                    item["clips"] = new string[] { action.Key };
                    item["seconds"] = Math.Round(Math.Max(0.25, files.Count / Fps), 3);
                    item["weight"] = IsMovement(action.Type) ? 0 : 10;
                    item["walking"] = false;
                    item["category"] = IsMovement(action.Type) ? "move" : "idle";
                    actions[action.Key] = item;
                }

                Dictionary<string, object> states = new Dictionary<string, object>();
                AddState(states, "idle", FindKey(source, "Stand"));
                AddState(states, "walk", FindKey(source, "Walk"));
                AddState(states, "drag", FindKey(source, "Pinched"));
                AddState(states, "sleep", FindKey(source, "Sit"));
                AddState(states, "edge", FindKey(source, "GrabWall"));
                AddState(states, "peek", FindKey(source, "GrabCeiling"));
                if (!states.ContainsKey("idle"))
                {
                    foreach (ParsedAction first in source)
                    {
                        if (clips.ContainsKey(first.Key))
                        {
                            AddState(states, "idle", first.Key);
                            break;
                        }
                    }
                }

                Dictionary<string, object> manifest = new Dictionary<string, object>();
                manifest["id"] = Path.GetFileName(target.TrimEnd('\\', '/'));
                manifest["name"] = DisplayName(Path.GetFileName(target.TrimEnd('\\', '/')));
                Dictionary<string, object> display = new Dictionary<string, object>();
                display["height"] = 180.0;
                manifest["display"] = display;
                manifest["states"] = states;
                manifest["clips"] = clips;
                manifest["actions"] = actions;
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                File.WriteAllText(Path.Combine(target, "pet.json"),
                    serializer.Serialize(manifest), new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(target, "import-source.txt"),
                    "source=XML 精灵" + Environment.NewLine
                    + "frames=" + totalFrames.ToString(CultureInfo.InvariantCulture) + Environment.NewLine,
                    new UTF8Encoding(false));
                note = "XML 精灵转宠物包（" + actions.Count.ToString(CultureInfo.InvariantCulture)
                    + " 个动作，" + totalFrames.ToString(CultureInfo.InvariantCulture) + " 帧）";
                Log.Write("XML sprite import: " + note);
                return true;
            }
            catch (Exception ex)
            {
                error = "XML 精灵导入失败：" + ex.Message;
                return false;
            }
        }

        private static List<ParsedAction> ReadActions(string path)
        {
            List<ParsedAction> result = new List<ParsedAction>();
            XmlDocument doc = new XmlDocument();
            doc.Load(path);
            XmlNodeList nodes = doc.SelectNodes("//*[local-name()='Action' and @Name]");
            if (nodes == null) return result;
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> usedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int actionNumber = 0;
            foreach (XmlNode node in nodes)
            {
                ParsedAction action = new ParsedAction();
                action.Name = node.Attributes["Name"].Value;
                if (string.IsNullOrEmpty(action.Name) || seen.Contains(action.Name)) continue;
                action.Type = Attribute(node, "Type");
                action.Key = KnownActionKey(action.Name);
                if (IsUnsupportedActionName(action.Name))
                {
                    seen.Add(action.Name);
                    continue;
                }
                if (string.IsNullOrEmpty(action.Key)
                    && string.Equals(action.Type, "Embedded", StringComparison.OrdinalIgnoreCase))
                {
                    seen.Add(action.Name);
                    continue;
                }
                XmlNode animation = node.SelectSingleNode("./*[local-name()='Animation']");
                if (animation != null)
                {
                    XmlNodeList poses = animation.SelectNodes("./*[local-name()='Pose']");
                    if (poses != null)
                    {
                        foreach (XmlNode poseNode in poses)
                        {
                            string image = Attribute(poseNode, "Image");
                            if (string.IsNullOrEmpty(image)) continue;
                            Pose pose = new Pose();
                            pose.Image = image;
                            pose.DurationMs = Number(Attribute(poseNode, "Duration"), DefaultFrameMs);
                            action.Poses.Add(pose);
                        }
                    }
                }
                if (action.Poses.Count > 0)
                {
                    if (string.IsNullOrEmpty(action.Key))
                    {
                        do
                        {
                            actionNumber++;
                            action.Key = "action_" + actionNumber.ToString("00", CultureInfo.InvariantCulture);
                        }
                        while (usedKeys.Contains(action.Key));
                    }
                    action.Label = KnownActionLabel(action.Name);
                    if (string.IsNullOrEmpty(action.Label))
                    {
                        action.Label = "动作 " + actionNumber.ToString("00", CultureInfo.InvariantCulture);
                    }
                    usedKeys.Add(action.Key);
                    result.Add(action);
                    seen.Add(action.Name);
                }
            }
            return result;
        }

        private static bool IsUnsupportedActionName(string name)
        {
            return name.StartsWith("PullUp", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("Divide", StringComparison.OrdinalIgnoreCase);
        }

        private static string FindImageDirectory(string source, string actionsXml)
        {
            if (HasSprite(Path.Combine(source, "shime1.png"))) return source;
            string[] names = new string[] { "shime1.png", "shime01.png", "1.png" };
            string root = Path.GetDirectoryName(actionsXml);
            DirectoryInfo cursor = new DirectoryInfo(root);
            for (int depth = 0; cursor != null && depth < 4; depth++)
            {
                string img = Path.Combine(cursor.FullName, "img");
                if (Directory.Exists(img))
                {
                    string sourceName = new DirectoryInfo(source).Name;
                    string exact = Path.Combine(img, sourceName);
                    if (HasAnySprite(exact, names)) return exact;
                    foreach (string dir in Directory.GetDirectories(img))
                    {
                        if (HasAnySprite(dir, names)) return dir;
                    }
                }
                if (HasAnySprite(cursor.FullName, names)) return cursor.FullName;
                cursor = cursor.Parent;
            }
            return "";
        }

        private static bool HasAnySprite(string dir, string[] names)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return false;
            foreach (string name in names)
            {
                if (HasSprite(Path.Combine(dir, name))) return true;
            }
            return false;
        }

        private static bool HasSprite(string path)
        {
            return File.Exists(path);
        }

        private static string ResolveImage(string root, string requested,
            Dictionary<string, string> cache)
        {
            string name = Path.GetFileName((requested ?? "").Replace('\\', '/'));
            string cached;
            if (cache.TryGetValue(name, out cached)) return cached;
            string direct = Path.Combine(root, name);
            if (File.Exists(direct))
            {
                cache[name] = direct;
                return direct;
            }
            try
            {
                foreach (string file in Directory.GetFiles(root, name, SearchOption.AllDirectories))
                {
                    cache[name] = file;
                    return file;
                }
            }
            catch
            {
            }
            return "";
        }

        private static bool IsStateOnly(string name)
        {
            return string.Equals(name, "Stand", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Walk", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Pinched", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "GrabWall", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "GrabCeiling", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLoopingState(string name)
        {
            return string.Equals(name, "Stand", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Walk", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Sit", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Sprawl", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "GrabWall", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "GrabCeiling", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsMovement(string type)
        {
            return string.Equals(type, "Move", StringComparison.OrdinalIgnoreCase);
        }

        private static void AddState(Dictionary<string, object> states, string state, string key)
        {
            if (!string.IsNullOrEmpty(key)) states[state] = new string[] { key };
        }

        private static string FindKey(List<ParsedAction> actions, string name)
        {
            foreach (ParsedAction action in actions)
            {
                if (string.Equals(action.Name, name, StringComparison.OrdinalIgnoreCase))
                    return action.Key;
            }
            return "";
        }

        private static string Attribute(XmlNode node, string name)
        {
            return node.Attributes != null && node.Attributes[name] != null
                ? node.Attributes[name].Value
                : "";
        }

        private static double Number(string value, double fallback)
        {
            double parsed;
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture,
                out parsed) ? parsed : fallback;
        }

        private static string KnownActionKey(string name)
        {
            switch (name)
            {
                case "Stand": return "idle";
                case "Walk": return "walk";
                case "Run": return "run";
                case "Dash": return "dash";
                case "Sit": return "sit";
                case "SitAndLookUp": return "sit_look_up";
                case "SitAndLookAtMouse": return "sit_look_mouse";
                case "SitAndSpinHeadAction": return "spin_head";
                case "SitWithLegsUp": return "sit_legs_up";
                case "SitWithLegsDown": return "sit_legs_down";
                case "SitAndDangleLegs": return "swing_legs";
                case "Sprawl": return "lie_down";
                case "Creep": return "crawl";
                case "GrabCeiling": return "peek";
                case "ClimbCeiling": return "climb_ceiling";
                case "GrabWall": return "edge";
                case "ClimbWall": return "climb_wall";
                case "Jumping": return "jump";
                case "Falling": return "fall";
                case "Bouncing": return "bounce";
                case "Tripping": return "stumble";
                case "Pinched": return "drag";
                case "Resisting": return "struggle";
                default: return "";
            }
        }

        private static string KnownActionLabel(string name)
        {
            switch (name)
            {
                case "Stand": return "待机";
                case "Walk": return "行走";
                case "Run": return "奔跑";
                case "Dash": return "冲刺";
                case "Sit": return "坐下";
                case "SitAndLookUp": return "坐着抬头";
                case "SitAndLookAtMouse": return "坐着看鼠标";
                case "SitAndSpinHeadAction": return "摇头";
                case "SitWithLegsUp": return "抬腿坐";
                case "SitWithLegsDown": return "垂腿坐";
                case "SitAndDangleLegs": return "晃腿";
                case "Sprawl": return "趴下";
                case "Creep": return "匍匐";
                case "GrabCeiling": return "顶部探头";
                case "ClimbCeiling": return "顶部移动";
                case "GrabWall": return "贴边";
                case "ClimbWall": return "沿边移动";
                case "Jumping": return "跳跃";
                case "Falling": return "下落";
                case "Bouncing": return "弹跳";
                case "Tripping": return "绊倒";
                case "Pinched": return "拖拽";
                case "Resisting": return "挣扎";
                default: return "";
            }
        }

        private static string DisplayName(string id)
        {
            if (string.Equals(id, "sprite-a", StringComparison.OrdinalIgnoreCase))
                return "精灵素材 A";
            if (string.Equals(id, "sprite-b", StringComparison.OrdinalIgnoreCase))
                return "精灵素材 B";
            return "XML 精灵宠物";
        }
    }
}
