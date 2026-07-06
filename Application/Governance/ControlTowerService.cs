using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;
using ExcelDataReader;
using IncentivePortal.Data;
using IncentivePortal.Models;

namespace IncentivePortal.Services;

public sealed class RolePermissionInput
{
    public string RoleName { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public bool IsAllowed { get; set; }
}

public record PropertyDiff(string PropertyName, string OldValue, string NewValue);

public interface IControlTowerService
{
    Task<(
        List<PortalSetting> Settings, 
        List<TdsRule> TdsRules, 
        List<ColumnMappingRule> ColumnMappings, 
        List<OutstandingRule> OutstandingRules, 
        NotificationSetting? NotificationSetting, 
        string DecryptedSmtpPassword,
        TallyIntegrationSetting? TallySetting,
        string DecryptedTallyPassword,
        List<RolePermission> RolePermissions,
        List<AuditLog> AuditLogs,
        List<HelpText> HelpTexts,
        List<ReportColumnConfig> ReportColumnConfigs
    )> GetIndexDataAsync(CancellationToken cancellationToken);

    Task SavePortalSettingsAsync(Dictionary<string, string> settings, CancellationToken cancellationToken);
    Task SaveTdsRuleAsync(TdsRule rule, CancellationToken cancellationToken);
    Task DeleteTdsRuleAsync(int id, CancellationToken cancellationToken);
    Task SaveColumnMappingAsync(ColumnMappingRule mapping, CancellationToken cancellationToken);
    Task DeleteColumnMappingAsync(int id, CancellationToken cancellationToken);
    Task SaveOutstandingRuleAsync(OutstandingRule rule, CancellationToken cancellationToken);
    Task DeleteOutstandingRuleAsync(int id, CancellationToken cancellationToken);
    Task SaveNotificationSettingsAsync(NotificationSetting setting, string smtpPassword, CancellationToken cancellationToken);
    Task<bool> TestSmtpSettingsAsync(string host, int port, bool useSsl, string user, string password, string fromEmail, string fromName, string testEmail, CancellationToken cancellationToken);
    Task<bool> TestSmsSettingsAsync(string apiKey, string senderId, string testMobile, CancellationToken cancellationToken);
    Task SaveRolePermissionsAsync(List<RolePermissionInput> permissions, CancellationToken cancellationToken);
    Task SaveTallySettingsAsync(TallyIntegrationSetting setting, string tallyPassword, CancellationToken cancellationToken);
    Task<object> GetAuditLogDiffAsync(long id, CancellationToken cancellationToken);
    Task SaveHelpTextAsync(HelpText help, CancellationToken cancellationToken);
    Task SaveReportColumnConfigAsync(ReportColumnConfig config, CancellationToken cancellationToken);
    Task DeleteReportColumnConfigAsync(int id, CancellationToken cancellationToken);
    Task<(List<PartyCodeMapping> Mappings, int TotalPages)> GetPartyCodeMappingsAsync(string? search, int page, int pageSize, CancellationToken cancellationToken);
    Task SavePartyCodeMappingAsync(int id, string alternativeCode, string originalCode, string? notes, bool isActive, CancellationToken cancellationToken);
    Task DeletePartyCodeMappingAsync(int id, CancellationToken cancellationToken);
    Task<(int ImportedCount, int SkippedCount)> ImportPartyCodeMappingsAsync(IFormFile file, CancellationToken cancellationToken);
}

public sealed class ControlTowerService : IControlTowerService
{
    private readonly IncentiveDbContext _db;
    private readonly INotificationService _notificationService;
    private readonly IDataProtector _protector;
    private readonly IMemoryCache _cache;

    public ControlTowerService(
        IncentiveDbContext db,
        INotificationService notificationService,
        IDataProtectionProvider protectionProvider,
        IMemoryCache cache)
    {
        _db = db;
        _notificationService = notificationService;
        _protector = protectionProvider.CreateProtector("ControlTowerSettings");
        _cache = cache;
    }

