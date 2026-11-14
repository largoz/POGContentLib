# POGContentLib — Roadmap

Modular community content framework for **Pit of Goblin** (Unity IL2CPP, MelonLoader 0.7.2,
Unity Netcode for GameObjects). One package, modular inside: a stable `Core` plus per-domain
modules (`Items`, `Mobs`, `Levels`, `Effects`) each with its own versioned data contract.

Design rationale and the reverse-engineering that backs it: see
the content-framework research notes
(§2.5 lists the statically-confirmed facts this roadmap relies on).

---

## Pre-release gate — checks and rework before anything is published (review of 2026-11-14)

Nothing of the Shops module is pushed or published. Until the first release the file format and the ids
are free to change; after it they are a one-way door (modders ship files). As of 2026-11-14 Thunderstore
lists 11 packages for the game and **none adds items or shops**: this library is the first of its kind, so
no other content mod exists to share a slot with yet. The multi-mod rules (patch / requires / history) are
built ahead of need; the foundation matters more. The first real consumers are our own content packs.

### Game update 2026-11-14 (BuildID 26652532): the library is broken on it

Found by the first asserting driver run (`tests/RuntimeDriver/checks/`), 2026-11-14:
- **The game moved its spawners to Addressables.** `SpawnerBase` now has `m_networkPrefabRef`
  (`AssetReferenceGameObject`), `m_prefabHandle`, `m_loadedPrefab`, `LoadNetworkPrefab()`, `PreloadNetworkPrefab()`;
  `ShopLocation` has `m_lootTableRef` (`AssetReferenceT<InventoryLootTable>`), `LoadLootTable()`, `CurrentShopItem`,
  `SetShopItemRpc(NetworkObjectReference)`, `ResolveShopItemOnClient()`, and `CreateShopItems()` is a coroutine.
  `m_networkPrefab` and `m_lootTable` are gone: the shop hook throws `MissingMethodException` on every slot (26 in the
  hub) and the game keeps its own choice.
- **No mod item is built:** `Shell not in memory yet: Item_Diamond`, `Network prefabs registered: 0 ok`. Shells are
  found with `FindObjectsOfTypeAll`, and item prefabs are no longer in memory before they are loaded by address. This
  breaks the Items module, so everything else waits for it.
- `ShopLocation.OnNetworkSpawn` ran **once** per slot on this build (26 calls, no second call). G3 may be gone with
  the update; one run, not settled.
- The compatibility probe listed only `ShopLocation.m_lootTable` (1 of 90): `m_networkPrefab` is not in its list.
- Separate, not the library: since the Proton Experimental update of the same night (experimental-11.0-20261112),
  MelonLoader hung at start twice (loader lock, before any log line) and started once with
  `--melonloader.hideconsole`. One launch each way; not settled.

Port first, in this order: Items (load shells by address, or find where the game keeps them), then the shop hook on
the new pipeline (read `CreateShopItems` / `LoadNetworkPrefab` / `HasNetworkPrefab` first), then the gate below.

### G. Check first (in this order; each one decides something)

- [ ] **G1 Two players on the library's foundation** (= Milestone 0.3, never run since v0.1). Decides whether
      prefabs registered at runtime reach a client at all. If they do not, the core needs rework before
      shops do, and the rest of this list changes.
    - Try two instances on one machine first: one through Steam, one in the offline wine prefix (testing
      notes §3.2). The game has `NetworkHandler.HostServerDebug` / `JoinServerDebug` and `HostDiscovery` /
      `JoinDiscovery` (LAN); they are in the proxies, whether they work is unknown. Memory for two
      instances on a 16 GB box is unmeasured.
    - Otherwise: a second PC with a second Steam account (`RUNTIME_TESTS.md`).
- [ ] **G2 Dump the game's network prefab list** at session start (the driver prints
      `NetworkManager.NetworkConfig.Prefabs`, name + hash). Decides whether a new slot prefab is needed at
      all: if the standalone vanilla slot prefab (the `ShopLocation` the templates are copied from) is
      already registered, the host can spawn that one as is (see R1).
