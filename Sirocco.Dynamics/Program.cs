using Microsoft.Extensions.DependencyInjection;
using Sirocco.Dynamics;
using Microsoft.Extensions.Logging;

var services = new ServiceCollection();
services.AddScoped<IMain, Main>();
var serviceProvider = services.BuildServiceProvider();

await serviceProvider.GetService<IMain>()!.Run();