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
    static void Main()
    {
        var sim=new ColonySimulation(); sim.Reveal(14,11,100);
        var ore=Build(sim,StructureKind.Extractor,new Cell(11,7)); Link(sim,ore,false);Link(sim,ore,true);Check(sim.Dispatch(ore),"Ore dispatch");
        Advance(sim,600); Check(sim.Sold>0,"Ore earning");
        Check(sim.BuyTrain(),"Buy second train");var second=sim.Trains[1];Check(sim.UpgradeTrain(second),"Upgrade selected second");Check(second.Capacity==8 && sim.Train.Capacity==4,"Only selected second upgraded");
        var fuel=Build(sim,StructureKind.Extractor,new Cell(13,3));var plant=Build(sim,StructureKind.PowerPlant,new Cell(16,5));Link(sim,fuel,false);Link(sim,fuel,true);Link(sim,plant,false);Link(sim,plant,true);
        plant.Paused=true;Check(sim.Dispatch(fuel,plant),"Fuel dispatch");int soldBefore=sim.Sold,creditsBefore=sim.Credits;Advance(sim,300);
        Check(sim.Sold>soldBefore,"Ore continues while fuel service runs");Check(sim.Credits-creditsBefore==(sim.Sold-soldBefore)*8,"Only ore credits awarded");Check(sim.FuelDelivered>0,"Fluxite delivery");Check(sim.FuelConsumed==0,"Paused plant does not burn");Check(plant.Stock==plant.Storage,"Plant fills to capacity");
        Check(second.Cargo>0 && second.Phase==TrainPhase.Unloading,"Full plant retains waiting fuel cargo");int retained=second.Cargo;sim.ParkTrain(second);Advance(sim,30);Check(second.Cargo==retained,"Park request retains blocked cargo");Check(second.Phase==TrainPhase.Unloading && second.ParkRequested,"Park waits for safe cargo delivery");Check(sim.Train.Source==ore && !sim.Train.ParkRequested,"Other train not parked");
        Check(sim.UpgradeExtractor(ore),"Upgrade ore");Check(sim.UpgradeExtractor(ore),"Upgrade ore again");plant.Paused=false;Advance(sim,300);Check(sim.FuelConsumed>0,"Plant burns under demand");Advance(sim,1000); Check(second.Phase==TrainPhase.Parked && second.Cargo==0 && second.Source==null && second.Destination==null,"Fuel train completes delivery before parking");Check(sim.Train.Source==ore,"Ore service preserved");
        Check(sim.Dispatch(fuel,plant),"Restart fuel service");Advance(sim,100);Check(sim.Trains.Count==2,"Fleet count stable");
        int duplicateCredits=sim.Credits;Check(!sim.Dispatch(ore),"Duplicate service rejected");Check(sim.Credits==duplicateCredits,"Rejected dispatch does not charge");
        for(int i=0;i<300 && !(sim.Train.Phase==TrainPhase.ToColony&&sim.Train.Cargo>0);i++) Advance(sim,.25f);
        Check(sim.Train.Phase==TrainPhase.ToColony && sim.Train.Cargo>0,"Ore train carries cargo before parking");int soldAtPark=sim.Sold;int oreCargo=sim.Train.Cargo;sim.ParkTrain(sim.Train);Advance(sim,30);Check(sim.Train.Phase==TrainPhase.Parked && sim.Train.Cargo==0,"Ore train parks after unloading");Check(sim.Sold-soldAtPark>=oreCargo,"Parking ore cargo sold");Check(second.Source==fuel && !second.ParkRequested,"Fuel service unchanged by parking ore train");
        Check(sim.BuyTrain(),"Buy third train");Check(sim.BuyTrain(),"Buy fourth train");int maxCredits=sim.Credits;Check(!sim.BuyTrain(),"Fifth train rejected");Check(sim.Trains.Count==4 && sim.Credits==maxCredits,"Fleet limit preserves money");
        Console.WriteLine("PASS scenarioAssertions="+scenarioChecks+" invariantAssertions="+invariantChecks+" timeSteps="+timeSteps+" sold="+sim.Sold+" ore="+sim.AccountedOre+"/"+sim.Produced+" fuel="+sim.AccountedFuel+"/"+sim.FuelProduced+" delivered="+sim.FuelDelivered+" consumed="+sim.FuelConsumed+" battery="+sim.Battery);
    }
}