- [ ] **G3 Why `ShopLocation.OnNetworkSpawn` runs twice per hub load.** Log a stack trace, `IsSpawned` and
      the frame in the prefix. Today a memo hides the symptom. Decides whether slots are re-spawned during
      a session and what "one roll per spawn" really means.
- [ ] **G4 A purchase.** A mod item from a vanilla slot and from a new slot: gold taken, the item in the
      inventory and usable, reputation up by round(price × 0.3). Then theft, and the seller's death making
      a new slot's item free.
- [ ] **G5 Random sellers in permanent locations.** Seen 2026-11-12: `InBetween_RandomShopkeeper_Bango_01(Clone)`
      in InBetween and `EntranceCave_Shopkeeper_Foodlover(Clone)` in DarkBurg, so hub sellers are drawn from
      a list too. The mechanism was not read (`ScenePropSpawner` is the likely place). Matters twice: a
      slot's anchor or seller may be absent on a given visit, and it is the route to mod sellers in hubs.
- [ ] **G6 A real pack instead of the sample.** Rebuild POGCustomStones on the current library (the "Port
      POGCustomStones" item below): the API on real content, use handlers included. Eat a food item built
      with the rewritten `EatCapability`.

Before any launch: copy the Steam remote save folder and the save folder in the prefix. The 2026-11-12 runs
had no backup. What can be said afterwards: no new save files appeared, Steam's stats cache was last written
on 2026-11-10 (before the runs), and the account metadata file was rewritten with the same size (cosmetics
and settings only; its content before and after was not compared).

### What the 2026-11-12 run did not verify

- **The distribution.** "50 % / 50 %" in the logs is the computed chance. No frequency was measured.
- **A purchase, a theft, a seller's death.** Only the "Buy" prompt was seen.
- **The tools under a human's hands.** Every action was called from code by the test driver. No key and no
  button was pressed; the panel was only drawn. (Legacy `Input` works in this game: POG Config uses it.)
- **Eating.** `EatCapability` was rewritten and its item was never eaten.
- **A client.** Nothing.

### R. Rework (decide after G1–G3)

- [ ] **R1 Shop files as host-only data.** Today each new slot is its own network prefab, so every player
      needs identical shop files or the join fails with no explanation (ForceSamePrefabs). The tools'
      working copy in `UserData` turns that into a trap: save a draft, forget it, and you cannot join a
      friend. Target: one shared slot prefab (or the vanilla one, G2), with the slot id and the price-tag
      offset sent host → clients in a named message (`CustomMessagingManager`, the `HostBridge` item),
      late joiners included. A client without the file still sees the slot.
- [ ] **R2 Dungeon merchants without touching the entrance list.** Today a merchant adds copies of its preset
      to `PropList_EntranceCave_MerchantOrAdventurer`. Every mod then shrinks the dead adventurers' share
      (4 of 10 → 4 of 20 with ten mods), and by the code read the same seed draws a different entrance
      than vanilla. Choose the variant only after the game has drawn the preset. Cost: a mod merchant
      appears at most as often as its preset.
- [ ] **R3 The tools as a separate mod** on the public API. Today 630 lines of IMGUI ship to every player and
      reach into internals. The switch belongs on a POG Config page (as the design said), not in a
      hand-edited cfg. Add the standing-point editor: the `items` section exists in the format and no tool
      writes it.
- [ ] **R4 "Sell at seller X" as the main path.** `add` into a fixed vanilla slot displaces the vanilla item: at
      weight 10 the starter shop has no bread half the time. Main path: the library places a new slot at
      the seller. `add` into someone's slot stays for deliberate competition, with a warning in `SHOPS.md`.
- [ ] **R5 Vanilla ids that survive a game update.** A slot id holds the object path and the default item's
      name; slot counts already changed between builds. Address a slot as seller flag + index, keep the
      path as a fallback.

### Silent failures (a modder gets no log line today)

Done 2026-11-14 (`Shops/ShopChecks.cs`, `ShopCatalog.CheckModSlotTargets`), each logged once per run with the
closest existing ids. In game = seen in the asserting run on the 2026-11-14 build.
- [x] A rule targets a vanilla slot id that matches nothing in its scene: warned 5 s after the scene's slots spawned,
      closest ids listed (in game). A rule for a mod slot the mod does not define: warned at load (in game, unit test).
- [x] An unknown item id: warned before the session (mod items) or when a slot uses it (in game); the dev tools show
      the slot's notes. **Wording wrong for one case:** an item that is registered but whose template was not built
      (2026-11-14: every item) is reported as "not installed, or misspelt". Ask the Items module and say which.
- [~] `ShopCost` 0: warned before the session and when a slot uses it. **Not seen in game:** the test item could not
      be built on the new game build.
- [x] The vanilla weight is a player setting, `[POGContentLib_Shops] VanillaWeight` (host, next scene; read in game).
      Its effect on a pick is unit-tested only: the pick itself is broken on the new build.

### Smaller defects

- [x] **One weighted pick** (2026-11-14): `Model/WeightedPick.Index`, used by `ResolvedSlot.Pick`, `ShopRuntime` and
      `SellerTemplates`. Unit tests measure the distribution (400 000 draws) and the half-open boundaries; each was
      shown to fail on a planted defect (closed interval; negative weights counted).
- [x] The slot memo, the empty-tag set and the merchant variant are keyed by `GetInstanceID()` (2026-11-14; compiles,
      not exercised in game: stocking is broken on the new build).
- [ ] Standing point: the "bottom centre of the mesh" branch is probably dead. The four prefabs checked have
      their standing point at the pivot (other shells not checked), which is wrong for an item whose model
      was replaced. Recompute when the visual was replaced.
- [ ] A pool slot touched by a rule goes through the fixed-item path: with a dead seller it stays empty where
      vanilla would still draw from the pool (from the code read; not tested).
- [ ] `EatCapability` fields became nullable: packs built on 0.1.0 must be rebuilt (binary-incompatible).
- [x] The runtime driver asserts (2026-11-14): `checks=1`, `checks/expect.txt` checked by `expect.sh`, `run.sh` exits
      with the number of failures and refuses a log older than the launch (a stale log had passed three checks).

### Order

1. Silent failures and the single pick: small, safe, independent of everything else.
2. G1 and G2, then decide R1 and R2.
3. G3–G6.
4. R3–R5, then the first release.

---

## Milestone 0 — De-risk (requires launching the game; done by hand)

These cannot be verified without running the game in two instances — the framework is coded
to the statically-confirmed facts, but these three must be validated before trusting them:

- [ ] **0.1 ForceSamePrefabs** — already resolved statically to `TRUE`
      (NetworkConfig+0x6C). Confirm at runtime via a config log on session start.
- [ ] **0.2 AssetBundle round-trip smoke test** — build an `HDRP/Lit` cube in Unity
      2022.3.62f2, load it via `AssetLoader.LoadBundle`, spawn it in-game. Round-trip is not
      yet proven anywhere in this project; this gates the full bundle visual pipeline.
- [ ] **0.3 Two-player registration test** — host + client, confirm a deterministic-hash mod
      prefab registers on both peers and the client sees the real mod item (not the shell),
      and that a vanilla client is rejected at connect (expected, given ForceSamePrefabs=true).

---

## v0.1 — Core + Items (current)

Framework skeleton compiles clean; correctness of the runtime paths pending Milestone 0.

- [x] Bevy-style plugin graph (`IPOGPlugin` / `PluginGraph` / `PluginRegistry`) driven by real
      MelonLoader + NGO lifecycle stages (Boot / SceneLoaded / BeforeSessionStart / SessionStop).
- [x] `ContentRegistry` — deterministic `GlobalObjectIdHash` = MD5(modId:contentId), def-by-hash.
- [x] `ShellFactory` — clone a safe vanilla shell (Diamond default), assign the hash, strip
      the self-spawn chain; prefers clean asset instances over dirty live ones.
- [x] `NetworkRegistrar` + `NetworkLifecycleHooks` — register prefabs via a Harmony prefix on
      `StartHost/StartClient/StartServer` (the only legal window), post-verify via
      `NetworkPrefabOverrideLinks`, re-salt on hash collision, reset on `Shutdown`.
- [x] `LootInjector` — both table families (fixed `Il2Cpp.LootTable` array-realloc **and**
      `Il2CppInventory.InventoryLootTable` List.Add), targetable by category and/or table name
      (biomes, ore veins, shop pools, gambling, reward chests); host-side, zero consumer patching.
- [x] Full item identity: `ItemType` (slot routing) + `MapIcon` + `FoodValue`/`ShopCost` —
      driven by the item-system map (the item-system research notes).
- [x] `SaveGuard` — Harmony finalizer on `ItemContainer.FromContainerData` (the one save path
      that throws on an unknown hash → turns "lose all reward chests" into "lose one").
- [x] `Items` module — `ModItemDefinition`, host-authoritative use flow, `ModItemHandle`
      (hash-resolved state), Interact/OnNetworkSpawn Harmony patches.
- [ ] **Port POGCustomStones** onto POGContentLib as the reference content pack (validates the API).
- [~] **Minimal parity layer (multiplayer PRECONDITION, promoted from v0.2) — SCAFFOLDED, compiles.**
      Because `ForceSamePrefabs=TRUE`, mismatched content packs make co-op joins fail with an opaque
      "connection failed". The Lib does this self-sufficiently (no other mod required):
    - [ ] **2026-11-12, seen in every hosted test session: no Steam lobby callback fires**, so the log says
          "Parity/Steam bridge unavailable" and parity is off. The game hosts through an EOS lobby now
          (`NetworkHandler.m_onlineService` / `ServicesEOS`, see POGPlayerLimit's probe); move the manifest to
          EOS lobby attributes.
    - [x] `ParityManifest` — deterministic, order-independent snapshot of registered content
          (`modId:contentId@version` + an 8-hex token); versioned wire format; pure logic, no game dep.
    - [x] `ParityService` — host advertises the manifest, a joining client reads and compares the
          host's; a mismatch is logged AND raised via the public `Content.Parity.OnMismatch` event
          (`ParityReport`: Missing / Extra / VersionMismatch). Fallback is a plain MelonLogger line.
    - [x] Channel = **Steam lobby metadata** (`NetworkHandler.Singleton.Lobby.SetData/GetData`, key
          `pog_parity`), read *before* connecting so it touches no NGO state. The NGO
          ConnectionApproval/`DisconnectReason` path is intentionally NOT used (vanilla already sets
          `ConnectionApproval=TRUE`; wrapping the game's own approval callback is riskier).
    - [x] `ParitySteamBridge` binds the Facepunch interop **reflectively** (guarded, degrades to one
          log line); `CompatibilityProbe` now name-checks those members too.
    - [ ] **RUNTIME-TODO (Milestone 0):** confirm `NetworkHandler.Singleton.Lobby` is populated and
          host-written metadata is visible to the joiner at the Start*/join moment we hook.
      Rich presentation is the optional POGConfig UI in v0.2. Validate in the 2-player test.
- [ ] Run Milestone 0; fix whatever the two-player test reveals.

## v0.2 — Items hardened + assets

- [~] **AssetBundle visual pipeline — IMPLEMENTED, unproven in-game.** `ItemVisual.Bundle(path, asset)`
      instantiates a custom prefab as the item's visual (with placement via `.At(offset, euler, scale)`)
      and disables the shell's renderers. Engine-side effects ride along inside the prefab (particles,
      lights, trails, animation); custom C# MonoBehaviours cannot survive IL2CPP and are dropped by
      Unity — logic belongs in capabilities/use handlers. Materials that lost their shader are repaired
      onto `HDRP/Lit` (same-named properties survive the swap). Modder guide: `CUSTOM_ASSETS.md`.
      Gated on the Milestone 0.2 round-trip test.
- [ ] Loose-file tier polish: PNG icons, OGG audio, OBJ meshes (no Unity Editor needed).
- [ ] `HostBridge` client→host use requests via `CustomMessagingManager` named messages
      (so non-host players can trigger item effects).
- [~] **Shops module** (`Shops/`, design `docs/design/shops.md`, guide `SHOPS.md`, sample `samples/ShopDemo/`) —
      2026-11-12, works for a single host; seen in the real game through `tests/RuntimeDriver`. **Not ready
      to publish: see the pre-release gate at the top** for what was not verified and what to rework.
    - [x] Shop files (`pogshop.json`, format 1): parse with exact errors, typo warnings; unit-tested
          (`tests/Shops.Tests`, mutation-calibrated).
    - [x] Weights instead of percentages; add / patch (patch needs `requires`); dependency order; per-slot
          "who changed what" history. The model is unit-tested; the runtime's own pick is not (gate).
    - [~] Rules on vanilla slots: the host picks by weight and hands the game a fixed item. In game: a mod
          item and a vanilla item into fixed and pool slots, priced, linked to the slot, faction kept. No
          purchase was made and no distribution measured.
    - [~] New slots next to any seller (network prefab per slot, built before the session). In game: placed,
          stocked, priced, bound to the seller with its faction. No purchase; no client (gate R1).
    - [~] Standing points (`m_standingTransform`, what the game really uses; `TableGrip` is not) for mod items.
          Checked for an item that keeps the shell's model only.
    - [~] Dungeon merchants as weighted variants of a vanilla preset (no new prefab). In game: one dungeon,
          the mod merchant selling its own stock (gate R2 changes how the weight works).
    - [~] In-game shop tools: aim + place, nudge, panel with chances/history/items, Save, round-timer freeze
          (in game: frozen 8 s → elapsed +0.00), scene jumps (in game: Lobby → InBetween → DarkBurg). All of
          it driven from code by the test driver; no key or button was pressed by hand.
    - [ ] Two-player test: new slots and merchant stock on a client (gate G1).
    - [ ] Shop files in the multiplayer parity manifest (the parity layer itself needs the EOS lobby, below).
- [~] **Item capabilities — SCAFFOLDED, compiles.** Declarative `ItemCapability` on a definition
      attaches the sibling `ActiveItem_*` component and sets its scalar fields (item anatomy §2).
    - [x] `EatCapability` (health/stamina/satiety), `MeleeWeaponCapability` (durability + hit),
          `ThrowableCapability` (health/bounce); attached in `ItemsPlugin.ApplyCapabilities`, guarded.
    - [x] Handling / "weight" — `Big` / `PickupStaminaPenalty` / `MovementFactorWhenHeld` (plain
          `InventoryItem` fields; statically sound, not a component). There is no real weight field.
    - [ ] **RUNTIME-TODO (Milestone 0):** a bare shell lacks the components' serialized refs (VFX/
          sounds/curves) and their spawn-time NetworkVariables (durability/ammo/health seed); confirm
          the scalar effects fire and the added NetworkBehaviours stay in sync across peers.
    - [x] `GlowCapability` — glow/pulse the way vanilla does it (item anatomy §9): a child `Light`
          (colour / intensity / range), optional emissive material tint, optional pulse via the game's
          own `LightFlicker` (strength / duration / vibrato / randomness).
- [x] **Open visual-effect system (no hardcoded effects)** — `ItemCapability.Attach` is public, so a
      third-party mod implements effects through the *same* contract the built-ins use (no privileged
      core). `ItemVisualEffect` base gives `CreateEffectChild` (reused on rebuild, never stacks) and
      `GetVisualRoot` (follows a custom mesh). `ParticleEffect` covers the no-code cases: clone a
      vanilla VFX (`FromGameObject`) or instantiate your own particle prefab (`FromBundle`), with
      placement and recolour. `Content.Diagnostics.ListVfx(filter)` — or a `vfx:<filter>` line in
      `UserData/pog_probe.txt` — finds vanilla effects to reuse. Guide: `VISUAL_EFFECTS.md`.
    - [ ] Torch/light + weapon NetworkVariable seeding via an `OnNetworkSpawn` step (needs a donor
          prefab for the light/VFX refs) — v2.
- [x] **`VisualProbe` + `Content.Diagnostics`** — prefab-serialized visual data (light colour/
      intensity/range, flicker settings, emissive materials) is invisible to static analysis, so this
      logs it from a live instance. Drive it from code (`Content.Diagnostics.ProbeItem("Item_Speaking
      Stone")`) or with no code at all via `UserData/pog_probe.txt` (one prefab name per line). Use it
      to capture real numbers to feed into `GlowCapability`. Read-only; also reports HDRP intensity.
- [ ] Per-item state persistence: slot-keyed companion file (JsonUtility drops unknown fields,
      so mod state cannot live in the vanilla save).
- [ ] **Rich parity UI (optional, soft-dep)** — POGConfig (if installed) subscribes to the Lib's
      `OnParityMismatch` event and renders a native panel (installed packs, add/update/remove,
      shown *before* joining). Presentation only; detection stays in the Lib. NOT a separate mod.
- [ ] Retire legacy **POGCustomItems** to `archive/` once Items reaches parity
      (POGSpawner must be re-pointed at POGContentLib first).

## v0.3 — Mobs module

Same shape as Items (shell-clone + deterministic hash), confirmed feasible in the research.

- [ ] `ModMobDefinition` { shell, visuals, stats, dangerLevel, maxAlive, behavior }.
- [ ] Shell clones from `SkullEntity`/`RatEntity` (cheapest) / `SkeletonEntity` (humanoid).
- [ ] Spawn-table injection into `EnemyLootTable` arrays (fixed → array-realloc) and/or
      `SetEnemyLootTable`/`SetEnemyConfig` before `StartGame` on the host.
- [ ] Managed FSM API layered on the stock brain (`FSM.AddState/AddTransition(Func<bool>)`).
- [ ] Danger-budget balance via `m_dangerLevel` / `m_maxSpawnAmount` (no director patches).

## v0.4 — Levels module (tier a+)

Dungeon geometry is seed-local (no per-object network spawn), so custom piece-based dungeons
need no NGO scene-hash patching.

- [ ] `ModLevelDefinition` via runtime `DungeonDescriptor` + community `DungeonPiece` prefabs.
- [ ] Inject descriptors into `LevelInfoData.m_dungeons` rotation.
- [ ] Resurrect `UIDebugDungeonVisit` (or clone its flow) as a deterministic-seed test launcher.
- [ ] Consider splitting Levels into its own package if it grows an editor SDK.

## Later / stretch

- [ ] `Effects` module.
- [ ] Whole custom **scenes** from AssetBundles (tier b) — needs symmetric NGO
      `NetworkSceneManager` hash handling (`VerifySceneBeforeLoading` is a public settable field).
- [ ] Codeless content packs (manifest + bundle, folder auto-discovery) — the ecosystem-growth
      step; restrict bundles to vanilla-component prefabs under IL2CPP.
- [ ] POGConfig integration: in-game "installed content packs" panel + parity-mismatch UX (soft dep).
- [ ] Thunderstore release: framework package + content packs as pinned dependents.

---

## Known limitations (current)

- Runtime correctness of network registration, save behavior and bundle round-trip rests on
  static decompilation until Milestone 0 is run in-game.
- Use effects are host-only for now (client→host RPC is v0.2).
- Loot injection targets `LootTable` category arrays; `InventoryLootTable` (per-chest/enemy)
  targeting is a later, more selective feature.
- Custom visuals are icon/tint level until the AssetBundle smoke test (0.2) passes.
