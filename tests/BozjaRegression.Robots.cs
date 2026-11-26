using System.Numerics;
using System.Reflection;
using BossMod;
using BossMod.Components;
using MasterPlayer.MechanicHost;
using MasterPlayer.MechanicRuntime;
using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Replay;

internal static partial class BozjaRegression
{
    private static void RunRobots(Action<string, Action> run)
    {
        run("Bozja native CE32 five components client polygon pull manifest", RobotClient);
        run("Bozja native CE32 Finish arena Created order cold replay dynamic NPC clock", RobotArena);
        run("Bozja native CE32 math full MaxHP divisor prime matrix twenty sided display navigation split", RobotMath);
        run("Bozja native CE32 math true party eight dead current hp ignored Finish any caster", RobotParty);
        run("Bozja native CE32 source stale math and empty pending exceptions explicit withdrawal recovery", RobotFailures);
        run("Bozja native CE32 train zero direction icon permutations unknown icons exact two Starts", RobotTrainStarts);
        run("Bozja native CE32 train forward Effect advances both six twelve hidden cache and final color", RobotTrainEffects);
        run("Bozja native CE32 march true slots duplicate Lose pending first direction lead facing", RobotMarch);
        run("Bozja native CE32 dynamic raidwide two watched actions original clock", () => Damage(27));
        run("Bozja native CE32 real Feed five components cold icon status slots event clocks normalized host", RobotFeed);
        run("Bozja native CE32 alliance zero length forced projection does not withdraw valid hints", RobotAllianceZero);
    }

    private static void RobotClient()
    {
        using var f = new Fixture(27);
        Check(f.M.IsRobots && f.M.Definition.Rules.Length == 5, "CE32 five components");
        Check(Service.LuminaRow<Lumina.Excel.Sheets.DynamicEvent>(14)?.Name.ToString() == f.M.Definition.Name, "CE32 client name");
        foreach (var action in Enum.GetValues<BossMod.Shadowbringers.Foray.CriticalEngagement.CE32RiseOfTheRobots.AID>())
            Check(data!.GetExcelSheet<Lumina.Excel.Sheets.Action>()!.TryGetRow((uint)action, out _)
                && new ActionID(ActionType.Spell, (uint)action).CastTimeExtra() == 0, "CE32 client action " + action);
        for (var x = -31f; x <= 31; x += .417f)
            for (var z = -31f; z <= 31; z += .389f)
            {
                var p = f.Center + new Vector3(x, 0, z);
                Check(f.Source.Arena.InBounds(new(p.X, p.Z)) == f.M.Arena.Contains(p, 0), "CE32 polygon");
            }
        f.M.Clear();
        f.Set(100, a => a with { Position = f.Center + Vector3.UnitX * 30.001f }); f.Sync();
        Check(!f.M.Active, "CE32 pull outside");
        f.Set(100, a => a with { Position = f.Center + Vector3.UnitX * 30 }); f.Sync();
        Check(f.M.Active, "CE32 pull inclusive");
    }

    private static ActorCastInfo RobotStart(Fixture f, BossComponent source, uint action, ulong id = 2, float rotation = 0, float total = 8)
    {
        var actor = f.W.Actors.Find(id)!;
        var spell = new ActorCastInfo { Action = new(ActionType.Spell, action), TotalTime = total,
            Location = actor.Position.ToVec3(), Rotation = new(rotation) };
        actor.CastInfo = spell;
        source.OnCastStarted(actor, spell);
        var target = f.M.Actors[id];
        target.Cast = new(action, 0, actor.Position.ToVec3(), rotation, f.Now, f.Now.AddSeconds(total + .3f), false, false)
        { IsSpell = true, TotalTime = total };
        target.Elapsed = 0;
        f.M.Start(target);
        f.Set(id, a => a with { Cast = target.Cast });
        f.M.CommitActorInput(f.Actors.Single(a => a.Id == id));
        return spell;
    }

    private static void RobotFinish(Fixture f, BossComponent source, ActorCastInfo spell, ulong id = 2)
    {
        source.OnCastFinished(f.W.Actors.Find(id)!, spell);
        f.M.Finish(f.M.Actors[id], f.M.Actors[id].Cast! with { ActionId = spell.Action.ID });
    }

