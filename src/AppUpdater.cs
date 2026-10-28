// Checks the public release feed, downloads the installer and verifies its SHA-256.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace BalancePet
{
    internal sealed class AvailableUpdate
    {
        public Version Version;
        public string VersionText = "";
        public string Tag = "";
        public string SetupUrl = "";
        public string SetupName = "";
        public string Sha256 = "";
        public long Size;
        public string Notes = "";
    }

    internal static class AppUpdater
    {
        private const string LatestReleaseApi =
            "https://api.github.com/repos/sysoutlk/balance-pet/releases/latest";

        public static AvailableUpdate Check(Config cfg)
        {
            HttpClient client = Http.For(cfg, 20);
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApi);
            request.Headers.TryAddWithoutValidation("User-Agent", "balance-pet-updater/" + Program.Version);
            request.Headers.TryAddWithoutValidation("Accept", "application/vnd.github+json");
            HttpResponseMessage response = client.SendAsync(request).Result;
            string body = response.Content.ReadAsStringAsync().Result;
            if (!response.IsSuccessStatusCode)
            {
                int code = (int)response.StatusCode;
                if (code == 403 || code == 429)
                    throw new InvalidOperationException("更新检查请求过于频繁，请稍后再试");
                throw new InvalidOperationException("检查更新失败：HTTP "
                    + code.ToString(CultureInfo.InvariantCulture));
            }

            Dictionary<string, object> release = Json.ReadText(body);
            string tag = Convert.ToString(Json.Get(release, "tag_name"), CultureInfo.InvariantCulture);
            Version latest = ParseVersion(tag);
            Version current = ParseVersion(Program.Version);
            if (latest == null) throw new InvalidOperationException("Release 版本号无法识别");
            if (current != null && latest <= current) return null;

            AvailableUpdate update = new AvailableUpdate();
            update.Version = latest;
            update.VersionText = VersionText(latest);
            update.Tag = tag;
            update.Notes = Convert.ToString(Json.Get(release, "body"), CultureInfo.InvariantCulture);
            foreach (object item in Json.AsArray(Json.Get(release, "assets")))
            {
                Dictionary<string, object> asset = item as Dictionary<string, object>;
                if (asset == null) continue;
                string name = Convert.ToString(Json.Get(asset, "name"), CultureInfo.InvariantCulture);
                if (!string.Equals(name, "balance-pet-setup.exe", StringComparison.OrdinalIgnoreCase))
                    continue;
                update.SetupName = name;
                update.SetupUrl = Convert.ToString(
                    Json.Get(asset, "browser_download_url"), CultureInfo.InvariantCulture);
                update.Size = (long)Json.ToNumber(Json.Get(asset, "size"), 0);
                string digest = Convert.ToString(
                    Json.Get(asset, "digest"), CultureInfo.InvariantCulture);
                if (digest != null && digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                    update.Sha256 = digest.Substring(7).Trim().ToLowerInvariant();
                break;
            }
            if (string.IsNullOrEmpty(update.SetupUrl))
                throw new InvalidOperationException("最新 Release 缺少安装器资产");
            if (string.IsNullOrEmpty(update.Sha256))
                throw new InvalidOperationException("最新 Release 安装器缺少 SHA-256 摘要");
            return update;
        }

        public static string Download(Config cfg, AvailableUpdate update, Action<int> progress)
        {
            if (update == null) throw new ArgumentNullException("update");
            string folder = Path.Combine(Path.GetTempPath(), "balance-pet-update-" + update.VersionText);
            Directory.CreateDirectory(folder);
            string target = Path.Combine(folder, "balance-pet-setup.exe");
            string temp = target + ".download";
            if (File.Exists(temp)) File.Delete(temp);

            try
            {
                HttpClient client = Http.For(cfg, 60);
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, update.SetupUrl);
                request.Headers.TryAddWithoutValidation(
                    "User-Agent", "balance-pet-updater/" + Program.Version);
                request.Headers.TryAddWithoutValidation("Accept", "application/octet-stream");
                HttpResponseMessage response = client.SendAsync(
                    request, HttpCompletionOption.ResponseHeadersRead).Result;
                if (!response.IsSuccessStatusCode)
                    throw new InvalidOperationException("下载安装器失败：HTTP "
                        + ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture));

                long total = response.Content.Headers.ContentLength.HasValue
                    ? response.Content.Headers.ContentLength.Value : update.Size;
                long received = 0;
                int lastPercent = -1;
                using (Stream input = response.Content.ReadAsStreamAsync().Result)
                using (FileStream output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (SHA256 hash = SHA256.Create())
                {
                    byte[] buffer = new byte[81920];
                    int read;
                    while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        output.Write(buffer, 0, read);
                        hash.TransformBlock(buffer, 0, read, null, 0);
                        received += read;
                        if (total > 0)
                        {
                            int percent = (int)Math.Min(100, received * 100 / total);
                            if (percent != lastPercent)
                            {
                                lastPercent = percent;
                                if (progress != null) progress(percent);
                            }
                        }
                    }
                    hash.TransformFinalBlock(new byte[0], 0, 0);
                    string actual = BitConverter.ToString(hash.Hash).Replace("-", "").ToLowerInvariant();
                    if (!string.Equals(actual, update.Sha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("安装器 SHA-256 校验失败");
                    if (update.Size > 0 && received != update.Size)
                        throw new InvalidOperationException("安装器文件大小校验失败");
                }
                if (File.Exists(target)) File.Delete(target);
                File.Move(temp, target);
                if (progress != null) progress(100);
                return target;
            }
            catch
            {
                try { if (File.Exists(temp)) File.Delete(temp); }
                catch
                {
                }
                throw;
            }
        }

        public static void LaunchInstaller(string setupPath, string installDir)
        {
            if (string.IsNullOrEmpty(setupPath) || !File.Exists(setupPath))
                throw new InvalidOperationException("升级安装器不存在");
            if (string.IsNullOrEmpty(installDir)) throw new InvalidOperationException("安装目录未知");
            string arguments = "--silent --dir=" + Quote(installDir)
                + " --exact-dir --no-shortcuts --wait-pid="
                + Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture)
                + " --relaunch";
            Process.Start(new ProcessStartInfo
            {
                FileName = setupPath,
                Arguments = arguments,
                WorkingDirectory = Path.GetDirectoryName(setupPath),
                UseShellExecute = true
            });
        }

        private static Version ParseVersion(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            string value = text.Trim();
            if (value.StartsWith("v", StringComparison.OrdinalIgnoreCase)) value = value.Substring(1);
            int suffix = value.IndexOf('-');
            if (suffix >= 0) value = value.Substring(0, suffix);
            Version version;
            if (!Version.TryParse(value, out version)) return null;
            int major = Math.Max(0, version.Major);
            int minor = Math.Max(0, version.Minor);
            int build = Math.Max(0, version.Build);
            return new Version(major, minor, build);
        }

        private static string VersionText(Version version)
        {
            return version.Major.ToString(CultureInfo.InvariantCulture) + "."
                + version.Minor.ToString(CultureInfo.InvariantCulture) + "."
                + version.Build.ToString(CultureInfo.InvariantCulture);
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? "").Replace("\"", "\\\"") + "\"";
        }
    }
}
