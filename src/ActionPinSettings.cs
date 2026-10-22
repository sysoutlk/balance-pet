// A per-role action pin. When enabled, automatic actions repeat the chosen
// action instead of drawing from the random pool.
using System;
using System.Collections.Generic;

namespace BalancePet
{
    internal static class ActionPinSettings
    {
        private const string Section = "actionPin";

        public static string RoleKey(string role)
        {
            return string.IsNullOrEmpty(role) ? "__builtin" : role;
        }

        public static bool Enabled(Config cfg, string role)
        {
            if (cfg == null) return false;
            Dictionary<string, object> item = cfg.GetObject(Section, RoleKey(role));
            return Json.ToBool(Json.Get(item, "enabled"), false);
        }

        public static string Action(Config cfg, string role)
        {
            if (cfg == null) return "";
            Dictionary<string, object> item = cfg.GetObject(Section, RoleKey(role));
            object value = Json.Get(item, "action");
            return value == null ? "" : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
        }

        public static string ValidAction(Config cfg, string role, PetPack pack, List<string> builtinPool)
        {
            if (!Enabled(cfg, role)) return "";
            string key = Action(cfg, role);
            if (string.IsNullOrEmpty(key)) return "";
            if (pack != null) return pack.Action(key) == null ? "" : key;
            return builtinPool != null && builtinPool.Contains(key) ? key : "";
        }

        public static void Set(Config cfg, string role, string action, bool enabled)
        {
            if (cfg == null) return;
            Dictionary<string, object> item = new Dictionary<string, object>();
            item["enabled"] = enabled;
            item["action"] = enabled ? (action ?? "") : "";
            cfg.Set(Section, RoleKey(role), item);
        }
    }
}
