-- Run AFTER the original seed script. Creates the API login table.
USE GseOperationsDb;
GO

IF OBJECT_ID(N'dbo.AppUsers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AppUsers
    (
        Id INT IDENTITY(1,1) NOT NULL,
        Username VARCHAR(50) NOT NULL,
        PasswordHash VARCHAR(500) NOT NULL,
        Role VARCHAR(20) NOT NULL,
        FailedLoginCount INT NOT NULL CONSTRAINT DF_AppUsers_Failed DEFAULT 0,
        LockoutEndUtc DATETIME2 NULL,
        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_AppUsers_CreatedAtUtc DEFAULT GETUTCDATE(),
        CONSTRAINT PK_AppUsers PRIMARY KEY (Id),
        CONSTRAINT UQ_AppUsers_Username UNIQUE (Username),
        CONSTRAINT CK_AppUsers_Role CHECK (Role IN ('Viewer', 'Engineer', 'Admin'))
    );
END;
GO
