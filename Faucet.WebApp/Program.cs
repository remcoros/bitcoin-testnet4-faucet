using Faucet;
using Faucet.Data;
using Microsoft.EntityFrameworkCore;
using Faucet.WebApp.Routes;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// Add services to the container.
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddMemoryCache();
builder.Services.AddRazorPages();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/";
        options.LogoutPath = "/signout";
        options.AccessDeniedPath = "/error";
    });
builder.Services.AddAuthorization();

builder.Services.AddOpenIddict()
    // Register the OpenIddict core components.
    .AddCore(options =>
    {
        // Configure OpenIddict to use the Entity Framework Core stores and models.
        options.UseEntityFrameworkCore()
            .UseDbContext<OpenIddictDbContext>();
    })
    // Register the OpenIddict client components.
    .AddClient(options =>
    {
        // Allow the OpenIddict client to negotiate the authorization code flow.
        options.AllowAuthorizationCodeFlow();

        // Register the signing and encryption credentials used to protect
        // sensitive data like the state tokens produced by OpenIddict.
        options
            .AddDevelopmentEncryptionCertificate()
            .AddDevelopmentSigningCertificate();

        // Register the ASP.NET Core host and configure the ASP.NET Core-specific options.
        options.UseAspNetCore()
            .EnableRedirectionEndpointPassthrough()
            // our container doesn't use SSL
            .DisableTransportSecurityRequirement();

        // Register the System.Net.Http integration and use the identity of the current
        // assembly as a more specific user agent, which can be useful when dealing with
        // providers that use the user agent as a way to throttle requests (e.g Reddit).
        options.UseSystemNetHttp()
            .SetProductInformation(typeof(Program).Assembly);

        // Register the OAuth providers.
        var webProviders = options.UseWebProviders();
        var gitHubClientId = config.GetValue<string>("GitHub:ClientId") ??
                             throw new InvalidOperationException("GitHub:ClientId not specified");
        if (!string.IsNullOrEmpty(gitHubClientId))
        {
            webProviders
                .AddGitHub(github =>
                {
                    var clientSecret = config.GetValue<string>("GitHub:ClientSecret") ??
                                       throw new InvalidOperationException("GitHub:ClientSecret not specified");

                    github
                        .SetClientId(gitHubClientId)
                        .SetClientSecret(clientSecret)
                        .SetRedirectUri("github/signin/callback");
                });
        }

        var discordClientId = config.GetValue<string>("Discord:ClientId") ??
                              throw new InvalidOperationException("Discord:ClientId not specified");
        if (!string.IsNullOrEmpty(discordClientId))
        {
            webProviders.AddDiscord(discord =>
            {
                var clientSecret = config.GetValue<string>("Discord:ClientSecret") ??
                                   throw new InvalidOperationException("Discord:ClientSecret not specified");

                discord
                    .SetClientId(discordClientId)
                    .SetClientSecret(clientSecret)
                    .SetRedirectUri("discord/signin/callback");
            });
        }

        var twitterClientId = config.GetValue<string>("Twitter:ClientId") ??
                              throw new InvalidOperationException("Twitter:ClientId not specified");
        if (!string.IsNullOrEmpty(twitterClientId))
        {
            webProviders.AddTwitter(twitter =>
            {
                var clientSecret = config.GetValue<string>("Twitter:ClientSecret") ??
                                   throw new InvalidOperationException("Twitter:ClientSecret not specified");

                twitter
                    .SetClientId(twitterClientId)
                    .SetClientSecret(clientSecret)
                    .SetRedirectUri("twitter/signin/callback");
            });
        }
    });

builder.Services.AddDbContext<OpenIddictDbContext>(options =>
{
    // In memory is fine for our simple deployment
    options.UseInMemoryDatabase("db");
    options.UseOpenIddict();
});

// Faucet services
builder.Services.AddFaucetServices(opts => builder.Configuration.GetSection("Faucet").Bind(opts));
builder.Services.ConfigureDbContext<FaucetDbContext>((sp, options) =>
{
    var connectionString = sp.GetRequiredService<IOptions<FaucetOptions>>().Value.ConnectionString;
    options.UseSqlite(connectionString);
});

// Build the app
var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedProto
});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
}

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Routes
app.MapStaticAssets();
app.MapRazorPages()
    .WithStaticAssets();

app.MapAuthentication();

// Migrations
using (var scope = app.Services.CreateScope())
{
    var log = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Faucet");
    var db = scope.ServiceProvider.GetRequiredService<FaucetDbContext>();

    log.LogInformation("Applying migrations...");
    await db.Database.MigrateAsync();
    log.LogInformation("Migrations applied");
}

await app.RunAsync();