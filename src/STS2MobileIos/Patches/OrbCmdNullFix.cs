using System;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace STS2MobileIos.Patches;

// Original OrbCmd.AddSlots/RemoveSlots (416736) omitted a null check in UI animation code.
//   NCombatRoom.Instance?.GetCreatureNode(player.Creature).OrbManager?.AddSlotAnim(amount)
// The value above can be null; dereferencing .OrbManager then throws an NRE.
// Real players have UI nodes; simulated cloned creatures do not, so orb cards such as Glacier fail during simulation.
// Replace with a bool prefix that preserves the original logic and adds null guards. Real gameplay is unchanged.
public static class OrbCmdNullFix
{
    public static bool AddSlotsPrefix(Player player, int amount, ref Task __result)
    {
        __result = Task.CompletedTask;
        if (CombatManager.Instance.IsOverOrEnding) return false;
        amount = Math.Min(10 - player.PlayerCombatState.OrbQueue.Capacity, amount);
        player.PlayerCombatState.OrbQueue.AddCapacity(amount);
        NCombatRoom.Instance?.GetCreatureNode(player.Creature)?.OrbManager?.AddSlotAnim(amount);
        return false;
    }

    public static bool RemoveSlotsPrefix(Player player, int amount)
    {
        if (!CombatManager.Instance.IsOverOrEnding)
        {
            amount = Math.Min(player.PlayerCombatState.OrbQueue.Capacity, amount);
            player.PlayerCombatState.OrbQueue.RemoveCapacity(amount);
            NCombatRoom.Instance?.GetCreatureNode(player.Creature)?.OrbManager?.RemoveSlotAnim(amount);
        }
        return false;
    }

    // NCombatRoom.AddCreature creates UI nodes for summons (including creatures spawned on death), which fails for clones.
    // Skip UI-node creation while SimScope enables test mode. CombatState/CombatManager have already added
    // these creatures to the combat state; UI nodes are separate. Real gameplay leaves test mode off.
    public static bool AddCreatureUiPrefix()
        => !MegaCrit.Sts2.Core.TestSupport.TestMode.IsOn;   // true = create the node normally; false = skip it.
}
