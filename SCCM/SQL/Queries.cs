using SCCM.Common;
using SCCM.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SCCM.SQL
{
    public class Queries
    {
        public static string FindCollectionIDByName(string NameofCollection, string sCCMSQLConnectionString)
        {
            var result = "";
            using (var conn = new SqlConnection(sCCMSQLConnectionString))
            {
                conn.Open();
                using (var comm = new SqlCommand())
                {
                    comm.CommandText = @"SELECT CollectionID from v_Collection WHERE Name LIKE '%" + NameofCollection +
                                       "'";
                    comm.CommandType = CommandType.Text;
                    comm.Connection = conn;

                    result = comm.ExecuteScalar().ToString();
                }
            }

            return result;
        }

        public static List<PCResult> ListActiveDevices(string SQLConnectionString, string DeviceName, string Username)
        {
            var results = new List<PCResult>();

            var query = @"SELECT f.Netbios_Name0, e.LastSoftwareScan, a.Client_Version0 from System_DISC a
Left Join vSoftwareInventoryStatus e ON a.ItemKey = e.ResourceID 
INNER JOIN v_R_System_Valid f ON a.ItemKey = f.ResourceID
WHERE f.Netbios_Name0 ='" + DeviceName + "' OR f.User_Name0 = '" + Username + "' AND a.Active0 = 1 AND f.Is_Virtual_Machine0 = 0";

            using (var conn = new SqlConnection(SQLConnectionString))
            {
                conn.Open();
                using (var comm = new SqlCommand())
                {
                    comm.CommandText = query;
                    comm.CommandType = CommandType.Text;
                    comm.Connection = conn;

                    var reader = comm.ExecuteReader();
                    while (reader.Read())
                    {
                        var entry = new PCResult()
                        {
                            Name = reader["Netbios_Name0"].ToString(),
                            LastSoftwareScan = reader["LastSoftwareScan"].ToString(),
                            ClientVersion = reader["Client_Version0"].ToString()
                        };

                        results.Add(entry);
                    }

                    reader.Close();
                }
            }

            return results;
        }

        public static List<PCReport> PCImageReport(string DeviceName, string SQLConnectionString, string StartTime)
        {
            var result = new List<PCReport>();

            if (String.IsNullOrEmpty(StartTime))
            {
                StartTime = DateTime.Now.AddHours(-12).ToString(); //Defaults to the last 12 hours if no value was submitted
            }

            var query = @"SELECT
 CASE [Severity] 
  WHEN '1073741824' THEN 'Informational' 
  WHEN '-1073741824' THEN 'Error' 
  WHEN '-2147483648' THEN 'Warning' 
 END AS Severity
 ,CAST([InsStrValue1] AS int) AS 'STEP'
  ,[Time]
  ,[Component]
  ,[MessageID]
  ,CASE [MessageID] 
 WHEN '11122' THEN ('Failed execuiting in the group (' + [InsStrValue3] + ') with exit code ' + [InsStrValue4])  
  WHEN '11124' THEN ('Task Sequence started the group (' + [InsStrValue3] + ').')
  WHEN '11127' THEN ('Successfully completed the group (' + [InsStrValue3] + ').') 
  WHEN '11128' THEN ('Task Sequence skipped the disabled action (' + [InsStrValue2] + ') in the group (' + [InsStrValue3] + ').') 
  WHEN '11130' THEN ('Task Sequence skipped the action (' + [InsStrValue2] + ') in the group (' + [InsStrValue3] + ').')
  WHEN '11134' THEN ('Successfully completed the action (' + [InsStrValue2] + ') in the group (' + [InsStrValue3] + ') with exit code ' + [InsStrValue4] + ' Action output: ' + (COALESCE([InsStrValue5], '') + '' + COALESCE([InsStrValue6], '') + '' + COALESCE([InsStrValue7],'')+ COALESCE([InsStrValue8],'')+ COALESCE([InsStrValue9],'')+ COALESCE([InsStrValue10],''))) 
  WHEN '11135' THEN ('Failed execuiting the action (' + [InsStrValue2] + ') in the group (' + [InsStrValue3] + ') with exit code ' + [InsStrValue4] + ' Action output: ' + (COALESCE([InsStrValue5], '') + '' + COALESCE([InsStrValue6], '') + '' + COALESCE([InsStrValue7],'')+ COALESCE([InsStrValue8],'')+ COALESCE([InsStrValue9],'')+ COALESCE([InsStrValue10],'')))  
  WHEN '11138' THEN ('Task Sequence ignored execution failure of the action (' + [InsStrValue2] + ') in the group (' + [InsStrValue3] + ').')  
  WHEN '11140' THEN ('Task Sequence started execution of a task sequence.')  
  WHEN '11142' THEN ('Task Sequence performed a system reboot initiated by the action (' + [InsStrValue2] + ') in the group (' + [InsStrValue3] + ').')  
  WHEN '11144' THEN ('Non-client started execution of a task sequence.')
 END AS Description
FROM vStatusMessagesWithStrings a (NOLOCK) 
WHERE MachineName = '" + DeviceName + @"'
 AND Component in ('Task Sequence Engine','Task Sequence Manager','Task Sequence Action')";

            if (StartTime != "0")
            {
                query = query + "AND Time BETWEEN '" + StartTime + "' AND GETUTCDATE() ORDER BY Time DESC";
            }
            else
            {
                query = query + " ORDER BY Time DESC";
            }

            using (var conn = new SqlConnection(SQLConnectionString))
            {
                conn.Open();
                using (var comm = new SqlCommand())
                {
                    comm.CommandText = query;
                    comm.CommandType = CommandType.Text;
                    comm.Connection = conn;

                    var reader = comm.ExecuteReader();

                    while (reader.Read())
                    {
                        var entry = new PCReport()
                        {
                            Severity = reader["Severity"].ToString(),
                            Time = reader["Time"].ToString(),
                            Component = reader["Component"].ToString(),
                            MessageID = reader["MessageID"].ToString(),
                            Step = reader["STEP"].ToString(),
                            Description = string.IsNullOrEmpty(reader["Description"].ToString()) ? "" : reader["Description"].ToString()
                        };

                        result.Add(entry);
                    }
                }
            }

            return result;
        }

        public static List<SearchResults> SearchResult(string HostName, string SQLConnectionString, string SCCMServer, bool isFuzzy)
        {
            var result = new List<SearchResults>();
            var query =
                @"SELECT a.ResourceID, a.Active0, a.SMS_Unique_Identifier0, a.operatingSystem0, a.createTimeStamp0, a.Name0, a.Last_Logon_Timestamp0, a.User_Name0, c.LastBootUpTime0, d.LastHardwareScan, e.LastSoftwareScan, a.Distinguished_Name0  from v_R_System a
Left Join v_GS_OPERATING_SYSTEM c ON a.ResourceID = c.ResourceID
Left Join vWorkstationStatus d ON a.ResourceID = d.ResourceID
Left Join vSoftwareInventoryStatus e ON a.ResourceID = e.ResourceID";

            if (!isFuzzy)
            {
                if (HostName.Contains(':'))
                {
                    query = query +
                            " WHERE a.ResourceID IN (SELECT ResourceID FROM v_GS_NETWORK_ADAPTER_CONFIGURATION WHERE MacAddress0 = '" +
                            HostName + "')";
                }
                else
                {
                    query = query + " WHERE a.Name0 ='" + HostName + "'";
                }
            }
            else
            {
                query = query + " WHERE a.Name0 LIKE'%" + HostName + "%'";
            }

            using (var conn = new SqlConnection(SQLConnectionString))
            {
                conn.Open();
                using (var comm = new SqlCommand())
                {
                    comm.CommandText = query;
                    comm.CommandType = CommandType.Text;
                    comm.Connection = conn;

                    var reader = comm.ExecuteReader();
                    while (reader.Read())
                    {

                        var item = new SearchResults()
                        {

                            Name = string.IsNullOrEmpty(reader["Name0"].ToString()) ? "" : reader["Name0"].ToString(),
                            SMBIOSGUID =
                            string.IsNullOrEmpty(reader["SMS_Unique_Identifier0"].ToString())
                                ? ""
                                : reader["SMS_Unique_Identifier0"].ToString(),
                            ResourceID = string.IsNullOrEmpty(reader["ResourceID"].ToString()) ? "" : reader["ResourceID"].ToString()
                        };


                        try
                        {
                            item.Active = !string.IsNullOrEmpty(reader["Active0"].ToString());
                        }
                        catch
                        {
                            item.Active = false;
                        }

                        item.Macaddresseses = GetMacAddresses(item.ResourceID, conn.ConnectionString, item.Active);

                        if (item.Active)
                        {
                            item.AD_Path = string.IsNullOrEmpty(reader["Distinguished_Name0"].ToString()) ? "" : reader["Distinguished_Name0"].ToString();
                            item.LastHardwareScan = string.IsNullOrEmpty(reader["LastHardwareScan"].ToString()) ? "NA" : reader["LastHardwareScan"].ToString();
                            item.LastLoginDate = string.IsNullOrEmpty(reader["Last_Logon_Timestamp0"].ToString()) ? "NA" : reader["Last_Logon_Timestamp0"].ToString();
                            item.LastSoftScan = string.IsNullOrEmpty(reader["LastSoftwareScan"].ToString()) ? "NA" : reader["LastSoftwareScan"].ToString();
                            item.LastUserLoggedIn = string.IsNullOrEmpty(reader["User_Name0"].ToString()) ? "NA" : reader["User_Name0"].ToString();
                            item.OS = string.IsNullOrEmpty(reader["operatingSystem0"].ToString()) ? "UNKNOWN" : reader["operatingSystem0"].ToString();
                            item.AddRemoveSoftwares = GetSoftware(item.ResourceID, conn.ConnectionString);
                        }
                        else
                        {
                            item.OS = "UNKNOWN";
                        }

                        if (item.ResourceID != "")
                        {                            
                            using (var connection = InternalFunctions.Connect(SCCMServer))
                            {
                                item.Collections = Infrastructure.Queries.GetCollectionsFromWMI(item.ResourceID, connection);
                            }
                        }

                        result.Add(item);


                    }

                    reader.Close();
                }
            }

            return result;

        }

        internal static List<C_AddRemoveSoftware> GetSoftware(string resourceID, string connectionString)
        {
            var results = new List<C_AddRemoveSoftware>();

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (var comm = new SqlCommand())
                {
                    comm.CommandText = @"SELECT DisplayName0,TimeStamp AS 'Discovered_ON', InstallDate0 AS 'Install_Date', Publisher0, Version0 FROM v_Add_Remove_Programs 
WHERE ResourceID = " + resourceID + " ORDER BY DisplayName0";
                    comm.CommandType = CommandType.Text;
                    comm.Connection = conn;

                    var reader = comm.ExecuteReader();

                    while (reader.Read())
                    {
                        var item = new C_AddRemoveSoftware
                        {
                            Software = reader["DisplayName0"].ToString(),
                            Publisher = reader["Publisher0"].ToString(),
                            Version = reader["Version0"].ToString()
                        };

                        try
                        {
                            item.DiscoveredOn = reader["Discovered_ON"].ToString();
                        }
                        catch
                        {
                            item.DiscoveredOn = "NA";
                        }

                        try
                        {
                            item.InstallDate = reader["Install_Date"].ToString();
                        }
                        catch
                        {
                            item.InstallDate = "NA";
                        }

                        results.Add(item);
                    }

                    reader.Close();
                }
            }

            return results;
        }

        internal static string[] getScanDates(string ResID, string SQLConnectionString)
        {
            var result = new string[1];
            var query = @"SELECT d.LastHardwareScan, e.LastSoftwareScan from System_DISC a
Left Join vWorkstationStatus d ON a.ItemKey = d.ResourceID
Left Join vSoftwareInventoryStatus e ON a.ItemKey = e.ResourceID 
WHERE a.ImageKey ='" + ResID + "' AND a.Active0 = 1";

            using (var conn = new SqlConnection(SQLConnectionString))
            {
                conn.Open();
                using (var comm = new SqlCommand())
                {
                    comm.CommandText = query;
                    comm.CommandType = CommandType.Text;
                    comm.Connection = conn;

                    var reader = comm.ExecuteReader();

                    while (reader.Read())
                    {
                        result[0] = reader["LastHardwareScan"].ToString();
                        result[1] = reader["LastSoftwareScan"].ToString();
                    }

                    reader.Close();
                }
            }

            return result;

        }
        internal static List<C_Collections> GetCollections(string resourceID, string SQLConnectionString)
        {
            var results = new List<C_Collections>();

            using (var conn = new SqlConnection(SQLConnectionString))
            {
                conn.Open();
                using (var comm = new SqlCommand())
                {
                    comm.CommandText = @"SELECT (CASE a.IsDirect WHEN 1 THEN 'Y' ELSE 'N' END) as Direct, b.Name, a.CollectionID from v_FullCollectionMembership a 
INNER JOIN v_Collection b ON a.CollectionID = b.CollectionID
WHERE ResourceID = " + resourceID + "ORDER BY b.Name";
                    comm.CommandType = CommandType.Text;
                    comm.Connection = conn;

                    var reader = comm.ExecuteReader();

                    while (reader.Read())
                    {
                        var item = new C_Collections
                        {
                            Name = reader["Name"].ToString(),
                            CollectionID = reader["CollectionID"].ToString(),
                            isDirect = reader["Direct"].ToString() == "Y" ? true : false,
                            ShoppingCollection = reader["Name"].ToString().Contains("AXP - ") ? "Y" : "N"
                        };

                        results.Add(item);
                    }
                }
            }

            return results;
        }

        private static List<C_Macaddresses> GetMacAddresses(string resID, string SQLConnectionString, bool isActive)
        {
            var results = new List<C_Macaddresses>();
            var query = @"SELECT MACAddress0, Description0 FROM v_GS_NETWORK_ADAPTER_CONFIGURATION WHERE ResourceID = " +
                        resID + " AND MACAddress0 IS NOT NULL ORDER BY TimeStamp DESC";

            if (!isActive)
            {
                query = @"SELECT MAC_Addresses0 FROM System_MAC_Addres_ARR WHERE Itemkey = " + resID;
            }

            using (var conn = new SqlConnection(SQLConnectionString))
            {
                conn.Open();
                using (var comm = new SqlCommand())
                {
                    comm.CommandText = query;
                    comm.CommandType = CommandType.Text;
                    comm.Connection = conn;

                    var reader = comm.ExecuteReader();
                    while (reader.Read())
                    {
                        var item = new C_Macaddresses
                        {
                            MACAddress = isActive ? reader["MACAddress0"].ToString() : reader["MAC_Addresses0"].ToString(),
                            Description = isActive ? reader["Description0"].ToString() : ""
                        };

                        results.Add(item);
                    }

                    reader.Close();
                }
            }

            return results;
        }
        public static List<PCResult> SearchTopConsoleUser(string HostName, string SQLConnectionString)
        {
            var results = new List<PCResult>();
            var query = @"SELECT a.ResourceID, a.Active0, b.UniqueUserName FROM v_R_System a
INNER JOIN v_UserMachineRelationship b ON a.ResourceID = b.MachineResourceID
WHERE a.Netbios_Name0 = '" + HostName + "'";

            using (var conn = new SqlConnection(SQLConnectionString))
            {
                conn.Open();
                using (var comm = new SqlCommand())
                {
                    comm.Connection = conn;
                    comm.CommandType = CommandType.Text;
                    comm.CommandText = query;

                    var reader = comm.ExecuteReader();

                    while (reader.Read())
                    {
                        var result = new PCResult
                        {
                            Name = HostName,
                            ResourceID = reader["ResourceID"].ToString(),
                            AssociatedUserWithDevice = reader["UniqueUserName"].ToString(),
                            isActive = Convert.ToInt32(reader["Active0"].ToString())
                        };

                        results.Add(result);
                    }

                    reader.Close();
                }
            }

            return results;
        }

    }
}
