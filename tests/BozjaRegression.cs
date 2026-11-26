using System.Numerics;
using System.Reflection;
using BossMod;
using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.MechanicRuntime;
using MasterPlayer.Replay;
using MasterPlayer.Scripting;
using SourceModule = BossMod.Shadowbringers.Foray.CriticalEngagement.CE14VigilForLost.CE14VigilForLost;

internal static partial class BozjaRegression
{
    private static readonly DateTime START = new(2026, 11, 25, 13, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 CENTER = new(451, 0, 830);
    private static Lumina.GameData? data;

    private sealed class Fixture : IDisposable
    {
        internal readonly WorldState W;
        internal readonly BossModule Source;
        internal readonly BozjaEncounter M;
        internal Vector3 Center => M.Definition.Center;
        internal readonly List<ReplayActorState> Actors = [];
        internal readonly List<ReplayPartyMember> Party = [new(0, "", 100), new(1, "", 101)];
        internal DateTime Now = START;
        internal Actor Viewer => W.Actors.Find(100)!;
        internal BozjaActor NativeViewer => M.Actors[100];

        internal Fixture(int definition = 0, bool helperFirst = false)
        {
            Initialize();
            M = new(BozjaCatalog.Definitions[definition]);
            W = new(1, "bozja-ce14-source-differential");
            W.Frame.Timestamp = START;
            Add(1, M.Definition.PrimaryOid, Center, ActorType.Enemy);
            if (helperFirst) Add(3, 0x233C, Center + new Vector3(3, 0, 0));
            Add(2, 0x233C, Center + new Vector3(10, 0, 0));
            Add(100, 0, Center, ActorType.Player);
            Add(101, 0, Center + new Vector3(-15, 0, 0), ActorType.Player);
            W.Execute(new PartyState.OpModify(0, new(100, 100, false, "Viewer")));
            W.Execute(new PartyState.OpModify(1, new(101, 101, false, "Buddy")));
            Source = definition switch
            {
                0 => new SourceModule(W, W.Actors.Find(1)!),
                1 => new BossMod.Shadowbringers.Foray.CriticalEngagement.CE21FinalFurlong.CE21FinalFurlong(W, W.Actors.Find(1)!),
                2 => new BossMod.Shadowbringers.Foray.CriticalEngagement.CE12BayingOfHounds.CE12BayingOfHounds(W, W.Actors.Find(1)!),
                3 => new BossMod.Shadowbringers.Foray.CriticalEngagement.CE13KillItWithFire.CE13KillItWithFire(W, W.Actors.Find(1)!),
                4 => new BossMod.Shadowbringers.Foray.CriticalEngagement.CE11ShadowOfDeathHand.CE11ShadowOfDeathHand(W, W.Actors.Find(1)!),
                5 => new BossMod.Shadowbringers.Foray.CriticalEngagement.CE24PatriotGames.CE24PatriotGames(W, W.Actors.Find(1)!),
                6 => new BossMod.Shadowbringers.Foray.CriticalEngagement.CE31MetalFoxChaos.CE31MetalFoxChaos(W, W.Actors.Find(1)!),
                7 => new BossMod.Shadowbringers.Foray.CriticalEngagement.CE34TrampledUnderHoof.CE34TrampledUnderHoof(W, W.Actors.Find(1)!),
                8 => new BossMod.Shadowbringers.Foray.CriticalEngagement.CE33WhereStrodeTheBehemoth.CE33WhereStrodeTheBehemoth(W, W.Actors.Find(1)!),
                9 => new BossMod.Shadowbringers.Foray.CriticalEngagement.CE23FiresOfWar.CE23FiresOfWar(W, W.Actors.Find(1)!),
                10 => new BossMod.Shadowbringers.Foray.CriticalEngagement.CE51ThereWouldBeBlood.CE51ThereWouldBeBlood(W, W.Actors.Find(1)!),
                11 => new BossMod.Shadowbringers.Foray.CriticalEngagement.CE63WornToShadow.CE63WornToShadow(W, W.Actors.Find(1)!),
                12 => new BossMod.Shadowbringers.Foray.CriticalEngagement.CE52TimeToBurn.CE52TimeToBurn(W, W.Actors.Find(1)!),
                13 => new BossMod.Shadowbringers.Foray.CriticalEngagement.CE41WithDiremiteAndMain.CE41WithDiremiteAndMain(W, W.Actors.Find(1)!),
                14 => new BossMod.Shadowbringers.Foray.CriticalEngagement.CE44FamiliarFace.CE44FamiliarFace(W, W.Actors.Find(1)!),
                15 => new BossMod.Shadowbringers.Foray.CriticalEngagement.CE62LooksToDieFor.CE62LooksToDieFor(W, W.Actors.Find(1)!),
                16 => new BossMod.Shadowbringers.Foray.CriticalEngagement.CE53HereComesTheCavalry.CE53HereComesTheCavalry(W, W.Actors.Find(1)!),
                17 => new BossMod.Shadowbringers.Foray.CriticalEngagement.CE54NeverCryWolf.CE54NeverCryWolf(W, W.Actors.Find(1)!),
                18 => new BossMod.Shadowbringers.Foray.DelubrumReginae.DRN2Dahu.DRN2Dahu(W, W.Actors.Find(1)!),
                19 => new BossMod.Shadowbringers.Foray.DelubrumReginae.DRN4Phantom.DRN4Phantom(W, W.Actors.Find(1)!),
                20 => new BossMod.Shadowbringers.Foray.DelubrumReginae.DRS5Phantom.DRS5Phantom(W, W.Actors.Find(1)!),
                21 => new BossMod.Shadowbringers.Foray.DelubrumReginae.DRS3Dahu.DRS3Dahu(W, W.Actors.Find(1)!),
                22 => new BossMod.Shadowbringers.Foray.DelubrumReginae.DRN1TrinitySeeker.DRN1TrinitySeeker(W, W.Actors.Find(1)!),
                23 => new BossMod.Shadowbringers.Foray.DelubrumReginae.DRN3QueensGuard.DRN3QueensGuard(W, W.Actors.Find(1)!),
                24 => new BossMod.Shadowbringers.Foray.DelubrumReginae.DRN6Queen.DRN6Queen(W, W.Actors.Find(1)!),
                25 => new BossMod.Shadowbringers.Foray.CriticalEngagement.CE64FeelingTheBurn.CE64FeelingTheBurn(W, W.Actors.Find(1)!),
                26 => new BossMod.Shadowbringers.Foray.CriticalEngagement.CE42FromBeyondTheGrave.CE42FromBeyondTheGrave(W, W.Actors.Find(1)!),
                27 => new BossMod.Shadowbringers.Foray.CriticalEngagement.CE32RiseOfTheRobots.CE32RiseOfTheRobots(W, W.Actors.Find(1)!),
                28 => new BossMod.Shadowbringers.Foray.TheDalriada.DAL1Sartauvoir.DAL1Sartauvoir(W, W.Actors.Find(1)!),
                29 => new BossMod.Shadowbringers.Foray.TheDalriada.DAL04thLegionColossusRubricatus.DAL04thLegionColossusRubricatus(W, W.Actors.Find(1)!),
                30 => new BossMod.Shadowbringers.Foray.TheDalriada.DAL2Cuchulainn.DAL2Cuchulainn(W, W.Actors.Find(1)!),
                31 => new BossMod.Shadowbringers.Foray.TheDalriada.DAL3SaunionDawon.DAL3SaunionDawon(W, W.Actors.Find(1)!),
                32 => new BossMod.Shadowbringers.Foray.TheDalriada.DAL4DiabloArmament.DAL4DiabloArmament(W, W.Actors.Find(1)!),
                33 => new BossMod.Shadowbringers.Foray.TheDalriada.DAL1Gauntlet.DAL1Gauntlet(W, W.Actors.Find(1)!),
                34 => new BossMod.Shadowbringers.Foray.CastrumLacusLitore.CLL2AlbeleoTheMaleficient.CLL2AlbeleoTheMaleficent(W, W.Actors.Find(1)!),
                35 => new BossMod.Shadowbringers.Foray.Skirmish.ConflictingWithTheFirstLaw.ConflictingWithTheFirstLaw(W, W.Actors.Find(1)!),
                36 => new BossMod.Shadowbringers.Foray.Skirmish.HeavyBootsOfLead.HeavyBootsOfLead(W, W.Actors.Find(1)!),
                37 => new BossMod.Shadowbringers.Foray.Skirmish.PyromancerSupreme.PyromancerSupreme(W, W.Actors.Find(1)!),
                _ => throw new ArgumentOutOfRangeException(nameof(definition)),
            };
            Sync();
        }

        internal Actor Add(ulong id, uint oid, Vector3 position, ActorType type = ActorType.Helper)
        {
            W.Execute(new ActorState.OpCreate(id, oid, (int)id, 0, "", 0, type, Class.PLD, 100,
                new(position, 0), .5f, default, true, false, 0, 0, 0));
            Actors.Add(new() { Id = id, Oid = oid, Kind = type == ActorType.Player ? (byte)1 : (byte)2,
                Position = position, Targetable = true, InCombat = id == 1, Hp = 1000 });
            return W.Actors.Find(id)!;
        }

        internal void Set(ulong id, Func<ReplayActorState, ReplayActorState> change)
        {
            var index = Actors.FindIndex(a => a.Id == id);
            Actors[index] = change(Actors[index]);
        }

        internal void Move(ulong id, Vector3 p)
        {
            Set(id, a => a with { Position = p });
            W.Actors.Find(id)!.PosRot = new(p, W.Actors.Find(id)!.Rotation.Rad);
            Sync();
        }

        internal void Remove(ulong id)
        {
            W.Execute(new ActorState.OpDestroy(id));
            Actors.RemoveAll(a => a.Id == id);
            Sync();
        }

        internal void Sync(bool update = true) => M.Synchronize(Actors, Party.ToArray(), 100, Now, update);

        internal T Activate<T>(string name) where T : BossComponent
        {
            var type = Source.GetType().Assembly.GetType(Source.GetType().Namespace + "." + name)!;
            typeof(BossModule).GetMethod("ActivateComponent")!.MakeGenericMethod(type).Invoke(Source, null);
            return (T)Source.Components.Last();
        }

        internal ActorCastInfo Start(BossComponent component, uint action, ulong id = 2, float total = 3, ulong target = 100)
        {
            var actor = W.Actors.Find(id)!;
            var spell = new ActorCastInfo { Action = new(ActionType.Spell, action), TotalTime = total,
                TargetID = target, Location = actor.Position.ToVec3(), Rotation = new(.4f) };
            actor.CastInfo = spell;
            component.OnCastStarted(actor, spell);
            Set(id, a => a with { Cast = new(action, target, a.Position, .4f, Now, Now.AddSeconds(total + .3f), false, false)
                { TotalTime = total, IsSpell = true } });
            Sync();
            return spell;
        }

        public void Dispose() => Source.Dispose();
    }

    internal static void Run(Action<string, Action> run)
    {
        run("Bozja native CE14 actual source standard shape color raw Finish dynamic clock", Standard);
        run("Bozja native CE14 actual source open world towers lazy membership original generations", Membership);
        run("Bozja native CE14 actual source towers ordered hints raw zones conversion damage viewer mask", Towers);
        run("Bozja native CE14 fixed polygon pull cold activation no timeout no combat reset", Lifecycle);
        run("Bozja native CE14 source dynamic NPC damage clocks target fallback duplicate original caster", Damage);
        run("Bozja native CE14 grouped 98 99 activation sorting and first Finish actual source", GroupOrder);
        run("Bozja native CE14 client CFC territory DynamicEvent nine action extra manifest", ClientData);
        run("Bozja native CE14 legacy replay adapter nullable raw cast fields exact clocks cold EventSeen", Legacy);
        RunFurlong(run);
        RunHounds(run);
        RunFire(run);
        RunDeathHand(run);
        RunPatriot(run);
        RunMetalFox(run);
        RunHoof(run);
        RunBehemoth(run);
        RunFiresOfWar(run);
        RunZadnor(run);
        RunShadows(run);
        RunTime(run);
        RunCrystals(run);
        RunHashmal(run);
        RunAida(run);
        RunCavalry(run);
        RunFrost(run);
        RunDahu(run);
        RunPhantom(run);
        RunSavagePhantom(run);
        RunSavageDahu(run);
        RunSeeker(run);
        RunGuard(run);
        RunQueen(run);
        RunBurn(run);
        RunGrave(run);
        RunRobots(run);
        RunDal1(run);
        RunDal0(run);
        RunDal2(run);
        RunDal3(run);
        RunDal4(run);
        RunGauntlet(run);
        RunAlbeleo(run);
        RunSkirmishes(run);
        RunBozjaBlockers(run);
    }

    private static void Initialize()
    {
        if (MasterPlayer.MechanicRuntime.MechanicRuntimeEnvironment.IsInitialized)
        {
            data ??= BossMod.Service.LuminaGameData;
            return;
        }
        var path = Environment.GetEnvironmentVariable("FFXIV_SQPACK");
        Check(Directory.Exists(path), "Set FFXIV_SQPACK for real CE14 source.");
        data = new(path!, new Lumina.LuminaOptions { DefaultExcelLanguage = Lumina.Data.Language.ChineseSimplified });
        MasterPlayer.MechanicRuntime.MechanicRuntimeEnvironment.Initialize(data,
            ScriptServiceProxy.Create<Dalamud.Plugin.Services.IPluginLog>((_, _) => null));
    }

    private static void Standard()
    {
        foreach (var rule in BozjaCatalog.Definitions[0].Rules.Where(r => r.Kind == BozjaRuleKind.Aoe))
        {
            using var f = new Fixture();
            var source = f.Activate<BossMod.Components.GenericAOEs>(rule.Component);
            var spell = f.Start(source, rule.Actions[0]);
            void Compare(string step)
            {
                var expected = source.ActiveAOEs(0, f.Viewer).ToArray();
                var actual = f.M.Visible().Where(a => a.Cast.Rule == rule).ToArray();
                Check(expected.Length == actual.Length, $"{rule.Component}/{step} count.");
                for (var i = 0; i < expected.Length; ++i)
                {
                    var a = actual[i]; var e = expected[i]; var c = a.Cast;
                    Check(e.Origin.X == c.Origin.X && e.Origin.Z == c.Origin.Z && e.Rotation.Rad == c.Rotation
                        && e.Activation == c.Activation && e.Risky == a.Risky && e.Color == (a.Danger ? Colors.Danger : 0),
                        $"{rule.Component}/{step}[{i}] origin/rotation/clock/risk/color.");
                    for (var x = 400f; x < 510; x += 3.7f)
                        for (var z = 780f; z < 885; z += 4.1f)
                            Check(e.Check(new(x, z)) == DungeonSourceGeometry.Contains(rule.Shape!, c.Origin, c.Rotation, new(x, 0, z)),
                                $"{rule.Component}/{step}[{i}] geometry {x},{z}.");
                }
            }
            Compare("Start");
            // Duplicate callbacks are preserved, and color promotion is recomputed.
            for (var i = 0; i < 2; ++i) { source.OnCastStarted(f.W.Actors.Find(2)!, spell); f.M.Start(f.M.Actors[2]); }
            Compare("three duplicates");
            var unrelated = new ActorCastInfo { Action = new(ActionType.Spell, 999), TotalTime = 1 };
            source.OnCastFinished(f.W.Actors.Find(2)!, unrelated);
            f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast! with { ActionId = 999 });
            Compare("unrelated Finish");
            source.OnCastFinished(f.W.Actors.Find(2)!, spell);
            f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
            Compare("watched first Finish");
            f.Move(2, CENTER + new Vector3(24, 0, 20));
            f.Now = START.AddSeconds(100);
            f.Sync(); f.W.Frame.Timestamp = f.Now;
            Compare("locked no TTL");
        }
    }

