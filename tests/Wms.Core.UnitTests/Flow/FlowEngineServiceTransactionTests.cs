using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Wms.Core.Application.Ports;
using Wms.Core.Application.Persistence;
using Wms.Core.Domain.Abstractions;
using Wms.Core.Domain.Entities.Flow;
using Wms.Core.Engine;
using Wms.Core.Infrastructure.Persistence;
using Wms.Core.Infrastructure.Services;
using Xunit;

namespace Wms.Core.UnitTests.Flow;

/// <summary>
/// Tests for FlowEngineService.ExecuteAsync segmented transaction behavior.
///
/// These tests verify that the ExecuteAsync method correctly manages the
/// transaction lifecycle: BeginTransaction at start, Commit+Begin at each
/// IsTransactionBoundary node, Rollback on exception, and Commit/Rollback
/// at the end based on success/failure path.
///
/// Architecture note: FlowEngineService.CreateInstanceAsync (called at the
/// start of ExecuteAsync) uses the WmsDbContext directly (_db.FieldInstances.Add
/// + SaveChangesAsync) to obtain a self-incrementing Id. This couples the test
/// to a real DbContext instance. Full integration tests require either an
/// InMemory EF Core provider or a test database. The tests below use Moq to
/// verify the IUnitOfWork call sequence, with the caveat that the WmsDbContext
/// mock setup for CreateInstanceAsync is non-trivial.
///
/// See Phase 0 plan for follow-up: add EF Core InMemory provider to enable
/// full end-to-end FlowEngineService tests.
/// </summary>
public class FlowEngineServiceTransactionTests
{
    private readonly Mock<IUnitOfWork> _mockUow = new();
    private readonly Mock<IFlowDbContext> _mockFlowDb = new();
    private readonly Mock<ILogger<FlowEngineService>> _mockLogger = new();
    private readonly Mock<IBackgroundTaskQueue> _mockTaskQueue = new();
    private readonly Mock<IMemoryCache> _mockCache = new();
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory = new();

    /// <summary>
    /// Build a FlowContext with mocked IFlowDbContext and IUnitOfWork.
    /// </summary>
    private FlowContext CreateContext()
    {
        return new FlowContext(_mockFlowDb.Object, _mockUow.Object);
    }

    /// <summary>
    /// Build a minimal FlowTemplate with the given nodes.
    /// </summary>
    private static FlowTemplate CreateTemplate(params FlowNode[] nodes)
    {
        return new FlowTemplate
        {
            Id = 1,
            Name = "TestTemplate",
            Code = "TEST",
            Category = "Test",
            Phase = "Request",
            IsActive = true,
            Nodes = nodes.ToList()
        };
    }

    /// <summary>
    /// Create a FlowNode with common defaults.
    /// </summary>
    private static FlowNode CreateNode(
        string nodeType,
        int stepOrder,
        bool isTransactionBoundary = false,
        string? onFailure = "Stop")
    {
        return new FlowNode
        {
            Id = stepOrder,
            TemplateId = 1,
            NodeType = nodeType,
            NodeName = $"Node_{nodeType}",
            StepOrder = stepOrder,
            IsEnabled = true,
            IsDeleted = false,
            IsTransactionBoundary = isTransactionBoundary,
            OnFailure = onFailure
        };
    }

    /// <summary>
    /// A simple INodeHandler stub for testing.
    /// </summary>
    private class StubNodeHandler : INodeHandler
    {
        private readonly NodeResult _result;
        private readonly Action<FlowContext>? _sideEffect;

        public string NodeType { get; }
        public string DisplayName => NodeType;
        public string Category => "Test";
        public string Description => "Test handler";
        public string? ConfigSchema => null;

        public StubNodeHandler(string nodeType, NodeResult result, Action<FlowContext>? sideEffect = null)
        {
            NodeType = nodeType;
            _result = result;
            _sideEffect = sideEffect;
        }

        public Task<NodeResult> ExecuteAsync(FlowContext context, string? configJson)
        {
            _sideEffect?.Invoke(context);
            return Task.FromResult(_result);
        }
    }

    /// <summary>
    /// A handler that throws an exception when executed.
    /// </summary>
    private class ThrowingNodeHandler : INodeHandler
    {
        private readonly Exception _exception;

