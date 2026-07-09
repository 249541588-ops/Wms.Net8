using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wms.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTransTaskUnitloadCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 仅新增 TransTasks.UnitloadCode 快照列（托盘编码保留修复）。
            // 注意：FlowNodes.IsDeleted/IsPostTransaction、WasteBatchSetting 表、TSysTimedTask 移除
            // 等模型与快照的差异已存在于数据库中（代码已引用），属历史漂移，不在此迁移处理。
            migrationBuilder.AddColumn<string>(
                name: "UnitloadCode",
                table: "TransTasks",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UnitloadCode",
                table: "TransTasks");
        }
    }
}
