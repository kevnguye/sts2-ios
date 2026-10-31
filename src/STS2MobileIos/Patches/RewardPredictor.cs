using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;

namespace STS2MobileIos.Patches;

// In-game reward predictor using cloned RNG state and the candidate pool.
// Given a Rewards RNG clone at the dealing position, options and pity, reproduce the three generated cards.
// Use the game's GetPossibleCards filters and Rng.NextItem instead of hard-coded pools, for every character.
public static class RewardPredictor
{
    public struct Pred { public string Entry; public CardRarity Rarity; public bool Upgraded; public int Type; }

    // clone: independent Rewards RNG copy at the dealing position; pity: current rarity pity value; a7: Ascension >= 7.
    // dbg: when provided, record each card's roll, thresholds, offset and rarity to locate divergences.
    public static List<Pred> Predict(Player player, CardCreationOptions options, Rng clone,
        float pity, int actIndex, bool a7, List<string> dbg = null)
        => Predict(player, options, clone, pity, actIndex, a7, out _, dbg);

    // finalPity: pity after dealing all three cards, carried across combats during multi-step lookahead.
    public static List<Pred> Predict(Player player, CardCreationOptions options, Rng clone,
        float pity, int actIndex, bool a7, out float finalPity, List<string> dbg = null,
        int count = 3, bool usePity = true)
    {
        var res = new List<Pred>();
        // CreateForReward first calls Hook.ModifyCardRewardCreationOptions (for pool-expanding relics such as PrismaticGem).
        // Our hook receives options before that call; apply it here too, or pool order and NextItem selection will differ.
        options = ApplyRewardOptionsHook(player, options);
        var oddsType = options.RarityOdds;
        // Use GetPossibleCards plus FilterForPlayerCount, which removes MultiplayerOnly cards in solo play while preserving order.
        // Omitting this shifts NextItem indices and selects the wrong card, especially for elite rewards.
        var baseCards = GameFilterForPlayerCount(player, options.GetPossibleCards(player)).ToList();
        var black = new HashSet<CardModel>();
        float upScale = a7 ? 0.125f : 0.25f;

        for (int i = 0; i < count; i++)
        {
            // 1. Rarity: get thresholds directly from the game's static GetBaseOdds through reflection, without copied constants.
            float rareT = GameBaseOdds(oddsType, CardRarity.Rare);
            float uncT = GameBaseOdds(oddsType, CardRarity.Uncommon);
            float offset = (!usePity || oddsType == CardRarityOddsType.BossEncounter) ? 0f : pity;
            float roll = clone.NextFloat(1f);
            CardRarity rarity;
            float pityBefore = pity;
            // Game logic: rare threshold = rareBase + offset; uncommon threshold = uncommonBase + rareBase + offset.
            float rareThr = rareT + offset;
            float uncThr = uncT + rareThr;
            if (roll < rareThr) { rarity = CardRarity.Rare; if (usePity) pity = -0.05f; }
            else if (roll < uncThr) { rarity = CardRarity.Uncommon; if (usePity) pity = Bump(pity, a7); }
            else { rarity = CardRarity.Common; if (usePity) pity = Bump(pity, a7); }
            dbg?.Add($"Card {i + 1}: roll={roll:R} rareT={rareT:R} uncT={uncT:R} off={offset:R}(pityBefore={pityBefore:R}) -> {rarity} (rareThr={rareThr:R} uncThr={uncThr:R})");

            // Remove blacklisted cards and filter the real pool by rarity, preserving order; advance if empty (GetNextAllowedRarity).
            var cands = Filter(baseCards, black, rarity);
            if (cands.Count == 0)
                rarity = NextAllowed(baseCards, black, rarity, out cands);
            if (cands.Count == 0) break;

            // 2. Choose a card using the game's native NextItem.
            var card = clone.NextItem<CardModel>(cands);
            black.Add(card.CanonicalInstance);

            // 3. Reproduce RollForUpgrade, always consuming one RNG step.
            float up = clone.NextFloat(1f);
            float chance = rarity == CardRarity.Rare ? 0f : actIndex * upScale;
            bool upg = card.IsUpgradable && up <= chance;

            res.Add(new Pred { Entry = card.Id.Entry, Rarity = rarity, Upgraded = upg, Type = (int)card.Type });
        }
        finalPity = pity;
        return res;
    }

    static float Bump(float pity, bool a7) => System.MathF.Min(pity + (a7 ? 0.005f : 0.01f), 0.4f);

    // Call the game's nonpublic static CardRarityOdds.GetBaseOdds(oddsType, rarity), without copied constants.
    static System.Reflection.MethodInfo _getBaseOdds;
    static float GameBaseOdds(CardRarityOddsType t, CardRarity rarity)
    {
        _getBaseOdds ??= typeof(MegaCrit.Sts2.Core.Odds.CardRarityOdds).GetMethod("GetBaseOdds",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        return (float)_getBaseOdds.Invoke(null, new object[] { t, rarity });
    }

    // Apply Hook.ModifyCardRewardCreationOptions(runState, player, options) for pool-expanding relics such as PrismaticGem.
    // Without such relics, the hook returns options unchanged. Invoke through reflection (Hook is in Core.Hooks).
    static System.Reflection.MethodInfo _rewardOptHook;
    static CardCreationOptions ApplyRewardOptionsHook(Player player, CardCreationOptions options)
    {
        try
        {
            _runStateProp ??= typeof(Player).GetProperty("RunState");
            var runState = _runStateProp.GetValue(player);
            _rewardOptHook ??= typeof(CardCreationOptions).Assembly
                .GetType("MegaCrit.Sts2.Core.Hooks.Hook")?
                .GetMethod("ModifyCardRewardCreationOptions",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            if (_rewardOptHook != null)
                return (CardCreationOptions)_rewardOptHook.Invoke(null, new object[] { runState, player, options });
        }
        catch { }   // If the hook fails, fall back to the original options and the base prediction.
        return options;
    }

    // Call the game's private static CardFactory.FilterForPlayerCount(runState, options) for matching results.
    static System.Reflection.MethodInfo _filterPlayerCount;
    static System.Reflection.PropertyInfo _runStateProp;
    static IEnumerable<CardModel> GameFilterForPlayerCount(Player player, IEnumerable<CardModel> pool)
    {
        _filterPlayerCount ??= typeof(MegaCrit.Sts2.Core.Factories.CardFactory).GetMethod("FilterForPlayerCount",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        _runStateProp ??= typeof(Player).GetProperty("RunState");
        var runState = _runStateProp.GetValue(player);
        return (IEnumerable<CardModel>)_filterPlayerCount.Invoke(null, new object[] { runState, pool });
    }

    static List<CardModel> Filter(List<CardModel> pool, HashSet<CardModel> black, CardRarity rar) =>
        pool.Where(c => c.Rarity == rar && !black.Contains(c.CanonicalInstance)).ToList();

    // Advance to a rarity available in the pool, wrapping Common -> Uncommon -> Rare -> Common.
    static CardRarity NextAllowed(List<CardModel> pool, HashSet<CardModel> black, CardRarity rar, out List<CardModel> cands)
    {
        var order = new[] { CardRarity.Common, CardRarity.Uncommon, CardRarity.Rare };
        int start = System.Array.IndexOf(order, rar);
        for (int k = 1; k <= 3; k++)
        {
            var next = order[(start + k) % 3];
            cands = Filter(pool, black, next);
            if (cands.Count > 0) return next;
        }
        cands = new List<CardModel>();
        return rar;
    }
}
