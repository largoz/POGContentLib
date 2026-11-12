using MelonLoader;
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
    /// ShopCost, the base price the shop shows.
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
                Tint = new Color(0.4f, 1f, 0.5f),
                GoldValue = 20,
                ShopCost = 7,
                Capabilities = { new EatCapability { CopyFrom = "Item_Apple", HealthOnEating = 15f, BodyValue = 1 } },
            });
            LoggerInstance.Msg("ShopDemo: Glow Berry registered (shop rules in pogshop.json).");
        }
    }
}
