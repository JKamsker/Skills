using JDownloader.Cli.Commands.Shared;
using JDownloader.Cli.Runtime;
using JDownloader.Cli.Transport;

namespace JDownloader.Cli.Commands.Settings;

public abstract class SettingsCommandBase : FixedRequestPlanCommand
{
    protected SettingsCommandBase(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { }
}

public sealed class SettingsConfigListCommand : SettingsCommandBase { public SettingsConfigListCommand(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { } protected override string Operation => "settings.config.list"; protected override string Endpoint => "/config/list"; }
public sealed class SettingsConfigGetCommand : SettingsCommandBase { public SettingsConfigGetCommand(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { } protected override string Operation => "settings.config.get"; protected override string Endpoint => "/config/get"; }
public sealed class SettingsConfigSetCommand : SettingsCommandBase { public SettingsConfigSetCommand(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { } protected override string Operation => "settings.config.set"; protected override string Endpoint => "/config/set"; protected override bool Destructive => true; }
public sealed class SettingsConfigResetCommand : SettingsCommandBase { public SettingsConfigResetCommand(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { } protected override string Operation => "settings.config.reset"; protected override string Endpoint => "/config/reset"; protected override bool Destructive => true; }
public sealed class SettingsPluginsListCommand : SettingsCommandBase { public SettingsPluginsListCommand(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { } protected override string Operation => "settings.plugins.list"; protected override string Endpoint => "/plugins/list"; }
public sealed class SettingsPluginsGetCommand : SettingsCommandBase { public SettingsPluginsGetCommand(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { } protected override string Operation => "settings.plugins.get"; protected override string Endpoint => "/plugins/query"; }
public sealed class SettingsExtensionsListCommand : SettingsCommandBase { public SettingsExtensionsListCommand(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { } protected override string Operation => "settings.extensions.list"; protected override string Endpoint => "/extensions/list"; }
public sealed class SettingsExtensionsGetCommand : SettingsCommandBase { public SettingsExtensionsGetCommand(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { } protected override string Operation => "settings.extensions.get"; protected override string Endpoint => "/extensions/list"; }
public sealed class SettingsExtensionsEnableCommand : SettingsCommandBase { public SettingsExtensionsEnableCommand(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { } protected override string Operation => "settings.extensions.enable"; protected override string Endpoint => "/extensions/install"; protected override bool Destructive => true; }
public sealed class SettingsExtensionsDisableCommand : SettingsCommandBase { public SettingsExtensionsDisableCommand(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { } protected override string Operation => "settings.extensions.disable"; protected override string Endpoint => "/extensions/uninstall"; protected override bool Destructive => true; }
