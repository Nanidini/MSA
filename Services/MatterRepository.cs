using System;
using System.Collections.Generic;
using System.Linq;
using Slick_Domain.Models;
using Slick_Domain.Entities;
using System.Text;
using NLog;
using MCE = Slick_Domain.Entities.MatterCustomEntities;
using Slick_Domain.Enums;
using Slick_Domain.Common;
using Slick_Domain.XmlProcessing;
using System.IO;
using Slick_Domain.Extensions;
using System.Text.RegularExpressions;
using MixERP.Net.VCards;
using Slick_Domain.Interfaces;
using System.Data.SqlClient;
using System.Data.Entity;
using Slick_Common.Extensions;

namespace Slick_Domain.Services
{
    public class MatterRepository : SlickRepository
    {
        private Logger _logger = LogManager.GetCurrentClassLogger();

        public MatterRepository(SlickContext context) : base(context)
        {
        }


      



        public bool MatterIsArchived(int matterId)
        {
            return context.Matters.First(x=>x.MatterId==matterId).ArchiveStatusTypeId==(int)Enums.ArchiveStatusTypeId.Archived;
        } 
        
        public bool ArchiveMatter(int matterId,string archivePath, string livePath, ICaller caller)
        {
            if (!caller.CallerId.Equals(Slick_Domain.Common.DomainConstants.ArchiveCallerId))
            {
                throw new Exception("Only Archive Web Service can call this function.");
            }
            if (MatterIsArchived(matterId))
            {
                throw new Exception("Matter is already archived");
            }
            bool success = true;
            string matterDocumentArchiveDirectory = archivePath +@"\"+ matterId.ToString();
            string matterDocumentLivePath = livePath + @"\" + matterId.ToString();

            if (!Directory.Exists(matterDocumentArchiveDirectory))
                Directory.CreateDirectory(matterDocumentArchiveDirectory);

            var docRep = new DocumentsRepository(context);
            
            var documents = docRep.GetAllDocumentsForMatter(matterId);
            List<MCE.MatterDocumentsView> succesfullyArchivedDocuments = new List<MCE.MatterDocumentsView>();
            try
            {
                documents.ToList().ForEach(x =>
                {
                    docRep.ArchiveDocument(x.DocumentMasterId.Value, matterDocumentArchiveDirectory, matterDocumentLivePath, caller);
                    succesfullyArchivedDocuments.Add(x);
                });
            }
            catch(Exception e)
            {
                success = false;
                succesfullyArchivedDocuments.ForEach(x =>
                {
                    docRep.UnarchiveDocument(x.DocumentMasterId.Value, matterDocumentLivePath, matterDocumentArchiveDirectory, caller);
                });
                throw e;
            }
              
            
            Matter m = context.Matters.First();
            m.ArchiveStatusTypeId = (int)Slick_Domain.Enums.ArchiveStatusTypeId.Archived;
            m.ArchiveStatusUpdatedDate = System.DateTime.Now;
            context.SaveChanges();

            return success;
        }




        public bool UnarchiveMatter(int matterId, string archivePath, string livePath, ICaller caller)
        {
            if (!caller.CallerId.Equals(Slick_Domain.Common.DomainConstants.ArchiveCallerId))
            {
                throw new Exception("Only Archive Web Service can call this function.");
            }
            if (MatterIsArchived(matterId))
            {
                throw new Exception("Matter is already archived");
            }
            bool success = true;
            string matterDocumentArchiveDirectory = archivePath + @"/" + matterId.ToString();
            string matterDocumentLivePath = livePath + @"/" + matterId.ToString();

            if (!Directory.Exists(matterDocumentArchiveDirectory))
                Directory.CreateDirectory(matterDocumentArchiveDirectory);

            var docRep = new DocumentsRepository(context);
            
            var documents = docRep.GetAllDocumentsForMatter(matterId);
            List<MCE.MatterDocumentsView> succesfullyArchivedDocuments = new List<MCE.MatterDocumentsView>();
            try
            {
                documents.ToList().ForEach(x =>
                {
                    docRep.UnarchiveDocument(x.DocumentMasterId.Value, matterDocumentArchiveDirectory, matterDocumentLivePath, caller);
                    succesfullyArchivedDocuments.Add(x);
                });
            }
            catch (Exception e)
            {
                success = false;
                succesfullyArchivedDocuments.ForEach(x =>
                {
                    docRep.ArchiveDocument(x.DocumentMasterId.Value, matterDocumentArchiveDirectory, matterDocumentLivePath, caller);
                });
                throw e;
            }

            

            Matter m = context.Matters.First();
            m.ArchiveStatusTypeId = (int)Slick_Domain.Enums.ArchiveStatusTypeId.NotArchived;
            m.ArchiveStatusUpdatedDate = System.DateTime.Now;
            context.SaveChanges();


            return success;

        }

        public IEnumerable<MCE.MatterReviewedHistoryView> GetMatterReviewHistory(int id)
        {
            return context.MatterReviewedHistories.Where(x => x.MatterId == id).
                Select(m => new MCE.MatterReviewedHistoryView
                {
                    ReviewedBy = m.User.Username,
                    ReviewedDate = m.LastReviewedDate,
                    ReviewedByPhoneNumber = !String.IsNullOrEmpty(m.User.OfficeExt) ? m.User.OfficeExt : m.User.Phone,
                    ReviewedByEmail = m.User.Email
                })
                .ToList();
        }
        public IEnumerable<MCE.MatterSecurityView> GetMatterSecurityViews(int matterId)
        {
            return context.MatterSecurities.Where(s => !s.Deleted && s.MatterId == matterId)
                .Select(s => new
                {
                    s.MatterSecurityId,
                    s.MatterId,
                    s.MatterTypeId,
                    s.MatterType.MatterTypeName,
                    s.SettlementTypeId,
                    s.SettlementType.SettlementTypeName,
                    s.SecurityAssetType.SecurityAssetTypeName,
                    s.StreetAddress,
                    s.Suburb,
                    s.StateId,
                    s.State.StateName,
                    s.State.StateDesc,
                    s.PostCode,
                    s.ValuationExpiryDate,
                    TitleRefs = s.MatterSecurityTitleRefs.Select(t => new { t.MatterSecurityTitleRefId, t.MatterSecurityId, t.TitleReference, t.LandDescription, t.IsRegistered }).ToList()
                }).ToList().Select(s => new MCE.MatterSecurityView(s.MatterSecurityId, s.MatterId, s.MatterTypeId, s.MatterTypeName, s.SettlementTypeId, s.SettlementTypeName, s.SecurityAssetTypeName, s.StreetAddress,
                                            s.Suburb, s.StateId, s.StateName, s.PostCode,
                                            s.TitleRefs.Select(t => new MCE.MatterSecurityTitleRefView(t.MatterSecurityTitleRefId, t.MatterSecurityId, t.TitleReference, t.LandDescription, t.IsRegistered)).ToList(), null, s.ValuationExpiryDate, pStateDesc: s.StateDesc)).ToList();
        }
        public List<string> GetOnHoldUpdateAddresses(int matterId)
        {
            var addresses = new List<String>();
            var validPartyTypes = new List<int>() { (int)MatterPartyTypeEnum.Borrower, (int)MatterPartyTypeEnum.Mortgagor };
            var mtDetails = context.Matters.Where(m => m.MatterId == matterId)
                .Select(m => new
                {
                    SendEmailLender = m.MortMgrId.HasValue && m.MortMgr.OverrideLenderOnHoldEmail ? m.MortMgr.SendOnHoldEmailLender : m.Lender.SendOnHoldEmailLender,
                    LenderEmail = m.Lender.PrimaryContact.Email,

                    SendEmailBroker = m.MortMgrId.HasValue && m.MortMgr.OverrideLenderOnHoldEmail ? m.MortMgr.SendOnHoldEmailBroker : m.Lender.SendOnHoldEmailBroker,
                    BrokerEmail = m.Broker.PrimaryContact.Email,

                    SendEmailBorrower = m.MortMgrId.HasValue && m.MortMgr.OverrideLenderOnHoldEmail ? m.MortMgr.SendOnHoldEmailBorrower : m.Lender.SendOnHoldEmailBorrower,
                    BorrowerEmails = m.MatterParties.Where(x=>validPartyTypes.Contains(x.PartyTypeId)).Select(x=>x.Email).ToList(),

                    SendEmailMortMgr = m.MortMgrId.HasValue && m.MortMgr.OverrideLenderOnHoldEmail ? m.MortMgr.SendOnHoldEmailMortMgr : m.Lender.SendOnHoldEmailMortMgr,
                    MortMgrEmail = m.MortMgr.PrimaryContact.Email,

                    SendEmailFileOwner = m.MortMgrId.HasValue && m.MortMgr.OverrideLenderOnHoldEmail ? m.MortMgr.SendOnHoldEmailFileOwner : m.Lender.SendOnHoldEmailFileOwner,
                    FileOwnerEmail = m.User.Email,

                    SendEmailSecondaryContact = m.MortMgrId.HasValue && m.MortMgr.OverrideLenderOnHoldEmail ? m.MortMgr.SendOnHoldEmailSecondaryContact : m.Lender.SendOnHoldEmailSecondaryContact,
                    SecondaryContactEmails = m.MatterAdditionalContacts.Where(c=>c.AdditionalContactTypeId ==(int)AdditionalContactTypeEnum.Lender).Select(e=>e.PrimaryContact.Email).ToList(),

                    SendEmailOtherParty = m.MortMgr.OverrideLenderOnHoldEmail ? m.MortMgr.SendOnHoldEmailOtherParty: m.Lender.SendOnHoldEmailOtherParty,
                    OtherPartyEmails = m.MatterOtherParties.Where(e=>e.Email != null).Select(x=>x.Email).ToList(),

                    SendEmailCustomEmail = m.MortMgr.OverrideLenderOnHoldEmail ? m.MortMgr.SendOnHoldEmailCustomEmail : m.Lender.SendOnHoldEmailCustomEmail,

                }).FirstOrDefault();

            if (mtDetails.SendEmailLender) addresses.Add(mtDetails.LenderEmail);
            if (mtDetails.SendEmailBroker) addresses.Add(mtDetails.BrokerEmail);
            if (mtDetails.SendEmailBorrower) mtDetails.BorrowerEmails.ForEach((x)=> { addresses.Add(x); });
            if (mtDetails.SendEmailMortMgr) addresses.Add(mtDetails.MortMgrEmail);
            if (mtDetails.SendEmailFileOwner) addresses.Add(mtDetails.FileOwnerEmail);
            if (mtDetails.SendEmailSecondaryContact) mtDetails.SecondaryContactEmails.ForEach((x) => { addresses.Add(x); });
            if (mtDetails.SendEmailOtherParty) mtDetails.OtherPartyEmails.ForEach((x) => { addresses.Add(x); });
            if (!string.IsNullOrEmpty(mtDetails.SendEmailCustomEmail) && mtDetails.SendEmailCustomEmail.Contains("@")) addresses.Add(mtDetails.SendEmailCustomEmail);

            addresses = addresses.Where(e => !string.IsNullOrEmpty(e) && e.Contains("@")).ToList();

            return addresses;
        }



        #region QR Code methods

        public string GetDataForQRCode(int matterId, QRCodeTemplate code, EmailsRepository eRep, Dictionary<string, string> placeholderValues)
        {
            string data = "";
            const string emailTemplate = "MATMSG: TO: {{EmailRecipients}}; SUB:{{EmailSubject}}; BODY: {{EmailBody}}; ; ";
            switch (code.QRCodeContentTypeId)
            {
                case (int)QRCodeContentTypeEnum.Generic:
                    data = eRep.ReplaceEmailPlaceHolders(placeholderValues, code.GenericContent);
                    break;
                case (int)QRCodeContentTypeEnum.Email:
                    data = emailTemplate
                        .Replace("{{EmailRecipients}}", GetEmailRecipientsForQRCode(code, matterId))
                        .Replace("{{EmailSubject}}", eRep.ReplaceEmailPlaceHolders(placeholderValues, code.EmailSubject))
                        .Replace("{{EmailBody}}", eRep.ReplaceEmailPlaceHolders(placeholderValues, code.EmailBody));
                    break;
                case (int)QRCodeContentTypeEnum.FileownerContact:
                    var contactDetails = context.Matters.Where(x => x.MatterId == matterId)
                        .Select(m => new
                        {
                            m.User.Firstname,
                            m.User.Lastname,
                            m.User.Email,
                            m.User.Phone,
                            m.User.Fax,
                            m.User.UpdatedDate,
                            Address = m.User.State.Offices.Select(x => new { x.PostalAddress, x.PostalSuburb, x.PostalPostCode, x.State.StateName }).FirstOrDefault()
                        }).FirstOrDefault();



                    MixERP.Net.VCards.VCard card = new MixERP.Net.VCards.VCard()
                    {
                        Version = MixERP.Net.VCards.Types.VCardVersion.V4,
                        FormattedName = contactDetails.Firstname?.Trim() + " " + contactDetails.Lastname?.Trim(),
                        FirstName = contactDetails.Firstname?.Trim(),
                        LastName = contactDetails.Lastname?.Trim(),
                        Organization = "MSA National",
                        Note = "Your personal MSA National File Manager",
                        Emails = new List<MixERP.Net.VCards.Models.Email>() { new MixERP.Net.VCards.Models.Email() { EmailAddress = contactDetails.Email } },
                        Telephones = new List<MixERP.Net.VCards.Models.Telephone>()
                                {
                                    new MixERP.Net.VCards.Models.Telephone() { Number = contactDetails.Phone, Type = MixERP.Net.VCards.Types.TelephoneType.Work, Preference= 0 }
                                },
                        Addresses = new List<MixERP.Net.VCards.Models.Address>
                            {
                                new MixERP.Net.VCards.Models.Address()
                                {
                                    Street = contactDetails.Address.PostalAddress + " " + contactDetails.Address.PostalSuburb + " " + contactDetails.Address.StateName,
                                    PostalCode = contactDetails.Address.PostalPostCode
                                }
                            }

                    };

                    data = MixERP.Net.VCards.Serializer.VCardSerializer.Serialize(card);

                    break;
                case (int)QRCodeContentTypeEnum.ProgressBar:
                    var token = context.LimitedLoantrakAccessTokens.FirstOrDefault(m => m.MatterId == matterId)?.LimitedLoantrakAccessTokenValue;
                    if (string.IsNullOrEmpty(token))
                    {
                        return null;
                    }
                    else
                    {
                        data = GlobalVars.GetGlobalTxtVar("LoantrakBaseURL", context) + GlobalVars.GetGlobalTxtVar("ProgressBarAPI", context) + token;
                    }
                    break;
            }

            return data;
        }

        public List<MatterCustomEntities.MatterQRCodeView> GetQRCodesToGenerateForMatter(int matterId, int lenderId, int? mortMgrId, int matterGroupTypeId)
        {
            List<MatterCustomEntities.MatterQRCodeView> codes = new List<MCE.MatterQRCodeView>();
            var codeTemplates = context.QRCodeTemplates.Where(x =>
                (x.LenderId == lenderId || !x.LenderId.HasValue) &&
                (x.MortMgrId == mortMgrId || !x.MortMgrId.HasValue) &&
                (x.MatterGroupTypeId == matterGroupTypeId || !x.MatterGroupTypeId.HasValue)
                );

           
            //string contactTemplate = "BEGIN:VCARD VERSION:3.0\n"
            //                            + "FN;{{ParalegalFirstname}} {{ParalegalLastname}}\n"
            //                            + "N;{{ParalegalLastname}};{{ParalegalFirstname}};;;\n"
            //                            + "EMAIL;{{ParalegalEmail}}\n"
            //                            + "TEL;{{ParalegalPhone}}\n"
            //                            + "LABEL;TYPE=WORK:MSA National\n"
            //                            + "ADR;TYPE=WORK:;;{{OfficeStreet}};{{OfficeCity}};{{OfficeState}};{{OfficePostcode}};Australia\n"
            //                            + "REV:{{UpdatedDate}}\n"
            //                            + "END:VCARD";

            //string contactTemplate = 
            //        "BEGIN:VCARD\n"+"VERSION 2.1\n"
            //        + "N:{{ParalegalLastname}};{{ParalegalFirstname}}\n"
            //        + "FN:{{ParalegalFirstname}}\n"
            //        + "{{ParalegalLastname}}\n"
            //        + "TEL;WORK;VOICE;PREF:{{ParalegalPhone}}\n"
            //        + "ADR;HOME:;{{OfficeStreet}};{{OfficeCity}};{{OfficeState}};{{OfficePostcode}}\n"
            //        + "EMAIL;WORK:{{ParalegalEmail}}\n"
            //        + "NOTE: Comments: Your Personal MSA National File Manager\n"
            //        + "URL;HOME:www.msanational.com.au\n"
            //        + "END:VCARD";

            EmailsRepository eRep = new EmailsRepository(context);
            var placeholderValues = eRep.LinkPlaceHoldersToMatterValues(matterId);
            foreach (var code in codeTemplates)
            {
                string data = GetDataForQRCode(matterId, code, eRep, placeholderValues);
                if (data != null)
                {
                    codes.Add(new MCE.MatterQRCodeView() { FileName = code.QRCodeTemplateName, Data = data, MatterId = matterId, QRCodeStyleTypeId = code.QRCodeStyleTypeId, Size = code.Size, IncludeLogo=code.IncludeMSALogo });
                }
            }

            return codes;
        }
        
        private string GetEmailRecipientsForQRCode(QRCodeTemplate code, int matterId)
        {
            string recipients = "";

            var contacts = context.Matters.Select(x => new {
                x.MatterId,
                FileownerEmail = x.User.Email,
                BrokerEmail = x.Broker.PrimaryContact.Email,
                LenderEmail = x.Lender.PrimaryContact.Email,
                MortMgrEmail = x.MortMgr.PrimaryContact.Email,
                OtherPartyEmail = x.MatterOtherParties.FirstOrDefault().Email,
                OtherEmail = code.EmailToOtherAddress
            }).FirstOrDefault(m => m.MatterId == matterId);


            if (code.EmailToFileManager && contacts.FileownerEmail != null)
            {
                if (!string.IsNullOrEmpty(recipients))
                {
                    recipients += ", ";
                }
                recipients += contacts.FileownerEmail;
            }
            if (code.EmailToLender && contacts.LenderEmail != null)
            {
                if (!string.IsNullOrEmpty(recipients))
                {
                    recipients += ", ";
                }
                recipients += contacts.LenderEmail;
            }
            if (code.EmailToMortMgr && contacts.MortMgrEmail != null)
            {
                if (!string.IsNullOrEmpty(recipients))
                {
                    recipients += ", ";
                }
                recipients += contacts.MortMgrEmail;
            }
            if (code.EmailToBroker && contacts.BrokerEmail != null)
            {
                if (!string.IsNullOrEmpty(recipients))
                {
                    recipients += ", ";
                }
                recipients += contacts.BrokerEmail;
            }
            if (code.EmailToOtherParty && contacts.OtherPartyEmail != null)
            {
                if (!string.IsNullOrEmpty(recipients))
                {
                    recipients += ", ";
                }
                recipients += contacts.OtherPartyEmail;
            }
            if (!String.IsNullOrEmpty(contacts.OtherEmail))
            {
                if (!string.IsNullOrEmpty(recipients))
                {
                    recipients += ", ";
                }
                recipients += contacts.OtherEmail;
            }
            return recipients;
        }

        #endregion
        public void GenerateLoantrakToken(int matterId)
        {
            bool hashGenerated = false;
            string newHash = "";
            while (!hashGenerated)
            {
                newHash = matterId.ToString("x") + SlickSecurity.RandomStringURLsafe(20);
                if (!context.LimitedLoantrakAccessTokens.Any(x => x.LimitedLoantrakAccessTokenValue == newHash))
                {
                    hashGenerated = true;
                }
            }
            //string newHash = ToString("X");

            context.LimitedLoantrakAccessTokens.Add
            (
                new LimitedLoantrakAccessToken()
                {
                    LimitedLoantrakAccessTokenValue = newHash,
                    MatterId = matterId,
                    Enabled = true,
                    CreatedDate = DateTime.Now,
                    UpdatedDate = DateTime.Now,
                    UpdatedByUserId = GlobalVars.CurrentUser.UserId
                }
            );
                
        }
        public IEnumerable<EntityCompacted> GetMattersCompacted(IQueryable<Matter> matterQry)
        {
            return
                 matterQry
                .Where(s => s.MatterStatusTypeId == 1)
                .OrderBy(o => o.MatterId)
                .Select(m2 => new { m2.MatterId, m2.MatterDescription })
                .ToList()
                .Select(m => new EntityCompacted
                {
                    Id = m.MatterId,
                    Details = string.Format("{1} - {0}", m.MatterDescription, m.MatterId)
                })
                .ToList();
        }

        public IEnumerable<EntityCompacted> GetMattersCompacted()
        {
            var qry = context.Matters.AsNoTracking();
            return GetMattersCompacted(qry);
        }
        public IEnumerable<EntityCompacted> GetMattersCompactedByState(int stateId)
        {
            var qry = context.Matters.AsNoTracking()
                    .Where(m => m.StateId == stateId);
            return GetMattersCompacted(qry);
        }

        public MCE.MatterTypeView GetMatterTypeView(int matterTypeId)
        {
            return context.MatterTypes.Where(m => m.MatterTypeId == matterTypeId)
                .Select(m => new { m.MatterTypeId, m.MatterTypeName, m.MatterTypeDesc, m.IsMatterGroup, m.MatterTypeGroupId, MatterTypeGroupName = m.MatterType2.MatterTypeName, m.DisplayOrder, m.ReportRanking, m.Enabled, m.UpdatedDate, m.UpdatedByUserId, UpdatedByUsername = m.User.Username })
                .ToList()
                .Select(m => new MCE.MatterTypeView(m.MatterTypeId, m.MatterTypeName, m.MatterTypeDesc, m.IsMatterGroup, m.MatterTypeGroupId, m.MatterTypeGroupName, m.DisplayOrder, m.ReportRanking,
                                    m.Enabled, m.UpdatedDate, m.UpdatedByUserId, m.UpdatedByUsername))
                .FirstOrDefault();
        }

        public MCE.MatterViewCompact GetMatterDetailsCompact(int matterId)
        {
            return context.Matters.AsNoTracking().Where(x => x.MatterId == matterId)
                .Select(s => new MCE.MatterViewCompact
                {
                    Borrowers = s.MatterDescription,
                    BrokerName = s.Broker.CompanyName,
                    BrokerId = s.BrokerId,
                    LenderName = s.Lender.LenderName,
                    LoanTypeId = s.LoanTypeId,
                    MortMgrName = s.MortMgr.MortMgrName,
                    MatterId = s.MatterId,
                    MatterGroupTypeId = s.MatterGroupTypeId,
                    SettlementDate = s.SettlementSchedule.SettlementDate,
                    StateId = s.StateId,
                    LenderId = s.LenderId,
                    MortMgrId = s.MortMgrId,
                    IsSelfActing = ((bool?)s.MatterDischarge.IsSelfActing) ?? false,
                    IsPEXA = s.MatterSecurities.Any(ms => ms.SettlementTypeId == (int)SettlementTypeEnum.PEXA),
                    StopAutomatedEmails = s.StopAutomatedEmails,
                    MatterStatusTypeId = s.MatterStatusTypeId,
                    MatterStatusTypeName = s.MatterStatusType.MatterStatusTypeName
                }
            ).FirstOrDefault();
        }

        public MCE.MatterOtherDetailsView GetMatterOtherDetailsView(int matterId)
        {
            return context.Matters.Where(x => x.MatterId == matterId)
                .Select(s => new MCE.MatterOtherDetailsView
                {
                    LendersRefNo = s.LenderRefNo,
                    MatterDescription = s.MatterDescription,
                    BrokerId = s.BrokerId,
                    PrimaryContactBrokerId = s.BrokerPrimaryContactId,
                    PrimaryContactMortMgrId = s.MortMgrPrimaryContactId
                }
                ).FirstOrDefault();
        }
        public IEnumerable<MCE.MatterSecurityInsuranceDetailsView> GetSecurityInsuranceDetailsViews(int matterId)
        {
            return GetSecurityInsuranceDetailsView(context.MatterSecurities.Where(m => m.MatterId == matterId && !m.Deleted));
        }
        public IEnumerable<MCE.MatterSecurityInsuranceDetailsView> GetSecurityInsuranceDetailsView(int matterSecurityId)
        {
            return GetSecurityInsuranceDetailsView(context.MatterSecurities.Where(m => m.MatterSecurityId == matterSecurityId));
        }
        public IEnumerable<MCE.MatterSecurityInsuranceDetailsView> GetSecurityInsuranceDetailsView(IQueryable<MatterSecurity> qry)
        {
            return qry.Select(x => new MCE.MatterSecurityInsuranceDetailsView()
            {
                MatterSecurityId = x.MatterSecurityId,
                MatterSecurityAddress = x.StreetAddress.Trim() + " " + x.Suburb.Trim() + ", " + x.State.StateName + ", " + x.PostCode.Trim(),
                InsuranceTypeId =
                    x.InsuranceTypeId.HasValue && x.InsuranceTypeId != (int)InsuranceTypeEnum.NotApplicable ? x.InsuranceTypeId.Value :
                        x.IsVacant ? (int)InsuranceTypeEnum.VacantLand :
                            x.Matter.IsConstruction == true ? (int)InsuranceTypeEnum.Construction :
                                x.MatterTypeId == (int)MatterTypeEnum.ExistingSecurity ? (int)InsuranceTypeEnum.ExistingSecurity :
                                    x.IsStrata ? (int)InsuranceTypeEnum.Strata
                                        : (int)InsuranceTypeEnum.NonStrata,
                LenderExistingInsuranceRequired = x.Matter.Lender.ExistingInsuranceRequired,
                LenderStrataInsuranceRequired = x.Matter.Lender.StrataInsuranceRequired,
                TitleRef = x.MatterSecurityTitleRefs.Select(r=>r.TitleReference).FirstOrDefault(),
                InsuranceStartDate = x.InsuranceStartDate,
                InsuranceEndDate = x.InsuranceEndDate,
                InsuranceCompanyName = x.InsuranceCompanyName,
                InsuranceNotApplicable = (x.InsuranceNotApplicable == true || x.InsuranceTypeId == (int)InsuranceTypeEnum.NotApplicable) ? true : false,
                InsuranceNotes = x.InsuranceNotes,
                IsReplacementValueOnly = x.IsReplacementValueOnly ?? false,
                PolicyNo = x.PolicyNo,
                SpecificAmount = x.SpecificAmount ?? 0.0M,
                MatterDocumentId = x.InsuranceMatterDocumentId,
                MatterDocumentName = x.MatterDocument.DocumentMaster.DocName,
                HasDocument = x.InsuranceMatterDocumentId.HasValue
            }).ToList();
        }
        public IEnumerable<MCE.MatterTypeView> GetMatterTypesView()
        {
            var matterTypes = context.MatterTypes.AsNoTracking();
            return GetMatterTypesView(matterTypes);
        }

        public IEnumerable<MCE.MatterTypeView> GetMatterTypesView(bool isMatterGroup)
        {
            var matterTypes = context.MatterTypes.AsNoTracking().Where(x => x.IsMatterGroup == isMatterGroup);
            return GetMatterTypesView(matterTypes);
        }

        public bool IsDischargeSummaryLocked(int matterId)
        {
            return context.SettlementSchedules.Any(s => s.MatterId == matterId &&
            s.SettlementScheduleStatusType.IsActiveStatus == true &&
            s.DischargeSummary.DischargeSummaryStatusTypeId == (int)Enums.DischargeSummaryStatusTypeEnum.Locked);
        }


        public Tuple<bool, string> CanMatterBeClosed(int matterId, int lenderId, int stateId, int matterGroupTypeId, int? listTypeBeingAdded = null)
        {
            bool canClose = true;
            string reason = "";

            if(stateId == (int)StateIdEnum.TAS)
            {
                return new Tuple<bool, string>(true, null);
            }

            var lenderRequirements = context.Lenders.Where(l=>l.LenderId == lenderId).Select(l => new
            {
                l.LenderId,
                SecPacketRequired = matterGroupTypeId == (int)MatterGroupTypeEnum.NewLoan ? l.NewLoanSecPacketRequired : l.DischargeSecPacketRequired,
                ArchiveRequired = matterGroupTypeId == (int)MatterGroupTypeEnum.NewLoan ? l.NewLoanArchiveRequired : l.DischargeArchiveRequired
            }).FirstOrDefault();

            var slickListDetails = new
            {
                OnSecPacketList = context.ConsignmentMatters.Any(c => c.MatterId == matterId && (listTypeBeingAdded == (int)ConsignmentTypeEnum.SecurityPacket || c.Consignment.ConsignmentTypeId == (int)ConsignmentTypeEnum.SecurityPacket)),
                OnArchiveList = context.ConsignmentMatters.Any(c => c.MatterId == matterId && (listTypeBeingAdded == (int)ConsignmentTypeEnum.Archive || c.Consignment.ConsignmentTypeId == (int)ConsignmentTypeEnum.Archive))
            };

            if(lenderRequirements.SecPacketRequired && !slickListDetails.OnSecPacketList)
            {
                canClose = false;
                reason = "- Lender requires matters to be on Security Packet SLICK List to close \n";
            }

            if (lenderRequirements.ArchiveRequired && !slickListDetails.OnArchiveList)
            {
                canClose = false;
                reason = "- Lender requires matters to be on Archive SLICK List to close";
            }

            return new Tuple<bool, string>(canClose, reason?.Trim());
        }


        public IEnumerable<MCE.MatterFastRefiDetailView> GetFastRefiDetailsForMatter(int matterId)
        {
            return context.MatterFastRefiDetails.Where(m => m.MatterId == matterId).Select(f => new MCE.MatterFastRefiDetailView
            {
                MatterId = matterId,
                MatterFastRefiDetailId = f.MatterFastRefiDetailId,
                BalanceExpiryDate = f.BalanceExpiryDate,
                EFT_AccountName = f.EFT_AccountName,
                EFT_AccountNo = f.EFT_AccountNo,
                EFT_BSB = f.EFT_BSB,
                ContactEmail = f.ContactEmail,
                ContactFax = f.ContactFax,
                ContactName = f.ContactName,
                isDirty = false
            }).ToList();
        }

        public IEnumerable<MCE.BulkUpdateMatterView> GetMatterDetailsForBulkUpdate(int matterId)
        {
            var matters =
                (from m in context.Matters
                 join c in context.MatterWFComponents on m.MatterId equals c.MatterId into cg
                 from c2 in cg.DefaultIfEmpty()
                 join w in context.WFComponents on c2.WFComponentId equals w.WFComponentId into wg
                 from w2 in wg.DefaultIfEmpty()
                 where m.MatterId == matterId && c2.WFComponentStatusType.IsActiveStatus && 
                    (c2.DisplayStatusTypeId == (int)Enums.DisplayStatusTypeEnum.Display || c2.DisplayStatusTypeId == (int)Enums.DisplayStatusTypeEnum.Default)


                 select new MCE.BulkUpdateMatterView
                 {
                     MatterId = m.MatterId,
                     MatterGroupTypeId = m.MatterGroupTypeId,
                     MatterWFComponentId = c2.MatterWFComponentId,
                     Borrowers = m.MatterDescription,
                     LenderName = m.Lender.LenderNameShort,
                     FileOwner = m.User.Username,
                     LenderRef = m.LenderRefNo,
                     MatterStatusId = m.MatterStatusTypeId,
                     MatterStatus = m.MatterStatusType.MatterStatusTypeName,
                     Settled = m.Settled,
                     CurrentWFComponentName = w2.WFComponentName,
                     ModuleId = w2.WFModuleId,
                     CanBulkUpdate = w2.WFModule.CanBulkUpdate && !w2.BulkUpdateExcludeComponent,
                     ReasonNotUpdateable = (w2.WFModule.CanBulkUpdate && !w2.BulkUpdateExcludeComponent) ? null : "Milestone can not be Bulk Updated"
                 }
                    ).ToList();

            foreach (var matter in matters)
            {
                var currMatter = matter;
                string reason = "";
                CheckIfCanBeBulkUpdated(ref currMatter, ref reason);
                currMatter.ReasonNotUpdateable = !string.IsNullOrEmpty(reason) ? reason : currMatter.ReasonNotUpdateable  ;
            }

            return matters;
        }

        public bool CheckIfCanBeBulkUpdated(ref MCE.BulkUpdateMatterView matter, ref string reason)
        {
            bool isValidComponent = true;
            bool hasIssues = false;
            matter.IsUpdateable = CanBeBulkUpdated(matter, ref isValidComponent, ref hasIssues, ref reason);

            if(matter.IsUpdateable && GlobalVars.CurrentUser?.StateId == (int)StateIdEnum.SA)
            {
                bool permitted = Slick_Domain.Common.CommonMethods.CheckMatterPermissions(context, matter.MatterId, GlobalVars.CurrentUser.UserId);
                if (!permitted)
                {
                    matter.IsUpdateable = false;
                    reason = "User does not have access to Matter";
                }
            }

            matter.IsValidComponent = isValidComponent;
            matter.HasIssues = hasIssues;

            return matter.IsUpdateable;
        }
        public bool CheckIfCanBeBulkUpdated(MCE.MatterTasksListView matter)
        {
            bool isValidComponent = true;
            bool hasIssues = false;
            var isUpdateable = CanBeBulkUpdated(matter, ref isValidComponent, ref hasIssues);

            return isUpdateable;
        }
        private bool CanBeBulkUpdated(MCE.BulkUpdateMatterView matter, ref bool isValidComponent, ref bool hasIssues, ref string reason)
        {
            int wfComponentId = 0;
            int lenderId = 0;
            bool canClose = true;

            if (matter.MatterWFComponentId.HasValue)
            {
                hasIssues = context.MatterWFIssues.Any(x => x.DisplayedMatterWFComponentId == matter.MatterWFComponentId && !x.Resolved);
                var mtDetails = context.MatterWFComponents.Select(x => new { x.MatterWFComponentId, x.WFComponentId,x.Matter.LenderId, x.Matter.StateId, x.Matter.MatterGroupTypeId, x }).FirstOrDefault(m => m.MatterWFComponentId == matter.MatterWFComponentId);
                wfComponentId = mtDetails.WFComponentId;
                lenderId = mtDetails.LenderId;
                if (wfComponentId == (int)WFComponentEnum.CloseMatter)
                {
                    var closeCheck = CanMatterBeClosed(matter.MatterId, lenderId, mtDetails.StateId, mtDetails.MatterGroupTypeId);
                    canClose = closeCheck.Item1;
                    reason = closeCheck.Item2;
                }
            }


            var tRep = new TeamRepository(context);
            if(matter.MatterWFComponentId == (int)WFComponentEnum.QASettlement || matter.MatterWFComponentId == (int)WFComponentEnum.QASettlementInstructions)
            {
                if(!tRep.IsUserQA(GlobalVars.CurrentUser.UserId, lenderId))
                {
                    canClose = false;
                    reason = $"User is not set up in valid QA Team for {matter.LenderName}";
                }
            }


            if (matter.MatterWFComponentId == (int)WFComponentEnum.PrepareSettlementDischQA)
            {
                if (!tRep.IsDischargeQA(GlobalVars.CurrentUser.UserId))
                {
                    canClose = false;
                    reason = $"User is not set up in valid Discharge QA Team";
                }
            }


            isValidComponent = matter.CanBulkUpdate;
            return isValidComponent && canClose && matter.MatterStatusId != (int)MatterStatusTypeEnum.NotProceeding && matter.MatterStatusId != (int)Enums.MatterStatusTypeEnum.Closed && matter.MatterStatusId != (int)Enums.MatterStatusTypeEnum.OnHold;
        }
        private bool CanBeBulkUpdated(MCE.MatterTasksListView matter, ref bool isValidComponent, ref bool hasIssues)
        {
           
            hasIssues = context.MatterWFIssues.Any(x => x.DisplayedMatterWFComponentId == matter.MatterWFComponentId && !x.Resolved);

            bool canClose = true;
            if (matter.WFComponentId == (int)WFComponentEnum.CloseMatter)
            {
              
                var closeCheck = CanMatterBeClosed(matter.MatterId, matter.LenderId.Value, matter.StateId.Value, matter.MatterTypeGroupId);
                canClose = closeCheck.Item1;
                
            }

            isValidComponent = matter.CanBulkUpdate;
            return isValidComponent && canClose && matter.MatterStatusId != (int)MatterStatusTypeEnum.NotProceeding && matter.MatterStatusId != (int)Enums.MatterStatusTypeEnum.Closed && matter.MatterStatusId != (int)Enums.MatterStatusTypeEnum.OnHold;
        }

        public IEnumerable<MCE.MatterTypeView> GetMatterTypesView(IQueryable<MatterType> matterTypes)
        {
            return matterTypes
                .Select(m => new { m.MatterTypeId, m.MatterTypeName, m.MatterTypeDesc, m.IsMatterGroup, m.MatterTypeGroupId, MatterTypeGroupName = m.MatterType2.MatterTypeName, m.DisplayOrder, m.ReportRanking, m.Enabled, m.UpdatedDate, m.UpdatedByUserId, UpdatedByUsername = m.User.Username })
                .ToList()
                .Select(m => new MCE.MatterTypeView(m.MatterTypeId, m.MatterTypeName, m.MatterTypeDesc, m.IsMatterGroup, m.MatterTypeGroupId, m.MatterTypeGroupName, m.DisplayOrder, m.ReportRanking,
                                    m.Enabled, m.UpdatedDate, m.UpdatedByUserId, m.UpdatedByUsername))
                .OrderBy(o => o.DisplayOrder).ThenBy(o2 => o2.MatterTypeGroupName).ThenBy(o3 => o3.MatterTypeName)
                .ToList();
        }

