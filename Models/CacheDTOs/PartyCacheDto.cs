namespace IncentivePortal.Models.CacheDTOs;

public class PartyCacheDto
{
    public int Id { get; set; }
    public string PartyCode { get; set; } = string.Empty;
    public string DealerType { get; set; } = string.Empty;
    public int BranchId { get; set; }
}
