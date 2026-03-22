-- Run this script against your SQL Server / LocalDB instance.
-- The connection string in appsettings.json targets: Server=(localdb)\mssqllocaldb;Database=PWAMessenger

CREATE DATABASE PWAMessenger;
GO

USE PWAMessenger;
GO

CREATE TABLE Users (
    UserId      INT PRIMARY KEY IDENTITY,
    Username    NVARCHAR(50) NOT NULL UNIQUE
);
GO

CREATE TABLE FcmTokens (
    TokenId      INT PRIMARY KEY IDENTITY,
    UserId       INT NOT NULL REFERENCES Users(UserId),
    Token        NVARCHAR(500) NOT NULL,
    RegisteredAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    LastSeenAt   DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);
GO

INSERT INTO Users (Username) VALUES ('Phil'), ('Maggie');
GO
