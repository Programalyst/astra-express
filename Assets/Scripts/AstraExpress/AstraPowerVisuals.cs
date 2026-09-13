using UnityEngine;

namespace AstraExpress
{
    public sealed partial class AstraGame
    {
        // Keep power alongside the rails, with a separate jumper into each building.
        private static readonly Vector3 PowerLaneOffset = new Vector3(0.66f, 0.16f, 0.66f);
        private Material disconnectedPortMaterial;

        private void DrawPowerNetwork()
        {
            if (disconnectedPortMaterial == null)
                disconnectedPortMaterial = MakeMaterial(gold, 0.25f);

            foreach (var cell in Simulation.Conduits)
            {
                bool connected = Simulation.PoweredCells.Contains(cell);
                Vector3 node = Position(cell) + PowerLaneOffset;
                DrawPowerJunction(node, connected ? powerMaterial : darkPowerMaterial);
                foreach (var direction in ColonySimulation.Directions)
                {
                    var adjacent = cell + direction;
                    if (!Simulation.Conduits.Contains(adjacent) || direction.X + direction.Y < 0) continue;
                    DrawPowerCable(node, Position(adjacent) + PowerLaneOffset, connected);
                }
            }

            foreach (var structure in Simulation.Structures) DrawBuildingConnection(structure);
        }

        private void DrawPowerJunction(Vector3 node, Material indicator)
        {
            Box("Power junction housing", networkRoot, node, new Vector3(0.38f, 0.25f, 0.38f), foundationMaterial);
            Box("Power junction light", networkRoot, node + Vector3.up * 0.145f,
                new Vector3(0.24f, 0.045f, 0.24f), indicator);
        }

        private void DrawPowerCable(Vector3 start, Vector3 end, bool connected)
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
                new Vector3(0.13f, 0.06f, direction.magnitude + 0.08f),
                connected ? powerMaterial : darkPowerMaterial);
            core.transform.localRotation = rotation;
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
            Vector3 socket = new Vector3(body.center.x, 0.39f, body.min.z + 0.16f);
            float foundationFront = building.position.z - structure.Size * 1.93f * 0.5f;
            Vector3 node = Position(structure.Port) + PowerLaneOffset;
            Vector3 ramp = new Vector3(node.x, 0.27f, foundationFront - 0.10f);
            Vector3 elbow = new Vector3(socket.x, 0.27f, ramp.z);
            Vector3 inlet = new Vector3(socket.x, 0.27f, socket.z - 0.22f);
            DrawPowerCable(node, ramp, structure.Connected);
            DrawPowerCable(ramp, elbow, structure.Connected);
            DrawPowerCable(elbow, inlet, structure.Connected);
            if (!Simulation.Conduits.Contains(structure.Port)) DrawPowerJunction(node, indicator);

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
            float glow = 0.6f;
            if (!Simulation.Paused && (Simulation.Battery > 0.01f || Simulation.Generation > 0))
                glow += 0.18f * (0.5f + 0.5f * Mathf.Sin(Time.time * 2.5f));
            powerMaterial.SetColor("_EmissionColor", cyan * glow);
        }
    }
}
