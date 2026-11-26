using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Replay;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial.SafeZone;
using BossMod;

internal static class AlteRoiteRegression
{
    private static readonly DateTime START = new(2026, 11, 25, 12, 0, 0, DateTimeKind.Utc);
    private static Lumina.GameData? data;
    private sealed class Fixture
    {
        internal readonly DungeonEncounter E = new(691);
        internal readonly List<DungeonActor> Actors =
        [
            new(1, 0x1A6F, default, InCombat: true, Hp: 1000, HitboxRadius: 6.2f),
            new(2, 0x233C, new(2, 0, 3)),
            new(100, 0, new(1, 0, 1), Player: true, Hostile: false),
            new(101, 0, new(10, 0, 0), Player: true, Hostile: false),
        ];
        internal DateTime Now = START;
        internal AlteRoiteEncounter M => E.AlteRoite;
        internal Fixture() { E.CurrentPartyMembers = [new(0, "", 100), new(1, "", 101)]; Sync(); }
        internal void Set(ulong id, Func<DungeonActor, DungeonActor> change)
        { var i = Actors.FindIndex(a => a.Id == id); Actors[i] = change(Actors[i]); }
        internal void Sync(ulong viewer = 100, bool update = true)
        {
            E.AlteRoiteInputScan = !update;
            E.Synchronize(Actors, Actors.FirstOrDefault(a => a.Id == viewer)?.Position, Now, playerId: viewer);
            E.AlteRoiteInputScan = false;
        }
        internal void Cast(uint action, ulong source = 2, ulong target = 100, float rotation = .4f)
        {
            Set(source, a => a with { Cast = new(action, a.Position, rotation, Now,
                Now.AddSeconds(3.3), target, TotalTime: 3, IsSpell: true) });
            Sync();
        }
        internal void Finish(ulong source = 2) { Set(source, a => a with { Cast = null }); Sync(); }
    }

    private sealed class Oracle : IDisposable
    {
        internal readonly WorldState W;
        internal readonly BossMod.Stormblood.Raid.O1NAlteRoite.O1NAlteRoite Module;
        internal Actor Boss => W.Actors.Find(1)!;
        internal Actor Viewer => W.Actors.Find(100)!;
        internal Oracle()
        {
            Initialize();
            W = new(1, "alteroite-source-differential");
            W.Frame.Timestamp = START;
            Add(1, 0x1A6F, default, ActorType.Enemy, 6.2f);
            Add(2, 0x233C, new(2, 0, 3), ActorType.Helper);
            Add(100, 0, new(1, 0, 1), ActorType.Player);
            Add(101, 0, new(10, 0, 0), ActorType.Player);
            W.Execute(new PartyState.OpModify(0, new(100, 100, false, "Player")));
            W.Execute(new PartyState.OpModify(1, new(101, 101, false, "Buddy")));
            Module = new(W, Boss);
        }
        internal Actor Add(ulong id, uint oid, Vector3 p, ActorType type = ActorType.Enemy, float hitbox = .5f)
        {
            W.Execute(new ActorState.OpCreate(id, oid, (int)id, 0, "", 0, type, Class.PLD, 100,
                new(p, 0), hitbox, default, true, false, 0, 0, 0));
            return W.Actors.Find(id)!;
        }
        internal T Activate<T>(string name) where T : BossComponent
        {
            var type = typeof(BossMod.Stormblood.Raid.O1NAlteRoite.O1NAlteRoite).Assembly.GetType(
                "BossMod.Stormblood.Raid.O1NAlteRoite." + name)!;
            typeof(BossModule).GetMethod("ActivateComponent")!.MakeGenericMethod(type).Invoke(Module, null);
            return (T)Module.Components.Last();
        }
        public void Dispose() => Module.Dispose();
    }

    internal static void Run(Action<string, Action> run)
    {
        run("AlteRoite native standard components real source shapes independent knockback queue exact raw clocks", Standard);
        run("AlteRoite native fire orbs actual source drivers priority lock visibility movement finish destruction", Orbs);
        run("AlteRoite native tornado actual raw primary cast unquantized center filter no live flags", Tornado);
        run("AlteRoite native icon actual source duplicates counter empty effect death spread removal original references", Icons);
        run("AlteRoite native ThinIce actual source eighteen converter cases status mask exact wall landing no hard nav", Ice);
        run("AlteRoite native cold activation interleaved orb reset real slots immunity strict expiry lifecycle", Cold);
        run("AlteRoite native party navigation actual source union buddy spread and diagnostic damage no tether invention", Output);
        run("AlteRoite native actual source dynamic damage clocks targets dead party and duplicate Finish", Damage);
    }

