using FluentAssertions;
using Moq;
using Wms.Core.Application.Persistence;
using Wms.Core.Domain.Abstractions;
using Wms.Core.Engine;
using Xunit;

namespace Wms.Core.UnitTests.Flow;

public class FlowContextTests
{
    [Fact]
    public void Constructor_WithDbAndUnitOfWork_AssignsBoth()
    {
        var mockDb = new Mock<IFlowDbContext>();
        var mockUow = new Mock<IUnitOfWork>();

        var ctx = new FlowContext(mockDb.Object, mockUow.Object);

        ctx.Db.Should().BeSameAs(mockDb.Object);
        ctx.UnitOfWork.Should().BeSameAs(mockUow.Object);
    }

    [Fact]
    public void Constructor_WithNullDb_Throws()
    {
        var mockUow = new Mock<IUnitOfWork>();
        var act = () => new FlowContext(null!, mockUow.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("db");
    }

    [Fact]
    public void Constructor_WithNullUnitOfWork_Throws()
    {
        var mockDb = new Mock<IFlowDbContext>();
        var act = () => new FlowContext(mockDb.Object, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("unitOfWork");
    }
}
