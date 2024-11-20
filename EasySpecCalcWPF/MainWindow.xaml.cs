using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Threading.Tasks;
using System.Linq;
using System.Windows.Controls;
using System.Collections.Generic;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Input;
using HandyControl.Controls;
using EasySpecCalcCore;

namespace EasySpecCalc
{
    public partial class MainWindow : HandyControl.Controls.Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.ContentRendered += (s, e) => BtnProjectSetup_Click(this, new RoutedEventArgs());
            Log("Welcome to EasySpecCalc!", "#FFFFFF");
        }
        private void Log(string message, string colorCode)
        {
            string timePrefix = DateTime.Now.ToString("\nyyyy-MM-dd HH:mm:ss" + " :");
            Run timePrefixRun = new Run(timePrefix) { Foreground = Brushes.White };
            Brush messageBrush = (Brush)new BrushConverter().ConvertFromString(colorCode);
            Run messageRun = new Run("\n      " + message + Environment.NewLine) { Foreground = messageBrush };

            Dispatcher.Invoke(() =>
            {
                MyTextBlock.Inlines.Add(timePrefixRun);
                MyTextBlock.Inlines.Add(messageRun);
                MyScrollViewer.ScrollToEnd();
            });
        }
        private void BtnAdvancedSettings_Click(object sender, RoutedEventArgs e)
        {
            if (MainContentGrid.Visibility == Visibility.Visible)
            {
                MainContentGrid.Visibility = Visibility.Collapsed;
                AdvancedSettingsContentGrid.Visibility = Visibility.Visible;
            }
            else
            {
                MainContentGrid.Visibility = Visibility.Visible;
                AdvancedSettingsContentGrid.Visibility = Visibility.Collapsed;
            }
        }
        private async void BtnCalculateAll_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                BtnCalculateAll.IsEnabled = false;

                var inputDialog = new InputSmilesDialog();
                if (inputDialog.ShowDialog() == true)
                {
                    if (!inputDialog.SkipSmilesGeneration)
                    {
                        string smiles = inputDialog.Smiles;
                        HandyControl.Controls.MessageBox.Show(smiles, "SMILES", MessageBoxButton.OK, MessageBoxImage.Information);
                        var processor = new MoleculeProcessor();
                        var molBlock = processor.ProcessMolecule(smiles);
                        HandyControl.Controls.MessageBox.Show(molBlock, "Mol Block", MessageBoxButton.OK, MessageBoxImage.Information);
                        processor.ConvertMolecule(molBlock);
                    }
                    else
                    {
                        Log("Skipping SMILES generation, using MOL2 file directly", "#00FF00");
                    }

                    var conformationSearch = new ConformationSearch();
                    Action<string> conformationSearchHandler = (output) =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            string colorCode = output.Contains("Command executed successfully") || output.Contains("Conformation search completed") ? "#00FF00" :
                                               output.Contains("Error occurred") || output.Contains("Command execution failed") ? "#FF0000" : "#FFFFFF";
                            Log(output, colorCode);
                        });
                    };
                    conformationSearch.OutputReceived += conformationSearchHandler;

                    try
                    {
                        Log("Starting conformation search...", "#00FF00");
                        await conformationSearch.RunConformationSearchAsync();
                        Log("Conformation search completed, generating XYZ file...", "#00FF00");
                        var xyzOutput = new XYZoutput();
                        Action<string> xyzOutputHandler = (output) =>
                        {
                            Dispatcher.Invoke(() =>
                            {
                                string colorCode = output.Contains("failed") || output.Contains("error") ? "#FF0000" :
                                                   output.Contains("success") ? "#00FF00" : "#FFFFFF";
                                Log(output, colorCode);
                            });
                        };
                        xyzOutput.OutputReceived += xyzOutputHandler;
                        string workingDirectory = Config.ProjectPath;
                        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(Directory.GetFiles(workingDirectory, "*.prmtop")[0]);
                        await xyzOutput.GenerateXYZFileAsync(workingDirectory, fileNameWithoutExtension);
                        Log("XYZ file generated successfully", "#00FF00");
                        xyzOutput.OutputReceived -= xyzOutputHandler;
                    }
                    finally
                    {
                        conformationSearch.OutputReceived -= conformationSearchHandler;
                    }
                }

                var mopacCalculator = new MOPACCalculator();
                var mopacOutputAnalyzer = new MOPACOutputAnalyzer();
                Action<string> progressHandler = (output) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        string colorCode = output.Contains("completed") || output.Contains("success") ? "#00FF00" :
                                           output.Contains("error") || output.Contains("failed") ? "#FF0000" : "#FFFFFF";
                        Log(output, colorCode);
                    });
                };

                mopacCalculator.ProgressReported += progressHandler;
                mopacOutputAnalyzer.ProgressReported += progressHandler;

                try
                {
                    Log("Starting MOPAC calculation...", "#00FF00");
                    string workingDirectory = Config.ProjectPath;
                    string mopacPath = Config.MopacPath;
                    string projectName = Config.ProjectName;
                    string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(Directory.GetFiles(workingDirectory, "*.prmtop")[0]);
                    await mopacCalculator.RunMOPACCalculationsAsync(workingDirectory, fileNameWithoutExtension, mopacPath);
                    Log("MOPAC calculation completed", "#00FF00");

                    Log("Starting MOPAC output file analysis...", "#00FF00");
                    string mopacOutputDirectory = Path.Combine(workingDirectory, "MOPAC_Results");
                    if (!Directory.Exists(mopacOutputDirectory))
                    {
                        throw new DirectoryNotFoundException($"MOPAC output directory does not exist: {mopacOutputDirectory}");
                    }
                    var (lowestEnergyFrame, lowestEnergy) = await mopacOutputAnalyzer.AnalyzeMOPACOutputsAsync(mopacOutputDirectory);
                    Log($"Analysis completed. Lowest energy conformation is {lowestEnergyFrame}, with energy {lowestEnergy} KCAL/MOL", "#00FF00");
                    await mopacOutputAnalyzer.ConvertLowestEnergyToXYZAsync(mopacOutputDirectory, lowestEnergyFrame, workingDirectory, projectName);
                    Log($"Converted lowest energy conformation {lowestEnergyFrame} to XYZ format", "#00FF00");
                }
                finally
                {
                    mopacCalculator.ProgressReported -= progressHandler;
                    mopacOutputAnalyzer.ProgressReported -= progressHandler;
                }

                var orcaOutputWindow = new ORCAOutputWindow();
                try
                {
                    var orca = new ORCA(Config.ProjectPath, Config.ProjectName);

                    ConfigureORCAEvents(orca, orcaOutputWindow);

                    orcaOutputWindow.Show();

                    await orca.RunCalculationsAsync();

                    Log("ORCA calculations completed (including ground state, excited state, and CCSD calculations)", "#00FF00");
                }
                finally
                {
                    orcaOutputWindow.Close();
                }

                Log("All calculations and analysis tasks completed", "#00FF00");
            }
            catch (Exception ex)
            {
                Log($"Error occurred: {ex.Message}", "#FF0000");
                HandyControl.Controls.MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnCalculateAll.IsEnabled = true;
            }
        }
        private async void BtnProjectSetup_Click(object sender, RoutedEventArgs e)
        {
            var projectSetupDialog = new ProjectSetupDialog(Log);
            projectSetupDialog.Owner = this;
            projectSetupDialog.ShowDialog();
        }
        private async void BtnConformationSearch_Click(object sender, RoutedEventArgs e)
        {
            var inputDialog = new InputSmilesDialog();
            if (inputDialog.ShowDialog() == true)
            {
                if (!inputDialog.SkipSmilesGeneration)
                {
                    string smiles = inputDialog.Smiles;
                    HandyControl.Controls.MessageBox.Show(smiles, "SMILES", MessageBoxButton.OK, MessageBoxImage.Information);
                    var processor = new MoleculeProcessor();
                    var molBlock = processor.ProcessMolecule(smiles);
                    HandyControl.Controls.MessageBox.Show(molBlock, "Mol Block", MessageBoxButton.OK, MessageBoxImage.Information);
                    processor.ConvertMolecule(molBlock);
                }
                else
                {
                    Log("Skipping SMILES generation, using MOL2 file directly", "#00FF00");
                }

                var conformationSearch = new ConformationSearch();
                Action<string> conformationSearchHandler = null;
                conformationSearchHandler = (output) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        string colorCode = output.Contains("Command executed successfully") || output.Contains("Conformation search completed") ? "#00FF00" :
                                           output.Contains("Error occurred") || output.Contains("Command execution failed") ? "#FF0000" : "#FFFFFF";
                        Log(output, colorCode);
                    });
                };
                conformationSearch.OutputReceived += conformationSearchHandler;
                try
                {
                    Log("Starting conformation search...", "#00FF00");
                    await conformationSearch.RunConformationSearchAsync();
                    Log("Conformation search completed, generating XYZ file...", "#00FF00");
                    var xyzOutput = new XYZoutput();
                    Action<string> xyzOutputHandler = null;
                    xyzOutputHandler = (output) =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            string colorCode = output.Contains("failed") || output.Contains("error") ? "#FF0000" :
                                               output.Contains("success") ? "#00FF00" : "#FFFFFF";
                            Log(output, colorCode);
                        });
                    };
                    xyzOutput.OutputReceived += xyzOutputHandler;
                    string workingDirectory = Config.ProjectPath;
                    string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(Directory.GetFiles(workingDirectory, "*.prmtop")[0]);
                    await xyzOutput.GenerateXYZFileAsync(workingDirectory, fileNameWithoutExtension);
                    xyzOutput.OutputReceived -= xyzOutputHandler;
                }
                catch (Exception ex)
                {
                    Log($"Error occurred: {ex.Message}", "#FF0000");
                    HandyControl.Controls.MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    conformationSearch.OutputReceived -= conformationSearchHandler;
                }
            }
        }
        private async void BtnMopacCalculationAndAnalysis_Click(object sender, RoutedEventArgs e)
        {
            var mopacCalculator = new MOPACCalculator();
            var mopacOutputAnalyzer = new MOPACOutputAnalyzer();
            Action<string> progressHandler = null;
            progressHandler = (output) =>
            {
                Dispatcher.Invoke(() =>
                {
                    string colorCode = output.Contains("completed") || output.Contains("success") ? "#00FF00" :
                                       output.Contains("error") || output.Contains("failed") ? "#FF0000" : "#FFFFFF";
                    Log(output, colorCode);
                });
            };

            mopacCalculator.ProgressReported += progressHandler;
            mopacOutputAnalyzer.ProgressReported += progressHandler;

            try
            {
                Log("Starting MOPAC calculation...", "#00FF00");
                string workingDirectory = Config.ProjectPath;
                string mopacPath = Config.MopacPath;
                string projectName = Config.ProjectName;
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(Directory.GetFiles(workingDirectory, "*.prmtop")[0]);
                await mopacCalculator.RunMOPACCalculationsAsync(workingDirectory, fileNameWithoutExtension, mopacPath);

                Log("Starting MOPAC output file analysis...", "#00FF00");
                string mopacOutputDirectory = Path.Combine(workingDirectory, "MOPAC_Results");
                if (!Directory.Exists(mopacOutputDirectory))
                {
                    throw new DirectoryNotFoundException($"MOPAC output directory does not exist: {mopacOutputDirectory}");
                }
                var (lowestEnergyFrame, lowestEnergy) = await mopacOutputAnalyzer.AnalyzeMOPACOutputsAsync(mopacOutputDirectory);
                Log($"Analysis completed. Lowest energy conformation is {lowestEnergyFrame}, with energy {lowestEnergy} KCAL/MOL", "#00FF00");
                await mopacOutputAnalyzer.ConvertLowestEnergyToXYZAsync(mopacOutputDirectory, lowestEnergyFrame, workingDirectory, projectName);
                Log($"Converted lowest energy conformation {lowestEnergyFrame} to XYZ format", "#00FF00");
            }
            catch (Exception ex)
            {
                Log($"Error occurred: {ex.Message}", "#FF0000");
                HandyControl.Controls.MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                mopacCalculator.ProgressReported -= progressHandler;
                mopacOutputAnalyzer.ProgressReported -= progressHandler;
            }
        }
        private async void BtnORCA_Click(object sender, RoutedEventArgs e)
        {
            var orcaOutputWindow = new ORCAOutputWindow();
            try
            {
                BtnORCA.IsEnabled = false;

                var orca = new ORCA(Config.ProjectPath, Config.ProjectName);

                ConfigureORCAEvents(orca, orcaOutputWindow);

                orcaOutputWindow.Show();

                await orca.RunCalculationsAsync();

                Log("ORCA calculations completed (including ground state, excited state, and CCSD calculations)", "#00FF00");
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
            finally
            {
                BtnORCA.IsEnabled = true;
                orcaOutputWindow.Close();
            }
        }
        private void BtnAbout_Click(object sender, RoutedEventArgs e)
        {
            var aboutDialog = new AboutDialog();
            aboutDialog.Owner = this;
            aboutDialog.ShowDialog();
        }
        private void ConfigureORCAEvents(ORCA orca, ORCAOutputWindow outputWindow)
        {
            orca.ProgressReported += (output) => Dispatcher.Invoke(() =>
            {
                string colorCode = DetermineColorCode(output);
                Log(output, colorCode);
            });

            orca.OutputReceived += (output) => outputWindow.Dispatcher.Invoke(() =>
                outputWindow.AppendOutput(output));

            orca.UserConfirmationRequired += async (message) =>
            {
                var result = await Dispatcher.InvokeAsync(() =>
                    HandyControl.Controls.MessageBox.Show(
                        message,
                        "Negative Frequency Found",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning
                    )
                );
                return result == MessageBoxResult.Yes;
            };
        }
        private string DetermineColorCode(string output)
        {
            if (output.Contains("completed") || output.Contains("success"))
                return "#00FF00";
            if (output.Contains("error") || output.Contains("failed"))
                return "#FF0000";
            return "#FFFFFF";
        }
        private void HandleError(Exception ex)
        {
            Log($"Error occurred: {ex.Message}", "#FF0000");
            Dispatcher.Invoke(() =>
                HandyControl.Controls.MessageBox.Show(
                    ex.Message,
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                )
            );
        }
    }
}