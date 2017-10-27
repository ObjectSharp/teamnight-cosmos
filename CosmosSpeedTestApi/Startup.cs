using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CosmosSpeedTestApi
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Add service and create Policy with options
            services.AddCors(options =>
            {
                options.AddPolicy("CorsPolicy",
                    builder => builder.AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials());
            });

            // Adds services required for using options.
            services.AddOptions();
            services.Configure<SpeedTestOptions>(Configuration.GetSection("Cosmos"));

            services.AddMvc();

            services.AddSingleton<IDocumentClient>(sp => 
            {
                var options = sp.GetService<IOptions<SpeedTestOptions>>().Value;

                var connectionPolicy = new ConnectionPolicy
                {
                    ConnectionMode = ConnectionMode.Direct,
                    ConnectionProtocol = Protocol.Tcp
                };

                // Set the read region selection preference order
                foreach (var location in options.PreferredLocations)
                    connectionPolicy.PreferredLocations.Add(location);

                var client = new DocumentClient(new Uri(options.CosmosUri), options.CosmosKey, connectionPolicy);
                client.OpenAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                return client;
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            //if (env.IsDevelopment())
            //{
                app.UseDeveloperExceptionPage();
            //}

            app.UseCors("CorsPolicy");
            
            app.UseDefaultFiles();
            app.UseStaticFiles();
            
            app.UseMvc();
        }
    }
}
