using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace LastShot.Models;

public class FrequencyChannel
{
    public Guid Id { get; set; }

    [MaxLength(64)]
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// MHz centre frequency from the Ofcom schedule.
    /// </summary>
    [Range(0.0, 10000.0)]
    public double FrequencyMHz { get; set; }

    /// <summary>
    /// KHz bandwidth for auditing ERP / spectrum occupancy.
    /// </summary>
    [Range(0.0, 1000.0)]
    public double BandwidthKHz { get; set; }

    public bool IsDualFrequency { get; set; }

    [MaxLength(256)]
    public string? Notes { get; set; }

    public ICollection<RentalContract> RentalContracts { get; set; } = new List<RentalContract>();
}
