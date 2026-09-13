using System;
using System.Collections.Generic;
using System.Linq;

namespace AstraExpress
{
    public readonly struct Cell : IEquatable<Cell>
    {
        public readonly int X;
        public readonly int Y;
        public Cell(int column, int row) { X = column; Y = row; }
        public bool Equals(Cell other) => X == other.X && Y == other.Y;
        public override bool Equals(object other) => other is Cell cell && Equals(cell);
        public override int GetHashCode() => X * 397 ^ Y;
        public static Cell operator +(Cell first, Cell second) => new Cell(first.X + second.X, first.Y + second.Y);
        public override string ToString() => $"{X}, {Y}";
    }

    public enum ResourceKind { Ore, Fluxite }
    public enum StructureKind { Colony, Solar, Extractor }
    public enum TrainPhase { Parked, ToMine, Loading, ToColony, Unloading }

    public sealed class Deposit
    {
        public Cell Origin;
        public int Size;
        public ResourceKind Resource = ResourceKind.Ore;
        public Structure Extractor;
        public float Rate => Size == 1 ? 0.5f : Size == 2 ? 1f : 1.75f;
        public int Price => Size == 1 ? 150 : Size == 2 ? 250 : 350;
        public bool Contains(Cell cell) => cell.X >= Origin.X && cell.X < Origin.X + Size && cell.Y >= Origin.Y && cell.Y < Origin.Y + Size;
    }

    public sealed class Structure
    {
        public StructureKind Kind;
        public Cell Origin;
        public int Size;
        public bool Starter;
        public bool Connected;
        public bool Paused;
        public Deposit Deposit;
        public int Stock;
        public int Level = 1;
        public float Progress;
        public float SuppliedFraction;
        public Cell Port => new Cell(Origin.X, Origin.Y - 1);
        public int Storage => 24 * Size;
        public float Demand => Size * Level;
        public float Rate => Deposit == null ? 0 : Deposit.Rate * Level;
        public bool Contains(Cell cell) => cell.X >= Origin.X && cell.X < Origin.X + Size && cell.Y >= Origin.Y && cell.Y < Origin.Y + Size;
    }

    public sealed class FreightTrain
    {
        public TrainPhase Phase;
        public Structure Source;
        public ResourceKind Resource;
        public int Cargo;
        public int Capacity = 4;
        public int CapacityLevel = 1;
        public float X;
        public float Y;
        public float Dwell;
        public bool ParkRequested;
        public List<Cell> Route = new List<Cell>();
        public List<Cell> Leg = new List<Cell>();
        public int Waypoint;
    }

    public sealed class ColonySimulation
    {
        public const int Width = 28;
        public const int Height = 22;
        public const float BatteryCapacity = 100;
        public const float Reserve = 10;
        public static readonly Cell[] Directions = { new Cell(1, 0), new Cell(-1, 0), new Cell(0, 1), new Cell(0, -1) };
        public readonly bool[,] Revealed = new bool[Width, Height];
        public readonly List<Deposit> Deposits = new List<Deposit>();
        public readonly List<Structure> Structures = new List<Structure>();
        public readonly HashSet<Cell> Conduits = new HashSet<Cell>();
        public readonly HashSet<Cell> Rails = new HashSet<Cell>();
        public readonly HashSet<Cell> PoweredCells = new HashSet<Cell>();
        public readonly FreightTrain Train = new FreightTrain();
        public readonly Structure Colony;
        public int Credits { get; private set; } = 500;
        public float Battery { get; private set; } = BatteryCapacity;
        public float Generation { get; private set; }
        public float Demand { get; private set; }
        public float RoverX { get; private set; } = 7;
        public float RoverY { get; private set; } = 6;
        public int Produced { get; private set; }
        public int Sold { get; private set; }
        public int Deliveries { get; private set; }
        public int Revision { get; private set; }
        public int RevealRevision { get; private set; }
        public bool Paused;
        public string Message = "Welcome, commander. Explore east of the colony to discover your first ore deposit.";
        public bool RoverMoving => roverWaypoint < roverRoute.Count;
        public Cell RoverCell => new Cell((int)Math.Round(RoverX), (int)Math.Round(RoverY));
        public int AccountedOre => Structures.Sum(structure => structure.Stock) + Train.Cargo + Sold;
        private List<Cell> roverRoute = new List<Cell>();
        private int roverWaypoint;

