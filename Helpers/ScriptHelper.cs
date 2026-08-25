using System.Diagnostics;
using System.Text;

namespace Helpers;

public static class ScriptHelper
{
    public static void MoveToTrash(string path)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "trash-put",
            Arguments = $"\"{path}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true
        };

        Process.Start(psi)?.WaitForExit();
    }

    public static string ExecDocker(string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(psi);
        var output = process!.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
            Console.WriteLine($"Docker error: {error}");

        return output;
    }
}