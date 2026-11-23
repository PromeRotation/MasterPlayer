using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly Dictionary<ulong, DoomtrainActorReference> doomActors = [];
    private readonly HashSet<ulong> doomSeen = [];
    private readonly List<(DoomtrainActorReference Actor, DateTime At, float Length, float Offset, int Key)> doomLevin = [];
    private readonly List<DungeonAmbientAoe> doomHeadOn = [];
    private readonly Dictionary<(ulong Source, DateTime Start), string> doomHeadKeys = [];
    private readonly List<DungeonAmbientAoe> doomUpper = [];
    private readonly Dictionary<(ulong Source, DateTime Start), string> doomUpperKeys = [];
    private readonly List<DungeonAmbientAoe> doomDerail = [];
    private readonly Dictionary<(ulong Source, DateTime Start), string> doomDerailKeys = [];
    private readonly List<(DoomtrainActorReference Target, DateTime At, int Key)> doomTankBaits = [];
    private readonly List<(DoomtrainActorReference Source, DoomtrainActorReference Target, DateTime At, int Key)> doomRays = [];
    private readonly List<DoomtrainActorReference> doomArcane = [];
    private readonly List<DungeonPendingCast> doomStacks = [];
    private readonly HashSet<(ulong Source, DateTime Start)> doomStackKeys = [];
    private readonly Dictionary<ulong, (bool Present, ushort Param, DateTime Expiry, ulong Source)> doomRawStop = [];
    private readonly Dictionary<ulong, (bool Present, ushort Param, DateTime Expiry, ulong Source)> doomPendingStop = [];
    private DungeonPendingCast? doomWindpipe;
    private int doomKey;

    private DoomtrainActorReference DoomActor(DungeonActor actor)
    {
        if (!doomActors.TryGetValue(actor.Id, out var reference))
            doomActors[actor.Id] = reference = new(actor);
        reference.Actor = actor;
        return reference;
    }

    private void DoomObserve()
    {
        if (!IsDoomtrain || Active == null) return;
        foreach (var actor in _actors.Values)
        {
            var reference = DoomActor(actor);
            if (doomSeen.Add(actor.Id))
            {
                if (DoomEnabled(DoomtrainComponent.Levin)) DoomAddLevin(actor);
                if (DoomEnabled(DoomtrainComponent.Arcane) && actor.Oid == 0x4A36) doomArcane.Add(reference);
            }
            var next = (Present: actor.Statuses?.Contains(4176u) == true,
                Param: actor.StatusParams?.GetValueOrDefault(4176u) ?? 0,
                Expiry: actor.StatusExpirations?.GetValueOrDefault(4176u) ?? default,
                Source: actor.StatusSources?.GetValueOrDefault(4176u) ?? 0);
            if (doomPendingStop.TryGetValue(actor.Id, out var pending))
            {
                if (next.Present != pending.Present || next.Present && (next.Param != pending.Param || next.Source != pending.Source)) continue;
                doomPendingStop.Remove(actor.Id);
            }
            else DoomStopTransition(actor.Id, doomRawStop.GetValueOrDefault(actor.Id), next);
            doomRawStop[actor.Id] = next;
        }
        doomSeen.RemoveWhere(id => !_actors.ContainsKey(id));
    }

    private void DoomAddLevin(DungeonActor actor)
    {
        if (actor.Oid != 0x4A31) return;
        var ground = actor.Position.Y < 2;
        var length = ground ? 30 : 0;
        var offset = 0;
        if (doomCar == 2)
        {
            if (actor.Position.X is > 95 and < 100) length = 20;
            if (actor.Position.X is > 100 and < 105) length = 10;
        }
        if (doomCar == 3 && (actor.Position.X < 95 || actor.Position.X > 105))
        { length = ground ? 10 : 20; offset = ground ? 0 : 10; }
        if (doomCar == 5 && actor.Position.X < 95)
        { length = ground ? 20 : 10; offset = ground ? 0 : 20; }
        if (doomCar == 5 && actor.Position.X > 105)
        { length = 10; offset = ground ? 0 : 10; }
        doomLevin.Add((DoomActor(actor), _now.AddSeconds(7), length, offset, ++doomKey));
    }

    private void DoomCast(DungeonPendingCast cast)
    {
        if (!IsDoomtrain || Active == null) return;
        if (doomStage < 0) DoomTick();
        DoomRecordCast(cast);
        DoomTick();
    }

    private void DoomRecordCast(DungeonPendingCast cast, DoomtrainComponent? only = null)
    {
        var action = cast.Rule.Action;
        var component = action switch
        {
            45635 or 45637 => DoomtrainComponent.HeadOn,
            45630 => DoomtrainComponent.Upper,
            45654 => DoomtrainComponent.Derail,
            45667 => DoomtrainComponent.Windpipe,
            45649 => DoomtrainComponent.Stack,
            _ => (DoomtrainComponent?)null,
        };
        if (component == null || only != null && only != component || !DoomEnabled(component.Value)) return;
        if (component == DoomtrainComponent.Stack)
        {
            if (!doomActors.ContainsKey(cast.TargetId)) return;
            var stackKey = (cast.Source, cast.StartedAt);
            var stackIndex = doomStacks.FindIndex(c => c.Source == cast.Source && c.StartedAt == cast.StartedAt);
            if (stackIndex >= 0) doomStacks[stackIndex] = cast;
            else if (doomStackKeys.Add(stackKey)) doomStacks.Add(cast);
            return;
        }
        if (component == DoomtrainComponent.Windpipe)
        {
            if (doomCar is 2 or 5) doomWindpipe = cast;
            return;
        }
        var list = component switch
        {
            DoomtrainComponent.HeadOn => doomHeadOn,
            DoomtrainComponent.Upper => doomUpper,
            _ => doomDerail,
        };
        var keys = component switch
        {
            DoomtrainComponent.HeadOn => doomHeadKeys,
            DoomtrainComponent.Upper => doomUpperKeys,
            _ => doomDerailKeys,
        };
        var key = (cast.Source, cast.StartedAt);
        var corrected = keys.TryGetValue(key, out var id);
        id ??= $"doom/{component}/{++doomKey}";
        keys[key] = id;
        var aoe = component switch
        {
            DoomtrainComponent.HeadOn => new DungeonAmbientAoe(id, "迎面放射：地面/高台", new DungeonShapeTemplate(DoomDeck(action == 45637)),
                Arena!.Position),
            DoomtrainComponent.Upper => new DungeonAmbientAoe(id, "高台雷射", cast.Rule.Shape!, cast.Location, cast.Rotation, Activation: cast.FinishAt),
            _ => new DungeonAmbientAoe(id, "脱轨：进入后方出口", new DonutTemplate(2, 32),
                new(100, Arena?.Position.Y ?? 0, 262.5f), Activation: cast.FinishAt.AddSeconds(.6)),
        };
        var index = list.FindIndex(a => a.Id == id);
        if (index >= 0) list[index] = aoe;
        else if (!corrected) list.Add(aoe);
    }

    private void DoomFinish(DungeonPendingCast cast)
    {
        if (!IsDoomtrain || Active == null) return;
        if (cast.Rule.Action is 45635 or 45637) doomHeadOn.Clear();
        if (cast.Rule.Action == 45630) doomUpper.Clear();
        if (cast.Rule.Action == 45654)
        {
            doomDerail.Clear();
            if (DoomEnabled(DoomtrainComponent.ArenaChange)) DoomSetCar(5);
        }
        if (cast.Rule.Action == 45667) doomWindpipe = null;
        if (cast.Rule.Action == 45649)
        {
            var index = doomStacks.FindIndex(c => c.TargetId == cast.TargetId);
            if (index >= 0) doomStacks.RemoveAt(index);
        }
        // Finish only clears cast-owned geometry. State counters consume Effects.
    }

    private void DoomCorrectTarget(DungeonPendingCast previous, ulong target)
    {
        if (!IsDoomtrain || previous.TargetId == target) return;
        // The source feed restarts a cast when its target changes. Location or
        // rotation corrections alone keep the same locked cast instance.
        DoomFinish(previous);
        var key = (previous.Source, previous.StartedAt);
        doomHeadKeys.Remove(key); doomUpperKeys.Remove(key); doomDerailKeys.Remove(key); doomStackKeys.Remove(key);
    }

    private void DoomEffect(ulong source, uint action)
    {
        if (!IsDoomtrain || Active == null) return;
        if (action == 45624 && DoomEnabled(DoomtrainComponent.Unlimited)) ++doomUnlimitedHits;
        if (action == 45638 && DoomEnabled(DoomtrainComponent.Runaway)) ++doomRunawayHits;
        if (action is >= 45619 and <= 45622 && DoomEnabled(DoomtrainComponent.Levin))
            doomLevin.RemoveAll(c => c.Actor.Actor.Id == source);
        if (action == 45661 && DoomEnabled(DoomtrainComponent.TankBait) && doomTankBaits.Count > 0) doomTankBaits.RemoveAt(0);
        if (action == 45640 && DoomEnabled(DoomtrainComponent.Ray)) doomRays.Clear();
        DoomTick();
    }

    private void DoomIcon(ulong source, uint icon, ulong target)
    {
        if (!IsDoomtrain || Active == null) return;
        if (icon == 343 && DoomEnabled(DoomtrainComponent.TankBait))
        {
            if (!doomActors.TryGetValue(target, out var victim)) doomActors.TryGetValue(source, out victim);
            if (victim != null) doomTankBaits.Add((victim, _now.AddSeconds(5), ++doomKey));
        }
        if (icon == 642 && DoomEnabled(DoomtrainComponent.Ray)
            && doomActors.TryGetValue(source, out var caster) && doomActors.TryGetValue(target, out var player))
            doomRays.Add((caster, player, _now.AddSeconds(9.7), ++doomKey));
    }

    private void DoomStatus(ulong actor, uint status, bool present, ushort param, DateTime? expiry, ulong source)
    {
        if (!IsDoomtrain || Active == null || status != 4176) return;
        var next = (Present: present, Param: param, Expiry: expiry ?? default, Source: source);
        var previous = doomPendingStop.TryGetValue(actor, out var pending) ? pending : doomRawStop.GetValueOrDefault(actor);
        DoomStopTransition(actor, previous, next);
        doomPendingStop[actor] = next;
    }

    private void DoomStopTransition(ulong actor, (bool Present, ushort Param, DateTime Expiry, ulong Source) previous,
        (bool Present, ushort Param, DateTime Expiry, ulong Source) next)
    {
        if (!DoomEnabled(DoomtrainComponent.Ray) || !next.Present || !doomActors.TryGetValue(actor, out var reference)
            || previous.Present && previous.Param == next.Param && previous.Source == next.Source && (next.Expiry - previous.Expiry).TotalSeconds <= 1) return;
        for (var i = 0; i < doomRays.Count; ++i) doomRays[i] = doomRays[i] with { Source = reference };
    }
}
