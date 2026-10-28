using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;

namespace USBCANToolSetup
{
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool uninstall = false;
            bool quiet = false;
            string dir = Setup.DefaultDir;
            bool desktop = true;

            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (string.Equals(a, "/uninstall", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(a, "/u", StringComparison.OrdinalIgnoreCase))
                    uninstall = true;
                else if (string.Equals(a, "/S", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(a, "/quiet", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(a, "/q", StringComparison.OrdinalIgnoreCase))
                    quiet = true;
                else if (string.Equals(a, "/DIR", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    dir = args[++i];
                else if (string.Equals(a, "/nodesktop", StringComparison.OrdinalIgnoreCase))
                    desktop = false;
            }

            try
            {
                if (uninstall)
                {
                    Setup.Uninstall(quiet);
                    return 0;
                }
                if (quiet)
                {
                    Setup.Install(dir, desktop, true);
                    return 0;
                }
                Application.Run(new SetupForm());
                return 0;
            }
            catch (Exception ex)
            {
                if (!quiet)
                    MessageBox.Show(ex.Message, "USBCAN Tool Setup", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }
    }

    static class Setup
    {
        public const string AppName = "USBCAN Tool";
        public const string ExeName = "USBCANTool.exe";
        public const string SetupName = "USBCANToolSetup.exe";
        public const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\USBCANTool";

        public static string DefaultDir
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Programs", "USBCANTool");
            }
        }

        public static string StartMenuDir
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                    AppName);
            }
        }

