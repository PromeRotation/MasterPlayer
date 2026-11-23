using System.Numerics;
using MasterPlayer.Mechanics.PilgrimsTraverse;
using MasterPlayer.Replay;
using PromeRotation.LogSystem;

internal static class PilgrimsNativeParityRegression
{
    private static readonly DateTime Start = new(2026, 11, 23, 0, 0, 0, DateTimeKind.Utc);

    public static void Run(Action<string, Action> run)
    {
        run("Pilgrims parity PT10 PT40 prioritize adds forbid only their dots and clean on reset", AddTargets);
        run("Pilgrims parity PT20 cancelled planting clears predictions and preserves committed roots", Planting);
        run("Pilgrims parity PT99 cancelled chains clear only prediction and preserve actual debuff", Chains);
        run("Pilgrims parity PT30 other perspectives retain following chase circles without bait goals", Chase);
        run("Pilgrims parity self spread and root bait retain outlines without self danger", PersonalOutlines);
        run("Pilgrims parity PT50 chase line and subsequent hits end when the target dies", Pit);
        run("Pilgrims parity PT99 icon bait and eight crystals publish forecast outlines before casts", Forecasts);
        run("Pilgrims parity sequence preview counts retain source telegraphs and all navigation windows", SequencePreviews);
        run("Pilgrims parity PT50 wind switches sand safety display and PT99 own fire bait stays outline", WindAndFire);
        run("Pilgrims parity knockback immunity lasts until the hit and includes Inner Strength", KnockbackImmunity);
        run("Pilgrims parity PT30 unsequenced first-target effects advance each chasing hit", UnsequencedChase);
        run("Pilgrims parity PT60 rotation rejects mismatched packets and preserves its angular grid", RotationGrid);
        run("Pilgrims parity PT30 moving ball previews preserve both source arc capsule directions", BallArcs);
        run("Pilgrims parity partial PT50 input retains late knockback and PT99 cancel clears fire bait", PartialAndCancel);
        run("Pilgrims parity PT10 spread damage diagnostics retain targets and unknown activation", SpreadDamage);
    }

    private static void SpreadDamage()
    {
        var e = new Pt10Encounter();
        var boss = Actor(1, e.BossOid, e.Center);
        var add = Actor(2, 0x460E, e.Center);
        var first = Actor(100, 0, e.Center) with { Kind = 1 };
        var second = Actor(101, 0, e.Center) with { Kind = 1 };
        e.Synchronize([boss, add, first, second], Start);
        Check(e.PredictedDamageTargets.Count == 0, "Spread damage appeared before Leafmash.");
        e.StartCast(new(1, 44055, e.Center, 0, Start, Start.AddSeconds(5), 0));
        Check(e.PredictedDamageTargets.Order().SequenceEqual(new ulong[] { 100, 101 })
            && e.Hints(Start, 100).Single().Contains("Raidwide×1")
            && e.Hints(Start, 100).Single().Contains("判定时间未知"),
            "Spread prediction lost party targets or invented an activation time.");
        e.Synchronize([boss, add with { Dead = true }, first, second], Start.AddSeconds(1));
        Check(e.PredictedDamageTargets.Count == 0 && !e.Hints(Start, 100).Any(),
            "Dead shrublets retained their spread damage prediction.");
        e.Reset();
        Check(e.PredictedDamageTargets.Count == 0, "Reset retained a spread prediction.");
    }

    private static ReplayActorState Actor(ulong id, uint oid, Vector3 position)
        => new() { Id = id, EntityId = (uint)id, Oid = oid, Position = position, InCombat = true, Hp = 1000, MaxHp = 1000, Targetable = true };

