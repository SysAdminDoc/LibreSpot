using LibreSpot.Desktop.Properties;
using LibreSpot.Desktop.ViewModels;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class ViewModelStateDomainTests
{
    [Fact]
    public async Task PromptState_ConfirmClearsPromptAndRunsAction()
    {
        var state = new PromptStateViewModel();
        var ran = false;

        state.Show(
            "Apply changes",
            "LibreSpot will apply the selected profile.",
            "Apply profile",
            "Keep editing",
            isDestructive: false,
            () =>
            {
                ran = true;
                return Task.CompletedTask;
            },
            "Summary",
            "Summary body");

        Assert.True(state.IsVisible);
        Assert.True(state.IsConfirmDefault);
        Assert.Equal("Apply changes", state.Title);
        Assert.Equal("Apply profile", state.ConfirmText);

        await state.ConfirmAsync();

        Assert.True(ran);
        Assert.False(state.IsVisible);
        Assert.False(state.IsConfirmDefault);
        Assert.Equal(Strings.ButtonContinue, state.ConfirmText);
        Assert.Equal(Strings.ButtonCancel, state.CancelText);
    }

    [Fact]
    public void PromptState_CancelClearsDestructivePromptWithoutRunningAction()
    {
        var state = new PromptStateViewModel();
        var ran = false;

        state.Show(
            "Reset LibreSpot",
            "This removes local customization state.",
            "Reset",
            "Keep setup",
            isDestructive: true,
            () =>
            {
                ran = true;
                return Task.CompletedTask;
            });

        Assert.True(state.IsVisible);
        Assert.True(state.IsDestructive);
        Assert.False(state.IsConfirmDefault);

        state.Cancel();

        Assert.False(ran);
        Assert.False(state.IsVisible);
        Assert.False(state.IsDestructive);
        Assert.Equal(string.Empty, state.Title);
    }

    [Fact]
    public void SettingsSearchState_MatchesTitleAndDescriptionCaseInsensitively()
    {
        var state = new SettingsSearchStateViewModel
        {
            Text = "  lyrics  "
        };

        Assert.True(state.HasText);
        Assert.Equal("lyrics", state.Query);
        Assert.True(state.Matches("Lyrics theme", "SpotX restores this skin after patching."));
        Assert.True(state.Matches("Theme pack", "Enable dynamic synced LYRICS."));
        Assert.False(state.Matches("Cache limit", "Leave 0 for default behavior."));

        state.Text = string.Empty;

        Assert.False(state.HasText);
        Assert.True(state.Matches("Cache limit", "Leave 0 for default behavior."));
    }
}