        public static string DesktopLink
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                    AppName + ".lnk");
            }
        }

        public static void Install(string destDir, bool desktopShortcut, bool quiet)
        {
            if (string.IsNullOrWhiteSpace(destDir))
                destDir = DefaultDir;
            destDir = Path.GetFullPath(destDir);

            Directory.CreateDirectory(destDir);
            string extract = Path.Combine(Path.GetTempPath(), "USBCANToolSetup_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(extract);
            try
            {
                ExtractPayload(extract);
                CopyTree(extract, destDir);
            }
            finally
            {
                try { Directory.Delete(extract, true); } catch { }
            }

            string exe = Path.Combine(destDir, ExeName);
            if (!File.Exists(exe))
                throw new InvalidOperationException("Install payload is missing " + ExeName + ".");
            if (!File.Exists(Path.Combine(destDir, "zlgcan.dll")))
                throw new InvalidOperationException("Install payload is missing zlgcan.dll.");

            string thisSetup = Application.ExecutablePath;
            string destSetup = Path.Combine(destDir, SetupName);
            if (!SamePath(thisSetup, destSetup))
                File.Copy(thisSetup, destSetup, true);

            Directory.CreateDirectory(StartMenuDir);
            CreateShortcut(Path.Combine(StartMenuDir, AppName + ".lnk"), exe, destDir, "USBCAN RX/TX bridge");
            CreateShortcut(Path.Combine(StartMenuDir, "Uninstall " + AppName + ".lnk"), destSetup, destDir, "Remove USBCAN Tool", "/uninstall");
            if (desktopShortcut)
                CreateShortcut(DesktopLink, exe, destDir, "USBCAN RX/TX bridge");

            WriteUninstallKey(destDir, destSetup);

            if (!quiet)
            {
                var r = MessageBox.Show(
                    "Installed to:\n" + destDir + "\n\nOpen USBCAN Tool now?",
                    AppName + " Setup",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);
                if (r == DialogResult.Yes)
                    Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = destDir, UseShellExecute = true });
            }
        }

        public static void Uninstall(bool quiet)
        {
            string destDir = ReadInstallLocation() ?? DefaultDir;
            string destSetup = Path.Combine(destDir, SetupName);
            if (File.Exists(destSetup) && SamePath(Application.ExecutablePath, destSetup))
            {
                string tmp = Path.Combine(Path.GetTempPath(), SetupName);
                File.Copy(destSetup, tmp, true);
                var p = Process.Start(new ProcessStartInfo(tmp, quiet ? "/uninstall /S" : "/uninstall")
                {
                    UseShellExecute = false
                });
                if (p != null)
                    p.WaitForExit(60000);
                return;
            }

            if (!quiet)
            {
                var r = MessageBox.Show(
                    "Remove " + AppName + " from this user account?\n\nSettings in AppData are kept.",
                    AppName + " Setup",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (r != DialogResult.Yes)
                    return;
            }

            try { File.Delete(DesktopLink); } catch { }
            try
            {
                if (Directory.Exists(StartMenuDir))
                    Directory.Delete(StartMenuDir, true);
            }
            catch { }

            try
            {
                if (Directory.Exists(destDir))
                    Directory.Delete(destDir, true);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Could not delete:\n" + destDir + "\nClose the app and try again.\n" + ex.Message);
            }

            try
            {
                using (RegistryKey hkcu = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall", true))
                {
                    if (hkcu != null)
                        hkcu.DeleteSubKey("USBCANTool", false);
                }
            }
            catch { }

            if (!quiet)
                MessageBox.Show(AppName + " was removed.", AppName + " Setup", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        static void ExtractPayload(string dest)
        {
            Stream s = OpenPayload();
            string zip = Path.Combine(dest, "payload.zip");
            using (s)
            using (FileStream f = File.Create(zip))
                s.CopyTo(f);
            ZipFile.ExtractToDirectory(zip, dest);
            File.Delete(zip);
        }

        static Stream OpenPayload()
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            string[] names = asm.GetManifestResourceNames();
            for (int i = 0; i < names.Length; i++)
            {
                string n = names[i];
                if (string.Equals(n, "payload.zip", StringComparison.OrdinalIgnoreCase) ||
                    n.EndsWith(".payload.zip", StringComparison.OrdinalIgnoreCase) ||
                    n.EndsWith("payload.zip", StringComparison.OrdinalIgnoreCase))
                {
                    Stream embedded = asm.GetManifestResourceStream(n);
                    if (embedded != null)
                        return embedded;
                }
            }

            string dir = Path.GetDirectoryName(asm.Location) ?? ".";
            string sidecar = Path.Combine(dir, "payload.zip");
            if (File.Exists(sidecar))
                return File.OpenRead(sidecar);

            string listed = names.Length == 0 ? "(none)" : string.Join(", ", names);
            throw new InvalidOperationException(
                "This setup file has no payload.zip.\n" +
                "Embedded resources: " + listed + "\n\n" +
                "On the build PC, rebuild USBCANToolSetup in Visual Studio (x64),\n" +
                "or double-click installer\\Pack.bat.\n" +
                "Then copy the new dist\\USBCANToolSetup.exe.");
        }

        static void CopyTree(string src, string dest)
        {
            foreach (string dir in Directory.GetDirectories(src, "*", SearchOption.AllDirectories))
            {
                string rel = dir.Substring(src.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                Directory.CreateDirectory(Path.Combine(dest, rel));
            }
            foreach (string file in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
            {
                string rel = file.Substring(src.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string target = Path.Combine(dest, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(file, target, true);
            }
        }

        static void WriteUninstallKey(string destDir, string destSetup)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(UninstallKey))
            {
                if (key == null)
                    return;
                key.SetValue("DisplayName", AppName);
                key.SetValue("Publisher", "USBCANTool");
                key.SetValue("DisplayVersion", "1.0.0");
                key.SetValue("InstallLocation", destDir);
                key.SetValue("UninstallString", "\"" + destSetup + "\" /uninstall");
                key.SetValue("QuietUninstallString", "\"" + destSetup + "\" /uninstall /S");
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                string exe = Path.Combine(destDir, ExeName);
                if (File.Exists(exe))
                    key.SetValue("DisplayIcon", exe);
                long size = 0;
                try
                {
                    foreach (string f in Directory.GetFiles(destDir, "*", SearchOption.AllDirectories))
                        size += new FileInfo(f).Length;
                    key.SetValue("EstimatedSize", (int)(size / 1024), RegistryValueKind.DWord);
                }
                catch { }
            }
        }

        static string ReadInstallLocation()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(UninstallKey, false))
                {
                    if (key == null)
                        return null;
                    return key.GetValue("InstallLocation") as string;
                }
            }
            catch
            {
                return null;
            }
        }

        static void CreateShortcut(string linkPath, string target, string workDir, string description, string arguments = null)
        {
            Type t = Type.GetTypeFromProgID("WScript.Shell");
            if (t == null)
                return;
            object shell = Activator.CreateInstance(t);
            object sc = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { linkPath });
            Type st = sc.GetType();
            st.InvokeMember("TargetPath", BindingFlags.SetProperty, null, sc, new object[] { target });
            st.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, sc, new object[] { workDir });
            st.InvokeMember("Description", BindingFlags.SetProperty, null, sc, new object[] { description });
            st.InvokeMember("WindowStyle", BindingFlags.SetProperty, null, sc, new object[] { 1 });
            if (!string.IsNullOrEmpty(arguments))
                st.InvokeMember("Arguments", BindingFlags.SetProperty, null, sc, new object[] { arguments });
            st.InvokeMember("Save", BindingFlags.InvokeMethod, null, sc, null);
        }

        static bool SamePath(string a, string b)
        {
            try
            {
                return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }

    sealed class SetupForm : Form
    {
        readonly TextBox _dir;
        readonly CheckBox _desktop;
        readonly Label _note;

        public SetupForm()
        {
            Text = Setup.AppName + " Setup";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(520, 250);
            Font = new Font("Segoe UI", 9F);

            Controls.Add(new Label
            {
                Text = "Installs for this Windows user only (no administrator account).",
                Location = new Point(16, 16),
                AutoSize = true
            });
            Controls.Add(new Label
            {
                Text = "Folder:",
                Location = new Point(16, 48),
                AutoSize = true
            });

            _dir = new TextBox
            {
                Location = new Point(16, 68),
                Size = new Size(390, 23),
                Text = Setup.DefaultDir
            };
            Controls.Add(_dir);

            var browse = new Button { Text = "Browse…", Location = new Point(414, 66), Size = new Size(90, 25) };
            browse.Click += delegate
            {
                using (var d = new FolderBrowserDialog())
                {
                    d.SelectedPath = Directory.Exists(_dir.Text) ? _dir.Text : Setup.DefaultDir;
                    if (d.ShowDialog(this) == DialogResult.OK)
                        _dir.Text = d.SelectedPath;
                }
            };
            Controls.Add(browse);

            _desktop = new CheckBox
            {
                Text = "Desktop shortcut",
                Checked = true,
                Location = new Point(16, 104),
                AutoSize = true
            };
            Controls.Add(_desktop);

            _note = new Label
            {
                Location = new Point(16, 132),
                Size = new Size(488, 52),
                Text = "Copies USBCANTool.exe, zlgcan.dll, and the kerneldlls folder.\n" +
                       "The ZLG USB driver itself is separate and may still need a one-time vendor install."
            };
            Controls.Add(_note);

            var install = new Button { Text = "Install", Location = new Point(230, 204), Size = new Size(90, 28) };
            install.Click += delegate
            {
                try
                {
                    install.Enabled = false;
                    Setup.Install(_dir.Text, _desktop.Checked, false);
                    Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    install.Enabled = true;
                }
            };
            Controls.Add(install);

            var uninstall = new Button { Text = "Uninstall", Location = new Point(326, 204), Size = new Size(90, 28) };
            uninstall.Click += delegate
            {
                try
                {
                    Setup.Uninstall(false);
                    Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            Controls.Add(uninstall);

            var close = new Button { Text = "Close", Location = new Point(422, 204), Size = new Size(82, 28) };
            close.Click += delegate { Close(); };
            Controls.Add(close);
            AcceptButton = install;
            CancelButton = close;
        }
    }
}
