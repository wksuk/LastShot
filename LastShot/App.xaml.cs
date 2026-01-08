using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using LastShot.Data;
using LastShot.Models;
using LastShot.Services;
using LastShot.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LastShot;

public partial class App : Application
{
    private readonly IHost _host;

    public App()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                var databasePath = ResolveDatabasePath();
                services.AddDbContextFactory<LastShotDbContext>(options =>
                {
                    options.UseSqlite($"Data Source={databasePath};Cache=Shared");
                });

                services.AddSingleton<IComplianceDataService, ComplianceDataService>();
                services.AddLogging();
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await _host.StartAsync();
        await EnsureDatabaseAsync();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync(TimeSpan.FromSeconds(5));
        _host.Dispose();
        base.OnExit(e);
    }

    private static string ResolveDatabasePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var lastShotFolder = Path.Combine(appData, "LastShot");
        Directory.CreateDirectory(lastShotFolder);
        return Path.Combine(lastShotFolder, "lastshot.db");
    }

    private async Task EnsureDatabaseAsync()
    {
        using var scope = _host.Services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<LastShotDbContext>>();
        await using var context = await factory.CreateDbContextAsync();
        await context.Database.EnsureCreatedAsync();
        await SeedSampleDataAsync(context);
    }

    private static async Task SeedSampleDataAsync(LastShotDbContext context)
    {
        if (await context.Customers.AnyAsync())
        {
            return;
        }

        var customerAdam = new Customer
        {
            Id = Guid.NewGuid(),
            Name = "Skilton Events",
            BusinessType = "Event production",
            Email = "adam@skilton.events",
            Phone = "+44 7700 900001",
            AddressLine1 = "Unit 12",
            TownOrCity = "Bristol",
            Postcode = "BS1 5AA"
        };

        var customerHarbor = new Customer
        {
            Id = Guid.NewGuid(),
            Name = "Harbor Logistics",
            BusinessType = "Logistics",
            Email = "ops@harbor-logistics.co.uk",
            Phone = "+44 7700 900245",
            AddressLine1 = "Dockside Estate",
            TownOrCity = "Bristol",
            Postcode = "BS1 4RJ"
        };

        var radioOne = new RadioAsset
        {
            Id = Guid.NewGuid(),
            SerialNumber = "HP785-0001",
            Model = "HP785",
            FrequencyCapability = "UHF (400-470MHz)",
            PowerOutputWatts = 10,
            AntennaType = "Whip",
            Status = RadioStatus.HiredOut,
            IsIr2044Compliant = true,
            LastServiceDateUtc = DateTime.UtcNow.AddDays(-30)
        };

        var radioTwo = new RadioAsset
        {
            Id = Guid.NewGuid(),
            SerialNumber = "HP785-0002",
            Model = "HP785",
            FrequencyCapability = "UHF (400-470MHz)",
            PowerOutputWatts = 10,
            AntennaType = "Whip",
            Status = RadioStatus.HiredOut,
            IsIr2044Compliant = true,
            LastServiceDateUtc = DateTime.UtcNow.AddDays(-60)
        };

        var channelOne = new FrequencyChannel
        {
            Id = Guid.NewGuid(),
            Label = "Schedule 2 Ch.1",
            FrequencyMHz = 453.0125,
            BandwidthKHz = 12.5,
            IsDualFrequency = false,
            Notes = "Primary hire channel"
        };

        var channelTwo = new FrequencyChannel
        {
            Id = Guid.NewGuid(),
            Label = "Schedule 2 Ch.2",
            FrequencyMHz = 453.0500,
            BandwidthKHz = 12.5,
            IsDualFrequency = true,
            Notes = "Dual frequency option"
        };

        var now = DateTime.UtcNow;
        var contractOne = new RentalContract
        {
            Id = Guid.NewGuid(),
            ContractNumber = "HIRE-2026-001",
            ContractType = RentalContractType.Hiring,
            StartDateUtc = now.AddDays(-7),
            EndDateUtc = now.AddDays(21),
            NotificationConfirmed = true,
            RadioModel = "HP785",
            Comments = "12 week hire for logistics hub",
            CustomerId = customerAdam.Id,
            Customer = customerAdam,
            RadioAssetId = radioOne.Id,
            RadioAsset = radioOne,
            FrequencyChannelId = channelOne.Id,
            FrequencyChannel = channelOne
        };

        var contractTwo = new RentalContract
        {
            Id = Guid.NewGuid(),
            ContractNumber = "DEMO-2026-014",
            ContractType = RentalContractType.Demonstration,
            StartDateUtc = now.AddDays(-1),
            EndDateUtc = now.AddDays(5),
            NotificationConfirmed = false,
            RadioModel = "HP785",
            Comments = "Demo kit for Harbor Logistics",
            CustomerId = customerHarbor.Id,
            Customer = customerHarbor,
            RadioAssetId = radioTwo.Id,
            RadioAsset = radioTwo,
            FrequencyChannelId = channelTwo.Id,
            FrequencyChannel = channelTwo
        };

        var logOne = new ComplianceLogEntry
        {
            Id = Guid.NewGuid(),
            TimestampUtc = now.AddDays(-2),
            ActionType = ComplianceActionType.FrequencyCheck,
            Description = "Scanner sweep completed - no co-channel occupancy",
            RentalContractId = contractOne.Id,
            RentalContract = contractOne
        };

        var logTwo = new ComplianceLogEntry
        {
            Id = Guid.NewGuid(),
            TimestampUtc = now.AddDays(-1),
            ActionType = ComplianceActionType.Notification,
            Description = "Customer notified of Ofcom licence conditions",
            RentalContractId = contractTwo.Id,
            RentalContract = contractTwo
        };

        await context.Customers.AddRangeAsync(customerAdam, customerHarbor);
        await context.RadioAssets.AddRangeAsync(radioOne, radioTwo);
        await context.FrequencyChannels.AddRangeAsync(channelOne, channelTwo);
        await context.RentalContracts.AddRangeAsync(contractOne, contractTwo);
        await context.ComplianceLogs.AddRangeAsync(logOne, logTwo);
        await context.SaveChangesAsync();
    }
}
