#if UNITY_EDITOR
// ============================================================================
// SHADOW CITY — Editor/ProjectBootstrap.cs
// One-click setup: menu "Shadow City → Setup Scene" creates the playable
// scene; "Shadow City → Tripo Importer" opens the AI asset pipeline window.
// ============================================================================
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShadowCity.EditorTools
{
    public static class ProjectBootstrap
    {
        [MenuItem("Shadow City/Setup Scene (run once)")]
        public static void SetupScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();

            new GameObject("Bootstrap").AddComponent<Bootstrap>();

            System.IO.Directory.CreateDirectory("Assets/ShadowCity/Scenes");
            EditorSceneManager.SaveScene(scene, "Assets/ShadowCity/Scenes/Main.unity");

            // add to build settings
            var scenes = new EditorBuildSettingsScene[]
            { new("Assets/ShadowCity/Scenes/Main.unity", true) };
            EditorBuildSettings.scenes = scenes;

            // sensible defaults
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.companyName = "ShadowCity";
            PlayerSettings.productName = "Shadow City";

            EditorUtility.DisplayDialog("Shadow City",
                "Scene created & saved.\nPress ▶ Play to run the game.", "OK");
        }

        [MenuItem("Shadow City/Configure Android Build")]
        public static void ConfigureAndroid()
        {
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel22;
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.shadowcity.game");
            EditorUtility.DisplayDialog("Shadow City", "Android settings applied (IL2CPP, ARM64+ARMv7).", "OK");
        }
    }
}
#endif
