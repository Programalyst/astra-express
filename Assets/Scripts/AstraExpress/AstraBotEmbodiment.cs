using UnityEngine;

namespace AstraExpress
{
    public sealed partial class AstraGame
    {
        private Transform companionVisual;
        private LineRenderer companionBeam;
        private Transform companionFace;
        private string companionMode = "idle";
        private Vector3 companionWorkPosition;
        private float companionModeSince;
        private Vector3 companionBaseScale;

        // Presentation only: never moves the rover, spends credits or picks a tile.
        public void CoachSetCompanionMode(string mode)
        {
            if (mode != "idle" && mode != "thinking" && mode != "launching" && mode != "ready" && mode != "working" && mode != "off") return;
            if (mode == companionMode) return;
            companionMode = mode;
            companionModeSince = Time.unscaledTime;
        }

        private void UpdateAstraBotVisual()
        {
            if (worldRoot == null || AstraBotModel == null) return;
            Vector3 home = Position(Simulation.Colony.Port, 1.1f) + new Vector3(-1.6f, 0, -1.3f);
            if (companionVisual == null)
            {
                companionVisual = Model(AstraBotModel, "AstraBot service drone", worldRoot, home, 1.2f, 0.65f).transform;
                companionBaseScale = companionVisual.localScale;
                companionWorkPosition = home;
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
                    ? Mathf.Clamp(Vector3.Distance(companionVisual.position, Position(botTarget.Value, 1.1f) + new Vector3(1.2f, 0, -1.2f)) / 3.2f + 0.5f, 1.4f, 5f)
                    : 1.4f;
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
            }
            bool visible = companionMode != "off";
            companionVisual.gameObject.SetActive(visible);
            if (!visible) return;
            companionFace.rotation = worldCamera.transform.rotation;
            bool working = botBusy && botTarget.HasValue && Simulation.IsRevealed(botTarget.Value);
            if (working) companionWorkPosition = Position(botTarget.Value, 1.1f) + new Vector3(1.2f, 0, -1.2f);
            // Camera-relative berth beside (not on top of) the bottom-left UI.
            // Orthographic projection needs explicit scale compensation as the player zooms.
            float dockY = Mathf.Clamp01((138f * UiScale + 125f) / Screen.height);
            Vector3 dock = worldCamera.ViewportToWorldPoint(new Vector3(0.37f, dockY, 8f));
            float launchAge = Time.unscaledTime - companionModeSince;
            bool launching = companionMode == "launching";
            bool deployed = working || companionMode == "working" || companionMode == "ready";
            float launchProgress = launching ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((launchAge - 1.5f) / 4f)) : 0f;
            Vector3 destination = deployed ? (working ? companionWorkPosition : home) : launching ? Vector3.Lerp(dock, home + Vector3.up * 2f, launchProgress) : dock;
            if (launching) destination += worldCamera.transform.up * Mathf.Sin(launchProgress * Mathf.PI) * 2f;
            destination += worldCamera.transform.up * Mathf.Sin(Time.unscaledTime * (companionMode == "thinking" ? 2f : 1.2f)) * 0.045f;
            float dockScale = Mathf.Clamp(worldCamera.orthographicSize / 12f, 0.8f, 3f);
            float size = deployed ? 1f : Mathf.Lerp(dockScale, 1f, launchProgress);
            companionVisual.localScale = Vector3.Lerp(companionVisual.localScale, companionBaseScale * size, Time.unscaledDeltaTime * 3f);
            Vector3 direction = destination - companionVisual.position;
            float speed = working ? 3.2f : Mathf.Max(6f, direction.magnitude * 1.5f);
            companionVisual.position = Vector3.MoveTowards(companionVisual.position, destination, speed * Time.unscaledDeltaTime);
            Vector3 heading = new Vector3(direction.x, 0, direction.z);
            if (heading.sqrMagnitude > 0.04f)
                companionVisual.rotation = Quaternion.Slerp(companionVisual.rotation, Quaternion.LookRotation(heading), Time.unscaledDeltaTime * 3f);
            if (launching && launchAge < 1.5f) companionVisual.Rotate(Vector3.up, 90f * Time.unscaledDeltaTime, Space.World);
            companionBeam.enabled = working && direction.sqrMagnitude < 1.2f;
            if (companionBeam.enabled)
            {
                companionBeam.SetPosition(0, companionVisual.position + Vector3.up * 0.12f);
                companionBeam.SetPosition(1, Position(botTarget.Value, 0.25f));
            }
        }
    }
}
