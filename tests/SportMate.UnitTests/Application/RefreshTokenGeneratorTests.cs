using SportMate.Application.Auth;
using SportMate.Domain.Entities;

namespace SportMate.UnitTests.Application;

public sealed class RefreshTokenGeneratorTests
{
    [Fact]
    public void Generate_ReturnsUrlSafeRandomTokens()
    {
        var tokens = Enumerable.Range(0, 100).Select(_ => RefreshTokenGenerator.Generate()).ToList();

        Assert.Equal(tokens.Count, tokens.Distinct().Count());
        Assert.All(tokens, token =>
        {
            Assert.Equal(43, token.Length); // 32 bytes, base64url without padding
            Assert.All(token, c => Assert.True(char.IsAsciiLetterOrDigit(c) || c is '-' or '_'));
        });
    }

    [Fact]
    public void Hash_IsDeterministicLowerHexSha256MatchingColumnLength()
    {
        var token = RefreshTokenGenerator.Generate();

        var hash = RefreshTokenGenerator.Hash(token);

        Assert.Equal(hash, RefreshTokenGenerator.Hash(token));
        Assert.Equal(RefreshToken.TokenHashLength, hash.Length);
        Assert.All(hash, c => Assert.True(char.IsAsciiDigit(c) || c is >= 'a' and <= 'f'));
        Assert.NotEqual(token, hash);
    }

    [Fact]
    public void Hash_MatchesKnownSha256Vector()
    {
        Assert.Equal(
            "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",
            RefreshTokenGenerator.Hash("abc"));
    }
}
