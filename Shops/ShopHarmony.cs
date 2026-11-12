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
        private static readonly HashSet<IntPtr> _empty = new HashSet<IntPtr>();

        public static void Mark(ShopLocation slot) => _empty.Add(slot.Pointer);

        public static void HideTagIfEmpty(ShopLocation slot)
        {
            if (slot == null || !_empty.Contains(slot.Pointer)) return;
            // The whole tag (its board too), not only the text: a blank board looked like a broken tag.
            if (slot.m_priceTagText != null) SlotTemplates.TopChildOf(slot.m_priceTagText.transform, slot.transform).gameObject.SetActive(false);
        }

        public static void Reset() => _empty.Clear();
    }
}
