using System.Numerics;
using System.Text.Json.Serialization;
using PromeRotation.Spatial.SafeZone;

namespace MasterPlayer.Scripting.Visual;

public enum VisualAoeShape { Circle, Donut, Fan, Rectangle, RayRectangle }
public enum VisualAoeOrigin { Caster, CastLocation, Target, Fixed }
public enum VisualAoeTracking { Snapshot, FollowPosition, FollowPositionAndRotation }
public enum VisualAoeDirection { CastRotation, CasterRotation, Fixed }

public sealed class VisualScriptDefinition
{
    public const int CurrentSchemaVersion = 1;

    [JsonRequired] public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    [JsonRequired] public string Id { get; set; } = Guid.NewGuid().ToString("D");
    public string Name { get; set; } = "新建可视化脚本";
    public string Note { get; set; } = "";
    public int TerritoryId { get; set; }
    public List<VisualMechanicRule> Rules { get; set; } = [];

    public List<string> Validate()
    {
        var errors = new List<string>();
        if (SchemaVersion != CurrentSchemaVersion)
            errors.Add($"不支持的脚本格式版本 {SchemaVersion}，当前支持 {CurrentSchemaVersion}。");
        if (!Guid.TryParseExact(Id, "D", out _)) errors.Add("脚本 ID 必须是有效的 GUID。");
        if (string.IsNullOrWhiteSpace(Name)) errors.Add("请填写脚本名称。");
        if (TerritoryId is <= 0 or > ushort.MaxValue) errors.Add("区域 ID 必须为 1～65535。");
        if (Rules == null)
        {
            errors.Add("机制列表不能为空值。");
            return errors;
        }
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < Rules.Count; i++)
        {
            var rule = Rules[i];
            if (rule == null) { errors.Add($"机制 {i + 1} 不能为空值。"); continue; }
            if (!Guid.TryParseExact(rule.Id, "D", out _) || !ids.Add(rule.Id))
                errors.Add($"机制 {i + 1} 的 ID 无效或重复。");
            errors.AddRange(rule.Validate().Select(e => $"机制 {i + 1}（{rule.Name}）：{e}"));
        }
        return errors;
    }
}

public sealed class VisualMechanicRule
{
    [JsonRequired] public string Id { get; set; } = Guid.NewGuid().ToString("D");
    public string Name { get; set; } = "新机制";
    public bool Enabled { get; set; } = true;
    public int ActionId { get; set; }
    public uint CasterDataId { get; set; }
    public VisualAoeShape Shape { get; set; }
    public float Radius { get; set; } = 5f;
    public float InnerRadius { get; set; } = 3f;
    public float AngleDegrees { get; set; } = 90f;
    public float Width { get; set; } = 6f;
    public float Length { get; set; } = 20f;
    public VisualAoeOrigin Origin { get; set; }
    public VisualAoeTracking Tracking { get; set; }
    public VisualAoeDirection Direction { get; set; }
    public Vector3 Position { get; set; }
    public Vector2 Offset { get; set; }
    public float RotationDegrees { get; set; }
    public float AngleOffsetDegrees { get; set; }
    public float DelaySeconds { get; set; }
    public float HitDurationSeconds { get; set; } = 0.6f;
    public bool SuppressGeneric { get; set; } = true;

