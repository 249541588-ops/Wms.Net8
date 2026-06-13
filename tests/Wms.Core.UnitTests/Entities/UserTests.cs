using FluentAssertions;
using Wms.Core.Domain.Entities.Identity;
using Xunit;

namespace Wms.Core.UnitTests.Entities;

/// <summary>
/// User 实体单元测试
/// </summary>
public class UserTests
{
    [Fact]
    public void User_ShouldInitializeWithDefaults()
    {
        // Arrange & Act
        var user = new User();

        // Assert
        user.IsActive.Should().BeTrue();
        user.UserName.Should().NotBeNull();
        user.PasswordHash.Should().NotBeNull();
        user.PasswordSalt.Should().NotBeNull();
        user.RealName.Should().NotBeNull();
    }

    [Fact]
    public void SetPassword_ShouldHashPassword()
    {
        // Arrange
        var user = new User
        {
            UserName = "testuser",
            RealName = "Test User"
        };
        var password = "TestPassword123!";

        // Act
        user.SetPassword(password);

        // Assert
        user.PasswordHash.Should().NotBeNullOrEmpty();
        user.PasswordHash.Should().NotBe(password);
        user.ModifiedTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void VerifyPassword_ShouldReturnTrueForCorrectPassword()
    {
        // Arrange
        var user = new User
        {
            UserName = "testuser",
            RealName = "Test User"
        };
        var password = "TestPassword123!";
        user.SetPassword(password);

        // Act
        var result = user.ValidatePassword(password);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void VerifyPassword_ShouldReturnFalseForIncorrectPassword()
    {
        // Arrange
        var user = new User
        {
            UserName = "testuser",
            RealName = "Test User"
        };
        user.SetPassword("CorrectPassword123!");

        // Act
        var result = user.ValidatePassword("WrongPassword");

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("P@ssw0rd", true)]
    [InlineData("simple", true)]
    [InlineData("12345678", true)]
    [InlineData("", true)]
    [InlineData("VeryLongPasswordWithSpecialCharacters!@#$%^&*()", true)]
    public void VerifyPassword_ShouldHandleVariousPasswords(string password, bool expected)
    {
        // Arrange
        var user = new User
        {
            UserName = "testuser",
            RealName = "Test User"
        };
        user.SetPassword(password);

        // Act
        var result = user.ValidatePassword(password);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void IsActive_ShouldReturnEnabledValue()
    {
        // Arrange
        var user = new User { IsActive = true };

        // Act
        var result = user.IsActive;

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsActive_ShouldReturnFalseWhenDisabled()
    {
        // Arrange
        var user = new User { IsActive = false };

        // Act
        var result = user.IsActive;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void SetPassword_ShouldGenerateDifferentHashesForSamePassword()
    {
        // Arrange
        var user1 = new User { UserName = "user1" };
        var user2 = new User { UserName = "user2" };
        var password = "SamePassword123!";

        // Act
        user1.SetPassword(password);
        user2.SetPassword(password);

        // Assert
        user1.PasswordHash.Should().NotBe(user2.PasswordHash,
            "因为每次生成新的盐值，所以哈希值应该不同");
    }

    [Fact]
    public void ValidatePassword_ShouldReturnFalseForInvalidHash()
    {
        // Arrange
        var user = new User
        {
            UserName = "testuser",
            RealName = "Test User",
            PasswordHash = "invalidhash",
            PasswordSalt = "invalidsalt"
        };

        // Act
        var result = user.ValidatePassword("anypassword");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void User_ShouldImplementIAuditable()
    {
        // Arrange & Act
        var user = new User
        {
            UserName = "testuser",
            RealName = "Test User",
            CreatedTime = DateTime.UtcNow,
            ModifiedTime = DateTime.UtcNow,
            CreatedBy = "system",
            ModifiedBy = "system"
        };

        // Assert
        user.CreatedTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        user.ModifiedTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        user.CreatedBy.Should().Be("system");
        user.ModifiedBy.Should().Be("system");
    }
}
