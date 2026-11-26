using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Hydatos;

internal sealed partial class HydatosEncounter(HydatosDefinition definition)
{
    internal HydatosDefinition Definition { get; } = definition;
    internal Dictionary<ulong, HydatosActor> Actors { get; } = [];
    internal List<HydatosActor> History { get; } = [];
    internal List<HydatosAoe> Aoes { get; } = [];
    internal HydatosActor? Primary { get; private set; }
    internal bool Active { get; private set; }
    internal bool Completed { get; private set; }
    internal DateTime Now { get; private set; }
    internal List<(HydatosRule Rule, HydatosActor Actor)> DispelTargets { get; } = [];
    internal ulong SourcePlayerId { get; private set; }
    internal Vector3 ArenaCenter { get; private set; }
    internal Vector3 NavigationCenter { get; private set; }
    internal string? InputDiagnostic { get; private set; }
    internal long LoadVersion { get; private set; }
    internal (int Phase, int Order) LoadOrder { get; private set; }
    internal ReplayPartyMember[] Party { get; private set; } = [];
    private readonly DateTime[,] immunity = new DateTime[64, 3];
    private long serial;
    private bool dualCast;
    private int ionCasts;
    private int akhCasts;
    private int stackCasts;
    private readonly List<HydatosActor> pending = [];
    private ulong previousSourcePlayerId;

    internal void Advance(DateTime now)
    {
        Now = now;
        foreach (var actor in Actors.Values)
        {
            if (actor.Cast != null && now > actor.Clock)
                actor.Elapsed = MathF.Min(actor.Elapsed + (float)(now - actor.Clock).TotalSeconds, AdjustedTotal(actor.Cast));
            actor.Clock = now;
        }
    }

