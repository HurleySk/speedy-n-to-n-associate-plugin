using System;

namespace SpeedyNtoNAssociatePlugin.Models
{
    public class AssociationPair
    {
        public Guid Guid1 { get; set; }
        public Guid Guid2 { get; set; }

        public (Guid, Guid) NormalizedKey()
        {
            return Guid1.CompareTo(Guid2) <= 0 ? (Guid1, Guid2) : (Guid2, Guid1);
        }
    }
}