    private static void KnockbackImmunity()
    {
        foreach (var (encounter, action, key) in new (PtBossEncounter, uint, string)[]
        {
            (new Pt20Encounter(), 43455, "touchdown/landing"),
            (new Pt40Encounter(), 42128, "saltwater/landing"),
            (new Pt90Encounter(), 44266, "skinflayer/landing"),
        })
        {
            encounter.Synchronize([Actor(1, encounter.BossOid, encounter.Center),
                Actor(100, 0, encounter.Center) with { Kind = 1 }], Start);
            encounter.StartCast(new(1, action, encounter.Center, 0, Start, Start.AddSeconds(5), 100));
            Check(encounter.Collect(Start, 100).Any(a => a.Key == key), "Missing the nonimmune knockback baseline.");
            encounter.Apply(new LogSystemStatusAddEvent { TargetId = 100, StatusId = 2663, Duration = 10 }, Start);
            Check(!encounter.Collect(Start, 100).Any(a => a.Key == key), "Inner Strength failed to suppress knockback landing.");
            encounter.Apply(new LogSystemStatusAddEvent { TargetId = 100, StatusId = 2663, Duration = 4 }, Start);
            Check(encounter.Collect(Start, 100).Any(a => a.Key == key), "Immunity expiring before the hit suppressed knockback landing.");
        }
    }

    private static void UnsequencedChase()
    {
        var e = new Pt30Encounter();
        var center = new Vector3(-300, 0, -300);
        e.Synchronize([new(1, Pt30Encounter.BOSS_OID, center, 0, false, true),
            new(100, 0, center + Vector3.UnitX * 10, 0, false, false)], Start);
        e.Icon(100, 197, Start);
        e.Cast(10, 44918, center, 0, Start, Start.AddSeconds(5));
        e.Effect(10, 0, 44918, center, Start.AddSeconds(5));
        e.Effect(10, 0, 44918, center, Start.AddSeconds(5), 1);
        Check(e.Remaining == 7, "One unsequenced multi-target event advanced multiple hits.");
        var next = center + Vector3.UnitX * 3;
        e.Effect(10, 0, 44919, next, Start.AddSeconds(5.6));
        var hazard = e.Hazards(Start.AddSeconds(5.7), 101, center).Single(h => h.Key == "chase");
        Check(e.Remaining == 6 && Math.Abs((hazard.From - Start).TotalSeconds - 6.2) < .000001
            && e.ChaseLine == (next, center + Vector3.UnitX * 10), "A later unsequenced hit failed to advance the chase clock and origin.");
    }

    private static void RotationGrid()
    {
        var e = new Pt60Encounter();
        e.Synchronize([Actor(1, e.BossOid, e.Center)], Start);
        e.StartCast(new(1, 44867, e.Center, 0, Start, Start.AddSeconds(5), 100));
        void Effect(uint sequence, float rotation) => e.Apply(new LogSystemActionEffectEvent
        {
            SourceId = 1, ActionId = 44909, GlobalSequence = sequence, SourceRotation = rotation,
            SourcePosition = e.Center,
        }, Start.AddSeconds(5.1));
        Effect(1, .08f);
        Check(Math.Abs(e.Collect(Start.AddSeconds(5.1), 100).Single(a => a.Key == "rotate/0/0").Rotation) < .000001f,
            "A rotation packet outside the source tolerance consumed a hit.");
        Effect(2, .04f);
        var next = e.Collect(Start.AddSeconds(5.1), 100).Single(a => a.Key == "rotate/0/0");
        Check(Math.Abs(next.Rotation - MathF.PI / 3) < .000001f
            && Math.Abs((next.From - Start).TotalSeconds - 6) < .000001,
            "An accepted packet shifted the sequence angular grid or next-hit clock.");
        var output = e.Collect(Start.AddSeconds(5.1), 100);
        Check(output.Count(a => a.Shape is MasterPlayer.Spatial.SafeZone.FanTemplate && !a.NavigationOnly) == 4,
            "Rotating Needles must display the current and following cone on both sides.");
        var center = output.Single(a => a.Key == "rotate/center");
        Check(center.NavigationOnly && !center.Safe && center.Geometry.Contains(e.Center + Vector3.UnitX * 4.1f)
            && !center.Geometry.Contains(e.Center + Vector3.UnitX * 3.9f), "Rotating Needles center restriction became an optional goal.");
    }

