using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace AstraExpress
{
    public sealed partial class AstraGame
    {
        private Structure linkTarget, linkSelection;
        private Tool linkTool;
        private bool linkSuppressed;
        private Transform linkGhost;
        private Material linkFillMaterial, linkLineMaterial;
        private readonly List<Cell> linkStops = new List<Cell>();
        private int linkRevision = -1, linkReveal = -1, linkCredits = -1, linkCost, linkSegment;
        private string linkReason = "", linkSuccess = "";
        private float linkSuccessUntil;
        private bool ConnectionPanelVisible => !Simulation.Paused && !pickingTile && (NetworkTool || Time.unscaledTime < linkSuccessUntil);
        private Rect LinkGuidePanel => ConnectionPanelVisible ? new Rect(16, 88, Mathf.Min(640, UiWidth - (SidebarVisible ? 326 : 32)), 82) : Rect.zero;
        private Rect ConnectionCancelRect => new Rect(LinkGuidePanel.xMax - 110, LinkGuidePanel.y + 9, 96, 26);
        private bool LinkGuideActive => NetworkTool && linkTarget != null && !linkSuppressed && !Simulation.Paused;

        private void ResetLinkGuide()
        {
            linkTarget = linkSelection = null;
            linkGhost = null;
            linkStops.Clear();
            linkSuppressed = false;
            linkRevision = -1;
            linkSuccessUntil = 0;
        }

        private void HideLinkGuide()
        {
            coachInputResumeFrame = Time.frameCount + 1;
            linkSuppressed = true;
            linkSuccessUntil = 0;
            if (linkGhost != null) linkGhost.gameObject.SetActive(false);
        }

        private static Material TransparentGuideMaterial(Material material)
        {
            material.SetFloat("_Surface", 1);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            return material;
        }

        private int LinkSegmentIndex(List<Cell> stops, bool rail)
        {
            var network = rail ? Simulation.Rails : Simulation.Conduits;
            int segment = 0;
            while (segment + 2 < stops.Count && ColonySimulation.Corridor(stops[segment], stops[segment + 1]).All(network.Contains)) segment++;
            return segment;
        }

        private string LinkCompletion(Structure building, bool rail)
        {
            if (!rail)
            {
                if (building.Kind == StructureKind.Solar) return "POWER LINKED · This solar array now supplies the colony.";
                if (building.Kind == StructureKind.PowerPlant) return building.Paused ? "POWER LINKED · Resume this plant after Fluxite arrives." : "POWER LINKED · Delivered Fluxite can fuel this plant when the battery needs power.";
                return building.Paused ? "POWER LINKED · Resume this extractor to start mining." : "POWER LINKED · Rails and an assigned train carry the mined resource.";
            }
            if (building.Kind == StructureKind.PowerPlant) return "PLANT RAIL LINKED · Select a Fluxite extractor and choose this plant as its destination.";
            if (building.Deposit?.Resource == ResourceKind.Fluxite)
            {
                var destination = CoachFuelDestination(building);
                if (destination == null) return "RAIL LINKED · Build a power plant, then choose it for Fluxite deliveries.";
                if (!destination.Connected) return "RAIL LINKED · Connect the selected plant's power port next.";
                if (Simulation.RailRoute(destination) == null) return "RAIL LINKED · Connect the selected plant to the same rail network.";
            }
            if (Simulation.Trains.Any(train => train.Source == building)) return "RAIL LINKED · This extractor already has an assigned train.";
            return Simulation.Trains.Any(train => train.Phase == TrainPhase.Parked)
                ? "RAIL LINKED · Select this extractor and choose Dispatch idle train."
                : "RAIL LINKED · Open Fleet to buy a locomotive or park an existing service.";
        }

        private void UpdateLinkGuide()
        {
            if (selected != linkSelection) { linkSelection = selected; linkSuppressed = false; }
            Structure next = null;
            Tool nextTool = Tool.Conduit;
            if (selected != null && (selected.Kind == StructureKind.Extractor || selected.Kind == StructureKind.PowerPlant) && tool == Tool.Rail && Simulation.RailRoute(selected) == null)
            { next = selected; nextTool = Tool.Rail; }
            else if (selected != null && !selected.Connected) next = selected;
            if (next != linkTarget || nextTool != linkTool)
            {
                if (linkTarget != null && !linkSuppressed &&
                    (linkTool == Tool.Conduit ? linkTarget.Connected : Simulation.RailRoute(linkTarget) != null))
                {
                    linkSuccess = LinkCompletion(linkTarget, linkTool == Tool.Rail);
                    linkSuccessUntil = Time.unscaledTime + 4;
                }
                linkTarget = next; linkTool = nextTool; linkRevision = -1; linkStops.Clear();
                if (linkGhost != null) Destroy(linkGhost.gameObject);
                linkGhost = null;
            }
            if (linkGhost != null) linkGhost.gameObject.SetActive(LinkGuideActive && !routeStart.HasValue);
            if (!LinkGuideActive) return;
            if (linkRevision == Simulation.Revision && linkReveal == Simulation.RevealRevision && linkCredits == Simulation.Credits) return;
            linkRevision = Simulation.Revision; linkReveal = Simulation.RevealRevision; linkCredits = Simulation.Credits;
            if (linkGhost != null) Destroy(linkGhost.gameObject);
            linkGhost = null; linkStops.Clear();
            var route = CoachPath(linkTarget, linkTool == Tool.Rail);
            linkReason = route.reason;
            if (!route.possible) return;
            linkCost = route.cost;
            linkStops.AddRange(route.stops.Select(p => new Cell(p.x, p.y)));
            linkSegment = route.nextSegment;
            if (linkFillMaterial == null)
            {
                linkFillMaterial = TransparentGuideMaterial(MakeMaterial(new Color(cyan.r, cyan.g, cyan.b, 0.12f)));
                linkLineMaterial = TransparentGuideMaterial(MakeMaterial(new Color(cyan.r, cyan.g, cyan.b, 0.85f), 0.8f));
            }
            linkGhost = new GameObject("Suggested connection · preview only").transform;
            linkGhost.SetParent(worldRoot, false);
            linkGhost.gameObject.SetActive(!routeStart.HasValue);
            var cells = new HashSet<Cell>();
            for (int segment = linkSegment; segment + 1 < linkStops.Count; segment++)
            {
                var path = ColonySimulation.Corridor(linkStops[segment], linkStops[segment + 1]);
                foreach (var cell in path) cells.Add(cell);
                for (int i = 0; i + 1 < path.Count; i++)
                {
                    Vector3 start = Position(path[i], 0.35f), end = Position(path[i + 1], 0.35f);
                    GuideLine(new[] { GuideSurface(Vector3.Lerp(start, end, 0.08f)), GuideSurface(Vector3.Lerp(start, end, 0.42f)) }, 0.12f);
                    GuideLine(new[] { GuideSurface(Vector3.Lerp(start, end, 0.58f)), GuideSurface(Vector3.Lerp(start, end, 0.92f)) }, 0.12f);
                }
            }
            foreach (var cell in cells)
            {
                var tile = Box("Suggested route tile", linkGhost, Position(cell, 0.22f), new Vector3(1.70f, 0.025f, 1.70f), linkFillMaterial);
                tile.transform.rotation = GroundRotation(cell.X, cell.Y, Vector3.forward);
                tile.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            for (int i = linkSegment; i < linkStops.Count; i++)
            {
                Vector3 corner = Position(linkStops[i], 0.34f) - new Vector3(0.93f, 0, 0.93f);
                GuideLine(new[] { corner, corner + Vector3.right * 1.86f, corner + new Vector3(1.86f, 0, 1.86f), corner + Vector3.forward * 1.86f, corner }.Select(point => GuideSurface(point, 0.34f)).ToArray(), 0.075f);
            }
        }

        private Vector3 GuideSurface(Vector3 point, float height = 0.35f) => Position(point.x / 2, point.z / 2, height);

        private void GuideLine(Vector3[] points, float width)
        {
            var line = new GameObject("Ghost route").AddComponent<LineRenderer>();
            line.transform.SetParent(linkGhost, false);
            line.sharedMaterial = linkLineMaterial;
            line.widthMultiplier = width;
            line.positionCount = points.Length;
            line.SetPositions(points);
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.generateLightingData = true;
        }

        private bool OverLinkGuide(Vector2 point) => ConnectionPanelVisible && LinkGuidePanel.Contains(point);

        // Preview and camera only: neither credits nor network tiles change here.
        public void CoachGuideLink(string request)
        {
            string[] parts = request.Split(',');
            if (parts.Length != 3 || (parts[0] != "Conduit" && parts[0] != "Rail") ||
                !int.TryParse(parts[1], out int x) || !int.TryParse(parts[2], out int y)) return;
            var building = Simulation.Structures.FirstOrDefault(b => b.Origin.Equals(new Cell(x, y)));
            if (building == null || building.Kind == StructureKind.Colony) return;
            if (parts[0] == "Rail" && building.Kind != StructureKind.Extractor && building.Kind != StructureKind.PowerPlant) return;
            StopFollowingRover();
            selected = building;
            Tool requestedTool = parts[0] == "Rail" ? Tool.Rail : Tool.Conduit;
            if (tool != requestedTool) SetTool(requestedTool);
            trainSelected = false;
            linkSuppressed = false;
            UpdateLinkGuide();
            var bounds = new Bounds(Position(Simulation.Colony.Port), Vector3.zero);
            bounds.Encapsulate(Position(building.Port));
            foreach (var stop in linkStops) bounds.Encapsulate(Position(stop));
            Vector3 center = bounds.center;
            Vector3 span = bounds.size;
            Vector3 right = worldCamera.transform.right, up = worldCamera.transform.up;
            float horizontal = (Mathf.Abs(span.x * right.x) + Mathf.Abs(span.y * right.y) + Mathf.Abs(span.z * right.z)) * 0.5f + 3;
            float vertical = (Mathf.Abs(span.x * up.x) + Mathf.Abs(span.y * up.y) + Mathf.Abs(span.z * up.z)) * 0.5f + 3;
            worldCamera.orthographicSize = Mathf.Clamp(Mathf.Max(horizontal / (worldCamera.aspect * 0.60f), vertical / 0.55f), 10, 31);
            cameraTarget = center + worldCamera.transform.right * (worldCamera.orthographicSize * worldCamera.aspect * 0.18f);
            PositionCamera();
            coachTimer = 1;
        }

        private void DrawLinkGuide()
        {
            if (!ConnectionPanelVisible) return;
            Rect panel = LinkGuidePanel;
            Panel(panel);
            Fill(new Rect(panel.x, panel.y, 3, panel.height), tool == Tool.Rail ? gold : cyan);
            string title = tool == Tool.Rail ? "RAIL CONNECTION" : "POWER CONNECTION";
            string instruction;
            if (!LinkGuideActive && Time.unscaledTime < linkSuccessUntil)
            {
                title = "CONNECTION COMPLETE";
                instruction = linkSuccess;
            }
            else if (routeStart.HasValue)
            {
                title += "  ·  START SELECTED";
                instruction = "Click a highlighted destination port or tile. The full route bends automatically.";
                if (hover.HasValue && !routeStart.Value.Equals(NetworkEndpoint(hover.Value)))
                {
                    bool valid = Simulation.TryPlanNetworkRoute(routeStart.Value, NetworkEndpoint(hover.Value), tool == Tool.Rail, verticalFirst, out _, out int cost, out string reason);
                    title = valid ? (tool == Tool.Rail ? "RAIL" : "POWER") + $" CONNECTION  ·  {cost} credits" : "CHOOSE ANOTHER DESTINATION";
                    instruction = valid ? "Click to build this path. R prefers the other bend; Escape cancels." : reason;
                }
            }
            else
            {
                if (LinkGuideActive && linkStops.Count > 1) title += $"  ·  {linkCost} credits suggested";
                instruction = LinkGuideActive && linkStops.Count < 2 ? linkReason : "Click a glowing start port, then the destination port. The full route bends automatically.";
            }
            GUI.Label(new Rect(panel.x + 14, panel.y + 9, panel.width - 138, 22), title, smallStyle);
            GUI.Label(new Rect(panel.x + 14, panel.y + 34, panel.width - 28, 42), instruction, bodyStyle);
            if (Button(ConnectionCancelRect, NetworkTool ? "Cancel" : "Done"))
            {
                routeStart = null;
                SetTool(Tool.Explore);
                HideLinkGuide();
            }
        }
    }
}
