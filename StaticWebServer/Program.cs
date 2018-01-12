using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using DasMulli.Win32.ServiceUtils;
using Microsoft.AspNetCore.Hosting;

namespace StaticWebServer
{
    public class Program
    {
        private const string RunAsServiceFlag = "--run-as-service";
        private const string RegisterServiceFlag = "--register-service";
        private const string UnregisterServiceFlag = "--unregister-service";
        private const string InteractiveFlag = "--interactive";
        private const string ServiceName = "StaticWebServer";
        private const string ServiceDisplayName = "StaticWebServer";
        private const string ServiceDescription = "Static content webserver";

        public static void Main(string[] args)
        {
            try
            {
                if (args.Contains(RunAsServiceFlag))
                    RunAsService(args);
                else if (args.Contains(RegisterServiceFlag))
                    RegisterService();
                else if (args.Contains(UnregisterServiceFlag))
                    UnregisterService();
                else if (args.Contains(InteractiveFlag))
                    RunInteractive(args);
                else
                    DisplayHelp();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error ocurred: {ex.Message}");
            }
        }

        private static void RunAsService(string[] args)
        {
            try
            {
                Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
                var rweKestrelWebServerService = new KestrelService(args.Where(a => a != RunAsServiceFlag).ToArray());
                var rweKestrelWebServerServiceHost = new Win32ServiceHost(rweKestrelWebServerService);
                rweKestrelWebServerServiceHost.Run();
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: '{0}'", e);
            }
        }

        private static void RunInteractive(string[] args)
        {
            var webHost = new WebHostBuilder()
                .UseKestrel(options =>
                {
                    var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
                    var configuration = Startup.Configuration;
                    var hostname = configuration["WebHost:CertString"];
                    var certfile = configuration["WebHost:NonStoreCertificate"];

                    store.Open(OpenFlags.ReadOnly);

                    var certs = store.Certificates.Find(X509FindType.FindBySubjectName, hostname, false);

                    if (certs.Count > 0)
                    {
                        var certificate = certs[0];
                        options.Listen(IPAddress.Any, 443, listenOptions =>
                        {
                            listenOptions.UseHttps(certificate);
                            listenOptions.UseConnectionLogging();
                        });
                    }
                    else
                    {
                        Console.WriteLine("Cannot find certificate in store, resorting to local pfx file");
                        options.Listen(IPAddress.Any, 443, listenOptions =>
                        {
                            listenOptions.UseHttps(certfile);
                            listenOptions.UseConnectionLogging();
                        });
                    }

                    options.Listen(IPAddress.Any, 80, listenOptions => { listenOptions.UseConnectionLogging(); });
                })
                //.UseContentRoot(Directory.GetCurrentDirectory())
                .UseStartup<Startup>()
                .Build();


            webHost.Run();
        }

        private static void RegisterService()
        {
            // Environment.GetCommandLineArgs() includes the current DLL from a "dotnet my.dll --register-service" call, which is not passed to Main()
            var remainingArgs = Environment.GetCommandLineArgs()
                .Where(arg => arg != RegisterServiceFlag)
                .Select(EscapeCommandLineArgument)
                .Append(RunAsServiceFlag);

            var host = Process.GetCurrentProcess().MainModule.FileName;

            if (!host.EndsWith("dotnet.exe", StringComparison.OrdinalIgnoreCase))
                remainingArgs = remainingArgs.Skip(1);

            var fullServiceCommand = host + " " + string.Join(" ", remainingArgs);

            var serviceDefinition = new ServiceDefinitionBuilder(ServiceName)
                .WithDisplayName(ServiceDisplayName)
                .WithDescription(ServiceDescription)
                .WithBinaryPath(fullServiceCommand)
                .WithCredentials(Win32ServiceCredentials.LocalSystem)
                .WithAutoStart(true)
                .Build();

            new Win32ServiceManager().CreateOrUpdateService(serviceDefinition, true);

            Console.WriteLine(
                $@"Successfully registered and started service ""{ServiceDisplayName}"" (""{ServiceDescription}"")");
        }

        private static void UnregisterService()
        {
            new Win32ServiceManager()
                .DeleteService(ServiceName);

            Console.WriteLine(
                $@"Successfully unregistered service ""{ServiceDisplayName}"" (""{ServiceDescription}"")");
        }

        private static void DisplayHelp()
        {
            Console.WriteLine(ServiceDisplayName);
            Console.WriteLine(ServiceDescription);
            Console.WriteLine();
            Console.WriteLine("Use the following commands to operate the server:");
            Console.WriteLine(
                "  --register-service        Registers and starts this program as a windows service named \"" +
                ServiceDisplayName + "\"");
            Console.WriteLine(
                "                            All additional arguments will be passed to ASP.NET Core's WebHostBuilder.");
            Console.WriteLine("  --unregister-service      Removes the windows service creatd by --register-service.");
            Console.WriteLine(
                "  --interactive             Runs the underlying asp.net core app. Useful to test arguments.");
        }

        private static string EscapeCommandLineArgument(string arg)
        {
            arg = Regex.Replace(arg, @"(\\*)" + "\"", @"$1$1\" + "\"");
            arg = "\"" + Regex.Replace(arg, @"(\\+)$", @"$1$1") + "\"";
            return arg;
        }
    }
}