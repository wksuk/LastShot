using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LastShot.Data;
using LastShot.Models;
using Microsoft.EntityFrameworkCore;

namespace LastShot.Services;

public sealed class ComplianceDataService : IComplianceDataService
{
    private readonly IDbContextFactory<LastShotDbContext> _contextFactory;

    public ComplianceDataService(IDbContextFactory<LastShotDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<DashboardSummary> GetDashboardSummaryAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var nowUtc = DateTime.UtcNow;
        var upcomingThreshold = nowUtc.AddDays(14);
        var monthAnchor = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var activeRentalsTask = context.RentalContracts
            .CountAsync(rc => rc.StartDateUtc <= nowUtc && rc.EndDateUtc >= nowUtc, cancellationToken);

        var availableRadiosTask = context.RadioAssets
            .CountAsync(r => r.Status == RadioStatus.InStorage, cancellationToken);

        var complianceEventsTask = context.ComplianceLogs
            .CountAsync(log => log.TimestampUtc >= monthAnchor, cancellationToken);

        var expiringContractsTask = context.RentalContracts
            .AsNoTracking()
            .Include(rc => rc.Customer)
            .Where(rc => rc.EndDateUtc >= nowUtc && rc.EndDateUtc <= upcomingThreshold)
            .OrderBy(rc => rc.EndDateUtc)
            .Take(10)
            .Select(rc => new RentalAlert
            {
                ContractId = rc.Id,
                CustomerName = rc.Customer.Name,
                EndDateUtc = rc.EndDateUtc,
                ContractType = rc.ContractType.ToString()
            })
            .ToListAsync(cancellationToken);

        await Task.WhenAll(activeRentalsTask, availableRadiosTask, complianceEventsTask, expiringContractsTask);

        var expiringContracts = expiringContractsTask.Result;

        return new DashboardSummary
        {
            ActiveRentals = activeRentalsTask.Result,
            AvailableRadios = availableRadiosTask.Result,
            ComplianceEventsThisMonth = complianceEventsTask.Result,
            UpcomingExpirations = expiringContracts.Count,
            ExpiringContracts = expiringContracts
        };
    }

    public async Task<IReadOnlyList<Customer>> GetCustomersAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Customers
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Customer> SaveCustomerAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(customer);
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        customer.Name = (customer.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(customer.Name))
        {
            throw new InvalidOperationException("Customer name is required.");
        }

        if (customer.CreatedAtUtc == default)
        {
            customer.CreatedAtUtc = DateTime.UtcNow;
        }