        public MCE.MatterGroupView GetMatterGroupView(int matterTypeId)
        {
            return context.MatterTypes.Where(m => m.IsMatterGroup && m.MatterTypeId == matterTypeId)
                .Select(m => new { m.MatterTypeId, m.MatterTypeName, m.MatterTypeDesc, m.DisplayOrder, m.Enabled, m.UpdatedDate, m.UpdatedByUserId, UpdatedByUsername = m.User.Username })
                .ToList()
                .Select(m => new MCE.MatterGroupView(m.MatterTypeId, m.MatterTypeName, m.MatterTypeDesc, m.DisplayOrder,
                                    m.Enabled, m.UpdatedDate, m.UpdatedByUserId, m.UpdatedByUsername))
                .FirstOrDefault();
        }
        public IEnumerable<MCE.MatterGroupView> GetMatterGroupsView()
        {
            return context.MatterTypes.Where(m => m.IsMatterGroup)
                .Select(m => new { m.MatterTypeId, m.MatterTypeName, m.MatterTypeDesc, m.DisplayOrder, m.Enabled, m.UpdatedDate, m.UpdatedByUserId, UpdatedByUsername = m.User.Username })
                .ToList()
                .Select(m => new MCE.MatterGroupView(m.MatterTypeId, m.MatterTypeName, m.MatterTypeDesc, m.DisplayOrder,
                                    m.Enabled, m.UpdatedDate, m.UpdatedByUserId, m.UpdatedByUsername))
                .ToList();
        }

        public IEnumerable<MCE.MatterSearchListView> GetSearchMattersView(IQueryable<Matter> matters)
        {
            var srch = (from m in matters
                    //join mt in context.MatterMatterTypes on m.MatterId equals mt.MatterId into mtg
                    //join ms in context.MatterSecurities on m.MatterId equals ms.MatterId into msg
                    select new
                    {
                        m.MatterId,
                        m.MatterDescription,
                        MatterGroupTypeName = m.MatterType.MatterTypeName,
                        m.User.Username,
                        m.Lender.LenderName,
                        m.LenderRefNo,
                        m.SecondaryRefNo,
                        MatterTypes = m.MatterMatterTypes.Select(mt => new { mt.MatterMatterTypeId, mt.MatterTypeId, mt.MatterType.MatterTypeName }),
                        PexaWorkspaceList = m.MatterPexaWorkspaces.Select(x => x.PexaWorkspace.PexaWorkspaceNo),
                        Securities = m.MatterSecurities.Where(x => !x.Deleted).Select(s => new
                        {
                            s.MatterSecurityId,
                            s.MatterTypeId,
                            s.MatterType.MatterTypeName,
                            s.SettlementTypeId,
                            s.SettlementType.SettlementTypeName,
                            s.SecurityAssetType.SecurityAssetTypeName,
                            s.StreetAddress,
                            s.Suburb,
                            s.StateId,
                            s.State.StateName,
                            s.PostCode,
                            s.ValuationExpiryDate,
                            TitleRefs = s.MatterSecurityTitleRefs.Select(t => new { t.MatterSecurityTitleRefId, t.MatterSecurityId, t.TitleReference, t.LandDescription, t.IsRegistered })
                        }),
                        m.MatterSearch.MatterSearchText
                    }).ToList()
                .Select(m => new MCE.MatterSearchListView(m.MatterId, m.MatterDescription, m.Username, m.MatterGroupTypeName, m.LenderName, m.LenderRefNo, m.SecondaryRefNo, m.PexaWorkspaceList?.ToList(), 
                m.MatterTypes.Select(t => new MCE.MatterMatterTypeView(t.MatterMatterTypeId, m.MatterId, t.MatterTypeId, t.MatterTypeName)).ToList(),
                m.Securities.Select(s => new MCE.MatterSecurityView(s.MatterSecurityId, m.MatterId, s.MatterTypeId, s.MatterTypeName, s.SettlementTypeId, s.SettlementTypeName, s.SecurityAssetTypeName, s.StreetAddress,
                                            s.Suburb, s.StateId, s.StateName, s.PostCode,
                                            s.TitleRefs.Select(t => new MCE.MatterSecurityTitleRefView(t.MatterSecurityTitleRefId, t.MatterSecurityId, t.TitleReference, t.LandDescription, t.IsRegistered)).ToList(), null, s.ValuationExpiryDate)).ToList(),
                m.MatterSearchText
                ))
            .ToList();


            return srch; 
        }

        //TESTING-------------------------------------------------------------------------------------------------------
        //Selects only the fields that are going to be used in the search before the large join, to save database resources.
        public IEnumerable<MCE.MatterSearchListView> GetSearchMattersViewQuick(IQueryable<Matter> matters)
        {
            return (from m in matters.Select(x => new { x.MatterId, x.MatterDescription, x.MatterType, x.Lender, x.LenderRefNo, x.SecondaryRefNo, x.MatterSearch, x.MatterPexaWorkspaces, x.User.Username })
                    join mt in context.MatterMatterTypes.Select(x => new { x.MatterId, x.MatterMatterTypeId, x.MatterTypeId, x.MatterType }) on m.MatterId equals mt.MatterId into mtg
                    join ms in context.MatterSecurities.Select(x => new
                    {
                        x.MatterId,
                        x.Deleted,
                        x.MatterSecurityId,
                        x.MatterTypeId,
                        x.MatterType,
                        x.SettlementTypeId,
                        x.SettlementType,
                        x.SecurityAssetType,
                        x.StreetAddress,
                        x.Matter.SecondaryRefNo,
                        x.Suburb,
                        x.StateId,
                        x.State,
                        x.PostCode,
                        x.ValuationExpiryDate,
                        x.MatterSecurityTitleRefs
                    }) on m.MatterId equals ms.MatterId into msg
                    select new
                    {
                        m.MatterId,
                        m.MatterDescription,
                        MatterGroupTypeName = m.MatterType.MatterTypeName,
                        m.Lender.LenderName,
                        m.LenderRefNo,
                        m.SecondaryRefNo,
                        m.Username,
                        MatterTypes = mtg.Select(m => new { m.MatterMatterTypeId, m.MatterTypeId, m.MatterType.MatterTypeName }),
                        PexaWorkspaceList = m.MatterPexaWorkspaces.Select(x => x.PexaWorkspace.PexaWorkspaceNo),
                        Securities = msg.Where(x => !x.Deleted).Select(s => new
                        {
                            s.MatterSecurityId,
                            s.MatterTypeId,
                            s.MatterType.MatterTypeName,
                            s.SettlementTypeId,
                            s.SettlementType.SettlementTypeName,
                            s.SecurityAssetType.SecurityAssetTypeName,
                            s.StreetAddress,
                            s.Suburb,
                            s.StateId,
                            s.State.StateName,
                            s.PostCode,
                            s.ValuationExpiryDate,
                            TitleRefs = s.MatterSecurityTitleRefs.Select(t => new { t.MatterSecurityTitleRefId, t.MatterSecurityId, t.TitleReference, t.LandDescription, t.IsRegistered })
                        }),
                        m.MatterSearch.MatterSearchText
                    }).ToList()
                .Select(m => new MCE.MatterSearchListView(m.MatterId, m.MatterDescription, m.Username, m.MatterGroupTypeName, m.LenderName, m.LenderRefNo, m.SecondaryRefNo, m.PexaWorkspaceList?.ToList(),
                m.MatterTypes.Select(t => new MCE.MatterMatterTypeView(t.MatterMatterTypeId, m.MatterId, t.MatterTypeId, t.MatterTypeName)).ToList(),
                m.Securities.Select(s => new MCE.MatterSecurityView(s.MatterSecurityId, m.MatterId, s.MatterTypeId, s.MatterTypeName, s.SettlementTypeId, s.SettlementTypeName, s.SecurityAssetTypeName, s.StreetAddress,
                                            s.Suburb, s.StateId, s.StateName, s.PostCode,
                                            s.TitleRefs.Select(t => new MCE.MatterSecurityTitleRefView(t.MatterSecurityTitleRefId, t.MatterSecurityId, t.TitleReference, t.LandDescription, t.IsRegistered)).ToList(), null, s.ValuationExpiryDate)).ToList(),
                m.MatterSearchText
                ))
        .ToList();

        }
        //END TESTING---------------------------------------------------------------------------------------------------



        public IEnumerable<MCE.MatterSearchListView> GetSearchMattersViewSproc()
        {
            DateTime tmpDate = DateTime.Today.AddDays(-14);

            var res = context.sp_Slick_GetQuickSearchResults(tmpDate).ToList().OrderByDescending(x => x.UpdatedDate)
            .Select(s => new MCE.MatterSearchListView()
            {
                MatterId = s.MatterId,
                MatterIdStr = s.MatterId.ToString(),
                MatterDescription = s.MatterDescription,
                LenderName = s.LenderName,
                LenderRefNo = s.LenderRefNo,
                SecondaryRefNo = s.SecondaryRefNo,
                FileownerName = s.FileOwnerName,
                HasSecondaryRefNo = !String.IsNullOrEmpty(s.SecondaryRefNo),
                PexaWorkspacesStr = s.PexaWorkspaceList,
                HasPexaWorkspaces = !String.IsNullOrEmpty(s.PexaWorkspaceList),
                MatterTypes = !String.IsNullOrEmpty(s.MatterTypeList) ? s.MatterTypeList.Split(',').ToList() : new List<string> { s.MatterGroupTypeName },
                SecuritiesStr = !String.IsNullOrEmpty(s.MatterSecuritiesList) ? s.MatterSecuritiesList.Replace("\\n", $"{(char)13}") : null,
                TitleRefStr = !String.IsNullOrEmpty(s.TitleRefList) ? s.TitleRefList.Replace("\\n", $"{(char)13}") : null,
                SearchString = s.MatterSearchText
            }).ToList();
        
            return res;
        }
        public IEnumerable<MCE.MatterSearchListView> GetSearchMattersView()
        {
            DateTime tmpDate = DateTime.Today.AddMonths(-1);
            IQueryable<Matter> tmp = context.Matters.AsNoTracking().Where(m => m.MatterStatusTypeId == 1 || m.MatterStatusTypeId == 2 || m.FileOpenedDateOnly >= tmpDate || m.UpdatedDate >= tmpDate);
            return GetSearchMattersView(tmp);
        }

        public IEnumerable<MCE.MatterSearchListView> GetSearchMattersView(User user)
        {
            return GetSearchMattersView(user, null);
        }
        public IEnumerable<MCE.MatterSearchListView> GetSearchMattersView(User user, string searchText)
        {
            DateTime tmpDate = DateTime.Today.AddMonths(-1);

            IQueryable<Matter> tmp = context.Matters.AsNoTracking()
            .Where(m =>
                  m.MatterStatusTypeId == 1       || 
                  m.MatterStatusTypeId == 2       ||
                  m.FileOpenedDateOnly >= tmpDate || 
                  m.UpdatedDate >= tmpDate
             );

            if (user.LenderId.HasValue)
                tmp = tmp.Where(m => m.LenderId == user.LenderId && user.UserTypeId == (int)Enums.UserTypeEnum.Lender);
            if (user.MortMgrId.HasValue)
                tmp = tmp.Where(m => m.MortMgrId == user.MortMgrId && user.UserTypeId == (int)Enums.UserTypeEnum.MortMgr);
            if (user.BrokerId.HasValue)
                tmp = tmp.Where(m => user.UserTypeId == (int)Enums.UserTypeEnum.Broker && (m.BrokerId == user.BrokerId || m.Broker.PrimaryContactId == user.Broker.PrimaryContactId));

            if (!string.IsNullOrWhiteSpace(searchText))
                tmp = tmp.Where(m => m.MatterSearch.MatterSearchText.Contains(searchText.ToLower()));

            return GetSearchMattersView(tmp);
        }

        //TESTING------------------------------------------------------------------------------------------------------------------------------
        public IEnumerable<MCE.MatterSearchListView> GetSearchMattersViewQuick(User user, string searchText)
        {
            DateTime tmpDate = DateTime.Today.AddMonths(-1);

            IQueryable<Matter> tmp = context.Matters.AsNoTracking().Where(m =>
               m.MatterStatusTypeId == 1
               || m.MatterStatusTypeId == 2
               || m.FileOpenedDateOnly >= tmpDate
               || m.UpdatedDate >= tmpDate);

            if (user.LenderId.HasValue)
                tmp = tmp.Where(m => m.LenderId == user.LenderId && user.UserTypeId == (int)Enums.UserTypeEnum.Lender);
            if (user.MortMgrId.HasValue)
                tmp = tmp.Where(m => m.MortMgrId == user.MortMgrId && user.UserTypeId == (int)Enums.UserTypeEnum.MortMgr);
            if (user.BrokerId.HasValue)
                tmp = tmp.Where(m => user.UserTypeId == (int)Enums.UserTypeEnum.Broker && (m.BrokerId == user.BrokerId || m.Broker.PrimaryContactId == user.Broker.PrimaryContactId));

            if (!string.IsNullOrWhiteSpace(searchText))
                tmp = tmp.Where(m => m.MatterSearch.MatterSearchText.Contains(searchText.ToLower()));

            return GetSearchMattersViewQuick(tmp);
        }
        //END TESTING-----------------------------------------------------------------------------------------------------------------------------
        public IEnumerable<MCE.MatterSearchListView> GetSearchMattersView(List<int> matterList)
        {
            IQueryable<Matter> tmp = context.Matters.AsNoTracking().Where(m => matterList.Contains(m.MatterId));
            return GetSearchMattersView(tmp);
        }

        public void EditDetails(int currentMatterId, int newLenderId, Lender newLender)
        {
            var mrep = context.Matters.Where(m => m.MatterId == currentMatterId).FirstOrDefault();
            mrep.LenderId = newLenderId;
            //mrep.Lender. = newLenderName;
            context.SaveChanges();
        }

        






        public MCE.MatterSearchListView GetSearchMatterView(User user, int matterId)
        {
            IQueryable<Matter> tmp = context.Matters.AsNoTracking().Where(m => m.MatterId == matterId);
            if (user.LenderId.HasValue)
                if (!user.HasStateRestrictions)
                    tmp = tmp.Where(m => m.LenderId == user.LenderId && user.UserTypeId == (int)Enums.UserTypeEnum.Lender);
                else
                {
                    IEnumerable<int> restrictions = context.UserStateRestrictions.Where(r => r.UserId == user.UserId).Select(u => u.StateId );
                    tmp = tmp.Where(m => restrictions.Contains(m.InstructionStateId ?? m.StateId) && m.LenderId == user.LenderId && user.UserTypeId == (int)Enums.UserTypeEnum.Lender);
                }
            if (user.MortMgrId.HasValue)
                tmp = tmp.Where(m => m.MortMgrId == user.MortMgrId && user.UserTypeId == (int)Enums.UserTypeEnum.MortMgr);
            if (user.BrokerId.HasValue)
                tmp = tmp.Where(m => user.UserTypeId == (int)Enums.UserTypeEnum.Broker && (m.BrokerId == user.BrokerId || m.Broker.PrimaryContactId == user.Broker.PrimaryContactId));

            return GetSearchMattersView(tmp).FirstOrDefault();
        }
        public MCE.MatterSearchListView GetSearchMatterView(int matterId)
        {
            IQueryable<Matter> tmp = context.Matters.AsNoTracking().Where(m => m.MatterId == matterId);
            return GetSearchMattersView(tmp).FirstOrDefault();
        }
        public IEnumerable<MatterCustomEntities.MatterSpecialConditionView> GetMatterSpecialConditions(int matterId)
        {
            return context.MatterSpecialConditions.Where(m=>m.MatterId == matterId)
                    .Select(c => new MatterCustomEntities.MatterSpecialConditionView
                    {
                        SpecialConditionId = c.MatterSpecialConditionId,
                        SpecialConditionDesc = c.SpecialConditionDesc,
                        IncludeLetterToBorrower = c.IncludeOnLetterToBorrower,
                        IncludeFrontCover = c.IncludeOnFrontCover,
                        IncludeLoanAgreement = c.IncludeOnLoanAgreement,
                        Resolved = c.Resolved,
                        ResolvedDate = c.ResolvedDate,
                        ResolvedByUsername = c.User.Username
                    });
        }
        public IEnumerable<MatterCustomEntities.MatterBannerView> GetMatterBanners(int matterId)
        {
            return context.MatterCustomBanners.Where(m => m.MatterId == matterId && !m.Deleted)
                .Select(m => new
                {
                    MatterBannerId = m.MatterCustomBannerId,
                    MatterId = m.MatterId,
                    BackgroundBrushString = m.BannerColour,
                    BannerDescription = m.BannerText,
                    m.CanDelete,
                    m.BannerAddedDate,
                    m.DisplayOrder
                }).ToList().OrderBy(o=>o.DisplayOrder).ToList()
                .Select(m => new MCE.MatterBannerView() // do the brush conversion after evaluating from database
                {
                    MatterBannerId = m.MatterBannerId,
                    MatterId = m.MatterId,
                    BackgroundBrushString = m.BackgroundBrushString,
                    BannerDescription = m.BannerDescription,
                    CreatedDate = m.BannerAddedDate,
                    DisplayOrder = m.DisplayOrder,
                    CanBeDeleted = m.CanDelete
                    
                }).ToList();
        }
        public MatterCustomEntities.MatterView GetMatterView(int matterId)
        {
            _logger.Trace($"MatterRepository::GetMatterView(matterId: {matterId})");
            var mtQry = context.Matters.AsNoTracking().Where(m => m.MatterId == matterId)
                   .Select(m => new
                   {
                       m.MatterId,
                       m.MatterDescription,
                       m.IsTestFile,
                       m.MatterGroupTypeId,
                       m.Paperless,
                       m.IsEscalated,
                       

                       MatterTypeGroupName = m.MatterType.MatterTypeName,
                       m.MortgageIssuedByType.MortgageIssuedByTypeName,
                       DischargeTypeId = (int?)m.MatterDischarge.DischargeTypeId,
                       m.MatterDischarge.DischargeType.DischargeTypeName,
                       IsSelfActing = (bool?)m.MatterDischarge.IsSelfActing,
                       m.MatterStatusTypeId,
                       m.MatterStatusType.MatterStatusTypeName,
                       m.InstructionsReceivedDate,
                       m.InstructionRefNo,
                       m.SecondaryRefNo,
                       m.IsConstruction,
                       m.Lender.SecondaryRefName,
                       m.StateId,
                       m.State.StateName,
                       m.InstructionStateId,
                       InstructionStateName = m.State1.StateName,
                       m.FileOwnerUserId,
                       FileOwnerUser = new
                       {
                           m.User.UserId,
                           m.User.Username,
                           m.User.Firstname,
                           m.User.Lastname,
                           m.User.DisplayInitials
                       },
                       FileOwnerWFH = m.User.WorkingFromHome,
                       m.LenderId,
                       m.Lender.LenderName,
                       LenderDefPrimaryContact = new
                       {
                           m.Lender.PrimaryContact.Firstname,
                           m.Lender.PrimaryContact.Lastname,
                           m.Lender.PrimaryContact.Email,
                           m.Lender.PrimaryContact.Phone,
                           m.Lender.PrimaryContact.Fax,
                           m.Lender.PrimaryContact.Mobile,
                           PrimaryContactId = (int?)m.Lender.PrimaryContactId,
                           UpdatedDate = (DateTime?)m.Lender.PrimaryContact.UpdatedDate,
                           UpdatedByUserId = (int?)m.Lender.PrimaryContact.UpdatedByUserId,
                           m.Lender.PrimaryContact.User.Username
                       },
                       LenderPrimaryContact = new
                       {
                           m.PrimaryContact1.Firstname,
                           m.PrimaryContact1.Lastname,
                           m.PrimaryContact1.Email,
                           m.PrimaryContact1.Phone,
                           m.PrimaryContact1.Fax,
                           m.PrimaryContact1.Mobile,
                           PrimaryContactId = (int?)m.PrimaryContact1.PrimaryContactId,
                           UpdatedDate = (DateTime?)m.Lender.PrimaryContact.UpdatedDate,
                           UpdatedByUserId = (int?)m.Lender.PrimaryContact.UpdatedByUserId,
                           m.Lender.User.Username
                       },
                       FundingChannelType = new { m.FundingChannelTypeId, m.FundingChannelType.FundingChannelTypeName },
                       m.MortMgrId,
                       m.MortMgr.MortMgrName,
                       MortMgrPrimaryContact = new
                       {
                           m.MortMgr.PrimaryContact.Firstname,
                           m.MortMgr.PrimaryContact.Lastname,
                           m.MortMgr.PrimaryContact.Email,
                           m.MortMgr.PrimaryContact.Phone,
                           m.MortMgr.PrimaryContact.Fax,
                           m.MortMgr.PrimaryContact.Mobile,
                           PrimaryContactId = (int?)m.MortMgr.PrimaryContactId,
                           UpdatedDate = (DateTime?)m.MortMgr.PrimaryContact.UpdatedDate,
                           UpdatedByUserId = (int?)m.MortMgr.PrimaryContact.UpdatedByUserId,
                           m.MortMgr.PrimaryContact.User.Username
                       },
                       m.BrokerId,
                       BrokerLastname = m.Broker.PrimaryContact.Lastname,
                       BrokerFirstname = m.Broker.PrimaryContact.Firstname,
                       BrokerPrimaryContact = new
                       {
                           m.Broker.PrimaryContact.Firstname,
                           m.Broker.PrimaryContact.Lastname,
                           m.Broker.PrimaryContact.Email,
                           m.Broker.PrimaryContact.Phone,
                           m.Broker.PrimaryContact.Fax,
                           m.Broker.PrimaryContact.Mobile,
                           PrimaryContactId = (int?)m.Broker.PrimaryContactId,
                           UpdatedDate = (DateTime?)m.Broker.PrimaryContact.UpdatedDate,
                           UpdatedByUserId = (int?)m.Broker.PrimaryContact.UpdatedByUserId,
                           m.Broker.PrimaryContact.User.Username
                       },
                       m.LenderRefNo,
                       PexaDetail = m.MatterPexaDetails.Select(p => new {
                           MatterId = p.MatterId,
                           NominatedSettlementDate = (DateTime?)p.NominatedSettlementDate,
                           MatterPexaDetailId = (int?)p.MatterPexaDetailId,
                           UpdatedByUserId = p.UpdatedByUserId,
                           UpdatedDate = (DateTime?)m.UpdatedDate
                       }).FirstOrDefault(),
                       PexaWorkspace = m.MatterPexaWorkspaces.Select(p => new
                       {
                           MatterPexaWorkspaceId = (int?)p.MatterPexaWorkspaceId,
                           MatterId = p.MatterId,
                           PexaWorkspaceId = (int?)p.PexaWorkspaceId,
                           PexaWorkspaceNo = p.PexaWorkspace.PexaWorkspaceNo,
                           PexaWorkspaceStatus = p.PexaWorkspace.PexaWorkspaceStatusType.PexaWorkspaceStatusTypeName,
                           Enabled = p.PexaWorkspace.Enabled,
                           UpdatedDate = p.UpdatedDate,
                           UpdatedByUserId = (int?)p.UpdatedByUserId
                       }),
                       m.IsDigiDocs,
                       m.MatterBillable,
                       m.BillingLocked,
                       m.Certified,
                       m.CertifiedDate,
                       m.Settled,
                       m.SettlementScheduleId,
                       SettlementDate = (DateTime?)m.SettlementSchedule.SettlementDate,
                       SettlementTime = (TimeSpan?)m.SettlementSchedule.SettlementTime,
                       FastREFISettlementDate = (DateTime?)m.SettlementSchedule1.SettlementDate,
                       FastREFISettlementTime = (TimeSpan?)m.SettlementSchedule1.SettlementTime,
                       m.FileOpenedDate,
                       m.CheckInstructionsDate,
                       m.ReworkCount,
                       m.UpdatedDate,
                       m.UpdatedByUserId,
                       UpdatedByUsername = m.User1.Username,
                       UpdatedByLastname = m.User1.Lastname,
                       UpdatedByFirstname = m.User1.Firstname,
                       m.StopReminders,
                       m.MatterEscalatedFlag,
                       m.StopAutomatedEmails,
                       m.DisclosureDate,
                       m.LoanExpiryDate,
                       m.NCCPExpiryDate,
                       m.LMIExpiryDate,
                       PexaDate = m.MatterPexaDetails.Select(n=>new { n.NominatedSettlementDate, n.UpdatedDate }).OrderByDescending(o=>o.UpdatedDate).FirstOrDefault(),
                       ManualAnticipatedDate = m.AnticipatedSettlementDate.HasValue ? m.AnticipatedSettlementDate :  m.MatterWFComponents.OrderByDescending(o=>o.UpdatedDate).SelectMany(o=>o.MatterWFOutstandingReqs.Where(d=>d.ExpectedSettlementDate.HasValue).Select(s=>s.ExpectedSettlementDate)).FirstOrDefault(),
                       RelationshipManagerDetails = m.Broker.BrokerRelationshipManagers.Select(x =>
                                            new MatterCustomEntities.RelationshipManagerLimitedView()
                                            {
                                                RelationshipManagerId = x.RelationshipManagerId,
                                                Firstname = x.RelationshipManager.PrimaryContact.Firstname,
                                                Lastname = x.RelationshipManager.PrimaryContact.Lastname,
                                                Email = x.RelationshipManager.PrimaryContact.Email,
                                                Mobile = x.RelationshipManager.PrimaryContact.Mobile,
                                                Phone = x.RelationshipManager.PrimaryContact.Phone,
                                                Fax = x.RelationshipManager.PrimaryContact.Fax
                                            }),

                       MatterTypes = m.MatterMatterTypes.Select(t => new { t.MatterMatterTypeId, t.MatterTypeId, t.MatterType.MatterTypeName }),
                       Parties = m.MatterParties.Select(p => new
                       {
                           p.MatterPartyId,
                           p.PartyTypeId,
                           p.PartyType.PartyTypeName,
                           p.IsIndividual,
                           p.Title,
                           p.Lastname,
                           p.Firstname,
                           p.CompanyName,
                           p.IsInternational,
                           p.StreetAddress,
                           p.Suburb,
                           p.StateId,
                           p.State.StateName,
                           p.Postcode,
                           p.Country,
                           p.Phone,
                           p.Mobile,
                           p.Fax,
                           p.Email,
                           p.TrustDeedMatterDocumentId,
                           p.TrustDeedVetted,
                           p.TrustDeedVettedByMSAUser,
                           p.TrustDeedVettedByOtherName,
                           TrustDeedVettedByUsername = p.User1.Fullname,
                           p.MatterDocument.DocumentMaster.DocName,
                           p.GuarantorLetterMatterDocumentId,
                           GuarantorDocName = p.MatterDocument1.DocumentMaster.DocName,
                           p.TrustName,
                           p.IsTrustee
                       }),
                       Securities = m.MatterSecurities.Where(ms => !ms.Deleted).Select(s => new
                       {
                           s.MatterSecurityId,
                           s.MatterTypeId,
                           s.MatterType.MatterTypeName,
                           s.SettlementTypeId,
                           s.SettlementType.SettlementTypeName,
                           s.SecurityAssetType.SecurityAssetTypeName,
                           s.StreetAddress,
                           s.Suburb,
                           s.StateId,
                           s.State.StateName,
                           s.PostCode,
                           s.ValuationDate,
                           s.ValuationExpiryDate,
                           s.OverridePSTRequirement,
                           TitleRefs = s.MatterSecurityTitleRefs.Select(t => new { t.MatterSecurityTitleRefId, t.MatterSecurityId, t.TitleReference, t.LandDescription, t.IsRegistered }),
                           Parties = s.MatterSecurityParties.Select(x => new { PartyName = (x.MatterParty.IsIndividual ? x.MatterParty.Title + " " + x.MatterParty.Firstname + " " + x.MatterParty.Lastname : x.MatterParty.CompanyName).Trim() }),
                           InsuranceTypeId = s.InsuranceTypeId.HasValue && s.InsuranceTypeId != (int)InsuranceTypeEnum.NotApplicable ? s.InsuranceTypeId.Value :
                                s.IsVacant ? (int)InsuranceTypeEnum.VacantLand :
                                    s.Matter.IsConstruction == true ? (int)InsuranceTypeEnum.Construction :
                                        s.MatterTypeId == (int)MatterTypeEnum.ExistingSecurity ? (int)InsuranceTypeEnum.ExistingSecurity :
                                            s.IsStrata ? (int)InsuranceTypeEnum.Strata
                                                : (int)InsuranceTypeEnum.NonStrata,
                           HasDocument = s.InsuranceMatterDocumentId.HasValue,
                           MatterDocumentId = s.InsuranceMatterDocumentId,
                           MatterDocumentName = s.MatterDocument.DocumentMaster.DocName,
                           s.InsuranceStartDate,
                           s.InsuranceEndDate,
                           s.DischargeReasonType.DischargeReasonTypeName,
                           RegisteredDocuments = s.MatterSecurityDocuments.Select(d => new
                           {
                               d.MatterSecurityDocumentId,
                               d.DocumentName,
                               d.DocumentDescription,
                               d.StateName,
                               d.RegisteredDocumentTypeId
                           })
                       }),
                       m.Lender.StrataInsuranceRequired,
                       m.Lender.ExistingInsuranceRequired,
                       TrustTransactions = m.TrustTransactionItems.Select(t => new { t.TrustTransactionItemId, t.TransactionDirectionTypeId, t.Amount, t.PayerPayee, t.TrustTransactionStatusType.TrustTransactionStatusTypeName }),
                       LoanAccounts = m.MatterLoanAccounts.Select(l => new { l.MatterLoanAccountId, l.MatterId, l.LoanAccountNo, l.LoanDescription, l.LoanAmount, l.OriginalLoanAmount, l.FacilityTypeId, l.FacilityType.FacilityTypeName, l.PostSettlementFacilityNo }),
                       DischargeIndicativePayouts = m.MatterIndicativePayoutFigures.Select(i => new { i.MatterIndicativePayoutFigureId, i.LoanAccNo, i.IndicativePayoutAmount }),
                       DischargePayouts = m.MatterDischargePayouts.Select(p => new { p.MatterDischargePayoutId, p.MatterId, p.AccountNo, p.PayoutAmount, p.UpdatedDate, p.UpdatedByUserId }),
                       DischargeOverrides = m.MatterDischargeBankedOverrides.Select(x => new
                       {
                           MatterDischargeBankedOverrideId = x.MatterDischargeBankedOverrideId,
                           MatterId = x.MatterId,
                           AccountNo = x.AccountNo,
                           Amount = x.Amount,
                           Notes = x.Notes,
                           UpdatedDate = (DateTime?)x.UpdatedDate,
                           UpdatedByUserId = x.UpdatedByUserId
                       }),
                       OtherPartyDetails = m.MatterOtherParties.Select(x => new { x.Name, x.Address, x.Contact, x.Email, x.Fax, x.Phone, x.Reference }),
                       m.MatterPoints,
                       MatterSpecialConditions = m.MatterSpecialConditions.Select(c => new { SpecialConditionId = c.MatterSpecialConditionId, SpecialConditionDesc = c.SpecialConditionDesc, IncludeLetterToBorrower = c.IncludeOnLetterToBorrower, IncludeFrontCover = c.IncludeOnFrontCover, IncludeLoanAgreement = c.IncludeOnLoanAgreement, Resolved = c.Resolved }),
                       m.LoanTypeId,
                       LoanTypeName = m.LoanTypeId.HasValue ? m.LoanType.LoanTypeName : null,
                       m.BusinessTypeId,
                       BusinessTypeName = m.BusinessTypeId.HasValue ? m.BusinessType.BusinessTypeName : null,
                       m.IsMQConversion,
                       m.CheckInstructionsDateTime,
                       m.FHOGApplies,
                       FastRefiDetails = m.MatterFastRefiDetails.Select(f=>new
                       {
                           f.MatterFastRefiDetailId,
                           f.BalanceExpiryDate,
                           f.EFT_AccountName,
                           f.EFT_AccountNo,
                           f.EFT_BSB,
                       }).ToList(),
                       ToDoListItems = m.UserToDoListItems.Select(x => new
                       {
                           x.UserToDoListItemId,
                           x.MatterId,
                           x.ItemDescription,
                           x.ItemNotes,
                           x.DueDate,
                           x.ItemAssignedToUserId,
                           ItemAssignedToUserName = x.User.Username,
                           x.ItemCreatedByUserId,
                           ItemCreatedByUserName = x.User1.Username,
                           x.ItemCreatedDate,
                           x.ItemUpdatedByUserId,
                           ItemUpdatedByUserName = x.User2.Username,
                           x.ItemUpdatedDate,
                           x.ToDoListItemStatusTypeId,
                           x.ToDoListItemStatusType.ToDoListItemStatusTypeName,
                           x.ReminderDate,
                           x.CreatedFromWFIssueId,
                           x.CreatedAtMatterWFComponentId,
                           x.ToDoListItemReminderTypeId,
                           x.ToDoListItemReminderType.ToDoListItemReminderTypeDescription,
                           x.User.StateId,
                           x.ReminderOffsetTime,
                           Documents = x.UserToDoListItemDocuments.Select(d=>new { d.ReasonDocumentId, d.Reason.ReasonTxt, d.Reason.Highlight } ).ToList()
                           
                       }).Where(i => i.ToDoListItemStatusTypeId != (int)ToDoListStatusTypeEnum.Complete),
                       LenderAdditionalContacts = m.MatterAdditionalContacts.Select(x => new { x.PrimaryContact.Firstname, x.PrimaryContact.Lastname, x.PrimaryContact.Email, x.PrimaryContact.Mobile, x.PrimaryContactId, x.UpdatedDate, x.UpdatedByUserId, x.User.Username }),

                       DocDeliveryDetails = m.MatterWFComponents.Where(x=>
                        (x.DisplayStatusTypeId == (int)DisplayStatusTypeEnum.Default || x.DisplayStatusTypeId == (int)DisplayStatusTypeEnum.Display) 
                        && x.MatterWFSendProcessedDocs.Any()).OrderByDescending(o=>o.DisplayOrder).FirstOrDefault().MatterWFSendProcessedDocs.Select(x=>new { x.DocumentDeliveryTypeId, x.DocumentDeliveryType.DocumentDeliveryTypeName  })
                   }).Take(1);

            MatterCustomEntities.MatterView response = mtQry.ToList()
                .Select(m => new MCE.MatterView(m.MatterId, m.MatterDescription, m.Paperless, m.IsTestFile, m.MatterGroupTypeId, m.MatterTypeGroupName, m.MatterPoints, m.DischargeTypeId, m.DischargeTypeName, m.IsSelfActing, m.MatterStatusTypeId,
                m.MatterStatusTypeName, m.StateId, m.StateName, m.InstructionStateId, m.InstructionStateName, m.FileOwnerUser.UserId, m.FileOwnerUser.Username, m.FileOwnerUser.Lastname, m.FileOwnerUser.Firstname, m.FileOwnerWFH,
                m.LenderId, m.LenderName, m.FundingChannelType.FundingChannelTypeId, m.FundingChannelType == null ? null : m.FundingChannelType.FundingChannelTypeName,
                m.LenderDefPrimaryContact == null || !m.LenderDefPrimaryContact.PrimaryContactId.HasValue ? null : new MCE.PrimaryContactView(m.LenderDefPrimaryContact.PrimaryContactId ?? 0, m.LenderDefPrimaryContact.Lastname, m.LenderDefPrimaryContact.Firstname,
                            m.LenderDefPrimaryContact.Phone, m.LenderDefPrimaryContact.Mobile, m.LenderDefPrimaryContact.Fax, m.LenderDefPrimaryContact.Email, m.LenderDefPrimaryContact.UpdatedDate ?? new DateTime(),
                            m.LenderDefPrimaryContact.UpdatedByUserId ?? 0, m.LenderDefPrimaryContact.Username),
                m.LenderPrimaryContact == null || !m.LenderPrimaryContact.PrimaryContactId.HasValue ? null :
                            new MCE.PrimaryContactView(m.LenderPrimaryContact.PrimaryContactId ?? 0, m.LenderPrimaryContact.Lastname, m.LenderPrimaryContact.Firstname,
                            m.LenderPrimaryContact.Phone, m.LenderPrimaryContact.Mobile, m.LenderPrimaryContact.Fax, m.LenderPrimaryContact.Email, m.LenderPrimaryContact.UpdatedDate ?? new DateTime(),
                            m.LenderPrimaryContact.UpdatedByUserId ?? 0, m.LenderPrimaryContact.Username),
                m.MortMgrId, m.MortMgrName,
                m.MortMgrPrimaryContact == null || !m.MortMgrPrimaryContact.PrimaryContactId.HasValue ? null : new MCE.PrimaryContactView(m.MortMgrPrimaryContact.PrimaryContactId.Value, m.MortMgrPrimaryContact.Lastname, m.MortMgrPrimaryContact.Firstname,
                            m.MortMgrPrimaryContact.Phone, m.MortMgrPrimaryContact.Mobile, m.MortMgrPrimaryContact.Fax, m.MortMgrPrimaryContact.Email, m.MortMgrPrimaryContact.UpdatedDate.Value,
                            m.MortMgrPrimaryContact.UpdatedByUserId.Value, m.MortMgrPrimaryContact.Username),
                m.MortMgrPrimaryContact == null || !m.MortMgrPrimaryContact.PrimaryContactId.HasValue ? null : new MCE.PrimaryContactView(m.MortMgrPrimaryContact.PrimaryContactId.Value, m.MortMgrPrimaryContact.Lastname, m.MortMgrPrimaryContact.Firstname,
                            m.MortMgrPrimaryContact.Phone, m.MortMgrPrimaryContact.Mobile, m.MortMgrPrimaryContact.Fax, m.MortMgrPrimaryContact.Email, m.MortMgrPrimaryContact.UpdatedDate.Value,
                            m.MortMgrPrimaryContact.UpdatedByUserId.Value, m.MortMgrPrimaryContact.Username),
                m.BrokerId, Common.EntityHelper.GetFullName(m.BrokerLastname, m.BrokerFirstname),
                m.BrokerPrimaryContact == null || !m.BrokerPrimaryContact.PrimaryContactId.HasValue ? null : new MCE.PrimaryContactView(m.BrokerPrimaryContact.PrimaryContactId.Value, m.BrokerPrimaryContact.Lastname, m.BrokerPrimaryContact.Firstname, m.BrokerPrimaryContact.Phone, m.BrokerPrimaryContact.Mobile, m.BrokerPrimaryContact.Fax, m.BrokerPrimaryContact.Email, m.BrokerPrimaryContact.UpdatedDate.Value, m.BrokerPrimaryContact.UpdatedByUserId.Value, m.BrokerPrimaryContact.Username),
                m.BrokerPrimaryContact == null || !m.BrokerPrimaryContact.PrimaryContactId.HasValue ? null : new MCE.PrimaryContactView(m.BrokerPrimaryContact.PrimaryContactId.Value, m.BrokerPrimaryContact.Lastname, m.BrokerPrimaryContact.Firstname, m.BrokerPrimaryContact.Phone, m.BrokerPrimaryContact.Mobile, m.BrokerPrimaryContact.Fax, m.BrokerPrimaryContact.Email, m.BrokerPrimaryContact.UpdatedDate.Value, m.BrokerPrimaryContact.UpdatedByUserId.Value, m.BrokerPrimaryContact.Username),
                m.LenderRefNo, m.SecondaryRefNo, m.SecondaryRefName,
                m.PexaDetail == null || !m.PexaDetail.NominatedSettlementDate.HasValue || !m.PexaDetail.MatterPexaDetailId.HasValue ? null : new MCE.MatterPexaDetailView() { MatterId = m.PexaDetail.MatterId, NominatedSettlementDate = m.PexaDetail.NominatedSettlementDate.Value, MatterPexaDetailId = m.PexaDetail.MatterPexaDetailId.Value, UpdatedByUserId = m.PexaDetail.UpdatedByUserId, UpdatedDate = m.UpdatedDate },
                m.PexaWorkspace.Select(p => new MCE.MatterPexaWorkspaceView() { MatterPexaWorkspaceId = p.MatterPexaWorkspaceId.Value, MatterId = p.MatterId, PexaWorkspaceId = p.PexaWorkspaceId.Value, PexaWorkspaceNo = p.PexaWorkspaceNo, PexaWorkspaceStatus = p.PexaWorkspaceStatus, Enabled = p.Enabled, UpdatedDate = p.UpdatedDate, UpdatedByUserId = p.UpdatedByUserId.Value }).ToList(),
                m.IsDigiDocs, m.MatterBillable, m.BillingLocked, m.Settled, m.Certified, m.CertifiedDate, m.FileOpenedDate, m.CheckInstructionsDate, m.ReworkCount,
                m.SettlementScheduleId, m.SettlementDate, m.SettlementTime, m.FastREFISettlementDate, m.FastREFISettlementTime, m.UpdatedDate, m.UpdatedByUserId, m.UpdatedByUsername,
                m.UpdatedByLastname, m.UpdatedByFirstname, m.StopReminders, m.StopAutomatedEmails,
                m.MatterTypes.Select(t => new MCE.MatterMatterTypeView(t.MatterMatterTypeId, m.MatterId, t.MatterTypeId, t.MatterTypeName)).ToList(),
                m.Parties.Select(p => new MCE.MatterPartyView(p.MatterPartyId, m.MatterId, p.PartyTypeId, p.PartyTypeName, p.IsIndividual, p.Title, p.Lastname, p.Firstname, p.CompanyName, p.IsInternational, p.StreetAddress,
                                            p.Suburb, p.StateId, p.StateName, p.Postcode, p.Country, p.Phone, p.Mobile, p.Fax, p.Email, p.IsTrustee, p.TrustName, p.TrustDeedMatterDocumentId, p.DocName, p.GuarantorLetterMatterDocumentId, 
                                            p.GuarantorDocName, m.LenderId, p.TrustDeedVetted, p.TrustDeedVettedByMSAUser ? p.TrustDeedVettedByUsername : p.TrustDeedVettedByOtherName
                                            
                                            )).ToList(),
                m.Securities.Select(s => new MCE.MatterSecurityView
                                        (s.MatterSecurityId, m.MatterId, s.MatterTypeId, s.MatterTypeName, s.SettlementTypeId, s.SettlementTypeName, s.SecurityAssetTypeName, s.StreetAddress,
                                            s.Suburb, s.StateId, s.StateName, s.PostCode,
                                            s.TitleRefs.Select(t => new MatterCustomEntities.MatterSecurityTitleRefView(t.MatterSecurityTitleRefId, t.MatterSecurityId, t.TitleReference, t.LandDescription, t.IsRegistered)).ToList(),
                                            s.Parties.Select(x => x.PartyName).ToList(), s.ValuationExpiryDate, s.InsuranceStartDate, s.InsuranceEndDate,
                                            pInsuranceRequired: s.InsuranceTypeId == (int)InsuranceTypeEnum.NonStrata || (s.InsuranceTypeId == (int)InsuranceTypeEnum.Strata && m.StrataInsuranceRequired) || (s.InsuranceTypeId == (int)InsuranceTypeEnum.ExistingSecurity && m.ExistingInsuranceRequired),
                                            pMatterDocumentId: s.MatterDocumentId, pMatterDocumentName: s.MatterDocumentName, pValuationDate: s.ValuationDate, pOverridePSTRequirement: s.OverridePSTRequirement, pRegisteredDocs: s.RegisteredDocuments.Select(d=> new MCE.MatterSecurityDocumentView() { DocumentName = !string.IsNullOrWhiteSpace(d.DocumentDescription) ? d.DocumentName + " - " + d.DocumentDescription: d.DocumentName, RegisteredDocumentTypeId = d.RegisteredDocumentTypeId, StateName = d.StateName
                                            }).ToList(), pDischargeReasonType: s.DischargeReasonTypeName 
                                            
                                            
                                            )).ToList(),
                m.LoanAccounts.Select(l => new MatterCustomEntities.MatterLoanAccountView(l.MatterLoanAccountId, l.MatterId, l.LoanAccountNo, l.LoanDescription, l.LoanAmount, l.OriginalLoanAmount, l.FacilityTypeId, l.FacilityTypeId.HasValue ? l.FacilityTypeName : null, l.PostSettlementFacilityNo)).ToList(),
                m.DischargeIndicativePayouts.Select(i => new MatterCustomEntities.MatterIndicativePayoutView() { IndicativePayoutId = i.MatterIndicativePayoutFigureId, LoanAccountNo = i.LoanAccNo, IndicativePayoutAmount = i.IndicativePayoutAmount ?? 0M }).ToList(),
                m.DischargePayouts.Select(p => new MatterCustomEntities.MatterDischargePayoutView(p.MatterDischargePayoutId, p.MatterId, p.AccountNo, p.PayoutAmount, p.UpdatedDate, p.UpdatedByUserId)).ToList(),
                m.DischargeOverrides.Select(x => new MatterCustomEntities.MatterDischargeBankedOverrideView()
                {
                    MatterDischargeBankedOverrideId = x.MatterDischargeBankedOverrideId,
                    MatterId = x.MatterId,
                    AccountNo = x.AccountNo,
                    Amount = x.Amount,
                    Notes = x.Notes,
                    UpdatedDate = x.UpdatedDate ?? new DateTime(),
                    UpdatedByUserId = x.UpdatedByUserId
                }).ToList(),
                m.OtherPartyDetails.Select(x => new MCE.MatterOtherPartyView { Name = x.Name, Address = x.Address, Contact = x.Contact, Email = x.Email, Fax = x.Fax, Phone = x.Phone, Reference = x.Reference }).FirstOrDefault(),
                m.TrustTransactions.Select(t => new AccountsCustomEntities.TrustTransactionItemViewLight { TrustTransactionItemId = t.TrustTransactionItemId, TransactionDirectionTypeId = t.TransactionDirectionTypeId, PayerPayee = t.PayerPayee, Amount = t.Amount, TrustTransactionStatusTypeName = t.TrustTransactionStatusTypeName }).ToList(),
                m.InstructionsReceivedDate, m.InstructionRefNo,
                m.MatterSpecialConditions.Select(c => new MatterCustomEntities.MatterSpecialConditionView { SpecialConditionId = c.SpecialConditionId, SpecialConditionDesc = c.SpecialConditionDesc, IncludeLetterToBorrower = c.IncludeLetterToBorrower, IncludeFrontCover = c.IncludeFrontCover, IncludeLoanAgreement = c.IncludeLoanAgreement, Resolved = c.Resolved }).ToList(),
                m.IsConstruction, m.LoanTypeId, m.LoanTypeName, m.BusinessTypeId, m.BusinessTypeName, m.ToDoListItems.ToList().Select(l =>
                                                                    new MCE.ToDoListItemView
                                                                    (
                                                                        l.UserToDoListItemId,
                                                                        l.MatterId,
                                                                        l.ToDoListItemStatusTypeId,
                                                                        l.ToDoListItemStatusTypeName,

                                                                        l.ItemDescription,
                                                                        l.ItemNotes,
                                                                        l.DueDate,
                                                                        l.ItemAssignedToUserId,
                                                                        l.ItemAssignedToUserName,
                                                                        l.ItemCreatedByUserId,
                                                                        l.ItemCreatedByUserName,
                                                                        l.ItemCreatedDate,
                                                                        l.ItemUpdatedByUserId,
                                                                        l.ItemUpdatedByUserName,
                                                                        l.ItemUpdatedDate,
                                                                        m.MatterDescription,
                                                                        l.ReminderDate,
                                                                        m.LenderName,
                                                                        l.CreatedFromWFIssueId,
                                                                        l.CreatedAtMatterWFComponentId,
                                                                        l.ToDoListItemReminderTypeId,
                                                                        l.ToDoListItemReminderTypeDescription,
                                                                        m.SettlementDate,
                                                                        l.StateId,
                                                                        l.ReminderOffsetTime,
                                                                        l.Documents.Select(d=> new MCE.ToDoListItemDetailView() { SelectedDetailReasonId = d.ReasonDocumentId, SelectedDetailReasonName = d.ReasonTxt, Highlight = d.Highlight }).ToList()
                                                                     )).OrderBy(x => x.ToDoListItemStatusTypeId).ThenBy(x => x.DueDate ?? DateTime.MaxValue).ToList(), 
                m.RelationshipManagerDetails.FirstOrDefault(), m.IsMQConversion, m.DisclosureDate, m.NCCPExpiryDate, m.CheckInstructionsDateTime,  m.FHOGApplies,
                m.LenderAdditionalContacts.Select(x=>new MCE.PrimaryContactView(x.PrimaryContactId, x.Lastname, x.Firstname, null, x.Mobile, null, x.Email, x.UpdatedDate, x.UpdatedByUserId, x.Username)).ToList(),
                m.DocDeliveryDetails.Where(o=>o.DocumentDeliveryTypeId.HasValue).GroupBy(o=>o.DocumentDeliveryTypeId).Select(x=>new MCE.MatterDocDeliveryView() { DocumentDeliveryTypeId = x.First().DocumentDeliveryTypeId.Value, DocumentDeliveryTypeName = x.First().DocumentDeliveryTypeName, Count = x.Count(), ShowCount = x.Count() > 1 }).ToList()
                ,m.MortgageIssuedByTypeName, m.LMIExpiryDate, m.FileOwnerUser.DisplayInitials ?? string.Concat((m.FileOwnerUser.Firstname+" "+m.FileOwnerUser.Lastname).Where(c=>char.IsUpper(c))), m.PexaDate?.NominatedSettlementDate, m.ManualAnticipatedDate, m.IsEscalated, m.FastRefiDetails.Select(f=> new MCE.MatterFastRefiDetailView() { MatterFastRefiDetailId = f.MatterFastRefiDetailId, EFT_BSB = f.EFT_BSB, EFT_AccountName = f.EFT_AccountName, EFT_AccountNo = f.EFT_AccountNo, BalanceExpiryDate = f.BalanceExpiryDate }).ToList(), m.LoanExpiryDate

                )).FirstOrDefault();


            



            foreach(var item in response.ToDoListItems.Where(i => i.ToDoListItemReminderTypeId != (int)ToDoListItemReminderTypeEnum.SpecificTime))
            {
               
                switch (item.ToDoListItemReminderTypeId)
                {
                    case (int)ToDoListItemReminderTypeEnum.DayBeforeSettlement:
                        if (item.SettlementDate != null && item.SettlementDate > new DateTime())
                        {
                            item.DueDate = item.SettlementDate.Value.AddBusinessDays(-1, item.StateId, context);
                        }
                        else
                        {
                            item.DueDate = null;
                        }
                        break;
                    case (int)ToDoListItemReminderTypeEnum.DayOfSettlement:
                        if (item.SettlementDate != null && item.SettlementDate > new DateTime())
                        {
                            item.DueDate = item.SettlementDate;
                        }
                        else
                        {
                            item.DueDate = null;
                        }
                        break;
                    case (int)ToDoListItemReminderTypeEnum.DayAfterSettlement:
                        if (item.SettlementDate != null && item.SettlementDate > new DateTime())
                        {
                            item.DueDate = item.SettlementDate.Value.AddBusinessDays(1, item.StateId, context); ;
                        }
                        else
                        {
                            item.DueDate = null;
                        }
                        break;
                }
                
            }



            if (response.IsDischargeType == true)
            {
                //check if actually at right workflow
                var qry = context.MatterDocuments.AsNoTracking().Where(x => x.MatterId == matterId);

                var mwfRep = new MatterWFRepository(context);
                var wfComps = mwfRep.GetMatterComponentsForMatters(qry.Select(x => x.MatterId).ToList());




                if (mwfRep.HasMilestoneCompleted(wfComps, (int)WFComponentEnum.SecurityPacketReceived))
                {

                    //response.PayoutAmount = MatterDischargePayout

                    response.SecurityDocuments = new List<MatterCustomEntities.MatterSecurityDocumentView>();

                    foreach (var security in response.Securities)
                    {
                        security.SecurityDocuments = new List<MCE.MatterSecurityDocumentView>();
                        List<MatterSecurityDocument> secDocs = context.MatterSecurityDocuments.Where(m => m.MatterSecurityId == security.MatterSecurityId).ToList();
                        foreach (var secDoc in secDocs)
                        {
                            security.SecurityDocuments.Add(new MCE.MatterSecurityDocumentView
                            {
                                MatterSecurityDocumentId = secDoc.MatterSecurityDocumentId,
                                MatterSecurityId = secDoc.MatterSecurityId,
                                RegisteredDocumentTypeId = secDoc.RegisteredDocumentTypeId,
                                DocumentName = secDoc.DocumentName,
                                StateName = secDoc.StateName,
                                UpdatedDate = secDoc.UpdatedDate,
                                UpdatedByUserId = secDoc.UpdatedByUserId,
                            });


                            response.SecurityDocuments.Add(new MCE.MatterSecurityDocumentView
                            {
                                MatterSecurityDocumentId = secDoc.MatterSecurityDocumentId,
                                MatterSecurityId = secDoc.MatterSecurityId,
                                RegisteredDocumentTypeId = secDoc.RegisteredDocumentTypeId,
                                DocumentName = secDoc.DocumentName,
                                StateName = secDoc.StateName,
                                UpdatedDate = secDoc.UpdatedDate,
                                UpdatedByUserId = secDoc.UpdatedByUserId,
                            });
                        }
                        //response.SecurityDocuments.Add(context.MatterSecurityDocuments.Where(m => m.MatterSecurityId == security.MatterSecurityId).FirstOrDefault());



                    }
                }

            }
            response.LinkedMatters = GetLinkedMattersForMatterId(matterId);
            if (!response.IsDischargeType)
            {
                var secRequirements = context.SecurityDocumentRequirements.Where(x => x.LenderId == response.LenderId && x.IsDigiDocs == response.IsDigiDocs).Select(r => new { r.LenderId, r.MatterTypeId, r.StateId, r.IsDigiDocs, r.RequirementText }).ToList();
                foreach(var sec in response.Securities)
                {
                    if(string.IsNullOrEmpty(sec.DocumentRequirementText)) sec.DocumentRequirementText = secRequirements.Where(x => x.LenderId == response.LenderId && x.StateId == sec.StateId && x.MatterTypeId == sec.MatterTypeId && x.IsDigiDocs == response.IsDigiDocs).FirstOrDefault()?.RequirementText ?? " - Not Entered - ";
                    sec.HasRequirementText = !string.IsNullOrEmpty(sec.DocumentRequirementText);
                }
            }
            return response;
        }

