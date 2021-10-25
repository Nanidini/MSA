using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using Meziantou.Framework.Win32;
using Microsoft.Exchange.WebServices.Data;
using System.IO;
namespace Slick_Domain.Services
{
    public class ExchangeWebService
    {   
        public ExchangeService _service = new ExchangeService(ExchangeVersion.Exchange2013_SP1, TimeZoneInfo.Utc);
        public DateTime? AuthenticatedDate = null;
        private DateTime? _LockedUntilDate;
        private List<DateTime> _SearchExecutionTimes = new List<DateTime>();

        private int _EWSSearchLimit = 0;
        private int _EWSSearchLimitTimeFrameMinutes = 0;
        private int _EWSSearchWaitTimeMinutes = 0;


        public ExchangeWebService()
        {
            AuthenticateExchangeService();
        }
        public ExchangeWebService(string username, string password)
        {
            AuthenticateExchangeService(username, password);
        }
        
        public ExchangeWebService(bool authenticateCurrentUser)
        {
            if (authenticateCurrentUser)
            {
                using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadUncommitted))
                {
                    _EWSSearchLimit = Int32.Parse(GlobalVars.GetGlobalTxtVar("EWSSearchLimit", uow.Context));
                    _EWSSearchLimitTimeFrameMinutes = Int32.Parse(GlobalVars.GetGlobalTxtVar("EWSSearchLimitTimeFrameMinutes", uow.Context));
                    _EWSSearchWaitTimeMinutes = Int32.Parse(GlobalVars.GetGlobalTxtVar("EWSSearchWaitTimeMinutes", uow.Context)); ;
                }
                var cred = CredentialManager.ReadCredential(GlobalVars.CurrentUser.CredentialStore);
              
                if (cred == null || cred.Password == null)
                {
                    var credStore = $"MicrosoftOffice15_Data:SSPI:{Environment.UserName}@msanational.com.au";

                    cred = CredentialManager.ReadCredential(credStore);
                    if (cred != null && cred.Password != null)
                    {
                        CredentialManager.DeleteCredential("SLICK");
                        CredentialManager.WriteCredential("SLICK", Environment.UserName, cred.Password, CredentialPersistence.Enterprise);

                    }
                    else
                    {
                        throw (new Exception($"No credentials found to send SMTP email - looking for credential: {GlobalVars.CurrentUser.CredentialStore}"));
                    }
                }

