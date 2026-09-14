using UnityEngine;

namespace AstraExpress
{
    public sealed partial class AstraGame
    {
        private Transform companionVisual;
        private LineRenderer companionBeam;
        private Transform companionScanner;
        private Mesh companionScannerMesh;
        private Material companionScannerFillMaterial, companionScannerLineMaterial;
        private LineRenderer[] companionScannerLines;
        private readonly Vector3[] companionScannerVertices = new Vector3[3];
        private readonly Vector3[] companionScannerOutline = new Vector3[4];
        private readonly Vector3[] companionScannerSegment = new Vector3[2];
        private static readonly int[] CompanionScannerTriangles = { 0, 1, 2 };
        private Transform companionFace;
        private string companionMode = "idle";
        private Vector3 companionWorkPosition;
        private float companionModeSince;
        private Vector3 companionBaseScale;
        private Vector2 companionDockViewport = new Vector2(0.43f, 0.23f);
        private bool companionDocked = true;

        // Presentation only: never moves the rover, spends credits or picks a tile.
        public void CoachSetCompanionMode(string mode)
        {
            if (mode != "idle" && mode != "thinking" && mode != "launching" && mode != "ready" && mode != "working" && mode != "returning" && mode != "off") return;
            if (mode == companionMode) return;
            companionMode = mode;
            companionModeSince = Time.unscaledTime;
            if (mode == "returning") companionDocked = false;
            else if (mode == "idle") companionDocked = true;
        }

        public void CoachSetCompanionDock(string value)
        {
            string[] parts = (value ?? "").Split(',');
            if (parts.Length != 2 || !int.TryParse(parts[0], out int x) || !int.TryParse(parts[1], out int y)) return;
            companionDockViewport = new Vector2(Mathf.Clamp(x, 0, 1000) / 1000f, Mathf.Clamp(y, 0, 1000) / 1000f);
        }

        private Vector3 CompanionCenter()
        {
            bool found = false;
            Bounds bounds = default;
            foreach (var renderer in companionVisual.GetComponentsInChildren<Renderer>())
            {
                if (renderer == companionBeam || !renderer.enabled) continue;
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            return found ? bounds.center : companionVisual.position;
        }

        private LineRenderer CompanionScannerLine(string objectName, float width)
        {
            var line = new GameObject(objectName).AddComponent<LineRenderer>();
            line.transform.SetParent(companionScanner, false);
            line.gameObject.layer = 2;
            line.sharedMaterial = companionScannerLineMaterial;
            line.widthMultiplier = width;
            line.useWorldSpace = true;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.numCapVertices = 2;
            return line;
        }

        private void CreateCompanionScanner()
        {
            companionScanner = new GameObject("AstraBot triangular holographic scanner").transform;
            companionScanner.SetParent(worldRoot, false);
            companionScanner.gameObject.layer = 2;
            Color fill = new Color(0.12f, 1f, 0.48f, 0.11f);
            Color line = new Color(0.28f, 1f, 0.62f, 0.88f);
            companionScannerFillMaterial = TransparentGuideMaterial(MakeMaterial(fill, 1.4f));
            companionScannerFillMaterial.SetFloat("_Cull", 0);
            companionScannerLineMaterial = TransparentGuideMaterial(MakeMaterial(line, 2.2f));
            var surface = new GameObject("Emissive scanner fan");
            surface.transform.SetParent(companionScanner, false);
            surface.layer = 2;
            companionScannerMesh = new Mesh { name = "AstraBot scanner triangle" };
            companionScannerMesh.vertices = companionScannerVertices;
            companionScannerMesh.triangles = CompanionScannerTriangles;
            surface.AddComponent<MeshFilter>().sharedMesh = companionScannerMesh;
            var renderer = surface.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = companionScannerFillMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            companionScannerLines = new[] {
                CompanionScannerLine("Scanner outline", 0.045f),
                CompanionScannerLine("Scanner centre ray", 0.028f),
                CompanionScannerLine("Scanner grid 1", 0.022f),
                CompanionScannerLine("Scanner grid 2", 0.022f),
                CompanionScannerLine("Scanner grid 3", 0.022f),
                CompanionScannerLine("Scanner grid 4", 0.022f)
            };
            companionScanner.gameObject.SetActive(false);
        }

        private void UpdateCompanionScanner(bool visible, Vector3 source, Vector3 target)
        {
            if (companionScanner == null) return;
            companionScanner.gameObject.SetActive(visible);
            if (!visible) return;
            Vector3 travel = target - source;
            Vector3 across = Vector3.Cross(Vector3.up, travel).normalized;
            if (across.sqrMagnitude < 0.01f) across = worldCamera.transform.right;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4.2f);
            float halfWidth = 0.82f + pulse * 0.14f;
            Vector3 left = target - across * halfWidth;
            Vector3 right = target + across * halfWidth;
            companionScannerVertices[0] = companionScanner.InverseTransformPoint(source);
            companionScannerVertices[1] = companionScanner.InverseTransformPoint(left);
            companionScannerVertices[2] = companionScanner.InverseTransformPoint(right);
            companionScannerMesh.vertices = companionScannerVertices;
            companionScannerMesh.RecalculateNormals();
            companionScannerMesh.RecalculateBounds();
            Color green = new Color(0.18f, 1f, 0.52f, 1f);
            companionScannerFillMaterial.SetColor("_BaseColor", new Color(green.r, green.g, green.b, 0.08f + pulse * 0.06f));
            companionScannerFillMaterial.SetColor("_EmissionColor", green * (1.0f + pulse * 0.7f));
            companionScannerLineMaterial.SetColor("_EmissionColor", green * (1.7f + pulse * 1.1f));
            companionScannerLines[0].positionCount = 4;
            companionScannerOutline[0] = source;
            companionScannerOutline[1] = left;
            companionScannerOutline[2] = right;
            companionScannerOutline[3] = source;
            companionScannerLines[0].SetPositions(companionScannerOutline);
            companionScannerLines[1].positionCount = 2;
            companionScannerSegment[0] = source;
            companionScannerSegment[1] = target;
            companionScannerLines[1].SetPositions(companionScannerSegment);
            for (int index = 0; index < 4; index++)
            {
                float phase = Mathf.Repeat(Time.unscaledTime * 0.42f + index * 0.25f, 1f);
                float distance = Mathf.Lerp(0.18f, 0.94f, phase);
                companionScannerLines[index + 2].positionCount = 2;
                companionScannerSegment[0] = Vector3.Lerp(source, left, distance);
                companionScannerSegment[1] = Vector3.Lerp(source, right, distance);
                companionScannerLines[index + 2].SetPositions(companionScannerSegment);
            }
        }