        public string NodeType { get; }
        public string DisplayName => NodeType;
        public string Category => "Test";
        public string Description => "Throwing handler";
        public string? ConfigSchema => null;

        public ThrowingNodeHandler(string nodeType, Exception? exception = null)
        {
            NodeType = nodeType;
            _exception = exception ?? new InvalidOperationException("Test exception");
        }

        public Task<NodeResult> ExecuteAsync(FlowContext context, string? configJson)
            => throw _exception;
    }

    // ========================================================================
    // Transaction sequence verification tests
    //
    // These tests verify that ExecuteAsync calls the correct sequence of
    // IUnitOfWork methods (BeginTransactionAsync, CommitAsync, RollbackAsync).
    //
    // NOTE: FlowEngineService constructor requires WmsDbContext (concrete class),
    // and CreateInstanceAsync calls _db.FlowInstances.Add() + SaveChangesAsync()
    // which requires a real EF Core provider. These tests are marked Skip until
    // we can either:
    //   1. Add Microsoft.EntityFrameworkCore.InMemory to the test project, or
    //   2. Refactor FlowEngineService to depend on IFlowDbContext instead of WmsDbContext
    //
    // The transaction call sequence logic is documented here for future verification.
    // ========================================================================

    [Fact(Skip = "Requires WmsDbContext with InMemory provider for CreateInstanceAsync. Phase 0 follow-up.")]
    public async Task ExecuteAsync_SuccessPath_ShouldCall_Begin_Commit_Begin_Commit_Sequence()
    {
        // Arrange: 3 nodes where node 2 is a transaction boundary
        // Expected UoW call sequence: Begin, [node1], Commit, Begin, [node2 boundary], [node3], Commit
        var nodes = new[]
        {
            CreateNode("NodeA", 1),
            CreateNode("NodeB", 2, isTransactionBoundary: true),
            CreateNode("NodeC", 3)
        };
        var template = CreateTemplate(nodes);
        var context = CreateContext();

        // Setup: CreateInstanceAsync needs FlowInstances.Add + SaveChangesAsync
        var mockDbSet = new Mock<DbSet<FlowInstance>>();
        // ... additional setup needed for WmsDbContext ...

        var handlers = new INodeHandler[]
        {
            new StubNodeHandler("NodeA", NodeResult.Ok()),
            new StubNodeHandler("NodeB", NodeResult.Ok()),
            new StubNodeHandler("NodeC", NodeResult.Ok())
        };

        // var service = CreateService(handlers);
        // Act
        // var result = await service.ExecuteAsync(template, context);

        // Assert: verify the UoW call sequence
        // The mock should verify:
        //   1. BeginTransactionAsync called once at start
        //   2. CommitAsync called once at boundary node
        //   3. BeginTransactionAsync called again after boundary commit
        //   4. CommitAsync called once at end (final commit)
    }

    [Fact(Skip = "Requires WmsDbContext with InMemory provider for CreateInstanceAsync. Phase 0 follow-up.")]
    public async Task ExecuteAsync_ExceptionWithOnFailureStop_ShouldCall_Begin_Rollback_Sequence()
    {
        // Arrange: node that throws, OnFailure = Stop
        // Expected: Begin, [throw], Rollback
        var nodes = new[]
        {
            CreateNode("ThrowingNode", 1, onFailure: "Stop")
        };
        var template = CreateTemplate(nodes);
        var context = CreateContext();

        var handlers = new INodeHandler[]
        {
            new ThrowingNodeHandler("ThrowingNode")
        };

        // var service = CreateService(handlers);
        // var result = await service.ExecuteAsync(template, context);

        // Assert:
        //   1. BeginTransactionAsync called once at start
        //   2. RollbackAsync called once in catch block
        //   3. No additional BeginTransactionAsync (OnFailure=Stop, so no re-begin)
        //   4. RollbackAsync called once in failure path at end
        //   (Note: the catch-block Rollback + the failure-path Rollback
        //    both target the same transaction segment)
    }

