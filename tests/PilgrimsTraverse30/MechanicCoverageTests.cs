using System.Numerics;
using BossMod;
using BossMod.Components;
using MasterPlayer.MechanicHost;
using MasterPlayer.MechanicRuntime;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

internal static class MechanicCoverageTests
{
    private static readonly DateTime Start = new(2026, 11, 17, 5, 8, 0, DateTimeKind.Utc);
    private static readonly WPos Center = new(-300, -300);

    public static void Run()
    {
        CheckHalosAndPlatforms();
        CheckGripAndReach(40551, 40552, 70);
        CheckGripAndReach(44927, 40553, -70);
        CheckMovingVoidzone();
        CheckChaseEventRecognition();
        Console.WriteLine("PASS PT30 coverage: all four halos, both rectangle/cone sequences, stationary/moving voidzones, icon/cast chase recognition, MP geometry conversion and usable inner side platforms.");
    }

    private static MechanicSession CreateSession()
    {
        var session = new MechanicSession("PT30 full mechanic coverage");
        session.BeginFrame(Start);
        session.Execute(new WorldState.OpZoneChange(1283, 1034));
        session.Execute(new ActorState.OpCreate(100, 0, 0, 0, "Player", 0, ActorType.Player, Class.MCH, 100,
            new(-287.4f, 0, -311.8f, 0), .5f, new(120145, 120145, 0, 10000, 10000), true, true, 0, 0, 0));
        session.Execute(new PartyState.OpModify(0, new(100, 100, false, "Player")));
        AddEnemy(session, 1, 0x4609, new(-300, 0, -300, 0));
        AddEnemy(session, 2, 0x233C, new(-300, 0, -300, 0));
        session.Execute(new ActorState.OpCombat(1, true));
        session.Update();
        Require(session.BossModules.ActiveModule?.GetType().Name == "DD30ForgivenTreachery", "PT30 module did not activate");
        return session;
    }

    private static void AddEnemy(MechanicSession session, ulong id, uint oid, Vector4 position) =>
        session.Execute(new ActorState.OpCreate(id, oid, (int)id, 0, "Fixture", 0, ActorType.Enemy, Class.None, 100,
            position, oid == 0x4609 ? 14 : 0, new(5741267, 5741267, 0, 0, 0), true, false, 0, 0, 0));

    private static void Cast(MechanicSession session, uint action, float seconds = 12) =>
        session.Execute(new ActorState.OpCastInfo(2, new()
        {
            Action = new(ActionType.Spell, action), Rotation = default,
            Location = new(-300, 0, -300), TotalTime = seconds
        }));

    private static AIHints Hints(MechanicSession session, PilgrimsTraverse30ChaseHints? adapter = null)
    {
        session.Update();
        var hints = new AIHints();
        new AIHintsBuilder(session.World, session.BossModules, session.ZoneModules, null).Update(hints, 0, false);
        (adapter ?? new()).Update(session.BossModules.ActiveModule!, hints, 0);
        return hints;
    }

    private static List<IGeometryShape> Convert(IEnumerable<ShapeDistance> zones)
    {
        var result = new List<IGeometryShape>();
        foreach (var zone in zones)
        {
            var primitives = MechanicHintsConvert.ConvertForbiddenZone(zone, 0, new(-300, 0, -300), 32, out var kind);
            Require(primitives.Count > 0 && kind != MechanicNavZoneKind.Failed, $"MP dropped {zone.GetType().Name}");
            result.AddRange(primitives.Select(p => p.Template.Resolve(p.Position, p.Rotation, null)));
        }
        return result;
    }

    private static void CheckHalosAndPlatforms()
    {
        using var session = CreateSession();
        foreach (var (action, inner, outer) in new (uint, float, float)[] { (39642, 9, 14), (39643, 14, 19), (39674, 19, 24), (39743, 24, 29) })
        {
            Cast(session, action);
            var hints = Hints(session);
            Require(hints.ForbiddenZones.Count == 1, $"Halo {action} missing or duplicated");
            var point = new WPos(-300 + (inner + outer) / 2, -300);
            Require(hints.ForbiddenZones[0].shapeDistance.Contains(point), $"Halo {action} wrong radii");
            Require(Convert(hints.ForbiddenZones.Select(z => z.shapeDistance)).Any(s => s.Contains(point.ToVec3())), "MP donut conversion lost the hazard");
            var obstacles = Convert(hints.TemporaryObstacles);
            Require(obstacles.Any(s => s.Contains(new(-300, 0, -300))), "Central hole lost at MP bridge");
            foreach (var safePad in new[] { new WPos(-288, -300), new WPos(-312, -300) })
            {
                Require(session.BossModules.ActiveModule!.Bounds.Contains(safePad - session.BossModules.ActiveModule.Center), "Fixture pad is outside real arena");
                Require(!obstacles.Any(s => s.Contains(safePad.ToVec3())), "Hole obstacle blocks a usable inner side platform");
                if (action == 39643)
                    Require(!hints.ForbiddenZones.Any(z => z.shapeDistance.Contains(safePad)), "14-19m halo must leave inner pads usable");
            }
            session.Execute(new ActorState.OpCastInfo(2, null));
            Require(Hints(session).ForbiddenZones.Count == 0, "Resolved halo remained registered");
        }
    }

