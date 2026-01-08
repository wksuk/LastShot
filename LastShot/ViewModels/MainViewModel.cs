using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LastShot.Models;
using LastShot.Services;
using Microsoft.Extensions.Logging;

namespace LastShot.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IComplianceDataService _dataService;
    private readonly ILogger<MainViewModel> _logger;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    private readonly AsyncRelayCommand _saveCustomerCommand;
    private readonly AsyncRelayCommand _deleteCustomerCommand;
    private readonly RelayCommand _newCustomerCommand;

    private readonly AsyncRelayCommand _saveRadioAssetCommand;
    private readonly AsyncRelayCommand _deleteRadioAssetCommand;
    private readonly RelayCommand _newRadioAssetCommand;

    private readonly AsyncRelayCommand _saveFrequencyCommand;
    private readonly AsyncRelayCommand _deleteFrequencyCommand;
    private readonly RelayCommand _newFrequencyCommand;

    private readonly AsyncRelayCommand _saveRentalContractCommand;
    private readonly AsyncRelayCommand _deleteRentalContractCommand;
    private readonly RelayCommand _newRentalContractCommand;

    private readonly AsyncRelayCommand _saveComplianceLogCommand;
    private readonly AsyncRelayCommand _deleteComplianceLogCommand;
    private readonly RelayCommand _newComplianceLogCommand;

    public MainViewModel(IComplianceDataService dataService, ILogger<MainViewModel> logger)
    {
        _dataService = dataService;
        _logger = logger;

        RefreshCommand = new AsyncRelayCommand(RefreshAsync);

        _saveCustomerCommand = new AsyncRelayCommand(SaveCustomerAsync);
        _deleteCustomerCommand = new AsyncRelayCommand(DeleteSelectedCustomerAsync, () => SelectedCustomer is not null);
        _newCustomerCommand = new RelayCommand(BeginNewCustomer);

        _saveRadioAssetCommand = new AsyncRelayCommand(SaveRadioAssetAsync);
        _deleteRadioAssetCommand = new AsyncRelayCommand(DeleteSelectedRadioAssetAsync, () => SelectedRadioAsset is not null);
        _newRadioAssetCommand = new RelayCommand(BeginNewRadioAsset);

        _saveFrequencyCommand = new AsyncRelayCommand(SaveFrequencyChannelAsync);
        _deleteFrequencyCommand = new AsyncRelayCommand(DeleteSelectedFrequencyAsync, () => SelectedFrequencyChannel is not null);
        _newFrequencyCommand = new RelayCommand(BeginNewFrequencyChannel);

        _saveRentalContractCommand = new AsyncRelayCommand(SaveRentalContractAsync);
        _deleteRentalContractCommand = new AsyncRelayCommand(DeleteSelectedRentalContractAsync, () => SelectedRentalContract is not null);
        _newRentalContractCommand = new RelayCommand(BeginNewRentalContract);

        _saveComplianceLogCommand = new AsyncRelayCommand(SaveComplianceLogAsync);
        _deleteComplianceLogCommand = new AsyncRelayCommand(DeleteSelectedComplianceLogAsync, () => SelectedComplianceLog is not null);
        _newComplianceLogCommand = new RelayCommand(BeginNewComplianceLog);
    }

    public ObservableCollection<RentalAlertDisplay> ExpiringContracts { get; } = new();
    public ObservableCollection<Customer> Customers { get; } = new();
    public ObservableCollection<RadioAsset> RadioAssets { get; } = new();
    public ObservableCollection<FrequencyChannel> FrequencyChannels { get; } = new();
    public ObservableCollection<RentalContract> RentalContracts { get; } = new();
    public ObservableCollection<ComplianceLogEntry> ComplianceLogs { get; } = new();

    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand SaveCustomerCommand => _saveCustomerCommand;
    public IAsyncRelayCommand DeleteCustomerCommand => _deleteCustomerCommand;
    public IRelayCommand NewCustomerCommand => _newCustomerCommand;

    public IAsyncRelayCommand SaveRadioAssetCommand => _saveRadioAssetCommand;
    public IAsyncRelayCommand DeleteRadioAssetCommand => _deleteRadioAssetCommand;
    public IRelayCommand NewRadioAssetCommand => _newRadioAssetCommand;

    public IAsyncRelayCommand SaveFrequencyChannelCommand => _saveFrequencyCommand;
    public IAsyncRelayCommand DeleteFrequencyChannelCommand => _deleteFrequencyCommand;
    public IRelayCommand NewFrequencyChannelCommand => _newFrequencyCommand;

    public IAsyncRelayCommand SaveRentalContractCommand => _saveRentalContractCommand;
    public IAsyncRelayCommand DeleteRentalContractCommand => _deleteRentalContractCommand;
    public IRelayCommand NewRentalContractCommand => _newRentalContractCommand;

    public IAsyncRelayCommand SaveComplianceLogCommand => _saveComplianceLogCommand;
    public IAsyncRelayCommand DeleteComplianceLogCommand => _deleteComplianceLogCommand;
    public IRelayCommand NewComplianceLogCommand => _newComplianceLogCommand;

    [ObservableProperty]
    private int activeRentals;

    [ObservableProperty]
    private int upcomingExpirations;

    [ObservableProperty]
    private int availableRadios;

    [ObservableProperty]
    private int complianceEventsThisMonth;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string? lastError;

    [ObservableProperty]
    private DateTime? lastUpdatedUtc;

    [ObservableProperty]
    private Customer editingCustomer = CreateNewCustomer();

    [ObservableProperty]
    private Customer? selectedCustomer;

    [ObservableProperty]
    private RadioAsset editingRadioAsset = CreateNewRadioAsset();

    [ObservableProperty]
    private RadioAsset? selectedRadioAsset;

    [ObservableProperty]
    private FrequencyChannel editingFrequency = CreateNewFrequencyChannel();

    [ObservableProperty]
    private FrequencyChannel? selectedFrequencyChannel;

    [ObservableProperty]
    private RentalContract editingRentalContract = CreateNewRentalContract();

    [ObservableProperty]
    private RentalContract? selectedRentalContract;

    [ObservableProperty]
    private ComplianceLogEntry editingComplianceLog = CreateNewComplianceLog();

    [ObservableProperty]
    private ComplianceLogEntry? selectedComplianceLog;

    public string HeaderTitle => "WKSUK Compliance Dashboard";

    public string HeaderSubtitle => "Track rentals, spectrum use, and audit readiness in one place.";

    public string LastUpdatedDisplay => LastUpdatedUtc?.ToLocalTime().ToString("dd MMM yyyy HH:mm") ?? "Not refreshed yet";

    public async Task InitializeAsync()
    {
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (!await _refreshGate.WaitAsync(0))
        {
            return;
        }

        try
        {
            IsBusy = true;
            LastError = null;

            var dashboardTask = LoadDashboardSummaryAsync();
            var dataTask = LoadManagementDataAsync();

            await Task.WhenAll(dashboardTask, dataTask);
            LastUpdatedUtc = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh compliance data.");
            LastError = ex.Message;
        }
        finally
        {
            IsBusy = false;
            _refreshGate.Release();
        }
    }

    private async Task LoadDashboardSummaryAsync()
    {
        var summary = await _dataService.GetDashboardSummaryAsync();

        ActiveRentals = summary.ActiveRentals;
        UpcomingExpirations = summary.UpcomingExpirations;
        AvailableRadios = summary.AvailableRadios;
        ComplianceEventsThisMonth = summary.ComplianceEventsThisMonth;

        ExpiringContracts.Clear();
        foreach (var alert in summary.ExpiringContracts)
        {
            ExpiringContracts.Add(new RentalAlertDisplay(alert));
        }
    }

    private async Task LoadManagementDataAsync()
    {
        var currentCustomerId = SelectedCustomer?.Id;
        var currentRadioId = SelectedRadioAsset?.Id;
        var currentFrequencyId = SelectedFrequencyChannel?.Id;
        var currentContractId = SelectedRentalContract?.Id;
        var currentLogId = SelectedComplianceLog?.Id;

        var customersTask = _dataService.GetCustomersAsync();
        var radiosTask = _dataService.GetRadioAssetsAsync();
        var frequenciesTask = _dataService.GetFrequencyChannelsAsync();
        var contractsTask = _dataService.GetRentalContractsAsync();
        var logsTask = _dataService.GetComplianceLogsAsync();

        await Task.WhenAll(customersTask, radiosTask, frequenciesTask, contractsTask, logsTask);

        ReplaceItems(Customers, customersTask.Result);
        ReplaceItems(RadioAssets, radiosTask.Result);
        ReplaceItems(FrequencyChannels, frequenciesTask.Result);
        ReplaceItems(RentalContracts, contractsTask.Result);
        ReplaceItems(ComplianceLogs, logsTask.Result);

        Reselect(Customers, currentCustomerId, c => c.Id, value => SelectedCustomer = value);
        Reselect(RadioAssets, currentRadioId, r => r.Id, value => SelectedRadioAsset = value);
        Reselect(FrequencyChannels, currentFrequencyId, f => f.Id, value => SelectedFrequencyChannel = value);
        Reselect(RentalContracts, currentContractId, rc => rc.Id, value => SelectedRentalContract = value);
        Reselect(ComplianceLogs, currentLogId, log => log.Id, value => SelectedComplianceLog = value);
    }

    private async Task SaveCustomerAsync()
    {
        try
        {
            IsBusy = true;
            LastError = null;
            var saved = await _dataService.SaveCustomerAsync(CloneCustomer(EditingCustomer));
            await LoadManagementDataAsync();
            SelectedCustomer = Customers.FirstOrDefault(c => c.Id == saved.Id);
            await LoadDashboardSummaryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save customer record.");
            LastError = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DeleteSelectedCustomerAsync()
    {
        if (SelectedCustomer is null)
        {
            return;
        }

        try
        {
            IsBusy = true;
            LastError = null;
            await _dataService.DeleteCustomerAsync(SelectedCustomer.Id);
            SelectedCustomer = null;
            await LoadManagementDataAsync();
            await LoadDashboardSummaryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete customer record.");
            LastError = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveRadioAssetAsync()
    {
        try
        {
            IsBusy = true;
            LastError = null;
            var saved = await _dataService.SaveRadioAssetAsync(CloneRadioAsset(EditingRadioAsset));
            await LoadManagementDataAsync();
            SelectedRadioAsset = RadioAssets.FirstOrDefault(r => r.Id == saved.Id);
            await LoadDashboardSummaryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save radio asset.");
            LastError = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DeleteSelectedRadioAssetAsync()
    {
        if (SelectedRadioAsset is null)
        {
            return;
        }

        try
        {
            IsBusy = true;
            LastError = null;
            await _dataService.DeleteRadioAssetAsync(SelectedRadioAsset.Id);
            SelectedRadioAsset = null;
            await LoadManagementDataAsync();
            await LoadDashboardSummaryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete radio asset.");
            LastError = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveFrequencyChannelAsync()
    {
        try
        {
            IsBusy = true;
            LastError = null;
            var saved = await _dataService.SaveFrequencyChannelAsync(CloneFrequencyChannel(EditingFrequency));
            await LoadManagementDataAsync();
            SelectedFrequencyChannel = FrequencyChannels.FirstOrDefault(f => f.Id == saved.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save frequency channel.");
            LastError = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DeleteSelectedFrequencyAsync()
    {
        if (SelectedFrequencyChannel is null)
        {
            return;
        }

        try
        {
            IsBusy = true;
            LastError = null;
            await _dataService.DeleteFrequencyChannelAsync(SelectedFrequencyChannel.Id);
            SelectedFrequencyChannel = null;
            await LoadManagementDataAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete frequency channel.");
            LastError = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveRentalContractAsync()
    {
        try
        {
            IsBusy = true;
            LastError = null;
            var saved = await _dataService.SaveRentalContractAsync(CloneRentalContract(EditingRentalContract, convertToUtc: true));
            await LoadManagementDataAsync();
            SelectedRentalContract = RentalContracts.FirstOrDefault(rc => rc.Id == saved.Id);
            await LoadDashboardSummaryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save rental contract.");
            LastError = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DeleteSelectedRentalContractAsync()
    {
        if (SelectedRentalContract is null)
        {
            return;
        }

        try
        {
            IsBusy = true;
            LastError = null;
            await _dataService.DeleteRentalContractAsync(SelectedRentalContract.Id);
            SelectedRentalContract = null;
            await LoadManagementDataAsync();
            await LoadDashboardSummaryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete rental contract.");
            LastError = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveComplianceLogAsync()
    {
        try
        {
            IsBusy = true;
            LastError = null;
            var saved = await _dataService.SaveComplianceLogAsync(CloneComplianceLog(EditingComplianceLog, convertToUtc: true));
            await LoadManagementDataAsync();
            SelectedComplianceLog = ComplianceLogs.FirstOrDefault(log => log.Id == saved.Id);
            await LoadDashboardSummaryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save compliance log entry.");
            LastError = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DeleteSelectedComplianceLogAsync()
    {
        if (SelectedComplianceLog is null)
        {
            return;
        }

        try
        {
            IsBusy = true;
            LastError = null;
            await _dataService.DeleteComplianceLogAsync(SelectedComplianceLog.Id);
            SelectedComplianceLog = null;
            await LoadManagementDataAsync();
            await LoadDashboardSummaryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete compliance log entry.");
            LastError = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void BeginNewCustomer()
    {
        SelectedCustomer = null;
        EditingCustomer = CreateNewCustomer();
    }

    private void BeginNewRadioAsset()
    {
        SelectedRadioAsset = null;
        EditingRadioAsset = CreateNewRadioAsset();
    }

    private void BeginNewFrequencyChannel()
    {
        SelectedFrequencyChannel = null;
        EditingFrequency = CreateNewFrequencyChannel();
    }

    private void BeginNewRentalContract()
    {
        SelectedRentalContract = null;
        EditingRentalContract = CreateNewRentalContract();
    }

    private void BeginNewComplianceLog()
    {
        SelectedComplianceLog = null;
        EditingComplianceLog = CreateNewComplianceLog();
    }

    partial void OnSelectedCustomerChanged(Customer? value)
    {
        EditingCustomer = value is null ? CreateNewCustomer() : CloneCustomer(value);
        _deleteCustomerCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedRadioAssetChanged(RadioAsset? value)
    {
        EditingRadioAsset = value is null ? CreateNewRadioAsset() : CloneRadioAsset(value);
        _deleteRadioAssetCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedFrequencyChannelChanged(FrequencyChannel? value)
    {
        EditingFrequency = value is null ? CreateNewFrequencyChannel() : CloneFrequencyChannel(value);
        _deleteFrequencyCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedRentalContractChanged(RentalContract? value)
    {
        EditingRentalContract = value is null ? CreateNewRentalContract() : CloneRentalContract(value, convertToUtc: false);
        _deleteRentalContractCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedComplianceLogChanged(ComplianceLogEntry? value)
    {
        EditingComplianceLog = value is null ? CreateNewComplianceLog() : CloneComplianceLog(value, convertToUtc: false);
        _deleteComplianceLogCommand.NotifyCanExecuteChanged();
    }

    partial void OnLastUpdatedUtcChanged(DateTime? value)
    {
        OnPropertyChanged(nameof(LastUpdatedDisplay));
    }

    private static void ReplaceItems<T>(ObservableCollection<T> target, IReadOnlyList<T> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }

    private static void Reselect<T>(ObservableCollection<T> items, Guid? previousId, Func<T, Guid> selector, Action<T?> setter)
        where T : class
    {
        if (previousId is Guid id)
        {
            var match = items.FirstOrDefault(item => selector(item) == id);
            setter(match);
        }
        else
        {
            setter(null);
        }
    }

    private static Customer CreateNewCustomer() => new();

    private static RadioAsset CreateNewRadioAsset() => new();

    private static FrequencyChannel CreateNewFrequencyChannel() => new();

    private static RentalContract CreateNewRentalContract()
    {
        var start = DateTime.Now;
        return new RentalContract
        {
            StartDateUtc = start,
            EndDateUtc = start.AddDays(7),
            ContractType = RentalContractType.Hiring,
            NotificationConfirmed = false
        };
    }

    private static ComplianceLogEntry CreateNewComplianceLog() => new() { TimestampUtc = DateTime.Now };

    private static Customer CloneCustomer(Customer source) => new()
    {
        Id = source.Id,
        Name = source.Name,
        AddressLine1 = source.AddressLine1,
        AddressLine2 = source.AddressLine2,
        TownOrCity = source.TownOrCity,
        Postcode = source.Postcode,
        Email = source.Email,
        Phone = source.Phone,
        BusinessType = source.BusinessType,
        CreatedAtUtc = source.CreatedAtUtc
    };

    private static RadioAsset CloneRadioAsset(RadioAsset source) => new()
    {
        Id = source.Id,
        Model = source.Model,
        SerialNumber = source.SerialNumber,
        FrequencyCapability = source.FrequencyCapability,
        PowerOutputWatts = source.PowerOutputWatts,
        AntennaType = source.AntennaType,
        IsIr2044Compliant = source.IsIr2044Compliant,
        LastServiceDateUtc = source.LastServiceDateUtc,
        Status = source.Status
    };

    private static FrequencyChannel CloneFrequencyChannel(FrequencyChannel source) => new()
    {
        Id = source.Id,
        Label = source.Label,
        FrequencyMHz = source.FrequencyMHz,
        BandwidthKHz = source.BandwidthKHz,
        IsDualFrequency = source.IsDualFrequency,
        Notes = source.Notes
    };

    private static RentalContract CloneRentalContract(RentalContract source, bool convertToUtc)
    {
        return new RentalContract
        {
            Id = source.Id,
            ContractNumber = source.ContractNumber,
            ContractType = source.ContractType,
            StartDateUtc = convertToUtc ? ConvertToUtc(source.StartDateUtc) : ConvertFromUtc(source.StartDateUtc),
            EndDateUtc = convertToUtc ? ConvertToUtc(source.EndDateUtc) : ConvertFromUtc(source.EndDateUtc),
            NotificationConfirmed = source.NotificationConfirmed,
            RadioModel = source.RadioModel,
            CodeplugPath = source.CodeplugPath,
            Comments = source.Comments,
            CustomerId = source.CustomerId,
            Customer = source.Customer,
            RadioAssetId = source.RadioAssetId,
            RadioAsset = source.RadioAsset,
            FrequencyChannelId = source.FrequencyChannelId,
            FrequencyChannel = source.FrequencyChannel
        };
    }

    private static ComplianceLogEntry CloneComplianceLog(ComplianceLogEntry source, bool convertToUtc) => new()
    {
        Id = source.Id,
        TimestampUtc = convertToUtc ? ConvertToUtc(source.TimestampUtc) : ConvertFromUtc(source.TimestampUtc),
        ActionType = source.ActionType,
        Description = source.Description,
        EvidencePath = source.EvidencePath,
        RentalContractId = source.RentalContractId,
        RentalContract = source.RentalContract
    };

    private static DateTime ConvertToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Local).ToUniversalTime()
    };

    private static DateTime ConvertFromUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToLocalTime(),
        DateTimeKind.Utc => value.ToLocalTime(),
        _ => value
    };
}

public sealed record RentalAlertDisplay
{
    public RentalAlertDisplay(RentalAlert alert)
    {
        ContractId = alert.ContractId;
        CustomerName = alert.CustomerName;
        EndDateUtc = alert.EndDateUtc;
        ContractType = alert.ContractType;
    }

    public Guid ContractId { get; }
    public string CustomerName { get; }
    public DateTime EndDateUtc { get; }
    public string ContractType { get; }

    public string EndDateLocal => EndDateUtc.ToLocalTime().ToString("dd MMM yyyy");

    public string RemainingDisplay
    {
        get
        {
            var remaining = EndDateUtc - DateTime.UtcNow;
            var days = Math.Max(0, (int)Math.Ceiling(remaining.TotalDays));
            return days switch
            {
                0 => "Due today",
                1 => "1 day remaining",
                _ => $"{days} days remaining"
            };
        }
    }
}
