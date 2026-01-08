using System;
using System.Collections.Generic;

namespace LastShot.Models;

public class DashboardSummary
{
    public int ActiveRentals { get; init; }
    public int UpcomingExpirations { get; init; }
    public int AvailableRadios { get; init; }
    public int ComplianceEventsThisMonth { get; init; }
    public IReadOnlyList<RentalAlert> ExpiringContracts { get; init; } = Array.Empty<RentalAlert>();
}

public record RentalAlert
{
    public Guid ContractId { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public DateTime EndDateUtc { get; init; }
    public string ContractType { get; init; } = string.Empty;
}
