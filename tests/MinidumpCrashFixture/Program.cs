internal static class Program
{
    private const string ArmedArgument = "--librespot-minidump-armed";
    private const string DisabledCrashArgument = "--fixture-disabled";

    private static int Main(string[] args)
    {
        if (!args.Contains(ArmedArgument, StringComparer.OrdinalIgnoreCase) &&
            !args.Contains(DisabledCrashArgument, StringComparer.OrdinalIgnoreCase))
        {
            return 0;
        }

        Console.Error.WriteLine("LibreSpot isolated minidump fixture is terminating intentionally.");
        Environment.FailFast("LibreSpot isolated minidump fixture");
        return 134;
    }
}
