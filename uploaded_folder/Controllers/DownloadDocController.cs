using System;
using System.IO;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;

namespace COR.Profile.Api.Controllers
{
    [Route("[controller]")]
    [NonController]
    [ApiController]
    [EnableRateLimiting("fixed")]
    public class DownloadDocController
    {
        private IConfiguration _configuration { get; set; }

        public DownloadDocController(IConfiguration Configuration)
        {
            _configuration = Configuration;                      
        }

        [NonAction]
        public string DownloadFile(string docUrl)
        {            
            string tempPath = Path.GetTempPath();
            if (!string.IsNullOrEmpty(docUrl))
            {

                string _sessionPDFFileName = "ProfileDocument" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".pdf";

                tempPath += _sessionPDFFileName;

                using (WebClient webclient = new ())
                {
                  
                    webclient.Credentials = new NetworkCredential(_configuration["ReportServerConfig:UserName"], Constants.Helper.EncryptionHelper.DecryptString(_configuration["ReportServerConfig:Password"]));

                    webclient.DownloadFile(docUrl, tempPath);
                }
                return tempPath;

            }

            return tempPath;
        }
    }
}