    public async Task<(
        List<PortalSetting> Settings, 
        List<TdsRule> TdsRules, 
        List<ColumnMappingRule> ColumnMappings, 
        List<OutstandingRule> OutstandingRules, 
        NotificationSetting? NotificationSetting, 
        string DecryptedSmtpPassword,
        TallyIntegrationSetting? TallySetting,
        string DecryptedTallyPassword,
        List<RolePermission> RolePermissions,
        List<AuditLog> AuditLogs,
        List<HelpText> HelpTexts,
        List<ReportColumnConfig> ReportColumnConfigs
    )> GetIndexDataAsync(CancellationToken cancellationToken)
    {
        var settings = await _db.PortalSettings.Where(s => !s.IsDeleted).ToListAsync(cancellationToken);
        var tdsRules = await _db.TdsRules.Where(r => !r.IsDeleted).OrderByDescending(r => r.EffectiveFrom).ToListAsync(cancellationToken);
        var columnMappings = await _db.ColumnMappingRules.Where(m => !m.IsDeleted).OrderBy(m => m.UploadContext).ThenBy(m => m.ExcelHeader).ToListAsync(cancellationToken);
        var outstandingRules = await _db.OutstandingRules.Where(o => !o.IsDeleted).OrderBy(o => o.Priority).ToListAsync(cancellationToken);
        
        var notificationSetting = await _db.NotificationSettings.Where(n => !n.IsDeleted).FirstOrDefaultAsync(cancellationToken);
        var decryptedSmtpPassword = "";
        if (notificationSetting != null && !string.IsNullOrEmpty(notificationSetting.SmtpPassEncrypted))
        {
            try
            {
                decryptedSmtpPassword = _protector.Unprotect(notificationSetting.SmtpPassEncrypted);
            }
            catch { }
        }

        var tallySetting = await _db.TallyIntegrationSettings.Where(t => !t.IsDeleted).FirstOrDefaultAsync(cancellationToken);
        var decryptedTallyPassword = "";
        if (tallySetting != null && !string.IsNullOrEmpty(tallySetting.PasswordEncrypted))
        {
            try
            {
                decryptedTallyPassword = _protector.Unprotect(tallySetting.PasswordEncrypted);
            }
            catch { }
        }

        var rolePermissions = await _db.RolePermissions.Where(p => !p.IsDeleted).ToListAsync(cancellationToken);
        var auditLogs = await _db.AuditLogs.OrderByDescending(l => l.ChangedAt).Take(150).ToListAsync(cancellationToken);
        var helpTexts = await _db.HelpTexts.Where(h => !h.IsDeleted).ToListAsync(cancellationToken);
        var reportConfigs = await _db.ReportColumnConfigs.Where(c => !c.IsDeleted).OrderBy(c => c.ReportName).ThenBy(c => c.SortOrder).ToListAsync(cancellationToken);

        return (
            settings, tdsRules, columnMappings, outstandingRules, 
            notificationSetting, decryptedSmtpPassword,
            tallySetting, decryptedTallyPassword,
            rolePermissions, auditLogs, helpTexts, reportConfigs
        );
    }

    public async Task SavePortalSettingsAsync(Dictionary<string, string> settings, CancellationToken cancellationToken)
    {
        if (settings == null) return;
        foreach (var kvp in settings)
        {
            var key = kvp.Key.Trim();
            var val = kvp.Value?.Trim() ?? string.Empty;

            var existing = await _db.PortalSettings.FirstOrDefaultAsync(s => s.Key == key && !s.IsDeleted, cancellationToken);
            if (existing != null)
            {
                existing.Value = val;
            }
            else
            {
                _db.PortalSettings.Add(new PortalSetting { Key = key, Value = val });
            }
        }
        await _db.SaveChangesAsync(cancellationToken);
        _cache.Remove("layout_portal_settings"); // Clear Layout Cache
    }

