using HarmonyLib;
using Il2Cpp;
using POGLib.Core;
using UnityEngine;

namespace POGLib.Items
{
    /// <summary>
    /// Items-domain Harmony patches. Same pattern that proved workable on this engine
    /// (POGCustomItems): postfix on InventoryItem.OnNetworkSpawn/Interact.
    /// </summary>
    [HarmonyPatch(typeof(InventoryItem), GameNames.Methods.InventoryItem_OnNetworkSpawn)]
    internal static class Patch_Item_OnNetworkSpawn
    {
        static void Postfix(InventoryItem __instance)
        {
            if (__instance == null) return;
            var plugin = ItemsPlugin.Instance;
            if (plugin == null || !plugin.IsModItem(__instance)) return;

            // A copy of our template, spawned: an ordinary scene object from here on (see TemplateCopies).
            TemplateCopies.Release(__instance.gameObject);

            // Ensure the state component exists on the spawned instance (managed state does not
            // survive Instantiate — the handle lazily resolves itself by hash).
            if (__instance.GetComponent<ModItemHandle>() == null)
                __instance.gameObject.AddComponent<ModItemHandle>();

            // Kick visual effects awake: they were built on the INACTIVE template, where
            // ParticleSystem.Play() silently no-ops — each live instance must restart them.
            ItemVisualEffect.RestartEffectsOn(__instance);
        }
    }

    /// <summary>
    /// The item's primary action — "use what I am holding" (left click). This is the correct hook:
    /// Interact is the PICKUP interaction, so patching that never fired the use flow (IsInHands is
    /// false while picking up, and using a held item does not call Interact at all).
    /// </summary>
    [HarmonyPatch(typeof(InventoryItem), GameNames.Methods.InventoryItem_StartPrimaryAction)]
    internal static class Patch_Item_StartPrimaryAction
    {
        static void Postfix(InventoryItem __instance)
        {
            if (__instance == null) return;
            var plugin = ItemsPlugin.Instance;
            if (plugin == null || !plugin.IsModItem(__instance)) return;

            plugin.TryUseItem(__instance, __instance.Entity);
        }
    }

    /// <summary>
    /// D9 (2026-11-19, LAN): a shop slot that sells copies (<c>SpawnCopyInsteadOfPickup</c>, e.g. the starter
    /// shop's bread slot) hands the buyer a new item through <c>InventoryItem.SpawnCopyAndPickup</c>, which looks the
    /// prefab up with <c>NetworkHandler.GetNetworkPrefab(hash)</c>. That walks only the game's own prefab list, so
    /// for a mod item it returned null: NGO logged "The NetworkObject prefab parameter was null!", BuyItem threw,
    /// and the buyer paid (gold 14 -> 7) and got nothing. A mod hash the game's list does not know is answered
    /// with the mod's template, which is registered with NGO under that hash (NetworkRegistrar).
    /// </summary>
    [HarmonyPatch(typeof(Il2CppGame.Networking.NetworkHandler), nameof(Il2CppGame.Networking.NetworkHandler.GetNetworkPrefab))]
    internal static class Patch_NetworkHandler_GetNetworkPrefab
    {
        private static readonly System.Collections.Generic.HashSet<uint> _logged = new System.Collections.Generic.HashSet<uint>();

        static void Postfix(uint prefabIdHash, ref Unity.Netcode.NetworkObject __result)
        {
            try
            {
                if (__result != null) return;
                var prefab = CoreServices.Content?.GetPrefabByHash(prefabIdHash);
                if (prefab == null) return;
                __result = prefab;
                if (_logged.Add(prefabIdHash))
                    MelonLoader.MelonLogger.Msg($"[POGLib] The game asked for network prefab {prefabIdHash:X8} (a copy of a mod item); answered with {prefab.name}.");
            }
            catch (System.Exception ex) { MelonLoader.MelonLogger.Warning($"[POGLib] GetNetworkPrefab({prefabIdHash:X8}): {ex.GetType().Name}: {ex.Message}"); }
        }
    }
}
