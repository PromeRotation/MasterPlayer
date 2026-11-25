using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

internal static partial class JobQuestRegression
{
    private static readonly DateTime Start = new(2026, 11, 23, 0, 0, 0, DateTimeKind.Utc);

    internal static void Run(Action<string, Action> run)
    {
        run("Job quests exact arenas and geometry", Geometry);
        run("Job quests independent observed cast endings and raidwide prediction", CastLifetimes);
        run("Job quests grouped enemies survive primary death destruction and battle end", Groups);
        run("Job quests Griffin long range locked gaze and facing deadlines", Gaze);
        run("Job quests enemy markers clamp to exact boundary without affecting navigation", Markers);
        run("Job quests Portrait flowers lock creation pose and replace singleton snapshot", PortraitFlowers);
        run("Job quests Portrait earthquake advances every actual finish with per-axis matching", PortraitRings);
        run("Job quests Portrait flood corrects independent lines from effect caster position", PortraitFlood);
        run("Job quests Portrait Nine Ivies preserves first nine insertion order", PortraitIvies);
        run("Job quests Portrait gaze raidwide and primary-only lifecycle", PortraitControls);
        run("Job quests Portrait authoritative packet corrections preserve derived state", PortraitCorrections);
        run("Job quests Viper player pull and cached Gowrow lifecycle", Viper);
        run("Job quests Musosai shared Arashi clock follows every live object until effect", Musosai);
        run("Job quests Sage NPC spread radius and damage preserve source party semantics", Sage);
        run("Job quests Ompagne persistent NPC stacks and chain priority", Ompagne);
        run("Job quests Sage target corrections finish old records and reject passive targets", SageCorrections);
        run("Job quests Bekko rings follow actual endings and exact matching", BekkoRings);
        run("Job quests Bekko first eight insertion order and persistent floor", BekkoLines);
        run("Job quests Orcus FIFO rings preserve source order and corrections", OrcusRings);
        run("Job quests Orcus dynamic poison predictions hand over one at a time", OrcusPoison);
        run("Job quests Bekko Orcus exact shapes arenas and adds", BekkoOrcusGeometry);
        run("Job quests Fallen icons bind event actors and retain independent clocks", FallenIcons);
        run("Job quests Fallen spread death is permanent while stack death is temporary", FallenLifetimes);
        run("Job quests Fallen invincible targets retain incidental AOE permission", FallenTargets);
        run("Job quests Sage Life rockbreaker follows helper endings without timeout", SageLifeRocks);
        run("Job quests Sage Life towers count NPCs and preserve hard entry branches", SageLifeTowers);
        run("Job quests Sage Life stack spread radii targets and component endings", SageLifeParty);
        run("Job quests Sage Life Guildivain tethers and arena state transitions", SageLifeGuildivain);
        run("Job quests Io caution uses final second and releases all control on lose death and perspective", IoCaution);
        run("Job quests Io fire predictions transfer individually to persistent objects", IoFire);
        run("Job quests Node explosive bootstrap order matching frozen references death and destruction", NodeExplosives);
        run("Job quests Node invincible priority permits incidental AOE and preserves pending dead", NodeTargets);
        run("Job quests Steel swords retain destroyed generations until matching effects", SteelSwords);
        run("Job quests Steel four bolts promote and independent half rings preserve facing", SteelBolts);
        run("Job quests Wind exact arenas and long range gaze facing and drawing", QuestWindGeometry);
        run("Job quests Wind distinct source sets exact wall landing immunity and cancellation", QuestWindKnockbacks);
        run("Job quests Foxshot dynamic bait NPC navigation damage and dead target", FoxshotBait);
        run("Job quests MSQ exact geometry clone casts party masks and primary lifecycle", MainScenarioBasics);
        run("Job quests MSQ Lunar Ifrit independent FIFO promotion and raidwide predictions", LunarIfritFifo);
        run("Job quests MSQ Lunar Ifrit nail priorities pending dead and selector hold", LunarIfritPriorities);
        run("Job quests MSQ Oracle FIFO real observations directional wall and chained knockbacks", OracleFifoAndKnockback);
        run("Job quests MSQ Feast twelve electrocutions preserve display and replace navigation by targets", FeastElectrocutionBoundaries);
        run("Job quests MSQ Feast five Burn FIFO and radial wall knockback", FeastBurnAndKnockback);
        run("Job quests MSQ Rhalgr dynamic Termini shared clock frozen generations and effects", RhalgrTermini);
        run("Job quests MSQ Rhalgr poison handoff knockback navigation and landing source sets", RhalgrVoidzones);
        run("Job quests Endwalker exact polygon unions and trash-only pull", EndwalkerQuestArenasAndPull);
        run("Job quests Endwalker Nox and rock FIFO promotion with observed lifetimes", EndwalkerQuestFifo);
        run("Job quests Endwalker independent party components and frozen actor references", EndwalkerQuestParty);
        run("Job quests Endwalker Lock On creation handoff global FIFO and generations", FrostyLockOn);
        run("Job quests Endwalker Colibri locked charges and party count navigation", ColibriCharges);
        run("Job quests Faded Thordan locked gaze full drawings invincible priority and selector hold", FadedThordanControls);
        run("Job quests Faded Zenos sword-only pull exact markers and primary lifetime", FadedZenosLifecycle);
        run("Job quests Faded exact cast geometry player raidwide source and observed endings", FadedCastRules);
    }