        public MatterCustomEntities.MatterLimitedView GetMatterLimitedView(int matterId)
        {
            _logger.Trace($"MatterRepository::GetMatterView(matterId: {matterId})");
            var mtQry = context.Matters.AsNoTracking().Where(m => m.MatterId == matterId)
                   .Select(m => new
                   {
                       m.MatterId,
                       m.MatterDescription,
                       //m.IsTestFile,
                       //m.MatterGroupTypeId,
                       //m.Paperless,
                       MatterTypeGroupName = m.MatterType.MatterTypeName,
                       //DischargeTypeId = (int?)m.MatterDischarge.DischargeTypeId,
                       m.MatterDischarge.DischargeType.DischargeTypeName,
                       //IsSelfActing = (bool?)m.MatterDischarge.IsSelfActing,
                       //m.MatterStatusTypeId,
                       //m.MatterStatusType.MatterStatusTypeName,
                       //m.InstructionsReceivedDate,
                       //m.InstructionRefNo,
                       //m.SecondaryRefNo,
                       //m.IsConstruction,
                       //m.Lender.SecondaryRefName,
                       //m.StateId,
                       //m.State.StateName,
                       //m.InstructionStateId,
                       //InstructionStateName = m.State1.StateName,
                       //m.FileOwnerUserId,
                       FileOwnerUser = new
                       {
                           m.User.UserId,
                           m.User.Username,
                           m.User.Firstname,
                           m.User.Lastname
                       },
                       //FileOwnerWFH = m.User.WorkingFromHome,
                       //m.LenderId,
                       m.Lender.LenderName,
                       LenderDefPrimaryContact = new
                       {
                           m.Lender.PrimaryContact.Firstname,
                           m.Lender.PrimaryContact.Lastname,
                           m.Lender.PrimaryContact.Email,
                           m.Lender.PrimaryContact.Phone,
                           m.Lender.PrimaryContact.Fax,
                           m.Lender.PrimaryContact.Mobile,
                           PrimaryContactId = (int?)m.Lender.PrimaryContactId,
                           UpdatedDate = (DateTime?)m.Lender.PrimaryContact.UpdatedDate,
                           UpdatedByUserId = (int?)m.Lender.PrimaryContact.UpdatedByUserId,
                           m.Lender.PrimaryContact.User.Username
                       },
                       LenderPrimaryContact = new
                       {
                           m.PrimaryContact1.Firstname,
                           m.PrimaryContact1.Lastname,
                           m.PrimaryContact1.Email,
                           m.PrimaryContact1.Phone,
                           m.PrimaryContact1.Fax,
                           m.PrimaryContact1.Mobile,
                           PrimaryContactId = (int?)m.PrimaryContact1.PrimaryContactId,
                           UpdatedDate = (DateTime?)m.Lender.PrimaryContact.UpdatedDate,
                           UpdatedByUserId = (int?)m.Lender.PrimaryContact.UpdatedByUserId,
                           m.Lender.User.Username
                       },
                       //FundingChannelType = new { m.FundingChannelTypeId, m.FundingChannelType.FundingChannelTypeName },
                       //m.MortMgrId,
                       //m.MortMgr.MortMgrName,
                       //MortMgrPrimaryContact = new
                       //{
                       //    m.MortMgr.PrimaryContact.Firstname,
                       //    m.MortMgr.PrimaryContact.Lastname,
                       //    m.MortMgr.PrimaryContact.Email,
                       //    m.MortMgr.PrimaryContact.Phone,
                       //    m.MortMgr.PrimaryContact.Fax,
                       //    m.MortMgr.PrimaryContact.Mobile,
                       //    PrimaryContactId = (int?)m.MortMgr.PrimaryContactId,
                       //    UpdatedDate = (DateTime?)m.MortMgr.PrimaryContact.UpdatedDate,
                       //    UpdatedByUserId = (int?)m.MortMgr.PrimaryContact.UpdatedByUserId,
                       //    m.MortMgr.PrimaryContact.User.Username
                       //},
                       m.BrokerId,
                       BrokerLastname = m.Broker.PrimaryContact.Lastname,
                       BrokerFirstname = m.Broker.PrimaryContact.Firstname,
                       BrokerPrimaryContact = new
                       {
                           m.Broker.PrimaryContact.Firstname,
                           m.Broker.PrimaryContact.Lastname,
                           m.Broker.PrimaryContact.Email,
                           m.Broker.PrimaryContact.Phone,
                           m.Broker.PrimaryContact.Fax,
                           m.Broker.PrimaryContact.Mobile,
                           PrimaryContactId = (int?)m.Broker.PrimaryContactId,
                           UpdatedDate = (DateTime?)m.Broker.PrimaryContact.UpdatedDate,
                           UpdatedByUserId = (int?)m.Broker.PrimaryContact.UpdatedByUserId,
                           m.Broker.PrimaryContact.User.Username
                       },
                       m.LenderRefNo,
                       PexaDetail = m.MatterPexaDetails.Select(p => new {
                           MatterId = p.MatterId,
                           NominatedSettlementDate = (DateTime?)p.NominatedSettlementDate,
                           MatterPexaDetailId = (int?)p.MatterPexaDetailId,
                           UpdatedByUserId = p.UpdatedByUserId,
                           UpdatedDate = (DateTime?)m.UpdatedDate
                       }).FirstOrDefault(),
                       PexaWorkspace = m.MatterPexaWorkspaces.Select(p => new
                       {
                           MatterPexaWorkspaceId = (int?)p.MatterPexaWorkspaceId,
                           MatterId = p.MatterId,
                           PexaWorkspaceId = (int?)p.PexaWorkspaceId,
                           PexaWorkspaceNo = p.PexaWorkspace.PexaWorkspaceNo,
                           PexaWorkspaceStatus = p.PexaWorkspace.PexaWorkspaceStatusType.PexaWorkspaceStatusTypeName,
                           Enabled = p.PexaWorkspace.Enabled,
                           UpdatedDate = p.UpdatedDate,
                           UpdatedByUserId = (int?)p.UpdatedByUserId
                       }),
                       //m.IsDigiDocs,
                       //m.MatterBillable,
                       //m.BillingLocked,
                       //m.Certified,
                       //m.CertifiedDate,
                       //m.Settled,
                       m.SettlementScheduleId,
                       SettlementDate = (DateTime?)m.SettlementSchedule.SettlementDate,
                       SettlementTime = (TimeSpan?)m.SettlementSchedule.SettlementTime,
                       //FastREFISettlementDate = (DateTime?)m.SettlementSchedule1.SettlementDate,
                       //FastREFISettlementTime = (TimeSpan?)m.SettlementSchedule1.SettlementTime,
                       //m.FileOpenedDate,
                       //m.CheckInstructionsDate,
                       //m.ReworkCount,
                       m.UpdatedDate,
                       //m.UpdatedByUserId,
                       //UpdatedByUsername = m.User1.Username,
                       //UpdatedByLastname = m.User1.Lastname,
                       //UpdatedByFirstname = m.User1.Firstname,
                       //m.StopReminders,
                       //m.MatterEscalatedFlag,
                       //m.StopAutomatedEmails,
                       //m.DisclosureDate,
                       //m.NCCPExpiryDate,
                       //RelationshipManagerDetails = m.Broker.BrokerRelationshipManagers.Select(x =>
                       //                     new MatterCustomEntities.RelationshipManagerLimitedView()
                       //                     {
                       //                         RelationshipManagerId = x.RelationshipManagerId,
                       //                         Firstname = x.RelationshipManager.PrimaryContact.Firstname,
                       //                         Lastname = x.RelationshipManager.PrimaryContact.Lastname,
                       //                         Email = x.RelationshipManager.PrimaryContact.Email,
                       //                         Mobile = x.RelationshipManager.PrimaryContact.Mobile,
                       //                         Phone = x.RelationshipManager.PrimaryContact.Phone,
                       //                         Fax = x.RelationshipManager.PrimaryContact.Fax
                       //                     }),

                       MatterTypes = m.MatterMatterTypes.Select(t => new { t.MatterMatterTypeId, t.MatterTypeId, t.MatterType.MatterTypeName }),
                       Parties = m.MatterParties.Select(p => new
                       {
                           p.MatterPartyId,
                           p.PartyTypeId,
                           p.PartyType.PartyTypeName,
                           p.IsIndividual,
                           p.Title,
                           p.Lastname,
                           p.Firstname,
                           p.CompanyName,
                           p.IsInternational,
                           p.StreetAddress,
                           p.Suburb,
                           p.StateId,
                           p.State.StateName,
                           p.Postcode,
                           p.Country,
                           p.Phone,
                           p.Mobile,
                           p.Fax,
                           p.Email
                       }),
                       Securities = m.MatterSecurities.Where(ms => !ms.Deleted).Select(s => new
                       {
                           s.MatterSecurityId,
                           s.MatterTypeId,
                           s.MatterType.MatterTypeName,
                           s.SettlementTypeId,
                           s.SettlementType.SettlementTypeName,
                           s.SecurityAssetType.SecurityAssetTypeName,
                           s.StreetAddress,
                           s.Suburb,
                           s.StateId,
                           s.State.StateName,
                           s.PostCode,
                           s.ValuationExpiryDate,
                           TitleRefs = s.MatterSecurityTitleRefs.Select(t => new { t.MatterSecurityTitleRefId, t.MatterSecurityId, t.TitleReference, t.LandDescription, t.IsRegistered }),
                           Parties = s.MatterSecurityParties.Select(x => new { PartyName = x.MatterParty.IsIndividual ? x.MatterParty.Title + " " + x.MatterParty.Firstname + " " + x.MatterParty.Lastname : x.MatterParty.CompanyName }),
                           s.InsuranceStartDate,
                           s.InsuranceEndDate
                       }),

                       //TrustTransactions = m.TrustTransactionItems.Select(t => new { t.TrustTransactionItemId, t.TransactionDirectionTypeId, t.Amount, t.PayerPayee, t.TrustTransactionStatusType.TrustTransactionStatusTypeName }),
                       LoanAccounts = m.MatterLoanAccounts.Select(l => new { l.MatterLoanAccountId, l.MatterId, l.LoanAccountNo, l.LoanDescription, l.LoanAmount, l.OriginalLoanAmount, l.FacilityTypeId, l.FacilityType.FacilityTypeName, l.PostSettlementFacilityNo }),
                       //DischargeIndicativePayouts = m.MatterIndicativePayoutFigures.Select(i => new { i.MatterIndicativePayoutFigureId, i.LoanAccNo, i.IndicativePayoutAmount }),
                       //DischargePayouts = m.MatterDischargePayouts.Select(p => new { p.MatterDischargePayoutId, p.MatterId, p.AccountNo, p.PayoutAmount, p.UpdatedDate, p.UpdatedByUserId }),
                       //DischargeOverrides = m.MatterDischargeBankedOverrides.Select(x => new
                       //{
                       //    MatterDischargeBankedOverrideId = x.MatterDischargeBankedOverrideId,
                       //    MatterId = x.MatterId,
                       //    AccountNo = x.AccountNo,
                       //    Amount = x.Amount,
                       //    Notes = x.Notes,
                       //    UpdatedDate = (DateTime?)x.UpdatedDate,
                       //    UpdatedByUserId = x.UpdatedByUserId
                       //}),
                       OtherPartyDetails = m.MatterOtherParties.Select(x => new { x.Name, x.Address, x.Contact, x.Email, x.Fax, x.Phone, x.Reference }),
                       //m.MatterPoints,
                       //MatterSpecialConditions = m.MatterSpecialConditions.Select(c => new { SpecialConditionId = c.MatterSpecialConditionId, SpecialConditionDesc = c.SpecialConditionDesc, IncludeLetterToBorrower = c.IncludeOnLetterToBorrower, IncludeFrontCover = c.IncludeOnFrontCover, IncludeLoanAgreement = c.IncludeOnLoanAgreement, Resolved = c.Resolved }),
                       //m.LoanTypeId,
                       //LoanTypeName = m.LoanTypeId.HasValue ? m.LoanType.LoanTypeName : null,
                       //m.IsMQConversion,
                       //m.CheckInstructionsDateTime,
                       //m.FHOGApplies,
                       //ToDoListItems = m.UserToDoListItems.Select(x => new
                       //{
                       //    x.UserToDoListItemId,
                       //    x.MatterId,
                       //    x.ItemDescription,
                       //    x.ItemNotes,
                       //    x.DueDate,
                       //    x.ItemAssignedToUserId,
                       //    ItemAssignedToUserName = x.User.Username,
                       //    x.ItemCreatedByUserId,
                       //    ItemCreatedByUserName = x.User1.Username,
                       //    x.ItemCreatedDate,
                       //    x.ItemUpdatedByUserId,
                       //    ItemUpdatedByUserName = x.User2.Username,
                       //    x.ItemUpdatedDate,
                       //    x.ToDoListItemStatusTypeId,
                       //    x.ToDoListItemStatusType.ToDoListItemStatusTypeName,
                       //    x.ReminderDate,
                       //    x.CreatedFromWFIssueId,
                       //    x.CreatedAtMatterWFComponentId
                       //}).Where(i => i.ToDoListItemStatusTypeId != (int)ToDoListStatusTypeEnum.Complete)
                   }).Take(1);

            MatterCustomEntities.MatterLimitedView response = mtQry.ToList()
                .Select(m => new MCE.MatterLimitedView(m.MatterId, m.MatterDescription, m.MatterTypeGroupName, m.DischargeTypeName,
                m.FileOwnerUser.UserId, m.FileOwnerUser.Username, m.FileOwnerUser.Lastname, m.FileOwnerUser.Firstname,
                m.LenderName,
                m.LenderDefPrimaryContact == null || !m.LenderDefPrimaryContact.PrimaryContactId.HasValue ? null : new MCE.PrimaryContactView(m.LenderDefPrimaryContact.PrimaryContactId ?? 0, m.LenderDefPrimaryContact.Lastname, m.LenderDefPrimaryContact.Firstname,
                            m.LenderDefPrimaryContact.Phone, m.LenderDefPrimaryContact.Mobile, m.LenderDefPrimaryContact.Fax, m.LenderDefPrimaryContact.Email, m.LenderDefPrimaryContact.UpdatedDate ?? new DateTime(),
                            m.LenderDefPrimaryContact.UpdatedByUserId ?? 0, m.LenderDefPrimaryContact.Username),

                m.LenderPrimaryContact == null || !m.LenderPrimaryContact.PrimaryContactId.HasValue ? null :
                            new MCE.PrimaryContactView(m.LenderPrimaryContact.PrimaryContactId ?? 0, m.LenderPrimaryContact.Lastname, m.LenderPrimaryContact.Firstname,
                            m.LenderPrimaryContact.Phone, m.LenderPrimaryContact.Mobile, m.LenderPrimaryContact.Fax, m.LenderPrimaryContact.Email, m.LenderPrimaryContact.UpdatedDate ?? new DateTime(),
                            m.LenderPrimaryContact.UpdatedByUserId ?? 0, m.LenderPrimaryContact.Username),
                m.BrokerId, Common.EntityHelper.GetFullName(m.BrokerLastname, m.BrokerFirstname),
                m.BrokerPrimaryContact == null || !m.BrokerPrimaryContact.PrimaryContactId.HasValue ? null : new MCE.PrimaryContactView(m.BrokerPrimaryContact.PrimaryContactId.Value, m.BrokerPrimaryContact.Lastname, m.BrokerPrimaryContact.Firstname, m.BrokerPrimaryContact.Phone, m.BrokerPrimaryContact.Mobile, m.BrokerPrimaryContact.Fax, m.BrokerPrimaryContact.Email, m.BrokerPrimaryContact.UpdatedDate.Value, m.BrokerPrimaryContact.UpdatedByUserId.Value, m.BrokerPrimaryContact.Username),
                m.BrokerPrimaryContact == null || !m.BrokerPrimaryContact.PrimaryContactId.HasValue ? null : new MCE.PrimaryContactView(m.BrokerPrimaryContact.PrimaryContactId.Value, m.BrokerPrimaryContact.Lastname, m.BrokerPrimaryContact.Firstname, m.BrokerPrimaryContact.Phone, m.BrokerPrimaryContact.Mobile, m.BrokerPrimaryContact.Fax, m.BrokerPrimaryContact.Email, m.BrokerPrimaryContact.UpdatedDate.Value, m.BrokerPrimaryContact.UpdatedByUserId.Value, m.BrokerPrimaryContact.Username),
                m.LenderRefNo,
                m.PexaDetail == null || !m.PexaDetail.NominatedSettlementDate.HasValue || !m.PexaDetail.MatterPexaDetailId.HasValue ? null : new MCE.MatterPexaDetailView() { MatterId = m.PexaDetail.MatterId, NominatedSettlementDate = m.PexaDetail.NominatedSettlementDate.Value, MatterPexaDetailId = m.PexaDetail.MatterPexaDetailId.Value, UpdatedByUserId = m.PexaDetail.UpdatedByUserId, UpdatedDate = m.UpdatedDate },
                m.PexaWorkspace.Select(p => new MCE.MatterPexaWorkspaceView() { MatterPexaWorkspaceId = p.MatterPexaWorkspaceId.Value, MatterId = p.MatterId, PexaWorkspaceId = p.PexaWorkspaceId.Value, PexaWorkspaceNo = p.PexaWorkspaceNo, PexaWorkspaceStatus = p.PexaWorkspaceStatus, Enabled = p.Enabled, UpdatedDate = p.UpdatedDate, UpdatedByUserId = p.UpdatedByUserId.Value }).ToList(),

                m.SettlementScheduleId, m.SettlementDate, m.SettlementTime, m.UpdatedDate,

                m.MatterTypes.Select(t => new MCE.MatterMatterTypeView(t.MatterMatterTypeId, m.MatterId, t.MatterTypeId, t.MatterTypeName)).ToList(),
                m.Parties.Select(p => new MCE.MatterPartyView(p.MatterPartyId, m.MatterId, p.PartyTypeId, p.PartyTypeName, p.IsIndividual, p.Title, p.Lastname, p.Firstname, p.CompanyName, p.IsInternational, p.StreetAddress,
                                            p.Suburb, p.StateId, p.StateName, p.Postcode, p.Country, p.Phone, p.Mobile, p.Fax, p.Email)).ToList(),
                m.Securities.Select(s => new MCE.MatterSecurityView(s.MatterSecurityId, m.MatterId, s.MatterTypeId, s.MatterTypeName, s.SettlementTypeId, s.SettlementTypeName, s.SecurityAssetTypeName, s.StreetAddress,
                                            s.Suburb, s.StateId, s.StateName, s.PostCode,
                                            s.TitleRefs.Select(t => new MatterCustomEntities.MatterSecurityTitleRefView(t.MatterSecurityTitleRefId, t.MatterSecurityId, t.TitleReference, t.LandDescription, t.IsRegistered)).ToList(),
                                            s.Parties.Select(x => x.PartyName).ToList(), s.ValuationExpiryDate, s.InsuranceStartDate, s.InsuranceEndDate)).ToList(),
                m.LoanAccounts.Select(l => new MatterCustomEntities.MatterLoanAccountView(l.MatterLoanAccountId, l.MatterId, l.LoanAccountNo, l.LoanDescription, l.LoanAmount, l.OriginalLoanAmount, l.FacilityTypeId, l.FacilityTypeId.HasValue ? l.FacilityTypeName : null, l.PostSettlementFacilityNo)).ToList(),

                m.OtherPartyDetails.Select(x => new MCE.MatterOtherPartyView { Name = x.Name, Address = x.Address, Contact = x.Contact, Email = x.Email, Fax = x.Fax, Phone = x.Phone, Reference = x.Reference }).FirstOrDefault()





                )).FirstOrDefault();

            //if (response.IsDischargeType == true)
            //{
            //    //check if actually at right workflow
            //    var qry = context.MatterDocuments.AsNoTracking().Where(x => x.MatterId == matterId);

            //    var mwfRep = new MatterWFRepository(context);
            //    var wfComps = mwfRep.GetMatterComponentsForMatters(qry.Select(x => x.MatterId).ToList());




            //    if (mwfRep.HasMilestoneCompleted(wfComps, (int)WFComponentEnum.SecurityPacketReceived))
            //    {

            //        //response.PayoutAmount = MatterDischargePayout

            //        response.SecurityDocuments = new List<MatterCustomEntities.MatterSecurityDocumentView>();

            //        foreach (var security in response.Securities)
            //        {
            //            security.SecurityDocuments = new List<MCE.MatterSecurityDocumentView>();
            //            List<MatterSecurityDocument> secDocs = context.MatterSecurityDocuments.Where(m => m.MatterSecurityId == security.MatterSecurityId).ToList();
            //            foreach (var secDoc in secDocs)
            //            {
            //                security.SecurityDocuments.Add(new MCE.MatterSecurityDocumentView
            //                {
            //                    MatterSecurityDocumentId = secDoc.MatterSecurityDocumentId,
            //                    MatterSecurityId = secDoc.MatterSecurityId,
            //                    RegisteredDocumentTypeId = secDoc.RegisteredDocumentTypeId,
            //                    DocumentName = secDoc.DocumentName,
            //                    StateName = secDoc.StateName,
            //                    UpdatedDate = secDoc.UpdatedDate,
            //                    UpdatedByUserId = secDoc.UpdatedByUserId,
            //                });


            //                response.SecurityDocuments.Add(new MCE.MatterSecurityDocumentView
            //                {
            //                    MatterSecurityDocumentId = secDoc.MatterSecurityDocumentId,
            //                    MatterSecurityId = secDoc.MatterSecurityId,
            //                    RegisteredDocumentTypeId = secDoc.RegisteredDocumentTypeId,
            //                    DocumentName = secDoc.DocumentName,
            //                    StateName = secDoc.StateName,
            //                    UpdatedDate = secDoc.UpdatedDate,
            //                    UpdatedByUserId = secDoc.UpdatedByUserId,
            //                });
            //            }
            //            //response.SecurityDocuments.Add(context.MatterSecurityDocuments.Where(m => m.MatterSecurityId == security.MatterSecurityId).FirstOrDefault());



            //        }
            //    }

            //}
            //response.LinkedMatters = GetLinkedMattersForMatterId(matterId);
            return response;
        }

