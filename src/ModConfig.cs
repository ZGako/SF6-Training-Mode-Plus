using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace SF6_TMP;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNameCaseInsensitive = true,
    AllowTrailingCommas = true,
    ReadCommentHandling = JsonCommentHandling.Skip)]
[JsonSerializable(typeof(JsonObject))]
[JsonSerializable(typeof(JsonNode))]
internal partial class ModConfigJsonContext : JsonSerializerContext
{
}

/// <summary>
///     Represents a hierarchical section in the JSON configuration.
///     Can hold both a bound strongly-typed config object for this section AND nested child sections.
/// </summary>
public class ConfigSection
{
    private readonly Dictionary<string, ConfigSection> _children = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Cached raw JSON node for this section so that modules/features that bind lazily
    ///     (e.g., when entering Training Mode for the first time) immediately receive their loaded values,
    ///     and uninitialized sections are never lost when saving.
    /// </summary>
    private JsonObject _cachedNode = new();

    private Action<JsonObject>? _deserializeSelf;
    private Func<JsonObject?>? _serializeSelf;

    internal ConfigSection()
    {
    }

    /// <summary>
    ///     Retrieves or creates a nested child section under the given key.
    /// </summary>
    public ConfigSection GetOrCreateSection(string key)
    {
        if (!_children.TryGetValue(key, out var child))
        {
            child = new ConfigSection();
            if (_cachedNode.TryGetPropertyValue(key, out var existingNode) && existingNode is JsonObject existingObj)
                child._cachedNode = existingObj.DeepClone().AsObject();

            _children[key] = child;
        }

        return child;
    }

    /// <summary>
    ///     Binds a strongly-typed config object directly to this section.
    ///     If the config file was already loaded, <paramref name="setter" /> is immediately invoked with the deserialized
    ///     data.
    /// </summary>
    public void Bind<T>(Func<T> getter, Action<T> setter, JsonTypeInfo<T> typeInfo) where T : class, new()
    {
        _serializeSelf = () =>
        {
            var current = getter();
            return JsonSerializer.SerializeToNode(current, typeInfo) as JsonObject;
        };

        _deserializeSelf = jsonObj =>
        {
            try
            {
                // Clone the object without child section keys before deserializing into T
                var cleanObj = new JsonObject();
                foreach (var kvp in jsonObj)
                {
                    if (_children.ContainsKey(kvp.Key)) continue;
                    cleanObj[kvp.Key] = kvp.Value?.DeepClone();
                }

                if (cleanObj.Count > 0)
                {
                    var loaded = cleanObj.Deserialize(typeInfo);
                    if (loaded != null)
                    {
                        setter(loaded);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                API.LogError($"Failed to deserialize config section ({typeof(T).Name}), using defaults: {ex.Message}");
            }

            setter(getter() ?? new T());
        };

        // Immediately hydrate from cached JSON if ModConfig.Load() has already run
        if (_cachedNode.Count > 0) _deserializeSelf(_cachedNode);
    }

    /// <summary>
    ///     Binds a strongly-typed config object to a named child section under this section,
    ///     and returns that child section in case further sub-sections need to be registered under it.
    /// </summary>
    public ConfigSection BindSection<T>(
        string key,
        Func<T> getter,
        Action<T> setter,
        JsonTypeInfo<T> typeInfo) where T : class, new()
    {
        var section = GetOrCreateSection(key);
        section.Bind(getter, setter, typeInfo);
        return section;
    }

    internal void LoadFromNode(JsonObject node)
    {
        _cachedNode = node.DeepClone().AsObject();

        // 1. Deserialize this section's own config (if bound)
        _deserializeSelf?.Invoke(_cachedNode);

        // 2. Propagate to any registered child sections
        foreach (var (key, childSection) in _children)
            if (_cachedNode.TryGetPropertyValue(key, out var childNode) && childNode is JsonObject childObj)
                childSection.LoadFromNode(childObj);
            else
                childSection.LoadFromNode(new JsonObject());
    }

    internal JsonObject SaveToNode()
    {
        // Start from cached node so any sections that haven't been initialized yet keep their saved JSON data
        var result = _cachedNode.DeepClone().AsObject();

        // 1. Merge this section's own bound properties
        if (_serializeSelf != null)
        {
            var selfObj = _serializeSelf();
            if (selfObj != null)
                foreach (var kvp in selfObj)
                    result[kvp.Key] = kvp.Value?.DeepClone();
        }

        // 2. Merge all registered child sections
        foreach (var (key, childSection) in _children) result[key] = childSection.SaveToNode();

        _cachedNode = result.DeepClone().AsObject();
        return result;
    }

    internal void ClearSection()
    {
        foreach (var child in _children.Values) child.ClearSection();
        _children.Clear();
        _cachedNode = new JsonObject();
        _serializeSelf = null;
        _deserializeSelf = null;
    }
}

public class ModConfig : ConfigSection
{
    private static readonly string ConfigFilePath =
        Path.Combine(TrainingModePlus.DirectoryPath, "SF6_TrainingModePlus_Config.json");

    public static ModConfig Instance { get; private set; } = new();

    // Event fired whenever a setting is changed in your live setup screen
    public static event Action<ModConfig>? OnConfigChanged;

    public static void Load()
    {
        try
        {
            if (File.Exists(ConfigFilePath))
            {
                var json = File.ReadAllText(ConfigFilePath);
                var rootNode = JsonSerializer.Deserialize(json, ModConfigJsonContext.Default.JsonObject) ??
                               new JsonObject();
                Instance.LoadFromNode(rootNode);
            }
            else
            {
                Instance.LoadFromNode(new JsonObject());
            }

            // save in case of new fields or no config present
            Save();
        }
        catch (Exception ex)
        {
            API.LogError($"Failed to load ModConfig, using defaults: {ex.Message}");
            Instance ??= new ModConfig();
            Instance.LoadFromNode(new JsonObject());
        }
    }

    public static void Save()
    {
        try
        {
            if (Instance == null) return;

            var directory = Path.GetDirectoryName(ConfigFilePath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            var rootNode = Instance.SaveToNode();
            var json = JsonSerializer.Serialize(rootNode, ModConfigJsonContext.Default.JsonObject);
            File.WriteAllText(ConfigFilePath, json);
        }
        catch (Exception ex)
        {
            API.LogError($"Failed to save ModConfig: {ex.Message}");
        }
    }

    /// <summary>
    ///     Call this from your live setup menu whenever a value is tweaked mid-match.
    /// </summary>
    public static void NotifyChanged()
    {
        if (Instance != null) OnConfigChanged?.Invoke(Instance);
    }

    /// <summary>
    ///     Call this inside your [PluginExitPoint] method to release static delegates and references.
    /// </summary>
    public static void Clear()
    {
        Instance?.ClearSection();
        OnConfigChanged = null;
        Instance = null!;
    }
}