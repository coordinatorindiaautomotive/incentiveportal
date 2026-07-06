using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using ClosedXML.Excel;
using IncentivePortal.Data;
using IncentivePortal.Models;
using IncentivePortal.Services;

namespace IncentivePortal.Services;

public sealed class ConfirmPayoutDto
{
    public int Month { get; set; }
    public int Year { get; set; }
    public string PartyCode { get; set; } = string.Empty;
    public string PartyName { get; set; } = string.Empty;
    public decimal SaleValue { get; set; }
    public decimal SlabPercent { get; set; }
    public decimal OnBillDiscount { get; set; }
    public decimal AchievementPercent { get; set; }
    public decimal GrossIncentive { get; set; }
    public decimal TdsAmount { get; set; }
    public decimal NetTransferAmount { get; set; }
    public decimal TransferredAmount { get; set; }
    public string PaymentStatus { get; set; } = "Pending";
    public string? UTRNumber { get; set; }
    public DateTime? PaymentDate { get; set; }
    public string BankAccountNumber { get; set; } = string.Empty;
    public string IFSC { get; set; } = string.Empty;
    public string BeneficiaryName { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Remarks { get; set; }
}

public interface ITransferService
{
    Task<(int DefaultMonth, int DefaultYear, List<SsIncentive> Incentives)> GetIndexDataAsync(CancellationToken cancellationToken);
    Task ReconcileAsync(int id, string utr, CancellationToken cancellationToken);
    Task BulkReconcileAsync(List<int> ids, string utr, CancellationToken cancellationToken);
    Task<object> UploadReconciliationAsync(
        IFormFile file,
        int? reconcileMonth,
        int? reconcileYear,
        int? mapAccountNo,
        int? mapStatus,
        int? mapUtr,
        int? mapPartyCode,
        int? mapName,
        int? mapIfsc,
        int? mapAmount,
        int? mapDate,
        string username,
        CancellationToken cancellationToken);
    Task<List<object>> ParseHeadersAsync(IFormFile file, CancellationToken cancellationToken);
    Task<string> ConfirmAutoCreatedPayoutsAsync(List<ConfirmPayoutDto> models, string username, CancellationToken cancellationToken);
    Task<List<BankStatementRecord>> GetUnmatchedRecordsAsync(int month, int year, CancellationToken cancellationToken);
    Task SaveUnmatchedRecordAsync(BankStatementRecord model, string username, CancellationToken cancellationToken);
    Task DeleteUnmatchedRecordAsync(int id, string username, CancellationToken cancellationToken);
    Task CreateUnmatchedRecordAsync(BankStatementRecord model, string username, CancellationToken cancellationToken);
    Task<List<object>> GetPartiesListAsync(CancellationToken cancellationToken);
    Task ReconcileUnmatchedRecordAsync(int id, string partyCode, string username, CancellationToken cancellationToken);
}

public sealed class TransferService(IncentiveDbContext db, IIncentiveCalculationService calculationService) : ITransferService
{
    public async Task<(int DefaultMonth, int DefaultYear, List<SsIncentive> Incentives)> GetIndexDataAsync(CancellationToken cancellationToken)
    {
        var oldestPending = await db.SsIncentives
            .Where(s => !s.IsDeleted && s.PaymentStatus == "Pending")
            .OrderBy(s => s.Year)
            .ThenBy(s => s.Month)
            .Select(s => new { s.Year, s.Month })
            .FirstOrDefaultAsync(cancellationToken);

        int defaultMonth;
        int defaultYear;

        if (oldestPending != null)
        {
            defaultMonth = oldestPending.Month;
            defaultYear = oldestPending.Year;
        }
        else
        {
            defaultMonth = DateTime.Today.Month;
            defaultYear = DateTime.Today.Year;
        }

        var incentives = await db.SsIncentives.Where(s => !s.IsDeleted).ToListAsync(cancellationToken);
        return (defaultMonth, defaultYear, incentives);
    }

