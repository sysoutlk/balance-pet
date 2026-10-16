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
using System.Net.Http;
using System.Threading.Tasks;

namespace DshPet
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
        public bool IsCustom;
        public string Note = "";
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
        private static readonly HttpClient Client = CreateClient();
        public static string Version = "1.0.0";

        private static HttpClient CreateClient()
        {
            HttpClient client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(8);
            try
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "dsh-pet/" + Version);
            }
            catch
            {
            }
            return client;
        }

        public static List<ProviderDef> Builtin()
        {
            List<ProviderDef> list = new List<ProviderDef>();

            ProviderDef deepseek = new ProviderDef();
            deepseek.Id = "deepseek";
            deepseek.Name = "DeepSeek";
            deepseek.Currency = "CNY";
            deepseek.Note = "官方 /用户余额接口，含赠金与充值拆分。";
            list.Add(deepseek);

            ProviderDef moonshot = new ProviderDef();
            moonshot.Id = "moonshot";
            moonshot.Name = "Kimi月之暗面";
            moonshot.Currency = "CNY";
            moonshot.Note = "官方余额接口（现金 + 代金券）。国际站可改 baseUrl 为 https://api.moonshot.ai。";
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
                    return legacy.Trim();
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
                    url = EndpointFor(cfg, def);
                    if (string.IsNullOrEmpty(key))
                    {
                        state.Error = "未配置 API Key";
                        return state;
                    }
                }
                string body = Get(url, auth, key);
                Parse(def, cfg, body, state);
                state.Ok = state.Balance.HasValue;
                if (!state.Ok && string.IsNullOrEmpty(state.Error)) state.Error = "响应里没有余额字段";
                if (!state.Ok) Log.Write("provider " + def.Id + " parse failed: " + Snippet(body));
            }
            catch (Exception ex)
            {
                Exception inner = ex.InnerException;
                state.Error = inner != null && !string.IsNullOrEmpty(inner.Message) ? inner.Message : ex.Message;
                Log.Write("provider " + def.Id + " failed: " + state.Error);
            }
            return state;
        }

        private static string Snippet(string text)
        {
            if (string.IsNullOrEmpty(text)) return "<empty>";
            text = text.Replace("\r", " ").Replace("\n", " ");
            return text.Length <= 400 ? text : text.Substring(0, 400) + "…";
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

        private static string Get(string url, string auth, string key)
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
            if (auth == "bearer" && !string.IsNullOrEmpty(key))
            {
                request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + key);
            }
            else if (auth == "x-api-key" && !string.IsNullOrEmpty(key))
            {
                request.Headers.TryAddWithoutValidation("x-api-key", key);
            }
            HttpResponseMessage response = Client.SendAsync(request).Result;
            if (!response.IsSuccessStatusCode)
            {
                int code = (int)response.StatusCode;
                if (code == 401 || code == 403) throw new Exception("密钥无效或权限不足");
                if (code == 404) throw new Exception("接口不存在（HTTP 404）");
                if (code == 429) throw new Exception("请求过于频繁，稍后再试");
                throw new Exception("HTTP " + code);
            }
            return response.Content.ReadAsStringAsync().Result;
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

        private static void AddDetail(ProviderState state, string label, double? value)
        {
            if (!value.HasValue || value.Value <= 0) return;
            state.Details.Add(label + " " + Fmt.Money(value, state.Currency));
        }
    }
}
