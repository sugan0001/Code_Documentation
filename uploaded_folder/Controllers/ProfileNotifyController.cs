using COR.Profile.Api.DataHandler;
using COR.Profile.Api.Models;
using COR.Profile.Api.Repositories;
using COR.Profile.Constants;
using COR.Profile.DataModel.DataModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using OfficeOpenXml.FormulaParsing.Excel.Functions.Math;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

namespace COR.Profile.Api.Controllers
{
    [Route("[controller]")]
    [ApiController]
    [EnableRateLimiting("fixed")]
    [Authorize]
    public class ProfileNotifyController : BaseController
    {
        private IProfileNotifyRepository _profileRepository { get; set; }

        public ProfileNotifyController(IProfileNotifyRepository _profileRepositoryObject,
               FeatureFlagService featureFlagService) : base(featureFlagService)
        {
       
            _profileRepository = _profileRepositoryObject;
        }


        [HttpGet]
        [Route("ProfileAlertUsers")]
        public IActionResult ProfileAlertUsers()
        {
            try
            {
                var response = _profileRepository.GetProfileAlertUsers();
                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpPost]
        [Route("ExpiringProfileAlert")]
        public async Task<IActionResult> SendProfileAlert(ProfileAlertRequest profileAlertRequest)
        {
            try
            {
                var web_userid = GetUser();
                var result = await _profileRepository.SendProfileAlert(profileAlertRequest, web_userid);
                return StatusCode(StatusCodes.Status200OK, result);
            }
            catch (Exception exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, exception.Message);
            }
        }


    }
}

