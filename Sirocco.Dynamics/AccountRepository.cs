using Microsoft.Extensions.Logging;
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
        void Create(Account account);
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

        public void Create(Account account)
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
        }

        public void Update(Account account)
        {
            throw new NotImplementedException();
        }

        public IList<Account> GetAll()
        {
            throw new NotImplementedException();
        }
    }
}