    private static void Membership()
    {
        using var f = new Fixture();
        var source = f.Activate<BossMod.Components.CastTowersOpenWorld>("Towers");
        f.Start(source, 21266);
        f.Add(102, 0, CENTER + new Vector3(10, 0, 0), ActorType.Player); f.Sync();
        var tower = f.M.Towers.Single();
        Check(tower.AllowedSoakers == null && source.Towers[0].AllowedSoakers == null, "Start eagerly captured soakers.");
        CompareCounts();
        f.Add(103, 0, CENTER + new Vector3(10, 0, 0), ActorType.Player); f.Sync();
        Check(!f.M.Soakers(tower).Contains(f.M.Actors[103]), "Late actor joined old tower.");
        CompareCounts();
        var original = f.M.Actors[102]; var sourceOriginal = f.W.Actors.Find(102)!;
        f.Remove(102);
        f.Add(102, 0, CENTER + new Vector3(-20, 0, 0), ActorType.Player);
        f.Sync();
        Check(original.Destroyed && sourceOriginal.IsDestroyed && !ReferenceEquals(original, f.M.Actors[102])
            && f.M.Soakers(tower).Contains(original) && !f.M.Soakers(tower).Contains(f.M.Actors[102]),
            "Tower replaced original actor generation.");
        CompareCounts();
        f.Move(100, CENTER + new Vector3(10, 0, 0)); CompareCounts();
        f.Add(3, 0x233C, CENTER + new Vector3(20, 0, 0)); f.Sync();
        f.Start(source, 21266, 3);
        var second = f.M.Towers.Last();
        Check(f.M.Soakers(second).Contains(f.M.Actors[103]) && !f.M.Soakers(tower).Contains(f.M.Actors[103]),
            "Tower caches were not independent.");
        f.Remove(2);
        Check(source.Towers.Count == 1 && f.M.Towers.Length == 1, "Caster destruction failed to Finish tower.");
        void CompareCounts()
        {
            Check(source.Towers[0].NumInside(f.Source) == f.M.NumInside(tower), "Original tower inside count differs.");
            Check(source.Towers[0].AllowedSoakers!.Count == f.M.Soakers(tower).Count, "Allowed world count differs.");
        }
    }

