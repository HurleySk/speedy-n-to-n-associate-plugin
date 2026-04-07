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
        private const int DefaultFetchXmlPageSize = 5000;

        /// <summary>
        /// Tracks rows skipped during the most recent FetchXML streaming enumeration.
        /// </summary>
        public int LastFetchXmlSkippedCount { get; private set; }

        #region Streaming Methods

        public IEnumerable<AssociationPair> StreamFromCsv(string filePath)
        {
            var seen = new HashSet<(Guid, Guid)>();
            bool firstLine = true;

            using (var reader = new StreamReader(filePath))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    var parts = line.Split(',');
                    if (parts.Length < 2) continue;

                    var field1 = CleanGuidField(parts[0]);
                    var field2 = CleanGuidField(parts[1]);

                    // Skip header row if first line doesn't parse as GUIDs
                    if (firstLine)
                    {
                        firstLine = false;
                        if (!Guid.TryParse(field1, out _))
                            continue;
                    }

                    if (!Guid.TryParse(field1, out var g1) ||
                        !Guid.TryParse(field2, out var g2))
                        continue;

                    if (g1 == Guid.Empty || g2 == Guid.Empty)
                        continue;

                    var pair = new AssociationPair { Guid1 = g1, Guid2 = g2 };
                    if (seen.Add(pair.NormalizedKey()))
                        yield return pair;
                }
            }
        }

        public IEnumerable<AssociationPair> StreamFromFetchXml(
            IOrganizationService service, string fetchXml,
            string entity1LogicalName, string entity2LogicalName)
        {
            LastFetchXmlSkippedCount = 0;
            var seen = new HashSet<(Guid, Guid)>();
            int skipped = 0;

            fetchXml = EnsureFetchCount(fetchXml);

            var response = service.RetrieveMultiple(new Microsoft.Xrm.Sdk.Query.FetchExpression(fetchXml));
            foreach (var pair in ExtractPairsFromPage(response, seen, entity1LogicalName, entity2LogicalName, ref skipped))
                yield return pair;

            int page = 2;
            while (response.MoreRecords)
            {
                var pagedFetch = SetPagingAttributes(fetchXml, response.PagingCookie, page);
                response = service.RetrieveMultiple(new Microsoft.Xrm.Sdk.Query.FetchExpression(pagedFetch));
                foreach (var pair in ExtractPairsFromPage(response, seen, entity1LogicalName, entity2LogicalName, ref skipped))
                    yield return pair;
                page++;
            }

            LastFetchXmlSkippedCount = skipped;
        }

        #endregion

        #region Original Load Methods (for preview)

        public List<AssociationPair> LoadFromCsv(string filePath)
        {
            var pairs = new List<AssociationPair>();
            var seen = new HashSet<(Guid, Guid)>();
            var lines = File.ReadAllLines(filePath);

            int startLine = 0;
            if (lines.Length > 0)
            {
                var firstParts = lines[0].Split(',');
                if (firstParts.Length >= 2 &&
                    !Guid.TryParse(CleanGuidField(firstParts[0]), out _))
                {
                    startLine = 1;
                }
            }

            for (int i = startLine; i < lines.Length; i++)
            {
                var parts = lines[i].Split(',');
                if (parts.Length < 2) continue;

                if (!Guid.TryParse(CleanGuidField(parts[0]), out var g1) ||
                    !Guid.TryParse(CleanGuidField(parts[1]), out var g2))
                    continue;

                if (g1 == Guid.Empty || g2 == Guid.Empty)
                    continue;

                var pair = new AssociationPair { Guid1 = g1, Guid2 = g2 };
                if (seen.Add(pair.NormalizedKey()))
                    pairs.Add(pair);
            }

            return pairs;
        }

        public Tuple<List<AssociationPair>, int> LoadFromFetchXml(
            IOrganizationService service, string fetchXml,
            string entity1LogicalName, string entity2LogicalName)
        {
            var pairs = new List<AssociationPair>();
            var seen = new HashSet<(Guid, Guid)>();
            int skipped = 0;

            fetchXml = EnsureFetchCount(fetchXml);

            var response = service.RetrieveMultiple(new Microsoft.Xrm.Sdk.Query.FetchExpression(fetchXml));
            ExtractPairs(response, pairs, seen, entity1LogicalName, entity2LogicalName, ref skipped);

            int page = 2;
            while (response.MoreRecords)
            {
                var pagedFetch = SetPagingAttributes(fetchXml, response.PagingCookie, page);
                response = service.RetrieveMultiple(new Microsoft.Xrm.Sdk.Query.FetchExpression(pagedFetch));
                ExtractPairs(response, pairs, seen, entity1LogicalName, entity2LogicalName, ref skipped);
                page++;
            }

            return Tuple.Create(pairs, skipped);
        }

        #endregion

        #region FetchXML Pair Extraction

        private static List<AssociationPair> ExtractPairsFromPage(EntityCollection response,
            HashSet<(Guid, Guid)> seen, string entity1Name, string entity2Name, ref int skipped)
        {
            var results = new List<AssociationPair>();

            foreach (var entity in response.Entities)
            {
                var taggedGuids = ExtractTaggedGuids(entity);
                AssociationPair pair = null;

                if (string.IsNullOrEmpty(entity1Name) || string.IsNullOrEmpty(entity2Name))
                {
                    if (taggedGuids.Count >= 2)
                    {
                        pair = new AssociationPair { Guid1 = taggedGuids[0].id, Guid2 = taggedGuids[1].id };
                    }
                    else
                    {
                        skipped++;
                        continue;
                    }
                }
                else
                {
                    Guid? guid1 = null, guid2 = null;

                    foreach (var tg in taggedGuids)
                    {
                        if (!guid1.HasValue && tg.entityName == entity1Name)
                            guid1 = tg.id;
                        else if (!guid2.HasValue && tg.entityName == entity2Name)
                            guid2 = tg.id;

                        if (guid1.HasValue && guid2.HasValue) break;
                    }

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
                        pair = new AssociationPair { Guid1 = guid1.Value, Guid2 = guid2.Value };
                    }
                    else
                    {
                        skipped++;
                        continue;
                    }
                }

                if (pair != null && seen.Add(pair.NormalizedKey()))
                    results.Add(pair);
            }

            return results;
        }

        private static void ExtractPairs(EntityCollection response, List<AssociationPair> pairs,
            HashSet<(Guid, Guid)> seen, string entity1Name, string entity2Name, ref int skipped)
        {
            foreach (var entity in response.Entities)
            {
                var taggedGuids = ExtractTaggedGuids(entity);

                if (string.IsNullOrEmpty(entity1Name) || string.IsNullOrEmpty(entity2Name))
                {
                    if (taggedGuids.Count >= 2)
                    {
                        var pair = new AssociationPair { Guid1 = taggedGuids[0].id, Guid2 = taggedGuids[1].id };
                        if (seen.Add(pair.NormalizedKey()))
                            pairs.Add(pair);
                    }
                    else
                    {
                        skipped++;
                    }
                    continue;
                }

                Guid? guid1 = null, guid2 = null;

                foreach (var tg in taggedGuids)
                {
                    if (!guid1.HasValue && tg.entityName == entity1Name)
                        guid1 = tg.id;
                    else if (!guid2.HasValue && tg.entityName == entity2Name)
                        guid2 = tg.id;

                    if (guid1.HasValue && guid2.HasValue) break;
                }

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
                    if (seen.Add(pair.NormalizedKey()))
                        pairs.Add(pair);
                }
                else
                {
                    skipped++;
                }
            }
        }

        private static List<(Guid id, string entityName)> ExtractTaggedGuids(Entity entity)
        {
            var guids = new List<(Guid id, string entityName)>();

            if (entity.Id != Guid.Empty)
                guids.Add((entity.Id, entity.LogicalName));

            foreach (var attr in entity.Attributes)
            {
                if (attr.Value is Guid g && g != Guid.Empty)
                {
                    if (!guids.Any(t => t.id == g))
                        guids.Add((g, entity.LogicalName));
                }
                else if (attr.Value is AliasedValue av && av.Value is Guid ag && ag != Guid.Empty)
                {
                    if (!guids.Any(t => t.id == ag))
                        guids.Add((ag, av.EntityLogicalName));
                }
                else if (attr.Value is EntityReference er && er.Id != Guid.Empty)
                {
                    if (!guids.Any(t => t.id == er.Id))
                        guids.Add((er.Id, er.LogicalName));
                }
            }

            return guids;
        }

        #endregion

        #region FetchXML Paging

        private static string EnsureFetchCount(string fetchXml)
        {
            try
            {
                var doc = XDocument.Parse(fetchXml);
                var fetchElement = doc.Root;
                if (fetchElement != null && fetchElement.Attribute("count") == null)
                {
                    fetchElement.SetAttributeValue("count", DefaultFetchXmlPageSize.ToString());
                }
                return doc.ToString(SaveOptions.DisableFormatting);
            }
            catch (System.Xml.XmlException)
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
            catch (System.Xml.XmlException)
            {
                return fetchXml;
            }
        }

        #endregion

        #region Helpers

        private static string CleanGuidField(string value) => value.Trim().Trim('"');

        #endregion
    }
}
