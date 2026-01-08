using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace LastShot.Models;

public class RentalContract
{
    public Guid Id { get; set; }

    [MaxLength(32)]
    public string ContractNumber { get; set; } = string.Empty;

    public RentalContractType ContractType { get; set; }

    public DateTime StartDateUtc { get; set; }

    public DateTime EndDateUtc { get; set; }

    public bool NotificationConfirmed { get; set; }

    [MaxLength(64)]
    public string RadioModel { get; set; } = "HP785";

    [MaxLength(260)]
    public string? CodeplugPath { get; set; }

    [MaxLength(512)]
    public string? Comments { get; set; }

    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    public Guid RadioAssetId { get; set; }
    public RadioAsset RadioAsset { get; set; } = null!;

    public Guid FrequencyChannelId { get; set; }
    public FrequencyChannel FrequencyChannel { get; set; } = null!;

    public ICollection<ComplianceLogEntry> ComplianceLogs { get; set; } = new List<ComplianceLogEntry>();
}
