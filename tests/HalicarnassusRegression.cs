using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Replay;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial.SafeZone;

internal static class HalicarnassusRegression
{
    private static readonly DateTime START = new(2026, 11, 25, 12, 0, 0, DateTimeKind.Utc);
    private static Lumina.GameData? oracleData;
    private sealed class Fixture
    {
        internal readonly DungeonEncounter E = new(693);
        internal readonly List<DungeonActor> Actors =
        [
            new(1, 0x1E14, default, InCombat: true, Hp: 1000),
            new(2, 0x186D, new(2, 0, 3)),
            new(100, 0, default, PartySlot: 0, PartyRole: "WrongRosterRole", ClassJob: 19, Player: true, Hostile: false),
        ];
        internal DateTime Now = START;
        internal HalicarnassusEncounter M => E.Halicarnassus;
        internal Fixture()
        {
            E.CurrentPartyMembers = [new(0, "", 100)];
            Sync();
        }
        internal void Set(ulong id, Func<DungeonActor, DungeonActor> change)
        { var index = Actors.FindIndex(a => a.Id == id); Actors[index] = change(Actors[index]); }
        internal void Sync(ulong viewer = 100, bool update = true)
        {
            E.HalicarnassusInputScan = !update;
            E.Synchronize(Actors, Actors.FirstOrDefault(a => a.Id == viewer)?.Position, Now, playerId: viewer);
            E.HalicarnassusInputScan = false;
        }
        internal void Cast(uint action, ulong source = 2, Vector3? position = null, float rotation = .4f)
        {
            Set(source, a => a with { Cast = new(action, position ?? a.Position, rotation, Now,
                Now.AddSeconds(3.3), TotalTime: 3, IsSpell: true) });
            Sync();
        }
        internal void End(ulong source = 2) { Set(source, a => a with { Cast = null }); Sync(); }
    }

    internal static void Run(Action<string, Action> run)
    {
        run("Halicarnassus native six ordinary exact source parameters watched Finish original clock", Ordinary);
        run("Halicarnassus native two independent absolute chessboards role all classes repeated Starts native Effect", Chess);
        run("Halicarnassus native cold activation spell raw ordering original generation Pending lifecycle", Lifecycle);
        run("Halicarnassus native actual source components per-event ordered exact geometry differential", Differential);
        run("Halicarnassus native exact immediate navigation nonparty missing-view gates and no invented predictions", Output);
    }