    private static void Towers()
    {
        using var f = new Fixture();
        var source = f.Activate<BossMod.Components.CastTowersOpenWorld>("Towers");
        f.Start(source, 21266, total: 9);
        Compare("empty allowed");
        f.Move(100, CENTER + new Vector3(10, 0, 0)); Compare("own correct");
        f.Move(101, CENTER + new Vector3(10, 0, 0)); Compare("overfilled");
        f.Move(100, CENTER); Compare("outside filled");
        f.Add(3, 0x233C, CENTER + new Vector3(-10, 0, 0)); f.Sync();
        f.Start(source, 21266, 3, 1);
        Compare("late empty tower suppresses normal");
        f.Add(104, 0, CENTER + new Vector3(10, 0, 0), ActorType.Player); f.Sync();
        Compare("forbidden viewer correct tower no zone", 104);
        f.Move(104, CENTER); Compare("forbidden viewer outside", 104);
        f.Set(101, a => a with { Dead = true }); f.W.Actors.Find(101)!.IsDead = true; f.Sync();
        Compare("dead counts but not damage");

        void Compare(string step, ulong viewerId = 100)
        {
            var viewer = f.W.Actors.Find(viewerId)!; var native = f.M.Actors[viewerId];
            var hints = new BossComponent.TextHints(); source.AddHints(0, viewer, hints);
            var actualHints = f.M.Hints(native).ToArray();
            Check(hints.Count == actualHints.Length && hints.Select(h => (h.Item1, h.Item2))
                .SequenceEqual(actualHints.Select(h => (h.Text, h.Risky))), $"{step} hints.");
            var sink = new MechanicDrawSink(new());
            MechanicDrawZoneCircle[] background;
            MechanicDrawZoneCircle[] foreground;
            using (MechanicDrawSink.Install(sink))
            {
                source.DrawArenaBackground(0, viewer);
                background = sink.Commands.OfType<MechanicDrawZoneCircle>().ToArray();
                sink.Commands.Clear();
                source.DrawArenaForeground(0, viewer);
                foreground = sink.Commands.OfType<MechanicDrawZoneCircle>().ToArray();
            }
            var views = f.M.TowerViews(native).ToArray();
            var bg = views.Where(t => t.Background).ToArray();
            var fg = views.Where(t => t.Outline).ToArray();
            Check(background.Length == bg.Length && foreground.Length == fg.Length, $"{step} actual Draw layer counts.");
            for (var i = 0; i < bg.Length; ++i)
                Check(background[i].Center.X == bg[i].Cast.Origin.X && background[i].Center.Z == bg[i].Cast.Origin.Z
                    && background[i].Radius == 6 && !background[i].Outline && background[i].Color == Colors.AOE,
                    $"{step} actual background[{i}].");
            for (var i = 0; i < fg.Length; ++i)
                Check(foreground[i].Center.X == fg[i].Cast.Origin.X && foreground[i].Center.Z == fg[i].Cast.Origin.Z
                    && foreground[i].Radius == 6 && foreground[i].Outline && foreground[i].Thickness == 2
                    && foreground[i].Color == (fg[i].Safe ? Colors.Safe : Colors.Danger), $"{step} actual foreground[{i}].");
            var ai = new AIHints(); source.AddAIHints(0, viewer, default, ai);
            var zones = f.M.Navigation(native).ToArray();
            Check(ai.ForbiddenZones.Count == zones.Length, $"{step} raw zone count.");
            for (var i = 0; i < zones.Length; ++i)
            {
                var zone = zones[i]; var expected = ai.ForbiddenZones[i];
                Check(expected.Item2 == zone.Activation, $"{step} first tower clock.");
                for (var x = 420f; x < 485; x += 1.3f)
                    for (var z = 800f; z < 860; z += 1.7f)
                    {
                        var distance = expected.Item1.Distance(new(x, z));
                        var actual = zone.Join != null ? zone.Distance(x, z)
                            : MathF.Sqrt((x - zone.Origin.X) * (x - zone.Origin.X) + (z - zone.Origin.Z) * (z - zone.Origin.Z)) - 6;
                        Check(distance == actual, $"{step} raw SDF first difference {x},{z}.");
                    }
                var converted = MasterPlayer.MechanicHost.MechanicHintsConvert.ConvertForbiddenZone(expected.Item1,
                    0, CENTER, 29.5f, out var kind);
                var result = zone.Convert(CENTER, 29.5f).ToArray();
                Check(converted.Count == result.Length, $"{step} converter {kind} count.");
            }
            var damages = f.M.Damage(native).ToArray();
            Check(ai.PredictedDamage.Count == damages.Length, $"{step} prediction count.");
            for (var i = 0; i < damages.Length; ++i)
                Check(ai.PredictedDamage[i].Players.Raw == damages[i].Mask
                    && ai.PredictedDamage[i].Activation == damages[i].Activation, $"{step} prediction mask/clock.");
        }
    }

