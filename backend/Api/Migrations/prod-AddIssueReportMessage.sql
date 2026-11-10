/*
  prod-AddIssueReportMessage.sql — Signalements : fil « Échange » + statut « À valider » (consigne de Marc, 2026-11-10).
  Base cible : vm-trm-sql1 / EmployeeLifecycle (prod). Pour la base de dev EmployeeLifecycleDev : même script.

  Équivaut à la migration EF 20261110164249_AddIssueReportMessage, mais IDEMPOTENT : peut être rejoué sans risque.
    1. dbo.IssueReportMessage (nom EXACT, au singulier : la copie vers Le Guichet — Pivot\tools\reprendre-signalements.ps1 —
       le lit par ce nom et ces colonnes : MessageId, IssueId, CreatedUtc, FromTeam, AuthorSam, AuthorName, Body).
    2. Statut A_VALIDER : la colonne IssueReports.Status (varchar(20)) n'a AUCUNE contrainte CHECK créée par EF ; si une
       contrainte a été ajoutée à la main et ne connaît pas A_VALIDER, elle est refaite avec les 6 statuts.
    3. La ligne __EFMigrationsHistory, pour que l'application ne cherche pas à rejouer la migration.

  Appliquer (fichier en UTF-8 AVEC BOM, sinon le français devient mojibake ; -I pour QUOTED_IDENTIFIER) :
    sqlcmd -S vm-trm-sql1 -d EmployeeLifecycle -E -I -b -i backend\Api\Migrations\prod-AddIssueReportMessage.sql
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.IssueReportMessage', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[IssueReportMessage] (
        [MessageId]  int IDENTITY(1,1) NOT NULL,
        [IssueId]    int            NOT NULL,
        [CreatedUtc] datetime2(3)   NOT NULL CONSTRAINT [DF_IssueReportMessage_CreatedUtc] DEFAULT (SYSUTCDATETIME()),
        [FromTeam]   bit            NOT NULL,          -- 1 = équipe (un administrateur), 0 = la personne qui a signalé
        [AuthorSam]  nvarchar(128)  NULL,
        [AuthorName] nvarchar(200)  NULL,
        [Body]       nvarchar(max)  NOT NULL,
        CONSTRAINT [PK_IssueReportMessage] PRIMARY KEY ([MessageId]),
        CONSTRAINT [FK_IssueReportMessage_IssueReports_IssueId] FOREIGN KEY ([IssueId])
            REFERENCES [dbo].[IssueReports] ([IssueId]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_IssueReportMessage_IssueId_CreatedUtc] ON [dbo].[IssueReportMessage] ([IssueId], [CreatedUtc]);
    PRINT 'Table dbo.IssueReportMessage créée.';
END
ELSE PRINT 'Table dbo.IssueReportMessage déjà présente.';

-- Statut A_VALIDER : toute contrainte CHECK sur IssueReports.Status qui ne le connaît pas est refaite.
DECLARE @ck sysname;
SELECT TOP (1) @ck = cc.name
FROM sys.check_constraints cc
WHERE cc.parent_object_id = OBJECT_ID(N'dbo.IssueReports')
  AND cc.definition LIKE N'%Status%' AND cc.definition NOT LIKE N'%A_VALIDER%';
IF @ck IS NOT NULL
BEGIN
    EXEC (N'ALTER TABLE [dbo].[IssueReports] DROP CONSTRAINT [' + @ck + N']');
    ALTER TABLE [dbo].[IssueReports] WITH CHECK ADD CONSTRAINT [CK_IssueReports_Status]
        CHECK ([Status] IN ('NEW', 'IN_PROGRESS', 'A_VALIDER', 'FIXED', 'REJECTED', 'DUPLICATE'));
    PRINT 'Contrainte de statut refaite (A_VALIDER accepté).';
END
ELSE PRINT 'Aucune contrainte de statut à élargir (A_VALIDER déjà accepté).';

IF NOT EXISTS (SELECT 1 FROM [dbo].[__EFMigrationsHistory] WHERE [MigrationId] = N'20261110164249_AddIssueReportMessage')
BEGIN
    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261110164249_AddIssueReportMessage', N'10.0.10');
    PRINT 'Migration 20261110164249_AddIssueReportMessage inscrite.';
END

COMMIT;
GO

-- Vérification
SELECT name, create_date FROM sys.tables WHERE name = N'IssueReportMessage';
SELECT MigrationId FROM [dbo].[__EFMigrationsHistory] WHERE MigrationId = N'20261110164249_AddIssueReportMessage';
GO
