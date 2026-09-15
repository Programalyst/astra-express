using System;
using System.Linq;
using AstraExpress;
using UnityEditor;
using UnityEngine;

public static class AstraDefensePlaytest
{
    [MenuItem("Astra Express/Defense playtest (fresh Play Mode only)")]
    public static void Prepare()
    {
        var game = UnityEngine.Object.FindFirstObjectByType<AstraGame>();
        if (!EditorApplication.isPlaying || game == null || game.Simulation == null)
            throw new InvalidOperationException("Enter Play Mode and wait for the colony to initialize first.");
        var simulation = game.Simulation;
        if (simulation.Structures.Count != 2 || simulation.Trains.Count != 0 || simulation.RaidsStarted || simulation.Credits != 500)
            throw new InvalidOperationException("This fixture only runs on a fresh, untouched Play Mode colony; existing player progress is not reset.");
        simulation.Reveal(16, 16, 100);
        var mineSite = new Cell(4, 23);
        if (!simulation.Build(StructureKind.Extractor, mineSite)) throw new InvalidOperationException(simulation.Message);
        Connect(simulation, simulation.StructureAt(mineSite));
        var turretSite = new Cell(6, 23);
        if (!simulation.Build(StructureKind.Turret, turretSite)) throw new InvalidOperationException(simulation.Message);
        Connect(simulation, simulation.StructureAt(turretSite));
        simulation.Paused = false;
        for (int step = 0; step < 3600 && simulation.LaserShots.Count == 0; step++) simulation.Step(0.05f);
        simulation.Paused = true;
        game.CoachFocus("5,23");
        if (simulation.LaserShots.Count == 0) throw new InvalidOperationException("No live laser shot reached the playtest fixture within 180 simulation seconds.");
        var shot = simulation.LaserShots.First();
        Debug.Log($"DEFENSE_PLAYTEST wave={simulation.WaveNumber}; aliens={simulation.Aliens.Count}; credits={simulation.Credits}; battery={simulation.Battery:0.0}; shot=({shot.TargetX:0.0},{shot.TargetY:0.0}). Revealed-map fixture; normal mining, spawning, pathing and shooting rules. Resume to watch defense, then exit Play Mode to discard.");
    }

    private static void Connect(ColonySimulation simulation, Structure building)
    {
        var route = simulation.FindPath(simulation.Colony.Port, building.Port, cell => simulation.StructureAt(cell) == null);
        if (route == null || !simulation.Lay(route, false)) throw new InvalidOperationException(simulation.Message);
    }
}
