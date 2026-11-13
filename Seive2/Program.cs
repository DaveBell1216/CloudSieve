
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Seive2.HostedServices;
using Seive2.Services;

namespace Seive2
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Configure Kestrel server timeouts
            builder.WebHost.ConfigureKestrel(serverOptions =>
            {
                serverOptions.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(15);
                serverOptions.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(15);
            });

            // Add services to the container.
            builder.Services.AddControllers(options =>
            {
                // Increase timeout for long-running prime calculations
                options.MaxModelBindingCollectionSize = int.MaxValue;
            });
            builder.Services.AddOpenApi();

            var tenantId = builder.Configuration["Authentication:TenantId"]
                ?? throw new InvalidOperationException("Authentication:TenantId must be configured.");
            var audience = builder.Configuration["Authentication:Audience"]
                ?? throw new InvalidOperationException("Authentication:Audience must be configured.");

            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.Authority = $"https://login.microsoftonline.com/{tenantId}/v2.0";
                    options.Audience = audience;
                    options.MapInboundClaims = false;
                    options.TokenValidationParameters.RoleClaimType = "roles";
                });
            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy("Sieve.Operator", policy => policy.RequireRole("Sieve.Operator"));
                options.AddPolicy("Sieve.Worker", policy => policy.RequireRole("Sieve.Worker"));
            });

            builder.Services.AddSingleton<SqlWorkCoordinator>();

            // Register LargestPrimeTracker as singleton
            builder.Services.AddSingleton<LargestPrimeTracker>();

            if (builder.Configuration.GetValue("AutoStartJob:Enabled", false))
            {
                builder.Services.AddHostedService<AutoStartJobService>();
            }

            var app = builder.Build();

            await app.Services.GetRequiredService<SqlWorkCoordinator>().InitializeDatabaseAsync();
            await app.Services.GetRequiredService<LargestPrimeTracker>().InitializeDatabaseAsync();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}
