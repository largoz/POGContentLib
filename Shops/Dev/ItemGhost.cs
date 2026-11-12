using Il2Cpp;
using Il2CppAnimations;
using UnityEngine;

namespace POGContentLib.Shops.Dev
{
    /// <summary>
    /// A render-only copy of an item prefab (meshes and materials, no scripts, no NetworkObject) for the
    /// dev tools' previews. Instantiating the real prefab outside the network would run its NetworkBehaviours
    /// unspawned; this cannot do anything but be drawn.
    /// </summary>
    internal static class ItemGhost
    {
        public static GameObject Create(InventoryItem prefab, string name)
        {
            var root = new GameObject(name);
            if (prefab == null) return root;
            var origin = prefab.transform;
            foreach (var mr in prefab.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (mr == null || mr.gameObject.name.Contains("Outline")) continue;
                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var part = new GameObject(mr.gameObject.name);
                part.transform.SetParent(root.transform, false);
                // The part's pose relative to the item root.
                part.transform.localPosition = origin.InverseTransformPoint(mr.transform.position);
                part.transform.localRotation = Quaternion.Inverse(origin.rotation) * mr.transform.rotation;
                part.transform.localScale = mr.transform.lossyScale;
                part.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                part.AddComponent<MeshRenderer>().sharedMaterials = mr.sharedMaterials;
            }
            return root;
        }

        /// <summary>
        /// The item's shop-table grip ("TableGrip", GripSlot.ShopTable) as a pose relative to the item root,
        /// or false when the item has none.
        /// </summary>
        public static bool TryGetTableGrip(InventoryItem prefab, out Vector3 localPos, out Quaternion localRot)
        {
            localPos = Vector3.zero; localRot = Quaternion.identity;
            if (prefab == null) return false;
            foreach (var g in prefab.GetComponentsInChildren<IKGrip>(true))
            {
                if (g == null || g.m_slotType != GripSlot.ShopTable) continue;
                var t = g.m_gripRoot != null ? g.m_gripRoot : g.transform;
                localPos = prefab.transform.InverseTransformPoint(t.position);
                localRot = Quaternion.Inverse(prefab.transform.rotation) * t.rotation;
                return true;
            }
            return false;
        }
    }
}
