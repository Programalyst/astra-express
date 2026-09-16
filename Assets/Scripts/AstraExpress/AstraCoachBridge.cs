using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

namespace AstraExpress
{
    public sealed partial class AstraGame
    {
        private bool coachInputBlocked;
        private int coachInputResumeFrame;
        private float coachTimer;
        private string coachSession;
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void AstraCoachPublish(string json);
        [DllImport("__Internal")] private static extern void AstraCoachScreenshot(string request, string jpeg);
#endif
        [Serializable] private sealed class CoachPoint
        {
            public int x, y;
            public float screenX, screenY;
            public bool visible;
        }
        [Serializable] private sealed class CoachRoute
        {
            public bool possible;
            public int cost, nextSegment;
            public string reason;
            public CoachPoint[] stops;
        }
        [Serializable] private sealed class CoachBuilding
        {
            public string kind, resource;
            public CoachPoint origin, port, destination, destinationPort;
            public bool connected, paused, railConnected, served, destinationRailConnected, disabled;
            public int stock, storage, level, size, servedBy;
            public float demand, rate, generation, burnEnergy, suppliedFraction, health, repairRemaining;
            public CoachRoute powerRoute, railRoute, destinationRailRoute;
        }
        [Serializable] private sealed class CoachDeposit
        {
            public CoachPoint origin;
            public int size, cost;
            public bool buildable;
            public string reason, resource;
        }
        [Serializable] private sealed class CoachTrain
        {
            public int index, cargo, capacity, capacityLevel;
            public string phase, status, resource;
            public bool parkRequested, waitingForFuelSpace;
            public CoachPoint position, owner, source, destination;
        }
        [Serializable] private sealed class CoachAnchor
        {
            public string id, label;
            public float x, y, width, height;
            public bool visible = true;
        }
        private CoachAnchor[] CoachAnchors()
        {
            var anchors = new List<CoachAnchor>();
            void Add(string id, string label, Rect rect) => anchors.Add(new CoachAnchor { id = id, label = label, x = rect.x / UiWidth, y = rect.y / UiHeight, width = rect.width / UiWidth, height = rect.height / UiHeight });
            string[] names = { "explore", "extractor", "solar", "conduit", "rail", "plant", "turret" };
            for (int i = 0; i < names.Length; i++) Add("tool-" + names[i], names[i], new Rect(28 + i * 147, UiHeight - 75, 139, 44));
            Add("pause", Simulation.Paused ? "Resume" : "Pause", new Rect(UiWidth - 214, 17, 92, 36));
            if (RepairPanel != Rect.zero) Add("repair-building", "Repair building", new Rect(RepairPanel.x + 12, RepairPanel.y + 26, RepairPanel.width - 24, 29));
            if (ConnectionPanelVisible) Add("connection-cancel", NetworkTool ? "Cancel connection" : "Dismiss connection status", ConnectionCancelRect);
            if (!SidebarVisible || NetworkTool) return anchors.ToArray();
            if (ConnectionSelection)
            {
                Add(selected.Kind == StructureKind.Extractor ? "primary-action" : selected.Kind == StructureKind.PowerPlant ? "plant-connect" : "solar-connect",
                    !selected.Connected ? "Show power connection" : "Show rail connection", ConnectionActionRect);
                return anchors.ToArray();
            }
            if (selected != null && selected.Kind == StructureKind.Extractor)
            {
                Add("primary-action", "Mine action", new Rect(Sidebar.x + 18, 356, Sidebar.width - 36, 36));
                Add("train-capacity", "Upgrade train capacity", new Rect(Sidebar.x + 18, 457, Sidebar.width - 36, 34));
                Add("mine-pause", selected.Paused ? "Resume mine" : "Pause mine", new Rect(Sidebar.x + 18, 416, 112, 34));
                if (selected.Deposit.Resource == ResourceKind.Fluxite) Add("fuel-destination", "Choose fuel destination", new Rect(Sidebar.x + 18, 324, Sidebar.width - 36, 26));
            }
            else if (selected != null && selected.Kind == StructureKind.PowerPlant) {
                bool needsLink = !selected.Connected || Simulation.RailRoute(selected) == null;
                Add("plant-pause", selected.Paused ? "Resume plant" : "Pause plant", new Rect(Sidebar.x + 18, 312 + (needsLink ? 39 : 0), Sidebar.width - 36, 32));
            }
            return anchors.ToArray();
        }
        private CoachAnchor[] CoachPanels()
        {
            var panels = new List<CoachAnchor>();
            void Add(string id, Rect rect) => panels.Add(new CoachAnchor { id = id, x = rect.x / UiWidth, y = rect.y / UiHeight, width = rect.width / UiWidth, height = rect.height / UiHeight });
            Add("header", new Rect(0, 0, UiWidth, 72));
            if (DefensePanel != Rect.zero) Add("defense", DefensePanel);
            if (RepairPanel != Rect.zero) Add("repair", RepairPanel);
            if (ObjectiveVisible) Add("objective", ObjectivePanel);
            if (SidebarVisible) Add("sidebar", Sidebar);
            if (ConnectionPanelVisible) Add("connection", LinkGuidePanel);
            return panels.ToArray();
        }
        [Serializable] private sealed class CoachState
        {
            public string session, tool, message, selectedKind, trainPhase, placementReason;
            public string botActionId, botActionStatus, botActionMessage, companionMode;
            public bool botBusy, pickingTile, hasPickedTile, nativeCompanion, smartRouting = true;
            public CoachPoint pickedTile, botTarget;
            public CoachPoint[] connectionTargets;
            public CoachAnchor[] uiAnchors, uiPanels;
            public CoachRoute solarSitePowerRoute, pickedSitePowerRoute;
            public int credits, produced, sold, deliveries, capacity, capacityLevel, cargo;
            public int selectedTrainIndex, idleTrains, trainCount, plantCost, fuelProduced, fuelDelivered, fuelConsumed;
            public float battery, generation, demand, elapsed, solarGeneration, fuelGeneration, plantOutput, fuelEnergy, companionScreenX, companionScreenY, companionDockX, companionDockY;
            public bool paused, roverMoving, routeStarted, trainParkRequested, trainSelected, extractorOwnedTrains = true;
            public CoachPoint rover, colonyPort, selected, routeStart, frontier, solarSite, plantSite, fuelDestination;
            public CoachBuilding[] buildings;
            public CoachDeposit[] deposits;
            public CoachTrain[] trains;
            public bool raidsStarted;
            public int waveNumber, aliensDefeated;
            public float nextWaveIn;
            public CoachPoint[] visibleAliens;
        }
        private void ResetCoach() { coachSession = Guid.NewGuid().ToString("N"); coachTimer = 1; }

