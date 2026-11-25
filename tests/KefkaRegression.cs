using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Replay;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial.SafeZone;

internal static class KefkaRegression
{
    private static readonly DateTime START = new(2026, 11, 25, 8, 0, 0, DateTimeKind.Utc);
    private sealed class Fixture
    {
        internal readonly DungeonEncounter E = new(751);
        internal readonly List<DungeonActor> Actors =
        [
            new(1, 0x2153, default, InCombat: true, Hp: 1000, MaxHp: 1000),
            new(2, 0x18D6, new(0, 0, 10)),
            new(3, 0x18D6, new(10, 0, 0)),
        ];
        internal DateTime Now = START;
        internal KefkaEncounter K => E.Kefka;
        internal Fixture()
        {
            for (var i = 0; i < 8; ++i)
                Actors.Add(new((ulong)(100 + i), 0, new(i * .1f, 0, 0), PartySlot: i, Player: true, Hostile: false));
            E.CurrentPartyMembers = Enumerable.Range(0, 8).Select(i => new ReplayPartyMember(i, "", (ulong)(100 + i))).ToArray();
            Sync();
        }
        internal void Set(ulong id, Func<DungeonActor, DungeonActor> change)
        { var index = Actors.FindIndex(a => a.Id == id); Actors[index] = change(Actors[index]); }
        internal void Sync(ulong viewer = 100) => E.Synchronize(Actors, Actors.FirstOrDefault(a => a.Id == viewer)?.Position, Now, playerId: viewer);
        internal void Step(double seconds) { Now = Now.AddSeconds(seconds); Sync(); }
        internal void Cast(uint action, ulong source = 2, ulong target = 0, float rotation = 0, double duration = 3)
        {
            Set(source, a => a with { Cast = new(action, a.Position, rotation, Now, Now.AddSeconds(duration + .3), target, TotalTime: (float)duration) });
            Sync();
        }
        internal void End(ulong source = 2) { Set(source, a => a with { Cast = null }); Sync(); }
    }

    internal static void Run(Action<string, Action> run)
    {
        run("Kefka native real catalog identity fake actions do not acquire danger", Catalog);
        run("Kefka native shared AOE groups raw lock real Finish no timeout packet Cancel isolation", Ordinary);
        run("Kefka native RevoltingRuin axis1 predicate F plus3.1 persistent until Aero Start or complete effect", Ruin);
        run("Kefka native radial10 forward15 full movement model sequential display first-only navigation", Knockback);
        run("Kefka native immune categories overwrite loss strict expiry old Party and slot reassignment", Immunity);
        run("Kefka native positive reverse gaze source cone predicate joint facing narrow intervals and no timeout", Gaze);
        run("Kefka native exact8 stack spread radius5 original targets death and rebuilt identity", Party);
        run("Kefka native shared output navigates actual landing constraint independent previews owner release", Output);
        run("Kefka native raw field replacement accumulated float late clock missing perspective and real lifecycle", Lifecycle);
        run("Kefka native scalar landing oracle grid origin zero and exact R19 boundary", LandingOracle);
        run("Kefka native target ID FIFO missing target Finish and unknown Finish group broadcast", FinishConsumption);
        run("Kefka native missing Actor single target self fallback dynamic float prediction clock", Predictions);
        run("Kefka native independent spread clocks R5 outlines R6 navigation solo gate and revival", SpreadClocks);
        run("Kefka native source text donut inner edge cone apex raw player quantized stack and rebuilt overlap", TextEdges);
        run("Kefka native cold Pet status replay hot created gain loss and destroyed status filtering", PetStatuses);
    }

    private static void Catalog()
    {
        var f = new Fixture();
        Check(ArrDungeonCatalog.Owns(0x2153, 751) && !ArrDungeonCatalog.Owns(0x2153, 289)
            && f.E.Definitions[0].Casts.Length == 18, "Source identity/CFC mismatch.");
        foreach (var action in new uint[] { 10515, 10517, 10523 }) { f.Cast(action); f.End(); }
        Check(f.K.Mechanics.Count == 0 && !f.E.AmbientAoes().Any(a => a.Navigate), "Fake enum-only action gained navigation.");
    }

