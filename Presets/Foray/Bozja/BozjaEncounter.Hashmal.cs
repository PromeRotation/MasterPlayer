using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Replay;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed partial class BozjaEncounter
{
    internal List<Vector3> ControlTowers { get; } = [];
    internal List<BozjaExaflare> Exaflares { get; } = [];
    internal List<BozjaCast> Hammers { get; } = [];
    private readonly List<BozjaAoe> exaflarePublished = [];
    private int exaflareVersion;
    private int exaflareLastVersion;
    private int exaflareLastCount;
    internal int ExaflareErrors { get; private set; }
    internal int MissingExaflarePosition { get; private set; }

    private void ClearHashmal()
    {
        ControlTowers.Clear();
        Exaflares.Clear();
        Hammers.Clear();
        NumPhantomLinesFinished = 0;
        exaflarePublished.Clear();
        exaflareVersion = exaflareLastVersion = exaflareLastCount = ExaflareErrors = MissingExaflarePosition = 0;
    }

    private void HashmalCreated(BozjaActor actor)
    {
        if (!Active || !actor.NativeNpc || actor.Actor.Oid != 0x1EB17F
            || Definition.Rules.FirstOrDefault(r => r.Kind == BozjaRuleKind.Hammer) is not { } rule) return;
        Hammers.Add(new(++serial, rule, actor, DungeonEncounter.QuantizedDungeonPosition(actor.Actor.Position),
            0, Now.AddSeconds(12.6d)));
    }

    private void StartHashmal(BozjaActor actor, BozjaRule rule)
    {
        var spell = actor.Cast!;
        if (rule.Kind == BozjaRuleKind.ControlTower && spell.ActionId == 23830
            && !Casts.Any(c => c.Rule == rule && c.Origin.X == spell.Location.X && c.Origin.Z == spell.Location.Z))
            Casts.Add(new(++serial, rule, actor, spell.Location, 0, Activation(actor)));
        if (rule.Kind == BozjaRuleKind.Exaflare && spell.ActionId is 23819 or 23972)
        {
            var (sin, cos) = Math.SinCos(spell.Rotation ?? 0);
            var cloud = spell.ActionId == 23972;
            var distance = cloud ? 5 : 8;
            Exaflares.Add(new(new(++serial, rule, actor, actor.Actor.Position, 0, Activation(actor)),
                new(distance * (float)sin, 0, distance * (float)cos), cloud ? 4 : 8, cloud ? 2 : 4, cloud ? 1.1d : .8d));
        }
    }

    private void FinishHashmal(BozjaActor actor, BozjaRule rule, ReplayCast spell)
    {
        if (rule.Kind == BozjaRuleKind.Hammer)
        {
            if (Hammers.Count > 0) Hammers.RemoveAt(0);
            return;
        }
        if (rule.Kind != BozjaRuleKind.ControlTower) return;
        if (spell.ActionId == 23830)
        {
            var index = Casts.FindIndex(c => c.Rule == rule
                && c.Origin.X == spell.Location.X && c.Origin.Z == spell.Location.Z);
            if (index >= 0) Casts.RemoveAt(index);
            if (ControlTowers.Any(p => MathF.Abs(p.X - spell.Location.X) <= .1f
                && MathF.Abs(p.Z - spell.Location.Z) <= .1f)) return;
            ControlTowers.Add(actor.Actor.Position);
        }
        else if (spell.ActionId == 23818)
        {
            Casts.RemoveAll(c => c.Rule == rule);
            var index = ControlTowers.FindIndex(p => p.X == actor.Actor.Position.X && p.Z == actor.Actor.Position.Z);
            if (index < 0) return;
            ControlTowers.RemoveAt(index);
        }
        else return;
        Arena = ControlTowers.Count == 0 ? Definition.Arena : new BozjaControlArena(Definition.Center, ControlTowers);
        Center = Definition.Center;
    }

    private void HashmalEffect(BozjaActor source, uint action, Vector3? position)
    {
        if (!Definition.Rules.Any(r => r.Kind == BozjaRuleKind.Exaflare && r.Actions.Contains(action))) return;
        var point = action == 23820 ? position : source.Actor.Position;
        if (point == null)
        {
            ++MissingExaflarePosition;
            return;
        }
        var index = Exaflares.FindIndex(l => AlmostEqual(l.Cast.Origin, point.Value));
        if (index < 0)
        {
            ++ExaflareErrors;
            return;
        }
        var line = Exaflares[index];
        line.Cast = line.Cast with { Origin = point.Value + line.Advance, Activation = Now.AddSeconds(line.Interval) };
        if (--line.Left == 0) Exaflares.RemoveAt(index);
        ++exaflareVersion;
    }

    private void UpdateHashmal()
    {
        if (IsSavagePhantom)
        {
            UpdateSavagePhantomExaflares();
            return;
        }
        if (exaflareLastCount == Exaflares.Count && exaflareLastVersion == exaflareVersion) return;
        exaflareLastCount = Exaflares.Count;
        exaflareLastVersion = exaflareVersion;
        exaflarePublished.Clear();
        PublishExaflares(Exaflares, exaflarePublished);
    }

    private void PublishExaflares(IReadOnlyList<BozjaExaflare> lines, List<BozjaAoe> output)
    {
        foreach (var line in lines)
        {
            var point = line.Cast.Origin;
            var time = line.Cast.Activation > Now ? line.Cast.Activation : Now;
            for (var j = 1; j < Math.Min(line.Left, line.MaxShown); ++j)
            {
                point += line.Advance;
                time = time.AddSeconds(line.Interval);
                output.Add(new(line.Cast with
                {
                    Generation = ++serial, Origin = DungeonEncounter.QuantizedDungeonPosition(point), Activation = time,
                }, false, true));
            }
        }
        foreach (var line in lines)
            output.Add(new(line.Cast with
            {
                Origin = DungeonEncounter.QuantizedDungeonPosition(line.Cast.Origin),
            }, true, true));
    }

    private IEnumerable<BozjaAoe> HashmalAoes(BozjaRule rule)
    {
        if (rule.Kind == BozjaRuleKind.ControlTower)
            foreach (var cast in Casts.Where(c => c.Rule == rule)) yield return new(cast, false, true);
        if (rule.Kind is BozjaRuleKind.Exaflare or BozjaRuleKind.PhantomExaflare)
            foreach (var aoe in IsSavagePhantom
                ? phantomPublished.GetValueOrDefault(rule).Aoes ?? []
                : exaflarePublished.Where(a => a.Cast.Rule == rule)) yield return aoe;
        if (rule.Kind == BozjaRuleKind.Hammer)
            for (var i = 0; i < Math.Min(Hammers.Count, 2); ++i)
                yield return new(Hammers[i], i == 0 && Hammers.Count > 1, true);
    }
}
