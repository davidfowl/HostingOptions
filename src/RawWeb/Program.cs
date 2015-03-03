using System;
using System.Text;
using System.Threading.Tasks;
using Kestrel;
using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Http;
using Microsoft.AspNet.Http.Core;
using Microsoft.AspNet.Http.Interfaces;
using Microsoft.AspNet.Server.Kestrel;
using Microsoft.Framework.ConfigurationModel;
using Microsoft.Framework.Runtime;
using Microsoft.Net.Http.Server;

namespace RawWeb
{
    public class Startup
    {
        // Running with Microsoft.AspNet.Hosting
        public void Configure(IApplicationBuilder app)
        {
            app.Run(async context =>
            {
                await context.Response.WriteAsync("Hello World");
            });
        }
    }

    public class Program
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILibraryManager _libraryManager;

        public Program(IServiceProvider serviceProvider, ILibraryManager libraryManager)
        {
            _serviceProvider = serviceProvider;
            _libraryManager = libraryManager;
        }

        public async Task Main(string[] args)
        {
            // await DoRawKestrel();
            // await DoServerFactoryKestrelWithFeatures();
            await DoKestrelServerFactoryWithAppBuilder();
        }

        // Console app using lower layers of kestrel
        private async Task DoRawKestrel()
        {
            var engine = new KestrelEngine(_libraryManager);
            engine.Start(1);

            var data = Encoding.UTF8.GetBytes("Hello World");

            engine.CreateServer("http", "localhost", 5001, async frame =>
            {
                frame.ResponseHeaders["Content-Length"] = new[] { data.Length.ToString() };
                await frame.ResponseBody.WriteAsync(data, 0, data.Length);
            });

            var tcs = new TaskCompletionSource<object>();
            await tcs.Task;
        }

        // Do kestrel with a server factory
        private async Task DoServerFactoryKestrelWithFeatures()
        {
            var configuration = new Configuration()
                .AddCommandLine(new[] { "--server.urls", "http://localhost:5001" });

            // Each server implements IServerFactory
            var factory = new Kestrel.ServerFactory(_libraryManager);

            // Initialize using the configration
            var info = factory.Initialize(configuration);

            var data = Encoding.UTF8.GetBytes("Hello World");

            // Call start to get the callback
            factory.Start(info, features =>
            {
                // Get the raw IHttpResponseFeature out of the collection and write to the 
                // response
                var response = (IHttpResponseFeature)features[typeof(IHttpResponseFeature)];

                response.Headers["Content-Length"] = new[] { data.Length.ToString() };
                return response.Body.WriteAsync(data, 0, data.Length);

            });

            var tcs = new TaskCompletionSource<object>();
            await tcs.Task;
        }

        // Do kestrel with a server factory
        private async Task DoKestrelServerFactoryWithAppBuilder()
        {
            var configuration = new Configuration()
                .AddCommandLine(new[] { "--server.urls", "http://localhost:5001" });

            // Each server implements IServerFactory
            var factory = new ServerFactory(_libraryManager);

            // Initialize using the configration
            var info = factory.Initialize(configuration);

            // Create the app builder and call the user delegate
            var app = new ApplicationBuilder(_serviceProvider);

            // Add a piece of middleware manually
            app.Run(async context =>
            {
                await context.Response.WriteAsync("Hello World");
            });

            var requestDelegate = app.Build();

            // Call start to get the callback
            factory.Start(info, features =>
            {
                // Turn the feature collection into an HttpContext
                var context = new DefaultHttpContext(features);
                return requestDelegate(context);
            });

            var tcs = new TaskCompletionSource<object>();
            await tcs.Task;
        }

        private async Task DoWebListener()
        {
            using (var listener = new WebListener())
            {
                listener.UrlPrefixes.Add("http://localhost:5001");
                listener.Start();

                var data = Encoding.UTF8.GetBytes("Hello World");

                while (true)
                {
                    var context = await listener.GetContextAsync();
                    context.Response.ContentLength = data.Length;
                    await context.Response.Body.WriteAsync(data, 0, data.Length);
                    await context.Response.Body.FlushAsync();
                }
            }
        }
    }
}
