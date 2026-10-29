BEGIN TRANSACTION;
ALTER TABLE [RequestEmployees] ADD [PositionLastFollowed] nvarchar(max) NULL;

ALTER TABLE [RequestEmployees] ADD [PositionWorkdayAtSubmit] nvarchar(max) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261029115517_AddPositionFollowUp', N'10.0.10');

COMMIT;
GO