        public ColonySimulation()
        {
            Colony = new Structure { Kind = StructureKind.Colony, Origin = new Cell(5, 7), Size = 2, Starter = true, Connected = true };
            Structures.Add(Colony);
            Structures.Add(new Structure { Kind = StructureKind.Solar, Origin = new Cell(2, 7), Size = 2, Starter = true, Connected = true });
            Deposits.Add(new Deposit { Origin = new Cell(11, 7), Size = 1 });
            Deposits.Add(new Deposit { Origin = new Cell(8, 13), Size = 1 });
            Deposits.Add(new Deposit { Origin = new Cell(15, 11), Size = 2 });
            Deposits.Add(new Deposit { Origin = new Cell(20, 6), Size = 2 });
            Deposits.Add(new Deposit { Origin = new Cell(22, 16), Size = 3 });
            Conduits.Add(Colony.Port);
            Rails.Add(Colony.Port);
            Train.X = Colony.Port.X;
            Train.Y = Colony.Port.Y;
            Reveal(5.5f, 7.5f, 5);
            Reveal(RoverX, RoverY, 3);
            Reconnect();
        }

        public static bool InBounds(Cell cell) => cell.X >= 0 && cell.Y >= 0 && cell.X < Width && cell.Y < Height;
        public bool IsRevealed(Cell cell) => InBounds(cell) && Revealed[cell.X, cell.Y];
        public Structure StructureAt(Cell cell) => Structures.FirstOrDefault(structure => structure.Contains(cell));
        public Deposit DepositAt(Cell cell) => Deposits.FirstOrDefault(deposit => deposit.Contains(cell));
        public bool FullyRevealed(Deposit deposit) => Footprint(deposit.Origin, deposit.Size).All(IsRevealed);
        public static IEnumerable<Cell> Footprint(Cell origin, int size)
        {
            for (int column = 0; column < size; column++)
                for (int row = 0; row < size; row++)
                    yield return new Cell(origin.X + column, origin.Y + row);
        }

        public void Reveal(float centerX, float centerY, float radius)
        {
            bool changed = false;
            for (int column = 0; column < Width; column++)
                for (int row = 0; row < Height; row++)
                    if (!Revealed[column, row] && (column - centerX) * (column - centerX) + (row - centerY) * (row - centerY) <= radius * radius)
                    {
                        Revealed[column, row] = true;
                        changed = true;
                    }
            if (changed) RevealRevision++;
        }

        public bool OrderRover(Cell destination)
        {
            if (!InBounds(destination) || StructureAt(destination) != null) return Fail("The rover needs clear ground. Click beside the building.");
            var route = FindPath(RoverCell, destination, cell => StructureAt(cell) == null);
            if (route == null) return Fail("No walkable route to that destination.");
            roverRoute = route;
            roverWaypoint = 0;
            Message = "Rover exploring. Moving costs 2 power per tile; solar recharges the shared battery.";
            return true;
        }

