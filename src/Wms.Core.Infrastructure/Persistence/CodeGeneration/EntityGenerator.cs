using System.Data;
using System.Text;
using Microsoft.Data.SqlClient;

namespace Wms.Core.Infrastructure.Persistence.CodeGeneration;

/// <summary>
/// 数据库表结构代码生成器
/// 从 SQL Server 数据库读取表结构，自动生成实体类和 NHibernate Mapping 类
/// </summary>
public class EntityGenerator
{
    private readonly string _connectionString;
    private readonly string _entityOutputPath;
    private readonly string _mappingOutputPath;
    private readonly string _entityNamespace;
    private readonly string _mappingNamespace;

    /// <summary>
    /// 初始化代码生成器
    /// </summary>
    /// <param name="connectionString">数据库连接字符串</param>
    /// <param name="entityOutputPath">实体类输出目录（绝对路径）</param>
    /// <param name="mappingOutputPath">Mapping 类输出目录（绝对路径）</param>
    /// <param name="entityNamespace">实体类命名空间，默认 Wms.Core.Domain.Entities</param>
    /// <param name="mappingNamespace">Mapping 类命名空间，默认 Wms.Core.Infrastructure.Persistence.Mappings</param>
    public EntityGenerator(
        string connectionString,
        string entityOutputPath,
        string mappingOutputPath,
        string entityNamespace = "Wms.Core.Domain.Entities",
        string mappingNamespace = "Wms.Core.Infrastructure.Persistence.Mappings")
    {
        _connectionString = connectionString;
        _entityOutputPath = entityOutputPath;
        _mappingOutputPath = mappingOutputPath;
        _entityNamespace = entityNamespace;
        _mappingNamespace = mappingNamespace;
    }

    /// <summary>
    /// 生成指定表的实体类和 Mapping 类。传入 null 或空列表则生成所有表。
    /// </summary>
    /// <param name="tableNames">要生成的表名列表（不传则生成所有表）</param>
    /// <param name="overwrite">是否覆盖已存在的文件</param>
    /// <returns>生成结果摘要</returns>
    public async Task<GenerationResult> GenerateAsync(IEnumerable<string>? tableNames = null, bool overwrite = false)
    {
        var result = new GenerationResult();

        Directory.CreateDirectory(_entityOutputPath);
        Directory.CreateDirectory(_mappingOutputPath);

        var tables = await GetTableSchemasAsync(tableNames);

        foreach (var table in tables)
        {
            var className = ToPascalCase(table.TableName);
            var entityFilePath = Path.Combine(_entityOutputPath, $"{className}.cs");
            var mappingFilePath = Path.Combine(_mappingOutputPath, $"{className}Mapping.cs");

            if (!overwrite && File.Exists(entityFilePath))
            {
                result.Skipped.Add($"{className}（实体类文件已存在）");
                continue;
            }

            if (!overwrite && File.Exists(mappingFilePath))
            {
                result.Skipped.Add($"{className}Mapping（映射类文件已存在）");
                continue;
            }

            var entityCode = GenerateEntityClass(table, className);
            await File.WriteAllTextAsync(entityFilePath, entityCode, Encoding.UTF8);
            result.GeneratedEntities.Add(entityFilePath);

            var mappingCode = GenerateMappingClass(table, className);
            await File.WriteAllTextAsync(mappingFilePath, mappingCode, Encoding.UTF8);
            result.GeneratedMappings.Add(mappingFilePath);
        }

        return result;
    }