    internal void Synchronize(IEnumerable<ReplayActorState> observations, DateTime now, ulong sourcePlayerId = 0,
        ReplayPartyMember[]? party = null)
    {
        Advance(now);
        var values = observations.Select(a => a.TargetId == 0xE0000000 || a.Cast?.TargetId == 0xE0000000 ? a with
        {
            TargetId = a.TargetId == 0xE0000000 ? 0 : a.TargetId,
            Cast = a.Cast is { TargetId: 0xE0000000 } cast ? cast with { TargetId = 0 } : a.Cast,
        } : a).ToArray();
        SourcePlayerId = sourcePlayerId != 0 ? sourcePlayerId : values.FirstOrDefault(a => a.Kind == 1)?.Id ?? 0;
        var promotePending = Primary == null || Completed || Primary.Destroyed;
        var sourceSlotCommitted = SourcePlayerId != 0 && previousSourcePlayerId == SourcePlayerId;
        var sourceSlotKnown = sourceSlotCommitted && Actors.ContainsKey(SourcePlayerId);
        previousSourcePlayerId = SourcePlayerId;
        var present = values.ToDictionary(a => a.Id);
        foreach (var old in Actors.Values.Where(a => !present.TryGetValue(a.Actor.Id, out var next)
            || NewActor(a.Actor, next)).ToArray())
        {
            if (old.Cast is { } spell) Finish(old, spell);
            if (Active && !old.IsPet)
                foreach (var status in old.Statuses.OfType<ReplayStatus>()) Lose(old, status);
            old.Cast = null;
            old.Destroyed = true;
            Actors.Remove(old.Actor.Id);
            if (old.Actor.Id == SourcePlayerId) sourceSlotKnown = false;
        }
        foreach (var value in values)
        {
            if (!Actors.TryGetValue(value.Id, out var shell))
            {
                shell = new(value);
                Actors.Add(value.Id, shell);
                History.Add(shell);
                if (value.Oid == Definition.PrimaryOid)
                {
                    if (Primary is { Destroyed: true } previous && !Completed && previous.Actor.Id == value.Id)
                        Primary = shell;
                    else if (promotePending && sourceSlotKnown
                        && Actors.GetValueOrDefault(SourcePlayerId) is { Destroyed: false } loadObserver
                        && Vector3.DistanceSquared(loadObserver.Actor.Position, value.Position) <= 500 * 500)
                    {
                        Load(shell, 0);
                        promotePending = false;
                    }
                    else pending.Add(shell);
                }
                if (Active && shell.NativeNpc) Created(shell);
            }
            shell.Actor = value;
            if (value.Id == SourcePlayerId) sourceSlotKnown = sourceSlotCommitted;
            var cast = shell.NativeNpc && value.Cast?.IsSpell == true ? value.Cast : null;
            if (!SameCast(shell.Cast, cast))
            {
                if (shell.Cast is { } previous) Finish(shell, previous);
                shell.Cast = cast;
                if (cast != null)
                {
                    shell.Elapsed = Math.Clamp((float)(now - cast.StartedAt).TotalSeconds, 0, AdjustedTotal(cast));
                    shell.Clock = now;
                    if (Active) Start(shell);
                }
            }
            ObserveStatuses(shell);
        }
        pending.RemoveAll(a => a.Destroyed || a.Actor.Dead);
        if (promotePending && Actors.GetValueOrDefault(SourcePlayerId) is { } observer)
            for (var i = pending.Count - 1; i >= 0; --i)
            {
                var candidate = pending[i];
                if (Vector3.DistanceSquared(observer.Actor.Position, candidate.Actor.Position) > 500 * 500) continue;
                Load(candidate, 1);
                pending.RemoveAt(i);
                break;
            }
        // Feed commits Party only after every Actor/status callback. Cold
        // activation, which follows that commit, uses the new roster.
        Party = party ?? [new(0, "", SourcePlayerId)];
        if (Primary is not { } primary) return;
        var sourcePlayer = Actors.GetValueOrDefault(SourcePlayerId)?.Actor;
        if (sourcePlayer == null) return;
        ArenaCenter = sourcePlayer?.Position ?? default;
        if (MathF.Abs(ArenaCenter.X - NavigationCenter.X) > 5 || MathF.Abs(ArenaCenter.Z - NavigationCenter.Z) > 5)
            NavigationCenter = new(MathF.Round(ArenaCenter.X), 0, MathF.Round(ArenaCenter.Z));
        if (Active && PhaseComplete())
        {
            Reset();
            Completed = true;
        }
        if (primary.Destroyed || Completed) return;
        if (!Active && sourcePlayer != null && Vector3.DistanceSquared(sourcePlayer.Position, primary.Actor.Position) > 500 * 500)
        {
            ReturnToPending(primary);
            return;
        }
        if (!Active && primary.Actor.Targetable && primary.Actor.InCombat)
        {
            Active = true;
            foreach (var rule in Definition.Rules)
            {
                if (rule.Kind == HydatosRuleKind.DualCast)
                {
                    Aoes.RemoveAll(a => a.Rule.Kind == HydatosRuleKind.DualCast);
                    dualCast = false;
                }
                foreach (var actor in Actors.Values)
                {
                    if (actor.NativeNpc) Created(actor, rule);
                    if (actor.Cast != null) Start(actor, rule);
                    foreach (var status in actor.Statuses.OfType<ReplayStatus>()) Gain(actor, status, rule);
                }
            }
        }
        if (Active && PhaseComplete())
        {
            Reset();
            Completed = true;
        }
        // SimpleBossModule.CheckReset is evaluated after the state machine update.
        if (Active && !primary.Actor.InCombat)
        {
            Reset();
            if (sourcePlayer != null && Vector3.DistanceSquared(sourcePlayer.Position, primary.Actor.Position) > 500 * 500)
                ReturnToPending(primary);
            else
            {
                ++LoadVersion;
                LoadOrder = (2, History.IndexOf(primary));
            }
        }
    }

