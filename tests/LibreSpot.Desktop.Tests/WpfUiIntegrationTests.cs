using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class WpfUiIntegrationTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();

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

    [Fact]
    public void HighContrastPalette_RendersRepresentativeControlsOffscreen()
    {
        RunSta(() =>
        {
            EnsureApplication();
            var appResources = Application.Current.Resources;
            var originalDictionaries = appResources.MergedDictionaries.ToList();
            appResources.MergedDictionaries.Clear();
            try
            {
                appResources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/LibreSpot;component/Themes/HighContrastPalette.xaml")
                });
                appResources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/LibreSpot;component/Themes/Controls.xaml")
                });

                var root = BuildHighContrastSmokeSurface(appResources);
                root.Measure(new Size(720, 640));
                root.Arrange(new Rect(0, 0, 720, 640));
                root.UpdateLayout();

                foreach (var control in FindFocusableControls(root))
                {
                    Assert.False(
                        string.IsNullOrWhiteSpace(AutomationProperties.GetName(control)),
                        $"{control.GetType().Name} must expose an automation name under high contrast.");
                }

                var rendered = new RenderTargetBitmap(720, 640, 96, 96, PixelFormats.Pbgra32);
                rendered.Render(root);
                AssertNonBlankRender(rendered);
            }
            finally
            {
                appResources.MergedDictionaries.Clear();
                foreach (var dictionary in originalDictionaries)
                {
                    appResources.MergedDictionaries.Add(dictionary);
                }
            }
        });
    }

    [Fact]
    public void WpfShell_UsesSnackbarPresenterForCompletionFeedback()
    {
        var xaml = ReadRepoFile("src", "LibreSpot.Desktop", "MainWindow.xaml");
        var codeBehind = ReadRepoFile("src", "LibreSpot.Desktop", "MainWindow.xaml.cs");

        Assert.Contains("xmlns:ui=\"clr-namespace:Wpf.Ui.Controls;assembly=Wpf.Ui\"", xaml);
        Assert.Contains("<ui:SnackbarPresenter x:Name=\"CompletionSnackbarPresenter\"", xaml);
        Assert.Contains("AutomationProperties.Name=\"{services:Loc Ui_CompletionNotifications}\"", xaml);
        Assert.Contains("new Snackbar(CompletionSnackbarPresenter)", codeBehind);
        Assert.Contains("ControlAppearance.Success", codeBehind);
        Assert.Contains("ControlAppearance.Caution", codeBehind);
        Assert.Contains("ControlAppearance.Danger", codeBehind);
    }

    [Fact]
    public void WpfShell_ExposesTaskbarThumbnailActions()
    {
        var xaml = ReadRepoFile("src", "LibreSpot.Desktop", "MainWindow.xaml");

        Assert.Contains("<TaskbarItemInfo.ThumbButtonInfos>", xaml);
        Assert.Contains("ShowRecommendedWorkspaceCommand", xaml);
        Assert.Contains("ShowCustomWorkspaceCommand", xaml);
        Assert.Contains("ShowMaintenanceWorkspaceCommand", xaml);
        Assert.Contains("ImportProfileCommand", xaml);
        Assert.Contains("OpenLibreSpotFolderCommand", xaml);
    }

    [Fact]
    public void WpfShell_ExposesCustomPatchEditor()
    {
        var xaml = ReadRepoFile("src", "LibreSpot.Desktop", "MainWindow.xaml");
        var codeBehind = ReadRepoFile("src", "LibreSpot.Desktop", "MainWindow.xaml.cs");

        Assert.Contains("ICSharpCode.AvalonEdit", xaml);
        Assert.Contains("CustomPatchesTextEditor", xaml);
        Assert.Contains("ValidateCustomPatchesCommand", xaml);
        Assert.Contains("FormatCustomPatchesCommand", xaml);
        Assert.Contains("ImportCustomPatchesFromUrlCommand", xaml);
        Assert.Contains("CustomPatchesTextEditor_OnTextChanged", codeBehind);
        Assert.Contains("SyncCustomPatchesEditorText", codeBehind);
    }

    [Fact]
    public void WpfShell_MinimizesToTrayAndUsesClickableTrayNotifications()
    {
        var codeBehind = ReadRepoFile("src", "LibreSpot.Desktop", "MainWindow.xaml.cs");

        Assert.Contains("Forms.NotifyIcon", codeBehind);
        Assert.Contains("HideToTray", codeBehind);
        Assert.Contains("BalloonTipClicked", codeBehind);
        Assert.Contains("ShowTrayCompletionNotification", codeBehind);
        Assert.Contains("RestoreFromTray", codeBehind);
    }

    private static void EnsureApplication()
    {
        if (Application.Current == null)
        {
            new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        }
    }

    private static FrameworkElement BuildHighContrastSmokeSurface(ResourceDictionary resources)
    {
        var panel = new StackPanel
        {
            Width = 720,
            MinHeight = 640,
            Background = (Brush)resources["CanvasBrush"],
            Margin = new Thickness(0)
        };

        panel.Children.Add(new TextBlock
        {
            Text = "Recommended setup",
            Style = (Style)resources["HeadlineTextStyle"],
            Margin = new Thickness(16, 14, 16, 8)
        });
        panel.Children.Add(NamedButton("Run recommended setup", (Style)resources["PrimaryButtonStyle"]));
        panel.Children.Add(NamedButton("Disabled maintenance action", (Style)resources["SecondaryButtonStyle"], isEnabled: false));
        var marketplace = new CheckBox
        {
            Content = "Install Marketplace",
            Style = (Style)resources["SettingCheckBoxStyle"],
            IsChecked = true,
            Margin = new Thickness(16, 8, 16, 0)
        };
        AutomationProperties.SetName(marketplace, "Install Marketplace");
        panel.Children.Add(marketplace);

        var combo = new ComboBox
        {
            Style = (Style)resources["ComboBoxStylePremium"],
            Margin = new Thickness(16, 8, 16, 0),
            Width = 260,
            ItemsSource = new[] { "Default", "High contrast" },
            SelectedIndex = 0
        };
        AutomationProperties.SetName(combo, "Theme scheme");
        panel.Children.Add(combo);

        var search = new TextBox
        {
            Style = (Style)resources["TextBoxStylePremium"],
            Text = "theme search",
            Width = 260,
            Margin = new Thickness(16, 8, 16, 0)
        };
        AutomationProperties.SetName(search, "Search themes and schemes");
        panel.Children.Add(search);

        panel.Children.Add(new Border
        {
            Style = (Style)resources["SubtleCardStyle"],
            Margin = new Thickness(16, 12, 16, 0),
            Child = new StackPanel
            {
                Children =
                {
                    new TextBlock { Text = "Health card", Style = (Style)resources["SectionTitleTextStyle"] },
                    new TextBlock { Text = "Marketplace files installed", Style = (Style)resources["CaptionTextStyle"] }
                }
            }
        });

        var log = new TextBox
        {
            Style = (Style)resources["LogTextBoxStyle"],
            Text = "[WARN] Network download failed; using verified cached copy.\n[INFO] Run complete.",
            Width = 640,
            Height = 96,
            IsReadOnly = true,
            Margin = new Thickness(16, 12, 16, 0)
        };
        AutomationProperties.SetName(log, "Run log");
        panel.Children.Add(log);

        panel.Children.Add(new Border
        {
            Style = (Style)resources["SurfaceCardStyle"],
            Margin = new Thickness(16, 12, 16, 0),
            Child = new StackPanel
            {
                Children =
                {
                    new TextBlock { Text = "Decision prompt", Style = (Style)resources["SectionTitleTextStyle"] },
                    NamedButton("Cancel", (Style)resources["SecondaryButtonStyle"]),
                    NamedButton("Confirm", (Style)resources["DestructiveButtonStyle"])
                }
            }
        });

        var snackbar = new Wpf.Ui.Controls.SnackbarPresenter
        {
            Width = 640,
            Height = 44,
            Margin = new Thickness(16, 12, 16, 0)
        };
        AutomationProperties.SetName(snackbar, "Completion notifications");
        panel.Children.Add(snackbar);

        return panel;
    }

    private static Button NamedButton(string name, Style style, bool isEnabled = true)
    {
        var button = new Button
        {
            Content = name,
            Style = style,
            Width = 260,
            MinHeight = 40,
            Margin = new Thickness(16, 8, 16, 0),
            IsEnabled = isEnabled
        };
        AutomationProperties.SetName(button, name);
        return button;
    }

    private static IEnumerable<Control> FindFocusableControls(DependencyObject root)
    {
        if (root is Control { Focusable: true } control)
        {
            yield return control;
        }

        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            foreach (var descendant in FindFocusableControls(VisualTreeHelper.GetChild(root, index)))
            {
                yield return descendant;
            }
        }
    }

    private static void AssertNonBlankRender(BitmapSource bitmap)
    {
        var stride = bitmap.PixelWidth * 4;
        var pixels = new byte[stride * bitmap.PixelHeight];
        bitmap.CopyPixels(pixels, stride, 0);
        var nonTransparentPixels = 0;
        var uniqueColors = new HashSet<int>();

        for (var index = 0; index < pixels.Length; index += 4)
        {
            if (pixels[index + 3] == 0)
            {
                continue;
            }

            nonTransparentPixels++;
            uniqueColors.Add(pixels[index] | (pixels[index + 1] << 8) | (pixels[index + 2] << 16));
        }

        Assert.True(nonTransparentPixels > 10_000, $"High-contrast smoke surface rendered too few pixels: {nonTransparentPixels}.");
        Assert.True(uniqueColors.Count >= 3, $"High-contrast smoke surface rendered too few distinct colors: {uniqueColors.Count}.");
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

    private static string ReadRepoFile(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { RepoRoot }.Concat(parts).ToArray()));

    private static string ResolveRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "LibreSpot.ps1")) &&
                Directory.Exists(Path.Combine(directory.FullName, "src")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not resolve repository root.");
    }
}
