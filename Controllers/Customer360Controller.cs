using IncentivePortal.Application.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace IncentivePortal.Controllers;

[Authorize]
public class Customer360Controller(ICustomer360Service customer360Service) : Controller
{
    public async Task<IActionResult> Index(string? partyCode, CancellationToken cancellationToken)
    {
        var viewModel = await customer360Service.GetCustomer360Async(partyCode, cancellationToken);
        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> Search(string query, CancellationToken cancellationToken)
    {
        var result = await customer360Service.SearchCustomersAsync(query, cancellationToken);
        return Json(result.SearchResults);
    }

    [HttpPost]
    public async Task<IActionResult> GetSalesAnalysis(string partyCode, CancellationToken cancellationToken)
    {
        var draw = Request.Form["draw"].FirstOrDefault();
        var start = Request.Form["start"].FirstOrDefault();
        var length = Request.Form["length"].FirstOrDefault();
        var search = Request.Form["search[value]"].FirstOrDefault();
        
        var sortColIdx = Request.Form["order[0][column]"].FirstOrDefault();
        var sortCol = Request.Form.ContainsKey($"columns[{sortColIdx}][data]") ? Request.Form[$"columns[{sortColIdx}][data]"].FirstOrDefault() ?? "" : "";
        var sortDir = Request.Form.ContainsKey("order[0][dir]") ? Request.Form["order[0][dir]"].FirstOrDefault() ?? "" : "";

        int pageSize = length != null ? System.Convert.ToInt32(length) : 10;
        int skip = start != null ? System.Convert.ToInt32(start) : 0;

        var result = await customer360Service.GetSalesAnalysisAsync(partyCode, skip, pageSize, search, sortCol, sortDir, cancellationToken);
        
        if (result != null)
        {
            result.draw = draw != null ? System.Convert.ToInt32(draw) : 1;
        }

        return Json(result);
    }
}
