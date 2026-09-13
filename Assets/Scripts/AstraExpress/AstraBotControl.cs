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
            if (command.type == "auto_explore") { yield return BotAutoExplore(); yield break; }
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
                followRover = true;
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
                if (existing != null && existing.Kind == kind)
                {
                    if (kind == StructureKind.Solar) yield return BotConnectBuilding(existing, false, true);
                    else FinishBot(true, "This building already exists.");
                    yield break;
                }
                SetTool(kind == StructureKind.Extractor ? Tool.Extractor : kind == StructureKind.Solar ? Tool.Solar : Tool.PowerPlant);
                yield return new WaitForSecondsRealtime(0.55f);
                if (Simulation.Paused) { FinishBot(false, "Colony paused before placement. No building was placed."); yield break; }
                bool built = Simulation.Build(kind, cell);
                if (built)
                {
                    var placed = Simulation.StructureAt(cell);
                    selected = placed; tool = Tool.Explore;
                    if (kind == StructureKind.Solar)
                    {
                        botActionMessage = "Solar placed. Connecting its south port to colony power";
                        // Let the placed array appear before presenting its conduit preview.
                        yield return new WaitForSecondsRealtime(0.55f);
                        yield return BotConnectBuilding(placed, false, true);
                        yield break;
                    }
                }
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
                yield return BotConnectBuilding(building, command.type == "connect_rail");
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

        private IEnumerator BotConnectBuilding(Structure building, bool rail, bool finishSolar = false)
        {
            Simulation.Reconnect();
            if (rail ? Simulation.RailRoute(building) != null : building.Connected)
            {
                FinishBot(true, finishSolar ? "Solar is power connected and supplying the colony." : "This connection already exists.");
                yield break;
            }
            Cell start = Simulation.Colony.Port;
            Cell end = building.Port;
            string kept = finishSolar ? "Solar is placed but not power connected. " : "Connection incomplete. ";
            if (!Simulation.TryPlanNetworkRoute(start, end, rail, false, out _, out int cost, out string reason))
            {
                FinishBotConnectionFailure(kept + reason + " Existing construction is kept.");
                yield break;
            }
            selected = building; trainSelected = false;
            CoachGuideLink($"{(rail ? "Rail" : "Conduit")},{building.Origin.X},{building.Origin.Y}");
            SetTool(rail ? Tool.Rail : Tool.Conduit); verticalFirst = false;
            botTarget = hover = start; PlaceNetworkAt(start);
            if (!routeStart.HasValue || !routeStart.Value.Equals(start))
            {
                FinishBotConnectionFailure(kept + (Simulation.Paused ? "Colony is paused." : Simulation.Message));
                yield break;
            }
            botTarget = hover = end;
            botActionMessage = $"Previewing {(rail ? "rails" : "conduits")} to the south port: {cost} credits";
            coachTimer = 1;
            yield return new WaitForSecondsRealtime(0.9f);
            // Recheck the same planner that the manual placement uses before it spends credits.
            if (Simulation.Paused || !Simulation.TryPlanNetworkRoute(start, end, rail, verticalFirst, out _, out _, out reason))
            {
                FinishBotConnectionFailure(kept + (Simulation.Paused ? "Colony paused before connecting." : reason));
                yield break;
            }
            PlaceNetworkAt(end);
            if (routeStart.HasValue)
            {
                FinishBotConnectionFailure(kept + Simulation.Message);
                yield break;
            }
            Simulation.Reconnect();
            bool connected = rail ? Simulation.RailRoute(building) != null : building.Connected && Simulation.PoweredCells.Contains(building.Port);
            HideLinkGuide(); SetTool(Tool.Explore);
            if (!connected)
            {
                FinishBot(false, kept + "The colony cannot reach this port through the network. Existing construction is kept.");
                yield break;
            }
            // Allow the connected network and updated solar generation to be published first.
            botActionMessage = finishSolar ? "Solar power connected; confirming generation" : rail ? "Rails connected to the depot" : "Power connected";
            coachTimer = 1;
            yield return null;
            FinishBot(true, finishSolar ? "Solar power connected. The array now adds 2 power/s to the colony." : rail ? "Rails connected to the depot." : "Power connected. Flowing cyan marks the active conduit.");
        }

        private void FinishBotConnectionFailure(string reason)
        {
            routeStart = null; HideLinkGuide(); SetTool(Tool.Explore);
            FinishBot(false, reason);
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
