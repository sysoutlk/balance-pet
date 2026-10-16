// Coding-plan usage ("套餐用量"): the 5-hour / weekly windows that subscription plans
// (Kimi Code, Codex, Zhipu GLM, Z.AI, MiniMax, OpenCode Go, ...) expose, instead of a
// prepaid balance. Endpoints verified against the providers' own clients; the custom
// entry accepts any JSON that looks like a quota window.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;

namespace DshPet
{
    internal sealed class PlanWindow
    {
        public string Label = "";
        public double Percent;
        public string Detail = "";
    }

    internal sealed class PlanState
    {
        public string Id = "";
        public string Name = "";
        public bool Enabled;
        public bool Ok;
        public bool Unsupported;   // provider answers, but exposes no usage window for this key
        public string Note = "";
        public string Error = "";
        public string ErrorDetail = "";
        public string KeySource = "";
        public long AtMs;
        public List<PlanWindow> Windows = new List<PlanWindow>();

        public string Summary()
        {
            if (!Ok) return Error;
            StringBuilder sb = new StringBuilder();
            foreach (PlanWindow window in Windows)
            {
                if (sb.Length > 0) sb.Append(" · ");
                sb.Append(window.Label + " " + window.Percent.ToString("0.#", CultureInfo.InvariantCulture) + "%");
            }
            return sb.Length == 0 ? "无窗口数据" : sb.ToString();
        }
    }

    internal sealed class PlanDef
    {
        public string Id = "";
        public string Name = "";
        public string Url = "";
        public string Auth = "bearer";     // bearer | raw | kimiCli | codexToken
        public string Parser = "generic";  // kimi | codex | zhipu | generic
        public string Note = "";
        public bool IsCustom;
    }

    internal static class Plans
    {
        public const string KimiCliVersion = "1.41.0";

        public static List<PlanDef> Catalog()
        {
            List<PlanDef> list = new List<PlanDef>();
            list.Add(Def("kimiCode", "Kimi Code（套餐）", "https://api.kimi.com/coding/v1/usages", "kimiCli", "kimi",
                "kimi.com 编程套餐的 5 小时 / 7 天用量，用套餐 Key（sk-kim…）。"));
            list.Add(Def("codex", "Codex（ChatGPT 套餐）", "https://chatgpt.com/backend-api/wham/usage", "codexToken", "codex",
                "自动读取本机 ~/.codex/auth.json 的登录信息；也可手动粘贴 access token。"));
            list.Add(Def("zhipu", "智谱 GLM 编程套餐", "https://open.bigmodel.cn/api/monitor/usage/quota/limit", "raw", "zhipu",
                "open.bigmodel.cn 的 5 小时 / 每周额度（Key 不加 Bearer 前缀）。"));
            list.Add(Def("zai", "Z.AI（GLM 国际站）", "https://api.z.ai/api/monitor/usage/quota/limit", "raw", "zhipu",
                "api.z.ai 的 5 小时 / 每周额度（Key 不加 Bearer 前缀）。"));
            list.Add(Def("minimax", "MiniMax 编程套餐", "https://api.minimaxi.com/v1/api/openplatform/coding_plan/remains", "bearer", "generic",
                "MiniMax Coding Plan 额度（api.minimaxi.com）。"));
            list.Add(Def("opencode", "OpenCode Go", "https://opencode.ai/zen/go/v1/usage", "bearer", "generic",
                "$10/月订阅，美元额度三个时间窗口。"));
            list.Add(Def("aliyun", "阿里云 Token Plan / Coding Plan",
                "https://token-plan.cn-beijing.maas.aliyuncs.com/compatible-mode/v1", "bearer", "aliyun",
                "先列出套餐可用模型，再查该模型的用量窗口；国际站可改 plans.aliyun.baseUrl。"));
            PlanDef custom = Def("custom", "自定义用量接口", "", "bearer", "generic",
                "任意返回额度 JSON 的接口；会从响应里自动识别 limit/used/percentage/重置时间。");
            custom.IsCustom = true;
            list.Add(custom);
            return list;
        }

        private static PlanDef Def(string id, string name, string url, string auth, string parser, string note)
        {
            PlanDef def = new PlanDef();
            def.Id = id;
            def.Name = name;
            def.Url = url;
            def.Auth = auth;
            def.Parser = parser;
            def.Note = note;
            return def;
        }

