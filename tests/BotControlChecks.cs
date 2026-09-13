using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AstraExpress;

// The control adapter runs unchanged; only Unity's frame/coroutine shell is replaced.
namespace UnityEngine
{
    public sealed class Coroutine { public readonly Stack<IEnumerator> Stack = new Stack<IEnumerator>(); public bool Stopped; }
    public sealed class WaitForSecondsRealtime { public WaitForSecondsRealtime(float seconds) {} }
    public static class Mathf { public static int Clamp(int v, int min, int max) => Math.Max(min, Math.Min(max, v)); }
    public static class Time { public static float realtimeSinceStartup; }
    public static class JsonUtility { public static object Value; public static T FromJson<T>(string value) => (T)Value; }
}

namespace AstraExpress
{
    public sealed partial class AstraGame
    {
        public ColonySimulation Simulation = new ColonySimulation();
        private enum Tool { Explore, Extractor, Solar, PowerPlant, Conduit, Rail }
        private Tool tool;
        private bool trainSelected, verticalFirst, linkSuppressed, followRover;
        private Cell? routeStart, hover;
        private Structure selected, fuelDestination;
        private int selectedTrainIndex;
        private float coachTimer;
        private string coachSession = "control-check";
        private int commandCount;
        private UnityEngine.Coroutine lastRoutine;
        private UnityEngine.Coroutine StartCoroutine(IEnumerator routine)
        {
            lastRoutine = new UnityEngine.Coroutine(); lastRoutine.Stack.Push(routine); return lastRoutine;
        }
        private void StopCoroutine(UnityEngine.Coroutine routine) { routine.Stopped = true; }
        private void SetTool(Tool value) { tool = value; routeStart = null; trainSelected = false; }
        private void SetToolForFleet() { tool = Tool.Explore; routeStart = null; }
        private void HideLinkGuide() {}
        private void CoachFocus(string coordinates) {}
        private void CoachGuideLink(string command) { SetTool(command.StartsWith("Rail") ? Tool.Rail : Tool.Conduit); }
        private Structure CoachFuelDestination(Structure b) => Simulation.Structures.FirstOrDefault(s => s.Kind == StructureKind.PowerPlant);
        // Automatic exploration has its own tests; this suite covers solar and wiring.
        private IEnumerator BotAutoExplore() { FinishBot(true, "Auto explore dispatched"); yield break; }
        public string Status => botActionStatus;
        public string ActionMessage => botActionMessage;
        public bool HasPreview => routeStart.HasValue;
        public void Start(string type, Cell cell)
        {
            UnityEngine.JsonUtility.Value = new BotCommand { id = "test-" + (++commandCount), session = coachSession, type = type, x = cell.X, y = cell.Y };
            CoachBotCommand("{}");
        }
        public bool Tick()
        {
            if (lastRoutine == null || lastRoutine.Stopped) return false;
            while (lastRoutine.Stack.Count > 0)
            {
                var top = lastRoutine.Stack.Peek();
                if (!top.MoveNext()) { lastRoutine.Stack.Pop(); continue; }
                if (top.Current is IEnumerator nested) { lastRoutine.Stack.Push(nested); continue; }
                UnityEngine.Time.realtimeSinceStartup += .25f;
                Simulation.Step(.25f);
                return true;
            }
            return false;
        }
        public void Run()
        {
            int frames = 0;
            while (Tick()) if (++frames > 1000) throw new Exception("Coroutine did not finish");
        }
        public void Until(Func<bool> condition)
        {
            for (int i = 0; i < 1000 && !condition(); i++) if (!Tick()) break;
            if (!condition()) throw new Exception("Expected coroutine checkpoint was not reached: " + ActionMessage);
        }
    }
}

class BotControlChecks
{
    static int checks;
    static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    static AstraGame Game(int credits = 500)
    {
        var game = new AstraGame();
        game.Simulation.Reveal(14, 11, 100); // Explicitly revealed test fixture, never a bot privilege.
        SetCredits(game.Simulation, credits);
        return game;
    }
    static void SetCredits(ColonySimulation simulation, int credits) => typeof(ColonySimulation).GetProperty("Credits").SetValue(simulation, credits);
    static Structure ArrayAt(AstraGame game, Cell cell) => game.Simulation.StructureAt(cell);

