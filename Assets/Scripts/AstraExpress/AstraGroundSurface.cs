using System.Collections.Generic;
using UnityEngine;

namespace AstraExpress
{
    public sealed class AstraGroundSurface : MonoBehaviour
    {
        private readonly List<Cell> faces = new List<Cell>();
        private readonly List<int> visibleTriangles = new List<int>();
        private readonly List<int> hiddenTriangles = new List<int>();
        private Mesh surfaceMesh;
        private Mesh collisionMesh;
        private MeshCollider surfaceCollider;

        public void Initialize(TerrainGrid terrain, Material surface, Material fog, float textureRepeat)
        {
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            float repeat = Mathf.Max(0.1f, textureRepeat);
            float halfCell = TerrainGrid.CellSize * 0.5f;
            for (int row = 0; row < ColonySimulation.Height; row++)
                for (int column = 0; column < ColonySimulation.Width; column++)
                {
                    var cell = new Cell(column, row);
                    if (terrain.Kind(cell) != TerrainKind.Flat) continue;
                    float left = column * TerrainGrid.CellSize - halfCell;
                    float right = left + TerrainGrid.CellSize;
                    float bottom = row * TerrainGrid.CellSize - halfCell;
                    float top = bottom + TerrainGrid.CellSize;
                    float height = terrain.Elevation(cell) * TerrainGrid.LevelHeight - 0.02f;
                    AddFace(cell, new Vector3(left, height, bottom), new Vector3(left, height, top),
                        new Vector3(right, height, bottom), new Vector3(right, height, top), false);
                    if (terrain.Elevation(cell) == 0) continue;
                    if (column == 0)
                        AddFace(cell, new Vector3(left, -0.04f, bottom), new Vector3(left, -0.04f, top),
                            new Vector3(left, height, bottom), new Vector3(left, height, top), true);
                    if (column == ColonySimulation.Width - 1)
                        AddFace(cell, new Vector3(right, -0.04f, top), new Vector3(right, -0.04f, bottom),
                            new Vector3(right, height, top), new Vector3(right, height, bottom), true);
                    if (row == 0)
                        AddFace(cell, new Vector3(right, -0.04f, bottom), new Vector3(left, -0.04f, bottom),
                            new Vector3(right, height, bottom), new Vector3(left, height, bottom), true);
                    if (row == ColonySimulation.Height - 1)
                        AddFace(cell, new Vector3(left, -0.04f, top), new Vector3(right, -0.04f, top),
                            new Vector3(left, height, top), new Vector3(right, height, top), true);
                }

            surfaceMesh = new Mesh { name = "Continuous ground with world-space UVs" };
            surfaceMesh.SetVertices(vertices);
            surfaceMesh.SetUVs(0, uv);
            surfaceMesh.SetTriangles(triangles, 0);
            surfaceMesh.RecalculateNormals();
            surfaceMesh.RecalculateTangents();
            surfaceMesh.RecalculateBounds();
            collisionMesh = Instantiate(surfaceMesh);
            collisionMesh.name = "Stable ground picking mesh";
            surfaceCollider = gameObject.AddComponent<MeshCollider>();
            surfaceCollider.sharedMesh = collisionMesh;
            gameObject.AddComponent<MeshFilter>().sharedMesh = surfaceMesh;
            gameObject.AddComponent<MeshRenderer>().sharedMaterials = new[] { surface, fog };
            surfaceMesh.subMeshCount = 2;
            surfaceMesh.SetTriangles(System.Array.Empty<int>(), 0, false);
            surfaceMesh.SetTriangles(triangles, 1, false);

            void AddFace(Cell cell, Vector3 first, Vector3 second, Vector3 third, Vector3 fourth, bool wall)
            {
                int start = vertices.Count;
                faces.Add(cell);
                vertices.Add(first); vertices.Add(second); vertices.Add(third); vertices.Add(fourth);
                foreach (var vertex in new[] { first, second, third, fourth })
                    uv.Add(wall ? new Vector2((first.x == second.x ? vertex.z : vertex.x) / repeat, vertex.y / repeat)
                        : new Vector2(vertex.x / repeat, vertex.z / repeat));
                AddTriangles(triangles, start);
            }
        }

        public void SetRevealed(ColonySimulation simulation)
        {
            visibleTriangles.Clear();
            hiddenTriangles.Clear();
            for (int face = 0; face < faces.Count; face++)
                AddTriangles(simulation.IsRevealed(faces[face]) ? visibleTriangles : hiddenTriangles, face * 4);
            surfaceMesh.SetTriangles(visibleTriangles, 0, false);
            surfaceMesh.SetTriangles(hiddenTriangles, 1, false);
        }

        public bool TryGetCell(RaycastHit hit, out Cell cell)
        {
            int face = hit.triangleIndex / 2;
            if (hit.collider == surfaceCollider && hit.triangleIndex >= 0 && face < faces.Count)
            {
                cell = faces[face];
                return true;
            }
            cell = default;
            return false;
        }

        private static void AddTriangles(List<int> triangles, int start)
        {
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
            triangles.Add(start + 2); triangles.Add(start + 1); triangles.Add(start + 3);
        }

        private void OnDestroy()
        {
            if (surfaceCollider != null) surfaceCollider.sharedMesh = null;
            if (surfaceMesh != null) Destroy(surfaceMesh);
            if (collisionMesh != null) Destroy(collisionMesh);
        }
    }
}
