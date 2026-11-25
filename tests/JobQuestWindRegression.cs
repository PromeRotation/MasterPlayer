using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

internal static partial class JobQuestRegression
{
    private static void QuestWindGeometry()
    {
        var paladin = JobQuestCatalog.Create(706).Single();
        Check(paladin.Arena(Vector3.Zero) is CircleGeometry { Radius: 20.5f, Position.X: -89.3f, Position.Z: 0 },
            "Paladin arena was inferred from the boss pose.");
        Check(paladin.Casts.Length == 4 && paladin.Casts.Single(c => c.Action == 8134).Shape is FanTemplate { Radius: 6.5f }
            && !paladin.Casts.Any(c => c.Action == 8138), "Paladin activated an extra raidwide or omitted cone.");
        var dancer = JobQuestCatalog.Create(867).Single();
        var arena = dancer.Arena(Vector3.Zero);
        var points = arena.GetContours().Single();
        Check(points.Count == 50 && arena.Position == new Vector3(520.47f, 0, 124.99f)
            && arena.Contains(new(537.9f, 0, 124.99f)) && !arena.Contains(new(538, 0, 124.99f)),
            "Gamboling exact 50-vertex ellipse.");
        var (e, boss, player) = IoSteelNode(867);
        player = player with { Position = boss.Position - Vector3.UnitZ * 100, Rotation = 0 };
        var gaze = new DungeonCastObservation(17198, boss.Position, 0, Start, Start.AddSeconds(3));
        e.Synchronize([boss with { Cast = gaze }, player], player.Position, Start, playerId: 100);
        e.Tick(Start.AddSeconds(2.5));
        Check(e.GazeFacing(player.Position, 0) is { } facing && MathF.Cos(facing) < .707107f
            && e.AmbientAoes().Count(a => a.Id.StartsWith("gamboling/gaze/")) == 3
            && e.Hints(player.Position).Contains("Turn away from gaze!"),
            "Gamboling long-range gaze did not route through facing, eye, pupil, view arc and hint.");
        e.Finish(1, 17198, Start.AddSeconds(2.5));
        Check(e.GazeFacing(player.Position, 0) == null && !e.AmbientAoes().Any(a => a.Id.StartsWith("gamboling/gaze/")),
            "Gamboling cancelled gaze retained outputs.");
    }

