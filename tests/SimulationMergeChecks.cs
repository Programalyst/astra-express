using System;
using System.Linq;
using AstraExpress;

class Review
{
    static int scenarioChecks;
    static int invariantChecks;
    static int timeSteps;
    static void Check(bool value, string label) { if (!value) throw new Exception(label); scenarioChecks++; }
    static void Invariant(bool value, string label) { if (!value) throw new Exception(label); invariantChecks++; }
    static void Advance(ColonySimulation sim, float seconds) { for (int i=0;i<(int)(seconds*4);i++) { sim.Step(.25f); timeSteps++; Invariant(sim.Produced==sim.AccountedOre,"Ore conservation"); Invariant(sim.FuelProduced==sim.AccountedFuel,"Fuel conservation"); Invariant(sim.Battery>=0 && sim.Battery<=100.001f,"Battery bounds"); Invariant(sim.Trains.All(t=>t.Cargo>=0&&t.Cargo<=t.Capacity),"Cargo bounds"); } }
    static void Link(ColonySimulation sim, Structure structure, bool rails) { var path=sim.FindPath(sim.Colony.Port,structure.Port,c=>sim.StructureAt(c)==null); Check(sim.Lay(path,rails),"Lay route: "+sim.Message); }
    static Structure Build(ColonySimulation sim,StructureKind kind,Cell cell) { Check(sim.Build(kind,cell),"Build: "+sim.Message); return sim.StructureAt(cell); }
    static void Reach(ColonySimulation sim, Cell destination)
    {
        Check(sim.OrderRover(destination), "Terrain rover order to " + destination);
        for (int i = 0; i < 400 && sim.RoverMoving; i++)
        {
            Advance(sim, .25f);
            Invariant(sim.Terrain.Walkable(sim.RoverCell), "Rover stays on walkable terrain");
        }
        Check(!sim.RoverMoving && sim.RoverCell.Equals(destination), "Rover reaches " + destination);
    }

