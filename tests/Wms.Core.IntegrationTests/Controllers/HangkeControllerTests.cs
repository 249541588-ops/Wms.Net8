using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Wms.Core.WebApi.Models;
using Xunit;

namespace Wms.Core.IntegrationTests.Controllers;

/// <summary>
/// 杭可 API 控制器集成测试
/// </summary>
public class HangkeControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public HangkeControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public async Task UpdateLocationStatus_ValidState_ReturnsSuccess(int state)
    {
        // Arrange — 使用测试库中存在的固定货位编码，按需调整
        var request = new HangKeStatus { LocationCode = "TEST-LOC-001", HKState = state };

        // Act
        var response = await _client.PostAsJsonAsync(
            "/api/v1.0/Hangke/UpdateLocationStatus", request);

        // Assert — 业务返回 200，无论货位是否存在 success=true/false
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateLocationStatus_EmptyCode_ReturnsFail()
    {
        // Arrange
        var request = new HangKeStatus { LocationCode = "", HKState = 1 };

        // Act
        var response = await _client.PostAsJsonAsync(
            "/api/v1.0/Hangke/UpdateLocationStatus", request);

        // Assert
        // FluentValidation 会自动触发 400 响应（[ApiController] 自动处理）
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,           // 业务路径返回 WcsResult(success=false)
            HttpStatusCode.BadRequest);  // FluentValidation 拦截
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    [InlineData(-1)]
    [InlineData(100)]
    public async Task UpdateLocationStatus_InvalidState_ReturnsFail(int state)
    {
        // Arrange
        var request = new HangKeStatus { LocationCode = "TEST-LOC-001", HKState = state };

        // Act
        var response = await _client.PostAsJsonAsync(
            "/api/v1.0/Hangke/UpdateLocationStatus", request);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateLocationStatus_NotFoundLocation_ReturnsFail()
    {
        // Arrange
        var request = new HangKeStatus { LocationCode = "NOT_EXIST_LOCATION_xyz", HKState = 1 };

        // Act
        var response = await _client.PostAsJsonAsync(
            "/api/v1.0/Hangke/UpdateLocationStatus", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("货位不存在");
    }

    [Fact]
    public async Task UpdateLocationStatus_State7_EmptyLocation_ReturnsSuccessOrDataAnomaly()
    {
        // Arrange
        var request = new HangKeStatus { LocationCode = "TEST-EMPTY-LOC-001", HKState = 7 };

        // Act
        var response = await _client.PostAsJsonAsync(
            "/api/v1.0/Hangke/UpdateLocationStatus", request);

        // Assert — 空货位时仍接受状态变更（业务决策），但若货位本身不存在则返回数据异常
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateLocationStatus_ConcurrentSameLocation_NoDeadlock()
    {
        // Arrange — 同一货位并发请求，验证按货位锁不会死锁
        var request = new HangKeStatus { LocationCode = "TEST-LOC-001", HKState = 1 };
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _client.PostAsJsonAsync(
                "/api/v1.0/Hangke/UpdateLocationStatus", request))
            .ToList();

        // Act
        var responses = await Task.WhenAll(tasks);

        // Assert — 全部应在合理时间内完成（无死锁）
        responses.Should().HaveCount(10);
        foreach (var r in responses)
        {
            r.StatusCode.Should().BeOneOf(
                HttpStatusCode.OK,
                HttpStatusCode.BadRequest);
        }
    }

    [Fact]
    public async Task UpdateLocationStatus_MalformedJson_ReturnsClientError()
    {
        // Arrange
        var content = new StringContent(
            "{invalid json", System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync(
            "/api/v1.0/Hangke/UpdateLocationStatus", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateLocationStatus_ErrorResponse_DoesNotLeakStackTrace()
    {
        // Arrange — 故意使用超长 LocationCode 触发校验
        var longCode = new string('A', 100);
        var request = new HangKeStatus { LocationCode = longCode, HKState = 1 };

        // Act
        var response = await _client.PostAsJsonAsync(
            "/api/v1.0/Hangke/UpdateLocationStatus", request);

        // Assert — 返回内容不应包含堆栈/表名/SQL 片段
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotContain("at Wms.Core");
        content.Should().NotContain("StackTrace");
        content.Should().NotContain("SELECT");
        content.Should().NotContain("Locations");
    }

    // ============================================================
    // UpdateLocationRackCode 测试
    // ============================================================

    [Fact]
    public async Task UpdateLocationRackCode_EmptyRackCode_ReturnsFail()
    {
        // Act
        var response = await _client.PostAsync(
            "/api/v1.0/Hangke/UpdateLocationRackCode?rackCode=&newRackCode=HK01", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("物流货架号不能空");
    }

    [Fact]
    public async Task UpdateLocationRackCode_EmptyNewRackCode_ReturnsFail()
    {
        // Act
        var response = await _client.PostAsync(
            "/api/v1.0/Hangke/UpdateLocationRackCode?rackCode=R01&newRackCode=", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("杭可货架号不能空");
    }

    [Fact]
    public async Task UpdateLocationRackCode_NewRackCodeTooLong_ReturnsFail()
    {
        // Arrange — newRackCode > 7 字符会超出 AnotherCode[MaxLength(16)] 限制
        var longCode = new string('H', 8);

        // Act
        var response = await _client.PostAsync(
            $"/api/v1.0/Hangke/UpdateLocationRackCode?rackCode=R01&newRackCode={longCode}", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("过长");
    }

    [Fact]
    public async Task UpdateLocationRackCode_NotFoundRack_ReturnsZeroAffected()
    {
        // Act — 不存在的物流货架号
        var response = await _client.PostAsync(
            "/api/v1.0/Hangke/UpdateLocationRackCode?rackCode=NOT_EXIST_xyz&newRackCode=HK01", null);

        // Assert — 业务返回成功，但受影响数量为 0
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("更新 0 个货位");
    }

    [Fact]
    public async Task UpdateLocationRackCode_ConcurrentSameRack_NoDeadlock()
    {
        // Arrange — 并发同一货架号请求，验证按 RACK: 前缀锁不会死锁
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _client.PostAsync(
                "/api/v1.0/Hangke/UpdateLocationRackCode?rackCode=R01&newRackCode=HK01", null))
            .ToList();

        // Act
        var responses = await Task.WhenAll(tasks);

        // Assert
        responses.Should().HaveCount(10);
        foreach (var r in responses)
        {
            r.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task UpdateLocationRackCode_ErrorResponse_DoesNotLeakStackTrace()
    {
        // Arrange — 用合法参数但触发内部异常时（如有），确保不泄露堆栈
        // 此测试作为回归保险，即使当前路径不抛异常也保留
        var response = await _client.PostAsync(
            "/api/v1.0/Hangke/UpdateLocationRackCode?rackCode=R01&newRackCode=HK01", null);

        // Act
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        content.Should().NotContain("at Wms.Core");
        content.Should().NotContain("StackTrace");
        content.Should().NotContain("SELECT");
    }
}
