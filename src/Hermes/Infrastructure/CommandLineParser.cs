namespace Hermes.Infrastructure;

/// <summary>
/// Simple command-line argument parser for Hermes applications.
/// Supports named arguments in the form --name value or --name "quoted value".
/// </summary>
public class CommandLineParser
{
    private readonly Dictionary<string, string?> _arguments = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _positionalArgs = new();

    public CommandLineParser(string[] args)
    {
        Parse(args);
    }

    /// <summary>
    /// Gets all positional (non-named) arguments.
    /// </summary>
    public IReadOnlyList<string> PositionalArguments => _positionalArgs;

    /// <summary>
    /// Tries to get a named argument value.
    /// </summary>
    /// <param name="name">The argument name (without -- prefix).</param>
    /// <param name="value">The argument value if found.</param>
    /// <returns>True if the argument was found, false otherwise.</returns>
    public bool TryGetValue(string name, out string? value)
    {
        return _arguments.TryGetValue(name, out value);
    }

    /// <summary>
    /// Gets a named argument value or returns the default.
    /// </summary>
    /// <param name="name">The argument name (without -- prefix).</param>
    /// <param name="defaultValue">Default value if argument not found.</param>
    /// <returns>The argument value or default.</returns>
    public string? GetValue(string name, string? defaultValue = null)
    {
        return _arguments.TryGetValue(name, out var value) ? value : defaultValue;
    }

    /// <summary>
    /// Checks if a flag argument is present (e.g., --verbose).
    /// </summary>
    /// <param name="name">The flag name (without -- prefix).</param>
    /// <returns>True if the flag is present.</returns>
    public bool HasFlag(string name)
    {
        return _arguments.ContainsKey(name);
    }

    private void Parse(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg.StartsWith("--"))
            {
                var name = arg[2..];
                string? value = null;

                // Check if next arg is a value (not another flag)
                if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                {
                    value = args[i + 1];
                    i++; // Skip the value in next iteration
                }

                _arguments[name] = value;
            }
            else if (arg.StartsWith("-") && arg.Length == 2)
            {
                // Short flag like -v
                var name = arg[1..];
                string? value = null;

                if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                {
                    value = args[i + 1];
                    i++;
                }

                _arguments[name] = value;
            }
            else
            {
                // Positional argument
                _positionalArgs.Add(arg);
            }
        }
    }
}
