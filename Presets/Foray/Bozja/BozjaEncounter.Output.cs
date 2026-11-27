using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed partial class BozjaEncounter
{
    private List<BozjaActor> Pheromones => pheromones ??= Actors.Values.Where(a =>
        a.Actor.Oid == Definition.Rules.Single(r => r.Kind == BozjaRuleKind.Pheromones).EnemyOid).ToList();

    internal IEnumerable<BozjaAoe> Visible(BozjaActor? viewer = null)
    {
        foreach (var cast in ConstructionCasts) yield return new(cast, false, true);
        if (!Active) yield break;
        foreach (var rule in ActiveRules)
        {
            if (!Cll1OutputEnabled(rule) || !Cll4OutputEnabled(rule)) continue;
            foreach (var aoe in LightAoes(rule)) yield return aoe;
            foreach (var aoe in HeavyAoes(rule)) yield return aoe;
            foreach (var aoe in Cll1Aoes(rule)) yield return aoe;
            foreach (var aoe in Cll4Aoes(rule)) yield return aoe;
            foreach (var aoe in ArtAoes(rule)) yield return aoe;
            if (viewer != null)
                foreach (var aoe in VirtueAoes(viewer, rule)) yield return aoe;
            if (rule.Kind == BozjaRuleKind.OwainElemental)
                foreach (var aoe in OwainElements) yield return aoe;
            if (rule.Kind is BozjaRuleKind.RaidenArena or BozjaRuleKind.RaidenLancing)
                foreach (var cast in Casts.Where(c => c.Rule == rule)) yield return new(cast, false, true);
            if (rule.Kind == BozjaRuleKind.Cll3Arena)
                foreach (var cast in Casts.Where(c => c.Rule == rule)) yield return new(cast, false, true);
            foreach (var aoe in QueenAoes(viewer, rule)) yield return aoe;
            foreach (var aoe in BurnAoes(rule)) yield return aoe;
            foreach (var aoe in GraveAoes(rule)) yield return aoe;
            foreach (var aoe in RobotAoes(viewer, rule)) yield return aoe;
            foreach (var aoe in CuchAoes(viewer, rule)) yield return aoe;
            foreach (var aoe in SaunionAoes(viewer, rule)) yield return aoe;
            foreach (var aoe in DiabloAoes(rule)) yield return aoe;
            foreach (var aoe in GauntletAoes(rule)) yield return aoe;
            if (IsDiablo && rule.Component is "Explosion" or "PillarOfShamash") continue;
            if (rule.Kind == BozjaRuleKind.ColossusRotation)
                foreach (var aoe in colossusPublished) yield return aoe;
            if (rule.Kind is BozjaRuleKind.GuardArena or BozjaRuleKind.GuardAboveBoard or BozjaRuleKind.GuardTurrets)
                foreach (var cast in Casts.Where(c => c.Rule == rule)) yield return new(cast, false, true);
            if (rule.Kind == BozjaRuleKind.SeekerMercy)
                foreach (var aoe in SeekerMercies.Take(2)) yield return aoe;
            if (rule.Kind is BozjaRuleKind.SeekerArena or BozjaRuleKind.SeekerBlade or BozjaRuleKind.SeekerSplitter)
                foreach (var cast in Casts.Where(c => c.Rule == rule)) yield return new(cast, false, true);
            if (rule.Kind == BozjaRuleKind.PhantomCreeping)
                foreach (var cast in Casts.Where(c => c.Rule == rule).Take(2)) yield return new(cast, false, true);
            if (rule.Kind == BozjaRuleKind.PhantomArena)
                foreach (var cast in Casts.Where(c => c.Rule == rule)) yield return new(cast, false);
            if (rule.Kind == BozjaRuleKind.PhantomExaflare)
                foreach (var aoe in HashmalAoes(rule)) yield return aoe;
            if (rule.Kind is BozjaRuleKind.DahuShockwave or BozjaRuleKind.DahuRotation)
                foreach (var aoe in DahuAoes(rule)) yield return aoe;
            if (rule.Kind is BozjaRuleKind.LunarCry or BozjaRuleKind.ThermalGust or BozjaRuleKind.EndlessFrost)
                foreach (var aoe in FrostAoes(rule)) yield return aoe;
            if (rule.Kind is BozjaRuleKind.LightningClap or BozjaRuleKind.Burn)
                foreach (var aoe in AidaAoes(rule)) yield return aoe;
            if (rule.Kind is BozjaRuleKind.ControlTower or BozjaRuleKind.Exaflare or BozjaRuleKind.Hammer)
                foreach (var aoe in HashmalAoes(rule)) yield return aoe;
            if (rule.Kind is BozjaRuleKind.CrystalSight or BozjaRuleKind.Earthbreaker)
                foreach (var aoe in CrystalAoes(rule)) yield return aoe;
            if (rule.Kind == BozjaRuleKind.Time)
                foreach (var aoe in TimeAoes(rule)) yield return aoe;
            if (rule.Kind == BozjaRuleKind.Countdown)
                foreach (var cast in PublishedCountdowns) yield return new(cast, false, true);
            if (rule.Kind == BozjaRuleKind.TooHot)
                foreach (var cast in FireHotViews()) yield return new(cast, false, true);
            if (rule.Kind == BozjaRuleKind.Meteor && Meteors.Count > 0) yield return new(Meteors[0], false, true);
            if (rule.Kind == BozjaRuleKind.Juice)
                foreach (var cast in JuiceAoes(rule)) yield return new(cast, false, true);
            if (rule.Kind == BozjaRuleKind.MagitekBitLasers)
                foreach (var aoe in BitLasers(rule)) yield return aoe;
            if (rule.Kind == BozjaRuleKind.Glimmer)
                foreach (var burst in Bursts.Where(b => b.Cast.Rule == rule)) yield return new(burst.Cast, false, true);
            if (rule.Kind == BozjaRuleKind.Pheromones)
                foreach (var actor in Pheromones)
                    yield return new(new(-actor.BirthOrder, rule, actor,
                        actor.Actor.Position, actor.Actor.Rotation, default), false, true);
            if (rule.Kind == BozjaRuleKind.DeadLeaves)
            {
                var action = LeavesAction(viewer);
                foreach (var cast in Casts.Where(c => c.Rule == rule && c.Action == action))
                    yield return new(cast, false, true);
            }
            if (rule.Kind is not (BozjaRuleKind.Aoe or BozjaRuleKind.Charge or BozjaRuleKind.Hellpounce or BozjaRuleKind.VoidQuake or BozjaRuleKind.ArenaChange or BozjaRuleKind.ArenaOnFinish or BozjaRuleKind.ElectrochemicalReaction or BozjaRuleKind.Thunderbolt or BozjaRuleKind.Stormcall or BozjaRuleKind.Foreshadowing)) continue;
            var list = Casts.Where(c => c.Rule == rule).ToArray();
            for (var i = 0; i < list.Length; ++i)
            {
                if (i >= rule.MaxCasts || !Cll1TimewindowVisible(rule, list, i)) break;
                if (rule.Kind == BozjaRuleKind.VoidQuake && i != 0) break;
                if (rule.Kind == BozjaRuleKind.Stormcall && i >= 2) break;
                if (rule.Kind == BozjaRuleKind.Foreshadowing && list.Length == 5 && i != 0) break;
                var risky = !rule.Safe && (rule.MaxRisky == null || i < rule.MaxRisky);
                if (IsCll3 && rule.Kind == BozjaRuleKind.Charge) risky = list[i].Activation.AddSeconds(-1d) <= Now;
                yield return new(list[i], rule.MaxDangerColor is { } max && list.Length > max && i < max, risky);
            }
        }
    }

    internal BozjaCast[] Towers => Active ? Casts.Where(c => c.Rule.Kind == BozjaRuleKind.Tower).ToArray() : [];

    internal HashSet<BozjaActor> Soakers(BozjaCast tower)
        => tower.AllowedSoakers ??= Actors.Values.Where(a => a.Actor.Oid == 0).ToHashSet();

    internal int NumInside(BozjaCast tower)
        => Soakers(tower).Count(a => Inside(tower, a));

    internal static bool Inside(BozjaCast tower, BozjaActor actor)
        => DistanceSquared(tower.Origin, actor.Actor.Position) <= TowerRadius(tower) * TowerRadius(tower);

    internal static float TowerRadius(BozjaCast tower) => ((CircleTemplate)tower.Shape).Radius;

    internal IEnumerable<BozjaTowerView> TowerViews(BozjaActor viewer)
    {
        foreach (var tower in Towers)
        {
            if (VirtueInvalidTower(tower.Rule))
            {
                // Source's background short-circuits before counting for a
                // forbidden viewer. Its foreground has no valid outline.
                yield return new(tower, !Soakers(tower).Contains(viewer), false, false);
                continue;
            }
            var allowed = Soakers(tower).Contains(viewer);
            var inside = Inside(tower, viewer);
            var count = NumInside(tower);
            var safe = count < 1 || inside && count <= 1;
            yield return new(tower, !allowed || !inside && count >= 1,
                allowed && (safe || inside && count > 1), safe);
        }
    }

    internal IEnumerable<BozjaHint> Hints(BozjaActor viewer, bool consumeOzmaState = true)
    {
        if (ConstructionCasts.Any(c => c.Contains(viewer.Actor.Position))) yield return new("GTFO from aoe!");
        if (!Active) yield break;
        if (IsRobots && RobotHintError != null) yield break;
        if (IsFire)
        {
            _ = FireHotViews().ToArray();
            if (fireMissingMater) yield break;
        }
        foreach (var rule in ActiveRules)
        {
            if (!Cll1OutputEnabled(rule) || !Cll4OutputEnabled(rule)) continue;
            foreach (var hint in Cll1Hints(viewer, rule)) yield return hint;
            foreach (var hint in Cll3Hints(viewer, rule)) yield return hint;
            foreach (var hint in OzmaHints(viewer, rule, consumeOzmaState)) yield return hint;
            foreach (var hint in DeathgazeHints(viewer, rule)) yield return hint;
            foreach (var hint in ArtHints(viewer, rule)) yield return hint;
            foreach (var hint in OwainHints(viewer, rule)) yield return hint;
            foreach (var hint in RaidenHints(viewer, rule)) yield return hint;
            foreach (var hint in CircleBaitHints(viewer, rule)) yield return hint;
            foreach (var hint in VirtueHints(viewer, rule)) yield return hint;
            if (rule.Kind == BozjaRuleKind.AllianceKnockback && !rule.KnockbackStopAtWall
                && AllianceSimpleMovements(viewer, rule).Any(m => !Arena.Contains(m.To, 0)))
                yield return new("About to be knocked into danger!");
            if (rule.Kind == BozjaRuleKind.AlliancePull && AlliancePullMovements(viewer).Any(m => AlliancePullUnsafe(m.To)))
                yield return new("About to be knocked into danger!");
            if (rule.Kind == BozjaRuleKind.Cll4Wind && Cll4Movements(viewer).Any(m => !Arena.Contains(m.To, 0)))
                yield return new("About to be knocked into danger!");
            foreach (var hint in DahuHints(viewer, rule)) yield return hint;
            foreach (var hint in PhantomHints(viewer, rule)) yield return hint;
            foreach (var hint in SavageDahuHints(viewer, rule)) yield return hint;
            foreach (var hint in SeekerHints(viewer, rule)) yield return hint;
            foreach (var hint in QueenHints(viewer, rule)) yield return hint;
            foreach (var hint in RobotHints(viewer, rule)) yield return hint;
            foreach (var hint in CuchHints(viewer, rule)) yield return hint;
            foreach (var hint in SaunionHints(viewer, rule)) yield return hint;
            foreach (var hint in DiabloHints(viewer, rule)) yield return hint;
            foreach (var hint in GauntletHints(viewer, rule)) yield return hint;
            if (rule.Kind == BozjaRuleKind.GallowsMarch && GraveMarchUnsafe(viewer))
                yield return new("Aim for safe spot!");
            if (rule.Kind == BozjaRuleKind.BurnWeakpoint && BurnEyes(viewer).Any(e => EyeDanger(e, viewer.Actor)))
                yield return new("Face open weakpoint to eye!");
            if (rule.Kind == BozjaRuleKind.GuardParry)
                foreach (var hint in GuardHints(viewer)) yield return hint;
            var list = Casts.Where(c => c.Rule == rule).ToArray();
            if (rule.Kind is not (BozjaRuleKind.SaunionBait or BozjaRuleKind.CuchFlesh or BozjaRuleKind.RobotOrder or BozjaRuleKind.QueenChess or BozjaRuleKind.Meteor or BozjaRuleKind.CrystalSight or BozjaRuleKind.LunarCry or BozjaRuleKind.SeekerBlade)
                && !(IsCll1 && rule.Component == "WildCharges")
                && Visible(viewer).Any(a => a.Cast.Rule == rule && a.Risky && a.Cast.Contains(viewer.Actor.Position)))
                yield return new(rule.Kind == BozjaRuleKind.ArtChase ? "GTFO from chasing aoe!"
                    : rule.Kind == BozjaRuleKind.DeadLeaves ? "Go to different color!"
                    : rule.Kind is BozjaRuleKind.Exaflare or BozjaRuleKind.PhantomExaflare or BozjaRuleKind.DiabloNox ? "GTFO from exaflare!"
                    : IsLightArtillery && rule.Component == "FireVoidzone" || rule.Kind is BozjaRuleKind.Pheromones or BozjaRuleKind.Juice or BozjaRuleKind.ShockSphere ? "GTFO from voidzone!" : "GTFO from aoe!");
            if (rule.Kind == BozjaRuleKind.Meteor && Meteors.Count > 0 && meteorOrigin is { } origin
                && !Meteors[0].Contains(viewer.Actor.Position) && DistanceSquared(viewer.Actor.Position, origin) < 900)
                yield return new("Hide behind obstacle!");
            if (rule.Kind == BozjaRuleKind.CrystalSight && crystalSights.TryGetValue(rule, out var sight)
                && sight.Safezones.FirstOrDefault() is { } safe && sight.Origin is { } sightOrigin
                && !safe.Contains(viewer.Actor.Position) && DistanceSquared(viewer.Actor.Position, sightOrigin) < (IsIrminsul ? 4900 : 3600))
                yield return new("Hide behind obstacle!");
            if (rule.Kind == BozjaRuleKind.LunarCry && lunarSight.Safezones.FirstOrDefault() is { } lunarSafe
                && lunarSight.Origin is { } lunarOrigin && !lunarSafe.Contains(viewer.Actor.Position)
                && DistanceSquared(viewer.Actor.Position, lunarOrigin) < 6400)
                yield return new("Hide behind obstacle!");
            if (rule.Kind == BozjaRuleKind.BracingWind && BracingMovements(viewer).Any(m => !Arena.Contains(m.To, 0)))
                yield return new("About to be knocked into danger!");
            if (rule.Kind == BozjaRuleKind.CrystalSpread && IsCrystalSpreadTarget(viewer))
                yield return new("Spread, avoid intersecting cage hitboxes!");
            if (rule.Kind == BozjaRuleKind.Spread)
                foreach (var hint in SpreadHints(viewer, rule)) yield return hint;
            if (rule.Kind == BozjaRuleKind.Hailfire)
            {
                var active = Hailfires.Where(b => !b.Source.Actor.Dead).ToArray();
                if (active.Any(b => b.Target.Actor.Id == viewer.Actor.Id
                    && Party.Any(p => p.EntityId != b.Target.Actor.Id
                        && Actors.GetValueOrDefault(p.EntityId) is { Actor.Dead: false } member
                        && HornView(b).Contains(member.Actor.Position))))
                    yield return new("Bait away from raid!");
                if (active.Any(b => b.Target.Actor.Id != viewer.Actor.Id && HornView(b).Contains(viewer.Actor.Position)))
                    yield return new("GTFO from baited aoe!");
            }
            if (rule.Component == "GraspingRancor" && list.Length > 0 && Hand(viewer) is { } hand)
            {
                var shouldFreeze = DungeonSourceGeometry.Contains(rule.Shape!, list[0].Origin, 0, hand.Actor.Position);
                var frozen = hand.Tether?.Id == 2;
                yield return new(shouldFreeze ? "Face the hand!" : "Look away from hand and kite into safezone!",
                    shouldFreeze != frozen);
            }
            if (rule.Kind == BozjaRuleKind.Stack)
                foreach (var hint in StackHints(viewer, rule)) yield return hint;
            if (rule.Kind == BozjaRuleKind.BombStack)
            {
                if (list.Any(s => DistanceSquared(s.Target!.Actor.Position, StackCenter(s)) <= 36))
                    yield return new("Touch bomb when ready to resolve stack!");
                else
                    foreach (var hint in StackHints(viewer, rule)) yield return hint;
            }
            if (rule.Kind == BozjaRuleKind.Cleave && FireCleaves().Any(c => !ReferenceEquals(c.Target, viewer)
                && c.Contains(DungeonEncounter.QuantizedDungeonPosition(viewer.Actor.Position))))
                yield return new("GTFO from cleave!");
            if (rule.Kind == BozjaRuleKind.LightningRod)
                foreach (var hint in MineHints(viewer)) yield return hint;
            if (rule.Kind == BozjaRuleKind.Rush)
                foreach (var hint in RushHints(viewer)) yield return hint;
            if (rule.Kind == BozjaRuleKind.Horn)
                foreach (var hint in HornHints(viewer)) yield return hint;
            if (rule.Kind == BozjaRuleKind.ChargeTether)
                foreach (var hint in SteelHints(viewer)) yield return hint;
            if (rule.Kind is BozjaRuleKind.ForwardKnockback or BozjaRuleKind.SideKnockback
                && CavalryMovements(viewer, rule).Any(m => !Arena.Contains(m.To, 0)))
                yield return new("About to be knocked into danger!");
            if (rule.Kind is BozjaRuleKind.Gaze or BozjaRuleKind.CastGaze or BozjaRuleKind.SeekerMoon && Eyes.FirstOrDefault(e =>
                DistanceSquared(e.Position, viewer.Actor.Position) <= e.Range * e.Range && EyeDanger(e, viewer.Actor)) is { } eye)
                yield return new(eye.Inverted ? "Face the eye!" : "Turn away from gaze!");
            if (rule.Kind == BozjaRuleKind.Wind && WindMovements(viewer).Any(m => !Arena.Contains(m.To, 0)))
                yield return new("About to be knocked into danger!");
            if (!IsFire && !rule.GlobalHint && list.Length > 0 && rule.Kind == BozjaRuleKind.Raidwide) yield return new(rule.Hint ?? "Raidwide", false);
            if (!rule.GlobalHint && list.Length > 0 && rule.Kind == BozjaRuleKind.Tankbuster) yield return new("Tankbuster", false);
            if (rule.Kind != BozjaRuleKind.Tower || list.Length == 0) continue;
            if (IsVirtue && (VirtueTethers(rule).Count == 0
                || VirtueTethers(rule).Any(t => ReferenceEquals(t.Target, viewer)))) continue;
            if (VirtueInvalidTower(rule))
            {
                if (list.Any(t => !Soakers(t).Contains(viewer) && Inside(t, viewer)))
                    yield return new("GTFO from tower!");
                continue;
            }
            if (list.Any(t => !Soakers(t).Contains(viewer) && Inside(t, viewer)))
                yield return new("GTFO from tower!");
            else if (list.FirstOrDefault(t => Soakers(t).Contains(viewer) && Inside(t, viewer)) is { } own)
                yield return NumInside(own) == 1 ? new("Soak the tower!", false)
                    : new(NumInside(own) > 1 ? "Too many soakers in the tower!" : "Too few soakers in the tower!");
            else if (list.Any(t => Soakers(t).Contains(viewer) && NumInside(t) < 1))
                yield return new("Soak the tower!");
        }
    }

    internal IEnumerable<string> GlobalHints()
    {
        if (!Active) yield break;
        if (IsRobots && (RobotNavigationError ?? RobotHintError) is { } robotError) yield return robotError;
        if (IsFire)
        {
            _ = FireHotViews().ToArray();
            if (FireNavigationError != null) yield return FireNavigationError;
            if (FireDrawingError != null && FireDrawingError != FireNavigationError) yield return FireDrawingError;
        }
        foreach (var rule in ActiveRules)
        {
            if (!Cll1OutputEnabled(rule) || !Cll4OutputEnabled(rule)) continue;
            foreach (var hint in Cll1GlobalHints(rule)) yield return hint;
            if (rule.Kind == BozjaRuleKind.OwainCastHint && Casts.FirstOrDefault(c => c.Rule == rule) is { } timed)
            {
                var remaining = timed.Source.Cast is { } cast ? Total(cast) + .3f - timed.Source.Elapsed : 0;
                yield return $"Ivory Palm is enraging! {remaining:f1}s left";
            }
            if (rule.Kind == BozjaRuleKind.TimedCastHint && Casts.FirstOrDefault(c => c.Rule == rule) is { } raidenEnrage)
            {
                var remaining = raidenEnrage.Source.Cast is { } cast ? Total(cast) + .3f - raidenEnrage.Source.Elapsed : 0;
                yield return $"{rule.Hint} {remaining:f1}s left";
            }
            if (rule.Kind == BozjaRuleKind.DiabloOrbs && DiabloOrbs.Count > 0) yield return "Soak the orbs!";
            if (rule.Kind == BozjaRuleKind.SaunionMind && saunionOneMind)
                yield return $"Pull {Primary!.Actor.Name} & {Dawon?.Actor.Name} further apart!";
            if (rule.Kind == BozjaRuleKind.SaunionBait && Casts.Any(c => c.Rule == rule))
                yield return "Tankbuster cleave";
            if (rule.Kind == BozjaRuleKind.GallowsMarch && Primary?.Cast?.ActionId == 24096)
                yield return "Apply doom & march debuffs";
            if (rule.Kind == BozjaRuleKind.DahuBait && DahuBaits.Count > 0) yield return "Tankbuster cleave";
            if (IsVirtue && rule.Kind == BozjaRuleKind.CircleBait && Casts.Any(c => c.Rule == rule)) yield return "Tankbuster cleave";
            if (rule.Kind == BozjaRuleKind.SeekerIconBait && rule.Component == "MercifulArc"
                && SeekerBaits.Any(b => b.Rule == rule)) yield return "Tankbuster cleave";
            if (rule.GlobalHint && Casts.Any(c => c.Rule == rule))
                yield return rule.Hint ?? (rule.Kind == BozjaRuleKind.Tankbuster ? "Tankbuster" : "Raidwide");
            if (IsFire && rule.Kind == BozjaRuleKind.Raidwide && Casts.Any(c => c.Rule == rule))
                yield return rule.Hint ?? "Raidwide";
            if (rule.Kind == BozjaRuleKind.DelayedTankbuster && DelayedHits.Count > 0) yield return "Tankbuster";
            if (rule.Kind == BozjaRuleKind.DelayedRaidwide && WildBoltActivation != default) yield return "Raidwide";
            if (rule.Kind == BozjaRuleKind.Horn && Horns.Count > 0) yield return "Tankbuster cleave";
            if (rule.Kind == BozjaRuleKind.Juice && MissingJuicePositionCount > 0)
                yield return "地面效果缺少目标位置，未生成预测";
            if (rule.Kind == BozjaRuleKind.Exaflare && ExaflareErrors > 0)
                yield return $"地火效果未匹配队列：{ExaflareErrors}";
            if (rule.Kind == BozjaRuleKind.Exaflare && MissingExaflarePosition > 0)
                yield return $"地火效果缺少区域坐标：{MissingExaflarePosition}";
            if (rule.Kind == BozjaRuleKind.LightningClap && MissingLightningPosition > 0)
                yield return $"落雷效果缺少区域坐标：{MissingLightningPosition}";
            if (rule.Kind is BozjaRuleKind.CastHint or BozjaRuleKind.Interrupt && Casts.Any(c => c.Rule == rule))
                yield return rule.Kind == BozjaRuleKind.Interrupt ? interruptHints.GetValueOrDefault(rule, "Interrupt!") : rule.Hint ?? "Set hp to 1";
            if (rule.Kind == BozjaRuleKind.Enrage && Casts.FirstOrDefault(c => c.Rule == rule) is { } enrage)
            {
                var remaining = enrage.Source.Cast is { } spell ? Total(spell) + .3f - enrage.Source.Elapsed : 0;
                yield return $"Enrage! {remaining:f1}s left";
            }
        }
    }

    internal IEnumerable<BozjaZone> Navigation(BozjaActor viewer)
    {
        if (!Active) yield break;
        if (IsRobots && RobotNavigationError != null) yield break;
        if (IsFire && FireNavigationError != null) yield break;
        foreach (var rule in ActiveRules)
        {
            if (!Cll1OutputEnabled(rule) || !Cll4OutputEnabled(rule)) continue;
            foreach (var zone in LightNavigation(rule)) yield return zone;
            foreach (var zone in HeavyNavigation(rule)) yield return zone;
            foreach (var zone in Cll1Navigation(viewer, rule)) yield return zone;
            foreach (var zone in Cll3Navigation(rule)) yield return zone;
            foreach (var zone in Cll4Navigation(rule)) yield return zone;
            foreach (var zone in ArtNavigation(viewer, rule)) yield return zone;
            foreach (var zone in OwainNavigation(viewer, rule)) yield return zone;
            foreach (var zone in RaidenNavigation(viewer, rule)) yield return zone;
            foreach (var zone in CircleBaitNavigation(viewer, rule)) yield return zone;
            foreach (var zone in VirtueNavigation(viewer, rule)) yield return zone;
            if (rule.Kind == BozjaRuleKind.AlliancePull && Casts.FirstOrDefault(c => c.Rule == rule) is { } pull)
                yield return new($"alliance/pull/{pull.Generation}", rule.Component, pull.Activation,
                    new CircleTemplate(rule.Component == "Sucker" ? 23 : 20), pull.Origin, 0);
            foreach (var zone in GauntletNavigation(viewer, rule)) yield return zone;
            foreach (var zone in SaunionNavigation(viewer, rule)) yield return zone;
            if (IsDiablo)
            {
                foreach (var zone in DiabloNavigation(rule)) yield return zone;
                if (rule.Component is "Explosion" or "PillarOfShamash") continue;
            }
            foreach (var zone in SeekerNavigation(viewer, rule)) yield return zone;
            foreach (var zone in QueenNavigation(viewer, rule)) yield return zone;
            foreach (var zone in BurnNavigation(viewer, rule)) yield return zone;
            foreach (var zone in RobotNavigation(viewer, rule)) yield return zone;
            foreach (var aoe in CuchAoes(viewer, rule))
                yield return new($"cuch/aoe/{aoe.Cast.Generation}", rule.Component, aoe.Cast.Activation,
                    aoe.Cast.Shape, aoe.Cast.Origin, aoe.Cast.Rotation);
            if (rule.Kind == BozjaRuleKind.ColossusRotation)
                foreach (var zone in ColossusNavigation(rule)) yield return zone;
            if (rule.Kind == BozjaRuleKind.ShockSphere)
                foreach (var actor in GraveSpheres)
                    yield return new($"grave/sphere/{actor.BirthOrder}", rule.Component, default,
                        rule.Shape!, actor.Actor.Position, 0, Obstacle: true);
            if (rule.Kind is BozjaRuleKind.GraveArena or BozjaRuleKind.SoulPurge)
                foreach (var aoe in GraveAoes(rule).Where(a => a.Risky))
                    yield return new($"grave/aoe/{aoe.Cast.Generation}", rule.Component, aoe.Cast.Activation,
                        aoe.Cast.Shape, aoe.Cast.Origin, aoe.Cast.Rotation);
            if (rule.Kind == BozjaRuleKind.GuardParry)
                foreach (var zone in GuardNavigation(viewer)) yield return zone;
            if (rule.Kind is BozjaRuleKind.GuardArena or BozjaRuleKind.GuardAboveBoard or BozjaRuleKind.GuardTurrets)
                foreach (var aoe in Visible(viewer).Where(a => a.Cast.Rule == rule))
                    yield return new($"guard/aoe/{aoe.Cast.Generation}", rule.Component, aoe.Cast.Activation,
                        aoe.Cast.Shape, aoe.Cast.Origin, aoe.Cast.Rotation);
            if (rule.Kind is BozjaRuleKind.SeekerArena or BozjaRuleKind.SeekerSplitter or BozjaRuleKind.SeekerMercy)
                foreach (var aoe in Visible(viewer).Where(a => a.Cast.Rule == rule && a.Risky))
                    yield return new($"seeker/aoe/{aoe.Cast.Generation}", rule.Component, aoe.Cast.Activation,
                        aoe.Cast.Shape, aoe.Cast.Origin, aoe.Cast.Rotation);
            if (rule.Kind == BozjaRuleKind.PhantomKnockback)
            {
                foreach (var zone in PhantomNavigation(rule)) yield return zone;
                continue;
            }
            if (rule.Kind is BozjaRuleKind.PhantomArena or BozjaRuleKind.PhantomExaflare or BozjaRuleKind.PhantomCreeping)
                foreach (var aoe in Visible(viewer).Where(a => a.Cast.Rule == rule))
                    yield return new($"phantom/{aoe.Cast.Generation}", rule.Component, aoe.Cast.Activation,
                        aoe.Cast.Shape, aoe.Cast.Origin, aoe.Cast.Rotation);
            if (rule.Kind is BozjaRuleKind.DahuSpit or BozjaRuleKind.DahuBurn)
            {
                foreach (var zone in SavageDahuNavigation(viewer, rule)) yield return zone;
                continue;
            }
            if (rule.Kind is BozjaRuleKind.DahuShockwave or BozjaRuleKind.DahuRotation or BozjaRuleKind.DahuKnockback or BozjaRuleKind.DahuBait or BozjaRuleKind.DahuSavageKnockback)
            {
                foreach (var zone in DahuNavigation(viewer, rule)) yield return zone;
                continue;
            }
            if (rule.Kind is BozjaRuleKind.BracingWind or BozjaRuleKind.LunarCry or BozjaRuleKind.ThermalGust or BozjaRuleKind.EndlessFrost)
            {
                foreach (var zone in FrostNavigation(viewer, rule)) yield return zone;
                continue;
            }
            var list = Casts.Where(c => c.Rule == rule).ToArray();
            if (rule.Kind == BozjaRuleKind.Pheromones)
                foreach (var actor in Pheromones)
                {
                    var capsule = (BozjaCapsuleTemplate)rule.Shape!;
                    float[] lengths = [capsule.Length * .5f, capsule.Length, 2 * capsule.Length, 3 * capsule.Length];
                    DateTime[] clocks = [Now.AddSeconds(1.1d), Now.AddSeconds(3d), Now.AddSeconds(10d), DateTime.MaxValue];
                    for (var i = 0; i < lengths.Length; ++i)
                        yield return new($"pheromone/{actor.BirthOrder}/{i}", rule.Component, clocks[i],
                            new BozjaCapsuleTemplate(capsule.Radius, lengths[i]), actor.Actor.Position, actor.Actor.Rotation);
                    yield return new($"pheromone/{actor.BirthOrder}/obstacle", rule.Component, default,
                        new CircleTemplate(capsule.Radius), actor.Actor.Position, 0, Obstacle: true);
                }
            if (rule.Kind == BozjaRuleKind.Wind)
                foreach (var zone in WindNavigation(viewer)) yield return zone;
            if (rule.Kind == BozjaRuleKind.LightningRod)
                foreach (var zone in MineNavigation(viewer)) yield return zone;
            if (rule.Kind == BozjaRuleKind.Rush)
                foreach (var zone in RushNavigation(viewer)) yield return zone;
            if (rule.Kind == BozjaRuleKind.Horn)
                foreach (var zone in HornNavigation(viewer)) yield return zone;
            if (rule.Kind == BozjaRuleKind.Hailfire)
                foreach (var zone in HailfireNavigation(viewer)) yield return zone;
            if (rule.Kind == BozjaRuleKind.ChargeTether)
                foreach (var zone in SteelNavigation(viewer)) yield return zone;
            if (rule.Kind is BozjaRuleKind.CrystalSpread or BozjaRuleKind.Spread)
                foreach (var zone in CrystalSpreadNavigation(viewer, rule)) yield return zone;
            if (rule.Kind is BozjaRuleKind.ControlTower or BozjaRuleKind.Exaflare or BozjaRuleKind.Hammer)
                foreach (var aoe in HashmalAoes(rule).Where(a => a.Risky))
                    yield return new($"aoe/{aoe.Cast.Generation}", rule.Component, aoe.Cast.Activation,
                        aoe.Cast.Shape, aoe.Cast.Origin, aoe.Cast.Rotation);
            if (rule.Kind is BozjaRuleKind.LightningClap or BozjaRuleKind.Burn)
                foreach (var aoe in AidaAoes(rule))
                    yield return new($"aoe/{aoe.Cast.Generation}", rule.Component, aoe.Cast.Activation,
                        aoe.Cast.Shape, aoe.Cast.Origin, aoe.Cast.Rotation);
            if (rule.Kind == BozjaRuleKind.Cleave)
                foreach (var zone in FireCleaveNavigation(viewer)) yield return zone;
            if (rule.Kind == BozjaRuleKind.ElectrochemicalReaction
                && Mines.Count > 0 && MineForbidden(Mines[0], ViewerSlot(viewer)))
                foreach (var cast in list)
                    yield return new($"aoe/{cast.Generation}", rule.Component, cast.Activation, cast.Shape, cast.Origin, cast.Rotation);
            if (rule.Kind is BozjaRuleKind.Aoe or BozjaRuleKind.Charge or BozjaRuleKind.Hellpounce or BozjaRuleKind.VoidQuake or BozjaRuleKind.DeadLeaves or BozjaRuleKind.ArenaChange or BozjaRuleKind.ArenaOnFinish or BozjaRuleKind.MagitekBitLasers or BozjaRuleKind.Glimmer or BozjaRuleKind.Meteor or BozjaRuleKind.Juice or BozjaRuleKind.Thunderbolt or BozjaRuleKind.Countdown or BozjaRuleKind.TooHot or BozjaRuleKind.Stormcall or BozjaRuleKind.Foreshadowing or BozjaRuleKind.Time or BozjaRuleKind.CrystalSight or BozjaRuleKind.Earthbreaker)
            {
                foreach (var aoe in Visible(viewer).Where(a => a.Cast.Rule == rule && a.Risky))
                {
                    var cast = aoe.Cast;
                    yield return new($"aoe/{cast.Generation}", rule.Component, cast.Activation, cast.Shape, cast.Origin, cast.Rotation);
                }
                if (rule.Component == "CoffinNails" && list.Length > 2)
                    yield return new($"near/{list[0].Generation}", rule.Component, list[0].Activation,
                        new CircleTemplate(4), list[0].Origin, 0, Goal: true);
            }
            if (rule.Kind is BozjaRuleKind.Stack or BozjaRuleKind.BombStack)
                foreach (var zone in StackNavigation(viewer, rule)) yield return zone;
            if (rule.Kind != BozjaRuleKind.Tower || list.Length == 0) continue;
            if (VirtueInvalidTower(rule)) continue;
            var inTower = list.Any(t => Inside(t, viewer) && NumInside(t) == 1);
            var join = new List<Vector3>();
            var forbidden = new List<BozjaCast>();
            foreach (var tower in list)
            {
                var inside = Inside(tower, viewer);
                var count = NumInside(tower);
                if (Soakers(tower).Contains(viewer) && (count < 1 || inside && count == 1)) join.Add(tower.Origin);
                else if (!Soakers(tower).Contains(viewer) || count > 1 || !inside && count == 1) forbidden.Add(tower);
            }
            // Source missingSoakers starts as !inTower and never becomes false.
            // Any join therefore suppresses all separately collected hazards.
            if (join.Count > 0)
                yield return new($"tower/{list[0].Generation}/join", rule.Component, list[0].Activation, null, default, 0, join.ToArray(),
                    JoinRadius: TowerRadius(list[0]));
            else if (!inTower)
                foreach (var tower in forbidden)
                    yield return new($"tower/{tower.Generation}/avoid", rule.Component, list[0].Activation, tower.Shape, tower.Origin, 0);
        }
    }

    internal BozjaZone[] NavigationForHost(BozjaActor viewer)
    {
        var raw = Navigation(viewer).ToArray();
        if (IsAllianceTrash)
        {
            SortActivations(raw, 0, raw.Length - 1, z => z.Activation);
            return raw;
        }
        if (!IsBaldesion && !IsCll1 && !IsSkirmish && !IsGauntlet && Definition.Key != "DAL1Sartauvoir" && !IsDiablo && !IsSaunion && !IsCuchulainn && !IsRobots && !IsGrave && !IsBurn && !IsQueen && !IsGuard && !IsSeeker && !Definition.Rules.Any(r => r.Kind is BozjaRuleKind.Pheromones or BozjaRuleKind.MagitekBitLasers or BozjaRuleKind.Gaze or BozjaRuleKind.CastGaze or BozjaRuleKind.Meteor or BozjaRuleKind.Countdown or BozjaRuleKind.Stormcall or BozjaRuleKind.Foreshadowing or BozjaRuleKind.Time or BozjaRuleKind.CrystalSight or BozjaRuleKind.ControlTower or BozjaRuleKind.Burn or BozjaRuleKind.ChargeTether or BozjaRuleKind.EndlessFrost or BozjaRuleKind.DahuRotation or BozjaRuleKind.PhantomExaflare or BozjaRuleKind.ColossusRotation)) return raw;
        // AIHints.Normalize sorts all forbidden zones, including complements;
        // temporary obstacles have their own list and preserve source order.
        var zones = raw.Where(z => !z.Obstacle).ToArray();
        SortActivations(zones, 0, zones.Length - 1, z => z.Activation);
        return [.. zones, .. raw.Where(z => z.Obstacle)];
    }

    internal IEnumerable<BozjaDamage> Damage(BozjaActor viewer)
    {
        if (!Active) yield break;
        foreach (var rule in ActiveRules)
        {
            if (!Cll1OutputEnabled(rule) || !Cll4OutputEnabled(rule)) continue;
            foreach (var damage in SaunionDamage(rule)) yield return damage;
            foreach (var damage in SeekerDamage(rule)) yield return damage;
            foreach (var damage in RaidenDamage(rule)) yield return damage;
            foreach (var damage in CircleBaitDamage(rule)) yield return damage;
            var list = Casts.Where(c => c.Rule == rule).ToArray();
            if (rule.Kind == BozjaRuleKind.Tankbuster)
                foreach (var cast in list)
                {
                    // SingleTargetCast retains original caster references after
                    // duplicate Start; its current CastInfo can be non-Spell.
                    var spell = IsAllianceTrash ? cast.Source.RawCast : cast.Source.Cast;
                    if (spell == null) continue;
                    var target = spell.TargetId == cast.Source.Actor.Id ? cast.Source.Actor.TargetId : spell.TargetId;
                    var slot = Party.Where(p => p.Slot is >= 0 and < 64).OrderBy(p => p.Slot)
                        .FirstOrDefault(p => p.EntityId == target)?.Slot;
                    var activation = IsAllianceTrash
                        ? Now.AddSeconds(BurnRawTotal(spell) + .3f - cast.Source.RawElapsed)
                        : Activation(cast.Source);
                    yield return new(slot is { } i ? 1UL << i : 0, activation, "Tankbuster");
                }
            if (rule.Kind == BozjaRuleKind.Raidwide)
                foreach (var cast in list)
                {
                    var activation = IsAllianceTrash
                        ? AllianceRaidwideActivation(cast.Source)
                        : Activation(cast.Source);
                    yield return new(PartyMask(_ => true), activation, "Raidwide");
                }
            if (rule.Kind == BozjaRuleKind.Tower && list.Length > 0 && !VirtueInvalidTower(rule))
                yield return new(PartyMask(a => list.Any(t => Soakers(t).Contains(viewer) && Inside(t, a))),
                    list[0].Activation, "Raidwide");
            if (rule.Kind == BozjaRuleKind.LightningRod && Mines.Count > 0) yield return MineDamage(viewer);
            if (rule.Kind == BozjaRuleKind.Rush)
                foreach (var damage in RushDamage()) yield return damage;
            if (rule.Kind == BozjaRuleKind.Horn)
                foreach (var damage in HornDamage()) yield return damage;
            if (rule.Kind == BozjaRuleKind.DahuBait)
                foreach (var damage in DahuDamage()) yield return damage;
            if (rule.Kind == BozjaRuleKind.DahuBurn)
                foreach (var damage in SavageDahuBurnDamage()) yield return damage;
            if (rule.Kind == BozjaRuleKind.ChargeTether && SteelDamage() is { } steel) yield return steel;
            if (rule.Kind == BozjaRuleKind.DelayedRaidwide && WildBoltActivation != default)
                yield return new(PartyMask(_ => true), WildBoltActivation, "Raidwide");
            if (rule.Kind == BozjaRuleKind.DelayedTankbuster)
                foreach (var hit in DelayedHits)
                    yield return new(hit.Slot is >= 0 and < 64 ? 1UL << hit.Slot : 0, hit.Activation, "Tankbuster");
        }
    }

    internal BozjaDamage[] DamageForHost(BozjaActor viewer)
    {
        var damage = Damage(viewer).ToList();
        if (IsCll1 || IsGauntlet || Definition.Key == "DAL1Sartauvoir" || IsDiablo || IsSaunion || IsCuchulainn || IsQueen || IsSeeker || Definition.Rules.Any(r => r.Kind is BozjaRuleKind.ControlTower or BozjaRuleKind.Burn or BozjaRuleKind.ChargeTether))
            damage.Sort(static (a, b) => a.Activation.CompareTo(b.Activation));
        if (IsCll3 || IsCll4 || IsBaldesion || IsAllianceTrash) damage.Sort(static (a, b) => a.Activation.CompareTo(b.Activation));
        return damage.ToArray();
    }

    private ulong PartyMask(Func<BozjaActor, bool> predicate)
    {
        ulong mask = 0;
        foreach (var member in Party.Where(p => p.Slot is >= 0 and < 64))
            if (Actors.GetValueOrDefault(member.EntityId) is { Actor.Dead: false } actor && predicate(actor))
                mask |= 1UL << member.Slot;
        return mask;
    }
}
