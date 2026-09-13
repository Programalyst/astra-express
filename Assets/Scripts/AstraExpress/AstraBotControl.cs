using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AstraExpress
{
    public sealed partial class AstraGame
    {
        [Serializable] private sealed class BotCommand
        {
            public string id, session, type;
            public int x = -1, y = -1, targetX = -1, targetY = -1, seconds = 5;
        }
        private Coroutine botRoutine;
        private bool botBusy, pickingTile, botRoverOrder;
        private Cell? pickedTile, botTarget;
        private string botActionId = "", botActionStatus = "idle", botActionMessage = "";
        private readonly HashSet<string> botCompletedIds = new HashSet<string>();

        private void ResetBotControl()
        {
            if (botRoutine != null) StopCoroutine(botRoutine);
            botRoutine = null; botBusy = pickingTile = botRoverOrder = false;
            pickedTile = botTarget = null;
            botActionId = ""; botActionStatus = "idle"; botActionMessage = "";
            botCompletedIds.Clear();
        }

        public void CoachPickTile(string value)
        {
            if (botBusy) return;
            pickingTile = value == "1";
            if (value == "clear") pickedTile = null;
            if (pickingTile) { SetTool(Tool.Explore); HideLinkGuide(); }
            coachTimer = 1;
        }

        public void CoachBotStop(string reason)
        {
            if (botRoutine != null) StopCoroutine(botRoutine);
            botRoutine = null;
            if (botBusy) FinishBot(false, "Stopped. Completed construction is kept; existing services continue.", "cancelled");
            pickingTile = false; routeStart = null; HideLinkGuide(); tool = Tool.Explore;
            coachTimer = 1;
        }

        // A deliberately narrow game control adapter. It accepts no scripts, hidden-state
        // queries or resource overrides. Actions use the same tools and rules as the player.
        public void CoachBotCommand(string json)
        {
            if (Simulation == null || botBusy || json == null || json.Length > 3000) return;
            BotCommand command;
            try { command = JsonUtility.FromJson<BotCommand>(json); } catch { return; }
            if (command == null || command.session != coachSession || string.IsNullOrEmpty(command.id) || command.id.Length > 100) return;
            if (botCompletedIds.Contains(command.id)) return;
            botActionId = command.id; botActionStatus = "running"; botActionMessage = "Preparing action";
            botBusy = true; pickingTile = false; coachTimer = 1;
            botRoutine = StartCoroutine(ExecuteBotCommand(command));
        }

        private void FinishBot(bool success, string message, string status = null)
        {
            if (botRoverOrder) { Simulation.StopRover(); botRoverOrder = false; }
            botBusy = false; botActionStatus = status ?? (success ? "complete" : "failed");
            botActionMessage = message; botCompletedIds.Add(botActionId);
            // A game session has a bounded command ledger; IDs are unique across plans.
            if (botCompletedIds.Count > 512) botCompletedIds.Clear();
            botTarget = null; coachTimer = 1;
        }

        private Structure BotBuilding(Cell cell) => Simulation.Structures.FirstOrDefault(b => b.Origin.Equals(cell) || b.Port.Equals(cell));

        private IEnumerator ExecuteBotCommand(BotCommand command)
        {
            if (command.type == "stop") { FinishBot(true, "Stopped."); yield break; }
            if (command.type == "wait")
            {
                botActionMessage = "Observing the colony";
                yield return new WaitForSecondsRealtime(Mathf.Clamp(command.seconds, 1, 20));
                FinishBot(true, "Observation complete; inspect the latest colony state."); yield break;
            }
            if (command.type == "resume") { Simulation.Paused = false; FinishBot(true, "Colony resumed."); yield break; }
            if (Simulation.Paused) { FinishBot(false, "Colony is paused. Resume before taking an action."); yield break; }
            if (command.type == "buy_train")
            {
                selected = null; trainSelected = true; SetToolForFleet();
                botActionMessage = "Opening Fleet to buy a locomotive";
                yield return new WaitForSecondsRealtime(0.65f);
                bool bought = Simulation.BuyTrain();
                if (bought) selectedTrainIndex = Simulation.Trains.Count - 1;
                FinishBot(bought, Simulation.Message); yield break;
            }
            var cell = new Cell(command.x, command.y);
            if (!ColonySimulation.InBounds(cell)) { FinishBot(false, "Choose a tile inside the map."); yield break; }
            if (command.type != "explore" && !Simulation.IsRevealed(cell)) { FinishBot(false, "Explore this tile before building or selecting it."); yield break; }
            botTarget = cell; hover = cell;
            CoachFocus($"{cell.X},{cell.Y}");
            botActionMessage = $"Targeting tile ({cell.X}, {cell.Y})";
            yield return new WaitForSecondsRealtime(0.55f);
            if (command.type == "explore")
            {
                SetTool(Tool.Explore); selected = null;
                if (!Simulation.OrderRover(cell)) { FinishBot(false, Simulation.Message); yield break; }
                botRoverOrder = true;
                float deadline = Time.realtimeSinceStartup + 60;
                botActionMessage = "Rover exploring; waiting for arrival";
                while (Simulation.RoverMoving && Time.realtimeSinceStartup < deadline) yield return null;
                bool arrived = Simulation.RoverCell.Equals(cell) && !Simulation.RoverMoving;
                FinishBot(arrived, arrived ? "Rover arrived. Newly discovered deposits are now available to the plan." : "Rover did not reach the target. Inspect battery and route before retrying.");
                yield break;
            }
            if (command.type == "build_extractor" || command.type == "build_solar" || command.type == "build_plant")
            {
                var kind = command.type == "build_extractor" ? StructureKind.Extractor : command.type == "build_solar" ? StructureKind.Solar : StructureKind.PowerPlant;
                var existing = Simulation.StructureAt(cell);
                if (existing != null && existing.Kind == kind) { FinishBot(true, "This building already exists."); yield break; }
                SetTool(kind == StructureKind.Extractor ? Tool.Extractor : kind == StructureKind.Solar ? Tool.Solar : Tool.PowerPlant);
                yield return new WaitForSecondsRealtime(0.55f);
                bool built = Simulation.Build(kind, cell);
                if (built) { selected = Simulation.StructureAt(cell); tool = Tool.Explore; }
                FinishBot(built, Simulation.Message); yield break;
            }
            var building = BotBuilding(cell);
            if (building == null) { FinishBot(false, "There is no building or port on this tile."); yield break; }
            selected = building; trainSelected = false; SetTool(Tool.Explore);
            if (command.type == "select") { FinishBot(true, "Building selected."); yield break; }
            if (command.type == "pause_mine" || command.type == "resume_mine")
            {
                if (building.Kind != StructureKind.Extractor && building.Kind != StructureKind.PowerPlant) { FinishBot(false, "Only a mine or plant can be paused."); yield break; }
                building.Paused = command.type == "pause_mine";
                FinishBot(true, building.Paused ? "Production paused." : "Production resumed."); yield break;
            }
            if (command.type == "connect_conduit" || command.type == "connect_rail")
            {
                bool rail = command.type == "connect_rail";
                if (rail ? Simulation.RailRoute(building) != null : building.Connected) { FinishBot(true, "This connection already exists."); yield break; }
                var route = CoachPath(building, rail);
                if (!route.possible || route.stops.Length < 2) { FinishBot(false, route.reason); yield break; }
                CoachGuideLink($"{(rail ? "Rail" : "Conduit")},{building.Origin.X},{building.Origin.Y}");
                for (int i = 0; i + 1 < route.stops.Length; i++)
                {
                    var start = new Cell(route.stops[i].x, route.stops[i].y);
                    var end = new Cell(route.stops[i + 1].x, route.stops[i + 1].y);
                    if (!Simulation.CanLay(ColonySimulation.Corridor(start, end), rail, out _, out string reason)) { FinishBot(false, reason); yield break; }
                    routeStart = null; tool = rail ? Tool.Rail : Tool.Conduit;
                    botTarget = hover = start; PlaceNetworkAt(start);
                    botActionMessage = $"Linking segment {i + 1} / {route.stops.Length - 1}";
                    yield return new WaitForSecondsRealtime(0.5f);
                    botTarget = hover = end; PlaceNetworkAt(end);
                    if (routeStart.HasValue) { FinishBot(false, Simulation.Message); yield break; }
                    yield return new WaitForSecondsRealtime(0.5f);
                }
                HideLinkGuide(); SetTool(Tool.Explore);
                bool connected = rail ? Simulation.RailRoute(building) != null : building.Connected;
                FinishBot(connected, connected ? (rail ? "Rails connected to the depot." : "Power connected. Flowing cyan marks the active conduit.") : Simulation.Message);
                yield break;
            }
            if (command.type == "dispatch_train")
            {
                if (building.Kind != StructureKind.Extractor) { FinishBot(false, "Select an extractor to dispatch a train."); yield break; }
                if (Simulation.Trains.Any(t => t.Source == building && t.Phase != TrainPhase.Parked)) { FinishBot(true, "This mine already has a train service."); yield break; }
                Structure destination = building.Deposit.Resource == ResourceKind.Ore ? Simulation.Colony : BotBuilding(new Cell(command.targetX, command.targetY));
                if (destination == null && building.Deposit.Resource == ResourceKind.Fluxite) destination = CoachFuelDestination(building);
                if (destination != null && destination.Kind == StructureKind.PowerPlant) fuelDestination = destination;
                yield return new WaitForSecondsRealtime(0.65f);
                bool dispatched = Simulation.Dispatch(building, destination);
                FinishBot(dispatched, Simulation.Message); yield break;
            }
            FinishBot(false, "Unsupported game action.");
        }

        private void DrawBotTarget()
        {
            Cell? target = botTarget ?? pickedTile;
            if (target.HasValue)
            {
                Vector3 point = worldCamera.WorldToScreenPoint(Position(target.Value, 0.4f));
                if (point.z > 0)
                {
                    float x = point.x / UiScale, y = (Screen.height - point.y) / UiScale;
                    Color color = botBusy ? gold : cyan;
                    Fill(new Rect(x - 20, y - 20, 13, 3), color); Fill(new Rect(x - 20, y - 20, 3, 13), color);
                    Fill(new Rect(x + 7, y - 20, 13, 3), color); Fill(new Rect(x + 17, y - 20, 3, 13), color);
                    Fill(new Rect(x - 20, y + 17, 13, 3), color); Fill(new Rect(x - 20, y + 7, 3, 13), color);
                    Fill(new Rect(x + 7, y + 17, 13, 3), color); Fill(new Rect(x + 17, y + 7, 3, 13), color);
                    WorldLabel(Position(target.Value, 0.4f), botBusy ? "ASTRABOT TARGET" : $"TASK TILE ({target.Value.X}, {target.Value.Y})", color);
                }
            }
            if (pickingTile) { Panel(new Rect(UiWidth / 2 - 220, 82, 440, 40)); GUI.Label(new Rect(UiWidth / 2 - 210, 89, 420, 25), "Click a task tile · Escape cancels · Rover stays put", bodyStyle); }
        }
    }
}
