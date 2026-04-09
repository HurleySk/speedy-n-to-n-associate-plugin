using Microsoft.Xrm.Sdk;
using SpeedyNtoNAssociatePlugin.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace SpeedyNtoNAssociatePlugin.Services
{
    public class SqlDataSourceService
    {
        public DataSourceResult LoadFromSql(
            IOrganizationService service, string sqlQuery,
            string entity1LogicalName, string entity2LogicalName)
        {
            var pairs = new List<AssociationPair>();
            var seen = new HashSet<(Guid, Guid)>();
            int skipped = 0;
            string diagnosticLog = "";

            var request = new OrganizationRequest("ExecutePowerBISql");
            request["QueryText"] = sqlQuery;

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

            // ExecutePowerBISql returns a DataSet under the "Records" key
            if (response.Results.Contains("Records") && response.Results["Records"] is DataSet ds)
            {
                diagnosticLog = DescribeDataSet(ds);
                ExtractPairsFromDataSet(ds, pairs, seen, ref skipped);
            }
            else if (response.Results.Contains("Result") && response.Results["Result"] is EntityCollection ec)
            {
                diagnosticLog = $"EntityCollection with {ec.Entities.Count} entities.";
                DataSourceService.ExtractPairs(ec, pairs, seen,
                    entity1LogicalName, entity2LogicalName, ref skipped);
            }
            else
            {
                var keys = string.Join(", ", response.Results.Keys);
                var types = string.Join(", ", response.Results.Values.Select(v => v?.GetType().FullName ?? "null"));
                throw new InvalidOperationException(
                    $"Unexpected ExecutePowerBISql response format.\nKeys: {keys}\nTypes: {types}\n\n" +
                    "Please report this to the plugin developer.");
            }

            return new DataSourceResult { Pairs = pairs, SkippedCount = skipped, DiagnosticLog = diagnosticLog };
        }

        private static string DescribeDataSet(DataSet ds)
        {
            if (ds.Tables.Count == 0)
                return "DataSet has 0 tables.";

            var table = ds.Tables[0];
            var colDescs = new List<string>();
            for (int i = 0; i < table.Columns.Count; i++)
            {
                var c = table.Columns[i];
                colDescs.Add($"{c.ColumnName} ({c.DataType.Name})");
            }

            var desc = $"DataSet: {table.Rows.Count} rows, {table.Columns.Count} columns: [{string.Join(", ", colDescs)}]";

            if (table.Rows.Count > 0)
            {
                var firstRowVals = new List<string>();
                for (int i = 0; i < table.Columns.Count; i++)
                {
                    var val = table.Rows[0][i];
                    firstRowVals.Add(val == DBNull.Value ? "NULL" : val.ToString());
                }
                desc += $" | Row 0: [{string.Join(", ", firstRowVals)}]";
            }

            return desc;
        }

        private static void ExtractPairsFromDataSet(DataSet ds, List<AssociationPair> pairs,
            HashSet<(Guid, Guid)> seen, ref int skipped)
        {
            if (ds.Tables.Count == 0)
                return;

            var table = ds.Tables[0];

            // Find columns that contain GUIDs by checking the first data row
            var guidColumnIndices = new List<int>();

            if (table.Rows.Count > 0)
            {
                for (int col = 0; col < table.Columns.Count; col++)
                {
                    var colType = table.Columns[col].DataType;
                    if (colType == typeof(Guid))
                    {
                        guidColumnIndices.Add(col);
                    }
                    else if (colType == typeof(string))
                    {
                        // Check if the first row's value parses as a GUID
                        var val = table.Rows[0][col];
                        if (val != DBNull.Value && Guid.TryParse(val.ToString(), out _))
                            guidColumnIndices.Add(col);
                    }
                }
            }

            if (guidColumnIndices.Count < 2)
            {
                throw new InvalidOperationException(
                    $"SQL query returned {table.Rows.Count} rows but only {guidColumnIndices.Count} GUID column(s) found. " +
                    "The query must return at least two GUID columns.");
            }

            int col1 = guidColumnIndices[0];
            int col2 = guidColumnIndices[1];

            foreach (DataRow row in table.Rows)
            {
                Guid g1, g2;

                var v1 = row[col1];
                var v2 = row[col2];

                if (v1 == DBNull.Value || v2 == DBNull.Value)
                {
                    skipped++;
                    continue;
                }

                if (v1 is Guid guid1)
                    g1 = guid1;
                else if (!Guid.TryParse(v1.ToString(), out g1))
                {
                    skipped++;
                    continue;
                }

                if (v2 is Guid guid2)
                    g2 = guid2;
                else if (!Guid.TryParse(v2.ToString(), out g2))
                {
                    skipped++;
                    continue;
                }

                if (g1 == Guid.Empty || g2 == Guid.Empty)
                {
                    skipped++;
                    continue;
                }

                var pair = new AssociationPair { Guid1 = g1, Guid2 = g2 };
                if (seen.Add(pair.NormalizedKey()))
                    pairs.Add(pair);
            }
        }
    }
}
