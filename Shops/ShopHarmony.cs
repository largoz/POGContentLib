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
            // A mod slot spawned from our template: an ordinary scene object from here on (see TemplateCopies).
            try { if (__instance != null && __instance.gameObject.name.StartsWith(ShopScene.ModSlotPrefix)) POGContentLib.Core.TemplateCopies.Release(__instance.gameObject); }
            catch (Exception ex) { MelonLogger.Warning($"[POGContentLib] [Shops] {ex.GetType().Name}: {ex.Message}"); }
            try { ShopEmptySlots.HideTagIfEmpty(__instance); }
            catch (Exception ex) { MelonLogger.Warning($"[POGContentLib] [Shops] {ex.GetType().Name}: {ex.Message}"); }
        }
    }

    /// <summary>
    /// A slot hands every peer its item through <c>SetShopItemInternal</c> (the host from its own RPC, a client from
    /// <c>ResolveShopItemOnClient</c>; build 26668562). The item was placed earlier, by the slot's holder pose, which is
    /// made for the slot's vanilla item: the torch slot lays its torch flat, and laid the Ember Lantern on its side too
    /// (2026-11-19, host and client). A mod item is stood upright here, on every peer, keeping its position and its
    /// heading.
    /// </summary>
    [HarmonyPatch(typeof(ShopLocation), nameof(ShopLocation.SetShopItemInternal))]
    internal static class Patch_ShopLocation_SetShopItemInternal
    {
        static void Postfix(ShopLocation __instance, Il2Cpp.InventoryItem __0)
        {
            try { ShopItemPose.StandUpright(__instance, __0); }
            catch (Exception ex) { MelonLogger.Warning($"[POGContentLib] [Shops] standing an item upright: {ex.GetType().Name}: {ex.Message}"); }
        }
    }

    internal static class ShopItemPose
    {
        /// <summary>Tilt below this many degrees is left as the slot made it.</summary>
        public const float TiltToleranceDegrees = 5f;
        private static readonly HashSet<string> _logged = new HashSet<string>(StringComparer.Ordinal);

        public static void StandUpright(ShopLocation slot, Il2Cpp.InventoryItem item)
        {
            if (slot == null || item == null) return;
            var items = POGContentLib.Items.ItemsPlugin.Instance;
            if (items == null || !items.IsModItem(item)) return;

            var t = item.transform;
            float tilt = UnityEngine.Vector3.Angle(t.up, UnityEngine.Vector3.up);
            if (tilt <= TiltToleranceDegrees) return;

            // Heading: where the item's front pointed, flattened; a front that pointed straight up or down gives no
            // heading, so the direction its top pointed is used instead.
            var heading = UnityEngine.Vector3.ProjectOnPlane(t.forward, UnityEngine.Vector3.up);
            if (heading.sqrMagnitude < 1e-4f) heading = UnityEngine.Vector3.ProjectOnPlane(t.up, UnityEngine.Vector3.up);
            if (heading.sqrMagnitude < 1e-4f) heading = UnityEngine.Vector3.forward;
            t.rotation = UnityEngine.Quaternion.LookRotation(heading.normalized, UnityEngine.Vector3.up);

            string key = slot.gameObject.name + "|" + item.m_itemID;
            if (_logged.Add(key))
                MelonLogger.Msg($"[POGContentLib] [Shops] {item.m_itemID} in '{slot.gameObject.name}': stood upright (the slot tilted it {tilt:0}°).");
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
            // The HasNetworkPrefab prefix alone did not reach a slot without a reference (a pool slot, a new slot:
            // 2026-11-14, both stayed empty), so the game reads the field itself. Give such a slot a reference;
            // LoadNetworkPrefab answers for it and the preload is skipped, so it is never loaded.
            var r = slot.m_networkPrefabRef;
            if (r == null || string.IsNullOrEmpty(r.AssetGUID))
            {
                var standIn = StandInGuid();
                if (standIn != null) slot.m_networkPrefabRef = new UnityEngine.AddressableAssets.AssetReferenceGameObject(standIn);
            }
        }

        // A GUID of a real GameObject in the game's catalog (the persistent player prefab), so that even an
        // unexpected load of the stand-in would find an asset.
        private static string _standIn;
        private static string StandInGuid()
        {
            if (!string.IsNullOrEmpty(_standIn)) return _standIn;
            try { _standIn = Il2CppGame.Networking.NetworkHandler.Singleton?.m_persistentPlayer?.AssetGUID; }
            catch { _standIn = null; }
            if (string.IsNullOrEmpty(_standIn))
                MelonLoader.MelonLogger.Warning("[POGContentLib] [Shops] no stand-in reference for slots without one; their choice may not reach the game.");
            return _standIn;
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
