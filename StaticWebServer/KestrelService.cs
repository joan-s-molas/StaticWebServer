using System;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using DasMulli.Win32.ServiceUtils;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace StaticWebServer
{
    internal class KestrelService : IWin32Service
    {
        public KestrelService(string[] commandLineArguments)
        {
            CommandLineArguments = commandLineArguments;
        }

        public string ServiceName => "StaticContentServer";

        public string[] CommandLineArguments { get; }

        public IWebHost WebHost { get; set; }

        public bool StopRequestedByWindows { get; set; }

        public void Start(string[] startupArguments, ServiceStoppedCallback serviceStoppedCallback)
        {
            string[] combinedArguments;
            if (startupArguments.Length > 0)
            {
                combinedArguments = new string[CommandLineArguments.Length + startupArguments.Length];
                Array.Copy(CommandLineArguments, combinedArguments, CommandLineArguments.Length);
                Array.Copy(startupArguments, 0, combinedArguments, CommandLineArguments.Length,
                    startupArguments.Length);
            }
            else
            {
                combinedArguments = CommandLineArguments;
            }
            try
            {
                var config = new ConfigurationBuilder()
                    .AddCommandLine(combinedArguments)
                    .Build();

                Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

                WebHost = new WebHostBuilder()
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
                    .UseStartup<Startup>()
                    .UseConfiguration(config)
                    .Build();
            }
            catch (Exception e)
            {
                Console.WriteLine("An error ocurred: '{0}'", e);
            }
            WebHost
                .Services
                .GetRequiredService<IApplicationLifetime>()
                .ApplicationStopped
                .Register(() =>
                {
                    if (StopRequestedByWindows == false)
                        serviceStoppedCallback();
                });

            WebHost.Start();
        }

        public void Stop()
        {
            StopRequestedByWindows = true;
            WebHost.Dispose();
        }
    }
}