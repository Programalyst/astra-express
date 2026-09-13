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
            public string kind;
            public CoachPoint origin, port;
            public bool connected, paused, railConnected, served;
            public int stock, storage, level;
            public float demand, rate;
            public CoachRoute powerRoute, railRoute;
        }
        [Serializable] private sealed class CoachDeposit
        {
            public CoachPoint origin;
            public int size, cost;
            public bool buildable;
            public string reason;
        }
        [Serializable] private sealed class CoachState
        {
            public string session, tool, message, selectedKind, trainPhase, placementReason;
            public int credits, produced, sold, deliveries, capacity, capacityLevel, cargo;
            public float battery, generation, demand, elapsed;
            public bool paused, roverMoving, routeStarted, trainParkRequested;
            public CoachPoint rover, colonyPort, selected, routeStart, frontier, solarSite;
            public CoachBuilding[] buildings;
            public CoachDeposit[] deposits;
        }
        private void ResetCoach() { coachSession = Guid.NewGuid().ToString("N"); coachTimer = 1; }

        // These methods only control coaching UI/camera; the guide never plays for the user.
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
            Vector3 point = worldCamera.WorldToViewportPoint(Position(cell, 0.4f));
            var guiPoint = new Vector2(point.x * UiWidth, (1 - point.y) * UiHeight);
            return new CoachPoint { x = cell.X, y = cell.Y, screenX = point.x, screenY = 1 - point.y,
                visible = point.z > 0 && point.x > 0.02f && point.x < 0.98f && guiPoint.y > 74 && guiPoint.y < UiHeight - 132 && !Sidebar.Contains(guiPoint) && !new Rect(16, 88, 280, 165).Contains(guiPoint) };
        }
        private CoachRoute CoachPath(Structure building, bool rail)
        {
            // Prefer a valid L, then an actual traversable path. CanLay checks cost and occupation.
            var paths = new List<List<Cell>> {
                ColonySimulation.Corridor(Simulation.Colony.Port, building.Port),
                ColonySimulation.Corridor(Simulation.Colony.Port, building.Port, true)
            };
            var walkable = Simulation.FindPath(Simulation.Colony.Port, building.Port,
                cell => Simulation.IsRevealed(cell) && Simulation.StructureAt(cell) == null);
            if (walkable != null) paths.Add(walkable);
            List<Cell> best = null;
            int bestCost = int.MaxValue;
            string reason = "Explore and clear a corridor between the ports.";
            foreach (var path in paths)
                if (Simulation.CanLay(path, rail, out int cost, out string failure))
                {
                    if (cost < bestCost) { best = path; bestCost = cost; }
                }
                else reason = failure;
            if (best == null) return new CoachRoute { possible = false, reason = reason, stops = Array.Empty<CoachPoint>() };
            var stops = new List<Cell> { best[0] };
            for (int i = 1; i + 1 < best.Count; i++)
                if (best[i].X - best[i - 1].X != best[i + 1].X - best[i].X || best[i].Y - best[i - 1].Y != best[i + 1].Y - best[i].Y) stops.Add(best[i]);
            if (!best[best.Count - 1].Equals(stops[stops.Count - 1])) stops.Add(best[best.Count - 1]);
            return new CoachRoute { possible = true, cost = bestCost, nextSegment = LinkSegmentIndex(stops, rail), reason = "", stops = stops.Select(CoachPosition).ToArray() };
        }
        private void PublishCoach()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            coachTimer += Time.unscaledDeltaTime;
            if (coachTimer < 0.75f) return;
            coachTimer = 0;
            var state = new CoachState {
                session = coachSession, elapsed = Time.realtimeSinceStartup, tool = tool.ToString(),
                credits = Simulation.Credits, battery = Simulation.Battery, generation = Simulation.Generation, demand = Simulation.Demand,
                produced = Simulation.Produced, sold = Simulation.Sold, deliveries = Simulation.Deliveries, paused = Simulation.Paused,
                rover = CoachPosition(Simulation.RoverCell), roverMoving = Simulation.RoverMoving,
                colonyPort = CoachPosition(Simulation.Colony.Port), message = Simulation.Message,
                selectedKind = selected == null ? "" : selected.Kind.ToString(), selected = selected == null ? null : CoachPosition(selected.Origin),
                routeStarted = routeStart.HasValue, routeStart = routeStart.HasValue ? CoachPosition(routeStart.Value) : null,
                trainPhase = Simulation.Train.Phase.ToString(), trainParkRequested = Simulation.Train.ParkRequested,
                capacity = Simulation.Train.Capacity, capacityLevel = Simulation.Train.CapacityLevel, cargo = Simulation.Train.Cargo,
                placementReason = ""
            };
            if (hover.HasValue && (tool == Tool.Extractor || tool == Tool.Solar))
                Simulation.CanBuild(tool == Tool.Extractor ? StructureKind.Extractor : StructureKind.Solar, hover.Value, out _, out _, out _, out state.placementReason);
            if (hover.HasValue && routeStart.HasValue)
                Simulation.CanLay(ColonySimulation.Corridor(routeStart.Value, hover.Value, verticalFirst), tool == Tool.Rail, out _, out state.placementReason);
            state.buildings = Simulation.Structures.Select(building => new CoachBuilding {
                kind = building.Kind.ToString(), origin = CoachPosition(building.Origin), port = CoachPosition(building.Port),
                connected = building.Connected, paused = building.Paused, stock = building.Stock, storage = building.Storage, level = building.Level,
                demand = building.Demand, rate = building.Rate, served = Simulation.Train.Source == building,
                railConnected = building.Kind == StructureKind.Extractor && Simulation.RailRoute(building) != null,
                powerRoute = building.Connected ? null : CoachPath(building, false),
                railRoute = building.Kind == StructureKind.Extractor ? CoachPath(building, true) : null
            }).ToArray();
            // Do not reveal ore coordinates hidden by fog to the model.
            state.deposits = Simulation.Deposits.Where(deposit => Simulation.FullyRevealed(deposit) && deposit.Extractor == null).Select(deposit => {
                bool can = Simulation.CanBuild(StructureKind.Extractor, deposit.Origin, out _, out _, out int cost, out string reason);
                return new CoachDeposit { origin = CoachPosition(deposit.Origin), size = deposit.Size, cost = cost, buildable = can, reason = reason };
            }).ToArray();
            var clear = new List<Cell>();
            for (int x = 0; x < ColonySimulation.Width; x++)
                for (int y = 0; y < ColonySimulation.Height; y++)
                {
                    var cell = new Cell(x, y);
                    if (Simulation.IsRevealed(cell) && Simulation.StructureAt(cell) == null) clear.Add(cell);
                }
            // Explore a known edge of the fog, without consulting hidden deposits.
            var edge = clear.Where(cell => ColonySimulation.Directions.Any(d => ColonySimulation.InBounds(cell + d) && !Simulation.IsRevealed(cell + d)))
                .OrderBy(cell => Math.Abs(cell.X - Simulation.RoverX) + Math.Abs(cell.Y - Simulation.RoverY)).ThenByDescending(cell => cell.X).ToList();
            if (edge.Count > 0) state.frontier = CoachPosition(edge[0]);
            foreach (var cell in clear.OrderBy(cell => Math.Abs(cell.X - Simulation.Colony.Port.X) + Math.Abs(cell.Y - Simulation.Colony.Port.Y)))
                if (Simulation.CanBuild(StructureKind.Solar, cell, out _, out _, out _, out _)) { state.solarSite = CoachPosition(cell); break; }
            AstraCoachPublish(JsonUtility.ToJson(state));
#endif
        }
    }
}
