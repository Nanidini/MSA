using Slick_Domain.Common;
using Slick_Domain.Entities;
using Slick_Domain.Enums;
using Slick_Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using MCE = Slick_Domain.Entities.MatterCustomEntities;

namespace Slick_Domain.Services
{
    /// <summary>
    /// The Email Repository class.  Used for sending emails, generating their details based on data in the database to build their relevant template.
    /// </summary>
    public class EmailsRepository : SlickRepository
    {

        public EmailsRepository(SlickContext context) : base(context)
        {
        }
        public IEnumerable<MatterCustomEntities.MatterEmailLogView> GetEmailLogsForMatter(int matterId)
        {
            return context.MatterWFAutoEmailLogs.AsNoTracking().Where(m => m.MatterId == matterId)
                .Select(x => new
                {
                    x.MatterWFAutoEmailLogId,
                    x.MatterId,
                    x.LogNotes,
                    x.WFComponent.WFComponentName,
                    x.WFComponentId,
                    x.SentDate,
                    x.User.Fullname,
                    x.FromNoReply,
                    x.EmailSubject,
                    x.EmailTo,
                    x.EmailCC,
                    x.EmailBCC,
                    x.EmailBody,
                    x.SentByUserId
                })
                .ToList()
                .OrderByDescending(o => o.SentDate)
                .Select(x => new MatterCustomEntities.MatterEmailLogView()
                {
                    MatterWFAutoEmailLogId = x.MatterWFAutoEmailLogId,
                    MatterId = x.MatterId,
                    EmailTo = x.EmailTo,
                    EmailCC = x.EmailCC,
                    EmailBCC = x.EmailBCC,
                    LogNotes = x.LogNotes,
                    WFComponentName = x.WFComponentName,
                    SentDate = x.SentDate,
                    Subject = x.EmailSubject?.Trim(),
                    IsException = x.EmailSubject?.ToUpper()?.Contains("EXCEPTION") == true,
                    SentFromName = x.FromNoReply ? "No-Reply" : x.Fullname,
                    PlainTextBody = Slick_Domain.Common.CommonMethods.ConvertToPlainText(x.EmailBody)?.Trim(),
                    HtmlBody = x.EmailBody,
                }).ToList();
        }
        public void SendEmail(string toAddr, string ccAddr, string subject, string body)
        {
            SendEmail(toAddr, ccAddr, subject, body, true);
        }
        /// <summary>
        /// Sends an email using SMTP. Creates a MailMessage and consumes the parameters provided to create the email.
        /// </summary>
        /// <param name="toAddr">The to email address.</param>
        /// <param name="ccAddr">The cc email address.</param>
        /// <param name="subject">The subject of the email.</param>
        /// <param name="body">The body of the email.</param>
        /// <param name="isHTML">Flag for whether or not the email body is made of html or text</param>
        /// <param name="bannerImagePath">Include the MSA banner.</param>
        public void SendEmail(string toAddr, string ccAddr, string subject, string body, bool isHTML, string bannerImagePath = null, List<string> replyToAddresses = null, string attachedDocPath = null)
        {
            if (string.IsNullOrWhiteSpace(toAddr))
                return;

            SmtpClient smtpClient = new SmtpClient(GlobalVars.GetGlobalTxtVar("MailSMTPServer", context));
            if (GlobalVars.GetGlobalTxtVar("MailSMTPport", context) != null)
                smtpClient.Port = Convert.ToInt32(GlobalVars.GetGlobalTxtVar("MailSMTPport", context));

            if (GlobalVars.GetGlobalTxtVar("MailCredUser", context) == null)
                smtpClient.UseDefaultCredentials = true;
            else
                smtpClient.Credentials = new System.Net.NetworkCredential(GlobalVars.GetGlobalTxtVar("MailCredUser", context), GlobalVars.GetGlobalTxtVar("MailCredPass"));

            smtpClient.EnableSsl = true;
             
            MailMessage message = new MailMessage();

            message.From = new MailAddress(GlobalVars.GetGlobalTxtVar("MailCredUser", context));
            foreach (var address in toAddr.Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries))
                message.To.Add(address);
            if (!string.IsNullOrWhiteSpace(ccAddr))
                foreach (var address in ccAddr.Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries))
                    message.CC.Add(address);

            if(replyToAddresses != null)
            {
                foreach (var addr in replyToAddresses)
                {
                    message.ReplyToList.Add(addr);
                }
            }


            message.Body = body;
            message.Subject = subject;
            message.IsBodyHtml = isHTML;

            if (!string.IsNullOrEmpty(bannerImagePath))
            {
                if (System.IO.File.Exists(bannerImagePath))
                {
                    string embeddedBody = message.Body;
                    message.AlternateViews.Add(EmbedImage(ref embeddedBody, bannerImagePath));
                    message.Body = embeddedBody;
                }
            }

            if (!string.IsNullOrEmpty(attachedDocPath))
            {
                if (System.IO.File.Exists(attachedDocPath))
                {
                    message.Attachments.Add(new Attachment(attachedDocPath));
                }
            }


