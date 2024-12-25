using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Discord;
using Discord.Rest;
using Faucet.Authentication;
using Microsoft.AspNetCore.Authentication;
using NBitcoin;
using OpenIddict.Client.AspNetCore;
using static OpenIddict.Client.WebIntegration.OpenIddictClientWebIntegrationConstants;

namespace Faucet.WebApp.Routes;

public static class AuthenticationRoutes
{
    private const string GitHubProvider = "GitHub";
    private const string DiscordProvider = "Discord";
    
    public static WebApplication MapAuthentication(this WebApplication app)
    {
        app.MapGet("/whoami", (HttpContext context) =>
        {
            var userHash = context.User.FindFirst(FaucetClaimTypes.UserHash)?.Value;
            var createdAt = context.User.FindFirst(FaucetClaimTypes.AccountCreatedAt)?.Value;

            return Results.Json(new
            {
                UserHash = userHash,
                AccountCreatedAt = createdAt
            });
        }).RequireAuthorization();

        app.MapGet("signout", () => Results.SignOut(new AuthenticationProperties()
        {
            RedirectUri = "/"
        })).WithName("signout");

        // GitHub signin/callbacks
        app.MapGet("github/signin",
            () => Results.Challenge(new() { RedirectUri = "/" },
                [Providers.GitHub]))
            .WithName("github-signin");
        app.MapMethods("github/signin/callback", [HttpMethods.Get, HttpMethods.Post], GitHubSignInCallback);

        // GitHub signin/callbacks
        app.MapGet("discord/signin",
                () => Results.Challenge(new() { RedirectUri = "/" },
                    [Providers.Discord]))
            .WithName("discord-signin");
        app.MapMethods("discord/signin/callback", [HttpMethods.Get, HttpMethods.Post], DiscordSignInCallback);
        
        return app;
    }

    private static async Task<IResult> GitHubSignInCallback(HttpContext context, CancellationToken cancellationToken)
    {
        var faucet = context.RequestServices.GetRequiredService<FaucetServices>();

        var result = await context.AuthenticateAsync(Providers.GitHub);
        if (!result.Succeeded || result.Principal is null)
        {
            return Results.Unauthorized();
        }
       
        var userId = result.Principal.FindFirstValue("id");
        var createdAt = result.Principal.FindFirstValue("created_at");
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(createdAt))
        {
            return Results.Unauthorized();
        }
        
        if (!DateTimeOffset.TryParse(createdAt, CultureInfo.InvariantCulture, out var createdAtDate))
        {
            return Results.Unauthorized();
        }

        var userHash = faucet.GenerateUserHash(GitHubProvider, userId);
        var identity = new ClaimsIdentity(authenticationType: GitHubProvider, nameType: ClaimTypes.Name, roleType: ClaimTypes.Role);
        
        // Add the hash of the user account and the account creation date as claims
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userHash));
        identity.AddClaim(new Claim(FaucetClaimTypes.UserHash, userHash));
        identity.AddClaim(new Claim(FaucetClaimTypes.AccountCreatedAt, createdAtDate.ToString("O")));

        var properties = new AuthenticationProperties { RedirectUri = result.Properties!.RedirectUri };

        return Results.SignIn(new ClaimsPrincipal(identity), properties);
    }
    
    private static async Task<IResult> DiscordSignInCallback(HttpContext context, CancellationToken cancellationToken)
    {
        var faucet = context.RequestServices.GetRequiredService<FaucetServices>();

        var result = await context.AuthenticateAsync(Providers.GitHub);
        if (!result.Succeeded || result.Principal is null)
        {
            return Results.Unauthorized();
        }

        var user = result.Principal.FindFirstValue("user");
        if (string.IsNullOrEmpty(user))
        {
            return Results.Unauthorized();
        }

        var discordUser = JsonSerializer.Deserialize<JsonDocument>(user);
        if (discordUser is null)
        {
            return Results.Unauthorized();
        }
        
        var userId = discordUser.RootElement.GetString("id");
        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        var client = new DiscordRestClient();
        await client
            .LoginAsync(TokenType.Bearer, result.Properties.GetTokenValue(OpenIddictClientAspNetCoreConstants.Tokens.BackchannelAccessToken))
            .WithCancellation(cancellationToken);

        var createdAtDate = client.CurrentUser.CreatedAt;

        var userHash = faucet.GenerateUserHash(DiscordProvider, userId);
        var identity = new ClaimsIdentity(authenticationType: DiscordProvider, nameType: ClaimTypes.Name, roleType: ClaimTypes.Role);
        
        // Add the hash of the user account and the account creation date as claims
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userHash));
        identity.AddClaim(new Claim(FaucetClaimTypes.UserHash, userHash));
        identity.AddClaim(new Claim(FaucetClaimTypes.AccountCreatedAt, createdAtDate.ToString("O")));

        var properties = new AuthenticationProperties { RedirectUri = result.Properties!.RedirectUri };

        return Results.SignIn(new ClaimsPrincipal(identity), properties);
    }
}