    private static void Ordinary()
    {
        var f = new Fixture();
        f.Cast(10518);
        var original = f.K.Mechanics.Single();
        f.Set(2, a => a with { Position = new(5, 0, 12), Rotation = 1 });
        f.Step(10);
        Check(f.K.Mechanics.Single() == original && original.Shape is CircleTemplate { Radius: 10 }, "Expired or followed a locked cast.");
        var adapter = new DungeonEventAdapter(f.E, () => f.Sync(), _ => 0, _ => { });
        adapter.Handle(new PromeRotation.LogSystem.LogSystemCancelCastEvent { SourceId = 2, ActionId = 10518 }, f.Now);
        Check(f.K.Mechanics.Count == 1, "Unconfirmed cancel consumed raw component.");
        f.Cast(10520, 3);
        f.End();
        Check(f.K.Mechanics.Count == 1 && f.K.Mechanics[0].Source.Actor.Id == 3, "Shared group Finish consumed other source.");
        f.End(3);
        Check(f.K.Mechanics.Count == 0, "True Finish failed to release group.");
    }

    private static void Ruin()
    {
        var f = new Fixture();
        f.Set(2, a => a with { Position = new(1, 0, -1) });
        f.Cast(10530);
        Check(f.K.Ruin == null, "AlmostEqual boundary incorrectly created Ruin.");
        f.End(); f.Set(2, a => a with { Position = new(1.001f, 0, 0) }); f.Cast(10530, rotation: .7f);
        var ruin = f.K.Ruin!;
        Check(ruin != null && ruin.Activation == f.Now.AddSeconds(3.3f).AddSeconds(3.1d)
            && ruin.Shape is FanTemplate { Radius: 102.7f }, "Ruin source clock/shape.");
        f.End(); f.Step(50);
        Check(ReferenceEquals(f.K.Ruin, ruin), "Finish/time cleared persistent Ruin.");
        f.K.Effect(3, 10531);
        Check(f.K.Ruin == null, "Cross-source complete effect failed.");
        f.Cast(10530); f.Cast(10532, 3);
        Check(f.K.Ruin == null, "Aero Start did not clear Ruin.");
    }

    private static void Knockback()
    {
        var f = new Fixture();
        f.Cast(10535, rotation: MathF.PI / 2);
        f.Cast(10535, 3, rotation: 0);
        var movements = f.K.Mechanics.Select(m => m.Displacement!).ToArray();
        Check(movements.Length == 2 && movements.All(m => m.Forward && m.Distance == 15 && !m.StopAtWall && !m.IgnoreImmunes),
            "Original movement fields lost.");
        var aoes = f.E.AmbientAoes().ToArray();
        Check(aoes.Count(a => a.Id.EndsWith("/navigation")) == 1
            && aoes.Any(a => a.Id.Contains($"/{f.K.Mechanics[1].Generation}/")), "First navigation/all sequential display differ.");
        f.End(); Check(f.E.AmbientAoes().Count(a => a.Id.EndsWith("/navigation")) == 1, "Hidden caster failed promotion.");
        f.End(3); f.Cast(10532);
        Check(f.K.Mechanics.Single().Displacement is { Forward: false, Distance: 10 }, "Aero radial parameter.");
    }

