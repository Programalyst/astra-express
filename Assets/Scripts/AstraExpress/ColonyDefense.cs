using System;
using System.Collections.Generic;
using System.Linq;

namespace AstraExpress
{
    public sealed class Alien
    {
        public int Id;
        public float X, Y;
        public float Health = ColonySimulation.AlienHealth;
        public Structure Target;
        public float AttackCooldown;
        internal List<Cell> Route = new List<Cell>();
        internal int Waypoint;
        internal int RouteRevision = -1;
        internal float RepathRemaining;
    }

    public sealed class LaserShot
    {
        public Structure Turret;
        public float TargetX, TargetY;
        public float Remaining = 0.16f;
    }

    public sealed partial class ColonySimulation
    {
        public const int TurretCost = 100;
        public const float TurretRange = 7;
        public const float TurretDamage = 15;
        public const float TurretShotPower = 5;
        public const float TurretInterval = 0.75f;
        public const float AlienHealth = 40;
        public const float AlienSpeed = 1;
        public const float AlienDamage = 6;
        public const float FirstWaveDelay = 20;
        public const float WaveInterval = 45;
        public const int MaxAliens = 12;
        public const float RepairDuration = 10;
        public static readonly Cell AlienSpawn = new Cell(0, Height - 1);
        public readonly List<Alien> Aliens = new List<Alien>();
        public readonly List<LaserShot> LaserShots = new List<LaserShot>();
        public bool RaidsStarted { get; private set; }
        public int WaveNumber { get; private set; }
        public int AliensDefeated { get; private set; }
        public float NextWaveIn { get; private set; }
        private readonly bool raidsEnabled;
        private int nextAlienId;
        private int pendingAliens;
        private float spawnRemaining;

        private void AwakenAliens()
        {
            if (!raidsEnabled || RaidsStarted) return;
            RaidsStarted = true;
            NextWaveIn = FirstWaveDelay;
            Message = "Large-scale mining has attracted aliens! First wave from the northwest in 20 seconds. Build and power laser turrets.";
        }

        public bool Repair(Structure building)
        {
            if (Paused) return Fail("Resume the colony before starting repairs.");
            if (building == null || !Structures.Contains(building) || building.Health >= Structure.MaxHealth || building.RepairRemaining > 0)
                return Fail("Select a damaged building that is not already being repaired.");
            building.RepairRemaining = RepairDuration;
            Message = "Repairs started: 10 seconds, no credits or power needed. Incoming damage interrupts repairs.";
            return true;
        }

        private void DamageBuilding(Structure building, float damage)
        {
            if (building.Disabled) return;
            building.Health = Math.Max(0, building.Health - damage);
            building.RepairRemaining = 0;
            if (!building.Disabled) return;
            building.SuppliedFraction = building.Generation = 0;
            Revision++;
            Reconnect();
            Message = building.Kind + " disabled at " + building.Origin + ". Select it to repair; stored cargo and its layout are safe.";
        }

        private void StepDefense(float delta)
        {
            foreach (var building in Structures)
            {
                if (building.RepairRemaining <= 0) continue;
                building.RepairRemaining = Math.Max(0, building.RepairRemaining - delta);
                if (building.RepairRemaining > 0) continue;
                building.Health = Structure.MaxHealth;
                Revision++;
                Reconnect();
                Message = building.Kind + " repaired. Previous pause and train-service settings are preserved.";
            }
            foreach (var shot in LaserShots) shot.Remaining -= delta;
            LaserShots.RemoveAll(shot => shot.Remaining <= 0);
            if (!raidsEnabled) return;
            if (RaidsStarted)
            {
                NextWaveIn -= delta;
                if (NextWaveIn <= 0)
                {
                    WaveNumber++;
                    int count = Math.Min(Math.Min(2 + (WaveNumber - 1) / 2, 6), MaxAliens - Aliens.Count);
                    pendingAliens = count;
                    spawnRemaining = 0;
                    NextWaveIn = WaveInterval;
                    Message = $"Alien wave {WaveNumber}: {count} incoming from the northwest. Powered turrets fire automatically.";
                }
                spawnRemaining -= delta;
                if (pendingAliens > 0 && spawnRemaining <= 0 && Aliens.Count < MaxAliens)
                {
                    Aliens.Add(new Alien { Id = ++nextAlienId, X = AlienSpawn.X, Y = AlienSpawn.Y });
                    pendingAliens--;
                    spawnRemaining = 1.5f;
                }
            }
            foreach (var turret in Structures)
            {
                if (turret.Kind != StructureKind.Turret) continue;
                turret.ShotCooldown = Math.Max(0, turret.ShotCooldown - delta);
                if (turret.Disabled || !turret.Connected || turret.Paused || turret.ShotCooldown > 0 || Battery < TurretShotPower) continue;
                var target = Aliens.Where(alien => alien.Health > 0 && CanShoot(turret, alien))
                    .OrderBy(alien => DistanceSquared(turret.Origin.X, turret.Origin.Y, alien.X, alien.Y)).ThenBy(alien => alien.Id).FirstOrDefault();
                if (target == null) continue;
                Battery -= TurretShotPower;
                turret.ShotCooldown = TurretInterval;
                target.Health = Math.Max(0, target.Health - TurretDamage);
                LaserShots.Add(new LaserShot { Turret = turret, TargetX = target.X, TargetY = target.Y });
                if (target.Health <= 0) AliensDefeated++;
            }
            Aliens.RemoveAll(alien => alien.Health <= 0);
            foreach (var alien in Aliens) StepAlien(alien, delta);
        }