    static void TerrainChecks(ColonySimulation economy)
    {
        int before = scenarioChecks;
        var sim = new ColonySimulation(enableRaids: false);
        sim.Reveal(14, 11, 100);
        var terrain = sim.Terrain;
        foreach (var bounds in new[] { new[] { 17, 29, 3, 22 }, new[] { 1, 9, 18, 26 } })
        {
            for (int column = bounds[0]; column <= bounds[1]; column++)
                for (int row = bounds[2]; row <= bounds[3]; row++)
                {
                    var cell = new Cell(column, row);
                    bool boundary = column == bounds[0] || column == bounds[1] || row == bounds[2] || row == bounds[3];
                    Check(boundary ? terrain.Kind(cell) != TerrainKind.Flat : terrain.Kind(cell) == TerrainKind.Flat && terrain.Elevation(cell) == 1, "Plateau interior is enclosed on all four sides");
                }
            foreach (int column in new[] { bounds[0], bounds[1] })
                foreach (int row in new[] { bounds[2], bounds[3] })
                    Check(terrain.IsCorner(new Cell(column, row)), "All four cliff corners are recognized");
        }
        foreach (var cell in new[] { new Cell(0, 23), new Cell(4, 27), new Cell(4, 17), new Cell(10, 22), new Cell(30, 16), new Cell(22, 29), new Cell(22, 2) })
            Check(terrain.Walkable(cell) && terrain.Elevation(cell) == 0 && terrain.HeightAt(cell.X, cell.Y) == 0, "Low ground surrounds bounded plateaus");
        var upperExit = new Cell(8, 22);
        var valleyRamp = new Cell(9, 22);
        var valleyExit = new Cell(10, 22);
        Check(terrain.Kind(valleyRamp) == TerrainKind.Ramp && terrain.Uphill(valleyRamp).Equals(new Cell(-1, 0)), "New northern exit slopes east into the valley");
        Check(terrain.CanTraverse(upperExit, valleyRamp) && terrain.CanTraverse(valleyRamp, valleyExit) && terrain.CanTraverse(valleyExit, valleyRamp) && terrain.CanTraverse(valleyRamp, upperExit), "Valley ramp traverses both ways");
        Check(!terrain.CanTraverse(valleyRamp, new Cell(9, 21)) && !terrain.CanTraverse(valleyRamp, new Cell(9, 23)), "Valley ramp rejects sideways exits");
        Check(Math.Abs(terrain.EdgeCost(upperExit, valleyRamp) + terrain.EdgeCost(valleyRamp, valleyExit) - 2.25f) < .0001f, "Descending valley ramp has correct surface distance");
        Reach(sim, upperExit);
        Reach(sim, valleyExit);
        Reach(sim, upperExit);
        var exitRoute = new[] { upperExit, valleyRamp, valleyExit };
        Check(sim.Lay(exitRoute, false) && sim.Lay(exitRoute, true), "Both power and rail infrastructure cross new valley ramp");
        var fuelUpper = new Cell(25, 21);
        var fuelRamp = new Cell(25, 22);
        var fuelLow = new Cell(25, 23);
        var fuelPort = new Cell(25, 24);
        Check(terrain.Kind(fuelRamp) == TerrainKind.Ramp && terrain.Uphill(fuelRamp).Equals(new Cell(0, -1)), "Eastern plateau descent faces north toward Fluxite");
        Check(terrain.Elevation(fuelUpper) == 1 && terrain.HeightAt(fuelLow.X, fuelLow.Y) == 0 && terrain.HeightAt(fuelPort.X, fuelPort.Y) == 0, "Descent joins upper ground to the lowland Fluxite port");
        Check(terrain.CanTraverse(fuelUpper, fuelRamp) && terrain.CanTraverse(fuelRamp, fuelLow) && terrain.CanTraverse(fuelLow, fuelRamp) && terrain.CanTraverse(fuelRamp, fuelUpper), "Fluxite ramp traverses in both directions");
        Check(!terrain.CanTraverse(fuelRamp, new Cell(24, 22)) && !terrain.CanTraverse(fuelRamp, new Cell(26, 22)), "Fluxite ramp rejects sideways exits");
        Check(Math.Abs(terrain.EdgeCost(fuelUpper, fuelRamp) + terrain.EdgeCost(fuelRamp, fuelLow) - 2.25f) < .0001f, "Fluxite descent has correct surface distance");
        var fuelRoute = new[] { fuelUpper, fuelRamp, fuelLow, fuelPort };
        Check(sim.FindPath(fuelUpper, fuelPort, cell => sim.StructureAt(cell) == null).SequenceEqual(fuelRoute), "Direct downhill route reaches the Fluxite port");
        Reach(sim, fuelUpper);
        Reach(sim, fuelPort);
        Reach(sim, fuelUpper);
        Check(sim.Lay(fuelRoute, false) && sim.Lay(fuelRoute, true), "Power and rails can descend toward Fluxite");
        Check(ColonySimulation.Footprint(new Cell(25, 25), 2).All(cell => terrain.Kind(cell) == TerrainKind.Flat && terrain.HeightAt(cell.X, cell.Y) == 0), "Entire Fluxite footprint sits on flat low ground");
        Check(ColonySimulation.Width == 32 && ColonySimulation.Height == 32, "Grid is 32 by 32");
        Check(ColonySimulation.InBounds(new Cell(31, 31)), "Expanded corner is in bounds");
        Check(!ColonySimulation.InBounds(new Cell(32, 31)) && !ColonySimulation.InBounds(new Cell(31, 32)), "Expanded boundaries reject outside cells");
        Check(sim.IsRevealed(new Cell(31, 31)), "Reveal covers expanded grid");
        Reach(sim, new Cell(31, 31));
        foreach (int row in new[] { 5, 15 })
        {
            var low = new Cell(16, row);
            var ramp = new Cell(17, row);
            var high = new Cell(18, row);
            Check(terrain.CanTraverse(low, ramp) && terrain.CanTraverse(ramp, high), "Ramp permits uphill traversal at row " + row);
            Check(terrain.CanTraverse(high, ramp) && terrain.CanTraverse(ramp, low), "Ramp permits downhill traversal at row " + row);
            var uphill = sim.FindPath(low, high, c => sim.StructureAt(c) == null);
            var downhill = sim.FindPath(high, low, c => sim.StructureAt(c) == null);
            Check(uphill != null && uphill.SequenceEqual(new[] { low, ramp, high }), "Uphill route crosses ramp at row " + row);
            Check(downhill != null && downhill.SequenceEqual(new[] { high, ramp, low }), "Downhill route crosses ramp at row " + row);
            Check(terrain.EdgeCost(low, ramp) > 1 && Math.Abs(terrain.EdgeCost(low, ramp) - terrain.EdgeCost(ramp, low)) < .00001f, "Slope cost exceeds flat cost and is symmetric");
            Reach(sim, low);
            Reach(sim, high);
            Reach(sim, low);
        }

        int credits = sim.Credits;
        int structures = sim.Structures.Count;
        Check(!sim.Build(StructureKind.PowerPlant, new Cell(16, 5)), "Reject old plant fixture spanning a ramp");
        Check(!sim.Build(StructureKind.Solar, new Cell(17, 7)), "Reject hillside construction");
        Check(sim.Credits == credits && sim.Structures.Count == structures, "Invalid terrain construction preserves money and structures");
        var hill = new Cell(17, 7);
        Check(!sim.OrderRover(hill), "Reject rover hillside destination");
        Check(sim.FindPath(new Cell(16, 7), hill, c => true) == null, "Reject path ending on hillside");
        var detour = sim.FindPath(new Cell(16, 7), new Cell(18, 7), c => sim.StructureAt(c) == null);
        Check(detour != null && detour.Contains(new Cell(17, 5)) && detour.All(terrain.Walkable), "Path detours around hillside via north pass");
        var blocked = ColonySimulation.Corridor(new Cell(16, 7), new Cell(18, 7));
        int conduits = sim.Conduits.Count;
        int rails = sim.Rails.Count;
        Check(!sim.Lay(blocked, false), "Reject conduit directly through hillside");
        Check(!sim.Lay(blocked, true), "Reject rail directly through hillside");
        Check(sim.Credits == credits && sim.Conduits.Count == conduits && sim.Rails.Count == rails, "Rejected terrain routes preserve money and networks");
        // Positive network test reaches a real plateau deposit from the real colony.
        // Reuse the earned credits and idle locomotive from the fleet scenarios.
        var plateau = Build(economy, StructureKind.Extractor, new Cell(4, 23));
        var route = economy.FindPath(economy.Colony.Port, plateau.Port, c => economy.StructureAt(c) == null);
        Check(route != null && route.Any(cell => economy.Terrain.Kind(cell) == TerrainKind.Ramp), "Plateau connection uses a ramp");
        Check(economy.Lay(route, false) && plateau.Connected, "Conduit powers plateau mine across ramp");
        Check(economy.Lay(route, true) && economy.RailRoute(plateau) != null, "Rail reaches plateau mine across ramp");
        Check(economy.Trains.Any(train => train.Source == plateau), "Plateau rail connection automatically dispatches ore service");
        var train = economy.Trains.First(t => t.Source == plateau);
        int sold = economy.Sold;
        Advance(economy, 120);
        Check(economy.Sold > sold, "Plateau train returns ore for payment");
        for (int i = 0; i < 300 && !(train.Phase == TrainPhase.ToColony && train.Cargo > 0); i++) Advance(economy, .25f);
        Check(train.Phase == TrainPhase.ToColony && train.Cargo > 0, "Plateau train carries cargo before parking");
        sold = economy.Sold;
        int cargo = train.Cargo;
        economy.ParkTrain(train);
        Advance(economy, 45);
        Check(train.Phase == TrainPhase.Parked && train.Cargo == 0 && train.Source == null, "Plateau train parks after delivery");
        Check(economy.Sold - sold >= cargo, "Plateau parking preserves ore payment");
        Check(Math.Abs(train.X - economy.Colony.Port.X) < .0001f && Math.Abs(train.Y - economy.Colony.Port.Y) < .0001f, "Plateau train returns to the low depot");
        Console.WriteLine("PASS terrainScenarioAssertions=" + (scenarioChecks - before));
    }

