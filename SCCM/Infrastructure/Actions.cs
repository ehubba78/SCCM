using Microsoft.ConfigurationManagement.ManagementProvider;
using Microsoft.ConfigurationManagement.ManagementProvider.WqlQueryEngine;
using SCCM.Common;
using SCCM.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SCCM.Infrastructure
{
    public class Actions
    {
        /// <summary>
        /// Creates a Static Collection which includes Direct Memberships
        /// </summary>
        /// <param name="PrimaryServer">Primary SCCM server required for WMI</param>
        /// <param name="collection">Request</param>
        /// <returns>DirectMemberships ID to confirm successful or not.</returns>
        public static Collection CreateStaticCollection(string PrimaryServer, Collection collection)
        {
            try
            {
                using (var connection = InternalFunctions.Connect(PrimaryServer))
                {
                    // Create a new SMS_Collection object.
                    var newCollection = connection.CreateInstance("SMS_Collection");

                    // Populate new collection properties.
                    newCollection["Name"].StringValue = collection.CollectionName;
                    newCollection["Comment"].StringValue = collection.Comment;
                    newCollection["OwnedByThisSite"].BooleanValue = collection.ownedByThisSite;
                    newCollection["LimitToCollectionID"].StringValue = collection.LimitedToCollectionID;

                    // Save the new collection object and properties.  
                    // In this case, it seems necessary to 'get' the object again to access the properties.  
                    newCollection.Put();
                    newCollection.Get();

                    collection.CollectionID = newCollection["CollectionID"].StringValue;
                    newCollection.Dispose();

                    // Create a new static rule object.
                    collection = AddResourcetoCollection(PrimaryServer, collection);
                }

                return collection;
            }
            catch (SmsException ex)
            {
                Console.WriteLine("Failed to create collection. Error: " + ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Adds new Query Base Memberships to an existing collection.
        /// </summary>
        /// <param name="getServer"></param>
        /// <param name="coll"></param>
        /// <returns>QueryMembership ID to confirm successful or not</returns>

        public static Collection AddQueryMembershiptoCollection(string getServer, Collection coll)
        {
            using (var connect = InternalFunctions.Connect(getServer))
            {

                try
                {
                    var collection = connect.GetInstance("SMS_Collection.CollectionID='" + coll.CollectionID + "'");

                    var validateQueryParameters = new Dictionary<string, object>
                    {
                        {"WQLQuery", coll.QueryBaseMemberships[0].Query}
                    };
                    var result = connect.ExecuteMethod("SMS_CollectionRuleQuery", "ValidateQuery",
                        validateQueryParameters);

                    foreach (var membership in coll.QueryBaseMemberships)
                    {
                        // Create query rule.        
                        var newQueryRule = connect.CreateInstance("SMS_CollectionRuleQuery");
                        newQueryRule["QueryExpression"].StringValue = membership.Query;
                        newQueryRule["RuleName"].StringValue = membership.RuleName;

                        // Add the rule. Although not used in this sample, QueryID contains the query identifier.                           

                        var addMembershipRuleParameters = new Dictionary<string, object>
                        {
                            {"collectionRule", newQueryRule}
                        };
                        var queryID = collection.ExecuteMethod("AddMembershipRule", addMembershipRuleParameters);

                        membership.ID = queryID["QueryID"].StringValue;
                    }

                    // Start collection evaluator.        

                    collection.ExecuteMethod("RequestRefresh", null);

                    return coll;
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
        }

        /// <summary>
        /// Creates a Dynamic Collection within SCCM.  Uses Model Collection to gather all necessary data
        /// </summary>
        /// <param name="PRIMARYSite">SCCM's Primary Site use for WMI calls</param>
        /// <param name="Collection">Model used to gather details about the collection</param>
        /// <returns>returns the Model with the new COllection ID, this confirms collection was created.</returns>
        public static Collection CreateDynamicCollection(string PRIMARYSite, Collection collection)
        {
            try
            {
                using (var connection = InternalFunctions.Connect(PRIMARYSite))
                {
                    // Create new SMS_Collection object.        
                    var newCollection = connection.CreateInstance("SMS_Collection");
                    // Populate the new collection object properties.        
                    newCollection["Name"].StringValue = collection.CollectionName;
                    newCollection["Comment"].StringValue = collection.Comment;
                    newCollection["OwnedByThisSite"].BooleanValue = collection.ownedByThisSite;
                    newCollection["LimitToCollectionID"].StringValue = collection.LimitedToCollectionID;

                    // Save the new collection object and properties.        
                    // In this case, it seems necessary to 'get' the object again to access the properties.        

                    newCollection.Put();
                    newCollection.Get();

                    // Validate the query.        

                    var validateQueryParameters = new Dictionary<string, object>
                    {
                        {"WQLQuery", collection.QueryBaseMemberships[0].Query}
                    };
                    var result = connection.ExecuteMethod("SMS_CollectionRuleQuery", "ValidateQuery",
                        validateQueryParameters);

                    // Create query rule.        
                    var newQueryRule = connection.CreateInstance("SMS_CollectionRuleQuery");
                    newQueryRule["QueryExpression"].StringValue = collection.QueryBaseMemberships[0].Query;
                    newQueryRule["RuleName"].StringValue = collection.QueryBaseMemberships[0].RuleName;

                    // Add the rule. Although not used in this sample, QueryID contains the query identifier.                           

                    var addMembershipRuleParameters = new Dictionary<string, object> { { "collectionRule", newQueryRule } };
                    var queryID = newCollection.ExecuteMethod("AddMembershipRule", addMembershipRuleParameters);

                    // Start collection evaluator.        

                    newCollection.ExecuteMethod("RequestRefresh", null);

                    collection.QueryID = queryID["QueryID"].StringValue;
                    collection.CollectionID = newCollection["CollectionID"].StringValue;
                }

                return collection;
            }
            catch (SmsException ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// Resets the PC by removing the resource from all collections where it's listed as a Direct Membership.
        /// </summary>
        /// <param name="getCentral">Primary SCCM Server</param>
        /// <param name="getResID">Resource ID of device you are resetting</param>
        /// <param name="getPCName">Resource Name of device you are resetting</param>
        /// <returns>boolean response of request</returns>
        public static bool RESETPC(string getCentral, string getResID, string getPCName)
        {
            using (var connection = InternalFunctions.Connect(getCentral))
            {

                var query = "select * from SMS_FullCollectionMembership Where ResourceID='" + getResID +
                            "' AND IsDirect='true'";
                var isSuccess = true;

                foreach (IResultObject getobject in connection.QueryProcessor.ExecuteQuery(query))
                {
                    var queryCollection = "Select * from SMS_Collection Where CollectionID='" +
                                          getobject["CollectionID"].StringValue + "'";
                    foreach (IResultObject getCollection in connection.QueryProcessor.ExecuteQuery(queryCollection))
                    {
                        if (!getCollection["Name"].StringValue.ToLower().Contains("all"))
                        {
                            try
                            {
                                RemoveResourceCollection(Convert.ToInt32(getResID),
                                    getobject["CollectionID"].StringValue, getPCName, getCentral);
                            }
                            catch
                            {
                                isSuccess = false;

                            }
                        }
                    }
                }
                return isSuccess;

            }
        }

        /// <summary>
        /// Deletes a resource from a specific Collection
        /// </summary>
        /// <param name="getServer">Primary SCCM Server</param>
        /// <param name="getResID">Resource ID of device</param>
        /// <param name="workstation">Resource Name of device</param>
        /// <returns>Returns Object</returns>
        public static DeleteFromCollection DeleteResourceFromSccm(string getServer, string getResID, string workstation)
        {
            var result = new DeleteFromCollection() { isFound = false };

            using (var connection = InternalFunctions.Connect(getServer))
            {

                foreach (
                    IResultObject getobject in
                    connection.QueryProcessor.ExecuteQuery("SELECT * FROM SMS_R_System WHERE ResourceID = '" + getResID +
                                                           "'"))
                {
                    result.Workstation = getobject["Name"].StringValue;
                    result.isFound = true;
                    getobject.Delete();
                }
            }

            return result;
        }

        /// <summary>
        /// Adds a SCCM Resource to an SCCM Collection
        /// </summary>
        /// <param name="getServer">Primary SCCM Server</param>
        /// <param name="coll">Model Collection</param>
        /// <returns>Collection Object</returns>
        public static Collection AddResourcetoCollection(string getServer, Collection coll)
        {
            using (var connect = InternalFunctions.Connect(getServer))
            {
                try
                {
                    var collection = connect.GetInstance("SMS_Collection.CollectionID='" + coll.CollectionID + "'");

                    try
                    {
                        collection.Get();
                    }
                    catch
                    {
                        //Collection ID is not valid. When trying to get properties from collection triggered an exception
                        var ex = new Exception("CollectionID provided is invalid.  Please correct and try again!");
                        throw ex;
                    }

                    foreach (var directMembership in coll.DirectMemberships)
                    {
                        if (DoesResExists(connect, directMembership.ResourceID, coll.CollectionID))
                        {
                            directMembership.Status = "Already exists in collection (" + coll.CollectionID + ")";
                            continue;
                        }

                        var collectionRule = connect.CreateEmbeddedObjectInstance("SMS_CollectionRuleDirect");
                        collectionRule["ResourceClassName"].StringValue = "SMS_R_System";
                        collectionRule["ResourceID"].IntegerValue = directMembership.ResourceID;
                        collectionRule["RuleName"].StringValue = directMembership.DeviceName;

                        var Params = new Dictionary<string, object> { { "collectionRule", collectionRule } };

                        var staticID = collection.ExecuteMethod("AddMembershipRule", Params);

                        directMembership.Status = staticID["ReturnValue"].StringValue == "0"
                            ? "Successfully Added"
                            : "Failed to Add to collection";
                    }

                    InternalFunctions.SendRefresh(collection);

                    return coll;
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
        }     

        /// <summary>
        /// Retrieves a list of Task Sequences used for software bundles, primarily for Imaging purposes
        /// </summary>
        /// <param name="getServer">Primary SCCM Server</param>
        /// <param name="getFilter">Based on the naming convention of the TS names. If named by region, filter will only populate based on credentials you provide.</param>
        /// <returns>Profile Object</returns>
        public static List<Profiles> getBundles(string getServer, string getFilter)
        {
            string query;
            var results = new List<Profiles>();
            if (getFilter == "")
            {
                query = "select * from SMS_TaskSequencePackage WHERE Name LIKE '%TS-%'";
            }
            else
            {
                var splitValues = getFilter.Split(',');
                var tmpValue = "";

                foreach (var region in splitValues)
                {
                    if (tmpValue == "")
                    {
                        tmpValue = "Name LIKE '%" + region + "%' ";
                    }
                    else
                    {
                        tmpValue = tmpValue + "OR Name LIKE '%" + region + "%' ";
                    }
                }

                query = "select * from SMS_TaskSequencePackage WHERE " + tmpValue;
            }


            using (var connection = InternalFunctions.Connect(getServer))
            {
                foreach (IResultObject getobject in connection.QueryProcessor.ExecuteQuery(query))
                {
                    var result = new Profiles()
                    {
                        Name = getobject["Name"].StringValue,
                        ID = getobject["PackageID"].StringValue
                    };

                    results.Add(result);
                }
            }

            results.Sort(new InternalFunctions.ProfileInfoComparer());
            return results;
        }

        /// <summary>
        /// Retrieves a collection of the Base Images available for imaging.  Package needs to include in comments <ACTIVE> or <BETA> to be used for filtering purposes.
        /// </summary>
        /// <param name="getServer">SCCM Primary Server</param>
        /// <returns>Object of OS Base Images</returns>
        public static List<OS> getOSBaseImage(string getServer)
        {
            var results = new List<OS>();

            using (var connect = InternalFunctions.Connect(getServer))
            {
                const string query =
                    "select * from SMS_Collection WHERE Name LIKE '%_ZTI%' AND Comment LIKE '%<ACTIVE>%' OR Comment LIKE '%<BETA>%'";

                foreach (IResultObject getObject in connect.QueryProcessor.ExecuteQuery(query))
                {
                    var result = new OS();

                    if (getObject["Comment"].StringValue.Contains("<ACTIVE>"))
                    {
                        result.Name = getObject["Name"].StringValue.Replace("_ZTI_OSD-Nomad_", "");
                        result.ID = getObject["CollectionID"].StringValue;
                        result.isBeta = false;
                        results.Add(result);
                    }
                    else
                    {
                        result.Name = getObject["Name"].StringValue.Replace("_ZTI_OSD-Nomad_", "");
                        result.ID = getObject["CollectionID"].StringValue;
                        result.isBeta = true;
                        results.Add(result);
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Triggers the Refresh command in SCCM WMI on a specific collection.
        /// </summary>
        /// <param name="CollectionID">Collection ID</param>        
        /// <param name="getServer">Primary SCCM Server Name</param>
        /// <returns>Return Boolean on status of request</returns>
        public static bool RefreshCollection(string CollectionID, string getServer)
        {
            string getReturn;

            using (var connect = InternalFunctions.Connect(getServer))
            {
                var collection = connect.GetInstance("SMS_Collection.CollectionID='" + CollectionID + "'");

                var requestRefreshParameters = new Dictionary<string, object> { { "IncludeSubCollections", false } };
                var staticID = collection.ExecuteMethod("RequestRefresh", requestRefreshParameters);

                getReturn = staticID["ReturnValue"].StringValue;
            }

            return getReturn == "0";
        }

        /// <summary>
        /// Creates a New Resource in SCCM base on the information provided
        /// </summary>
        /// <param name="SCCMServer">SCCM Primary Server Name</param>
        /// <param name="Workstation">Device Name</param>
        /// <param name="getMACAddress">Device MAC Address (already validated if it's a valid address)</param>
        /// <returns>Resource ID of the Device</returns>
        public static string AddWorkstationToSCCM(string SCCMServer, string Workstation, string getMACAddress)
        {
            using (var connect = InternalFunctions.Connect(SCCMServer))
            {
                if (getMACAddress.Contains("-"))
                {
                    getMACAddress = getMACAddress.Replace("-", ":");
                }

                var inParams = new Dictionary<string, object>
                {
                    {"NetbiosName", Workstation},
                    {"MACAddress", getMACAddress},
                    {"OverwriteExistingRecord", false}
                };

                var outParams = connect.ExecuteMethod("SMS_Site", "ImportMachineEntry", inParams);

                return outParams["ReturnValue"].StringValue == "0" ? outParams["ResourceID"].StringValue : "";
            }
        }

        /// <summary>
        /// Imaging purposes, returns the Status value
        /// </summary>
        /// <param name="getBaseImageID">CollectionID</param>
        /// <param name="getWorkstation">Device Name</param>
        /// <param name="getServer">SCCM Primary Server</param>
        /// <param name="StartTime">Time Imaging began</param>
        /// <returns>Returns status string</returns>
        public static string getStatus(string getBaseImageID, string getWorkstation, string getServer,
            DateTime StartTime)
        {
            var query = "select * from SMS_ClassicDeploymentAssetDetails WHERE DeviceName ='" + getWorkstation +
                        "' AND CollectionID='" + getBaseImageID + "'";

            var result = string.Empty;

            using (var connection = InternalFunctions.Connect(getServer))
            {
                foreach (IResultObject getobject in connection.QueryProcessor.ExecuteQuery(query))
                {
                    var SummarizationTime = InternalFunctions.TimeConverter(getobject["SummarizationTime"].StringValue);

                    if (StartTime < SummarizationTime)
                    {
                        //return "(" + getobject["MessageID"].StringValue + ") " + getMessage(getobject["MessageID"].StringValue);
                        return "Status: Active/Running (1%)";
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Displays all devices listed in a Collection
        /// </summary>
        /// <param name="iD">Collection ID</param>
        /// <param name="getServer">SCCM Primary Server Name</param>
        /// <returns>List of devices</returns>
        public static List<CollectionBucket> GatherItems(string iD, string getServer)
        {
            var results = new List<CollectionBucket>();

            using (var connect = InternalFunctions.Connect(getServer))
            {


                string search = "SELECT Name FROM SMS_FullCollectionMembership WHERE CollectionID='" + iD + "'";

                foreach (IResultObject item in connect.QueryProcessor.ExecuteQuery(search))
                {
                    var result = new CollectionBucket { PCName = item["Name"].StringValue };

                    results.Add(result);
                }

            }

            return results;
        }

        /// <summary>
        /// Removed a QueryBasemembership from a collection.
        /// </summary>
        /// <param name="PrimaryServer"></param>
        /// <param name="coll">Requires the QueryID</param>
        /// <returns></returns>
        public static Collection RemoveQueryBaseMembership(string PrimaryServer, Collection coll)
        {
            using (var connection = InternalFunctions.Connect(PrimaryServer))
            {
                var collection = connection.GetInstance(@"SMS_Collection.CollectionID='" + coll.CollectionID + "'");
                try
                {
                    collection.Get();
                }
                catch
                {
                    var ex = new Exception("Invalid CollectionID.  Please validate and try again!");
                    throw ex;
                }

                foreach (var membership in coll.QueryBaseMemberships)
                {
                    var newQueryRule = connection.CreateInstance("SMS_CollectionRuleQuery");
                    newQueryRule["QueryID"].IntegerValue = Convert.ToInt32(membership.ID);

                    // Add the rule. Although not used in this sample, QueryID contains the query identifier.                           

                    var addMembershipRuleParameters = new Dictionary<string, object>
                    {
                        {"collectionRule", newQueryRule}
                    };

                    var staticID = collection.ExecuteMethod("DeleteMembershipRule", addMembershipRuleParameters);

                    membership.Status = staticID["ReturnValue"].StringValue == "0" ? "REMOVED" : "FAILED";
                }
            }

            return coll;
        }
        /// <summary>
        /// Removes a Device from a Collection
        /// </summary>
        /// <param name="ResourceID">ResourceID of the Device you want to remove</param>
        /// <param name="CollectionID">Collection ID from which you want to remove the device</param>
        /// <param name="WorkstationName">Device Name</param>
        /// <param name="getServer">Primary SCCM Server Name</param>
        /// 
        public static void RemoveResourceCollection(int ResourceID, string CollectionID, string WorkstationName, string getServer)
        {
            using (var connection = InternalFunctions.Connect(getServer))
            {
                if (!DoesResExists(connection, ResourceID, CollectionID))
                {
                    return;
                }

                var collection = connection.GetInstance(@"SMS_Collection.CollectionID='" + CollectionID + "'");
                var collectionRule = connection.CreateEmbeddedObjectInstance("SMS_CollectionRuleDirect");

                collectionRule["ResourceClassName"].StringValue = "SMS_R_System";
                collectionRule["ResourceID"].IntegerValue = ResourceID;
                collectionRule["RuleName"].StringValue = WorkstationName;

                var inParams = new Dictionary<string, object> { { "collectionRule", collectionRule } };

                var staticID = collection.ExecuteMethod("DeleteMembershipRule", inParams);

                if (staticID["ReturnValue"].StringValue == "0")
                {
                    var requestRefreshParameters = new Dictionary<string, object> { { "IncludeSubCollections", false } };
                    collection.ExecuteMethod("RequestRefresh", requestRefreshParameters);
                }
                else
                {
                    var ex = new Exception("UNKNOWN Error Occurred");
                    throw ex;
                }

            }


        }

        /// <summary>
        /// Confirms if Device exists in SCCM 
        /// </summary>
        /// <param name="connect">WMI Connection to SCCM Primary Server</param>
        /// <param name="ResourceID">Resource ID of device in question</param>
        /// <param name="CollectionID">Collection ID to validate if resource is listed</param>
        /// <returns>Boolean of request</returns>
        public static bool DoesResExists(WqlConnectionManager connect, int ResourceID, string CollectionID)
        {
            var Query = "SELECT ResourceID FROM SMS_CM_Res_Coll_" + CollectionID;
            var ListofResources = connect.QueryProcessor.ExecuteQuery(Query);

            try
            {
                foreach (IResultObject resource in ListofResources)
                {
                    if (resource["ResourceID"].StringValue == ResourceID.ToString())
                    {
                        return true;
                    }
                }
            }
            catch
            {
                //Ignore Error
            }

            return false;
        }

    }
}
