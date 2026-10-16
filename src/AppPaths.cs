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
        /// <summary>
        /// 角色缩略图用的图片文件：文件角色就是它自己；文件夹角色（逐帧动画）取第一帧。
        /// 找不到时返回空串，调用方退回内置小鲸鱼缩略图。
        /// </summary>
        public static string RoleThumbFile(string role)
        {
            try
            {
                string path = Path.Combine(RolesDir, role);
                if (File.Exists(path)) return path;
                if (!Directory.Exists(path)) return "";
                string[] patterns = new string[] { "*.png", "*.jpg", "*.jpeg", "*.bmp" };
                foreach (string pattern in patterns)
                {
                    string[] files = Directory.GetFiles(path, pattern);
                    if (files.Length > 0)
                    {
                        Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                        return files[0];
                    }
                }
                foreach (string dir in Directory.GetDirectories(path))
                {
                    string[] files = Directory.GetFiles(dir, "*.png");
                    if (files.Length > 0)
                    {
                        Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                        return files[0];
                    }
                }
            }
            catch
            {
            }
            return "";
        }

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
                // 逐帧角色是一个文件夹（roles\名字\idle\frame_00.png ...），也列进来
                foreach (string dir in Directory.GetDirectories(RolesDir))
                {
                    found.Add(Path.GetFileName(dir));
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