    static void RoverStopChecks()
    {
        var sim = new ColonySimulation(enableRaids: false);
        sim.Reveal(14, 11, 100);
        Reach(sim, new Cell(16, 5));
        Check(sim.OrderRover(new Cell(18, 5)), "Start rover crossing for cancellation");
        Advance(sim, .5f);
        Check(sim.RoverMoving && sim.RoverX > 16.5f && sim.RoverX < 17.5f, "Cancel while rover is partway up a ramp");
        float x = sim.RoverX, y = sim.RoverY;
        sim.StopRover();
        Advance(sim, 2);
        Check(!sim.RoverMoving && sim.RoverX == x && sim.RoverY == y, "StopRover immediately cancels movement without snapping position");
        Reach(sim, new Cell(18, 5));
    }

    static string NetworkState(ColonySimulation sim)
    {
        return sim.Credits + ":" + sim.Revision + ":" + sim.RevealRevision + ":" + sim.Generation + ":" + sim.Message +
            ":C=" + string.Join(";", sim.Conduits.OrderBy(c => c.X).ThenBy(c => c.Y)) +
            ":R=" + string.Join(";", sim.Rails.OrderBy(c => c.X).ThenBy(c => c.Y)) +
            ":P=" + string.Join(";", sim.PoweredCells.OrderBy(c => c.X).ThenBy(c => c.Y)) +
            ":B=" + string.Join(";", sim.Structures.Select(b => b.Origin + "=" + b.Connected));
    }

    static void ExtractorConduitChecks()
    {
        foreach (var site in new[] { new Cell(10, 4), new Cell(13, 13), new Cell(25, 25), new Cell(4, 23) })
        {
            var simulation = new ColonySimulation(enableRaids: false);
            simulation.Reveal(16, 16, 100);
            var deposit = simulation.DepositAt(site);
            var port = new Cell(site.X, site.Y - 1);
            var route = simulation.FindPath(simulation.Colony.Port, port, cell => simulation.StructureAt(cell) == null);
            Check(simulation.Lay(route, false), "Wire deposit port before construction");
            var crossing = ColonySimulation.Corridor(port, new Cell(site.X, site.Y + deposit.Size));
            Check(simulation.Lay(crossing, false), "Lay conduit across the deposit footprint");
            var conduits = new System.Collections.Generic.HashSet<Cell>(simulation.Conduits);
            var powered = new System.Collections.Generic.HashSet<Cell>(simulation.PoweredCells);
            int credits = simulation.Credits;
            Check(simulation.CanBuild(StructureKind.Extractor, site, out _, out _, out int cost, out _) && cost == deposit.Price, "Conduits do not block extractor preview or alter price");
            var extractor = Build(simulation, StructureKind.Extractor, site);
            Check(extractor.Connected && simulation.Credits == credits - deposit.Price, "Extractor builds powered at normal price");
            Check(simulation.Conduits.SetEquals(conduits) && simulation.PoweredCells.SetEquals(powered), "Building preserves conduit cells and downstream power");
            Check(simulation.CanLay(crossing, false, out int reuseCost, out _) && reuseCost == 0, "Conduits beneath extractor remain reusable");
            Check(simulation.TryPlanNetworkRoute(port, crossing.Last(), false, false, out _, out int plannedCost, out _) && plannedCost == 0, "Power planner reuses conduit through extractor");
            Check(!simulation.CanLay(crossing, true, out _, out _), "Rails cannot cross an extractor footprint");
            if (deposit.Size > 1)
                Check(!simulation.CanLay(new[] { new Cell(site.X + 1, site.Y) }, false, out _, out _), "Cannot add new conduit beneath an existing extractor");
        }
        var blocked = new ColonySimulation(enableRaids: false);
        blocked.Reveal(16, 16, 100);
        var oreSite = new Cell(10, 4);
        Check(blocked.Lay(new[] { oreSite }, true), "Place rail over an unmined deposit");
        int remainingCredits = blocked.Credits;
        Check(!blocked.Build(StructureKind.Extractor, oreSite) && blocked.Credits == remainingCredits && blocked.DepositAt(oreSite).Extractor == null, "Tracks still block extractor placement without charging");
        var solarSite = new Cell(1, 1);
        Check(blocked.Lay(new[] { solarSite }, false), "Place conduit on a clear building site");
        Check(!blocked.CanBuild(StructureKind.Solar, solarSite, out _, out _, out _, out _), "Other buildings still require clear infrastructure footprints");
    }

