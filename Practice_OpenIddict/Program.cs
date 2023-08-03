using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenIddict.Abstractions;
using System;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

class Program
{
    public class MyDbContext : DbContext
    {
        public MyDbContext(DbContextOptions<MyDbContext> options)
            : base(options)
        {
        }
    }

    public class AuthenticationConfiguration
    {
        public string JwtSigningCertificate { get; set; } = "";

        [Required]
        [Range(60, 3600)]
        public int JwtLifetimeInSeconds { get; set; }
    }

    static async Task Main(string[] args)
    {
        var services = new ServiceCollection();

        // add EF Core services to the DI container.
        services.AddDbContext<MyDbContext>(options =>
        {
            options.UseSqlite("Data Source=myDatabase.db");
            options.UseOpenIddict();
        });

        ConfigureOpenIddict_ConsumerApi(services);

        var provider = services.BuildServiceProvider();

        using (var scope = provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<MyDbContext>();
            await context.Database.EnsureCreatedAsync();

            // check if services have been registered properly.
            var dbContext = scope.ServiceProvider.GetService<MyDbContext>();
            var openIddictScopeManager = scope.ServiceProvider.GetService<IOpenIddictScopeManager>();

            Console.WriteLine(dbContext != null ? "DbContext has been registered correctly." : "DbContext has not been registered correctly.");
            Console.WriteLine(openIddictScopeManager != null ? "IOpenIddictScopeManager has been registered correctly." : "IOpenIddictScopeManager has not been registered correctly.");

            // check if the database has been created successfully.
            var databaseExists = await context.Database.CanConnectAsync();
            Console.WriteLine(databaseExists ? "Database has been created correctly." : "Database has not been created correctly.");
        }
    }

    static void ConfigureOpenIddict_ConsumerApi(ServiceCollection services)
    {
        AuthenticationConfiguration configuration = new AuthenticationConfiguration()
        {
            JwtSigningCertificate = "",
            JwtLifetimeInSeconds = 90
        };

        // register the OpenIddict services.
        services.AddOpenIddict()

        // register the OpenIddict core services.
        .AddCore(options =>
        {
            options.UseEntityFrameworkCore()
                   .UseDbContext<MyDbContext>();
        })

        // register the OpenIddict server services.
        .AddServer(options =>
        {
            options.AddDevelopmentSigningCertificate();

            options.SetTokenEndpointUris("connect/token");
            options.AllowPasswordFlow();
            options.SetAccessTokenLifetime(TimeSpan.FromSeconds(configuration.JwtLifetimeInSeconds));

            options.DisableAccessTokenEncryption();

            options.AddDevelopmentEncryptionCertificate(); // for some reason this is necessary even though we don't encrypt the tokens

            options.UseAspNetCore()
                .EnableTokenEndpointPassthrough()
                .DisableTransportSecurityRequirement();

            options.DisableTokenStorage();
        })

        .AddValidation(options =>
        {
            // import the configuration (like valid issuer and the signing certificate) from the local OpenIddict server instance.
            options.UseLocalServer();

            /*
             * OpenIddict's validation features don't integrate directly with ASP.NET Core unlike server features, 
             * hence the UseAspNetCore() is not applicable there.
             */
            //options.UseAspNetCore();
        });
    }

    static void ConfigureOpenIddict_Vanilla(ServiceCollection services)
    {
        // register the OpenIddict services.
        services.AddOpenIddict()

        // register the OpenIddict core services.
        .AddCore(options =>
        {
            options.UseEntityFrameworkCore()
                   .UseDbContext<MyDbContext>();
        })

        // register the OpenIddict server services.
        .AddServer(options =>
        {
            options.SetTokenEndpointUris("/connect/token");

            options.AllowPasswordFlow();

            // for demo purposes, the sample uses an ephemeral signing certificate (valid certificate should be used)
            options.AddEphemeralEncryptionKey()
                   .AddEphemeralSigningKey();

            options.UseAspNetCore()
                   .EnableTokenEndpointPassthrough();
        });
    }
}
