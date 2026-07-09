/* =====================================================================
   T306 软删除 - Users 表新增 DeletedAt / DeletedBy 列与索引
   ---------------------------------------------------------------------
   关联变更:
     - User.cs 新增 DeletedAt (DateTime?) / DeletedBy (string?, MaxLength=64)
     - UserConfiguration.cs 添加列映射与索引 HasIndex(IsActive) / HasIndex(DeletedAt)
     - UsersController.Delete 改为软删除 (IsActive=false + DeletedAt=UtcNow)
     - GetAll/GetById/Update/ChangeStatus 对 DeletedAt != null 返回 404

   适用数据库: Microsoft SQL Server (含 Azure SQL Database / Managed Instance)
   执行方式:  SSMS / sqlcmd / Invoke-Sqlcmd 均可

   特性:
     - 幂等: 可重复执行, 已存在的对象会 SKIP
     - 事务包装: 任一步骤失败整体回滚 (XACT_ABORT ON)
     - 类型对齐: 与 EF Core 现有迁移生成的列类型一致
       (datetime2 / nvarchar(64) / bit)

   生成日期: 2026-07-07
   ===================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

/* ---------------------------------------------------------------------
   1. 新增 DeletedAt 列 (datetime2, NULL)
   --------------------------------------------------------------------- */
IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Users')
      AND name = N'DeletedAt'
)
BEGIN
    ALTER TABLE dbo.Users
        ADD DeletedAt datetime2 NULL;

    PRINT N'[OK]   已添加列 dbo.Users.DeletedAt (datetime2, NULL)';
END
ELSE
BEGIN
    PRINT N'[SKIP] 列 dbo.Users.DeletedAt 已存在';
END


/* ---------------------------------------------------------------------
   2. 新增 DeletedBy 列 (nvarchar(64), NULL)
   --------------------------------------------------------------------- */
IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Users')
      AND name = N'DeletedBy'
)
BEGIN
    ALTER TABLE dbo.Users
        ADD DeletedBy nvarchar(64) NULL;

    PRINT N'[OK]   已添加列 dbo.Users.DeletedBy (nvarchar(64), NULL)';
END
ELSE
BEGIN
    PRINT N'[SKIP] 列 dbo.Users.DeletedBy 已存在';
END


/* ---------------------------------------------------------------------
   3. 创建索引 IX_Users_IsActive
      用于加速 GetAll 时按 status 过滤
   --------------------------------------------------------------------- */
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Users')
      AND name = N'IX_Users_IsActive'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_Users_IsActive
        ON dbo.Users(IsActive);

    PRINT N'[OK]   已创建索引 IX_Users_IsActive';
END
ELSE
BEGIN
    PRINT N'[SKIP] 索引 IX_Users_IsActive 已存在';
END


/* ---------------------------------------------------------------------
   4. 创建索引 IX_Users_DeletedAt
      用于加速 GetAll 默认过滤 (WHERE DeletedAt IS NULL)
   --------------------------------------------------------------------- */
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Users')
      AND name = N'IX_Users_DeletedAt'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_Users_DeletedAt
        ON dbo.Users(DeletedAt);

    PRINT N'[OK]   已创建索引 IX_Users_DeletedAt';
END
ELSE
BEGIN
    PRINT N'[SKIP] 索引 IX_Users_DeletedAt 已存在';
END


/* ---------------------------------------------------------------------
   5. 数据回填 (无需操作)
      现有用户 DeletedAt 默认 NULL, 即"未删除", 符合业务语义。
      IsActive 已存在的旧数据保持原值不变。
   --------------------------------------------------------------------- */

COMMIT TRANSACTION;

PRINT N'';
PRINT N'==== T306 软删除迁移完成 (AddUserSoftDelete) ====';


/* =====================================================================
   验证查询 (可选, 执行迁移后运行以确认)
   ===================================================================== */
/*
SELECT  c.name AS column_name,
        t.name AS data_type,
        c.max_length,
        c.is_nullable
FROM    sys.columns c
JOIN    sys.types t ON c.user_type_id = t.user_type_id
WHERE   c.object_id = OBJECT_ID(N'dbo.Users')
  AND   c.name IN (N'DeletedAt', N'DeletedBy')
ORDER BY c.name;

SELECT  name, type_desc, is_unique, is_disabled
FROM    sys.indexes
WHERE   object_id = OBJECT_ID(N'dbo.Users')
  AND   name IN (N'IX_Users_IsActive', N'IX_Users_DeletedAt')
ORDER BY name;

-- 抽样确认现有用户未被误标记
SELECT TOP (10) Id, UserName, IsActive, DeletedAt, DeletedBy
FROM   dbo.Users
ORDER BY Id;
*/


/* =====================================================================
   回滚脚本 (如需还原, 单独执行以下语句, 顺序倒序)
   ===================================================================== */
/*
DROP INDEX IF EXISTS IX_Users_DeletedAt ON dbo.Users;
DROP INDEX IF EXISTS IX_Users_IsActive  ON dbo.Users;
ALTER TABLE dbo.Users DROP COLUMN IF EXISTS DeletedBy;
ALTER TABLE dbo.Users DROP COLUMN IF EXISTS DeletedAt;
*/
