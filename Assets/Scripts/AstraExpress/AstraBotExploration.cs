using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AstraExpress
{
    public sealed partial class AstraGame
    {
        private const float BotSurveySeconds = 55;
        private const float BotSurveyReserve = 10;

        // Survey targets depend only on revealed ground and the fog boundary.
        // In particular, neither hidden deposits nor hidden ramps guide this search.
        private List<Cell> BotFrontierRoute()
        {
            var start = Simulation.RoverCell;
            var pending = new Queue<Cell>();
            var previous = new Dictionary<Cell, Cell> { [start] = start };
            var distance = new Dictionary<Cell, float> { [start] = 0 };
            pending.Enqueue(start);
            Cell? best = null;
            double bestScore = -1;
            while (pending.Count > 0)
            {
                var current = pending.Dequeue();
                int unseen = 0;
                for (int dx = -3; dx <= 3; dx++)
                    for (int dy = -3; dy <= 3; dy++)
                    {
                        var sample = new Cell(current.X + dx, current.Y + dy);
                        if (dx * dx + dy * dy <= 9 && ColonySimulation.InBounds(sample) && !Simulation.IsRevealed(sample)) unseen++;
                    }
                double score = unseen / Math.Max(1, distance[current]);
                if (!current.Equals(start) && unseen > 0 && (score > bestScore ||
                    (Math.Abs(score - bestScore) < .00001 && (!best.HasValue || current.X > best.Value.X))))
                {
                    best = current; bestScore = score;
                }
                foreach (var direction in ColonySimulation.Directions)
                {
                    var next = current + direction;
                    // Test visibility before querying terrain or occupancy at next.
                    if (!Simulation.IsRevealed(next) || previous.ContainsKey(next) ||
                        !Simulation.Terrain.CanTraverse(current, next) || Simulation.StructureAt(next) != null) continue;
                    previous[next] = current;
                    distance[next] = distance[current] + Simulation.Terrain.EdgeCost(current, next);
                    pending.Enqueue(next);
                }
            }
            if (!best.HasValue) return null;
            var route = new List<Cell> { best.Value };
            while (!route[route.Count - 1].Equals(start)) route.Add(previous[route[route.Count - 1]]);
            route.Reverse();
            return route;
        }

        private HashSet<Cell> BotVisibleDeposits(ResourceKind resource) => new HashSet<Cell>(
            Simulation.Deposits.Where(d => Simulation.FullyRevealed(d) && d.Resource == resource).Select(d => d.Origin));

        private IEnumerator BotAutoExplore()
        {
            var knownOre = BotVisibleDeposits(ResourceKind.Ore);
            var knownFuel = BotVisibleDeposits(ResourceKind.Fluxite);
            int initialRevision = Simulation.RevealRevision;
            float deadline = Time.realtimeSinceStartup + BotSurveySeconds;
            int steps = 0;
            // Take ownership before any order, so Stop/global-off also cancels a
            // rover that was already moving when this explicitly started survey begins.
            botRoverOrder = true;
            Simulation.StopRover();
            while (botBusy && Time.realtimeSinceStartup < deadline && steps < 100)
            {
                if (Simulation.Paused) { FinishBot(false, "Survey stopped because the colony is paused. Resume before exploring again."); yield break; }
                var discovered = BotVisibleDeposits(ResourceKind.Ore).Where(c => !knownOre.Contains(c)).ToList();
                if (discovered.Count > 0)
                {
                    var ore = discovered[0];
                    int fuel = BotVisibleDeposits(ResourceKind.Fluxite).Count(c => !knownFuel.Contains(c));
                    FinishBot(true, $"New Ore discovered at ({ore.X}, {ore.Y})." + (fuel > 0 ? $" Also discovered {fuel} Fluxite deposit(s), which are fuel, not saleable ore." : "") + " Inspect the fresh map before planning extraction.");
                    yield break;
                }
                if (Simulation.Battery <= BotSurveyReserve + 2.5f)
                {
                    FinishBot(false, "Survey stopped to keep 10 battery in reserve. Recharge with connected power before exploring farther."); yield break;
                }
                var route = BotFrontierRoute();
                if (route == null)
                {
                    FinishBot(true, "All currently reachable ground has been surveyed; no new Ore found. Existing deposits are not new discoveries."); yield break;
                }
                var destination = route[route.Count - 1];
                botTarget = destination;
                botActionMessage = $"Auto-exploring toward ({destination.X}, {destination.Y}) · searching for new Ore";
                // Issue only the next adjacent, already-revealed step. The ordinary
                // rover pathfinder therefore cannot choose a hidden-terrain shortcut.
                var next = route[1];
                if (!Simulation.IsRevealed(next) || !Simulation.OrderRover(next))
                {
                    FinishBot(false, "Survey route changed or is blocked. Inspect the latest map before retrying."); yield break;
                }
                FollowBotRoverMove(destination);
                steps++;
                while (Simulation.RoverMoving && botBusy && Time.realtimeSinceStartup < deadline)
                {
                    if (Simulation.Paused) { FinishBot(false, "Survey stopped because the colony is paused. Resume before exploring again."); yield break; }
                    if (Simulation.Battery <= BotSurveyReserve) { FinishBot(false, "Survey stopped at the battery reserve. Recharge with connected power before exploring farther."); yield break; }
                    yield return null;
                }
                if (!botBusy) yield break;
                if (!Simulation.RoverMoving && !Simulation.RoverCell.Equals(next))
                {
                    FinishBot(false, "The rover could not complete its survey step. Inspect the route before retrying."); yield break;
                }
            }
            if (!botBusy) yield break;
            var finalOre = BotVisibleDeposits(ResourceKind.Ore).Where(c => !knownOre.Contains(c)).ToList();
            if (finalOre.Count > 0)
            {
                var ore = finalOre[0];
                FinishBot(true, $"New Ore discovered at ({ore.X}, {ore.Y}). Inspect the fresh map before planning extraction.");
                yield break;
            }
            int newFuel = BotVisibleDeposits(ResourceKind.Fluxite).Count(c => !knownFuel.Contains(c));
            FinishBot(true, (Simulation.RevealRevision > initialRevision ? "Survey batch revealed more terrain" : "Survey batch ended") +
                (newFuel > 0 ? $" and {newFuel} Fluxite deposit(s)" : "") + "; no new Ore confirmed. Recheck the map before another bounded survey.");
        }
    }
}
