using UnityEngine;
using UnityEngine.Rendering;

namespace AstraExpress
{
    public sealed partial class AstraGame
    {
        private const float FogRevealSeconds = 0.7f;
        private const int FogParticleLimit = 128;
        private const int FogBurstLimit = 24;
        private Material fogVeilMaterial;
        private Material fogRevealMaterial;
        private Texture2D fogVisibilityMask;
        private Mesh fogVeilMesh;
        private GameObject fogVisualRoot;
        private ParticleSystem fogRevealParticles;
        private Color32[] fogMaskPixels;
        private float[] fogOpacity;
        private bool[] fogKnownRevealed;
        private bool fogFading;
        private float fogClock;
        private static readonly int FogMaskId = Shader.PropertyToID("_VisibilityMask");
        private static readonly int FogClockId = Shader.PropertyToID("_FogClock");

        // Called after terrain creation and before ResetWorld's initial SyncWorld.
        // The existing opaque unknown tile material remains the secrecy backstop;
        // this visual layer never activates deposits or changes simulation visibility.
        private void InitializeFogVisuals()
        {
            DisposeFogVisuals();
            var surfaceShader = Resources.Load<Shader>("AtmosphereFog/UnknownSurface");
            var veilShader = Resources.Load<Shader>("AtmosphereFog/FogVeil");
            var particleShader = Resources.Load<Shader>("AtmosphereFog/RevealMote");
            if (surfaceShader != null && surfaceShader.isSupported) fogMaterial.shader = surfaceShader;
            if (veilShader == null || !veilShader.isSupported) return;
            if (fogVeilMaterial == null)
            {
                fogVeilMaterial = new Material(veilShader) { name = "Frontier fog veil" };
                ownedMaterials.Add(fogVeilMaterial);
            }
            int count = ColonySimulation.Width * ColonySimulation.Height;
            fogKnownRevealed = new bool[count];
            fogOpacity = new float[count];
            fogMaskPixels = new Color32[count];
            for (int row = 0; row < ColonySimulation.Height; row++)
                for (int column = 0; column < ColonySimulation.Width; column++)
                {
                    int index = row * ColonySimulation.Width + column;
                    bool visible = Simulation.IsRevealed(new Cell(column, row));
                    fogKnownRevealed[index] = visible;
                    fogOpacity[index] = visible ? 0 : 1;
                    byte value = visible ? (byte)0 : (byte)255;
                    fogMaskPixels[index] = new Color32(value, value, value, 255);
                }
            fogVisibilityMask = new Texture2D(ColonySimulation.Width, ColonySimulation.Height, TextureFormat.RGBA32, false, true)
            {
                name = "Fog visibility only", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
            };
            ApplyFogMask();
            fogVeilMaterial.SetTexture(FogMaskId, fogVisibilityMask);
            fogVisualRoot = new GameObject("Frontier atmosphere");
            fogVisualRoot.transform.SetParent(worldRoot, false);
            var filter = fogVisualRoot.AddComponent<MeshFilter>();
            var renderer = fogVisualRoot.AddComponent<MeshRenderer>();
            fogVeilMesh = CreateFogVeilMesh();
            filter.sharedMesh = fogVeilMesh;
            renderer.sharedMaterial = fogVeilMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            if (particleShader != null && particleShader.isSupported) CreateFogRevealPool(particleShader);
            fogClock = 0;
            fogFading = false;
            fogMaterial.SetFloat(FogClockId, 0);
            fogVeilMaterial.SetFloat(FogClockId, 0);
        }

        private Mesh CreateFogVeilMesh()
        {
            int width = ColonySimulation.Width;
            int height = ColonySimulation.Height;
            var vertices = new Vector3[(width + 1) * (height + 1)];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[width * height * 6];
            int triangle = 0;
            for (int row = 0; row <= height; row++)
                for (int column = 0; column <= width; column++)
                {
                    int index = row * (width + 1) + column;
                    // Cell boundaries coincide with the ramp's start/end heights.
                    vertices[index] = Position(column - 0.5f, row - 0.5f, 0.48f);
                    uv[index] = new Vector2((float)column / width, (float)row / height);
                    if (row == height || column == width) continue;
                    int next = index + width + 1;
                    triangles[triangle++] = index;
                    triangles[triangle++] = next;
                    triangles[triangle++] = index + 1;
                    triangles[triangle++] = index + 1;
                    triangles[triangle++] = next;
                    triangles[triangle++] = next + 1;
                }
            var mesh = new Mesh { name = "Terrain-conforming fog veil", vertices = vertices, uv = uv, triangles = triangles };
            mesh.RecalculateBounds();
            return mesh;
        }

