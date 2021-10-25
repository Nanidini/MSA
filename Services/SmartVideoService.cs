using Slick_Domain.Common;
using Slick_Domain.Entities;
using Slick_Domain.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using RestSharp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Web.Script.Serialization;

namespace Slick_Domain.Services
{
    public static class SmartVideoService
    {
        public static string SendSmartVideos(List<MatterCustomEntities.MatterView> matterList)
        {
            string SmartVidAuthString = "Basic bHAzZWw2cDhtYjQ1aHRmazcwMW1kbGU9PTo2N2YyNjEwZjExODI4MDUzYWI5ZTE4YmM1YzUwZjg4MQ==";
            string SmartVidProjectKey = "rkfltiwm4lw2paioxl";
            string SmartVidBaseUrl = "https://sydney.personalisedvideolab.com/purse/";

            string ResponseUrl = ""; //TEMPORARY - SHOULD RETURN THE UPDATED LIST<MATTERSMARTVIDEO> WHEN THAT DB FIELD CREATED 

            var client = new RestClient("https://sydney.personalisedvideolab.com/api/render/1001");
            var request = new RestRequest(Method.POST);
            request.AddHeader("cache-control", "no-cache");
            request.AddHeader("Authorization", SmartVidAuthString);
            request.AddHeader("Content-Type", "application/json");
            List<int> matterIDs = new List<int>();

            string requestString;


            SmartVideoRequest svRequest = new SmartVideoRequest
            {
                projectkey = SmartVidProjectKey
            };

            List<SmartVideoRequestVariable> projVariables = new List<SmartVideoRequestVariable>();

            foreach (MatterCustomEntities.MatterView mt in matterList)
            {
                matterIDs.Add(mt.MatterId);

                MatterCustomEntities.MatterPartyView primaryPt = mt.Parties.Where(p => p.PartyTypeId == (int)Enums.PartyTypeEnum.Borrower).FirstOrDefault();

                SmartVideoRequestVariable projVariable = new SmartVideoRequestVariable
                {
                    id = mt.MatterId.ToString(),

                    name_text = mt.Parties.Where(p => p.PartyTypeId == (int)Enums.PartyTypeEnum.Borrower).FirstOrDefault().Firstname,
                    name_audio = mt.Parties.Where(p => p.PartyTypeId == (int)Enums.PartyTypeEnum.Borrower).FirstOrDefault().Firstname + ".mp3",

                    loan_amount_text = String.Format("{0:n}", mt.LoanAccounts.Sum(m => m.LoanAmount)),
                    loan_amount_audio = SmartVidBaseUrl + mt.LoanAccounts.Sum(x => x.LoanAmount),

                    monthly_payment_text = "$1490.35",//TEMP - Needs to be determined
                    monthly_payment_audio = SmartVidBaseUrl + "1490.35",//TEMP - Needs to be determined

                    phone_number = primaryPt.Mobile,
                    address_text = primaryPt.StreetAddress + ", " + primaryPt.Suburb + ", " + primaryPt.StateName + ", " + primaryPt.PostCode,

                    insurance_text = "$595959", //TEMP - Needs to be determined
                    insurance_audio = SmartVidBaseUrl+"595959", //TEMP - Needs to be determined

                    who_signs_audio = "They sign.mp3"
                };

                if (mt.BrokerName != null)
                {
                    projVariable.broker_text = mt.BrokerName;
                    projVariable.broker_audio = mt.BrokerName + ".mp3";
                }

                if (mt.Parties.Where(p => p.PartyTypeId == (int)Enums.PartyTypeEnum.Borrower).Count() == 2) //TEMP - What if there are more than two borrowers? How do we work out which borrower is 'primary'?
                {
                    var mtParties = mt.Parties.Where(p => p.PartyTypeId == (int)Enums.PartyTypeEnum.Borrower).ToList();
                    projVariable.coborrower_text = mtParties[2].Firstname;
                    projVariable.coborrower_audio = "Co-borrower.mp3";
                }

                if (mt.MatterTypeId == (int)Enums.MatterTypeEnum.Refinance || mt.MatterTypeId == (int)Enums.MatterTypeEnum.Refinance)
                {
                    projVariable.outgoing_lender_text = "Commonwealth Bank"; //TEMP - Needs to be determined                
                }

                projVariables.Add(projVariable);
            }

            svRequest.variables = projVariables;

            requestString = JsonConvert.SerializeObject(svRequest,
                            Newtonsoft.Json.Formatting.Indented,
                            new JsonSerializerSettings
                            {
                                NullValueHandling = NullValueHandling.Ignore
                            });

            requestString = requestString.Replace("coborrower_text", "co-borrower_text"); //since we can't have dashes in variable names, but myvid wants dashes
            requestString = requestString.Replace("coborrower_audio", "co-borrower_audio");

            Console.WriteLine(requestString);

            request.AddParameter("undefined", requestString, ParameterType.RequestBody);

            //EXECUTE
            IRestResponse response = client.Execute(request);

            //GET THE RESPONSE
            string responseText = response.Content;
            JavaScriptSerializer serializer = new JavaScriptSerializer();

            Console.WriteLine(responseText);

            JObject smartVidResponse = JObject.Parse(responseText);

            SmartVideoResponse ResponseObj = new SmartVideoResponse()
            {
                Response = smartVidResponse["Response"].ToString(),
                Receipt = smartVidResponse["Receipt"].ToString(),
                Count = smartVidResponse["Count"].ToString(),
            };

            List<Tokens> tokens = new List<Tokens>();

            var tokenItems = smartVidResponse["Tokens"];
            foreach (var token in tokenItems)
            {
                JProperty jProperty = token.ToObject<JProperty>();

                tokens.Add(new Tokens {
                    matterId = jProperty.Name ,
                    token = token.First().ToString()
                });
            }

            ResponseObj.Tokens = tokens;
            //foreach(var token in response.Content)
            //Console.WriteLine(responseText);
            Console.WriteLine(tokens.FirstOrDefault().matterId + ": " + "https://secure-sydney.personalisedvideolab.com/" + tokens.FirstOrDefault().token +".mp4");
            return ResponseUrl;
        }

        public class SmartVideoRequest
        {
            public string projectkey;
            public List<SmartVideoRequestVariable> variables;

        }
        public class SmartVideoRequestVariable
        {
            public string id { get; set; }
            public string name_text { get; set; }
            public string name_audio { get; set; }
            public string loan_amount_text { get; set; }
            public string loan_amount_audio { get; set; }
            public string monthly_payment_text { get; set; }
            public string monthly_payment_audio { get; set; }
            public string broker_text { get; set; }
            public string broker_audio { get; set; }
            public string phone_number { get; set; }
            public string address_text { get; set; }
            public string insurance_text { get; set; }
            public string insurance_audio { get; set; }

            public string coborrower_text{ get; set; }

            public string coborrower_audio { get; set; }
            public string outgoing_lender_text { get; set; }
            public string who_signs_audio { get; set; }

        }


        public class SmartVideoResponse
        {
            //{"Response":200,"Receipt": 618817,"Count":"2","Tokens": {"asdasdsaddxvc":"e4940ed0904347609ee01b1ac5c5bbd7","1232132132":"8715017e34294607af140708cebfec69"}}
            public string Response { get; set; }
            public string Receipt { get; set; }
            public string Count { get; set; }
            public List<Tokens> Tokens { get; set; }
        }
        public class Tokens
        {
            public string matterId { get; set; }
            public string token { get; set; }
        }

    }
   
   
}
