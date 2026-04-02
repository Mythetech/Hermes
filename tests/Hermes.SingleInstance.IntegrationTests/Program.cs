// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using System.Diagnostics;
using Hermes;
using Hermes.SingleInstance;

public static class Program
{
    private const string AppId = "hermes-si-test";
    private const string SecondInstanceFlag = "--second-instance";
    private const string TestArg1 = "--open";
    private const string TestArg2 = "test-file.txt";

    public static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == SecondInstanceFlag)
            return RunAsSecondInstance();

        return RunAsFirstInstance();
    }

    private static int RunAsFirstInstance()
    {
        Console.WriteLine("HERMES_READY: Single instance integration test started");
        var passed = 0;
        var failed = 0;

        // Test 1: Acquire guard as first instance
        Console.WriteLine("HERMES_TEST_START: si-first-instance-acquire");
        using var guard = HermesApplication.SingleInstance(AppId);
        if (guard.IsFirstInstance)
        {
            Console.WriteLine("HERMES_TEST_PASS: si-first-instance-acquire");
            passed++;
        }
        else
        {
            Console.WriteLine("HERMES_TEST_FAIL: si-first-instance-acquire - Not first instance");
            failed++;
            return 1;
        }

        // Set up listener for second instance args
        string[]? receivedArgs = null;
        var argsReceived = new ManualResetEventSlim(false);
        guard.SecondInstanceLaunched += incomingArgs =>
        {
            receivedArgs = incomingArgs;
            argsReceived.Set();
        };

        // Test 2: Launch child process as second instance
        Console.WriteLine("HERMES_TEST_START: si-child-process-launch");
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath))
        {
            Console.WriteLine("HERMES_TEST_FAIL: si-child-process-launch - Cannot determine process path");
            failed++;
            return 1;
        }

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = $"{SecondInstanceFlag} {TestArg1} {TestArg2}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var child = Process.Start(psi);
        if (child is null)
        {
            Console.WriteLine("HERMES_TEST_FAIL: si-child-process-launch - Failed to start child process");
            failed++;
            return 1;
        }

        var childOutput = child.StandardOutput.ReadToEnd();
        child.WaitForExit(TimeSpan.FromMilliseconds(15000));
        Console.WriteLine($"  Child output: {childOutput.Trim()}");

        if (child.ExitCode == 0)
        {
            Console.WriteLine("HERMES_TEST_PASS: si-child-process-launch");
            passed++;
        }
        else
        {
            Console.WriteLine($"HERMES_TEST_FAIL: si-child-process-launch - Child exited with code {child.ExitCode}");
            failed++;
        }

        // Test 3: Verify args were forwarded
        Console.WriteLine("HERMES_TEST_START: si-args-forwarded");
        if (argsReceived.Wait(TimeSpan.FromSeconds(10)))
        {
            if (receivedArgs is not null &&
                receivedArgs.Length == 2 &&
                receivedArgs[0] == TestArg1 &&
                receivedArgs[1] == TestArg2)
            {
                Console.WriteLine("HERMES_TEST_PASS: si-args-forwarded");
                passed++;
            }
            else
            {
                var actual = receivedArgs is not null ? string.Join(", ", receivedArgs) : "null";
                Console.WriteLine($"HERMES_TEST_FAIL: si-args-forwarded - Expected [{TestArg1}, {TestArg2}], got [{actual}]");
                failed++;
            }
        }
        else
        {
            Console.WriteLine("HERMES_TEST_FAIL: si-args-forwarded - Timed out waiting for args");
            failed++;
        }

        // Test 4: Launch another child to verify repeated notifications work
        Console.WriteLine("HERMES_TEST_START: si-repeated-notification");
        receivedArgs = null;
        argsReceived.Reset();

        var psi2 = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = $"{SecondInstanceFlag} --repeat test",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var child2 = Process.Start(psi2);
        if (child2 is not null)
        {
            child2.WaitForExit(TimeSpan.FromMilliseconds(15000));

            if (argsReceived.Wait(TimeSpan.FromSeconds(10)) &&
                receivedArgs is not null &&
                receivedArgs.Length == 2 &&
                receivedArgs[0] == "--repeat" &&
                receivedArgs[1] == "test")
            {
                Console.WriteLine("HERMES_TEST_PASS: si-repeated-notification");
                passed++;
            }
            else
            {
                Console.WriteLine("HERMES_TEST_FAIL: si-repeated-notification - Args not received correctly");
                failed++;
            }
        }
        else
        {
            Console.WriteLine("HERMES_TEST_FAIL: si-repeated-notification - Failed to start second child");
            failed++;
        }

        // Summary
        Console.WriteLine();
        var total = passed + failed;
        if (failed > 0)
            Console.WriteLine($"HERMES_TEST_SUMMARY: FAILED ({failed}/{total} tests failed)");
        else
            Console.WriteLine($"HERMES_TEST_SUMMARY: PASSED ({passed}/{total} tests passed)");

        return failed > 0 ? 1 : 0;
    }

    private static int RunAsSecondInstance()
    {
        // Skip the --second-instance flag, forward remaining args
        var argsToForward = Environment.GetCommandLineArgs()
            .Skip(1) // executable path
            .Skip(1) // --second-instance flag
            .ToArray();

        using var guard = HermesApplication.SingleInstance(AppId);

        if (guard.IsFirstInstance)
        {
            Console.WriteLine("ERROR: Second instance was detected as first instance");
            return 1;
        }

        var success = guard.NotifyFirstInstance(argsToForward);
        Console.WriteLine(success
            ? $"OK: Forwarded {argsToForward.Length} arg(s) to first instance"
            : "ERROR: Failed to notify first instance");

        return success ? 0 : 1;
    }
}
