using JDownloader.Cli.Commands.Shared;
using JDownloader.Cli.Runtime;
using JDownloader.Cli.Transport;

namespace JDownloader.Cli.Commands.Events;

public abstract class EventsCommandBase : FixedRequestPlanCommand
{
    protected EventsCommandBase(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { }
}

public sealed class EventsPublishersCommand : EventsCommandBase { public EventsPublishersCommand(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { } protected override string Operation => "events.publishers"; protected override string Endpoint => "/events/listpublisher"; }
public sealed class EventsSubscribeCommand : EventsCommandBase { public EventsSubscribeCommand(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { } protected override string Operation => "events.subscribe"; protected override string Endpoint => "/events/subscribe"; protected override bool Destructive => true; }
public sealed class EventsSetCommand : EventsCommandBase { public EventsSetCommand(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { } protected override string Operation => "events.set"; protected override string Endpoint => "/events/setsubscription"; protected override bool Destructive => true; }
public sealed class EventsRemoveCommand : EventsCommandBase { public EventsRemoveCommand(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { } protected override string Operation => "events.remove"; protected override string Endpoint => "/events/removesubscription"; protected override bool Destructive => true; }
public sealed class EventsStatusCommand : EventsCommandBase { public EventsStatusCommand(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { } protected override string Operation => "events.status"; protected override string Endpoint => "/events/subscriptionstatus"; }
public sealed class EventsListenCommand : EventsCommandBase { public EventsListenCommand(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { } protected override string Operation => "events.listen"; protected override string Endpoint => "/events/listen"; }
public sealed class EventsPollCommand : EventsCommandBase { public EventsPollCommand(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { } protected override string Operation => "events.poll"; protected override string Endpoint => "/events/poll"; }
