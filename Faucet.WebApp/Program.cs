using Faucet;
using Faucet.Data;
using Faucet.Wallet;
using Microsoft.EntityFrameworkCore;
using Faucet.WebApp.Routes;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ??
                       throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

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
            .EnableRedirectionEndpointPassthrough();

        // Register the System.Net.Http integration and use the identity of the current
        // assembly as a more specific user agent, which can be useful when dealing with
        // providers that use the user agent as a way to throttle requests (e.g Reddit).
        options.UseSystemNetHttp()
            .SetProductInformation(typeof(Program).Assembly);
        
        // Register the GitHub integration.
        options.UseWebProviders()
            .AddGitHub(github =>
            {
                var clientId = config.GetValue<string>("GitHub:ClientId") ?? throw new InvalidOperationException("GitHub:ClientId not specified");
                var clientSecret = config.GetValue<string>("GitHub:ClientSecret") ?? throw new InvalidOperationException("GitHub:ClientSecret not specified");
                
                github
                    .SetClientId(clientId)
                    .SetClientSecret(clientSecret)
                    .SetRedirectUri("github/signin/callback");
            })
            .AddDiscord(discord =>
            {
                var clientId = config.GetValue<string>("Discord:ClientId") ?? throw new InvalidOperationException("Discord:ClientId not specified");
                var clientSecret = config.GetValue<string>("Discord:ClientSecret") ?? throw new InvalidOperationException("Discord:ClientSecret not specified");
                
                discord
                    .SetClientId(clientId)
                    .SetClientSecret(clientSecret)
                    .SetRedirectUri("discord/signin/callback");
            });
    });
builder.Services.AddDbContext<OpenIddictDbContext>(options =>
{
    // In memory is fine for our simple deployment
    options.UseInMemoryDatabase("db");
    options.UseOpenIddict();
});

// Faucet services
builder.Services.AddOptions<FaucetOptions>()
    .Bind(config.GetSection("Faucet"))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddTransient<FaucetOptions>(x => x.GetRequiredService<IOptions<FaucetOptions>>().Value);
builder.Services.AddDbContext<FaucetDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddSingleton<FaucetWallet>();
builder.Services.AddScoped<FaucetServices>();

var app = builder.Build();

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