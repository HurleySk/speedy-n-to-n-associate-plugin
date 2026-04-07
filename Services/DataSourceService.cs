using Microsoft.Xrm.Sdk;
using SpeedyNtoNAssociatePlugin.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace SpeedyNtoNAssociatePlugin.Services
{
    public class DataSourceService
    {
        public List<AssociationPair> LoadFromCsv(string filePath)
        {
            var pairs = new List<AssociationPair>();
            var seen = new HashSet<(Guid, Guid)>();
            var lines = File.ReadAllLines(filePath);

            // Skip header if first line doesn't parse as GUIDs
            int startLine = 0;
            if (lines.Length > 0)
            {
                var firstParts = lines[0].Split(',');
                if (firstParts.Length >= 2 &&
                    !Guid.TryParse(firstParts[0].Trim().Trim('"'), out _))
                {
                    startLine = 1;
                }
            }

            for (int i = startLine; i < lines.Length; i++)
            {
                var parts = lines[i].Split(',');
                if (parts.Length < 2) continue;

                if (!Guid.TryParse(parts[0].Trim().Trim('"'), out var g1) ||
                    !Guid.TryParse(parts[1].Trim().Trim('"'), out var g2))
                    continue;

                if (g1 == Guid.Empty || g2 == Guid.Empty)
                    continue;

                var pair = new AssociationPair { Guid1 = g1, Guid2 = g2 };
                var key = pair.NormalizedKey();

                if (seen.Add(key))
                    pairs.Add(pair);
            }

            return pairs;
        }

        public List<AssociationPair> LoadFromFetchXml(
            IOrganizationService service, string fetchXml,
            string entity1LogicalName, string entity2LogicalName)
        {
            var pairs = new List<AssociationPair>();
            var seen = new HashSet<(Guid, Guid)>();
            int skipped = 0;

            // Ensure count attribute exists for paging support
            fetchXml = EnsureFetchCount(fetchXml);

            var response = service.RetrieveMultiple(new Microsoft.Xrm.Sdk.Query.FetchExpression(fetchXml));
            ExtractPairs(response, pairs, seen, entity1LogicalName, entity2LogicalName, ref skipped);

            // Handle paging with proper XML manipulation
            int page = 2;
            while (response.MoreRecords)
            {
                var pagedFetch = SetPagingAttributes(fetchXml, response.PagingCookie, page);
                response = service.RetrieveMultiple(new Microsoft.Xrm.Sdk.Query.FetchExpression(pagedFetch));
                ExtractPairs(response, pairs, seen, entity1LogicalName, entity2LogicalName, ref skipped);
                page++;
            }

            SkippedRows = skipped;
            return pairs;
        }

        public int SkippedRows { get; private set; }

        private static void ExtractPairs(EntityCollection response, List<AssociationPair> pairs,
            HashSet<(Guid, Guid)> seen, string entity1Name, string entity2Name, ref int skipped)
        {
            foreach (var entity in response.Entities)
            {
                // Collect all GUIDs tagged with their source entity name
                var taggedGuids = new List<(Guid id, string entityName)>();

                // The entity's own ID belongs to entity.LogicalName
                if (entity.Id != Guid.Empty)
                    taggedGuids.Add((entity.Id, entity.LogicalName));

                foreach (var attr in entity.Attributes)
                {
                    if (attr.Value is Guid g && g != Guid.Empty)
                    {
                        // Plain Guid attribute -- belongs to the primary entity
                        if (!taggedGuids.Any(t => t.id == g))
                            taggedGuids.Add((g, entity.LogicalName));
                    }
                    else if (attr.Value is AliasedValue av)
                    {
                        if (av.Value is Guid ag && ag != Guid.Empty)
                        {
                            if (!taggedGuids.Any(t => t.id == ag))
                                taggedGuids.Add((ag, av.EntityLogicalName));
                        }
                    }
                    else if (attr.Value is EntityReference er && er.Id != Guid.Empty)
                    {
                        if (!taggedGuids.Any(t => t.id == er.Id))
                            taggedGuids.Add((er.Id, er.LogicalName));
                    }
                }

                // If no entity filter specified, just take the first two GUIDs
                if (string.IsNullOrEmpty(entity1Name) || string.IsNullOrEmpty(entity2Name))
                {
                    if (taggedGuids.Count >= 2)
                    {
                        var pair = new AssociationPair { Guid1 = taggedGuids[0].id, Guid2 = taggedGuids[1].id };
                        var key = pair.NormalizedKey();
                        if (seen.Add(key))
                            pairs.Add(pair);
                    }
                    else
                    {
                        skipped++;
                    }
                    continue;
                }

                // Find one GUID from entity1 and one from entity2
                Guid? guid1 = null, guid2 = null;

                foreach (var tg in taggedGuids)
                {
                    if (!guid1.HasValue && tg.entityName == entity1Name)
                        guid1 = tg.id;
                    else if (!guid2.HasValue && tg.entityName == entity2Name)
                        guid2 = tg.id;

                    if (guid1.HasValue && guid2.HasValue) break;
                }

                // For self-referencing relationships, both entities have the same name
                if (entity1Name == entity2Name && guid1.HasValue && !guid2.HasValue)
                {
                    foreach (var tg in taggedGuids)
                    {
                        if (tg.entityName == entity2Name && tg.id != guid1.Value)
                        {
                            guid2 = tg.id;
                            break;
                        }
                    }
                }

                if (guid1.HasValue && guid2.HasValue)
                {
                    var pair = new AssociationPair { Guid1 = guid1.Value, Guid2 = guid2.Value };
                    var key = pair.NormalizedKey();
                    if (seen.Add(key))
                        pairs.Add(pair);
                }
                else
                {
                    skipped++;
                }
            }
        }

        private static string EnsureFetchCount(string fetchXml)
        {
            try
            {
                var doc = XDocument.Parse(fetchXml);
                var fetchElement = doc.Root;
                if (fetchElement != null && fetchElement.Attribute("count") == null)
                {
                    fetchElement.SetAttributeValue("count", "5000");
                }
                return doc.ToString(SaveOptions.DisableFormatting);
            }
            catch
            {
                return fetchXml;
            }
        }

        private static string SetPagingAttributes(string fetchXml, string pagingCookie, int page)
        {
            try
            {
                var doc = XDocument.Parse(fetchXml);
                var fetchElement = doc.Root;
                if (fetchElement != null)
                {
                    fetchElement.SetAttributeValue("page", page.ToString());
                    fetchElement.SetAttributeValue("paging-cookie", pagingCookie);
                }
                return doc.ToString(SaveOptions.DisableFormatting);
            }
            catch
            {
                return fetchXml;
            }
        }
    }
}
