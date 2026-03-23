using JDownloader.Cli.Auth;
using JDownloader.Cli.Commands.Accounts;
using JDownloader.Cli.Commands.Advanced;
using JDownloader.Cli.Commands.Auth;
using JDownloader.Cli.Commands.Captcha;
using JDownloader.Cli.Commands.Device;
using JDownloader.Cli.Commands.Doctor;
using JDownloader.Cli.Commands.Downloads;
using JDownloader.Cli.Commands.Events;
using JDownloader.Cli.Commands.Extraction;
using JDownloader.Cli.Commands.Grabber;
using JDownloader.Cli.Commands.Settings;
using JDownloader.Cli.Commands.System;
using JDownloader.Cli.Config;
using JDownloader.Cli.Runtime;
using JDownloader.Cli.Transport;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace JDownloader.Cli.Bootstrap;

public static class CliApplication
{
    public static CommandApp Create(ICliEnvironment? environment = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICliEnvironment>(environment ?? new SystemCliEnvironment());
        services.AddSingleton<CliPathProvider>();
        services.AddSingleton<IProfileStore, FileProfileStore>();
        services.AddSingleton<IKeyFileProvider, FileKeyFileProvider>();
        services.AddSingleton<ICredentialProtector, AesCredentialProtector>();
        services.AddSingleton<IProfileResolver, ProfileResolver>();
        services.AddSingleton<IOutputRenderer, OutputRenderer>();
        services.AddSingleton<IDiagnosticLogger, DiagnosticLogger>();
        services.AddSingleton<IConfirmationGuard, ConfirmationGuard>();
        services.AddSingleton<IMyJdAuthService, MyJdAuthService>();
        services.AddSingleton<IRequestIdProvider, TimestampRequestIdProvider>();
        services.AddSingleton<IMyJdRelayClient, MyJdRelayClient>();
        services.AddSingleton<IDeviceCatalog, DeviceCatalog>();
        services.AddSingleton<IMyJdTransport, LiveMyJdTransport>();

        var app = new CommandApp(new TypeRegistrar(services));
        app.Configure(config =>
        {
            config.SetApplicationName("jd2");
            config.SetApplicationVersion("0.1.0");

            RegisterAuth(config);
            RegisterDevice(config);
            RegisterDownloads(config);
            RegisterGrabber(config);
            RegisterAccounts(config);
            RegisterExtraction(config);
            RegisterSettings(config);
            RegisterCaptcha(config);
            RegisterEvents(config);
            RegisterSystem(config);
            RegisterAdvanced(config);
            config.AddCommand<DoctorCommand>("doctor").WithDescription("Inspect config paths, resolution, and stored auth state.");
        });

        return app;
    }

    private static void RegisterAuth(IConfigurator config)
    {
        config.AddBranch("auth", auth =>
        {
            auth.SetDescription("Authentication, identity, and saved profiles.");
            auth.AddCommand<LoginCommand>("login").WithDescription("Store encrypted auth material for a profile.");
            auth.AddCommand<LogoutCommand>("logout").WithDescription("Remove stored auth material for the resolved profile.");
            auth.AddCommand<AuthStatusCommand>("status").WithDescription("Show stored auth state for the resolved profile.");
            auth.AddCommand<WhoAmICommand>("whoami").WithDescription("Show the resolved profile and stored account.");
            auth.AddBranch("profiles", profiles =>
            {
                profiles.SetDescription("Manage saved CLI profiles.");
                profiles.AddCommand<ListProfilesCommand>("list");
                profiles.AddCommand<GetProfileCommand>("get");
                profiles.AddCommand<AddProfileCommand>("add");
                profiles.AddCommand<RenameProfileCommand>("rename");
                profiles.AddCommand<RemoveProfileCommand>("remove");
                profiles.AddCommand<UseProfileCommand>("use");
            });
        });
    }

    private static void RegisterDevice(IConfigurator config)
    {
        config.AddBranch("device", device =>
        {
            device.SetDescription("Resolve, inspect, and select JDownloader devices.");
            device.AddCommand<ListDevicesCommand>("list");
            device.AddCommand<GetDeviceCommand>("get");
            device.AddCommand<UseDeviceCommand>("use");
            device.AddCommand<DevicePingCommand>("ping");
            device.AddCommand<DeviceDirectInfoCommand>("direct-info");
        });
    }