    private static void Geometry()
    {
        var blood = JobQuestCatalog.Create(708).Single();
        var rectangle = (RectangleGeometry)blood.Arena(Vector3.Zero);
        Check(rectangle.Rotation == 0 && rectangle.Width == 16 && rectangle.Length == 15
            && rectangle.Position == new Vector3(0, 0, 15.8f), "Blood arena rectangle parameters.");
        Check(rectangle.Contains(new(8, 0, 23.3f)) && !rectangle.Contains(new(8.1f, 0, 23.3f)), "Blood arena edge.");
        foreach (var territory in new uint[] { 708, 709, 715, 1022, 1233, 1234, 1235 })
        foreach (var definition in JobQuestCatalog.Create(territory))
        {
            var arena = definition.Arena(Vector3.Zero);
            Check(arena.Contains(arena.Position), "Arena center outside boundary.");
            if (territory is 1233 or 1234 && definition.BossOid != 0x4298)
                Check(arena is WorldPolygonGeometry p && p.WorldPoints.Count > 100, "Detailed source arena missing.");
            foreach (var rule in definition.Casts.Where(r => r.Shape != null))
            {
                var shape = rule.Shape!.Resolve(Vector3.Zero, 0, null);
                var inside = rule.Action is 37520 or 37538 or 26546 ? new Vector3(20, 0, 0) : new Vector3(0, 0, 1);
                Check(shape.Contains(inside) && !shape.Contains(new(200, 0, 200)), $"Shape {rule.Action} extent.");
            }
        }
        Check(((CircleTemplate)blood.Casts.Single(r => r.Action == 8407).Shape!).Radius == 7
            && ((FanTemplate)blood.Casts.Single(r => r.Action == 8408).Shape!).AngleRadians == MathF.PI * 2 / 3,
            "Blood circle/cone dimensions.");
        Check(JobQuestCatalog.Owns(1234, 0x4298), "Migrated Portrait ownership missing.");
    }

    private static void CastLifetimes()
    {
        var e = new DungeonEncounter(1233);
        DungeonActor boss = new(1, 0x4292, Vector3.Zero, InCombat: true);
        DungeonActor player = new(100, 0, Vector3.Zero, PartySlot: 0);
        DungeonActor Helper(ulong id, uint action) => new(id, 0x233C, Vector3.Zero,
            Cast: new(action, new((float)id, 0, 10), 0, Start, Start.AddSeconds(3)));
        e.Synchronize([boss, player, Helper(2, 37512), Helper(3, 37513), Helper(4, 37515)], Vector3.Zero, Start, playerId: 100);
        Check(e.OutputCasts.Count() == 3, "Three rubble actions did not coexist.");
        e.Effect(2, 37512, 1, Start.AddSeconds(2));
        e.Tick(Start.AddSeconds(30));
        Check(e.OutputCasts.Count() == 3, "Effect or predicted timeout removed an observed SimpleAOE.");
        e.Synchronize([boss, player, new(2, 0x233C, Vector3.Zero), Helper(3, 37513), Helper(4, 37515)],
            Vector3.Zero, Start.AddSeconds(31), playerId: 100);
        Check(e.OutputCasts.Count() == 2, "Independent first observed ending.");
        e.RemoveActor(3);
        Check(e.OutputCasts.Count() == 1, "Destroy did not remove exactly its own AOE.");
        e.Synchronize([boss, player], Vector3.Zero, Start.AddSeconds(32), playerId: 100);
        Check(!e.OutputCasts.Any(), "Remaining helper ending retained output.");
        var raidwide = new DungeonCastObservation(37511, Vector3.Zero, 0, Start.AddSeconds(33), Start.AddSeconds(36.3));
        e.StartCast(1, raidwide, Start.AddSeconds(33));
        Check(e.OutputCasts.All(c => c.Value.Rule.Shape == null) && e.PredictedDamage.Single() is { TargetId: 100, DamageType: "Raidwide" } p
            && p.Activation == raidwide.FinishAt, "Raidwide invented geometry or lost prediction.");
        e.Finish(1, 37511, Start.AddSeconds(34));
        e.StartCast(1, raidwide with { Action = 37514, StartedAt = Start.AddSeconds(35) }, Start.AddSeconds(35));
        Check(!e.OutputCasts.Any() && !e.PredictedDamage.Any(), "Inactive source enum 37514 invented output.");
    }

    private static void Groups()
    {
        foreach (var (oid, addOid, action) in new (uint, uint, uint)[] { (0x4293, 0x4294, 37517), (0x4296, 0x4297, 37522) })
        {
            var e = new DungeonEncounter(1234);
            var center = JobQuestCatalog.Create(1234).Single(d => d.BossOid == oid).Arena(Vector3.Zero).Position;
            DungeonActor boss = new(1, oid, center, InCombat: true);
            DungeonActor add = new(2, addOid, center);
            DungeonActor player = new(100, 0, center, PartySlot: 0);
            void Sync(double t) => e.Synchronize([boss, add, player], center, Start.AddSeconds(t), playerId: 100);
            Sync(0);
            add = add with { Targetable = false, Hp = 0 };
            boss = boss with { Dead = true, Hp = 0 };
            Sync(1);
            Check(e.Active != null && e.PreserveStageAcrossBattleEnd, "Group ended on primary death or untargetable HP-zero add.");
            e.RemoveActor(1);
            Check(e.Active != null, "Group ended on primary destruction.");
            add = add with { Targetable = true, Cast = new(action, center, 0, Start.AddSeconds(2), Start.AddSeconds(5)) };
            e.Synchronize([add, player], center, Start.AddSeconds(2), playerId: 100);
            Check(e.OutputCasts.Single().Value.Rule.Action == action, "Surviving add failed to start its AOE.");
            add = add with { Dead = true };
            e.Synchronize([add, player], center, Start.AddSeconds(3), playerId: 100);
            Check(e.Active == null && !e.OutputCasts.Any(), "Last group member death did not clear encounter.");
            boss = boss with { Dead = false, Hp = 100 };
            add = add with { Dead = false, Hp = 100, Cast = null };
            Sync(4);
            e.RemoveActor(1);
            e.RemoveActor(2);
            Check(e.Active == null, "Last group member destruction did not clear encounter.");
        }
    }