                if (cred != null)
                {
                    AuthenticateExchangeService(GlobalVars.CurrentUser.Email ?? GlobalVars.CurrentUser.Username + "@msanational.com.au", cred.Password);
                }

            }
            else
            {
                AuthenticateExchangeService();
            }
        }

        internal void AuthenticateExchangeService(string username, string password)
        {
            if(_service == null)
            {
                _service = new ExchangeService(ExchangeVersion.Exchange2013_SP1, TimeZoneInfo.Utc);
            }
            _service.Credentials = new NetworkCredential(username, password);
            try
            {
                // Use Autodiscover to set the URL endpoint.
                // and using a AutodiscoverRedirectionUrlValidationCallback in case of https enabled clod account
                _service.AutodiscoverUrl(username, SslRedirectionCallback);
            }
            catch (Exception ex)
            {
                _service = null;
            }
            AuthenticatedDate = DateTime.Now;
        }
        //authenticate with windows auth instead
        internal void AuthenticateExchangeService()
        {
            try
            {
                AuthenticatedDate = DateTime.Now;
                //_service.UseDefaultCredentials = true;
                _service.UseDefaultCredentials = true;
                _service.Url = new Uri("https://IP/EWS/Exchange.asmx");

            }
            catch (Exception ex)
            {
                _service = null;

            }
        }
        bool SslRedirectionCallback(string serviceUrl)
        {
            // Return true if the URL is an HTTPS URL.
            return serviceUrl.ToLower().StartsWith("https://");
        }


        /// <summary>
        /// Base get emails function, filters based on parameters
        /// </summary>
        /// <param name="startDatetime">Optional start time parameter</param>
        /// <param name="endDatetime">Optional end time parameter</param>
        /// <returns>A list of EmailMessages</returns>
        public IEnumerable<EmailMessage> GetEmails(DateTime? startDatetime=null, DateTime? endDatetime=null)
        {
            List<EmailMessage> mailList = new List<EmailMessage>();
            ItemView iv = new ItemView(100, 0);
            PropertySet itItemPropSet = new PropertySet(BasePropertySet.IdOnly) { ItemSchema.MimeContent, ItemSchema.Subject, EmailMessageSchema.From };
            FindItemsResults<Item> findResults;

            SearchFilter.SearchFilterCollection filter = new SearchFilter.SearchFilterCollection();
            if (startDatetime.HasValue && endDatetime.HasValue)
                filter = new SearchFilter.SearchFilterCollection(LogicalOperator.And, new SearchFilter.IsGreaterThanOrEqualTo(ItemSchema.DateTimeReceived, startDatetime.Value), new SearchFilter.IsLessThanOrEqualTo(ItemSchema.DateTimeReceived, endDatetime.Value));
            else if (startDatetime.HasValue)
                filter.Add(new SearchFilter.IsGreaterThanOrEqualTo(ItemSchema.DateTimeReceived, startDatetime.Value));
            else if (endDatetime.HasValue)
                filter.Add(new SearchFilter.IsLessThanOrEqualTo(ItemSchema.DateTimeReceived, endDatetime.Value));

            Folder folder = Folder.Bind(_service, WellKnownFolderName.Inbox);
            findResults = folder.FindItems(filter,iv);
            do
            {
                var findresults = folder.FindItems(filter, iv);
                _service.LoadPropertiesForItems(findresults.Items, itItemPropSet);
                mailList.Concat(findResults.Select(x => x as EmailMessage));
               
            }
            while (findResults.MoreAvailable);
            return mailList;
        }
        /// <summary>
        /// Override function for base get emails function, returns the filtered collection that contains the <paramref name="substring"/> based on a switch statement.
        /// </summary>
        /// <param name="substring">The substring to filter by</param>
        /// <param name="startDatetime">The optional Start Date parameter</param>
        /// <param name="endDatetime">The optional End Date parameter</param>
        /// <returns>A list of EmailMessages from the inbox</returns>
        public IEnumerable<EmailMessage> GetEmails(string substring, DateTime? startDatetime = null, DateTime? endDatetime = null)
        {
            List<EmailMessage> mailList = new List<EmailMessage>();
            ItemView iv = new ItemView(100, 0);
            PropertySet itItemPropSet = new PropertySet(BasePropertySet.IdOnly) { ItemSchema.MimeContent, ItemSchema.Subject, EmailMessageSchema.From };
            FindItemsResults<Item> findResults;
            SearchFilter.SearchFilterCollection filter = new SearchFilter.SearchFilterCollection();

            if (startDatetime.HasValue && endDatetime.HasValue)
                filter = new SearchFilter.SearchFilterCollection(LogicalOperator.And, new SearchFilter.IsGreaterThanOrEqualTo(ItemSchema.DateTimeReceived, startDatetime.Value), new SearchFilter.IsLessThanOrEqualTo(ItemSchema.DateTimeReceived, endDatetime.Value), new SearchFilter.ContainsSubstring());
            else if (startDatetime.HasValue)
                filter.Add(new SearchFilter.IsGreaterThanOrEqualTo(ItemSchema.DateTimeReceived, startDatetime.Value));
            else if (endDatetime.HasValue)
                filter.Add(new SearchFilter.IsLessThanOrEqualTo(ItemSchema.DateTimeReceived, endDatetime.Value));

            switch (substring)
            {
                case string s when s.Contains("@"):
                    filter.Add(new SearchFilter.ContainsSubstring(EmailMessageSchema.From, substring));
                    filter.Add(new SearchFilter.ContainsSubstring(EmailMessageSchema.Sender, substring));
                    break;
                default:
                    filter.Add(new SearchFilter.ContainsSubstring(EmailMessageSchema.Subject, substring));
                    filter.Add(new SearchFilter.ContainsSubstring(EmailMessageSchema.TextBody, substring));
                    break;
            }

            Folder folder = Folder.Bind(_service, WellKnownFolderName.Inbox);
            findResults = folder.FindItems(filter, iv);
            do
            {
                var findresults = folder.FindItems(filter, iv);
                _service.LoadPropertiesForItems(findresults.Items, itItemPropSet);
                mailList.Concat(findResults.Select(x => x as EmailMessage));

            }
            while (findResults.MoreAvailable);
            return mailList;
        }
        public Folder GetAllItemsFolder()
        {
            ExtendedPropertyDefinition allFoldersType = new ExtendedPropertyDefinition(13825, MapiPropertyType.Integer);

            FolderId rootFolderId = new FolderId(WellKnownFolderName.Root);
            FolderView folderView = new FolderView(1000);
            folderView.Traversal = FolderTraversal.Shallow;

            SearchFilter searchFilter1 = new SearchFilter.IsEqualTo(allFoldersType, "2");
            SearchFilter searchFilter2 = new SearchFilter.IsEqualTo(FolderSchema.DisplayName, "allitems");

            SearchFilter.SearchFilterCollection searchFilterCollection =
                new SearchFilter.SearchFilterCollection(LogicalOperator.And);
            searchFilterCollection.Add(searchFilter1);
            searchFilterCollection.Add(searchFilter2);

            FindFoldersResults findFoldersResults =
                _service.FindFolders(rootFolderId, searchFilterCollection, folderView);

            return findFoldersResults.FirstOrDefault();

        }

        public List<Folder> GetFolders()
        {
            try
            {
                ExtendedPropertyDefinition isHiddenProp = new ExtendedPropertyDefinition(0x10f4, MapiPropertyType.Boolean);

                var newResult = new List<Folder>();

                //var rootFolder = Folder.Bind(_service, WellKnownFolderName.AllItems, new PropertySet(BasePropertySet.IdOnly)
                //        {
                //        FolderSchema.Id,
                //        FolderSchema.DisplayName,
                //        FolderSchema.UnreadCount,
                //        FolderSchema.TotalCount,
                //        FolderSchema.WellKnownFolderName
                //        });

                var rootFolder = GetAllItemsFolder();

                if (rootFolder != null)
                {
                    newResult.Add(rootFolder);
                }

                var res = _service.FindFolders(WellKnownFolderName.MsgFolderRoot,
                    new SearchFilter.IsEqualTo(isHiddenProp, false), new FolderView(500)
                    {
                        PropertySet = new PropertySet(BasePropertySet.IdOnly)
                        {
                        FolderSchema.Id,
                        FolderSchema.DisplayName,
                        FolderSchema.UnreadCount,
                        FolderSchema.TotalCount,
                        }
                    })
                    .Where(f => f is Folder && !(f is CalendarFolder || f is TasksFolder || f is ContactsFolder) && f.TotalCount > 0)
                    .Select(f => f).ToList();

                foreach (var foundFolder in res)
                {
                    try
                    {
                        var fullDetailFolder = Folder.Bind(_service, foundFolder.Id, new PropertySet(BasePropertySet.IdOnly)
                        {
                        FolderSchema.Id,
                        FolderSchema.DisplayName,
                        FolderSchema.UnreadCount,
                        FolderSchema.TotalCount,
                        FolderSchema.WellKnownFolderName
                        });
                        newResult.Add(fullDetailFolder);
                    }
                    catch(Exception)
                    {
                        newResult.Add(foundFolder); //the wellknownfoldername enum is out of date so certain new default folders don't work i.e. clutter
                    }
                }

                return newResult
                    .OrderByDescending(f=>f.DisplayName.ToUpper() == "ALLITEMS")
                    .ThenByDescending(f => f.WellKnownFolderName.HasValue)
                    .ThenByDescending(f => f.WellKnownFolderName == WellKnownFolderName.MsgFolderRoot)
                    .ThenByDescending(f=>f.WellKnownFolderName == WellKnownFolderName.Inbox)
                    .ThenByDescending(f=>f.WellKnownFolderName == WellKnownFolderName.SentItems)
                    .ThenByDescending(f => f.WellKnownFolderName == WellKnownFolderName.DeletedItems).ToList();

            }
            catch(Exception e)
            {
                return new List<Folder>();
            }
        }
      
        /// <summary>
        /// 
        /// </summary>
        /// <param name="predicates"></param>
        /// <param name="startDatetime"></param>
        /// <param name="endDatetime"></param>
        /// <returns></returns>
        public IEnumerable<EmailMessage> GetEmails(List<Predicate<EmailMessage>> predicates, DateTime? startDatetime = null, DateTime? endDatetime = null)
        {
            List<EmailMessage> mailList = new List<EmailMessage>();
            ItemView iv = new ItemView(100, 0);
            PropertySet itItemPropSet = new PropertySet(BasePropertySet.IdOnly) { ItemSchema.MimeContent, ItemSchema.Subject, EmailMessageSchema.From };
            FindItemsResults<Item> findResults;
            SearchFilter.SearchFilterCollection filter = new SearchFilter.SearchFilterCollection();
            if (startDatetime.HasValue && endDatetime.HasValue)
                filter = new SearchFilter.SearchFilterCollection(LogicalOperator.And, new SearchFilter.IsGreaterThanOrEqualTo(ItemSchema.DateTimeReceived, startDatetime.Value), new SearchFilter.IsLessThanOrEqualTo(ItemSchema.DateTimeReceived, endDatetime.Value));
            else if (startDatetime.HasValue)
                filter.Add(new SearchFilter.IsGreaterThanOrEqualTo(ItemSchema.DateTimeReceived, startDatetime.Value));
            else if (endDatetime.HasValue)
                filter.Add(new SearchFilter.IsLessThanOrEqualTo(ItemSchema.DateTimeReceived, endDatetime.Value));
            Folder folder = Folder.Bind(_service, WellKnownFolderName.Inbox);
            findResults = folder.FindItems(filter, iv);
            do
            {
                var findresults = folder.FindItems(filter, iv);
                _service.LoadPropertiesForItems(findresults.Items, itItemPropSet);
                mailList.Concat(findResults.Select(x => x as EmailMessage));

            }
            while (findResults.MoreAvailable);
            return mailList;
        }
        public IEnumerable<string> GetAttachments(EmailMessage message)
        {

            return null;
        }
        public void SendEmail(string toAddressCsv, string subject, string body, string ccAddressCsv = "", List<string> files = null)
        {
            List<EmailAddress> CsvToAddressList(string csvstring)
            {
                return csvstring.Split(',').ToList().Select(x=>new EmailAddress(x)).ToList(); 
            }

            List<EmailAddress> toAddresses = CsvToAddressList(toAddressCsv);

            try
            {
                EmailMessage msg = new EmailMessage(_service);
                msg.Subject = subject;
                msg.Body = body;
                msg.ToRecipients.AddRange(toAddresses);
                if (!string.IsNullOrEmpty(ccAddressCsv))
                {
                    List<EmailAddress> ccAddresses = CsvToAddressList(ccAddressCsv);
                    msg.CcRecipients.AddRange(ccAddresses);
                }
                if (files != null)
                {
                    foreach(var file in files)
                    {
                        msg.Attachments.AddFileAttachment(file);
                    }
                }

                msg.Send();
            }
            catch (Exception e)
            {
                throw e;
            }



        }
        public void SendEmail(string toAddressCsv, string subject, string body, List<string> attachments, string ccAddressCsv = "")
        {
            List<EmailAddress> CsvToAddressList(string csvstring)
            {
                return csvstring.Split(',').ToList().Select(x => new EmailAddress(x)).ToList();
            }

            List<EmailAddress> toAddresses = CsvToAddressList(toAddressCsv);
            try
            {
                EmailMessage msg = new EmailMessage(_service);
                msg.Subject = subject;
                msg.Body = body;
                msg.ToRecipients.AddRange(toAddresses);
                if (!string.IsNullOrEmpty(ccAddressCsv))
                {
                    List<EmailAddress> ccAddresses = CsvToAddressList(ccAddressCsv);
                    msg.CcRecipients.AddRange(ccAddresses);
                }
                foreach (var file in attachments)
                    msg.Attachments.AddFileAttachment(file);
                msg.Send();
            }
            catch (Exception e)
            {
                throw e;
            }



        }
        public void SendEmail(string toAddressCsv, string subject, string body, List<string> inlineAttachments, List<string> attachments, string ccAddressCsv = "")
        {
            List<EmailAddress> CsvToAddressList(string csvstring)
            {
                return csvstring.Split(',').ToList().Select(x => new EmailAddress(x)).ToList();
            }

            List<EmailAddress> toAddresses = CsvToAddressList(toAddressCsv);
            try
            {
                EmailMessage msg = new EmailMessage(_service);
                msg.Subject = subject;
                msg.Body = body;
                msg.ToRecipients.AddRange(toAddresses);
                if (!string.IsNullOrEmpty(ccAddressCsv))
                {
                    List<EmailAddress> ccAddresses = CsvToAddressList(ccAddressCsv);
                    msg.CcRecipients.AddRange(ccAddresses);
                }

                if (inlineAttachments != null)
                {
                    int attachIndex = 0;
                    foreach (var inline in inlineAttachments)
                    {
                        if(System.IO.Path.GetExtension(inline)?.ToUpper() != ".XML")
                        msg.Attachments.AddFileAttachment(System.IO.Path.GetFileName(inline), System.IO.File.ReadAllBytes(inline));
                        msg.Attachments[attachIndex].IsInline = true;
                        msg.Attachments[attachIndex].ContentId = $"img-{attachIndex}";
                        attachIndex++;
                    }
                }
                if (attachments != null)
                {
                    foreach (var file in attachments)
                        msg.Attachments.AddFileAttachment(file);
                }
                msg.SendAndSaveCopy();
            }
            catch (Exception e)
            {
                throw e;
            }



        }


        public string GetAttachmentFilePath(int matterId, EmailMessage email, string attachmentId)
        {
            email = EmailMessage.Bind(_service, email.Id, new PropertySet(BasePropertySet.IdOnly, EmailMessageSchema.Sender, ItemSchema.Attachments, ItemSchema.Subject, ItemSchema.DateTimeReceived, ItemSchema.DisplayTo));

            var attachment = email.Attachments.FirstOrDefault(i => i.Id == attachmentId);
            string pathToSave = Path.Combine(Path.GetTempPath(), matterId.ToString());

            if (!Directory.Exists(pathToSave))
            {
                Directory.CreateDirectory(pathToSave);
            }

            if (attachment is FileAttachment)
            {
                FileAttachment fileAttachment = attachment as FileAttachment;

                string filePath = Path.Combine(pathToSave, attachment.Name);

                int duplicateIndex = 1;
                var name = Path.GetFileNameWithoutExtension(filePath);

                while (File.Exists(filePath))
                {
                    FileInfo fInfo = new FileInfo(filePath);
                    filePath = Path.Combine(pathToSave, name + "_" + duplicateIndex + fInfo.Extension);
                    duplicateIndex++;
                }


                // Load the attachment into a file.
                // This call results in a GetAttachment call to EWS.
                fileAttachment.Load(filePath);

                return filePath;
            }
            return null;
           
        }

        private bool CheckIfThrottled()
        {
            if (_LockedUntilDate.HasValue && _LockedUntilDate.Value > DateTime.Now)
            {
                return true;
            }
            else if(_LockedUntilDate.HasValue && _LockedUntilDate.Value < DateTime.Now)
            {
                _LockedUntilDate = null;
                _SearchExecutionTimes = new List<DateTime>();
                return false;
            }
            else
            {
                var cutOff = DateTime.Now.AddMinutes(-1 * _EWSSearchLimitTimeFrameMinutes);
                _SearchExecutionTimes = _SearchExecutionTimes.Where(x => x > cutOff).ToList();
                if (_SearchExecutionTimes.Count > _EWSSearchLimit)
                {                  
                    var newLockedDate = DateTime.Now.AddMinutes(_EWSSearchWaitTimeMinutes);
                    _LockedUntilDate = new DateTime(newLockedDate.Year, newLockedDate.Month, newLockedDate.Day, newLockedDate.Hour, newLockedDate.Minute, 0);
                    return true;
                }
                else
                {
                    _LockedUntilDate = null;
                    return false;
                }
            }
        }


        public List<EmailMessage> SearchEmailInbox(List<string> textSearchStrings, DateTime searchStartDate, DateTime searchEndDate, bool searchBodies, FolderId specificFolder, bool isAllItems, List<FolderId> allFolders = null)
        {
            if (CheckIfThrottled())
            {
                throw new Exception($"Too many searches in last 5 minutes - please wait until {_LockedUntilDate.Value.ToShortTimeString()}");
            }
            if (!textSearchStrings.Any())
            {
                throw new Exception("NOTHING TO SEARCH FOR!");
            }
            List<EmailMessage> matches = new List<EmailMessage>();

            SearchFilter greaterThanfilter = new SearchFilter.IsGreaterThanOrEqualTo(ItemSchema.DateTimeReceived, searchStartDate);
            SearchFilter lessThanfilter = new SearchFilter.IsLessThan(ItemSchema.DateTimeReceived, searchEndDate);


            SearchFilter dateFilter = new SearchFilter.SearchFilterCollection(LogicalOperator.And, greaterThanfilter, lessThanfilter);



            string queryString = $"(received:>{searchStartDate.ToString("MM/dd/yyyy")})" +
                $" AND ((subject:{string.Join(" OR subject:", textSearchStrings.Select(s => $"\"{s}\"").ToList())})";

            if (searchBodies)
            {
                queryString+= $" OR (body:{string.Join(" OR body:", textSearchStrings.Select(s=>$"\"{s}\"").ToList()) })";
            }

            queryString += ")";



            List<SearchFilter> subjectFilters = new List<SearchFilter>();
            List<SearchFilter> bodyFilters = new List<SearchFilter>();


            foreach (var searchString in textSearchStrings.Distinct())
            {
                subjectFilters.Add(new SearchFilter.ContainsSubstring(ItemSchema.Subject,
                    searchString, ContainmentMode.Substring, ComparisonMode.IgnoreCase));
                if (searchBodies)
                {
                    bodyFilters.Add(new SearchFilter.ContainsSubstring(ItemSchema.Body, searchString, ContainmentMode.Substring, ComparisonMode.IgnoreCaseAndNonSpacingCharacters));
                }
            }
            
            SearchFilter combinedSubjectFilter = new SearchFilter.SearchFilterCollection(LogicalOperator.Or, subjectFilters.ToArray());
            
            SearchFilter combinedTextFilter = searchBodies ? 
               new SearchFilter.SearchFilterCollection(LogicalOperator.Or, bodyFilters.ToArray())
                : new SearchFilter.SearchFilterCollection(LogicalOperator.Or, combinedSubjectFilter);

            SearchFilter combinedFilter = new SearchFilter.SearchFilterCollection(LogicalOperator.And, dateFilter, combinedTextFilter);

            List<PropertyDefinitionBase> propertiesToSearch = new List<PropertyDefinitionBase>() { ItemSchema.Subject, ItemSchema.DateTimeReceived };
            if (searchBodies)
            {
                propertiesToSearch.Add(ItemSchema.Body);
            }

            //var searchResult = _service.FindItems(WellKnownFolderName.Inbox, combinedFilter, new ItemView(500) );


            List<FolderId> foldersToSearch = new List<FolderId>();

            if(specificFolder  == null)
            {
                foldersToSearch.Add(Folder.Bind(_service, WellKnownFolderName.Inbox, new PropertySet(BasePropertySet.IdOnly)).Id);
            }
            else
            {
                if (!isAllItems)
                {
                    foldersToSearch.Add(specificFolder);
                }
                else
                {
                    foldersToSearch = allFolders ?? new List<FolderId>();
                }
            }

            foreach (var folder in foldersToSearch)
            {

                var searchResult = _service.FindItems(folder, queryString, new ItemView(500));

                foreach (var item in searchResult.Items)
                {
                    if (item is EmailMessage)
                    {
                        EmailMessage message = EmailMessage.Bind(_service, item.Id, new PropertySet(BasePropertySet.IdOnly, EmailMessageSchema.Sender, ItemSchema.Attachments, ItemSchema.Subject, ItemSchema.DateTimeReceived, ItemSchema.DisplayTo, ItemSchema.TextBody));

                        matches.Add(message);
                    }
                }
            }

            _SearchExecutionTimes.Add(DateTime.Now);

            return matches.GroupBy(i=>i.Id.UniqueId).Select(e=>e.First()).ToList();
        }
        public string DownloadEmailAsMessage(int matterId, EmailMessage message)
        {

            string pathToSave = Path.Combine(Path.GetTempPath(), matterId.ToString());

            if (!Directory.Exists(pathToSave))
            {
                Directory.CreateDirectory(pathToSave);
            }

            string name = Common.CommonMethods.ToSafeFileName(message.Subject);
            string filePath = Path.Combine(pathToSave,  name + ".eml");

            int duplicateIndex = 1;
            while (File.Exists(filePath))
            {
                FileInfo fInfo = new FileInfo(filePath);
                filePath = Path.Combine(pathToSave, name + "_" + duplicateIndex + fInfo.Extension);
                duplicateIndex++;
            }

            message.Load(new PropertySet(ItemSchema.MimeContent));

            MimeContent mc = message.MimeContent;
            using (FileStream fs = new FileStream(filePath, FileMode.Create))
            {
                fs.Write(mc.Content, 0, mc.Content.Length);
            }

            return filePath;
        }


    }


    public class TraceListener : ITraceListener
    {
        #region ITraceListener Members

        public void Trace(string traceType, string traceMessage)
        {
            Console.WriteLine("--------------------------------------------");
            Console.WriteLine($"Trace type: {traceType}");
            Console.WriteLine($"Trace message: {traceMessage}");
            Console.WriteLine("--------------------------------------------");
        }

        #endregion

    }
}
