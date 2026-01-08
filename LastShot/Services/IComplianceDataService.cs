using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LastShot.Models;

namespace LastShot.Services;

public interface IComplianceDataService
{
    Task<DashboardSummary> GetDashboardSummaryAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Customer>> GetCustomersAsync(CancellationToken cancellationToken = default);
    Task<Customer> SaveCustomerAsync(Customer customer, CancellationToken cancellationToken = default);
    Task DeleteCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RadioAsset>> GetRadioAssetsAsync(CancellationToken cancellationToken = default);
    Task<RadioAsset> SaveRadioAssetAsync(RadioAsset asset, CancellationToken cancellationToken = default);
    Task DeleteRadioAssetAsync(Guid assetId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FrequencyChannel>> GetFrequencyChannelsAsync(CancellationToken cancellationToken = default);
    Task<FrequencyChannel> SaveFrequencyChannelAsync(FrequencyChannel channel, CancellationToken cancellationToken = default);
    Task DeleteFrequencyChannelAsync(Guid channelId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RentalContract>> GetRentalContractsAsync(CancellationToken cancellationToken = default);
    Task<RentalContract> SaveRentalContractAsync(RentalContract contract, CancellationToken cancellationToken = default);
    Task DeleteRentalContractAsync(Guid contractId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ComplianceLogEntry>> GetComplianceLogsAsync(CancellationToken cancellationToken = default);
    Task<ComplianceLogEntry> SaveComplianceLogAsync(ComplianceLogEntry entry, CancellationToken cancellationToken = default);
    Task DeleteComplianceLogAsync(Guid entryId, CancellationToken cancellationToken = default);
}
