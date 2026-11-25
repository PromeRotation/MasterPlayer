using MasterPlayer.Replay;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class QuantumEncounter
{
    private ulong lightMask;
    private ulong darkMask;
    private ulong combined;
    private readonly DateTime[] expirations = new DateTime[4];
    private readonly bool[] wantLight = new bool[2];
    private readonly DateTime[] drainActivations = new DateTime[2];
    private readonly DateTime[] colorActivations = new DateTime[2];
    private bool drainActive;
    private bool boundsTowers;
    private int drainCasts;
    private uint griefHp;
    private uint eaterHp;
    private float hpDifference;
    internal bool WantsLight => wantLight[0];
    internal int SlotOf(QuantumActor actor) => Slot(actor);

    private int Slot(QuantumActor actor) => party.FirstOrDefault(p => p.EntityId == actor.Actor.Id)?.Slot ?? -1;
    private static bool HasBit(ulong mask, int slot) => slot is >= 0 and < 64 && (mask & 1UL << slot) != 0;
    private static void SetBit(ref ulong mask, int slot, bool value)
    {
        if (slot is < 0 or >= 64) return;
        if (value) mask |= 1UL << slot;
        else mask &= ~(1UL << slot);
    }

    private void BootstrapLight()
    {
        ResetLight();
        foreach (var actor in Actors.Values)
        {
            if (NativeNpc(actor.Actor) && actor.Cast != null) LightCast(CaptureCast(actor));
            foreach (var status in actor.Statuses.OfType<ReplayStatus>())
                StatusChanged(actor, status, true, false);
        }
    }

    private void StatusChanged(QuantumActor actor, ReplayStatus status, bool gained, bool hot)
    {
        if (!Constructed || hot && actor.Actor.NativeType == 0x202) return;
        if (status.Id is 4560 or 4559)
        {
            var slot = Slot(actor);
            var present = gained && status.ExpiresAt != default;
            if (status.Id == 4560) SetBit(ref lightMask, slot, present);
            else SetBit(ref darkMask, slot, present);
            // Deliberately not lightMask | darkMask: losing either clears combined.
            SetBit(ref combined, slot, present);
            if (slot is >= 0 and < 4) expirations[slot] = gained ? status.ExpiresAt : default;
            TowerStatus(actor, status.Id, gained);
        }
    }

    private void LightCast(DungeonCastObservation cast)
    {
        var slot = cast.Action is 44129 or 44132 ? 0 : 1;
        if (cast.Action is not (44129 or 44134 or 44132 or 44130)) return;
        wantLight[slot] = cast.Action is 44129 or 44130;
        drainActive = true;
        drainActivations[slot] = cast.FinishAt.AddSeconds(-1d);
    }

    private void LightFinish(uint action)
    {
        if (action is not (44129 or 44134 or 44132 or 44130)) return;
        if ((++drainCasts & 1) == 0) drainActive = false;
        wantLight[0] = wantLight[1];
        drainActivations[0] = drainActivations[1];
    }

    private void UpdateHp()
    {
        if (Eater == null || Primary == null) return;
        eaterHp = Eater.Actor.Hp ?? 0;
        griefHp = Primary.Actor.Hp ?? 0;
        hpDifference = unchecked((int)(eaterHp - griefHp)) * 100f / (Primary.Actor.MaxHp ?? 0);
    }

    // Calling the AI output mutates each colour's activation in the source.
    // Compare expiry with the previous activation before installing the new one.
    internal (bool Light, bool Dark, bool AvoidOther, DateTime Activation) ColorRequirement()
    {
        var slot = Actors.GetValueOrDefault(PlayerId) is { } actor ? Slot(actor) : -1;
        if (!drainActive)
        {
            if (eaterHp <= 1 && !HasBit(lightMask, slot)) return (true, false, false, colorActivations[0]);
            if (griefHp <= 1 && !HasBit(darkMask, slot)) return (false, true, false, colorActivations[1]);
            if (MathF.Abs(hpDifference) > 25 || !HasBit(combined, slot))
                return hpDifference > 0 ? (false, true, false, colorActivations[1]) : (true, false, false, colorActivations[0]);
            return default;
        }
        var index = wantLight[0] ? 0 : 1;
        var mask = wantLight[0] ? lightMask : darkMask;
        var risky = !HasBit(mask, slot) || slot is >= 0 and < 4 && expirations[slot] < colorActivations[index];
        if (risky) colorActivations[index] = drainActivations[0];
        return (wantLight[0] && risky, !wantLight[0] && risky, true, colorActivations[index]);
    }

    internal int TargetPriority(DungeonActor actor)
    {
        var slot = Actors.GetValueOrDefault(PlayerId) is { } player ? Slot(player) : -1;
        if (actor.Hp <= 1) return -1;
        if (actor.Oid == 0x48EF && HasBit(darkMask, slot)
            || actor.Oid == 0x48EE && HasBit(lightMask, slot)) return 0;
        return actor.Oid is 0x48F0 or 0x48F1 ? 1 : -1;
    }

    internal IEnumerable<string> Hints()
    {
        if (!Loaded || Actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        var slot = Slot(player);
        var light = HasBit(lightMask, slot);
        var dark = HasBit(darkMask, slot);
        if (Timeline.Stage == -1)
        {
            var l = System.Numerics.BitOperations.PopCount(lightMask);
            var d = System.Numerics.BitOperations.PopCount(darkMask);
            if (!HasBit(combined, slot))
            {
                if (combined == 0 || l == 1 && d == 1) yield return "Get light or dark buff!";
                else yield return l > d ? "Get dark buff!" : "Get light buff!";
            }
            else if (dark && d > 2 || light && l > 2) yield return "Switch to other color!";
        }
        else if (boundsTowers) yield return light ? "Get light buff!（已获得）" : "Get light buff!";
        else if (drainActive)
        {
            if (wantLight[0]) yield return light ? "Get light buff!（已获得）" : "Get light buff!";
            else yield return dark ? "Get dark buff!（已获得）" : "Get dark buff!";
        }
        else if (eaterHp <= 1 && !light) yield return "Get light buff!";
        else if (griefHp <= 1 && !dark) yield return "Get dark buff!";
        else if (MathF.Abs(hpDifference) > 25 || !HasBit(combined, slot))
            yield return hpDifference > 0 ? "Get dark buff!" : "Get light buff!";
        else yield return $"Target {(dark ? Eater?.Actor.Name : Primary?.Actor.Name)}!";
        if (hpDifference != 0) yield return $"Eater HP: {(hpDifference > 0 ? "+" : "")}{hpDifference:f1}%";
        if (Timeline.Stage == 12) yield return "Unsupported quantum level (upload replays!)";
        if (Timeline.Stage == 11) yield return "???";
        foreach (var stage in Timeline.Overdue) yield return $"Q40 state {stage}: transition triggered because of overdue";
        foreach (var hint in MechanicHints(player)) yield return hint;
    }

    private void ResetLight()
    {
        lightMask = darkMask = combined = 0;
        Array.Clear(expirations);
        Array.Clear(wantLight);
        Array.Clear(drainActivations);
        Array.Clear(colorActivations);
        drainCasts = 0;
        drainActive = boundsTowers = false;
        griefHp = eaterHp = 0;
        hpDifference = 0;
    }
}
