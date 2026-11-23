using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsValigarmanda => territory == ValigarmandaCatalog.TERRITORY;
    private readonly Dictionary<ulong, ValigarmandaActorReference> valigActors = [];
    private readonly List<DungeonAmbientAoe> valigLightning = [];
    private readonly List<DungeonAmbientAoe> valigChilling = [];
    private readonly List<DungeonPendingCast> valigTowers = [];
    private readonly List<DungeonPendingCast> valigKnockbacks = [];
    private readonly List<(ValigarmandaActorReference Source, ValigarmandaActorReference Target, DateTime At, uint Marker, int Key)> valigLines = [];
    private readonly List<(ValigarmandaActorReference Source, ValigarmandaActorReference Target, DateTime At, int Key)> valigTalons = [];
    private readonly DateTime[] valigDisaster = new DateTime[3];
    private DungeonAmbientAoe? valigCoil;
    private DungeonAmbientAoe? valigNorthern;
    private int valigKey;
    private bool valigBolt;
    private readonly bool[] valigGroundHint = new bool[64];
    private DateTime valigPlatformAt;

    private void ValigObserve()
    {
        if (!IsValigarmanda || Active == null) return;
        foreach (var id in valigActors.Keys.Where(id => !_actors.ContainsKey(id)).ToArray()) ValigRemoved(id);
        foreach (var actor in _actors.Values)
        {
            if (valigActors.TryGetValue(actor.Id, out var current)) current.Actor = actor;
            else
            {
                valigActors[actor.Id] = new(actor);
                var position = QuantizedDungeonPosition(actor.Position);
                if (actor.Oid == 0x4181)
                    valigLightning.Add(new($"valig/lightning/{++valigKey}", "秘雷", RayRectangleTemplate.Symmetric(5, 50),
                        position, actor.Rotation, Activation: _now.AddSeconds(8.6)));
                if (actor.Oid == 0x4493)
                {
                    valigChilling.Add(new($"valig/chilling/{++valigKey}", "冰灾十字", new DungeonCrossTemplate(40, 2.5f),
                        position, -.003f * (MathF.PI / 180), Activation: _now.AddSeconds(7.1)));
                    valigChilling.Add(new($"valig/chilling/{++valigKey}", "冰灾斜十字", new DungeonCrossTemplate(40, 2.5f),
                        position, 44.998f * (MathF.PI / 180), Activation: _now.AddSeconds(7.1)));
                }
            }
        }
        valigTalons.RemoveAll(b => b.Target.Actor.Dead);
        ValigObserveStatuses();
        ValigTickMovement();
    }

    private void ValigCast(DungeonPendingCast cast)
    {
        if (!IsValigarmanda || Active == null) return;
        var action = cast.Rule.Action;
        if (action is 36160 or 36156)
            valigCoil = new("valig/coil", "缠绕或低语", action == 36160 ? new DonutTemplate(8, 30)
                : new FanTemplate(50, 80 * (MathF.PI / 180)), cast.Location, cast.Rotation, Activation: cast.FinishAt);
        if (action == 36187) valigTowers.Add(cast);
        if (action == 36189) valigKnockbacks.Add(cast);
        if (action == 36197)
        {
            valigDisaster[0] = cast.FinishAt.AddSeconds(3.1);
            valigDisaster[1] = cast.FinishAt.AddSeconds(11.6);
            valigDisaster[2] = cast.FinishAt.AddSeconds(19.6);
        }
        if (action == 36177) Array.Fill(valigFreeze, cast.FinishAt.AddSeconds(1));
        if (action == 36176)
        {
            // 源 requireLevitating 从未置位，ThunderousBreath 只复制空位，不能按技能描述补造平台提示。
            foreach (var actor in _actors.Values.Where(a => a.PartySlot is >= 0 and < 64)) valigGroundHint[actor.PartySlot] = false;
            valigPlatformAt = cast.FinishAt;
        }
        if (action == 36172)
        {
            foreach (var actor in _actors.Values.Where(a => a.PartySlot is >= 0 and < 64)) valigGroundHint[actor.PartySlot] = true;
            valigPlatformAt = cast.FinishAt;
            valigBolt = true;
        }
    }

    private void ValigFinish(ulong source, uint action)
    {
        if (!IsValigarmanda) return;
        if (action == 39001) valigLightning.Clear();
        if (action == 39265) valigChilling.Clear();
        if (action is 36160 or 36156) valigCoil = null;
        if (action == 36187) Remove(valigTowers);
        if (action == 36189) Remove(valigKnockbacks);
        if (action is 36176 or 36174) Array.Clear(valigGroundHint);
        if (action == 36172) valigBolt = false;
        void Remove(List<DungeonPendingCast> list)
        {
            var i = list.FindIndex(c => c.Source == source);
            if (i >= 0) list.RemoveAt(i);
        }
    }

    private void ValigIcon(ulong actor, ulong targetId, uint icon, DateTime at)
    {
        if (!IsValigarmanda || Active == null || icon != 344 || !valigActors.TryGetValue(PrimaryId, out var source)) return;
        var target = valigActors.GetValueOrDefault(targetId) ?? valigActors.GetValueOrDefault(actor);
        if (target != null) valigTalons.Add((source, target, at.AddSeconds(5), ++valigKey));
    }

    private void ValigEffect(ulong source, uint action, ulong target, DateTime at)
    {
        if (!IsValigarmanda || Active == null) return;
        if (action is 36168 or 36169) valigNorthern = null;
        if (action is 26708 or 34722 && valigActors.TryGetValue(source, out var caster) && valigActors.TryGetValue(target, out var marked))
            valigLines.Add((caster, marked, at.AddSeconds(5), action, ++valigKey));
        if (action == 36194)
            foreach (var marker in new[] { 26708u, 34722u })
            {
                var lines = valigLines.Where(l => l.Marker == marker).ToArray();
                if (lines.Length == 1 && valigActors.TryGetValue(target, out var corrected))
                {
                    var i = valigLines.IndexOf(lines[0]);
                    valigLines[i] = lines[0] with { Target = corrected };
                }
                var index = valigLines.FindIndex(l => l.Marker == marker && l.Target.Actor.Id == target);
                if (index >= 0) valigLines.RemoveAt(index);
            }
        if (action == 38323) valigLines.Clear();
        if (action == 36185 && valigTalons.Count > 0) valigTalons.RemoveAt(0);
        var hit = action switch { 36199 => 0, 36200 => 1, 36198 => 2, _ => -1 };
        if (hit >= 0) valigDisaster[hit] = default;
    }

    private void ValigMap(uint index, uint state, DateTime at)
    {
        if (!IsValigarmanda || Active == null || Arena == null || index != 2) return;
        Vector3? position = state switch
        {
            0x00200010 => new(131.487f, Arena.Position.Y, 107.988f),
            0x00020001 => new(116.472f, Arena.Position.Y, 127.977f),
            _ => null,
        };
        if (position is { } p) valigNorthern = new("valig/northern", "北十字", RayRectangleTemplate.Symmetric(25, 60),
            p, -126.875f * (MathF.PI / 180), Activation: at.AddSeconds(9.2));
    }

    private void ValigRemoved(ulong id)
    {
        if (!IsValigarmanda) return;
        if (_casts.TryGetValue(id, out var cast)) ValigFinish(id, cast.Rule.Action);
        if (valigActors.GetValueOrDefault(id)?.Actor is { PartySlot: >= 0 and < 64 } actor)
        {
            var key = (id, 3523u);
            var present = HasStatus(actor, 3523);
            if (valigStatusPending.TryGetValue(key, out var pending)) present = pending.Present;
            else if (valigStatusRaw.TryGetValue(key, out var raw)) present = raw.Present;
            if (present) valigFreeze[actor.PartySlot] = default;
        }
        valigActors.Remove(id);
        foreach (var key in valigStatusRaw.Keys.Where(k => k.Actor == id).ToArray()) valigStatusRaw.Remove(key);
        foreach (var key in valigStatusPending.Keys.Where(k => k.Actor == id).ToArray()) valigStatusPending.Remove(key);
        foreach (var key in valigStatusPendingAt.Keys.Where(k => k.Actor == id).ToArray()) valigStatusPendingAt.Remove(key);
    }

    private void ResetValig()
    {
        valigActors.Clear(); valigLightning.Clear(); valigChilling.Clear(); valigTowers.Clear(); valigKnockbacks.Clear();
        valigLines.Clear(); valigTalons.Clear(); Array.Clear(valigDisaster); valigCoil = valigNorthern = null;
        valigKey = 0; valigBolt = false; Array.Clear(valigGroundHint); valigPlatformAt = default;
        Array.Clear(valigFreeze); valigStatusRaw.Clear(); valigStatusPending.Clear(); valigStatusPendingAt.Clear(); valigMoveAnchor = null; valigMovePlayer = 0;
    }
}
