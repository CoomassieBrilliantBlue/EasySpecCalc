using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using GraphMolWrap;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Text;

namespace EasySpecCalcCore
{
    public static class Config
    {
        public static string ProjectName { get; private set; }
        public static string ProjectPath { get; private set; }
        public static string MopacPath { get; private set; }
        public static string OrcaPath { get; private set; }
        public static int MopacParallel { get; private set; }
        public static int CoreCount { get; private set; }
        public static long Memory { get; private set; }
        public static int ChargeQuantity { get; private set; }
        public static string GroundStateFreq { get; private set; }
        public static string ExcitedStateFreq { get; private set; }

        private static string configFilePath;

        static Config()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appDirectory = Path.Combine(localAppData, "EasySpecCalc");
            configFilePath = Path.Combine(appDirectory, "config");
            LoadConfiguration();
        }

        public static void LoadConfiguration()
        {
            Dictionary<string, string> configDictionary = new Dictionary<string, string>();
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
            ProjectName = GetConfigValue(configDictionary, "ProjectName", "");
            ProjectPath = GetConfigValue(configDictionary, "ProjectPath", "");
            MopacPath = GetConfigValue(configDictionary, "MopacPath", @"C:\Program Files\MOPAC\bin\mopac.exe");
            OrcaPath = GetConfigValue(configDictionary, "OrcaPath", Environment.GetEnvironmentVariable("ORCA") ?? "ORCA");
            MopacParallel = int.Parse(GetConfigValue(configDictionary, "MopacParallel", "4"));
            CoreCount = int.Parse(GetConfigValue(configDictionary, "CoreCount", Environment.ProcessorCount.ToString()));
            Memory = long.Parse(GetConfigValue(configDictionary, "Memory", "8192"));
            ChargeQuantity = int.Parse(GetConfigValue(configDictionary, "ChargeQuantity", "0"));
            GroundStateFreq = GetConfigValue(configDictionary, "GroundStateFreq", "");
            ExcitedStateFreq = GetConfigValue(configDictionary, "ExcitedStateFreq", "");
        }

        private static string GetConfigValue(Dictionary<string, string> configDict, string key, string defaultValue)
        {
            return configDict.ContainsKey(key) ? configDict[key] : defaultValue;
        }

        public static void SaveConfiguration()
        {
            List<string> lines = new List<string>
        {
            $"ProjectName={ProjectName}",
            $"ProjectPath={ProjectPath}",
            $"MopacPath={MopacPath}",
            $"OrcaPath={OrcaPath}",
            $"MopacParallel={MopacParallel}",
            $"CoreCount={CoreCount}",
            $"Memory={Memory}",
            $"ChargeQuantity={ChargeQuantity}",
            $"GroundStateFreq={GroundStateFreq}",
            $"ExcitedStateFreq={ExcitedStateFreq}"
        };
            File.WriteAllLines(configFilePath, lines);
        }

