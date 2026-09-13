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
        Console.WriteLine("PASS scenarioAssertions="+scenarioChecks+" invariantAssertions="+invariantChecks+" timeSteps="+timeSteps+" sold="+sim.Sold+" ore="+sim.AccountedOre+"/"+sim.Produced+" fuel="+sim.AccountedFuel+"/"+sim.FuelProduced+" delivered="+sim.FuelDelivered+" consumed="+sim.FuelConsumed+" battery="+sim.Battery);
    }
}
