using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace LastShot.Models;

public class RadioAsset
{
    public Guid Id { get; set; }

    [MaxLength(64)]
    public string Model { get; set; } = "HP785";

    [MaxLength(64)]
    [Required]
    public string SerialNumber { get; set; } = string.Empty;

    [MaxLength(64)]
    public string FrequencyCapability { get; set; } = string.Empty;

    public double PowerOutputWatts { get; set; } = 10d;

    [MaxLength(64)]
    public string AntennaType { get; set; } = string.Empty;

    public bool IsIr2044Compliant { get; set; } = true;

    public DateTime LastServiceDateUtc { get; set; } = DateTime.UtcNow;

    public RadioStatus Status { get; set; } = RadioStatus.InStorage;

    public ICollection<RentalContract> RentalContracts { get; set; } = new List<RentalContract>();
}