    private static void Initialize()
    {
        if (MasterPlayer.MechanicRuntime.MechanicRuntimeEnvironment.IsInitialized) return;
        var path = Environment.GetEnvironmentVariable("FFXIV_SQPACK");
        Check(Directory.Exists(path), "Set FFXIV_SQPACK for real O1 source oracle.");
        data = new(path!, new Lumina.LuminaOptions { DefaultExcelLanguage = Lumina.Data.Language.ChineseSimplified });
        MasterPlayer.MechanicRuntime.MechanicRuntimeEnvironment.Initialize(data,
            ScriptServiceProxy.Create<Dalamud.Plugin.Services.IPluginLog>((_, _) => null));
    }

    private static ActorCastInfo Spell(uint action, Actor actor)
        => new() { Action = new(ActionType.Spell, action), TotalTime = 3,
            Location = actor.Position.ToVec3(), Rotation = new(.4f), TargetID = 100 };

    private static void Standard()
    {
        foreach (var (name, action) in new (string, uint)[] { ("ClampAOE", 9186), ("TwinBoltAOE", 9176) })
        {
            using var o = new Oracle();
            var source = o.Activate<BossMod.Components.GenericAOEs>(name);
            var caster = o.W.Actors.Find(2)!;
            var spell = Spell(action, caster);
            var f = new Fixture(); f.Cast(action);
            source.OnCastStarted(caster, spell);
            Compare(source, o.Viewer, f.M.Visible().Where(a => a.Action == action), $"{name} Start");
            source.OnCastStarted(caster, spell); f.M.Start(f.M.Actors[2]);
            source.OnCastFinished(caster, spell); caster.CastInfo = null; f.Finish();
            Compare(source, o.Viewer, f.M.Visible().Where(a => a.Action == action), $"{name} duplicate Finish one");
            Check(f.M.Ordinary.Count(a => a.Action == action) == 1, "Watched Finish removed all duplicates.");
            f.M.Effect(2, action, 100);
            f.Now = f.Now.AddSeconds(100); f.Sync();
            Compare(source, o.Viewer, f.M.Visible().Where(a => a.Action == action), $"{name} no effect/timeout cleanup");
        }
        foreach (var (name, action) in new (string, uint)[] { ("BreathwingKB", 9182), ("ClampKB", 9186), ("DownburstKB", 7896) })
        {
            using var o = new Oracle();
            var source = o.Activate<BossMod.Components.SimpleKnockbacks>(name);
            var caster = o.W.Actors.Find(2)!;
            var spell = Spell(action, caster);
            var f = new Fixture(); f.Cast(action);
            source.OnCastStarted(caster, spell);
            source.OnCastStarted(caster, spell); f.M.Start(f.M.Actors[2]);
            var native = f.M.Knockbacks.Where(a => a.Action == action).ToArray();
            Check(source.Casters.Count == native.Length && native.Length == 2, "Independent KB duplicates.");
            for (var i = 0; i < native.Length; ++i)
                Check(source.Casters[i].Activation == native[i].Activation && source.Casters[i].Origin.X == native[i].Origin.X
                    && source.Casters[i].Origin.Z == native[i].Origin.Z
                    && source.Casters[i].Direction.Rad == native[i].Rotation && source.Distance == 20,
                    "KB exact locked source parameters.");
            var moves = source.CalculateMovements(0, o.Viewer);
            var actual = f.M.Movements(action).ToArray();
            Check(moves.Count == actual.Length, "KB shape/range count differs.");
            for (var i = 0; i < actual.Length; ++i)
                Check(moves[i].to.X == actual[i].To.X && moves[i].to.Z == actual[i].To.Z, "KB endpoint differs.");
            source.OnCastFinished(caster, spell); f.Finish();
            Check(source.Casters.Count == 1 && f.M.Knockbacks.Count(k => k.Action == action) == 1, "KB Finish first source ID.");
            var hints = new AIHints(); source.AddAIHints(0, o.Viewer, default, hints);
            Check(hints.ForbiddenZones.Count == 0, "SimpleKnockbacks source unexpectedly supplies hard navigation.");
        }
    }

