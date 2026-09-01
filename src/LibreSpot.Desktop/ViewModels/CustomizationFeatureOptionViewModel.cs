using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using LibreSpot.Desktop.Models;

namespace LibreSpot.Desktop.ViewModels;

public sealed class CustomizationFeatureOptionViewModel : ObservableObject
{
    private bool _isOverrideEnabled;
    private bool _booleanValue;
    private string _valueText;

    public CustomizationFeatureOptionViewModel(CustomizationFeatureDefinition definition)
    {
        Definition = definition;
        Choices = new ObservableCollection<string>(definition.Values ?? []);
        _valueText = DefaultText;
        _booleanValue = definition.Default.ValueKind == JsonValueKind.True;
    }

    public CustomizationFeatureDefinition Definition { get; }
    public string Name => Definition.Name;
    public string Description => Definition.Description;
    public string Group => Definition.Group;
    public string ValueType => Definition.Type;
    public ObservableCollection<string> Choices { get; }
    public bool IsBoolean => string.Equals(ValueType, "bool", StringComparison.Ordinal);
    public bool IsEnum => string.Equals(ValueType, "enum", StringComparison.Ordinal);
    public bool IsNumber => string.Equals(ValueType, "number", StringComparison.Ordinal);
    public bool IsString => !IsBoolean && !IsEnum && !IsNumber;
    public bool IsServerGated => Definition.ServerGated;
    public bool HasSpotXForcedValue => Definition.SpotXForced is not null;

    public string DefaultText => FormatJsonValue(Definition.Default);

    public string RangeText => IsNumber && (Definition.Minimum.HasValue || Definition.Maximum.HasValue)
        ? string.Create(
            CultureInfo.CurrentCulture,
            $"{Definition.Minimum?.ToString(CultureInfo.CurrentCulture) ?? "−∞"} to {Definition.Maximum?.ToString(CultureInfo.CurrentCulture) ?? "+∞"}")
        : string.Empty;

    public string SpotXForcedText => Definition.SpotXForced is null
        ? string.Empty
        : $"SpotX {Definition.SpotXForced.Mode}: {FormatJsonValue(Definition.SpotXForced.Value)}";

    public bool IsOverrideEnabled
    {
        get => _isOverrideEnabled;
        set => SetProperty(ref _isOverrideEnabled, value);
    }

    public bool BooleanValue
    {
        get => _booleanValue;
        set => SetProperty(ref _booleanValue, value);
    }

    public string ValueText
    {
        get => _valueText;
        set
        {
            if (SetProperty(ref _valueText, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(IsValueValid));
            }
        }
    }

    public bool IsValueValid => !IsNumber || TryParseNumber(ValueText, out _);

    public void LoadOverride(JsonElement? value)
    {
        IsOverrideEnabled = value.HasValue;
        var resolved = value ?? Definition.Default;
        if (IsBoolean)
        {
            BooleanValue = resolved.ValueKind == JsonValueKind.True;
            return;
        }

        ValueText = FormatJsonValue(resolved);
    }

    public object GetSerializableValue()
    {
        if (IsBoolean)
        {
            return BooleanValue;
        }

        if (IsNumber)
        {
            if (!TryParseNumber(ValueText, out var number))
            {
                number = Definition.Default.TryGetDouble(out var fallback) ? fallback : 0;
            }

            if (Definition.Minimum.HasValue)
            {
                number = Math.Max(number, Definition.Minimum.Value);
            }

            if (Definition.Maximum.HasValue)
            {
                number = Math.Min(number, Definition.Maximum.Value);
            }

            return number;
        }

        return ValueText;
    }

    public bool Matches(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
               Description.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
               Group.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
               SpotXForcedText.Contains(query, StringComparison.CurrentCultureIgnoreCase);
    }

    private static bool TryParseNumber(string value, out double result) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result) ||
        double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out result);

    private static string FormatJsonValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() ?? string.Empty,
        JsonValueKind.True => bool.TrueString,
        JsonValueKind.False => bool.FalseString,
        JsonValueKind.Number => value.GetRawText(),
        _ => value.GetRawText()
    };
}
