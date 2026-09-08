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
        Account? GetById(Guid accountId);
        Guid Create(Account account);
        void Update(Account account);
        IList<Account> GetAll();
        IList<Tuple<string, string>> GetNoteTexts();
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

        public Account? GetById(Guid accountId)
        {
            var results = FetchAllAccounts(accountId);
            var accounts = BuildFromResult(results);
            return accounts.Count > 0 ? accounts[0] : null;
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

        public IList<Tuple<string, string>> GetNoteTexts()
        {
            var notes = FetchAllNotes();
            var noteTexts = new List<Tuple<string, string>>();

            foreach (var note in notes.Entities)
            {
                if (note.Attributes.ContainsKey("AccountId"))
                {
                    noteTexts.Add(new Tuple<string, string>(
                        (string)note.GetAttributeValue<AliasedValue>("account.Name").Value,
                        note.GetAttributeValue<string>("Text")
                    ));
                }

                if(note.Attributes.ContainsKey("Contactid"))
                {
                    noteTexts.Add(new Tuple<string, string>(
                        (string)note.GetAttributeValue<AliasedValue>("contact.Name").Value,
                        note.GetAttributeValue<string>("Text")
                    ));
                }
            }

            return noteTexts;
        }

        private IList<Account> BuildFromResult(EntityCollection entityCollection)
        {
            Dictionary<Guid, Account> accounts = new();
            Dictionary<Guid, Contact> contacts = new();
            Dictionary<Guid, Note> notes = new();
            Dictionary<Guid, Guid> contactToAccountMap = new();
            Dictionary<Guid, Guid> notesToContactMap = new();
            Dictionary<Guid, Guid> notesToAccountMap = new();
            Dictionary<Guid, Guid> accountToParentMap = new();

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

                if (entity.Attributes.ContainsKey("contact.Contactid"))
                {
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

                    if (entity.Attributes.ContainsKey("contact.note.Noteid"))
                    {
                        var contactNoteId = (Guid)entity.GetAttributeValue<AliasedValue>("contact.note.Noteid").Value;
                        if (contactNoteId != default &&
                        entity.Attributes.ContainsKey("contact.note.ContactId"))
                        {
                            note = new()
                            {
                                Id = contactNoteId,
                                Text = (string)entity.GetAttributeValue<AliasedValue>("contact.note.Text").Value
                            };

                            if (!notes.ContainsKey(contactNoteId))
                            {
                                notes.Add(contactNoteId, note);
                            }

                            contactId = (Guid)entity.GetAttributeValue<AliasedValue>("contact.note.ContactId").Value;
                            if (!notesToContactMap.ContainsKey(contactId))
                            {
                                notesToContactMap.Add(contactId, contactNoteId);
                            }
                        }
                    }
                }
                
                if(entity.Attributes.ContainsKey("note.Noteid"))
                {
                    var accountNoteId = (Guid)entity.GetAttributeValue<AliasedValue>("note.Noteid").Value;
                    if (accountNoteId != default &&
                    entity.Attributes.ContainsKey("note.AccountId"))
                    {
                        note = new()
                        {
                            Id = accountNoteId,
                            Text = (string)entity.GetAttributeValue<AliasedValue>("note.Text").Value
                        };

                        if (!notes.ContainsKey(accountNoteId))
                        {
                            notes.Add(accountNoteId, note);
                        }

                        var accountId = (Guid)entity.GetAttributeValue<AliasedValue>("note.AccountId").Value;
                        if (!notesToAccountMap.ContainsKey(accountNoteId))
                        {
                            notesToAccountMap.Add(accountNoteId, accountId);
                        }
                    }
                }
                
                if (entity.Attributes.ContainsKey("ParentId") && !accountToParentMap.ContainsKey(entity.Id)) 
                {
                    var parentId = entity.GetAttributeValue<Guid>("ParentId");
                    if (!accountToParentMap.ContainsKey(entity.Id))
                    {
                        accountToParentMap.Add(entity.Id, parentId);
                    }
                }
            }

            // Mapping Contacts
            foreach(var kvp in contactToAccountMap)
            {
                if (accounts.ContainsKey(kvp.Value))
                {
                    accounts[kvp.Value].Contacts.Add(contacts[kvp.Key]);
                }
            }

            // Mapping notes
            foreach (var kvp in notesToContactMap)
            {
                contacts[kvp.Key].Notes.Add(notes[kvp.Value]);
            }

            foreach (var kvp in notesToAccountMap)
            {
                if (accounts.ContainsKey(kvp.Value))
                {
                    accounts[kvp.Value].Notes.Add(notes[kvp.Key]);
                }
            }

            // Mapping parents
            foreach (var kvp in accountToParentMap)
            {
                accounts[kvp.Key].Parent = FetchAccount(kvp.Value);
            }

            return accounts.Values.ToList();
        }

        private EntityCollection FetchAllAccounts(Guid entityId = default)
        {
            var query = new QueryExpression("Account")
            {
                ColumnSet = new ColumnSet("Name", "ParentId")
            };

            var contactLink = new LinkEntity("Account", "Contact", "Id", "AccountId", JoinOperator.LeftOuter)
            {
                Columns = new ColumnSet("Contactid", "Name", "PhoneNumber", "AccountId"),
                EntityAlias = "contact"
            };

            // Link the Note entity to the Contact entity
            contactLink.LinkEntities.Add(new LinkEntity("Contact", "Note", "Id", "ContactId", JoinOperator.LeftOuter)
            {
                Columns = new ColumnSet("Noteid", "Text", "ContactId"),
                EntityAlias = "contact.note"
            });

            var noteLink = new LinkEntity("Account", "Note", "Id", "AccountId", JoinOperator.LeftOuter)
            {
                Columns = new ColumnSet("Noteid", "Text", "AccountId"),
                EntityAlias = "note"
            };

            var parentLink = new LinkEntity("Account", "Account", "ParentId", "Id", JoinOperator.LeftOuter)
            {
                Columns = new ColumnSet("Name"),
                EntityAlias = "parent"
            };

            query.LinkEntities.Add(contactLink);
            query.LinkEntities.Add(noteLink);
            query.LinkEntities.Add(parentLink);

            if (entityId != default)
            {
                query.Criteria = new FilterExpression()
                {
                    Filters = { new FilterExpression()
                    {
                        Conditions = { new ConditionExpression("Accountid", ConditionOperator.Equal, entityId) }
                    }}
                };
            }

            return FetchResults(query);
        }

        private EntityCollection FetchAllNotes()
        {
            var query = new QueryExpression("Note")
            {
                ColumnSet = new ColumnSet("Noteid", "Text", "AccountId")
            };

            query.LinkEntities.Add(new LinkEntity("Note", "Account", "AccountId", "Id", JoinOperator.Inner)
            {
                Columns = new ColumnSet("Name"),
                EntityAlias = "account"
            });

            query.LinkEntities.Add(new LinkEntity("Note", "Contact", "ContactId", "Id", JoinOperator.Inner)
            {
                Columns = new ColumnSet("Name"),
                EntityAlias = "contact"
            });

            return FetchResults(query);
        }

        private EntityCollection FetchResults(QueryExpression query)
        {
            var pageNumber = 1;
            var allResults = new EntityCollection();
            var maxPages = 1000; // Safety limit
            var hasMoreRecords = true;
            string pagingCookie = null;

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

                if (pageNumber >= maxPages)
                {
                    throw new Exception($"Maximum page limit ({maxPages}) reached");
                }
            }

            return allResults;
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
