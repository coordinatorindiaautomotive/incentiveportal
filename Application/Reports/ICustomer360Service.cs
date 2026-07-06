using IncentivePortal.DTOs;
using System.Threading;
using System.Threading.Tasks;

namespace IncentivePortal.Application.Reports;

public interface ICustomer360Service
{
    Task<Customer360ViewModel> GetCustomer360Async(string? partyCode, CancellationToken cancellationToken = default);
    Task<Customer360ViewModel> SearchCustomersAsync(string query, CancellationToken cancellationToken = default);
    Task<DataTableResponse<SalesAnalysisDto>> GetSalesAnalysisAsync(string partyCode, int start, int length, string search, string sortCol, string sortDir, CancellationToken cancellationToken = default);
}
