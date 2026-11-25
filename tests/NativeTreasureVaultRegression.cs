using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

internal static class NativeTreasureVaultRegression
{
    private static readonly DateTime Start = new(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(100, 0, 100);
    private static readonly (uint Primary, uint Boss, uint Bait, uint Raidwide, int Rules)[] Modules =
        [(0x489E, 0x48A0, 43630, 43633, 14), (0x4895, 0x4897, 43607, 43608, 15), (0x489B, 0x489D, 43623, 43626, 14)];

    internal static void Run(Action<string, Action> run)
    {
        run("native treasure Vault trio complete catalog and exact source geometry", Geometry);
        run("native treasure Vault trio cached first boss and destroyed trash gate", Lifecycle);
        run("native treasure Vault trio original cache generation cannot be replaced", CacheGeneration);
        run("native treasure Vault trio priority and vulnerable differences", Rewards);
        run("native treasure Vault cast bait dynamic geometry and source party direction navigation", DynamicBaits);
        run("native treasure Vault cast bait self target has no current-target fallback", SelfAndMissing);
        run("native treasure Vault cast bait finish keeps FIFO until native source effect", BaitFifo);
        run("native treasure Vault cast bait target destruction freezes old identity and damage uses current roster", TargetIdentity);
        run("native treasure Vault cast bait source destroy and target death remove permanently", BaitDeath);
        run("native treasure Vault cast bait raw generations append and packet correction does not revive consumed", Generations);
        run("native treasure Vault trio actual raidwide has only current cast damage prediction", Raidwide);
        run("native treasure Vault frozen absent target survives packet correction and retains slot prediction", MissingTargetCorrection);
        run("native treasure Vault first raw correction cannot revive consumed bait receipt", ConsumedFirstRaw);
        run("native treasure Vault target OID replacement freezes old navigation identity permanently", OidReplacement);
    }
    private static DungeonActor Enemy(ulong id, uint oid, Vector3? position = null) =>
        new(id, oid, position ?? Center, InCombat: true, IsEnemy: true, ObjectIndex: (ushort)(id * 2), Hp: 100, MaxHp: 100);
    private static DungeonActor Player(ulong id, Vector3? position = null, bool dead = false) =>
        new(id, 0, position ?? Center + Vector3.UnitZ * 8, Dead: dead, Hostile: false, Player: true);
    private static DungeonEncounter Create(uint primary, uint boss)
    {
        var e = new DungeonEncounter(1279) { TreasurePartyInput = [100, 101] };
        e.Synchronize([Enemy(1, primary), Enemy(2, boss), Enemy(3, boss), Player(100), Player(101, Center + Vector3.UnitZ * 10)],
            Center + Vector3.UnitZ * 8, Start, playerId: 100);
        return e;
    }
    private static DungeonCastObservation Cast(uint action, ulong target = 100, double time = 0) =>
        new(action, Center + Vector3.UnitX * 30, 1.2f, Start.AddSeconds(time), Start.AddSeconds(time + 5.3), target);
    private static DungeonAmbientAoe[] Baits(DungeonEncounter e) => e.AmbientAoes().Where(a => a.Id.StartsWith("treasure/cast-bait/")).ToArray();
    private static DungeonAmbientAoe[] Markers(DungeonEncounter e) => e.AmbientAoes().Where(a => a.Id.StartsWith("treasure/enemy/")).ToArray();
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
    private static void Geometry()
    {
        foreach (var (primary, boss, bait, _, rules) in Modules)
        {
            var e = Create(primary, boss);
            Check(e.Active!.Casts.Length == rules && e.Active.Casts.Single(c => c.Action == bait).Shape == null
                && NativeTreasureCatalog.Owns(primary, 1279) && !NativeTreasureCatalog.Owns(boss, 1279),
                "Vault source ownership or complete action count differs.");
            Check(e.Arena!.Contains(new(119.49f, 0, 119.49f)) && !e.Arena.Contains(new(119.6f, 0, 100)), "Vault fixed square changed.");
        }
        var bat = Create(0x489E, 0x48A0).Active!.Casts.ToDictionary(c => c.Action);
        var fluid = Create(0x4895, 0x4897).Active!.Casts.ToDictionary(c => c.Action);
        var wind = Create(0x489B, 0x489D).Active!.Casts.ToDictionary(c => c.Action);
        Check(bat[43627].Shape is CircleTemplate { Radius: 3 } && bat[43632].Shape is FanTemplate { Radius: 40 } f
            && f.AngleRadians == MathF.PI / 4 && bat[43629].Shape is RayRectangleTemplate { Length: 40, MinLocalX: -4, MaxLocalX: 4 }
            && bat[43628].Shape == bat[43631].Shape, "Bat source geometry differs.");
        Check(fluid[43603].Shape is DonutTemplate { InnerRadius: 6, OuterRadius: 40 }
            && fluid[43609].Shape is FanTemplate { Radius: 39 } cone && cone.AngleRadians == MathF.PI / 6
            && fluid[43606].Shape == fluid[43605].Shape && fluid[43610].Shape is CircleTemplate { Radius: 5 }, "Fluid geometry differs.");
        Check(wind[43625].Shape is RayRectangleTemplate { Length: 60, MinLocalX: -20, MaxLocalX: 20 }
            && wind[43620].Shape is RayRectangleTemplate { Length: 14, MinLocalX: -2, MaxLocalX: 2 }
            && wind[43621].Shape is CircleTemplate { Radius: 4 } && wind[43622].Shape == wind[43624].Shape,
            "Wind source geometry differs.");
    }
    private static void Lifecycle()
    {
        foreach (var (primary, boss, _, _, _) in Modules)
        {
            var e = Create(primary, boss);
            var source = Enemy(1, primary);
            var giant = Enemy(2, boss);
            e.Synchronize([source with { Dead = true }, giant, Player(100)], Center, Start.AddSeconds(1), playerId: 100);
            Check(e.Active != null, "Dead trash corpse ended AllDestroyed.");
            e.Synchronize([giant, Player(100)], Center, Start.AddSeconds(2), playerId: 100);
            Check(e.Active != null && Markers(e).Length == 1, "Cached boss did not retain after trash destruction.");
            e.Synchronize([giant with { Dead = true }, Enemy(3, boss), Player(100)], Center, Start.AddSeconds(3), playerId: 100);
            Check(e.Active == null, "Later duplicate boss substituted for dead first cache.");
        }
    }
    private static void CacheGeneration()
    {
        foreach (var (primary, boss, _, _, _) in Modules)
        {
            var e = Create(primary, boss);
            e.RemoveActor(2, Start.AddSeconds(1));
            e.Synchronize([Enemy(1, primary), Enemy(2, boss), Player(100)], Center, Start.AddSeconds(2), playerId: 100);
            Check(Markers(e).Length == 1, "Same-ID recreated boss rebound cache.");
            e.Synchronize([Enemy(2, boss), Player(100)], Center, Start.AddSeconds(3), playerId: 100);
            Check(e.Active == null, "Recreated boss kept destroyed cache alive.");
            e = new(1279);
            e.Synchronize([Enemy(1, primary), Player(100)], Center, Start, playerId: 100);
            e.Synchronize([Enemy(2, boss), Player(100)], Center, Start.AddSeconds(1), playerId: 100);
            Check(e.Active == null, "First cache incorrectly preceded completion check.");
        }
    }
    private static void Rewards()
    {
        foreach (var (primary, boss, _, _, _) in Modules)
        {
            Check(Enumerable.Range(0, 5).All(i => NativeTreasureCatalog.Priority(primary, (uint)(0x48B9 + i)) == 6 - i)
                && NativeTreasureCatalog.Priority(primary, 0x48B8) == 1
                && NativeTreasureCatalog.Vulnerable(primary, 0x48B7) && !NativeTreasureCatalog.Vulnerable(primary, boss),
                "Reward priority/color differs.");
        }
        Check(NativeTreasureCatalog.Priority(0x489E, 0x48A0) == 2 && NativeTreasureCatalog.Priority(0x489E, 0x48B7) == 0
            && NativeTreasureCatalog.Priority(0x4895, 0x48B7) == 2 && NativeTreasureCatalog.Priority(0x4895, 0x4897) == 0
            && NativeTreasureCatalog.Priority(0x489B, 0x48B7) == 0, "Boss/Goldy special rank lost.");
    }
    private static void DynamicBaits()
    {
        foreach (var (primary, boss, action, _, _) in Modules)
        {
            var e = Create(primary, boss);
            var cast = Cast(action);
            e.StartCast(2, cast, Start);
            var display = Baits(e).Single(a => a.Display);
            Check(display.OutlineOnly && display.Position == Center && display.Rotation == 0
                && Baits(e).Single(a => a.Navigate).Shape is FanTemplate { Radius: 100 }
                && e.Hints(Center + Vector3.UnitZ * 8).Contains("Bait away from raid!"), "Initial dynamic bait or teammate navigation differs.");
            e.Synchronize([Enemy(1, primary), Enemy(2, boss) with { Cast = cast }, Player(100, Center + Vector3.UnitX * 8),
                Player(101, Center + Vector3.UnitX * 10)], Center + Vector3.UnitX * 8, Start.AddSeconds(1), playerId: 100);
            display = Baits(e).Single(a => a.Display);
            Check(display.Rotation == MathF.PI / 2 && display.Position == Center, "Bait used cast location/rotation instead of dynamic identities.");
            e.Synchronize([Enemy(1, primary), Enemy(2, boss) with { Cast = cast }, Player(100, Center + Vector3.UnitX * 8),
                Player(101, Center + Vector3.UnitX * 10)], Center + Vector3.UnitX * 10, Start.AddSeconds(2), playerId: 101);
            Check(!Baits(e).Single(a => a.Display).OutlineOnly && Baits(e).Single(a => a.Navigate).Shape.GetType() == display.Shape.GetType()
                && e.Hints(Center + Vector3.UnitX * 10).Contains("GTFO from baited aoe!"), "Viewing switch failed own/other branch.");
        }
    }
    private static void SelfAndMissing()
    {
        var e = Create(0x489E, 0x48A0);
        e.StartCast(2, Cast(43630, 999), Start);
        Check(!Baits(e).Any(), "Unresolved target unexpectedly queued.");
        e.Finish(2, 43630, Start.AddSeconds(.5));
        e.StartCast(2, Cast(43630, 2, 1), Start.AddSeconds(1));
        e.Synchronize([Enemy(1, 0x489E), Enemy(2, 0x48A0) with { Cast = Cast(43630, 2, 1), Rotation = .7f, TargetId = 100 },
            Player(100)], Center, Start.AddSeconds(2), playerId: 2);
        Check(Baits(e).Single(a => a.Display).Rotation == .7f && !Baits(e).Any(a => a.Navigate)
            && !e.PredictedDamage.Any(), "Self target fell back to enemy current target or generated self teammate zones.");
    }
    private static void BaitFifo()
    {
        var e = Create(0x489E, 0x48A0);
        e.StartCast(2, Cast(43630), Start);
        e.Finish(2, 43630, Start.AddSeconds(.5));
        e.StartCast(2, Cast(43630, 101, 1), Start.AddSeconds(1));
        e.Finish(2, 43630, Start.AddSeconds(2));
        e.Tick(Start.AddSeconds(100));
        Check(Baits(e).Count(a => a.Display) == 2 && e.PredictedDamage.Count() == 2
            && e.PredictedDamage.All(p => p.Activation == Start.AddSeconds(5.3)), "Finish/time cleared FIFO or union used per-target activation.");
        e.Effect(2, 43630, 1, Start.AddSeconds(100), 100);
        e.TreasureEffect(3, 43630, 1, 100, Start.AddSeconds(100));
        Check(Baits(e).Count(a => a.Display) == 2, "Expanded or other-source effect consumed bait.");
        e.TreasureEffect(2, 43630, 1, 999, Start.AddSeconds(101));
        Check(Baits(e).Count(a => a.Display) == 1 && e.PredictedDamage.Single().TargetId == 101
            && e.PredictedDamage.Single().Activation == Start.AddSeconds(6.3), "Native source FIFO depended on MainTarget or current cast.");
        e.TreasureEffect(2, 43630, 1, 999, Start.AddSeconds(102));
        Check(Baits(e).Count(a => a.Display) == 1, "Duplicate native effect consumed second bait.");
    }
    private static void TargetIdentity()
    {
        var e = Create(0x489E, 0x48A0);
        var cast = Cast(43630);
        e.StartCast(2, cast, Start);
        var oldRotation = Baits(e).Single(a => a.Display).Rotation;
        e.RemoveActor(100, Start.AddSeconds(.5));
        e.TreasurePartyInput = [101, 100];
        e.Synchronize([Enemy(1, 0x489E), Enemy(2, 0x48A0) with { Cast = cast }, Player(100, Center + Vector3.UnitX * 10), Player(101)],
            Center, Start.AddSeconds(1), playerId: 100);
        Check(Baits(e).Single(a => a.Display).OutlineOnly && Baits(e).Single(a => a.Display).Rotation == oldRotation
            && Baits(e).Single(a => a.Navigate).Id.EndsWith("/avoid") && e.PredictedDamage.Single().PartySlot == 1,
            "Destroyed target rebound, inherited navigation identity or lost current roster slot.");
    }
    private static void BaitDeath()
    {
        foreach (var targetDied in new[] { false, true })
        {
            var e = Create(0x4895, 0x4897);
            var cast = Cast(43607);
            e.StartCast(2, cast, Start);
            e.Synchronize([Enemy(1, 0x4895), Enemy(2, 0x4897) with { Cast = cast, Dead = !targetDied }, Player(100, dead: targetDied)],
                Center, Start.AddSeconds(1), playerId: 100);
            Check(!Baits(e).Any() && !e.PredictedDamage.Any(), "Dead source/target did not remove bait.");
            e.Synchronize([Enemy(1, 0x4895), Enemy(2, 0x4897) with { Cast = cast }, Player(100)],
                Center, Start.AddSeconds(2), playerId: 100);
            Check(!Baits(e).Any(), "Revival resurrected removed bait.");
        }
        var sourceDestroyed = Create(0x489B, 0x489D);
        sourceDestroyed.StartCast(2, Cast(43623), Start);
        sourceDestroyed.RemoveActor(2, Start.AddSeconds(1));
        Check(!Baits(sourceDestroyed).Any(), "Destroyed source retained bait.");
    }
    private static void Generations()
    {
        var e = Create(0x489E, 0x48A0);
        var cast = Cast(43630);
        e.StartCast(2, cast, Start, false);
        e.StartCast(2, cast with { Location = Center }, Start.AddSeconds(.1));
        Check(Baits(e).Count(a => a.Display) == 1, "Same-generation correction appended bait.");
        e.StartCast(2, cast with { TotalTime = 5.1f }, Start.AddSeconds(.2), false);
        Check(Baits(e).Count(a => a.Display) == 2, "Raw real change failed append behind old effect-ended bait.");
        e.TreasureEffect(2, 43630, 1, 0, Start.AddSeconds(.3));
        e.TreasureEffect(2, 43630, 2, 0, Start.AddSeconds(.4));
        e.StartCast(2, cast with { Location = Center + Vector3.UnitX }, Start.AddSeconds(.5));
        Check(!Baits(e).Any(), "Packet correction revived already consumed bait.");
    }
    private static void Raidwide()
    {
        foreach (var (primary, boss, _, action, _) in Modules)
        {
            var e = Create(primary, boss);
            e.StartCast(2, Cast(action, 0), Start);
            Check(e.PredictedDamage.Count() == 2 && !e.OutputCasts.Any(c => c.Value.Rule.Shape != null), "Raidwide became spatial danger or lost damage.");
            e.Finish(2, action, Start.AddSeconds(1));
            Check(!e.PredictedDamage.Any(), "Actual raidwide Finish retained prediction.");
        }
    }
    private static void MissingTargetCorrection()
    {
        var e = Create(0x489E, 0x48A0);
        var cast = Cast(43630);
        e.StartCast(2, cast, Start);
        e.RemoveActor(100, Start.AddSeconds(.1));
        e.Synchronize([Enemy(1, 0x489E), Enemy(2, 0x48A0) with { Cast = cast }, Player(101)],
            Center, Start.AddSeconds(.2), playerId: 100);
        e.StartCast(2, cast with { Location = Center }, Start.AddSeconds(.3));
        Check(Baits(e).Count(a => a.Display) == 1 && Baits(e).Single(a => a.Navigate).Id.EndsWith("/avoid")
            && e.PredictedDamage.Single().TargetId == 100 && e.PredictedDamage.Single().PartySlot == 0,
            "Absent frozen target was erased by correction or missing Actor filtered retained roster prediction.");
    }
    private static void ConsumedFirstRaw()
    {
        var e = Create(0x489E, 0x48A0);
        var cast = Cast(43630);
        e.StartCast(2, cast, Start);
        e.TreasureEffect(2, 43630, 1, 0, Start.AddSeconds(.1));
        e.StartCast(2, cast with { TargetId = 101 }, Start.AddSeconds(.2), false);
        Check(!Baits(e).Any(), "First raw target correction revived consumed accepted generation.");
        e = Create(0x489E, 0x48A0);
        e.StartCast(2, cast with { TargetId = 999 }, Start);
        e.StartCast(2, cast, Start.AddSeconds(.1), false);
        Check(Baits(e).Count(a => a.Display) == 1, "First raw correction failed initially unparsed target without a consumed receipt.");
    }
    private static void OidReplacement()
    {
        var e = Create(0x489E, 0x48A0);
        var cast = Cast(43630);
        e.StartCast(2, cast, Start);
        e.Synchronize([Enemy(1, 0x489E), Enemy(2, 0x48A0) with { Cast = cast }, Player(100, Center + Vector3.UnitX * 9) with { Oid = 123 }, Player(101)],
            Center, Start.AddSeconds(.1), playerId: 100);
        e.Synchronize([Enemy(1, 0x489E), Enemy(2, 0x48A0) with { Cast = cast }, Player(100, Center + Vector3.UnitX * 9), Player(101)],
            Center, Start.AddSeconds(.2), playerId: 100);
        Check(Baits(e).Single(a => a.Display).Rotation == 0 && Baits(e).Single(a => a.Navigate).Id.EndsWith("/avoid"),
            "OID replacement rebound old target reference after original OID returned.");
    }
}
