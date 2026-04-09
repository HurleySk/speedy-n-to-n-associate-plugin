using System.Collections.Generic;

namespace SpeedyNtoNAssociatePlugin.Models
{
    public class DataSourceResult
    {
        public List<AssociationPair> Pairs { get; set; } = new List<AssociationPair>();
        public int SkippedCount { get; set; }
        public string DiagnosticLog { get; set; }
    }
}
