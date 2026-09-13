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
        private Rect LinkGuidePanel => new Rect(316, 88, UiWidth - 626, 88);
        private bool LinkGuideActive => linkTarget != null && !linkSuppressed && !Simulation.Paused;

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
            if (linkGhost != null) linkGhost.gameObject.SetActive(LinkGuideActive);
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
            var cells = new HashSet<Cell>();
            for (int segment = linkSegment; segment + 1 < linkStops.Count; segment++)
            {
                var path = ColonySimulation.Corridor(linkStops[segment], linkStops[segment + 1]);
                foreach (var cell in path) cells.Add(cell);
                for (int i = 0; i + 1 < path.Count; i++)
                {
                    Vector3 start = Position(path[i], 0.35f), end = Position(path[i + 1], 0.35f);
                    GuideLine(new[] { Vector3.Lerp(start, end, 0.08f), Vector3.Lerp(start, end, 0.42f) }, 0.12f);
                    GuideLine(new[] { Vector3.Lerp(start, end, 0.58f), Vector3.Lerp(start, end, 0.92f) }, 0.12f);
                }
            }
            foreach (var cell in cells)
            {
                var tile = Box("Suggested route tile", linkGhost, Position(cell, 0.22f), new Vector3(1.70f, 0.025f, 1.70f), linkFillMaterial);
                tile.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            for (int i = linkSegment; i < linkStops.Count; i++)
            {
                Vector3 corner = Position(linkStops[i], 0.34f) - new Vector3(0.93f, 0, 0.93f);
                GuideLine(new[] { corner, corner + Vector3.right * 1.86f, corner + new Vector3(1.86f, 0, 1.86f), corner + Vector3.forward * 1.86f, corner }, 0.075f);
            }
        }

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

        private bool LinkMarkerRect(Cell cell, out Rect rectangle)
        {
            Vector3 screen = worldCamera.WorldToScreenPoint(Position(cell, 0.3f));
            rectangle = new Rect(screen.x / UiScale - 85, (Screen.height - screen.y) / UiScale + 8, 170, 24);
            return screen.z > 0 && rectangle.y >= 74 && rectangle.yMax <= UiHeight - 130 &&
                !rectangle.Overlaps(Sidebar) && !rectangle.Overlaps(new Rect(16, 88, 280, 165)) && !rectangle.Overlaps(LinkGuidePanel);
        }

        private bool OverLinkGuide(Vector2 point)
        {
            if (Simulation.Paused) return false;
            if ((LinkGuideActive || Time.unscaledTime < linkSuccessUntil) && LinkGuidePanel.Contains(point)) return true;
            if (LinkGuideActive)
                for (int i = linkSegment; i < linkStops.Count; i++)
                    if (LinkMarkerRect(linkStops[i], out Rect rectangle) && rectangle.Contains(point)) return true;
            return false;
        }

        // Preview and camera only: neither credits nor network tiles change here.
        public void CoachGuideLink(string request)
        {
            string[] parts = request.Split(',');
            if (parts.Length != 3 || (parts[0] != "Conduit" && parts[0] != "Rail") ||
                !int.TryParse(parts[1], out int x) || !int.TryParse(parts[2], out int y)) return;
            var building = Simulation.Structures.FirstOrDefault(b => b.Origin.Equals(new Cell(x, y)));
            if (building == null || building.Kind == StructureKind.Colony) return;
            if (parts[0] == "Rail" && building.Kind != StructureKind.Extractor && building.Kind != StructureKind.PowerPlant) return;
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
            float horizontal = (Mathf.Abs(span.x * right.x) + Mathf.Abs(span.z * right.z)) * 0.5f + 3;
            float vertical = (Mathf.Abs(span.x * up.x) + Mathf.Abs(span.z * up.z)) * 0.5f + 3;
            worldCamera.orthographicSize = Mathf.Clamp(Mathf.Max(horizontal / (worldCamera.aspect * 0.60f), vertical / 0.55f), 10, 31);
            cameraTarget = center + worldCamera.transform.right * (worldCamera.orthographicSize * worldCamera.aspect * 0.18f);
            PositionCamera();
            coachTimer = 1;
        }

        private void DrawLinkGuide()
        {
            if (Simulation.Paused) return;
            if (!LinkGuideActive)
            {
                if (Time.unscaledTime < linkSuccessUntil)
                {
                    Panel(LinkGuidePanel);
                    GUI.Label(new Rect(LinkGuidePanel.x + 14, LinkGuidePanel.y + 17, LinkGuidePanel.width - 28, 55), linkSuccess, headingStyle);
                }
                return;
            }
            Rect panel = LinkGuidePanel;
            Panel(panel);
            Fill(new Rect(panel.x, panel.y, 3, panel.height), cyan);
            bool possible = linkStops.Count > 1;
            string kind = linkTool == Tool.Rail ? linkTarget.Kind == StructureKind.PowerPlant ? "PLANT RAIL" : "RAIL" : "POWER";
            GUI.Label(new Rect(panel.x + 14, panel.y + 8, panel.width - 108, 20),
                possible ? $"SUGGESTED {kind} LINK · {linkCost} credits · PREVIEW" : $"{kind} LINK · ROUTE BLOCKED", smallStyle);
            if (Button(new Rect(panel.xMax - 89, panel.y + 9, 76, 25), "HIDE  X")) { HideLinkGuide(); return; }
            string instruction = linkReason;
            if (possible)
            {
                int first = linkSegment + 1, second = first + 1;
                if (tool != linkTool) instruction = $"Press {(linkTool == Tool.Rail ? 5 : 4)} for {linkTool}. Then click marker {first}, followed by marker {second}.";
                else if (!routeStart.HasValue) instruction = $"Click marker {first} to start. Then click marker {second} to build the highlighted segment.";
                else if (routeStart.Value.Equals(linkStops[linkSegment])) instruction = $"Start selected. Now click marker {second} to build this segment. Right-click cancels.";
                else instruction = $"Your start is outside this suggestion. Right-click to cancel, then start at marker {first}.";
                for (int i = linkSegment; i < linkStops.Count; i++)
                {
                    string endpoint = linkTarget.Kind == StructureKind.PowerPlant ? "PLANT PORT" : linkTarget.Kind == StructureKind.Solar ? "SOLAR PORT" : linkTarget.Deposit?.Resource == ResourceKind.Fluxite ? "FLUXITE PORT" : "EXTRACTOR PORT";
                    string name = i == 0 ? "COLONY PORT" : i == linkStops.Count - 1 ? endpoint : "TURN HERE";
                    bool active = i == linkSegment + (routeStart.HasValue && routeStart.Value.Equals(linkStops[linkSegment]) ? 1 : 0);
                    if (LinkMarkerRect(linkStops[i], out Rect marker))
                    {
                        Fill(marker, new Color(0.04f, 0.065f, 0.10f, 0.93f));
                        Fill(new Rect(marker.x, marker.y, 3, marker.height), active ? cyan : gold);
                        // The label and the highlighted tile both use the same placement rules.
                        if (GUI.Button(marker, $"{i + 1}  {name}", labelStyle))
                        {
                            coachInputResumeFrame = Time.frameCount + 1;
                            if (tool != linkTool) SetTool(linkTool);
                            PlaceNetworkAt(linkStops[i]);
                            Event.current.Use();
                        }
                    }
                }
                float left = 28 + (int)linkTool * 147;
                float bottom = UiHeight - 88;
                Fill(new Rect(left, bottom + 8, 139, 3), cyan);
                Fill(new Rect(left, bottom + 60, 139, 3), cyan);
            }
            GUI.Label(new Rect(panel.x + 14, panel.y + 36, panel.width - 28, 46), instruction, bodyStyle);
        }
    }
}
