using System;
using RestSharp;
using Slick_Domain.Handlers;
using Slick_Domain.Common;
using System.IO;
using System.Xml;
using System.Linq;
using System.Collections.Generic;
using Slick_Domain.Models;
using System.Net;
using System.Web.Script.Serialization;
using Slick_Domain.Entities.InfoTrack;
using Slick_Domain.Entities;
using System.Text.RegularExpressions;


namespace Slick_Domain.Services
{
    public class InfoTrackService
    {

        static private CookieContainer cookieContainer = new CookieContainer();
        static private string InfoTrackBaseUrl = GlobalVars.GetGlobalTxtVar("InfoTrackUrl");
        static private string InfoTrackApiUrl = GlobalVars.GetGlobalTxtVar("InfoTrackApiBaseUrl");
        static private string InfoTrackOldUrl = "https://api.infotrack.com.au/";

        /// <summary>
        /// Sets up the Login Client for InfoTrack; Also creates space in the system for cookies; note - cookies are referenced through the client
        /// </summary>
        /// <returns></returns>
        private static RestClient LoginClient()
        {
            string loginUrl = "Account/Login";
            RestClient client = new RestClient(InfoTrackBaseUrl + loginUrl);
            client.CookieContainer = new CookieContainer();
            SecurityObject securityObject = SlickSecurity.GetInfoTrackSecurityObject();

            IRestRequest request = new RestRequest(InfoTrackBaseUrl + loginUrl, Method.POST);
            request.RequestFormat = DataFormat.Json;
            request.AddJsonBody(new { Username = securityObject.UserName, Password = securityObject.Pwd });
            //request.AddParameter("Username", securityObject.UserName);
            //request.AddParameter("Password", securityObject.Pwd);
            client.Execute(request);
            return client;
        }

        /// <summary>
        /// Sets up the request for the API Request using cookies from the client
        /// </summary>
        /// <param name="client">client to reference the cookies</param>
        /// <param name="method">API Method</param>
        /// <param name="apiUri">the API path excluding the base API</param>
        /// <returns>request</returns>
        private static RestRequest SetUpRequestWithCookie(RestClient client, Method method, string apiUri)
        {
            // REQUEST SET UP
            RestRequest request = new RestRequest(InfoTrackBaseUrl + apiUri, method);
            request.RequestFormat = DataFormat.Json;

            // GET COOKIES FOR THE API FROM THE CLIENT
            CookieCollection clientCookies = client.CookieContainer.GetCookies(new Uri(InfoTrackBaseUrl));
            foreach (Cookie cookie in clientCookies)
                request.AddCookie(cookie.Name, cookie.Value);

            return request;

        }