    private static void Orbs()
    {
        using var o = new Oracle();
        var source = o.Activate<BossMod.Components.GenericAOEs>("FireOrbsTimedFollowAOE");
        var f = new Fixture();
        var boundaries = 0;
        void At(double t) { f.Now = START.AddSeconds(t); o.W.Frame.Timestamp = f.Now; }
        void CompareOrbs(string step)
        {
            Compare(source, o.Viewer, f.M.Visible().Where(a => a.Action == 9173), step);
            ++boundaries;
        }
        void Add(ulong id, Vector3 p)
        { o.Add(id, 0x1A71, p); f.Actors.Add(new(id, 0x1A71, p)); f.Sync(); CompareOrbs($"Create{id}"); }
        void Driver(uint action, double t)
        {
            At(t); f.Cast(action, 1);
            var spell = Spell(action, o.Boss); o.Boss.CastInfo = spell; source.OnCastStarted(o.Boss, spell);
            CompareOrbs($"Driver{action}@{t}");
        }
        At(0); Add(10, new(.123f, 0, 4.567f));
        At(1.999); Add(11, new(5, 0, 6));
        At(2); Add(12, new(7, 0, 8));
        Check(!f.M.Orbs[11].Outer && f.M.Orbs[12].Outer, "Outer inclusive two-second boundary.");
        Driver(7896, 3); Driver(9182, 5);
        Check(f.M.Orbs[12].Predicted == START.AddSeconds(3).AddSeconds(12.18f),
            "Existing Downburst must take precedence over newer BreathWing.");
        At(11.839); f.Sync(); CompareOrbs("before first warning");
        At(11.84f); f.Sync(); CompareOrbs("inclusive warning");
        At(12); f.Set(10, a => a with { Position = new(9.123f, 0, 7.456f), Dead = true }); f.Sync();
        o.W.Actors.Find(10)!.PosRot = new(9.123f, 0, 7.456f, 0); o.W.Actors.Find(10)!.IsDead = true;
        CompareOrbs("dead moving orb retains original list order");
        f.Cast(9173, 12);
        var orb = o.W.Actors.Find(12)!; var actual = Spell(9173, orb); orb.CastInfo = actual; source.OnCastStarted(orb, actual);
        var locked = f.M.Orbs[12].Actual;
        Driver(7896, 13);
        Check(f.M.Orbs[12].Actual == locked, "Driver rewrote actual locked orb clock.");
        CompareOrbs("actual lock");
        At(50); f.Sync(); CompareOrbs("after prediction no TTL");
        source.OnCastFinished(orb, actual); orb.CastInfo = null; f.Finish(12);
        CompareOrbs("watched Finish");
        source.OnActorDestroyed(o.W.Actors.Find(10)!); f.Actors.RemoveAll(a => a.Id == 10); f.Sync();
        o.W.Execute(new ActorState.OpDestroy(10));
        CompareOrbs("destroy");
        Add(13, new(14, 0, -5));
        CompareOrbs("cached enemy order after dictionary hole reuse");
        Console.WriteLine($"INFO AlteRoite real FireOrbs component: {boundaries} event boundaries.");
    }

    private static void Tornado()
    {
        using var o = new Oracle();
        var source = o.Activate<BossMod.Components.GenericAOEs>("DownburstTornado");
        var f = new Fixture();
        for (var i = 0; i < 4; ++i)
        {
            var position = new Vector3(i == 3 ? 2.0001f : .123f + i * .7f, 0, i == 3 ? 0 : .45f);
            var a = o.Add((ulong)(10 + i), 0x18D6, position);
            a.IsDead = i == 0; a.IsTargetable = false;
            f.Actors.Add(new((ulong)(10 + i), 0x18D6, position, Dead: i == 0, Targetable: false));
        }
        f.Sync();
        Compare(source, o.Viewer, f.M.Visible().Where(a => a.Action == 7896), "Tornado first query before raw Downburst");
        o.W.Execute(new ActorState.OpDestroy(10)); f.Actors.RemoveAll(a => a.Id == 10); f.Sync();
        o.Add(20, 0x18D6, new(.123f, 0, .45f)); f.Actors.Add(new(20, 0x18D6, new(.123f, 0, .45f))); f.Sync();
        f.Cast(7896, 1); o.Boss.CastInfo = Spell(7896, o.Boss);
        Compare(source, o.Viewer, f.M.Visible().Where(a => a.Action == 7896), "Tornado raw center/filter");
        Check(f.M.Visible().Count(a => a.Action == 7896) == 3, "Tornado flags or unquantized center boundary.");
        f.M.Effect(2, 7897, 0);
        Check(f.M.Visible().Count(a => a.Action == 7896) == 3, "Unwatched Effect cleared tornado.");
        o.W.Execute(new ActorState.OpDestroy(11)); f.Actors.RemoveAll(a => a.Id == 11); f.Sync();
        o.Add(21, 0x18D6, new(1.9f, 0, .2f)); f.Actors.Add(new(21, 0x18D6, new(1.9f, 0, .2f))); f.Sync();
        Compare(source, o.Viewer, f.M.Visible().Where(a => a.Action == 7896), "Tornado cached enemy order after hole reuse");
        f.Finish(1); o.Boss.CastInfo = null;
        Compare(source, o.Viewer, f.M.Visible().Where(a => a.Action == 7896), "Tornado raw Finish");
    }

