using System.Windows;

namespace SchoolManagement.Update;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Any(a => a.Equals("/unattended", StringComparison.OrdinalIgnoreCase)
                          || a.Equals("--unattended", StringComparison.OrdinalIgnoreCase)))
        {
            return RunUnattended();
        }

        var app = new System.Windows.Application();
        app.Run(new MainWindow());
        return 0;
    }

    private static int RunUnattended()
    {
        try
        {
            var engine = new UpdateEngine(Console.WriteLine);
            var installation = engine.DetectInstallation();
            var report = engine.RunAsync(installation).GetAwaiter().GetResult();
            Console.WriteLine();
            Console.WriteLine(report.FormatSummary());
            return report.Success ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("ERREUR : " + ex.Message);
            return 1;
        }
    }
}
