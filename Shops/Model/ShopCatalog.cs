using System;
using System.Collections.Generic;
using System.Linq;

namespace POGContentLib.Shops.Model
{
    /// <summary>One way an entry got into, or was changed in, a slot — the slot's "who changed what" history.</summary>
    public sealed class HistoryRecord
    {
        public string Mod { get; }
        public string Text { get; }
        public HistoryRecord(string mod, string text) { Mod = mod; Text = text; }
        public override string ToString() => $"{Mod}: {Text}";
    }

    /// <summary>A candidate in a slot: an item (or "nothing") with a weight and the content that put it there.</summary>
    public sealed class SlotEntry
    {
        public string Item { get; internal set; }
        public double Weight { get; internal set; }
        /// <summary>"vanilla" or the mod id that added the entry.</summary>
        public string AddedBy { get; }
        public SlotEntry(string item, double weight, string addedBy) { Item = item; Weight = weight; AddedBy = addedBy; }
        public override string ToString() => $"{Item} x{Weight} ({AddedBy})";
    }

    /// <summary>A slot after every loaded mod has had its say.</summary>
    public sealed class ResolvedSlot
    {
        public string SlotId { get; }
        public IReadOnlyList<SlotEntry> Entries { get; }
        public IReadOnlyList<HistoryRecord> History { get; }
        public double TotalWeight { get; }
        /// <summary>True when no mod touched this slot: leave the game's own logic alone.</summary>
        public bool Untouched { get; }

        internal ResolvedSlot(string slotId, List<SlotEntry> entries, List<HistoryRecord> history, bool untouched)
        {
            SlotId = slotId;
            Entries = entries;
            History = history;
            TotalWeight = entries.Sum(e => e.Weight);
            Untouched = untouched;
        }

        /// <summary>Chance of one entry, 0..1.</summary>
        public double ChanceOf(SlotEntry e) => TotalWeight > 0 ? e.Weight / TotalWeight : 0;

        /// <summary>Chance of an item summed over all its entries, 0..1.</summary>
        public double ChanceOfItem(string item) =>
            TotalWeight > 0 ? Entries.Where(e => e.Item == item).Sum(e => e.Weight) / TotalWeight : 0;

        /// <summary>
        /// Pick an entry for a uniform random number r in [0, 1). Returns null when the slot has no weight
        /// at all; returns an entry whose Item is "nothing" when the slot should stay empty.
        /// </summary>
        public SlotEntry Pick(double r)
        {
            if (TotalWeight <= 0) return null;
            double target = r * TotalWeight, acc = 0;
            SlotEntry last = null;
            foreach (var e in Entries)
            {
                if (e.Weight <= 0) continue;
                acc += e.Weight;
                last = e;
                if (target < acc) return e;
            }
            return last;   // r rounding at the top end
        }
    }

    /// <summary>Content the game placed in a slot, as seen at runtime: an item and how many times it appears.</summary>
    public readonly struct VanillaEntry
    {
        public string Item { get; }
        public int Count { get; }
        public VanillaEntry(string item, int count) { Item = item; Count = count; }
    }

    /// <summary>
    /// Every loaded shop file folded into one deterministic set of rules (docs/design/shops.md §4–§5).
    ///
    /// Weights, not percentages: an entry's chance is its weight over the slot's total, so any number of
    /// mods can add to one slot without breaking each other. Vanilla content gets
    /// <see cref="ShopCatalogOptions.DefaultVanillaWeight"/> per occurrence.
    ///
    /// Order is fixed: all slots, then every "add" (mods in dependency order, ties by mod id), then every
    /// "patch" in the same order — so the last patch wins and every step is recorded in the slot history.
    /// A patch that touches another mod's slot or item must list that mod in "requires"; otherwise it is
    /// refused (non-invasive "add" needs nothing).
    /// </summary>
    public sealed class ShopCatalog
    {
        private enum OpKind { Add, SetWeight, Remove, Replace }

        private sealed class Op
        {
            public OpKind Kind;
            public string Mod;
            public string Item;
            public double Weight;
            public string ReplaceWith;
        }

