using AstraExpress;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

// Bind the existing CC0 model in the build copy, without rewriting the user's scene.
public sealed class AstraBotSceneAssets : IProcessSceneWithReport
{
    public int callbackOrder => 0;
    public void OnProcessScene(Scene scene, BuildReport report)
    {
        foreach (var root in scene.GetRootGameObjects())
        foreach (var game in root.GetComponentsInChildren<AstraGame>(true))
        {
            if (game.AstraBotModel != null) continue;
            game.AstraBotModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Kenny/kenney_space-kit/Models/FBX format/craft_miner.fbx");
            if (game.AstraBotModel == null) throw new BuildFailedException("AstraBot's Kenney mining craft is missing.");
        }
    }
}
