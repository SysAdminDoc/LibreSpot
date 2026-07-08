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
            : ViewModelText.Format("Vm_EnvironmentLastRefreshedFormat", RefreshedAt.Value.ToString("T", CultureInfo.CurrentCulture));

    public bool IsStale =>
        RefreshedAt is not null &&
        DateTime.Now - RefreshedAt.Value >= TimeSpan.FromMinutes(5);

    public string FreshnessTitle
    {
        get
        {
            if (RefreshedAt is null)
            {
                return ViewModelText.Get("Vm_EnvironmentStatusUnchecked");
            }

            var age = DateTime.Now - RefreshedAt.Value;
            if (age < TimeSpan.FromMinutes(1))
            {
                return ViewModelText.Get("Vm_EnvironmentCheckedJustNow");
            }

            if (age < TimeSpan.FromMinutes(3))
            {
                return ViewModelText.Get("Vm_EnvironmentCheckedRecently");
            }

            return IsStale
                ? ViewModelText.Get("Vm_EnvironmentRefreshRecommended")
                : ViewModelText.Get("Vm_EnvironmentMayHaveChanged");
        }
    }

    public string FreshnessDetail
    {
        get
        {
            if (RefreshedAt is null)
            {
                return ViewModelText.Get("Vm_EnvironmentUncheckedDetail");
            }

            var refreshedAt = RefreshedAt.Value.ToString("T", CultureInfo.CurrentCulture);
            return IsStale
                ? ViewModelText.Format("Vm_EnvironmentStaleDetailFormat", refreshedAt)
                : ViewModelText.Format("Vm_EnvironmentFreshDetailFormat", refreshedAt);
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
