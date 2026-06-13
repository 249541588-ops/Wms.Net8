using Wms.Core.Domain.Entities.StockFlow;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wms.Core.Infrastructure.Persistence.Configurations;

internal class MonthlyReportEntryConfiguration : IEntityTypeConfiguration<MonthlyReportEntry>
{
    public void Configure(EntityTypeBuilder<MonthlyReportEntry> builder)
    {
        builder.ToTable("MonthlyReportEntries");
        builder.HasKey(x => new { x.Month, x.Material, x.Batch, x.StockStatus, x.Uom });
    }
}
