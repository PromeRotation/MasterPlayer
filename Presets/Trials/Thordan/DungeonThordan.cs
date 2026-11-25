using System.Numerics;
using MasterPlayer.Mechanics.PilgrimsTraverse;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using PromeRotation.LogSystem;
using C = MasterPlayer.Mechanics.Dungeons.ThordanComponent;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    internal bool IsThordan => territory == ThordanCatalog.TERRITORY;
    internal ThordanEncounter Thordan { get; } = new(warning);
    internal bool ThordanInputScan { get; set; }

    private void ThordanSynchronize(IEnumerable<DungeonActor> input, DateTime now, Func<string, bool>? enabled, ulong playerId)
    {
        var actors = input.ToArray();
        _now = now;
        PlayerId = playerId;
        _actors.Clear();
        foreach (var actor in actors) _actors[actor.Id] = actor;
        Thordan.Synchronize(actors, CurrentPartyMembers, playerId, now,
            enabled?.Invoke(Definitions[0].Name) != false, !ThordanInputScan);
        ThordanPublish();
    }

    private void ThordanPublish()
    {
        Active = Thordan.Active ? Definitions[0] : null;
        PrimaryId = Thordan.Primary?.Actor.Id ?? 0;
        Arena = null;
        if (Thordan.Loaded)
            Arena = Thordan.ReducedArena ? ThordanCatalog.REDUCED_ARENA : ThordanCatalog.STARTING_ARENA;
    }

    internal void ThordanEvent(LogSystemEvent ev, DateTime now)
    {
        switch (ev)
        {
            case NativeActionEffectEvent effect when effect.ActionType is null or 1:
                Thordan.Effect(effect.SourceId, effect.ActionId, now);
                break;
            case LogSystemTargetIconEvent icon:
                Thordan.Icon(icon.SourceId, icon.MarkerId, now);
                break;
            case LogSystemEObjAnimationEvent animation:
                Thordan.Animation(animation.SourceId, ((uint)animation.Param1 << 16) | animation.Param2);
                break;
            case LogSystemDirectorUpdateEvent { Command: 0x40000005 }:
                Thordan.End();
                break;
        }
        ThordanPublish();
    }

    private IEnumerable<DungeonAmbientAoe> ThordanAoes()
    {
        if (!Thordan.Loaded || Thordan.Primary is not { } primary) yield break;
        if (!Thordan.Active || Thordan.Slot < 0)
        {
            var arena = Thordan.ReducedArena ? ThordanCatalog.REDUCED_ARENA : ThordanCatalog.STARTING_ARENA;
            foreach (var contour in arena.GetContours())
                yield return new("thordan/arena/display", "Arena outline", new WorldPolygonTemplate(contour),
                    default, Navigate: false, OutlineOnly: true);
        }
        if (!primary.Actor.Dead && primary.Actor.Targetable)
            yield return ThordanMarker("primary", primary.Actor);
        if (!Thordan.Active || Thordan.Actors.GetValueOrDefault(PlayerId) is not { } viewer) yield break;
        var navigate = Thordan.Slot >= 0;
        foreach (var component in Thordan.ActiveComponents)
        {
            switch (component)
            {
                case C.AscalonsMight:
                case C.HeavenlySlashAdelphel:
                case C.HeavenlySlashJanlenoux:
                    foreach (var aoe in ThordanCleaves(component, viewer, navigate)) yield return aoe;
                    break;
                case C.DragonsGaze:
                    foreach (var eye in Thordan.Aoes.Where(a => a.Component == component))
                    {
                        var danger = ThordanEncounter.EyeDanger(eye.Origin, viewer.Actor);
                        var color = danger ? new Vector4(1, 0, 0, 1) : new Vector4(0, 1, 0, 1);
                        yield return new($"thordan/gaze/{eye.Generation}/eye", "Turn away from gaze!", eddaEyeSymbol,
                            eye.Origin, Navigate: false, Safe: !danger, Unclipped: true, Color: color);
                        yield return new($"thordan/gaze/{eye.Generation}/pupil", "Dragon's Gaze", new CircleTemplate(.28f),
                            eye.Origin, Navigate: false, Unclipped: true, Color: new(.04f, .04f, .04f, 1));
                        yield return new($"thordan/gaze/{eye.Generation}/arc", "Dragon's Gaze", eddaGazeArc,
                            viewer.Actor.Position, viewer.Actor.Rotation, Navigate: false, Unclipped: true, Color: new(1, 0, 0, 1));
                    }
                    if (Thordan.EyePositionHint != default)
                        yield return new("thordan/gaze/hint", "Dragon eye position", new CircleTemplate(1),
                            Thordan.EyePositionHint, Navigate: false, OutlineOnly: true, Safe: true);
                    break;
                case C.ArenaChange:
                    yield return new("thordan/arena/change", "Arena Change", new DonutTemplate(21, 24), default, Navigate: navigate);
                    break;
                case C.SwordShieldOfTheHeavens:
                    foreach (var sword in Thordan.Swords.Where(s => !s.Actor.Destroyed && !s.Actor.Actor.Dead && s.Actor.Actor.Targetable))
                        yield return ThordanMarker($"sword/{sword.Actor.Actor.Id}", sword.Actor.Actor);
                    break;
                case C.SerZephirin:
                case C.CometCircle:
                case C.MeteorCircle:
                    var oid = component == C.SerZephirin ? 0x1454u : component == C.CometCircle ? 0x1462u : 0x1461u;
                    foreach (var add in Thordan.Actors.Values.Where(a => a.Actor.Oid == oid && !a.Actor.Dead && a.Actor.Targetable))
                        yield return ThordanMarker($"add/{add.Actor.Id}", add.Actor);
                    break;
                case C.LightningStorm:
                case C.HiemalStormSpread:
                case C.DragonsRage:
                    foreach (var aoe in ThordanPartyAoes(component, viewer, navigate)) yield return aoe;
                    break;
                case C.SkywardLeap:
                case C.SpiralPierce:
                    foreach (var aoe in ThordanBaits(component, viewer, navigate)) yield return aoe;
                    break;
                case C.BurningChains:
                    if (navigate && Thordan.ChainPartners[Thordan.Slot] is { } partner)
                        yield return new("thordan/chain/navigation", "Break the chains!",
                            new CircleTemplate(MathF.Sqrt(ThordanEncounter.DistanceSquared(partner.Actor.Position, viewer.Actor.Position)) + 1),
                            partner.Actor.Position, Display: false, Activation: _now.AddSeconds(10d));
                    break;
                case C.Conviction:
                    foreach (var aoe in ThordanTowers(viewer, navigate)) yield return aoe;
                    break;
                case C.HiemalStormVoidzone:
                    foreach (var zone in Thordan.Actors.Values.Where(a => a.Actor.Oid == 0x1E9729 && a.Actor.EventState != 7))
                        yield return new($"thordan/voidzone/{zone.Actor.Id}", "Hiemal Storm", new CircleTemplate(6),
                            zone.Actor.Position, Navigate: navigate, Obstacle: true);
                    break;
                case C.HeavyImpact:
                    foreach (var sequence in Thordan.Heavy)
                        yield return new($"thordan/heavy/{sequence.Generation}", "Heavy Impact",
                            ThordanHeavyShape(sequence.Order), sequence.Origin, sequence.Rotation,
                            Navigate: navigate, Activation: sequence.Activation);
                    break;
                case C.FaithUnmoving:
                    if (Thordan.Faith is { } faith)
                    {
                        if (navigate)
                            yield return new("thordan/faith/navigation", "Faith Unmoving landing in R18",
                                new ThordanFaithTemplate(faith), default, Display: false, Activation: Thordan.FaithNavigationAt);
                        foreach (var aoe in TrialKnockbackDrawing("thordan/faith", "Faith Unmoving",
                            viewer.Actor.Position, faith.Landing(viewer.Actor.Position))) yield return aoe;
                    }
                    break;
                case C.LightOfAscalon:
                    var point = viewer.Actor.Position;
                    for (var i = 0; i < Thordan.Light.Count; ++i)
                    {
                        var landing = Thordan.Light[i].Landing(point);
                        foreach (var aoe in TrialKnockbackDrawing($"thordan/light/{i}", "Light of Ascalon", point, landing))
                            yield return aoe;
                        point = landing;
                    }
                    break;
                case C.HolyShieldBash:
                    foreach (var aoe in ThordanWildCharge(viewer, navigate)) yield return aoe;
                    break;
                default:
                    foreach (var aoe in Thordan.Aoes.Where(a => a.Component == component && component != C.HoliestOfHoly))
                        yield return new($"thordan/aoe/{aoe.Generation}", $"Action {aoe.Action}", aoe.Shape,
                            aoe.Origin, aoe.Rotation, Navigate: navigate && aoe.Risky, Activation: aoe.Activation);
                    break;
            }
        }
    }

    private static DungeonAmbientAoe ThordanMarker(string id, DungeonActor actor)
        => new($"thordan/marker/{id}", "Enemy", new LocalPolygonTemplate(
            [new(0, -.7f), new(.433f, .35f), new(-.433f, .35f)]), actor.Position, actor.Rotation,
            Navigate: false, Color: new(1, 0, 0, 1));

    internal IEnumerable<(string Id, Vector3 Start, Vector3 End)> ThordanLines()
    {
        if (!IsThordan || !Thordan.Active || Thordan.Actors.GetValueOrDefault(PlayerId) is not { } viewer) yield break;
        if (Thordan.Slot >= 0 && Thordan.ChainPartners[Thordan.Slot] is { } partner)
            yield return ("thordan/chain/line", viewer.Actor.Position, partner.Actor.Position);
        foreach (var bait in Thordan.Baits.Where(b => b.Component == C.SpiralPierce && !b.Source.Actor.Dead))
            yield return ($"thordan/pierce/line/{bait.Generation}", bait.Source.Actor.Position, bait.Target.Actor.Position);
    }

    internal static GeometryTemplate ThordanHeavyShape(int order) => order switch
    {
        0 => new FanTemplate(6.5f, 270 * MathF.PI / 180),
        1 => new PtDonutSectorTemplate(6.5f, 12.5f, 270 * MathF.PI / 180),
        2 => new PtDonutSectorTemplate(12.5f, 18.5f, 270 * MathF.PI / 180),
        _ => new PtDonutSectorTemplate(18.5f, 27.5f, 270 * MathF.PI / 180),
    };
}
