using System.Globalization;
using LibreSpot.Desktop.Models;

namespace LibreSpot.Desktop.ViewModels;

public sealed class EnvironmentSnapshotStateViewModel : ObservableObject
{
    private EnvironmentSnapshot _snapshot = new();
    private DateTime? _refreshedAt;

    public EnvironmentSnapshot Snapshot
    {
        get => _snapshot;
        private set => SetProperty(ref _snapshot, value);
    }

    public DateTime? RefreshedAt
    {
        get => _refreshedAt;
        private set
        {
            if (SetProperty(ref _refreshedAt, value))
            {
                RaiseFreshnessChanged();
            }
        }
    }

    public string LastRefreshedText =>
        RefreshedAt is null
            ? string.Empty
            : $"Last refreshed {RefreshedAt.Value.ToString("T", CultureInfo.CurrentCulture)}";

    public bool IsStale =>
        RefreshedAt is not null &&
        DateTime.Now - RefreshedAt.Value >= TimeSpan.FromMinutes(5);

    public string FreshnessTitle
    {
        get
        {
            if (RefreshedAt is null)
            {
                return "Status not checked yet";
            }

            var age = DateTime.Now - RefreshedAt.Value;
            if (age < TimeSpan.FromMinutes(1))
            {
                return "Environment checked just now";
            }

            if (age < TimeSpan.FromMinutes(3))
            {
                return "Environment checked recently";
            }

            return IsStale ? "Refresh recommended" : "Environment may have changed";
        }
    }

    public string FreshnessDetail
    {
        get
        {
            if (RefreshedAt is null)
            {
                return "Use Refresh environment before you decide whether Spotify, Spicetify, or the saved profile need repair.";
            }

            var refreshedAt = RefreshedAt.Value.ToString("T", CultureInfo.CurrentCulture);
            return IsStale
                ? $"Last checked at {refreshedAt}. Recheck before you repair or reset if anything changed outside LibreSpot."
                : $"Last checked at {refreshedAt}. Refresh after you change Spotify outside LibreSpot.";
        }
    }

    public void Update(EnvironmentSnapshot snapshot, DateTime refreshedAt)
    {
        Snapshot = snapshot;
        RefreshedAt = refreshedAt;
    }

    public void RefreshFreshness() => RaiseFreshnessChanged();

    private void RaiseFreshnessChanged()
    {
        OnPropertyChanged(nameof(LastRefreshedText));
        OnPropertyChanged(nameof(IsStale));
        OnPropertyChanged(nameof(FreshnessTitle));
        OnPropertyChanged(nameof(FreshnessDetail));
    }
}
