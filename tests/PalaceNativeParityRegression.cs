using System.Numerics;
using MasterPlayer.Mechanics.PalaceOfTheDead;
using MasterPlayer.Replay;
using PromeRotation.LogSystem;

internal static class PalaceNativeParityRegression
{
    private static readonly DateTime Start = new(2026, 11, 23, 0, 0, 0, DateTimeKind.Utc);
    private static Vector3 Center => PalaceArena.Center;
    private static ReplayActorState Actor(ulong id, uint oid, Vector3 position, uint kind = 2)
        => new() { Id = id, Oid = oid, EntityId = (uint)id, Position = position, Kind = (byte)kind, Targetable = true,
            InCombat = true, Hostile = kind != 1, Hp = 1000, MaxHp = 1000, HitboxRadius = .5f };
    private static PalaceCast Cast(ulong source, uint action, ulong target, Vector3 sourcePosition)
        => new(source, action, target, sourcePosition, sourcePosition, 0, Start, Start.AddSeconds(4));

    public static void Run(Action<string, Action> run)
    {
        run("Palace parity own Valfodr shows charge outline avoids allies and preserves landing constraint", Charge);
        run("Palace parity simultaneous opposing gazes select one direction safe against both", Gazes);
        run("Palace parity Yulunggu water pull represents entire expanded union complement", Pull);
        run("Palace parity Godfather small bombs lock birth positions and resolve as one wave", Bombs);
        run("Palace parity puddles consume one same-type boundary prediction per observation without timeout", Puddles);
        run("Palace parity cast snapshots preserve overdue boss casts and end on observation", ObservedCasts);
        run("Palace parity frozen water membership follows existing actors without admitting later pools", FrozenWater);
        run("Palace parity first registered lava bomb alone controls target ban", FirstBomb);
        run("Palace parity short observed casts cancel immediately while packet-only starts retain grace", ShortCasts);
        run("Palace parity final phase is monotonic stops rotation immediately and restores ongoing meteor", Phases);
        run("Palace parity knockback projections immunities directional bans and damage match distinct consumers", Knockbacks);
        run("Palace parity party bait excludes unrelated players and includes allied NPCs", PartyBait);
        run("Palace parity cancellation is issued once per observed player cast generation", CancelGeneration);
        run("Palace trash gaze raw Finish dynamic origin and overdue NPC clock", TrashRawGaze);
        run("Palace trash cone follows Actor rotation and retains original shapes across contiguous raw casts", TrashCone);
        run("Palace trash initial empty feed restores actual cast diffs and recreated Actor rejects old cancel", TrashLifecycle);
        run("Palace trash generic raw danger lasts until actual Finish and interrupt intent survives action change", TrashOrdinary);
        run("Palace trash Spikes raw slots refresh fixed ten seconds and either Lose clears the Actor", TrashSpikes);
        run("Palace trash public gaze follows original target freezes destroyed reference and replaces on raw change", GenericGaze);
        run("Palace trash source shape table cross fixed donut and zero-length charge match upstream geometry", TrashShapes);
        run("Palace trash Boss floor gates only Zone consumers and safe facing does not block casts", TrashGates);
        run("Palace early Zone excludes late Boss objects and effects while retaining trash protection", EarlyZoneIsolation);
    }

    private static void EarlyZoneIsolation()
    {
        var e = new PalaceEncounter { BossMechanicsEnabled = false };
        var player = Actor(100, 0, Center + Vector3.UnitZ * 5, 1);
        var tornado = Actor(20, 0x18F0, Center);
        var trap = Actor(21, 0x1EA08E, Center);
        var boss = Actor(10, 0x181D, Center);
        var source = Actor(30, 0x1890, Center) with
        {
            Cast = new(6953, 100, Center, 0, Start, Start.AddSeconds(4), false, false),
            Statuses = [new(197, 0, 30, Start.AddSeconds(30)) { SlotIndex = 4 }],
        };
        e.Synchronize([player, tornado, trap, boss, source], Start);
        e.StartCast(Cast(10, 7155, 100, Center));
        e.Apply(new LogSystemActionEffectEvent { SourceId = 10, TargetId = 100, ActionId = 7155,
            GlobalSequence = 1 }, Start);
        var areas = e.Collect(Start.AddSeconds(3.6), 100);
        Check(!e.BossActive && !areas.Any(a => a.Key.StartsWith("object/") || a.Action == 7155)
            && !e.PredictedDamage().Any(), "Early Zone inherited late Boss lifecycle, Tornado or effect predictions.");
        Check(areas.Any(a => a.Key == "trap/21") && areas.Any(a => a.Key.EndsWith("/eye"))
            && e.Facing(Start.AddSeconds(3.6), 100) != null
            && e.ForbiddenAutoAttackTargets(Start.AddSeconds(3.6)).Contains(30ul),
            "Disabling late Boss mechanics removed independent trap, gaze or Spikes consumers.");
        var raw = source.Cast! with { ActionId = 999, StartedAt = Start.AddSeconds(5), FinishAt = Start.AddSeconds(9) };
        e.Synchronize([player, tornado, trap, source with { Cast = raw }], Start.AddSeconds(5),
            resolveTrash: a => new(a.Id, a.Cast!.ActionId, 100, Center, a.Position, 0,
                a.Cast.StartedAt, a.Cast.FinishAt, new MasterPlayer.Spatial.SafeZone.CircleTemplate(6)));
        Check(e.Collect(Start.AddSeconds(5), 100).Any(a => a.Action == 999),
            "Early Zone lost ordinary raw AOE after Boss isolation.");
    }