    private static void Immunity()
    {
        var f = new Fixture(); f.Cast(10532);
        var activation = f.K.Mechanics[0].Activation;
        f.Set(100, a => a with { StatusSlots = [new(160, 0, 0, activation) { SlotIndex = 4 }] }); f.Sync();
        Check(!f.K.Immune(activation), "Expiry equality gave immunity.");
        f.Set(100, a => a with { StatusSlots = [
            new(160, 0, 0, activation.AddSeconds(10)) { SlotIndex = 4 },
            new(1209, 0, 0, activation.AddSeconds(-1)) { SlotIndex = 6 }] }); f.Sync();
        Check(!f.K.Immune(activation), "Role category did not overwrite in real slot order.");
        f.Set(100, a => a with { StatusSlots = [
            new(160, 0, 0, activation.AddSeconds(10)) { SlotIndex = 4 },
            new(1209, 0, 0, activation.AddSeconds(-1)) { SlotIndex = 6 },
            new(2663, 0, 0, activation.AddSeconds(5)) { SlotIndex = 9 }] }); f.Sync();
        Check(f.K.Immune(activation) && !f.E.AmbientAoes().Any(a => a.Id.EndsWith("/navigation")), "Independent job immune not consumed.");
        f.E.CurrentPartyMembers = f.E.CurrentPartyMembers.Select(p => p with { Slot = p.Slot == 0 ? 1 : p.Slot == 1 ? 0 : p.Slot }).ToArray();
        f.Sync(); Check(!f.K.Immune(activation), "Immunity followed identity instead of stored slot.");
    }

    private static void Gaze()
    {
        var f = new Fixture();
        f.Cast(10539); f.Step(3);
        Check(f.K.Hints().Contains("Face the eye!") == false, "Already-facing reverse gaze danger.");
        var heading = f.E.GazeFacing(default, MathF.PI);
        Check(heading.HasValue && MathF.Cos(heading.Value) >= .707107f, "Reverse gaze failed PR heading.");
        f.Cast(10540, 3); f.Step(3);
        var both = f.E.GazeFacing(default, MathF.PI);
        Check(both.HasValue && f.K.Eyes.All(e => !KefkaEncounter.EyeDanger(e,
            f.K.Actors[100].Actor with { Rotation = both.Value })), "Positive/reverse gaze not jointly solved.");
        f.End(); f.End(3);
        Check(f.E.GazeFacing(default, 0) == null, "Gaze Finish leaked heading.");
    }

    private static void Party()
    {
        var f = new Fixture(); f.Cast(10528, target: 100);
        Check(f.K.Hints().Contains("Stack (8/8)"), "Exact-eight correct stack hint.");
        f.Set(107, a => a with { Position = new(15, 0, 0) }); f.Sync();
        Check(f.K.Hints().Contains("Stack! (7/8)"), "Exact-eight underfill hint.");
        var old = f.K.Mechanics[0].Target!;
        f.Actors.RemoveAll(a => a.Id == 100); f.Sync(101);
        f.Actors.Add(new(100, 0, new(15, 0, 0), Player: true, PartySlot: 0)); f.Sync();
        Check(!ReferenceEquals(old, f.K.Actors[100]) && ReferenceEquals(f.K.Mechanics[0].Target, old),
            "Rebuilt ID replaced original stack target.");
        f.End(); f.Cast(10527, target: 101);
        Check(f.E.AmbientAoes().Any(a => a.Id.StartsWith("kefka/spread/") && a.Shape is CircleTemplate { Radius: 5 }), "Spread R5 lost.");
        var marker = f.K.Mechanics.Single();
        f.Set(101, a => a with { Dead = true }); f.Sync();
        Check(f.K.Mechanics.Single() == marker && !f.E.AmbientAoes().Any(a => a.Id.StartsWith("kefka/spread/")),
            "Dead spread was deleted or still displayed.");
        f.Set(101, a => a with { Dead = false }); f.Sync();
        Check(f.E.AmbientAoes().Any(a => a.Id.StartsWith("kefka/spread/") && a.Activation == marker.Activation),
            "Revival did not restore original spread clock.");
    }

