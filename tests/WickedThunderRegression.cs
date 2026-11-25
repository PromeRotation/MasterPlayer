using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

internal static class WickedThunderRegression
{
    private static readonly DateTime START = new(2026, 11, 25, 10, 0, 0, DateTimeKind.Utc);
    private sealed class Fixture
    {
        internal readonly DungeonEncounter E = new(1231);
        internal readonly List<DungeonActor> Actors =
        [
            new(1, 0x4263, new(100, 0, 100), InCombat: true, Hp: 1000, MaxHp: 1000),
            new(2, 0x233C, new(100, 0, 80)),
            new(3, 0x4264, new(100, 0, 100)),
        ];
        internal DateTime Now = START;
        internal WickedThunderEncounter M => E.WickedThunder;
        internal Fixture()
        {
            for (var i = 0; i < 8; ++i)
                Actors.Add(new((ulong)(100 + i), 0, new(100 + i, 0, 100), PartySlot: i,
                    Player: true, Hostile: false));
            E.CurrentPartyMembers = Enumerable.Range(0, 8).Select(i => new ReplayPartyMember(i, "", (ulong)(100 + i))).ToArray();
            Sync();
        }
        internal void Set(ulong id, Func<DungeonActor, DungeonActor> change)
        { var index = Actors.FindIndex(a => a.Id == id); Actors[index] = change(Actors[index]); }
        internal void Sync(ulong viewer = 100)
            => E.Synchronize(Actors, Actors.FirstOrDefault(a => a.Id == viewer)?.Position, Now, playerId: viewer);
        internal void Step(double seconds) { Now = Now.AddSeconds(seconds); Sync(); }
        internal void Cast(uint action, ulong source = 2, ulong target = 0, float rotation = 0, double duration = 3)
        {
            Set(source, a => a with { Cast = new(action, a.Position, rotation, Now,
                Now.AddSeconds(duration + .3), target, TotalTime: (float)duration, IsSpell: true) });
            Sync();
        }
        internal void End(ulong source = 2) { Set(source, a => a with { Cast = null }); Sync(); }
        internal void Status(ulong id, int slot, uint action, ushort param = 0)
        {
            Set(id, a => a with { StatusSlots = [new ReplayStatus(action, param, 1, Now.AddSeconds(30)) { SlotIndex = slot }] });
            Sync();
        }
    }

    internal static void Run(Action<string, Action> run)
    {
        run("WickedThunder native catalog source CFC separate exact square and reduced platforms", Catalog);
        run("WickedThunder native ordinary locks watched Finish no timeout no enum fake navigation", Ordinary);
        run("WickedThunder native side copies first two unequal clocks Timeline source global FIFO", Sides);
        run("WickedThunder native cannon real slots arbitrary Start resets delays front two sticky colors", Cannons);
        run("WickedThunder native cannon cold actor cast status interleaving and hot Pet filtering", Cold);
        run("WickedThunder native hunt all display first12 risk hidden promotion sticky colors", Hunts);
        run("WickedThunder native Hyper fixed reduced center green ten effects independent counter", Hyper);
        run("WickedThunder native Stampede quantized lock 9.4 effect clock map only cancellation", Stampede);
        run("WickedThunder native five-hit stack repeated Source icons target transfer and independent counters", Stacks);
        run("WickedThunder native stack original references death revival rebuilding and capacity", StackReferences);
        run("WickedThunder native Jolt dynamic original targets Finish retained effect FIFO no target reacquire", Baits);
        run("WickedThunder native spread R6 display R7 navigation target Finish and missing source identity", Spreads);
        run("WickedThunder native damage dynamic raw NPC clock no invented stack spread predictions", Damage);
        run("WickedThunder native output exact stack complement Jolt full length owner and nonparty gates", Output);
        run("WickedThunder native lifecycle no view death wipe switch and same-ID source generation", Lifecycle);
    }

    private static void Catalog()
    {
        var f = new Fixture();
        Check(f.E.IsWickedThunder && f.E.Definitions[0].Casts.All(c => c.Shape == null), "Raw ownership.");
        Check(f.E.Arena is RectangleGeometry { Width: 40, Length: 40, Rotation: 0 }, "Square parameters.");
        f.M.Map(0, 0x00020001);
        Check(f.M.Center == new Vector3(85, 0, 100) && f.M.Arena.Contains(new(80, 0, 80), 0)
            && !f.M.Arena.Contains(new(91, 0, 100), 0), "East-removed platform.");
        f.M.Map(0, 0x00200010);
        Check(f.M.Center.X == 115, "West-removed platform.");
        f.M.Map(0, 0x00400004);
        Check(ReferenceEquals(f.M.Arena, WickedThunderCatalog.ARENA), "Map restore.");
    }

