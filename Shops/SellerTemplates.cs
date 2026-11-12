using System;
using System.Collections.Generic;
using System.Linq;
using Il2Cpp;
using Il2CppGame.Spawning;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;
using POGContentLib.Items;
using POGContentLib.Shops.Model;
using UnityEngine;

namespace POGContentLib.Shops
{
    /// <summary>
    /// Dungeon merchants from shop files ("sellers") as VARIANTS of a vanilla merchant preset.
    ///
    /// The entrance cave draws one entry of <see cref="MerchantList"/> uniformly with the dungeon seed
    /// (_research/SHOPS_NATIVE.md Q7). A seller of weight w adds w more references to its preset to that list,
    /// so the preset comes up w more times out of the new total. When a preset instance spawns, the host picks
    /// whose variant it is — the vanilla merchant (weight = how often the preset was in the list originally) or
    /// one of the mod sellers built on it (weight w each) — and stocks its slots from that variant. Net chance of
    /// a mod merchant: w over the list total, exactly as if it were w entries of its own.
    ///
    /// No new network prefab: the presets are only loaded with the dungeon scene, after a session has started,
    /// when ForceSamePrefabs no longer allows registering one (found 2026-11-12). The variant keeps the preset's
    /// NPC, look and death flag. The list is changed identically on every peer.
    /// </summary>
    internal static class SellerTemplates
    {
        public const string MerchantList = "PropList_EntranceCave_MerchantOrAdventurer";
        public const string Vanilla = "vanilla";

        private static readonly HashSet<IntPtr> _injected = new HashSet<IntPtr>();
        private static readonly Dictionary<string, int> _originalCopies = new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly Dictionary<IntPtr, string> _variantOf = new Dictionary<IntPtr, string>();

        public static int Copies(double weight) => weight <= 0 ? 0 : Mathf.Max(1, Mathf.RoundToInt((float)weight));

        /// <summary>Add each seller's preset to the entrance list, weight = extra copies. Every peer, idempotent.</summary>
        public static void BuildPending(ShopCatalog catalog)
        {
            if (catalog == null || catalog.Sellers.Count == 0) return;
            foreach (var list in Resources.FindObjectsOfTypeAll<PropListAsset>())
            {
                if (list == null || list.name != MerchantList || list.PropList == null || _injected.Contains(list.Pointer)) continue;
                var items = list.PropList.ToList();
                int before = items.Count;
                var added = new List<string>();
                foreach (var s in catalog.Sellers)
                {
                    var preset = items.Take(before).FirstOrDefault(g => g != null && g.name == s.Def.Preset);
                    if (preset == null)
                    {
                        MelonLogger.Warning($"[POGContentLib] [Shops] merchant {s.FullId}: preset '{s.Def.Preset}' is not in {MerchantList} " +
                                            $"(it has: {string.Join(", ", items.Take(before).Where(g => g != null).Select(g => g.name).Distinct())})");
                        continue;
                    }
                    _originalCopies[preset.name] = items.Take(before).Count(g => g != null && g.name == preset.name);
                    int n = Copies(s.Def.Weight);
                    for (int i = 0; i < n; i++) items.Add(preset);
                    added.Add($"{s.FullId} on {preset.name} x{n}");
                }
                var arr = new Il2CppReferenceArray<GameObject>(items.Count);
                for (int i = 0; i < items.Count; i++) arr[i] = items[i];
                list.PropList = arr;
                _injected.Add(list.Pointer);
                MelonLogger.Msg($"[POGContentLib] [Shops] {MerchantList}: {before} -> {items.Count} entries ({string.Join(", ", added)}).");
            }
        }

        /// <summary>
        /// Host: the slot id to stock a merchant slot by, when the slot belongs to a preset some mod seller is built
        /// on — "Mod:seller#n" for a mod variant; null otherwise (vanilla variant or not a merchant preset).
        /// </summary>
        public static string VariantSlotId(ShopLocation slot, ShopCatalog catalog, System.Random rng)
        {
            if (catalog == null || catalog.Sellers.Count == 0) return null;
            Transform root = null;
            for (var x = slot.transform; x != null; x = x.parent)
                if (catalog.Sellers.Any(s => s.Def.Preset == ShopScene.CleanName(x.name))) { root = x; break; }
            if (root == null) return null;
            string preset = ShopScene.CleanName(root.name);

            if (!_variantOf.TryGetValue(root.gameObject.Pointer, out var variant))
            {
                var options = new List<(string id, double w)> { (Vanilla, _originalCopies.TryGetValue(preset, out var c) ? c : 1) };
                options.AddRange(catalog.Sellers.Where(s => s.Def.Preset == preset).Select(s => (s.FullId, (double)Copies(s.Def.Weight))));
                double total = options.Sum(o => o.w), target = rng.NextDouble() * total, acc = 0;
                variant = options[options.Count - 1].id;
                foreach (var o in options) { acc += o.w; if (target < acc) { variant = o.id; break; } }
                _variantOf[root.gameObject.Pointer] = variant;
                MelonLogger.Msg($"[POGContentLib] [Shops] dungeon merchant {preset} is {(variant == Vanilla ? "the vanilla one" : variant)} " +
                                $"({string.Join(", ", options.Select(o => $"{o.id} {100 * o.w / total:0.#}%"))})");
            }
            if (variant == Vanilla) return null;

            var spawner = root.GetComponentInChildren<NpcSpawner>(true);
            int n = 0;
            if (spawner != null && spawner.m_itemsForSale != null)
                for (int i = 0; i < spawner.m_itemsForSale.Length; i++)
                    if (spawner.m_itemsForSale[i] != null && spawner.m_itemsForSale[i].Pointer == slot.Pointer) { n = i + 1; break; }
            if (n == 0) n = Array.IndexOf(root.GetComponentsInChildren<ShopLocation>(true).Select(x => x.Pointer).ToArray(), slot.Pointer) + 1;
            return ShopCatalog.SellerSlotId(variant, n);
        }

        public static void Reset() => _variantOf.Clear();

        /// <summary>Apply the shop files' standing points to the item templates (every peer).</summary>
        public static void ApplyItemDisplays(ShopCatalog catalog)
        {
            if (catalog == null) return;
            foreach (var kv in catalog.ItemDisplays)
            {
                var prefab = ShopScene.ResolveItemPrefab(kv.Key);
                var item = prefab != null ? prefab.GetComponent<InventoryItem>() : null;
                if (item == null) continue;   // not built yet; next scene load
                var st = kv.Value.standing;
                StandingPoints.Ensure(item,
                    st?.Pos != null ? new Vector3(st.Pos[0], st.Pos[1], st.Pos[2]) : (Vector3?)null,
                    st?.Rot != null ? new Vector3(st.Rot[0], st.Rot[1], st.Rot[2]) : (Vector3?)null);
            }
        }
    }
}
