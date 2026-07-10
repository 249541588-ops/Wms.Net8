/* =====================================================================
   R502 RefreshToken 重用检测 - RefreshTokens 表新增 FamilyId 列与索引
   ---------------------------------------------------------------------
   关联变更:
     - RefreshToken.cs 新增 FamilyId (string?, MaxLength=64)
     - RefreshTokenConfiguration.cs 添加列映射与索引
       HasIndex(FamilyId) -> IX_RefreshTokens_FamilyId
     - IRefreshTokenRepository 新增 RevokeFamily(string familyId, string reason)
     - RefreshTokenRepository 实现 RevokeFamily（整族撤销 + Warning 审计日志）
     - AuthController.Login 每次登录生成新 FamilyId（家族根）
     - AuthController.Refresh 检测 IsUsed/IsRevoked 的 token 被再次提交
       -> 立即吊销整个 FamilyId 家族 + 输出 [安全事件][R502] 日志

   适用数据库: Microsoft SQL Server (含 Azure SQL Database / Managed Instance)
   执行方式:  SSMS / sqlcmd / Invoke-Sqlcmd 均可

   特性:
     - 幂等: 可重复执行, 已存在的对象会 SKIP
     - 事务包装: 任一步骤失败整体回滚 (XACT_ABORT ON)
     - 类型对齐: 与 EF Core 现有迁移生成的列类型一致
       (nvarchar(64) -> SQL Server nvarchar(64))
     - 索引非唯一: 同一家族可能存在多个 token (登录根 + 刷新链路), 不加 UNIQUE 约束
     - 数据兼容: 现有 token 的 FamilyId 为 NULL, 由 AuthController.Refresh
       在下次刷新时自然回填 (生成新 FamilyId 开启新链路)

   生成日期: 2026-07-07
   ===================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

/* ---------------------------------------------------------------------
   1. 新增 FamilyId 列 (nvarchar(64), NULL)
      -----------------------------------------------------------------
      设计要点:
      - NULL 兼容: 现有 RefreshToken 数据无此字段, 升级后保持 NULL,
        不会破坏既有会话; 下次刷新时由代码自然回填新 FamilyId。
      - nvarchar(64): 存储 Guid.NewGuid().ToString("N") 即 32 位十六进制,
        预留余量至 64 以兼容未来可能的 ID 格式变更。
      - 无 UNIQUE 约束: 同一 FamilyId 下存在多个 token (家族根 + 刷新链路),
        UNIQUE 会破坏整族撤销语义。
   --------------------------------------------------------------------- */
IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.RefreshTokens')
      AND name = N'FamilyId'
)
BEGIN
    ALTER TABLE dbo.RefreshTokens
        ADD FamilyId nvarchar(64) NULL;

    PRINT N'[OK]   已添加列 dbo.RefreshTokens.FamilyId (nvarchar(64), NULL)';
END
ELSE
BEGIN
    PRINT N'[SKIP] 列 dbo.RefreshTokens.FamilyId 已存在';
END


/* ---------------------------------------------------------------------
   2. 创建索引 IX_RefreshTokens_FamilyId
      -----------------------------------------------------------------
      用途:
      - RevokeFamily(string familyId) 查询: WHERE FamilyId = @p0 AND !IsRevoked AND ExpiryTime > @now
      - 重用检测场景下, 攻击者偷到 token 后短时间内即可触发整族撤销,
        该索引保证大批量 token 场景下撤销查询的响应速度。
      - 非唯一: 见上文说明。
   --------------------------------------------------------------------- */
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.RefreshTokens')
      AND name = N'IX_RefreshTokens_FamilyId'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_RefreshTokens_FamilyId
        ON dbo.RefreshTokens(FamilyId);

    PRINT N'[OK]   已创建索引 IX_RefreshTokens_FamilyId';
END
ELSE
BEGIN
    PRINT N'[SKIP] 索引 IX_RefreshTokens_FamilyId 已存在';
END


/* ---------------------------------------------------------------------
   3. 数据回填 (无需操作)
      -----------------------------------------------------------------
      现有 RefreshToken 数据的 FamilyId 默认 NULL, 业务语义为
      "迁移期旧 token, 等待下次刷新时由代码分配新 FamilyId"。
      不做主动回填, 避免误将不相关的 token 归入同一家族。
   --------------------------------------------------------------------- */


COMMIT TRANSACTION;

PRINT N'';
PRINT N'==== R502 RefreshToken 重用检测迁移完成 (AddRefreshTokenFamily) ====';


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
WHERE   c.object_id = OBJECT_ID(N'dbo.RefreshTokens')
  AND   c.name = N'FamilyId';

SELECT  name, type_desc, is_unique, is_disabled
FROM    sys.indexes
WHERE   object_id = OBJECT_ID(N'dbo.RefreshTokens')
  AND   name = N'IX_RefreshTokens_FamilyId';

-- 抽样确认现有 token 的 FamilyId 为 NULL (迁移期数据)
SELECT TOP (10) Id, UserId, UserName, FamilyId, IsUsed, IsRevoked, ExpiryTime
FROM   dbo.RefreshTokens
ORDER BY Id;

-- 审计: 后续运行一段时间后, 统计已回填 FamilyId 的 token 数量
-- SELECT COUNT(*) FROM dbo.RefreshTokens WHERE FamilyId IS NOT NULL;
-- SELECT COUNT(*) FROM dbo.RefreshTokens WHERE FamilyId IS NULL;
*/


/* =====================================================================
   回滚脚本 (如需还原, 单独执行以下语句, 顺序倒序)
   ===================================================================== */
/*
DROP INDEX IF EXISTS IX_RefreshTokens_FamilyId ON dbo.RefreshTokens;
ALTER TABLE dbo.RefreshTokens DROP COLUMN IF EXISTS FamilyId;
*/
