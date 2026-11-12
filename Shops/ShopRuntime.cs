using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Il2CppGame.Gameplay;
using Il2CppGame.Spawning;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;
using POGContentLib.Shops.Model;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace POGContentLib.Shops
{
    /// <summary>What happened to one slot this session (read by the dev tools and the logs).</summary>
    public sealed class SlotRecord
    {
        public string SlotId { get; internal set; }
        public ShopLocation Slot { get; internal set; }
        public ResolvedSlot Resolved { get; internal set; }
        /// <summary>What the game itself put in the slot, captured before any rule changed it.</summary>
        public List<VanillaEntry> Vanilla { get; internal set; } = new List<VanillaEntry>();
        /// <summary>Item id the host chose, "nothing", or null when the game's own choice was left alone.</summary>
        public string Picked { get; internal set; }
        public List<string> Notes { get; } = new List<string>();
    }

    /// <summary>
    /// Host-side application of the shop rules in a loaded scene:
    ///  • a vanilla slot touched by any rule gets its item chosen by weight here and handed to the game as
    ///    that slot's fixed item (the path most vanilla slots already use), so the game spawns, prices and
    ///    sells it as usual;
    ///  • mod slots are spawned next to their anchor once the scene's own objects exist, bound to their
    ///    seller (faction, theft, the seller's death) and stocked the same way.
    /// Clients run none of this: they see the result through the game's own replication.
    /// </summary>
    public static class ShopRuntime
    {
        private static readonly Dictionary<string, SlotRecord> _records = new Dictionary<string, SlotRecord>(StringComparer.Ordinal);
        private static System.Random _rng = new System.Random();

        public static IReadOnlyDictionary<string, SlotRecord> Records => _records;

        /// <summary>Tests can fix the random source; null restores a fresh one.</summary>
        public static void SetRandomSeed(int? seed) => _rng = seed.HasValue ? new System.Random(seed.Value) : new System.Random();

        internal static void Reset()
        {
            _records.Clear();
            _memo.Clear();
            SellerTemplates.Reset();
        }

        private static ShopCatalog Catalog => ShopsPlugin.Instance?.Catalog;

        private static bool IsHost
        {
            get { var nm = NetworkManager.Singleton; return nm != null && nm.IsListening && nm.IsServer; }
        }

        // ---- Stocking (vanilla and mod slots) ----

        /// <summary>Called from the ShopLocation.OnNetworkSpawn prefix: choose this slot's item on the host.</summary>
        // One choice per slot object per session: OnNetworkSpawn can run more than once for the same
        // in-scene slot (seen 2026-11-12: twice per hub load), and the second call must neither re-roll
        // nor mistake the first choice for the game's own content.
        private sealed class Memo { public List<VanillaEntry> Vanilla; public string Picked; public NetworkObject Prefab; }
        private static readonly Dictionary<IntPtr, Memo> _memo = new Dictionary<IntPtr, Memo>();

        internal static void OnSlotSpawning(ShopLocation slot)
        {
            var catalog = Catalog;
            if (catalog == null || slot == null || !IsHost) return;
            if (_memo.TryGetValue(slot.Pointer, out var seen))
            {
                if (seen.Prefab != null) { slot.m_networkPrefab = seen.Prefab; slot.m_lootTable = null; }
                else if (seen.Picked == ShopIds.Nothing) { slot.m_networkPrefab = null; slot.m_lootTable = null; }
                return;
            }
            string id = SellerTemplates.VariantSlotId(slot, catalog, _rng) ?? ShopScene.SlotId(slot);
            var vanilla = ShopScene.VanillaEntriesOf(slot);
            var memo = new Memo { Vanilla = vanilla };
            _memo[slot.Pointer] = memo;
            var resolved = catalog.Resolve(id, vanilla);
            var rec = new SlotRecord { SlotId = id, Slot = slot, Resolved = resolved, Vanilla = vanilla };
            _records[id] = rec;
            if (resolved.Untouched) return;

            // Drop entries whose item is not loaded (its mod is missing, or a typo), then pick by weight.
            var usable = new List<(SlotEntry e, NetworkObject prefab)>();
            foreach (var e in resolved.Entries)
            {
                if (e.Weight <= 0) continue;
                if (e.Item == ShopIds.Nothing) { usable.Add((e, null)); continue; }
                var prefab = ShopScene.ResolveItemPrefab(e.Item);
                if (prefab != null) usable.Add((e, prefab));
                else rec.Notes.Add($"{e.Item} (from {e.AddedBy}) is not loaded; skipped");
            }
            double total = usable.Sum(u => u.e.Weight);
            if (total <= 0)
            {
                rec.Picked = memo.Picked = ShopIds.Nothing;
                rec.Notes.Add("no usable entry: the slot stays empty");
                MakeEmpty(slot, rec);
                return;
            }
            double target = _rng.NextDouble() * total, acc = 0;
            var chosen = usable[usable.Count - 1];
            foreach (var u in usable) { acc += u.e.Weight; if (target < acc) { chosen = u; break; } }

            rec.Picked = memo.Picked = chosen.e.Item;
            memo.Prefab = chosen.prefab;
            if (chosen.prefab == null) { MakeEmpty(slot, rec); return; }
            slot.m_networkPrefab = chosen.prefab;
            slot.m_lootTable = null;
            MelonLogger.Msg($"[POGContentLib] [Shops] {id}: {chosen.e.Item} ({100 * chosen.e.Weight / total:0.#}% of {usable.Count} candidate(s))");
        }

        private static void MakeEmpty(ShopLocation slot, SlotRecord rec)
        {
            // Both sources null: the slot offers nothing (see SHOPS_NATIVE.md for how CreateShopItems
            // treats that). The price tag would still show the last price, so hide it.
            slot.m_networkPrefab = null;
            slot.m_lootTable = null;
            ShopEmptySlots.Mark(slot);
            MelonLogger.Msg($"[POGContentLib] [Shops] {rec.SlotId}: stays empty");
        }

        // ---- Mod slots ----

        internal static void OnSceneLoaded()
        {
            var catalog = Catalog;
            if (catalog == null || catalog.ModSlots.Count == 0) return;
            string scene = SceneManager.GetActiveScene().name;
            if (!catalog.ModSlots.Values.Any(s => s.Def.Scene == scene)) return;
            MelonCoroutines.Start(SpawnModSlotsWhenReady(scene));
        }

        private static IEnumerator SpawnModSlotsWhenReady(string scene)
        {
            // Wait for the session and for the scene's own sellers/slots to be spawned by NGO.
            float deadline = Time.realtimeSinceStartup + 60f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (SceneManager.GetActiveScene().name != scene) yield break;
                if (IsHost && ShopScene.SlotsInLoadedScenes().Any(s => s.IsSpawned)) break;
                yield return new WaitForSeconds(0.5f);
            }
            if (!IsHost) yield break;
            yield return new WaitForSeconds(1f);
            SpawnModSlots(scene);
        }

        internal static void SpawnModSlots(string scene)
        {
            var catalog = Catalog;
            int ok = 0, failed = 0;
            foreach (var ms in catalog.ModSlots.Values.Where(s => s.Def.Scene == scene))
            {
                try
                {
                    if (SpawnOne(ms)) ok++; else failed++;
                }
                catch (Exception ex)
                {
                    failed++;
                    MelonLogger.Error($"[POGContentLib] [Shops] slot {ms.FullId} failed: {ex.GetType().Name}: {ex.Message}");
                }
            }
            MelonLogger.Msg($"[POGContentLib] [Shops] {scene}: placed {ok} mod slot(s){(failed > 0 ? $", {failed} failed" : "")}.");
        }

        private static bool SpawnOne(ShopCatalog.ModSlot ms)
        {
            var def = ms.Def;
            if (ShopScene.FindSlot(ms.FullId) != null) return true;   // already spawned this scene visit
            var template = SlotTemplates.Get(ms.FullId);
            if (template == null) { MelonLogger.Warning($"[POGContentLib] [Shops] slot {ms.FullId}: no template (built after the session started?)"); return false; }

            string anchorId = string.IsNullOrEmpty(def.Anchor) ? def.Seller : def.Anchor;
            var anchor = ShopScene.FindAnchor(anchorId);
            if (anchor == null) { MelonLogger.Warning($"[POGContentLib] [Shops] slot {ms.FullId}: anchor '{anchorId}' is not in scene {def.Scene}"); return false; }
            NpcSpawner seller = string.IsNullOrEmpty(def.Seller) ? null : ShopScene.FindSeller(def.Seller);
            if (!string.IsNullOrEmpty(def.Seller) && seller == null)
                MelonLogger.Warning($"[POGContentLib] [Shops] slot {ms.FullId}: seller '{def.Seller}' not found; placed without a seller");

            var go = UnityEngine.Object.Instantiate(template.gameObject);
            go.name = ShopScene.ModSlotPrefix + ms.FullId;
            var pos = anchor.TransformPoint(new Vector3(def.Pos[0], def.Pos[1], def.Pos[2]));
            var rot = anchor.rotation * Quaternion.Euler(def.Rot != null ? new Vector3(def.Rot[0], def.Rot[1], def.Rot[2]) : Vector3.zero);
            go.transform.SetPositionAndRotation(pos, rot);
            var slot = go.GetComponent<ShopLocation>();

            Faction faction = Faction.None;
            if (seller != null)
            {
                slot.m_shopKeeper = seller;
                var sibling = seller.m_itemsForSale.FirstOrDefault(s => s != null && s.m_shopFaction != null);
                if (sibling != null) faction = sibling.m_shopFaction.Value;
                AppendToSeller(seller, slot);
            }
            else if (!string.IsNullOrEmpty(def.Faction) && !Enum.TryParse(def.Faction, out faction))
                MelonLogger.Warning($"[POGContentLib] [Shops] slot {ms.FullId}: unknown faction '{def.Faction}' (use {string.Join(", ", Enum.GetNames(typeof(Faction)))})");

            go.GetComponent<NetworkObject>().Spawn(true);   // OnNetworkSpawn → OnSlotSpawning stocks it
            if (faction != Faction.None && slot.m_shopFaction != null) slot.m_shopFaction.Value = faction;
            MelonLogger.Msg($"[POGContentLib] [Shops] placed {ms.FullId} at {pos:F2} next to {anchor.name}{(seller != null ? $", sold by {ShopScene.SellerId(seller)} ({faction})" : "")}");
            return true;
        }

        private static void AppendToSeller(NpcSpawner seller, ShopLocation slot)
        {
            var old = seller.m_itemsForSale;
            var arr = new Il2CppReferenceArray<ShopLocation>(old.Length + 1);
            for (int i = 0; i < old.Length; i++) arr[i] = old[i];
            arr[old.Length] = slot;
            seller.m_itemsForSale = arr;
        }
    }
}
