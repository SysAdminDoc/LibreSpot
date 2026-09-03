using Xunit;

namespace LibreSpot.Desktop.Tests;

/// <summary>
/// Covers the rule the offscreen accessibility scan settles on. Lives outside
/// the Wpf classes on purpose: it launches nothing, so it runs in the fast
/// suite where a regression in the settle rule is caught in seconds rather than
/// in the slow scan that depends on it.
/// </summary>
public sealed class AxeScanShapeTests
{
    [Fact]
    public void AStateWithNoViolationsIsNotSettledWhileItsWindowIsStillFillingIn()
    {
        // This is the state RD-167 created: custom and maintenance record no
        // violations at all. Before the charted element count was part of the
        // shape, these two agreed on the first comparison and the scan was
        // taken roughly 400ms in, against a window that was still drawing.
        var halfDrawn = new AxeScanShape(WindowsScanned: 1, ElementsCharted: 34, ViolationKeys: []);
        var finished = new AxeScanShape(WindowsScanned: 1, ElementsCharted: 212, ViolationKeys: []);

        Assert.False(halfDrawn.HasSameShapeAs(finished));
        Assert.False(finished.HasSameShapeAs(halfDrawn));
    }

    [Fact]
    public void TwoPassesOverTheSameFinishedWindowAreSettled()
    {
        var first = new AxeScanShape(1, 212, ["NameNotNull|List(50008)|(none)"]);
        var second = new AxeScanShape(1, 212, ["NameNotNull|List(50008)|(none)"]);

        Assert.True(first.HasSameShapeAs(second));
    }

    [Fact]
    public void AWindowThatAppearsOrDisappearsIsNotSettled()
    {
        var oneWindow = new AxeScanShape(1, 212, []);
        var twoWindows = new AxeScanShape(2, 212, []);

        Assert.False(oneWindow.HasSameShapeAs(twoWindows));
    }

    [Fact]
    public void TheSameCountOfDifferentViolationsIsNotSettled()
    {
        // Equal totals with different rules means the tree changed under the
        // scan, so comparing counts alone would call a moving window settled.
        var before = new AxeScanShape(1, 212, ["NameNotNull|List(50008)|(none)"]);
        var after = new AxeScanShape(1, 212, ["NameExcludesPrivateUnicodeCharacters|Text(50020)|(none)"]);

        Assert.False(before.HasSameShapeAs(after));
    }

    [Fact]
    public void TheSameRuleAtADifferentCountIsNotSettled()
    {
        const string key = "NameExcludesPrivateUnicodeCharacters|Text(50020)|(none)";
        var two = new AxeScanShape(1, 212, [key, key]);
        var six = new AxeScanShape(1, 212, [key, key, key, key, key, key]);

        Assert.False(two.HasSameShapeAs(six));
    }
}
