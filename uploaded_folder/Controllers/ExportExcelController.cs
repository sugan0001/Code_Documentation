using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using COR.Profile.Api.Models;
using COR.Profile.Api.Repositories;
using COR.Profile.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Ocelot.DownstreamRouteFinder.UrlMatcher;
using Newtonsoft.Json.Linq;
using Microsoft.AspNetCore.Http;
using COR.Profile.Api.DataHandler;

namespace COR.Profile.Api.Controllers
{
    [Route("[controller]")]
    [ApiController]
    [EnableRateLimiting("fixed")]
    [Authorize]
    public class ExportExcelController : BaseController
    {
        private IProfileDataRepository<ProfileModel, long> _profileRepository { get; set; }


        private IConfiguration _configuration { get; set; }
        private string Proxyurl = string.Empty;

        public ExportExcelController(IProfileDataRepository<ProfileModel, long> _profileRepositoryObject, IConfiguration Configuration, FeatureFlagService featureFlagService) : base(featureFlagService)
        {
            _profileRepository = _profileRepositoryObject;
            _configuration = Configuration;
            Proxyurl = _configuration["ProxyURL"];
        }

        [HttpPost]
        [Route("ExportPendingProfile")]
        public async Task<IActionResult> ExportPendingProfile([FromBody] FormWCRInputModel Params)
        {
            Params.UserName = GetUser();
            Params.perpage = int.MaxValue;
            byte[] fileBytes;
            string urlpath = string.Empty;

            using (WebClient webclient = new())
            {
                /*webclient.UseDefaultCredentials = true;
                webclient.Credentials = new NetworkCredential(_configuration["ReportServerConfig:UserName"], Constants.Helper.EncryptionHelper.DecryptString(_configuration["ReportServerConfig:Password"]));  */
                /*Stream stream = await webclient.OpenReadTaskAsync(new Uri(BuildPendingReportUri(Params)));*/
                var geturl = BuildPendingReportUri(Params);

                using (HttpClient client = new HttpClient())
                {
                    string bearer_token = Request.Headers["Authorization"];
                    client.DefaultRequestHeaders.Add("Authorization", bearer_token);
                    var response = client.GetAsync(Proxyurl + "Report/ViewReportExcel" + "?url=" + Uri.EscapeDataString(geturl)).Result;
                    if (response.IsSuccessStatusCode)
                    {
                        var result = response.Content.ReadAsStringAsync().Result;
                        JObject jsonObject = JObject.Parse(result);
                        urlpath = (string)jsonObject["value"];

                    }
                }

                /* stream.CopyTo(streamReader);
                 fileBytes = streamReader.ToArray();*/
            }
            return StatusCode(StatusCodes.Status200OK, urlpath);
        }

        [NonAction]
        private string BuildPendingReportUri(FormWCRInputModel Params)
        {
            Params.search = EmptyStringForNullValue(Params.search);
            Params.Generator_Name = EmptyStringForNullValue(Params.Generator_Name);
            Params.Waste_Common_Name = EmptyStringForNullValue(Params.Waste_Common_Name);
            Params.Generator_Size = EmptyStringForNullValue(Params.Generator_Size);
            Params.Epa_Waste_Code = EmptyStringForNullValue(Params.Epa_Waste_Code);
            Params.Adv_Search= EmptyStringForNullValue(Params.Adv_Search);
            Params.CopyStatus = EmptyStringForNullValue(Params.CopyStatus);
            Params.customer_id_list = EmptyStringForNullValue(Params.customer_id_list);
            Params.generator_id_list = EmptyStringForNullValue(Params.generator_id_list);
            Params.Form_Id = EmptyStringForNullValue(Params.Form_Id);
            Params.period = EmptyStringForNullValue(Params.period);
            string uri = string.Format("/COR2.0_ExcelReport/PendingProfile&"+ "web_userid={0}&generator_name={1}&status_list={2}&form_id={3}&generator_id_list={4}&waste_common_name={5}&epa_waste_code={6}&generator_size={7}&copy_status={8}&owner={9}&generator_site_type={10}&sort={11}&page=1&perpage={13}&excel_output=1&customer_id_list={15}"+ "&period={16}&search={17}&adv_search={18}&tsdf_type={19}&haz_filter={20}"
                , Params.UserName,
                Params.Generator_Name,
                Params.profileStatus,
                Params.Form_Id,
                Params.generator_id_list,
                Params.Waste_Common_Name,
                Params.Epa_Waste_Code,
                Params.Generator_Size,
                Params.CopyStatus,
                Params.owner,
                Params.generator_site_type,
                Params.sortby,
                Params.page,
                int.MaxValue,
                Params.excel_output,
                Params.customer_id_list,
                Params.period,
                Params.search,
                Params.Adv_Search,
                Params.tsdf_type,
                Params.haz_filter);

            return uri;
        }



