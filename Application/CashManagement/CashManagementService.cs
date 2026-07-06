using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using IncentivePortal.Data;
using IncentivePortal.Helpers;
using IncentivePortal.Models;


namespace IncentivePortal.Services;

public interface ICashManagementService
{
    Task<CashInListViewModel> GetCashInListAsync(string? status, int? branchId, DateTime? from, DateTime? to, CancellationToken cancellationToken);
    Task CreateOrUpdateCashInAsync(CashInTransaction model, string action, string? attachmentPath, string user, CancellationToken cancellationToken);
    Task ApproveCashInAsync(int id, string newStatus, string? remarks, string user, CancellationToken cancellationToken);
    Task DeleteCashInAsync(int id, CancellationToken cancellationToken);

    Task<CashOutListViewModel> GetCashOutListAsync(string? status, int? branchId, DateTime? from, DateTime? to, CancellationToken cancellationToken);
    Task CreateOrUpdateCashOutAsync(CashOutTransaction model, string action, string? attachmentPath, string user, CancellationToken cancellationToken);
    Task ApproveCashOutAsync(int id, string newStatus, string? remarks, string user, CancellationToken cancellationToken);

    Task<ReconciliationViewModel> GetReconciliationAsync(string? reconStatus, int? branchId, int? year, int? month, CancellationToken cancellationToken);
    Task<AutoMatchResult> AutoMatchAsync(int year, int month, string user, CancellationToken cancellationToken);
    Task ApproveReconAsync(int id, string? remarks, string user, CancellationToken cancellationToken);
    Task VerifyTallyAsync(int id, string type, string tallyStatus, string? tallyVoucherNo, string? remarks, CancellationToken cancellationToken);
    Task ManualMatchAsync(int id, string tallyVoucherNo, decimal tallyAmount, string user, CancellationToken cancellationToken);
    Task ManualMatchTransactionsAsync(int cashInId, int cashOutId, string remarks, string user, CancellationToken cancellationToken);

    Task<ExceptionsViewModel> GetExceptionsAsync(string? severity, string? exStatus, CancellationToken cancellationToken);
    Task ResolveExceptionAsync(int id, string resolution, string user, CancellationToken cancellationToken);
    Task EscalateExceptionAsync(int id, CancellationToken cancellationToken);

    Task<CashBookViewModelContainer> GetCashBookAsync(int? branchId, DateTime? from, DateTime? to, CancellationToken cancellationToken);

    Task<List<CashPeriodControl>> GetPeriodControlsAsync(CancellationToken cancellationToken);
    Task OpenNewPeriodAsync(int year, int month, string user, CancellationToken cancellationToken);
    Task ClosePeriodAsync(int year, int month, string user, CancellationToken cancellationToken);
    Task LockPeriodAsync(int year, int month, CancellationToken cancellationToken);
    Task<CashPeriodControl?> GetActivePeriod(CancellationToken cancellationToken = default);
    Task<bool> IsCurrentPeriodLocked(CancellationToken cancellationToken = default);
    Task LockPeriod(int month, int year, string lockedBy, CancellationToken cancellationToken = default);

    Task<CashMastersViewModel> GetMastersAsync(CancellationToken cancellationToken);
    Task SaveMasterItemAsync(int id, string itemType, string name, CancellationToken cancellationToken);
    Task ToggleMasterItemActiveAsync(int id, CancellationToken cancellationToken);
    Task DeleteMasterItemAsync(int id, CancellationToken cancellationToken);

    Task<(bool success, decimal totalSale, int totalInvoices, string branchCode, string message)> GetDmsSaleAsync(int branchId, DateTime date, CancellationToken cancellationToken);

    Task<List<string>> SyncCostCenterCashAsync(int? month, int? year, CancellationToken cancellationToken);
    Task<List<CostCenterCash>> GetCostCenterCashListAsync(int? month, int? year, CancellationToken cancellationToken);
}

public class CashInListViewModel
{
    public List<Branch> Branches { get; set; } = new();
    public List<CashInTransaction> Transactions { get; set; } = new();
    public decimal TotalAmount { get; set; }
    public int TotalCount { get; set; }
    public List<string> ReceiptTypes { get; set; } = new();
    public List<Party> Parties { get; set; } = new();
}

public class CashOutListViewModel
{
    public List<Branch> Branches { get; set; } = new();
    public List<CashOutTransaction> Transactions { get; set; } = new();
    public decimal TotalAmount { get; set; }
    public int TotalCount { get; set; }
    public List<string> ExpenseCategories { get; set; } = new();
}

public class ReconciliationViewModel
{
    public List<Branch> Branches { get; set; } = new();
    public List<CashReconRecord> Records { get; set; } = new();
    public int TotalCount { get; set; }
    public int MatchedCount { get; set; }
    public int PartialCount { get; set; }
    public int MissTally { get; set; }
    public int MissPortal { get; set; }
    public int ResolvedYear { get; set; }
    public int ResolvedMonth { get; set; }
}

public class AutoMatchResult
{
    public int Matched { get; set; }
    public int Partial { get; set; }
    public int MissingTally { get; set; }
    public int MissingPortal { get; set; }
    public int TotalProcessed { get; set; }
}

public class ExceptionsViewModel
{
    public List<CashException> Exceptions { get; set; } = new();
    public int Critical { get; set; }
    public int High { get; set; }
    public int Medium { get; set; }
    public int Low { get; set; }
    public int OpenCount { get; set; }
}

public class CashBookViewModelContainer
{
    public List<Branch> Branches { get; set; } = new();
    public CashBookViewModel CashBook { get; set; } = new();
    public List<CashInTransaction> RegisterCashIns { get; set; } = new();
    public List<CashOutTransaction> RegisterCashOuts { get; set; } = new();
    public List<string> ReceiptTypes { get; set; } = new();
    public List<string> ExpenseCategories { get; set; } = new();
    public List<Party> Parties { get; set; } = new();
}

public class CashMastersViewModel
{
    public List<CashMasterItem> ReceiptTypes { get; set; } = new();
    public List<CashMasterItem> ExpenseCategories { get; set; } = new();
}

public sealed class CashManagementService(IncentiveDbContext db, ICurrentUser cu, IConfiguration configuration) : ICashManagementService
{
    private bool IsHO() => cu.IsInRole(AppRoles.SuperAdmin) || cu.IsInRole(AppRoles.HOFinance) || cu.IsInRole(AppRoles.Auditor);
    private bool IsAdmin() => cu.IsInRole(AppRoles.SuperAdmin);
    private bool CanApprove() => cu.IsInRole(AppRoles.SuperAdmin) || cu.IsInRole(AppRoles.HOFinance);

    private async Task CheckPeriodOpenAsync(int year, int month, CancellationToken cancellationToken)
    {
        var period = await db.CashPeriodControls
            .FirstOrDefaultAsync(p => p.ControlYear == year && p.ControlMonth == month, cancellationToken);
        if (period != null)
        {
            if (period.Status == "Locked")
            {
                throw new InvalidOperationException($"Period {year}/{month} is closed for changes.");
            }
            if (period.Status == "Closed" && !IsAdmin())
            {
                throw new InvalidOperationException($"Period {year}/{month} is closed for changes.");
            }
        }
    }

    private IQueryable<CashInTransaction> CashInQuery()
    {
        var q = db.CashInTransactions.Include(x => x.Branch).AsQueryable();
        if (!IsHO() && cu.BranchId.HasValue)
            q = q.Where(x => x.BranchId == cu.BranchId.Value);
        return q;
    }

    private IQueryable<CashOutTransaction> CashOutQuery()
    {
        var q = db.CashOutTransactions.Include(x => x.Branch).AsQueryable();
        if (!IsHO() && cu.BranchId.HasValue)
            q = q.Where(x => x.BranchId == cu.BranchId.Value);
        return q;
    }

    private async Task<List<Branch>> GetAllowedBranchesAsync(CancellationToken cancellationToken)
    {
        var branches = await db.Branches.Where(x => !x.IsDeleted).OrderBy(x => x.Name).ToListAsync(cancellationToken);
        if (!IsHO() && cu.BranchId.HasValue)
            branches = branches.Where(x => x.Id == cu.BranchId.Value).ToList();
        return branches;
    }

    private string NextRef(string prefix, int count) =>
        $"{prefix}-{DateTime.Now:yyyyMM}-{(count + 1):D4}";

