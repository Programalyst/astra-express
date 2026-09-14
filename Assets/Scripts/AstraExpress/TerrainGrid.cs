using System;

namespace AstraExpress
{
    public enum TerrainKind { Flat, Ramp, Hillside }

    public sealed class TerrainGrid
    {
        public const float LevelHeight = 1.5f;
        public const float CellSize = 2;
        public const int HillsideColumn = 17;
        public const int NorthernHillsideColumn = 9;
        public const int NorthernHillsideRow = 18;
        public const int NorthernRampColumn = 4;
        public const int NorthernValleyRampRow = 22;
        private static readonly Plateau[] plateaus =
        {
            new Plateau(17, 29, 3, 28),
            new Plateau(1, 9, NorthernHillsideRow, 26)
        };

        private sealed class Plateau
        {
            public readonly int West, East, South, North;
            public Plateau(int west, int east, int south, int north)
            {
                West = west; East = east; South = south; North = north;
            }
            public bool Contains(Cell cell) => cell.X >= West && cell.X <= East && cell.Y >= South && cell.Y <= North;
            public bool Interior(Cell cell) => cell.X > West && cell.X < East && cell.Y > South && cell.Y < North;
            public float Height(float column, float row) => Math.Max(0, Math.Min(1,
                Math.Min(Math.Min(column - West + 0.5f, East + 0.5f - column),
                    Math.Min(row - South + 0.5f, North + 0.5f - row))));
        }

        private Plateau Boundary(Cell cell)
        {
            foreach (var plateau in plateaus)
                if (plateau.Contains(cell) && !plateau.Interior(cell)) return plateau;
            return null;
        }

        public TerrainKind Kind(Cell cell)
        {
            if (Boundary(cell) == null) return TerrainKind.Flat;
            if (cell.X == HillsideColumn && (cell.Y == 5 || cell.Y == 15)
                || cell.X == NorthernRampColumn && cell.Y == NorthernHillsideRow
                || cell.X == NorthernHillsideColumn && cell.Y == NorthernValleyRampRow) return TerrainKind.Ramp;
            return TerrainKind.Hillside;
        }

        public int Elevation(Cell cell)
        {
            foreach (var plateau in plateaus)
                if (plateau.Interior(cell)) return 1;
            return 0;
        }
        public bool Walkable(Cell cell) => ColonySimulation.InBounds(cell) && Kind(cell) != TerrainKind.Hillside;
        public bool IsCorner(Cell cell)
        {
            var plateau = Boundary(cell);
            return plateau != null && (cell.X == plateau.West || cell.X == plateau.East)
                && (cell.Y == plateau.South || cell.Y == plateau.North);
        }

        public float CornerYaw(Cell cell)
        {
            var plateau = Boundary(cell);
            if (plateau == null || !IsCorner(cell)) return 0;
            return cell.Y == plateau.South ? (cell.X == plateau.East ? 90 : 180)
                : (cell.X == plateau.East ? 0 : 270);
        }

        public Cell Uphill(Cell cell)
        {
            var plateau = Boundary(cell);
            if (plateau == null) return new Cell(0, 0);
            if (cell.X == plateau.West) return new Cell(1, 0);
            if (cell.X == plateau.East) return new Cell(-1, 0);
            return new Cell(0, cell.Y == plateau.South ? 1 : -1);
        }

        public float HeightAt(float column, float row)
        {
            float height = 0;
            foreach (var plateau in plateaus) height = Math.Max(height, plateau.Height(column, row));
            return LevelHeight * height;
        }

        public bool CanTraverse(Cell from, Cell to)
        {
            if (!Walkable(from) || !Walkable(to)) return false;
            if (Math.Abs(from.X - to.X) + Math.Abs(from.Y - to.Y) != 1) return false;
            if (Kind(from) == TerrainKind.Ramp || Kind(to) == TerrainKind.Ramp)
            {
                Cell uphill = Uphill(Kind(from) == TerrainKind.Ramp ? from : to);
                if ((to.X - from.X) * uphill.Y != (to.Y - from.Y) * uphill.X) return false;
            }
            if (Kind(from) == TerrainKind.Flat && Kind(to) == TerrainKind.Flat) return Elevation(from) == Elevation(to);
            return true;
        }

        public float EdgeCost(Cell from, Cell to)
        {
            float middleX = (from.X + to.X) * 0.5f;
            float middleY = (from.Y + to.Y) * 0.5f;
            float middleHeight = HeightAt(middleX, middleY);
            float firstRise = (middleHeight - HeightAt(from.X, from.Y)) / CellSize;
            float secondRise = (HeightAt(to.X, to.Y) - middleHeight) / CellSize;
            return (float)(Math.Sqrt(0.25f + firstRise * firstRise) + Math.Sqrt(0.25f + secondRise * secondRise));
        }

        public float MoveTowards(ref float column, ref float row, Cell target, float budget)
        {
            float offsetX = target.X - column;
            float offsetY = target.Y - row;
            float distance = (float)Math.Sqrt(offsetX * offsetX + offsetY * offsetY);
            if (distance < 0.00001f) return 0;
            float segment = distance;
            float directionX = offsetX / distance;
            float directionY = offsetY / distance;
            if (Math.Abs(directionX) > 0.00001f)
            {
                float boundary = (float)Math.Floor(column + 0.5f) + 0.5f * Math.Sign(directionX);
                float untilBoundary = (boundary - column) / directionX;
                if (untilBoundary > 0.00001f) segment = Math.Min(segment, untilBoundary);
            }
            if (Math.Abs(directionY) > 0.00001f)
            {
                float boundary = (float)Math.Floor(row + 0.5f) + 0.5f * Math.Sign(directionY);
                float untilBoundary = (boundary - row) / directionY;
                if (untilBoundary > 0.00001f) segment = Math.Min(segment, untilBoundary);
            }
            float rise = (HeightAt(column + directionX * segment, row + directionY * segment) - HeightAt(column, row)) / CellSize;
            float surfaceDistance = (float)Math.Sqrt(segment * segment + rise * rise);
            float spent = Math.Min(budget, surfaceDistance);
            float travel = segment * spent / surfaceDistance;
            column += directionX * travel;
            row += directionY * travel;
            return spent;
        }
    }
}
