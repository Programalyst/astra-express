using System.Collections.Generic;
using UnityEngine;

namespace AstraExpress
{
    public sealed partial class AstraGame
    {
        // Keep power alongside the rails, with a separate jumper into each building.
        private static readonly Vector3 PowerLaneOffset = new Vector3(0.66f, 0.16f, 0.66f);
        private Material disconnectedPortMaterial;
        private Material powerFlowMaterial;
        private bool powerFlowShaderChecked;
        private float powerFlowTime;
        private float nextPowerFlowRefresh;
        private readonly List<PowerFlowSegment> powerFlowSegments = new List<PowerFlowSegment>();
        private readonly Dictionary<Cell, int> powerSourceDistances = new Dictionary<Cell, int>();
        private readonly HashSet<Cell> powerSourcePorts = new HashSet<Cell>();
        private MaterialPropertyBlock powerFlowProperties;
        private static readonly int FlowLengthId = Shader.PropertyToID("_FlowLength");
        private static readonly int FlowOffsetId = Shader.PropertyToID("_FlowOffset");
        private static readonly int FlowDirectionId = Shader.PropertyToID("_FlowDirection");
        private static readonly int FlowAmountId = Shader.PropertyToID("_FlowAmount");
        private static readonly int FlowTimeId = Shader.PropertyToID("_FlowTime");
        private static readonly int PowerAvailableId = Shader.PropertyToID("_PowerAvailable");

        private sealed class PowerFlowSegment
        {
            public Renderer Renderer;
            public Cell From;
            public Cell To;
            public Structure Building;
            public float Length;
            public float Offset;
            public float Direction = float.NaN;
            public float Amount = float.NaN;
        }

        private void DrawPowerNetwork()
        {
            if (disconnectedPortMaterial == null)
                disconnectedPortMaterial = MakeMaterial(gold, 0.25f);
            EnsurePowerFlowMaterial();
            // SyncWorld replaces the network hierarchy; retain no stale renderers.
            powerFlowSegments.Clear();

            foreach (var cell in Simulation.Conduits)
            {
                bool connected = Simulation.PoweredCells.Contains(cell);
                DrawPowerJunction(cell, connected ? powerMaterial : darkPowerMaterial);
                foreach (var direction in ColonySimulation.Directions)
                {
                    var adjacent = cell + direction;
                    if (!Simulation.Conduits.Contains(adjacent) || direction.X + direction.Y < 0 ||
                        !Simulation.Terrain.CanTraverse(cell, adjacent)) continue;
                    DrawSurfacePowerCable(cell, adjacent, connected);
                }
            }

            foreach (var structure in Simulation.Structures) DrawBuildingConnection(structure);
            RefreshPowerFlow();
        }

        private void EnsurePowerFlowMaterial()
        {
            if (powerFlowShaderChecked) return;
            powerFlowShaderChecked = true;
            darkPowerMaterial.SetColor("_BaseColor", new Color(0.055f, 0.095f, 0.12f));
            // Resources keeps the shader in the WebGL player even without a scene material.
            var shader = Resources.Load<Shader>("Rendering/AstraPowerFlow");
            if (shader == null || !shader.isSupported) return;
            powerFlowMaterial = new Material(shader) { name = "Live conduit power flow" };
            ownedMaterials.Add(powerFlowMaterial);
        }

        private Vector3 PowerNode(Cell cell) => Position(cell.X + PowerLaneOffset.x / 2,
            cell.Y + PowerLaneOffset.z / 2, PowerLaneOffset.y);

        private void DrawPowerJunction(Cell cell, Material indicator)
        {
            Vector3 node = PowerNode(cell);
            Quaternion rotation = GroundRotation(cell.X + PowerLaneOffset.x / 2,
                cell.Y + PowerLaneOffset.z / 2, Vector3.forward);
            var housing = Box("Power junction housing", networkRoot, node,
                new Vector3(0.38f, 0.25f, 0.38f), foundationMaterial);
            housing.transform.rotation = rotation;
            var light = Box("Power junction light", networkRoot, node + rotation * Vector3.up * 0.145f,
                new Vector3(0.24f, 0.045f, 0.24f), indicator);
            light.transform.rotation = rotation;
        }