        public static void UpdateSetting(string key, string value)
        {
            switch (key)
            {
                case "ProjectName":
                    ProjectName = value;
                    break;
                case "ProjectPath":
                    ProjectPath = value;
                    break;
                case "MopacPath":
                    MopacPath = value;
                    break;
                case "OrcaPath":
                    OrcaPath = value;
                    break;
                case "MopacParallel":
                    MopacParallel = int.Parse(value);
                    break;
                case "CoreCount":
                    CoreCount = int.Parse(value);
                    break;
                case "Memory":
                    Memory = long.Parse(value);
                    break;
                case "ChargeQuantity":
                    ChargeQuantity = int.Parse(value);
                    break;
                case "GroundStateFreq":
                    GroundStateFreq = value;
                    break;
                case "ExcitedStateFreq":
                    ExcitedStateFreq = value;
                    break;
            }
            SaveConfiguration();
        }
    }
    public class MoleculeProcessor
    {
        public string ProcessMolecule(string smiles)
        {
            var mol = RWMol.MolFromSmiles(smiles, 0, true, null);
            RDKFuncs.addHs(mol);
            DistanceGeom.EmbedMolecule(mol);
            ForceField.MMFFOptimizeMolecule(mol);
            var molBlock = RDKFuncs.MolToV3KMolBlock(mol);
            return molBlock;
        }
        public void ConvertMolecule(string molBlock)
        {
            var projectDir = Config.ProjectPath;
            var molFilePath = Path.Combine(projectDir, Config.ProjectName + ".mol");
            File.WriteAllText(molFilePath, molBlock);
            var mol2FilePath = Path.Combine(projectDir, Config.ProjectName + ".mol2");
            var obabelPath = Path.Combine(Directory.GetCurrentDirectory(), "obabel.exe");
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = obabelPath,
                Arguments = $"-i mol \"{molFilePath}\" -o mol2 -O \"{mol2FilePath}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using (Process process = Process.Start(psi))
            {
                process.WaitForExit();
            }
            string mol2Content = File.ReadAllText(mol2FilePath);
            mol2Content = mol2Content.Replace("UNL1", "****");
            File.WriteAllText(mol2FilePath, mol2Content);
            File.Delete(molFilePath);
        }
    }
    public class ConformationSearch
    {
        public event Action<string> OutputReceived;
        public async Task RunConformationSearchAsync()
        {
            var workingDirectory = Config.ProjectPath;

            if (!Directory.Exists(workingDirectory))
            {
                throw new DirectoryNotFoundException($"Working directory {workingDirectory} does not exist.");
            }

            var mol2Files = Directory.GetFiles(workingDirectory, "*.mol2");
            if (mol2Files.Length == 0)
            {
                throw new FileNotFoundException("No .mol2 file found in working directory.");
            }
            var mol2File = mol2Files[0];

            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(mol2File);

            Directory.SetCurrentDirectory(workingDirectory);

            var wslCommand1 = $"conda activate AmberTools23 && antechamber -i {fileNameWithoutExtension}.mol2 -fi mol2 -o {fileNameWithoutExtension}.prepin -fo prepi -c bcc -s 2 && parmchk2 -i {fileNameWithoutExtension}.prepin -f prepi -o {fileNameWithoutExtension}.frcmod";

            await RunWslCommandAsync(wslCommand1);

            var fileNameWithoutExtensionDirectory = Path.Combine(workingDirectory, fileNameWithoutExtension);
            if (!Directory.Exists(fileNameWithoutExtensionDirectory))
            {
                Directory.CreateDirectory(fileNameWithoutExtensionDirectory);
            }

            var filesToMove1 = Directory.GetFiles(workingDirectory)
                .Where(f => Path.GetFileName(f) != $"{fileNameWithoutExtension}.prepin" && Path.GetFileName(f) != $"{fileNameWithoutExtension}.frcmod");
            foreach (var file in filesToMove1)
            {
                var destFileName = Path.Combine(fileNameWithoutExtensionDirectory, Path.GetFileName(file));
                File.Move(file, destFileName, true);
            }

            var prepinPath = Path.Combine(workingDirectory, $"{fileNameWithoutExtension}.prepin");
            if (File.Exists(prepinPath))
            {
                var prepinContent = File.ReadAllText(prepinPath);
                prepinContent = Regex.Replace(prepinContent, @"\*\*\*\s+INT", "INT");
                prepinContent = prepinContent.Replace("\r\n", "\n").Replace("\r", "\n");
                File.WriteAllText(prepinPath, prepinContent);
            }
            else
            {
                throw new FileNotFoundException($"{prepinPath} file not found.");
            }

            var tleapCommandsContent = $@"source leaprc.gaff
loadamberprep {fileNameWithoutExtension}.prepin
loadamberparams {fileNameWithoutExtension}.frcmod
saveamberparm INT {fileNameWithoutExtension}.prmtop {fileNameWithoutExtension}.inpcrd
quit
";
            tleapCommandsContent = tleapCommandsContent.Replace("\r\n", "\n").Replace("\r", "\n");
            var tleapCommandsPath = Path.Combine(workingDirectory, "tleap_commands.txt");
            File.WriteAllText(tleapCommandsPath, tleapCommandsContent);

            var md1InContent = @"1ns simulation at 1000K
&cntrl
imin=0,nstlim=500000,dt=0.002,ntpr=50,ntwr=100,ntwx=5000,ntc=2,
tempi=1000,temp0=1000,ntt=3,ntb=0,cut=12.0,gamma_ln=2.0,igb=0
ntxo=1, ! Write coordinate file in ASCII format
ioutfm=0, ! Write trajectory file in ASCII format
/
";
            md1InContent = md1InContent.Replace("\r\n", "\n").Replace("\r", "\n");
            var md1InPath = Path.Combine(workingDirectory, "md.in");
            File.WriteAllText(md1InPath, md1InContent);

            var wslCommand2 = $"conda activate AmberTools23 && tleap -s -f leaprc.ff10 -f tleap_commands.txt && sander -O -i md.in -o md.out -p {fileNameWithoutExtension}.prmtop -c {fileNameWithoutExtension}.inpcrd -r md.rst -x {fileNameWithoutExtension}.mdcrd";

            await RunWslCommandAsync(wslCommand2);

            var filesToMove2 = Directory.GetFiles(workingDirectory)
                .Where(f => Path.GetFileName(f) != $"{fileNameWithoutExtension}.prmtop" && Path.GetFileName(f) != $"{fileNameWithoutExtension}.mdcrd");
            foreach (var file in filesToMove2)
            {
                var destFileName = Path.Combine(fileNameWithoutExtensionDirectory, Path.GetFileName(file));
                File.Move(file, destFileName, true);
            }
        }
        private async Task RunWslCommandAsync(string command)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "wsl.exe",
                Arguments = $"bash -ic \"{command}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Config.ProjectPath
            };

            using (var process = new Process { StartInfo = psi, EnableRaisingEvents = true })
            {
                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        OutputReceived?.Invoke(e.Data);
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        OutputReceived?.Invoke(e.Data);
                    }
                };

                process.Start();

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await Task.Run(() => process.WaitForExit());

                if (process.ExitCode == 1)
                {
                    OutputReceived?.Invoke("Command executed successfully");
                }
                else if (process.ExitCode != 0)
                {
                    OutputReceived?.Invoke($"Command execution failed with exit code: {process.ExitCode}");
                }
            }
        }
    }
    public class XYZoutput
    {
        public event Action<string> OutputReceived;

        public async Task GenerateXYZFileAsync(string workingDirectory, string fileNameWithoutExtension)
        {
            string prmtopPath = Path.Combine(workingDirectory, $"{fileNameWithoutExtension}.prmtop");
            string mdcrdPath = Path.Combine(workingDirectory, $"{fileNameWithoutExtension}.mdcrd");
            string outputPath = Path.Combine(workingDirectory, $"{fileNameWithoutExtension}.xyz");

            if (!File.Exists(prmtopPath) || !File.Exists(mdcrdPath))
            {
                throw new FileNotFoundException("prmtop or mdcrd file not found");
            }

            OutputReceived?.Invoke("Start processing files...");

            await Task.Run(() =>
            {
                List<string> atoms = ReadAtoms(prmtopPath);
                OutputReceived?.Invoke($"Read {atoms.Count} atoms from {prmtopPath}");

                List<List<Vector3>> frames = ReadFrames(mdcrdPath, atoms.Count);
                OutputReceived?.Invoke($"Read {frames.Count} frames from {mdcrdPath}");

                WriteXyzFile(outputPath, atoms, frames);
                OutputReceived?.Invoke($"XYZ file generated: {outputPath}");
            });

            if (!File.Exists(outputPath))
            {
                throw new Exception("XYZ file generation failed");
            }

            OutputReceived?.Invoke("XYZ file generated successfully");
        }

        private List<string> ReadAtoms(string prmtopPath)
        {
            string[] lines = File.ReadAllLines(prmtopPath);
            int atomNameIndex = Array.FindIndex(lines, l => l.Trim().StartsWith("%FLAG ATOM_NAME"));
            int chargeIndex = Array.FindIndex(lines, l => l.Trim().StartsWith("%FLAG CHARGE"));

            if (atomNameIndex == -1 || chargeIndex == -1 || atomNameIndex >= chargeIndex)
            {
                throw new Exception("Atom information section not found in prmtop file");
            }

            List<string> atoms = new List<string>();
            for (int i = atomNameIndex + 2; i < chargeIndex; i++)
            {
                atoms.AddRange(lines[i].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
            }

            return atoms;
        }

        private List<List<Vector3>> ReadFrames(string mdcrdPath, int atomCount)
        {
            List<List<Vector3>> frames = new List<List<Vector3>>();
            List<float> allCoordinates = new List<float>();

            using (StreamReader reader = new StreamReader(mdcrdPath))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.StartsWith("INT")) continue;

                    string[] values = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    allCoordinates.AddRange(values.Select(v => float.Parse(v, CultureInfo.InvariantCulture)));
                }
            }

            int coordinatesPerFrame = atomCount * 3;
            int frameCount = allCoordinates.Count / coordinatesPerFrame;

            for (int i = 0; i < frameCount; i++)
            {
                List<Vector3> frame = new List<Vector3>();
                int startIndex = i * coordinatesPerFrame;

                for (int j = 0; j < atomCount; j++)
                {
                    int index = startIndex + j * 3;
                    frame.Add(new Vector3(
                        allCoordinates[index],
                        allCoordinates[index + 1],
                        allCoordinates[index + 2]
                    ));
                }

                frames.Add(frame);
            }

            return frames;
        }

        private void WriteXyzFile(string xyzFile, List<string> atoms, List<List<Vector3>> frames)
        {
            using (StreamWriter writer = new StreamWriter(xyzFile))
            {
                for (int i = 0; i < frames.Count; i++)
                {
                    writer.WriteLine(atoms.Count);
                    writer.WriteLine($"generated by EasySpecCalc,Frame {i + 1}");

                    for (int j = 0; j < atoms.Count; j++)
                    {
                        Vector3 position = frames[i][j];
                        writer.WriteLine($"{atoms[j]} {position.X:F6} {position.Y:F6} {position.Z:F6}");
                    }
                }
            }
        }

        private struct Vector3
        {
            public float X, Y, Z;

            public Vector3(float x, float y, float z)
            {
                X = x;
                Y = y;
                Z = z;
            }
        }
    }
    public class MOPACCalculator
    {
        public event Action<string> ProgressReported;

        public async Task RunMOPACCalculationsAsync(string workingDirectory, string fileNameWithoutExtension, string MopacPath)
        {
            string xyzFilePath = Path.Combine(workingDirectory, $"{fileNameWithoutExtension}.xyz");
            if (!File.Exists(xyzFilePath))
            {
                throw new FileNotFoundException("XYZ file not found", xyzFilePath);
            }

            string destinationDirectory = Path.Combine(workingDirectory, fileNameWithoutExtension);
            if (!Directory.Exists(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            string prmtopPath = Path.Combine(workingDirectory, $"{fileNameWithoutExtension}.prmtop");
            string mdcrdPath = Path.Combine(workingDirectory, $"{fileNameWithoutExtension}.mdcrd");

            string destinationPrmtopPath = Path.Combine(destinationDirectory, $"{fileNameWithoutExtension}.prmtop");
            string destinationMdcrdPath = Path.Combine(destinationDirectory, $"{fileNameWithoutExtension}.mdcrd");

            File.Move(prmtopPath, destinationPrmtopPath, true);
            File.Move(mdcrdPath, destinationMdcrdPath, true);

            ProgressReported?.Invoke($"Moved prmtop and mdcrd files to {destinationDirectory}");

            ProgressReported?.Invoke("Starting MOPAC calculations...");
            List<string> frames = ReadXYZFile(xyzFilePath);
            ProgressReported?.Invoke($"Read {frames.Count} frames from {xyzFilePath}");
            string mopacOutputDirectory = Path.Combine(workingDirectory, "MOPAC_Results");
            Directory.CreateDirectory(mopacOutputDirectory);
            for (int i = 0; i < frames.Count; i++)
            {
                string mopFileName = Path.Combine(mopacOutputDirectory, $"Frame_{i + 1}.mop");
                CreateMOPACInputFile(frames[i], mopFileName);
                ProgressReported?.Invoke($"Created MOPAC input file: {mopFileName}");
            }
            await RunMOPACCalculationsParallelAsync(mopacOutputDirectory, frames.Count, MopacPath);
            ProgressReported?.Invoke("MOPAC calculations completed");
        }

        private List<string> ReadXYZFile(string filePath)
        {
            List<string> frames = new List<string>();
            string currentFrame = "";
            int atomCount = 0;
            int currentAtomCount = 0;

            foreach (string line in File.ReadLines(filePath))
            {
                if (int.TryParse(line, out int count))
                {
                    if (currentFrame != "")
                    {
                        frames.Add(currentFrame.Trim());
                    }
                    atomCount = count;
                    currentAtomCount = 0;
                    currentFrame = line + Environment.NewLine;
                }
                else
                {
                    currentFrame += line + Environment.NewLine;
                    if (!line.StartsWith("generated by EasySpecCalc,Frame"))
                    {
                        currentAtomCount++;
                    }
                    if (currentAtomCount == atomCount)
                    {
                        frames.Add(currentFrame.Trim());
                        currentFrame = "";
                    }
                }
            }

            if (currentFrame != "")
            {
                frames.Add(currentFrame.Trim());
            }

            return frames;
        }

        private void CreateMOPACInputFile(string frame, string fileName)
        {
            using (StreamWriter writer = new StreamWriter(fileName))
            {
                writer.WriteLine("PM6-DH+ precise");
                writer.WriteLine("molecule");
                writer.WriteLine("All coordinates are Cartesian");

                string[] lines = frame.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string line in lines.Skip(2))
                {
                    string[] parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 4)
                    {
                        string atom = Regex.Replace(parts[0], @"\d+", "");
                        double x = double.Parse(parts[1], CultureInfo.InvariantCulture);
                        double y = double.Parse(parts[2], CultureInfo.InvariantCulture);
                        double z = double.Parse(parts[3], CultureInfo.InvariantCulture);
                        writer.WriteLine($"{atom,-2} {x,10:F8} 1 {y,10:F8} 1 {z,10:F8} 1");
                    }
                }
            }
        }

        private async Task RunMOPACCalculationsParallelAsync(string outputDirectory, int frameCount, string MopacPath)
        {
            SemaphoreSlim semaphore = new SemaphoreSlim(Config.MopacParallel);
            List<Task> tasks = new List<Task>();

            for (int i = 1; i <= frameCount; i++)
            {
                int frameNumber = i;
                tasks.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        await RunMOPACForFrameAsync(outputDirectory, frameNumber, MopacPath);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }));
            }

            await Task.WhenAll(tasks);
        }

        private async Task RunMOPACForFrameAsync(string outputDirectory, int frameNumber, string MopacPath)
        {
            string mopFilePath = Path.GetFullPath(Path.Combine(outputDirectory, $"Frame_{frameNumber}.mop"));
            string mopacCommand = $"\"{MopacPath}\" \"{mopFilePath}\"";

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = $"\"{MopacPath}\"",
                Arguments = $"\"{mopFilePath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process process = new Process { StartInfo = psi })
            {
                try
                {
                    ProgressReported?.Invoke($"Executing command: {mopacCommand}");
                    process.Start();
                    string output = await process.StandardOutput.ReadToEndAsync();
                    string error = await process.StandardError.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    if (process.ExitCode == 0)
                    {
                        ProgressReported?.Invoke($"MOPAC calculation completed for Frame {frameNumber}.");
                    }
                    else
                    {
                        ProgressReported?.Invoke($"MOPAC calculation failed for Frame {frameNumber}. Exit code: {process.ExitCode}");
                        if (!string.IsNullOrEmpty(error))
                        {
                            ProgressReported?.Invoke($"Error message: {error}");
                        }
                    }

                    if (!string.IsNullOrEmpty(output))
                    {
                        ProgressReported?.Invoke($"Output: {output}");
                    }
                }
                catch (Exception ex)
                {
                    ProgressReported?.Invoke($"Error executing MOPAC: {ex.Message}");
                }
            }
        }
    }
    public class MOPACOutputAnalyzer
    {
        public event Action<string> ProgressReported;
        public async Task<(string lowestEnergyFrame, double lowestEnergy)> AnalyzeMOPACOutputsAsync(string outputDirectory)
        {
            ProgressReported?.Invoke("Starting analysis of MOPAC output files...");
            var outputFiles = Directory.GetFiles(outputDirectory, "Frame_*.out");
            if (outputFiles.Length == 0)
            {
                throw new FileNotFoundException("No MOPAC output files found", outputDirectory);
            }
            ProgressReported?.Invoke($"Found {outputFiles.Length} MOPAC output files");
            string lowestEnergyFrame = "";
            double lowestEnergy = double.MaxValue;
            foreach (var file in outputFiles)
            {
                var (frameNumber, energy) = await ExtractEnergyFromFileAsync(file);
                if (energy < lowestEnergy)
                {
                    lowestEnergy = energy;
                    lowestEnergyFrame = $"Frame_{frameNumber}";
                }
                ProgressReported?.Invoke($"Processed {Path.GetFileName(file)}, Energy: {energy} KCAL/MOL");
            }
            return (lowestEnergyFrame, lowestEnergy);
        }
        private async Task<(int frameNumber, double energy)> ExtractEnergyFromFileAsync(string filePath)
        {
            string content = await File.ReadAllTextAsync(filePath);
            var match = Regex.Match(content, @"FINAL HEAT OF FORMATION =\s+([-\d.]+)\s+KCAL/MOL");
            if (match.Success && double.TryParse(match.Groups[1].Value, out double energy))
            {
                int frameNumber = int.Parse(Regex.Match(Path.GetFileNameWithoutExtension(filePath), @"\d+").Value);
                return (frameNumber, energy);
            }
            throw new FormatException($"Unable to extract energy value from file {filePath}");
        }
        public async Task ConvertLowestEnergyToXYZAsync(string outputDirectory, string lowestEnergyFrame, string projectPath, string projectName)
        {
            ProgressReported?.Invoke($"Starting conversion of lowest energy configuration {lowestEnergyFrame} to XYZ format...");
            string mopFilePath = Path.Combine(outputDirectory, $"{lowestEnergyFrame}.mop");
            string xyzFilePath = Path.Combine(projectPath, $"{projectName}-minimize.xyz");
            string[] mopLines = await File.ReadAllLinesAsync(mopFilePath);
            var atomLines = mopLines.Skip(3).Where(line => !string.IsNullOrWhiteSpace(line)).ToList();
            using (StreamWriter writer = new StreamWriter(xyzFilePath))
            {
                await writer.WriteLineAsync(atomLines.Count.ToString());
                await writer.WriteLineAsync($"generated by EasySpecCalc,{lowestEnergyFrame}");
                foreach (var line in atomLines)
                {
                    var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 7)
                    {
                        string atom = parts[0];
                        double x = double.Parse(parts[1]);
                        double y = double.Parse(parts[3]);
                        double z = double.Parse(parts[5]);
                        await writer.WriteLineAsync($"{atom} {x:F6} {y:F6} {z:F6}");
                    }
                }
            }
            ProgressReported?.Invoke($"XYZ file saved to: {xyzFilePath}");
        }
    }
    public class ORCA
    {
        public event Action<string> ProgressReported;
        public event Action<string> OutputReceived;
        public event Func<string, Task<bool>> UserConfirmationRequired;

        private readonly string workingDirectory;
        private readonly string projectName;

        public ORCA(string workingDirectory, string projectName)
        {
            this.workingDirectory = workingDirectory;
            this.projectName = projectName;
        }

        private string GetFilePath(string suffix) => Path.Combine(workingDirectory, $"{projectName}-{suffix}");

        private string XYZFilePath => GetFilePath("minimize.xyz");
        private string GroundStateInputPath => GetFilePath("GroundState.inp");
        private string GroundStateOutputPath => GetFilePath("GroundState.out");
        private string GroundStateXYZPath => GetFilePath("GroundState.xyz");
        private string ExcitationStateInputPath => GetFilePath("ExcitationState.inp");
        private string ExcitationStateOutputPath => GetFilePath("ExcitationState.out");
        private string ExcitationStateXYZPath => GetFilePath("ExcitationState.xyz");
        private string ExcitationStateCCSDInputPath => GetFilePath("ExcitationState-CCSD.inp");
        private string GroundStateFolderPath => GetFilePath("GroundState");
        private string ExcitationStateFolderPath => GetFilePath("ExcitationState");
        private string ExcitationStateCCSDFolderPath => GetFilePath("ExcitationState-CCSD");

        public async Task RunCalculationsAsync()
        {
            await RunGroundStateCalculationAsync();
            await RunExcitationStateCalculationAsync();
            await RunExcitationStateCCSDCalculationAsync();
        }

        private async Task RunGroundStateCalculationAsync()
        {
            ProgressReported?.Invoke("Starting ORCA ground state calculation process...");

            await ConvertXYZToORCAInputAsync(XYZFilePath, GroundStateInputPath, isGroundState: true);
            await RunORCAAsync(GroundStateInputPath, GroundStateOutputPath);

            if (!await CheckFrequenciesAsync(GroundStateOutputPath)) return;

            await GenerateExcitationStateInputAsync();
            MoveFilesToFolder(GroundStateFolderPath, "*-GroundState*", "*-GroundState.xyz", "*-GroundState.out");

            ProgressReported?.Invoke("ORCA ground state calculation process completed.");
        }

        private async Task RunExcitationStateCalculationAsync()
        {
            ProgressReported?.Invoke("Starting ORCA excited state calculation process...");

            await RunORCAAsync(ExcitationStateInputPath, ExcitationStateOutputPath);

            if (!await CheckFrequenciesAsync(ExcitationStateOutputPath)) return;

            await GenerateExcitationStateCCSDInputAsync();
            MoveFilesToFolder(ExcitationStateFolderPath, "*-ExcitationState*", "*ExcitationState-CCSD*", "*-ExcitationState.xyz", "*-ExcitationState.out");

            ProgressReported?.Invoke("ORCA excited state calculation process completed.");
        }

        private async Task RunExcitationStateCCSDCalculationAsync()
        {
            ProgressReported?.Invoke("Starting ORCA CCSD excited state calculation process...");

            string ccsdOutputPath = GetFilePath("ExcitationState-CCSD.out");
            await RunORCAAsync(ExcitationStateCCSDInputPath, ccsdOutputPath);

            MoveFilesToFolder(ExcitationStateCCSDFolderPath, "*-ExcitationState-CCSD*", "*-ExcitationState-CCSD.out");

            ProgressReported?.Invoke("ORCA CCSD excited state calculation process completed.");
        }

        private async Task ConvertXYZToORCAInputAsync(string xyzPath, string outputPath, bool isGroundState)
        {
            ProgressReported?.Invoke($"Starting XYZ to ORCA input file conversion: {xyzPath}");

            string xyzContent = await File.ReadAllTextAsync(xyzPath);
            string[] lines = xyzContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length < 3)
                throw new FormatException("Invalid XYZ file format");

            int atomCount = int.Parse(lines[0]);
            List<string> atomLines = lines.Skip(2).Take(atomCount).ToList();

            string orcaInput = isGroundState
                ? GenerateGroundStateInput(atomLines)
                : GenerateExcitationStateInput(atomLines);

            await File.WriteAllTextAsync(outputPath, orcaInput);
            ProgressReported?.Invoke($"ORCA input file saved to: {outputPath}");
        }

        private string GenerateGroundStateInput(List<string> atomLines)
        {
            MoveFilesToFolder($"{projectName}", "*.xyz");
            return $@"! r2SCAN-3c opt {Config.GroundStateFreq}defgrid3 noautostart miniprint nopop
%maxcore {Config.Memory}
%pal nprocs  {Config.CoreCount} end
%cpcm
smd true
SMDsolvent ""water""
end
* xyz   {Config.ChargeQuantity}   1
{string.Join(Environment.NewLine, atomLines)}
*";
        }

        private string GenerateExcitationStateInput(List<string> atomLines)
        {
            return $@"! PBE0 def2-SV(P) def2/J RIJCOSX tightSCF opt {Config.ExcitedStateFreq}defgrid3 def2-SVP/C noautostart miniprint nopop
%maxcore {Config.Memory}
%pal nprocs {Config.CoreCount} end
%cpcm
smd true
SMDsolvent ""water""
end
%tddft
nroots 10
TDA false
end
* xyz {Config.ChargeQuantity} 1
{string.Join(Environment.NewLine, atomLines)}
*";
        }

        private async Task RunORCAAsync(string inputFilePath, string outputFilePath)
        {
            ProgressReported?.Invoke($"Starting ORCA calculation: {inputFilePath}");

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = Config.OrcaPath,
                    Arguments = $"\"{inputFilePath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            using var outputFileStream = new StreamWriter(outputFilePath, false);

            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    outputFileStream.WriteLine(e.Data);
                    OutputReceived?.Invoke(e.Data);
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    outputFileStream.WriteLine($"Error: {e.Data}");
                    OutputReceived?.Invoke($"Error: {e.Data}");
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync();

            ProgressReported?.Invoke($"ORCA calculation completed, output file saved to: {outputFilePath}");
        }

        private async Task<bool> CheckFrequenciesAsync(string outputFilePath)
        {
            ProgressReported?.Invoke("Checking vibrational frequencies...");

            using var reader = new StreamReader(outputFilePath);
            string line;
            bool foundFrequencySection = false;
            List<float> frequencies = new List<float>();

            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (line.Contains("VIBRATIONAL FREQUENCIES"))
                {
                    foundFrequencySection = true;
                    await reader.ReadLineAsync();
                    await reader.ReadLineAsync();
                    continue;
                }

                if (foundFrequencySection)
                {
                    var match = Regex.Match(line, @"^\s*\d+:\s+([-]?\d+\.\d+)\s+cm\*\*-1");
                    if (match.Success)
                    {
                        float frequency = float.Parse(match.Groups[1].Value);
                        frequencies.Add(frequency);
                    }
                    else if (line.Trim() == "")
                    {
                        break;
                    }
                }
            }

            var negativeFrequencies = frequencies.Where(f => f < 0).ToList();

            if (negativeFrequencies.Any())
            {
                string message = $"Found {negativeFrequencies.Count} negative frequencies. Minimum frequency is {negativeFrequencies.Min():F2} cm**-1. Continue processing?";
                return await UserConfirmationRequired?.Invoke(message);
            }

            ProgressReported?.Invoke("No negative frequencies found. Continuing processing.");
            return true;
        }

        private async Task GenerateExcitationStateInputAsync()
        {
            await ConvertXYZToORCAInputAsync(GroundStateXYZPath, ExcitationStateInputPath, isGroundState: false);
        }

        private async Task GenerateExcitationStateCCSDInputAsync()
        {
            ProgressReported?.Invoke("Starting to generate CCSD excited state ORCA input file...");

            string xyzContent = await File.ReadAllTextAsync(ExcitationStateXYZPath);
            string[] lines = xyzContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length < 3)
                throw new FormatException("Invalid excited state XYZ file format");

            int atomCount = int.Parse(lines[0]);
            List<string> atomLines = lines.Skip(2).Take(atomCount).ToList();

            string orcaInput = $@"! STEOM-DLPNO-CCSD RIJK def2-TZVP def2/JK def2-TZVP/C tightSCF noautostart nopop