        public static PlanState Query(Config cfg, PlanDef def)
        {
            PlanState state = new PlanState();
            state.Id = def.Id;
            state.Name = def.Name;
            state.Enabled = true;
            state.Note = def.Note;
            state.AtMs = DataService.NowMs();
            DateTime started = DateTime.UtcNow;
            try
            {
                string url = def.Url;
                if (def.IsCustom)
                {
                    url = cfg.GetStringAt("plans.custom.url", "");
                    if (string.IsNullOrEmpty(url))
                    {
                        state.Error = "未配置接口 URL";
                        return state;
                    }
                }
                string keySource;
                string key = ResolveCredential(cfg, def, out keySource);
                state.KeySource = keySource;
                if (string.IsNullOrEmpty(key) && def.Auth != "codexToken")
                {
                    state.Error = "未配置 API Key";
                    return state;
                }
                string body = Get(cfg, def, url, key);
                switch (def.Parser)
                {
                    case "kimi": ParseKimi(body, state); break;
                    case "codex": ParseCodex(body, state); break;
                    case "zhipu": ParseZhipu(body, state); break;
                    case "aliyun": QueryAliyun(cfg, def, key, state); break;
                    default: ParseGeneric(body, state, def.IsCustom ? cfg.GetStringAt("plans.custom.path", "") : ""); break;
                }
                state.Ok = state.Windows.Count > 0;
                if (!state.Ok && string.IsNullOrEmpty(state.Error)) state.Error = "响应里没有识别到用量窗口";
                if (!state.Ok) Log.Write("plan " + def.Id + " parse failed: " + Snippet(body));
                Log.Write("plan " + def.Id + " ok=" + state.Ok + " ms=" + (long)(DateTime.UtcNow - started).TotalMilliseconds);
            }
            catch (Exception ex)
            {
                ProviderException providerError = ex as ProviderException;
                if (providerError != null) state.ErrorDetail = Snippet(providerError.Body);
                state.Error = InnermostMessage(ex);
                if (string.IsNullOrEmpty(state.ErrorDetail)) state.ErrorDetail = state.Error;
                Log.Write("plan " + def.Id + " failed: " + state.Error);
            }
            return state;
        }

        /// <summary>HttpClient wraps the real failure in two layers; dig to the root cause.</summary>
        private static string InnermostMessage(Exception ex)
        {
            Exception current = ex;
            string message = ex.Message;
            int guard = 0;
            while (current.InnerException != null && guard < 6)
            {
                current = current.InnerException;
                guard++;
                if (!string.IsNullOrEmpty(current.Message))
                {
                    message = current.Message;
                }
            }
            return message;
        }

        // ------------------------------------------------------------ 凭证
        private static string ResolveCredential(Config cfg, PlanDef def, out string source)
        {
            source = "";
            string stored = cfg.GetStringAt("plans." + def.Id + ".apiKey", "");
            if (!string.IsNullOrEmpty(stored))
            {
                source = "pet-config.json";
                return Secret.Unprotect(stored).Trim();
            }
            if (def.Auth == "codexToken")
            {
                string token = CodexToken(out source);
                if (!string.IsNullOrEmpty(token)) return token;
            }
            return "";
        }

        /// <summary>Reuses the Codex CLI's own login (~/.codex/auth.json) when available.</summary>
        private static string CodexToken(out string source)
        {
            source = "";
            try
            {
                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex", "auth.json");
                if (!File.Exists(path)) return "";
                Dictionary<string, object> auth = Json.ReadObject(path);
                Dictionary<string, object> tokens = Json.AsObject(Json.Get(auth, "tokens"));
                string token = Convert.ToString(Json.Get(tokens, "access_token"), CultureInfo.InvariantCulture);
                if (string.IsNullOrEmpty(token)) token = Convert.ToString(Json.Get(auth, "access_token"), CultureInfo.InvariantCulture);
                string account = Convert.ToString(Json.Get(tokens, "account_id"), CultureInfo.InvariantCulture);
                if (string.IsNullOrEmpty(account)) account = Convert.ToString(Json.Get(auth, "account_id"), CultureInfo.InvariantCulture);
                if (string.IsNullOrEmpty(token)) return "";
                // Tuck the account id into the token string so Get() can send the header.
                source = "本机 Codex 登录";
                return token + "\u0001" + account;
            }
            catch
            {
                return "";
            }
        }