    private void Load(HydatosActor primary, int phase)
    {
        Reset();
        Primary = primary;
        Completed = false;
        ++LoadVersion;
        var index = History.IndexOf(primary);
        LoadOrder = (phase, phase == 1 ? -index : index);
    }

    private void ReturnToPending(HydatosActor primary)
    {
        Reset();
        pending.Add(primary);
        Primary = null;
    }

    private bool PhaseComplete()
        => Definition.Name == "Phuabo"
            ? History.Where(a => a.Actor.Oid == 0x2741).All(a => a.Destroyed)
                || Actors.Values.Any(a => a.Actor.Oid == 0x2740 && a.Actor.Targetable)
            : Primary is { } p && (p.Destroyed || p.Actor.Dead || p.Actor.Hp == 0);

    private void Created(HydatosActor actor, HydatosRule? only = null)
    {
        if (actor.Actor.Oid != 0x2768) return;
        foreach (var rule in Definition.Rules.Where(r => r.Kind == HydatosRuleKind.Charybdis
            && (only == null || ReferenceEquals(r, only))))
            Aoes.Add(new(++serial, rule, actor, rule.Shape, Quantized(actor.Actor.Position),
                0, Now.AddSeconds(4d), Now, Remaining: 19));
    }

    private void Start(HydatosActor source, HydatosRule? only = null)
    {
        if (source.Cast is not { } spell) return;
        // Duplicate DualCast activation has already replaced the prior instance.
        var rules = only != null ? [only] : Definition.Rules.DistinctBy(r => r.Component).ToArray();
        foreach (var rule in rules)
        {
            if (!rule.Actions.Contains(spell.ActionId)) continue;
            var origin = spell.Location;
            var rotation = spell.Rotation ?? source.Actor.Rotation;
            var shape = rule.Shape;
            var activation = Activation(source);
            HydatosActor? target = null;
            var minDistance = 0f;
            switch (rule.Kind)
            {
                case HydatosRuleKind.IconStack:
                case HydatosRuleKind.IonShower:
                case HydatosRuleKind.Dispel:
                case HydatosRuleKind.Charybdis:
                case HydatosRuleKind.HotTailSecond:
                case HydatosRuleKind.Touchdown:
                    continue;
                case HydatosRuleKind.DualCast:
                    if (spell.ActionId == 15909) { dualCast = true; continue; }
                    var first = spell.ActionId == 15914;
                    GeometryTemplate Shape(bool donut) => donut ? new DonutTemplate(5, 40) : new CircleTemplate(10);
                    Aoes.Add(new(++serial, rule, source, Shape(first), origin, 0, activation, spell.StartedAt));
                    if (dualCast)
                    {
                        Aoes.Add(new(++serial, rule, source, Shape(!first), origin, 0,
                            Activation(source, 3.1f), spell.StartedAt, Risky: false));
                        dualCast = false;
                    }
                    continue;
                case HydatosRuleKind.IceAndLevin:
                    Aoes.RemoveAll(a => a.Rule.Kind == rule.Kind);
                    rotation = 0;
                    activation = Activation(source, 1d);
                    break;
                case HydatosRuleKind.Petrattraction:
                    Aoes.RemoveAll(a => a.Rule.Kind == rule.Kind);
                    origin = source.Actor.Position;
                    rotation = 0;
                    activation = Activation(source, 1.4d);
                    break;
                case HydatosRuleKind.Charge:
                    var delta = Xz(spell.Location - source.Actor.Position);
                    shape = RayRectangleTemplate.Symmetric(rule.Range * 2, delta.Length());
                    origin = Quantized(source.Actor.Position);
                    rotation = MathF.Atan2(delta.X, delta.Y);
                    break;
                case HydatosRuleKind.Pull:
                    minDistance = source.Actor.HitboxRadius
                        + (Actors.GetValueOrDefault(SourcePlayerId)?.Actor.HitboxRadius ?? 0);
                    break;
                case HydatosRuleKind.Bait:
                case HydatosRuleKind.AkhMorn:
                    target = Actors.GetValueOrDefault(spell.TargetId);
                    if (target == null) continue;
                    if (rule.Kind == HydatosRuleKind.AkhMorn) akhCasts = 3;
                    break;
            }
            Aoes.Add(new(++serial, rule, source, shape, origin, rotation, activation, spell.StartedAt, target,
                MinDistance: minDistance));
            if (rule.Actions.Length < 2) continue;
            var group = Aoes.Where(a => ReferenceEquals(a.Rule, rule)).ToList();
            if (rule.Kind == HydatosRuleKind.Gaze)
            {
                if (group.Count != 99) continue;
                group.Sort((a, b) => a.Activation.CompareTo(b.Activation));
            }
            else
            {
                if (group.Count < 99) continue;
                DungeonAoeOrder.Sort(group, a => a.Activation);
            }
            var index = 0;
            for (var i = 0; i < Aoes.Count; ++i)
                if (ReferenceEquals(Aoes[i].Rule, rule)) Aoes[i] = group[index++];
        }
    }