    private static void Gaze()
    {
        var e = new DungeonEncounter(1234);
        var center = JobQuestArenas.FlightOfTheGriffin(0).Position;
        var eye = center + Vector3.UnitZ * 120;
        DungeonActor boss = new(1, 0x4296, center, InCombat: true);
        DungeonActor player = new(100, 0, center, PartySlot: 0, ObjectIndex: 0);
        var cast = new DungeonCastObservation(37523, eye, 0, Start, Start.AddSeconds(3));
        e.Synchronize([boss with { Cast = cast }, player], center, Start, playerId: 100);
        Check(e.GazeFacing(center, 0) == null, "Gaze forced facing before its deadline window.");
        var drawing = e.AmbientAoes().Where(a => a.Id.StartsWith("griffin/gaze/")).ToArray();
        Check(drawing.Length == 3 && drawing.All(a => !a.Navigate) && drawing.Any(a => a.Id.EndsWith("/eye") && a.Position == eye),
            "Long range gaze eye/pupil/arc or locked location missing.");
        e.Tick(Start.AddSeconds(2.25));
        Check(e.GazeFacing(center, 0) is { } facing && MathF.Cos(facing) < .7f, "Griffin gaze facing missing at .75-second deadline.");
        Check(e.GazeFacing(eye - Vector3.UnitZ * 10001, 0) == null, "Gaze exceeded default 10000 range.");
        e.StartCast(2, cast with { Location = center - Vector3.UnitZ * 120 }, Start.AddSeconds(2.25));
        e.Synchronize([boss with { Cast = cast, Position = center + Vector3.UnitX * 5 }, player],
            center, Start.AddSeconds(2.3), playerId: 100);
        Check(e.Gazes.Single().Location == eye, "Moving caster changed locked cast location.");
        e.Synchronize([boss, player], center, Start.AddSeconds(2.4), playerId: 100);
        Check(e.GazeFacing(center, 0) == null && !e.AmbientAoes().Any(a => a.Id.StartsWith("griffin/gaze/")),
            "Observed gaze cancellation retained facing or drawing.");
    }

    private static void Markers()
    {
        var center = new Vector3(0, 0, 15.8f);
        var e = new DungeonEncounter(708);
        e.Synchronize([new(1, 0x1BED, center, InCombat: true, IsEnemy: true),
            new(2, 0xFFFF, new(20, 3, 40), Hostile: false, IsEnemy: true),
            new(3, 0xFFFF, center, Hostile: true, IsEnemy: false)], center, Start);
        var markers = e.AmbientAoes().Where(a => a.Id.StartsWith("job-quest/enemy/")).ToArray();
        Check(markers.Length == 2 && markers.All(a => !a.Navigate), "Source enemy classification or marker navigation.");
        Check(markers.Single(a => a.Id.EndsWith("/2")) is { OutlineOnly: true, Unclipped: true } marker
            && Vector3.Distance(marker.Position, new(8, 3, 23.3f)) < .001f, "Outside marker did not clamp to exact rectangle boundary.");
        foreach (var (territory, oid, expected) in new (uint, uint, Vector3)[]
        {
            (1233, 0x4292, new(3.82f, 0, .11f)), (1234, 0x4293, new(54.745f, 0, -219.305f)),
            (1234, 0x4296, new(.17f, 0, -250.525f)),
        })
        {
            var arena = JobQuestCatalog.Create(territory).Single(d => d.BossOid == oid).Arena(Vector3.Zero);
            Check(arena.Position == expected, "Custom arena lost source bounding-box center.");
            e = new DungeonEncounter(territory);
            var outside = expected + Vector3.UnitZ * 100;
            e.Synchronize([new(1, oid, outside, InCombat: true)], expected, Start);
            Check(e.AmbientAoes().Single(a => a.Id == "job-quest/enemy/1") is { OutlineOnly: true } axis
                && axis.Position == outside, "Custom source center-axis exception was projected to the boundary.");
        }
    }

    private static (DungeonEncounter Encounter, DungeonActor Boss, DungeonActor Player) Portrait()
    {
        var center = new Vector3(0, 0, -340);
        DungeonActor boss = new(1, 0x4298, center, InCombat: true);
        DungeonActor player = new(100, 0, center, PartySlot: 0);
        var e = new DungeonEncounter(1234);
        e.Synchronize([boss, player], center, Start, playerId: 100);
        return (e, boss, player);
    }

    private static void PortraitFlowers()
    {
        var (e, boss, player) = Portrait();
        var pose = boss.Position + Vector3.UnitX * 5;
        DungeonActor flower = new(2, 0x4299, pose, Rotation: .5f);
        e.Synchronize([boss, player, flower], player.Position, Start.AddSeconds(1), playerId: 100);
        var first = e.AmbientAoes().Single(a => a.Id == "portrait/flower");
        Check(first.Position == pose && first.Rotation == .5f && first.Activation == Start.AddSeconds(10.8), "Preexisting flower activation/pose.");
        e.Synchronize([boss, player, flower with { Position = pose + Vector3.UnitX, Rotation = 2 }],
            player.Position, Start.AddSeconds(2), playerId: 100);
        Check(e.AmbientAoes().Single(a => a.Id == "portrait/flower") == first, "Observed movement reseeded creation snapshot.");
        e.Tick(Start.AddSeconds(2.1));
        e.ObjectCreated(2, Start.AddSeconds(2.1), 0x4299, pose, .5f);
        Check(e.AmbientAoes().Single(a => a.Id == "portrait/flower") == first, "Late creation event postponed observed flower activation.");
        e.Tick(Start.AddSeconds(3));
        e.ObjectCreated(3, Start.AddSeconds(3), 0x4299, boss.Position - Vector3.UnitX * 5, 1);
        var replacement = e.AmbientAoes().Single(a => a.Id == "portrait/flower");
        Check(replacement.Position != first.Position && replacement.Activation == Start.AddSeconds(12.8), "Second flower failed to replace singleton.");
        e.RemoveActor(3);
        e.Tick(Start.AddSeconds(30));
        Check(e.AmbientAoes().Single(a => a.Id == "portrait/flower") == replacement, "Flower destruction or clock cleared source-owned snapshot.");
        e.Effect(2, 37527, 1, Start.AddSeconds(31));
        Check(!e.AmbientAoes().Any(a => a.Id == "portrait/flower"), "Old flower effect did not clear newest snapshot.");
        e.Synchronize([boss, player, flower], player.Position, Start.AddSeconds(32), playerId: 100);
        Check(!e.AmbientAoes().Any(a => a.Id == "portrait/flower"), "Repeated world observation resurrected resolved flower.");
        e.ObjectCreated(2, Start.AddSeconds(32), 0x4299, pose, .5f);
        Check(!e.AmbientAoes().Any(a => a.Id == "portrait/flower"), "Late duplicate creation revived a resolved flower.");
        e.Synchronize([boss, player, new(4, 0x4299, pose)], player.Position, Start.AddSeconds(33), playerId: 100);
        e.Effect(4, 37527, 2, Start.AddSeconds(34));
        e.ObjectCreated(4, Start.AddSeconds(34), 0x4299, pose, .5f);
        Check(!e.AmbientAoes().Any(a => a.Id == "portrait/flower"), "First late creation event revived a flower resolved after observation.");
    }