    private static void FinishConsumption()
    {
        var f = new Fixture();
        f.Cast(10527, target: 100);
        f.Step(.2);
        f.Cast(10527, source: 3, target: 100);
        var second = f.K.Mechanics[1];
        f.End(3);
        Check(f.K.Mechanics.Single() == second, "Finish consumed caster instead of first matching target ID.");
        f.End(2);
        Check(f.K.Mechanics.Count == 0, "Second target-ID Finish failed.");
        f.Cast(10528, target: 101);
        f.Actors.RemoveAll(a => a.Id == 101);
        f.Sync();
        f.Cast(10528, source: 3, target: 101);
        Check(f.K.Mechanics.Count == 1, "Missing target Start fabricated a marker.");
        f.End(3);
        Check(f.K.Mechanics.Count == 0, "Missing target Finish failed to consume existing target ID.");

        f = new Fixture();
        f.Cast(10516);
        var seed = f.K.Mechanics.Single();
        f.K.Mechanics.Add(seed with { Generation = 200 });
        f.K.Finish(f.K.Actors[2], f.K.Actors[2].Cast!);
        Check(f.K.Mechanics.Count == 1, "Shared group Finish consumed two entries.");
        foreach (var action in new uint[] { 10518, 10524, 10537, 10536 })
            f.K.Mechanics.Add(seed with { Action = action, Generation = action });
        f.K.Finish(f.K.Actors[2], f.K.Actors[2].Cast! with { Action = 999 });
        Check(f.K.Mechanics.Count == 1 && f.K.Mechanics[0].Action == 10536,
            "Unknown Finish did not broadcast once to each shared group or removed watched-only Wave.");
    }

    private static void Predictions()
    {
        var f = new Fixture();
        f.Actors.RemoveAll(a => a.Id == 101);
        f.Sync();
        f.Cast(10542, target: 101);
        Check(f.K.Damage().Single().TargetId == 101 && f.K.Hints().Contains("Tankbuster"),
            "Missing Actor suppressed roster target damage or global hint.");
        f.Step(10);
        var total = f.K.Actors[2].Cast!.TotalTime!.Value;
        Check(f.K.Damage().Single().Activation == f.Now.AddSeconds((total + .3f) - total),
            "Damage used frozen Start clock after raw elapsed saturated.");
        f.End();
        f.Set(2, a => a with { TargetId = 100 });
        f.Cast(10542, target: 2);
        Check(f.K.Damage().Single().TargetId == 100, "Self target did not fall back to current target.");
        f.Set(2, a => a with { TargetId = 101 });
        f.Sync();
        Check(f.K.Damage().Single().TargetId == 101, "Self target fallback failed dynamic retarget.");
        f.End();
        f.Cast(10541);
        f.Set(107, a => a with { Dead = true });
        f.Step(10);
        Check(f.K.Hints().Contains("Raidwide") && f.K.Damage().Count() == 6
            && f.K.Damage().All(p => p.Activation == f.Now.AddSeconds((total + .3f) - total)),
            "Raidwide included dead/missing Actors or used frozen activation.");
    }

    private static void SpreadClocks()
    {
        var f = new Fixture();
        f.Cast(10527, target: 100);
        var first = f.K.Mechanics.Single();
        f.Step(.5);
        f.Cast(10527, source: 3, target: 101);
        var second = f.K.Mechanics[1];
        var aoes = f.E.AmbientAoes().Where(a => a.Id.StartsWith("kefka/spread/")).ToArray();
        Check(aoes.Count(a => a.Display && a.OutlineOnly && !a.Navigate
                && a.Shape is CircleTemplate { Radius: 5 }) == 2, "Spread display differs from R5 outline.");
        Check(aoes.Any(a => a.Id.EndsWith($"{second.Generation}/avoid") && a.Activation == second.Activation
                && !a.Display && a.Shape is CircleTemplate { Radius: 6 })
            && !aoes.Any(a => a.Id == $"kefka/spread/{first.Generation}/avoid/101"),
            "Other spread acquired own earlier clock or R6 navigation missing.");
        var predictions = f.K.Damage().ToArray();
        // SpreadFromCastTargets passes alwaysShowSpreads=false through the
        // sixth positional parameter to UniformStackSpread.raidwideOnResolve.
        Check(predictions.Length == 0, "Source RaidwideOnResolve=false spread generated damage predictions.");
        f.Actors.RemoveAll(a => a.Player && a.Id != 100);
        f.E.CurrentPartyMembers = [new(0, "", 100)];
        f.Sync();
        Check(!f.E.AmbientAoes().Any(a => a.Id.StartsWith("kefka/spread/") && a.Navigate),
            "Solo spread generated a forbidden zone.");
    }

