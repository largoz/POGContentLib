using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2CppGame.Journal;
using MelonLoader;
using UnityEngine;

namespace POGContentLib.Items
{
    /// <summary>
    /// D6: mod items in the journal. The game never creates item entries in code: each journal tab
    /// (<c>UI_JournalGroupItems</c>) holds entries already placed in its UI (<c>m_entries</c>), each with fixed data
    /// (<c>JournalEntryDataItem</c>: icon, name and description terms, rarity, values) and a picture baked into the
    /// entry. <c>Initialize</c> walks <c>m_entries</c> and shows the ones whose <c>Name</c> is discovered
    /// (<c>DiscoveryManager.IsDiscovered</c>); a discovery made later shows its entry through
    /// <c>DiscoveryManager_OnNewDiscoveryMade</c>, which walks the same list (build 26668562, 2026-11-19). The item's
    /// discovery id is its name term (<c>m_itemTooltipID</c>), so an entry named with the same term is found by both.
    /// Here, right before a tab initializes, every mod item listed for that tab gets an entry of its own: a copy of
    /// an existing entry, with the item's data and picture.
    /// </summary>
    [HarmonyPatch(typeof(UI_JournalGroupItems), nameof(UI_JournalGroupItems.Initialize))]
    internal static class Patch_UI_JournalGroupItems_Initialize
    {
        static void Prefix(UI_JournalGroupItems __instance)
        {
            try { JournalEntries.AddModEntries(__instance); }
            catch (Exception ex) { MelonLogger.Warning($"[POGContentLib.Items] Journal entries: {ex.GetType().Name}: {ex.Message}"); }
        }
    }

    internal static class JournalEntries
    {
        public const string EntryNamePrefix = "ModJournalEntry|";

        public static void AddModEntries(UI_JournalGroupItems tab)
        {
            var plugin = ItemsPlugin.Instance;
            if (tab == null || plugin == null || tab.m_entries == null) return;
            var entries = tab.m_entries;

            // The copy follows an entry of this tab, so it lands in the same layout with the same look.
            UI_JournalEntryItem pattern = null;
            for (int i = entries.Count - 1; i >= 0 && pattern == null; i--) pattern = entries[i];
            if (pattern == null) pattern = tab.m_entryPrefab;
            if (pattern == null) return;
            var parent = entries.Count > 0 && entries[entries.Count - 1] != null ? entries[entries.Count - 1].transform.parent : tab.transform;

            var present = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < entries.Count; i++)
            {
                var data = entries[i] != null ? entries[i].m_entryData : null;
                if (data != null && !string.IsNullOrEmpty(data.Name)) present.Add(data.Name);
            }

            int added = 0;
            foreach (var def in plugin.Definitions)
            {
                if (def == null || !def.ShowInJournal || ItemsPlugin.JournalGroupOf(def) != tab.m_itemGroup) continue;
                string nameKey = ItemsPlugin.NameKeyOf(def);
                if (present.Contains(nameKey)) continue;   // already added (a tab can initialize again)

                var copyGo = UnityEngine.Object.Instantiate(pattern.gameObject, parent);
                copyGo.name = EntryNamePrefix + ContentRegistryId(def);
                var copy = copyGo.GetComponent<UI_JournalEntryItem>();
                if (copy == null) { UnityEngine.Object.Destroy(copyGo); continue; }

                // An item without an icon of its own shows its shell's (as in the hotbar and the toasts); without
                // either, the copy would keep the picture of the entry it was copied from (run 12, the sack).
                var icon = plugin.IconFor(def);
                bool shellIcon = false;
                if (icon == null) { icon = TemplateIcon(def); shellIcon = icon != null; }
                Il2Cpp.RarityData.RarityObject rarity = default;
                bool hasRarity = false;
                try { if (tab.m_rarityData != null) { rarity = tab.m_rarityData.GetRarityData(default); hasRarity = true; } } catch { }

                var entryData = new JournalEntryDataItem
                {
                    Icon = icon,
                    Name = nameKey,
                    Description = ItemsPlugin.DescriptionKeyOf(def),
                    Rarity = default,
                    ItemGroup = tab.m_itemGroup,
                    FoodValue = def.FoodValue,
                    CoinValue = def.GoldValue,
                    ShowFoodValue = def.FoodValue > 0,
                    ShowCoinValue = def.GoldValue > 0,
                };
                if (hasRarity) { entryData.RarityColor = rarity.PrimaryColor; entryData.RarityIcon = rarity.Sprite; }
                copy.m_entryData = entryData;
                // The picture is part of the entry, not read from its data.
                if (copy.m_entryImage != null && icon != null) copy.m_entryImage.sprite = icon;
                if (copy.m_rarityIcon != null && hasRarity && rarity.Sprite != null) copy.m_rarityIcon.sprite = rarity.Sprite;

                entries.Add(copy);
                present.Add(nameKey);
                added++;
                MelonLogger.Msg($"[POGContentLib.Items] Journal: {def.ContentId} listed under {tab.m_itemGroup} (shows once discovered; icon {(icon == null ? "none" : shellIcon ? "the shell's" : "its own")}).");
            }
            if (added > 0)
                MelonLogger.Msg($"[POGContentLib.Items] Journal tab {tab.m_itemGroup}: {added} mod item(s) added to its {entries.Count} entries.");
        }

        private static Sprite TemplateIcon(ModItemDefinition def)
        {
            var content = POGContentLib.Core.CoreServices.Content;
            if (content == null) return null;
            foreach (var prefab in content.AllPrefabs)
                if (prefab != null && ReferenceEquals(content.GetDefinitionByHash(prefab.GlobalObjectIdHash), def))
                    return prefab.GetComponent<Il2Cpp.InventoryItem>()?.m_itemIcon;
            return null;
        }

        private static string ContentRegistryId(ModItemDefinition def) => POGContentLib.Core.ContentRegistry.FullId(def.ModId, def.ContentId);
    }
}
