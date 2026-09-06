using Microsoft.Extensions.Logging;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Sirocco.Dynamics.Model;
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
        private readonly IAccountRepository _accountRepository;

        public Main(ILogger<Main> logger, IOrganizationService organizationService, IAccountRepository accountRepository)
        {
            _logger = logger;
            _organizationService = organizationService;
            _accountRepository = accountRepository;
        }

        public Task Run()
        {
            _logger.LogInformation("Job started.");

            var account1 = new Account()
            {
                Name = "Parent Account",
            };

            var account2 = new Account()
            {
                Name = "Under account",
            };

            var account1Id = _accountRepository.Create(account1);
            var account2Id = _accountRepository.Create(account2);

            return Task.CompletedTask;
        }
    }
}