        public int CalculateMatterDifficulty(MatterView matter)
        {
            int total = 1;
            return total;
        }

        public MatterWFCreateMatter AddMatterWFCreateMatter(MatterWFComponent mwfComp)
        {
            MatterWFCreateMatter mh = new MatterWFCreateMatter();
            Matter m = context.Matters.Find(mwfComp.MatterId);
            mh.MatterWFComponentId = mwfComp.MatterWFComponentId;
            mh.MatterDescription = m.MatterDescription;
            mh.MatterTypeGroupId = m.MatterGroupTypeId;
            mh.StateId = m.StateId;
            mh.FileOwnerUserId = m.FileOwnerUserId;
            mh.LenderId = m.LenderId;
            mh.MortMgrId = m.MortMgrId;
            mh.BrokerId = m.BrokerId;
            mh.LenderRefNo = m.LenderRefNo;
            mh.UpdatedDate = DateTime.Now;
            mh.UpdatedByUserId = 2;

            context.MatterWFCreateMatters.Add(mh);
            context.SaveChanges();
            return mh;
        }


        public IEnumerable<MCE.MatterEventView> GetMatterEvents(int matterId)
        {
                return context.MatterEvents.Where(m => m.MatterId == matterId)
                .Select(m => new
                {
                    m.MatterEventId,
                    m.MatterId,
                    m.MatterWFComponentId,
                    WFComponentId = (int?)m.MatterWFComponent.WFComponentId,
                    m.MatterWFComponent.WFComponent.WFComponentName,
                    m.MatterEventTypeId,
                    m.MatterEventType.MatterEventTypeName,
                    m.MatterEventStatusTypeId,
                    m.MatterEventStatusType.MatterEventStatusTypeName,
                    m.ReasonId,
                    ReasonTxt = "",
                    //m.Reason.ReasonTxt,
                    m.EventNotes,
                    m.EventDate,
                    m.EventByUserId,
                    EventByUsername = m.User.Username, 
                    EventByLastName = m.User.Lastname,
                    EventByFirstName = m.User.Firstname,
                    m.EventCreatedByNotes
                }).ToList()
                .Select(m => new MCE.MatterEventView(m.MatterEventId, m.MatterId, m.MatterWFComponentId, m.WFComponentId, m.WFComponentName, m.MatterEventTypeId,
                        m.MatterEventTypeName, m.MatterEventStatusTypeId, m.MatterEventStatusTypeName, m.ReasonId, m.ReasonTxt, m.EventNotes, m.EventDate, m.EventByUserId, m.EventByUsername, m.EventByLastName, m.EventByFirstName, m.EventCreatedByNotes))
                   .ToList();
        }

        public IEnumerable<GeneralCustomEntities.GeneralCheckList> GetGeneralCheckList_MatterGroupType(bool defValue, bool inclSelectAll)
        {
            try
            {
                var matterGroupTypesTmp = context.MatterTypes.AsNoTracking()
                            .Where(m => m.Enabled == true && m.IsMatterGroup)
                            .Select(m => new { m.MatterTypeId, m.MatterTypeName })
                            .ToList()
                            .Select(m => new GeneralCustomEntities.GeneralCheckList(defValue, m.MatterTypeId, m.MatterTypeName))
                            .ToList();


                if (inclSelectAll)
                    matterGroupTypesTmp.Insert(0, new GeneralCustomEntities.GeneralCheckList(defValue, -1, "-- Select All --"));

                return matterGroupTypesTmp;
            }
            catch (Exception ex)
            {
                Handlers.ErrorHandler.LogError(ex);
                return null;
            }

        }

        public IEnumerable<GeneralCustomEntities.GeneralCheckList> GetGeneralCheckList_MatterType(int? matterGroupTypeId, bool defValue, bool inclSelectAll)
        {
            try
            {
                List<GeneralCustomEntities.GeneralCheckList> matterTypesTmp = new List<GeneralCustomEntities.GeneralCheckList>();

                if (matterGroupTypeId.HasValue)
                {
                    matterTypesTmp = context.MatterTypes.AsNoTracking()
                                .Where(m => m.Enabled == true && m.MatterTypeGroupId == matterGroupTypeId)
                                .Select(m => new { m.MatterTypeId, m.MatterTypeName })
                                .ToList()
                                .Select(m => new GeneralCustomEntities.GeneralCheckList(defValue, m.MatterTypeId, m.MatterTypeName))
                                .ToList();
                }
                else
                {
                    matterTypesTmp = context.MatterTypes.AsNoTracking()
                               .Where(m => m.Enabled == true && m.MatterTypeGroupId.HasValue)
                               .Select(m => new { m.MatterTypeId, m.MatterTypeName })
                               .ToList()
                               .Select(m => new GeneralCustomEntities.GeneralCheckList(defValue, m.MatterTypeId, m.MatterTypeName))
                               .ToList();
                }

                if (inclSelectAll)
                    matterTypesTmp.Insert(0, new GeneralCustomEntities.GeneralCheckList(defValue, -1, "-- Select All --"));

                return matterTypesTmp;
            }
            catch (Exception ex)
            {
                Handlers.ErrorHandler.LogError(ex);
                return null;
            }

        }

        public IEnumerable<GeneralCustomEntities.GeneralCheckList> GetGeneralCheckList_MatterStatusType(bool checkedValue, bool inclSelectAll)
        {
            try
            {
                var resTmp = context.MatterStatusTypes
                            .Select(l => new { l.MatterStatusTypeId, l.MatterStatusTypeName })
                            .ToList()
                            .Select(l => new GeneralCustomEntities.GeneralCheckList(checkedValue, l.MatterStatusTypeId, l.MatterStatusTypeName))
                            .ToList();


                if (inclSelectAll)
                    resTmp.Insert(0, new GeneralCustomEntities.GeneralCheckList(checkedValue, -1, "-- Select All --"));

                return resTmp;
            }
            catch (Exception ex)
            {
                Handlers.ErrorHandler.LogError(ex);
                return null;
            }

        }

        public IEnumerable<MCE.MatterLedgerItemView> GetMatterLedgersView(IQueryable<MatterLedgerItem> matterLedgerItems)
        {
            return matterLedgerItems.Select(m => new
            {
                m.MatterLedgerItemId,
                m.MatterId,
                m.Matter.Lender.LenderName,
                m.Matter.MortMgr.MortMgrName,
                m.Matter.Settled,
                m.Matter.SettlementSchedule.SettlementDate,
                m.LedgerItemSourceTypeId,
                m.LedgerItemSourceType.LedgerItemSourceTypeName,
                m.TransactionTypeId,
                m.TransactionType.TransactionTypeName,
                m.Amount,
                m.GST,
                m.Description,
                m.PayableByTypeId,
                PayableByTypeName = m.PayableType.PayableTypeName,
                m.InvoiceId,
                m.Invoice.InvoiceNo,
                m.Invoice.InvoicePaidDate,
                m.UpdatedDate,
                m.UpdatedByUserId,
                UpdatedByUsername = m.User.Username,
                UpdatedByLastname = m.User.Lastname,
                UpdatedByFirstname = m.User.Firstname
            }).ToList()
            .Select(m => new MCE.MatterLedgerItemView(m.MatterLedgerItemId, m.MatterId, m.LenderName, m.MortMgrName, m.Settled, m.SettlementDate, m.LedgerItemSourceTypeId,
                        m.LedgerItemSourceTypeName, m.TransactionTypeId, m.TransactionTypeName, m.Amount, m.GST, m.Description, m.PayableByTypeId,
                        m.PayableByTypeName, m.InvoiceId, m.InvoiceNo, m.InvoicePaidDate, m.UpdatedDate, m.UpdatedByUserId, m.UpdatedByUsername,
                        Common.EntityHelper.GetFullName(m.UpdatedByLastname, m.UpdatedByFirstname)))
               .ToList();
        }

        public MCE.MatterFileOwnerView GetFileOwnerDetails(int matterId)
        {
            return context.Matters.AsNoTracking()
                .Where(x => x.MatterId == matterId)
                .Select(x => new
                {
                    x.FileOwnerUserId,
                    x.User.Firstname,
                    x.User.Lastname,
                    x.User.Username,
                    x.User.Phone,
                    x.User.Email
                }).ToList()
                .Select(x => new MCE.MatterFileOwnerView
                {
                    Email = x.Email,
                    FirstName = x.Firstname,
                    LastName = x.Lastname,
                    FullName = EntityHelper.GetFullName(x.Lastname, x.Firstname),
                    Phone = x.Phone,
                    UserId = x.FileOwnerUserId,
                    UserName = x.Username
                }).FirstOrDefault();

        }


        public void LinkPossibleMatters(int matterId, int lenderId, int origFileOwnerId, int newFileOwnerId, int newStateId, bool hasDocPrepRun, bool canChangeFileManager, List<MatterCustomEntities.PossibleLink> linkedMatters)
        {
            //handle our various emails here
            //var crossBorderExceptions = linkedMatters.Where(x => x.StateId != newStateId && (x.CanChangeFileOwner || canChangeFileManager)).Select(x=>x.MatterId).ToList();
            
            //if (crossBorderExceptions.Any() && lenderId == 41)
            //{
            //    EmailsService.SendPossibleLinkedFileManagerExceptionEmail("File is cross-border.", lenderId, matterId, crossBorderExceptions.ToList(), context);
            //}

            var changedFileOwners = linkedMatters.Where(x => x.DocPrepHasRun && x.FileOwnerUserId != x.OrigFileOwnerUserId).Select(m => m.MatterId).ToList();
            var matter = context.Matters.FirstOrDefault(m => m.MatterId == matterId);

            if (newFileOwnerId != origFileOwnerId)
            {
                matter.FileOwnerUserId = newFileOwnerId;
                matter.StateId = context.Users.FirstOrDefault(u=>u.UserId == newFileOwnerId).StateId;

                var mwfRep = new MatterWFRepository(context);

                int? matterWfCompViewId = mwfRep.GetFirstActiveComponentForMatter(matter.MatterId)?.MatterWFComponentId;
                if (!matterWfCompViewId.HasValue)
                {
                    matterWfCompViewId = mwfRep.GetLastCompletedComponentForMatter(matter.MatterId)?.MatterWFComponentId;
                }

                if (matterWfCompViewId.HasValue)
                {
                    context.SaveChanges();
                    XmlProcessor.CreateAnswers(matterId, matterWfCompViewId.Value, lenderId, matter.MatterGroupTypeId, isChangingAnswers: true);
                }
                string newFileOwnerName = context.Users.FirstOrDefault(u => u.UserId == newFileOwnerId)?.Fullname;
                string existingFileOwnerName = context.Users.FirstOrDefault(u => u.UserId == origFileOwnerId)?.Fullname;
                if (existingFileOwnerName != newFileOwnerName)
                {
                    context.MatterNotes.Add(new MatterNote()
                    {
                        IsPublic = false,
                        HighPriority = true,
                        MatterId = matterId,
                        MatterNoteTypeId = (int)MatterNoteTypeEnum.StatusUpdate,
                        NoteHeader = "File Manager Changed",
                        NoteBody = $"File manager for this file was changed when linking to Matter <b>{linkedMatters.FirstOrDefault().MatterId}</b><br/><b>File Manager was:</b> {existingFileOwnerName}<br/><b>New File Manager:</b> {newFileOwnerName}",
                        IsPinned = true,
                        UpdatedByUserId = (int)GlobalVars.CurrentUser.UserId,
                        UpdatedDate = DateTime.Now,
                        IsDeleted = false
                    });
                }

                if (hasDocPrepRun)
                {
                    changedFileOwners.Add(matterId);
                }
            }

            foreach (var changed in changedFileOwners)
            {
                EmailsService.SendPossibleLinkedFileManagerReRunDocPrepEmail(lenderId, changed, context);
            }

            LinkMatters(matterId, linkedMatters.Select(x => new MCE.LinkedMatterView() { MatterId = matterId, LinkedMatterId = x.MatterId, LinkedMatterTypeId = x.RestrictionTypeId, LinkedMatterFileOwnerId = x.FileOwnerUserId, LinkedMatterStateId = x.StateId, CanChangeFileManager = x.CanChangeFileOwner }).ToList(), clear: false);          
        }

        /// <summary>
        /// Get any matters that may appear to be a strong match for a particular matter description string.
        /// </summary>
        /// <param name="matterId"><see cref="Matter.MatterId"> of the Matter to find potential links for.</see></param>
        /// <returns>A list of <see cref="MCE.PossibleLink">s for all matters with more than two words matching the matter description. Exact matches return as 999 words matching as they're the strongest... elegant I know.</see></returns>
        public IEnumerable<MatterCustomEntities.PossibleLink> GetPossibleLinksForMatter(int matterId)
        {
            var mtDetails = context.Matters.Select(x => new { MatterId = x.MatterId, LenderId = x.LenderId }).FirstOrDefault(m => m.MatterId == matterId);
            MatterCustomEntities.MatterWFComponentView view = new MCE.MatterWFComponentView() { MatterId = mtDetails.MatterId, LenderId = mtDetails.LenderId };
            return GetPossibleLinksForMatterWFComponentView(view);
        }

        /// <summary>
        /// Get any matters that may appear to be a strong match for a particular matter description string.
        /// </summary>
        /// <param name="matterWFCompView">A <see cref="MCE.MatterWFComponentView"/> with all key details required for matching.</param>
        /// <returns>A list of <see cref="MCE.PossibleLink">s for all matters with more than two words matching the matter description. Exact matches return as 999 words matching as they're the strongest... elegant I know.</see></returns>
        public IEnumerable<MatterCustomEntities.PossibleLink> GetPossibleLinksForMatterWFComponentView(MatterCustomEntities.MatterWFComponentView matterWFCompView)
        {
            string uncleanedMtDesc = "";
            string mtDesc = "";
            List<int> existingLinks = new List<int>();
           
            uncleanedMtDesc = context.Matters.Select(x => new { x.MatterId, x.MatterDescription }).FirstOrDefault(m => m.MatterId == matterWFCompView.MatterId).MatterDescription;
            mtDesc = uncleanedMtDesc;
            
            var links = GetLinkedMattersForMatterId(matterWFCompView.MatterId);
            existingLinks = links.Select(x => x.MatterId).Concat(links.Select(x => x.LinkedMatterId)).ToList();
            
            mtDesc = RemoveMultipleSpacesAndSymbols(mtDesc);
            
            List<string> ignoredWords = new List<string>()
            {
                "THE",
                "TRUST",
                "FOR",
                "AND",
                "PTY",
                "LTD",
                "SUPER",
                "FUND",
                "TRUSTEE",
                "NOMINEES",
                "NOMINEE",
                "NSW",
                "ACT",
                "WA",
                "QLD",
                "NT",
                "TAS",
                "VIC",
                "SERVICES"
            };

            List<string> names = mtDesc.Split(' ').Where(x => !ignoredWords.Any(s => x.ToUpper() == s)).Distinct().ToList();

            names = names.Concat(context.Matters.FirstOrDefault(m => m.MatterId == matterWFCompView.MatterId).MatterParties.Select(x => x.Firstname + x.Lastname + x.CompanyName )).ToList();


            names = names.Distinct().Where(s=>!ignoredWords.Contains(s)).ToList();

            List<MatterCustomEntities.PossibleLink> allPossibleLinks = new List<MatterCustomEntities.PossibleLink>();

            //Get EXACT matches first
            DateTime cutoffDate = DateTime.Now.AddMonths(-3);
            string descToCheck = RemoveIgnoredWords(uncleanedMtDesc.Replace(" ", "").ToUpper(), ignoredWords);
            List<int> ignoredMilestones = new List<int>() { (int)WFComponentEnum.PrintAndCollateFile, (int)WFComponentEnum.PEXAWorkspace };
            var resultSet = context.Matters.Where(m => m.MatterId != matterWFCompView.MatterId
                                && !existingLinks.Any(x => x == m.MatterId)
                                && m.LenderId == matterWFCompView.LenderId
                                && m.UpdatedDate > cutoffDate
                                //&& m.MatterGroupTypeId == matterWFCompView.MatterTypeGroupId
                                && (m.MatterStatusTypeId == (int)Slick_Domain.Enums.MatterStatusTypeEnum.InProgress ||
                                    m.MatterStatusTypeId == (int)Slick_Domain.Enums.MatterStatusTypeEnum.OnHold)).ToList()
                                        .Select(x => new MatterCustomEntities.PossibleLink
                                        {
                                            Linked = false,
                                            MatterId = x.MatterId,
                                            FileOwnerUserId = x.FileOwnerUserId,
                                            StateId = x.StateId,
                                            MatterGroupTypeId = x.MatterGroupTypeId,
                                            CanChangeFileOwner = !x.MatterWFComponents.Any(m =>m.WFComponentId == (int)WFComponentEnum.SendProcessedDocs
                                                                && m.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.Complete),
                                            DocPrepHasRun = x.MatterWFComponents.Any(m => m.WFComponentId == (int)WFComponentEnum.DocPreparation
                                                                && m.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.Complete),
                                            CurrentMilestone = x.MatterStatusTypeId == (int)MatterStatusTypeEnum.OnHold ? "On Hold"
                                                 : "- "+ String.Join($"\n- ", x.MatterWFComponents.Where(m =>  x.MatterId == m.MatterId && !ignoredMilestones.Any(i=>i == m.WFComponentId) &&
                                            (m.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.InProgress || m.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.Starting))
                                            .Select(m => m.WFComponent.WFComponentName)),
                                            MatterDescription = " " + x.MatterDescription.Replace(", ", " ") + " ",
                                            LenderRefNo = x.LenderRefNo,
                                            LenderName = x.Lender.LenderName,
                                            LenderId = x.LenderId,
                                            MatterStatusTypeId = x.MatterStatusTypeId,
                                            OrigFileOwnerUserId = x.FileOwnerUserId,
                                            OrigStateId = x.StateId,
                                            MatchedWords = 999,
                                            MatchedWordList = new List<string> { descToCheck },
                                            RestrictionTypeId = (int)Slick_Domain.Enums.LinkedMatterTypeEnum.NotSameDay,
                                            SplitDescription = RemoveIgnoredWords(x.MatterDescription, ignoredWords).Split(' ').Where(i => !ignoredWords.Any(s => i.ToUpper() == s)).Distinct().ToList(),
                                            CombinedPartyString = " " + String.Join(" ",x.MatterParties.Select(p=>p.CompanyName?.Replace(" ","") + " " + p.Firstname?.Replace(" ", "") + " " + p.Lastname?.Replace(" ", "")))
                                        }).ToList();


            //get exact / near exact matches first, give them priority over near matches
            allPossibleLinks = resultSet.Where(m => RemoveIgnoredWords(m.MatterDescription.Replace(" ", "").ToUpper(), ignoredWords) == descToCheck ||
            m.MatterDescription.Replace(" ", "").ToUpper().Contains(uncleanedMtDesc.Replace(" ", "").ToUpper())
            || RemoveMultipleSpacesAndSymbols(m.MatterDescription).Replace(" ", "").ToUpper().Contains(RemoveMultipleSpacesAndSymbols(uncleanedMtDesc).Replace(" ", "").ToUpper())
            ).ToList();

            /*(m.MatterDescription.ToUpper().Contains(name.ToUpper() + " ") || m.MatterDescription.ToUpper().Contains(" " + name.ToUpper()) ||
                                                                            m.CombinedPartyString.ToUpper().Contains(name.ToUpper() + " ") || m.CombinedPartyString.ToUpper().Contains(" " + name.ToUpper()))*/

            //var test = resultSet.FirstOrDefault(m => m.MatterId == 3034760);

            foreach (var name in names)
            {
                List<MatterCustomEntities.PossibleLink> possibleLinksForName = new List<MatterCustomEntities.PossibleLink>();

                bool flag = false;

                //can be uncommented to work out why a matter is or isn't filtering in 
                //if (test != null)
                //{
                //    if (test.MatterDescription.ToUpper().Contains(" " + name.ToUpper() + " "))
                //    {
                //        flag = true;
                //    }

                //    if (test.CombinedPartyString.ToUpper().Contains(" " + name.ToUpper() + " "))
                //    {
                //        flag = true;
                //    }

                //    if (test.SplitDescription.Any(x => x.Replace(" ", "").ToUpper() == name.Replace(" ", "").ToUpper()))
                //    {
                //        flag = true;
                //    }
                //}


                possibleLinksForName = resultSet.Where(m => m.MatterId != matterWFCompView.MatterId && !existingLinks.Any(x => x == m.MatterId) &&
                                                                        m.LenderId == matterWFCompView.LenderId &&
                                                                            (m.MatterStatusTypeId == (int)Slick_Domain.Enums.MatterStatusTypeEnum.InProgress ||
                                                                            m.MatterStatusTypeId == (int)Slick_Domain.Enums.MatterStatusTypeEnum.OnHold) &&
                                                                            (m.MatterDescription.ToUpper().Contains(" " + name.ToUpper() + " ") ||
                                                                            m.CombinedPartyString.ToUpper().Contains(" " + name.ToUpper() + " ")
                                                                            || m.SplitDescription.Any(x => x.Replace(" ", "").ToUpper() == name.Replace(" ", "").ToUpper()))
                                                                            )
                                                                        .Select(x => new MatterCustomEntities.PossibleLink
                                                                        {
                                                                            Linked = false,
                                                                            MatterId = x.MatterId,
                                                                            MatterDescription = x.MatterDescription,
                                                                            DocPrepHasRun = x.DocPrepHasRun,
                                                                            LenderRefNo = x.LenderRefNo,
                                                                            LenderName = x.LenderName,
                                                                            LenderId = x.LenderId,
                                                                            FileOwnerUserId = x.FileOwnerUserId,
                                                                            OrigFileOwnerUserId = x.FileOwnerUserId,
                                                                            OrigStateId = x.StateId,
                                                                            CanChangeFileOwner = x.CanChangeFileOwner,
                                                                            CurrentMilestone = x.CurrentMilestone,
                                                                            StateId = x.StateId,
                                                                            RestrictionTypeId = (int)Slick_Domain.Enums.LinkedMatterTypeEnum.NotSameDay
                                                                        }).ToList();
                
                foreach (var link in possibleLinksForName)
                {
                    if (allPossibleLinks.Any(x => x.MatterId == link.MatterId))
                    {
                        var existingMatch = allPossibleLinks.FirstOrDefault(x => x.MatterId == link.MatterId);
                        existingMatch.MatchedWords = existingMatch.MatchedWords + 1;
                        existingMatch.MatchedWordList.Add(name);
                    }
                    else
                    {

                        link.MatchedWords = 1;
                        link.MatchedWordList = new List<string>(){ name };
                        allPossibleLinks.Add(link);
                    }
                }
            }

            //var test2 = allPossibleLinks.Where(m => m.MatterId == 3034760);


            return allPossibleLinks.Where(x => x.MatchedWords >= 2).OrderByDescending(x => x.MatchedWords).ToList();

        }
        /// <summary>
        /// For use in automatically created matters, to add the log of the create matter to the <see cref="MatterWFComponent"/> for CreateMatter.
        /// <para>PREREQ: Matter has to have been saved - matterwfcomponent has to have been saved. Can be in same transaction though - doesn't need to be committed.</para>
        /// </summary>
        /// <param name="matterWFComponentId">ID of the Create Matter <see cref="MatterWFComponent"/></param>
        /// <param name="matterId"><see cref="Matter"/> ID for the saved matter</param>
        public void SaveMatterWFCreateMatterFromMatter(int matterWFComponentId, int matterId)
        {
            var mt = context.Matters.FirstOrDefault(m => m.MatterId == matterId);
            SaveMatterWFCreateMatterFromMatter(matterWFComponentId, mt);
        }
        /// <summary>
        /// For use in automatically created matters, to add the log of the create matter to the <see cref="MatterWFComponent"/> for CreateMatter.
        /// <para>PREREQ: Matter has to have been saved - matterwfcomponent has to have been saved. Can be in same transaction though - doesn't need to be committed.</para>
        /// </summary>
        /// <param name="matterWFComponentId">ID of the Create Matter <see cref="MatterWFComponent"/></param>
        /// <param name="matterId"><see cref="Matter"/>The entire entity of the saved matter</param>
        public void SaveMatterWFCreateMatterFromMatter(int matterWFComponentId, Matter mt)
        {
            var mth = new MatterWFCreateMatter() { MatterWFComponentId = matterWFComponentId };

            mth.MatterDescription = mt.MatterDescription;
            mth.MatterTypeGroupId = mt.MatterGroupTypeId;
            mth.StateId = mt.StateId;
            mth.InstructionStateId = mt.InstructionStateId;
            mth.InstructionsReceivedDate = mt.InstructionsReceivedDate;
            mth.FileOwnerUserId = mt.FileOwnerUserId;
            mth.LenderRefNo = mt.LenderRefNo;
            mth.SecondaryRefNo = mt.SecondaryRefNo;
            mth.IsTestFile = mt.IsTestFile;
            mth.LenderId = mt.LenderId;
            mth.MortMgrId = mt.MortMgrId;
            mth.BrokerId = mt.BrokerId;
            mth.LenderPrimaryContactId = mt.LenderPrimaryContactId;
            mth.BrokerPrimaryContactId = mt.BrokerPrimaryContactId;
            mth.MortMgrPrimaryContactId = mt.MortMgrPrimaryContactId;
            mth.CCNotifications = mt.CCNotifications;
            mth.LoanTypeId = mt.LoanTypeId;
            mth.UpdatedByUserId = 2;
            mth.UpdatedDate = DateTime.Now;

            context.MatterWFCreateMatters.Add(mth);
            context.SaveChanges();
        }


        public string RemoveMultipleSpacesAndSymbols(string input)
        {

            RegexOptions options = RegexOptions.None;
            Regex reg = new Regex("[^a-zA-Z' ]");
            input = reg.Replace(input, string.Empty);

            Regex regex = new Regex("[ ]{2,}", options);
            return input = regex.Replace(input, " ");
        }
        public string RemoveIgnoredWords(string input, List<string> ignoredWords)
        {
            return string.Join(" ", input.Split().Where(w => !ignoredWords.Contains(w, StringComparer.InvariantCultureIgnoreCase)));
        }
        public void LinkMattersRecursive(int matterId,List<MatterCustomEntities.LinkedMatterView> linkedMatterViews)
        {
            if (linkedMatterViews.Count() == 0) return;
            if (linkedMatterViews.Count!=linkedMatterViews.Distinct().Count()) linkedMatterViews = linkedMatterViews.Distinct().ToList();
            
            var existingLinks = GetLinkedMattersForMatterId(matterId);
            MatterCustomEntities.LinkedMatterView linkedMatterView = linkedMatterViews.ElementAt(0);
            if (existingLinks.Any(v => !(v.LinkedMatterId == linkedMatterView.MatterId)))
            {
                var linkedIdToRemove = existingLinks.First(v => !(v.LinkedMatterId == linkedMatterView.MatterId));
                var linkRowsToRemove = context.MatterLinks.Where(l => linkedIdToRemove.MatterId == l.LinkedMatterId).ToList();
                context.MatterLinks.RemoveRange(linkRowsToRemove);
            }

            if (!existingLinks.Any(l => l.LinkedMatterId == linkedMatterView.LinkedMatterId))
            {
                if (matterId != linkedMatterView.LinkedMatterId)
                    context.MatterLinks.Add( new MatterLink(){ MatterId = matterId, LinkedMatterId = linkedMatterView.LinkedMatterId, MatterLinkTypeId = linkedMatterView.LinkedMatterTypeId });
            }
            foreach (var existing in context.MatterLinks.Where(l => (l.LinkedMatterId == linkedMatterView.LinkedMatterId && l.MatterId == matterId) || (l.MatterId == linkedMatterView.LinkedMatterId && l.LinkedMatterId == matterId)))
                existing.MatterLinkTypeId = linkedMatterView.LinkedMatterTypeId;
            linkedMatterViews.RemoveAt(0);
            LinkMattersRecursive(matterId, linkedMatterViews);
        }






        public List<int> LinkMatters(int matterId, List<MatterCustomEntities.LinkedMatterView> linkedMatters, bool clear=false)
        {
            linkedMatters = linkedMatters.Distinct().ToList();  
            //first check that we aren't adding dupes to the database

            List<int> mattersChanged = new List<int>();

            var existingLinks = GetLinkedMattersForMatterId(matterId);
                
            var deletedItems = existingLinks.Where(l => !linkedMatters.Any(x => x.LinkedMatterId == l.LinkedMatterId));

            var mwfRep = new MatterWFRepository(context);
            //firstly resolve any file owner changes that are required
            foreach(var link in linkedMatters)
            {
                if(link.LinkedMatterFileOwnerId > 0)
                {
                    Matter linkingMatter = context.Matters.FirstOrDefault(m => m.MatterId == link.LinkedMatterId);
                    Matter thisMatter = context.Matters.FirstOrDefault(m => m.MatterId == matterId);
                    if (linkingMatter.FileOwnerUserId != link.LinkedMatterFileOwnerId && linkingMatter.MatterGroupTypeId == thisMatter.MatterId)
                    {
                        string existingFileOwnerName = context.Users.FirstOrDefault(u => u.UserId == linkingMatter.FileOwnerUserId).Fullname;
                        string newFileOwnerName = context.Users.FirstOrDefault(u => u.UserId == link.LinkedMatterFileOwnerId).Fullname;

                        linkingMatter.FileOwnerUserId = link.LinkedMatterFileOwnerId;
                        linkingMatter.StateId = context.Users.FirstOrDefault(u => u.UserId == link.LinkedMatterFileOwnerId).StateId;

                        int? matterWfCompViewId = mwfRep.GetFirstActiveComponentForMatter(linkingMatter.MatterId)?.MatterWFComponentId;
                        if (!matterWfCompViewId.HasValue)
                        {
                            matterWfCompViewId = mwfRep.GetLastCompletedComponentForMatter(linkingMatter.MatterId)?.MatterWFComponentId;
                        }
                        if (matterWfCompViewId.HasValue)
                        {
                            context.SaveChanges();
                            XmlProcessor.CreateAnswers(linkingMatter.MatterId, matterWfCompViewId.Value, linkingMatter.LenderId, linkingMatter.MatterGroupTypeId, isChangingAnswers: true);
                        }
                        if (existingFileOwnerName != newFileOwnerName)
                        {
                            context.MatterNotes.Add(new MatterNote()
                            {
                                IsPublic = false,
                                HighPriority = true,
                                MatterId = link.LinkedMatterId,
                                MatterNoteTypeId = (int)MatterNoteTypeEnum.StatusUpdate,
                                NoteHeader = "File Manager Changed",
                                NoteBody = $"File manager for this file was changed when linking to Matter <b>{matterId}</b><br/><b>File Manager was:</b> {existingFileOwnerName}<br/><b>New File Manager:</b> {newFileOwnerName}",
                                IsPinned = true,
                                UpdatedByUserId = (int)GlobalVars.CurrentUser.UserId,
                                UpdatedDate = DateTime.Now,
                                IsDeleted = false
                            });
                        }
                    }
                }
            }


            //next do the wipe - this doesn't happen on the recursive linkings
            if (clear)
            {

                //remove all links related to file first and then rebuild
                foreach (var del in deletedItems)
                {
                    context.MatterLinks.RemoveRange(context.MatterLinks.Where(m => (m.MatterId == del.MatterId && m.LinkedMatterId == del.LinkedMatterId) || (m.MatterId == del.LinkedMatterId && m.LinkedMatterId == del.MatterId)));
                    context.SaveChanges();
                }
            }
            else
            {
                deletedItems = new List<MCE.LinkedMatterView>();
            }



            //add newly needed links
            foreach (var linkedMatter in linkedMatters)
            {
                if (linkedMatter.MatterId == linkedMatter.LinkedMatterId) continue;
                if (matterId != linkedMatter.LinkedMatterId)
                {
                    var linkedLinks = GetLinkedMattersForMatterId(linkedMatter.LinkedMatterId).Where(w => w.LinkedMatterId != matterId).Select(x => new MatterCustomEntities.LinkedMatterView() { MatterId = matterId, LinkedMatterId = x.LinkedMatterId, LinkedMatterTypeId = x.LinkedMatterTypeId }).ToList();

                    if (!context.MatterLinks.Any(x => (x.MatterId == matterId && x.LinkedMatterId == linkedMatter.LinkedMatterId) || (x.MatterId == linkedMatter.LinkedMatterId && x.LinkedMatterId == matterId)))
                    {
                        context.MatterLinks.Add(new MatterLink { MatterId = matterId, LinkedMatterId = linkedMatter.LinkedMatterId, MatterLinkTypeId = linkedMatter.LinkedMatterTypeId });

                        mattersChanged.Add(linkedMatter.LinkedMatterId);

                        context.SaveChanges();

                        if (linkedLinks.Any(x => !linkedMatters.Select(l => l.LinkedMatterId).Contains(x.LinkedMatterId)))
                        {
                            foreach(var toLink in linkedLinks.Where(x => !linkedMatters.Select(l => l.LinkedMatterId).Contains(x.LinkedMatterId)).ToList())
                            {
                                if (!context.MatterLinks.Any(x => (x.MatterId == toLink.MatterId && x.LinkedMatterId == toLink.LinkedMatterId) || (x.MatterId == toLink.LinkedMatterId && x.LinkedMatterId == toLink.MatterId)))
                                {
                                    if (!deletedItems.Any(l => l.LinkedMatterId == toLink.LinkedMatterId))
                                    {
                                        mattersChanged = mattersChanged.Concat(LinkMatters(matterId, new List<MCE.LinkedMatterView>() { new MCE.LinkedMatterView() { MatterId = toLink.MatterId, LinkedMatterId = toLink.LinkedMatterId, LinkedMatterTypeId = toLink.LinkedMatterTypeId } }, false)).ToList();
                                        context.SaveChanges();
                                    }
                                }
                            }
                        }

                    }

                    existingLinks = GetLinkedMattersForMatterId(matterId);

                    foreach (var additional in mattersChanged.Distinct())
                    {
                        foreach (var existing in existingLinks)
                        {
                            if (!context.MatterLinks.Any(x => (x.MatterId == existing.LinkedMatterId && x.LinkedMatterId == additional) || (x.MatterId == additional && x.LinkedMatterId == existing.LinkedMatterId)))
                            {
                                if (!deletedItems.Any(l => l.LinkedMatterId == additional))
                                {
                                    mattersChanged = mattersChanged.Concat(LinkMatters(existing.LinkedMatterId, new List<MCE.LinkedMatterView>() { new MCE.LinkedMatterView() { MatterId = existing.LinkedMatterId, LinkedMatterId = additional, LinkedMatterTypeId = existing.LinkedMatterTypeId } }, false)).ToList();
                                    context.SaveChanges();
                                }
                            }
                        }
                    }

                    //finally do the same thing in reverse direction to propagate links that way too
                    //mattersChanged = mattersChanged.Concat(LinkMatters(linkedMatter.LinkedMatterId, new List<MCE.LinkedMatterView>() { new MCE.LinkedMatterView() { MatterId = linkedMatter.LinkedMatterId, LinkedMatterId = linkedMatter.MatterId != 0 ? linkedMatter.MatterId : matterId, LinkedMatterTypeId = linkedMatter.LinkedMatterTypeId } }, false)).ToList();
                    //context.SaveChanges();
                }

            }

            return mattersChanged.Concat(deletedItems.Select(x=>x.LinkedMatterId)).Distinct().ToList();
            ////update existing
            //foreach (var linkedMatter in linkedMatters.Where(x => existingLinks.Any(m => m.LinkedMatterId == x.LinkedMatterId)))
            //{
            //    foreach(var existing in context.MatterLinks.Where(l => (l.LinkedMatterId == linkedMatter.LinkedMatterId && l.MatterId == matterId) || (l.MatterId == linkedMatter.LinkedMatterId && l.LinkedMatterId == matterId)))
            //    {
            //        existing.MatterLinkTypeId = linkedMatter.LinkedMatterTypeId;
            //    }
            //}
        }

