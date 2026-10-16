// Folder layout. Everything is resolved next to the executable so the app stays a
// portable folder: balance-pet.exe + pet-config.json + roles\.
using System;
using System.Collections.Generic;
using System.IO;

namespace DshPet
{
    internal static class AppPaths
    {
        public static string Root
        {
            get { return AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/'); }
        }

        public static string RolesDir { get { return Path.Combine(Root, "roles"); } }

        public static string ConfigPath { get { return Path.Combine(Root, "pet-config.json"); } }

        public static string StatePath { get { return Path.Combine(Root, "pet-state.json"); } }

        /// <summary>Character images the user dropped into roles\. None are bundled with the
        /// repository; the built-in vector mascot covers the empty case.</summary>
        public static List<string> RoleFiles()
        {
            List<string> files = new List<string>();
            try
            {
                if (!Directory.Exists(RolesDir)) return files;
                List<string> found = new List<string>();
                string[] patterns = new string[] { "*.png", "*.jpg", "*.jpeg", "*.gif", "*.bmp" };
                foreach (string pattern in patterns)
                {
                    foreach (string path in Directory.GetFiles(RolesDir, pattern))
                    {
                        found.Add(Path.GetFileName(path));
                    }
                }
                found.Sort(StringComparer.CurrentCulture);
                files.AddRange(found);
            }
            catch
            {
            }
            return files;
        }
    }
}
