using System;
using System.Collections.Generic;
using System.Linq;
using Il2Cpp;
using MelonLoader;
using POGContentLib.Core;
using POGContentLib.Shops.Model;
using Unity.Netcode;
using UnityEngine;

namespace POGContentLib.Shops
{
    /// <summary>
    /// Mistakes in shop files that would otherwise do nothing without a word: an item that is not loaded,
    /// an item a shop would give away (ShopCost 0), a vanilla slot id that matches nothing in its scene.
    /// Each is logged once per game run, with the closest ids that do exist.
    /// </summary>
    internal static class ShopChecks
    {
        /// <summary>How long after a scene's first slot spawned its rules are checked against its slots.</summary>
        public const float SceneCheckDelaySeconds = 5f;

        private const string Tag = "[POGContentLib] [Shops]";
        private static readonly HashSet<string> _reported = new HashSet<string>(StringComparer.Ordinal);
        private static bool Once(string key) => _reported.Add(key);

        /// <summary>
        /// Before a session: every mod item a rule names is registered and has a price. Vanilla items are
        /// checked when a slot uses them (their prefabs are not all in memory this early).
        /// </summary>
        public static void CheckModItems(ShopCatalog catalog)
        {
            if (catalog == null) return;
            foreach (var g in catalog.ItemReferences.GroupBy(r => r.Item))
            {
                if (ShopIds.IsVanilla(g.Key)) continue;
                string usedBy = Describe(g);
                var prefab = ShopScene.ResolveItemPrefab(g.Key);
                if (prefab == null) NotLoaded(g.Key, usedBy);
                else CheckPrice(g.Key, prefab, usedBy);
            }
        }

        private static string Describe(IEnumerable<ItemReference> refs)
        {
            var list = refs.ToList();
            string s = string.Join("; ", list.Take(3).Select(r => r.ToString()));
            return list.Count > 3 ? s + $"; +{list.Count - 3} more" : s;
        }

        /// <summary>An item a rule names is not loaded: its mod is not installed, or the id is misspelt.</summary>
        public static void NotLoaded(string item, string usedBy)
        {
            if (!Once("item:" + item)) return;
            // Registered but not built is a different problem from a missing mod or a typo (2026-11-14: every item,
            // when the shells were not in memory): say which.
            var items = POGContentLib.Items.ItemsPlugin.Instance;
            if (items != null && items.IsRegistered(item))
            {
                MelonLogger.Warning($"{Tag} item '{item}' ({usedBy}) is registered but its template was not built " +
                                    "(see the [POGContentLib] lines about its shell above). Shops leave it out.");
                return;
            }
            var near = NearestIds.Of(item, KnownItemIds());
            MelonLogger.Warning($"{Tag} item '{item}' ({usedBy}) is not loaded: its mod is not installed, or the id is misspelt. " +
                                $"Shops leave it out.{(near.Count > 0 ? " Closest loaded ids: " + string.Join(", ", near) : "")}");
        }

        /// <summary>A shop takes ShopCost (× the reputation factor) as the price: 0 gives the item away.</summary>
        public static void CheckPrice(string item, NetworkObject prefab, string usedBy)
        {
            var inv = prefab != null ? prefab.GetComponent<InventoryItem>() : null;
            if (inv == null || inv.m_shopCost > 0 || !Once("free:" + item)) return;
            MelonLogger.Warning($"{Tag} item '{item}' ({usedBy}) has ShopCost {inv.m_shopCost}: a shop sells it for free. " +
                                (ShopIds.IsVanilla(item) ? "The game gives this item no price of its own." : "Set ShopCost in its ModItemDefinition."));
        }

        /// <summary>
        /// Rules aimed at vanilla slots of <paramref name="scene"/> whose id matches no slot there. Called a few
        /// seconds after the scene's slots spawned (<see cref="SceneCheckDelaySeconds"/>).
        /// </summary>
        public static void ReportUnmatchedSlots(string scene, ShopCatalog catalog)
        {
            if (catalog == null) return;
            var targets = VanillaTargetsIn(scene, catalog);
            if (targets.Count == 0) return;
            var here = ShopScene.SlotsInLoadedScenes()
                .Where(s => s.gameObject.scene.name == scene)
                .Select(ShopScene.SlotId).Where(id => id != null).Distinct().ToList();
            var hereSet = new HashSet<string>(here, StringComparer.Ordinal);
            foreach (var id in targets)
            {
                if (hereSet.Contains(id) || !Once("slot:" + id)) continue;
                var near = NearestIds.Of(id, here);
                MelonLogger.Warning($"{Tag} {string.Join(", ", catalog.ModsTargeting(id))}: slot '{id}' is not in {scene} " +
                                    $"({SceneCheckDelaySeconds:0} s after its slots spawned), so those rules do nothing on this visit. " +
                                    (near.Count > 0 ? $"Closest slot ids here: {string.Join(" | ", near)}. " : "The scene has no shop slots. ") +
                                    "A slot of a seller the game draws at random can be absent on some visits.");
            }
        }

        public static List<string> VanillaTargetsIn(string scene, ShopCatalog catalog) =>
            catalog.TargetedSlots.Where(id => ShopIds.SceneOfVanilla(id) == scene).OrderBy(id => id, StringComparer.Ordinal).ToList();

        // Mod items by their id, game items as "vanilla:<prefab name>" (the forms a shop file uses).
        private static IEnumerable<string> KnownItemIds()
        {
            var ids = new List<string>();
            foreach (var p in CoreServices.Content.AllPrefabs)
            {
                var inv = p != null ? p.GetComponent<InventoryItem>() : null;
                if (inv != null && !string.IsNullOrEmpty(inv.m_itemID)) ids.Add(inv.m_itemID);
            }
            foreach (var inv in Resources.FindObjectsOfTypeAll<InventoryItem>())
                if (inv != null && !inv.gameObject.scene.IsValid() && !ShopScene.IsTemplate(inv.transform))
                    ids.Add(ShopIds.VanillaItem(inv.gameObject.name));
            return ids;
        }
    }
}