    private static void PortraitRings()
    {
        var (e, boss, _) = Portrait();
        var center = boss.Position;
        DungeonCastObservation Cast(uint action, Vector3 location) => new(action, location, 0, Start, Start.AddSeconds(5));
        // The later rings can already be casting; only the first creates a sequence.
        e.Synchronize([boss, new(2, 0x233C, center, Cast: Cast(37531, center)),
            new(3, 0x233C, center, Cast: Cast(37532, center + new Vector3(.9f, 0, .9f))),
            new(4, 0x233C, center, Cast: Cast(37533, center))], center, Start);
        DungeonAmbientAoe Ring() => e.AmbientAoes().Single(a => a.Id.StartsWith("portrait/ring/"));
        Check(Ring().Shape is CircleTemplate { Radius: 10 }, "Earthquake first ring.");
        Check(e.Hints(center).Contains("GTFO from aoe!"), "Earthquake personal range warning missing.");
        e.Tick(Start.AddSeconds(1));
        e.Finish(2, 37531, Start.AddSeconds(1), completed: false);
        Check(Ring().Shape is DonutTemplate { InnerRadius: 10, OuterRadius: 20 } && Ring().Activation == Start.AddSeconds(3),
            "Early cancellation did not advance earthquake by source finish contract.");
        e.Tick(Start.AddSeconds(2));
        e.RemoveActor(3);
        Check(Ring().Shape is DonutTemplate { InnerRadius: 20, OuterRadius: 30 } && Ring().Activation == Start.AddSeconds(4),
            "Destroy/diagonal per-axis match did not advance earthquake.");
        e.Tick(Start.AddSeconds(100));
        Check(Ring().Shape is DonutTemplate { InnerRadius: 20 }, "Earthquake expired without last actual finish.");
        e.Finish(4, 37533, Start.AddSeconds(100), completed: false);
        Check(!e.AmbientAoes().Any(a => a.Id.StartsWith("portrait/ring/")), "Third actual ending did not clear sequence.");
        var (isolated, isolatedBoss, _) = Portrait();
        isolated.Synchronize([isolatedBoss, new(2, 0x233C, center, Cast: Cast(37532, center))], center, Start);
        Check(!isolated.AmbientAoes().Any(a => a.Id.StartsWith("portrait/ring/")), "Second ring invented missing first-ring history.");
    }

    private static void PortraitFlood()
    {
        var (e, boss, _) = Portrait();
        var center = boss.Position;
        DungeonActor caster = new(2, 0x233C, center,
            Cast: new(37535, center + Vector3.UnitZ * 2, 0, Start, Start.AddSeconds(5.3)));
        e.Synchronize([boss, caster], center, Start);
        DungeonAmbientAoe[] Water() => e.AmbientAoes().Where(a => a.Id.StartsWith("portrait/water/")).ToArray();
        Check(Water().Length == 5 && Water().Count(a => a.Color == new Vector4(1, 1, 0, 1)) == 3,
            "Flood first/independent two-line preview colors.");
        Check(e.Hints(center - Vector3.UnitX * 7.5f).Contains("GTFO from exaflare!"), "Water personal range warning missing.");
        var current = Water().Single(a => a.Id == "portrait/water/0/current");
        Check(Vector3.Distance(current.Position, center - Vector3.UnitX * 7.5f) < .045f && current.Activation == Start.AddSeconds(5.3),
            "Flood origin used cast location or added an extra delay.");
        var future = Water().Single(a => a.Id == "portrait/water/0/next");
        e.Tick(Start.AddSeconds(20));
        Check(Water().Single(a => a.Id == future.Id) == future, "Clock alone rebuilt cached water prediction.");
        // EffectPosition and packet rotation are deliberately unrelated to the effect caster.
        e.Effect(2, 37536, 1, Start.AddSeconds(20), location: center + Vector3.UnitX * 100,
            sourceRotation: 2, sourcePosition: center - Vector3.UnitX * 5);
        current = Water().Single(a => a.Id == "portrait/water/0/current");
        Check(Vector3.Distance(current.Position, center - Vector3.UnitX * 12.5f) < .045f
            && current.Activation == Start.AddSeconds(22), "Effect caster correction or saved line rotation missing.");
        Check(Water().Single(a => a.Id == "portrait/water/1/current").Activation == Start.AddSeconds(5.3),
            "One effect advanced both lines.");
        e.Effect(2, 37536, 1, Start.AddSeconds(20), sourcePosition: center - Vector3.UnitX * 12.5f);
        Check(Water().Single(a => a.Id == "portrait/water/0/current") == current, "Duplicate effect advanced water again.");
        e.Finish(2, 37535, Start.AddSeconds(20), completed: false);
        Check(Water().Length == 4, "First cast finish removed persistent side lines.");
        for (uint i = 0; i < 4; ++i)
        {
            current = Water().Single(a => a.Id == "portrait/water/0/current");
            e.Effect(2, 37536, i + 2, Start.AddSeconds(21 + i), sourcePosition: current.Position);
        }
        Check(Water().Length == 2 && Water().All(a => a.Position.X > center.X), "Left line exhaustion removed opposite line.");
        e.Tick(Start.AddSeconds(100));
        Check(Water().Length == 2, "Remaining line expired by clock.");
    }

