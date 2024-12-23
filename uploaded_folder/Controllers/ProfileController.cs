using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Threading.Tasks;
using COR.Profile.Api.CustomAttribute;
using COR.Profile.Api.DataHandler;
using COR.Profile.Api.Models;
using COR.Profile.Api.Repositories;
using COR.Profile.Constants;
using COR.Profile.DataModel.DataAccess;
using COR.Profile.DataModel.DataModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


namespace COR.Profile.Api.Controllers
{

    [Route("[controller]")]
    [ApiController]
    [EnableRateLimiting("fixed")]
    [Authorize]

    public class ProfileController : BaseController
    {
        private string plt_ai_ConnectionString { get; set; }
        private IProfileDataRepository<ProfileModel, long> _profileRepository { get; set; }
        private IConfiguration _configuration { get; set; }
        private ProfileDataManager dataManager { get; set; }
        public string UtilityUrl { get; set; }
        public string Gateway { get; set; }
        string web_userid = string.Empty;
        private readonly string filename = DateTime.Now.ToString("yyyyMMddHHmmss") + ".pdf";
        private IHostingEnvironment _hostingEnvironment { get; set; }
        private ProfileDBContext profileEntities { get; set; }

        public ProfileController(IProfileDataRepository<ProfileModel, long> _profileRepositoryObject,
                IConfiguration Configuration,
                IHostingEnvironment hostingEnvironment,FeatureFlagService featureFlagService) : base(featureFlagService)
        {
            profileEntities = new ProfileDBContext(Configuration);
            _hostingEnvironment = hostingEnvironment;
            _profileRepository = _profileRepositoryObject;
            _configuration = Configuration;
            plt_ai_ConnectionString = _configuration["Plt_ai_ConnectionString"];
            dataManager = new ProfileDataManager(_configuration, hostingEnvironment);
            UtilityUrl = _configuration["UtilityUrl"];
            Gateway = _configuration["Gateway"];
        }


