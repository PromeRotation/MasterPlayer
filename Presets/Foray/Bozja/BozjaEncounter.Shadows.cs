using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed partial class BozjaEncounter
{
    private GeometryTemplate? shadowBossShape;
    private readonly List<(BozjaActor Source, GeometryTemplate? Shape)> shadowCopies = [];
    private static readonly GeometryTemplate shadowPulse = new DonutTemplate(8, 25);
    private static readonly GeometryTemplate shadowStorm = new FanTemplate(36, 2 * (65f * (MathF.PI / 180f)));
    private static readonly GeometryTemplate shadowGust = new CircleTemplate(20);

    private void ClearShadows()
    {
        shadowBossShape = null;
        shadowCopies.Clear();
    }

    private void ShadowStatus(BozjaActor actor, ReplayStatus status, bool gain)
    {
        if (!gain || status.Id != 2234
            || Definition.Rules.FirstOrDefault(r => r.Kind == BozjaRuleKind.Stormcall) is not { } rule) return;
        var position = actor.Actor.Position;
        var delta = new Vector2(position.X - Center.X, position.Z - Center.Z);
        var length = MathF.Sqrt(delta.X * delta.X + delta.Y * delta.Y);
        // WDir division rounds the reciprocal before multiplying components.
        // Direct Vector2 division changes R35 boundary checks by one ULP.
        var direction = length > 0 ? delta * (1f / length) : Vector2.Zero;
        var origin = Center + new Vector3(30f * direction.X, 0, 30f * direction.Y);
        Casts.Add(new(++serial, rule, actor, origin, 0, Now.AddSeconds(status.Param == 0x1E ? 9.7f : 19.9f)));
        var list = Casts.Where(c => c.Rule == rule).ToArray();
        if (list.Length != 3) return;
        SortActivations(list, 0, list.Length - 1, c => c.Activation);
        var index = 0;
        for (var i = 0; i < Casts.Count; ++i)
            if (Casts[i].Rule == rule) Casts[i] = list[index++];
    }

    private void StartShadows(BozjaActor actor, BozjaRule rule)
    {
        var spell = actor.Cast!;
        if (rule.Kind == BozjaRuleKind.Stormcall)
        {
            var index = Casts.FindIndex(c => c.Rule == rule && c.Source.Actor.Id == actor.Actor.Id);
            if (index >= 0) Casts[index] = Casts[index] with
            {
                Source = actor,
                Origin = spell.Location with { Y = 0 },
                Rotation = 0,
                Activation = Activation(actor),
            };
            return;
        }
        var shape = spell.ActionId switch
        {
            24130 => shadowPulse,
            24131 => shadowStorm,
            24132 => shadowGust,
            _ => null,
        };
        if (shape != null)
        {
            shadowBossShape = shape;
            Casts.Add(new(++serial, rule, actor, spell.Location with { Y = 0 }, spell.Rotation ?? 0,
                Activation(actor)) { ShapeOverride = shape });
            for (var i = 0; i < shadowCopies.Count; ++i)
                if (shadowCopies[i].Shape == null) shadowCopies[i] = (shadowCopies[i].Source, shape);
        }
        else if (spell.ActionId == 24129)
            shadowCopies.Add((actor, shadowBossShape));
        else return;
        if (shadowBossShape == null || shadowCopies.Count != 4) return;
        var activation = Activation(actor, 11.1f);
        foreach (var copy in shadowCopies)
            Casts.Add(new(++serial, rule, copy.Source,
                DungeonEncounter.QuantizedDungeonPosition(copy.Source.Actor.Position), copy.Source.Actor.Rotation,
                activation) { ShapeOverride = copy.Shape! });
    }

    private void FinishShadows(BozjaActor actor, BozjaRule rule, ReplayCast spell)
    {
        if (rule.Kind == BozjaRuleKind.Foreshadowing)
        {
            if (spell.ActionId is 24130 or 24131 or 24132) shadowBossShape = null;
            else if (spell.ActionId is 24133 or 24134 or 24135) shadowCopies.Clear();
            else return;
        }
        var index = Casts.FindIndex(c => c.Rule == rule && c.Source.Actor.Id == actor.Actor.Id);
        if (index >= 0) Casts.RemoveAt(index);
    }
}
