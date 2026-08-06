using System.Text.Json;

namespace ArbuzTweaker;

internal sealed class CrosshairPresetService
{
    private const string PresetsFileName = "crosshair-presets.json";
    private const string DefaultPresetName = "Шаблон 1";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _presetsPath;
    private bool _lastLoadFailed;

    public CrosshairPresetService(ConfigService configService)
    {
        _presetsPath = Path.Combine(configService.ConfigsPath, PresetsFileName);
    }

    public IReadOnlyList<CrosshairPresetData> LoadPresets(CrosshairShape shape)
    {
        var store = LoadStore();
        return GetShapePresets(store, shape)
            .EnsureDefaultPreset(store, shape, SaveStore)
            .OrderBy(preset => preset.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public bool PresetExists(CrosshairShape shape, string name)
    {
        var store = LoadStore();
        return GetShapePresets(store, shape)
            .Any(item => string.Equals(item.Name, name, StringComparison.CurrentCultureIgnoreCase));
    }

    public void SavePreset(CrosshairShape shape, CrosshairPresetData preset)
    {
        var store = LoadStore();
        var presets = GetShapePresets(store, shape).EnsureDefaultPreset(store, shape, SaveStore);
        var existingIndex = presets.FindIndex(item => string.Equals(item.Name, preset.Name, StringComparison.CurrentCultureIgnoreCase));

        if (existingIndex >= 0)
            presets[existingIndex] = preset;
        else
            presets.Add(preset);

        SaveStore(store);
    }

    public void RenamePreset(CrosshairShape shape, string oldName, string newName)
    {
        var store = LoadStore();
        var presets = GetShapePresets(store, shape).EnsureDefaultPreset(store, shape, SaveStore);
        var preset = presets.FirstOrDefault(item => string.Equals(item.Name, oldName, StringComparison.CurrentCultureIgnoreCase));
        if (preset == null)
            return;

        preset.Name = newName;
        SaveStore(store);
    }

    public void DeletePreset(CrosshairShape shape, string name)
    {
        var store = LoadStore();
        var presets = GetShapePresets(store, shape).EnsureDefaultPreset(store, shape, SaveStore);
        if (presets.Count <= 1)
            return;

        presets.RemoveAll(item => string.Equals(item.Name, name, StringComparison.CurrentCultureIgnoreCase));
        SaveStore(store);
    }

    public static CrosshairPresetData CreateDefaultPreset()
    {
        return new CrosshairPresetData
        {
            Name = DefaultPresetName,
            Size = 14,
            Gap = 4,
            Thickness = 2,
            OpacityPercent = 100,
            ColorArgb = Color.White.ToArgb(),
            OutlineColorArgb = Color.Black.ToArgb(),
            ShowCenterDot = true,
            ShowOutline = false
        };
    }

    private CrosshairPresetStore LoadStore()
    {
        try
        {
            _lastLoadFailed = false;

            if (!File.Exists(_presetsPath))
                return new CrosshairPresetStore();

            var content = File.ReadAllText(_presetsPath);
            return JsonSerializer.Deserialize<CrosshairPresetStore>(content) ?? new CrosshairPresetStore();
        }
        catch
        {
            // Файл существует, но прочитать его не удалось (занят, битый JSON, права).
            // Пометка запрещает перезаписывать его пустым store — иначе одна неудачная
            // загрузка уничтожала все сохранённые шаблоны.
            _lastLoadFailed = true;
            return new CrosshairPresetStore();
        }
    }

    private void SaveStore(CrosshairPresetStore store)
    {
        if (_lastLoadFailed)
            return;

        try
        {
            var directory = Path.GetDirectoryName(_presetsPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var content = JsonSerializer.Serialize(store, JsonOptions);
            var tempPath = _presetsPath + ".tmp";
            File.WriteAllText(tempPath, content);
            File.Move(tempPath, _presetsPath, true);
        }
        catch
        {
        }
    }

    private static List<CrosshairPresetData> GetShapePresets(CrosshairPresetStore store, CrosshairShape shape)
    {
        store.PresetsByShape ??= new Dictionary<string, List<CrosshairPresetData>>(StringComparer.OrdinalIgnoreCase);
        var key = shape.ToString();

        if (!store.PresetsByShape.TryGetValue(key, out var presets) || presets == null)
        {
            presets = new List<CrosshairPresetData>();
            store.PresetsByShape[key] = presets;
        }

        return presets;
    }
}

internal static class CrosshairPresetListExtensions
{
    public static List<CrosshairPresetData> EnsureDefaultPreset(
        this List<CrosshairPresetData> presets,
        CrosshairPresetStore store,
        CrosshairShape shape,
        Action<CrosshairPresetStore> saveStore)
    {
        if (presets.Count > 0)
            return presets;

        presets.Add(CrosshairPresetService.CreateDefaultPreset());
        saveStore(store);
        return presets;
    }
}

internal sealed class CrosshairPresetStore
{
    public Dictionary<string, List<CrosshairPresetData>> PresetsByShape { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

internal sealed class CrosshairPresetData
{
    public string Name { get; set; } = string.Empty;
    public int Size { get; set; }
    public int Gap { get; set; }
    public int Thickness { get; set; }
    public int OpacityPercent { get; set; } = 100;

    // Без явного дефолта отсутствующее в JSON поле давало ARGB(0,0,0,0) —
    // полностью прозрачный прицел, который «включён», но невидим.
    public int ColorArgb { get; set; } = Color.White.ToArgb();

    public int OutlineColorArgb { get; set; } = Color.Black.ToArgb();
    public bool ShowCenterDot { get; set; } = true;
    public bool ShowOutline { get; set; }

    public static CrosshairPresetData FromSettings(string name, CrosshairSettings settings)
    {
        return new CrosshairPresetData
        {
            Name = name,
            Size = settings.Size,
            Gap = settings.Gap,
            Thickness = settings.Thickness,
            OpacityPercent = settings.OpacityPercent,
            ColorArgb = settings.Color.ToArgb(),
            OutlineColorArgb = settings.OutlineColor.ToArgb(),
            ShowCenterDot = settings.ShowCenterDot,
            ShowOutline = settings.ShowOutline
        };
    }
}