    private static void CheckGripAndReach(uint grip, uint reach, float angle)
    {
        using var session = CreateSession();
        Cast(session, grip, 7.7f);
        var hints = Hints(session);
        Require(hints.ForbiddenZones.Count == 2, $"Grip {grip} must predict rectangle and follow-up");
        Require(Convert(hints.ForbiddenZones.Select(z => z.shapeDistance)).Count == 2, "MP rectangle conversion missing");
        session.Execute(new ActorState.OpCastInfo(2, null));
        hints = Hints(session);
        Require(hints.ForbiddenZones.Count == 1, "Follow-up cone missing after rectangle");
        var prims = MechanicHintsConvert.ConvertForbiddenZone(hints.ForbiddenZones[0].shapeDistance, 0, new(-300, 0, -300), 32, out _);
        Require(prims.Single().Template is FanTemplate fan && MathF.Abs(fan.AngleRadians - 220 * Angle.DegToRad) < .001f, "220 degree cone lost at MP bridge");
        var front = Center + angle.Degrees().ToDirection() * 16.5f;
        var rear = Center - angle.Degrees().ToDirection() * 16.5f;
        Require(hints.ForbiddenZones[0].shapeDistance.Contains(front) && !hints.ForbiddenZones[0].shapeDistance.Contains(rear), "Follow-up cone points to wrong side");
        Require(Convert(hints.ForbiddenZones.Select(z => z.shapeDistance)).Any(s => s.Contains(front.ToVec3())), "MP cone conversion points to wrong side");
        Cast(session, reach, 1);
        session.Execute(new ActorState.OpCastInfo(2, null));
        Require(Hints(session).ForbiddenZones.Count == 0, "Resolved follow-up cone remained registered");
    }

    private static void CheckMovingVoidzone()
    {
        using var session = CreateSession();
        AddEnemy(session, 3, 0x460A, new(-283.5f, 0, -300, 0));
        session.Execute(new ActorState.OpPlayActionTimelineEvent(3, 0x1E46));
        var hints = Hints(session);
        Require(Convert(hints.TemporaryObstacles).Any(s => s.Contains(new(-283.5f, 0, -300))), "Stationary voidzone not blocking movement");
        session.BeginFrame(Start.AddSeconds(.1));
        session.Execute(new ActorState.OpMove(3, new(-283.5f, 0, -299.5f, 0)));
        hints = Hints(session);
        Require(hints.ForbiddenZones.Count == 3, "Moving voidzone must predict 1.1/3/future-second sweeps");
        Require(Convert(hints.ForbiddenZones.Select(z => z.shapeDistance)).Count == 3, "MP arc conversion missing");
        session.Execute(new ActorState.OpPlayActionTimelineEvent(3, 0x1E3C));
        Require(Hints(session).ForbiddenZones.Count == 0, "Removed voidzone still has danger sweeps");
    }

    private static void CheckChaseEventRecognition()
    {
        using var session = CreateSession();
        session.Execute(new ActorState.OpIcon(100, 197, 100));
        session.Execute(new ActorState.OpCastInfo(2, new()
        {
            Action = new(ActionType.Spell, 44918), TotalTime = 3, Location = new(-287.4f, 0, -311.8f)
        }));
        var adapter = new PilgrimsTraverse30ChaseHints();
        var hints = Hints(session, adapter);
        Require(adapter.Active && hints.GoalZones.Count > 0, "Live icon/cast sequence did not start chase adaptation");
        var chase = session.BossModules.ActiveModule!.FindComponent<StandardChasingAOEs>()!;
        Require(chase.Chasers.Count == 1 && chase.Chasers[0].NumRemaining == 8, "Chase cast recognition lost hit count");
        AddEnemy(session, 4, 0x233C, new(-300, 0, -300, 0));
        session.Execute(new ActorState.OpCastInfo(4, new()
        {
            Action = new(ActionType.Spell, 39674), TotalTime = 12, Location = new(-300, 0, -300)
        }));
        hints = Hints(session, adapter);
        Require(hints.ForbiddenZones.Any(z => z.shapeDistance.Contains(new(-278.5f, -300))), "Chase adaptation suppressed the overlapping halo");
        Convert(hints.ForbiddenZones.Select(z => z.shapeDistance));
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
