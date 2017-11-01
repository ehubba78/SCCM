using Microsoft.ConfigurationManagement.ManagementProvider;
using Microsoft.ConfigurationManagement.ManagementProvider.WqlQueryEngine;
using SCCM.Common;
using SCCM.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SCCM.Infrastructure
{
    public class Queries
    {
        public static List<C_Collections> getAllCollections(string PrimaryServer, bool SDSOnly)
        {
            var results = new List<C_Collections>();

            var query = "select CollectionID, Name from SMS_Collection";

            using (var connection = InternalFunctions.Connect(PrimaryServer))
            {
                foreach (IResultObject getobject in connection.QueryProcessor.ExecuteQuery(query))
                {
                    try
                    {
                        var entry = new C_Collections()
                        {
                            CollectionID = getobject["CollectionID"].StringValue,
                            Name = getobject["Name"].StringValue
                        };

                        results.Add(entry);
                    }
                    catch
                    {
                        //Do nothing as request will be sent back as false                        
                    }
                }
            }

            return results;
        }

        public static List<QueryBaseMembership> GetAllQueryBaseMemberships(string PrimaryServer, Collection coll)
        {
            var memberships = new List<QueryBaseMembership>();

            using (var connection = InternalFunctions.Connect(PrimaryServer))
            {
                var collection = connection.GetInstance(@"SMS_Collection.CollectionID='" + coll.CollectionID + "'");

                try
                {
                    collection.Get();
                }
                catch
                {
                    var ex = new Exception("CollectionID Provided is invalid...Validate and try again!");
                    throw ex;
                }

                var results = collection.GetArrayItems("CollectionRules");
                foreach (IResultObject rule in results)
                {
                    var entry = new QueryBaseMembership()
                    {
                        ID = rule["QueryID"].StringValue,
                        Query = rule["QueryExpression"].StringValue,
                        RuleName = rule["RuleName"].StringValue
                    };

                    memberships.Add(entry);
                }
            }

            return memberships;
        }

        public static string GetSourceVersion(string getServer, string PackageID)
        {
            using (var connection = InternalFunctions.Connect(getServer))
            {
                foreach (IResultObject getobject in connection.QueryProcessor.ExecuteQuery("select * from SMS_Package WHERE PackageID='" + PackageID + "'"))
                {
                    try
                    {
                        return getobject["SourceVersion"].StringValue;
                    }
                    catch
                    {
                        //Do nothing as request will be sent back as false                        
                    }
                }
            }

            return "ERR";

        }

        /// <summary>
        /// Confirms Device is listed in the collection ID Specified.  If found returns the ResourceID of that Device
        /// </summary>
        /// <param name="getServer"></param>
        /// <param name="workstation"></param>
        /// <param name="CollectionID"></param>
        /// <returns></returns>
        public static string ConfirmDeviceInCollection(string getServer, string workstation, string CollectionID)
        {
            using (var connection = InternalFunctions.Connect(getServer))
            {
                foreach (IResultObject getobject in connection.QueryProcessor.ExecuteQuery("SELECT * FROM SMS_FullCollectionMembership Where Name = '" + workstation + "' AND CollectionID = '" + CollectionID + "'"))
                {
                    try
                    {
                        return getobject["ResourceID"].StringValue;

                    }
                    catch
                    {
                        return "";
                    }
                }
            }

            return "";
        }

        /// <summary>
        /// Gets the GUID of the Device Specified
        /// </summary>
        /// <param name="DeviceName"></param>
        /// <param name="getMAC"></param>
        /// <param name="SCCMServer"></param>
        /// <returns></returns>
        public static string getGUID(string DeviceName, string getMAC, string SCCMServer)
        {
            var result = "";

            using (var connection = InternalFunctions.Connect(SCCMServer))
            {
                var query = "SELECT * FROM SMS_R_System WHERE MACAddresses IN ('" + getMAC + "') AND Name = '" +
                            DeviceName + "'";
                //"select * from SMS_R_System Where Name='" + DeviceName + "' AND isActive = 1";

                foreach (IResultObject getRecord in connection.QueryProcessor.ExecuteQuery(query))
                {
                    try
                    {
                        result = getRecord["SMBIOSGUID"].StringValue;
                    }
                    catch
                    {
                        result = "UNKNOWN";
                    }
                }
            }

            return result;
        }
        /// <summary>
        /// Searches SCCM using provided MAC Address and returns all the results
        /// </summary>
        /// <param name="MACAddress"></param>
        /// <param name="SCCMServer"></param>
        /// <returns></returns>
        public static List<PCResult> getActivePCs(string MACAddress, string SCCMServer)
        {
            var results = new List<PCResult>();

            using (var connection = InternalFunctions.Connect(SCCMServer))
            {
                var query = "select * from SMS_R_System Where ";

                if (MACAddress.Contains(":"))
                {
                    query = query + "MACAddress='" + MACAddress + "'";
                }
                else
                {
                    query = query + "ResourceID =" + MACAddress;
                }

                query = query + " AND Active = 1";

                foreach (IResultObject getRecord in connection.QueryProcessor.ExecuteQuery(query))
                {
                    var result = new PCResult
                    {
                        Name = getRecord["NetBIOSName"].StringValue,
                        ResourceID = getRecord["ResourceID"].StringValue,
                        AssociatedUserWithDevice = getRecord["LastLogonUserName"].StringValue,
                        isActive = 1
                    };

                    results.Add(result);
                }
            }

            return results;
        }
        public static List<PCResult> getAllPCs(string MACAddress, string SCCMServer)
        {
            var results = new List<PCResult>();

            using (var connection = InternalFunctions.Connect(SCCMServer))
            {
                var query = "";

                if (MACAddress.Length == 8) //ResourceID
                {
                    query = "select * from SMS_R_System WHERE ResourceID = " + MACAddress;
                }
                else
                {
                    query = "select * from SMS_R_System Where MACAddresses='" + MACAddress + "'";
                }

                foreach (IResultObject getRecord in connection.QueryProcessor.ExecuteQuery(query))
                {
                    var result = new PCResult
                    {
                        Name = getRecord["NetBIOSName"].StringValue,
                        ResourceID = getRecord["ResourceID"].StringValue
                    };

                    try
                    {
                        result.isActive = getRecord["Active"].IntegerValue;
                    }
                    catch
                    {
                        result.isActive = 0;
                    }

                    results.Add(result);
                }
            }

            return results;
        }

        public static bool SearchCollectionforCPU(string CollectionID, string ResourceID, string getServer)
        {
            using (var connect = InternalFunctions.Connect(getServer))
            {
                var search = "SELECT Name FROM SMS_FullCollectionMembership WHERE CollectionID='" + CollectionID + "' AND ResourceID=" + ResourceID;

                foreach (var result in connect.QueryProcessor.ExecuteQuery(search))
                {
                    return true;
                }
            }

            return false;
        }

        public static List<SearchResults> SearchResult(string SCCMServer, string HostName, bool isFuzzy, string SQLConnectionString)
        {
            var result = new List<SearchResults>();
            var query =
                "SELECT ResourceId, Active, SMBIOSGUID, operatingSystem, createTimeStamp, Name, LastLogonTimestamp, LastLogonUsername, Macaddresses, DistinguishedName from SMS_R_SYSTEM";
            var getID = "";

            if (!isFuzzy)
            {
                if (HostName.Contains(":"))
                {
                    getID = FindResID(SCCMServer, HostName, true, true);

                    if (String.IsNullOrEmpty(getID))
                    {
                        var ex = new Exception("Could not Find any Record(s) with this name: " + HostName);
                        throw ex;
                    }
                    else
                    {
                        query = query + @" WHERE ResourceId=" + getID + "";
                    }
                }
                else
                {
                    query = query + " Where Name ='" + HostName + "'";
                }
            }
            else
            {
                query = query + " Where Name LIKE'%" + HostName + "%'";
            }

            using (var conn = InternalFunctions.Connect(SCCMServer))
            {
                foreach (
                IResultObject reader in
                    conn.QueryProcessor.ExecuteQuery(query))
                {
                    //if (
                    //    !string.IsNullOrEmpty(Actions.FindResID(SCCMServer, reader["ItemKey"].ToString(), false, true))) //Does exists in WMI
                    //{
                    var item = new SearchResults()
                    {

                        Name = String.IsNullOrEmpty(reader["Name"].ToString()) ? "" : reader["Name"].ToString(),
                        SMBIOSGUID =
                        String.IsNullOrEmpty(reader["SMBIOSGUID"].ToString())
                            ? ""
                            : reader["SMBIOSGUID"].ToString(),
                        ResourceID = String.IsNullOrEmpty(reader["ResourceId"].ToString()) ? "" : reader["ResourceId"].ToString()
                    };


                    try
                    {
                        item.Active = !String.IsNullOrEmpty(reader["Active"].ToString());
                    }
                    catch
                    {
                        item.Active = false;
                    }

                    //item.Macaddresseses = SQL_Queries.GetMacAddresses(item.ResourceID, conn.ConnectionString, item.Active);

                    item.Macaddresseses = GetMacAddressesfromWMI(reader["MacAddresses"].StringArrayValue, conn,
                        item.ResourceID);

                    if (item.Active)
                    {
                        item.AD_Path = String.IsNullOrEmpty(reader["DistinguishedName"].ToString()) ? "" : reader["DistinguishedName"].ToString();
                        //item.LastHardwareScan = string.IsNullOrEmpty(reader["LastHardwareScan"].ToString()) ? "NA" : reader["LastHardwareScan"].ToString();
                        item.LastLoginDate = String.IsNullOrEmpty(reader["LastLogonTimestamp"].DateTimeValue.ToString()) ? "NA" : reader["LastLogonTimestamp"].DateTimeValue.ToString();

                        item.LastUserLoggedIn = String.IsNullOrEmpty(reader["LastLogonUsername"].ToString()) ? "NA" : reader["LastLogonUsername"].ToString();
                        item.OS = String.IsNullOrEmpty(reader["operatingSystem"].ToString()) ? "UNKNOWN" : reader["operatingSystem"].ToString();
                        item.AddRemoveSoftwares = SQLQueries.GetSoftware(item.ResourceID, SQLConnectionString);

                        try
                        {
                            var results = SQLQueries.getScanDates(item.ResourceID, SQLConnectionString);

                            item.LastHardwareScan = String.IsNullOrEmpty(results[0]) ? "NA" : results[0];
                            item.LastSoftScan = String.IsNullOrEmpty(results[1]) ? "NA" : results[1];
                        }
                        catch
                        {
                            item.LastHardwareScan = "NA";
                            item.LastSoftScan = "NA";
                        }

                    }
                    else
                    {
                        item.OS = "UNKNOWN";
                    }

                    if (item.ResourceID != "")
                        //item.Collections = GetCollections(item.ResourceID, conn.ConnectionString);
                        item.Collections = GetCollectionsFromWMI(item.ResourceID, conn);

                    result.Add(item);
                    //}

                }
            }


            return result;
        }       

        private static List<C_Macaddresses> GetMacAddressesfromWMI(string[] macAddresses, WqlConnectionManager conn, string resourceID)
        {
            var results = new List<C_Macaddresses>();

            foreach (IResultObject getobject in conn.QueryProcessor.ExecuteQuery("select * from sms_g_system_network_adapter_configuration where ResourceID = '" + resourceID + "' AND MACAddress NOT null"))
            {
                var result = new C_Macaddresses
                {
                    MACAddress = getobject["MACAddress"].StringValue,
                    Description = getobject["Description"].StringValue
                };


                results.Add(result);
            }

            return results;
        }

        internal static List<C_Collections> GetCollectionsFromWMI(string resourceID, WqlConnectionManager conn)
        {
            var Results = new List<C_Collections>();
            var collectionID = "";
            foreach (IResultObject entry in conn.QueryProcessor.ExecuteQuery("select * from SMS_FullCollectionMembership Where ResourceID='" + resourceID + "'"))
            {
                if (String.IsNullOrEmpty(collectionID))
                {
                    collectionID = "'" + entry["CollectionID"].StringValue + "'";
                }
                else
                {
                    collectionID = collectionID + ",'" + entry["CollectionID"].StringValue + "'";
                }
            }

            if (!String.IsNullOrEmpty(collectionID))
            {
                var queryCollection = "Select Name from SMS_Collection Where CollectionID IN (" + collectionID + ")";


                foreach (IResultObject getCollection in conn.QueryProcessor.ExecuteQuery(queryCollection))
                {
                    var result = new C_Collections
                    {
                        Name = getCollection["Name"].StringValue
                    };

                    Results.Add(result);
                }

            }
            return Results;

        }

        public static string FindResID(string SCCMServer, string workstation, bool onlyActive, bool SearchForResID)
        {
            var query = "";

            if (SearchForResID)
            {
                query = "SELECT * FROM SMS_R_System WHERE ResourceID =" + workstation;
            }
            else
            {
                query = "SELECT * FROM SMS_R_System WHERE NetBiosName='" + workstation + "'";
            }

            if (onlyActive)
            {
                query = query + " AND Active=1";
            }

            using (var connection = InternalFunctions.Connect(SCCMServer))
            {

                foreach (
                    IResultObject getobject in
                        connection.QueryProcessor.ExecuteQuery(query))
                {
                    try
                    {
                        return getobject["ResourceID"].StringValue;
                    }
                    catch
                    {
                        //Do nothing as request will be sent back as false
                    }
                }
            }
            return "";
        }

        public static List<PCResult> getAllPCsByName(string HostName, string SCCMServer)
        {
            var results = new List<PCResult>();
            var query = "select * from  WHERE ResourceName='" + HostName + "'";

            using (var connection = InternalFunctions.Connect(SCCMServer))
            {
                foreach (
                    IResultObject getRecord in
                    connection.QueryProcessor.ExecuteQuery(query))
                {
                    var result = new PCResult
                    {
                        Name = getRecord["ResourceName"].StringValue,
                        ResourceID = getRecord["ResourceID"].StringValue,
                        AssociatedUserWithDevice = getRecord["UniqueUserName"].StringValue
                    };

                    if (getRecord["IsActive"].StringValue == "True")
                    {
                        result.isActive = 1;
                    }
                    else
                    {
                        result.isActive = 0;
                    }

                    results.Add(result);
                }
            }

            return results;
        }

    }
}
