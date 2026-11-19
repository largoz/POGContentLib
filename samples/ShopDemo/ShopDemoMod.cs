using System.IO;
using System.Linq;
using MelonLoader;
using MelonLoader.Utils;
using POGContentLib.Items;
using UnityEngine;

[assembly: MelonInfo(typeof(ShopDemo.ShopDemoMod), "ShopDemo", "1.0.0", "POGContentLib sample")]
[assembly: MelonGame(null, "Pit of Goblin")]
[assembly: MelonAdditionalDependencies("POGContentLib")]

namespace ShopDemo
{
    /// <summary>
    /// Sample content pack for the Shops module. Phase 1 (code) registers the item; phase 2 (data) is
    /// pogshop.json next to this mod, which says where it is sold. Nothing here is shop-specific except
    /// ShopCost, the base price the shop shows. The berry has its own model, GlowBerry.obj (+ .mtl), shipped
    /// next to pogshop.json and made by model/make_glowberry.py.
    /// </summary>
    public class ShopDemoMod : MelonMod
    {
        public override void OnInitializeMelon()
        {
            Content.Items.Register(new ModItemDefinition
            {
                ModId = "ShopDemo",
                ContentId = "GlowBerry",
                DisplayName = "Glow Berry",
                DisplayDescription = "A berry that glows faintly. Tastes like an apple, somehow.",
                ItemType = Il2Cpp.ItemType.Food,
                MapIcon = Il2Cpp.MapIconType.Food,
                Visual = ModelPath() is string obj ? ItemVisual.Obj(obj) : null,   // null: the diamond shell
                IconFile = IconPath(),                                              // rendered from the model (model/make_icon.py)
                GoldValue = 20,
                ShopCost = 7,
                Capabilities = { new EatCapability { CopyFrom = "Item_Apple", HealthOnEating = 15f, BodyValue = 1 } },
            });
            LoggerInstance.Msg("ShopDemo: Glow Berry registered (shop rules in pogshop.json).");
        }

        // The model ships next to pogshop.json: Mods/<Author>-ShopDemo/ under Gale, nested deeper under r2modman.
        private string ModelPath()
        {
            string found = null;
            try { found = Directory.EnumerateFiles(MelonEnvironment.ModsDirectory, "GlowBerry.obj", SearchOption.AllDirectories).FirstOrDefault(); }
            catch (System.Exception e) { LoggerInstance.Warning("ShopDemo: looking for GlowBerry.obj failed: " + e.Message); }
            if (found == null) LoggerInstance.Warning("ShopDemo: GlowBerry.obj not found under Mods/: the berry keeps the diamond shell's look.");
            return found;
        }

        // The icon ships next to the model.
        private string IconPath()
        {
            var obj = ModelPath();
            var png = obj != null ? Path.Combine(Path.GetDirectoryName(obj), "GlowBerry.png") : null;
            if (png != null && File.Exists(png)) return png;
            LoggerInstance.Warning("ShopDemo: GlowBerry.png not found next to the model: the berry shows the diamond shell's icon.");
            return null;
        }
    }
}
