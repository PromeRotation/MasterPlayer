using System.Numerics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MasterPlayer.Scripting;

/// <summary>
/// 单个脚本的设置存取。
/// [UserSetting] 标注的属性自动映射到持久化存储（键 = 属性名）；
/// 未用特性标注的属性也支持键值读写（Get/Set）。
/// 值类型支持：bool / int / float / string / enum / ScriptColor。
/// </summary>
public sealed class ScriptSettingStore
{
    private readonly Dictionary<string, object?> _values = new();
    private readonly Dictionary<string, Type> _propertyTypes = new();
    private readonly Action _onSave;

    internal ScriptSettingStore(Action onSave)
    {
        _onSave = onSave;
    }

    /// <summary>注册脚本实例的 [UserSetting] 属性（反射收集，供 UI 与持久化使用）。</summary>
    internal void RegisterProperties(Type scriptType, object instance)
    {
        foreach (var prop in scriptType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var attr = prop.GetCustomAttribute<UserSettingAttribute>();
            if (attr == null)
                continue;

            _propertyTypes[prop.Name] = prop.PropertyType;
            if (_values.ContainsKey(prop.Name))
                continue;

            // 读取当前属性默认值作为初始值
            try
            {
                _values[prop.Name] = prop.GetValue(instance);
            }
            catch
            {
                _values[prop.Name] = DefaultValue(prop.PropertyType);
            }
        }
    }

    /// <summary>所有已注册的设置项（属性名 → 显示名）。</summary>
    public IReadOnlyDictionary<string, string> RegisteredSettings => _registered;

    private Dictionary<string, string> _registered = new();

    internal void SetDisplayNames(Type scriptType)
    {
        var map = new Dictionary<string, string>();
        foreach (var prop in scriptType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var attr = prop.GetCustomAttribute<UserSettingAttribute>();
            if (attr != null)
                map[prop.Name] = attr.DisplayName;
        }
        _registered = map;
    }

    // ── 键值读写（脚本内用）────────────────────────────

    public T Get<T>(string key, T defaultValue = default!)
    {
        if (_values.TryGetValue(key, out var value) && value != null && value is T typed)
            return typed;
        return defaultValue;
    }

    public void Set(string key, object? value)
    {
        _values[key] = value;
        _onSave();
    }

    public bool GetBool(string key, bool defaultValue = false) => Get(key, defaultValue);
    public int GetInt(string key, int defaultValue = 0) => Get(key, defaultValue);
    public float GetFloat(string key, float defaultValue = 0f) => Get(key, defaultValue);
    public string GetString(string key, string defaultValue = "") => Get(key, defaultValue);

    // ── 属性读写（[UserSetting] 属性 getter/setter 用）──

    public object? GetPropertyValue(string name)
        => _values.TryGetValue(name, out var value) ? value : DefaultValue(GetPropertyType(name));

    public void SetPropertyValue(string name, object? value)
    {
        _values[name] = value;
        _onSave();
    }

    public Type GetPropertyType(string name)
        => _propertyTypes.TryGetValue(name, out var type) ? type : typeof(object);

    /// <summary>是否存在该键（含未注册的临时键）。</summary>
    public bool Contains(string key) => _values.ContainsKey(key);

    // ── 持久化 ─────────────────────────────────────────

    internal JsonObject Serialize()
    {
        var obj = new JsonObject();
        foreach (var (key, value) in _values)
        {
            if (value == null)
                continue;
            obj[key] = SerializeValue(value);
        }
        return obj;
    }

    internal void Deserialize(JsonObject? obj)
    {
        if (obj == null)
            return;

        foreach (var (key, node) in obj)
        {
            if (node == null || !_propertyTypes.TryGetValue(key, out var type))
                continue;
            try
            {
                _values[key] = DeserializeValue(node, type);
            }
            catch
            {
                // 单键解析失败不影响其他键
            }
        }
    }

    private static JsonNode? SerializeValue(object value)
    {
        if (value is ScriptColor color)
            return JsonSerializer.SerializeToNode(color.V4);
        if (value.GetType().IsEnum)
            return JsonValue.Create(value.ToString());
        return JsonSerializer.SerializeToNode(value);
    }

    private static object? DeserializeValue(JsonNode node, Type type)
    {
        if (type == typeof(ScriptColor))
        {
            var v4 = JsonSerializer.Deserialize<Vector4?>(node.ToJsonString());
            return v4 is null ? new ScriptColor() : new ScriptColor { V4 = v4.Value };
        }
        if (type.IsEnum)
            return Enum.Parse(type, node.GetValue<string>());
        return node.Deserialize(type);
    }

    private static object? DefaultValue(Type type)
    {
        if (type == typeof(string))
            return "";
        if (type.IsEnum)
            return Enum.GetValues(type).GetValue(0);
        if (type == typeof(Vector4))
            return new Vector4(1f, 1f, 1f, 1f);
        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }
}
