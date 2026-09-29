using SportMate.Application.Auth;
using SportMate.Domain.Common;
using SportMate.Domain.Entities;
using SportMate.Domain.Enums;

namespace SportMate.UnitTests.Application;

public sealed class RolesTests
{
    [Theory]
    [InlineData(UserRole.User, Roles.User, 1)]
    [InlineData(UserRole.CourtOwner, Roles.CourtOwner, 2)]
    [InlineData(UserRole.Admin, Roles.Admin, 3)]
    public void RoleNamesAndStoredValuesAreStable(UserRole role, string expectedName, byte storedValue)
    {
        Assert.Equal(expectedName, Roles.NameOf(role));
        Assert.Equal(storedValue, (byte)role);
    }

    [Fact]
    public void EveryRoleHasAConstant()
    {
        Assert.Equal(
            Enum.GetNames<UserRole>().Order(),
            new[] { Roles.User, Roles.CourtOwner, Roles.Admin }.Order());
    }

    [Fact]
    public void ChangeRole_RejectsUndefinedRole()
    {
        var user = new User("a@example.com", "hash", "A", UserRole.User, DateTime.UtcNow);

        Assert.Throws<DomainException>(() => user.ChangeRole((UserRole)99, DateTime.UtcNow));
        Assert.Equal(UserRole.User, user.Role);
    }
}
