using LibreSpot.Desktop.Properties;
using LibreSpot.Desktop.Models;
using LibreSpot.Desktop.Services;
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

    [Fact]
    public void ActivityRunState_BeginAndNoticeOwnProgressVisibilityAndStatus()
    {
        var state = new ActivityRunStateViewModel();

        state.Begin("Applying profile", "Preparing backend", "Preparing");

        Assert.True(state.IsVisible);
        Assert.True(state.IsRunning);
        Assert.False(state.IsCancelRequested);
        Assert.Equal(0, state.ProgressValue);
        Assert.Equal("Applying profile", state.Title);
        Assert.Equal("Preparing backend", state.Status);
        Assert.Equal("Preparing", state.Step);

        state.ShowNotice("Run complete", "Done", "No backend started");

        Assert.True(state.IsVisible);
        Assert.Equal("Run complete", state.Title);
        Assert.Equal("Done", state.Status);
        Assert.Equal("No backend started", state.Step);
        Assert.Equal(0, state.ProgressValue);
    }

    [Fact]
    public void ActivityRunState_AppendsTrimsAndClearsLogLines()
    {
        var state = new ActivityRunStateViewModel();
        var timestamp = new DateTime(2026, 6, 29, 10, 0, 0);

        for (var i = 0; i < 2005; i++)
        {
            state.AppendLog($"line {i}", "info", timestamp);
        }

        Assert.Equal(2000, state.LogEntries.Count);
        Assert.Equal("line 5", state.LogEntries[0].Message);
        Assert.Equal("2000 log lines", state.LogLineCountText);
        Assert.False(state.IsLogEmpty);

        state.ClearLog();

        Assert.Empty(state.LogEntries);
        Assert.True(state.IsLogEmpty);
        Assert.Equal("No log output yet", state.LogLineCountText);
    }

    [Fact]
    public void ActivityRunState_ReplacesUndoItemsAndClearsState()
    {
        var state = new ActivityRunStateViewModel();
        var item = new OperationJournalUndoItem(
            "phase",
            "EnableAutoReapply",
            "task",
            "LibreSpot\\ReapplyWatcher",
            "Registered",
            "Unregister the task.");

        state.ReplaceUndoActionItems(new[] { item });

        Assert.True(state.HasUndoActionItems);
        Assert.Single(state.UndoActionItems);

        state.ClearUndoActionItems();

        Assert.False(state.HasUndoActionItems);
        Assert.Empty(state.UndoActionItems);
    }

    [Fact]
    public void EnvironmentSnapshotState_DefaultsToUncheckedFreshness()
    {
        var state = new EnvironmentSnapshotStateViewModel();

        Assert.False(state.Snapshot.SpotifyInstalled);
        Assert.Equal(string.Empty, state.LastRefreshedText);
        Assert.False(state.IsStale);
        Assert.Equal("Status not checked yet", state.FreshnessTitle);
        Assert.Contains("Use Refresh environment", state.FreshnessDetail);
    }

    [Fact]
    public void EnvironmentSnapshotState_UpdateOwnsSnapshotAndFreshnessCopy()
    {
        var state = new EnvironmentSnapshotStateViewModel();
        var refreshedAt = DateTime.Now;

        state.Update(
            new EnvironmentSnapshot
            {
                SpotifyInstalled = true,
                SpicetifyInstalled = true,
                AutoReapplyTaskRegistered = true
            },
            refreshedAt);

        Assert.True(state.Snapshot.SpotifyInstalled);
        Assert.True(state.Snapshot.SpicetifyInstalled);
        Assert.True(state.Snapshot.AutoReapplyTaskRegistered);
        Assert.StartsWith("Last refreshed ", state.LastRefreshedText);
        Assert.False(state.IsStale);
        Assert.Equal("Environment checked just now", state.FreshnessTitle);
        Assert.Contains("Refresh after you change Spotify", state.FreshnessDetail);
    }

    [Fact]
    public void EnvironmentSnapshotState_StaleThresholdChangesGuidance()
    {
        var state = new EnvironmentSnapshotStateViewModel();

        state.Update(new EnvironmentSnapshot(), DateTime.Now - TimeSpan.FromMinutes(6));

        Assert.True(state.IsStale);
        Assert.Equal("Refresh recommended", state.FreshnessTitle);
        Assert.Contains("Recheck before you repair or reset", state.FreshnessDetail);
    }
}