    private static void Lifecycle()
    {
        using var f = new Fixture();
        Check(f.M.Active && f.M.Primary?.Actor.Id == 1, "CE14 did not pull.");
        var converted = MasterPlayer.MechanicHost.MechanicHintsConvert.ConvertBounds(f.Source.Arena.Bounds,
            f.Source.Arena.Center, 0)!;
        for (var x = 420.013f; x < 482; x += .173f)
            for (var z = 799.037f; z < 861; z += .197f)
                Check(converted.Contains(new(x, 0, z), 0) == f.M.Definition.Arena.Contains(new(x, 0, z), 0),
                    $"Real fixed polygon boundary differs {x:R},{z:R}.");
        var checkPull = typeof(SourceModule).GetMethod("CheckPull", BindingFlags.NonPublic | BindingFlags.Instance)!;
        f.W.Actors.Find(1)!.InCombat = true;
        foreach (var distance in new[] { 29.8f, 30, 30.01f })
        {
            f.Viewer.PosRot = new(CENTER + new Vector3(distance, 0, 0), 0);
            var pull = (bool)checkPull.Invoke(f.Source, null)!;
            var native = new BozjaEncounter(BozjaCatalog.Definitions[0]);
            native.Synchronize(f.Actors.Select(a => a.Id == 100 ? a with { Position = CENTER + new Vector3(distance, 0, 0) } : a),
                f.Party.ToArray(), 100, START);
            Check(pull == native.Active, $"Real source inclusive pull radius {distance}.");
        }
        f.Set(1, a => a with { InCombat = false, Targetable = false }); f.Sync();
        f.Now = START.AddSeconds(11000); f.Sync();
        Check(f.M.Active, "Invented reset or 10000-second completion.");
        f.Set(1, a => a with { Hp = 0 }); f.Sync();
        Check(!f.M.Active && f.M.Completed, "HP0 did not complete.");
        var m = new BozjaEncounter(BozjaCatalog.Definitions[0]);
        var p = f.Actors.First(a => a.Id == 100) with { Position = CENTER };
        var b1 = f.Actors.First(a => a.Id == 1) with { Hp = 1000, Targetable = true, InCombat = true };
        var b2 = b1 with { Id = 3 };
        m.Synchronize([p], f.Party.ToArray(), 100, START);
        m.Synchronize([], f.Party.ToArray(), 100, START);
        m.Synchronize([p, b1, b2], f.Party.ToArray(), 100, START);
        Check(m.Primary?.Actor.Id == 1, "Recreated old Party[0] did not permit first immediate load.");
        m.Synchronize([p with { Position = CENTER + new Vector3(600, 0, 0) }, b1 with { Hp = 0 }],
            f.Party.ToArray(), 100, START);
        Check(m.Primary == null && !m.Completed, "Distance retirement lost pending before completion.");
        m.Synchronize([p, b1], f.Party.ToArray(), 100, START);
        Check(m.Primary?.Actor.Id == 1 && m.Active, "Same actor could not return from distance pending.");
        m.Director(0x40000005);
        m.Synchronize([p, b1], f.Party.ToArray(), 100, START);
        Check(m.Primary == null && !m.Active, "Director resurrected unloaded primary.");
        m.Director(0x40000011);
        m.Synchronize([p, b1], f.Party.ToArray(), 100, START);
        Check(m.Primary == null && !m.Active, "Director finish invented primary reload.");
        using var cold = new Fixture();
        cold.W.Actors.Find(1)!.InCombat = true;
        cold.Source.Update(); // source boss HP is zero in this fixture
        var initialZero = new BozjaEncounter(BozjaCatalog.Definitions[0]);
        initialZero.Synchronize([p, b1 with { Hp = 0 }], f.Party.ToArray(), 100, START);
        Check(cold.Source.StateMachine.ActivePhaseIndex == 1 && cold.Source.StateMachine.ActiveState == null
            && initialZero.Completed && !initialZero.Active && initialZero.Primary != null,
            "Initially HP0 phase must complete while loaded without inventing a prior active manager tick.");
        cold.W.Actors.Find(1)!.HPMP = new(1000, 1000, 0, 0, 0);
        cold.Source.Update();
        initialZero.Synchronize([p, b1], f.Party.ToArray(), 100, START);
        Check(cold.Source.StateMachine.ActiveState == null && !initialZero.Active, "Completed initial phase restarted after HP restore.");
    }

