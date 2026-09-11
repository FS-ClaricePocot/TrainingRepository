-- OrderManagementDb setup script (re-runnable)
-- Purpose: Setup OrderManagementDb for FS Learning Activities

-- 1. Create database only if it doesn't already exist
IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = 'OrderManagementDb')
BEGIN
    CREATE DATABASE OrderManagementDb;
END
GO

USE OrderManagementDb;
GO

-- 2. Create Customers table only if it doesn't already exist
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Customers')
BEGIN
    CREATE TABLE Customers (
        CustomerId INT PRIMARY KEY IDENTITY(1,1),
        Name NVARCHAR(200) NOT NULL,
        Email NVARCHAR(320) NOT NULL,
        IsActive BIT NOT NULL DEFAULT 1
    );
END
GO

-- 3. Create Orders table only if it doesn't already exist
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Orders')
BEGIN
    CREATE TABLE Orders (
        OrderId INT PRIMARY KEY IDENTITY(1,1),
        CustomerId INT NOT NULL REFERENCES Customers(CustomerId),
        Total DECIMAL(10,2) NOT NULL,
        Status NVARCHAR(50) NOT NULL
    );
END
GO

-- 4. Enforce uniqueness on Email so re-inserts of the
--    same customer can be detected reliably so script is safe to run repeatedly.
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'UQ_Customers_Email' AND object_id = OBJECT_ID('Customers')
)
BEGIN
    CREATE UNIQUE INDEX UQ_Customers_Email ON Customers(Email);
END
GO

-- 5. Insert the repro customer (Juan Dela Cruz) only if it doesn't already exist,
--    and capture the CustomerId.
DECLARE @CustomerId INT;

IF NOT EXISTS (SELECT 1 FROM Customers WHERE Email = 'repro@example.com')
BEGIN
    INSERT INTO Customers (Name, Email, IsActive)
    VALUES ('Juan Dela Cruz', 'repro@example.com', 1);
END

SELECT @CustomerId = CustomerId
FROM Customers
WHERE Email = 'repro@example.com';

-- 6. Insert the three orders only if this customer with different status
IF NOT EXISTS (SELECT 1 FROM Orders WHERE CustomerId = @CustomerId)
BEGIN
    INSERT INTO Orders (CustomerId, Total, Status) VALUES
        (@CustomerId, 150.00, 'Completed'),
        (@CustomerId, 75.50,  'Completed'),
        (@CustomerId, 40.00,  'Pending');
END