        public List<MatterCustomEntities.LinkedMatterView> GetLinkedMattersForMatterId(int matterId, int? lenderId = null)
        {
            List<MatterCustomEntities.LinkedMatterView> retList = new List<MatterCustomEntities.LinkedMatterView>();

            retList = context.MatterLinks.Where(x => x.MatterId == matterId && (!lenderId.HasValue || x.Matter1.LenderId == lenderId.Value)).Select(x => 
                new MatterCustomEntities.LinkedMatterView
                {
                    MatterId = matterId, LinkedMatterId = x.LinkedMatterId,
                    LinkedMatterTypeId = x.MatterLinkTypeId,
                    LinkedMatterGroupTypeId = x.Matter1.MatterGroupTypeId,
                    LinkedMatterTypeName = x.MatterLinkType.MatterLinkTypeName,
                    LinkedMatterDesc = x.Matter1.MatterDescription,
                    LinkedLenderRef = x.Matter1.LenderId != 139 || x.Matter1.MortMgrId == 49 ? x.Matter1.LenderRefNo : x.Matter1.SecondaryRefNo,
                    LinkedFileOwnerName = x.Matter1.User.Firstname.Trim() + " " + x.Matter1.User.Lastname.Trim(),
                    LinkedSettlementDate = x.Matter1.SettlementScheduleId.HasValue ? x.Matter1.SettlementSchedule.SettlementDate : (DateTime?)null,
                    LinkedMatterStatusTypeId = x.Matter1.MatterStatusTypeId,
                    LinkedMatterStatusType = x.Matter1.MatterStatusType.MatterStatusTypeDesc.ToUpper(),
                    LinkedSettlementBooked = x.Matter1.SettlementScheduleId.HasValue,
                    LinkedMatterSecurityList = x.Matter1.MatterSecurities.Where(s=>!s.Deleted).Select(s=>s.StreetAddress +", " + s.Suburb + ", " + s.PostCode + " " + s.State.StateName).ToList(),
                    LinkedMatterSettled = x.Matter1.Settled,
                    LinkedMatterTypes = x.Matter1.MatterMatterTypes.Select(m => m.MatterType.MatterTypeName).ToList(),
                    SameDayRequired = x.MatterLinkTypeId == (int)Slick_Domain.Enums.LinkedMatterTypeEnum.SameDay,
                }).ToList();
            var retList2 =
                context.MatterLinks.Where(x => x.LinkedMatterId == matterId && (!lenderId.HasValue || x.Matter.LenderId == lenderId.Value)).Select(x => new MatterCustomEntities.LinkedMatterView
                {
                    MatterId = matterId,
                    LinkedMatterId = x.MatterId,
                    LinkedMatterDesc = x.Matter.MatterDescription,
                    LinkedMatterGroupTypeId = x.Matter.MatterGroupTypeId,
                    LinkedLenderRef = x.Matter.LenderId != 139 || x.Matter.MortMgrId == 49 ? x.Matter.LenderRefNo : x.Matter.SecondaryRefNo,
                    LinkedFileOwnerName = x.Matter.User.Firstname.Trim() + " " + x.Matter.User.Lastname.Trim(),
                    LinkedSettlementDate = x.Matter.SettlementScheduleId.HasValue ? x.Matter.SettlementSchedule.SettlementDate : (DateTime?)null,
                    LinkedSettlementBooked = x.Matter.SettlementScheduleId.HasValue,
                    LinkedMatterSettled = x.Matter.Settled,
                    LinkedMatterTypeId = x.MatterLinkTypeId,
                    LinkedMatterStatusTypeId = x.Matter.MatterStatusTypeId,
                    LinkedMatterStatusType = x.Matter.MatterStatusType.MatterStatusTypeDesc.ToUpper(),
                    LinkedMatterSecurityList = x.Matter.MatterSecurities.Where(s => !s.Deleted).Select(s => s.StreetAddress + ", " + s.Suburb + ", " + s.PostCode + " " + s.State.StateName).ToList(),
                    LinkedMatterTypeName = x.MatterLinkType.MatterLinkTypeName,
                    LinkedMatterTypes = x.Matter.MatterMatterTypes.Select(m => m.MatterType.MatterTypeName).ToList(),

                    SameDayRequired = x.MatterLinkTypeId == (int)Slick_Domain.Enums.LinkedMatterTypeEnum.SameDay
                }).ToList(); //it's a two way street so get both ways
            var toReturn = retList.Concat(retList2).ToList();
            foreach(var item in toReturn)
            {
                item.LinkedMatterTypeNames = String.Join(", ",item.LinkedMatterTypes);
                if (item.LinkedMatterSecurityList != null && item.LinkedMatterSecurityList.Any())
                {
                    item.LinkedMatterSecurities = String.Join("\n", item.LinkedMatterSecurityList);
                }
                else
                {
                    item.LinkedMatterSecurities = null;
                }
            }
            
            return toReturn.OrderBy(l=>l.LinkedMatterId).ToList();
        }

        public int GetNextMatterId()
        {
            int nextMatterId = int.Parse(GlobalVars.GetGlobalTxtVar("UAT_MinTestingOPMatterId", context));
            var matterId = context.Matters.Where(x => x.MatterId < nextMatterId).OrderByDescending(x => x.MatterId).FirstOrDefault()?.MatterId;
            if (matterId == null)
            {
                matterId = 1;
            }
            else
            {
                matterId += 1;
            }

            return matterId.Value;
        }

        //public IEnumerable<MCE.MatterLedgerItemView> GetMatterLedgerViewForMatter(int matterId, int invoiceStatusTypeId, DateTime? dateFrom, DateTime? dateTo)
        //{
        //    IQueryable<MatterLedgerItem> ledgerItems = context.MatterLedgerItems.AsNoTracking().Where(m => m.MatterId == matterId);

        //    if (invoiceStatusTypeId != -1)
        //    {
        //        switch (invoiceStatusTypeId)
        //        {
        //            case 0:     //Not Invoiced
        //                ledgerItems = ledgerItems.Where(m => !m.InvoiceId.HasValue);
        //                break;
        //            case 1:     //Invoiced but not paid
        //                ledgerItems = ledgerItems.Where(m => m.InvoiceId.HasValue && !m.Invoice.InvoicePaidDate.HasValue);
        //                break;
        //            case 2:     //Invoiced and paid
        //                ledgerItems = ledgerItems.Where(m => m.InvoiceId.HasValue && m.Invoice.InvoicePaidDate.HasValue);
        //                break;
        //        }
        //    }
        //    if (dateFrom.HasValue)
        //        ledgerItems = ledgerItems.Where(m => m.Matter.SettlementSchedule.SettlementDate >= dateFrom);

        //    if (dateTo.HasValue)
        //        ledgerItems = ledgerItems.Where(m => m.Matter.SettlementSchedule.SettlementDate <= dateTo);

        //    return GetMatterLedgersView(ledgerItems);
        //}
        //public IEnumerable<MCE.MatterLedgerItemView> GetMatterLedgerViewForMatters(IEnumerable<GeneralCustomEntities.GeneralCheckList> lenderList,
        //                                                                                        IEnumerable<GeneralCustomEntities.GeneralCheckList> mortMgrList,
        //                                                                                        int invoiceStatusTypeId,
        //                                                                                        DateTime? dateFrom, DateTime? dateTo)
        //{

        //    //Are any lenders or MM's checked?
        //    if (lenderList.Where(l => l.IsChecked == true).Count() == 0 && mortMgrList.Where(l => l.IsChecked == true).Count() == 0)
        //        return null;

        //    IQueryable<MatterLedgerItem> ledgerItems = context.MatterLedgerItems.AsNoTracking();

        //    //Lender List selections
        //    if (lenderList.Where(l => l.IsChecked == true).Count() > 0 && lenderList.Where(l=>l.Id== -1 && l.IsChecked).Count() == 0 )
        //    {
        //        //Must be some Lenders selected, but not the Select-All
        //        var llIst = lenderList.Where(l => l.IsChecked == true).Select(l => l.Id).ToList();

        //        ledgerItems = ledgerItems.Where(m => llIst.Contains((m.Matter.LenderId)));

        //    }

        //    //Mortgage Mgr selections
        //    if (mortMgrList.Where(l => l.IsChecked == true).Count() > 0 && mortMgrList.Where(l => l.Id == -1 && l.IsChecked).Count() == 0)
        //    {
        //        //Must be some Lenders selected, but not the Select-All
        //        var mmList = mortMgrList.Where(l => l.IsChecked == true).Select(l => l.Id).ToList();

        //        //Mortgage Mgr can be null. If the "include null" option is selected (value=0) need to include in the where
        //        if (mortMgrList.Where(l => l.Id == 0 && l.IsChecked).Count() == 1)
        //            ledgerItems = ledgerItems.Where(m => mmList.Contains((m.Matter.MortMgrId.Value)) || !m.Matter.MortMgrId.HasValue);
        //        else
        //            ledgerItems = ledgerItems.Where(m => mmList.Contains((m.Matter.MortMgrId.Value)));
        //    }

        //    if (invoiceStatusTypeId != -1)
        //    {
        //        switch (invoiceStatusTypeId)
        //        {
        //            case 0:     //Not Invoiced
        //                ledgerItems = ledgerItems.Where(m => !m.InvoiceId.HasValue);
        //                break;
        //            case 1:     //Invoiced but not paid
        //                ledgerItems = ledgerItems.Where(m => m.InvoiceId.HasValue && !m.Invoice.InvoicePaidDate.HasValue);
        //                break;
        //            case 2:     //Invoiced and paid
        //                ledgerItems = ledgerItems.Where(m => m.InvoiceId.HasValue && m.Invoice.InvoicePaidDate.HasValue);
        //                break;
        //        }
        //    }

        //    if (dateFrom.HasValue)
        //        ledgerItems = ledgerItems.Where(m => m.Matter.SettlementSchedule.SettlementDate >= dateFrom);

        //    if (dateTo.HasValue)
        //        ledgerItems = ledgerItems.Where(m => m.Matter.SettlementSchedule.SettlementDate <= dateTo);

        //    return GetMatterLedgersView(ledgerItems);
        //}

        public IEnumerable<MCE.MatterDischargeView> GetAnticipatedMatterDischargesView(int? stateId, int? lenderId, DateTime settlementDate, IEnumerable<EntityCompacted> summaryStatusList)
        {
            //Are any of the status items checked?
            if (summaryStatusList.Where(l => l.IsChecked == true).Count() == 0)
                return new List<MCE.MatterDischargeView>();


            List<int> validMatterStatuses = new List<int>() { (int)MatterStatusTypeEnum.InProgress, (int)MatterStatusTypeEnum.OnHold };
            List<int> validDisplayStatuses = new List<int>() { (int)DisplayStatusTypeEnum.Default, (int)DisplayStatusTypeEnum.Display };
            List<int> validComponentStatuses = new List<int>() { (int)MatterWFComponentStatusTypeEnum.InProgress, (int)MatterWFComponentStatusTypeEnum.Starting, (int)MatterWFComponentStatusTypeEnum.Complete, (int)MatterWFComponentStatusTypeEnum.OnHold };




            IQueryable<Matter> qry = context.Matters.AsNoTracking().Where(s => s.MatterGroupTypeId == (int)Enums.MatterGroupTypeEnum.Discharge && s.MatterDischargeId.HasValue && !s.SettlementScheduleId.HasValue && validMatterStatuses.Contains(s.MatterStatusTypeId) 
                      && (stateId == null || s.StateId == stateId.Value) && (lenderId == null || s.LenderId == lenderId.Value)
                 && (settlementDate == null ||
                    (s.MatterPexaDetails.Any() && s.MatterPexaDetails.FirstOrDefault().NominatedSettlementDate == settlementDate) ||
                    (!s.MatterPexaDetails.Any() && s.AnticipatedSettlementDate.HasValue && DbFunctions.TruncateTime(s.AnticipatedSettlementDate.Value) == settlementDate) ||
                    (!s.MatterPexaDetails.Any() && !s.AnticipatedSettlementDate.HasValue && s.MatterWFComponents.FirstOrDefault(w => w.WFComponentId == (int)WFComponentEnum.FollowupOutstandingRequirements && validDisplayStatuses.Contains(w.DisplayStatusTypeId) && validComponentStatuses.Contains(w.WFComponentStatusTypeId) && w.MatterWFOutstandingReqs.Any()).MatterWFOutstandingReqs.FirstOrDefault().ExpectedSettlementDate == settlementDate)));


            qry = qry.Where(x => x.MatterDischargeId != null);

            return GetMatterDischargesView(qry);

        }

        public IEnumerable<MCE.MatterDischargeView> GetMatterDischargesView(int? stateId, int? lenderId, DateTime settlementDate, IEnumerable<EntityCompacted> summaryStatusList)
        {
            //Are any of the status items checked?
            if (summaryStatusList.Where(l => l.IsChecked == true).Count() == 0)
                return new List<MCE.MatterDischargeView>();

            IQueryable<SettlementSchedule> qry = context.SettlementSchedules.AsNoTracking().Where(m => m.Matter.MatterGroupTypeId == (int)Enums.MatterGroupTypeEnum.Discharge && m.SettlementDate == settlementDate &&
                                        m.Matter.MatterStatusTypeId != (int)Enums.MatterStatusTypeEnum.NotProceeding &&
                                        m.Matter.MatterStatusTypeId != (int)Enums.MatterStatusTypeEnum.OnHold &&
                                        ((m.SettlementScheduleStatusTypeId == (int)Enums.SettlementScheduleStatusTypeEnum.NotSettled) ||
                                          m.SettlementScheduleStatusTypeId == (int)Enums.SettlementScheduleStatusTypeEnum.Settled));


            //Summary List selections
            if (summaryStatusList.Where(l => l.Id == -1 && l.IsChecked).Count() == 0)
            {
                //Must be some status selected, but not the Select-All
                var llIst = summaryStatusList.Where(l => l.IsChecked == true).Select(l => l.Id).ToList();

                if (summaryStatusList.Where(s => s.Id == 0 && s.IsChecked).Count() > 0)
                    qry = qry.Where(m => llIst.Contains((m.DischargeSummary.DischargeSummaryStatusTypeId)) || !m.DischargeSummaryId.HasValue);
                else
                    qry = qry.Where(m => llIst.Contains((m.DischargeSummary.DischargeSummaryStatusTypeId)));
            }

            if (stateId.HasValue)
                qry = qry.Where(m => m.Matter.StateId == stateId);

            if (lenderId.HasValue)
                qry = qry.Where(m => m.Matter.LenderId == lenderId);

            qry = qry.Where(x => x.Matter.MatterDischargeId != null);

            return GetMatterDischargesView(qry);

        }
        public IEnumerable<MCE.MatterDischargeView> GetMatterDischargesView(int dischargeSummaryId)
        {
            IQueryable<SettlementSchedule> qry = context.SettlementSchedules.AsNoTracking().Where(m => m.DischargeSummaryId == dischargeSummaryId);
            return GetMatterDischargesView(qry);
        }

        public IEnumerable<MCE.MatterDischargeView> GetMatterDischargesView(IQueryable<SettlementSchedule> dischargeItemsQry)
        {
            return dischargeItemsQry
                .Select(m => new
                {
                    m.SettlementScheduleId,
                    m.MatterId,
                    m.Matter.MatterDescription,
                    m.Matter.MatterDischarge.DischargeTypeId,
                    m.Matter.MatterDischarge.DischargeType.DischargeTypeName,
                    m.Matter.LenderId,
                    m.Matter.Lender.LenderName,
                    m.Matter.StateId,
                    m.Matter.State.StateName,
                    SettlementDate = (DateTime?)m.SettlementDate,
                    DischargeSummaryId = (int?)m.DischargeSummaryId,
                    m.DischargeSummary.SummaryNo,
                    DischargeSummaryStatusTypeId = (int?)m.DischargeSummary.DischargeSummaryStatusTypeId,
                    m.DischargeSummary.DischargeSummaryStatusType.DischargeSummaryStatusTypeName,
                    m.Matter.MatterLoanAccounts,
                    m.UpdatedDate,
                    m.UpdatedByUserId,
                    m.User.Username
                })
                .ToList()
                .Select(m => new MCE.MatterDischargeView(m.SettlementScheduleId, m.MatterId, m.MatterDescription, m.DischargeTypeId, m.DischargeTypeName, m.LenderId,
                        m.LenderName, m.StateId, m.StateName, m.SettlementDate, m.DischargeSummaryId, m.SummaryNo, m.DischargeSummaryStatusTypeId, m.DischargeSummaryStatusTypeName,
                        m.MatterLoanAccounts, m.UpdatedDate, m.UpdatedByUserId, m.Username))
                .ToList();
        }
        public IEnumerable<MCE.MatterDischargeView> GetMatterDischargesView(IQueryable<Matter> dischargeItemsQry)
        {
            return dischargeItemsQry
                .Select(m => new
                {
                    m.SettlementScheduleId,
                    m.MatterId,
                    m.MatterDescription,
                    m.MatterDischarge.DischargeTypeId,
                    m.MatterDischarge.DischargeType.DischargeTypeName,
                    m.LenderId,
                    m.Lender.LenderName,
                    m.StateId,
                    m.State.StateName,
                    PexaDate = m.MatterPexaDetails.Select(n => new { n.NominatedSettlementDate, n.UpdatedDate }).OrderByDescending(o => o.UpdatedDate).FirstOrDefault(),
                    ManualAnticipatedDate = m.AnticipatedSettlementDate,
                    DischargeSummaryId = (int?)null,
                    SummaryNo = (string)null,
                    DischargeSummaryStatusTypeId = (int?)null,
                    DischargeSummaryStatusTypeName = (string)null,
                    m.MatterLoanAccounts,
                    m.UpdatedDate,
                    m.UpdatedByUserId,
                    m.User.Username
                })
                .ToList()
                .Select(m => new MCE.MatterDischargeView(0, m.MatterId, m.MatterDescription, m.DischargeTypeId, m.DischargeTypeName, m.LenderId,
                        m.LenderName, m.StateId, m.StateName, m.PexaDate?.NominatedSettlementDate ?? m.ManualAnticipatedDate ?? (DateTime?)null , m.DischargeSummaryId, m.SummaryNo, m.DischargeSummaryStatusTypeId, m.DischargeSummaryStatusTypeName,
                        m.MatterLoanAccounts, m.UpdatedDate, m.UpdatedByUserId, m.Username))
                .ToList();
        }

        public MCE.DischargeSummaryView GetDischargeSummaryView(int dischargeSummaryId)
        {
            IQueryable<DischargeSummary> qry = context.DischargeSummaries.Where(t => t.DischargeSummaryId == dischargeSummaryId);
            return GetDischargeSummariesView(qry).FirstOrDefault();
        }

        public IEnumerable<MCE.DischargeSummaryView> GetDischargeSummariesView(int? lenderId, DateTime? settlementFromDate, DateTime? settlementToDate, IEnumerable<EntityCompacted> summaryStatusList)
        {
            //Are any of the status items checked?
            if (summaryStatusList.Where(l => l.IsChecked == true).Count() == 0)
                return new List<MCE.DischargeSummaryView>();

            IQueryable<DischargeSummary> qry = context.DischargeSummaries.AsNoTracking();

            //Summary List selections
            if (summaryStatusList.Where(l => l.Id == -1 && l.IsChecked).Count() == 0)
            {
                //Must be some status selected, but not the Select-All
                var llIst = summaryStatusList.Where(l => l.IsChecked == true).Select(l => l.Id).ToList();

                qry = qry.Where(m => llIst.Contains((m.DischargeSummaryStatusTypeId)));
            }

            if (lenderId.HasValue)
                qry = qry.Where(m => m.LenderId == lenderId);

            if (settlementFromDate.HasValue)
                qry = qry.Where(m => m.SettlementDate >= settlementFromDate);
            if (settlementToDate.HasValue)
                qry = qry.Where(m => m.SettlementDate <= settlementToDate);

            return GetDischargeSummariesView(qry);

        }
        public IEnumerable<MCE.DischargeSummaryView> GetDischargeSummariesView(IQueryable<DischargeSummary> dischargeSummariesQry)
        {
            return dischargeSummariesQry
                .Select(m => new MCE.DischargeSummaryView
                {
                    DischargeSummaryId = m.DischargeSummaryId,
                    DischargeSummaryNo = m.SummaryNo,
                    LenderId = m.LenderId,
                    LenderName = m.Lender.LenderName,
                    SettlementDate = m.SettlementDate,
                    DischargeSummaryStatusTypeId = m.DischargeSummaryStatusTypeId,
                    DischargeSummaryStatusTypeName = m.DischargeSummaryStatusType.DischargeSummaryStatusTypeName,
                    Notes = m.Notes,
                    UpdatedDate = m.UpdatedDate,
                    UpdatedByUserId = m.UpdatedByUserId,
                    UpdatedByUsername = m.User.Username
                }).ToList();
        }

        public string BuildDischargeSummaryNo(MCE.MatterDischargeView mdv)
        {
            string lenderShortName = context.Lenders.FirstOrDefault(l => l.LenderId == mdv.LenderId).LenderNameShort;
            string summNo = lenderShortName + "/" + mdv.SettlementDate.Value.ToString("yyyy-MM-dd");

            int appendCount = 0;
            while (context.DischargeSummaries.Where(d => d.SummaryNo == summNo).Count() > 0)
            {
                appendCount++;
                summNo = lenderShortName + "/" + mdv.SettlementDate.Value.ToString("yyyy-MM-dd") + "/" + appendCount.ToString();
            }

            return summNo;
        }

        public IEnumerable<MCE.FundingRequestView> GetFundingRequestsView(int? lenderId, DateTime? settlementFromDate, DateTime? settlementToDate, IEnumerable<EntityCompacted> fundingRequestStatusList)
        {
            //Are any of the status items checked?
            if (fundingRequestStatusList.Where(l => l.IsChecked == true).Count() == 0)
                return new List<MCE.FundingRequestView>();

            IQueryable<FundingRequest> qry = context.FundingRequests.AsNoTracking();

            //Summary List selections
            if (fundingRequestStatusList.Where(l => l.Id == -1 && l.IsChecked).Count() == 0)
            {
                //Must be some status selected, but not the Select-All
                var llIst = fundingRequestStatusList.Where(l => l.IsChecked == true).Select(l => l.Id).ToList();

                qry = qry.Where(m => llIst.Contains((m.FundingRequestStatusTypeId)));
            }

            if (lenderId.HasValue)
                qry = qry.Where(m => m.LenderId == lenderId);

            if (settlementFromDate.HasValue)
                qry = qry.Where(m => m.SettlementDate >= settlementFromDate);
            if (settlementToDate.HasValue)
                qry = qry.Where(m => m.SettlementDate <= settlementToDate);

            return GetFundingRequestsView(qry);

        }
        public IEnumerable<MCE.FundingRequestView> GetFundingRequestsView(IQueryable<FundingRequest> fundingRequestsQry)
        {
            return fundingRequestsQry
                .Select(m => new MCE.FundingRequestView
                {
                    FundingRequestId = m.FundingRequestId,
                    FundingRequestNo = m.FundingRequestNo,
                    LenderId = m.LenderId,
                    LenderName = m.Lender.LenderName,
                    SettlementDate = m.SettlementDate,
                    FundingRequestStatusTypeId = m.FundingRequestStatusTypeId,
                    FundingRequestStatusTypeName = m.FundingRequestStatusType.FundingRequestStatusTypeName,
                    Notes = m.Notes,
                    UpdatedDate = m.UpdatedDate,
                    UpdatedByUserId = m.UpdatedByUserId,
                    UpdatedByUsername = m.User.Username,
                    TotalAmount = m.MatterLedgerItems.Any() ? m.MatterLedgerItems.Sum(x=>x.Amount) : 0M
                }).ToList();
        }
        public MCE.FundingRequestView GetFundingRequestView(int fundingRequestId)
        {
            IQueryable<FundingRequest> qry = context.FundingRequests.Where(t => t.FundingRequestId == fundingRequestId);
            return GetFundingRequestsView(qry).FirstOrDefault();
        }



        public IEnumerable<MCE.FundingRequestItemView> GetFundingRequestItemsViewByMatter(int matterId)
        {
            IQueryable<MatterLedgerItem> qry = context.MatterLedgerItems.AsNoTracking().Where(m => m.MatterId == matterId &&
                                        !m.FundingRequestId.HasValue &&
                                        m.PayableToAccountTypeId == (int)AccountTypeEnum.Trust &&
                                        m.PayableByTypeId == (int)PayableTypeEnum.Lender);

            return GetFundingRequestItemsView(qry);

        }
        public IEnumerable<MCE.FundingRequestItemView> GetFundingRequestItemsView(int? stateId, int? lenderId, DateTime settlementDate, int? fundingRequestStatusId)
        {
         
            IQueryable<MatterLedgerItem> qry = 
                context.MatterLedgerItems.AsNoTracking().Where(m => !m.Matter.IsTestFile && m.Matter.MatterGroupTypeId == (int)Enums.MatterGroupTypeEnum.NewLoan &&
                                        (m.Matter.SettlementSchedule.SettlementDate == settlementDate ||
                                         (m.ExpectedPaymentDate.HasValue && m.ExpectedPaymentDate == settlementDate)) &&
                                        (m.FundingRequestId == null || m.FundingRequest.SettlementDate == settlementDate) &&
                                        m.Matter.MatterStatusTypeId != (int)Enums.MatterStatusTypeEnum.NotProceeding &&
                                        m.Matter.MatterStatusTypeId != (int)Enums.MatterStatusTypeEnum.OnHold &&
                                        m.PayableToAccountTypeId == (int)AccountTypeEnum.Trust &&
                                        m.PayableByTypeId == (int)PayableTypeEnum.Lender &&
                                        ((m.Matter.SettlementSchedule.SettlementScheduleStatusTypeId == (int)Enums.SettlementScheduleStatusTypeEnum.NotSettled) ||
                                          m.Matter.SettlementSchedule.SettlementScheduleStatusTypeId == (int)Enums.SettlementScheduleStatusTypeEnum.Settled));



            qry = qry.Where(t => t.MatterLedgerItemStatusTypeId != (int)Enums.MatterLedgerItemStatusTypeEnum.Cancelled);

            if (stateId.HasValue)
                qry = qry.Where(m => m.Matter.StateId == stateId);

            if (lenderId.HasValue)
                qry = qry.Where(m => m.Matter.LenderId == lenderId);

            if (fundingRequestStatusId.HasValue)
                qry = qry.Where(m => m.FundingRequest.FundingRequestStatusTypeId == fundingRequestStatusId);

            return GetFundingRequestItemsView(qry).DistinctBy(x=>x.MatterLedgerItemId);

        }
        
        public IEnumerable<MCE.PossibleLetterToCustodianItemView> GetPossibleLetterToCustodianItemViewsForComp(MatterCustomEntities.MatterWFComponentView mwfComp, bool ignoreIssueDocs = true)
        {
            var settlementTypes = context.Matters.Where(m => m.MatterId == mwfComp.MatterId).SelectMany(x => x.MatterSecurities.Where(s => !s.Deleted).Select(s => s.SettlementTypeId)).ToList();

            return GetPossibleLetterToCustodianItemViews(context.PossibleDocumentForCustodians
                    .Where(x => x.Enabled &&
                    (x.LenderId == null || x.LenderId == mwfComp.LenderId) &&
                    (x.MortMgrId == null || x.MortMgrId == mwfComp.MortMgrId) &&
                    (ignoreIssueDocs == false || x.IssueOnly == false) && 
                    (x.SettlementTypeId == null || settlementTypes.Contains(x.SettlementTypeId.Value))                
                ));
        }
        public IEnumerable<MCE.PossibleLetterToCustodianItemView> GetAllPossibleLetterToCustodianItemViews()
        {
            return GetPossibleLetterToCustodianItemGridViews(context.PossibleDocumentForCustodians);
        }
        public IEnumerable<MCE.PossibleLetterToCustodianItemView> GetPossibleLetterToCustodianItemViews(IQueryable<PossibleDocumentForCustodian> qry)
        {
            return qry.Select(x => new MCE.PossibleLetterToCustodianItemView {
                Id = x.PossibleDocumentForCustodianId,
                ItemDescription = x.ItemDesc,
                LenderId = x.LenderId,
                LenderName = x.LenderId.HasValue ? x.Lender.LenderName : "< -- Any -- >",
                MortMgrId = x.MortMgrId,
                MortMgrName = x.MortMgrId.HasValue ? x.MortMgr.MortMgrName : "< -- Any -- >",
                SettlementTypeId = x.SettlementTypeId,
                SettlementTypeName = x.SettlementTypeId.HasValue ? x.SettlementType.SettlementTypeName : "< -- Any -- >",
                UpdatedById = x.UpdatedByUserId,
                UpdatedDate = x.UpdatedDate,
                UpdatedByUserName = x.User.Username,
                Enabled = x.Enabled,
                IssueOnly = x.IssueOnly,
                isDirty = false
            }).ToList();
        }
        public IEnumerable<MCE.PossibleLetterToCustodianItemView> GetPossibleLetterToCustodianItemGridViews(IQueryable<PossibleDocumentForCustodian> qry)
        {
            return qry.Select(x => new MCE.PossibleLetterToCustodianItemView
            {
                Id = x.PossibleDocumentForCustodianId,
                ItemDescription = x.ItemDesc,
                LenderId = x.LenderId ?? -1,
                LenderName = x.LenderId.HasValue ? x.Lender.LenderName : "< -- Any -- >",
                MortMgrId = x.MortMgrId ?? -1,
                MortMgrName = x.MortMgrId.HasValue ? x.MortMgr.MortMgrName : "< -- Any -- >",
                SettlementTypeId = x.SettlementTypeId ?? -1,
                SettlementTypeName = x.SettlementTypeId.HasValue ? x.SettlementType.SettlementTypeName : "< -- Any -- >",
                UpdatedById = x.UpdatedByUserId,
                UpdatedDate = x.UpdatedDate,
                UpdatedByUserName = x.User.Username,
                IssueOnly = x.IssueOnly,
                Enabled = x.Enabled,
                isDirty = false
            }).ToList();
        }


        public IEnumerable<MCE.PossibleLetterToCustodianIssueView> GetPossibleLetterToCustodianIssueViewsForComp(MatterCustomEntities.MatterWFComponentView mwfComp)
        {
            var settlementTypes = context.Matters.Where(m => m.MatterId == mwfComp.MatterId).SelectMany(x => x.MatterSecurities.Where(s => !s.Deleted).Select(s => s.SettlementTypeId)).ToList();

            return GetPossibleLetterToCustodianIssueViews(context.PossibleCustodianLetterIssues
                    .Where(x => x.Enabled &&
                    (x.LenderId == null || x.LenderId == mwfComp.LenderId) &&
                    (x.MortMgrId == null || x.MortMgrId == mwfComp.MortMgrId) &&
                    (x.SettlementTypeId == null || settlementTypes.Contains(x.SettlementTypeId.Value))
                ));
        }
        public IEnumerable<MCE.PossibleLetterToCustodianIssueView> GetAllPossibleLetterToCustodianIssueViews()
        {
            return GetPossibleLetterToCustodianIssueGridViews(context.PossibleCustodianLetterIssues);
        }
        public IEnumerable<MCE.PossibleLetterToCustodianIssueView> GetPossibleLetterToCustodianIssueGridViews(IQueryable<PossibleCustodianLetterIssue> qry)
        {
            return qry.Select(x => new MCE.PossibleLetterToCustodianIssueView
            {
                Id = x.PossibleCustodianLetterIssueId,
                ItemDescription = x.ItemDesc,
                LenderId = x.LenderId ?? -1,
                LenderName = x.LenderId.HasValue ? x.Lender.LenderName : "< -- Any -- >",
                MortMgrId = x.MortMgrId ?? -1,
                MortMgrName = x.MortMgrId.HasValue ? x.MortMgr.MortMgrName : "< -- Any -- >",
                SettlementTypeId = x.SettlementTypeId ?? -1,
                SettlementTypeName = x.SettlementTypeId.HasValue ? x.SettlementType.SettlementTypeName : "< -- Any -- >",
                UpdatedById = x.UpdatedByUserId,
                UpdatedDate = x.UpdatedDate,
                UpdatedByUserName = x.User.Username,
                Enabled = x.Enabled,
                isDirty = false
            }).ToList();
        }

        public IEnumerable<MCE.PossibleLetterToCustodianIssueView> GetPossibleLetterToCustodianIssueViews(IQueryable<PossibleCustodianLetterIssue> qry)
        {
            return qry.Select(x => new MCE.PossibleLetterToCustodianIssueView
            {
                Id = x.PossibleCustodianLetterIssueId,
                ItemDescription = x.ItemDesc,
                LenderId = x.LenderId,
                LenderName = x.LenderId.HasValue ? x.Lender.LenderName : "< -- Any -- >",
                MortMgrId = x.MortMgrId,
                MortMgrName = x.MortMgrId.HasValue ? x.MortMgr.MortMgrName : "< -- Any -- >",
                SettlementTypeId = x.SettlementTypeId,
                SettlementTypeName = x.SettlementTypeId.HasValue ? x.SettlementType.SettlementTypeName : "< -- Any -- >",
                UpdatedById = x.UpdatedByUserId,
                UpdatedDate = x.UpdatedDate,
                UpdatedByUserName = x.User.Username,
                Enabled = x.Enabled,
                isDirty = false
            }).ToList();
        }

        public IEnumerable<MCE.CustodianDocumentMatterDocumentLinkView> GetCustodianDocumentMatterDocumentLinkViewsForMatter(int lenderId, int? mortMgrId)
        {
            return GetCustodianDocumentMatterDocuments(context.CustodianDocumentMatterDocumentLinks
                .Where(x => (!x.LenderId.HasValue || x.LenderId.Value == lenderId) && (!x.MortMgrId.HasValue || x.MortMgrId.Value == mortMgrId)));
        }

        public IEnumerable<MCE.CustodianDocumentMatterDocumentLinkView> GetAllCustodianDocumentMatterDocumentLinkView()
        {
            return GetCustodianDocumentMatterDocuments(context.CustodianDocumentMatterDocumentLinks);
        }

        public IEnumerable<MCE.CustodianDocumentMatterDocumentLinkView> GetCustodianDocumentMatterDocuments(IQueryable<CustodianDocumentMatterDocumentLink> qry)
        {
            return qry.Select(x => new MCE.CustodianDocumentMatterDocumentLinkView()
            {
                Id = x.CustodianDocumentMatterDocumentLinkId,
                CustodianDocumentName = x.CustodianDocumentName,
                MatterDocumentName = x.MatterDocumentName,
                LenderId = x.LenderId,
                MortMgrId = x.MortMgrId,
                CheckAdditionalDocs = x.CheckAdditionalDocs,
                CheckSettlementPack = x.CheckSettlementPack,
                isDirty = false
            });
        }

        public IEnumerable<MCE.PossibleLetterToCustodianActionView> GetPossibleLetterToCustodianActionViewsForComp(MatterCustomEntities.MatterWFComponentView mwfComp)
        {
            var settlementTypes = context.Matters.Where(m => m.MatterId == mwfComp.MatterId).SelectMany(x => x.MatterSecurities.Where(s => !s.Deleted).Select(s => s.SettlementTypeId)).ToList();

            return GetPossibleLetterToCustodianActionViews(context.PossibleCustodianLetterActions
                    .Where(x => x.Enabled &&
                    (x.LenderId == null || x.LenderId == mwfComp.LenderId) &&
                    (x.MortMgrId == null || x.MortMgrId == mwfComp.MortMgrId) &&
                    (x.SettlementTypeId == null || settlementTypes.Contains(x.SettlementTypeId.Value))
                ));
        }
        public IEnumerable<MCE.PossibleLetterToCustodianActionView> GetAllPossibleLetterToCustodianActionViews()
        {
            return GetPossibleLetterToCustodianActionGridViews(context.PossibleCustodianLetterActions);
        }
        public IEnumerable<MCE.PossibleLetterToCustodianActionView> GetPossibleLetterToCustodianActionGridViews(IQueryable<PossibleCustodianLetterAction> qry)
        {
            return qry.Select(x => new MCE.PossibleLetterToCustodianActionView
            {
                Id = x.PossibleCustodianLetterActionId,
                ItemDescription = x.ItemDesc,
                LenderId = x.LenderId ?? -1,
                LenderName = x.LenderId.HasValue ? x.Lender.LenderName : "< -- Any -- >",
                MortMgrId = x.MortMgrId ?? -1,
                MortMgrName = x.MortMgrId.HasValue ? x.MortMgr.MortMgrName : "< -- Any -- >",
                SettlementTypeId = x.SettlementTypeId ?? -1,
                SettlementTypeName = x.SettlementTypeId.HasValue ? x.SettlementType.SettlementTypeName : "< -- Any -- >",
                UpdatedById = x.UpdatedByUserId,
                UpdatedDate = x.UpdatedDate,
                UpdatedByUserName = x.User.Username,
                Enabled = x.Enabled,
                isDirty = false
            }).ToList();
        }

