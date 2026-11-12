using System.Collections.Generic;
using System.Linq;
using Il2Cpp;
using Il2CppGame.Spawning;
using POGContentLib.Core;
using POGContentLib.Shops.Model;
using Unity.Netcode;
using UnityEngine;

namespace POGContentLib.Shops
{
    /// <summary>
    /// How shop things in a loaded scene are named and found (docs/design/shops.md §6, §12.6).
    ///
    /// Vanilla slot id: "vanilla:&lt;scene&gt;/&lt;object path&gt;", e.g.
    /// "vanilla:PitOfGoblin_Lobby/StarterShop/Game.Spawning.ShopLocation Item_Bread". When two siblings
    /// share a name, the second and later get "#2", "#3"… in sibling order.
    /// Vanilla seller id: "vanilla:&lt;npc flag&gt;" (NpcSpawner.m_npcFlag, a designer-given unique name,
    /// e.g. "vanilla:NPC_ShopKeeper_Lobby_Hub_Curiosa"); a seller without a flag falls back to its path.
    /// Mod slots spawned by the library are named "POGShopSlot|&lt;full id&gt;".
    /// </summary>
    public static class ShopScene
    {
        public const string ModSlotPrefix = "POGShopSlot|";

        public static string SlotId(ShopLocation slot)
        {
            if (slot == null) return null;
            var name = slot.gameObject.name;
            if (name.StartsWith(ModSlotPrefix)) return name.Substring(ModSlotPrefix.Length).Replace("(Clone)", "");
            return ShopIds.VanillaSlot(slot.gameObject.scene.name, PathOf(slot.transform));
        }

        public static string SellerId(NpcSpawner seller)
        {
            if (seller == null) return null;
            if (!string.IsNullOrEmpty(seller.m_npcFlag)) return ShopIds.Vanilla + ":" + seller.m_npcFlag;
            return ShopIds.VanillaSlot(seller.gameObject.scene.name, PathOf(seller.transform));
        }

        /// <summary>Object path from the scene root with "#n" on repeated sibling names.</summary>
        public static string PathOf(Transform t)
        {
            var parts = new List<string>();
            for (var x = t; x != null; x = x.parent)
            {
                string n = x.name;
                var parent = x.parent;
                int same = 0, mine = 0;
                if (parent != null)
                {
                    for (int i = 0; i < parent.childCount; i++)
                    {
                        var c = parent.GetChild(i);
                        if (c.name != n) continue;
                        same++;
                        if (c == x) mine = same;
                    }
                }
                else
                {
                    foreach (var root in x.gameObject.scene.GetRootGameObjects())
                    {
                        if (root.name != n) continue;
                        same++;
                        if (root.transform == x) mine = same;
                    }
                }
                parts.Add(mine > 1 ? n + "#" + mine : n);
            }
            parts.Reverse();
            return string.Join("/", parts);
        }

        /// <summary>Our own templates live under inactive holders; they are not part of any scene's shops.</summary>
        public static bool IsTemplate(Transform t) => t != null && t.root != null && t.root.name.StartsWith("POGContentLib_");

        public static IEnumerable<ShopLocation> SlotsInLoadedScenes() =>
            Object.FindObjectsOfType<ShopLocation>(true).Where(s => s != null && !IsTemplate(s.transform));

        public static IEnumerable<NpcSpawner> SellersInLoadedScenes() =>
            Object.FindObjectsOfType<NpcSpawner>(true).Where(s => s != null && !IsTemplate(s.transform) && s.m_itemsForSale != null && s.m_itemsForSale.Length > 0);

        public static NpcSpawner FindSeller(string sellerId) =>
            string.IsNullOrEmpty(sellerId) ? null : Object.FindObjectsOfType<NpcSpawner>(true).FirstOrDefault(s => s != null && !IsTemplate(s.transform) && SellerId(s) == sellerId);

        public static ShopLocation FindSlot(string slotId) =>
            string.IsNullOrEmpty(slotId) ? null : SlotsInLoadedScenes().FirstOrDefault(s => SlotId(s) == slotId);

        /// <summary>The transform an id names: a seller, a slot, or (fallback) a scene object path.</summary>
        public static Transform FindAnchor(string anchorId)
        {
            var seller = FindSeller(anchorId);
            if (seller != null) return seller.transform;
            var slot = FindSlot(anchorId);
            if (slot != null) return slot.transform;
            return null;
        }

        /// <summary>What the game itself would sell in this slot, as catalog entries.</summary>
        public static List<VanillaEntry> VanillaEntriesOf(ShopLocation slot)
        {
            var list = new List<VanillaEntry>();
            if (slot.m_networkPrefab != null)
                list.Add(new VanillaEntry(ShopIds.VanillaItem(CleanName(slot.m_networkPrefab.name)), 1));
            else if (slot.m_lootTable != null && slot.m_lootTable.Items != null)
            {
                var counts = new Dictionary<string, int>();
                foreach (var it in slot.m_lootTable.Items)
                {
                    if (it == null) continue;
                    var id = ShopIds.VanillaItem(CleanName(it.gameObject.name));
                    counts[id] = counts.TryGetValue(id, out var c) ? c + 1 : 1;
                }
                foreach (var kv in counts) list.Add(new VanillaEntry(kv.Key, kv.Value));
            }
            return list;
        }

        /// <summary>
        /// The NetworkObject prefab an item id stands for: a registered mod item ("Mod:Item") or a loaded
        /// game item prefab ("vanilla:Item_Key"). Null when it is not loaded (mod missing, typo).
        /// </summary>
        public static NetworkObject ResolveItemPrefab(string itemId)
        {
            if (!ShopIds.TrySplit(itemId, out var owner, out var local)) return null;
            if (owner == ShopIds.Vanilla)
            {
                var item = GameAssets.FindItemPrefab(local);
                return item != null ? item.GetComponent<NetworkObject>() : null;
            }
            // By item id rather than by hash: a hash collision re-salts the hash, the id never changes.
            var direct = CoreServices.Content.GetPrefabByHash(ContentRegistry.ComputeHash(owner, local));
            if (IsItem(direct, itemId)) return direct;
            foreach (var p in CoreServices.Content.AllPrefabs)
                if (IsItem(p, itemId)) return p;
            return null;
        }

        private static bool IsItem(NetworkObject p, string itemId)
        {
            if (p == null) return false;
            var item = p.GetComponent<InventoryItem>();
            return item != null && item.m_itemID == itemId;
        }

        public static string CleanName(string n) => n == null ? null : n.Replace("(Clone)", "").Trim();
    }
}
