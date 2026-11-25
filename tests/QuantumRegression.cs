using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Replay;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

internal static class QuantumRegression
{
    private static readonly DateTime START = new(2026, 11, 25, 3, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 CENTER = new(-600, 0, -300);
    private sealed class Fixture
    {
        internal readonly DungeonEncounter E = new(1311);
        internal readonly List<DungeonActor> Actors =
        [
            new(1, 0x48EE, CENTER, InCombat: true, Hp: 1000, MaxHp: 1000),
            new(2, 0x48EF, CENTER, Hp: 1000, MaxHp: 1000),
            new(3, 0x233C, CENTER + Vector3.UnitX * 7),
            new(100, 0, CENTER, PartySlot: 0, Player: true),
            new(101, 0, CENTER + Vector3.UnitX, PartySlot: 1, Player: true),
            new(102, 0, CENTER - Vector3.UnitX, PartySlot: 2, Player: true),
            new(103, 0, CENTER - Vector3.UnitZ, PartySlot: 3, Player: true),
        ];
        internal DateTime Now = START;
        internal Fixture(bool q40 = true)
        {
            E.CurrentPartyMembers = Enumerable.Range(0, 4).Select(i => new ReplayPartyMember(i, "", (ulong)(100 + i))).ToArray();
            if (q40)
            {
                Set(1, a => a with { StatusSlots = new uint[] { 4571, 1018, 4570, 586 }
                    .Select((id, i) => new ReplayStatus(id, 8, 0, START.AddHours(1)) { SlotIndex = i }).ToArray() });
                Set(2, a => a with { StatusSlots = [new(4572, 8, 0, START.AddHours(1)) { SlotIndex = 0 }] });
            }
            Sync();
            Now = Now.AddMilliseconds(10);
            Sync();
        }
        internal QuantumEncounter Q => E.Quantum;
        internal void Set(ulong id, Func<DungeonActor, DungeonActor> change)
        {
            var index = Actors.FindIndex(a => a.Id == id);
            Actors[index] = change(Actors[index]);
        }
        internal void Sync(ulong viewer = 100) => E.Synchronize(Actors, Actors.FirstOrDefault(a => a.Id == viewer)?.Position, Now, playerId: viewer);
        internal void Step(double seconds) { Now = Now.AddSeconds(seconds); Sync(); }
        internal void Cast(uint action, ulong source = 3, double duration = 3)
        {
            Set(source, a => a with { Cast = new(action, a.Position, a.Rotation, Now, Now.AddSeconds(duration + .3), TotalTime: (float)duration) });
            Sync();
        }
        internal void End(ulong source = 3) { Set(source, a => a with { Cast = null }); Sync(); }
        internal void Effect(uint action, ulong source = 3, Vector3? target = null) => Q.Effect(source, action, target, Now);
        internal void ToPull()
        {
            Effect(44797); Effect(44798); Sync();
            Check(Q.Timeline.Stage == 3, "Expected Pull state.");
        }
        internal void ToSmall()
        {
            ToPull();
            Effect(44121); Sync();
            Check(Q.Timeline.Stage == 4, "Pull must consume EventCast.");
        }
    }

    internal static void Run(Action<string, Action> run)
    {
        run("Quantum native catalog exact identity source component rules and unsupported branches", Catalog);
        run("Quantum native fork sees previous component update and caches missing Eater only before pull", QuantumLevel);
        run("Quantum native condition early transitions exact counts float timeout and permanent WIP", Timeline);
        run("Quantum native raw ordinary cast captured clocks no timeout true Finish and same-ID reconstruction", Ordinary);
        run("Quantum native source crystal direction creation order two branches and event-only advancement", Blaze);
        run("Quantum native cached future clocks hidden exaflares and no rebase on wall clock alone", BlazeCache);
        run("Quantum native Bounds actual Finish locks circle or donut true Map holes and clearing indices", Bounds);
        run("Quantum native tower inverse-mask initialization differs from Gain Lose callbacks", Towers);
        run("Quantum native two-person stack old actor references repeated icons VFX-only state counter", Stacks);
        run("Quantum native Drain two clocks strict real Finish expiry prior activation and slot masks", Drain);
        run("Quantum native public output inverse pool hard navigation true holes previews and owner clear", Output);
        run("Quantum native missing viewer disabled scope death director and rebuilt original actor", Lifecycle);
        run("Quantum native source ray-box independent scalar oracle across signs corners and zero direction", RayOracle);
        run("Quantum native raw location replacement ignores StartedAt correction and cold component uses accumulated float clock", CastClocks);
        run("Quantum native status source normalization single-direction one-second refresh threshold and persistent slot masks", StatusRefresh);
        run("Quantum native real tower damage mask neutral hints and raw input Created before Cast ordering", HintsAndOrder);
        run("Quantum native construction pending status callbacks use previous Party without frame-end rebootstrap", PendingConstruction);
        run("Quantum native actual Pet creation and destruction status callbacks differ from cold component replay", PetStatuses);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void PetStatuses()
    {
        var f = new Fixture(false);
        f.Actors.RemoveAll(a => a.Id == 100);
        f.Sync(101);
        f.Actors.Add(new(100, 0, CENTER, Player: true, NativeType: 0x202,
            StatusSlots: [new(4560, 0, 0, START.AddSeconds(30)) { SlotIndex = 5 }]));
        f.Sync();
        Check(f.Q.TargetPriority(f.Actors.Single(a => a.Id == 1)) == -1, "Hot created Pet changed source light cache.");
        // Reconstruct the module while the Pet already exists and the old roster
        // is installed. Only that component cold replay may consume its status.
        f.Actors.RemoveAll(a => a.Id == 1);
        f.Sync();
        f.Actors.Add(new(10, 0x48EE, CENTER, Hp: 1000, MaxHp: 1000));
        f.Sync();
        Check(f.Q.TargetPriority(f.Actors.Single(a => a.Id == 10)) == 0, "Cold Pet replay did not populate light mask.");
        f.Actors.RemoveAll(a => a.Id == 100);
        f.Sync(101);
        f.Actors.Add(new(100, 0, CENTER, Player: true));
        f.Sync();
        Check(f.Q.TargetPriority(f.Actors.Single(a => a.Id == 10)) == 0,
            "Actual Pet destruction Lose cleared a cold-replayed light mask.");
    }

    private static void Catalog()
    {
        var f = new Fixture();
        Check(f.Q.Timeline.Stage == 1 && f.E.Active?.BossOid == 0x48EE
            && f.E.Arena!.GetBounds().Width == 41 && f.E.Arena.GetBounds().Height == 31, "Identity/outward round floor.");
        Check(ArrDungeonCatalog.Owns(0x48EE, 1311) && !ArrDungeonCatalog.Owns(0x48EF, 1311)
            && !ArrDungeonCatalog.Owns(0x48EE, 1063), "CFC/territory/primary ownership.");
        Check(f.E.Definitions[0].Casts.Length == 9 && f.E.OutputCasts.Count() == 0, "Activated rules only.");
    }

    private static void QuantumLevel()
    {
        var f = new Fixture(false);
        Check(f.Q.Timeline.Stage == 0 && f.Q.Level == 0, "Empty level must wait indefinitely.");
        f.Step(20000);
        Check(f.Q.Timeline.Stage == 0, "ConditionFork gained timeout.");
        f.Set(1, a => a with { StatusSlots = [new(4571, 16, 0, f.Now.AddHours(1))] });
        f.Sync();
        Check(f.Q.Level == 16 && f.Q.Timeline.Stage == 0, "State machine read same-frame level update.");
        f.Sync();
        Check(f.Q.Timeline.Stage == 12, "16 must select unsupported 15 branch once.");
        f.Step(20000);
        Check(f.Q.Timeline.Stage == 12, "SimpleState auto-expired.");
        var absent = new Fixture(false);
        absent.Actors.RemoveAll(a => a.Id == 2);
        // Rebuild from a world which never had Eater during CheckPull.
        absent.E.ResetFight();
        absent.Sync();
        absent.Actors.Add(new(20, 0x48EF, CENTER, StatusSlots: [new(4572, 40, 0, absent.Now.AddHours(1))]));
        absent.Sync();
        Check(absent.Q.Eater == null && absent.Q.Level == 0, "Late Eater rebound after pull.");
    }

    private static void Timeline()
    {
        var f = new Fixture();
        f.ToPull();
        f.Effect(44121); f.Sync();
        for (var i = 0; i < 13; ++i) f.Effect(44122);
        f.Sync();
        Check(f.Q.Timeline.Stage == 5, "Count 13 incorrectly satisfied ==12.");
        f.Step(2.599);
        Check(f.Q.Timeline.Stage == 5, "Small end timeout early.");
        f.Step(.0011);
        Check(f.Q.Timeline.Stage == 6 && f.Q.Timeline.Overdue.SequenceEqual([5]), "Overdue source threshold.");
        f.Step(2.401); f.Step(2.201); f.Step(6.101); f.Step(7.901);
        Check(f.Q.Timeline.Stage == 11, "Empty lines condition must finish after start timeout.");
        f.Step(20000);
        Check(f.Q.Timeline.Stage == 11, "Final source ??? state auto-exited.");
    }

    private static void Ordinary()
    {
        var f = new Fixture();
        f.Cast(45117);
        var first = f.Q.Ordinary.Single();
        Check(first.Activation == f.Now.AddSeconds(3.3f), "Source float NPC clock.");
        f.Set(3, a => a with { Position = CENTER, Rotation = 1.2f });
        f.Step(8);
        Check(ReferenceEquals(first, f.Q.Ordinary.Single()) && first.Position == CENTER + Vector3.UnitX * 7, "AOE timed out or moved.");
        f.End();
        Check(f.Q.Ordinary.Count == 0, "True cast end did not remove group source.");
        f.Cast(44098);
        var original = f.Q.Ordinary.Single().Source;
        f.Actors.RemoveAll(a => a.Id == 3); f.Sync();
        f.Actors.Add(new(3, 0x233C, CENTER, Cast: new(45200, CENTER, 0, f.Now, f.Now.AddSeconds(5.3), TotalTime: 5)));
        f.Sync();
        Check(f.Q.Ordinary.Count == 1 && !ReferenceEquals(original, f.Q.Ordinary[0].Source), "Same-ID source reused old actor.");
    }

    private static void Blaze()
    {
        var f = new Fixture();
        f.Effect(44797);
        f.Actors.Add(new(10, 0x1EBE70, CENTER + Vector3.UnitX * 7)); f.Sync();
        f.Effect(44798);
        f.Cast(44118);
        var blaze = f.Q.Blaze!;
        Check(blaze.Crystals[0].Direction == Vector3.UnitX && blaze.Lines.Count == 2
            && blaze.Lines[0].Advance == Vector3.UnitX * 4 && blaze.Lines[0].Remaining == 3
            && blaze.Lines[1].Remaining == 7, "Creation direction/ray count.");
        Check(!blaze.Aoes().Any(), "Hidden source exaflares displayed.");
        f.Effect(44118);
        Check(blaze.NumCasts == 1 && blaze.Lines[0].Next == CENTER + Vector3.UnitX * 11
            && blaze.Lines[1].Next == CENTER + Vector3.UnitX * 3, "First effect must advance both directions.");
        f.Set(3, a => a with { Rotation = MathF.PI / 2 }); f.Sync();
        f.Effect(44119, target: CENTER + Vector3.UnitX * 11);
        Check(blaze.Lines[0].Next == CENTER + Vector3.UnitX * 15 && blaze.NumCasts == 1, "Rest position/orientation/NumCasts.");
    }

    private static void BlazeCache()
    {
        var blaze = new QuantumScourgingBlaze();
        var caster = new DungeonActor(3, 0x233C, CENTER);
        blaze.Effect(caster, 44797, default, START);
        blaze.Created(new(4, 0x1EBE70, CENTER));
        blaze.Start(caster, new(44118, CENTER, 0, START, START.AddSeconds(7)));
        blaze.Update(START);
        blaze.ShowAoes();
        var before = blaze.Aoes().ToArray();
        blaze.Update(START.AddSeconds(20));
        Check(blaze.Aoes().SequenceEqual(before), "Cached future clocks were rebased by time alone.");
        blaze.Effect(caster, 44118, default, START.AddSeconds(8));
        Check(blaze.NumCasts == 1 && blaze.Lines.All(l => l.Next == CENTER), "Strict <1 must reject exactly 1 second late.");
        blaze.Effect(caster, 44118, default, START.AddSeconds(6));
        Check(blaze.Lines.All(l => l.Next != CENTER), "Source accepts negative time difference.");
    }

    private static void Bounds()
    {
        var f = new Fixture(); f.ToSmall();
        f.Set(3, a => a with { Rotation = -MathF.PI / 2 });
        f.Cast(44122); f.End();
        Check(f.Q.EndAoe?.Shape is CircleTemplate { Radius: 8 }
            && f.Q.EndAoe.Activation == f.Now.AddSeconds(6), "End orientation/processing clock.");
        f.Q.Map(0, 0x00020001, f.Now);
        f.Q.Map(0x17, 0x00020001, f.Now);
        Check(f.Q.Pillars.SequenceEqual([0, 23]) && !f.Q.Arena.Contains(new(-600, 0, -307)), "Real polygon holes.");
        f.Q.Map(1, 0x00080004, f.Now);
        Check(f.Q.Pillars.Count == 2, "Wrong restore index cleared pillars.");
        f.Effect(44123);
        Check(f.Q.EndAoe == null && f.Q.Pillars.Count == 2, "End effect incorrectly restored floor.");
        f.Q.Map(0x0C, 0x00080004, f.Now);
        Check(f.Q.Pillars.Count == 0 && f.Q.Arena.Contains(new(-600, 0, -307)), "0C clear failed.");
    }

    private static void Towers()
    {
        var f = new Fixture();
        f.Set(100, a => a with { StatusSlots = [new(4560, 0, 0, START.AddHours(1))] }); f.Sync();
        f.ToPull();
        f.Q.Map(0x1B, 0x00020001, f.Now);
        Check(f.Q.Towers.Count == 4 && f.Q.Towers.All(t => (t.Forbidden & 3) == 0),
            "Activation Gain clears light; Map clears non-light.");
        f.Set(100, a => a with { StatusSlots = [] }); f.Sync();
        Check(f.Q.Towers.All(t => (t.Forbidden & 1) != 0), "Loss did not forbid.");
        f.Q.Map(0x1B, 0x00080004, f.Now);
        Check(f.Q.Towers.Count == 4, "Map count removed towers before state exit.");
    }

    private static void Stacks()
    {
        var f = new Fixture(); f.ToSmall();
        f.Q.Icon(100, 78, f.Now); f.Q.Icon(100, 78, f.Now);
        Check(f.Q.Stacks.Count == 2 && f.Q.Stacks.All(s => s.Activation == f.Now.AddSeconds(5.1d)), "Repeated icon dedup/delay.");
        var original = f.Q.Stacks[0].Target;
        f.Actors.RemoveAll(a => a.Id == 100); f.Sync(101);
        f.Actors.Add(new(100, 0, CENTER + Vector3.UnitX * 15, PartySlot: 0, Player: true)); f.Sync();
        Check(!ReferenceEquals(original, f.Q.Actors[100]) && ReferenceEquals(original, f.Q.Stacks[0].Target), "Old stack actor rebound.");
        for (var i = 0; i < 12; ++i) f.Effect(44122);
        f.Effect(44123); f.Q.Map(0x0C, 0x00080004, f.Now); f.Sync();
        Check(f.Q.Timeline.Stage == 7, "Expected VFX wait.");
        f.Q.Vfx(39); f.Sync();
        Check(f.Q.Timeline.Stage == 7, "Unrelated VFX consumed.");
        f.Q.Vfx(40); f.Sync();
        Check(f.Q.Timeline.Stage == 8 && f.Q.Stacks.Count == 0, "VFX-only counter did not end stacks.");
    }

    private static void Drain()
    {
        var f = new Fixture();
        f.Cast(44129);
        var requirement = f.Q.ColorRequirement();
        Check(requirement.Light && requirement.AvoidOther
            && requirement.Activation == f.Now.AddSeconds(3.3f).AddSeconds(-1), "Drain first slot.");
        f.Set(100, a => a with { StatusSlots = [new(4560, 0, 0, f.Now.AddSeconds(10)) { SlotIndex = 7 }] }); f.Sync();
        Check(!f.Q.ColorRequirement().Light, "Colour gain not accepted.");
        f.End();
        Check(f.Q.WantsLight == false, "First true Finish did not shift slot 1.");
        f.Cast(44130); f.End();
        Check(!f.Q.ColorRequirement().AvoidOther, "Second Finish did not end drain.");
        var priority = f.E.PriorityTarget(1, CENTER);
        Check(priority == 1, "Light colour target selection.");
    }

    private static void Output()
    {
        var f = new Fixture();
        using var safe = new ScriptSafeZoneApi { DangerMarginForTest = 0, UtcNowForTest = f.Now };
        var draw = new ScriptDrawApi();
        var manager = safe.Manager;
        var output = new DungeonOutput(safe, draw, manager);
        output.Apply(f.E, f.Now, false);
        var snapshot = manager.BuildSnapshot(f.Now);
        Check(snapshot.DangerAreas.Count > 0 && draw.Previews.Count >= 2, "Colour hard constraint or shared previews absent.");
        Check(f.E.AmbientAoes().Any(a => a.Id == "quantum/color/acquire" && !a.Display && a.Navigate), "Inverse acquisition is not navigation.");
        output.Clear();
        Check(manager.BuildSnapshot(f.Now).DangerAreas.Count == 0 && draw.Previews.Count == 0, "Owner output leaked.");
    }

    private static void Lifecycle()
    {
        var f = new Fixture(); f.Cast(45117);
        f.Sync(999);
        Check(f.Q.Ordinary.Count == 1 && !f.E.AmbientAoes().Any(), "Missing viewer dropped history or leaked personal output.");
        f.Sync();
        f.Set(100, a => a with { Dead = true }); f.Sync();
        Check(f.Q.Ordinary.Count == 1 && f.E.PreserveFightAcrossPlayerDeath, "Player death reset global model.");
        f.Q.EndModule(); f.Sync();
        Check(f.Q.Timeline.Stage == 13 && !f.Q.Loaded, "Director did not end original module.");
        f.Actors.RemoveAll(a => a.Id == 1); f.Sync();
        f.Actors.Add(new(1, 0x48EE, CENTER, InCombat: true, Hp: 1000, MaxHp: 1000)); f.Sync();
        Check(f.Q.Active && f.Q.Timeline.Stage == 0, "New primary retained old completed state.");
        f.E.Synchronize(f.Actors, CENTER, f.Now, _ => false, 100);
        Check(!f.Q.Loaded && !f.E.AmbientAoes().Any(), "Mechanism disabled retained native outputs.");
    }

    private static void RayOracle()
    {
        foreach (var x in new[] { -25f, -18.1f, -7f, 0f, 7f, 18.1f, 25f })
            foreach (var z in new[] { -20f, -14.1f, 0f, 14.1f, 20f })
                foreach (var direction in new[] { Vector3.UnitX, -Vector3.UnitX, Vector3.UnitZ, -Vector3.UnitZ, Vector3.Zero })
                {
                    var offset = new Vector3(x, 0, z);
                    var distance = QuantumScourgingBlaze.RayAabb(offset, direction, 18.1f, 14.1f);
                    if (direction == Vector3.Zero || direction.X == 0 && MathF.Abs(x) > 18.1f
                        || direction.Z == 0 && MathF.Abs(z) > 14.1f) continue;
                    var far = direction.X != 0 ? (MathF.CopySign(18.1f, direction.X) - x) / direction.X
                        : (MathF.CopySign(14.1f, direction.Z) - z) / direction.Z;
                    var near = direction.X != 0 ? (-MathF.CopySign(18.1f, direction.X) - x) / direction.X
                        : (-MathF.CopySign(14.1f, direction.Z) - z) / direction.Z;
                    var expected = near >= 0 ? near : far >= 0 ? far : float.MaxValue;
                    Check(distance == expected, $"Independent slab oracle ({x},{z}) {direction}: {distance} / {expected}.");
                }
    }

    private static void CastClocks()
    {
        var f = new Fixture();
        f.Cast(45117);
        var original = f.Q.Ordinary.Single();
        f.Set(3, a => a with { Cast = a.Cast! with { StartedAt = f.Now.AddSeconds(-3) } });
        f.Step(.1);
        Check(ReferenceEquals(original, f.Q.Ordinary.Single()), "Elapsed correction synthesized Finish/Start.");
        f.Set(3, a => a with { Cast = a.Cast! with { Location = CENTER } });
        f.Step(.1);
        Check(!ReferenceEquals(original, f.Q.Ordinary.Single()) && f.Q.Ordinary.Single().Position == CENTER,
            "Raw Location change failed source Finish/Start.");

        var cold = new Fixture(false);
        cold.Actors.RemoveAll(a => a.Id == 3);
        cold.Sync();
        cold.Actors.Insert(2, new(10, 0x1EBE70, CENTER + Vector3.UnitX * 7));
        cold.Actors.Add(new(3, 0x233C, CENTER + Vector3.UnitX * 7));
        cold.Cast(44118, duration: 1);
        cold.Set(1, a => a with { StatusSlots = [new(4571, 40, 0, cold.Now.AddHours(1))] });
        cold.Step(2);
        Check(cold.Q.Timeline.Stage == 0 && cold.Q.Level == 40, "Level must update after fork.");
        cold.Step(.1);
        var total = cold.Q.Actors[3].Cast!.TotalTime!.Value;
        Check(cold.Q.Blaze?.Lines.Count == 2
            && cold.Q.Blaze.Lines.All(l => l.Activation == cold.Now.AddSeconds((total + .3f) - total)),
            $"Cold clock: lines={cold.Q.Blaze?.Lines.Count}, elapsed={cold.Q.Actors[3].CastElapsed}, activation={cold.Q.Blaze?.Lines.FirstOrDefault()?.Activation:O}.");
        Check(cold.Q.Blaze!.HasUnknownDirection && !cold.Q.Blaze.Aoes().Any()
            && cold.Q.Hints().Any(h => h.Contains("direction history unavailable")),
            "Missing direction history was invented or expanded into unbounded output.");
    }

    private static void StatusRefresh()
    {
        var f = new Fixture();
        var expires = f.Now.AddSeconds(50);
        f.Set(100, a => a with { StatusSlots =
            [new(4560, 0, 0, expires) { SlotIndex = 7 }, new(4559, 0, 0, expires) { SlotIndex = 9 }] });
        f.Sync();
        f.Set(100, a => a with { StatusSlots =
            [new(4560, 0, 0xE0000000, default) { SlotIndex = 7 }, new(4559, 0, 0, expires) { SlotIndex = 9 }] });
        f.Step(.1);
        Check(f.Q.Actors[100].Statuses[7]!.SourceId == 0
            && f.Q.Actors[100].Statuses[7]!.ExpiresAt == default
            && !f.Q.ColorRequirement().Light && !f.Q.ColorRequirement().Dark,
            "Shortened expiry or invalid source triggered Lose/Gain.");
        f.Set(100, a => a with { StatusSlots = [new(4559, 0, 0, expires) { SlotIndex = 9 }] });
        f.Step(.1);
        Check(f.Q.ColorRequirement().Light, "Actual Lose must clear combined even with dark present.");
    }

    private static void HintsAndOrder()
    {
        var f = new Fixture();
        f.ToPull();
        f.Q.Map(0x1B, 0x00020001, f.Now);
        var tower = f.Q.Towers[0];
        f.Set(100, a => a with { Position = tower.Position }); f.Sync();
        Check(f.E.PredictedDamage.Count() == 1 && f.E.PredictedDamage.Single().TargetId == 100
            && f.E.PredictedDamage.Single().Activation == tower.Activation,
            "Tower predicted damage included non-soakers or wrong clock.");
        Check(f.Q.Hints().Contains("Soak the tower!（已在塔内）"), "Correct tower lost neutral hint.");
        f.Set(101, a => a with { Position = tower.Position }); f.Sync();
        Check(f.Q.Hints().Contains("Too many soakers in the tower!"), "Tower over-capacity hint absent.");

        var order = new Fixture();
        order.Effect(44797);
        order.Actors.Insert(2, new(10, 0x1EBE70, CENTER + Vector3.UnitX * 7, ObjectIndex: 30));
        order.Set(3, a => a with { ObjectIndex = 1, Cast = new(44118, a.Position, 0,
            order.Now, order.Now.AddSeconds(3.3), TotalTime: 3) });
        order.Sync();
        Check(order.Q.Blaze!.Lines.Count == 2, "ObjectIndex reordered source Created/Cast callbacks.");
    }

    private static void PendingConstruction()
    {
        var f = new Fixture(false);
        f.E.ResetFight();
        f.Set(100, a => a with { StatusSlots = [new(4560, 0, 0, f.Now.AddHours(1))] });
        f.Sync();
        Check(f.Q.ColorRequirement().Light, "New Party was incorrectly used for constructor/status callback.");
        f.Step(.1);
        Check(f.Q.ColorRequirement().Light, "Unchanged raw silently repaired source's missing initial mask.");
        f.Set(100, a => a with { StatusSlots = [new(4560, 1, 0, f.Now.AddHours(1))] });
        f.Step(.1);
        Check(!f.Q.ColorRequirement().Light, "A true later Gain failed to acquire the committed slot.");

        var retained = new Fixture();
        retained.ToSmall();
        retained.Q.Icon(100, 78, retained.Now);
        var previous = retained.Q.Primary;
        retained.Actors.RemoveAll(a => a.Id == 1);
        retained.E.QuantumInputScan = true;
        retained.Sync();
        retained.Actors.Add(new(1, 0x48EE, CENTER, InCombat: true, Hp: 1000, MaxHp: 1000));
        retained.Sync();
        retained.E.QuantumInputScan = false;
        retained.Sync();
        Check(retained.Q.Timeline.Stage == 4 && retained.Q.Stacks.Count == 1
            && !ReferenceEquals(previous, retained.Q.Primary),
            "Same-ID rebind before module Update reset existing phase/components.");
    }
}
