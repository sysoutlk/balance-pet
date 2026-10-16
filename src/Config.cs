// Minimal JSON helpers (JavaScriptSerializer from System.Web.Extensions) plus the
// configuration model. The config file is plain JSON and intentionally shared with the
// earlier script-based prototype, so existing user settings keep working. API keys are
// stored DPAPI-encrypted ("dpapi:..." prefix) whenever they are written by the app.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace DshPet
{
    internal static class Json
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        public static Dictionary<string, object> ReadObject(string path)
        {
            try
            {
                string text = File.ReadAllText(path, Encoding.UTF8).TrimStart('\uFEFF');
                return ReadText(text);
            }
            catch
            {
                return new Dictionary<string, object>();
            }
        }

        public static Dictionary<string, object> ReadText(string text)
        {
            try
            {
                if (string.IsNullOrEmpty(text)) return new Dictionary<string, object>();
                Dictionary<string, object> value = Serializer.Deserialize<Dictionary<string, object>>(text.TrimStart('\uFEFF'));
                if (value == null) return new Dictionary<string, object>();
                return value;
            }
            catch
            {
                return new Dictionary<string, object>();
            }
        }

        public static void WriteObject(string path, object value)
        {
            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                File.WriteAllText(path, Serializer.Serialize(value), new UTF8Encoding(false));
            }
            catch
            {
                // Saving is best effort: a locked or read-only config must not kill the pet.
            }
        }

        public static double ToNumber(object value, double fallback)
        {
            if (value == null) return fallback;
            try
            {
                return Convert.ToDouble(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }

        public static double? ToNumberOrNull(object value)
        {
            if (value == null) return null;
            try
            {
                string text = Convert.ToString(value, CultureInfo.InvariantCulture);
                if (string.IsNullOrEmpty(text)) return null;
                double parsed;
                if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed)) return parsed;
            }
            catch
            {
            }
            return null;
        }

        public static object ReadAny(string text)
        {
            try
            {
                if (string.IsNullOrEmpty(text)) return null;
                return Serializer.DeserializeObject(text.TrimStart('\uFEFF'));
            }
            catch
            {
                return null;
            }
        }

        public static object Get(Dictionary<string, object> data, string key)
        {
            object value;
            if (data != null && data.TryGetValue(key, out value)) return value;
            return null;
        }

        /// <summary>
        /// Walks a compact JSON path: "data.total", "balance_infos[currency=CNY].total_balance",
        /// "items[0].balance". Used by the "custom" provider so users can point the pet at any
        /// OpenAI-compatible relay without touching the code.
        /// </summary>
        public static object Pick(object root, string path)
        {
            if (string.IsNullOrEmpty(path)) return root;
            object current = root;
            string[] parts = path.Split('.');
            for (int i = 0; i < parts.Length; i++)
            {
                if (current == null) return null;
                string raw = parts[i].Trim();
                if (raw.Length == 0) continue;
                string name = raw;
                string index = null;
                int bracket = raw.IndexOf('[');
                if (bracket >= 0)
                {
                    name = raw.Substring(0, bracket);
                    int close = raw.IndexOf(']', bracket);
                    if (close > bracket) index = raw.Substring(bracket + 1, close - bracket - 1);
                }
                if (name.Length > 0)
                {
                    Dictionary<string, object> dict = current as Dictionary<string, object>;
                    if (dict == null || !dict.ContainsKey(name)) return null;
                    current = dict[name];
                }
                if (!string.IsNullOrEmpty(index))
                {
                    if (index.IndexOf('=') >= 0)
                    {
                        int eq = index.IndexOf('=');
                        string wantKey = index.Substring(0, eq).Trim();
                        string wantValue = index.Substring(eq + 1).Trim().Trim('\'', '"');
                        object match = null;
                        foreach (object item in AsArray(current))
                        {
                            Dictionary<string, object> obj = item as Dictionary<string, object>;
                            if (obj == null) continue;
                            object probe;
                            if (obj.TryGetValue(wantKey, out probe)
                                && string.Equals(Convert.ToString(probe, CultureInfo.InvariantCulture), wantValue, StringComparison.OrdinalIgnoreCase))
                            {
                                match = item;
                                break;
                            }
                        }
                        current = match;
                    }
                    else
                    {
                        int n;
                        if (!int.TryParse(index, NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) return null;
                        List<object> list = AsArray(current);
                        if (n < 0 || n >= list.Count) return null;
                        current = list[n];
                    }
                }
            }
            return current;
        }

        public static bool ToBool(object value, bool fallback)
        {
            if (value == null) return fallback;
            if (value is bool) return (bool)value;
            try
            {
                return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }

        public static Dictionary<string, object> AsObject(object value)
        {
            Dictionary<string, object> dict = value as Dictionary<string, object>;
            return dict ?? new Dictionary<string, object>();
        }

        public static List<object> AsArray(object value)
        {
            object[] array = value as object[];
            if (array != null) return new List<object>(array);
            List<object> list = value as List<object>;
            return list ?? new List<object>();
        }
    }

    internal static class Secret
    {
        private const string Prefix = "dpapi:";

        /// <summary>Encrypts a secret for the current Windows user (DPAPI). Falls back to
        /// plain text when the OS call is unavailable, so the app never loses the key.</summary>
        public static string Protect(string plain)
        {
            if (string.IsNullOrEmpty(plain)) return plain;
            try
            {
                byte[] data = ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), null, DataProtectionScope.CurrentUser);
                return Prefix + Convert.ToBase64String(data);
            }
            catch
            {
                return plain;
            }
        }

        public static string Unprotect(string value)
        {
            if (string.IsNullOrEmpty(value) || !value.StartsWith(Prefix, StringComparison.Ordinal)) return value;
            try
            {
                byte[] data = ProtectedData.Unprotect(Convert.FromBase64String(value.Substring(Prefix.Length)), null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(data);
            }
            catch
            {
                return "";
            }
        }

        public static bool IsProtected(string value)
        {
            return !string.IsNullOrEmpty(value) && value.StartsWith(Prefix, StringComparison.Ordinal);
        }
    }

    internal sealed class Config
    {
        public string FilePath;
        public Dictionary<string, object> Raw = new Dictionary<string, object>();

        public static Config Load(string path)
        {
            Config config = new Config();
            config.FilePath = path;
            config.Raw = Json.ReadObject(path);
            config.ApplyDefaults();
            if (!File.Exists(path)) config.Save();
            return config;
        }

        public void Save()
        {
            Json.WriteObject(FilePath, Raw);
        }

        /// <summary>Resolves a dotted section path ("providers.deepseek"), creating missing
        /// levels when create is true.</summary>
        private Dictionary<string, object> Node(string path, bool create)
        {
            if (string.IsNullOrEmpty(path)) return Raw;
            Dictionary<string, object> current = Raw;
            string[] parts = path.Split('.');
            for (int i = 0; i < parts.Length; i++)
            {
                object next;
                Dictionary<string, object> child;
                if (current.TryGetValue(parts[i], out next))
                {
                    child = next as Dictionary<string, object>;
                    if (child == null)
                    {
                        if (!create) return null;
                        child = new Dictionary<string, object>();
                        current[parts[i]] = child;
                    }
                }
                else
                {
                    if (!create) return null;
                    child = new Dictionary<string, object>();
                    current[parts[i]] = child;
                }
                current = child;
            }
            return current;
        }

        private object GetAtRaw(string path)
        {
            if (path == null) return null;
            if (path.Length == 0) return Raw;
            int split = path.LastIndexOf('.');
            if (split < 0)
            {
                object value;
                if (Raw.TryGetValue(path, out value)) return value;
                return null;
            }
            Dictionary<string, object> node = Node(path.Substring(0, split), false);
            if (node == null) return null;
            object item;
            if (node.TryGetValue(path.Substring(split + 1), out item)) return item;
            return null;
        }

        public string GetStringAt(string path, string fallback)
        {
            object value = GetAtRaw(path);
            if (value == null) return fallback;
            string text = Convert.ToString(value, CultureInfo.InvariantCulture);
            return text == null ? fallback : text;
        }

        public double GetNumberAt(string path, double fallback)
        {
            return Json.ToNumber(GetAtRaw(path), fallback);
        }

        public bool GetFlagAt(string path, bool fallback)
        {
            return Json.ToBool(GetAtRaw(path), fallback);
        }

        public void SetAt(string path, object value)
        {
            int split = path.LastIndexOf('.');
            if (split < 0)
            {
                Raw[path] = value;
                return;
            }
            Dictionary<string, object> node = Node(path.Substring(0, split), true);
            node[path.Substring(split + 1)] = value;
        }

        private Dictionary<string, object> Section(string name)
        {
            return Json.AsObject(GetAtRaw(name));
        }

        public string GetString(string section, string key, string fallback)
        {
            object value;
            if (Section(section).TryGetValue(key, out value) && value != null) return Convert.ToString(value, CultureInfo.InvariantCulture);
            return fallback;
        }

        public double GetNumber(string section, string key, double fallback)
        {
            object value;
            if (Section(section).TryGetValue(key, out value)) return Json.ToNumber(value, fallback);
            return fallback;
        }

        public bool GetFlag(string section, string key, bool fallback)
        {
            object value;
            if (Section(section).TryGetValue(key, out value)) return Json.ToBool(value, fallback);
            return fallback;
        }

        public List<string> GetStrings(string section, string key)
        {
            object value;
            if (!Section(section).TryGetValue(key, out value)) return new List<string>();
            List<string> items = new List<string>();
            foreach (object item in Json.AsArray(value))
            {
                if (item != null) items.Add(Convert.ToString(item, CultureInfo.InvariantCulture));
            }
            return items;
        }

        public List<Dictionary<string, object>> GetObjects(string section, string key)
        {
            List<Dictionary<string, object>> items = new List<Dictionary<string, object>>();
            object value;
            if (!Section(section).TryGetValue(key, out value)) return items;
            foreach (object item in Json.AsArray(value))
            {
                items.Add(Json.AsObject(item));
            }
            return items;
        }

        public Dictionary<string, object> GetObject(string section, string key)
        {
            object value;
            if (!Section(section).TryGetValue(key, out value)) return new Dictionary<string, object>();
            return Json.AsObject(value);
        }

        public void Set(string section, string key, object value)
        {
            if (string.IsNullOrEmpty(section))
            {
                Raw[key] = value;
                return;
            }
            Dictionary<string, object> target = Section(section);
            target[key] = value;
            Raw[section] = target;
        }

        private void ApplyDefaults()
        {
            // Data sources. Everything is standalone: balances come from the provider APIs
            // configured below, nothing else on the machine is read.
            SetDefault("", "primaryProvider", "deepseek");
            SetDefault("", "statePath", "");
            SetDefault("", "apiKey", "");
            SetDefault("", "role", "");
            SetDefault("", "scale", 1.0);
            SetDefault("", "opacity", 1.0);
            SetDefault("", "maxClones", 6);
            SetDefault("", "dataUrl", "https://api.deepseek.com/user/balance");
            SetDefault("", "pollSeconds", 0);
            SetDefault("bubble", "seconds", 7);
            SetDefault("bubble", "pollSeconds", 60);

            // Provider toggles. Only endpoints with a documented balance API are on-by-default
            // candidates; everything ships off until the user configures a key.
            SetDefault("providers.deepseek", "on", true);
            SetDefault("providers.deepseek", "apiKey", "");
            SetDefault("providers.moonshot", "on", false);
            SetDefault("providers.moonshot", "apiKey", "");
            SetDefault("providers.moonshot", "baseUrl", "https://api.moonshot.cn");
            SetDefault("providers.siliconflow", "on", false);
            SetDefault("providers.siliconflow", "apiKey", "");
            SetDefault("providers.openrouter", "on", false);
            SetDefault("providers.openrouter", "apiKey", "");
            SetDefault("providers.custom", "on", false);
            SetDefault("providers.custom", "apiKey", "");
            SetDefault("custom", "name", "自定义接口");
            SetDefault("custom", "url", "");
            SetDefault("custom", "auth", "bearer");
            SetDefault("custom", "currency", "CNY");
            SetDefault("custom", "path", "");
            SetDefault("walk", "enabled", true);
            SetDefault("walk", "speedPxPerSec", 72.0);
            SetDefault("walk", "pauseMinSec", 8.0);
            SetDefault("walk", "pauseMaxSec", 26.0);
            SetDefault("walk", "marginPx", 12.0);
            SetDefault("walk", "flip", true);
            SetDefault("snap", "enabled", true);
            SetDefault("snap", "distancePx", 140.0);
            SetDefault("snap", "marginPx", 6.0);
            SetDefault("throw", "frictionPerSec", 0.35);
            SetDefault("throw", "bounce", 0.55);
            SetDefault("throw", "stopSpeedPxPerSec", 40.0);
            SetDefault("sleep", "enabled", true);
            SetDefault("sleep", "fromHour", 23);
            SetDefault("sleep", "toHour", 7);
            SetDefault("alert", "balanceWarn", true);
            SetDefault("alert", "budgetWarn", true);
            SetDefault("alert", "quipsOn", true);
            SetDefault("alert", "below", 0.0);
            SetDefault("alert", "budget", 0.0);
            SetDefault("actions", "enabled", true);
            SetDefault("actions", "pauseMinSec", 14.0);
            SetDefault("actions", "pauseMaxSec", 45.0);
            SetDefault("actions", "pool", new string[] { "hop", "wobble", "stretch", "spin", "dash", "greet" });
            SetDefault("quips", "greeting", new string[]
            {
                "今天也一起省点 token 吧～",
                "谷时半价，冲就完了",
                "余额我盯着的，你放心",
                "别熬夜了，熬夜真的很贵",
                "摸鱼可以，烧钱不行",
                "这波消费，值不值？"
            });
            SetDefault("quips", "sleep", new string[]
            {
                "呼……呼……",
                "（翻了个身）",
                "zzz……半夜便宜，我先睡了",
                "这点儿还干活？我睡了"
            });
            SetDefault("quips", "hop", new string[] { "嘿咻！", "跳一下～" });
            SetDefault("quips", "spin", new string[] { "转圈圈！", "晕了晕了" });
            SetDefault("quips", "wobble", new string[] { "左右摇摆～", "看我摇摆" });
            SetDefault("quips", "stretch", new string[] { "伸个懒腰～", "啊——舒服" });
            SetDefault("quips", "dash", new string[] { "冲刺！", "让一让！" });
            SetDefault("quips", "nap", new string[] { "眯一会儿…", "我先打个盹" });
            SetDefault("quips", "follow", new string[] { "跟着你～", "等等我！" });
            SetDefault("quips", "greet", new string[] { "你好呀！", "我在这儿～" });
        }

        private void SetDefault(string section, string key, object value)
        {
            if (string.IsNullOrEmpty(section))
            {
                object current;
                if (Raw.TryGetValue(key, out current) && current != null) return;
                Raw[key] = value;
                return;
            }
            object existing = GetAtRaw(section + "." + key);
            if (existing == null) SetAt(section + "." + key, value);
        }
    }
}
