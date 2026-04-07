using Microsoft.Xrm.Sdk;
using SpeedyNtoNAssociatePlugin.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SpeedyNtoNAssociatePlugin.Services
{
    public class SqlDataSourceService
    {
        public Tuple<List<AssociationPair>, int> LoadFromSql(
            IOrganizationService service, string sqlQuery,
            string entity1LogicalName, string entity2LogicalName)
        {
            var pairs = new List<AssociationPair>();
            var seen = new HashSet<(Guid, Guid)>();
            int skipped = 0;

            var request = new OrganizationRequest("ExecutePowerBISql");
            request["Query"] = sqlQuery;

            OrganizationResponse response;
            try
            {
                response = service.Execute(request);
            }
            catch (Exception ex) when (ex.Message.IndexOf("TDS", StringComparison.OrdinalIgnoreCase) >= 0
                                    || ex.Message.IndexOf("endpoint", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                throw new InvalidOperationException(
                    "SQL queries require the TDS endpoint to be enabled in your Dataverse environment. " +
                    "Enable it in the Power Platform Admin Center under Settings > Features.", ex);
            }

            // Handle EntityCollection response (most common)
            if (response.Results.Contains("Result") && response.Results["Result"] is EntityCollection ec)
            {
                DataSourceService.ExtractPairs(ec, pairs, seen,
                    entity1LogicalName, entity2LogicalName, ref skipped);
            }
            else
            {
                // Try to extract pairs from whatever format we got
                ExtractPairsFromResponse(response, pairs, seen, ref skipped);
            }

            return Tuple.Create(pairs, skipped);
        }

        private static void ExtractPairsFromResponse(OrganizationResponse response,
            List<AssociationPair> pairs, HashSet<(Guid, Guid)> seen, ref int skipped)
        {
            foreach (var key in response.Results.Keys)
            {
                var value = response.Results[key];

                if (value is EntityCollection ec)
                {
                    // Found an EntityCollection under a different key
                    DataSourceService.ExtractPairs(ec, pairs, seen, "", "", ref skipped);
                    return;
                }

                if (value is string json && json.TrimStart().StartsWith("["))
                {
                    // Attempt basic JSON array parsing for GUID pairs
                    ExtractPairsFromJson(json, pairs, seen, ref skipped);
                    return;
                }
            }

            // If we reach here, log diagnostic info
            var keys = string.Join(", ", response.Results.Keys);
            var types = string.Join(", ", response.Results.Values.Select(v => v?.GetType().FullName ?? "null"));
            throw new InvalidOperationException(
                $"Unexpected ExecutePowerBISql response format.\nKeys: {keys}\nTypes: {types}\n\n" +
                "Please report this to the plugin developer.");
        }

        private static void ExtractPairsFromJson(string json, List<AssociationPair> pairs,
            HashSet<(Guid, Guid)> seen, ref int skipped)
        {
            // Basic JSON array parsing without external dependencies
            // Expected format: [{"col1":"guid1","col2":"guid2",...}, ...]
            var rows = json.Split(new[] { '}' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var row in rows)
            {
                var guids = new List<Guid>();
                var parts = row.Split(new[] { '"', ':', ',' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var part in parts)
                {
                    if (Guid.TryParse(part.Trim(), out var g) && g != Guid.Empty)
                        guids.Add(g);
                }

                if (guids.Count >= 2)
                {
                    var pair = new AssociationPair { Guid1 = guids[0], Guid2 = guids[1] };
                    if (seen.Add(pair.NormalizedKey()))
                        pairs.Add(pair);
                }
                else if (guids.Count > 0)
                {
                    skipped++;
                }
            }
        }
    }
}