        private void CreateFogRevealPool(Shader shader)
        {
            if (fogRevealMaterial == null)
            {
                fogRevealMaterial = new Material(shader) { name = "Frontier reveal motes" };
                ownedMaterials.Add(fogRevealMaterial);
            }
            var root = new GameObject("Shared reveal particle pool");
            root.transform.SetParent(fogVisualRoot.transform, false);
            fogRevealParticles = root.AddComponent<ParticleSystem>();
            fogRevealParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = fogRevealParticles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = FogParticleLimit;
            main.startLifetime = 0.9f;
            main.startSpeed = 0;
            main.gravityModifier = 0;
            var emission = fogRevealParticles.emission;
            emission.enabled = false;
            var shape = fogRevealParticles.shape;
            shape.enabled = false;
            var color = fogRevealParticles.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(new Color(0.58f, 0.92f, 0.89f), 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(0.7f, 0.16f), new GradientAlphaKey(0, 1) });
            color.color = gradient;
            var size = fogRevealParticles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, 0.6f, 1, 1.4f));
            var renderer = fogRevealParticles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = fogRevealMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            fogRevealParticles.Play();
        }

        // Call only when RevealRevision changes, after the authoritative visibility.
        private void SyncFogVisuals()
        {
            if (fogVisibilityMask == null) return;
            int emitted = 0;
            for (int row = 0; row < ColonySimulation.Height; row++)
                for (int column = 0; column < ColonySimulation.Width; column++)
                {
                    int index = row * ColonySimulation.Width + column;
                    var cell = new Cell(column, row);
                    bool visible = Simulation.IsRevealed(cell);
                    if (visible == fogKnownRevealed[index]) continue;
                    fogKnownRevealed[index] = visible;
                    if (!visible) fogOpacity[index] = 1;
                    fogFading = true;
                    if (!visible || fogRevealParticles == null || emitted >= FogBurstLimit) continue;
                    for (int mote = 0; mote < 3 && emitted < FogBurstLimit; mote++, emitted++)
                    {
                        // Tile-coordinate variation avoids consuming gameplay randomness.
                        float phase = column * 2.39f + row * 1.71f + mote * 2.09f;
                        float offsetX = Mathf.Cos(phase) * 0.32f;
                        float offsetY = Mathf.Sin(phase) * 0.32f;
                        var particle = new ParticleSystem.EmitParams
                        {
                            position = Position(column + offsetX, row + offsetY, 0.24f),
                            velocity = new Vector3(offsetX * 0.35f, 0.45f + mote * 0.09f, offsetY * 0.35f),
                            startLifetime = 0.75f + mote * 0.14f,
                            startSize = 0.22f + mote * 0.07f,
                            startColor = new Color(0.72f, 0.96f, 0.93f, 0.72f)
                        };
                        fogRevealParticles.Emit(particle, 1);
                    }
                }
        }

        private void UpdateFogVisuals()
        {
            if (fogVisibilityMask == null) return;
            if (fogRevealParticles != null)
            {
                if (Simulation.Paused && fogRevealParticles.isPlaying) fogRevealParticles.Pause();
                else if (!Simulation.Paused && fogRevealParticles.isPaused) fogRevealParticles.Play();
            }
            if (Simulation.Paused) return;
            fogClock += Mathf.Min(Time.deltaTime, 0.1f);
            fogMaterial.SetFloat(FogClockId, fogClock);
            fogVeilMaterial.SetFloat(FogClockId, fogClock);
            if (!fogFading) return;
            fogFading = false;
            for (int index = 0; index < fogOpacity.Length; index++)
            {
                if (fogKnownRevealed[index]) fogOpacity[index] = Mathf.MoveTowards(fogOpacity[index], 0, Time.deltaTime / FogRevealSeconds);
                if (fogKnownRevealed[index] && fogOpacity[index] > 0) fogFading = true;
                byte value = (byte)Mathf.RoundToInt(fogOpacity[index] * 255);
                fogMaskPixels[index] = new Color32(value, value, value, 255);
            }
            ApplyFogMask();
        }

        private void ApplyFogMask()
        {
            fogVisibilityMask.SetPixels32(fogMaskPixels);
            fogVisibilityMask.Apply(false, false);
        }

        private void DisposeFogVisuals()
        {
            if (fogVisualRoot != null) Destroy(fogVisualRoot);
            if (fogVisibilityMask != null) Destroy(fogVisibilityMask);
            if (fogVeilMesh != null) Destroy(fogVeilMesh);
            fogVisualRoot = null;
            fogVisibilityMask = null;
            fogVeilMesh = null;
            fogRevealParticles = null;
            fogMaskPixels = null;
            fogOpacity = null;
            fogKnownRevealed = null;
        }
    }
}