        [HttpPost]
        [Route("ExportApporvedProfile")]
        public async Task<IActionResult> ExportApporvedProfile([FromBody]ProfileListParams Param)
        {

            Param.UserName = GetUser();
            Param.PerPage = int.MaxValue;
            byte[] fileBytes;
            string urlpath = string.Empty;

            using (WebClient webclient = new())
            {
                /*  webclient.UseDefaultCredentials = true;
                  webclient.Credentials = new NetworkCredential(_configuration["ReportServerConfig:UserName"], Constants.Helper.EncryptionHelper.DecryptString(_configuration["ReportServerConfig:Password"]));                */
                /* Stream stream = await webclient.OpenReadTaskAsync(new Uri(BuildReportUri(Param, "ExportApprovedProfile")));*/
                /* using var streamReader = new MemoryStream();*/

                var geturl = BuildReportUri(Param, "ExportApprovedProfile");

                using (HttpClient client = new HttpClient())
                {
                    string bearer_token = Request.Headers["Authorization"];
                    client.DefaultRequestHeaders.Add("Authorization", bearer_token);
                    var response = client.GetAsync(Proxyurl + "Report/ViewReportExcel" + "?url=" + Uri.EscapeDataString(geturl)).Result;
                    if (response.IsSuccessStatusCode)
                    {
                        var result = response.Content.ReadAsStringAsync().Result;
                        JObject jsonObject = JObject.Parse(result);
                        urlpath = (string)jsonObject["value"];

                    }
                }

                /* stream.CopyTo(streamReader);
                 fileBytes = streamReader.ToArray();*/
            }
            return StatusCode(StatusCodes.Status200OK, urlpath);
        }

        [HttpPost]
        [Route("ExportExpiredProfile")]
        public async Task<IActionResult> ExportExpiredProfile([FromBody]ProfileListParams Param)
        {
            Param.UserName = GetUser();
            Param.PerPage = int.MaxValue;
            byte[] fileBytes;
            string urlpath = string.Empty;
            /*  using (WebClient webclient = new())
              {
                  webclient.UseDefaultCredentials = true;
                  webclient.Credentials = new NetworkCredential(_configuration["ReportServerConfig:UserName"], Constants.Helper.EncryptionHelper.DecryptString(_configuration["ReportServerConfig:Password"]));
                  Stream stream = await webclient.OpenReadTaskAsync(new Uri(BuildExpiredReportUri(Param)));
                  using var streamReader = new MemoryStream();
                  stream.CopyTo(streamReader);
                  fileBytes = streamReader.ToArray();
              }*/

            var geturl = BuildExpiredReportUri(Param);

            using (HttpClient client = new HttpClient())
            {
                string bearer_token = Request.Headers["Authorization"];
                client.DefaultRequestHeaders.Add("Authorization", bearer_token);
                var response = client.GetAsync(Proxyurl + "Report/ViewReportExcel" + "?url=" + Uri.EscapeDataString(geturl)).Result;
                if (response.IsSuccessStatusCode)
                {
                    var result = response.Content.ReadAsStringAsync().Result;
                    JObject jsonObject = JObject.Parse(result);
                    urlpath = (string)jsonObject["value"];

                }
            }

            return StatusCode(StatusCodes.Status200OK, urlpath);
        }

        [NonAction]
        private string BuildExpiredReportUri(ProfileListParams param)
        {
            return BuildReportUri(param, "ExportExpiredProfile");
        }


        [NonAction]
        private string BuildReportUri(ProfileListParams param, string report)
        {
            param.Search = EmptyStringForNullValue(param.Search);
            param.GeneratorName = EmptyStringForNullValue(param.GeneratorName);
            param.WasteCommonName = EmptyStringForNullValue(param.WasteCommonName);
            param.GeneratorSize = EmptyStringForNullValue(param.GeneratorSize);
            param.EpaWasteCode = EmptyStringForNullValue(param.EpaWasteCode);
            param.AdvSearch = EmptyStringForNullValue(param.AdvSearch);
            param.CopyStatus = EmptyStringForNullValue(param.CopyStatus);
            param.customer_id_list = EmptyStringForNullValue(param.customer_id_list);
            param.generator_id_list = EmptyStringForNullValue(param.generator_id_list);
            param.facility_search = EmptyStringForNullValue(param.facility_search);
            param.facility_id_list = EmptyStringForNullValue(param.facility_id_list);
            param.period = EmptyStringForNullValue(param.period);
            param.ApprovalCode = EmptyStringForNullValue(param.ApprovalCode);
            param.under_review = EmptyStringForNullValue(param.under_review) == "" ? "A" : param.under_review;
            string uri = string.Format("/COR2.0_ExcelReport/" + report + "&" +
                "web_userid={0}" +
                "&status_list={1}" +
                "&search={2}" +
                "&adv_search={3}" +
                "&generator_size={4}" +
                "&generator_name={5}" +
                "&generator_site_type={6}"+
                "&profile_id={7}" +
                "&approval_code={8}"+
                "&waste_common_name={9}" +
                "&epa_waste_code={10}" +
                "&facility_search={11}" +
                "&facility_id_list={12}" +
                "&copy_status={13}" +
                "&sort={14}" +
                "&page={15}" +
                "&perpage={16}" +
                "&excel_output={17}" +
                "&customer_id_list={18}" +
                "&generator_id_list={19}" +
                "&owner={20}" +
                "&period={21}&tsdf_type={22}&haz_filter={23}&under_review={24}"
                , param.UserName, param.ProfileStatus, param.Search, param.AdvSearch,
                    param.GeneratorSize, param.GeneratorName, param.generator_site_type,
                    param.ProfileId,param.ApprovalCode, param.WasteCommonName, param.EpaWasteCode,
                 param.facility_search, param.facility_id_list, param.CopyStatus,
                 param.SortBy, param.Page, int.MaxValue, 1,
                 param.customer_id_list, param.generator_id_list,
                 param.owner, param.period, param.tsdf_type,
                param.haz_filter,param.under_review);
            return uri;
        }

    }
}