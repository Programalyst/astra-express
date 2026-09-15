using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace AstraExpress
{
    public sealed partial class AstraGame
    {
        private GameObject networkOptionsRoot;
        private Mesh networkOptionsMesh;
        private Material networkOptionsMaterial;
        private Dictionary<Cell, int> networkOptions = new Dictionary<Cell, int>();
        private readonly List<Structure> networkPortOptions = new List<Structure>();
        private Cell? optionsStart;
        private Tool optionsTool;
        private int optionsRevision = -1, optionsReveal = -1, optionsCredits = -1;
        private bool NetworkTool => tool == Tool.Conduit || tool == Tool.Rail;

        private Cell NetworkEndpoint(Cell requested)
        {
            var building = Simulation.IsRevealed(requested) ? Simulation.StructureAt(requested) : null;
            if (building != null && (tool == Tool.Conduit || building.Kind != StructureKind.Solar && building.Kind != StructureKind.Turret)) return building.Port;
            return requested;
        }

        private void ResetNetworkPlacement()
        {
            if (networkOptionsRoot != null) Destroy(networkOptionsRoot);
            if (networkOptionsMesh != null) Destroy(networkOptionsMesh);
            networkOptionsRoot = null; networkOptionsMesh = null;
            networkOptions.Clear(); networkPortOptions.Clear(); optionsRevision = -1;
        }

        private void BeginBuildingConnection(Structure building)
        {
            if (building == null || building.Connected) return;
            SetTool(Tool.Conduit);
            routeStart = building.Port;
            Simulation.Message = "Building placed. Its power port is selected; click a glowing colony/grid tile to connect it.";
        }

        private void UpdateNetworkPlacement()
        {
            bool active = NetworkTool && !Simulation.Paused && !pickingTile;
            if (networkOptionsRoot != null) networkOptionsRoot.SetActive(active);
            if (!active) return;
            if (optionsRevision == Simulation.Revision && optionsReveal == Simulation.RevealRevision &&
                optionsCredits == Simulation.Credits && System.Nullable.Equals(optionsStart, routeStart) && optionsTool == tool) return;
            optionsRevision = Simulation.Revision; optionsReveal = Simulation.RevealRevision;
            optionsCredits = Simulation.Credits; optionsStart = routeStart; optionsTool = tool;
            bool rail = tool == Tool.Rail;
            networkOptions = routeStart.HasValue ? Simulation.NetworkConnectionCosts(routeStart.Value, rail)
                .Where(pair => pair.Value <= Simulation.Credits).ToDictionary(pair => pair.Key, pair => pair.Value) : new Dictionary<Cell, int>();
            networkPortOptions.Clear();
            foreach (var building in Simulation.Structures)
                if ((!rail || building.Kind != StructureKind.Solar && building.Kind != StructureKind.Turret) && (!building.Starter || building == Simulation.Colony || Simulation.PoweredCells.Contains(building.Port)) && Simulation.IsRevealed(building.Port) &&
                    (!routeStart.HasValue ? Simulation.CanLay(new[] { building.Port }, rail, out _, out _) : networkOptions.ContainsKey(building.Port)))
                    networkPortOptions.Add(building);
            if (networkOptionsMaterial == null)
            {
                var shader = Resources.Load<Shader>("Rendering/AstraPlacementTiles");
                if (shader == null || !shader.isSupported) return;
                networkOptionsMaterial = new Material(shader) { name = "Available connection tiles" };
                ownedMaterials.Add(networkOptionsMaterial);
            }
            if (networkOptionsRoot == null)
            {
                networkOptionsRoot = new GameObject("Available connection destinations");
                networkOptionsRoot.transform.SetParent(worldRoot, false);
                networkOptionsRoot.AddComponent<MeshFilter>();
                var renderer = networkOptionsRoot.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = networkOptionsMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            }
            if (networkOptionsMesh != null) Destroy(networkOptionsMesh);
            var vertices = new List<Vector3>(); var colors = new List<Color>(); var triangles = new List<int>();
            Color tint = rail ? gold : cyan;
            var ports = new HashSet<Cell>(networkPortOptions.Select(building => building.Port));
            var network = rail ? Simulation.Rails : Simulation.PoweredCells;
            var cells = new HashSet<Cell>(networkOptions.Keys); cells.UnionWith(ports);
            foreach (var cell in cells)
            {
                bool start = routeStart.HasValue && routeStart.Value.Equals(cell);
                bool strong = ports.Contains(cell) || network.Contains(cell);
                Color fill = start ? ink : strong ? tint : new Color(0.02f, 0.18f, 0.22f); fill.a = start ? 0.38f : strong ? 0.32f : 0.25f;
                Color edge = start ? ink : tint; edge.a = strong || start ? 1f : 0.90f;
                TileRect(cell, -0.83f, -0.83f, 1.66f, 1.66f, fill);
                float length = strong || start ? 1.66f : 0.55f;
                TileRect(cell, -0.83f, -0.83f, length, 0.12f, edge);
                TileRect(cell, -0.83f, -0.83f, 0.12f, length, edge);
                TileRect(cell, 0.83f - length, 0.71f, length, 0.12f, edge);
                TileRect(cell, 0.71f, 0.83f - length, 0.12f, length, edge);
            }
            networkOptionsMesh = new Mesh { name = "Legal network targets" };
            networkOptionsMesh.SetVertices(vertices); networkOptionsMesh.SetColors(colors); networkOptionsMesh.SetTriangles(triangles, 0);
            networkOptionsMesh.RecalculateBounds(); networkOptionsRoot.GetComponent<MeshFilter>().sharedMesh = networkOptionsMesh;

            void TileRect(Cell cell, float x, float z, float width, float height, Color color)
            {
                int first = vertices.Count;
                vertices.Add(Position(cell.X + x / 2, cell.Y + z / 2, 0.44f));
                vertices.Add(Position(cell.X + x / 2, cell.Y + (z + height) / 2, 0.44f));
                vertices.Add(Position(cell.X + (x + width) / 2, cell.Y + (z + height) / 2, 0.44f));
                vertices.Add(Position(cell.X + (x + width) / 2, cell.Y + z / 2, 0.44f));
                for (int i = 0; i < 4; i++) colors.Add(color);
                triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
                triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 3);
            }
        }

        private void DrawNetworkPlacement()
        {
            if (!NetworkTool || Simulation.Paused || pickingTile) return;
            Color color = tool == Tool.Rail ? gold : cyan;
            if (routeStart.HasValue)
                WorldLabel(Position(routeStart.Value, 0.7f), "START SELECTED", ink);
            foreach (var building in networkPortOptions)
            {
                if (routeStart.HasValue && routeStart.Value.Equals(building.Port)) continue;
                string name = building.Kind == StructureKind.Colony ? "COLONY" : building.Kind == StructureKind.Solar ? "SOLAR" : building.Kind == StructureKind.Turret ? "TURRET" : building.Kind == StructureKind.PowerPlant ? "PLANT" : "MINE";
                string label = routeStart.HasValue ? name + " · CONNECT" : name + " · START";
                if (!NetworkMarkerRect(building.Port, out Rect marker)) continue;
                Fill(marker, new Color(0.035f, 0.07f, 0.1f, 0.96f));
                Fill(new Rect(marker.x, marker.y, 3, marker.height), color);
                if (GUI.Button(marker, label, labelStyle)) { PlaceNetworkAt(building.Port); coachInputResumeFrame = Time.frameCount + 1; Event.current.Use(); }
            }
        }

        private bool NetworkMarkerRect(Cell cell, out Rect rect)
        {
            Vector3 screen = worldCamera.WorldToScreenPoint(Position(cell, 0.35f));
            rect = new Rect(screen.x / UiScale - 85, (Screen.height - screen.y) / UiScale + 12, 170, 24);
            return screen.z > 0 && rect.x > 8 && rect.xMax < UiWidth - 8 && rect.y > 75 && rect.yMax < UiHeight - 132 &&
                !ContextPanelOverlaps(rect);
        }

        private bool OverNetworkPlacement(Vector2 point)
        {
            if (!NetworkTool || Simulation.Paused || pickingTile) return false;
            return networkPortOptions.Any(building => (!routeStart.HasValue || !routeStart.Value.Equals(building.Port)) &&
                NetworkMarkerRect(building.Port, out Rect rect) && rect.Contains(point));
        }
    }
}