        [HttpPost]
        [Route("AddProfile")]
        public IActionResult AddProfile([FromBody] ProfileModel profile)
        {
            string userid = GetUser();
            try
            {

                FormWcr formwcr = this.profileEntities.FormWcr.FirstOrDefault(c => c.FormId == profile.formid && c.RevisionId == profile.revisionId);
                if (formwcr != null && !IsFormHasEditAccess(Convert.ToInt32(formwcr.DisplayStatusUid)))
                {
                    return StatusCode(StatusCodes.Status204NoContent, "No changes to update");
                }

                updatedByDataChange(ref profile);
                ProfileResponseModel profileResponseModel;
                _profileRepository.SaveProfile(profile, out profileResponseModel, profile.formid, profile.revisionId);
                DataTable dtr = profileResponseModel?.dataTable?.Rows?.Count > 0 ? profileResponseModel?.dataTable?.Rows?[0].Table : null;
                if (dtr != null && dtr.Rows.Count > 0 && dtr.Rows[0].ItemArray[0].ToString() == ProfileMessages.Success_Msg)
                {
                    if (profile.IsIdleTimeOutExpired)
                    {
                        profile.EQAIFormUrl = GetEQAIFormWCREncryptedUrl(form_id: Convert.ToString(profile.formid), revision_id: Convert.ToString(profile.revisionId), web_userid: userid);
                        SendPDFModel sendPDFModel = _profileRepository.NofityForUnsavedData(profile);
                        TrackMessage<SendPDFModel>(sendPDFModel);
                    }
                    return StatusCode(StatusCodes.Status200OK, dtr.Rows[0].ItemArray[0].ToString());
                }
                else
                {
                    if (dtr != null && dtr.Rows.Count > 0)
                    {
                        return StatusCode(StatusCodes.Status400BadRequest, dtr.Rows[0].ItemArray[0].ToString());
                    }
                    return StatusCode(StatusCodes.Status400BadRequest, "Error in saving profile");
                }
            }
            catch (Exception ex)
            {
                return  StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        private bool IsFormHasEditAccess(int display_status_uid)
        {
            try
            {
                return this.profileEntities.FormDisplayStatus.Any(fd =>
                                   (fd.DisplayStatus == "Ready For Submission" && fd.DisplayStatusUid == display_status_uid)
                                    || (fd.DisplayStatus == "Draft" && fd.DisplayStatusUid == display_status_uid)
                                    || (fd.DisplayStatus == "Pending Customer Response" && fd.DisplayStatusUid == display_status_uid));
            }
            catch (Exception ex)
            {
                throw ex;
            }

        }

        [HttpPost]
        [Route("SubmitProfile")]
        public object SubmitProfile([FromBody] ProfileModel Model)
        {
            try
            {
                string userName = GetUser();
                var response = _profileRepository.SubmitProfile(Model.formid, Model.revisionId, userName);
                string value = Request.Headers["Authorization"];

                Task.Run(() =>
                {
                    using (HttpClient client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Add("Authorization", value);
                        var routing_response = client.GetAsync(Gateway + "Signature/FacilityRoutingNotification?form_id=" + Model.formid + "&revision_id=" + Model.revisionId).Result;
                        if (!routing_response.IsSuccessStatusCode)
                        {
                            Console.WriteLine(routing_response.Content.ReadAsStringAsync().Result);
                        }
                    }
                });
                return response;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }


        [HttpGet]
        [Route("CreateProfile")]
        public object CreateProfile()
        {
            try
            {
                string userName = GetUser();
                return _profileRepository.CreateProfile(userName);
            }
            catch (Exception ex)
            {
                throw ex;
            }

        }

        [HttpGet]
        [Route("GetProfileSection")]
        [DisableRateLimiting]
        public object GetProfileSection(int formId, int revisionId, string section)
        {
            try
            {
                return _profileRepository.GetProfileSection(formId, revisionId, section);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }


        [HttpPost]
        [Route("ProfileList")]
        [DisableRateLimiting]
        public object ProfileList([FromBody] FormWCRInputModel Param)
        {
            try
            {
                Param.UserName = GetUser();
                return _profileRepository.GetFormWCRList(Param);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        [HttpPost]
        [Route("TemplateList")]
        public object TemplateList([FromBody] ProfileListParams Param)
        {
            try
            {
                Param.UserName = GetUser();
                return _profileRepository.GetTemplateList(Param);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        [HttpPost]
        [Route("ProfileFormWcrDetails")]
        public object ProfileFormWcrDetails([FromBody] FormWcrParams Param)
        {
            try
            {
                Param.UserName = GetUser();
                return _profileRepository.ProfileFormWcrDetails(Param);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpPost]
        [Route("ProfileApprovedDetails")]
        public object ProfileApprovedDetails(ProfileList Model)
        {
            try
            {
                Model.UserName = GetUser();
                return _profileRepository.GetApprovedDetails(Model);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpGet]
        [Route("ProfileApprovedDetails")]
        public object ProfileApprovedDetails(int profile_id)
        {
            try
            {
                ProfileList Model = new ProfileList();
                Model.UserName = GetUser();
                Model.profile_id = profile_id;
                return _profileRepository.GetApprovedDetails(Model);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpGet]
        [Route("TSDFProfileApprovedDetails")]
        public object TSDFProfileApprovedDetails(int profile_id)
        {
            try
            {
                ProfileList Model = new ProfileList();
                Model.UserName = GetUser();
                Model.profile_id = profile_id;
                return _profileRepository.GetTSDFApprovedDetails(Model);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpGet]
        [Route("ProfileExpiredDetails")]
        public object ProfileExpiredDetails(int profile_id)
        {
            try
            {
                ProfileList Model = new ProfileList();
                Model.UserName = GetUser();
                Model.profile_id = profile_id;
                return _profileRepository.GetApprovedDetails(Model);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpGet]
        [Route("ProfileContactLink")]
        public object ProfileContactLink()
        {
            try
            {
                string UserName = GetUser();
                var response = _profileRepository.GetCustomerGeneratorSiteType(UserName);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }

        }

        [HttpGet]
        [Route("CustomerGeneratorSiteType")]
        public object CustomerGeneratorSiteType()
        {
            try
            {
                string UserName = GetUser();
                return _profileRepository.GetCustomerGeneratorSiteType(UserName);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpPost]
        [Route("GetProfileList")]
        [DisableRateLimiting]
        public object GetProfileList([FromBody] ProfileListParams Param)
        {
            try
            {
                Param.UserName = GetUser();
                return _profileRepository.GetProfileList(Param);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpPost]
        [Route("ProfileApporvedList")]
        public object ProfileApporvedList([FromBody] ProfileListParams Param)
        {
            try
            {
                Param.UserName = GetUser();
                return _profileRepository.GetApprovedList(Param);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }

        }


        [HttpPost]
        [Route("ProfileExpiredList")]
        public object ProfileExpiredList([FromBody] ProfileListParams Param)
        {
            try
            {
                Param.UserName = GetUser();
                return _profileRepository.GetExpiredList(Param);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }

        }

        [HttpPost]
        [Route("GetProfileCount")]
        public object GetPendingProfileCount(FormWCRInputModel formWCRCountModel)
        {
            try
            {
                string UserName = GetUser();
                formWCRCountModel.UserName = UserName;
                return _profileRepository.GetProfileCount(formWCRCountModel);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpPost]
        [Route("GetApprovedExpiredCount")]
        [DisableRateLimiting]
        public object GetApprovedExpiredCount(ProfileListParams approvedParam)
        {
            try
            {
                string UserName = GetUser();
                approvedParam.UserName = UserName;
                return _profileRepository.GetApprovedCount(approvedParam);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }

        }

        [HttpPost]
        [Route("GetApprovedProfileCount")]
        [DisableRateLimiting]
        public object GetApprovedProfileCount(ProfileListParams approvedParam)
        {
            return _profileRepository.GetApprovedCount(approvedParam);
        }

        [HttpPost]
        [Route("GetExpiredProfileCount")]
        [DisableRateLimiting]
        public object GetExpiredProfileCount(ProfileListParams expiredParam)
        {
            return _profileRepository.GetExpiredCount(expiredParam);
        }

        [HttpGet("GetApprovedProfileSelection")]
        public object GetApprovedProfileSelection(int profileId, string section, string TSDFType)
        {
            object returnXMLvalue = _profileRepository.GetApprovedProfileSelection(profileId, section, TSDFType);
            return returnXMLvalue;
        }

        [HttpGet("GetApprovedProfileSupplementarySelection")]
        public object GetApprovedProfileSupplementarySelection(int profileId)
        {
            object returnXMLvalue = _profileRepository.GetApprovedProfileSupplementarySelection(profileId);
            return returnXMLvalue;
        }

        [HttpGet]
        [Route("GetCorUserProfileApprovalCode")]

        public object GetCorUserProfileApprovalCode(string approval_code, int profile_id, string form_id, string revision_id)
        {
            try
            {
                return _profileRepository.GetCorUserProfileApprovalCode(approval_code, profile_id, form_id, revision_id);
            }
            catch (Exception exception)
            {
                throw new Exception(exception.Message);
            }
        }

        [HttpGet]
        [Route("GetProfileRenewelStatus")]
        public object GetProfileRenewelStatus(int profile_id)
        {
            try
            {
                string userId = GetUser();
                if (!string.IsNullOrWhiteSpace(userId))
                {
                    return _profileRepository.GetProfileRenewelStatus(profile_id);
                }
                return null;
            }
            catch (Exception exception)
            {
                throw new Exception(exception.Message);
            }
        }

        [HttpPost]
        [Route("GetBulkRenewelProfileList")]
        public object GetBulkRenewelProfileList([FromBody] BulkRenewelProfileListParams Param)
        {
            try
            {
                Param.webuser_id = GetUser();
                return _profileRepository.GetBulkRenewelProfileList(Param);
            }
            catch (Exception exception)
            {
                throw new Exception(exception.Message);
            }
        }

        #region Copy Profile

        [HttpGet("CopyProfile")]
        public object CopyProfile(long form_id, long revision_id, int profile_id, string copysource)
        {
            try
            {
                var featureFlags = _featureFlagService.GetFeatureFlagsAsync().Result;
                bool isFeatureFlagOn = featureFlags["ff-cor2-universal"];
                string web_user_id = isFeatureFlagOn ? GetAuraActualUser() : GetUser();
                string modified_by_web_user_id = isFeatureFlagOn ? GetUser() : impersonateDetail(web_user_id);
                return _profileRepository.CopyProfile(form_id, revision_id, copysource, web_user_id, profile_id, modified_by_web_user_id);
            }
            catch (Exception exception)
            {
                throw exception.InnerException;
            }
        }

        #endregion

        #region Copy Profile Form

        [HttpGet("CopyProfileForm")]
        public object CopyProfileForm(long form_id, long revision_id, int profile_id, string copysource)
        {
            try
            {
                var featureFlags = _featureFlagService.GetFeatureFlagsAsync().Result;
                bool isFeatureFlagOn = featureFlags["ff-cor2-universal"];
                string web_user_id = GetUser();
                string modified_by_web_user_id = isFeatureFlagOn ? GetUser() : impersonateDetail(web_user_id);
                return _profileRepository.CopyProfileForm(form_id, revision_id, copysource, web_user_id, profile_id, modified_by_web_user_id);
            }
            catch (Exception exception)
            {
                throw exception.InnerException;
            }
        }

        #endregion

        #region Copy Approved Profile

        [HttpGet("CopyApprovedProfile")]
        public string CopyApprovedProfile(int profile_id, string copysource)
        {
            try
            {
                var featureFlags = _featureFlagService.GetFeatureFlagsAsync().Result;
                bool isFeatureFlagOn = featureFlags["ff-cor2-universal"];
                string web_user_id = GetUser();
                string modified_by_web_user_id = isFeatureFlagOn ? GetUser() : impersonateDetail(web_user_id);
                return _profileRepository.CopyApprovedProfile(profile_id, copysource, web_user_id, modified_by_web_user_id);
            }
            catch (Exception exception)
            {
                throw exception.InnerException;
            }
        }

        #endregion

        #region Profile Copy Source
        [HttpGet("ProfileCopySource")]
        public string ProfileCopySource(long form_id, long revision_id, long profileid, string copysource)
        {
            try
            {
                var featureFlags = _featureFlagService.GetFeatureFlagsAsync().Result;
                bool isFeatureFlagOn = featureFlags["ff-cor2-universal"];
                string web_user_id = GetUser();
                string modified_by_web_user_id = isFeatureFlagOn ? GetUser() : impersonateDetail(web_user_id);
                return _profileRepository.ProfileCopySource(form_id, revision_id, profileid, copysource, web_user_id, modified_by_web_user_id);
            }
            catch (Exception exception)
            {
                throw exception.InnerException;
            }
        }
        #endregion

        #region Send PDF

        [HttpPost]
        [Route("SendPdf")]
        public async Task<IActionResult> SendPdf(SendPDFModel model)
        {
            try
            {
                web_userid = GetUser();
                string file_loc = null;
                string extn = ".pdf";
                if (string.IsNullOrEmpty(model.file_name))
                {
                    file_loc = path1 + filename;
                }
                else
                {
                    file_loc = path1 + model.file_name + extn;
                }

                Stream stream;
                string docUrl = _configuration["ReportServerConfig:ReportServerUrl"] + model.profilePDFURL + UrlConfig.pdfEndUrl;

                using (WebClient webclient = new WebClient())
                {
                    webclient.UseDefaultCredentials = true;
                    webclient.Credentials = new NetworkCredential(_configuration["ReportServerConfig:UserName"], Constants.Helper.EncryptionHelper.DecryptString(_configuration["ReportServerConfig:Password"]));
                    webclient.DownloadFile(new Uri(docUrl), file_loc);
                    stream = await webclient.OpenReadTaskAsync(file_loc);
                }

                var response = await UploadDocument(stream, model.file_name + extn);
                if (response.IsSuccessStatusCode)
                {
                    string body = string.Empty;
                    var responseMailTemplate = await GetMailTemplate();
                    if (responseMailTemplate.IsSuccessStatusCode)
                    {
                        var mailTemplate = responseMailTemplate.Content.ReadAsStringAsync().Result;
                        body = mailTemplate.Replace("[[mail body]]", Convert.ToString(model.body));
                    }
                    model.body = string.IsNullOrEmpty(body) ? model.body : body;
                    var result = response.Content.ReadAsStringAsync().Result;
                    DocumentResponseModel[] responsemodel;
                    if (result != null)
                    {
                        responsemodel = JsonConvert.DeserializeObject<DocumentResponseModel[]>(result);
                        model.image_id = responsemodel?.FirstOrDefault()?.image_id;
                        model.file_name = model.file_name + extn;
                        TrackMessage<SendPDFModel>(model);
                    }
                    return StatusCode(StatusCodes.Status200OK, "Mail sent successfully");
                }

                return StatusCode(StatusCodes.Status400BadRequest, "Failure Sending Mail");

            }
            catch (Exception exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, exception.Message);
            }
        }

        #endregion

        #region --> upload RAD Document

        private async Task<HttpResponseMessage> UploadDocument(Stream stream, string fileName)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    byte[] data;
                    using (var br = new BinaryReader(stream))
                        data = br.ReadBytes((int)stream.Length);

                    ByteArrayContent bytes = new ByteArrayContent(data);

                    MultipartFormDataContent multiContent = new MultipartFormDataContent();
                    string value = Request.Headers["Authorization"];

                    client.DefaultRequestHeaders.Add("Authorization", value);

                    var values = new[]
                    {
                        new KeyValuePair<string, string>("form_type", "ATTACH"),
                        new KeyValuePair<string, string>("document_source", "COROTHER"),
                        new KeyValuePair<string, string>("document_name",fileName)
                    };

                    foreach (var keyValuePair in values)
                    {
                        multiContent.Add(new StringContent(keyValuePair.Value), keyValuePair.Key);
                    }

                    var fileContent = bytes;
                    fileContent.Headers.ContentDisposition =
                         new ContentDispositionHeaderValue("form-data") //<- 'form-data' instead of 'attachment'
                         {
                             Name = "fileKey", // <- included line...
                             FileName = fileName,
                         };
                    multiContent.Add(fileContent);

                    var response = await client.PostAsync(Gateway + UrlConfig.upload_relativeUri, multiContent);
                    return response;
                }
            }
            catch (Exception exception)
            {
                throw exception.InnerException;
            }
        }

        private async Task<HttpResponseMessage> GetMailTemplate()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    string value = Request.Headers["Authorization"];
                    client.DefaultRequestHeaders.Add("Authorization", value);
                    HttpResponseMessage responseMailTemplate;
                    responseMailTemplate = await client.GetAsync(Gateway + "Utility/GetMailTemplate");
                    return responseMailTemplate;
                }
            }
            catch (Exception exception)
            {
                throw exception.InnerException;
            }
        }

        #endregion

        #region Helper Methods

        private async void SendMail(SendPDFModel sendPDFModel)
        {
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(_configuration["UtilityUrl"]);
                client.DefaultRequestHeaders.Accept.Clear();
                HttpResponseMessage response;
                SendMailModel sendMailModel = new SendMailModel
                {
                    recipientAddresses = sendPDFModel.toAddress,
                    copyRecipients = sendPDFModel.ccAddressesList,
                    subject = sendPDFModel.subject,
                    body = sendPDFModel.body,
                    isHtml = true,
                    fileNames = sendPDFModel.filePaths,
                    senderEmail = sendPDFModel.senderEmail,
                    senderName = sendPDFModel.senderName,
                    blindCopyRecipients = sendPDFModel.bccAddressList
                };
                response = await client.PostAsJsonAsync("Mail/SendMail", sendMailModel);
                if (response.IsSuccessStatusCode)
                {
                    // Get the URI of the created resource.
                    Uri gizmoUrl = response.Headers.Location;
                }

            }
        }

        [NonAction]
        private string ImpersonatedUser()
        {
            var userId = GetUser();
            var featureFlags = _featureFlagService.GetFeatureFlagsAsync().Result;
            bool isFeatureFlagOn = featureFlags["ff-cor2-universal"];
            if (!isFeatureFlagOn)
            {
                var impersonate_detail = this.GetUserImpersonationDetatils();
                string _impersonateUser = impersonate_detail.FirstOrDefault(c => c.Type == "impersonate_user")?.Value;
                string _impersonateFlag = impersonate_detail.FirstOrDefault(c => c.Type == "impersonate_flag")?.Value;
                return _impersonateFlag == "T" ? _impersonateUser : userId;
            }
            else
            {
                return userId;
            }

        }
        private List<Claim> GetUserImpersonationDetatils()
        {
            try
            {
                return User.Claims.ToList();
            }
            catch (Exception ex)
            {
                throw ex.InnerException;
            }
        }


        private void updatedByDataChange(ref ProfileModel profile)
        {
            var featureFlags = _featureFlagService.GetFeatureFlagsAsync().Result;
            bool isFeatureFlagOn = featureFlags["ff-cor2-universal"];
            string userName = isFeatureFlagOn ? GetAuraActualUser() : GetUser();
            string _impersonateUser = isFeatureFlagOn ? GetUser() : this.ImpersonatedUser();
            string currentUser = _impersonateUser;

            profile.created_by = currentUser;

            profile.CurrentUser = userName;
            profile.SimulatedUser = _impersonateUser;

            profile.modified_by = currentUser;

            if (profile.DocumentAttachment != null && profile.DocumentAttachment.DocumentAttachment != null && profile.DocumentAttachment.DocumentAttachment.Any())
            {
                profile.DocumentAttachment.DocumentAttachment.ForEach(x => x.created_by = currentUser);
            }

            if (profile.SectionH != null && profile.SectionH.USEFacility != null && profile.SectionH.USEFacility.Any())
            {
                profile.SectionH.USEFacility.ForEach(x => x.created_by = currentUser);
                profile.SectionH.USEFacility.ForEach(x => x.modified_by = currentUser);
            }

            if (profile.SectionL != null && profile.SectionL.USEFacility != null && profile.SectionL.USEFacility.Any())
            {
                profile.SectionL.USEFacility.ForEach(x => x.created_by = currentUser);
                profile.SectionL.USEFacility.ForEach(x => x.modified_by = currentUser);
            }
        }

        #endregion

        #region --> View Profile report by formid or profileid 

        [HttpGet]
        [Route("ViewProfileReport")]
        public string ViewProfileReport(string form_id, string revision_id, string form_type = "WCR")
        {
            return _profileRepository.ViewProfileReport(form_id, revision_id, form_type);
        }

        #endregion

        #region --> Get Customer's and Generator's Contact

        [HttpGet]
        [Route("GetCustomerContacts")]
        public object GetCustomerContacts(string CustomerId)
        {
            try
            {
                string WebUserId = GetUser();
                var responseModel = _profileRepository.GetCustomerContacts(CustomerId, WebUserId);
                return Ok(responseModel);
            }
            catch (Exception exception)
            {
                return exception.InnerException;
            }
        }

        [HttpPost]
        [Route("GetGeneratorContacts")]
        public object GetGeneratorContacts(GeneratorContactModel Model)
        {
            try
            {
                string UserName = GetUser();
                var responseModel = _profileRepository.GetGeneratorContacts(Model, UserName);
                return Ok(responseModel);
            }
            catch (Exception exception)
            {
                return exception.InnerException;
            }
        }

        #endregion

        #region --> save EQAI formId, revisionId with encrypted data

        [HttpGet]
        [EndpointAccess("EqaiAccess")]
        [Route("UpdateEQAIFormWCRLink")]
        public string GetEQAIFormWCREncryptedUrl(string form_id, string revision_id, string web_userid = null)
        {
            try
            {
                string _token = dataManager.GetEQAIFormWCREncryptedUrl(form_id, revision_id, web_userid);
                if (!string.IsNullOrWhiteSpace(_token))
                {
                    return _configuration["WebUrl"] + "?formUrl=" + _token;
                }
                return "Form not linked";

            }
            catch (Exception exception)
            {
                throw exception.InnerException;
            }
        }

        [HttpPost]
        [Route("GetEQAIFormWCRLinkedUrl")]
        public object GetEQAIFormWCRDecryptedUrl(FormUlrModel formData)
        {
            try
            {
                web_userid = GetUser();
                return dataManager.GetEQAIFormWCRDecryptedUrl(formData.formUrl, web_userid);
            }
            catch (Exception exception)
            {
                throw exception.InnerException;
            }
        }

        #endregion

        #region DownloadEQAIForm

        [AllowAnonymous]
        [HttpGet]
        [Route("DownloadEQAIForm")]
        public FileResult DownloadEQAIForm(int form_id, int revision_id)
        {
            string url = string.Format(UrlConfig.FormReportRelativeUri, form_id, revision_id);
            return this.ViewReportPdf(url);
        }

        private FileResult ViewReportPdf(string url)
        {
            string _sessionPDFFileName = DateTime.Now.ToString("yyyyMMddHHmmss") + ".pdf";
            Task.Run(() => deleteFile());
            string docUrl = _configuration["ReportServerConfig:ReportServerUrl"] + url + ConstantHelper.reportEndUri;
            string fileName = "ViewReportPdf" + "_" + _sessionPDFFileName;
            string paths = @"DownloadFiles\" + fileName;
            webclient(docUrl, paths);
            byte[] fileBytes = System.IO.File.ReadAllBytes(paths);
            return File(fileBytes, "application/pdf", fileName);
        }

        private void webclient(string docUrl, string path)
        {
            try
            {
                using (WebClient webclient = new WebClient())
                {
                    webclient.UseDefaultCredentials = false;
                    webclient.Credentials = new NetworkCredential(_configuration["ReportServerConfig:UserName"], Constants.Helper.EncryptionHelper.DecryptString(_configuration["ReportServerConfig:Password"]));
                    webclient.DownloadFile(docUrl, path);
                }
            }
            catch (Exception exception)
            {
                throw exception.InnerException;
            }
        }

        private new void deleteFile()
        {
            if (Directory.Exists(@"DownloadFiles\"))
            {
                string[] files = Directory.GetFiles(@"DownloadFiles\");
                foreach (string file in files)
                {
                    DateTime modification = System.IO.File.GetCreationTime(file);
                    int _diffDays = Convert.ToInt32((DateTime.Now - modification).TotalDays);
                    if (_diffDays > 0)
                    {
                        FileInfo fi = new FileInfo(file);
                        fi.Delete();
                    }
                }
            }
            else
            {
                Directory.CreateDirectory(@"DownloadFiles\");
            }
        }

        #endregion

        #region --> Update USEFacility Information

        [Route("UpdateUSEFacility")]
        [HttpPost]
        public IActionResult UpdateUSEFacility(SectionH profile)
        {
            try
            {
                string UserName = this.ImpersonatedUser();
                profile.USEFacility.ForEach(site => { site.created_by = UserName; site.modified_by = UserName; });
                dataManager.UpdateUSEFacility(profile);
                return StatusCode(StatusCodes.Status200OK, new { Message = ProfileMessages.facilities_updated });
            }
            catch (Exception exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, exception);
            }
        }

        #endregion

        #region --> list out all the contact information against the user

        [HttpPost]
        [Route("UserContacts")]
        public JArray GetUserContacts(UserContactInputParamModel paramInstance)
        {
            try
            {
                paramInstance.web_userid = GetUser();
                return dataManager.GetUserContacts(paramInstance);
            }
            catch (Exception exception)
            {
                throw exception.InnerException;
            }
        }

        #endregion

        #region --> Prepare Message Tracker

        private void TrackMessage<T>(T model) where T : SendPDFModel
        {
            MessageTracker messageTracker = new MessageTracker()
            {
                subject = model.subject,
                body = model.body,
                date_to_send = DateTime.Now,
                created_by = web_userid,
                message_source = "COR",
                message_type_id = null,
                message_id = null,
                TO = model.toAddress,
                CC = model.ccAddressesList,
                BCC = model.bccAddressList,
                recipient_list = string.Join(",", model.toAddress),
                copy_recipient_list = string.Join(",", model.ccAddressesList),
                blindcopy_recipient_list = string.Join(",", model.bccAddressList),
                image_id = model.image_id,
                image_id_list = model.image_id_list,
                file_name = model.file_name,
                attachment_type = "pdf",
                attachment_id = null,
                source = null
            };

            using (HttpClient client = new HttpClient())
            {
                string messageRelativeUri = "MessageTracker/TrackMessage";
                string api_url = Gateway + messageRelativeUri;
                HttpResponseMessage response = client.PostAsJsonAsync(api_url, messageTracker).Result;
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine(response);
                }
            }
        }

        #endregion
        private string impersonateDetail(string userName)
        {
            var impersonate_detatail = this.GetUserImpersonationDetatils();
            string _impersonateUser = impersonate_detatail.FirstOrDefault(c => c.Type == "impersonate_user")?.Value;
            string _impersonateFlag = impersonate_detatail.FirstOrDefault(c => c.Type == "impersonate_flag")?.Value;

            var currentUser = _impersonateFlag == "T" ? _impersonateUser : userName;
            return currentUser;
        }

        #region --> Requirement 12650: COR2 Bulk Renewal Process

        [HttpPost]
        [Route("GetBulkRenewedForms")]
        public IActionResult GetBulkRenewedForms(BulkRenewalFormsInputModel BulkRenewalFormsInputModel)
        {
            try
            {
                BulkRenewalFormsInputModel.web_userid = GetUser();
                BulkRenewalFormsInputModel.impersonated_web_userid = this.ImpersonatedUser();
                var RenewedForms = this._profileRepository.GetBulkRenewedForms(BulkRenewalFormsInputModel);
                return StatusCode(StatusCodes.Status200OK, value: RenewedForms);
            }
            catch (Exception exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, exception);
            }
        }

        #endregion

        #region --> Requirement 30257: COR2: Attaching New Forms Without Amending or Renewing
        [HttpPost]
        [Route("SaveProfileDocuments")]
        public IActionResult SaveProfileDocuments([FromBody] DocumentNofication documentAttachments)
        {
            try
            {
                string currentUser = this.ImpersonatedUser();
                documentAttachments.webUserId = GetUser();
                SendPDFModel sendPDFModel = _profileRepository.NofityAndSaveProfileAttachment(documentAttachments);
                TrackMessage<SendPDFModel>(sendPDFModel);
                return StatusCode(StatusCodes.Status200OK, value: documentAttachments);
            }
            catch (Exception exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, exception);
            }
        }
        #endregion

    }
}