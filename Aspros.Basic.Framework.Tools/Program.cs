using System.Diagnostics;

return await AsprosTool.RunAsync(args);

internal static class AsprosTool
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length < 3 || !string.Equals(args[0], "db", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(args[1], "migration", StringComparison.OrdinalIgnoreCase))
        {
            PrintUsage();
            return 2;
        }

        var command = args[2].ToLowerInvariant();
        if (command is not ("add" or "script" or "update"))
        {
            PrintUsage();
            return 2;
        }

        var options = ParseOptions(args[3..]);

        if (command == "add" && options.Positionals.Count != 1)
        {
            Console.Error.WriteLine("Migration name is required. Example: aspros db migration add InitialCreate --project ./Xr.Trade --startup-project ./Xr.Trade.Api");
            return 2;
        }

        if (command == "update" && !options.Flags.Contains("allow-update"))
        {
            Console.Error.WriteLine("Database update is intentionally protected. Add --allow-update only for development/test databases.");
            return 2;
        }

        var efArguments = BuildEfArguments(command, options);
        return await RunDotnetEfAsync(efArguments);
    }

    private static List<string> BuildEfArguments(string command, Options options)
    {
        var result = new List<string> { "ef", "migrations", command };

        if (command == "add")
        {
            result.Add(options.Positionals[0]);
        }

        AddOption(result, options, "project");
        AddOption(result, options, "startup-project");
        AddOption(result, options, "context");
        AddOption(result, options, "output-dir");
        AddOption(result, options, "configuration");
        AddOption(result, options, "framework");
        AddOption(result, options, "no-build");

        if (command == "script")
        {
            AddOption(result, options, "from");
            AddOption(result, options, "to");
            AddOption(result, options, "output");
            if (options.Flags.Contains("idempotent"))
            {
                result.Add("--idempotent");
            }
        }

        if (command == "update")
        {
            AddOption(result, options, "connection");
            AddOption(result, options, "configuration");
            AddOption(result, options, "framework");
            AddOption(result, options, "no-build");
            if (options.Positionals.Count > 0)
            {
                result.Add(options.Positionals[0]);
            }
        }

        return result;
    }

    private static void AddOption(List<string> args, Options options, string name)
    {
        if (options.Values.TryGetValue(name, out var value))
        {
            args.Add($"--{name}");
            args.Add(value);
        }
    }

    private static Options ParseOptions(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var positionals = new List<string>();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                positionals.Add(arg);
                continue;
            }

            var name = arg[2..];
            if (name is "idempotent" or "allow-update")
            {
                flags.Add(name);
                continue;
            }

            if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Option '--{name}' requires a value.");
            }

            values[name] = args[++i];
        }

        return new Options(values, flags, positionals);
    }

    private static async Task<int> RunDotnetEfAsync(IEnumerable<string> efArguments)
    {
        var arguments = string.Join(" ", efArguments.Select(Quote));
        Console.WriteLine($"Running: dotnet {arguments}");

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.AddRange(efArguments);

        using var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null) Console.WriteLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null) Console.Error.WriteLine(e.Data);
        };

        try
        {
            if (!process.Start())
            {
                Console.Error.WriteLine("Unable to start dotnet.");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unable to start dotnet: {ex.Message}");
            return 1;
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();
        return process.ExitCode;
    }

    private static string Quote(string value) =>
        value.Contains(' ') ? $"\"{value.Replace("\"", "\\\"")}\"" : value;

    private static void PrintUsage()
    {
        Console.WriteLine("""
        Aspros Basic Framework database migration tool

        Commands:
          aspros db migration add <Name> --project <path> --startup-project <path> [--context <Type>]
          aspros db migration script --project <path> --startup-project <path> [--context <Type>] [--idempotent]
          aspros db migration update --allow-update --project <path> --startup-project <path> [--context <Type>]

        Notes:
          - The tool delegates model/migration semantics to EF Core.
          - 'update' is protected and intended for development/test databases.
          - Production should normally use a reviewed SQL script or migration bundle.
          - No database is modified by 'add' or 'script'.
        """);
    }

    private sealed record Options(
        Dictionary<string, string> Values,
        HashSet<string> Flags,
        List<string> Positionals);
}
