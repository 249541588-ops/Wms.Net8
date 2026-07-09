using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Wms.Core.Domain.Entities.Identity;
using Wms.Core.Domain.Repositories;
using Wms.Core.Domain.Services;
using Wms.Core.Infrastructure.Persistence;
using Wms.Core.Infrastructure.Services;
using Xunit;

namespace Wms.Core.UnitTests.Services;

/// <summary>
/// AuthService 单元测试
/// </summary>
public class AuthServiceTests
{
    private readonly Mock<IUserRepository> _mockUserRepo;
    private readonly Mock<IRepository<User, int>> _mockRepository;
    private readonly Mock<WmsDbContext> _mockDb;
    private readonly Mock<IPasswordHasher> _mockPasswordHasher;
    private readonly Mock<ILogger<AuthService>> _mockLogger;

    public AuthServiceTests()
    {
        _mockUserRepo = new Mock<IUserRepository>();
        _mockRepository = new Mock<IRepository<User, int>>();
        _mockDb = new Mock<WmsDbContext>(new DbContextOptions<WmsDbContext>());
        _mockPasswordHasher = new Mock<IPasswordHasher>();
        _mockLogger = new Mock<ILogger<AuthService>>();
    }

    [Fact]
    public async Task LoginAsync_ShouldReturnUser_WhenCredentialsAreValid()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            UserName = "testuser",
            RealName = "Test User",
            IsActive = true
        };
        user.SetPassword("TestPassword123!");

        _mockUserRepo.Setup(r => r.GetByUsername("testuser")).Returns(user);

        var authService = new AuthService(
            _mockUserRepo.Object,
            _mockRepository.Object,
            _mockDb.Object,
            _mockPasswordHasher.Object,
            _mockLogger.Object);

        // Act
        var result = await authService.LoginAsync("testuser", "TestPassword123!");

        // Assert
        result.Should().NotBeNull();
        result!.UserName.Should().Be("testuser");
        result.RealName.Should().Be("Test User");
    }

    [Fact]
    public async Task LoginAsync_ShouldReturnNull_WhenUserNotFound()
    {
        // Arrange
        _mockUserRepo.Setup(r => r.GetByUsername("nonexistent")).Returns((User?)null);

        var authService = new AuthService(
            _mockUserRepo.Object,
            _mockRepository.Object,
            _mockDb.Object,
            _mockPasswordHasher.Object,
            _mockLogger.Object);

        // Act
        var result = await authService.LoginAsync("nonexistent", "password");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task LoginAsync_ShouldReturnNull_WhenUserIsDisabled()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            UserName = "testuser",
            RealName = "Test User",
            IsActive = false
        };
        user.SetPassword("password");

        _mockUserRepo.Setup(r => r.GetByUsername("testuser")).Returns(user);

        var authService = new AuthService(
            _mockUserRepo.Object,
            _mockRepository.Object,
            _mockDb.Object,
            _mockPasswordHasher.Object,
            _mockLogger.Object);

        // Act
        var result = await authService.LoginAsync("testuser", "password");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task LoginAsync_ShouldReturnNull_WhenPasswordIsIncorrect()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            UserName = "testuser",
            RealName = "Test User",
            IsActive = true
        };
        user.SetPassword("CorrectPassword");

        _mockUserRepo.Setup(r => r.GetByUsername("testuser")).Returns(user);

        var authService = new AuthService(
            _mockUserRepo.Object,
            _mockRepository.Object,
            _mockDb.Object,
            _mockPasswordHasher.Object,
            _mockLogger.Object);

        // Act
        var result = await authService.LoginAsync("testuser", "WrongPassword");

        // Assert
        result.Should().BeNull();
    }
}
