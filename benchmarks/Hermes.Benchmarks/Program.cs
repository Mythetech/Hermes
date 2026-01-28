using BenchmarkDotNet.Running;

namespace Hermes.Benchmarks;

public class Program
{
    public static void Main(string[] args)
    {
        // Run all benchmarks or filter via command line
        // Usage:
        //   dotnet run -c Release                    # Run all benchmarks
        //   dotnet run -c Release -- --filter *Sync* # Run only sync context benchmarks
        //   dotnet run -c Release -- --filter *Message* # Run only message pump benchmarks
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