        private void UpdateAstraBotVisual()
        {
            if (worldRoot == null || AstraBotModel == null) return;
            // Both anchors are camera-relative: the companion belongs to the UI
            // dock until a real, revealed game target exists. The planning hold is
            // deliberately not derived from the colony port.
            Vector3 dock = worldCamera.ViewportToWorldPoint(new Vector3(companionDockViewport.x, companionDockViewport.y, 8f));
            Vector3 roverHover = Position(Simulation.RoverCell, 1.1f) + Vector3.up * 0.9f;
            if (companionVisual == null)
            {
                companionVisual = Model(AstraBotModel, "AstraBot service drone", worldRoot, dock, 1.2f, 0.65f).transform;
                companionBaseScale = companionVisual.localScale;
                companionWorkPosition = roverHover;
                // A tiny camera-facing face badge preserves the mint AstraBot identity.
                // World-space geometry stays depth tested rather than covering the HUD.
                companionFace = new GameObject("AstraBot face badge").transform;
                companionFace.SetParent(companionVisual, false);
                companionFace.localPosition = Vector3.up * 0.72f;
                var shell = MakeMaterial(new Color(0.65f, 0.87f, 0.8f));
                var visor = MakeMaterial(new Color(0.06f, 0.18f, 0.23f));
                Box("Mint shell", companionFace, Vector3.zero, new Vector3(0.48f, 0.38f, 0.06f), shell);
                Box("Visor", companionFace, new Vector3(0, 0, -0.04f), new Vector3(0.38f, 0.25f, 0.035f), visor);
                Box("Left eye", companionFace, new Vector3(-0.1f, 0.015f, -0.065f), new Vector3(0.045f, 0.095f, 0.015f), powerMaterial);
                Box("Right eye", companionFace, new Vector3(0.1f, 0.015f, -0.065f), new Vector3(0.045f, 0.095f, 0.015f), powerMaterial);
                Box("Smile", companionFace, new Vector3(0, -0.075f, -0.065f), new Vector3(0.09f, 0.018f, 0.015f), powerMaterial);
                Box("Antenna", companionFace, new Vector3(0, 0.24f, 0), new Vector3(0.025f, 0.1f, 0.025f), shell);
                Box("Signal", companionFace, new Vector3(0, 0.3f, 0), new Vector3(0.06f, 0.06f, 0.04f), orangeMaterial);
                companionArrivalWait = () => botTarget.HasValue && companionVisual != null
                    ? Mathf.Clamp(Vector3.Distance(companionVisual.position, Position(botTarget.Value, 1.1f) + new Vector3(1.2f, 0, -1.2f)) / 6.5f + 0.35f, 0.8f, 3f)
                    : 0.8f;
                foreach (var collider in companionVisual.GetComponentsInChildren<Collider>()) Destroy(collider);
                foreach (var child in companionVisual.GetComponentsInChildren<Transform>()) child.gameObject.layer = 2; // Ignore Raycast
                var beam = new GameObject("Quiet work indicator"); beam.transform.SetParent(companionVisual, false);
                companionBeam = beam.AddComponent<LineRenderer>();
                companionBeam.sharedMaterial = powerMaterial;
                companionBeam.positionCount = 2;
                companionBeam.startWidth = 0.025f; companionBeam.endWidth = 0.008f;
                companionBeam.useWorldSpace = true;
                companionBeam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                companionBeam.receiveShadows = false;
                CreateCompanionScanner();
            }
            bool visible = companionMode != "off";
            companionVisual.gameObject.SetActive(visible);
            if (!visible)
            {
                if (companionScanner != null) companionScanner.gameObject.SetActive(false);
                return;
            }
            companionFace.rotation = worldCamera.transform.rotation;
            bool working = botBusy && botTarget.HasValue && Simulation.IsRevealed(botTarget.Value);
            if (working) companionWorkPosition = Position(botTarget.Value, 1.1f) + new Vector3(1.2f, 0, -1.2f);
            // Camera-relative berth beside (not on top of) the bottom-left UI.
            // Orthographic projection needs explicit scale compensation as the player zooms.
            float launchAge = Time.unscaledTime - companionModeSince;
            bool launching = companionMode == "launching";
            bool thinking = companionMode == "thinking";
            bool returning = companionMode == "returning";
            bool deployed = working || companionMode == "working" || companionMode == "ready";
            const float flourishSeconds = 1.6f;
            float flourish = launching ? Mathf.Clamp01(launchAge / flourishSeconds) : 1f;
            float launchProgress = launching ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((launchAge - flourishSeconds) / 2.2f)) : 0f;
            Vector3 figureEight = worldCamera.transform.right * (Mathf.Sin(flourish * Mathf.PI * 2f) * 0.8f)
                + worldCamera.transform.up * (Mathf.Sin(flourish * Mathf.PI * 4f) * 0.4f);
            float orbitPhase = Mathf.Max(0f, launchAge - flourishSeconds) * 1.8f;
            Vector3 thinkingOrbit = worldCamera.transform.right * (Mathf.Cos(orbitPhase) * 1.05f)
                + worldCamera.transform.up * (Mathf.Sin(orbitPhase) * 0.5f);
            float trickCycle = Mathf.Repeat(launchAge, 5.4f);
            float trickProgress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((trickCycle - 2.4f) / 1.45f));
            float trickAngle = trickProgress * Mathf.PI * 2f;
            Vector3 thinkingLoop = worldCamera.transform.up * (Mathf.Sin(trickAngle) * 0.55f)
                + worldCamera.transform.right * ((1f - Mathf.Cos(trickAngle)) * 0.18f);
            // Once launched, hold the last real task position between actions. Falling
            // back to `home` here visibly teleported the drone to the colony port.
            if ((launching || thinking) && launchAge >= flourishSeconds) companionWorkPosition = roverHover;
            Vector3 destination = deployed ? companionWorkPosition
                : thinking ? roverHover + thinkingOrbit + thinkingLoop
                : launching ? (launchAge < flourishSeconds ? dock + figureEight : Vector3.Lerp(dock, roverHover, launchProgress) + thinkingOrbit + thinkingLoop)
                : dock;
            if (launching && launchAge >= flourishSeconds) destination += worldCamera.transform.up * Mathf.Sin(launchProgress * Mathf.PI) * 2f;
            destination += worldCamera.transform.up * Mathf.Sin(Time.unscaledTime * (companionMode == "thinking" ? 2f : 1.2f)) * 0.045f;
            float dockScale = Mathf.Clamp(worldCamera.orthographicSize / 12f, 0.8f, 3f) * 1.45f;
            bool settledDock = companionDocked && !launching && !thinking && !deployed;
            // Keep a stable scale during the flight home. Scaling the imported
            // craft while moving changes its renderer centre and can pull it away
            // from the berth; enlarge only after the rendered centre arrives.
            float size = deployed || thinking || (returning && !companionDocked) ? 1f : Mathf.Lerp(dockScale, 1f, launchProgress);
            Quaternion dockRotation = Quaternion.LookRotation(-worldCamera.transform.forward, worldCamera.transform.up) * Quaternion.Euler(0, 0, -12f);
            if (settledDock)
            {
                // Snap the final presentation transform before measuring renderer
                // bounds. Easing scale/rotation after alignment changed the measured
                // centre every frame and caused a visible corrective wobble.
                companionVisual.localScale = companionBaseScale * dockScale;
                companionVisual.rotation = dockRotation;
                companionFace.rotation = worldCamera.transform.rotation;
            }
            else companionVisual.localScale = Vector3.Lerp(companionVisual.localScale, companionBaseScale * size, Time.unscaledDeltaTime * 3f);
            Vector3 direction = destination - companionVisual.position;
            if (returning || (!launching && !thinking && !deployed))
            {
                // Returning is a screen-space promise. Interpolating toward a world
                // point breaks when the camera follows the rover because that point
                // moves every frame; instead, approach the reported UI berth directly
                // in viewport coordinates, then project back into the scene.
                Vector3 visualCenter = CompanionCenter();
                Vector3 currentView = worldCamera.WorldToViewportPoint(visualCenter);
                if (currentView.z <= 0 || float.IsNaN(currentView.x) || float.IsNaN(currentView.y))
                    currentView = new Vector3(companionDockViewport.x, companionDockViewport.y, 8f);
                currentView.z = 8f;
                Vector3 dockView = new Vector3(companionDockViewport.x, companionDockViewport.y, 8f);
                float screenSpeed = returning ? 0.24f : 0.8f;
                Vector3 nextView = companionDocked ? dockView : Vector3.MoveTowards(currentView, dockView, screenSpeed * Time.unscaledDeltaTime);
                if (returning && Vector2.Distance(new Vector2(nextView.x, nextView.y), companionDockViewport) < 0.003f) companionDocked = true;
                Vector3 nextCenter = worldCamera.ViewportToWorldPoint(nextView);
                companionVisual.position += nextCenter - visualCenter;
            }
            else
            {
                float speed = working ? 6.5f : launching ? Mathf.Max(10f, direction.magnitude * 2.5f) : thinking ? Mathf.Max(8f, direction.magnitude * 2f) : Mathf.Max(7f, direction.magnitude * 1.5f);
                companionVisual.position = Vector3.MoveTowards(companionVisual.position, destination, speed * Time.unscaledDeltaTime);
            }
            Vector3 heading = new Vector3(direction.x, 0, direction.z);
            if (!returning && heading.sqrMagnitude > 0.04f)
            {
                Quaternion flightRotation = Quaternion.LookRotation(heading);
                if (thinking || (launching && launchAge >= flourishSeconds))
                    flightRotation *= Quaternion.Euler(Mathf.Sin(orbitPhase) * 12f, 0, trickProgress * 360f);
                companionVisual.rotation = Quaternion.Slerp(companionVisual.rotation, flightRotation, Time.unscaledDeltaTime * 5f);
            }
            if (launching && launchAge < flourishSeconds) companionVisual.Rotate(Vector3.up, 360f / flourishSeconds * Time.unscaledDeltaTime, Space.World);
            // Freeze the craft body while returning. Rotating the imported model's
            // offset root during screen-space travel can swing the visible mesh away
            // from its correctly moving holder; the face badge remains camera-facing.
            companionBeam.enabled = working && direction.sqrMagnitude < 1.2f;
            if (companionBeam.enabled)
            {
                companionBeam.SetPosition(0, companionVisual.position + Vector3.up * 0.12f);
                companionBeam.SetPosition(1, Position(botTarget.Value, 0.25f));
            }
            companionFace.rotation = worldCamera.transform.rotation;
            Vector3 scanTarget = botTarget.HasValue ? Position(botTarget.Value, 0.2f) : Vector3.zero;
            Vector3 scanView = botTarget.HasValue ? worldCamera.WorldToViewportPoint(scanTarget) : Vector3.zero;
            bool scanVisible = botTarget.HasValue && Simulation.IsRevealed(botTarget.Value) && !returning && !settledDock
                && (!launching || launchAge >= flourishSeconds) && scanView.z > 0 && scanView.x >= 0 && scanView.x <= 1 && scanView.y >= 0 && scanView.y <= 1;
            UpdateCompanionScanner(scanVisible, companionVisual.position - Vector3.up * 0.08f, scanTarget);
        }
    }
}