    public async Task<CashInListViewModel> GetCashInListAsync(string? status, int? branchId, DateTime? from, DateTime? to, CancellationToken cancellationToken)
    {
        var branches = await GetAllowedBranchesAsync(cancellationToken);

        IQueryable<CashInTransaction> q = CashInQuery();

        if (!string.IsNullOrEmpty(status))    q = q.Where(x => x.Status == status);
        if (branchId.HasValue)                q = q.Where(x => x.BranchId == branchId.Value);
        if (from.HasValue)                    q = q.Where(x => x.TransactionDate >= from.Value);
        if (to.HasValue)                      q = q.Where(x => x.TransactionDate <= to.Value);

        q = q.OrderByDescending(x => x.TransactionDate).ThenByDescending(x => x.Id);

        var totalAmount = await q.SumAsync(x => x.Amount, cancellationToken);
        var totalCount = await q.CountAsync(cancellationToken);

        var receiptTypes = await db.CashMasterItems
            .Where(x => x.ItemType == "ReceiptType" && x.IsActive && !x.IsDeleted)
            .OrderBy(x => x.Name)
            .Select(x => x.Name)
            .ToListAsync(cancellationToken);

        var parties = await db.Parties
            .Where(x => !x.IsDeleted && x.Status == "Active")
            .OrderBy(x => x.PartyCode)
            .ToListAsync(cancellationToken);

        var transactions = await q.Take(500).ToListAsync(cancellationToken);

        return new CashInListViewModel
        {
            Branches = branches,
            Transactions = transactions,
            TotalAmount = totalAmount,
            TotalCount = totalCount,
            ReceiptTypes = receiptTypes,
            Parties = parties
        };
    }

    public async Task CreateOrUpdateCashInAsync(CashInTransaction model, string action, string? attachmentPath, string user, CancellationToken cancellationToken)
    {
        var branches = await GetAllowedBranchesAsync(cancellationToken);
        if (!IsHO() && cu.BranchId.HasValue)
            model.BranchId = cu.BranchId.Value;

        if (!branches.Any(b => b.Id == model.BranchId))
        {
            throw new UnauthorizedAccessException("Unauthorized branch.");
        }

        // Enforce period lock checks
        await CheckPeriodOpenAsync(model.TransactionDate.Year, model.TransactionDate.Month, cancellationToken);
        if (model.Id > 0)
        {
            var existing = await db.CashInTransactions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == model.Id, cancellationToken);
            if (existing != null)
            {
                await CheckPeriodOpenAsync(existing.TransactionDate.Year, existing.TransactionDate.Month, cancellationToken);
            }
        }

        if (attachmentPath != null)
        {
            model.AttachmentPath = attachmentPath;
        }

        // Sanitize string properties that could be bound as null
        model.CustomerName    = model.CustomerName?.Trim() ?? string.Empty;
        model.DealerCode      = model.DealerCode?.Trim() ?? string.Empty;
        model.BankName        = model.BankName?.Trim() ?? string.Empty;
        model.ReferenceNo     = model.ReferenceNo?.Trim() ?? string.Empty;
        model.Narration       = model.Narration?.Trim() ?? string.Empty;
        model.ReceiptType     = model.ReceiptType?.Trim() ?? string.Empty;
        model.PaymentMode     = model.PaymentMode?.Trim() ?? string.Empty;