        public IEnumerable<MCE.PossibleLetterToCustodianActionView> GetPossibleLetterToCustodianActionViews(IQueryable<PossibleCustodianLetterAction> qry)
        {
            return qry.Select(x => new MCE.PossibleLetterToCustodianActionView
            {
                Id = x.PossibleCustodianLetterActionId,
                ItemDescription = x.ItemDesc,
                LenderId = x.LenderId,
                LenderName = x.LenderId.HasValue ? x.Lender.LenderName : "< -- Any -- >",
                MortMgrId = x.MortMgrId,
                MortMgrName = x.MortMgrId.HasValue ? x.MortMgr.MortMgrName : "< -- Any -- >",
                SettlementTypeId = x.SettlementTypeId,
                SettlementTypeName = x.SettlementTypeId.HasValue ? x.SettlementType.SettlementTypeName : "< -- Any -- >",
                UpdatedById = x.UpdatedByUserId,
                UpdatedDate = x.UpdatedDate,
                UpdatedByUserName = x.User.Username,
                Enabled = x.Enabled,
                isDirty = false
            }).ToList();
        }
        private class FundingRequestWarningView
        {

        }
        public List<int> EmailFundingRequestWarnings(int lenderId, List<Tuple<int, DateTime>> alreadyEmailed)
        {

            var emailAddresses = context.UserTeams.Where(t => t.Team.TeamName.ToUpper().Trim() == "AFSH FUNDING").Select(u => u.User.Email).ToList();
            if (!emailAddresses.Any()) emailAddresses = new List<string> { "george.kogios@msanational.com.au" };
            var emRep = new EmailsRepository(context);
            List<int> mattersEmailed = new List<int>();
            List<int> mattersToIgnore = alreadyEmailed.Select(x => x.Item1).ToList();


            List<int> stateIds = new List<int>()
            {
                (int)Enums.StateIdEnum.ACT,
                (int)Enums.StateIdEnum.NSW,
                (int)Enums.StateIdEnum.NT,
                (int)Enums.StateIdEnum.QLD,
                (int)Enums.StateIdEnum.SA,
                (int)Enums.StateIdEnum.TAS,
                (int)Enums.StateIdEnum.VIC,
                (int)Enums.StateIdEnum.WA,
            };

            foreach (var stateId in stateIds)
            {
                var cutoff = DateTime.Now.AddBusinessDays(1, stateId, context).Date;
                DateTime existingCutoff = DateTime.Now.AddHours(-2);
                var today = DateTime.Now.Date;
                var items = context.MatterLedgerItems.Where(m =>
                                        !m.Matter.IsTestFile && m.Matter.LenderId == lenderId
                                        && m.Matter.StateId == stateId &&
                                        !mattersToIgnore.Contains(m.Matter.MatterId) &&
                                        m.Matter.SettlementScheduleId.HasValue &&
                                        ((m.FundingRequestId == null && m.Matter.SettlementSchedule.SettlementDate <= cutoff) ||
                                        (m.FundingRequest != null && m.FundingRequest.FundingRequestStatusTypeId == (int)FundingRequestStatusTypeEnum.InProgress && m.Matter.SettlementSchedule.SettlementDate == today)) &&
                                        m.Matter.SettlementSchedule.SettlementDate >= today &&
                                        (m.MatterLedgerItemStatusTypeId == (int)MatterLedgerItemStatusTypeEnum.Ready || m.MatterLedgerItemStatusTypeId == (int)MatterLedgerItemStatusTypeEnum.Requested) &&
                                        m.Matter.MatterGroupTypeId == (int)Enums.MatterGroupTypeEnum.NewLoan &&
                                        (m.ExpectedPaymentDate.HasValue || m.Matter.SettlementSchedule != null) &&
                                        m.Matter.MatterStatusTypeId != (int)Enums.MatterStatusTypeEnum.NotProceeding &&
                                        m.Matter.MatterStatusTypeId != (int)Enums.MatterStatusTypeEnum.OnHold &&
                                        m.PayableToAccountTypeId == (int)AccountTypeEnum.Trust &&
                                        m.PayableByTypeId == (int)PayableTypeEnum.Lender &&
                                        !m.Matter.MatterWarningEmails.Any(w => w.WarningEmailTypeId == (int)WarningEmailTypeEnum.FundingRequestWarning && w.EmailTriggeredDate > existingCutoff && !w.Overriden) &&
                                        ((m.Matter.SettlementSchedule.SettlementScheduleStatusTypeId == (int)Enums.SettlementScheduleStatusTypeEnum.NotSettled) ||
                                          m.Matter.SettlementSchedule.SettlementScheduleStatusTypeId == (int)Enums.SettlementScheduleStatusTypeEnum.Settled))
                                          .Select(x => new
                                          {
                                              x.MatterId,
                                              x.Matter.MatterDescription,
                                              x.Matter.Lender.LenderName,
                                              x.Matter.LenderId,
                                              x.Matter.MortMgrId,
                                              x.Matter.MortMgr.MortMgrName,
                                              x.Matter.LenderRefNo,
                                              x.Matter.SecondaryRefNo,
                                              x.Matter.SettlementSchedule.SettlementDate,
                                              x.Matter.SettlementSchedule.SettlementTime,
                                              FMFirstname = x.Matter.User.Firstname,
                                              FMLastname = x.Matter.User.Lastname,
                                              ActiveMilestones = x.Matter.MatterWFComponents
                                                                .Where(w =>
                                                                    (w.DisplayStatusTypeId == (int)DisplayStatusTypeEnum.Default || w.DisplayStatusTypeId == (int)DisplayStatusTypeEnum.Display) &&
                                                                    (w.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.InProgress || w.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.Starting))
                                                                    .Select(c => c.WFComponent.WFComponentName).ToList(),
                                              x.FundingRequestId,
                                              x.Amount,
                                              x.Description
                                          }).ToList()
                                          .GroupBy(x => x.MatterId)
                                          .Select(x => new
                                          {
                                              x.First().MatterId,
                                              x.First().MatterDescription,
                                              x.First().LenderId,
                                              x.First().LenderName,
                                              x.First().MortMgrId,
                                              x.First().MortMgrName,
                                              x.First().LenderRefNo,
                                              x.First().SecondaryRefNo,
                                              x.First().SettlementDate,
                                              x.First().SettlementTime,
                                              Milestones = x.First().ActiveMilestones,
                                              Filemanager = x.First().FMFirstname.Trim() + " " + x.First().FMLastname.Trim(),
                                              NotOnFundingRequest = x.Any(f => !f.FundingRequestId.HasValue),
                                              OnInProgressFundingRequest = x.Any(f => f.FundingRequestId.HasValue),
                                              Amounts = x.Select(a => new { a.Amount, a.Description, OnFundingRequest = a.FundingRequestId.HasValue })
                                          }).ToList();

                var test = items.Count();
                foreach (var item in items)
                {
                    string warningText = item.Amounts.Any(x => !x.OnFundingRequest) ? "Not On Funding Request" : "BUILDING funding request";
                    Console.WriteLine($"- Matter {item.MatterId} / Settling {item.SettlementDate.ToString("dd-MMM")} / {warningText} - Emailing {string.Join(",", emailAddresses)}");
                    string lenderRefNoToUse = item.LenderId == 139 ? item.SecondaryRefNo : item.LenderRefNo;
                    string subject = $"Funding Request Warning - {item.MatterId} / {lenderRefNoToUse} / {item.MatterDescription}";
                    string body = $"<span style='font-family: Calibri; font-size:11pt;'><b>Funding Request Warning</b></br>";
                    body += $"<p><b>Matter: </b>{item.MatterId} </br> <b>File Manager: </b>{item.Filemanager} </br> <b>Current Milestone:</b> {string.Join(", ", item.Milestones)}</br><b>Settlement Date:</b> {item.SettlementDate.ToString("dddd MMMM d")}</p><p>";
                    body += "</span><span style='font-family: Calibri; font-size:11pt; color:red'><b>Warnings:</b></span><span style='font-family: Calibri; font-size:11pt;'></br>";
                    foreach (var amount in item.Amounts)
                    {
                        if (!amount.OnFundingRequest)
                        {
                            body += $" - Funding amount of {amount.Amount.ToString("c")} / {amount.Description} - <u>Not On Funding Request</u>";
                        }
                        else
                        {

                            body += $" - Funding amount of  {amount.Amount.ToString("c")} / {amount.Description} - <u>Funding Request Status: <b>BUILDING</b></u>";
                        }
                    }

                    body += "</p>Please take appropriate action.</span>";
                    mattersEmailed.Add(item.MatterId);
                    emRep.SendEmail(string.Join(", ", emailAddresses), "george.kogios@msanational.com.au", subject, body);

                    Console.WriteLine($"EMAILING MATTER {item.MatterId} BOOKED FOR {item.SettlementDate.ToString("dddd MMMM d")} to {string.Join(", ", emailAddresses)}");
                    context.MatterWarningEmails.Add(new MatterWarningEmail() { MatterId = item.MatterId, EmailTriggeredByUserId = GlobalVars.CurrentUser.UserId, EmailTriggeredDate = DateTime.Now, Overriden = false, WarningEmailTypeId = (int)WarningEmailTypeEnum.FundingRequestWarning });

                }
            }

            return mattersEmailed;

        }
        public void EmailSettlementAtRiskMatters()
        {
            DateTime pexaCutoff = DateTime.UtcNow.AddMinutes(45);
            DateTime paperCutoff = DateTime.UtcNow.AddMinutes(150);
            var emRep = new EmailsRepository(context);
            DateTime today = DateTime.Now.Date;
            var mattersToSend = context.Matters.Where(m => m.StateId != (int)StateIdEnum.TAS && m.MatterGroupTypeId != (int)MatterGroupTypeEnum.Discharge && !m.Settled && m.SettlementSchedule != null && m.SettlementSchedule.SettlementDate == DateTime.Today &&
            !m.MatterWarningEmails.Any(w => w.WarningEmailTypeId == (int)WarningEmailTypeEnum.SettlementAtRisk && w.EmailTriggeredDate > today && !w.Overriden) && 
            m.MatterWFComponents.Where(w=>w.WFComponentId != (int)WFComponentEnum.QASettlementInstructions).Any(x=>(x.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.InProgress || x.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.Starting)
                    && (x.WFComponentId != (int)WFComponentEnum.PrintAndCollateFile)
                    && (x.DisplayStatusTypeId == (int)DisplayStatusTypeEnum.Default || x.DisplayStatusTypeId == (int)DisplayStatusTypeEnum.Display)
                    && x.WFComponent.AccountsStageId == (int)AccountsStageEnum.NotReadyForAccounts))
                    .Select(x=>new { x.MatterId, x.Lender.LenderName, x.MatterDescription, x.SettlementSchedule.SettlementDate, x.SettlementSchedule.SettlementTime, x.StateId,
                        Milestones = x.MatterWFComponents.Where(w=>(w.DisplayStatusTypeId == (int)DisplayStatusTypeEnum.Default || w.DisplayStatusTypeId == (int)DisplayStatusTypeEnum.Display) && (w.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.InProgress || w.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.Starting )).Select(c=>c.WFComponent.WFComponentName).ToList(),
                        FileownerEmail = x.User.Email, FileownerName = x.User.Firstname, IsPaper = x.MatterSecurities.Any(s => !s.Deleted && s.SettlementTypeId == (int)SettlementTypeEnum.Paper) }).ToList();

            foreach(var mt in mattersToSend)
            {
                bool isPaper = mt.IsPaper;

                DateTime rawSettlementDate = (mt.SettlementDate + mt.SettlementTime);
                DateTime UTCSettlementDateTime = rawSettlementDate.ConvertToUTC(mt.StateId);

                if ((isPaper && UTCSettlementDateTime < paperCutoff) || (!isPaper && UTCSettlementDateTime < pexaCutoff))
                {
                    string milestonesStr = mt.Milestones.Any() ? string.Join(", ", mt.Milestones) : "- NO ACTIVE MILESTONE -";

      

                    string settlementTypeString = isPaper ? "Paper Settlement" : "PEXA Settlement";
                    Console.WriteLine($"EMAILING MATTER: {mt.MatterId} / STATE: {((StateIdEnum)mt.StateId).ToString()} - {settlementTypeString} - booked for {mt.SettlementTime} local.");

                    string emailBody = $"<div style='font-family:Calibri;font-size:11pt;'>Hi {mt.FileownerName},<br> {mt.LenderName} Matter <b>{mt.MatterId}</b> - <i>{mt.MatterDescription}</i> is due for {settlementTypeString} at {mt.SettlementTime}" +
                        $" but Slick has not yet been updated to make this matter \"READY FOR ACCOUNTS\".<br>" + $"The matter is currently sitting at: <b>{milestonesStr}</b><br>" + 
                         $"This settlement is now at risk of not completing, please update urgently.<br>If you have any questions, you may contact Raunak - raunak.golchha@msanational.com.au for assistance.<br>Kind regards, Alfred</div>";

                    bool testingEmails = Slick_Domain.GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmails, context).ToUpper() == "TRUE";
                    string toAddress = testingEmails ? "robert.quinn@msanational.com.au" : mt.FileownerEmail;
                    string ccAddress = "george.kogios@msanational.com.au";
                    string subject = $"SLICK Warning: Settlement at Risk for Matter {mt.MatterId} - {mt.MatterDescription}";
                    emRep.SendEmail(toAddress, ccAddress, subject, emailBody);
                    context.MatterWarningEmails.Add(new MatterWarningEmail() { MatterId = mt.MatterId, EmailTriggeredByUserId = GlobalVars.CurrentUser.UserId, Overriden = false, WarningEmailTypeId = (int)WarningEmailTypeEnum.SettlementAtRisk, EmailTriggeredDate = DateTime.Now });
                    
                }

            }

        }






        public IEnumerable<MCE.FundingRequestItemView> GetFundingRequestItemsView(int fundingRequestId)
        {
            return context.FundingRequestMatterLedgerItems.Where(f => f.FundingRequestId == fundingRequestId)
                .Select(m => new
                {
                    m.FundingRequestId,
                    m.FundingRequest.FundingRequestNo,
                    FundingRequestStatusTypeId = (int?)m.FundingRequest.FundingRequestStatusTypeId,
                    m.FundingRequest.FundingRequestStatusType.FundingRequestStatusTypeName,
                    m.MatterLedgerItemId,
                    m.MatterLedgerItem.MatterId,
                    m.MatterLedgerItem.Matter.MatterDescription,
                    MatterTypes = m.MatterLedgerItem.Matter.MatterSecurities.Select(x => x.MatterType.MatterTypeName).Distinct(),
                    FileOwnerName = m.MatterLedgerItem.Matter.User.Fullname,
                    m.MatterLedgerItem.Matter.LenderId,
                    m.MatterLedgerItem.Matter.Lender.LenderName,
                    m.MatterLedgerItem.Matter.StateId,
                    m.MatterLedgerItem.Matter.State.StateName,
                    SettlementDate = (DateTime?)m.MatterLedgerItem.Matter.SettlementSchedule.SettlementDate,
                    SettlementTypes = m.MatterLedgerItem.Matter.MatterSecurities.Select(x => x.SettlementType.SettlementTypeName).Distinct(),
                    FundingAmount = m.MatterLedgerItem.Amount,
                    FundingAmountDesc = m.MatterLedgerItem.Description,
                    m.MatterLedgerItem.UpdatedDate,
                    m.MatterLedgerItem.UpdatedByUserId,
                    m.MatterLedgerItem.User.Username,
                    LenderRefNo = m.MatterLedgerItem.Matter.LenderId == 139 ? m.MatterLedgerItem.Matter.SecondaryRefNo : m.MatterLedgerItem.Matter.LenderRefNo

                })
                .ToList()
                .Select(m => new MCE.FundingRequestItemView(m.FundingRequestId, m.FundingRequestNo, m.FundingRequestStatusTypeId, 
                        m.FundingRequestStatusTypeName, m.SettlementDate, m.SettlementTypes, m.MatterLedgerItemId,
                        m.MatterId, m.LenderRefNo, m.MatterDescription, m.MatterTypes, m.FileOwnerName, m.LenderId, m.LenderName, m.StateId, m.StateName,
                        m.FundingAmount, m.FundingAmountDesc, m.UpdatedDate, m.UpdatedByUserId, m.Username,false))
                .ToList();
        }
        public MCE.FundingRequestItemView GetFundingRequestItemsViewForLedgerItem(int matterLedgerItemId)
        {
            return context.FundingRequestMatterLedgerItems.Where(f => f.MatterLedgerItemId == matterLedgerItemId)
                .Select(m => new
                {
                    m.FundingRequestId,
                    m.FundingRequest.FundingRequestNo,
                    FundingRequestStatusTypeId = (int?)m.FundingRequest.FundingRequestStatusTypeId,
                    m.FundingRequest.FundingRequestStatusType.FundingRequestStatusTypeName,
                    m.MatterLedgerItemId,
                    m.MatterLedgerItem.MatterId,
                    m.MatterLedgerItem.Matter.MatterDescription,
                    MatterTypes = m.MatterLedgerItem.Matter.MatterSecurities.Select(x => x.MatterType.MatterTypeName).Distinct(),
                    FileOwnerName = m.MatterLedgerItem.Matter.User.Fullname,
                    m.MatterLedgerItem.Matter.LenderId,
                    m.MatterLedgerItem.Matter.Lender.LenderName,
                    m.MatterLedgerItem.Matter.StateId,
                    m.MatterLedgerItem.Matter.State.StateName,
                    SettlementDate = (DateTime?)m.MatterLedgerItem.Matter.SettlementSchedule.SettlementDate,
                    SettlementTypes = m.MatterLedgerItem.Matter.MatterSecurities.Select(x => x.SettlementType.SettlementTypeName).Distinct(),
                    FundingAmount = m.MatterLedgerItem.Amount,
                    FundingAmountDesc = m.MatterLedgerItem.Description,
                    m.MatterLedgerItem.UpdatedDate,
                    m.MatterLedgerItem.UpdatedByUserId,
                    m.MatterLedgerItem.User.Username,
                    LenderRefNo = m.MatterLedgerItem.Matter.LenderId == 139 ? m.MatterLedgerItem.Matter.SecondaryRefNo : m.MatterLedgerItem.Matter.LenderRefNo

                })
                .ToList()
                .Select(m => new MCE.FundingRequestItemView(m.FundingRequestId, m.FundingRequestNo, m.FundingRequestStatusTypeId,
                        m.FundingRequestStatusTypeName, m.SettlementDate, m.SettlementTypes, m.MatterLedgerItemId,
                        m.MatterId, m.LenderRefNo, m.MatterDescription, m.MatterTypes, m.FileOwnerName, m.LenderId, m.LenderName, m.StateId, m.StateName,
                        m.FundingAmount, m.FundingAmountDesc, m.UpdatedDate, m.UpdatedByUserId, m.Username, false))
                .FirstOrDefault();
        }
        public IEnumerable<MCE.FundingRequestItemView> GetFundingRequestItemsView(IQueryable<MatterLedgerItem> fundingRequestItemsQry)
        {
            bool highlightRequired = fundingRequestItemsQry.Any(x => x.FundingRequestId.HasValue);


            return fundingRequestItemsQry
                .Select(m => new
                {
                    m.FundingRequestId,
                    m.FundingRequest.FundingRequestNo,
                    FundingRequestStatusTypeId = (int?)m.FundingRequest.FundingRequestStatusTypeId,
                    m.FundingRequest.FundingRequestStatusType.FundingRequestStatusTypeName,
                    m.MatterLedgerItemId,
                    m.MatterId,
                    m.Matter.MatterDescription,
                    MatterTypes = m.Matter.MatterSecurities.Select(x => x.MatterType.MatterTypeName).Distinct(),
                    FileOwnerName = m.Matter.User.Fullname,
                    m.Matter.LenderId,
                    m.Matter.Lender.LenderName,
                    m.Matter.StateId,
                    m.Matter.State.StateName,
                    m.Matter.SettlementSchedule.SettlementDate,
                    SettlementTypes = m.Matter.MatterSecurities.Select(x => x.SettlementType.SettlementTypeName).Distinct(),
                    FundingAmount = m.Amount,
                    FundingAmountDesc = m.Description,
                    m.UpdatedDate,
                    m.UpdatedByUserId,
                    m.User.Username,
                    LenderRefNo = m.Matter.LenderId == 139 ? m.Matter.SecondaryRefNo : m.Matter.LenderRefNo 
                })
                .ToList()
                .Select(m => new MCE.FundingRequestItemView(m.FundingRequestId, m.FundingRequestNo, m.FundingRequestStatusTypeId,
                        m.FundingRequestStatusTypeName, m.SettlementDate, m.SettlementTypes, m.MatterLedgerItemId, 
                        m.MatterId, m.LenderRefNo, m.MatterDescription, m.MatterTypes, m.FileOwnerName, m.LenderId, m.LenderName, m.StateId, m.StateName, 
                        m.FundingAmount, m.FundingAmountDesc, m.UpdatedDate, m.UpdatedByUserId, m.Username, highlightRequired))
                .ToList();
        }
       

        public string BuildFundingRequestNo(int lenderId, DateTime settlementDate)
        {
            string lenderShortName = context.Lenders.FirstOrDefault(l => l.LenderId == lenderId).LenderNameShort;
            string summNo = lenderShortName + "/" + settlementDate.ToString("yyyy-MM-dd");

            int appendCount = 0;
            while (context.FundingRequests.Where(d => d.FundingRequestNo == summNo).Count() > 0)
            {
                appendCount++;
                summNo = lenderShortName + "/" + settlementDate.ToString("yyyy-MM-dd") + "/" + appendCount.ToString();
            }

            return summNo;
        }


        public IEnumerable<MCE.PPSRequestView> GetPPSRequestsView(int? lenderId, DateTime? settlementFromDate, DateTime? settlementToDate, int? fundingRequestStatusId)
        {
            IQueryable<PPSRequest> qry = context.PPSRequests.AsNoTracking();

            if (lenderId.HasValue)
                qry = qry.Where(m => m.LenderId == lenderId);
            if (settlementFromDate.HasValue)
                qry = qry.Where(m => m.SettlementDate >= settlementFromDate);
            if (settlementToDate.HasValue)
                qry = qry.Where(m => m.SettlementDate <= settlementToDate);
            if (fundingRequestStatusId.HasValue)
                qry = qry.Where(m => m.FundingRequestStatusTypeId == fundingRequestStatusId);

            return GetPPSRequestsView(qry);

        }
        public IEnumerable<MCE.PPSRequestView> GetPPSRequestsView(IQueryable<PPSRequest> ppsRequestsQry)
        {
            return ppsRequestsQry
                .Select(m => new MCE.PPSRequestView
                {
                    PPSRequestId = m.PPSRequestId,
                    PPSRequestNo = m.PPSRequestNo,
                    LenderId = m.LenderId,
                    LenderName = m.Lender.LenderName,
                    SettlementDate = m.SettlementDate,
                    FundingRequestStatusTypeId = m.FundingRequestStatusTypeId,
                    FundingRequestStatusTypeName = m.FundingRequestStatusType.FundingRequestStatusTypeName,
                    Notes = m.Notes,
                    UpdatedDate = m.UpdatedDate,
                    UpdatedByUserId = m.UpdatedByUserId,
                    UpdatedByUsername = m.User.Username
                }).ToList();
        }
        public MCE.PPSRequestView GetPPSRequestView(int ppsRequestId)
        {
            IQueryable<PPSRequest> qry = context.PPSRequests.Where(t => t.PPSRequestId == ppsRequestId);
            return GetPPSRequestsView(qry).FirstOrDefault();
        }


        public IEnumerable<MCE.PPSRequestItemView> GetPPSRequestItemsView(int? stateId, int? lenderId, DateTime settlementDate, int? fundingRequestStatusId)
        {
            IQueryable<MatterLedgerItem> qry = context.MatterLedgerItems.AsNoTracking().Where(m => m.Matter.MatterGroupTypeId == (int)Enums.MatterGroupTypeEnum.NewLoan && m.Matter.SettlementSchedule.SettlementDate == settlementDate &&
                                        m.Matter.MatterStatusTypeId != (int)Enums.MatterStatusTypeEnum.NotProceeding &&
                                        m.Matter.MatterStatusTypeId != (int)Enums.MatterStatusTypeEnum.OnHold &&
                                        m.PayableByAccountTypeId == (int)AccountTypeEnum.Trust &&
                                        (m.TransactionTypeId == (int)TransactionTypeEnum.PPS ||
                                         m.TransactionTypeId == (int)TransactionTypeEnum.PPSFree) &&
                                        ((m.Matter.SettlementSchedule.SettlementScheduleStatusTypeId == (int)Enums.SettlementScheduleStatusTypeEnum.NotSettled) ||
                                          m.Matter.SettlementSchedule.SettlementScheduleStatusTypeId == (int)Enums.SettlementScheduleStatusTypeEnum.Settled));

            if (stateId.HasValue)
                qry = qry.Where(m => m.Matter.StateId == stateId);

            if (lenderId.HasValue)
                qry = qry.Where(m => m.Matter.LenderId == lenderId);

            if (fundingRequestStatusId.HasValue)
                qry = qry.Where(m => m.FundingRequest.FundingRequestStatusTypeId == fundingRequestStatusId);

            return GetPPSRequestItemsView(qry);

        }
        public IEnumerable<MCE.PPSRequestItemView> GetPPSRequestItemsView(int ppsRequestId)
        {
            IQueryable<MatterLedgerItem> qry = context.MatterLedgerItems.AsNoTracking().Where(m => m.PPSRequestId == ppsRequestId);
            return GetPPSRequestItemsView(qry);
        }
        public IEnumerable<MCE.PPSRequestItemView> GetPPSRequestItemsView(IQueryable<MatterLedgerItem> ppsRequestItemsQry)
        {
            return ppsRequestItemsQry
                .Select(m => new
                {
                    m.PPSRequestId,
                    m.PPSRequest.PPSRequestNo,
                    FundingRequestStatusTypeId = (int?)m.FundingRequest.FundingRequestStatusTypeId,
                    m.FundingRequest.FundingRequestStatusType.FundingRequestStatusTypeName,
                    m.MatterLedgerItemId,
                    m.MatterId,
                    m.Matter.MatterDescription,
                    m.Matter.LenderId,
                    m.Matter.Lender.LenderName,
                    m.Matter.StateId,
                    m.Matter.State.StateName,
                    m.Matter.SettlementSchedule.SettlementDate,
                    m.PayableToName,
                    FundingAmount = m.Amount,
                    FundingAmountDesc = m.Description,
                    m.UpdatedDate,
                    m.UpdatedByUserId,
                    m.User.Username
                })
                .ToList()
                .Select(m => new MCE.PPSRequestItemView(m.PPSRequestId, m.PPSRequestNo, m.FundingRequestStatusTypeId, m.FundingRequestStatusTypeName, m.SettlementDate, m.MatterLedgerItemId,
                        m.MatterId, m.MatterDescription, m.LenderId, m.LenderName, m.StateId, m.StateName, m.PayableToName,
                        m.FundingAmount, m.FundingAmountDesc, m.UpdatedDate, m.UpdatedByUserId, m.Username))
                .ToList();
        }

        public string BuildPPSRequestNo(MCE.PPSRequestItemView riv)
        {
            string lenderShortName = context.Lenders.FirstOrDefault(l => l.LenderId == riv.LenderId).LenderNameShort;
            string summNo = lenderShortName + "/" + riv.SettlementDate.ToString("yyyy-MM-dd");

            int appendCount = 0;
            while (context.PPSRequests.Where(d => d.PPSRequestNo == summNo).Count() > 0)
            {
                appendCount++;
                summNo = lenderShortName + "/" + riv.SettlementDate.ToString("yyyy-MM-dd") + "/" + appendCount.ToString();
            }

            return summNo;
        }






        public string getMatterSearchStr(MCE.MatterView mv)
        {
            var srchStr = new StringBuilder();

            srchStr.Append(mv.MatterId.ToString());
            if (mv.MatterDescription != null)
                srchStr.AppendFormat("|{0}", mv.MatterDescription);
            if (mv.LenderRefNo != null)
                srchStr.AppendFormat("|{0}", mv.LenderRefNo);

            if(!String.IsNullOrEmpty(mv.SecondaryRefNo))
            {
                srchStr.AppendFormat("|{0}", mv.SecondaryRefNo);
            }
            //Securities
            foreach (var security in mv.Securities)
            {
                if (security.StreetAddress != null)
                    srchStr.AppendFormat("|{0}", security.StreetAddress);
                if (security.Suburb != null)
                    srchStr.AppendFormat("|{0}", security.Suburb);

                foreach (var titleRef in security.TitleRefs)
                {
                    srchStr.AppendFormat("|{0}", titleRef.TitleReference);
                }
            }

            foreach (var pexaWS in mv.PexaWorkspaces)
            {
                srchStr.AppendFormat("|{0}", pexaWS.PexaWorkspaceNo);
            }

            return srchStr.ToString().ToLower();

        }

        public void UpdateMatterSearch()
        {

            //Build Matter Searches
            var newMatters = context.Matters.Where(m => m.MatterSearch == null);

            List<MatterSearch> matterSearchList = new List<MatterSearch>();
            foreach (var mat in newMatters)
            {
                MCE.MatterView mv = GetMatterView(mat.MatterId);
                MatterSearch ms = new MatterSearch();
                ms.MatterId = mv.MatterId;
                ms.MatterSearchText = getMatterSearchStr(mv);

                matterSearchList.Add(ms);
            }
            context.MatterSearches.AddRange(matterSearchList);

            context.SaveChanges();
        }

        public void UpdateMatterSearch(int matterId)
        {
            MCE.MatterView mv = GetMatterView(matterId);
            var ms = context.MatterSearches.FirstOrDefault(x => x.MatterId == matterId);
            bool isNew = ms == null;

            if (isNew) ms = new MatterSearch();
            ms.MatterId = matterId;
            ms.MatterSearchText = getMatterSearchStr(mv);

            if (isNew)
                context.MatterSearches.Add(ms);

            context.SaveChanges();
        }




        public IEnumerable<MCE.TitleSearchView> GetTitleSearchesForMatter(int matterId)
        {
            return context.MatterInfoTracks.AsNoTracking().Where(m => m.MatterId == matterId)
                    .Select(m => new MCE.TitleSearchView
                    {
                        Model = new Entities.InfoTrack.MapModel
                        {
                            DateCompleted = m.DateCompleted,
                            DateOrdered = m.DateOrdered,
                            Description = m.Description,
                            DownloadUrl = m.DownloadURL,
                            IsBillable = m.IsBillable,
                            OrderedByUserId = m.OrderedByUserId,
                            OrderId = m.OrderId,
                            ServiceName = m.ServiceName,
                            Status = m.Status,
                            TotalFee = m.TotalFee,
                            TotalFeeGst = m.TotalFeeGST
                        },
                        OrderedByUserName = m.User.Username,
                        TotalFeeGstIncl = (m.TotalFee ?? 0) + (m.TotalFeeGST ?? 0),
                        DocumentId = m.DocumentId,
                        DocName = m.Document.DocumentMaster.DocName,
                        DocType = m.Document.DocumentMaster.DocType
                    }).ToList();
        }

        public IEnumerable<Entities.InfoTrack.InfoTrackDownload> GetDownloadUrlsForMatter(int matterId)
        {
            return context.MatterInfoTracks.Where(x => x.MatterId == matterId && x.DocumentId == null && x.DownloadURL != null)
                .Select(x => new Entities.InfoTrack.InfoTrackDownload
                {
                    MatterId = x.MatterId,
                    InfoTrackId = x.MatterInfoTrackId,
                    DownloadUrl = x.DownloadURL,
                    FileName = x.Description
                });
        }

        public bool IsMatterPEXA(int matterId)
        {
            if (context.MatterSecurities.Where(m => !m.Deleted && m.MatterId == matterId && m.SettlementTypeId == (int)Enums.SettlementTypeEnum.PEXA).Count() > 0)
                return true;
            else
                return false;
        }

        public bool IsMatterPaper(int matterId)
        {
            if (context.MatterSecurities.Where(m => !m.Deleted && m.MatterId == matterId && m.SettlementTypeId == (int)Enums.SettlementTypeEnum.Paper).Count() > 0)
                return true;
            else
                return false;
        }

        public void SetMatterCertified(int matterId)
        {
            var mt = context.Matters.FirstOrDefault(m => m.MatterId == matterId);
            mt.Certified = true;
            mt.CertifiedDate = DateTime.Now;
        }

        /// <summary>
        /// Used by Web Service for All Matter Outstandings
        ///  --- Future Change - Change to just use MatterOutstandingItems insted of WF.
        /// </summary>
        /// <param name="matterId"></param>
        /// <returns></returns>
        public IEnumerable<MCE.MatterOutstandingsView> GetMatterOutstandings(int matterId)
        {
            var outstandings = context.MatterWFOutstandingReqItems
                .Where(m => m.MatterWFOutstandingReq.MatterWFComponent.MatterId == matterId &&
                            m.Resolved != true &&
                            m.MatterWFOutstandingReq.MatterWFComponent.WFComponentStatusTypeId != (int)MatterWFComponentStatusTypeEnum.Cancelled &&
                            m.MatterWFOutstandingReq.MatterWFComponent.WFComponentStatusTypeId != (int)MatterWFComponentStatusTypeEnum.Deleted)
                            .Select(m => new MCE.MatterOutstandingsView
                            {
                                OutstandingItemName = m.OutstandingItemName,
                                IssueDetails = m.IssueDetails,
                                Resolved = m.Resolved,
                                ResolvedDate = m.IssueResolvedDate,
                                SecurityAddress = m.MatterWFProcDocsSecurity.StreetAddress,
                                SecuritySuburb = m.MatterWFProcDocsSecurity.Suburb,
                                SecurityStateName = m.MatterWFProcDocsSecurity.State.StateName,
                                SecurityPostCode = m.MatterWFProcDocsSecurity.PostCode,
                                FollowupDate = m.MatterWFOutstandingReq.FollowUpDate
                            }).ToList();

            var mwfComps = context.MatterWFComponents.Where(m => m.MatterId == matterId);
            var mwfRep = new MatterWFRepository(context);

            List<MCE.MatterOutstandingsView> reminders = new List<MCE.MatterOutstandingsView>();
            List<MatterWFFollowUp> log;
            List<MatterWFFollowUpHistory> history;

            if (mwfRep.HasMilestoneCompleted(matterId, (int)WFComponentEnum.DocsReturned))
            {

                foreach (var mwfComponent in mwfComps)
                {
                    log = context.MatterWFFollowUps.Where(x => x.MatterWFComponentId == mwfComponent.MatterWFComponentId).ToList();
                    foreach (var matterWfFollowup in log)
                    {
                        history = context.MatterWFFollowUpHistories.Where(m => m.MatterWFFollowupId == matterWfFollowup.MatterWFFollowUpId).ToList();
                        foreach (var followUp in history)
                        {
                            reminders.Add(new MCE.MatterOutstandingsView
                            {
                                OutstandingItemName = "Follow Up",
                                IssueDetails = followUp.MilestoneNotes,
                                FollowupDate = followUp.FollowUpDate

                            });
                        }
                    }
                }
            }

            return outstandings.Concat(reminders); 
        }

        public IEnumerable<MCE.MatterOutstandingsView> GetDocsSentMatterOutstandings(int matterId, int wfComponentId)//, List<MatterWFComponent> wfComps)
        {
            List<MatterWFSendProcessedDoc> mwfSendProcessedDocs = 
                context.MatterWFSendProcessedDocs.Where(m => m.MatterWFComponentId == wfComponentId && m.MatterWFComponent.MatterId == matterId)
            .ToList();
            List<MCE.MatterOutstandingsView> docsSent = new List<MCE.MatterOutstandingsView>();
            List<MCE.MatterOutstandingsView> followUps = new List<MCE.MatterOutstandingsView>();

            foreach (var doc in mwfSendProcessedDocs)
            {
                int? deliveryType = doc.DocumentDeliveryTypeId;
                string detailsMessage = "";
                //string HtmlNewLine = "</p>";

                string senderTracking = "";
                string receiverTracking = "";

                if (doc.ExpressPostSentTracking != null)
                {
                    senderTracking = doc.ExpressPostSentTracking;
                }
                else
                {
                    senderTracking = " - ";
                }

                if (doc.ExpressPostReceiveTracking != null)
                {
                    receiverTracking = doc.ExpressPostReceiveTracking;
                }
                else
                {
                    receiverTracking = " - ";
                }




                if (deliveryType == (int)WFProcessedDocSentTypeEnum.DigiDocs) //eSigned doc:
                {

                    detailsMessage = $"<p>Loan Documents have been sent to: {doc.NameDocsSentTo.Trim()}</p>" +
                                       $"<p>Via: {doc.DocumentDeliveryType.DocumentDeliveryTypeName.Trim()}</p>" +
                                       $"<p>To the email address: {doc.EmailDocsSentTo}</p>";

                }
                else if (deliveryType == (int)WFProcessedDocSentTypeEnum.ExpressPostReturn) //Express Post (return envelope)
                {

                    detailsMessage = $"<p>Loan Documents have been sent to: {doc.NameDocsSentTo.Trim()}</p>" +
                                     $"<p>Via: {doc.DocumentDeliveryType.DocumentDeliveryTypeName.Trim()}</p>" +
                                     $"<p>Sender tracking number: {senderTracking}</p>" +
                                     $"<p>Receiver tracking number: {receiverTracking}</p>";
                }
                else if (deliveryType == (int)WFProcessedDocSentTypeEnum.ExpressPostNoReturn) //Express Post (no return)
                {

                    detailsMessage = $"<p>Loan Documents have been sent to: {doc.NameDocsSentTo.Trim()}</p>" +
                                     $"<p>Via: {doc.DocumentDeliveryType.DocumentDeliveryTypeName.Trim()}</p>" +
                                     $"<p>Sender tracking number: {senderTracking}</p>";

                }
                else if (deliveryType == (int)WFProcessedDocSentTypeEnum.StandardPost || deliveryType == (int)WFProcessedDocSentTypeEnum.Courier) //Standard Post
                {

                    detailsMessage = $"<p>Loan Documents have been sent to: {doc.NameDocsSentTo.Trim()}</p>" +
                                     $"<p>Via: {doc.DocumentDeliveryType.DocumentDeliveryTypeName.Trim()}</p>";
                }
                else if (deliveryType == (int)WFProcessedDocSentTypeEnum.ForCollection) //Collection
                {

                    detailsMessage = $"<p>Loan Documents are available for collection by: {doc.NameDocsSentTo.Trim()}</p>";

                }
                else if (deliveryType == (int)WFProcessedDocSentTypeEnum.Email || deliveryType == (int)WFProcessedDocSentTypeEnum.Docusign || deliveryType == (int)WFProcessedDocSentTypeEnum.DigiDocs) //Email, Docusign, Digidocs
                {

                    detailsMessage = $"<p>Loan Documents have been sent to: {doc.NameDocsSentTo.Trim()}</p>" +
                                     $"<p>Via: {doc.DocumentDeliveryType.DocumentDeliveryTypeName.Trim()}</p>" +
                                     $"<p>To the email address: {doc.EmailDocsSentTo}</p>";

                }

                else //Shouldn't be anything else but just in case new deliverytype IDs are added this can temporarily handle them.
                {

                    detailsMessage = $"<p>Loan Documents have been sent to: {doc.NameDocsSentTo.Trim()}</p>" +
                                     $"<p>Via: {doc.DocumentDeliveryType.DocumentDeliveryTypeName.Trim()}</p>";

                }

                var docSent = new MCE.MatterOutstandingsView
                {
                    OutstandingItemName = "Loan Documents Sent",
                    IssueDetails = detailsMessage
                };

                docsSent.Add(docSent);
            }

            var outstandings = context.MatterWFOutstandingReqItems
                .Where(m => m.MatterWFOutstandingReq.MatterWFComponent.MatterId == matterId &&
                            m.Resolved != true &&
                            m.MatterWFOutstandingReq.MatterWFComponent.WFComponentStatusTypeId != (int)MatterWFComponentStatusTypeEnum.Cancelled &&
                            m.MatterWFOutstandingReq.MatterWFComponent.WFComponentStatusTypeId != (int)MatterWFComponentStatusTypeEnum.Deleted)
                            .Select(m => new MCE.MatterOutstandingsView
                            {
                                OutstandingItemName = m.OutstandingItemName,
                                IssueDetails = m.IssueDetails + "FOLLOW-UP DATE: "+ m.MatterWFOutstandingReq.FollowUpDate,
                                Resolved = m.Resolved,
                                ResolvedDate = m.IssueResolvedDate,
                                SecurityAddress = m.MatterWFProcDocsSecurity.StreetAddress,
                                SecuritySuburb = m.MatterWFProcDocsSecurity.Suburb,
                                SecurityStateName = m.MatterWFProcDocsSecurity.State.StateName,
                                SecurityPostCode = m.MatterWFProcDocsSecurity.PostCode,
                                
                            }).ToList();

            var mwfComps = context.MatterWFComponents.Where(m => m.MatterWFComponentId == matterId);

            List<MCE.MatterOutstandingsView> reminders = new List<MCE.MatterOutstandingsView>();
            List<MatterWFFollowUp> log;
            List<MatterWFFollowUpHistory> history;
            foreach (var mwfComponent in mwfComps)
            {
                log = context.MatterWFFollowUps.Where(x => x.MatterWFComponentId == mwfComponent.MatterWFComponentId).ToList();
                foreach (var matterWfFollowup in log)
                {
                    history = context.MatterWFFollowUpHistories.Where(m => m.MatterWFFollowupId == matterWfFollowup.MatterWFFollowUpId).ToList();
                    foreach (var followUp in history) {
                        reminders.Add(new MCE.MatterOutstandingsView
                        {
                            OutstandingItemName = "FOLLOW UP : ",
                            IssueDetails = followUp.MilestoneNotes,
                            FollowupDate = followUp.FollowUpDate
                            
                        });
                    }
                }
            }
                
            return docsSent.Concat(outstandings);
        }