    public async Task ReconcileAsync(int id, string utr, CancellationToken cancellationToken)
    {
        var ssIncentive = await db.SsIncentives.FirstAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
        if (ssIncentive.PaymentStatus == "Paid" || ssIncentive.PaymentStatus == "Success" || ssIncentive.PaymentStatus == "Reconciled")
        {
            throw new InvalidOperationException("This transfer is already reconciled and locked.");
        }

        if (string.IsNullOrWhiteSpace(ssIncentive.BankAccountNumber) || ssIncentive.BankAccountNumber == "-" || string.IsNullOrWhiteSpace(ssIncentive.IFSC) || ssIncentive.IFSC == "-")
        {
            throw new InvalidOperationException("Cannot reconcile manually because the dealer's bank account details are not linked.");
        }

        ssIncentive.UTRNumber = string.IsNullOrWhiteSpace(utr) ? null : utr;
        ssIncentive.PaymentStatus = "Paid";
        ssIncentive.PaymentDate = DateTime.UtcNow;
        ssIncentive.TransferredAmount = ssIncentive.NetTransferAmount;
        
        db.Entry(ssIncentive).State = EntityState.Modified;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task BulkReconcileAsync(List<int> ids, string utr, CancellationToken cancellationToken)
    {
        if (ids == null || ids.Count == 0)
        {
            throw new ArgumentException("No transfers selected for reconciliation.");
        }

        var incentives = await db.SsIncentives
            .Where(x => ids.Contains(x.Id) && !x.IsDeleted)
            .ToListAsync(cancellationToken);

        var pendingIncentives = incentives.Where(x => x.PaymentStatus == "Pending" || x.PaymentStatus == "Failed" || x.PaymentStatus == "Reversed" || x.PaymentStatus == "Credit Party").ToList();
        if (pendingIncentives.Count == 0)
        {
            throw new InvalidOperationException("None of the selected transfers are in a pending state.");
        }

        var missingBankDetails = pendingIncentives.Where(x => string.IsNullOrWhiteSpace(x.BankAccountNumber) || x.BankAccountNumber == "-" || string.IsNullOrWhiteSpace(x.IFSC) || x.IFSC == "-").ToList();
        if (missingBankDetails.Count > 0)
        {
            var names = string.Join(", ", missingBankDetails.Select(x => x.PartyName));
            throw new InvalidOperationException($"Cannot bulk reconcile because the following dealers do not have linked bank account details: {names}.");
        }

        foreach (var item in pendingIncentives)
        {
            item.UTRNumber = string.IsNullOrWhiteSpace(utr) ? null : utr;
            item.PaymentStatus = "Paid";
            item.PaymentDate = DateTime.UtcNow;
            item.TransferredAmount = item.NetTransferAmount;
            db.Entry(item).State = EntityState.Modified;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<object> UploadReconciliationAsync(
        IFormFile file,
        int? reconcileMonth,
        int? reconcileYear,
        int? mapAccountNo,
        int? mapStatus,
        int? mapUtr,
        int? mapPartyCode,
        int? mapName,
        int? mapIfsc,
        int? mapAmount,
        int? mapDate,
        string username,
        CancellationToken cancellationToken)
    {
        int targetMonth;
        int targetYear;

        if (reconcileMonth.HasValue && reconcileYear.HasValue)
        {
            targetMonth = reconcileMonth.Value;
            targetYear = reconcileYear.Value;
        }
        else
        {
            var nameMatch = Regex.Match(file.FileName, @"(?<month>Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[a-z]*[\s']*(?<year>\d{2,4})", RegexOptions.IgnoreCase);
            if (nameMatch.Success)
            {
                targetMonth = DateTime.ParseExact(nameMatch.Groups["month"].Value[..3], "MMM", CultureInfo.InvariantCulture).Month;
                var yearText = nameMatch.Groups["year"].Value;
                targetYear = int.Parse(yearText, CultureInfo.InvariantCulture);
                targetYear = targetYear < 100 ? 2000 + targetYear : targetYear;
            }
            else
            {
                var oldestPending = await db.SsIncentives
                    .Where(s => !s.IsDeleted && s.PaymentStatus == "Pending")
                    .OrderBy(s => s.Year)
                    .ThenBy(s => s.Month)
                    .Select(s => new { s.Year, s.Month })
                    .FirstOrDefaultAsync(cancellationToken);

                if (oldestPending != null)
                {
                    targetMonth = oldestPending.Month;
                    targetYear = oldestPending.Year;
                }
                else
                {
                    targetMonth = DateTime.Today.Month;
                    targetYear = DateTime.Today.Year;
                }
            }
        }

        try
        {
            await calculationService.CalculateMonthAsync(targetMonth, targetYear, false, cancellationToken: cancellationToken);
        }
        catch { }

        var bankImportLog = await db.ImportLogs.FirstOrDefaultAsync(x => x.ImportType == "BankStatementReconciliation" && x.Month == targetMonth && x.Year == targetYear, cancellationToken);
        if (bankImportLog == null)
        {
            bankImportLog = new ImportLog
            {
                ImportType = "BankStatementReconciliation",
                FileName = file.FileName,
                TotalRows = 0,
                SuccessRows = 0,
                FailedRows = 0,
                Status = "Completed",
                ErrorJson = "[]",
                Year = targetYear,
                Month = targetMonth,
                VersionNumber = 1,
                ChangeReason = "Auto-created during bank statement reconciliation"
            };
            db.ImportLogs.Add(bankImportLog);
        }
        else
        {
            bankImportLog.FileName = file.FileName;
            bankImportLog.UpdatedAt = DateTime.UtcNow;
            bankImportLog.UpdatedBy = username;
            db.Entry(bankImportLog).State = EntityState.Modified;
        }
        await db.SaveChangesAsync(cancellationToken);

        using var stream = file.OpenReadStream();
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheets.FirstOrDefault() ?? workbook.Worksheets.First();

        int nameCol = mapName ?? 5;
        int accountNoCol = mapAccountNo ?? 6;
        int ifscCol = mapIfsc ?? 7;
        int amountCol = mapAmount ?? 8;
        int partyCodeCol = mapPartyCode ?? 9;
        int dateCol = mapDate ?? 14;
        int statusCol = mapStatus ?? 15;
        int utrCol = mapUtr ?? 22;

        var headerRow = sheet.Row(1);
        int lastCol = sheet.LastColumnUsed()?.ColumnNumber() ?? 30;

        if (!mapAccountNo.HasValue && !mapStatus.HasValue && !mapUtr.HasValue)
        {
            for (int col = 1; col <= lastCol; col++)
            {
                var headerText = headerRow.Cell(col).Value.ToString().Trim();
                if (string.IsNullOrEmpty(headerText)) continue;

                if (headerText.Equals("STATUS", StringComparison.OrdinalIgnoreCase))
                    statusCol = col;
                else if (headerText.Contains("UTR", StringComparison.OrdinalIgnoreCase))
                    utrCol = col;
                else if (headerText.Contains("Debit narration", StringComparison.OrdinalIgnoreCase))
                    partyCodeCol = col;
                else if (headerText.Equals("Beneficiary Name", StringComparison.OrdinalIgnoreCase))
                    nameCol = col;
                else if (headerText.Equals("Beneficiary Account No", StringComparison.OrdinalIgnoreCase) || headerText.Equals("Account No", StringComparison.OrdinalIgnoreCase))
                    accountNoCol = col;
                else if (headerText.Contains("IFSC", StringComparison.OrdinalIgnoreCase))
                    ifscCol = col;
                else if (headerText.Equals("Amount", StringComparison.OrdinalIgnoreCase))
                    amountCol = col;
                else if (headerText.Equals("Pymt_Date", StringComparison.OrdinalIgnoreCase) || headerText.Equals("Payment Date", StringComparison.OrdinalIgnoreCase))
                    dateCol = col;
            }
        }

        var headers = new List<string>();
        for (int col = 1; col <= lastCol; col++)
        {
            var val = headerRow.Cell(col).Value.ToString().Trim();
            headers.Add(string.IsNullOrEmpty(val) ? $"Column_{col}" : val);
        }

        var oldRecords = await db.BankStatementRecords
            .Where(r => r.Month == targetMonth && r.Year == targetYear)
            .ToListAsync(cancellationToken);
        if (oldRecords.Count > 0)
        {
            db.BankStatementRecords.RemoveRange(oldRecords);
        }

        var transfers = await db.SsIncentives
            .Where(x => !x.IsDeleted && (x.PaymentStatus == "Pending" || x.PaymentStatus == "Failed" || x.PaymentStatus == "Reversed" || x.PaymentStatus == "Credit Party"))
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Month)
            .ToListAsync(cancellationToken);

        var parties = await db.Parties.Include(p => p.Branch).AsNoTracking().ToListAsync(cancellationToken);
        var bankDetails = await db.BankDetails.Include(b => b.Party).AsNoTracking().Where(b => b.ApprovalStatus == "Approved").ToListAsync(cancellationToken);

        int reconciledCount = 0;
        int failedCount = 0;
        var logs = new List<string>();
        var autoCreatedList = new List<SsIncentive>();

        var rows = sheet.RowsUsed().Skip(1).ToList();
        foreach (var row in rows)
        {
            var beneficiaryName = row.Cell(nameCol).Value.ToString().Trim();
            var accountNo = row.Cell(accountNoCol).Value.ToString().Trim().TrimStart('0');
            var beneficiaryIfsc = row.Cell(ifscCol).Value.ToString().Trim();

            var amtStr = row.Cell(amountCol).Value.ToString().Replace(",", "").Trim();
            decimal rowAmount = 0m;
            decimal.TryParse(amtStr, out rowAmount);

            var partyCodeFromExcel = row.Cell(partyCodeCol).Value.ToString().Trim();
            var dateStr = row.Cell(dateCol).Value.ToString().Trim();
            var status = row.Cell(statusCol).Value.ToString().Trim();
            var utr = row.Cell(utrCol).Value.ToString().Trim();

            DateTime? paymentDate = null;
            if (!string.IsNullOrEmpty(dateStr))
            {
                var formats = new[] { "dd-MM-yyyy", "dd/MM/yyyy", "d-M-yyyy", "d/M/yyyy", "dd-MMM-yyyy", "dd/MMM/yyyy", "yyyy-MM-dd" };
                if (DateTime.TryParseExact(dateStr, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                    paymentDate = dt;
                else if (DateTime.TryParse(dateStr, CultureInfo.GetCultureInfo("en-IN"), DateTimeStyles.None, out dt))
                    paymentDate = dt;
                else if (DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                    paymentDate = dt;
            }

            if (string.IsNullOrEmpty(beneficiaryName) && string.IsNullOrEmpty(accountNo) && rowAmount == 0)
                continue;

            var rowData = new Dictionary<string, string>();
            for (int col = 1; col <= lastCol; col++)
            {
                rowData[headers[col - 1]] = row.Cell(col).Value.ToString().Trim();
            }
            var rawJson = JsonSerializer.Serialize(rowData);

            var bankRecord = new BankStatementRecord
            {
                Month = targetMonth,
                Year = targetYear,
                FileName = file.FileName,
                RowNumber = row.RowNumber(),
                BeneficiaryName = beneficiaryName,
                AccountNumber = accountNo,
                IFSC = beneficiaryIfsc,
                Amount = rowAmount,
                Status = string.IsNullOrEmpty(status) ? "Success" : status,
                UTR = utr,
                PaymentDate = paymentDate,
                IsReconciled = false,
                PartyCode = string.IsNullOrWhiteSpace(partyCodeFromExcel) ? null : partyCodeFromExcel,
                RawRowJson = rawJson,
                ImportLogId = bankImportLog.Id,
                CreatedBy = username,
                CreatedAt = DateTime.UtcNow
            };
            db.BankStatementRecords.Add(bankRecord);

            if (string.IsNullOrEmpty(status))
                status = "Success";

            Party? matchedParty = null;
            if (!string.IsNullOrEmpty(partyCodeFromExcel))
            {
                matchedParty = parties.FirstOrDefault(p =>
                    partyCodeFromExcel.Equals(p.PartyCode, StringComparison.OrdinalIgnoreCase) ||
                    partyCodeFromExcel.Contains(p.PartyCode, StringComparison.OrdinalIgnoreCase) ||
                    p.PartyCode.Equals(partyCodeFromExcel, StringComparison.OrdinalIgnoreCase));
            }

            SsIncentive? matchedTransfer = null;
            if (matchedParty != null)
            {
                matchedTransfer = transfers.FirstOrDefault(t => t.PartyCode.Equals(matchedParty.PartyCode, StringComparison.OrdinalIgnoreCase)
                    && t.Month == targetMonth
                    && t.Year == targetYear);
            }

            if (matchedTransfer == null && !string.IsNullOrEmpty(accountNo))
            {
                var cleanAccountNo = accountNo.Trim().TrimStart('0');
                matchedTransfer = transfers.FirstOrDefault(t =>
                    !string.IsNullOrEmpty(t.BankAccountNumber) &&
                    t.BankAccountNumber.Trim().TrimStart('0') == cleanAccountNo &&
                    t.Month == targetMonth &&
                    t.Year == targetYear);

                if (matchedTransfer != null)
                {
                    matchedParty = parties.FirstOrDefault(p => p.PartyCode.Equals(matchedTransfer.PartyCode, StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    var matchedBank = bankDetails.FirstOrDefault(b => b.AccountNumber.Trim().TrimStart('0') == cleanAccountNo);
                    if (matchedBank != null)
                    {
                        matchedTransfer = transfers.FirstOrDefault(t => t.PartyCode.Equals(matchedBank.Party.PartyCode, StringComparison.OrdinalIgnoreCase)
                            && t.Month == targetMonth
                            && t.Year == targetYear);

                        if (matchedParty == null)
                            matchedParty = parties.FirstOrDefault(p => p.Id == matchedBank.PartyId);
                    }
                }
            }

            if (matchedTransfer == null && !string.IsNullOrEmpty(beneficiaryName) && rowAmount > 0)
            {
                var cleanExcelName = CleanName(beneficiaryName);
                if (!string.IsNullOrEmpty(cleanExcelName))
                {
                    var matchedBank = bankDetails.FirstOrDefault(b => {
                        var cleanHolder = CleanName(b.AccountHolder);
                        return !string.IsNullOrEmpty(cleanHolder) &&
                               (cleanExcelName.Contains(cleanHolder) || cleanHolder.Contains(cleanExcelName));
                    });

                    if (matchedBank != null)
                    {
                        matchedTransfer = transfers.FirstOrDefault(t =>
                            t.PartyCode.Equals(matchedBank.Party.PartyCode, StringComparison.OrdinalIgnoreCase)
                            && t.Month == targetMonth
                            && t.Year == targetYear
                            && Math.Abs(t.NetTransferAmount - rowAmount) <= 10m);

                        if (matchedTransfer != null && matchedParty == null)
                            matchedParty = parties.FirstOrDefault(p => p.Id == matchedBank.PartyId);
                    }

                    if (matchedTransfer == null)
                    {
                        var matchedP = parties.FirstOrDefault(p => {
                            var cleanPartyName = CleanName(p.PartyName);
                            return !string.IsNullOrEmpty(cleanPartyName) &&
                                   (cleanExcelName.Contains(cleanPartyName) || cleanPartyName.Contains(cleanExcelName));
                        });

                        if (matchedP != null)
                        {
                            matchedTransfer = transfers.FirstOrDefault(t =>
                                t.PartyCode.Equals(matchedP.PartyCode, StringComparison.OrdinalIgnoreCase)
                                && t.Month == targetMonth
                                && t.Year == targetYear
                                && Math.Abs(t.NetTransferAmount - rowAmount) <= 10m);

                            if (matchedTransfer != null && matchedParty == null)
                                matchedParty = matchedP;
                        }
                    }
                }
            }

            var normStatus = NormalizeStatus(status);

            if (matchedTransfer != null)
            {
                matchedTransfer.UTRNumber = string.IsNullOrWhiteSpace(utr) ? null : utr;
                matchedTransfer.PaymentStatus = normStatus;
                matchedTransfer.PaymentDate = paymentDate ?? DateTime.UtcNow;

                matchedTransfer.BankAccountNumber = !string.IsNullOrWhiteSpace(accountNo) ? accountNo : matchedTransfer.BankAccountNumber;
                matchedTransfer.IFSC = !string.IsNullOrWhiteSpace(beneficiaryIfsc) ? beneficiaryIfsc : matchedTransfer.IFSC;
                matchedTransfer.BeneficiaryName = !string.IsNullOrWhiteSpace(beneficiaryName) ? beneficiaryName : matchedTransfer.BeneficiaryName;

                if (rowAmount > 0)
                    matchedTransfer.NetTransferAmount = rowAmount;
                matchedTransfer.TransferredAmount = (normStatus == "Paid") ? (rowAmount > 0 ? rowAmount : matchedTransfer.NetTransferAmount) : 0;

                db.Entry(matchedTransfer).State = EntityState.Modified;
                reconciledCount++;
                logs.Add($"Reconciled Party: {matchedTransfer.PartyCode} | UTR: {utr} | Status: {status}");
                transfers.Remove(matchedTransfer);

                bankRecord.IsReconciled = true;
                bankRecord.PartyCode = matchedTransfer.PartyCode;
            }
            else if (matchedParty != null)
            {
                var ssIncentive = await db.SsIncentives.FirstOrDefaultAsync(s =>
                    s.PartyCode == matchedParty.PartyCode &&
                    s.Month == targetMonth &&
                    s.Year == targetYear &&
                    !s.IsDeleted, cancellationToken);

                if (ssIncentive != null)
                {
                    if (ssIncentive.PaymentStatus == "Paid" || ssIncentive.PaymentStatus == "Success" || ssIncentive.PaymentStatus == "Reconciled")
                    {
                        logs.Add($"Party: {matchedParty.PartyCode} | Already Reconciled & Locked (Skipped)");
                        bankRecord.IsReconciled = true;
                        bankRecord.PartyCode = matchedParty.PartyCode;
                        continue;
                    }

                    ssIncentive.PaymentStatus = normStatus;
                    ssIncentive.UTRNumber = utr;
                    ssIncentive.PaymentDate = paymentDate ?? DateTime.UtcNow;

                    ssIncentive.BankAccountNumber = !string.IsNullOrWhiteSpace(accountNo) ? accountNo : ssIncentive.BankAccountNumber;
                    ssIncentive.IFSC = !string.IsNullOrWhiteSpace(beneficiaryIfsc) ? beneficiaryIfsc : ssIncentive.IFSC;
                    ssIncentive.BeneficiaryName = !string.IsNullOrWhiteSpace(beneficiaryName) ? beneficiaryName : ssIncentive.BeneficiaryName;

                    if (rowAmount > 0)
                        ssIncentive.NetTransferAmount = rowAmount;
                    ssIncentive.TransferredAmount = (normStatus == "Paid") ? (rowAmount > 0 ? rowAmount : ssIncentive.NetTransferAmount) : 0;
                    ssIncentive.TdsAmount = Math.Max(0, ssIncentive.GrossIncentive - ssIncentive.NetTransferAmount);
                    ssIncentive.Remarks = "Auto-Updated via Bank Reconciliation fallback";
                    db.Entry(ssIncentive).State = EntityState.Modified;

                    reconciledCount++;
                    logs.Add($"Reconciled Party: {matchedParty.PartyCode} | Amount: ₹{rowAmount:N0} | UTR: {utr} | Status: {status}");
                    bankRecord.IsReconciled = true;
                    bankRecord.PartyCode = matchedParty.PartyCode;
                }
                else
                {
                    var proposed = new SsIncentive
                    {
                        Month = targetMonth,
                        Year = targetYear,
                        MonthLabel = new DateTime(targetYear, targetMonth, 1).ToString("MMMM yyyy"),
                        PartyCode = matchedParty.PartyCode,
                        PartyName = matchedParty.PartyName,
                        SaleValue = 0,
                        SlabPercent = 0,
                        OnBillDiscount = 0,
                        AchievementPercent = 0,
                        GrossIncentive = rowAmount,
                        TdsAmount = 0,
                        NetTransferAmount = rowAmount,
                        TransferredAmount = (normStatus == "Paid") ? rowAmount : 0,
                        ProcessingDate = DateTime.UtcNow,
                        PaymentDate = paymentDate,
                        PaymentStatus = normStatus,
                        UTRNumber = utr,
                        BankAccountNumber = !string.IsNullOrWhiteSpace(accountNo) ? accountNo : "-",
                        IFSC = !string.IsNullOrWhiteSpace(beneficiaryIfsc) ? beneficiaryIfsc : "-",
                        BeneficiaryName = !string.IsNullOrWhiteSpace(beneficiaryName) ? beneficiaryName : matchedParty.PartyName,
                        Mode = "Dynamic",
                        Status = "Posted",
                        IsEdited = false,
                        Remarks = "Auto-Created via Bank Reconciliation"
                    };
                    autoCreatedList.Add(proposed);
                    logs.Add($"Proposed Auto-Created Payout for Party: {matchedParty.PartyCode} | Amount: ₹{rowAmount:N0} | UTR: {utr} | Status: {status}");
                    bankRecord.PartyCode = matchedParty.PartyCode;
                    bankRecord.IsReconciled = false;
                }
            }
            else
            {
                failedCount++;
                logs.Add($"Unmatched row {row.RowNumber()} | Party Code: {partyCodeFromExcel} | Account: {accountNo}");
            }
        }

        bankImportLog.TotalRows = rows.Count;
        bankImportLog.SuccessRows = reconciledCount;
        bankImportLog.FailedRows = failedCount;
        db.Entry(bankImportLog).State = EntityState.Modified;

        await db.SaveChangesAsync(cancellationToken);

        return new
        {
            ok = true,
            message = $"Successfully processed reconciliation. Reconciled: {reconciledCount}, Unmatched: {failedCount}.",
            logs = logs,
            autoCreated = autoCreatedList
        };
    }

    public async Task<List<object>> ParseHeadersAsync(IFormFile file, CancellationToken cancellationToken)
    {
        using var stream = file.OpenReadStream();
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheets.FirstOrDefault() ?? workbook.Worksheets.First();
        var headerRow = sheet.Row(1);
        int lastCol = sheet.LastColumnUsed()?.ColumnNumber() ?? 30;

        var headers = new List<object>();
        for (int col = 1; col <= lastCol; col++)
        {
            var val = headerRow.Cell(col).Value.ToString().Trim();
            if (!string.IsNullOrEmpty(val))
            {
                headers.Add(new { index = col, name = val });
            }
        }
        return headers;
    }

    public async Task<string> ConfirmAutoCreatedPayoutsAsync(List<ConfirmPayoutDto> models, string username, CancellationToken cancellationToken)
    {
        int createdCount = 0;
        int updatedCount = 0;

        var uniqueModels = models
            .GroupBy(x => new { x.Year, x.Month, x.PartyCode })
            .Select(g => g.First())
            .ToList();

        foreach (var item in uniqueModels)
        {
            var existing = await db.SsIncentives.FirstOrDefaultAsync(x =>
                x.Year == item.Year &&
                x.Month == item.Month &&
                x.PartyCode == item.PartyCode &&
                !x.IsDeleted, cancellationToken);

            if (existing != null)
            {
                existing.UTRNumber = item.UTRNumber;
                existing.PaymentStatus = item.PaymentStatus;
                existing.PaymentDate = item.PaymentDate;
                existing.NetTransferAmount = item.NetTransferAmount;
                existing.TransferredAmount = item.TransferredAmount;

                existing.BankAccountNumber = !string.IsNullOrWhiteSpace(item.BankAccountNumber) ? item.BankAccountNumber : existing.BankAccountNumber;
                existing.IFSC = !string.IsNullOrWhiteSpace(item.IFSC) ? item.IFSC : existing.IFSC;
                existing.BeneficiaryName = !string.IsNullOrWhiteSpace(item.BeneficiaryName) ? item.BeneficiaryName : existing.BeneficiaryName;

                existing.Remarks = "Auto-Updated via Bank Reconciliation Confirmation";
                existing.UpdatedBy = username;
                existing.UpdatedAt = DateTime.UtcNow;
                db.Entry(existing).State = EntityState.Modified;
                updatedCount++;
            }
            else
            {
                var newIncentive = new SsIncentive
                {
                    Month = item.Month,
                    Year = item.Year,
                    MonthLabel = new DateTime(item.Year, item.Month, 1).ToString("MMMM yyyy"),
                    PartyCode = item.PartyCode,
                    PartyName = item.PartyName,
                    SaleValue = item.SaleValue,
                    SlabPercent = item.SlabPercent,
                    OnBillDiscount = item.OnBillDiscount,
                    AchievementPercent = item.AchievementPercent,
                    GrossIncentive = item.GrossIncentive,
                    TdsAmount = item.TdsAmount,
                    NetTransferAmount = item.NetTransferAmount,
                    TransferredAmount = item.TransferredAmount,
                    Outstanding = 0m,
                    PaymentStatus = item.PaymentStatus,
                    UTRNumber = item.UTRNumber,
                    PaymentDate = item.PaymentDate,
                    BankAccountNumber = !string.IsNullOrWhiteSpace(item.BankAccountNumber) ? item.BankAccountNumber : "-",
                    IFSC = !string.IsNullOrWhiteSpace(item.IFSC) ? item.IFSC : "-",
                    BeneficiaryName = !string.IsNullOrWhiteSpace(item.BeneficiaryName) ? item.BeneficiaryName : item.PartyName,
                    Mode = string.IsNullOrWhiteSpace(item.Mode) ? "Dynamic" : item.Mode,
                    Status = string.IsNullOrWhiteSpace(item.Status) ? "Posted" : item.Status,
                    PartCategoryCode = "",
                    SourceLocation = "",
                    IsEdited = false,
                    Remarks = string.IsNullOrWhiteSpace(item.Remarks) ? "Auto-Created via Bank Reconciliation" : item.Remarks,
                    CreatedBy = username,
                    CreatedAt = DateTime.UtcNow,
                    ProcessingDate = DateTime.UtcNow
                };
                db.SsIncentives.Add(newIncentive);
                createdCount++;
            }

            var statementRecord = await db.BankStatementRecords.FirstOrDefaultAsync(x =>
                x.Year == item.Year &&
                x.Month == item.Month &&
                x.PartyCode == item.PartyCode &&
                !x.IsReconciled &&
                !x.IsDeleted, cancellationToken);
            if (statementRecord != null)
            {
                statementRecord.IsReconciled = true;
                statementRecord.UpdatedBy = username;
                statementRecord.UpdatedAt = DateTime.UtcNow;
                db.Entry(statementRecord).State = EntityState.Modified;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return $"Successfully processed payout records. Created: {createdCount}, Updated: {updatedCount}.";
    }

    public async Task<List<BankStatementRecord>> GetUnmatchedRecordsAsync(int month, int year, CancellationToken cancellationToken)
    {
        return await db.BankStatementRecords
            .Where(r => r.Month == month && r.Year == year && !r.IsReconciled && !r.IsDeleted)
            .OrderBy(r => r.RowNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveUnmatchedRecordAsync(BankStatementRecord model, string username, CancellationToken cancellationToken)
    {
        var record = await db.BankStatementRecords.FirstOrDefaultAsync(r => r.Id == model.Id && !r.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("Statement record not found.");

        record.BeneficiaryName = model.BeneficiaryName;
        record.AccountNumber = model.AccountNumber?.TrimStart('0');
        record.IFSC = model.IFSC;
        record.Amount = model.Amount;
        record.Status = model.Status;
        record.UTR = model.UTR;
        record.PaymentDate = model.PaymentDate;
        record.PartyCode = model.PartyCode;
        record.UpdatedBy = username;
        record.UpdatedAt = DateTime.UtcNow;

        db.Entry(record).State = EntityState.Modified;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteUnmatchedRecordAsync(int id, string username, CancellationToken cancellationToken)
    {
        var record = await db.BankStatementRecords.FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("Statement record not found.");

        record.IsDeleted = true;
        record.UpdatedBy = username;
        record.UpdatedAt = DateTime.UtcNow;

        db.Entry(record).State = EntityState.Modified;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task CreateUnmatchedRecordAsync(BankStatementRecord model, string username, CancellationToken cancellationToken)
    {
        if (model.Month == 0 || model.Year == 0)
        {
            throw new ArgumentException("Month and Year are required.");
        }

        var nextRowNumber = 1;
        var maxRow = await db.BankStatementRecords
            .Where(r => r.Month == model.Month && r.Year == model.Year)
            .Select(r => (int?)r.RowNumber)
            .ToListAsync(cancellationToken);
        if (maxRow.Count > 0)
        {
            nextRowNumber = (maxRow.Max() ?? 0) + 1;
        }

        var record = new BankStatementRecord
        {
            Month = model.Month,
            Year = model.Year,
            FileName = "Manually Created",
            RowNumber = nextRowNumber,
            BeneficiaryName = model.BeneficiaryName,
            AccountNumber = model.AccountNumber?.TrimStart('0'),
            IFSC = model.IFSC,
            Amount = model.Amount,
            Status = string.IsNullOrWhiteSpace(model.Status) ? "Failed" : model.Status,
            UTR = model.UTR,
            PaymentDate = model.PaymentDate,
            PartyCode = model.PartyCode,
            IsReconciled = false,
            RawRowJson = "{}",
            CreatedBy = username,
            CreatedAt = DateTime.UtcNow
        };

        db.BankStatementRecords.Add(record);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<object>> GetPartiesListAsync(CancellationToken cancellationToken)
    {
        return await db.Parties
            .Where(p => !p.IsDeleted && p.Status == "Active")
            .OrderBy(p => p.PartyCode)
            .Select(p => new { code = p.PartyCode, name = p.PartyName })
            .Cast<object>()
            .ToListAsync(cancellationToken);
    }

    public async Task ReconcileUnmatchedRecordAsync(int id, string partyCode, string username, CancellationToken cancellationToken)
    {
        var record = await db.BankStatementRecords.FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("Statement record not found.");

        var party = await db.Parties.FirstOrDefaultAsync(p => p.PartyCode == partyCode && !p.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("Selected dealer not found.");

        var existingPayout = await db.SsIncentives.FirstOrDefaultAsync(s =>
            s.PartyCode == partyCode &&
            s.Month == record.Month &&
            s.Year == record.Year &&
            !s.IsDeleted, cancellationToken);

        var normStatus = record.Status;
        if (string.IsNullOrEmpty(normStatus) || string.Equals(normStatus, "Success", StringComparison.OrdinalIgnoreCase))
            normStatus = "Paid";
        else if (string.Equals(normStatus, "Failed", StringComparison.OrdinalIgnoreCase))
            normStatus = "Failed";

        if (existingPayout != null)
        {
            if (existingPayout.PaymentStatus == "Paid" || existingPayout.PaymentStatus == "Success" || existingPayout.PaymentStatus == "Reconciled")
            {
                throw new InvalidOperationException("This dealer's payout is already reconciled and locked.");
            }

            existingPayout.UTRNumber = record.UTR;
            existingPayout.PaymentStatus = normStatus;
            existingPayout.PaymentDate = record.PaymentDate ?? DateTime.UtcNow;

            existingPayout.BankAccountNumber = !string.IsNullOrEmpty(record.AccountNumber) ? record.AccountNumber : existingPayout.BankAccountNumber;
            existingPayout.IFSC = !string.IsNullOrEmpty(record.IFSC) ? record.IFSC : existingPayout.IFSC;
            existingPayout.BeneficiaryName = !string.IsNullOrEmpty(record.BeneficiaryName) ? record.BeneficiaryName : existingPayout.BeneficiaryName;

            if (existingPayout.NetTransferAmount == 0 && record.Amount > 0)
            {
                existingPayout.NetTransferAmount = record.Amount;
            }
            existingPayout.TransferredAmount = (normStatus == "Paid") ? (record.Amount > 0 ? record.Amount : existingPayout.NetTransferAmount) : 0;
            existingPayout.TdsAmount = Math.Max(0, existingPayout.GrossIncentive - existingPayout.NetTransferAmount);
            existingPayout.Remarks = "Reconciled manually from unmatched statement entry";
            existingPayout.UpdatedBy = username;
            existingPayout.UpdatedAt = DateTime.UtcNow;

            db.Entry(existingPayout).State = EntityState.Modified;
        }
        else
        {
            var newPayout = new SsIncentive
            {
                Month = record.Month,
                Year = record.Year,
                MonthLabel = new DateTime(record.Year, record.Month, 1).ToString("MMMM yyyy"),
                PartyCode = party.PartyCode,
                PartyName = party.PartyName,
                SaleValue = 0,
                SlabPercent = 0,
                OnBillDiscount = 0,
                AchievementPercent = 0,
                GrossIncentive = record.Amount,
                TdsAmount = 0,
                NetTransferAmount = record.Amount,
                TransferredAmount = (normStatus == "Paid") ? record.Amount : 0,
                ProcessingDate = DateTime.UtcNow,
                PaymentDate = record.PaymentDate,
                PaymentStatus = normStatus,
                UTRNumber = record.UTR,
                BankAccountNumber = !string.IsNullOrEmpty(record.AccountNumber) ? record.AccountNumber : "-",
                IFSC = !string.IsNullOrEmpty(record.IFSC) ? record.IFSC : "-",
                BeneficiaryName = !string.IsNullOrEmpty(record.BeneficiaryName) ? record.BeneficiaryName : party.PartyName,
                Mode = "Dynamic",
                Status = "Posted",
                PartCategoryCode = "",
                SourceLocation = "",
                IsEdited = false,
                Remarks = "Manually Created & Reconciled from unmatched statement entry",
                CreatedBy = username,
                CreatedAt = DateTime.UtcNow
            };
            db.SsIncentives.Add(newPayout);
        }

        record.IsReconciled = true;
        record.PartyCode = partyCode;
        record.UpdatedBy = username;
        record.UpdatedAt = DateTime.UtcNow;
        db.Entry(record).State = EntityState.Modified;

        await db.SaveChangesAsync(cancellationToken);
    }

    private static string CleanName(string name)
    {
        if (string.IsNullOrEmpty(name)) return string.Empty;
        var clean = name.ToLowerInvariant();
        var wordsToRemove = new[] { "pvt", "ltd", "private", "limited", "motors", "motor", "automobile", "automobiles", "service", "services", "workshop", "work shop", "auto", "parts", "garage", "denting", "painting" };
        foreach (var word in wordsToRemove)
        {
            clean = Regex.Replace(clean, @"\b" + Regex.Escape(word) + @"\b", "");
        }
        clean = Regex.Replace(clean, @"[^a-z0-9]", "");
        return clean.Trim();
    }

    private static string NormalizeStatus(string excelStatus)
    {
        if (string.IsNullOrEmpty(excelStatus)) return "Paid";
        var lower = excelStatus.ToLowerInvariant();
        if (lower.Contains("fail") || lower.Contains("reject") || lower.Contains("return"))
            return "Failed";
        if (lower.Contains("revers"))
            return "Reversed";
        if (lower.Contains("pend"))
            return "Pending";
        if (lower.Contains("success") || lower.Contains("paid") || lower.Contains("reconcil") || lower.Contains("complete"))
            return "Paid";
        return excelStatus;
    }
}
