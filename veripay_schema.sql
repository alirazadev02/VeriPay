-- VeriPay SQL Server Schema
-- Run this in SSMS or Azure Data Studio:
--   sqlcmd -S localhost -E -i veripay_schema.sql
-- ─────────────────────────────────────────────────────────────────────────────

-- Create the database if it does not exist
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'VeriPay')
BEGIN
    CREATE DATABASE [VeriPay];
END
GO

USE [VeriPay];
GO

-- ─── Banks reference ──────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'banks')
BEGIN
    CREATE TABLE [banks] (
        [id]         INT           IDENTITY(1,1) PRIMARY KEY,
        [bank_code]  NVARCHAR(50)  NOT NULL,
        [bank_name]  NVARCHAR(100) NOT NULL,
        [is_active]  BIT           NOT NULL DEFAULT 1,
        [created_at] DATETIME2     NOT NULL DEFAULT GETUTCDATE(),

        CONSTRAINT uq_banks_code UNIQUE ([bank_code])
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM [banks])
BEGIN
    INSERT INTO [banks] ([bank_code], [bank_name]) VALUES
    ('HBL',         'Habib Bank Limited'),
    ('UBL',         'United Bank Limited'),
    ('MCB',         'MCB Bank Limited'),
    ('Meezan',      'Meezan Bank Limited'),
    ('ABL',         'Allied Bank Limited'),
    ('NBP',         'National Bank of Pakistan'),
    ('BankAlfalah', 'Bank Alfalah Limited'),
    ('FaysalBank',  'Faysal Bank Limited');
END
GO

-- ─── Migration: add new banks ─────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM [banks] WHERE [bank_code] = 'Jazzcash')
    INSERT INTO [banks] ([bank_code], [bank_name]) VALUES ('Jazzcash',   'JazzCash');
IF NOT EXISTS (SELECT 1 FROM [banks] WHERE [bank_code] = 'Easypaisa')
    INSERT INTO [banks] ([bank_code], [bank_name]) VALUES ('Easypaisa',  'Easypaisa');
IF NOT EXISTS (SELECT 1 FROM [banks] WHERE [bank_code] = 'BOP')
    INSERT INTO [banks] ([bank_code], [bank_name]) VALUES ('BOP',        'Bank of Punjab');
IF NOT EXISTS (SELECT 1 FROM [banks] WHERE [bank_code] = 'JSBL')
    INSERT INTO [banks] ([bank_code], [bank_name]) VALUES ('JSBL',       'JS Bank Limited');
IF NOT EXISTS (SELECT 1 FROM [banks] WHERE [bank_code] = 'SoneriBank')
    INSERT INTO [banks] ([bank_code], [bank_name]) VALUES ('SoneriBank', 'Soneri Bank');
IF NOT EXISTS (SELECT 1 FROM [banks] WHERE [bank_code] = 'SCPakistan')
    INSERT INTO [banks] ([bank_code], [bank_name]) VALUES ('SCPakistan', 'Standard Chartered Pakistan');
GO

-- ─── Transfers ────────────────────────────────────────────────────────────────
-- Status codes:
--   1 = Initiated        2 = SentToSwitch    3 = AtBeneficiary
--   4 = Credited         5 = Failed          6 = ReversalPending   7 = Reversed
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'transfers')
BEGIN
    CREATE TABLE [transfers] (
        [id]                   BIGINT        IDENTITY(1,1) PRIMARY KEY,
        [transfer_id]          NVARCHAR(50)  NOT NULL,
        [rail]                 NVARCHAR(10)  NOT NULL,
        [ref_id]               NVARCHAR(100) NOT NULL,
        [client_id]            NVARCHAR(50)  NOT NULL,
        [from_bank]            NVARCHAR(50)  NOT NULL,
        [to_bank]              NVARCHAR(50)  NOT NULL,
        [amount]               DECIMAL(15,2) NOT NULL,
        [currency]             NVARCHAR(10)  NOT NULL DEFAULT 'PKR',
        [current_status]       TINYINT       NOT NULL DEFAULT 1,
        [type]                 NVARCHAR(10)  NOT NULL DEFAULT 'TRANSFER',
        [original_transfer_id] NVARCHAR(50)  NULL,
        [reason]               NVARCHAR(500) NULL,
        [created_at]           DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
        [updated_at]           DATETIME2     NOT NULL DEFAULT GETUTCDATE(),

        CONSTRAINT uq_transfers_id    UNIQUE ([transfer_id]),
        CONSTRAINT chk_rail           CHECK ([rail]  IN ('1LINK', 'RAAST')),
        CONSTRAINT chk_type           CHECK ([type]  IN ('TRANSFER', 'REVERSAL')),
        CONSTRAINT chk_status         CHECK ([current_status] BETWEEN 1 AND 7),
        CONSTRAINT fk_original_transfer
            FOREIGN KEY ([original_transfer_id])
            REFERENCES [transfers]([transfer_id])
            ON DELETE NO ACTION
    );

    CREATE INDEX idx_transfers_client_id  ON [transfers]([client_id]);
    CREATE INDEX idx_transfers_ref_id     ON [transfers]([ref_id]);
    CREATE INDEX idx_transfers_status     ON [transfers]([current_status]);
    CREATE INDEX idx_transfers_type       ON [transfers]([type]);
    CREATE INDEX idx_transfers_created_at ON [transfers]([created_at]);
END
GO

-- ─── Migration: add reason column to existing databases ─────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'transfers') AND name = 'reason'
)
BEGIN
    ALTER TABLE [transfers] ADD [reason] NVARCHAR(500) NULL;
END
GO

-- ─── Transfer Events (timeline) ───────────────────────────────────────────────
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'transfer_events')
BEGIN
    CREATE TABLE [transfer_events] (
        [id]              BIGINT         IDENTITY(1,1) PRIMARY KEY,
        [transfer_id]     NVARCHAR(50)   NOT NULL,
        [status]          NVARCHAR(100)  NOT NULL,
        [source]          NVARCHAR(100)  NOT NULL,
        [occurred_at_utc] DATETIME2(3)   NOT NULL,
        [details]         NVARCHAR(MAX)  NULL,   -- stores JSON text
        [created_at]      DATETIME2      NOT NULL DEFAULT GETUTCDATE(),

        CONSTRAINT fk_event_transfer
            FOREIGN KEY ([transfer_id])
            REFERENCES [transfers]([transfer_id])
            ON DELETE CASCADE
    );

    CREATE INDEX idx_events_transfer_id ON [transfer_events]([transfer_id]);
    CREATE INDEX idx_events_occurred_at ON [transfer_events]([occurred_at_utc]);
END
GO