    private static void RobotCompare(Fixture f, GenericAOEs source, string component, string step, bool navigation = true)
    {
        var expected = source.ActiveAOEs(0, f.Viewer).ToArray();
        var actual = f.M.Visible(f.NativeViewer).Where(a => a.Cast.Rule.Component == component).ToArray();
        Check(expected.Select(a => (a.Origin.ToVec3(), a.Rotation.Rad, a.Activation, a.Risky, a.Color))
            .SequenceEqual(actual.Select(a => (a.Cast.Origin, a.Cast.Rotation, a.Cast.Activation, a.Risky,
                a.Danger ? Colors.Danger : a.SafeColor ? Colors.SafeFromAOE : component is "Train" or "MagitekSlash" ? Colors.AOE : 0))), step + " ordered fields");
        for (var i = 0; i < actual.Length; ++i)
            for (var x = -27f; x <= 27; x += .731f)
                for (var z = -27f; z <= 27; z += 1.137f)
                {
                    var p = f.Center + new Vector3(x, 0, z);
                    Check(expected[i].Check(new(p.X, p.Z)) == actual[i].Cast.Contains(p), step + " raw Check");
                }
        if (!navigation) return;
        var ai = new AIHints(); source.AddAIHints(0, f.Viewer, default, ai);
        var zones = f.M.Navigation(f.NativeViewer).Where(z => z.Component == component).ToArray();
        Check(ai.ForbiddenZones.Count == zones.Length, step + " raw navigation count");
        for (var i = 0; i < zones.Length; ++i)
        {
            var converted = MechanicHintsConvert.ConvertForbiddenZone(ai.ForbiddenZones[i].Item1, 0, f.M.Center, f.M.SampleRadius, out var kind);
            if (kind == MechanicNavZoneKind.ComplementSkipped
                && MechanicHintsConvert.TryConvertComplement(ai.ForbiddenZones[i].Item1, 0, out var goals)) converted = goals;
            var shapes = zones[i].Convert(f.M.Center, f.M.SampleRadius).ToArray();
            Check(converted.Count == shapes.Length && ai.ForbiddenZones[i].Item2 == zones[i].Activation, step + " actual host conversion");
            for (var x = -27f; x <= 27; x += .821f)
                for (var z = -27f; z <= 27; z += 1.237f)
                {
                    var p = f.Center + new Vector3(x, 0, z);
                    Check(converted.Any(c => c.Template.Resolve(c.Position, c.Rotation, null).Contains(p, 0))
                        == shapes.Any(s => s.Contains(p, 0)), step + " converted membership");
                }
        }
    }

    private static void RobotArena()
    {
        foreach (var wallFirst in new[] { false, true })
        {
            using var f = new Fixture(27);
            var source = f.Activate<GenericAOEs>("ArenaChange");
            if (wallFirst) { f.Add(30, 0x1EB032, f.Center); f.Sync(); }
            var spell = f.Start(source, 20671);
            RobotCompare(f, source, "ArenaChange", "CE32 Start has no preview");
            RobotFinish(f, source, spell);
            RobotCompare(f, source, "ArenaChange", "CE32 Finish ring wallFirst=" + wallFirst);
            if (!wallFirst) { f.Add(30, 0x1EB032, f.Center); f.Sync(); }
            Check(f.Source.Center.ToVec3() == f.M.Center, "CE32 quantized center");
            f.Start(source, 20671); RobotFinish(f, source, f.W.Actors.Find(2)!.CastInfo!);
            RobotCompare(f, source, "ArenaChange", "CE32 Finish after wall still ring");
        }
        using var cold = new Fixture(27);
        cold.Add(30, 0x1EB032, cold.Center); cold.M.Clear(); cold.Sync();
        var expected = cold.Activate<GenericAOEs>("ArenaChange");
        Check(cold.M.Center == cold.Source.Center.ToVec3() && cold.M.Arena.GetContours()[0].Count > 0, "CE32 cold wall");
        RobotCompare(cold, expected, "ArenaChange", "CE32 cold wall no invented Finish");
    }

    private static void RobotHp(Fixture f, uint hp, ulong id = 100)
    {
        f.W.Actors.Find(id)!.HPMP.MaxHP = hp;
        f.Set(id, a => a with { MaxHp = hp, Hp = 1 });
        f.Sync();
    }

