namespace SpeedyNtoNAssociatePlugin.Models
{
    public class AssociationRunOptions
    {
        public RelationshipInfo Relationship { get; set; }
        public int DegreeOfParallelism { get; set; }
        public bool BypassPlugins { get; set; }
        public bool VerboseLogging { get; set; }
        public int MaxRetries { get; set; }
        public int BatchSize { get; set; }
        public bool FireAndForget { get; set; }
    }
}