        // Coaching UI and camera methods; opt-in game control is isolated in AstraBotControl.
        public void CoachSetInputBlocked(string value)
        {
            if (value == "2") coachInputResumeFrame = Time.frameCount + 1;
            coachInputBlocked = value == "1";
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLInput.captureAllKeyboardInput = !coachInputBlocked;
#endif
        }
        public void CoachFocus(string coordinates)
        {
            string[] parts = coordinates.Split(',');
            if (parts.Length != 2 || !int.TryParse(parts[0], out int x) || !int.TryParse(parts[1], out int y)) return;
            var cell = new Cell(x, y);
            if (!ColonySimulation.InBounds(cell)) return;
            StopFollowingRover();
            cameraTarget = Position(cell);
            PositionCamera();
            coachTimer = 1;
        }
        public void CoachCapture(string request)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            StartCoroutine(CaptureCoachFrame(request));
#endif
        }
#if UNITY_WEBGL && !UNITY_EDITOR
        private IEnumerator CaptureCoachFrame(string request)
        {
            yield return new WaitForEndOfFrame();
            Texture2D full = null, small = null;
            RenderTexture target = null;
            RenderTexture previous = RenderTexture.active;
            string jpeg = "";
            try
            {
                full = ScreenCapture.CaptureScreenshotAsTexture();
                float scale = Mathf.Min(1, 1280f / full.width, 960f / full.height);
                int width = Mathf.Max(1, Mathf.RoundToInt(full.width * scale));
                int height = Mathf.Max(1, Mathf.RoundToInt(full.height * scale));
                target = RenderTexture.GetTemporary(width, height, 0);
                Graphics.Blit(full, target);
                RenderTexture.active = target;
                small = new Texture2D(width, height, TextureFormat.RGB24, false);
                small.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                small.Apply();
                jpeg = Convert.ToBase64String(small.EncodeToJPG(78));
            }
            catch (Exception exception) { Debug.LogWarning("Coach frame capture failed: " + exception.Message); }
            finally
            {
                RenderTexture.active = previous;
                if (target != null) RenderTexture.ReleaseTemporary(target);
                if (full != null) Destroy(full);
                if (small != null) Destroy(small);
            }
            AstraCoachScreenshot(request, jpeg);
        }
#endif
        private CoachPoint CoachPosition(Cell cell)
        {
            Vector3 point = worldCamera.WorldToViewportPoint(Position(cell, 0.04f));
            var guiPoint = new Vector2(point.x * UiWidth, (1 - point.y) * UiHeight);
            return new CoachPoint { x = cell.X, y = cell.Y, screenX = point.x, screenY = 1 - point.y,
                visible = point.z > 0 && point.x > 0.02f && point.x < 0.98f && guiPoint.y > 74 && guiPoint.y < UiHeight - 132 && !ContextPanelContains(guiPoint) };
        }
        private CoachRoute CoachPath(Structure building, bool rail)
        {
            if (!Simulation.TryPlanNetworkRoute(Simulation.Colony.Port, building.Port, rail, false, out var best, out int bestCost, out string reason))
                return new CoachRoute { possible = false, reason = reason, stops = Array.Empty<CoachPoint>() };
            var stops = new List<Cell> { best[0] };
            for (int i = 1; i + 1 < best.Count; i++)
                if (best[i].X - best[i - 1].X != best[i + 1].X - best[i].X || best[i].Y - best[i - 1].Y != best[i + 1].Y - best[i].Y) stops.Add(best[i]);
            if (!best[best.Count - 1].Equals(stops[stops.Count - 1])) stops.Add(best[best.Count - 1]);
            return new CoachRoute { possible = true, cost = bestCost, nextSegment = LinkSegmentIndex(stops, rail), reason = "", stops = stops.Select(CoachPosition).ToArray() };
        }

