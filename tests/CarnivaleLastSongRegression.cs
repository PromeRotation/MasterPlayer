using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

internal static class CarnivaleLastSongRegression
{
    private static readonly DateTime Start = new(2026, 11, 25, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(100, 0, 100);

    internal static void Run(Action<string, Action> run)
    {
        run("Carnivale last song two exact layouts and separate uninflated visibility contours", Layouts);
        run("Carnivale last song locks caster origin instead of spell location and retains observed lifetime", OriginLock);
        run("Carnivale last song death and start in one raw frame do not finish group prematurely", DeathStart);
        run("Carnivale last song independent hint bool becomes false on one finish while second AOE remains", HintBool);
        run("Carnivale last song cancelled raw no resurrection true replacement new generation and old finish guard", Generations);
        run("Carnivale last song shared action survives background module unload and display handoff", SharedAction);
        run("Carnivale last song activated background replays accepted cast at current actor position", Bootstrap);
        run("Carnivale last song missing observer hides outputs and preserves generation across restore", MissingObserver);
        run("Carnivale last song delayed first raw recovers inferred end explicit cancel remains final", DelayedRaw);
        run("Carnivale last song production adapter navigates visible danger only and cleans owner", Output);
    }

    private sealed class Fixture
    {
        internal readonly DungeonEncounter E = new(796);
        internal readonly List<DungeonActor> Actors;
        internal DateTime Now = Start;
        internal Fixture(uint oid = 0x271E)
        {
            Actors = [new(1, oid, new(100, 0, 112), InCombat: true), new(100, 0, Center, PartySlot: 0, Player: true)];
            Sync();
        }
        internal void Set(ulong id, Func<DungeonActor, DungeonActor> fn)
        {
            var i = Actors.FindIndex(a => a.Id == id);
            Actors[i] = fn(Actors[i]);
        }
        internal void Sync() => E.Synchronize(Actors, Center, Now, playerId: 100);
        internal void Raw(ulong id = 1, Vector3? location = null, bool dead = false)
        {
            Set(id, a => a with { Dead = dead, Cast = new(14756, location ?? Center, 0, Start, Start.AddSeconds(6)) });
            Sync();
        }
    }

    private static void Layouts()
    {
        Check(CarnivaleArenas.Layout2CornersSightContours.Select(c => c.Length).SequenceEqual(new[] { 64, 12, 8 })
            && CarnivaleArenas.LayoutBigQuadSightContours.Select(c => c.Length).SequenceEqual(new[] { 64, 4 }), "Resolved blocker topology.");
        foreach (var contours in new[] { CarnivaleArenas.Layout2CornersSightContours, CarnivaleArenas.LayoutBigQuadSightContours })
            foreach (var origin in new Vector3[]
            {
                new(100, 0, 112), new(82, 0, 90), new(90, 0, 100), new(100, 0, 82),
                new(118, 0, 105), new(85, 0, 110), new(100, 0, 125), new(100, 0, 100),
            })
            {
                try { DungeonPolygonVisibility.Create(Center, origin, contours); }
                catch (Exception e) { throw new InvalidOperationException($"Visibility origin {origin}, contours={contours.Length}", e); }
            }
        foreach (var oid in new uint[] { 0x271D, 0x271E })
        {
            var f = new Fixture(oid);
            Check(CarnivaleCatalog.Owns(796, oid) && !CarnivaleCatalog.Owns(795, oid)
                && f.E.Active!.Casts.Single().Action == 14756 && !f.E.PredictedDamage.Any(), "Source component scope.");
            Check(f.E.Arena!.GetContours().Count == (oid == 0x271D ? 3 : 2), "Arena holes.");
            f.E.StartCast(1, new(14757, Center, 0, Start, Start.AddSeconds(2)), Start);
            Check(f.E.Casts.Count == 0, "Syrup enumeration became component.");
        }
    }

    private static void OriginLock()
    {
        foreach (var oid in new uint[] { 0x271D, 0x271E })
        {
            var f = new Fixture(oid);
            f.E.StartCast(1, new(14756, Center - Vector3.UnitZ * 15, 0, Start, Start.AddSeconds(6),
                SourcePosition: new(82, 0, 90)), Start, fromPacket: true);
            f.Raw(location: Center - Vector3.UnitZ * 15);
            var song = f.E.CarnivaleLastSongs.Single();
            Check(song.Origin == new Vector3(100, 0, 112) && song.Cast.Location != song.Origin
                && song.Cast.StartedAt == Start && song.Cast.FinishAt == Start.AddSeconds(6), "Origin/clock.");
            f.Set(1, a => a with { Position = new(82, 0, 90), Rotation = 2 });
            f.Now = Start.AddSeconds(10);
            f.Sync();
            Check(ReferenceEquals(song.Visibility, f.E.CarnivaleLastSongs.Single().Visibility)
                && f.E.CarnivaleLastSongCasting, "Actor motion or deadline changed cached sight.");
            f.E.Effect(1, 14756, 1, f.Now);
            Check(f.E.CarnivaleLastSongs.Any(), "Effect cleared sight.");
            f.Set(1, a => a with { Cast = null });
            f.Sync();
            Check(!f.E.CarnivaleLastSongs.Any() && !f.E.CarnivaleLastSongCasting, "Observed end leaked sight.");
        }
    }

    private static void DeathStart()
    {
        foreach (var oid in new uint[] { 0x271D, 0x271E })
        {
            var f = new Fixture(oid);
            f.Raw(dead: true);
            Check(f.E.Active != null && f.E.CarnivaleLastSongs.Single().Caster.Actor.Dead
                && f.E.CarnivaleLastSongCasting, "Death+Start unloaded before raw observation.");
            f.Now = Start.AddSeconds(20);
            f.Sync();
            Check(f.E.Active != null, "Dead caster expired by prediction.");
            f.Set(1, a => a with { Cast = null });
            f.Sync();
            Check(f.E.Active == null && !f.E.AmbientAoes().Any(), "Finish did not complete dead group.");
        }
    }

    private static void HintBool()
    {
        var f = new Fixture();
        f.Actors.Add(new(2, 0x271E, new(90, 0, 110)));
        f.Raw();
        f.Raw(2);
        Check(f.E.CarnivaleLastSongCasting && f.E.CarnivaleLastSongs.Count() == 2
            && f.E.Hints(Center).Contains("Use the cube to take cover!"), "Two caster start.");
        f.E.Finish(1, 14756, Start.AddSeconds(1));
        Check(!f.E.CarnivaleLastSongCasting && f.E.CarnivaleLastSongs.Single().Cast.Source == 2
            && !f.E.Hints(Center).Contains("Use the cube to take cover!"), "Hint bool became caster count.");
        f.Sync();
        Check(!f.E.CarnivaleLastSongCasting && f.E.CarnivaleLastSongs.Count() == 1, "Stable raw reset hint bool.");
        f.Set(1, a => a with { Dead = true });
        f.Set(2, a => a with { Dead = true });
        f.Sync();
        Check(f.E.Active == null, "Source bool false with all dead did not finish despite remaining AOE.");
    }

    private static void Generations()
    {
        var f = new Fixture();
        f.Raw();
        var first = f.E.CarnivaleLastSongs.Single().Generation;
        f.E.Finish(1, 14756, Start.AddSeconds(.5));
        f.Sync();
        Check(!f.E.CarnivaleLastSongs.Any(), "Unchanged cancelled raw revived.");
        f.Now = Start.AddSeconds(1);
        f.Set(1, a => a with { Position = new(90, 0, 110) });
        f.Raw(location: Center + Vector3.UnitX * 2);
        var replacement = f.E.CarnivaleLastSongs.Single();
        Check(replacement.Generation > first && replacement.Origin == new Vector3(90, 0, 110), "True replacement identity/origin.");
        f.E.Finish(1, 14756, Start.AddSeconds(.5));
        f.E.Finish(1, 14757, f.Now);
        Check(f.E.CarnivaleLastSongs.Count() == 1 && f.E.CarnivaleLastSongCasting, "Old/foreign finish consumed new sight.");
        f.E.Finish(1, 14756, f.Now);
        Check(!f.E.CarnivaleLastSongs.Any(), "Current Finish leaked.");
    }

    private static void SharedAction()
    {
        var f = new Fixture(0x271D);
        f.Actors.Add(new(2, 0x271E, new(100, 0, 112), InCombat: true));
        f.Actors.Add(new(3, 0x271E, new(90, 0, 110)));
        f.Raw(2);
        f.Raw(3);
        Check(f.E.CarnivaleLastSongs.Count() == 2, "Both source components observe global cast.");
        f.Set(1, a => a with { Dead = true });
        f.E.Finish(3, 14756, Start.AddSeconds(.5));
        f.Sync();
        Check(f.E.Active?.BossOid == 0x271E && f.E.Casts.Single().Value.Source == 2
            && f.E.CarnivaleLastSongs.Single().Cast.Source == 2, "First group unload removed shared cast or left a display gap.");
        f.Now = Start.AddSeconds(1);
        f.Sync();
        Check(f.E.Active?.BossOid == 0x271E && f.E.CarnivaleLastSongs.Single().Cast.Source == 2
            && !f.E.CarnivaleLastSongCasting, "Shared action background handoff.");
    }

    private static void Bootstrap()
    {
        foreach (var cancelled in new[] { false, true })
        {
            var f = new Fixture(0x271D);
            f.Actors.Add(new(2, 0x271E, Center));
            f.Raw();
            if (cancelled) f.E.Finish(1, 14756, Start.AddSeconds(.5));
            f.Set(1, a => a with { Position = new(82, 0, 90) });
            f.Set(2, a => a with { InCombat = true });
            f.Now = Start.AddSeconds(1);
            f.Sync();
            var modules = (System.Collections.IEnumerable)typeof(DungeonEncounter)
                .GetField("carnivaleLoaded", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(f.E)!;
            var background = modules.Cast<CarnivaleModuleState>().Single(m => m.Primary.Actor.Oid == 0x271E);
            Check(background.Started && background.LastSongs.Count == (cancelled ? 0 : 1), "Activation lost cast or revived cancelled raw.");
            if (!cancelled)
                Check(background.LastSongs.Single().Origin == new Vector3(82, 0, 90)
                    && f.E.CarnivaleLastSongs.Single().Origin == new Vector3(100, 0, 112), "Activation copied previous module lock.");
        }
        var sameFrame = new Fixture(0x271D);
        // Enumerate the new caster before the ending caster to expose premature replay.
        sameFrame.Actors.Insert(0, new(3, 0x271D, new(90, 0, 110)));
        sameFrame.Actors.Add(new(2, 0x271E, Center));
        sameFrame.Raw();
        sameFrame.Set(1, a => a with { Cast = null });
        sameFrame.Set(2, a => a with { InCombat = true });
        sameFrame.Now = Start.AddSeconds(1);
        sameFrame.Raw(3);
        var modulesNow = (System.Collections.IEnumerable)typeof(DungeonEncounter)
            .GetField("carnivaleLoaded", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(sameFrame.E)!;
        var activated = modulesNow.Cast<CarnivaleModuleState>().Single(m => m.Primary.Actor.Oid == 0x271E);
        Check(activated.LastSongCasting && activated.LastSongs.Single().Cast.Source == 3
            && !sameFrame.E.CarnivaleLastSongCasting, "Activation replayed ended cast or rebuilt existing component bool.");
    }

    private static void MissingObserver()
    {
        var f = new Fixture();
        f.Raw();
        f.Actors.RemoveAll(a => a.Id == 100);
        f.E.Synchronize(f.Actors, null, Start.AddSeconds(1), playerId: 100);
        f.Set(1, a => a with { Cast = null });
        f.Actors.Add(new(100, 0, Center, Player: true, PartySlot: 0));
        f.Now = Start.AddSeconds(2);
        f.Sync();
        Check(!f.E.CarnivaleLastSongs.Any() && !f.E.CarnivaleLastSongCasting, "Observer reset retained sight.");
        f.Now = Start.AddSeconds(2.1);
        f.Set(1, a => a with { Cast = new(14756, Center, 0, f.Now, f.Now.AddSeconds(6)) });
        f.Sync();
        Check(f.E.CarnivaleLastSongs.Count() == 1, "Observer restore could not accept new cast.");
        f.E.Finish(1, 14756, f.Now.AddSeconds(.1));
        f.Actors.RemoveAll(a => a.Id == 100);
        f.E.Synchronize(f.Actors, null, f.Now.AddSeconds(.2), playerId: 100);
        f.Actors.Add(new(100, 0, Center, Player: true, PartySlot: 0));
        f.Now = f.Now.AddSeconds(.3);
        f.Sync();
        Check(!f.E.CarnivaleLastSongs.Any(), "Observer absence discarded cancellation receipt.");
        var during = new Fixture();
        during.Actors.Add(new(2, 0x271E, new(90, 0, 110)));
        during.Raw();
        during.Actors.RemoveAll(a => a.Id == 100);
        during.Set(2, a => a with { Cast = new(14756, Center, 0, Start.AddSeconds(1), Start.AddSeconds(7)) });
        during.E.Synchronize(during.Actors, null, Start.AddSeconds(1), playerId: 100);
        during.Set(2, a => a with { Cast = null });
        during.E.Synchronize(during.Actors, null, Start.AddSeconds(1.5), playerId: 100);
        during.Set(1, a => a with { Dead = true });
        during.Set(2, a => a with { Dead = true });
        during.Actors.Add(new(100, 0, Center, Player: true, PartySlot: 0));
        during.Now = Start.AddSeconds(2);
        during.Sync();
        Check(during.E.Active == null && !during.E.CarnivaleLastSongCasting, "Missing observer skipped another caster's full cast lifetime.");
    }

    private static void DelayedRaw()
    {
        foreach (var explicitCancel in new[] { false, true })
        {
            var f = new Fixture();
            f.E.StartCast(1, new(14756, Center, 0, Start, Start.AddSeconds(6)), Start);
            f.Now = Start.AddSeconds(.25);
            f.Sync();
            Check(!f.E.CarnivaleLastSongs.Any(), "Missing raw grace failed.");
            if (explicitCancel) f.E.Finish(1, 14756, Start.AddSeconds(.27));
            f.Now = Start.AddSeconds(.3);
            f.Raw();
            Check(f.E.CarnivaleLastSongs.Count() == (explicitCancel ? 0 : 1), "Delayed first raw lost or explicit cancel revived.");
            f.Sync();
            Check(f.E.CarnivaleLastSongs.Count() == (explicitCancel ? 0 : 1), "Repeated raw changed inferred/real end.");
            if (!explicitCancel)
            {
                f.E.Finish(1, 14756, Start.AddSeconds(.27));
                Check(!f.E.CarnivaleLastSongs.Any(), "Delayed same-generation cancel rejected after recovery.");
            }
        }
    }

    private static void Output()
    {
        var f = new Fixture();
        f.Raw();
        var song = f.E.CarnivaleLastSongs.Single();
        Check(song.Visibility.Contains(new(100, 0, 120), 0) && !song.Visibility.Contains(new(100, 0, 90), 0),
            "Visible danger vs cube shadow.");
        using var safe = new ScriptSafeZoneApi { UtcNowForTest = Start, DangerMarginForTest = 0 };
        var output = new DungeonOutput(safe, new ScriptDrawApi(), safe.Manager);
        output.Apply(f.E, Start, false);
        var snapshot = safe.Manager.BuildSnapshot(Start);
        Check(snapshot.DangerAreas.Count == 1 && snapshot.GoalAreas.Count == 0
            && f.E.AmbientAoes().Single(a => a.Id.StartsWith("carnivale/last-song/")).Activation == Start.AddSeconds(6),
            "Visibility became only display or invented safe goal.");
        output.Clear();
        Check(safe.Manager.BuildSnapshot(Start).DangerAreas.Count == 0 && safe.Manager.BuildSnapshot(Start).ArenaBoundary == null,
            "Visibility owner cleanup.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
