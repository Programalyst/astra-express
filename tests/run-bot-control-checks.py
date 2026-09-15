#!/usr/bin/env python3
"""Run the actual bot coroutine and placement methods in a small Unity frame shim."""
import argparse
from pathlib import Path
import subprocess
import tempfile

parser = argparse.ArgumentParser()
parser.add_argument("project", nargs="?", type=Path, default=Path(__file__).resolve().parent.parent)
parser.add_argument("--control", type=Path, help="Optional staged AstraBotControl.cs")
parser.add_argument("--unity", type=Path, default=Path("/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app"))
args = parser.parse_args()
scripts = args.project / "Assets/Scripts/AstraExpress"
control = (args.control or scripts / "AstraBotControl.cs").read_text()
game = (scripts / "AstraGame.cs").read_text()

def method(source, signature):
    start = source.index(signature)
    opening = source.index("{", start)
    depth = 1
    end = opening + 1
    while depth:
        depth += (source[end] == "{") - (source[end] == "}")
        end += 1
    return source[start:end]

# DrawBotTarget only renders UI. The rest of the adapter is compiled unchanged.
control = control[:control.index("        private void DrawBotTarget()")] + "    }\n}\n"
placement = method(game, "private void PlaceNetworkAt(Cell target)")
if "TryPlanNetworkRoute" not in placement:
    raise SystemExit("Install the shared manual placement implementation before running these checks.")
helpers = []
if "NetworkEndpoint(" in placement:
    helper_source = game if "private Cell NetworkEndpoint(" in game else (scripts / "AstraNetworkPlacement.cs").read_text()
    helpers.append(method(helper_source, "private Cell NetworkEndpoint("))
shim = "using System; using System.Collections.Generic; using System.Linq; namespace AstraExpress { public sealed partial class AstraGame {\n" + "\n".join(helpers + [placement]) + "\n} }"
mono_root = args.unity / "Contents/Resources/Scripting/MonoBleedingEdge"
mono = mono_root / "bin/mono"
with tempfile.TemporaryDirectory(prefix="astra-bot-control-") as directory:
    work = Path(directory)
    (work / "Control.cs").write_text(control)
    (work / "Placement.cs").write_text(shim)
    output = work / "BotControlChecks.exe"
    subprocess.run([str(mono), str(mono_root / "lib/mono/4.5/csc.exe"), "-nologo", "-langversion:latest", "-out:" + str(output),
        str(scripts / "ColonySimulation.cs"), str(scripts / "ColonyDefense.cs"), str(scripts / "TerrainGrid.cs"), str(work / "Control.cs"), str(work / "Placement.cs"),
        str(Path(__file__).with_name("BotControlChecks.cs"))], check=True)
    subprocess.run([str(mono), str(output)], check=True)
