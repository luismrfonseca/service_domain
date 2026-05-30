IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [Clientes] (
    [Id] uniqueidentifier NOT NULL,
    [No] int NOT NULL,
    [Nome] nvarchar(200) NOT NULL,
    [NomeFiscal] nvarchar(20) NOT NULL,
    [Email] nvarchar(100) NULL,
    [PhcStamp] nvarchar(25) NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Clientes] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [Encomendas] (
    [Id] uniqueidentifier NOT NULL,
    [DocumentoNo] int NOT NULL,
    [ClienteNo] int NOT NULL,
    [Data] datetime2 NOT NULL,
    [Total] decimal(18,4) NOT NULL,
    [PhcStamp] nvarchar(25) NULL,
    [Status] nvarchar(20) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Encomendas] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [Localizacoes] (
    [Id] uniqueidentifier NOT NULL,
    [Armazem] int NOT NULL,
    [Nome] nvarchar(100) NOT NULL,
    [PhcStamp] nvarchar(25) NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Localizacoes] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [Lotes] (
    [Id] uniqueidentifier NOT NULL,
    [LoteCodigo] nvarchar(100) NOT NULL,
    [Ref] nvarchar(50) NOT NULL,
    [PhcStamp] nvarchar(25) NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Lotes] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [Produtos] (
    [Id] uniqueidentifier NOT NULL,
    [Ref] nvarchar(50) NOT NULL,
    [Designacao] nvarchar(200) NOT NULL,
    [PhcStamp] nvarchar(25) NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Produtos] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [Stocks] (
    [Id] uniqueidentifier NOT NULL,
    [Ref] nvarchar(50) NOT NULL,
    [LoteCodigo] nvarchar(100) NULL,
    [Armazem] int NOT NULL,
    [Localizacao] nvarchar(50) NOT NULL,
    [Quantidade] decimal(18,4) NOT NULL,
    [PhcStamp] nvarchar(25) NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Stocks] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [SyncInbox] (
    [Id] uniqueidentifier NOT NULL,
    [EntityType] nvarchar(50) NOT NULL,
    [PhcStamp] nvarchar(25) NOT NULL,
    [Payload] nvarchar(max) NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [ErrorMessage] nvarchar(max) NULL,
    [RetryCount] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [ProcessedAt] datetime2 NULL,
    CONSTRAINT [PK_SyncInbox] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [SyncOutbox] (
    [Id] uniqueidentifier NOT NULL,
    [EntityType] nvarchar(50) NOT NULL,
    [EntityId] uniqueidentifier NOT NULL,
    [Payload] nvarchar(max) NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [ErrorMessage] nvarchar(max) NULL,
    [RetryCount] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [ProcessedAt] datetime2 NULL,
    CONSTRAINT [PK_SyncOutbox] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [EncomendaLinhas] (
    [Id] uniqueidentifier NOT NULL,
    [EncomendaId] uniqueidentifier NOT NULL,
    [Ref] nvarchar(50) NOT NULL,
    [Designacao] nvarchar(200) NOT NULL,
    [Quantidade] decimal(18,4) NOT NULL,
    [Preco] decimal(18,4) NOT NULL,
    [Lote] nvarchar(100) NULL,
    [PhcStamp] nvarchar(25) NULL,
    CONSTRAINT [PK_EncomendaLinhas] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EncomendaLinhas_Encomendas_EncomendaId] FOREIGN KEY ([EncomendaId]) REFERENCES [Encomendas] ([Id]) ON DELETE CASCADE
);
GO

CREATE UNIQUE INDEX [IX_Clientes_No] ON [Clientes] ([No]);
GO

CREATE UNIQUE INDEX [IX_Clientes_PhcStamp] ON [Clientes] ([PhcStamp]);
GO

CREATE INDEX [IX_EncomendaLinhas_EncomendaId] ON [EncomendaLinhas] ([EncomendaId]);
GO

CREATE INDEX [IX_EncomendaLinhas_PhcStamp] ON [EncomendaLinhas] ([PhcStamp]);
GO

CREATE UNIQUE INDEX [IX_Encomendas_DocumentoNo] ON [Encomendas] ([DocumentoNo]);
GO

CREATE INDEX [IX_Encomendas_PhcStamp] ON [Encomendas] ([PhcStamp]);
GO

CREATE INDEX [IX_Encomendas_Status] ON [Encomendas] ([Status]);
GO

CREATE UNIQUE INDEX [IX_Localizacoes_Armazem] ON [Localizacoes] ([Armazem]);
GO

CREATE UNIQUE INDEX [IX_Localizacoes_PhcStamp] ON [Localizacoes] ([PhcStamp]);
GO

CREATE UNIQUE INDEX [IX_Lotes_PhcStamp] ON [Lotes] ([PhcStamp]);
GO

CREATE UNIQUE INDEX [IX_Lotes_Ref_LoteCodigo] ON [Lotes] ([Ref], [LoteCodigo]);
GO

CREATE UNIQUE INDEX [IX_Produtos_PhcStamp] ON [Produtos] ([PhcStamp]);
GO

CREATE UNIQUE INDEX [IX_Produtos_Ref] ON [Produtos] ([Ref]);
GO

CREATE INDEX [IX_Stocks_PhcStamp] ON [Stocks] ([PhcStamp]);
GO

CREATE UNIQUE INDEX [IX_Stocks_Ref_LoteCodigo_Armazem_Localizacao] ON [Stocks] ([Ref], [LoteCodigo], [Armazem], [Localizacao]) WHERE [LoteCodigo] IS NOT NULL;
GO

CREATE INDEX [IX_SyncInbox_Status_CreatedAt] ON [SyncInbox] ([Status], [CreatedAt]);
GO

CREATE INDEX [IX_SyncOutbox_Status_CreatedAt] ON [SyncOutbox] ([Status], [CreatedAt]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260527130319_InitialCreate', N'8.0.27');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

DROP INDEX [IX_Encomendas_DocumentoNo] ON [Encomendas];
GO

ALTER TABLE [Encomendas] ADD [ParentId] uniqueidentifier NULL;
GO

ALTER TABLE [Encomendas] ADD [Tipo] int NOT NULL DEFAULT 0;
GO

ALTER TABLE [EncomendaLinhas] ADD [Localizacao] nvarchar(50) NULL;
GO

ALTER TABLE [EncomendaLinhas] ADD [ParentLineId] uniqueidentifier NULL;
GO

CREATE UNIQUE INDEX [IX_Encomendas_DocumentoNo_Tipo] ON [Encomendas] ([DocumentoNo], [Tipo]);
GO

CREATE INDEX [IX_Encomendas_ParentId] ON [Encomendas] ([ParentId]);
GO

CREATE INDEX [IX_EncomendaLinhas_ParentLineId] ON [EncomendaLinhas] ([ParentLineId]);
GO

ALTER TABLE [EncomendaLinhas] ADD CONSTRAINT [FK_EncomendaLinhas_EncomendaLinhas_ParentLineId] FOREIGN KEY ([ParentLineId]) REFERENCES [EncomendaLinhas] ([Id]) ON DELETE NO ACTION;
GO

ALTER TABLE [Encomendas] ADD CONSTRAINT [FK_Encomendas_Encomendas_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [Encomendas] ([Id]) ON DELETE NO ACTION;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260527132012_AddLogisticsSupport', N'8.0.27');
GO

COMMIT;
GO