    private static void Ordinary()
    {
        var f = new Fixture();
        f.Cast(37560, rotation: .4f);
        var aoe = f.M.Ordinary.Single();
        f.Set(2, a => a with { Position = new(110, 0, 90), Rotation = 1 });
        f.Step(30);
        Check(f.M.Ordinary.Single() == aoe && aoe.Shape is RayRectangleTemplate { Length: 40 }, "Lock/timeout.");
        f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast! with { Action = 999 });
        Check(f.M.Ordinary.Count == 1, "SimpleAOEs accepted unrelated Finish.");
        f.End();
        Check(f.M.Ordinary.Count == 0, "Watched Finish.");
        f.Cast(37545); f.End();
        Check(f.M.Ordinary.Count == 0 && !f.E.AmbientAoes().Any(a => a.Navigate), "Enum-only cast acquired AOE.");
    }

    private static void Sides()
    {
        var f = new Fixture();
        f.M.Timeline(2, 0x11D8);
        Check(f.M.Sides.Count == 0, "Timeline wrong OID.");
        f.M.Timeline(3, 0x11D8);
        f.Step(1);
        f.Cast(37564, rotation: .7f);
        f.Cast(37561, source: 3);
        var first = f.M.VisibleSides().ToArray();
        Check(first.Length == 2 && first[0].Danger && first[0].Risky && !first[1].Risky
            && !f.M.Sides[0].Danger, "Copy versus sticky / first2.");
        f.End();
        Check(f.M.Sides.Count == 2 && f.M.Sides[0].Action == 37564, "Global source-independent FIFO.");
        f.End(3);
        Check(f.M.Sides.Single().Action == 37561 && !f.M.VisibleSides().Single().Danger, "Promotion color.");
    }

    private static void Cannons()
    {
        var f = new Fixture();
        f.Cast(37549);
        f.Status(100, 4, 2970, 0x2D3);
        f.Status(101, 5, 2970, 0x2D4);
        f.Status(102, 59, 2970, 0);
        Check(f.M.Cannons.Count == 3 && f.M.Cannons[0].Activation == START.AddSeconds(9.5f)
            && f.M.Cannons[1].Activation == START.AddSeconds(8.3f)
            && f.M.Cannons[0].Rotation == MathF.PI && f.M.Cannons[1].Rotation == 0, "Slot/delay/angle.");
        var visible = f.M.VisibleCannons().ToArray();
        Check(visible.Length == 2 && visible[0].Danger && visible[0].Risky && !visible[1].Risky, "Front2.");
        f.M.Effect(3, 20032, 0);
        f.M.VisibleCannons().ToArray();
        f.M.Effect(2, 37551, 0);
        Check(f.M.VisibleCannons().Single().Risky, "Promotion.");
        // Previously exposed second cannon stays yellow when it becomes last.
        f.M.Cannons.Clear();
        f.Status(100, 4, 2970, 1);
        f.Status(101, 5, 2970, 1);
        var exposed = f.M.VisibleCannons().First();
        f.M.Cannons.RemoveAt(1);
        Check(f.M.VisibleCannons().Single().Danger && exposed.Danger, "Sticky last color.");
        f.Cast(999);
        f.Status(102, 59, 2970, 1);
        Check(f.M.Cannons.Count == 1, "Unrelated Start failed to reset delays.");
    }

    private static void Cold()
    {
        var f = new Fixture();
        f.M.Clear();
        f.Set(1, a => a with { Cast = new(37549, a.Position, 0, START, START.AddSeconds(3.3),
            TotalTime: 3, IsSpell: true), StatusSlots = [new ReplayStatus(2970, 0, 1, START.AddSeconds(30)) { SlotIndex = 59 }] });
        f.Set(100, a => a with { NativeType = 0x202,
            StatusSlots = [new ReplayStatus(2970, 0, 1, START.AddSeconds(30)) { SlotIndex = 10 }] });
        f.Sync();
        Check(f.M.Cannons.Count == 2, "Cold Pet slot not replayed.");
        f.Status(100, 10, 2970, 3);
        Check(f.M.Cannons.Count == 2, "Hot Pet should not gain.");
        f.Set(100, a => a with { NativeType = 0 });
        f.Status(100, 11, 2970, 4);
        Check(f.M.Cannons.Count == 3, "New real slot must gain.");
    }

    private static void Hunts()
    {
        var f = new Fixture();
        for (var i = 0; i < 14; ++i) { f.Cast(37557); f.End(); }
        var all = f.M.VisibleHunts().ToArray();
        Check(all.Length == 14 && all.Take(12).All(m => m.Risky && m.Danger)
            && all.Skip(12).All(m => !m.Risky && !m.Danger), "Tail must display without risk.");
        f.M.Effect(3, 37558, 0);
        Check(f.M.VisibleHunts().ElementAt(11).Risky && !f.M.VisibleHunts().Last().Risky, "Hidden promotion.");
        f.M.Effect(3, 37558, 0);
        Check(f.M.VisibleHunts().First().Danger, "Hunt sticky color.");
    }

    private static void Hyper()
    {
        var f = new Fixture();
        f.Cast(37553);
        Check(f.M.Hyper == null, "Hyper outside reduced arena.");
        f.End();
        f.M.Map(0, 0x00020001);
        f.Cast(37554);
        var hyper = f.M.Hyper!;
        Check(hyper.Origin == new Vector3(85, 0, 80) && !hyper.Risky
            && hyper.Activation == f.Now.AddSeconds(3.3f).AddSeconds(.6d), "Hyper lock and clock.");
        f.End(); f.Step(30);
        for (var i = 0; i < 9; ++i) f.M.Effect(3, 37555, 0);
        Check(f.M.Hyper != null && f.M.Damage().Count() == 8, "Hyper premature clear/damage.");
        f.M.Effect(3, 37555, 0);
        Check(f.M.Hyper == null, "Tenth effect.");
    }

    private static void Stampede()
    {
        var f = new Fixture();
        f.Set(3, a => a with { Rotation = .8f }); f.Sync();
        f.M.Effect(3, 37547, 0);
        var stamp = f.M.Stampede!;
        Check(stamp.Activation == START.AddSeconds(9.4d) && stamp.Origin == DungeonEncounter.QuantizedDungeonPosition(new(95, 0, 80))
            && stamp.Rotation == .8f, "Stampede source pose/clock.");
        f.Step(50);
        Check(f.M.Stampede == stamp, "Stampede timed out.");
        f.M.Map(1, 0x00020001);
        Check(f.M.Stampede != null, "Wrong index.");
        f.M.Map(0, 0x00020001);
        Check(f.M.Stampede == null, "Destruction map clear.");
    }

    private static void Stacks()
    {
        var f = new Fixture();
        f.M.Icon(100, 316); f.M.Icon(101, 161);
        var old = f.M.Stacks[0];
        f.Step(1);
        f.M.Effect(2, 37571, 102);
        Check(f.M.Stacks[0].TargetId == 102 && f.M.Stacks[0].Activation == old.Activation, "Transfer refreshed clock.");
        for (var i = 0; i < 3; ++i) f.M.Effect(2, 37571, 102);
        Check(f.M.Stacks.Count == 2, "Fourth hit cleared.");
        f.M.Effect(2, 37569, 999);
        Check(f.M.Stacks.Single().Action == 37571, "Independent Soulpress fallback.");
        f.M.Effect(3, 37571, 102);
        Check(f.M.Stacks.Count == 0, "Fifth hit.");
        f.M.Icon(100, 316); f.M.Icon(100, 316);
        Check(f.M.Stacks.Count == 2, "Duplicate icons were deduped.");
        for (var i = 0; i < 5; ++i) f.M.Effect(2, 37571, 999);
        Check(f.M.Stacks.Count == 1 && f.M.Stacks[0].TargetId == 100, "Unmatched fifth removes only first.");
    }

    private static void StackReferences()
    {
        var f = new Fixture();
        f.M.Icon(100, 316);
        var target = f.M.Stacks[0].Target;
        f.Set(100, a => a with { Dead = true }); f.Sync();
        Check(!f.E.AmbientAoes().Any(a => a.Id.StartsWith("wicked/stack/")) && f.M.Stacks.Count == 1, "Dead stack deleted.");
        f.Set(100, a => a with { Dead = false }); f.Sync();
        Check(f.E.AmbientAoes().Any(a => a.Id.StartsWith("wicked/stack/")), "Revived stack hidden.");
        f.Actors.RemoveAll(a => a.Id == 100); f.Sync(101);
        f.Actors.Add(new(100, 0, new(110, 0, 110), Player: true, Hostile: false)); f.Sync();
        Check(ReferenceEquals(target, f.M.Stacks[0].Target) && !ReferenceEquals(target, f.M.Actors[100]), "Stack rebound.");
    }

    private static void Baits()
    {
        var f = new Fixture();
        f.Cast(37576, target: 100);
        var bait = f.M.Baits.Single();
        f.End();
        f.Set(100, a => a with { Position = new(110, 0, 80) }); f.Step(20);
        Check(f.M.Baits.Count == 1 && WickedThunderEncounter.BaitRotation(bait) == MathF.PI / 2, "Bait dynamic/finish.");
        f.M.Effect(3, 37576, 0);
        Check(f.M.Baits.Count == 1, "Wrong source ended bait.");
        f.M.Effect(2, 37576, 0);
        Check(f.M.Baits.Count == 0, "Watched source effect.");
        f.Cast(37576, target: 999); f.End();
        f.Actors.Add(new(999, 0, default, Player: true)); f.Sync();
        Check(f.M.Baits.Count == 0, "Missing target was reacquired late.");
    }

    private static void Spreads()
    {
        var f = new Fixture();
        f.Cast(37573, target: 101);
        Check(f.E.AmbientAoes().Any(a => a.Id.StartsWith("wicked/spread/") && a.Shape is CircleTemplate { Radius: 6 } && !a.Navigate)
            && f.E.AmbientAoes().Any(a => a.Shape is CircleTemplate { Radius: 7 } && !a.Display), "Spread radii.");
        f.M.Finish(f.M.Actors[3], f.M.Actors[2].Cast!);
        Check(f.M.Ordinary.Count == 0, "Spread Finish must match target instead of source.");
    }

    private static void Damage()
    {
        var f = new Fixture();
        f.M.Icon(100, 316); f.Cast(37573, target: 100);
        Check(!f.M.Damage().Any(), "Stack/spread invented prediction.");
        f.Cast(37575);
        f.Set(101, a => a with { Dead = true }); f.Step(20);
        var damage = f.M.Damage().ToArray();
        Check(damage.Length == 7 && damage.All(d => d.Activation == f.Now.AddSeconds(.3f)), "Dynamic capped NPC clock.");
        f.End();
        Check(!f.M.Damage().Any(), "Raidwide finish.");
    }

    private static void Output()
    {
        var f = new Fixture();
        f.M.Icon(101, 316);
        var join = f.E.AmbientAoes().Single(a => a.Id == "wicked/stack/join/37571");
        var shape = join.Shape.Resolve(default, 0, null);
        Check(!shape.Contains(new(101, 0, 100), 0) && shape.Contains(new(115, 0, 115), 0), "Stack complement.");
        f.M.Stacks.Clear();
        f.Cast(37576, target: 101);
        var bait = f.E.AmbientAoes().Single(a => a.Id.StartsWith("wicked/bait/"));
        Check(bait.Navigate && bait.Shape.Resolve(bait.Position, bait.Rotation, null).Contains(new(101.5f, 0, 130), 0),
            "Jolt must keep full60 instead of target length.");
        f.Sync(999);
        Check(!f.E.AmbientAoes().Any(a => a.Navigate), "Missing view navigation.");
    }

    private static void Lifecycle()
    {
        var f = new Fixture();
        f.Cast(37557); f.End();
        f.Sync(0);
        Check(f.M.Hunts.Count == 1, "Missing view reset global history.");
        f.Set(100, a => a with { Dead = true }); f.Sync();
        Check(f.M.Hunts.Count == 1, "Viewer death reset history.");
        f.M.End(); f.Sync();
        Check(!f.M.Active && f.M.Hunts.Count == 0, "Wipe reused old primary.");
        f.Actors.RemoveAll(a => a.Id == 1); f.Sync();
        f.Actors.Add(new(1, 0x4263, new(100, 0, 100), InCombat: true, Hp: 1000, MaxHp: 1000)); f.Sync();
        Check(f.M.Active, "Real new primary failed to activate.");
    }

    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
