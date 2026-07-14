using System.Windows;
using System.Windows.Media.Animation;
using LibreSpot.Desktop.Services;

namespace LibreSpot.Desktop.Controls;

/// <summary>
/// A freeze-safe storyboard animation whose duration is selected when its
/// clock is created. WPF cannot freeze a storyboard that puts a
/// DynamicResource expression directly on Timeline.Duration, while a static
/// duration cannot react when the OS animation or high-contrast setting
/// changes after the template was loaded.
/// </summary>
public sealed class MotionAwareDoubleAnimation : DoubleAnimation
{
    private static readonly Duration ReducedMotionDuration = new(TimeSpan.FromMilliseconds(1));

    public static readonly DependencyProperty StandardDurationProperty = DependencyProperty.Register(
        nameof(StandardDuration),
        typeof(Duration),
        typeof(MotionAwareDoubleAnimation),
        new PropertyMetadata(new Duration(TimeSpan.FromMilliseconds(150))));

    public static readonly DependencyProperty HoldWhenMotionSuppressedProperty = DependencyProperty.Register(
        nameof(HoldWhenMotionSuppressed),
        typeof(bool),
        typeof(MotionAwareDoubleAnimation),
        new PropertyMetadata(false));

    public Duration StandardDuration
    {
        get => (Duration)GetValue(StandardDurationProperty);
        set => SetValue(StandardDurationProperty, value);
    }

    public bool HoldWhenMotionSuppressed
    {
        get => (bool)GetValue(HoldWhenMotionSuppressedProperty);
        set => SetValue(HoldWhenMotionSuppressedProperty, value);
    }

    protected override Duration GetNaturalDurationCore(Clock clock) =>
        ThemeManager.ShouldSuppressMotion ? ReducedMotionDuration : StandardDuration;

    protected override double GetCurrentValueCore(
        double defaultOriginValue,
        double defaultDestinationValue,
        AnimationClock animationClock)
    {
        if (!ThemeManager.ShouldSuppressMotion)
        {
            return base.GetCurrentValueCore(defaultOriginValue, defaultDestinationValue, animationClock);
        }

        return GetSuppressedValue(defaultOriginValue, defaultDestinationValue);
    }

    internal double GetSuppressedValue(double defaultOriginValue, double defaultDestinationValue) =>
        HoldWhenMotionSuppressed
            ? From ?? defaultOriginValue
            : To ?? defaultDestinationValue;

    protected override Freezable CreateInstanceCore() => new MotionAwareDoubleAnimation();
}