    private static void PortraitIvies()
    {
        var (e, boss, _) = Portrait();
        var center = boss.Position;
        var helpers = Enumerable.Range(2, 10).Select(i => new DungeonActor((ulong)i, 0x233C, center,
            Cast: new(37529, center, 0, Start, Start.AddSeconds(3)))).ToArray();
        e.Synchronize([boss, .. helpers], center, Start);
        Check(e.OutputCasts.Where(c => c.Value.Rule.Action == 37529).Select(c => c.Value.Source).SequenceEqual(Enumerable.Range(2, 9).Select(i => (ulong)i)),
            "Nine Ivies initial first-nine order.");
        e.Finish(2, 37529, Start.AddSeconds(1));
        e.Synchronize([boss, .. helpers.Skip(1), new(20, 0x233C, center,
            Cast: new(37529, center, 0, Start.AddSeconds(2), Start.AddSeconds(5)))], center, Start.AddSeconds(2));
        var visible = e.OutputCasts.Where(c => c.Value.Rule.Action == 37529).Select(c => c.Value.Source).ToArray();
        Check(visible.Length == 9 && visible.Contains(11ul) && !visible.Contains(20ul), "Dictionary slot reuse displaced an older visible Ivy.");
        e.Finish(3, 37529, Start.AddSeconds(2.1));
        var current = e.Casts[4];
        e.StartCast(4, new(37529, center, 0, current.StartedAt.AddSeconds(.1), current.FinishAt.AddSeconds(1)),
            Start.AddSeconds(2.2));
        Check(e.OutputCasts.Where(c => c.Value.Rule.Action == 37529).Select(c => c.Value.Source)
            .SequenceEqual(new ulong[] { 5, 6, 7, 8, 9, 10, 11, 20, 4 }),
            "Accepted short-start/different-finish generation lost its Ivy membership or insertion order.");
    }

    private static void PortraitControls()
    {
        var (e, boss, player) = Portrait();
        var center = boss.Position;
        var eye = center + Vector3.UnitZ * 120;
        e.StartCast(1, new(37537, eye, 0, Start, Start.AddSeconds(3)), Start);
        e.Tick(Start.AddSeconds(2.5));
        Check(e.GazeFacing(center, 0) != null && e.AmbientAoes().Count(a => a.Id.StartsWith("portrait/gaze/")) == 3,
            "Portrait gaze not connected to shared facing and dual drawing.");
        e.Finish(1, 37537, Start.AddSeconds(2.6));
        Check(e.GazeFacing(center, 0) == null, "Portrait gaze finish retained facing.");
        e.StartCast(1, new(37539, center, 0, Start.AddSeconds(3), Start.AddSeconds(9.3)), Start.AddSeconds(3));
        Check(e.PredictedDamage.Single().DamageType == "Raidwide" && e.OutputCasts.All(c => c.Value.Rule.Shape == null),
            "Portrait raidwide fabricated danger or omitted damage prediction.");
        e.Synchronize([boss with { Dead = true }, player, new(2, 0x4299, center)], center, Start.AddSeconds(4));
        Check(e.Active == null && !e.AmbientAoes().Any(), "Flower incorrectly extended primary-only Portrait lifecycle.");
    }

    private static void PortraitCorrections()
    {
        var (e, boss, _) = Portrait();
        var center = boss.Position;
        DungeonActor helper = new(2, 0x233C, center, Cast: new(37531, center, 0, Start, Start.AddSeconds(5.3)));
        e.Synchronize([boss, helper], center, Start);
        var corrected = center + Vector3.UnitX * 2;
        e.StartCast(2, helper.Cast! with { Location = corrected }, Start.AddSeconds(.1));
        Check(e.AmbientAoes().Single(a => a.Id.StartsWith("portrait/ring/")).Position == corrected,
            "Earthquake authoritative packet left a stale derived origin.");
        e.Tick(Start.AddSeconds(1));
        e.Finish(2, 37531, Start.AddSeconds(1));
        Check(e.AmbientAoes().Single(a => a.Id.StartsWith("portrait/ring/")).Shape is DonutTemplate { InnerRadius: 10 },
            "Corrected earthquake origin failed to match its actual finish.");
        var sourcePosition = center + Vector3.UnitX * 10;
        e.StartCast(2, new(37535, center, 0, Start.AddSeconds(2), Start.AddSeconds(7.3), SourcePosition: sourcePosition), Start.AddSeconds(2));
        var water = e.AmbientAoes().Where(a => a.Id.StartsWith("portrait/water/") && a.Id.EndsWith("/current")).ToArray();
        Check(water.Length == 2 && water.All(a => a.Position.X > 2), "Flood ignored event SourcePosition and seeded at stale object position.");
        e.StartCast(2, new(37535, center, MathF.PI / 2, Start.AddSeconds(2), Start.AddSeconds(7.3), SourcePosition: sourcePosition),
            Start.AddSeconds(2.1));
        water = e.AmbientAoes().Where(a => a.Id.StartsWith("portrait/water/") && a.Id.EndsWith("/current")).ToArray();
        Check(water.Length == 2 && water.All(a => MathF.Abs(a.Position.X - sourcePosition.X) < .045f),
            "Same-generation packet correction failed to rotate both derived water lines.");
    }