    /// <summary>
    /// 根据数据库表结构生成 DTO record 类文件。
    /// 主表生成 {ClassName}Request，明细表生成 {ClassName}RequestItems，合并写入同一个文件。
    /// </summary>
    /// <param name="mainTable">主表名</param>
    /// <param name="detailTable">明细表名（可选，不传则只生成主表 DTO）</param>
    /// <param name="outputPath">输出文件路径（绝对路径）</param>
    /// <param name="dtoNamespace">DTO 命名空间，默认 Wms.Core.Application.DTOs</param>
    /// <param name="overwrite">是否覆盖已存在的文件</param>
    /// <returns>生成结果</returns>
    public async Task<GenerationResult> GenerateDtoAsync(
        string mainTable,
        string? detailTable = null,
        string? outputPath = null,
        string dtoNamespace = "Wms.Core.Application.DTOs",
        bool overwrite = false)
    {
        var result = new GenerationResult();
        var filePath = outputPath ?? _entityOutputPath;

        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        if (!overwrite && File.Exists(filePath))
        {
            result.Skipped.Add($"{Path.GetFileName(filePath)}（文件已存在）");
            return result;
        }

        var tables = await GetTableSchemasAsync(new[] { mainTable });
        if (tables.Count == 0)
        {
            result.Skipped.Add($"{mainTable}（表不存在）");
            return result;
        }

        var mainSchema = tables[0];
        var mainClassName = ToPascalCase(mainSchema.TableName);

        TableSchema? detailSchema = null;
        string detailClassName = "";
        if (!string.IsNullOrWhiteSpace(detailTable))
        {
            var detailTables = await GetTableSchemasAsync(new[] { detailTable });
            if (detailTables.Count > 0)
            {
                detailSchema = detailTables[0];
                detailClassName = ToPascalCase(detailSchema.TableName);
            }
        }

        var code = GenerateDtoClass(mainSchema, mainClassName, detailSchema, detailClassName, dtoNamespace);
        await File.WriteAllTextAsync(filePath, code, Encoding.UTF8);
        result.GeneratedEntities.Add(filePath);

        return result;
    }

    #region DTO 生成

    private string GenerateDtoClass(
        TableSchema mainTable, string mainClassName,
        TableSchema? detailTable, string detailClassName,
        string dtoNamespace)
    {
        var sb = new IndentedStringBuilder();

        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine();
        sb.AppendLine($"namespace {dtoNamespace};");
        sb.AppendLine();

        // 主表 DTO
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// {mainClassName} 请求对象");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"public record {mainClassName}Request");
        sb.AppendLine("{");
        using (sb.Indent())
        {
            foreach (var col in mainTable.Columns)
            {
                var propName = ToPascalCase(col.ColumnName);
                var clrType = MapClrType(col);
                var typeStr = col.IsNullable && clrType != "string" ? $"{clrType}?" : clrType;
                var defaultVal = GetDtoDefaultValue(col, clrType);

                sb.AppendLine("/// <summary>");
                sb.AppendLine("///");
                sb.AppendLine("/// </summary>");
                sb.AppendLine($"public {typeStr} {propName} {{ get; init; }}{defaultVal}");
                sb.AppendLine();
            }

            // Items 集合
            if (detailTable != null)
            {
                sb.AppendLine($"/// <summary>");
                sb.AppendLine("///");
                sb.AppendLine("/// </summary>");
                sb.AppendLine($"public List<{detailClassName}RequestItems> Items {{ get; set; }}");
                sb.AppendLine();
                sb.AppendLine("/// <summary>");
                sb.AppendLine("///");
                sb.AppendLine("/// </summary>");
                sb.AppendLine($"public {mainClassName}Request()");
                sb.AppendLine("{");
                using (sb.Indent())
                {
                    sb.AppendLine($"Items = new List<{detailClassName}RequestItems>();");
                }
                sb.AppendLine("}");
            }
        }
        sb.AppendLine("}");

        // 明细表 DTO
        if (detailTable != null)
        {
            // 跳过外键列（关联主表的列）
            var fkColumns = detailTable.ForeignKeys.Select(fk => fk.ColumnName).ToHashSet();

            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("/// <summary>");
            sb.AppendLine("/// 明细");
            sb.AppendLine("/// </summary>");
            sb.AppendLine($"public record {detailClassName}RequestItems");
            sb.AppendLine("{");
            using (sb.Indent())
            {
                foreach (var col in detailTable.Columns)
                {
                    // 跳过主键和外键列
                    if (col.IsPrimaryKey || fkColumns.Contains(col.ColumnName)) continue;

                    var propName = ToPascalCase(col.ColumnName);
                    var clrType = MapClrType(col);
                    var typeStr = col.IsNullable && clrType != "string" ? $"{clrType}?" : clrType;
                    var defaultVal = GetDtoDefaultValue(col, clrType);

                    sb.AppendLine("/// <summary>");
                    sb.AppendLine("///");
                    sb.AppendLine("/// </summary>");
                    sb.AppendLine($"public {typeStr} {propName} {{ get; init; }}{defaultVal}");
                    sb.AppendLine();
                }
            }
            sb.AppendLine("}");
        }

        return sb.ToString();
    }

