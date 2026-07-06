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
using IncentivePortal.Models;

namespace IncentivePortal.Services;

/// <summary>
/// Service interface for synchronizing ledgers and outstanding balances with Tally ERP 9 systems.
/// </summary>
public interface ITallyIntegrationService
{
    Task SyncLedgersAsync(CancellationToken cancellationToken = default);
    Task<List<string>> SyncOutstandingAsync(int? month = null, int? year = null, CancellationToken cancellationToken = default);
    Task ImportVouchersAsync(Stream tallyXml, CancellationToken cancellationToken = default);
}

public class TallyOutstandingDetails
{
    public decimal Total { get; set; }
    public decimal Less7 { get; set; }
    public decimal Days7To14 { get; set; }
    public decimal Days14To21 { get; set; }
    public decimal Days21To28 { get; set; }
    public decimal Days28To35 { get; set; }
    public decimal Days35To50 { get; set; }
    public decimal Days50To80 { get; set; }
    public decimal More80 { get; set; }
}

/// <summary>
/// Fetches outstanding balances from Tally ERP 9 Release 6.x using the built-in
/// "Group Outstandings" report (Display → More Ledger → Statement of Account → Outstanding).
/// XML response format: DSPDISPNAME (party name), DSPCLDRAMTA (Dr/receivable), DSPCLCRAMTA (Cr/advance).
/// Net Outstanding = |DSPCLDRAMTA| - DSPCLCRAMTA
/// </summary>
public sealed class TallyIntegrationService(
    IncentiveDbContext db,
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory) : ITallyIntegrationService
{
    public Task SyncLedgersAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public async Task<List<string>> SyncOutstandingAsync(int? month = null, int? year = null, CancellationToken cancellationToken = default)
    {
        var logs = new List<string>();
        var tallyUrl = configuration["Tally:Url"] ?? "http://localhost:9000";
        logs.Add($"[Tally Sync] Connecting to Tally ERP 9 at {tallyUrl} ...");

        // ── Resolve target period ──────────────────────────────────────────────
        int targetMonth = month ?? DateTime.UtcNow.Month;
        int targetYear  = year  ?? DateTime.UtcNow.Year;

        if (!month.HasValue || !year.HasValue)
        {
            var latestPeriod = await db.IncentivePeriods
                .AsNoTracking()
                .OrderByDescending(p => p.Year)
                .ThenByDescending(p => p.Month)
                .FirstOrDefaultAsync(cancellationToken);

            if (latestPeriod != null)
            {
                targetMonth = latestPeriod.Month;
                targetYear  = latestPeriod.Year;
            }
        }

        var monthLabel = new DateTime(targetYear, targetMonth, 1).ToString("MMMM yyyy");
        // Period end date = last day of that month
        var periodEnd   = new DateTime(targetYear, targetMonth, DateTime.DaysInMonth(targetYear, targetMonth));
        var periodStart = new DateTime(targetYear, 1, 1); // FY start — Tally needs full-year context
        logs.Add($"[Tally Sync] Target period: {monthLabel} (as-of {periodEnd:dd-MMM-yyyy})");

        // ── Load parties ───────────────────────────────────────────────────────
        var parties = await db.Parties
            .AsNoTracking()
            .Where(p => !p.IsDeleted)
            .ToListAsync(cancellationToken);

        if (parties.Count == 0)
        {
            logs.Add("[Tally Sync] No active parties found in database.");
            return logs;
        }
        logs.Add($"[Tally Sync] Found {parties.Count} active parties to sync.");

        // ── Step 1: Fetch Group Outstandings XML from Tally ───────────────────
        // Dictionary<PartyCode or Name, NetOutstanding>
        var tallyData = new Dictionary<string, TallyOutstandingDetails>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var xmlRequest = BuildBillsReceivableRequest(periodStart, periodEnd);
            using var httpClient = httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(120); // Bills Receivable can be large

            var content = new StringContent(xmlRequest, Encoding.UTF8, "application/xml");
            logs.Add($"[Tally Sync] Sending 'Bills Receivable' request (individual dealer bills)...");

            var response = await httpClient.PostAsync(tallyUrl, content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logs.Add($"[Tally Sync] ERROR: Tally returned HTTP {(int)response.StatusCode}. Sync aborted.");
                return logs;
            }

            var responseXml = await response.Content.ReadAsStringAsync(cancellationToken);
            logs.Add($"[Tally Sync] Received {responseXml.Length:N0} bytes from Tally. Parsing bills...");

            tallyData = ParseBillsReceivableXml(responseXml, periodEnd, logs);
            logs.Add($"[Tally Sync] Aggregated outstanding for {tallyData.Count} unique parties from Tally bills.");
        }
        catch (TaskCanceledException)
        {
            logs.Add($"[Tally Sync] ERROR: Connection to Tally timed out at {tallyUrl}. Make sure Tally is open with a company loaded.");
            return logs;
        }
        catch (HttpRequestException ex)
        {
            logs.Add($"[Tally Sync] ERROR: Cannot connect to Tally. {ex.Message}");
            return logs;
        }
        catch (Exception ex)
        {
            logs.Add($"[Tally Sync] ERROR: {ex.Message}");
            return logs;
        }

        if (tallyData.Count == 0)
        {
            logs.Add("[Tally Sync] WARNING: No bills parsed. Check that Tally company is open and Bills Receivable has data.");
            return logs;
        }

        // ── Step 2: Wipe old records & insert only Tally-matched ones ─────────
        // Soft-delete ALL existing outstanding records for this period first
        var existingAll = await db.DealerOutstandings
            .Where(o => o.Year == targetYear && o.Month == targetMonth && !o.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var old in existingAll)
        {
            old.IsDeleted  = true;
            old.UpdatedAt  = DateTime.UtcNow;
            old.UpdatedBy  = "tally_sync";
        }
        logs.Add($"[Tally Sync] Cleared {existingAll.Count} existing outstanding records for {monthLabel}.");

        var trackedIncentives = await db.SsIncentives
            .Where(s => s.Year == targetYear && s.Month == targetMonth && !s.IsDeleted)
            .ToDictionaryAsync(s => s.PartyCode, s => s, cancellationToken);

        int matched = 0, skipped = 0;

        foreach (var party in parties)
        {
            var details = TryMatchDetails(party, tallyData);

            // ── Skip parties NOT found in Tally ───────────────────────────────
            if (details == null)
            {
                skipped++;

                // If this party was previously "Credit Party" due to old outstanding, revert to Pending
                if (trackedIncentives.TryGetValue(party.PartyCode, out var inv) &&
                    inv.PaymentStatus == "Credit Party")
                {
                    inv.PaymentStatus = "Pending";
                    inv.UpdatedAt     = DateTime.UtcNow;
                    logs.Add($"[Status]    {party.PartyCode} | Credit Party → Pending (not in Tally)");
                }
                continue; // do NOT write to DealerOutstandings
            }

            var outstanding = details.Total;

            // ── Only store if outstanding > 0 ─────────────────────────────────
            if (outstanding <= 0)
            {
                skipped++;
                if (trackedIncentives.TryGetValue(party.PartyCode, out var inv2) &&
                    inv2.PaymentStatus == "Credit Party")
                {
                    inv2.PaymentStatus = "Pending";
                    logs.Add($"[Status]    {party.PartyCode} | Credit Party → Pending (zero balance in Tally)");
                }
                continue;
            }

            logs.Add($"[Matched]   {party.PartyCode} | '{party.PartyName}' → ₹{outstanding:N0} (<7d: ₹{details.Less7:N0}, 7-14d: ₹{details.Days7To14:N0}, 14-21d: ₹{details.Days14To21:N0}, 21-28d: ₹{details.Days21To28:N0}, 28-35d: ₹{details.Days28To35:N0}, 35-50d: ₹{details.Days35To50:N0}, 50-80d: ₹{details.Days50To80:N0}, >80d: ₹{details.More80:N0})");
            matched++;

            // Insert fresh record (old ones were soft-deleted above)
            db.DealerOutstandings.Add(new DealerOutstanding
            {
                Month       = targetMonth,
                Year        = targetYear,
                MonthLabel  = monthLabel,
                PartyCode   = party.PartyCode,
                PartyName   = party.PartyName,
                Outstanding = outstanding,
                OutstandingLess7Days = details.Less7,
                Outstanding7To14Days = details.Days7To14,
                Outstanding14To21Days = details.Days14To21,
                Outstanding21To28Days = details.Days21To28,
                Outstanding28To35Days = details.Days28To35,
                Outstanding35To50Days = details.Days35To50,
                Outstanding50To80Days = details.Days50To80,
                OutstandingMore80Days = details.More80,
                SyncedAt    = DateTime.UtcNow,
                CreatedAt   = DateTime.UtcNow,
                CreatedBy   = "tally_sync"
            });

            // Sync PaymentStatus → Credit Party if outstanding > 0
            if (trackedIncentives.TryGetValue(party.PartyCode, out var incentive))
            {
                var oldStatus = incentive.PaymentStatus;
                if (incentive.PaymentStatus is "Pending" or "Failed" or "Reversed")
                    incentive.PaymentStatus = "Credit Party";

                if (incentive.PaymentStatus != oldStatus)
                    logs.Add($"[Status]    {party.PartyCode} | {oldStatus} → {incentive.PaymentStatus}");
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        logs.Add("─────────────────────────────────────────────────");
        logs.Add($"[Tally Sync COMPLETE]  Period  : {monthLabel}");
        logs.Add($"  Tally bills fetched   : {tallyData.Count} unique parties");
        logs.Add($"  Matched & saved       : {matched} dealers");
        logs.Add($"  Skipped (not in Tally): {skipped} dealers");
        logs.Add("─────────────────────────────────────────────────");

        return logs;
    }

    public Task ImportVouchersAsync(Stream tallyXml, CancellationToken cancellationToken = default) => Task.CompletedTask;

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds the "Bills Receivable" HTTP request for Tally ERP 9 Release 6.x.
    /// This is the same data shown in: Display → Account Books → Bills Receivable → Pending Bills
    /// Response gives bill-level data with BILLPARTY (name+code) and BILLCL (pending amount).
    /// </summary>
    private static string BuildBillsReceivableRequest(DateTime fromDate, DateTime toDate)
    {
        var from = fromDate.ToString("yyyyMMdd");
        var to   = toDate.ToString("yyyyMMdd");

        return $"""
<ENVELOPE>
  <HEADER>
    <TALLYREQUEST>Export Data</TALLYREQUEST>
  </HEADER>
  <BODY>
    <EXPORTDATA>
      <REQUESTDESC>
        <REPORTNAME>Bills Receivable</REPORTNAME>
        <STATICVARIABLES>
          <SVEXPORTFORMAT>$$SysName:XML</SVEXPORTFORMAT>
          <SVFROMDATE>{from}</SVFROMDATE>
          <SVTODATE>{to}</SVTODATE>
        </STATICVARIABLES>
      </REQUESTDESC>
    </EXPORTDATA>
  </BODY>
</ENVELOPE>
""";
    }

    /// <summary>
    /// Parses Tally "Bills Receivable" XML response.
    /// Each BILLFIXED block has BILLPARTY like "JAIPURIA AUTOMOBILES(WRJ050172026)".
    /// BILLCL is the pending amount (negative = receivable).
    /// We extract the party code from parentheses and sum all bills per party code.
    /// Also calculates outstanding aging buckets based on the bill date.
    /// </summary>
    private static Dictionary<string, TallyOutstandingDetails> ParseBillsReceivableXml(string xml, DateTime periodEnd, List<string> logs)
    {
        // Key: party code (extracted from parentheses)
        var byCode = new Dictionary<string, TallyOutstandingDetails>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var doc = XDocument.Parse("<ROOT>" + xml + "</ROOT>");

            // Get all BILLFIXED elements (each represents one bill entry)
            var billBlocks = doc.Descendants()
                .Where(e => e.Name.LocalName.Equals("BILLFIXED", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var block in billBlocks)
            {
                var partyRaw = block.Descendants()
                    .FirstOrDefault(e => e.Name.LocalName.Equals("BILLPARTY", StringComparison.OrdinalIgnoreCase))
                    ?.Value?.Trim();

                if (string.IsNullOrWhiteSpace(partyRaw)) continue;

                // Get BILLCL (sibling of BILLFIXED, not child)
                var billClNode = block.ElementsAfterSelf()
                    .FirstOrDefault(e => e.Name.LocalName.Equals("BILLCL", StringComparison.OrdinalIgnoreCase));
                if (billClNode == null) continue;

                var clStr = billClNode.Value?.Trim() ?? "0";
                decimal.TryParse(clStr, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var clAmt);

                // BILLCL is negative for receivable (e.g. -3829.00 = we're owed ₹3829)
                var pendingAmt = Math.Abs(clAmt);

                // Extract party code from parentheses: "NAME(CODE)" → "CODE"
                string? partyCode = null;
                var parenStart = partyRaw.LastIndexOf('(');
                var parenEnd   = partyRaw.LastIndexOf(')');
                if (parenStart >= 0 && parenEnd > parenStart)
                {
                    partyCode = partyRaw.Substring(parenStart + 1, parenEnd - parenStart - 1).Trim();
                }

                if (string.IsNullOrWhiteSpace(partyCode)) continue;

                // Parse bill date inside the BILLFIXED block
                var billDateStr = block.Descendants()
                    .FirstOrDefault(e => e.Name.LocalName.Equals("BILLDATE", StringComparison.OrdinalIgnoreCase))
                    ?.Value?.Trim();

                DateTime? billDate = null;
                if (!string.IsNullOrEmpty(billDateStr))
                {
                    if (DateTime.TryParse(billDateStr, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var dt))
                    {
                        billDate = dt;
                    }
                    else
                    {
                        var formats = new[] { "d-MMM-yy", "dd-MMM-yy", "d-MMM-yyyy", "dd-MMM-yyyy", "yyyyMMdd" };
                        if (DateTime.TryParseExact(billDateStr, formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var dtExact))
                        {
                            billDate = dtExact;
                        }
                    }
                }

                int days = 0;
                if (billDate.HasValue)
                {
                    days = (periodEnd.Date - billDate.Value.Date).Days;
                    if (days < 0) days = 0;
                }

                if (!byCode.TryGetValue(partyCode, out var details))
                {
                    details = new TallyOutstandingDetails();
                    byCode[partyCode] = details;
                }

                details.Total += pendingAmt;

                if (days < 7) details.Less7 += pendingAmt;
                else if (days < 14) details.Days7To14 += pendingAmt;
                else if (days < 21) details.Days14To21 += pendingAmt;
                else if (days < 28) details.Days21To28 += pendingAmt;
                else if (days < 35) details.Days28To35 += pendingAmt;
                else if (days < 50) details.Days35To50 += pendingAmt;
                else if (days < 80) details.Days50To80 += pendingAmt;
                else details.More80 += pendingAmt;
            }

            logs.Add($"[Tally Sync] Bills parsed: {billBlocks.Count} entries → {byCode.Count} unique party codes.");
        }
        catch (Exception ex)
        {
            logs.Add($"[Tally Sync] XML parse error: {ex.Message}");
        }

        return byCode;
    }

    /// <summary>
    /// Matches our DB party to a Tally bill record details.
    /// ONLY uses exact party code match — the code extracted from parentheses in BILLPARTY.
    /// </summary>
    private static TallyOutstandingDetails? TryMatchDetails(Party party, Dictionary<string, TallyOutstandingDetails> tallyData)
    {
        if (!string.IsNullOrWhiteSpace(party.PartyCode) &&
            tallyData.TryGetValue(party.PartyCode.Trim(), out var details))
            return details;

        return null;
    }
}