    static void PowerReadoutChecks()
    {
        var plant = new Structure { Kind = StructureKind.PowerPlant, Connected = true, Generation = 8 };
        plant.SampleGeneration(0.25f);
        Check(plant.AverageGeneration == 0, "Readout waits for a full sampling window");
        plant.Generation = 0;
        plant.SampleGeneration(0.75f);
        Check(plant.AverageGeneration == 2, "Readout averages generated energy over elapsed simulation time");
        plant.Generation = 8;
        plant.SampleGeneration(0.25f);
        Check(plant.AverageGeneration == 2, "Readout stays fixed between sampling windows");
        plant.Generation = 0;
        plant.SampleGeneration(0.75f);
        Check(plant.AverageGeneration == 2, "Intermittent generation displays its sustained rate");
        plant.SampleGeneration(1);
        Check(plant.AverageGeneration == 0, "Readout settles to zero when battery demand stops");
        plant.Generation = 8;
        plant.SampleGeneration(1);
        plant.Paused = true;
        Check(plant.AverageGeneration == 0, "Paused plant immediately reports zero");
        plant.SampleGeneration(0.25f);
        plant.Paused = false;
        Check(plant.AverageGeneration == 0, "Resumed plant does not reuse a pre-pause sample");
        plant.SampleGeneration(1);
        plant.Connected = false;
        Check(plant.AverageGeneration == 0, "Disconnected plant immediately reports zero");
        plant.Connected = true;
        plant.Health = 0;
        Check(plant.AverageGeneration == 0, "Disabled plant immediately reports zero");
    }