    private static string GetDtoDefaultValue(ColumnInfo col, string clrType)
    {
        if (col.IsNullable) return "";

        return clrType switch
        {
            "string" => " = string.Empty;",
            "bool" => col.DefaultValue != null && col.DefaultValue.Contains("1") ? " = true;" : " = false;",
            "int" or "long" or "short" or "byte" => " = 0;",
            "decimal" or "double" => " = 0m;",
            _ => ""
        };
    }

    #endregion

    #region 数据库元数据读取

    private async Task<List<TableSchema>> GetTableSchemasAsync(IEnumerable<string>? tableNames)
    {
        var tables = new List<TableSchema>();

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var tableList = await GetTableListAsync(connection, tableNames);

        foreach (var (schema, tableName) in tableList)
        {
            var columns = await GetColumnsAsync(connection, schema, tableName);
            var foreignKeys = await GetForeignKeysAsync(connection, schema, tableName);

            tables.Add(new TableSchema
            {
                Schema = schema,
                TableName = tableName,
                Columns = columns,
                ForeignKeys = foreignKeys
            });
        }

        return tables;
    }

    private async Task<List<(string Schema, string TableName)>> GetTableListAsync(
        SqlConnection connection, IEnumerable<string>? tableNames)
    {
        var result = new List<(string, string)>();

        var sql = new StringBuilder();
        sql.AppendLine("SELECT TABLE_SCHEMA, TABLE_NAME");
        sql.AppendLine("FROM INFORMATION_SCHEMA.TABLES");
        sql.AppendLine("WHERE TABLE_TYPE = 'BASE TABLE'");

        var names = tableNames?.ToList();
        if (names is { Count: > 0 })
        {
            var conditions = new List<string>();
            for (int i = 0; i < names.Count; i++)
            {
                conditions.Add($"(TABLE_SCHEMA + '.' + TABLE_NAME = @t{i} OR TABLE_NAME = @t{i})");
            }
            sql.AppendLine($"AND ({string.Join(" OR ", conditions)})");
        }

        sql.AppendLine("ORDER BY TABLE_SCHEMA, TABLE_NAME");

        using var cmd = new SqlCommand(sql.ToString(), connection);
        if (names is { Count: > 0 })
        {
            for (int i = 0; i < names.Count; i++)
                cmd.Parameters.AddWithValue($"@t{i}", names[i]);
        }

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result.Add((reader.GetString(0), reader.GetString(1)));

        return result;
    }

