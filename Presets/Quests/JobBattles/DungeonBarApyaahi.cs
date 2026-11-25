using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private List<AlphaActorState>? barP3Mycotenders;
    private readonly Dictionary<ulong, DungeonPendingCast> barP3Started = [];
    private readonly List<(long Id, DungeonPendingCast Cast, bool Risky, bool Danger)> barP3Power = [];
    private readonly List<DungeonPendingCast> barP3Knockbacks = [];
    private readonly List<(long Id, ulong Source, Vector3 Next, Vector3 Advance, DateTime Activation, int Remaining)> barP3Bursts = [];
    private readonly Dictionary<ulong, long> barP3BurstCasts = [];
    private readonly List<DungeonAmbientAoe> barP3BurstAoes = [];
    private readonly List<DungeonPendingCast> barP3Raidwides = [];
    private readonly List<DungeonPendingCast> barP3Enrages = [];
    private readonly Dictionary<ulong, (float Total, float Elapsed, DateTime At, bool Raw)> barP3Clocks = [];
    private DungeonAmbientAoe? barP3ArenaRing;
    private bool barP3BurstsDirty;
    private long barP3Generation;
    internal bool BarP3SmallArena { get; private set; }
    internal IReadOnlyList<(long Id, DungeonPendingCast Cast, bool Risky, bool Danger)> BarP3Power => barP3Power;
    internal IReadOnlyList<DungeonPendingCast> BarP3Knockbacks => barP3Knockbacks;
    internal IReadOnlyList<(long Id, ulong Source, Vector3 Next, Vector3 Advance, DateTime Activation, int Remaining)> BarP3Bursts => barP3Bursts;
    private static readonly Vector4 BarP3DangerColor = new(1, 1, 0, 1);

    private bool BarP3Pull()
    {
        barP3Mycotenders ??= alphaActors.Values.Where(a => a.Actor.Oid == 0x4670).ToList();
        return barP3Mycotenders.Count != 0 && barP3Mycotenders[0].Actor.InCombat;
    }

    private void BarP3Cast(DungeonPendingCast cast, float? totalTime, bool raw, DateTime observedStart)
    {
        if (!BarPassageStageStarted(0x466A) || !Definitions.First(d => d.BossOid == 0x466A).Casts.Any(r => r.Action == cast.Rule.Action)) return;
        var correction = barP3Started.TryGetValue(cast.Source, out var previous)
            && previous.Rule.Action == cast.Rule.Action && previous.StartedAt == cast.StartedAt;
        if (!BarP3SmallArena && cast.Rule.Action is 40914 or 40920)
            barP3ArenaRing = new("bar-p3/arena", "Arena change", new DonutTemplate(20, 25),
                new(0, cast.Location.Y, 379.27f),
                Activation: cast.FinishAt.AddSeconds(cast.Rule.Action == 40914 ? .5 : 2));
        if (cast.Rule.Action == 40910)
        {
            var index = correction ? barP3Power.FindIndex(p => ReferenceEquals(p.Cast, previous)) : -1;
            if (!correction || index >= 0)
            {
                var power = (index < 0 ? ++barP3Generation : barP3Power[index].Id, cast,
                    index >= 0 && barP3Power[index].Risky, index >= 0 && barP3Power[index].Danger);
                if (index < 0) barP3Power.Add(power);
                else barP3Power[index] = power;
            }
        }
        if (cast.Rule.Action == 40909)
        {
            var index = correction ? barP3Knockbacks.FindIndex(k => ReferenceEquals(k, previous)) : -1;
            if (index >= 0) barP3Knockbacks[index] = cast;
            else if (!correction) barP3Knockbacks.Add(cast);
        }
        if (cast.Rule.Action == 40917)
        {
            var index = barP3BurstCasts.TryGetValue(cast.Source, out var id)
                ? barP3Bursts.FindIndex(l => l.Id == id && l.Remaining == 9) : -1;
            if (!correction || index >= 0)
            {
                var (sin, cos) = Math.SinCos(cast.Rotation);
                var line = (index < 0 ? ++barP3Generation : barP3Bursts[index].Id, cast.Source,
                    cast.SourcePosition ?? _actors[cast.Source].Position,
                    new Vector3((float)sin, 0, (float)cos) * 5, cast.FinishAt, 9);
                if (index < 0) barP3Bursts.Add(line);
                else barP3Bursts[index] = line;
                barP3BurstCasts[cast.Source] = line.Item1;
                barP3BurstsDirty = true;
            }
        }
        var hints = cast.Rule.Action switch
        {
            40927 => barP3Enrages,
            40922 or 40916 => barP3Raidwides,
            _ => null,
        };
        if (hints != null)
        {
            var index = correction ? hints.FindIndex(c => ReferenceEquals(c, previous)) : -1;
            if (index >= 0) hints[index] = cast;
            else if (!correction) hints.Add(cast);
            if (!correction || !barP3Clocks.TryGetValue(cast.Source, out var clock) || raw && !clock.Raw)
            {
                var total = Math.Max(.05f, totalTime ?? (float)(cast.FinishAt - cast.StartedAt).TotalSeconds - .3f);
                barP3Clocks[cast.Source] = (total, Math.Clamp((float)(_now - observedStart).TotalSeconds, 0, total), _now, raw);
            }
        }
        barP3Started[cast.Source] = cast;
    }

    private void BarP3Finish(DungeonPendingCast cast)
    {
        if (!BarPassageStageStarted(0x466A)) return;
        var action = cast.Rule.Action;
        if (action == 40910 && barP3Power.Count > 0) barP3Power.RemoveAt(0);
        if (action == 40909)
        {
            var index = barP3Knockbacks.FindIndex(c => c.Source == cast.Source);
            if (index >= 0) barP3Knockbacks.RemoveAt(index);
        }
        if (action is 40917 or 40918)
        {
            var index = barP3Bursts.FindIndex(l => MathF.Abs(l.Next.X - cast.Location.X) <= 1
                && MathF.Abs(l.Next.Z - cast.Location.Z) <= 1);
            if (index >= 0)
            {
                var line = barP3Bursts[index];
                if (line.Remaining == 1) barP3Bursts.RemoveAt(index);
                else barP3Bursts[index] = line with
                {
                    Next = cast.Location + line.Advance,
                    Activation = _now.AddSeconds(1.2),
                    Remaining = line.Remaining - 1,
                };
                barP3BurstsDirty = true;
            }
        }
        if (action == 40917) barP3BurstCasts.Remove(cast.Source);
        var hints = action switch
        {
            40927 => barP3Enrages,
            40922 or 40916 => barP3Raidwides,
            _ => null,
        };
        if (hints != null)
        {
            var index = hints.FindIndex(c => c.Source == cast.Source);
            if (index >= 0) hints.RemoveAt(index);
        }
        barP3Started.Remove(cast.Source);
        barP3Clocks.Remove(cast.Source);
    }

    private void BarP3Map(uint index, uint state)
    {
        if (!BarPassageStageStarted(0x466A) || index != 6) return;
        if (state == 0x00020001)
        {
            BarP3SmallArena = true;
            barP3ArenaRing = null;
        }
        else if (state == 0x00008004)
            BarP3SmallArena = false;
        if (IsBarPassageP3)
            Arena = JobQuestCatalog.BarPassageArena(barPassagePrimary?.Position.Y ?? 0, BarP3SmallArena);
    }

    private void BarP3Update()
    {
        BarP3AdvanceClocks(_now);
        if (!barP3BurstsDirty) return;
        barP3BurstsDirty = false;
        barP3BurstAoes.Clear();
        // Source caches all futures, then all heads, only when the line count/version changes.
        foreach (var line in barP3Bursts)
        {
            var at = line.Activation > _now ? line.Activation : _now;
            var position = line.Next;
            for (var i = 1; i < Math.Min(line.Remaining, 4); ++i)
            {
                position += line.Advance;
                at = at.AddSeconds(1.2);
                barP3BurstAoes.Add(new($"bar-p3/burst/{line.Id}/{i}", "Burst", new CircleTemplate(6),
                    QuantizedDungeonPosition(position), Activation: at));
            }
        }
        foreach (var line in barP3Bursts)
            barP3BurstAoes.Add(new($"bar-p3/burst/{line.Id}/0", "Burst", new CircleTemplate(6),
                QuantizedDungeonPosition(line.Next), Activation: line.Activation, Color: BarP3DangerColor));
    }

    private void BarP3AdvanceClocks(DateTime now)
    {
        foreach (var (source, clock) in barP3Clocks.ToArray())
        {
            if (now <= clock.At) continue;
            // These three source actions have ExtraCastTime100ms=0 in the matching client.
            // Preserve float frame accumulation and saturation while CastInfo remains present.
            barP3Clocks[source] = clock with
            {
                Elapsed = Math.Min(clock.Elapsed + (float)(now - clock.At).TotalSeconds, clock.Total),
                At = now,
            };
        }
    }

    private float BarP3Remaining(ulong source) => barP3Clocks.TryGetValue(source, out var clock)
        ? clock.Total + .3f - clock.Elapsed : 0;

    internal Vector3 BarP3Landing(DungeonActor player) => BarP3Landings(player).LastOrDefault(player.Position);

    private IEnumerable<Vector3> BarP3Landings(DungeonActor player)
    {
        var from = player.Position;
        foreach (var cast in barP3Knockbacks)
        {
            if (WhorlImmune(player, cast.FinishAt)) continue;
            var direction = from - cast.Location;
            direction.Y = 0;
            if (direction.LengthSquared() == 0) continue;
            from += Vector3.Normalize(direction) * 13;
            yield return from;
        }
    }

    private IEnumerable<DungeonAmbientAoe> BarP3Aoes()
    {
        if (!IsBarPassageP3) yield break;
        if (barP3ArenaRing != null) yield return barP3ArenaRing;
        BarP3Update();
        foreach (var aoe in barP3BurstAoes) yield return aoe;
        if (barP3Power.Count > 0)
        {
            var first = barP3Power[0].Cast.FinishAt.AddSeconds(1);
            var last = barP3Power[^1].Cast.FinishAt.AddSeconds(-1);
            for (var i = 0; i < barP3Power.Count; ++i)
            {
                var p = barP3Power[i];
                // ActiveAOEs mutates qualifying entries; later prefix changes never reset others.
                if (p.Cast.FinishAt < first)
                    barP3Power[i] = p = p with { Risky = true, Danger = p.Cast.FinishAt < last };
                yield return new($"bar-p3/power/{p.Id}", "Pathogenic Power", new CircleTemplate(6),
                    p.Cast.Location, Navigate: p.Risky, Activation: p.Cast.FinishAt, Color: p.Danger ? BarP3DangerColor : null);
            }
        }
        if (barP3Knockbacks.Count > 0)
            yield return new("bar-p3/knockback/start", "Pathogenic Power / 站位", new CircleTemplate(6),
                barP3Knockbacks[0].Location, Safe: true, Display: false,
                Activation: barP3Knockbacks[0].FinishAt, GoalPriority: 1);
        if (_actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        var from = player.Position;
        foreach (var cast in barP3Knockbacks)
        {
            if (WhorlImmune(player, cast.FinishAt)) continue;
            var direction = from - cast.Location;
            direction.Y = 0;
            if (direction.LengthSquared() == 0) continue;
            var to = from + Vector3.Normalize(direction) * 13;
            foreach (var aoe in QuestKnockbackProjection($"bar-p3/knockback/{cast.Source}", player, from, to)) yield return aoe;
            from = to;
        }
    }

    private IEnumerable<DungeonDamagePrediction> BarP3RaidwideDamage()
    {
        if (!IsBarPassageP3) yield break;
        foreach (var cast in barP3Raidwides)
            foreach (var actor in _actors.Values.Where(a => !a.Dead && a.PartySlot >= 0))
                yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name,
                    _now.AddSeconds(BarP3Remaining(cast.Source)), "Raidwide");
    }

    private IEnumerable<string> BarP3Hints(Vector3? position)
    {
        if (!IsBarPassageP3) yield break;
        if (barP3Raidwides.Count > 0) yield return "Raidwide";
        if (barP3Enrages.Count > 0)
        {
            yield return $"Enrage! {BarP3Remaining(barP3Enrages[0].Source):f1}s left";
        }
        if (_actors.GetValueOrDefault(PlayerId) is { } player && BarP3Landings(player).Any(p => Arena?.Contains(p) == false))
            yield return "Knockback into danger!";
        if (position is not { } p) yield break;
        var aoes = BarP3Aoes().Where(a => a.Navigate && !a.Safe && a.Display).ToArray();
        if (aoes.Any(a => a.Id.StartsWith("bar-p3/burst/") && a.Shape.Resolve(a.Position, a.Rotation, null).Contains(p)))
            yield return "GTFO from exaflare!";
        if (aoes.Any(a => !a.Id.StartsWith("bar-p3/burst/") && a.Shape.Resolve(a.Position, a.Rotation, null).Contains(p))
            || _casts.Values.Any(c => BarPassageCastVisible(c) && c.Rule.Shape?.Resolve(c.Location, c.Rotation, null).Contains(p) == true))
            yield return "GTFO from aoe!";
    }

    private void ResetBarP3()
    {
        barP3Started.Clear();
        barP3Power.Clear();
        barP3Knockbacks.Clear();
        barP3Bursts.Clear();
        barP3BurstCasts.Clear();
        barP3BurstAoes.Clear();
        barP3Raidwides.Clear();
        barP3Enrages.Clear();
        barP3Clocks.Clear();
        barP3ArenaRing = null;
        barP3BurstsDirty = false;
        BarP3SmallArena = false;
        jobQuestStacks.Clear();
        jobQuestSpreads.Clear();
        jobQuestPartyStarted.Clear();
    }
}
