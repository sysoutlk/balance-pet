// Provider registry and balance queries.
//
// Providers with a documented API-key balance endpoint are queried directly:
//   DeepSeek   GET https://api.deepseek.com/user/balance
//   Kimi       GET https://api.moonshot.cn/v1/users/me/balance
//   SiliconFlow GET https://api.siliconflow.cn/v1/user/info
//   OpenRouter GET https://openrouter.ai/api/v1/credits   (covers GPT/Claude/Gemini/Grok)
// Everything else (Qwen/DashScope, Doubao/Ark, OpenAI, Anthropic, Gemini, xAI) has no
// public key-based balance API; those entries are listed with a short note instead of
// pretending to work, and the "custom" provider lets any relay or self-hosted gateway
// be wired in through pet-config.json without touching the code.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BalancePet
{
    internal static class Fmt
    {
        public static string Symbol(string currency)
        {
            if (string.Equals(currency, "USD", StringComparison.OrdinalIgnoreCase)) return "$";
            if (string.Equals(currency, "EUR", StringComparison.OrdinalIgnoreCase)) return "EUR";
            return "¥";
        }

        public static string Money(double? value, string currency)
        {
            if (!value.HasValue) return "--";
            return Symbol(currency) + " " + value.Value.ToString("N2", CultureInfo.InvariantCulture);
        }
    }

    internal sealed class ProviderDef
    {
        public string Id = "";
        public string Name = "";
        public string Currency = "CNY";
        public string Auth = "bearer";      // bearer | x-api-key | none
        public bool HasBalance = true;      // false = no documented public balance endpoint
        public bool PeakPricing;            // 该平台有高峰/优惠时段计价（错峰优惠，目前只有 DeepSeek）
        public bool IsCustom;
        public string Note = "";
    }

    /// <summary>Carries the raw response body so error messages can quote the server.</summary>
    internal sealed class ProviderException : Exception
    {
        public string Body;

        public ProviderException(string message, string body)
            : base(message)
        {
            Body = body;
        }
    }

    internal sealed class ProviderState
    {
        public string Id = "";
        public string Name = "";
        public string Currency = "CNY";
        public bool Enabled;
        public bool Ok;
        public bool Supported = true;
        public bool IsCustom;
        public double? Balance;
        public double? TodayUsage;
        public double? TodayKeyUsage;       // 平台实时：当前 API Key 今日费用
        public double? TodayTotalUsage;     // 平台实时：全部 API Key 今日费用
        public double? LifetimeUsage;       // 平台账户累计费用
        public bool UsageExact;
        public int UsageKeyCount;
        public string UsageKeyName = "";
        public string UsageError = "";
        public long LocalUsageSinceMs;      // local balance tracking coverage start
        public List<DayUsage> PlatformHistory = new List<DayUsage>();
        public List<string> Details = new List<string>();
        public string KeySource = "";       // where the API key came from, for the settings UI
        public string Error = "";
        public string ErrorDetail = "";     // raw error kept for tooltips when Error is short
        public long AtMs;

        public string SourceText
        {
            get
            {
                if (IsCustom) return "自定义";
                return "直连";
            }
        }
    }

    internal static class Providers
    {
        public static string Version = "1.2.0";
        private static readonly object PlatformUsageCacheLock = new object();
        private static string platformUsageCacheToken = "";
        private static string platformUsageCacheDay = "";
        private static long platformUsageCacheAtMs;
        private static PlatformUsageSnapshot platformUsageCache;

        /// <summary>这个平台的计价是否分标准/优惠时段（错峰优惠）：只有 DeepSeek 是。</summary>
        public static bool HasPeakPricing(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            foreach (ProviderDef def in Builtin())
            {
                if (def.Id == id) return def.PeakPricing;
            }
            return false;
        }

        public static List<ProviderDef> Builtin()
        {
            List<ProviderDef> list = new List<ProviderDef>();

            ProviderDef deepseek = new ProviderDef();
            deepseek.Id = "deepseek";
            deepseek.Name = "DeepSeek";
            deepseek.Currency = "CNY";
            deepseek.Note = "官方 /用户余额接口，含赠金与充值拆分。";
            deepseek.PeakPricing = true;   // 错峰优惠：只有它区分标准/优惠时段
            list.Add(deepseek);

            ProviderDef moonshot = new ProviderDef();
            moonshot.Id = "moonshot";
            moonshot.Name = "Kimi月之暗面";
            moonshot.Currency = "CNY";
            moonshot.Note = "官方余额接口（现金 + 代金券）。请用 Kimi 开放平台 platform.moonshot.cn 的 API Key；"
                + "国内站/国际站会自动互相重试，也可手动改 providers.moonshot.baseUrl。";
            list.Add(moonshot);

            ProviderDef siliconflow = new ProviderDef();
            siliconflow.Id = "siliconflow";
            siliconflow.Name = "硅基流动";
            siliconflow.Currency = "CNY";
            siliconflow.Note = "官方 user/info 接口（充值 + 赠送）。";
            list.Add(siliconflow);

            ProviderDef openrouter = new ProviderDef();
            openrouter.Id = "openrouter";
            openrouter.Name = "OpenRouter";
            openrouter.Currency = "USD";
            openrouter.Note = "美元额度，可覆盖 GPT / Claude / Gemini / Grok 等海外模型。";
            list.Add(openrouter);

            ProviderDef qwen = new ProviderDef();
            qwen.Id = "qwen";
            qwen.Name = "通义千问 DashScope";
            qwen.HasBalance = false;
            qwen.Note = "DashScope 未开放 API Key 余额接口，请在阿里云费用中心查看；自建中转可用「自定义接口」接入。";
            list.Add(qwen);

            ProviderDef doubao = new ProviderDef();
            doubao.Id = "doubao";
            doubao.Name = "豆包 火山方舟";
            doubao.HasBalance = false;
            doubao.Note = "火山方舟未开放 API Key 余额接口，请在火山引擎控制台查看。";
            list.Add(doubao);

            ProviderDef zhipu = new ProviderDef();
            zhipu.Id = "zhipu";
            zhipu.Name = "智谱 GLM";
            zhipu.HasBalance = false;
            zhipu.Note = "智谱开放平台未开放 API Key 余额接口，请在控制台查看。";
            list.Add(zhipu);

            ProviderDef openai = new ProviderDef();
            openai.Id = "openai";
            openai.Name = "OpenAI GPT";
            openai.Currency = "USD";
            openai.HasBalance = false;
            openai.Note = "OpenAI 官方无 Key 余额接口（仅 Admin Key 可查成本报表）；建议用 OpenRouter 或自定义接口。";
            list.Add(openai);

            ProviderDef anthropic = new ProviderDef();
            anthropic.Id = "anthropic";
            anthropic.Name = "Anthropic Claude";
            anthropic.Currency = "USD";
            anthropic.HasBalance = false;
            anthropic.Note = "Claude 官方无 Key 余额接口（Admin Key 可查成本报表）；建议用 OpenRouter 或自定义接口。";
            list.Add(anthropic);

            ProviderDef gemini = new ProviderDef();
            gemini.Id = "gemini";
            gemini.Name = "Google Gemini";
            gemini.Currency = "USD";
            gemini.HasBalance = false;
            gemini.Note = "Gemini 无 Key 余额接口，费用走 Google Cloud 结算，请在云控制台查看。";
            list.Add(gemini);

            ProviderDef grok = new ProviderDef();
            grok.Id = "xai";
            grok.Name = "xAI Grok";
            grok.Currency = "USD";
            grok.HasBalance = false;
            grok.Note = "xAI 暂未提供 API Key 余额接口，请在 xAI Console 查看。";
            list.Add(grok);

            return list;
        }

        /// <summary>Built-in providers plus the configurable custom endpoint.</summary>
        public static List<ProviderDef> Effective(Config cfg)
        {
            List<ProviderDef> list = Builtin();
            ProviderDef custom = new ProviderDef();
            custom.Id = "custom";
            custom.Name = cfg.GetString("custom", "name", "自定义接口");
            if (string.IsNullOrEmpty(custom.Name)) custom.Name = "自定义接口";
            custom.Currency = cfg.GetString("custom", "currency", "CNY");
            if (string.IsNullOrEmpty(custom.Currency)) custom.Currency = "CNY";
            custom.Auth = cfg.GetString("custom", "auth", "bearer");
            custom.IsCustom = true;
            custom.Note = "任意提供余额 JSON 的接口（中转 / 自建网关）。";
            list.Add(custom);
            return list;
        }

        public static ProviderDef Find(List<ProviderDef> defs, string id)
        {
            foreach (ProviderDef def in defs)
            {
                if (def.Id == id) return def;
            }
            return null;
        }

        /// <summary>Resolves one provider's API key from pet-config.json only. The key is
        /// DPAPI-encrypted when the settings card saved it; plain text keeps working.</summary>
        public static string ResolveKey(Config cfg, ProviderDef def, out string source)
        {
            source = "";
            string stored = cfg.GetString("providers." + def.Id, "apiKey", "");
            if (!string.IsNullOrEmpty(stored))
            {
                source = "pet-config.json";
                return Secret.Unprotect(stored).Trim();
            }
            if (def.Id == "deepseek")
            {
                string legacy = cfg.GetString("", "apiKey", "");
                if (!string.IsNullOrEmpty(legacy))
                {
                    source = "pet-config.json";
                    return Secret.Unprotect(legacy).Trim();
                }
            }
            return "";
        }

        /// <summary>Queries one provider and returns a filled state (never throws).</summary>
        public static ProviderState Query(Config cfg, ProviderDef def)
        {
            ProviderState state = new ProviderState();
            state.Id = def.Id;
            state.Name = def.Name;
            state.Currency = def.Currency;
            state.Enabled = true;
            state.Supported = def.HasBalance;
            state.IsCustom = def.IsCustom;
            state.AtMs = DataService.NowMs();
            try
            {
                if (!def.HasBalance)
                {
                    state.Error = def.Note;
                    return state;
                }
                string keySource;
                string key = ResolveKey(cfg, def, out keySource);
                key = NormalizeKey(key);
                state.KeySource = keySource;
                string url;
                string auth = def.Auth;
                if (def.IsCustom)
                {
                    url = cfg.GetString("custom", "url", "");
                    if (string.IsNullOrEmpty(url))
                    {
                        state.Error = "未配置接口 URL";
                        return state;
                    }
                    url = url.Replace("{key}", Uri.EscapeDataString(key == null ? "" : key));
                    if (auth != "none" && string.IsNullOrEmpty(key))
                    {
                        state.Error = "未配置 API Key";
                        return state;
                    }
                }
                else
                {
                    if (string.IsNullOrEmpty(key))
                    {
                        state.Error = "未配置 API Key";
                        return state;
                    }
                    if (def.Id == "moonshot")
                    {
                        // The key may belong to the international site; try both hosts before giving up.
                        string moonshotBody = null;
                        string first = null;
                        foreach (string baseUrl in MoonshotHosts(cfg))
                        {
                            try
                            {
                                moonshotBody = Get(cfg, baseUrl + "/v1/users/me/balance", def.Auth, key);
                                break;
                            }
                            catch (Exception ex)
                            {
                                if (first == null) first = ex.Message;
                            }
                        }
                        if (moonshotBody == null)
                        {
                            state.Error = first;
                            state.ErrorDetail = first;
                            return state;
                        }
                        Parse(def, cfg, moonshotBody, state);
                        state.Ok = state.Balance.HasValue;
                        if (!state.Ok && string.IsNullOrEmpty(state.Error)) state.Error = "响应里没有余额字段";
                        if (!state.Ok) Log.Write("provider " + def.Id + " parse failed: " + Snippet(moonshotBody));
                        return state;
                    }
                    url = EndpointFor(cfg, def);
                    if (def.Id == "deepseek"
                        && url.IndexOf("api.deepseek.com", StringComparison.OrdinalIgnoreCase) >= 0
                        && !LooksLikeDeepSeekKey(key))
                    {
                        state.Error = "DeepSeek API Key 格式不正确（应以 sk- 开头）";
                        state.ErrorDetail = state.Error;
                        return state;
                    }
                }
                string body = Get(cfg, url, auth, key);
                Parse(def, cfg, body, state);
                state.Ok = state.Balance.HasValue;
                if (!state.Ok && string.IsNullOrEmpty(state.Error)) state.Error = "响应里没有余额字段";
                if (!state.Ok) Log.Write("provider " + def.Id + " parse failed: " + Snippet(body));
                if (state.Ok && def.Id == "deepseek") ApplyDeepSeekPlatformUsage(cfg, state, key);
            }
            catch (Exception ex)
            {
                ProviderException providerError = ex as ProviderException;
                if (providerError != null) state.ErrorDetail = Snippet(providerError.Body);
                Exception inner = ex.InnerException;
                state.Error = inner != null && !string.IsNullOrEmpty(inner.Message) ? inner.Message : ex.Message;
                if (string.IsNullOrEmpty(state.ErrorDetail)) state.ErrorDetail = state.Error;
                Log.Write("provider " + def.Id + " failed: " + state.Error);
            }
            return state;
        }

        private static List<string> MoonshotHosts(Config cfg)
        {
            List<string> hosts = new List<string>();
            string configured = cfg.GetString("providers.moonshot", "baseUrl", "https://api.moonshot.cn");
            if (string.IsNullOrEmpty(configured)) configured = "https://api.moonshot.cn";
            configured = configured.TrimEnd('/');
            hosts.Add(configured);
            string alternate = configured.IndexOf("moonshot.ai", StringComparison.OrdinalIgnoreCase) >= 0
                ? "https://api.moonshot.cn"
                : "https://api.moonshot.ai";
            if (alternate != configured) hosts.Add(alternate);
            return hosts;
        }

        /// <summary>Pasted keys often carry quotes, spaces or a "Bearer " prefix.</summary>
        private static string NormalizeKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return key;
            key = key.Trim().Trim('"', '\'').Trim();
            if (key.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) key = key.Substring(7).Trim();
            return key;
        }

        private static string Snippet(string text)
        {
            if (string.IsNullOrEmpty(text)) return "<empty>";
            text = text.Replace("\r", " ").Replace("\n", " ");
            return text.Length <= 400 ? text : text.Substring(0, 400) + "…";
        }

        private static string ErrorBodyDetail(string body)
        {
            if (string.IsNullOrEmpty(body)) return "";
            string text = body;
            if (text.IndexOf('<') >= 0)
            {
                text = Regex.Replace(text, "<[^>]*>", " ");
                text = WebUtility.HtmlDecode(text);
                text = Regex.Replace(text, "\\s+", " ").Trim();
            }
            return Snippet(text);
        }

        private static bool LooksLikeDeepSeekKey(string key)
        {
            if (string.IsNullOrEmpty(key) || key.Length < 8) return false;
            if (!key.StartsWith("sk-", StringComparison.OrdinalIgnoreCase)) return false;
            foreach (char ch in key)
            {
                if (char.IsWhiteSpace(ch) || char.IsControl(ch)) return false;
            }
            return true;
        }

        private static string EndpointFor(Config cfg, ProviderDef def)
        {
            if (def.Id == "deepseek") return cfg.GetString("", "dataUrl", "https://api.deepseek.com/user/balance");
            if (def.Id == "moonshot")
            {
                string baseUrl = cfg.GetString("providers.moonshot", "baseUrl", "https://api.moonshot.cn");
                if (string.IsNullOrEmpty(baseUrl)) baseUrl = "https://api.moonshot.cn";
                return baseUrl.TrimEnd('/') + "/v1/users/me/balance";
            }
            if (def.Id == "siliconflow") return "https://api.siliconflow.cn/v1/user/info";
            if (def.Id == "openrouter") return "https://openrouter.ai/api/v1/credits";
            return "";
        }

        private static string Get(Config cfg, string url, string auth, string key)
        {
            HttpClient client = Http.For(cfg, 12);
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("User-Agent", "balance-pet/" + Version);
            if (auth == "bearer" && !string.IsNullOrEmpty(key))
            {
                request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + key);
            }
            else if (auth == "x-api-key" && !string.IsNullOrEmpty(key))
            {
                request.Headers.TryAddWithoutValidation("x-api-key", key);
            }
            HttpResponseMessage response = client.SendAsync(request).Result;
            string body = response.Content.ReadAsStringAsync().Result;
            if (!response.IsSuccessStatusCode)
            {
                int code = (int)response.StatusCode;
                string message;
                if (code == 400) message = "请求参数或 API Key 格式不正确";
                else if (code == 401 || code == 403) message = "密钥无效或权限不足";
                else if (code == 404) message = "接口不存在";
                else if (code == 429) message = "请求过于频繁，稍后再试";
                else if (code == 451) message = "网络出口被服务方限制（HTTP 451），请更换线路/节点或设置 network.proxy";
                else message = "请求失败";
                string detail = ErrorMessageOf(body);
                if (string.IsNullOrEmpty(detail) && !string.IsNullOrEmpty(body))
                    detail = ErrorBodyDetail(body);
                string text = "HTTP " + code + " · " + message;
                if (!string.IsNullOrEmpty(detail)) text += "（" + detail + "）";
                throw new ProviderException(text, body);
            }
            return body;
        }

        /// <summary>Pulls error.message out of the provider's JSON error envelope.</summary>
        private static string ErrorMessageOf(string body)
        {
            try
            {
                if (string.IsNullOrEmpty(body)) return "";
                Dictionary<string, object> data = Json.ReadText(body);
                Dictionary<string, object> error = Json.AsObject(Json.Get(data, "error"));
                string message = Convert.ToString(Json.Get(error, "message"), CultureInfo.InvariantCulture);
                if (string.IsNullOrEmpty(message)) message = Convert.ToString(Json.Get(data, "message"), CultureInfo.InvariantCulture);
                if (string.IsNullOrEmpty(message)) return "";
                return message.Length <= 80 ? message : message.Substring(0, 80) + "…";
            }
            catch
            {
                return "";
            }
        }

        private static void Parse(ProviderDef def, Config cfg, string body, ProviderState state)
        {
            if (def.IsCustom)
            {
                object root = Json.ReadAny(body);
                string path = cfg.GetString("custom", "path", "");
                object picked = Json.Pick(root, path);
                state.Balance = Json.ToNumberOrNull(picked);
                return;
            }
            Dictionary<string, object> data = Json.ReadText(body);
            if (def.Id == "deepseek")
            {
                List<object> infos = Json.AsArray(Json.Get(data, "balance_infos"));
                Dictionary<string, object> info = null;
                foreach (object item in infos)
                {
                    Dictionary<string, object> candidate = Json.AsObject(item);
                    string currency = Convert.ToString(Json.Get(candidate, "currency"), CultureInfo.InvariantCulture);
                    if (currency == "CNY")
                    {
                        info = candidate;
                        break;
                    }
                    if (info == null) info = candidate;
                }
                if (info == null)
                {
                    state.Error = "响应里没有 balance_infos";
                    return;
                }
                string infoCurrency = Convert.ToString(Json.Get(info, "currency"), CultureInfo.InvariantCulture);
                if (!string.IsNullOrEmpty(infoCurrency)) state.Currency = infoCurrency;
                state.Balance = Json.ToNumberOrNull(Json.Get(info, "total_balance"));
                AddDetail(state, "赠金", Json.ToNumberOrNull(Json.Get(info, "granted_balance")));
                AddDetail(state, "充值", Json.ToNumberOrNull(Json.Get(info, "topped_up_balance")));
                return;
            }
            if (def.Id == "moonshot")
            {
                Dictionary<string, object> node = Json.AsObject(Json.Get(data, "data"));
                state.Balance = Json.ToNumberOrNull(Json.Get(node, "available_balance"));
                AddDetail(state, "代金券", Json.ToNumberOrNull(Json.Get(node, "voucher_balance")));
                AddDetail(state, "现金", Json.ToNumberOrNull(Json.Get(node, "cash_balance")));
                return;
            }
            if (def.Id == "siliconflow")
            {
                Dictionary<string, object> node = Json.AsObject(Json.Get(data, "data"));
                state.Balance = Json.ToNumberOrNull(Json.Get(node, "totalBalance"));
                if (!state.Balance.HasValue) state.Balance = Json.ToNumberOrNull(Json.Get(node, "balance"));
                AddDetail(state, "充值", Json.ToNumberOrNull(Json.Get(node, "chargeBalance")));
                AddDetail(state, "赠送", Json.ToNumberOrNull(Json.Get(node, "balance")));
                return;
            }
            if (def.Id == "openrouter")
            {
                Dictionary<string, object> node = Json.AsObject(Json.Get(data, "data"));
                double? credits = Json.ToNumberOrNull(Json.Get(node, "total_credits"));
                double? usage = Json.ToNumberOrNull(Json.Get(node, "total_usage"));
                if (credits.HasValue)
                {
                    state.Balance = credits.Value - (usage.HasValue ? usage.Value : 0);
                }
                AddDetail(state, "额度总额", credits);
                AddDetail(state, "已用", usage);
                return;
            }
            state.Error = "未知的提供商";
        }

        private sealed class PlatformUsageSnapshot
        {
            public bool CostOk;
            public double? TodayKeyUsage;
            public double? TodayTotalUsage;
            public double? LifetimeUsage;
            public int KeyCount;
            public string KeyName = "";
            public string Error = "";
            public List<DayUsage> History = new List<DayUsage>();
        }

        private static void ApplyDeepSeekPlatformUsage(Config cfg, ProviderState state, string apiKey)
        {
            string stored = cfg.GetString("providers.deepseek", "platformToken", "");
            string token = NormalizePlatformToken(Secret.Unprotect(stored));
            if (string.IsNullOrEmpty(token)) return;

            string day = DataService.TodayKey();
            long now = DataService.NowMs();
            double refreshSec = cfg.GetNumberAt("providers.deepseek.usageRefreshSec", 180);
            if (refreshSec < 30) refreshSec = 30;
            if (refreshSec > 3600) refreshSec = 3600;
            lock (PlatformUsageCacheLock)
            {
                if (platformUsageCache != null
                    && platformUsageCacheDay == day
                    && platformUsageCacheToken == token
                    && now - platformUsageCacheAtMs < (long)(refreshSec * 1000))
                {
                    CopyPlatformUsage(platformUsageCache, state);
                    return;
                }
            }

            PlatformUsageSnapshot snapshot;
            try
            {
                snapshot = QueryDeepSeekPlatformUsage(cfg, token, apiKey, state.Currency);
            }
            catch (Exception ex)
            {
                state.UsageError = ex.Message;
                Log.Write("deepseek platform usage failed: " + ex.Message);
                return;
            }
            lock (PlatformUsageCacheLock)
            {
                platformUsageCacheToken = token;
                platformUsageCacheDay = day;
                platformUsageCacheAtMs = now;
                platformUsageCache = snapshot;
            }
            CopyPlatformUsage(snapshot, state);
        }

        private static void CopyPlatformUsage(PlatformUsageSnapshot source, ProviderState state)
        {
            state.UsageExact = source.CostOk;
            state.TodayKeyUsage = source.TodayKeyUsage;
            state.TodayTotalUsage = source.TodayTotalUsage;
            state.LifetimeUsage = source.LifetimeUsage;
            state.UsageKeyCount = source.KeyCount;
            state.UsageKeyName = source.KeyName;
            state.UsageError = source.Error;
            state.PlatformHistory = new List<DayUsage>();
            foreach (DayUsage item in source.History)
            {
                DayUsage copy = new DayUsage();
                copy.Day = item.Day;
                copy.Usage = item.Usage;
                state.PlatformHistory.Add(copy);
            }
        }

        private static PlatformUsageSnapshot QueryDeepSeekPlatformUsage(Config cfg, string token, string apiKey, string currency)
        {
            long start;
            long end;
            HistoryWindow(30, out start, out end);
            string costPath = "/api/v0/usage/by_api_key/cost?start="
                + start.ToString(CultureInfo.InvariantCulture)
                + "&end=" + end.ToString(CultureInfo.InvariantCulture)
                + "&tz=28800&bucket=86400";

            Task<string> costTask = Task.Factory.StartNew(delegate { return GetPlatform(cfg, costPath, token); });
            Task<string> keysTask = Task.Factory.StartNew(delegate { return GetPlatform(cfg, "/api/v0/users/get_api_keys", token); });
            Task<string> summaryTask = Task.Factory.StartNew(delegate { return GetPlatform(cfg, "/api/v0/users/get_user_summary", token); });
            Task.WaitAll(new Task[] { costTask, keysTask, summaryTask }, 14000);

            PlatformUsageSnapshot snapshot = new PlatformUsageSnapshot();
            string costBody = PlatformTaskResult(costTask, "近30天按 Key 成本");
            List<Dictionary<string, object>> keyRows = null;
            if (keysTask.IsCompleted && !keysTask.IsFaulted && !keysTask.IsCanceled)
            {
                keyRows = ParsePlatformKeys(ParsePlatformEnvelope(keysTask.Result));
                snapshot.KeyCount = keyRows.Count;
            }

            object costBiz = ParsePlatformEnvelope(costBody);
            Dictionary<string, double> dailyTotals;
            Dictionary<string, Dictionary<string, double>> dailyByKey;
            List<Dictionary<string, object>> costKeys;
            ParsePlatformCost(costBiz, start, end, currency, out dailyTotals, out dailyByKey, out costKeys);
            snapshot.CostOk = true;
            if (snapshot.KeyCount == 0) snapshot.KeyCount = costKeys.Count;
            string todayKey = DataService.TodayKey();
            double todayTotal;
            snapshot.TodayTotalUsage = dailyTotals.TryGetValue(todayKey, out todayTotal)
                ? Math.Round(todayTotal, 8) : 0;
            List<string> historyDays = new List<string>(dailyTotals.Keys);
            historyDays.Sort(StringComparer.Ordinal);
            foreach (string day in historyDays)
            {
                DayUsage item = new DayUsage();
                item.Day = day;
                item.Usage = Math.Round(dailyTotals[day], 8);
                snapshot.History.Add(item);
            }

            Dictionary<string, object> currentKey = FindCurrentKey(apiKey, keyRows, costKeys);
            if (currentKey != null)
            {
                string tracking = KeyField(currentKey, "tracking_id", "trackingId");
                string name = KeyField(currentKey, "name");
                snapshot.KeyName = name;
                Dictionary<string, double> keyDay;
                double current;
                if (!string.IsNullOrEmpty(tracking)
                    && dailyByKey.TryGetValue(todayKey, out keyDay)
                    && keyDay.TryGetValue(tracking, out current))
                {
                    snapshot.TodayKeyUsage = Math.Round(current, 8);
                }
                else
                {
                    snapshot.TodayKeyUsage = 0;
                }
            }
            else
            {
                snapshot.Error = "当前 API Key 未在平台 Key 列表中匹配";
            }

            if (summaryTask.IsCompleted && !summaryTask.IsFaulted && !summaryTask.IsCanceled)
            {
                try
                {
                    snapshot.LifetimeUsage = ParseLifetimeCost(ParsePlatformEnvelope(summaryTask.Result), currency);
                }
                catch (Exception ex)
                {
                    if (string.IsNullOrEmpty(snapshot.Error)) snapshot.Error = "累计消耗：" + ex.Message;
                }
            }
            else
            {
                if (string.IsNullOrEmpty(snapshot.Error)) snapshot.Error = "账户累计消耗查询超时";
            }
            return snapshot;
        }

        private static string PlatformTaskResult(Task<string> task, string label)
        {
            if (!task.IsCompleted) throw new TimeoutException(label + "查询超时");
            if (task.IsCanceled) throw new InvalidOperationException(label + "查询已取消");
            if (task.IsFaulted)
            {
                Exception error = task.Exception == null ? null : task.Exception.GetBaseException();
                throw new InvalidOperationException(label + "：" + (error == null ? "查询失败" : error.Message));
            }
            return task.Result;
        }

        private static object ParsePlatformEnvelope(string body)
        {
            object parsed = Json.ReadAny(body);
            Dictionary<string, object> root = parsed as Dictionary<string, object>;
            if (root == null) throw new InvalidOperationException("平台响应不是 JSON 对象");
            int code = (int)Json.ToNumber(Json.Get(root, "code"), 0);
            if (code != 0)
            {
                if (code == 40002 || code == 40003) throw new InvalidOperationException("平台 Token 无效或已过期");
                throw new InvalidOperationException("平台接口错误 code=" + code.ToString(CultureInfo.InvariantCulture));
            }
            Dictionary<string, object> data = Json.AsObject(Json.Get(root, "data"));
            int bizCode = (int)Json.ToNumber(Json.Get(data, "biz_code"), 0);
            if (bizCode != 0)
            {
                if (bizCode == 40002 || bizCode == 40003) throw new InvalidOperationException("平台 Token 无效或已过期");
                string message = Convert.ToString(Json.Get(data, "biz_msg"), CultureInfo.InvariantCulture);
                throw new InvalidOperationException("平台业务错误 " + bizCode.ToString(CultureInfo.InvariantCulture)
                    + (string.IsNullOrEmpty(message) ? "" : "：" + message));
            }
            return Json.Get(data, "biz_data");
        }

        private static List<Dictionary<string, object>> ParsePlatformKeys(object biz)
        {
            List<Dictionary<string, object>> result = new List<Dictionary<string, object>>();
            List<object> rows = Json.AsArray(biz);
            Dictionary<string, object> map = biz as Dictionary<string, object>;
            if (map != null)
            {
                object nested = GetAny(map, "api_keys", "apiKeys", "keys", "items", "list");
                rows = Json.AsArray(nested);
            }
            foreach (object row in rows)
            {
                Dictionary<string, object> item = row as Dictionary<string, object>;
                if (item != null) result.Add(item);
            }
            return result;
        }

        private static void ParsePlatformCost(object biz, long start, long end, string wantedCurrency,
            out Dictionary<string, double> dailyTotals,
            out Dictionary<string, Dictionary<string, double>> dailyByKey,
            out List<Dictionary<string, object>> keys)
        {
            dailyTotals = new Dictionary<string, double>(StringComparer.Ordinal);
            dailyByKey = new Dictionary<string, Dictionary<string, double>>(StringComparer.Ordinal);
            keys = new List<Dictionary<string, object>>();
            Dictionary<string, object> root = Json.AsObject(biz);
            List<object> groups = Json.AsArray(Json.Get(root, "data"));
            Dictionary<string, object> selectedGroup = null;
            foreach (object groupValue in groups)
            {
                Dictionary<string, object> group = groupValue as Dictionary<string, object>;
                if (group == null) continue;
                if (selectedGroup == null) selectedGroup = group;
                string currency = Convert.ToString(Json.Get(group, "currency"), CultureInfo.InvariantCulture);
                if (!string.IsNullOrEmpty(wantedCurrency)
                    && string.Equals(currency, wantedCurrency, StringComparison.OrdinalIgnoreCase))
                {
                    selectedGroup = group;
                    break;
                }
            }
            if (selectedGroup == null) return;
            foreach (object seriesValue in Json.AsArray(Json.Get(selectedGroup, "series")))
            {
                Dictionary<string, object> series = seriesValue as Dictionary<string, object>;
                if (series == null) continue;
                Dictionary<string, object> keyInfo = Json.AsObject(Json.Get(series, "api_key"));
                string tracking = KeyField(keyInfo, "tracking_id", "trackingId", "id", "name");
                bool knownKey = false;
                foreach (Dictionary<string, object> existing in keys)
                {
                    if (KeyField(existing, "tracking_id", "trackingId", "id", "name") == tracking)
                    {
                        knownKey = true;
                        break;
                    }
                }
                if (!string.IsNullOrEmpty(tracking) && !knownKey)
                {
                    keys.Add(keyInfo);
                }
                foreach (object bucketValue in Json.AsArray(Json.Get(series, "buckets")))
                {
                    Dictionary<string, object> bucket = bucketValue as Dictionary<string, object>;
                    if (bucket == null) continue;
                    long time = (long)Json.ToNumber(Json.Get(bucket, "time"), 0);
                    if (time < start || time >= end) continue;
                    double amount = Json.ToNumber(Json.Get(bucket, "cost"), 0);
                    if (amount <= 0) continue;
                    string day = BeijingDayKey(time);
                    double oldDay;
                    dailyTotals.TryGetValue(day, out oldDay);
                    dailyTotals[day] = oldDay + amount;
                    if (!string.IsNullOrEmpty(tracking))
                    {
                        Dictionary<string, double> keyDay;
                        if (!dailyByKey.TryGetValue(day, out keyDay))
                        {
                            keyDay = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                            dailyByKey[day] = keyDay;
                        }
                        double oldKey;
                        keyDay.TryGetValue(tracking, out oldKey);
                        keyDay[tracking] = oldKey + amount;
                    }
                }
            }
        }

        private static double? ParseLifetimeCost(object biz, string wantedCurrency)
        {
            Dictionary<string, object> root = Json.AsObject(biz);
            double? fallback = null;
            foreach (object itemValue in Json.AsArray(Json.Get(root, "total_costs")))
            {
                Dictionary<string, object> item = itemValue as Dictionary<string, object>;
                if (item == null) continue;
                string currency = Convert.ToString(Json.Get(item, "currency"), CultureInfo.InvariantCulture);
                double value = Math.Round(Json.ToNumber(GetAny(item, "amount", "cost", "total"), 0), 8);
                if (!fallback.HasValue) fallback = value;
                if (string.Equals(currency, wantedCurrency, StringComparison.OrdinalIgnoreCase)) return value;
            }
            return fallback;
        }

        private static Dictionary<string, object> FindCurrentKey(string configuredKey,
            List<Dictionary<string, object>> keyRows, List<Dictionary<string, object>> costKeys)
        {
            if (keyRows != null)
            {
                foreach (Dictionary<string, object> row in keyRows)
                {
                    if (KeyMatches(configuredKey, row)) return row;
                }
                if (keyRows.Count == 1) return keyRows[0];
            }
            if (costKeys != null)
            {
                foreach (Dictionary<string, object> row in costKeys)
                {
                    if (KeyMatches(configuredKey, row)) return row;
                }
                if (costKeys.Count == 1) return costKeys[0];
            }
            return null;
        }

        private static bool KeyMatches(string configured, Dictionary<string, object> keyInfo)
        {
            string key = NormalizeKey(configured).Replace(" ", "");
            string sensitive = KeyField(keyInfo, "sensitive_id", "sensitiveId", "key", "api_key", "apiKey");
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(sensitive)) return false;
            sensitive = sensitive.Replace(" ", "");
            if (string.Equals(key, sensitive, StringComparison.OrdinalIgnoreCase)) return true;
            if (sensitive.IndexOf("***", StringComparison.Ordinal) >= 0)
            {
                int mark = sensitive.LastIndexOf("***", StringComparison.Ordinal) + 3;
                sensitive = sensitive.Substring(mark).Trim('-', '.', '*');
            }
            if (sensitive.Length >= 4 && key.EndsWith(sensitive, StringComparison.OrdinalIgnoreCase)) return true;
            if (sensitive.Length >= 4 && sensitive.EndsWith(key, StringComparison.OrdinalIgnoreCase)) return true;
            return key.Length >= 6 && sensitive.EndsWith(key.Substring(key.Length - 6), StringComparison.OrdinalIgnoreCase);
        }

        private static string KeyField(Dictionary<string, object> data, params string[] names)
        {
            object value = GetAny(data, names);
            return value == null ? "" : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static object GetAny(Dictionary<string, object> data, params string[] names)
        {
            if (data == null) return null;
            foreach (string name in names)
            {
                object value;
                if (data.TryGetValue(name, out value) && value != null) return value;
            }
            return null;
        }

        private static string NormalizePlatformToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return "";
            string text = token.Trim().Trim('"', '\'');
            if (text.StartsWith("{", StringComparison.Ordinal))
            {
                Dictionary<string, object> parsed = Json.ReadAny(text) as Dictionary<string, object>;
                string value = KeyField(parsed, "value", "token", "access_token", "accessToken");
                if (!string.IsNullOrEmpty(value)) text = value.Trim();
            }
            if (text.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) text = text.Substring(7).Trim();
            return text;
        }

        private static string GetPlatform(Config cfg, string path, string token)
        {
            string url = "https://platform.deepseek.com" + path;
            HttpClient client = Http.For(cfg, 15);
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);
            request.Headers.TryAddWithoutValidation("Accept", "application/json");
            request.Headers.TryAddWithoutValidation("Origin", "https://platform.deepseek.com");
            request.Headers.TryAddWithoutValidation("Referer", "https://platform.deepseek.com/usage");
            request.Headers.TryAddWithoutValidation("x-app-version", "1.0.0");
            request.Headers.TryAddWithoutValidation("x-client-platform", "web");
            request.Headers.TryAddWithoutValidation("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
                + "(KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
            HttpResponseMessage response = client.SendAsync(request).Result;
            string body = response.Content.ReadAsStringAsync().Result;
            if (!response.IsSuccessStatusCode)
            {
                int code = (int)response.StatusCode;
                if (code == 401 || code == 403) throw new InvalidOperationException("平台 Token 无效或已过期");
                if (code == 429) throw new InvalidOperationException("平台用量接口请求过于频繁");
                throw new InvalidOperationException("平台用量接口 HTTP " + code.ToString(CultureInfo.InvariantCulture));
            }
            return body;
        }

        private static void HistoryWindow(int days, out long start, out long end)
        {
            if (days < 1) days = 1;
            if (days > 30) days = 30;
            DateTime beijing = DataService.BeijingNow();
            DateTime firstDay = beijing.Date.AddDays(1 - days);
            DateTime localMidnight = new DateTime(firstDay.Year, firstDay.Month, firstDay.Day, 0, 0, 0, DateTimeKind.Unspecified);
            DateTime utc = DateTime.SpecifyKind(localMidnight.AddHours(-8), DateTimeKind.Utc);
            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            start = (long)(utc - epoch).TotalSeconds;
            end = start + days * 86400L;
        }

        private static string BeijingDayKey(long time)
        {
            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return epoch.AddSeconds(time).AddHours(8).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        private static void AddDetail(ProviderState state, string label, double? value)
        {
            if (!value.HasValue || value.Value <= 0) return;
            state.Details.Add(label + " " + Fmt.Money(value, state.Currency));
        }
    }
}