        public bool CanBuild(StructureKind kind, Cell requested, out Cell origin, out int size, out int cost, out string reason)
        {
            origin = requested;
            size = kind == StructureKind.Solar ? 2 : 1;
            cost = kind == StructureKind.Solar ? 100 : 150;
            reason = "";
            if (!IsRevealed(requested)) { reason = "Explore this ground first."; return false; }
            if (kind != StructureKind.Solar && kind != StructureKind.Extractor) { reason = "The colony is fixed."; return false; }
            var deposit = DepositAt(requested);
            if (kind == StructureKind.Extractor)
            {
                if (deposit == null) { reason = "Place an extractor on a discovered ore patch."; return false; }
                if (!FullyRevealed(deposit)) { reason = "Explore the entire patch before placing an extractor."; return false; }
                origin = deposit.Origin;
                size = deposit.Size;
                cost = deposit.Price;
                if (deposit.Extractor != null) { reason = "This deposit already has an extractor."; return false; }
            }
            foreach (var cell in Footprint(origin, size))
            {
                if (!IsRevealed(cell)) { reason = "The complete footprint must be explored."; return false; }
                if (StructureAt(cell) != null || Rails.Contains(cell) || Conduits.Contains(cell) || cell.Equals(RoverCell) || TrainOccupies(cell))
                { reason = "Footprint occupied. Leave room for vehicles and infrastructure."; return false; }
                if (kind == StructureKind.Solar && DepositAt(cell) != null) { reason = "Keep ore deposits free for extractors."; return false; }
                if (Structures.Any(structure => structure.Port.Equals(cell))) { reason = "Keep the connection ports clear."; return false; }
            }
            var port = new Cell(origin.X, origin.Y - 1);
            if (!IsRevealed(port) || StructureAt(port) != null) { reason = "Explore and clear the port immediately south of the building."; return false; }
            if (Credits < cost) { reason = $"Need {cost} credits. Deliver ore to earn more."; return false; }
            return true;
        }

        private bool TrainOccupies(Cell cell) => Math.Abs(Train.X - cell.X) < 0.55f && Math.Abs(Train.Y - cell.Y) < 0.55f;

        public bool Build(StructureKind kind, Cell requested)
        {
            if (!CanBuild(kind, requested, out var origin, out int size, out int cost, out string reason)) return Fail(reason);
            var structure = new Structure { Kind = kind, Origin = origin, Size = size };
            if (kind == StructureKind.Extractor)
            {
                structure.Deposit = DepositAt(origin);
                structure.Deposit.Extractor = structure;
            }
            Structures.Add(structure);
            Credits -= cost;
            Revision++;
            Reconnect();
            Message = kind == StructureKind.Solar ? "Solar built. Wire its cyan port to the colony's power network." : "Extractor built. Connect its south port with conduits, then lay a railway to the colony.";
            return true;
        }

        public static List<Cell> Corridor(Cell start, Cell end, bool verticalFirst = false)
        {
            var result = new List<Cell> { start };
            int column = start.X;
            int row = start.Y;
            while (column != end.X || row != end.Y)
            {
                if ((verticalFirst && row != end.Y) || column == end.X) row += Math.Sign(end.Y - row);
                else column += Math.Sign(end.X - column);
                result.Add(new Cell(column, row));
            }
            return result;
        }

        public bool CanLay(IReadOnlyList<Cell> path, bool rail, out int cost, out string reason)
        {
            cost = 0;
            reason = "";
            if (path == null || path.Count == 0) { reason = "Choose a start and end tile."; return false; }
            var network = rail ? Rails : Conduits;
            var unique = new HashSet<Cell>();
            for (int index = 0; index < path.Count; index++)
            {
                var cell = path[index];
                if (!IsRevealed(cell) || StructureAt(cell) != null) { reason = "Route must stay on explored, unoccupied ground."; return false; }
                if (index > 0 && Math.Abs(cell.X - path[index - 1].X) + Math.Abs(cell.Y - path[index - 1].Y) > 1)
                { reason = "Route tiles must connect edge to edge."; return false; }
                if (unique.Add(cell) && !network.Contains(cell)) cost += rail ? 3 : 2;
            }
            if (cost > Credits) { reason = $"Route costs {cost} credits; only {Credits} available."; return false; }
            return true;
        }

        public bool Lay(IReadOnlyList<Cell> path, bool rail)
        {
            if (!CanLay(path, rail, out int cost, out string reason)) return Fail(reason);
            var network = rail ? Rails : Conduits;
            foreach (var cell in path) network.Add(cell);
            Credits -= cost;
            Revision++;
            Reconnect();
            Message = rail ? $"Railway built for {cost} credits. Select an extractor and dispatch the train." : $"Conduits built for {cost} credits. Cyan lines connect the shared power grid.";
            return true;
        }