        public static IRestRequest SetupInfoTrackApiRequest(string fullApiUrl, Method method)
        {
            RestRequest request = new RestRequest(fullApiUrl, method);
            SecurityObject securityObject = SlickSecurity.GetInfoTrackSecurityObject();
            request.AddHeader("Authorization", "Basic " + Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{securityObject.UserName}:{securityObject.Pwd}")));

            return request;
        }

        public static TitleCustomEntities.InfoTrackGetOrderResponse GetTitleData(int orderId)
        {
            
            string apiUrl = "data/DataOrder/getorder/" + orderId;
            RestClient client = new RestClient(InfoTrackApiUrl + apiUrl);
            IRestRequest request = SetupInfoTrackApiRequest(InfoTrackApiUrl + apiUrl, Method.GET);
            
            IRestResponse response = client.Execute(request);
            JavaScriptSerializer jsrz = new JavaScriptSerializer();
            //TitleCustomEntities.InfoTrackGetOrderResponse results = jsrz.Deserialize<TitleCustomEntities.InfoTrackGetOrderResponse>(ReplaceFirstX(response.Content, "\"NIL\"","",4));
            TitleCustomEntities.InfoTrackGetOrderResponse  results = jsrz.Deserialize<TitleCustomEntities.InfoTrackGetOrderResponse>(response.Content.Replace("\"NIL\"", ""));
            return results;
        }


        public static TitleCustomEntities.InfoTrackPostOrderResponse OrderTitleData(TitleCustomEntities.InfoTrackOrderRequestBody order)
        {
            
            string apiUrl = "data/DataOrder/titlesearch";
            RestClient client = new RestClient(InfoTrackApiUrl + apiUrl);
            IRestRequest request = SetupInfoTrackApiRequest(InfoTrackApiUrl + apiUrl, Method.POST);
            request.AddJsonBody(order);
            IRestResponse response = client.Execute(request);
            JavaScriptSerializer jsrz = new JavaScriptSerializer();
            TitleCustomEntities.InfoTrackPostOrderResponse results = jsrz.Deserialize<TitleCustomEntities.InfoTrackPostOrderResponse>(response.Content);
            return results;
        }

        //private static string ReplaceFirstX(string text, string search, string replace, int repeat)
        //{
        //    int pos = text.IndexOf(search);
        //    if (pos < 0)
        //    {
        //        return text;
        //    }
        //    text = text.Substring(0, pos) + replace + text.Substring(pos + search.Length);
        //    if (repeat < 1)
        //    {
        //        return text;
        //    }
        //    else
        //    {
        //        repeat--;
        //        return ReplaceFirstX(text, search, replace, repeat);
        //    }

        //}

        public static byte[] DownloadTitlePDF(int ldmOrderId)
        {
            try
            {
                // SET UP CLIENT
                // API SET UP INC COOKIES
                string apiUrl = "v1/order/download/"+ ldmOrderId;

                RestClient client = new RestClient(InfoTrackOldUrl + apiUrl);
                IRestRequest request = SetupInfoTrackApiRequest(InfoTrackOldUrl + apiUrl, Method.GET);

                // SET UP RESPONSE + CONVERT TO A PRE-DEFINED CLASS
                IRestResponse response = client.Execute(request);
                return response.RawBytes;

            }
            catch (Exception ex)
            {
                ErrorHandler.LogError(ex);
                return null;
            }

        }

        /// <summary>
        /// Verify the Address and the Vendor for NSW Files
        /// </summary>
        /// <param name="titleRef">Title Reference to search for</param>
        /// <returns></returns>
        /// 
        public static List<TitleCustomEntities.TitlePreviewNSW> VerifyNswTitle(string titleRef)
        {
            List<TitleCustomEntities.TitlePreviewNSW> titleList = null;
            try
            {
                // SET UP CLIENT
                RestClient client = LoginClient();

                // API SET UP INC COOKIES
                string apiUrl = "Nsw/Titles/BulkVerifyTitle";

                RestRequest request = SetUpRequestWithCookie(client, Method.POST, apiUrl);

                // SET UP BODY
                request.AddJsonBody(new[] { new { Index = "0", TitleReference = titleRef } });

                // SET UP RESPONSE + CONVERT TO A PRE-DEFINED CLASS
                IRestResponse response = client.Execute(request);
                JavaScriptSerializer jsrz = new JavaScriptSerializer();
                titleList = jsrz.Deserialize<List<TitleCustomEntities.TitlePreviewNSW>>(response.Content);

            }
            catch (Exception ex)
            {
                ErrorHandler.LogError(ex);
            }

            return titleList;

        }

        /// <summary>
        /// Verify the Address and the Vendor for QLD Files
        /// </summary>
        /// <param name="titleRef">Title Reference to search for</param>
        /// <returns></returns>
        public static List<TitleCustomEntities.TitlePreviewQLD> VerifyQldTitle(string titleRef)
        {
            List<TitleCustomEntities.TitlePreviewQLD> titleList = null;

            // INFOTRACK
            try
            {
                // SET UP CLIENT
                RestClient client = LoginClient();

                // API SET UP INC COOKIES
                string apiUrl = "Queensland/Titles/VerifyTitle";

                RestRequest request = SetUpRequestWithCookie(client, Method.POST, apiUrl);

                // SET UP BODY
                request.AddJsonBody(new[] { new { Index = "0", TitleReference = titleRef } });
  
                // GET RESPONSE + CONVERT TO A PRE-DEFINED CLASS
                IRestResponse response = client.Execute(request);
                JavaScriptSerializer jsrz = new JavaScriptSerializer();
                titleList = jsrz.Deserialize<List<TitleCustomEntities.TitlePreviewQLD>>(response.Content);
                
                // QLD has a separate API for Obtaining the Address
                apiUrl = "QldEnquiries/AddressLookUp/AddressQuery";
                string lotNumber, plan;
                
                foreach (TitleCustomEntities.TitlePreviewQLD title in titleList)
                {
                    title.TitleReference = title.TitleReference.Replace(" ", "").Replace("/", "").Replace("\\", "");
                    if (Regex.Matches(title.TitleReference, @"[a-zA-Z]").Count > 0)
                    {
                        client = LoginClient();
                        request = SetUpRequestWithCookie(client, Method.POST, apiUrl);
                        lotNumber = new string(title.TitleReference.TakeWhile(c => !Char.IsLetter(c)).ToArray());
                        plan = title.TitleReference.Substring(title.TitleReference.IndexOf(lotNumber) + lotNumber.Length, title.TitleReference.Length - lotNumber.Length);
                        request.AddJsonBody(new { LotNumber = lotNumber, Plan = plan });
                        response = client.Execute(request);
                        if (!(response.StatusCode == HttpStatusCode.InternalServerError))
                        {
                            title.Address = response.Content;
                        }
                        else {
                            title.Address = "";
                        }
                        
                    }
                }
             
            }
            catch (Exception ex)
            {
                ErrorHandler.LogError(ex);
            }

            return titleList;

        }



        private static Dictionary<string, Action<string>> _infoTrackActions;
        private static MapModel _mapModel = null;
        private static List<int> _matterIds = null;
        private static List<InfoTrackDownload> _infoTrackDownloads = null;

        public static string LaunchTitleSearchForMatter(int matterId)
        {
            try
            {
                var matterDetail = new InfoTrackRestClient.MatterDetail { ClientReference = matterId.ToString(), RetailerReference = 2.ToString() };

                var client = new RestClient(GlobalVars.GetGlobalTxtVar(DomainConstants.InfoTrackUrl));
                var securityObject = SlickSecurity.GetInfoTrackSecurityObject();
                client.Authenticator = new RestSharp.Authenticators.HttpBasicAuthenticator(securityObject.UserName, securityObject.Pwd);

                var request = new RestRequest(GlobalVars.GetGlobalTxtVar(DomainConstants.InfoTrackResourceUrl), Method.POST);
                request.RequestFormat = DataFormat.Json;
                request.AddJsonBody(matterDetail);

                var response = client.Execute<InfoTrackRestClient.Mapping>(request);
                if (response.Data.Url == null)
                {
                    throw new Exception($"InfoTrack Exception: -{response.StatusDescription} {response.Content}");
                }
                return response.Data.Url;
            }
            catch (Exception ex)
            {
                ErrorHandler.LogError(ex);
            }

            return null;
        }

        /// <summary>
        /// This will process all documents - but only download the latest and Save for that Matter if set.
        /// </summary>
        /// <param name="matterId"></param>
        public void ProcessLatestItems(int? matterId = null)
        {
            string fileName = string.Empty;
            try
            {
                var dropFolder = GlobalVars.GetGlobalTxtVar(DomainConstants.InfoTrackDropFolder);
                var processedFolder = GlobalVars.GetGlobalTxtVar(DomainConstants.InfoTrackProcessedFolder);
                SetUpMappingActions();
                DeleteOldProcessedFiles(processedFolder);
                _matterIds = new List<int>();
                foreach (var file in Directory.GetFiles(dropFolder))
                {
                    fileName = file;
                    var doc = new XmlDocument();
                    doc.LoadXml(File.ReadAllText(file));
                    if(SaveXMLToInfoTrack(doc))
                    {
                        File.Move(file, Path.Combine(processedFolder, Path.GetFileName(file)));
                        if(_mapModel.MatterId == matterId || !matterId.HasValue) _matterIds.Add(_mapModel.MatterId);
                    }
                }
                if(matterId.HasValue && !_matterIds.Any(x=> x==matterId.Value))
                {
                    _matterIds.Add(matterId.Value);
                }
                DownloadLatestDocuments();
                SaveLatestDocuments();
            }
            catch (Exception ex)
            {
                ErrorHandler.LogError(ex,$"Error Processing InfoTrack file {fileName}");
            }
        }

        private void SetUpMappingActions()
        {
            _infoTrackActions = new Dictionary<string, Action<string>>();
            _infoTrackActions.Add(DomainConstants.InfoTrackMapClientReference, (x) => _mapModel.MatterId = int.Parse(x));
            _infoTrackActions.Add(DomainConstants.InfoTrackMapDateOrdered, (x) => _mapModel.DateOrdered = x.ToFormattedDate().Value);
            _infoTrackActions.Add(DomainConstants.InfoTrackMapDateCompleted, (x) => _mapModel.DateCompleted = x.ToFormattedDate().Value);
            _infoTrackActions.Add(DomainConstants.InfoTrackMapDescription, (x) => _mapModel.Description = x);
            _infoTrackActions.Add(DomainConstants.InfoTrackMapOrderId, (x) => _mapModel.OrderId = int.Parse(x));
            _infoTrackActions.Add(DomainConstants.InfoTrackMapRetailerRef, (x) => _mapModel.OrderedByUserId = int.Parse(x));
            _infoTrackActions.Add(DomainConstants.InfoTrackMapTotalFee, (x) => _mapModel.TotalFee = decimal.Parse(x));
            _infoTrackActions.Add(DomainConstants.InfoTrackMapTotalFeeGST, (x) => _mapModel.TotalFeeGst = decimal.Parse(x));
            _infoTrackActions.Add(DomainConstants.InfoTrackMapServiceName, (x) => _mapModel.ServiceName = x);
            _infoTrackActions.Add(DomainConstants.InfoTrackMapStatus, (x) => _mapModel.Status = x);
            _infoTrackActions.Add(DomainConstants.InfoTrackMapDownloadUrl, (x) => _mapModel.DownloadUrl = x);
            _infoTrackActions.Add(DomainConstants.InfoTrackMapIsBillable, (x) => _mapModel.IsBillable = x == "true" ? true : false);
        }

        /// <summary>
        /// Deletes old files greater than specified number of days old
        /// </summary>
        private void DeleteOldProcessedFiles(string processedFolder)
        {
            int days = int.Parse(GlobalVars.GetGlobalTxtVar("InfoTrackDaysToDeleteProcessedAfter"));
                
            var dir = new DirectoryInfo(processedFolder);
            foreach (var file in dir.GetFiles().Where(x=>x.LastWriteTime >= DateTime.Now.AddDays(days)))
            {
                File.Delete(file.FullName);
            }
        }

        private bool SaveXMLToInfoTrack(XmlDocument doc)
        {
            try
            {
                _mapModel = new MapModel();
                
                var orderNode = doc.SelectSingleNode("/Order");
                foreach (XmlNode node in orderNode.ChildNodes)
                {
                    if (node == null || !_infoTrackActions.ContainsKey(node.Name)) continue;

                    var action = _infoTrackActions.First(x => x.Key == node.Name);
                    var executeAction = action.Value;
                    executeAction(node.InnerText);
                }
                SaveModelToDatabase();
            }
            catch(Exception ex)
            {
                ErrorHandler.LogError(ex);
                return false;
            }

            return true;
        }

        private void SaveModelToDatabase()
        {
            using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadCommitted))
            {
                var rep = new Repository<MatterInfoTrack>(uow.Context);
                rep.Add(new MatterInfoTrack
                {
                    DateCompleted = _mapModel.DateCompleted,
                    DateOrdered = _mapModel.DateOrdered,
                    Description = _mapModel.Description,
                    DownloadURL = string.IsNullOrEmpty(_mapModel.DownloadUrl) ? null : _mapModel.DownloadUrl,
                    IsBillable = _mapModel.IsBillable,
                    MatterId = _mapModel.MatterId,
                    OrderedByUserId = _mapModel.OrderedByUserId,
                    OrderId = _mapModel.OrderId,
                    ServiceName = _mapModel.ServiceName,
                    Status = _mapModel.Status,
                    TotalFee = _mapModel.TotalFee,
                    TotalFeeGST = _mapModel.TotalFeeGst,
                    UpdatedDate = DateTime.Now
                });
                uow.Save();
                uow.CommitTransaction();
            }
        }

