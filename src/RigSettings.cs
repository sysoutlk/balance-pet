// Per-single-image rig settings. Existing rig.* values remain the fallback so
// configs written before rigRoles was introduced keep working unchanged.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace BalancePet
{
    internal static class RigSettings
    {
        private const string Section = "rigRoles";

        public static bool IsSinglePngRole(string role)
        {
            if (string.IsNullOrEmpty(role)) return false;
            if (!string.Equals(Path.GetExtension(role), ".png", StringComparison.OrdinalIgnoreCase)) return false;
            try
            {
                return File.Exists(Path.Combine(AppPaths.RolesDir, role));
            }
            catch
            {
                return false;
            }
        }

        public static bool GetFlag(Config cfg, string role, string key, bool fallback)
        {
            Dictionary<string, object> profile = cfg == null || string.IsNullOrEmpty(role)
                ? new Dictionary<string, object>()
                : cfg.GetObject(Section, role);
            object value;
            if (profile.TryGetValue(key, out value)) return Json.ToBool(value, fallback);
            return cfg == null ? fallback : cfg.GetFlag("rig", key, fallback);
        }

        public static double GetNumber(Config cfg, string role, string key, double fallback)
        {
            Dictionary<string, object> profile = cfg == null || string.IsNullOrEmpty(role)
                ? new Dictionary<string, object>()
                : cfg.GetObject(Section, role);
            object value;
            if (profile.TryGetValue(key, out value)) return Json.ToNumber(value, fallback);
            return cfg == null ? fallback : cfg.GetNumber("rig", key, fallback);
        }

        public static void Set(Config cfg, string role, string key, object value)
        {
            if (cfg == null || string.IsNullOrEmpty(role)) return;
            Dictionary<string, object> profile = cfg.GetObject(Section, role);
            profile[key] = value;
            cfg.Set(Section, role, profile);
        }
    }
}
