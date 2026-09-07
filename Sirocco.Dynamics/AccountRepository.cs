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
            //QueryExpression notesQuery = new("Note")
            //{
            //    TopCount = 1000,
            //    ColumnSet = new ColumnSet("Text", "ContactId")
            //};

            //QueryExpression contactQuery = new("Contact")
            //{
            //    TopCount = 1000,
            //    ColumnSet = new ColumnSet("Name", "PhoneNumber", "AccountId")
            //};

            //var allContacts = _organizationService.RetrieveMultiple(contactQuery);
            //var allnotes = _organizationService.RetrieveMultiple(notesQuery);



            //var query = new QueryExpression("Account")
            //{
            //    ColumnSet = new ColumnSet("Name", "ParentId"),
            //    TopCount = 1000
            //};

            //var link = new LinkEntity("Account", "Contact", "Id", "AccountId", JoinOperator.Inner)
            //{
            //    Columns = new ColumnSet("Id", "Name", "PhoneNumber", "AccountId"),
            //    EntityAlias = "contact"
            //};

            //link.LinkEntities.Add(new LinkEntity("Contact", "Note", "Id", "ContactId", JoinOperator.Inner)
            //{
            //    Columns = new ColumnSet("Id", "Text", "ContactId"),
            //    EntityAlias = "contact.note"
            //});

            //query.LinkEntities.Add(link);

            //var results = _organizationService.RetrieveMultiple(query);

            //var pageNumber = 1;
            //var allResults = new EntityCollection();

            //while (true)
            //{
            //    query.PageInfo = new PagingInfo
            //    {
            //        PageNumber = pageNumber,
            //        Count = 5000
            //    };
            //    var results = _organizationService.RetrieveMultiple(query);
            //    allResults.Entities.AddRange(results.Entities);
            //    if (results.MoreRecords)
            //    {
            //        pageNumber++;
            //    }
            //    else
            //    {
            //        break;
            //    }
            //}

            var results = FetchAllAccounts();

            return BuildFromResult(results);

            //var accountsList = new List<Account>();

            //foreach(var e in results.Entities)
            //{
            //    var truc = new StringBuilder(2000);
            //    foreach(var a in e.Attributes)
            //    {
            //        truc.AppendLine($"{a.Key}:{a.Value}");
            //    }

            //    _logger.LogInformation(truc.ToString());
            //}

            //// Group by Account ID
            //var accountGroups = results.Entities
            //    .GroupBy(e => e.Id)
            //    .ToDictionary(g => g.Key, g => g.ToList());

            //foreach (var accountGroup in accountGroups.Values)
            //{
            //    var accountEntity = accountGroup.First();
            //    var account = new Account
            //    {
            //        Id = accountEntity.Id,
            //        Name = accountEntity.GetAttributeValue<string>("Name"),
            //        Contacts = new List<Contact>()
            //    };

            //    // Group by Contact ID within each Account
            //    //var contactGroups = accountGroup
            //    //    .GroupBy(e => e.GetAttributeValue<AliasedValue>("ContactId"))
            //    //    //.Where(g => g.Key != Guid.Empty)
            //    //    .ToDictionary(g => g.Key, g => g.ToList());

            //    //foreach (var contactGroup in contactGroups.Values)
            //    //{
            //    //    var contactId = GetAliasedValue<Guid>(contactGroup.First(), "contact", "Id");
            //    //    var contact = new Contact
            //    //    {
            //    //        Id = contactId,
            //    //        Name = GetAliasedValue<string>(contactGroup.First(), "contact", "Name"),
            //    //        PhoneNumber = GetAliasedValue<string>(contactGroup.First(), "contact", "PhoneNumber"),
            //    //        Notes = new List<Note>()
            //    //    };

            //    //    // Collect all Notes for this Contact
            //    //    foreach (var row in contactGroup)
            //    //    {
            //    //        var noteId = GetAliasedValue<Guid>(row, "contact_note", "Id");
            //    //        var noteText = GetAliasedValue<string>(row, "contact_note", "Text");

            //    //        if (noteId != Guid.Empty)
            //    //        {
            //    //            contact.Notes.Add(new Note
            //    //            {
            //    //                Id = noteId,
            //    //                Text = noteText
            //    //            });
            //    //        }
            //    //    }

            //    //    account.Contacts.Add(contact);
            //    //}

            //    accountsList.Add(account);
            //}

            //var query = new QueryExpression("Account")
            //{
            //    ColumnSet = new ColumnSet(new string[] { "Name", "ParentId" }),
            //    TopCount = 100
            //};

            ////var link = new LinkEntity("Contact", "Account", "AccountId", "Id", JoinOperator.Inner);
            //var link = new LinkEntity("Account", "Contact", "Id", "AccountId", JoinOperator.Inner)
            //{
            //    Columns = new ColumnSet("Name", "PhoneNumber"),
            //    EntityAlias = "contact"
            //};

            //link.LinkEntities.Add(new LinkEntity("Contact", "Note", "Id", "ContactId", JoinOperator.LeftOuter)
            //{
            //    Columns = new ColumnSet("Text"),
            //    EntityAlias = "contact.note"
            //});

            //query.LinkEntities.Add(link);

            //var truc = _organizationService.RetrieveMultiple(query);

            //var accountsGrouped = truc.Entities
            //    .GroupBy(e => e.Id)
            //    .ToDictionary(g => g.Key, g => g.ToList());

            //foreach (var account in truc.Entities)
            //{
            //    var contactGroups = account.Attributes
            //        .GroupBy(e => e.GetAliasedValue<Guid>(e, "contact", "Id"))
            //        .Where(g => g.Key != Guid.Empty)
            //        .ToDictionary(g => g.Key, g => g.ToList());

            //    //var bidule = account.GetAttributeValue<AliasedValue>("contact.Name");

            //    //foreach (var attribute in account.Attributes)
            //    //{
            //    //    //if(attribute.Value is AliasedValue)
            //    //    //{
            //    //    //    _logger.LogInformation($"{attribute.Key}:{attribute.GetAttributeValue<AliasedValue>()}");
            //    //    //}
            //    //    //else
            //    //    //{
            //    //        _logger.LogInformation($"{attribute.Key}:{attribute.Value}");
            //    //    //}
            //    //}

            //    ////foreach (var contact in account.RelatedEntities)
            //    ////{
            //    ////    _logger.LogInformation($"Account: {account.GetAttributeValue<string>("Name")}, Contact: ");
            //    ////}
            //}

            //return Array.Empty<Account>();
        }

        private IList<Account> BuildFromResult(EntityCollection entityCollection)
        {
            Dictionary<Guid, Account> accounts = new();
            Dictionary<Guid, Contact> contacts = new();
            Dictionary<Guid, Guid> contactToAccountMap = new();

            int i = 0;
            int total = entityCollection.Entities.Count;

            //var ziz = entityCollection.Entities.ToArray();
            //List<Entity> bidule = new();

            foreach (var entity in entityCollection.Entities)
            //for(int i = 0; i < total; i++)
            {
                //bidule.Add(entity);
                //var entity = entityCollection[i];
                _logger.LogInformation($"titi");
                Console.WriteLine($"{i}/{total}");

                Account account;
                Contact contact;

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
                //"Id", "Text", "ContactId"
                //"Id", "Name", "PhoneNumber", "AccountId"
                //var contactName
                var contactId = (Guid)entity.GetAttributeValue<AliasedValue>("contact.AccountId").Value;
                if (!contacts.TryGetValue(contactId, out contact))
                {
                    contact = new Contact()
                    {
                        Id = contactId,
                        Name = (string)entity.GetAttributeValue<AliasedValue>("contact.Name").Value
                    };

                    var accountId = (Guid)entity.GetAttributeValue<AliasedValue>("contact.AccountId").Value;
                    if (!contactToAccountMap.ContainsKey(contactId))
                    {
                        contactToAccountMap.Add(contactId, accountId);
                    }

                    contacts.Add(contactId, contact);
                }

                ////if()

                ////_logger.LogInformation(entity.GetAttributeValue<string>("Name"));
                i++;
            }

            foreach(var kvp in contactToAccountMap)
            {
                  accounts[kvp.Value].Contacts.Add(contacts[kvp.Key]);
            }

            //foreach (var entity in entityCollection.Entities)
            //{
            //    Account account;
            //    Contact contact;

            //    //if (!accounts.TryGetValue(entity.Id, out account))
            //    //{
            //    //    throw new Exception($"Couldn't find account with id:{entity.Id}");
            //    //}

            //    var contactId = (Guid)entity.GetAttributeValue<AliasedValue>("contact.AccountId").Value;
            //    if (!contacts.TryGetValue(contactId, out contact))
            //    {
            //        throw new Exception($"Couldn't find contact with id:{contactId}");
            //    }

            //    var accountId = (Guid)entity.GetAttributeValue<AliasedValue>("contact.AccountId").Value;
            //    accounts[accountId].Contacts.Add(contact);
            //}

            //_logger.LogInformation($"Processed entities: {bidule.Count}");
            //_logger.LogInformation(entity.GetAttributeValue<string>("Name"));

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
                Columns = new ColumnSet("Id", "Name", "PhoneNumber", "AccountId"),
                EntityAlias = "contact"
            };

            link.LinkEntities.Add(new LinkEntity("Contact", "Note", "Id", "ContactId", JoinOperator.Inner)
            {
                Columns = new ColumnSet("Id", "Text", "ContactId"),
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
