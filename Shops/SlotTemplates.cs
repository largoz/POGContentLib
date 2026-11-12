using System.Collections.Generic;
using System.Linq;
using Il2CppGame.Spawning;
using MelonLoader;
using POGContentLib.Core;
using POGContentLib.Shops.Model;
using Unity.Netcode;
using UnityEngine;

namespace POGContentLib.Shops
{
    /// <summary>
    /// One network prefab per mod slot, built identically on every peer from the same shop files, so a
    /// slot the host spawns arrives on clients with its price-tag offset already in place (child
    /// transforms are not synchronised). Hash = MD5("POGShopSlot", full slot id), like item hashes.
    ///
    /// Built from a vanilla slot (a ShopLocation that belongs to a loaded prefab, e.g. a dungeon merchant)
    /// during SceneLoaded, i.e. before StartHost/StartClient — the only time ForceSamePrefabs allows
    /// registration. Templates sit ACTIVE under one INACTIVE holder: they never run themselves, but a copy
    /// made by Instantiate (ours on the host, NGO's on a client) is a root object and is active at once.
    /// </summary>
    internal static class SlotTemplates
    {
        public const string HashNamespace = "POGShopSlot";
        private static GameObject _holder;
        private static readonly Dictionary<string, ShopLocation> _built = new Dictionary<string, ShopLocation>();

        public static ShopLocation Get(string fullSlotId) => _built.TryGetValue(fullSlotId, out var t) ? t : null;

        /// <summary>The vanilla slot new slots are copied from (null before it is loaded).</summary>
        internal static ShopLocation Donor => FindDonor(null);
        public static int Count => _built.Count;

        public static void BuildPending(ShopCatalog catalog)
        {
            if (catalog == null) return;
            var pending = catalog.ModSlots.Values.Where(s => !_built.ContainsKey(s.FullId)).ToList();
            if (pending.Count == 0) return;
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening)
            {
                MelonLogger.Warning($"[POGContentLib] [Shops] {pending.Count} slot template(s) not built before the session started; they appear from the next session.");
                return;
            }

            var donor = FindDonor(null);
            if (donor == null)
            {
                MelonLogger.Msg("[POGContentLib] [Shops] No vanilla shop slot in memory yet to copy for new slots (will retry on the next scene).");
                return;
            }
            EnsureHolder();
            foreach (var s in pending)
            {
                var src = FindDonor(s.Def.Template) ?? donor;
                var t = Build(src, s);
                if (t != null) _built[s.FullId] = t;
            }
            MelonLogger.Msg($"[POGContentLib] [Shops] Slot templates ready: {_built.Count} (copied from '{donor.gameObject.name}').");
        }

        /// <summary>A ShopLocation from a loaded prefab asset (not a scene instance), preferring a named one.</summary>
        private static ShopLocation FindDonor(string preferredName)
        {
            var all = Resources.FindObjectsOfTypeAll<ShopLocation>()
                .Where(s => s != null && !s.gameObject.scene.IsValid() && s.GetComponent<NetworkObject>() != null && s.m_priceTagText != null && s.m_itemHolder != null)
                .ToList();
            if (!string.IsNullOrEmpty(preferredName))
            {
                string local = ShopIds.TrySplit(preferredName, out _, out var l) ? l : preferredName;
                var named = all.FirstOrDefault(s => s.gameObject.name == local || s.gameObject.name.EndsWith(local));
                if (named != null) return named;
            }
            return all.FirstOrDefault();
        }

        /// <summary>Inactive parent of every shop template (slots and merchants).</summary>
        internal static GameObject Holder { get { EnsureHolder(); return _holder; } }

        private static void EnsureHolder()
        {
            if (_holder != null) return;
            _holder = new GameObject("POGContentLib_ShopTemplates");
            _holder.SetActive(false);
            Object.DontDestroyOnLoad(_holder);
            _holder.hideFlags = HideFlags.HideAndDontSave;
        }

        private static ShopLocation Build(ShopLocation donor, ShopCatalog.ModSlot s)
        {
            var go = Object.Instantiate(donor.gameObject, _holder.transform);
            go.name = ShopScene.ModSlotPrefix + s.FullId;
            go.SetActive(true);   // active itself, inactive through the holder
            var slot = go.GetComponent<ShopLocation>();
            var net = go.GetComponent<NetworkObject>();
            slot.m_networkPrefab = null;
            slot.m_lootTable = null;
            slot.m_shopKeeper = null;
            net.GlobalObjectIdHash = ContentRegistry.ComputeHash(HashNamespace, s.FullId);

            var tag = s.Def.PriceTag;
            if (tag != null && slot.m_priceTagText != null)
            {
                var tagRoot = TopChildOf(slot.m_priceTagText.transform, go.transform);
                if (tag.Pos != null) tagRoot.localPosition = new Vector3(tag.Pos[0], tag.Pos[1], tag.Pos[2]);
                if (tag.Rot != null) tagRoot.localEulerAngles = new Vector3(tag.Rot[0], tag.Rot[1], tag.Rot[2]);
            }
            CoreServices.Content.RegisterPrefab(net);
            return slot;
        }

        /// <summary>The direct child of <paramref name="root"/> that contains <paramref name="t"/>.</summary>
        internal static Transform TopChildOf(Transform t, Transform root)
        {
            var x = t;
            while (x.parent != null && x.parent != root) x = x.parent;
            return x.parent == root ? x : t;
        }
    }
}