    private static void BallArcs()
    {
        var start = new Vector3(-300, 0, -280);
        foreach (var heading in new[] { MathF.PI / 2, -MathF.PI / 2 })
        {
            var preview = Pt30Encounter.BallArcPreview(start, heading);
            var sweep = heading > 0 ? MathF.PI / 6 : -MathF.PI / 6;
            var source = new BossMod.AOEShapeArcCapsule(4, new(sweep), new(-300, -300));
            for (var x = -16f; x <= 16; x += .47f)
                for (var z = 12f; z <= 26; z += .43f)
                {
                    var point = new Vector3(-300 + x, 0, -300 + z);
                    var hit = source.Check(new(point.X, point.Z), new(start.X, start.Z), default);
                    Check(hit ? preview.Contains(point, .02f) : !preview.Contains(point, -.02f),
                        "Moving-ball preview differs from the source thirty-degree arc capsule.");
                }
        }
    }

    private static void PartialAndCancel()
    {
        var wind = new Pt50Encounter();
        wind.Synchronize([Actor(1, wind.BossOid, wind.Center)], Start);
        wind.StartCast(new(1, 43538, wind.Center, 0, Start, Start.AddSeconds(10), 100));
        Check(wind.Collect(Start.AddSeconds(8.1), 100).Any(a => a.Key == "wind/landing")
            && wind.KnockbackSources(Start.AddSeconds(8.1), 100).Count() == 1,
            "Missing Liquefaction input removed the final two seconds of Biting Wind protection.");
        var fire = new Pt99Encounter();
        fire.Synchronize([Actor(1, fire.BossOid, fire.Center), Actor(100, 0, fire.Center) with { Kind = 1 }], Start);
        fire.StartCast(new(1, 44061, fire.Center, 0, Start, Start.AddSeconds(5), 100));
        fire.Cancel(2);
        Check(fire.Collect(Start.AddSeconds(1), 100).Any(a => a.Key.StartsWith("fire/bait/")), "Another source cleared fire bait.");
        fire.Cancel(1);
        Check(!fire.Collect(Start.AddSeconds(1), 100).Any(a => a.Key.StartsWith("fire/bait/")), "Cancelled fire bait retained its visual forecast.");
    }

    private static void SequencePreviews()
    {
        var leaf = new Pt10Encounter();
        leaf.Synchronize([Actor(1, leaf.BossOid, leaf.Center)], Start);
        for (var i = 0; i < 4; ++i)
            leaf.Apply(new LogSystemActionEffectEvent { SourceId = 1, ActionId = 44058, GlobalSequence = (uint)(i + 1),
                EffectPosition = leaf.Center + Vector3.UnitX * i }, Start.AddSeconds(i));
        var leaves = leaf.Collect(Start, 100).Where(a => a.Key.StartsWith("leaf/")).ToArray();
        Check(leaves.Length == 4 && leaves.Count(a => !a.NavigationOnly) == 3, "Leafmash must display the next three jumps.");
        var tree = new Pt20Encounter();
        tree.Synchronize([Actor(1, tree.BossOid, tree.Center)], Start);
        for (var i = 0; i < 4; ++i)
            tree.Apply(new LogSystemStatusAddEvent { TargetId = (ulong)(10 + i), StatusId = 2056,
                Param = (ushort)(0x395 + i), Duration = 30 }, Start.AddSeconds(i));
        var bursts = tree.Collect(Start, 100).Where(a => a.Key.StartsWith("burst/")).ToArray();
        Check(bursts.Length == 4 && bursts.Count(a => !a.NavigationOnly) == 3, "Burst must display three circles while retaining the fourth future window.");
        var swipe = new Pt70Encounter();
        swipe.Synchronize([Actor(1, swipe.BossOid, swipe.Center)], Start);
        for (var i = 0; i < 8; ++i)
            swipe.StartCast(new((ulong)(10 + i), 43437, swipe.Center, i * .1f, Start, Start.AddSeconds(1 + i), 100));
        var swipes = swipe.Collect(Start, 100).Where(a => a.Key.StartsWith("swipe/")).ToArray();
        Check(swipes.Length == 8 && swipes.Count(a => !a.NavigationOnly) == 2, "Octuple Swipe must display the next two blades.");
        var lightning = new Pt80Encounter();
        lightning.Synchronize(new[] { Actor(1, lightning.BossOid, lightning.Center) }.Concat(
            Enumerable.Range(0, 9).Select(i => Actor((ulong)(10 + i), 0x485E, lightning.Center + Vector3.UnitX * (i - 4)))), Start);
        var charges = lightning.Collect(Start, 100).Where(a => a.Key.StartsWith("lightning/")).ToArray();
        Check(charges.Length == 9 && charges.All(a => !a.NavigationOnly), "Lightning hid a charge or the terminal explosion.");
    }