        public sealed class ModSlot
        {
            public string FullId { get; internal set; }
            public string Mod { get; internal set; }
            public SlotDef Def { get; internal set; }
        }

        public sealed class ModSeller
        {
            public string FullId { get; internal set; }
            public string Mod { get; internal set; }
            public SellerDef Def { get; internal set; }
        }

        private readonly Dictionary<string, List<Op>> _ops = new Dictionary<string, List<Op>>(StringComparer.Ordinal);
        private readonly Dictionary<string, ModSlot> _modSlots = new Dictionary<string, ModSlot>(StringComparer.Ordinal);
        private readonly List<ModSeller> _sellers = new List<ModSeller>();
        private readonly Dictionary<string, (string mod, List<PoolEntry> pool)> _sellerSlots = new Dictionary<string, (string, List<PoolEntry>)>(StringComparer.Ordinal);
        private readonly Dictionary<string, (string mod, TagDef standing)> _displays = new Dictionary<string, (string, TagDef)>(StringComparer.Ordinal);
        private readonly List<Diagnostic> _diagnostics = new List<Diagnostic>();

        public ShopCatalogOptions Options { get; }
        /// <summary>Mods in the order their rules apply.</summary>
        public IReadOnlyList<string> ModOrder { get; private set; } = Array.Empty<string>();
        public IReadOnlyDictionary<string, ModSlot> ModSlots => _modSlots;
        public IReadOnlyList<ModSeller> Sellers => _sellers;
        /// <summary>Standing points set by shop files, by item id.</summary>
        public IReadOnlyDictionary<string, (string mod, TagDef standing)> ItemDisplays => _displays;

        /// <summary>Id of slot n (1-based, the preset's slot order) of a mod merchant: "Mod:seller#n".</summary>
        public static string SellerSlotId(string fullSellerId, int n) => fullSellerId + "#" + n;
        public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;
        /// <summary>Every slot id some rule targets (mod slots and vanilla ones).</summary>
        public IEnumerable<string> TargetedSlots => _ops.Keys.Concat(_modSlots.Keys).Concat(_sellerSlots.Keys).Distinct();

        private ShopCatalog(ShopCatalogOptions options) { Options = options ?? new ShopCatalogOptions(); }

        public static ShopCatalog Build(IEnumerable<ShopFile> files, ShopCatalogOptions options = null)
        {
            var c = new ShopCatalog(options);
            c.BuildFrom(files.Where(f => f != null).ToList());
            return c;
        }