    private static void RobotMath()
    {
        foreach (var action in new uint[] { 20677, 20678, 20679, 20680 })
            for (var hp = 0u; hp <= 9; ++hp)
            {
                using var f = new Fixture(27);
                RobotHp(f, hp);
                var source = f.Activate<GenericAOEs>("OrderTowers");
                var spell = f.Start(source, action, total: 16);
                var numbers = (List<uint>[])source.GetType().GetField("Numbers")!.GetValue(source)!;
                Check(numbers[0].SequenceEqual(f.M.RobotNumbers[0]!), "CE32 full raw numbers " + hp + "/" + action);
                RobotCompare(f, source, "OrderTowers", "CE32 math " + hp + "/" + action);
                var expectedShape = source.ActiveAOEs(0, f.Viewer)[0];
                foreach (var vertex in f.M.RobotOrders[0]!.Cast.Shape.Resolve(f.M.Center, 0, null).GetContours().SelectMany(c => c))
                    foreach (var z in new[] { vertex.Z, MathF.BitIncrement(vertex.Z), MathF.BitDecrement(vertex.Z) })
                    {
                        var p = vertex with { Z = z };
                        Check(expectedShape.Check(new(p.X, p.Z)) == f.M.RobotOrders[0]!.Cast.Contains(p),
                            "CE32 exact polygon vertex and ULP boundary " + hp + "/" + action + "/" + p);
                    }
                var hints = new BossComponent.TextHints(); source.AddHints(0, f.Viewer, hints);
                Check(hints.Select(h => (h.Item1, h.Item2)).SequenceEqual(f.M.Hints(f.NativeViewer)
                    .Select(h => (h.Text, h.Risky))), "CE32 math hints");
                var aoe = f.M.RobotOrders[0]!;
                Check(aoe.Risky && aoe.SafeColor == (numbers[0][0] != 0), "CE32 safe color remains Risky");
                Check(aoe.Cast.Shape.Resolve(f.M.Center, 0, null) is MasterPlayer.Spatial.Geometry.PolygonRegionGeometry polygon
                    && polygon.FillPolygons.Count > 0, "CE32 real filled polygon");
                RobotFinish(f, source, spell);
                RobotCompare(f, source, "OrderTowers", "CE32 Finish clears math");
            }
    }

    private static void RobotParty()
    {
        using var f = new Fixture(27);
        f.Add(102, 0, f.Center, ActorType.Player);
        f.Party.Add(new(8, "", 102));
        f.W.Execute(new PartyState.OpModify(8, new(102, 102, false, "Alliance")));
        RobotHp(f, 6); RobotHp(f, 7, 101); RobotHp(f, 3, 102);
        f.W.Actors.Find(101)!.IsDead = true; f.Set(101, a => a with { Dead = true }); f.Sync();
        var source = f.Activate<GenericAOEs>("OrderTowers");
        var spell = f.Start(source, 20677);
        Check(f.M.RobotNumbers[1]!.Count > 0 && source.ActiveAOEs(1, f.W.Actors.Find(101)!).Length == 1
            && source.ActiveAOEs(8, f.W.Actors.Find(102)!).Length == 0, "CE32 dead own party included alliance excluded");
        var helper = f.W.Actors.Find(1)!; source.OnCastFinished(helper, spell);
        f.M.Finish(f.M.Actors[1], f.M.Actors[2].Cast!);
        Check(f.M.RobotOrders.All(a => a == null) && f.M.RobotNumbers.All(n => n == null), "CE32 any caster Finish clears all slots");
    }

    private static void RobotFailures()
    {
        using var f = new Fixture(27);
        RobotHp(f, 6);
        var source = f.Activate<GenericAOEs>("OrderTowers");
        RobotStart(f, source, 20677);
        RobotHp(f, 10);
        RobotStart(f, source, 20678, 1);
        Exception? failure = null;
        try { source.AddHints(0, f.Viewer, new()); }
        catch (ArgumentOutOfRangeException ex) { failure = ex; }
        Check(failure?.StackTrace?.Contains("OrderTowers.AddHints") == true
            && f.M.RobotOrders[0] != null && f.M.RobotNumbers[0]!.Count == 0, "CE32 actual overlapping-caster stale raw input");
        f.M.PrepareRobotOutputs(f.NativeViewer);
        Check(f.M.RobotHintError != null && !f.M.Hints(f.NativeViewer).Any()
            && f.M.Visible(f.NativeViewer).Any(a => a.Cast.Rule.Kind == BozjaRuleKind.RobotOrder), "CE32 explicit hint diagnostic retains drawings");
        var march = f.Activate<StatusDrivenForcedMarch>("OrderForcedMarch");
        var status = new ActorStatus(1293, 0, START.AddSeconds(3), 0);
        march.OnStatusGain(f.Viewer, ref status);
        f.Set(100, a => a with { Statuses = [new(1293, 0, 0, status.ExpireAt) { SlotIndex = 59 }] }); f.Sync();
        failure = null;
        try { march.AddAIHints(0, f.Viewer, default, new()); }
        catch (ArgumentOutOfRangeException ex) { failure = ex; }
        Check(failure?.StackTrace?.Contains("OrderForcedMarch.AddAIHints") == true, "CE32 actual empty numbers pending source exception");
        f.M.PrepareRobotOutputs(f.NativeViewer);
        Check(f.M.RobotNavigationError != null && !f.M.NavigationForHost(f.NativeViewer).Any(), "CE32 whole navigation withdrawn");
        RobotHp(f, 7); RobotStart(f, source, 20679, 1); f.M.PrepareRobotOutputs(f.NativeViewer);
        Check(f.M.RobotNavigationError == null && f.M.RobotHintError == null
            && f.M.Navigation(f.NativeViewer).Any(), "CE32 later valid Start recovery");
    }