    private static void WindAndFire()
    {
        var sand = new Pt50Encounter();
        sand.Synchronize([Actor(1, sand.BossOid, sand.Center), Actor(100, 0, sand.Center) with { Kind = 1 }], Start);
        sand.StartCast(new(1, 43531, sand.Center, 0, Start, Start.AddSeconds(3), 100));
        sand.StartCast(new(1, 43538, sand.Center, 0, Start, Start.AddSeconds(10), 100));
        Check(sand.Collect(Start.AddSeconds(7.9), 100).Count(a => a.Key.StartsWith("sand/island/")) == 7,
            "Land previews disappeared before the two-second wind threshold.");
        var imminent = sand.Collect(Start.AddSeconds(8.1), 100);
        var shelter = imminent.Single(a => a.Key == "sand/shelter");
        var enter = imminent.Single(a => a.Key == "sand/enter");
        Check(shelter.Safe && shelter.PreviewOnly && !imminent.Any(a => a.Key.StartsWith("sand/island/")),
            "Wind displayed land as safe while navigation requires sand.");
        var sandPoint = Enumerable.Range(-19, 39).SelectMany(x => Enumerable.Range(-19, 39)
            .Select(z => sand.Center + new Vector3(x, 0, z))).First(p => shelter.Geometry.Contains(p));
        Check(!enter.Geometry.Contains(sandPoint), "Safe sand display contradicts the enter-sand navigation geometry.");
        sand.Apply(new LogSystemActionEffectEvent { SourceId = 1, ActionId = 43538, GlobalSequence = 1 }, Start.AddSeconds(10));
        Check(sand.Collect(Start.AddSeconds(10.1), 100).Count(a => a.Key.StartsWith("sand/island/")) == 7,
            "Wind resolution retained the reversed safety display.");
        var fire = new Pt99Encounter();
        fire.Synchronize([Actor(1, fire.BossOid, fire.Center),
            Actor(100, 0, fire.Center) with { Kind = 1 }, Actor(101, 0, fire.Center + Vector3.UnitX * 5) with { Kind = 1 }], Start);
        fire.StartCast(new(1, 44061, fire.Center, 0, Start, Start.AddSeconds(5), 100));
        var bait = fire.Collect(Start, 100);
        Check(bait.Single(a => a.Key == "fire/bait/100") is { PreviewOnly: true, OutlineOnly: true }
            && bait.Single(a => a.Key == "fire/bait/101") is { PreviewOnly: true, OutlineOnly: false },
            "Fire bait did not distinguish the viewing player's outline from another player's fill.");
    }

    private static void AddTargets()
    {
        foreach (var encounter in new PtBossEncounter[] { new Pt10Encounter(), new Pt40Encounter() })
        {
            var oid = encounter.Floor == 10 ? 0x460Eu : 0x460Cu;
            var boss = Actor(1, encounter.BossOid, encounter.Center);
            var player = Actor(100, 0, encounter.Center) with { Kind = 1 };
            var first = Actor(10, oid, encounter.Center + Vector3.UnitX);
            var second = Actor(11, oid, encounter.Center + Vector3.UnitZ * 2);
            encounter.Synchronize([boss, player, first, second, Actor(12, oid, encounter.Center) with { Dead = true }], Start);
            Check(encounter.SelectTarget(Start, 100, 1)?.Id == 10, "Adds did not outrank the boss.");
            Check(encounter.SelectTarget(Start, 100, 11)?.Id == 11, "An equivalent current add target was lost.");
            Check(encounter.ForbiddenDotTargets.SequenceEqual([10UL, 11UL]), "DoT restriction included the boss or a dead add.");
            encounter.Synchronize([boss, player, first with { Dead = true }], Start.AddSeconds(1));
            Check(encounter.SelectTarget(Start, 100, 10)?.Id == 1 && encounter.ForbiddenDotTargets.Count == 0, "Dead adds prevented boss fallback.");
            encounter.Reset();
            Check(encounter.ForbiddenDotTargets.Count == 0 && encounter.SelectTarget(Start, 100, 1) == null, "Reset retained add control.");
        }
    }

