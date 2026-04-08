using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace SpeedyNtoNAssociatePlugin.Tests.Mocks
{
    class MockOrganizationService : IOrganizationService
    {
        public ConcurrentBag<OrganizationRequest> ExecutedRequests { get; } = new ConcurrentBag<OrganizationRequest>();
        public Func<OrganizationRequest, OrganizationResponse> ExecuteHandler { get; set; }

        private int _retrieveMultipleCallCount;
        public int RetrieveMultipleCallCount => _retrieveMultipleCallCount;
        public Func<QueryBase, EntityCollection> RetrieveMultipleHandler { get; set; }

        public OrganizationResponse Execute(OrganizationRequest request)
        {
            ExecutedRequests.Add(request);

            if (ExecuteHandler != null)
                return ExecuteHandler(request);

            return new OrganizationResponse();
        }

        public EntityCollection RetrieveMultiple(QueryBase query)
        {
            Interlocked.Increment(ref _retrieveMultipleCallCount);

            if (RetrieveMultipleHandler != null)
                return RetrieveMultipleHandler(query);

            return new EntityCollection();
        }

        public Guid Create(Entity entity) => throw new NotImplementedException();
        public void Update(Entity entity) => throw new NotImplementedException();
        public void Delete(string entityName, Guid id) => throw new NotImplementedException();
        public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet) => throw new NotImplementedException();
        public void Associate(string entityName, Guid entityId, Relationship relationship,
            EntityReferenceCollection relatedEntities) => throw new NotImplementedException();
        public void Disassociate(string entityName, Guid entityId, Relationship relationship,
            EntityReferenceCollection relatedEntities) => throw new NotImplementedException();
    }
}