        public IEnumerable<MCE.MatterWebSearchView> MatterSearchWebView(IQueryable<Matter> qry)
        {
            return qry.Select(m => new MCE.MatterWebSearchView
            {
                MatterId = m.MatterId,
                MatterDescription = m.MatterDescription,
                LenderRefNo = m.LenderRefNo,
                FileOwnerFullname = m.User.Fullname,
                SecurityAddress = m.MatterSecurities.Select(s => new MCE.MatterWebSecurityView
                {
                    Address = s.StreetAddress,
                    Suburb = s.Suburb,
                    State = s.State.StateName,
                    PostCode = s.PostCode,
                    SettlementType = s.SettlementType.SettlementTypeName
                }).ToList()
            }).ToList();
        }

        public IEnumerable<MCE.MatterWebSearchView> MatterSearchWebView(string userName, int matterId)
        {
            var usr = context.Users.Where(u => u.Username == userName && u.Enabled)
                                .Select(u => new { u.UserId, u.UserTypeId, u.LenderId, u.MortMgrId, u.BrokerId, u.Broker.PrimaryContactId })
                                .FirstOrDefault();

            if (usr == null)
                return new List<MCE.MatterWebSearchView>();

            var mt = context.Matters.Where(m => m.MatterId == matterId);
            switch (usr.UserTypeId)
            {
                case (int)UserTypeEnum.Lender:
                    mt = mt.Where(m => m.LenderId == usr.LenderId);
                    break;
                case (int)UserTypeEnum.MortMgr:
                    mt = mt.Where(m => m.MortMgrId == usr.MortMgrId);
                    break;
                case (int)UserTypeEnum.Broker:
                    mt = mt.Where(m => m.BrokerId == usr.BrokerId || m.Broker.PrimaryContactId == usr.PrimaryContactId);
                    break;
            }

            return MatterSearchWebView(mt);
        }

        public IEnumerable<MCE.MatterWebSearchView> MatterSearchWebView(string userName, string searchStr)
        {
            var usr = context.Users.Where(u => u.Username == userName && u.Enabled)
                                .Select(u => new { u.UserId, u.UserTypeId, u.LenderId, u.MortMgrId, u.BrokerId, u.Broker.PrimaryContactId })
                                .FirstOrDefault();

            if (usr == null)
                return new List<MCE.MatterWebSearchView>();

            var mt = context.Matters.Where(m => m.MatterDescription.Contains(searchStr) || m.LenderRefNo.Contains(searchStr));
            switch (usr.UserTypeId)
            {
                case (int)UserTypeEnum.Lender:
                    mt = mt.Where(m => m.LenderId == usr.LenderId);
                    break;
                case (int)UserTypeEnum.MortMgr:
                    mt = mt.Where(m => m.MortMgrId == usr.MortMgrId);
                    break;
                case (int)UserTypeEnum.Broker:
                    mt = mt.Where(m => m.BrokerId == usr.BrokerId || m.Broker.PrimaryContactId == usr.PrimaryContactId);
                    break;
            }


            return MatterSearchWebView(mt);
        }


        //GetWebQuickFilterCount using Stored Procedure
        //public IEnumerable<MCE.MatterWebQuickFilterCountView> GetWebQuickFilterCount(User usr)
        //{
        //    return null;
        //}

        public IEnumerable<MCE.MatterWebQuickFilterCountView> GetWebQuickFilterCount(User usr)
        {
            DateTime? filterDate;
            filterDate = null;

            var retList = new List<MCE.MatterWebQuickFilterCountView>();
            MCE.MatterWebQuickFilterCountView itm;

            var resultList = context.sp_Slick_GetWebQuickFilter(usr.UserId, filterDate).ToList()
            .Select(s => new Slick_Domain.Models.sp_Slick_GetWebQuickFilter_Result()
            {
                OnHoldCount = s.OnHoldCount,
                InProgressCount = s.InProgressCount,
                SettledCount = s.SettledCount,
                NotProceedingCount = s.NotProceedingCount,
                ClosedCount = s.ClosedCount,
                AboutToExpireCount = s.AboutToExpireCount,
                SettledTodayCount = s.SettledTodayCount,
                SettlingTodayCount = s.SettlingTodayCount,
                InstructionsReceivedCount = s.InstructionsReceivedCount,
                DocsSentCount = s.DocsSentCount,
                DocsReturnedCount = s.DocsReturnedCount,
                DocsVerifiedCount = s.DocsVerifiedCount,
                ReadyToBookCount = s.ReadyToBookCount,
                SettlementCompleteCount = s.SettlementCompleteCount,
                Outstandings = s.Outstandings
            }).ToList();

            //----------------------------------------------------------------------------------------------

            //On Hold

            int holdCount = 0;

            try
            {
                holdCount = (int)resultList.Select(q => q.OnHoldCount).Single();
            }
            catch (NullReferenceException)
            {
                holdCount = 0;
            }

            //In Progress - Never actually gets used... so for now we'll just set it as 0 to save an SQL call. 
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "InProgress" };

            try
            {
                itm.FilterCount = (int)resultList.Select(q => q.InProgressCount).Single();
            }
            catch (NullReferenceException)
            {
                itm.FilterCount = 0;
            }

            retList.Add(itm);

