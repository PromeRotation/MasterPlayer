using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using PromeRotation.LogSystem;
using Fight = HalataliRegression.Fight;

internal static class StrayboroughRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Strayborough native discovery three encounters and precise territory ownership", () =>
        {
            Check(ArrDungeonCatalog.Create(1204).Length == 3 && ArrDungeonCatalog.IsScript("masterplayer.dungeon.strayborough"), "Discovery.");
            foreach (var oid in new uint[] { 0x4183, 0x41CA, 0x421F })
                Check(ArrDungeonCatalog.Owns(oid, 1204) && !ArrDungeonCatalog.Owns(oid, 1203), "Ownership.");
        });
        run("Strayborough Jack exact clipped arena radar and world source polygon parity", () =>
        {
            using var f = Prepare(0x41CA);
            var type = typeof(BossMod.Dawntrail.Dungeon.D08StrayboroughDeadwalk.D082JackInThePot.D082JackInThePot);
            var bounds = (BossMod.ArenaBoundsCustom)type.GetField("arena", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
            Compare(f.Engine.Arena!, p => bounds.Contains(new(p.X - bounds.Center.X, p.Z - bounds.Center.Z)), 22, .31f);
        });
        run("Strayborough Leonogg shrink prediction finish plus 0.9 and map confirmation", () =>
        {
            using var f = Prepare(0x4183); var initial = f.Now; Cast(f, 1, 36529);
            Check(Near(Group(f, "shrink").Single().Activation, initial, 5.9), "Shrink clock.");
            f.Now = f.Now.AddSeconds(30); f.Engine.Tick(f.Now); Check(((CircleGeometry)f.Engine.Arena!).Radius == 19.5f, "Time shrank arena.");
            f.Engine.MapEffect(0, 0x20001, f.Now); f.Render();
            Check(((CircleGeometry)f.Engine.Arena!).Radius == 14 && Group(f, "shrink").Length == 0, "Map shrink.");
            Finish(f, 1); f.Now = f.Now.AddSeconds(.1); Cast(f, 1, 36529); Check(Group(f, "shrink").Length == 0, "Repeated shrink.");
        });
        run("Strayborough falling timeline locked raw position FIFO finish no effect timeout or destroy consumption", () =>
        {
            using var f = Prepare(0x4183); var c = f.Engine.Arena!.Position; var initial = f.Now;
            f.Actors[20] = new(20, 0x4205, c + new Vector3(1.123f, 0, 2.456f)); Scan(f);
            Cast(f, 20, 36532); Check(Group(f, "falling").Length == 0, "Cast invented falling.");
            f.Engine.Timeline(20, 0x11D1, f.Now); f.Engine.Timeline(20, 0x11D1, f.Now);
            var position = Group(f, "falling")[0].Position;
            f.Actors[20] = f.Actors[20] with { Position = c, Dead = true }; Scan(f);
            f.Engine.Effect(20, 36532, 1, f.Now); f.Now = f.Now.AddSeconds(30); f.Engine.Tick(f.Now);
            Check(Group(f, "falling").Length == 2 && Group(f, "falling")[0].Position == position
                && Near(Group(f, "falling")[0].Activation, initial, 3), "Snapshot or premature consumption.");
            Finish(f, 20); Check(Group(f, "falling").Length == 1, "Finish not FIFO.");
            f.Engine.RemoveActor(20); f.Actors.Remove(20); Scan(f); Check(Group(f, "falling").Length == 1, "Destroy consumed no-cast prediction.");
        });
        run("Strayborough charge effect duplicate dynamic six by two timeline first removal", () =>
        {
            using var f = Prepare(0x4183); var c = f.Engine.Arena!.Position;
            f.Actors[20] = new(20, 0x41BB, c); Scan(f);
            f.Engine.Effect(20, 36533, 1, f.Now); f.Engine.Effect(20, 36533, 2, f.Now);
            f.Actors[20] = f.Actors[20] with { Position = c + Vector3.UnitX * 3, Rotation = .7f, Dead = true }; Scan(f);
            var a = Group(f, "charge"); Check(a.Length == 2 && a.All(a => a.Position == f.Actors[20].Position && a.Activation == DateTime.MinValue), "Dynamic duplicate.");
            var source = new BossMod.AOEShapeRect(6, 1);
            Compare(a[0].Shape.Resolve(a[0].Position, a[0].Rotation, null), p => source.Check(new(p.X, p.Z), new(a[0].Position.X, a[0].Position.Z), new(.7f)), 10);
            f.Engine.Timeline(20, 0x1E3C, f.Now); Check(Group(f, "charge").Length == 1, "Timeline removed all duplicates.");
            f.Engine.RemoveActor(20); f.Actors.Remove(20); Scan(f); Check(Group(f, "charge").Length == 1, "Destroyed charge lost cached actor.");
        });
        run("Strayborough exaflare five snapshots delayed first all risky effect axis match and exact interval", () =>
        {
            using var f = Prepare(0x4183); var c = f.Engine.Arena!.Position; var initial = f.Now;
            Cast(f, 10, 39683, rotation: 0, location: c + Vector3.UnitX * 8);
            var aoes = Group(f, "exa"); Check(aoes.Length == 5 && aoes.All(a => a.Navigate)
                && aoes[0].Position == Q(c) && Near(aoes[0].Activation, initial, 6.6) && Near(aoes[4].Activation, initial, 12.6),
                $"Exa origin/time/risk: {c}, {string.Join(';', aoes.Select(a => $"{a.Position} {(a.Activation - initial)?.TotalSeconds} {a.Navigate}"))}.");
            Finish(f, 10); f.Now = f.Now.AddSeconds(40); f.Engine.Tick(f.Now);
            Check(Group(f, "exa")[4].Activation == aoes[4].Activation, "Time rebuilt cached future times.");
            f.Actors[10] = f.Actors[10] with { Position = c + new Vector3(.9f, 0, .9f) }; Scan(f);
            f.Engine.Effect(10, 39684, 1, f.Now);
            aoes = Group(f, "exa"); Check(aoes.Length == 4 && Near(aoes[0].Activation, f.Now, 1.5), "Axis match or received clock.");
            for (var i = 0; i < 4; ++i)
            {
                f.Actors[10] = f.Actors[10] with { Position = c + new Vector3(.9f, 0, 4.9f + i * 4) }; Scan(f);
                f.Engine.Effect(10, 39684, (uint)i + 2, f.Now);
            }
            Check(Group(f, "exa").Length == 0, "Exa not fully consumed.");
        });
        foreach (var (action, source, max) in new (uint, BossMod.AOEShape, int)[]
        {
            (36531, new BossMod.AOEShapeCone(20, new(MathF.PI / 6)), 4),
            (37139, new BossMod.AOEShapeRect(50, 8), 8), (37147, new BossMod.AOEShapeRect(50, 6), 8),
            (37340, new BossMod.AOEShapeRect(40, 2), 5), (37133, new BossMod.AOEShapeRect(40, 2), 8),
        })
            run($"Strayborough {action} exact dual geometry ordered display window all risky real finish", () =>
            {
                using var f = Prepare(action == 36531 ? 0x4183u : 0x421Fu);
                for (ulong i = 10; i < 18; ++i) Cast(f, i, action, duration: 30 - i, rotation: .8f);
                var a = Group(f, "simple"); Check(a.Length == max && a.All(a => a.Navigate), "Window or risk.");
                Compare(a[0].Shape.Resolve(a[0].Position, a[0].Rotation, null), p => source.Check(new(p.X, p.Z), new(a[0].Position.X, a[0].Position.Z), new(.8f)));
                if (action == 36531) Check(a.Take(2).All(a => a.Color != null) && a.Skip(2).All(a => a.Color == null), "Danger colors.");
                f.Now = f.Now.AddSeconds(90); f.Engine.Tick(f.Now); f.Engine.Effect(10, action, 1, f.Now);
                Check(Group(f, "simple").Length == max, "Time/effect consumed queue.");
                Finish(f, 10); Check(Group(f, "simple")[0].Id.Contains("/11/"), "Finish didn't advance FIFO.");
            });
        run("Strayborough chaser icon prehint first stationary then direction offset AI and actual effect clock", () =>
        {
            using var f = Prepare(0x4183); var c = f.Engine.Arena!.Position; f.Engine.Icon(100, 197, f.Now);
            Check(Group(f, "chase/edge").Length == 1 && Near(Group(f, "chase/edge")[0].Activation, f.Now, 5.1), "Icon edge.");
            Cast(f, 10, 39686, location: c);
            f.Actors[100] = f.Actors[100] with { Position = c + Vector3.UnitX * 10, Rotation = MathF.PI / 2 }; Scan(f);
            Check(Group(f, "chase/edge").Length == 0 && Group(f, "chaser").Single(a => a.Id.EndsWith("/display")).Position == Q(c), "First moved early.");
            var nav = Group(f, "chaser").Single(a => a.Id.EndsWith("/avoid"));
            Check(nav.Shape is CircleTemplate { Radius: 7 } && Vector3.Distance(nav.Position, Q(c) - Vector3.UnitX * 4) < .001, "Own offset.");
            Finish(f, 10); f.Now = f.Now.AddSeconds(7); f.Engine.Effect(10, 39686, 1, f.Now, target: 10); f.Render();
            var display = Group(f, "chaser").Single(a => a.Id.EndsWith("/display"));
            var origin = Q(c); var next = Q(origin + Vector3.Normalize(f.Actors[100].Position - origin) * 3);
            Check(display.Position == next && Near(display.Activation, f.Now, (double)1.6f), "Advance distance/time.");
            f.PlayerId = 101; Scan(f); Check(Group(f, "chaser").Single(a => a.Id.EndsWith("/avoid")).Shape is CircleTemplate { Radius: 5 }, "Other target radius.");
        });
        run("Strayborough chasing first two five casts later four persists across waves", () =>
        {
            using var f = Prepare(0x4183);
            for (var wave = 0; wave < 3; ++wave)
            {
                f.Now = f.Now.AddSeconds(1); f.Engine.Icon(100, 197, f.Now); Cast(f, 10, 39686);
                Finish(f, 10); var count = wave < 2 ? 5 : 4;
                for (var i = 0; i < count - 1; ++i) f.Engine.Effect(10, 39687, (uint)(wave * 10 + i), f.Now, target: 10);
                Check(Group(f, "chaser").Length > 0, "Chaser ended early.");
                f.Engine.Effect(10, 39687, (uint)(wave * 10 + 9), f.Now, target: 10); Check(Group(f, "chaser").Length == 0, "Chaser ended late.");
            }
        });
        run("Strayborough pour status extra one any actor snapshot persists until matching timeline", () =>
        {
            using var f = Prepare(0x41CA); var c = f.Engine.Arena!.Position;
            f.Actors[20] = new(20, 0x233C, c + new Vector3(.123f, 0, .456f)); Scan(f);
            f.Engine.StatusChanged(20, 1909, true, f.Now, 2); Check(Group(f, "pour").Length == 0, "Wrong extra.");
            f.Now = f.Now.AddSeconds(.1); f.Engine.StatusChanged(20, 1909, true, f.Now, 1);
            Check(Group(f, "pour").Single().Shape is CircleTemplate { Radius: 8 }, "Any actor status.");
            var p = Group(f, "pour")[0].Position; f.Actors[20] = f.Actors[20] with { Position = c + Vector3.UnitX * 8 }; Scan(f);
            f.Engine.StatusChanged(20, 1909, false, f.Now); f.Now = f.Now.AddSeconds(50); f.Engine.Tick(f.Now);
            Check(Group(f, "pour")[0].Position == p, "Lose/time moved or consumed.");
            f.Engine.Timeline(20, 0x11DD, f.Now); Check(Group(f, "pour").Length == 1, "Wrong OID consumed.");
            f.Actors[21] = new(21, 0x41CB, c); Scan(f); f.Engine.Timeline(21, 0x11DD, f.Now); Check(Group(f, "pour").Length == 0, "Right timeline didn't consume.");
        });
        foreach (var (state, p, q, a, b, delay) in TeaPatterns())
            run($"Strayborough tea {state:X8} {p} {q} exact source pattern and delay", () =>
            {
                using var f = Prepare(0x41CA); var y = f.Engine.Arena!.Position.Y;
                f.Actors[20] = new(20, 0x41D5, new(p.X, y, p.Y)); Scan(f); f.Engine.Tether(1, 276, true, f.Now, source: 20);
                if (q != null) { f.Actors[21] = new(21, 0x41D5, new(q.Value.X, y, q.Value.Y)); Scan(f); f.Engine.Tether(1, 276, true, f.Now, source: 21); }
                f.Engine.MapEffect(0x23, state, f.Now);
                var aoes = Group(f, "tea");
                Check(aoes.Length == (b == null ? 1 : 2) && aoes[0].Position == new Vector3(a.X, y, a.Y)
                    && Near(aoes[0].Activation, f.Now, delay), "Pattern/time.");
                if (b != null) Check(aoes[1].Position == new Vector3(b.Value.X, y, b.Value.Y), "Second output.");
                f.Engine.MapEffect(1, state, f.Now); Check(Group(f, "tea").Length == aoes.Length * 2, "Duplicate map overwritten.");
                Cast(f, 10, 36720); f.Engine.Effect(10, 36720, 1, f.Now); Check(Group(f, "tea").Length == aoes.Length * 2, "Effect consumed.");
                Finish(f, 10); Check(Group(f, "tea").Length == 0, "Real finish didn't clear.");
            });
        run("Strayborough tea moving cup exact equality no approximate anchor direct first two", () =>
        {
            using var f = Prepare(0x41CA); f.Actors[20] = new(20, 0x41D5, new(34, 0, -170)); Scan(f); f.Engine.Tether(1, 276, true, f.Now, source: 20);
            f.Actors[20] = f.Actors[20] with { Position = new(34.001f, 0, -170) }; Scan(f); f.Engine.MapEffect(1, 0x02000100, f.Now);
            Check(Group(f, "tea").Length == 0, "Approximate or tether-time matching.");
            f.Engine.Tether(1, 276, true, f.Now, source: 20); f.Engine.MapEffect(1, 0x00100001, f.Now);
            Check(Group(f, "tea").Length == 2 && Group(f, "tea").All(a => a.Position == f.Actors[20].Position && Near(a.Activation, f.Now, 16)), "Direct duplicate cups.");
        });
        run("Strayborough ghost cross arena changes with perspective and ghost status dual view parity", () =>
        {
            using var f = Prepare(0x421F); f.Actors[101] = f.Actors[101] with { Statuses = [3949] }; Scan(f);
            f.Engine.MapEffect(0x0B, 0x00800040, f.Now);
            var bounds = BossMod.Dawntrail.Dungeon.D08StrayboroughDeadwalk.D083Träumerei.D083Träumerei.CrossBounds;
            Compare(f.Engine.Arena!, p => bounds.Contains(new(p.X - bounds.Center.X, p.Z - bounds.Center.Z)), 23, .37f);
            Check(!f.Engine.Arena!.Contains(f.Engine.Arena.Position), "Flesh center walkable.");
            f.PlayerId = 101; Scan(f); Check(f.Engine.Arena!.Contains(f.Engine.Arena.Position), "Ghost perspective didn't restore cross.");
            f.PlayerId = 100; Scan(f); Check(!f.Engine.Arena!.Contains(f.Engine.Arena.Position), "Flesh perspective retained ghost bounds.");
            f.Engine.MapEffect(0x0B, 0x00080004, f.Now); Check(f.Engine.Arena!.Contains(f.Engine.Arena.Position), "Map clear.");
        });
        run("Strayborough ghost platforms spread precedence opposite hint hard union and dead revival", () =>
        {
            using var f = Prepare(0x421F); f.Engine.MapEffect(0x0D, 0x20001, f.Now); Check(Group(f, "platform").Length == 0, "Wrong map activated.");
            f.Engine.MapEffect(0x0C, 0x20001, f.Now); Check(Group(f, "platform").Length == 4 && Group(f, "platform").All(a => !a.Safe && a.Navigate), "Default platforms.");
            f.Actors[100] = f.Actors[100] with { Statuses = [3949] }; Scan(f);
            Cast(f, 10, 37146, target: 101); Cast(f, 1, 37148, 8);
            Check(Group(f, "spread").All(a => !a.Navigate) && Group(f, "platform/enter").Length == 1 && f.Engine.Hints(f.Actors[100].Position).Contains("变成幽灵"), "Spread display/priority.");
            var nav = Group(f, "platform/enter")[0].Shape.Resolve(f.Engine.Arena!.Position, 0, null);
            Check(nav.Contains(f.Engine.Arena.Position) && !nav.Contains(new(137.5f, 0, -443.5f)), "Hard union.");
            f.Actors[101] = f.Actors[101] with { Dead = true }; Scan(f);
            Check(Group(f, "spread").Length == 0 && Group(f, "platform/enter").Length == 0, "Dead spread didn't fall through to fleshbuster.");
            f.Actors[101] = f.Actors[101] with { Dead = false }; Scan(f); Check(Group(f, "platform/enter").Length == 1, "Revived cast spread lost.");
            Finish(f, 10); Check(Group(f, "spread").Length == 0 && Group(f, "platform/enter").Length == 0, "Finish not consume spread.");
            Finish(f, 1); Check(Group(f, "platform").Length == 4, "Activation should persist.");
        });
        run("Strayborough flesh spread nine radius excludes other marked targets no damage prediction", () =>
        {
            using var f = Prepare(0x421F); Cast(f, 10, 37146, target: 100); Cast(f, 11, 37146, target: 101);
            var a = Group(f, "spread"); Check(a.Count(a => a.Display) == 2 && a.Count(a => a.Id.Contains("/party/")) == 2
                && a.Where(a => a.Navigate).All(a => a.Shape is CircleTemplate { Radius: 9 }), "Spread nav candidates/radius.");
            Check(!f.Engine.PredictedDamage.Any(), "Cast spread invented damage.");
        });
        run("Strayborough stretch bidirectional good bad activation cache expiry ghost only nav and untether", () =>
        {
            using var f = Prepare(0x421F); var c = f.Engine.Arena!.Position; f.Actors[20] = new(20, 0x4221, c - Vector3.UnitZ * 5); Scan(f);
            f.Engine.Tether(100, 57, true, f.Now, source: 20); Check(Group(f, "stretch").Length == 1, "Flesh should draw only.");
            f.Actors[100] = f.Actors[100] with { Statuses = [3949] }; Scan(f);
            var at = Group(f, "stretch").Single(a => a.Navigate).Activation;
            Check(Near(at, f.Now, 10) && !f.Engine.PredictedDamage.Any(), "Stretch clock or false TB.");
            f.Now = f.Now.AddSeconds(5); f.Engine.Tether(100, 1, true, f.Now, source: 20);
            Check(Group(f, "stretch").Where(a => a.Navigate).All(a => a.Activation == at), "Good line reset activation.");
            f.Now = f.Now.AddSeconds(7); Scan(f); Check(Group(f, "stretch").Count(a => a.Navigate) == 2, "Cache expiry removed baits.");
            f.Engine.Tether(20, 57, true, f.Now, source: 100);
            Check(Group(f, "stretch").Count(a => a.Navigate) == 3 && Group(f, "stretch").Any(a => a.Navigate && Near(a.Activation, f.Now, 10)), "Reverse side or cache renewed.");
            f.Engine.Tether(100, 1, false, f.Now, source: 20); Check(Group(f, "stretch").Length == 0, "Untether didn't clear pair.");
        });
        run("Strayborough line stack marker live direction hard join shared prediction target resolve correction", () =>
        {
            using var f = Prepare(0x421F); var c = f.Engine.Arena!.Position;
            f.Actors[10] = new(10, 0x233C, c - Vector3.UnitZ * 8); Scan(f); f.Engine.Effect(10, 37144, 1, f.Now, target: 101);
            var display = Group(f, "stack").Single(a => a.Display);
            var source = new BossMod.AOEShapeRect(80, 4);
            Compare(display.Shape.Resolve(display.Position, display.Rotation, null), p => source.Check(new(p.X, p.Z), new(display.Position.X, display.Position.Z), new(display.Rotation)));
            Check(Group(f, "stack/join").Length == 1 && f.Engine.PredictedDamage.Count(d => d.DamageType == "Shared") == 4, "Join/shared.");
            f.PlayerId = 101; Scan(f); Check(Group(f, "stack").Any(a => a.Id.EndsWith("/gather")), "Target gather.");
            f.Engine.Effect(10, 37143, 2, f.Now, target: 102); Check(Group(f, "stack").Length == 0, "Single target correction didn't consume.");
        });
        run("Strayborough packet adapter pour status after actor birth and ghost status immediate arena update", () =>
        {
            using (var f = Prepare(0x41CA))
            {
                f.Actors[20] = new(20, 0x41CB, f.Engine.Arena!.Position); Scan(f);
                f.Events.Handle(new LogSystemStatusAddEvent { TargetId = 20, StatusId = 1909, Param = 1, Timestamp = f.Now }, f.Now);
                Check(Group(f, "pour").Length == 1, "Adapter dropped 1909.");
                Scan(f); Check(Group(f, "pour").Length == 1, "Scan duplicated packet.");
            }
            using (var f = Prepare(0x421F))
            {
                f.Engine.MapEffect(0x0B, 0x00800040, f.Now); var center = f.Engine.Arena!.Position;
                f.Events.Handle(new LogSystemStatusAddEvent { TargetId = 100, StatusId = 3949, Timestamp = f.Now }, f.Now);
                Check(f.Engine.Arena!.Contains(center), "Ghost packet not immediate.");
                f.Now = f.Now.AddSeconds(.1);
                f.Events.Handle(new LogSystemStatusRemoveEvent { TargetId = 100, StatusId = 3949, Timestamp = f.Now }, f.Now);
                Check(!f.Engine.Arena!.Contains(center), "Ghost loss not immediate.");
            }
        });
        run("Strayborough pour scan detects status acquired after initial statusless actor observation", () =>
        {
            using var f = Prepare(0x41CA); f.Actors[20] = new(20, 0x41CB, f.Engine.Arena!.Position); Scan(f);
            f.Actors[20] = f.Actors[20] with { Statuses = [1909], StatusParams = new Dictionary<uint, ushort> { [1909] = 1 } }; Scan(f);
            Check(Group(f, "pour").Length == 1, "Statusless first observation exhausted restore.");
            Scan(f); Check(Group(f, "pour").Length == 1, "Repeated scan duplicated.");
        });
        run("Strayborough event tether owner destroyed before snapshot synthesizes untether", () =>
        {
            using var f = Prepare(0x421F); f.Actors[20] = new(20, 0x4221, f.Engine.Arena!.Position - Vector3.UnitZ * 5); Scan(f);
            f.Actors[100] = f.Actors[100] with { Statuses = [3949] }; Scan(f);
            f.Events.Handle(new LogSystemTetherEvent { SourceId = 20, TargetId = 100, TetherId = 57, Timestamp = f.Now }, f.Now);
            Check(Group(f, "stretch").Length == 2, "Event tether missing.");
            f.Events.Handle(new LogSystemObjectRemoveEvent { SourceId = 20, Timestamp = f.Now }, f.Now);
            Check(Group(f, "stretch").Length == 0, "Event-owned tether survived owner destruction.");
        });
        run("Strayborough same action replacement finishes old caster before new start", () =>
        {
            using var f = Prepare(0x4183); Cast(f, 10, 36531);
            f.Now = f.Now.AddSeconds(7); Cast(f, 10, 36531);
            Check(Group(f, "simple").Length == 1 && Near(Group(f, "simple")[0].Activation, f.Now, 5), "Replacement retained old caster.");
            Finish(f, 10); Check(Group(f, "simple").Length == 0, "Replacement finish left orphan.");
        });
        run("Strayborough same action overlapping replacement and actor disappearance close cleanup gaps", () =>
        {
            using (var f = Prepare(0x4183))
            {
                Cast(f, 10, 36531); f.Now = f.Now.AddSeconds(.5); Cast(f, 10, 36531);
                Check(Group(f, "simple").Length == 1, "Overlap replacement retained old caster.");
                Finish(f, 10); Check(Group(f, "simple").Length == 0, "Overlap replacement orphan.");
            }
            using (var f = Prepare(0x421F))
            {
                f.Actors[20] = new(20, 0x4221, f.Engine.Arena!.Position - Vector3.UnitZ * 5); Scan(f);
                f.Engine.Tether(100, 57, true, f.Now, source: 20); f.Actors.Remove(20); Scan(f);
                Check(Group(f, "stretch").Length == 0, "Missing snapshot owner retained tether.");
                f.Engine.RemoveActor(20); Check(Group(f, "stretch").Length == 0, "Late removal restored tether.");
            }
        });
        run("Strayborough icon actor and target mask remain distinct", () =>
        {
            using var f = Prepare(0x4183); var c = f.Engine.Arena!.Position;
            f.Engine.Icon(100, 197, f.Now, actorId: 101); Check(Group(f, "chase/edge").Length == 1, "Target mask.");
            Cast(f, 10, 39686, location: f.Actors[101].Position);
            Check(Group(f, "chase/edge").Length == 1 && Group(f, "chaser").Single(a => a.Id.EndsWith("/avoid")).Shape is CircleTemplate { Radius: 5 },
                "Icon actor was replaced with mask target.");
        });
        run("Strayborough exaflare nonzero locked rotation preserved in native predictions", () =>
        {
            using var f = Prepare(0x4183); var c = f.Engine.Arena!.Position; Cast(f, 10, 39683, rotation: MathF.PI / 2);
            Check(Group(f, "exa")[1].Position == Q(c + Vector3.UnitX * 4), "Exa packet facing dropped.");
        });
        run("Strayborough unmatched exaflare effect cannot rebuild elapsed predictions", () =>
        {
            using var f = Prepare(0x4183); Cast(f, 10, 39683); Finish(f, 10);
            var at = Group(f, "exa")[4].Activation;
            f.Now = f.Now.AddSeconds(30); f.Actors[10] = f.Actors[10] with { Position = f.Actors[10].Position + Vector3.UnitX * 20 }; Scan(f);
            f.Engine.Effect(10, 39684, 1, f.Now); Check(Group(f, "exa")[4].Activation == at, "Unmatched effect refreshed cache.");
        });
        run("Strayborough platform polygons display navigation and inverted union match source", () =>
        {
            using var f = Prepare(0x421F); f.Engine.MapEffect(0x0C, 0x20001, f.Now);
            var type = typeof(BossMod.Dawntrail.Dungeon.D08StrayboroughDeadwalk.D083Träumerei.D083Träumerei).Assembly
                .GetType("BossMod.Dawntrail.Dungeon.D08StrayboroughDeadwalk.D083Träumerei.GhostlyGuise")!;
            var source = (BossMod.AOEShape)type.GetField("circlesAvoid", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
            var native = Group(f, "platform").Select(a => a.Shape.Resolve(a.Position, 0, null)).ToArray();
            for (var x = 128.17f; x < 168; x += .33f)
                for (var z = -452.13f; z < -413; z += .37f)
                {
                    var p = new Vector3(x, 0, z);
                    Check(native.Any(a => a.Contains(p, 0)) == source.Check(new(x, z), new(148, -433), default), "Platform polygon parity.");
                }
            foreach (var a in native) Compare(a, p => source.Check(new(p.X, p.Z), new(148, -433), default), 3.1f, .13f);
        });
        foreach (var boss in new uint[] { 0x4183, 0x41CA, 0x421F })
            run($"Strayborough {boss:X} temporary disengage retained death clears owner no invented restrictions", () =>
            {
                using var f = Prepare(boss); f.Actors[1] = f.Actors[1] with { InCombat = false, Targetable = false }; Scan(f);
                Check(f.Engine.Active?.BossOid == boss && f.Engine.ForbiddenTargets.Length == 0 && !f.Engine.Gazes.Any(), "Disengage or invented controls.");
                f.Actors[1] = f.Actors[1] with { Dead = true }; Scan(f);
                Check(f.Engine.Active == null && !f.Engine.AmbientAoes().Any() && f.Draw.Previews.Count == 0, "Death cleanup.");
            });
    }

    private static IEnumerable<(uint State, Vector2 P, Vector2? Q, Vector2 A, Vector2? B, double Delay)> TeaPatterns()
    {
        yield return (0x02000100, new(17, -163), new(17, -177), new(3.5f, -161.5f), new(30.5f, -178.5f), 11.5);
        yield return (0x02000100, new(17, -153), new(10, -170), new(25.5f, -156.5f), new(20.5f, -178.5f), 11.5);
        yield return (0x02000100, new(17, -153), new(17, -177), new(20.5f, -178.5f), new(3.5f, -161.5f), 11.5);
        yield return (0x02000100, new(34, -170), null, new(8.5f, -173.5f), null, 11.5);
        yield return (0x02000100, new(0, -170), null, new(25.5f, -166.5f), null, 11.5);
        yield return (0x10000800, new(0, -170), new(34, -170), new(8.5f, -156.5f), new(25.5f, -183.5f), 14.5);
        yield return (0x10000800, new(0, -170), new(17, -187), new(3.5f, -178.5f), new(8.5f, -156.5f), 14.5);
        yield return (0x10000800, new(17, -187), new(17, -153), new(30.5f, -161.5f), new(3.5f, -178.5f), 14.5);
        yield return (0x00400020, new(0, -170), new(17, -163), new(5, -165), new(22, -182), 19);
        yield return (0x00400020, new(17, -177), new(17, -153), new(5, -175), new(29, -175), 19);
    }
    private static Fight Prepare(uint boss)
    {
        var f = new Fight(boss, 1204); f.Engine.ResetFight();
        var c = f.Engine.Definitions.Single(d => d.BossOid == boss).Arena(Vector3.Zero).Position;
        f.Actors.Clear(); f.Actors[1] = new(1, boss, c, InCombat: true);
        for (var i = 0; i < 4; ++i) f.Actors[(ulong)(100 + i)] = new((ulong)(100 + i), 0, c + Vector3.UnitX * (i * 4), PartySlot: i, Hostile: false);
        Scan(f); return f;
    }
    private static void Scan(Fight f) { f.Engine.Synchronize(f.Actors.Values, f.Actors[100].Position, f.Now, playerId: f.PlayerId); f.Render(); }
    private static void Cast(Fight f, ulong id, uint action, double duration = 5, float rotation = .7f, Vector3? location = null, ulong target = 0)
    {
        if (!f.Actors.ContainsKey(id)) f.Actors[id] = new(id, 0x233C, f.Engine.Arena!.Position);
        var cast = new DungeonCastObservation(action, location ?? f.Actors[id].Position, rotation, f.Now, f.Now.AddSeconds(duration), target);
        f.Actors[id] = f.Actors[id] with { Cast = cast }; Scan(f); f.Engine.StartCast(id, cast, f.Now); f.Render();
    }
    private static void Finish(Fight f, ulong id) { f.Actors[id] = f.Actors[id] with { Cast = null }; f.Engine.Finish(id, endedAt: f.Now); f.Render(); }
    private static DungeonAmbientAoe[] Group(Fight f, string key) => f.Engine.AmbientAoes().Where(a => a.Id.StartsWith("stray/" + key)).ToArray();
    private static bool Near(DateTime? at, DateTime start, double seconds) => at is { } value && Math.Abs((value - start).TotalSeconds - seconds) < .00001;
    private static Vector3 Q(Vector3 p) { var q = new BossMod.WPos(p.X, p.Z).Quantized(); return new(q.X, p.Y, q.Z); }
    private static void Compare(IGeometryShape native, Func<Vector3, bool> source, float range = 55, float step = 1.17f)
    {
        var world = HostDrawingGeometry.Convert(native);
        for (var x = -range + .137f; x < range; x += step)
            for (var z = -range + .231f; z < range; z += step)
            {
                var p = native.Position + new Vector3(x, 0, z);
                Check(native.Contains(p, 0) == source(p) && world.Contains(p, 0) == source(p), $"Dual geometry at {p}.");
            }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
