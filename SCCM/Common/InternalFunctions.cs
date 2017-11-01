using Microsoft.ConfigurationManagement.ManagementProvider;
using Microsoft.ConfigurationManagement.ManagementProvider.WqlQueryEngine;
using SCCM.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SCCM.Common
{
    internal class InternalFunctions
    {
        internal static WqlConnectionManager Connect(string getServer)
        {
            try
            {
                var namedValues = new SmsNamedValuesDictionary();
                var connection = new WqlConnectionManager(namedValues);

                connection.Connect(getServer);

                return connection;
            }
            catch (SmsException e)
            {
                throw e;
            }
            catch (UnauthorizedAccessException e)
            {
                throw e;
            }
        }

        internal class ProfileInfoComparer : IComparer<Profiles>
        {
            #region IComparer<CMProfiles> Members

            public int Compare(Profiles x, Profiles y)
            {
                return x.Name.CompareTo(y.Name);
            }

            #endregion
        }

        internal static bool SendRefresh(IResultObject GETcollection)
        {
            var requestRefreshParameters = new Dictionary<string, object> { { "IncludeSubCollections", false } };
            var staticID = GETcollection.ExecuteMethod("RequestRefresh", requestRefreshParameters);

            return staticID["ReturnValue"].StringValue == "0";
        }

        internal static string getMessage(string MessageID)
        {
            return AdvertStatus.Message.GetMessage(MessageID);
        }

        internal static DateTime TimeConverter(string SummarizationTime)
        {
            return System.Management.ManagementDateTimeConverter.ToDateTime(SummarizationTime);
        }

    }
}
