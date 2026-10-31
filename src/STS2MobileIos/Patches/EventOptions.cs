using System.Collections.Generic;

namespace STS2MobileIos.Patches;

// Event-option simulation data: option order and estimated rewards for each event.
// 2026-07-14 audit corrections, originally recorded in experiments/research/event_sim_and_table_audit.md:
// Grade A: reorder 19 events to match GenerateInitialOptions indices exactly; index-based fallback depends on this.
// Add Key throughout: the KEY suffix of EVID.pages.PAGE.options.KEY, with name matching compatible with LOCKED variants.
// Grade B: correct values, missing options and semantics; grade C: add THE_ARCHITECT.
// Exception: NEOW uses separate live logic (2 random positive relics and a curse-related one; dynamic relic-ID keys; Neow L214-278).
// Treat the event choice as a decision point; evaluate each option using current gold/deck/max HP and carry changes into simulation.
public class EvOpt
{
    public string Name;
    public int Hp;              // Current HP change; negative means loss, all unblockable.
    public int MaxHp;           // Maximum HP change.
    public int HealPct;         // Healing percentage; 100 means full heal.
    public int Gold;            // Gold change; -9999 means lose all gold.
    public int AddCards;        // Number of cards gained.
    public int RemoveCards;     // Number of chosen cards removed.
    public int TransformCards;  // Number of cards transformed.
    public int UpgradeCards;    // Number of cards upgraded; negative means downgraded.
    public int AddPotions;      // Potion-count change.
    public int RewardsSteps;    // Rewards RNG steps consumed.
    public string AddRelic;     // Specific relic ID, ROLL for one random relic, ROLL_X2, or CHOICE for a pool choice.
    public string AddCurse;     // Curse ID.
    public int MinGold;         // Minimum gold; the option is unavailable below it.
    public string AddCardEntry; // Specific card entry added, such as byrdonisegg, for cross-node interactions including rest-site hatching.
    public int AddCardType;     // Card type: 1 Attack, 2 Skill, 3 Power, 5 Curse; defaults to 0.
    public double Bonus;        // Fixed bonus value, such as the egg becoming a zero-cost 14-damage attack at a rest site.
    public string Key;          // TextKey option suffix: KEY in EVID.pages.PAGE.options.KEY.
                                // Match dynamic option lists by name, including locked/state-dependent variants, instead of relying only on indices.
                                // LOCKED variants such as SNAKE_LOCKED are absent from clickable options, so need no separate entries.
    public string Note;
}

