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
        public GameObject AstraBotModel;
        public GameObject ColonyModel;
        public GameObject SolarModel;
        public GameObject ExtractorModel;
        public GameObject PowerPlantModel;
        public GameObject AlienModel;
        public GameObject TurretModel;
        public GameObject OreModel;
        public GameObject DecorativeRockModel;
        public GameObject FluxiteModel;
        public GameObject TrainModel;
        public GameObject TerrainModel;
        public GameObject RampModel;
        public GameObject HillsideModel;
        public GameObject HillsideCornerModel;
        public Material SurfaceTemplate;
        public Material LaserMaterial;
        public Material TerrainSurfaceMaterial;
        [Min(0.1f)] public float TerrainTextureRepeat = 6f;
        [SerializeField] private string diagnostics;
        public ColonySimulation Simulation { get; private set; }
        private enum Tool { Explore, Extractor, Solar, Conduit, Rail, PowerPlant, Turret }
        private Tool tool;
        private Camera worldCamera;
        private Transform worldRoot;
        private Transform roverVisual;
        private readonly Dictionary<FreightTrain, Transform> trainVisuals = new Dictionary<FreightTrain, Transform>();
        private readonly Dictionary<FreightTrain, Renderer> cargoVisuals = new Dictionary<FreightTrain, Renderer>();
        private readonly Dictionary<Cell, GroundTile> ground = new Dictionary<Cell, GroundTile>();
        private readonly Dictionary<Collider, Cell> terrainColliders = new Dictionary<Collider, Cell>();
        private readonly Dictionary<Cell, GameObject> ore = new Dictionary<Cell, GameObject>();
        private readonly Dictionary<Cell, GameObject> decorativeRocks = new Dictionary<Cell, GameObject>();
        private readonly Dictionary<Structure, Transform> buildings = new Dictionary<Structure, Transform>();
        private readonly Dictionary<Structure, Animator[]> extractorAnimators = new Dictionary<Structure, Animator[]>();
        private readonly Dictionary<Material, Material> converted = new Dictionary<Material, Material>();
        private readonly List<Material> ownedMaterials = new List<Material>();
        private Transform networkRoot;
        private LineRenderer preview;
        private Material fogMaterial;
        private Material groundMaterial;
        private AstraGroundSurface groundSurface;
        private Material foundationMaterial;
        private Material powerMaterial;
        private Material darkPowerMaterial;
        private Material railMaterial;
        private Material orangeMaterial;
        private Material orePayloadMaterial;
        private Material fluxitePayloadMaterial;
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
        private Vector3 cameraVelocity;
        private bool followRover = true;
        private bool rightPanPending;
        private bool rightPanDragging;
        private Vector2 rightPanOrigin;
        private GUIStyle titleStyle;
        private GUIStyle headingStyle;
        private GUIStyle bodyStyle;
        private Rect hintPanelRect;
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
        private bool SidebarVisible => !Simulation.Paused && !pickingTile && !BuildingTool && (trainSelected || selected != null);
        private bool ConnectionSelection => SidebarVisible && !trainSelected && selected != null && selected.Kind != StructureKind.Turret && (NetworkTool || (!selected.Connected && selected.Kind != StructureKind.Extractor));
        private Rect Sidebar => SidebarVisible ? new Rect(UiWidth - 294, 88, 278,
            ConnectionSelection && NetworkTool ? 174 : ConnectionSelection || (!trainSelected && selected != null && (selected.Kind == StructureKind.Solar || selected.Kind == StructureKind.Colony)) ? 210 : selected?.Kind == StructureKind.Extractor ? 422 : 380) : Rect.zero;
        private Rect ConnectionActionRect => new Rect(Sidebar.x + 18, 252, Sidebar.width - 36, 34);
        private bool ObjectiveVisible => !Simulation.Paused && !pickingTile && !trainSelected && selected == null && !BuildingTool && !NetworkTool && !ConnectionPanelVisible;
        private Rect ObjectivePanel => ObjectiveVisible ? new Rect(16, 88, 350, 114) : Rect.zero;
        private bool ContextPanelContains(Vector2 point) => SidebarVisible && Sidebar.Contains(point) || ObjectiveVisible && ObjectivePanel.Contains(point) || ConnectionPanelVisible && LinkGuidePanel.Contains(point) || DefensePanel.Contains(point) || RepairPanel.Contains(point);
        private bool ContextPanelOverlaps(Rect rectangle) => SidebarVisible && Sidebar.Overlaps(rectangle) || ObjectiveVisible && ObjectivePanel.Overlaps(rectangle) || ConnectionPanelVisible && LinkGuidePanel.Overlaps(rectangle) || DefensePanel.Overlaps(rectangle) || RepairPanel.Overlaps(rectangle);
        private bool BuildingTool => tool == Tool.Extractor || tool == Tool.Solar || tool == Tool.PowerPlant || tool == Tool.Turret;
        private StructureKind BuildKind => tool == Tool.Turret ? StructureKind.Turret : tool == Tool.PowerPlant ? StructureKind.PowerPlant : tool == Tool.Solar ? StructureKind.Solar : StructureKind.Extractor;

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
            Application.runInBackground = true;
            powerFlowProperties = new MaterialPropertyBlock();
            Application.targetFrameRate = 60;
            worldCamera = Camera.main;
            cameraTarget = new Vector3(15, 0, 16);
            worldCamera.orthographic = true;
            worldCamera.orthographicSize = 16;
            worldCamera.transform.rotation = Quaternion.Euler(35, 20, 0);
            PositionCamera();
            fogMaterial = MakeMaterial(new Color(0.10f, 0.13f, 0.21f));
            groundMaterial = MakeMaterial(new Color(0.91f, 0.52f, 0.39f));
            foundationMaterial = MakeMaterial(new Color(0.20f, 0.25f, 0.33f));
            powerMaterial = MakeMaterial(cyan, 0.3f);
            darkPowerMaterial = MakeMaterial(new Color(0.29f, 0.41f, 0.47f));
            railMaterial = MakeMaterial(new Color(0.80f, 0.84f, 0.85f));
            orangeMaterial = MakeMaterial(new Color(0.95f, 0.52f, 0.20f));
            var oreRenderer = OreModel != null ? OreModel.GetComponentInChildren<Renderer>(true) : null;
            orePayloadMaterial = oreRenderer != null && oreRenderer.sharedMaterial != null ? oreRenderer.sharedMaterial : orangeMaterial;
            whiteMaterial = MakeMaterial(ink);
            previewMaterial = MakeMaterial(cyan, 0.25f);
            fuelMaterial = MakeMaterial(new Color(0.38f, 0.95f, 0.67f), 0.45f);
            var fluxiteRenderer = FluxiteModel != null ? FluxiteModel.GetComponentInChildren<Renderer>(true) : null;
            fluxitePayloadMaterial = fluxiteRenderer != null && fluxiteRenderer.sharedMaterial != null ? fluxiteRenderer.sharedMaterial : fuelMaterial;
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
            ResetNetworkPlacement();
            ResetBotControl();
            ResetCoach();
            ResetLinkGuide();
            if (worldRoot != null) Destroy(worldRoot.gameObject);
            worldRoot = new GameObject("Colony world").transform;
            ground.Clear(); terrainColliders.Clear(); ore.Clear(); decorativeRocks.Clear(); buildings.Clear(); trainVisuals.Clear(); cargoVisuals.Clear();
            extractorAnimators.Clear();
            ResetDefenseVisuals();
            selected = null; trainSelected = false; routeStart = null; tool = Tool.Explore;
            fuelDestination = null; selectedTrainIndex = 0; confirmRestart = false;
            Simulation = new ColonySimulation();
            previousDeliveries = 0;
            revision = -1; revealRevision = -1;
            var surfaceObject = new GameObject("Continuous colony ground");
            surfaceObject.transform.SetParent(worldRoot, false);
            groundSurface = surfaceObject.AddComponent<AstraGroundSurface>();
            var surfaceMaterial = TerrainSurfaceMaterial != null ? TerrainSurfaceMaterial : Resources.Load<Material>("Terrain/NuclearKnights/Grid Desert");
            groundSurface.Initialize(Simulation.Terrain, surfaceMaterial != null ? surfaceMaterial : groundMaterial, fogMaterial, TerrainTextureRepeat);
            for (int column = 0; column < ColonySimulation.Width; column++)
                for (int row = 0; row < ColonySimulation.Height; row++)
                {
                    var cell = new Cell(column, row);
                    if (Simulation.Terrain.Kind(cell) == TerrainKind.Flat) continue;
                    var tile = CreateTerrain(cell);
                    ground[cell] = new GroundTile(tile, fogMaterial);
                }
            foreach (var deposit in Simulation.Deposits)
                foreach (var cell in ColonySimulation.Footprint(deposit.Origin, deposit.Size))
                {
                    bool fluxite = deposit.Resource == ResourceKind.Fluxite;
                    bool crystal = fluxite && FluxiteModel != null;
                    var cluster = Model(crystal ? FluxiteModel : OreModel, fluxite ? "Fluxite" : "Ore", worldRoot, Position(cell, 0), 1.55f, crystal ? 1.4f : 0.8f, crystal || !fluxite, fitToBounds: fluxite);
                    cluster.transform.Rotate(0, (cell.X * 37 + cell.Y * 19) % 360, 0);
                    if (fluxite && !crystal)
                        foreach (var renderer in cluster.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = fuelMaterial;
                    ore[cell] = cluster;
                }
            if (DecorativeRockModel != null)
            {
                var random = new System.Random(84213);
                foreach (var cell in Simulation.DecorativeRockCells())
                {
                    float width = 0.65f + (float)random.NextDouble() * 0.45f;
                    var rock = Model(DecorativeRockModel, "Decorative rock " + cell, worldRoot, Position(cell, 0), width, width * 0.65f, preserveMaterials: true, fitToBounds: false);
                    rock.transform.localScale *= width;
                    rock.transform.Rotate(0, (float)random.NextDouble() * 360, 0);
                    rock.SetActive(false);
                    decorativeRocks[cell] = rock;
                }
            }
            roverVisual = Model(RoverModel, "Rover 01", worldRoot, Position(7, 6, 0.08f), 1.7f, 1.1f, true).transform;
            var previewObject = new GameObject("Placement preview");
            previewObject.transform.SetParent(worldRoot);
            preview = previewObject.AddComponent<LineRenderer>();
            preview.sharedMaterial = previewMaterial;
            preview.widthMultiplier = 0.09f;
            preview.positionCount = 0;
            preview.shadowCastingMode = ShadowCastingMode.Off;
            InitializeFogVisuals();
            SyncWorld();
            CenterRover();
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

        private GameObject Model(GameObject prefab, string objectName, Transform parent, Vector3 position, float width, float height, bool preserveMaterials = false, bool fitToBounds = true)
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
            float scale = fitToBounds ? Mathf.Min(width / Mathf.Max(bounds.size.x, bounds.size.z, 0.01f), height / Mathf.Max(bounds.size.y, 0.01f)) : 1f;
            Vector3 center = bounds.center - holder.transform.position;
            model.transform.localScale *= scale;
            model.transform.localPosition = new Vector3(-center.x * scale, -(bounds.min.y - holder.transform.position.y) * scale, -center.z * scale);
            foreach (var renderer in renderers)
            {
                if (preserveMaterials) continue;
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

        private GameObject CreateTerrain(Cell cell)
        {
            TerrainKind kind = Simulation.Terrain.Kind(cell);
            var prefab = Simulation.Terrain.IsCorner(cell) ? HillsideCornerModel : kind == TerrainKind.Ramp ? RampModel : HillsideModel;
            float baseHeight = Simulation.Terrain.Elevation(cell) * TerrainGrid.LevelHeight;
            var tile = Model(prefab, kind + " terrain " + cell, worldRoot, new Vector3(cell.X * 2, baseHeight - 0.02f, cell.Y * 2), 2, TerrainGrid.LevelHeight, preserveMaterials: true);
            var renderers = tile.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            tile.transform.localScale = new Vector3(TerrainGrid.CellSize / Mathf.Max(bounds.size.x, 0.01f), TerrainGrid.LevelHeight / Mathf.Max(bounds.size.y, 0.01f), TerrainGrid.CellSize / Mathf.Max(bounds.size.z, 0.01f));
            Cell uphill = Simulation.Terrain.Uphill(cell);
            float yaw = Simulation.Terrain.IsCorner(cell) ? Simulation.Terrain.CornerYaw(cell) : Mathf.Atan2(-uphill.X, -uphill.Y) * Mathf.Rad2Deg;
            tile.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            foreach (var filter in tile.GetComponentsInChildren<MeshFilter>())
            {
                var collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                terrainColliders[collider] = cell;
            }
            return tile;
        }

        private Vector3 Position(Cell cell, float height = 0) => Position(cell.X, cell.Y, height);
        private Vector3 Position(float column, float row, float height = 0) => new Vector3(column * 2, Simulation.Terrain.HeightAt(column, row) + height, row * 2);

        private Quaternion GroundRotation(float column, float row, Vector3 forward)
        {
            var cell = new Cell(Mathf.FloorToInt(column + 0.5f), Mathf.FloorToInt(row + 0.5f));
            Cell uphill = Simulation.Terrain.Uphill(cell);
            float rise = TerrainGrid.LevelHeight / TerrainGrid.CellSize;
            Vector3 normal = Simulation.Terrain.Kind(cell) == TerrainKind.Ramp ? new Vector3(-uphill.X * rise, 1, -uphill.Y * rise).normalized : Vector3.up;
            return Quaternion.LookRotation(Vector3.ProjectOnPlane(forward, normal).normalized, normal);
        }

        private void Update()
        {
            if (Simulation == null) return;
            HandleInput();
            Simulation.Step(Time.deltaTime);
            SyncWorld();
            UpdateExtractorAnimations();
            UpdateFogVisuals();
            UpdatePowerVisuals();
            UpdateDefenseVisuals();
            UpdateLinkGuide();
            UpdateNetworkPlacement();
            MoveVisual(roverVisual, Position(Simulation.RoverX, Simulation.RoverY, 0.08f));
            foreach (var train in Simulation.Trains)
            {
                MoveVisual(trainVisuals[train], Position(train.X, train.Y, 0.24f));
                cargoVisuals[train].gameObject.SetActive(train.Cargo > 0);
                cargoVisuals[train].sharedMaterial = train.Resource == ResourceKind.Fluxite ? fluxitePayloadMaterial : orePayloadMaterial;
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
            PublishCoach();
        }

        private void LateUpdate()
        {
            if (Simulation == null) return;
            if (followRover)
            {
                cameraTarget = Vector3.SmoothDamp(cameraTarget, Position(Simulation.RoverX, Simulation.RoverY), ref cameraVelocity, 0.22f, Mathf.Infinity, Time.unscaledDeltaTime);
                PositionCamera();
            }
            // Screen-docked presentation must run after the camera reaches its final
            // pose for this frame. Updating before camera follow made the drone chase
            // the UI berth by one frame and look like it was colliding with the dock.
            UpdateAstraBotVisual();
        }

        private void MoveVisual(Transform visual, Vector3 position)
        {
            Vector3 direction = position - visual.position;
            direction.y = 0;
            if (direction.sqrMagnitude < 0.000001f) direction = Vector3.ProjectOnPlane(visual.forward, Vector3.up);
            visual.rotation = Quaternion.Slerp(visual.rotation, GroundRotation(position.x / 2, position.z / 2, direction), Time.deltaTime * 12);
            visual.position = position;
        }

        private void SyncWorld()
        {
            foreach (var pair in decorativeRocks)
                pair.Value.SetActive(Simulation.IsRevealed(pair.Key) && Simulation.StructureAt(pair.Key) == null
                    && !Simulation.Rails.Contains(pair.Key) && !Simulation.Conduits.Contains(pair.Key));
            if (Simulation.RevealRevision != revealRevision)
            {
                revealRevision = Simulation.RevealRevision;
                groundSurface.SetRevealed(Simulation);
                foreach (var pair in ground) pair.Value.SetRevealed(Simulation.IsRevealed(pair.Key));
                foreach (var pair in ore) pair.Value.SetActive(Simulation.IsRevealed(pair.Key));
                SyncFogVisuals();
            }
            if (Simulation.Revision == revision) return;
            revision = Simulation.Revision;
            foreach (var train in Simulation.Trains)
            {
                if (trainVisuals.ContainsKey(train)) continue;
                var visual = Model(TrainModel, "Astra locomotive " + (Simulation.Trains.IndexOf(train) + 1), worldRoot, Position(train.X, train.Y, 0.24f), 1.7f, 0.8f).transform;
                trainVisuals[train] = visual;
                cargoVisuals[train] = Box("Freight payload", visual, new Vector3(0, 0.7f, -0.2f), new Vector3(0.45f, 0.3f, 0.55f), orePayloadMaterial).GetComponent<Renderer>();
            }
            foreach (var structure in Simulation.Structures)
            {
                if (buildings.ContainsKey(structure)) continue;
                Vector3 center = Position(structure.Origin) + new Vector3(structure.Size - 1, 0, structure.Size - 1);
                var root = new GameObject(structure.Kind + " " + structure.Origin).transform;
                root.SetParent(worldRoot);
                root.position = center;
                bool extractor = structure.Kind == StructureKind.Extractor;
                bool powerPlant = structure.Kind == StructureKind.PowerPlant;
                if (!extractor)
                    Box("Foundation", root, new Vector3(0, 0.04f, 0), new Vector3(structure.Size * 1.93f, 0.2f, structure.Size * 1.93f), foundationMaterial);
                GameObject prefab = structure.Kind == StructureKind.Turret ? TurretModel : structure.Kind == StructureKind.Colony ? ColonyModel : structure.Kind == StructureKind.Solar ? SolarModel : structure.Kind == StructureKind.PowerPlant ? PowerPlantModel : ExtractorModel;
                var model = Model(prefab, structure.Kind.ToString(), root, new Vector3(0, extractor ? 0 : 0.15f, 0), structure.Size * 1.8f, extractor ? structure.Size * 2.2f : structure.Kind == StructureKind.Colony || powerPlant ? 2.6f : 1.5f, extractor || structure.Kind == StructureKind.Colony || powerPlant);
                if (extractor)
                {
                    var animators = model.GetComponentsInChildren<Animator>(true);
                    foreach (var animator in animators) animator.speed = 0;
                    extractorAnimators[structure] = animators;
                }
                buildings[structure] = root;
            }
            if (networkRoot != null) Destroy(networkRoot.gameObject);
            networkRoot = new GameObject("Rail and power networks").transform;
            networkRoot.SetParent(worldRoot);
            foreach (var cell in Simulation.Rails)
            {
                var bed = Box("Track bed", networkRoot, Position(cell, 0.04f), new Vector3(1.15f, 0.15f, 1.15f), foundationMaterial);
                var node = Box("Rail node", networkRoot, Position(cell, 0.18f), new Vector3(0.6f, 0.13f, 0.6f), railMaterial);
                bed.transform.rotation = node.transform.rotation = GroundRotation(cell.X, cell.Y, Vector3.forward);
                foreach (var direction in ColonySimulation.Directions)
                {
                    var adjacent = cell + direction;
                    if (!Simulation.Rails.Contains(adjacent) || direction.X + direction.Y < 0 || !Simulation.Terrain.CanTraverse(cell, adjacent)) continue;
                    if (direction.X != 0)
                    {
                        SurfaceConnection("Rail", cell, adjacent, new Vector3(0, 0.17f, -0.3f), 0.12f, railMaterial);
                        SurfaceConnection("Rail", cell, adjacent, new Vector3(0, 0.17f, 0.3f), 0.12f, railMaterial);
                    }
                    else
                    {
                        SurfaceConnection("Rail", cell, adjacent, new Vector3(-0.3f, 0.17f, 0), 0.12f, railMaterial);
                        SurfaceConnection("Rail", cell, adjacent, new Vector3(0.3f, 0.17f, 0), 0.12f, railMaterial);
                    }
                }
            }
            DrawPowerNetwork();
        }

        private void UpdateExtractorAnimations()
        {
            foreach (var pair in extractorAnimators)
            {
                var structure = pair.Key;
                float speed = !Simulation.Paused && structure.Connected && !structure.Paused && structure.Stock < structure.Storage
                    ? Mathf.Clamp01(structure.SuppliedFraction) : 0;
                foreach (var animator in pair.Value) animator.speed = speed;
            }
        }

        private void SurfaceConnection(string objectName, Cell from, Cell to, Vector3 offset, float thickness, Material material)
        {
            var points = new List<Vector3> { Position(from.X + offset.x / 2, from.Y + offset.z / 2, offset.y) };
            float start = from.X + offset.x / 2;
            float end = to.X + offset.x / 2;
            if (from.X != to.X)
                for (float boundary = Mathf.Floor(start + 0.5f) + 0.5f; boundary < end; boundary++)
                    points.Add(Position(boundary, from.Y + offset.z / 2, offset.y));
            points.Add(Position(to.X + offset.x / 2, to.Y + offset.z / 2, offset.y));
            for (int index = 1; index < points.Count; index++)
            {
                Vector3 direction = points[index] - points[index - 1];
                var segment = Box(objectName, networkRoot, (points[index] + points[index - 1]) * 0.5f, new Vector3(thickness, thickness, direction.magnitude), material);
                segment.transform.rotation = Quaternion.LookRotation(direction);
            }
        }

        private bool OverUi(Vector2 screen)
        {
            Vector2 point = new Vector2(screen.x / UiScale, (Screen.height - screen.y) / UiScale);
            return OverNetworkPlacement(point) || OverLinkGuide(point) || hintPanelRect.Contains(point) || point.y < 72 || point.y > UiHeight - 128 || ContextPanelContains(point);
        }

        private void HandleInput()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && (botBusy || pickingTile)) { CoachBotStop("Escape"); return; }
            if (coachInputBlocked || Time.frameCount <= coachInputResumeFrame)
            {
                rightPanPending = rightPanDragging = false;
                return;
            }
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            if (mouse == null) return;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame) { HideLinkGuide(); routeStart = null; tool = Tool.Explore; confirmRestart = false; }
                if (keyboard.spaceKey.wasPressedThisFrame) Simulation.Paused = !Simulation.Paused;
                if (keyboard.digit1Key.wasPressedThisFrame) SetTool(Tool.Explore);
                if (keyboard.digit2Key.wasPressedThisFrame) SetTool(Tool.Extractor);
                if (keyboard.digit3Key.wasPressedThisFrame) SetTool(Tool.Solar);
                if (keyboard.digit4Key.wasPressedThisFrame) SetTool(Tool.Conduit);
                if (keyboard.digit5Key.wasPressedThisFrame) SetTool(Tool.Rail);
                if (keyboard.digit6Key.wasPressedThisFrame) SetTool(Tool.PowerPlant);
                if (keyboard.digit7Key.wasPressedThisFrame) SetTool(Tool.Turret);
                if (keyboard.rKey.wasPressedThisFrame) verticalFirst = !verticalFirst;
                if (keyboard.cKey.wasPressedThisFrame) CenterColony();
                if (keyboard.vKey.wasPressedThisFrame) CenterRover();
                Vector3 pan = Vector3.zero;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) pan.x--;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) pan.x++;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) pan.z++;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) pan.z--;
                if (pan.sqrMagnitude > 0) StopFollowingRover();
                cameraTarget += pan * (Time.unscaledDeltaTime * worldCamera.orthographicSize);
            }
            Vector2 screen = mouse.position.ReadValue();
            if (mouse.rightButton.wasPressedThisFrame)
            {
                rightPanPending = !OverUi(screen);
                rightPanDragging = false;
                rightPanOrigin = screen;
            }
            if (rightPanPending && mouse.rightButton.isPressed && (screen - rightPanOrigin).sqrMagnitude >= 36 * UiScale * UiScale)
                rightPanDragging = true;
            bool rightPanning = rightPanPending && rightPanDragging && mouse.rightButton.isPressed;
            if (mouse.middleButton.isPressed || rightPanning)
            {
                Vector2 delta = mouse.delta.ReadValue();
                if (delta.sqrMagnitude > 0) StopFollowingRover();
                Vector3 right = worldCamera.transform.right;
                Vector3 forward = Vector3.ProjectOnPlane(worldCamera.transform.up, Vector3.up).normalized;
                cameraTarget -= (right * delta.x + forward * delta.y) * (worldCamera.orthographicSize * 2 / Screen.height);
            }
            if (!OverUi(screen)) worldCamera.orthographicSize = Mathf.Clamp(worldCamera.orthographicSize - mouse.scroll.ReadValue().y * 0.018f, 8, 31);
            cameraTarget.x = Mathf.Clamp(cameraTarget.x, 0, (ColonySimulation.Width - 1) * 2);
            cameraTarget.z = Mathf.Clamp(cameraTarget.z, 0, (ColonySimulation.Height - 1) * 2);
            PositionCamera();
            hover = null;
            if (mouse.rightButton.wasReleasedThisFrame)
            {
                if (rightPanPending && !rightPanDragging && !OverUi(screen)) { routeStart = null; tool = Tool.Explore; }
                rightPanPending = rightPanDragging = false;
                return;
            }
            if (!mouse.rightButton.isPressed) rightPanPending = rightPanDragging = false;
            if (rightPanPending) return;
            if (OverUi(screen)) return;
            var ray = worldCamera.ScreenPointToRay(screen);
            foreach (var hit in Physics.RaycastAll(ray, 200).OrderBy(hit => hit.distance))
            {
                if (!groundSurface.TryGetCell(hit, out var cell) && !terrainColliders.TryGetValue(hit.collider, out cell)) continue;
                hover = cell;
                break;
            }
            if (pickingTile)
            {
                if (mouse.leftButton.wasPressedThisFrame && hover.HasValue) { pickedTile = hover; pickingTile = false; coachTimer = 1; }
                return;
            }
            if (!mouse.leftButton.wasPressedThisFrame || !hover.HasValue || Simulation.Paused) return;
            Cell target = hover.Value;
            if (tool == Tool.Explore)
            {
                selected = Simulation.IsRevealed(target) ? Simulation.StructureAt(target) : null;
                trainSelected = false;
                if (selected == null)
                {
                    YieldBotRoverToPlayer();
                    if (Simulation.OrderRover(target)) followRover = true;
                }
                else StopFollowingRover();
            }
            else if (BuildingTool)
            {
                if (Simulation.Build(BuildKind, target))
                {
                    selected = Simulation.StructureAt(target);
                    tool = Tool.Explore;
                    BeginBuildingConnection(selected);
                }
            }
            else PlaceNetworkAt(target);
        }

        private void PlaceNetworkAt(Cell target)
        {
            if (Simulation.Paused || (tool != Tool.Conduit && tool != Tool.Rail)) return;
            target = NetworkEndpoint(target);
            if (routeStart.HasValue && routeStart.Value.Equals(target)) { Simulation.Message = "Start selected. Click a different highlighted tile to connect."; return; }
            if (!routeStart.HasValue)
            {
                if (Simulation.CanLay(new[] { target }, tool == Tool.Rail, out _, out string reason)) { routeStart = target; Simulation.Message = "Start selected. Choose a highlighted destination; the bend adjusts around obstacles."; }
                else Simulation.Message = reason;
            }
            else
            {
                if (Simulation.TryPlanNetworkRoute(routeStart.Value, target, tool == Tool.Rail, verticalFirst, out var path, out _, out string reason))
                {
                    if (Simulation.Lay(path, tool == Tool.Rail)) routeStart = null;
                }
                else Simulation.Message = reason;
            }
        }

        private void SetTool(Tool next)
        {
            if (next == Tool.Conduit || next == Tool.Rail) linkSuppressed = false;
            tool = next; routeStart = null; trainSelected = false;
            if (next != Tool.Explore) StopFollowingRover();
        }
        private void StopFollowingRover() { followRover = false; cameraVelocity = Vector3.zero; }
        private void FollowBotRoverMove(Cell destination)
        {
            // Keep player framing when the complete move already fits. If AstraBot
            // sends the shared rover out of view, smoothly follow it until the player
            // pans again instead of letting the action disappear off-screen.
            Vector3 roverView = worldCamera.WorldToViewportPoint(Position(Simulation.RoverX, Simulation.RoverY));
            Vector3 destinationView = worldCamera.WorldToViewportPoint(Position(destination));
            bool Fits(Vector3 point) => point.z > 0 && point.x >= 0.08f && point.x <= 0.92f && point.y >= 0.22f && point.y <= 0.9f;
            if (Fits(roverView) && Fits(destinationView)) return;
            followRover = true;
            cameraVelocity = Vector3.zero;
        }
        private void OnApplicationFocus(bool focused)
        {
            if (!focused) rightPanPending = rightPanDragging = false;
        }

        private void PositionCamera() => worldCamera.transform.position = cameraTarget - worldCamera.transform.forward * 48;
        private void CenterColony() { StopFollowingRover(); cameraTarget = Position(7, 8); PositionCamera(); }
        private void CenterRover() { followRover = true; cameraVelocity = Vector3.zero; cameraTarget = Position(Simulation.RoverX, Simulation.RoverY); PositionCamera(); }

        private void UpdatePreview()
        {
            preview.positionCount = 0;
            if (!hover.HasValue || Simulation.Paused) return;
            Cell origin = NetworkTool ? NetworkEndpoint(hover.Value) : hover.Value;
            int size = 1;
            bool valid = Simulation.IsRevealed(origin);
            if (BuildingTool)
                valid = Simulation.CanBuild(BuildKind, origin, out origin, out size, out _, out _);
            if (NetworkTool && !routeStart.HasValue)
                valid = Simulation.CanLay(new[] { origin }, tool == Tool.Rail, out _, out _);
            if (routeStart.HasValue)
            {
                valid = Simulation.TryPlanNetworkRoute(routeStart.Value, origin, tool == Tool.Rail, verticalFirst, out var path, out _, out _);
                if (!valid) path = ColonySimulation.Corridor(routeStart.Value, origin, verticalFirst);
                var points = new List<Vector3>();
                for (int index = 0; index < path.Count; index++)
                {
                    if (index > 0) points.Add(Position((path[index - 1].X + path[index].X) * 0.5f, (path[index - 1].Y + path[index].Y) * 0.5f, 0.35f));
                    points.Add(Position(path[index], 0.35f));
                }
                preview.positionCount = points.Count;
                preview.SetPositions(points.ToArray());
            }
            else
            {
                Vector3 corner = Position(origin, 0.3f) - new Vector3(0.93f, 0, 0.93f);
                float extent = size * 2 - 0.14f;
                preview.positionCount = 5;
                var corners = new[] { corner, corner + Vector3.right * extent, corner + new Vector3(extent, 0, extent), corner + Vector3.forward * extent, corner };
                preview.SetPositions(corners.Select(point => Position(point.x / 2, point.z / 2, 0.3f)).ToArray());
            }
            previewMaterial.SetColor("_BaseColor", valid ? (tool == Tool.Rail ? gold : cyan) : new Color(1, 0.35f, 0.38f));
        }

        private void OnGUI()
        {
            if (Simulation == null) return;
            if ((pickingTile || coachInputBlocked || Time.frameCount <= coachInputResumeFrame) && (Event.current.isMouse || Event.current.isKey || Event.current.type == EventType.ScrollWheel)) Event.current.Use();
            Styles();
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * UiScale);
            DrawWorldLabels();
            Panel(new Rect(0, 0, UiWidth, 72));
            GUI.Label(new Rect(22, 10, 300, 32), "ASTRA EXPRESS", titleStyle);
            GUI.Label(new Rect(365, 12, 190, 27), $"{Simulation.Credits:N0} credits", headingStyle);
            if (Simulation.Deliveries > 0) GUI.Label(new Rect(365, 42, 200, 20), $"{Simulation.Sold} ore delivered  /  {Simulation.Deliveries} trips", smallStyle);
            GUI.Label(new Rect(585, 10, 280, 26), $"SHARED BATTERY   {Simulation.Battery:0} / 100", bodyStyle);
            Fill(new Rect(585, 39, 175, 6), new Color(0.17f, 0.23f, 0.30f));
            Fill(new Rect(585, 39, 175 * Simulation.Battery / 100, 6), Simulation.Battery < 12 ? gold : cyan);
            GUI.Label(new Rect(585, 49, 460, 20), $"SOLAR +{Simulation.SolarGeneration:0}/s   FUEL +{Simulation.AverageFuelGeneration:0.#}/s   MINES -{Simulation.Demand:0.#}/s", smallStyle);
            if (Button(new Rect(UiWidth - 214, 17, 92, 36), Simulation.Paused ? "Resume" : "Pause", Simulation.Paused)) Simulation.Paused = !Simulation.Paused;
            if (Button(new Rect(UiWidth - 112, 17, 92, 36), confirmRestart ? "Confirm?" : "Restart"))
            {
                if (confirmRestart) { ResetWorld(); return; }
                confirmRestart = true;
                Simulation.Message = "Restart deletes this colony. Click CONFIRM? again to start over; Escape cancels.";
            }
            DrawObjective();
            DrawSelection();
            DrawDefense();
            DrawToolbar();
            DrawLinkGuide();
            DrawBotTarget();
            DrawNetworkPlacement();
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
            if (!ObjectiveVisible) return;
            bool discovered = Simulation.Deposits.Any(deposit => deposit.Resource == ResourceKind.Ore && Simulation.FullyRevealed(deposit));
            bool built = Simulation.Structures.Any(structure => structure.Kind == StructureKind.Extractor && structure.Deposit.Resource == ResourceKind.Ore);
            bool powered = Simulation.Structures.Any(structure => structure.Kind == StructureKind.Extractor && structure.Deposit.Resource == ResourceKind.Ore && structure.Connected);
            bool routed = Simulation.Trains.Any(train => train.Source != null);
            bool fuelFound = Simulation.Deposits.Any(deposit => deposit.Resource == ResourceKind.Fluxite && Simulation.FullyRevealed(deposit));
            int stage = Simulation.FuelConsumed > 0 ? 8 : Simulation.FuelDelivered > 0 ? 7 : Simulation.Deliveries > 0 ? fuelFound ? 6 : 5 : routed ? 4 : powered ? 3 : built ? 2 : discovered ? 1 : 0;
            string[] titles = { "Explore the frontier", "Build your first extractor", "Bring the mine online", "Connect a paying railway", "Your first delivery", "Find a stronger power source", "Build the fuel supply line", "Light the reactor", "An industrial frontier" };
            string[] descriptions = {
                "Click ground east of the colony. Your rover reveals ore hidden by the fog.",
                "Choose Extractor, then click the revealed ore patch. Keep its south port clear.",
                "Choose Conduit. Click the colony's cyan port, then the extractor's south port.",
                "Lay rails between those same ports. Its free train dispatches automatically when the route is complete.",
                "The train collects local ore and sells it at the colony. Only deliveries earn credits.",
                "Keep ore deliveries running. Explore for green Fluxite to fuel a power plant.",
                "Select a Fluxite patch to start a fuel supply line for a power plant.",
                "Connect the plant's conduit port and resume it. Fuel burns only when the battery needs power.",
                "Fuel now powers expansion! Find larger ore patches, upgrade mines, and keep fuel arriving." };
            Rect panel = ObjectivePanel;
            Panel(panel);
            GUI.Label(new Rect(panel.x + 14, panel.y + 10, panel.width - 28, 26), titles[stage], headingStyle);
            GUI.Label(new Rect(panel.x + 14, panel.y + 39, panel.width - 28, 43), descriptions[stage], bodyStyle);
            GUI.Label(new Rect(panel.x + 14, panel.y + 87, panel.width - 28, 18), Simulation.RoverMoving ? "ROVER EXPLORING  ·  V to follow" : "ROVER READY", smallStyle);
        }

        private void DrawSelection()
        {
            if (!SidebarVisible) return;
            Rect panel = Sidebar;
            Panel(panel);
            float left = panel.x + 18;
            float width = panel.width - 36;
            GUI.Label(new Rect(left, 103, width, 22), trainSelected ? "RAIL OPERATIONS" : ConnectionSelection ? "CONNECTION STATUS" : "SELECTED BUILDING", smallStyle);
            string title = trainSelected ? $"Locomotive {selectedTrainIndex + 1}" : selected == null ? "Rover 01" : selected.Kind == StructureKind.Turret ? "Laser turret" : selected.Kind == StructureKind.Colony ? "Landing colony" : selected.Kind == StructureKind.Solar ? "Solar array" : selected.Kind == StructureKind.PowerPlant ? "Fluxite power plant" : selected.Deposit.Resource == ResourceKind.Fluxite ? "Fluxite extractor" : "Ore extractor";
            titleStyle.fontSize = title.Length > 17 ? 21 : 25;
            GUI.Label(new Rect(left, 132, width, 32), title, titleStyle);
            titleStyle.fontSize = 25;
            float row = 180;
            if (selected?.Kind == StructureKind.Turret) { DrawTurretSelection(left, width, row); return; }
            if (ConnectionSelection)
            {
                bool rail = selected.Kind != StructureKind.Solar && Simulation.RailRoute(selected) != null;
                Stat(left, ref row, "POWER", selected.Connected ? "Connected" : "Not connected");
                Stat(left, ref row, selected.Kind == StructureKind.Solar ? "OUTPUT" : "RAILS", selected.Kind == StructureKind.Solar ? selected.Connected ? "+2 power / second" : "Waiting for power link" : rail ? "Connected to depot" : "Not connected");
                if (!NetworkTool && Button(ConnectionActionRect, !selected.Connected ? "Show power connection" : "Show rail connection", active: true))
                    CoachGuideLink($"{(!selected.Connected ? "Conduit" : "Rail")},{selected.Origin.X},{selected.Origin.Y}");
                return;
            }
            if (selected != null && selected.Kind == StructureKind.Extractor)
            {
                string status = selected.Disabled ? "Disabled - repair" : Simulation.Paused ? "Colony paused" : selected.Paused ? "Mine paused" : !selected.Connected ? "Needs power" : selected.Stock >= selected.Storage ? "Storage full" : selected.SuppliedFraction < 0.99f ? "Low power" : "Mining";
                Stat(left, ref row, "STATUS", status);
                Stat(left, ref row, "STORAGE", $"{selected.Stock} / {selected.Storage} {selected.Deposit.Resource}");
                Stat(left, ref row, "OUTPUT", $"{selected.Rate:0.##}/s / {selected.Demand:0.#} power/s");
                var ownedTrain = Simulation.TrainFor(selected);
                var assigned = ownedTrain?.Source == selected ? ownedTrain : null;
                bool idle = ownedTrain?.Phase == TrainPhase.Parked;
                bool fuel = selected.Deposit.Resource == ResourceKind.Fluxite;
                var plants = Simulation.Structures.Where(structure => structure.Kind == StructureKind.PowerPlant).ToList();
                if (fuel && (fuelDestination == null || !plants.Contains(fuelDestination))) fuelDestination = plants.FirstOrDefault();
                var destination = fuel ? assigned?.Destination ?? fuelDestination : Simulation.Colony;
                bool sourceRail = Simulation.RailRoute(selected) != null;
                bool destinationRail = destination != null && Simulation.RailRoute(destination) != null;
                bool destinationPower = destination != null && destination.Connected;
                bool parking = ownedTrain != null && ownedTrain.ParkRequested;
                Readiness(new Rect(left, row + 3, width, 40), selected.Connected && destinationPower, sourceRail && destinationRail, assigned != null || !idle, assigned != null, parking);
                if (fuel)
                {
                    string label = destination == null ? "Build a power plant first" : "To plant " + destination.Origin + (assigned == null && plants.Count > 1 ? " >" : "");
                    if (Button(new Rect(left, row + 48, width, 26), label, enabled: assigned == null && plants.Count > 1)) fuelDestination = plants[(plants.IndexOf(fuelDestination) + 1) % plants.Count];
                }
                else GUI.Label(new Rect(left, row + 51, width, 20), "Destination: colony / 8 credits per ore", interfaceSmall);
                string action = Simulation.Paused ? "Resume colony" : !selected.Connected ? "Show power connection" : !sourceRail ? "Show rail connection" :
                    destination == null ? "Build power plant [6]" : !destinationPower ? "Connect plant power" : !destinationRail ? "Connect plant rails" :
                    assigned != null ? parking ? "Returning to colony..." : "Park train at colony" : "Restart train";
                if (Button(new Rect(left, row + 80, width, 36), action, active: true, enabled: !parking))
                {
                    if (Simulation.Paused) Simulation.Paused = false;
                    else if (!selected.Connected) CoachGuideLink($"Conduit,{selected.Origin.X},{selected.Origin.Y}");
                    else if (!sourceRail) CoachGuideLink($"Rail,{selected.Origin.X},{selected.Origin.Y}");
                    else if (destination == null) SetTool(Tool.PowerPlant);
                    else if (!destinationPower) CoachGuideLink($"Conduit,{destination.Origin.X},{destination.Origin.Y}");
                    else if (!destinationRail) CoachGuideLink($"Rail,{destination.Origin.X},{destination.Origin.Y}");
                    else if (assigned != null) Simulation.ParkTrain(ownedTrain);
                    else if (idle) Simulation.Dispatch(selected, destination);
                }
                int upgradeCost = selected.Level * 120;
                bool maximum = selected.Level >= 3;
                string hint = !maximum && Simulation.Credits < upgradeCost ? $"Upgrade needs {upgradeCost - Simulation.Credits} more credits." : ownedTrain != null ? $"{TrainStatus(ownedTrain)} · {ownedTrain.Cargo}/{ownedTrain.Capacity} cargo" : "Train unavailable.";
                GUI.Label(new Rect(left, row + 118, width, 18), hint, interfaceSmall);
                if (Button(new Rect(left, row + 140, 112, 34), selected.Paused ? "Resume mine" : "Pause mine")) selected.Paused = !selected.Paused;
                if (Button(new Rect(left + 122, row + 140, width - 122, 34), maximum ? "Max level" : $"Upgrade\n{upgradeCost} cr", enabled: !maximum && Simulation.Credits >= upgradeCost)) Simulation.UpgradeExtractor(selected);
                if (ownedTrain != null)
                {
                    int trainCost = ownedTrain.CapacityLevel * 100;
                    bool trainMaximum = ownedTrain.CapacityLevel >= ColonySimulation.MaxTrainCapacityLevel;
                    if (Button(new Rect(left, row + 181, width, 34), trainMaximum ? $"Train capacity {ownedTrain.Capacity} / maximum" : $"Train capacity {ownedTrain.Capacity} to {ownedTrain.Capacity + 4} / {trainCost} cr", enabled: !trainMaximum && Simulation.Credits >= trainCost)) Simulation.UpgradeTrain(ownedTrain);
                }
            }
            else if (selected != null && selected.Kind == StructureKind.PowerPlant)
            {
                string state = selected.Disabled ? "Disabled - repair" : Simulation.Paused ? "Colony paused" : selected.Paused ? "Plant paused" : !selected.Connected ? "Needs power link" : selected.Stock == 0 && selected.BurnEnergy <= 0 ? "Needs fuel" : selected.AverageGeneration <= 0 ? "Battery satisfied" : "Generating";
                Stat(left, ref row, "STATUS", state);
                Stat(left, ref row, "FUEL", $"{selected.Stock} / {selected.Storage} Fluxite");
                Stat(left, ref row, "AVG OUTPUT", $"{selected.AverageGeneration:0.#} / {ColonySimulation.PlantOutput} power/s");
                Stat(left, ref row, "BURN LEFT", $"{selected.BurnEnergy:0.#} power");
                bool rail = Simulation.RailRoute(selected) != null;
                if (!selected.Connected || !rail)
                {
                    if (Button(new Rect(left, row + 4, width, 32), !selected.Connected ? "Show power connection" : "Show rail connection"))
                        CoachGuideLink($"{(!selected.Connected ? "Conduit" : "Rail")},{selected.Origin.X},{selected.Origin.Y}");
                    row += 39;
                }
                if (Button(new Rect(left, row + 4, width, 32), selected.Paused ? "Resume plant" : "Pause plant")) selected.Paused = !selected.Paused;
                GUI.Label(new Rect(left, row + 45, width, 53), $"1 Fluxite = {ColonySimulation.FuelEnergy} power. Assign this plant from a Fluxite extractor. Unused fuel is retained.", smallStyle);
            }
            else if (selected != null)
            {
                Stat(left, ref row, "POWER", selected.Disabled ? "DISABLED - REPAIR" : selected.Connected ? "CONNECTED" : "WIRE SOUTH PORT");
                Stat(left, ref row, "SOUTH PORT", selected.Port.ToString());
                Stat(left, ref row, selected.Kind == StructureKind.Solar ? "GENERATION" : "DEPOT", selected.Kind == StructureKind.Solar ? selected.Connected && !selected.Disabled ? "+2 power / second" : "0 / 2 power per second" : selected.Disabled ? "Repair to unload ore" : "Ore arrives here for credits");
            }
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
            string[] names = { "Explore", "Extractor", "Solar", "Conduit", "Rail", "Plant", "Turret" };
            string[] subtitles = { "Reveal terrain", "From 150 cr", "100 cr / +2 power", "2 cr per tile", "3 cr per tile", "250 cr / Fluxite", $"{ColonySimulation.TurretCost} cr / laser" };
            string[] icons = { "rover", "extractor", "solar", "conduit", "rail", "solar", "focus" };
            for (int index = 0; index < names.Length; index++)
                if (ToolbarCard(new Rect(28 + index * 147, bottom + 13, 139, 44), names[index], subtitles[index], icons[index], (index + 1).ToString(), tool == (Tool)index && !trainSelected, index == 4 || index == 1 ? gold : cyan)) SetTool((Tool)index);
            GUI.Label(new Rect(1067, bottom + 12, UiWidth - 1091, 50), "WASD: pan  Scroll: zoom\nSpace: pause\nR: prefer other bend", smallStyle);
            string message = Simulation.Message;
            if (tool == Tool.Conduit || tool == Tool.Rail)
            {
                message = routeStart.HasValue ? "Choose a highlighted tile. Bright ports join buildings; outlined tiles extend the route. Bend adjusts automatically." : "Click a glowing port or a building to choose its port. Then click the destination; valid bends are automatic.";
                if (routeStart.HasValue && hover.HasValue)
                {
                    bool valid = Simulation.TryPlanNetworkRoute(routeStart.Value, NetworkEndpoint(hover.Value), tool == Tool.Rail, verticalFirst, out _, out int cost, out string reason);
                    message = routeStart.Value.Equals(NetworkEndpoint(hover.Value)) ? "Start selected. Click another highlighted tile or a destination port." : valid ? $"BUILD {tool.ToString().ToUpper()}   {cost} credits   /   Click to build this path; R prefers the other bend; right-click cancels." : reason;
                }
            }
            else if (BuildingTool && hover.HasValue)
            {
                bool valid = Simulation.CanBuild(BuildKind, hover.Value, out _, out int size, out int cost, out string reason);
                message = valid ? $"BUILD {tool.ToString().ToUpper()}   {size} x {size} tiles   /   {cost} credits   /   Click to confirm." : reason;
            }
            float hintWidth = Mathf.Min(600, UiWidth - 32);
            float hintHeight = Mathf.Max(48, bodyStyle.CalcHeight(new GUIContent(message), hintWidth - 24) + 12);
            hintPanelRect = new Rect(16, bottom - hintHeight - 8, hintWidth, hintHeight);
            Fill(hintPanelRect, new Color(0.045f, 0.07f, 0.12f, 0.9f));
            GUI.Label(new Rect(hintPanelRect.x + 12, hintPanelRect.y + 6, hintWidth - 24, hintHeight - 12), message, bodyStyle);
        }

        private void DrawWorldLabels()
        {
            foreach (var deposit in Simulation.Deposits)
            {
                if (!Simulation.FullyRevealed(deposit) || deposit.Extractor != null) continue;
                var center = Position(deposit.Origin) + new Vector3(deposit.Size - 1, 0, deposit.Size - 1);
                var cameraUp = worldCamera.transform.up;
                float halfWidth = deposit.Size * TerrainGrid.CellSize * 0.5f;
                float modelHeight = deposit.Resource == ResourceKind.Fluxite && FluxiteModel != null ? 1.4f : 0.8f;
                float topOffset = halfWidth * (Mathf.Abs(cameraUp.x) + Mathf.Abs(cameraUp.z)) + modelHeight * Mathf.Abs(cameraUp.y);
                WorldLabel(center + cameraUp * topOffset, $"{deposit.Resource.ToString().ToUpper()}  {deposit.Size}x{deposit.Size} / {deposit.Rate:0.##}/s", deposit.Resource == ResourceKind.Fluxite ? cyan : gold, -32);
            }
            if (!NetworkTool && (!LinkGuideActive || linkStops.Count < 2)) WorldLabel(Position(Simulation.Colony.Port, 0.2f), "COLONY PORT", cyan);
            if (!NetworkTool && selected != null && selected != Simulation.Colony && (!LinkGuideActive || linkStops.Count < 2))
                WorldLabel(Position(selected.Port, 0.2f),
                    selected.Connected ? "POWER CONNECTED" : "CONNECT POWER HERE",
                    selected.Connected ? cyan : gold);
        }

        private void WorldLabel(Vector3 position, string caption, Color accent, float verticalOffset = 8)
        {
            Vector3 screen = worldCamera.WorldToScreenPoint(position);
            if (screen.z < 0) return;
            var rectangle = new Rect(screen.x / UiScale - 85, (Screen.height - screen.y) / UiScale + verticalOffset, 170, 24);
            if (rectangle.y < 74 || rectangle.yMax > UiHeight - 130 || ContextPanelOverlaps(rectangle)) return;
            Fill(rectangle, new Color(0.04f, 0.065f, 0.10f, 0.9f));
            Fill(new Rect(rectangle.x, rectangle.y, 3, rectangle.height), accent);
            GUI.Label(rectangle, caption, labelStyle);
        }

        private void OnDestroy()
        {
            DisposeFogVisuals();
            ResetNetworkPlacement();
            if (worldRoot != null) Destroy(worldRoot.gameObject);
            foreach (var material in ownedMaterials) if (material != null) Destroy(material);
        }
    }
}
