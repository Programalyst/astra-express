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
    public enum StructureKind { Colony, Solar, Extractor, PowerPlant }
    public enum TrainPhase { Parked, ToMine, Loading, ToColony, Unloading, ReturningToDepot }

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
        public float BurnEnergy;
        public float Generation;
        public Cell Port => new Cell(Origin.X, Origin.Y - 1);
        public int Storage => Kind == StructureKind.PowerPlant ? 48 : 24 * Size;
        public float Demand => Size * Level;
        public float Rate => Deposit == null ? 0 : Deposit.Rate * Level;
        public bool Contains(Cell cell) => cell.X >= Origin.X && cell.X < Origin.X + Size && cell.Y >= Origin.Y && cell.Y < Origin.Y + Size;
    }

    public sealed class FreightTrain
    {
        public TrainPhase Phase;
        public Structure Source;
        public Structure Destination;
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
        public const int Width = 32;
        public const int Height = 32;
        public const float BatteryCapacity = 100;
        public const float Reserve = 10;
        public const int PlantCost = 250;
        public const int TrainCost = 150;
        public const int MaxTrains = 4;
        public const float PlantOutput = 8;
        public const float FuelEnergy = 40;
        public static readonly Cell[] Directions = { new Cell(1, 0), new Cell(-1, 0), new Cell(0, 1), new Cell(0, -1) };
        public readonly bool[,] Revealed = new bool[Width, Height];
        public readonly TerrainGrid Terrain = new TerrainGrid();
        public readonly List<Deposit> Deposits = new List<Deposit>();
        public readonly List<Structure> Structures = new List<Structure>();
        public readonly HashSet<Cell> Conduits = new HashSet<Cell>();
        public readonly HashSet<Cell> Rails = new HashSet<Cell>();
        public readonly HashSet<Cell> PoweredCells = new HashSet<Cell>();
        public readonly FreightTrain Train = new FreightTrain();
        public readonly List<FreightTrain> Trains = new List<FreightTrain>();
        public readonly Structure Colony;
        public int Credits { get; private set; } = 500;
        public float Battery { get; private set; } = BatteryCapacity;
        public float Generation { get; private set; }
        public float SolarGeneration { get; private set; }
        public float FuelGeneration => Structures.Sum(structure => structure.Generation);
        public float Demand { get; private set; }
        public float RoverX { get; private set; } = 7;
        public float RoverY { get; private set; } = 6;
        public int Produced { get; private set; }
        public int Sold { get; private set; }
        public int Deliveries { get; private set; }
        public int FuelProduced { get; private set; }
        public int FuelDelivered { get; private set; }
        public int FuelConsumed { get; private set; }
        public int Revision { get; private set; }
        public int RevealRevision { get; private set; }
        public bool Paused;
        public string Message = "Welcome, commander. Explore east of the colony to discover your first ore deposit.";
        public bool RoverMoving => roverWaypoint < roverRoute.Count;
        public Cell RoverCell => new Cell((int)Math.Round(RoverX), (int)Math.Round(RoverY));
        public int AccountedOre => Structures.Where(structure => structure.Deposit?.Resource == ResourceKind.Ore).Sum(structure => structure.Stock) + Trains.Where(train => train.Resource == ResourceKind.Ore).Sum(train => train.Cargo) + Sold;
        public int AccountedFuel => Structures.Where(structure => structure.Kind == StructureKind.PowerPlant || structure.Deposit?.Resource == ResourceKind.Fluxite).Sum(structure => structure.Stock) + Trains.Where(train => train.Resource == ResourceKind.Fluxite).Sum(train => train.Cargo) + FuelConsumed;
        private List<Cell> roverRoute = new List<Cell>();
        private readonly HashSet<Structure> manuallyStoppedServices = new HashSet<Structure>();
        private int roverWaypoint;

        public ColonySimulation()
        {
            Colony = new Structure { Kind = StructureKind.Colony, Origin = new Cell(5, 7), Size = 2, Starter = true, Connected = true };
            Structures.Add(Colony);
            Structures.Add(new Structure { Kind = StructureKind.Solar, Origin = new Cell(2, 7), Size = 2, Starter = true, Connected = true });
            Deposits.Add(new Deposit { Origin = new Cell(10, 4), Size = 1 });
            Deposits.Add(new Deposit { Origin = new Cell(12, 16), Size = 1 });
            Deposits.Add(new Deposit { Origin = new Cell(13, 8), Size = 1, Resource = ResourceKind.Fluxite });
            Deposits.Add(new Deposit { Origin = new Cell(4, 23), Size = 2, Resource = ResourceKind.Fluxite });
            Deposits.Add(new Deposit { Origin = new Cell(22, 16), Size = 2 });
            Conduits.Add(Colony.Port);
            Rails.Add(Colony.Port);
            Train.X = Colony.Port.X;
            Train.Y = Colony.Port.Y;
            Trains.Add(Train);
            Reveal(5.5f, 7.5f, 5);
            Reveal(RoverX, RoverY, 3);
            Reconnect();
        }

        public static bool InBounds(Cell cell) => cell.X >= 0 && cell.Y >= 0 && cell.X < Width && cell.Y < Height;
        public bool IsRevealed(Cell cell) => InBounds(cell) && Revealed[cell.X, cell.Y];
        public List<Cell> DecorativeRockCells()
        {
            var random = new Random(73421);
            var candidates = new List<Cell>();
            for (int column = 2; column < Width - 2; column++)
                for (int row = 2; row < Height - 2; row++)
                {
                    var cell = new Cell(column, row);
                    bool clear = true;
                    for (int offsetX = -1; offsetX <= 1; offsetX++)
                        for (int offsetY = -1; offsetY <= 1; offsetY++)
                        {
                            var neighbor = new Cell(column + offsetX, row + offsetY);
                            if (Terrain.Kind(neighbor) != TerrainKind.Flat || DepositAt(neighbor) != null
                                || StructureAt(neighbor) != null || neighbor.Equals(RoverCell)) clear = false;
                        }
                    if (clear) candidates.Add(cell);
                }
            for (int index = candidates.Count - 1; index > 0; index--)
            {
                int other = random.Next(index + 1);
                var previous = candidates[index];
                candidates[index] = candidates[other];
                candidates[other] = previous;
            }
            var rocks = new List<Cell>();
            foreach (var cell in candidates)
            {
                if (rocks.Any(other => Math.Abs(other.X - cell.X) <= 2 && Math.Abs(other.Y - cell.Y) <= 2)) continue;
                rocks.Add(cell);
                if (rocks.Count == 24) break;
            }
            return rocks;
        }
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

        public void StopRover()
        {
            roverRoute.Clear();
            roverWaypoint = 0;
        }

        public bool OrderRover(Cell destination)
        {
            if (!Terrain.Walkable(destination) || StructureAt(destination) != null) return Fail("The rover needs clear ground. Use the marked ramps to reach the plateau; hillsides are impassable.");
            var route = FindPath(RoverCell, destination, cell => StructureAt(cell) == null);
            if (route == null) return Fail("No walkable route to that destination.");
            roverRoute = route;
            roverWaypoint = 0;
            Message = "Rover exploring. Movement costs 2 power per tile-equivalent of surface distance; ramps take slightly more.";
            return true;
        }

        public bool CanBuild(StructureKind kind, Cell requested, out Cell origin, out int size, out int cost, out string reason)
        {
            origin = requested;
            size = kind == StructureKind.Solar || kind == StructureKind.PowerPlant ? 2 : 1;
            cost = kind == StructureKind.PowerPlant ? PlantCost : kind == StructureKind.Solar ? 100 : 150;
            reason = "";
            if (!IsRevealed(requested)) { reason = "Explore this ground first."; return false; }
            if (kind != StructureKind.Solar && kind != StructureKind.Extractor && kind != StructureKind.PowerPlant) { reason = "The colony is fixed."; return false; }
            var deposit = DepositAt(requested);
            if (kind == StructureKind.Extractor)
            {
                if (deposit == null) { reason = "Place an extractor on a discovered ore or Fluxite patch."; return false; }
                if (!FullyRevealed(deposit)) { reason = "Explore the entire patch before placing an extractor."; return false; }
                origin = deposit.Origin;
                size = deposit.Size;
                cost = deposit.Price;
                if (deposit.Extractor != null) { reason = "This deposit already has an extractor."; return false; }
            }
            foreach (var cell in Footprint(origin, size))
            {
                if (!IsRevealed(cell)) { reason = "The complete footprint must be explored."; return false; }
                if (Terrain.Kind(cell) != TerrainKind.Flat || Terrain.Elevation(cell) != Terrain.Elevation(origin))
                { reason = "Buildings need a level footprint. Keep ramps and hillsides clear."; return false; }
                if (StructureAt(cell) != null || Rails.Contains(cell) || Conduits.Contains(cell) || cell.Equals(RoverCell) || TrainOccupies(cell))
                { reason = "Footprint occupied. Leave room for vehicles and infrastructure."; return false; }
                if (kind != StructureKind.Extractor && DepositAt(cell) != null) { reason = "Keep resource deposits free for extractors."; return false; }
                if (Structures.Any(structure => structure.Port.Equals(cell))) { reason = "Keep the connection ports clear."; return false; }
            }
            var port = new Cell(origin.X, origin.Y - 1);
            if (!IsRevealed(port) || StructureAt(port) != null) { reason = "Explore and clear the port immediately south of the building."; return false; }
            if (Terrain.Kind(port) != TerrainKind.Flat || Terrain.Elevation(port) != Terrain.Elevation(origin))
            { reason = "The south port must be on level ground at the building's elevation."; return false; }
            if (Credits < cost) { reason = $"Need {cost} credits. Deliver ore to earn more."; return false; }
            return true;
        }

        private bool TrainOccupies(Cell cell) => Trains.Any(train => Math.Abs(train.X - cell.X) < 0.55f && Math.Abs(train.Y - cell.Y) < 0.55f);

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
            Message = kind == StructureKind.PowerPlant ? "Power plant built. Connect conduits and rails to its south port; an idle train will bring Fluxite automatically." : kind == StructureKind.Solar ? "Solar built. Wire its cyan port to the colony's power network." : "Extractor built. Connect power and rails; an idle train dispatches automatically when its route is complete.";
            if (kind == StructureKind.Extractor || kind == StructureKind.PowerPlant) AutoDispatchReadyServices();
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

        // Preview and placement share this planner. Try the player's preferred bend,
        // then its mirror, then an affordable detour across known traversable tiles.
        public bool TryPlanNetworkRoute(Cell start, Cell end, bool rail, bool preferVertical,
            out List<Cell> path, out int cost, out string reason)
        {
            return PlanNetworkRoute(start, end, rail, preferVertical, out path, out cost, out reason, null, 0);
        }

        public bool TryPlanSolarConnection(Cell requested, out List<Cell> path, out int cost, out string reason)
        {
            path = null; cost = 0;
            if (!CanBuild(StructureKind.Solar, requested, out var origin, out int size, out int buildingCost, out reason)) return false;
            var footprint = new HashSet<Cell>(Footprint(origin, size));
            return PlanNetworkRoute(Colony.Port, new Cell(origin.X, origin.Y - 1), false, false,
                out path, out cost, out reason, footprint, buildingCost);
        }

        private bool PlanNetworkRoute(Cell start, Cell end, bool rail, bool preferVertical,
            out List<Cell> path, out int cost, out string reason, ISet<Cell> excluded, int reservedCredits)
        {
            path = null; cost = 0; reason = "";
            if (!CanLay(new[] { start }, rail, out _, out reason) || !CanLay(new[] { end }, rail, out _, out reason)) return false;
            foreach (bool bend in new[] { preferVertical, !preferVertical })
            {
                var candidate = Corridor(start, end, bend);
                if ((excluded == null || !candidate.Any(excluded.Contains)) && CanLay(candidate, rail, out cost, out reason) && cost <= Credits - reservedCredits) { path = candidate; return true; }
            }
            var scores = SearchNetworkRoutes(start, rail, out var previous, excluded);
            if (!scores.ContainsKey(end)) { reason = "No clear explored route. Reveal more ground or use a ramp pass."; cost = 0; return false; }
            var detour = new List<Cell> { end };
            var cursor = end;
            while (!cursor.Equals(start)) { cursor = previous[cursor]; detour.Add(cursor); }
            detour.Reverse();
            if (!CanLay(detour, rail, out cost, out reason)) return false;
            if (cost > Credits - reservedCredits) { reason = $"Connection costs {cost} credits after reserving {reservedCredits} for the building; {Credits} available."; return false; }
            path = detour;
            return true;
        }

        // One search supplies the legal destination overlay, instead of a path search
        // for every tile each frame. Existing network tiles cost nothing to reuse.
        public Dictionary<Cell, int> NetworkConnectionCosts(Cell start, bool rail)
        {
            var scores = SearchNetworkRoutes(start, rail, out _);
            return scores.ToDictionary(pair => pair.Key, pair => pair.Value / 1024);
        }

        private Dictionary<Cell, int> SearchNetworkRoutes(Cell start, bool rail, out Dictionary<Cell, Cell> previous, ISet<Cell> excluded = null)
        {
            previous = new Dictionary<Cell, Cell>();
            var scores = new Dictionary<Cell, int>();
            bool Allowed(Cell cell) => IsRevealed(cell) && Terrain.Walkable(cell) && StructureAt(cell) == null && (excluded == null || !excluded.Contains(cell));
            if (!Allowed(start)) return scores;
            var network = rail ? Rails : Conduits;
            int price = rail ? 3 : 2;
            scores[start] = network.Contains(start) ? 0 : price * 1024;
            previous[start] = start;
            var frontier = new List<Cell> { start };
            var visited = new HashSet<Cell>();
            while (frontier.Count > 0)
            {
                int best = 0;
                for (int i = 1; i < frontier.Count; i++) if (scores[frontier[i]] < scores[frontier[best]]) best = i;
                var current = frontier[best]; frontier.RemoveAt(best);
                if (!visited.Add(current)) continue;
                foreach (var direction in Directions)
                {
                    var next = current + direction;
                    if (visited.Contains(next) || !Allowed(next) || !Terrain.CanTraverse(current, next)) continue;
                    // Fewer new tiles wins; distance breaks ties. Every simple route
                    // has fewer than 1024 steps on this 32 x 32 map.
                    int score = scores[current] + (network.Contains(next) ? 0 : price * 1024) + 1;
                    if (scores.TryGetValue(next, out int old) && old <= score) continue;
                    scores[next] = score; previous[next] = current;
                    if (!frontier.Contains(next)) frontier.Add(next);
                }
            }
            return scores;
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
                if (!Terrain.Walkable(cell)) { reason = "Hillsides are impassable. Route through a ramp pass."; return false; }
                if (index > 0 && !cell.Equals(path[index - 1]) && !Terrain.CanTraverse(path[index - 1], cell))
                { reason = "Use a ramp to change elevation. Routes must run straight up or down ramps; R changes the route bend."; return false; }
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
            Message = rail ? $"Railway built for {cost} credits." : $"Conduits built for {cost} credits. Cyan lines connect the shared power grid.";
            AutoDispatchReadyServices();
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
                    if (Conduits.Contains(next) && Terrain.CanTraverse(current, next) && PoweredCells.Add(next)) frontier.Enqueue(next);
                }
            }
            foreach (var structure in Structures) structure.Connected = structure.Starter || PoweredCells.Contains(structure.Port);
            SolarGeneration = Structures.Count(structure => structure.Kind == StructureKind.Solar && structure.Connected) * 2;
            Generation = SolarGeneration + FuelGeneration;
        }

        public List<Cell> RailRoute(Structure extractor) => FindPath(Colony.Port, extractor.Port, cell => Rails.Contains(cell));

        private void AutoDispatchReadyServices()
        {
            int started = 0;
            bool waitingForTrain = false;
            bool waitingForPlant = false;
            string previousMessage = Message;
            foreach (var extractor in Structures)
            {
                if (extractor.Kind != StructureKind.Extractor || manuallyStoppedServices.Contains(extractor)
                    || Trains.Any(train => train.Source == extractor) || RailRoute(extractor) == null) continue;
                var destination = extractor.Deposit.Resource == ResourceKind.Ore ? Colony : Structures
                    .Where(plant => plant.Kind == StructureKind.PowerPlant && RailRoute(plant) != null)
                    .OrderByDescending(plant => plant.Connected)
                    .ThenBy(plant => Math.Abs(plant.Port.X - extractor.Port.X) + Math.Abs(plant.Port.Y - extractor.Port.Y))
                    .FirstOrDefault();
                if (destination == null) { waitingForPlant = true; continue; }
                if (!Trains.Any(train => train.Phase == TrainPhase.Parked)) { waitingForTrain = true; continue; }
                if (Dispatch(extractor, destination)) started++;
            }
            Message = previousMessage;
            if (started > 0) Message += $" {started} train service(s) dispatched automatically.";
            if (waitingForTrain) Message += " No idle train for another ready route. Buy a train in Fleet for automatic assignment.";
            if (waitingForPlant) Message += " Fluxite awaits a rail-connected power plant.";
        }

        public bool Dispatch(Structure extractor, Structure destination = null)
        {
            if (extractor == null || extractor.Kind != StructureKind.Extractor) return Fail("Select an extractor first.");
            if (Trains.Any(train => train.Source == extractor)) return Fail("This extractor already has a train service.");
            var available = Trains.FirstOrDefault(train => train.Phase == TrainPhase.Parked);
            if (available == null) return Fail("No idle train. Buy a locomotive in Fleet, or park an existing service.");
            if (extractor.Deposit.Resource == ResourceKind.Ore) destination = Colony;
            else if (destination == null || destination.Kind != StructureKind.PowerPlant || !Structures.Contains(destination)) return Fail("Choose a power plant as this Fluxite extractor's destination.");
            var route = RailRoute(extractor);
            if (route == null) return Fail("Connect rails between the colony depot and the extractor's south port.");
            var deliveryRoute = FindPath(destination.Port, extractor.Port, cell => Rails.Contains(cell));
            if (deliveryRoute == null) return Fail("Connect rails from the extractor to the selected destination's south port.");
            available.Source = extractor;
            available.Destination = destination;
            available.Resource = extractor.Deposit.Resource;
            available.Route = deliveryRoute;
            available.ParkRequested = false;
            manuallyStoppedServices.Remove(extractor);
            BeginLeg(available, false);
            available.Leg = route;
            Message = available.Resource == ResourceKind.Ore ? "Ore service started. Each delivery earns credits at the colony." : "Fluxite service started. Fuel goes to the plant, not the ore buyer.";
            return true;
        }

        public bool BuyTrain()
        {
            if (Trains.Count >= MaxTrains) return Fail("Fleet is full: four locomotives maximum.");
            if (Credits < TrainCost) return Fail($"Need {TrainCost} credits for another locomotive.");
            Credits -= TrainCost;
            Trains.Add(new FreightTrain { X = Colony.Port.X, Y = Colony.Port.Y });
            Revision++;
            Message = "Locomotive purchased. Ready routes are assigned automatically.";
            AutoDispatchReadyServices();
            return true;
        }

        public void ParkTrain(FreightTrain train = null)
        {
            train = train ?? Train;
            if (train.Phase == TrainPhase.Parked) return;
            if (train.Source != null) manuallyStoppedServices.Add(train.Source);
            train.ParkRequested = true;
            if (train.Phase == TrainPhase.Loading) ReturnToDepot(train);
            Message = "Train will finish any cargo delivery and return to the colony. Full plants must make room before fuel unloads.";
        }

        public bool UpgradeTrain(FreightTrain train = null)
        {
            train = train ?? Train;
            if (train.CapacityLevel >= 3) return Fail("Train capacity is fully upgraded.");
            int cost = train.CapacityLevel * 100;
            if (Credits < cost) return Fail($"Need {cost} credits for this upgrade.");
            Credits -= cost;
            train.CapacityLevel++;
            train.Capacity = train.CapacityLevel * 4;
            Message = $"Train upgraded to {train.Capacity} cargo. Additional capacity is used at the next loading stop.";
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
                StepGeneration(delta);
                StepRover(delta);
                StepExtractors(delta);
                foreach (var train in Trains) StepTrain(train, delta);
                remaining -= delta;
            }
        }

        private void StepGeneration(float delta)
        {
            Battery = Math.Min(BatteryCapacity, Battery + SolarGeneration * delta);
            foreach (var plant in Structures)
            {
                plant.Generation = 0;
                if (plant.Kind != StructureKind.PowerPlant || !plant.Connected || plant.Paused) continue;
                float requested = Math.Min(PlantOutput * delta, BatteryCapacity - Battery);
                if (requested <= 0.00001f) continue;
                if (plant.BurnEnergy <= 0.00001f && plant.Stock > 0)
                {
                    plant.Stock--;
                    FuelConsumed++;
                    plant.BurnEnergy += FuelEnergy;
                }
                float generated = Math.Min(requested, plant.BurnEnergy);
                plant.BurnEnergy -= generated;
                plant.Generation = generated / delta;
                Battery += generated;
            }
            Generation = SolarGeneration + FuelGeneration;
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
                float column = RoverX;
                float row = RoverY;
                float spent = Terrain.MoveTowards(ref column, ref row, target, distanceBudget);
                RoverX = column;
                RoverY = row;
                Battery = Math.Max(0, Battery - spent * 2);
                distanceBudget -= spent;
                Reveal(RoverX, RoverY, 3);
                if (Math.Abs(RoverX - target.X) + Math.Abs(RoverY - target.Y) < 0.0001f) roverWaypoint++;
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
                    if (structure.Deposit.Resource == ResourceKind.Ore) Produced++;
                    else FuelProduced++;
                }
            }
        }

        private static bool Working(Structure structure) => structure.Kind == StructureKind.Extractor && structure.Connected && !structure.Paused && structure.Stock < structure.Storage;

        private void BeginLeg(FreightTrain train, bool homeward)
        {
            train.Leg = new List<Cell>(train.Route);
            if (homeward) train.Leg.Reverse();
            train.Waypoint = 0;
            train.Phase = homeward ? TrainPhase.ToColony : TrainPhase.ToMine;
        }

        private void ReturnToDepot(FreightTrain train)
        {
            train.Leg = FindPath(new Cell((int)Math.Round(train.X), (int)Math.Round(train.Y)), Colony.Port, cell => Rails.Contains(cell));
            train.Waypoint = 0;
            train.Phase = TrainPhase.ReturningToDepot;
        }

        private void StepTrain(FreightTrain train, float delta)
        {
            if (train.Phase == TrainPhase.Parked) return;
            if (train.Phase == TrainPhase.ToMine || train.Phase == TrainPhase.ToColony || train.Phase == TrainPhase.ReturningToDepot)
            {
                float budget = delta * 2;
                while (budget > 0 && train.Waypoint < train.Leg.Count)
                {
                    var target = train.Leg[train.Waypoint];
                    float offsetX = target.X - train.X;
                    float offsetY = target.Y - train.Y;
                    float distance = (float)Math.Sqrt(offsetX * offsetX + offsetY * offsetY);
                    if (distance < 0.0001f) { train.Waypoint++; continue; }
                    budget -= Terrain.MoveTowards(ref train.X, ref train.Y, target, budget);
                    if (Math.Abs(train.X - target.X) + Math.Abs(train.Y - target.Y) < 0.0001f) train.Waypoint++;
                }
                if (train.Waypoint >= train.Leg.Count)
                {
                    if (train.Phase == TrainPhase.ReturningToDepot)
                    {
                        train.Phase = TrainPhase.Parked;
                        train.Source = null;
                        train.Destination = null;
                        train.ParkRequested = false;
                    }
                    else if (train.Phase == TrainPhase.ToMine && train.ParkRequested) ReturnToDepot(train);
                    else
                    {
                        train.Phase = train.Phase == TrainPhase.ToMine ? TrainPhase.Loading : TrainPhase.Unloading;
                        train.Dwell = 1;
                    }
                }
                return;
            }
            train.Dwell -= delta;
            if (train.Dwell > 0) return;
            if (train.Phase == TrainPhase.Loading)
            {
                if (train.ParkRequested) { ReturnToDepot(train); return; }
                int loaded = Math.Min(train.Capacity, train.Source.Stock);
                if (loaded == 0) return;
                train.Source.Stock -= loaded;
                train.Cargo = loaded;
                BeginLeg(train, true);
            }
            else
            {
                if (train.Cargo > 0 && train.Resource == ResourceKind.Ore)
                {
                    int payment = train.Cargo * 8;
                    Credits += payment;
                    Sold += train.Cargo;
                    Deliveries++;
                    Message = $"Delivery received! +{payment} credits. Explore farther for higher-yield deposits.";
                    train.Cargo = 0;
                }
                else if (train.Cargo > 0)
                {
                    int unloaded = Math.Min(train.Cargo, train.Destination.Storage - train.Destination.Stock);
                    train.Destination.Stock += unloaded;
                    train.Cargo -= unloaded;
                    FuelDelivered += unloaded;
                    if (unloaded > 0) Message = $"{unloaded} Fluxite delivered to the power plant. Fuel generates power, not credits.";
                    if (train.Cargo > 0) return;
                }
                if (train.ParkRequested) ReturnToDepot(train);
                else BeginLeg(train, false);
            }
        }

        public List<Cell> FindPath(Cell start, Cell end, Func<Cell, bool> allowed)
        {
            if (!Terrain.Walkable(start) || !Terrain.Walkable(end) || !allowed(end)) return null;
            var frontier = new List<Cell> { start };
            var previous = new Dictionary<Cell, Cell> { [start] = start };
            var costs = new Dictionary<Cell, float> { [start] = 0 };
            while (frontier.Count > 0)
            {
                int best = 0;
                for (int index = 1; index < frontier.Count; index++)
                {
                    var candidate = frontier[index];
                    var incumbent = frontier[best];
                    if (costs[candidate] + Math.Abs(candidate.X - end.X) + Math.Abs(candidate.Y - end.Y) < costs[incumbent] + Math.Abs(incumbent.X - end.X) + Math.Abs(incumbent.Y - end.Y)) best = index;
                }
                var current = frontier[best];
                frontier.RemoveAt(best);
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
                    if (!Terrain.CanTraverse(current, next) || !allowed(next)) continue;
                    float cost = costs[current] + Terrain.EdgeCost(current, next);
                    if (costs.TryGetValue(next, out float previousCost) && cost >= previousCost) continue;
                    costs[next] = cost;
                    previous[next] = current;
                    if (!frontier.Contains(next)) frontier.Add(next);
                }
            }
            return null;
        }

        private bool Fail(string reason) { Message = reason; return false; }
    }
}
