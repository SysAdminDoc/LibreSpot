namespace LibreSpot.Desktop.Services;

internal static class PowerShellHostPath
{
    public static string Resolve()
    {
        var systemPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            @"WindowsPowerShell\v1.0\powershell.exe");
        return File.Exists(systemPath) ? systemPath : "powershell.exe";
    }
}
