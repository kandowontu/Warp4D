using Warp4D.UI;

namespace Warp4D;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Length >= 3 && args[0].Equals("--smoke", StringComparison.OrdinalIgnoreCase))
        {
            return SmokeTest.Run(args[1], args[2]);
        }
        if (args.Length >= 3 && args[0].Equals("--attract-smoke", StringComparison.OrdinalIgnoreCase))
        {
            return SmokeTest.RunAttractDemo(args[1], args[2]);
        }
        if (args.Length >= 3 && args[0].Equals("--game-profile-smoke", StringComparison.OrdinalIgnoreCase))
        {
            return SmokeTest.RunGameProfile(args[1], args[2]);
        }
        if (args.Length >= 3 && args[0].Equals("--scroll-smoke", StringComparison.OrdinalIgnoreCase))
        {
            return SmokeTest.RunScrollCapture(args[1], args[2]);
        }
        Application.Run(new MainForm());
        return 0;
    }
}
