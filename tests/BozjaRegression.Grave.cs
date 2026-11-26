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
    private static void RunGrave(Action<string, Action> run)
    {
        run("Bozja native CE42 client fifteen components polygon pull actual action manifest", GraveClient);
        foreach (var rule in BozjaCatalog.Definitions[26].Rules.Where(r => r.Kind == BozjaRuleKind.Aoe))
            run("Bozja native CE42 standard watched Finish safe risk geometry " + rule.Component, () => GraveStandard(rule));
        run("Bozja native CE42 SoulPurge dual FIFO sticky colors query hidden risk no Finish TTL", GravePurge);
        run("Bozja native CE42 deathwall Created raw ring current arena cold actor ordering", GraveArena);
        run("Bozja native CE42 real cold activation deathwall and Gallows swapped actor order", GraveColdArena);
        run("Bozja native CE42 ShockSphere lazy original enemies obstacle dead kept destroyed replaced", GraveSpheres);
        run("Bozja native CE42 forced march duplicate true slots sorted pending zero all safe circles", GraveMarch);
        run("Bozja native CE42 two interrupt hints independent original names callback cached", GraveInterrupts);
        run("Bozja native CE42 dynamic damage original references actual clock party mask", () => Damage(26));
        run("Bozja native CE42 real Feed fifteen components cold slots no chain history and event FIFO", GraveFeed);
    }

    private static void GraveClient()
    {
        using var f = new Fixture(26);
        Check(f.M.IsGrave && f.M.Definition.Rules.Length == 15 && f.M.Definition.Territory == 975, "CE42 actual identity");
        Check(Service.LuminaRow<Lumina.Excel.Sheets.DynamicEvent>(20)?.Name.ToString() == f.M.Definition.Name, "CE42 exact client name");
        foreach (var action in Enum.GetValues<BossMod.Shadowbringers.Foray.CriticalEngagement.CE42FromBeyondTheGrave.AID>())
            Check(data!.GetExcelSheet<Lumina.Excel.Sheets.Action>()!.TryGetRow((uint)action, out _)
                && new ActionID(ActionType.Spell, (uint)action).CastTimeExtra() == 0, "CE42 client action " + action);
        for (var x = -31f; x <= 31; x += .417f)
            for (var z = -31f; z <= 31; z += .389f)
            {
                var p = f.Center + new Vector3(x, 0, z);
                Check(f.Source.Arena.InBounds(new(p.X, p.Z)) == f.M.Arena.Contains(p, 0), "CE42 polygon");
            }
        f.M.Clear();
        f.Set(100, a => a with { Position = f.Center + Vector3.UnitX * 30.001f }); f.Sync();
        Check(!f.M.Active, "CE42 outside pull30");
        f.Set(100, a => a with { Position = f.Center + Vector3.UnitX * 30 }); f.Sync();
        Check(f.M.Active, "CE42 inclusive pull30");
    }

    private static void GraveStandard(BozjaRule rule)
    {
        using var f = new Fixture(26);
        var source = f.Activate<GenericAOEs>(rule.Component);
        var spell = f.Start(source, rule.Actions[0]);
        void Compare(string step) => CompareMetalFoxBits(f, source, rule.Component, "CE42 " + rule.Component + "/" + step,
            rule.Safe ? Colors.SafeFromAOE : 0);
        Compare("Start");
        source.OnCastStarted(f.W.Actors.Find(2)!, spell); f.M.Start(f.M.Actors[2]);
        Compare("duplicate");
        source.OnEventCast(f.W.Actors.Find(2)!, PatriotEvent(spell.Action.ID)); f.M.Effect(2, spell.Action.ID);
        f.Now = START.AddSeconds(100); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
        Compare("no Effect or timeout");
        source.OnCastFinished(f.W.Actors.Find(2)!, spell); f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
        Compare("first ID Finish");
    }

    private static void GravePurge()
    {
        using var f = new Fixture(26);
        var source = f.Activate<GenericAOEs>("SoulPurge");
        var chain = f.Start(source, 24098);
        f.Start(source, 24099);
        CompareMetalFoxBits(f, source, "SoulPurge", "CE42 dual circle first");
        f.Start(source, 24101);
        CompareMetalFoxBits(f, source, "SoulPurge", "CE42 later single donut with old dual");
        f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
        source.OnCastFinished(f.W.Actors.Find(2)!, chain);
        f.Now = START.AddSeconds(100); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
        CompareMetalFoxBits(f, source, "SoulPurge", "CE42 no Finish/TTL");
        foreach (var action in new uint[] { 24102, 24100, 24099, 24101 })
        {
            source.OnEventCast(f.W.Actors.Find(1)!, PatriotEvent(action)); f.M.Effect(1, action);
            CompareMetalFoxBits(f, source, "SoulPurge", "CE42 wrong source FIFO " + action);
        }
        // A single item which has never had a second item remains Color0.
        f.Start(source, 24099);
        CompareMetalFoxBits(f, source, "SoulPurge", "CE42 fresh singleton not always Danger");
    }

    private static void GraveArena()
    {
        foreach (var wallFirst in new[] { false, true })
        {
            using var f = new Fixture(26);
            var source = f.Activate<GenericAOEs>("ArenaChange");
            if (wallFirst)
            {
                f.Add(40, 0x1EB173, f.Center);
                f.Sync();
            }
            f.Start(source, 24096);
            CompareMetalFoxBits(f, source, "ArenaChange", "CE42 ring before wall=" + wallFirst);
            if (!wallFirst)
            {
                f.Add(40, 0x1EB173, f.Center);
                f.Sync();
            }
            CompareMetalFoxBits(f, source, "ArenaChange", "CE42 actual Created clears ring");
            Check(f.M.Center == f.Source.Center.ToVec3(), "CE42 quantized center native=" + f.M.Center + " source=" + f.Source.Center.ToVec3());
            f.Start(source, 24096);
            CompareMetalFoxBits(f, source, "ArenaChange", "CE42 shrunk arena ignores next ring");
        }
    }

    private static void GraveSpheres()
    {
        using var f = new Fixture(26);
        var source = f.Activate<GenericAOEs>("ShockSphere");
        void Compare(string step)
        {
            var expected = source.ActiveAOEs(0, f.Viewer).ToArray();
            var actual = f.M.Visible().Where(a => a.Cast.Rule.Component == "ShockSphere").ToArray();
            Check(expected.Select(a => (a.Origin.ToVec3(), a.Rotation.Rad, a.Activation, a.Risky))
                .SequenceEqual(actual.Select(a => (a.Cast.Origin, a.Cast.Rotation, a.Cast.Activation, a.Risky))), step + " fields");
            var ai = new AIHints(); source.AddAIHints(0, f.Viewer, default, ai);
            var zones = f.M.NavigationForHost(f.NativeViewer).ToArray();
            Check(ai.ForbiddenZones.Count == 0 && zones.All(z => z.Obstacle) && ai.TemporaryObstacles.Count == zones.Length, step + " only obstacles");
            for (var i = 0; i < zones.Length; ++i)
                for (var x = -12f; x < 12; x += .71f)
                {
                    var p = actual[i].Cast.Origin + new Vector3(x, 0, x / 2);
                    Check(ai.TemporaryObstacles[i].Contains(new(p.X, p.Z)) == zones[i].Convert(f.Center, 30).Any(s => s.Contains(p, 0)), step + " obstacle geometry");
                }
        }
        Compare("CE42 empty lazy sphere cache");
        f.Add(20, 0x3232, f.Center + Vector3.UnitX * 3); f.Sync();
        Compare("CE42 sphere Created");
        f.Set(20, a => a with { Dead = true, Targetable = false, EventState = 7 });
        f.W.Actors.Find(20)!.IsDead = true; f.Sync();
        Compare("CE42 dead untargetable state7 sphere retained");
        f.Remove(20); Compare("CE42 destroyed sphere removed");
        f.Add(20, 0x3232, f.Center - Vector3.UnitX * 3); f.Sync();
        Compare("CE42 new generation appended");
        f.Add(25, 0x3232, f.Center + Vector3.UnitZ * 8, ActorType.Player); f.Sync();
        Compare("CE42 Enemies hot cache includes nonNPC OID before component callback filter");
    }

    private static void GraveColdArena()
    {
        foreach (var wallFirst in new[] { false, true })
        {
            using var f = new Fixture(26);
            if (wallFirst) f.Add(40, 0x1EB173, f.Center);
            var shooter = f.Add(21, 0x233C, f.Center + Vector3.UnitX * 10);
            shooter.CastInfo = new() { Action = new(ActionType.Spell, 24096), TotalTime = 5, Location = shooter.Position.ToVec3() };
            f.Set(21, a => a with { Cast = new(24096, 0, a.Position, 0, START, START.AddSeconds(5.3f), false, false)
                { TotalTime = 5, IsSpell = true } });
            if (!wallFirst) f.Add(40, 0x1EB173, f.Center);
            f.M.Clear(); f.Sync();
            var source = f.Activate<GenericAOEs>("ArenaChange");
            CompareMetalFoxBits(f, source, "ArenaChange", "CE42 cold actor order wallFirst=" + wallFirst);
            Check(!f.M.Visible().Any(a => a.Cast.Rule.Kind == BozjaRuleKind.GraveArena)
                && f.M.Center == f.Source.Center.ToVec3(), "CE42 cold deathwall clears or prevents ring with exact single quantization");
        }
    }

    private static void GraveMarch()
    {
        using var f = new Fixture(26);
        var safe = f.Activate<GenericAOEs>("PurifyingLight");
        var source = f.Activate<StatusDrivenForcedMarch>("GallowsMarch");
        void SetSlot(int slot, uint id, bool gain, double expire = 30, ulong owner = 0)
        {
            var value = new ActorStatus(id, 0, START.AddSeconds(expire), owner);
            if (gain) source.OnStatusGain(f.Viewer, ref value); else source.OnStatusLose(f.Viewer, ref value);
            f.Set(100, a => a with { Statuses = a.Statuses.Where(s => s.SlotIndex != slot)
                .Concat(gain ? [new ReplayStatus(id, 0, owner, value.ExpireAt) { SlotIndex = slot }] : []).ToArray() });
            f.Sync();
        }
        void Compare(string step)
        {
            Check(source.ForcedMovements(f.Viewer).Select(m => (m.from.ToVec3(), m.to.ToVec3(), m.dir.Rad))
                .SequenceEqual(f.M.GraveMovements(f.NativeViewer)), step + " exact projected segments");
            var hints = new BossComponent.TextHints(); source.AddHints(0, f.Viewer, hints);
            Check(hints.Select(h => (h.Item1, h.Item2)).SequenceEqual(f.M.Hints(f.NativeViewer)
                .Where(h => h.Text == "Aim for safe spot!").Select(h => (h.Text, h.Risky))), step + " final endpoint hint");
            var ai = new AIHints(); source.AddAIHints(0, f.Viewer, default, ai);
            Check(ai.ForbiddenZones.Count == 0 && !f.M.Navigation(f.NativeViewer).Any(z => z.Component == "GallowsMarch"), step + " no invented facing/navigation");
            Check(source.NumActiveForcedMarches == f.M.GraveActiveMarches, step + " active count");
            var sink = new MechanicDrawSink(new());
            using (MechanicDrawSink.Install(sink)) source.DrawArenaForeground(0, f.Viewer);
            Check(sink.Commands.OfType<MechanicDrawLine>().Select(l => (l.A.ToVec3(), l.B.ToVec3()))
                .SequenceEqual(f.M.GraveMovements(f.NativeViewer).Select(m => (m.From, m.To))), step + " source DrawSink lines");
        }
        SetSlot(0, 2161, true, 20); SetSlot(59, 2161, true, 10);
        SetSlot(7, 2163, true, 15); SetSlot(8, 2162, true, 25); SetSlot(9, 2164, true, 26);
        Compare("CE42 duplicate slots sorted all four directions zero safe circles");
        var last = f.M.GraveMovements(f.NativeViewer).Last().To;
        f.Move(2, last); f.Start(safe, 24115); Compare("CE42 one safe destination");
        f.Add(21, 0x3237, last + Vector3.UnitX * 20); f.Sync();
        f.Start(safe, 24115, 21); Compare("CE42 two disjoint circles require intersection");
        SetSlot(59, 2161, false); Compare("CE42 Lose removes first matching direction");
        SetSlot(3, 1257, true, 5); SetSlot(4, 3629, true, 6); Compare("CE42 active forced segment then pending");
        f.Now = START.AddSeconds(2.7); f.W.Frame.Timestamp = f.Now; f.Sync(); Compare("CE42 remaining float active distance");
        SetSlot(3, 1257, false); Compare("CE42 either forced Lose clears end");
        f.Now = START.AddSeconds(100); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now); Compare("CE42 pending no TTL");
    }

    private static void GraveInterrupts()
    {
        using var f = new Fixture(26);
        var first = f.Activate<CastInterruptHint>("MarchingBreath");
        var second = f.Activate<CastInterruptHint>("SoulSacrifice");
        f.W.Actors.Find(2)!.Name = "First";
        f.Set(2, a => a with { Name = "First" });
        f.Add(20, 0x3233, f.Center);
        f.W.Actors.Find(20)!.Name = "Second"; f.Set(20, a => a with { Name = "Second" }); f.Sync();
        var a = f.Start(first, 24107);
        var b = f.Start(second, 24113, 20);
        void Compare(string step)
        {
            var hints = new BossComponent.GlobalHints(); first.AddGlobalHints(hints); second.AddGlobalHints(hints);
            Check(hints.SequenceEqual(f.M.GlobalHints()), step + " independent component text");
        }
        Compare("CE42 simultaneous named interrupts");
        f.W.Actors.Find(2)!.Name = "Renamed"; f.Set(2, x => x with { Name = "Renamed" }); f.Sync();
        Compare("CE42 no new callback keeps cached name");
        first.OnCastStarted(f.W.Actors.Find(2)!, a); f.M.Start(f.M.Actors[2]); Compare("CE42 first duplicates generic second named");
        first.OnCastFinished(f.W.Actors.Find(2)!, a); f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
        Compare("CE42 first now renamed second unchanged");
        second.OnCastFinished(f.W.Actors.Find(20)!, b); f.M.Finish(f.M.Actors[20], f.M.Actors[20].Cast!);
        Compare("CE42 second Finish leaves first");
    }

    private static void GraveFeed()
    {
        Initialize();
        using var session = new MechanicSession("CE42-fifteen-feed");
        var feed = new MechanicWorldFeed(session);
        var region = new BozjaRegion(975);
        var model = region.Encounters.Single(m => m.IsGrave);
        var center = model.Center;
        ReplayCast Spell(uint action, Vector3 p) => new(action, 100, p, .4f, START, START.AddSeconds(5.3f), false, false)
            { TotalTime = 5, IsSpell = true };
        var actors = new List<ReplayActorState>
        {
            new() { Id = 1, Oid = 0x2E35, Kind = 2, SubKind = 5, Position = center, Targetable = true, InCombat = true, Hp = 1000, Cast = Spell(24099, center) },
            new() { Id = 20, Oid = 0x3237, Kind = 2, Position = center, Cast = Spell(24115, center), Hp = 1000 },
            new() { Id = 100, Kind = 1, Position = center, Hp = 1000, Statuses = [
                new(2161, 0, 0, START.AddSeconds(10)) { SlotIndex = 0 }, new(2161, 0, 0, START.AddSeconds(20)) { SlotIndex = 59 }] },
            new() { Id = 101, Kind = 1, Position = center + Vector3.UnitX * 15, Hp = 1000 },
            new() { Id = 21, Oid = 0x3232, Kind = 2, Position = center + Vector3.UnitX * 5, Hp = 1000 },
        };
        ReplayPartyMember[] party = [new(0, "", 100), new(1, "", 101)];
        var converter = typeof(ReplayFeedAdapter).GetMethod("ConvertActor", BindingFlags.NonPublic | BindingFlags.Static)!;
        void Frame(double at, uint effect = 0)
        {
            var now = START.AddSeconds(at);
            session.BeginFrame(now);
            var snapshots = actors.Select((a, i) =>
            {
                var converted = (MechanicActorSnapshot)converter.Invoke(null, [a, i, now])!;
                return converted with { Statuses = a.Statuses.Select(s => new MechanicStatusDto(s.SlotIndex ?? 0, s.Id, (ushort)s.Param,
                    s.SourceId, (float)(s.ExpiresAt - now).TotalSeconds)).ToArray() };
            }).ToArray();
            feed.ApplyFrame(new(975, 778, 100, 0, party.Select(p => new MechanicPartySlotDto(p.Slot, p.EntityId, p.EntityId, "")).ToArray(), [], snapshots));
            if (effect != 0) feed.Enqueue(new MechanicEventActionEffect(20, 1, effect, 0, 0, default, 0, 0, 0, []));
            feed.Flush(); session.Update();
            region.Synchronize(actors.ToArray(), party, 100, now, true, () => { if (effect != 0) region.Effect(20, effect); });
            var source = session.BossModules.LoadedModules.Single(m => m.PrimaryActor.OID == 0x2E35);
            var viewer = session.World.Actors.Find(100)!; var native = model.Actors[100];
            Check(source.Components.Count == 15 && model.Active, "CE42 true fifteen components");
            var expected = source.Components.OfType<GenericAOEs>().SelectMany(c => c.ActiveAOEs(0, viewer).ToArray()).ToArray();
            var actual = model.Visible(native).ToArray();
            Check(expected.Select(e => (e.Origin.ToVec3(), e.Rotation.Rad, e.Activation, e.Risky, e.Color))
                .SequenceEqual(actual.Select(a => (a.Cast.Origin, a.Cast.Rotation, a.Cast.Activation, a.Risky,
                    a.Danger ? Colors.Danger : a.Cast.Rule.Safe ? Colors.SafeFromAOE : 0))), "CE42 actual Feed visible fields/order " + at);
            Check(source.CalculateGlobalHints().SequenceEqual(model.GlobalHints())
                && source.CalculateHintsForRaidMember(0, viewer).Select(h => (h.Item1, h.Item2))
                    .SequenceEqual(model.Hints(native).Select(h => (h.Text, h.Risky))), "CE42 actual Feed hints " + at);
            var march = source.Components.OfType<StatusDrivenForcedMarch>().Single();
            Check(march.ForcedMovements(viewer).Select(m => (m.from.ToVec3(), m.to.ToVec3(), m.dir.Rad)).SequenceEqual(model.GraveMovements(native)),
                "CE42 actual Feed true status slot movement");
            var ai = new AIHints(); foreach (var c in source.Components) c.AddAIHints(0, viewer, default, ai);
            ai.Normalize();
            Check(ai.ForbiddenZones.Select(z => z.Item2).SequenceEqual(model.NavigationForHost(native).Where(z => !z.Obstacle).Select(z => z.Activation))
                && ai.TemporaryObstacles.Count == model.NavigationForHost(native).Count(z => z.Obstacle), "CE42 actual Feed source navigation channels " + at);
            Check(ai.PredictedDamage.Select(d => (d.Players.Raw, d.Activation, d.Type.ToString()))
                .SequenceEqual(model.DamageForHost(native).Select(d => (d.Mask, d.Activation, d.Type))), "CE42 actual Feed damage " + at);
        }
        Frame(0);
        Check(model.Visible().Count(a => a.Cast.Rule.Kind == BozjaRuleKind.SoulPurge) == 1, "CE42 cold missing ChainMagick does not invent dual");
        actors[0] = actors[0] with { Cast = Spell(24098, center) }; Frame(1);
        actors[0] = actors[0] with { Cast = Spell(24101, center) }; Frame(2);
        Frame(3, 24100);
        actors.Add(new() { Id = 30, Oid = 0x1EB173, Kind = 7, Position = center });
        actors[0] = actors[0] with { Cast = Spell(24096, center) }; Frame(4);
        actors[2] = actors[2] with { Statuses = [] }; Frame(5);
        actors.RemoveAll(a => a.Id == 21); Frame(6, 24102);
        var tick = 7d;
        foreach (var action in new uint[] { 24103, 24104, 24109, 24111, 24112, 24106, 24093, 24094, 24107, 24113 })
        {
            actors[1] = actors[1] with { Cast = Spell(action, center), Name = "Caster" + action };
            Frame(tick++);
            actors[1] = actors[1] with { Cast = null };
            Frame(tick++);
        }
    }
}