        public void Reconnect()
        {
            PoweredCells.Clear();
            var frontier = new Queue<Cell>();
            frontier.Enqueue(Colony.Port);
            PoweredCells.Add(Colony.Port);
            while (frontier.Count > 0)
            {
                var current = frontier.Dequeue();
                foreach (var direction in Directions)
                {
                    var next = current + direction;
                    if (Conduits.Contains(next) && PoweredCells.Add(next)) frontier.Enqueue(next);
                }
            }
            foreach (var structure in Structures) structure.Connected = structure.Starter || PoweredCells.Contains(structure.Port);
            Generation = Structures.Count(structure => structure.Kind == StructureKind.Solar && structure.Connected) * 2;
        }

        public List<Cell> RailRoute(Structure extractor) => FindPath(Colony.Port, extractor.Port, cell => Rails.Contains(cell));

        public bool Dispatch(Structure extractor)
        {
            if (extractor == null || extractor.Kind != StructureKind.Extractor) return Fail("Select an extractor first.");
            if (Train.Phase != TrainPhase.Parked) return Fail("Park the current service before choosing another mine.");
            var route = RailRoute(extractor);
            if (route == null) return Fail("Connect rails between the colony depot and the extractor's south port.");
            Train.Source = extractor;
            Train.Resource = extractor.Deposit.Resource;
            Train.Route = route;
            Train.ParkRequested = false;
            BeginLeg(false);
            Message = "Service started. The train collects ore and sells each delivery at the colony.";
            return true;
        }

        public void ParkTrain()
        {
            Train.ParkRequested = true;
            if (Train.Phase == TrainPhase.Loading) BeginLeg(true);
            Message = "Train will deliver any cargo and park at the colony. No cargo is discarded.";
        }

        public bool UpgradeTrain()
        {
            if (Train.CapacityLevel >= 3) return Fail("Train capacity is fully upgraded.");
            int cost = Train.CapacityLevel * 100;
            if (Credits < cost) return Fail($"Need {cost} credits for this upgrade.");
            Credits -= cost;
            Train.CapacityLevel++;
            Train.Capacity = Train.CapacityLevel * 4;
            Message = $"Train upgraded to {Train.Capacity} cargo. Additional capacity is used at the next loading stop.";
            return true;
        }

        public bool UpgradeExtractor(Structure extractor)
        {
            if (extractor == null || extractor.Kind != StructureKind.Extractor || extractor.Level >= 3) return Fail("Select an extractor below level 3.");
            int cost = extractor.Level * 120;
            if (Credits < cost) return Fail($"Need {cost} credits for this upgrade.");
            Credits -= cost;
            extractor.Level++;
            Message = "Extractor upgraded. Faster production also consumes more power.";
            return true;
        }

        public void Step(float elapsed)
        {
            if (Paused || elapsed <= 0) return;
            float remaining = Math.Min(elapsed, 0.25f);
            while (remaining > 0.00001f)
            {
                float delta = Math.Min(remaining, 0.05f);
                Battery = Math.Min(BatteryCapacity, Battery + Generation * delta);
                StepRover(delta);
                StepExtractors(delta);
                StepTrain(delta);
                remaining -= delta;
            }
        }

        private void StepRover(float delta)
        {
            float distanceBudget = Math.Min(2 * delta, Battery / 2);
            while (distanceBudget > 0.00001f && RoverMoving)
            {
                var target = roverRoute[roverWaypoint];
                if (StructureAt(target) != null) { roverRoute.Clear(); Message = "Rover route blocked by construction. Choose a new destination."; break; }
                float offsetX = target.X - RoverX;
                float offsetY = target.Y - RoverY;
                float distance = (float)Math.Sqrt(offsetX * offsetX + offsetY * offsetY);
                if (distance < 0.0001f) { roverWaypoint++; continue; }
                float travel = Math.Min(distanceBudget, distance);
                RoverX += offsetX / distance * travel;
                RoverY += offsetY / distance * travel;
                Battery = Math.Max(0, Battery - travel * 2);
                distanceBudget -= travel;
                Reveal(RoverX, RoverY, 3);
                if (travel >= distance - 0.0001f) roverWaypoint++;
            }
        }

