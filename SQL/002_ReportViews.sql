USE IncentivePortalDb;
GO

CREATE OR ALTER VIEW vw_IncentiveRegister AS
SELECT
    p.PartyCode,
    p.PartyName,
    b.Name AS BranchName,
    ms.Month,
    ms.Year,
    ms.SaleValue,
    ms.Outstanding,
    ic.GrossIncentive,
    ic.AdjustedAmount,
    ic.TransferAmount
FROM IncentiveCalculations ic
JOIN MonthlySales ms ON ms.Id = ic.MonthlySaleId
JOIN Parties p ON p.Id = ms.PartyId
JOIN Branches b ON b.Id = p.BranchId
WHERE ic.IsDeleted = 0;
GO

CREATE OR ALTER VIEW vw_TransferReport AS
SELECT
    p.PartyCode,
    p.PartyName,
    te.TransferMode,
    te.Amount,
    te.Status,
    te.UTR,
    te.TransferDate,
    te.ReconciledAt
FROM TransferEntries te
JOIN IncentiveCalculations ic ON ic.Id = te.IncentiveCalculationId
JOIN MonthlySales ms ON ms.Id = ic.MonthlySaleId
JOIN Parties p ON p.Id = ms.PartyId
WHERE te.IsDeleted = 0;
GO