    private static void Viper()
    {
        var center = new Vector3(264, 0, 480);
        var arena = (WorldPolygonGeometry)JobQuestCatalog.Create(1235).Single().Arena(center);
        Check(arena.WorldPoints.Count == 20 && arena.Position == center
            && arena.WorldPoints[0] == center + Vector3.UnitZ * 19.5f, "Viper exact regular 20-gon.");
        var midpoint = (arena.WorldPoints[0] + arena.WorldPoints[1]) * .5f;
        var outward = Vector3.Normalize(midpoint - center);
        Check(!arena.Contains(midpoint + outward * .02f) && arena.Contains(midpoint - outward * .02f),
            "Viper polygon was approximated by a circle.");
        var e = new DungeonEncounter(1235);
        DungeonActor boss = new(1, 0x429F, center, Targetable: false, Hp: 0);
        DungeonActor player = new(100, 0, center, PartySlot: 1, ObjectIndex: 0);
        DungeonActor perspective = new(101, 0, center, PartySlot: 0, InCombat: true, ObjectIndex: 2);
        DungeonActor gowrow = new(2, 0x42A0, center);
        void Sync(double t, params DungeonActor[] adds) => e.Synchronize([boss, player, perspective, .. adds], center, Start.AddSeconds(t), playerId: 101);
        Sync(0, gowrow);
        Check(e.Active == null, "Viper pulled before current player entered combat.");
        player = player with { InCombat = true };
        perspective = perspective with { InCombat = false };
        Sync(1, gowrow);
        Check(e.Active != null, "Viper incorrectly required targetable/combat/HP-positive primary.");
        Check(e.AmbientAoes().Count(a => a.Id.StartsWith("job-quest/enemy/")) == 1,
            "Viper drew its invisible primary or omitted Gowrow.");
        e.RemoveActor(2);
        Sync(2, new DungeonActor(3, 0x42A0, center, Dead: true));
        Check(e.Active != null, "Destroyed live cached Gowrow was replaced by another dead Gowrow.");
        Sync(3, gowrow with { Dead = true });
        Check(e.Active != null, "A destroyed cached reference was rebound to a reused entity ID.");
        boss = boss with { Id = 10, Targetable = true, Hp = 100 };
        Sync(4, new DungeonActor(20, 0x42A0, center, Hp: 0));
        Check(e.Active != null, "New primary identity or Gowrow HP zero without Dead ended the source phase.");
        Sync(5, new DungeonActor(20, 0x42A0, center, Dead: true));
        Check(e.Active == null && !e.OutputCasts.Any(), "Cached Gowrow death did not finish or repulled primary.");
        boss = boss with { Id = 11 };
        Sync(6, new DungeonActor(30, 0x42A0, center));
        Check(e.Active != null, "New primary identity failed to reset Gowrow cache.");
        boss = boss with { Dead = true };
        Sync(7, new DungeonActor(30, 0x42A0, center));
        Check(e.Active == null, "Viper primary death did not end active phase.");
        boss = boss with { Dead = false };
        Sync(8, new DungeonActor(30, 0x42A0, center));
        Check(e.Active == null, "Completed primary was reopened after a later Dead=false update.");
        e.DirectorUpdate(0x40000005);
        Sync(9, new DungeonActor(30, 0x42A0, center));
        Check(e.Active == null, "Viper restarted during director wipe.");
        e.DirectorUpdate(0x40000011);
        Sync(10, new DungeonActor(30, 0x42A0, center));
        Check(e.Active != null, "Director recovery failed to reset completed instance.");
        e.RemoveActor(30);
        e.DirectorUpdate(0x40000011);
        Sync(11, new DungeonActor(31, 0x42A0, center, Dead: true));
        Sync(12, new DungeonActor(31, 0x42A0, center, Dead: true));
        Check(e.Active != null, "Isolated recovery rebound a destroyed live cached Gowrow.");
        boss = boss with { Dead = true };
        Sync(13);
        boss = boss with { Dead = false };
        e.DirectorUpdate(0x40000011);
        Sync(14);
        Check(e.Active == null, "Isolated recovery reopened a completed primary.");
        e.RemoveActor(boss.Id);
        Sync(15);
        Check(e.Active != null, "Same-ID primary recreation retained the previous instance completion.");
        e.Synchronize([boss, player], null, Start.AddSeconds(16), playerId: 999);
        Check(e.Active != null, "Missing replay perspective overrode source slot-zero pull.");
        var firstDead = new DungeonEncounter(1235);
        DungeonActor hidden = new(50, 0x429F, center, Targetable: false);
        DungeonActor deadGowrow = new(51, 0x42A0, center, Dead: true);
        firstDead.Synchronize([hidden, player, deadGowrow], center, Start, playerId: 100);
        Check(firstDead.Active != null, "First-cache dead Gowrow ended before source UpdateModule ordering.");
        firstDead.Synchronize([hidden, player, deadGowrow], center, Start.AddSeconds(.1), playerId: 100);
        Check(firstDead.Active == null, "Cached dead Gowrow did not end on the next update.");
        var prePull = new DungeonEncounter(1235);
        prePull.Synchronize([hidden, player with { InCombat = false }, deadGowrow], center, Start, playerId: 100);
        prePull.Synchronize([hidden, player, new(52, 0x42A0, center)], center, Start.AddSeconds(1), playerId: 100);
        prePull.Synchronize([hidden, player, new(52, 0x42A0, center)], center, Start.AddSeconds(2), playerId: 100);
        Check(prePull.Active != null, "Pre-pull observation cached a dead Gowrow before component activation.");
        var mismatch = new DungeonEncounter(1235) { SourcePlayerId = 101 };
        mismatch.Synchronize([hidden, player, perspective], center, Start, playerId: 100);
        Check(mismatch.Active == null, "Normal PlayerState identity was replaced by a different object-zero actor.");
        mismatch.Synchronize([hidden, player with { InCombat = false }, perspective with { InCombat = true }], center,
            Start.AddSeconds(1), playerId: 100);
        Check(mismatch.Active != null, "Normal PlayerState pull failed when object-zero actor disagreed.");
    }

    private static void Musosai()
    {
        var center = new Vector3(-217.27f, 0, -158.31f);
        var e = new DungeonEncounter(709);
        DungeonActor boss = new(1, 0x1BEE, center, InCombat: true);
        DungeonActor player = new(100, 0, center, PartySlot: 0);
        DungeonActor clone = new(2, 0x1BF0, center);
        e.Synchronize([boss, player, clone, new(3, 0x1BF1, center)], center, Start, playerId: 100);
        DungeonAmbientAoe[] Arashi() => e.AmbientAoes().Where(a => a.Id.StartsWith("musosai/arashi/")).ToArray();
        e.StartCast(1, new(8418, center, 0, Start, Start.AddSeconds(4.3)), Start);
        Check(Arashi().Length == 0, "Arashi visual invented predictions.");
        e.StartCast(1, new(8643, center, 0, Start.AddSeconds(1), Start.AddSeconds(4.3)), Start.AddSeconds(1));
        Check(Arashi().Single().Activation == Start.AddSeconds(4.3), "Arashi trigger missing.");
        e.Tick(Start.AddSeconds(1.1));
        e.Finish(1, 8643, Start.AddSeconds(1.1));
        clone = clone with { Position = center + Vector3.UnitX * 5, Dead = true, Targetable = false };
        e.Synchronize([boss, player, clone, new(3, 0x1BF1, center)], center, Start.AddSeconds(2), playerId: 100);
        Check(Arashi().Single().Position == clone.Position, "Dead/untargetable clone stopped contributing a dynamic circle.");
        e.StartCast(1, new(8642, center, 0, Start.AddSeconds(3), Start.AddSeconds(6.3)), Start.AddSeconds(3));
        Check(Arashi().Single().Activation == Start.AddSeconds(6.3), "Later trigger failed to replace shared activation.");
        e.Tick(Start.AddSeconds(100));
        Check(Arashi().Length == 1 && e.Hints(clone.Position).Contains("GTFO from aoe!"), "Clock expired Arashi or warning absent.");
        e.RemoveActor(2);
        Check(Arashi().Length == 0, "Destroyed clone retained circle.");
        e.Synchronize([boss, player, clone with { Id = 4 }], center, Start.AddSeconds(101), playerId: 100);
        Check(Arashi().Length == 1, "Empty clone list erased pending activation.");
        e.Effect(4, 8642, 1, Start.AddSeconds(102));
        Check(Arashi().Length == 1, "Non-Arashi effect cleared shared activation.");
        e.Effect(4, 8419, 2, Start.AddSeconds(103));
        Check(Arashi().Length == 0, "Arashi effect failed to clear all circles.");
        e.Synchronize([boss, player, clone with { Id = 4 }], center, Start.AddSeconds(104), playerId: 100);
        Check(Arashi().Length == 0, "Repeated observation resurrected resolved Arashi.");
        e.Synchronize([boss with { Cast = new(8643, center, 0, Start.AddSeconds(100), Start.AddSeconds(103.3)) },
            player, clone with { Id = 4 }], center, Start.AddSeconds(104.1), playerId: 100);
        Check(Arashi().Length == 0, "First delayed old snapshot resurrected resolved Arashi.");
        e.StartCast(1, new(8422, center, 0, Start.AddSeconds(105), Start.AddSeconds(130.3)), Start.AddSeconds(105));
        Check(e.PredictedDamage.Single().DamageType == "Raidwide" && e.OutputCasts.All(c => c.Value.Rule.Shape == null), "Musojin raidwide contract.");
        e.Synchronize([boss with { Hp = 0 }, player, clone], center, Start.AddSeconds(106), playerId: 100);
        Check(e.Active == null && !e.AmbientAoes().Any(), "Musosai primary HP0 incorrectly waited for clones.");
    }

