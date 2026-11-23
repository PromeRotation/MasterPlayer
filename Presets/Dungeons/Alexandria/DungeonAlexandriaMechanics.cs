using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private long alexSerial;
    private bool alexSmall;
    private int alexPurgeCount;
    private ulong alexForbidden;
    private DungeonAmbientAoe? alexShrink;
    private readonly HashSet<ulong> alexObserved = [];
    private readonly HashSet<(ulong Source, DateTime Start)> alexStarts = [];
    private readonly List<DungeonPendingCast> alexSimple = [];
    private readonly List<(DungeonAmbientAoe Aoe, uint Kind, float Inner)> alexQueue = [];
    private readonly List<(Vector3 Origin, int Step, DateTime At, ulong Source, DateTime Start)> alexTernary = [];
    private readonly List<(DungeonActor Target, DateTime At, ulong Forbidden, long Serial, ulong Source)> alexStacks = [];
    private readonly List<(DungeonPendingCast Cast, DungeonActor Target, long Serial)> alexSpreads = [];
    private readonly List<(DungeonActor Target, DateTime At, long Serial)> alexDisinfection = [];
    private readonly List<(DungeonActor Source, DungeonActor Target, DateTime At, uint Resolve, long Serial)> alexLines = [];
    private readonly List<(DungeonActor Source, DungeonActor Target, DateTime At, long Serial, DateTime Start)> alexSalvation = [];

    private void AlexObserveActors()
    {
        if (territory != AlexandriaCatalog.TERRITORY || Active == null) return;
        if (Active.BossOid == 0x4173)
            foreach (var actor in _actors.Values.Where(a => a.Oid is 0x4174 or 0x4175))
                if (alexObserved.Add(actor.Id))
                {
                    var at = alexQueue.Count == 0 ? _now.AddSeconds(9.9) : alexQueue[0].Aoe.Activation!.Value.AddSeconds(2.5 * alexQueue.Count);
                    var donut = actor.Oid == 0x4174;
                    AlexAddQueue(new($"alex/queue/{++alexSerial}", donut ? "连续月环" : "连续十字",
                        donut ? new DonutTemplate(4, 40) : new DungeonCrossTemplate(40, 3),
                        QuantizedDungeonPosition(actor.Position), Activation: at), actor.Oid, donut && alexQueue.Count > 0 ? 5.5f : 4);
                }
    }

    private void AlexAddQueue(DungeonAmbientAoe aoe, uint kind, float inner)
    {
        alexQueue.Add((aoe, kind, inner));
        alexQueue.Sort((a, b) => a.Aoe.Activation!.Value.CompareTo(b.Aoe.Activation!.Value));
        AlexUpdateQueue();
    }

    private void AlexUpdateQueue()
    {
        if (alexQueue.Count == 0) return;
        var first = alexQueue[0];
        if (alexPurgeCount > 0 && first.Kind == 0x4174) first.Inner = 4;
        first.Aoe = first.Aoe with { Navigate = true,
            Color = alexQueue.Count > 1 ? new Vector4(1, 1, 0, 1) : first.Aoe.Color };
        alexQueue[0] = first;
        if (alexQueue.Count < 2) return;
        var second = alexQueue[1];
        var center = Arena!.Position;
        var risky = !(first.Kind == 0x4174 && second.Kind is 0x4174 or 0x4175)
            && !(first.Kind == 36379 && second.Kind == 0x4174
                && MathF.Abs(second.Aoe.Position.X - center.X) <= 1 && MathF.Abs(second.Aoe.Position.Z - center.Z) <= 1);
        alexQueue[1] = (second.Aoe with { Navigate = risky }, second.Kind, second.Inner);
    }

    private void AlexCast(DungeonPendingCast cast)
    {
        if (territory != AlexandriaCatalog.TERRITORY || Arena == null) return;
        var action = cast.Rule.Action;
        var fresh = alexStarts.Add((cast.Source, cast.StartedAt));
        var id = $"alex/cast/{cast.Source}/{cast.StartedAt.Ticks}";
        if (action is 36379 or 36381)
        {
            var index = alexQueue.FindIndex(q => q.Aoe.Id == id);
            if (index >= 0)
            {
                var old = alexQueue[index];
                alexQueue[index] = (old.Aoe with { Position = cast.Location, Rotation = cast.Rotation }, old.Kind, old.Inner);
                AlexUpdateQueue();
            }
            else if (fresh) AlexAddQueue(new(id, cast.Rule.Name, cast.Rule.Shape!, cast.Location, cast.Rotation, Activation: cast.FinishAt), action, 4);
        }
        else if (action == 39254)
        {
            var index = alexTernary.FindIndex(t => t.Source == cast.Source && t.Start == cast.StartedAt);
            if (index >= 0) alexTernary[index] = (cast.Location, alexTernary[index].Step, alexTernary[index].At, cast.Source, cast.StartedAt);
            else if (fresh) alexTernary.Add((cast.Location, 0, cast.FinishAt, cast.Source, cast.StartedAt));
        }
        else if (action == 36333 && _actors.TryGetValue(cast.TargetId, out var target))
        {
            var index = alexStacks.FindIndex(s => s.Source == cast.Source);
            var value = (target, cast.FinishAt, 0UL, ++alexSerial, cast.Source);
            if (index < 0 && fresh) alexStacks.Add(value);
            else if (index >= 0) alexStacks[index] = (target, cast.FinishAt, 0, alexStacks[index].Serial, cast.Source);
        }
        else if (action is 36334 or 39243 && _actors.TryGetValue(cast.TargetId, out var spreadTarget))
        {
            var index = alexSpreads.FindIndex(s => s.Cast.Source == cast.Source && s.Cast.StartedAt == cast.StartedAt);
            if (index < 0 && fresh) alexSpreads.Add((cast, spreadTarget, ++alexSerial));
            else if (index >= 0) alexSpreads[index] = (cast, spreadTarget, alexSpreads[index].Serial);
        }
        else if (action == 36783 && _actors.TryGetValue(cast.Source, out var source) && _actors.TryGetValue(cast.TargetId, out var bait))
        {
            var index = alexSalvation.FindIndex(b => b.Source.Id == cast.Source && b.Start == cast.StartedAt);
            var value = (source, bait, cast.FinishAt.AddSeconds(.2), ++alexSerial, cast.StartedAt);
            if (index < 0 && fresh) alexSalvation.Add(value);
            else if (index >= 0) alexSalvation[index] = value;
        }
        else if (cast.Rule.Shape != null && cast.Rule.PartyAoe == null)
        {
            var index = alexSimple.FindIndex(c => c.Source == cast.Source && c.StartedAt == cast.StartedAt);
            if (index < 0) alexSimple.Add(cast); else alexSimple[index] = cast;
        }
        if (!alexSmall && action is 36378 or 36338 or 36765)
        {
            var outer = action == 36378 ? new Vector2(23, 18) : action == 36338 ? new(23, 23) : new(16, 16);
            var inner = action == 36378 ? new Vector2(20, 15) : action == 36338 ? new(20, 20) : new(15, 15);
            alexShrink = new("alex/shrink", "缩场预告", VanguardRing(Arena.Position, outer, inner),
                Arena.Position, Activation: cast.FinishAt.AddSeconds(action == 36378 ? .8 : action == 36338 ? .5 : .7));
        }
    }

    private void AlexFinish(DungeonPendingCast cast, DateTime now)
    {
        if (territory != AlexandriaCatalog.TERRITORY) return;
        alexSimple.RemoveAll(c => c.Source == cast.Source);
        if (cast.Rule.Action == 36333)
        {
            var stack = alexStacks.FindIndex(s => s.Target.Id == cast.TargetId);
            if (stack >= 0) alexStacks.RemoveAt(stack);
        }
        if (cast.Rule.Action is 36334 or 39243)
        {
            var spread = alexSpreads.FindIndex(s => s.Cast.Rule.Action == cast.Rule.Action && s.Target.Id == cast.TargetId);
            if (spread >= 0) alexSpreads.RemoveAt(spread);
        }
        if (cast.Rule.Action is 36379 or 36381 or 36382 or 36383 && alexQueue.Count > 0)
        {
            alexQueue.RemoveAt(0);
            if (cast.Rule.Action is 36382 or 36383) ++alexPurgeCount;
            AlexUpdateQueue();
            if (alexPurgeCount == 5) alexPurgeCount = 0;
        }
        var order = cast.Rule.Action switch { 39254 => 0, 39255 => 1, 39256 => 2, _ => -1 };
        var index = alexTernary.FindIndex(t => t.Step == order && MathF.Abs(t.Origin.X - cast.Location.X) <= 1
            && MathF.Abs(t.Origin.Z - cast.Location.Z) <= 1);
        if (index >= 0)
        {
            var old = alexTernary[index];
            if (order == 2) alexTernary.RemoveAt(index);
            else alexTernary[index] = (old.Origin, old.Step + 1, now.AddSeconds(2), old.Source, old.Start);
        }
    }

    private void AlexIcon(ulong target, uint icon, DateTime now)
    {
        if (territory != AlexandriaCatalog.TERRITORY || Active?.BossOid != 0x4173 || !_actors.TryGetValue(target, out var actor)) return;
        if (icon == 62)
        {
            alexStacks.Add((actor, now.AddSeconds(5.1), 0, ++alexSerial, 0));
            if (alexForbidden != 0)
            {
                var first = alexStacks[0];
                alexStacks[0] = (first.Target, first.At, alexForbidden, first.Serial, first.Source);
                alexForbidden = 0;
            }
        }
        if (icon == 344)
        {
            if (actor.PartySlot is >= 0 and < 64)
            {
                var mask = 1UL << actor.PartySlot;
                if (alexStacks.Count > 0)
                {
                    var first = alexStacks[0];
                    alexStacks[0] = (first.Target, first.At, first.Forbidden | mask, first.Serial, first.Source);
                }
                else alexForbidden |= mask;
            }
            alexDisinfection.Add((actor, now.AddSeconds(5.1), ++alexSerial));
        }
    }

    private void AlexEffect(ulong source, uint action, ulong target, DateTime now)
    {
        if (territory != AlexandriaCatalog.TERRITORY) return;
        if (action == 36385 && alexDisinfection.Count > 0) alexDisinfection.RemoveAt(0);
        if (action == 36386 && alexStacks.Count > 0)
        {
            var index = alexStacks.FindIndex(s => s.Target.Id == target);
            alexStacks.RemoveAt(index < 0 ? 0 : index);
        }
        if (action is 36778 or 36786 && _actors.TryGetValue(source, out var a) && _actors.TryGetValue(target, out var b))
            alexLines.Add((a, b, now.AddSeconds(action == 36778 ? 5 : 5.5), action == 36778 ? 36780u : 36787u, ++alexSerial));
        if (action is 36780 or 36787)
        {
            var index = alexLines.FindIndex(l => l.Resolve == action && l.Target.Id == target);
            if (index >= 0) alexLines.RemoveAt(index);
            else if (_actors.ContainsKey(target) && alexLines.Count(l => l.Resolve == action) == 1) alexLines.RemoveAll(l => l.Resolve == action);
        }
        if (action == 36784) alexSalvation.Clear();
    }

    private void SynchronizeAlex()
    {
        if (territory != AlexandriaCatalog.TERRITORY) return;
        for (var i = 0; i < alexStacks.Count; ++i)
        {
            var s = alexStacks[i];
            alexStacks[i] = ( _actors.GetValueOrDefault(s.Target.Id) ?? s.Target, s.At, s.Forbidden, s.Serial, s.Source);
        }
        for (var i = 0; i < alexDisinfection.Count; ++i)
        {
            var b = alexDisinfection[i];
            alexDisinfection[i] = (_actors.GetValueOrDefault(b.Target.Id) ?? b.Target, b.At, b.Serial);
        }
        alexDisinfection.RemoveAll(b => b.Target.Dead);
        for (var i = 0; i < alexSpreads.Count; ++i)
        {
            var s = alexSpreads[i];
            alexSpreads[i] = (s.Cast, _actors.GetValueOrDefault(s.Target.Id) ?? s.Target, s.Serial);
        }
        for (var i = 0; i < alexLines.Count; ++i)
        {
            var b = alexLines[i];
            alexLines[i] = (_actors.GetValueOrDefault(b.Source.Id) ?? b.Source, _actors.GetValueOrDefault(b.Target.Id) ?? b.Target, b.At, b.Resolve, b.Serial);
        }
        for (var i = 0; i < alexSalvation.Count; ++i)
        {
            var b = alexSalvation[i];
            alexSalvation[i] = (_actors.GetValueOrDefault(b.Source.Id) ?? b.Source, _actors.GetValueOrDefault(b.Target.Id) ?? b.Target, b.At, b.Serial, b.Start);
        }
    }

    private void AlexMap(uint index, uint state)
    {
        if (territory != AlexandriaCatalog.TERRITORY || Arena == null) return;
        if (state == 0x00020001 && (Active?.BossOid, index) is (0x4173, 3) or (0x416A, 0x27) or (0x41CE, 0x28))
        {
            var size = index == 3 ? new Vector2(40, 30) : index == 0x27 ? new(40, 40) : new(30, 30);
            Arena = new RectangleGeometry(Arena.Position, 0, size.X, size.Y);
            alexSmall = true;
            alexShrink = null;
        }
        if (index == 0x2F && state == 0x00080004)
        {
            alexLines.RemoveAll(l => l.Resolve == 36787);
            alexSalvation.Clear();
        }
    }

    private void ResetAlex()
    {
        alexSerial = 0; alexSmall = false; alexPurgeCount = 0; alexForbidden = 0; alexShrink = null;
        alexObserved.Clear(); alexStarts.Clear(); alexSimple.Clear(); alexQueue.Clear(); alexTernary.Clear();
        alexStacks.Clear(); alexSpreads.Clear(); alexDisinfection.Clear(); alexLines.Clear(); alexSalvation.Clear();
    }
}
