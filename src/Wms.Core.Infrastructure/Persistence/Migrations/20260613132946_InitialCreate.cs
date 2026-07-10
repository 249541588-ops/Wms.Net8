using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wms.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AllowedOpTypes",
                columns: table => new
                {
                    role_key = table.Column<int>(type: "int", nullable: false),
                    id = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AllowedOpTypes", x => new { x.role_key, x.id });
                });

            migrationBuilder.CreateTable(
                name: "AppSeqs",
                columns: table => new
                {
                    SeqName = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    NextVal = table.Column<int>(type: "int", nullable: true),
                    Increment = table.Column<int>(type: "int", nullable: true),
                    MinValue = table.Column<int>(type: "int", nullable: true),
                    MaxValue = table.Column<int>(type: "int", nullable: true),
                    Cycle = table.Column<bool>(type: "bit", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSeqs", x => x.SeqName);
                });

            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    SettingName = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SettingType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsReadOnly = table.Column<bool>(type: "bit", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.SettingName);
                });

            migrationBuilder.CreateTable(
                name: "ArchivedTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TaskCode = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    TaskType = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UnitloadCode = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    FromLocationCode = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ToLocationCode = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ActualLocationCode = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ForWcs = table.Column<bool>(type: "bit", nullable: true),
                    WasSentToWcs = table.Column<bool>(type: "bit", nullable: true),
                    SentToWcsAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OrderCode = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Ext1 = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Ext2 = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    WareHouse = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    LocationGroup = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Cancelled = table.Column<bool>(type: "bit", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchivedTasks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ArchivedUnitloadItemDetails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UnitloadItemId = table.Column<int>(type: "int", nullable: true),
                    BarCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    xLevel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    OCV3 = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    IR3 = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    V3KeYa = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    OCV4 = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    IR4 = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    V4KeYa = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Capacity = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    KVal = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CCP = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Dcirnz = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Sequence = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LocIndex = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchivedUnitloadItemDetails", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ArchivedUnitloadItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UnitloadId = table.Column<int>(type: "int", nullable: false),
                    MaterialId = table.Column<int>(type: "int", nullable: false),
                    Batch = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    StockStatus = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FalseQuantity = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Uom = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    ProductionTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OutOrdering = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    BoxCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Position = table.Column<int>(type: "int", nullable: true),
                    xLevel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    OperationNumber = table.Column<int>(type: "int", nullable: false),
                    BatchNumber = table.Column<int>(type: "int", nullable: true),
                    IsAdvance = table.Column<int>(type: "int", nullable: true),
                    IsSupplement = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchivedUnitloadItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ArchivedUnitloads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContainerCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Weight = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Height = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Length = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Width = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Volume = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    StorageGroup = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    OutFlag = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ContainerSpecification = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    HasCountingError = table.Column<bool>(type: "bit", nullable: true),
                    HasMsgError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LocationId = table.Column<int>(type: "int", nullable: true),
                    CurrentLocationTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OpHintType = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    OpHintInfo = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ArchiveReason = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    OperationNumber = table.Column<int>(type: "int", nullable: true),
                    CurrentOperation = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    NextOperation = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsExcludeCurrentUnitload = table.Column<bool>(type: "bit", nullable: true),
                    IsUpload = table.Column<bool>(type: "bit", nullable: true),
                    IsAdvance = table.Column<int>(type: "int", nullable: true),
                    IsSupplement = table.Column<int>(type: "int", nullable: true),
                    IsToHangke = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchivedUnitloads", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuthSettings",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    OpType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AllowedRoles = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Module = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BackgroundJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    JobType = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ApiUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RequestType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    JobName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Payload = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    JobArgs = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CronExpression = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CronExpressionDescription = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    State = table.Column<int>(type: "int", nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackgroundJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BasicDictionary",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParentId = table.Column<int>(type: "int", nullable: false),
                    No = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Value = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Abbreviation = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    FullPinyin = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Sort = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IsNext = table.Column<bool>(type: "bit", nullable: false),
                    ExpandField1 = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ExpandField2 = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BasicDictionary", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BatchCount",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Batch = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    BatchNumber = table.Column<int>(type: "int", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ctime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    mtime = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BatchCount", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BatteryOps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContainerCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    BarCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    OpType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    xLevel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    LocIndex = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreateUser = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BatteryOps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BizTypeInfos",
                columns: table => new
                {
                    BizTypeCode = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    BizType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Enabled = table.Column<bool>(type: "bit", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    AppliesToInboundOrders = table.Column<bool>(type: "bit", nullable: true),
                    AppliesToOutboundOrders = table.Column<bool>(type: "bit", nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: true),
                    Options = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BizTypeInfos", x => x.BizTypeCode);
                });

            migrationBuilder.CreateTable(
                name: "Cells",
                columns: table => new
                {
                    CellId = table.Column<int>(type: "int", nullable: false),
                    LanewayId = table.Column<int>(type: "int", nullable: false),
                    Side = table.Column<int>(type: "int", nullable: false),
                    xColumn = table.Column<int>(type: "int", nullable: false),
                    xLevel = table.Column<int>(type: "int", nullable: false),
                    Shape = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    iByShape = table.Column<int>(type: "int", nullable: false),
                    oByShape = table.Column<int>(type: "int", nullable: false),
                    i1 = table.Column<int>(type: "int", nullable: false),
                    o1 = table.Column<int>(type: "int", nullable: false),
                    i2 = table.Column<int>(type: "int", nullable: false),
                    o2 = table.Column<int>(type: "int", nullable: false),
                    i3 = table.Column<int>(type: "int", nullable: false),
                    o3 = table.Column<int>(type: "int", nullable: false),
                    Level = table.Column<int>(type: "int", nullable: true),
                    InboundOrderByShape = table.Column<int>(type: "int", nullable: true),
                    OutboundOrderByShape = table.Column<int>(type: "int", nullable: true),
                    In1 = table.Column<int>(type: "int", nullable: true),
                    Out1 = table.Column<int>(type: "int", nullable: true),
                    In2 = table.Column<int>(type: "int", nullable: true),
                    Out2 = table.Column<int>(type: "int", nullable: true),
                    In3 = table.Column<int>(type: "int", nullable: true),
                    Out3 = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cells", x => x.CellId);
                });

            migrationBuilder.CreateTable(
                name: "CountingLineItemDetails",
                columns: table => new
                {
                    CountingLineItemDetailId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CountingLineItemId = table.Column<int>(type: "int", nullable: true),
                    BarCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    xLevel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Voltage = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ElectricResistance = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ElectricCurrent = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Sequence = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    LocIndex = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CountingLineItemDetails", x => x.CountingLineItemDetailId);
                });

            migrationBuilder.CreateTable(
                name: "CountingLineItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CountingLineId = table.Column<int>(type: "int", nullable: false),
                    UnitloadItem = table.Column<int>(type: "int", nullable: true),
                    MaterialId = table.Column<int>(type: "int", nullable: false),
                    Batch = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    StockStatus = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    BookQuantity = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CountedQuantity = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AdjustmentReason = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ProductionTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OutOrdering = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    BoxCode = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Uom = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    InventoryQuantity = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ActualQuantity = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DifferenceQuantity = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    VerifyQuantity = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CountingLineItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CountingLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CountingOrderId = table.Column<int>(type: "int", nullable: false),
                    LocationId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Unitload = table.Column<int>(type: "int", nullable: true),
                    Material = table.Column<int>(type: "int", nullable: true),
                    Batch = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    StockStatus = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Uom = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    SystemQuantity = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ActualQuantity = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    LineNumber = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CountingLines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CountingOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CountingOrderCode = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: true),
                    Closed = table.Column<bool>(type: "bit", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Material = table.Column<int>(type: "int", nullable: true),
                    StockStatus = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Batch = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CountingModel = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    RackIds = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CountingOrders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FlowInstances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InstanceCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TemplateId = table.Column<int>(type: "int", nullable: false),
                    BusinessType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BusinessId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CurrentNodeOrder = table.Column<int>(type: "int", nullable: false),
                    ContextJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ErrorMsg = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CompletedTime = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlowInstances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Flows",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    MaterialId = table.Column<int>(type: "int", nullable: false),
                    Batch = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    StockStatus = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Uom = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    BizType = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Direction = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    OpType = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    TxNo = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    OrderCode = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    BizOrder = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ContainerCode = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Ext1 = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Ext2 = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    UnitloadId = table.Column<int>(type: "int", nullable: true),
                    LocationId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Flows", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FlowTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Phase = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsBuiltIn = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    MatchRules = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlowTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InboundLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InboundOrderId = table.Column<int>(type: "int", nullable: false),
                    MaterialId = table.Column<int>(type: "int", nullable: false),
                    Batch = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    StockStatus = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Uom = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    QuantityExpected = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    QuantityReceived = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    LineNumber = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboundLines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InboundOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Version = table.Column<int>(type: "int", nullable: false),
                    InboundOrderCode = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    BizType = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    BizOrder = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    OrderBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ApprovedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Closed = table.Column<bool>(type: "bit", nullable: true),
                    ClosedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboundOrders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LanewayUsage",
                columns: table => new
                {
                    LanewayId = table.Column<int>(type: "int", nullable: false),
                    StorageGroup = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Specification = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    WeightLimit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    HeightLimit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    mtime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Total = table.Column<int>(type: "int", nullable: false),
                    Available = table.Column<int>(type: "int", nullable: false),
                    Loaded = table.Column<int>(type: "int", nullable: false),
                    InboundDisabled = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LanewayUsage", x => new { x.LanewayId, x.StorageGroup, x.Specification, x.WeightLimit, x.HeightLimit });
                });

            migrationBuilder.CreateTable(
                name: "LocationAllocRuleStats",
                columns: table => new
                {
                    RuleName = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TotalTimes = table.Column<int>(type: "int", nullable: true),
                    TotalDuration = table.Column<double>(type: "float", nullable: true),
                    SuccessTimes = table.Column<int>(type: "int", nullable: true),
                    LastRunTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastRunSuccess = table.Column<bool>(type: "bit", nullable: true),
                    LastRunTarget = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    LastRunDuration = table.Column<double>(type: "float", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocationAllocRuleStats", x => x.RuleName);
                });

            migrationBuilder.CreateTable(
                name: "LocationOps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    LocationId = table.Column<int>(type: "int", nullable: false),
                    OpType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PreviousState = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    NewState = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocationOps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Materials",
                columns: table => new
                {
                    MaterialId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Version = table.Column<int>(type: "int", nullable: false),
                    MaterialCode = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    MaterialType = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    SpareCode = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Specification = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    MnemonicCode = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    BatchEnabled = table.Column<bool>(type: "bit", nullable: true),
                    MaterialGroup = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ValidDays = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    StandingTime = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AbcClass = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Uom = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    DefaultStorageGroup = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Barcode = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Enabled = table.Column<bool>(type: "bit", nullable: true),
                    UnitVolume = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    UnitLength = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    UnitWidth = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    UnitHeight = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    UnitWeight = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    LowerBound = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    UpperBound = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DefaultQuantity = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Materials", x => x.MaterialId);
                });

            migrationBuilder.CreateTable(
                name: "Menus",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParentId = table.Column<int>(type: "int", nullable: false),
                    Sort = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    EnglishName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    GermanName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Url = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ImgUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDisplay = table.Column<int>(type: "int", nullable: false),
                    FunctionButton = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Creator = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Editor = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EditTime = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Menus", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MonthlyReportEntries",
                columns: table => new
                {
                    Month = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Material = table.Column<int>(type: "int", nullable: false),
                    Batch = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StockStatus = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Uom = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    v = table.Column<int>(type: "int", nullable: false),
                    Beginning = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Incoming = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Outgoing = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Ending = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonthlyReportEntries", x => new { x.Month, x.Material, x.Batch, x.StockStatus, x.Uom });
                });

            migrationBuilder.CreateTable(
                name: "MonthlyReports",
                columns: table => new
                {
                    Month = table.Column<DateTime>(type: "datetime2", nullable: false),
                    v = table.Column<int>(type: "int", nullable: false),
                    ctime = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonthlyReports", x => new { x.Month, x.v });
                });

            migrationBuilder.CreateTable(
                name: "Ocv3ScanCodeBatchProcess",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LocationCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ContainerCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Batch = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    CreateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdateTime = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ocv3ScanCodeBatchProcess", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboundBatch",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LanewayId = table.Column<int>(type: "int", nullable: false),
                    MaterialId = table.Column<int>(type: "int", nullable: false),
                    CurrentOperation = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    OperationNumber = table.Column<int>(type: "int", nullable: true),
                    Batch = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    xLevel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    QuantityRequired = table.Column<int>(type: "int", nullable: false),
                    QuantityDelivered = table.Column<int>(type: "int", nullable: false),
                    IsAdvance = table.Column<int>(type: "int", nullable: false),
                    IsSupplement = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Sort = table.Column<int>(type: "int", nullable: true),
                    ErrorCount = table.Column<int>(type: "int", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ModifiedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboundBatch", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboundLineAllocations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OutboundLineId = table.Column<int>(type: "int", nullable: true),
                    StockId = table.Column<int>(type: "int", nullable: true),
                    UnitloadItemId = table.Column<int>(type: "int", nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AllocatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboundLineAllocations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboundLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OutboundOrderId = table.Column<int>(type: "int", nullable: false),
                    MaterialId = table.Column<int>(type: "int", nullable: false),
                    Batch = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    StockStatus = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Uom = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    QuantityRequired = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    QuantityDelivered = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    WaveLineId = table.Column<int>(type: "int", nullable: true),
                    LineNumber = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboundLines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboundOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Version = table.Column<int>(type: "int", nullable: false),
                    OutboundOrderCode = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    BizType = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    BizOrder = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ShipTo = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ShipBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ShipBefore = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OrderBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ApprovedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Closed = table.Column<bool>(type: "bit", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Wave = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboundOrders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Ports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PortCode = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    PortName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    PortType = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    IsAvailable = table.Column<bool>(type: "bit", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    KP1 = table.Column<int>(type: "int", nullable: true),
                    KP2 = table.Column<int>(type: "int", nullable: true),
                    CurrentUatType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    CurrentUatId = table.Column<int>(type: "int", nullable: true),
                    CheckedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CheckMessage = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ModifiedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Token = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    JwtTokenId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiryTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsUsed = table.Column<bool>(type: "bit", nullable: false),
                    IsRevoked = table.Column<bool>(type: "bit", nullable: false),
                    RevokedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Role_Menu",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MenuId = table.Column<int>(type: "int", nullable: false),
                    RoleId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Role_Menu", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Role_Menu_Funs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MenuId = table.Column<int>(type: "int", nullable: false),
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    FunctionButton = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Role_Menu_Funs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ROLE_OPTYPE",
                columns: table => new
                {
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    OpType = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ROLE_OPTYPE", x => new { x.RoleId, x.OpType });
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RoleName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    IsBuiltIn = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Stocks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    MaterialId = table.Column<int>(type: "int", nullable: false),
                    Batch = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    StockStatus = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AllocatedQuantity = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PickedQuantity = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    LockedQuantity = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ProductionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReceivedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Uom = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    OutOrdering = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Stocktaking = table.Column<bool>(type: "bit", nullable: true),
                    AgeBaseline = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UnitloadId = table.Column<int>(type: "int", nullable: true),
                    LocationId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stocks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StockStatusInfos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    StockStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Enabled = table.Column<bool>(type: "bit", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockStatusInfos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sys_Language",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Chinese = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ChineseDesc = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    English = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Deutsch = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Indonesian = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Module = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsPackageContent = table.Column<int>(type: "int", nullable: false),
                    Creator = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Modifier = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifyDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sys_Language", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HttpMethod = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Module = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    StatusCode = table.Column<int>(type: "int", nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    RequestBody = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Success = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TSysTimedTask",
                columns: table => new
                {
                    TaskName = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    GroupName = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PID = table.Column<long>(type: "bigint", nullable: false),
                    Interval = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ApiUrl = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    AuthKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AuthValue = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Describe = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Creator = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ModifyDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Modifier = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RequestType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TSysTimedTask", x => new { x.TaskName, x.GroupName });
                });

            migrationBuilder.CreateTable(
                name: "UnionUnitloadItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UnitloadId = table.Column<int>(type: "int", nullable: true),
                    MaterialId = table.Column<int>(type: "int", nullable: true),
                    Batch = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    StockStatus = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Uom = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ProductionTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OutOrdering = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnionUnitloadItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UnionUnitloads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContainerCode = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Weight = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Height = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Length = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Width = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Volume = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    StorageGroup = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    OutFlag = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ContainerSpecification = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Archived = table.Column<bool>(type: "bit", nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    OpHintType = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    OpHintInfo = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    LocationId = table.Column<int>(type: "int", nullable: true),
                    CurrentLocationTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BeingMoved = table.Column<bool>(type: "bit", nullable: true),
                    Allocated = table.Column<bool>(type: "bit", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnionUnitloads", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UnitloadOps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OpType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Direction = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ContainerCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitloadOps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UploadMesInfo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContainerCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    LocationCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    BizType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Direction = table.Column<int>(type: "int", nullable: true),
                    OpType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CurrentOperation = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    MestextInfo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MesIsFlag = table.Column<int>(type: "int", nullable: false),
                    MesMsg = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ctime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    mtime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ErrCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UploadMesInfo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PasswordSalt = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RealName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IsBuiltIn = table.Column<bool>(type: "bit", nullable: false),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false),
                    LockedReason = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    LastLoginTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastLoginIp = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Warehouses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    xVersion = table.Column<int>(type: "int", nullable: false),
                    UserCode = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: true),
                    xName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Telephone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    AreaCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PostCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Comments = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Warehouses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WaveLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WaveId = table.Column<int>(type: "int", nullable: false),
                    OutboundLineId = table.Column<int>(type: "int", nullable: true),
                    QuantityRequired = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    LineNumber = table.Column<int>(type: "int", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WaveLines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Waves",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Version = table.Column<int>(type: "int", nullable: false),
                    WaveCode = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    WaveName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    PlannedShipTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActualShipTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Waves", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FlowNodeLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InstanceId = table.Column<int>(type: "int", nullable: false),
                    NodeOrder = table.Column<int>(type: "int", nullable: false),
                    NodeType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NodeName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    InputJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    OutputJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ErrorMsg = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlowNodeLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FlowNodeLogs_FlowInstances_InstanceId",
                        column: x => x.InstanceId,
                        principalTable: "FlowInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FlowNodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TemplateId = table.Column<int>(type: "int", nullable: false),
                    NodeType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NodeName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StepOrder = table.Column<int>(type: "int", nullable: false),
                    ConfigJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    OnFailure = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SkipCondition = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsTransactionBoundary = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlowNodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FlowNodes_FlowTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "FlowTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BatteryCells",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaterialId = table.Column<int>(type: "int", nullable: false),
                    IsSendPack = table.Column<int>(type: "int", nullable: true),
                    Batch = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    BarCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    xLevel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    OCV3 = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    IR3 = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    V3KeYa = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    OCV4 = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    IR4 = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    V4KeYa = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Capacity = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    KVal = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CCP = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Dcirnz = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Sequence = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    LocIndex = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    OperationNumber = table.Column<int>(type: "int", nullable: true),
                    IsAdvance = table.Column<int>(type: "int", nullable: true),
                    ContainerCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BatteryCells", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BatteryCells_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "MaterialId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BatteryCellSorting",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaterialId = table.Column<int>(type: "int", nullable: false),
                    PickName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PickId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    XSpecification = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CapacityMin = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CapacityMax = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OCV4Min = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OCV4Max = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IR4Min = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IR4Max = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    KValMin = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    KValMax = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DcirnzMin = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DcirnzMax = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Passageway = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsEnable = table.Column<short>(type: "smallint", nullable: false),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BatteryCellSorting", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BatteryCellSorting_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "MaterialId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Laneways",
                columns: table => new
                {
                    LanewayId = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    WarehouseId = table.Column<int>(type: "int", nullable: true),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    LanewayCode = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Area = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Automated = table.Column<bool>(type: "bit", nullable: true),
                    Offline = table.Column<bool>(type: "bit", nullable: true),
                    OfflineComment = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    TakeOfflineTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TotalOfflineHours = table.Column<double>(type: "float", nullable: true),
                    DoubleDeep = table.Column<bool>(type: "bit", nullable: true),
                    ReservedLocationCount = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Laneways", x => x.LanewayId);
                    table.ForeignKey(
                        name: "FK_Laneways_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Laneway_Port",
                columns: table => new
                {
                    PortId = table.Column<int>(type: "int", nullable: false),
                    LanewayId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Laneway_Port", x => new { x.LanewayId, x.PortId });
                    table.ForeignKey(
                        name: "FK_Laneway_Port_Laneways_LanewayId",
                        column: x => x.LanewayId,
                        principalTable: "Laneways",
                        principalColumn: "LanewayId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Laneway_Port_Ports_PortId",
                        column: x => x.PortId,
                        principalTable: "Ports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Racks",
                columns: table => new
                {
                    RackId = table.Column<int>(type: "int", nullable: false),
                    WarehouseId = table.Column<int>(type: "int", nullable: true),
                    LanewayId = table.Column<int>(type: "int", nullable: true),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    RackCode = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Side = table.Column<int>(type: "int", nullable: true),
                    Deep = table.Column<int>(type: "int", nullable: true),
                    Columns = table.Column<int>(type: "int", nullable: true),
                    Levels = table.Column<int>(type: "int", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Racks", x => x.RackId);
                    table.ForeignKey(
                        name: "FK_Racks_Laneways_LanewayId",
                        column: x => x.LanewayId,
                        principalTable: "Laneways",
                        principalColumn: "LanewayId");
                    table.ForeignKey(
                        name: "FK_Racks_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Locations",
                columns: table => new
                {
                    LocationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Version = table.Column<int>(type: "int", nullable: false),
                    LocationCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    LocationType = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    InboundCount = table.Column<int>(type: "int", nullable: false),
                    InboundLimit = table.Column<int>(type: "int", nullable: false),
                    InboundDisabled = table.Column<bool>(type: "bit", nullable: false),
                    InboundDisabledComment = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    OutboundCount = table.Column<int>(type: "int", nullable: false),
                    OutboundLimit = table.Column<int>(type: "int", nullable: false),
                    OutboundDisabled = table.Column<bool>(type: "bit", nullable: false),
                    OutboundDisabledComment = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    xExists = table.Column<bool>(type: "bit", nullable: false),
                    WeightLimit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    HeightLimit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    xSpecification = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    RackId = table.Column<int>(type: "int", nullable: true),
                    xColumn = table.Column<int>(type: "int", nullable: false),
                    xLevel = table.Column<int>(type: "int", nullable: false),
                    StorageGroup = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    SubStorageGroup = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    OperationNumber = table.Column<int>(type: "int", nullable: true),
                    Batch = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    UnitloadCount = table.Column<int>(type: "int", nullable: false),
                    CellId = table.Column<int>(type: "int", nullable: true),
                    Tag = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RequestType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    WarehouseId = table.Column<int>(type: "int", nullable: true),
                    AreaName = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AnotherCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    HKPosintionState = table.Column<int>(type: "int", nullable: true),
                    HKPosintionCK = table.Column<int>(type: "int", nullable: true),
                    LanewayCodes = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    DoubleIn = table.Column<int>(type: "int", nullable: true),
                    WeightLimitTemp = table.Column<decimal>(type: "decimal(18,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Locations", x => x.LocationId);
                    table.ForeignKey(
                        name: "FK_Locations_Racks_RackId",
                        column: x => x.RackId,
                        principalTable: "Racks",
                        principalColumn: "RackId");
                    table.ForeignKey(
                        name: "FK_Locations_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Unitloads",
                columns: table => new
                {
                    UnitloadId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Version = table.Column<int>(type: "int", nullable: true),
                    ContainerCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Weight = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Height = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Length = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Width = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Volume = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    StorageGroup = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    OutFlag = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ContainerSpecification = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    HasCountingError = table.Column<bool>(type: "bit", nullable: true),
                    HasMsgError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Odd = table.Column<bool>(type: "bit", nullable: true),
                    BeingMoved = table.Column<bool>(type: "bit", nullable: true),
                    Allocated = table.Column<bool>(type: "bit", nullable: true),
                    LocationId = table.Column<int>(type: "int", nullable: true),
                    CurrentLocationTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OpHintType = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    OpHintInfo = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    outboundorder_key = table.Column<int>(type: "int", nullable: true),
                    OperationNumber = table.Column<int>(type: "int", nullable: true),
                    CurrentOperation = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    NextOperation = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsExcludeCurrentUnitload = table.Column<bool>(type: "bit", nullable: true),
                    IsUpload = table.Column<bool>(type: "bit", nullable: true),
                    IsAdvance = table.Column<int>(type: "int", nullable: true),
                    IsSupplement = table.Column<int>(type: "int", nullable: true),
                    IsToHangke = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Unitloads", x => x.UnitloadId);
                    table.ForeignKey(
                        name: "FK_Unitloads_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "LocationId");
                });

            migrationBuilder.CreateTable(
                name: "TransTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    TaskCode = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    TaskType = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    UnitloadId = table.Column<int>(type: "int", nullable: true),
                    StartLocationId = table.Column<int>(type: "int", nullable: false),
                    EndLocationId = table.Column<int>(type: "int", nullable: false),
                    ForWcs = table.Column<bool>(type: "bit", nullable: true),
                    WasSentToWcs = table.Column<bool>(type: "bit", nullable: true),
                    SentToWcsAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OrderCode = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Ext1 = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Ext2 = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    WareHouse = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    LocationGroup = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransTasks_Locations_EndLocationId",
                        column: x => x.EndLocationId,
                        principalTable: "Locations",
                        principalColumn: "LocationId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransTasks_Locations_StartLocationId",
                        column: x => x.StartLocationId,
                        principalTable: "Locations",
                        principalColumn: "LocationId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransTasks_Unitloads_UnitloadId",
                        column: x => x.UnitloadId,
                        principalTable: "Unitloads",
                        principalColumn: "UnitloadId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UnitloadItems",
                columns: table => new
                {
                    UnitloadItemId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UnitloadId = table.Column<int>(type: "int", nullable: true),
                    MaterialId = table.Column<int>(type: "int", nullable: true),
                    Batch = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    StockStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    FalseQuantity = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Uom = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ProductionTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OutOrdering = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    BoxCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Position = table.Column<int>(type: "int", nullable: true),
                    xLevel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    OperationNumber = table.Column<int>(type: "int", nullable: true),
                    BatchNumber = table.Column<int>(type: "int", nullable: true),
                    IsAdvance = table.Column<int>(type: "int", nullable: true),
                    IsSupplement = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitloadItems", x => x.UnitloadItemId);
                    table.ForeignKey(
                        name: "FK_UnitloadItems_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "MaterialId");
                    table.ForeignKey(
                        name: "FK_UnitloadItems_Unitloads_UnitloadId",
                        column: x => x.UnitloadId,
                        principalTable: "Unitloads",
                        principalColumn: "UnitloadId");
                });

            migrationBuilder.CreateTable(
                name: "UnitloadItemDetails",
                columns: table => new
                {
                    UnitloadItemDetailId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UnitloadItemId = table.Column<int>(type: "int", nullable: true),
                    BarCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    xLevel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    OCV3 = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    IR3 = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    V3KeYa = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    OCV4 = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    IR4 = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    V4KeYa = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Capacity = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    KVal = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CCP = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Dcirnz = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Sequence = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LocIndex = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitloadItemDetails", x => x.UnitloadItemDetailId);
                    table.ForeignKey(
                        name: "FK_UnitloadItemDetails_UnitloadItems_UnitloadItemId",
                        column: x => x.UnitloadItemId,
                        principalTable: "UnitloadItems",
                        principalColumn: "UnitloadItemId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_BatteryCells_MaterialId",
                table: "BatteryCells",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_BatteryCellSorting_MaterialId",
                table: "BatteryCellSorting",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_FlowNodeLogs_InstanceId",
                table: "FlowNodeLogs",
                column: "InstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_FlowNodes_TemplateId",
                table: "FlowNodes",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_Laneway_Port_PortId",
                table: "Laneway_Port",
                column: "PortId");

            migrationBuilder.CreateIndex(
                name: "IX_Laneways_WarehouseId",
                table: "Laneways",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_Locations_RackId",
                table: "Locations",
                column: "RackId");

            migrationBuilder.CreateIndex(
                name: "IX_Locations_WarehouseId",
                table: "Locations",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_Racks_LanewayId",
                table: "Racks",
                column: "LanewayId");

            migrationBuilder.CreateIndex(
                name: "IX_Racks_WarehouseId",
                table: "Racks",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_TransTasks_EndLocationId",
                table: "TransTasks",
                column: "EndLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_TransTasks_StartLocationId",
                table: "TransTasks",
                column: "StartLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_TransTasks_UnitloadId",
                table: "TransTasks",
                column: "UnitloadId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitloadItemDetails_UnitloadItemId",
                table: "UnitloadItemDetails",
                column: "UnitloadItemId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitloadItems_MaterialId",
                table: "UnitloadItems",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitloadItems_UnitloadId",
                table: "UnitloadItems",
                column: "UnitloadId");

            migrationBuilder.CreateIndex(
                name: "IX_Unitloads_LocationId",
                table: "Unitloads",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_UserId",
                table: "UserRoles",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AllowedOpTypes");

            migrationBuilder.DropTable(
                name: "AppSeqs");

            migrationBuilder.DropTable(
                name: "AppSettings");

            migrationBuilder.DropTable(
                name: "ArchivedTasks");

            migrationBuilder.DropTable(
                name: "ArchivedUnitloadItemDetails");

            migrationBuilder.DropTable(
                name: "ArchivedUnitloadItems");

            migrationBuilder.DropTable(
                name: "ArchivedUnitloads");

            migrationBuilder.DropTable(
                name: "AuthSettings");

            migrationBuilder.DropTable(
                name: "BackgroundJobs");

            migrationBuilder.DropTable(
                name: "BasicDictionary");

            migrationBuilder.DropTable(
                name: "BatchCount");

            migrationBuilder.DropTable(
                name: "BatteryCells");

            migrationBuilder.DropTable(
                name: "BatteryCellSorting");

            migrationBuilder.DropTable(
                name: "BatteryOps");

            migrationBuilder.DropTable(
                name: "BizTypeInfos");

            migrationBuilder.DropTable(
                name: "Cells");

            migrationBuilder.DropTable(
                name: "CountingLineItemDetails");

            migrationBuilder.DropTable(
                name: "CountingLineItems");

            migrationBuilder.DropTable(
                name: "CountingLines");

            migrationBuilder.DropTable(
                name: "CountingOrders");

            migrationBuilder.DropTable(
                name: "FlowNodeLogs");

            migrationBuilder.DropTable(
                name: "FlowNodes");

            migrationBuilder.DropTable(
                name: "Flows");

            migrationBuilder.DropTable(
                name: "InboundLines");

            migrationBuilder.DropTable(
                name: "InboundOrders");

            migrationBuilder.DropTable(
                name: "Laneway_Port");

            migrationBuilder.DropTable(
                name: "LanewayUsage");

            migrationBuilder.DropTable(
                name: "LocationAllocRuleStats");

            migrationBuilder.DropTable(
                name: "LocationOps");

            migrationBuilder.DropTable(
                name: "Menus");

            migrationBuilder.DropTable(
                name: "MonthlyReportEntries");

            migrationBuilder.DropTable(
                name: "MonthlyReports");

            migrationBuilder.DropTable(
                name: "Ocv3ScanCodeBatchProcess");

            migrationBuilder.DropTable(
                name: "OutboundBatch");

            migrationBuilder.DropTable(
                name: "OutboundLineAllocations");

            migrationBuilder.DropTable(
                name: "OutboundLines");

            migrationBuilder.DropTable(
                name: "OutboundOrders");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "Role_Menu");

            migrationBuilder.DropTable(
                name: "Role_Menu_Funs");

            migrationBuilder.DropTable(
                name: "ROLE_OPTYPE");

            migrationBuilder.DropTable(
                name: "Stocks");

            migrationBuilder.DropTable(
                name: "StockStatusInfos");

            migrationBuilder.DropTable(
                name: "Sys_Language");

            migrationBuilder.DropTable(
                name: "SystemLogs");

            migrationBuilder.DropTable(
                name: "TransTasks");

            migrationBuilder.DropTable(
                name: "TSysTimedTask");

            migrationBuilder.DropTable(
                name: "UnionUnitloadItems");

            migrationBuilder.DropTable(
                name: "UnionUnitloads");

            migrationBuilder.DropTable(
                name: "UnitloadItemDetails");

            migrationBuilder.DropTable(
                name: "UnitloadOps");

            migrationBuilder.DropTable(
                name: "UploadMesInfo");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "WaveLines");

            migrationBuilder.DropTable(
                name: "Waves");

            migrationBuilder.DropTable(
                name: "FlowInstances");

            migrationBuilder.DropTable(
                name: "FlowTemplates");

            migrationBuilder.DropTable(
                name: "Ports");

            migrationBuilder.DropTable(
                name: "UnitloadItems");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Materials");

            migrationBuilder.DropTable(
                name: "Unitloads");

            migrationBuilder.DropTable(
                name: "Locations");

            migrationBuilder.DropTable(
                name: "Racks");

            migrationBuilder.DropTable(
                name: "Laneways");

            migrationBuilder.DropTable(
                name: "Warehouses");
        }
    }
}
