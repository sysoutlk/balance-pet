// Installs a local pet pack into roles\.
//
// The installer never downloads or bundles third-party assets. It only copies a
// directory the user picked, validates our own pet.json/state-folder structure,
// and gives the pack a safe local id. Existing ids get a -2/-3 suffix.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace BalancePet
{
    internal static class PetPackInstaller
    {
        public static string Install(Config cfg, string source, out string note, out string error)
        {
            note = "";
            error = "";
            if (cfg == null)
            {
                error = "程序配置尚未加载";
                return null;
            }
            if (string.IsNullOrEmpty(source) || !Directory.Exists(source))
            {
                error = "宠物包目录不存在";
                return null;
            }
            source = Path.GetFullPath(source);
            string manifest = Path.Combine(source, "pet.json");
            bool hasManifest = File.Exists(manifest);
            bool hasState = HasStateFolder(source);
            string xmlActions = "";
            string xmlImages = "";
            string xmlName = "xml-sprite";
            bool hasXmlSpritePack = false;
            string spriteAtlasManifest = "";
            string spriteAtlasImage = "";
            string spriteAtlasName = "sprite-atlas";
            bool hasSpriteAtlasPack = SpriteAtlasPackConverter.TryLocate(
                source, out spriteAtlasManifest, out spriteAtlasImage, out spriteAtlasName);
            string actionFolder = "";
            string actionFolderName = "action-folder-pet";
            bool hasActionFolderPack = false;
            hasXmlSpritePack = XmlSpritePackConverter.TryLocate(
                source, out xmlActions, out xmlImages, out xmlName);
            if (!hasXmlSpritePack && !hasSpriteAtlasPack)
            {
                hasActionFolderPack = ActionFolderPackConverter.TryLocate(
                    source, out actionFolder, out actionFolderName);
            }
            if (!hasManifest && !hasState && !hasXmlSpritePack
                && !hasSpriteAtlasPack && !hasActionFolderPack)
            {
                error = "目录里没有 pet.json、状态帧目录、XML 精灵、WebP 图集或动作文件夹";
                return null;
            }

            Dictionary<string, object> raw = null;
            if (hasManifest)
            {
                try
                {
                    object parsed = Json.ReadAny(File.ReadAllText(manifest, Encoding.UTF8));
                    raw = parsed as Dictionary<string, object>;
                    if (raw == null)
                    {
                        error = "pet.json 不是有效的 JSON 对象";
                        return null;
                    }
                }
                catch (Exception ex)
                {
                    error = "读取 pet.json 失败：" + ex.Message;
                    return null;
                }
            }

            string id = hasXmlSpritePack ? xmlName
                : (hasSpriteAtlasPack ? spriteAtlasName
                    : (hasActionFolderPack ? actionFolderName
                        : Path.GetFileName(source.TrimEnd('\\', '/'))));
            if (raw != null)
            {
                string declared = Convert.ToString(Json.Get(raw, "id"), CultureInfo.InvariantCulture);
                if (!string.IsNullOrEmpty(declared)) id = declared;
            }
            id = Sanitize(id);
            if (id.Length == 0) id = "pack";
            if (id.Length > 64) id = id.Substring(0, 64);

            string rolesDir = AppPaths.RolesDir;
            Directory.CreateDirectory(rolesDir);
            string target = Path.Combine(rolesDir, id);
            int suffix = 2;
            while (Directory.Exists(target) || File.Exists(target))
            {
                target = Path.Combine(rolesDir, id + "-" + suffix.ToString(CultureInfo.InvariantCulture));
                suffix++;
            }
            if (IsSameOrInsideAbsolute(target, source))
            {
                error = "不能从宠物包将要复制进去的上级目录导入";
                return null;
            }

            try
            {
                if (hasXmlSpritePack)
                {
                    if (!XmlSpritePackConverter.Convert(xmlActions, xmlImages, target, out note, out error))
                    {
                        if (!TryDelete(target)) error += "；临时目录未能清理：" + target;
                        return null;
                    }
                }
                else if (hasSpriteAtlasPack)
                {
                    if (!SpriteAtlasPackConverter.Convert(spriteAtlasManifest, spriteAtlasImage,
                            target, out note, out error))
                    {
                        if (!TryDelete(target)) error += "；临时目录未能清理：" + target;
                        return null;
                    }
                }
                else if (hasActionFolderPack)
                {
                    if (!ActionFolderPackConverter.Convert(actionFolder, target,
                            out note, out error))
                    {
                        if (!TryDelete(target)) error += "；临时目录未能清理：" + target;
                        return null;
                    }
                }
                else
                {
                    CopyFolder(source, target, true);
                }
                File.WriteAllText(Path.Combine(target, "imported-pack.txt"),
                    "imported=1" + Environment.NewLine, new UTF8Encoding(false));
                string installedId = Path.GetFileName(target);
                if (!ValidateInstalledPack(cfg, installedId, out error))
                {
                    if (!TryDelete(target)) error += "；临时目录未能清理：" + target;
                    return null;
                }
                return installedId;
            }
            catch (Exception ex)
            {
                error = "导入失败：" + ex.Message;
                if (!TryDelete(target)) error += "；临时目录未能清理：" + target;
                return null;
            }
        }

        private static bool HasStateFolder(string source)
        {
            try
            {
                foreach (string dir in Directory.GetDirectories(source))
                {
                    string name = Path.GetFileName(dir).ToLowerInvariant();
                    if (name == "preview" || name == "clips" || name.StartsWith(".")) continue;
                    string[] images = Directory.GetFiles(dir, "*.png");
                    if (images.Length == 0) images = Directory.GetFiles(dir, "*.gif");
                    if (images.Length == 0) images = Directory.GetFiles(dir, "*.jpg");
                    if (images.Length == 0) images = Directory.GetFiles(dir, "*.jpeg");
                    if (images.Length > 0) return true;
                }
            }
            catch
            {
            }
            return false;
        }

        private static bool ValidateInstalledPack(Config cfg, string installedId, out string error)
        {
            error = "";
            PetPack pack = PetPack.TryLoad(cfg, installedId);
            if (pack == null)
            {
                error = "pet.json 或状态目录无法解析";
                return false;
            }
            List<string> idle = pack.StateClips("idle");
            if (idle == null || idle.Count == 0)
            {
                error = "宠物包缺少 idle 状态";
                return false;
            }
            foreach (KeyValuePair<string, List<string>> state in pack.States)
            {
                foreach (string clipName in state.Value)
                {
                    if (pack.Clip(clipName) == null)
                    {
                        error = "状态 " + state.Key + " 引用了不存在的片段 " + clipName;
                        return false;
                    }
                }
            }
            foreach (string actionKey in pack.ActionKeys())
            {
                PackAction action = pack.Action(actionKey);
                if (action == null || action.Clips.Count == 0)
                {
                    error = "动作 " + actionKey + " 没有配置片段";
                    return false;
                }
                foreach (string clipName in action.Clips)
                {
                    if (pack.Clip(clipName) == null)
                    {
                        error = "动作 " + actionKey + " 引用了不存在的片段 " + clipName;
                        return false;
                    }
                }
            }

            string roleRoot = Path.Combine(AppPaths.RolesDir, installedId);
            foreach (KeyValuePair<string, PackClip> pair in pack.Clips)
            {
                PackClip clip = pair.Value;
                if (!IsRelativeInside(clip.Path, roleRoot))
                {
                    error = "片段 " + pair.Key + " 的路径超出宠物包目录";
                    return false;
                }
                string rolePath = installedId + "/" + (string.IsNullOrEmpty(clip.Path) ? clip.Name : clip.Path).Replace('\\', '/');
                RoleAnim anim = RoleAnim.Load(cfg, rolePath);
                if (anim == null)
                {
                    error = "片段 " + pair.Key + " 的图片无法解码";
                    return false;
                }
                FrameSet frames = anim.For("idle");
                if (frames == null || frames.Empty || frames.Frames[0].PixelWidth < 1 || frames.Frames[0].PixelHeight < 1)
                {
                    error = "片段 " + pair.Key + " 没有可用帧";
                    return false;
                }
            }
            return true;
        }

        private static bool IsRelativeInside(string path, string root)
        {
            if (string.IsNullOrEmpty(path) || Path.IsPathRooted(path) || path.IndexOf(':') >= 0) return false;
            string fullRoot = Path.GetFullPath(root).TrimEnd('\\', '/');
            string fullPath = Path.GetFullPath(Path.Combine(fullRoot, path)).TrimEnd('\\', '/');
            return IsSameOrInsideAbsolute(fullPath, fullRoot);
        }

        private static bool IsSameOrInsideAbsolute(string path, string root)
        {
            string fullRoot = Path.GetFullPath(root).TrimEnd('\\', '/');
            string fullPath = Path.GetFullPath(path).TrimEnd('\\', '/');
            if (string.Equals(fullPath, fullRoot, StringComparison.OrdinalIgnoreCase)) return true;
            return fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        private static string Sanitize(string value)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char ch in (value ?? ""))
            {
                if ((ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z')
                    || (ch >= '0' && ch <= '9') || ch == '_' || ch == '-')
                {
                    sb.Append(ch);
                }
                else if (ch == ' ' || ch == '.')
                {
                    sb.Append('_');
                }
            }
            return sb.ToString().Trim('_');
        }

        private static void CopyFolder(string source, string target, bool isRoot)
        {
            if (!isRoot && (File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException("宠物包不能包含符号链接或目录联接：" + source);
            }
            Directory.CreateDirectory(target);
            foreach (string file in Directory.GetFiles(source))
            {
                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new IOException("宠物包不能包含符号链接：" + file);
                }
                File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
            }
            foreach (string dir in Directory.GetDirectories(source))
            {
                CopyFolder(dir, Path.Combine(target, Path.GetFileName(dir)), false);
            }
        }

        private static bool TryDelete(string path)
        {
            string full = "";
            for (int attempt = 0; attempt < 6; attempt++)
            {
                try
                {
                    string roles = Path.GetFullPath(AppPaths.RolesDir).TrimEnd('\\') + "\\";
                    full = Path.GetFullPath(path);
                    if (!full.StartsWith(roles, StringComparison.OrdinalIgnoreCase)) return false;
                    if (!Directory.Exists(full)) return true;
                    Directory.Delete(full, true);
                    if (!Directory.Exists(full)) return true;
                }
                catch
                {
                }
                if (attempt < 5)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    Thread.Sleep(80);
                }
            }
            Log.Write("pack cleanup failed: " + full);
            return false;
        }
    }
}