    static void NetworkPlannerChecks()
    {
        int before = scenarioChecks;
        var sim = new ColonySimulation(enableRaids: false);
        var starter = sim.Structures.Single(building => building.Kind == StructureKind.Solar);
        var starterRoute = ColonySimulation.Corridor(starter.Port, sim.Colony.Port);
        Check(sim.Conduits.SetEquals(starterRoute) && starterRoute.Count == 4, "Starting solar has a visible four-tile conduit to the colony");
        Check(starterRoute.All(sim.IsRevealed) && starterRoute.All(sim.PoweredCells.Contains), "Starting conduit is revealed and powered end to end");
        Check(starter.Connected && sim.SolarGeneration == 2 && sim.Credits == 500 && sim.Rails.Count == 1, "Starting connection is free and adds no rail");
        Check(sim.CanLay(starterRoute, false, out int starterCost, out _) && starterCost == 0, "Starting conduit can be reused for free");
        sim.Conduits.Remove(new Cell(3, 6));
        sim.Reconnect();
        Check(!starter.Connected && sim.SolarGeneration == 0, "Starter solar obeys normal conduit connectivity instead of a hidden exemption");
        sim.Conduits.Add(new Cell(3, 6));
        sim.Reconnect();
        Check(starter.Connected && sim.SolarGeneration == 2, "Restoring starter conduit restores solar generation");
        sim.Reveal(14, 11, 100);
        var solar = Build(sim, StructureKind.Solar, new Cell(9, 11));
        var start = sim.Colony.Port;
        var end = solar.Port;
        Check(!solar.Connected && sim.SolarGeneration == 2, "New solar waits for an actual conduit connection");
        Check(!sim.CanLay(ColonySimulation.Corridor(start, end, true), false, out _, out _), "Preferred vertical bend is blocked by the colony building");
        string snapshot = NetworkState(sim);
        Check(sim.TryPlanNetworkRoute(start, end, false, true, out var route, out int cost, out string reason), "Planner reverses a blocked bend: " + reason);
        Check(route.First().Equals(start) && route.Last().Equals(end) && route.SequenceEqual(ColonySimulation.Corridor(start, end, false)), "Alternate bend connects the real colony and solar ports");
        Check(route.All(c => sim.StructureAt(c) == null) && cost == 16, "Alternate bend avoids the building and quotes eight new conduit tiles");
        Check(NetworkState(sim) == snapshot && !solar.Connected, "Planning changes no credits, network, revision, message, or connection state");
        int credits = sim.Credits;
        Check(sim.Lay(route, false), "Lay the confirmed solar power plan");
        Check(sim.Credits == credits - cost && solar.Connected && sim.SolarGeneration == 4, "Only confirmed Lay charges and brings solar online");
        snapshot = NetworkState(sim);
        Check(sim.TryPlanNetworkRoute(end, start, false, false, out var reused, out int reuseCost, out _), "Plan a reverse trip along existing conduits");
        Check(reuseCost == 0 && reused.All(sim.Conduits.Contains) && NetworkState(sim) == snapshot, "Existing conduit route is free and planning stays read-only");
        Check(sim.TryPlanNetworkRoute(start, end, true, true, out var rails, out int railCost, out _), "Plan rails on the same corridor");
        Check(railCost == 24, "Conduits do not discount separate rails");
        credits = sim.Credits;
        Check(sim.Lay(rails, true) && sim.Credits == credits - railCost, "Rail placement charges only its quoted new rails");
        Check(sim.TryPlanNetworkRoute(end, start, true, false, out var reverseRails, out railCost, out _) && railCost == 0 && reverseRails.All(sim.Rails.Contains), "Existing rails can be reused free in reverse");

        // When both bends are valid, preserve the player's explicit choice.
        var openStart = new Cell(1, 1);
        var openEnd = new Cell(4, 4);
        snapshot = NetworkState(sim);
        Check(sim.TryPlanNetworkRoute(openStart, openEnd, false, false, out var horizontal, out _, out _) && horizontal.SequenceEqual(ColonySimulation.Corridor(openStart, openEnd)), "Preserve a valid horizontal-first choice");
        Check(sim.TryPlanNetworkRoute(openStart, openEnd, false, true, out var vertical, out _, out _) && vertical.SequenceEqual(ColonySimulation.Corridor(openStart, openEnd, true)), "Preserve a valid vertical-first choice");
        Check(NetworkState(sim) == snapshot, "Switching preview bends does not construct anything");

        // Both L paths hit a hillside. A valid path must detour through a pass.
        var low = new Cell(16, 7);
        var high = new Cell(18, 7);
        foreach (bool rail in new[] { false, true })
        {
            snapshot = NetworkState(sim);
            Check(sim.TryPlanNetworkRoute(low, high, rail, false, out var pass, out int passCost, out _), "Find a ramp detour for " + (rail ? "rails" : "power"));
            Check(pass.First().Equals(low) && pass.Last().Equals(high) && pass.Contains(new Cell(17, 5)) && pass.All(c => sim.IsRevealed(c) && sim.Terrain.Walkable(c) && sim.StructureAt(c) == null), "Detour uses revealed clear ground and the north pass");
            Check(sim.CanLay(pass, rail, out int verifiedCost, out _) && verifiedCost == passCost, "Terrain preview and placement agree on validity and cost");
            Check(NetworkState(sim) == snapshot, "Terrain detour planning does not build or charge");
        }

        // Reject invalid endpoints and unknown terrain instead of drawing a false link.
        snapshot = NetworkState(sim);
        Check(!sim.TryPlanNetworkRoute(start, sim.Colony.Origin, false, false, out _, out _, out _), "Reject an occupied destination instead of a port");
        Check(!sim.TryPlanNetworkRoute(sim.Colony.Origin, end, false, false, out _, out _, out _), "Reject an occupied start tile");
        Check(!sim.TryPlanNetworkRoute(new Cell(-1, 6), end, false, false, out _, out _, out _), "Reject an out-of-bounds endpoint");
        Check(!sim.TryPlanNetworkRoute(start, new Cell(17, 7), true, false, out _, out _, out _), "Reject a hillside endpoint");
        Check(NetworkState(sim) == snapshot, "Invalid endpoints leave existing links and balances intact");
        var hidden = new ColonySimulation(enableRaids: false);
        snapshot = NetworkState(hidden);
        Check(!hidden.TryPlanNetworkRoute(hidden.Colony.Port, new Cell(20, 5), false, false, out _, out _, out string hiddenReason) && !string.IsNullOrWhiteSpace(hiddenReason), "Hidden destination is rejected with a reason");
        Check(NetworkState(hidden) == snapshot, "Rejecting hidden ground does not reveal it");
        hidden.Reveal(low.X, low.Y, 0.1f);
        hidden.Reveal(high.X, high.Y, 0.1f);
        snapshot = NetworkState(hidden);
        Check(!hidden.TryPlanNetworkRoute(low, high, false, false, out _, out _, out _), "Two visible endpoints cannot route through an undiscovered pass");
        Check(NetworkState(hidden) == snapshot, "Blocked hidden detour leaves fog and networks unchanged");

        // A free reused alternate can be affordable when the preferred new bend is not.
        var reuse = new ColonySimulation(enableRaids: false);
        reuse.Reveal(14, 11, 100);
        var reuseStart = new Cell(1, 1);
        // Keep both bend options on lowland, below the northern hillside at row 15.
        var reuseEnd = new Cell(16, 14);
        var existing = ColonySimulation.Corridor(reuseStart, reuseEnd, true);
        Check(reuse.Lay(existing, false), "Create existing alternate conduit route");
        Build(reuse, StructureKind.Extractor, new Cell(10, 4));
        Build(reuse, StructureKind.Extractor, new Cell(23, 13));
        Build(reuse, StructureKind.Solar, new Cell(8, 4));
        Check(!reuse.CanLay(ColonySimulation.Corridor(reuseStart, reuseEnd), false, out _, out _), "Preferred new bend exceeds the remaining credits");
        snapshot = NetworkState(reuse);
        Check(reuse.TryPlanNetworkRoute(reuseStart, reuseEnd, false, false, out var affordable, out int affordableCost, out _) && affordableCost == 0 && affordable.All(reuse.Conduits.Contains), "Planner selects the free existing alternate within budget");
        Check(NetworkState(reuse) == snapshot, "Affordable route search remains read-only");

        // The fallback must prefer a longer free network over a shorter new route.
        var detourReuse = new ColonySimulation(enableRaids: false);
        detourReuse.Reveal(14, 11, 100);
        var southPass = ColonySimulation.Corridor(low, new Cell(16, 15))
            .Concat(ColonySimulation.Corridor(new Cell(16, 15), new Cell(18, 15)).Skip(1))
            .Concat(ColonySimulation.Corridor(new Cell(18, 15), high).Skip(1)).ToList();
        Check(detourReuse.Lay(southPass, false), "Create a longer existing route through the south pass");
        Build(detourReuse, StructureKind.PowerPlant, new Cell(14, 1));
        Build(detourReuse, StructureKind.Extractor, new Cell(10, 4));
        Check(detourReuse.Lay(ColonySimulation.Corridor(new Cell(0, 0), new Cell(12, 7)), true) && detourReuse.Credits == 2, "Leave insufficient credits for the short northern detour");
        snapshot = NetworkState(detourReuse);
        Check(detourReuse.TryPlanNetworkRoute(low, high, false, false, out var freeDetour, out int freeDetourCost, out _) && freeDetourCost == 0 && freeDetour.Contains(new Cell(17, 15)) && freeDetour.All(detourReuse.Conduits.Contains), "Fallback selects the longer free southern network");
        var connectionCosts = detourReuse.NetworkConnectionCosts(low, false);
        Check(connectionCosts.TryGetValue(high, out int overlayCost) && overlayCost == freeDetourCost, "Destination overlay agrees with minimum reuse cost");
        Check(!connectionCosts.ContainsKey(new Cell(17, 7)) && !connectionCosts.ContainsKey(detourReuse.Colony.Origin), "Destination overlay excludes hillsides and buildings");
        Check(NetworkState(detourReuse) == snapshot, "Fallback and destination-cost search leave the colony unchanged");

        var poor = new ColonySimulation(enableRaids: false);
        poor.Reveal(14, 11, 100);
        Build(poor, StructureKind.Solar, new Cell(1, 1));
        Build(poor, StructureKind.Solar, new Cell(5, 1));
        Build(poor, StructureKind.Solar, new Cell(9, 1));
        Build(poor, StructureKind.Solar, new Cell(1, 11));
        var waitingSolar = Build(poor, StructureKind.Solar, new Cell(9, 11));
        Check(poor.Credits == 0 && !waitingSolar.Connected, "Empty budget fixture has disconnected solar");
        snapshot = NetworkState(poor);
        Check(!poor.TryPlanNetworkRoute(poor.Colony.Port, waitingSolar.Port, false, false, out _, out _, out string budgetReason) && !string.IsNullOrWhiteSpace(budgetReason), "Unaffordable route is rejected with a reason");
        Check(NetworkState(poor) == snapshot && !waitingSolar.Connected, "Failed plan does not charge or falsely connect solar");
        Check(!poor.Lay(ColonySimulation.Corridor(poor.Colony.Port, waitingSolar.Port), false) && poor.Credits == 0 && !waitingSolar.Connected && poor.Conduits.SetEquals(starterRoute), "Rejected Lay is atomic and leaves solar disconnected");
        Check(poor.TryPlanNetworkRoute(poor.Colony.Port, poor.Colony.Port, true, false, out var samePort, out int sameCost, out _) && samePort.Count == 1 && sameCost == 0, "A valid existing port needs no new rails or credits");
        Console.WriteLine("PASS networkPlannerScenarioAssertions=" + (scenarioChecks - before));
    }

