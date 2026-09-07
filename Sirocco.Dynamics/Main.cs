using FakeItEasy;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Sirocco.Dynamics.Model;
using System;
using System.Collections.Generic;
using System.Security.Principal;
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

            // 7. Create two accounts and let the first one be the parent of the second one.
            SetParentRelation(account1Id, account2Id);

            // 8. Create two contacts and associate the first one with the first account and the second one with the second account
            SetContacts(account1Id, account2Id);

            // 9. Update fields in one account and one contact
            UpdateAccountAndContact(account1Id, account2Id);

            // 10. Create a note and associate it with the parent account
            CreateNoteForAccount(account1Id, account2Id);

            // 11. Create two notes and associate them with the second contact
            AddOtherNotes(account1Id, account2Id);

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

        private void SetContacts(Guid account1Id, Guid account2Id)
        {
            var account1 = _accountRepository.GetById(account1Id);
            var account2 = _accountRepository.GetById(account2Id);

            account1.Contacts.Add(new Contact() { Name = "Contact 1", PhoneNumber = "123-456-7890" });
            account2.Contacts.Add(new Contact() { Name = "Contact 2", PhoneNumber = "987-654-3210" });

            _accountRepository.Update(account1);
            _accountRepository.Update(account2);
        }

        private void UpdateAccountAndContact(Guid account1Id, Guid account2Id)
        {
            var account1 = _accountRepository.GetById(account1Id);
            var account2 = _accountRepository.GetById(account2Id);

            account1.Name = "Updated Parent Account";
            account2.Contacts.First().PhoneNumber = "111-222-3333";

            _accountRepository.Update(account1);
            _accountRepository.Update(account2);
        }

        private void CreateNoteForAccount(Guid account1Id, Guid account2Id)
        {
            var account1 = _accountRepository.GetById(account1Id);
            var account2 = _accountRepository.GetById(account2Id);

            account1.Notes.Add(new Note() { Text = "This is a note for the parent account." });

            _accountRepository.Update(account1);
        }

        private void AddOtherNotes(Guid account1Id, Guid account2Id)
            => PerformUpdate(account1Id, account2Id, (account1, account2) =>
            {
                account2.Contacts.First().Notes.Add(new Note() { Text = "Contact note 1." });
                account2.Contacts.First().Notes.Add(new Note() { Text = "Contact note 2." });
            });

        private void PerformUpdate(Guid account1Id, Guid account2Id, Action<Account, Account> updateAction)
        {
            var account1 = _accountRepository.GetById(account1Id);
            var account2 = _accountRepository.GetById(account2Id);

            updateAction(account1, account2);

            _accountRepository.Update(account1);
            _accountRepository.Update(account2);
        }

        private void LogAccountDetails(Account account)
        {
            _logger.LogInformation($"Account ID: {account.Id}, Name: {account.Name}, Parent:{account.Parent?.Name} \n"
                + $"Notes:{string.Join(", ", account.Notes.Select(n => $"'{n.Text}'"))} \n"
                + $"Contacts:{string.Join(", ", account.Contacts.Select(c => $"'{c.Name} ({c.PhoneNumber})'"))}");
        }
    }
}
