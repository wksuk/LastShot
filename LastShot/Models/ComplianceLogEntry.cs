using System;
using System.ComponentModel.DataAnnotations;

namespace LastShot.Models;

public class ComplianceLogEntry
{
    public Guid Id { get; set; }

    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    public ComplianceActionType ActionType { get; set; }

    [MaxLength(512)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(260)]
    public string? EvidencePath { get; set; }

    public Guid? RentalContractId { get; set; }

    public RentalContract? RentalContract { get; set; }
}
