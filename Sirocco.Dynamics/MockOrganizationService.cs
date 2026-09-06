using FakeXrmEasy;
using FakeXrmEasy.Abstractions.Enums;
using FakeXrmEasy.Middleware;
using FakeXrmEasy.Middleware.Crud;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sirocco.Dynamics
{
    internal class MockOrganizationService : IOrganizationService
    {
        private readonly IOrganizationService _internalService;

        public MockOrganizationService() 
        {
            var context = MiddlewareBuilder
                        .New()
                        .AddCrud()
                        .UseCrud()

                        // Here we are saying we're using FakeXrmEasy (FXE) under a commercial context
                        // For more info please refer to the license at https://dynamicsvalue.github.io/fake-xrm-easy-docs/licensing/license/
                        // And the licensing FAQ at https://dynamicsvalue.github.io/fake-xrm-easy-docs/licensing/faq/
                        .SetLicense(FakeXrmEasyLicense.RPL_1_5)
                        .Build();

            _internalService = context.GetOrganizationService();
        }

        public void Associate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities) =>
            _internalService.Associate(entityName, entityId, relationship, relatedEntities);

        public Guid Create(Entity entity) =>
            _internalService.Create(entity);

        public void Delete(string entityName, Guid id) =>
            _internalService.Delete(entityName, id);
        

        public void Disassociate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities) =>
            _internalService.Disassociate(entityName, entityId, relationship, relatedEntities);

        public OrganizationResponse Execute(OrganizationRequest request) =>
            _internalService.Execute(request);

        public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet) =>
            _internalService.Retrieve(entityName, id, columnSet);

        public EntityCollection RetrieveMultiple(QueryBase query) =>
            _internalService.RetrieveMultiple(query);

        public void Update(Entity entity) =>
            _internalService.Update(entity);
    }
}
