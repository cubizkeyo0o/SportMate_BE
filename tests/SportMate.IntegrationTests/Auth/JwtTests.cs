using System.Net;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SportMate.IntegrationTests.Infrastructure;

namespace SportMate.IntegrationTests.Auth;

public sealed class JwtTests(SportMateApiFactory factory) : IClassFixture<SportMateApiFactory>
{
    private const string MePath = "/api/v1/users/me";

    [Fact]
    public async Task AccessToken_ContainsMinimalClaims()
    {
        using var client = factory.CreateClient();
        var auth = await AuthApi.RegisterAndLoginAsync(client);

        var token = new JsonWebToken(auth.AccessToken);

        Assert.Equal(auth.User.Id.ToString(), token.Subject);
        Assert.Equal(auth.User.Email, token.GetClaim(JwtRegisteredClaimNames.Email).Value);
        Assert.Equal("User", token.GetClaim("role").Value);
        Assert.False(string.IsNullOrEmpty(token.Id));
        Assert.Equal(SportMateApiFactory.Issuer, token.Issuer);
        Assert.Contains(SportMateApiFactory.Audience, token.Audiences);
        Assert.True(token.ValidTo > DateTime.UtcNow);
        Assert.True(token.IssuedAt <= DateTime.UtcNow);
    }

    [Fact]
    public async Task ValidAccessToken_IsAccepted()
    {
        using var client = factory.CreateClient();
        var auth = await AuthApi.RegisterAndLoginAsync(client);

        using var response = await client.SendAsync(AuthApi.Authorized(HttpMethod.Get, MePath, auth.AccessToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task MissingToken_Returns401()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(MePath);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MalformedToken_Returns401()
    {
        using var client = factory.CreateClient();

        using var response = await client.SendAsync(AuthApi.Authorized(HttpMethod.Get, MePath, "not.a.jwt"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TokenSignedWithOtherKey_Returns401()
    {
        using var client = factory.CreateClient();
        var auth = await AuthApi.RegisterAndLoginAsync(client);
        var forged = CreateToken(auth.User.Id, DateTime.UtcNow.AddMinutes(10), "a-completely-different-signing-key-0123456789");

        using var response = await client.SendAsync(AuthApi.Authorized(HttpMethod.Get, MePath, forged));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ExpiredToken_Returns401()
    {
        using var client = factory.CreateClient();
        var auth = await AuthApi.RegisterAndLoginAsync(client);
        var expired = CreateToken(auth.User.Id, DateTime.UtcNow.AddMinutes(-5), SportMateApiFactory.SigningKey);

        using var response = await client.SendAsync(AuthApi.Authorized(HttpMethod.Get, MePath, expired));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CorrectlySignedTokenForCurrentKey_IsAccepted()
    {
        // Guards the two negative tests above: the only difference there is the key or the expiry.
        using var client = factory.CreateClient();
        var auth = await AuthApi.RegisterAndLoginAsync(client);
        var token = CreateToken(auth.User.Id, DateTime.UtcNow.AddMinutes(10), SportMateApiFactory.SigningKey);

        using var response = await client.SendAsync(AuthApi.Authorized(HttpMethod.Get, MePath, token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ValidlySignedTokenForUnknownUser_Returns401()
    {
        using var client = factory.CreateClient();
        var token = CreateToken(Guid.NewGuid(), DateTime.UtcNow.AddMinutes(10), SportMateApiFactory.SigningKey);

        using var response = await client.SendAsync(AuthApi.Authorized(HttpMethod.Get, MePath, token));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static string CreateToken(Guid userId, DateTime expiresAtUtc, string signingKey)
    {
        var issuedAt = expiresAtUtc.AddMinutes(-15);
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = SportMateApiFactory.Issuer,
            Audience = SportMateApiFactory.Audience,
            IssuedAt = issuedAt,
            NotBefore = issuedAt,
            Expires = expiresAtUtc,
            Claims = new Dictionary<string, object> { [JwtRegisteredClaimNames.Sub] = userId.ToString() },
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                SecurityAlgorithms.HmacSha256)
        });
    }
}
