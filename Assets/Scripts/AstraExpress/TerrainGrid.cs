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
        public const int NorthernHillsideRow = 15;
        public const int NorthernRampColumn = 4;

        public TerrainKind Kind(Cell cell)
        {
            if (cell.X == HillsideColumn) return cell.Y == 5 || cell.Y == 15 ? TerrainKind.Ramp : TerrainKind.Hillside;
            if (cell.X <= NorthernHillsideColumn && cell.Y == NorthernHillsideRow)
                return cell.X == NorthernRampColumn ? TerrainKind.Ramp : TerrainKind.Hillside;
            if (cell.X == NorthernHillsideColumn && cell.Y > NorthernHillsideRow) return TerrainKind.Hillside;
            return TerrainKind.Flat;
        }

        public int Elevation(Cell cell) => cell.X > HillsideColumn || cell.X < NorthernHillsideColumn && cell.Y > NorthernHillsideRow ? 1 : 0;
        public bool Walkable(Cell cell) => ColonySimulation.InBounds(cell) && Kind(cell) != TerrainKind.Hillside;
        public bool IsCorner(Cell cell) => cell.X == NorthernHillsideColumn && cell.Y == NorthernHillsideRow;

        public Cell Uphill(Cell cell)
        {
            if (cell.X == HillsideColumn) return new Cell(1, 0);
            if (cell.Y == NorthernHillsideRow) return new Cell(0, 1);
            return new Cell(-1, 0);
        }

        public float HeightAt(float column, float row)
        {
            float easternHeight = Math.Max(0, Math.Min(1, column - HillsideColumn + 0.5f));
            float northernHeight = Math.Max(0, Math.Min(1, Math.Min(NorthernHillsideColumn + 0.5f - column, row - NorthernHillsideRow + 0.5f)));
            return LevelHeight * Math.Max(easternHeight, northernHeight);
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