    private async Task<List<ColumnInfo>> GetColumnsAsync(SqlConnection connection, string schema, string tableName)
    {
        var columns = new List<ColumnInfo>();

        var sql = @"
SELECT
    c.COLUMN_NAME,
    c.DATA_TYPE,
    c.IS_NULLABLE,
    c.CHARACTER_MAXIMUM_LENGTH,
    c.NUMERIC_PRECISION,
    c.NUMERIC_SCALE,
    c.COLUMN_DEFAULT,
    COLUMNPROPERTY(OBJECT_ID(c.TABLE_SCHEMA + '.' + c.TABLE_NAME), c.COLUMN_NAME, 'IsIdentity'),
    ISNULL(pk.is_primary_key, 0),
    ISNULL(ep.value, '')
FROM INFORMATION_SCHEMA.COLUMNS c
LEFT JOIN (
    SELECT ic.COLUMN_NAME, ccu.TABLE_SCHEMA, ccu.TABLE_NAME, 1 AS is_primary_key
    FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE ic
    INNER JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS ccu
        ON ic.CONSTRAINT_NAME = ccu.CONSTRAINT_NAME
        AND ic.TABLE_SCHEMA = ccu.TABLE_SCHEMA
        AND ic.TABLE_NAME = ccu.TABLE_NAME
    WHERE ccu.CONSTRAINT_TYPE = 'PRIMARY KEY'
) pk ON c.COLUMN_NAME = pk.COLUMN_NAME AND c.TABLE_SCHEMA = pk.TABLE_SCHEMA AND c.TABLE_NAME = pk.TABLE_NAME
LEFT JOIN sys.extended_properties ep ON ep.major_id = OBJECT_ID(c.TABLE_SCHEMA + '.' + c.TABLE_NAME)
    AND ep.minor_id = c.ORDINAL_POSITION
    AND ep.name = 'MS_Description'
WHERE c.TABLE_SCHEMA = @schema AND c.TABLE_NAME = @tableName
ORDER BY c.ORDINAL_POSITION";

        using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@schema", schema);
        cmd.Parameters.AddWithValue("@tableName", tableName);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(new ColumnInfo
            {
                ColumnName = reader.GetString(0),
                DataType = reader.GetString(1),
                IsNullable = reader.GetString(2) == "YES",
                MaxLength = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                NumericPrecision = reader.IsDBNull(4) ? null : reader.GetByte(4),
                NumericScale = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                DefaultValue = reader.IsDBNull(6) ? null : reader.GetString(6),
                IsIdentity = !reader.IsDBNull(7) && Convert.ToInt32(reader[7]) == 1,
                IsPrimaryKey = !reader.IsDBNull(8) && Convert.ToInt32(reader[8]) == 1,
                Description = reader.IsDBNull(9) ? "" : reader.GetString(9)
            });
        }