    private static void QuestWindKnockbacks()
    {
        foreach (var territory in new uint[] { 706, 867 })
        {
            var (e, boss, player) = IoSteelNode(territory);
            var center = boss.Position;
            var gamboling = territory == 867;
            var action = gamboling ? 17289u : 8136u;
            player = player with { Position = center + Vector3.UnitX * 5 };
            DungeonActor wind = new(2, gamboling ? 0x29D5u : 0x1B52u, center + Vector3.UnitX * 15,
                Dead: !gamboling, EventState: 7, Targetable: false);
            var cast = new DungeonCastObservation(action, center, 0, Start, Start.AddSeconds(3), 100, TotalTime: 3);
            e.Synchronize([boss, player, wind], player.Position, Start, playerId: 100);
            e.StartCast(1, cast, Start);
            var aoes = e.AmbientAoes().ToArray();
            Check(aoes.Count(a => a.Id.StartsWith("quest-wind/live/")) == (gamboling ? 1 : 0)
                && aoes.Count(a => a.Id.StartsWith("quest-wind/kb/avoid/")) == 1
                && aoes.Count(a => a.Id is "quest-wind/kb/player" or "quest-wind/kb/path") == 2,
                "Wind display/KB source sets, targetable filter or projected player/path.");
            if (gamboling)
            {
                wind = wind with { Dead = true };
                e.Synchronize([boss with { Cast = cast }, player, wind], player.Position, Start.AddSeconds(1), playerId: 100);
                Check(!e.AmbientAoes().Any(a => a.Id.StartsWith("quest-wind/live/"))
                    && e.AmbientAoes().Any(a => a.Id.StartsWith("quest-wind/kb/avoid/")),
                    "Dead dancer wind incorrectly removed the KB source.");
            }
            else
            {
                wind = wind with { EventState = 0 };
                e.Synchronize([boss with { Cast = cast }, player, wind], player.Position, Start.AddSeconds(1), playerId: 100);
                Check(e.AmbientAoes().Any(a => a.Id.StartsWith("quest-wind/live/"))
                    && e.Hints(player.Position).Contains("About to be knocked into danger!"),
                    "Paladin dead wind or landing warning was filtered.");
            }
            player = player with { Statuses = [160], StatusExpirations = new Dictionary<uint, DateTime> { [160] = cast.FinishAt } };
            e.Synchronize([boss with { Cast = cast }, player, wind], player.Position, Start.AddSeconds(1.1), playerId: 100);
            Check(e.AmbientAoes().Any(a => a.Id == "quest-wind/kb/player"), "Equal-expiry status incorrectly grants immunity.");
            player = player with { StatusExpirations = new Dictionary<uint, DateTime> { [160] = cast.FinishAt.AddTicks(1) } };
            e.Synchronize([boss with { Cast = cast }, player, wind], player.Position, Start.AddSeconds(1.2), playerId: 100);
            Check(!e.AmbientAoes().Any(a => a.Id.StartsWith("quest-wind/kb/")), "Valid immunity retained navigation/projection.");
            player = player with { Statuses = [], StatusExpirations = null, Position = center + Vector3.UnitX * (gamboling ? 16 : 18) };
            e.Synchronize([boss with { Cast = cast }, player, wind], player.Position, Start.AddSeconds(1.3), playerId: 100);
            var landing = e.AmbientAoes().Single(a => a.Id == "quest-wind/kb/player").Position;
            Check(e.Arena!.Contains(landing) && Vector3.Distance(landing, player.Position) < (gamboling ? 2 : 3),
                "Wind knockback did not stop on the true arena ray.");
            e.Finish(1, action, Start.AddSeconds(2), completed: false);
            Check(!e.AmbientAoes().Any(a => a.Id.StartsWith("quest-wind/kb/")), "Wind cancellation retained KB.");

            var (outside, outsideBoss, outsidePlayer) = IoSteelNode(territory);
            outsidePlayer = outsidePlayer with { Position = center + Vector3.UnitX * (gamboling ? 20 : 21) };
            outside.Synchronize([outsideBoss, outsidePlayer], outsidePlayer.Position, Start, playerId: 100);
            outside.StartCast(1, cast, Start);
            var path = outside.AmbientAoes().Single(a => a.Id == "quest-wind/kb/path");
            Check(!outside.AmbientAoes().Any(a => a.Id == "quest-wind/kb/player")
                && ((RectangleTemplate)path.Shape).Length > (gamboling ? 24.99f : 9.99f)
                && path.Position.X > outsidePlayer.Position.X,
                "Outside-arena wind KB accepted a negative ray intersection or drew an unprojectable marker.");

            var (changed, changedBoss, changedPlayer) = IoSteelNode(territory);
            changedPlayer = changedPlayer with { Position = center + Vector3.UnitX * 5 };
            wind = wind with { Position = center + Vector3.UnitX * 15 };
            changed.Synchronize([changedBoss with { Cast = cast }, changedPlayer, wind], changedPlayer.Position, Start, playerId: 100);
            var moved = changedBoss with { Position = center + Vector3.UnitZ * 2, Cast = cast with { Location = center + Vector3.UnitX } };
            changed.Synchronize([moved, changedPlayer, wind], changedPlayer.Position, Start.AddSeconds(1), playerId: 100);
            var origin = changed.AmbientAoes().Single(a => a.Id.StartsWith("quest-wind/kb/avoid/")).Position;
            Check(Vector3.Distance(origin, moved.Position) < .04f, "Observed CastInfo location change failed Finished/Started relock.");
            changed.StartCast(1, moved.Cast! with { Location = center - Vector3.UnitX }, Start.AddSeconds(1.1), fromPacket: true);
            Check(changed.AmbientAoes().Single(a => a.Id.StartsWith("quest-wind/kb/avoid/")).Position == origin,
                "Duplicate packet correction rebound the wind KB origin.");
            var subtle = moved with { Position = center + Vector3.UnitZ * 4,
                Cast = moved.Cast! with { FinishAt = moved.Cast!.FinishAt.AddTicks(5000), TotalTime = 3.0005f } };
            changed.Synchronize([subtle, changedPlayer, wind], changedPlayer.Position, Start.AddSeconds(1.2), playerId: 100);
            Check(Vector3.Distance(changed.AmbientAoes().Single(a => a.Id.StartsWith("quest-wind/kb/avoid/")).Position,
                subtle.Position) < .04f, "Sub-millisecond CastInfo duration change failed to relock.");
            var jitter = subtle with { Position = center + Vector3.UnitZ * 6,
                Cast = subtle.Cast! with { FinishAt = subtle.Cast!.FinishAt.AddTicks(1) } };
            var subtleOrigin = changed.AmbientAoes().Single(a => a.Id.StartsWith("quest-wind/kb/avoid/")).Position;
            changed.Synchronize([jitter, changedPlayer, wind], changedPlayer.Position, Start.AddSeconds(1.3), playerId: 100);
            Check(changed.AmbientAoes().Single(a => a.Id.StartsWith("quest-wind/kb/avoid/")).Position == subtleOrigin,
                "Clock reconstruction jitter restarted an unchanged raw TotalTime.");
            var newer = cast with { StartedAt = Start.AddSeconds(10), FinishAt = Start.AddSeconds(13) };
            changed.StartCast(1, newer, Start.AddSeconds(10));
            var newerOrigin = changed.AmbientAoes().Single(a => a.Id.StartsWith("quest-wind/kb/avoid/")).Position;
            changed.StartCast(1, cast with { Location = center - Vector3.UnitZ }, Start.AddSeconds(10.1), fromPacket: false);
            Check(changed.OutputCasts.Single().Value.FinishAt == newer.FinishAt
                && changed.AmbientAoes().Single(a => a.Id.StartsWith("quest-wind/kb/avoid/")).Position == newerOrigin,
                "Stale changed snapshot replaced the newer packet cast.");
        }
    }