    private static void TextEdges()
    {
        var f = new Fixture();
        f.Set(100, a => a with { Position = new(10, 0, 10) });
        f.Cast(10516);
        Check(!f.K.Hints().Contains("GTFO from aoe!"), "Source donut inner edge became text danger.");
        f.End();
        f.Set(100, a => a with { Position = new(0, 0, 10) });
        f.Cast(10530);
        Check(!f.K.Hints().Contains("GTFO from aoe!"), "Source Ruin apex became text danger.");
        f.K.Effect(3, 10531);
        f.End();
        f.Set(100, a => a with { Position = new(10.00001f, 0, 10) });
        f.Cast(10518);
        Check(!f.K.Hints().Contains("GTFO from aoe!"), "Ordinary text quantized raw player into the circle.");

        f = new Fixture();
        f.Set(101, a => a with { Position = new(6, 0, 0) });
        f.Cast(10528, target: 100);
        Check(f.K.Hints().Contains("Stack! (7/8)"), "Stack ignored quantized target center edge.");
        f.End();
        f.Set(101, a => a with { Position = new(.1f, 0, 0) });
        f.Cast(10528, target: 100);
        f.Cast(10528, source: 3, target: 101);
        f.Actors.RemoveAll(a => a.Id == 100);
        f.Sync(101);
        f.Actors.Add(new(100, 0, default, Player: true, Hostile: false, PartySlot: 0));
        f.Sync(101);
        Check(f.K.Hints().Contains("Stack (8/8)"), "Destroyed original target marker was mistaken for a current marked party Actor.");

        f = new Fixture();
        f.Cast(10527, target: 100);
        foreach (var id in Enumerable.Range(101, 7))
            f.Set((ulong)id, a => a with { Position = new(15, 0, 0) });
        f.Sync();
        Check(f.K.Hints().Contains("Spread!（已分散）"), "Safe spread informational hint missing.");
    }

    private static void PetStatuses()
    {
        var f = new Fixture();
        f.K.Clear();
        f.Set(1, a => a with { InCombat = false });
        f.Set(100, a => a with { NativeType = 0x202,
            StatusSlots = [new(160, 0, 0, START.AddSeconds(30)) { SlotIndex = 4 }] });
        f.Sync();
        f.Set(1, a => a with { InCombat = true });
        f.Sync();
        f.Cast(10532);
        var activation = f.K.Mechanics.Single().Activation;
        Check(f.K.Immune(activation), "Cold Pet status was excluded from component bootstrap.");
        f.Set(100, a => a with { StatusSlots = [] });
        f.Sync();
        Check(f.K.Immune(activation), "Hot Pet Lose incorrectly cleared cold immunity.");
        f.Set(100, a => a with { StatusSlots = [new(160, 0, 0, START.AddSeconds(30)) { SlotIndex = 4 }] });
        f.Sync();
        f.Actors.RemoveAll(a => a.Id == 100 || a.Id == 101);
        f.Sync(102);
        f.Actors.Add(new(101, 0, default, Player: true, Hostile: false, NativeType: 0x202,
            StatusSlots: [new(2663, 0, 0, START.AddSeconds(30)) { SlotIndex = 9 }]));
        f.Sync(101);
        Check(!f.K.Immune(activation), "Hot newly created Pet Gain was mistaken for cold component replay.");
        f.Actors.Add(new(100, 0, default, Player: true, Hostile: false));
        f.Sync(100);
        Check(f.K.Immune(activation), "Destroyed Pet generated an unsupported status Lose callback.");
    }