    private static void GenericGaze()
    {
        var e = new PalaceEncounter();
        var source = Actor(10, 0x1890, Center) with { HitboxRadius = 2.2f };
        var target = Actor(200, 0, Center + Vector3.UnitZ * 7, 1);
        var viewer = Actor(100, 0, Center + Vector3.UnitZ * 12, 1);
        PalaceCast Resolve(ReplayActorState a) => new(a.Id, a.Cast!.ActionId, a.Cast.TargetId, a.Cast.Location,
            a.Position, a.Cast.Rotation, a.Cast.StartedAt, a.Cast.FinishAt,
            PalaceTrashSpell.Shape(3, 6, 0, a.HitboxRadius, "fan090"), GenericGaze: true);
        e.Synchronize([target, source, viewer], Start, resolveTrash: Resolve);
        var raw = new ReplayCast(6356, 200, Center, 0, Start, Start.AddSeconds(4), false, false);
        e.Synchronize([target, source with { Cast = raw }, viewer], Start.AddSeconds(.1), resolveTrash: Resolve);
        var markers = e.Collect(Start.AddSeconds(3.5), 100);
        Check(markers.Single(a => a.Key.EndsWith("/eye")).Position == target.Position
            && e.Facing(Start.AddSeconds(3.5), 100) != null,
            "Public gaze was lost outside the Zone's caster-centered cone.");
        e.Apply(new LogSystemInterruptCastEvent { SourceId = 10, ActionId = 6356 }, Start.AddSeconds(3.6));
        Check(e.Facing(Start.AddSeconds(3.6), 100) != null, "Interrupt log incorrectly finished an observed raw gaze.");
        e.Synchronize([source with { Cast = raw }, viewer], Start.AddSeconds(3.7), resolveTrash: Resolve);
        e.Synchronize([target with { Position = Center - Vector3.UnitZ * 100 }, source with { Cast = raw }, viewer],
            Start.AddSeconds(3.8), resolveTrash: Resolve);
        Check(e.Collect(Start.AddSeconds(3.8), 100).Single(a => a.Key.EndsWith("/eye")).Position == target.Position,
            "Destroyed original target rebound to a recreated Actor with the same ID.");
        e.Synchronize([target, source with { Cast = raw with { ActionId = 999, TargetId = 0,
            StartedAt = Start.AddSeconds(4), FinishAt = Start.AddSeconds(8) } }, viewer], Start.AddSeconds(4),
            resolveTrash: a => Resolve(a) with { Shape = null, GenericGaze = false });
        Check(e.Facing(Start.AddSeconds(7.5), 100) == null,
            "Public gaze inherited the Zone's persistence across contiguous cast changes.");
    }

    private static void TrashShapes()
    {
        foreach (var type in new byte[] { 2, 3, 4, 5, 10, 11, 12, 13 })
        {
            var native = PalaceTrashSpell.Shape(type, 16, 6, 2.2f, "fan090")!;
            BossMod.AOEShape source = type switch
            {
                2 => new BossMod.AOEShapeCircle(16),
                3 => new BossMod.AOEShapeCone(18.2f, new(MathF.PI / 4)),
                4 => new BossMod.AOEShapeRect(18.2f, 3),
                5 => new BossMod.AOEShapeCircle(18.2f),
                10 => new BossMod.AOEShapeDonut(3, 16),
                11 => new BossMod.AOEShapeCross(16, 3),
                12 => new BossMod.AOEShapeRect(16, 3),
                _ => new BossMod.AOEShapeCone(16, new(MathF.PI / 4)),
            };
            for (var x = -40; x <= 40; ++x)
                for (var z = -40; z <= 40; ++z)
                {
                    var point = new Vector3(x * .51f, 0, z * .49f);
                    Check(MasterPlayer.Mechanics.Dungeons.DungeonSourceGeometry.Contains(native, Vector3.Zero, .371f, point)
                        == source.Check(new(point.X, point.Z), default, new(.371f)),
                        $"Upstream guessed shape mismatch type={type} point={point}.");
                }
        }
        var zero = PalaceTrashSpell.Shape(8, 0, 4, 0, null);
        Check(zero is MasterPlayer.Spatial.SafeZone.RayRectangleTemplate { Length: 0 },
            "A zero-length charge lost the descriptor needed after the caster moves.");
    }

    private static void TrashSpikes()
    {
        var e = new PalaceEncounter();
        var source = Actor(10, 0x1890, Center) with
        {
            Statuses = [new(197, 0, 10, Start.AddSeconds(30)) { SlotIndex = 2 },
                new(999, 0, 10, Start.AddSeconds(30)) { SlotIndex = 4 }],
        };
        e.Synchronize([source], Start);
        Check(e.ForbiddenAutoAttackTargets(Start.AddSeconds(9.9)).Contains(10ul)
            && !e.ForbiddenAutoAttackTargets(Start.AddSeconds(10)).Contains(10ul), "Spikes used status expiry instead of fixed ten seconds.");
        var refreshed = source with { Statuses = [source.Statuses[0] with { ExpiresAt = Start.AddSeconds(40) }, source.Statuses[1]] };
        e.Synchronize([refreshed], Start.AddSeconds(8));
        Check(e.ForbiddenAutoAttackTargets(Start.AddSeconds(17.9)).Contains(10ul), "Raw status refresh failed to extend Spikes.");
        e.Synchronize([refreshed with { Statuses = [refreshed.Statuses[0]] }], Start.AddSeconds(9));
        Check(e.ForbiddenAutoAttackTargets(Start.AddSeconds(17.9)).Contains(10ul), "Removing another native slot removed Spikes.");
        var both = refreshed with { Statuses = [refreshed.Statuses[0],
            new(198, 0, 10, Start.AddSeconds(50)) { SlotIndex = 4 }] };
        e.Synchronize([both], Start.AddSeconds(10));
        e.Synchronize([both with { Statuses = [both.Statuses[1]] }], Start.AddSeconds(11));
        Check(!e.ForbiddenAutoAttackTargets(Start.AddSeconds(11)).Contains(10ul),
            "Lose197 must clear all source Spikes even with198 still present.");
        e.Synchronize([both with { Statuses = [both.Statuses[1]] }], Start.AddSeconds(12));
        Check(!e.ForbiddenAutoAttackTargets(Start.AddSeconds(12)).Contains(10ul), "Unchanged198 snapshot rearmed removed Spikes.");
        e.Apply(new LogSystemStatusAddEvent { TargetId = 10, StatusId = 198 }, Start.AddSeconds(13));
        e.Synchronize([both with { Statuses = [both.Statuses[1]] }], Start.AddSeconds(14));
        Check(e.ForbiddenAutoAttackTargets(Start.AddSeconds(22.9)).Contains(10ul)
            && !e.ForbiddenAutoAttackTargets(Start.AddSeconds(23)).Contains(10ul), "Acknowledging raw extended packet Gain clock.");
        e.Apply(new LogSystemStatusRemoveEvent { TargetId = 10, StatusId = 198 }, Start.AddSeconds(15));
        e.Synchronize([both with { Statuses = [both.Statuses[1] with { ExpiresAt = Start.AddSeconds(60) }] }], Start.AddSeconds(16));
        Check(e.ForbiddenAutoAttackTargets(Start.AddSeconds(25.9)).Contains(10ul)
            && !e.ForbiddenAutoAttackTargets(Start.AddSeconds(26)).Contains(10ul),
            "A packet Lose suppressed the source feed's later genuine raw refresh.");
        e = new();
        e.Synchronize([source], Start);
        var zero = source with { Statuses = [source.Statuses[0] with { ExpiresAt = DateTime.MaxValue, ZeroRemainingTime = true }] };
        e.Synchronize([zero], Start.AddSeconds(9));
        Check(!e.ForbiddenAutoAttackTargets(Start.AddSeconds(10)).Contains(10ul),
            "A real zero remaining value became a false raw Gain through MaxValue normalization.");
        e.Synchronize([zero], Start.AddSeconds(9.1));
        Check(!e.ForbiddenAutoAttackTargets(Start.AddSeconds(11)).Contains(10ul), "Repeated zero remaining rearmed Spikes.");
    }