    private static void Planting()
    {
        var e = new Pt20Encounter();
        e.Synchronize([Actor(1, e.BossOid, e.Center)], Start);
        var cast = new PtBossEncounter.Cast(10, 43461, e.Center, 0, Start, Start.AddSeconds(5), 100);
        e.StartCast(cast);
        e.Cancel(11);
        Check(e.Collect(Start, 100).Any(a => a.Key.StartsWith("embrace/")), "Another source cancelled planting.");
        e.Cancel(10);
        Check(!e.Collect(Start, 100).Any(a => a.Key.StartsWith("embrace/") || a.Key.StartsWith("roots/")), "Cancelled planting retained permanent predictions.");
        e.StartCast(cast with { Hit = Start.AddSeconds(6) });
        e.Apply(new LogSystemStatusAddEvent { TargetId = 10, StatusId = 1909, Param = 3, Duration = 30 }, Start.AddSeconds(6));
        e.Cancel(10);
        var roots = e.Collect(Start.AddSeconds(6), 100);
        Check(roots.Count(a => a.Key.StartsWith("roots/")) == 4 && roots.Count(a => a.Key.StartsWith("embrace/")) == 4,
            "Cancelling a later cast erased physically committed roots.");
        e.StartCast(cast with { Source = 11, Position = e.Center + Vector3.UnitX * 8, Hit = Start.AddSeconds(12) });
        e.Cancel(11);
        var restored = e.Collect(Start.AddSeconds(7), 100);
        Check(roots.Where(a => a.Key.StartsWith("roots/") || a.Key.StartsWith("embrace/")).All(a => restored.Contains(a)),
            "Cancelling another planting erased or relocated a committed root wave.");
    }

    private static void Chains()
    {
        var e = new Pt99Encounter();
        e.Synchronize([Actor(1, e.BossOid, e.Center)], Start);
        var cast = new PtBossEncounter.Cast(10, 44064, e.Center, 0, Start, Start.AddSeconds(5), 100);
        e.StartCast(cast);
        e.Cancel(11);
        Check(e.HoldMovement(Start.AddSeconds(5.2), 100), "Other-source cancellation erased chains.");
        e.Cancel(10);
        Check(!e.HoldMovement(Start.AddSeconds(5.2), 100), "Cancelled chains retained a movement hold.");
        e.Apply(new LogSystemStatusAddEvent { TargetId = 100, StatusId = 4562, Duration = 10 }, Start);
        e.Cancel(10);
        Check(e.HoldMovement(Start.AddSeconds(5.2), 100), "Cancellation removed an actual chain debuff.");
    }

