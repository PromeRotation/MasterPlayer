using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

internal static class NativeTreasureRegression
{
    private static readonly DateTime Start = new(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(100, 0, 100);

    internal static void Run(Action<string, Action> run)
    {
        run("native treasure Gilded exact square and seven active components", Geometry);
        run("native treasure observed end, packet correction and helper cancellation", ObservedEnd);
        run("native treasure raidwide and tankbuster prediction without danger circle", Predictions);
        run("native treasure explicit priorities, Pointless, height and legal object slots", Priorities);
        run("native treasure primary death and destruction preserve bonus phase", Lifecycle);
        run("native treasure enemy colors and square projection stay display only", Markers);
        run("native treasure ownership covers migrated primary only", Ownership);
        run("native treasure altar polygon and every source geometry", Altars);
        run("native treasure delayable damage retains target queue through cancel and removal", Delayable);
        run("native treasure SingleTargetCast reads current target and only self falls back", CurrentTarget);
        run("native treasure altar ordered rewards and last add phase cleanup", AltarLifecycle);
        run("native treasure quick recast and resolved generation receipts", DelayableGenerations);
        run("native treasure real raw target handoff appends old and new predictions", RawHandoff);
        run("native treasure current raidwide and tankbuster clocks stay dynamic", CurrentClocks);
        run("native treasure Gymnasion source geometry and active actions", GymnasionGeometry);
        run("native treasure Gymnasion SingleTargetCast uses current target and no delayed queue", GymnasionTargets);
        run("native treasure Lampas insertion FIFO limits navigation and danger colors", LampasLights);
        run("native treasure Gymnasion reward ranks colors and All group lifetime", GymnasionRewards);
        run("native treasure Oubliettes four complete source geometries and inactive actions", OublietteGeometry);
        run("native treasure Oubliettes distinct reward ranks and excluded helper lifetime", OublietteRewards);
        run("native treasure Oubliettes clone snapshots and independent cast clocks", OublietteClones);
        run("native treasure Oubliettes current unavoidable clocks and native eight slots", OublietteDamage);
        run("native treasure Bull exact cut polygon and eighteen source action rules", BullGeometry);
        run("native treasure Bull reward lifetime and entrance marker clamp", BullRewards);
        run("native treasure remaining Gymnasion exact active geometry and rewards", GymnasionRemaining);
        run("native treasure Tigris locked eyes and observed-end facing", TigrisGaze);
        run("native treasure Satyros wind is an unfiltered dynamic obstacle", SatyrosWind);
        run("native treasure Triton current cast, native effect and unfiltered live handoff", TritonWaters);
        run("native treasure Sphinx spread target FIFO, movement, death and generation", SphinxSpreads);
        run("native treasure altar four source contracts and excluded helpers", AltarFour);
        run("native treasure Matanga multiple origins, casting, targeting and native party slots", MatangaCleaves);
        run("native treasure altar gaze range, locked origin and observed cancellation", AltarGazes);
        run("native treasure Arachne rings require independent real casts", ArachneRings);
        run("native treasure Korrigan Serpent Megakantha source geometry and reward sets", TreasureNextGeometry);
        run("native treasure Serpent water is EventState-filtered live hard obstacle", SerpentWater);
        run("native treasure Megakantha shared five-effect counter survives source cancellation", MegakanthaAtmosphere);
    }

    private static DungeonActor Enemy(ulong id, uint oid, ushort index = 2) => new(id, oid, Center,
        InCombat: true, IsEnemy: true, ObjectIndex: index, Hp: 100, MaxHp: 100);

    private static DungeonActor Player(ulong id = 100, int slot = 0) => new(id, 0, Center,
        Hostile: false, PartySlot: slot, Player: true);

    private static void AltarFour()
    {
        foreach (var (oid, count, helper) in new[] { (0x2535u, 11, 0x233Cu), (0x2536u, 13, 0x233Cu),
            (0x253Bu, 9, 0x253Cu), (0x253Fu, 13, 0x2572u) })
        {
            var encounter = new DungeonEncounter(794) { TreasurePartyInput = [100, 101] };
            var bonus = Enemy(2, 0x2545, 4);
            encounter.Synchronize([Enemy(1, oid), bonus, Enemy(3, helper, 6), Player(), Player(101, 1)],
                Center, Start, playerId: 100);
            var rules = encounter.Active!.Casts.ToDictionary(c => c.Action);
            Check(rules.Count == count && !rules.ContainsKey(8599) && !rules.ContainsKey(9636)
                && rules[5352].Shape is CircleTemplate { Radius: 6 }
                && rules[8598].Shape is FanTemplate { Radius: 8.42f } scritch && scritch.AngleRadians == 2 * MathF.PI / 3,
                "Activated components or Matanga cast geometry differs.");
            Check(NativeTreasureCatalog.Priority(oid, 0x2545) == (oid == 0x2535 ? 2 : 1)
                && NativeTreasureCatalog.Priority(oid, 0x9999) == 0 && NativeTreasureCatalog.Vulnerable(oid, 0x2545),
                "Bonus priority/color or explicit zero changed.");
            if (oid == 0x2536)
                Check(rules[13262].Shape is CircleTemplate { Radius: 15 } && rules[13260].Shape is RayRectangleTemplate { Length: 49.6f, MinLocalX: -3, MaxLocalX: 3 },
                    "Beast invented falloff navigation or wrong rectangle.");
            if (oid == 0x253F)
                Check(!rules.ContainsKey(13387) && rules[13388].Shape is CircleTemplate { Radius: 7 }
                    && rules[13390].Shape is RayRectangleTemplate { Length: 41.72f, MinLocalX: -1.5f, MaxLocalX: 1.5f }
                    && rules[13391].Shape is CircleTemplate { Radius: 10.72f }, "Older visual or Idol geometry differs.");
            Check(encounter.AmbientAoes().Count(a => a.Id.StartsWith("treasure/enemy/")) == 2, "Helper drawn as enemy.");
            encounter.Synchronize([Enemy(1, oid) with { Dead = true }, bonus, Enemy(3, helper, 6), Player()],
                Center, Start.AddSeconds(1), playerId: 100);
            Check(encounter.Active != null, "Matanga failed to retain All phase.");
            encounter.Synchronize([Enemy(3, helper, 6), Player()], Center, Start.AddSeconds(2), playerId: 100);
            Check(encounter.Active == null, "Excluded helper extended All phase.");
        }
    }

    private static void MatangaCleaves()
    {
        var encounter = new DungeonEncounter(794) { TreasurePartyInput = [100, 101, 102, 103] };
        var source = Enemy(2, 0x2545, 4) with { Position = Center - Vector3.UnitZ * 5, TargetId = 101,
            Cast = new(8598, Center, 0, Start, Start.AddSeconds(3)) };
        var second = Enemy(3, 0x2545, 6) with { Position = Center - Vector3.UnitX * 4, TargetId = 101 };
        var party = new[] { Player(), Player(101, 1), Player(102, 2) with { Dead = true }, Player(103, 3) };
        void Sync(double time, ulong view, params DungeonActor[] origins) => encounter.Synchronize(
            [Enemy(1, 0x2535), .. origins, .. party, Player(104, 4)], Center, Start.AddSeconds(time), playerId: view);
        DungeonAmbientAoe[] Cleaves() => encounter.AmbientAoes().Where(a => a.Id.StartsWith("treasure/cleave/")).ToArray();
        Sync(0, 100, source, second);
        Check(Cleaves().Count(a => a.Navigate) == 2 && Cleaves().Count(a => a.Display && a.OutlineOnly && !a.Navigate) == 2
            && encounter.Hints(Center).Contains("GTFO from cleave!"), "Missing live casting/multiple cleaves.");
        encounter.TreasureEffect(2, 8599, 1, 101);
        encounter.Tick(Start.AddSeconds(20));
        Check(Cleaves().Length == 4 && Cleaves().All(a => a.Activation == null), "Effect or time changed default NextExpected.");
        Sync(21, 101, source, second);
        Check(Cleaves().Count(a => a.Navigate) == 4 && Cleaves().Where(a => a.Navigate).All(a =>
            a.Shape is FanTemplate { Radius: 100 } && !a.Display && !a.Id.EndsWith("/102") && !a.Id.EndsWith("/104"))
            && !encounter.Hints(Center).Contains("GTFO from cleave!"), "Target bait used actor PartySlot instead of live native party.");
        Sync(22, 100, source with { Targetable = false }, second with { Dead = true });
        Check(Cleaves().Length == 0, "Dead/untargetable Matanga emitted cleave.");
        Sync(23, 100, source with { TargetId = 999 }, second with { TargetId = 999 });
        Check(Cleaves().Length == 0, "Missing target invented cleave.");
    }

    private static void AltarGazes()
    {
        foreach (var (oid, action) in new[] { (0x2536u, 13263u), (0x253Bu, 13784u), (0x3022u, 21674u) })
        {
            var encounter = new DungeonEncounter(oid == 0x3022 ? 924u : 794u);
            encounter.Synchronize([Enemy(1, oid), Player()], Center, Start, playerId: 100);
            var eye = Center + Vector3.UnitZ * 65;
            encounter.StartCast(1, new(action, eye, 0, Start, Start.AddSeconds(3)), Start);
            encounter.Tick(Start.AddSeconds(2.3));
            Check(encounter.GazeFacing(Center, 0) is float && encounter.Hints(Center).Contains("Turn away from gaze!")
                && encounter.AmbientAoes().Count(a => a.Id.StartsWith("treasure/gaze/")) == 3,
                "Gaze used comment radius rather than default 10000 or lost eye/view.");
            encounter.Tick(Start.AddSeconds(30));
            Check(encounter.Gazes.Single().Location == eye, "Gaze expired at predicted clock.");
            encounter.Finish(1, action, Start.AddSeconds(31), completed: false);
            Check(!encounter.Gazes.Any() && encounter.GazeFacing(Center, 0) == null, "Cancelled gaze retained facing.");
        }
    }

    private static void ArachneRings()
    {
        var encounter = new DungeonEncounter(794);
        encounter.Synchronize([Enemy(1, 0x253B), Enemy(2, 0x253C, 4) with { Targetable = false },
            Enemy(3, 0x2565, 6) with { Targetable = false }, Player()], Center, Start, playerId: 100);
        encounter.StartCast(2, new(13346, Center, 0, Start, Start.AddSeconds(3.5)), Start);
        encounter.Finish(2, 13346, Start.AddSeconds(1));
        Check(!encounter.OutputCasts.Any(), "First quake invented automatic rings.");
        encounter.StartCast(3, new(13344, Center + Vector3.UnitX, 0, Start.AddSeconds(2), Start.AddSeconds(5.5)), Start.AddSeconds(2));
        Check(encounter.OutputCasts.Single().Value.Rule.Shape is DonutTemplate { InnerRadius: 20, OuterRadius: 30 },
            "Third quake required previous stages or targetable helper.");
        encounter.Effect(3, 13344, 1, Start.AddSeconds(6));
        encounter.Tick(Start.AddSeconds(30));
        Check(encounter.OutputCasts.Count() == 1, "Effect/time cleared observed-end quake.");
        encounter.RemoveActor(3);
        Check(!encounter.OutputCasts.Any(), "Destroyed quake helper leaked.");
    }

    private static void TreasureNextGeometry()
    {
        foreach (var (territory, oid, count, bonus, add) in new[] {
            (924u, 0x3022u, 9, 0x301Du, 0x301Cu), (924u, 0x3025u, 13, 0x301Du, 0x3026u),
            (1123u, 0x3D33u, 12, 0x3D4Fu, 0x3D35u) })
        {
            var encounter = new DungeonEncounter(territory);
            encounter.Synchronize([Enemy(1, oid), Enemy(2, bonus, 4), Enemy(3, add, 6), Enemy(4, 0x233C, 8), Player()],
                Center, Start, playerId: 100);
            var rules = encounter.Active!.Casts.ToDictionary(c => c.Action);
            Check(rules.Count == count && NativeTreasureCatalog.Priority(oid, bonus) == (oid == 0x3022 ? 6 : 7)
                && NativeTreasureCatalog.Priority(oid, add) == 1 && NativeTreasureCatalog.Vulnerable(oid, bonus),
                "Source rules/priority/color differ.");
            if (oid == 0x3022)
                Check(rules[21675].Shape is CircleTemplate { Radius: 3 } && rules[21674].Shape == null
                    && rules[21673].Hint == null && !rules.ContainsKey(21770), "Korrigan geometry or delay duplicate differs.");
            else if (oid == 0x3025)
                Check(rules[21700].Shape is FanTemplate { Radius: 15.29f } cone && cone.AngleRadians == MathF.PI / 2
                    && rules[22771].Shape is FanTemplate { Radius: 13.45f }
                    && rules[21701].Shape is CircleTemplate { Radius: 8 } && rules[21702].Shape is CircleTemplate { Radius: 8 }
                    && !rules.ContainsKey(21770), "Serpent invented Inhale or changed fixed radius.");
            else
                Check(rules[32240].Shape is CircleTemplate { Radius: 8 }
                    && rules[32244].Shape is RayRectangleTemplate { Length: 15, MinLocalX: -2, MaxLocalX: 2 }
                    && rules[32237].Shape is FanTemplate { Radius: 12 }
                    && !rules.ContainsKey(32241) && !rules.ContainsKey(32239) && !rules.ContainsKey(32199),
                    "Megakantha active/visual boundary differs.");
            encounter.Synchronize([Enemy(2, bonus, 4), Enemy(4, 0x233C, 8), Player()], Center, Start.AddSeconds(1), playerId: 100);
            Check(encounter.Active != null, "Reward failed to preserve phase.");
            encounter.Synchronize([Enemy(4, 0x233C, 8), Player()], Center, Start.AddSeconds(2), playerId: 100);
            Check(encounter.Active == null, "Helper prolonged phase.");
        }
        var multi = Gymnasion(0x3D33, Enemy(2, 0x3D33, 4));
        Check(multi.AmbientAoes().Count(a => a.Id.StartsWith("treasure/enemy/")) == 2,
            "Megakantha Arena.Actors(rest) lost second primary-OID actor.");
    }

    private static void SerpentWater()
    {
        var water = Enemy(2, 0x1EA7D5, 4) with { Dead = true, Targetable = false };
        var encounter = Oubliette(0x3025, water);
        DungeonAmbientAoe[] Waters() => encounter.AmbientAoes().Where(a => a.Id.StartsWith("treasure/water/")).ToArray();
        Check(Waters().Single().Obstacle && Waters().Single().Shape is CircleTemplate { Radius: 7.5f }
            && encounter.Hints(Center).Contains("GTFO from voidzone!"), "Water filtered dead source or lost obstacle.");
        var moved = Center + new Vector3(1.23456f, 0, 2.34567f);
        encounter.Synchronize([Enemy(1, 0x3025), water with { Position = moved }, Player()], Center, Start.AddSeconds(1), playerId: 100);
        Check(Waters().Single().Position == moved, "Water quantized live position.");
        encounter.StartCast(1, new(21701, moved, 0, Start.AddSeconds(2), Start.AddSeconds(5)), Start.AddSeconds(2));
        Check(Waters().Length == 1 && encounter.OutputCasts.Single().Value.Rule.Shape is CircleTemplate { Radius: 8 },
            "Cast R8 and live R7.5 were merged.");
        encounter.Synchronize([Enemy(1, 0x3025), water with { EventState = 7 }, Player()], Center, Start.AddSeconds(6), playerId: 100);
        Check(Waters().Length == 0, "EventState7 water retained obstacle.");
        encounter.Synchronize([Enemy(1, 0x3025), water, Player()], Center, Start.AddSeconds(7), playerId: 100);
        Check(Waters().Length == 1, "Water did not return on EventState reset.");
        encounter.RemoveActor(2);
        Check(Waters().Length == 0, "Destroyed water retained obstacle.");
    }

    private static void MegakanthaAtmosphere()
    {
        var encounter = Gymnasion(0x3D33, Enemy(2, 0x233C, 4), Enemy(3, 0x233C, 6));
        DungeonAmbientAoe[] Atmosphere() => encounter.AmbientAoes().Where(a => a.Id == "treasure/odious").ToArray();
        var first = new DungeonCastObservation(32242, Center, .6f, Start, Start.AddSeconds(5.6));
        encounter.StartCast(2, first, Start);
        encounter.TreasureEffect(2, 32242, 1, 100);
        encounter.TreasureEffect(2, 32242, 1, 100);
        encounter.TreasureEffect(2, 33015, 2, 101);
        var next = first with { Location = Center + Vector3.UnitX * 3, Rotation = 1.2f };
        encounter.StartCast(3, next, Start);
        encounter.StartCast(2, first with { Location = Center - Vector3.UnitZ }, Start.AddSeconds(.1));
        Check(Atmosphere().Single().Position == next.Location && Atmosphere().Single().Rotation == next.Rotation,
            "Old-source correction replaced later global AOE.");
        encounter.Finish(3, 32242, Start.AddSeconds(1), completed: false);
        encounter.RemoveActor(3);
        encounter.Tick(Start.AddSeconds(30));
        Check(Atmosphere().Single().Activation == next.FinishAt && Atmosphere().Single().Shape is FanTemplate { Radius: 40 } half
            && half.AngleRadians == MathF.PI, "Cancellation/destroy/time cleared frozen half-circle.");
        encounter.TreasureEffect(99, 33016, 3, 0);
        encounter.TreasureEffect(99, 33015, 4, 0);
        Check(Atmosphere().Length == 1, "Start reset count or duplicate counted as a hit.");
        encounter.TreasureEffect(99, 33016, 5, 0);
        Check(Atmosphere().Length == 0, "Fifth global effect failed to clear AOE.");
        encounter.StartCast(2, first with { Location = Center + Vector3.UnitX }, Start.AddSeconds(31));
        Check(Atmosphere().Length == 0, "Same-generation correction resurrected resolved half-circle.");
        encounter.TreasureEffect(99, 33016, 6, 0);
        encounter.Finish(2, 32242, Start.AddSeconds(31));
        var fresh = first with { StartedAt = Start.AddSeconds(32), FinishAt = Start.AddSeconds(37.6) };
        encounter.StartCast(2, fresh, Start.AddSeconds(32));
        for (uint i = 7; i <= 9; ++i) encounter.TreasureEffect(99, 33015, i, 0);
        Check(Atmosphere().Length == 1, "Effect before new Start did not retain global counter.");
        encounter.TreasureEffect(99, 32242, 10, 0);
        Check(Atmosphere().Length == 0, "Counter did not reset after the fifth hit.");
        encounter.ResetFight();
        Check(!encounter.AmbientAoes().Any(), "Reset leaked atmosphere.");
    }

    private static DungeonEncounter Gymnasion(uint oid, params DungeonActor[] actors)
    {
        var encounter = new DungeonEncounter(1123) { TreasurePartyInput = [100, 101] };
        encounter.Synchronize([Enemy(1, oid), Player(), Player(101, 1), .. actors], Center, Start, playerId: 100);
        return encounter;
    }

    private static DungeonEncounter Oubliette(uint oid, params DungeonActor[] actors)
    {
        var encounter = new DungeonEncounter(924) { TreasurePartyInput = [100, 101] };
        encounter.Synchronize([Enemy(1, oid), Player(), Player(101, 1), .. actors], Center, Start, playerId: 100);
        return encounter;
    }

    private static void GymnasionRemaining()
    {
        foreach (var (oid, count, add, excluded) in new[]
        {
            (0x3D29u, 11, 0x3D2Au, 0x233Cu), (0x3D2Du, 5, 0x3D2Eu, 0x3D2Fu),
            (0x3D30u, 9, 0x3D31u, 0x3D32u), (0x3D3Bu, 14, 0x3D3Cu, 0x3D3Du),
        })
        {
            var encounter = Gymnasion(oid, Enemy(2, add, 4), Enemy(3, excluded, 6));
            var rules = encounter.Active!.Casts.ToDictionary(c => c.Action);
            Check(rules.Count == count && NativeTreasureCatalog.Owns(oid, 1123)
                && !NativeTreasureCatalog.Owns(excluded, 1123), "Remaining Gymnasion ownership or active rules differ.");
            Check(NativeTreasureCatalog.Priority(oid, add) == 1
                && encounter.AmbientAoes().Count(a => a.Id.StartsWith("treasure/enemy/")) == 2,
                "Excluded auxiliary entered enemy markers or add priority differs.");
            if (oid == 0x3D29)
                Check(rules[32208].Shape is FanTemplate { Radius: 45 } cone && cone.AngleRadians == MathF.PI / 2
                    && rules[32210].Shape is FanTemplate { Radius: 12 } claws && claws.AngleRadians == MathF.PI / 2
                    && rules[32209].Shape is CircleTemplate { Radius: 6 } && rules[32207].Shape == null
                    && NativeTreasureCatalog.Priority(oid, 0x3D4E) == 0 && NativeTreasureCatalog.Vulnerable(oid, 0x3D4E),
                    "Tigris source cone, gaze or Lyssa priority differs.");
            else if (oid == 0x3D2D)
                Check(rules[32221].Shape is FanTemplate { Radius: 40 } cone && cone.AngleRadians == MathF.PI / 2
                    && rules[32225].Shape is CircleTemplate { Radius: 15 }
                    && !rules.ContainsKey(32224) && !rules.ContainsKey(32227)
                    && NativeTreasureCatalog.Priority(oid, 0x3D4D) == 3,
                    "Satyros added unactivated actions or changed wing geometry.");
            else if (oid == 0x3D30)
                Check(rules[32230].Shape is FanTemplate { Radius: 40 } cone && cone.AngleRadians == MathF.PI / 3
                    && rules[32231].Shape is CircleTemplate { Radius: 13 }
                    && rules[32229].Shape == null && !rules.ContainsKey(32233) && !rules.ContainsKey(32234),
                    "Triton changed geometry or invented pull/imprisonment.");
            else
                Check(rules[32273].Shape is DonutTemplate { InnerRadius: 3, OuterRadius: 12 }
                    && rules[32270].Shape is DonutTemplate { InnerRadius: 12, OuterRadius: 60 }
                    && rules[32272].Shape is DungeonCrossTemplate { Length: 50, HalfWidth: 7 }
                    && rules[32274].Shape is RayRectangleTemplate { Length: 45, MinLocalX: -2.5f, MaxLocalX: 2.5f }
                    && rules[32269].Shape is CircleTemplate { Radius: 4 } && rules[32271].Shape == null
                    && rules[32266].Shape == null && !rules.ContainsKey(32267)
                    && NativeTreasureCatalog.Priority(oid, 0x3D4D) == 2,
                    "Sphinx source shapes, spread or unactivated action boundary differs.");
            encounter.Synchronize([Enemy(1, oid) with { Dead = true }, Enemy(2, add, 4), Enemy(3, excluded, 6), Player()],
                Center, Start.AddSeconds(1), playerId: 100);
            Check(encounter.Active != null, "Primary death ended remaining add.");
            encounter.Synchronize([Enemy(3, excluded, 6), Player()], Center, Start.AddSeconds(2), playerId: 100);
            Check(encounter.Active == null && !encounter.AmbientAoes().Any(), "Excluded auxiliary prolonged All group.");
        }
    }

    private static void TigrisGaze()
    {
        var position = Center + Vector3.UnitZ * 5;
        var cast = new DungeonCastObservation(32207, position, 0, Start, Start.AddSeconds(4.3));
        var boss = Enemy(1, 0x3D29) with { Position = position, Cast = cast };
        var encounter = Gymnasion(0x3D29);
        encounter.Synchronize([boss, Player()], Center, Start, playerId: 100);
        Check(encounter.Gazes.Single().Location == position && encounter.GazeFacing(Center, 0) == null
            && encounter.AmbientAoes().Count(a => a.Id.StartsWith("tigris/gaze/")) == 3
            && encounter.Hints(Center).Contains("Turn away from gaze!"), "Tigris missing eye/view/hint or early facing.");
        encounter.Synchronize([boss with { Position = Center + Vector3.UnitX * 12 }, Player()], Center,
            Start.AddSeconds(4), playerId: 100);
        var facing = encounter.GazeFacing(Center, 0);
        Check(facing != null && MathF.Cos(facing.Value) < .707107f && encounter.Gazes.Single().Location == position,
            "Tigris gaze followed moving caster or failed PR-facing intent.");
        encounter.Tick(Start.AddSeconds(20));
        Check(encounter.Gazes.Any(), "Expected time expired gaze.");
        encounter.Finish(1, 32207, Start.AddSeconds(20));
        Check(!encounter.Gazes.Any() && encounter.GazeFacing(Center, 0) == null
            && !encounter.AmbientAoes().Any(a => a.Id.StartsWith("tigris/gaze/")), "Tigris gaze leaked after finish.");
        encounter = Gymnasion(0x3D29, Enemy(2, 0x233C, 4), Enemy(3, 0x233C, 6), Enemy(4, 0x233C, 8));
        var angles = new[] { 0f, MathF.PI / 2, MathF.PI, 3 * MathF.PI / 2 + .01f };
        for (var i = 0; i < angles.Length; ++i)
            encounter.StartCast((ulong)i + 1, cast with
            {
                Location = Center + new Vector3(MathF.Sin(angles[i]) * 10, 0, MathF.Cos(angles[i]) * 10),
            }, Start);
        encounter.Tick(Start.AddSeconds(3.6));
        facing = encounter.GazeFacing(Center, 0);
        Check(facing is { } narrow && angles.All(a =>
            MathF.Abs(MathF.Atan2(MathF.Sin(narrow - a), MathF.Cos(narrow - a))) > MathF.PI / 4),
            "Extra facing margin erased the source's narrow multi-eye safe interval.");
    }

    private static void SatyrosWind()
    {
        var wind = Enemy(2, 0x3D2F, 4) with { Dead = true, Targetable = false, EventState = 7 };
        var encounter = Gymnasion(0x3D2D, wind);
        var aoe = encounter.AmbientAoes().Single(a => a.Id == "treasure/wind/2");
        Check(aoe.Shape is CircleTemplate { Radius: 4 } && aoe.Obstacle && aoe.Navigate && aoe.Display
            && aoe.Activation == null && encounter.Hints(Center).Contains("GTFO from voidzone!"),
            "Wind filtered dead/EventState7 source or became timed danger.");
        var moved = Center + new Vector3(2.1234f, 0, 3.4567f);
        encounter.Synchronize([Enemy(1, 0x3D2D), wind with { Position = moved }, Player()],
            Center, Start.AddSeconds(10), playerId: 100);
        Check(encounter.AmbientAoes().Single(a => a.Id == "treasure/wind/2").Position == moved,
            "Wind failed to track exact live position.");
        encounter.RemoveActor(2);
        Check(!encounter.AmbientAoes().Any(a => a.Id == "treasure/wind/2"), "Destroyed wind obstacle retained.");
    }

    private static void TritonWaters()
    {
        var cast = new DungeonCastObservation(32229, Center + Vector3.UnitX * 7, 0, Start, Start.AddSeconds(3.3), 101);
        var boss = Enemy(1, 0x3D30) with { Cast = cast };
        var target = Player(101, 1) with { Position = Center + Vector3.UnitZ * 8 };
        var encounter = Gymnasion(0x3D30);
        encounter.Synchronize([boss, Player(), target], Center, Start, playerId: 100);
        DungeonAmbientAoe[] Waters() => encounter.AmbientAoes().Where(a => a.Id.StartsWith("trial/voidzone/")).ToArray();
        Check(Waters().Single().Position == target.Position && Waters().Single().Activation == cast.FinishAt,
            "Triton cast prediction does not use current target or locked activation.");
        var moved = target with { Position = Center + Vector3.UnitX * 11 };
        encounter.Synchronize([boss with { Cast = cast with { FinishAt = cast.FinishAt.AddSeconds(.5) } }, Player(), moved],
            Center, Start.AddSeconds(1), playerId: 100);
        Check(Waters().Single().Position == moved.Position && Waters().Single().Activation == cast.FinishAt,
            "Triton cast center froze or activation followed current clock.");
        encounter.Finish(1, 32229, Start.AddSeconds(2));
        Check(Waters().Length == 0, "Cancelled Triton cast invented event prediction.");
        encounter.TreasureEffect(1, 32229, 1, 101, Start.AddSeconds(4), Center);
        encounter.TreasureEffect(1, 32229, 2, 0, Start.AddSeconds(5), moved.Position);
        encounter.Effect(1, 32229, 3, Start.AddSeconds(6), 100, Center);
        encounter.Tick(Start.AddSeconds(30));
        Check(Waters().Length == 2 && Waters().All(a => a.Shape is CircleTemplate { Radius: 5 } && a.Position == moved.Position)
            && Waters()[0].Activation == Start.AddSeconds(4).AddSeconds((double)1.4f),
            "Triton event main target/fallback, processing clock, dedup or indefinite lifetime differs.");
        var bubble = Enemy(3, 0x3D32, 6) with { Position = moved.Position + Vector3.UnitX * 2, Dead = true, Targetable = false, EventState = 7 };
        encounter.Synchronize([boss with { Cast = null }, Player(), moved, bubble], Center, Start.AddSeconds(31), playerId: 100);
        Check(Waters().Count(a => a.Id.Contains("/event/")) == 1 && Waters().Count(a => a.Id.Contains("/live/")) == 1,
            "Bubble did not consume exactly first matching prediction at distance two.");
        encounter.Synchronize([boss with { Cast = null }, Player(), moved, bubble], Center, Start.AddSeconds(32), playerId: 100);
        Check(Waters().Length == 1 && Waters().Single().Id.EndsWith("/live/3"), "Second update failed one-per-live handoff.");
        encounter.RemoveActor(3);
        Check(Waters().Length == 0, "Destroyed bubble retained live output.");
    }

    private static void SphinxSpreads()
    {
        var target = Player(101, 1) with { Position = Center + Vector3.UnitX * 4 };
        var first = new DungeonCastObservation(32271, Center, 0, Start, Start.AddSeconds(3.3), 101);
        var second = first with { FinishAt = Start.AddSeconds(6.3) };
        var helper = Enemy(2, 0x233C, 4) with { Cast = first };
        var other = Enemy(3, 0x233C, 6) with { Cast = second };
        var encounter = Gymnasion(0x3D3B);
        encounter.Synchronize([Enemy(1, 0x3D3B), helper, other, Player(), target], Center, Start, playerId: 100);
        DungeonAmbientAoe[] Spreads() => encounter.AmbientAoes().Where(a => a.Id.StartsWith("quest/spread/")).ToArray();
        Check(Spreads().Count(a => a.Display && !a.Navigate && a.OutlineOnly && a.Shape is CircleTemplate { Radius: 6 }) == 2
            && Spreads().Count(a => a.Navigate && !a.Display && a.Shape is CircleTemplate { Radius: 7 }) == 2
            && !encounter.PredictedDamage.Any(), "Sphinx spread display/navigation radii or invented prediction.");
        encounter.Effect(2, 32271, 1, Start.AddSeconds(.5), 101);
        Check(Spreads().Count(a => a.Display) == 2, "Sphinx effect prematurely resolved spread.");
        encounter.Finish(3, 32271, Start.AddSeconds(1));
        Check(Spreads().Single(a => a.Navigate).Activation == second.FinishAt,
            "Sphinx finish removed by helper instead of first matching target.");
        var moved = target with { Position = Center + Vector3.UnitZ * 9 };
        encounter.Synchronize([Enemy(1, 0x3D3B), helper, Player(), moved], Center, Start.AddSeconds(1.1), playerId: 100);
        var frozenPosition = Spreads().Single(a => a.Display).Position;
        Check(Vector3.Distance(frozenPosition, moved.Position) < .05f, "Spread failed to follow saved actor.");
        encounter.RemoveActor(101);
        encounter.Synchronize([Enemy(1, 0x3D3B), helper, Player(), moved with { Position = Center + Vector3.UnitX * 15 }],
            Center, Start.AddSeconds(1.2), playerId: 100);
        Check(Spreads().Single(a => a.Display).Position == frozenPosition, "Same-ID recreation rebound destroyed spread target.");
        encounter.Finish(2, 32271, Start.AddSeconds(2));
        Check(Spreads().Length == 0, "Target FIFO retained spread after final finish.");
    }

    private static void BullGeometry()
    {
        var center = new Vector3(0, 5, -372);
        var encounter = new DungeonEncounter(1209) { TreasurePartyInput = [100] };
        encounter.Synchronize([Enemy(1, 0x4305) with { Position = center }, Player() with { Position = center }],
            center, Start, playerId: 100);
        var arena = encounter.Arena!;
        Check(arena.Contains(new(0, 5, -353.66f), 0) && !arena.Contains(new(0, 5, -353.64f), 0)
            && arena.Contains(new(19.55f, 5, -372), 0) && arena.Contains(new(0, 5, -391.55f), 0)
            && MathF.Abs(arena.Position.Z - -372.62218f) < .002f && arena.Position.Y == 5,
            "Cenote radius, cut direction or post-difference center differs.");
        var rules = encounter.Active!.Casts.ToDictionary(c => c.Action);
        Check(rules.Count == 18 && rules[38261].Shape == null && rules[38262].Shape == null
            && new[] { 38249u, 38250u }.All(a => rules[a].Shape is RayRectangleTemplate { Length: 40, MinLocalX: -5, MaxLocalX: 5 })
            && new[] { 38251u, 38255u }.All(a => rules[a].Shape is RayRectangleTemplate { Length: 40, MinLocalX: -2.5f, MaxLocalX: 2.5f })
            && new[] { 38253u, 38254u }.All(a => rules[a].Shape is DungeonCrossTemplate { Length: 20, HalfWidth: 5 })
            && rules[38258].Shape is RayRectangleTemplate { Length: 44, MinLocalX: -5, MaxLocalX: 5 }
            && rules[38260].Shape is CircleTemplate { Radius: 6 }
            && rules[38279].Shape is CircleTemplate { Radius: 11 }
            && rules[38277].Shape is CircleTemplate { Radius: 6 }
            && rules[38278].Shape is FanTemplate { Radius: 15 } scoop && scoop.AngleRadians == 2 * MathF.PI / 3
            && Enumerable.Range(32301, 5).All(a => rules[(uint)a].Shape is CircleTemplate { Radius: 7 })
            && !rules.ContainsKey(38334) && !rules.ContainsKey(38280) && !rules.ContainsKey(38248),
            "Bull source components or inactive action boundary differs.");
        Check(NativeTreasureCatalog.Owns(0x4305, 1209) && NativeTreasureCatalog.Owns(0x4306, 1209)
            && ArrDungeonCatalog.IsScript("masterplayer.treasure.cenote-ja-ja-gural"),
            "Cenote native ownership omits Bull or Golden Molter.");
    }

    private static void BullRewards()
    {
        var center = new Vector3(0, 5, -372);
        var boss = Enemy(1, 0x4305) with { Position = center };
        var reward = Enemy(2, 0x4300, 4) with { Position = new(1, 5, -350) };
        var axis = Enemy(3, 0x42FE, 6) with { Position = new(0, 5, -350) };
        var helper = Enemy(4, 0x233C, 8);
        var encounter = new DungeonEncounter(1209) { TreasurePartyInput = [100] };
        encounter.Synchronize([boss, reward, axis, helper, Player()], center, Start, playerId: 100);
        var marker = encounter.AmbientAoes().Single(a => a.Id.EndsWith("enemy/2"));
        Check(marker.OutlineOnly && marker.Color == new Vector4(1, 0, 1, 1)
            && MathF.Abs(marker.Position.X - 1) < .001f && MathF.Abs(marker.Position.Z - -353.65f) < .002f
            && encounter.AmbientAoes().Single(a => a.Id.EndsWith("enemy/3")).Position == axis.Position,
            "Cenote cut-edge projection or source center-axis exception differs.");
        Check(encounter.PriorityTarget(1, center) == 2
            && Enumerable.Range(0, 5).All(i => NativeTreasureCatalog.Priority(0x4305, (uint)(0x4300 + i)) == 6 - i)
            && NativeTreasureCatalog.Priority(0x4305, 0x42FE) == 2
            && NativeTreasureCatalog.Priority(0x4305, 0x42FF) == 1, "Bull source reward priority differs.");
        encounter.Synchronize([boss with { Dead = true }, reward, axis, helper, Player()], center, Start.AddSeconds(1), playerId: 100);
        Check(encounter.Active != null, "Bull primary death ended live rewards.");
        encounter.Synchronize([reward with { Dead = true }, axis with { Dead = true }, helper, Player()],
            center, Start.AddSeconds(2), playerId: 100);
        Check(encounter.Active == null && !encounter.AmbientAoes().Any(), "Helper prolonged Bull All group.");
    }

    private static void OublietteGeometry()
    {
        foreach (var (oid, count) in new[] { (0x3011u, 14), (0x302Bu, 9), (0x3016u, 12), (0x3018u, 15) })
        {
            var encounter = Oubliette(oid);
            var rules = encounter.Active!.Casts.ToDictionary(c => c.Action);
            Check(rules.Count == count && encounter.Arena!.GetContours().Single().Count == 48
                && rules[21769].Shape is CircleTemplate { Radius: 11 }
                && rules[21767].Shape is RayRectangleTemplate { Length: 13, MinLocalX: -2, MaxLocalX: 2 }
                && rules[21768].Shape is FanTemplate { Radius: 15 } scoop && scoop.AngleRadians == 2 * MathF.PI / 3
                && !rules.ContainsKey(21770), "Oubliette shared source components differ.");
            Check(oid == 0x302B ? !rules.ContainsKey(6448)
                : Enumerable.Range(6448, 5).All(a => rules[(uint)a].Shape is CircleTemplate { Radius: 6.84f }),
                "Unactivated Swallow mandragora component or wrong shared radius.");
            if (oid == 0x3011)
                Check(rules[21663].Shape is FanTemplate { Radius: 44 } hydrofan && hydrofan.AngleRadians == MathF.PI / 6
                    && rules[21659].Shape is FanTemplate { Radius: 30 } hypno && hypno.AngleRadians == 2 * MathF.PI / 3
                    && rules[21660].Shape is RayRectangleTemplate { Length: 12, MinLocalX: -2, MaxLocalX: 2 }
                    && rules[21664].Shape is CircleTemplate { Radius: 8 } && rules[21658].Shape is CircleTemplate { Radius: 8 }
                    && rules[21662].Shape == null && !rules.ContainsKey(21661), "Undine source geometry differs.");
            else if (oid == 0x302B)
                Check(rules[21719].Shape is CircleTemplate { Radius: 11 }
                    && rules[21720].Shape is DonutTemplate { InnerRadius: 8, OuterRadius: 60 }
                    && rules[21717].Shape is RayRectangleTemplate { Length: 24, MinLocalX: -3, MaxLocalX: 3 }
                    && rules[21766].Shape is CircleTemplate { Radius: 8 } && rules[21716].Shape == null
                    && rules[21718].Shape is CircleTemplate { Radius: 10 }, "Swallow AIDs were swapped or extra knockback invented.");
            else if (oid == 0x3016)
                Check(rules[21667].Shape is RayRectangleTemplate { Length: 47, MinLocalX: -3, MaxLocalX: 3 }
                    && rules[21668].Shape is CircleTemplate { Radius: 12 }
                    && rules[21666].Shape == null && rules[21666].Hint == "Calls thunderclouds"
                    && rules[21669].Shape is CircleTemplate { Radius: 8 } && !rules.ContainsKey(21665),
                    "Pegasus invented post CloudCall charge or changed component.");
            else
                Check(rules[21686].Shape is RayRectangleTemplate { Length: 40, MinLocalX: -4, MaxLocalX: 4 }
                    && rules[21684].Shape is CircleTemplate { Radius: 6 } && rules[21682].Shape == null
                    && new[] { 22779u, 21683u }.All(a => rules[a].Shape is CircleTemplate { Radius: 12 })
                    && new[] { 22780u, 21685u }.All(a => rules[a].Shape is FanTemplate { Radius: 30 } cone
                        && cone.AngleRadians == 2 * MathF.PI / 3), "Pixie grouped clone shapes differ.");
        }
        Check(ArrDungeonCatalog.IsScript("masterplayer.treasure.shifting-oubliettes")
            && !NativeTreasureCatalog.Owns(0x3017, 924) && !NativeTreasureCatalog.Owns(0x3011, 1123),
            "Oubliette ownership includes auxiliary or wrong territory.");
    }

    private static void OublietteRewards()
    {
        foreach (var (oid, add, excluded, onion, tomato, queen, fuath, keeper) in new[]
        {
            (0x3011u, 0x3013u, 0x3012u, 7, 4, 3, 3, 2),
            (0x302Bu, 0x302Cu, 0x233Cu, 7, 4, 3, 3, 2),
            (0x3016u, 0u, 0x3017u, 5, 2, 1, 2, 1),
            (0x3018u, 0x3019u, 0x304Du, 6, 3, 2, 3, 2),
        })
        {
            Check(NativeTreasureCatalog.Priority(oid, 0x301D) == onion
                && NativeTreasureCatalog.Priority(oid, 0x301E) == onion - 1
                && NativeTreasureCatalog.Priority(oid, 0x301F) == onion - 2
                && NativeTreasureCatalog.Priority(oid, 0x3020) == tomato
                && NativeTreasureCatalog.Priority(oid, 0x3021) == queen
                && NativeTreasureCatalog.Priority(oid, 0x3033) == fuath
                && NativeTreasureCatalog.Priority(oid, 0x3034) == keeper,
                "Distinct Oubliette priority tables were conflated.");
            var reward = Enemy(2, 0x301D, 4) with { InCombat = false, Targetable = false };
            var auxiliary = Enemy(3, excluded, 6);
            var encounter = Oubliette(oid, reward, auxiliary);
            Check(encounter.AmbientAoes().Count(a => a.Id.StartsWith("treasure/enemy/")) == 1,
                "Untargetable reward or excluded auxiliary drawn as enemy.");
            encounter.Synchronize([Enemy(1, oid) with { Dead = true }, reward, auxiliary, Player()],
                Center, Start.AddSeconds(1), playerId: 100);
            Check(encounter.Active != null, "Untargetable live reward failed All lifetime.");
            encounter.Synchronize([reward with { Dead = true }, auxiliary, Player()],
                Center, Start.AddSeconds(2), playerId: 100);
            Check(encounter.Active == null, "Excluded Bubble/Helper/Thunderhead/Double extended All lifetime.");
            if (add == 0) continue;
            encounter = Oubliette(oid, Enemy(2, add, 4));
            Check(encounter.PriorityTarget(1, Center) == 2
                && encounter.AmbientAoes().Single(a => a.Id.EndsWith("enemy/2")).Color == new Vector4(1, 0, 0, 1),
                "Ordinary add priority or color differs from Vulnerable reward.");
        }
    }

    private static void OublietteClones()
    {
        var source = Enemy(2, 0x304C, 4) with { Targetable = false };
        var encounter = Oubliette(0x3018, source);
        encounter.StartCast(1, new(21685, Center, 0, Start, Start.AddSeconds(5.3)), Start);
        encounter.StartCast(2, new(22780, Center + Vector3.UnitX * 5, MathF.PI, Start, Start.AddSeconds(7.3)), Start);
        Check(encounter.OutputCasts.Count() == 2 && encounter.Casts[1].FinishAt != encounter.Casts[2].FinishAt,
            "Grouped clone casts collapsed to one activation or required targetability.");
        encounter.Finish(2, 21685, Start.AddSeconds(1), completed: false);
        Check(encounter.Casts.ContainsKey(2), "Old other-action cancel cleared clone generation.");
        encounter.Finish(1, 21685, Start.AddSeconds(5.3), completed: true);
        Check(encounter.OutputCasts.Single().Value is { Source: 2 } clone && clone.Location == Center + Vector3.UnitX * 5
            && clone.Rotation == MathF.PI && clone.FinishAt == Start.AddSeconds(7.3), "Clone clock or snapshot lost after primary finish.");
        encounter.RemoveActor(2);
        Check(!encounter.OutputCasts.Any(), "Removed clone geometry leaked.");
        encounter = Oubliette(0x3016, Enemy(2, 0x3017, 4) with { Targetable = false });
        encounter.StartCast(2, new(21669, Center, 0, Start, Start.AddSeconds(3.3)), Start);
        Check(encounter.OutputCasts.Count() == 1, "Thunderhead required inactive CloudCall predecessor.");
    }

    private static void OublietteDamage()
    {
        foreach (var (oid, action, raidwide) in new[] { (0x3011u, 21662u, true), (0x3018u, 21682u, true), (0x302Bu, 21716u, false) })
        {
            var encounter = Oubliette(oid, Enemy(2, 0x233C, 4));
            var cast = new DungeonCastObservation(action, Center, 0, Start, Start.AddSeconds(4.3), 100);
            DungeonActor[] Frame(DungeonCastObservation? observation) =>
            [
                Enemy(1, oid), Enemy(2, 0x233C, 4) with { Cast = observation }, Player(), Player(101, 7),
                Player(102, 2) with { Dead = true }, Player(103, 8),
            ];
            encounter.TreasurePartyInput = [100, 101, 102];
            encounter.Synchronize(Frame(cast), Center, Start, playerId: 100);
            var damage = encounter.PredictedDamage.ToArray();
            Check(damage.Length == (raidwide ? 2 : 1) && damage.All(p => p.DamageType == (raidwide ? "Raidwide" : "Tankbuster"))
                && !encounter.OutputCasts.Any(c => c.Value.Rule.Shape != null), "Unavoidable predicted twice or included dead/alliance/geometry.");
            encounter.Synchronize(Frame(cast with { FinishAt = Start.AddSeconds(4.8) }), Center, Start.AddSeconds(.1), playerId: 100);
            Check(encounter.Casts[2].FinishAt == cast.FinishAt && encounter.PredictedDamage.All(p => p.Activation == Start.AddSeconds(4.8)),
                "Oubliette current clock-only update used cached activation.");
            encounter.Synchronize(Frame(null), Center, Start.AddSeconds(.2), playerId: 100);
            Check(!encounter.PredictedDamage.Any(), "Observed unavoidable cancel retained damage.");
        }
    }

    private static void GymnasionGeometry()
    {
        var mandragoras = Gymnasion(0x3D41);
        var rules = mandragoras.Active!.Casts.ToDictionary(c => c.Action);
        Check(rules.Count == 8 && rules[32297].Shape == null && rules[32300].Hint == "Calls adds"
            && rules[32299].Shape is CircleTemplate { Radius: 3 }
            && Enumerable.Range(32301, 5).All(a => rules[(uint)a].Shape is CircleTemplate { Radius: 7 }),
            "Gymnastic source group or radius differs.");
        var leon = Gymnasion(0x3D27);
        rules = leon.Active!.Casts.ToDictionary(c => c.Action);
        Check(rules.Count == 12 && rules[32204].Shape is RayRectangleTemplate { Length: 46, MinLocalX: -10, MaxLocalX: 10 }
            && rules[32201].Shape is CircleTemplate { Radius: 12 } && rules[32816].Shape is CircleTemplate { Radius: 12 }
            && rules[32205].Shape is FanTemplate { Radius: 8 } cone && cone.AngleRadians == 2 * MathF.PI / 3
            && rules[32203].Shape is CircleTemplate { Radius: 8 } && rules[32317].Shape is CircleTemplate { Radius: 6 },
            "Leon source component differs.");
        leon.StartCast(1, new(32816, Center, 0, Start, Start.AddSeconds(7.3)), Start);
        Check(!leon.OutputCasts.Single().Value.Rule.Shape!.Resolve(Center, 0, null).Contains(Center + Vector3.UnitX * 13),
            "Flare Star comment expanded active R12 geometry to R40.");
        var lampas = Gymnasion(0x3D40);
        rules = lampas.Active!.Casts.ToDictionary(c => c.Action);
        Check(rules.Count == 4 && rules[32292].Shape is CircleTemplate { Radius: 5 }
            && rules[32294].Shape is FanTemplate { Radius: 40 } light && light.AngleRadians == MathF.PI / 3
            && rules[32288].Hint == "Calls bonus adds" && !rules.ContainsKey(32293),
            "Lampas geometry or visual action ownership differs.");
        foreach (var encounter in new[] { mandragoras, leon, lampas })
            Check(encounter.Arena!.GetContours().Single().Count == 48 && encounter.GazeFacing(Center, 0) == null,
                "Gymnasion changed polygon or invented facing.");
        Check(ArrDungeonCatalog.IsScript("masterplayer.treasure.shifting-gymnasion")
            && NativeTreasureCatalog.Owns(0x3D40, 1123) && !NativeTreasureCatalog.Owns(0x3D4D, 1123)
            && !NativeTreasureCatalog.Owns(0x3D32, 1123), "Gymnasion ownership expanded to reward or bubble.");
    }

    private static void GymnasionTargets()
    {
        foreach (var (oid, action) in new[] { (0x3D41u, 32297u), (0x3D27u, 32200u), (0x3D40u, 32290u) })
        {
            var boss = Enemy(1, oid);
            var caster = action == 32290 ? Enemy(2, 0x233C, 4) : boss;
            var cast = new DungeonCastObservation(action, Center, 0, Start, Start.AddSeconds(5.3), 100);
            var encounter = Gymnasion(oid, caster.Id == boss.Id ? [] : [caster]);
            DungeonActor[] Frame(DungeonCastObservation observation) => caster.Id == boss.Id
                ? [boss with { Cast = observation }, Player(), Player(101, 1)]
                : [boss, caster with { Cast = observation }, Player(), Player(101, 1)];
            encounter.Synchronize(Frame(cast), Center, Start, playerId: 100);
            Check(encounter.PredictedDamage.Single().TargetId == 100, "SingleTargetCast missing initial target.");
            var clockOnly = cast with { FinishAt = Start.AddSeconds(5.8) };
            encounter.Synchronize(Frame(clockOnly), Center, Start.AddSeconds(.05), playerId: 100);
            Check(encounter.Casts[caster.Id].FinishAt == cast.FinishAt
                && encounter.PredictedDamage.Single().Activation == clockOnly.FinishAt,
                "Clock-only change must read current CastInfo, not cached Start clock.");
            var correction = cast with { TargetId = 101, FinishAt = Start.AddSeconds(5.8) };
            encounter.Synchronize(Frame(correction), Center, Start.AddSeconds(.1), playerId: 100);
            Check(encounter.PredictedDamage.Single() is { TargetId: 101, DamageType: "Tankbuster" } damage
                && damage.Activation == correction.FinishAt, "Current target handoff retained old delayed damage.");
            encounter.Finish(caster.Id, action, Start.AddSeconds(.2), completed: false);
            Check(!encounter.PredictedDamage.Any(), "Ordinary cast became persistent Delayable.");
        }
    }

    private static void LampasLights()
    {
        var encounter = Gymnasion(0x3D40, [.. Enumerable.Range(10, 6).Select(i => Enemy((ulong)i, 0x233C, (ushort)(i * 2)))]);
        for (ulong i = 10; i < 15; ++i)
            encounter.StartCast(i, new(32294, Center + Vector3.UnitX * (i - 10), (float)i,
                Start, Start.AddSeconds(30 - i)), Start);
        DungeonAmbientAoe[] Lights() => encounter.AmbientAoes().Where(a => a.Id.StartsWith("treasure/light/")).ToArray();
        var lights = Lights();
        Check(lights.Select(a => a.Id).SequenceEqual(new[] { 10, 11, 12, 13 }.Select(i => $"treasure/light/{i}"))
            && lights.All(a => a.Navigate && a.Display)
            && lights.Take(2).All(a => a.Color == new Vector4(1, 1, 0, 1))
            && lights.Skip(2).All(a => a.Color == null), "Lampas max casts, insertion order or danger colors differ.");
        Check(!encounter.OutputCasts.Any(), "Lampas duplicate unfiltered output leaked.");
        encounter.Finish(10, 32294, Start.AddSeconds(1), completed: false);
        lights = Lights();
        Check(lights.Select(a => a.Id).SequenceEqual(new[] { 11, 12, 13, 14 }.Select(i => $"treasure/light/{i}"))
            && lights.Last().Activation == Start.AddSeconds(16) && lights.Last().Rotation == 14,
            "Hidden fifth light did not promote with its original snapshot.");
        encounter.StartCast(14, new(32294, Center + Vector3.UnitZ, .7f, Start, Start.AddSeconds(16)),
            Start.AddSeconds(1.1));
        Check(Lights().Last().Id.EndsWith("/14") && Lights().Last().Rotation == .7f,
            "Same generation packet correction moved light or lost authoritative direction.");
        encounter.StartCast(15, new(32294, Center, 0, Start.AddSeconds(2), Start.AddSeconds(30)), Start.AddSeconds(2));
        Check(Lights().Last().Id.EndsWith("/14"), "Reused dictionary hole moved newest caster ahead of hidden tail.");
        encounter.RemoveActor(11);
        Check(Lights().Last().Id.EndsWith("/15"), "Destroyed caster did not promote next light.");
        encounter.Finish(12, 32294, Start.AddSeconds(3));
        encounter.Finish(13, 32294, Start.AddSeconds(3));
        Check(Lights().Length == 2 && Lights().All(a => a.Color == null),
            "Danger colors must return to default when total live count is two.");
        encounter.ResetFight();
        Check(!Lights().Any(), "Reset leaked light queue.");
        encounter = Gymnasion(0x3D40, [.. Enumerable.Range(10, 5).Select(i => Enemy((ulong)i, 0x233C, (ushort)(i * 2)))]);
        for (ulong i = 10; i < 15; ++i)
            encounter.StartCast(i, new(32294, Center + Vector3.UnitX * ((long)i - 14) * 100, 0,
                Start, Start.AddSeconds(5)), Start);
        var point = Center + Vector3.UnitZ * 10;
        Check(!encounter.Hints(point).Contains("离开范围！"), "Hidden fifth light emitted danger hint.");
        encounter.RemoveActor(10);
        Check(encounter.Hints(point).Contains("离开范围！"), "Promoted light lost GenericAOEs danger hint.");
        encounter.Synchronize([Enemy(1, 0x3D40), Player(), Player(101, 1),
            .. Enumerable.Range(11, 4).Select(i => Enemy((ulong)i, 0x233C, (ushort)(i * 2)) with
            {
                Cast = new(32294, Center + Vector3.UnitX * (i - 14) * 100, 0, Start, Start.AddSeconds(5)),
            }),
            Enemy(10, 0x233C, 20) with { Cast = new(32294, Center, 1, Start.AddSeconds(1), Start.AddSeconds(6)) }],
            Center, Start.AddSeconds(1), playerId: 100);
        Check(Lights().Select(a => a.Id).SequenceEqual(new[] { 11, 12, 13, 14 }.Select(i => $"treasure/light/{i}")),
            "Recreated source reused its old insertion position.");
        encounter.Finish(11, 32294, Start.AddSeconds(2));
        Check(Lights().Last().Id.EndsWith("/10") && Lights().Last().Activation == Start.AddSeconds(6),
            "Recreated source did not promote with new generation clock.");
    }

    private static void GymnasionRewards()
    {
        foreach (var (oid, bonus) in new[] { (0x3D41u, 0x3D4Fu), (0x3D27u, 0x3D4Eu), (0x3D40u, 0x3D4Du) })
        {
            var reward = Enemy(2, bonus, 4) with { InCombat = false };
            var encounter = Gymnasion(oid, reward, Enemy(3, 0x233C, 6));
            Check(encounter.PriorityTarget(1, Center) == 2
                && encounter.AmbientAoes().Single(a => a.Id.EndsWith("enemy/2")).Color == new Vector4(1, 0, 1, 1),
                "Reward rank or Vulnerable color lost.");
            encounter.Synchronize([Enemy(1, oid) with { Dead = true }, reward, Enemy(3, 0x233C, 6), Player()],
                Center, Start.AddSeconds(1), playerId: 100);
            Check(encounter.Active != null, "Reward survivor failed to preserve group.");
            encounter.Synchronize([reward with { Dead = true }, Enemy(3, 0x233C, 6), Player()],
                Center, Start.AddSeconds(2), playerId: 100);
            Check(encounter.Active == null, "Helper extended finished group.");
        }
        Check(NativeTreasureCatalog.Priority(0x3D27, 0x3D4F) == 7
            && NativeTreasureCatalog.Priority(0x3D27, 0x3D53) == 3
            && NativeTreasureCatalog.Priority(0x3D27, 0x3D4E) == 3
            && NativeTreasureCatalog.Priority(0x3D27, 0x3D4D) == 2
            && NativeTreasureCatalog.Priority(0x3D27, 0x3D28) == 1
            && NativeTreasureCatalog.Priority(0x3D41, 0x3D4F) == 6
            && NativeTreasureCatalog.Priority(0x3D41, 0x3D53) == 2
            && NativeTreasureCatalog.Priority(0x3D41, 0x3D42) == 1,
            "Leon and Mandragoras reward rank tables were conflated.");
    }

    private static DungeonEncounter Create(params DungeonActor[] actors)
    {
        var encounter = new DungeonEncounter(1279) { TreasurePartyInput = [100, 101] };
        encounter.Synchronize([Enemy(1, 0x48B0), Player(), .. actors], Center, Start, playerId: 100);
        return encounter;
    }

    private static void Geometry()
    {
        var encounter = Create();
        Check(encounter.Active?.Casts.Length == 7, "Seven components must be active.");
        Check(encounter.Arena!.Contains(Center + new Vector3(19.49f, 0, 19.49f), 0)
            && !encounter.Arena.Contains(Center + Vector3.UnitX * 19.51f, 0), "Square is not 19.5 half width.");
        var rules = encounter.Active!.Casts.ToDictionary(r => r.Action);
        Check(rules[43721].Shape is CircleTemplate { Radius: 6 }
            && rules[43719].Shape is DungeonCrossTemplate { Length: 50, HalfWidth: 5 }
            && rules[43728].Shape is RayRectangleTemplate { MinLocalX: -2, MaxLocalX: 2, Length: 75 }
            && rules[43727].Shape is RayRectangleTemplate { MinLocalX: -1.5f, MaxLocalX: 1.5f, Length: 20 }
            && rules[43726].Shape is CircleTemplate { Radius: 8 }, "Source geometry differs.");
        Check(rules[43725].Shape == null && rules[43723].Shape == null, "Damage hint invented geometry.");
        Check(encounter.Active.VisualActions.Length == 0, "Inactive visual actions were claimed.");
    }

    private static void ObservedEnd()
    {
        var helper = Enemy(2, 0x233C, 4);
        var encounter = Create(helper);
        var cast = new DungeonCastObservation(43719, Center, MathF.PI / 4, Start, Start.AddSeconds(5.3));
        encounter.StartCast(2, cast, Start);
        encounter.Synchronize([Enemy(1, 0x48B0) with { InCombat = false }, helper with
        {
            Position = Center + Vector3.UnitX * 10, Cast = cast, Rotation = 0,
        }, Player()], Center, Start.AddSeconds(12), playerId: 100);
        var locked = encounter.OutputCasts.Single().Value;
        Check(locked.Location == Center && locked.Rotation == MathF.PI / 4 && locked.FinishAt == cast.FinishAt
            && locked.UntilObservedEnd, "Moving helper or expected time changed the locked AOE.");
        encounter.Synchronize([Enemy(1, 0x48B0), helper, Player()], Center, Start.AddSeconds(12.1), playerId: 100);
        Check(!encounter.OutputCasts.Any(), "Observed cancellation did not remove helper AOE.");
        var next = new DungeonCastObservation(43721, Center + Vector3.UnitZ * 4, 0,
            Start.AddSeconds(13), Start.AddSeconds(16.3));
        encounter.StartCast(2, next, Start.AddSeconds(13));
        encounter.StartCast(2, next with { Location = Center - Vector3.UnitZ * 3, FinishAt = Start.AddSeconds(17.3) },
            Start.AddSeconds(13.1));
        Check(encounter.Casts[2].Location == Center - Vector3.UnitZ * 3
            && encounter.Casts[2].FinishAt == Start.AddSeconds(17.3), "Authoritative packet correction was ignored.");
        encounter.Finish(2, 43719, Start.AddSeconds(14));
        Check(encounter.Casts[2].Rule.Action == 43721, "Old action finish removed new generation.");
        encounter.RemoveActor(2);
        Check(!encounter.OutputCasts.Any(), "Destroyed helper output leaked.");
    }

    private static void Predictions()
    {
        var helper = Enemy(2, 0x233C, 4);
        var encounter = Create(helper, Player(101, 1));
        encounter.StartCast(2, new(43723, Center, 0, Start, Start.AddSeconds(5.3)), Start);
        var raidwide = encounter.PredictedDamage.ToArray();
        Check(raidwide.Length == 2 && raidwide.All(p => p.DamageType == "Raidwide"
            && p.Activation == Start.AddSeconds(5.3)), "Raidwide targets or activation differ.");
        Check(encounter.OutputCasts.All(c => c.Value.Rule.Shape == null), "Raidwide became a dangerous R35 circle.");
        encounter.Finish(2, 43723, Start.AddSeconds(6));
        encounter.StartCast(1, new(43725, Center, 0, Start.AddSeconds(7), Start.AddSeconds(12.3), 101),
            Start.AddSeconds(7));
        Check(encounter.PredictedDamage.Single() is { TargetId: 101, DamageType: "Tankbuster" } tank
            && tank.Activation == Start.AddSeconds(12.3), "SingleTargetCast target differs.");
        encounter.Finish(1, 43725, Start.AddSeconds(8));
        Check(!encounter.PredictedDamage.Any(), "Damage predictions survived finish.");
    }

    private static void Priorities()
    {
        var cat = Enemy(2, 0x48B7, 4);
        var keeper = Enemy(3, 0x48B8, 6);
        var unrelated = Enemy(4, 0x9999, 8) with { Hostile = false };
        var encounter = Create(cat, keeper, unrelated);
        Check(encounter.PriorityTarget(1, Center) == 2, "Goldy Cat must precede keeper and boss.");
        encounter.Synchronize([Enemy(1, 0x48B0), cat with { Hp = 1 }, keeper, unrelated, Player()],
            Center, Start.AddSeconds(1), playerId: 100);
        Check(encounter.PriorityTarget(1, Center) == 3, "Pointless cat overrode keeper.");
        encounter.Synchronize([Enemy(1, 0x48B0), cat with { Hp = 1, Position = Center + Vector3.UnitY * 13 },
            keeper, unrelated, Player()], Center, Start.AddSeconds(2), playerId: 100);
        Check(encounter.PriorityTarget(1, Center) == 2, "Height must precede PendingDead before explicit priority.");
        encounter.Synchronize([Enemy(1, 0x48B0), cat with { ObjectIndex = 3 }, keeper with { ObjectIndex = 200 },
            unrelated, Player()], Center, Start.AddSeconds(3), playerId: 100);
        Check(encounter.PriorityTarget(4, Center) == 4, "Zero-priority enemy or legal slot classification lost.");
        encounter.Synchronize([Enemy(1, 0x48B0) with { Hp = 1 }, unrelated with { Hp = 1 }, Player()],
            Center, Start.AddSeconds(4), playerId: 100);
        Check(encounter.PriorityTarget(1, Center) == 0 && encounter.TargetToClear(1) == 1,
            "Pointless candidates must clear own selected target.");
    }

    private static void Lifecycle()
    {
        var boss = Enemy(1, 0x48B0);
        var keeper = Enemy(2, 0x48B8, 4) with { InCombat = false };
        var encounter = Create(keeper);
        encounter.Synchronize([boss with { Hp = 0 }, keeper, Player()], Center, Start.AddSeconds(1), playerId: 100);
        Check(encounter.Active != null, "HP zero is not AllDeadOrDestroyed.");
        encounter.Synchronize([boss with { Dead = true }, keeper, Player()], Center, Start.AddSeconds(2), playerId: 100);
        Check(encounter.Active != null && encounter.PreserveStageAcrossBattleEnd,
            "Primary death ended surviving bonus phase.");
        encounter.RemoveActor(1);
        encounter.Synchronize([keeper with { Cast = new(43726, Center, 0, Start.AddSeconds(3), Start.AddSeconds(7.3)) },
            Player()], Center, Start.AddSeconds(3), playerId: 100);
        Check(encounter.Active != null && encounter.Casts[2].Rule.Action == 43726,
            "Primary destruction stopped bonus mechanics.");
        encounter.Synchronize([keeper with { Dead = true }, Player()], Center, Start.AddSeconds(4), playerId: 100);
        Check(encounter.Active == null && !encounter.OutputCasts.Any() && !encounter.AmbientAoes().Any(),
            "Last bonus death did not clear phase.");
    }

    private static void Markers()
    {
        var encounter = Create(Enemy(2, 0x48B7, 4) with { Position = new(150, 0, 130) },
            Enemy(3, 0x48B1, 6), Enemy(4, 0x48B0, 8));
        var markers = encounter.AmbientAoes().Where(a => a.Id.StartsWith("treasure/enemy/")).ToArray();
        Check(markers.Length == 2 && markers.All(a => !a.Navigate), "Turret was drawn or markers became navigation.");
        var bonus = markers.Single(a => a.Id.EndsWith("/2"));
        Check(bonus.Position == new Vector3(119.5f, 0, 119.5f) && bonus.OutlineOnly && bonus.Unclipped
            && bonus.Color == new Vector4(1, 0, 1, 1), "Outside bonus marker differs.");
        Check(markers.Single(a => a.Id.EndsWith("/1")).Color == new Vector4(1, 0, 0, 1), "Primary color differs.");
    }

    private static void Ownership()
    {
        Check(NativeTreasureCatalog.Owns(0x48B0, 1279) && !NativeTreasureCatalog.Owns(0x48B8, 1279)
            && !NativeTreasureCatalog.Owns(0x48B0, 1278), "Ownership expanded outside migrated primary.");
        Check(ArrDungeonCatalog.IsScript("masterplayer.treasure.vault-oneiron"), "Entry discovery contract missing.");
    }

    private static void Altars()
    {
        foreach (var oid in new[] { 0x2541u, 0x2542u, 0x2538u })
        {
            var encounter = new DungeonEncounter(794) { TreasurePartyInput = [100, 101] };
            encounter.Synchronize([Enemy(1, oid), Player()], Center, Start, playerId: 100);
            Check(encounter.Active != null && encounter.Arena!.GetContours().Single().Count == 48,
                "Altar polygon was replaced by a circle.");
            var angle = MathF.PI / 48;
            var direction = new Vector3(MathF.Sin(angle), 0, MathF.Cos(angle));
            Check(!encounter.Arena!.Contains(Center + direction * 18.99f, 0)
                && encounter.Arena.Contains(Center + direction * 18.95f, 0), "Altar polygon apothem differs.");
            var rules = encounter.Active!.Casts.ToDictionary(c => c.Action);
            if (oid == 0x2541)
                Check(rules.Count == 3 && rules[13365].Shape is CircleTemplate { Radius: 12.4f }
                    && rules[13366].Hint == "Spawns adds", "Gold Whisker components differ.");
            else
            {
                Check(Enumerable.Range(6448, 5).All(a => rules[(uint)a].Shape is CircleTemplate { Radius: 6.84f }),
                    "Shared mandragora geometry differs.");
                if (oid == 0x2542)
                    Check(rules.Count == 9 && rules[13368].Shape is FanTemplate { Radius: 22.85f } cone
                        && cone.AngleRadians == MathF.PI / 2 && rules[13369].Shape is CircleTemplate { Radius: 3 }
                        && rules[13370].Hint == "Calls adds", "Mandragora components differ.");
                else
                    Check(rules.Count == 10 && rules[13360].Shape is DonutTemplate { InnerRadius: 9, OuterRadius: 40 }
                        && rules[13363].Shape is CircleTemplate { Radius: 10 }
                        && rules[13361].Shape is CircleTemplate { Radius: 15.4f }
                        && rules[13741].Shape is RayRectangleTemplate { Length: 18, MinLocalX: -2, MaxLocalX: 2 }
                        && rules[13362].Shape is RayRectangleTemplate { Length: 45.4f, MinLocalX: -4, MaxLocalX: 4 }
                        && !rules.ContainsKey(13359), "Hati components differ.");
            }
            Check(!encounter.Gazes.Any() && encounter.GazeFacing(Center, 0) == null,
                "Comments created a gaze component.");
        }
    }

    private static void Delayable()
    {
        foreach (var (oid, action) in new[] { (0x2541u, 13364u), (0x2542u, 13367u),
            (0x2535u, 13311u), (0x253Bu, 13342u), (0x253Fu, 13385u), (0x3022u, 21673u) })
        {
            var encounter = new DungeonEncounter(oid == 0x3022 ? 924u : 794u) { TreasurePartyInput = [100, 101] };
            var bonusOid = oid == 0x2541 ? 0x2544u : 0x2546u;
            if (oid == 0x3022) bonusOid = 0x301D;
            var bonus = Enemy(3, bonusOid, 6);
            var boss = Enemy(1, oid);
            var helper = Enemy(2, 0x233C, 4);
            encounter.Synchronize([boss, helper, bonus, Player(), Player(101, 1)], Center, Start, playerId: 100);
            encounter.StartCast(1, new(action, Center, 0, Start, Start.AddSeconds(3.3), 100), Start);
            encounter.StartCast(2, new(action, Center, 0, Start, Start.AddSeconds(4.3), 100), Start);
            Check(encounter.PredictedDamage.Count() == 2, "Delayable cast duplicated generic tankbuster prediction.");
            encounter.Finish(1, action, Start.AddSeconds(1), completed: false);
            encounter.RemoveActor(2);
            encounter.Synchronize([boss, bonus, Player(), Player(101, 1)], Center, Start.AddSeconds(30), playerId: 100);
            Check(encounter.PredictedDamage.Count() == 2 && encounter.Hints(Center).Contains("Tankbuster"),
                "Finish, cancellation, destruction or predicted time cleared delayable queue.");
            encounter.TreasureEffect(3, action, 1, 101);
            Check(encounter.PredictedDamage.Count() == 2, "Wrong main target consumed a prediction.");
            encounter.TreasureEffect(3, action, 2, 100);
            encounter.TreasureEffect(3, action, 2, 100);
            Check(encounter.PredictedDamage.Single().Activation == Start.AddSeconds(4.3),
                "Effect must remove only first target record, once per packet.");
            encounter.Synchronize([boss, bonus, Player() with { PartySlot = 1 }, Player(101, 0)],
                Center, Start.AddSeconds(31), playerId: 100);
            Check(encounter.PredictedDamage.Single().TargetId == 100,
                "PR role reordering changed the source party's captured slot.");
            encounter.TreasureEffect(99, action, 3, 100);
            Check(!encounter.PredictedDamage.Any() && !encounter.Hints(Center).Contains("Tankbuster"),
                "Different-source matching effect did not clear last record.");
            encounter.StartCast(1, new(action, Center, 0, Start.AddSeconds(32), Start.AddSeconds(35.3), 100), Start.AddSeconds(32));
            Check(encounter.PredictedDamage.Count() == 1, "Live delayable cast duplicated damage.");
            encounter.TreasureEffect(99, action, 4, 100);
            Check(encounter.Casts.ContainsKey(1) && !encounter.PredictedDamage.Any(),
                "Resolved delayable effect retained generic live-cast prediction.");
        }
    }

    private static void CurrentTarget()
    {
        var boss = Enemy(1, 0x48B0);
        var encounter = Create(Player(101, 1));
        var cast = new DungeonCastObservation(43725, Center, 0, Start, Start.AddSeconds(5.3), 100);
        encounter.Synchronize([boss with { Cast = cast }, Player(), Player(101, 1)], Center, Start, playerId: 100);
        Check(encounter.PredictedDamage.Single().TargetId == 100, "Initial cast target missing.");
        encounter.Synchronize([boss with { Cast = cast with { TargetId = 101 } }, Player(), Player(101, 1)],
            Center, Start.AddSeconds(1), playerId: 100);
        Check(encounter.PredictedDamage.Single().TargetId == 101, "Current CastInfo target change ignored.");
        encounter.Synchronize([boss with { TargetId = 100, Cast = cast with { TargetId = 0 } }, Player(), Player(101, 1)],
            Center, Start.AddSeconds(2), playerId: 100);
        Check(!encounter.PredictedDamage.Any(), "Zero target fell back to aggro target.");
        encounter.Synchronize([boss with { TargetId = 101, Cast = cast with { TargetId = 1 } }, Player(), Player(101, 1)],
            Center, Start.AddSeconds(3), playerId: 100);
        Check(encounter.PredictedDamage.Single().TargetId == 101, "Self target did not use current aggro target.");
    }

    private static void AltarLifecycle()
    {
        foreach (var oid in new[] { 0x2542u, 0x2538u })
        {
            var encounter = new DungeonEncounter(794);
            var enemies = Enumerable.Range(0, 5).Select(i => Enemy((ulong)(10 + i), (uint)(0x2546 + i), (ushort)(4 + 2 * i))).ToArray();
            encounter.Synchronize([Enemy(1, oid), .. enemies, Player()], Center, Start, playerId: 100);
            Check(encounter.PriorityTarget(1, Center) == 10, "Reward order must start with onion.");
            for (var i = 0; i < 5; ++i)
            {
                enemies[i] = enemies[i] with { Dead = true };
                encounter.Synchronize([Enemy(1, oid), .. enemies, Player()], Center, Start.AddSeconds(i + 1), playerId: 100);
                Check(encounter.PriorityTarget(1, Center) == (i < 4 ? (ulong)(11 + i) : 1ul),
                    "Reward ordering or fallback to priority zero differs.");
            }
            var last = Enemy(20, oid == 0x2542 ? 0x255Cu : 0x2569u, 16) with { Targetable = false };
            encounter.Synchronize([Enemy(1, oid) with { Dead = true }, last, Player()],
                Center, Start.AddSeconds(6), playerId: 100);
            Check(encounter.Active != null, "Untargetable nonreward add must block phase completion.");
            encounter.Synchronize([Enemy(1, oid) with { Dead = true }, Enemy(30, 0x233C), Player()],
                Center, Start.AddSeconds(7), playerId: 100);
            Check(encounter.Active == null && !encounter.AmbientAoes().Any(), "Unlisted helper kept phase alive.");
        }
    }

    private static DungeonEncounter DelayEncounter()
    {
        var encounter = new DungeonEncounter(794) { TreasurePartyInput = [100, 101] };
        encounter.Synchronize([Enemy(1, 0x2541), Enemy(2, 0x2544, 4), Player(), Player(101, 1)],
            Center, Start, playerId: 100);
        return encounter;
    }

    private static void DelayableGenerations()
    {
        var encounter = DelayEncounter();
        var first = new DungeonCastObservation(13364, Center, 0, Start, Start.AddSeconds(3.3), 100);
        encounter.StartCast(1, first, Start);
        encounter.Finish(1, 13364, Start.AddSeconds(.05));
        encounter.StartCast(1, first with { TargetId = 101, StartedAt = Start.AddSeconds(.1), FinishAt = Start.AddSeconds(3.4) },
            Start.AddSeconds(.1));
        Check(encounter.PredictedDamage.Select(p => p.TargetId).SequenceEqual([100ul, 101ul]),
            "Quick recast replaced an unfinished old target.");
        encounter.TreasureEffect(99, 13364, 1, 100);
        encounter.TreasureEffect(99, 13364, 2, 101);
        Check(!encounter.PredictedDamage.Any(), "Both effects did not consume both generations.");
        encounter.StartCast(1, first with { TargetId = 101, Location = Center + Vector3.UnitZ,
            StartedAt = Start.AddSeconds(.1), FinishAt = Start.AddSeconds(3.4) }, Start.AddSeconds(.2));
        Check(!encounter.PredictedDamage.Any() && !encounter.Hints(Center).Contains("Tankbuster"),
            "Packet correction resurrected an already resolved live generation.");
    }

    private static void RawHandoff()
    {
        var encounter = DelayEncounter();
        var cast = new DungeonCastObservation(13364, Center, 0, Start, Start.AddSeconds(3.3), 100, TotalTime: 3);
        var boss = Enemy(1, 0x2541);
        void Sync(DungeonCastObservation raw, double time) => encounter.Synchronize([
            boss with { Cast = raw }, Enemy(2, 0x2544, 4), Player(), Player(101, 1)],
            Center, Start.AddSeconds(time), playerId: 100);
        encounter.StartCast(1, cast, Start);
        Sync(cast, .01);
        Sync(cast with { TargetId = 101 }, .1);
        Check(encounter.PredictedDamage.Select(p => p.TargetId).SequenceEqual([100ul, 101ul]),
            "Confirmed raw target handoff did not append a fresh source Start.");
        encounter.TreasureEffect(99, 13364, 1, 100);
        Check(encounter.PredictedDamage.Single().TargetId == 101, "Handoff old target effect removed new target.");
        Sync(cast with { TargetId = 101, TotalTime = MathF.BitIncrement(3f) }, .2);
        Check(encounter.PredictedDamage.Count() == 2, "Real raw TotalTime change did not begin a new source cast.");
        encounter = DelayEncounter();
        Sync(cast, 0);
        Sync(cast with { FinishAt = Start.AddSeconds(4.3), TotalTime = 4 }, .1);
        Sync(cast with { TargetId = 101, FinishAt = Start.AddSeconds(4.3), TotalTime = 4 }, .2);
        Check(encounter.PredictedDamage.Select(p => p.TargetId).SequenceEqual([100ul, 100ul, 101ul])
            && encounter.PredictedDamage.Select(p => p.Activation).SequenceEqual(
                [Start.AddSeconds(3.3), Start.AddSeconds(4.3), Start.AddSeconds(4.3)]),
            "Large raw duration replacement left a tombstone blocking the next real raw cast.");
    }

    private static void CurrentClocks()
    {
        foreach (var action in new[] { 43725u, 43723u })
        {
            var encounter = Create(Player(101, 1));
            var boss = Enemy(1, 0x48B0);
            var cast = new DungeonCastObservation(action, Center, 0, Start, Start.AddSeconds(5.3), 100);
            encounter.Synchronize([boss with { Cast = cast }, Player(), Player(101, 1)], Center, Start, playerId: 100);
            encounter.Synchronize([boss with { Cast = cast with { TargetId = 101, FinishAt = Start.AddSeconds(5.8) } },
                Player(), Player(101, 1)], Center, Start.AddSeconds(1), playerId: 100);
            Check(encounter.PredictedDamage.All(p => p.Activation == Start.AddSeconds(5.8))
                && encounter.PredictedDamage.Count() == (action == 43723 ? 2 : 1),
                "Unavoidable damage used locked AOE activation instead of current CastInfo.");
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
