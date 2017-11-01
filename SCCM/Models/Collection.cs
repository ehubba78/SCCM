using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SCCM.Models
{
    public class Collection
    {
        public string CollectionName { get; set; }
        public string Comment { get; set; }
        public bool ownedByThisSite { get; set; }
        public List<DirectMembership> DirectMemberships { get; set; }
        public List<QueryBaseMembership> QueryBaseMemberships { get; set; }
        public string LimitedToCollectionID { get; set; }

        //Return Variables
        public string QueryID { get; set; }
        public string CollectionID { get; set; }
    }
}
