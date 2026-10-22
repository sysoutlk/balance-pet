// Folder layout. Everything is resolved next to the executable so the app stays a
// portable folder: balance-pet.exe + pet-config.json + roles\.
using System;
using System.Collections.Generic;
using System.IO;

namespace BalancePet
{
    internal static class AppPaths
    {
        public static string Root
        {
            get { return AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/'); }
        }

        /// <summary>
        /// Where runtime data lives: %APPDATA%\BalancePet by default, so double-clicking the exe
        /// anywhere never scatters pet-config/pet-state/roles next to it. A portable.txt next to
        /// the exe switches everything back to the exe folder (green/portable mode).
        /// </summary>
        public static string DataRoot
        {
            get
            {
                try
                {
                    string overrideRoot = Environment.GetEnvironmentVariable("BALANCE_PET_DATA_ROOT");
                    if (!string.IsNullOrEmpty(overrideRoot))
                    {
                        Directory.CreateDirectory(overrideRoot);
                        return overrideRoot.TrimEnd('\\', '/');
                    }
                    if (File.Exists(Path.Combine(Root, "portable.txt"))) return Root;
                    string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    if (string.IsNullOrEmpty(appData)) return Root;
                    string dir = Path.Combine(appData, "BalancePet");
                    Directory.CreateDirectory(dir);
                    return dir;
                }
                catch
                {
                    return Root;
                }
            }
        }

        public static string RolesDir { get { return Path.Combine(DataRoot, "roles"); } }

        public static string ConfigPath { get { return Path.Combine(DataRoot, "pet-config.json"); } }

        public static string DraftConfigPath { get { return Path.Combine(DataRoot, "pet-config.draft.json"); } }

        public static string StatePath { get { return Path.Combine(DataRoot, "pet-state.json"); } }

        /// <summary>Character images the user dropped into roles\. None are bundled with the
        /// repository; the built-in vector mascot covers the empty case.</summary>
        /// <summary>装扮/挂件目录（帽子之类的小素材放这里）。</summary>
        public static string PropsDir
        {
            get
            {
                string dir = Path.Combine(DataRoot, "props");
                try { Directory.CreateDirectory(dir); } catch { }
                return dir;
            }
        }

        /// <summary>
        /// 解析挂件路径：`props\帽子.png` 这类相对路径按仓库根目录算，只写文件名则去 props\ 里找。
        /// 找不到返回空串。
        /// </summary>
        public static string ResolveProp(string name)
        {
            try
            {
                if (string.IsNullOrEmpty(name)) return "";
                string direct = Path.IsPathRooted(name) ? name : Path.Combine(Root, name);
                if (File.Exists(direct)) return direct;
                string inProps = Path.Combine(PropsDir, Path.GetFileName(name));
                if (File.Exists(inProps)) return inProps;
            }
            catch
            {
            }
            return "";
        }

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
                string[] patterns = new string[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif" };
                foreach (string pattern in patterns)
                {
                    string[] files = Directory.GetFiles(path, pattern);
                    if (files.Length > 0)
                    {
                        Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                        return files[0];
                    }
                }
                string[] dirs = Directory.GetDirectories(path, "*", SearchOption.AllDirectories);
                Array.Sort(dirs, StringComparer.OrdinalIgnoreCase);
                foreach (string dir in dirs)
                {
                    foreach (string pattern in patterns)
                    {
                        string[] files = Directory.GetFiles(dir, pattern);
                        if (files.Length > 0)
                        {
                            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                            return files[0];
                        }
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

        /// <summary>
        /// One-time migration from the old portable layout: copy config, usage history, roles,
        /// props and quips from the exe folder into the user data folder.
        /// </summary>
        public static void MigratePortableData()
        {
            try
            {
                string data = DataRoot;
                if (string.Equals(data.TrimEnd('\\', '/'), Root.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase)) return;
                if (File.Exists(Path.Combine(data, "pet-config.json"))) return;
                string oldConfig = Path.Combine(Root, "pet-config.json");
                if (!File.Exists(oldConfig)) return;
                CopyFileIfMissing(oldConfig, Path.Combine(data, "pet-config.json"));
                CopyFileIfMissing(Path.Combine(Root, "pet-state.json"), Path.Combine(data, "pet-state.json"));
                CopyFileIfMissing(Path.Combine(Root, "quips.json"), Path.Combine(data, "quips.json"));
                CopyDirIfMissing(Path.Combine(Root, "roles"), Path.Combine(data, "roles"));
                CopyDirIfMissing(Path.Combine(Root, "props"), Path.Combine(data, "props"));
            }
            catch
            {
            }
        }

        private static void CopyFileIfMissing(string from, string to)
        {
            if (!File.Exists(from) || File.Exists(to)) return;
            File.Copy(from, to, false);
        }

        private static void CopyDirIfMissing(string from, string to)
        {
            if (!Directory.Exists(from)) return;
            if (Directory.Exists(to) && Directory.GetFileSystemEntries(to).Length > 0) return;
            Directory.CreateDirectory(to);
            string prefix = from.TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            foreach (string dir in Directory.GetDirectories(from, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(Path.Combine(to, dir.Substring(prefix.Length)));
            }
            foreach (string file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
            {
                string target = Path.Combine(to, file.Substring(prefix.Length));
                string parent = Path.GetDirectoryName(target);
                if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
                if (!File.Exists(target)) File.Copy(file, target, false);
            }
        }
    }
}