        private void DownloadLatestDocuments()
        {            
            if (_infoTrackDownloads == null)
                _infoTrackDownloads = new List<InfoTrackDownload>();
            else
                _infoTrackDownloads.Clear();

            var securityObject = SlickSecurity.GetInfoTrackSecurityObject();
            foreach (var matterId in _matterIds)
            {
                DownloadLatestDocuments(matterId,securityObject.UserName,securityObject.Pwd);
            }            
        }

        private void DownloadLatestDocuments(int matterId, string user, string pwd)
        {
            using (var uow = new UnitOfWork())
            {
                _infoTrackDownloads.AddRange(uow.GetMatterRepositoryInstance().GetDownloadUrlsForMatter(matterId));
            }
                
            foreach (var infotrackDownload in _infoTrackDownloads.Where(x=> x.MatterId == matterId && !x.IsDownloaded))
            {
                DownloadFile(user, pwd, infotrackDownload);
                infotrackDownload.IsDownloaded = true;
            }
        }

        private void DownloadFile(string user, string pwd, InfoTrackDownload infoTrackDownload)
        {
            using (WebClient client = new WebClient())
            {
                client.Credentials = new NetworkCredential(user, pwd);
                client.DownloadFile(infoTrackDownload.DownloadUrl, 
                    $"{Path.Combine(CommonMethods.GetInfoTrackDocPath(infoTrackDownload.MatterId, infoTrackDownload.InfoTrackId),infoTrackDownload.FileName.ToSafeFileName())}.pdf");
            }  
        }

