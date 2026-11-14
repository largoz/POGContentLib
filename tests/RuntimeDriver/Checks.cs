// Checks the driver asserts (PASS / FAIL lines; run.sh counts the FAILs) and the two observations the
// pre-release gate asks for: G2 (the game's network prefab list) and G3 (why ShopLocation.OnNetworkSpawn
// runs more than once). Enabled with checks=1 in driver.txt; the matching shop file and expect.txt are
// in tests/RuntimeDriver/checks/.
using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Il2Cpp;
using Il2CppGame.Spawning;
using MelonLoader;
using POGContentLib.Items;
using POGContentLib.Shops;
using Unity.Netcode;
using UnityEngine;

namespace POGContentLib.RuntimeDriver
{
    internal static class Checks
    {
        public const string Mod = "DriverChecks";
        public const string BreadSlot = "vanilla:PitOfGoblin_Lobby/StarterShop/Game.Spawning.ShopLocation Item_Bread";
        public const string PrefCategory = "POGContentLib_Shops", PrefWeight = "VanillaWeight";

        static int _pass, _fail;
        public static bool Enabled;
        public static string Summary => $"{_pass} pass, {_fail} fail";

        public static void Say(string msg) => MelonLogger.Msg("[DRIVER] " + msg);

        public static void Check(string name, bool ok, string detail)
        {
            if (ok) _pass++; else _fail++;
            Say($"{(ok ? "PASS" : "FAIL")} {name}: {detail}");
        }

        /// <summary>OnInitializeMelon: an item with no price, for the "sold for free" warning.</summary>
        public static void RegisterTestItems()
        {
            Content.Items.Register(new ModItemDefinition
            {
                ModId = Mod,
                ContentId = "FreeThing",
                DisplayName = "Free Thing",
                DisplayDescription = "Driver test item with no ShopCost.",
                ItemType = ItemType.Food,
                MapIcon = MapIconType.Food,
                Tint = new Color(1f, 0.3f, 0.3f),
                GoldValue = 5,
                Capabilities = { new EatCapability { CopyFrom = "Item_Apple", HealthOnEating = 1f } },
            });
        }

        /// <summary>Menu: the player setting the next scene load must pick up (0 = only mods' items).</summary>
        public static void SetVanillaWeight(double w)
        {
            var e = MelonPreferences.GetEntry<double>(PrefCategory, PrefWeight);
            if (e == null) { Check("setting exists", false, $"{PrefCategory}.{PrefWeight} not found"); return; }
            Say($"setting {PrefCategory}.{PrefWeight}: {e.Value} -> {w}");
            e.Value = w;
        }

        /// <summary>In the hub, after the slots spawned: what the setting and the rules did to the bread slot.</summary>
        public static void HubChecks()
        {
            var records = POGContentLib.Items.Content.Shops.Slots;
            if (!records.TryGetValue(BreadSlot, out var rec)) { Check("bread slot stocked", false, "no record for " + BreadSlot); return; }
            var vanilla = rec.Resolved.Entries.Where(e => e.AddedBy == "vanilla").ToList();
            Check("vanilla weight setting reached the pick", vanilla.Count > 0 && vanilla.All(e => e.Weight == 0),
                  "vanilla entries: " + string.Join(", ", vanilla.Select(e => $"{e.Item} x{e.Weight}")));
            Check("vanilla item not drawn at weight 0", rec.Picked != "vanilla:Item_Bread", "picked " + rec.Picked);
            Check("unknown item left out and noted", rec.Notes.Any(n => n.Contains(Mod + ":NoSuchItem")) && rec.Picked != Mod + ":NoSuchItem",
                  "notes: " + string.Join(" | ", rec.Notes));
            // One roll per slot object although OnNetworkSpawn repeats: run.sh counts the pick lines (expect.txt).
            Say($"G3 most OnNetworkSpawn calls on one slot object: {SpawnTrace.MaxCalls}");
        }

        // ---- G2: the network prefab list ----

        public static void DumpNetworkPrefabs()
        {
            var nm = NetworkManager.Singleton;
            var links = nm?.NetworkConfig?.Prefabs?.NetworkPrefabOverrideLinks;
            if (links == null) { Say("G2 no NetworkPrefabOverrideLinks"); return; }
            var hashes = new HashSet<uint>();
            int total = 0, shop = 0, nested = 0;
            foreach (var kv in links)
            {
                total++;
                hashes.Add(kv.Key);
                var go = kv.Value?.Prefab;
                if (go == null) { Say($"G2 prefab {kv.Key}: (no prefab object)"); continue; }
                bool root = go.GetComponent<ShopLocation>() != null;
                int inside = go.GetComponentsInChildren<ShopLocation>(true).Length;
                bool npc = go.GetComponentInChildren<NpcSpawner>(true) != null;
                if (root) shop++;
                if (!root && inside > 0) nested++;
                Say($"G2 prefab {kv.Key} {go.name}{(root ? " [ShopLocation]" : "")}{(!root && inside > 0 ? $" [{inside} ShopLocation inside]" : "")}{(npc ? " [NpcSpawner]" : "")}");
            }
            Say($"G2 total {total} registered prefabs; {shop} with ShopLocation on the root, {nested} with ShopLocations inside");

            // Independent of the library: every ShopLocation in a loaded prefab asset, and whether its hash is registered.
            foreach (var s in Resources.FindObjectsOfTypeAll<ShopLocation>())
            {
                if (s == null || s.gameObject.scene.IsValid() || ShopScene.IsTemplate(s.transform)) continue;
                var no = s.GetComponent<NetworkObject>();
                bool rootObj = s.transform.parent == null;
                Say($"G2 slot asset {s.gameObject.name} (root {s.transform.root.name}{(rootObj ? ", is root" : "")}) networkObject={(no != null ? no.GlobalObjectIdHash.ToString() : "none")} registered={(no != null && hashes.Contains(no.GlobalObjectIdHash))} tag={(s.m_priceTagText != null)} holder={(s.m_itemHolder != null)}");
            }
        }
    }

    // ---- G3: every OnNetworkSpawn of a slot, with the caller's stack for the first few repeats ----

    [HarmonyPatch(typeof(ShopLocation), nameof(ShopLocation.OnNetworkSpawn))]
    internal static class SpawnTrace
    {
        static readonly Dictionary<int, int> _calls = new Dictionary<int, int>();
        static int _stacks;
        public static int MaxCalls => _calls.Count == 0 ? 0 : _calls.Values.Max();

        static void Prefix(ShopLocation __instance)
        {
            if (!Checks.Enabled) return;
            try
            {
                int id = __instance.GetInstanceID();
                _calls.TryGetValue(id, out int n);
                _calls[id] = ++n;
                var no = __instance.NetworkObject;
                Checks.Say($"G3 OnNetworkSpawn #{n} {__instance.gameObject.name} inst={id} netId={(no != null ? no.NetworkObjectId : 0)} " +
                           $"isSpawned={__instance.IsSpawned} sceneObject={(no != null ? (object)no.IsSceneObject : "-")} scene={__instance.gameObject.scene.name} frame={Time.frameCount} t={Time.realtimeSinceStartup:F2}");
                if (_stacks < 4 && (n >= 2 || _stacks < 1))
                {
                    _stacks++;
                    string trace = StackTraceUtility.ExtractStackTrace();
                    Checks.Say($"G3 stack for #{n} {__instance.gameObject.name}:\n" + string.Join("\n", (trace ?? "(none)").Split('\n').Take(30)));
                }
            }
            catch (Exception ex) { Checks.Say($"G3 trace failed: {ex.GetType().Name}: {ex.Message}"); }
        }
    }
}
