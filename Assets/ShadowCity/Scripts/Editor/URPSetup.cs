#if UNITY_EDITOR
// ============================================================================
// SHADOW CITY — Editor/URPSetup.cs
// One-click URP installation & activation for projects created from the
// plain "3D" template (when the "3D URP" template is unavailable in Hub).
//   Menu: Shadow City → 1. Install URP Package
//         Shadow City → 2. Activate URP Pipeline   (after install completes)
// Safe to run on a project that already has URP — it just activates it.
// ============================================================================
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadowCity.EditorTools
{
    public static class URPSetup
    {
        static AddRequest addRequest;

        [MenuItem("Shadow City/1. Install URP Package")]
        public static void InstallURP()
        {
            // Correct version for Unity 2022.3 LTS
            addRequest = Client.Add("com.unity.render-pipelines.universal@14.0.11");
            EditorApplication.update += Progress;
            Debug.Log("[ShadowCity] Installing URP… watch the progress bar (bottom-right).");
        }

        static void Progress()
        {
            if (addRequest == null || !addRequest.IsCompleted) return;
            EditorApplication.update -= Progress;
            if (addRequest.Status == StatusCode.Success)
            {
                Debug.Log("[ShadowCity] URP installed ✔ — now run: Shadow City → 2. Activate URP Pipeline");
                EditorUtility.DisplayDialog("Shadow City",
                    "URP installed.\n\nNow run:\nShadow City → 2. Activate URP Pipeline", "OK");
            }
            else
            {
                Debug.LogError("[ShadowCity] URP install failed: " + addRequest.Error?.message);
            }
        }

        [MenuItem("Shadow City/2. Activate URP Pipeline")]
        public static void ActivateURP()
        {
            // Create the URP asset (+ its renderer) via reflection so this file
            // compiles even before the URP package exists in the project.
            var urpAssetType = System.Type.GetType(
                "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset, Unity.RenderPipelines.Universal.Runtime");
            if (urpAssetType == null)
            {
                EditorUtility.DisplayDialog("Shadow City",
                    "URP is not installed yet.\nRun: Shadow City → 1. Install URP Package first,\n" +
                    "wait for it to finish, then run this again.", "OK");
                return;
            }

            var rendererDataType = System.Type.GetType(
                "UnityEngine.Rendering.Universal.UniversalRendererData, Unity.RenderPipelines.Universal.Runtime");
            var rendererData = ScriptableObject.CreateInstance(rendererDataType);
            AssetDatabase.CreateAsset(rendererData, "Assets/ShadowCity/URP_Renderer.asset");

            var create = urpAssetType.GetMethod("Create",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public,
                null, new[] { rendererDataType }, null);
            var pipelineAsset = (RenderPipelineAsset)create.Invoke(null, new[] { (object)rendererData });
            AssetDatabase.CreateAsset(pipelineAsset, "Assets/ShadowCity/URP_Pipeline.asset");

            GraphicsSettings.defaultRenderPipeline = pipelineAsset;
            QualitySettings.renderPipeline = pipelineAsset;

            // Mobile-friendly defaults via serialized props (shadow distance etc.)
            var so = new SerializedObject(pipelineAsset);
            var shadowDist = so.FindProperty("m_ShadowDistance");
            if (shadowDist != null) { shadowDist.floatValue = 90f; so.ApplyModifiedProperties(); }

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Shadow City",
                "URP activated ✔\n\nNext: Shadow City → Setup Scene (run once), then press Play.",
                "OK");
            Debug.Log("[ShadowCity] URP pipeline activated ✔");
        }
    }
}
#endif
