
[![Unity Tests](https://github.com/tavisit/MasterThesis/actions/workflows/unity-tests.yml/badge.svg)](https://github.com/tavisit/MasterThesis/actions/workflows/unity-tests.yml)
[![Format Check](https://github.com/tavisit/MasterThesis/actions/workflows/format-check.yml/badge.svg)](https://github.com/tavisit/MasterThesis/actions/workflows/format-check.yml)
[![Export Unity Package](https://github.com/tavisit/MasterThesis/actions/workflows/export-package.yml/badge.svg)](https://github.com/tavisit/MasterThesis/actions/workflows/export-package.yml)

# Master Thesis — Procedural city generation (Unity)

**Constraint-based procedural city layout generation** using **Wave Function Collapse** in Unity (C#): street networks plus multi-layer transport (**streets**, **railways**, **metro**), with layouts that can respect **terrain** and **island-style** boundaries. Supports **grid** (planned-city style) and **organic** morphologies; tiles are solved with WFC, then **splines and meshes** approximate smooth, terrain-aware routes.

- **Author:** Octavian-Mihai Matei · Máster Universitario en Diseño y Programación de Videojuegos (UOC)  
- **PAC3 demo:** [video](https://youtu.be/ST6w6_5fg2k) · [release v0.3.0-pac3](https://github.com/tavisit/MasterThesis/releases/tag/v0.3.0-pac3)

## Repository layout

| Path | Contents |
|------|----------|
| `Code/` | Unity project (open this folder in Unity) |
| `Code/Assets/PCG/` | PCG module: scripts, scenes, shaders |
| `Documentation/` | Thesis PDFs and related documents |

Unity **package dependencies** for the PCG assets are listed in [Code/Assets/PCG/README.md](Code/Assets/PCG/README.md).

## License

- **Source code** in this repository: [GNU General Public License v3.0](LICENSE) unless noted otherwise.  
- **Thesis text** (PDF): Creative Commons **BY-NC-SA 3.0 España** — see the copyright page in the PDF.  
- **Third-party art/assets** (stores, Poly Haven, etc.) keep their original licenses; attributions are detailed in the thesis memorandum.
