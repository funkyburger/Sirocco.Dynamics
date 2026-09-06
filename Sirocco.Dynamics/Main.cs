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

            SetParentRelation(account1Id, account2Id);

            LogAccountDetails(_accountRepository.GetById(account1Id));
            LogAccountDetails(_accountRepository.GetById(account2Id));

            return Task.CompletedTask;
        }

        private void SetParentRelation(Guid account1Id, Guid account2Id)
        {
            var account1 = _accountRepository.GetById(account1Id);
            var account2 = _accountRepository.GetById(account2Id);

            account2.Parent = account1;

            _accountRepository.Update(account2);
        }

        private void LogAccountDetails(Account account)
        {
            _logger.LogInformation($"Account ID: {account.Id}, Name: {account.Name}, Parent:{account.Parent?.Name} \n"
                + $"Notes:{string.Join(", ", account.Notes.Select(n => $"'{n.Text}'"))}");
                //+ $"Contacts:{string.Join(", ", $"'{account.Notes}'")}");

        }
    }
}
