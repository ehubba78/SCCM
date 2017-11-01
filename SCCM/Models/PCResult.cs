using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SCCM.Models
{
    public class PCResult
    {
        public string Name { get; set; }
        public string ResourceID { get; set; }
        public int isActive { get; set; }
        public string AssociatedUserWithDevice { get; set; }
        public string LastSoftwareScan { get; set; }
        public string ClientVersion { get; set; }
    }
}
