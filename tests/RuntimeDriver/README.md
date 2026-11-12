# Runtime driver — POGContentLib in the real game, no mouse

A throwaway MelonMod that plays the game from inside and logs one `[DRIVER] …` line per step; `run.sh` launches
the game, screenshots, closes it and cleans up. This is how the Shops module was checked on 2026-11-12. Never ship
the driver. Ground rules for launching on this machine: `_Modding/ModSource/_research/TESTING.md` §0 (say before you
launch; never while the user plays).

## Run

```sh
dotnet build ../../POGContentLib.csproj -c Release -p:GameDir=$HOME/.local/share/com.kesomannen.gale/pit-of-goblin/profiles/Default
dotnet build RuntimeDriver.csproj -c Release            # GameDir defaults to the Gale profile
mkdir -p /tmp/run && printf "dump=1\n" > /tmp/run/driver.txt
./run.sh /tmp/run [extra DLL or folder for Mods/ ...]   # POG_TIMEOUT=720 for dungeon runs
```

`run.sh` deploys the library build, the driver, the extras and `<out>/driver.txt`; copies `<out>/shops/*.json` to
`UserData/POGContentLib/Shops/`; shoots `<out>/<name>.png` ~2 s after each `[DRIVER] shot <name>`; stops at
`[DRIVER] done`; then closes the game and removes everything it deployed (`Mods/` is listed at the end).

## How it gets into a game

From the main menu it opens the save-slot panel (`NetworkMenuGameSaveState`, the instance in the menu scene — the
copy `Resources.FindObjectsOfTypeAll` also returns throws) and calls `StartSelectedGame(slot)`: the game hosts and
loads `PitOfGoblin_Lobby`. Default slot = the last one, so slot 1 stays the player's. A new slot creates a save.

## driver.txt keys

| Key | Does |
|---|---|
| `slot=N` | save slot to start (default: last) |
| `dump=1` | one line per seller and per slot in the hub (ids, stock, faction, price, holder) |
| `look=<id>;<id>` | point the game's camera at each slot/seller id and shoot `look_N` (overridden before HDRP culls; a second camera does not show) |
| `devtools=1`, `devslot=<slot id>` | drive the shop tools: aim at the counter beside that slot, place a slot, shoot HUD and panel, Save, print and delete the file |
| `jumps=<scene>;<scene>` | the tools' scene jump to each scene, dump its shops, shoot `jump_N` |
| `dungeon=1` | down the bucket: list sellers, mod merchant slots, shoot `merchant`; then the tools' round-timer freeze for 8 s and 8 s unfrozen |
| `stay=N` | seconds before `done` |

Shop checks (always): per slot touched by a rule, what was picked and the chances, then the item the game actually
linked to the slot (`InventoryItem.ShopLocation`), active, price, tag, faction, seller.
