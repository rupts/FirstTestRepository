using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Offcie365.TestSSO.Models;
using System.Data.Services.Client;
using Microsoft.WindowsAzure.ActiveDirectory.GraphHelper;
using Microsoft.WindowsAzure.ActiveDirectory;
using System.Text;

namespace Offcie365.TestSSO.Controllers
{
    public class HomeController : Controller
    {
        private const string TenantIdClaimType = "http://schemas.microsoft.com/identity/claims/tenantid";
        private const string LoginUrl = "https://login.windows.net/{0}";
        private const string GraphUrl = "https://graph.windows.net";
        private const string GraphUserUrl = "https://graph.windows.net/{0}/users/{1}?api-version=2013-04-05";
        private static readonly string AppPrincipalId = ConfigurationManager.AppSettings["ida:ClientID"];
        private static readonly string AppKey = ConfigurationManager.AppSettings["ida:Password"];

        public ActionResult Index()
        {
            return View();
        }

        public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }

        public ActionResult Stats()
        {
            var resource = "https://evartebe-my.sharepoint.com"; // Request access to the AD graph resource.
            var redirectURI = "https://localhost:44304/Home/CatchCode"; // The URL where the authorization code is sent on redirect.

            string authorizationUrl = string.Format("https://login.windows.net/{0}/oauth2/authorize?api-version=1.0&response_type=code&client_id={1}&resource={2}&redirect_uri={3}",
                ClaimsPrincipal.Current.FindFirst(TenantIdClaimType).Value,
                AppPrincipalId,
                resource,
                redirectURI
            );

            return new RedirectResult(authorizationUrl);
        }

        public ActionResult CatchCode(string code)
        { //  Replace the following port with the correct port number from your own project.
            var appRedirect = "https://localhost:44304/Home/CatchCode";

            //  Create an authentication context.
            AuthenticationContext ac = new AuthenticationContext(string.Format("https://login.windows.net/{0}",
            ClaimsPrincipal.Current.FindFirst(TenantIdClaimType).Value));

            //  Create a client credential based on the application ID and secret.
            ClientCredential clcred = new ClientCredential(AppPrincipalId, AppKey);

            //  Use the authorization code to acquire an access token.
            var arAD = ac.AcquireTokenByAuthorizationCode(code, new Uri(appRedirect), clcred);

            //  Convert token to the ADToken so you can use it in the graphhelper project.

            AADJWTToken token = new AADJWTToken();
            token.AccessToken = arAD.AccessToken;

            //  Initialize a graphService instance by using the token acquired in the previous step.

            //string tenantName = ClaimsPrincipal.Current.FindFirst(TenantIdClaimType).Value;
            //DirectoryDataService graphService = new DirectoryDataService(tenantName, token);
            //graphService.BaseUri = new Uri(string.Format("https://graph.windows.net/{0}", tenantName));

            ////  Get the list of all users.

            //var users = graphService.users;
            //QueryOperationResponse<Microsoft.WindowsAzure.ActiveDirectory.User> response;
            //response = users.Execute() as QueryOperationResponse<Microsoft.WindowsAzure.ActiveDirectory.User>;
            //List<Microsoft.WindowsAzure.ActiveDirectory.User> userList = response.ToList();
            //ViewBag.userList = userList;

            if (arAD.IsMultipleResourceRefreshToken)
            {
                // This is an MRRT so use it to request an access token for SharePoint.
                AuthenticationResult arSP = ac.AcquireTokenByRefreshToken(arAD.RefreshToken, AppPrincipalId, clcred, "https://evartebe-my.sharepoint.com");
            }

            //  Now make a call to get a list of all files in a folder. 
            //  Replace placeholders in the following string with correct values for your domain and user name. 

            var skyGetAllFilesCommand = "https://evartebe-my.sharepoint.com/_api/v1.0/me/files/getByPath('/Shared%20With%20Everyone')";

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(skyGetAllFilesCommand);
            request.Headers.Add("Authorization", string.Format("Bearer {0}", arAD.AccessToken));
            request.Method = "GET";

            var response = request.GetResponse();

          
            using (var receiveStream = response.GetResponseStream())
            using (var readStream = new StreamReader(receiveStream, Encoding.UTF8))
            {
                var body = readStream.ReadToEnd();
                ViewBag.skyResponse = body.ToString();
            }

            return View();
        }

        [Authorize]
        public async Task<ActionResult> UserProfile()
        {
            string tenantId = ClaimsPrincipal.Current.FindFirst(TenantIdClaimType).Value;

            // Get a token for calling the Windows Azure Active Directory Graph
            AuthenticationContext authContext = new AuthenticationContext(String.Format(CultureInfo.InvariantCulture, LoginUrl, tenantId));
            ClientCredential credential = new ClientCredential(AppPrincipalId, AppKey);
            AuthenticationResult assertionCredential = authContext.AcquireToken(GraphUrl, credential);
            string authHeader = assertionCredential.CreateAuthorizationHeader();
            string requestUrl = String.Format(
                CultureInfo.InvariantCulture,
                GraphUserUrl,
                HttpUtility.UrlEncode(tenantId),
                HttpUtility.UrlEncode(User.Identity.Name));

            HttpClient client = new HttpClient();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            request.Headers.TryAddWithoutValidation("Authorization", authHeader);
            HttpResponseMessage response = await client.SendAsync(request);
            string responseString = await response.Content.ReadAsStringAsync();
            UserProfile profile = JsonConvert.DeserializeObject<UserProfile>(responseString);

            return View(profile);
        }
    }
}