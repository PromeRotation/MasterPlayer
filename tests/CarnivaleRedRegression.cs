using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

internal static class CarnivaleRedRegression
{
    private static readonly DateTime Start = new(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(100, 0, 100);
    internal static void Run(Action<string, Action> run)
    {
        run("Carnivale Red both acts small arena primary lifecycle markers no hand priority", Lifecycle);
        run("Carnivale Red all ordinary circles true radius and 74 long forward ray observed Finish", Ordinary);
        run("Carnivale Red Pillar and Protean grouped FIFO any Finish exact thirty degrees source direction", Groups);
        run("Carnivale Red FluidSwing 50 forward locked facing missing target no cone no wall", Forward);
        run("Carnivale Red independent BigSplash25 and FluidSwing50 components strict immunity", Independent);
        run("Carnivale Red global Ferrofluid6 pull clamp and away overwrite finish separate effect clear", Ferrofluid);
        run("Carnivale Red Ferrofluid destination current global hazard no timeout no unknown source clear", FerrofluidHazard);
        run("Carnivale Red Pyretic fills64 cast requirements cancel persists any960 raw loss clears all", Pyretic);
        run("Carnivale Red Pyretic no Gain start death clears only current slot after text perspective", PyreticDeath);
        run("Carnivale Red Pyretic same frame Start and raw960 loss preserve actor order", PyreticOrder);
        run("Carnivale Red hand first identity dead fallback throttle occupied expired pure hints", Hints);
        run("Carnivale Red actual raidwide source clocks no repeated splash prediction", Damage);
        run("Carnivale Red late cast bootstrap canceled raw barrier no historical effect", Bootstrap);
    }

    private sealed class Fixture
    {
        internal readonly DungeonEncounter E = new(796);
        internal readonly List<DungeonActor> Actors;
        internal DateTime Now = Start;
        internal ulong Viewer = 100;
        internal Fixture(uint oid = 0x2C5D, bool combat = true)
        {
            Actors = [new(1, oid, Center, InCombat: combat), new(2, 0x233C, Center),
                new(100, 0, Center + Vector3.UnitZ * 5, Player: true, PartySlot: 0),
                new(101, 0, Center + Vector3.UnitX * 3, Player: true, PartySlot: 1)];
            Sync();
        }
        internal CarnivaleRedState State => E.CarnivaleRed!;
        internal void Set(ulong id, Func<DungeonActor, DungeonActor> fn)
        {
            var i = Actors.FindIndex(a => a.Id == id);
            Actors[i] = fn(Actors[i]);
        }
        internal void Sync() => E.Synchronize(Actors, Actors.FirstOrDefault(a => a.Id == Viewer)?.Position, Now, playerId: Viewer);
        internal void Cast(uint action, ulong source = 2, double seconds = 4, float rotation = 0)
        {
            Now = Now.AddSeconds(.01);
            if (!Actors.Any(a => a.Id == source)) Actors.Add(new(source, 0x233C, Center));
            Set(source, a => a with { Cast = new(action, Center, rotation, Now, Now.AddSeconds(seconds), 999) });
            Sync();
        }
        internal DungeonAmbientAoe[] Saps() => E.AmbientAoes().Where(a => a.Id.StartsWith("carnivale/sap/")).ToArray();
        internal DungeonActor Player => Actors.Single(a => a.Id == Viewer);
        internal string[] Hints() => E.Hints(Player.Position).ToArray();
    }

    private static void Lifecycle()
    {
        foreach (var oid in new uint[] { 0x2C5B, 0x2C5D })
        {
            var f = new Fixture(oid, false);
            Check(f.E.CarnivalePrecombatHint != null, "Preparation.");
            f.Actors.AddRange(new uint[] { 0x2C5C, 0x2C60, 0x2C5F, 0x2C5E }.Select((add, i) =>
                new DungeonActor((ulong)(10 + i), add, Center, IsEnemy: true, ObjectIndex: (ushort)(2 * i + 4))));
            f.Set(1, a => a with { InCombat = true });
            f.Sync();
            Check(f.E.AmbientAoes().Count(a => a.Id.StartsWith("carnivale/enemy/")) == (oid == 0x2C5B ? 1 : 3)
                && !f.E.Arena!.Contains(Center + Vector3.UnitX * 17) && f.E.PriorityTarget(0, f.Player.Position) == 0,
                "Marker/arena or fabricated hand priority.");
            f.Set(1, a => a with { Hp = 0 });
            f.Sync();
            Check(f.E.Active == null, "Adds kept module alive.");
        }
    }

    private static void Ordinary()
    {
        foreach (var (oid, action, radius) in new[] { (0x2C5Bu, 18694u, 6f), (0x2C5Bu, 18692u, 5f),
            (0x2C5Bu, 18698u, 10f), (0x2C5Du, 18969u, 5f), (0x2C5Du, 18985u, 10f) })
        {
            var f = new Fixture(oid);
            f.Cast(action);
            var cast = f.E.OutputCasts.Single().Value;
            Check(cast.Rule.Shape is CircleTemplate c && c.Radius == radius, "Ordinary source radius.");
            f.Now = Start.AddSeconds(30);
            f.Sync();
            f.E.Effect(2, action, 1, f.Now);
            Check(f.E.OutputCasts.Single().Value == cast, "Effect/timeout removed ordinary.");
            f.E.Finish(2, action, f.Now);
            Check(!f.E.OutputCasts.Any(), "Ordinary real Finish.");
        }
        var ray = new Fixture(0x2C5B);
        ray.Cast(18699, rotation: MathF.PI / 2);
        var value = ray.E.OutputCasts.Single().Value;
        Check(value.Rule.Shape is RayRectangleTemplate { MinLocalX: -2, MaxLocalX: 2, Length: 74 }
            && value.Rotation == MathF.PI / 2, "FireBlast ray.");
    }

    private static void Groups()
    {
        foreach (var (oid, first, second) in new[] { (0x2C5Bu, 18696u, 18895u), (0x2C5Du, 18971u, 18984u) })
        {
            var f = new Fixture(oid);
            f.Cast(first, seconds: 20, rotation: MathF.PI / 2);
            f.Cast(second, 3, 5);
            Check(f.Saps().Length == 2 && f.Saps()[0].Activation > f.Saps()[1].Activation, "Group order.");
            var geometry = f.Saps()[0].Shape.Resolve(Center, f.Saps()[0].Rotation, null);
            if (oid == 0x2C5D)
                Check(geometry.Contains(Center + Vector3.UnitX * 38, 0)
                    && !geometry.Contains(Center + new Vector3(20, 0, 10), 0)
                    && !geometry.Contains(Center + Vector3.UnitX * 40, 0), "Protean R39 total30/source rotation.");
            else Check(f.Saps().All(a => a.Shape is CircleTemplate { Radius: 8 }), "Pillar radius.");
            f.E.Finish(2, 60000, f.Now);
            Check(f.Saps().Length == 1, "Any-Finish group.");
        }
    }

    private static void Forward()
    {
        foreach (var (oid, action) in new[] { (0x2C5Bu, 18689u), (0x2C5Du, 18961u) })
        {
            var f = new Fixture(oid);
            f.Cast(action, rotation: MathF.PI / 2);
            var source = f.E.CarnivaleCastKnockbacks.Single();
            var move = f.E.CarnivaleCastKnockbackMovements(f.Player, action).Single();
            Check(source is { Distance: 50, Forward: true, StopAtWall: false, EffectShape: null }
                && Near(move.To, f.Player.Position + Vector3.UnitX * 50)
                && f.Hints().Contains("Interrupt!") && !f.E.OutputCasts.Any(c => c.Value.Rule.Shape != null),
                "Forward cast/type without target or cone.");
            f.Set(2, a => a with { Position = Center + Vector3.UnitX * 10, Rotation = 0 });
            f.Sync();
            Check(Near(f.E.CarnivaleCastKnockbackMovements(f.Player, action).Single().To, move.To), "Forward changed after lock.");
        }
    }

    private static void Independent()
    {
        var f = new Fixture();
        f.Cast(18961, rotation: MathF.PI / 2);
        f.Cast(18965, 3);
        var swing = f.E.CarnivaleCastKnockbackMovements(f.Player, 18961).Single();
        var splash = f.E.CarnivaleCastKnockbackMovements(f.Player, 18965).Single();
        Check(swing.From == f.Player.Position && splash.From == f.Player.Position
            && Near(splash.To, f.Player.Position + Vector3.UnitZ * 25), "Independent KB components.");
        f.E.StatusChanged(100, 160, true, f.Now, expiresAt: splash.Source.Cast.FinishAt);
        Check(f.E.CarnivaleCastKnockbackMovements(f.Player, 18965).Any(), "Equal expiry immunity.");
        f.E.StatusChanged(100, 160, true, f.Now, expiresAt: splash.Source.Cast.FinishAt.AddTicks(1));
        Check(!f.E.CarnivaleCastKnockbackMovements(f.Player, 18965).Any(), "Strict immunity.");
    }

    private static void Ferrofluid()
    {
        var f = new Fixture();
        f.Cast(18963);
        var first = f.State.FerrofluidKnockback!;
        Check(!first.Pull && Near(f.E.CarnivaleFerrofluidLanding(f.Player)!.Value, f.Player.Position + Vector3.UnitZ * 6), "Away6.");
        f.Cast(18962, 3);
        Check(f.State.FerrofluidKnockback!.Pull && f.E.CarnivaleFerrofluidLanding(f.Player) == Center, "Pull clamped center/no hitboxes.");
        f.E.Finish(2, 18963, f.Now);
        Check(f.State.FerrofluidKnockback == null && f.State.FerrofluidAoe is { Pull: true }, "Old source Finish should clear new global KB only.");
        f.E.Effect(2, 18974, 1, f.Now);
        Check(f.State.FerrofluidAoe == null, "Opposite effect clears global prediction.");
    }

    private static void FerrofluidHazard()
    {
        var f = new Fixture();
        f.Cast(18963);
        Check(f.Hints().Contains("About to be knocked into danger!"), "Away lands in current donut.");
        f.Now = Start.AddSeconds(30);
        f.Sync();
        f.E.Effect(999, 18974, 1, f.Now);
        Check(f.State.FerrofluidAoe != null, "Unknown source/timeout cleared.");
        f.E.Effect(2, 18973, 2, f.Now);
        Check(f.State.FerrofluidKnockback != null && !f.Hints().Contains("About to be knocked into danger!"), "Effect must leave KB but remove current landing hazard.");
    }

    private static void Pyretic()
    {
        var f = new Fixture(0x2C5B);
        f.Cast(18691);
        var requirement = f.State.Pyretic.Requirements[0]!;
        Check(f.State.Pyretic.Requirements.All(c => ReferenceEquals(c, requirement)) && f.Hints().Contains("Stop everything!"), "Fill64.");
        f.E.Finish(2, 18691, f.Now);
        Check(f.State.Pyretic.Requirements[0] != null && !f.Hints().Any(h => h.StartsWith("Pyretic,")), "Cancel must retain requirement, clear cast hint.");
        f.Set(101, a => a with { StatusSlots = [new(960, 0, 1, Start) { SlotIndex = 4 }] });
        f.Sync();
        Check(f.State.Pyretic.Requirements[0] != null, "Gain must not clear.");
        f.Set(101, a => a with { StatusSlots = [] });
        f.Sync();
        Check(f.State.Pyretic.Requirements.All(c => c == null), "Any actor960 Lose clears all64.");
    }

    private static void PyreticOrder()
    {
        foreach (var lossFirst in new[] { false, true })
        {
            var f = new Fixture(0x2C5B);
            f.Set(101, a => a with { StatusSlots = [new(960, 0, 1, Start)] });
            f.Sync();
            if (lossFirst)
            {
                var loss = f.Actors.Single(a => a.Id == 101);
                f.Actors.Remove(loss);
                f.Actors.Insert(1, loss);
            }
            f.Set(101, a => a with { StatusSlots = [] });
            f.Cast(18691);
            Check(f.State.Pyretic.Requirements.All(c => (c != null) == lossFirst), "Per-actor Start/Lose order.");
        }
    }

    private static void PyreticDeath()
    {
        var f = new Fixture(0x2C5B);
        f.Set(100, a => a with { StatusSlots = [new(960, 0, 1, Start)] });
        f.Sync();
        Check(!f.Hints().Contains("Stop everything!"), "Gain alone invented requirement.");
        f.Cast(18691);
        f.Set(100, a => a with { Dead = true });
        f.Sync();
        Check(f.Hints().Contains("Stop everything!") && f.State.Pyretic.Requirements[0] == null
            && f.State.Pyretic.Requirements[1] != null, "Death only viewer slot after hint.");
        f.Viewer = 101;
        f.Sync();
        Check(f.Hints().Contains("Stop everything!"), "Perspective lost other slot.");
    }

    private static void Hints()
    {
        var f = new Fixture();
        f.Actors.AddRange([new(3, 0x2C5F, Center, Dead: true, Name: "dead left"), new(4, 0x2C5F, Center, Name: "second left"),
            new(5, 0x2C5E, Center, Name: "right")]);
        f.Set(100, a => a with { StatusSlots = [new(700, 0, 1, Start)] });
        f.Sync();
        Check(f.Hints().Any(h => h.StartsWith("right will")) && !f.Hints().Any(h => h.Contains("second left"))
            && f.Hints().Contains("You were doomed! Cleanse it with Exuviation."), "First/dead fallback and expired occupied700.");
        f.E.Effect(2, 18964, 1, f.Now);
        Check(!f.Hints().Contains("Prepare to use Excuviation to remove debuff"), "Throttle effect invented cast hint.");
    }

    private static void Damage()
    {
        foreach (var action in new uint[] { 18965, 19022 })
        {
            var f = new Fixture();
            f.Cast(action);
            Check(f.E.PredictedDamage.Count() == 2 && !f.E.OutputCasts.Any(c => c.Value.Rule.Shape != null), "Actual raidwide only.");
            f.Set(2, a => a with { Cast = a.Cast! with { FinishAt = Start.AddSeconds(40) } });
            f.Sync();
            Check(f.E.PredictedDamage.All(d => d.Activation == Start.AddSeconds(40)), "Raidwide raw clock.");
        }
    }

    private static void Bootstrap()
    {
        var f = new Fixture(0x2C5B, false);
        f.Cast(18689, rotation: MathF.PI / 2);
        f.E.Finish(2, 18689, f.Now.AddSeconds(.1));
        f.Now = Start.AddSeconds(10);
        f.Set(1, a => a with { InCombat = true });
        f.Sync();
        Check(!f.E.CarnivaleCastKnockbacks.Any(), "Canceled pre-pull raw revived.");
        var late = new Fixture(0x2C5D, false);
        late.Cast(18962);
        late.Now = Start.AddSeconds(10);
        late.Set(1, a => a with { InCombat = true });
        late.Sync();
        Check(late.State.FerrofluidKnockback?.Cast.FinishAt == late.Now.AddSeconds((double).3f), "Late activation source capped clock.");
    }

    private static bool Near(Vector3 a, Vector3 b) => Vector3.Distance(a, b) < .001f;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