    private static void TrashRawGaze()
    {
        var e = new PalaceEncounter();
        var source = Actor(10, 0x1890, Center);
        var viewer = Actor(100, 0, Center + Vector3.UnitZ * 29, 1) with { Rotation = MathF.PI };
        e.Synchronize([source, viewer], Start);
        var cast = new ReplayCast(6953, 100, Center, 0, Start, Start.AddSeconds(4), false, false) { TotalTime = 3.7f };
        e.Synchronize([source with { Cast = cast }, viewer], Start.AddSeconds(.1));
        Check(e.Facing(Start.AddSeconds(3.5), 100) != null, "Observed gaze has no facing.");
        Check(e.Collect(Start.AddSeconds(3.5), 100).Count(a => a.Key.StartsWith("trash/gaze/")) == 3,
            "Gaze must publish eye/pupil/view through the common dual-view state.");
        e.Synchronize([source with { Position = Center - Vector3.UnitZ * 2, Cast = cast }, viewer], Start.AddSeconds(3.6));
        Check(e.Facing(Start.AddSeconds(3.6), 100) == null, "Gaze used the old origin instead of current Actor position.");
        e.Synchronize([source with { Cast = cast }, viewer], Start.AddSeconds(8));
        Check(e.Facing(Start.AddSeconds(8), 100) != null && e.CastRestrictions(Start.AddSeconds(8), 100, true).BlockCasts,
            "A still-present raw cast expired at its original prediction.");
        var eye = e.Collect(Start.AddSeconds(8), 100).Single(a => a.Key.EndsWith("/eye"));
        Check(Math.Abs((eye.From - Start.AddSeconds(8)).TotalSeconds - ((3.7f + .3f) - 3.7f)) < 1e-6,
            "NPCRemainingTime must saturate elapsed at raw TotalTime.");
        e.Synchronize([source, viewer], Start.AddSeconds(8.1));
        Check(e.Facing(Start.AddSeconds(8.1), 100) == null
            && !e.Collect(Start.AddSeconds(8.1), 100).Any(a => a.Key.StartsWith("trash/gaze/")),
            "Raw null without a packet failed to revoke gaze controls and drawing.");
    }

    private static void TrashCone()
    {
        var e = new PalaceEncounter();
        var source = Actor(10, 0x1890, Center);
        var viewer = Actor(100, 0, Center + Vector3.UnitZ * 5, 1);
        e.Synchronize([source, viewer], Start);
        var cast = new ReplayCast(6356, 100, Center, 0, Start, Start.AddSeconds(3), false, false);
        e.Synchronize([source with { Cast = cast }, viewer], Start.AddSeconds(.1));
        Check(e.Facing(Start.AddSeconds(2.5), 100) != null, "Cone did not activate.");
        e.Synchronize([source with { Cast = cast, Rotation = MathF.PI }, viewer], Start.AddSeconds(2.6));
        Check(e.Facing(Start.AddSeconds(2.6), 100) == null, "Cone locked packet rotation instead of Actor rotation.");
        var second = cast with { ActionId = 999, StartedAt = Start.AddSeconds(3), FinishAt = Start.AddSeconds(6) };
        e.Synchronize([source with { Cast = second }, viewer], Start.AddSeconds(3));
        Check(e.Facing(Start.AddSeconds(5.5), 100) != null,
            "Finished callback followed by Start with no intervening null incorrectly expired the source gaze list.");
        e.Synchronize([source, viewer], Start.AddSeconds(6));
        Check(e.Facing(Start.AddSeconds(6), 100) == null, "Contiguous gaze did not expire on observed null.");
    }

    private static void TrashLifecycle()
    {
        var e = new PalaceEncounter();
        var source = Actor(10, 0x1890, Center);
        var viewer = Actor(100, 0, Center + Vector3.UnitZ * 5, 1);
        var cast = new ReplayCast(6953, 100, Center, 0, Start, Start.AddSeconds(4), false, false);
        e.Synchronize([source with { Cast = cast }, viewer], Start.AddSeconds(1));
        Check(e.Facing(Start.AddSeconds(3.5), 100) != null, "Initial empty actor feed lost its CastInfo diff.");
        e.StartCast(Cast(10, 6953, 100, Center));
        Check(e.Facing(Start.AddSeconds(3.5), 100) != null, "Actual packet following initial raw was lost.");
        e.RemoveActor(10);
        var next = cast with { StartedAt = Start.AddSeconds(5), FinishAt = Start.AddSeconds(9) };
        e.Synchronize([source with { Cast = next }, viewer], Start.AddSeconds(5.1));
        e.Apply(new LogSystemCancelCastEvent { SourceId = 10, ActionId = 6953 }, Start.AddSeconds(1));
        Check(e.Facing(Start.AddSeconds(8.5), 100) != null, "Old cancel cleared a recreated source.");
        e.Apply(new LogSystemCancelCastEvent { SourceId = 10, ActionId = 6953 }, Start.AddSeconds(8.6));
        e.Synchronize([source with { Cast = next }, viewer], Start.AddSeconds(8.7));
        Check(e.Facing(Start.AddSeconds(8.7), 100) != null, "Cancel log finished a still-present raw generation.");
        e.Synchronize([source, viewer], Start.AddSeconds(8.8));
        Check(e.Facing(Start.AddSeconds(8.8), 100) == null, "Raw null retained the cancelled generation.");
        e.StartCast(Cast(10, 6953, 100, Center) with { Start = next.StartedAt, Hit = next.FinishAt });
        Check(e.Facing(Start.AddSeconds(8.8), 100) == null, "A delayed packet revived a genuinely finished raw generation.");
        source = source with { GameObjectId = 10010, ObjectIndex = 5, Cast = next };
        e.Synchronize([source, viewer], Start.AddSeconds(9));
        Check(e.Facing(Start.AddSeconds(9), 100) != null, "Known original generation lost its gaze.");
        e.Synchronize([source with { GameObjectId = 20010, ObjectIndex = 6,
            Cast = next with { ActionId = 999, StartedAt = Start.AddSeconds(9), FinishAt = Start.AddSeconds(13) } }, viewer],
            Start.AddSeconds(9.1));
        Check(e.Facing(Start.AddSeconds(12.5), 100) == null, "Known same-ID same-OID recreated Actor inherited old Zone lists.");
        e.Reset();
        Check(!e.Collect(Start.AddSeconds(8.8), 100).Any(), "Owner/reset retained trash output.");
    }

