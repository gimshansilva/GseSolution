-- Least-privilege SQL login for the API (no DELETE, no DDL). Replace the password first.
USE master;
GO
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'GseApiUser')
    CREATE LOGIN GseApiUser WITH PASSWORD = N'<<REPLACE-WITH-A-LONG-RANDOM-PASSWORD>>', CHECK_POLICY = ON;
GO
USE GseOperationsDb;
GO
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'GseApiUser')
    CREATE USER GseApiUser FOR LOGIN GseApiUser;
GO
GRANT SELECT, INSERT, UPDATE ON SCHEMA::dbo TO GseApiUser;
GO
-- Connection string then looks like:
-- Server=.;Database=GseOperationsDb;User Id=GseApiUser;Password=...;TrustServerCertificate=True;Encrypt=True
