using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Slick_Domain.Constants;
namespace Slick_Domain.Services
{
    public class SmsService
    {
        public static IRestResponse SendMacquarieNowSms(string mobileNumber, string body, string subject = "", string sender = GlobalConstants.MsaNationalSmsName)
        {

            string smsFrom = "MSANational";
            //body = HttpUtility.UrlEncode(body);
            string smsBody = "";
            if (subject == "")
            {
                smsBody = body;
            }
            else
            {
                smsBody = subject + ":\n" + body;
            }

            var client = new RestClient(@"https://now.macquarieview.com/api/3/sms/out?to=" + mobileNumber + "&from=" + sender);
            
            var request = new RestRequest(Method.GET);
            request.AlwaysMultipartFormData = true;
            request.AddParameter("body", smsBody);

            client.Timeout = -1;
            request.AddHeader("Authorization", "Bearer eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIyMDc4IiwiYnJldm8iOnRydWUsImlzcyI6InAiLCJwcmVmZXJyZWRfdXNlcm5hbWUiOiJtY3F0XFxkYW5pZWxoIiwiaWF0IjoxNjA3MzA4MTQ0LCJqdGkiOiJfZWJXcmc9PSJ9.s4H3MgUthBQxcP4DtYHp9xR9I-lHVjThO8Q_sPJY6KY");
            IRestResponse response = client.Execute(request);
            return response;
        }
        

        public static class GSMConverter
        {
            // The index of the character in the string represents the index
            // of the character in the respective character set

            // Basic Character Set
            private const string BASIC_SET =
                    "@£$¥èéùìòÇ\nØø\rÅåΔ_ΦΓΛΩΠΨΣΘΞ\x1bÆæßÉ !\"#¤%&'()*+,-./0123456789:;<=>?" +
                    "¡ABCDEFGHIJKLMNOPQRSTUVWXYZÄÖÑÜ§¿abcdefghijklmnopqrstuvwxyzäöñüà";

            // Basic Character Set Extension 
            private const string EXTENSION_SET =
                    "````````````````````^```````````````````{}`````\\````````````[~]`" +
                    "|````````````````````````````````````€``````````````````````````";

            // If the character is in the extension set, it must be preceded
            // with an 'ESC' character whose index is '27' in the Basic Character Set
            private const int ESC_INDEX = 27;

            public static string StringToGSMHexString(string text, bool delimitWithDash = true)
            {
                // Replace \r\n with \r to reduce character count
                //text = text.Replace(Environment.NewLine, "\r");

                // Use this list to store the index of the character in 
                // the basic/extension character sets
                var indicies = new List<int>();

                foreach (var c in text)
                {
                    int index = BASIC_SET.IndexOf(c);
                    if (index != -1)
                    {
                        indicies.Add(index);
                        continue;
                    }

                    index = EXTENSION_SET.IndexOf(c);
                    if (index != -1)
                    {
                        // Add the 'ESC' character index before adding 
                        // the extension character index
                        indicies.Add(ESC_INDEX);
                        indicies.Add(index);
                        continue;
                    }
                }

                // Convert indicies to 2-digit hex
                var hex = indicies.Select(i => i.ToString("X2")).ToArray();

                string delimiter = delimitWithDash ? "-" : "";

                // Delimit output
                string delimited = string.Join(delimiter, hex);
                return delimited;
            }
        }
    }
}