    private static void Ordinary()
    {
        foreach (var action in HalicarnassusCatalog.ACTIONS.Take(6))
        {
            var f = new Fixture();
            f.Cast(action);
            var aoe = f.M.Ordinary.Single();
            var sourceClock = new BossMod.ActorCastInfo { TotalTime = 3 }.NPCRemainingTime;
            Check(aoe.Activation == START.AddSeconds(sourceClock) && aoe.Origin == new Vector3(2, 0, 3)
                && aoe.Rotation == .4f, "Locked origin/rotation/float NPC clock.");
            f.Set(2, a => a with { Position = new(17, 0, -10), Rotation = 2 });
            f.Now = f.Now.AddSeconds(50); f.Sync();
            Check(ReferenceEquals(aoe, f.M.Ordinary.Single()), "Movement/timeout changed original entry.");
            f.M.Effect(2, action);
            f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast! with { Action = 999 });
            Check(f.M.Ordinary.Count == 1, "Effect or unrelated Finish consumed ordinary list.");
            f.End();
            Check(f.M.Ordinary.Count == 0, "Watched raw Finish did not consume first ID.");
        }
        var duplicate = new Fixture();
        duplicate.Cast(9313);
        duplicate.M.Start(duplicate.M.Actors[2]);
        duplicate.End();
        Check(duplicate.M.Ordinary.Count == 1, "SimpleAOEs must delete only one duplicate source entry.");
    }

    private static void Chess()
    {
        var f = new Fixture();
        f.Cast(9329, position: new(100, 0, 100)); f.End();
        Check(f.M.Waltz.Count == 12 && f.M.Waltz.All(a => a.Activation == default && a.Rotation == 0
            && a.Origin.X != a.Origin.Z && MathF.Abs(a.Origin.X) <= 15 && MathF.Abs(a.Origin.Z) <= 15),
            "Waltz absolute tiles / Finish lifetime.");
        f.Cast(9329); f.End();
        Check(f.M.Waltz.Count == 24, "Repeated Waltz Start must append.");
        f.Cast(9325); f.End(); f.Cast(9325); f.End();
        Check(f.M.Game.Count == 32, "Repeated Game Start must retain hidden second board.");
        var id = f.M.Game[0].Generation;
        for (uint cls = 0; cls <= 44; ++cls)
        {
            f.Set(100, a => a with { ClassJob = cls }); f.Sync();
            var actual = f.M.Visible().Where(a => a.Action == 9325).ToArray();
            var role = BossMod.ClassRole.GetRole((BossMod.Class)cls);
            var safe = role switch
            {
                BossMod.Role.Tank => new[] { 6 },
                BossMod.Role.Healer => [9],
                BossMod.Role.Melee or BossMod.Role.Ranged => [5, 10],
                _ => [],
            };
            Check(actual.Length == 16 - safe.Length
                && actual.Select(a => a.Generation).SequenceEqual(f.M.Game.Take(16)
                    .Where((_, i) => !safe.Contains(i)).Select(a => a.Generation)),
                $"Dynamic intrinsic role / first16 order at Class {cls}.");
        }
        Check(f.M.Game[0].Generation == id, "Changing role rebuilt board.");
        foreach (var action in new uint[] { 9330, 9331, 8964, 9327 }) f.M.Effect(2, action);
        Check(f.M.Waltz.Count == 24 && f.M.Game.Count == 32, "Unwatched Effect cleared chess.");
        f.M.Effect(100, 9325);
        Check(f.M.Game.Count == 32, "Player Effect consumed NPC chess.");
        f.M.Effect(2, 9325);
        Check(f.M.Game.Count == 0 && f.M.Waltz.Count == 24, "Independent Game clear.");
        f.M.Effect(1, 9329);
        Check(f.M.Waltz.Count == 0, "Cross-source watched Effect must clear full Waltz.");
    }

    private static void Lifecycle()
    {
        var f = new Fixture();
        f.Cast(9329); f.End();
        f.Set(1, a => a with { InCombat = false }); f.Sync();
        Check(f.M.Active && f.E.PreserveStageAcrossBattleEnd && f.E.PreserveFightAcrossPlayerDeath,
            "Default CheckReset=false or script battle/death preservation.");
        f.Set(100, a => a with { Dead = true }); f.Sync();
        Check(f.M.Waltz.Count == 12, "Viewer death discarded global board.");
        f.Sync(0);
        Check(f.M.Waltz.Count == 12 && !f.M.Visible().Any(), "Missing viewer history/display.");
        f.Actors.Add(new(3, 0x1E14, default, InCombat: true, Hp: 1000));
        f.Actors.RemoveAll(a => a.Id == 1);
        f.Sync(update: false); f.Sync(update: false);
        Check(f.M.Primary?.Actor.Id == 1 && f.M.Primary.Destroyed && f.M.Active, "Event scan retired module.");
        f.Sync();
        Check(f.M.Primary == null && !f.M.Active && f.M.Waltz.Count == 0, "Update did not retire old primary.");
        f.Sync();
        Check(f.M.Active && f.M.Primary?.Actor.Id == 3, "Next Update did not promote Pending.");
        f.Set(2, a => a with { Cast = new(9313, default, 0, START, START.AddSeconds(3.3),
            TotalTime: 3, IsSpell: false) }); f.Sync();
        Check(f.M.Ordinary.Count == 0, "NonSpell Start accepted.");
        f.M.Clear();
        f.Set(2, a => a with { Cast = a.Cast! with { IsSpell = true } });
        f.Sync();
        Check(f.M.Active && f.M.Ordinary.Count == 1, "Cold current Spell not replayed.");
        var old = f.M.Actors[2];
        f.Set(2, a => a with { GameObjectId = 50 }); f.Sync();
        f.Set(2, a => a with { GameObjectId = 51, Cast = null }); f.Sync();
        Check(old.Destroyed && !ReferenceEquals(old, f.M.Actors[2]) && f.M.Ordinary.Count == 0,
            "Raw generation Finish before replacing source shell.");
        f.E.Synchronize(f.Actors, default, f.Now, _ => false, 100);
        Check(!f.M.Active && f.E.Arena == null && !f.E.AmbientAoes().Any(), "Disabled mechanic leaked output.");

        var far = new Fixture();
        far.M.Clear();
        far.Set(1, a => a with { InCombat = false });
        far.Set(100, a => a with { Position = new(501, 0, 0) }); far.Sync();
        Check(!far.M.Loaded, "Far candidate loaded before 500 three-dimensional gate.");
        far.Set(100, a => a with { Position = default }); far.Sync();
        Check(far.M.Loaded && !far.M.Active, "Nearby precombat candidate did not load.");
        far.Set(100, a => a with { Position = new(0, 501, 0) }); far.Sync();
        Check(!far.M.Loaded, "Inactive far loaded module did not return to Pending.");
        far.Set(100, a => a with { Position = default }); far.Sync();
        Check(far.M.Loaded, "Returned precombat candidate permanently retired.");

        var unpulled = new Fixture();
        unpulled.M.Clear();
        unpulled.Set(1, a => a with { InCombat = false, Targetable = false, Hp = 0 });
        unpulled.Sync();
        Check(unpulled.M.Loaded && !unpulled.M.Active, "Prepull zero HP retired a loaded module.");
        unpulled.Set(1, a => a with { Hp = 1000, InCombat = true, Targetable = true });
        unpulled.Sync();
        Check(unpulled.M.Active, "Same original actor failed to pull after prepull zero HP.");
        unpulled.Set(1, a => a with { Hp = 0 }); unpulled.Sync();
        Check(!unpulled.M.Loaded, "Active zero HP failed to retire death phase.");

        var hotDead = new Fixture();
        hotDead.M.Clear(); hotDead.Actors.RemoveAll(a => a.Id == 1); hotDead.Sync();
        hotDead.Actors.Add(new(1, 0x1E14, default, Dead: true, Targetable: false, Hp: 1000));
        hotDead.Sync();
        Check(hotDead.M.Loaded && !hotDead.M.Active,
            "ActorAdded immediate loading must not apply Pending's dead filter.");
        hotDead.Set(1, a => a with { Dead = false, Targetable = true, InCombat = true }); hotDead.Sync();
        Check(hotDead.M.Active, "Immediately loaded original dead actor failed revival pull.");

        var wipe = new Fixture();
        wipe.Actors.Add(new(3, 0x1E14, default, InCombat: true, Hp: 1000));
        wipe.Sync();
        wipe.M.Director(0x40000005);
        wipe.Cast(9325);
        Check(wipe.M.Loaded && wipe.M.Primary?.Actor.Id == 3 && !wipe.M.Active && wipe.M.Game.Count == 0,
            "Wipe must allow Pending loading while preventing component activation.");
        wipe.M.Director(0x40000006); wipe.Sync();
        Check(!wipe.M.Active, "Duty recommenced must not replace source 0011 barrier.");
        wipe.M.Director(0x40000011); wipe.Sync();
        Check(wipe.M.Active && wipe.M.Game.Count == 16, "0011 did not release wipe activation barrier/cold cast.");
    }

    private static void Differential()
    {
        if (!MasterPlayer.MechanicRuntime.MechanicRuntimeEnvironment.IsInitialized)
        {
            var path = Environment.GetEnvironmentVariable("FFXIV_SQPACK");
            Check(Directory.Exists(path), "Set FFXIV_SQPACK for real O3 source component oracle.");
            oracleData = new(path!, new Lumina.LuminaOptions { DefaultExcelLanguage = Lumina.Data.Language.ChineseSimplified });
            MasterPlayer.MechanicRuntime.MechanicRuntimeEnvironment.Initialize(oracleData,
                ScriptServiceProxy.Create<Dalamud.Plugin.Services.IPluginLog>((_, _) => null));
        }
        var world = new BossMod.WorldState(1, "halicarnassus-source-oracle");
        var type = typeof(BossMod.Stormblood.Raid.O3NHalicarnassus.O3NHalicarnassus);
        var module = (BossMod.BossModule)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(type);
        typeof(BossMod.BossModule).GetField("WorldState")!.SetValue(module, world);
        var caster = new BossMod.Actor(2, 0x186D, 2, 0, "", 0, BossMod.ActorType.Helper, default, 100, default);
        var viewer = new BossMod.Actor(100, 0, 100, 0, "", 0, BossMod.ActorType.Player, BossMod.Class.PLD, 100, default);
        module.PrimaryActor = caster;
        var f = new Fixture();
        var events = 0;
        var points = 0;
        void Compare(string step, BossMod.Components.GenericAOEs source, IEnumerable<HalicarnassusAoe> native)
        {
            var expected = source.ActiveAOEs(0, viewer).ToArray();
            var actual = native.ToArray();
            Check(expected.Length == actual.Length, $"First mismatch {step}: count {expected.Length}/{actual.Length}.");
            for (var i = 0; i < actual.Length; ++i)
            {
                var e = expected[i]; var a = actual[i];
                Check(e.Origin.X == a.Origin.X && e.Origin.Z == a.Origin.Z
                    && e.Rotation.Rad == a.Rotation && e.Activation == a.Activation && e.Risky
                    && e.Color == 0, $"First mismatch {step}[{i}]: origin/rotation/clock/risk/color.");
                var parameters = (e.Shape, a.Shape) switch
                {
                    (BossMod.AOEShapeCircle s, CircleTemplate n) => s.Radius == n.Radius,
                    (BossMod.AOEShapeDonut s, DonutTemplate n) => s.InnerRadius == n.InnerRadius && s.OuterRadius == n.OuterRadius,
                    (BossMod.AOEShapeCone s, FanTemplate n) => s.Radius == n.Radius && s.HalfAngle.Rad * 2 == n.AngleRadians,
                    (BossMod.AOEShapeRect s, RayRectangleTemplate n) => s.LengthFront == n.Length && s.LengthBack == 0
                        && s.HalfWidth == n.MaxLocalX && -s.HalfWidth == n.MinLocalX,
                    (BossMod.AOEShapeRect s, RectangleTemplate n) => s.LengthFront + s.LengthBack == n.Length
                        && s.HalfWidth * 2 == n.Width && s.LengthBack == s.LengthFront,
                    _ => false,
                };
                Check(parameters, $"First mismatch {step}[{i}]: shape parameters.");
                for (var x = -40f; x <= 40; x += 2.3f)
                    for (var z = -40f; z <= 40; z += 2.7f)
                    {
                        var p = new Vector3(x, 0, z);
                        Check(e.Check(new(x, z)) == DungeonSourceGeometry.Contains(a.Shape, a.Origin, a.Rotation, p),
                            $"First mismatch {step}[{i}]: geometry {p}.");
                        ++points;
                    }
            }
            ++events;
        }
        var names = new[] { "Ribbit", "SpellbladeThunderIII", "SpellbladeBlizzardIII",
            "SpellbladeFireIII", "CrossReaper", "TheQueensWaltz1", "TheQueensWaltz2", "TheGame" };
        for (var c = 0; c < names.Length; ++c)
        {
            var source = (BossMod.Components.GenericAOEs)Activator.CreateInstance(type.Assembly.GetType(type.Namespace + "." + names[c])!,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [module], null)!;
            var action = HalicarnassusCatalog.ACTIONS[c];
            var spell = new BossMod.ActorCastInfo { Action = new(BossMod.ActionType.Spell, action),
                TotalTime = 3, Location = new(2, 0, 3), Rotation = new(.4f) };
            world.Frame.Timestamp = f.Now;
            for (var repeat = 0; repeat < 2; ++repeat)
            {
                f.Cast(action);
                source.OnCastStarted(caster, spell);
                Compare($"{names[c]} Start {repeat}", source, f.M.Visible().Where(a => a.Action == action));
                f.End();
                source.OnCastFinished(caster, spell);
                Compare($"{names[c]} Finish {repeat}", source, f.M.Visible().Where(a => a.Action == action));
            }
            if (action == 9325)
                for (uint cls = 0; cls <= 44; ++cls)
                {
                    viewer.Class = (BossMod.Class)cls;
                    f.Set(100, a => a with { ClassJob = cls }); f.Sync();
                    Compare($"TheGame Class {cls}", source, f.M.Visible().Where(a => a.Action == action));
                }
            source.OnEventCast(caster, new(new(BossMod.ActionType.Spell, action), 0, 0, 0, default, 0, 0, default));
            f.M.Effect(2, action);
            Compare($"{names[c]} Effect", source, f.M.Visible().Where(a => a.Action == action));
        }
        Console.WriteLine($"INFO Halicarnassus actual source differential: {events} event/role boundaries, {points} geometry points.");
    }

    private static void Output()
    {
        var f = new Fixture();
        f.Cast(9325); f.End();
        var outputs = f.E.AmbientAoes().Where(a => a.Id.Contains("/aoe/")).ToArray();
        Check(outputs.Length == 15 && outputs.All(a => a.Navigate && a.Activation == default(DateTime)
            && a.Shape is RectangleTemplate { Width: 10, Length: 10 }), "Immediate Tank board navigation.");
        Check(outputs.All(a => !DungeonSourceGeometry.Contains(a.Shape, a.Position, a.Rotation, new(5, 0, -5))),
            "Tank safe tile changed.");
        Check(!f.E.OutputCasts.Any() && !f.E.PredictedDamage.Any(), "Invented cast/damage output.");
        f.Sync(999);
        Check(f.E.AmbientAoes().All(a => !a.Navigate), "Missing viewer navigation.");
        f.E.CurrentPartyMembers = []; f.Sync();
        Check(f.E.AmbientAoes().Where(a => a.Id.Contains("/aoe/")).Count() == 15
            && f.E.AmbientAoes().All(a => !a.Navigate), "Nonparty viewer display/navigation separation.");
        Check(ArrDungeonCatalog.IsScript("masterplayer.raid.halicarnassus.normal")
            && ArrDungeonCatalog.Create(693).Single().BossOid == 0x1E14,
            "Default enable / precise ownership.");
    }

    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }
}
