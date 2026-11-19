using System;
using HarmonyLib;
using MelonLoader;
using Unity.Netcode;

namespace POGContentLib.Core
{
    /// <summary>Which side of the session is starting (drives the parity step).</summary>
    internal enum SessionRole { Host, Client }

    /// <summary>
    /// NGO lifecycle Harmony hooks. A prefix on Start* is the only correct window to register
    /// mod prefabs (ForceSamePrefabs=true). A postfix on Shutdown resets session-scoped state
    /// so a lobby rejoin re-registers content. The same window drives the multiplayer parity step:
    /// the host advertises its manifest, a joining client reads and compares the host's.
    /// </summary>
    internal static class NetworkLifecycleHooks
    {
        internal static void OnBeforeSessionStart(SessionRole role)
        {
            if (!CoreServices.Ready) return;
            // The game normally loads its prefab list just before this (and GamePrefabsLoaded has built the
            // mod templates); make sure of it, so registration below never runs with nothing built.
            if (!GamePrefabs.IsLoaded && GamePrefabs.EnsureLoaded()) OnGamePrefabsLoaded();
            CoreServices.Network.RegisterAll();
            PluginRegistry.Dispatch(PluginStage.BeforeSessionStart);

            // Multiplayer parity (ForceSamePrefabs): host advertises, client compares. Guarded so a
            // Steam/interop shape change degrades to a log line instead of breaking session start.
            try
            {
                if (role == SessionRole.Host) ParityService.AdvertiseAsHost();
                else ParityService.CheckAsClient();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[POGContentLib] Parity step failed ({role}): {ex.Message}");
            }
        }

        private static int _dispatchedList;

        /// <summary>The game's prefab list is loaded: run GamePrefabsLoaded once per loaded list object.</summary>
        internal static void OnGamePrefabsLoaded()
        {
            if (!CoreServices.Ready) return;
            var list = GamePrefabs.Loaded;
            if (list == null) return;
            int id = list.GetInstanceID();
            if (id == _dispatchedList) return;
            _dispatchedList = id;
            MelonLogger.Msg($"[POGContentLib] The game's network prefab list is loaded ({list.List?.Count} entries).");
            PluginRegistry.Dispatch(PluginStage.GamePrefabsLoaded);
        }

        /// <summary>The session has its NetworkManager set up: the library's messages can be registered.</summary>
        internal static void OnSessionStarted()
        {
            if (!CoreServices.Ready) return;
            CoreServices.Bridge?.Hook();
        }

        internal static void OnSessionStop()
        {
            if (!CoreServices.Ready) return;
            CoreServices.Bridge?.Unhook();
            CoreServices.Network.Reset();
            PluginRegistry.Dispatch(PluginStage.SessionStop);
        }
    }

    [HarmonyPatch(typeof(NetworkManager), nameof(NetworkManager.StartHost))]
    internal static class Patch_StartHost
    {
        static void Prefix() => NetworkLifecycleHooks.OnBeforeSessionStart(SessionRole.Host);
        static void Postfix() { try { NetworkLifecycleHooks.OnSessionStarted(); } catch (Exception ex) { MelonLogger.Warning($"[POGContentLib] After StartHost: {ex.Message}"); } }
    }

    [HarmonyPatch(typeof(NetworkManager), nameof(NetworkManager.StartServer))]
    internal static class Patch_StartServer
    {
        static void Prefix() => NetworkLifecycleHooks.OnBeforeSessionStart(SessionRole.Host);
    }

    [HarmonyPatch(typeof(NetworkManager), nameof(NetworkManager.StartClient))]
    internal static class Patch_StartClient
    {
        static void Prefix() => NetworkLifecycleHooks.OnBeforeSessionStart(SessionRole.Client);
        static void Postfix() { try { NetworkLifecycleHooks.OnSessionStarted(); } catch (Exception ex) { MelonLogger.Warning($"[POGContentLib] After StartClient: {ex.Message}"); } }
    }

    [HarmonyPatch(typeof(Il2CppGame.Networking.NetworkHandler), nameof(Il2CppGame.Networking.NetworkHandler.EnsureNetworkPrefabsRegistered))]
    internal static class Patch_EnsureNetworkPrefabsRegistered
    {
        static void Postfix()
        {
            try { NetworkLifecycleHooks.OnGamePrefabsLoaded(); }
            catch (Exception ex) { MelonLogger.Error($"[POGContentLib] GamePrefabsLoaded failed: {ex}"); }
        }
    }

    [HarmonyPatch(typeof(NetworkManager), nameof(NetworkManager.Shutdown))]
    internal static class Patch_Shutdown
    {
        static void Postfix() => NetworkLifecycleHooks.OnSessionStop();
    }
}