    static void Main()
    {
        var site = new Cell(8, 9);
        var game = Game();
        game.Start("build_solar", site);
        game.Until(() => ArrayAt(game, site) != null);
        Check(game.Status == "running", "Solar placement must not acknowledge full completion");
        Check(!ArrayAt(game, site).Connected, "New solar starts disconnected");
        Check(game.Simulation.Credits == 400, "Placement charges the normal 100 credits");
        Check(game.Simulation.TryPlanNetworkRoute(game.Simulation.Colony.Port, ArrayAt(game, site).Port, false, false, out _, out int wiringCost, out _), "Solar wiring is valid");
        game.Until(() => game.ActionMessage.Contains("confirming generation"));
        Check(game.Status == "running" && ArrayAt(game, site).Connected, "Connection is recomputed before completion acknowledgement");
        Check(game.Simulation.SolarGeneration == 4, "Connected array contributes 2 additional power/s");
        game.Run();
        Check(game.Status == "complete", "Connected solar completes");
        Check(game.Simulation.Credits == 400 - wiringCost, "Wiring charges the shared planner cost exactly once");
        Check(game.Simulation.Conduits.Contains(ArrayAt(game, site).Port), "Actual south port receives the conduit");
        int credits = game.Simulation.Credits, tiles = game.Simulation.Conduits.Count;
        game.Start("build_solar", site); game.Run();
        Check(game.Status == "complete" && game.Simulation.Credits == credits && game.Simulation.Conduits.Count == tiles, "Existing connected solar is idempotent");

        var poor = Game(100); poor.Start("build_solar", site); poor.Run();
        Check(poor.Status == "failed" && poor.ActionMessage.Contains("not power connected"), "Insufficient wiring budget cannot report success");
        Check(ArrayAt(poor, site) != null && !ArrayAt(poor, site).Connected && poor.Simulation.Credits == 0, "Paid solar survives wiring failure without extra charges");
        Check(!poor.HasPreview, "Failed wiring leaves no active placement");
        SetCredits(poor.Simulation, 50); poor.Start("build_solar", site); poor.Run();
        Check(poor.Status == "complete" && poor.Simulation.Credits == 50 - wiringCost, "Retry wires the existing array without charging construction again");

        var detour = Game();
        Check(detour.Simulation.Build(StructureKind.Solar, new Cell(8, 6)), "Place a normal obstacle in the straight route");
        detour.Start("build_solar", site); detour.Run();
        Check(detour.Status == "complete" && ArrayAt(detour, site).Connected, "Bot uses the same valid detour as manual placement");

        var portCommand = Game();
        Check(portCommand.Simulation.Build(StructureKind.Solar, site), "Place solar for explicit port command");
        portCommand.Start("connect_conduit", ArrayAt(portCommand, site).Port); portCommand.Run();
        Check(portCommand.Status == "complete" && ArrayAt(portCommand, site).Connected, "Explicit conduit command accepts the actual building port");
        var railCommand = Game();
        var mineSite = new Cell(11, 7);
        Check(railCommand.Simulation.Build(StructureKind.Extractor, mineSite), "Place mine for shared rail adapter");
        railCommand.Start("connect_rail", mineSite); railCommand.Run();
        Check(railCommand.Status == "complete" && railCommand.Simulation.RailRoute(ArrayAt(railCommand, mineSite)) != null, "Rails use the same verified connection adapter");

        var expansion = Game(1000);
        var expansionSolar = new Cell(8, 9);
        var firstMine = new Cell(11, 7);
        var secondMine = new Cell(15, 11);
        expansion.Start("build_solar", expansionSolar); expansion.Run();
        Check(expansion.Status == "complete" && ArrayAt(expansion, expansionSolar).Connected, "Expansion solar joins the shared colony power grid");
        expansion.Start("build_extractor", firstMine); expansion.Run();
        Check(expansion.Status == "complete" && !ArrayAt(expansion, firstMine).Connected, "First extractor placement waits for a separate verified connection batch");
        expansion.Start("connect_conduit", firstMine); expansion.Run();
        Check(expansion.Status == "complete" && ArrayAt(expansion, firstMine).Connected, "First extractor connects to the shared power grid");
        expansion.Start("build_extractor", secondMine); expansion.Run();
        Check(expansion.Status == "complete" && !ArrayAt(expansion, secondMine).Connected, "Second extractor placement also waits for fresh connection state");
        expansion.Start("connect_conduit", secondMine); expansion.Run();
        var connectedMines = expansion.Simulation.Structures.Where(s => s.Kind == StructureKind.Extractor).ToList();
        Check(expansion.Status == "complete" && connectedMines.Count == 2 && connectedMines.All(s => s.Connected), "AstraBot can power two extractors through the same colony grid");
        Check(connectedMines.All(s => expansion.Simulation.PoweredCells.Contains(s.Port)), "Both extractor south ports visibly belong to the powered network");
        Check(expansion.Simulation.SolarGeneration == 4 && connectedMines.Sum(s => s.Demand) == 3, "One added array covers the two extractors' rated three-power demand");
        Check(connectedMines.All(s => expansion.Simulation.RailRoute(s) == null), "Power-only expansion does not add unrequested rail service");

        foreach (string interruption in new[] { "stop", "pause", "budget" })
        {
            var interrupted = Game(); interrupted.Start("build_solar", site);
            interrupted.Until(() => interrupted.ActionMessage.StartsWith("Previewing"));
            int before = interrupted.Simulation.Conduits.Count;
            if (interruption == "stop") interrupted.CoachBotStop("Copilot off");
            else if (interruption == "pause") interrupted.Simulation.Paused = true;
            else SetCredits(interrupted.Simulation, 0);
            interrupted.Run();
            Check(interrupted.Status == (interruption == "stop" ? "cancelled" : "failed"), "Preview interruption is reported: " + interruption);
            Check(interrupted.Simulation.Conduits.Count == before && !ArrayAt(interrupted, site).Connected, "Preview interruption spends no wiring credits: " + interruption);
            Check(!interrupted.HasPreview, "Preview is cleared after interruption: " + interruption);
        }
        Console.WriteLine(checks + " bot solar/control checks passed.");
    }
}
