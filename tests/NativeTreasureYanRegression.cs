using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

internal static class NativeTreasureYanRegression
{
    private static readonly DateTime Start = new(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(100, 0, 100);
    private static DungeonActor Player => new(100, 0, Center, PartySlot: 0, Player: true, Hostile: false);
    private static DungeonActor Sack(ulong id, bool gold, Vector3 p) => new(id, gold ? 0x1EBE47u : 0x1EBE48u,
        p, Dead: true, Targetable: false, HitboxRadius: .5f);
    private static DungeonEncounter Create(params DungeonActor[] sacks)
    {
        var e = new DungeonEncounter(1279) { TreasurePartyInput = [100, 101] };
        e.Synchronize([.. sacks, Player, Player with { Id = 101, PartySlot = 1, Position = Center + Vector3.UnitZ * 15 },
            new(20, 0x4902, Center + Vector3.UnitX * 10)], Player.Position, Start, playerId: 100);
        return e;
    }
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
    private static DungeonAmbientAoe Goal(DungeonEncounter e) => e.AmbientAoes().Single(a => a.Id == "treasure/sack/goal");
    internal static void Run(Action<string, Action> run)
    {
        run("native treasure Yan noncombat dead untargetable silver activation both sacks AllDestroyed", Lifetime);
        run("native treasure Yan weighted distance gold first strict tie hitbox soft goal and Rush exclusion", Goals);
        run("native treasure Yan raw creation position FIFO destroy at current position generation receipts", Towers);
        run("native treasure Yan towers live party centers display only empty solo full oversoak and hints", Soakers);
        run("native treasure Yan Rush raw delta quantized origin locked length actual end and cancel", Rush);
        run("native treasure Yan stale generation Create position persistent order Rush duplicate packet and real replacement", Corrections);
    }
    private static void Lifetime()
    {
        var silver = Sack(1, false, Center);
        var gold = Sack(2, true, Center + Vector3.UnitZ * 2);
        var e = Create(silver, gold);
        Check(e.Active?.BossOid == 0x1EBE48 && NativeTreasureCatalog.Owns(0x1EBE48, 1279)
            && !NativeTreasureCatalog.Owns(0x4902, 1279) && e.TreasureSackTowers.Count == 2,
            "Silver primary CheckPull=true failed.");
        Check(e.PriorityTarget(20, Center) == 0, "Yan acquired PR target control absent from its previous bridge.");
        e.Synchronize([gold, Player, new(20, 0x4902, Center)], Center, Start.AddSeconds(1), playerId: 100);
        Check(e.Active != null && e.TreasureSackTowers.Count == 1, "Silver loss ended remaining gold.");
        e.Synchronize([Player, new(20, 0x4902, Center)], Center, Start.AddSeconds(2), playerId: 100);
        Check(e.Active == null, "Yan combat actor incorrectly kept module alive.");
    }
    private static void Goals()
    {
        var silver = Sack(1, false, Center + Vector3.UnitZ * 2);
        var gold = Sack(2, true, Center + Vector3.UnitX * 3);
        var e = Create(silver, gold);
        var g = Goal(e);
        Check(g.Position == gold.Position && g.Shape is CircleTemplate { Radius: 1 }
            && g is { Safe: true, Display: false, Navigate: true, GoalPriority: 10, OverrideDangerMargin: false, Activation: null },
            "Gold9 vs silver12 or immediate soft goal parameters differ.");
        gold = gold with { Position = Center + Vector3.UnitX * 4 };
        e.Synchronize([silver, gold, Player, new(20, 0x4902, Center)], Center, Start.AddSeconds(1), playerId: 100);
        Check(Goal(e).Position == silver.Position, "Gold16 incorrectly beats silver12.");
        e.StartCast(20, new(44269, Center + Vector3.UnitZ * 6, 0, Start.AddSeconds(1), Start.AddSeconds(5)), Start.AddSeconds(1));
        e.Tick(Start.AddSeconds(50));
        Check(Goal(e).Position == gold.Position, "Rush expired by predicted time or failed sack-center exclusion.");
        e.Finish(20, 44269, Start.AddSeconds(51), completed: false);
        Check(Goal(e).Position == silver.Position, "Rush actual cancel retained exclusion.");
        e = Create(Sack(1, false, Center + Vector3.UnitX), Sack(3, true, Center + Vector3.UnitZ * MathF.Sqrt(3)));
        Check(Goal(e).Position.Z > 101, "Strict weighted tie did not keep gold-first candidate.");
    }
    private static void Towers()
    {
        var p = Center + new Vector3(.012345f, 0, .04567f);
        var silver = Sack(1, false, p);
        var gold = Sack(2, true, p);
        var e = Create(silver, gold);
        Check(e.TreasureSackTowers.All(t => t.Position == p && t.Activation == Start.AddSeconds(10)), "Bootstrap quantized position or clock.");
        e.Tick(Start.AddSeconds(2));
        e.ObjectCreated(1, Start, 0x1EBE48, p);
        Check(e.TreasureSackTowers.Count == 2, "Duplicate Created appended a second tower.");
        var first = e.TreasureSackTowers[0].Order;
        e.RemoveActor(2);
        Check(e.TreasureSackTowers.Count == 1 && e.TreasureSackTowers[0].Order != first,
            "Destroy did not remove first coordinate match independently of actor identity.");
        e.RemoveActor(2);
        Check(e.TreasureSackTowers.Count == 1, "Duplicate Destroy consumed next equal-position tower.");
        var remaining = Sack(3, true, Center + Vector3.UnitZ * 10);
        e = Create(silver, remaining);
        e.Synchronize([silver with { Position = p + Vector3.UnitX * 5 }, remaining, Player], Center, Start.AddSeconds(3), playerId: 100);
        e.RemoveActor(1);
        Check(e.TreasureSackTowers.Count == 2 && e.TreasureSackTowers[0].Position == p, "Moved destruction force-removed original tower.");
        e.ObjectCreated(1, Start.AddSeconds(4), 0x1EBE48, p);
        Check(e.TreasureSackTowers.Count == 3, "Same ID new generation failed Created.");
    }
    private static void Soakers()
    {
        var silver = Sack(1, false, Center + Vector3.UnitX * 3);
        var e = Create(silver);
        DungeonAmbientAoe Tower() => e.AmbientAoes().Single(a => a.Id.StartsWith("treasure/sack/tower/"));
        Check(Tower() is { Safe: true, OutlineOnly: true, Navigate: false }
            && e.Hints(Center).Contains("Soak the tower!"), "Empty tower did not remain safe outline display-only.");
        var other = Player with { Id = 101, PartySlot = 1, Position = silver.Position, HitboxRadius = 10 };
        e.Synchronize([silver, Player, other], Center, Start.AddSeconds(1), playerId: 100);
        Check(Tower() is { Safe: false, OutlineOnly: false, Navigate: false }, "Full tower did not show solid default AOE.");
        e.Synchronize([silver, Player with { Position = silver.Position }, other], silver.Position, Start.AddSeconds(2), playerId: 100);
        Check(Tower() is { Safe: false, OutlineOnly: true }
            && e.Hints(silver.Position).Contains("Too many soakers in the tower!"), "Oversoak outline or text differs.");
        e.Synchronize([silver, Player with { Position = silver.Position }, other with { Dead = true }], silver.Position, Start.AddSeconds(3), playerId: 100);
        Check(Tower() is { Safe: true, OutlineOnly: true }, "Dead party member counted as soaker.");
        Check(e.AmbientAoes().Where(a => a.Id.StartsWith("treasure/enemy/")).All(a => a.Color == new Vector4(1, 1, 0, 1))
            && !e.PredictedDamage.Any(), "Yan marker palette or unexpected tower damage prediction.");
    }
    private static void Rush()
    {
        var e = Create(Sack(1, false, Center + Vector3.UnitZ * 15));
        var from = Center + new Vector3(.0321f, 0, .0789f);
        var to = Center + new Vector3(10, 0, 12);
        e.Synchronize([Sack(1, false, Center + Vector3.UnitZ * 15), new(20, 0x4902, from), Player], Center, Start, playerId: 100);
        e.StartCast(20, new(44269, to, .3f, Start, Start.AddSeconds(4.3)), Start);
        var c = e.Casts[20];
        var delta = to - from;
        Check(c.Rule.Shape is RayRectangleTemplate r && r.MaxLocalX == 4 && r.MinLocalX == -4
            && MathF.Abs(r.Length - new Vector2(delta.X, delta.Z).Length()) < .00001
            && MathF.Abs(c.Rotation - MathF.Atan2(delta.X, delta.Z)) < .00001 && c.Location != to,
            "Rush did not calculate raw delta before quantizing source.");
        e.Tick(Start.AddSeconds(60));
        Check(e.OutputCasts.Any(), "Rush expired without actual Finish.");
        e.Finish(20, 44269, Start.AddSeconds(61));
        Check(!e.OutputCasts.Any(), "Rush real Finish retained shape.");
    }
    private static void Corrections()
    {
        var silver = Sack(1, false, Center + Vector3.UnitZ * 15);
        var a = Sack(2, true, Center + Vector3.UnitX * 2);
        var b = Sack(3, true, Center - Vector3.UnitX * 2);
        var e = Create(silver, a, b);
        e.RemoveActor(2);
        e.Synchronize([silver, a, b, Player], Center, Start.AddSeconds(1), playerId: 100);
        e.ObjectCreated(2, Start.AddSeconds(1), 0x1EBE47, Center + Vector3.UnitZ * 2);
        Check(e.TreasureSackTowers.Last().Position == Center + Vector3.UnitZ * 2,
            "Create used a stale previous generation snapshot.");
        Check(Goal(e).Position == b.Position, "Deleted dictionary slot let a recreated equal-score sack take priority.");
        e.RemoveActor(1);
        e.Synchronize([silver, a, b, Player], Center, Start.AddSeconds(1.1), playerId: 100);
        var newPosition = Center + Vector3.UnitZ * 3;
        e.ObjectCreated(1, Start.AddSeconds(1.2), 0x1EBE47, newPosition);
        e.Synchronize([silver, a, b, Player], Center, Start.AddSeconds(1.3), playerId: 100);
        Check(e.TreasureSackTowers.Any(t => t.Position == newPosition), "Stale cross-OID raw destroyed a new Create.");
        e.Synchronize([silver with { Oid = 0x1EBE47, Position = newPosition }, a, b, Player],
            Center, Start.AddSeconds(1.4), playerId: 100);
        Check(e.TreasureSackTowers.Any(t => t.Position == newPosition), "New OID confirmation retained a destruction barrier.");

        var cast = new DungeonCastObservation(44269, Center + new Vector3(10, 0, 10), 0, Start, Start.AddSeconds(4.3));
        e = Create(silver);
        e.Synchronize([silver, Player, new(20, 0x4902, Center, Cast: cast)], Center, Start, playerId: 100);
        var initial = e.Casts[20];
        var moved = new DungeonActor(20, 0x4902, Center + Vector3.UnitX * 2, Cast: cast);
        e.Synchronize([silver, Player, moved], Center, Start.AddSeconds(.1), playerId: 100);
        e.StartCast(20, cast, Start.AddSeconds(.2));
        Check(e.Casts[20].Location == initial.Location && e.Casts[20].Rotation == initial.Rotation
            && e.Casts[20].Rule.Shape == initial.Rule.Shape, "Duplicate packet relocked after caster movement.");
        e.StartCast(20, cast with { Location = Center + new Vector3(20, 0, 20) }, Start.AddSeconds(.25));
        Check(e.Casts[20].Location == initial.Location && e.Casts[20].Rotation == initial.Rotation
            && e.Casts[20].Rule.Shape is RayRectangleTemplate { Length: var length }
            && MathF.Abs(length - MathF.Sqrt(800)) < .00001, "Collinear packet length correction was ignored.");
        var changed = cast with { Location = Center + new Vector3(10, 0, 15) };
        e.Synchronize([silver, Player, moved with { Cast = changed }], Center, Start.AddSeconds(.3), playerId: 100);
        Check(e.Casts[20].Location != initial.Location && e.Casts[20].Rotation != initial.Rotation,
            "Genuine observed CastInfo replacement failed to relock charge.");
    }
}