        return columns;
    }

    private async Task<List<ForeignKeyInfo>> GetForeignKeysAsync(SqlConnection connection, string schema, string tableName)
    {
        var fks = new List<ForeignKeyInfo>();

        var sql = @"
SELECT
    fk.name,
    COL_NAME(fkc.parent_object_id, fkc.parent_column_id),
    OBJECT_SCHEMA_NAME(fkc.referenced_object_id),
    OBJECT_NAME(fkc.referenced_object_id),
    COL_NAME(fkc.referenced_object_id, fkc.referenced_column_id)
FROM sys.foreign_keys fk
INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
WHERE OBJECT_SCHEMA_NAME(fkc.parent_object_id) = @schema
    AND OBJECT_NAME(fkc.parent_object_id) = @tableName
ORDER BY fk.name, fkc.constraint_column_id";

        using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@schema", schema);
        cmd.Parameters.AddWithValue("@tableName", tableName);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            fks.Add(new ForeignKeyInfo
            {
                ForeignKeyName = reader.GetString(0),
                ColumnName = reader.GetString(1),
                ReferencedTable = reader.GetString(3),
                ReferencedColumn = reader.GetString(4)
            });
        }

        return fks;
    }

    #endregion

    #region 实体类生成

    private string GenerateEntityClass(TableSchema table, string className)
    {
        var sb = new IndentedStringBuilder();
        var fkColumns = table.ForeignKeys.Select(fk => fk.ColumnName).ToHashSet();

        bool hasCreatedTime = table.Columns.Any(c => c.ColumnName == "CreatedTime");
        bool hasModifiedTime = table.Columns.Any(c => c.ColumnName == "ModifiedTime");
        bool hasCreatedBy = table.Columns.Any(c => c.ColumnName == "CreatedBy");
        bool hasModifiedBy = table.Columns.Any(c => c.ColumnName == "ModifiedBy");
        bool isAuditable = hasCreatedTime && hasModifiedTime;
        bool hasVersion = table.Columns.Any(c => c.ColumnName == "Version");

        var pkColumns = table.Columns.Where(c => c.IsPrimaryKey).ToList();
        var pkType = pkColumns.Count == 1 ? MapClrType(pkColumns[0]) : "int";
        var isStringPk = pkType == "string";

        // 判断是否需要 DataAnnotations using
        bool needsDataAnnotations = table.Columns.Any(c =>
            !c.IsPrimaryKey && !fkColumns.Contains(c.ColumnName) &&
            ((c.MaxLength.HasValue && c.MaxLength.Value is > 0 and < int.MaxValue && MapClrType(c) == "string")
             || (!c.IsNullable && !c.IsIdentity && c.DefaultValue == null
                 && c.ColumnName != "CreatedTime" && c.ColumnName != "ModifiedTime" && c.ColumnName != "Version")));

        sb.AppendLine("using Wms.Core.Domain.Interfaces;");
        if (needsDataAnnotations)
            sb.AppendLine("using System.ComponentModel.DataAnnotations;");
        sb.AppendLine();
        sb.AppendLine($"namespace {_entityNamespace};");
        sb.AppendLine();

        // 类注释
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// {className} 实体");
        sb.AppendLine("/// </summary>");

        // 类声明
        var interfaces = new List<string> { $"IEntity<{pkType}>" };
        if (isAuditable) interfaces.Add("IAuditable");
        if (hasVersion) interfaces.Add("IVersioned");

        sb.AppendLine($"public class {className} : {string.Join(", ", interfaces)}");
        sb.AppendLine("{");

        using (sb.Indent())
        {
            // 主键
            if (pkColumns.Count == 1)
            {
                sb.AppendLine("/// <summary>");
                sb.AppendLine("/// 主键");
                sb.AppendLine("/// </summary>");
                var pkDefault = isStringPk ? " = string.Empty;" : "";
                sb.AppendLine($"public virtual {pkType} Id {{ get; set; }}{pkDefault}");
                sb.AppendLine();
            }

            // Version
            if (hasVersion)
            {
                sb.AppendLine("/// <summary>");
                sb.AppendLine("/// 版本号（用于乐观并发控制）");
                sb.AppendLine("/// </summary>");
                sb.AppendLine("public virtual int Version { get; set; }");
                sb.AppendLine();
            }

            // 审计字段
            if (isAuditable)
            {
                sb.AppendLine("#region IAuditable 实现");
                sb.AppendLine();
                if (hasCreatedTime)
                {
                    sb.AppendLine("/// <summary>");
                    sb.AppendLine("/// 创建时间");
                    sb.AppendLine("/// </summary>");
                    sb.AppendLine("public virtual DateTime CreatedTime { get; set; }");
                    sb.AppendLine();
                }
                if (hasModifiedTime)
                {
                    sb.AppendLine("/// <summary>");
                    sb.AppendLine("/// 修改时间");
                    sb.AppendLine("/// </summary>");
                    sb.AppendLine("public virtual DateTime ModifiedTime { get; set; }");
                    sb.AppendLine();
                }
                if (hasCreatedBy)
                {
                    sb.AppendLine("/// <summary>");
                    sb.AppendLine("/// 创建用户");
                    sb.AppendLine("/// </summary>");
                    sb.AppendLine("[MaxLength(64)]");
                    sb.AppendLine("public virtual string? CreatedBy { get; set; }");
                    sb.AppendLine();
                }
                if (hasModifiedBy)
                {
                    sb.AppendLine("/// <summary>");
                    sb.AppendLine("/// 修改用户");
                    sb.AppendLine("/// </summary>");
                    sb.AppendLine("[MaxLength(64)]");
                    sb.AppendLine("public virtual string? ModifiedBy { get; set; }");
                    sb.AppendLine();
                }
                sb.AppendLine("#endregion");
                sb.AppendLine();
            }

            // 外键导航属性
            foreach (var fk in table.ForeignKeys)
            {
                var refClassName = ToPascalCase(fk.ReferencedTable);
                var fkCol = table.Columns.FirstOrDefault(c => c.ColumnName == fk.ColumnName);
                var isNullable = fkCol?.IsNullable ?? true;

                sb.AppendLine("/// <summary>");
                sb.AppendLine($"/// 关联 {refClassName}");
                sb.AppendLine("/// </summary>");
                if (!isNullable)
                    sb.AppendLine("[Required]");
                sb.AppendLine($"public virtual {refClassName}? {refClassName} {{ get; set; }}");
                sb.AppendLine();
            }

            // 普通业务属性
            var skipColumns = BuildSkipColumnSet(pkColumns, isAuditable, hasCreatedBy, hasModifiedBy, hasVersion, fkColumns);

            foreach (var col in table.Columns.Where(c => !skipColumns.Contains(c.ColumnName)))
            {
                var propName = ToPascalCase(col.ColumnName);
                var clrType = MapClrType(col);
                var isNullableType = col.IsNullable && clrType != "string";
                var typeStr = isNullableType ? $"{clrType}?" : clrType;
                var defaultVal = GetDefaultValue(col, clrType);

                sb.AppendLine("/// <summary>");
                sb.AppendLine($"/// {(!string.IsNullOrEmpty(col.Description) ? col.Description : propName)}");
                sb.AppendLine("/// </summary>");

                if (clrType == "string")
                {
                    if (!col.IsNullable && col.DefaultValue == null)
                        sb.AppendLine("[Required]");
                    if (col.MaxLength.HasValue && col.MaxLength.Value is > 0 and < int.MaxValue)
                        sb.AppendLine($"[MaxLength({col.MaxLength.Value})]");
                }

                sb.AppendLine($"public virtual {typeStr} {propName} {{ get; set; }}{defaultVal}");
                sb.AppendLine();
            }

            // IEntity 显式实现
            sb.AppendLine("#region IEntity 成员");
            sb.AppendLine();
            sb.AppendLine("/// <summary>");
            sb.AppendLine("/// 显式接口实现 - 返回 Id");
            sb.AppendLine("/// </summary>");
            sb.AppendLine("object IEntity.Id => Id;");
            sb.AppendLine();
            sb.AppendLine("#endregion");
            sb.AppendLine();

            // 构造函数
            sb.AppendLine($"/// <summary>");
            sb.AppendLine($"/// 初始化 {className} 类的新实例");
            sb.AppendLine($"/// </summary>");
            sb.AppendLine($"public {className}()");
            sb.AppendLine("{");
            using (sb.Indent())
            {
                if (isAuditable)
                {
                    sb.AppendLine("CreatedTime = DateTime.UtcNow;");
                    sb.AppendLine("ModifiedTime = DateTime.UtcNow;");
                }
            }
            sb.AppendLine("}");
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    #endregion

    #region Mapping 类生成

    private string GenerateMappingClass(TableSchema table, string className)
    {
        var sb = new IndentedStringBuilder();
        var fkColumns = table.ForeignKeys.Select(fk => fk.ColumnName).ToHashSet();

        bool hasCreatedTime = table.Columns.Any(c => c.ColumnName == "CreatedTime");
        bool hasModifiedTime = table.Columns.Any(c => c.ColumnName == "ModifiedTime");
        bool hasCreatedBy = table.Columns.Any(c => c.ColumnName == "CreatedBy");
        bool hasModifiedBy = table.Columns.Any(c => c.ColumnName == "ModifiedBy");
        bool isAuditable = hasCreatedTime && hasModifiedTime;
        bool hasVersion = table.Columns.Any(c => c.ColumnName == "Version");

        var pkColumns = table.Columns.Where(c => c.IsPrimaryKey).ToList();
        var isStringPk = pkColumns.Count == 1 && MapClrType(pkColumns[0]) == "string";

        sb.AppendLine("using Wms.Core.Domain.Entities;");
        sb.AppendLine("using Microsoft.EntityFrameworkCore;");
        sb.AppendLine("using Microsoft.EntityFrameworkCore.Metadata.Builders;");
        sb.AppendLine();
        sb.AppendLine($"namespace {_mappingNamespace};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// {className} 实体的 NHibernate 映射");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"internal class {className}Configuration : IEntityTypeConfiguration<{className}>");
        sb.AppendLine("{");
        sb.AppendLine($"    public void Configure(EntityTypeBuilder<{className}> builder)");
        sb.AppendLine("    {");

        using (sb.Indent())
        using (sb.Indent())
        {
            sb.AppendLine($"builder.ToTable(\"{table.TableName}\");");
            sb.AppendLine();

            // 主键
            if (pkColumns.Count == 1)
            {
                var pk = pkColumns[0];
                if (isStringPk)
                {
                    sb.AppendLine("builder.HasKey(x => x.Id);");
                    sb.AppendLine("builder.Property(x => x.Id).ValueGeneratedNever();");
                }
                else
                {
                    sb.AppendLine("builder.HasKey(x => x.Id);");
                    sb.AppendLine("builder.Property(x => x.Id).ValueGeneratedOnAdd();");
                }
            }
            sb.AppendLine();

            if (hasVersion)
            {
                sb.AppendLine("builder.Property(x => x.Version).IsRowVersion().IsConcurrencyToken();");
                sb.AppendLine();
            }

            if (isAuditable)
            {
                if (hasCreatedTime) sb.AppendLine("builder.Property(x => x.CreatedTime).HasDefaultValueSql(\"GETDATE()\").ValueGeneratedOnAdd();");
                if (hasModifiedTime) sb.AppendLine("builder.Property(x => x.ModifiedTime);");
                if (hasCreatedBy) sb.AppendLine("builder.Property(x => x.CreatedBy).HasMaxLength(64);");
                if (hasModifiedBy) sb.AppendLine("builder.Property(x => x.ModifiedBy).HasMaxLength(64);");
                sb.AppendLine();
            }

            // 外键导航属性
            foreach (var fk in table.ForeignKeys)
            {
                var refClassName = ToPascalCase(fk.ReferencedTable);
                var fkCol = table.Columns.FirstOrDefault(c => c.ColumnName == fk.ColumnName);
                var isNullable = fkCol?.IsNullable ?? true;

                var required = !isNullable ? ".IsRequired()" : "";
                sb.AppendLine($"builder.HasOne(x => x.{refClassName}).WithMany().HasForeignKey(\"{fk.ColumnName}\"){required};");
                sb.AppendLine();
            }

            // 普通属性
            var skipColumns = BuildSkipColumnSet(pkColumns, isAuditable, hasCreatedBy, hasModifiedBy, hasVersion, fkColumns);

            foreach (var col in table.Columns.Where(c => !skipColumns.Contains(c.ColumnName)))
            {
                var propName = ToPascalCase(col.ColumnName);

                if (col.DataType is "text" or "ntext" or "xml" or "varchar(max)" or "nvarchar(max)")
                {
                    sb.AppendLine($"builder.Property(x => x.{propName});");
                }
                else if (col.MaxLength.HasValue && col.MaxLength.Value is > 0 and < int.MaxValue && MapClrType(col) == "string")
                {
                    if (!col.IsNullable)
                        sb.AppendLine($"builder.Property(x => x.{propName}).HasMaxLength({col.MaxLength.Value}).IsRequired();");
                    else
                        sb.AppendLine($"builder.Property(x => x.{propName}).HasMaxLength({col.MaxLength.Value});");
                }
                else
                {
                    sb.AppendLine($"builder.Property(x => x.{propName});");
                }
            }
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    #endregion

    #region 辅助方法

    private static HashSet<string> BuildSkipColumnSet(
        List<ColumnInfo> pkColumns, bool isAuditable, bool hasCreatedBy, bool hasModifiedBy,
        bool hasVersion, HashSet<string> fkColumns)
    {
        var skip = new HashSet<string>();
        skip.UnionWith(pkColumns.Select(c => c.ColumnName));
        if (isAuditable)
        {
            skip.Add("CreatedTime");
            skip.Add("ModifiedTime");
        }
        if (hasCreatedBy) skip.Add("CreatedBy");
        if (hasModifiedBy) skip.Add("ModifiedBy");
        if (hasVersion) skip.Add("Version");
        skip.UnionWith(fkColumns);
        return skip;
    }

    /// <summary>
    /// 将数据库列名/表名转换为 PascalCase（如 material_code → MaterialCode）
    /// </summary>
    public static string ToPascalCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;

        // 不含分隔符且首字母已大写，直接返回
        if (!name.Contains('_') && !name.Contains('-') && char.IsUpper(name[0]))
            return name;

        var parts = name.Split(new[] { '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
        var result = new StringBuilder();
        foreach (var part in parts)
        {
            if (part.Length == 0) continue;
            result.Append(char.ToUpperInvariant(part[0]));
            result.Append(part[1..].ToLowerInvariant());
        }
        return result.ToString();
    }

    /// <summary>
    /// 将 SQL Server 数据类型映射到 CLR 类型
    /// </summary>
    private static string MapClrType(ColumnInfo column)
    {
        return column.DataType.ToLowerInvariant() switch
        {
            "bigint" => "long",
            "int" or "integer" => "int",
            "smallint" => "short",
            "tinyint" => "byte",
            "bit" => "bool",
            "decimal" or "money" or "smallmoney" or "numeric" => "decimal",
            "float" or "double precision" => "double",
            "real" => "float",
            "date" or "datetime" or "datetime2" or "smalldatetime" => "DateTime",
            "datetimeoffset" => "DateTimeOffset",
            "time" => "TimeSpan",
            "char" or "nchar" or "varchar" or "nvarchar" or "text" or "ntext" or "xml"
                or "varchar(max)" or "nvarchar(max)" => "string",
            "uniqueidentifier" => "Guid",
            "varbinary" or "binary" or "image" or "varbinary(max)" or "timestamp" or "rowversion" => "byte[]",
            _ => "string"
        };
    }

    private static string GetDefaultValue(ColumnInfo col, string clrType)
    {
        if (col.IsNullable)
            return clrType == "string" ? "" : "";

        return clrType switch
        {
            "string" => " = string.Empty;",
            "bool" => col.DefaultValue != null && col.DefaultValue.Contains("1") ? " = true;" : " = false;",
            "int" or "long" or "short" or "byte" => col.DefaultValue != null
                ? $" = {ParseNumericDefault(col.DefaultValue)};"
                : "",
            "decimal" or "double" => col.DefaultValue != null
                ? $" = {ParseNumericDefault(col.DefaultValue)}m;"
                : "",
            _ => ""
        };
    }

    private static string ParseNumericDefault(string defaultValue)
    {
        var val = defaultValue.Trim('(', ')', ' ');
        if (val.StartsWith("((")) val = val.Trim('(', ')');
        return val;
    }

    #endregion
}

#region 数据模型

internal class TableSchema
{
    public string Schema { get; set; } = "";
    public string TableName { get; set; } = "";
    public List<ColumnInfo> Columns { get; set; } = new();
    public List<ForeignKeyInfo> ForeignKeys { get; set; } = new();
}

internal class ColumnInfo
{
    public string ColumnName { get; set; } = "";
    public string DataType { get; set; } = "";
    public bool IsNullable { get; set; }
    public int? MaxLength { get; set; }
    public byte? NumericPrecision { get; set; }
    public int? NumericScale { get; set; }
    public string? DefaultValue { get; set; }
    public bool IsIdentity { get; set; }
    public bool IsPrimaryKey { get; set; }
    public string Description { get; set; } = "";
}

internal class ForeignKeyInfo
{
    public string ForeignKeyName { get; set; } = "";
    public string ColumnName { get; set; } = "";
    public string ReferencedTable { get; set; } = "";
    public string ReferencedColumn { get; set; } = "";
}

/// <summary>
/// 生成结果
/// </summary>
public class GenerationResult
{
    public List<string> GeneratedEntities { get; set; } = new();
    public List<string> GeneratedMappings { get; set; } = new();
    public List<string> Skipped { get; set; } = new();

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine("生成完成：");
        sb.AppendLine($"  实体类：{GeneratedEntities.Count} 个");
        sb.AppendLine($"  映射类：{GeneratedMappings.Count} 个");
        if (Skipped.Count > 0)
        {
            sb.AppendLine($"  跳过：{Skipped.Count} 个");
            foreach (var s in Skipped)
                sb.AppendLine($"    - {s}");
        }
        return sb.ToString();
    }
}

#endregion

#region IndentedStringBuilder

internal class IndentedStringBuilder
{
    private readonly StringBuilder _sb = new();
    private int _indentLevel;
    private const string IndentStr = "    ";

    public IndentedStringBuilder AppendLine(string? line = null)
    {
        if (string.IsNullOrEmpty(line))
            _sb.AppendLine();
        else
        {
            for (int i = 0; i < _indentLevel; i++)
                _sb.Append(IndentStr);
            _sb.AppendLine(line);
        }
        return this;
    }

    public IDisposable Indent()
    {
        _indentLevel++;
        return new IndentScope(this);
    }

    private void Decrement() => _indentLevel--;

    public override string ToString() => _sb.ToString();

    private class IndentScope(IndentedStringBuilder builder) : IDisposable
    {
        public void Dispose() => builder.Decrement();
    }
}

#endregion
