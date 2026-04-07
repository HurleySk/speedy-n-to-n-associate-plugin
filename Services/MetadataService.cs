using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using SpeedyNtoNAssociatePlugin.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SpeedyNtoNAssociatePlugin.Services
{
    public class MetadataService
    {
        public Tuple<List<Tuple<string, string>>, List<RelationshipInfo>> GetAllMetadata(
            IOrganizationService service)
        {
            var request = new RetrieveAllEntitiesRequest
            {
                EntityFilters = EntityFilters.Entity | EntityFilters.Relationships,
                RetrieveAsIfPublished = true
            };

            var response = (RetrieveAllEntitiesResponse)service.Execute(request);

            // Extract entities
            var entities = response.EntityMetadata
                .Where(e => e.IsIntersect != true)
                .Select(e => Tuple.Create(
                    e.LogicalName,
                    e.DisplayName?.UserLocalizedLabel?.Label ?? e.LogicalName))
                .OrderBy(e => e.Item2)
                .ToList();

            // Extract N:N relationships (deduplicated)
            var seen = new HashSet<string>();
            var relationships = new List<RelationshipInfo>();

            foreach (var entity in response.EntityMetadata)
            {
                if (entity.ManyToManyRelationships == null) continue;

                foreach (var rel in entity.ManyToManyRelationships)
                {
                    if (seen.Add(rel.SchemaName))
                    {
                        relationships.Add(new RelationshipInfo
                        {
                            SchemaName = rel.SchemaName,
                            Entity1LogicalName = rel.Entity1LogicalName,
                            Entity2LogicalName = rel.Entity2LogicalName,
                            IntersectEntityName = rel.IntersectEntityName,
                            Entity1IntersectAttribute = rel.Entity1IntersectAttribute,
                            Entity2IntersectAttribute = rel.Entity2IntersectAttribute
                        });
                    }
                }
            }

            return Tuple.Create(entities, relationships);
        }
    }
}