    private static void RegisterDownloads(IConfigurator config)
    {
        config.AddBranch("downloads", downloads =>
        {
            downloads.SetDescription("Inspect and control active downloads.");
            downloads.AddCommand<DownloadsStatusCommand>("status");
            downloads.AddCommand<DownloadsSpeedCommand>("speed");
            downloads.AddCommand<DownloadsStartCommand>("start");
            downloads.AddCommand<DownloadsStopCommand>("stop");
            downloads.AddCommand<DownloadsPauseCommand>("pause");
            downloads.AddBranch("links", links =>
            {
                links.AddCommand<DownloadsLinksListCommand>("list");
                links.AddCommand<DownloadsLinksRemoveCommand>("remove");
            });
            downloads.AddBranch("packages", packages =>
            {
                packages.AddCommand<DownloadsPackagesListCommand>("list");
                packages.AddCommand<DownloadsPackagesRemoveCommand>("remove");
            });
            downloads.AddBranch("stopmark", stopmark =>
            {
                stopmark.AddCommand<DownloadsStopmarkGetCommand>("get");
                stopmark.AddCommand<DownloadsStopmarkSetCommand>("set");
                stopmark.AddCommand<DownloadsStopmarkClearCommand>("clear");
            });
        });
    }

    private static void RegisterGrabber(IConfigurator config)
    {
        config.AddBranch("grabber", grabber =>
        {
            grabber.SetDescription("Manage linkgrabber ingestion and staging.");
            grabber.AddCommand<GrabberAddCommand>("add");
            grabber.AddCommand<GrabberAddContainerCommand>("add-container");
            grabber.AddCommand<GrabberClearCommand>("clear");
            grabber.AddCommand<GrabberMoveToDownloadsCommand>("move-to-downloads");
            grabber.AddBranch("links", links =>
            {
                links.AddCommand<GrabberLinksListCommand>("list");
                links.AddCommand<GrabberLinksRemoveCommand>("remove");
            });
            grabber.AddBranch("packages", packages =>
            {
                packages.AddCommand<GrabberPackagesListCommand>("list");
                packages.AddCommand<GrabberPackagesRemoveCommand>("remove");
            });
            grabber.AddBranch("jobs", jobs =>
            {
                jobs.AddCommand<GrabberJobsListCommand>("list");
                jobs.AddCommand<GrabberJobsGetCommand>("get");
            });
            grabber.AddBranch("variants", variants =>
            {
                variants.AddCommand<GrabberVariantsListCommand>("list");
                variants.AddCommand<GrabberVariantsSetCommand>("set");
            });
        });
    }

    private static void RegisterAccounts(IConfigurator config)
    {
        config.AddBranch("accounts", accounts =>
        {
            accounts.SetDescription("Manage premium accounts and basic-auth entries.");
            accounts.AddCommand<AccountsListCommand>("list");
            accounts.AddCommand<AccountsGetCommand>("get");
            accounts.AddCommand<AccountsAddCommand>("add");
            accounts.AddCommand<AccountsUpdateCommand>("update");
            accounts.AddCommand<AccountsEnableCommand>("enable");
            accounts.AddCommand<AccountsDisableCommand>("disable");
            accounts.AddCommand<AccountsRemoveCommand>("remove");
            accounts.AddCommand<AccountsRefreshCommand>("refresh");
            accounts.AddBranch("hosters", hosters =>
            {
                hosters.AddCommand<AccountsHostersListCommand>("list");
                hosters.AddCommand<AccountsHostersUrlsCommand>("urls");
            });
            accounts.AddBranch("basic-auth", basicAuth =>
            {
                basicAuth.AddCommand<AccountsBasicAuthListCommand>("list");
                basicAuth.AddCommand<AccountsBasicAuthAddCommand>("add");
                basicAuth.AddCommand<AccountsBasicAuthUpdateCommand>("update");
                basicAuth.AddCommand<AccountsBasicAuthRemoveCommand>("remove");
            });
        });
    }

    private static void RegisterExtraction(IConfigurator config)
    {
        config.AddBranch("extraction", extraction =>
        {
            extraction.SetDescription("Inspect and control archive extraction.");
            extraction.AddCommand<ExtractionQueueCommand>("queue");
            extraction.AddCommand<ExtractionInfoCommand>("info");
            extraction.AddCommand<ExtractionStartCommand>("start");
            extraction.AddCommand<ExtractionCancelCommand>("cancel");
            extraction.AddCommand<ExtractionAddPasswordCommand>("add-password");
            extraction.AddBranch("settings", settings =>
            {
                settings.AddCommand<ExtractionSettingsGetCommand>("get");
                settings.AddCommand<ExtractionSettingsSetCommand>("set");
            });
        });
    }

    private static void RegisterSettings(IConfigurator config)
    {
        config.AddBranch("settings", settings =>
        {
            settings.SetDescription("Inspect config, plugins, and extensions.");
            settings.AddBranch("config", configBranch =>
            {
                configBranch.AddCommand<SettingsConfigListCommand>("list");
                configBranch.AddCommand<SettingsConfigGetCommand>("get");
                configBranch.AddCommand<SettingsConfigSetCommand>("set");
                configBranch.AddCommand<SettingsConfigResetCommand>("reset");
            });
            settings.AddBranch("plugins", plugins =>
            {
                plugins.AddCommand<SettingsPluginsListCommand>("list");
                plugins.AddCommand<SettingsPluginsGetCommand>("get");
            });
            settings.AddBranch("extensions", extensions =>
            {
                extensions.AddCommand<SettingsExtensionsListCommand>("list");
                extensions.AddCommand<SettingsExtensionsGetCommand>("get");
                extensions.AddCommand<SettingsExtensionsEnableCommand>("enable");
                extensions.AddCommand<SettingsExtensionsDisableCommand>("disable");
            });
        });
    }

