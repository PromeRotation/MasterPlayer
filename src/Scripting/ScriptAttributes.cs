using System.Numerics;

namespace MasterPlayer.Scripting;

/// <summary>
/// 机制脚本元数据特性（参考可达鸭 ScriptType 形式，便于脚本移植）。
/// guid 是脚本的身份主键：配置、设置、启停、仓库更新都以它为准。
/// territorys 为空 = 通用脚本（不限区域）。
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class MechanicScriptAttribute : Attribute
{
    public string Name { get; }
    public uint[] Territorys { get; }
    public string Guid { get; }
    public string Version { get; }
    public string Author { get; }
    public string Note { get; }

    public MechanicScriptAttribute(
        string name,
        uint[] territorys,
        string guid,
        string version,
        string author,
        string note = "")
    {
        Name = name;
        Territorys = territorys ?? [];
        Guid = guid;
        Version = version;
        Author = author;
        Note = note;
    }
}

/// <summary>
/// 脚本中暴露的用户设置项（参考可达鸭 UserSetting 形式）。
/// 支持类型：bool / int / float / string / enum / ScriptColor。
/// 由宿主反射收集并持久化到 MasterPlayer.json 的 scriptSettings（键 = 脚本guid-属性名）。
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class UserSettingAttribute : Attribute
{
    public string DisplayName { get; }

    public UserSettingAttribute(string displayName)
    {
        DisplayName = displayName;
    }
}

/// <summary>
/// 颜色值类型（参考可达鸭 ScriptColor 形式），可被 JSON 持久化。
/// </summary>
public sealed class ScriptColor
{
    public Vector4 V4 { get; set; } = new(1f, 1f, 1f, 1f);

    public static implicit operator Vector4(ScriptColor color) => color.V4;
}