        private CoachRoute CoachSolarRoute(Cell cell)
        {
            bool possible = Simulation.TryPlanSolarConnection(cell, out var path, out int cost, out string reason);
            return new CoachRoute { possible = possible, cost = cost, reason = reason,
                stops = path == null ? Array.Empty<CoachPoint>() : new[] { CoachPosition(path[0]), CoachPosition(path[path.Count - 1]) } };
        }

        // Read the same destination the extractor sidebar presents. This never assigns
        // a service or changes the player's selected destination.
        private Structure CoachFuelDestination(Structure extractor)
        {
            if (extractor == null || extractor.Kind != StructureKind.Extractor || extractor.Deposit.Resource != ResourceKind.Fluxite) return null;
            var assigned = Simulation.Trains.FirstOrDefault(train => train.Source == extractor);
            if (assigned?.Destination != null) return assigned.Destination;
            if (fuelDestination != null && fuelDestination.Kind == StructureKind.PowerPlant && Simulation.Structures.Contains(fuelDestination)) return fuelDestination;
            return Simulation.Structures.FirstOrDefault(building => building.Kind == StructureKind.PowerPlant);
        }

        private CoachBuilding CoachBuildingState(Structure building)
        {
            bool extractor = building.Kind == StructureKind.Extractor;
            bool plant = building.Kind == StructureKind.PowerPlant;
            int servedBy = Simulation.Trains.FindIndex(train => train.Phase != TrainPhase.Parked && (extractor ? train.Source == building : plant && train.Destination == building));
            Structure destination = extractor ? building.Deposit.Resource == ResourceKind.Fluxite ? CoachFuelDestination(building) : Simulation.Colony : null;
            bool destinationRails = destination != null && Simulation.FindPath(building.Port, destination.Port, cell => Simulation.Rails.Contains(cell)) != null;
            return new CoachBuilding {
                kind = building.Kind.ToString(), resource = extractor ? building.Deposit.Resource.ToString() : plant ? ResourceKind.Fluxite.ToString() : "",
                origin = CoachPosition(building.Origin), port = CoachPosition(building.Port), size = building.Size,
                connected = building.Connected, paused = building.Paused, stock = building.Stock, storage = building.Storage, level = building.Level,
                health = building.Health, disabled = building.Disabled, repairRemaining = building.RepairRemaining,
                demand = extractor ? building.Demand : 0, rate = building.Rate, generation = building.Kind == StructureKind.Solar && building.Connected && !building.Disabled ? 2 : building.Generation,
                burnEnergy = building.BurnEnergy, suppliedFraction = building.SuppliedFraction, served = servedBy >= 0, servedBy = servedBy,
                railConnected = (extractor || plant) && Simulation.RailRoute(building) != null,
                powerRoute = building.Connected ? null : CoachPath(building, false),
                railRoute = extractor || plant ? CoachPath(building, true) : null,
                destination = destination == null ? null : CoachPosition(destination.Origin),
                destinationPort = destination == null ? null : CoachPosition(destination.Port),
                destinationRailConnected = destinationRails,
                // Mines first join the depot network. A plant linked to that same
                // network can receive fuel; no extra extractor-to-plant line is needed.
                destinationRailRoute = destination != null && destination != Simulation.Colony && !destinationRails ? CoachPath(destination, true) : null
            };
        }