    private static void Icons()
    {
        using var o = new Oracle();
        var source = o.Activate<BossMod.Components.IconStackSpread>("BlazeLevinStackSpread");
        var f = new Fixture();
        void CompareIcons(string step)
        {
            Check(source.Stacks.Count == f.M.Stacks.Count && source.Spreads.Count == f.M.Spreads.Count
                && source.CastCounter == f.M.StackCounter, $"Icon first mismatch {step}: count/counter.");
            for (var i = 0; i < source.Stacks.Count; ++i)
                Check(source.Stacks[i].Target.InstanceID == f.M.Stacks[i].Target.Actor.Id
                    && source.Stacks[i].Activation == f.M.Stacks[i].Activation
                    && source.Stacks[i].MinSize == 2 && source.Stacks[i].MaxSize == int.MaxValue, "Stack contract.");
            for (var i = 0; i < source.Spreads.Count; ++i)
                Check(source.Spreads[i].Target.InstanceID == f.M.Spreads[i].Target.Actor.Id
                    && source.Spreads[i].Activation == f.M.Spreads[i].Activation, "Spread contract.");
        }
        void Icon(ulong id, uint icon)
        { source.OnEventIcon(o.W.Actors.Find(id)!, icon, 999); f.M.Icon(id, icon); CompareIcons($"Icon{icon}"); }
        void Effect(uint action, ulong target)
        {
            source.OnEventCast(o.Boss, new(new(ActionType.Spell, action), target, 0, 0, default, 0, 0, default));
            f.M.Effect(1, action, target); CompareIcons($"Effect{action}");
        }
        Icon(100, 62); Icon(100, 62); Icon(101, 108); Icon(101, 108);
        Effect(9178, 101); Effect(9177, 555); Effect(9185, 555);
        Effect(9185, 100); Effect(9185, 100);
        Check(source.CastCounter == 1 && source.Stacks.Count == 0, "Empty Effect counter precondition.");
        Icon(100, 62); Effect(9185, 100);
        Check(f.M.Stacks.Count == 1 && f.M.StackCounter == 2, "Empty Effect must not reset counter then consume future stack.");
        o.W.Actors.Find(101)!.IsDead = true; f.Set(101, a => a with { Dead = true });
        source.Update(); f.Sync(); CompareIcons("death Update permanently removes spread");
        o.W.Actors.Find(101)!.IsDead = false; f.Set(101, a => a with { Dead = false }); f.Sync();
        CompareIcons("revival does not restore removed spread");
        var old = f.M.Stacks.Single().Target;
        f.Set(100, a => a with { Dead = true }); f.Sync();
        Check(f.M.Stacks.Count == 1 && !f.M.Damage().Any(), "Dead stack hidden/retained without invented Shared damage.");
        f.Set(100, a => a with { Dead = false }); f.Sync();
        f.Set(100, a => a with { GameObjectId = 5 }); f.Sync();
        f.Set(100, a => a with { GameObjectId = 6, Position = new(20, 0, 20) }); f.Sync();
        Check(old.Destroyed && ReferenceEquals(f.M.Stacks.Single().Target, old)
            && old.Actor.Position != f.M.Actors[100].Actor.Position, "Marker reacquired a rebuilt same-ID target.");
    }

