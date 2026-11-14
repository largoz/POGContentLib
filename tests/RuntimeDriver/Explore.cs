// Exploration for the 2026-11-14 game build (spawners load by address). explore=1 in driver.txt.
// Read-only apart from loading one item prefab by address; answers, from the running game:
//   - which prefabs NGO knows and whether item prefabs are among them (and in memory);
//   - what the Addressables catalog calls an item, and whether loading it by address gives the shell the
//     library needs (with the same hash NGO registered);
//   - the order in which a shop slot loads, picks, spawns and links its item (traces below).
using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Il2Cpp;
using Il2CppGame.Spawning;
using Il2CppInventory;
using MelonLoader;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace POGContentLib.RuntimeDriver
{
    internal static class Explore
    {
        public static bool Enabled;
        static void Say(string m) => MelonLogger.Msg("[DRIVER] X " + m);

        public static void Snapshot(string where)
        {
            Try($"{where} ngo", () => Ngo(where));
            Try($"{where} memory", () => Memory(where));
            Try($"{where} game list", () => GameList(where));
        }

        public static void AtMenu()
        {
            Snapshot("menu");
            Try("catalog", Catalog);
        }

        public static void InHub()
        {
            Snapshot("hub");
            Try("slot refs", SlotRefs);
        }

        // The game's own network prefab list (NetworkHandler.m_networkPrefabsList, loaded by address). Read through
        // its concrete List<NetworkPrefab>: interface proxies are not safe to enumerate here.
        static void GameList(string where)
        {
            var h = Il2CppGame.Networking.NetworkHandler.Singleton;
            if (h == null) { Say($"{where}: NetworkHandler.Singleton null"); return; }
            var list = h.m_loadedNetworkPrefabsList;
            var reference = h.m_networkPrefabsList;
            string key = reference?.RuntimeKey?.ToString() ?? "-";
            if (list == null || list.List == null) { Say($"{where}: game prefab list not loaded (ref key {key}, guid {reference?.AssetGUID})"); return; }
            int n = list.List.Count, items = 0;
            NetworkObject diamond = null;
            for (int i = 0; i < n; i++)
            {
                var go = list.List[i]?.Prefab;
                if (go == null) continue;
                if (go.GetComponent<InventoryItem>() != null) items++;
                if (go.name == "Item_Diamond") diamond = go.GetComponent<NetworkObject>();
            }
            var ngo = NgoHashes();
            Say($"{where}: game prefab list '{list.name}' {n} entries, {items} items; Item_Diamond={(diamond != null ? $"hash {diamond.GlobalObjectIdHash}, in NGO {ngo.Contains(diamond.GlobalObjectIdHash)}, standing {diamond.GetComponent<InventoryItem>()?.m_standingTransform != null}" : "absent")}; ref key {key}");
        }

        static void Try(string what, Action a)
        {
            try { a(); }
            catch (Exception ex) { Say($"{what} FAILED {ex.GetType().Name}: {ex.Message.Split('\n')[0]}"); }
        }

        static HashSet<uint> NgoHashes()
        {
            var set = new HashSet<uint>();
            var links = NetworkManager.Singleton?.NetworkConfig?.Prefabs?.NetworkPrefabOverrideLinks;
            if (links != null) foreach (var kv in links) set.Add(kv.Key);
            return set;
        }

        static void Ngo(string where)
        {
            var nm = NetworkManager.Singleton;
            var cfg = nm?.NetworkConfig;
            var links = cfg?.Prefabs?.NetworkPrefabOverrideLinks;
            int total = 0, items = 0, nullPrefab = 0;
            var names = new List<string>();
            bool diamond = false;
            if (links != null)
                foreach (var kv in links)
                {
                    total++;
                    var go = kv.Value?.Prefab;
                    if (go == null) { nullPrefab++; continue; }
                    if (go.GetComponent<InventoryItem>() != null) { items++; if (names.Count < 12) names.Add(go.name); }
                    if (go.name == "Item_Diamond") diamond = true;
                }
            Say($"{where}: NGO forceSame={cfg?.ForceSamePrefabs} links={total} (no prefab object: {nullPrefab}) items={items} Item_Diamond={diamond} first items: {string.Join(", ", names)}");
        }

        static void Memory(string where)
        {
            var all = Resources.FindObjectsOfTypeAll<InventoryItem>();
            int assets = all.Count(i => i != null && !i.gameObject.scene.IsValid());
            bool diamond = all.Any(i => i != null && !i.gameObject.scene.IsValid() && i.gameObject.name == "Item_Diamond");
            Say($"{where}: InventoryItem in memory {all.Length}, prefab assets {assets}, Item_Diamond asset={diamond}; " +
                $"sample: {string.Join(", ", all.Where(i => i != null && !i.gameObject.scene.IsValid()).Select(i => i.gameObject.name).Distinct().Take(10))}");
        }

        // The catalog's keys for one item and for the prefab list, with the location each resolves to.
        // Enumerating Addressables.ResourceLocators / their Keys through the interface proxies crashed the game
        // (EXCEPTION_ACCESS_VIOLATION, 2026-11-14). Ask for candidate keys one by one instead.
        static void Catalog()
        {
            foreach (var key in new[] { "Item_Diamond", "Item_Diamond.prefab", "networkprefabs", "NetworkPrefabsList" })
                Try("locate " + key, () => Locate(key));
            // The address format, from a key the game itself holds.
            var r = Il2CppGame.Networking.NetworkHandler.Singleton?.m_networkPrefabsList;
            if (r != null && !string.IsNullOrEmpty(r.AssetGUID)) Try("locate prefab list guid", () => Locate(r.AssetGUID));
        }

        static void Locate(string key)
        {
            var h = Addressables.LoadResourceLocationsAsync((Il2CppSystem.Object)(Il2CppSystem.String)key, null);
            var list = h.WaitForCompletion();
            int c = CountOf(list);
            var parts = new List<string>();
            for (int i = 0; i < c && i < 5; i++)
            {
                var l = list[i];
                parts.Add($"{l.PrimaryKey} -> {l.InternalId} ({l.ResourceType?.Name}, provider {l.ProviderId?.Split('.').Last()})");
            }
            Say($"locate '{key}': {c} location(s): {string.Join(" | ", parts)}");
        }

        static void LoadByAddress(string key)
        {
            var hashes = NgoHashes();
            var h = Addressables.LoadAssetAsync<GameObject>((Il2CppSystem.Object)(Il2CppSystem.String)key);
            var go = h.WaitForCompletion();
            if (go == null) { Say($"load '{key}': null (status {h.Status})"); return; }
            var item = go.GetComponent<InventoryItem>();
            var no = go.GetComponent<NetworkObject>();
            Say($"load '{key}': {go.name} scene={(go.scene.IsValid() ? go.scene.name : "none")} item={(item != null ? item.m_itemID : "none")} " +
                $"standing={(item != null && item.m_standingTransform != null)} hash={(no != null ? no.GlobalObjectIdHash.ToString() : "none")} inNgo={(no != null && hashes.Contains(no.GlobalObjectIdHash))}");
        }

        // What each hub slot points at, by address, without loading anything.
        static void SlotRefs()
        {
            foreach (var s in UnityEngine.Object.FindObjectsOfType<ShopLocation>(true))
            {
                string fixedKey = s.HasNetworkPrefab ? KeyOf(s.m_networkPrefabRef?.AssetGUID) : "-";
                string poolKey = s.HasLootTable ? KeyOf(s.m_lootTableRef?.AssetGUID) : "-";
                Say($"slot {s.gameObject.name}: fixed={fixedKey} pool={poolKey} loaded={(s.m_loadedPrefab != null ? s.m_loadedPrefab.name : "-")} " +
                    $"shopItem={(s.CurrentShopItem != null ? s.CurrentShopItem.name : "-")} price={s.CurrentPrice} instance={(s.m_networkInstance != null ? s.m_networkInstance.name : "-")}");
            }
        }

        static int CountOf<T>(Il2CppSystem.Collections.Generic.IList<T> l) where T : Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase =>
            l == null ? -1 : l.Cast<Il2CppSystem.Collections.Generic.ICollection<T>>().Count;

        static string KeyOf(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return "(no guid)";
            try
            {
                var list = Addressables.LoadResourceLocationsAsync((Il2CppSystem.Object)(Il2CppSystem.String)guid, null).WaitForCompletion();
                return CountOf(list) > 0 ? $"{list[0].PrimaryKey}" : $"{guid}(no location)";
            }
            catch (Exception ex) { return $"{guid}({ex.GetType().Name})"; }
        }

        // ---- Traces: the order a slot loads, picks, spawns and links its item ----

        static readonly Dictionary<string, int> _count = new Dictionary<string, int>();
        static bool First(string what, int max = 30)
        {
            _count.TryGetValue(what, out int n);
            _count[what] = ++n;
            return n <= max;
        }
        public static void Trace(string what, string msg, bool stack = false)
        {
            if (!Enabled || !First(what)) return;
            Say($"T {what} #{_count[what]} f{Time.frameCount} {msg}");
            if (stack && _count[what] <= 1) Say($"T {what} stack:\n" + string.Join("\n", (StackTraceUtility.ExtractStackTrace() ?? "").Split('\n').Take(14)));
        }
        static string N(UnityEngine.Object o) => o == null ? "null" : o.name;
    }

    [HarmonyPatch(typeof(SpawnerBase), nameof(SpawnerBase.LoadNetworkPrefab))]
    static class X_LoadNetworkPrefab
    {
        static void Postfix(SpawnerBase __instance, NetworkObject __result) =>
            Explore.Trace("LoadNetworkPrefab", $"{__instance.GetType().Name}/{__instance.gameObject.name} -> {(__result == null ? "null" : __result.name)}", true);
    }

    [HarmonyPatch(typeof(SpawnerBase), nameof(SpawnerBase.PreloadNetworkPrefab))]
    static class X_Preload
    {
        static void Prefix(SpawnerBase __instance) => Explore.Trace("PreloadNetworkPrefab", $"{__instance.gameObject.name} hasRef={__instance.HasNetworkPrefab}", true);
    }

    [HarmonyPatch(typeof(ShopLocation), nameof(ShopLocation.LoadLootTable))]
    static class X_LoadLootTable
    {
        static void Postfix(ShopLocation __instance, InventoryLootTable __result) =>
            Explore.Trace("LoadLootTable", $"{__instance.gameObject.name} -> {(__result == null ? "null" : $"{__result.name}[{__result.Items?.Count}]")}", true);
    }

    [HarmonyPatch(typeof(InventoryLootTable), nameof(InventoryLootTable.GetRandomItem))]
    static class X_GetRandomItem
    {
        static void Postfix(InventoryLootTable __instance, InventoryItem __result) =>
            Explore.Trace("GetRandomItem", $"{__instance.name} -> {(__result == null ? "null" : __result.name)}", true);
    }

    [HarmonyPatch(typeof(ShopLocation), nameof(ShopLocation.CreateShopItems))]
    static class X_CreateShopItems
    {
        static void Prefix(ShopLocation __instance) =>
            Explore.Trace("CreateShopItems", $"{__instance.gameObject.name} server={__instance.IsServer} hasRef={__instance.HasNetworkPrefab} hasPool={__instance.HasLootTable} loaded={(__instance.m_loadedPrefab == null ? "-" : __instance.m_loadedPrefab.name)}", true);
    }

    [HarmonyPatch(typeof(ShopLocation), nameof(ShopLocation.CanSpawn))]
    static class X_CanSpawn
    {
        static void Postfix(ShopLocation __instance, NetworkObject __0, bool __result) =>
            Explore.Trace("CanSpawn", $"{__instance.gameObject.name} ({(__0 == null ? "null" : __0.name)}) -> {__result}", true);
    }

    [HarmonyPatch(typeof(ItemSpawner), nameof(ItemSpawner.InstantiateNetworkPrefab))]
    static class X_Instantiate
    {
        static void Postfix(ItemSpawner __instance, NetworkObject __result) =>
            Explore.Trace("InstantiateNetworkPrefab", $"{__instance.gameObject.name} -> {(__result == null ? "null" : __result.name)}", true);
    }

    [HarmonyPatch(typeof(ItemSpawner), nameof(ItemSpawner.SpawnNetworkPrefab))]
    static class X_Spawn
    {
        static void Prefix(ItemSpawner __instance, NetworkObject __0) =>
            Explore.Trace("SpawnNetworkPrefab", $"{__instance.gameObject.name} ({(__0 == null ? "null" : __0.name)})", true);
    }

    [HarmonyPatch(typeof(ShopLocation), nameof(ShopLocation.SetShopItemInternal))]
    static class X_SetShopItem
    {
        static void Prefix(ShopLocation __instance, InventoryItem __0) =>
            Explore.Trace("SetShopItemInternal", $"{__instance.gameObject.name} ({(__0 == null ? "null" : __0.name)}) server={__instance.IsServer}", true);
    }

    [HarmonyPatch(typeof(ShopLocation), nameof(ShopLocation.OnNetworkSpawn))]
    static class X_SlotSpawn
    {
        static void Prefix(ShopLocation __instance) =>
            Explore.Trace("ShopLocation.OnNetworkSpawn", $"{__instance.gameObject.name} hasRef={__instance.HasNetworkPrefab} hasPool={__instance.HasLootTable} loaded={(__instance.m_loadedPrefab == null ? "-" : __instance.m_loadedPrefab.name)}");
    }
}

