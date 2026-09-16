using System;
using System.Linq;
using AstraExpress;

static class DefenseChecks
{
    private static int checks;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("Defense: " + message);
        checks++;
    }
    private static void Advance(ColonySimulation simulation, float seconds)
    {
        for (int step = 0; step < (int)Math.Ceiling(seconds / 0.05f); step++)
        {
            simulation.Step(0.05f);
            if (simulation.Battery < 0 || simulation.Battery > 100.001f || simulation.AccountedOre != simulation.Produced || simulation.AccountedFuel != simulation.FuelProduced)
                throw new Exception("Defense broke economy conservation");
        }
    }
    private static ColonySimulation Game()
    {
        var simulation = new ColonySimulation();
        simulation.Reveal(16, 16, 100);
        return simulation;
    }
    private static Structure Build(ColonySimulation simulation, StructureKind kind, Cell origin)
    {
        Check(simulation.Build(kind, origin), simulation.Message);
        return simulation.StructureAt(origin);
    }
    private static void Connect(ColonySimulation simulation, Structure building, bool rail = false)
    {
        var route = simulation.FindPath(simulation.Colony.Port, building.Port, cell => simulation.StructureAt(cell) == null);
        Check(route != null && simulation.Lay(route, rail), "Connect " + building.Kind + ": " + simulation.Message);
    }
    public static void Run()
    {
        var peaceful = Game();
        var small = Build(peaceful, StructureKind.Extractor, new Cell(10, 4));
        Connect(peaceful, small);
        Advance(peaceful, 120);
        Check(peaceful.Produced > 0 && !peaceful.RaidsStarted && peaceful.Aliens.Count == 0, "Small mining and exploration do not trigger raids");
        foreach (var site in new[] { new Cell(4, 23), new Cell(25, 25) })
        {
            var simulation = Game();
            var mine = Build(simulation, StructureKind.Extractor, site);
            Advance(simulation, 25);
            Check(!simulation.RaidsStarted, "Unpowered 2x2 construction does not trigger raids");
            mine.Paused = true;
            Connect(simulation, mine);
            Advance(simulation, 25);
            Check(!simulation.RaidsStarted, "Paused 2x2 mine does not trigger raids");
            mine.Paused = false;
            Advance(simulation, 0.25f);
            Check(!simulation.RaidsStarted, "No trigger before actual production");
            Advance(simulation, 1);
            Check(simulation.RaidsStarted && simulation.WaveNumber == 0 && simulation.NextWaveIn > 19, "Either large ore patch awakens raids on first produced unit");
            mine.Paused = true;
            simulation.Paused = true;
            float countdown = simulation.NextWaveIn;
            Advance(simulation, 30);
            Check(simulation.NextWaveIn == countdown && simulation.Aliens.Count == 0, "Game pause freezes wave timer");
            simulation.Paused = false;
            for (int step = 0; step < 500 && simulation.WaveNumber == 0; step++) simulation.Step(0.05f);
            Check(simulation.Aliens.Count == 1 && simulation.Aliens.All(alien => Math.Abs(alien.X) + Math.Abs(alien.Y - 31) < 0.1f), "First wave enters at northwest corner after warning");
            Advance(simulation, 2);
            Check(simulation.Aliens.Count == 2 && Math.Abs(simulation.Aliens[0].X - simulation.Aliens[1].X) + Math.Abs(simulation.Aliens[0].Y - simulation.Aliens[1].Y) > 0.5f, "Wave arrivals are staggered so aliens do not spawn as one overlapping model");
            Check(simulation.Aliens.All(alien => alien.Target != null && simulation.Terrain.Walkable(new Cell((int)Math.Round(alien.X), (int)Math.Round(alien.Y)))), "Aliens select reachable buildings and walk on terrain");
            Advance(simulation, 100);
            Check(simulation.WaveNumber >= 3 && simulation.Aliens.Count <= ColonySimulation.MaxAliens, "Waves continue after mining pauses and respect live cap");
        }
        var assault = Game();
        var solar = assault.Structures.First(building => building.Kind == StructureKind.Solar);
        var attacker = new Alien { Id = 1, X = 1, Y = 7 };
        assault.Aliens.Add(attacker);
        Advance(assault, 0.05f);
        Check(attacker.Target == solar && solar.Health == 94, "Alien attacks nearest reachable building");
        Check(assault.Repair(solar), "Damaged building can start repairs");
        Advance(assault, 1.1f);
        Check(solar.RepairRemaining == 0 && solar.Health < 94, "Further damage interrupts repairs");
        Advance(assault, 18);
        Check(solar.Disabled && assault.SolarGeneration == 0 && assault.Structures.Contains(solar), "Defeated solar is disabled, not deleted, and stops generation");
        Check(attacker.Target != solar, "Alien retargets after disabling a building");
        assault.Aliens.Clear();
        int credits = assault.Credits;
        Check(assault.Repair(solar) && !assault.Repair(solar), "Repair starts once");
        assault.Paused = true;
        Advance(assault, 10);
        Check(solar.RepairRemaining == 10 && solar.Disabled, "Pause freezes repair");
        assault.Paused = false;
        Advance(assault, 10.1f);
        Check(solar.Health == 100 && assault.SolarGeneration == 2 && assault.Credits == credits, "Free repair restores generation");
        var defense = Game();
        int creditsBeforeTurret = defense.Credits;
        var turret = Build(defense, StructureKind.Turret, new Cell(1, 4));
        Check(ColonySimulation.TurretCost == 100 && defense.Credits == creditsBeforeTurret - 100, "Laser turret costs exactly 100 credits");
        var enemy = new Alien { Id = 2, X = 1, Y = 5 };
        defense.Aliens.Add(enemy);
        Advance(defense, 0.1f);
        Check(enemy.Health == 40 && defense.LaserShots.Count == 0, "Unwired turret cannot fire");
        Connect(defense, turret);
        float battery = defense.Battery;
        Advance(defense, 0.05f);
        Check(enemy.Health == 25 && defense.LaserShots.Count == 1 && defense.Battery <= battery - 1.8f, "Powered turret spends battery on laser hit");
        Advance(defense, 2);
        Check(defense.Aliens.Count == 0 && defense.AliensDefeated == 1 && defense.Trains.Count == 0, "Automated turret kills once and creates no train");
        turret.Health = 0;
        defense.Aliens.Add(new Alien { Id = 3, X = 1, Y = 5 });
        Advance(defense, 1);
        Check(defense.Aliens[0].Health == 40, "Disabled turret cannot fire");
        turret.Health = 100;
        defense.Structures.First(building => building.Kind == StructureKind.Solar).Health = 0;
        defense.Reconnect();
        typeof(ColonySimulation).GetProperty("Battery").SetValue(defense, 0f);
        Advance(defense, 1);
        Check(defense.Aliens[0].Health == 40 && defense.Battery == 0, "Empty battery prevents shots without going negative");
        var visibility = Game();
        var cliffTurret = Build(visibility, StructureKind.Turret, new Cell(16, 7));
        Check(!visibility.CanShoot(cliffTurret, new Alien { X = 22, Y = 7 }), "Cliff blocks low-to-high shot");
        Check(visibility.CanShoot(cliffTurret, new Alien { X = 14, Y = 7 }), "Clear same-level shot allowed");
        Check(visibility.CanShoot(cliffTurret, new Alien { X = 9, Y = 7 }), "Alien exactly seven tiles away is in range");
        Check(!visibility.CanShoot(cliffTurret, new Alien { X = 8.99f, Y = 7 }), "Alien beyond seven tiles is out of range");
        Connect(visibility, cliffTurret);
        var edgeTarget = new Alien { Id = 10, X = 9, Y = 7 };
        visibility.Aliens.Add(edgeTarget);
        Advance(visibility, 0.05f);
        Check(edgeTarget.Health == 25, "Laser applies damage immediately at the full seven-tile range");
        Check(!visibility.CanShoot(cliffTurret, new Alien { X = 5, Y = 7 }), "Range enforced");
        visibility.Revealed[14, 7] = false;
        Check(!visibility.CanShoot(cliffTurret, new Alien { X = 14, Y = 7 }), "Fog conceals alien from turret");
        Check(!visibility.Build(StructureKind.Turret, ColonySimulation.AlienSpawn), "Entry cannot be blocked");
        visibility.Aliens.Add(new Alien { X = 12, Y = 10 });
        Check(!visibility.Build(StructureKind.Turret, new Cell(12, 10)), "Buildings cannot overlap aliens");
        var plateau = Game();
        var plateauMine = Build(plateau, StructureKind.Extractor, new Cell(4, 23));
        var climber = new Alien { Id = 4, X = 4, Y = 17 };
        plateau.Aliens.Add(climber);
        bool usedRamp = false;
        for (int step = 0; step < 240; step++)
        {
            plateau.Step(0.05f);
            var cell = new Cell((int)Math.Round(climber.X), (int)Math.Round(climber.Y));
            usedRamp |= plateau.Terrain.Kind(cell) == TerrainKind.Ramp;
            Check(plateau.Terrain.Walkable(cell), "Climber avoids impassable terrain");
        }
        Check(usedRamp && plateauMine.Health < 100, "Alien climbs ramp and attacks plateau mine");
        var shutdown = Game();
        var shutdownMine = Build(shutdown, StructureKind.Extractor, new Cell(10, 4));
        Connect(shutdown, shutdownMine);
        Connect(shutdown, shutdownMine, true);
        Advance(shutdown, 15);
        shutdownMine.Health = shutdown.Colony.Health = 0;
        int produced = shutdown.Produced, sold = shutdown.Sold;
        Advance(shutdown, 30);
        Check(shutdown.Produced == produced && shutdown.Sold == sold && shutdown.Train.Owner == shutdownMine, "Disabled mine and colony halt work but preserve freight and ownership");
        Check(shutdown.Repair(shutdownMine) && shutdown.Repair(shutdown.Colony), "Mine and colony repair");
        Advance(shutdown, 40);
        Check(shutdown.Produced > produced && shutdown.Sold > sold && shutdown.Trains.Count == 1, "Repair resumes service without duplicate train");
        var reset = new ColonySimulation();
        Check(!reset.RaidsStarted && reset.Aliens.Count == 0 && reset.WaveNumber == 0 && reset.LaserShots.Count == 0, "New game clears combat state");
        Console.WriteLine("PASS defenseAssertions=" + checks);
    }
}
