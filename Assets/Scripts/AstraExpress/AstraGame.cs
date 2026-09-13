using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace AstraExpress
{
    public sealed partial class AstraGame : MonoBehaviour
    {
        public GameObject RoverModel;
        public GameObject ColonyModel;
        public GameObject SolarModel;
        public GameObject ExtractorModel;
        public GameObject OreModel;
        public GameObject TrainModel;
        public Material SurfaceTemplate;
        [SerializeField] private string diagnostics;
        public ColonySimulation Simulation { get; private set; }
        private enum Tool { Explore, Extractor, Solar, Conduit, Rail }
        private Tool tool;
        private Camera worldCamera;
        private Transform worldRoot;
        private Transform roverVisual;
        private Transform trainVisual;
        private Transform cargoVisual;
        private readonly Dictionary<Cell, Renderer> ground = new Dictionary<Cell, Renderer>();
        private readonly Dictionary<Cell, GameObject> ore = new Dictionary<Cell, GameObject>();
        private readonly Dictionary<Structure, Transform> buildings = new Dictionary<Structure, Transform>();
        private readonly Dictionary<Material, Material> converted = new Dictionary<Material, Material>();
        private readonly List<Material> ownedMaterials = new List<Material>();
        private Transform networkRoot;
        private LineRenderer preview;
        private Material fogMaterial;
        private Material groundMaterial;
        private Material alternateGround;
        private Material foundationMaterial;
        private Material powerMaterial;
        private Material darkPowerMaterial;
        private Material railMaterial;
        private Material orangeMaterial;
        private Material whiteMaterial;
        private Material previewMaterial;
        private Structure selected;
        private bool trainSelected;
        private Cell? hover;
        private Cell? routeStart;
        private bool verticalFirst;
        private int revision = -1;
        private int revealRevision = -1;
        private float diagnosticTimer;
        private int previousDeliveries;
        private Vector3 cameraTarget;
        private GUIStyle titleStyle;
        private GUIStyle headingStyle;
        private GUIStyle bodyStyle;
        private GUIStyle smallStyle;
        private GUIStyle buttonStyle;
        private GUIStyle labelStyle;
        private readonly Color ink = new Color(0.91f, 0.94f, 0.98f);
        private readonly Color muted = new Color(0.59f, 0.68f, 0.79f);
        private readonly Color cyan = new Color(0.36f, 0.87f, 0.84f);
        private readonly Color gold = new Color(1f, 0.77f, 0.39f);
        private float UiScale => Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
        private float UiWidth => Screen.width / UiScale;
        private float UiHeight => Screen.height / UiScale;
        private Rect Sidebar => new Rect(UiWidth - 294, 88, 278, 420);

        private void Start()
        {
            Application.targetFrameRate = 60;
            worldCamera = Camera.main;
            cameraTarget = new Vector3(15, 0, 16);
            worldCamera.orthographic = true;
            worldCamera.orthographicSize = 16;
            worldCamera.transform.rotation = Quaternion.Euler(55, 20, 0);
            PositionCamera();
            fogMaterial = MakeMaterial(new Color(0.10f, 0.13f, 0.21f));
            groundMaterial = MakeMaterial(new Color(0.33f, 0.32f, 0.43f));
            alternateGround = MakeMaterial(new Color(0.35f, 0.34f, 0.46f));
            foundationMaterial = MakeMaterial(new Color(0.20f, 0.25f, 0.33f));
            powerMaterial = MakeMaterial(cyan, 0.3f);
            darkPowerMaterial = MakeMaterial(new Color(0.29f, 0.41f, 0.47f));
            railMaterial = MakeMaterial(new Color(0.80f, 0.84f, 0.85f));
            orangeMaterial = MakeMaterial(new Color(0.95f, 0.52f, 0.20f));
            whiteMaterial = MakeMaterial(ink);
            previewMaterial = MakeMaterial(cyan, 0.25f);
            ResetWorld();
        }

        private Material MakeMaterial(Color color, float glow = 0)
        {
            var material = new Material(SurfaceTemplate);
            material.SetColor("_BaseColor", color);
            material.enableInstancing = true;
            if (glow > 0)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * glow);
            }
            ownedMaterials.Add(material);
            return material;
        }

        private void ResetWorld()
        {
            ResetCoach();
            ResetLinkGuide();
            if (worldRoot != null) Destroy(worldRoot.gameObject);
            worldRoot = new GameObject("Colony world").transform;
            ground.Clear(); ore.Clear(); buildings.Clear();
            selected = null; trainSelected = false; routeStart = null; tool = Tool.Explore;
            Simulation = new ColonySimulation();
            previousDeliveries = 0;
            revision = -1; revealRevision = -1;
            for (int column = 0; column < ColonySimulation.Width; column++)
                for (int row = 0; row < ColonySimulation.Height; row++)
                {
                    var cell = new Cell(column, row);
                    var tile = Box("Ground " + cell, worldRoot, Position(cell, -0.22f), new Vector3(1.97f, 0.4f, 1.97f), fogMaterial);
                    ground[cell] = tile.GetComponent<Renderer>();
                }
            foreach (var deposit in Simulation.Deposits)
                foreach (var cell in ColonySimulation.Footprint(deposit.Origin, deposit.Size))
                {
                    var cluster = Model(OreModel, "Ore", worldRoot, Position(cell, 0), 1.55f, 0.8f);
                    cluster.transform.Rotate(0, (cell.X * 37 + cell.Y * 19) % 360, 0);
                    ore[cell] = cluster;
                }
            roverVisual = Model(RoverModel, "Rover 01", worldRoot, Position(7, 6, 0.1f), 1.2f, 0.9f).transform;
            trainVisual = Model(TrainModel, "Astra locomotive", worldRoot, Position(5, 6, 0.24f), 1.7f, 0.8f).transform;
            cargoVisual = Box("Ore payload", trainVisual, new Vector3(0, 0.7f, -0.2f), new Vector3(0.45f, 0.3f, 0.55f), orangeMaterial).transform;
            cargoVisual.gameObject.SetActive(false);
            var previewObject = new GameObject("Placement preview");
            previewObject.transform.SetParent(worldRoot);
            preview = previewObject.AddComponent<LineRenderer>();
            preview.sharedMaterial = previewMaterial;
            preview.widthMultiplier = 0.09f;
            preview.positionCount = 0;
            preview.shadowCastingMode = ShadowCastingMode.Off;
            SyncWorld();
        }

        private GameObject Box(string objectName, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
            instance.name = objectName;
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = position;
            instance.transform.localScale = scale;
            instance.GetComponent<Renderer>().sharedMaterial = material;
            Destroy(instance.GetComponent<Collider>());
            return instance;
        }

        private GameObject Model(GameObject prefab, string objectName, Transform parent, Vector3 position, float width, float height)
        {
            var holder = new GameObject(objectName);
            holder.transform.SetParent(parent, false);
            holder.transform.localPosition = position;
            if (prefab == null)
            {
                Box("Placeholder", holder.transform, new Vector3(0, height * 0.5f, 0), new Vector3(width, height, width), whiteMaterial);
                return holder;
            }
            var model = Instantiate(prefab, holder.transform);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return holder;
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            float scale = Mathf.Min(width / Mathf.Max(bounds.size.x, bounds.size.z, 0.01f), height / Mathf.Max(bounds.size.y, 0.01f));
            Vector3 center = bounds.center - holder.transform.position;
            model.transform.localScale *= scale;
            model.transform.localPosition = new Vector3(-center.x * scale, -(bounds.min.y - holder.transform.position.y) * scale, -center.z * scale);
            foreach (var renderer in renderers)
            {
                var materials = renderer.sharedMaterials;
                for (int index = 0; index < materials.Length; index++)
                {
                    var source = materials[index];
                    if (source == null) { materials[index] = whiteMaterial; continue; }
                    if (!converted.TryGetValue(source, out var material))
                    {
                        Color color = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;
                        material = MakeMaterial(color);
                        Texture texture = source.HasProperty("_BaseMap") ? source.GetTexture("_BaseMap") : source.HasProperty("_MainTex") ? source.GetTexture("_MainTex") : null;
                        if (texture != null) material.SetTexture("_BaseMap", texture);
                        converted[source] = material;
                    }
                    materials[index] = material;
                }
                renderer.sharedMaterials = materials;
            }
            foreach (var collider in model.GetComponentsInChildren<Collider>()) Destroy(collider);
            return holder;
        }

        private static Vector3 Position(Cell cell, float height = 0) => Position(cell.X, cell.Y, height);
        private static Vector3 Position(float column, float row, float height = 0) => new Vector3(column * 2, height, row * 2);

        private void Update()
        {
            if (Simulation == null) return;
            HandleInput();
            Simulation.Step(Time.deltaTime);
            SyncWorld();
            UpdatePowerVisuals();
            UpdateLinkGuide();
            MoveVisual(roverVisual, Position(Simulation.RoverX, Simulation.RoverY, 0.08f));
            MoveVisual(trainVisual, Position(Simulation.Train.X, Simulation.Train.Y, 0.24f));
            cargoVisual.gameObject.SetActive(Simulation.Train.Cargo > 0);
            UpdatePreview();
            diagnosticTimer += Time.unscaledDeltaTime;
            if (diagnosticTimer >= 1)
            {
                diagnosticTimer = 0;
                diagnostics = $"credits={Simulation.Credits}; battery={Simulation.Battery:F1}; generation={Simulation.Generation}; rover={Simulation.RoverX:F1},{Simulation.RoverY:F1}; produced={Simulation.Produced}; sold={Simulation.Sold}; cargo={Simulation.Train.Cargo}; accounted={Simulation.AccountedOre}; deliveries={Simulation.Deliveries}; train={Simulation.Train.Phase}; mines={Simulation.Structures.Count(structure => structure.Kind == StructureKind.Extractor)}; conduits={Simulation.Conduits.Count}; rails={Simulation.Rails.Count}";
            }
            if (previousDeliveries != Simulation.Deliveries)
            {
                previousDeliveries = Simulation.Deliveries;
                Debug.Log("ASTRA_DELIVERY " + diagnostics);
            }
            PublishCoach();
        }

        private void MoveVisual(Transform visual, Vector3 position)
        {
            Vector3 direction = position - visual.position;
            direction.y = 0;
            if (direction.sqrMagnitude > 0.000001f) visual.rotation = Quaternion.Slerp(visual.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 12);
            visual.position = position;
        }

        private void SyncWorld()
        {
            if (Simulation.RevealRevision != revealRevision)
            {
                revealRevision = Simulation.RevealRevision;
                foreach (var pair in ground) pair.Value.sharedMaterial = Simulation.IsRevealed(pair.Key) ? ((pair.Key.X + pair.Key.Y) % 2 == 0 ? groundMaterial : alternateGround) : fogMaterial;
                foreach (var pair in ore) pair.Value.SetActive(Simulation.IsRevealed(pair.Key) && Simulation.DepositAt(pair.Key).Extractor == null);
            }
            if (Simulation.Revision == revision) return;
            revision = Simulation.Revision;
            foreach (var structure in Simulation.Structures)
            {
                if (buildings.ContainsKey(structure)) continue;
                Vector3 center = Position(structure.Origin) + new Vector3(structure.Size - 1, 0, structure.Size - 1);
                var root = new GameObject(structure.Kind + " " + structure.Origin).transform;
                root.SetParent(worldRoot);
                root.position = center;
                Box("Foundation", root, new Vector3(0, 0.04f, 0), new Vector3(structure.Size * 1.93f, 0.2f, structure.Size * 1.93f), foundationMaterial);
                GameObject prefab = structure.Kind == StructureKind.Colony ? ColonyModel : structure.Kind == StructureKind.Solar ? SolarModel : ExtractorModel;
                Model(prefab, structure.Kind.ToString(), root, new Vector3(0, 0.15f, 0), structure.Size * 1.8f, structure.Kind == StructureKind.Colony ? 2.6f : 1.5f);
                if (structure.Kind == StructureKind.Extractor)
                {
                    Box("Ore processing tower", root, new Vector3(0.3f, 0.9f, 0.3f), new Vector3(0.35f, 1.5f, 0.35f), orangeMaterial);
                    foreach (var cell in ColonySimulation.Footprint(structure.Origin, structure.Size)) if (ore.TryGetValue(cell, out var cluster)) cluster.SetActive(false);
                }
                buildings[structure] = root;
            }
            if (networkRoot != null) Destroy(networkRoot.gameObject);
            networkRoot = new GameObject("Rail and power networks").transform;
            networkRoot.SetParent(worldRoot);
            foreach (var cell in Simulation.Rails)
            {
                Box("Track bed", networkRoot, Position(cell, 0.04f), new Vector3(1.15f, 0.15f, 1.15f), foundationMaterial);
                Box("Rail node", networkRoot, Position(cell, 0.18f), new Vector3(0.6f, 0.13f, 0.6f), railMaterial);
                foreach (var direction in ColonySimulation.Directions)
                {
                    var adjacent = cell + direction;
                    if (!Simulation.Rails.Contains(adjacent) || direction.X + direction.Y < 0) continue;
                    Vector3 midpoint = (Position(cell) + Position(adjacent)) * 0.5f;
                    if (direction.X != 0)
                    {
                        Box("Rail", networkRoot, midpoint + new Vector3(0, 0.17f, -0.3f), new Vector3(2, 0.12f, 0.12f), railMaterial);
                        Box("Rail", networkRoot, midpoint + new Vector3(0, 0.17f, 0.3f), new Vector3(2, 0.12f, 0.12f), railMaterial);
                    }
                    else
                    {
                        Box("Rail", networkRoot, midpoint + new Vector3(-0.3f, 0.17f, 0), new Vector3(0.12f, 0.12f, 2), railMaterial);
                        Box("Rail", networkRoot, midpoint + new Vector3(0.3f, 0.17f, 0), new Vector3(0.12f, 0.12f, 2), railMaterial);
                    }
                }
            }
            DrawPowerNetwork();
        }

        private bool OverUi(Vector2 screen)
        {
            Vector2 point = new Vector2(screen.x / UiScale, (Screen.height - screen.y) / UiScale);
            return OverLinkGuide(point) || point.y < 72 || point.y > UiHeight - 128 || Sidebar.Contains(point) || new Rect(16, 88, 280, 165).Contains(point);
        }

        private void HandleInput()
        {
            if (coachInputBlocked || Time.frameCount <= coachInputResumeFrame) return;
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            if (mouse == null) return;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame) { HideLinkGuide(); routeStart = null; tool = Tool.Explore; }
                if (keyboard.spaceKey.wasPressedThisFrame) Simulation.Paused = !Simulation.Paused;
                if (keyboard.digit1Key.wasPressedThisFrame) SetTool(Tool.Explore);
                if (keyboard.digit2Key.wasPressedThisFrame) SetTool(Tool.Extractor);
                if (keyboard.digit3Key.wasPressedThisFrame) SetTool(Tool.Solar);
                if (keyboard.digit4Key.wasPressedThisFrame) SetTool(Tool.Conduit);
                if (keyboard.digit5Key.wasPressedThisFrame) SetTool(Tool.Rail);
                if (keyboard.rKey.wasPressedThisFrame) verticalFirst = !verticalFirst;
                if (keyboard.cKey.wasPressedThisFrame) CenterColony();
                if (keyboard.vKey.wasPressedThisFrame) CenterRover();
                Vector3 pan = Vector3.zero;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) pan.x--;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) pan.x++;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) pan.z++;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) pan.z--;
                cameraTarget += pan * (Time.unscaledDeltaTime * worldCamera.orthographicSize);
            }
            Vector2 screen = mouse.position.ReadValue();
            if (mouse.middleButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                Vector3 right = worldCamera.transform.right;
                Vector3 forward = Vector3.ProjectOnPlane(worldCamera.transform.up, Vector3.up).normalized;
                cameraTarget -= (right * delta.x + forward * delta.y) * (worldCamera.orthographicSize * 2 / Screen.height);
            }
            if (!OverUi(screen)) worldCamera.orthographicSize = Mathf.Clamp(worldCamera.orthographicSize - mouse.scroll.ReadValue().y * 0.018f, 8, 31);
            cameraTarget.x = Mathf.Clamp(cameraTarget.x, 0, (ColonySimulation.Width - 1) * 2);
            cameraTarget.z = Mathf.Clamp(cameraTarget.z, 0, (ColonySimulation.Height - 1) * 2);
            PositionCamera();
            hover = null;
            if (OverUi(screen)) return;
            var ray = worldCamera.ScreenPointToRay(screen);
            if (new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float distance))
            {
                var point = ray.GetPoint(distance);
                var cell = new Cell(Mathf.RoundToInt(point.x / 2), Mathf.RoundToInt(point.z / 2));
                if (ColonySimulation.InBounds(cell)) hover = cell;
            }
            if (mouse.rightButton.wasPressedThisFrame) { routeStart = null; tool = Tool.Explore; }
            if (!mouse.leftButton.wasPressedThisFrame || !hover.HasValue || Simulation.Paused) return;
            Cell target = hover.Value;
            if (tool == Tool.Explore)
            {
                selected = Simulation.IsRevealed(target) ? Simulation.StructureAt(target) : null;
                trainSelected = false;
                if (selected == null) Simulation.OrderRover(target);
            }
            else if (tool == Tool.Extractor || tool == Tool.Solar)
            {
                if (Simulation.Build(tool == Tool.Extractor ? StructureKind.Extractor : StructureKind.Solar, target))
                {
                    selected = Simulation.StructureAt(target);
                    tool = Tool.Explore;
                }
            }
            else PlaceNetworkAt(target);
        }

        private void PlaceNetworkAt(Cell target)
        {
            if (Simulation.Paused || (tool != Tool.Conduit && tool != Tool.Rail)) return;
            if (!routeStart.HasValue)
            {
                if (Simulation.CanLay(new[] { target }, tool == Tool.Rail, out _, out string reason)) routeStart = target;
                else Simulation.Message = reason;
            }
            else
            {
                if (Simulation.Lay(ColonySimulation.Corridor(routeStart.Value, target, verticalFirst), tool == Tool.Rail)) routeStart = null;
            }
        }

        private void SetTool(Tool next) { if (next == Tool.Conduit || next == Tool.Rail) linkSuppressed = false; tool = next; routeStart = null; trainSelected = false; }
        private void PositionCamera() => worldCamera.transform.position = cameraTarget - worldCamera.transform.forward * 48;
        private void CenterColony() { cameraTarget = Position(7, 8); PositionCamera(); }
        private void CenterRover() { cameraTarget = Position(Simulation.RoverX, Simulation.RoverY); PositionCamera(); }

        private void UpdatePreview()
        {
            preview.positionCount = 0;
            if (!hover.HasValue || Simulation.Paused) return;
            Cell origin = hover.Value;
            int size = 1;
            bool valid = Simulation.IsRevealed(origin);
            if (tool == Tool.Extractor || tool == Tool.Solar)
                valid = Simulation.CanBuild(tool == Tool.Extractor ? StructureKind.Extractor : StructureKind.Solar, origin, out origin, out size, out _, out _);
            if (routeStart.HasValue)
            {
                var path = ColonySimulation.Corridor(routeStart.Value, origin, verticalFirst);
                valid = Simulation.CanLay(path, tool == Tool.Rail, out _, out _);
                preview.positionCount = path.Count;
                preview.SetPositions(path.Select(cell => Position(cell, 0.35f)).ToArray());
            }
            else
            {
                Vector3 corner = Position(origin, 0.3f) - new Vector3(0.93f, 0, 0.93f);
                float extent = size * 2 - 0.14f;
                preview.positionCount = 5;
                preview.SetPositions(new[] { corner, corner + Vector3.right * extent, corner + new Vector3(extent, 0, extent), corner + Vector3.forward * extent, corner });
            }
            previewMaterial.SetColor("_BaseColor", valid ? cyan : new Color(1, 0.35f, 0.38f));
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && Simulation != null) Simulation.Paused = true;
        }

        private void OnGUI()
        {
            if (Simulation == null) return;
            if ((coachInputBlocked || Time.frameCount <= coachInputResumeFrame) && (Event.current.isMouse || Event.current.isKey || Event.current.type == EventType.ScrollWheel)) Event.current.Use();
            Styles();
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * UiScale);
            DrawWorldLabels();
            Panel(new Rect(0, 0, UiWidth, 72));
            GUI.Label(new Rect(22, 10, 300, 32), "ASTRA EXPRESS", titleStyle);
            GUI.Label(new Rect(24, 43, 300, 20), "FRONTIER 01   /   FIRST LIGHT", smallStyle);
            GUI.Label(new Rect(365, 12, 190, 27), $"{Simulation.Credits:N0} credits", headingStyle);
            GUI.Label(new Rect(365, 42, 200, 20), $"{Simulation.Sold} ore delivered  /  {Simulation.Deliveries} trips", smallStyle);
            GUI.Label(new Rect(585, 10, 280, 26), $"SHARED BATTERY   {Simulation.Battery:0} / 100", bodyStyle);
            Fill(new Rect(585, 39, 175, 6), new Color(0.17f, 0.23f, 0.30f));
            Fill(new Rect(585, 39, 175 * Simulation.Battery / 100, 6), Simulation.Battery < 12 ? gold : cyan);
            GUI.Label(new Rect(585, 49, 360, 20), $"SOLAR +{Simulation.Generation:0}/s    MINING -{Simulation.Demand:0.#}/s", smallStyle);
            if (Button(new Rect(UiWidth - 214, 17, 92, 36), Simulation.Paused ? "Resume" : "Pause", Simulation.Paused)) Simulation.Paused = !Simulation.Paused;
            if (Button(new Rect(UiWidth - 112, 17, 92, 36), "Restart")) { ResetWorld(); return; }
            DrawObjective();
            DrawSelection();
            DrawToolbar();
            DrawLinkGuide();
            if (Simulation.Paused)
            {
                Panel(new Rect(UiWidth / 2 - 180, UiHeight / 2 - 65, 360, 122));
                GUI.Label(new Rect(UiWidth / 2 - 160, UiHeight / 2 - 50, 320, 30), "COLONY PAUSED", headingStyle);
                GUI.Label(new Rect(UiWidth / 2 - 160, UiHeight / 2 - 18, 320, 28), "Press Space or Resume to continue.", bodyStyle);
                if (Button(new Rect(UiWidth / 2 - 100, UiHeight / 2 + 17, 200, 30), "Resume exploration", true)) Simulation.Paused = false;
            }
        }

        private void DrawObjective()
        {
            bool discovered = Simulation.Deposits.Any(Simulation.FullyRevealed);
            bool built = Simulation.Structures.Any(structure => structure.Kind == StructureKind.Extractor);
            bool powered = Simulation.Structures.Any(structure => structure.Kind == StructureKind.Extractor && structure.Connected);
            bool routed = Simulation.Train.Source != null;
            int stage = Simulation.Deliveries > 0 ? 5 : routed ? 4 : powered ? 3 : built ? 2 : discovered ? 1 : 0;
            string[] titles = { "Explore the frontier", "Build your first extractor", "Bring the mine online", "Connect a paying railway", "Your first delivery", "A colony in motion" };
            string[] descriptions = {
                "Click ground east of the colony. Your rover reveals ore hidden by the fog.",
                "Choose Extractor, then click the orange ore patch. Keep its south port clear.",
                "Choose Conduit. Click the colony's cyan port, then the extractor's south port.",
                "Lay rails between those same ports. Select your extractor and dispatch the train.",
                "The train collects local ore and sells it at the colony. Only deliveries earn credits.",
                "Keep this mine earning. Explore farther for larger patches, then expand power and capacity." };
            Panel(new Rect(16, 88, 280, 165));
            GUI.Label(new Rect(32, 102, 248, 20), stage == 5 ? "FIRST ROUTE ESTABLISHED" : $"MISSION   {stage + 1} / 5", smallStyle);
            GUI.Label(new Rect(32, 127, 248, 48), titles[stage], headingStyle);
            GUI.Label(new Rect(32, 176, 248, 65), descriptions[stage], bodyStyle);
        }

        private void DrawSelection()
        {
            Rect panel = Sidebar;
            Panel(panel);
            float left = panel.x + 18;
            float width = panel.width - 36;
            GUI.Label(new Rect(left, 103, width, 22), trainSelected ? "RAIL OPERATIONS" : selected != null ? "COLONY INFRASTRUCTURE" : "EXPLORATION CONTROL", smallStyle);
            string title = trainSelected ? "Astra locomotive" : selected == null ? "Rover 01" : selected.Kind == StructureKind.Colony ? "Landing colony" : selected.Kind == StructureKind.Solar ? "Solar array" : "Ore extractor";
            GUI.Label(new Rect(left, 132, width, 32), title, titleStyle);
            float row = 180;
            if (trainSelected)
            {
                Stat(left, ref row, "SERVICE", TrainStatus());
                Stat(left, ref row, "CARGO", $"{Simulation.Train.Cargo} / {Simulation.Train.Capacity} ore");
                Stat(left, ref row, "PAYMENT", "8 credits / ore");
                bool parked = Simulation.Train.Phase == TrainPhase.Parked;
                bool parking = Simulation.Train.ParkRequested && !parked;
                if (Button(new Rect(left, row + 10, width, 38), parked ? "Parked at colony" : parking ? "Parking at colony..." : "Park at colony", enabled: !parked && !parking)) Simulation.ParkTrain();
                int cost = Simulation.Train.CapacityLevel * 100;
                bool maximum = Simulation.Train.CapacityLevel >= 3;
                if (Button(new Rect(left, row + 58, width, 38), maximum ? "Capacity fully upgraded" : $"Capacity +4 / {cost} cr", enabled: !maximum && Simulation.Credits >= cost)) Simulation.UpgradeTrain();
                GUI.Label(new Rect(left, row + 101, width, 35), maximum ? "Maximum capacity: 12 ore." : Simulation.Credits < cost ? $"Need {cost - Simulation.Credits} more credits." : "Adds four cargo slots at the next loading stop.", smallStyle);
            }
            else if (selected != null && selected.Kind == StructureKind.Extractor)
            {
                string status = Simulation.Paused ? "Colony paused" : selected.Paused ? "Mine paused" : !selected.Connected ? "Needs power" : selected.Stock >= selected.Storage ? "Storage full" : selected.SuppliedFraction < 0.99f ? "Low power" : "Mining";
                Stat(left, ref row, "STATUS", status);
                Stat(left, ref row, "STORAGE", $"{selected.Stock} / {selected.Storage} ore");
                Stat(left, ref row, "OUTPUT", $"{selected.Rate:0.##}/s / {selected.Demand:0.#} power/s");
                bool railConnected = Simulation.RailRoute(selected) != null;
                bool trainRunning = Simulation.Train.Phase != TrainPhase.Parked;
                bool served = trainRunning && Simulation.Train.Source == selected;
                bool parking = trainRunning && Simulation.Train.ParkRequested;
                Readiness(new Rect(left, row + 3, width, 42), selected.Connected, railConnected, trainRunning, served, parking);
                string dispatchAction = Simulation.Paused ? "Resume colony" : !selected.Connected ? "Show power connection" : !railConnected ? "Show rail connection" :
                    parking ? "Parking at colony..." : trainRunning ? served ? "Train running" : "Park to switch mine" : "Dispatch train";
                bool actionEnabled = Simulation.Paused || !selected.Connected || !railConnected || (!parking && !served);
                if (Button(new Rect(left, row + 61, width, 38), dispatchAction, active: actionEnabled, enabled: actionEnabled))
                {
                    if (Simulation.Paused) Simulation.Paused = false;
                    else if (!selected.Connected) CoachGuideLink($"Conduit,{selected.Origin.X},{selected.Origin.Y}");
                    else if (!railConnected) CoachGuideLink($"Rail,{selected.Origin.X},{selected.Origin.Y}");
                    else if (trainRunning) { Simulation.ParkTrain(); SetTool(Tool.Explore); }
                    else if (Simulation.Dispatch(selected)) SetTool(Tool.Explore);
                }
                GUI.Label(new Rect(left, row + 102, width, 18), parking ? "Wait here, then choose Dispatch train." : served ? TrainStatus() : $"{selected.Size} x {selected.Size} deposit · Level {selected.Level}", interfaceSmall);
                if (Button(new Rect(left, row + 122, 112, 36), selected.Paused ? "Resume mine" : "Pause mine")) selected.Paused = !selected.Paused;
                int upgradeCost = selected.Level * 120;
                bool maximum = selected.Level >= 3;
                if (Button(new Rect(left + 122, row + 122, width - 122, 36), maximum ? "Max level" : $"Upgrade\n{upgradeCost} cr", enabled: !maximum && Simulation.Credits >= upgradeCost)) Simulation.UpgradeExtractor(selected);
                if (!maximum && Simulation.Credits < upgradeCost) GUI.Label(new Rect(left, row + 160, width, 16), $"Upgrade needs {upgradeCost - Simulation.Credits} more credits.", interfaceSmall);
            }
            else if (selected != null)
            {
                Stat(left, ref row, "POWER", selected.Connected ? "CONNECTED" : "WIRE SOUTH PORT");
                Stat(left, ref row, "SOUTH PORT", selected.Port.ToString());
                Stat(left, ref row, selected.Kind == StructureKind.Solar ? "GENERATION" : "DEPOT", selected.Kind == StructureKind.Solar ? "+2 power / second" : "Ore arrives here for credits");
                GUI.Label(new Rect(left, row + 14, width, 85), "Conduits and tracks are separate networks. They can share a corridor, but rails do not transmit power.", bodyStyle);
            }
            else
            {
                Stat(left, ref row, "POSITION", Simulation.RoverCell.ToString());
                Stat(left, ref row, "MOVEMENT", Simulation.RoverMoving ? "EXPLORING" : "AWAITING ORDERS");
                Stat(left, ref row, "ENERGY", "2 power / tile travelled");
                GUI.Label(new Rect(left, row + 16, width, 100), "Click ground to explore. Previously revealed terrain stays visible. At low power, pause mines and let solar recharge.", bodyStyle);
            }
            if (Button(new Rect(left, panel.yMax - 52, 112, 34), "Colony [C]")) CenterColony();
            if (Button(new Rect(left + 122, panel.yMax - 52, width - 122, 34), "Rover [V]")) CenterRover();
        }

        private void Stat(float left, ref float row, string name, string value)
        {
            GUI.Label(new Rect(left, row, 90, 23), name, smallStyle);
            GUI.Label(new Rect(left + 92, row - 2, 152, 28), value, bodyStyle);
            row += 32;
        }

        private void DrawToolbar()
        {
            float bottom = UiHeight - 88;
            Panel(new Rect(16, bottom, UiWidth - 32, 72));
            string[] names = { "Explore", "Extractor", "Solar", "Conduit", "Rail" };
            string[] subtitles = { "Reveal the frontier", "From 150 cr", "100 cr / +2 power", "Power / 2 cr per tile", "Ore / 3 cr per tile" };
            string[] icons = { "rover", "extractor", "solar", "conduit", "rail" };
            for (int index = 0; index < names.Length; index++)
                if (ToolbarCard(new Rect(28 + index * 157, bottom + 13, 147, 44), names[index], subtitles[index], icons[index], (index + 1).ToString(), tool == (Tool)index && !trainSelected, index == 4 || index == 1 ? gold : cyan)) SetTool((Tool)index);
            if (ToolbarCard(new Rect(813, bottom + 13, 160, 44), "Train", "Operations / upgrades", "train", null, trainSelected, gold)) { trainSelected = true; selected = null; tool = Tool.Explore; routeStart = null; }
            GUI.Label(new Rect(990, bottom + 12, UiWidth - 1030, 50), "WASD / middle drag: pan\nScroll: zoom   Space: pause", smallStyle);
            Fill(new Rect(16, bottom - 38, UiWidth - 32, 30), new Color(0.045f, 0.07f, 0.12f, 0.9f));
            string message = Simulation.Message;
            if (tool == Tool.Conduit || tool == Tool.Rail)
            {
                message = routeStart.HasValue ? "Choose the end tile. R changes bend direction. Right click cancels." : "Click the start port, then the end port. Routes can share existing tiles for free.";
                if (routeStart.HasValue && hover.HasValue)
                {
                    bool valid = Simulation.CanLay(ColonySimulation.Corridor(routeStart.Value, hover.Value, verticalFirst), tool == Tool.Rail, out int cost, out string reason);
                    message = valid ? $"BUILD {tool.ToString().ToUpper()}   {cost} credits   /   Click to confirm; R changes bend; right click cancels." : reason;
                }
            }
            else if ((tool == Tool.Extractor || tool == Tool.Solar) && hover.HasValue)
            {
                bool valid = Simulation.CanBuild(tool == Tool.Extractor ? StructureKind.Extractor : StructureKind.Solar, hover.Value, out _, out int size, out int cost, out string reason);
                message = valid ? $"BUILD {tool.ToString().ToUpper()}   {size} x {size} tiles   /   {cost} credits   /   Click to confirm." : reason;
            }
            GUI.Label(new Rect(28, bottom - 33, UiWidth - 60, 25), message, bodyStyle);
        }

        private void DrawWorldLabels()
        {
            foreach (var deposit in Simulation.Deposits)
            {
                if (!Simulation.FullyRevealed(deposit) || deposit.Extractor != null) continue;
                WorldLabel(Position(deposit.Origin) + new Vector3(deposit.Size - 1, 1.3f, deposit.Size - 1), $"ORE  {deposit.Size}x{deposit.Size}  /  {deposit.Rate:0.##}/s", gold);
            }
            if (!LinkGuideActive || linkStops.Count < 2) WorldLabel(Position(Simulation.Colony.Port, 0.2f), "COLONY PORT", cyan);
            if (selected != null && selected != Simulation.Colony && (!LinkGuideActive || linkStops.Count < 2))
                WorldLabel(Position(selected.Port, 0.2f),
                    selected.Connected ? "POWER CONNECTED" : "CONNECT POWER HERE",
                    selected.Connected ? cyan : gold);
        }

        private void WorldLabel(Vector3 position, string caption, Color accent)
        {
            Vector3 screen = worldCamera.WorldToScreenPoint(position);
            if (screen.z < 0) return;
            var rectangle = new Rect(screen.x / UiScale - 85, (Screen.height - screen.y) / UiScale + 8, 170, 24);
            if (rectangle.y < 74 || rectangle.yMax > UiHeight - 130 || rectangle.Overlaps(Sidebar) || rectangle.Overlaps(new Rect(16, 88, 280, 165))) return;
            Fill(rectangle, new Color(0.04f, 0.065f, 0.10f, 0.9f));
            Fill(new Rect(rectangle.x, rectangle.y, 3, rectangle.height), accent);
            GUI.Label(rectangle, caption, labelStyle);
        }

        private void OnDestroy()
        {
            if (worldRoot != null) Destroy(worldRoot.gameObject);
            foreach (var material in ownedMaterials) if (material != null) Destroy(material);
        }
    }
}