    private static void RobotTrainStarts()
    {
        foreach (var iconFirst in new[] { false, true })
            foreach (var icon in new uint[] { 235, 236 })
            {
                using var f = new Fixture(27);
                var source = f.Activate<GenericRotatingAOE>("Train");
                void Icon(uint value) { source.OnEventIcon(f.Viewer, value, 0); f.M.Icon(100, value, 0); }
                if (iconFirst) Icon(icon);
                RobotStart(f, source, 20672, 1, 0);
                RobotStart(f, source, 20685, 2, MathF.PI);
                if (!iconFirst) Icon(icon);
                source.Update(); f.M.Update(f.Now);
                RobotCompare(f, source, "Train", "CE32 zero/front/back Start iconFirst=" + iconFirst);
                Check(source.Sequences.Count == 2 && source.Sequences.All(s => s.NumRemainingCasts == 12), "CE32 last Start decides count");
            }
        using var cold = new Fixture(27);
        var train = cold.Activate<GenericRotatingAOE>("Train");
        RobotStart(cold, train, 20672, 1); RobotStart(cold, train, 20674);
        train.Update(); cold.M.Update(cold.Now);
        RobotCompare(cold, train, "Train", "CE32 missing icon history no sequence");
        train.OnEventIcon(cold.Viewer, 999, 0); cold.M.Icon(100, 999, 0);
        RobotStart(cold, train, 20672, 1);
        train.OnEventIcon(cold.Viewer, 235, 0); cold.M.Icon(100, 235, 0);
        train.Update(); cold.M.Update(cold.Now);
        RobotCompare(cold, train, "Train", "CE32 third Start blocks exact count2");
    }

    private static void RobotTrainEffects()
    {
        foreach (var annihilate in new[] { false, true })
        {
            using var f = new Fixture(27);
            var source = f.Activate<GenericRotatingAOE>("Train");
            source.OnEventIcon(f.Viewer, 236, 0); f.M.Icon(100, 236, 0);
            RobotStart(f, source, annihilate ? 20683u : 20672u, 1);
            RobotStart(f, source, annihilate ? 20685u : 20674u, 2, MathF.PI);
            source.Update(); f.M.Update(f.Now);
            RobotCompare(f, source, "Train", "CE32 initial dual sequence");
            var initial = f.M.Visible(f.NativeViewer).ToArray();
            f.Now = START.AddSeconds(50); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
            source.Update(); f.M.Update(f.Now);
            Check(f.M.Visible(f.NativeViewer).SequenceEqual(initial), "CE32 no version cache retains overdue clocks");
            foreach (var action in new uint[] { 20675, 20686, 20674 })
            {
                source.OnEventCast(f.W.Actors.Find(2)!, PatriotEvent(action)); f.M.Effect(2, action);
            }
            Check(source.NumCasts == 0 && f.M.RobotNumCasts == 0, "CE32 backs not consumed");
            for (var i = 0; i < (annihilate ? 12 : 6); ++i)
            {
                var action = annihilate ? 20684u : 20673u;
                source.OnEventCast(f.W.Actors.Find(2)!, PatriotEvent(action)); f.M.Effect(2, action);
                source.Update(); f.M.Update(f.Now);
                Check(source.NumCasts == f.M.RobotNumCasts, "CE32 two sequence increments each forward");
                RobotCompare(f, source, "Train", "CE32 forward dual effect " + i);
            }
            Check(!f.M.Visible(f.NativeViewer).Any(), "CE32 true final forward clears");
        }
    }