    private static void TrashOrdinary()
    {
        var e = new PalaceEncounter();
        var source = Actor(10, 0x1890, Center);
        var viewer = Actor(100, 0, Center, 1);
        PalaceCast Resolve(ReplayActorState a) => new(a.Id, a.Cast!.ActionId, 100, Center, Center, 0,
            a.Cast.StartedAt, a.Cast.FinishAt, new MasterPlayer.Spatial.SafeZone.CircleTemplate(6));
        e.Synchronize([source, viewer], Start, resolveTrash: Resolve);
        var raw = new ReplayCast(6386, 100, Center, 0, Start, Start.AddSeconds(4), true, false);
        e.Synchronize([source with { Cast = raw }, viewer], Start.AddSeconds(.1), resolveTrash: Resolve);
        e.Apply(new LogSystemActionEffectEvent { SourceId = 10, ActionId = 6386, GlobalSequence = 1 }, Start.AddSeconds(4));
        Check(e.Collect(Start.AddSeconds(7), 100).Any(a => a.Action == 6386)
            && e.Hints(Start.AddSeconds(7), 100).Any(t => t.StartsWith("打断")),
            "Effect or overdue prediction consumed raw generic danger/interrupt intent.");
        e.Synchronize([source with { Cast = raw with { ActionId = 999, StartedAt = Start.AddSeconds(8),
            FinishAt = Start.AddSeconds(12) } }, viewer], Start.AddSeconds(8), resolveTrash: Resolve);
        Check(e.InterruptCandidates.Single().Cast!.ActionId == 999, "Source interrupt list was prematurely cleared on action change.");
        e.Synchronize([source, viewer], Start.AddSeconds(9), resolveTrash: Resolve);
        Check(!e.InterruptCandidates.Any() && !e.Collect(Start.AddSeconds(9), 100).Any(a => a.Action != 0),
            "Observed raw Finish failed to clear ordinary mechanics.");

        e = new();
        PalaceCast Charge(ReplayActorState a) => new(a.Id, a.Cast!.ActionId, 100, a.Cast.Location, a.Position, 0,
            a.Cast.StartedAt, a.Cast.FinishAt, PalaceTrashSpell.Shape(8, 0, 4, 0, null),
            Charge: true, CastExtra: .7f);
        e.Synchronize([source, viewer], Start, resolveTrash: Charge);
        var quantized = new BossMod.WPos(Center.X, Center.Z).Quantized();
        raw = raw with { ActionId = 998, Location = new(quantized.X, Center.Y, quantized.Z) };
        e.Synchronize([source with { Cast = raw }, viewer], Start.AddSeconds(.05), resolveTrash: Charge);
        var sourceRect = new BossMod.SDRect(new(Center.X, Center.Z), new(Center.X, Center.Z), 2);
        Check(!float.IsFinite(sourceRect.Distance(new(Center.X, Center.Z)))
            && !e.Collect(Start.AddSeconds(.05), 100).Any(a => a.Action == 998),
            "Degenerate source charge invented finite navigation or threw in a geometry consumer.");
        raw = raw with { ActionId = 998, Location = Center + Vector3.UnitZ * 10 };
        e.Synchronize([source with { Cast = raw }, viewer], Start.AddSeconds(.1), resolveTrash: Charge);
        var moved = source with { Position = Center + Vector3.UnitX * 10, Cast = raw };
        e.Synchronize([moved, viewer], Start.AddSeconds(1), resolveTrash: Charge);
        var area = e.Collect(Start.AddSeconds(8), 100).Single(a => a.Action == 998);
        Check(Vector3.Distance(area.Position, moved.Position) < .03f
            && area.Geometry.Contains(Center + new Vector3(5, 0, 5)),
            "Charge did not recompute quantized current caster to current raw destination.");
        Check(Math.Abs((area.From - Start.AddSeconds(8)).TotalSeconds + .4) < 1e-6,
            "AdjustedTotalTime must preserve a negative NPCRemainingTime rather than clamp it to zero.");
    }

