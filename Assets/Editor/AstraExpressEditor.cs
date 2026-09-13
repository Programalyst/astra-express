using System;
using System.IO;
using AstraExpress;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class AstraExpressEditor
{
    private const string ScenePath = "Assets/Scenes/AstraExpress.unity";
    private const string SpaceRoot = "Assets/Kenny/kenney_space-kit/Models/FBX format/";
    private static string JobFolder => Path.Combine(Application.dataPath, "../Temp/AstraExpress");
    private static double nextPoll;

    [Serializable]
    private sealed class JobStatus
    {
        public string state;
        public string message;
        public string time;
    }

    static AstraExpressEditor()
    {
        EditorApplication.update -= Poll;
        EditorApplication.update += Poll;
    }

    private static void Status(string state, string message)
    {
        Directory.CreateDirectory(JobFolder);
        File.WriteAllText(Path.Combine(JobFolder, "status.json"), JsonUtility.ToJson(new JobStatus { state = state, message = message, time = DateTime.UtcNow.ToString("O") }, true));
        Debug.Log($"[Astra Express] {state}: {message}");
    }

    private static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        nextPoll = EditorApplication.timeSinceStartup + 0.5;
        string request = Path.Combine(JobFolder, "request.txt");
        if (!File.Exists(request)) return;
        string action = File.ReadAllText(request).Trim();
        File.Delete(request);
        try
        {
            if (action == "validateground")
            {
                AstraGroundSurfaceChecks.Validate();
                Status("validated", "Continuous ground checks passed.");
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before running Editor jobs.");
            if (action == "setup") CreateScene();
            else if (action == "reloadscene")
            {
                if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save scene changes before reloading.");
                EditorSceneManager.OpenScene(ScenePath);
                Status("reloaded", "Reloaded the saved colony scene.");
            }
            else if (action == "switchweb")
            {
                Status("switching", "Switching to Web; wait for compilation and domain reload before building.");
                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL)) throw new InvalidOperationException("Web target switch failed.");
                Status("switched", "Web target selected. Compilation may still be pending.");
            }
            else if (action == "buildweb") BuildWeb();
            else if (action == "postprocessing")
            {
                AstraPostProcessingEditor.Apply();
                Status("postprocessing", "Lightweight post-processing configured and scene saved.");
            }
            else throw new InvalidOperationException("Unknown Editor job: " + action);
        }
        catch (Exception exception) { Status("failed", exception.ToString()); }
    }

    [MenuItem("Astra Express/Create playable scene")]
    public static void CreateScene()
    {
        if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("The current scene has unsaved changes. Save or discard them before creating the Astra scene.");
        if (File.Exists(ScenePath)) throw new InvalidOperationException("AstraExpress.unity already exists; open it instead of overwriting it.");
        Status("setup", "Creating the playable scene and shared material.");
        Directory.CreateDirectory("Assets/GameGenerated");
        AssetDatabase.Refresh();
        var surface = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        surface.SetFloat("_Smoothness", 0.15f);
        surface.enableInstancing = true;
        AssetDatabase.CreateAsset(surface, "Assets/GameGenerated/Surface.mat");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.055f, 0.075f, 0.12f);
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 180;
        cameraObject.AddComponent<AudioListener>();
        var light = new GameObject("Frontier sun").AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1, 0.89f, 0.75f);
        light.intensity = 1.6f;
        light.shadows = LightShadows.Soft;
        light.transform.rotation = Quaternion.Euler(55, -35, 0);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.57f, 0.64f, 0.77f);
        var game = new GameObject("Astra Express").AddComponent<AstraGame>();
        game.SurfaceTemplate = surface;
        game.RoverModel = LoadModel("Assets/Synty/PolygonSciFiWorlds/Prefabs/Props/Vehicles/SM_Veh_Apc_01.prefab");
        game.ColonyModel = LoadModel(SpaceRoot + "hangar_roundA.fbx");
        game.SolarModel = LoadModel("Assets/Kenny/kenney_city-kit-industrial_2.0/Models/FBX format/solar-panel-landscape-group.fbx");
        game.ExtractorModel = LoadModel(SpaceRoot + "machine_generatorLarge.fbx");
        game.OreModel = LoadModel(SpaceRoot + "rock_largeA.fbx");
        game.FluxiteModel = LoadModel("Assets/Imported/NuclearKnights/Prefabs/Environment/Single Crystal.prefab");
        game.TrainModel = LoadModel(SpaceRoot + "monorail_trainFront.fbx");
        game.TerrainModel = LoadModel(SpaceRoot + "terrain.fbx");
        game.RampModel = LoadModel(SpaceRoot + "terrain_ramp.fbx");
        game.HillsideModel = LoadModel(SpaceRoot + "terrain_sideCliff_double.prefab");
        game.HillsideCornerModel = LoadModel(SpaceRoot + "terrain_sideCorner.fbx");
        AstraPostProcessingEditor.Configure(scene, camera);
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        PlayerSettings.productName = "Astra Express";
        PlayerSettings.runInBackground = false;
        QualitySettings.SetQualityLevel(0, true);
        AssetDatabase.SaveAssets();
        Status("ready", "Playable scene created at " + ScenePath);
    }

    private static GameObject LoadModel(string path)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (model == null) throw new FileNotFoundException("Required model is missing: " + path);
        return model;
    }

    [MenuItem("Astra Express/Build Web")]
    public static void BuildWeb()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL) throw new InvalidOperationException("Switch to Web first, then let compilation and domain reload finish.");
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before building.");
        if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save scene changes before building.");
        Status("building", "Building Web player to Builds/Web. Editor requests may wait until the build finishes.");
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        PlayerSettings.WebGL.template = "PROJECT:Astra";
        // A rebuild must not reuse cached data with a different WASM binary.
        PlayerSettings.WebGL.nameFilesAsHashes = true;
        PlayerSettings.defaultWebScreenWidth = 1280;
        PlayerSettings.defaultWebScreenHeight = 720;
        PlayerSettings.SetIl2CppCodeGeneration(UnityEditor.Build.NamedBuildTarget.WebGL, UnityEditor.Build.Il2CppCodeGeneration.OptimizeSize);
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = "Builds/Web",
            target = BuildTarget.WebGL,
            options = BuildOptions.Development
        });
        if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException($"Web build {report.summary.result}; {report.summary.totalErrors} errors.");
        Status("built", $"Builds/Web/index.html; {report.summary.totalSize} bytes; {report.summary.totalTime.TotalSeconds:0}s.");
    }
}
