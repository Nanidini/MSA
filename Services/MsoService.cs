using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RestSharp;
using System.Net;
using HtmlAgilityPack;
using System.Data;
using System.Globalization;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace Slick_Domain.Services
{
    public class MsoService
    {

        private CookieContainer _cookieContainer = new CookieContainer();
        private string _hidValue, _applicationId, _username, _password;
        int _matterId, _matterCertificationQueueId;
        private string notifyEmail = "daniel.hwang@msanational.com.au";
        private HtmlDocument _htmlDocument = new HtmlDocument();
        public int StateId { get; set; }
        public DateTime? _lastLoginDate;

        public List<int> GetQueueItems()
        {
            using (var uow = new UnitOfWork(isolation: IsolationLevel.ReadUncommitted))
            {
                //var matterRepository = uow.GetMatterRepositoryInstance();
                return uow.Context.MatterCertificationQueues
                    .Where(x => x.RequestStatusTypeId == (int)Slick_Domain.Enums.CertificationRequestStatusTypeEnum.Requested)
                    .Select(x => x.MatterCertificationQueueId).ToList();
            }
            
        }



        public bool Login(string username = null, string password = null, bool sendEmail = false, bool alwaysLogin = false)
        {
            try
            {
                if (alwaysLogin || !_lastLoginDate.HasValue || (DateTime.Now - _lastLoginDate.Value).Minutes > 10)
                {

                    if (username != null) _username = username;
                    if (password != null) _password = password;
                    var request = new RestRequest(Method.POST);
                    request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
                    request.AddParameter("username", _username);
                    request.AddParameter("password", _password);
                    var response = MsoClient(request, "https://mso.macquarie.com.au/epkms/auth");

                    //IRestResponse response = client.Execute(request);
                    Console.WriteLine("----- Logged In -----");


                    _lastLoginDate = DateTime.Now;
                    return true;
                }
                else
                {
                    return true;
                }
            }
            catch (Exception e)
            {
                if (sendEmail)
                {
                    EmailsService.SendSimpleNoReplyEmail(
                           new List<string>() { notifyEmail },
                           new List<string>() { },
                           "Certification: MSO Login Failed .NET issue - Matter Number: " + _matterId + "",
                           "Failed to log into MSO:\n Message:" + e.Message + "\nStack:" + e.StackTrace
                       );
                }
                return false;
            }

        }

        public void GetHiddenID(bool sendEmail = false)
        {
            // I logged into MSO - Please provide me with a cookie  the HID value

            var request = new RestRequest(Method.GET);
            var response = MsoClient(request,
                "https://mso.macquarie.com.au/mso/prweb/MSOServlet" +
                "?pyActivity=%40baseclass.doUIAction&action=display&harnessName=SolicitorRelatedCases" +
                "&className=MGL-BFS-InvestDepositApp-Work&readOnly=false&UserIdentifier=64597023");
            Console.WriteLine("----- Received Hidden Id -----");
            _htmlDocument.LoadHtml(response.Content);
            _hidValue = _htmlDocument.GetElementbyId("pzHarnessID").GetAttributeValue("value", "");

            if (_hidValue == null)
            {
                if (sendEmail)
                {
                    EmailsService.SendSimpleNoReplyEmail(
                       new List<string>() { notifyEmail },
                       new List<string>() { },
                       "Certification: MSO Login Failed - Expected HID value from MSO - Matter Number: " + _matterId + "",
                       "Failed to log into MSO - Matter Number: " + _matterId
                   );
                }

                return;
            }
        }

        public void OpenApplicationPage(string applicationId)
        {
            _applicationId = applicationId?.Trim();
            ChromeOptions chromeOptions = new ChromeOptions()
            {
                UnhandledPromptBehavior = UnhandledPromptBehavior.Accept,
                LeaveBrowserRunning = true,

            };
            chromeOptions.LeaveBrowserRunning = true;
            var chromeDriverService = ChromeDriverService.CreateDefaultService(Slick_Domain.GlobalVars.GetGlobalTxtVar("ChromeDriverPath"));
            chromeDriverService.HideCommandPromptWindow = true;
            chromeOptions.AddExcludedArgument("enable-automation");
            chromeOptions.AddAdditionalCapability("useAutomationExtension", false);
            chromeOptions.AddArgument("--start-maximized");
            IWebDriver driver = new ChromeDriver(chromeDriverService, chromeOptions);

            try
            {
                //ChromeOptions chromeOptions = new ChromeOptions()
                //{
                //    UnhandledPromptBehavior = UnhandledPromptBehavior.Accept,
                //    auto
                //};

                string applicationPageUrl = "https://mso.macquarie.com.au/mso/prweb/MSOServlet" +
                "?pyActivity=doUIAction&action=openWorkByHandle&harnessName=&readOnly=&className=&" +
                            "performPreProcessing=&pzPrimaryPage=&workID=&workPool=&key=MGL-BFS-INVESTDEPOSITAPP-WORK%20" + _applicationId
                            + "&flowName=&preActivity=&preActivityParams=&target=_blank&pzHarnessID=" + _hidValue;
                var request = new RestRequest(Method.GET);
                var response = MsoClient(request, applicationPageUrl);
                var absolutePath = "https://mso.macquarie.com.au" + response.ResponseUri.AbsolutePath.Replace("!STANDARD", @"!InvestDepositApp/$PegaMashup");
                var cookieCollection = _cookieContainer.GetCookies(new Uri(@"https://mso.macquarie.com.au"));
                driver.WindowHandles.FirstOrDefault();
                driver.Navigate().GoToUrl(@"https://mso.macquarie.com.au/mso/prwebmu/images/standardlogo.gif");
                foreach (System.Net.Cookie cookie in cookieCollection)
                {
                    OpenQA.Selenium.Cookie chromeCookie = new OpenQA.Selenium.Cookie(cookie.Name, cookie.Value);
                    driver.Manage().Cookies.AddCookie(chromeCookie);
                }
                //driver.Navigate().GoToUrl(applicationPageUrl);
                applicationPageUrl = absolutePath + "?pyActivity=doUIAction&action=openWorkByHandle&harnessName=&readOnly=&className=&performPreProcessing=&pzPrimaryPage=&workID=&workPool=&key=MGL-BFS-INVESTDEPOSITAPP-WORK%20" + _applicationId
                + "&flowName=&preActivity=&preActivityParams=&target=_blank&pzHarnessID=" + _hidValue;
                driver.Navigate().GoToUrl(applicationPageUrl);
                //var request = new RestRequest(Method.GET);
                //var response = MsoClient(request, applicationPageUrl);

                //_htmlDocument.LoadHtml(response.Content);
                //var titleNode = _htmlDocument.DocumentNode.SelectSingleNode("//title");
                //driver.Quit();

                foreach (var proc in System.Diagnostics.Process.GetProcessesByName("chromedriver"))
                {
                    try
                    {
                        proc.Kill();
                    }
                    catch (Exception e)
                    {
                        //C'est la vie, it aint our chrome driver.
                    }
                }



            }
            catch (Exception e)
            {
                driver.Quit();
            }
        }

        public void ClearCookie()
        {
            _cookieContainer = new CookieContainer();
        }

        public bool LoadApplicationPage(int? matterCertificationQueueId = null)
        {

            using (var uow = new UnitOfWork(isolation: IsolationLevel.ReadUncommitted))
            {
                if (matterCertificationQueueId != null)
                {
                    _matterCertificationQueueId = (int)matterCertificationQueueId;
                    _matterId = uow.Context.MatterCertificationQueues.Where(x => x.MatterCertificationQueueId == matterCertificationQueueId).Select(x => x.MatterId).FirstOrDefault();
                }
                _applicationId = uow.Context.Matters.Where(x => x.MatterId == _matterId).Select(x => x.LenderRefNo).FirstOrDefault().Trim();
            }
            try
            {
                string applicationPageUrl = "https://mso.macquarie.com.au/mso/prweb/MSOServlet" +
                "?pyActivity=doUIAction&action=openWorkByHandle&harnessName=&readOnly=&className=&" +
                            "performPreProcessing=&pzPrimaryPage=&workID=&workPool=&key=MGL-BFS-INVESTDEPOSITAPP-WORK%20" + _applicationId
                            + "&flowName=&preActivity=&preActivityParams=&target=_blank&pzHarnessID=" + _hidValue;
                var request = new RestRequest(Method.GET);
                var response = MsoClient(request, applicationPageUrl);

                _htmlDocument.LoadHtml(response.Content);
                var titleNode = _htmlDocument.DocumentNode.SelectSingleNode("//title");
                if (titleNode == null || titleNode.InnerText == "Case not found")
                {
                    EmailsService.SendSimpleNoReplyEmail(
                           new List<string>() { notifyEmail },
                           new List<string>() { },
                           "Certification: MSO Load Application - Wrong APP " + _applicationId + "-- Matter Number: " + _matterId + "",
                           "Failed to load application page"
                       );
                    using (var uow = new UnitOfWork(isolation: IsolationLevel.ReadCommitted))
                    {
                        var matterCertificationQueueItem = uow.Context.MatterCertificationQueues.Where(x => x.MatterCertificationQueueId == _matterCertificationQueueId).FirstOrDefault();
                        matterCertificationQueueItem.RequestStatusTypeId = (int)Slick_Domain.Enums.CertificationRequestStatusTypeEnum.Error;
                        matterCertificationQueueItem.UpdatedDate = DateTime.UtcNow;
                        uow.Save();
                        uow.CommitTransaction();
                    }
                    return false;
                }
                else
                {
                    return true;
                }
            }
            catch (Exception e)
            {
                using (var uow = new UnitOfWork(isolation: IsolationLevel.ReadUncommitted))
                {
                    var matterCertificationQueueItem = uow.Context.MatterCertificationQueues.Where(x => x.MatterCertificationQueueId == _matterCertificationQueueId).FirstOrDefault();
                    matterCertificationQueueItem.RequestStatusTypeId = (int)Slick_Domain.Enums.CertificationRequestStatusTypeEnum.Error;
                    matterCertificationQueueItem.UpdatedDate = DateTime.UtcNow;
                    uow.Save();
                    uow.CommitTransaction();
                }
                EmailsService.SendSimpleNoReplyEmail(
                           new List<string>() { notifyEmail },
                           new List<string>() { },
                           "Certification: MSO Load Application .NET issue - Matter Number: " + _matterId + "",
                           "Failed to load application page:\n Message:" + e.Message + "\nStack:" + e.StackTrace
                       );
                return false;
            }
        }

        public void SaveCertification(ref string certificationPageCss)
        {
            string certificationPagePath = null, certificationFilePath = null;
            try
            {
                string certificationPageUrl = "https://mso.macquarie.com.au/mso/prweb/MSOServlet?pyActivity=PrintSection&sectionname=CertificationScreenSummaryPDF&pzPrimaryPageName=pyWorkPage&target=popup&pzHarnessID=" + _hidValue;
                string fileName = "";
                var request = new RestRequest(Method.GET);
                var response = MsoClient(request, certificationPageUrl);
                _htmlDocument.LoadHtml(response.Content);
                var scriptNodeXPaths = _htmlDocument.DocumentNode.SelectNodes("//script").Select(x => x.XPath).ToList();
                foreach (var xPath in scriptNodeXPaths)
                {
                    _htmlDocument.DocumentNode.SelectSingleNode(xPath).RemoveAll();
                }
                if (VerifyCertification())
                {

                    fileName = "_Certification_";
                }
                else
                {
                    fileName = "_TBC_Certification_";
                }
                var styleNode = _htmlDocument.DocumentNode.SelectSingleNode("//style");
                certificationPagePath = Path.GetTempPath() + _matterId + "_" + DateTime.Now.ToString("yyyyMMddhhmmss") + ".html";
                var certificationPdfPath = Path.GetTempPath() + _matterId + fileName + DateTime.Now.ToString("yyyyMMddhhmmss") + ".pdf";
                var style = _htmlDocument.CreateElement("style");
                var text = _htmlDocument.CreateTextNode(certificationPageCss);
                style.AppendChild(text);
                _htmlDocument.DocumentNode.AppendChild(style);
                _htmlDocument.Save(certificationPagePath);
                //certificationFilePath = Slick_Domain.Services.DocumentExport.ExportToPDF(Path.GetFileNameWithoutExtension(certificationPagePath), "testpdf", Path.GetExtension(certificationPagePath).Replace(".", ""), Path.GetDirectoryName(certificationPagePath), Path.GetDirectoryName(certificationPagePath));
                certificationFilePath = DocumentExport.ExportToPDF(Path.GetFileNameWithoutExtension(certificationPagePath), Path.GetFileNameWithoutExtension(certificationPdfPath), Path.GetExtension(certificationPagePath).Replace(".", ""), Path.GetDirectoryName(certificationPagePath), Path.GetDirectoryName(certificationPdfPath));
                int uploadedDocumentId = Slick_Domain.Helpers.DocumentHelper.UploadToSlick(_matterId, certificationPdfPath, Slick_Domain.Enums.DocumentDisplayAreaEnum.ExecutedDocs, true, false);
                using (var uow = new UnitOfWork(isolation: IsolationLevel.ReadCommitted))
                {
                    var matterCertificationQueueItem = uow.Context.MatterCertificationQueues.Where(x => x.MatterCertificationQueueId == _matterCertificationQueueId).FirstOrDefault();
                    matterCertificationQueueItem.RequestStatusTypeId = (int)Slick_Domain.Enums.CertificationRequestStatusTypeEnum.Retrieved;
                    matterCertificationQueueItem.MatterDocumentId = uploadedDocumentId;
                    matterCertificationQueueItem.UpdatedDate = DateTime.UtcNow;
                    uow.Save();
                    uow.CommitTransaction();
                }
            }
            catch (Exception e)
            {
                try
                {
                    using (var uow = new UnitOfWork(isolation: IsolationLevel.ReadUncommitted))
                    {
                        var matterCertificationQueueItem = uow.Context.MatterCertificationQueues.Where(x => x.MatterCertificationQueueId == _matterCertificationQueueId).FirstOrDefault();
                        matterCertificationQueueItem.RequestStatusTypeId = (int)Slick_Domain.Enums.CertificationRequestStatusTypeEnum.Error;
                        matterCertificationQueueItem.UpdatedDate = DateTime.UtcNow;
                        uow.Save();
                        uow.CommitTransaction();
                    }
                    {
                        EmailsService.SendSimpleNoReplyEmail(
                               new List<string>() { notifyEmail },
                               new List<string>() { },
                               "Certification: MSO Save Certification .NET issue - Matter Number: " + _matterId + "",
                               "Failed to save Certification MSO:\n Message:" + e.Message + "\nStack:" + e.StackTrace
                           );
                    }
                }
                catch (Exception f)
                {
                    {
                        EmailsService.SendSimpleNoReplyEmail(
                               new List<string>() { notifyEmail },
                               new List<string>() { },
                               "Certification: MSO Save Certification .NET issue - Matter Number: " + _matterId + "",
                               "Failed to save Certification MSO:\n Message:" + e.Message + "\nStack:" + e.StackTrace
                           );
                    }
                }
            }
            if (certificationPagePath != null) File.Delete(certificationPagePath);
            if (certificationFilePath != null) File.Delete(certificationFilePath);
        }


        public void GetTableDataForNewLoans()
        {
            string applicationPageUrl = "https://mso.macquarie.com.au/mso/prweb/MSOServlet?pyActivity=ReloadSection&pzTransactionId=&pzFromFrame=&pzPrimaryPageName=pyDisplayHarness&expandRL=false&StreamName=NewLoans&RenderSingle=EXPANDEDSubSectionNewLoansBB~pzLayout_3&StreamClass=Rule-HTML-Section&bClientValidation=true&PreActivity=pzdoGridAction&ActivityParams=gridAction%3DSORT%26gridActivity%3DpzGridSortPaginate%26PageListProperty%3DNewLoanCases.pxResults%26ClassName%3DMGL-BFS-Data-OriginationsApplicantsInfo%26RDName%3DNewLoanSolicitorWorkInfo%26RDAppliesToClass%3DMGL-BFS-Data-OriginationsApplicantsInfo%26pgRepContPage%3DNewLoanCases%26bLoadActivity%3Dfalse%26pyPageMode%3DNumeric%26RDParamsList%3DBrandName%2CSolicitorName%2CCaseID%2CCustomerName%2CBrokerName%26gridLayoutID%3DSubSectionNewLoansBB%26BaseReference%3D%26sortProperty%3D%40%40ReturnNotNullDate(.WLArrivalDateTime%2C.WBArrivalDateTime)%26sortType%3DDESC%26pyPageSize%3D600%26showSaveDiscard%3Dtrue%26isReportDef%3Dtrue%26prevSortProperty%3D%26prevSortType%3D%26customSortActivity%3D&BaseReference=&ReadOnly=0&FieldError=&FormError=&pyCustomError=&Increment=true&inStandardsMode=true&AJAXTrackID=1&pzHarnessID=" + _hidValue + "&HeaderButtonSectionName=&ClientInt=Start";
            var request = new RestRequest(Method.POST);
            #region New Loans Table Page Request Parameter
            request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
            request.AddParameter("$ODesktopWrapperInclude", "");
            request.AddParameter("$ODeterminePortalTop", "");
            request.AddParameter("$OEvalDOMScripts_Include", "");
            request.AddParameter("$OGridInc", "");
            request.AddParameter("$OHarnessStatic", "");
            request.AddParameter("$OLaunchFlow", "");
            request.AddParameter("$OListViewIncludes", "");
            request.AddParameter("$OMenuBar", "");
            request.AddParameter("$OSessionUser", "");
            request.AddParameter("$OTextInput", "");
            request.AddParameter("$OWorkformStyles", "");
            request.AddParameter("$OmenubarInclude", "");
            request.AddParameter("$OpyWorkFormStandard", "");
            request.AddParameter("$OpzDropdownLazyload", "");
            request.AddParameter("$OxmlDocumentInclude", "");
            #endregion
            var response = MsoClient(request, applicationPageUrl);
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(response.Content);
            HtmlNodeCollection nodes = htmlDoc.DocumentNode.SelectNodes("//table[@id='bodyTbl_right']//tr");
        }

        //public DataTable CreateDataTableFromHtmlTable(HtmlNodeCollection nodes)
        //{
        //    DataTable dataTable = new DataTable();
            
        //}

        //[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
        //public class HeaderName : Attribute
        //{
        //    public string name = "";
        //}

        //private class NewLoansTable
        //{
        //    [HeaderName(true, "Case ID")]
        //    public string CaseId { get; set; }
        //    public string AccountNo { get; set; }
        //    public decimal ExpectedFunds { get; set; }
        //}


        private class FundingDetailsView
        {
            public string BSB { get; set; }
            public string AccountNo { get; set; }
            public decimal ExpectedFunds { get; set; }
        }


        private bool VerifyCertification()
        {
            bool verified = true;
            string certificationDateOffWarning = "";
            // Check Settlement Date
            string dateTimeSpanValue = null, paralegalEmail = null;
            var settlementDateSpanNode = _htmlDocument.DocumentNode.SelectSingleNode("//div/span[@class='field-caption dataLabelForRead' and text() = 'Settlement Date']");

            if (settlementDateSpanNode != null)
            {
                dateTimeSpanValue = (settlementDateSpanNode.ParentNode.SelectNodes("./div[@class='field-item dataValueRead']/span").FirstOrDefault()?.InnerText);
            }
            DateTime settlementDateTime = new DateTime();
            bool settlementDateExists = (!string.IsNullOrEmpty(dateTimeSpanValue) && DateTime.TryParse(dateTimeSpanValue, out settlementDateTime));
            //var certificationDateTimeNode = _htmlDocument.DocumentNode.SelectSingleNode("//style");
            var certificationDateTimeNode = _htmlDocument.DocumentNode.SelectSingleNode("//div/span[@class='field-caption dataLabelForRead' and text() = 'Certified When']");
            if (certificationDateTimeNode != null)
            {
                dateTimeSpanValue = (certificationDateTimeNode.ParentNode.SelectNodes("./div[@class='field-item dataValueRead']/span").FirstOrDefault()?.InnerText);
            }

            DateTime certifiedDateTime = new DateTime();

            bool certifiedDateExists = (!string.IsNullOrEmpty(dateTimeSpanValue) && DateTime.TryParse(dateTimeSpanValue, out certifiedDateTime));
            // Find the Disbursement table
            var disbursementTableNodes = _htmlDocument.DocumentNode.SelectNodes("//table[@pl_prop_class = 'MGL-BFS-Data-PaymentItem' and contains(@pl_prop, '.LoanApplication.SolicitorPaymentList')]/tr");

            // Build the Disbursement table
            var disbursementTable = new DataTable("disbursementTable");

            var headers = disbursementTableNodes[0].Elements("th").Select(th => th.InnerText.Trim());
            foreach (var header in headers)
            {
                disbursementTable.Columns.Add(header);
            }

            var rows = disbursementTableNodes.Skip(1).Select(tr => tr
                .Elements("td")
                .Select(td => td.InnerText.Trim())
                .ToArray());
            foreach (var row in rows)
            {
                disbursementTable.Rows.Add(row);
            }

            // Get the trust details for that matter
            var fundingAccountDetail = new FundingDetailsView();
            decimal fundingAmount = 0;
            using (var uow = new UnitOfWork(isolation: IsolationLevel.ReadUncommitted))
            {
                var accountsRepository = uow.GetAccountsRepository();
                var trustAccount = accountsRepository.GetTrustAccountForMatter(_matterId);
                fundingAccountDetail.BSB = trustAccount.BSB.Replace(" ", "").Replace("-", "");
                fundingAccountDetail.AccountNo = trustAccount.AccountNo.Replace(" ", "").Replace("-", "");
                fundingAccountDetail.ExpectedFunds = accountsRepository.GetMatterFinFunding(_matterId, false).Where(f => f.Description.ToUpper().Contains("EXPECTED FUNDS")).Sum(x => x.Amount);
            }

            // Get the total disbursement amount
            foreach (DataRow row in disbursementTable.Rows)
            {
                if (row["BSB"].ToString().Replace(" ", "").Replace("-", "") == fundingAccountDetail.BSB.Replace(" ", "").Replace("-", "") && row["Account Number"].ToString().Trim() == fundingAccountDetail.AccountNo)
                {
                    //fundingAmount = fundingAmount + Convert.ToDecimal(row["Amount"].ToString().Replace(" ", ""));
                    fundingAmount = fundingAmount + decimal.Parse(row["Amount"].ToString(), NumberStyles.AllowCurrencySymbol | NumberStyles.Number);
                }
            }
            DateTime confirmedSettlementDate = new DateTime();
            using (var uow = new UnitOfWork(isolation: IsolationLevel.ReadUncommitted))
            {
                //var matterRepository = uow.GetMatterRepositoryInstance();
                var matterCertificationQueueItem = uow.Context.MatterCertificationQueues.Where(x => x.MatterCertificationQueueId == _matterCertificationQueueId).FirstOrDefault();
                //var test = matterRepository.
                if (fundingAmount == fundingAccountDetail.ExpectedFunds)
                {
                    matterCertificationQueueItem.AmountVerified = true;
                }
                else
                {
                    verified = false;
                }
                if (disbursementTable.Rows.Count > 0)
                {
                    matterCertificationQueueItem.AccountDetailsVerified = true;
                }
                else
                {
                    verified = false;
                }

                if (settlementDateExists)
                {
                    confirmedSettlementDate = uow.Context.Matters.Where(x => x.MatterId == _matterId).Select(x => x.SettlementSchedule.SettlementDate).FirstOrDefault();
                    matterCertificationQueueItem.ExternalSettlementDate = settlementDateTime;

                    if (settlementDateTime.Date == confirmedSettlementDate)
                    {
                        matterCertificationQueueItem.SettlementDateVerified = true;
                    }
                    else
                    {
                        verified = false;
                    }
                }
                else
                {
                    verified = false;
                }
                if (certifiedDateExists)
                {
                    var matterState = uow.Context.Matters.Where(x => x.MatterId == _matterId).Select(x => x.InstructionStateId).FirstOrDefault();
                    matterCertificationQueueItem.ExternalCertificationDate = certifiedDateTime;
                    if (settlementDateTime.Date == confirmedSettlementDate)
                    {
                        var certificationSafeDate = Slick_Domain.Common.TimeZoneHelper.AddBusinessDays(matterCertificationQueueItem.RequestDate, 2, (int)matterState, uow.Context);
                        if (certifiedDateTime <= certificationSafeDate)
                        {
                            matterCertificationQueueItem.CertificationDateVerified = true;
                        }
                        else
                        {
                            verified = false;
                            certificationDateOffWarning = "*** WARNING: SETTLEMENT DATE MORE THAN 2 BUSINESS DAYS *** ";
                        }

                    }
                }
                else
                {
                    verified = false;
                }



                matterCertificationQueueItem.DisbursementAmount = fundingAmount;

                paralegalEmail = uow.Context.Matters.Where(x => x.MatterId == _matterId).Select(x => x.User.Email).FirstOrDefault();

                uow.Save();
                uow.CommitTransaction();


            }
            if (!verified)
            {

                EmailsService.SendSimpleNoReplyEmail(
                        new List<string>() { paralegalEmail },
                        new List<string>() { GlobalVars.GetGlobalTxtVar("CertificationCCEmail")  },
                        "Certification: Possible Mismatching Data issue with Certification OR the matter was preemptively certified - Matter Number: " + _matterId + "",
                           certificationDateOffWarning + "MQ Certification Date: - (" + (certifiedDateExists ? certifiedDateTime.ToString() : "NONE") + ")<br /><br />"
                           + "MQ Settlement Date: - (" + (settlementDateExists ? settlementDateTime.ToString() : "NONE") + ")<br />"
                           + "MSA Settlement Date: - (" + (confirmedSettlementDate != null ? confirmedSettlementDate.ToString() : "NONE") + ")<br /><br />"
                           + "MQ Funding Amount: " + fundingAmount + "<br />"
                           + "MSA Expected Funds: " + fundingAccountDetail.ExpectedFunds
                    );
            }

            return verified;
        }



        private IRestResponse MsoClient(RestRequest request, string url)
        {
            var client = new RestClient(url);
            client.Timeout = -1;
            client.CookieContainer = _cookieContainer;
            return client.Execute(request);
        }




    }
}
