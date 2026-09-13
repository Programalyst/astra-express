#!/usr/bin/env python3
"""Compile and run fleet/fuel checks against the project's actual simulation and terrain."""
import argparse
from pathlib import Path
import subprocess
import tempfile

parser = argparse.ArgumentParser()
parser.add_argument("project", nargs="?", type=Path, default=Path(__file__).resolve().parent.parent)
parser.add_argument("--unity", type=Path, default=Path("/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app"))
args = parser.parse_args()
mono_root = args.unity / "Contents/Resources/Scripting/MonoBleedingEdge"
mono = mono_root / "bin/mono"
compiler = mono_root / "lib/mono/4.5/csc.exe"
with tempfile.TemporaryDirectory(prefix="astra-simulation-checks-") as directory:
    output = Path(directory) / "SimulationMergeChecks.exe"
    subprocess.run([
        str(mono), str(compiler), "-nologo", "-langversion:latest", "-out:" + str(output),
        str(args.project / "Assets/Scripts/AstraExpress/ColonySimulation.cs"),
        str(args.project / "Assets/Scripts/AstraExpress/TerrainGrid.cs"),
        str(Path(__file__).resolve().parent / "SimulationMergeChecks.cs"),
    ], check=True)
    subprocess.run([str(mono), str(output)], check=True)