    internal void Finish(HydatosActor source, ReplayCast spell)
    {
        foreach (var rule in Definition.Rules.DistinctBy(r => r.Component))
        {
            if (rule.Kind is not (HydatosRuleKind.Aoe or HydatosRuleKind.Gaze or HydatosRuleKind.Raidwide
                or HydatosRuleKind.SingleTarget or HydatosRuleKind.Charge or HydatosRuleKind.Pull or HydatosRuleKind.Bait))
                continue;
            if (rule.Actions.Length == 1 && !rule.Actions.Contains(spell.ActionId)) continue;
            var index = Aoes.FindIndex(a => ReferenceEquals(a.Rule, rule) && a.Source.Actor.Id == source.Actor.Id);
            if (index >= 0) Aoes.RemoveAt(index);
        }
    }

    internal void Icon(ulong source, uint icon, ulong target)
    {
        if (!Active) return;
        var kind = Definition.Name == "Daphne" && icon == 62 ? HydatosRuleKind.IconStack
            : Definition.Name == "Ovni" && icon == 111 ? HydatosRuleKind.IonShower : (HydatosRuleKind?)null;
        var rule = Definition.Rules.FirstOrDefault(r => r.Kind == kind);
        // StackWithIcon consumes the actor receiving the icon; IonShower resolves targetID.
        var actor = Actors.GetValueOrDefault(kind == HydatosRuleKind.IonShower ? target : source);
        if (rule == null || actor == null) return;
        if (kind == HydatosRuleKind.IonShower) ionCasts = 0;
        var delay = kind == HydatosRuleKind.IconStack ? 4.9f : 5d;
        Aoes.Add(new(++serial, rule, actor, rule.Shape, default, 0, Now.AddSeconds(delay), Now, actor));
    }

