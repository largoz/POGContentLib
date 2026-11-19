using System;
using System.Linq;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using MelonLoader;
using POGContentLib.Core;
using POGContentLib.Items;

[assembly: MelonInfo(typeof(POGContentLib.Plugin), "POGContentLib", "0.1.0", "POG Community")]
[assembly: MelonGame(null, "Pit of Goblin")]
// Load earlier than content mods so their OnInitializeMelon can call Content.*.
[assembly: MelonPriority(-1000)]
// The patches are applied one class at a time (PatchEachClassIndependently); without this MelonLoader patched the
// whole assembly too, and every prefix and postfix ran twice (CheckEachPatchOnce, 2026-11-19: 26 of 26).
[assembly: HarmonyDontPatchAll]

namespace POGContentLib
{
    /// <summary>
    /// POGContentLib entry point. This is a MelonMod (NOT a MelonPlugin): working with Il2Cpp
    /// types, NetworkManager and component injection is only possible after the game has
    /// initialized — i.e. in OnInitializeMelon and later events, not in loader pre-init.
    ///
    /// Drives PluginRegistry stages from real events:
    ///   OnInitializeMelon        -> Boot
    ///   OnSceneWasLoaded         -> SceneLoaded
    ///   (Harmony on Start*)      -> BeforeSessionStart   [in NetworkLifecycleHooks]
    ///   (Harmony on Shutdown)    -> SessionStop          [in NetworkLifecycleHooks]
    /// </summary>
    public class Plugin : MelonMod
    {
        public override void OnInitializeMelon()
        {
            MelonLogger.Msg("[POGContentLib] Initializing (MelonMod)...");

            // 1) Injected Il2Cpp types — BEFORE any AddComponent<T>.
            try
            {
                ClassInjector.RegisterTypeInIl2Cpp<ModItemHandle>();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[POGContentLib] ClassInjector failed: {ex.Message}");
            }

            // 2) Compatibility probe — verify the game members we bind to exist on this build,
            //    BEFORE PatchAll (so a game update surfaces as a clear log line, not a silent break).
            CompatibilityProbe.Check();

            // 3) Shared core services (reachable from the static Harmony patches).
            CoreServices.Init();

            // 4) Built-in domain plugins (each is a separate module).
            PluginRegistry.Add(new ItemsPlugin());
            PluginRegistry.Add(new POGContentLib.Shops.ShopsPlugin());
            // TODO(v2): PluginRegistry.Add(new MobsPlugin());
            // TODO(v3): PluginRegistry.Add(new LevelsPlugin());

            // 5) Build the graph and run Boot (register services/handlers).
            PluginRegistry.Boot();

            // 6) Harmony: NGO lifecycle hooks (Start*/Shutdown) + the reward-chest save guard.
            //    Patch each class SEPARATELY: PatchAll aborts the whole batch on the first failure,
            //    which once cost us every patch in the assembly because of one bad parameter name.
            //    Isolating them means a single incompatible patch degrades that one feature only.
            PatchEachClassIndependently();

            // 7) In-game shop tools (off unless [POGContentLib_ShopTools] Enabled = true).
            POGContentLib.Shops.Dev.ShopDevTools.Init();

            MelonLogger.Msg("[POGContentLib] Boot done. Waiting for scene + session.");
        }

        /// <summary>
        /// Apply every [HarmonyPatch] class in this assembly one at a time, so a patch that the
        /// current game build rejects cannot take the others down with it. Logs exactly which class
        /// failed and why — the actionable form of "the game updated".
        /// </summary>
        private void PatchEachClassIndependently()
        {
            int ok = 0, failed = 0;
            foreach (var type in typeof(Plugin).Assembly.GetTypes())
            {
                if (type.GetCustomAttributes(typeof(HarmonyPatch), true).Length == 0) continue;
                try
                {
                    new PatchClassProcessor(HarmonyInstance, type).Patch();
                    ok++;
                }
                catch (Exception ex)
                {
                    failed++;
                    // Walk the whole chain: Harmony wraps the real reason ("parameter X not found",
                    // "unsupported type"…) two or three levels deep behind "IL Compile Error".
                    var reason = new System.Text.StringBuilder();
                    for (var e = ex; e != null; e = e.InnerException)
                        reason.Append("\n    ").Append(e.GetType().Name).Append(": ").Append(e.Message);
                    MelonLogger.Error($"[POGContentLib] Harmony patch FAILED for {type.Name}:{reason}");
                }
            }

            if (failed == 0) MelonLogger.Msg($"[POGContentLib] Harmony: {ok} patch class(es) applied.");
            else MelonLogger.Warning($"[POGContentLib] Harmony: {ok} applied, {failed} FAILED (those features are off; the rest work).");
        }

        public override void OnUpdate() => POGContentLib.Shops.Dev.ShopDevTools.OnUpdate();

        public override void OnGUI() => POGContentLib.Shops.Dev.ShopDevTools.OnGUI();

        private bool _patchesChecked;

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            if (!_patchesChecked) { _patchesChecked = true; CheckEachPatchOnce(); }
            // Game shell prefabs are now in memory — build any templates not built yet.
            PluginRegistry.Dispatch(PluginStage.SceneLoaded);
        }

        /// <summary>
        /// Every patch of this library must run once per call of the game's method. MelonLoader patches a mod's whole
        /// assembly by itself unless the assembly opts out, and <see cref="PatchEachClassIndependently"/> patches it
        /// again: then each prefix and postfix ran twice (found 2026-11-19: a client asked the host twice about one
        /// slot). Checked at the first scene, when every patching has happened; the log says either way.
        /// </summary>
        private void CheckEachPatchOnce()
        {
            try
            {
                int methods = 0, twice = 0;
                foreach (var original in HarmonyInstance.GetPatchedMethods())
                {
                    var info = HarmonyLib.Harmony.GetPatchInfo(original);
                    if (info == null) continue;
                    methods++;
                    var mine = info.Prefixes.Concat(info.Postfixes).Concat(info.Transpilers).Concat(info.Finalizers)
                        .Where(p => p.owner == HarmonyInstance.Id);
                    foreach (var g in mine.GroupBy(p => p.PatchMethod).Where(g => g.Count() > 1))
                    {
                        twice++;
                        MelonLogger.Warning($"[POGContentLib] Harmony: {original.DeclaringType?.Name}.{original.Name} has {g.Key.DeclaringType?.Name}.{g.Key.Name} {g.Count()} times.");
                    }
                }
                MelonLogger.Msg($"[POGContentLib] Harmony: {methods} game method(s) patched; {(twice == 0 ? "each patch once" : $"{twice} patch(es) more than once")}.");
            }
            catch (Exception ex) { MelonLogger.Warning($"[POGContentLib] Harmony: could not check the patches: {ex.Message}"); }
        }
    }
}
