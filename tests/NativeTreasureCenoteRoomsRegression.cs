using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

internal static class NativeTreasureCenoteRoomsRegression
{
    private static readonly DateTime Start = new(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly (uint Room, Vector3 Center, uint Trash)[] Rooms =
    [
        (0x1EBAA5, new(0, 0, 192), 0x4310),
        (0x1EBAA6, new(160, 0, 19), 0x431D),
        (0x1EBAA7, new(-197, 0, -145), 0x4325),
    ];
    private static DungeonActor Actor(ulong id, uint oid, Vector3 position, bool dead = false) =>
        new(id, oid, position, Dead: dead, InCombat: false, Targetable: false, Hp: dead ? 0u : 100u, MaxHp: 100, IsEnemy: oid != 0);
    private static DungeonEncounter Encounter(uint room, Vector3 center, uint trash)
    {
        var e = new DungeonEncounter(1209);
        e.Synchronize([Actor(1, room, center), Actor(2, trash, center), Actor(100, 0, center) with { Player = true, Hostile = false }],
            center, Start, playerId: 100);
        return e;
    }
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
    internal static void Run(Action<string, Action> run)
    {
        run("native treasure Cenote rooms source rules all 56 shapes and inactive actions", Rules);
        run("native treasure Cenote rooms pull requires room trash regardless combat dead targetable", Pull);
        run("native treasure Cenote rooms lifetime only object state7 or destruction", Lifetime);
        run("native treasure Cenote rooms completed and destroyed generation rejects stale frames", Generations);
        run("native treasure Cenote rooms coexisting modules preserve source insertion order", Coexistence);
        run("native treasure Cenote rooms independent observed cast clocks effects cancellation and view change", Casts);
        run("native treasure Cenote room4 wingblades preserve packet rotation without side offsets", Wingblades);
        run("native treasure Cenote rooms exact compound arena final center and fixed entrance", Arenas);
        run("native treasure Cenote rooms complete trash rewards priorities and no object marker", Rewards);
    }
    private static void Rules()
    {
        var definitions = NativeCenoteRoomCatalog.Create().Where(d => d.BossOid != NativeCenoteRoomCatalog.ROOM_1).ToArray();
        Check(definitions.Select(d => d.Casts.Length).SequenceEqual([20, 18, 18])
            && definitions.All(d => d.Casts.Select(c => c.Action).Distinct().Count() == d.Casts.Length
                && !d.Casts.Any(c => c.Action is 38280 or 38305 or 9630)), "Rule count, duplicates or inactive actions differ.");
        var two = definitions[0].Casts.ToDictionary(c => c.Action);
        Check(two[38296].Shape is CircleTemplate { Radius: 8 } && two[38298].Shape == two[38296].Shape
            && two[38293].Shape is RayRectangleTemplate { Length: 40, MinLocalX: -4, MaxLocalX: 4 }
            && two[38290].Shape is RayRectangleTemplate { Length: 14, MinLocalX: -2, MaxLocalX: 2 }
            && two[38300].Shape is RayRectangleTemplate { Length: 10, MinLocalX: -2, MaxLocalX: 2 }
            && two[38299].Shape is FanTemplate { Radius: 8, AngleRadians: MathF.PI / 2 }
            && two[38295].Shape is FanTemplate { Radius: 10, AngleRadians: MathF.PI / 2 }
            && two[38291].Shape == two[38295].Shape
            && two[38297].Shape is FanTemplate { Radius: 10 } f && f.AngleRadians == 2 * MathF.PI / 3
            && two[38302].Shape == two[38297].Shape
            && new uint[] { 38301, 38294, 38277 }.All(a => two[a].Shape is CircleTemplate { Radius: 6 }),
            "Room2 active geometry differs.");
        var three = definitions[1].Casts.ToDictionary(c => c.Action);
        Check(three[38308].Shape is RayRectangleTemplate { Length: 40, MinLocalX: -4, MaxLocalX: 4 }
            && three[38307].Shape is FanTemplate { Radius: 18 } w && w.AngleRadians == MathF.PI / 3
            && three[38303].Shape is CircleTemplate { Radius: 8 }
            && three[38304].Shape is FanTemplate { Radius: 12 } b && b.AngleRadians == 2 * MathF.PI / 3
            && three[38310].Shape is FanTemplate { Radius: 13 } s && s.AngleRadians == MathF.PI / 3
            && three[38306].Shape is FanTemplate { Radius: 8, AngleRadians: MathF.PI / 2 }
            && new uint[] { 38313, 38311, 38277 }.All(a => three[a].Shape is CircleTemplate { Radius: 6 })
            && new uint[] { 38312, 38309 }.All(a => three[a].Shape is CircleTemplate { Radius: 5 }), "Room3 shapes differ.");
        var four = definitions[2].Casts.ToDictionary(c => c.Action);
        Check(new uint[] { 38286, 38316, 38314, 38277 }.All(a => four[a].Shape is CircleTemplate { Radius: 6 })
            && four[38287].Shape is RayRectangleTemplate { Length: 20, MinLocalX: -2, MaxLocalX: 2 }
            && four[38317].Shape is CircleTemplate { Radius: 8 }
            && four[38319].Shape is FanTemplate { Radius: 8, AngleRadians: MathF.PI } && four[38318].Shape == four[38319].Shape
            && four[38288].Shape is FanTemplate { Radius: 10 } y && y.AngleRadians == 2 * MathF.PI / 3
            && four[38289].Shape is RayRectangleTemplate { Length: 10, MinLocalX: -2, MaxLocalX: 2 }
            && four[38315].Shape is FanTemplate { Radius: 12 } o && o.AngleRadians == 2 * MathF.PI / 3, "Room4 shapes differ.");
        Check(definitions.All(d => new uint[] { 32302, 32301, 32304, 32303, 32305 }
            .All(a => d.Casts.Single(c => c.Action == a).Shape is CircleTemplate { Radius: 7 })
            && d.Casts.Single(c => c.Action == 38279).Shape is CircleTemplate { Radius: 11 }
            && d.Casts.Single(c => c.Action == 38278).Shape is FanTemplate { Radius: 15 }), "Shared reward shapes differ.");
    }
    private static void Pull()
    {
        foreach (var (room, center, trash) in Rooms)
        {
            var e = new DungeonEncounter(1209);
            e.Synchronize([Actor(1, room, center), Actor(2, 0x4300, center)], center, Start, playerId: 100);
            Check(e.Active == null, "Reward-only presence started room.");
            e.Synchronize([Actor(1, room, center, dead: true), Actor(2, trash, center, dead: true)], center, Start, playerId: 100);
            Check(e.Active?.BossOid == room, "Dead/untargetable/noncombat room or trash improperly blocked source CheckPull.");
        }
    }
    private static void Lifetime()
    {
        foreach (var (room, center, trash) in Rooms)
        {
            var e = Encounter(room, center, trash);
            e.Synchronize([Actor(1, room, center, dead: true)], center, Start.AddSeconds(1), playerId: 100);
            Check(e.Active?.BossOid == room, "Empty or all-dead trash incorrectly ended room.");
            e.Synchronize([Actor(1, room, center) with { EventState = 7 }, Actor(2, trash, center)], center, Start.AddSeconds(2), playerId: 100);
            Check(e.Active == null && !e.AmbientAoes().Any(), "State7 failed room end or immediately restarted.");
            e.Synchronize([Actor(3, room, center), Actor(2, trash, center)], center, Start.AddSeconds(3), playerId: 100);
            Check(e.Active?.BossOid == room && e.PrimaryId == 3, "New room object failed activation.");
            e.Synchronize([Actor(2, trash, center)], center, Start.AddSeconds(4), playerId: 100);
            Check(e.Active == null, "Destroyed room object retained phase on live trash.");
            e = Encounter(room, center, trash);
            e.Synchronize([Actor(1, 0x233C, center), Actor(2, trash, center)], center, Start.AddSeconds(1), playerId: 100);
            Check(e.Active == null, "Same-ID other object substituted for destroyed original room.");
        }
    }
    private static void Casts()
    {
        foreach (var (room, center, trash) in Rooms)
        {
            var e = Encounter(room, center, trash);
            var rules = e.Active!.Casts;
            var actors = new List<DungeonActor> { Actor(1, room, center), Actor(100, 0, center) with { Player = true } };
            for (var i = 0; i < rules.Length; ++i)
                actors.Add(Actor((ulong)(i + 2), trash, center) with { Cast = new(rules[i].Action, center, .37f,
                    Start, Start.AddSeconds(3 + i)) });
            e.Synchronize(actors, center, Start.AddSeconds(.1), playerId: 100);
            Check(e.Casts.Count == rules.Length && e.OutputCasts.Count() == rules.Length, "Bootstrap lost independent ordinary/group casts.");
            e.Tick(Start.AddSeconds(100));
            e.Effect(2, rules[0].Action, 1, Start.AddSeconds(100));
            Check(e.Casts.Count == rules.Length, "Effect or predicted expiry removed observed casts.");
            e.Synchronize(actors, center, Start.AddSeconds(101), playerId: 101);
            Check(e.Casts.Count == rules.Length, "Viewer switch reset world mechanisms.");
            for (var i = 0; i < rules.Length; ++i) e.Finish((ulong)(i + 2), rules[i].Action, Start.AddSeconds(102));
            Check(!e.Casts.Any() && e.Active?.BossOid == room, "Actual endings retained casts or ended room.");
            e = Encounter(room, center, trash);
            e.Synchronize([Actor(1, room, center), Actor(2, trash, center) with
                { Cast = new(rules[0].Action, center, 0, Start, Start.AddSeconds(3)) }],
                center, Start.AddSeconds(.1), playerId: 100);
            e.Synchronize([Actor(1, room, center), Actor(2, trash, center)], center, Start.AddSeconds(.2), playerId: 100);
            Check(!e.Casts.Any() && e.Active?.BossOid == room, "Short observed cancellation before prediction retained cast.");
        }
    }
    private static void Generations()
    {
        foreach (var (room, center, trash) in Rooms)
        {
            var e = Encounter(room, center, trash);
            var original = new[] { Actor(1, room, center), Actor(2, trash, center) };
            e.Synchronize([Actor(1, room, center) with { EventState = 7 }, original[1]],
                center, Start.AddSeconds(1), playerId: 100);
            e.Synchronize(original, center, Start.AddSeconds(2), playerId: 100);
            Check(e.Active == null, "Completed original object restarted without creation.");
            e.ObjectCreated(1, Start.AddSeconds(3), room, center);
            e.Synchronize(original, center, Start.AddSeconds(3), playerId: 100);
            Check(e.Active?.BossOid == room, "Real new same-ID object could not start.");
            e.RemoveActor(1, Start.AddSeconds(4));
            e.Synchronize(original, center, Start.AddSeconds(4), playerId: 100);
            Check(e.Active == null, "Remove event ahead of stale world resurrected original object.");
            e.Synchronize([original[1]], center, Start.AddSeconds(5), playerId: 100);
            e.Synchronize(original, center, Start.AddSeconds(6), playerId: 100);
            Check(e.Active?.BossOid == room, "Observed absence failed new-generation activation.");
        }
        var oldRoom = Rooms[0];
        var newRoom = Rooms[1];
        var replaced = Encounter(oldRoom.Room, oldRoom.Center, oldRoom.Trash);
        replaced.Synchronize([Actor(1, newRoom.Room, newRoom.Center) with { EventState = 7 },
            Actor(2, newRoom.Trash, newRoom.Center)], newRoom.Center, Start.AddSeconds(1), playerId: 100);
        Check(replaced.Active == null, "Completed replacement room unexpectedly started.");
        replaced.Synchronize([Actor(1, newRoom.Room, newRoom.Center), Actor(2, newRoom.Trash, newRoom.Center)],
            newRoom.Center, Start.AddSeconds(2), playerId: 100);
        Check(replaced.Active == null, "Old active room overwrote completed replacement generation receipt.");
    }
    private static void Coexistence()
    {
        var a = Rooms[0];
        var b = Rooms[1];
        var c = Rooms[2];
        var e = new DungeonEncounter(1209);
        var actors = new[] { Actor(1, a.Room, a.Center), Actor(2, b.Room, b.Center),
            Actor(3, a.Trash, a.Center), Actor(4, b.Trash, b.Center), Actor(5, c.Room, c.Center), Actor(6, c.Trash, c.Center) };
        e.Synchronize(actors, b.Center, Start, playerId: 100);
        Check(e.Active?.BossOid == a.Room, "Closest room replaced first loaded equal-priority module.");
        e.Synchronize(actors.Reverse(), b.Center, Start.AddSeconds(1), playerId: 100);
        Check(e.Active?.BossOid == a.Room, "Equal priority switched active room.");
        e.Synchronize(actors.Reverse().Select(a => a.Id == 1 ? a with { EventState = 7 } : a),
            b.Center, Start.AddSeconds(2), playerId: 100);
        Check(e.Active?.BossOid == b.Room, "Completed room did not promote next module.");
        e = new DungeonEncounter(1209);
        e.Synchronize(actors, b.Center, Start, playerId: 100);
        e.Synchronize(actors.Select(a => a.Id == 2 ? a with { EventState = 7 } : a), b.Center, Start.AddSeconds(1), playerId: 100);
        e.Synchronize(actors.Select(a => a.Id is 1 or 5 ? a with { EventState = 7 } : a),
            b.Center, Start.AddSeconds(2), playerId: 100);
        Check(e.Active == null, "Unselected completed room restarted after state restoration.");
    }
    private static void Wingblades()
    {
        var r = Rooms[2];
        var e = Encounter(r.Room, r.Center, r.Trash);
        e.Synchronize([Actor(1, r.Room, r.Center), Actor(2, r.Trash, r.Center) with
            { Cast = new(38319, r.Center, .37f, Start, Start.AddSeconds(3)) },
            Actor(3, r.Trash, r.Center) with { Cast = new(38318, r.Center, .37f, Start, Start.AddSeconds(4)) }],
            r.Center, Start.AddSeconds(.1), playerId: 100);
        Check(e.Casts.Count == 2 && e.Casts.Values.All(c => c.Rotation == .37f)
            && e.Casts.Values.All(c => c.Rule.Shape!.Resolve(c.Location, c.Rotation, null)
                .Contains(r.Center + new Vector3(MathF.Sin(.37f), 0, MathF.Cos(.37f)) * 5, 0)),
            "Left/right action names added unsupported rotation.");
    }
    private static void Arenas()
    {
        foreach (var (room, center, trash) in Rooms)
        {
            var e = Encounter(room, center, trash);
            var arena = e.Arena!;
            Check(arena.Contains(center) && !arena.Contains(center + Vector3.UnitZ * 20)
                && MathF.Abs(arena.Position.Z - (center.Z - 4.214325f)) < .001f
                && arena.GetContours().Single().Count == 49, "Compound room outline or final AABB center differs.");
            e.Synchronize([Actor(1, room, center + Vector3.UnitX * 50), Actor(2, trash, center)], center, Start.AddSeconds(1), playerId: 100);
            Check(e.Arena!.Position == arena.Position, "Moving room object moved fixed world arena.");
        }
    }
    private static void Rewards()
    {
        foreach (var (room, center, trash) in Rooms)
        {
            var e = Encounter(room, center, trash);
            e.Synchronize([Actor(1, room, center) with { Targetable = true },
                Actor(2, trash, center) with { Targetable = true }, Actor(3, 0x4300, center) with { Targetable = true },
                Actor(4, 0x42FF, center) with { Targetable = true }, Actor(5, 0x233C, center) with { Targetable = true }],
                center, Start.AddSeconds(1), playerId: 100);
            var markers = e.AmbientAoes().Where(a => a.Id.StartsWith("treasure/enemy/")).ToArray();
            Check(markers.Length == 3 && markers.All(a => a.Id is not ("treasure/enemy/1" or "treasure/enemy/5"))
                && markers.Single(a => a.Id == "treasure/enemy/3").Color == new Vector4(1, 0, 1, 1)
                && NativeCenoteRoomCatalog.Enemies(room).Length == NativeCenoteRoomCatalog.Trash(room).Length + 7
                && NativeTreasureCatalog.Priority(room, 0x4300) == 6
                && NativeTreasureCatalog.Priority(room, 0x42FE) == 2 && NativeTreasureCatalog.Priority(room, 0x42FF) == 1
                && NativeTreasureCatalog.Priority(room, trash) == 0, "Room object/helper marker or reward list/priority differs.");
        }
    }
}
