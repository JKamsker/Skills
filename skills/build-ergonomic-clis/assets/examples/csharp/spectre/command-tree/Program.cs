using ExampleCli.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

var services = new ServiceCollection();
services.AddSingleton<TargetResolver>();
services.AddSingleton<DangerousActionGuard>();
services.AddSingleton<DiagnosticLogger>();

// Reuse your usual Spectre type registrar here.
var app = new CommandApp(new TypeRegistrar(services));

app.Configure(config =>
{
    config.SetApplicationName("example");
    config.SetApplicationVersion("0.1.0");

    config.AddBranch("auth", auth =>
    {
        auth.SetDescription("Sign in, inspect identity, and manage saved sessions");
        auth.AddCommand<LoginCommand>("login").WithDescription("Authenticate for interactive or scripted use");
        auth.AddCommand<WhoAmICommand>("whoami").WithDescription("Show the active user and resolved target");
        auth.AddCommand<LogoutCommand>("logout").WithDescription("Delete the saved session for the current host");
    });

    config.AddBranch("projects", projects =>
    {
        projects.SetDescription("List, inspect, and update projects");
        projects.SetDefaultCommand<ProjectsListCommand>();
        projects.AddCommand<ProjectsListCommand>("list").WithDescription("List projects for the resolved target");
        projects.AddCommand<ProjectsShowCommand>("show").WithDescription("Show one project");
        projects.AddCommand<ProjectsDeleteCommand>("delete").WithDescription("Delete one project");
    });

    config.AddBranch("server", server =>
    {
        server.SetDescription("Health, diagnostics, and configuration");
        server.AddCommand<PingCommand>("ping").WithDescription("Check connectivity");
        server.AddCommand<ConfigShowCommand>("config").WithDescription("Show the resolved context and config source");
        server.AddCommand<LogsTailCommand>("logs").WithDescription("Stream logs to stdout and diagnostics to stderr");
    });
});

return await app.RunAsync(args);