        private void StepExtractors(float delta)
        {
            Demand = 0;
            foreach (var structure in Structures)
            {
                structure.SuppliedFraction = 0;
                if (Working(structure)) Demand += structure.Demand;
            }
            if (Demand <= 0) return;
            float supplied = Math.Min(Math.Max(0, Battery - Reserve), Demand * delta);
            float fraction = supplied / (Demand * delta);
            Battery -= supplied;
            foreach (var structure in Structures)
            {
                if (!Working(structure)) continue;
                structure.SuppliedFraction = fraction;
                structure.Progress += structure.Rate * delta * fraction;
                while (structure.Progress >= 1 && structure.Stock < structure.Storage)
                {
                    structure.Progress -= 1;
                    structure.Stock++;
                    Produced++;
                }
            }
        }

        private static bool Working(Structure structure) => structure.Kind == StructureKind.Extractor && structure.Connected && !structure.Paused && structure.Stock < structure.Storage;

        private void BeginLeg(bool homeward)
        {
            Train.Leg = new List<Cell>(Train.Route);
            if (homeward) Train.Leg.Reverse();
            Train.Waypoint = 0;
            Train.Phase = homeward ? TrainPhase.ToColony : TrainPhase.ToMine;
        }

        private void StepTrain(float delta)
        {
            if (Train.Phase == TrainPhase.Parked) return;
            if (Train.Phase == TrainPhase.ToMine || Train.Phase == TrainPhase.ToColony)
            {
                float budget = delta * 2;
                while (budget > 0 && Train.Waypoint < Train.Leg.Count)
                {
                    var target = Train.Leg[Train.Waypoint];
                    float offsetX = target.X - Train.X;
                    float offsetY = target.Y - Train.Y;
                    float distance = (float)Math.Sqrt(offsetX * offsetX + offsetY * offsetY);
                    if (distance < 0.0001f) { Train.Waypoint++; continue; }
                    float travel = Math.Min(budget, distance);
                    Train.X += offsetX / distance * travel;
                    Train.Y += offsetY / distance * travel;
                    budget -= travel;
                    if (travel >= distance - 0.0001f) Train.Waypoint++;
                }
                if (Train.Waypoint >= Train.Leg.Count)
                {
                    Train.Phase = Train.Phase == TrainPhase.ToMine ? TrainPhase.Loading : TrainPhase.Unloading;
                    Train.Dwell = 1;
                }
                return;
            }
            Train.Dwell -= delta;
            if (Train.Dwell > 0) return;
            if (Train.Phase == TrainPhase.Loading)
            {
                if (Train.ParkRequested) { BeginLeg(true); return; }
                int loaded = Math.Min(Train.Capacity, Train.Source.Stock);
                if (loaded == 0) return;
                Train.Source.Stock -= loaded;
                Train.Cargo = loaded;
                BeginLeg(true);
            }
            else
            {
                if (Train.Cargo > 0 && Train.Resource == ResourceKind.Ore)
                {
                    int payment = Train.Cargo * 8;
                    Credits += payment;
                    Sold += Train.Cargo;
                    Deliveries++;
                    Message = $"Delivery received! +{payment} credits. Explore farther for higher-yield deposits.";
                    Train.Cargo = 0;
                }
                if (Train.ParkRequested) { Train.Phase = TrainPhase.Parked; Train.Source = null; }
                else BeginLeg(false);
            }
        }

        public List<Cell> FindPath(Cell start, Cell end, Func<Cell, bool> allowed)
        {
            if (!InBounds(start) || !InBounds(end) || !allowed(end)) return null;
            var frontier = new Queue<Cell>();
            var previous = new Dictionary<Cell, Cell> { [start] = start };
            frontier.Enqueue(start);
            while (frontier.Count > 0)
            {
                var current = frontier.Dequeue();
                if (current.Equals(end))
                {
                    var result = new List<Cell> { end };
                    while (!current.Equals(start)) { current = previous[current]; result.Add(current); }
                    result.Reverse();
                    return result;
                }
                foreach (var direction in Directions)
                {
                    var next = current + direction;
                    if (!InBounds(next) || previous.ContainsKey(next) || !allowed(next)) continue;
                    previous[next] = current;
                    frontier.Enqueue(next);
                }
            }
            return null;
        }

        private bool Fail(string reason) { Message = reason; return false; }
    }
}