    private static void RobotMarch()
    {
        using var f = new Fixture(27);
        RobotHp(f, 7);
        var math = f.Activate<GenericAOEs>("OrderTowers"); f.Start(math, 20677);
        var source = f.Activate<StatusDrivenForcedMarch>("OrderForcedMarch");
        f.M.RobotRandomOdd = (float)source.GetType().GetField("randomOdd", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(source)!;
        void Status(int slot, uint id, bool gain, double expiry)
        {
            var status = new ActorStatus(id, 0, START.AddSeconds(expiry), 0);
            if (gain) source.OnStatusGain(f.Viewer, ref status); else source.OnStatusLose(f.Viewer, ref status);
            f.Set(100, a => a with { Statuses = a.Statuses.Where(s => s.SlotIndex != slot)
                .Concat(gain ? [new ReplayStatus(id, 0, 0, status.ExpireAt) { SlotIndex = slot }] : []).ToArray() });
            f.Sync();
        }
        void Compare(string step)
        {
            f.M.PrepareRobotOutputs(f.NativeViewer);
            Check(source.ForcedMovements(f.Viewer).Select(m => (m.from.ToVec3(), m.to.ToVec3(), m.dir.Rad))
                .SequenceEqual(f.M.RobotMovements(f.NativeViewer)), step + " exact float segments");
            Check(source.NumActiveForcedMarches == f.M.RobotActiveMarches, step + " active count");
            var ai = new AIHints(); source.AddAIHints(0, f.Viewer, default, ai);
            var direction = ai.ForbiddenDirections.SingleOrDefault();
            var native = f.M.RobotDirection(f.NativeViewer);
            Check(ai.ForbiddenDirections.Count == (native == null ? 0 : 1), step + " directions count");
            if (native is { } n)
            {
                Check((direction.Item1.Rad, direction.Item2.Rad, direction.Item3) == n, step + " raw wide direction");
                var heading = direction.Item3 <= f.Now.AddSeconds(.75) ? direction.Item1.Rad + MathF.PI : (float?)null;
                Check(f.M.Facing(f.NativeViewer.Actor) == heading, step + " old bridge facing not gaze solver");
            }
            var zones = f.M.Navigation(f.NativeViewer).Where(z => z.Component == "OrderForcedMarch").ToArray();
            Check(zones.Length == ai.ForbiddenZones.Count, step + " march navigation");
            for (var i = 0; i < zones.Length; ++i)
            {
                Check(zones[i].Goal && zones[i].Activation == ai.ForbiddenZones[i].Item2, step + " direct inverted fan goal");
                Check(MechanicHintsConvert.TryConvertComplement(ai.ForbiddenZones[i].Item1, 0, out var converted), step + " source actual complement");
                var actual = zones[i].Convert(f.Center, f.M.SampleRadius).ToArray();
                Check(converted.Count == actual.Length, step + " direct goal convert count");
            }
            var hints = new BossComponent.TextHints(); source.AddHints(0, f.Viewer, hints);
            Check(hints.Select(h => (h.Item1, h.Item2)).SequenceEqual(f.M.Hints(f.NativeViewer)
                .Where(h => h.Text == "Aim for safe spot!").Select(h => (h.Text, h.Risky))), step + " destination hints");
        }
        Status(0, 1293, true, 20); Status(59, 1293, true, 10);
        Status(3, 1295, true, 15); Status(4, 1294, true, 25); Status(5, 1296, true, 26);
        Compare("CE32 four directions and sorted duplicate true slots");
        Status(59, 1293, false, 10); Compare("CE32 Lose first direction");
        Status(7, 1257, true, 5); Status(8, 3629, true, 6);
        f.Now = START.AddSeconds(2.7); f.W.Frame.Timestamp = f.Now; f.Sync(); Compare("CE32 active then pending");
        Status(7, 1257, false, 5); Compare("CE32 either Lose clears ForcedEnd");
        f.Now = START.AddSeconds(20); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now); Compare("CE32 overdue no TTL facing window");
        f.W.Actors.Find(100)!.HPMP.MaxHP = 6;
        f.NativeViewer.Actor = f.NativeViewer.Actor with { MaxHp = 6 };
        RobotStart(f, math, 20677, 1);
        Compare("CE32 default randomOdd no pending direction subtraction");
    }

