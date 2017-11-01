using System.Collections.Generic;

namespace SCCM.Models
{
    public class SearchResults
    {
        public string Name { get; set; }
        public bool Active { get; set; }
        public string SMBIOSGUID { get; set; }
        public object LastHardwareScan { get; set; }
        public string LastSoftScan { get; set; }
        public string LastLoginDate { get; set; }
        public string LastUserLoggedIn { get; set; }
        public string ClientVersion { get; set; }
        public string OS { get; set; }
        public string AD_Path { get; set; }
        public bool AD_Enabled { get; set; }
        public string LifeSpan { get; set; }
        public List<C_Collections> Collections { get; set; }
        public List<C_Macaddresses> Macaddresseses { get; set; }
        public string ResourceID { get; internal set; }
        public List<C_AddRemoveSoftware> AddRemoveSoftwares { get; set; }
    }

    public class C_Collections
    {
        public string Name { get; set; }
        public bool isDirect { get; set; }
        public string CollectionID { get; set; }
        public string ShoppingCollection { get; set; }
    }

    public class C_Macaddresses
    {
        public string MACAddress { get; set; }
        public string Description { get; set; }
    }

    public class C_AddRemoveSoftware
    {
        public string Software { get; set; }
        public string Publisher { get; set; }
        public string DiscoveredOn { get; set; }
        public string InstallDate { get; set; }
        public string Version { get; set; }

    }
}
