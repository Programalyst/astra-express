using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace AstraExpress
{
    public sealed class AstraGame : MonoBehaviour
    {
        public GameObject RoverModel;
        public GameObject ColonyModel;
        public GameObject SolarModel;
        public GameObject ExtractorModel;
        public GameObject OreModel;
        public GameObject TrainModel;
        public GameObject TerrainModel;
        public Material SurfaceTemplate;
        [SerializeField] private string diagnostics;
        public ColonySimulation Simulation { get; private set; }
        private enum Tool { Explore, Extractor, Solar, Conduit, Rail, PowerPlant }
        private Tool tool;
        private Camera worldCamera;
        private Transform worldRoot;
        private Transform roverVisual;
        private readonly Dictionary<FreightTrain, Transform> trainVisuals = new Dictionary<FreightTrain, Transform>();
        private readonly Dictionary<FreightTrain, Renderer> cargoVisuals = new Dictionary<FreightTrain, Renderer>();
        private readonly Dictionary<Cell, GroundTile> ground = new Dictionary<Cell, GroundTile>();
        private readonly Dictionary<Cell, GameObject> ore = new Dictionary<Cell, GameObject>();
        private readonly Dictionary<Structure, Transform> buildings = new Dictionary<Structure, Transform>();
        private readonly Dictionary<Material, Material> converted = new Dictionary<Material, Material>();
        private readonly List<Material> ownedMaterials = new List<Material>();
        private Transform networkRoot;
        private LineRenderer preview;
        private Material fogMaterial;
        private Material groundMaterial;
        private Material foundationMaterial;
        private Material powerMaterial;
        private Material darkPowerMaterial;
        private Material railMaterial;
        private Material orangeMaterial;
        private Material whiteMaterial;
        private Material previewMaterial;
        private Material fuelMaterial;
        private Structure selected;
        private Structure fuelDestination;
        private int selectedTrainIndex;
        private bool confirmRestart;
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
        private bool BuildingTool => tool == Tool.Extractor || tool == Tool.Solar || tool == Tool.PowerPlant;
        private StructureKind BuildKind => tool == Tool.PowerPlant ? StructureKind.PowerPlant : tool == Tool.Solar ? StructureKind.Solar : StructureKind.Extractor;

        private sealed class GroundTile
        {
            private readonly Renderer[] renderers;
            private readonly Material[][] surfaceMaterials;
            private readonly Material[][] hiddenMaterials;
            private bool? revealed;

            public GroundTile(GameObject tile, Material fog)
            {
                renderers = tile.GetComponentsInChildren<Renderer>();
                surfaceMaterials = renderers.Select(renderer => renderer.sharedMaterials).ToArray();
                hiddenMaterials = surfaceMaterials.Select(materials => Enumerable.Repeat(fog, materials.Length).ToArray()).ToArray();
            }

            public void SetRevealed(bool visible)
            {
                if (revealed == visible) return;
                revealed = visible;
                for (int index = 0; index < renderers.Length; index++)
                    renderers[index].sharedMaterials = visible ? surfaceMaterials[index] : hiddenMaterials[index];
            }
        }

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
            groundMaterial = MakeMaterial(new Color(0.91f, 0.52f, 0.39f));
            foundationMaterial = MakeMaterial(new Color(0.20f, 0.25f, 0.33f));
            powerMaterial = MakeMaterial(cyan, 0.3f);
            darkPowerMaterial = MakeMaterial(new Color(0.29f, 0.41f, 0.47f));
            railMaterial = MakeMaterial(new Color(0.80f, 0.84f, 0.85f));
            orangeMaterial = MakeMaterial(new Color(0.95f, 0.52f, 0.20f));
            whiteMaterial = MakeMaterial(ink);
            previewMaterial = MakeMaterial(cyan, 0.25f);
            fuelMaterial = MakeMaterial(new Color(0.38f, 0.95f, 0.67f), 0.45f);
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
            if (worldRoot != null) Destroy(worldRoot.gameObject);
            worldRoot = new GameObject("Colony world").transform;
            ground.Clear(); ore.Clear(); buildings.Clear(); trainVisuals.Clear(); cargoVisuals.Clear();
            selected = null; trainSelected = false; routeStart = null; tool = Tool.Explore;
            fuelDestination = null; selectedTrainIndex = 0; confirmRestart = false;
            Simulation = new ColonySimulation();
            previousDeliveries = 0;
            revision = -1; revealRevision = -1;
            for (int column = 0; column < ColonySimulation.Width; column++)
                for (int row = 0; row < ColonySimulation.Height; row++)
                {
                    var cell = new Cell(column, row);
                    var tile = TerrainModel != null
                        ? Model(TerrainModel, "Space terrain " + cell, worldRoot, Position(cell, -0.02f), 1.97f, 0.4f)
                        : Box("Ground " + cell, worldRoot, Position(cell, -0.22f), new Vector3(1.97f, 0.4f, 1.97f), groundMaterial);
                    ground[cell] = new GroundTile(tile, fogMaterial);
                }
            foreach (var deposit in Simulation.Deposits)
                foreach (var cell in ColonySimulation.Footprint(deposit.Origin, deposit.Size))
                {
                    var cluster = Model(OreModel, "Ore", worldRoot, Position(cell, 0), 1.55f, 0.8f);
                    cluster.transform.Rotate(0, (cell.X * 37 + cell.Y * 19) % 360, 0);
                    if (deposit.Resource == ResourceKind.Fluxite)
                        foreach (var renderer in cluster.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = fuelMaterial;
                    ore[cell] = cluster;
                }
            roverVisual = Model(RoverModel, "Rover 01", worldRoot, Position(7, 6, 0.1f), 1.2f, 0.9f).transform;
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
            MoveVisual(roverVisual, Position(Simulation.RoverX, Simulation.RoverY, 0.08f));
            foreach (var train in Simulation.Trains)
            {
                MoveVisual(trainVisuals[train], Position(train.X, train.Y, 0.24f));
                cargoVisuals[train].gameObject.SetActive(train.Cargo > 0);
                cargoVisuals[train].sharedMaterial = train.Resource == ResourceKind.Fluxite ? fuelMaterial : orangeMaterial;
            }
            UpdatePreview();
            diagnosticTimer += Time.unscaledDeltaTime;
            if (diagnosticTimer >= 1)
            {
                diagnosticTimer = 0;
                diagnostics = $"credits={Simulation.Credits}; battery={Simulation.Battery:F1}; generation={Simulation.Generation:F1}; rover={Simulation.RoverX:F1},{Simulation.RoverY:F1}; produced={Simulation.Produced}; sold={Simulation.Sold}; accounted={Simulation.AccountedOre}; deliveries={Simulation.Deliveries}; fleet={Simulation.Trains.Count}; fuelProduced={Simulation.FuelProduced}; fuelAccounted={Simulation.AccountedFuel}; fuelDelivered={Simulation.FuelDelivered}; fuelConsumed={Simulation.FuelConsumed}; plants={Simulation.Structures.Count(structure => structure.Kind == StructureKind.PowerPlant)}";
            }
            if (previousDeliveries != Simulation.Deliveries)
            {
                previousDeliveries = Simulation.Deliveries;
                Debug.Log($"ASTRA_DELIVERY credits={Simulation.Credits}; sold={Simulation.Sold}; trips={Simulation.Deliveries}; ore={Simulation.AccountedOre}/{Simulation.Produced}; fuel={Simulation.AccountedFuel}/{Simulation.FuelProduced}");
            }
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
                foreach (var pair in ground) pair.Value.SetRevealed(Simulation.IsRevealed(pair.Key));
                foreach (var pair in ore) pair.Value.SetActive(Simulation.IsRevealed(pair.Key) && Simulation.DepositAt(pair.Key).Extractor == null);
            }
            if (Simulation.Revision == revision) return;
            revision = Simulation.Revision;
            foreach (var train in Simulation.Trains)
            {
                if (trainVisuals.ContainsKey(train)) continue;
                var visual = Model(TrainModel, "Astra locomotive " + (Simulation.Trains.IndexOf(train) + 1), worldRoot, Position(train.X, train.Y, 0.24f), 1.7f, 0.8f).transform;
                trainVisuals[train] = visual;
                cargoVisuals[train] = Box("Freight payload", visual, new Vector3(0, 0.7f, -0.2f), new Vector3(0.45f, 0.3f, 0.55f), orangeMaterial).GetComponent<Renderer>();
            }
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
                    Box("Processing tower", root, new Vector3(0.3f, 0.9f, 0.3f), new Vector3(0.35f, 1.5f, 0.35f), structure.Deposit.Resource == ResourceKind.Fluxite ? fuelMaterial : orangeMaterial);
                    foreach (var cell in ColonySimulation.Footprint(structure.Origin, structure.Size)) if (ore.TryGetValue(cell, out var cluster)) cluster.SetActive(false);
                }
                if (structure.Kind == StructureKind.PowerPlant)
                {
                    Box("Fluxite reactor", root, new Vector3(-0.95f, 1.25f, 0.7f), new Vector3(0.75f, 2.2f, 0.75f), fuelMaterial);
                    Box("Heat exchanger", root, new Vector3(0.95f, 1.05f, 0.7f), new Vector3(0.55f, 1.8f, 0.55f), railMaterial);
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
            foreach (var cell in Simulation.Conduits)
            {
                Material material = Simulation.PoweredCells.Contains(cell) ? powerMaterial : darkPowerMaterial;
                Vector3 offset = new Vector3(0.66f, 0.10f, 0.66f);
                Box("Power junction", networkRoot, Position(cell) + offset, new Vector3(0.27f, 0.22f, 0.27f), material);
                foreach (var direction in ColonySimulation.Directions)
                {
                    var adjacent = cell + direction;
                    if (!Simulation.Conduits.Contains(adjacent) || direction.X + direction.Y < 0) continue;
                    Box("Conduit", networkRoot, (Position(cell) + Position(adjacent)) * 0.5f + offset, direction.X != 0 ? new Vector3(2, 0.1f, 0.12f) : new Vector3(0.12f, 0.1f, 2), material);
                }
            }
            foreach (var structure in Simulation.Structures)
            {
                Box("Connection port", networkRoot, Position(structure.Port, 0.04f), new Vector3(1.65f, 0.06f, 1.65f), structure.Connected ? powerMaterial : darkPowerMaterial);
            }
        }

        private bool OverUi(Vector2 screen)
        {
            Vector2 point = new Vector2(screen.x / UiScale, (Screen.height - screen.y) / UiScale);
            return point.y < 72 || point.y > UiHeight - 128 || Sidebar.Contains(point) || new Rect(16, 88, 280, 165).Contains(point);
        }

        private void HandleInput()
        {
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            if (mouse == null) return;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame) { routeStart = null; tool = Tool.Explore; confirmRestart = false; }
                if (keyboard.spaceKey.wasPressedThisFrame) Simulation.Paused = !Simulation.Paused;
                if (keyboard.digit1Key.wasPressedThisFrame) SetTool(Tool.Explore);
                if (keyboard.digit2Key.wasPressedThisFrame) SetTool(Tool.Extractor);
                if (keyboard.digit3Key.wasPressedThisFrame) SetTool(Tool.Solar);
                if (keyboard.digit4Key.wasPressedThisFrame) SetTool(Tool.Conduit);
                if (keyboard.digit5Key.wasPressedThisFrame) SetTool(Tool.Rail);
                if (keyboard.digit6Key.wasPressedThisFrame) SetTool(Tool.PowerPlant);
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
            else if (BuildingTool)
            {
                if (Simulation.Build(BuildKind, target))
                {
                    selected = Simulation.StructureAt(target);
                    tool = Tool.Explore;
                }
            }
            else if (!routeStart.HasValue)
            {
                if (Simulation.CanLay(new[] { target }, tool == Tool.Rail, out _, out string reason)) routeStart = target;
                else Simulation.Message = reason;
            }
            else
            {
                if (Simulation.Lay(ColonySimulation.Corridor(routeStart.Value, target, verticalFirst), tool == Tool.Rail)) routeStart = null;
            }
        }

        private void SetTool(Tool next) { tool = next; routeStart = null; trainSelected = false; }
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
            if (BuildingTool)
                valid = Simulation.CanBuild(BuildKind, origin, out origin, out size, out _, out _);
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

        private void Styles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 25, fontStyle = FontStyle.Bold, normal = { textColor = ink } };
            headingStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, normal = { textColor = ink }, wordWrap = true };
            bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, normal = { textColor = ink }, wordWrap = true };
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, normal = { textColor = muted }, wordWrap = true };
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, normal = { textColor = ink } };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = ink }, hover = { textColor = Color.white }, active = { textColor = Color.white } };
        }

        private static void Fill(Rect rectangle, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rectangle, Texture2D.whiteTexture);
            GUI.color = old;
        }

        private bool Button(Rect rectangle, string caption, bool active = false, bool enabled = true)
        {
            Color previous = GUI.backgroundColor;
            bool previousEnabled = GUI.enabled;
            GUI.backgroundColor = active ? new Color(0.18f, 0.65f, 0.65f) : new Color(0.25f, 0.32f, 0.44f);
            GUI.enabled = enabled;
            bool clicked = GUI.Button(rectangle, caption, buttonStyle);
            GUI.enabled = previousEnabled;
            GUI.backgroundColor = previous;
            return clicked;
        }

        private void Panel(Rect rectangle)
        {
            Fill(rectangle, new Color(0.045f, 0.07f, 0.12f, 0.95f));
            Fill(new Rect(rectangle.x, rectangle.y, rectangle.width, 2), new Color(0.25f, 0.43f, 0.51f));
        }

        private void OnGUI()
        {
            if (Simulation == null) return;
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
            GUI.Label(new Rect(585, 49, 460, 20), $"SOLAR +{Simulation.SolarGeneration:0}/s   FUEL +{Simulation.FuelGeneration:0.#}/s   MINES -{Simulation.Demand:0.#}/s", smallStyle);
            if (Button(new Rect(UiWidth - 214, 17, 92, 36), Simulation.Paused ? "RESUME" : "PAUSE", Simulation.Paused)) Simulation.Paused = !Simulation.Paused;
            if (Button(new Rect(UiWidth - 112, 17, 92, 36), confirmRestart ? "CONFIRM?" : "RESTART"))
            {
                if (confirmRestart) { ResetWorld(); return; }
                confirmRestart = true;
                Simulation.Message = "Restart deletes this colony. Click CONFIRM? again to start over; Escape cancels.";
            }
            DrawObjective();
            DrawSelection();
            DrawToolbar();
            if (Simulation.Paused)
            {
                Panel(new Rect(UiWidth / 2 - 180, UiHeight / 2 - 65, 360, 122));
                GUI.Label(new Rect(UiWidth / 2 - 160, UiHeight / 2 - 50, 320, 30), "COLONY PAUSED", headingStyle);
                GUI.Label(new Rect(UiWidth / 2 - 160, UiHeight / 2 - 18, 320, 28), "Press Space or Resume to continue.", bodyStyle);
                if (Button(new Rect(UiWidth / 2 - 100, UiHeight / 2 + 17, 200, 30), "RESUME EXPLORATION", true)) Simulation.Paused = false;
            }
        }

        private void DrawObjective()
        {
            bool discovered = Simulation.Deposits.Any(Simulation.FullyRevealed);
            bool built = Simulation.Structures.Any(structure => structure.Kind == StructureKind.Extractor);
            bool powered = Simulation.Structures.Any(structure => structure.Kind == StructureKind.Extractor && structure.Connected);
            bool routed = Simulation.Trains.Any(train => train.Source != null);
            bool fuelFound = Simulation.Deposits.Any(deposit => deposit.Resource == ResourceKind.Fluxite && Simulation.FullyRevealed(deposit));
            int stage = Simulation.FuelConsumed > 0 ? 8 : Simulation.FuelDelivered > 0 ? 7 : Simulation.Deliveries > 0 ? fuelFound ? 6 : 5 : routed ? 4 : powered ? 3 : built ? 2 : discovered ? 1 : 0;
            string[] titles = { "Explore the frontier", "Build your first extractor", "Bring the mine online", "Connect a paying railway", "Your first delivery", "Find a stronger power source", "Build the fuel supply line", "Light the reactor", "An industrial frontier" };
            string[] descriptions = {
                "Click ground east of the colony. Your rover reveals ore hidden by the fog.",
                "Choose Extractor, then click the orange ore patch. Keep its south port clear.",
                "Choose Conduit. Click the colony's cyan port, then the extractor's south port.",
                "Lay rails between those same ports. Select your extractor and dispatch the train.",
                "The train collects local ore and sells it at the colony. Only deliveries earn credits.",
                "Keep ore earning. Explore southeast for green Fluxite. Buy a second train in Fleet.",
                "Build a Fluxite extractor and plant. Wire both, lay rails, then assign the plant at the mine.",
                "Connect the plant's conduit port and resume it. Fuel burns only when the battery needs power.",
                "Fuel now powers expansion! Find larger ore patches, upgrade mines, and keep fuel arriving." };
            Panel(new Rect(16, 88, 280, 165));
            GUI.Label(new Rect(32, 102, 248, 20), stage == 8 ? "FUEL ECONOMY ESTABLISHED" : stage >= 5 ? "NEXT: FUEL-POWERED FRONTIER" : $"MISSION   {stage + 1} / 5", smallStyle);
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
            string title = trainSelected ? $"Locomotive {selectedTrainIndex + 1}" : selected == null ? "Rover 01" : selected.Kind == StructureKind.Colony ? "Landing colony" : selected.Kind == StructureKind.Solar ? "Solar array" : selected.Kind == StructureKind.PowerPlant ? "Fluxite power plant" : selected.Deposit.Resource == ResourceKind.Fluxite ? "Fluxite extractor" : "Ore extractor";
            GUI.Label(new Rect(left, 132, width, 32), title, titleStyle);
            float row = 180;
            if (trainSelected)
            {
                if (Button(new Rect(left, row, 58, 27), "<")) selectedTrainIndex = (selectedTrainIndex + Simulation.Trains.Count - 1) % Simulation.Trains.Count;
                GUI.Label(new Rect(left + 65, row + 3, 110, 23), $"{selectedTrainIndex + 1} / {Simulation.Trains.Count} trains", bodyStyle);
                if (Button(new Rect(left + width - 58, row, 58, 27), ">")) selectedTrainIndex = (selectedTrainIndex + 1) % Simulation.Trains.Count;
                row += 38;
                var train = Simulation.Trains[selectedTrainIndex];
                string state = train.Phase == TrainPhase.ToColony ? "Delivering" : train.Phase == TrainPhase.Unloading && train.Cargo > 0 && train.Resource == ResourceKind.Fluxite && train.Destination.Stock >= train.Destination.Storage ? "Plant full: waiting" : train.Phase.ToString();
                Stat(left, ref row, "SERVICE", state);
                Stat(left, ref row, "CARGO", $"{train.Cargo} / {train.Capacity} {train.Resource}");
                Stat(left, ref row, "DESTINATION", train.Destination == null ? "Not assigned" : train.Destination == Simulation.Colony ? "Colony / ore buyer" : "Plant " + train.Destination.Origin);
                if (Button(new Rect(left, row + 5, width, 32), train.ParkRequested ? "RETURN REQUESTED" : "PARK AT COLONY", enabled: train.Phase != TrainPhase.Parked && !train.ParkRequested)) Simulation.ParkTrain(train);
                if (Button(new Rect(left, row + 44, width, 32), $"CAPACITY +4 / {train.CapacityLevel * 100} cr", enabled: train.CapacityLevel < 3)) Simulation.UpgradeTrain(train);
                if (Button(new Rect(left, row + 83, width, 32), $"BUY TRAIN / {ColonySimulation.TrainCost} cr", enabled: Simulation.Trains.Count < ColonySimulation.MaxTrains))
                    if (Simulation.BuyTrain()) selectedTrainIndex = Simulation.Trains.Count - 1;
            }
            else if (selected != null && selected.Kind == StructureKind.Extractor)
            {
                string status = selected.Paused ? "PAUSED" : !selected.Connected ? "DISCONNECTED" : selected.Stock >= selected.Storage ? "STORAGE FULL" : selected.SuppliedFraction < 0.99f ? "LOW POWER" : "EXTRACTING";
                Stat(left, ref row, "STATUS", status);
                Stat(left, ref row, "DEPOSIT", $"{selected.Size} x {selected.Size} / infinite");
                Stat(left, ref row, "OUTPUT", $"{selected.Rate:0.##}/s  /  {selected.Demand:0.#} power/s");
                Stat(left, ref row, "STORAGE", $"{selected.Stock} / {selected.Storage} {selected.Deposit.Resource}");
                Stat(left, ref row, "SOUTH PORT", selected.Port.ToString());
                var assigned = Simulation.Trains.FirstOrDefault(train => train.Source == selected);
                if (selected.Deposit.Resource == ResourceKind.Fluxite)
                {
                    var plants = Simulation.Structures.Where(structure => structure.Kind == StructureKind.PowerPlant).ToList();
                    if (fuelDestination == null && plants.Count > 0) fuelDestination = plants[0];
                    string destination = assigned != null ? "TO PLANT " + assigned.Destination.Origin : fuelDestination == null ? "BUILD A POWER PLANT FIRST" : "TO PLANT " + fuelDestination.Origin + "  >";
                    if (Button(new Rect(left, row, width, 27), destination, enabled: assigned == null && plants.Count > 1)) fuelDestination = plants[(plants.IndexOf(fuelDestination) + 1) % plants.Count];
                    row += 31;
                }
                if (Button(new Rect(left, row + 3, width, 30), assigned != null ? "VIEW ASSIGNED TRAIN" : "DISPATCH IDLE TRAIN"))
                {
                    if (assigned != null) { selectedTrainIndex = Simulation.Trains.IndexOf(assigned); trainSelected = true; }
                    else Simulation.Dispatch(selected, fuelDestination);
                }
                if (Button(new Rect(left, row + 39, 112, 30), selected.Paused ? "RESUME MINE" : "PAUSE MINE")) selected.Paused = !selected.Paused;
                if (Button(new Rect(left + 122, row + 39, width - 122, 30), $"UPGRADE {selected.Level * 120}", enabled: selected.Level < 3)) Simulation.UpgradeExtractor(selected);
            }
            else if (selected != null && selected.Kind == StructureKind.PowerPlant)
            {
                string state = selected.Paused ? "PAUSED" : !selected.Connected ? "WIRE SOUTH PORT" : selected.Stock == 0 && selected.BurnEnergy <= 0 ? "NEEDS FUEL DELIVERY" : selected.Generation <= 0 ? "BATTERY SATISFIED" : "GENERATING";
                Stat(left, ref row, "STATUS", state);
                Stat(left, ref row, "FUEL", $"{selected.Stock} / {selected.Storage} Fluxite");
                Stat(left, ref row, "OUTPUT", $"{selected.Generation:0.#} / {ColonySimulation.PlantOutput} power/s");
                Stat(left, ref row, "BURN LEFT", $"{selected.BurnEnergy:0.#} power");
                Stat(left, ref row, "SOUTH PORT", selected.Port.ToString());
                if (Button(new Rect(left, row + 4, width, 32), selected.Paused ? "RESUME PLANT" : "PAUSE PLANT")) selected.Paused = !selected.Paused;
                GUI.Label(new Rect(left, row + 45, width, 62), "1 Fluxite = 40 power. Unused fuel is retained. Assign this plant from a Fluxite extractor.", smallStyle);
            }
            else if (selected != null)
            {
                Stat(left, ref row, "POWER", selected.Connected ? "CONNECTED" : "WIRE SOUTH PORT");
                Stat(left, ref row, "SOUTH PORT", selected.Port.ToString());
                Stat(left, ref row, selected.Kind == StructureKind.Solar ? "GENERATION" : "DEPOT", selected.Kind == StructureKind.Solar ? selected.Connected ? "+2 power / second" : "0 / 2 power per second" : "Ore arrives here for credits");
                GUI.Label(new Rect(left, row + 14, width, 85), "Conduits and tracks are separate networks. They can share a corridor, but rails do not transmit power.", bodyStyle);
            }
            else
            {
                Stat(left, ref row, "POSITION", Simulation.RoverCell.ToString());
                Stat(left, ref row, "MOVEMENT", Simulation.RoverMoving ? "EXPLORING" : "AWAITING ORDERS");
                Stat(left, ref row, "ENERGY", "2 power / tile travelled");
                GUI.Label(new Rect(left, row + 16, width, 100), "Click ground to explore. Previously revealed terrain stays visible. At low power, pause mines and let solar recharge.", bodyStyle);
            }
            if (Button(new Rect(left, panel.yMax - 52, 112, 34), "COLONY  [C]")) CenterColony();
            if (Button(new Rect(left + 122, panel.yMax - 52, width - 122, 34), "ROVER  [V]")) CenterRover();
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
            string[] names = { "1  EXPLORE", "2  EXTRACTOR", "3  SOLAR / 100", "4  CONDUIT / 2", "5  RAIL / 3", "6  PLANT / 250" };
            for (int index = 0; index < names.Length; index++)
                if (Button(new Rect(28 + index * 143, bottom + 13, 135, 44), names[index], tool == (Tool)index && !trainSelected)) SetTool((Tool)index);
            if (Button(new Rect(890, bottom + 13, 135, 44), "FLEET", trainSelected)) { trainSelected = true; selected = null; tool = Tool.Explore; routeStart = null; }
            GUI.Label(new Rect(1040, bottom + 12, UiWidth - 1070, 50), "WASD: PAN   SCROLL: ZOOM\nSPACE: PAUSE   R: ROUTE BEND", smallStyle);
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
            else if (BuildingTool && hover.HasValue)
            {
                bool valid = Simulation.CanBuild(BuildKind, hover.Value, out _, out int size, out int cost, out string reason);
                message = valid ? $"BUILD {tool.ToString().ToUpper()}   {size} x {size} tiles   /   {cost} credits   /   Click to confirm." : reason;
            }
            GUI.Label(new Rect(28, bottom - 33, UiWidth - 60, 25), message, bodyStyle);
        }

        private void DrawWorldLabels()
        {
            foreach (var deposit in Simulation.Deposits)
            {
                if (!Simulation.FullyRevealed(deposit) || deposit.Extractor != null) continue;
                WorldLabel(Position(deposit.Origin) + new Vector3(deposit.Size - 1, 1.3f, deposit.Size - 1), $"{deposit.Resource.ToString().ToUpper()}  {deposit.Size}x{deposit.Size} / {deposit.Rate:0.##}/s", deposit.Resource == ResourceKind.Fluxite ? cyan : gold);
            }
            WorldLabel(Position(Simulation.Colony.Port, 0.2f), "COLONY PORT", cyan);
            if (selected != null) WorldLabel(Position(selected.Port, 0.2f), "CONNECT HERE", cyan);
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
