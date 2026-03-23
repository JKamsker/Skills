using JDownloader.Cli.Runtime;
using JDownloader.Cli.Transport;
using Spectre.Console.Cli;
using System.Text.Json;

namespace JDownloader.Cli.Commands.Shared;

public abstract class RequestPlanCommandBase : DeviceApiCommand<RequestCommandSettings>
{
    private readonly IMyJdTransport _transport;
    private readonly IConfirmationGuard _confirmationGuard;

    protected RequestPlanCommandBase(
        IProfileResolver profileResolver,
        IOutputRenderer outputRenderer,
        IDiagnosticLogger diagnosticLogger,
        IMyJdTransport transport,
        IConfirmationGuard confirmationGuard)
        : base(profileResolver, outputRenderer, diagnosticLogger)
    {
        _transport = transport;
        _confirmationGuard = confirmationGuard;
    }

    protected abstract MyJdRequestPlan CreatePlan(CommandContext context, RequestCommandSettings settings, ResolvedProfileContext resolved);

    protected override async Task<CommandOutput> ExecuteCoreAsync(CommandContext context, RequestCommandSettings settings, ResolvedProfileContext resolved, CancellationToken cancellationToken)
    {
        var plan = CreatePlan(context, settings, resolved);
        if (plan.Destructive)
        {
            var proceed = await _confirmationGuard.AuthorizeAsync(
                settings,
                $"'{context.Name}' is destructive.",
                () => Task.FromResult(BuildPreviewOutput(resolved, plan)));
            if (!proceed)
                return new CommandOutput(new { preview = true });
        }
        else if (settings.DryRun)
        {
            return BuildPreviewOutput(resolved, plan);
        }

        var result = await _transport.ExecuteAsync(resolved, plan, cancellationToken);
        return new CommandOutput(
            result.Data,
            RenderHumanData(result.Data),
            result.Warnings);
    }

    public static CommandOutput BuildPreviewOutput(ResolvedProfileContext resolved, MyJdRequestPlan plan)
    {
        var data = new
        {
            action = "dry-run",
            profile = resolved.ProfileName,
            device = resolved.Device is null ? null : new { id = resolved.Device.Id, name = resolved.Device.Name },
            plan.Operation,
            plan.Method,
            plan.Endpoint,
            plan.Query,
            plan.Body,
            plan.Destructive,
            plan.ProducesBinary,
        };

        return new CommandOutput(
            data,
            [
                "Dry-run only. No changes were applied.",
                $"Profile: {resolved.ProfileName}",
                $"Device: {resolved.Device?.DisplayValue ?? "(none)"}",
                $"Method: {plan.Method}",
                $"Endpoint: {plan.Endpoint}",
            ]);
    }

    protected static Dictionary<string, object?> BuildSelectorQuery(RequestCommandSettings settings)
    {
        var query = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(settings.Fields))
            query["fields"] = settings.Fields.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (settings.Limit is not null)
            query["limit"] = settings.Limit;
        if (settings.Offset is not null)
            query["offset"] = settings.Offset;
        if (settings.LinkIds.Length > 0)
            query["linkIds"] = settings.LinkIds;
        if (settings.PackageIds.Length > 0)
            query["packageIds"] = settings.PackageIds;
        if (settings.Hosters.Length > 0)
            query["hosters"] = settings.Hosters;

        var queryOverride = JsonInput.ParseOptional(settings.QueryJson);
        if (queryOverride is not null)
            query["queryOverride"] = queryOverride;

        return query;
    }

    protected static object? BuildBody(RequestCommandSettings settings)
    {
        return JsonInput.ParseOptional(settings.BodyJson);
    }

    private static IReadOnlyList<string> RenderHumanData(object data)
    {
        return JsonSerializer.Serialize(
                data,
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true,
                })
            .Split(Environment.NewLine);
    }
}

public abstract class FixedRequestPlanCommand : RequestPlanCommandBase
{
    protected FixedRequestPlanCommand(
        IProfileResolver profileResolver,
        IOutputRenderer outputRenderer,
        IDiagnosticLogger diagnosticLogger,
        IMyJdTransport transport,
        IConfirmationGuard confirmationGuard)
        : base(profileResolver, outputRenderer, diagnosticLogger, transport, confirmationGuard)
    {
    }

    protected abstract string Operation { get; }
    protected abstract string Endpoint { get; }
    protected virtual string Method => "POST";
    protected virtual bool Destructive => false;
    protected virtual bool ProducesBinary => false;

    protected override MyJdRequestPlan CreatePlan(CommandContext context, RequestCommandSettings settings, ResolvedProfileContext resolved)
    {
        return new MyJdRequestPlan(
            Operation,
            Method,
            Endpoint,
            BuildSelectorQuery(settings),
            BuildBody(settings),
            Destructive,
            ProducesBinary,
            resolved.Device?.Id);
    }
}
