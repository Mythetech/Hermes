// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Diagnostics;
using System.Runtime.InteropServices;
using Hermes;
using Hermes.SingleInstance;

public static class Program
{
    private const string AppId = "hermes-si-test";
    private const string SecondInstanceFlag = "--second-instance";
    private const string NewSessionFlag = "--new-session";
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

        // Test 5: a launch from another terminal tab or the Dock runs in a different Unix session
        if (!OperatingSystem.IsWindows())
        {
            Console.WriteLine("HERMES_TEST_START: si-cross-session");
            receivedArgs = null;
            argsReceived.Reset();

            var psi3 = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = $"{SecondInstanceFlag} {NewSessionFlag} --cross-session",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var child3 = Process.Start(psi3);
            if (child3 is null)
            {
                Console.WriteLine("HERMES_TEST_FAIL: si-cross-session - Failed to start child process");
                failed++;
            }
            else
            {
                var child3Output = child3.StandardOutput.ReadToEnd();
                child3.WaitForExit(TimeSpan.FromMilliseconds(15000));
                Console.WriteLine($"  Child output: {child3Output.Trim()}");

                if (child3.ExitCode == 0 &&
                    argsReceived.Wait(TimeSpan.FromSeconds(10)) &&
                    receivedArgs is ["--cross-session"])
                {
                    Console.WriteLine("HERMES_TEST_PASS: si-cross-session");
                    passed++;
                }
                else
                {
                    Console.WriteLine($"HERMES_TEST_FAIL: si-cross-session - Child exited with code {child3.ExitCode}");
                    failed++;
                }
            }
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
        var argsToForward = Environment.GetCommandLineArgs()
            .Skip(1) // executable path
            .Skip(1) // --second-instance flag
            .ToArray();

        if (argsToForward.FirstOrDefault() == NewSessionFlag)
        {
            argsToForward = argsToForward[1..];
            if (setsid() == -1)
            {
                Console.WriteLine($"ERROR: setsid failed with errno {Marshal.GetLastPInvokeError()}");
                return 2;
            }
        }

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

    [DllImport("libc", SetLastError = true)]
    private static extern int setsid();
}