        // ------------------------------------------------------------ HTTP
        private static string Get(Config cfg, PlanDef def, string url, string key)
        {
            HttpClient client = Http.For(cfg, 45);
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("Accept", "application/json");
            if (def.Auth == "kimiCli")
            {
                request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + key);
                request.Headers.TryAddWithoutValidation("User-Agent", "KimiCLI/" + KimiCliVersion);
                request.Headers.TryAddWithoutValidation("X-Msh-Platform", "kimi_cli");
                request.Headers.TryAddWithoutValidation("X-Msh-Version", KimiCliVersion);
                request.Headers.TryAddWithoutValidation("X-Msh-Device-Name", Safe(Environment.MachineName));
                request.Headers.TryAddWithoutValidation("X-Msh-Device-Model", DeviceModel());
                request.Headers.TryAddWithoutValidation("X-Msh-Device-Id", DeviceId());
                request.Headers.TryAddWithoutValidation("X-Msh-Os-Version", Safe(Environment.OSVersion.VersionString));
            }
            else if (def.Auth == "raw")
            {
                request.Headers.TryAddWithoutValidation("Authorization", key);
            }
            else if (def.Auth == "codexToken")
            {
                string token = key;
                string account = "";
                int split = key == null ? -1 : key.IndexOf('\u0001');
                if (split >= 0)
                {
                    token = key.Substring(0, split);
                    account = key.Substring(split + 1);
                }
                request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);
                if (!string.IsNullOrEmpty(account))
                {
                    request.Headers.TryAddWithoutValidation("chatgpt-account-id", account);
                }
                request.Headers.TryAddWithoutValidation("User-Agent", "codex_cli_rs/0.50.0");
                request.Headers.TryAddWithoutValidation("originator", "codex_cli_rs");
            }
            else
            {
                request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + key);
                request.Headers.TryAddWithoutValidation("User-Agent", "dsh-pet/1.1");
            }
            HttpResponseMessage response = client.SendAsync(request).Result;
            string body = response.Content.ReadAsStringAsync().Result;
            if (!response.IsSuccessStatusCode)
            {
                int code = (int)response.StatusCode;
                string message;
                if (code == 401 || code == 403) message = "密钥无效或权限不足";
                else if (code == 404) message = "接口不存在";
                else if (code == 429) message = "请求过于频繁，稍后再试";
                else if (code == 451) message = "网络出口被服务方限制（HTTP 451），请更换线路/节点或设置 network.proxy";
                else message = "请求失败";
                throw new ProviderException("HTTP " + code + " · " + message, body);
            }
            return body;
        }

        private static string Safe(string value)
        {
            if (string.IsNullOrEmpty(value)) return "unknown";
            StringBuilder sb = new StringBuilder();
            foreach (char c in value)
            {
                if (c >= 0x20 && c <= 0x7e) sb.Append(c);
            }
            return sb.Length == 0 ? "unknown" : sb.ToString();
        }

        private static string DeviceModel()
        {
            string label = Environment.OSVersion.Version.Build >= 22000 ? "11" : "10";
            string machine = Environment.Is64BitOperatingSystem ? "x86_64" : "x86";
            return "Windows " + label + " " + machine;
        }

        /// <summary>Shares the Kimi CLI's device fingerprint when it exists, else our own.</summary>
        private static string DeviceId()
        {
            try
            {
                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kimi", "device_id");
                if (File.Exists(path))
                {
                    string value = File.ReadAllText(path).Trim();
                    if (value.Length > 0) return value;
                }
            }
            catch
            {
            }
            return "00000000000000000000000000000001";
        }

        // ------------------------------------------------------------ 解析
        private static void ParseKimi(string body, PlanState state)
        {
            Dictionary<string, object> data = Json.ReadText(body);
            Dictionary<string, object> usage = Json.AsObject(Json.Get(data, "usage"));
            if (usage.Count > 0) AddWindow(state, "7 天", usage, "limit", "used", "remaining", "resetTime");
            foreach (object item in Json.AsArray(Json.Get(data, "limits")))
            {
                Dictionary<string, object> entry = Json.AsObject(item);
                Dictionary<string, object> detail = Json.AsObject(Json.Get(entry, "detail"));
                Dictionary<string, object> window = Json.AsObject(Json.Get(entry, "window"));
                double duration = Json.ToNumber(Json.Get(window, "duration"), 0);
                string unit = Convert.ToString(Json.Get(window, "timeUnit"), CultureInfo.InvariantCulture);
                if (detail.Count > 0) AddWindow(state, DurationLabel(duration, unit), detail, "limit", "used", "remaining", "resetTime");
            }
        }

        private static void ParseCodex(string body, PlanState state)
        {
            Dictionary<string, object> data = Json.ReadText(body);
            string plan = Convert.ToString(Json.Get(data, "plan_type"), CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(plan)) state.Note = "ChatGPT 套餐：" + plan;
            Dictionary<string, object> rate = Json.AsObject(Json.Get(data, "rate_limit"));
            AddCodexWindow(state, Json.AsObject(Json.Get(rate, "primary_window")));
            AddCodexWindow(state, Json.AsObject(Json.Get(rate, "secondary_window")));
        }

        private static void AddCodexWindow(PlanState state, Dictionary<string, object> window)
        {
            if (window.Count == 0) return;
            double percent = Json.ToNumber(Json.Get(window, "used_percent"), 0);
            double seconds = Json.ToNumber(Json.Get(window, "limit_window_seconds"), 0);
            double resetAfter = Json.ToNumber(Json.Get(window, "reset_after_seconds"), 0);
            PlanWindow item = new PlanWindow();
            item.Label = seconds > 0 ? SecondsLabel(seconds) : "额度";
            item.Percent = percent;
            item.Detail = percent.ToString("0.#", CultureInfo.InvariantCulture) + "%"
                + (resetAfter > 0 ? "  ·  " + DurationText(resetAfter) + "后重置" : "");
            state.Windows.Add(item);
        }

        private static void ParseZhipu(string body, PlanState state)
        {
            Dictionary<string, object> data = Json.ReadText(body);
            Dictionary<string, object> node = Json.AsObject(Json.Get(data, "data"));
            foreach (object item in Json.AsArray(Json.Get(node, "limits")))
            {
                Dictionary<string, object> limit = Json.AsObject(item);
                string type = Convert.ToString(Json.Get(limit, "type"), CultureInfo.InvariantCulture);
                if (!string.Equals(type, "TOKENS_LIMIT", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(type, "CREDIT_LIMIT", StringComparison.OrdinalIgnoreCase)) continue;
                double unit = Json.ToNumber(Json.Get(limit, "unit"), 0);
                PlanWindow window = new PlanWindow();
                window.Label = unit == 3 ? "5 小时" : (unit == 6 ? "每周" : "额度");
                window.Percent = Json.ToNumber(Json.Get(limit, "percentage"), 0);
                double resetMs = Json.ToNumber(Json.Get(limit, "nextResetTime"), 0);
                window.Detail = window.Percent.ToString("0.#", CultureInfo.InvariantCulture) + "%"
                    + (resetMs > 0 ? "  ·  " + ResetText(resetMs / 1000.0) : "");
                state.Windows.Add(window);
            }
        }

        /// <summary>
        /// Alibaba ModelStudio token/coding plans: the usage endpoint is per model
        /// (`/models/{id}/usage`), so list the plan's models first and probe the first few
        /// until one answers with a quota payload.
        /// </summary>
        private static void QueryAliyun(Config cfg, PlanDef def, string key, PlanState state)
        {
            string baseUrl = cfg.GetStringAt("plans.aliyun.baseUrl", def.Url).TrimEnd('/');
            if (string.IsNullOrEmpty(baseUrl)) baseUrl = def.Url.TrimEnd('/');
            List<string> models = new List<string>();
            string configured = cfg.GetStringAt("plans.aliyun.model", "");
            if (!string.IsNullOrEmpty(configured)) models.Add(configured.Trim());
            try
            {
                string listBody = Get(cfg, def, baseUrl + "/models", key);
                foreach (string id in ModelIds(listBody))
                {
                    if (!models.Contains(id)) models.Add(id);
                }
            }
            catch (Exception ex)
            {
                state.Error = InnermostMessage(ex);
            }
            string lastError = state.Error;
            int probes = 0;
            foreach (string model in models)
            {
                if (string.IsNullOrEmpty(model)) continue;
                if (probes >= 8) break;
                probes++;
                try
                {
                    string body = Get(cfg, def, baseUrl + "/models/" + Uri.EscapeDataString(model) + "/usage", key);
                    ParseGeneric(body, state, "");
                    if (state.Ok)
                    {
                        state.Note = "模型：" + model;
                        return;
                    }
                    state.Windows.Clear();
                    lastError = "模型 " + model + " 没有返回用量窗口";
                }
                catch (Exception ex)
                {
                    lastError = model + "：" + InnermostMessage(ex);
                }
            }
            state.Error = string.IsNullOrEmpty(lastError) ? "没有查到可用模型的用量" : lastError;
            if (models.Count > 0)
            {
                // The plan answers and lists its models, it just does not expose a usage window
                // for this key. Say so plainly instead of showing a raw HTTP error.
                state.Unsupported = true;
                state.Error = "阿里未对该 Key 开放用量查询（可选模型 " + models.Count + " 个，用量请在百炼控制台查看）";
            }
        }

        private static List<string> ModelIds(string body)
        {
            List<string> ids = new List<string>();
            try
            {
                Dictionary<string, object> data = Json.ReadText(body);
                foreach (object item in Json.AsArray(Json.Get(data, "data")))
                {
                    string id = Convert.ToString(Json.Get(Json.AsObject(item), "id"), CultureInfo.InvariantCulture);
                    if (!string.IsNullOrEmpty(id) && ids.Count < 16) ids.Add(id);
                }
            }
            catch
            {
            }
            return ids;
        }

        /// <summary>
        /// Best-effort walker for JSON shapes we have not pinned down (MiniMax, custom
        /// endpoints): collects objects that carry a percentage or a limit/used pair.
        /// </summary>
        private static void ParseGeneric(string body, PlanState state, string path)
        {
            object root = Json.ReadAny(body);
            if (!string.IsNullOrEmpty(path)) root = Json.Pick(root, path);
            Walk(root, state, 0, "");
            if (state.Windows.Count > 5)
            {
                state.Windows.RemoveRange(5, state.Windows.Count - 5);
            }
        }

        private static void Walk(object node, PlanState state, int depth, string labelHint)
        {
            if (node == null || depth > 4 || state.Windows.Count >= 5) return;
            Dictionary<string, object> map = node as Dictionary<string, object>;
            if (map != null)
            {
                string hint = FirstString(map, labelHint, "name", "title", "label", "scope", "period", "timeUnit", "unit");
                PlanWindow window = WindowFrom(map, hint);
                if (window != null) state.Windows.Add(window);
                foreach (KeyValuePair<string, object> pair in map)
                {
                    if (pair.Value == null) continue;
                    Walk(pair.Value, state, depth + 1, hint);
                }
                return;
            }
            foreach (object item in Json.AsArray(node))
            {
                Walk(item, state, depth + 1, labelHint);
            }
        }

        private static PlanWindow WindowFrom(Dictionary<string, object> map, string hint)
        {
            double? percent = NumberAmong(map, "used_percent", "usedPercent", "percentage", "usagePercent", "used_rate", "usedRate", "percent");
            double? limit = NumberAmong(map, "limit", "total", "quota", "max", "limit_amount", "total_amount");
            double? used = NumberAmong(map, "used", "usedAmount", "used_amount", "current", "consumed");
            double? remaining = NumberAmong(map, "remaining", "remain", "left", "available");
            if (percent == null)
            {
                if (limit.HasValue && limit.Value > 0)
                {
                    if (used.HasValue) percent = used.Value / limit.Value * 100;
                    else if (remaining.HasValue) percent = (limit.Value - remaining.Value) / limit.Value * 100;
                }
            }
            if (percent == null) return null;
            PlanWindow window = new PlanWindow();
            double seconds = FirstNumber(map, "limit_window_seconds", "window_seconds", "period_seconds", "windowSeconds") ?? 0;
            window.Label = seconds > 0 ? SecondsLabel(seconds) : (string.IsNullOrEmpty(hint) ? "用量" : hint);
            window.Percent = percent.Value;
            string reset = ResetTextFromMap(map);
            window.Detail = percent.Value.ToString("0.#", CultureInfo.InvariantCulture) + "%" + reset;
            return window;
        }

        private static void AddWindow(PlanState state, string label, Dictionary<string, object> map,
            string limitKey, string usedKey, string remainingKey, string resetKey)
        {
            double? limit = Json.ToNumberOrNull(Json.Get(map, limitKey));
            double? used = Json.ToNumberOrNull(Json.Get(map, usedKey));
            double? remaining = Json.ToNumberOrNull(Json.Get(map, remainingKey));
            double percent;
            if (limit.HasValue && limit.Value > 0 && used.HasValue)
            {
                percent = used.Value / limit.Value * 100;
            }
            else if (limit.HasValue && limit.Value > 0 && remaining.HasValue)
            {
                percent = (limit.Value - remaining.Value) / limit.Value * 100;
            }
            else
            {
                return;
            }
            PlanWindow window = new PlanWindow();
            window.Label = label;
            window.Percent = percent;
            string reset = Convert.ToString(Json.Get(map, resetKey), CultureInfo.InvariantCulture);
            string usedText = (used.HasValue ? used.Value : limit.Value - (remaining.HasValue ? remaining.Value : 0)).ToString("0.##", CultureInfo.InvariantCulture);
            string limitText = limit.Value.ToString("0.##", CultureInfo.InvariantCulture);
            window.Detail = usedText + " / " + limitText + "（" + percent.ToString("0.#", CultureInfo.InvariantCulture) + "%）"
                + (string.IsNullOrEmpty(reset) ? "" : "  ·  " + ResetTextIso(reset));
            state.Windows.Add(window);
        }

        private static string ResetTextIso(string iso)
        {
            try
            {
                DateTime when = DateTime.Parse(iso, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
                double seconds = (when - DateTime.UtcNow).TotalSeconds;
                if (seconds <= 0) return "已重置";
                return DurationText(seconds) + "后重置";
            }
            catch
            {
                return "重置 " + iso;
            }
        }

        private static string ResetText(double epochSeconds)
        {
            double seconds = epochSeconds - (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
            if (seconds <= 0) return "已重置";
            return DurationText(seconds) + "后重置";
        }

        private static string ResetTextFromMap(Dictionary<string, object> map)
        {
            foreach (string key in new string[] { "reset_at", "resetAt", "resetTime", "nextResetTime", "resetsAt", "reset_time" })
            {
                object value = Json.Get(map, key);
                if (value == null) continue;
                string text = Convert.ToString(value, CultureInfo.InvariantCulture);
                if (string.IsNullOrEmpty(text)) continue;
                double epoch;
                if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out epoch))
                {
                    if (epoch > 1e12) epoch = epoch / 1000.0;   // milliseconds
                    return "  ·  " + ResetText(epoch);
                }
                return "  ·  " + ResetTextIso(text);
            }
            double? resetAfter = NumberAmong(map, "reset_after_seconds", "resetAfterSeconds", "reset_in");
            if (resetAfter.HasValue) return "  ·  " + DurationText(resetAfter.Value) + "后重置";
            return "";
        }

        private static string DurationLabel(double duration, string unit)
        {
            if (string.IsNullOrEmpty(unit)) return "窗口";
            if (unit.IndexOf("MINUTE", StringComparison.OrdinalIgnoreCase) >= 0) return SecondsLabel(duration * 60);
            if (unit.IndexOf("HOUR", StringComparison.OrdinalIgnoreCase) >= 0) return SecondsLabel(duration * 3600);
            if (unit.IndexOf("DAY", StringComparison.OrdinalIgnoreCase) >= 0) return SecondsLabel(duration * 86400);
            return "窗口";
        }

        private static string SecondsLabel(double seconds)
        {
            if (seconds >= 86400) return Trim(seconds / 86400) + " 天";
            if (seconds >= 3600) return Trim(seconds / 3600) + " 小时";
            return Trim(seconds / 60) + " 分钟";
        }

        private static string Trim(double value)
        {
            double rounded = Math.Round(value, 1);
            return rounded == Math.Floor(rounded)
                ? ((long)rounded).ToString(CultureInfo.InvariantCulture)
                : rounded.ToString("0.#", CultureInfo.InvariantCulture);
        }

        private static string DurationText(double seconds)
        {
            if (seconds < 0) seconds = 0;
            long total = (long)seconds;
            if (total >= 86400) return (total / 86400) + " 天 " + ((total % 86400) / 3600) + " 小时";
            if (total >= 3600) return (total / 3600) + " 小时 " + ((total % 3600) / 60) + " 分";
            return Math.Max(1, total / 60) + " 分钟";
        }

        private static double? NumberAmong(Dictionary<string, object> map, params string[] keys)
        {
            foreach (string key in keys)
            {
                double? value = Json.ToNumberOrNull(Json.Get(map, key));
                if (value.HasValue) return value;
            }
            return null;
        }

        private static double? FirstNumber(Dictionary<string, object> map, params string[] keys)
        {
            return NumberAmong(map, keys);
        }

        private static string FirstString(Dictionary<string, object> map, string fallback, params string[] keys)
        {
            foreach (string key in keys)
            {
                string value = Convert.ToString(Json.Get(map, key), CultureInfo.InvariantCulture);
                if (!string.IsNullOrEmpty(value)) return value;
            }
            return fallback;
        }

        private static string Snippet(string text)
        {
            if (string.IsNullOrEmpty(text)) return "<empty>";
            text = text.Replace("\r", " ").Replace("\n", " ");
            return text.Length <= 400 ? text : text.Substring(0, 400) + "…";
        }
    }
}