    private static void RobotFeed()
    {
        Initialize();
        using var session = new MechanicSession("CE32-five-real-feed");
        var feed = new MechanicWorldFeed(session);
        var region = new BozjaRegion(920);
        var model = region.Encounters.Single(m => m.IsRobots);
        var center = model.Center;
        ReplayCast Spell(uint action, double at = 0, float rotation = 0)
            => new(action, 100, center, rotation, START.AddSeconds(at), START.AddSeconds(at + 8.3f), false, false)
            { TotalTime = 8, IsSpell = true };
        var actors = new List<ReplayActorState>
        {
            new() { Id = 1, Oid = 0x2E8C, Kind = 2, SubKind = 5, Position = center, Targetable = true, InCombat = true, Hp = 1000, Cast = Spell(20671) },
            new() { Id = 20, Oid = 0x233C, Kind = 2, Position = center, Hp = 1000 },
            new() { Id = 100, Kind = 1, Position = center, Hp = 1, MaxHp = 7, Statuses = [
                new(1293, 0, 0, START.AddSeconds(20)) { SlotIndex = 0 },
                new(1295, 0, 0, START.AddSeconds(15)) { SlotIndex = 59 }] },
            new() { Id = 101, Kind = 1, Position = center + Vector3.UnitX * 15, Hp = 1, MaxHp = 6, Dead = true },
            new() { Id = 102, Oid = 0x233C, Kind = 2, SubKind = 2, Position = center, Hp = 1, MaxHp = 6,
                Statuses = [new(1296, 0, 0, START.AddSeconds(25)) { SlotIndex = 59 }] },
        };
        ReplayPartyMember[] party = [new(0, "", 100), new(1, "", 101)];
        var converter = typeof(ReplayFeedAdapter).GetMethod("ConvertActor", BindingFlags.NonPublic | BindingFlags.Static)!;
        void Frame(double at, uint icon = 0, uint effect = 0, bool error = false)
        {
            var now = START.AddSeconds(at);
            session.BeginFrame(now);
            var snapshots = actors.Select((a, i) =>
            {
                var converted = (MechanicActorSnapshot)converter.Invoke(null, [a, i, now])!;
                return converted with { Statuses = a.Statuses.Select(s => new MechanicStatusDto(s.SlotIndex ?? 0, s.Id, (ushort)s.Param,
                    s.SourceId, (float)(s.ExpiresAt - now).TotalSeconds)).ToArray() };
            }).ToArray();
            feed.ApplyFrame(new(920, 735, 100, 0, party.Select(p => new MechanicPartySlotDto(p.Slot, p.EntityId, p.EntityId, "")).ToArray(), [], snapshots));
            if (icon != 0) feed.Enqueue(new MechanicEventIcon(icon, 100, 1));
            if (effect != 0) feed.Enqueue(new MechanicEventActionEffect(20, 1, effect, 0, 0, default, 0, 0, 0, []));
            feed.Flush(); session.Update();
            region.Synchronize(actors.ToArray(), party, 100, now, true, () =>
            {
                if (icon != 0) region.Icon(1, icon, 100);
                if (effect != 0) region.Effect(20, effect);
            });
            var source = session.BossModules.LoadedModules.Single(m => m.PrimaryActor.OID == 0x2E8C);
            var viewer = session.World.Actors.Find(100)!;
            var slot = session.World.Party.FindSlot(100);
            var native = model.Actors[100];
            var march = source.Components.OfType<StatusDrivenForcedMarch>().Single();
            model.RobotRandomOdd = (float)march.GetType().GetField("randomOdd", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(march)!;
            model.PrepareRobotOutputs(native);
            if (error)
            {
                Exception? hintFailure = null;
                Exception? navigationFailure = null;
                try { source.CalculateHintsForRaidMember(slot, viewer); } catch (ArgumentOutOfRangeException ex) { hintFailure = ex; }
                try { foreach (var component in source.Components) component.AddAIHints(slot, viewer, default, new()); }
                catch (ArgumentOutOfRangeException ex) { navigationFailure = ex; }
                Check(hintFailure?.StackTrace?.Contains("OrderTowers.AddHints") == true
                    && navigationFailure?.StackTrace?.Contains("OrderForcedMarch.AddAIHints") == true
                    && model.RobotHintError != null && model.RobotNavigationError != null, "CE32 real Feed source exception boundary");
                Check(model.Visible(native).Any(a => a.Cast.Rule.Kind == BozjaRuleKind.RobotOrder)
                    && !model.NavigationForHost(native).Any() && !model.Hints(native).Any(), "CE32 real Feed diagnostics preserve old drawing withdraw failing channels");
                return;
            }
            Check(source.Components.Count == 5 && model.Active && model.RobotHintError == null
                && model.RobotNavigationError == null, "CE32 actual five components normal input " + at);
            var expected = source.Components.OfType<GenericAOEs>().SelectMany(c => c.ActiveAOEs(slot, viewer).ToArray()).ToArray();
            var actual = model.Visible(native).ToArray();
            Check(expected.Select(e => (e.Origin.ToVec3(), e.Rotation.Rad, e.Activation, e.Risky, e.Color))
                .SequenceEqual(actual.Select(a => (a.Cast.Origin, a.Cast.Rotation, a.Cast.Activation, a.Risky,
                    a.Danger ? Colors.Danger : a.SafeColor ? Colors.SafeFromAOE : a.Cast.Rule.Kind == BozjaRuleKind.RobotTrain ? Colors.AOE : 0))),
                "CE32 actual Feed ordered raw visible " + at);
            Check(source.CalculateGlobalHints().SequenceEqual(model.GlobalHints())
                && source.CalculateHintsForRaidMember(slot, viewer).Select(h => (h.Item1, h.Item2))
                    .SequenceEqual(model.Hints(native).Select(h => (h.Text, h.Risky))), "CE32 actual Feed hints " + at);
            Check(march.ForcedMovements(viewer).Select(m => (m.from.ToVec3(), m.to.ToVec3(), m.dir.Rad))
                .SequenceEqual(model.RobotMovements(native)), "CE32 actual Feed 60 status slots projection " + at);
            Check(march.ForcedMovements(session.World.Actors.Find(102)!).Select(m => (m.from.ToVec3(), m.to.ToVec3(), m.dir.Rad))
                .SequenceEqual(model.RobotMovements(model.Actors[102])), "CE32 Feed cold Pet status retained hot callbacks excluded " + at);
            Check(model.RobotMovements(model.Actors[102]).Count() == 1, "CE32 exactly one cold Pet move no hot addition or removal");
            var ai = new AIHints();
            foreach (var component in source.Components) component.AddAIHints(slot, viewer, default, ai);
            ai.Normalize();
            var zones = model.NavigationForHost(native);
            Check(ai.ForbiddenZones.Select(z => z.Item2).SequenceEqual(zones.Select(z => z.Activation)), "CE32 Feed normalized clocks " + at);
            for (var i = 0; i < zones.Length; ++i)
            {
                var converted = MechanicHintsConvert.ConvertForbiddenZone(ai.ForbiddenZones[i].Item1, 0, model.Center, model.SampleRadius, out var kind);
                if (kind == MechanicNavZoneKind.ComplementSkipped
                    && MechanicHintsConvert.TryConvertComplement(ai.ForbiddenZones[i].Item1, 0, out var goals))
                {
                    converted = goals;
                    Check(zones[i].Goal, "CE32 Feed recognized complements only");
                }
                var shapes = zones[i].Convert(model.Center, model.SampleRadius).ToArray();
                Check(converted.Count == shapes.Length, "CE32 Feed actual bridge count " + at + "/" + zones[i].Component);
                for (var x = -27f; x <= 27; x += 1.721f)
                    for (var z = -27f; z <= 27; z += 1.927f)
                    {
                        var p = center + new Vector3(x, 0, z);
                        Check(converted.Any(c => c.Template.Resolve(c.Position, c.Rotation, null).Contains(p, 0))
                            == shapes.Any(s => s.Contains(p, 0)), "CE32 Feed converted geometry " + at);
                    }
            }
            Check(ai.PredictedDamage.Select(d => (d.Players.Raw, d.Activation, d.Type.ToString()))
                .SequenceEqual(model.DamageForHost(native).Select(d => (d.Mask, d.Activation, d.Type))), "CE32 Feed damage " + at);
            Check(ai.ForbiddenDirections.Select(d => (d.Item1.Rad, d.Item2.Rad, d.Item3))
                .SequenceEqual(model.RobotDirection(native) is { } direction ? [direction] : []), "CE32 Feed raw directions " + at);
        }
        Frame(0);
        actors[0] = actors[0] with { Cast = null };
        actors[4] = actors[4] with { Statuses = [] };
        Frame(1);
        actors.Add(new() { Id = 30, Oid = 0x1EB032, Kind = 7, Position = center }); Frame(2);
        actors[0] = actors[0] with { Cast = Spell(20672, 3) };
        actors[1] = actors[1] with { Cast = Spell(20674, 3, MathF.PI) }; Frame(3);
        Check(!model.Visible(model.Actors[100]).Any(), "CE32 cold no historical icon does not guess");
        Frame(4, 235); Frame(5, effect: 20673);
        actors[1] = actors[1] with { Cast = Spell(20677, 6) };
        actors[2] = actors[2] with { Statuses = [
            new(1293, 0, 0, START.AddSeconds(20)) { SlotIndex = 0 },
            new(1295, 0, 0, START.AddSeconds(15)) { SlotIndex = 59 }] };
        party = [new(0, "", 101), new(1, "", 100)];
        Frame(6);
        Check(model.RobotNumbers[1]!.SequenceEqual(new uint[] { 0, 1, 2, 4 }),
            "CE32 Actor math callback consumes previous Party before same-frame roster swap");
        actors[0] = actors[0] with { Cast = Spell(20687, 7) }; Frame(7);
        actors[1] = actors[1] with { Cast = Spell(21030, 8) }; Frame(8);
        actors[0] = actors[0] with { Cast = Spell(20681, 9) }; Frame(9);
        actors[0] = actors[0] with { Cast = null }; Frame(10);
        actors[1] = actors[1] with { Cast = Spell(20680, 11) }; Frame(11);
        actors[2] = actors[2] with { Statuses = [] }; Frame(12);
        actors[2] = actors[2] with { MaxHp = 10, Statuses = [new(1293, 0, 0, START.AddSeconds(20)) { SlotIndex = 59 }] };
        actors.Add(new() { Id = 22, Oid = 0x233C, Kind = 2, Position = center, Hp = 1000, Cast = Spell(20678, 13) });
        Frame(13, error: true);
        actors[2] = actors[2] with { MaxHp = 7 };
        actors[^1] = actors[^1] with { Cast = Spell(20679, 14) };
        Frame(14);
    }

    private static void RobotAllianceZero()
    {
        using var f = new Fixture(27);
        f.Party.Clear(); f.Party.Add(new(8, "", 100)); f.Sync();
        f.Activate<GenericAOEs>("OrderTowers");
        var march = f.Activate<StatusDrivenForcedMarch>("OrderForcedMarch");
        f.Viewer.PosRot = new(f.Center + new Vector3(0, 0, 10), 0);
        f.NativeViewer.Actor = f.NativeViewer.Actor with { Position = f.Viewer.Position.ToVec3() };
        var status = new ActorStatus(1257, 0, START.AddTicks(1), 0);
        march.OnStatusGain(f.Viewer, ref status);
        typeof(BozjaEncounter).GetMethod("RobotStatus", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(f.M, [f.NativeViewer, new ReplayStatus(1257, 0, 0, status.ExpireAt), true]);
        Check(march.ForcedMovements(f.Viewer).Single().from == march.ForcedMovements(f.Viewer).Single().to
            && f.M.RobotMovements(f.NativeViewer).Single().From == f.M.RobotMovements(f.NativeViewer).Single().To,
            "CE32 actual float-rounded zero segment");
        march.AddHints(8, f.Viewer, new());
        march.AddAIHints(8, f.Viewer, default, new());
        f.M.PrepareRobotOutputs(f.NativeViewer);
        Check(f.M.RobotNavigationError == null && f.M.RobotHintError == null, "CE32 no fabricated exception withdrawal");
        var pending = new ActorStatus(1293, 0, START.AddSeconds(5), 0);
        march.OnStatusGain(f.Viewer, ref pending);
        typeof(BozjaEncounter).GetMethod("RobotStatus", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(f.M, [f.NativeViewer, new ReplayStatus(1293, 0, 0, pending.ExpireAt), true]);
        Exception? error = null;
        try { march.AddAIHints(8, f.Viewer, default, new()); } catch (IndexOutOfRangeException ex) { error = ex; }
        Check(error != null, "CE32 real alliance pending source index exception");
        f.M.PrepareRobotOutputs(f.NativeViewer);
        Check(f.M.RobotNavigationError != null && f.M.RobotHintError != null, "CE32 actual invalid slot diagnosed");
    }
}
