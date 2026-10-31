using System.Collections.Generic;

namespace STS2MobileIos.Patches;

// Event value table: estimated value of each event's best option.
// Approximate range 0-9: free rare/Ancient relic ~7; removal/upgrade/enchantment ~4; small gold gain ~2-3; narrative only 0; curses reduce value.
// RewardsSteps counts PlayerRng.Rewards steps at entry/best option: random cards=2/card, potions=1, rolled rarity relics=1; fixed rewards/transforms=0.
public static class EventData
{
    public static readonly Dictionary<string, (double Value, string Desc, int RewardsSteps)> Table = new()
    {
        // A-F
        ["ABYSSAL_BATHS"] = (2.0, "Gain max HP with increasing unblockable damage, or abstain to heal 10; low net value", 0),
        ["AMALGAMATOR"] = (4.5, "Combine 2 basic cards into 1 ultimate card: remove 2, gain an improved basic card", 0),
        ["AROMA_OF_CHAOS"] = (4.0, "Upgrade 1 card (preferred to random transformation)", 0),
        ["BATTLEWORN_DUMMY"] = (4.5, "Defeat the 300-HP dummy in 3 turns for a relic; conservatively choose 150 HP for 2 upgrades if unsure", 1),
        ["BRAIN_LEECH"] = (3.0, "Choose 1 of 5 class cards (mandatory); no HP loss", 15),
        ["BUGSLAYER"] = (3.0, "Choose a fixed strong colorless card: Exterminate or Squash", 0),
        ["BYRDONIS_NEST"] = (3.5, "Eat the egg for +7 max HP, or take the companion card", 0),
        ["COLORFUL_PHILOSOPHERS"] = (6.0, "Choose another color pool, then 1 common, 1 uncommon and 1 rare card", 18),
        ["COLOSSAL_FLOWER"] = (5.5, "Dig to the bottom for PollinousCore with cumulative damage, or take 135 gold", 0),
        ["CRYSTAL_SPHERE"] = (3.0, "Card-flipping minigame for relics/potions/cards/gold; inconsistent, low-confidence value", 0),
        ["DARV"] = (7.0, "Ancient: choose 1 of 3 boss-level relics, such as Astrolabe, Black Star or Pandora's Box", 0),
        ["DENSE_VEGETATION"] = (2.5, "Take 8 damage for about 61-100 gold, or rest then face a mandatory fight", 0),
        ["DOLL_ROOM"] = (4.5, "Lose 15 HP to choose among 3 event relics, or take 1 random relic free", 0),
        ["DOORS_OF_LIGHT_AND_DARK"] = (4.5, "Upgrade 2 random cards, or remove 1 chosen card", 0),
        ["DROWNING_BEACON"] = (4.5, "Lose 13 max HP for FresnelLens (enchants later cards), or take a free potion", 0),
        ["ENDLESS_CONVEYOR"] = (4.5, "Pay for repeated conveyor-belt dishes: upgrades, healing, gold, potions or colorless cards", 0),
        ["FAKE_MERCHANT"] = (4.5, "Throw the foul potion and win the fight for the rug and all fake relics, or buy them", 0),
        ["FIELD_OF_MAN_SIZED_HOLES"] = (4.0, "Enchant 1 card with PerfectFit, or remove 2 and gain a curse", 0),
        // G-R
        ["GRAVE_OF_THE_FORGOTTEN"] = (7.0, "Gain ForgottenSoul for free", 0),
        ["HUNGRY_FOR_MUSHROOMS"] = (7.0, "Gain BigMushroom for free (the other option costs HP)", 0),
        ["INFESTED_AUTOMATON"] = (3.0, "Gain 1 random Power/zero-cost card on the table for free", 1),
        ["JUNGLE_MAZE_ADVENTURE"] = (3.0, "Go alone for ~135-165 gold and lose 18 HP, or cooperate for ~35-65 gold without damage", 0),
        ["LOST_WISP"] = (3.0, "Search for ~45-75 free gold (the relic alternative adds a curse)", 0),
        ["LUMINOUS_CHOIR"] = (5.0, "Sacrifice ~99-149 gold for 1 rolled relic", 1),
        ["MORPHIC_GROVE"] = (2.0, "Go alone for +5 max HP at no cost", 0),
        ["NONUPEIPE"] = (7.0, "Ancient: choose 1 of 3 randomly selected relics from a 9-relic pool, free", 0),
        ["OROBAS"] = (7.0, "Ancient: choose among 3 rolled relics, one from each group, free", 0),
        ["PAEL"] = (7.0, "Ancient: choose from three Pael relic groups, free", 0),
        ["POTION_COURIER"] = (3.0, "Take 3 fixed potions, or loot 1 random rare potion", 0),
        ["PUNCH_OFF"] = (6.0, "Win a fight for a relic and potion, or Nab a free rolled relic with a curse", 2),
        ["RANWID_THE_ELDER"] = (6.0, "Trade 1 tradable relic for 2 rolled relics (net +1)", 2),
        ["REFLECTIONS"] = (4.5, "Downgrade up to 2 cards and upgrade up to 4; net upgrade gain", 0),
        ["RELIC_TRADER"] = (2.0, "Trade 1 of 3 owned relics for a rolled replacement; 3 RNG steps consumed on entry", 3),
        ["ROOM_FULL_OF_CHEESE"] = (6.0, "Search for ChosenCheese at a cost of 14 damage", 0),
        ["ROUND_TEA_PARTY"] = (8.0, "Drink tea for free RoyalPoison and a full heal", 0),
        // S-Z
        ["SAPPHIRE_SEED"] = (4.0, "Eat to heal 9 HP and upgrade 1 card, free", 0),
        ["SELF_HELP_BOOK"] = (4.0, "Apply 2 enchantment stacks to a card type; free permanent benefit", 0),
        ["SLIPPERY_BRIDGE"] = (4.0, "Remove 1 random non-basic card for free", 0),
        ["SPIRALING_WHIRLPOOL"] = (3.0, "Enchant 1 card with Spiral, or heal 33% of max HP", 0),
        ["SPIRIT_GRAFTER"] = (4.0, "Heal 25 HP and gain the Metamorphosis Power card", 0),
        ["STONE_OF_ALL_TIME"] = (4.0, "Discard 1 potion for +10 max HP", 0),
        ["SUNKEN_STATUE"] = (6.0, "Take the sword to gain the stone-sword relic for free", 0),
        ["SUNKEN_TREASURY"] = (4.0, "Large chest: ~333 gold and Greed; small chest: 60 gold", 0),
        ["SYMBIOTE"] = (4.0, "Apply a strong Corruption enchantment, or transform 1 card (after Act 2)", 0),
        ["TABLET_OF_TRUTH"] = (3.0, "Heal 20 HP, or lose max HP to upgrade cards", 0),
        ["TANX"] = (7.0, "Ancient: choose 1 of 3 melee relics", 0),
        ["TEA_MASTER"] = (6.0, "Free discourteous-tea relic, with tea available to buy (150 gold required before Act 2)", 0),
        ["TEZCATARA"] = (7.0, "Ancient: choose 1 of 3 food relics, rolled from 3 pools", 0),
        ["THE_ARCHITECT"] = (0.0, "Final scoring fight; no collectible rewards", 0),
        ["THE_FUTURE_OF_POTIONS"] = (4.0, "Discard 1 potion to choose 1 of 3 upgraded cards of matching rarity (requires 2+ potions)", 2),
        ["THE_LANTERN_KEY"] = (3.0, "Return the key for 100 gold, or keep it for the War Historian reward quest", 0),
        ["THE_LEGENDS_WERE_TRUE"] = (3.0, "Take the map to gain the spoils-map card (Act 1 only)", 0),
        ["THIS_OR_THAT"] = (6.0, "Fancy: next relic plus Clumsy curse; free relic", 0),
        ["TINKER_TIME"] = (3.0, "Customize 1 Mad Science card for the deck", 0),
        ["TRASH_HEAP"] = (6.0, "Jump in: lose 8 HP to choose 1 of 5 random relics", 0),
        ["TRIAL"] = (5.0, "One of 3 random trials: relics, gold or cards; some add curses", 0),
        ["UNREST_SITE"] = (6.0, "Kill for the next relic at a cost of 8 max HP (requires HP <=70%)", 0),
        ["VAKUU"] = (7.0, "Ancient: choose 1 of 3 relics, rolled from 3 pools", 0),
        ["WAR_HISTORIAN_REPY"] = (9.0, "Quest-gated by LanternKey: history-lesson relic, 2 potions and 2 relics", 0),
        ["WATERLOGGED_SCRIPTORIUM"] = (4.0, "Blood ink: +6 max HP free, or pay for an enchantment (55 gold required)", 0),
        ["WELCOME_TO_WONGOS"] = (5.0, "Act 2 shop: buy the featured rare relic for 200 gold", 0),
        ["WELLSPRING"] = (3.0, "Bottle 1 random potion", 1),
        ["WHISPERING_HOLLOW"] = (4.0, "About 35 gold for 2 potions (44 gold required)", 2),
        ["WOOD_CARVINGS"] = (3.0, "Transform 1 basic card, or apply a swimming-snake enchantment to an eligible card", 0),
        ["ZEN_WEAVER"] = (4.0, "Pay to thin the deck: 125 gold removes 1 card; 250 removes 2", 0),
    };

    // Event value; unknown events get the neutral average of about 4.
    public static double Value(string eventId) =>
        eventId != null && Table.TryGetValue(eventId, out var v) ? v.Value : 4.0;

    // Rewards RNG steps consumed on entry/by the best option; defaults to 0.
    public static int RewardsSteps(string eventId) =>
        eventId != null && Table.TryGetValue(eventId, out var v) ? v.RewardsSteps : 0;

    public static string Desc(string eventId) =>
        eventId != null && Table.TryGetValue(eventId, out var v) ? v.Desc : null;
}
