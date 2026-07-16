/* =====================================================================
   托盘类型验证 - 工序结果码映射 字典种子数据
   ---------------------------------------------------------------------
   关联变更:
     - VerfiyPalletTypeRequestHandler 改为从字典读取 resultcode 映射
       (替换原 switch/case 硬编码: 一注装盘→2, 清洗装盘→3, 化成→4)
     - BasicDictionaryController 的 Create/Update/Delete/SetEnabled
       新增 ClearCacheFor 调用, 保证后台修改即时生效
     - Cst.cs 新增常量: 托盘类型验证映射 / 托盘类型验证_NOT_EXIST

   字典结构:
     父级 No = VERFIYPALLETTYPE_MAP
       ├─ No=NOT_EXIST        Name=托盘不存在   Value=1
       ├─ No=OP_一注装盘      Name=一注装盘     Value=2
       ├─ No=OP_清洗装盘      Name=清洗装盘     Value=3
       └─ No=OP_化成          Name=化成         Value=4

     Handler 读取逻辑 (见 VerfiyPalletTypeRequestHandler):
       - unitload == null                → 取 No=NOT_EXIST 的 Value (默认 1)
       - 否则用 unitload.CurrentOperation 匹配子项 Name (忽略大小写/空格) → 取 Value
       - 未匹配                          → 返回 WcsFail (ResultCodeTypes.数据异常)

   适用数据库: Microsoft SQL Server (含 Azure SQL Database / Managed Instance)
   执行方式:  SSMS / sqlcmd / Invoke-Sqlcmd 均可

   特性:
     - 幂等: 可重复执行, 已存在的记录会 SKIP
     - 事务包装: 任一步骤失败整体回滚 (XACT_ABORT ON)
     - 中文必须使用 N'' 前缀 (nvarchar)

   生成日期: 2026-07-15
   ===================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

/* ---------------------------------------------------------------------
   1. 父级字典分类: VERFIYPALLETTYPE_MAP
      ----------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM BasicDictionary WHERE No = N'VERFIYPALLETTYPE_MAP')
BEGIN
    INSERT INTO BasicDictionary
        (ParentId, No, Name, Value, Abbreviation, FullPinyin, Remarks,
         Sort, Status, IsNext, ExpandField1, ExpandField2,
         CreatedTime, ModifiedTime, CreatedBy, ModifiedBy)
    VALUES
        (0, N'VERFIYPALLETTYPE_MAP', N'托盘类型验证-工序结果码映射', N'', NULL, NULL,
         N'托盘类型验证工位返回的 resultcode 映射；子项 Name=工序名，Value=resultcode；新增工序只需加子项',
         1, 1, 1, NULL, NULL,
         GETDATE(), GETDATE(), N'system', NULL);
END

/* ---------------------------------------------------------------------
   2. 子项映射 (ParentId 关联父级)
      ----------------------------------------------------------------- */
DECLARE @parentId INT;
SELECT @parentId = Id FROM BasicDictionary WHERE No = N'VERFIYPALLETTYPE_MAP';

IF @parentId IS NULL
BEGIN
    RAISERROR (N'父级字典 VERFIYPALLETTYPE_MAP 不存在或插入失败，终止种子脚本', 16, 1);
    RETURN;
END

-- 2.1 托盘不存在 → 1
IF NOT EXISTS (SELECT 1 FROM BasicDictionary WHERE No = N'NOT_EXIST')
BEGIN
    INSERT INTO BasicDictionary
        (ParentId, No, Name, Value, Abbreviation, FullPinyin, Remarks,
         Sort, Status, IsNext, ExpandField1, ExpandField2,
         CreatedTime, ModifiedTime, CreatedBy, ModifiedBy)
    VALUES
        (@parentId, N'NOT_EXIST', N'托盘不存在', N'1', NULL, NULL,
         N'unitload 不存在时返回的 resultcode',
         1, 1, 0, NULL, NULL,
         GETDATE(), GETDATE(), N'system', NULL);
END

-- 2.2 一注装盘 → 2
IF NOT EXISTS (SELECT 1 FROM BasicDictionary WHERE No = N'OP_一注装盘')
BEGIN
    INSERT INTO BasicDictionary
        (ParentId, No, Name, Value, Abbreviation, FullPinyin, Remarks,
         Sort, Status, IsNext, ExpandField1, ExpandField2,
         CreatedTime, ModifiedTime, CreatedBy, ModifiedBy)
    VALUES
        (@parentId, N'OP_一注装盘', N'一注装盘', N'2', NULL, NULL,
         N'匹配 unitload.CurrentOperation = 一注装盘',
         2, 1, 0, NULL, NULL,
         GETDATE(), GETDATE(), N'system', NULL);
END

-- 2.3 清洗装盘 → 3
IF NOT EXISTS (SELECT 1 FROM BasicDictionary WHERE No = N'OP_清洗装盘')
BEGIN
    INSERT INTO BasicDictionary
        (ParentId, No, Name, Value, Abbreviation, FullPinyin, Remarks,
         Sort, Status, IsNext, ExpandField1, ExpandField2,
         CreatedTime, ModifiedTime, CreatedBy, ModifiedBy)
    VALUES
        (@parentId, N'OP_清洗装盘', N'清洗装盘', N'3', NULL, NULL,
         N'匹配 unitload.CurrentOperation = 清洗装盘',
         3, 1, 0, NULL, NULL,
         GETDATE(), GETDATE(), N'system', NULL);
END

-- 2.4 化成 → 4
IF NOT EXISTS (SELECT 1 FROM BasicDictionary WHERE No = N'OP_化成')
BEGIN
    INSERT INTO BasicDictionary
        (ParentId, No, Name, Value, Abbreviation, FullPinyin, Remarks,
         Sort, Status, IsNext, ExpandField1, ExpandField2,
         CreatedTime, ModifiedTime, CreatedBy, ModifiedBy)
    VALUES
        (@parentId, N'OP_化成', N'化成', N'4', NULL, NULL,
         N'匹配 unitload.CurrentOperation = 化成',
         4, 1, 0, NULL, NULL,
         GETDATE(), GETDATE(), N'system', NULL);
END

COMMIT TRANSACTION;

PRINT N'种子数据插入完成: VERFIYPALLETTYPE_MAP (1 父级 + 4 子项)';
