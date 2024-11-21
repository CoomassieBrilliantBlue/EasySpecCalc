# EasySpecCalc
![](https://img.shields.io/github/stars/CoomassieBrilliantBlue/EasySpecCalc.svg) ![](https://img.shields.io/github/forks/CoomassieBrilliantBlue/EasySpecCalc.svg) ![](https://img.shields.io/github/tag/CoomassieBrilliantBlue/EasySpecCalc.svg) ![](https://img.shields.io/github/release/CoomassieBrilliantBlue/EasySpecCalc.svg) ![](https://img.shields.io/github/issues/CoomassieBrilliantBlue/EasySpecCalc.svg)
### Version: 0.0.1

EasySpecCalc is a simple spectral calculation tool,  designed to assist researchers in spectral analysis.

<p align="center">
  <img src="./static/{B14724AD-6824-4C06-B9F3-3018E3377C0F}.png" alt="main UI" width="50%">
</p>

<p align="center">
  <img src="./static/{9648A9A8-49CE-4F67-B6A3-7D3BB2F86601}.png" alt="setting" width="50%">
</p>
The program provides a fully automated workflow from molecular formula to spectrum generation, with a wide range of customizable settings.

## Requirements

**For Conformer Search functionality:**
- Requires WSL2
- Use conda to install AmberTools23, and ensure the environment name is also set to `AmberTools23`.

**For Lowest Energy Search functionality:**
- Requires Mopac installation.

**For Ground and Excited States calculations:**
- Requires ORCA installation.

---

**Author**: Simon Zhu

## To-do

- Add intensity correction functionality.
- Add cross-platform support (~~Initially intended to be cross-platform, but UI and Core code became tightly coupled during development~~).
- Support more input formats.
- Add visualization capabilities for the results.
- Incorporate molecular dynamics support.

## Completed

- Built 3D structures from SMILES.
- Implemented Conformer Search and Lowest Energy Search.