public static class EventOptions
{
    public static readonly Dictionary<string, EvOpt[]> Table = new()
    {
        // ═══ A-M ═══
        ["ABYSSAL_BATHS"] = new[] {
            // Source order: AbyssalBaths.GenerateInitialOptions = [IMMERSE, ABSTAIN]; later LINGER/EXIT_BATHS pages.
            new EvOpt { Name="Immerse twice, then leave", Key="IMMERSE", Hp=-7, MaxHp=4, Note="Increasing damage of 3+4; stop after two immersions (later LINGER/EXIT_BATHS pages)" },
            new EvOpt { Name="Abstain", Key="ABSTAIN", Hp=10, Note="Heal exactly 10 HP" },
        },
        ["AMALGAMATOR"] = new[] {
            new EvOpt { Name="Combine Strikes", Key="COMBINE_STRIKES", RemoveCards=2, AddCards=1, Note="2 basic Strikes become 1 ultimate Strike (fixed)" },
            new EvOpt { Name="Combine Defends", Key="COMBINE_DEFENDS", RemoveCards=2, AddCards=1, Note="2 basic Defends become 1 ultimate Defend (fixed)" },
        },
        ["AROMA_OF_CHAOS"] = new[] {
            // Grade A reorder: AromaOfChaos = [LET_GO (transform), MAINTAIN_CONTROL (upgrade)]; old table was reversed.
            new EvOpt { Name="Transform", Key="LET_GO", TransformCards=1, Note="Choose 1 card" },
            new EvOpt { Name="Upgrade", Key="MAINTAIN_CONTROL", UpgradeCards=1, Note="Choose 1 card" },
        },
        ["BATTLEWORN_DUMMY"] = new[] {
            // Grade A reorder: BattlewornDummy = [SETTING_1 (75 HP), SETTING_2 (150), SETTING_3 (300)]; old order [2,3,1] was wrong.
            new EvOpt { Name="Setting 1: 75-HP dummy", Key="SETTING_1", AddPotions=1, RewardsSteps=1, Note="Safest, but low reward" },
            new EvOpt { Name="Setting 2: 150-HP dummy", Key="SETTING_2", UpgradeCards=2, Note="Deal 150 damage in 3 turns to upgrade 2 random cards (conservative recommendation)" },
            new EvOpt { Name="Setting 3: 300-HP dummy", Key="SETTING_3", AddRelic="ROLL", RewardsSteps=1, Note="Deal 300 damage in 3 turns for a rolled relic; choose only with sufficient damage" },
        },
        ["BRAIN_LEECH"] = new[] {
            new EvOpt { Name="Share Knowledge", Key="SHARE_KNOWLEDGE", AddCards=1, RewardsSteps=15, Note="Mandatory choice of 1 of 5 class cards (exact prediction available)" },
            new EvOpt { Name="Rip", Key="RIP", Hp=-5, AddCards=1, RewardsSteps=9, Note="Lose 5 HP; choose 1 of 3 colorless cards, with skip allowed" },
        },
        ["BUGSLAYER"] = new[] {
            new EvOpt { Name="Exterminate", Key="EXTERMINATION", AddCards=1, Note="Fixed colorless Exterminate card" },
            new EvOpt { Name="Squash", Key="SQUASH", AddCards=1, Note="Fixed colorless Squash card" },
        },
        ["BYRDONIS_NEST"] = new[] {
            new EvOpt { Name="Eat the egg", Key="EAT", MaxHp=7 },
            new EvOpt { Name="Take the egg", Key="TAKE", AddCardEntry="byrdonisegg", AddCardType=0, Bonus=3.5, Note="Add egg to deck; HATCH at a rest site gives zero-cost, 14-damage ByrdSwoop and a bird companion; synergy pays off there" },
        },
        ["COLORFUL_PHILOSOPHERS"] = new[] {
            // Color-name keys are uppercase and dynamic, up to 3 (ColorfulPhilosophers); use a single abstract entry with index fallback.
            new EvOpt { Name="Choose another color pool and take 3 cards", AddCards=3, RewardsSteps=18, Note="Choose 1 of 3 at each common/uncommon/rare tier; each may be skipped. Color-name keys are dynamic, so name matching does not apply" },
        },
        ["COLOSSAL_FLOWER"] = new[] {
            // First page has [EXTRACT_CURRENT_PRIZE_1, REACH_DEEPER_1]; later keys are EXTRACT_INSTEAD/POLLINOUS_CORE.
            // Three entries abstract multi-page paths (table length differs, so no index match); digging two layers/to bottom shares REACH_DEEPER_1.
            new EvOpt { Name="Harvest immediately", Key="EXTRACT_CURRENT_PRIZE_1", Gold=35 },
            new EvOpt { Name="Dig 2 layers, then harvest", Key="REACH_DEEPER_1", Hp=-11, Gold=135, Note="Later page: EXTRACT_INSTEAD" },
            new EvOpt { Name="Dig to the core", Key="REACH_DEEPER_1", Hp=-18, AddRelic="POLLINOUS_CORE", Note="Lose 18 HP total; later page: POLLINOUS_CORE" },
        },
        ["CRYSTAL_SPHERE"] = new[] {
            // Grade A reorder: CrystalSphere = [UNCOVER_FUTURE (pay), PAYMENT_PLAN (Debt)]; old table was reversed.
            // Upfront cost = 50+Rng(1,50), or 51-100; table uses expected 75, conservative MinGold=99. Minigame rewards remain unverified estimates.
            new EvOpt { Name="Pay upfront for divination", Key="UNCOVER_FUTURE", Gold=-75, AddPotions=1, MinGold=99, Note="Expected value of 3 divinations; cost varies from 51 to 100" },
            new EvOpt { Name="Installments (Debt curse)", Key="PAYMENT_PLAN", AddCurse="DEBT", Gold=35, AddPotions=1, Note="Expected value of 6 divinations, about twice as much; event RNG only, 0 Rewards steps" },
        },
        ["DARV"] = new[] {
            // Seven Ancient events use dynamic relic-ID keys for three-way choices, with no static key.
            new EvOpt { Name="Choose 1 of 3 boss-level relics", AddRelic="CHOICE", Note="Free; DustyTome always replaces slot 3, not a random slot (Darv L153-162: Take(2), then Add)" },
        },
        ["DENSE_VEGETATION"] = new[] {
            new EvOpt { Name="Push through", Key="TRUDGE_ON", Hp=-8, Gold=80, Note="61-99 gold; expected 80 (checked against source)" },
            new EvOpt { Name="Rest", Key="REST", HealPct=30, Note="Heal 30%, then mandatory combat with no loot (later FIGHT page)" },
        },
        ["DOLL_ROOM"] = new[] {
            // Grade B missing option: DollRoom L86-91 has RANDOM, TAKE_SOME_TIME (5 HP loss), EXAMINE; old table omitted the middle option.
            new EvOpt { Name="Take one casually", Key="RANDOM", AddRelic="ROLL", Note="1 of 3 random dolls, free" },
            new EvOpt { Name="Take time to choose", Key="TAKE_SOME_TIME", Hp=-5, AddRelic="CHOICE", Note="Randomly remove 1 of 3 dolls, then choose between the remaining 2" },
            new EvOpt { Name="Inspect carefully", Key="EXAMINE", Hp=-15, AddRelic="CHOICE", Note="Lose 15 HP to choose 1 of 3" },
        },
        ["DOORS_OF_LIGHT_AND_DARK"] = new[] {
            new EvOpt { Name="Door of Light", Key="LIGHT", UpgradeCards=2, Note="2 random cards" },
            new EvOpt { Name="Door of Darkness", Key="DARK", RemoveCards=1, Note="Choose 1 card" },
        },
        ["DROWNING_BEACON"] = new[] {
            new EvOpt { Name="Retrieve the bottle", Key="BOTTLE", AddPotions=1, Note="Fixed glowing-water potion" },
            new EvOpt { Name="Climb the lighthouse", Key="CLIMB", MaxHp=-13, AddRelic="FRESNEL_LENS" },
        },
        ["ENDLESS_CONVEYOR"] = new[] {
            // Grade B rewrite: mutually exclusive EndlessConveyor paths (L128-132/L259-269); OBSERVE_CHEF appears only initially and ends the event.
            // After taking a dish, only take-more/LEAVE remain; no later chef upgrade. Each dish costs 40 (LOCKED if unaffordable); MinGold=120 is an entry condition.
            new EvOpt { Name="Take dishes (assume 2 dishes for expected value)", Gold=-80, MaxHp=2, MinGold=40, Note="40 gold per dish; weighted random benefits. Dish-name keys are dynamic, so name matching does not apply" },
            new EvOpt { Name="Watch the chef", Key="OBSERVE_CHEF", UpgradeCards=1, Note="Upgrade 1 random card free; choosing this immediately ends the event" },
        },
        ["FAKE_MERCHANT"] = new[] {
            // CUSTOM: GenerateInitialOptions returns no EventOptions for custom UI; these are strategy abstractions requiring special live handling, not index/name matching.
            new EvOpt { Name="Throw Foul Potion and fight", AddRelic="ROLL", Note="Requires FoulPotion; victory gives the rug and all fake relics (D tier)" },
            new EvOpt { Name="Leave", Note="D-tier fake relics are not worth buying" },
        },
        ["FIELD_OF_MAN_SIZED_HOLES"] = new[] {
            // Grade A reorder: [RESIST (remove 2 + curse), ENTER_YOUR_HOLE (enchant)]; old table was reversed.
            new EvOpt { Name="Resist", Key="RESIST", RemoveCards=2, AddCurse="NORMALITY", Note="Remove 2 chosen cards and gain a curse" },
            new EvOpt { Name="Enter the hole", Key="ENTER_YOUR_HOLE", Note="Enchant 1 card with PerfectFit, free" },
        },
        ["GRAVE_OF_THE_FORGOTTEN"] = new[] {
            // Grade A reorder: [CONFRONT (curse + enchant, possibly LOCKED), ACCEPT (relic)]; old table was reversed.
            new EvOpt { Name="Face it", Key="CONFRONT", AddCurse="DECAY", Note="Gain a curse and a soul enchantment on 1 card (LOCKED if none eligible)" },
            new EvOpt { Name="Accept", Key="ACCEPT", AddRelic="FORGOTTEN_SOUL", Note="Free reward" },
        },
        ["HUNGRY_FOR_MUSHROOMS"] = new[] {
            // RelicOption uses the relic ID as Key.
            new EvOpt { Name="Big Mushroom", Key="BIG_MUSHROOM", AddRelic="BIG_MUSHROOM", Note="Free reward" },
            new EvOpt { Name="Fragrant Mushroom", Key="FRAGRANT_MUSHROOM", Hp=-15, UpgradeCards=2, AddRelic="FRAGRANT_MUSHROOM", Note="The old table omitted 2 card upgrades" },
        },
        ["INFESTED_AUTOMATON"] = new[] {
            new EvOpt { Name="Study", Key="STUDY", AddCards=1, RewardsSteps=3, Note="Mandatory addition of a random Power card" },
            new EvOpt { Name="Touch the core", Key="TOUCH_CORE", AddCards=1, RewardsSteps=3, Note="Mandatory addition of a random zero-cost card" },
        },
        ["JUNGLE_MAZE_ADVENTURE"] = new[] {
            new EvOpt { Name="Go alone", Key="SOLO_QUEST", Hp=-18, Gold=150, Note="150 +/-15 gold (checked against source)" },
            new EvOpt { Name="Go together", Key="JOIN_FORCES", Gold=50, Note="No damage; 50 +/-15 gold" },
        },
        ["LOST_WISP"] = new[] {
            // Grade A reorder: [CLAIM (relic + Decay), SEARCH (+60)]; old table was reversed.
            new EvOpt { Name="Claim", Key="CLAIM", AddRelic="LOST_WISP", AddCurse="DECAY", Note="A-tier relic, but adds Decay" },
            new EvOpt { Name="Search", Key="SEARCH", Gold=60, Note="No damage; 60 +/-15 gold" },
        },
        ["LUMINOUS_CHOIR"] = new[] {
            // Grade A reorder: [REACH_INTO_THE_FLESH, OFFER_TRIBUTE (possibly LOCKED)]; old table was reversed.
            new EvOpt { Name="Reach into the flesh", Key="REACH_INTO_THE_FLESH", RemoveCards=2, AddCurse="SPORE_MIND" },
            new EvOpt { Name="Offer tribute", Key="OFFER_TRIBUTE", Gold=-125, AddRelic="ROLL", RewardsSteps=1, MinGold=149, Note="Threshold = 149-Rng(0,50), or 99-149; MinGold=149 is conservative" },
        },
        ["MORPHIC_GROVE"] = new[] {
            // Grade A reorder: [GROUP (lose gold + transform 2), LONER (+5 max HP)]; old table was reversed.
            new EvOpt { Name="Join the group", Key="GROUP", Gold=-9999, TransformCards=2, Note="Lose all gold" },
            new EvOpt { Name="Go alone", Key="LONER", MaxHp=5, Note="No cost" },
        },
        // ═══ N-Z ═══
        ["NONUPEIPE"] = new[] { new EvOpt { Name="Choose 1 of 3 trinket relics", AddRelic="CHOICE", Note="Relic-ID key is dynamic" } },
        ["OROBAS"] = new[] { new EvOpt { Name="Choose 1 of 3 relics from 3 pools", AddRelic="CHOICE", Note="Relic-ID key is dynamic" } },
        ["PAEL"] = new[] { new EvOpt { Name="Choose 1 of 3 Pael relics from 3 pools", AddRelic="CHOICE", Note="Relic-ID key is dynamic" } },
        ["POTION_COURIER"] = new[] {
            // Grade A reorder: [GRAB_POTIONS (3 Foul), RANSACK (1 uncommon)]; old table was reversed.
            new EvOpt { Name="Take potions", Key="GRAB_POTIONS", AddPotions=3, Note="3 fixed FoulPotions; keep one for the fake merchant encounter" },
            new EvOpt { Name="Loot", Key="RANSACK", AddPotions=1, RewardsSteps=1, Note="1 random uncommon potion" },
        },
        ["PUNCH_OFF"] = new[] {
            // Grade A reorder: [NAB (Injury + relic), I_CAN_TAKE_THEM (combat)]; old table was reversed.
            new EvOpt { Name="Nab", Key="NAB", AddRelic="ROLL", AddCurse="INJURY", RewardsSteps=1 },
            new EvOpt { Name="Fight", Key="I_CAN_TAKE_THEM", AddRelic="ROLL", AddPotions=1, Gold=94, RewardsSteps=2, Note="Defeat 2 boxing constructs (later FIGHT page); 91-98 gold" },
        },
        ["RANWID_THE_ELDER"] = new[] {
            // Grade B addition/reorder: RanwidTheElder L77-108 has POTION (possibly LOCKED), GOLD, RELIC (possibly LOCKED).
            // The event randomly specifies the potion/relic to surrender; the player does not choose. Old table omitted potion and had wrong order.
            new EvOpt { Name="Hand over a potion", Key="POTION", AddPotions=-1, AddRelic="ROLL", RewardsSteps=1, Note="Discard 1 random potion for 1 relic (LOCKED if no potions)" },
            new EvOpt { Name="Hand over 100 gold", Key="GOLD", Gold=-100, AddRelic="ROLL", RewardsSteps=1, MinGold=100 },
            new EvOpt { Name="Trade a relic for 2", Key="RELIC", AddRelic="ROLL_X2", RewardsSteps=2, Note="Lose 1 random tradable relic; net +1 (LOCKED if none tradable)" },
        },
        ["REFLECTIONS"] = new[] {
            new EvOpt { Name="Touch the mirror", Key="TOUCH_A_MIRROR", UpgradeCards=4, Note="Downgrade up to 2, then upgrade up to 4; net gain" },
            new EvOpt { Name="Shatter", Key="SHATTER", AddCurse="BAD_LUCK", Note="Doubles the deck! Devastating for thin-deck builds; almost never choose" },
        },
        ["RELIC_TRADER"] = new[] {
            // TOP/MIDDLE/BOTTOM keys and 1-3 options are dynamic; use a single abstract entry with index fallback.
            new EvOpt { Name="Exchange one", AddRelic="ROLL", RewardsSteps=3, Note="Lose 1 and gain 1 as a sidegrade gamble; 3 steps on entry. TOP/MIDDLE/BOTTOM keys are dynamic" },
        },
        ["ROOM_FULL_OF_CHEESE"] = new[] {
            // Grade A reorder: [GORGE (choose 2 of 8), SEARCH (-14 HP + relic)]; old table was reversed.
            new EvOpt { Name="Feast", Key="GORGE", AddCards=2, RewardsSteps=16, Note="Choose 2 of 8 common cards (uniform; 2 steps/card)" },
            new EvOpt { Name="Search", Key="SEARCH", Hp=-14, AddRelic="CHOSEN_CHEESE", Note="A-tier relic" },
        },
        ["ROUND_TEA_PARTY"] = new[] {
            new EvOpt { Name="Drink tea", Key="ENJOY_TEA", HealPct=100, AddRelic="ROYAL_POISON", Note="Full heal plus a negative D-tier relic; healing is the main benefit" },
            new EvOpt { Name="Provoke", Key="PICK_FIGHT", Hp=-11, AddRelic="ROLL", RewardsSteps=1, Note="Later page: CONTINUE_FIGHT" },
        },
        ["SAPPHIRE_SEED"] = new[] {
            new EvOpt { Name="Eat", Key="EAT", Hp=9, UpgradeCards=1, Note="Heal 9 HP and upgrade 1 chosen card" },
            new EvOpt { Name="Plant", Key="PLANT", Note="Enchant 1 card with Sown" },
        },
        ["SELF_HELP_BOOK"] = new[] {
            // Each option enchants one card of its corresponding type with 2 stacks, free; unavailable types are LOCKED, so 1-4 options vary dynamically.
            new EvOpt { Name="Read the back cover (Attacks: Sharp+2)", Key="READ_THE_BACK", UpgradeCards=1, Bonus=0.5, Note="Attack cards gain 2 damage, free" },
            new EvOpt { Name="Read a passage (Skills: Nimble+2)", Key="READ_PASSAGE", UpgradeCards=1, Note="Skill cards gain 2 Block, free" },
            new EvOpt { Name="Read it all (Powers: Swift+2)", Key="READ_ENTIRE_BOOK", UpgradeCards=1, Note="Enchant Power cards, free" },
            new EvOpt { Name="Skip", Key="NO_OPTIONS", Note="Only option when no cards can be enchanted" },
        },
        ["SLIPPERY_BRIDGE"] = new[] {
            new EvOpt { Name="Let go", Key="OVERCOME", RemoveCards=1, Note="Remove the displayed random non-basic card, free" },
            new EvOpt { Name="Hold on once, then let go", Key="HOLD_ON_0", Hp=-3, RemoveCards=1, Note="Lose 3 HP to reroll the target (later HOLD_ON_n pages)" },
        },
        ["SPIRALING_WHIRLPOOL"] = new[] {
            new EvOpt { Name="Watch the whirlpool", Key="OBSERVE", Note="Enchant 1 card with Spiral" },
            new EvOpt { Name="Drink", Key="DRINK", HealPct=33 },
        },
        ["SPIRIT_GRAFTER"] = new[] {
            new EvOpt { Name="Accept", Key="LET_IT_IN", Hp=25, AddCards=1, Note="Heal 25 HP and gain the Metamorphosis Power card" },
            new EvOpt { Name="Reject", Key="REJECTION", Hp=-10, UpgradeCards=1 },
        },
        ["STONE_OF_ALL_TIME"] = new[] {
            // Both options have possible LOCKED variants.
            new EvOpt { Name="Lift the stone", Key="LIFT", MaxHp=10, AddPotions=-1, Note="Discard 1 random potion for +10 max HP (LOCKED if no potions)" },
            new EvOpt { Name="Push the stone", Key="PUSH", Hp=-6, Note="Enchant 1 card with Vigorous x8" },
        },
        ["SUNKEN_STATUE"] = new[] {
            new EvOpt { Name="Draw the sword", Key="GRAB_SWORD", AddRelic="SWORD_OF_STONE", Note="Free reward; kill 5 elites to turn it into the jade sword" },
            new EvOpt { Name="Dive", Key="DIVE_INTO_WATER", Hp=-7, Gold=111, Note="111 +/-10 gold (checked against source)" },
        },
        ["SUNKEN_TREASURY"] = new[] {
            // Grade A reorder: [FIRST_CHEST (small, 60 +/-8), SECOND_CHEST (large, 333 +/-30 + Greed)]; old table was reversed.
            new EvOpt { Name="Small chest", Key="FIRST_CHEST", Gold=60, Note="60 +/-8 gold" },
            new EvOpt { Name="Large chest", Key="SECOND_CHEST", Gold=333, AddCurse="GREED", Note="333 +/-30 gold" },
        },
        ["SYMBIOTE"] = new[] {
            new EvOpt { Name="Approach", Key="APPROACH", Note="Enchant 1 card with Corrupted (strong); LOCKED if none eligible" },
            new EvOpt { Name="Burn it", Key="KILL_WITH_FIRE", TransformCards=1 },
        },
        ["TABLET_OF_TRUTH"] = new[] {
            // Grade A reorder: [DECIPHER_1, SMASH (heal 20)]; old table was reversed. Later pages: DECIPHER/GIVE_UP.
            // Decipher costs: 3/6/12/24/(MaxHp-1); level 5 upgrades all cards (TabletOfTruth.GetDecipherCost L76-93).
            new EvOpt { Name="Decipher 2 levels", Key="DECIPHER_1", MaxHp=-9, UpgradeCards=2, Note="Lose 3+6 max HP to upgrade 2 random cards; deeper levels (-12/-24/all HP) require caution" },
            new EvOpt { Name="Smash it", Key="SMASH", Hp=20, Note="Heal 20 HP" },
        },
        ["TANX"] = new[] { new EvOpt { Name="Choose 1 of 3 melee relics", AddRelic="CHOICE", Note="Relic-ID key is dynamic" } },
        ["TEA_MASTER"] = new[] {
            // Grade A reorder: [BONE_TEA (-50, possibly locked), EMBER_TEA (-150, possibly locked), TEA_OF_DISCOURTESY (free)]; old order was rotated.
            new EvOpt { Name="Bone Tea", Key="BONE_TEA", Gold=-50, AddRelic="BONE_TEA", MinGold=50 },
            new EvOpt { Name="Ember Tea", Key="EMBER_TEA", Gold=-150, AddRelic="EMBER_TEA", MinGold=150 },
            new EvOpt { Name="Discourteous Tea", Key="TEA_OF_DISCOURTESY", AddRelic="TEA_OF_DISCOURTESY", Note="Free" },
        },
        ["TEZCATARA"] = new[] { new EvOpt { Name="Choose 1 of 3 food relics from 3 pools", AddRelic="CHOICE", Note="Relic-ID key is dynamic" } },
        ["THE_ARCHITECT"] = new[] {
            // Grade C addition: final victory dialogue in combat layout; advance dialogue and PROCEED -> WinRun. Explicit exception with no strategic value.
            new EvOpt { Name="Advance the dialogue to win", Key="PROCEED", Note="WinRun; no decision point" },
        },
        ["THE_FUTURE_OF_POTIONS"] = new[] {
            // One option per potion, up to 3; every TextKey is POTION. After name matching, choose the rarest bottle (option i = potion i).
            new EvOpt { Name="Trade 1 potion", Key="POTION", AddCards=1, AddPotions=-1, RewardsSteps=6, Note="Choose 1 of 3 upgraded cards of the same rarity" },
        },
        ["THE_LANTERN_KEY"] = new[] {
            // Grade A reorder: [RETURN_THE_KEY (+100), KEEP_THE_KEY (combat)]; old table was reversed.
            new EvOpt { Name="Return the key", Key="RETURN_THE_KEY", Gold=100 },
            new EvOpt { Name="Keep the key and fight", Key="KEEP_THE_KEY", AddCardEntry="lanternkey", AddCardType=0, Bonus=5.0, Note="Later FIGHT page; victory grants key card. Act 3 ChainActs grants HistoryCourse (zero-cost replay of last turn's final Attack/Skill); add +6 when redeemed" },
        },
        ["THE_LEGENDS_WERE_TRUE"] = new[] {
            new EvOpt { Name="Take the map", Key="NAB_THE_MAP", AddCards=1, Note="Spoils-map card (improves chests)" },
            new EvOpt { Name="Find the exit slowly", Key="SLOWLY_FIND_AN_EXIT", Hp=-8, AddPotions=1, RewardsSteps=1 },
        },
        ["THIS_OR_THAT"] = new[] {
            // Grade A reorder: [PLAIN (-6 HP +54 gold), ORNATE (relic + Clumsy)]; old table was reversed.
            new EvOpt { Name="Plain", Key="PLAIN", Hp=-6, Gold=54, Note="41-68 gold; expected 54 (checked against source)" },
            new EvOpt { Name="Fancy", Key="ORNATE", AddRelic="ROLL", AddCurse="CLUMSY", RewardsSteps=1 },
        },
        ["TINKER_TIME"] = new[] {
            // Grade B correction: TinkerTime is constrained; page 2 offers 2 random types from ATTACK/SKILL/POWER, then page 3 offers 2 of 9 type-specific modifiers.
            new EvOpt { Name="Customize a Mad Science card", Key="CHOOSE_CARD_TYPE", AddCards=1, Note="Random choice between 2 types and between 2 modifiers, not unrestricted selection" },
        },
        ["TRASH_HEAP"] = new[] {
            new EvOpt { Name="Jump in", Key="DIVE_IN", Hp=-8, AddRelic="ROLL", Note="Random relic from a 5-item pool: Darkstone, Dream Catcher, Hand Drill, Maw Bank or Boots" },
            new EvOpt { Name="Grab a handful", Key="GRAB", Gold=100, AddCards=1, Note="Gain 100 gold, but must add 1 random card from a 10-card pool" },
        },
        ["TRIAL"] = new[] {
            // Grade B correction (Trial L116-126/L164-168): trial type is random (MERCHANT/NOBLE/NONDESCRIPT, each 1/3), but GUILTY/INNOCENT is a player choice.
            // NobleGuilty heals 10 without a curse. REJECT leads to ACCEPT/DOUBLE_DOWN (abandon-save confirmation), not a clean exit.
            new EvOpt { Name="Accept judgment (expected value)", Key="ACCEPT", Gold=100, UpgradeCards=1, RewardsSteps=1,
                Note="Each type has 1/3 probability; choose the better GUILTY/INNOCENT option: merchant (2 relics/2 upgrades), noble (heal 10 without curse/300 gold), passerby (2 cards/2 transforms)" },
            new EvOpt { Name="Refuse (forced back)", Key="REJECT", Note="Leads to accept-again/DOUBLE_DOWN (abandon-save confirmation); no clean exit, so effectively accept. Never select DOUBLE_DOWN" },
        },
        ["UNREST_SITE"] = new[] {
            // Grade A reorder: [REST (full heal + PoorSleep), KILL (-8 max HP + relic)]; old table was reversed.
            new EvOpt { Name="Rest", Key="REST", HealPct=100, AddCurse="POOR_SLEEP", Note="Emergency option at very low HP" },
            new EvOpt { Name="Kill", Key="KILL", MaxHp=-8, AddRelic="ROLL", RewardsSteps=1 },
        },
        ["VAKUU"] = new[] { new EvOpt { Name="Choose 1 of 3 relics from 3 pools", AddRelic="CHOICE", Note="Relic-ID key is dynamic" } },
        ["WAR_HISTORIAN_REPY"] = new[] {
            // UNLOCK_CAGE/UNLOCK_CHEST may occur consecutively; one take-all abstraction covers both via index fallback.
            new EvOpt { Name="Unlock cage and chest (take all in solo)", AddRelic="ROLL_X2", AddPotions=2, RemoveCards=1, RewardsSteps=4,
                Note="Requires LanternKey quest; HistoryCourse [S], 2 potions, 2 relic rolls; consecutive UNLOCK_CAGE/UNLOCK_CHEST keys" },
        },
        ["WATERLOGGED_SCRIPTORIUM"] = new[] {
            // Blood ink is always present; paid enchantments become LOCKED when unaffordable, giving a dynamic 1-3-option list.
            // Grade B correction: 6/55/99 are fixed values (GoldVar, WaterloggedScriptorium L30 and IsAllowed); pen cost was wrongly 35, should be 55.
            new EvOpt { Name="Blood ink (+max HP)", Key="BLOODY_INK", MaxHp=6, Note="Free +6 max HP (fixed)" },
            new EvOpt { Name="Tentacle pen (pay to enchant 1 card with Steady)", Key="TENTACLE_QUILL", Gold=-55, MinGold=55, UpgradeCards=1, Note="Steady enchantment on 1 card; fixed cost 55 gold" },
            new EvOpt { Name="Spiny sponge (pay to enchant multiple cards with Steady)", Key="PRICKLY_SPONGE", Gold=-99, MinGold=99, UpgradeCards=2, Note="Steady enchantment on 2 cards (CardsVar(2)); fixed cost 99 gold" },
        },
        ["WELCOME_TO_WONGOS"] = new[] {
            // Grade A reorder: [BARGAIN_BIN (-100, possibly locked), FEATURED_ITEM (-200, possibly locked), MYSTERY_BOX (possibly locked), LEAVE]; first two were swapped.
            new EvOpt { Name="Discount section", Key="BARGAIN_BIN", Gold=-100, AddRelic="ROLL", MinGold=100, Note="Common tier" },
            new EvOpt { Name="Featured item", Key="FEATURED_ITEM", Gold=-200, AddRelic="ROLL", MinGold=200, Note="Rare-tier relic" },
            new EvOpt { Name="Mystery box", Key="MYSTERY_BOX", Gold=-300, AddRelic="WONGOS_MYSTERY_TICKET", MinGold=300, Note="Opens after 5 combats for 3 relics" },
            new EvOpt { Name="Leave", Key="LEAVE", UpgradeCards=-1, Note="Penalty: randomly downgrade 1 upgraded card!" },
        },
        ["WELLSPRING"] = new[] {
            new EvOpt { Name="Bottle", Key="BOTTLE", AddPotions=1, RewardsSteps=1 },
            new EvOpt { Name="Bathe", Key="BATHE", RemoveCards=1, AddCurse="GUILTY", Note="Remove 1 chosen card, but gain Guilty" },
        },
        ["WHISPERING_HOLLOW"] = new[] {
            new EvOpt { Name="Pay", Key="GOLD", Gold=-35, AddPotions=2, RewardsSteps=2, MinGold=44, Note="Cost 35 +/-9, or 26-44; MinGold=44 is conservative" },
            new EvOpt { Name="Embrace", Key="HUG", Hp=-9, TransformCards=1 },
        },
        ["WOOD_CARVINGS"] = new[] {
            // Source order: BIRD, SNAKE (possibly LOCKED), TORUS. Reorder old BIRD/TORUS/SNAKE for index fallback; bird/ring transform a chosen basic card (remove 1, add 1).
            new EvOpt { Name="Bird (basic card -> Peck)", Key="BIRD", TransformCards=1, Bonus=1.0, Note="Transform a chosen basic card into Peck" },
            new EvOpt { Name="Snake (Slither enchantment)", Key="SNAKE", UpgradeCards=1, Note="Enchant 1 card; LOCKED if none eligible" },
            new EvOpt { Name="Ring (basic card -> ToricToughness)", Key="TORUS", TransformCards=1, Bonus=1.0, Note="Transform a chosen basic card into ToricToughness" },
        },
        ["ZEN_WEAVER"] = new[] {
            // Grade A reorder: [BREATHING (-50), EMOTIONAL (-125), ARACHNID (-250)]; old table was reversed. All three locked variants share LOCKED.
            new EvOpt { Name="Breathing technique", Key="BREATHING_TECHNIQUES", Gold=-50, AddCards=2, MinGold=50, Note="2 Enlightenment cards" },
            new EvOpt { Name="Emotional awareness", Key="EMOTIONAL_AWARENESS", Gold=-125, RemoveCards=1, MinGold=125 },
            new EvOpt { Name="Spider acupuncture", Key="ARACHNID_ACUPUNCTURE", Gold=-250, RemoveCards=2, MinGold=250 },
        },
    };

    public static EvOpt[] Get(string evId) =>
        evId != null && Table.TryGetValue(evId, out var v) ? v : null;
}