            smtpClient.Send(message);
            smtpClient.Dispose();
            smtpClient = null;
        }
        /// <summary>
        /// Embeds an image into an email message view.
        /// </summary>
        /// <remarks>
        /// body is passed by reference, and is modified with the content id of the image being added. 
        /// </remarks>
        /// <param name="body">The body of the image.</param>
        /// <param name="filePath">The file path of the image we are embedding.</param>
        /// <returns>An alternate view.</returns>
        private AlternateView EmbedImage(ref string body, String filePath)
        {
            LinkedResource res = new LinkedResource(filePath, MediaTypeNames.Image.Jpeg);
            res.ContentId = Guid.NewGuid().ToString();
            body = body.Replace("{{EmbeddedImageId}}", res.ContentId);
            AlternateView alternateView = AlternateView.CreateAlternateViewFromString(body, null, MediaTypeNames.Text.Html);
            alternateView.LinkedResources.Add(res);
            var i = 0;
            return alternateView;
        }


        public List<string> GetReplyAddressesForMatter(int matterId)
        {
            var addresses = new List<string>();

            var mtDetails = context.Matters.Where(m => m.MatterId == matterId).Select(m => new
            {
                m.MatterId,
                m.MatterGroupTypeId,
                m.LenderId,
                m.MortMgrId,
                FileownerEmail = m.User.Email,
                m.Settled
            }).FirstOrDefault();

            var allReplyAddresses = context.LenderReplyAddresses.Where(l => l.LenderId == mtDetails.LenderId && (!l.MortMgrId.HasValue || l.MortMgrId == mtDetails.MortMgrId)).Select(e => new
            {
                e.ReplyAddressTypeId,
                e.Email,
                e.MortMgrId,
            });

            if(allReplyAddresses.Any(m=>m.MortMgrId == mtDetails.MortMgrId))
            {
                allReplyAddresses = allReplyAddresses.Where(m => m.MortMgrId == mtDetails.MortMgrId);
            }

            if(mtDetails.MatterGroupTypeId != (int)Enums.MatterGroupTypeEnum.Discharge)
            {
                addresses.Add(mtDetails.FileownerEmail);

                var mwfRep = new Services.MatterWFRepository(context);

                if (!mwfRep.HasMilestoneCompleted(matterId, (int)WFComponentEnum.DocsReturned) && !mwfRep.HasMilestoneCompleted(matterId, (int)WFComponentEnum.CheckReturnedDocs))
                {
                    addresses = addresses.Concat(allReplyAddresses.Where(e => e.ReplyAddressTypeId == (int)ReplyAddressTypeEnum.NewLoanPreDocsReturned).Select(e => e.Email)).ToList();

                    if (!allReplyAddresses.Any())
                    {
                        addresses.Add(GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_NoReplyDocPrepContactAddress, context));
                    }
                }
                else if (mtDetails.Settled)
                {
                    addresses = addresses.Concat(allReplyAddresses.Where(e => e.ReplyAddressTypeId == (int)ReplyAddressTypeEnum.NewLoanPostSettlement).Select(e => e.Email)).ToList();

                    if (!allReplyAddresses.Any())
                    {
                        addresses.Add(GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_NoReplyPSTContactAddress, context));
                    }
                }
            }
            else
            {
                addresses = allReplyAddresses.Where(e => e.ReplyAddressTypeId == (int)ReplyAddressTypeEnum.DischargesGeneral).Select(e => e.Email).ToList();
            }


            return addresses;
        }


        #region MNP Warning Emails
        /// <summary>
        /// Gets the view of the MNP warning email templates as readonly
        /// </summary>
        public IEnumerable<EmailEntities.ReminderMessage> GetWFComponentEmailsView(int? lenderDocMasterId = null)
        {
            if (lenderDocMasterId.HasValue)
            {
                var emails = context.WFComponentEmails.Where(d=>d.WFComponentEmailLenderDocuments.Any(l=>l.LenderDocumentMasterId == lenderDocMasterId));

                return GetWFComponentEmails(emails);
            }
            else
            {
                var emails = context.WFComponentEmails.AsNoTracking();

                return GetWFComponentEmails(emails);
            }
        }

        public IEnumerable<EmailEntities.MNPWarningEmailView> GetMNPWarningEmailViews()
        {
            return GetMNPWarningEmailViews(context.MNPWarningEmailDefinitions.AsNoTracking());
        }
        
        public IEnumerable<EmailEntities.MNPWarningEmailView> GetMNPWarningEmailView(int definitionId)
        {
            return GetMNPWarningEmailViews(context.MNPWarningEmailDefinitions.AsNoTracking().Where(m=>m.MNPWarningEmailDefinitionId == definitionId));
        }
        public IEnumerable<EmailEntities.MNPWarningEmailView> GetMNPWarningEmailViews(IQueryable<MNPWarningEmailDefinition> qry)
        {
            return qry
                .Select(s =>
                new
                {
                    s.MNPWarningEmailDefinitionId,
                    s.MNPEmailTriggerTypeId,
                    s.MNPWarningEmailTriggerType.MNPWarningEmailTriggerName,
                    s.MatterTypeId,
                    s.MatterType.MatterTypeName,
                    s.StateId,
                    s.State.StateName,
                    s.LenderId,
                    s.Lender.LenderName,
                    s.MortMgrId,
                    s.MortMgr.MortMgrName,
                    SettlementTypeNames = s.MNPWarningEmailDefinitionSettlementTypes.OrderBy(x => x.MatterTypeId).Select(x => x.MatterType.MatterTypeName),
                    DischargeTypeNames = s.MNPWarningEmailDefinitionDischargeTypes.OrderBy(x => x.DischargeTypeId).Select(x => x.DischargeType.DischargeTypeName),
                    FundingChannelTypeNames = s.MNPWarningEmailDefinitionFundingChannelTypes.OrderBy(x => x.FundingChannelTypeId).Select(x => x.FundingChannelType.FundingChannelTypeName),
                    SelfActingType = s.SelfActingTypeId.HasValue ? ((SelfActingTypeEnum)s.SelfActingTypeId).ToString() : "- Either -",
                    s.Enabled,
                    s.UpdatedByUserId,
                    s.User.Username,
                    s.UpdatedDate,
                    MortMgrTypeName = s.MortMgrTypeId.HasValue ? s.MortMgrType.MortMgrTypeName : "- None Selected -",
                    s.FacilityTypeId,
                    FacilityTypeName = s.FacilityTypeId.HasValue ? s.FacilityType.FacilityTypeName : "- None Specified - ",
                    WarningCount = s.MNPWarningEmailDefinitionBodies.Count()
                })
                .OrderByDescending(o => o.Enabled).ThenBy(o => o.LenderName).ThenBy(o=>o.MNPEmailTriggerTypeId)
                .ToList()
                .Select(s2 => new EmailEntities.MNPWarningEmailView
                {
                    Id = s2.MNPWarningEmailDefinitionId,
                    MNPTriggerTypeName = s2.MNPWarningEmailTriggerName,
                    MNPTriggerTypeId = s2.MNPEmailTriggerTypeId,
                    LenderId = s2.LenderId,
                    LenderName = s2.LenderName ?? "-- Any Lender --",
                    MatterTypeName = s2.MatterTypeName ?? "-- Any Matter Type --",
                    MatterTypeId = s2.MatterTypeId,
                    MortMgrName = s2.MortMgrName ?? "-- Any Mort Mgr --",
                    SettlementTypeNames = s2.SettlementTypeNames.Any() ? String.Join(", ", s2.SettlementTypeNames) : "-- Any Settlement Type --",
                    DischargeTypeNames = s2.DischargeTypeNames.Any() ? String.Join(", ", s2.DischargeTypeNames) : "-- Any Discharge Type --",
                    FundingChannelTypeNames = s2.FundingChannelTypeNames.Any() ? String.Join(", ", s2.FundingChannelTypeNames) : "-- Any Channel --",
                    MortMgrTypeName = s2.MortMgrTypeName,
                    SelfActingType = s2.SelfActingType,
                    MortMgrId = s2.MortMgrId,
                    StateId = s2.StateId,
                    StateName = s2.StateName ?? "-- Any --",
                    IsEnabled = s2.Enabled,
                    UpdatedBy = s2.Username,
                    UpdatedById = s2.UpdatedByUserId,
                    UpdatedDate = s2.UpdatedDate,
                    FacilityTypeId = s2.FacilityTypeId,
                    FacilityTypeName = s2.FacilityTypeName,
                    WarningCount = s2.WarningCount
                }
                ).ToList();
        }
        #endregion
        /// <summary>
        /// Gets the Emails to send based on the <see cref="Slick_Domain.Enums.WFComponentEnum"/> cast as an integer.
        /// </summary>
        /// <remarks>
        /// <para>If there are no items returned, we return null.</para>
        /// <para>If there are no best matched emails we return null</para>
        /// </remarks>
        /// <param name="matter">The matter to get the emails based on.</param>
        /// <param name="componentId">The <see cref="Slick_Domain.Enums.WFComponentEnum"/>.</param>
        /// <param name="triggerType">The <see cref="Slick_Domain.Enums.MilestoneEmailTriggerTypeEnum"/>.</param>
        /// <returns>An enumerable collection of reminder messages to be sent.</returns>
        public IEnumerable<EmailEntities.ReminderMessage> GetEmailsToSendForComponentId(MCE.MatterViewCompact matter, int componentId, int triggerType)
        {
            var items = new List<GeneralCustomEntities.BestMatchForCriteria>();
            if (matter.MatterGroupTypeId == (int)MatterGroupTypeEnum.NewLoan || matter.MatterGroupTypeId == (int)MatterGroupTypeEnum.Consent)
            {
                var addnlMatterDetails = context.Matters.Select(x => new { x.MatterId, x.LoanTypeId, x.FundingChannelTypeId }).FirstOrDefault(m=>m.MatterId == matter.MatterId);
                int? matterFundingChannelTypeId = addnlMatterDetails.FundingChannelTypeId;
                int? loanTypeId = addnlMatterDetails.LoanTypeId;

                int? mortMgrTypeId = context.Matters.FirstOrDefault(m => m.MatterId == matter.MatterId).MortMgr?.MortMgrTypeId;
                //search for exact match based on all mattertypes for email matching all matter types for matter first, then look for a generic non-matter type specific email if none found for this combination.
                List<int> matterTypeIds = context.MatterMatterTypes.Where(m => m.MatterId == matter.MatterId).Select(x => x.MatterTypeId).Distinct().ToList();
                foreach (var wce in context.WFComponentEmails.AsNoTracking().Where(x => 
                x.WFComponentId == componentId && x.MilestoneEmailTriggerTypeId == triggerType && (x.MatterTypeId == matter.MatterGroupTypeId || !x.MatterTypeId.HasValue)
                && (x.LenderId == null || x.LenderId == matter.LenderId) && (x.MortMgrId == null || x.MortMgrId == matter.MortMgrId)
                && (!x.MortMgrTypeId.HasValue || x.MortMgrTypeId == mortMgrTypeId) 
                && ((!matterFundingChannelTypeId.HasValue && !x.WFComponentEmailFundingChannelTypes.Any()) || (matterFundingChannelTypeId.HasValue && (!x.WFComponentEmailFundingChannelTypes.Any() || x.WFComponentEmailFundingChannelTypes.Any(t=>t.FundingChannelTypeId == matterFundingChannelTypeId.Value))))

                && ((!loanTypeId.HasValue && !x.WFComponentEmailLoanTypes.Any()) || (loanTypeId.HasValue && (!x.WFComponentEmailLoanTypes.Any() || x.WFComponentEmailLoanTypes.Any(t => t.LoanTypeId == loanTypeId.Value))))


                && x.Enabled))
                {
                    var emailTypeIds = wce.WFComponentEmailSettlementTypes.Select(x => x.MatterTypeId).ToList();

                    bool match = matterTypeIds.All(emailTypeIds.Contains) && matterTypeIds.Count == emailTypeIds.Count;

                    if (!match && emailTypeIds.Any()) continue;

                    if (wce.IfBorrowerEmailOnly)
                    {
                        if(!context.MatterParties.Any(x=>x.MatterId == matter.MatterId && !string.IsNullOrEmpty(x.Email)))
                        {
                            continue;
                        }
                    }

                    items.Add(new GeneralCustomEntities.BestMatchForCriteria
                    {
                        ID = wce.WFComponentEmailId,
                        LenderId = wce.LenderId,
                        MatterTypeId = wce.MatterTypeId,
                        MortMgrId = wce.MortMgrId,
                        StateId = wce.StateId,
                        isSpecific = emailTypeIds.Any(),
                        hasSpecificMortMgrType = wce.MortMgrTypeId.HasValue,
                        hasSpecificLoanType = wce.WFComponentEmailLoanTypes.Any()
                    });
                }
            }
            else if(matter.MatterGroupTypeId == (int)MatterGroupTypeEnum.Discharge)
            {
                var dischDetails = 
                    context.Matters.Where(m => m.MatterId == matter.MatterId)
                    .Select(x => new
                    {
                        DischargeTypeId = x.MatterDischarge != null ? (int?)x.MatterDischarge.DischargeTypeId : (int?)null,
                        IsSelfActing = x.MatterDischarge != null ? (bool?) x.MatterDischarge.IsSelfActing : (bool?)null,
                        x.FundingChannelTypeId
                    })
                    .FirstOrDefault();
                
                if (dischDetails != null && dischDetails.DischargeTypeId.HasValue && dischDetails.IsSelfActing.HasValue)
                {
                    foreach (var wce in context.WFComponentEmails.AsNoTracking().Where(x => x.WFComponentId == componentId && x.MilestoneEmailTriggerTypeId == triggerType
                    && x.MatterTypeId == (int)Enums.MatterGroupTypeEnum.Discharge && (!x.WFComponentEmailFundingChannelTypes.Any() || (dischDetails.FundingChannelTypeId.HasValue && x.WFComponentEmailFundingChannelTypes.Any(f=>f.FundingChannelTypeId == dischDetails.FundingChannelTypeId)))
                    && (x.LenderId == null || x.LenderId == matter.LenderId) && (x.MortMgrId == null || x.MortMgrId == matter.MortMgrId)
                    && (x.WFComponentEmailDischargeTypes.Any(d=>d.DischargeTypeId == dischDetails.DischargeTypeId) || x.WFComponentEmailDischargeTypes.Count() == 0) && x.Enabled))
                    {
                        if (wce.SelfActingTypeId.HasValue)
                        {
                            if(dischDetails.IsSelfActing.Value == true && wce.SelfActingTypeId == (int)Enums.SelfActingTypeEnum.NonSelfActing)
                            {
                                continue;
                            }
                            if (!dischDetails.IsSelfActing.Value == true && wce.SelfActingTypeId == (int)Enums.SelfActingTypeEnum.SelfActing)
                            {
                                continue;
                            }
                        }

                        if (wce.IfBorrowerEmailOnly)
                        {
                            if (!context.MatterParties.Any(x => (x.PartyTypeId == (int)MatterPartyTypeEnum.Borrower || x.PartyTypeId == (int)MatterPartyTypeEnum.Mortgagor) &&  x.MatterId == matter.MatterId && !string.IsNullOrEmpty(x.Email)))
                            {
                                continue;
                            }
                        }

                        items.Add(new GeneralCustomEntities.BestMatchForCriteria
                        {
                            ID = wce.WFComponentEmailId,
                            LenderId = wce.LenderId,
                            MatterTypeId = wce.MatterTypeId,
                            MortMgrId = wce.MortMgrId,
                            StateId = wce.StateId,
                            isSpecific = wce.SelfActingTypeId.HasValue,
                            hasSpecificMortMgrType = false,
                            hasSpecificLoanType = false
                        });
                    }

                    //if (!items.Any())
                    //{
                    //    var possibleEmails = context.WFComponentEmails.AsNoTracking().Where(x => x.WFComponentId == componentId && x.MilestoneEmailTriggerTypeId == triggerType
                    //    && x.Enabled).ToList();
                    //    foreach (var wce in possibleEmails)
                    //    //&& !x.WFComponentEmailDischargeTypes.Any() 
                    //    {
                    //        if (wce.SelfActingTypeId.HasValue)
                    //        {
                    //            if (dischDetails.IsSelfActing.Value == true && wce.SelfActingTypeId == (int)Enums.SelfActingTypeEnum.NonSelfActing)
                    //            {
                    //                continue;
                    //            }
                    //            if (!dischDetails.IsSelfActing.Value == true && wce.SelfActingTypeId == (int)Enums.SelfActingTypeEnum.SelfActing)
                    //            {
                    //                continue;
                    //            }
                    //        }
                    //        items.Add(new GeneralCustomEntities.BestMatchForCriteria
                    //        {
                    //            ID = wce.WFComponentEmailId,
                    //            LenderId = wce.LenderId,
                    //            MatterTypeId = wce.MatterTypeId,
                    //            MortMgrId = wce.MortMgrId,
                    //            StateId = wce.StateId
                    //        });
                    //    }
                    //}
                }
                else
                {
                    foreach (var wce in context.WFComponentEmails.AsNoTracking().Where(x => x.WFComponentId == componentId && x.MilestoneEmailTriggerTypeId == triggerType
                        && x.WFComponentEmailDischargeTypes.Count() == 0 && !x.SelfActingTypeId.HasValue && x.Enabled))
                    {

                        items.Add(new GeneralCustomEntities.BestMatchForCriteria
                        {
                            ID = wce.WFComponentEmailId,
                            LenderId = wce.LenderId,
                            MatterTypeId = wce.MatterTypeId,
                            MortMgrId = wce.MortMgrId,
                            StateId = wce.StateId,
                            isSpecific = false,
                            hasSpecificMortMgrType = false,
                            hasSpecificLoanType = false
                        });
                    }
                }
            }



            if (items == null || !items.Any()) return null;
            var bestMatchedEmails = CommonMethods.GetBestRankedValueListFromSelectedQuery(matter.MatterGroupTypeId, null, matter.StateId, matter.LenderId, matter.MortMgrId, items);

            if (bestMatchedEmails == null) return null;


            if (bestMatchedEmails.Any(x => x.hasSpecificMortMgrType))
            {
                bestMatchedEmails = bestMatchedEmails.Where(x => x.hasSpecificMortMgrType).ToList();
            }

            if (bestMatchedEmails.Any(x => x.hasSpecificLoanType))
            {
                bestMatchedEmails = bestMatchedEmails.Where(x => x.hasSpecificLoanType).ToList();
            }

            var bestMatchedEmailIds = bestMatchedEmails?.Select(x => x.ID);

            return
            GetWFComponentEmails(from c in context.WFComponentEmails
                                 join x in bestMatchedEmailIds on c.WFComponentEmailId equals x
                                 select c);
        }
        /// <summary>
        /// Gets the Reminder messages for a specific <see cref="Slick_Domain.Enums.WFComponentEnum"/> 
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public EmailEntities.ReminderMessage GetWFComponentEmailsView(int id)
        {
            var emails = context.WFComponentEmails.AsNoTracking().Where(x => x.WFComponentEmailId == id);
            return GetWFComponentEmails(emails).FirstOrDefault();
        }
        /// <summary>
        /// Gets the EmailPlaceholderModel for the matter based on the workflow component.
        /// </summary>
        /// <param name="matterId">The <see cref="Matter.MatterId"/>.</param>
        /// <param name="matterWFComponentId">The <see cref="MatterWFComponent.MatterWFComponentId"/>.</param>
        /// <param name="isSMS">A flag for whether or not the email is to be sent to macquarie and converted to an SMS</param>
        /// <param name="brokerSms">A flag for broker SMS.</param>
        /// <returns>The Email Model to be manipulated.</returns>
        public EmailEntities.EmailPlaceHolderModel BuildEmailPlaceHolderModelWithMatterWFComponent(int matterId, int matterWFComponentId, bool isSMS = false, bool brokerSms = false)
        {
            EmailEntities.EmailPlaceHolderModel emailModel = BuildEmailPlaceHolderModel(matterId, isSMS, brokerSms);

            if (emailModel != null)
            {
                var component = context.MatterWFComponents.Where(x => x.MatterWFComponentId == matterWFComponentId)
                    .Select(x => new { x.WFComponentId, x.WFComponent.WFComponentName })
                    .FirstOrDefault();

                if (component != null)
                {
                    emailModel.Milestone = component.WFComponentName;
                    emailModel.WFComponentId = component.WFComponentId;
                }
            }

            return emailModel;
        }
        /// <summary>
        /// Get the other email address 
        /// </summary>
        /// <param name="matterId"></param>
        /// <returns></returns>
        public string GetOtherEmailToAddress(int matterId)
        {
            return context.MatterOtherParties.FirstOrDefault(x => x.MatterId == matterId)?.Email;
        }

        public EmailEntities.EmailPlaceHolderModel BuildEmailPlaceHolderModelWithWFComponent(int matterId, int componentId, bool isSMS = false)
        {
            EmailEntities.EmailPlaceHolderModel emailModel = BuildEmailPlaceHolderModel(matterId, isSMS);

            if (emailModel != null)
            {
                emailModel.Milestone = context.WFComponents.FirstOrDefault(x => x.WFComponentId == componentId)?.WFComponentName;
                emailModel.WFComponentId = componentId;
            }

            return emailModel;
        }

        public List<EmailEntities.EmailSendProcDocsModel> BuildSendProcDocsEmailModel(int matterId)
        {
            return context.MatterSendProcessedDocs.Where(x => x.MatterId == matterId)
                .Select(x => new EmailEntities.EmailSendProcDocsModel
                {
                    DeliveryMethodId = x.DocumentDeliveryTypeId,
                    DeliveryMethod = x.DocumentDeliveryType.DocumentDeliveryTypeDesc,
                    EmailDocsSentTo = x.EmailDocsSentTo,
                    NameDocsSentTo = x.NameDocsSentTo,
                    DocsSentToLabel = x.DocsSentToLabel,
                    ExpressPostReceiveTracking = x.ExpressPostReceiveTracking,
                    ExpressPostSentTracking = x.ExpressPostSentTracking
                })
                .ToList();
        }

        public EmailEntities.EmailPlaceHolderModel BuildEmailPlaceHolderModel(int matterId, bool isSMS = false, bool brokerSms = false, bool convertWordToPDF = false)
        {
            var emailModel =
                (from m in context.Matters.AsNoTracking()
                 where m.MatterId == matterId
                 select new
                 {
                     m.MatterId,
                     m.LenderId,
                     m.MortMgrId,
                     m.MatterGroupTypeId,
                     m.StateId,
                     m.MatterType.MatterTypeName,
                     WorkTypes = m.MatterMatterTypes.OrderBy(o => o.MatterTypeId).Select(w => w.MatterType.MatterTypeName),
                     m.LenderRefNo,
                     m.SecondaryRefNo,
                     DischargeTypeName = m.MatterDischarge.DischargeType.DischargeTypeName,
                     fileOwnerFirstName = m.User.Firstname,
                     fileOwnerLastName = m.User.Lastname,
                     fileOwnerInitials = m.User.DisplayInitials,
                     fileOwnerEmail = m.User.Email,
                     fileOwnerMobile = m.User.Mobile,
                     fileOwnerPhone = m.User.Phone,
                     m.MatterDescription,
                     m.Lender.LenderName,
                     lenderEmail = m.PrimaryContact1.Email ?? m.Lender.PrimaryContact.Email,
                     lenderMobile = m.PrimaryContact1.Mobile ?? m.Lender.PrimaryContact.Mobile,
                     lenderAddnlContacts = m.MatterAdditionalContacts.Where(x => x.AdditionalContactTypeId == (int)AdditionalContactTypeEnum.Lender).Select(l => new { l.PrimaryContact.Email, l.PrimaryContact.Mobile }),
                     brokerLastname = m.Broker.PrimaryContact.Lastname,
                     brokerFirstname = m.Broker.PrimaryContact.Firstname,
                     brokerEmail = m.Broker.PrimaryContact.Email,
                     brokerMobile = m.Broker.PrimaryContact.Mobile,
                     relationshipManagerDetails = m.Broker != null ?
                                    m.Broker.BrokerRelationshipManagers.Select(x =>
                                            new MatterCustomEntities.RelationshipManagerLimitedView()
                                            {
                                                RelationshipManagerId = x.RelationshipManagerId,
                                                Firstname = x.RelationshipManager.PrimaryContact.Firstname,
                                                Lastname = x.RelationshipManager.PrimaryContact.Lastname,
                                                Email = x.RelationshipManager.PrimaryContact.Email,
                                                Mobile = x.RelationshipManager.PrimaryContact.Mobile,
                                                Phone = x.RelationshipManager.PrimaryContact.Phone,
                                                Fax = x.RelationshipManager.PrimaryContact.Fax
                                            }).FirstOrDefault() : (MatterCustomEntities.RelationshipManagerLimitedView)null,
                     otherPartyEmails = m.MatterOtherParties.Where(e => e.Email != null).Select(e => e.Email),
                     otherPartyMobiles = m.MatterOtherParties.Select(e => e.Phone).Where(e => e.Substring(0, 2) == "04"),


                     ManualAnticipatedDate = m.AnticipatedSettlementDate.HasValue ? m.AnticipatedSettlementDate.Value : m.MatterWFComponents.OrderByDescending(o => o.UpdatedDate).SelectMany(o => o.MatterWFOutstandingReqs.Where(d => d.ExpectedSettlementDate.HasValue).Select(s => s.ExpectedSettlementDate)).FirstOrDefault(),

                     OtherSideReference = m.OtherPartyReference,

                     pexaNominatedDate = ((DateTime?)m.SettlementSchedule.SettlementDate).HasValue ? (DateTime?)m.SettlementSchedule.SettlementDate :
                                            (DateTime?)m.MatterPexaDetails.FirstOrDefault(p => p.MatterId == matterId).NominatedSettlementDate,
                     matterTypeIds = m.MatterMatterTypes.Select(t => t.MatterTypeId).ToList(),
                     m.MortMgr.MortMgrName,
                     mortMgrEmail = m.PrimaryContact2.Email ?? m.MortMgr.PrimaryContact.Email,
                     mortMgrMobile = m.PrimaryContact2.Mobile ?? m.MortMgr.PrimaryContact.Mobile,
                     LoanAmt = m.MatterLoanAccounts.Any() ? m.MatterLoanAccounts.Sum(x => x.LoanAmount) : 0,
                     m.CCNotifications,
                     SettlementDate = (DateTime?)m.SettlementSchedule.SettlementDate,
                     SettlementTime = (TimeSpan?)m.SettlementSchedule.SettlementTime,

                     SettlementVenue = m.SettlementSchedule.SettlementScheduleVenues.Any() ? m.SettlementSchedule.SettlementScheduleVenues.FirstOrDefault().SettlementVenue.VenueName : null,
                     ChequeCollectionBranch = m.SettlementSchedule.SettlementScheduleVenues.Any() ? m.SettlementSchedule.SettlementScheduleVenues.FirstOrDefault().SettlementAgent != null ? "<b>Cheque Collection Branch: </b>" + m.SettlementSchedule.SettlementScheduleVenues.FirstOrDefault().SettlementAgent.ChequeCollectionBranch : null : null,
                     LedgerItems = m.MatterLedgerItems.Where
                         (li => li.PayableByTypeId == (int)Enums.PayableTypeEnum.Borrower && li.PayableToTypeId == (int)Enums.PayableTypeEnum.MSA)
                            .Select(l => new { PayableTo = l.PayableToName, l.Amount, l.Description }).ToList(),
                     NonLedgerItems = m.MatterLedgerItems.Where
                         (li => li.PayableByTypeId == (int)Enums.PayableTypeEnum.Borrower && li.PayableToTypeId != (int)Enums.PayableTypeEnum.MSA)
                         .Select(l => new { PayableTo = l.PayableToName, l.Amount, l.Description }).ToList(),
                     currUserEmail = GlobalVars.CurrentUser.Email,
                     currUserMobile = GlobalVars.CurrentUser.Mobile,
                     DischIndicativePayouts = m.MatterIndicativePayoutFigures.Select(x => new MatterCustomEntities.MatterIndicativePayoutView() { LoanAccountNo = x.LoanAccNo, IndicativePayoutAmount = x.IndicativePayoutAmount ?? 0M }),
                     DischPayouts = m.MatterDischargePayouts.Select(x => new MatterCustomEntities.MatterIndicativePayoutView() { LoanAccountNo = x.AccountNo, IndicativePayoutAmount = x.PayoutAmount }),
                     BorrowerDetails = m.MatterParties.Where(mp => mp.MatterId == matterId && mp.PartyTypeId == (int)Enums.MatterPartyTypeEnum.Borrower)
                            .Select(p => new { p.Firstname, p.DisplayFirstname, p.Lastname, p.Email, p.Mobile, p.CompanyName, p.IsIndividual }).ToList(),
                     Parties = m.MatterParties.Where(mp => mp.MatterId == matterId && mp.PartyTypeId == (int)Enums.MatterPartyTypeEnum.Borrower)
                            .Select(p => new { Firstname = p.IsIndividual ? p.Firstname : p.CompanyName, Lastname = p.IsIndividual ? p.Lastname : null }).ToList(),
                     Securities = m.MatterSecurities.Where(ms => !ms.Deleted && ms.MatterId == matterId)
                            .Select(s => new { s.MatterSecurityId, s.PostCode, s.State.StateName, s.Suburb, s.StreetAddress, s.SettlementTypeId, s.SettlementType.SettlementTypeName, s.MatterType.MatterTypeName }).ToList(),
                     TitleRefs = m.MatterSecurities.Where(x => !x.Deleted && x.MatterId == matterId).SelectMany(tr => tr.MatterSecurityTitleRefs.Select(y => new { y.TitleReference, y.MatterSecurityId })).ToList(),
                     DisclosureDate = m.DisclosureDate,
                     LEXPDate = (DateTime?)m.LoanExpiryDate, //m.LEXPDate,
                     FastRefiDetails =  m.MatterFastRefiDetails.Select(f => new
                     {
                         f.MatterFastRefiDetailId,
                         f.BalanceExpiryDate,
                         f.EFT_AccountName,
                         f.EFT_AccountNo,
                         f.EFT_BSB,
                     }).ToList(),
                     FHOGApplies = m.FHOGApplies,
                 }).FirstOrDefault();

            List<string> settlementNotes = new List<string>();
            if (emailModel.SettlementDate.HasValue)
            {
                var ss = context.Matters.FirstOrDefault(m => m.MatterId == matterId).SettlementSchedule;
                if(ss != null)
                {
                    settlementNotes = ss.SettlementScheduleVenues.Select(x => x.SettlementVenueNotes).Where(x=>!string.IsNullOrEmpty(x)).ToList();
                }
            }

            string partyFirstnamesStr = "";
            List<string> partyFirstnames = new List<string>();
            if (emailModel.BorrowerDetails != null && emailModel.BorrowerDetails.Any())
            {

                partyFirstnames = emailModel.BorrowerDetails.Where(i => i.IsIndividual && !string.IsNullOrEmpty(i.Firstname)).Select(s => s.DisplayFirstname ?? CleanName(s.Firstname)).Distinct().ToList();
                int partyIndex = 0;
                bool append = partyFirstnames.Count > 1;
                foreach (var name in partyFirstnames)
                {
                    string toAdd = name;
                    if (append)
                    {
                        if(partyIndex < partyFirstnames.Count - 1)
                        {
                            if (partyIndex == partyFirstnames.Count - 2)
                            {
                                toAdd += " & ";
                            }
                            else
                            {
                                toAdd += ", ";
                            }
                        }
                    }
                    partyFirstnamesStr += toAdd;
                    partyIndex++;
                }
            }

            string securitySuburbs = "";
            if (emailModel.Securities != null && emailModel.Securities.Any(s=>!string.IsNullOrEmpty(s.Suburb)))
            {
                var uniqueSuburbs = emailModel.Securities.Where(s=>!string.IsNullOrEmpty(s.Suburb)).Select(s => s.Suburb?.Trim()).GroupBy(s => s.ToUpper()).Select(s => s.First()).ToList();
                bool append = uniqueSuburbs.Count() > 1;
                for(int i = 0; i < uniqueSuburbs.Count(); i++)
                {
                    securitySuburbs += uniqueSuburbs[i] + (append ? (i == uniqueSuburbs.Count() ? " & " : ", ") : "");
                }
            }

            if (emailModel == null) return null;

            var pRep = new PEXARepository(context);

            int lenderId = emailModel.LenderId;
            int? mortMgrId = emailModel.MortMgrId;
            int matterGroupTypeId = emailModel.MatterGroupTypeId;
            string lenderRefToUse = emailModel.LenderRefNo;
            string secondaryRefToUse = emailModel.SecondaryRefNo; 

            if (matterGroupTypeId == (int)MatterGroupTypeEnum.NewLoan && lenderId == 139 && mortMgrId != 49)
            {
                lenderRefToUse = emailModel.SecondaryRefNo;
                secondaryRefToUse = emailModel.LenderRefNo;
            }

            string lenderEmail = emailModel.lenderEmail;
            if(matterGroupTypeId == (int)MatterGroupTypeEnum.NewLoan)
            {

                
                var emails = context.LenderStateEmails.Where(o => o.LenderId == lenderId && o.StateId == emailModel.StateId && o.Enabled).Select(e => new { e.EmailAddress, e.Overrides, e.Enabled }).ToList();
                if (emails.Any(o => o.Overrides))
                {
                    lenderEmail = null;
                    lenderEmail = String.Join("; ", emails.Where(e => e.Overrides).Select(a => a.EmailAddress).Distinct());
                }
                else if (emails.Any())
                {
                    lenderEmail = (lenderEmail != null ? lenderEmail + ";" : "" ) + String.Join("; ", emails.Select(a => a.EmailAddress).Distinct());
                }
            }


            var combinedLedgerItems = emailModel.LedgerItems.Select(e => new EmailEntities.EmailLedgerItem()
            {
                Amount = e.Amount,
                AmountString = e.Amount.ToString("c"),
                Description = e.Description,
                PayableTo = e.PayableTo,
                IsLedgerItem = true
            }).ToList().Union(emailModel.NonLedgerItems.Select(e => new EmailEntities.EmailLedgerItem()
            {
                Amount = e.Amount,
                AmountString = e.Amount.ToString("c"),
                Description = e.Description,
                PayableTo = e.PayableTo,
                IsLedgerItem = false
            })).ToList();


            string partyFullNames = "";
            List<string> formattedNames = emailModel.BorrowerDetails.Select(p => (p.Firstname?.Trim() + " " + p.Lastname?.Trim())?.Trim()).ToList();
            partyFullNames = formattedNames.FirstOrDefault();
            if(formattedNames.Count() > 1)
            {
                partyFullNames = "";
                int index = 0;
                foreach (var party in partyFullNames)
                {
                    string separator = index == partyFullNames.Count() - 1 ? "" : index == partyFullNames.Count () - 2 ? " & " : ", ";
                    partyFullNames += party + separator;
                    index++; 
                }
            }

            if (string.IsNullOrEmpty(partyFullNames))
            {
                partyFullNames = emailModel.MatterDescription;
            }

            string fastRefiDetails = "";
            if(emailModel.FastRefiDetails?.Any() == true)
            {
                string fastRefiTypeLabel = emailModel.matterTypeIds.Any(m => m == (int)MatterTypeEnum.RapidRefinance) ? "Rapid Refinance" : "FAST Refinance";
                fastRefiDetails = $"<p><b><u>{fastRefiTypeLabel}</u></b></p>";
                foreach(var detail in emailModel.FastRefiDetails)
                {
                    fastRefiDetails += $"Account {detail.EFT_AccountNo} / {detail.EFT_AccountName} - {(detail.BalanceExpiryDate.HasValue ? detail.BalanceExpiryDate.Value.ToString("dd/MM/yyyy") : "<b>Please provide a current balance screenshot ensuring the date is included</b>")} </br>";
                }
            }


            var ledgerItemsStr = EmailsService.BuildDischargePayoutSection(BuildLedgerItemsForEmail(combinedLedgerItems, emailModel.LenderId));
            DateTime emptyDate = new DateTime();
            var model = new EmailEntities.EmailPlaceHolderModel
            {
                MatterId = emailModel.MatterId.ToString(),
                FileOwner = EntityHelper.GetFullName(emailModel.fileOwnerLastName, emailModel.fileOwnerFirstName),
                FileOwnerInitials = emailModel.fileOwnerInitials,
                FileOwnerPhone = emailModel.fileOwnerPhone ?? emailModel.fileOwnerMobile,
                FileOwnerEmail = emailModel.fileOwnerEmail,
                LenderRefNo = lenderRefToUse,
                SecondaryRefNo = secondaryRefToUse,
                MatterType = emailModel.MatterTypeName,
                MatterDescription = emailModel.MatterDescription,
                OtherSideReference = emailModel.OtherSideReference,
                WorkTypes = BuildWorkTypeString(emailModel.WorkTypes, emailModel.MatterTypeName),
                DischargeType = emailModel.DischargeTypeName,
                MortMgrName = emailModel.MortMgrName,
                LenderName = emailModel.LenderName,
                BrokerName = EntityHelper.GetFullName(emailModel.brokerLastname, emailModel.brokerFirstname),
                LoanAmount = emailModel.LoanAmt == 0 ? null : emailModel.LoanAmt.ToString("c"),
                CurrentUser = EntityHelper.GetFullName(GlobalVars.CurrentUser.LastName, GlobalVars.CurrentUser.FirstName),
                TodaysDate = DateTime.Now.ToShortDateString(),
                SettlementDate = emailModel.SettlementDate.HasValue ? emailModel.SettlementDate.Value.ToString("dd-MMM-yyyy") : null,
                SettlementTime = emailModel.SettlementTime.HasValue ? DateTime.Today.Add(emailModel.SettlementTime.Value).ToString("h:mm tt") : null,
                SettlementVenue = emailModel.SettlementVenue,
                ChequeCollectionBranch = emailModel.ChequeCollectionBranch,
                SettlementNotes = settlementNotes.Any() ? "Settlement Notes – " + String.Join(",",settlementNotes) : null,
                SettlementType = "PEXA",
                Parties = emailModel.MatterDescription,
                PartyFirstNames = partyFirstnames,
                PartyFirstNamesStr = partyFirstnamesStr,
                FastRefiDetails = fastRefiDetails,
                DisclosureDateStr = emailModel.DisclosureDate.HasValue ? emailModel.DisclosureDate.Value.ToString("dd-MMM-yyyy") : "",
                LEXPDateStr = emailModel.LEXPDate.HasValue ? emailModel.LEXPDate.Value.ToString("dd-MMM-yyyy") : "",

                //PexaNominatedDate = emailModel.pexaNominatedDate.HasValue ? "The <b><u>anticipated</u></b> settlement date in PEXA is <u>" + emailModel.pexaNominatedDate.Value.ToString("dddd, dd MMMM yyyy") + ".</u> and subject to change without notice." : null,
                PexaNominatedDate = emailModel.pexaNominatedDate.HasValue ? "The current nominated date in PEXA is <u>" + emailModel.pexaNominatedDate.Value.ToString("dddd, dd MMMM yyyy") + ".</u> This date is <u>ANTICIPATED ONLY</u>, and subject to change without notice." : null,
                PexaNominatedDateNew = emailModel.pexaNominatedDate.HasValue && emailModel.pexaNominatedDate != emptyDate ? "<b>PEXA Proposed Settlement Date: <u>" + emailModel.pexaNominatedDate.Value.ToString("dddd, dd MMMM yyyy") + ".</b></u> This date is subject to change by any of the parties, even on the proposed day of settlement." : "",

                AnticipatedDate = emailModel.pexaNominatedDate.HasValue ? "The current nominated date in PEXA is <u>" + emailModel.pexaNominatedDate.Value.ToString("dddd, dd MMMM yyyy") + ".</u> This date is <u>ANTICIPATED ONLY</u>, and subject to change without notice." : emailModel.ManualAnticipatedDate.HasValue ? "The current anticipated settlement date is <u>" + emailModel.ManualAnticipatedDate.Value.ToString("dddd, dd MMMM yyyy") + ".</u> This date is anticipated only, and subject to change without notice." : null,
                DischargePayoutStr = (emailModel.DischIndicativePayouts != null || emailModel.DischPayouts != null) ? BuildDischargePayoutString(emailModel.DischIndicativePayouts.ToList(), emailModel.DischPayouts.ToList()) : null,
                DischargePayoutRequirements = ledgerItemsStr,

                PartyFullnames = partyFullNames,

                SecurityAddress = string.Empty,
                SecuritySuburbs = securitySuburbs,

                TitleReferences = string.Empty,
                FHOGAnswer = "<b>FHOG: </b>" + (emailModel.FHOGApplies ? "Yes" : "No"), 
                EmailMobiles = new EmailEntities.EmailMobileAddresses
                {
                    Broker = isSMS? emailModel.brokerMobile :emailModel.brokerEmail,
                    CurrentUser = isSMS ? emailModel.currUserMobile : emailModel.currUserEmail,
                    FileOwner = isSMS ? emailModel.fileOwnerMobile : emailModel.fileOwnerEmail,
                    Lender = isSMS ? emailModel.lenderMobile : lenderEmail,
                    OtherParty = isSMS? string.Join(";", emailModel.otherPartyMobiles.ToList() ) : string.Join(";",emailModel.otherPartyEmails.ToList()),
                    MortMgr = isSMS ? emailModel.mortMgrMobile : emailModel.mortMgrEmail,
                    CCEmails = emailModel.CCNotifications,
                    RelationshipManager = isSMS? emailModel.relationshipManagerDetails?.Mobile : emailModel.relationshipManagerDetails?.Email,
                    Borrower = isSMS ? string.Join(";", emailModel.BorrowerDetails.Select(x=>x.Mobile).ToList()) : string.Join(";", emailModel.BorrowerDetails.Select(x => x.Email).ToList())
                }                
            };

            string formattedBorrowerNames = "";

            for(int i = 0; i < emailModel.BorrowerDetails?.Count(); i++)
            {
                var party = emailModel.BorrowerDetails[i];
                string formattedBorrowerName = party.IsIndividual ? (party.Firstname + " " + party.Lastname)?.Trim() : party.CompanyName?.Trim();

                if (!string.IsNullOrEmpty(formattedBorrowerName))
                {
                    if (emailModel.BorrowerDetails.Count > 1 && i < emailModel.BorrowerDetails.Count - 2)
                    {
                        formattedBorrowerName += ", ";
                    }
                    else if (emailModel.BorrowerDetails.Count > 1 && i == emailModel.BorrowerDetails.Count - 2)
                    {
                        formattedBorrowerName += " & ";
                    }
                }
                formattedBorrowerNames += formattedBorrowerName;

            }
            model.BorrowerName = formattedBorrowerNames;

            if (matterGroupTypeId == (int)MatterGroupTypeEnum.NewLoan)
            {
                model.MatterFinancialGrid = BuildFinancialGridPlaceholder(matterId);
            }
            if(matterGroupTypeId == (int)MatterGroupTypeEnum.Consent)
            {
                var finDetails = BuildDisbursementsPlaceholder(matterId);
                model.MatterDisbursements = finDetails.Item1;
                model.DisbursementsTotal = finDetails.Item2;
            }


            if (emailModel.lenderAddnlContacts.Any())
            {
                if (isSMS)
                {
                    model.EmailMobiles.SecondaryContacts = string.Join(";", emailModel.lenderAddnlContacts.Where(x => !string.IsNullOrEmpty(x.Mobile)).Select(x => x.Mobile));
                }
                else
                {
                    model.EmailMobiles.SecondaryContacts =  string.Join(";", emailModel.lenderAddnlContacts.Where(x => !string.IsNullOrEmpty(x.Email)).Select(x => x.Email));
                }
            }
            if (!string.IsNullOrEmpty(emailModel.brokerMobile))
            {
                string cleanMobile = emailModel.brokerMobile.Trim();
                if (cleanMobile.Length == 9)
                    if (cleanMobile.Substring(0, 1) == "4")
                        cleanMobile = "0" + cleanMobile;
                if (cleanMobile.Length == 10)
                    if(cleanMobile.Substring(0,2) == "04")
                        model.EmailMobiles.BrokerMobile = cleanMobile+GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_MSASMSAddress, context);
            }

            foreach(var borr in emailModel.BorrowerDetails)
            {
                if (!string.IsNullOrEmpty(borr.Mobile))
                {
                    string cleanMobile = borr.Mobile.Trim();
                    if (cleanMobile.Length == 9)
                        if (cleanMobile.Substring(0, 1) == "4")
                            cleanMobile = "0" + cleanMobile;
                    if (cleanMobile.Length == 10)
                        if (cleanMobile.Substring(0, 2) == "04")
                            model.EmailMobiles.BorrowerMobiles += cleanMobile + GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_MSASMSAddress, context) + ";";
                }
            }
            
            if (emailModel.Securities.Any(m =>  m.SettlementTypeId == (int)Enums.SettlementTypeEnum.Paper))
            {
                model.SettlementType = "Paper";
            }

            if (emailModel.Parties != null && emailModel.Parties.Any())
            {
                model.Parties = string.Empty;
                for (int i = 0; i <= emailModel.Parties.Count() - 1; i++)
                {
                    if (i > 0) model.Parties += " & ";
                    model.Parties += CommonMethods.FormatLastNameWithFirstNameInitial(emailModel.Parties[i].Firstname, emailModel.Parties[i].Lastname);
                }
            }

            foreach (var sec in emailModel.Securities)
            {
                model.SecurityAddress += string.Format("  -  {0}{1}",
                    EntityHelper.FormatAddressDetails(sec.StreetAddress, sec.Suburb, sec.StateName, sec.PostCode, sec.MatterTypeName), Environment.NewLine);
            }

            foreach (var titleRef in emailModel.TitleRefs)
            {
                model.TitleReferences += string.Format(" - {0}{1}", titleRef, Environment.NewLine);
            }


            model.MatterDetails = string.Format("{0} {1} {2}",
                string.IsNullOrEmpty(model.Parties) ? null : "LOAN TO " + model.Parties, 
                string.IsNullOrEmpty(model.LenderRefNo) ? null : "REF " + model.LenderRefNo,
                string.IsNullOrEmpty(model.SecondaryRefNo) ? null : "LFID " + model.LenderRefNo,
                string.IsNullOrEmpty(model.LoanAmount) ? null : model.LoanAmount);

            //if (!string.IsNullOrEmpty(model.EmailMobiles.Broker) && !string.IsNullOrEmpty(model.EmailMobiles.Borrower))
            //{
            //    model.EmailMobiles.CCEmails = model.EmailMobiles.Broker;
            //    model.EmailMobiles.Broker = null;
            //}

            return model;
        }
        public string BuildWorkTypeString(IEnumerable<string> workTypes, string matterGroupTypeName)
        {
            workTypes = workTypes.Distinct().ToList();
            if(workTypes.Count() == 1)
            {
                return workTypes.First();
            }
            else if(workTypes.Count() == 0)
            {
                return matterGroupTypeName;
            }
            else
            {
                return string.Join(" / ", workTypes);
            }
        }

        static string ReplaceLast(string find, string replace, string str)
        {
            int lastIndex = str.LastIndexOf(find);

            if (lastIndex == -1)
            {
                return str;
            }

            string beginString = str.Substring(0, lastIndex);
            string endString = str.Substring(lastIndex + find.Length);

            return beginString + replace + endString;
        }
        public static string CleanName(String name)
        {
            name = name.Trim();
            //Daniel "The Chang" Hwang's fault. Try and remove the second name if it looks like it's a middle name rather than a second half of a first name.
            if (name.Contains(" "))
            {
                if (name.Length > 10)
                {
                    int spaceIndex = name.IndexOf(" ");
                    name = name.Substring(0, spaceIndex);
                }
            }
            return name;
        }
        public string BuildFinancialGridPlaceholder(int matterId)
        {
            var sb = new StringBuilder();

            var model = BuildReadyToBookEmailModel(matterId, (int)WFComponentEnum.BookSettlement, null, null);

            sb.Append($"<p><span style='font-size: 14pt; font-family: Calibri; color: #00B050;'><b>Loan Amount = {(model.BookingDetails.LoanAmount.ToString("C"))}</b></span></p>");

            //sb.Append("<table width='650'>");
            //sb.Append($"<tr><td style='font-size: 11pt; font-family: Calibri;' width='450';><u>Total Amount</u></td>");
            //sb.Append($"<td style='font-size: 11pt; font-family: Calibri;'width='200'; align='right'><b>{(model.BookingDetails.LoanAmount.ToString("C"))}</b></td></tr>");
            //sb.Append("</table><br/>");

            //Lender Items
            if (model.BookingDetails.LenderRetainedItems.Any())
            {
                sb.Append("<table style='margin - left:10;' width='650'>");
                sb.Append("<tr><td width='30'>&nbsp;</td><td colspan=2 style='font-size: 12pt; font-family: Calibri; color: #0070C0;'><b>Less Amounts Retained by Lender</b></th></tr>");
                foreach (var lri in model.BookingDetails.LenderRetainedItems)
                {
                    sb.Append($"<tr><td>&nbsp;</td><td style='font-size: 12pt; font-family: Calibri;' width='370'>{lri.Description}</td>");
                    sb.Append($"<td style='font-size: 12pt; font-family: Calibri;' width='250' align='right'>{(lri.Amount.Value.ToString("C"))}</td></tr>");
                }
                sb.Append($"<tr><td>&nbsp;</td><td style='font-size: 12pt; font-family: Calibri;' align='right'><b>Total = </b></td>");
                sb.Append($"<td style='font-size: 12pt; font-family: Calibri;' align=right><b>{(model.BookingDetails.LenderRetainedItems.Sum(x => x.Amount.Value).ToString("C"))}</b></td></tr>");
                sb.Append("</table><br/>");
            }

            //Fund Items
            if (model.BookingDetails.ExpectedFundsItems.Any())
            {
                sb.Append("<table style='margin-left:10;' width='650'>");
                sb.Append("<tr><td width='30'>&nbsp;</td><td colspan=2 style='font-size: 12pt; font-family: Calibri; color: #0070C0;'><b>Funding Amounts</b></th></tr>");

                foreach (var lri in model.BookingDetails.ExpectedFundsItems)
                {
                    sb.Append($"<tr><td>&nbsp;</td><td style='font-size: 12pt; font-family: Calibri;' width='455'>{lri.Description}</td>");
                    sb.Append($"<td style='font-size: 12pt; font-family: Calibri;' width='165' align='right'>{(lri.Amount.Value.ToString("C"))}</td></tr>");
                }
                sb.Append($"<tr><td>&nbsp;</td><td style='font-size: 12pt; font-family: Calibri;' align='right'><b>Funding Amounts Total =</b></td>");
                sb.Append($"<td style='font-size: 12pt; font-family: Calibri;' align='right'><b>{(model.BookingDetails.ExpectedFundsItems.Sum(x => x.Amount.Value).ToString("C"))}</b></td></tr>");
                sb.Append("</table><br/>");
            }

            //Fee Deductions
            sb.Append("<table style='margin-left:10;' width='650'>");
            sb.Append("<tr><td width='30'>&nbsp;</td><td colspan=4 style='font-size: 12pt; font-family: Calibri; color: #0070C0'><b>Deductions Amounts</b></th></tr>");
            int liIndex = 0;
            foreach (var li in model.LedgerItems)
            {
                liIndex++;
                sb.Append($"<tr><td width='30'>&nbsp;</td><td width='25' style='font-size: 12pt; font-family: Calibri;'>{liIndex}.</td>");
                sb.Append($"<td width='250' style='font-size: 12pt; font-family: Calibri;'>{li.Description}</td>");
                sb.Append($"<td style='font-size: 12pt; font-family: Calibri;' width='200'>{li.PayableTo}</td>");
                sb.Append($"<td style='font-size: 12pt; font-family: Calibri;' align='right' width='175'>{li.AmountString}</td></tr>");
            }
            sb.Append($"<tr><td colspan='4' style='font-size: 12pt; font-family: Calibri;' align='right'><b>Deductions Total =</b></td>");
            sb.Append($"<td style='font-size: 12pt; font-family: Calibri;' align='right'><b>{model.LedgerItemsTotal}</b></td></tr>");
            sb.Append("</table><br/>");


            //Total
            var total = (model.BookingDetails.ExpectedFundsItems.Any() ? model.BookingDetails.ExpectedFundsItems.Sum(x => x.Amount.Value) : model.BookingDetails.LoanAmount)
                - model.LedgerItems.Sum(x => x.Amount.Value);

            sb.Append("<table width='650' style='margin-left:10;'>");
            sb.Append($"<tr><td width='30'>&nbsp;</td><td style='font-size: 16pt; font-family: Calibri; color: #00B050;' width='455' align='right'><b>Available Funds =</b></td>");
            sb.Append($"<td style='font-size: 16pt; font-family: Calibri; color: #00B050;' width='165' align='right'><b>{(total.ToString("C"))}</b></td></tr>");
            sb.Append("</table><br/>");


            //if (model.IsPurchase && model.BookingDetails.IsPaper)
            //{
            //    sb.Append($"<p><span style='font-size: 20px; font-family: Calibri;'><b>Note</b></span></p>");
            //    sb.Append($"<p><span style='font-size: 11pt; font-family: Calibri;'>The first {model.BookingDetails.NumberOfFreeCheques} bank cheques directed by you are free.");
            //    if (model.BookingDetails.BankChequeFee.HasValue)
            //    {
            //        sb.Append($" Each subsequent cheque is {model.BookingDetails.BankChequeFee.Value.ToString("c")}.</span></p>");
            //    }
            //    sb.Append($"<p><span style='font-size: 11pt; font-family: Calibri;'>The Direction to Pay must be in writing sent to us by fax or email <b>4.00 pm two (2) days prior to settlement.</b></span>");
            //    sb.Append($"<p><span style='font-size: 11pt; font-family: Calibri;'>Any shortfalls are to be organised with your client directly.  Authority to debit your client's account is not available.</span></p>");
            //}

            return sb.ToString();
        }
        public Tuple<string,string> BuildDisbursementsPlaceholder(int matterId)
        {
            var sb = new StringBuilder();

            var model = BuildReadyToBookEmailModel(matterId, (int)WFComponentEnum.BookSettlement, null, null);


            //sb.Append("<table width='650'>");
            //sb.Append($"<tr><td style='font-size: 11pt; font-family: Calibri;' width='450';><u>Total Amount</u></td>");
            //sb.Append($"<td style='font-size: 11pt; font-family: Calibri;'width='200'; align='right'><b>{(model.BookingDetails.LoanAmount.ToString("C"))}</b></td></tr>");
            //sb.Append("</table><br/>");

   
            //Fee Deductions
            sb.Append("<table style='margin-left:10;' width='650'>");
            //sb.Append("<tr><td width='30'>&nbsp;</td><td colspan=4 style='font-size: 12pt; font-family: Calibri; color: #0070C0'><b>Deductions Amounts</b></th></tr>");
            int liIndex = 0;
            foreach (var li in model.LedgerItems)
            {
                liIndex++;
                sb.Append($"<tr><td width='30'>&nbsp;</td><td width='25' style='font-size: 12pt; font-family: Calibri;'>{liIndex}.</td>");
                sb.Append($"<td width='250' style='font-size: 12pt; font-family: Calibri;'>{li.Description}</td>");
                sb.Append($"<td style='font-size: 12pt; font-family: Calibri;' width='200'>{li.PayableTo}</td>");
                sb.Append($"<td style='font-size: 12pt; font-family: Calibri;' align='right' width='175'>{li.AmountString}</td></tr>");
            }
            sb.Append($"<tr><td colspan='4' style='font-size: 12pt; font-family: Calibri;' align='right'><b>Total =</b></td>");
            sb.Append($"<td style='font-size: 12pt; font-family: Calibri;' align='right'><b>{model.LedgerItemsTotal}</b></td></tr>");
            sb.Append("</table><br/>");


        


            //if (model.IsPurchase && model.BookingDetails.IsPaper)
            //{
            //    sb.Append($"<p><span style='font-size: 20px; font-family: Calibri;'><b>Note</b></span></p>");
            //    sb.Append($"<p><span style='font-size: 11pt; font-family: Calibri;'>The first {model.BookingDetails.NumberOfFreeCheques} bank cheques directed by you are free.");
            //    if (model.BookingDetails.BankChequeFee.HasValue)
            //    {
            //        sb.Append($" Each subsequent cheque is {model.BookingDetails.BankChequeFee.Value.ToString("c")}.</span></p>");
            //    }
            //    sb.Append($"<p><span style='font-size: 11pt; font-family: Calibri;'>The Direction to Pay must be in writing sent to us by fax or email <b>4.00 pm two (2) days prior to settlement.</b></span>");
            //    sb.Append($"<p><span style='font-size: 11pt; font-family: Calibri;'>Any shortfalls are to be organised with your client directly.  Authority to debit your client's account is not available.</span></p>");
            //}

            return new Tuple<string, string>(sb.ToString(), model.LedgerItemsTotal);
        }

        public string BuildDischargePayoutString(List<MatterCustomEntities.MatterIndicativePayoutView> indicativePayouts, List<MatterCustomEntities.MatterIndicativePayoutView> confirmedPayouts)
        {
            string payoutStr = "";
            if (confirmedPayouts.Any())
            {
                payoutStr += "The current payout figures for this matter are as follows:<p/>";
                foreach(var payout in confirmedPayouts)
                {
                    payoutStr += $"<div style='text-indent:2em;'><b>Loan Account: </b>{payout.LoanAccountNo}</div>";
                    payoutStr += $"<div style='text-indent:2em;'><b>Payout Amount: </b>{payout.IndicativePayoutAmount.ToString("c")}</div><br/>";
                }
            }
            else if (indicativePayouts.Any())
            {
                payoutStr += "The current <b>indicative</b> payout figures for this matter are as follows:";
                foreach (var payout in indicativePayouts)
                {
                    payoutStr += $"<div style='text-indent:2em;'><b>Loan Account: </b>{payout.LoanAccountNo}</div>";
                    payoutStr += $"<div style='text-indent:2em;'><b>Payout Amount: </b>{payout.IndicativePayoutAmount.ToString("c")}</div><br/>";
                }
            }
            if (!string.IsNullOrEmpty(payoutStr))
            {
                payoutStr += "<i>Please note that these amounts are subject to change.</i><br/>";
            }
            return payoutStr;
        }

        public void LogEmail(GeneralCustomEntities.EmailLogView log)
        {
            MatterWFAutoEmailLog logEntity = new MatterWFAutoEmailLog()
            {
                LogNotes = log.LogNotes,
                MatterId = log.MatterId ?? 0,
                WFComponentId = log.WFComponentId,
                SentByUserId = log.EmailFromUserId,
                SentDate = DateTime.Now,
                FromNoReply = log.FromNoReply,
                EmailTo = log.EmailTo,
                EmailCC = log.EmailCC,
                EmailBCC = log.EmailBCC,
                EmailSubject = log.EmailSubject,
                EmailBody = log.Body,
                ModelLenderEmail = log.ModelLenderEmail,
                ModelBrokerEmail = log.ModelBrokerEmail,
                ModelBorrowerEmail = log.ModelBorrowerEmail,
                ModelOtherPartyEmail = log.ModelOtherPartyEmail,
                ModelAlternativeEmail = log.ModelAlternativeEmail,
                ModelCCEmail = log.ModelCCEmail,
                ModelNoReply = log.ModelNoReply
            };
            context.MatterWFAutoEmailLogs.Add(logEntity);
        }

        public string GetCapitals(string input)
        {
            return string.Concat(input.Where(c => char.IsUpper(c)));
        }
        public Dictionary<string, string> LinkPlaceHoldersToMatterValues(int matterId)
        {
            var emailPlaceHolderActions = new Dictionary<string, string>();

            var mt = context.Matters.Where(m => m.MatterId == matterId)
                        .Select(m => new
                        {
                            m.MatterId,
                            m.MatterType.MatterTypeName,
                            m.LenderRefNo,
                            fileOwnerName = m.User.Fullname,
                            fileOwnerEmail = m.User.Email,
                            fileOwnerMobile = m.User.Mobile,
                            fileOwnerPhone = m.User.Phone,
                            fileOwnerInitials = m.User.DisplayInitials,
                            m.MatterDescription,
                            m.Lender.LenderName,
                            lenderEmail = m.Lender.PrimaryContact.Email,
                            lenderMobile = m.PrimaryContact1.Mobile ?? m.Lender.PrimaryContact.Mobile,
                            brokerLastname = m.Broker.PrimaryContact.Lastname,
                            brokerFirstname = m.Broker.PrimaryContact.Firstname,
                            brokerEmail = m.Broker.PrimaryContact.Email,
                            brokerMobile = m.Broker.PrimaryContact.Mobile,
                            m.MortMgr.MortMgrName,
                            mortMgrEmail = m.PrimaryContact2.Email ?? m.MortMgr.PrimaryContact.Email,
                            mortMgrMobile = m.PrimaryContact2.Mobile ?? m.MortMgr.PrimaryContact.Mobile,
                            LoanAmt = m.MatterLoanAccounts.Any() ? m.MatterLoanAccounts.Sum(x => x.LoanAmount) : 0,
                            m.CCNotifications,
                            SettlementDate = (DateTime?)m.SettlementSchedule.SettlementDate,
                            SettlementTime = (TimeSpan?)m.SettlementSchedule.SettlementTime,
                            LoantrakToken = m.LimitedLoantrakAccessTokens.Select(x=>x.LimitedLoantrakAccessTokenValue).FirstOrDefault(),
                            SettlementVenue = m.SettlementSchedule.SettlementScheduleVenues.Any() ? m.SettlementSchedule.SettlementScheduleVenues.FirstOrDefault().SettlementVenue.VenueName : null,
                            BorrowerDetails = m.MatterParties.Where(mp => mp.PartyTypeId == (int)Enums.MatterPartyTypeEnum.Borrower)
                                                        .Select(p => new { p.Firstname, p.Lastname, p.Email, p.Mobile }).FirstOrDefault(),
                            Parties = m.MatterParties.Where(mp => mp.PartyTypeId == (int)Enums.MatterPartyTypeEnum.Borrower)
                                                        .Select(p => new { p.Firstname, p.Lastname }).ToList(),
                            Securities = m.MatterSecurities.Where(ms => !ms.Deleted)
                                                        .Select(s => new { s.MatterSecurityId, s.PostCode, s.State.StateName, s.Suburb, s.StreetAddress, s.SettlementTypeId, s.MatterType.MatterTypeName }).ToList(),
                            TitleRefs = m.MatterSecurities.Where(x => !x.Deleted).SelectMany(tr => tr.MatterSecurityTitleRefs.Select(y => new { y.TitleReference, y.MatterSecurityId })).ToList(),
                            PexaWorkspaces = m.MatterPexaWorkspaces.Where(x=>x.MatterSecurityMatterPexaWorkspaces.Any(s=>!s.MatterSecurity.Deleted)).Select(x=>x.PexaWorkspace.PexaWorkspaceNo),
                            OutstandingRequirements = m.MatterWFComponents
                             .SelectMany(x=>x.MatterWFOutstandingReqs.SelectMany(r=>r.MatterWFOutstandingReqItems.Where(i=>!i.Resolved).Select(i=>i.OutstandingItemName)))
                        }).FirstOrDefault();

            string tmpStr;

            emailPlaceHolderActions.Add(DomainConstants.EmailMatterId, (mt.fileOwnerInitials ?? GetCapitals(mt.fileOwnerName)) +" "+ mt.MatterId.ToString());
            emailPlaceHolderActions.Add(DomainConstants.EmailMatterDescription, mt.MatterDescription);

            //            emailPlaceHolderActions.Add(DomainConstants.EmailMilestone, () => { replaceValue = model.Milestone; });
            emailPlaceHolderActions.Add(DomainConstants.EmailMatterType, mt.MatterTypeName);
            emailPlaceHolderActions.Add(DomainConstants.EmailCurrentUser, GlobalVars.CurrentUser.FullName);
            emailPlaceHolderActions.Add(DomainConstants.EmailTodaysDate, DateTime.Today.ToString("dd-MMM-yyyy"));
            emailPlaceHolderActions.Add(DomainConstants.EmailLoantrakToken, mt.LoantrakToken);
            emailPlaceHolderActions.Add(DomainConstants.EmailOutstandingReqs, " - " + string.Join("\n - ", mt.OutstandingRequirements));
            emailPlaceHolderActions.Add(DomainConstants.EmailPexaWorkspaces, string.Join(", ", mt.PexaWorkspaces));
            emailPlaceHolderActions.Add(DomainConstants.EmailMortMgr, mt.MortMgrName);

            tmpStr = "";
            foreach (var mp in mt.Parties)
                tmpStr += CommonMethods.FormatLastNameWithFirstNameInitial(mp.Firstname, mp.Lastname) + " & ";
            if (mt.Parties.Any()) tmpStr = tmpStr.Substring(0, tmpStr.Length - 3);
            emailPlaceHolderActions.Add(DomainConstants.EmailParties, tmpStr);

            tmpStr = "Loan To " + tmpStr + " LFID " + mt.LenderRefNo + " " + mt.LoanAmt.ToString("c");
            emailPlaceHolderActions.Add(DomainConstants.EmailMatterDetails, tmpStr);

            tmpStr = "";
            for (int i = 0; i <= mt.Securities.Count() - 1; i++)
            {
                if (i > 0) tmpStr += Environment.NewLine;
                tmpStr += EntityHelper.FormatAddressDetails(mt.Securities[i].StreetAddress, mt.Securities[i].Suburb, mt.Securities[i].StateName, mt.Securities[i].PostCode, mt.Securities[i].MatterTypeName);
            }
            emailPlaceHolderActions.Add(DomainConstants.EmailSecurities, tmpStr);
            emailPlaceHolderActions.Add(DomainConstants.EmailFileOwner, mt.fileOwnerName);
            emailPlaceHolderActions.Add(DomainConstants.EmailFileOwnerEmail, mt.fileOwnerEmail);
            emailPlaceHolderActions.Add(DomainConstants.EmailFileOwnerPhone, mt.fileOwnerPhone ?? mt.fileOwnerMobile);
            emailPlaceHolderActions.Add(DomainConstants.EmailLender, mt.LenderName);
            emailPlaceHolderActions.Add(DomainConstants.EmailBorrower, EntityHelper.GetFullName(mt.BorrowerDetails?.Lastname, mt.BorrowerDetails?.Firstname));
            emailPlaceHolderActions.Add(DomainConstants.EmailBroker, EntityHelper.GetFullName(mt.brokerLastname, mt.brokerFirstname));
            emailPlaceHolderActions.Add(DomainConstants.EmailLoanAmt, mt.LoanAmt.ToString("c"));
            emailPlaceHolderActions.Add(DomainConstants.EmailLenderRefNo, mt.LenderRefNo);
            emailPlaceHolderActions.Add(DomainConstants.EmailSettlementDate, mt.SettlementDate.HasValue ? mt.SettlementDate.Value.ToString("dd-MMM-yyyy") : null);
            emailPlaceHolderActions.Add(DomainConstants.EmailSettlementTime, mt.SettlementTime.HasValue ? DateTime.Today.Add(mt.SettlementTime.Value).ToString("h:mm tt") : null);
            emailPlaceHolderActions.Add(DomainConstants.EmailSettlementVenue, mt.SettlementVenue);

            emailPlaceHolderActions.Add(DomainConstants.EmailGreeting, "Good " + EmailsService.GetTimeOfDay(DateTime.Now) + ",");


            tmpStr = "";
            for (int i = 0; i <= mt.TitleRefs.Count() - 1; i++)
            {
                if (i > 0) tmpStr += Environment.NewLine;
                tmpStr += mt.TitleRefs[i];
            }
            emailPlaceHolderActions.Add(DomainConstants.EmailTitleReferences, tmpStr);
            emailPlaceHolderActions.Add(DomainConstants.EmailSendProcDocsDetails, EmailsService.BuildSendProcDocsEmail(BuildSendProcDocsEmailModel(matterId)));

            if (mt.Securities.Any(m => m.SettlementTypeId == (int)Enums.SettlementTypeEnum.Paper))
            {
                emailPlaceHolderActions.Add(DomainConstants.EmailPaperPexa, "Paper");
            }
            else
            {
                emailPlaceHolderActions.Add(DomainConstants.EmailPaperPexa, "PEXA");
            }

            return emailPlaceHolderActions;
        }

        public string ReplaceEmailPlaceHolders(Dictionary<string, string> replaceDictionary, string textToReplace)
        {
            if (string.IsNullOrWhiteSpace(textToReplace)) return string.Empty;

            foreach (KeyValuePair<string, string> entry in replaceDictionary)
                textToReplace = textToReplace.Replace("{{" + entry.Key + "}}", entry.Value);
            return textToReplace;
        }



        public EmailEntities.EmailPlaceHolderModel BuildEmailPlaceHolderModelTEST(int matterWFComponentId)
        {
            var emailModel =
                (from m in context.Matters.AsNoTracking()
                 join wf in context.MatterWFComponents.AsNoTracking() on m.MatterId equals wf.MatterId
                 where wf.MatterWFComponentId == matterWFComponentId
                 select new
                 {
                     SettlementDate = (DateTime?) m.SettlementSchedule.SettlementDate,
                     SettlementTime = (TimeSpan?) m.SettlementSchedule.SettlementTime
                 }).FirstOrDefault();

            if (emailModel == null) return null;

            var model = new EmailEntities.EmailPlaceHolderModel
            {

                SettlementDate = emailModel.SettlementDate.HasValue ? emailModel.SettlementDate.Value.ToString("dd-MMM-yyyy") : null,
                SettlementTime = emailModel.SettlementTime.HasValue ? DateTime.Today.Add(emailModel.SettlementTime.Value).ToString("h:mm tt") : null
            };

          
            return model;
        }

        public EmailEntities.EmailModel BuildEmailModel(int matterId, bool isSelfActing)
        {
            EmailEntities.EmailModel model = null;
            if (isSelfActing)
            {
                model = BuildSelfActingEmailModel(matterId);
            }
            else
            {
                model = BuildNonSelfActingEmailModel(matterId);
            }

            return model;
        }
        
        /// <summary>
        /// Builds Email for Non Self Acting - As per SelfActingModel except pulling from Ledger Items
        /// </summary>
        /// <param name="matterId"></param>
        /// <returns></returns>
        private EmailEntities.EmailModel BuildNonSelfActingEmailModel(int matterId)
        {
            var em =
            (from m in context.Matters.AsNoTracking()
             where m.MatterId == matterId
             select new
             {
                 m.MatterId,
                 m.LenderRefNo,
                 m.MatterDescription,
                 m.StateId,
                 m.LenderId,
                 m.SettlementSchedule.Email,
                 SettlementDate = (DateTime?) m.SettlementSchedule.SettlementDate,
                 SettlementTime = (TimeSpan?) m.SettlementSchedule.SettlementTime,
                 SettlementVenue = m.SettlementSchedule.SettlementScheduleVenues.FirstOrDefault().SettlementVenue.VenueName,
                 m.SettlementSchedule.SettlementScheduleVenues.FirstOrDefault().SettlementAgent.AgentName,
                 AgentAddress = m.SettlementSchedule.SettlementScheduleVenues.FirstOrDefault().SettlementAgent.StreetAddress,
                 AgentSuburb = m.SettlementSchedule.SettlementScheduleVenues.FirstOrDefault().SettlementAgent.Suburb,
                 AgentState = m.SettlementSchedule.SettlementScheduleVenues.FirstOrDefault().SettlementAgent.State.StateName,

                 Securities = m.MatterSecurities.Where(ms => !ms.Deleted && ms.MatterId == matterId)
                        .Select(s => new { s.MatterSecurityId, s.PostCode, s.State.StateName, s.Suburb, s.StreetAddress }).ToList(),
                 TitleRefs = m.MatterSecurities.Where(x => !x.Deleted && x.MatterId == matterId).SelectMany(tr => tr.MatterSecurityTitleRefs.Select(y => new { y.TitleReference, y.MatterSecurityId })).ToList(),

                 LedgerItems = m.MatterLedgerItems.Where
                 (li => li.PayableByTypeId == (int) Enums.PayableTypeEnum.Borrower && li.PayableToTypeId == (int)Enums.PayableTypeEnum.MSA 
                                            && li.TransactionTypeId == (int) Enums.TransactionTypeEnum.Cheque)
                    .Select(l => new { PayableTo = l.PayableToName, l.Amount, l.Description }).ToList(),

                 NonLedgerItems = m.MatterLedgerItems.Where
                 (li => li.PayableByTypeId == (int) Enums.PayableTypeEnum.Borrower && li.PayableToTypeId != (int)Enums.PayableTypeEnum.MSA && li.TransactionTypeId == (int) Enums.TransactionTypeEnum.Cheque)
                 .Select(l => new { PayableTo = l.PayableToName, l.Amount, l.Description }).ToList(),
                 RegisteredDocs = m.MatterSecurities.Where(x => !x.Deleted && x.MatterId == matterId).SelectMany(rd => rd.MatterSecurityDocuments.Select(sd => new { sd.MatterSecurityId, sd.RegisteredDocumentType.RegisteredDocumentName, sd.DocumentName })).ToList()
             }).FirstOrDefault();

            if (em == null) return null;

            var model = new EmailEntities.EmailModel
            {
                MatterId = em.MatterId,
                LenderRefNo = em.LenderRefNo,
                SettlementDate = em.SettlementDate.HasValue ? em.SettlementDate.Value.ToString("dd-MMM-yyyy") : null,
                SettlementTime = em.SettlementTime.HasValue ? DateTime.Today.Add(em.SettlementTime.Value).ToString("h:mm tt") : null,
                SettlementVenue = string.IsNullOrEmpty(em.SettlementVenue) ? $"{em.AgentName}, {(EntityHelper.FormatAddressDetails(em.AgentAddress, em.AgentSuburb, em.AgentState, null))}" : em.SettlementVenue,
                EmailAddress = em.Email,
                Subject = $"{em.MatterDescription}:Matter-{em.MatterId}:Ref-{em.LenderRefNo}"
            };

            int secIndex = -1;
            foreach (var sec in em.Securities)
            {
                secIndex++;
                model.SecurityList.Add(new EmailEntities.EmailSecurity { SecurityId = sec.MatterSecurityId, SecurityAddress = EntityHelper.FormatAddressDetails(sec.StreetAddress, sec.Suburb, sec.StateName, string.Empty) });
                model.SecuritiesAppended += EntityHelper.FormatAddressDetails(sec.StreetAddress, sec.Suburb, sec.StateName, sec.PostCode);
                if (em.Securities.Count > 1 && secIndex != em.Securities.Count - 1)
                    model.SecuritiesAppended += ", ";
            }

            var ledgerItems = new List<EmailEntities.EmailLedgerItem>();
            decimal ledgerTotal = 0;
            foreach (var li in em.LedgerItems)
            {
                ledgerTotal += li.Amount;
                ledgerItems.Add(new EmailEntities.EmailLedgerItem
                {
                    PayableTo = li.PayableTo,
                    Amount = li.Amount,
                    Description = li.Description,
                    IsLedgerItem = true
                });
            }

            foreach (var li in em.NonLedgerItems)
            {
                ledgerTotal += li.Amount;
                ledgerItems.Add(new EmailEntities.EmailLedgerItem
                {
                    PayableTo = li.PayableTo,
                    Amount = li.Amount,
                    Description = li.Description,
                    IsLedgerItem = false
                });
            }
            model.LedgerItemsTotal = ledgerTotal.ToString("C");

            model.LedgerItems = BuildLedgerItemsForEmail(ledgerItems, em.LenderId);

            foreach (var doc in em.TitleRefs)
            {
                model.SecurityDocs.Add(new EmailEntities.EmailSecurityDoc { SecurityId = doc.MatterSecurityId, DocumentName = $"Certificate of Title {doc.TitleReference}" });
            }
            foreach (var doc in em.RegisteredDocs)
            {
                model.SecurityDocs.Add(new EmailEntities.EmailSecurityDoc { SecurityId = doc.MatterSecurityId, DocumentName = $"{doc.RegisteredDocumentName} {doc.DocumentName}" });
            }

            return model;
        }

        public string GetToEmailAddressFromUserCompleteComponent(int matterId, int wfComponentId)
        {
            var matterWFComponentId = context.MatterWFComponents.Where
                (x => x.MatterId == matterId && x.WFComponentId == wfComponentId &&
                 x.WFComponentStatusTypeId == (int) MatterWFComponentStatusTypeEnum.Complete).OrderByDescending(x=> x.DisplayOrder).FirstOrDefault()?.MatterWFComponentId;

            return GetUserEmailFromMatterWFComponent(matterWFComponentId);
        }

        private string GetUserEmailFromMatterWFComponent(int? matterWFComponentId)
        {
            if (matterWFComponentId == null) return null;

            var userId = context.MatterEvents.FirstOrDefault(x => x.MatterWFComponentId == matterWFComponentId && x.MatterEventStatusTypeId == (int) MatterEventStatusTypeEnum.Good &&
                x.MatterEventTypeId == (int) MatterEventTypeList.MilestoneComplete
            )?.EventByUserId;

            if (userId != null)
            {
                return context.Users.FirstOrDefault(x => x.UserId == userId)?.Email;
            }
            else return null;            
        }

        public string GetToEmailAddressFromPreviousUserCompletedComponent(int matterId, int displayOrder)
        {
            var matterWFComponentId = context.MatterWFComponents.OrderByDescending(x=> x.DisplayOrder).Where
                (x => x.MatterId == matterId && x.MatterId == matterId &&
                 x.WFComponentStatusTypeId == (int) MatterWFComponentStatusTypeEnum.Complete &&
                 x.DisplayOrder < displayOrder
                 ).FirstOrDefault()?.MatterWFComponentId;

            return GetUserEmailFromMatterWFComponent(matterWFComponentId);
        }






        

        public List<EmailEntities.MNPWarningEmailQueueView> GetMNPWarningEmailQueue()
        {
            List<EmailEntities.MNPWarningEmailQueueView> definitions =
                context.MNPWarningEmailDefinitions.Where(e => e.Enabled)
                .Select
                (
                    e => new EmailEntities.MNPWarningEmailQueueView()
                    {
                        MNPWarningEmailDefinitionId = e.MNPWarningEmailDefinitionId,
                        MNPTriggerTypeId = e.MNPEmailTriggerTypeId,
                        FacilityTypeId = e.FacilityTypeId,
                        LenderId = e.LenderId,
                        LenderName = e.Lender.LenderName,
                        StateId = e.StateId,
                        MatterTypeId = e.MatterTypeId,
                        MortMgrId = e.MortMgrId,
                        MortMgrTypeId = e.MortMgrTypeId,
                        SelfActingTypeId = e.SelfActingTypeId,
                        SettlementTypeIds = e.MNPWarningEmailDefinitionSettlementTypes.Select(x=>x.MatterTypeId).ToList(),
                        DischargeTypeIds = e.MNPWarningEmailDefinitionDischargeTypes.Select(x=>x.DischargeTypeId).ToList(),
                        FundingChannelTypeIds = e.MNPWarningEmailDefinitionFundingChannelTypes.Select(x=>x.FundingChannelTypeId).ToList(),
                        Bodies = e.MNPWarningEmailDefinitionBodies.Select(b=>new EmailEntities.MNPWarningEmailDetailsView()
                        {
                            MNPWarningEmailDefinitionBodyId = b.MNPWarningEmailDefinitionBodyId,
                            MNPWarningEmailDefinitionId = b.MNPWarningEmailDefinitionId,
                            WarningNumber = b.WarningNumber,
                            OffsetDays = b.OffsetDays,
                            SendNoReply = true,
                            OtherEmails = b.NotifyOtherEmail,
                            OtherSMS = b.NotifyOtherSMS,
                            SendEmail = b.SendEmail,
                            SendSMS = b.SendSMS,
                            MessageSubject = b.EmailMessageSubject,
                            MessageBody = b.EmailMessageBody,
                            AttachedDocNames = b.AttachedDocNames,
                            SMSBody = b.SMSMessageBody,
                            NotifyBorrower = b.NotifyBorrower,
                            NotifyFileOwner = b.NotifyFileOwner, 
                            NotifyBroker = b.NotifyBroker,
                            NotifyLender = b.NotifyLender,
                            NotifyMortMgr = b.NotifyMortMgr,
                            NotifyOther = b.NotifyOther,
                            NotifyOtherEmail = b.NotifyOtherEmail,
                            NotifyOtherParty = b.NotifyOtherParty,
                            NotifyRelationshipManager = b.NotifyRelationshipManager, 
                            NotifySecondaryContact  = b.NotifySecondaryContact, 
                            NotifyStandard = false,
                        }).ToList()
                    }
                ).ToList();
            return definitions;
        }



        //public List<MNPWarningEmailDetail> SendAllMNPWarningEmails()
        //{

        //    //List<MNPWarningEmailDetail> emailList = new List<MNPWarningEmailDetail>();
        //    //foreach(var item in queue)
        //    //{
        //    //}
        //    //return emailList;
        //}

        public void GetAndSendMNPWarningEmails(EmailEntities.MNPWarningEmailQueueView item, DateTime startDate, ref List<EmailEntities.MNPWarningEmailDetail> log, bool isTesting = false)
        {
            List<int> completeDisplayStatuses = new List<int>() { (int)DisplayStatusTypeEnum.Default, (int)DisplayStatusTypeEnum.Display };
            List<int> completeCompStatuses = new List<int>() { (int)MatterWFComponentStatusTypeEnum.Complete };

            IQueryable<Matter> matchingMatters = context.Matters
                .Where(x=>
                //(!item.LenderId.HasValue || x.LenderId == item.LenderId.Value) &&
                //(!item.MortMgrId.HasValue || x.MortMgrId == item.MortMgrId.Value) &&
                x.MatterGroupTypeId == (int)MatterGroupTypeEnum.NewLoan && 
                !x.StopMNPReminders
                && x.FileOpenedDate > startDate 
                && !x.SettlementScheduleId.HasValue
                && !x.MatterWFComponents.Any(m=>m.WFComponentId == (int)WFComponentEnum.DocsReturned && completeDisplayStatuses.Contains(m.DisplayStatusTypeId) && completeCompStatuses.Contains(m.WFComponentStatusTypeId))
                && (x.MatterStatusTypeId == (int)MatterStatusTypeEnum.InProgress || x.MatterStatusTypeId == (int)MatterStatusTypeEnum.OnHold)
                && !x.IsTestFile && x.StopAutomatedEmails != true && !x.Settled);

            //filter based on matching parameters first

            #region matter param filters

            if (item.LenderId.HasValue)
            {
                matchingMatters = matchingMatters.Where(l => l.LenderId == item.LenderId.Value);
                if(item.LenderId == 182)
                {
                    matchingMatters = matchingMatters.Where(l => !l.LoanTypeId.HasValue);
                }
            }

            if (item.MortMgrId.HasValue)
            {
                matchingMatters = matchingMatters.Where(l => l.MortMgrId.HasValue && l.MortMgrId == item.MortMgrId.Value);
            }

            if (item.MortMgrTypeId.HasValue)
            {
                matchingMatters = matchingMatters.Where(l => l.MortMgrId.HasValue && l.MortMgr.MortMgrTypeId == item.MortMgrTypeId.Value);
            }

            if (item.MatterTypeId.HasValue)
            {
                matchingMatters = matchingMatters.Where(x => x.MatterGroupTypeId == item.MatterTypeId.Value);
            }

            if (item.SettlementTypeIds.Any())
            {
                matchingMatters = matchingMatters.Where(f => f.MatterMatterTypes.Select(m=>m.MatterTypeId).Intersect(item.SettlementTypeIds).Any());
            }

            if (item.DischargeTypeIds.Any())
            {
                matchingMatters = matchingMatters.Where(f => f.MatterDischarge != null && item.DischargeTypeIds.Contains(f.MatterDischarge.DischargeTypeId));
            }

            if (item.FundingChannelTypeIds.Any())
            {
                matchingMatters = matchingMatters.Where(f => f.FundingChannelTypeId.HasValue && item.FundingChannelTypeIds.Contains(f.FundingChannelTypeId.Value));
            }

            if (item.FacilityTypeId.HasValue)
            {
                //stub - do the conversion to loanpurpose thingo text, yuck.
                switch (item.FacilityTypeId)
                {
                    case (int)FacilityTypeEnum.Fixed:
                        matchingMatters = matchingMatters.Where(f => f.MatterLoanAccounts.Any(l => !l.LoanDescription.ToUpper().Contains("IO") && l.LoanDescription.ToUpper().Contains("FIXED")));
                        break;
                    case (int)FacilityTypeEnum.FixedInterestOnly:
                        matchingMatters = matchingMatters.Where(f => f.MatterLoanAccounts.Any(l => l.LoanDescription.ToUpper().Contains("FIXED IO")));
                        break;
                    case (int)FacilityTypeEnum.InterestOnly:
                        matchingMatters = matchingMatters.Where(f => f.MatterLoanAccounts.Any(l => l.LoanDescription.ToUpper().Contains("IO") && !l.LoanDescription.ToUpper().Contains("FIXED")) );
                        break;
                    case (int)FacilityTypeEnum.Variable:
                        matchingMatters = matchingMatters.Where(f => f.MatterLoanAccounts.Any(l => !l.LoanDescription.ToUpper().Contains("IO") && !l.LoanDescription.ToUpper().Contains("FIXED")));
                        break;

                }
            }

            if (item.SelfActingTypeId.HasValue)
            {
                if (item.SelfActingTypeId == (int)Enums.SelfActingTypeEnum.NonSelfActing)
                {
                    matchingMatters = matchingMatters.Where(f => f.MatterDischarge != null && f.MatterDischarge.IsSelfActing == false);
                }
                else
                {
                    matchingMatters = matchingMatters.Where(f => f.MatterDischarge != null && f.MatterDischarge.IsSelfActing == true);
                }
            }

            #endregion 

            

            foreach(var body in item.Bodies.OrderByDescending(o=>o.WarningNumber))
            {
                var newQry = matchingMatters.Where(m => !m.IsTestFile);
                SendMNPWarningEmails(newQry, item, body, ref log, isTesting: isTesting);
            }


        }

        private void SendMNPWarningEmails(IQueryable<Matter> possibleMatters, EmailEntities.MNPWarningEmailQueueView definition, EmailEntities.MNPWarningEmailDetailsView body, ref List<EmailEntities.MNPWarningEmailDetail> log, bool isTesting = false)
        {
            DateTime cutoff = DateTime.Now.Date.AddDays(-1 * body.OffsetDays);
            DateTime today = DateTime.Now.Date;
            var subQry = possibleMatters.Where(x => !x.MatterMNPWarningEmailLogs.Any(m => m.WarningDate > today));


            switch ((MNPEmailTriggerTypeEnum)definition.MNPTriggerTypeId)
            {
                case MNPEmailTriggerTypeEnum.DaysFromMatterCreated:
                    subQry = subQry.Where(x => x.FileOpenedDate <= cutoff);
                    break;
                case MNPEmailTriggerTypeEnum.DisclosureDate:
                    subQry = subQry.Where(x => x.DisclosureDate.HasValue && x.DisclosureDate <= cutoff);
                    break;
                case MNPEmailTriggerTypeEnum.LEXPDate:
                    subQry = subQry.Where(x => x.LoanExpiryDate.HasValue && x.LoanExpiryDate <= cutoff);
                    break;
                case MNPEmailTriggerTypeEnum.ValuationExpiry:
                    subQry = subQry.Where(x => x.MatterSecurities.Any(s => !s.Deleted && s.ValuationExpiryDate.HasValue && s.ValuationExpiryDate <= cutoff));
                    break;
                case MNPEmailTriggerTypeEnum.LMIExpiryDate:
                    subQry = subQry.Where(x => x.LMIExpiryDate.HasValue && x.LMIExpiryDate <= cutoff);
                    break;
                case MNPEmailTriggerTypeEnum.DateOfValuation:
                    subQry = subQry.Where(x => x.MatterSecurities.Any(s => !s.Deleted && s.ValuationDate.HasValue && s.ValuationDate <= cutoff));
                    break;
                case MNPEmailTriggerTypeEnum.ManualDate:
                    subQry = subQry.Where(x => x.ManualExpirationDate.HasValue && x.ManualExpirationDate <= cutoff);
                    break;
            }

            subQry = subQry.Where(x => x.MatterMNPWarningEmailLogs.Where(t => t.MNPWarningEmailDefinition.MNPEmailTriggerTypeId == definition.MNPTriggerTypeId).Count() < body.WarningNumber);
            
            var matterIds = subQry.Select(x => x.MatterId).ToList();

            Console.WriteLine($"-Found {matterIds.Count} matters to email");
            
            foreach (var matter in matterIds)
            {
                List<string> recipients = new List<string>();
                List<int> compStatuses = new List<int>() { (int)MatterWFComponentStatusTypeEnum.InProgress, (int)MatterWFComponentStatusTypeEnum.Starting, (int)MatterWFComponentStatusTypeEnum.OnHold };
                List<int> dispStatuses = new List<int>() { (int)DisplayStatusTypeEnum.Default, (int)DisplayStatusTypeEnum.Display };

                if (!isTesting)
                {
                    SendMNPWarningForMatter(matter, body, ref recipients);
                    context.MatterMNPWarningEmailLogs.Add(new MatterMNPWarningEmailLog()
                    {
                        MatterId = matter,
                        MNPWarningEmailDefinitionId = definition.MNPWarningEmailDefinitionId,
                        WarningDate = DateTime.Now,
                        SentTo = string.Join(", ", recipients)
                    });
                }
                var mt = context.Matters.FirstOrDefault(m => m.MatterId == matter);
                log.Add(new EmailEntities.MNPWarningEmailDetail()
                {
                    MatterId = matter,
                    DisclosureDate = mt.DisclosureDate ?? new DateTime(),
                    LoanExpiryDate = mt.LoanExpiryDate ?? new DateTime(),
                    ActiveMilestones = String.Join(" / ", mt.MatterWFComponents.Where(c=>compStatuses.Contains(c.WFComponentStatusTypeId) && dispStatuses.Contains(c.DisplayStatusTypeId)).Select(x => x.WFComponent.WFComponentName)),
                    FileOpenedDate = mt.FileOpenedDate,
                    MatterStatus = mt.MatterStatusType.MatterStatusTypeName,
                    FileOwner = mt.User.Fullname
                });
            }
            if(!isTesting) context.SaveChanges();

        }


        private EmailEntities.ReminderMessage CreateReminderMessageFromMNPDetails(int matterId, EmailEntities.MNPWarningEmailDetailsView emailDetails)
        {
            EmailEntities.ReminderMessage email = new EmailEntities.ReminderMessage()
            {
                SendNoReply = true,
                SendSMS = emailDetails.SendSMS,
                SendEmail = emailDetails.SendEmail,
                AttachedDocNames = emailDetails.AttachedDocNames,
                SMSMessage = emailDetails.SMSBody,
                EmailMessage = emailDetails.MessageBody,
                EmailSubject = emailDetails.MessageSubject,
                NotifyLender = emailDetails.NotifyLender,
                NotifyMortMgr = emailDetails.NotifyLender,
                NotifyBorrower = emailDetails.NotifyBorrower,
                NotifyBroker = emailDetails.NotifyBroker,
                NotifyFileOwner = emailDetails.NotifyFileOwner,
                NotifyOther = emailDetails.NotifyOther,
                NotifyOtherParty = emailDetails.NotifyOtherParty,
                NotifyRelationshipManager = emailDetails.NotifyRelationshipManager,
                NotifySecondaryContacts = emailDetails.NotifySecondaryContact,
                NotifyStandard = false,
                OtherEmails = emailDetails.OtherEmails,
                OtherSMS = emailDetails.OtherSMS
            };
            return email;
       
        }


        private void SendMNPWarningForMatter(int matterId, EmailEntities.MNPWarningEmailDetailsView emailDetails, ref List<string> recipients)
        {
          

      
            var email = CreateReminderMessageFromMNPDetails(matterId, emailDetails);

            var emailModel = BuildEmailPlaceHolderModel(matterId);

            new MatterWFRepository(context).PublicUpdateCCNotificationsForEmailModel(ref emailModel, email);

            //EmailsService.SendAutomatedEmail(email.EmailSubject, email.EmailMessage, emailModel, email.AttachedDocNames, matterId);

            if ((GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_SendWFComponentEmails, context).ToUpper() == DomainConstants.True.ToUpper()
                    && GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_RunMode, context) == DomainConstants.Production) ||
                    GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmails, context)?.ToUpper() == DomainConstants.True.ToUpper())
            {
                   
                email.SendNoReply = true;
                //emailModel.EmailMobiles = new EmailEntities.EmailMobileAddresses() { Other = "george.kogios@msanational.com.au" };

                var noteRep = new Services.NotesRepository(context);

                if (email.SendEmail)
                {
                    recipients.Add(EmailsService.SendAutomatedEmail(email.EmailSubject, email.EmailMessage, emailModel, email.AttachedDocNames, matterId, sendEmail: true, sendSms: false, noReply: true, existingContext: context));

                    try
                    {
                        string noteSubject = email.EmailSubject;
                        string noteBody = email.EmailMessage;
                        EmailsService.ReplaceEmailPlaceHolders(emailModel, ref noteSubject, ref noteBody);

                        noteRep.SaveSimpleNote(matterId, (int)NoteTypeEnum.StatusUpdate, "MNP Warning Email Sent", $"SENT TO: {string.Join(", ", recipients.Where(r=>!string.IsNullOrEmpty(r)))} <p>{noteSubject}</p>{noteBody}", true, false, false, DomainConstants.SystemUserId);
                    }
                    catch(Exception e)
                    {
                        //error saving the note, but more important to save to the log and save the email.
                    }
                }
                if (email.SendSMS)
                { 

                    recipients.Add(EmailsService.SendAutomatedEmail(email.EmailSubject, email.SMSMessage, emailModel, email.AttachedDocNames, matterId, sendEmail: false, sendSms: true, noReply: true, existingContext: context));
                    try
                    {
                        string noteSubject = email.EmailSubject;
                        string noteBody = email.SMSMessage;
                        EmailsService.ReplaceEmailPlaceHolders(emailModel, ref noteSubject, ref noteBody);

                        noteRep.SaveSimpleNote(matterId, (int)NoteTypeEnum.StatusUpdate, "MNP Warning SMS Sent", $"SENT TO: {string.Join(", ", recipients.Where(r => !string.IsNullOrEmpty(r)))} <p>{noteSubject}</p>{noteBody}", true, false, false, DomainConstants.SystemUserId);
                    }
                    catch (Exception e)
                    {
                        //error saving the note, but more important to save to the log and save the email.
                    }
                }

            }
            
        }


        /// <summary>
        /// Builds Email for Self Acting - As per NonSelfActingModel - except all ledger items not only cheques
        /// </summary>
        /// <param name="matterId"></param>
        /// <returns></returns>
        private EmailEntities.EmailModel BuildSelfActingEmailModel(int matterId)
        {
            var em =
            (from m in context.Matters.AsNoTracking()
             where m.MatterId == matterId
             select new
             {
                 m.MatterId,
                 m.LenderRefNo,
                 m.MatterDescription,
                 m.StateId,
                 m.LenderId,
                 m.SettlementSchedule.Email,
                 SettlementDate = (DateTime?) m.SettlementSchedule.SettlementDate,
                 SettlementTime = (TimeSpan?) m.SettlementSchedule.SettlementTime,
                 SettlementVenue = m.SettlementSchedule.SettlementScheduleVenues.FirstOrDefault().SettlementVenue.VenueName,
                 m.SettlementSchedule.SettlementScheduleVenues.FirstOrDefault().SettlementAgent.AgentName,
                 AgentAddress = m.SettlementSchedule.SettlementScheduleVenues.FirstOrDefault().SettlementAgent.StreetAddress,
                 AgentSuburb = m.SettlementSchedule.SettlementScheduleVenues.FirstOrDefault().SettlementAgent.Suburb,
                 AgentState = m.SettlementSchedule.SettlementScheduleVenues.FirstOrDefault().SettlementAgent.State.StateName,

                 Securities = m.MatterSecurities.Where(ms => !ms.Deleted && ms.MatterId == matterId)
                        .Select(s => new { s.MatterSecurityId, s.PostCode, s.State.StateName, s.Suburb, s.StreetAddress }).ToList(),
                 TitleRefs = m.MatterSecurities.Where(x => !x.Deleted && x.MatterId == matterId).SelectMany(tr => tr.MatterSecurityTitleRefs.Select(y => new { y.TitleReference, y.MatterSecurityId })).ToList(),

                 LedgerItems = m.MatterLedgerItems.Where
                    (li => li.PayableByTypeId == (int) Enums.PayableTypeEnum.Borrower && li.PayableToAccountTypeId == (int)AccountTypeEnum.MSA)
                    .Select(l => new { PayableTo = l.PayableToName, l.Amount, l.Description }).ToList(),

                 NonLedgerItems = m.MatterLedgerItems.Where
                 (li => li.PayableByTypeId == (int) Enums.PayableTypeEnum.Borrower && li.PayableToAccountTypeId == (int)AccountTypeEnum.External)
                 .Select(l => new { PayableTo = l.PayableToName, l.Amount, l.Description }).ToList(),
                 RegisteredDocs = m.MatterSecurities.Where(x => !x.Deleted && x.MatterId == matterId).SelectMany(rd => rd.MatterSecurityDocuments.Select(sd => new { sd.MatterSecurityId, sd.RegisteredDocumentType.RegisteredDocumentName, sd.DocumentName })).ToList()
             }).FirstOrDefault();

            if (em == null) return null;

            var model = new EmailEntities.EmailModel
            {
                MatterId = em.MatterId,
                LenderRefNo = em.LenderRefNo,
                SettlementDate = em.SettlementDate.HasValue ? em.SettlementDate.Value.ToString("dd-MMM-yyyy") : null,
                SettlementTime = em.SettlementTime.HasValue ? DateTime.Today.Add(em.SettlementTime.Value).ToString("h:mm tt") : null,
                SettlementVenue = string.IsNullOrEmpty(em.SettlementVenue) ? $"{em.AgentName}, {(EntityHelper.FormatAddressDetails(em.AgentAddress, em.AgentSuburb, em.AgentState, null))}" : em.SettlementVenue,
                EmailAddress = em.Email,
                Subject = $"{em.MatterDescription}:Matter-{em.MatterId}:Ref-{em.LenderRefNo}"
            };

            model.IsSelfActing = true;
            model.BankDetails = GetBankDetailsForEmail(matterId, em.LenderId, em.StateId);

            int secIndex = -1;
            foreach (var sec in em.Securities)
            {
                secIndex++;
                model.SecurityList.Add(new EmailEntities.EmailSecurity { SecurityId = sec.MatterSecurityId, SecurityAddress = EntityHelper.FormatAddressDetails(sec.StreetAddress, sec.Suburb, sec.StateName, string.Empty) });
                model.SecuritiesAppended += EntityHelper.FormatAddressDetails(sec.StreetAddress, sec.Suburb, sec.StateName, sec.PostCode);
                if (em.Securities.Count > 1 && secIndex != em.Securities.Count - 1)
                    model.SecuritiesAppended += ", ";
            }

            var ledgerItems = new List<EmailEntities.EmailLedgerItem>();
            decimal ledgerTotal = 0;
            foreach (var li in em.LedgerItems)
            {
                ledgerTotal += li.Amount;
                ledgerItems.Add(new EmailEntities.EmailLedgerItem
                {
                    PayableTo = li.PayableTo,
                    Amount = li.Amount,
                    Description = li.Description,
                    IsLedgerItem = true
                });
            }

            foreach (var li in em.NonLedgerItems)
            {
                ledgerTotal += li.Amount;
                ledgerItems.Add(new EmailEntities.EmailLedgerItem
                {
                    PayableTo = li.PayableTo,
                    Amount = li.Amount,
                    Description = li.Description,
                    IsLedgerItem = false
                });
            }
            model.LedgerItemsTotal = ledgerTotal.ToString("C");

            model.LedgerItems = BuildLedgerItemsForEmail(ledgerItems, em.LenderId);

            foreach (var doc in em.TitleRefs)
            {
                model.SecurityDocs.Add(new EmailEntities.EmailSecurityDoc { SecurityId = doc.MatterSecurityId, DocumentName = $"Certificate of Title {doc.TitleReference}" });
            }
            foreach (var doc in em.RegisteredDocs)
            {
                model.SecurityDocs.Add(new EmailEntities.EmailSecurityDoc { SecurityId = doc.MatterSecurityId, DocumentName = $"{doc.RegisteredDocumentName} {doc.DocumentName}" });
            }
            return model;
        }

        private List<EmailEntities.EmailLedgerItem> BuildLedgerItemsForEmail(List<EmailEntities.EmailLedgerItem> ledgerItems, int lenderId)
        {
            var emailLedgerItems = new List<EmailEntities.EmailLedgerItem>();
          
            if (lenderId == DomainConstants.AdvantedgeLenderId)
            {
                var ledgerGrouped = ledgerItems.Where(x=> x.Description.ToUpper() != "PAYOUT TO LENDER")
                .GroupBy(l => l.PayableTo)
                .Select(s => new EmailEntities.EmailLedgerItem { PayableTo = s.FirstOrDefault()?.PayableTo, AmountString = s.Sum(x => x.Amount.Value).ToString("C") }).ToList();

                emailLedgerItems.AddRange(ledgerGrouped);
                
                foreach (var li in ledgerItems.Where(x=> x.Description.ToUpper() == "PAYOUT TO LENDER"))
                {
                    emailLedgerItems.Add(new EmailEntities.EmailLedgerItem { PayableTo = li.PayableTo, AmountString = li.Amount.Value.ToString("C") });
                }
            }
            else
            {
                var ledgerGrouped = ledgerItems
                .GroupBy(l => l.PayableTo)
                .Select(s => new EmailEntities.EmailLedgerItem { PayableTo = s.First().PayableTo, AmountString = s.Sum(x => x.Amount.Value).ToString("C") }).ToList();

                emailLedgerItems.AddRange(ledgerGrouped);
            }

            return emailLedgerItems;
        }

        private EmailEntities.EmailBankDetails GetBankDetailsForEmail(int matterId, int stateId, int lenderId)
        {
            var em = (from m in context.TrustAccountLookups
                     select new GeneralCustomEntities.BestMatchForCriteria
                     {
                         ID = m.TrustAccount.TrustAccountId,
                         LenderId = lenderId,
                         StateId = stateId
                     }).ToList();

            if (em == null) return null;

            var acct = CommonMethods.GetBestValueFromSelectedQuery(null, null, em.First().StateId, em.First().LenderId, null, em);
            if (acct != null)
            {
                var det = context.TrustAccounts.FirstOrDefault(x => x.TrustAccountId == acct.Value);
                return new EmailEntities.EmailBankDetails
                {
                    BankAcctName = det.AccountName,
                    BankAcctNo = det.AccountNo,
                    BankBSB = det.BSB,
                    BankName = det.Bank
                };
            }
            return null; 
        }

        /// <summary>
        /// Builds Email for Ready to Book Email
        /// </summary>
        /// <param name="matterId"></param>
        /// <returns></returns>
        public EmailEntities.EmailModel BuildReadyToBookEmailModel(int matterId, int wfComponentId, int? matterWFComponentId, List<OutstandingItemView> outstandings = null) //added optional outstandings to include in this
        {
            var em =
           (from m in context.Matters.AsNoTracking()
            where m.MatterId == matterId
            select new
            {
                m.MatterId,
                m.MatterGroupTypeId,
                m.MortMgrId,
                m.User.DisplayInitials,
                FileOwnerName = m.User.Fullname,
                FileOwnerNumber = m.User.Phone,
                FileOwnerEmail = m.User.Email,
                m.Lender.SettlementClearanceDays,
                m.MortMgr.MortMgrName,
                m.Lender.LenderName,
                m.LenderRefNo,
                m.MatterDescription,
                m.StateId,
                m.LenderId,
                IsPurchase = m.MatterMatterTypes.Any(x=> x.MatterTypeId == (int)MatterTypeEnum.Purchase),
                Securities = m.MatterSecurities.Where(ms => !ms.Deleted && ms.MatterId == matterId)
                       .Select(s => new { s.MatterSecurityId, s.PostCode, s.State.StateName, s.StateId, s.Suburb, s.StreetAddress }).ToList(),
                IsPaper = m.MatterSecurities.Any(ms => !ms.Deleted && ms.MatterId == matterId && ms.SettlementTypeId == (int)SettlementTypeEnum.Paper),
                LedgerItems = m.MatterLedgerItems.Where(x => x.PayableByAccountTypeId == (int) AccountTypeEnum.Trust 
                                        && x.MatterLedgerItemStatusTypeId != (int)MatterLedgerItemStatusTypeEnum.Cancelled
                                        && !x.ParentMatterLedgerItemId.HasValue
                                        && x.TransactionTypeId != (int) TransactionTypeEnum.Invoice)
                                .Select(l => new { l.PayableToTypeId, l.PayableToName, l.Amount,l.Description}).ToList(),
                 MatterTypeIds = m.MatterMatterTypes.Select(t=>t.MatterTypeId).ToList(),
                 FastRefiDetails = m.MatterFastRefiDetails.Select(f=>new { f.EFT_AccountName, f.EFT_AccountNo, f.BalanceExpiryDate }).ToList()


            }).FirstOrDefault();

            if (em == null) return null;


            string fastRefiDetails = "";
            if (em.FastRefiDetails?.Any() == true)
            {
                string fastRefiTypeLabel = em.MatterTypeIds.Any(m => m == (int)MatterTypeEnum.RapidRefinance) ? "Rapid Refinance" : "FAST Refinance";
                fastRefiDetails = $"<p><b><u>{fastRefiTypeLabel} Balance Expiry Dates</u></b></p>Please note should any dates displayed expire our office will require a screenshot of the new account balance noting the amount and balance date.";
                foreach (var detail in em.FastRefiDetails)
                {
                    fastRefiDetails += $"<p>Account {detail.EFT_AccountNo} / {detail.EFT_AccountName} - {(detail.BalanceExpiryDate.HasValue ? "<b>EXPIRY DATE: </b>" + detail.BalanceExpiryDate.Value.ToString("dd/MM/yyyy") : "<b>Please provide a current balance screenshot ensuring the date is included</b>")} </p>";
                }
            }

            var feeRep = new FeesRepository(context);
            var aRep = new AccountsRepository(context);
            var model = new EmailEntities.EmailModel
            {
                MatterId = em.MatterId,
                IsPurchase = em.IsPurchase,
                FileOwnerEmail = em.FileOwnerEmail,
                FileOwnerInitials = em.DisplayInitials ?? GetCapitals(em.FileOwnerName),
                FileOwnerNumber = em.FileOwnerNumber,
                FileOwnerName = em.FileOwnerName,
                LenderRefNo = em.LenderRefNo,
                MortMgrName = em.MortMgrName ?? null,
                LenderName = em.LenderName,
                LenderId = em.LenderId,
                FastRefiDetails = fastRefiDetails,
                BookingDetails = new EmailEntities.EmailReadyToBookDetails
                {
                    BankChequeFee = feeRep.GetFeeAmount(DomainConstants.Fees_BankCheque, DateTime.Today, em.MatterGroupTypeId, em.LenderId, em.MortMgrId, em.StateId),
                    SettlementCancellationFee = feeRep.GetFeeAmount(DomainConstants.Fees_SettlementCancellation, DateTime.Today, em.MatterGroupTypeId, em.LenderId, em.MortMgrId, em.StateId),
                    SettlementClearanceDays = em.SettlementClearanceDays,
                    LoanAmount = aRep.GetLoanAmount(em.MatterId),
                    LenderRetainedItems = context.MatterFinanceRetainedByLenders.Where(x => x.MatterId == em.MatterId)
                                          .Select(x=> new EmailEntities.EmailLedgerItem { Description = x.Description, Amount = x.Amount}).ToList(),
                    ExpectedFundsItems = context.MatterLedgerItems.Where(x => x.MatterId == matterId 
                                                    && x.MatterLedgerItemStatusTypeId != (int)MatterLedgerItemStatusTypeEnum.Cancelled
                                                    && !x.ParentMatterLedgerItemId.HasValue
                                                    && x.PayableToAccountTypeId == (int) AccountTypeEnum.Trust)
                                         .Select(x=> new EmailEntities.EmailLedgerItem { Description = x.Description, Amount = x.Amount }).ToList(),
                    IsPaper = em.IsPaper
                },                
                
                Subject = $"{em.MatterDescription} : Matter- {(em.DisplayInitials ?? GetCapitals(em.FileOwnerName))} {em.MatterId} : Ref- {em.LenderRefNo}"
            };

            if (wfComponentId == (int)WFComponentEnum.FollowupOutstandingRequirements && matterWFComponentId.HasValue)
            {
                if (outstandings == null)
                {
                    model.BookingDetails.OutstandingItems = context.MatterWFOutstandingReqItems
                        .Where(x => x.MatterWFOutstandingReq.MatterWFComponentId == matterWFComponentId.Value && !x.Resolved & !x.IsDeleted)
                        .Select(y => new OutstandingItemView
                        {
                            ResponsiblePartyId = y.OutstandingResponsiblePartyTypeId.HasValue?y.OutstandingResponsiblePartyTypeId.Value : 0,
                            MatterWFSecurityId = y.MatterWFSecurityId,
                            OutstandingItemName = y.OutstandingItemName,
                            IssueDetails = y.IssueDetails
                        })
                        .ToList();
                }
                else
                {
                    model.BookingDetails.OutstandingItems = outstandings;
                }

                    foreach (var securitySpecificOutstanding in model.BookingDetails.OutstandingItems.Where(x=> x.MatterWFSecurityId.HasValue))
                {
                    var wfSecurity = context.MatterWFProcDocsSecurities.FirstOrDefault(x => x.MatterWFProcDocsSecurityId == securitySpecificOutstanding.MatterWFSecurityId);
                    if (wfSecurity != null)
                    {
                        securitySpecificOutstanding.SecurityDetails = string.Format("({0})", EntityHelper.FormatAddressDetails
                                        (wfSecurity.StreetAddress, wfSecurity.Suburb, wfSecurity.State.StateName, null).TrimEnd(' '));
                    }                    
                }

                var outst = context.MatterWFOutstandingReqs.FirstOrDefault(x => x.MatterWFComponentId == matterWFComponentId);
                if (outst != null)
                {
                    if (outst.EmailSent.HasValue && outst.EmailSent.Value)
                    {
                        model.Subject = "FOLLOW UP: " + model.Subject;
                    }
                    model.BookingDetails.NumberOfFreeCheques = outst.NumberOfFreeCheques;
                    //model.BookingDetails.SettlementClearanceDays = outst.SettlementClearanceDays;
                }
            }


            int secIndex = -1;
            foreach (var sec in em.Securities)
            {
                secIndex++;
                model.SecurityList.Add(new EmailEntities.EmailSecurity {

                    SecurityId = sec.MatterSecurityId, SecurityAddress = EntityHelper.FormatAddressDetails(sec.StreetAddress, sec.Suburb, sec.StateName, string.Empty), StateId = sec.StateId });
                model.SecuritiesAppended += EntityHelper.FormatAddressDetails(sec.StreetAddress, sec.Suburb, sec.StateName, sec.PostCode);
                if (em.Securities.Count > 1 && secIndex != em.Securities.Count - 1)
                    model.SecuritiesAppended += ", ";
            }

            var ledgerItems = new List<EmailEntities.EmailLedgerItem>();
            decimal ledgerTotal = 0;
            foreach (var li in em.LedgerItems)
            {
                ledgerTotal += li.Amount;
                ledgerItems.Add(new EmailEntities.EmailLedgerItem
                {
                    PayableTo = li.PayableToName,
                    Amount = li.Amount,
                    AmountString = li.Amount.ToString("C"),
                    Description = li.Description
                });
            }

            model.LedgerItemsTotal = ledgerTotal.ToString("C");
            model.LedgerItems = ledgerItems;
          
            return model;
        }


        /// <summary>
        /// Build funds available Email Model
        /// </summary>
        /// <param name="matterId">The <see cref="Matter.MatterId"/></param>
        /// <param name="wfComponentId">The <see cref="MatterWFComponent.WFComponentId"/></param>
        /// <param name="matterWFComponentId">The <see cref="MatterWFComponent.MatterWFComponentId"/></param>
        /// <param name="outstandings">The Outstandings list</param>
        /// <returns>The new email model</returns>
        public EmailEntities.EmailModel BuildFundsAvailableEmailModel(int matterId, int wfComponentId, int? matterWFComponentId, List<OutstandingItemView> outstandings = null) //added optional outstandings to include in this
        {
            var em =
           (from m in context.Matters.AsNoTracking()
            where m.MatterId == matterId
            select new
            {
                m.MatterId,
                m.MatterGroupTypeId,
                m.MortMgrId,
                m.MortMgr.MortMgrName,
                m.LenderRefNo,
                m.MatterDescription,
                m.StateId,
                m.LenderId,
                m.Lender.LenderName,
                IsPurchase = m.MatterMatterTypes.Any(x => x.MatterTypeId == (int)MatterTypeEnum.Purchase),
                Securities = m.MatterSecurities.Where(ms => !ms.Deleted && ms.MatterId == matterId)
                       .Select(s => new { s.MatterSecurityId, s.PostCode, s.State.StateName, s.Suburb, s.StreetAddress }).ToList(),
                IsPaper = m.MatterSecurities.Any(ms => !ms.Deleted && ms.MatterId == matterId && ms.SettlementTypeId == (int)SettlementTypeEnum.Paper),
                LedgerItems = m.MatterLedgerItems.Where(x => x.PayableByAccountTypeId == (int)AccountTypeEnum.Trust
                                        && x.MatterLedgerItemStatusTypeId != (int)MatterLedgerItemStatusTypeEnum.Cancelled
                                        && !x.ParentMatterLedgerItemId.HasValue
                                        && x.TransactionTypeId != (int)TransactionTypeEnum.Invoice)
                                .Select(l => new { l.PayableToTypeId, l.PayableToName, l.Amount, l.Description }).ToList(),

            }).FirstOrDefault();

            if (em == null) return null;

            var feeRep = new FeesRepository(context);
            var aRep = new AccountsRepository(context);
            var model = new EmailEntities.EmailModel
            {
                MatterId = em.MatterId,
                IsPurchase = em.IsPurchase,
                LenderRefNo = em.LenderRefNo,
                MortMgrName = em.MortMgrName ?? null,
                LenderName = em.LenderName,
                LenderId = em.LenderId,
                BookingDetails = new EmailEntities.EmailReadyToBookDetails
                {
                    BankChequeFee = feeRep.GetFeeAmount(DomainConstants.Fees_BankCheque, DateTime.Today, em.MatterGroupTypeId, em.LenderId, em.MortMgrId, em.StateId),
                    SettlementCancellationFee = feeRep.GetFeeAmount(DomainConstants.Fees_SettlementCancellation, DateTime.Today, em.MatterGroupTypeId, em.LenderId, em.MortMgrId, em.StateId),
                    LoanAmount = aRep.GetLoanAmount(em.MatterId),
                    LenderRetainedItems = context.MatterFinanceRetainedByLenders.Where(x => x.MatterId == em.MatterId)
                                          .Select(x => new EmailEntities.EmailLedgerItem { Description = x.Description, Amount = x.Amount }).ToList(),
                    ExpectedFundsItems = context.MatterLedgerItems.Where(x => x.MatterId == matterId
                                                    && x.MatterLedgerItemStatusTypeId != (int)MatterLedgerItemStatusTypeEnum.Cancelled
                                                    && !x.ParentMatterLedgerItemId.HasValue
                                                    && x.PayableToAccountTypeId == (int)AccountTypeEnum.Trust)
                                         .Select(x => new EmailEntities.EmailLedgerItem { Description = x.Description, Amount = x.Amount }).ToList(),
                    IsPaper = em.IsPaper
                },

                Subject = $"{em.MatterDescription}  - MSA Reference : {em.MatterId} - {em.LenderName} Reference : {em.LenderRefNo}"
            };

            if (wfComponentId == (int)WFComponentEnum.FollowupOutstandingRequirements && matterWFComponentId.HasValue)
            {
                if (outstandings == null)
                {
                    model.BookingDetails.OutstandingItems = context.MatterWFOutstandingReqItems
                        .Where(x => x.MatterWFOutstandingReq.MatterWFComponentId == matterWFComponentId.Value && !x.Resolved & !x.IsDeleted)
                        .Select(y => new OutstandingItemView
                        {
                            ResponsiblePartyId = y.OutstandingResponsiblePartyTypeId.HasValue ? y.OutstandingResponsiblePartyTypeId.Value : 0,
                            MatterWFSecurityId = y.MatterWFSecurityId,
                            OutstandingItemName = y.OutstandingItemName,
                            IssueDetails = y.IssueDetails
                        })
                        .ToList();
                }
                else
                {
                    model.BookingDetails.OutstandingItems = outstandings;
                }

                foreach (var securitySpecificOutstanding in model.BookingDetails.OutstandingItems.Where(x => x.MatterWFSecurityId.HasValue))
                {
                    var wfSecurity = context.MatterWFProcDocsSecurities.FirstOrDefault(x => x.MatterWFProcDocsSecurityId == securitySpecificOutstanding.MatterWFSecurityId);
                    if (wfSecurity != null)
                    {
                        securitySpecificOutstanding.SecurityDetails = string.Format("({0})", EntityHelper.FormatAddressDetails
                                        (wfSecurity.StreetAddress, wfSecurity.Suburb, wfSecurity.State.StateName, null).TrimEnd(' '));
                    }
                }

                var outst = context.MatterWFOutstandingReqs.FirstOrDefault(x => x.MatterWFComponentId == matterWFComponentId);
                if (outst != null)
                {
                    if (outst.EmailSent.HasValue && outst.EmailSent.Value)
                    {
                        model.Subject = "FOLLOW UP: " + model.Subject;
                    }
                    model.BookingDetails.NumberOfFreeCheques = outst.NumberOfFreeCheques;
                    model.BookingDetails.SettlementClearanceDays = outst.SettlementClearanceDays;
                }
            }


            int secIndex = -1;
            foreach (var sec in em.Securities)
            {
                secIndex++;
                model.SecurityList.Add(new EmailEntities.EmailSecurity { SecurityId = sec.MatterSecurityId, SecurityAddress = EntityHelper.FormatAddressDetails(sec.StreetAddress, sec.Suburb, sec.StateName, string.Empty) });
                model.SecuritiesAppended += EntityHelper.FormatAddressDetails(sec.StreetAddress, sec.Suburb, sec.StateName, sec.PostCode);
                if (em.Securities.Count > 1 && secIndex != em.Securities.Count - 1)
                    model.SecuritiesAppended += ", ";
            }

            var ledgerItems = new List<EmailEntities.EmailLedgerItem>();
            decimal ledgerTotal = 0;
            foreach (var li in em.LedgerItems)
            {
                ledgerTotal += li.Amount;
                ledgerItems.Add(new EmailEntities.EmailLedgerItem
                {
                    PayableTo = li.PayableToName,
                    Amount = li.Amount,
                    AmountString = li.Amount.ToString("C"),
                    Description = li.Description
                });
            }

            model.LedgerItemsTotal = ledgerTotal.ToString("C");
            model.LedgerItems = ledgerItems;

            return model;
        }


        public IEnumerable<EmailEntities.MilestoneEmailToAddressView> GetMilestoneEmailToAddresses()
        {
            return context.MilestoneEmailToAddresses.AsNoTracking()
            .Select(s => new EmailEntities.MilestoneEmailToAddressView
            {
                Id = s.MilestoneEmailToAddressId,
                LenderId = s.LenderId,
                LenderName = s.Lender.LenderName,
                MatterTypeId = s.MatterTypeId,
                MatterTypeName = s.MatterType.MatterTypeName,
                WFComponentId = s.WFComponentId,
                WFComponentName = s.WFComponent.WFComponentName,
                Email = s.Email,
                UpdatedById = s.UpdatedByUserId,
                UpdatedByUserName = s.User.Username,
                UpdatedDate = s.UpdatedDate
            })
            .ToList();
        }

        private IEnumerable<EmailEntities.ReminderMessage> GetWFComponentEmails(IQueryable<WFComponentEmail> emails)
        {
            return emails
                .Select(s =>
                new
                {
                    s.WFComponentEmailId,
                    s.WFComponentId,
                    s.WFComponent.WFComponentName,
                    s.MilestoneEmailTriggerTypeId,
                    s.MilestoneEmailTriggerType.MilestoneEmailTriggerTypeName,
                    s.MatterTypeId,
                    s.MatterType.MatterTypeName,
                    s.StateId,
                    s.State.StateName,
                    s.LenderId,
                    s.Lender.LenderName,
                    s.MortMgrId,
                    s.MortMgr.MortMgrName,
                    s.SendMailMessage,
                    s.SendSMSMessage,
                    s.UseLenderSignature,
                    s.UseMortMgrSignature,
                    s.MortMgr.MortMgrSignatureDirectory,
                    s.Lender.LenderSignatureDirectory,
                    
                    s.EmailMessageBody,
                    s.EmailMessageSubject,
                    s.SMSMessageBody,
                    CustomCCEmail = s.CCEmail,
                    CustomBCCEmail = s.BCCEmail,
                    s.ConvertAttachmentsToPDF,
                    SettlementTypeNames = s.WFComponentEmailSettlementTypes.OrderBy(x => x.MatterTypeId).Select(x => x.MatterType.MatterTypeName),
                    DischargeTypeNames = s.WFComponentEmailDischargeTypes.OrderBy(x => x.DischargeTypeId).Select(x => x.DischargeType.DischargeTypeName),
                    FundingChannelTypeNames = s.WFComponentEmailFundingChannelTypes.OrderBy(x => x.FundingChannelTypeId).Select(x => x.FundingChannelType.FundingChannelTypeName),

                    LoanTypeNames = s.WFComponentEmailLoanTypes.OrderBy(x => x.LoanTypeId).Select(x => x.LoanType.LoanTypeName),
                    SelfActingType = s.SelfActingTypeId.HasValue ? ((SelfActingTypeEnum)s.SelfActingTypeId).ToString() : "- Either -",
                    s.NotifyStandardRecipients,
                    s.NotifyOtherParty,
                    s.NotifyOtherEmail,
                    s.NotifyOtherSMS,
                    s.NotifyBroker,
                    s.NotifyFileOwner,
                    s.NotifyRelationshipManager,
                    s.NotifyLender,
                    s.NotifyMortMgr,
                    s.NotifyBorrower,
                    s.NotifyOther,
                    s.NotifySecondaryContact,
                    s.Enabled,
                    s.UpdatedByUserId,
                    s.User.Username,
                    s.UpdatedDate,
                    s.AttachedDocNames,
                    s.SendNoReply,
                    MortMgrTypeName = s.MortMgrTypeId.HasValue ? s.MortMgrType.MortMgrTypeName : "- None Selected -",
                    s.IsPopupEmail,
                    LenderDocs = s.WFComponentEmailLenderDocuments.Where(d=>!d.LenderDocumentMaster.Deleted).Select(d => new { d.LenderDocumentMaster.LenderId, d.LenderDocumentMasterId, DocVersion = d.LenderDocumentMaster.LenderDocumentVersions.Select(v=>new { v.LenderDocumentVersionId, v.DocType, v.DocumentName, v.IsLatestVersion }).FirstOrDefault(v=>v.IsLatestVersion) })
                })
                .OrderByDescending(o => o.Enabled).ThenBy(o => o.EmailMessageSubject)
                .ToList()
                .Select(s2 => new EmailEntities.ReminderMessage
                {
                    Id = s2.WFComponentEmailId,
                    WFComponentId = s2.WFComponentId,
                    WFComponentName = s2.WFComponentName,
                    EmailMessage = s2.EmailMessageBody,
                    SMSMessage = s2.SMSMessageBody,
                    EmailTriggerType = s2.MilestoneEmailTriggerTypeName,
                    EmailTriggerTypeId = s2.MilestoneEmailTriggerTypeId,
                    ApplyCustomSignature = s2.UseMortMgrSignature || s2.UseLenderSignature,
                    SignaturePath = s2.UseMortMgrSignature ? s2.MortMgrSignatureDirectory : s2.UseLenderSignature ? s2.LenderSignatureDirectory : null, 
                    LenderId = s2.LenderId,
                    LenderName = s2.LenderName ?? "-- Any Lender --",
                    MatterTypeName = s2.MatterTypeName ?? "-- Any Matter Type --",
                    MatterTypeId = s2.MatterTypeId,
                    MortMgrName = s2.MortMgrName ?? "-- Any Mort Mgr --",
                    SettlementTypeNames = s2.SettlementTypeNames.Any() ? String.Join(", ", s2.SettlementTypeNames) : "-- Any Settlement Type --",
                    DischargeTypeNames = s2.DischargeTypeNames.Any() ? String.Join(", ", s2.DischargeTypeNames) : "-- Any Discharge Type --",
                    FundingChannelTypeNames = s2.FundingChannelTypeNames.Any() ? String.Join(", ", s2.FundingChannelTypeNames) : "-- Any Channel --",
                    LoanTypeNames = s2.LoanTypeNames.Any() ? String.Join(", ", s2.LoanTypeNames) : "-- Any Loan Type --",
                    CustomBCCEmail = s2.CustomBCCEmail,
                    CustomCCEmail = s2.CustomCCEmail,
                    ConvertAttachmentsToPDF = s2.ConvertAttachmentsToPDF,
                    MortMgrTypeName = s2.MortMgrTypeName,
                    SelfActingType = s2.SelfActingType,
                    MortMgrId = s2.MortMgrId,
                    SendEmail = s2.SendMailMessage,
                    SendSMS = s2.SendSMSMessage,
                    StateId = s2.StateId,
                    StateName = s2.StateName ?? "-- Any --",
                    EmailSubject = s2.EmailMessageSubject,
                    AttachedDocNames = s2.AttachedDocNames,
                    NotifyBroker = s2.NotifyBroker,
                    NotifyOtherParty = s2.NotifyOtherParty,
                    NotifyOther = s2.NotifyOther,
                    NotifyMortMgr = s2.NotifyMortMgr,
                    NotifyRelationshipManager = s2.NotifyRelationshipManager,
                    NotifySecondaryContacts = s2.NotifySecondaryContact,
                    NotifyLender = s2.NotifyLender,
                    NotifyBorrower = s2.NotifyBorrower,
                    NotifyFileOwner = s2.NotifyFileOwner,
                    NotifyStandard = s2.NotifyStandardRecipients,
                    OtherEmails = s2.NotifyOtherEmail,
                    OtherSMS = s2.NotifyOtherSMS, 
                    IsEnabled = s2.Enabled, 
                    SendNoReply = s2.SendNoReply,
                    UpdatedBy = s2.Username,
                    UpdatedById = s2.UpdatedByUserId,
                    UpdatedDate = s2.UpdatedDate,
                    IsPopupEmail = s2.IsPopupEmail,
                    LenderDocs = s2.LenderDocs.Select(d=>new EmailEntities.LenderDocsToAttach() { LenderDocumentMasterId = d.LenderDocumentMasterId, DocName = d.DocVersion.DocumentName, DocType = d.DocVersion.DocType, LenderDocumentVersionId = d.DocVersion.LenderDocumentVersionId, LenderId = d.LenderId  }).ToList()
                    
                }
                ).ToList();
        }

        #region dMail


        public int AddTemplate(string subject, string body, int currentUserId, int teamTypeId)
        {
            DateTime currentDateTime = DateTime.UtcNow;

            dMailTemplate newTemplate = new dMailTemplate()
            {
                TemplateSubject = subject,
                TemplateBody = body,
                CreatedByUserId = currentUserId,
                CreatedDate = currentDateTime,
                UpdatedByUserId = currentUserId,
                UpdatedDate = currentDateTime,
                TeamTypeId = teamTypeId
            };

            context.dMailTemplates.Add(newTemplate);
            context.SaveChanges();
            return newTemplate.dMailTemplateId;
        }

        public int AddCustomTemplate(int templateId, string subject, string body, int currentUserId)
        {

            DateTime currentDateTime = DateTime.UtcNow;

            dMailTemplateCustom editedTemplate = new dMailTemplateCustom()
            {
                dMailTemplateId = templateId,
                TemplateSubject = subject,
                TemplateBody = body,
                TemplateUserId = currentUserId,
                UpdatedDate = currentDateTime
            };

            context.dMailTemplateCustoms.Add(editedTemplate);
            context.SaveChanges();
            return editedTemplate.dMailTemplateCustomId;
        }

        public EmailEntities.DMailTemplateView GetTemplate(int templateId)
        {
            return context.dMailTemplates.AsNoTracking().Where(x => x.dMailTemplateId == templateId)
                .Select(x => new EmailEntities.DMailTemplateView { id = x.dMailTemplateId, TemplateSubject = x.TemplateSubject, TemplateBody = x.TemplateBody, TeamTypeId = x.TeamTypeId}).FirstOrDefault();
        }


        public IEnumerable<EmailEntities.DMailTemplateView> GetTeamTemplates(IEnumerable<int> teamTypeId)
        {

            return teamTypeId == null ?
                context.dMailTemplates.AsNoTracking()
                .Select(x => new EmailEntities.DMailTemplateView { id = x.dMailTemplateId, TemplateSubject = x.TemplateSubject, TemplateBody = x.TemplateBody, TeamTypeId = x.TeamTypeId }).ToList():
                context.dMailTemplates.AsNoTracking().Where(x => teamTypeId.Contains(x.TeamTypeId))
                .Select(x => new EmailEntities.DMailTemplateView { id = x.dMailTemplateId, TemplateSubject = x.TemplateSubject, TemplateBody = x.TemplateBody, TeamTypeId = x.TeamTypeId }).ToList();

        }

        public IEnumerable<EmailEntities.DMailTemplateView> GetTemplates(IEnumerable<int> templateIds)
        {
            return context.dMailTemplates.AsNoTracking().Where(x => templateIds.Contains(x.dMailTemplateId))
                .Select(x => new EmailEntities.DMailTemplateView { id = x.dMailTemplateId, TemplateSubject = x.TemplateSubject, TemplateBody = x.TemplateBody, TeamTypeId = x.TeamTypeId }).ToList();
        }


        public IEnumerable<EmailEntities.DMailTemplateView> GetCustomTemplates(IEnumerable<int> templateIds, int currentUserId)
        {
            return context.dMailTemplateCustoms.AsNoTracking().Where(x => templateIds.Contains(x.dMailTemplateId))
                .Select(x => new EmailEntities.DMailTemplateView { id = x.dMailTemplateId, TemplateSubject = x.TemplateSubject, TemplateBody = x.TemplateBody }).ToList();
        }

        #endregion
    }
}
