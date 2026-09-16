using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AstraExpress
{
    public sealed partial class AstraGame
    {
        private readonly Dictionary<Alien, Transform> alienVisuals = new Dictionary<Alien, Transform>();
        private readonly Dictionary<LaserShot, LineRenderer> laserVisuals = new Dictionary<LaserShot, LineRenderer>();
        private LineRenderer turretRangeVisual;
        private Material laserMaterial;
        private Rect DefensePanel => !Simulation.Paused && !pickingTile && !NetworkTool && !ConnectionPanelVisible ? new Rect(16, ObjectiveVisible ? 212 : 88, 270, 62) : Rect.zero;
        private Rect RepairPanel => SidebarVisible && selected != null && selected.Health < Structure.MaxHealth && !NetworkTool
            ? new Rect(Sidebar.x, Sidebar.yMax + 6, Sidebar.width, 62) : Rect.zero;

        private void ResetDefenseVisuals()
        {
            alienVisuals.Clear();
            laserVisuals.Clear();
            turretRangeVisual = null;
        }

        private LineRenderer DefenseLine(string name, float width)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>();
            line.transform.SetParent(worldRoot, false);
            if (name == "Turret laser")
            {
                if (LaserMaterial == null && laserMaterial == null) laserMaterial = MakeMaterial(cyan, 3);
                line.sharedMaterial = LaserMaterial != null ? LaserMaterial : laserMaterial;
            }
            else line.sharedMaterial = powerMaterial;
            line.widthMultiplier = width;
            line.useWorldSpace = true;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return line;
        }

        private void UpdateDefenseVisuals()
        {
            foreach (var alien in alienVisuals.Keys.Where(alien => !Simulation.Aliens.Contains(alien)).ToList())
            {
                Destroy(alienVisuals[alien].gameObject);
                alienVisuals.Remove(alien);
            }
            foreach (var alien in Simulation.Aliens)
            {
                if (!alienVisuals.TryGetValue(alien, out var visual))
                {
                    visual = Model(AlienModel, "Alien " + alien.Id, worldRoot, Position(alien.X, alien.Y), 1.2f, 1.4f).transform;
                    alienVisuals.Add(alien, visual);
                }
                bool visible = Simulation.IsRevealed(new Cell(Mathf.RoundToInt(alien.X), Mathf.RoundToInt(alien.Y)));
                visual.gameObject.SetActive(visible);
                MoveVisual(visual, Position(alien.X, alien.Y));
            }
            foreach (var shot in laserVisuals.Keys.Where(shot => !Simulation.LaserShots.Contains(shot)).ToList())
            {
                Destroy(laserVisuals[shot].gameObject);
                laserVisuals.Remove(shot);
            }
            foreach (var shot in Simulation.LaserShots)
            {
                if (!laserVisuals.TryGetValue(shot, out var laser))
                {
                    laser = DefenseLine("Turret laser", 0.075f);
                    laser.positionCount = 2;
                    laserVisuals.Add(shot, laser);
                }
                var start = Position(shot.Turret.Origin, 1.2f);
                var end = Position(shot.TargetX, shot.TargetY, 0.6f);
                laser.SetPosition(0, start);
                laser.SetPosition(1, end);
                if (buildings.TryGetValue(shot.Turret, out var turret))
                {
                    var model = turret.Find("Turret");
                    var direction = end - start;
                    direction.y = 0;
                    if (model != null && direction.sqrMagnitude > 0.001f) model.rotation = Quaternion.LookRotation(direction);
                }
            }
            Cell? rangeCenter = tool == Tool.Turret ? hover : selected?.Kind == StructureKind.Turret ? selected.Origin : (Cell?)null;
            if (turretRangeVisual == null) turretRangeVisual = DefenseLine("Laser turret range", 0.045f);
            turretRangeVisual.gameObject.SetActive(rangeCenter.HasValue && !NetworkTool);
            if (!rangeCenter.HasValue) return;
            turretRangeVisual.positionCount = 65;
            for (int index = 0; index <= 64; index++)
            {
                float angle = index * Mathf.PI * 2 / 64;
                turretRangeVisual.SetPosition(index, Position(rangeCenter.Value.X + Mathf.Cos(angle) * ColonySimulation.TurretRange,
                    rangeCenter.Value.Y + Mathf.Sin(angle) * ColonySimulation.TurretRange, 0.12f));
            }
        }

        private void DrawDefense()
        {
            if (DefensePanel != Rect.zero)
            {
                var panel = DefensePanel;
                Panel(panel);
                GUI.Label(new Rect(panel.x + 12, panel.y + 7, panel.width - 24, 22), Simulation.RaidsStarted
                    ? $"ALIEN WAVES / next in {Mathf.CeilToInt(Simulation.NextWaveIn)}s" : "FRONTIER DEFENSE", smallStyle);
                GUI.Label(new Rect(panel.x + 12, panel.y + 29, panel.width - 24, 22), Simulation.RaidsStarted
                    ? $"Wave {Simulation.WaveNumber} / {Simulation.AliensDefeated} defeated / from NW" : "Mining 2x2 deposits attracts aliens.", smallStyle);
            }
            if (RepairPanel != Rect.zero)
            {
                var panel = RepairPanel;
                Panel(panel);
                GUI.Label(new Rect(panel.x + 12, panel.y + 5, panel.width - 24, 18), $"{(selected.Disabled ? "DISABLED" : "DAMAGED")} / {selected.Health:0} / 100 HP", smallStyle);
                if (Button(new Rect(panel.x + 12, panel.y + 26, panel.width - 24, 29), selected.RepairRemaining > 0
                    ? $"Repairing / {Mathf.CeilToInt(selected.RepairRemaining)}s" : "Repair / 10s / free", enabled: selected.RepairRemaining <= 0)) Simulation.Repair(selected);
            }
            foreach (var building in Simulation.Structures)
            {
                if (building.Health >= Structure.MaxHealth) continue;
                WorldLabel(Position(building.Origin, building.Size + 1.5f), building.Disabled ? "DISABLED - SELECT TO REPAIR" : $"{building.Health:0} / 100 HP", new Color(1, 0.4f, 0.3f), -18);
            }
            foreach (var alien in Simulation.Aliens)
                if (Simulation.IsRevealed(new Cell(Mathf.RoundToInt(alien.X), Mathf.RoundToInt(alien.Y))))
                    WorldLabel(Position(alien.X, alien.Y, 1.7f), $"ALIEN {alien.Health:0}", new Color(1, 0.4f, 0.3f), -12);
        }

        private void DrawTurretSelection(float left, float width, float row)
        {
            Stat(left, ref row, "STATUS", selected.Disabled ? "Disabled - repair" : !selected.Connected ? "Needs power link" : Simulation.Battery < ColonySimulation.TurretShotPower ? "Low battery" : "Defending");
            Stat(left, ref row, "HEALTH", $"{selected.Health:0} / 100");
            Stat(left, ref row, "RANGE", $"{ColonySimulation.TurretRange:0} tiles / terrain LOS");
            Stat(left, ref row, "LASER", "15 damage / 0.75s");
            Stat(left, ref row, "BATTERY", "2 power per shot");
            if (!selected.Connected && Button(new Rect(left, row + 4, width, 34), "Show power connection", active: true))
                CoachGuideLink($"Conduit,{selected.Origin.X},{selected.Origin.Y}");
            GUI.Label(new Rect(left, row + 48, width, 45), "Automatically targets visible aliens. Cliffs can block shots. No rail connection needed.", smallStyle);
        }
    }
}