    public List<string> Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(Name)) errors.Add("请填写机制名称。");
        if (ActionId is <= 0 or > ushort.MaxValue) errors.Add("读条 ID 必须为 1～65535（十进制）。");
        if (!Enum.IsDefined(Origin) || !Enum.IsDefined(Tracking) || !Enum.IsDefined(Direction))
            errors.Add("位置或绑定类型不受支持。");
        errors.AddRange(ValidateGeometry());
        if (!float.IsFinite(DelaySeconds) || DelaySeconds < 0 || DelaySeconds > 300) errors.Add("读条结束后延迟必须为 0～300 秒。");
        if (!Positive(HitDurationSeconds) || HitDurationSeconds > 300) errors.Add("判定持续时间必须大于 0 且不超过 300 秒。");
        if (!Finite(Position) || !float.IsFinite(Offset.X) || !float.IsFinite(Offset.Y) || !float.IsFinite(RotationDegrees))
            errors.Add("坐标、偏移和朝向必须为有限数值。");
        if (Origin is VisualAoeOrigin.CastLocation or VisualAoeOrigin.Fixed && Tracking != VisualAoeTracking.Snapshot)
            errors.Add("技能落点和固定坐标只能使用锁定位置。");
        return errors;
    }

    internal List<string> ValidateGeometry()
    {
        var errors = new List<string>();
        if (!Enum.IsDefined(Shape)) errors.Add("AOE 形状不受支持。");
        if (!float.IsFinite(AngleOffsetDegrees)) errors.Add("角度偏移必须为有限数值。");
        if (Shape is VisualAoeShape.Circle or VisualAoeShape.Donut or VisualAoeShape.Fan && !Positive(Radius))
            errors.Add("半径必须大于 0 且为有限数值。");
        if (Shape is VisualAoeShape.Rectangle or VisualAoeShape.RayRectangle && (!Positive(Width) || !Positive(Length)))
            errors.Add("宽度和长度必须大于 0 且为有限数值。");
        if (Shape == VisualAoeShape.Donut && (!float.IsFinite(InnerRadius) || InnerRadius < 0 || InnerRadius >= Radius))
            errors.Add("月环内半径必须 ≥ 0 且小于外半径。");
        if (Shape == VisualAoeShape.Fan && (!float.IsFinite(AngleDegrees) || AngleDegrees is <= 0 or > 360))
            errors.Add("扇形总开角必须为 0～360 度（不含 0）。");
        return errors;
    }

    internal void ChangeShape(VisualAoeShape shape)
    {
        Shape = shape;
        ResetInactiveValues();
    }

    internal void ResetInactiveValues()
    {
        var shape = Shape;
        if (shape is VisualAoeShape.Rectangle or VisualAoeShape.RayRectangle && !Positive(Radius)) Radius = 5f;
        if (shape is not (VisualAoeShape.Rectangle or VisualAoeShape.RayRectangle))
        {
            if (!Positive(Width)) Width = 6f;
            if (!Positive(Length)) Length = 20f;
        }
        if (shape != VisualAoeShape.Donut && (!float.IsFinite(InnerRadius) || InnerRadius < 0)) InnerRadius = 3f;
        if (shape != VisualAoeShape.Fan && (!float.IsFinite(AngleDegrees) || AngleDegrees is <= 0 or > 360)) AngleDegrees = 90f;
        if (Origin != VisualAoeOrigin.Fixed && !Finite(Position)) Position = Vector3.Zero;
        if (!float.IsFinite(RotationDegrees) && (Direction != VisualAoeDirection.Fixed
            || Tracking == VisualAoeTracking.FollowPositionAndRotation || shape is VisualAoeShape.Circle or VisualAoeShape.Donut))
            RotationDegrees = 0f;
        if (shape is VisualAoeShape.Circle or VisualAoeShape.Donut && !float.IsFinite(AngleOffsetDegrees))
            AngleOffsetDegrees = 0f;
    }

    public GeometryTemplate CreateTemplate()
    {
        GeometryTemplate template = Shape switch
        {
            VisualAoeShape.Circle => new CircleTemplate(Radius),
            VisualAoeShape.Donut => new DonutTemplate(InnerRadius, Radius),
            VisualAoeShape.Fan => new FanTemplate(Radius, AngleDegrees * MathF.PI / 180f),
            VisualAoeShape.Rectangle => new RectangleTemplate(Width, Length),
            VisualAoeShape.RayRectangle => RayRectangleTemplate.Symmetric(Width, Length),
            _ => throw new InvalidOperationException("不支持的 AOE 形状。"),
        };
        var offset = MathF.IEEERemainder(AngleOffsetDegrees, 360f) * (MathF.PI / 180f);
        return offset == 0f || Shape is VisualAoeShape.Circle or VisualAoeShape.Donut
            ? template
            : new RotationOffsetTemplate(template, offset);
    }

    internal static bool Finite(Vector3 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
    private static bool Positive(float value) => float.IsFinite(value) && value > 0;
}