        private void DrawSurfacePowerCable(Cell from, Cell to, bool connected)
        {
            Vector3 previous = PowerNode(from);
            float offset = 0;
            float start = from.X + PowerLaneOffset.x / 2;
            float end = to.X + PowerLaneOffset.x / 2;
            // Network edges are drawn east or north only. Split at the tile edges
            // so the casing and its glowing core follow both ends of each ramp.
            if (from.X != to.X)
                for (float boundary = Mathf.Floor(start + 0.5f) + 0.5f; boundary < end; boundary++)
                {
                    Vector3 next = Position(boundary, from.Y + PowerLaneOffset.z / 2, PowerLaneOffset.y);
                    DrawPowerCable(previous, next, connected, from, to, null, offset);
                    offset += Vector3.Distance(previous, next);
                    previous = next;
                }
            DrawPowerCable(previous, PowerNode(to), connected, from, to, null, offset);
        }

        private void DrawPowerCable(Vector3 start, Vector3 end, bool connected,
            Cell from = default, Cell to = default, Structure building = null, float flowOffset = 0)
        {
            Vector3 direction = end - start;
            if (direction.sqrMagnitude < 0.0001f) return;
            Quaternion rotation = Quaternion.LookRotation(direction);
            Vector3 center = (start + end) * 0.5f;
            var casing = Box("Conduit casing", networkRoot, center,
                new Vector3(0.24f, 0.16f, direction.magnitude + 0.08f), foundationMaterial);
            casing.transform.localRotation = rotation;
            var core = Box(connected ? "Connected power line" : "Unpowered conduit", networkRoot,
                center + rotation * Vector3.up * 0.09f,
                new Vector3(0.17f, 0.065f, direction.magnitude + 0.08f),
                connected ? powerFlowMaterial != null ? powerFlowMaterial : powerMaterial : darkPowerMaterial);
            core.transform.localRotation = rotation;
            if (connected && powerFlowMaterial != null)
            {
                var renderer = core.GetComponent<Renderer>();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                powerFlowSegments.Add(new PowerFlowSegment
                {
                    Renderer = renderer, From = from, To = to, Building = building,
                    Length = direction.magnitude + 0.08f, Offset = flowOffset - 0.04f
                });
            }
        }

        private void DrawBuildingConnection(Structure structure)
        {
            Material indicator = structure.Connected ? powerMaterial : disconnectedPortMaterial;
            Vector3 port = Position(structure.Port, 0.07f);
            // An open border keeps the shared rail stop visible and usable.
            Box("Port border west", networkRoot, port + Vector3.left * 0.79f,
                new Vector3(0.075f, 0.06f, 1.65f), indicator);
            Box("Port border east", networkRoot, port + Vector3.right * 0.79f,
                new Vector3(0.075f, 0.06f, 1.65f), indicator);
            Box("Port border south", networkRoot, port + Vector3.back * 0.79f,
                new Vector3(1.65f, 0.06f, 0.075f), indicator);
            Box("Port border north", networkRoot, port + Vector3.forward * 0.79f,
                new Vector3(1.65f, 0.06f, 0.075f), indicator);

            Transform building = buildings[structure];
            Transform model = building.Find(structure.Kind.ToString());
            var renderers = model.GetComponentsInChildren<Renderer>();
            Bounds body = new Bounds(building.position + Vector3.up * 0.6f, Vector3.one);
            if (renderers.Length > 0)
            {
                body = renderers[0].bounds;
                foreach (var renderer in renderers) body.Encapsulate(renderer.bounds);
            }

            // Use the actual model bounds: prefab footprints can be much smaller
            // than their foundation. The socket overlaps the model's front face.
            float groundHeight = building.position.y;
            Vector3 socket = new Vector3(body.center.x, groundHeight + 0.39f, body.min.z + 0.16f);
            float foundationFront = building.position.z - structure.Size * 1.93f * 0.5f;
            Vector3 node = PowerNode(structure.Port);
            Vector3 ramp = new Vector3(node.x, groundHeight + 0.27f, foundationFront - 0.10f);
            Vector3 elbow = new Vector3(socket.x, groundHeight + 0.27f, ramp.z);
            Vector3 inlet = new Vector3(socket.x, groundHeight + 0.27f, socket.z - 0.22f);
            DrawPowerCable(node, ramp, structure.Connected, building: structure);
            DrawPowerCable(ramp, elbow, structure.Connected, building: structure,
                flowOffset: Vector3.Distance(node, ramp));
            DrawPowerCable(elbow, inlet, structure.Connected, building: structure,
                flowOffset: Vector3.Distance(node, ramp) + Vector3.Distance(ramp, elbow));
            if (!Simulation.Conduits.Contains(structure.Port)) DrawPowerJunction(structure.Port, indicator);

            Box("Building power socket", networkRoot, socket,
                new Vector3(0.54f, 0.48f, 0.48f), foundationMaterial);
            Box("Socket connection light", networkRoot, socket + new Vector3(0, 0.26f, -0.035f),
                new Vector3(0.38f, 0.05f, 0.30f), indicator);
            Box("Cable plug", networkRoot, socket + new Vector3(0, -0.09f, -0.27f),
                new Vector3(0.30f, 0.24f, 0.20f), indicator);

            // Two bright contacts make the connection readable from the game camera.
            for (int side = -1; side <= 1; side += 2)
                Box("Socket contact", networkRoot, socket + new Vector3(side * 0.17f, 0, -0.25f),
                    new Vector3(0.06f, 0.28f, 0.045f), indicator);
        }

