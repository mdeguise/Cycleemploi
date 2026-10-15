BEGIN TRANSACTION;
CREATE TABLE [IssueReports] (
    [IssueId] int NOT NULL IDENTITY,
    [CreatedUtc] datetime2 NOT NULL,
    [CreatedBySam] nvarchar(128) NOT NULL,
    [CreatedByName] nvarchar(256) NULL,
    [Type] varchar(20) NOT NULL,
    [Title] nvarchar(200) NOT NULL,
    [Description] nvarchar(max) NOT NULL,
    [Expected] nvarchar(max) NULL,
    [Screen] nvarchar(400) NULL,
    [Context] nvarchar(max) NULL,
    [Status] varchar(20) NOT NULL,
    [Resolution] nvarchar(max) NULL,
    [CommitRef] varchar(40) NULL,
    [WorkLog] nvarchar(max) NULL,
    [HandledUtc] datetime2 NULL,
    [HandledBySam] nvarchar(128) NULL,
    CONSTRAINT [PK_IssueReports] PRIMARY KEY ([IssueId])
);

CREATE TABLE [IssueReportImages] (
    [ImageId] int NOT NULL IDENTITY,
    [IssueId] int NOT NULL,
    [Ordinal] tinyint NOT NULL,
    [MimeType] varchar(100) NOT NULL,
    [FileName] nvarchar(260) NULL,
    [Size] int NOT NULL,
    [Content] varbinary(max) NOT NULL,
    CONSTRAINT [PK_IssueReportImages] PRIMARY KEY ([ImageId]),
    CONSTRAINT [FK_IssueReportImages_IssueReports_IssueId] FOREIGN KEY ([IssueId]) REFERENCES [IssueReports] ([IssueId]) ON DELETE CASCADE
);

CREATE INDEX [IX_IssueReportImages_IssueId_Ordinal] ON [IssueReportImages] ([IssueId], [Ordinal]);

CREATE INDEX [IX_IssueReports_CreatedBySam_CreatedUtc] ON [IssueReports] ([CreatedBySam], [CreatedUtc]);

CREATE INDEX [IX_IssueReports_Status_CreatedUtc] ON [IssueReports] ([Status], [CreatedUtc]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261015125654_AddIssueReports', N'10.0.10');

COMMIT;
GO