    internal void Effect(ulong sourceId, uint action, float? rotation, ulong mainTarget = 0)
    {
        if (!Active || !Actors.TryGetValue(sourceId, out var source) || !source.NativeNpc) return;
        if (action == 15476) Aoes.RemoveAll(a => a.Rule.Kind == HydatosRuleKind.Petrattraction);
        if (action == 15033 && ++stackCasts == 1)
        {
            var first = Aoes.FindIndex(a => a.Rule.Kind == HydatosRuleKind.IconStack && a.Target?.Actor.Id == mainTarget);
            if (first < 0) first = Aoes.FindIndex(a => a.Rule.Kind == HydatosRuleKind.IconStack);
            if (first >= 0) { Aoes.RemoveAt(first); stackCasts = 0; }
        }
        if (action == 14788 && ++ionCasts >= 3) Aoes.RemoveAll(a => a.Rule.Kind == HydatosRuleKind.IonShower);
        if (action == 15006) Aoes.RemoveAll(a => a.Rule.Kind == HydatosRuleKind.IceAndLevin);
        if (action == 15007)
        {
            Aoes.RemoveAll(a => a.Rule.Kind == HydatosRuleKind.HotTailSecond);
            InputDiagnostic = null;
        }
        if (action == 14993) Aoes.RemoveAll(a => a.Rule.Kind == HydatosRuleKind.Touchdown);
        if (action == 14995 && --akhCasts == 0) Aoes.RemoveAll(a => a.Rule.Kind == HydatosRuleKind.AkhMorn);
        if (action is 15914 or 15915 or 15912 or 15913)
        {
            var first = Aoes.FindIndex(a => a.Rule.Kind == HydatosRuleKind.DualCast);
            if (first >= 0) Aoes.RemoveAt(first);
        }
        if (action == 15002)
        {
            var first = Aoes.FindIndex(a => a.Rule.Kind == HydatosRuleKind.Charybdis && a.Source.Actor.Id == sourceId);
            if (first >= 0)
            {
                var next = Aoes[first] with { Remaining = Aoes[first].Remaining - 1 };
                if (next.Remaining == 0) Aoes.RemoveAt(first);
                else Aoes[first] = next;
            }
        }
        var touchdown = Definition.Rules.FirstOrDefault(r => r.Kind == HydatosRuleKind.Touchdown);
        if (action == 14992 && touchdown != null && !Aoes.Any(a => a.Rule == touchdown))
            Aoes.Add(new(++serial, touchdown, source, null, default, 0, Now.AddSeconds(3.1d), Now));
        var tail = Definition.Rules.FirstOrDefault(r => r.Kind == HydatosRuleKind.HotTailSecond);
        if (action == 14999 && tail != null)
        {
            Aoes.RemoveAll(a => a.Rule == tail);
            InputDiagnostic = rotation == null ? "录像缺少Hot Tail效果包朝向，未猜测第二段范围" : null;
            if (rotation is { } angle)
                Aoes.Add(new(++serial, tail, source, tail.Shape, source.Actor.Position, angle, Now.AddSeconds(3.1d), Now));
        }
    }

    internal IEnumerable<HydatosAoe> VisibleAoes()
    {
        var duals = Aoes.Where(a => a.Rule.Kind == HydatosRuleKind.DualCast).ToArray();
        for (var i = 0; i < duals.Length; ++i)
        {
            var index = Aoes.IndexOf(duals[i]);
            Aoes[index] = duals[i] with { Risky = i == 0, Danger = duals[i].Danger || i == 0 && duals.Length > 1 };
        }
        return Aoes;
    }

    internal DateTime Activation(HydatosActor source, double extraDelay = 0)
        => source.Cast is { } cast ? Now.AddSeconds((Total(cast) + .3f - source.Elapsed) + extraDelay) : default;

    internal void Reset()
    {
        Aoes.Clear();
        DispelTargets.Clear();
        Active = false;
        dualCast = false;
        ionCasts = akhCasts = stackCasts = 0;
        NavigationCenter = default;
        InputDiagnostic = null;
        Array.Clear(immunity);
    }

    internal void Clear()
    {
        Reset();
        Actors.Clear();
        History.Clear();
        pending.Clear();
        Primary = null;
        Completed = false;
        previousSourcePlayerId = 0;
        Party = [];
    }

    internal static Vector3 Quantized(Vector3 p) => DungeonEncounter.QuantizedDungeonPosition(p);
    internal static Vector2 Xz(Vector3 p) => new(p.X, p.Z);
    internal static bool NativeNpc(ReplayActorState a) => a.Kind != 1 && !(a.Kind == 2 && a.SubKind is 2 or 3 or 9);
    private static float Total(ReplayCast cast)
        => cast.TotalTime ?? (float)Math.Max(0, (cast.FinishAt - cast.StartedAt).TotalSeconds - .3d);
    private static float AdjustedTotal(ReplayCast cast) => Total(cast);
    internal static float DecodeRotation(ushort raw) => raw * (float)(1d / 65535 * 2 * Math.PI) - MathF.PI;

