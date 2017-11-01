using System.Collections.Generic;

namespace SCCM.Models
{
    public class OS
    {
        public string Name { get; set; }
        public string ID { get; set; }
        public bool isBeta { get; set; }

        public List<CollectionBucket> CollectionofPC { get; set; }
    }
    
}