    public async Task SaveTdsRuleAsync(TdsRule rule, CancellationToken cancellationToken)
    {
        if (rule.EffectiveFrom > rule.EffectiveTo)
            throw new ArgumentException("Effective From date cannot be after Effective To date.");

        var overlapExists = await _db.TdsRules.AnyAsync(r => 
            !r.IsDeleted && 
            r.Id != rule.Id && 
            r.Section == rule.Section &&
            r.EffectiveFrom <= rule.EffectiveTo && 
            r.EffectiveTo >= rule.EffectiveFrom, 
            cancellationToken);

        if (overlapExists)
            throw new ArgumentException($"A TDS rule under Section {rule.Section} already covers this date range.");

        if (rule.Id > 0)
        {
            var existing = await _db.TdsRules.FindAsync([rule.Id], cancellationToken);
            if (existing == null || existing.IsDeleted) throw new KeyNotFoundException("TDS rule not found.");
            
            existing.EffectiveFrom = rule.EffectiveFrom;
            existing.EffectiveTo = rule.EffectiveTo;
            existing.AnnualThreshold = rule.AnnualThreshold;
            existing.RateWithPan = rule.RateWithPan / 100m;
            existing.RateNoPan = rule.RateNoPan / 100m;
            existing.Section = rule.Section;
            existing.Notes = rule.Notes;
            existing.UpdatedAt = DateTime.UtcNow;
            _db.TdsRules.Update(existing);
        }
        else
        {
            rule.RateWithPan /= 100m;
            rule.RateNoPan /= 100m;
            _db.TdsRules.Add(rule);
        }
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteTdsRuleAsync(int id, CancellationToken cancellationToken)
    {
        var rule = await _db.TdsRules.FindAsync([id], cancellationToken);
        if (rule == null || rule.IsDeleted) throw new KeyNotFoundException("TDS rule not found.");
        
        rule.IsDeleted = true;
        rule.UpdatedAt = DateTime.UtcNow;
        _db.TdsRules.Update(rule);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveColumnMappingAsync(ColumnMappingRule mapping, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(mapping.ExcelHeader) || string.IsNullOrWhiteSpace(mapping.PortalField))
            throw new ArgumentException("Excel Header and Portal Field are required.");

        if (mapping.Id > 0)
        {
            var existing = await _db.ColumnMappingRules.FindAsync([mapping.Id], cancellationToken);
            if (existing == null || existing.IsDeleted) throw new KeyNotFoundException("Mapping rule not found.");
            
            existing.ExcelHeader = mapping.ExcelHeader.Trim();
            existing.PortalField = mapping.PortalField.Trim();
            existing.UploadContext = mapping.UploadContext.Trim();
            existing.IsActive = mapping.IsActive;
            existing.Notes = mapping.Notes;
            existing.UpdatedAt = DateTime.UtcNow;
            _db.ColumnMappingRules.Update(existing);
        }
        else
        {
            mapping.ExcelHeader = mapping.ExcelHeader.Trim();
            mapping.PortalField = mapping.PortalField.Trim();
            _db.ColumnMappingRules.Add(mapping);
        }
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteColumnMappingAsync(int id, CancellationToken cancellationToken)
    {
        var mapping = await _db.ColumnMappingRules.FindAsync([id], cancellationToken);
        if (mapping == null || mapping.IsDeleted) throw new KeyNotFoundException("Mapping rule not found.");
        
        mapping.IsDeleted = true;
        mapping.UpdatedAt = DateTime.UtcNow;
        _db.ColumnMappingRules.Update(mapping);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveOutstandingRuleAsync(OutstandingRule rule, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rule.Name))
            throw new ArgumentException("Rule Name is required.");

        if (rule.Id > 0)
        {
            var existing = await _db.OutstandingRules.FindAsync([rule.Id], cancellationToken);
            if (existing == null || existing.IsDeleted) throw new KeyNotFoundException("Outstanding rule not found.");
            
            existing.Name = rule.Name.Trim();
            existing.DeductionRate = rule.DeductionRate / 100m;
            existing.ThresholdAmount = rule.ThresholdAmount;
            existing.Description = rule.Description;
            existing.IsActive = rule.IsActive;
            existing.Priority = rule.Priority;
            existing.UpdatedAt = DateTime.UtcNow;
            _db.OutstandingRules.Update(existing);
        }
        else
        {
            rule.Name = rule.Name.Trim();
            rule.DeductionRate /= 100m;
            _db.OutstandingRules.Add(rule);
        }
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteOutstandingRuleAsync(int id, CancellationToken cancellationToken)
    {
        var rule = await _db.OutstandingRules.FindAsync([id], cancellationToken);
        if (rule == null || rule.IsDeleted) throw new KeyNotFoundException("Outstanding rule not found.");
        
        rule.IsDeleted = true;
        rule.UpdatedAt = DateTime.UtcNow;
        _db.OutstandingRules.Update(rule);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveNotificationSettingsAsync(NotificationSetting setting, string smtpPassword, CancellationToken cancellationToken)
    {
        var existing = await _db.NotificationSettings.FirstOrDefaultAsync(x => !x.IsDeleted, cancellationToken);
        if (existing == null)
        {
            setting.SmtpPassEncrypted = !string.IsNullOrEmpty(smtpPassword) ? _protector.Protect(smtpPassword) : "";
            _db.NotificationSettings.Add(setting);
        }
        else
        {
            existing.SmtpHost = setting.SmtpHost;
            existing.SmtpPort = setting.SmtpPort;
            existing.SmtpUseSsl = setting.SmtpUseSsl;
            existing.SmtpUser = setting.SmtpUser;
            existing.FromEmail = setting.FromEmail;
            existing.FromName = setting.FromName;
            existing.SmsApiKey = setting.SmsApiKey;
            existing.SmsSenderId = setting.SmsSenderId;
            existing.EmailEnabled = setting.EmailEnabled;
            existing.SmsEnabled = setting.SmsEnabled;
            existing.UpdatedAt = DateTime.UtcNow;

            if (!string.IsNullOrEmpty(smtpPassword))
            {
                existing.SmtpPassEncrypted = _protector.Protect(smtpPassword);
            }

            _db.NotificationSettings.Update(existing);
        }
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> TestSmtpSettingsAsync(string host, int port, bool useSsl, string user, string password, string fromEmail, string fromName, string testEmail, CancellationToken cancellationToken)
    {
        var tempSetting = new NotificationSetting
        {
            SmtpHost = host,
            SmtpPort = port,
            SmtpUseSsl = useSsl,
            SmtpUser = user,
            FromEmail = fromEmail,
            FromName = fromName
        };

        return await _notificationService.TestSmtpSettingsAsync(tempSetting, password, testEmail, cancellationToken);
    }

    public async Task<bool> TestSmsSettingsAsync(string apiKey, string senderId, string testMobile, CancellationToken cancellationToken)
    {
        var tempSetting = new NotificationSetting
        {
            SmsApiKey = apiKey,
            SmsSenderId = senderId
        };

        return await _notificationService.TestSmsSettingsAsync(tempSetting, testMobile, cancellationToken);
    }

    public async Task SaveRolePermissionsAsync(List<RolePermissionInput> permissions, CancellationToken cancellationToken)
    {
        var existing = await _db.RolePermissions.Where(p => !p.IsDeleted).ToListAsync(cancellationToken);
        foreach (var ext in existing)
        {
            ext.IsDeleted = true;
        }

        if (permissions != null)
        {
            foreach (var p in permissions)
            {
                if (string.IsNullOrEmpty(p.RoleName) || string.IsNullOrEmpty(p.Module) || string.IsNullOrEmpty(p.Action))
                    continue;

                _db.RolePermissions.Add(new RolePermission
                {
                    RoleName = p.RoleName,
                    Module = p.Module,
                    Action = p.Action,
                    IsAllowed = p.IsAllowed
                });
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        _cache.Set("roleperm_version", Guid.NewGuid().ToString());
    }

    public async Task SaveTallySettingsAsync(TallyIntegrationSetting setting, string tallyPassword, CancellationToken cancellationToken)
    {
        var existing = await _db.TallyIntegrationSettings.Where(t => !t.IsDeleted).FirstOrDefaultAsync(cancellationToken);
        if (existing == null)
        {
            existing = new TallyIntegrationSetting();
            _db.TallyIntegrationSettings.Add(existing);
        }

        existing.BaseUrl = setting.BaseUrl ?? "http://localhost:9000";
        existing.Port = setting.Port;
        existing.CompanyName = setting.CompanyName ?? string.Empty;
        existing.IsEnabled = setting.IsEnabled;
        existing.TimeoutSeconds = setting.TimeoutSeconds;
        existing.Username = setting.Username;

        if (!string.IsNullOrEmpty(tallyPassword))
        {
            existing.PasswordEncrypted = _protector.Protect(tallyPassword);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<object> GetAuditLogDiffAsync(long id, CancellationToken cancellationToken)
    {
        var log = await _db.AuditLogs.FindAsync([id], cancellationToken);
        if (log == null) throw new KeyNotFoundException("Audit log entry not found.");

        var oldDict = string.IsNullOrEmpty(log.OldValue) ? new Dictionary<string, object?>() : (JsonSerializer.Deserialize<Dictionary<string, object?>>(log.OldValue) ?? new Dictionary<string, object?>());
        var newDict = string.IsNullOrEmpty(log.NewValue) ? new Dictionary<string, object?>() : (JsonSerializer.Deserialize<Dictionary<string, object?>>(log.NewValue) ?? new Dictionary<string, object?>());

        var diff = new List<PropertyDiff>();
        var allKeys = oldDict.Keys.Union(newDict.Keys).ToList();

        foreach (var key in allKeys)
        {
            if (key.Equals("PasswordHash", StringComparison.OrdinalIgnoreCase) || key.Equals("PasswordSalt", StringComparison.OrdinalIgnoreCase) || key.Equals("SmtpPassEncrypted", StringComparison.OrdinalIgnoreCase) || key.Equals("PasswordEncrypted", StringComparison.OrdinalIgnoreCase))
            {
                continue; // Mask secure fields
            }

            oldDict.TryGetValue(key, out var oldVal);
            newDict.TryGetValue(key, out var newVal);

            var oldStr = oldVal?.ToString() ?? "";
            var newStr = newVal?.ToString() ?? "";

            if (oldStr != newStr)
            {
                diff.Add(new PropertyDiff(key, oldStr, newStr));
            }
        }

        return new 
        { 
            entityName = log.EntityName, 
            entityId = log.EntityId, 
            action = log.Action, 
            changedBy = log.ChangedBy, 
            changedAt = log.ChangedAt.ToString("g"), 
            diff = diff 
        };
    }

    public async Task SaveHelpTextAsync(HelpText help, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(help.FieldKey) || string.IsNullOrWhiteSpace(help.Text))
            throw new ArgumentException("Field Key and Text are required.");

        var existing = await _db.HelpTexts.FirstOrDefaultAsync(h => h.FieldKey == help.FieldKey && h.Page == help.Page && !h.IsDeleted, cancellationToken);
        if (existing != null)
        {
            existing.Text = help.Text;
        }
        else
        {
            _db.HelpTexts.Add(help);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveReportColumnConfigAsync(ReportColumnConfig config, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(config.ReportName) || string.IsNullOrWhiteSpace(config.ColumnKey) || string.IsNullOrWhiteSpace(config.DisplayName))
            throw new ArgumentException("Report Name, Column Key, and Display Name are required.");

        if (config.Id > 0)
        {
            var existing = await _db.ReportColumnConfigs.FindAsync([config.Id], cancellationToken);
            if (existing == null || existing.IsDeleted) throw new KeyNotFoundException("Report column configuration not found.");

            existing.DisplayName = config.DisplayName.Trim();
            existing.IsVisible = config.IsVisible;
            existing.SortOrder = config.SortOrder;
            existing.Format = config.Format?.Trim();
        }
        else
        {
            config.ReportName = config.ReportName.Trim();
            config.ColumnKey = config.ColumnKey.Trim();
            config.DisplayName = config.DisplayName.Trim();
            _db.ReportColumnConfigs.Add(config);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteReportColumnConfigAsync(int id, CancellationToken cancellationToken)
    {
        var c = await _db.ReportColumnConfigs.FindAsync([id], cancellationToken);
        if (c == null || c.IsDeleted) throw new KeyNotFoundException("Report config not found.");
        c.IsDeleted = true;
        c.UpdatedAt = DateTime.UtcNow;
        _db.ReportColumnConfigs.Update(c);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<(List<PartyCodeMapping> Mappings, int TotalPages)> GetPartyCodeMappingsAsync(string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = _db.PartyCodeMappings.Where(m => !m.IsDeleted);
        if (!string.IsNullOrEmpty(search))
        {
            var s = search.Trim();
            query = query.Where(m => m.AlternativeCode.Contains(s) || m.OriginalCode.Contains(s) || (m.Notes != null && m.Notes.Contains(s)));
        }

        var total = await query.CountAsync(cancellationToken);
        var totalPages = (int)System.Math.Ceiling((double)total / pageSize);
        if (totalPages == 0) totalPages = 1;

        var mappings = await query
            .OrderBy(m => m.AlternativeCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (mappings, totalPages);
    }

    public async Task SavePartyCodeMappingAsync(int id, string alternativeCode, string originalCode, string? notes, bool isActive, CancellationToken cancellationToken)
    {
        if (id == 0)
        {
            var mapping = new PartyCodeMapping
            {
                AlternativeCode = alternativeCode,
                OriginalCode = originalCode,
                Notes = notes,
                IsActive = isActive
            };
            _db.PartyCodeMappings.Add(mapping);
        }
        else
        {
            var mapping = await _db.PartyCodeMappings.FirstOrDefaultAsync(m => m.Id == id && !m.IsDeleted, cancellationToken)
                ?? throw new KeyNotFoundException("Mapping not found.");

            mapping.AlternativeCode = alternativeCode;
            mapping.OriginalCode = originalCode;
            mapping.Notes = notes;
            mapping.IsActive = isActive;
            _db.PartyCodeMappings.Update(mapping);
        }
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeletePartyCodeMappingAsync(int id, CancellationToken cancellationToken)
    {
        var mapping = await _db.PartyCodeMappings.FirstOrDefaultAsync(m => m.Id == id && !m.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("Mapping not found.");

        mapping.IsDeleted = true;
        _db.PartyCodeMappings.Update(mapping);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<(int ImportedCount, int SkippedCount)> ImportPartyCodeMappingsAsync(IFormFile file, CancellationToken cancellationToken)
    {
        int importedCount = 0;
        int skippedCount = 0;

        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        using var stream = file.OpenReadStream();
        using var reader = ExcelReaderFactory.CreateReader(stream);
        var dataset = reader.AsDataSet(new ExcelDataSetConfiguration
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = true }
        });

        var dt = dataset.Tables[0] ?? throw new InvalidDataException("No tables found in Excel sheet.");

        int colAlternative = -1;
        int colOriginal = -1;
        int colNotes = -1;

        for (int i = 0; i < dt.Columns.Count; i++)
        {
            var columnName = dt.Columns[i].ColumnName?.Trim();
            if (columnName != null && (columnName.Equals("AlternativeCode", StringComparison.OrdinalIgnoreCase) || 
                columnName.Equals("Alternative Code", StringComparison.OrdinalIgnoreCase) ||
                columnName.Equals("Alias Code", StringComparison.OrdinalIgnoreCase)))
            {
                colAlternative = i;
            }
            else if (columnName != null && (columnName.Equals("OriginalCode", StringComparison.OrdinalIgnoreCase) || 
                     columnName.Equals("Original Code", StringComparison.OrdinalIgnoreCase) ||
                     columnName.Equals("Canonical Code", StringComparison.OrdinalIgnoreCase)))
            {
                colOriginal = i;
            }
            else if (columnName != null && (columnName.Equals("Notes", StringComparison.OrdinalIgnoreCase) || 
                     columnName.Equals("Description", StringComparison.OrdinalIgnoreCase) ||
                     columnName.Equals("Remarks", StringComparison.OrdinalIgnoreCase)))
            {
                colNotes = i;
            }
        }

        if (colAlternative == -1 || colOriginal == -1)
        {
            throw new InvalidDataException("Excel file must contain both 'AlternativeCode' and 'OriginalCode' columns.");
        }

        var existingMappings = await _db.PartyCodeMappings
            .Where(m => !m.IsDeleted)
            .ToListAsync(cancellationToken);

        var existingDict = existingMappings
            .GroupBy(m => m.AlternativeCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        for (int r = 0; r < dt.Rows.Count; r++)
        {
            var row = dt.Rows[r];
            var altCode = row[colAlternative]?.ToString()?.Trim();
            var origCode = row[colOriginal]?.ToString()?.Trim();
            var notes = colNotes != -1 ? row[colNotes]?.ToString()?.Trim() : null;

            if (string.IsNullOrEmpty(altCode) || string.IsNullOrEmpty(origCode))
            {
                skippedCount++;
                continue;
            }

            if (existingDict.TryGetValue(altCode, out var mapping))
            {
                mapping.OriginalCode = origCode;
                if (notes != null) mapping.Notes = notes;
                mapping.IsActive = true;
                _db.PartyCodeMappings.Update(mapping);
            }
            else
            {
                var newMapping = new PartyCodeMapping
                {
                    AlternativeCode = altCode,
                    OriginalCode = origCode,
                    Notes = notes,
                    IsActive = true
                };
                _db.PartyCodeMappings.Add(newMapping);
                existingDict[altCode] = newMapping;
            }
            importedCount++;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return (importedCount, skippedCount);
    }
}
