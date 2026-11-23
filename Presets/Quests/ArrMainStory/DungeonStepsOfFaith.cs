using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private int stepsPhase = 1;
    private float stepsMinZ = 120;
    private float stepsMaxZ = 300;
    private readonly List<StepsEarthrisingLine> stepsLines = [];
    private int stepsLineId;
    private DungeonAmbientAoe? stepsFlame;
    private DungeonAmbientAoe? stepsFlame2;
    private int stepsFlameHits;
    private int stepsFlame2Hits;
    private bool stepsScorching;
    private bool stepsCauterize;

    private void StepsObserve()
    {
        if (!IsStepsOfFaith || Active == null || Arena == null || _actors.GetValueOrDefault(PlayerId) is not { } player) return;
        var center = new Vector3(0, Arena.Position.Y, Math.Clamp(player.Position.Z, stepsMinZ + 40, stepsMaxZ - 40));
        if (Arena.Position != center) Arena = new RectangleGeometry(center, 0, 44, 80);
    }

    private void StepsMap(uint index, uint state)
    {
        if (!IsStepsOfFaith) return;
        if (state == 0x00020001)
        {
            switch (index)
            {
                case 3: stepsPhase = 2; stepsMinZ = 120; stepsMaxZ = 200; break;
                case 4: stepsPhase = 4; stepsMinZ = -40; stepsMaxZ = 40; break;
                case 6: stepsPhase = 6; stepsMinZ = -200; stepsMaxZ = -120; break;
            }
        }
        else if (state == 0x00800040)
        {
            switch (index)
            {
                case 0: stepsPhase = 3; stepsMinZ = -40; stepsMaxZ = 200; break;
                case 1: stepsPhase = 5; stepsMinZ = -200; stepsMaxZ = 40; break;
            }
        }
        StepsObserve();
    }

    private void StepsCast(DungeonPendingCast cast)
    {
        if (!IsStepsOfFaith || _actors.GetValueOrDefault(PrimaryId) is not { } boss) return;
        switch (cast.Rule.Action)
        {
            case 30888:
                if (_actors.GetValueOrDefault(cast.Source) is { } caster)
                    stepsLines.Add(new(++stepsLineId, caster.Position, cast.FinishAt,
                        (cast.FinishAt > _now ? cast.FinishAt : _now).AddSeconds(1), 5));
                break;
            case 30185:
                stepsFlame = new("steps/flame", "连续吐息35次", RayRectangleTemplate.Symmetric(20, 500),
                    boss.Position, boss.Rotation, Activation: cast.FinishAt.AddSeconds(1));
                break;
            case 26411:
                stepsFlame2Hits = 0;
                stepsFlame2 = new("steps/flame2", "连续吐息14次", RayRectangleTemplate.Symmetric(20, 60),
                    cast.Location, cast.Rotation, Activation: cast.FinishAt);
                break;
            case 29789:
                stepsScorching = true;
                break;
            case 30885:
                stepsCauterize = true;
                break;
        }
    }

    private void StepsEffect(ulong source, uint action, DateTime at)
    {
        if (!IsStepsOfFaith) return;
        if (action == 30884)
        {
            if (++stepsFlameHits >= 35) { stepsFlame = null; stepsFlameHits = 0; }
            if (++stepsFlame2Hits >= 14) stepsFlame2 = null;
        }
        if (action is not (30888 or 26412) || _actors.GetValueOrDefault(source) is not { } caster) return;
        var pos = caster.Position;
        var index = stepsLines.FindIndex(l => MathF.Abs(l.Next.X - pos.X) <= 1 && MathF.Abs(l.Next.Z - pos.Z) <= 1);
        if (index < 0) return;
        var line = stepsLines[index];
        if (line.Left == 1) stepsLines.RemoveAt(index);
        else stepsLines[index] = line with { Next = pos - Vector3.UnitZ * 7.5f, NextAt = at.AddSeconds(1),
            FutureAt = at.AddSeconds(2), Left = line.Left - 1 };
    }

    private IEnumerable<DungeonAmbientAoe> StepsAoes()
    {
        if (Arena == null || _actors.GetValueOrDefault(PrimaryId) is not { } boss) yield break;
        var player = _actors.GetValueOrDefault(PlayerId);
        // SDRect parameters are front, back, half-width, not the arena half-width/height order.
        if (player != null && stepsPhase == 1 && !player.InCombat && !HasStatus(player, 1268))
            yield return new("steps/advance", "前往南侧开战", new RectangleTemplate(80, 60),
                Arena.Position + Vector3.UnitZ * 8, Display: false);
        else if (player != null && (stepsPhase == 3 && player.Position.Z > 25 || stepsPhase == 5 && player.Position.Z > -135))
            yield return new("steps/advance", "进入下一区域", new RectangleTemplate(76, 62),
                Arena.Position + Vector3.UnitZ * 9, Display: false);
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 30887).Take(2))
            yield return new($"steps/earthshaker/{cast.Source}", "大地摇动", new FanTemplate(80, MathF.PI / 6),
                cast.Location, cast.Rotation, Activation: cast.FinishAt);
        foreach (var line in stepsLines)
            for (var i = 0; i < Math.Min(line.Left, 2); ++i)
                yield return new($"steps/earthrising/{line.Id}/{i}", "连续地火", new CircleTemplate(8),
                    QuantizedDungeonPosition(line.Next - Vector3.UnitZ * (7.5f * i)),
                    Activation: i == 0 ? line.NextAt : line.FutureAt,
                    Color: i == 0 ? new Vector4(1, 0, 0, 1) : null);
        if (stepsFlame != null) yield return stepsFlame;
        if (stepsFlame2 != null) yield return stepsFlame2;
        if (stepsCauterize)
        {
            if (Arena.Position.Z > 218)
                yield return new("steps/cauterize", "向南离开俯冲区域", new RectangleTemplate(44, 78), Arena.Position + Vector3.UnitZ);
            else
                yield return new("steps/cauterize", "俯冲", RayRectangleTemplate.Symmetric(44, 160), boss.Position, MathF.PI,
                    Activation: boss.Cast?.FinishAt ?? _now);
        }
        if (stepsScorching)
            yield return new("steps/scorching", "灼热吐息", new RectangleTemplate(20, 200), boss.Position, boss.Rotation,
                Activation: boss.Cast?.FinishAt ?? _now);
        foreach (var actor in _actors.Values.Where(a => !a.Dead && a.Targetable && a.Oid is 0x3AB3 or 0x3AAB or 0x3AB6 or 0x3AC2 or 0x3AB8))
        {
            if (_actors.GetValueOrDefault(actor.TargetId) is not { } target) continue;
            var rotation = MathF.Atan2(target.Position.X - actor.Position.X, target.Position.Z - actor.Position.Z);
            var origin = QuantizedDungeonPosition(actor.Position);
            yield return new($"steps/cleave/display/{actor.Id}", "雷光雨", new FanTemplate(6, MathF.PI * 2 / 3),
                origin, rotation, Navigate: false, OutlineOnly: true);
            if (target.Id != PlayerId)
                yield return new($"steps/cleave/avoid/{actor.Id}", "避开雷光雨", new FanTemplate(6, MathF.PI * 2 / 3),
                    origin, rotation, Display: false);
            else
                foreach (var member in _actors.Values.Where(a => a.PartySlot >= 0 && a.Id != PlayerId))
                    yield return new($"steps/cleave/party/{actor.Id}/{member.Id}", "避免雷光雨面向队友", new FanTemplate(100, MathF.PI * 2 / 3),
                        origin, MathF.Atan2(member.Position.X - actor.Position.X, member.Position.Z - actor.Position.Z), Display: false);
        }
        if (player != null)
            foreach (var cast in _casts.Values.Where(c => c.Rule.Action is 26401 or 26408))
            {
                if (KnockbackImmune(player, cast.FinishAt)) continue;
                if (cast.Rule.Action == 26408 && player.Position.X == cast.Location.X && player.Position.Z == cast.Location.Z) continue;
                var direction = cast.Rule.Action == 26401 ? cast.Rotation : MathF.Atan2(player.Position.X - cast.Location.X, player.Position.Z - cast.Location.Z);
                var distance = cast.Rule.Action == 26401 ? 20 : 10;
                var vector = new Vector3(MathF.Sin(direction), 0, MathF.Cos(direction));
                var end = player.Position + vector * distance;
                var wall = QueenEternalKnockbackGeometry.FirstBoundaryDistance(Arena, player.Position, end);
                var inset = MathF.Max(.5f - .499f, player.HitboxRadius - .499f);
                end = player.Position + vector * MathF.Min(distance, wall - inset);
                foreach (var arrow in DungeonMovementPreview($"steps/knockback/{cast.Source}", "停墙击退落点", player.Position, end)) yield return arrow;
            }
        // Include the primary even while untargetable, as in Vishap.DrawEnemies.
        yield return new("steps/boss", "维沙普", new CircleTemplate(boss.HitboxRadius), boss.Position,
            Navigate: false, OutlineOnly: true);
        foreach (var actor in _actors.Values.Where(a => !a.Dead && a.Targetable && ArrQuestCatalog.STEPS_ADDS.Contains(a.Oid)))
            yield return new($"steps/add/{actor.Id}", "龙群", new CircleTemplate(actor.HitboxRadius), actor.Position,
                Navigate: false, OutlineOnly: true);
    }

    private void ResetSteps()
    {
        stepsPhase = 1; stepsMinZ = 120; stepsMaxZ = 300;
        stepsLines.Clear(); stepsLineId = 0;
        stepsFlame = stepsFlame2 = null;
        stepsFlameHits = stepsFlame2Hits = 0;
        stepsScorching = stepsCauterize = false;
    }
}
