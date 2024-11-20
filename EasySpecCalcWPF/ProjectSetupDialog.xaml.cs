using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.Management;
using System.Linq;
namespace EasySpecCalc
{
    public partial class ProjectSetupDialog : HandyControl.Controls.Window
    {
        private string configFilePath;
        private Dictionary<string, string> configDictionary;
        private Action<string, string> Log;
        public ProjectSetupDialog(Action<string, string> logMethod)
        {
            InitializeComponent();
            Log = logMethod;
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appDirectory = Path.Combine(localAppData, "EasySpecCalc");
            configFilePath = Path.Combine(appDirectory, "config");
            if (!Directory.Exists(appDirectory))
            {
                Directory.CreateDirectory(appDirectory);
            }
            InitializeConfiguration();
            LoadConfiguration();
            this.Closing += ProjectSetupDialog_Closing;
        }

        private void ProjectSetupDialog_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (ValidateAndSave())
            {
                var configOutput = new List<string>
    {
        "",
        $"Project Name: {configDictionary["ProjectName"]}",
        $"Project Path: {configDictionary["ProjectPath"]}",
        $"MOPAC Program Path: {configDictionary["MopacPath"]}",
        $"ORCA Program Path: {configDictionary["OrcaPath"]}",
        $"MOPAC Task Parallel Count: {configDictionary["MopacParallel"]}",
        $"Available Core Count: {configDictionary["CoreCount"]}",
        $"Available Memory (MB): {configDictionary["Memory"]}",
        $"Charge Quantity: {configDictionary["ChargeQuantity"]}",
        $"Ground State Frequency Calculation: {(string.IsNullOrEmpty(configDictionary["GroundStateFreq"]) ? "No" : "Yes")}",
        $"Excited State Frequency Calculation (TDDFT Method): {(string.IsNullOrEmpty(configDictionary["ExcitedStateFreq"]) ? "No" : "Yes")}"
    };

                string Output = string.Join(Environment.NewLine, configOutput);
                Log(Output, "#FFFFFF");
            }
            else
            {
                e.Cancel = true;
            }
        }

        private void InitializeConfiguration()
        {
            configDictionary = new Dictionary<string, string>();
            if (File.Exists(configFilePath))
            {
                var lines = File.ReadAllLines(configFilePath);
                foreach (var line in lines)
                {
                    var tokens = line.Split(new[] { '=' }, 2);
                    if (tokens.Length == 2)
                    {
                        configDictionary[tokens[0].Trim()] = tokens[1];
                    }
                }
            }

            if (!configDictionary.ContainsKey("ProjectName")) configDictionary["ProjectName"] = "";
            if (!configDictionary.ContainsKey("ProjectPath")) configDictionary["ProjectPath"] = "";
            if (!configDictionary.ContainsKey("MopacPath")) configDictionary["MopacPath"] = "MOPAC";
            if (!configDictionary.ContainsKey("OrcaPath")) configDictionary["OrcaPath"] = "ORCA";
            if (!configDictionary.ContainsKey("MopacParallel")) configDictionary["MopacParallel"] = "4";
            if (!configDictionary.ContainsKey("CoreCount")) configDictionary["CoreCount"] = GetDefaultCoreCount().ToString();
            if (!configDictionary.ContainsKey("Memory")) configDictionary["Memory"] = GetDefaultMemory().ToString();
            if (!configDictionary.ContainsKey("ChargeQuantity")) configDictionary["ChargeQuantity"] = "0";
            if (!configDictionary.ContainsKey("GroundStateFreq")) configDictionary["GroundStateFreq"] = "numfreq ";
            if (!configDictionary.ContainsKey("ExcitedStateFreq")) configDictionary["ExcitedStateFreq"] = "numfreq ";

            SaveConfiguration();
        }

        private void LoadConfiguration()
        {
            txtProjectName.Text = configDictionary["ProjectName"];
            txtProjectPath.Text = configDictionary["ProjectPath"];

            txtProjectName.SetCurrentValue(HandyControl.Controls.InfoElement.PlaceholderProperty, "Please enter project name");
            txtProjectPath.SetCurrentValue(HandyControl.Controls.InfoElement.PlaceholderProperty, "Please enter project path");
        }

        private int GetDefaultCoreCount()
        {
            int coreCount = 0;
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT NumberOfCores FROM Win32_Processor"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        coreCount += int.Parse(obj["NumberOfCores"].ToString());
                    }
                }
            }
            catch
            {
                coreCount = Environment.ProcessorCount;
            }
            return coreCount;
        }

        private long GetDefaultMemory()
        {
            long totalMemoryMB = 0;
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        totalMemoryMB = long.Parse(obj["TotalPhysicalMemory"].ToString()) / (1024 * 1024);
                    }
                }
            }
            catch
            {
                totalMemoryMB = 8192;
            }

            long availableMemoryMB = (long)(totalMemoryMB * 0.75);
            return availableMemoryMB - (availableMemoryMB % 1000);
        }

        private void SaveConfiguration()
        {
            var updatedLines = configDictionary.Select(kvp => $"{kvp.Key}={kvp.Value}");
            File.WriteAllLines(configFilePath, updatedLines);
        }

        private void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            BrowseFolder(txtProjectPath);
        }

        private void BrowseFolder(HandyControl.Controls.TextBox textBox)
        {
            using (var dialog = new CommonOpenFileDialog())
            {
                dialog.IsFolderPicker = true;
                dialog.InitialDirectory = textBox.Text;
                var windowInteropHelper = new System.Windows.Interop.WindowInteropHelper(this);
                if (dialog.ShowDialog(windowInteropHelper.Handle) == CommonFileDialogResult.Ok)
                {
                    textBox.Text = dialog.FileName;
                }
            }
        }

        private void btnConfirm_Click(object sender, RoutedEventArgs e)
        {
            if (ValidateAndSave())
            {
                this.Close();
            }
        }

        private bool ValidateAndSave()
        {
            if (string.IsNullOrWhiteSpace(txtProjectName.Text) || string.IsNullOrWhiteSpace(txtProjectPath.Text))
            {
                HandyControl.Controls.MessageBox.Show("Project name and project path cannot be empty!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            configDictionary["ProjectName"] = txtProjectName.Text;
            configDictionary["ProjectPath"] = txtProjectPath.Text;
            SaveConfiguration();

            if (!Directory.Exists(txtProjectPath.Text))
            {
                Directory.CreateDirectory(txtProjectPath.Text);
            }

            return true;
        }

        private void btnMoreSettings_Click(object sender, RoutedEventArgs e)
        {
            MoreSettingsDialog moreSettingsDialog = new MoreSettingsDialog(configFilePath);
            moreSettingsDialog.ShowDialog();
            InitializeConfiguration();
        }
    }
}