    private static void Damage() => Damage(0);

    private static void Damage(int definition)
    {
        foreach (var rule in BozjaCatalog.Definitions[definition].Rules
            .Where(r => r.Kind is BozjaRuleKind.Tankbuster or BozjaRuleKind.Raidwide))
        {
            var name = rule.Component;
            var action = rule.Actions[0];
            using var f = new Fixture(definition);
            var source = f.Activate<BossMod.Components.CastHint>(name);
            var spell = f.Start(source, action);
            void Compare(string step)
            {
                var expected = new AIHints(); source.AddAIHints(0, f.Viewer, default, expected);
                var actual = f.M.Damage(f.NativeViewer).ToArray();
                Check(actual.Length == expected.PredictedDamage.Count, $"{name}/{step} count.");
                for (var i = 0; i < actual.Length; ++i)
                    Check(actual[i].Mask == expected.PredictedDamage[i].Players.Raw
                        && actual[i].Activation == expected.PredictedDamage[i].Activation
                        && actual[i].Type == expected.PredictedDamage[i].Type.ToString(), $"{name}/{step}[{i}] mask/clock/type.");
            }
            Compare("initial");
            source.OnCastStarted(f.W.Actors.Find(2)!, spell); f.M.Start(f.M.Actors[2]); Compare("duplicate");
            f.Now = START.AddSeconds(20); f.Sync(); f.W.Frame.Timestamp = f.Now;
            spell.ElapsedTime = 3;
            Compare("NPC capped time current plus .3 float");
            f.Set(101, a => a with { Dead = true }); f.W.Actors.Find(101)!.IsDead = true; f.Sync();
            Compare("dead party mask");
            if (rule.Kind == BozjaRuleKind.Tankbuster)
            {
                spell.TargetID = 2; f.W.Actors.Find(2)!.TargetID = 101;
                f.M.Actors[2].Cast = f.M.Actors[2].Cast! with { TargetId = 2 };
                f.M.Actors[2].Actor = f.M.Actors[2].Actor with { TargetId = 101 };
                Compare("raw self target fallback");
                spell.TargetID = 999; f.M.Actors[2].Cast = f.M.Actors[2].Cast! with { TargetId = 999 };
                Compare("unknown zero mask preserved");
            }
            source.OnCastFinished(f.W.Actors.Find(2)!, spell);
            f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!); Compare("first original reference Finish");
            f.W.Actors.Find(2)!.CastInfo = null; f.M.Actors[2].Cast = null; Compare("missing current cast");
        }
    }

    private static void GroupOrder() => GroupOrder(0);

    private static void GroupOrder(int definition)
    {
        var rule = BozjaCatalog.Definitions[definition].Rules.First(r => r.GroupedFinish);
        foreach (var ties in new[] { false, true })
        {
            using var f = new Fixture(definition);
            var source = f.Activate<BossMod.Components.GenericAOEs>(rule.Component);
            for (var i = 0; i < 100; ++i)
            {
                var id = (ulong)(1000 + i);
                f.Add(id, 0x233C, f.Center + new Vector3(i % 5, 0, i % 7)); f.Sync();
                f.Start(source, rule.Actions[i % rule.Actions.Length], id, ties ? 3 : 100 - i);
                if (i is 97 or 98 or 99) Compare($"{i + 1} ties={ties}");
            }
            var actor = f.W.Actors.Find(1000)!;
            source.OnCastFinished(actor, new ActorCastInfo { Action = new(ActionType.Spell, 999) });
            f.M.Finish(f.M.Actors[1000], f.M.Actors[1000].Cast! with { ActionId = 999 });
            Compare("group unrelated Finish first source");
            void Compare(string step)
            {
                var expected = source.ActiveAOEs(0, f.Viewer).ToArray();
                var actual = f.M.Visible().ToArray();
                Check(expected.Length == actual.Length, step + " count.");
                for (var i = 0; i < actual.Length; ++i)
                    Check(expected[i].Origin.X == actual[i].Cast.Origin.X && expected[i].Origin.Z == actual[i].Cast.Origin.Z
                        && expected[i].Activation == actual[i].Cast.Activation, $"{step}[{i}] order.");
            }
        }
    }

    private static void ClientData()
    {
        Initialize();
        Check(Service.LuminaRow<Lumina.Excel.Sheets.TerritoryType>(920)?.ContentFinderCondition.RowId == 735,
            "Territory->CFC.");
        Check(Service.LuminaRow<Lumina.Excel.Sheets.ContentFinderCondition>(735)?.TerritoryType.RowId == 920,
            "CFC->Territory.");
        Check(Service.LuminaRow<Lumina.Excel.Sheets.DynamicEvent>(3)?.Name.ToString() == BozjaCatalog.Definitions[0].Name,
            "Actual DynamicEvent name.");
        var actions = BozjaCatalog.Definitions[0].Rules.SelectMany(r => r.Actions).Order().ToArray();
        Check(actions.SequenceEqual(new uint[] { 20146, 20148, 20150, 20151, 20152, 20153, 20154, 21266, 21268 }), "Action manifest incomplete.");
        foreach (var action in actions)
        {
            var extra = new ActionID(ActionType.Spell, action).CastTimeExtra();
            Console.WriteLine($"Bozja CE14 actual client action={action} extra={extra:R}");
            Check(extra == 0, $"Client zero-extra manifest changed {action}.");
        }
    }

    private static void Legacy()
    {
        foreach (var duration in new[] { .301, 3.3, 8.375 })
        {
            using var f = new Fixture();
            var raw = new ReplayCast(21268, 100, CENTER, null, START, START.AddSeconds(duration), false, true);
            var actor = f.Actors.First(a => a.Id == 2) with { Rotation = 1.2f, Cast = raw };
            var dto = (MechanicActorSnapshot)typeof(MasterPlayer.MechanicHost.ReplayFeedAdapter)
                .GetMethod("ConvertActor", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [actor, 2, START])!;
            f.Set(2, _ => actor);
            f.Sync();
            var cast = f.M.Casts.Single();
            var expected = new ActorCastInfo { Action = new(ActionType.Spell, dto.Cast!.ActionId),
                TotalTime = dto.Cast.TotalTime, ElapsedTime = dto.Cast.ElapsedTime };
            Check(cast.Rotation == dto.Cast.Rotation && cast.Activation == START.AddSeconds(expected.NPCRemainingTime),
                $"Real ReplayFeedAdapter legacy nullable fields clock {duration}.");
            // EventSeen does not suppress source cold activation.
            var cold = new BozjaEncounter(BozjaCatalog.Definitions[0]);
            cold.Synchronize(f.Actors, f.Party.ToArray(), 100, START);
            Check(cold.Casts.Count == 1 && cold.Casts[0].Activation == cast.Activation, "Cold EventSeen cast was lost.");
        }
    }

    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }
}