        private void SaveLatestDocuments()
        {
            foreach(var infoTrackDownload in _infoTrackDownloads.Where(x=>x.IsDownloaded).OrderBy(o=>o.MatterId))
            {
                var docs = GetDocuments(infoTrackDownload.MatterId);
                if(docs != null)
                {
                    foreach(var doc in docs)
                    {
                        var currDoc = doc;
                        currDoc.RelatedId = infoTrackDownload.InfoTrackId;
                        currDoc.OriginalName = infoTrackDownload.FileName.ToSafeFileName();
                        SaveDocument(ref currDoc);
                        var path = CommonMethods.GetInfoTrackDocPath(infoTrackDownload.MatterId, infoTrackDownload.InfoTrackId);
                        File.Move(Path.Combine(path, $"{doc.OriginalName}.{doc.FileType}"), CommonMethods.GetDocPath(infoTrackDownload.MatterId, doc.ID.Value, doc.FileType));
                    }
                    DeleteInfoTrackDirectory(infoTrackDownload.MatterId);
                }
            }
        }

        private void DeleteInfoTrackDirectory(int matterId)
        {
            try
            {
                var path = CommonMethods.GetInfoTrackDocPath(matterId, createDirectory: false);
                if (Directory.Exists(path)) Directory.Delete(path, true);
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("The directory is not empty"))
                {
                    // Ignore
                }
                else
                {
                    throw;
                }
            }
        }

        private void SaveDocument(ref DocumentInfo doc)
        {
            using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadCommitted))
            {
                try
                {
                    var currDoc = doc;
                   
                    MatterDocument matterDoc = null;
                    if(uow.GetRepositoryInstance<MatterDocument>().AllAsQueryNoTracking.Any(x=>x.MatterId == currDoc.MatterId && x.DocumentMaster.DocName == currDoc.FileName))
                    {
                        currDoc.FileName = $"{currDoc.FileName}_{DateTime.Now.ToShortDateString()}_{DateTime.Now.ToShortTimeString()}".ToSafeFileName();
                    }

                    doc.DocumentDisplayAreaType = Enums.DocumentDisplayAreaEnum.Attachments;
                    if(uow.GetDocumentsRepositoryInstance().SaveDocument(ref doc,ref matterDoc))
                    {
                        var rep = uow.GetRepositoryInstance<MatterInfoTrack>();
                        var itrack = rep.FindById(doc.RelatedId.Value);
                        itrack.DocumentId = doc.ID;
                        rep.Update(itrack);

                        uow.CommitTransaction();
                    }
                }
                catch (Exception e)
                {
                    if (uow != null && uow.Context.Database.CurrentTransaction != null) uow.RollbackTransaction();
                    ErrorHandler.LogError(e);
                }
            }
        }


        
        public static string GetFileType(string input)
        {
            if (!input.Contains('.'))
            {
                return null;
            }
            else
            {
                return input.Substring(input.LastIndexOf('.') + 1, input.Length - input.LastIndexOf('.') - 1);
            }
        }

        private IEnumerable<DocumentInfo> GetDocuments(int matterId)
        {
            var docs = new List<DocumentInfo>();
            var dir = CommonMethods.GetInfoTrackDocPath(matterId, createDirectory: false);

            DirectoryInfo dirInfo = new DirectoryInfo(dir);

            foreach (var subDir in dirInfo.GetDirectories())
            {
                foreach (var file in subDir.GetFiles().Where(x => !x.Name.Contains("~"))) // ignore temp files
                {
                    var docInfo = new DocumentInfo();
                    var periodPos = file.Name.LastIndexOf(".");
                    var filetype = file.Name.Substring(periodPos + 1);
                    var name = file.Name.Substring(0, file.Name.Length - 1 - filetype.Length);
                    
                    int pos = file.Name.LastIndexOf('.');
                    docInfo.MatterId = matterId;
                    docInfo.FileType = filetype;
                    docInfo.FileName = name;
                    docInfo.ModDate = file.LastWriteTime;
                    docs.Add(docInfo);
                }
            }
  
            return docs;
        }
    }
}
