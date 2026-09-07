using FakeXrmEasy.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Sirocco.Dynamics.Model;
using System;
using System.Collections;
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
            var accountEntity = _organizationService.Retrieve("Account", accountId, new ColumnSet(new string[] { "Name", "ParentId" }));

            return new Account() { 
                Id = accountEntity.Id,
                Name = accountEntity.GetAttributeValue<string>("Name"),
                Notes = lazy ? new List<Note>() : RetrieveAccountNotes(accountId).ToList(),
                Contacts = lazy ? new List<Contact>() : RetrieveRelatedContacts(accountId).ToList(),
                Parent = lazy ? null : FetchAccount(accountEntity.GetAttributeValue<Guid>("ParentId"))
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

            foreach (var contact in account.Contacts)
            {
                var contactId = _organizationService.Create(new Entity("Contact")
                {
                    Attributes = new AttributeCollection() {
                        { "Name", contact.Name },
                        { "PhoneNumber", contact.PhoneNumber },
                        { "AccountId", accountId }
                    }
                });

                foreach(var note in contact.Notes)
                {
                    if (note.Id != default)
                    {
                        var originalNote = _organizationService.Retrieve("Note", note.Id, new ColumnSet("Text", "ContactId"));
                        originalNote["Text"] = note.Text;
                    }
                    else
                    {
                        _organizationService.Create(new Entity("Note") {
                            Attributes = new AttributeCollection() {
                                { "Text", note.Text },
                                { "ContactId", contactId }
                            }
                        });
                    }
                }
            }

            return accountId;
        }

        public void Update(Account account)
        {
            var originalAccount = _organizationService.Retrieve("Account", account.Id, new ColumnSet(new string[] { "Name", "ParentId" }));
            originalAccount["Name"] = account.Name;
            originalAccount["ParentId"] = account.Parent?.Id;

            foreach (var note in account.Notes)
            {
                if (note.Id != default)
                {
                    var originalContact = _organizationService.Retrieve("Note", note.Id, new ColumnSet(new string[] { "Text", "AccountId" }));
                    originalContact["Text"] = note.Text;

                    _organizationService.Update(originalContact);
                }
                else
                {
                    _organizationService.Create(new Entity("Note")
                    {
                        Attributes = new AttributeCollection() {
                        { "Text", note.Text },
                        { "AccountId", account.Id }
                    }});
                }
            }

            foreach (var contact in account.Contacts)
            {
                if(contact.Id != default)
                {
                    var originalContact = _organizationService.Retrieve("Contact", contact.Id, new ColumnSet(new string[] { "Name", "ParentId" }));
                    originalContact["Name"] = contact.Name;
                    originalContact["PhoneNumber"] = contact.PhoneNumber;

                    _organizationService.Update(originalContact);
                } 
                else
                {
                    _organizationService.Create(new Entity("Contact") {
                        Attributes = new AttributeCollection() {
                        { "Name", contact.Name },
                        { "PhoneNumber", contact.PhoneNumber },
                        { "AccountId", account.Id }
                    }});
                }

                foreach (var note in contact.Notes)
                {
                    if (note.Id != default)
                    {
                        var originalNote = _organizationService.Retrieve("Note", note.Id, new ColumnSet(new string[] { "Text", "ContactId" }));
                        originalNote["Text"] = note.Text;
                        _organizationService.Update(originalNote);
                    }
                    else
                    {
                        _organizationService.Create(new Entity("Note") {
                            Attributes = new AttributeCollection() {
                                { "Text", note.Text },
                                { "ContactId", contact.Id }
                            }
                        });
                    }
                }
            }

            _organizationService.Update(originalAccount);
        }

        public IList<Account> GetAll()
        {
            var results = FetchAllAccounts();

            return BuildFromResult(results);
        }

        private IList<Account> BuildFromResult(EntityCollection entityCollection)
        {
            Dictionary<Guid, Account> accounts = new();
            Dictionary<Guid, Contact> contacts = new();
            Dictionary<Guid, Note> notes = new();
            Dictionary<Guid, Guid> contactToAccountMap = new();
            Dictionary<Guid, Guid> notesToContactMap = new();

            foreach (var entity in entityCollection.Entities)
            {
                Account account;
                Contact contact;
                Note note;

                if (entity.Id == default)
                {
                    throw new NullReferenceException("entity.Id");
                }

                if (!accounts.TryGetValue(entity.Id, out account))
                {
                    account = new Account()
                    {
                        Id = entity.Id,
                        Name = entity.GetAttributeValue<string>("Name")
                    };

                    accounts.Add(account.Id, account);
                }

                var contactId = (Guid)entity.GetAttributeValue<AliasedValue>("contact.Contactid").Value;
                if (!contacts.TryGetValue(contactId, out contact))
                {
                    contact = new Contact()
                    {
                        Id = contactId,
                        Name = (string)entity.GetAttributeValue<AliasedValue>("contact.Name").Value,
                        PhoneNumber = (string)entity.GetAttributeValue<AliasedValue>("contact.PhoneNumber").Value
                    };

                    var accountId = (Guid)entity.GetAttributeValue<AliasedValue>("contact.AccountId").Value;
                    if (!contactToAccountMap.ContainsKey(contactId))
                    {
                        contactToAccountMap.Add(contactId, accountId);
                    }

                    contacts.Add(contactId, contact);
                }

                var noteId = (Guid)entity.GetAttributeValue<AliasedValue>("contact.note.Noteid").Value;
                if (!notes.TryGetValue(noteId, out note))
                {
                    note = new()
                    {
                        Id = noteId,
                        Text = (string)entity.GetAttributeValue<AliasedValue>("contact.note.Text").Value
                    };

                    if (!notesToContactMap.ContainsKey(noteId))
                    {
                        notesToContactMap.Add(noteId, contactId);
                    }

                    notes.Add(noteId, note);
                }
            }

            // Mapping Contacts
            foreach(var kvp in contactToAccountMap)
            {
                accounts[kvp.Value].Contacts.Add(contacts[kvp.Key]);
            }

            // Mapping notes
            foreach(var kvp in notesToContactMap)
            {
                contacts[kvp.Value].Notes.Add(notes[kvp.Key]);
            }

            return accounts.Values.ToList();
        }

        private EntityCollection FetchAllAccounts()
        {
            var pageNumber = 1;
            var allResults = new EntityCollection();
            var maxPages = 1000; // Safety limit
            var hasMoreRecords = true;
            string pagingCookie = null;

            var query = new QueryExpression("Account")
            {
                ColumnSet = new ColumnSet("Name", "ParentId")
            };

            var link = new LinkEntity("Account", "Contact", "Id", "AccountId", JoinOperator.Inner)
            {
                Columns = new ColumnSet("Contactid", "Name", "PhoneNumber", "AccountId"),
                EntityAlias = "contact"
            };

            link.LinkEntities.Add(new LinkEntity("Contact", "Note", "Id", "ContactId", JoinOperator.Inner)
            {
                Columns = new ColumnSet("Noteid", "Text", "ContactId"),
                EntityAlias = "contact.note"
            });

            query.LinkEntities.Add(link);

            while (hasMoreRecords && pageNumber <= maxPages)
            {
                query.PageInfo = new PagingInfo
                {
                    PageNumber = pageNumber,
                    Count = 100,
                    PagingCookie = pagingCookie
                };

                var results = _organizationService.RetrieveMultiple(query);
                allResults.Entities.AddRange(results.Entities);

                hasMoreRecords = results.MoreRecords;
                pagingCookie = results.PagingCookie;
                pageNumber++;

                if (pageNumber > maxPages)
                {
                    throw new Exception($"Maximum page limit ({maxPages}) reached");
                }
            }

            return allResults;
        }

        private IEnumerable<Note> RetrieveAccountNotes(Guid accountId)
            => RetrieveRelatedNotes(accountId, "AccountId");

        private IEnumerable<Note> RetrieveContactNotes(Guid accountId)
            => RetrieveRelatedNotes(accountId, "ContactId");

        private IEnumerable<Note> RetrieveRelatedNotes(Guid accountId, string mappingProperty)
        {
            if(accountId == default)
            {
                yield break;
            }

            var filter = new FilterExpression();
            filter.Conditions.Add(new ConditionExpression(mappingProperty, ConditionOperator.Equal, accountId));
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

        private IEnumerable<Contact> RetrieveRelatedContacts(Guid accountId)
        {
            var filter = new FilterExpression();
            filter.Conditions.Add(new ConditionExpression("AccountId", ConditionOperator.Equal, accountId));
            QueryExpression relatedNotesQuery = new("Contact")
            {
                TopCount = 100
            };
            relatedNotesQuery.ColumnSet.AddColumns("Name");
            relatedNotesQuery.ColumnSet.AddColumns("PhoneNumber");
            relatedNotesQuery.Criteria.AddFilter(filter);

            var relatedNotes = _organizationService.RetrieveMultiple(relatedNotesQuery);

            foreach (var contact in relatedNotes.Entities)
            {
                yield return new Contact()
                {
                    Id = contact.Id,
                    Name = contact.GetAttributeValue<string>("Name"),
                    PhoneNumber = contact.GetAttributeValue<string>("PhoneNumber"),
                    Notes = RetrieveContactNotes(contact.Id).ToList()
                };
            }
        }

        private Account? FetchAccount(Guid accountId)
        {
            if(accountId == default)
            {
                return null;
            }

            var originalAccount = _organizationService.Retrieve("Account", accountId, new ColumnSet(new string[] { "Name", "ParentId" }));

            return new Account()
            {
                Name = originalAccount.GetAttributeValue<string>("Name")
            };
        }
    }
}
