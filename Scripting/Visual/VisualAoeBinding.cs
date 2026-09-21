using System.Numerics;
using ECommons.DalamudServices;
using PromeRotation.LogSystem;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Scripting.Visual;

internal sealed record VisualActor(uint EntityId, uint DataId, Vector3 Position, float Rotation);

internal sealed record VisualCast(LogSystemCastStartEvent Event, VisualActor? Caster, VisualActor? Target, float? CastRotation)
{
    internal static VisualCast Capture(LogSystemCastStartEvent e)
    {
        static VisualActor? Actor(uint id)
        {
            var actor = Svc.Objects.SearchByEntityId(id);
            return actor == null ? null : new VisualActor(actor.EntityId, actor.BaseId, actor.Position, actor.Rotation);
        }
        var rotation = Scripting.CastRotation.TryGet(e, out var value) ? value : (float?)null;
        return new VisualCast(e, Actor((uint)e.SourceId), Actor((uint)e.TargetId), rotation);
    }
}

internal static class VisualAoeBinding
{
    internal static bool Matches(VisualMechanicRule rule, VisualCast cast)
        => rule.Enabled && rule.ActionId == cast.Event.ActionId
           && (rule.CasterDataId == 0 || cast.Caster?.DataId == rule.CasterDataId);

    internal static bool TryCreate(VisualMechanicRule rule, VisualCast cast, out TransformBinding binding, out string error)
    {
        binding = null!;
        error = "";
        var actor = rule.Origin == VisualAoeOrigin.Target ? cast.Target : cast.Caster;
        if (rule.Tracking != VisualAoeTracking.Snapshot)
        {
            if (rule.Origin is not (VisualAoeOrigin.Caster or VisualAoeOrigin.Target))
                return Fail("该位置来源无法跟随实体。", out error);
            if (actor == null || actor.EntityId is 0 or 0xE0000000)
                return Fail("找不到绑定实体。", out error);
            if (rule.Tracking == VisualAoeTracking.FollowPositionAndRotation)
            {
                binding = new FollowEntityPositionAndRotation(actor.EntityId, rule.Offset, MissingEntityPolicy.RemoveArea);
                return true;
            }
        }

        var rotation = 0f;
        if (rule.Shape is not (VisualAoeShape.Circle or VisualAoeShape.Donut))
        {
            float? direction = rule.Direction switch
            {
                VisualAoeDirection.CastRotation => cast.CastRotation,
                VisualAoeDirection.CasterRotation => cast.Event.SourceRotation,
                VisualAoeDirection.Fixed => rule.RotationDegrees * (MathF.PI / 180f),
                _ => null,
            };
            if (direction == null || !float.IsFinite(direction.Value))
                return Fail("无法读取施法朝向，请确认读条仍在进行。", out error);
            rotation = direction.Value;
        }

        if (rule.Tracking == VisualAoeTracking.FollowPosition)
        {
            binding = new FollowEntityPosition(actor!.EntityId, rule.Offset, rotation, MissingEntityPolicy.RemoveArea);
            return true;
        }

        Vector3? position = rule.Origin switch
        {
            VisualAoeOrigin.Caster => cast.Event.SourcePosition ?? cast.Caster?.Position,
            VisualAoeOrigin.Target => cast.Target?.Position,
            VisualAoeOrigin.CastLocation => cast.Event.EffectPosition,
            VisualAoeOrigin.Fixed => rule.Position,
            _ => null,
        };
        if (position == null || !VisualMechanicRule.Finite(position.Value))
            return Fail("找不到有效的 AOE 位置。", out error);
        binding = new StaticTransform(position.Value + new Vector3(rule.Offset.X, 0f, rule.Offset.Y), rotation);
        return true;
    }

    private static bool Fail(string message, out string error)
    {
        error = message;
        return false;
    }
}
