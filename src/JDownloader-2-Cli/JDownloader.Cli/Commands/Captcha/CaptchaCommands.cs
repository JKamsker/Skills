using JDownloader.Cli.Commands.Shared;
using JDownloader.Cli.Runtime;
using JDownloader.Cli.Transport;

namespace JDownloader.Cli.Commands.Captcha;

public abstract class CaptchaCommandBase : FixedRequestPlanCommand
{
    protected CaptchaCommandBase(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { }
}

public sealed class CaptchaListCommand : CaptchaCommandBase { public CaptchaListCommand(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { } protected override string Operation => "captcha.list"; protected override string Endpoint => "/captcha/list"; }
public sealed class CaptchaGetCommand : CaptchaCommandBase { public CaptchaGetCommand(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { } protected override string Operation => "captcha.get"; protected override string Endpoint => "/captcha/get"; }
public sealed class CaptchaJobCommand : CaptchaCommandBase { public CaptchaJobCommand(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { } protected override string Operation => "captcha.job"; protected override string Endpoint => "/captcha/getCaptchaJob"; }
public sealed class CaptchaSolveCommand : CaptchaCommandBase { public CaptchaSolveCommand(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { } protected override string Operation => "captcha.solve"; protected override string Endpoint => "/captcha/solve"; protected override bool Destructive => true; }
public sealed class CaptchaSkipCommand : CaptchaCommandBase { public CaptchaSkipCommand(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { } protected override string Operation => "captcha.skip"; protected override string Endpoint => "/captcha/skip"; protected override bool Destructive => true; }
public sealed class CaptchaForwardCreateJobCommand : CaptchaCommandBase { public CaptchaForwardCreateJobCommand(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { } protected override string Operation => "captcha.forward.create-job"; protected override string Endpoint => "/captchaforward/createJobRecaptchaV2"; protected override bool Destructive => true; }
public sealed class CaptchaForwardGetResultCommand : CaptchaCommandBase { public CaptchaForwardGetResultCommand(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { } protected override string Operation => "captcha.forward.get-result"; protected override string Endpoint => "/captchaforward/getResult"; }
