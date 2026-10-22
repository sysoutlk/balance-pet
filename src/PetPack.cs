// A small, data-driven pet pack format.
//
// roles\<id>\pet.json
// {
//   "name": "My Pet",
//   "states": { "idle": ["idle"], "walk": ["walk"], "click": ["click"] },
//   "clips": {
//     "idle":  { "path": "idle",  "fps": 8, "loop": true },
//     "walk":  { "path": "walk",  "fps": 10, "loop": true },
//     "click": { "path": "click", "fps": 12, "loop": false }
//   },
//   "actions": {
//     "wave": { "name": "挥手", "clips": ["wave-a", "wave-b"], "seconds": 2.4,
//               "weight": 10, "walking": true, "category": "idle" }
//   },
//   "display": { "height": 220 },
//   "weights": { "idle": 70, "walk": 20, "click": 10 }
// }
//
// A pack without pet.json is also accepted: every subfolder named after a state
// (idle/walk/click/drag/...) becomes a clip with that name. This makes it easy to
// import existing frame folders without writing a manifest first.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace BalancePet
{
    internal sealed class PackClip
    {
        public string Name;
        public string Path;
        public double Fps = 8;
        public bool Loop = true;
        public FrameSet Frames;
        public int Width;
        public int Height;
        public double Duration
        {
            get
            {
                if (Frames == null || Frames.Empty) return 0.5;
                double fps = Frames.Fps < 1 ? 8 : Frames.Fps;
                return Frames.Frames.Count / fps;
            }
        }
    }

    internal sealed class PackAction
    {
        public string Key = "";
        public string Label = "";
        public string Category = "idle";
        public double Seconds = 2.0;
        public double Weight = 1.0;
        public bool Walking = true;
        public readonly List<string> Clips = new List<string>();
    }

    internal sealed class PetPack
    {
        public string Id = "";
        public string Name = "";
        public readonly Dictionary<string, List<string>> States = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, PackClip> Clips = new Dictionary<string, PackClip>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, double> Weights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, PackAction> Actions = new Dictionary<string, PackAction>(StringComparer.OrdinalIgnoreCase);
        public readonly List<string> ActionOrder = new List<string>();
        public double DisplayWidth;
        public double DisplayHeight;
        public bool DisplayUpscale;

        private static readonly string[] KnownStates = new string[]
        {
            "idle", "walk", "run", "sit", "sleep", "drag", "click", "turn",
            "edge", "jump", "fall", "wave", "greet", "nod", "shake", "hop",
            "bounce", "spin", "roll", "stretch", "yawn", "shy", "startle",
            "look", "lie", "pace", "peek", "stroll", "dash", "follow", "bump"
        };

        private static readonly HashSet<string> ReservedStates = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "idle", "walk", "sleep", "drag", "click",
            "edge", "peek", "fall", "thrown", "bump"
        };

        public bool HasActions { get { return Actions.Count > 0; } }

        public static PetPack TryLoad(Config cfg, string role)
        {
            if (string.IsNullOrEmpty(role)) return null;
            string root = Path.Combine(AppPaths.RolesDir, role);
            if (!Directory.Exists(root)) return null;
            string manifest = Path.Combine(root, "pet.json");
            if (!File.Exists(manifest))
            {
                // No manifest: accept a folder whose subfolders are state names.
                if (!HasStateFolders(root)) return null;
                PetPack auto = new PetPack();
                auto.Id = role;
                auto.Name = role;
                auto.ScanStateFolders(root);
                return auto;
            }
            try
            {
                Dictionary<string, object> raw = Json.ReadObject(manifest);
                PetPack pack = new PetPack();
                pack.Id = role;
                pack.Name = Json.Get(raw, "name") == null ? role : Convert.ToString(Json.Get(raw, "name"), CultureInfo.InvariantCulture);
                Dictionary<string, object> clips = Json.AsObject(Json.Get(raw, "clips"));
                foreach (KeyValuePair<string, object> pair in clips)
                {
                    Dictionary<string, object> item = Json.AsObject(pair.Value);
                    PackClip clip = new PackClip();
                    clip.Name = pair.Key;
                    string path = Json.Get(item, "path") == null ? pair.Key : Convert.ToString(Json.Get(item, "path"), CultureInfo.InvariantCulture);
                    clip.Path = string.IsNullOrEmpty(path) ? pair.Key : path;
                    clip.Fps = Json.ToNumber(Json.Get(item, "fps"), 8);
                    clip.Loop = Json.ToBool(Json.Get(item, "loop"), true);
                    if (clip.Fps < 1) clip.Fps = 1;
                    if (clip.Fps > 120) clip.Fps = 120;
                    pack.Clips[clip.Name] = clip;
                }
                if (pack.Clips.Count == 0) pack.ScanStateFolders(root);
                Dictionary<string, object> states = Json.AsObject(Json.Get(raw, "states"));
                foreach (KeyValuePair<string, object> pair in states)
                {
                    List<string> list = new List<string>();
                    foreach (object value in Json.AsArray(pair.Value))
                    {
                        if (value != null) list.Add(Convert.ToString(value, CultureInfo.InvariantCulture));
                    }
                    if (list.Count > 0) pack.States[pair.Key] = list;
                }
                if (pack.States.Count == 0)
                {
                    foreach (string name in pack.Clips.Keys) pack.States[name] = new List<string> { name };
                }
                Dictionary<string, object> weights = Json.AsObject(Json.Get(raw, "weights"));
                foreach (KeyValuePair<string, object> pair in weights)
                {
                    pack.Weights[pair.Key] = Json.ToNumber(pair.Value, 1);
                }
                Dictionary<string, object> display = Json.AsObject(Json.Get(raw, "display"));
                pack.DisplayWidth = Json.ToNumber(Json.Get(display, "width"),
                    Json.ToNumber(Json.Get(raw, "displayWidth"), 0));
                pack.DisplayHeight = Json.ToNumber(Json.Get(display, "height"),
                    Json.ToNumber(Json.Get(raw, "displayHeight"), 0));
                pack.DisplayUpscale = Json.ToBool(Json.Get(display, "upscale"), false);
                if (pack.DisplayWidth < 1) pack.DisplayWidth = 0;
                if (pack.DisplayHeight < 1) pack.DisplayHeight = 0;
                if (pack.DisplayWidth > 2000) pack.DisplayWidth = 2000;
                if (pack.DisplayHeight > 2000) pack.DisplayHeight = 2000;
                Dictionary<string, object> actions = Json.AsObject(Json.Get(raw, "actions"));
                foreach (KeyValuePair<string, object> pair in actions)
                {
                    List<string> stateClips;
                    pack.States.TryGetValue(pair.Key, out stateClips);
                    PackAction action = ParseAction(pair.Key, pair.Value, stateClips);
                    if (action == null) continue;
                    pack.Actions[action.Key] = action;
                    pack.ActionOrder.Add(action.Key);
                    if (action.Clips.Count > 0) pack.States[action.Key] = action.Clips;
                }
                if (!pack.States.ContainsKey("idle") && pack.Clips.Count > 0)
                {
                    string first = "";
                    foreach (string key in pack.Clips.Keys) { first = key; break; }
                    if (!string.IsNullOrEmpty(first)) pack.States["idle"] = new List<string> { first };
                }
                pack.AddImplicitActions();
                return pack.States.Count > 0 ? pack : null;
            }
            catch (Exception ex)
            {
                Log.Write("pack load failed: " + role + " " + ex.Message);
                return null;
            }
        }

        private static bool HasStateFolders(string root)
        {
            foreach (string dir in Directory.GetDirectories(root))
            {
                string name = Path.GetFileName(dir).ToLowerInvariant();
                foreach (string state in KnownStates)
                {
                    if (name == state) return true;
                }
            }
            return false;
        }

        private void ScanStateFolders(string root)
        {
            foreach (string dir in Directory.GetDirectories(root))
            {
                string name = Path.GetFileName(dir);
                string lower = name.ToLowerInvariant();
                if (lower == "preview" || lower == "clips" || name.StartsWith(".")) continue;
                PackClip clip = new PackClip();
                clip.Name = name;
                clip.Path = name;
                clip.Fps = 8;
                clip.Loop = lower == "idle" || lower == "walk" || lower == "sleep" || lower == "edge";
                Clips[name] = clip;
                States[name] = new List<string> { name };
            }
            List<string> rootImages = new List<string>();
            foreach (string file in Directory.GetFiles(root))
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext == ".gif" || ext == ".png" || ext == ".jpg" || ext == ".jpeg") rootImages.Add(file);
            }
            if (rootImages.Count > 0 && !States.ContainsKey("idle"))
            {
                // AnimMaker-style folders keep idle frames at the root and other states in subfolders.
                PackClip idle = new PackClip();
                idle.Name = "idle";
                idle.Path = rootImages.Count == 1 ? Path.GetFileName(rootImages[0]) : ".";
                idle.Fps = 8;
                idle.Loop = true;
                Clips["idle"] = idle;
                States["idle"] = new List<string> { "idle" };
            }
            AddImplicitActions();
        }

        private static PackAction ParseAction(string key, object value, List<string> fallbackClips)
        {
            if (string.IsNullOrEmpty(key)) return null;
            PackAction action = new PackAction();
            action.Key = key;

            string directClip = value as string;
            if (directClip != null)
            {
                action.Clips.Add(directClip);
                action.Weight = 10;
                return action;
            }
            Dictionary<string, object> item = value as Dictionary<string, object>;
            if (item == null)
            {
                foreach (object clip in Json.AsArray(value))
                {
                    string name = Convert.ToString(clip, CultureInfo.InvariantCulture);
                    if (!string.IsNullOrEmpty(name)) action.Clips.Add(name);
                }
                action.Weight = 10;
            }
            else
            {
                string label = Convert.ToString(Json.Get(item, "name"), CultureInfo.InvariantCulture);
                if (string.IsNullOrEmpty(label)) label = Convert.ToString(Json.Get(item, "label"), CultureInfo.InvariantCulture);
                if (!string.IsNullOrEmpty(label)) action.Label = label;

                string category = Convert.ToString(Json.Get(item, "category"), CultureInfo.InvariantCulture);
                if (string.IsNullOrEmpty(category)) category = Convert.ToString(Json.Get(item, "group"), CultureInfo.InvariantCulture);
                if (!string.IsNullOrEmpty(category)) action.Category = category.ToLowerInvariant();

                object seconds = Json.Get(item, "seconds");
                if (seconds == null) seconds = Json.Get(item, "duration");
                action.Seconds = Json.ToNumber(seconds, action.Seconds);
                action.Weight = Json.ToNumber(Json.Get(item, "weight"), action.Weight);
                action.Walking = Json.ToBool(Json.Get(item, "walking"),
                    !string.Equals(action.Category, "move", StringComparison.OrdinalIgnoreCase));

                object clipsValue = Json.Get(item, "clips");
                foreach (object clip in Json.AsArray(clipsValue))
                {
                    string name = Convert.ToString(clip, CultureInfo.InvariantCulture);
                    if (!string.IsNullOrEmpty(name)) action.Clips.Add(name);
                }
                if (action.Clips.Count == 0)
                {
                    string clip = Convert.ToString(Json.Get(item, "clip"), CultureInfo.InvariantCulture);
                    if (!string.IsNullOrEmpty(clip)) action.Clips.Add(clip);
                }
                if (action.Clips.Count == 0 && fallbackClips != null) action.Clips.AddRange(fallbackClips);
                if (action.Clips.Count == 0) action.Clips.Add(key);
            }

            if (action.Seconds < 0.2) action.Seconds = 0.2;
            if (action.Seconds > 120) action.Seconds = 120;
            if (action.Weight < 0) action.Weight = 0;
            if (action.Weight > 1000) action.Weight = 1000;
            if (string.IsNullOrEmpty(action.Category)) action.Category = "idle";
            return action.Clips.Count == 0 ? null : action;
        }

        private void AddImplicitActions()
        {
            List<string> names = new List<string>(States.Keys);
            foreach (string name in names)
            {
                if (Actions.ContainsKey(name) || ReservedStates.Contains(name)) continue;
                List<string> clips = States[name];
                if (clips == null || clips.Count == 0) continue;
                PackAction action = new PackAction();
                action.Key = name;
                action.Category = "idle";
                action.Seconds = 2.0;
                action.Weight = 10;
                action.Walking = true;
                action.Clips.AddRange(clips);
                Actions[name] = action;
                ActionOrder.Add(name);
            }
        }

        public List<string> StateClips(string state)
        {
            List<string> list;
            if (!string.IsNullOrEmpty(state) && States.TryGetValue(state, out list)) return list;
            if (States.TryGetValue("idle", out list)) return list;
            return null;
        }

        public PackClip Clip(string name)
        {
            PackClip clip;
            return !string.IsNullOrEmpty(name) && Clips.TryGetValue(name, out clip) ? clip : null;
        }

        public PackAction Action(string name)
        {
            PackAction action;
            return !string.IsNullOrEmpty(name) && Actions.TryGetValue(name, out action) ? action : null;
        }

        public List<string> ActionKeys()
        {
            List<string> keys = new List<string>();
            foreach (string key in ActionOrder)
            {
                if (Actions.ContainsKey(key)) keys.Add(key);
            }
            foreach (string key in Actions.Keys)
            {
                if (!keys.Contains(key)) keys.Add(key);
            }
            return keys;
        }

        public List<string> DefaultActionPool()
        {
            List<string> keys = new List<string>();
            foreach (string key in ActionKeys())
            {
                PackAction action = Actions[key];
                if (action.Weight > 0 && !ReservedStates.Contains(key)) keys.Add(key);
            }
            return keys;
        }

        public string PickAction(Random rng, bool walking, List<string> allowed, string avoid)
        {
            List<string> candidates = new List<string>();
            foreach (string key in ActionKeys())
            {
                if (allowed != null && !allowed.Contains(key)) continue;
                PackAction action = Actions[key];
                if (walking && !action.Walking) continue;
                if (allowed == null && action.Weight <= 0) continue;
                candidates.Add(key);
            }
            if (candidates.Count == 0) return "";
            if (candidates.Count > 1) candidates.Remove(avoid);
            return candidates[rng.Next(candidates.Count)];
        }
    }
}