        private void PublishCoach()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            coachTimer += Time.unscaledDeltaTime;
            if (coachTimer < 0.75f) return;
            coachTimer = 0;
            var currentTrain = Simulation.TrainFor(selected) ?? Simulation.Train;
            int trainIndex = currentTrain == null ? -1 : Simulation.Trains.IndexOf(currentTrain);
            var currentDestination = CoachFuelDestination(selected);
            Vector3 companionView = companionVisual != null ? worldCamera.WorldToViewportPoint(CompanionCenter()) : new Vector3(-1, -1, 0);
            var state = new CoachState {
                session = coachSession, elapsed = Time.realtimeSinceStartup, tool = tool.ToString(), nativeCompanion = AstraBotModel != null,
                companionMode = this.companionMode, companionScreenX = companionView.x, companionScreenY = companionView.y,
                companionDockX = companionDockViewport.x, companionDockY = companionDockViewport.y,
                connectionTargets = networkPortOptions.Where(b => !routeStart.HasValue || !b.Port.Equals(routeStart.Value)).OrderBy(b => b.Starter ? 0 : 1).Select(b => CoachPosition(b.Port)).ToArray(),
                botActionId = botActionId, botActionStatus = botActionStatus, botActionMessage = botActionMessage, botBusy = botBusy, pickingTile = pickingTile, hasPickedTile = pickedTile.HasValue,
                pickedTile = pickedTile.HasValue ? CoachPosition(pickedTile.Value) : null,
                botTarget = botTarget.HasValue ? CoachPosition(botTarget.Value) : null, uiAnchors = CoachAnchors(), uiPanels = CoachPanels(),
                credits = Simulation.Credits, battery = Simulation.Battery, generation = Simulation.Generation, demand = Simulation.Demand,
                produced = Simulation.Produced, sold = Simulation.Sold, deliveries = Simulation.Deliveries, paused = Simulation.Paused,
                rover = CoachPosition(Simulation.RoverCell), roverMoving = Simulation.RoverMoving,
                colonyPort = CoachPosition(Simulation.Colony.Port), message = Simulation.Message,
                selectedKind = selected == null ? "" : selected.Kind.ToString(), selected = selected == null ? null : CoachPosition(selected.Origin),
                routeStarted = routeStart.HasValue, routeStart = routeStart.HasValue ? CoachPosition(routeStart.Value) : null,
                trainPhase = currentTrain?.Phase.ToString() ?? "None", trainParkRequested = currentTrain?.ParkRequested ?? false,
                capacity = currentTrain?.Capacity ?? 0, capacityLevel = currentTrain?.CapacityLevel ?? 0, cargo = currentTrain?.Cargo ?? 0,
                trainSelected = trainSelected, selectedTrainIndex = trainIndex,
                idleTrains = Simulation.Trains.Count(train => train.Phase == TrainPhase.Parked), trainCount = Simulation.Trains.Count,
                plantCost = ColonySimulation.PlantCost, plantOutput = ColonySimulation.PlantOutput, fuelEnergy = ColonySimulation.FuelEnergy,
                solarGeneration = Simulation.SolarGeneration, fuelGeneration = Simulation.FuelGeneration,
                fuelProduced = Simulation.FuelProduced, fuelDelivered = Simulation.FuelDelivered, fuelConsumed = Simulation.FuelConsumed,
                fuelDestination = currentDestination == null ? null : CoachPosition(currentDestination.Origin),
                placementReason = ""
            };
            state.raidsStarted = Simulation.RaidsStarted;
            state.waveNumber = Simulation.WaveNumber;
            state.aliensDefeated = Simulation.AliensDefeated;
            state.nextWaveIn = Simulation.NextWaveIn;
            state.visibleAliens = Simulation.Aliens.Select(alien => new Cell(Mathf.RoundToInt(alien.X), Mathf.RoundToInt(alien.Y)))
                .Where(Simulation.IsRevealed).Select(CoachPosition).ToArray();
            if (hover.HasValue && BuildingTool)
                Simulation.CanBuild(BuildKind, hover.Value, out _, out _, out _, out state.placementReason);
            if (hover.HasValue && routeStart.HasValue)
                Simulation.TryPlanNetworkRoute(routeStart.Value, NetworkEndpoint(hover.Value), tool == Tool.Rail, verticalFirst, out _, out _, out state.placementReason);
            state.buildings = Simulation.Structures.Select(CoachBuildingState).ToArray();
            state.trains = Simulation.Trains.Select((train, index) => new CoachTrain {
                index = index, phase = train.Phase.ToString(), status = TrainStatus(train), resource = train.Resource.ToString(),
                cargo = train.Cargo, capacity = train.Capacity, capacityLevel = train.CapacityLevel, parkRequested = train.ParkRequested,
                position = CoachPosition(new Cell((int)Math.Round(train.X), (int)Math.Round(train.Y))),
                owner = train.Owner == null ? null : CoachPosition(train.Owner.Origin),
                source = train.Source == null ? null : CoachPosition(train.Source.Origin),
                destination = train.Destination == null ? null : CoachPosition(train.Destination.Origin),
                waitingForFuelSpace = train.Phase == TrainPhase.Unloading && train.Resource == ResourceKind.Fluxite && train.Cargo > 0 && train.Destination != null && train.Destination.Stock >= train.Destination.Storage
            }).ToArray();
            // Do not reveal ore coordinates hidden by fog to the model.
            state.deposits = Simulation.Deposits.Where(deposit => Simulation.FullyRevealed(deposit) && deposit.Extractor == null).Select(deposit => {
                bool can = Simulation.CanBuild(StructureKind.Extractor, deposit.Origin, out _, out _, out int cost, out string reason);
                return new CoachDeposit { origin = CoachPosition(deposit.Origin), resource = deposit.Resource.ToString(), size = deposit.Size, cost = cost, buildable = can, reason = reason };
            }).ToArray();
            var clear = new List<Cell>();
            for (int x = 0; x < ColonySimulation.Width; x++)
                for (int y = 0; y < ColonySimulation.Height; y++)
                {
                    var cell = new Cell(x, y);
                    if (Simulation.IsRevealed(cell) && Simulation.Terrain.Walkable(cell) && Simulation.StructureAt(cell) == null) clear.Add(cell);
                }
            // Only suggest a reachable revealed frontier. Never route the player
            // through a hillside or an undiscovered ramp using hidden terrain data.
            var revealedGround = new HashSet<Cell>(clear);
            var reachable = new HashSet<Cell> { Simulation.RoverCell };
            var frontier = new Queue<Cell>();
            frontier.Enqueue(Simulation.RoverCell);
            while (frontier.Count > 0)
            {
                var current = frontier.Dequeue();
                foreach (var direction in ColonySimulation.Directions)
                {
                    var next = current + direction;
                    if (revealedGround.Contains(next) && Simulation.Terrain.CanTraverse(current, next) && reachable.Add(next)) frontier.Enqueue(next);
                }
            }
            var edge = clear.Where(cell => reachable.Contains(cell) && ColonySimulation.Directions.Any(d => ColonySimulation.InBounds(cell + d) && !Simulation.IsRevealed(cell + d)))
                .OrderBy(cell => Math.Abs(cell.X - Simulation.RoverX) + Math.Abs(cell.Y - Simulation.RoverY)).ThenByDescending(cell => cell.X).ToList();
            if (edge.Count > 0) state.frontier = CoachPosition(edge[0]);
            foreach (var cell in clear.OrderBy(cell => Math.Abs(cell.X - Simulation.Colony.Port.X) + Math.Abs(cell.Y - Simulation.Colony.Port.Y)))
            {
                if (state.solarSite == null && Simulation.CanBuild(StructureKind.Solar, cell, out _, out _, out _, out _)) state.solarSite = CoachPosition(cell);
                if (state.plantSite == null && Simulation.CanBuild(StructureKind.PowerPlant, cell, out _, out _, out _, out _)) state.plantSite = CoachPosition(cell);
                if (state.solarSite != null && state.plantSite != null) break;
            }
            if (state.solarSite != null) state.solarSitePowerRoute = CoachSolarRoute(new Cell(state.solarSite.x, state.solarSite.y));
            if (pickedTile.HasValue) state.pickedSitePowerRoute = CoachSolarRoute(pickedTile.Value);
            AstraCoachPublish(JsonUtility.ToJson(state));
#endif
        }
    }
}
