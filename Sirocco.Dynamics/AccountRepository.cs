using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Sirocco.Dynamics.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sirocco.Dynamics
{
    internal interface IAccountRepository
    {
        Account GetById(Guid accountId);
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

        public Account GetById(Guid accountId)
        {
            var accountEntity = _organizationService.Retrieve("Account", accountId, new ColumnSet(new string[] { "Name" }));

            return new Account() { 
                Id = accountEntity.Id,
                Name = accountEntity.GetAttributeValue<string>("Name"),
                Notes = RetrieveRelatedNotes(accountId).ToList()
            };
        }

        public Guid Create(Account account)
        {
            var accountId = _organizationService.Create(new Entity("Account")
            {
                Attributes = new AttributeCollection() {
                    { "Name", account.Name }
                }
            });

            foreach (var note in account.Notes)
            {
                var noteId = _organizationService.Create(new Entity("Note")
                {
                    Attributes = new AttributeCollection() {
                        { "Text", note.Text }
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
            throw new NotImplementedException();
        }

        public IList<Account> GetAll()
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Note> RetrieveRelatedNotes(Guid accountId)
        {
            var filter = new FilterExpression();
            filter.Conditions.Add(new ConditionExpression("Accountid", ConditionOperator.Equal, accountId));
            QueryExpression relationQuery = new("AccountNotes")
            {
                TopCount = 100
            };
            relationQuery.ColumnSet.AddColumns("Noteid");
            relationQuery.Criteria.AddFilter(filter);

            var relatedNotes = _organizationService.RetrieveMultiple(relationQuery);

            foreach (var relation in relatedNotes.Entities)
            {
                var noteId = relation.GetAttributeValue<Guid>("Noteid");
                var note = _organizationService.Retrieve("Note", noteId, new ColumnSet("Text"));

                yield return new Note()
                {
                    Id = noteId,
                    Text = note.GetAttributeValue<string>("Text")
                };
            }
        }
    }
}
