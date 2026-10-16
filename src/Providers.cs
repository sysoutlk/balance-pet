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
            deepseek.Note = "官方 /\u7528\u6237\u4f59\u989d\u63a5\u53e3\uff0c\u542b\u8d60\u91d1\u4e0e\u5145\u503c\u62c6\u5206\u3002";
            list.Add(deepseek);

            ProviderDef moonshot = new ProviderDef();
            moonshot.Id = "moonshot";
            moonshot.Name = "Kimi\u6708\u4e4b\u6697\u9762";
            moonshot.Currency = "CNY";
            moonshot.Note = "\u5b98\u65b9\u4f59\u989d\u63a5\u53e3\uff08\u73b0\u91d1 + \u4ee3\u91d1\u5238\uff09\u3002\u56fd\u9645\u7ad9\u53ef\u6539 baseUrl \u4e3a https://api.moonshot.ai\u3002";
            list.Add(moonshot);

            ProviderDef siliconflow = new ProviderDef();
            siliconflow.Id = "siliconflow";
            siliconflow.Name = "\u7845\u57fa\u6d41\u52a8";
            siliconflow.Currency = "CNY";
            siliconflow.Note = "\u5b98\u65b9 user/info \u63a5\u53e3\uff08\u5145\u503c + \u8d60\u9001\uff09\u3002";
            list.Add(siliconflow);

            ProviderDef openrouter = new ProviderDef();
            openrouter.Id = "openrouter";
            openrouter.Name = "OpenRouter";
            openrouter.Currency = "USD";
            openrouter.Note = "\u7f8e\u5143\u989d\u5ea6\uff0c\u53ef\u8986\u76d6 GPT / Claude / Gemini / Grok \u7b49\u6d77\u5916\u6a21\u578b\u3002";
            list.Add(openrouter);

            ProviderDef qwen = new ProviderDef();
            qwen.Id = "qwen";
            qwen.Name = "\u901a\u4e49\u5343\u95ee DashScope";
            qwen.HasBalance = false;
            qwen.Note = "DashScope \u672a\u5f00\u653e API Key \u4f59\u989d\u63a5\u53e3\uff0c\u8bf7\u5728\u963f\u91cc\u4e91\u8d39\u7528\u4e2d\u5fc3\u67e5\u770b\uff1b\u81ea\u5efa\u4e2d\u8f6c\u53ef\u7528\u300c\u81ea\u5b9a\u4e49\u63a5\u53e3\u300d\u63a5\u5165\u3002";
            list.Add(qwen);

            ProviderDef doubao = new ProviderDef();
            doubao.Id = "doubao";
            doubao.Name = "\u8c46\u5305 \u706b\u5c71\u65b9\u821f";
            doubao.HasBalance = false;
            doubao.Note = "\u706b\u5c71\u65b9\u821f\u672a\u5f00\u653e API Key \u4f59\u989d\u63a5\u53e3\uff0c\u8bf7\u5728\u706b\u5c71\u5f15\u64ce\u63a7\u5236\u53f0\u67e5\u770b\u3002";
            list.Add(doubao);

            ProviderDef zhipu = new ProviderDef();
            zhipu.Id = "zhipu";
            zhipu.Name = "\u667a\u8c31 GLM";
            zhipu.HasBalance = false;
            zhipu.Note = "\u667a\u8c31\u5f00\u653e\u5e73\u53f0\u672a\u5f00\u653e API Key \u4f59\u989d\u63a5\u53e3\uff0c\u8bf7\u5728\u63a7\u5236\u53f0\u67e5\u770b\u3002";
            list.Add(zhipu);

            ProviderDef openai = new ProviderDef();
            openai.Id = "openai";
            openai.Name = "OpenAI GPT";
            openai.Currency = "USD";
            openai.HasBalance = false;
            openai.Note = "OpenAI \u5b98\u65b9\u65e0 Key \u4f59\u989d\u63a5\u53e3\uff08\u4ec5 Admin Key \u53ef\u67e5\u6210\u672c\u62a5\u8868\uff09\uff1b\u5efa\u8bae\u7528 OpenRouter \u6216\u81ea\u5b9a\u4e49\u63a5\u53e3\u3002";
            list.Add(openai);

            ProviderDef anthropic = new ProviderDef();
            anthropic.Id = "anthropic";
            anthropic.Name = "Anthropic Claude";
            anthropic.Currency = "USD";
            anthropic.HasBalance = false;
            anthropic.Note = "Claude \u5b98\u65b9\u65e0 Key \u4f59\u989d\u63a5\u53e3\uff08Admin Key \u53ef\u67e5\u6210\u672c\u62a5\u8868\uff09\uff1b\u5efa\u8bae\u7528 OpenRouter \u6216\u81ea\u5b9a\u4e49\u63a5\u53e3\u3002";
            list.Add(anthropic);

            ProviderDef gemini = new ProviderDef();
            gemini.Id = "gemini";
            gemini.Name = "Google Gemini";
            gemini.Currency = "USD";
            gemini.HasBalance = false;
            gemini.Note = "Gemini \u65e0 Key \u4f59\u989d\u63a5\u53e3\uff0c\u8d39\u7528\u8d70 Google Cloud \u7ed3\u7b97\uff0c\u8bf7\u5728\u4e91\u63a7\u5236\u53f0\u67e5\u770b\u3002";
            list.Add(gemini);

            ProviderDef grok = new ProviderDef();
            grok.Id = "xai";
            grok.Name = "xAI Grok";
            grok.Currency = "USD";
            grok.HasBalance = false;
            grok.Note = "xAI \u6682\u672a\u63d0\u4f9b API Key \u4f59\u989d\u63a5\u53e3\uff0c\u8bf7\u5728 xAI Console \u67e5\u770b\u3002";
            list.Add(grok);

            return list;
        }

        /// <summary>Built-in providers plus the configurable custom endpoint.</summary>
        public static List<ProviderDef> Effective(Config cfg)
        {
            List<ProviderDef> list = Builtin();
            ProviderDef custom = new ProviderDef();
            custom.Id = "custom";
            custom.Name = cfg.GetString("custom", "name", "\u81ea\u5b9a\u4e49\u63a5\u53e3");
            if (string.IsNullOrEmpty(custom.Name)) custom.Name = "\u81ea\u5b9a\u4e49\u63a5\u53e3";
            custom.Currency = cfg.GetString("custom", "currency", "CNY");
            if (string.IsNullOrEmpty(custom.Currency)) custom.Currency = "CNY";
            custom.Auth = cfg.GetString("custom", "auth", "bearer");
            custom.IsCustom = true;
            custom.Note = "\u4efb\u610f\u63d0\u4f9b\u4f59\u989d JSON \u7684\u63a5\u53e3\uff08\u4e2d\u8f6c / \u81ea\u5efa\u7f51\u5173\uff09\u3002";
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
                        state.Error = "\u672a\u914d\u7f6e\u63a5\u53e3 URL";
                        return state;
                    }
                    url = url.Replace("{key}", Uri.EscapeDataString(key == null ? "" : key));
                    if (auth != "none" && string.IsNullOrEmpty(key))
                    {
                        state.Error = "\u672a\u914d\u7f6e API Key";
                        return state;
                    }
                }
                else
                {
                    url = EndpointFor(cfg, def);
                    if (string.IsNullOrEmpty(key))
                    {
                        state.Error = "\u672a\u914d\u7f6e API Key";
                        return state;
                    }
                }
                string body = Get(url, auth, key);
                Parse(def, cfg, body, state);
                state.Ok = state.Balance.HasValue;
                if (!state.Ok && string.IsNullOrEmpty(state.Error)) state.Error = "\u54cd\u5e94\u91cc\u6ca1\u6709\u4f59\u989d\u5b57\u6bb5";
            }
            catch (Exception ex)
            {
                Exception inner = ex.InnerException;
                state.Error = inner != null && !string.IsNullOrEmpty(inner.Message) ? inner.Message : ex.Message;
            }
            return state;
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
                if (code == 401 || code == 403) throw new Exception("\u5bc6\u94a5\u65e0\u6548\u6216\u6743\u9650\u4e0d\u8db3");
                if (code == 404) throw new Exception("\u63a5\u53e3\u4e0d\u5b58\u5728\uff08HTTP 404\uff09");
                if (code == 429) throw new Exception("\u8bf7\u6c42\u8fc7\u4e8e\u9891\u7e41\uff0c\u7a0d\u540e\u518d\u8bd5");
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
                    state.Error = "\u54cd\u5e94\u91cc\u6ca1\u6709 balance_infos";
                    return;
                }
                string infoCurrency = Convert.ToString(Json.Get(info, "currency"), CultureInfo.InvariantCulture);
                if (!string.IsNullOrEmpty(infoCurrency)) state.Currency = infoCurrency;
                state.Balance = Json.ToNumberOrNull(Json.Get(info, "total_balance"));
                AddDetail(state, "\u8d60\u91d1", Json.ToNumberOrNull(Json.Get(info, "granted_balance")));
                AddDetail(state, "\u5145\u503c", Json.ToNumberOrNull(Json.Get(info, "topped_up_balance")));
                return;
            }
            if (def.Id == "moonshot")
            {
                Dictionary<string, object> node = Json.AsObject(Json.Get(data, "data"));
                state.Balance = Json.ToNumberOrNull(Json.Get(node, "available_balance"));
                AddDetail(state, "\u4ee3\u91d1\u5238", Json.ToNumberOrNull(Json.Get(node, "voucher_balance")));
                AddDetail(state, "\u73b0\u91d1", Json.ToNumberOrNull(Json.Get(node, "cash_balance")));
                return;
            }
            if (def.Id == "siliconflow")
            {
                Dictionary<string, object> node = Json.AsObject(Json.Get(data, "data"));
                state.Balance = Json.ToNumberOrNull(Json.Get(node, "totalBalance"));
                if (!state.Balance.HasValue) state.Balance = Json.ToNumberOrNull(Json.Get(node, "balance"));
                AddDetail(state, "\u5145\u503c", Json.ToNumberOrNull(Json.Get(node, "chargeBalance")));
                AddDetail(state, "\u8d60\u9001", Json.ToNumberOrNull(Json.Get(node, "balance")));
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
                AddDetail(state, "\u989d\u5ea6\u603b\u989d", credits);
                AddDetail(state, "\u5df2\u7528", usage);
                return;
            }
            state.Error = "\u672a\u77e5\u7684\u63d0\u4f9b\u5546";
        }

        private static void AddDetail(ProviderState state, string label, double? value)
        {
            if (!value.HasValue || value.Value <= 0) return;
            state.Details.Add(label + " " + Fmt.Money(value, state.Currency));
        }
    }
}
