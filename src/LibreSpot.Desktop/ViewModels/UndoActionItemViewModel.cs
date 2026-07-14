using LibreSpot.Desktop.Properties;
using LibreSpot.Desktop.Services;

namespace LibreSpot.Desktop.ViewModels;

public sealed class UndoActionItemViewModel
{
    public UndoActionItemViewModel(OperationJournalUndoItem item)
    {
        Item = item;
        Action = FormatActionLabel(item.Action);
        Phase = string.IsNullOrWhiteSpace(item.Phase) ? Strings.DashboardUnknownValue : item.Phase;
        Target = string.IsNullOrWhiteSpace(item.Target) ? Strings.DashboardUnknownValue : item.Target;
        Result = string.IsNullOrWhiteSpace(item.Result) ? Strings.DashboardUnknownValue : item.Result;
        RollbackHint = !string.IsNullOrWhiteSpace(item.PolicyRefusalReason)
            ? item.PolicyRefusalReason
            : string.IsNullOrWhiteSpace(item.UndoAction) ? item.RollbackHint : item.UndoAction;
        TokenKind = string.IsNullOrWhiteSpace(item.TokenKind) ? Strings.DashboardUnknownValue : FormatActionLabel(item.TokenKind);
        Risk = string.IsNullOrWhiteSpace(item.Risk) ? Strings.DashboardUnknownValue : item.Risk;
    }

    public OperationJournalUndoItem Item { get; }
    public string Action { get; }
    public string Phase { get; }
    public string Target { get; }
    public string Result { get; }
    public string RollbackHint { get; }
    public string TokenKind { get; }
    public string Risk { get; }
    public bool IsExecutable => Item.PolicyExecutable;
    public bool IsSelected { get; set; }
    public string Summary => $"{Result} {TokenKind}: {Target}";

    private static string FormatActionLabel(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Strings.DashboardUnknownValue;
        }

        var trimmed = value.Trim();
        var builder = new System.Text.StringBuilder(trimmed.Length + 8);
        for (var i = 0; i < trimmed.Length; i++)
        {
            var character = trimmed[i];
            if (character is '-' or '_')
            {
                AppendSpace(builder);
                continue;
            }

            if (i > 0
                && char.IsUpper(character)
                && (char.IsLower(trimmed[i - 1]) || char.IsDigit(trimmed[i - 1])))
            {
                AppendSpace(builder);
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    private static void AppendSpace(System.Text.StringBuilder builder)
    {
        if (builder.Length > 0 && builder[^1] != ' ')
        {
            builder.Append(' ');
        }
    }
}