    private static void Sage()
    {
        var center = new Vector3(0, 0, -82.146f);
        var arena = (WorldPolygonGeometry)JobQuestCatalog.Create(1022).Single().Arena(center);
        Check(arena.WorldPoints.Count == 20 && arena.WorldPoints[0] == center + Vector3.UnitZ * 18.5f,
            "Sage arena is not the source unrotated 20-gon.");
        var e = new DungeonEncounter(1022);
        DungeonActor boss = new(1, 0x3587, center, InCombat: true, TargetId: 2, IsEnemy: true, ObjectIndex: 2);
        DungeonActor npc = new(2, 0x3589, center + Vector3.UnitX * 8, FriendlyNpc: true, IsEnemy: false);
        DungeonActor player = new(100, 0, center, PartySlot: 0);
        e.Synchronize([boss, npc, player], center, Start, playerId: 100);
        Check(e.Actors[2].PartySlot == 24, "Lalah did not enter stable source NPC party slot.");
        e.StartCast(1, new(26535, center, 0, Start, Start.AddSeconds(8.3), 2), Start);
        Check(e.PredictedDamage.Single() is { TargetId: 2, PartySlot: 24, DamageType: "Tankbuster" },
            "Triple Threat lost NPC damage prediction.");
        e.Finish(1, 26535, Start.AddSeconds(.1));
        e.StartCast(1, new(26551, center, 0, Start.AddSeconds(1), Start.AddSeconds(9.3), 2), Start.AddSeconds(1));
        Check(!e.PredictedDamage.Any(), "Inactive DosisIII was migrated as an active component.");
        e.StartCast(1, new(26553, center, 0, Start.AddSeconds(2), Start.AddSeconds(7.3), 2), Start.AddSeconds(2));
        var display = e.AmbientAoes().Single(a => a.Id.StartsWith("quest/spread/") && a.Display);
        var avoid = e.AmbientAoes().Single(a => a.Id.EndsWith("/avoid"));
        Check(display.Shape is CircleTemplate { Radius: 5 } && display.OutlineOnly
            && avoid.Shape is CircleTemplate { Radius: 6 } && avoid.Activation == Start.AddSeconds(7.3),
            "NPC spread display R5 and AI R6 were conflated.");
        Check(e.PriorityTarget(0, center) == 1, "Enemy attacking party NPC was omitted from default targets.");
        npc = npc with { Position = center + Vector3.UnitX * 3 };
        e.Synchronize([boss with { Cast = new(26553, center, 0, Start.AddSeconds(2), Start.AddSeconds(7.3), 2) }, npc, player],
            center, Start.AddSeconds(3), playerId: 100);
        Check(e.Hints(center).Contains("GTFO from spreads!")
            && e.AmbientAoes().Single(a => a.Id.EndsWith("/avoid")).Position.X > 2.9f,
            "NPC spread did not follow current actor position or expose hint.");
        e.Synchronize([boss, npc, player], center, Start.AddSeconds(4), playerId: 100);
        Check(!e.AmbientAoes().Any(a => a.Id.StartsWith("quest/spread/")), "Cancelled spread was retained.");
        e.StartCast(1, new(26548, center, 0, Start.AddSeconds(5), Start.AddSeconds(10.9)), Start.AddSeconds(5));
        Check(e.PredictedDamage.Select(d => d.TargetId).Order().SequenceEqual(new ulong[] { 2, 100 })
            && e.OutputCasts.All(c => c.Value.Rule.Shape == null), "Sage raidwide lost NPC party or fabricated geometry.");
        e.Finish(1, 26548, Start.AddSeconds(5.1));
        e.StartCast(1, new(26553, center, 0, Start.AddSeconds(6), Start.AddSeconds(11.3), 100), Start.AddSeconds(6));
        Check(e.AmbientAoes().Any(a => a.Id.Contains("/separate/2")), "Own spread did not separate from unmarked NPC.");
        e.RemoveActor(2);
        e.Synchronize([boss with { Cast = new(26553, center, 0, Start.AddSeconds(6), Start.AddSeconds(11.3), 100) }, player],
            center, Start.AddSeconds(7), playerId: 100);
        Check(e.AmbientAoes().Any(a => a.Id.StartsWith("quest/spread/") && a.Display)
            && !e.AmbientAoes().Any(a => a.Id.StartsWith("quest/spread/") && a.Navigate),
            "Single party member should retain spread display without navigation.");
        e.Synchronize([boss with { Hp = 0 }, player, npc], center, Start.AddSeconds(8), playerId: 100);
        Check(e.Active == null, "Sage lifetime incorrectly waited for other bosses or Lalah.");
    }

