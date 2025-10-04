using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Deck.Api.Data;
using Deck.Api.Models;

namespace Deck.Tests.Infrastructure;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private SqliteConnection? _conn;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing"); //set environment 

        builder.ConfigureAppConfiguration((ctx, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ef:MigrateOnStartup"] = "false",
                ["Jwt:Issuer"] = "deck.local",
                ["Jwt:Audience"] = "deck.api",
                ["Jwt:Key"] = "test_super_secret_key_32chars_minimum!!!"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove actual dbcontext
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor is not null) services.Remove(descriptor);

            _conn = new SqliteConnection("DataSource=:memory:;Cache=Shared");
            _conn.Open();

            services.AddDbContext<AppDbContext>(opt =>
            {
                opt.UseSqlite(_conn);
            });

            const string issuer = "deck.local";
            const string audience = "deck.api";
            const string key = "test_super_secret_key_32chars_minimum!!!";
            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));

            services.PostConfigureAll<JwtBearerOptions>(o =>
            {
                o.RequireHttpsMetadata = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = signingKey,
                    ValidateIssuer = true,   ValidIssuer = issuer,
                    ValidateAudience = true, ValidAudience = audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(1)
                };
            });

            using var scope = services.BuildServiceProvider().CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            db.Database.EnsureDeleted();
            db.Database.EnsureCreated();

            var seedAt = new DateTime(2025, 10, 3, 0, 0, 0, DateTimeKind.Utc);
            if (!db.Users.Any())
            {
                db.Users.AddRange(
                    new User { Id = 1, Name = "User 1", CartVersion = 0, IsActive = true, CreatedDateTime = seedAt, ModifiedDateTime = seedAt },
                    new User { Id = 2, Name = "User 2", CartVersion = 0, IsActive = true, CreatedDateTime = seedAt, ModifiedDateTime = seedAt }
                );
            }
            if (!db.Items.Any())
            {
                db.Items.AddRange(
                    new Item { Id = 1, Name = "Item 1", Price = 1.11m, IsActive = true, CreatedDateTime = seedAt, ModifiedDateTime = seedAt },
                    new Item { Id = 2, Name = "Item 2", Price = 2.22m, IsActive = true, CreatedDateTime = seedAt, ModifiedDateTime = seedAt }
                );
            }
            db.SaveChanges();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _conn?.Dispose();
            _conn = null;
        }
    }
}