            //On Hold as Item
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "OnHold" };
            itm.FilterCount = holdCount;
            retList.Add(itm);

            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "Settled" };
            //if (usr.FiltersRefreshedTime < DateTime.Now.AddMinutes(-30)) //if FiltersRefreshedTime 
            //{
            //Settled
            try
            {
                itm.FilterCount = (int)resultList.Select(q => q.SettledCount).Single();
            }
            catch (NullReferenceException)
            {
                itm.FilterCount = 0;
            }
            //} else
            //{
            //    itm.FilterCount = -1; //no need to get a new value
            //}
            retList.Add(itm);

            //Not Proceeding

            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "NotProceeding" };

            try
            {
                itm.FilterCount = (int)resultList.Select(q => q.NotProceedingCount).Single();
            }
            catch (NullReferenceException)
            {
                itm.FilterCount = 0;
            }
            retList.Add(itm);

            //Closed
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "Closed" };
            try
            {
                itm.FilterCount = (int)resultList.Select(q => q.ClosedCount).Single();
            }
            catch (NullReferenceException)
            {
                itm.FilterCount = 0;
            }
            retList.Add(itm);


            //About To Expire
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "AboutToExpire" };
            itm.FilterCount = 0;
            itm.FilterCount = (int)resultList.Select(q => q.AboutToExpireCount).Single();
            retList.Add(itm);

            //Settled Today
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "SettledToday" };
            itm.FilterCount = 0;
            itm.FilterCount = (int)resultList.Select(q => q.SettledTodayCount).Single();
            retList.Add(itm);

            //Settling Today
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "SettlingToday" };
            itm.FilterCount = 0;
            itm.FilterCount = (int)resultList.Select(q => q.SettlingTodayCount).Single();
            retList.Add(itm);


            //--------------------------------------------------------------------------------------------

            //Workflow Counts need to iterate through the matters
            /*
   var mwfRep = new MatterWFRepository(context);
            int cntInstrRec = 0, cntDocsSent = 0, 
                cntDocsReturned = 0, cntDocsVerified = 0, cntOutstandings = 0, cntSettlementBooked = 0;

            var allWFComps = mwfRep.GetMatterComponentsForUser(usr);*/
            //----------------------------------------------------------------------------------------------

            int cntInstrRec = 0, cntDocsSent = 0,
                cntDocsReturned = 0, cntDocsVerified = 0,
                cntReadyToBook = 0, cntOutstandings = 0,
                cntSettlementBooked = 0;

            cntInstrRec = (int)resultList.Select(q => q.InstructionsReceivedCount).Single();
            cntDocsSent = (int)resultList.Select(q => q.DocsSentCount).Single();
            cntDocsReturned = (int)resultList.Select(q => q.DocsReturnedCount).Single();
            cntDocsVerified = (int)resultList.Select(q => q.DocsVerifiedCount).Single();
            cntReadyToBook = (int)resultList.Select(q => q.ReadyToBookCount).Single();
            cntOutstandings = (int)resultList.Select(q => q.Outstandings).Single();
            cntSettlementBooked = (int)resultList.Select(q => q.SettlementCompleteCount).Single();


            //------------------------------------------------------------------------------------------------------

            //Instructions Received
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "InstrReceived", FilterCount = cntInstrRec };
            retList.Add(itm);

            //Docs Sent
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "DocsSent", FilterCount = cntDocsSent };
            retList.Add(itm);

            //Docs Returned
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "DocsReturned", FilterCount = cntDocsReturned };
            retList.Add(itm);

            //Docs Verified
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "DocsVerified", FilterCount = cntDocsVerified };
            retList.Add(itm);

            //Outstanding Requirements
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "ReadyToBook", FilterCount = cntReadyToBook };
            retList.Add(itm);

            //Outstanding Requirements
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "Outstandings", FilterCount = cntOutstandings };
            retList.Add(itm);

            //Settlements Booked
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "SettlementBooked", FilterCount = cntSettlementBooked };
            retList.Add(itm);

            //Settled Today
            //itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "SettledToday", FilterCount = cntSettledToday };
            //retList.Add(itm);

            return retList;
        }

        public IEnumerable<MCE.MatterWebQuickFilterCountView> GetWebQuickFilterCacheCount(User usr, UnitOfWork uow)
        {           
            DateTime? filterDate;
            filterDate = null;

            var retList = new List<MCE.MatterWebQuickFilterCountView>();
            MCE.MatterWebQuickFilterCountView itm;

            context.sp_Slick_InsertWebQuickFilterCache(usr.UserId, filterDate);
            context.SaveChanges();
            uow.CommitTransaction();

            var cacheResultList = context.sp_Slick_GetWebQuickFilterCacheCount(usr.UserId, filterDate, (int)MatterGroupTypeEnum.NewLoan).ToList()
            .Select(s => new Slick_Domain.Models.sp_Slick_GetWebQuickFilterCacheCount_Result()
            {
                LoantrakFilterTypeId = s.LoantrakFilterTypeId,
                QueryCount = s.QueryCount,
            }).ToList();

            //if cache is null	
            if (cacheResultList.Count == 0)
            {
                itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "InProgress", FilterCount = 0 };
                retList.Add(itm);
                itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "OnHold", FilterCount = 0 };
                retList.Add(itm);
                itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "Settled", FilterCount = 0 };
                retList.Add(itm);
                itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "NotProceeding", FilterCount = 0 };
                retList.Add(itm);
                itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "Closed", FilterCount = 0 };
                retList.Add(itm);
                itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "AboutToExpire", FilterCount = 0 };
                retList.Add(itm);
                itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "SettledToday", FilterCount = 0 };
                retList.Add(itm);
                itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "SettlingToday", FilterCount = 0 };
                retList.Add(itm);
                itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "InstrReceived", FilterCount = 0 };
                retList.Add(itm);
                itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "DocsSent", FilterCount = 0 };
                retList.Add(itm);
                itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "DocsReturned", FilterCount = 0 };
                retList.Add(itm);
                itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "DocsVerified", FilterCount = 0 };
                retList.Add(itm);
                itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "ReadyToBook", FilterCount = 0 };
                retList.Add(itm);
                itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "Outstandings", FilterCount = 0 };
                retList.Add(itm);
                itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "SettlementBooked", FilterCount = 0 };
                retList.Add(itm);
                return retList;
            }

            int holdCount = 0;

            try
            {
                holdCount = (int)cacheResultList.Where(q => q.LoantrakFilterTypeId == (int)Slick_Domain.Enums.LoantrakFilterTypeEnum.LoansOnHold).Select(q => q.QueryCount).FirstOrDefault();
            }
            catch (NullReferenceException)
            {
                holdCount = 0;
            }

            //In Progress - Never actually gets used... so for now we'll just set it as 0 to save an SQL call. 
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "InProgress" };

            try
            {
                itm.FilterCount = (int)cacheResultList.Where(q => q.LoantrakFilterTypeId == (int)Slick_Domain.Enums.LoantrakFilterTypeEnum.LoansInProgress).Select(q => q.QueryCount).FirstOrDefault();
            }
            catch (NullReferenceException)
            {
                itm.FilterCount = 0;
            }

            retList.Add(itm);

            //On Hold as Item
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "OnHold" };
            itm.FilterCount = holdCount;
            retList.Add(itm);

            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "Settled" };
            //if (usr.FiltersRefreshedTime < DateTime.Now.AddMinutes(-30)) //if FiltersRefreshedTime 
            //{
            //Settled
            try
            {
                itm.FilterCount = (int)cacheResultList.Where(q => q.LoantrakFilterTypeId == (int)Slick_Domain.Enums.LoantrakFilterTypeEnum.Settled).Select(q => q.QueryCount).FirstOrDefault();
            }
            catch (NullReferenceException)
            {
                itm.FilterCount = 0;
            }
            //} else
            //{
            //    itm.FilterCount = -1; //no need to get a new value
            //}
            retList.Add(itm);

            //Not Proceeding

            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "NotProceeding" };

            try
            {
                itm.FilterCount = (int)cacheResultList.Where(q => q.LoantrakFilterTypeId == (int)Slick_Domain.Enums.LoantrakFilterTypeEnum.LoanNotProceeding).Select(q => q.QueryCount).FirstOrDefault();
            }
            catch (NullReferenceException)
            {
                itm.FilterCount = 0;
            }
            retList.Add(itm);

            //Closed
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "Closed" };
            try
            {
                itm.FilterCount = (int)cacheResultList.Where(q => q.LoantrakFilterTypeId == (int)Slick_Domain.Enums.LoantrakFilterTypeEnum.Closed).Select(q => q.QueryCount).FirstOrDefault();
            }
            catch (NullReferenceException)
            {
                itm.FilterCount = 0;
            }
            retList.Add(itm);


            //About To Expire
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "AboutToExpire" };
            itm.FilterCount = 0;
            itm.FilterCount = (int)cacheResultList.Where(q => q.LoantrakFilterTypeId == (int)Slick_Domain.Enums.LoantrakFilterTypeEnum.LoansAboutToExpire).Select(q => q.QueryCount).FirstOrDefault();
            retList.Add(itm);

            //Settled Today
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "SettledToday" };
            itm.FilterCount = 0;
            itm.FilterCount = (int)cacheResultList.Where(q => q.LoantrakFilterTypeId == (int)Slick_Domain.Enums.LoantrakFilterTypeEnum.SettledToday).Select(q => q.QueryCount).FirstOrDefault();
            retList.Add(itm);

            //Settling Today
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "SettlingToday" };
            itm.FilterCount = 0;
            itm.FilterCount = (int)cacheResultList.Where(q => q.LoantrakFilterTypeId == (int)Slick_Domain.Enums.LoantrakFilterTypeEnum.SettlingToday).Select(q => q.QueryCount).FirstOrDefault();
            retList.Add(itm);


            //--------------------------------------------------------------------------------------------

            //Workflow Counts need to iterate through the matters
            /*
   var mwfRep = new MatterWFRepository(context);
            int cntInstrRec = 0, cntDocsSent = 0, 
                cntDocsReturned = 0, cntDocsVerified = 0, cntOutstandings = 0, cntSettlementBooked = 0;

            var allWFComps = mwfRep.GetMatterComponentsForUser(usr);*/
            //----------------------------------------------------------------------------------------------

            int cntInstrRec = 0, cntDocsSent = 0,
                cntDocsReturned = 0, cntDocsVerified = 0,
                cntReadyToBook = 0, cntOutstandings = 0,
                cntSettlementBooked = 0;

            cntInstrRec = (int)cacheResultList.Where(q => q.LoantrakFilterTypeId == (int)Slick_Domain.Enums.LoantrakFilterTypeEnum.InstructionsReceived).Select(q => q.QueryCount).FirstOrDefault();
            cntDocsSent = (int)cacheResultList.Where(q => q.LoantrakFilterTypeId == (int)Slick_Domain.Enums.LoantrakFilterTypeEnum.DocsSent).Select(q => q.QueryCount).FirstOrDefault();
            cntDocsReturned = (int)cacheResultList.Where(q => q.LoantrakFilterTypeId == (int)Slick_Domain.Enums.LoantrakFilterTypeEnum.DocsReturned).Select(q => q.QueryCount).FirstOrDefault();
            cntDocsVerified = (int)cacheResultList.Where(q => q.LoantrakFilterTypeId == (int)Slick_Domain.Enums.LoantrakFilterTypeEnum.DocsVerified).Select(q => q.QueryCount).FirstOrDefault();
            cntReadyToBook = (int)cacheResultList.Where(q => q.LoantrakFilterTypeId == (int)Slick_Domain.Enums.LoantrakFilterTypeEnum.ReadyToBook).Select(q => q.QueryCount).FirstOrDefault();
            cntOutstandings = (int)cacheResultList.Where(q => q.LoantrakFilterTypeId == (int)Slick_Domain.Enums.LoantrakFilterTypeEnum.OutstandingRequirements).Select(q => q.QueryCount).FirstOrDefault();
            cntSettlementBooked = (int)cacheResultList.Where(q => q.LoantrakFilterTypeId == (int)Slick_Domain.Enums.LoantrakFilterTypeEnum.SettlementBooked).Select(q => q.QueryCount).FirstOrDefault();


            //------------------------------------------------------------------------------------------------------

            //Instructions Received
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "InstrReceived", FilterCount = cntInstrRec };
            retList.Add(itm);

            //Docs Sent
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "DocsSent", FilterCount = cntDocsSent };
            retList.Add(itm);

            //Docs Returned
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "DocsReturned", FilterCount = cntDocsReturned };
            retList.Add(itm);

            //Docs Verified
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "DocsVerified", FilterCount = cntDocsVerified };
            retList.Add(itm);

            //Outstanding Requirements
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "ReadyToBook", FilterCount = cntReadyToBook };
            retList.Add(itm);

            //Outstanding Requirements
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "Outstandings", FilterCount = cntOutstandings };
            retList.Add(itm);

            //Settlements Booked
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "SettlementBooked", FilterCount = cntSettlementBooked };
            retList.Add(itm);

            //Settled Today
            //itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "SettledToday", FilterCount = cntSettledToday };
            //retList.Add(itm);

            //Settled Today
            //itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "SettledToday", FilterCount = cntSettledToday };
            //retList.Add(itm);

            return retList;
        }
        public IEnumerable<MCE.MatterWebQuickFilterCountView> GetWebQuickFilterCount2(User usr)
        {
            DateTime? filterDate;
            filterDate = null;

            var retList = new List<MCE.MatterWebQuickFilterCountView>();
            MCE.MatterWebQuickFilterCountView itm;

            var resultList = context.sp_Slick_GetWebQuickFilter(usr.UserId, filterDate).ToList()
            .Select(s => new Slick_Domain.Models.sp_Slick_GetWebQuickFilter_Result()
            {
                OnHoldCount = s.OnHoldCount,
                InProgressCount = s.InProgressCount,
                SettledCount = s.SettledCount,
                NotProceedingCount = s.NotProceedingCount,
                ClosedCount = s.ClosedCount,
                AboutToExpireCount = s.AboutToExpireCount,
                SettledTodayCount = s.SettledTodayCount,
                SettlingTodayCount = s.SettlingTodayCount,
                InstructionsReceivedCount = s.InstructionsReceivedCount,
                DocsSentCount = s.DocsSentCount,
                DocsReturnedCount = s.DocsReturnedCount,
                DocsVerifiedCount = s.DocsVerifiedCount,
                ReadyToBookCount = s.ReadyToBookCount,
                SettlementCompleteCount = s.SettlementCompleteCount,
                Outstandings = s.Outstandings
            }).ToList();

            //----------------------------------------------------------------------------------------------

            //On Hold

            int holdCount = 0;

            try
            {
                holdCount = (int)resultList.Select(q => q.OnHoldCount).Single();
            }
            catch (NullReferenceException)
            {
                holdCount = 0;
            }

            //In Progress - Never actually gets used... so for now we'll just set it as 0 to save an SQL call. 
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "InProgress" };

            try
            {
                itm.FilterCount = (int)resultList.Select(q => q.InProgressCount).Single();
            }
            catch (NullReferenceException)
            {
                itm.FilterCount = 0;
            }

            retList.Add(itm);

            //On Hold as Item
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "OnHold" };
            itm.FilterCount = holdCount;
            retList.Add(itm);

            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "Settled" };
            //if (usr.FiltersRefreshedTime < DateTime.Now.AddMinutes(-30)) //if FiltersRefreshedTime 
            //{
            //Settled
            try
            {
                itm.FilterCount = (int)resultList.Select(q => q.SettledCount).Single();
            }
            catch (NullReferenceException)
            {
                itm.FilterCount = 0;
            }
            //} else
            //{
            //    itm.FilterCount = -1; //no need to get a new value
            //}
            retList.Add(itm);

            //Not Proceeding

            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "NotProceeding" };

            try
            {
                itm.FilterCount = (int)resultList.Select(q => q.NotProceedingCount).Single();
            }
            catch (NullReferenceException)
            {
                itm.FilterCount = 0;
            }
            retList.Add(itm);

            //Closed
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "Closed" };
            try
            {
                itm.FilterCount = (int)resultList.Select(q => q.ClosedCount).Single();
            }
            catch (NullReferenceException)
            {
                itm.FilterCount = 0;
            }
            retList.Add(itm);


            //About To Expire
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "AboutToExpire" };
            itm.FilterCount = 0;
            itm.FilterCount = (int)resultList.Select(q => q.AboutToExpireCount).Single();
            retList.Add(itm);

            //Settled Today
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "SettledToday" };
            itm.FilterCount = 0;
            itm.FilterCount = (int)resultList.Select(q => q.SettledTodayCount).Single();
            retList.Add(itm);

            //Settling Today
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "SettlingToday" };
            itm.FilterCount = 0;
            itm.FilterCount = (int)resultList.Select(q => q.SettlingTodayCount).Single();
            retList.Add(itm);


            //--------------------------------------------------------------------------------------------

            //Workflow Counts need to iterate through the matters
            /*
   var mwfRep = new MatterWFRepository(context);
            int cntInstrRec = 0, cntDocsSent = 0, 
                cntDocsReturned = 0, cntDocsVerified = 0, cntOutstandings = 0, cntSettlementBooked = 0;

            var allWFComps = mwfRep.GetMatterComponentsForUser(usr);*/
            //----------------------------------------------------------------------------------------------

            int cntInstrRec = 0, cntDocsSent = 0,
                cntDocsReturned = 0, cntDocsVerified = 0,
                cntReadyToBook = 0, cntOutstandings = 0,
                cntSettlementBooked = 0;

            cntInstrRec = (int)resultList.Select(q => q.InstructionsReceivedCount).Single();
            cntDocsSent = (int)resultList.Select(q => q.DocsSentCount).Single();
            cntDocsReturned = (int)resultList.Select(q => q.DocsReturnedCount).Single();
            cntDocsVerified = (int)resultList.Select(q => q.DocsVerifiedCount).Single();
            cntReadyToBook = (int)resultList.Select(q => q.ReadyToBookCount).Single();
            cntOutstandings = (int)resultList.Select(q => q.Outstandings).Single();
            cntSettlementBooked = (int)resultList.Select(q => q.SettlementCompleteCount).Single();


            //------------------------------------------------------------------------------------------------------

            //Instructions Received
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "InstrReceived", FilterCount = cntInstrRec };
            retList.Add(itm);

            //Docs Sent
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "DocsSent", FilterCount = cntDocsSent };
            retList.Add(itm);

            //Docs Returned
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "DocsReturned", FilterCount = cntDocsReturned };
            retList.Add(itm);

            //Docs Verified
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "DocsVerified", FilterCount = cntDocsVerified };
            retList.Add(itm);

            //Outstanding Requirements
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "ReadyToBook", FilterCount = cntReadyToBook };
            retList.Add(itm);

            //Outstanding Requirements
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "Outstandings", FilterCount = cntOutstandings };
            retList.Add(itm);

            //Settlements Booked
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "SettlementBooked", FilterCount = cntSettlementBooked };
            retList.Add(itm);

            //Settled Today
            //itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "SettledToday", FilterCount = cntSettledToday };
            //retList.Add(itm);

            return retList;
        }


        //----------------------------------------------------------------------------------------------------------------------------------------------------------------
        public int CreatePlaceholderMatter(int lenderId, string lenderReferencePrimary, int userId, LoanTypeEnum? loanType, int? stateId=null, bool? isTopUp = null)
        {
            var instrDateRec = TimeZoneHelper.ForceBusinessHours(DateTime.Now, (int)Slick_Domain.Enums.StateIdEnum.NSW, context, lenderId);
            var instrDateRecUTC = TimeZoneHelper.ForceBusinessHours(DateTime.Now, (int)Slick_Domain.Enums.StateIdEnum.NSW, context, lenderId).ToUniversalTime();

            var instrDeadlineOffset = context.Lenders.FirstOrDefault(l => l.LenderId == lenderId).InstructionDeadlineOffsetDays ?? 1;
            var instrDeadlineDate = TimeZoneHelper.AddBusinessDays(instrDateRec, instrDeadlineOffset, stateId ?? (int)StateIdEnum.NSW, context).ToUniversalTime();

            int TictocLenderId = context.Lenders.FirstOrDefault(x => x.LenderNameShort == "ABL").LenderId;
            int? mortMgrId = null;
            if (lenderId == TictocLenderId) mortMgrId = context.MortMgrs.FirstOrDefault(x => x.MortMgrName == "Tictoc Online" || x.MortMgrNameShort == "Tictoc" || x.MortMgrNameShort == "Tictoc Online").MortMgrId;

            int updatedByUserId = GlobalVars.CurrentUserId.HasValue && GlobalVars.CurrentUserId.Value >= 1 ? GlobalVars.CurrentUserId.Value : DomainConstants.SystemUserId;

            Matter matter = new Matter()
            {
                MatterId = GetNextMatterId(),
                MatterDescription = lenderReferencePrimary,
                LenderRefNo = lenderReferencePrimary,
                LenderSecondRefNo = "",
                SecondaryRefNo = "",
                InstructionsReceivedDate = instrDateRecUTC,
                InstructionDeadlineDate = instrDeadlineDate,
                IsTopUp = isTopUp,
                LenderId = lenderId,
                MortMgrId = mortMgrId,
                MatterGroupTypeId = (int)Slick_Domain.Enums.MatterGroupTypeEnum.NewLoan,
                MatterStatusTypeId = (int)Slick_Domain.Enums.MatterStatusTypeEnum.InProgress,
                StateId = stateId.HasValue ? stateId.Value: (int)Slick_Domain.Enums.StateIdEnum.NSW,
                FileOpenedDate = System.DateTime.Now,
                FileOpenedDateOnly = System.DateTime.Now,
                CheckInstructionsDate = System.DateTime.Now,
                UpdatedByUserId = updatedByUserId,
                FileOwnerUserId = userId,
                UpdatedDate = System.DateTime.Now,
                ReworkCount = 0,

            };
            if (loanType.HasValue)
            {
                matter.LoanTypeId = (int)loanType.Value;
            }
       
            try
            {
                context.Matters.Add(matter);
                context.SaveChanges();
            }
            catch(Exception e)
            {
       
                throw e;
            }
            
            MatterWFComponent matterWfComponent = new MatterWFComponent()
            {
                MatterId = matter.MatterId,
                WFComponentId = (int)Slick_Domain.Enums.WFComponentEnum.CreateMatter,
                WFComponentStatusTypeId = (int)Slick_Domain.Enums.MatterWFComponentStatusTypeEnum.Starting,
                DisplayOrder = 1,
                DisplayStatusTypeId = (int)Slick_Domain.Enums.DisplayStatusTypeEnum.Display,
                UpdatedByUserId = updatedByUserId,
                UpdatedDate = System.DateTime.UtcNow,
                DefTaskAllocTypeId = (int)Slick_Domain.Enums.TaskAllocationTypeEnum.Unallocated,
            };
           
            try
            {
                context.MatterWFComponents.Add(matterWfComponent);
                context.SaveChanges();
            }
            catch (Exception ex)
            {
                throw ex;
            }

            var mwfRep = new MatterWFRepository(context);
            int? bfwId = mwfRep.GetBestWorkFlowTemplateIdForMatter((int)Slick_Domain.Enums.MatterGroupTypeEnum.NewLoan, matter.StateId, matter.LenderId, matter.MortMgrId,matter.LoanTypeId).HasValue ? 
            mwfRep.GetBestWorkFlowTemplateIdForMatter((int)Slick_Domain.Enums.MatterGroupTypeEnum.NewLoan, matter.StateId, matter.LenderId, matter.MortMgrId, matter.LoanTypeId) : null;
            mwfRep.MergeMatterWorkflow(matter.MatterId, matterWfComponent.MatterWFComponentId, false, bfwId.Value, null, updatedByUserId: updatedByUserId);
            
            //using (UnitOfWork uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadCommitted))
            //{
            //    try
            //    {
            //        //uow.GetMatterWFRepositoryInstance().
            //        //                     MarkComponentAsInProgress(matterWfComponent.MatterWFComponentId);
            //        //MatterSearch mSearch = new MatterSearch { MatterId = matter.MatterId, MatterSearchText = uow.GetMatterRepositoryInstance().getMatterSearchStr(uow.GetMatterRepositoryInstance().GetMatterView(matter.MatterId)) };
            //        //uow.Context.MatterSearches.Add(mSearch);
            //        //uow.CommitTransaction();
            //    }
            //    catch (Exception ex)
            //    {
            //        uow.RollbackTransaction();
            //        throw ex;
            //    }
            //}

            return matter.MatterId;
        }
        public int CreateMatter(int lenderId, string borrowersDesc, string lenderReferencePrimary, Slick_Domain.Enums.MatterGroupTypeEnum matterGroupTypeEnum, Slick_Domain.Enums.StateIdEnum stateIdEnum, int? mortMgrId, int? fileOwnerId, int? brokerId, System.DateTime openedDate,bool auto, string lenderReferenceSecondary = "")
        {
            var instrDateRec = TimeZoneHelper.ForceBusinessHours(DateTime.Now, (int)Slick_Domain.Enums.StateIdEnum.NSW, context, lenderId);
            var instrDateRecUTC = instrDateRec.ToUniversalTime();
            int offsetDays = context.Lenders.FirstOrDefault(l => l.LenderId == lenderId).InstructionDeadlineOffsetDays ?? 1;
            var instrDeadlineDate = TimeZoneHelper.AddBusinessDays(instrDateRec, offsetDays, (int)stateIdEnum, context).ToUniversalTime();



            Matter matter = new Matter()
            {
                MatterId = GetNextMatterId(),
                MatterDescription = borrowersDesc,
                LenderRefNo = lenderReferencePrimary,
                LenderSecondRefNo = lenderReferenceSecondary == "" ? null : lenderReferenceSecondary,
                SecondaryRefNo = lenderReferenceSecondary == "" ? null : lenderReferenceSecondary,
                LenderId = lenderId,
                BrokerId = brokerId ?? null,
                MortMgrId = mortMgrId ?? null,
                MatterGroupTypeId = (int)matterGroupTypeEnum,
                MatterStatusTypeId = (int)Slick_Domain.Enums.MatterStatusTypeEnum.InProgress,
                StateId = (int)stateIdEnum,
                RawInstructionTime = DateTime.UtcNow,
                InstructionsReceivedDate = instrDateRecUTC,
                InstructionDeadlineDate = instrDeadlineDate,
                FileOpenedDate = openedDate,
                FileOpenedDateOnly = openedDate,
                CheckInstructionsDate = instrDateRec.ToLocalTime(),
                CheckInstructionsDateTime = instrDateRec.ToLocalTime(), 
                UpdatedByUserId = GlobalVars.CurrentUser.UserId == 0 ? 1 : GlobalVars.CurrentUser.UserId,
                UpdatedDate = openedDate,
                FileOwnerUserId = fileOwnerId.HasValue ? fileOwnerId.Value : 1,
                ReworkCount = 0,
            };
           
            try
            {
                context.Matters.Add(matter);
                context.SaveChanges();
                
            }
            catch(Exception ex)
            {
                _logger.Error(ex);
                throw ex;
            }
            
            return matter.MatterId;

        }


        public class ReducedPerpetualDetails
        {
            public int MatterWFLetterToCustodianId { get; set; }
            public int MatterWFComponentId { get; set; }
            public DateTime SentDate { get; set; }
            public bool DateIsUTC { get; set; }
            public string Fullname { get; set; }
            public int PerpetualStatusTypeId { get; set; }
            public string PerpetualStatusTypeName { get; set; }
            public string DocumentName { get; set; }

        }

        public IEnumerable<MatterCustomEntities.MatterPerpetualDetailsView> GetMatterCustodianIntegrationDetails(int matterId)
        {
            var validStatuses = new List<int>() { (int)PerpetualStatusTypeEnum.Ready, (int)PerpetualStatusTypeEnum.Sent, (int)PerpetualStatusTypeEnum.ErrorSending };
            var items = context.MatterWFLetterToCustodianItems.Where(p => p.PerpetualStatusTypeId != (int)PerpetualStatusTypeEnum.Paper
                && p.MatterWFLetterToCustodian.MatterWFComponent.MatterId == matterId
                && p.MatterWFLetterToCustodian.MatterWFComponent.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.Complete
                && p.Attached && p.MatterDocumentId.HasValue && p.IsDigital
                && validStatuses.Contains(p.PerpetualStatusTypeId)
                && p.MatterWFLetterToCustodian.ToSendToPerpetual)
                .Select(p => new ReducedPerpetualDetails
                {
                    MatterWFLetterToCustodianId = p.MatterWFLetterToCustodianId,
                    MatterWFComponentId = p.MatterWFLetterToCustodian.MatterWFComponentId,
                    SentDate = p.SentToPerpetualDate ?? p.MatterWFLetterToCustodian.UpdatedDate,
                    DateIsUTC = p.SentToPerpetualDate.HasValue,
                    Fullname = p.MatterWFLetterToCustodian.User.Fullname,
                    PerpetualStatusTypeName = p.PerpertualStatusType.PerpetualStatusTypeName,
                    PerpetualStatusTypeId = p.PerpetualStatusTypeId,
                    DocumentName = p.DocumentName
                })
            .ToList();

            //v gross
            items.ForEach(x => { if (x.DateIsUTC) { x.SentDate = x.SentDate.ToLocalTime(); } });

            return items.GroupBy(g => new{ g.MatterWFLetterToCustodianId, g.PerpetualStatusTypeId})
            .Select(g => new MatterCustomEntities.MatterPerpetualDetailsView
            {
                DisplayDate= g.OrderByDescending(o => o.SentDate).Select(s => s.SentDate).FirstOrDefault(),
                MatterWFComponentId = g.FirstOrDefault().MatterWFComponentId,
                SentByUserName = g.FirstOrDefault().Fullname,
                PerpetualStatusTypeId = g.FirstOrDefault().PerpetualStatusTypeId,
                PerpetualStatusTypeName = g.FirstOrDefault().PerpetualStatusTypeName,
                DocumentCount = g.Count(),
                DocumentNames = g.Select(d=>d.DocumentName).ToList()
            })
            .OrderBy(o=>o.DisplayDate)
            .ToList();

        }



        public (int,int) CreateMatter(int lenderId, string borrowersDesc, string lenderReference, Slick_Domain.Enums.MatterGroupTypeEnum matterGroupTypeEnum, Slick_Domain.Enums.StateIdEnum stateIdEnum, string deadlineDate, string arrivalTime, int? mortMgrId, int? fileOwnerId, int? brokerId, string filePath, string securityAddress, bool auto, Slick_Domain.Enums.StateIdEnum instrStateIdEnum, int? loanType = null)
        {
            //return 1;
            Matter matter = new Matter();
            matter.MatterId = GetNextMatterId();
            matter.MatterDescription = borrowersDesc;
            matter.LenderRefNo = lenderReference;
            matter.LenderId = lenderId;
            matter.MatterGroupTypeId = (int)matterGroupTypeEnum;
            matter.InstructionStateId = (int)(instrStateIdEnum);
            DateTime arrivalDate = System.DateTime.Parse(arrivalTime);
            System.DateTime localInstrDate = TimeZoneHelper.ForceBusinessHours(arrivalDate, (int)instrStateIdEnum, lenderId, context);
            System.DateTime instrDateToAdd = localInstrDate.ToUniversalTime();
            //instrDateToAdd = instrDateToAdd.ToUniversalTime();
            matter.InstructionDeadlineDate = TimeZoneHelper.AddBusinessDays(localInstrDate, 1, (int)instrStateIdEnum, context).ToUniversalTime();
            matter.InstructionsReceivedDate = instrDateToAdd;
            matter.RawInstructionTime = arrivalDate;
            if (loanType != null) matter.LoanTypeId = loanType;
            if (brokerId.HasValue && brokerId > 0)
            {
                matter.BrokerId = brokerId;
            }
            matter.StateId = (int)stateIdEnum;
            if (mortMgrId != null)
            {
                matter.MortMgrId = mortMgrId;
            }
            matter.MatterStatusTypeId = (int)Slick_Domain.Enums.MatterStatusTypeEnum.InProgress;
            matter.FileOpenedDate = System.DateTime.UtcNow;
            matter.FileOpenedDateOnly = System.DateTime.UtcNow;
            matter.UpdatedByUserId = 2 == 0 ? 1 : GlobalVars.CurrentUser.UserId;
            matter.UpdatedDate = System.DateTime.UtcNow;
            matter.FileOwnerUserId = fileOwnerId.HasValue ? fileOwnerId.Value : 1;
            matter.ReworkCount = 0;
            matter.IsTestFile = false;
            matter.InstructionRefNo = lenderReference;
            matter.InstructionSecurityAddress = securityAddress;
            var userRep = new UserRepository(context);
            matter.User = userRep.GetUser(fileOwnerId.HasValue ? fileOwnerId.Value : 1);
            try
            {
                context.Matters.Add(matter);
                context.SaveChanges();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            var mwfRep = new MatterWFRepository(context);
            int createMatterId= mwfRep.InitialiseCreateMatterWFComponent(matter.MatterId,null);

            var mwfXml = new MatterWFXML();
            mwfXml.MatterWFComponentId = createMatterId;
            mwfXml.Filename = filePath;
            mwfXml.LenderXML = File.ReadAllText(filePath);
            mwfXml.SequenceNo = 0;
            mwfXml.UpdatedDate = DateTime.Now;
            mwfXml.UpdatedByUserId = GlobalVars.CurrentUser.UserId;
            context.MatterWFXMLs.Add(mwfXml);
            context.SaveChanges();
            List<string> filePaths = new List<string>() {filePath };
            //filePaths.Add(filePath);
            XmlProcessor xmlProcessor = new XmlProcessor();
            try
            {
                var ieXmlDocDetailFilePaths = Slick_Domain.Utils.DocumentUtils.CreateEnumerableXMLDocuments(filePaths);
                var ieXmlMatterDetailDocs = Slick_Domain.Utils.DocumentUtils.LoadXMLDataOntoMatter(ieXmlDocDetailFilePaths, matter);

            
                XmlProcessor.CreateAnswers(matter.MatterId, createMatterId, matter.LenderId, matter.MatterGroupTypeId, ieXmlMatterDetailDocs);
            }
            catch
            {
                Console.WriteLine("Something went wrong with XML Extraction - Matter ID: " + matter.MatterId);
                XmlProcessor.CreateAnswers(matter.MatterId, createMatterId, matter.LenderId, matter.MatterGroupTypeId);

            }
            //var resp = Slick_Domain.Utils.DocumentUtils.LoadXMLDataOntoMatter(ieXmlDocDetailFilePaths,matter);
            //Slick_Domain.Utils.DocumentUtils.CreateAndExtractAnswersModified(matter, comp);


            int? bestWF = mwfRep.GetBestWorkFlowTemplateIdForMatter(matter.MatterGroupTypeId, matter.StateId, matter.LenderId, null, matter.LoanTypeId);
            int bestWFId = bestWF ?? default(int);
            var test = mwfRep.MergeMatterWorkflow(matter.MatterId, createMatterId, false,bestWFId, null);
            //var test = mwfRep.MergeMatterWorkflow(matter.MatterId, CreateMatter,)

            var currMatterView = mwfRep.GetMatterWFComponentView(createMatterId);
            var result = mwfRep.ProgressToNextMileStone(currMatterView);


            mwfRep.UpdateDueDates(matter.MatterId);


            //mwfRep.MarkComponentAsComplete(result.MatterWFComponentId);
            //result = context.MatterWFComponents.Where((m) => m.MatterId == matter.MatterId && m.WFComponentStatusTypeId != (int)Slick_Domain.Enums.MatterWFComponentStatusTypeEnum.Complete).First();
            //mwfRep.MarkComponentAsInProgress(result.MatterWFComponentId);
            //mwfRep.UpdateDueDates(matter.MatterId);

            //}


            //result = context.MatterWFComponents.Where((m) => m.MatterId == matter.MatterId && m.WFComponentStatusTypeId != (int)Slick_Domain.Enums.MatterWFComponentStatusTypeEnum.Complete).First();




            return (matter.MatterId,result.MatterWFComponentId);
        }

        public IEnumerable<MCE.MatterWebQuickFilterCountView> GetWebQuickFilterCountDischarges(User usr)
        {

            IQueryable<Matter> qry = context.Matters.AsNoTracking();
            var retList = new List<MCE.MatterWebQuickFilterCountView>();
            MCE.MatterWebQuickFilterCountView itm;

            switch (usr.UserTypeId)
            {
                case (int)UserTypeEnum.Lender:
                    if (!usr.HasStateRestrictions)
                        qry = context.Matters.AsNoTracking().Where(m => m.LenderId == usr.LenderId && m.MatterGroupTypeId == (int)Slick_Domain.Enums.MatterGroupTypeEnum.Discharge &&  m.IsTestFile == false);
                    else
                    {
                        IEnumerable<int> restrictions = context.UserStateRestrictions.Where(r => r.UserId == usr.UserId).Select(r => r.StateId);
                        qry = context.Matters.AsNoTracking().Where(m => m.LenderId == usr.LenderId && restrictions.Contains(m.InstructionStateId ?? m.StateId) && m.MatterGroupTypeId == (int)Slick_Domain.Enums.MatterGroupTypeEnum.Discharge && m.IsTestFile == false);
                    }
                    break;
                case (int)UserTypeEnum.MortMgr:
                    qry = qry.Where(m => m.MortMgrId == usr.MortMgrId && m.MatterGroupTypeId == (int)Slick_Domain.Enums.MatterGroupTypeEnum.Discharge && m.IsTestFile == false);
                    break;
                case (int)UserTypeEnum.Broker:
                    qry = qry.Where(m => m.BrokerId == usr.BrokerId || m.Broker.PrimaryContactId == usr.Broker.PrimaryContactId && m.MatterGroupTypeId != (int)Slick_Domain.Enums.MatterGroupTypeEnum.Discharge && m.IsTestFile == false);
                    break;
                case (int)UserTypeEnum.RelationshipManager:
                    int rmLenderId = 0;
                    List<int> rmBrokers = new List<int>();
                    rmLenderId = context.RelationshipManagers.Where(r => r.RelationshipManagerId == usr.RelationshipManagerId).Select(l => l.LenderId).FirstOrDefault();
                    rmBrokers = context.BrokerRelationshipManagers.Where(r => r.RelationshipManagerId == usr.RelationshipManagerId).Select(l => l.BrokerId).ToList();
                    qry = qry.Where(m => (m.LenderId == rmLenderId && rmBrokers.Contains((int)m.BrokerId)) && m.MatterGroupTypeId == (int)Slick_Domain.Enums.MatterGroupTypeEnum.Discharge);
                    break;
            }

            var qryData = qry.Select(m => new { m.MatterId, m.MatterGroupTypeId, m.MatterStatusTypeId, m.MatterStatusType.MatterStatusTypeName, m.FileOpenedDate, m.SettlementSchedule }).ToList();

            // This query gets all the counts by Matter Status (In Prog, On Hold, Settled etc)
            var qryByStatus = qryData.GroupBy(g => new { g.MatterStatusTypeId, g.MatterStatusTypeName })
                                .Select(x => new { x.Key.MatterStatusTypeId, x.Key.MatterStatusTypeName, matterCount = x.Count() });


            int cntInstrRec = 0, cntSecPacket = 0, cntSettlementBooked = 0, cntPayoutProvided = 0;
            var allQry = qryData.Select(i => new { i.MatterId, i.MatterStatusTypeId, i.MatterGroupTypeId }).Where(j => j.MatterGroupTypeId == (int)MatterGroupTypeEnum.Discharge && (j.MatterStatusTypeId == (int)MatterStatusTypeEnum.InProgress || j.MatterStatusTypeId == (int)MatterStatusTypeEnum.OnHold));
            var mwfRep = new MatterWFRepository(context);
            //var userWF = mwfRep.GetMatterComponentsForUser(usr);
            var userWF = mwfRep.GetMatterComponentsForMatters(allQry.Select(x => x.MatterId).ToList());

            //Settled Today
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "SettledToday" };
            itm.FilterCount = 0;
            itm.FilterCount = qryData.Where(q => ((q.MatterStatusTypeId == (int)MatterStatusTypeEnum.Settled || q.MatterStatusTypeId == (int)MatterStatusTypeEnum.Closed) &&
                                                  q.SettlementSchedule != null &&
                                                  q.SettlementSchedule.SettlementDate.Date == DateTime.Today)).Count();
            retList.Add(itm);

            //Settling Today
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "SettlingToday" };
            itm.FilterCount = 0;
            itm.FilterCount = qryData.Where(q => ((q.MatterStatusTypeId == (int)MatterStatusTypeEnum.InProgress || q.MatterStatusTypeId == (int)MatterStatusTypeEnum.OnHold) &&
                                                   (q.SettlementSchedule != null && q.SettlementSchedule.SettlementDate.Date == DateTime.Today)
            )).Count();
            retList.Add(itm);


            int holdCount = 0;
            //On Hold as Item
            try
            {
                holdCount = qryByStatus.FirstOrDefault(q => q.MatterStatusTypeId == (int)MatterStatusTypeEnum.OnHold).matterCount;
            }
            catch (NullReferenceException)
            {
                holdCount = 0;
            }

            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "OnHold" };
            itm.FilterCount = holdCount;
            retList.Add(itm);
          
            foreach (var mt in allQry)
            {
                var matId = mt.MatterId;
                var wfComps = userWF.Where(m => m.MatterId == matId).OrderBy(o => o.DisplayOrder).ToList();
                if (!mwfRep.HasMilestoneCompletedNotDeleted(wfComps, (int)WFComponentEnum.SecurityPacketReceived))
                    cntInstrRec++;
                else if (
                        !mwfRep.HasMilestoneCompletedNotDeleted(wfComps, (int)WFComponentEnum.BookSettlement) &&
                       mwfRep.HasMilestoneCompletedNotDeleted(wfComps, (int)WFComponentEnum.SecurityPacketReceived)
                    )
                    cntSecPacket++;
                else if
                    (
                       !mwfRep.HasMilestoneCompletedNotDeleted(wfComps, (int)WFComponentEnum.PrepareSettlementDischQA) &&
                       mwfRep.HasMilestoneCompletedNotDeleted(wfComps, (int)WFComponentEnum.BookSettlement)
                     )
                    cntSettlementBooked++;
                else if
                    (
                       !mwfRep.HasMilestoneCompletedNotDeleted(wfComps, (int)WFComponentEnum.SettlementCompletedDischarge) &&
                       mwfRep.HasMilestoneCompletedNotDeleted(wfComps, (int)WFComponentEnum.PrepareSettlementDischQA)
                    )
                    cntPayoutProvided++;
            }

            //Instructions Received
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "InstrReceived", FilterCount = cntInstrRec };
            retList.Add(itm);

            //Security Packet Received
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "PacketReceived", FilterCount = cntSecPacket };
            retList.Add(itm);

            //Settlement Booked
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "SettlementBooked", FilterCount = cntSettlementBooked };
            retList.Add(itm);

            //Payout Provided
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "PayoutProvided", FilterCount = cntPayoutProvided };
            retList.Add(itm);

            return retList;
        }

        //using sproc	
        public IEnumerable<MCE.MatterWebQuickFilterCountView> GetWebQuickFilterCountDischargesSproc(User usr)
        {
            DateTime? filterDate;
            filterDate = null;
            var retList = new List<MCE.MatterWebQuickFilterCountView>();
            MCE.MatterWebQuickFilterCountView itm;
            var resultList = context.sp_Slick_GetWebQuickFilterDischarges(usr.UserId, filterDate).ToList()
            .Select(s => new Slick_Domain.Models.sp_Slick_GetWebQuickFilterDischarges_Result()
            {
                OnHoldCount = s.OnHoldCount,
                InProgressCount = s.InProgressCount,
                SettledTodayCount = s.SettledTodayCount,
                SettlingTodayCount = s.SettlingTodayCount,
                InstructionsReceivedCount = s.InstructionsReceivedCount,
                PacketsReceivedCount = s.PacketsReceivedCount,
                SettlementCompleteCount = s.SettlementCompleteCount,
                PayoutProvidedCount = s.PayoutProvidedCount
            }).ToList();
            int holdCount = 0, cntInstrRec = 0, cntSecPacket = 0, cntSettlementBooked = 0, cntPayoutProvided = 0;
            try
            {
                holdCount = (int)resultList.Select(q => q.OnHoldCount).Single();
            }
            catch (NullReferenceException)
            {
                holdCount = 0;
            }
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "OnHold" };
            itm.FilterCount = holdCount;
            retList.Add(itm);
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "InProgress" };
            try
            {
                itm.FilterCount = (int)resultList.Select(q => q.InProgressCount).Single();
            }
            catch (NullReferenceException)
            {
                itm.FilterCount = 0;
            }
            retList.Add(itm);
            //Settled Today	
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "SettledToday" };
            itm.FilterCount = 0;
            itm.FilterCount = (int)resultList.Select(q => q.SettledTodayCount).Single();
            retList.Add(itm);
            //Settling Today	
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "SettlingToday" };
            itm.FilterCount = 0;
            itm.FilterCount = (int)resultList.Select(q => q.SettlingTodayCount).Single();
            retList.Add(itm);
            //foreach (var mt in allQry)	
            //{	
            //    var matId = mt.MatterId;	
            //    var wfComps = userWF.Where(m => m.MatterId == matId).OrderBy(o => o.DisplayOrder).ToList();	
            //    if (!mwfRep.HasMilestoneCompletedNotDeleted(wfComps, (int)WFComponentEnum.SecurityPacketReceived))	
            //        cntInstrRec++;	
            //    else if (	
            //            !mwfRep.HasMilestoneCompletedNotDeleted(wfComps, (int)WFComponentEnum.BookSettlement) &&	
            //           mwfRep.HasMilestoneCompletedNotDeleted(wfComps, (int)WFComponentEnum.SecurityPacketReceived)	
            //        )	
            //        cntSecPacket++;	
            //    else if	
            //        (	
            //           !mwfRep.HasMilestoneCompletedNotDeleted(wfComps, (int)WFComponentEnum.PrepareSettlementDischQA) &&	
            //           mwfRep.HasMilestoneCompletedNotDeleted(wfComps, (int)WFComponentEnum.BookSettlement)	
            //         )	
            //        cntSettlementBooked++;	
            //    else if	
            //        (	
            //           !mwfRep.HasMilestoneCompletedNotDeleted(wfComps, (int)WFComponentEnum.SettlementCompletedDischarge) &&	
            //           mwfRep.HasMilestoneCompletedNotDeleted(wfComps, (int)WFComponentEnum.PrepareSettlementDischQA)	
            //        )	
            //        cntPayoutProvided++;	
            //}	
            cntInstrRec = (int)resultList.Select(q => q.InstructionsReceivedCount).Single();
            cntSecPacket = (int)resultList.Select(q => q.PacketsReceivedCount).Single();
            cntSettlementBooked = (int)resultList.Select(q => q.SettlementCompleteCount).Single();
            cntPayoutProvided = (int)resultList.Select(q => q.PayoutProvidedCount).Single();
            //Instructions Received	
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "InstrReceived", FilterCount = cntInstrRec };
            retList.Add(itm);
            //Security Packet Received	
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "PacketReceived", FilterCount = cntSecPacket };
            retList.Add(itm);
            //Settlement Booked	
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "SettlementBooked", FilterCount = cntSettlementBooked };
            retList.Add(itm);
            //Payout Provided	
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "PayoutProvided", FilterCount = cntPayoutProvided };
            retList.Add(itm);
            return retList;
        }

        public IEnumerable<MCE.MatterWebQuickFilterCountView> GetWebQuickFilterCountCacheDischarges(User usr, UnitOfWork uow)
        {
            DateTime? filterDate;
            filterDate = null;

            var retList = new List<MCE.MatterWebQuickFilterCountView>();
            MCE.MatterWebQuickFilterCountView itm;

            context.sp_Slick_InsertWebQuickFilterCacheDischarges(usr.UserId, filterDate);
            context.SaveChanges();
            uow.CommitTransaction();

            var cacheResultList = context.sp_Slick_GetWebQuickFilterCacheCount(usr.UserId, filterDate, (int)MatterGroupTypeEnum.Discharge).ToList()
            .Select(s => new Slick_Domain.Models.sp_Slick_GetWebQuickFilterCacheCount_Result()
            {
                LoantrakFilterTypeId = s.LoantrakFilterTypeId,
                QueryCount = s.QueryCount,
            }).ToList();

            //if cache is null	
            if (cacheResultList.Count == 0)
            {
                itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "OnHold", FilterCount = 0 };
                retList.Add(itm);
                itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "InProgress", FilterCount = 0 };
                retList.Add(itm);
                itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "SettledToday", FilterCount = 0 };
                retList.Add(itm);
                itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "SettlingToday", FilterCount = 0 };
                retList.Add(itm);
                itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "InstrReceived", FilterCount = 0 };
                retList.Add(itm);
                itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "PacketReceived", FilterCount = 0 };
                retList.Add(itm);
                itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "SettlementBooked", FilterCount = 0 };
                retList.Add(itm);
                itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "PayoutProvided", FilterCount = 0 };
                retList.Add(itm);
                return retList;
            }

            int holdCount = 0, cntInstrRec = 0, cntSecPacket = 0, cntSettlementBooked = 0, cntPayoutProvided = 0;
            try
            {
                holdCount = (int)cacheResultList.Where(q => q.LoantrakFilterTypeId == (int)Slick_Domain.Enums.LoantrakFilterTypeEnum.LoansOnHold).Select(q => q.QueryCount).FirstOrDefault();
            }
            catch (NullReferenceException)
            {
                holdCount = 0;
            }
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "OnHold" };
            itm.FilterCount = holdCount;
            retList.Add(itm);
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "InProgress" };
            try
            {
                itm.FilterCount = (int)cacheResultList.Where(q => q.LoantrakFilterTypeId == (int)Slick_Domain.Enums.LoantrakFilterTypeEnum.LoansInProgress).Select(q => q.QueryCount).FirstOrDefault();
            }
            catch (NullReferenceException)
            {
                itm.FilterCount = 0;
            }
            retList.Add(itm);
            //Settled Today	
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "SettledToday" };
            itm.FilterCount = 0;
            itm.FilterCount = (int)cacheResultList.Where(q => q.LoantrakFilterTypeId == (int)Slick_Domain.Enums.LoantrakFilterTypeEnum.SettledToday).Select(q => q.QueryCount).FirstOrDefault();
            retList.Add(itm);
            //Settling Today	
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "SettlingToday" };
            itm.FilterCount = 0;
            itm.FilterCount = (int)cacheResultList.Where(q => q.LoantrakFilterTypeId == (int)Slick_Domain.Enums.LoantrakFilterTypeEnum.SettlingToday).Select(q => q.QueryCount).FirstOrDefault();
            retList.Add(itm);
            //foreach (var mt in allQry)	
            //{	
            //    var matId = mt.MatterId;	
            //    var wfComps = userWF.Where(m => m.MatterId == matId).OrderBy(o => o.DisplayOrder).ToList();	
            //    if (!mwfRep.HasMilestoneCompletedNotDeleted(wfComps, (int)WFComponentEnum.SecurityPacketReceived))	
            //        cntInstrRec++;	
            //    else if (	
            //            !mwfRep.HasMilestoneCompletedNotDeleted(wfComps, (int)WFComponentEnum.BookSettlement) &&	
            //           mwfRep.HasMilestoneCompletedNotDeleted(wfComps, (int)WFComponentEnum.SecurityPacketReceived)	
            //        )	
            //        cntSecPacket++;	
            //    else if	
            //        (	
            //           !mwfRep.HasMilestoneCompletedNotDeleted(wfComps, (int)WFComponentEnum.PrepareSettlementDischQA) &&	
            //           mwfRep.HasMilestoneCompletedNotDeleted(wfComps, (int)WFComponentEnum.BookSettlement)	
            //         )	
            //        cntSettlementBooked++;	
            //    else if	
            //        (	
            //           !mwfRep.HasMilestoneCompletedNotDeleted(wfComps, (int)WFComponentEnum.SettlementCompletedDischarge) &&	
            //           mwfRep.HasMilestoneCompletedNotDeleted(wfComps, (int)WFComponentEnum.PrepareSettlementDischQA)	
            //        )	
            //        cntPayoutProvided++;	
            //}	
            cntInstrRec = (int)cacheResultList.Where(q => q.LoantrakFilterTypeId == (int)Slick_Domain.Enums.LoantrakFilterTypeEnum.InstructionsReceived).Select(q => q.QueryCount).FirstOrDefault();
            cntSecPacket = (int)cacheResultList.Where(q => q.LoantrakFilterTypeId == (int)Slick_Domain.Enums.LoantrakFilterTypeEnum.SecurityPacketReceived).Select(q => q.QueryCount).FirstOrDefault();
            cntSettlementBooked = (int)cacheResultList.Where(q => q.LoantrakFilterTypeId == (int)Slick_Domain.Enums.LoantrakFilterTypeEnum.SettlementBooked).Select(q => q.QueryCount).FirstOrDefault();
            cntPayoutProvided = (int)cacheResultList.Where(q => q.LoantrakFilterTypeId == (int)Slick_Domain.Enums.LoantrakFilterTypeEnum.PayoutProvided).Select(q => q.QueryCount).FirstOrDefault();
            //Instructions Received	
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "InstrReceived", FilterCount = cntInstrRec };
            retList.Add(itm);
            //Security Packet Received	
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "PacketReceived", FilterCount = cntSecPacket };
            retList.Add(itm);
            //Settlement Booked	
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "SettlementBooked", FilterCount = cntSettlementBooked };
            retList.Add(itm);
            //Payout Provided	
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "PayoutProvided", FilterCount = cntPayoutProvided };
            retList.Add(itm);
            return retList;
        }


        public IEnumerable<MCE.MatterWebQuickFilterCountView> GetWebQuickFilterCountForRM(User usr, int relationshipmanagerId)
        {
            DateTime? filterDate;
            filterDate = null;

            var retList = new List<MCE.MatterWebQuickFilterCountView>();
            MCE.MatterWebQuickFilterCountView itm;

            var resultList = context.sp_Slick_GetWebQuickFilterforRM(usr.UserId, relationshipmanagerId, filterDate).ToList()
            .Select(s => new Slick_Domain.Models.sp_Slick_GetWebQuickFilterforRM_Result()
            {
                OnHoldCount = s.OnHoldCount,
                InProgressCount = s.InProgressCount,
                SettledCount = s.SettledCount,
                NotProceedingCount = s.NotProceedingCount,
                ClosedCount = s.ClosedCount,
                AboutToExpireCount = s.AboutToExpireCount,
                SettledTodayCount = s.SettledTodayCount,
                SettlingTodayCount = s.SettlingTodayCount,
                InstructionsReceivedCount = s.InstructionsReceivedCount,
                DocsSentCount = s.DocsSentCount,
                DocsReturnedCount = s.DocsReturnedCount,
                DocsVerifiedCount = s.DocsVerifiedCount,
                ReadyToBookCount = s.ReadyToBookCount,
                SettlementCompleteCount = s.SettlementCompleteCount,
                Outstandings = s.Outstandings
            }).ToList();

            //----------------------------------------------------------------------------------------------

            //On Hold

            int holdCount = 0;

            try
            {
                holdCount = (int)resultList.Select(q => q.OnHoldCount).Single();
            }
            catch (NullReferenceException)
            {
                holdCount = 0;
            }

            //In Progress - Never actually gets used... so for now we'll just set it as 0 to save an SQL call. 
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "InProgress" };

            try
            {
                itm.FilterCount = (int)resultList.Select(q => q.InProgressCount).Single();
            }
            catch (NullReferenceException)
            {
                itm.FilterCount = 0;
            }

            retList.Add(itm);

            //On Hold as Item
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "OnHold" };
            itm.FilterCount = holdCount;
            retList.Add(itm);

            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "Settled" };
            //if (usr.FiltersRefreshedTime < DateTime.Now.AddMinutes(-30)) //if FiltersRefreshedTime 
            //{
            //Settled
            try
            {
                itm.FilterCount = (int)resultList.Select(q => q.SettledCount).Single();
            }
            catch (NullReferenceException)
            {
                itm.FilterCount = 0;
            }
            //} else
            //{
            //    itm.FilterCount = -1; //no need to get a new value
            //}
            retList.Add(itm);

            //Not Proceeding

            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "NotProceeding" };

            try
            {
                itm.FilterCount = (int)resultList.Select(q => q.NotProceedingCount).Single();
            }
            catch (NullReferenceException)
            {
                itm.FilterCount = 0;
            }
            retList.Add(itm);

            //Closed
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "Closed" };
            try
            {
                itm.FilterCount = (int)resultList.Select(q => q.ClosedCount).Single();
            }
            catch (NullReferenceException)
            {
                itm.FilterCount = 0;
            }
            retList.Add(itm);


            //About To Expire
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "AboutToExpire" };
            itm.FilterCount = 0;
            itm.FilterCount = (int)resultList.Select(q => q.AboutToExpireCount).Single();
            retList.Add(itm);

            //Settled Today
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "SettledToday" };
            itm.FilterCount = 0;
            itm.FilterCount = (int)resultList.Select(q => q.SettledTodayCount).Single();
            retList.Add(itm);

            //Settling Today
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "SettlingToday" };
            itm.FilterCount = 0;
            itm.FilterCount = (int)resultList.Select(q => q.SettlingTodayCount).Single();
            retList.Add(itm);


            //--------------------------------------------------------------------------------------------

            //Workflow Counts need to iterate through the matters
            /*
   var mwfRep = new MatterWFRepository(context);
            int cntInstrRec = 0, cntDocsSent = 0, 
                cntDocsReturned = 0, cntDocsVerified = 0, cntOutstandings = 0, cntSettlementBooked = 0;

            var allWFComps = mwfRep.GetMatterComponentsForUser(usr);*/
            //----------------------------------------------------------------------------------------------

            int cntInstrRec = 0, cntDocsSent = 0,
                cntDocsReturned = 0, cntDocsVerified = 0,
                cntReadyToBook = 0, cntOutstandings = 0,
                cntSettlementBooked = 0;

            cntInstrRec = (int)resultList.Select(q => q.InstructionsReceivedCount).Single();
            cntDocsSent = (int)resultList.Select(q => q.DocsSentCount).Single();
            cntDocsReturned = (int)resultList.Select(q => q.DocsReturnedCount).Single();
            cntDocsVerified = (int)resultList.Select(q => q.DocsVerifiedCount).Single();
            cntReadyToBook = (int)resultList.Select(q => q.ReadyToBookCount).Single();
            cntOutstandings = (int)resultList.Select(q => q.Outstandings).Single();
            cntSettlementBooked = (int)resultList.Select(q => q.SettlementCompleteCount).Single();


            //------------------------------------------------------------------------------------------------------

            //Instructions Received
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "InstrReceived", FilterCount = cntInstrRec };
            retList.Add(itm);

            //Docs Sent
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "DocsSent", FilterCount = cntDocsSent };
            retList.Add(itm);

            //Docs Returned
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "DocsReturned", FilterCount = cntDocsReturned };
            retList.Add(itm);

            //Docs Verified
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "DocsVerified", FilterCount = cntDocsVerified };
            retList.Add(itm);

            //Outstanding Requirements
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "ReadyToBook", FilterCount = cntReadyToBook };
            retList.Add(itm);

            //Outstanding Requirements
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "Outstandings", FilterCount = cntOutstandings };
            retList.Add(itm);

            //Settlements Booked
            itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "SettlementBooked", FilterCount = cntSettlementBooked };
            retList.Add(itm);

            //Settled Today
            //itm = new MCE.MatterWebQuickFilterCountView() { FilterType = "SettledToday", FilterCount = cntSettledToday };
            //retList.Add(itm);

            return retList;
        }

        public void SendToTestLender(int matterId)
        {
            var mt = context.Matters.FirstOrDefault(m => m.MatterId == matterId);
            int testLenderId = Int32.Parse(Slick_Domain.GlobalVars.GetGlobalTxtVar("TestLenderId"));
            mt.LenderId = testLenderId;
            mt.FileOwnerUserId = 1;
            mt.MatterStatusTypeId = (int)MatterStatusTypeEnum.Closed;
            mt.LenderRefNo = "TEST-" + mt.LenderRefNo;
            mt.MortMgrId = null;
            mt.BrokerId = null;
            mt.IsTestFile = true;
            mt.StopAutomatedEmails = true;

            context.MatterLinks.RemoveRange(context.MatterLinks.Where(m => m.MatterId == matterId || m.LinkedMatterId == matterId));

            context.MatterNotes.Add
            (
                new MatterNote()
                {
                    MatterId = matterId,
                    IsPublic = false,
                    NoteHeader = "Matter Removed",
                    NoteBody = $"Matter was moved to test lender at {DateTime.Now.TimeOfDay.ToString()} - {DateTime.Now.Date.ToLongDateString()} by {GlobalVars.CurrentUser.FullName}",
                    MatterNoteTypeId = (int)Slick_Domain.Enums.MatterNoteTypeEnum.StatusUpdate,
                    IsDeleted = false,
                    IsPinned = true,
                    HighPriority = true,
                    UpdatedByUserId = GlobalVars.CurrentUser.UserId,
                    UpdatedDate = DateTime.Now
                }
             );

        }

        #region ToDoList methods

        public IEnumerable<MatterCustomEntities.ToDoListItemView> GetToDoListItemsForMatter(int matterId, bool showCompleted = false)
        {
            return GetToDoListItemsForQuery(context.UserToDoListItems.Where(m => m.MatterId == matterId && (m.ToDoListItemStatusTypeId != (int)Enums.ToDoListStatusTypeEnum.Complete || showCompleted)));
        }


        public IEnumerable<MatterCustomEntities.ToDoListItemView> GetToDoListItemsForUser(int userId, bool showQAFyis = true, bool showCompleted = false, DateTime? cutoffDate = null, bool showSettled = false)
        {

            var stateId = context.Users.Select(x => new { x.UserId, x.StateId }).FirstOrDefault(u => u.UserId == userId).StateId;
            DateTime dayBefore = DateTime.Now.AddBusinessDays(1, stateId);
            DateTime dayOf = DateTime.Today.Date;
            DateTime dayAfter = DateTime.Now.AddBusinessDays(-1, stateId);

            var qry = context.UserToDoListItems.Where(u => u.ItemAssignedToUserId == userId &&
            (showSettled || !u.Matter.Settled) &&
            (
                (u.ToDoListItemReminderTypeId == (int)ToDoListItemReminderTypeEnum.SpecificTime &&
                (!cutoffDate.HasValue || (u.ReminderDate.HasValue && u.ReminderDate < cutoffDate.Value)))
            || (u.ToDoListItemReminderTypeId == (int)ToDoListItemReminderTypeEnum.DayBeforeSettlement &&
                (!cutoffDate.HasValue || (u.Matter.SettlementSchedule.SettlementDate == dayBefore )))
            || (u.ToDoListItemReminderTypeId == (int)ToDoListItemReminderTypeEnum.DayOfSettlement &&
                (!cutoffDate.HasValue || (u.Matter.SettlementSchedule.SettlementDate == dayOf)))
            || (u.ToDoListItemReminderTypeId == (int)ToDoListItemReminderTypeEnum.DayAfterSettlement &&
                (!cutoffDate.HasValue || (u.Matter.SettlementSchedule.SettlementDate == dayBefore)))
            )  
            &&
            (u.ToDoListItemStatusTypeId != (int)Enums.ToDoListStatusTypeEnum.Complete || showCompleted)
                && (showQAFyis || (u.CreatedAtMatterWFComponentId == null || u.DueDate.HasValue)));

            return GetToDoListItemsForQuery(qry);
        }

        public IEnumerable<MatterCustomEntities.ToDoListItemView> GetToDoListItemsForQuery(IQueryable<UserToDoListItem> qry)
        {
            var reasons = context.Reasons.Where(r => r.ReasonGroupTypeId == (int)Slick_Domain.Enums.ReasonGroupTypeEnum.RemindersDetail && r.Enabled).Select(r => new EntityCompacted{ Id = r.ReasonId, Details = r.ReasonTxt, RelatedId = r.LenderId, AdditionalFlag = r.Highlight
            }).ToList();

            var retItems = new List<MCE.ToDoListItemView>();
            var items = qry.Select(x => new
            {
                x.UserToDoListItemId,
                x.User.StateId,
                x.MatterId,
                x.Matter.MatterDescription,
                x.ItemDescription,
                x.ItemNotes,
                x.DueDate,
                SettlementDate = x.Matter.SettlementSchedule != null ? x.Matter.SettlementSchedule.SettlementDate : (DateTime?)null,
                x.ReminderDate,
                x.ItemAssignedToUserId,
                ItemAssignedToUserName = x.User.Username,
                x.ItemCreatedByUserId,
                ItemCreatedByUserName = x.User1.Username,
                x.ItemCreatedDate,
                x.ItemUpdatedByUserId,
                ItemUpdatedByUserName = x.User2.Username,
                x.ItemUpdatedDate,
                x.ToDoListItemStatusTypeId,
                x.ToDoListItemStatusType.ToDoListItemStatusTypeName,
                x.Matter.Lender.LenderName,
                x.CreatedAtMatterWFComponentId,
                x.CreatedFromWFIssueId,
                x.ToDoListItemReminderTypeId,
                x.ToDoListItemReminderType.ToDoListItemReminderTypeDescription,
                x.ReminderOffsetTime,
                LenderId = (int?)x.Matter.LenderId,
                Documents = x.UserToDoListItemDocuments.Select(d => new { d.ReasonDocumentId, d.Reason.ReasonTxt, d.Reason.Highlight }).ToList()

            }).ToList()
            .Select(l =>
            new MCE.ToDoListItemView
            (
                l.UserToDoListItemId,
                l.MatterId,
                l.ToDoListItemStatusTypeId,
                l.ToDoListItemStatusTypeName,
                l.ItemDescription,
                l.ItemNotes,
                l.DueDate,
                l.ItemAssignedToUserId,
                l.ItemAssignedToUserName,
                l.ItemCreatedByUserId,
                l.ItemCreatedByUserName,
                l.ItemCreatedDate,
                l.ItemUpdatedByUserId,
                l.ItemUpdatedByUserName,
                l.ItemUpdatedDate,
                l.MatterDescription,
                l.ReminderDate,
                l.LenderName,
                l.CreatedFromWFIssueId,
                l.CreatedAtMatterWFComponentId,
                l.ToDoListItemReminderTypeId,
                l.ToDoListItemReminderTypeDescription,
                l.SettlementDate,
                l.StateId,
                l.ReminderOffsetTime,
                l.Documents.Select(d => new MCE.ToDoListItemDetailView() { SelectedDetailReasonId = (int?)d.ReasonDocumentId, SelectedDetailReasonName = d.ReasonTxt, Highlight = d.Highlight }).ToList()
            )
            {
                PossibleReasonDetails = reasons.Where(r => (!l.MatterId.HasValue && !r.RelatedId.HasValue) || (l.MatterId.HasValue && (!r.RelatedId.HasValue || r.RelatedId == l.LenderId))).ToObservableCollection()
            }   
            );

            foreach(var item in items)
            {
                switch (item.ToDoListItemReminderTypeId)
                {
                    case (int)ToDoListItemReminderTypeEnum.DayBeforeSettlement:
                        if(item.SettlementDate != null && item.SettlementDate > new DateTime())
                        {
                            item.DueDate = item.SettlementDate.Value.AddBusinessDays(-1, item.StateId, context);
                        }
                        break;
                    case (int)ToDoListItemReminderTypeEnum.DayOfSettlement:
                        if (item.SettlementDate != null && item.SettlementDate > new DateTime())
                        {
                            item.DueDate = item.SettlementDate;
                        }
                        break;
                    case (int)ToDoListItemReminderTypeEnum.DayAfterSettlement:
                        if (item.SettlementDate != null && item.SettlementDate > new DateTime())
                        {
                            item.DueDate = item.SettlementDate.Value.AddBusinessDays(1, item.StateId, context);
                        }
                        break;
                }
                retItems.Add(item);
            }
            
            
            
            return retItems.OrderBy(x => x.ToDoListItemStatusTypeId).ThenBy(x => x.DueDate ?? DateTime.MaxValue).ToList();
        }

        #endregion
    }
}
