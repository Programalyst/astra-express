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
        var sim = new ColonySimulation();
        sim.Reveal(14, 11, 100);
        var terrain = sim.Terrain;
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
        var plateau = Build(economy, StructureKind.Extractor, new Cell(20, 6));
        var route = economy.FindPath(economy.Colony.Port, plateau.Port, c => economy.StructureAt(c) == null);
        Check(route != null && route.Contains(new Cell(17, 5)), "Plateau connection uses north ramp");
        Check(economy.Lay(route, false) && plateau.Connected, "Conduit powers plateau mine across ramp");
        Check(economy.Lay(route, true) && economy.RailRoute(plateau) != null, "Rail reaches plateau mine across ramp");
        Check(economy.Dispatch(plateau), "Dispatch plateau ore service");
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
        var sim = new ColonySimulation();
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

    static void NetworkPlannerChecks()
    {
        int before = scenarioChecks;
        var sim = new ColonySimulation();
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
        var hidden = new ColonySimulation();
        snapshot = NetworkState(hidden);
        Check(!hidden.TryPlanNetworkRoute(hidden.Colony.Port, new Cell(20, 5), false, false, out _, out _, out string hiddenReason) && !string.IsNullOrWhiteSpace(hiddenReason), "Hidden destination is rejected with a reason");
        Check(NetworkState(hidden) == snapshot, "Rejecting hidden ground does not reveal it");
        hidden.Reveal(low.X, low.Y, 0.1f);
        hidden.Reveal(high.X, high.Y, 0.1f);
        snapshot = NetworkState(hidden);
        Check(!hidden.TryPlanNetworkRoute(low, high, false, false, out _, out _, out _), "Two visible endpoints cannot route through an undiscovered pass");
        Check(NetworkState(hidden) == snapshot, "Blocked hidden detour leaves fog and networks unchanged");

        // A free reused alternate can be affordable when the preferred new bend is not.
        var reuse = new ColonySimulation();
        reuse.Reveal(14, 11, 100);
        var reuseStart = new Cell(1, 1);
        // Keep both bend options on lowland, below the northern hillside at row 15.
        var reuseEnd = new Cell(16, 14);
        var existing = ColonySimulation.Corridor(reuseStart, reuseEnd, true);
        Check(reuse.Lay(existing, false), "Create existing alternate conduit route");
        Check(reuse.BuyTrain() && reuse.BuyTrain(), "Spend budget on two locomotives for affordability fixture");
        Build(reuse, StructureKind.Solar, new Cell(8, 4));
        Check(!reuse.CanLay(ColonySimulation.Corridor(reuseStart, reuseEnd), false, out _, out _), "Preferred new bend exceeds the remaining credits");
        snapshot = NetworkState(reuse);
        Check(reuse.TryPlanNetworkRoute(reuseStart, reuseEnd, false, false, out var affordable, out int affordableCost, out _) && affordableCost == 0 && affordable.All(reuse.Conduits.Contains), "Planner selects the free existing alternate within budget");
        Check(NetworkState(reuse) == snapshot, "Affordable route search remains read-only");

        // The fallback must prefer a longer free network over a shorter new route.
        var detourReuse = new ColonySimulation();
        detourReuse.Reveal(14, 11, 100);
        var southPass = ColonySimulation.Corridor(low, new Cell(16, 15))
            .Concat(ColonySimulation.Corridor(new Cell(16, 15), new Cell(18, 15)).Skip(1))
            .Concat(ColonySimulation.Corridor(new Cell(18, 15), high).Skip(1)).ToList();
        Check(detourReuse.Lay(southPass, false), "Create a longer existing route through the south pass");
        Build(detourReuse, StructureKind.PowerPlant, new Cell(14, 1));
        Check(detourReuse.BuyTrain(), "Reserve a train while testing a tight routing budget");
        Check(detourReuse.Lay(ColonySimulation.Corridor(new Cell(0, 0), new Cell(12, 7)), true) && detourReuse.Credits == 2, "Leave insufficient credits for the short northern detour");
        snapshot = NetworkState(detourReuse);
        Check(detourReuse.TryPlanNetworkRoute(low, high, false, false, out var freeDetour, out int freeDetourCost, out _) && freeDetourCost == 0 && freeDetour.Contains(new Cell(17, 15)) && freeDetour.All(detourReuse.Conduits.Contains), "Fallback selects the longer free southern network");
        var connectionCosts = detourReuse.NetworkConnectionCosts(low, false);
        Check(connectionCosts.TryGetValue(high, out int overlayCost) && overlayCost == freeDetourCost, "Destination overlay agrees with minimum reuse cost");
        Check(!connectionCosts.ContainsKey(new Cell(17, 7)) && !connectionCosts.ContainsKey(detourReuse.Colony.Origin), "Destination overlay excludes hillsides and buildings");
        Check(NetworkState(detourReuse) == snapshot, "Fallback and destination-cost search leave the colony unchanged");

        var poor = new ColonySimulation();
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
        Check(!poor.Lay(ColonySimulation.Corridor(poor.Colony.Port, waitingSolar.Port), false) && poor.Credits == 0 && !waitingSolar.Connected && poor.Conduits.Count == 1, "Rejected Lay is atomic and leaves solar disconnected");
        Check(poor.TryPlanNetworkRoute(poor.Colony.Port, poor.Colony.Port, true, false, out var samePort, out int sameCost, out _) && samePort.Count == 1 && sameCost == 0, "A valid existing port needs no new rails or credits");
        Console.WriteLine("PASS networkPlannerScenarioAssertions=" + (scenarioChecks - before));
    }

    static void Main()
    {
        var sim=new ColonySimulation(); sim.Reveal(14,11,100);
        var ore=Build(sim,StructureKind.Extractor,new Cell(11,7)); Link(sim,ore,false);Link(sim,ore,true);Check(sim.Dispatch(ore),"Ore dispatch");
        Advance(sim,600); Check(sim.Sold>0,"Ore earning");
        Check(sim.BuyTrain(),"Buy second train");var second=sim.Trains[1];Check(sim.UpgradeTrain(second),"Upgrade selected second");Check(second.Capacity==8 && sim.Train.Capacity==4,"Only selected second upgraded");
        var fuel=Build(sim,StructureKind.Extractor,new Cell(13,3));var plant=Build(sim,StructureKind.PowerPlant,new Cell(14,5));Link(sim,fuel,false);Link(sim,fuel,true);Link(sim,plant,false);Link(sim,plant,true);
        plant.Paused=true;Check(sim.Dispatch(fuel,plant),"Fuel dispatch");int soldBefore=sim.Sold,creditsBefore=sim.Credits;Advance(sim,300);
        Check(sim.Sold>soldBefore,"Ore continues while fuel service runs");Check(sim.Credits-creditsBefore==(sim.Sold-soldBefore)*8,"Only ore credits awarded");Check(sim.FuelDelivered>0,"Fluxite delivery");Check(sim.FuelConsumed==0,"Paused plant does not burn");Check(plant.Stock==plant.Storage,"Plant fills to capacity");
        Check(second.Cargo>0 && second.Phase==TrainPhase.Unloading,"Full plant retains waiting fuel cargo");int retained=second.Cargo;sim.ParkTrain(second);Advance(sim,30);Check(second.Cargo==retained,"Park request retains blocked cargo");Check(second.Phase==TrainPhase.Unloading && second.ParkRequested,"Park waits for safe cargo delivery");Check(sim.Train.Source==ore && !sim.Train.ParkRequested,"Other train not parked");
        Check(sim.UpgradeExtractor(ore),"Upgrade ore");Check(sim.UpgradeExtractor(ore),"Upgrade ore again");plant.Paused=false;Advance(sim,300);Check(sim.FuelConsumed>0,"Plant burns under demand");Advance(sim,1000); Check(second.Phase==TrainPhase.Parked && second.Cargo==0 && second.Source==null && second.Destination==null,"Fuel train completes delivery before parking");Check(sim.Train.Source==ore,"Ore service preserved");
        Check(sim.Dispatch(fuel,plant),"Restart fuel service");Advance(sim,100);Check(sim.Trains.Count==2,"Fleet count stable");
        int duplicateCredits=sim.Credits;Check(!sim.Dispatch(ore),"Duplicate service rejected");Check(sim.Credits==duplicateCredits,"Rejected dispatch does not charge");
        for(int i=0;i<300 && !(sim.Train.Phase==TrainPhase.ToColony&&sim.Train.Cargo>0);i++) Advance(sim,.25f);
        Check(sim.Train.Phase==TrainPhase.ToColony && sim.Train.Cargo>0,"Ore train carries cargo before parking");int soldAtPark=sim.Sold;int oreCargo=sim.Train.Cargo;sim.ParkTrain(sim.Train);Advance(sim,30);Check(sim.Train.Phase==TrainPhase.Parked && sim.Train.Cargo==0,"Ore train parks after unloading");Check(sim.Sold-soldAtPark>=oreCargo,"Parking ore cargo sold");Check(second.Source==fuel && !second.ParkRequested,"Fuel service unchanged by parking ore train");
        Check(sim.BuyTrain(),"Buy third train");Check(sim.BuyTrain(),"Buy fourth train");int maxCredits=sim.Credits;Check(!sim.BuyTrain(),"Fifth train rejected");Check(sim.Trains.Count==4 && sim.Credits==maxCredits,"Fleet limit preserves money");
        TerrainChecks(sim);
        RoverStopChecks();
        NetworkPlannerChecks();
        Console.WriteLine("PASS scenarioAssertions="+scenarioChecks+" invariantAssertions="+invariantChecks+" timeSteps="+timeSteps+" sold="+sim.Sold+" ore="+sim.AccountedOre+"/"+sim.Produced+" fuel="+sim.AccountedFuel+"/"+sim.FuelProduced+" delivered="+sim.FuelDelivered+" consumed="+sim.FuelConsumed+" battery="+sim.Battery);
    }
}
