IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Partners')
BEGIN
    CREATE TABLE Partners (
        PartnerId INT IDENTITY(1,1) PRIMARY KEY,
        Name NVARCHAR(200) NOT NULL
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PartnerApiKeys')
BEGIN
    CREATE TABLE PartnerApiKeys (
        KeyId INT IDENTITY(1,1) PRIMARY KEY,
        PartnerId INT NOT NULL FOREIGN KEY REFERENCES Partners(PartnerId),
        HashedKey NVARCHAR(64) NOT NULL UNIQUE,  -- SHA-256 hex = 64 chars
        Status NVARCHAR(20) NOT NULL,             -- Active | Rotating | Revoked
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        RotatingSince DATETIME2 NULL,
        RevokedAt DATETIME2 NULL
    );
END