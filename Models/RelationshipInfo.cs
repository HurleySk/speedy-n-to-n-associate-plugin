namespace SpeedyNtoNAssociatePlugin.Models
{
    public class RelationshipInfo
    {
        public string SchemaName { get; set; }
        public string Entity1LogicalName { get; set; }
        public string Entity2LogicalName { get; set; }
        public string IntersectEntityName { get; set; }
        public string Entity1IntersectAttribute { get; set; }
        public string Entity2IntersectAttribute { get; set; }

        public override string ToString()
        {
            return $"{SchemaName} ({Entity1LogicalName} <-> {Entity2LogicalName})";
        }
    }
}
