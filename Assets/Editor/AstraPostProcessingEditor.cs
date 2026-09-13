using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class AstraPostProcessingEditor
{
    public const string ProfilePath = "Assets/Settings/AstraPostProcessing.asset";
    private const string VolumeName = "Colony Post Processing";

    [MenuItem("Astra Express/Apply lightweight post-processing")]
    public static void Apply()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before configuring post-processing.");
        if (scene.path != "Assets/Scenes/AstraExpress.unity") throw new InvalidOperationException("Open AstraExpress.unity before configuring post-processing.");
        if (scene.isDirty) throw new InvalidOperationException("Save scene changes before configuring post-processing.");
        var camera = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true)).SingleOrDefault(candidate => candidate.CompareTag("MainCamera"));
        if (camera == null) throw new InvalidOperationException("The colony scene needs one Main Camera.");
        Configure(scene, camera);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save post-processing scene changes.");
        Validate();
    }

    public static void Configure(Scene scene, Camera camera)
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "AstraPostProcessing";
            AssetDatabase.CreateAsset(profile, ProfilePath);
            var bloom = Add<Bloom>(profile);
            bloom.threshold.Override(1f);
            bloom.intensity.Override(0.18f);
            bloom.scatter.Override(0.45f);
            bloom.highQualityFiltering.Override(false);
            bloom.downscale.Override(BloomDownscaleMode.Quarter);
            bloom.maxIterations.Override(3);
            var tonemapping = Add<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.Neutral);
            var grading = Add<ColorAdjustments>(profile);
            grading.postExposure.Override(0f);
            grading.contrast.Override(6f);
            grading.saturation.Override(2f);
            var vignette = Add<Vignette>(profile);
            vignette.intensity.Override(0.1f);
            vignette.smoothness.Override(0.65f);
            EditorUtility.SetDirty(profile);
            foreach (var component in profile.components) EditorUtility.SetDirty(component);
            AssetDatabase.SaveAssets();
        }

        var volumeObject = scene.GetRootGameObjects().SingleOrDefault(root => root.name == VolumeName);
        if (volumeObject == null)
        {
            volumeObject = new GameObject(VolumeName);
            SceneManager.MoveGameObjectToScene(volumeObject, scene);
            Undo.RegisterCreatedObjectUndo(volumeObject, "Add colony post-processing");
        }
        var volume = volumeObject.GetComponent<Volume>() ?? Undo.AddComponent<Volume>(volumeObject);
        Undo.RecordObject(volume, "Configure colony post-processing");
        volume.isGlobal = true;
        volume.priority = 10f;
        volume.weight = 1f;
        volume.sharedProfile = profile;
        var cameraData = camera.GetComponent<UniversalAdditionalCameraData>() ?? Undo.AddComponent<UniversalAdditionalCameraData>(camera.gameObject);
        Undo.RecordObject(cameraData, "Enable colony post-processing");
        cameraData.renderPostProcessing = true;
        cameraData.volumeLayerMask |= 1 << volumeObject.layer;
        cameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
        cameraData.dithering = true;
    }

    private static T Add<T>(VolumeProfile profile) where T : VolumeComponent
    {
        var component = profile.Add<T>(false);
        AssetDatabase.AddObjectToAsset(component, profile);
        return component;
    }

    [MenuItem("Astra Express/Validate post-processing")]
    public static void Validate()
    {
        var camera = Camera.main;
        var cameraData = camera != null ? camera.GetComponent<UniversalAdditionalCameraData>() : null;
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        var volumes = UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsSortMode.None).Where(volume => volume.sharedProfile == profile).ToArray();
        if (profile == null || cameraData == null || !cameraData.renderPostProcessing || volumes.Length != 1 || !volumes[0].isGlobal || volumes[0].weight <= 0f || (cameraData.volumeLayerMask.value & (1 << volumes[0].gameObject.layer)) == 0)
            throw new InvalidOperationException("Camera and global post-processing Volume are not connected.");
        if (!profile.TryGet<Bloom>(out var bloom) || !bloom.active || !bloom.IsActive() || !profile.TryGet<Tonemapping>(out _) || !profile.TryGet<ColorAdjustments>(out _) || !profile.TryGet<Vignette>(out _))
            throw new InvalidOperationException("The colony post-processing profile is incomplete.");
        Debug.Log("[Astra Express] Post-processing verified: global profile, bloom, neutral tonemapping, color grading, vignette, and camera enabled.");
    }
}