        private void BuildFrom(List<ShopFile> files)
        {
            // A mod may ship several files; they act as one, in path order.
            // (Explicit loops instead of ToDictionary: the game's proxy assemblies break the compiler's
            // nullable metadata for those generic calls, CS0656 NullableAttribute.)
            var byMod = new Dictionary<string, List<ShopFile>>(StringComparer.Ordinal);
            foreach (var f in files.OrderBy(f => f.SourcePath ?? "", StringComparer.Ordinal))
            {
                if (!byMod.TryGetValue(f.Mod, out var list)) byMod[f.Mod] = list = new List<ShopFile>();
                list.Add(f);
            }
            ModOrder = OrderMods(byMod);
            var requires = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (var kv in byMod)
                requires[kv.Key] = RequiresOf(kv.Value, null);

            // 1. Slots and sellers.
            foreach (var mod in ModOrder)
                foreach (var f in byMod[mod])
                {
                    foreach (var s in f.Slots)
                    {
                        string id = mod + ":" + s.Id;
                        if (_modSlots.ContainsKey(id)) { Diag(Severity.Error, f, $"slot '{id}' is defined twice; the second is ignored"); continue; }
                        _modSlots[id] = new ModSlot { FullId = id, Mod = mod, Def = s };
                    }
                    foreach (var s in f.Sellers)
                    {
                        string id = mod + ":" + s.Id;
                        _sellers.Add(new ModSeller { FullId = id, Mod = mod, Def = s });
                        for (int i = 0; i < (s.Slots?.Count ?? 0); i++)
                            if (s.Slots[i] != null && s.Slots[i].Count > 0)
                                _sellerSlots[SellerSlotId(id, i + 1)] = (mod, s.Slots[i]);
                    }
                    foreach (var d in f.Items)
                    {
                        string item = ShopIds.Qualify(d.Item, mod);
                        if (_displays.ContainsKey(item)) Diag(Severity.Warning, f, $"standing point of {item} is set twice; the later one is used");
                        _displays[item] = (mod, d.Standing);
                    }
                }

            // 2. Adds, then 3. patches, both in mod order.
            foreach (var mod in ModOrder)
                foreach (var f in byMod[mod])
                    foreach (var a in f.Add)
                        AddOpTo(ShopIds.Qualify(a.Slot, mod), new Op { Kind = OpKind.Add, Mod = mod, Item = ShopIds.Qualify(a.Item, mod), Weight = a.Weight });

            foreach (var mod in ModOrder)
                foreach (var f in byMod[mod])
                    foreach (var p in f.Patch)
                    {
                        string slot = ShopIds.Qualify(p.Slot, mod);
                        string item = ShopIds.Qualify(p.Item, mod);
                        // Trust seam: touching another mod's content must be declared.
                        var undeclared = new[] { ShopIds.OwnerOf(slot), ShopIds.OwnerOf(item) }
                            .Where(o => o != null && o != ShopIds.Vanilla && o != mod && !requires[mod].Contains(o))
                            .Distinct().ToList();
                        if (undeclared.Count > 0)
                        {
                            Diag(Severity.Error, f, $"patch on {slot} / {item} touches {string.Join(", ", undeclared)} without listing it in \"requires\"; refused");
                            continue;
                        }
                        var op = new Op { Mod = mod, Item = item };
                        if (p.Weight.HasValue) { op.Kind = OpKind.SetWeight; op.Weight = p.Weight.Value; }
                        else if (p.Remove) op.Kind = OpKind.Remove;
                        else { op.Kind = OpKind.Replace; op.ReplaceWith = ShopIds.Qualify(p.ReplaceWith, mod); }
                        AddOpTo(slot, op);
                    }

            foreach (var mod in ModOrder)
                foreach (var req in requires[mod])
                    if (!byMod.ContainsKey(req))
                        _diagnostics.Add(new Diagnostic(Severity.Info, mod, $"requires '{req}', which has no shop file loaded; its patches on '{req}' content will find nothing"));
        }