    private static void TrashGates()
    {
        var e = new PalaceEncounter();
        var viewer = Actor(100, 0, Center, 1) with { Rotation = 0 };
        var source = Actor(10, 0x1890, Center) with
        {
            Cast = new(6351, 100, Center, 0, Start, Start.AddSeconds(4), true, false),
            Statuses = [new(197, 0, 10, Start.AddSeconds(30))],
        };
        e.Synchronize([source, viewer], Start);
        var eye = e.Collect(Start.AddSeconds(3.6), 100).Single(a => a.Key.EndsWith("/eye"));
        Check(eye.Color?.X == 1 && e.CastRestrictions(Start.AddSeconds(3.6), 100, true).BlockCasts,
            "Zero-distance Zone gaze marker disagrees with the source's Atan2(0,0) facing.");
        e.Synchronize([source, viewer with { Rotation = MathF.PI }], Start.AddSeconds(.1));
        Check(!e.CastRestrictions(Start.AddSeconds(3.6), 100, true).BlockCasts,
            "Safe-facing gaze blocked unrelated healing casts.");
        e.ZoneMechanicsEnabled = false;
        Check(!e.Collect(Start.AddSeconds(3.6), 100).Any(a => a.Key.StartsWith("trash/gaze/"))
            && e.ForbiddenAutoAttackTargets(Start.AddSeconds(3.6)).Count == 0,
            "Boss floor kept consuming Zone gaze or Spikes.");
        e = new() { ZoneMechanicsEnabled = false };
        PalaceCast Resolve(ReplayActorState a) => new(a.Id, a.Cast!.ActionId, 100, Center, Center, 0,
            a.Cast.StartedAt, a.Cast.FinishAt, new MasterPlayer.Spatial.SafeZone.CircleTemplate(6), GenericGaze: true);
        e.Synchronize([source, viewer], Start, resolveTrash: Resolve);
        Check(e.Facing(Start.AddSeconds(3.6), 100) != null,
            "Boss floor without active Boss suppressed the independent public gaze.");
        e = new() { ZoneMechanicsEnabled = false };
        e.Synchronize([source, viewer], Start);
        e.Synchronize([source with { Cast = null }, viewer], Start.AddSeconds(.1));
        e.Synchronize([source with { Cast = source.Cast! with { ActionId = 999,
            StartedAt = Start.AddSeconds(1), FinishAt = Start.AddSeconds(5) } }, viewer], Start.AddSeconds(1));
        e.ZoneMechanicsEnabled = true;
        Check(e.Facing(Start.AddSeconds(4.6), 100) != null,
            "An unconsumed Zone list expired while its consumer was gated, unlike AutoClear.IterAndExpire.");
        e.Synchronize([source with { Cast = null }, viewer], Start.AddSeconds(5));
        Check(e.Facing(Start.AddSeconds(5), 100) == null,
            "Restored Zone consumer retained its list after genuine raw null.");
        e = new();
        var chirp = source with { Cast = source.Cast! with { ActionId = 6365 } };
        PalaceCast ChargeShape(ReplayActorState a) => new(a.Id, a.Cast!.ActionId, 100, Center + Vector3.UnitZ * 10,
            Center, 0, a.Cast.StartedAt, a.Cast.FinishAt,
            MasterPlayer.Spatial.SafeZone.RayRectangleTemplate.Symmetric(6, 10), Charge: true);
        e.Synchronize([chirp, viewer], Start);
        e.ZoneMechanicsEnabled = false;
        var charge = chirp with { Cast = chirp.Cast! with { ActionId = 999, Location = Center + Vector3.UnitZ * 10 } };
        e.Synchronize([charge, viewer], Start.AddSeconds(.1), resolveTrash: ChargeShape);
        Check(e.Collect(Start.AddSeconds(.2), 100).Any(a => a.Action == 999),
            "A gated Zone HintDisabled entry removed an independent public charge.");
        e.ZoneMechanicsEnabled = true;
        Check(!e.Collect(Start.AddSeconds(.2), 100).Any(a => a.Action == 999),
            "Restoring Zone consumption lost its still-live HintDisabled entry.");
        e.Synchronize([source with { Cast = null }, viewer], Start.AddSeconds(.3));
        // No consumer runs between null and a new non-null cast.
        e.Synchronize([charge, viewer], Start.AddSeconds(.4), resolveTrash: ChargeShape);
        Check(!e.Collect(Start.AddSeconds(.5), 100).Any(a => a.Action == 999),
            "Scanning expired the Zone list before IterAndExpire was consumed.");
        e.Synchronize([source with { Cast = null }, viewer], Start.AddSeconds(.6));
        _ = e.Collect(Start.AddSeconds(.6), 100);
        e.Synchronize([charge, viewer], Start.AddSeconds(.7), resolveTrash: ChargeShape);
        Check(e.Collect(Start.AddSeconds(.7), 100).Any(a => a.Action == 999),
            "A consumed raw-null entry still disabled the next public charge.");
    }

    private static void Charge()
    {
        var e = new PalaceEncounter();
        e.Party = [new(0, "", 100), new(1, "", 101)];
        e.Synchronize([Actor(10, 0x181D, Center) with { TargetId = 100 }, Actor(100, 0, Center + Vector3.UnitZ * 10, 1),
            Actor(101, 0, Center + Vector3.UnitZ * 5, 1)], Start);
        e.StartCast(Cast(10, 7156, 100, Center));
        var areas = e.Collect(Start, 100);
        Check(areas.Any(a => a.Action == 7156 && a.PreviewOnly && a.OutlineOnly), "Own charge outline missing.");
        var bait = areas.Single(a => a.Key == "charge-bait/10/101");
        Check(bait.Geometry.Contains(Center + Vector3.UnitZ * 10) && !bait.Geometry.Contains(Center + Vector3.UnitX * 10),
            "Charge bait sector did not exclude the ally direction.");
        Check(areas.Any(a => a.Key == "landing/10" && a.NavigationOnly), "Adding ally avoidance removed landing safety.");
        e.Cancel(10);
        Check(!e.Collect(Start, 100).Any(a => a.Action == 7156), "Cancelled charge retained personal regions or outline.");
        e.Reset();
        e.Party = [new(0, "", 100), new(1, "", 101)];
        e.Synchronize([Actor(10, 0x181D, Center) with { TargetId = 100 }, Actor(100, 0, Center - Vector3.UnitZ * 10, 1),
            Actor(101, 0, Center + Vector3.UnitZ * 3, 1)], Start);
        e.StartCast(Cast(10, 7156, 100, Center));
        bait = e.Collect(Start, 100).Single(a => a.Key == "charge-bait/10/101");
        Check(!bait.Geometry.Contains(Center - Vector3.UnitZ * 10) && bait.Geometry.Contains(Center + Vector3.UnitZ * 10),
            "An ally exactly three yalms away incorrectly forbade the full circle.");
        e.Synchronize([Actor(10, 0x181D, Center + Vector3.UnitX * 10) with { TargetId = 100,
                Cast = new(7156, 100, Center, 0, Start, Start.AddSeconds(4), false, false) },
            Actor(100, 0, Center + Vector3.UnitZ * 5, 1), Actor(101, 0, Center + new Vector3(5, 0, 12), 1)], Start.AddSeconds(1));
        var landing = e.Collect(Start.AddSeconds(1), 100).Single(a => a.Key == "landing/10");
        Check(!landing.Geometry.Contains(Center + Vector3.UnitZ * 5), "Bait direction was incorrectly treated as damage at the landing point.");
    }

    private static void Gazes()
    {
        var e = new PalaceEncounter();
        var north = Center + Vector3.UnitZ * 10;
        var south = Center - Vector3.UnitZ * 10;
        e.Synchronize([Actor(10, 0x1890, north), Actor(11, 0x1890, south), Actor(100, 0, Center, 1)], Start);
        e.StartCast(Cast(10, 6953, 100, north));
        e.StartCast(Cast(11, 6953, 100, south));
        var facing = e.Facing(Start.AddSeconds(3.5), 100);
        Check(facing != null && new[] { 0f, MathF.PI }.All(a =>
            MathF.Abs(MathF.Atan2(MathF.Sin(facing.Value - a), MathF.Cos(facing.Value - a))) > MathF.PI / 4),
            "Facing avoided only the first gaze.");
        Check(e.Facing(Start.AddSeconds(3), 100) == null, "Gaze turned earlier than its active window.");
        e.Cancel(10);
        e.Cancel(11);
        Check(e.Facing(Start.AddSeconds(3.5), 100) == null, "Cancelled gazes retained facing.");
        var directions = new[] { 0f, 1.5957963268f, 3.1582593203f, 4.7207223137f };
        var sources = directions.Select((d, i) => Actor((ulong)(20 + i), 0x1890,
            Center + new Vector3(MathF.Sin(d), 0, MathF.Cos(d)) * 10)).ToArray();
        e.Synchronize(sources.Append(Actor(100, 0, Center, 1)), Start);
        foreach (var source in sources) e.StartCast(Cast(source.Id, 6953, 100, source.Position));
        facing = e.Facing(Start.AddSeconds(3.5), 100);
        Check(facing != null && directions.All(d => MathF.Abs(MathF.Atan2(MathF.Sin(facing.Value - d),
            MathF.Cos(facing.Value - d))) > MathF.PI / 4 + .01f), "A narrow valid gap between four gazes was missed.");
    }

