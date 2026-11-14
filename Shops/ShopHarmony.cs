using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2CppGame.Spawning;
using MelonLoader;

namespace POGContentLib.Shops
{
    /// <summary>
    /// The one hook the shop rules need: right before a slot spawns on the network (where the game decides
    /// what it offers), let the host choose by weight. A prefix that throws must never break the game's
    /// own slot, so everything is guarded.
    /// </summary>
    [HarmonyPatch(typeof(ShopLocation), nameof(ShopLocation.OnNetworkSpawn))]
    internal static class Patch_ShopLocation_OnNetworkSpawn
    {
        static void Prefix(ShopLocation __instance)
        {
            try { ShopRuntime.OnSlotSpawning(__instance); }
            catch (Exception ex) { MelonLogger.Error($"[POGContentLib] [Shops] stocking failed, the game's own choice stays: {ex}"); }
        }

        static void Postfix(ShopLocation __instance)
        {
            try { ShopEmptySlots.HideTagIfEmpty(__instance); }
            catch (Exception ex) { MelonLogger.Warning($"[POGContentLib] [Shops] {ex.GetType().Name}: {ex.Message}"); }
        }
    }

    /// <summary>Slots the rules left empty: their price tag is hidden so no stale price shows.</summary>
    internal static class ShopEmptySlots
    {
        private static readonly HashSet<int> _empty = new HashSet<int>();   // GetInstanceID, not the reusable pointer

        public static void Mark(ShopLocation slot) => _empty.Add(slot.GetInstanceID());

        public static void HideTagIfEmpty(ShopLocation slot)
        {
            if (slot == null || !_empty.Contains(slot.GetInstanceID())) return;
            // The whole tag (its board too), not only the text: a blank board looked like a broken tag.
            if (slot.m_priceTagText != null) SlotTemplates.TopChildOf(slot.m_priceTagText.transform, slot.transform).gameObject.SetActive(false);
        }

        public static void Reset() => _empty.Clear();
    }
}

namespace POGContentLib.Shops
{
    /// <summary>
    /// The host's choice per slot, handed to the game's own pipeline (2026-11-14 build, read from traces of the
    /// hub's 13 slots): CreateShopItems takes a fixed item through LoadNetworkPrefab → CanSpawn →
    /// InstantiateNetworkPrefab (which calls LoadNetworkPrefab again), and a pool slot, whose HasNetworkPrefab is
    /// false, through LoadLootTable → GetRandomItem. A chosen slot is made to look like a fixed slot holding the
    /// chosen prefab: HasNetworkPrefab true, LoadNetworkPrefab returns it, no loot table, and the game's preload of
    /// its own reference is skipped. "Nothing" is a slot with neither (how CreateShopItems takes that on this
    /// build is not yet seen). Keyed by GetInstanceID; cleared when the session stops.
    /// </summary>
    internal static class ShopChoices
    {
        private static readonly System.Collections.Generic.Dictionary<int, Unity.Netcode.NetworkObject> _chosen =
            new System.Collections.Generic.Dictionary<int, Unity.Netcode.NetworkObject>();

        public static void Set(Il2CppGame.Spawning.ShopLocation slot, Unity.Netcode.NetworkObject prefab)
        {
            _chosen[slot.GetInstanceID()] = prefab;
            slot.m_loadedPrefab = prefab;   // what LoadNetworkPrefab would have cached
        }

        public static bool TryGet(UnityEngine.Object spawner, out Unity.Netcode.NetworkObject prefab)
        {
            prefab = null;
            return spawner != null && _chosen.Count > 0 && _chosen.TryGetValue(spawner.GetInstanceID(), out prefab);
        }

        public static void Reset() => _chosen.Clear();
    }

    [HarmonyPatch(typeof(Il2CppGame.Spawning.SpawnerBase), nameof(Il2CppGame.Spawning.SpawnerBase.HasNetworkPrefab), MethodType.Getter)]
    internal static class Patch_SpawnerBase_HasNetworkPrefab
    {
        static bool Prefix(Il2CppGame.Spawning.SpawnerBase __instance, ref bool __result)
        {
            if (!ShopChoices.TryGet(__instance, out var prefab)) return true;
            __result = prefab != null;
            return false;
        }
    }

    [HarmonyPatch(typeof(Il2CppGame.Spawning.SpawnerBase), nameof(Il2CppGame.Spawning.SpawnerBase.LoadNetworkPrefab))]
    internal static class Patch_SpawnerBase_LoadNetworkPrefab
    {
        static bool Prefix(Il2CppGame.Spawning.SpawnerBase __instance, ref Unity.Netcode.NetworkObject __result)
        {
            if (!ShopChoices.TryGet(__instance, out var prefab)) return true;
            __result = prefab;
            return false;
        }
    }

    [HarmonyPatch(typeof(Il2CppGame.Spawning.SpawnerBase), nameof(Il2CppGame.Spawning.SpawnerBase.PreloadNetworkPrefab))]
    internal static class Patch_SpawnerBase_PreloadNetworkPrefab
    {
        // The game's own item is not needed in a chosen slot: do not load it.
        static bool Prefix(Il2CppGame.Spawning.SpawnerBase __instance) => !ShopChoices.TryGet(__instance, out _);
    }

    [HarmonyPatch(typeof(Il2CppGame.Spawning.ShopLocation), nameof(Il2CppGame.Spawning.ShopLocation.HasLootTable), MethodType.Getter)]
    internal static class Patch_ShopLocation_HasLootTable
    {
        static bool Prefix(Il2CppGame.Spawning.ShopLocation __instance, ref bool __result)
        {
            if (!ShopChoices.TryGet(__instance, out _)) return true;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(Il2CppGame.Spawning.ShopLocation), nameof(Il2CppGame.Spawning.ShopLocation.LoadLootTable))]
    internal static class Patch_ShopLocation_LoadLootTable
    {
        static bool Prefix(Il2CppGame.Spawning.ShopLocation __instance, ref Il2CppInventory.InventoryLootTable __result)
        {
            if (!ShopChoices.TryGet(__instance, out _)) return true;
            __result = null;
            return false;
        }
    }
}
