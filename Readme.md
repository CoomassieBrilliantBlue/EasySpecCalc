# EasySpecCalc

![GitHub Stars](https://img.shields.io/github/stars/CoomassieBrilliantBlue/EasySpecCalc.svg) ![GitHub Forks](https://img.shields.io/github/forks/CoomassieBrilliantBlue/EasySpecCalc.svg) ![GitHub Tag](https://img.shields.io/github/tag/CoomassieBrilliantBlue/EasySpecCalc.svg) ![GitHub Release](https://img.shields.io/github/release/CoomassieBrilliantBlue/EasySpecCalc.svg) ![GitHub Issues](https://img.shields.io/github/issues/CoomassieBrilliantBlue/EasySpecCalc.svg)

### Version: 0.0.1

**EasySpecCalc** is a simple spectral calculation tool designed to assist researchers in spectral analysis.

### Main UI
<div align="center">
  <img src="./static/{B14724AD-6824-4C06-B9F3-3018E3377C0F}.png" alt="main UI" width="50%">
</div>

### Settings Interface
<div align="center">
  <img src="./static/{9648A9A8-49CE-4F67-B6A3-7D3BB2F86601}.png" alt="setting" width="25%">
</div>

The program provides a fully automated workflow from molecular formula to spectrum generation, with a wide range of customizable settings.

## Requirements

**For Conformer Search functionality:**
- **Steps to set up WSL2 on Windows:**
  1. **Enable WSL2:** Open PowerShell as Administrator and run the following command:
     ```sh
     dism.exe /online /enable-feature /featurename:Microsoft-Windows-Subsystem-Linux /all /norestart
     dism.exe /online /enable-feature /featurename:VirtualMachinePlatform /all /norestart
     ```
  2. **Set WSL2 as the default version:**
     ```sh
     wsl --set-default-version 2
     ```
  3. **Install a Linux Distribution:** Go to the Microsoft Store and install a Linux distribution of your choice (e.g., Ubuntu).
  4. **Install AmberTools23 using Conda:** After installing the Linux distribution, open it and run:
     ```sh
     conda create -n AmberTools23 -c conda-forge ambertools=23
     conda activate AmberTools23
     ```

**For Lowest Energy Search functionality:**
- Requires Mopac installation.

**For Ground and Excited States calculations:**
- Requires ORCA installation.


## To-do

- [ ] **Add intensity correction functionality**
- [ ] **Add cross-platform support**:（~~Originally intended to be cross-platform, but UI and core code became tightly coupled during development~~）
- [ ] **Support more input formats**
- [ ] **Add visualization capabilities**
- [ ] **Incorporate molecular dynamics support**

## Completed

  ✅ **Built 3D structures from SMILES**<br />
  ✅ **Implemented Conformer Search and Lowest Energy Search**

**Author**: Simon Zhu
