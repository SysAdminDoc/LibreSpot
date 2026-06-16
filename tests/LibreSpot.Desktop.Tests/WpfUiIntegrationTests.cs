using System.Reflection;
using System.Windows;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class WpfUiIntegrationTests
{
    [Fact]
    public void WpfUiAssembly_LoadsAndExposesTartetControls()
    {
        var asm = Assembly.Load("Wpf.Ui");
        Assert.NotNull(asm);

        var expectedControls = new[]
        {
            "Wpf.Ui.Controls.TitleBar",
            "Wpf.Ui.Controls.InfoBar",
            "Wpf.Ui.Controls.NumberBox",
            "Wpf.Ui.Controls.SplitButton",
            "Wpf.Ui.Controls.Snackbar",
            "Wpf.Ui.Controls.SnackbarPresenter",
        };

        foreach (var typeName in expectedControls)
        {
            var type = asm.GetType(typeName);
            Assert.True(type is not null, $"WPF-UI 4.3.0 must expose {typeName}.");
            Assert.True(typeof(FrameworkElement).IsAssignableFrom(type), $"{typeName} must derive from FrameworkElement.");
        }
    }

    [Fact]
    public void WpfUiControls_InstantiateWithoutThrowingOnParameterlessCtors()
    {
        RunSta(() =>
        {
            EnsureApplication();

            var asm = Assembly.Load("Wpf.Ui");
            var parameterlessControls = new[]
            {
                "Wpf.Ui.Controls.TitleBar",
                "Wpf.Ui.Controls.InfoBar",
                "Wpf.Ui.Controls.NumberBox",
                "Wpf.Ui.Controls.SplitButton",
                "Wpf.Ui.Controls.SnackbarPresenter",
            };

            foreach (var typeName in parameterlessControls)
            {
                var type = asm.GetType(typeName)!;
                var instance = Activator.CreateInstance(type) as FrameworkElement;
                Assert.True(instance is not null, $"{typeName} must instantiate via parameterless constructor.");
            }
        });
    }

    [Fact]
    public void LibreSpotPalette_LoadsAlongsideWpfUiWithoutConflict()
    {
        RunSta(() =>
        {
            EnsureApplication();

            var resources = new ResourceDictionary();
            resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/LibreSpot;component/Themes/Palette.xaml")
            });
            resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/LibreSpot;component/Themes/Controls.xaml")
            });

            Assert.True(resources.Contains("CanvasBrush"), "Palette must resolve CanvasBrush.");
            Assert.True(resources.Contains("AccentBrush"), "Palette must resolve AccentBrush.");
            Assert.True(resources.Contains("MotionMedDuration"), "Palette must resolve MotionMedDuration.");
        });
    }

    private static void EnsureApplication()
    {
        if (Application.Current == null)
        {
            new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        }
    }

    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
