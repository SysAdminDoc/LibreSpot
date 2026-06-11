using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class ReleaseWorkflowTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();

    private static string ResolveRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "LibreSpot.ps1")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("Could not locate repo root from the test runner.");
    }

    private static string ReadWorkflow() =>
        File.ReadAllText(Path.Combine(RepoRoot, ".github", "workflows", "release.yml"));

    [Fact]
    public void ReleaseWorkflow_HasRuntimeLifecycleGate()
    {
        var workflow = ReadWorkflow();

        Assert.Contains("DOTNET_TARGET_FRAMEWORK: net8.0-windows", workflow);
        Assert.Contains("DOTNET_SDK_CHANNEL: 8.0.x", workflow);
        Assert.Contains("DOTNET_SUPPORT_PHASE: maintenance", workflow);
        Assert.Contains("DOTNET_EOL_DATE: \"2026-11-10\"", workflow);
        Assert.Contains("DOTNET_HOLD_DECISION:", workflow);
        Assert.Contains("Runtime and build-tool lifecycle gate", workflow);
        Assert.Contains("reached end of support", workflow);
        Assert.Contains("Report resolved .NET SDK patch", workflow);
        Assert.Contains("dotnet --version", workflow);
        Assert.Contains("PowerShell syntax check (Windows PowerShell 5.1)", workflow);
        Assert.Contains("PowerShell syntax check (PowerShell 7)", workflow);
        Assert.Contains("shell: powershell", workflow);
        Assert.Contains("shell: pwsh", workflow);
        Assert.Contains("Restore test graph with locked NuGet audit", workflow);
        Assert.Contains("dotnet restore tests/LibreSpot.Desktop.Tests/LibreSpot.Desktop.Tests.csproj --locked-mode -p:AuditPipeline=true", workflow);
        Assert.Contains("dotnet test tests/LibreSpot.Desktop.Tests/LibreSpot.Desktop.Tests.csproj -c Release --nologo --no-restore", workflow);
        Assert.Contains("Restore WPF publish graph with locked NuGet audit", workflow);
        Assert.Contains("dotnet restore src/LibreSpot.Desktop/LibreSpot.Desktop.csproj -r win-x64 --locked-mode -p:AuditPipeline=true", workflow);
        Assert.Contains("--no-restore", workflow);
    }

    [Fact]
    public void ReleaseWorkflow_UsesLifecycleToolPins()
    {
        var workflow = ReadWorkflow();

        Assert.Contains("PS2EXE_VERSION: \"1.0.18\"", workflow);
        Assert.Contains("CYCLONEDX_VERSION: \"6.2.0\"", workflow);
        Assert.Contains("Install-Module -Name ps2exe -RequiredVersion $env:PS2EXE_VERSION", workflow);
        Assert.Contains("dotnet tool install --global CycloneDX --version $env:CYCLONEDX_VERSION", workflow);
    }

    [Fact]
    public void ReleaseWorkflow_DoesNotReferenceSecretsDirectlyInIfConditions()
    {
        var workflow = ReadWorkflow();

        Assert.Contains("SIGNPATH_API_TOKEN: ${{ secrets.SIGNPATH_API_TOKEN }}", workflow);
        Assert.Contains("if: ${{ env.SIGNPATH_API_TOKEN != '' && env.SIGNPATH_ORGANIZATION_ID != '' }}", workflow);
        Assert.DoesNotContain("if: ${{ secrets.", workflow);
    }

    [Fact]
    public void ReleaseWorkflow_DoesNotContainEmptyExpressionMarkers()
    {
        var workflow = ReadWorkflow();

        Assert.DoesNotContain("${{ }}", workflow);
    }
}
