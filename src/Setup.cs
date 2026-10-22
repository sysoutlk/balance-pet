// Tiny installer for Balance Pet: lets the user pick an install folder, copies the embedded
// balance-pet.exe there and creates shortcuts. Built by build.ps1 with the in-box csc.
//
//   balance-pet-setup.exe                          interactive installer
//   balance-pet-setup.exe --silent --dir=<path> [--no-shortcuts]
using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

[assembly: AssemblyTitle("余额桌宠安装程序")]
[assembly: AssemblyProduct("余额桌宠")]
[assembly: AssemblyDescription("余额桌宠安装程序")]
[assembly: AssemblyVersion("1.2.0.0")]
[assembly: AssemblyFileVersion("1.2.0.0")]
[assembly: AssemblyInformationalVersion("1.2.0")]

internal static class Setup
{
    [STAThread]
    private static int Main(string[] args)
    {
        bool silent = false;
        bool shortcuts = true;
        string dir = "";
        foreach (string raw in args)
        {
            string arg = raw == null ? "" : raw.Trim();
            if (arg == "--silent" || arg == "/S") silent = true;
            else if (arg == "--no-shortcuts") shortcuts = false;
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
                Install(dir, shortcuts);
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
        Form form = new Form();
        form.Text = "余额桌宠 安装程序";
        form.FormBorderStyle = FormBorderStyle.FixedDialog;
        form.MaximizeBox = false;
        form.MinimizeBox = false;
        form.StartPosition = FormStartPosition.CenterScreen;
        form.ClientSize = new Size(470, 190);
        form.Font = new Font("Microsoft YaHei UI", 9f);

        Label title = new Label();
        title.Text = "选择安装位置";
        title.Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold);
        title.AutoSize = true;
        title.Location = new Point(20, 18);
        form.Controls.Add(title);

        TextBox path = new TextBox();
        path.Text = initialDir;
        path.Location = new Point(20, 56);
        path.Width = 336;
        form.Controls.Add(path);

        Button browse = new Button();
        browse.Text = "浏览…";
        browse.Location = new Point(364, 54);
        browse.Width = 86;
        browse.Height = 26;
        browse.Click += delegate
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "选择安装目录";
                dialog.SelectedPath = path.Text;
                if (dialog.ShowDialog(form) == DialogResult.OK) path.Text = dialog.SelectedPath;
            }
        };
        form.Controls.Add(browse);

        CheckBox shortcut = new CheckBox();
        shortcut.Text = "创建桌面和开始菜单快捷方式";
        shortcut.Checked = true;
        shortcut.AutoSize = true;
        shortcut.Location = new Point(20, 92);
        form.Controls.Add(shortcut);

        Label hint = new Label();
        hint.Text = "程序文件只有 balance-pet.exe；配置、角色素材等数据保存在 %APPDATA%\\BalancePet。";
        hint.ForeColor = Color.DimGray;
        hint.AutoSize = true;
        hint.Location = new Point(20, 120);
        form.Controls.Add(hint);

        Button install = new Button();
        install.Text = "安装";
        install.Location = new Point(320, 150);
        install.Width = 130;
        install.Height = 30;
        install.Click += delegate
        {
            try
            {
                Install(path.Text, shortcut.Checked);
                MessageBox.Show(form, "安装完成：\n" + path.Text, "余额桌宠", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

    private static void Install(string dir, bool shortcuts)
    {
        if (string.IsNullOrEmpty(dir)) throw new InvalidOperationException("安装路径不能为空。");
        dir = Path.GetFullPath(dir);
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
        if (!shortcuts) return;
        CreateShortcut(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), exePath);
        string programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        if (!string.IsNullOrEmpty(programs))
        {
            string folder = Path.Combine(programs, "余额桌宠");
            Directory.CreateDirectory(folder);
            CreateShortcut(folder, exePath);
        }
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
