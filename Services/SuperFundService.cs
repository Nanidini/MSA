using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Slick_Domain.Entities;
using Slick_Domain.Enums;
using Slick_Domain.Models;
using Slick_Domain.Common;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using NLog;
using RestSharp;
using System.Xml;
using System.Xml.Serialization;
using System.Xml.Linq;


namespace Slick_Domain.Services
{
    public class SuperFundService
    {
        static private string SuperfundAPIKey = GlobalVars.GetGlobalTxtVar("SuperfundAPIKey");

        public static MatterCustomEntities.MatterWFSuperFundDetailView GetSuperFundDetailsForAbn(string abn)
        {
            // Set Request of parameter
            //var client = new RestClient("https://superfundlookup.gov.au/xmlsearch/SflXmlSearch.asmx/SearchByABN?abn=82440336940&guid=073d8060-d2aa-4303-b22f-9380d204f883");
            var client = new RestClient("https://superfundlookup.gov.au/xmlsearch/SflXmlSearch.asmx/SearchByABN?abn=" + abn + "&guid=" + SuperfundAPIKey + "");
            var request = new RestRequest(Method.GET);
            string Error = "";
            IRestResponse response = client.Execute(request);

            // Get Response in XML Texts
            string responseXML = response.Content;

            XmlSerializer s = new XmlSerializer(typeof(string), new XmlRootAttribute("Response"));

            XDocument doc = XDocument.Parse(response.Content);

            XElement root = doc.Root;
            foreach (var el in root.Descendants())
            {
                if (el.Name.LocalName == "Response")
                {
                    root = el;
                    break;
                }

            }
            if (responseXML.Contains("Exception"))
            {
                Error = CheckError(root);
            }

            MatterCustomEntities.MatterWFSuperFundDetailView view = new MatterCustomEntities.MatterWFSuperFundDetailView();

            if (Error != null && Error != "" && Error != string.Empty)
            {
                view = null;
            }
            else
            {
                view = new MatterCustomEntities.MatterWFSuperFundDetailView()
                {
                    FundOrganisationName = GetFundOrganisationNameFromXml(root),
                    FundOrganisationTypeCode = GetFundOrganisationTypeCodeFromXml(root),
                    FundTypeCode = GetFundTypeCodeFromXml(root),
                    FundTypeDescription = GetFundTypeDescriptionFromXml(root),
                    Compliant = GetFundComplyingStatusFromXml(root),
                    UpdatedDate = DateTime.Now,
                    UpdatedByUserId = Slick_Domain.GlobalVars.CurrentUser.UserId
                };
            }




            // Convert XML Response to Class
            //return responseXML;
            return view;
        }

        public static string CheckError(XElement root)
        {
            string Err = (from el in root.Descendants()
                          where el.Name.LocalName == "Description"
                          && el.Parent.Name.LocalName == "Exception"
                          select el).First().Value;
            return Err;
        }

        public static string GetFundOrganisationNameFromXml(XElement root)
        {
            string OrganisationName = (from el in root.Descendants()
                                       where el.Name.LocalName == "Name"
                                       && el.Parent.Name.LocalName == "OrganisationName"
                                       select el).First().Value;
            return OrganisationName;
        }

        public static string GetFundOrganisationTypeCodeFromXml(XElement root)
        {
            string OrganisationTypeCode = (from el in root.Descendants()
                                           where el.Name.LocalName == "TypeCode"
                                           && el.Parent.Name.LocalName == "OrganisationName"
                                           select el).First().Value;
            return OrganisationTypeCode;
        }

        public static string GetFundTypeCodeFromXml(XElement root)
        {
            string FundTypeCode = (from el in root.Descendants()
                                   where el.Name.LocalName == "Code"
                                   && el.Parent.Name.LocalName == "FundType"
                                   select el).First().Value;
            return FundTypeCode;
        }

        public static string GetFundTypeDescriptionFromXml(XElement root)
        {
            string FundTypeDescription = (from el in root.Descendants()
                                          where el.Name.LocalName == "Description"
                                          && el.Parent.Name.LocalName == "FundType"
                                          select el).First().Value;
            return FundTypeDescription;
        }

        public static bool GetFundComplyingStatusFromXml(XElement root)
        {
            bool Compliant;
            string ComplyingStatus = (from el in root.Descendants()
                                      where el.Name.LocalName == "Code"
                                      && el.Parent.Name.LocalName == "ComplyingStatus"
                                      select el).First().Value;

            if (ComplyingStatus == "Y") Compliant = true;
            else Compliant = false;

            return Compliant;
        }

        public static string GetSuperFundDetailsNames(string name)
        {
            // Set Request of parameter
            //var client = new RestClient("https://superfundlookup.gov.au/xmlsearch/SflXmlSearch.asmx/SearchByABN?abn=82440336940&guid=073d8060-d2aa-4303-b22f-9380d204f883");
            var client = new RestClient("https://superfundlookup.gov.au/xmlsearch/SflXmlSearch.asmx/SearchByName?name=" + System.Web.HttpUtility.UrlEncode(name) + "&guid=" + SuperfundAPIKey + "&activeFundsOnly=Y");
            var request = new RestRequest(Method.GET);
            IRestResponse response = client.Execute(request);

            // Get Response in XML Texts
            string responseXML = response.Content;

            // Convert XML Response to Class
            //return responseXML;
            //return view;

            return responseXML;
        }

    }


}