    private static void Pull()
    {
        var e = new PalaceEncounter();
        e.Synchronize([Actor(10, 0x181E, Center) with { TargetId = 100, HitboxRadius = 5.75f },
            Actor(100, 0, Center - Vector3.UnitZ * 15, 1), Actor(20, 0x1E9998, Center)], Start);
        var areas = e.Collect(Start, 100);
        var goal = areas.Single(a => a.Key == "pull/170");
        const float radius = 8 + 2 * 5.75f + 3;
        for (var x = -25; x <= 25; ++x)
            for (var z = -25; z <= 25; ++z)
            {
                var point = Center + new Vector3(x, 0, z);
                Check(goal.Geometry.Contains(point) == (PalaceArena.Boundary.Contains(point) && x * x + z * z > radius * radius),
                    "Pull region differs from the source union complement.");
            }
        Check(areas.Any(a => a.Key == "puddle-boss/10" && a.PreviewOnly && a.OutlineOnly)
            && e.Hints(Start, 100).Contains("把 Boss 拉出水圈"), "Boss-in-water drawing or hint missing.");
        Check(!goal.OverrideDangerMargin && !e.Hints(Start, 101).Contains("把 Boss 拉出水圈")
            && !e.Collect(Start, 101).Any(a => a.Key == "pull/170"), "Water preference changed margin policy or leaked to another player.");
    }

    private static void Bombs()
    {
        var e = new PalaceEncounter();
        var first = Actor(20, 0x18F3, Center);
        var second = Actor(21, 0x18F3, Center + Vector3.UnitX * 10);
        e.Synchronize([Actor(10, 0x1820, Center), first, second], Start);
        e.Synchronize([Actor(10, 0x1820, Center), first with { Position = Center + Vector3.UnitZ * 10 }, second], Start.AddSeconds(5));
        Check(Vector3.Distance(e.Collect(Start.AddSeconds(5), 100).Single(a => a.Key == "bomb/20").Position, Center) < .03,
            "Bomb prediction followed movement after birth.");
        e.Apply(new LogSystemActionEffectEvent { SourceId = 20, ActionId = 7172, GlobalSequence = 1 }, Start.AddSeconds(10));
        Check(e.Collect(Start.AddSeconds(30), 100).Count(a => a.Key.StartsWith("bomb/")) == 2,
            "Action effect or elapsed clock ended birth predictions before observed cast finish.");
        e.StartCast(Cast(20, 7172, 0, first.Position));
        e.Cancel(20, 7172);
        Check(!e.Collect(Start.AddSeconds(30), 100).Any(a => a.Key.StartsWith("bomb/")), "Finishing a small bomb retained part of its source wave.");
        e.Reset();
        e.Synchronize([Actor(10, 0x1820, Center), first, second], Start);
        e.StartCast(Cast(20, 7172, 0, first.Position));
        e.StartCast(Cast(21, 7172, 0, second.Position));
        e.Cancel(20, 7172);
        Check(!e.Collect(Start.AddSeconds(4), 100).Any(a => a.Action == 7172), "Resolving the bomb wave retained another bomb's cast region.");
        e.Reset();
        e.Synchronize([Actor(10, 0x1820, Center), first, second], Start);
        e.StartCast(Cast(20, 7172, 0, first.Position));
        e.RemoveActor(20);
        Check(!e.Collect(Start, 100).Any(a => a.Key.StartsWith("bomb/")), "Destroying a casting bomb failed to finish the entire wave.");
        e.Reset();
        var observed = new ReplayCast(7172, 0, Center, 0, Start, Start.AddSeconds(1.5), false, false);
        e.Synchronize([Actor(10, 0x1820, Center), first with { Cast = observed }, second], Start);
        e.Synchronize([Actor(10, 0x1820, Center), first with { Cast = observed, Dead = true }, second], Start.AddSeconds(.1));
        Check(e.Collect(Start, 100).Count(a => a.Key.StartsWith("bomb/")) == 2, "Death without cast finish cleared the wave.");
        e.Synchronize([Actor(10, 0x1820, Center), first with { Cast = observed with { Rotation = 1 } }, second], Start.AddSeconds(.2));
        Check(!e.Collect(Start, 100).Any(a => a.Key.StartsWith("bomb/")), "Replacing a bomb cast failed to finish its previous wave.");
    }

    private static void Puddles()
    {
        var e = new PalaceEncounter();
        var boss = Actor(10, 0x181D, Center);
        var player = Actor(100, 0, Center, 1);
        e.Synchronize([boss, player], Start);
        for (uint i = 1; i <= 2; ++i)
            e.Apply(new LogSystemActionEffectEvent { SourceId = 10, TargetId = 100, ActionId = 7155, GlobalSequence = i }, Start);
        e.Apply(new LogSystemActionEffectEvent { SourceId = 10, TargetId = 100, ActionId = 7158, GlobalSequence = 3 }, Start);
        var zone = Actor(20, 0x1E858E, Center + Vector3.UnitX * 2) with { Dead = true, Targetable = false };
        e.Synchronize([boss, player, zone], Start.AddSeconds(30));
        var areas = e.Collect(Start.AddSeconds(30), 100);
        Check(areas.Count(a => a.Key.StartsWith("puddle/") && a.Action == 7155) == 1
            && areas.Count(a => a.Key.StartsWith("puddle/") && a.Action == 7158) == 1
            && areas.Any(a => a.Key == "object/20"), "Handoff lost <=2 boundary, one-item/type separation, no TTL or raw dead source.");
        e.Synchronize([boss, player, zone], Start.AddSeconds(31));
        Check(e.Collect(Start.AddSeconds(31), 100).Count(a => a.Key.StartsWith("puddle/")) == 1,
            "Same live source failed to consume the next prediction on the next update.");
    }

