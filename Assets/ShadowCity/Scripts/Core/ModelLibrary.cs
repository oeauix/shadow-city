// ============================================================================
// SHADOW CITY — Core/ModelLibrary.cs
// AI-asset swap layer: if a prefab exists at Resources/Models/<id>, entities
// use it as their visual; otherwise they fall back to procedural primitives.
// Import path: add glTFast package -> GLBs in Assets/ShadowCity/Models import
// automatically -> drag each into Assets/ShadowCity/Resources/Models/.
// No gameplay code changes needed on swap.
// ============================================================================
using UnityEngine;

namespace ShadowCity
{
    public static class ModelLibrary
    {
        /// <summary>
        /// Try to instantiate an AI model as a child of parent.
        /// Returns null if the model isn't present (procedural fallback).
        /// </summary>
        public static GameObject TrySpawn(string id, Transform parent)
        {
            var prefab = Resources.Load<GameObject>("Models/" + id);
            if (prefab == null) return null;
            var go = Object.Instantiate(prefab, parent, false);
            go.name = "model_" + id;
            // strip any colliders the importer added — gameplay owns collision
            foreach (var c in go.GetComponentsInChildren<Collider>(true))
                Object.Destroy(c);
            return go;
        }

        public static bool Has(string id) =>
            Resources.Load<GameObject>("Models/" + id) != null;
    }
}