        private static float DistanceSquared(float firstX, float firstY, float secondX, float secondY)
            => (firstX - secondX) * (firstX - secondX) + (firstY - secondY) * (firstY - secondY);

        public bool CanShoot(Structure turret, Alien alien)
        {
            if (!IsRevealed(new Cell((int)Math.Round(alien.X), (int)Math.Round(alien.Y)))) return false;
            float distance = (float)Math.Sqrt(DistanceSquared(turret.Origin.X, turret.Origin.Y, alien.X, alien.Y));
            if (distance > TurretRange) return false;
            float startHeight = Terrain.HeightAt(turret.Origin.X, turret.Origin.Y) + 1.2f;
            float endHeight = Terrain.HeightAt(alien.X, alien.Y) + 0.6f;
            int samples = Math.Max(1, (int)Math.Ceiling(distance * 4));
            for (int index = 1; index < samples; index++)
            {
                float fraction = (float)index / samples;
                float column = turret.Origin.X + (alien.X - turret.Origin.X) * fraction;
                float row = turret.Origin.Y + (alien.Y - turret.Origin.Y) * fraction;
                if (Terrain.HeightAt(column, row) > startHeight + (endHeight - startHeight) * fraction) return false;
            }
            return true;
        }

        private void StepAlien(Alien alien, float delta)
        {
            alien.AttackCooldown = Math.Max(0, alien.AttackCooldown - delta);
            alien.RepathRemaining -= delta;
            var cell = new Cell((int)Math.Round(alien.X), (int)Math.Round(alien.Y));
            bool centered = Math.Abs(alien.X - cell.X) + Math.Abs(alien.Y - cell.Y) < 0.001f;
            if (centered && (alien.RouteRevision != Revision || alien.Target == null || alien.Target.Disabled || alien.RepathRemaining <= 0))
                PlanAlien(alien, cell);
            float budget = AlienSpeed * delta;
            while (budget > 0.00001f && alien.Waypoint < alien.Route.Count)
            {
                var next = alien.Route[alien.Waypoint];
                if (StructureAt(next) != null) { alien.Route.Clear(); alien.RouteRevision = -1; break; }
                if (Math.Abs(alien.X - next.X) + Math.Abs(alien.Y - next.Y) < 0.0001f) { alien.Waypoint++; continue; }
                budget -= Terrain.MoveTowards(ref alien.X, ref alien.Y, next, budget);
                if (Math.Abs(alien.X - next.X) + Math.Abs(alien.Y - next.Y) < 0.0001f) alien.Waypoint++;
            }
            if (alien.Target == null || alien.Target.Disabled || alien.Waypoint < alien.Route.Count || alien.AttackCooldown > 0) return;
            cell = new Cell((int)Math.Round(alien.X), (int)Math.Round(alien.Y));
            if (Math.Abs(alien.X - cell.X) + Math.Abs(alien.Y - cell.Y) > 0.001f || !CanAttackFrom(cell, alien.Target)) return;
            DamageBuilding(alien.Target, AlienDamage);
            alien.AttackCooldown = 1;
        }

        private bool CanAttackFrom(Cell cell, Structure building)
            => Directions.Any(direction => building.Contains(cell + direction) && Terrain.CanTraverse(cell, cell + direction));

        private void PlanAlien(Alien alien, Cell start)
        {
            alien.Target = null;
            alien.Route.Clear();
            alien.Waypoint = 0;
            alien.RouteRevision = Revision;
            alien.RepathRemaining = 1;
            var frontier = new Queue<Cell>();
            var previous = new Dictionary<Cell, Cell> { [start] = start };
            frontier.Enqueue(start);
            while (frontier.Count > 0)
            {
                var current = frontier.Dequeue();
                var target = Structures.FirstOrDefault(building => !building.Disabled && CanAttackFrom(current, building));
                if (target != null)
                {
                    alien.Target = target;
                    var route = new List<Cell> { current };
                    while (!current.Equals(start)) { current = previous[current]; route.Add(current); }
                    route.Reverse();
                    alien.Route = route;
                    return;
                }
                foreach (var direction in Directions)
                {
                    var next = current + direction;
                    if (previous.ContainsKey(next) || !Terrain.CanTraverse(current, next) || StructureAt(next) != null) continue;
                    previous[next] = current;
                    frontier.Enqueue(next);
                }
            }
        }
    }
}
