using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed partial class BozjaEncounter
{
    internal List<BozjaCast> Burns { get; } = [];
    internal string? BurnSequenceError { get; private set; }
    internal string? SequenceError => TimeSequenceError ?? BurnSequenceError ?? ThermalSequenceError ?? (Active ? DiabloSequenceError : null);
    internal int MissingLightningPosition { get; private set; }

    private void ClearAida()
    {
        Burns.Clear();
        BurnSequenceError = null;
        MissingLightningPosition = 0;
    }

    internal void CompleteEventBatch()
    {
        // If an old packet's actor disappeared, Feed skips the callback and
        // still completes Flush. A diagnostic must not keep the Region locked.
        BurnSequenceError = null;
        TimeSequenceError = null;
    }

    private void ObserveModelState(BozjaActor actor)
    {
        var value = actor.Actor;
        var next = (value.ModelState, value.AnimationState1, value.AnimationState2);
        if (actor.ObservedModelState == next) return;
        actor.ObservedModelState = next;
        ModelState(actor, next.ModelState, next.AnimationState1, next.AnimationState2);
    }

    internal void ModelState(BozjaActor actor, byte model, byte first, byte second)
    {
        // BossModule excludes only Pet for this callback; ordinary spell
        // filtering would also exclude Player/Chocobo/Buddy and lose inputs.
        if (!Active || actor.IsPet || actor.Actor.Oid != 0x31CB || first != 1
            || Definition.Rules.FirstOrDefault(r => r.Kind == BozjaRuleKind.Burn) is not { } rule) return;
        Burns.Add(new(++serial, rule, actor, DungeonEncounter.QuantizedDungeonPosition(actor.Actor.Position),
            0, Now.AddSeconds(5d)));
    }

    private void StartAida(BozjaActor actor, BozjaRule rule)
    {
        var spell = actor.Cast!;
        if (rule.Kind == BozjaRuleKind.LightningClap && spell.ActionId == 23968)
            Casts.Add(new(++serial, rule, actor, spell.Location, spell.Rotation ?? 0, Activation(actor)));
    }

    private void AidaEffect(BozjaActor actor, uint action, Vector3? position)
    {
        if (action == 23976 && Definition.Rules.Any(r => r.Kind == BozjaRuleKind.Burn))
        {
            BurnSequenceError = Burns.Count == 0 ? "Burn 空队列效果，保留事件等待真实 ModelState 输入" : null;
            // Source deliberately throws on empty FIFO. The script withdraws
            // the Region and retries the unchanged packet batch next frame.
            Burns.RemoveAt(0);
        }
        if (Definition.Rules.FirstOrDefault(r => r.Kind == BozjaRuleKind.LightningClap) is not { } rule) return;
        if (action is not (23968 or 23969)) return;
        var point = action == 23968 ? position : actor.Actor.Position;
        if (point == null)
        {
            ++MissingLightningPosition;
            return;
        }
        var index = Casts.FindIndex(c => c.Rule == rule && AlmostEqual(c.Origin, point.Value));
        if (index < 0) return;
        if (action == 23969) Casts.RemoveAt(index);
        else Casts[index] = Casts[index] with
        {
            Origin = point.Value, Rotation = 0, ShapeOverride = new DonutTemplate(4, 10), Activation = Now.AddSeconds(6.1d),
        };
    }

    private IEnumerable<BozjaAoe> AidaAoes(BozjaRule rule)
    {
        if (rule.Kind == BozjaRuleKind.LightningClap)
            foreach (var cast in Casts.Where(c => c.Rule == rule)) yield return new(cast, false, true);
        if (rule.Kind == BozjaRuleKind.Burn && Burns.Count != 0)
        {
            var deadline = Burns[0].Activation.AddSeconds(1d);
            foreach (var cast in Burns)
            {
                if (cast.Activation >= deadline) yield break;
                yield return new(cast, false, true);
            }
        }
    }
}