    private static void Ice()
    {
        using var o = new Oracle();
        var source = o.Activate<BossMod.Components.ThinIce>("ThinIce");
        var f = new Fixture();
        var status = new ActorStatus(911, 0x96, START.AddSeconds(30), 1);
        var wrong = new ActorStatus(912, 0x96, START.AddSeconds(30), 1);
        source.OnStatusGain(o.Viewer, ref wrong);
        var hints = new AIHints(); o.Module.CalculateAIHints(0, o.Viewer, default, hints);
        Check(hints.ForbiddenZones.Count == 0, "Wrong source SID activated.");
        source.OnStatusGain(o.Viewer, ref status);
        f.Set(100, a => a with { StatusSlots = [new(911, 0x96, 1, status.ExpireAt) { SlotIndex = 59 }] }); f.Sync();
        var positions = new Vector2[] { default, new(.123f, .371f), new(10, 0), new(19, 0), new(-19, 0),
            new(13.4f, 13.4f), new(0, 20), new(20.1f, 0), new(30, 0) };
        var cases = 0;
        foreach (var legacy in new[] { false, true })
            foreach (var p in positions)
            {
                o.W.Client.LegacyMovement = legacy; o.W.Client.CameraAzimuth = new(.25f);
                o.Viewer.PosRot = new(p.X, 0, p.Y, .75f);
                f.Set(100, a => a with { Position = new(p.X, 0, p.Y), Rotation = .75f }); f.Sync();
                hints = new(); o.Module.CalculateAIHints(0, o.Viewer, default, hints);
                Check(hints.ForbiddenZones.Count == 1, "Expected actual ThinIce source FZ.");
                var zone = hints.ForbiddenZones[0];
                var converted = MasterPlayer.MechanicHost.MechanicHintsConvert.ConvertForbiddenZone(zone.Item1, 0,
                    new(hints.PathfindMapCenter.X, 0, hints.PathfindMapCenter.Z), hints.PathfindMapBounds.Radius, out var kind);
                Check(converted.Count == 0 && kind.ToString() == "Failed" && zone.Item2 == DateTime.MaxValue,
                    "Actual ThinIce converter consumption changed; revisit navigation adaptation.");
                var movements = source.CalculateMovements(0, o.Viewer);
                var landing = f.M.IceLanding(f.M.Actors[100], legacy, .25f);
                Check(movements.Count == 1 && movements[0].to.X == landing.X && movements[0].to.Z == landing.Z
                    && !source.DestinationUnsafe(0, o.Viewer, movements[0].to), "Exact ThinIce source stop-wall endpoint.");
                Check(f.E.AmbientAoes().All(a => !a.Navigate), "ThinIce added hard navigation absent from bridge.");
                Console.WriteLine($"INFO AlteRoite ThinIce source→Convert legacy={legacy} pos={p} FZ=1 Convert={converted.Count} kind={kind}");
                ++cases;
            }
        source.OnStatusLose(o.Viewer, ref status);
        f.Set(100, a => a with { StatusSlots = [] }); f.Sync();
        hints = new(); o.Module.CalculateAIHints(0, o.Viewer, default, hints);
        Check(hints.ForbiddenZones.Count == 0 && f.M.IceMask == 0 && cases == 18, "ThinIce source loss/real slot mask.");
        Console.WriteLine("INFO AlteRoite ThinIce 18 actual isolated source→Convert cases; full Host/SafeZone/solver not executed.");
    }

