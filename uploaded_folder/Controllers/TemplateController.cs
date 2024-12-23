using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using COR.Profile.Api.DataHandler;
using COR.Profile.Api.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using COR.Profile.Api.Models;
using System;
using COR.Profile.DataModel.DataModel;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace COR.Profile.Api.Controllers
{
    [Route("[controller]")]
    [ApiController]
    [EnableRateLimiting("fixed")]
    [Authorize]
    public class TemplateController : BaseController
    {
        private readonly ITemplateRepository _templateRepository;
        private IProfileDataRepository<ProfileModel, long> _profileRepository { get; set; }

        public TemplateController(IProfileDataRepository<ProfileModel, long> _profileRepositoryObject, FeatureFlagService featureFlagService, ITemplateRepository templateRepository) : base(featureFlagService)
        {
            this._templateRepository = templateRepository;
            _profileRepository = _profileRepositoryObject;
        }

        [HttpPost]
        [Route("TemplateList")]
        public object Template(TemplateListRequest templateListRequest)
        {
            try
            {
                return _templateRepository.GetTemplateList(templateListRequest);
            }
            catch(Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message); ;
            }

        }

        [HttpGet]
        [Route("FormTemplateLockedItems")]
        public object FormTemplateLockedItems(int formId)
        {
            return _templateRepository.FormTemplateLockedItems(formId);
        }

        [HttpPost]
        [Route("FormTemplateDetails")]
        public object FormTemplateDetails([FromBody] FormWcrParams Param)
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

        // COR2 Phase-4 Code
        //[HttpPost]
        //[Route("FormTemplateLockedFields")]
        //public async Task<object> FormTemplateLockedFields([FromBody] LockedFieldRequest Param)
        //{
        //    try
        //    {
        //        Param.UserName = GetUser();
        //        return await _templateRepository.FormTemplateLockedFields(Param);
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        //    }
        //}

        [HttpPost]
        [Route("remove-template")]
        public async Task<IActionResult> DeleteTemplate([FromBody] DeleteTemplateRequest deleteTemplateRequest)
        {
            try
            {
                var UserName = GetUser();
                var response = _templateRepository.DeleteTemplate(deleteTemplateRequest, UserName);
                return Ok(response);
            }
            catch(Exception ex) 
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }

        }

    }
}
