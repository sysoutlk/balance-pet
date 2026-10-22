// Declarative import-profile metadata plus the shared one-click detector.
// Vendor-specific assets are never bundled; profiles only select a neutral local converter.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

namespace BalancePet
{
    internal sealed class ImportFormatProfile
    {
        public string Id = "";
        public string DisplayName = "";
        public string DefaultPackId = "";
        public string Kind = "";
        public int Version;
    }

    internal static class ImportFormatProfiles
    {
        private const string ResourcePrefix = "BalancePet.Profiles.";

        public static List<ImportFormatProfile> Load()
        {
            List<ImportFormatProfile> profiles = new List<ImportFormatProfile>();
            Assembly assembly = Assembly.GetExecutingAssembly();
            string[] resources = assembly.GetManifestResourceNames();
            Array.Sort(resources, StringComparer.OrdinalIgnoreCase);
            foreach (string resource in resources)
            {
                if (!resource.StartsWith(ResourcePrefix, StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    using (Stream stream = assembly.GetManifestResourceStream(resource))
                    {
                        if (stream == null) continue;
                        using (StreamReader reader = new StreamReader(stream, Encoding.UTF8, true))
                        {
                            Dictionary<string, object> raw = Json.ReadText(reader.ReadToEnd());
                            ImportFormatProfile profile = new ImportFormatProfile();
                            profile.Id = Convert.ToString(Json.Get(raw, "id"), CultureInfo.InvariantCulture);
                            profile.DisplayName = Convert.ToString(Json.Get(raw, "displayName"), CultureInfo.InvariantCulture);
                            profile.DefaultPackId = Convert.ToString(
                                Json.Get(raw, "defaultPackId"), CultureInfo.InvariantCulture);
                            profile.Kind = Convert.ToString(Json.Get(raw, "kind"), CultureInfo.InvariantCulture);
                            profile.Version = (int)Json.ToNumber(Json.Get(raw, "version"), 1);
                            if (string.IsNullOrEmpty(profile.Id)
                                || string.IsNullOrEmpty(profile.DisplayName)
                                || string.IsNullOrEmpty(profile.Kind)) continue;
                            profiles.Add(profile);
                        }
                    }
                }
                catch
                {
                }
            }
            if (profiles.Count == 0)
            {
                profiles.Add(new ImportFormatProfile
                {
                    Id = "layered-png-sequence-v1",
                    DisplayName = "分层 PNG 序列素材包",
                    DefaultPackId = "layered-png-sequence",
                    Kind = "layeredPngSequence",
                    Version = 1
                });
                profiles.Add(new ImportFormatProfile
                {
                    Id = "webm-preview-pack-v1",
                    DisplayName = "WebM + 预览图素材包",
                    DefaultPackId = "webm-preview-pack",
                    Kind = "webmPreviewPack",
                    Version = 1
                });
            }
            return profiles;
        }
    }

    internal static class GenericPackImporter
    {
        public static bool TryDetect(string source, out ImportFormatProfile profile,
            out string matchedSource, out string suggestedName)
        {
            profile = null;
            matchedSource = "";
            suggestedName = "imported-pet";
            foreach (ImportFormatProfile candidate in ImportFormatProfiles.Load())
            {
                string detectedSource = "";
                string detectedName = "";
                bool matched;
                if (string.Equals(candidate.Kind, "layeredPngSequence", StringComparison.OrdinalIgnoreCase))
                {
                    matched = LayeredPngSequenceImporter.TryLocate(
                        source, out detectedSource, out detectedName);
                }
                else if (string.Equals(candidate.Kind, "webmPreviewPack", StringComparison.OrdinalIgnoreCase))
                {
                    matched = WebmPreviewPackImporter.TryLocate(
                        source, out detectedSource, out detectedName);
                }
                else
                {
                    matched = false;
                }
                if (!matched) continue;
                profile = candidate;
                matchedSource = detectedSource;
                if (!string.IsNullOrEmpty(candidate.DefaultPackId))
                    suggestedName = candidate.DefaultPackId;
                else if (!string.IsNullOrEmpty(detectedName))
                    suggestedName = detectedName;
                return true;
            }
            return false;
        }

        public static bool Convert(ImportFormatProfile profile, string source, string target,
            out string note, out string error)
        {
            note = "";
            error = "";
            if (profile == null)
            {
                error = "导入格式未识别";
                return false;
            }
            bool ok;
            if (string.Equals(profile.Kind, "layeredPngSequence", StringComparison.OrdinalIgnoreCase))
            {
                ok = LayeredPngSequenceImporter.Import(source, target, out note, out error);
            }
            else if (string.Equals(profile.Kind, "webmPreviewPack", StringComparison.OrdinalIgnoreCase))
            {
                ok = WebmPreviewPackImporter.Import(source, target, out note, out error);
            }
            else
            {
                error = "不支持的导入格式";
                return false;
            }
            if (ok && !string.IsNullOrEmpty(profile.DisplayName))
                note = profile.DisplayName + "：" + note;
            return ok;
        }
    }
}
