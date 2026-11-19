# POGContentLib — Roadmap

Modular community content framework for **Pit of Goblin** (Unity IL2CPP, MelonLoader 0.7.2,
Unity Netcode for GameObjects). One package, modular inside: a stable `Core` plus per-domain
modules (`Items`, `Mobs`, `Levels`, `Effects`) each with its own versioned data contract.

Design rationale and the reverse-engineering that backs it: see
the content-framework research notes
(§2.5 lists the statically-confirmed facts this roadmap relies on).

---

## Current plan (2026-11-19): the library working in co-op, and a mod-mismatch window

Every item is done only with proof from the game on both machines (PC host, laptop client, POGLanTest, laptop sound
off): screenshots of host and client, unit tests green, each defect found goes into this ROADMAP. No publishing or
pushing without the user's command; commits by Largo (noreply), no co-authors; back up saves before launches; undo
the PC's test changes after each session.

- [x] **P1 D8:** a mod item in a vanilla slot stands upright. **Done 2026-11-19 (run 7, 08:46):** host log
      "EmberLantern in '…Item_Torch_Unlit': stood upright (the slot tilted it 52°)", both berries "upright already
      (tilt 0°)"; the lantern stands on the counter in the host's and the client's screenshot
      (`pog-runs/lan-1/run7/host-look.png`, `client-after-pass.png`).
