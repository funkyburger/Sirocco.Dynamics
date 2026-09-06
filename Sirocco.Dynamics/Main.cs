using Microsoft.Extensions.Logging;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sirocco.Dynamics
{
    internal interface IMain
    {
        Task Run();
    }

    internal class Main : IMain
    {
        private readonly ILogger<Main> _logger;
        private readonly IOrganizationService _organizationService;

        public Main(ILogger<Main> logger, IOrganizationService organizationService)
        {
            _logger = logger;
            _organizationService = organizationService;
        }

        public Task Run()
        {
            _logger.LogInformation("Job started.");

            QueryExpression query = new("Account")
            {
                TopCount = 5
            };

            query.ColumnSet.AddColumn("Name");

            _organizationService.Create(new Entity("Account")
            {
                Attributes = new AttributeCollection() {
                    { "Name", "John Doe" }
                }
            });

            var accounts = _organizationService.RetrieveMultiple(query);

            var account = accounts[0];

            return Task.CompletedTask;
        }
    }
}
