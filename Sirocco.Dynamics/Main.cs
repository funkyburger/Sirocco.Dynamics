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
                Name = "Peter Stuff",
                Notes = new List<Note> {
                    new Note() { Text = "Stuffed note 1" },
                    new Note() { Text = "Another stuffed note" }
                },
                Contacts = new List<Contact>
                {
                    new Contact { Name = "Contact A", PhoneNumber = "111-222-3333" },
                    new Contact { Name = "Contact B", PhoneNumber = "444-555-6666" }
                }
            };

            var account2 = new Account()
            {
                Name = "Henry Winkler",
                Notes = new List<Note> {
                    new Note() { Text = "Winkly note 1" },
                    new Note() { Text = "Another winkly note" },
                    new Note() { Text = "And a third one" }
                }
            };

            var account1Id = _accountRepository.Create(account1);
            var account2Id = _accountRepository.Create(account2);

            account1 = _accountRepository.GetById(account1Id);
            account2 = _accountRepository.GetById(account2Id);

            account2.Name = "Henry Winkler (updated)";
            account2.Parent = account1;
            account2.Contacts = new List<Contact>
            {
                new Contact { Name = "Contact 1", PhoneNumber = "123-456-7890" },
                new Contact { Name = "Contact 2", PhoneNumber = "987-654-3210" }
            };

            _accountRepository.Update(account2);

            account2 = _accountRepository.GetById(account2Id);

            if(account2.Parent != null)
            {
                _logger.LogInformation($"{account2.Name} has parent.");
            }
            
            _logger.LogInformation($"{account2.Name}:{string.Join(",", account2.Notes.Select(n => n.Text))} ; {string.Join(",", account2.Contacts.Select(c => c.Name))}");

            return Task.CompletedTask;
        }
    }
}