%maxcore {Config.Memory}
%pal nprocs {Config.CoreCount} end
%cpcm
smd true
SMDsolvent ""water""
end
%mdci
nroots 5
end
* xyz {Config.ChargeQuantity} 1
{string.Join(Environment.NewLine, atomLines)}
*";

            await File.WriteAllTextAsync(ExcitationStateCCSDInputPath, orcaInput);
            ProgressReported?.Invoke($"ORCA CCSD excited state calculation input file saved to: {ExcitationStateCCSDInputPath}");
        }

        private void MoveFilesToFolder(string folderPath, string includePattern, params string[] excludePatterns)
        {
            ProgressReported?.Invoke($"Starting to move files to {folderPath}...");
            Directory.CreateDirectory(folderPath);
            var filesToMove = Directory.GetFiles(workingDirectory, includePattern).ToList();

            if (excludePatterns != null && excludePatterns.Length > 0)
            {
                var excludeFiles = new List<string>();
                foreach (var pattern in excludePatterns)
                {
                    excludeFiles.AddRange(Directory.GetFiles(workingDirectory, pattern));
                }
                filesToMove = filesToMove.Except(excludeFiles).ToList();
            }

            foreach (string file in filesToMove)
            {
                string fileName = Path.GetFileName(file);
                string destFile = Path.Combine(folderPath, fileName);
                File.Move(file, destFile, true);
            }

            ProgressReported?.Invoke($"All relevant files have been moved to {folderPath}");
        }
    }
}