    private static void ObservedCasts()
    {
        var e = new PalaceEncounter();
        var player = Actor(100, 0, Center, 1);
        var cast = new ReplayCast(7170, 0, Center, 0, Start, Start.AddSeconds(5.3), false, false);
        var boss = Actor(10, 0x1820, Center);
        var bomb = Actor(20, 0x18F2, Center) with { Cast = cast };
        e.Synchronize([boss, bomb, player], Start);
        e.Synchronize([boss with { InCombat = false }, bomb, player], Start.AddSeconds(30));
        Check(e.BossActive && e.Collect(Start.AddSeconds(30), 100).Any(a => a.Action == 7170),
            "Boss disengage or predicted clock cleared an observed cast.");
        e.Synchronize([boss, bomb with { Cast = null }, player], Start.AddSeconds(31));
        Check(!e.Collect(Start.AddSeconds(31), 100).Any(a => a.Action == 7170), "Observed end retained cast.");
    }

    private static void FrozenWater()
    {
        var e = new PalaceEncounter();
        var boss = Actor(10, 0x181E, Center) with { TargetId = 100, HitboxRadius = 5.75f };
        var player = Actor(100, 0, Center, 1);
        e.Synchronize([boss, player], Start);
        var pool = Actor(20, 0x1E9998, Center);
        e.Synchronize([boss, player, pool], Start.AddSeconds(1));
        Check(!e.Collect(Start.AddSeconds(1), 100).Any(a => a.Key.StartsWith("puddle-boss/") || a.Key == "pull/170"),
            "A pool born after component activation entered frozen membership.");
        e.Reset();
        e.Synchronize([boss, player, pool with { Position = Center + Vector3.UnitX * 20 }], Start);
        e.Synchronize([boss, player, pool with { EventState = 7 }], Start.AddSeconds(1));
        Check(e.Collect(Start.AddSeconds(1), 100).Any(a => a.Key == "pull/170"),
            "Existing pool reference failed to follow position or was re-filtered by EventState.");
        e.Synchronize([boss, player], Start.AddSeconds(2));
        e.Synchronize([boss, player, pool with { Position = Center + Vector3.UnitX * 22 }], Start.AddSeconds(3));
        Check(e.Collect(Start.AddSeconds(3), 100).Any(a => a.Key == "pull/170"),
            "A recycled entity id replaced the frozen destroyed object reference.");
    }

    private static void FirstBomb()
    {
        var e = new PalaceEncounter();
        var boss = Actor(10, 0x1820, Center);
        var first = Actor(20, 0x18F2, Center + Vector3.UnitX * 10);
        var second = Actor(21, 0x18F2, Center);
        e.Synchronize([boss, first, second], Start);
        Check(e.ForbiddenTargets(Start).Count == 0, "Second bomb was banned despite first registered bomb outside radius.");
        e.RemoveActor(20);
        Check(e.ForbiddenTargets(Start).SequenceEqual([21ul]), "Destruction did not promote the first registered bomb.");
        e.Synchronize([boss, first, second], Start.AddSeconds(1));
        Check(e.ForbiddenTargets(Start).SequenceEqual([21ul]), "Dictionary slot reuse reordered registered bombs.");
        e.Reset();
        e.Synchronize([boss, second], Start);
        Check(e.ForbiddenTargets(Start).SequenceEqual([21ul]), "Reset retained a stale registered bomb.");
        e.Synchronize([boss, second with { Dead = true }], Start.AddSeconds(1));
        Check(!e.Collect(Start.AddSeconds(1), 100).Any(a => a.Key == "enemy/21"), "Dead add retained directional marker.");
    }

    private static void ShortCasts()
    {
        var e = new PalaceEncounter();
        var observed = new ReplayCast(7161, 0, Center, 0, Start, Start.AddSeconds(3), false, false);
        var boss = Actor(10, 0x181E, Center);
        e.Synchronize([boss with { Cast = observed }], Start);
        e.Synchronize([boss], Start.AddSeconds(.1));
        Check(!e.Collect(Start.AddSeconds(.1), 100).Any(a => a.Action == 7161), "A known cast survived a short observed cancellation.");
        e.StartCast(Cast(10, 7161, 0, Center));
        Check(!e.Collect(Start.AddSeconds(.1), 100).Any(a => a.Action == 7161), "A stale start resurrected a canceled generation.");
        e.Synchronize([boss with { Cast = observed with { StartedAt = Start.AddSeconds(.05) } }], Start.AddSeconds(.15));
        Check(!e.Collect(Start.AddSeconds(.15), 100).Any(a => a.Action == 7161), "A drifting stale observation resurrected a canceled generation.");
        e.StartCast(Cast(10, 7161, 0, Center) with { Start = Start.AddSeconds(1), Hit = Start.AddSeconds(4) });
        e.Synchronize([boss], Start.AddSeconds(1.1));
        Check(e.Collect(Start.AddSeconds(1.1), 100).Any(a => a.Action == 7161), "Packet-first cast lost observation grace.");
    }

    private static void Phases()
    {
        var e = new PalaceEncounter();
        e.Party = [new(0, "", 100)];
        var boss = Actor(10, 0x181F, Center) with { Hp = 373545, MaxHp = 373545, InCombat = false };
        var player = Actor(100, 0, Center, 1);
        e.Synchronize([boss, player], Start);
        Check(e.Hints(Start).Any(h => h.Contains("南北墙")), "Precombat strategy hint missing.");
        boss = boss with { InCombat = true };
        e.Synchronize([boss, player], Start);
        Check(e.Phase == "30%" && !e.Hints(Start).Any(h => h.Contains("南北墙")), "Precombat hint survived phase activation.");
        e.Apply(new LogSystemActionEffectEvent { SourceId = 10, ActionId = 7162, GlobalSequence = 1 }, Start);
        boss = boss with { Hp = 112063 };
        e.Synchronize([boss, player], Start.AddSeconds(1));
        Check(e.Phase == "21%" && e.Hints(Start).Any(h => h.Contains("循环 2/10")), "Phase boundary reset persistent rotation.");
        boss = boss with { Hp = 200000 };
        e.Synchronize([boss, player], Start.AddSeconds(2));
        Check(e.Phase == "21%", "Healing rewound the phase.");
        var meteor = new ReplayCast(7166, 0, Center, 0, Start.AddSeconds(2), Start.AddSeconds(8), false, false);
        e.Synchronize([boss with { Cast = meteor }, player], Start.AddSeconds(2));
        Check(!e.PredictedDamage().Any(), "Meteor activated before phase four.");
        boss = boss with { Hp = 56031, Cast = meteor };
        e.Synchronize([boss, player], Start.AddSeconds(3));
        Check(e.Phase == "黄道陨石" && !e.Hints(Start.AddSeconds(3)).Any(h => h.Contains("层循环"))
            && e.PredictedDamage().Single() is { TargetId: 100, DamageType: "Raidwide", Activation: var hit } && hit == meteor.FinishAt,
            "Final phase did not immediately disable rotation and discover ongoing meteor.");
        e.Apply(new LogSystemActionEffectEvent { SourceId = 10, ActionId = 7162, GlobalSequence = 2 }, Start.AddSeconds(4));
        e.Synchronize([boss with { Hp = 373545 }, player], Start.AddSeconds(4));
        Check(e.Phase == "黄道陨石" && !e.Hints(Start.AddSeconds(4)).Any(h => h.Contains("层循环"))
            && !e.Collect(Start.AddSeconds(4), 100).Any(a => a.Action == 7166), "Healing or late rotation effect restored loop or raidwide geometry.");
        e.StartCast(Cast(10, 7166, 0, Center) with { Start = Start.AddSeconds(10), Hit = Start.AddSeconds(16) });
        Check(e.PredictedDamage().Single().Activation == Start.AddSeconds(16), "New meteor packet borrowed the previous generation's finish.");
        e.Synchronize([boss with { Cast = meteor with { StartedAt = Start.AddSeconds(10), FinishAt = Start.AddSeconds(17) } }, player],
            Start.AddSeconds(10.1));
        Check(e.PredictedDamage().Single().Activation == Start.AddSeconds(17), "Current meteor finish correction was not consumed.");
        e.Synchronize([boss with { Position = Center + Vector3.UnitX * 30 }, player], Start.AddSeconds(18));
        var marker = e.Collect(Start.AddSeconds(18), 100).Single(a => a.Key == "enemy/10");
        Check(marker.OutlineOnly && MathF.Abs(marker.Position.X - (Center.X + 24.256f)) < .001f,
            "Out-of-bounds enemy marker was not clamped and outlined.");
    }