        var isNew = customer.Id == Guid.Empty;
        if (isNew)
        {
            customer.Id = Guid.NewGuid();
            await context.Customers.AddAsync(customer, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            return customer;
        }

        var existing = await context.Customers.FindAsync(new object[] { customer.Id }, cancellationToken);
        if (existing is null)
        {
            throw new InvalidOperationException("Customer could not be found.");
        }

        var createdAt = existing.CreatedAtUtc;
        context.Entry(existing).CurrentValues.SetValues(customer);
        existing.CreatedAtUtc = createdAt;
        await context.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task DeleteCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var hasContracts = await context.RentalContracts.AnyAsync(rc => rc.CustomerId == customerId, cancellationToken);
        if (hasContracts)
        {
            throw new InvalidOperationException("Cannot delete a customer that has rental contracts.");
        }

        var entity = await context.Customers.FindAsync(new object[] { customerId }, cancellationToken);
        if (entity is null)
        {
            return;
        }

        context.Customers.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RadioAsset>> GetRadioAssetsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.RadioAssets
            .AsNoTracking()
            .OrderBy(r => r.SerialNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task<RadioAsset> SaveRadioAssetAsync(RadioAsset asset, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(asset);
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        asset.SerialNumber = (asset.SerialNumber ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(asset.SerialNumber))
        {
            throw new InvalidOperationException("Serial number is required.");
        }

        var isNew = asset.Id == Guid.Empty;
        RadioAsset persisted;
        if (isNew)
        {
            asset.Id = Guid.NewGuid();
            persisted = asset;
            await context.RadioAssets.AddAsync(asset, cancellationToken);
        }
        else
        {
            var existing = await context.RadioAssets.FindAsync(new object[] { asset.Id }, cancellationToken);
            if (existing is null)
            {
                throw new InvalidOperationException("Radio asset could not be found.");
            }

            context.Entry(existing).CurrentValues.SetValues(asset);
            persisted = existing;
        }

        await context.SaveChangesAsync(cancellationToken);
        return persisted;
    }

    public async Task DeleteRadioAssetAsync(Guid assetId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var hasContracts = await context.RentalContracts.AnyAsync(rc => rc.RadioAssetId == assetId, cancellationToken);
        if (hasContracts)
        {
            throw new InvalidOperationException("Cannot delete a radio asset that is linked to rental contracts.");
        }

        var entity = await context.RadioAssets.FindAsync(new object[] { assetId }, cancellationToken);
        if (entity is null)
        {
            return;
        }

        context.RadioAssets.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FrequencyChannel>> GetFrequencyChannelsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.FrequencyChannels
            .AsNoTracking()
            .OrderBy(fc => fc.FrequencyMHz)
            .ThenBy(fc => fc.Label)
            .ToListAsync(cancellationToken);
    }

    public async Task<FrequencyChannel> SaveFrequencyChannelAsync(FrequencyChannel channel, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(channel);
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        channel.Label = (channel.Label ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(channel.Label))
        {
            throw new InvalidOperationException("Frequency label is required.");
        }

        var isNew = channel.Id == Guid.Empty;
        FrequencyChannel persisted;
        if (isNew)
        {
            channel.Id = Guid.NewGuid();
            persisted = channel;
            await context.FrequencyChannels.AddAsync(channel, cancellationToken);
        }
        else
        {
            var existing = await context.FrequencyChannels.FindAsync(new object[] { channel.Id }, cancellationToken);
            if (existing is null)
            {
                throw new InvalidOperationException("Frequency channel could not be found.");
            }

            context.Entry(existing).CurrentValues.SetValues(channel);
            persisted = existing;
        }

        await context.SaveChangesAsync(cancellationToken);
        return persisted;
    }

    public async Task DeleteFrequencyChannelAsync(Guid channelId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var inUse = await context.RentalContracts.AnyAsync(rc => rc.FrequencyChannelId == channelId, cancellationToken);
        if (inUse)
        {
            throw new InvalidOperationException("Cannot delete a frequency channel that is linked to rental contracts.");
        }

        var entity = await context.FrequencyChannels.FindAsync(new object[] { channelId }, cancellationToken);
        if (entity is null)
        {
            return;
        }

        context.FrequencyChannels.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RentalContract>> GetRentalContractsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.RentalContracts
            .AsNoTracking()
            .Include(rc => rc.Customer)
            .Include(rc => rc.RadioAsset)
            .Include(rc => rc.FrequencyChannel)
            .OrderByDescending(rc => rc.StartDateUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<RentalContract> SaveRentalContractAsync(RentalContract contract, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contract);
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        contract.StartDateUtc = EnsureUtc(contract.StartDateUtc);
        contract.EndDateUtc = EnsureUtc(contract.EndDateUtc);
        contract.ContractNumber = (contract.ContractNumber ?? string.Empty).Trim();
        ValidateRentalContract(contract);

        await EnsureContractReferencesExist(context, contract, cancellationToken);
        await EnsureFrequencyAndRadioAvailabilityAsync(context, contract, cancellationToken);

        var isNew = contract.Id == Guid.Empty;
        Guid? originalRadioAssetId = null;

        if (isNew)
        {
            contract.Id = Guid.NewGuid();
            await context.RentalContracts.AddAsync(contract, cancellationToken);
        }
        else
        {
            var existing = await context.RentalContracts.FindAsync(new object[] { contract.Id }, cancellationToken);
            if (existing is null)
            {
                throw new InvalidOperationException("Rental contract could not be found.");
            }

            originalRadioAssetId = existing.RadioAssetId;
            context.Entry(existing).CurrentValues.SetValues(contract);
        }

        await context.SaveChangesAsync(cancellationToken);
        await UpdateRadioAssetStateAsync(context, contract.RadioAssetId, cancellationToken);
        if (originalRadioAssetId.HasValue && originalRadioAssetId.Value != contract.RadioAssetId)
        {
            await UpdateRadioAssetStateAsync(context, originalRadioAssetId.Value, cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
        return contract;
    }

    public async Task DeleteRentalContractAsync(Guid contractId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.RentalContracts.FindAsync(new object[] { contractId }, cancellationToken);
        if (entity is null)
        {
            return;
        }

        var radioAssetId = entity.RadioAssetId;
        context.RentalContracts.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
        await UpdateRadioAssetStateAsync(context, radioAssetId, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ComplianceLogEntry>> GetComplianceLogsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.ComplianceLogs
            .AsNoTracking()
            .Include(log => log.RentalContract)
            .OrderByDescending(log => log.TimestampUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<ComplianceLogEntry> SaveComplianceLogAsync(ComplianceLogEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        entry.Description = (entry.Description ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(entry.Description))
        {
            throw new InvalidOperationException("Compliance description is required.");
        }

        entry.EvidencePath = string.IsNullOrWhiteSpace(entry.EvidencePath) ? null : entry.EvidencePath.Trim();

        if (entry.TimestampUtc == default)
        {
            entry.TimestampUtc = DateTime.UtcNow;
        }
        else
        {
            entry.TimestampUtc = EnsureUtc(entry.TimestampUtc);
        }

        if (entry.RentalContractId.HasValue)
        {
            var contractExists = await context.RentalContracts.AnyAsync(rc => rc.Id == entry.RentalContractId.Value, cancellationToken);
            if (!contractExists)
            {
                throw new InvalidOperationException("Referenced rental contract could not be found.");
            }
        }

        var isNew = entry.Id == Guid.Empty;
        if (isNew)
        {
            entry.Id = Guid.NewGuid();
            await context.ComplianceLogs.AddAsync(entry, cancellationToken);
        }
        else
        {
            var existing = await context.ComplianceLogs.FindAsync(new object[] { entry.Id }, cancellationToken);
            if (existing is null)
            {
                throw new InvalidOperationException("Compliance log entry could not be found.");
            }

            context.Entry(existing).CurrentValues.SetValues(entry);
        }

        await context.SaveChangesAsync(cancellationToken);
        return entry;
    }

    public async Task DeleteComplianceLogAsync(Guid entryId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.ComplianceLogs.FindAsync(new object[] { entryId }, cancellationToken);
        if (entity is null)
        {
            return;
        }

        context.ComplianceLogs.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateRentalContract(RentalContract contract)
    {
        if (contract.StartDateUtc >= contract.EndDateUtc)
        {
            throw new InvalidOperationException("Rental contracts must have an end date after the start date.");
        }

        var days = (contract.EndDateUtc - contract.StartDateUtc).TotalDays;
        switch (contract.ContractType)
        {
            case RentalContractType.Hiring when days > 366:
                throw new InvalidOperationException("Hiring contracts cannot exceed 12 months.");
            case RentalContractType.Parking when days > 93:
                throw new InvalidOperationException("Parking contracts cannot exceed 3 months.");
            case RentalContractType.Demonstration when days > 28:
                throw new InvalidOperationException("Demonstration contracts cannot exceed 28 days.");
        }

        if (string.IsNullOrWhiteSpace(contract.ContractNumber))
        {
            throw new InvalidOperationException("Contract number is required.");
        }
    }

    private static async Task EnsureContractReferencesExist(LastShotDbContext context, RentalContract contract, CancellationToken cancellationToken)
    {
        var hasCustomer = await context.Customers.AnyAsync(c => c.Id == contract.CustomerId, cancellationToken);
        var hasRadio = await context.RadioAssets.AnyAsync(r => r.Id == contract.RadioAssetId, cancellationToken);
        var hasFrequency = await context.FrequencyChannels.AnyAsync(f => f.Id == contract.FrequencyChannelId, cancellationToken);
        if (!hasCustomer || !hasRadio || !hasFrequency)
        {
            throw new InvalidOperationException("Rental contracts must reference existing customers, radios, and frequencies.");
        }
    }

    private static async Task EnsureFrequencyAndRadioAvailabilityAsync(LastShotDbContext context, RentalContract contract, CancellationToken cancellationToken)
    {
        var start = contract.StartDateUtc;
        var end = contract.EndDateUtc;

        var frequencyConflict = await context.RentalContracts
            .Where(rc => rc.Id != contract.Id && rc.FrequencyChannelId == contract.FrequencyChannelId)
            .Where(rc => rc.EndDateUtc > start && rc.StartDateUtc < end)
            .AnyAsync(cancellationToken);

        if (frequencyConflict)
        {
            throw new InvalidOperationException("The selected frequency channel is already assigned during that period.");
        }

        var radioConflict = await context.RentalContracts
            .Where(rc => rc.Id != contract.Id && rc.RadioAssetId == contract.RadioAssetId)
            .Where(rc => rc.EndDateUtc > start && rc.StartDateUtc < end)
            .AnyAsync(cancellationToken);

        if (radioConflict)
        {
            throw new InvalidOperationException("The selected radio is already hired during that period.");
        }
    }

    private static async Task UpdateRadioAssetStateAsync(LastShotDbContext context, Guid radioAssetId, CancellationToken cancellationToken)
    {
        var radio = await context.RadioAssets.FirstOrDefaultAsync(r => r.Id == radioAssetId, cancellationToken);
        if (radio is null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var isActive = await context.RentalContracts
            .AnyAsync(rc => rc.RadioAssetId == radioAssetId && rc.StartDateUtc <= now && rc.EndDateUtc >= now, cancellationToken);

        radio.Status = isActive ? RadioStatus.HiredOut : RadioStatus.InStorage;
        context.RadioAssets.Update(radio);
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }
}
