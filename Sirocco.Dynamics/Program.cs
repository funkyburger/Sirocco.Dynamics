using Microsoft.Extensions.DependencyInjection;
using Sirocco.Dynamics;
using Microsoft.Extensions.Logging;
using Microsoft.Xrm.Sdk;

var services = new ServiceCollection();
services.AddScoped<IMain, Main>();
services.AddScoped<IOrganizationService, MockOrganizationService>();
services.AddLogging(configure => configure.AddConsole());
var serviceProvider = services.BuildServiceProvider();

await serviceProvider.GetService<IMain>()!.Run();