        private void UpdatePowerVisuals()
        {
            bool hasPower = Simulation.Battery > 0.01f || Simulation.Generation > 0;
            if (powerFlowMaterial != null)
            {
                // Simulation.Paused does not change Unity's time scale.
                if (!Simulation.Paused && hasPower) powerFlowTime += Time.deltaTime;
                powerFlowMaterial.SetFloat(FlowTimeId, powerFlowTime);
                powerFlowMaterial.SetFloat(PowerAvailableId, hasPower ? 1 : 0);
                if (Time.unscaledTime >= nextPowerFlowRefresh)
                {
                    RefreshPowerFlow();
                    nextPowerFlowRefresh = Time.unscaledTime + 0.25f;
                }
            }
            float glow = 0.6f;
            if (!Simulation.Paused && hasPower)
                glow += 0.18f * (0.5f + 0.5f * Mathf.Sin(Time.time * 2.5f));
            powerMaterial.SetColor("_EmissionColor", cyan * glow);
        }

        private void RefreshPowerFlow()
        {
            if (powerFlowMaterial == null) return;
            powerSourceDistances.Clear();
            powerSourcePorts.Clear();
            var frontier = new Queue<Cell>();
            foreach (var structure in Simulation.Structures)
                if (structure.Connected && Simulation.PoweredCells.Contains(structure.Port) &&
                    (structure.Kind == StructureKind.Solar ||
                     structure.Kind == StructureKind.PowerPlant && structure.Generation > 0.01f))
                    powerSourcePorts.Add(structure.Port);
            // Starter solar supplies the colony internally until wired into the visible grid.
            // Battery supply also originates here when no connected generator is producing.
            if (powerSourcePorts.Count == 0) powerSourcePorts.Add(Simulation.Colony.Port);
            foreach (var port in powerSourcePorts)
            {
                powerSourceDistances[port] = 0;
                frontier.Enqueue(port);
            }
            while (frontier.Count > 0)
            {
                Cell cell = frontier.Dequeue();
                foreach (var direction in ColonySimulation.Directions)
                {
                    Cell next = cell + direction;
                    if (!Simulation.PoweredCells.Contains(next) || powerSourceDistances.ContainsKey(next) ||
                        !Simulation.Terrain.CanTraverse(cell, next)) continue;
                    powerSourceDistances[next] = powerSourceDistances[cell] + 1;
                    frontier.Enqueue(next);
                }
            }
            foreach (var segment in powerFlowSegments)
            {
                float direction = 0;
                float amount = 1;
                if (segment.Building != null)
                {
                    var structure = segment.Building;
                    direction = powerSourcePorts.Contains(structure.Port) || structure.Kind == StructureKind.Solar ? -1 : 1;
                    if (structure.Kind == StructureKind.Extractor)
                        amount = !structure.Paused && structure.Stock < structure.Storage && structure.SuppliedFraction > 0.01f ? 1 : 0;
                    else if (structure.Kind == StructureKind.PowerPlant)
                        amount = structure.Generation > 0.01f ? 1 : 0;
                }
                else if (powerSourceDistances.TryGetValue(segment.From, out int fromDistance) &&
                    powerSourceDistances.TryGetValue(segment.To, out int toDistance))
                    // Equal-distance cross-links have no single sourceward direction.
                    direction = fromDistance == toDistance ? 0 : toDistance > fromDistance ? 1 : -1;
                if (segment.Direction == direction && segment.Amount == amount) continue;
                segment.Direction = direction;
                segment.Amount = amount;
                powerFlowProperties.Clear();
                powerFlowProperties.SetFloat(FlowLengthId, segment.Length);
                powerFlowProperties.SetFloat(FlowOffsetId, segment.Offset);
                powerFlowProperties.SetFloat(FlowDirectionId, direction);
                powerFlowProperties.SetFloat(FlowAmountId, amount);
                segment.Renderer.SetPropertyBlock(powerFlowProperties);
            }
        }
    }
}
