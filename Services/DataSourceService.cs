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

        public List<AssociationPair> LoadFromFetchXml(IOrganizationService service, string fetchXml)
        {
            var pairs = new List<AssociationPair>();
            var seen = new HashSet<(Guid, Guid)>();

            // Ensure count attribute exists for paging support
            fetchXml = EnsureFetchCount(fetchXml);

            var response = service.RetrieveMultiple(new Microsoft.Xrm.Sdk.Query.FetchExpression(fetchXml));
            ExtractPairs(response, pairs, seen);

            // Handle paging with proper XML manipulation
            int page = 2;
            while (response.MoreRecords)
            {
                var pagedFetch = SetPagingAttributes(fetchXml, response.PagingCookie, page);
                response = service.RetrieveMultiple(new Microsoft.Xrm.Sdk.Query.FetchExpression(pagedFetch));
                ExtractPairs(response, pairs, seen);
                page++;
            }

            return pairs;
        }

        private static void ExtractPairs(EntityCollection response, List<AssociationPair> pairs, HashSet<(Guid, Guid)> seen)
        {
            foreach (var entity in response.Entities)
            {
                // Extract the first two GUID values from the record's attributes
                var guids = new List<Guid>();

                // First, the entity's own ID
                if (entity.Id != Guid.Empty)
                    guids.Add(entity.Id);

                // Then check all attributes for GUIDs (including aliased values from link-entity)
                foreach (var attr in entity.Attributes)
                {
                    if (guids.Count >= 2) break;

                    Guid? val = null;
                    if (attr.Value is Guid g)
                        val = g;
                    else if (attr.Value is AliasedValue av && av.Value is Guid ag)
                        val = ag;
                    else if (attr.Value is EntityReference er)
                        val = er.Id;

                    if (val.HasValue && val.Value != Guid.Empty && !guids.Contains(val.Value))
                        guids.Add(val.Value);
                }

                if (guids.Count >= 2)
                {
                    var pair = new AssociationPair { Guid1 = guids[0], Guid2 = guids[1] };
                    var key = pair.NormalizedKey();
                    if (seen.Add(key))
                        pairs.Add(pair);
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