    static void AutoDispatchChecks()
    {
        var sim = new ColonySimulation(enableRaids: false);
        sim.Reveal(16, 16, 100);
        var ore = Build(sim, StructureKind.Extractor, new Cell(10, 4));
        Check(sim.Lay(new[] { ore.Port }, true), "Lay disconnected extractor rail stub");
        Check(sim.Train.Phase == TrainPhase.Parked, "Disconnected track does not dispatch");
        Link(sim, ore, true);
        Check(sim.Train.Source == ore && sim.Train.Destination == sim.Colony, "Completing rails starts the starter train without manual dispatch");
        Check(!ore.Connected, "Rail-first assignment does not require power wiring first");
        Check(sim.Trains.Count == 1, "Automatic assignment never buys a train");
        var assigned = sim.Train;
        Check(sim.Lay(new[] { ore.Port }, true), "Relaying existing track succeeds");
        Check(sim.Trains.Count(train => train.Source == ore) == 1 && sim.Train == assigned, "Repeated connection events do not duplicate services");
        Link(sim, ore, false);
        Advance(sim, 180);
        Check(sim.Sold > 0, "Automatically dispatched service earns credits");
        var secondOre = Build(sim, StructureKind.Extractor, new Cell(23, 13));
        Link(sim, secondOre, true);
        Check(sim.Train.Source == ore && sim.TrainFor(secondOre).Source == secondOre, "New mine uses its own train without stealing the existing service");
        Check(sim.Trains.Count == 2, "Each extractor adds exactly one free train");
        sim.ParkTrain(assigned);
        Advance(sim, 120);
        Check(assigned.Phase == TrainPhase.Parked && assigned.Source == null, "Manual parking finishes and returns to depot");
        Check(sim.Lay(new[] { ore.Port }, true), "Connection event after manual parking");
        Check(!sim.Trains.Any(train => train.Source == ore), "Connection changes do not restart manually stopped service");
        Check(sim.Dispatch(ore), "Manually stopped service can be explicitly restarted");

        var fuelSim = new ColonySimulation(enableRaids: false);
        fuelSim.Reveal(16, 16, 100);
        var fuel = Build(fuelSim, StructureKind.Extractor, new Cell(13, 13));
        Link(fuelSim, fuel, true);
        Check(fuelSim.Train.Phase == TrainPhase.Parked, "Fluxite waits for a reachable plant rather than going to colony");
        var plant = Build(fuelSim, StructureKind.PowerPlant, new Cell(14, 5));
        Check(fuelSim.Train.Phase == TrainPhase.Parked, "Unconnected plant does not start fuel service");
        Link(fuelSim, plant, true);
        Check(fuelSim.Train.Source == fuel && fuelSim.Train.Destination == plant, "Connecting plant last automatically starts Fluxite delivery");
        Check(fuelSim.Train.Resource == ResourceKind.Fluxite, "Automatic fuel service carries the right resource");

        var prewired = new ColonySimulation(enableRaids: false);
        prewired.Reveal(16, 16, 100);
        Check(prewired.Lay(prewired.FindPath(prewired.Colony.Port, new Cell(10, 3), cell => prewired.StructureAt(cell) == null && prewired.DepositAt(cell) == null), true), "Lay rails before extractor construction");
        var prewiredOre = Build(prewired, StructureKind.Extractor, new Cell(10, 4));
        Check(prewired.Train.Source == prewiredOre, "Building on a preconnected port starts service");
    }

