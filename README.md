
[![Unity Tests](https://github.com/tavisit/MasterThesis/actions/workflows/unity-tests.yml/badge.svg)](https://github.com/tavisit/MasterThesis/actions/workflows/unity-tests.yml)
[![Format Check](https://github.com/tavisit/MasterThesis/actions/workflows/format-check.yml/badge.svg)](https://github.com/tavisit/MasterThesis/actions/workflows/format-check.yml)
[![Export Unity Package](https://github.com/tavisit/MasterThesis/actions/workflows/export-package.yml/badge.svg)](https://github.com/tavisit/MasterThesis/actions/workflows/export-package.yml)

# Master Thesis — Procedural city generation (Unity)

Constraint-based procedural city layout generation using Wave Function Collapse in Unity (C#): street networks plus multi-layer transport (streets, railways, metro), with layouts that can respect terrain and island-style boundaries. Supports grid (planned-city style) and organic morphologies; tiles are solved with WFC, then splines and meshes approximate smooth, terrain-aware routes.

- **Author:** Octavian-Mihai Matei · Máster Universitario en Diseño y Programación de Videojuegos (UOC)
- **Final release:** [v1.0.0](https://github.com/tavisit/MasterThesis/releases/tag/v1.0.0) (thesis submission)
- **PAC3 demo:** [video](https://youtu.be/ST6w6_5fg2k) · [v0.3.0-pac3](https://github.com/tavisit/MasterThesis/releases/tag/v0.3.0-pac3)

## How to run

1. Open the `Code/` folder in **Unity 6.3 LTS** (`6000.3.6f1`).
2. Open `Code/Assets/PCG/Scenes/DemoScene.unity`.
3. Press Play.

Unity package dependencies for the PCG module are listed in [Code/Assets/PCG/README.md](Code/Assets/PCG/README.md).

## Repository layout

| Path | Contents |
|------|----------|
| `Code/` | Unity project (open this folder in Unity) |
| `Code/Assets/PCG/` | PCG module: scripts, scenes, shaders |

## License

- **Source code** in this repository: [GNU General Public License v3.0](LICENSE) unless noted otherwise.
- **Thesis text** (memorandum): submitted to UOC; not hosted in this repository. Licensed **BY-NC-SA 3.0 España** (see the copyright page in the submitted PDF).
- **Third-party art/assets** (stores, Poly Haven, etc.) keep their original licenses; attributions are detailed in the thesis memorandum.