    private static void Chase()
    {
        var e = new Pt30Encounter();
        var center = new Vector3(-300, 0, -300);
        e.Synchronize([new(1, Pt30Encounter.BOSS_OID, center, 0, false, true),
            new(100, 0, center + Vector3.UnitX * 10, 0, false, false)], Start);
        e.Icon(100, 197, Start);
        e.Cast(10, 44918, center, 0, Start, Start.AddSeconds(5));
        Check(e.ChaseLine == (center, center + Vector3.UnitX * 10), "Initial chase cast omitted its target line.");
        e.Effect(10, 1, 44918, center, Start.AddSeconds(5));
        var hazard = e.Hazards(Start.AddSeconds(5.1), 101, center + Vector3.UnitZ * 10).Single(h => h.Key == "chase");
        Check(Math.Abs(hazard.Position.X - (center.X + 3.5f)) < .001f && hazard.Position.Z == center.Z,
            "Non-target perspective lost the actual target's next chase circle.");
        Check(e.Goal == null && Math.Abs((hazard.From - Start).TotalSeconds - 5.6) < .000001, "Non-target received a bait goal or incorrect next-hit timing.");
        Check(e.ChaseLine == (center, center + Vector3.UnitX * 10), "Chasing line did not connect the last hit to the target.");
        e.Cancel(10);
        Check(!e.Hazards(Start.AddSeconds(5.1), 101, center).Any(h => h.Key == "chase"), "Chase cancellation retained previews.");
        e.Reset();
        e.Synchronize([new(1, Pt30Encounter.BOSS_OID, center, 0, false, true),
            new(100, 0, center + Vector3.UnitX * 10, 0, false, false)], Start);
        e.Icon(100, 197, Start);
        e.Cast(10, 44918, center, 0, Start, Start.AddSeconds(5));
        e.Synchronize([new(1, Pt30Encounter.BOSS_OID, center, 0, false, true),
            new(100, 0, center + Vector3.UnitX * 10, 0, true, false)], Start.AddSeconds(4));
        Check(e.Hazards(Start.AddSeconds(4), 101, center).Any(h => h.Key == "chase"), "Death incorrectly removed the locked first hit.");
        e.Effect(10, 2, 44918, center, Start.AddSeconds(5));
        e.Synchronize([new(1, Pt30Encounter.BOSS_OID, center, 0, false, true),
            new(100, 0, center + Vector3.UnitX * 10, 0, true, false)], Start.AddSeconds(5.1));
        Check(!e.Hazards(Start.AddSeconds(5.1), 101, center).Any(h => h.Key == "chase") && e.Remaining == 0,
            "A dead target retained following hits.");
        Check(e.ChaseLine == null, "A dead target retained its chasing line.");
    }

    private static void PersonalOutlines()
    {
        var leaf = new Pt10Encounter();
        leaf.Synchronize([Actor(1, leaf.BossOid, leaf.Center), Actor(10, 0x460E, leaf.Center),
            Actor(100, 0, leaf.Center) with { Kind = 1 }], Start);
        leaf.StartCast(new(1, 44055, leaf.Center, 0, Start, Start.AddSeconds(5), 100));
        Check(leaf.Collect(Start, 100).Single(a => a.Key == "spread/100") is { PreviewOnly: true, OutlineOnly: true },
            "The local player's spread ring was not display-only.");
        var tree = new Pt20Encounter();
        tree.Synchronize([Actor(1, tree.BossOid, tree.Center), Actor(100, 0, tree.Center) with { Kind = 1 }], Start);
        tree.Apply(new LogSystemTargetIconEvent { TargetId = 100, MarkerId = 23 }, Start);
        var bait = tree.Collect(Start, 100).Where(a => a.Key.StartsWith("root-bait/") && a.Key != "root-bait/goal").ToArray();
        Check(bait.Length == 5 && bait.All(a => a.PreviewOnly && a.OutlineOnly), "Own planting bait lost its circular or cross outlines.");
    }

    private static void Pit()
    {
        var e = new Pt50Encounter();
        var boss = Actor(1, e.BossOid, e.Center);
        var player = Actor(100, 0, e.Center + Vector3.UnitX * 10) with { Kind = 1 };
        e.Synchronize([boss, player], Start);
        e.Apply(new LogSystemTargetIconEvent { TargetId = 100, MarkerId = 640 }, Start);
        e.StartCast(new(10, 43534, e.Center, 0, Start, Start.AddSeconds(5), 100));
        Check(e.DrawingLines(Start).Single() == ("pit/line", e.Center, player.Position), "Initial Pit cast omitted its target line.");
        e.Apply(new LogSystemActionEffectEvent { SourceId = 10, ActionId = 43534, GlobalSequence = 1, SourcePosition = e.Center }, Start.AddSeconds(5));
        Check(e.DrawingLines(Start.AddSeconds(5)).Single() == ("pit/line", e.Center, player.Position), "Pit chase line missing after first hit.");
        e.Synchronize([boss, player with { Dead = true }], Start.AddSeconds(5.1));
        Check(!e.Collect(Start.AddSeconds(5.1), 101).Any(a => a.Key.StartsWith("pit/"))
            && !e.DrawingLines(Start.AddSeconds(5.1)).Any(), "Dead target retained Pit's follow-up outputs.");
    }