        if (model.Id > 0)
        {
            if (!IsHO()) throw new UnauthorizedAccessException("Only admin or finance manager can edit.");
            var tx = await db.CashInTransactions.FindAsync(new object[] { model.Id }, cancellationToken);
            if (tx == null) throw new KeyNotFoundException("Transaction not found.");

            tx.TransactionDate = model.TransactionDate;
            tx.BranchId        = model.BranchId;
            tx.ReceiptType     = model.ReceiptType;
            tx.CustomerName    = model.CustomerName;
            tx.DealerCode      = model.DealerCode;
            tx.Amount          = model.Amount;
            tx.PaymentMode     = model.PaymentMode;
            tx.BankName        = model.BankName;
            tx.ReferenceNo     = model.ReferenceNo;
            tx.Narration       = model.Narration;
            tx.Status          = action == "submit" ? CashTxStatus.Submitted : tx.Status;
            if (!string.IsNullOrEmpty(model.AttachmentPath))
            {
                tx.AttachmentPath = model.AttachmentPath;
            }

            await db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            var count = await db.CashInTransactions.CountAsync(cancellationToken);
            model.TransactionNo = NextRef("CI", count);
            model.Status        = action == "submit" ? CashTxStatus.Submitted : CashTxStatus.Draft;
            model.CreatedBy     = user;
            model.CreatedAt     = DateTime.UtcNow;

            db.CashInTransactions.Add(model);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task ApproveCashInAsync(int id, string newStatus, string? remarks, string user, CancellationToken cancellationToken)
    {
        if (!CanApprove()) throw new UnauthorizedAccessException("Unauthorized.");
        var tx = await db.CashInTransactions.FindAsync(new object[] { id }, cancellationToken);
        if (tx == null) throw new KeyNotFoundException("Transaction not found.");

        tx.Status          = newStatus;
        tx.ApprovalRemarks = remarks;
        tx.ApprovedBy      = user;
        tx.ApprovedAt      = DateTime.UtcNow;

        if (newStatus == CashTxStatus.Posted && string.IsNullOrEmpty(tx.TallyVoucherNo))
            tx.TallyVoucherNo = $"TLY-{Random.Shared.Next(5000, 9999)}";

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteCashInAsync(int id, CancellationToken cancellationToken)
    {
        var tx = await db.CashInTransactions.FindAsync(new object[] { id }, cancellationToken);
        if (tx == null || tx.Status != CashTxStatus.Draft) throw new InvalidOperationException("Only draft transactions can be deleted.");
        tx.IsDeleted = true;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<CashOutListViewModel> GetCashOutListAsync(string? status, int? branchId, DateTime? from, DateTime? to, CancellationToken cancellationToken)
    {
        var branches = await GetAllowedBranchesAsync(cancellationToken);

        IQueryable<CashOutTransaction> q = CashOutQuery();

        if (!string.IsNullOrEmpty(status)) q = q.Where(x => x.Status == status);
        if (branchId.HasValue)             q = q.Where(x => x.BranchId == branchId.Value);
        if (from.HasValue)                 q = q.Where(x => x.TransactionDate >= from.Value);
        if (to.HasValue)                   q = q.Where(x => x.TransactionDate <= to.Value);

        q = q.OrderByDescending(x => x.TransactionDate).ThenByDescending(x => x.Id);

        var totalAmount = await q.SumAsync(x => x.Amount, cancellationToken);
        var totalCount = await q.CountAsync(cancellationToken);

        var expenseCategories = await db.CashMasterItems
            .Where(x => x.ItemType == "ExpenseCategory" && x.IsActive && !x.IsDeleted)
            .OrderBy(x => x.Name)
            .Select(x => x.Name)
            .ToListAsync(cancellationToken);

        var transactions = await q.Take(500).ToListAsync(cancellationToken);

        return new CashOutListViewModel
        {
            Branches = branches,
            Transactions = transactions,
            TotalAmount = totalAmount,
            TotalCount = totalCount,
            ExpenseCategories = expenseCategories
        };
    }

    public async Task CreateOrUpdateCashOutAsync(CashOutTransaction model, string action, string? attachmentPath, string user, CancellationToken cancellationToken)
    {
        var branches = await GetAllowedBranchesAsync(cancellationToken);
        if (!IsHO() && cu.BranchId.HasValue)
            model.BranchId = cu.BranchId.Value;

        if (!branches.Any(b => b.Id == model.BranchId))
        {
            throw new UnauthorizedAccessException("Unauthorized branch.");
        }

        // Enforce period lock checks
        await CheckPeriodOpenAsync(model.TransactionDate.Year, model.TransactionDate.Month, cancellationToken);
        if (model.Id > 0)
        {
            var existing = await db.CashOutTransactions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == model.Id, cancellationToken);
            if (existing != null)
            {
                await CheckPeriodOpenAsync(existing.TransactionDate.Year, existing.TransactionDate.Month, cancellationToken);
            }
        }

        if (attachmentPath != null)
        {
            model.AttachmentPath = attachmentPath;
        }

        // Sanitize string properties that could be bound as null
        model.VendorName      = model.VendorName?.Trim() ?? string.Empty;
        model.CostCenter      = model.CostCenter?.Trim() ?? string.Empty;
        model.GlAccount       = model.GlAccount?.Trim() ?? string.Empty;
        model.PaymentMode     = model.PaymentMode?.Trim() ?? string.Empty;
        model.PaymentInstrument = model.PaymentInstrument?.Trim() ?? string.Empty;
        model.BankName        = model.BankName?.Trim() ?? string.Empty;
        model.ReferenceNo     = model.ReferenceNo?.Trim() ?? string.Empty;
        model.Narration       = model.Narration?.Trim() ?? string.Empty;
        model.ExpenseCategory = model.ExpenseCategory?.Trim() ?? string.Empty;

        if (model.Id > 0)
        {
            if (!IsHO()) throw new UnauthorizedAccessException("Only admin or finance manager can edit.");
            var tx = await db.CashOutTransactions.FindAsync(new object[] { model.Id }, cancellationToken);
            if (tx == null) throw new KeyNotFoundException("Transaction not found.");

            tx.TransactionDate = model.TransactionDate;
            tx.BranchId        = model.BranchId;
            tx.ExpenseCategory = model.ExpenseCategory;
            tx.VendorName      = model.VendorName;
            tx.CostCenter      = model.CostCenter;
            tx.GlAccount       = model.GlAccount;
            tx.Amount          = model.Amount;
            tx.PaymentMode     = model.PaymentMode;
            tx.PaymentInstrument = model.PaymentInstrument;
            tx.BankName        = model.BankName;
            tx.ReferenceNo     = model.ReferenceNo;
            tx.Narration       = model.Narration;
            tx.Status          = action == "submit" ? CashTxStatus.Submitted : tx.Status;
            if (!string.IsNullOrEmpty(model.AttachmentPath))
            {
                tx.AttachmentPath = model.AttachmentPath;
            }

            await db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            var count = await db.CashOutTransactions.CountAsync(cancellationToken);
            model.TransactionNo = NextRef("CO", count);
            model.Status        = action == "submit" ? CashTxStatus.Submitted : CashTxStatus.Draft;
            model.CreatedBy     = user;
            model.CreatedAt     = DateTime.UtcNow;

            db.CashOutTransactions.Add(model);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task ApproveCashOutAsync(int id, string newStatus, string? remarks, string user, CancellationToken cancellationToken)
    {
        if (!CanApprove()) throw new UnauthorizedAccessException("Unauthorized.");
        var tx = await db.CashOutTransactions.FindAsync(new object[] { id }, cancellationToken);
        if (tx == null) throw new KeyNotFoundException("Transaction not found.");

        tx.Status          = newStatus;
        tx.ApprovalRemarks = remarks;
        tx.ApprovedBy      = user;
        tx.ApprovedAt      = DateTime.UtcNow;

        if (newStatus == CashTxStatus.Posted && string.IsNullOrEmpty(tx.TallyVoucherNo))
            tx.TallyVoucherNo = $"TLY-{Random.Shared.Next(6000, 9999)}";

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ReconciliationViewModel> GetReconciliationAsync(string? reconStatus, int? branchId, int? year, int? month, CancellationToken cancellationToken)
    {
        var branches = await db.Branches.Where(x => !x.IsDeleted).OrderBy(x => x.Name).ToListAsync(cancellationToken);

        int targetYear = year ?? 0;
        int targetMonth = month ?? 0;

        if (targetYear == 0 || targetMonth == 0)
        {
            var latestOpen = await db.CashPeriodControls
                .Where(x => x.Status == "Open")
                .OrderByDescending(x => x.ControlYear)
                .ThenByDescending(x => x.ControlMonth)
                .FirstOrDefaultAsync(cancellationToken);

            if (latestOpen != null)
            {
                targetYear = latestOpen.ControlYear;
                targetMonth = latestOpen.ControlMonth;
            }
            else
            {
                var now = DateTime.Now;
                targetYear = now.Year;
                targetMonth = now.Month;
            }
        }

        var q = db.CashReconRecords.Include(x => x.Branch)
                   .Include(x => x.CashIn).Include(x => x.CashOut)
                   .Where(x =>
                       (x.CashIn != null && x.CashIn.TransactionDate.Year == targetYear && x.CashIn.TransactionDate.Month == targetMonth) ||
                       (x.CashOut != null && x.CashOut.TransactionDate.Year == targetYear && x.CashOut.TransactionDate.Month == targetMonth) ||
                       (x.CashIn == null && x.CashOut == null && x.ReconDate.Year == targetYear && x.ReconDate.Month == targetMonth)
                   );

        if (!string.IsNullOrEmpty(reconStatus)) q = q.Where(x => x.ReconStatus == reconStatus);
        if (branchId.HasValue)                  q = q.Where(x => x.BranchId == branchId.Value);

        var list = await q.OrderByDescending(x => x.ReconDate).Take(500).ToListAsync(cancellationToken);

        // KPIs (scoped to resolved month/year)
        var kpiQuery = db.CashReconRecords.Include(x => x.CashIn).Include(x => x.CashOut)
                   .Where(x =>
                       (x.CashIn != null && x.CashIn.TransactionDate.Year == targetYear && x.CashIn.TransactionDate.Month == targetMonth) ||
                       (x.CashOut != null && x.CashOut.TransactionDate.Year == targetYear && x.CashOut.TransactionDate.Month == targetMonth) ||
                       (x.CashIn == null && x.CashOut == null && x.ReconDate.Year == targetYear && x.ReconDate.Month == targetMonth)
                   );

        if (branchId.HasValue) kpiQuery = kpiQuery.Where(x => x.BranchId == branchId.Value);

        var kpiList = await kpiQuery.ToListAsync(cancellationToken);
        var totalCount = kpiList.Count;
        var matchedCount = kpiList.Count(x => x.ReconStatus == ReconStatus.Matched || x.ReconStatus == ReconStatus.Approved || x.ReconStatus == ReconStatus.ManuallyMatched);
        var partialCount = kpiList.Count(x => x.ReconStatus == ReconStatus.Partial || x.ReconStatus == ReconStatus.FuzzyMatched);
        var missTally = kpiList.Count(x => x.ReconStatus == ReconStatus.MissingTally || x.ReconStatus == ReconStatus.ExcessCash);
        var missPortal = kpiList.Count(x => x.ReconStatus == ReconStatus.MissingPortal || x.ReconStatus == ReconStatus.ShortCash);

        return new ReconciliationViewModel
        {
            Branches = branches,
            Records = list,
            TotalCount = totalCount,
            MatchedCount = matchedCount,
            PartialCount = partialCount,
            MissTally = missTally,
            MissPortal = missPortal,
            ResolvedYear = targetYear,
            ResolvedMonth = targetMonth
        };
    }

    private string GetTallyOutletCode(Branch branch)
    {
        return string.IsNullOrWhiteSpace(branch.TallyOutletCode)
            ? (branch.Code switch
            {
                "VBZ" => "01",
                "RQL" => "02",
                "BGI" => "03",
                "GRL" => "04",
                "BSE" => "05",
                "PSS" => "06",
                "NBT" => "07",
                "PPH" => "08",
                "JPD" => "09",
                "ISN" => "10",
                "DUS" => "11",
                "JGT" => "12",
                "UTD" => "13",
                "SGN" => "14",
                "HMR" => "15",
                "ALW" => "16",
                "BWI" => "17",
                "STO" => "18",
                "SKR" => "19",
                "JNU" => "20",
                "SDH" => "21",
                "HDN" => "22",
                "TNG" => "23",
                "PKT" => "24",
                "JSK" => "25",
                "LQU" => "26",
                "OR7" => "27",
                "HUH" => "28",
                "SKF" => "29",
                "F33" => "30",
                "BER" => "31",
                "KNO" => "32",
                "SJG" => "33",
                "CR9" => "34",
                "SGH" => "35",
                _ => branch.Id.ToString("D2")
            })
            : branch.TallyOutletCode;
    }

    private class TallyVoucherDto
    {
        public string VoucherNo { get; set; } = string.Empty;
        public DateTime VoucherDate { get; set; }
        public decimal VoucherAmount { get; set; }
        public string TransactionType { get; set; } = "CashIn";
        public string OutletCode { get; set; } = string.Empty;
    }

    public async Task<AutoMatchResult> AutoMatchAsync(int year, int month, string user, CancellationToken cancellationToken)
    {
        await CheckPeriodOpenAsync(year, month, cancellationToken);

        int matched = 0;
        int partial = 0;
        int excessCount = 0;
        int shortCount = 0;

        var allowedBranches = await GetAllowedBranchesAsync(cancellationToken);
        var allowedBranchIds = allowedBranches.Select(b => b.Id).ToList();

        // Fetch CashIn and CashOut records that don't have a ReconRecord
        var cashIns = await db.CashInTransactions
            .Where(x => allowedBranchIds.Contains(x.BranchId) &&
                        x.TransactionDate.Year == year &&
                        x.TransactionDate.Month == month &&
                        (x.Status == CashTxStatus.Approved || x.Status == CashTxStatus.Posted || x.Status == CashTxStatus.Reconciled || x.Status == CashTxStatus.Closed) &&
                        !db.CashReconRecords.Any(r => r.CashInId == x.Id))
            .ToListAsync(cancellationToken);

        var cashOuts = await db.CashOutTransactions
            .Where(x => allowedBranchIds.Contains(x.BranchId) &&
                        x.TransactionDate.Year == year &&
                        x.TransactionDate.Month == month &&
                        (x.Status == CashTxStatus.Approved || x.Status == CashTxStatus.Posted || x.Status == CashTxStatus.Reconciled || x.Status == CashTxStatus.Closed) &&
                        !db.CashReconRecords.Any(r => r.CashOutId == x.Id))
            .ToListAsync(cancellationToken);

        var unmatchedIns = cashIns.ToList();
        var unmatchedOuts = cashOuts.ToList();
        var recRecords = new List<CashReconRecord>();
        int recCount = await db.CashReconRecords.CountAsync(cancellationToken);

        // PASS 1: Exact Match
        for (int i = unmatchedIns.Count - 1; i >= 0; i--)
        {
            var ci = unmatchedIns[i];
            var co = unmatchedOuts.FirstOrDefault(x => 
                x.BranchId == ci.BranchId && 
                x.Amount == ci.Amount && 
                Math.Abs((ci.TransactionDate.Date - x.TransactionDate.Date).Days) <= 3);

            if (co != null)
            {
                recCount++;
                var rec = new CashReconRecord
                {
                    ReconRef = NextRef("REC", recCount),
                    BranchId = ci.BranchId,
                    ReconDate = DateTime.Today,
                    TransactionType = "CashBookMatch",
                    CashInId = ci.Id,
                    CashOutId = co.Id,
                    PortalAmount = ci.Amount,
                    TallyAmount = co.Amount,
                    Variance = 0,
                    ReconStatus = ReconStatus.Matched,
                    Remarks = "Auto matched (Exact Pass 1)",
                    CreatedBy = user,
                    CreatedAt = DateTime.UtcNow
                };
                recRecords.Add(rec);
                matched++;

                unmatchedIns.RemoveAt(i);
                unmatchedOuts.Remove(co);
            }
        }

        // PASS 2: Fuzzy Match
        var fuzzyTolerance = configuration.GetValue<decimal>("Reconciliation:FuzzyMatchToleranceAmount", 50.00m);
        for (int i = unmatchedIns.Count - 1; i >= 0; i--)
        {
            var ci = unmatchedIns[i];
            var co = unmatchedOuts.FirstOrDefault(x => 
                x.BranchId == ci.BranchId && 
                Math.Abs(ci.Amount - x.Amount) <= fuzzyTolerance && 
                Math.Abs((ci.TransactionDate.Date - x.TransactionDate.Date).Days) <= 7);

            if (co != null)
            {
                recCount++;
                var rec = new CashReconRecord
                {
                    ReconRef = NextRef("REC", recCount),
                    BranchId = ci.BranchId,
                    ReconDate = DateTime.Today,
                    TransactionType = "CashBookMatch",
                    CashInId = ci.Id,
                    CashOutId = co.Id,
                    PortalAmount = ci.Amount,
                    TallyAmount = co.Amount,
                    Variance = ci.Amount - co.Amount,
                    ReconStatus = ReconStatus.FuzzyMatched,
                    Remarks = $"Auto matched (Fuzzy Pass 2). Variance: {ci.Amount - co.Amount}",
                    CreatedBy = user,
                    CreatedAt = DateTime.UtcNow
                };
                recRecords.Add(rec);
                partial++;

                unmatchedIns.RemoveAt(i);
                unmatchedOuts.Remove(co);
            }
        }

        // PASS 3: Classify Unmatched
        foreach (var ci in unmatchedIns)
        {
            recCount++;
            var rec = new CashReconRecord
            {
                ReconRef = NextRef("REC", recCount),
                BranchId = ci.BranchId,
                ReconDate = DateTime.Today,
                TransactionType = "CashIn",
                CashInId = ci.Id,
                CashOutId = null,
                PortalAmount = ci.Amount,
                TallyAmount = 0,
                Variance = ci.Amount,
                ReconStatus = ReconStatus.ExcessCash,
                Remarks = "Unmatched CashIn classified as ExcessCash (Pass 3)",
                CreatedBy = user,
                CreatedAt = DateTime.UtcNow
            };
            recRecords.Add(rec);
            excessCount++;
        }

        foreach (var co in unmatchedOuts)
        {
            recCount++;
            var rec = new CashReconRecord
            {
                ReconRef = NextRef("REC", recCount),
                BranchId = co.BranchId,
                ReconDate = DateTime.Today,
                TransactionType = "CashOut",
                CashOutId = co.Id,
                CashInId = null,
                PortalAmount = co.Amount,
                TallyAmount = 0,
                Variance = -co.Amount,
                ReconStatus = ReconStatus.ShortCash,
                Remarks = "Unmatched CashOut classified as ShortCash (Pass 3)",
                CreatedBy = user,
                CreatedAt = DateTime.UtcNow
            };
            recRecords.Add(rec);
            shortCount++;
        }

        db.CashReconRecords.AddRange(recRecords);
        await db.SaveChangesAsync(cancellationToken);

        return new AutoMatchResult
        {
            Matched = matched,
            Partial = partial,
            MissingTally = excessCount,
            MissingPortal = shortCount,
            TotalProcessed = matched * 2 + partial * 2 + excessCount + shortCount
        };
    }

    public async Task ManualMatchTransactionsAsync(int cashInId, int cashOutId, string remarks, string user, CancellationToken cancellationToken)
    {
        var cashIn = await db.CashInTransactions.FirstOrDefaultAsync(x => x.Id == cashInId, cancellationToken);
        if (cashIn == null) throw new KeyNotFoundException("Cash In transaction not found.");

        var cashOut = await db.CashOutTransactions.FirstOrDefaultAsync(x => x.Id == cashOutId, cancellationToken);
        if (cashOut == null) throw new KeyNotFoundException("Cash Out transaction not found.");

        await CheckPeriodOpenAsync(cashIn.TransactionDate.Year, cashIn.TransactionDate.Month, cancellationToken);
        await CheckPeriodOpenAsync(cashOut.TransactionDate.Year, cashOut.TransactionDate.Month, cancellationToken);

        var existingInRecon = await db.CashReconRecords.FirstOrDefaultAsync(r => r.CashInId == cashInId, cancellationToken);
        if (existingInRecon != null && existingInRecon.ReconStatus != ReconStatus.FuzzyMatched && existingInRecon.ReconStatus != ReconStatus.ExcessCash)
        {
            throw new InvalidOperationException("Cash In transaction is already matched.");
        }

        var existingOutRecon = await db.CashReconRecords.FirstOrDefaultAsync(r => r.CashOutId == cashOutId, cancellationToken);
        if (existingOutRecon != null && existingOutRecon.ReconStatus != ReconStatus.FuzzyMatched && existingOutRecon.ReconStatus != ReconStatus.ShortCash)
        {
            throw new InvalidOperationException("Cash Out transaction is already matched.");
        }

        if (existingInRecon != null) db.CashReconRecords.Remove(existingInRecon);
        if (existingOutRecon != null) db.CashReconRecords.Remove(existingOutRecon);

        var recCount = await db.CashReconRecords.CountAsync(cancellationToken);
        var newRecord = new CashReconRecord
        {
            ReconRef = NextRef("REC", recCount),
            BranchId = cashIn.BranchId,
            ReconDate = DateTime.Today,
            TransactionType = "CashBookMatch",
            CashInId = cashInId,
            CashOutId = cashOutId,
            PortalAmount = cashIn.Amount,
            TallyAmount = cashOut.Amount,
            Variance = cashIn.Amount - cashOut.Amount,
            ReconStatus = ReconStatus.ManuallyMatched,
            Remarks = remarks,
            CreatedBy = user,
            CreatedAt = DateTime.UtcNow
        };
        db.CashReconRecords.Add(newRecord);

        await db.SaveChangesAsync(cancellationToken);

        var audit = new AuditLog
        {
            EntityName = "CashReconRecord",
            EntityId = newRecord.Id.ToString(),
            Action = "ManualMatch",
            OldValue = "{}",
            NewValue = $"{{\"CashInId\":{cashInId},\"CashOutId\":{cashOutId},\"Remarks\":\"{remarks}\"}}",
            ChangedBy = user,
            ChangedAt = DateTime.UtcNow
        };
        db.AuditLogs.Add(audit);

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ApproveReconAsync(int id, string? remarks, string user, CancellationToken cancellationToken)
    {
        var rec = await db.CashReconRecords
            .Include(r => r.CashIn)
            .Include(r => r.CashOut)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (rec == null) throw new KeyNotFoundException("Reconciliation record not found.");

        var date = rec.CashIn != null ? rec.CashIn.TransactionDate : (rec.CashOut != null ? rec.CashOut.TransactionDate : rec.ReconDate);
        await CheckPeriodOpenAsync(date.Year, date.Month, cancellationToken);

        rec.ReconStatus = ReconStatus.Approved;
        rec.Remarks     = remarks ?? rec.Remarks;
        rec.ApprovedBy  = user;
        rec.ApprovedAt  = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task VerifyTallyAsync(int id, string type, string tallyStatus, string? tallyVoucherNo, string? remarks, CancellationToken cancellationToken)
    {
        if (type == "CashIn")
        {
            var tx = await db.CashInTransactions.FindAsync(new object[] { id }, cancellationToken);
            if (tx == null) throw new KeyNotFoundException("Transaction not found.");

            await CheckPeriodOpenAsync(tx.TransactionDate.Year, tx.TransactionDate.Month, cancellationToken);

            tx.TallySyncStatus = tallyStatus?.Trim() ?? string.Empty;
            tx.TallySyncAt     = DateTime.UtcNow;
            if (!string.IsNullOrEmpty(tallyVoucherNo))
                tx.TallyVoucherNo = tallyVoucherNo.Trim();
            if (!string.IsNullOrEmpty(remarks))
                tx.ApprovalRemarks = remarks.Trim();

            await db.SaveChangesAsync(cancellationToken);
        }
        else if (type == "CashOut")
        {
            var tx = await db.CashOutTransactions.FindAsync(new object[] { id }, cancellationToken);
            if (tx == null) throw new KeyNotFoundException("Transaction not found.");

            await CheckPeriodOpenAsync(tx.TransactionDate.Year, tx.TransactionDate.Month, cancellationToken);

            tx.TallySyncStatus = tallyStatus?.Trim() ?? string.Empty;
            tx.TallySyncAt     = DateTime.UtcNow;
            if (!string.IsNullOrEmpty(tallyVoucherNo))
                tx.TallyVoucherNo = tallyVoucherNo.Trim();
            if (!string.IsNullOrEmpty(remarks))
                tx.ApprovalRemarks = remarks.Trim();

            await db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            throw new ArgumentException("Invalid transaction type.");
        }
    }

    public async Task ManualMatchAsync(int id, string tallyVoucherNo, decimal tallyAmount, string user, CancellationToken cancellationToken)
    {
        var rec = await db.CashReconRecords
            .Include(r => r.CashIn)
            .Include(r => r.CashOut)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (rec == null) throw new KeyNotFoundException("Reconciliation record not found.");

        var date = rec.CashIn != null ? rec.CashIn.TransactionDate : (rec.CashOut != null ? rec.CashOut.TransactionDate : rec.ReconDate);
        await CheckPeriodOpenAsync(date.Year, date.Month, cancellationToken);

        rec.TallyVoucherNo = tallyVoucherNo?.Trim() ?? string.Empty;
        rec.TallyAmount = tallyAmount;
        rec.Variance = rec.PortalAmount - tallyAmount;
        rec.ReconStatus = ReconStatus.Matched;
        rec.Remarks = $"Manually matched by {user}";
        rec.ReconDate = DateTime.Today;

        if (rec.CashIn != null)
        {
            rec.CashIn.TallyVoucherNo = tallyVoucherNo;
            rec.CashIn.TallySyncStatus = "Matched";
            rec.CashIn.TallySyncAt = DateTime.UtcNow;
            db.Entry(rec.CashIn).State = EntityState.Modified;
        }
        else if (rec.CashOut != null)
        {
            rec.CashOut.TallyVoucherNo = tallyVoucherNo;
            rec.CashOut.TallySyncStatus = "Matched";
            rec.CashOut.TallySyncAt = DateTime.UtcNow;
            db.Entry(rec.CashOut).State = EntityState.Modified;
        }

        db.Entry(rec).State = EntityState.Modified;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ExceptionsViewModel> GetExceptionsAsync(string? severity, string? exStatus, CancellationToken cancellationToken)
    {
        var q = db.CashExceptions.Include(x => x.Branch).AsQueryable();
        if (!IsHO() && cu.BranchId.HasValue)
            q = q.Where(x => x.BranchId == cu.BranchId.Value);
        if (!string.IsNullOrEmpty(severity)) q = q.Where(x => x.Severity == severity);
        if (!string.IsNullOrEmpty(exStatus)) q = q.Where(x => x.Status   == exStatus);

        var list = await q.OrderByDescending(x => x.ExceptionDate).ToListAsync(cancellationToken);

        var critical = list.Count(x => x.Severity == ExceptionSeverity.Critical);
        var high = list.Count(x => x.Severity == ExceptionSeverity.High);
        var medium = list.Count(x => x.Severity == ExceptionSeverity.Medium);
        var low = list.Count(x => x.Severity == ExceptionSeverity.Low);
        var openCount = list.Count(x => x.Status != "Resolved");

        return new ExceptionsViewModel
        {
            Exceptions = list,
            Critical = critical,
            High = high,
            Medium = medium,
            Low = low,
            OpenCount = openCount
        };
    }

    public async Task ResolveExceptionAsync(int id, string resolution, string user, CancellationToken cancellationToken)
    {
        var ex = await db.CashExceptions.FindAsync(new object[] { id }, cancellationToken);
        if (ex == null) throw new KeyNotFoundException("Exception not found.");
        ex.Status     = "Resolved";
        ex.Resolution = resolution;
        ex.ResolvedBy = user;
        ex.ResolvedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task EscalateExceptionAsync(int id, CancellationToken cancellationToken)
    {
        var ex = await db.CashExceptions.FindAsync(new object[] { id }, cancellationToken);
        if (ex == null) throw new KeyNotFoundException("Exception not found.");
        ex.Status = "Escalated";
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<CashBookViewModelContainer> GetCashBookAsync(int? branchId, DateTime? from, DateTime? to, CancellationToken cancellationToken)
    {
        var branches = await GetAllowedBranchesAsync(cancellationToken);

        var today = DateTime.Today;
        var startDate = from ?? new DateTime(today.Year, today.Month, 1);
        var endDate = to ?? today;

        // 1. Calculate opening balance: sum of all non-draft, non-rejected CashIn prior to startDate minus prior CashOut
        var priorInQuery = db.CashInTransactions.Where(x => !x.IsDeleted && x.Status != CashTxStatus.Draft && x.Status != CashTxStatus.Rejected && x.TransactionDate < startDate);
        var priorOutQuery = db.CashOutTransactions.Where(x => !x.IsDeleted && x.Status != CashTxStatus.Draft && x.Status != CashTxStatus.Rejected && x.TransactionDate < startDate);

        if (branchId.HasValue)
        {
            priorInQuery = priorInQuery.Where(x => x.BranchId == branchId.Value);
            priorOutQuery = priorOutQuery.Where(x => x.BranchId == branchId.Value);
        }
        else if (!IsHO() && cu.BranchId.HasValue)
        {
            priorInQuery = priorInQuery.Where(x => x.BranchId == cu.BranchId.Value);
            priorOutQuery = priorOutQuery.Where(x => x.BranchId == cu.BranchId.Value);
        }

        decimal priorIn = await priorInQuery.SumAsync(x => x.Amount, cancellationToken);
        decimal priorOut = await priorOutQuery.SumAsync(x => x.Amount, cancellationToken);
        decimal openingBalance = priorIn - priorOut;

        // 2. Fetch current period transactions for Ledger (non-draft, non-rejected)
        var curInQuery = db.CashInTransactions.Include(x => x.Branch)
            .Where(x => !x.IsDeleted && x.Status != CashTxStatus.Draft && x.Status != CashTxStatus.Rejected && x.TransactionDate >= startDate && x.TransactionDate <= endDate);
        var curOutQuery = db.CashOutTransactions.Include(x => x.Branch)
            .Where(x => !x.IsDeleted && x.Status != CashTxStatus.Draft && x.Status != CashTxStatus.Rejected && x.TransactionDate >= startDate && x.TransactionDate <= endDate);

        // Fetch register transactions (all statuses, so draft/rejected are visible in the sub-pages)
        var regInQuery = db.CashInTransactions.Include(x => x.Branch)
            .Where(x => !x.IsDeleted && x.TransactionDate >= startDate && x.TransactionDate <= endDate);
        var regOutQuery = db.CashOutTransactions.Include(x => x.Branch)
            .Where(x => !x.IsDeleted && x.TransactionDate >= startDate && x.TransactionDate <= endDate);

        if (branchId.HasValue)
        {
            curInQuery = curInQuery.Where(x => x.BranchId == branchId.Value);
            curOutQuery = curOutQuery.Where(x => x.BranchId == branchId.Value);
            regInQuery = regInQuery.Where(x => x.BranchId == branchId.Value);
            regOutQuery = regOutQuery.Where(x => x.BranchId == branchId.Value);
        }
        else if (!IsHO() && cu.BranchId.HasValue)
        {
            curInQuery = curInQuery.Where(x => x.BranchId == cu.BranchId.Value);
            curOutQuery = curOutQuery.Where(x => x.BranchId == cu.BranchId.Value);
            regInQuery = regInQuery.Where(x => x.BranchId == cu.BranchId.Value);
            regOutQuery = regOutQuery.Where(x => x.BranchId == cu.BranchId.Value);
        }

        var cashIns = await curInQuery.OrderBy(x => x.TransactionDate).ToListAsync(cancellationToken);
        var cashOuts = await curOutQuery.OrderBy(x => x.TransactionDate).ToListAsync(cancellationToken);
        
        var registerCashIns = await regInQuery.OrderByDescending(x => x.TransactionDate).ThenByDescending(x => x.Id).ToListAsync(cancellationToken);
        var registerCashOuts = await regOutQuery.OrderByDescending(x => x.TransactionDate).ThenByDescending(x => x.Id).ToListAsync(cancellationToken);

        // Dynamic categories & receipt types for creation forms on unified page
        var receiptTypes = await db.CashMasterItems
            .Where(x => x.ItemType == "ReceiptType" && x.IsActive && !x.IsDeleted)
            .OrderBy(x => x.Name)
            .Select(x => x.Name)
            .ToListAsync(cancellationToken);
            
        var expenseCategories = await db.CashMasterItems
            .Where(x => x.ItemType == "ExpenseCategory" && x.IsActive && !x.IsDeleted)
            .OrderBy(x => x.Name)
            .Select(x => x.Name)
            .ToListAsync(cancellationToken);

        // Load active parties for dynamic customer selections
        var parties = await db.Parties
            .Where(x => !x.IsDeleted && x.Status == "Active")
            .OrderBy(x => x.PartyCode)
            .ToListAsync(cancellationToken);

        // 3. Build rows side-by-side grouped by date
        var rows = new List<CashBookRow>();
        decimal totalDebit = 0;
        decimal totalCredit = 0;

        // Insert opening balance as the first row
        rows.Add(new CashBookRow
        {
            Date = startDate.ToString("d/MMM"),
            DebitParticulars = "Op. Balance",
            DebitAmount = openingBalance
        });

        // Group transactions by date
        var allDates = cashIns.Select(x => x.TransactionDate.Date)
            .Union(cashOuts.Select(x => x.TransactionDate.Date))
            .OrderBy(d => d)
            .ToList();

        foreach (var date in allDates)
        {
            var dayIns = cashIns.Where(x => x.TransactionDate.Date == date).ToList();
            var dayOuts = cashOuts.Where(x => x.TransactionDate.Date == date).ToList();

            int maxCount = Math.Max(dayIns.Count, dayOuts.Count);
            for (int i = 0; i < maxCount; i++)
            {
                var row = new CashBookRow
                {
                    Date = i == 0 ? date.ToString("d/MMM") : ""
                };

                if (i < dayIns.Count)
                {
                    var ci = dayIns[i];
                    row.DebitParticulars = ci.ReceiptType == "Cash Collection" ? $"{ci.CustomerName} (Collection)" : $"{ci.ReceiptType} - {ci.CustomerName}";
                    if (!string.IsNullOrEmpty(ci.DealerCode))
                        row.DebitParticulars += $" ({ci.DealerCode})";
                    row.DebitAmount = ci.Amount;
                    totalDebit += ci.Amount;
                }

                if (i < dayOuts.Count)
                {
                    var co = dayOuts[i];
                    row.CreditParticulars = string.IsNullOrEmpty(co.VendorName) ? co.ExpenseCategory : co.VendorName;
                    row.CreditAmount = co.Amount;
                    row.CreditComments = co.Narration;
                    totalCredit += co.Amount;
                }

                rows.Add(row);
            }
        }

        var cashBook = new CashBookViewModel
        {
            OpeningBalance   = openingBalance,
            Rows             = rows,
            TotalDebit       = totalDebit,
            TotalCredit      = totalCredit,
            ClosingBalance   = (openingBalance + totalDebit) - totalCredit,
            GrandTotalDebit  = openingBalance + totalDebit,
            GrandTotalCredit = totalCredit
        };

        cashBook.GrandTotalCredit = cashBook.TotalCredit + cashBook.ClosingBalance;

        return new CashBookViewModelContainer
        {
            Branches = branches,
            CashBook = cashBook,
            RegisterCashIns = registerCashIns,
            RegisterCashOuts = registerCashOuts,
            ReceiptTypes = receiptTypes,
            ExpenseCategories = expenseCategories,
            Parties = parties
        };
    }

    public async Task<List<CashPeriodControl>> GetPeriodControlsAsync(CancellationToken cancellationToken)
    {
        var periods = await db.CashPeriodControls.OrderByDescending(x => x.ControlYear).ThenByDescending(x => x.ControlMonth).ToListAsync(cancellationToken);

        // Ensure current month exists
        var now = DateTime.Now;
        if (!periods.Any(p => p.ControlYear == now.Year && p.ControlMonth == now.Month))
        {
            var cur = new CashPeriodControl { ControlYear = now.Year, ControlMonth = now.Month, Status = "Open", CreatedBy = "system" };
            db.CashPeriodControls.Add(cur);
            await db.SaveChangesAsync(cancellationToken);
            periods.Insert(0, cur);
        }

        return periods;
    }

    public async Task OpenNewPeriodAsync(int year, int month, string user, CancellationToken cancellationToken)
    {
        var exists = await db.CashPeriodControls.AnyAsync(x => x.ControlYear == year && x.ControlMonth == month, cancellationToken);
        if (exists) throw new InvalidOperationException($"Period {year}/{month:D2} already exists.");

        var p = new CashPeriodControl
        {
            ControlYear = year,
            ControlMonth = month,
            Status = "Open",
            CreatedBy = user,
            CreatedAt = DateTime.UtcNow
        };
        db.CashPeriodControls.Add(p);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ClosePeriodAsync(int year, int month, string user, CancellationToken cancellationToken)
    {
        var p = await db.CashPeriodControls.FirstOrDefaultAsync(x => x.ControlYear == year && x.ControlMonth == month, cancellationToken);
        if (p == null) throw new KeyNotFoundException("Period control not found.");
        p.Status   = "Closed";
        p.ClosedBy = user;
        p.ClosedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task LockPeriodAsync(int year, int month, CancellationToken cancellationToken)
    {
        var p = await db.CashPeriodControls.FirstOrDefaultAsync(x => x.ControlYear == year && x.ControlMonth == month, cancellationToken);
        if (p == null) throw new KeyNotFoundException("Period control not found.");
        p.Status = "Locked";
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<CashPeriodControl?> GetActivePeriod(CancellationToken cancellationToken = default)
    {
        var now = DateTime.Today;
        var currentPeriod = await db.CashPeriodControls
            .FirstOrDefaultAsync(x => !x.IsDeleted && x.ControlYear == now.Year && x.ControlMonth == now.Month, cancellationToken);
        
        if (currentPeriod == null)
        {
            currentPeriod = new CashPeriodControl { ControlYear = now.Year, ControlMonth = now.Month, Status = "Open", CreatedBy = "system" };
            db.CashPeriodControls.Add(currentPeriod);
            await db.SaveChangesAsync(cancellationToken);
        }
        
        if (currentPeriod.Status == "Open")
        {
            return currentPeriod;
        }
        
        return await db.CashPeriodControls
            .Where(x => !x.IsDeleted && x.Status == "Open")
            .OrderByDescending(x => x.ControlYear)
            .ThenByDescending(x => x.ControlMonth)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> IsCurrentPeriodLocked(CancellationToken cancellationToken = default)
    {
        var now = DateTime.Today;
        var period = await db.CashPeriodControls
            .FirstOrDefaultAsync(x => !x.IsDeleted && x.ControlYear == now.Year && x.ControlMonth == now.Month, cancellationToken);
        
        return period != null && (period.Status == "Locked" || period.Status == "Closed");
    }

    public async Task LockPeriod(int month, int year, string lockedBy, CancellationToken cancellationToken = default)
    {
        var p = await db.CashPeriodControls
            .FirstOrDefaultAsync(x => !x.IsDeleted && x.ControlYear == year && x.ControlMonth == month, cancellationToken);
        if (p == null)
        {
            p = new CashPeriodControl
            {
                ControlYear = year,
                ControlMonth = month,
                Status = "Locked",
                ClosedBy = lockedBy,
                ClosedAt = DateTime.UtcNow,
                CreatedBy = lockedBy,
                CreatedAt = DateTime.UtcNow
            };
            db.CashPeriodControls.Add(p);
        }
        else
        {
            p.Status = "Locked";
            p.ClosedBy = lockedBy;
            p.ClosedAt = DateTime.UtcNow;
            p.UpdatedBy = lockedBy;
            p.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<CashMastersViewModel> GetMastersAsync(CancellationToken cancellationToken)
    {
        var items = await db.CashMasterItems
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.ItemType)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);
            
        return new CashMastersViewModel
        {
            ReceiptTypes = items.Where(x => x.ItemType == "ReceiptType").ToList(),
            ExpenseCategories = items.Where(x => x.ItemType == "ExpenseCategory").ToList()
        };
    }

    public async Task SaveMasterItemAsync(int id, string itemType, string name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty.");

        if (id == 0)
        {
            var exists = await db.CashMasterItems
                .AnyAsync(x => x.ItemType == itemType && x.Name.Trim().ToLower() == name.Trim().ToLower() && !x.IsDeleted, cancellationToken);
            if (exists)
                throw new InvalidOperationException($"A master item of type '{itemType}' with name '{name}' already exists.");

            var item = new CashMasterItem
            {
                ItemType = itemType,
                Name = name.Trim(),
                IsActive = true
            };
            db.CashMasterItems.Add(item);
        }
        else
        {
            var item = await db.CashMasterItems.FindAsync(new object[] { id }, cancellationToken);
            if (item == null) throw new KeyNotFoundException("Master item not found.");

            var exists = await db.CashMasterItems
                .AnyAsync(x => x.Id != id && x.ItemType == itemType && x.Name.Trim().ToLower() == name.Trim().ToLower() && !x.IsDeleted, cancellationToken);
            if (exists)
                throw new InvalidOperationException($"Another item of type '{itemType}' with name '{name}' already exists.");

            item.Name = name.Trim();
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ToggleMasterItemActiveAsync(int id, CancellationToken cancellationToken)
    {
        var item = await db.CashMasterItems.FindAsync(new object[] { id }, cancellationToken);
        if (item == null) throw new KeyNotFoundException("Master item not found.");

        item.IsActive = !item.IsActive;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteMasterItemAsync(int id, CancellationToken cancellationToken)
    {
        var item = await db.CashMasterItems.FindAsync(new object[] { id }, cancellationToken);
        if (item == null) throw new KeyNotFoundException("Master item not found.");

        item.IsDeleted = true;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<(bool success, decimal totalSale, int totalInvoices, string branchCode, string message)> GetDmsSaleAsync(int branchId, DateTime date, CancellationToken cancellationToken)
    {
        try
        {
            var targetBranchId = branchId;
            if (!IsHO() && cu.BranchId.HasValue)
            {
                targetBranchId = cu.BranchId.Value;
            }

            var branch = await db.Branches.FirstOrDefaultAsync(b => b.Id == targetBranchId && !b.IsDeleted, cancellationToken);
            if (branch == null)
            {
                return (false, 0, 0, "", "Branch not found.");
            }

            string branchCode = branch.Code;
            string queryBranchCode = string.IsNullOrWhiteSpace(branch.TallyOutletCode)
                ? (branch.Code switch
                {
                    "VBZ" => "01",
                    "RQL" => "02",
                    "BGI" => "03",
                    "GRL" => "04",
                    "BSE" => "05",
                    "PSS" => "06",
                    "NBT" => "07",
                    "PPH" => "08",
                    "JPD" => "09",
                    "ISN" => "10",
                    "DUS" => "11",
                    "JGT" => "12",
                    "UTD" => "13",
                    "SGN" => "14",
                    "HMR" => "15",
                    "ALW" => "16",
                    "BWI" => "17",
                    "STO" => "18",
                    "SKR" => "19",
                    "JNU" => "20",
                    "SDH" => "21",
                    "HDN" => "22",
                    "TNG" => "23",
                    "PKT" => "24",
                    "JSK" => "25",
                    "LQU" => "26",
                    "OR7" => "27",
                    "HUH" => "28",
                    "SKF" => "29",
                    "F33" => "30",
                    "BER" => "31",
                    "KNO" => "32",
                    "SJG" => "33",
                    "CR9" => "34",
                    "SGH" => "35",
                    _ => branch.Id.ToString("D2")
                })
                : branch.TallyOutletCode;

            string connectionString = configuration.GetConnectionString("TallyDbConnection") 
                ?? "Server=172.20.25.3,1433;Database=tallydatabase;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

            decimal totalSaleAmount = 0;
            int totalInvoices = 0;

            using (var conn = new Microsoft.Data.SqlClient.SqlConnection(connectionString))
            {
                await conn.OpenAsync(cancellationToken);
                
                string query = @"
                    WITH TaxSummary AS
                    (
                        SELECT
                            UTD,
                            SUM(CASE WHEN CHARGE_CD LIKE 'CGS%' THEN CHARGE_AMT ELSE 0 END) AS CGST,
                            SUM(CASE WHEN CHARGE_CD LIKE 'SGS%' THEN CHARGE_AMT ELSE 0 END) AS SGST,
                            SUM(CASE WHEN CHARGE_CD LIKE 'IGS%' THEN CHARGE_AMT ELSE 0 END) AS IGST
                        FROM gd_fdi_trans_charges
                        GROUP BY UTD
                    )

                    SELECT
                        COUNT(DISTINCT T.TRANS_ID) AS TotalInvoices,
                        ROUND(SUM(
                            T.TAXABLE_VALUE 
                            + ISNULL(TX.CGST, 0) 
                            + ISNULL(TX.SGST, 0) 
                            + ISNULL(TX.IGST, 0)
                        ), 0) AS TotalInvoiceAmount
                    FROM gd_fdi_trans T
                    LEFT JOIN TaxSummary TX
                        ON T.UTD = TX.UTD
                    WHERE T.TRANS_TYPE = 'CI'
                      AND T.OUTLET_CD = @BranchCode
                      AND CAST(T.TRANS_DATE AS DATE) BETWEEN @FromDate AND @ToDate
                      AND T.PAYMENT_MODE IS NOT NULL AND T.PAYMENT_MODE <> '';";

                using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@BranchCode", queryBranchCode);
                    cmd.Parameters.AddWithValue("@FromDate", date.Date);
                    cmd.Parameters.AddWithValue("@ToDate", date.Date);

                    using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
                    {
                        if (await reader.ReadAsync(cancellationToken))
                        {
                            totalInvoices = reader["TotalInvoices"] == DBNull.Value ? 0 : Convert.ToInt32(reader["TotalInvoices"]);
                            totalSaleAmount = reader["TotalInvoiceAmount"] == DBNull.Value ? 0 : Math.Round(Convert.ToDecimal(reader["TotalInvoiceAmount"]), 0, MidpointRounding.AwayFromZero);
                        }
                    }
                }
            }

            return (true, totalSaleAmount, totalInvoices, branchCode, "Success");
        }
        catch (Exception ex)
        {
            return (false, 0, 0, "", "Error querying Tally database: " + ex.Message);
        }
    }

    public async Task<List<CostCenterCash>> GetCostCenterCashListAsync(int? month, int? year, CancellationToken cancellationToken)
    {
        int targetMonth = month ?? DateTime.UtcNow.Month;
        int targetYear = year ?? DateTime.UtcNow.Year;

        return await db.CostCenterCashes
            .Where(x => x.Year == targetYear && x.Month == targetMonth && !x.IsDeleted)
            .OrderByDescending(x => x.ClosingBalance)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<string>> SyncCostCenterCashAsync(int? month, int? year, CancellationToken cancellationToken)
    {
        var logs = new List<string>();
        var tallyUrl = configuration["Tally:Url"] ?? "http://localhost:9000";
        logs.Add($"[Tally Cash Sync] Connecting to Tally ERP 9 at {tallyUrl} ...");

        int targetMonth = month ?? DateTime.UtcNow.Month;
        int targetYear = year ?? DateTime.UtcNow.Year;

        var periodStart = new DateTime(targetYear, targetMonth, 1);
        var periodEnd = periodStart.AddMonths(1).AddDays(-1);
        
        // Calculate the Financial Year start (April 1st)
        int fyYear = targetMonth >= 4 ? targetYear : targetYear - 1;
        var fyStart = new DateTime(fyYear, 4, 1);
        
        var openStart = fyStart;
        var openEnd = periodStart.AddDays(-1);
        bool hasOpeningPeriod = openEnd >= openStart;

        logs.Add($"[Tally Cash Sync] Target Period: {periodStart:MMMM yyyy}");
        logs.Add($"[Tally Cash Sync] FY Start: {fyStart:dd-MMM-yyyy}");

        var openBalances = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        if (hasOpeningPeriod)
        {
            logs.Add($"[Tally Cash Sync] Fetching opening balances ({openStart:dd-MMM-yyyy} to {openEnd:dd-MMM-yyyy})...");
            try
            {
                var openXml = await FetchCostCenterBreakupXmlAsync(tallyUrl, openStart, openEnd, cancellationToken);
                openBalances = ParseCostCenterBalances(openXml);
                logs.Add($"[Tally Cash Sync] Parsed opening balances for {openBalances.Count} cost centers.");
            }
            catch (Exception ex)
            {
                logs.Add($"[Tally Cash Sync] ERROR fetching opening balances: {ex.Message}");
                throw;
            }
        }
        else
        {
            logs.Add("[Tally Cash Sync] Period starts on FY start; cumulative opening balance is 0.");
        }

        logs.Add($"[Tally Cash Sync] Fetching period transactions ({periodStart:dd-MMM-yyyy} to {periodEnd:dd-MMM-yyyy})...");
        var periodTransactions = new Dictionary<string, (decimal debit, decimal credit, decimal balance)>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var periodXml = await FetchCostCenterBreakupXmlAsync(tallyUrl, periodStart, periodEnd, cancellationToken);
            periodTransactions = ParseCostCenterTransactions(periodXml);
            logs.Add($"[Tally Cash Sync] Parsed transactions for {periodTransactions.Count} cost centers.");
        }
        catch (Exception ex)
        {
            logs.Add($"[Tally Cash Sync] ERROR fetching transactions: {ex.Message}");
            throw;
        }

        // Clear existing records
        var existing = await db.CostCenterCashes
            .Where(x => x.Year == targetYear && x.Month == targetMonth && !x.IsDeleted)
            .ToListAsync(cancellationToken);
        foreach (var item in existing)
        {
            item.IsDeleted = true;
            item.UpdatedAt = DateTime.UtcNow;
            item.UpdatedBy = "tally_sync";
        }
        logs.Add($"[Tally Cash Sync] Cleared {existing.Count} existing local database records.");

        var allCostCenters = openBalances.Keys.Union(periodTransactions.Keys).OrderBy(x => x).ToList();
        int savedCount = 0;
        foreach (var ccName in allCostCenters)
        {
            // Tally balance is negative for Debit (Asset / Cash). Make it positive representation.
            decimal rawOpenBal = openBalances.TryGetValue(ccName, out var oVal) ? oVal : 0m;
            decimal openingBal = -rawOpenBal; 

            var tx = periodTransactions.TryGetValue(ccName, out var tVal) ? tVal : (debit: 0m, credit: 0m, balance: 0m);
            decimal debit = tx.debit;
            decimal credit = tx.credit;
            decimal closingBal = openingBal + debit - credit;

            if (openingBal != 0m || debit != 0m || credit != 0m || closingBal != 0m)
            {
                db.CostCenterCashes.Add(new CostCenterCash
                {
                    Year = targetYear,
                    Month = targetMonth,
                    CostCenterName = ccName,
                    OpeningBalance = openingBal,
                    Debit = debit,
                    Credit = credit,
                    ClosingBalance = closingBal,
                    SyncedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "tally_sync"
                });
                savedCount++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        logs.Add($"[Tally Cash Sync COMPLETE] Saved {savedCount} cost center cash records.");
        return logs;
    }

    private async Task<string> FetchCostCenterBreakupXmlAsync(string tallyUrl, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken)
    {
        var fromStr = fromDate.ToString("yyyyMMdd");
        var toStr = toDate.ToString("yyyyMMdd");

        string xmlRequest = $"""
<ENVELOPE>
  <HEADER>
    <VERSION>1</VERSION>
    <TALLYREQUEST>Export</TALLYREQUEST>
    <TYPE>Data</TYPE>
    <ID>Cost Centre Break-up</ID>
  </HEADER>
  <BODY>
    <DESC>
      <STATICVARIABLES>
        <SVEXPORTFORMAT>$$SysName:XML</SVEXPORTFORMAT>
        <SVFROMDATE>{fromStr}</SVFROMDATE>
        <SVTODATE>{toStr}</SVTODATE>
        <GROUP>Cash in Hand</GROUP>
      </STATICVARIABLES>
    </DESC>
  </BODY>
</ENVELOPE>
""";

        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(120);
        var content = new StringContent(xmlRequest, Encoding.UTF8, "text/xml");
        var response = await client.PostAsync(tallyUrl, content, cancellationToken);
        response.EnsureSuccessStatusCode();
        var xmlData = await response.Content.ReadAsStringAsync(cancellationToken);
        
        return System.Text.RegularExpressions.Regex.Replace(xmlData, @"&#[0-9]+;", "");
    }

    private Dictionary<string, decimal> ParseCostCenterBalances(string xml)
    {
        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var doc = XDocument.Parse("<ROOT>" + xml + "</ROOT>");
            var ccNodes = doc.Descendants().Where(e => e.Name.LocalName.Equals("CCBCCNAME", StringComparison.OrdinalIgnoreCase));
            foreach (var cc in ccNodes)
            {
                var name = cc.Attribute("NAME")?.Value?.Trim();
                var balNode = cc.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("CCBCCBAL", StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(name) && balNode != null)
                {
                    if (decimal.TryParse(balNode.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var bal))
                    {
                        result[name] = bal;
                    }
                }
            }
        }
        catch (Exception)
        {
            // Suppress parse errors and return empty
        }
        return result;
    }

    private Dictionary<string, (decimal debit, decimal credit, decimal balance)> ParseCostCenterTransactions(string xml)
    {
        var result = new Dictionary<string, (decimal debit, decimal credit, decimal balance)>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var doc = XDocument.Parse("<ROOT>" + xml + "</ROOT>");
            var ccNodes = doc.Descendants().Where(e => e.Name.LocalName.Equals("CCBCCNAME", StringComparison.OrdinalIgnoreCase));
            foreach (var cc in ccNodes)
            {
                var name = cc.Attribute("NAME")?.Value?.Trim();
                var drNode = cc.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("CCBCCDR", StringComparison.OrdinalIgnoreCase));
                var crNode = cc.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("CCBCCCR", StringComparison.OrdinalIgnoreCase));
                var balNode = cc.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("CCBCCBAL", StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(name))
                {
                    decimal dr = 0;
                    decimal cr = 0;
                    decimal bal = 0;

                    if (drNode != null) decimal.TryParse(drNode.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out dr);
                    if (crNode != null) decimal.TryParse(crNode.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out cr);
                    if (balNode != null) decimal.TryParse(balNode.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out bal);

                    result[name] = (dr, cr, bal);
                }
            }
        }
        catch (Exception)
        {
            // Suppress parse errors and return empty
        }
        return result;
    }
}