    private static void Cold()
    {
        using var oracle = new Oracle();
        oracle.Add(10, 0x1A71, default);
        oracle.Boss.CastInfo = Spell(7896, oracle.Boss);
        var coldOrbs = oracle.Activate<BossMod.Components.GenericAOEs>("FireOrbsTimedFollowAOE");
        var f = new Fixture();
        f.M.Clear(); f.Set(1, a => a with { InCombat = false });
        f.Actors.Add(new(10, 0x1A71, default));
        f.Cast(7896, 1); f.Set(1, a => a with { InCombat = true }); f.Sync();
        Check(f.M.Orbs[10].Predicted == START.AddSeconds(16.84f),
            "Cold component Created→Cast order must let first orb clear prior boss driver.");
        f.Now = START.AddSeconds(2); oracle.W.Frame.Timestamp = f.Now;
        oracle.Add(11, 0x1A71, new(3, 0, 5)); f.Actors.Add(new(11, 0x1A71, new(3, 0, 5))); f.Sync();
        Check(f.M.Orbs[11].Outer && f.M.Orbs[11].Predicted == f.Now.AddSeconds(16.84f),
            "Cold first orb must clear driver before later outer orb prediction.");
        f.Now = START.AddSeconds(14); f.Sync(); oracle.W.Frame.Timestamp = f.Now;
        Compare(coldOrbs, oracle.Viewer, f.M.Visible().Where(a => a.Action == 9173), "actual cold Created→Cast later outer orb reset");
        f.Now = START;
        f.M.Clear(); f.Set(1, a => a with { InCombat = false });
        f.Set(100, a => a with { StatusSlots = [new(911, 0, 1, START.AddSeconds(30)) { SlotIndex = 59 },
            new(160, 0, 1, START.AddSeconds(3.3f)) { SlotIndex = 58 }] });
        f.Sync(); f.Set(1, a => a with { InCombat = true }); f.Sync();
        Check(f.M.IceMask == 1, "Cold real sixty status slots.");
        var hit = START.AddSeconds(new ActorCastInfo { TotalTime = 3 }.NPCRemainingTime);
        f.Set(100, a => a with { StatusSlots = [new(160, 0, 1, hit) { SlotIndex = 58 }] }); f.Sync();
        Check(!f.M.Immune(hit), "Immunity equality must be strict >.");
        f.Set(100, a => a with { StatusSlots = [new(160, 1, 1, hit.AddTicks(1)) { SlotIndex = 58 }] }); f.Sync();
        Check(f.M.Immune(hit), "Immunity strict one tick future.");
        f.Set(1, a => a with { InCombat = false }); f.Sync();
        Check(f.M.Active && f.E.PreserveStageAcrossBattleEnd, "Default no combat reset.");
        f.Actors.Add(new(3, 0x1A6F, default, InCombat: true, Hp: 1000)); f.Sync();
        f.M.Director(0x40000005); f.Sync();
        Check(f.M.Loaded && !f.M.Active, "Wipe must preserve Pending and block activation.");
        f.M.Director(0x40000011); f.Sync();
        Check(f.M.Active && f.M.Primary?.Actor.Id == 3, "Pending resumed before/after wrong barrier.");
        f.E.Synchronize(f.Actors, default, f.Now, _ => false, 100);
        Check(!f.M.Loaded && !f.E.AmbientAoes().Any(), "Mechanism off output cleanup.");
    }

    private static void Output()
    {
        using var o = new Oracle();
        var source = o.Activate<BossMod.Components.IconStackSpread>("BlazeLevinStackSpread");
        var f = new Fixture();
        source.OnEventIcon(o.Viewer, 62, 101); f.M.Icon(100, 62);
        var hints = new AIHints(); source.AddAIHints(0, o.Viewer, default, hints);
        var navigation = f.E.AmbientAoes().Where(a => a.Navigate).ToArray();
        Check(hints.ForbiddenZones.Count == 1 && navigation.Length == 1, "Stack target buddy FZ count.");
        var converted = MasterPlayer.MechanicHost.MechanicHintsConvert.ConvertForbiddenZone(hints.ForbiddenZones[0].Item1,
            0, new(0, 0, 0), 20, out var conversion);
        Check(converted.Count == 0 && conversion.ToString() == "Failed",
            "Actual source own-stack buddy conversion changed; revisit native sampling boundary.");
        Console.WriteLine("INFO AlteRoite own stack actual source→Convert FZ=1 Convert=0 kind=Failed; native finite sampler must agree.");
        for (var x = -19f; x < 20; x += 1.7f)
            for (var z = -19f; z < 20; z += 1.9f)
            {
                var p = new Vector3(x, 0, z);
                if (x * x + z * z >= 400) continue;
                Check((hints.ForbiddenZones[0].Item1.Distance(new(x, z)) <= 0)
                    == DungeonSourceGeometry.Contains(navigation[0].Shape, navigation[0].Position, navigation[0].Rotation, p),
                    "Real source stack buddy R3 forbidden complement geometry.");
            }
        Check(!f.M.Damage().Any(), "Invented Shared prediction for default ForbiddenPlayers.");
        source.OnEventIcon(o.W.Actors.Find(101)!, 108, 999); f.M.Icon(101, 108);
        Check(f.E.AmbientAoes().All(a => !a.Id.EndsWith("/buddy")), "Spread buddy remained stack eligible.");
        f.Cast(9175, 1, 1); f.Set(1, a => a with { TargetId = 101 }); f.Sync();
        Check(f.M.Damage().Single(p => p.DamageType == "Tankbuster").TargetId == 101,
            "Self-target tankbuster must use dynamic caster target, not inferred tether.");
        f.Set(1, a => a with { TargetId = 100 }); f.Sync();
        Check(f.M.Damage().Single(p => p.DamageType == "Tankbuster").TargetId == 100, "Tank target not dynamic.");
        f.Sync(999);
        Check(!f.E.AmbientAoes().Any(a => a.Navigate), "Missing viewer navigation.");
        f.E.CurrentPartyMembers = []; f.Sync();
        Check(!f.M.Damage().Any() && !f.E.AmbientAoes().Any(a => a.Navigate), "Nonparty navigation/damage gate.");
    }

