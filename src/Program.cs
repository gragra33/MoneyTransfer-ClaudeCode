using Blazing.Extensions.DependencyInjection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using MoneyTransfer.Demo;

// ── DI Setup ────────────────────────────────────────────────────────────────
var services = new ServiceCollection();
services.AddSingleton<DemoTimeProvider>();
services.AddSingleton<TimeProvider>(serviceProvider => serviceProvider.GetRequiredService<DemoTimeProvider>());
services.AddValidatorsFromAssemblyContaining<Program>(ServiceLifetime.Singleton);
services.Register();  // scans [AutoRegister] attributes in this assembly

var provider = services.BuildServiceProvider();
var demoRunner = provider.GetRequiredService<IDemoRunner>();

// ── Run Demo Scenarios ───────────────────────────────────────────────────────
demoRunner.Run();

