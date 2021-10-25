using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Slick_Domain.Entities.DocuSignCustomEntities;
using Newtonsoft.Json;
using static Slick_Domain.Entities.TeamCustomEntities;

namespace Slick_Domain.Services
{
    public static class DocuSignService
    {
        public static DocuSignEnvelopeHistory GetEnvelopeHistory(string matterId, string envelopeIdentifier)
        {
            var client = new RestClient(GlobalVars.GetGlobalTxtVar("SlickInternalWebServiceUrl") + "api/GetEnvelopeHistory");
            client.Timeout = -1;
            var request = new RestRequest(Method.GET);
            request.AddParameter("matterId", matterId);
            request.AddParameter("envelopeIdentifier", envelopeIdentifier);
            IRestResponse response = client.Execute(request);
            return JsonConvert.DeserializeObject<DocuSignEnvelopeHistory>(response.Content);
        }

        public static IRestResponse VoidEnvelope(int matterId, string envelopeIdentifier, string voidReason)
        {
            var client = new RestClient(GlobalVars.GetGlobalTxtVar("SlickInternalWebServiceUrl") + "api/VoidDocuSignEnvelope");
            client.Timeout = -1;
            var request = new RestRequest(Method.GET);
            request.AddParameter("matterId", matterId);
            request.AddParameter("envelopeIdentifier", envelopeIdentifier);
            request.AddParameter("voidReason", voidReason);
            IRestResponse response = client.Execute(request);
            return response;
        }

        public static IRestResponse CreateUser(UserTeamListView user, int lenderId)
        {
            var client = new RestClient(GlobalVars.GetGlobalTxtVar("SlickInternalWebServiceUrl") + "api/CreateUser");
            client.Timeout = -1;
            var request = new RestRequest(Method.POST);
            request.AddObject(user);
            request.AddParameter("LenderId", lenderId);
            IRestResponse response = client.Execute(request);
            return response;
        }

        public static IRestResponse GetUsers(int LenderId)
        {
            var client = new RestClient(GlobalVars.GetGlobalTxtVar("SlickInternalWebServiceUrl") + "api/GetUsers");
            client.Timeout = -1;
            var request = new RestRequest(Method.GET);
            request.AddParameter("LenderId", LenderId);
            IRestResponse response = client.Execute(request);
            return response;
        }
        public static IRestResponse GetUser(int LenderId, string email)
        {
            var client = new RestClient(GlobalVars.GetGlobalTxtVar("SlickInternalWebServiceUrl") + "api/GetUser");
            client.Timeout = -1;
            var request = new RestRequest(Method.GET);
            request.AddParameter("LenderId", LenderId);
            request.AddParameter("email", email);
            IRestResponse response = client.Execute(request);
            return response;
        }
        public static IRestResponse DeleteUser(int LenderId, string email)
        {
            var client = new RestClient(GlobalVars.GetGlobalTxtVar("SlickInternalWebServiceUrl") + "api/DeleteUser");
            client.Timeout = -1;
            var request = new RestRequest(Method.DELETE);
            request.AddParameter("LenderId", LenderId);
            request.AddParameter("email", email);
            IRestResponse response = client.Execute(request);
            return response;
        }
        public static IRestResponse UpdateSharedAccess(string recipientEmail, string sourceEmails, int LenderId)
        {
            var client = new RestClient(GlobalVars.GetGlobalTxtVar("SlickInternalWebServiceUrl") + "api/UpdateSharedAccess");
            client.Timeout = -1;
            var request = new RestRequest(Method.PUT);
            request.AddQueryParameter("recipientEmail", recipientEmail);
            request.AddQueryParameter("sourceEmails", sourceEmails);
            request.AddQueryParameter("LenderId", LenderId.ToString());
            IRestResponse response = client.Execute(request);
            return response;
        }
        public static IRestResponse RemoveSharedAccess(string recipientEmail, string sourceEmail, int LenderId)
        {
            var client = new RestClient(GlobalVars.GetGlobalTxtVar("SlickInternalWebServiceUrl") + "api/RemoveSharedAccess");
            client.Timeout = -1;
            var request = new RestRequest(Method.PUT);
            request.AddQueryParameter("recipientEmail", recipientEmail);
            request.AddQueryParameter("sourceEmail", sourceEmail);
            request.AddQueryParameter("LenderId", LenderId.ToString());
            IRestResponse response = client.Execute(request);
            return response;
        }
        public static IRestResponse GetSharedAccess(string recipientEmail, int LenderId)
        {
            var client = new RestClient(GlobalVars.GetGlobalTxtVar("SlickInternalWebServiceUrl") + "api/GetSharedAccess");
            client.Timeout = -1;
            var request = new RestRequest(Method.PUT);
            request.AddQueryParameter("recipientEmail", recipientEmail);
            request.AddQueryParameter("LenderId", LenderId.ToString());
            IRestResponse response = client.Execute(request);
            return response;
        }
    }
}
