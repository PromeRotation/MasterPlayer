using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsNecron => territory == NecronCatalog.TERRITORY;
    private readonly Dictionary<ulong, NecronActorReference> necronActors = [];
    private NecronActorReference? necronPrimary;
    private readonly List<DungeonAmbientAoe> necronHands = [];
    private readonly List<DungeonAmbientAoe> necronFear = [];
    private readonly List<DungeonAmbientAoe> necronCold = [];
    private readonly List<DungeonAmbientAoe> necronRects = [];
    private readonly List<DungeonAmbientAoe> necronInvitations = [];
    private readonly List<DungeonAmbientAoe> necronBlights = [];
    private readonly List<string> necronStored = [];
    private readonly List<(NecronActorReference Target, DateTime At, int Key)> necronShockwaves = [];
    private DungeonAmbientAoe? necronMemento;
    private DungeonAmbientAoe? necronShrink;
    private DateTime necronDarkness;
    private DateTime necronNeutron;
    private bool necronHandFear;
    private bool necronHandMemento;
    private bool necronFearActive;
    private float necronColdOffset;
    private bool necronShow = true;
    private bool necronRelentless;
    private bool necronRotated;
    private int necronKey;
    private ulong necronWipePrimary;

    private bool NecronCanStart(DungeonActor actor)
    {
        if (actor.Oid != 0x4870) return false;
        if (actor.Id != necronWipePrimary) return actor.InCombat;
        if (!actor.InCombat) necronWipePrimary = 0;
        return false;
    }

    private void NecronObserve()
    {
        if (!IsNecron || Active == null) return;
        foreach (var id in necronActors.Keys.Where(id => !_actors.ContainsKey(id)).ToArray()) NecronRemoved(id);
        foreach (var actor in _actors.Values)
        {
            if (necronActors.TryGetValue(actor.Id, out var previous))
            {
                var changed = (previous.Actor.ModelState, previous.Actor.AnimationState1, previous.Actor.AnimationState2)
                    != (actor.ModelState, actor.AnimationState1, actor.AnimationState2);
                previous.Actor = actor;
                if (changed) NecronModel(actor);
            }
            else
            {
                necronActors[actor.Id] = new(actor);
                if (actor.Oid == 0x4903) NecronHandCreated(actor);
                if (actor.ModelState != 0 || actor.AnimationState1 != 0 || actor.AnimationState2 != 0) NecronModel(actor);
            }
            if (actor.Id == PrimaryId) necronPrimary = necronActors[actor.Id];
        }
        NecronTick();
        NecronUpdateArena();
    }

    private void NecronHandCreated(DungeonActor actor)
    {
        if (necronHandFear || necronHandMemento)
            necronHands.Add(new($"necron/hand/{++necronKey}", "扼杀之握连续条带", RayRectangleTemplate.Symmetric(6, 24),
                QuantizedDungeonPosition(actor.Position), actor.Rotation, Activation: _now.AddSeconds(necronHandFear ? 13.7 : 11.1)));
        if (necronFearActive)
            necronFear.Add(new($"necron/fear/{++necronKey}", "死亡恐惧预告", new CircleTemplate(3),
                QuantizedDungeonPosition(actor.Position), Activation: _now.AddSeconds(8)));
    }

    private void NecronCast(DungeonPendingCast cast)
    {
        if (!IsNecron || Active == null) return;
        var action = cast.Rule.Action;
        if (action is 44521 or 44532)
        {
            if (action == 44521) { necronHandFear = true; necronFearActive = true; }
            else necronHandMemento = true;
            foreach (var hand in _actors.Values.Where(a => a.Oid == 0x4903))
            {
                necronHands.Add(new($"necron/hand/{++necronKey}", "扼杀之握连续条带", RayRectangleTemplate.Symmetric(6, 24),
                    QuantizedDungeonPosition(hand.Position), hand.Rotation, Activation: _now.AddSeconds(action == 44521 ? 13.7 : 11.1)));
                if (action == 44521)
                    necronFear.Add(new($"necron/fear/{++necronKey}", "死亡恐惧预告", new CircleTemplate(3),
                        QuantizedDungeonPosition(hand.Position), Activation: _now.AddSeconds(8)));
            }
            if (action == 44532)
                necronMemento = new("necron/memento", "记忆死亡分裂场地", cast.Rule.Shape!, cast.Location, cast.Rotation, Activation: cast.FinishAt);
        }
        if (action == 44523 && _actors.GetValueOrDefault(cast.Source)?.Oid != 0x4903)
            necronHands.Add(new($"necron/hand/{++necronKey}", "扼杀之握", cast.Rule.Shape!, cast.Location, cast.Rotation, Activation: cast.FinishAt));
        if (action is 44524 or 44525)
        {
            necronColdOffset = action == 44524 ? 6 : -6;
            NecronLastCold(cast, 2.6);
        }
        if (action == 44611)
        {
            necronCold.Add(new($"necron/cold/{++necronKey}", "冰冷之握", cast.Rule.Shape!, cast.Location, cast.Rotation, Activation: cast.FinishAt));
            if (necronColdOffset != 0) NecronLastCold(cast, 1.6);
        }
        if (action == 44527) necronShow = false;
        if (action == 44531) necronRelentless = true;
        if (action == 44528)
        {
            necronShow = true;
            if (necronBlights.Count > 0) necronBlights[0] = necronBlights[0] with { Activation = cast.FinishAt.AddSeconds(1.2) };
        }
        if (action == 44603)
            necronShrink = new("necron/shrink", "大十字缩场预告", new DonutTemplate(9, 60),
                new(100, cast.Location.Y, 100), Activation: cast.FinishAt.AddSeconds(1.1));
        if (action == 44541) necronDarkness = cast.FinishAt.AddSeconds(6.4);
        if (action == 44538) necronNeutron = cast.FinishAt.AddSeconds(2.6);
        if (action == 44534 && necronRects.Count > 0)
            necronRects[0] = new($"necron/rect/{++necronKey}", "大十字实际条带", cast.Rule.Shape!,
                cast.Location, cast.Rotation, Activation: cast.FinishAt);
    }

    private void NecronLastCold(DungeonPendingCast cast, double delay)
    {
        if (necronCold.Count == 2)
            necronCold.Add(new($"necron/cold/{++necronKey}", "存在之惧提前偏移预告", RayRectangleTemplate.Symmetric(24, 30),
                new(100 + necronColdOffset + (necronColdOffset > 0 ? 1 : -1), cast.Location.Y, 85),
                cast.Rotation, Activation: cast.FinishAt.AddSeconds(delay)));
    }

    private void NecronFinish(DungeonPendingCast cast)
    {
        if (!IsNecron) return;
        var action = cast.Rule.Action;
        if (action == 44522) { necronFear.Clear(); necronFearActive = false; }
        if (action == 44523)
        {
            if (necronActors.GetValueOrDefault(cast.Source)?.Actor.Oid == 0x4903)
            { necronHands.Clear(); necronHandFear = necronHandMemento = false; }
            else if (necronHands.Count > 0) necronHands.RemoveAt(0);
        }
        if (action is 44611 or 44526 && necronCold.Count > 0)
        {
            var count = necronCold.Count; necronCold.RemoveAt(0);
            if (count == 2)
            {
                necronCold[0] = necronCold[0] with { Position = QuantizedDungeonPosition(new(100 + necronColdOffset, cast.Location.Y, 85)) };
                necronColdOffset = 0;
            }
        }
        if (action is 45181 or 45182)
        {
            necronRelentless = false;
            if (necronBlights.Count > 0)
            {
                necronBlights.RemoveAt(0); necronStored.RemoveAt(0);
                if (necronStored.Count == 0) necronRotated = false;
            }
        }
        if (action == 44534 && necronRects.Count > 0) necronRects.RemoveAt(0);
        if (action == 44817 && necronInvitations.Count > 0) necronInvitations.RemoveAt(0);
    }

    private void NecronIcon(ulong actor, ulong target, uint icon)
    {
        if (!IsNecron || Active == null || !necronActors.TryGetValue(actor, out var reference)) return;
        if (icon is 604 or 621 or 605 or 622)
        {
            var circle = icon is 604 or 621;
            necronStored.Add(circle ? "Circle" : "Donut");
            if (!necronRelentless)
                necronBlights.Add(new($"necron/blight/{++necronKey}", circle ? "以太病圆" : "以太病环",
                    circle ? new CircleTemplate(20) : new DonutTemplate(16, 60),
                    QuantizedDungeonPosition(reference.Actor.Position), reference.Actor.Rotation));
        }
        if (icon == 615)
            necronShockwaves.Add((necronActors.GetValueOrDefault(target) ?? reference, _now.AddSeconds(7.2), ++necronKey));
    }

    private void NecronModel(DungeonActor actor)
    {
        if (necronRotated || !necronRelentless || necronStored.Count != 4) return;
        var index = actor.ModelState switch { 21 => 0, 147 => 1, 65 => 2, 22 => 3, _ => -1 };
        if (index < 0) return;
        var rotated = necronStored.Skip(index).Concat(necronStored.Take(index)).ToArray();
        necronStored.Clear(); necronStored.AddRange(rotated);
        for (var i = 0; i < 4; ++i)
            necronBlights.Add(new($"necron/blight/{++necronKey}", rotated[i] == "Circle" ? "以太病圆" : "以太病环",
                rotated[i] == "Circle" ? new CircleTemplate(20) : new DonutTemplate(16, 60),
                QuantizedDungeonPosition(actor.Position), actor.Rotation, Activation: _now.AddSeconds(14.4 + i * 2.8)));
        necronRotated = true;
    }

    private void NecronTether(ulong source, bool present)
    {
        if (!IsNecron || Active == null || !present || Arena == null || _actors.GetValueOrDefault(source) is not { } actor) return;
        if (actor.Oid == 0x49DD)
            necronInvitations.Add(new($"necron/invitation/{++necronKey}", "邀请连续条带", RayRectangleTemplate.Symmetric(10, 36),
                QuantizedDungeonPosition(actor.Position), actor.Rotation, Activation: _now.AddSeconds(12.3)));
        if (actor.Oid is 0x4948 or 0x490A)
        {
            var offset = actor.Oid == 0x4948 ? 41f : -153f;
            var angle = MathF.Atan2(Arena.Position.X - actor.Position.X, Arena.Position.Z - actor.Position.Z) + offset * MathF.PI / 180;
            necronRects.Add(new($"necron/rect/{++necronKey}", "大十字条带预告", new RectangleTemplate(4.5f, 100),
                Arena.Position, angle, Activation: _now.AddSeconds(actor.Oid == 0x4948 ? 7.6 : 5.6)));
            if (necronRects.Count > 1 && necronRects[0].Activation > necronRects[1].Activation)
                (necronRects[0], necronRects[1]) = (necronRects[1], necronRects[0]);
        }
    }

    private void NecronEffect(uint action)
    {
        if (!IsNecron) return;
        if (action == 44542) necronDarkness = default;
        if (action == 44539) necronNeutron = default;
        if (action == 44547 && necronShockwaves.Count > 0) necronShockwaves.RemoveAt(0);
    }

    private void NecronTick()
    {
        if (necronShockwaves.FirstOrDefault().Target is { Actor: var target } && (target.Dead || target.Position.Y < -100))
            necronShockwaves.Clear();
    }

    private void NecronRemoved(ulong source)
    {
        if (!IsNecron || !necronActors.TryGetValue(source, out _)) return;
        if (_casts.TryGetValue(source, out var cast)) NecronFinish(cast);
        necronActors.Remove(source);
    }

    private void ResetNecron()
    {
        necronActors.Clear(); necronHands.Clear(); necronFear.Clear(); necronCold.Clear(); necronRects.Clear();
        necronPrimary = null;
        necronInvitations.Clear(); necronBlights.Clear(); necronStored.Clear(); necronShockwaves.Clear();
        necronMemento = necronShrink = null; necronDarkness = necronNeutron = default;
        necronHandFear = necronHandMemento = necronFearActive = necronRelentless = necronRotated = false;
        necronShow = true; necronColdOffset = 0; necronKey = 0;
        ResetNecronArena();
    }
}