    private void ObserveStatuses(HydatosActor actor)
    {
        var next = new ReplayStatus?[60];
        for (var i = 0; i < actor.Actor.Statuses.Length; ++i)
        {
            var status = actor.Actor.Statuses[i];
            var slot = status.SlotIndex ?? i;
            if (slot is >= 0 and < 60 && status.Id != 0)
                next[slot] = status with
                {
                    SourceId = status.SourceId == 0xE0000000 ? 0 : status.SourceId,
                    ExpiresAt = status.ZeroRemainingTime == true ? Now
                        : status.ExpiresAt == DateTime.MaxValue ? Now.AddSeconds(100000)
                        : Now.AddSeconds(Math.Min(Math.Abs((status.ExpiresAt - Now).TotalSeconds), 100000)),
                };
        }
        for (var i = 0; i < 60; ++i)
        {
            var old = actor.Statuses[i];
            var value = next[i];
            actor.Statuses[i] = value;
            if (!Active || actor.IsPet) continue;
            var identityChanged = old?.Id != value?.Id || old?.SourceId != value?.SourceId;
            if (old != null && identityChanged) Lose(actor, old);
            if (value != null && (identityChanged || old?.Param != value.Param
                || old == null || (value.ExpiresAt - old.ExpiresAt).TotalSeconds > 1d)) Gain(actor, value);
        }
    }

    private void Gain(HydatosActor actor, ReplayStatus status, HydatosRule? only = null)
    {
        if ((only == null || only.Kind is HydatosRuleKind.Pull or HydatosRuleKind.Petrattraction)
            && Definition.Rules.Any(r => r.Kind is HydatosRuleKind.Pull or HydatosRuleKind.Petrattraction))
            SetImmune(actor, status, status.ExpiresAt);
        foreach (var rule in Definition.Rules.Where(r => r.Kind == HydatosRuleKind.Dispel
            && r.Actions.Contains(status.Id) && (only == null || ReferenceEquals(only, r))))
            DispelTargets.Add((rule, actor));
    }

    private void Lose(HydatosActor actor, ReplayStatus status)
    {
        SetImmune(actor, status, default);
        foreach (var rule in Definition.Rules.Where(r => r.Kind == HydatosRuleKind.Dispel && r.Actions.Contains(status.Id)))
        {
            var index = DispelTargets.FindIndex(p => ReferenceEquals(p.Rule, rule) && ReferenceEquals(p.Actor, actor));
            if (index >= 0) DispelTargets.RemoveAt(index);
        }
    }

    private void SetImmune(HydatosActor actor, ReplayStatus status, DateTime expires)
    {
        var kind = status.Id switch
        {
            3054 or 160 or 1209 => 0,
            1722 or 2663 => 1,
            2345 => 2,
            _ => -1,
        };
        var slot = Party.FirstOrDefault(p => p.EntityId == actor.Actor.Id)?.Slot ?? -1;
        if (kind >= 0 && slot is >= 0 and < 64) immunity[slot, kind] = expires;
    }

    internal bool ImmuneAt(int slot, DateTime activation)
        => slot is >= 0 and < 64 && (immunity[slot, 0] > activation
            || immunity[slot, 1] > activation || immunity[slot, 2] > activation);
    private static bool SameCast(ReplayCast? a, ReplayCast? b)
        => a == null ? b == null : b != null && a.ActionId == b.ActionId && a.TargetId == b.TargetId
            && a.Location == b.Location && a.Rotation == b.Rotation && Total(a) == Total(b);
    private static bool NewActor(ReplayActorState a, ReplayActorState b)
        => a.Oid != b.Oid || a.GameObjectId != 0 && b.GameObjectId != 0 && a.GameObjectId != b.GameObjectId
            || a.ObjectIndex != null && b.ObjectIndex != null && a.ObjectIndex != b.ObjectIndex;
}