    private static void Damage()
    {
        foreach (var (name, action) in new (string, uint)[] { ("Roar", 9180), ("Charybdis", 9179), ("TwinBoltTetheredBuster", 9175) })
        {
            using var o = new Oracle();
            var source = o.Activate<BossMod.Components.CastHint>(name);
            var f = new Fixture();
            var caster = o.W.Actors.Find(2)!;
            f.Cast(action, target: 2);
            var spell = Spell(action, caster);
            spell.TargetID = 2;
            caster.CastInfo = spell;
            caster.TargetID = 101;
            f.Set(2, a => a with { TargetId = 101 }); f.Sync();
            source.OnCastStarted(caster, spell);
            void CompareDamage(string step)
            {
                var hints = new AIHints();
                source.AddAIHints(0, o.Viewer, default, hints);
                var expected = hints.PredictedDamage.SelectMany(p => new[] { (Slot: 0, Id: 100ul), (Slot: 1, Id: 101ul) }
                    .Where(a => p.Players[a.Slot]).Select(a => (a.Id, a.Slot, p.Activation, Type: p.Type.ToString()))).ToArray();
                var actual = f.M.Damage().Select(p => (p.TargetId, p.PartySlot, p.Activation, p.DamageType)).ToArray();
                Check(expected.SequenceEqual(actual), $"First difference {name}/{step}: ordered targets/clock/type.");
            }
            CompareDamage("Start");
            source.OnCastStarted(caster, spell); f.M.Start(f.M.Actors[2]);
            CompareDamage("duplicate Start");
            caster.TargetID = 100; f.Set(2, a => a with { TargetId = 100 }); f.Sync();
            CompareDamage("dynamic self target fallback");
            o.W.Actors.Find(101)!.IsDead = true; f.Set(101, a => a with { Dead = true }); f.Sync();
            CompareDamage("dead party mask");
            spell.ElapsedTime = 3;
            f.Now = START.AddSeconds(20); f.Sync(); o.W.Frame.Timestamp = f.Now;
            CompareDamage("capped NPC float remainder");
            source.OnCastFinished(caster, spell); caster.CastInfo = null; f.Finish();
            CompareDamage("raw Finish consumes first duplicate");
        }
        Console.WriteLine("INFO AlteRoite actual damage components: 18 ordered target/clock/type boundaries.");
    }

    private static void Compare(BossMod.Components.GenericAOEs source, Actor viewer,
        IEnumerable<AlteRoiteMechanic> native, string step)
    {
        var expected = source.ActiveAOEs(0, viewer).ToArray();
        var actual = native.ToArray();
        Check(expected.Length == actual.Length, $"First difference {step}: count {expected.Length}/{actual.Length}.");
        for (var i = 0; i < actual.Length; ++i)
        {
            var e = expected[i]; var a = actual[i];
            Check(e.Origin.X == a.Origin.X && e.Origin.Z == a.Origin.Z && e.Rotation.Rad == a.Rotation
                && e.Activation == a.Activation && e.Risky && e.Color == (a.Action == 7896 ? Colors.Danger : 0),
                $"First difference {step}[{i}] origin/rotation/clock/risk/color.");
            for (var x = -30f; x <= 30; x += 1.7f)
                for (var z = -30f; z <= 30; z += 2.1f)
                    Check(e.Check(new(x, z)) == DungeonSourceGeometry.Contains(a.Shape!, a.Origin, a.Rotation, new(x, 0, z)),
                        $"First difference {step}[{i}] geometry {x}/{z}.");
        }
    }

    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
