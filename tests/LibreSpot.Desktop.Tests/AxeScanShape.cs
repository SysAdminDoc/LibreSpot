namespace LibreSpot.Desktop.Tests;

/// <summary>
/// One Axe.Windows pass over the hidden shell, plus enough about the tree it
/// walked to tell a finished window from one that is still filling in.
/// </summary>
/// <remarks>
/// The settle loop compares two passes and stops when they agree. Violation
/// counts alone are not enough to compare on: a state with nothing recorded in
/// the baseline reports zero violations while it is half drawn and zero again
/// when it is done, so two passes agree immediately and the scan is accepted
/// before the content it is meant to check exists. <see cref="ElementsCharted"/>
/// is the number that keeps moving while the window fills, so it carries the
/// signal for those states.
/// </remarks>
internal sealed record AxeScanShape(int WindowsScanned, int ElementsCharted, IReadOnlyList<string> ViolationKeys)
{
    public bool HasSameShapeAs(AxeScanShape other)
    {
        if (WindowsScanned != other.WindowsScanned ||
            ElementsCharted != other.ElementsCharted ||
            ViolationKeys.Count != other.ViolationKeys.Count)
        {
            return false;
        }

        var left = ViolationKeys.GroupBy(key => key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var right = other.ViolationKeys.GroupBy(key => key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        return left.Count == right.Count
            && left.All(pair => right.TryGetValue(pair.Key, out var count) && count == pair.Value);
    }
}
