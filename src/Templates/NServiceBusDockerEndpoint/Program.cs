﻿using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace NServiceBusDockerEndpoint
{
    class Program
    {
        static SemaphoreSlim semaphore = new SemaphoreSlim(0);

        // TODO: consider using C# 7.1 or later, which will allow
        // removal of this method, and renaming of MainAsync to Main
        public static void Main(string[] args) => MainAsync(args).GetAwaiter().GetResult();

        static async Task MainAsync(string[] args)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // required to identify when a "docker stop" command has been issued on a Windows container
                // and allow for graceful shutdown of the endpoint
                SetConsoleCtrlHandler(new HandlerRoutine(ConsoleCtrlCheck), true);
            }
            else
            {
                AppDomain.CurrentDomain.ProcessExit += ProcessExit;
                Console.CancelKeyPress += Cancelling;
            }

            var host = new Host();

            Console.Title = host.EndpointName;

            await host.Start();
            await Console.Out.WriteLineAsync("Press Ctrl+C to exit...");

            // wait until notified that the process should exit
            await semaphore.WaitAsync();

            await host.Stop();
        }

        static void Cancelling(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            semaphore.Release();
        }

        static void ProcessExit(object sender, EventArgs e)
        {
            // notify the MainAsync method to continue executing
            semaphore.Release();
        }

        static bool ConsoleCtrlCheck(CtrlTypes ctrlType)
        {
            semaphore.Release();

            return true;
        }

        // imports required to successfully notice when "docker stop <containerid>" has been run
        // and allow for a graceful shutdown of the endpoint
        [DllImport("Kernel32")]
        static extern bool SetConsoleCtrlHandler(HandlerRoutine Handler, bool Add);

        delegate bool HandlerRoutine(CtrlTypes CtrlType);

        enum CtrlTypes
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT = 1,
            CTRL_CLOSE_EVENT = 2,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT = 6
        }
    }
}