    private static void Knockbacks()
    {
        var e = new PalaceEncounter();
        e.Party = [new(0, "", 100)];
        var boss = Actor(10, 0x181D, Center);
        var player = Actor(100, 0, Center + Vector3.UnitZ * 5, 1);
        var pool = Actor(20, 0x1E858E, Center + Vector3.UnitZ * 20) with { EventState = 7 };
        e.Synchronize([boss, player, pool], Start);
        e.StartCast(Cast(10, 7156, 100, Center));
        Check(e.PredictedDamage().Single() is { TargetId: 100, DamageType: "Tankbuster" },
            "Charge target damage prediction missing.");
        Check(e.Knockbacks(player).Single() is { Unsafe: false, From: var from, To: var to }
            && Vector3.Distance(from, to) > 10 && e.KnockbackMarkers(Start, player).Single().PreviewOnly,
            "Knockback movement or projected actor marker missing.");
        player = player with { Statuses = [new(160, 0, 0, Start.AddSeconds(5))] };
        e.Synchronize([boss, player, pool], Start.AddSeconds(.1), false);
        Check(!e.Knockbacks(player).Any() && !e.Collect(Start, 100).Any(a => a.Key == "landing/10")
            && e.Collect(Start, 100).Any(a => a.Key.StartsWith("kb-direction/")),
            "Immunity did not separate projection safety from the source's independent directional ban.");
        var template = new PalaceLandingTemplate(Center, [], .5f);
        var near = Center + Vector3.UnitZ * .001f;
        Check(Vector3.Distance(template.Landing(near), near) > 10, "A nonzero direction was discarded near the source.");
        var large = new PalaceLandingTemplate(Center, [], 2f);
        Check(Vector3.Distance(template.Landing(Center + Vector3.UnitZ * 5), large.Landing(Center + Vector3.UnitZ * 5)) > 1.4,
            "Transformed player hitbox did not affect wall stopping.");
        e.Reset();
        e.Party = [new(0, "", 100)];
        var unrelated = Actor(101, 0, Center + Vector3.UnitZ * 5, 1);
        e.Synchronize([boss, player, unrelated], Start);
        e.StartCast(Cast(10, 7156, 101, Center));
        Check(!e.PredictedDamage().Any(), "Non-party target entered the Raid prediction mask.");
        e.Cancel(10);
        e.StartCast(Cast(10, 7156, 100, Center) with { Start = Start.AddSeconds(1) });
        e.Synchronize([boss, player with { Dead = true }, unrelated], Start.AddSeconds(1.1), false);
        Check(e.PredictedDamage().Single().TargetId == 100, "Dead but registered bait target lost its damage prediction.");
        e.Reset();
        e.Party = [new(0, "", 100)];
        e.Synchronize([boss], Start);
        e.StartCast(Cast(10, 7156, 100, Center));
        e.Synchronize([boss, player], Start.AddSeconds(.1), false);
        Check(!e.PredictedDamage().Any(), "An unresolved target at cast start retroactively created a bait.");
    }

    private static void PartyBait()
    {
        var e = new PalaceEncounter { Party = [new(0, "", 100), new(1, "", 101)] };
        e.Synchronize([Actor(10, 0x181D, Center) with { TargetId = 100 }, Actor(100, 0, Center + Vector3.UnitZ * 5, 1),
            Actor(101, 0, Center + Vector3.UnitX * 5, 1), Actor(102, 0, Center - Vector3.UnitX * 5, 1),
            Actor(103, 123, Center - Vector3.UnitZ * 5) with { SubKind = 9 }], Start);
        var areas = e.Collect(Start, 100);
        Check(areas.Any(a => a.Key == "cleave-bait/10/101") && areas.Any(a => a.Key == "cleave-bait/10/103")
            && !areas.Any(a => a.Key == "cleave-bait/10/102"), "Cleave bait used all visible players instead of raid and friendly NPCs.");
    }

    private static void CancelGeneration()
    {
        var e = new PalaceEncounter();
        var cast = new ReplayCast(100, 10, Center, 0, Start, Start.AddSeconds(3), false, false);
        var player = Actor(100, 0, Center, 1) with { Cast = cast };
        e.Synchronize([player], Start);
        Check(e.TakeCastCancellation(100) && !e.TakeCastCancellation(100), "Old player snapshot emitted repeated immediate cancel commands.");
        e.Synchronize([player with { Cast = cast with { StartedAt = Start.AddSeconds(.05) } }], Start.AddSeconds(.1));
        Check(!e.TakeCastCancellation(100), "Small timestamp drift was mistaken for a new player cast.");
        e.Synchronize([player with { Cast = cast with { ActionId = 101, TargetId = 100, StartedAt = Start.AddSeconds(.1) } }], Start.AddSeconds(.1));
        Check(e.TakeCastCancellation(100), "New cast generation inherited the previous cancellation latch.");
        e.Reset();
        e.Synchronize([player], Start);
        Check(e.TakeCastCancellation(100), "Reset retained previous cancellation identity.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
