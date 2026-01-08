using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace LastShot.Models;

public class Customer
{
    public Guid Id { get; set; }

    [MaxLength(128)]
    [Required]
    public string Name { get; set; } = string.Empty;

    [MaxLength(256)]
    public string AddressLine1 { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? AddressLine2 { get; set; }

    [MaxLength(128)]
    public string TownOrCity { get; set; } = string.Empty;

    [MaxLength(32)]
    public string Postcode { get; set; } = string.Empty;

    [EmailAddress]
    [MaxLength(128)]
    public string Email { get; set; } = string.Empty;

    [Phone]
    [MaxLength(32)]
    public string Phone { get; set; } = string.Empty;

    [MaxLength(64)]
    public string BusinessType { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<RentalContract> RentalContracts { get; set; } = new List<RentalContract>();
}
