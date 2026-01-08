using System;

namespace LastShot.Models;

/// <summary>
/// Rental contract types aligned with Ofcom licence allowances.
/// </summary>
public enum RentalContractType
{
    Hiring = 0,
    Parking = 1,
    Demonstration = 2
}

/// <summary>
/// Inventory status for Hytera radio assets.
/// </summary>
public enum RadioStatus
{
    InStorage = 0,
    HiredOut = 1,
    Repair = 2
}

/// <summary>
/// Captures the broad type of compliance action that has been logged.
/// </summary>
public enum ComplianceActionType
{
    FrequencyCheck = 0,
    ContractUpdate = 1,
    Notification = 2,
    Maintenance = 3,
    Investigation = 4
}