    private static void RegisterCaptcha(IConfigurator config)
    {
        config.AddBranch("captcha", captcha =>
        {
            captcha.SetDescription("Inspect and answer captcha jobs.");
            captcha.AddCommand<CaptchaListCommand>("list");
            captcha.AddCommand<CaptchaGetCommand>("get");
            captcha.AddCommand<CaptchaJobCommand>("job");
            captcha.AddCommand<CaptchaSolveCommand>("solve");
            captcha.AddCommand<CaptchaSkipCommand>("skip");
            captcha.AddBranch("forward", forward =>
            {
                forward.AddCommand<CaptchaForwardCreateJobCommand>("create-job");
                forward.AddCommand<CaptchaForwardGetResultCommand>("get-result");
            });
        });
    }

    private static void RegisterEvents(IConfigurator config)
    {
        config.AddBranch("events", events =>
        {
            events.SetDescription("Inspect and manage event subscriptions.");
            events.AddCommand<EventsPublishersCommand>("publishers");
            events.AddCommand<EventsSubscribeCommand>("subscribe");
            events.AddCommand<EventsSetCommand>("set");
            events.AddCommand<EventsRemoveCommand>("remove");
            events.AddCommand<EventsStatusCommand>("status");
            events.AddCommand<EventsListenCommand>("listen");
            events.AddCommand<EventsPollCommand>("poll");
        });
    }

    private static void RegisterSystem(IConfigurator config)
    {
        config.AddBranch("system", system =>
        {
            system.SetDescription("JDownloader, OS, and update operations.");
            system.AddCommand<SystemInfoCommand>("info");
            system.AddCommand<SystemStorageCommand>("storage");
            system.AddCommand<SystemReconnectCommand>("reconnect");
            system.AddBranch("jd", jd =>
            {
                jd.AddCommand<SystemJdVersionCommand>("version");
                jd.AddCommand<SystemJdRevisionCommand>("revision");
                jd.AddCommand<SystemJdUptimeCommand>("uptime");
                jd.AddCommand<SystemJdRefreshPluginsCommand>("refresh-plugins");
                jd.AddCommand<SystemJdRestartCommand>("restart");
                jd.AddCommand<SystemJdExitCommand>("exit");
            });
            system.AddBranch("os", os =>
            {
                os.AddCommand<SystemOsShutdownCommand>("shutdown");
                os.AddCommand<SystemOsHibernateCommand>("hibernate");
                os.AddCommand<SystemOsStandbyCommand>("standby");
            });
            system.AddBranch("update", update =>
            {
                update.AddCommand<SystemUpdateCheckCommand>("check");
                update.AddCommand<SystemUpdateRunCommand>("run");
                update.AddCommand<SystemUpdateRestartCommand>("restart");
            });
            system.AddCommand<SystemToggleCommand>("toggle");
        });
    }

    private static void RegisterAdvanced(IConfigurator config)
    {
        config.AddBranch("advanced", advanced =>
        {
            advanced.SetDescription("Expert-only escape hatches and raw access.");
            advanced.AddBranch("content", content =>
            {
                content.AddCommand<AdvancedContentIconCommand>("icon");
                content.AddCommand<AdvancedContentFavIconCommand>("favicon");
                content.AddCommand<AdvancedContentFileIconCommand>("file-icon");
                content.AddCommand<AdvancedContentDescribeIconCommand>("describe");
            });
            advanced.AddBranch("dialogs", dialogs =>
            {
                dialogs.AddCommand<AdvancedDialogsListCommand>("list");
                dialogs.AddCommand<AdvancedDialogsGetCommand>("get");
                dialogs.AddCommand<AdvancedDialogsAnswerCommand>("answer");
                dialogs.AddCommand<AdvancedDialogsTypeInfoCommand>("type-info");
            });
            advanced.AddBranch("ui", ui =>
            {
                ui.AddCommand<AdvancedUiRefreshCommand>("refresh");
                ui.AddCommand<AdvancedUiFocusCommand>("focus");
            });
            advanced.AddBranch("ingest", ingest =>
            {
                ingest.AddCommand<AdvancedIngestCnlCommand>("cnl");
                ingest.AddCommand<AdvancedIngestFlashCommand>("flash");
            });
            advanced.AddBranch("raw", raw =>
            {
                raw.AddCommand<AdvancedRawRequestCommand>("request");
            });
        });
    }
}
