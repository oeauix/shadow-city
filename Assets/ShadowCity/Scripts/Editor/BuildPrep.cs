#if UNITY_EDITOR
// ============================================================================
// SHADOW CITY — Editor/BuildPrep.cs
// Runs automatically before every player build (local AND GameCI/CI).
// Sets the platform settings that would otherwise require hand-edited
// ProjectSettings.asset (which this repo intentionally does not pin):
//   • WebGL: gzip + decompression fallback → works on GitHub Pages
//            (default Brotli + no fallback = infinite loading on Pages)
//   • Android: package id, min SDK, landscape orientation
//   • Identity: product/company name
// ============================================================================
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ShadowCity.EditorTools
{
    public class BuildPrep : IPreprocessBuildWithReport
    {
        public int callbackOrder => -100;

        public void OnPreprocessBuild(BuildReport report)
        {
            PlayerSettings.companyName = "ShadowCity";
            PlayerSettings.productName = "Shadow City";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;

            var target = report.summary.platform;

            if (target == BuildTarget.WebGL)
            {
                // GitHub Pages serves .br/.gz without Content-Encoding headers.
                // Gzip + decompression fallback = self-decompressing loader that
                // works on ANY static host.
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
                PlayerSettings.WebGL.decompressionFallback = true;
                PlayerSettings.WebGL.dataCaching = true;
                PlayerSettings.runInBackground = true;
                Debug.Log("[ShadowCity] WebGL: gzip + decompression fallback set.");
            }

            if (target == BuildTarget.Android)
            {
                PlayerSettings.SetApplicationIdentifier(
                    BuildTargetGroup.Android, "com.shadowcity.game");
                PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel22;
                // NOTE: scripting backend left at default (Mono/ARMv7) for fast
                // CI iteration. Before store submission switch to IL2CPP+ARM64
                // via "Shadow City → Configure Android Build".
                Debug.Log("[ShadowCity] Android: id=com.shadowcity.game, minSdk=22.");
            }
        }
    }
}
#endif