    static void Main()
    {
        DefenseChecks.Run();
        var layout = new ColonySimulation(enableRaids: false);
        var rocks = layout.DecorativeRockCells();
        Check(rocks.Count == 24 && rocks.SequenceEqual(layout.DecorativeRockCells()), "Sparse rock layout is deterministic");
        foreach (var rock in rocks)
        {
            for (int offsetX = -1; offsetX <= 1; offsetX++)
                for (int offsetY = -1; offsetY <= 1; offsetY++)
                {
                    var neighbor = new Cell(rock.X + offsetX, rock.Y + offsetY);
                    Check(layout.Terrain.Kind(neighbor) == TerrainKind.Flat && layout.DepositAt(neighbor) == null && layout.StructureAt(neighbor) == null, "Rocks leave clearance around cliffs, ramps, deposits and starter buildings");
                }
            Check(rocks.All(other => other.Equals(rock) || Math.Abs(other.X - rock.X) > 2 || Math.Abs(other.Y - rock.Y) > 2), "Decorative rocks remain spaced apart");
        }
        Check(rocks.Any(cell => layout.Terrain.Elevation(cell) == 1) && rocks.Any(cell => layout.Terrain.Elevation(cell) == 0), "Rocks decorate both elevations");
        Check(layout.DepositAt(new Cell(13, 3)) == null, "Previous nearby ore location is empty");
        Check(layout.DepositAt(new Cell(10, 4))?.Resource == ResourceKind.Ore && layout.DepositAt(new Cell(10, 4))?.Size == 1, "Nearby ore is a 1x1 patch at (10, 4)");
        Check(layout.DepositAt(new Cell(12, 16)) == null, "Farther 1x1 ore leaves its previous lowland site clear");
        Check(layout.DepositAt(new Cell(23, 13))?.Resource == ResourceKind.Ore && layout.DepositAt(new Cell(23, 13))?.Size == 1 && layout.Terrain.Elevation(new Cell(23, 13)) == 1, "Farther 1x1 ore occupies the center of the eastern plateau");
        Check(layout.DepositAt(new Cell(13, 13))?.Resource == ResourceKind.Fluxite && layout.DepositAt(new Cell(13, 13))?.Size == 1, "Small Fluxite occupies (13, 13)");
        Check(layout.Deposits.Count(deposit => deposit.Resource == ResourceKind.Fluxite) == 1, "Only the 1x1 Fluxite fuel source remains");
        Check(layout.DepositAt(new Cell(13, 8)) == null, "Previous small Fluxite site is empty");
        Check(layout.Deposits.Count == 5 && layout.Deposits.Count(deposit => deposit.Resource == ResourceKind.Ore && deposit.Size == 2) == 2, "Both large deposits are 2x2 ore patches");
        Check(layout.DepositAt(new Cell(20, 6)) == null, "Former eastern 2x2 ore patch is removed");
        Check(layout.DepositAt(new Cell(25, 25))?.Size == 2 && layout.Deposits.All(deposit => deposit.Size < 3), "Eastern ore deposit retains the 2x2 tier");
        Check(ColonySimulation.Footprint(new Cell(22, 16), 2).All(cell => layout.DepositAt(cell) == null), "Moved Fluxite leaves its previous footprint clear");
        Check(layout.DepositAt(new Cell(24, 16)) == null && layout.DepositAt(new Cell(22, 18)) == null, "Eastern deposit leaves its former outer row and column clear");
        Check(layout.DepositAt(new Cell(15, 11)) == null && layout.DepositAt(new Cell(11, 7)) == null && layout.DepositAt(new Cell(8, 13)) == null && layout.DepositAt(new Cell(4, 17)) == null, "Former nearby resource sites are empty");
        foreach (var deposit in layout.Deposits)
        {
            var port = new Cell(deposit.Origin.X, deposit.Origin.Y - 1);
            var footprint = ColonySimulation.Footprint(deposit.Origin, deposit.Size).ToList();
            Check(footprint.All(cell => ColonySimulation.InBounds(cell) && layout.Terrain.Kind(cell) == TerrainKind.Flat && layout.Terrain.Elevation(cell) == layout.Terrain.Elevation(port)), "Deposit footprint is level with its port");
            Check(layout.FindPath(layout.Colony.Port, port, cell => layout.StructureAt(cell) == null) != null, "Resource port remains reachable");
            Check(footprint.All(cell => layout.Deposits.Count(other => other.Contains(cell)) == 1 && layout.StructureAt(cell) == null), "Deposit does not overlap another deposit or starter structure");
            Check(!layout.FullyRevealed(deposit), "Resource sites require exploration");
        }
        Check(layout.DepositAt(new Cell(4, 23))?.Resource == ResourceKind.Ore && layout.DepositAt(new Cell(4, 23))?.Size == 2 && layout.Terrain.Elevation(new Cell(4, 23)) == 1, "2x2 ore occupies the former northern Fluxite site");
        Check(layout.DepositAt(new Cell(25, 25))?.Resource == ResourceKind.Ore && layout.Terrain.Elevation(new Cell(25, 25)) == 0, "2x2 ore occupies (25, 25) on low ground north of the eastern plateau");
        var sim=new ColonySimulation(enableRaids: false); Check(sim.Trains.Count == 0 && sim.Train == null, "No unowned starter train"); Check(!sim.UpgradeTrain(), "No train upgrade before an extractor exists"); sim.Reveal(14,11,100);
        var ore=Build(sim,StructureKind.Extractor,new Cell(10,4)); Link(sim,ore,false);Link(sim,ore,true);Check(sim.Train.Source==ore,"Automatic ore dispatch");
        Advance(sim,600); Check(sim.Sold>0,"Ore earning");
        var fuel=Build(sim,StructureKind.Extractor,new Cell(13,13));var plant=Build(sim,StructureKind.PowerPlant,new Cell(14,5));Link(sim,fuel,false);Link(sim,fuel,true);Link(sim,plant,false);Link(sim,plant,true);
        var second = sim.TrainFor(fuel); Check(sim.UpgradeTrain(second), "Upgrade owned fuel train"); Check(second.Capacity == 8 && sim.Train.Capacity == 4, "Upgrade affects only owning mine");
        plant.Paused=true;Check(second.Source==fuel && second.Destination==plant,"Automatic fuel dispatch");int soldBefore=sim.Sold,creditsBefore=sim.Credits;Advance(sim,300);
        Check(sim.Sold>soldBefore,"Ore continues while fuel service runs");Check(sim.Credits-creditsBefore==(sim.Sold-soldBefore)*8,"Only ore credits awarded");Check(sim.FuelDelivered>0,"Fluxite delivery");Check(sim.FuelConsumed==0,"Paused plant does not burn");Check(plant.Stock==plant.Storage,"Plant fills to capacity");
        Check(second.Cargo>0 && second.Phase==TrainPhase.Unloading,"Full plant retains waiting fuel cargo");int retained=second.Cargo;sim.ParkTrain(second);Advance(sim,30);Check(second.Cargo==retained,"Park request retains blocked cargo");Check(second.Phase==TrainPhase.Unloading && second.ParkRequested,"Park waits for safe cargo delivery");Check(sim.Train.Source==ore && !sim.Train.ParkRequested,"Other train not parked");
        Check(sim.UpgradeExtractor(ore),"Upgrade ore");Check(sim.UpgradeExtractor(ore),"Upgrade ore again");plant.Paused=false;Advance(sim,300);Check(sim.FuelConsumed>0,"Plant burns under demand");Advance(sim,1000); Check(second.Phase==TrainPhase.Parked && second.Cargo==0 && second.Source==null && second.Destination==null,"Fuel train completes delivery before parking");Check(sim.Train.Source==ore,"Ore service preserved");
        Check(sim.Dispatch(fuel,plant),"Restart fuel service");Advance(sim,100);Check(sim.Trains.Count==2,"Fleet count stable");
        int duplicateCredits=sim.Credits;Check(!sim.Dispatch(ore),"Duplicate service rejected");Check(sim.Credits==duplicateCredits,"Rejected dispatch does not charge");
        for(int i=0;i<300 && !(sim.Train.Phase==TrainPhase.ToColony&&sim.Train.Cargo>0);i++) Advance(sim,.25f);
        Check(sim.Train.Phase==TrainPhase.ToColony && sim.Train.Cargo>0,"Ore train carries cargo before parking");int soldAtPark=sim.Sold;int oreCargo=sim.Train.Cargo;sim.ParkTrain(sim.Train);Advance(sim,30);Check(sim.Train.Phase==TrainPhase.Parked && sim.Train.Cargo==0,"Ore train parks after unloading");Check(sim.Sold-soldAtPark>=oreCargo,"Parking ore cargo sold");Check(second.Source==fuel && !second.ParkRequested,"Fuel service unchanged by parking ore train");
        TerrainChecks(sim);
        foreach (var deposit in sim.Deposits.Where(deposit => deposit.Extractor == null).ToList())
        {
            int before = sim.Credits;
            var mine = Build(sim, StructureKind.Extractor, deposit.Origin);
            Check(before - sim.Credits == deposit.Price, "Train is included in extractor cost");
            Check(sim.TrainFor(mine)?.Owner == mine, "New train has permanent extractor ownership");
        }
        Check(sim.Trains.Count == 5, "Five extractors own five trains without the former four-train cap");
        Check(sim.Trains.Select(train => train.Owner).Distinct().Count() == 5, "Each train has a unique owner");
        Check(sim.TrainFor(ore).Owner == ore && sim.TrainFor(fuel).Owner == fuel, "Parking retains ownership");
        int failedCredits = sim.Credits;
        Check(!sim.Build(StructureKind.Extractor, ore.Origin) && sim.Trains.Count == 5 && sim.Credits == failedCredits, "Duplicate build creates no train and charges nothing");
        RoverStopChecks();
        ExtractorConduitChecks();
        PowerReadoutChecks();
        NetworkPlannerChecks();
        AutoDispatchChecks();
        Console.WriteLine("PASS scenarioAssertions="+scenarioChecks+" invariantAssertions="+invariantChecks+" timeSteps="+timeSteps+" sold="+sim.Sold+" ore="+sim.AccountedOre+"/"+sim.Produced+" fuel="+sim.AccountedFuel+"/"+sim.FuelProduced+" delivered="+sim.FuelDelivered+" consumed="+sim.FuelConsumed+" battery="+sim.Battery);
    }
}