namespace POGContentLib.RuntimeDriver
{
    // When the game loads and registers its network prefabs, relative to the session start.
    [HarmonyLib.HarmonyPatch(typeof(Il2CppGame.Networking.NetworkHandler), nameof(Il2CppGame.Networking.NetworkHandler.EnsureNetworkPrefabsRegistered))]
    static class X_EnsureRegistered
    {
        static void Prefix(Il2CppGame.Networking.NetworkHandler __instance) =>
            Explore.Trace("EnsureNetworkPrefabsRegistered", $"before: loaded={(__instance.m_loadedNetworkPrefabsList != null)} ngoLinks={Unity.Netcode.NetworkManager.Singleton?.NetworkConfig?.Prefabs?.NetworkPrefabOverrideLinks?.Count} listening={Unity.Netcode.NetworkManager.Singleton?.IsListening}", true);
        static void Postfix(Il2CppGame.Networking.NetworkHandler __instance) =>
            Explore.Trace("EnsureNetworkPrefabsRegistered.after", $"loaded={(__instance.m_loadedNetworkPrefabsList != null)} entries={__instance.m_loadedNetworkPrefabsList?.List?.Count} ngoLinks={Unity.Netcode.NetworkManager.Singleton?.NetworkConfig?.Prefabs?.NetworkPrefabOverrideLinks?.Count}");
    }

    [HarmonyLib.HarmonyPatch(typeof(Unity.Netcode.NetworkManager), nameof(Unity.Netcode.NetworkManager.StartHost))]
    static class X_StartHost
    {
        static void Prefix() =>
            Explore.Trace("StartHost", $"loadedList={(Il2CppGame.Networking.NetworkHandler.Singleton?.m_loadedNetworkPrefabsList != null)} ngoLinks={Unity.Netcode.NetworkManager.Singleton?.NetworkConfig?.Prefabs?.NetworkPrefabOverrideLinks?.Count}", true);
    }
}
