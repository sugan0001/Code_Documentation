using System;
using System.Linq;
using System.Threading.Tasks;
using COR.Profile.DataModel.LDRbuilder.DataModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.RateLimiting;
using COR.Profile.Api.DataHandler;

namespace COR.Profile.Api.Controllers
{
    [Route("[controller]")]
    [ApiController]
    [EnableRateLimiting("fixed")]
    [Authorize]
    public class LDRBuilderController : BaseController
    {
        private IConfiguration _configuration;
        private BuilderContext LDRDBContext;
        string Gateway = string.Empty;
        string web_userid = string.Empty;
        public LDRBuilderController(IConfiguration Configuration, FeatureFlagService featureFlagService) : base(featureFlagService)
        {
            _configuration = Configuration;
            LDRDBContext = new BuilderContext(Configuration);
            Gateway = _configuration["Gateway"];
        }

        [HttpPost]
        [Route("LDRBuilderSave")]
        public async Task<object> SaveCORLDRBuilder(Corldrbuilder Corldrbuilder)
        {
            try
            {

                string userId = GetUser();
                DateTime CurrerntDate = DateTime.Now;
                if (ModelState.IsValid)
                {
                    Corldrbuilder.CreatedBy = userId;
                    Corldrbuilder.CreatedDate = CurrerntDate;
                    Corldrbuilder.ModifiedBy = userId;
                    Corldrbuilder.ModifiedDate = CurrerntDate;
                    SaveCORLdrbuilderLines(ref Corldrbuilder);
                    SaveCORLdrbuilderSubcategory(ref Corldrbuilder);
                    SaveCORLdrbuilderConstituents(ref Corldrbuilder);
                    SaveCORLdrbuilderWasteCodes(ref Corldrbuilder);
                    LDRDBContext.Corldrbuilder.Add(Corldrbuilder);
                    await LDRDBContext.SaveChangesAsync();
                    return StatusCode(StatusCodes.Status200OK, value: new { ldrbuilderID = Corldrbuilder.LdrbuilderId });
                }
                else
                {
                    string messages = string.Join("; ", ModelState.Values
                                        .SelectMany(x => x.Errors)
                                        .Select(x => x.ErrorMessage));
                    return StatusCode(StatusCodes.Status400BadRequest, value: messages);
                }
            }
            catch (Exception exception)
            {
                throw new Exception(exception.Message);
            }
        }

        private void SaveCORLdrbuilderLines(ref Corldrbuilder Corldrbuilder)
        {
            string userid = Corldrbuilder.CreatedBy;
            DateTime? date = Corldrbuilder.CreatedDate;
            Corldrbuilder.CorldrbuilderLines.ToList().ForEach(e =>
            {
                e.CreatedDate = date;
                e.CreatedBy = userid;
                e.ModifiedBy = userid;
                e.ModifiedDate = date;
            });
        }

        private void SaveCORLdrbuilderSubcategory(ref Corldrbuilder Corldrbuilder)
        {
            string userid = Corldrbuilder.CreatedBy;
            DateTime? date = Corldrbuilder.CreatedDate;
            Corldrbuilder.CorldrbuilderLines.ToList().ForEach(s =>
            {
                s.CorldrbuilderSubcategory.ToList().ForEach(c =>
                {
                    c.CreatedDate = date;
                    c.CreatedBy = userid;
                    c.ModifiedBy = userid;
                    c.ModifiedDate = date;
                });
            });
        }

        private void SaveCORLdrbuilderConstituents(ref Corldrbuilder Corldrbuilder)
        {
            string userid = Corldrbuilder.CreatedBy;
            DateTime? date = Corldrbuilder.CreatedDate;
            Corldrbuilder.CorldrbuilderLines.ToList().ForEach(s =>
            {
                s.CorldrbuilderConstituents.ToList().ForEach(c =>
                {
                    c.CreatedDate = date;
                    c.CreatedBy = userid;
                    c.ModifiedBy = userid;
                    c.ModifiedDate = date;
                });
            });
        }

        private void SaveCORLdrbuilderWasteCodes(ref Corldrbuilder Corldrbuilder)
        {
            string userid = Corldrbuilder.CreatedBy;
            DateTime? date = Corldrbuilder.CreatedDate;
            Corldrbuilder.CorldrbuilderLines.ToList().ForEach(s =>
            {
                s.CorldrbuilderWasteCode.ToList().ForEach(c =>
                {
                    c.CreatedDate = date;
                    c.CreatedBy = userid;
                    c.ModifiedBy = userid;
                    c.ModifiedDate = date;
                });
            });
        }

    }
}
