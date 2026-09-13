using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AstraExpress;

// Only the Unity clock and UI/cancellation adapter are replaced. The survey
// coroutine, resource visibility, terrain, rover motion and battery are real.
namespace UnityEngine { public static class Time { public static float realtimeSinceStartup; } }
namespace AstraExpress
{
    public sealed partial class AstraGame
    {
        public ColonySimulation Simulation = new ColonySimulation();
        private enum Tool { Explore }
        private bool botBusy = true, botRoverOrder, trainSelected;
        private Structure selected;
        private Cell? botTarget, hover;
        private string botActionMessage;
        public string Result;
        public bool Success;
        public int Finishes;
        private void SetTool(Tool tool) { }
        private void CoachFocus(string position) { }
        private void FinishBot(bool success, string message)
        {
            if (botRoverOrder) { Simulation.StopRover(); botRoverOrder = false; }
            botBusy = false; Success = success; Result = message; Finishes++;
        }
        public IEnumerator Survey() => BotAutoExplore();
        public List<Cell> SurveyRoute() => BotFrontierRoute();
        public void Cancel() => FinishBot(false, "cancelled");
    }
}

class ExplorationChecks
{
    static int checks;
    static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    static void Step(AstraGame game, IEnumerator routine, float dt = .05f)
    {
        if (routine.MoveNext()) { game.Simulation.Step(dt); UnityEngine.Time.realtimeSinceStartup += dt; }
    }
    static void Run(AstraGame game, Action<int> during = null)
    {
        var routine = game.Survey();
        for (int i = 0; i < 1400 && game.Finishes == 0; i++) { during?.Invoke(i); Step(game, routine); }
        Check(game.Finishes == 1, "Exactly one bounded action acknowledgment");
        Check(!game.Simulation.RoverMoving, "Rover stopped when survey ends");
    }
    static void Main()
    {
        var initial = new AstraGame(); var route = initial.SurveyRoute();
        Check(route != null && route.Count > 1, "Initial fog has a reachable survey route");
        Check(route.All(initial.Simulation.IsRevealed), "Every planned step is revealed before movement");
        Check(route.Zip(route.Skip(1), (a, b) => initial.Simulation.Terrain.CanTraverse(a, b)).All(v => v), "Survey route follows traversable adjacent terrain");
        var altered = new AstraGame();
        foreach (var d in altered.Simulation.Deposits.Where(d => !altered.Simulation.FullyRevealed(d))) { d.Origin = new Cell(26, 19); d.Resource = ResourceKind.Fluxite; }
        Check(altered.SurveyRoute().SequenceEqual(route), "Hidden resource coordinates and kind do not affect survey targets");
        var before = new HashSet<Cell>(initial.Simulation.Deposits.Where(initial.Simulation.FullyRevealed).Select(d => d.Origin));
        UnityEngine.Time.realtimeSinceStartup = 0;
        Run(initial);
        Check(initial.Success && initial.Result.StartsWith("New Ore discovered"), "Automatic survey discovers actual new Ore");
        Check(initial.Simulation.Deposits.Any(d => d.Resource == ResourceKind.Ore && initial.Simulation.FullyRevealed(d) && !before.Contains(d.Origin)), "Discovery result agrees with newly visible simulation Ore");
        Check(UnityEngine.Time.realtimeSinceStartup < 55, "Discovery completes before action deadline");
        Console.WriteLine("Initial survey: " + initial.Result);

        var allKnown = new AstraGame(); allKnown.Simulation.Reveal(14, 11, 100);
        Run(allKnown);
        Check(allKnown.Result.Contains("no new Ore") && !allKnown.Result.StartsWith("New Ore"), "Already known resources never count as new Ore");

        var fuelOnly = new AstraGame(); foreach (var d in fuelOnly.Simulation.Deposits) d.Resource = ResourceKind.Fluxite;
        UnityEngine.Time.realtimeSinceStartup = 0; Run(fuelOnly);
        Check(!fuelOnly.Result.StartsWith("New Ore"), "New Fluxite does not satisfy Ore discovery");
        Check(UnityEngine.Time.realtimeSinceStartup <= 55.1f, "No-Ore exploration respects the real-time deadline");

        var stalled = new AstraGame(); UnityEngine.Time.realtimeSinceStartup = 0;
        var stalledRoutine = stalled.Survey();
        while (UnityEngine.Time.realtimeSinceStartup < 60 && stalledRoutine.MoveNext()) UnityEngine.Time.realtimeSinceStartup += .25f;
        Check(stalled.Finishes == 1 && UnityEngine.Time.realtimeSinceStartup <= 55.25f && !stalled.Simulation.RoverMoving,
              "Absolute 55-second deadline stops stalled motion independently of simulation progress");

        var paused = new AstraGame(); Run(paused, i => { if (i == 3) paused.Simulation.Paused = true; });
        Check(!paused.Success && paused.Result.Contains("paused"), "Pause terminates an in-flight survey");

        var low = new AstraGame(); typeof(ColonySimulation).GetProperty("Battery").SetValue(low.Simulation, 11f);
        Run(low); Check(!low.Success && low.Result.Contains("reserve") && low.Simulation.Battery >= 10, "Low battery preserves reserve");

        var stopped = new AstraGame(); var live = stopped.Survey(); Step(stopped, live); Step(stopped, live); stopped.Cancel();
        float x = stopped.Simulation.RoverX, y = stopped.Simulation.RoverY;
        Step(stopped, live); stopped.Simulation.Step(.25f);
        Check(stopped.Finishes == 1 && stopped.Simulation.RoverX == x && stopped.Simulation.RoverY == y, "Stop cancels movement without later action completion");
        Console.WriteLine("PASS automatic-exploration assertions=" + checks);
    }
}
