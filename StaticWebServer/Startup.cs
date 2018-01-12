using System;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;

namespace StaticWebServer
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddCors(options =>
            {
                options.AddPolicy("AllowAll",
                    builder =>
                    {
                        builder
                            .AllowAnyOrigin()
                            .AllowAnyMethod()
                            .AllowAnyHeader()
                            .AllowCredentials();
                    });
            });
            services.Configure<MvcOptions>(options => { options.Filters.Add(new RequireHttpsAttribute()); });
        }

        public Startup(IHostingEnvironment env)
        {
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("config.json");
            Configuration = configBuilder.Build();
        }

        public static IConfigurationRoot Configuration { get; set; }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

            var options = new RewriteOptions()
                .AddRedirectToHttps();
            app.UseRewriter(options);

            app.UseCors("AllowAll");

            var contentroot = Configuration["WebHost:ContentRoot"];

            var fileProvider =
                new PhysicalFileProvider(contentroot);
            var defoptions = new DefaultFilesOptions();
            defoptions.DefaultFileNames.Clear();
            defoptions.FileProvider = fileProvider;
            defoptions.DefaultFileNames.Add(Configuration["WebHost:DefaultDocument"]);
            app.UseDefaultFiles(defoptions);

            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = fileProvider,
                RequestPath = new PathString("")
            });
            app.UseStaticFiles();

            loggerFactory.AddFile("logs\\kestrel.log");
            loggerFactory.AddDebug();
        }
    }
}