- [x] **P2 Buy and eat:** the client buys the glow berry (gold taken, berry in its inventory with its icon) and eats
      it (berry gone, effect in the log). POGLanTest: a client stuck on "Spawning Character..." rejoins.
      **Done 2026-11-19, run 9 (09:02), client log and screenshots:** gold 14 → 7; a new berry in its inventory
      slot 0, in hand, owner = the client (the bread slot sells copies, D9); its icon is the berry's own (256x256,
      D10); the "New Discovery" and "Item Purchased" toasts show the berry icon
      (`pog-runs/lan-1/run9/lap-shots/client-090304-04-bought.png`); primary action → health 100 → 110, the berry
      destroyed. Rejoin: with `SimulateStuckOnce` the client counted itself stuck 45 s after the hub, left through
      the game's own path (`NetworkHandler.Disconnect`, `NetworkMenu.OnDisconnected` → main menu), joined again
      6 s later, got its player and did the whole test after that. (Run 8: leaving with `NetworkManager.Shutdown`
      alone kept the client in the hub scene, and every rejoin came back without a player: a POGLanTest defect,
      fixed.) A real hang came by itself in run 12 (09:20, the laptop's battery at 6%): no player 45 s after the
      hub, a black screen; the client left through the menu, joined again 9 s later, got its player and ran the
      whole big-item test after that.
- [x] **P3 Pass and bucket:** an item dropped by one player and picked up by the other, and put in the bucket, both ways.
      **Done 2026-11-19 (run 7, 08:48), both logs agree:** the client bought the `berry_stand` berry (gold 7 → 0) and
      dropped it; the host picked it up (owner 0) and put it in the bucket; the client saw it in the bucket (listed
      in `ItemsInBucket`), took it out (owner 1) and put it back; 30 s later the host and the client both see it in
      the bucket, listed, owner 1. The host → client hand-over was through the bucket, not the floor.
- [x] **P4 Client without the content mods** (POGLanTest only): what it sees; neither it nor the host breaks.
      **Done 2026-11-19, run 10 (09:06), both logs and screenshots:** the client (POGLanTest and POG Graphics only,
      645 network prefabs against the host's 648) was let in, NGO refused nothing, and both stayed connected for the
      whole run (three reports each, `clientsConnected=2`). The client's NGO logged "Failed to create object
      locally" four times, for the mod objects: `641707B8` (the `berry_stand` slot), `473DB177` twice (the glow
      berries) and `F65A65F8` (the Ember Lantern); it sees the bread and torch slots empty under their price tags
      "7" and "140" (D12) and no berry stand (`run10/lap-shots/client-090645-01-look.png`). The host logged no
      exception (`Player.log`), its items stayed in place; the client's 6 exceptions are its missing Steam
      (`SteamClient.get_Name`, achievements), as in every client run. Nothing tells either player what is missing:
      that is P7.
- [~] **P5 D5–D7:** bundle model under `PickupItem`/`HeldItem`, journal entries, big-item shell: fix, or record why not.
      D6 and D7 fixed and seen in game (runs 11–12, below). D5 not fixed, and why: see D5.
- [x] **P6 One library:** each file only in the old `_Modding/ModSource/POGContentLib` copy ported or rejected (reason
      written down), then the old copy archived. **Done 2026-11-19.** Ported: the OBJ tier (D2) and the OBJ section
      of `CUSTOM_ASSETS.md` (rewritten for build 26668562; the bundle section's Unity version corrected to
      6000.3.24f1). Rejected: `ShopFactory` and the old `ShopsApi` (the Shops module replaced them; written against
      fields the 2026-11-14 update removed), `CyrillicFallback`, `LocalizationProbe` and the font probes (POGFonts'
      job), `ColliderProbe` and `ProbeObj` (the OBJ loader logs the same facts), and older forms of code this
      repository has in a newer form. The table with each reason is in `_Modding/ModSource/_archive/README.md`; the
      copy is `_Modding/ModSource/_archive/POGContentLib-2026-08` (commit 54158ad there, not pushed).
      POGCustomStones and the old POGCustomItems copy now build against this repository; POGSpawner does too, but
      fails on the game, not the library: `Bucket.Depth` is gone (`WorldProbe.cs:51`).
- [ ] **P7 Mod-mismatch window** (a separate mod, own repo `largoz/<name>`): after connecting, host and client exchange
      their mod lists (id, version, DLL hash) by an NGO named message, not the Steam lobby; each mod is "required for
      everyone" (network content through POGContentLib, or listed in a config) or "own only" (the default); a window in
      game lists missing required mods, different versions and extra ones, does not block play, closes with a button,
      and is copied to the log.

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

**Port status (2026-11-14, afternoon), from `explore=1` runs and the asserting run `checks-2`:**
- What the game does now: nothing is in memory at the main menu; `NetworkHandler.EnsureNetworkPrefabsRegistered`
  (called from `WaitForDeferredAssets` right before `StartHost`) loads `Assets/Gameplay/Data/CoreNetworkPrefabs.asset`
  (647 entries, 218 items) by address; NGO registers it at `StartHost` (645 links in the hub). **`ForceSamePrefabs` is
  false.** Addresses are asset paths; the file name is the prefab name. A slot's item is made by the `CreateShopItems`
  coroutine a few seconds after the slots spawn, in one batch: fixed slot `LoadNetworkPrefab → CanSpawn →
  InstantiateNetworkPrefab → SpawnNetworkPrefab`, pool slot `LoadLootTable → GetRandomItem`.
- [x] Items: new stage `GamePrefabsLoaded` (postfix on `EnsureNetworkPrefabsRegistered`) builds the templates from the
      game's list (`Core/GamePrefabs`); the session-start prefix makes the game load it if it has not. In game: built and
      registered (`Network prefabs registered: 2 ok`).
- [x] Shop hook: a chosen slot looks like a fixed slot holding the choice (`ShopChoices` + prefixes on
      `HasNetworkPrefab`, `LoadNetworkPrefab`, `PreloadNetworkPrefab`, `HasLootTable`, `LoadLootTable`). In game: the
      starter shop's bread slot held `ShopDemo:GlowBerry`, read back from the game's `CurrentShopItem`.
- [x] Vanilla content of a slot is read by address (`Core/Addresses`: the fixed item's address, the pool's table loaded
      with its own handle and released).
- [x] Slots without an item reference (a pool slot, a new slot) did not take the choice: the game reads
      `m_networkPrefabRef` itself, so the `HasNetworkPrefab` prefix alone does not reach them. A chosen slot without one
      gets a stand-in reference (the persistent player prefab's GUID); `LoadNetworkPrefab` answers and the preload is
      skipped, so it is never loaded. A new slot placed after the scene's batch also starts its own coroutine
      (`StockIfMissed`).
- [x] Food items threw in `ActiveItem_ReadyItem.OnNetworkSpawn` when a shop spawned them (Unity log only): `EatCapability`
      left the base class fields null. Both capabilities now fill them (`ReadyItemFields`, end pose from the donor).
- [x] **Asserting run `checks-5`: 0 failures** (18 log expectations, 7 driver checks, Unity log free of exceptions): the
      game's own `CurrentShopItem` matches the choice in a fixed slot (mod item), a fixed slot made empty, a pool slot
      (mod item) and a new slot; the warnings, the setting and the registration as above. Host only.
- [x] Dungeon merchants (run `dungeon-1`): the entrance list grew 10 → 110, the Curiosa preset came up as the mod variant,
      and its slot 1 held the chosen mod item (the game's `CurrentShopItem`). The dev tools' round-timer freeze held the
      timer (0.00 s in 8 s frozen, 8.00 s running).
- [ ] Clients, the dev tools under a human's hands, eating a mod food item: not checked on this build.
- Unity's log in the dungeon has one `NullReferenceException` in `DestructibleInventory.SpawnItem → InventoryItem.Pickup`.
  No mod item is in any loot table in that run (the library injects loot only on request), so it is taken for the
  game's own; not checked against a run without mods. The Unity-log expectations now look only for frames of code the
  library feeds (`ShopLocation`/`SpawnerBase`/`ItemSpawner`, `ActiveItem_*`); calibrated: they fail on the run with the
  food bug and pass on this one.
- [ ] Runs stall on the loading screen under heavy load (62 and 28 seen from another session's builds); queue runs
      behind `load < 10`.

### G. Check first (in this order; each one decides something)

- [~] **G1 Two players on the library's foundation** (= Milestone 0.3, never run since v0.1). Decides whether
      prefabs registered at runtime reach a client at all. If they do not, the core needs rework before
      shops do, and the rest of this list changes.
    - **2026-11-19, game build 26668562, LAN (POGLanTest, `_research/lan-test`): they do.** Host = this PC (Steam,
      Gale), client = the Windows laptop (no Steam, r2modman), UnityTransport, the host started through the game's
      own save start with the EOS relay bypassed. Both registered `ShopDemo:GlowBerry` under the same hash
      (473DB177; 647 prefabs each). The client's `CurrentShopItem` matched the host's in all 14 hub slots: the
      bread slot and the new `berry_stand` slot (a network-spawned slot prefab on the client) hold the glow berry
      at price 7, the 12 vanilla slots the same items and prices. Seen on both screens (screenshots in
      `/run/media/tim/Games/pog-runs/lan-1/`). A client joining the running hub (late join) got the same.
    - **Defect found:** on the client, mod item instances carry `hideFlags = HideAndDontSave` (host: `None`),
      inherited from the library's template. `FindObjectsOfType` does not return them there, and by Unity's rules a
      `DontSave` object is not destroyed when a scene unloads (not observed yet). Clear the flags on spawned
      instances.
    - Not done yet: a purchase or a theft by the client, eating, passing an item between players, a client
      without the mods. POGLanTest has a client buy-and-eat step (`[LAN-BUY]`: `Interact` on the bread slot's berry,
      `ChangeSlot`, the primary action); its one run did not reach the purchase, see the next line.
    - Run 5 (2026-11-19 04:18): the client joined while the host went from the menu lobby into the hub, NGO threw
      `Server Scene Handle (-24508) already exist! ... scene load of PitOfGoblin_Lobby` on the client, and the client
      stayed on "Spawning Character..." (no local player). Run 4 joined at the same point without it. Not the library
      as far as the logs show; the test tool should join only once the host is in the hub, or retry.
    - Client-side noise, not the library: without Steam the client logs Steamworks/LootLocker NREs, and a
      `PushToTalk`/Dissonance NRE every frame. Joining before the main menu is up leaves the client's player
      half-built (no camera); POGLanTest now waits for the menu.
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
      the slot's notes. **Wording wrong for one case (fixed 2026-11-14, not seen in game since nothing is unbuilt now):** an item that is registered but whose template was not built
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

### Defects found 2026-11-19 (LAN test, the decompile, the old library copy)

Open unless marked. "In game" = seen on build 26668562; "from the code" = read, not run.
- [x] **D1 Client copies of mod items carry `hideFlags = HideAndDontSave`** (in game, on the client; the host's copies
      have `None`). `ShellFactory` sets the flags on the template itself, not only on its holder (`SlotTemplates` sets
      them on its holder). On the client `FindObjectsOfType` misses those items, and Unity does not unload `DontSave`
      objects with their scene (not observed yet). Fix: clear the flags on every spawned instance of a mod prefab
      (items and slots), and check host and client both report `None`.
      **Fixed 2026-11-19** (`Core/TemplateCopies`, called from the item and slot spawn hooks). In game, next run: host
      and client report `None` for both glow berries, the lantern and the `berry_stand` slot, and the client's
      `FindObjectsOfType` now finds the 2 berries it missed.
- [x] **D2 OBJ models are lost from this repository.** The 2026-09 copy of the library in
      `_Modding/ModSource/POGContentLib` has `Core/ObjMesh.cs` (OBJ → mesh at runtime, several materials, `UCX_`
      collision, handedness), `ItemVisual.Obj`, `ItemVisuals.ReplaceMeshes(target, mesh, materials)` (the mesh goes
      into the shell's own renderers, so it stays under `PickupItem`/`HeldItem`) and `ItemVisuals.ApplyColliders`.
      This repository has none of them, and POGCustomItems 2.0.0 (Ember Lantern, `ItemVisual.Obj`) cannot be built
      against it (from the code). The same copy also has `ColliderProbe`, `CyrillicFallback` and `LocalizationProbe`,
      not here; decide each. Two copies of one library diverged: keep one.
      **Restored 2026-11-19:** `ObjMesh`, `ItemVisual.Obj`, the mesh swap with materials, `ApplyColliders`, and
      `GlowCapability.MaterialFilter` (lost too); `PhysicMaterial` is `PhysicsMaterial` in Unity 6. POGCustomItems builds
      again (its `.csproj` used paths relative to the old Windows layout; now `GameDir`). In game, host and client: the
      Ember Lantern (1116 vertices, 3 materials, 4 `UCX_` hulls) and the glow berry load, the mesh goes into the
      shell's 2 renderers, and both stand in the starter shop on both screens. Still to decide: `ColliderProbe`,
      `CyrillicFallback`, `LocalizationProbe`, and 158 lines of `DiagnosticsApi` that only the old copy has.
- [x] **D3 The ShopDemo sample has no model**: its glow berry is the diamond shell tinted green, so on screen it is a
      green gem (in game, host and client). A sample should show a custom model. **Fixed 2026-11-19:**
      `samples/ShopDemo/model/GlowBerry.obj` (a berry cluster made by `make_glowberry.py`), shipped next to
      `pogshop.json`; seen on host and client.
- [~] **D4 An item without `DisplayName` keeps the shell's tooltip id** (from the code): it is called "Diamond" and
      counts as discovered with the diamond. Also in v0.2 below. **Fixed in code 2026-11-19** (always our own keys,
      the content id as the name); an item without a name was not run in game.
- [ ] **D5 `AttachBundlePrefab` parents the model under the item's root** (from the code): visible in the inventory,
      wrong layer in the hands, and the shell's renderers come back on `SetVisibility(true)`. Also in v0.2 below.
      **Not fixed (2026-11-19), and why:** the fix (the model under `PickupItem`, a copy under `HeldItem`, the
      shell's renderers removed rather than switched off) can only be shown with a real AssetBundle, and none
      exists: building one needs the game's Unity editor (6000.3.24f1), which is not installed here and needs the
      user's Unity account. The whole bundle tier is unproven on this build anyway (Milestone 0.2). The recipe
      stays in the v0.2 list; do it together with the first bundle.
- [x] **D6 Mod items are never in the journal** (from the code: the game builds no item entries). Also in v0.2.
      **Fixed 2026-11-19** (`Items/JournalEntries.cs`): each journal tab's entries are placed in its UI
      (`UI_JournalGroupItems.m_entries`); right before a tab initializes, every mod item for that tab gets a copy
      of one of its entries with the item's `JournalEntryDataItem` (name and description terms, icon, values) and
      picture. New: `ModItemDefinition.JournalGroup` (default from `ItemType`) and `ShowInJournal`. In game: run 11,
      host and client, the berry listed under Food, shown after it was bought (client) and picked up (host);
      run 12, the client's journal opened at the sack, its name and description in the detail page
      (`run12/lap-shots/client-092219-04-journal.png`). An item with no icon of its own now takes its shell's for
      the entry (the sack showed another entry's picture; built, not run yet).
- [x] **D7 No big-item shell** (from the code): `Big = true` keeps the diamond's grips and size. Also in v0.2.
      **Fixed 2026-11-19:** `ItemShellKind.BigItem` = `BigItem_Decoration_01` (the starter shop's fur roll: grips
      for both hands, `ActiveItem_Throwable`; its `BucketUpgrade` is taken off the template, or the mod item would
      decorate the bucket). `Big = true` on the default shell picks it. The sample has a big item (Sack of Glow
      Berries). In game, run 12: built on that shell on both peers, sold in the decoration slot; the client bought
      it (gold 14 → 7): in hands, `m_isBigItem` true, `BucketUpgrade` 0, grips LeftHand/RightHand/Generic/
      Backpack/Table/ItemRack; dropped again. The vanilla big items, for reference: `BigItem_Decoration_01` and
      `BigItem_TreasureDisplay` carry `BucketUpgrade`, `BigItem_Scrap_Cog` is the game's 'BigCog'.
- [ ] **D8 A mod item in a vanilla slot takes the pose that slot gives its own item** (in game, host and client,
      2026-11-19): the Ember Lantern lies on its side in the torch slot, where the torch, the key ring and the rock lie
      flat too; the glow berry stands upright in the bread slot. The game places the item by the slot's holder
      (`ShopLocation.get_SpawnTransform`), so a holder posed for a lying item lays down anything. Not the mesh: the
      diamond's mesh child was turned only 6.6° off upright (`ItemVisuals.ReplaceMeshes` now sets a custom mesh upright
      to the item, logged; that alone did not change the picture). Fix: on every peer, after the slot places a mod item
      (`SetShopItemInternal`), keep the holder's position and yaw and stand the item upright, or take the pose from the
      shop file's `items` section; then the standing point at the model's bottom centre (Smaller defects).
      **Tried 2026-11-19, did not work:** a postfix on `ShopLocation.SetShopItemInternal`
      (`Patch_ShopLocation_SetShopItemInternal`, still in the code) never ran on the host: the decompile has one call
      site, the client's `ResolveShopItemOnClient`; the host sets its item another way. The host decides the pose
      (it is synced to clients), so the next try is there: the spawn pose (`ShopLocation.get_SpawnTransform`, used
      when `SpawnerBase` instantiates the chosen prefab).
      **Tried again, did not run:** a postfix on `SpawnerBase.InstantiateNetworkPrefab` logged nothing (run 6;
      `CreateShopItems` reaches it through the vtable). **Fixed 2026-11-19:** the pose is `get_SpawnTransform` =
      holder × inverse(item's `m_standingTransform`), and `CreateShopItems` then calls
      `ClientNetworkParenting.SetParentBeforeSpawn(slot)`, which records the pose relative to the slot that every
      client applies. A prefix there stands a mod item upright about its standing point. In game, run 7: P1 above.
- [x] **D9 Buying a mod item from a slot that sells copies takes the gold and gives nothing** (in game, run 6:
      gold 14 → 7, the berry stayed in the slot). Such a slot (`ItemSpawner.m_setSpawnCopyOnSpawn`, e.g. the starter
      shop's bread slot) hands the buyer a new item via `InventoryItem.SpawnCopyAndPickup`, which asks
      `NetworkHandler.GetNetworkPrefab(hash)`; that walks only the game's own prefab list, so NGO logged "The
      NetworkObject prefab parameter was null!" and `BuyItem` threw after `PurchaseLocal` (host `Player.log`).
      **Fixed 2026-11-19:** a postfix answers a mod hash the game's list does not know with the mod's template
      (`Patch_NetworkHandler_GetNetworkPrefab`). In game, run 7: the copy reached the client's inventory (P2).
- [x] **D10 An item with an OBJ model has the shell's icon** (in game, run 7: the client's "Added to …" toast shows
      the diamond). `ItemVisual.Obj` makes no icon and the sample sets none; only `Icon`, `IconSpriteName` or a PNG
      visual change it. **Fixed 2026-11-19:** `ModItemDefinition.IconFile` (a PNG of the mod's own; logged when
      loaded; a model without any icon logs a warning), and the sample ships `GlowBerry.png`, rendered from its
      model by `samples/ShopDemo/model/make_icon.py` (Blender, headless). In game, run 9: host and client load it
      (256x256); the client's purchase toasts and the host's "Added to Bucket" toast show the berry.
- [ ] **D12 A client without the mod sees price tags over empty slots** (in game, run 10: "7" and "140" in the
      starter shop, nothing above them). The slot is a scene object every peer has; only its item is a mod
      prefab the client cannot create. Nothing in the library can run on such a client; the host could keep mod
      items out of vanilla slots when a peer lacks the mod, once it knows (P7's mod list). Decide with P7.
- POGLanTest (the test tool), run 6–7: `ScreenCapture.CaptureScreenshot` fails under Il2CppInterop ("Method not
  found: ReadOnlySpan`1.GetPinnableReference") (D11, tool only), so screenshots are taken from outside the game.
- Not the library, seen the same night: POG Timer 1.0.2 (Thunderstore) logged "Hook failed" four times a second
  while the menu was up; 1.1.0, which does not, was published 2026-11-19. POGLanTest (the test tool) had four
  defects, fixed during the test: hosting from the main menu never loaded a scene, a client that joined before the
  main menu came up had no camera, and the first launch's intro and OK screen waited for input.

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
- [ ] **What a vanilla item needs, read from the decompile** (`docs/design/item-chain.md`, build 26668562,
      2026-11-19, nothing of it checked in game). The shell covers every hard requirement; the gaps:
    - [ ] Always give the item its own `m_itemTooltipID` (name term = discovery id), also without `DisplayName`.
          Today such an item keeps the shell's key: it is called "Diamond" and counts as discovered with it.
    - [ ] `AttachBundlePrefab` parents the model under the root; the game shows, hides and layers only
          `PickupItem` / `HeldItem` and re-enables every renderer of `m_renderers`. Put the model under
          `PickupItem` (copy under `HeldItem`) and take the shell's renderers out of `m_renderers`.
    - [ ] Journal entries for mod items (`JournalEntryDataItem` + the UI entry before
          `UI_JournalGroupItems.Initialize`); the game never creates item entries in code.
    - [ ] A big-item shell: `Big = true` on the diamond shell keeps the diamond's grips and size.
    - [ ] Expose `m_itemRarity`, `m_breakVelocity` (with a break effect), `m_triggerDiscoveryOnPickup`.
    - [ ] Check a definition when it is registered (capability duration > 0, curve set, a grip present) and
          say what is wrong, instead of an NRE in game.
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

### For content mods planned on top of the library

- [ ] **POGDeeperDepths** (working name; a separate mod, its plan is in its own folder,
      `_Modding/ModSource/POGDeeperDepths/ROADMAP.md`): depths past the vanilla ones, opened by cog tiers. It
      needs from the library the Levels module and items; extending the depth station is the mod's own work
      unless other mods need it too.

---

## Known limitations (current)

- Runtime correctness of network registration, save behavior and bundle round-trip rests on
  static decompilation until Milestone 0 is run in-game.
- Use effects are host-only for now (client→host RPC is v0.2).
- Loot injection targets `LootTable` category arrays; `InventoryLootTable` (per-chest/enemy)
  targeting is a later, more selective feature.
- Custom visuals are icon/tint level until the AssetBundle smoke test (0.2) passes.
