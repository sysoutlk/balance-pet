// Entry point and command-line modes.
//
//   balance-pet.exe                     start the pet
//   balance-pet.exe --settings          start and open the settings card
//   balance-pet.exe --demo              demo mode (constant bubble + auto actions)
//   balance-pet.exe --smoke [seconds]   start, run for N seconds, write smoke-report.txt
//   balance-pet.exe --self-test         headless data check, writes self-test-report.txt
//   balance-pet.exe --make-icon <file>  export the built-in mascot as a .ico
//   balance-pet.exe --make-anim <file>  turn a static picture into a frame-by-frame role
//   balance-pet.exe --import-pack <dir> [--activate] install a pet pack
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace BalancePet
{
    internal static class Program
    {
        public const string Version = "1.2.2";
        public static readonly Random Rng = new Random();
        public static Config CurrentConfig;
        public static int DemoPageIndex;

        private const string HelpText =
            "balance-pet " + Version + "\n" +
            "用法:\n" +
            "  balance-pet.exe                     启动桌宠\n" +
            "  balance-pet.exe --settings          启动并打开设置面板\n" +
            "  balance-pet.exe --demo              演示模式（常显气泡 + 自动动作）\n" +
            "  balance-pet.exe --smoke [秒数]      启动后自动退出并写 smoke-report.txt\n" +
            "  balance-pet.exe --self-test         不弹窗口，检查数据源并写 self-test-report.txt\n" +
            "  balance-pet.exe --make-icon <路径>  导出内置小鲸鱼图标 (.ico)\n" +
            "  balance-pet.exe --make-anim <图片>  把一张静态图生成逐帧动画角色（--name 角色名 --width 像素）\n" +
            "  balance-pet.exe --import-pack <目录> [--activate]\n" +
            "                                        导入宠物包并输出角色 id；加 --activate 立即切换\n" +
            "  balance-pet.exe --version           显示版本\n";

        [STAThread]
        public static int Main(string[] args)
        {
            try { Console.OutputEncoding = Encoding.UTF8; } catch { }
            Native.AttachConsole(Native.ATTACH_PARENT_PROCESS);
            int demoPage;
            if (int.TryParse(Environment.GetEnvironmentVariable("BALANCE_PET_DEMO_PAGE"),
                    NumberStyles.Integer, CultureInfo.InvariantCulture, out demoPage))
            {
                DemoPageIndex = Math.Max(0, demoPage);
            }

            List<string> flags = new List<string>(args);
            if (Has(flags, "--help") || Has(flags, "-h") || Has(flags, "/?"))
            {
                Console.WriteLine(HelpText);
                return 0;
            }
            if (Has(flags, "--version"))
            {
                Console.WriteLine("balance-pet " + Version);
                return 0;
            }
            string iconPath = ValueOf(flags, "--make-icon");
            if (!string.IsNullOrEmpty(iconPath))
            {
                return IconWriter.Write(iconPath);
            }
            string animSource = ValueOf(flags, "--make-anim");
            if (!string.IsNullOrEmpty(animSource))
            {
                return MakeAnim(flags, animSource);
            }

            AppPaths.MigratePortableData();
            Config cfg = Config.Load(AppPaths.ConfigPath);
            CurrentConfig = cfg;
            string importPack = ValueOf(flags, "--import-pack");
            if (!string.IsNullOrEmpty(importPack))
            {
                string note;
                string error;
                string role = PetPackInstaller.Install(cfg, importPack, out note, out error);
                if (string.IsNullOrEmpty(role))
                {
                    Console.WriteLine("导入失败：" + error);
                    return 1;
                }
                Console.WriteLine("已导入宠物包：" + role);
                Console.WriteLine("路径：" + Path.Combine(AppPaths.RolesDir, role));
                if (!string.IsNullOrEmpty(note)) Console.WriteLine("素材转换：" + note);
                if (Has(flags, "--activate"))
                {
                    cfg.Set("", "role", role);
                    cfg.SetAt("roles.random", false);
                    cfg.Save();
                    Console.WriteLine("已设为当前宠物：" + role);
                }
                return 0;
            }
            // 同目录放一个 quips.json 就能自带一套台词：启动时合并进来（随时可删，删了就没了）。
            string quipPack = System.IO.Path.Combine(AppPaths.DataRoot, "quips.json");
            if (System.IO.File.Exists(quipPack) && cfg.ImportQuips(quipPack) > 0)
            {
                cfg.Save();
            }
            if (Has(flags, "--self-test")) return SelfTest(cfg);

            bool createdNew;
            Mutex instanceMutex = new Mutex(true, "BalancePet.SingleInstance", out createdNew);
            if (!createdNew)
            {
                try
                {
                    EventWaitHandle signal = EventWaitHandle.OpenExisting("BalancePet.ShowSignal");
                    signal.Set();
                }
                catch
                {
                }
                return 0;
            }

            Application app = new Application();
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            app.DispatcherUnhandledException += delegate(object sender, DispatcherUnhandledExceptionEventArgs e)
            {
                Log.Write("UI unhandled: " + e.Exception);
            };
            Engine engine = new Engine(cfg, Has(flags, "--demo"));
            engine.Start();
            if (Has(flags, "--settings")) engine.ShowPanel();

            EventWaitHandle showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, "BalancePet.ShowSignal");
            ThreadPool.RegisterWaitForSingleObject(showSignal, delegate
            {
                app.Dispatcher.BeginInvoke(new Action(delegate { engine.ShowBubble(null, ""); }));
            }, null, -1, false);

            if (Has(flags, "--smoke"))
            {
                double smokeSeconds = ValueOfDouble(flags, "--smoke", 4.0);
                DispatcherTimer quit = new DispatcherTimer();
                quit.Interval = TimeSpan.FromSeconds(Math.Max(1.0, smokeSeconds));
                quit.Tick += delegate(object sender, EventArgs e)
                {
                    quit.Stop();
                    string report = "smoke-ok " + engine.Report();
                    WriteReport("smoke-report.txt", report);
                    Console.WriteLine(report);
                    engine.Quit();
                };
                quit.Start();
            }

            app.Run();
            try { instanceMutex.ReleaseMutex(); }
            catch { }
            return 0;
        }

        private static int SelfTest(Config cfg)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("balance-pet " + Version + " self-test");
            sb.AppendLine("config: " + cfg.FilePath);
            Snapshot s;
            try
            {
                DataService data = new DataService(cfg);
                s = data.BuildOnce();
            }
            catch (Exception ex)
            {
                sb.AppendLine("FAILED: " + ex.Message);
                WriteReport("self-test-report.txt", sb.ToString());
                Console.WriteLine(sb.ToString());
                return 1;
            }
            ProviderState primary = s.Primary;
            sb.AppendLine("ok=" + (s.Ok ? "True" : "False")
                + "  source=" + (primary == null ? "none" : primary.Name + "（" + primary.SourceText + "）")
                + "  balance=" + (primary == null ? "--" : Fmt.Money(primary.Balance, primary.Currency))
                + "  today=" + (primary == null || !primary.TodayUsage.HasValue ? "--" : Fmt.Money(primary.TodayUsage, primary.Currency)));
            if (s.HasPeak)
            {
                sb.AppendLine("peak=" + (s.IsPeak ? "标准时段（原价）" : "优惠时段（半价）") + "  next=" + FormatStamp(s.NextChangeAtMs));
            }
            sb.AppendLine("providers:");
            foreach (ProviderState p in s.Providers)
            {
                string line;
                if (!p.Enabled) line = "关闭";
                else if (!p.Supported) line = "无公开余额接口";
                else if (p.Ok) line = Fmt.Money(p.Balance, p.Currency)
                    + (p.Details.Count > 0 ? "（" + string.Join(" · ", p.Details.ToArray()) + "）" : "")
                    + (string.IsNullOrEmpty(p.KeySource) ? "" : "  key:" + p.KeySource);
                else line = "失败：" + p.Error;
                sb.AppendLine("  - " + p.Id + "  " + p.Name + "  " + line);
            }
            if (s.History.Count > 0) sb.AppendLine("trend: " + TrendLine(s.History));
            if (s.Plans.Count > 0)
            {
                sb.AppendLine("plans:");
                foreach (PlanState plan in s.Plans)
                {
                    if (plan.Ok)
                    {
                        sb.AppendLine("  - " + plan.Id + "  " + plan.Name + "  " + plan.Summary()
                            + (string.IsNullOrEmpty(plan.KeySource) ? "" : "  key:" + plan.KeySource));
                    }
                    else
                    {
                        sb.AppendLine("  - " + plan.Id + "  " + plan.Name + "  失败：" + plan.Error);
                    }
                }
            }
            string report = sb.ToString();
            Console.WriteLine(report);
            WriteReport("self-test-report.txt", report);
            return 0;
        }

        private static string FormatStamp(long timeMs)
        {
            if (timeMs <= 0) return "--";
            return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(timeMs).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        }

        private static string TrendLine(List<DayUsage> history)
        {
            StringBuilder sb = new StringBuilder();
            foreach (DayUsage item in history)
            {
                if (sb.Length > 0) sb.Append("  ");
                string day = item.Day.Length >= 10 ? item.Day.Substring(5) : item.Day;
                sb.Append(day + ":" + item.Usage.ToString("0.##"));
            }
            return sb.ToString();
        }

        private static void WriteReport(string name, string text)
        {
            try
            {
                File.WriteAllText(Path.Combine(AppPaths.DataRoot, name), text, new UTF8Encoding(false));
            }
            catch
            {
            }
        }

        private static bool Has(List<string> flags, string flag)
        {
            foreach (string item in flags)
            {
                if (string.Equals(item, flag, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static string ValueOf(List<string> flags, string flag)
        {
            for (int i = 0; i < flags.Count - 1; i++)
            {
                if (string.Equals(flags[i], flag, StringComparison.OrdinalIgnoreCase)) return flags[i + 1];
            }
            return "";
        }

        private static double ValueOfDouble(List<string> flags, string flag, double fallback)
        {
            string text = ValueOf(flags, flag);
            double value;
            if (!string.IsNullOrEmpty(text) && double.TryParse(text, out value) && value > 0) return value;
            return fallback;
        }

        /// <summary>把一张静态图生成"逐帧动画角色"：--make-anim 图片 [--name 角色名] [--width 像素]</summary>
        private static int MakeAnim(List<string> flags, string source)
        {
            string name = ValueOf(flags, "--name");
            if (string.IsNullOrEmpty(name)) name = Path.GetFileNameWithoutExtension(source) + "-动图";
            double width = ValueOfDouble(flags, "--width", 256);
            int written = AnimMaker.Build(source, name, width, delegate(string line)
            {
                if (line.StartsWith("error:", StringComparison.Ordinal)) Console.WriteLine(line);
            });
            if (written < 0)
            {
                Console.WriteLine("生成失败：读不出图片 " + source);
                return 1;
            }
            Console.WriteLine("已生成 " + written + " 帧：" + Path.Combine(AppPaths.RolesDir, name));
            return 0;
        }
    }

    internal static class IconWriter
    {
        public static int Write(string outPath)
        {
            try
            {
                int[] sizes = new int[] { 16, 24, 32, 48, 64, 128, 256 };
                List<byte[]> pngs = new List<byte[]>();
                foreach (int size in sizes)
                {
                    pngs.Add(RenderPng(size));
                }
                string directory = Path.GetDirectoryName(outPath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                using (FileStream stream = File.Create(outPath))
                {
                    BinaryWriter writer = new BinaryWriter(stream);
                    writer.Write((short)0);
                    writer.Write((short)1);
                    writer.Write((short)pngs.Count);
                    int offset = 6 + 16 * pngs.Count;
                    for (int i = 0; i < pngs.Count; i++)
                    {
                        int size = sizes[i];
                        writer.Write((byte)(size >= 256 ? 0 : size));
                        writer.Write((byte)(size >= 256 ? 0 : size));
                        writer.Write((byte)0);
                        writer.Write((byte)0);
                        writer.Write((short)1);
                        writer.Write((short)32);
                        writer.Write(pngs[i].Length);
                        writer.Write(offset);
                        offset += pngs[i].Length;
                    }
                    foreach (byte[] png in pngs) writer.Write(png);
                }
                Console.WriteLine("icon written: " + outPath);
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("make-icon failed: " + ex.Message);
                return 1;
            }
        }

        private static byte[] RenderPng(int size)
        {
            System.Windows.Controls.Grid root = new System.Windows.Controls.Grid();
            root.Width = size;
            root.Height = size;
            FrameworkElement mascot = Mascot.CreateVisual();
            mascot.Width = size * 0.94;
            mascot.Height = Math.Max(1, Math.Round(size * 0.94 * Mascot.BaseHeight / Mascot.BaseWidth));
            mascot.HorizontalAlignment = HorizontalAlignment.Center;
            mascot.VerticalAlignment = VerticalAlignment.Center;
            root.Children.Add(mascot);
            root.Measure(new Size(size, size));
            root.Arrange(new Rect(0, 0, size, size));
            root.UpdateLayout();
            System.Windows.Media.Imaging.RenderTargetBitmap bitmap =
                new System.Windows.Media.Imaging.RenderTargetBitmap(size, size, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            bitmap.Render(root);
            System.Windows.Media.Imaging.PngBitmapEncoder encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            MemoryStream stream = new MemoryStream();
            encoder.Save(stream);
            return stream.ToArray();
        }
    }
}
