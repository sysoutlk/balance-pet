// Minimal JSON helpers (JavaScriptSerializer from System.Web.Extensions) plus the
// configuration model. The config file is plain JSON and intentionally shared with the
// earlier script-based prototype, so existing user settings keep working. API keys are
// stored DPAPI-encrypted ("dpapi:..." prefix) whenever they are written by the app.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
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
            // JavaScriptSerializer hands arrays back as ArrayList, so anything enumerable that
            // is not a string or a key/value map counts as a list here.
            List<object> items = new List<object>();
            if (value == null) return items;
            if (value is string) return items;
            if (value is System.Collections.IDictionary) return items;
            System.Collections.IEnumerable sequence = value as System.Collections.IEnumerable;
            if (sequence == null) return items;
            foreach (object item in sequence) items.Add(item);
            return items;
        }
    }

    internal static class Log
    {
        private const long MaxBytes = 512 * 1024;

        /// <summary>Appends a line to pet-log.txt (best effort, keeps the file small).</summary>
        public static void Write(string message)
        {
            try
            {
                string path = Path.Combine(AppPaths.Root, "pet-log.txt");
                if (File.Exists(path))
                {
                    FileInfo info = new FileInfo(path);
                    if (info.Length > MaxBytes) File.Delete(path);
                }
                File.AppendAllText(path,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "  " + message + Environment.NewLine,
                    new UTF8Encoding(false));
            }
            catch
            {
            }
        }
    }

    /// <summary>
    /// Shared HTTP plumbing. By default requests go direct (the .NET default would pick up a
    /// stale system proxy and stall); setting network.proxy in pet-config.json routes them
    /// through that proxy instead, which is handy when the OpenAI/Codex route flaps.
    /// </summary>
    internal static class Http
    {
        private static readonly Dictionary<string, HttpClient> cache = new Dictionary<string, HttpClient>();

        public static HttpClient For(Config cfg, int timeoutSeconds)
        {
            string proxy = cfg == null ? "" : cfg.GetStringAt("network.proxy", "");
            string key = proxy + "|" + timeoutSeconds;
            HttpClient existing;
            if (cache.TryGetValue(key, out existing)) return existing;
            try
            {
                System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
            }
            catch
            {
            }
            HttpClientHandler handler = new HttpClientHandler();
            try
            {
                if (string.IsNullOrEmpty(proxy))
                {
                    handler.UseProxy = false;
                }
                else
                {
                    handler.Proxy = new System.Net.WebProxy(proxy);
                    handler.UseProxy = true;
                }
            }
            catch
            {
            }
            HttpClient client = new HttpClient(handler);
            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            cache[key] = client;
            return client;
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
            SetDefault("network", "proxy", "");
            SetDefault("ui", "title", "余额桌宠");   // 设置卡片标题（不叫 dsh：应用是多平台的）
            SetDefault("frames", "activePerSecond", 25.0);   // 动作/散步时的帧率（高性能可拉到 60~120）
            SetDefault("frames", "idlePerSecond", 4.0);      // 闲置时的帧率（省电）
            SetDefault("", "role", "");
            SetDefault("roles", "random", true);
            SetDefault("", "scale", 1.0);
            SetDefault("", "opacity", 1.0);
            SetDefault("", "maxClones", 6);
            SetDefault("", "dataUrl", "https://api.deepseek.com/user/balance");
            SetDefault("", "pollSeconds", 0);
            SetDefault("bubble", "seconds", 7);
            SetDefault("bubble", "pollSeconds", 60);
            SetDefault("bubble", "style", "light");
            SetDefault("bubble", "theme", "");
            SetDefault("bubble", "accent", "#4B6BFB");
            SetDefault("bubble", "effect", "off");
            SetDefault("bubble", "color1", "#4B6BFB");
            SetDefault("bubble", "color2", "#A855F7");
            SetDefault("bubble", "colors", new string[0]);   // 可选：["#4B6BFB","#22D3EE",...] 自定义混色
            SetDefault("bubble", "mixMax", 6.0);             // 混色上限：超了顶掉最早设置的（FIFO）
            SetDefault("bubble", "speed", 1.0);
            SetDefault("bubble", "effectDepth", 1.0);
            SetDefault("bubble", "effectReverse", false);
            SetDefault("bubble", "fontScale", 1.0);
            SetDefault("bubble", "width", 196.0);
            SetDefault("bubble", "radius", 16.0);
            SetDefault("bubble", "opacity", 1.0);
            SetDefault("bubble", "shadow", 0.28);
            SetDefault("bubble", "borderWidth", 1.0);
            SetDefault("bubble", "tailSize", 13.0);
            SetDefault("bubble", "cardColor", "");
            SetDefault("bubble", "textColor", "");
            SetDefault("bubble", "font", "");         // "" = follow the system UI font
            SetDefault("bubble", "numberFont", "");   // "" = follow the same font
            SetDefault("bubble", "trend", true);
            SetDefault("bubble", "tail", true);

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
            // Coding-plan usage sources (5-hour / weekly windows). All ship off; each row in
            // the settings card takes its own key (Codex reuses the local CLI login).
            foreach (string planId in new string[] { "kimiCode", "codex", "zhipu", "zai", "minimax", "opencode", "custom" })
            {
                SetDefault("plans." + planId, "on", false);
                SetDefault("plans." + planId, "apiKey", "");
            }
            SetDefault("plans.custom", "url", "");
            SetDefault("plans.custom", "path", "");
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
            SetDefault("throw", "minSpeedPxPerSec", 900.0);   // 低于这个速度松手 = 放下，不抛出
            SetDefault("throw", "carryFactor", 0.72);        // 撞到别的桌宠时，递出去的动量比例
            SetDefault("pets", "collide", true);             // 多桌宠互相碰撞 / 被撞的会被带着滑一程
            SetDefault("sleep", "enabled", true);
            SetDefault("sleep", "fromHour", 23);
            SetDefault("sleep", "toHour", 7);
            SetDefault("edgeHide", "enabled", true);
            SetDefault("edgeHide", "delaySec", 10.0);
            SetDefault("edgeHide", "peekPx", 14.0);
            SetDefault("clones", "auto", true);
            SetDefault("clones", "max", 6.0);
            SetDefault("clones", "min", 1.0);
            SetDefault("clones", "cycle", true);   // 到上限后慢慢收回，再慢慢涨回
            SetDefault("clones", "pauseMinSec", 60.0);
            SetDefault("clones", "pauseMaxSec", 180.0);
            SetDefault("alert", "balanceWarn", true);
            SetDefault("alert", "budgetWarn", true);
            SetDefault("alert", "quotaWarn", true);
            SetDefault("alert", "quipsOn", true);
            SetDefault("alert", "below", 0.0);
            SetDefault("alert", "budget", 0.0);
            // 老版本只有一个全局阈值：迁到 DeepSeek 那一行（现在按平台分别设），
            // 迁完把旧键清零，免得下次启动把用户改过的值又搬回去。
            double legacyBelow = GetNumberAt("alert.below", 0);
            if (legacyBelow > 0)
            {
                if (GetNumberAt("alerts.deepseek.below", 0) <= 0) SetAt("alerts.deepseek.below", legacyBelow);
                SetAt("alert.below", 0.0);
            }
            double legacyBudget = GetNumberAt("alert.budget", 0);
            if (legacyBudget > 0)
            {
                if (GetNumberAt("alerts.deepseek.budget", 0) <= 0) SetAt("alerts.deepseek.budget", legacyBudget);
                SetAt("alert.budget", 0.0);
            }
            SetDefault("actions", "enabled", true);
            SetDefault("actions", "pauseMinSec", 14.0);
            SetDefault("actions", "pauseMaxSec", 45.0);
            SetDefault("actions", "pool", new string[]
            {
                "hop", "bounce", "spin", "wobble", "nod", "shake", "wave", "clap",
                "stretch", "yawn", "shy", "startle", "look", "greet"
            });
            // 台词库 v2：默认池统一放在 QuipLibrary()，顺便按版本号做一次平滑升级
            //（老配置里还是短池子的换成加量版，自己写长了的不动）。
            double quipVersion = GetNumberAt("quips.version", 1);
            foreach (KeyValuePair<string, string[]> pair in QuipLibrary())
            {
                SetDefault("quips", pair.Key, pair.Value);
            }
            SetDefault("quips", "version", 2);
            if (quipVersion < 2)
            {
                foreach (KeyValuePair<string, string[]> pair in QuipLibrary())
                {
                    if (pair.Value.Length > GetStrings("quips", pair.Key).Count)
                    {
                        SetAt("quips." + pair.Key, pair.Value);
                    }
                }
                SetAt("quips.version", 2.0);
            }
        }

        /// <summary>内置台词库（动作键 → 台词池）：每个动作都给足几句，随机起来才不重复。</summary>
        private static Dictionary<string, string[]> QuipLibrary()
        {
            Dictionary<string, string[]> map = new Dictionary<string, string[]>();
            map["greeting"] = new string[]
            {
                "今天也一起省点 token 吧～",
                "省着点花，余额我盯着呢",
                "余额我盯着的，你放心",
                "别熬夜了，熬夜真的很贵",
                "摸鱼可以，烧钱不行",
                "这波消费，值不值？",
                "点我干嘛？想花钱了？",
                "先说好，今天额度还剩多少？",
                "你问我就对啦，我最会数钱",
                "又来看余额，说明心里还有数",
                "余额健康，人就不慌",
                "账单不吓人，人才有底气",
                "省一点是一点，蚊子腿也是肉",
                "记得开谷时优惠再冲，半价呢",
                "我在，随时给你报数",
                "今天的钱，花在刀刃上了吗？"
            };
            map["sleep"] = new string[]
            {
                "呼……呼……",
                "（翻了个身）",
                "zzz……半夜便宜，我先睡了",
                "这点儿还干活？我睡了",
                "梦到余额涨了，别叫醒我",
                "睡着了就不用花钱，晚安",
                "呼噜声都是省下来的钱",
                "（裹紧被子）明天再省"
            };
            map["hop"] = new string[] { "嘿咻！", "跳一下～", "蹦！", "轻点蹦，地板贵", "一跳一跳，精神十足", "让开点，我要起了！" };
            map["bounce"] = new string[] { "蹦蹦跳跳～", "看我连跳！", "弹起来啦", "蹦得越高越省电", "一、二、三，再来！" };
            map["spin"] = new string[] { "转圈圈！", "晕了晕了", "原地转三圈", "看我转得圆不圆", "转出个金星来" };
            map["roll"] = new string[] { "打滚！", "滚一圈～", "滚过来又滚过去", "地面凉快", "滚成一团啦" };
            map["wobble"] = new string[] { "左右摇摆～", "看我摇摆", "摇啊摇", "晃晃悠悠", "甩甩胳膊甩甩腿" };
            map["nod"] = new string[] { "嗯嗯！", "点头点头", "说得对", "这个可以有", "同意同意" };
            map["shake"] = new string[] { "不行不行", "摇头", "别了吧", "这么花可不行", "下次一定" };
            map["wave"] = new string[] { "挥手～", "嗨！这里这里", "看到我了吗", "过来打个招呼呀", "挥挥手，不带走一片云彩", "手在这里！" };
            map["clap"] = new string[] { "鼓掌！", "啪啪啪", "干得漂亮", "给你拍拍手", "这波操作满分" };
            map["yawn"] = new string[] { "哈啊——", "困了…", "再撑一会儿就睡", "眼皮打架了", "打工人也是要睡觉的", "哈欠传染给你" };
            map["shy"] = new string[] { "别看我啦…", "有点害羞", "看得我不好意思了", "脸有点烫", "（捂脸）", "别老盯着我嘛" };
            map["startle"] = new string[] { "吓我一跳！", "哇！", "谁！", "心脏要跳出来了", "差点摔一跤", "别突然出声啊" };
            map["look"] = new string[] { "左看看…", "右看看…", "这边没情况", "那边有点亮", "四处张望", "看看有没有人偷花钱" };
            map["lie"] = new string[] { "趴一会儿…", "歇歇脚", "摊平了", "累瘫.jpg", "地上真舒服", "趴着也能盯余额" };
            map["stroll"] = new string[] { "溜达一圈", "随便走走～", "散步去也", "活动活动筋骨", "沿着桌边走两步", "走到哪算哪" };
            map["peek"] = new string[] { "探头看看…", "有人吗？", "悄悄看一眼", "溜边儿走", "别发现我", "探个头就走" };
            map["pace"] = new string[] { "踱两步", "走来走去", "思考人生", "来回转圈想想事", "走到这头又走到那头" };
            map["stretch"] = new string[] { "伸个懒腰～", "啊——舒服", "拉拉伸", "胳膊伸直了", "活动一下才不僵" };
            map["dash"] = new string[] { "冲刺！", "让一让！", "赶时间！", "风一样的速度", "咻——", "别拦我" };
            map["follow"] = new string[] { "跟着你～", "等等我！", "你去哪我去哪", "脚步跟上", "别走太快呀", "我贴着你走" };
            map["bump"] = new string[] { "哎哟！", "谁撞我！", "让一让～", "撞到啦…", "看着点路呀", "骨头都要散了", "你赔我发型", "别挤别挤" };
            map["greet"] = new string[] { "你好呀！", "我在这儿～", "报到！", "看见你啦", "来啦来啦", "今天也关照一下我" };
            return map;
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
