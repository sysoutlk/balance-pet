// Tiny installer for Balance Pet: lets the user pick an install folder, copies the embedded
// balance-pet.exe there and creates shortcuts. Built by build.ps1 with the in-box csc.
//
//   balance-pet-setup.exe                          interactive installer
//   balance-pet-setup.exe --silent --dir=<parent-or-install-dir>
//       [--no-shortcuts] [--no-desktop-shortcut] [--no-start-menu-shortcut]
using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

[assembly: AssemblyTitle("余额桌宠安装程序")]
[assembly: AssemblyProduct("余额桌宠")]
[assembly: AssemblyDescription("余额桌宠安装程序")]
[assembly: AssemblyVersion("1.2.4.0")]
[assembly: AssemblyFileVersion("1.2.4.0")]
[assembly: AssemblyInformationalVersion("1.2.4")]

internal static class Setup
{
    [STAThread]
    private static int Main(string[] args)
    {
        bool silent = false;
        bool desktopShortcut = true;
        bool startMenuShortcut = true;
        string dir = "";
        foreach (string raw in args)
        {
            string arg = raw == null ? "" : raw.Trim();
            if (arg == "--silent" || arg == "/S") silent = true;
            else if (arg == "--no-shortcuts")
            {
                desktopShortcut = false;
                startMenuShortcut = false;
            }
            else if (arg == "--no-desktop-shortcut") desktopShortcut = false;
            else if (arg == "--no-start-menu-shortcut") startMenuShortcut = false;
            else if (arg.StartsWith("--dir=", StringComparison.OrdinalIgnoreCase)) dir = arg.Substring(6).Trim('"');
        }
        if (string.IsNullOrEmpty(dir))
        {
            dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Path.Combine("Programs", "BalancePet"));
        }
        if (silent)
        {
            try
            {
                Install(dir, desktopShortcut, startMenuShortcut);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("install failed: " + ex.Message);
                return 1;
            }
        }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        using (Form form = BuildForm(dir))
        {
            Application.Run(form);
        }
        return 0;
    }

    private static Form BuildForm(string initialDir)
    {
        string version = ProductVersion();
        Form form = new Form();
        form.Text = "余额桌宠 " + version + " 安装程序";
        form.FormBorderStyle = FormBorderStyle.FixedDialog;
        form.MaximizeBox = false;
        form.MinimizeBox = false;
        form.StartPosition = FormStartPosition.CenterScreen;
        form.ClientSize = new Size(590, 310);
        form.AutoScaleMode = AutoScaleMode.Dpi;
        form.Font = new Font("Microsoft YaHei UI", 9f);

        Label title = new Label();
        title.Text = "安装余额桌宠";
        title.Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold);
        title.AutoSize = true;
        title.Location = new Point(22, 18);
        form.Controls.Add(title);

        Label versionLabel = new Label();
        versionLabel.Text = "版本 " + version;
        versionLabel.ForeColor = Color.DimGray;
        versionLabel.AutoSize = true;
        versionLabel.Location = new Point(482, 21);
        form.Controls.Add(versionLabel);

        Label subtitle = new Label();
        subtitle.Text = "选择上级目录，安装器会自动创建 BalancePet 文件夹。";
        subtitle.ForeColor = Color.DimGray;
        subtitle.AutoSize = true;
        subtitle.Location = new Point(22, 46);
        form.Controls.Add(subtitle);

        Label pathLabel = new Label();
        pathLabel.Text = "安装到";
        pathLabel.AutoSize = true;
        pathLabel.Location = new Point(22, 76);
        form.Controls.Add(pathLabel);

        TextBox path = new TextBox();
        path.Text = initialDir;
        path.Location = new Point(22, 99);
        path.Width = 444;
        form.Controls.Add(path);

        Button browse = new Button();
        browse.Text = "浏览…";
        browse.Location = new Point(474, 97);
        browse.Width = 92;
        browse.Height = 26;
        browse.Click += delegate
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "选择 BalancePet 的上级目录，安装器会自动创建 BalancePet 子目录";
                string current = "";
                try { current = Path.GetDirectoryName(NormalizeInstallDir(path.Text)); }
                catch { }
                if (!string.IsNullOrEmpty(current) && Directory.Exists(current))
                    dialog.SelectedPath = current;
                if (dialog.ShowDialog(form) == DialogResult.OK)
                    path.Text = NormalizeInstallDir(dialog.SelectedPath);
            }
        };
        form.Controls.Add(browse);

        GroupBox shortcutGroup = new GroupBox();
        shortcutGroup.Text = "快捷方式";
        shortcutGroup.Location = new Point(22, 139);
        shortcutGroup.Size = new Size(544, 58);
        form.Controls.Add(shortcutGroup);

        CheckBox desktopShortcut = new CheckBox();
        desktopShortcut.Text = "创建桌面快捷方式";
        desktopShortcut.Checked = true;
        desktopShortcut.AutoSize = true;
        desktopShortcut.Location = new Point(14, 22);
        shortcutGroup.Controls.Add(desktopShortcut);

        CheckBox startMenuShortcut = new CheckBox();
        startMenuShortcut.Text = "创建开始菜单快捷方式";
        startMenuShortcut.Checked = true;
        startMenuShortcut.AutoSize = true;
        startMenuShortcut.Location = new Point(210, 22);
        shortcutGroup.Controls.Add(startMenuShortcut);

        Label hint = new Label();
        hint.Text = "程序文件安装到上方目录。\r\n配置与角色素材保存在 %APPDATA%\\BalancePet。";
        hint.ForeColor = Color.DimGray;
        hint.AutoSize = false;
        hint.Location = new Point(22, 225);
        hint.Size = new Size(544, 34);
        form.Controls.Add(hint);

        Button install = new Button();
        install.Text = "安装";
        install.Location = new Point(440, 266);
        install.Width = 130;
        install.Height = 30;
        install.Click += delegate
        {
            try
            {
                string installed = Install(path.Text, desktopShortcut.Checked,
                    startMenuShortcut.Checked);
                MessageBox.Show(form, "安装完成：\n" + installed, "余额桌宠",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                form.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(form, "安装失败：" + ex.Message, "余额桌宠", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
        form.Controls.Add(install);
        form.AcceptButton = install;
        return form;
    }

    private static string ProductVersion()
    {
        object[] values = Assembly.GetExecutingAssembly().GetCustomAttributes(
            typeof(AssemblyInformationalVersionAttribute), false);
        if (values.Length > 0)
        {
            string value = ((AssemblyInformationalVersionAttribute)values[0]).InformationalVersion;
            if (!string.IsNullOrEmpty(value)) return value;
        }
        Version version = Assembly.GetExecutingAssembly().GetName().Version;
        return version == null ? "" : version.ToString(3);
    }

    private static string Install(string dir, bool desktopShortcut, bool startMenuShortcut)
    {
        dir = NormalizeInstallDir(dir);
        Directory.CreateDirectory(dir);
        string exePath = Path.Combine(dir, "balance-pet.exe");
        using (Stream source = Assembly.GetExecutingAssembly().GetManifestResourceStream("balance-pet.exe"))
        {
            if (source == null) throw new InvalidOperationException("安装包不完整：缺少 balance-pet.exe，请重新下载。");
            using (FileStream target = File.Create(exePath))
            {
                source.CopyTo(target);
            }
        }
        if (desktopShortcut)
            CreateShortcut(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), exePath);
        if (startMenuShortcut)
        {
            string programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
            if (!string.IsNullOrEmpty(programs))
            {
                string folder = Path.Combine(programs, "余额桌宠");
                Directory.CreateDirectory(folder);
                CreateShortcut(folder, exePath);
            }
        }
        return dir;
    }

    private static string NormalizeInstallDir(string selected)
    {
        if (string.IsNullOrEmpty(selected)) throw new InvalidOperationException("安装路径不能为空。");
        string full = Path.GetFullPath(selected);
        string leaf = Path.GetFileName(full.TrimEnd('\\', '/'));
        if (string.Equals(leaf, "BalancePet", StringComparison.OrdinalIgnoreCase))
            return full;
        return Path.Combine(full, "BalancePet");
    }

    private static void CreateShortcut(string folder, string exePath)
    {
        if (string.IsNullOrEmpty(folder)) return;
        Directory.CreateDirectory(folder);
        string linkPath = Path.Combine(folder, "余额桌宠.lnk");
        Type shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType == null) return;
        object shell = Activator.CreateInstance(shellType);
        object link = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { linkPath });
        Type linkType = link.GetType();
        linkType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, link, new object[] { exePath });
        linkType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, link, new object[] { Path.GetDirectoryName(exePath) });
        linkType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, link, new object[] { exePath + ",0" });
        linkType.InvokeMember("Description", BindingFlags.SetProperty, null, link, new object[] { "多平台余额桌面宠物" });
        linkType.InvokeMember("Save", BindingFlags.InvokeMethod, null, link, null);
    }
}
