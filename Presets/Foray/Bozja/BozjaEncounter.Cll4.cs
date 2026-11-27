using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed partial class BozjaEncounter
{
    internal bool IsCll4 => Definition.PrimaryOid == 0x2EBD;
    internal bool Cll4DawonArena { get; private set; } = true;
    internal bool UsesDrawnArena => IsCll1 || IsCll4 || IsOwain || IsOzma || IsArachne;
    private bool cll4DawonWall;
    private bool cll4LyonWall;
    private bool cll4FirstPlumes = true;
    private bool cll4FirstObey = true;
    private readonly List<BozjaAoe> cll4Hammers = [];
    private readonly List<BozjaCast> cll4Obey = [];
    private List<BozjaActor>? cll4Verdant;
    private List<BozjaActor>? cll4Scarlet;
    internal float Cll4Radius
    {
        get
        {
            if (ReferenceEquals(Arena, BozjaCll4Catalog.DawonStarting)) return CastrumLacusArenas.DawonStartingRadius;
            if (ReferenceEquals(Arena, BozjaCll4Catalog.DawonDefault)) return CastrumLacusArenas.DawonDefaultRadius;
            if (ReferenceEquals(Arena, BozjaCll4Catalog.LyonStarting)) return CastrumLacusArenas.LyonStartingRadius;
            return CastrumLacusArenas.LyonDefaultRadius;
        }
    }

    private void ClearCll4()
    {
        Cll4DawonArena = cll4FirstPlumes = cll4FirstObey = true;
        cll4DawonWall = cll4LyonWall = false;
        cll4Hammers.Clear();
        cll4Obey.Clear();
        cll4Verdant = cll4Scarlet = null;
    }

    internal void ObserveCll4Drawing(BozjaActor viewer)
    {
        if (!IsCll4 || !Active) return;
        if (!Cll4DawonArena && BozjaCll4Catalog.DawonStarting.Contains(viewer.Actor.Position, 0))
        {
            Cll4DawonArena = true;
            Arena = BozjaCll4Catalog.DawonDefault;
            Center = new(80, 0, -813);
        }
        else if (Cll4DawonArena && BozjaCll4Catalog.LyonStarting.Contains(viewer.Actor.Position, 0))
        {
            Cll4DawonArena = false;
            Arena = cll4LyonWall ? BozjaCll4Catalog.LyonDefault : BozjaCll4Catalog.LyonStarting;
            Center = new(80, 0, -874);
        }
    }

    private bool Cll4OutputEnabled(BozjaRule rule) => !IsCll4 || (rule.Component switch
    {
        "Pentagust" or "FervidPulse" or "SwoopingFrenzy" or "Scratch" or "CrackleHiss"
            or "RipperClaw" or "SpikeFlail" or "LeftRightHammer" => Cll4DawonArena,
        "TasteOfBlood" or "NaturesBlood" or "NaturesPulse" or "TwinAgonies"
            or "TheKingsNotice" or "HeartOfNature" or "WindsPeak" or "WindsPeakKB" => !Cll4DawonArena,
        _ => true,
    });

    private void BeginCll4Rule(BozjaActor actor, BozjaRule rule)
    {
        Cll4Created(actor, rule);
        if (actor.Cast != null) Start(actor, rule);
        if (rule.Kind == BozjaRuleKind.Cll4Wind)
            foreach (var status in actor.Statuses.OfType<ReplayStatus>()) ImmuneStatus(actor, status, true);
    }

    private void Cll4Created(BozjaActor actor, BozjaRule? onlyRule = null)
    {
        if (!IsCll4) return;
        if (onlyRule == null)
        {
            if (actor.Actor.Oid == 0x2EBE) cll4Verdant?.Add(actor);
            if (actor.Actor.Oid == 0x2EBF) cll4Scarlet?.Add(actor);
        }
        if (!Active || !actor.NativeNpc) return;
        foreach (var rule in ActiveRules.Where(r => onlyRule == null || r == onlyRule))
        {
            var oid = actor.Actor.Oid;
            if (rule.Kind == BozjaRuleKind.Cll4Arena && oid is 0x1EB03C or 0x1EB02F)
            {
                if (oid == 0x1EB03C)
                {
                    cll4DawonWall = true;
                    Arena = BozjaCll4Catalog.DawonDefault;
                    Center = new(80, 0, -813);
                }
                else
                {
                    cll4LyonWall = true;
                    if (!Cll4DawonArena) Arena = BozjaCll4Catalog.LyonDefault;
                }
                Casts.RemoveAll(c => c.Rule == rule);
            }
            if (rule.Kind == BozjaRuleKind.Cll4Plumes && cll4FirstPlumes && oid is 0x2EBE or 0x2EBF)
                Cll4AddPlume(rule, actor, actor.Actor.Position, Now.AddSeconds(10.5d));
            if (rule.Kind == BozjaRuleKind.Cll4Obey && oid is 0x1EB03D or 0x1EB03E)
            {
                var point = DungeonEncounter.QuantizedDungeonPosition(actor.Actor.Position);
                var previous = cll4Obey.Count == 0 ? Primary!.Actor.Position : cll4Obey[^1].Origin;
                var cross = oid == 0x1EB03E;
                var rotation = cross ? MathF.Atan2(point.X - previous.X, point.Z - previous.Z) : 0;
                var at = cll4Obey.Count == 0 ? Now.AddSeconds(cll4FirstObey ? 11.5d : 13.8d)
                    : cll4Obey[0].Activation.AddSeconds(5.1d * cll4Obey.Count);
                cll4Obey.Add(new(++serial, rule, actor, point, rotation, at)
                    { ShapeOverride = cross ? new DungeonCrossTemplate(50, 7) : new DonutTemplate(12, 60) });
            }
        }
    }

    private void Cll4Destroyed(BozjaActor actor)
    {
        cll4Verdant?.Remove(actor);
        cll4Scarlet?.Remove(actor);
    }

    private void Cll4AddPlume(BozjaRule rule, BozjaActor actor, Vector3 point, DateTime at)
        => Casts.Add(new(++serial, rule, actor, DungeonEncounter.QuantizedDungeonPosition(point), 0, at)
        { ShapeOverride = actor.Actor.Oid == 0x2EBE ? new DonutTemplate(3, 12) : new CircleTemplate(10) });

    private void StartCll4(BozjaActor actor, BozjaRule rule)
    {
        var spell = actor.Cast!;
        if (rule.Kind == BozjaRuleKind.Cll4Arena)
        {
            var dawon = spell.ActionId == 20858;
            if (dawon ? cll4DawonWall : Cll4DawonArena || cll4LyonWall) return;
            Casts.RemoveAll(c => c.Rule == rule);
            Casts.Add(new(++serial, rule, actor, Center, 0, Activation(actor, dawon ? .2d : 1.2d))
            { ShapeOverride = dawon ? new DonutTemplate(30, 35) : new DonutTemplate(20, 25) });
        }
        if (rule.Kind == BozjaRuleKind.Cll4Hammer)
            cll4Hammers.Add(new(new(++serial, rule, actor, spell.Location, spell.Rotation ?? 0, Activation(actor)), false, true));
        if (rule.Kind == BozjaRuleKind.Cll4Wind)
            Casts.Add(new(++serial, rule, actor, spell.Location, 0, Activation(actor)));
        if (rule.Kind != BozjaRuleKind.Cll4Plumes || spell.ActionId != 20880) return;
        cll4Verdant ??= Actors.Values.Where(a => a.Actor.Oid == 0x2EBE).ToList();
        cll4Scarlet ??= Actors.Values.Where(a => a.Actor.Oid == 0x2EBF).ToList();
        var (sin, cos) = Math.SinCos(spell.Rotation ?? 0);
        var delta = 28 * new Vector3((float)sin, 0, (float)cos);
        foreach (var plume in cll4Verdant.Concat(cll4Scarlet))
        {
            var offset = plume.Actor.Position - spell.Location;
            var along = offset.X * (float)sin + offset.Z * (float)cos;
            var side = offset.X * (float)cos - offset.Z * (float)sin;
            if (along >= 0 && along <= 30 && MathF.Abs(side) <= 15)
                Cll4AddPlume(rule, plume, plume.Actor.Position + delta, Now.AddSeconds(8.5d));
        }
    }

    private void FinishCll4(BozjaActor actor, BozjaRule rule, ReplayCast spell)
    {
        if (rule.Kind == BozjaRuleKind.Cll4Plumes && spell.ActionId is 20865 or 20866)
        {
            Casts.RemoveAll(c => c.Rule == rule);
            cll4FirstPlumes = false;
        }
        if (rule.Kind == BozjaRuleKind.Cll4Hammer && rule.Actions.Contains(spell.ActionId))
        {
            var index = cll4Hammers.FindIndex(a => a.Cast.Source.Actor.Id == actor.Actor.Id);
            if (index >= 0) cll4Hammers.RemoveAt(index);
        }
        if (rule.Kind == BozjaRuleKind.Cll4Wind && rule.Actions.Contains(spell.ActionId))
        {
            var index = Casts.FindIndex(c => c.Rule == rule && c.Source.Actor.Id == actor.Actor.Id);
            if (index >= 0) Casts.RemoveAt(index);
        }
    }

    private void Cll4Effect(uint action)
    {
        if (!IsCll4 || cll4Obey.Count == 0 || action is not (20863 or 20864)) return;
        cll4Obey.RemoveAt(0);
        cll4FirstObey = false;
    }

    private IEnumerable<BozjaAoe> Cll4Aoes(BozjaRule rule)
    {
        if (rule.Kind is BozjaRuleKind.Cll4Arena or BozjaRuleKind.Cll4Plumes)
            foreach (var cast in Casts.Where(c => c.Rule == rule)) yield return new(cast, false, true);
        if (rule.Kind == BozjaRuleKind.Cll4Obey && cll4Obey.Count > 0)
            yield return new(cll4Obey[0], false, true);
        if (rule.Kind != BozjaRuleKind.Cll4Hammer || cll4Hammers.Count == 0) yield break;
        var deadline = cll4Hammers[0].Cast.Activation.AddSeconds(1.9d);
        var notLast = cll4Hammers[^1].Cast.Activation > deadline;
        for (var i = 0; i < cll4Hammers.Count; ++i)
        {
            var aoe = cll4Hammers[i];
            var risky = aoe.Cast.Activation < deadline;
            aoe = aoe with { Danger = aoe.Danger || risky && notLast, Risky = risky };
            cll4Hammers[i] = aoe;
        }
        foreach (var aoe in cll4Hammers) yield return aoe;
    }

    internal IEnumerable<(BozjaCast Cast, Vector3 From, Vector3 To)> Cll4Movements(BozjaActor viewer)
    {
        if (!Active || !IsCll4 || Cll4DawonArena) yield break;
        var from = viewer.Actor.Position;
        foreach (var cast in Casts.Where(c => c.Rule.Kind == BozjaRuleKind.Cll4Wind))
        {
            if (WindImmune(viewer, cast.Activation)) continue;
            var delta = from - cast.Origin;
            var length = MathF.Sqrt(delta.X * delta.X + delta.Z * delta.Z);
            if (!(length > 0)) continue;
            var inverse = 1f / length;
            var to = from + 10 * new Vector3(delta.X * inverse, 0, delta.Z * inverse);
            yield return (cast, from, to);
            from = to;
        }
    }

    private IEnumerable<BozjaZone> Cll4Navigation(BozjaRule rule)
    {
        foreach (var aoe in Cll4Aoes(rule).Where(a => a.Risky))
            yield return new($"cll4/aoe/{aoe.Cast.Generation}", rule.Component, aoe.Cast.Activation,
                aoe.Cast.Shape, aoe.Cast.Origin, aoe.Cast.Rotation);
        if (rule.Kind == BozjaRuleKind.Cll4Wind && Casts.FirstOrDefault(c => c.Rule == rule) is { } first)
            yield return new("cll4/wind", rule.Component, first.Activation, null, default, 0,
                [first.Origin], JoinRadius: 10, Goal: true);
    }
}