        private static HashSet<string> RequiresOf(List<ShopFile> files, Func<string, bool> keep)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var f in files)
                if (f.Requires != null)
                    foreach (var r in f.Requires)
                        if (!string.IsNullOrEmpty(r) && (keep == null || keep(r))) set.Add(r);
            return set;
        }

        private void AddOpTo(string slot, Op op)
        {
            if (!_ops.TryGetValue(slot, out var list)) _ops[slot] = list = new List<Op>();
            list.Add(op);
        }

        private void Diag(Severity s, ShopFile f, string m) => _diagnostics.Add(new Diagnostic(s, f.SourcePath ?? f.Mod, m));

        // Kahn's algorithm over "requires", smallest mod id first among the ready ones; a cycle is broken
        // by mod id and reported.
        private List<string> OrderMods(Dictionary<string, List<ShopFile>> byMod)
        {
            var deps = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (var kv in byMod)
                deps[kv.Key] = RequiresOf(kv.Value, r => byMod.ContainsKey(r) && r != kv.Key);
            var order = new List<string>();
            var remaining = new SortedSet<string>(byMod.Keys, StringComparer.Ordinal);
            while (remaining.Count > 0)
            {
                var ready = remaining.FirstOrDefault(m => deps[m].All(d => !remaining.Contains(d)));
                if (ready == null)
                {
                    ready = remaining.Min;
                    _diagnostics.Add(new Diagnostic(Severity.Warning, ready,
                        $"\"requires\" forms a cycle among {string.Join(", ", remaining)}; applying '{ready}' first"));
                }
                order.Add(ready);
                remaining.Remove(ready);
            }
            return order;
        }

        /// <summary>
        /// The final candidates of one slot. <paramref name="vanilla"/> is what the game itself put in it
        /// (empty for a mod slot). Unresolvable items are not filtered here: the runtime drops entries whose
        /// item is not loaded and records that in the history.
        /// </summary>
        public ResolvedSlot Resolve(string slotId, IEnumerable<VanillaEntry> vanilla)
        {
            var entries = new List<SlotEntry>();
            var history = new List<HistoryRecord>();
            bool touched = false;

            if (_modSlots.TryGetValue(slotId, out var ms))
            {
                touched = true;
                foreach (var e in ms.Def.Pool ?? new List<PoolEntry>())
                {
                    if (e == null) continue;
                    entries.Add(new SlotEntry(ShopIds.Qualify(e.Item, ms.Mod), e.Weight, ms.Mod));
                    history.Add(new HistoryRecord(ms.Mod, $"pool: {ShopIds.Qualify(e.Item, ms.Mod)} weight {e.Weight:0.##}"));
                }
            }
            else if (_sellerSlots.TryGetValue(slotId, out var ss))
            {
                touched = true;
                foreach (var e in ss.pool)
                {
                    if (e == null) continue;
                    entries.Add(new SlotEntry(ShopIds.Qualify(e.Item, ss.mod), e.Weight, ss.mod));
                    history.Add(new HistoryRecord(ss.mod, $"merchant stock: {ShopIds.Qualify(e.Item, ss.mod)} weight {e.Weight:0.##}"));
                }
            }
            else if (vanilla != null)
            {
                foreach (var v in vanilla)
                    if (v.Count > 0 && !string.IsNullOrEmpty(v.Item))
                        entries.Add(new SlotEntry(v.Item, Options.DefaultVanillaWeight * v.Count, ShopIds.Vanilla));
            }

            if (_ops.TryGetValue(slotId, out var ops))
            {
                touched = true;
                foreach (var op in ops) Apply(op, entries, history);
            }
            return new ResolvedSlot(slotId, entries, history, !touched);
        }

        private static void Apply(Op op, List<SlotEntry> entries, List<HistoryRecord> history)
        {
            switch (op.Kind)
            {
                case OpKind.Add:
                    entries.Add(new SlotEntry(op.Item, op.Weight, op.Mod));
                    history.Add(new HistoryRecord(op.Mod, $"add {op.Item} weight {op.Weight:0.##}"));
                    return;
            }
            var hits = entries.Where(e => e.Item == op.Item).ToList();
            if (hits.Count == 0)
            {
                history.Add(new HistoryRecord(op.Mod, $"patch on {op.Item} found no such entry (nothing changed)"));
                return;
            }
            switch (op.Kind)
            {
                case OpKind.SetWeight:
                    foreach (var h in hits)
                    {
                        history.Add(new HistoryRecord(op.Mod, $"weight of {h.Item} (from {h.AddedBy}) {h.Weight:0.##} -> {op.Weight:0.##}"));
                        h.Weight = op.Weight;
                    }
                    break;
                case OpKind.Remove:
                    foreach (var h in hits)
                    {
                        history.Add(new HistoryRecord(op.Mod, $"removed {h.Item} (from {h.AddedBy}, weight {h.Weight:0.##})"));
                        entries.Remove(h);
                    }
                    break;
                case OpKind.Replace:
                    foreach (var h in hits)
                    {
                        history.Add(new HistoryRecord(op.Mod, $"replaced {h.Item} (from {h.AddedBy}) with {op.ReplaceWith}"));
                        h.Item = op.ReplaceWith;
                    }
                    break;
            }
        }
    }

    public sealed class ShopCatalogOptions
    {
        /// <summary>Weight of one occurrence of a vanilla item in a slot: the scale modders weigh against.</summary>
        public double DefaultVanillaWeight { get; set; } = 10;
    }
}