    private static void Ompagne()
    {
        var center = new Vector3(-166.8f, 0, 290);
        var e = new DungeonEncounter(715);
        DungeonActor boss = new(1, 0x1C5E, center, InCombat: true, TargetId: 100, IsEnemy: true, ObjectIndex: 2);
        DungeonActor npc = new(2, 0x1C5C, center + Vector3.UnitX * 8, FriendlyNpc: true, IsEnemy: false);
        DungeonActor player = new(100, 0, center, PartySlot: 0);
        DungeonActor chain = new(3, 0x1C60, center, Hostile: false, IsEnemy: true, ObjectIndex: 4);
        DungeonActor buddy = new(101, 0, center, PartySlot: 1);
        e.Synchronize([boss, npc, player, buddy, chain], center, Start, playerId: 100);
        Check(e.PriorityTarget(1, center) == 3, "Chain priority5 failed to override default enemy priority.");
        e.StartCast(1, new(8457, center, 0, Start, Start.AddSeconds(5.3), 2), Start);
        e.Finish(1, 8457, Start.AddSeconds(1));
        npc = npc with { Position = center + Vector3.UnitX * 10, Targetable = false };
        e.Synchronize([boss, npc, player, buddy, chain], center, Start.AddSeconds(6), playerId: 100);
        var stack = e.AmbientAoes().Single(a => a.Id.StartsWith("quest/stack/") && a.Display);
        var join = e.AmbientAoes().Single(a => a.Id == "quest/stack/join");
        Check(stack.Position.X > center.X + 9.9f && stack.Safe && stack.OutlineOnly
            && join.Activation == Start.AddSeconds(5.3), "NPC stack lost position, duration, safe outline or original clock.");
        Check(join.Shape.Resolve(join.Position, 0, null).Contains(center)
            && !join.Shape.Resolve(join.Position, 0, null).Contains(npc.Position), "Stack was a soft goal instead of outside-circle danger.");
        Check(!e.PredictedDamage.Any(), "Source stack mask condition was replaced by invented shared damage.");
        e.Effect(1, 8457, 1, Start.AddSeconds(7));
        Check(e.AmbientAoes().Any(a => a.Id == "quest/stack/join"), "8457 effect cleared persistent stack.");
        e.StartCast(1, new(8457, center, 0, Start.AddSeconds(8), Start.AddSeconds(13.3), 100), Start.AddSeconds(8));
        var all = e.AmbientAoes().ToArray();
        Check(all.Count(a => a.Id.StartsWith("quest/stack/") && a.Display) == 2
            && all.Any(a => a.Id.EndsWith("/buddy")), "Multiple stack records or marked-player buddy movement missing.");
        e.Effect(999, 9298, 2, Start.AddSeconds(9));
        Check(!e.AmbientAoes().Any(a => a.Id.StartsWith("quest/stack/")), "Unrelated-source9298 failed to clear all stacks.");
        e.Finish(1, 8457, Start.AddSeconds(9.1));
        var correction = new DungeonCastObservation(8457, center, 0, Start.AddSeconds(9.2), Start.AddSeconds(14.5));
        e.StartCast(1, correction, Start.AddSeconds(9.2), fromPacket: false);
        e.StartCast(1, correction with { TargetId = 2 }, Start.AddSeconds(9.25));
        Check(e.AmbientAoes().Count(a => a.Id.StartsWith("quest/stack/") && a.Display) == 1,
            "Authoritative correction from unresolved target failed to create stack.");
        e.Finish(1, 8457, Start.AddSeconds(9.3));
        e.Effect(999, 9298, 3, Start.AddSeconds(9.4));
        e.StartCast(1, new(8457, center, 0, Start.AddSeconds(10), Start.AddSeconds(15.3), 999), Start.AddSeconds(10));
        e.Synchronize([boss, npc with { Id = 999 }, player, chain], center, Start.AddSeconds(11), playerId: 100);
        Check(!e.AmbientAoes().Any(a => a.Id.StartsWith("quest/stack/")), "Missing target at start was rebound later.");
        e.Synchronize([boss with { Hp = 0 }, npc, player, chain], center, Start.AddSeconds(12), playerId: 100);
        Check(e.Active == null && !e.AmbientAoes().Any(), "Ompagne did not clean up on primary-only HP0.");
    }

    private static void SageCorrections()
    {
        var center = new Vector3(0, 0, -82.146f);
        foreach (var fromPacket in new[] { false, true })
        {
            var e = new DungeonEncounter(1022);
            DungeonActor boss = new(1, 0x3587, center, InCombat: true, TargetId: 2, IsEnemy: true, ObjectIndex: 2);
            DungeonActor npc = new(2, 0x3589, center + Vector3.UnitX * 8, FriendlyNpc: true, IsEnemy: false);
            DungeonActor player = new(100, 0, center, PartySlot: 0);
            DungeonActor passive = new(3, 0x3586, center, TargetId: 2, IsEnemy: true, ObjectIndex: 4);
            e.Synchronize([boss, npc, player, passive], center, Start, playerId: 100);
            Check(e.PriorityTarget(3, center) == 1, "Passive enemy targeting NPC bypassed InCombat gate.");
            var cast = new DungeonCastObservation(26553, center, 0, Start, Start.AddSeconds(5.3), 2);
            e.StartCast(1, cast, Start);
            e.StartCast(1, cast with { TargetId = 100 }, Start.AddSeconds(.05), fromPacket);
            var display = e.AmbientAoes().Single(a => a.Id.StartsWith("quest/spread/") && a.Display);
            Check(MathF.Abs(display.Position.X) < .05f, "Same-time target change retained old spread target.");
            e.StartCast(1, cast, Start.AddSeconds(.1), fromPacket);
            display = e.AmbientAoes().Single(a => a.Id.StartsWith("quest/spread/") && a.Display);
            Check(display.Position.X > 7.9f, "Second same-time target change was blocked by synthetic Finish watermark.");
            e.Finish(1, 26553, Start.AddSeconds(1));
            Check(!e.AmbientAoes().Any(a => a.Id.StartsWith("quest/spread/")), "Corrected spread leaked after Finish.");
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