    private static void Forecasts()
    {
        var e = new Pt99Encounter();
        var boss = Actor(1, e.BossOid, e.Center);
        var player = Actor(100, 0, e.Center + Vector3.UnitX * 10) with { Kind = 1 };
        e.Synchronize([boss, player], Start);
        e.Apply(new LogSystemTargetIconEvent { TargetId = 100, MarkerId = 234 }, Start);
        Check(e.Collect(Start, 101).Single(a => a.Key == "spinelash/preview") is { PreviewOnly: true, OutlineOnly: false },
            "Another perspective lost the icon's rectangle before Spinelash started.");
        Check(e.Collect(Start, 100).Single(a => a.Key == "spinelash/preview").OutlineOnly, "Own Spinelash was not an outline.");
        Check(MathF.Abs(e.Collect(Start, 100).Single(a => a.Key == "spinelash/preview").Rotation
            - (-.003f * MathF.PI / 180)) < .000001f, "Spinelash did not preserve the fixed source preview direction.");
        e.StartCast(new(10, 45118, e.Center, 0, Start.AddSeconds(1), Start.AddSeconds(5), 100));
        Check(!e.Collect(Start.AddSeconds(1), 100).Any(a => a.Key == "spinelash/preview"), "Cast start retained the icon-only bait.");
        var crystals = Enumerable.Range(0, 8).Select(i => Actor((ulong)(20 + i), 0x1EBE70,
            e.Center + new Vector3(i % 2 * 4, 0, i / 2 * 4 - 6))).ToArray();
        e.Synchronize(crystals.Concat([boss, player]), Start.AddSeconds(1));
        Check(e.Collect(Start.AddSeconds(1), 100).Single(a => a.Key == "blaze-outline")
            is { PreviewOnly: true, OutlineOnly: true, Safe: true }, "Eight crystals failed to forecast the safe outline.");
        foreach (var crystal in crystals)
            e.StartCast(new(crystal.Id, 44079, crystal.Position, 0, Start.AddSeconds(2), Start.AddSeconds(7), 0));
        Check(!e.Collect(Start.AddSeconds(2), 100).Any(a => a.Key == "blaze-outline"), "All sixteen lines failed to replace the crystal forecast.");
        var secondCrystals = crystals.Select(c => c with { Id = c.Id + 100 }).ToArray();
        e.Synchronize(secondCrystals.Concat([boss, player]), Start.AddSeconds(30));
        e.Cancel(crystals[0].Id);
        Check(e.Collect(Start.AddSeconds(30), 100).Any(a => a.Key == "blaze-outline"),
            "A late cancellation from an expired wave removed the next crystal preview.");
        e.StartCast(new(secondCrystals[0].Id, 44079, secondCrystals[0].Position, 0,
            Start.AddSeconds(31), Start.AddSeconds(36), 0));
        Check(e.Collect(Start.AddSeconds(31), 100).Any(a => a.Key == "blaze-outline"), "Unresolved expired lines prematurely removed the next round's preview.");
        e.Cancel(secondCrystals[0].Id);
        Check(!e.Collect(Start.AddSeconds(31), 100).Any(a => a.Key == "blaze-outline"), "Cancelled blaze retained a permanent safe outline.");
        e.StartCast(new(secondCrystals[1].Id, 44079, secondCrystals[1].Position, 0,
            Start.AddSeconds(31.1), Start.AddSeconds(36.1), 0));
        Check(e.Collect(Start.AddSeconds(31.2), 100).Any(a => a.Key.StartsWith("blaze/") && a.Owner == secondCrystals[1].Id),
            "Cancelling one helper erased another helper's future crystal pairing.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
