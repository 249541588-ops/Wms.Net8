BEGIN TRANSACTION;
GO

ALTER TABLE [TransTasks] ADD [UnitloadCode] nvarchar(100) NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260617070845_AddTransTaskUnitloadCode', N'8.0.11');
GO

COMMIT;
GO

