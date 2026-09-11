-- Adds a nonclustered index on Orders(CustomerId, Status), turning the
-- Clustered Index Scan identified in the Day 5 execution plan into a Seek.
-- Re-runnable: only creates the index if it doesn't already exist.

USE OrderManagementDb;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_Orders_CustomerId_Status' AND object_id = OBJECT_ID('Orders')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_Orders_CustomerId_Status
    ON Orders (CustomerId, Status);
END
GO
