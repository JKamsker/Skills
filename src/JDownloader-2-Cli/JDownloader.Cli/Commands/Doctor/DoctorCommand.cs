using JDownloader.Cli.Commands.Shared;
using JDownloader.Cli.Config;
using JDownloader.Cli.Runtime;
using Spectre.Console.Cli;

namespace JDownloader.Cli.Commands.Doctor;

public sealed class DoctorCommand : AnonymousCommand<NoArgSettings>
{
    private readonly CliPathProvider _paths;
    private readonly IProfileStore _profileStore;
    private readonly IProfileResolver _profileResolver;
    private readonly IKeyFileProvider _keyFileProvider;

    public DoctorCommand(
        CliPathProvider paths,
        IProfileStore profileStore,
        IProfileResolver profileResolver,
        IKeyFileProvider keyFileProvider,
        IOutputRenderer outputRenderer,
        IDiagnosticLogger diagnosticLogger)
        : base(outputRenderer, diagnosticLogger)
    {
        _paths = paths;
        _profileStore = profileStore;
        _profileResolver = profileResolver;
        _keyFileProvider = keyFileProvider;
    }

    protected override async Task<CommandOutput> ExecuteCoreAsync(CommandContext context, NoArgSettings settings, CancellationToken cancellationToken)
    {
        var config = await _profileStore.LoadAsync(cancellationToken);
        ResolvedProfileContext? resolved = null;
        try
        {
            resolved = await _profileResolver.ResolveAsync(settings, requireDevice: false, cancellationToken);
        }
        catch
        {
            // doctor should still print config paths even when profile resolution fails
        }

        var data = new
        {
            classification = "service-native",
            configRoot = _paths.GetConfigRoot(),
            configFile = _paths.GetConfigFilePath(),
            keyFile = _keyFileProvider.GetKeyFilePath(),
            profiles = config.Profiles.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray(),
            config.DefaultProfile,
            resolvedProfile = resolved?.ProfileName,
            resolvedDevice = resolved?.Device is null ? null : new { resolved.Device.Id, resolved.Device.Name },
            resolvedOutputMode = resolved?.OutputMode.ToString(),
        };

        return new CommandOutput(
            data,
            [
                "CLI classification: service-native",
                $"Config root: {_paths.GetConfigRoot()}",
                $"Config file: {_paths.GetConfigFilePath()}",
                $"Key file: {_keyFileProvider.GetKeyFilePath()}",
                $"Profiles: {config.Profiles.Count}",
                $"Default profile: {config.DefaultProfile ?? "(none)"}",
                $"Resolved profile: {resolved?.ProfileName ?? "(none)"}",
                $"Resolved device: {resolved?.Device?.DisplayValue ?? "(none)"}",
            ]);
    }
}
