#!/usr/bin/env python3
"""Run the actual exploration coroutine against actual simulation/terrain, with a clock/UI shim."""
import argparse
from pathlib import Path
import subprocess
import tempfile

parser = argparse.ArgumentParser()
parser.add_argument('project', nargs='?', type=Path, default=Path(__file__).resolve().parent.parent)
parser.add_argument('--unity', type=Path, default=Path('/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app'))
args = parser.parse_args()
runtime = args.unity / 'Contents/Resources/Scripting/MonoBleedingEdge'
with tempfile.TemporaryDirectory(prefix='astra-exploration-checks-') as directory:
    output = Path(directory) / 'ExplorationChecks.exe'
    sources = [args.project / 'Assets/Scripts/AstraExpress' / name for name in
               ['ColonySimulation.cs', 'TerrainGrid.cs', 'AstraBotExploration.cs']]
    subprocess.run([str(runtime / 'bin/mono'), str(runtime / 'lib/mono/4.5/csc.exe'),
                    '-nologo', '-langversion:latest', '-out:' + str(output),
                    *map(str, sources), str(Path(__file__).with_name('ExplorationChecks.cs'))], check=True)
    subprocess.run([str(runtime / 'bin/mono'), str(output)], check=True)
