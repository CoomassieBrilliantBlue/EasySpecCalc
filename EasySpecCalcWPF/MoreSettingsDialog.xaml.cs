using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.Linq;
using System.Diagnostics;

namespace EasySpecCalc
{
    public partial class MoreSettingsDialog : HandyControl.Controls.Window
    {
        private string configFilePath;
        private Dictionary<string, string> configDictionary;

        public MoreSettingsDialog(string configPath)
        {
            InitializeComponent();
            configFilePath = configPath;
            LoadConfiguration();
        }

        private void LoadConfiguration()
        {
            configDictionary = new Dictionary<string, string>();
            var lines = File.ReadAllLines(configFilePath);
            foreach (var line in lines)
            {
                var tokens = line.Split(new[] { '=' }, 2);
                if (tokens.Length == 2)
                {
                    configDictionary[tokens[0].Trim()] = tokens[1];
                }
            }

            txtMopacPath.Text = GetConfigValue("MopacPath", "");
            txtOrcaPath.Text = GetConfigValue("OrcaPath", "");
            txtMopacParallel.Text = GetConfigValue("MopacParallel", "");
            txtCoreCount.Text = GetConfigValue("CoreCount", "");
            txtMemory.Text = GetConfigValue("Memory", "");
            txtChargeQuantity.Text = GetConfigValue("ChargeQuantity", "");

            chkGroundStateFreq.IsChecked = GetConfigValue("GroundStateFreq", "") == "numfreq ";
            chkExcitedStateFreq.IsChecked = GetConfigValue("ExcitedStateFreq", "") == "numfreq ";
        }

        private string GetConfigValue(string key, string defaultValue)
        {
            return configDictionary.ContainsKey(key) ? configDictionary[key] : defaultValue;
        }

        private void btnBrowseMopac_Click(object sender, RoutedEventArgs e)
        {
            BrowseFile(txtMopacPath, "MOPAC Executable (mopac.exe)|mopac.exe");
        }

        private void btnBrowseOrca_Click(object sender, RoutedEventArgs e)
        {
            BrowseFile(txtOrcaPath, "ORCA Executable (orca.exe)|orca.exe");
        }

        private void BrowseFile(HandyControl.Controls.TextBox textBox, string filter)
        {
            using (var dialog = new CommonOpenFileDialog())
            {
                dialog.IsFolderPicker = false;
                dialog.InitialDirectory = Path.GetDirectoryName(textBox.Text);
                dialog.Filters.Add(new CommonFileDialogFilter(filter.Split('|')[0], filter.Split('|')[1]));
                var windowInteropHelper = new System.Windows.Interop.WindowInteropHelper(this);
                if (dialog.ShowDialog(windowInteropHelper.Handle) == CommonFileDialogResult.Ok)
                {
                    textBox.Text = dialog.FileName;
                }
            }
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            SaveConfiguration();
            this.Close();
        }

        private void SaveConfiguration()
        {
            configDictionary["MopacPath"] = txtMopacPath.Text;
            configDictionary["OrcaPath"] = txtOrcaPath.Text;
            configDictionary["MopacParallel"] = txtMopacParallel.Text;
            configDictionary["CoreCount"] = txtCoreCount.Text;
            configDictionary["Memory"] = txtMemory.Text;
            configDictionary["ChargeQuantity"] = txtChargeQuantity.Text;
            configDictionary["GroundStateFreq"] = chkGroundStateFreq.IsChecked == true ? "numfreq " : "";
            configDictionary["ExcitedStateFreq"] = chkExcitedStateFreq.IsChecked == true ? "numfreq " : "";

            var updatedLines = configDictionary.Select(kvp => $"{kvp.Key}={kvp.Value}");
            File.WriteAllLines(configFilePath, updatedLines);
        }
    }
}