    private static void Output()
    {
        var f = new Fixture(); f.Cast(10535, rotation: MathF.PI / 2);
        using var safe = new ScriptSafeZoneApi { DangerMarginForTest = 0, UtcNowForTest = f.Now };
        var draw = new ScriptDrawApi();
        var output = new DungeonOutput(safe, draw, safe.Manager);
        output.Apply(f.E, f.Now, false);
        Check(safe.Manager.BuildSnapshot(f.Now).DangerAreas.Count > 0 && draw.Previews.Count > 1,
            "Actual movement navigation/shared display missing.");
        f.E.CurrentPartyMembers = [new(0, "", 101)]; f.Sync();
        output.Apply(f.E, f.Now, false);
        Check(safe.Manager.BuildSnapshot(f.Now).DangerAreas.Count == 0
            && safe.Manager.BuildSnapshot(f.Now).ArenaBoundary == null, "Non-roster personal navigation/boundary retained.");
        output.Clear();
        Check(safe.Manager.BuildSnapshot(f.Now).DangerAreas.Count == 0 && draw.Previews.Count == 0, "Owner cleanup.");
    }

    private static void Lifecycle()
    {
        var f = new Fixture(); f.Cast(10516);
        var original = f.K.Mechanics[0];
        f.Set(2, a => a with { Cast = a.Cast! with { StartedAt = f.Now.AddSeconds(-2), FinishAt = f.Now.AddSeconds(1) } }); f.Sync();
        Check(f.K.Mechanics.Single() == original, "Clock-only raw drift became new cast.");
        f.Set(2, a => a with { Cast = a.Cast! with { Location = new(3, 0, 0) } }); f.Sync();
        Check(f.K.Mechanics.Single().Generation != original.Generation && f.K.Mechanics[0].Origin.X == 3, "Real Loc replacement omitted.");
        f.Sync(999); Check(f.K.Active && f.K.Mechanics.Count == 1, "Missing viewer erased global state.");
        f.K.End(); f.Sync(); Check(!f.K.Active, "Old primary restarted completed phase.");
        f.Actors.RemoveAll(a => a.Id == 1); f.Sync();
        f.Actors.Add(new(1, 0x2153, default, InCombat: true, Hp: 1000)); f.Sync();
        Check(f.K.Active && f.K.Mechanics.Count == 1, "New primary failed fresh cold activation.");
    }

    private static void LandingOracle()
    {
        Check(!new KefkaKnockbackGeometry(new(default, 10, 0, START, false)).Contains(new(9, 0, 0)),
            "Radial exact R19 boundary became dangerous through default epsilon.");
        Check(!new KefkaKnockbackGeometry(new(default, 15, MathF.PI / 2, START, true)).Contains(new(4, 0, 0)),
            "Fixed exact R19 boundary became dangerous through default epsilon.");
        foreach (var forward in new[] { false, true })
            foreach (var rotation in new[] { 0f, .71f, MathF.PI / 2 })
                foreach (var origin in new[] { Vector3.Zero, new Vector3(3, 0, -2) })
                {
                    var movement = new KefkaDisplacement(origin, forward ? 15 : 10, rotation, START, forward);
                    var shape = new KefkaKnockbackGeometry(movement);
                    for (var x = -20; x <= 20; ++x)
                        for (var z = -20; z <= 20; ++z)
                        {
                            var p = new Vector3(x, 0, z);
                            var dx = forward ? Math.Sin(rotation) : x - origin.X;
                            var dz = forward ? Math.Cos(rotation) : z - origin.Z;
                            var norm = Math.Sqrt(dx * dx + dz * dz);
                            var lx = x + (norm > 0 ? movement.Distance * dx / norm : 0);
                            var lz = z + (norm > 0 ? movement.Distance * dz / norm : 0);
                            var squared = lx * lx + lz * lz;
                            if (Math.Abs(squared - 361) < .001) continue;
                            Check(shape.Contains(p, 0) == (squared > 361), "Independent scalar landing predicate differs.");
                        }
                }
    }

    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
