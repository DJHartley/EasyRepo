﻿using ICSharpCode.SharpZipLib.BZip2;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Windows.Forms;

namespace EasyRepo
{
    public partial class form_manager : Form
    {
        public string repoPath;
        private string exePath = Path.GetDirectoryName(Application.ExecutablePath);
        private string cfgPath = Path.GetDirectoryName(Application.ExecutablePath) + @"\config.cfg";
        private List<List<string>> Packages;

        public form_manager(string path, bool overwrite = false)
        {
            InitializeComponent();
            repoPath = path;
            if (!Directory.Exists(repoPath + @"\debs") || !File.Exists(repoPath + @"\Release"))
            {
                File.Delete(cfgPath);
                Application.Restart();
            }
            else
            {
                if (overwrite)
                {
                    File.Delete(cfgPath);
                }
                if (!File.Exists(cfgPath))
                {
                    File.Create(cfgPath).Close();
                    using (StreamWriter stream = new StreamWriter(cfgPath))
                    {
                        stream.WriteLine($"REPOPATH={repoPath}");
                    }
                }
            }
        }

        private void button_buildPackages_Click(object sender, EventArgs e)
        {
            Packages = GetPackages();
            if (!Directory.Exists(repoPath + @"\tmp"))
            {
                Directory.CreateDirectory(repoPath + @"\tmp");
            }
            else
            {
                Directory.GetFiles(repoPath + @"\tmp").ToList().ForEach(x => File.Delete(x));
            }
            using (StreamWriter writer = new StreamWriter(File.Create(repoPath + @"\tmp\Packages")))
            {
                foreach (List<string> package in Packages)
                {
                    foreach (string line in package)
                    {
                        writer.WriteLine(line);
                    }
                    writer.WriteLine("");
                }
            }
            using (FileStream packagesFile = new FileInfo(repoPath + @"\tmp\Packages").OpenRead())
            {
                using (FileStream bzfile = File.Create(repoPath + @"\Packages.bz2"))
                {
                    try
                    {
                        BZip2.Compress(packagesFile, bzfile, true, 4096);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            Directory.GetFiles(repoPath + @"\tmp").ToList().ForEach(x => File.Delete(x));
            Directory.Delete(repoPath + @"\tmp", true);
        }

        private List<List<string>> GetPackages()
        {
            if (!Directory.Exists(repoPath + @"\tmp"))
            {
                Directory.CreateDirectory(repoPath + @"\tmp");
            }
            else
            {
                Directory.GetFiles(repoPath + @"\tmp").ToList().ForEach(x => File.Delete(x));
            }
            List<List<string>> packages = new List<List<string>>();
            if (!Directory.Exists(repoPath + @"\debs"))
            {
                MessageBox.Show(@"Something is wrong with the repository. Please either manually add the folder .\debs or create a new one.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                foreach (string file in Directory.GetFiles(repoPath + @"\debs"))
                {
                    Process.Start(@"C:\Windows\System32\tar.exe", $@"-x -v -f {file} -C {repoPath}\tmp").WaitForExit();
                    Process.Start(@"C:\Windows\System32\tar.exe", $@"-x -v -f {repoPath}\tmp\control.tar.gz -C {repoPath}\tmp").WaitForExit();
                    if (File.Exists(repoPath + @"\tmp\control"))
                    {
                        var filestream = File.OpenRead(file);
                        var package = File.ReadAllLines(repoPath + @"\tmp\control").ToList();
                        package.Add("Filename: debs/" + file.Substring(file.LastIndexOf(@"\") + 1));
                        package.Add("Size: " + new FileInfo(file).Length);
                        package.Add("MD5sum: " + BitConverter.ToString(MD5.Create().ComputeHash(filestream)).Replace("-", "").ToLowerInvariant());
                        package.Add("SHA1: " + BitConverter.ToString(SHA1.Create().ComputeHash(filestream)).Replace("-", "").ToLowerInvariant());
                        package.Add("SHA256: " + BitConverter.ToString(SHA256.Create().ComputeHash(filestream)).Replace("-", "").ToLowerInvariant());
                        packages.Add(package);
                    }
                    else
                    {
                        MessageBox.Show($"There is no control file in the package '{file}'.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    Directory.GetFiles(repoPath + @"\tmp").ToList().ForEach(x => File.Delete(x));
                }
                Directory.Delete(repoPath + @"\tmp", true);
            }
            return packages;
        }

        private void button_close_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void button_addpackage_Click(object sender, EventArgs e)
        {
            OpenFileDialog debDialog = new OpenFileDialog();
            debDialog.Filter = "Debian packages|*.deb";
            debDialog.Title = "Select a .deb file.";
            if (debDialog.ShowDialog() == DialogResult.OK)
            {
                File.Copy(debDialog.FileName, repoPath + @"\debs\" + debDialog.FileName.Substring(debDialog.FileName.LastIndexOf(@"\") + 1));
            }
            button_refreshPackages_Click(sender, e);
        }

        private void form_manager_Load(object sender, EventArgs e)
        {
            if (!Directory.Exists(repoPath + @"\debs") || !File.Exists(repoPath + @"\Release"))
            {
                File.Delete(cfgPath);
                Application.Restart();
            }
            label_repoPath.Text = repoPath;
            Packages = GetPackages();
            combo_packages.Items.Clear();
            foreach (List<string> package in Packages)
            {
                var name = "";
                var version = "";
                foreach (string line in package)
                {
                    if (line.Contains("Name: "))
                    {
                        name = line.Substring(6);
                    }
                    if (line.Contains("Version: "))
                    {
                        version = line.Substring(9);
                    }
                }
                combo_packages.Items.Add($"{name} v{version}");
            }
        }

        private void button_refreshPackages_Click(object sender, EventArgs e)
        {
            form_manager_Load(sender, e);
        }

        private void button_getPackageInfo_Click(object sender, EventArgs e)
        {
            if (combo_packages.SelectedItem == null)
            {
                MessageBox.Show("Please select a package.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                foreach (List<string> package in Packages)
                {
                    var name = "";
                    var version = "";
                    foreach (string line in package)
                    {
                        if (line.Contains("Name: "))
                        {
                            name = line.Substring(6);
                        }
                        if (line.Contains("Version: "))
                        {
                            version = line.Substring(9);
                        }
                    }
                    if ($"{name} v{version}" == combo_packages.SelectedItem.ToString())
                    {
                        MessageBox.Show(String.Join("\n", package), "Package info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
        }

        private void button_minimize_Click(object sender, EventArgs e)
        {
            WindowState = FormWindowState.Minimized;
        }

        private void button_return_Click(object sender, EventArgs e)
        {
            new form_init().Show();
            Hide();
        }

        private void form_manager_FormClosed(object sender, FormClosedEventArgs e)
        {
            Application.Exit();
        }

        private void button_changeIcon_Click(object sender, EventArgs e)
        {
            OpenFileDialog fileDialog = new OpenFileDialog();
            fileDialog.Filter = "ImageFiles|*.png";
            fileDialog.Title = "Choose an image for your repo.";
            if (fileDialog.ShowDialog() == DialogResult.OK)
            {
                if (File.Exists(repoPath + @"\CydiaIcon.png"))
                {
                    File.Delete(repoPath + @"\CydiaIcon.png");
                }
                File.Copy(fileDialog.FileName, repoPath + @"\CydiaIcon.png");
            }
        }

        private void label_repoPath_Click(object sender, EventArgs e)
        {
            Process.Start("explorer", repoPath);
        }
    }
}