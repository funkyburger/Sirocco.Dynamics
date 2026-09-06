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
    internal interface IAccountRepository
    {
        Account GetById(Guid accountId, bool lazy = false);
        Guid Create(Account account);
        void Update(Account account);
        IList<Account> GetAll();
    }

    internal class AccountRepository : IAccountRepository
    {
        private readonly ILogger<AccountRepository> _logger;
        private readonly IOrganizationService _organizationService;

        public AccountRepository(ILogger<AccountRepository> logger, IOrganizationService organizationService)
        {
            _logger = logger;
            _organizationService = organizationService;
        }

        public Account GetById(Guid accountId, bool lazy = false)
        {
            var accountEntity = _organizationService.Retrieve("Account", accountId, new ColumnSet(new string[] { "Name" }));

            return new Account() { 
                Id = accountEntity.Id,
                Name = accountEntity.GetAttributeValue<string>("Name"),
                Notes = lazy ? new List<Note>() : RetrieveRelatedNotes(accountId).ToList(),
                Parent = FetchAccount(accountEntity.Id)
            };
        }

        public Guid Create(Account account)
        {
            var accountId = _organizationService.Create(new Entity("Account")
            {
                Attributes = new AttributeCollection() {
                    { "Name", account.Name },
                    { "ParentId", account.Parent?.Id }
                }
            });

            foreach (var note in account.Notes)
            {
                var noteId = _organizationService.Create(new Entity("Note")
                {
                    Attributes = new AttributeCollection() {
                        { "Text", note.Text },
                        { "AccountId", accountId }
                    }
                });

                var savedNote = _organizationService.Retrieve("Note", noteId, new ColumnSet("Text"));

                _organizationService.Associate("Account", accountId, new Relationship("account_notes"), new EntityReferenceCollection(new List<EntityReference> { savedNote.ToEntityReference() }));
            }

            RetrieveRelatedNotes(accountId);

            return accountId;
        }

        public void Update(Account account)
        {
            var originalAccount = _organizationService.Retrieve("Account", account.Id, new ColumnSet(new string[] { "Name", "ParentId" }));
            originalAccount["Name"] = account.Name;
            originalAccount["ParentId"] = account.Parent?.Id;

            _organizationService.Update(originalAccount);
        }

        public IList<Account> GetAll()
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Note> RetrieveRelatedNotes(Guid accountId)
        {
            var filter = new FilterExpression();
            filter.Conditions.Add(new ConditionExpression("AccountId", ConditionOperator.Equal, accountId));
            QueryExpression relatedNotesQuery = new("Note")
            {
                TopCount = 100
            };
            relatedNotesQuery.ColumnSet.AddColumns("Text");
            relatedNotesQuery.Criteria.AddFilter(filter);

            var relatedNotes = _organizationService.RetrieveMultiple(relatedNotesQuery);

            foreach (var relatedNote in relatedNotes.Entities)
            {
                yield return new Note()
                {
                    Id = relatedNote.Id,
                    Text = relatedNote.GetAttributeValue<string>("Text")
                };
            }
        }

        private Account FetchAccount(Guid accountId)
        {
            var originalAccount = _organizationService.Retrieve("Account", accountId, new ColumnSet(new string[] { "Name", "ParentId" }));

            return new Account()
            {
                Name = originalAccount.GetAttributeValue<string>("Name")
            };
        }
    }
}