    private static void FoxshotBait()
    {
        var (corrected, cb, cp) = IoSteelNode(867);
        var other = cp with { Id = 101, PartySlot = 1, Position = cb.Position - Vector3.UnitX * 8 };
        corrected.CurrentPartyMembers = [new(0, "", 100), new(1, "", 101)];
        cp = cp with { Position = cb.Position + Vector3.UnitX * 8 };
        var correctionCast = new DungeonCastObservation(17289, cb.Position, 0, Start, Start.AddSeconds(6), 100);
        DungeonActor cw = new(2, 0x29D5, cb.Position + Vector3.UnitX * 15);
        corrected.Synchronize([cb with { Cast = correctionCast }, cp, other, cw], cp.Position, Start, playerId: 100);
        var correctionOrigin = corrected.AmbientAoes().Single(a => a.Id.StartsWith("quest-wind/kb/avoid/")).Position;
        cb = cb with { Position = cb.Position + Vector3.UnitZ * 2, Cast = correctionCast };
        corrected.Synchronize([cb, cp, other, cw], cp.Position, Start.AddSeconds(1), playerId: 100);
        corrected.StartCast(1, correctionCast with { TargetId = 101 }, Start.AddSeconds(1.1));
        Check(corrected.Casts[1].TargetId == 101 && corrected.PredictedDamage.Single().TargetId == 101
            && corrected.AmbientAoes().Single(a => a.Id.StartsWith("quest-wind/kb/avoid/")).Position == correctionOrigin,
            "Packet target handoff rebound independent knockback.");
        corrected.Synchronize([cb, cp, other, cw], cp.Position, Start.AddSeconds(1.2), playerId: 100);
        Check(corrected.Casts[1].TargetId == 101
            && corrected.AmbientAoes().Single(a => a.Id.StartsWith("quest-wind/kb/avoid/")).Position == correctionOrigin,
            "Unchanged real observation reverted packet target or relocked KB.");
        corrected.StartCast(1, correctionCast with { TargetId = 999 }, Start.AddSeconds(1.3));
        corrected.StartCast(1, correctionCast with { TargetId = 100 }, Start.AddSeconds(1.4));
        Check(corrected.AmbientAoes().Count(a => a.Id == "foxshot/1/display") == 1
            && corrected.PredictedDamage.Single().TargetId == 100
            && corrected.AmbientAoes().Single(a => a.Id.StartsWith("quest-wind/kb/avoid/")).Position == correctionOrigin,
            "Valid packet target failed to restore bait after unresolved target.");
        var (e, boss, player) = IoSteelNode(867);
        var center = boss.Position;
        player = player with { Position = center + Vector3.UnitX * 8 };
        DungeonActor npc = new(2, 0x29D1, center + Vector3.UnitX * 4, FriendlyNpc: true);
        DungeonActor wind = new(3, 0x29D5, center + Vector3.UnitZ * 12);
        var cast = new DungeonCastObservation(17289, center, 0, Start, Start.AddSeconds(6), 100);
        e.Synchronize([boss with { Cast = cast }, player, npc, wind], player.Position, Start, playerId: 100);
        var display = e.AmbientAoes().Single(a => a.Id == "foxshot/1/display");
        Check(display.OutlineOnly && display.Shape is RayRectangleTemplate { Length: 8 }
            && e.AmbientAoes().Any(a => a.Id == "foxshot/1/party/2") && e.Hints(player.Position).Contains("Bait away from raid!")
            && e.PredictedDamage.Single() is { TargetId: 100, DamageType: "Tankbuster" },
            "Foxshot self outline, friendly NPC slot, dynamic rectangle, warning or damage.");
        boss = boss with { Position = center + Vector3.UnitZ * 2, Cast = cast };
        player = player with { Position = center + Vector3.UnitX * 12, Dead = true };
        e.Synchronize([boss, player, npc, wind], player.Position, Start.AddSeconds(1), playerId: 100);
        display = e.AmbientAoes().Single(a => a.Id == "foxshot/1/display");
        Check(display.Position == boss.Position && MathF.Abs(((RayRectangleTemplate)display.Shape).Length - MathF.Sqrt(148)) < .001f
            && e.AmbientAoes().Single(a => a.Id == "quest-wind/kb/player").Position != player.Position,
            "Foxshot lost dead target or dynamic source/target geometry.");
        // Moving the caster changes the bait but must not replace the independent knockback origin.
        var kbOrigin = e.AmbientAoes().Single(a => a.Id.StartsWith("quest-wind/kb/avoid/")).Position;
        Check(Vector3.Distance(kbOrigin, center) < .04f && Vector3.Distance(kbOrigin, boss.Position) > 1.9f,
            "Foxshot dynamic source rebound the independent locked knockback origin.");
        e.RemoveActor(100);
        e.Synchronize([boss, npc, wind], player.Position, Start.AddSeconds(1.1), playerId: 100);
        Check(e.PredictedDamage.Single().PartySlot == 0, "Foxshot removed actor lost its retained party-member damage mask.");
        var replacement = player with { Position = center - Vector3.UnitX * 10, Dead = false };
        e.Synchronize([boss, replacement, npc, wind], replacement.Position, Start.AddSeconds(1.2), playerId: 100);
        display = e.AmbientAoes().Single(a => a.Id == "foxshot/1/display");
        Check(display.OutlineOnly && e.AmbientAoes().Any(a => a.Id == "foxshot/1/avoid")
            && MathF.Abs(((RayRectangleTemplate)display.Shape).Length - MathF.Sqrt(148)) < .001f
            && !e.Hints(replacement.Position).Contains("GTFO from baited aoe!"),
            "Foxshot frozen target must use ID for display/hints and reference identity for navigation.");
        e.CurrentPartyMembers = [];
        Check(!e.PredictedDamage.Any(), "Foxshot retained damage after the actual party slot was removed.");
        e.Finish(1, 17289, Start.AddSeconds(1.5));
        Check(!e.AmbientAoes().Any(a => a.Id.StartsWith("foxshot/")) && !e.PredictedDamage.Any(),
            "Foxshot actual ending failed cleanup.");
    }
}