    [Fact(Skip = "Requires WmsDbContext with InMemory provider for CreateInstanceAsync. Phase 0 follow-up.")]
    public async Task ExecuteAsync_ExceptionWithOnFailureSkip_ShouldCall_Begin_Rollback_Begin_Sequence()
    {
        // Arrange: first node throws (OnFailure=Skip), second node succeeds
        // Expected: Begin, [throw], Rollback, Begin, [success], Commit
        var nodes = new[]
        {
            CreateNode("ThrowingNode", 1, onFailure: "Skip"),
            CreateNode("SuccessNode", 2)
        };
        var template = CreateTemplate(nodes);
        var context = CreateContext();

        var handlers = new INodeHandler[]
        {
            new ThrowingNodeHandler("ThrowingNode"),
            new StubNodeHandler("SuccessNode", NodeResult.Ok())
        };

        // var service = CreateService(handlers);
        // var result = await service.ExecuteAsync(template, context);

        // Assert:
        //   1. BeginTransactionAsync at start
        //   2. RollbackAsync in catch block (after throw)
        //   3. BeginTransactionAsync again (OnFailure=Skip re-begin)
        //   4. CommitAsync at end (success path after recovery)
    }

    [Fact(Skip = "Requires WmsDbContext with InMemory provider for CreateInstanceAsync. Phase 0 follow-up.")]
    public async Task ExecuteAsync_BoundaryCommitFailure_ShouldSetFailedResult()
    {
        // Arrange: boundary node succeeds but CommitAsync throws
        // Expected: result should indicate failure
        var nodes = new[]
        {
            CreateNode("BoundaryNode", 1, isTransactionBoundary: true)
        };
        var template = CreateTemplate(nodes);
        var context = CreateContext();

        // Setup: CommitAsync throws on second call (final commit)
        // _mockUow.Setup(u => u.CommitAsync(default))
        //     .ThrowsAsync(new Exception("Commit failed"));

        var handlers = new INodeHandler[]
        {
            new StubNodeHandler("BoundaryNode", NodeResult.Ok())
        };

        // var service = CreateService(handlers);
        // var result = await service.ExecuteAsync(template, context);

        // Assert: result.success should be false
    }

    /// <summary>
    /// Sanity test: verify that FlowContext correctly wires IUnitOfWork
    /// so that ExecuteAsync can access context.UnitOfWork.
    /// This test does NOT require WmsDbContext.
    /// </summary>
    [Fact]
    public void FlowContext_UnitOfWork_IsAccessible()
    {
        var context = CreateContext();

        context.UnitOfWork.Should().BeSameAs(_mockUow.Object);
    }

    /// <summary>
    /// Verify that IUnitOfWork interface has all required methods for
    /// segmented transaction pattern.
    /// </summary>
    [Fact]
    public void IUnitOfWork_DeclaresAllRequiredMethods()
    {
        var uowType = typeof(IUnitOfWork);

        uowType.GetMethod(nameof(IUnitOfWork.BeginTransactionAsync)).Should().NotBeNull();
        uowType.GetMethod(nameof(IUnitOfWork.CommitAsync)).Should().NotBeNull();
        uowType.GetMethod(nameof(IUnitOfWork.RollbackAsync)).Should().NotBeNull();
        uowType.GetMethod(nameof(IUnitOfWork.SaveChangesAsync)).Should().NotBeNull();
    }

    /// <summary>
    /// Verify that WmsDbContext implements IUnitOfWork (so context.UnitOfWork
    /// can be the same DbContext instance when resolved from DI).
    /// </summary>
    [Fact]
    public void WmsDbContext_ImplementsIUnitOfWork()
    {
        typeof(WmsDbContext).Should().Implement<IUnitOfWork>();
    }

    /// <summary>
    /// Verify that WmsDbContext.CommitAsync (explicit interface implementation)
    /// calls SaveChangesAsync + CommitTransactionAsync.
    /// This is verified by reading the source, not by execution here.
    /// The test documents the expected behavior.
    /// </summary>
    [Fact]
    public void WmsDbContext_CommitAsync_IsExplicitInterfaceImplementation()
    {
        // WmsDbContext.CommitAsync is an explicit interface implementation
        // (not a public method on WmsDbContext directly).
        // It should NOT appear as a public method on WmsDbContext.
        var publicCommit = typeof(WmsDbContext).GetMethod(
            "CommitAsync",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        publicCommit.Should().BeNull("CommitAsync is explicit interface implementation");

        // But it should be available via IUnitOfWork
        var interfaceCommit = typeof(IUnitOfWork).GetMethod("CommitAsync");
        interfaceCommit.Should().NotBeNull();
    }
}
