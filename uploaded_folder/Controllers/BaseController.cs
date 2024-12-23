using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using COR.Profile.Api.DataHandler;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Newtonsoft.Json.Linq;

namespace COR.Profile.Api.Controllers
{
    [Route("[controller]")]
    [ApiController]
    [EnableRateLimiting("fixed")]
    public class BaseController : ControllerBase
    {
        public static readonly string _sessionXlFileName = DateTime.Now.ToString("yyyyMMddHHmmss") + ".xlsx";
        public static readonly string path = @"DownloadFiles\" + _sessionXlFileName;
        public static readonly string path1 = @"DownloadFiles\";

        protected readonly FeatureFlagService _featureFlagService;

        public BaseController(FeatureFlagService featureFlagService)
        {
            _featureFlagService = featureFlagService;
        }
        [NonAction]
        public string GetUser()
        {
            try
            {
                var appMetadataClaim = User?.Claims?.FirstOrDefault(s => s.Type == "https://republicservices.com/app_metadata");

                if (appMetadataClaim != null)
                {
                    var appMetadataValue = appMetadataClaim.Value;
                    var appClaim = new Claim("app_metadata", appMetadataValue);
                    var identity = (ClaimsIdentity)User.Identity;
                    identity.RemoveClaim(appMetadataClaim);
                    identity.AddClaim(appClaim);
                }
                var userClaim = User?.Claims?.FirstOrDefault(s => s.Type == "app_metadata")?.Value;
                if (userClaim != null)
                {
                    JObject userClaimJson = JObject.Parse(userClaim);
                    string corUserId = (string)userClaimJson["cor_user_id"];

                    var featureFlags = _featureFlagService.GetFeatureFlagsAsync().Result;
                    bool isFeatureFlagOn = featureFlags["ff-cor2-universal"];
                    if (isFeatureFlagOn)
                    {
                        var impersonateClaim = User?.Claims?.FirstOrDefault(s => s.Type == "act_as")?.Value;
                        if (string.IsNullOrEmpty(impersonateClaim))
                        {
                            return string.IsNullOrWhiteSpace(corUserId) ? string.Empty : Convert.ToString(corUserId);
                        }
                        else
                        {
                            JObject tokenObject = JObject.Parse(impersonateClaim);
                            string impersonatedCorUserId = (string)tokenObject["app_metadata"]["cor_user_id"];
                            return impersonatedCorUserId;
                        }

                    }
                    else
                    {

                        return string.IsNullOrWhiteSpace(corUserId) ? string.Empty : Convert.ToString(corUserId);
                    }
                }
                else
                {
                    return string.Empty;
                }

            }
            catch
            {
                throw new Exception();
            }
        }


        public string GetAuraActualUser()
        {
            try
            {
                var appMetadataClaim = User?.Claims?.FirstOrDefault(s => s.Type == "https://republicservices.com/app_metadata");

                if (appMetadataClaim != null)
                {
                    var appMetadataValue = appMetadataClaim.Value;
                    var appClaim = new Claim("app_metadata", appMetadataValue);
                    var identity = (ClaimsIdentity)User.Identity;
                    identity.RemoveClaim(appMetadataClaim);
                    identity.AddClaim(appClaim);
                }
                var userClaim = User?.Claims?.FirstOrDefault(s => s.Type == "app_metadata")?.Value;
                if (!string.IsNullOrEmpty(userClaim))
                {
                    JObject userClaimJson = JObject.Parse(userClaim);
                    string corUserId = (string)userClaimJson["cor_user_id"];
                    return string.IsNullOrWhiteSpace(corUserId) ? string.Empty : Convert.ToString(corUserId);
                }
                else
                {
                    return string.Empty;
                }


            }
            catch
            {
                throw new Exception();
            }
        }

        #region Common method

        [NonAction]
        public void DeleteFile()
        {
            if (Directory.Exists(path1))
            {
                string[] files = Directory.GetFiles(path1);
                foreach (string file in files)
                {
                    DateTime modification = System.IO.File.GetCreationTime(file);
                    int _diffDays = Convert.ToInt32((DateTime.Now - modification).TotalDays);
                    if (_diffDays > 0)
                    {
                        FileInfo fi = new (file);
                        fi.Delete();
                    }
                }
            }
            else
                Directory.CreateDirectory(path1);
        }

        [NonAction]
        public static List<T> ConvertDataTable<T>(DataTable dt)
        {
            List<T> data = new ();

            foreach (DataRow row in dt.Rows)
            {
                T item = GetItem<T>(row);
                data.Add(item);


            }
            return data;
        }

        private static T GetItem<T>(DataRow dr)
        {
            Type temp = typeof(T);
            T obj = Activator.CreateInstance<T>();

            foreach (DataColumn column in dr.Table.Columns)
            {
                foreach (PropertyInfo pro in temp.GetProperties())
                {
                    if (pro.Name == column.ColumnName)
                    {
                        pro.SetValue(obj, dr[column.ColumnName] ?? string.Empty, null);
                    }
                    else
                    {
                        continue;
                    }
                }
            }
            return obj;
        }

        [NonAction]
        public string IsCheckNullOrEmpty(string checkValue)
        {
            checkValue = String.IsNullOrEmpty(checkValue) ? "" : checkValue;
            return checkValue;
        }
        [NonAction]
        public string EmptyStringForNullValue(string _value)
        {
            return string.IsNullOrWhiteSpace(_value) ? "" : _value;
        }

        #endregion
    }
}