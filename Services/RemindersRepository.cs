using Slick_Domain.Common;
using Slick_Domain.Entities;
using Slick_Domain.Enums;
using Slick_Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using MCE = Slick_Domain.Entities.MatterCustomEntities;

namespace Slick_Domain.Services
{
    public class RemindersRepository : SlickRepository
    {
        public RemindersRepository(SlickContext context) : base(context)
        {
        }

        public IEnumerable<MCE.MatterRemindersView> BuildReminders(DateTime chkDate)
        {
            var reminders = new List<MCE.MatterRemindersView>();

            var allReminders = GetRemindersForUpdating();
            if (allReminders == null)
                return reminders;

            var matters = allReminders.DistinctBy(x => new { x.MatterId, x.WFComponentId}).Select(m => new { m.MatterId, m.WFComponentId });
            foreach (var mt in matters.OrderBy(x=> x.MatterId))
            {
                var bestRem = GetBestReminderDefinition(allReminders.Where(x => x.MatterId == mt.MatterId && x.WFComponentId == mt.WFComponentId));
                var reminder = allReminders.FirstOrDefault(x => x == bestRem);
                if (reminder == null) return null;
                SetReminderType(reminder, chkDate);
                if (reminder.ReminderType != null && reminder.ReminderType != ReminderTypeEnum.ExpiredReminder)
                {
                    if (reminder.ReminderType == ReminderTypeEnum.FirstReminder ||
                        reminder.ReminderType == ReminderTypeEnum.FollowupReminder)
                    {
                        SetReminderExclusions(reminder);
                        if (string.IsNullOrWhiteSpace(reminder.ReminderLog))
                        {
                            BuildReminderMsg(reminder);
                        }
                    }
                    reminders.Add(reminder);
                }
            }

            return reminders;
        }

        private void SetReminderType(MCE.MatterRemindersView reminder, DateTime chkDate)
        {
            reminder.ReminderType = null;
            reminder.ReminderLogDate = chkDate;
            var reminderLogs = context.MatterWFReminderLogs.Where(x => x.MatterWFComponentId == reminder.MatterWFComponentId).OrderBy(x => x.ReminderDate).ToList();
            if (reminderLogs == null || !reminderLogs.Any())
            {
                reminder.ReminderType = ReminderTypeEnum.Initial;
            }
            else if (reminderLogs.Any(x => x.ReminderLogTypeId == (int)ReminderTypeEnum.ExpiredReminder))
                reminder.ReminderType = ReminderTypeEnum.ExpiredReminder;

            if (!reminder.ReminderType.HasValue)
            {
                var firstLogDate = reminderLogs.First(x => x.ReminderLogTypeId == (int)ReminderTypeEnum.Initial).ReminderDate;
                if (!reminderLogs.Any(x => x.ReminderLogTypeId == (int)ReminderTypeEnum.FirstReminder))
                {
                    if (chkDate.AddDays(reminder.MatterReminderDefinition.InitialWaitDays * -1) > firstLogDate)
                        reminder.ReminderType = ReminderTypeEnum.FirstReminder;
                }
                if (!reminder.ReminderType.HasValue && chkDate.AddDays(reminder.MatterReminderDefinition.ExpiryDays * -1) > firstLogDate)
                    reminder.ReminderType = ReminderTypeEnum.NewExpiredReminder;

                var lastLogDate = reminderLogs.OrderBy(x => x.ReminderDate).Last().ReminderDate;
                if (!reminder.ReminderType.HasValue && chkDate.AddDays(reminder.MatterReminderDefinition.WaitDays * -1) > lastLogDate)
                    reminder.ReminderType = ReminderTypeEnum.FollowupReminder;
            }
            if (!reminder.StopRemindersMatter.HasValue || !reminder.StopRemindersMatter.Value)
            {
                reminder.StopRemindersMilestone = context.MatterWFReminders.FirstOrDefault(x => x.MatterWFComponentId == reminder.MatterWFComponentId)?.StopReminders ?? false;
            }

            if (reminder.ReminderType == ReminderTypeEnum.FirstReminder && reminder.WFComponentId == (int)WFComponentEnum.DocsReturned)
            {
                reminder.HasEsign = context.Matters.FirstOrDefault(x => x.MatterId == reminder.MatterId)?.IsDigiDocs ?? false;
            }

        }

        public void ProcessReminders(IEnumerable<MCE.MatterRemindersView> reminders)
        {
            var remindersToSend = new List<EmailEntities.ReminderMessage>();
            foreach (var reminder in reminders)
            {
                if (!string.IsNullOrWhiteSpace(reminder.ReminderLog))
                {
                    InsertReminderLog(reminder, reminder.ReminderLog, null, reminder.ReminderLogDate);
                }

                if (reminder.ReminderType == ReminderTypeEnum.Initial)
                {
                    var completedEventDate = context.MatterEvents.Where
                        (x => x.MatterWFComponentId == reminder.CompletedMatterWFComponentId &&
                              x.MatterEventStatusTypeId == (int)MatterEventStatusTypeEnum.Good &&
                              x.MatterEventTypeId == (int)MatterEventTypeList.MilestoneComplete)?.FirstOrDefault()?.EventDate;

                    InsertReminderLog(reminder, "Initial Date of Completed Event", null, completedEventDate.HasValue ? completedEventDate.Value.ToLocalTime() : DateTime.Now);
                }

                if (reminder.ReminderType == ReminderTypeEnum.NewExpiredReminder)
                {
                    reminder.ReminderType = ReminderTypeEnum.ExpiredReminder;
                    InsertReminderLog(reminder, "Reminder Expired", null, DateTime.Now);
                }

                // Is there a message to send? 
                if (reminder.MessageToSend != null)
                {
                    var eRep = new EmailsRepository(context);
                    var msg = reminder.MessageToSend;

                    var replyToList = new List<string>() { context.Matters.FirstOrDefault(m=>m.MatterId == msg.MatterId).User.Email };
                    var mwfRep = new MatterWFRepository(context);

                    if (!mwfRep.HasMilestoneCompleted(msg.MatterId, (int)WFComponentEnum.DocsReturned) && !mwfRep.HasMilestoneCompleted(msg.MatterId, (int)WFComponentEnum.CheckReturnedDocs))
                    {
                        replyToList.Add(GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_NoReplyDocPrepContactAddress, context));
                    }
                    else if (context.Matters.FirstOrDefault(m => m.MatterId == msg.MatterId).Settled)
                    {
                        replyToList.Add(GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_NoReplyPSTContactAddress, context));
                    }


                    try
                    {
                        if (reminder.MessageToSend.SendEmail)
                        {
                            if (Slick_Domain.GlobalVars.RunMode == "PRD")
                            {
                                if (!String.IsNullOrEmpty(msg.EmailTo.Trim()))
                                {
                                    eRep.SendEmail(msg.EmailTo, msg.EmailCC, msg.EmailSubject, msg.EmailMessage, true, replyToAddresses: replyToList);
                                }
                                else
                                {
                                    //no matching email address to actually send here. 
                                    InsertReminderLog(msg.MatterWFComponentId, GetReminderTypeFromEmailTrigger(msg.EmailTriggerTypeId), msg.ReminderDefinitionId.Value,
                                    $"Error - No recipient found for {msg.EmailTriggerType} Email", msg.EmailTo, DateTime.Now);
                                }
                            }
                            else
                            {
                                eRep.SendEmail(Slick_Domain.GlobalVars.CurrentUser.Email, msg.EmailCC, msg.EmailSubject, msg.EmailMessage, true, replyToAddresses: replyToList);
                            }
                            InsertReminderLog(msg.MatterWFComponentId, GetReminderTypeFromEmailTrigger(msg.EmailTriggerTypeId), msg.ReminderDefinitionId.Value,
                                $"{msg.EmailTriggerType} Email has been sent", msg.EmailTo, DateTime.Now);
                        }
                    }
                    catch (Exception ex)
                    {
                        Handlers.ErrorHandler.LogError(ex);
                        InsertReminderLog(msg.MatterWFComponentId, GetReminderTypeFromEmailTrigger(msg.EmailTriggerTypeId), msg.ReminderDefinitionId.Value,
                            $"Error - Could not send {msg.EmailTriggerType} Email", msg.EmailTo, DateTime.Now);
                    }

                    try
                    {
                        if (msg.SendSMS)
                        {
                            if (Slick_Domain.GlobalVars.RunMode == "PRD")
                                eRep.SendEmail(msg.SMSTo, null, "SMS from MSA National", msg.SMSMessage);
                            else
                                eRep.SendEmail(Slick_Domain.GlobalVars.CurrentUser.Email, null, "SMS from MSA National", msg.SMSMessage);
                            InsertReminderLog(msg.MatterWFComponentId, GetReminderTypeFromEmailTrigger(msg.EmailTriggerTypeId), msg.ReminderDefinitionId.Value,
                                $"{msg.EmailTriggerType} SMS has been sent", msg.SMSTo, DateTime.Now);
                        }
                    }
                    catch (Exception ex)
                    {
                        Handlers.ErrorHandler.LogError(ex);
                        InsertReminderLog(msg.MatterWFComponentId, GetReminderTypeFromEmailTrigger(msg.EmailTriggerTypeId), msg.ReminderDefinitionId.Value,
                            $"Error - Could not send {msg.EmailTriggerType} SMS", msg.SMSTo, DateTime.Now);
                    }
                }
            }

            context.SaveChanges();
        }


        //public EmailEntities.WFComponentEmail ProcessAndUpdateReminder(IEnumerable<MCE.MatterRemindersView> reminders)
        //{
        //    var reminder = GetBestReminderDefinition(reminders);
        //    if (reminder == null) return null;

        //    SetReminderType(reminder);

        //    switch (reminder.ReminderType)
        //    {
        //        case ReminderTypeEnum.Initial:
        //            var completedEventDate = context.MatterEvents.Where
        //                                        (x => x.MatterWFComponentId == reminder.CompletedMatterWFComponentId &&
        //                                              x.MatterEventStatusTypeId == (int)MatterEventStatusTypeEnum.Good &&
        //                                              x.MatterEventTypeId == (int)MatterEventTypeList.MilestoneComplete)?.FirstOrDefault()?.EventDate;

        //            InsertReminderLog(reminder, "Initial Date of Completed Event", null, completedEventDate.HasValue ? completedEventDate.Value : DateTime.Now);

        //            break;

        //        case ReminderTypeEnum.ExpiredReminder:
        //            InsertReminderLog(reminder, "Final Reminder has been sent - Task will now need to be Manually completed", null, DateTime.Now);

        //            break;

        //        default:

        //            break;
        //    }

        //    if (ReminderRequired(reminder))
        //    {
        //        return BuildComponentEmail(reminder);
        //    }
        //    else
        //        if (reminder.ReminderType == ReminderTypeEnum.ExpiredReminder)
        //            InsertReminderLog(reminder, "Final Reminder has been sent - Task will now need to be Manually completed", null, DateTime.Now);

        //    return null;

        //}


        //public bool ReminderRequired(MCE.MatterRemindersView reminder)
        //{
        //    List<MatterWFReminderLog> reminderLogs = null;
        //    if (CheckIfInitialOrExpiredReminderLog(reminder, ref reminderLogs)) return false;

        //    var firstLogDate = reminderLogs.First(x => x.ReminderLogTypeId == (int)ReminderTypeEnum.Initial).ReminderDate;

        //    bool sendReminder = false;
        //    if (!reminderLogs.Any(x => x.ReminderLogTypeId == (int)ReminderTypeEnum.FirstReminder))
        //    {
        //        if (DateTime.Now.AddDays(reminder.MatterReminderDefinition.InitialWaitDays * -1) > firstLogDate)
        //        {
        //            reminder.ReminderType = ReminderTypeEnum.FirstReminder;
        //            if (CheckForReminderExclusion(ref reminder)) return false;
        //            sendReminder = true;
        //        }
        //    }
        //    else
        //    {
        //        var lastLogDate = reminderLogs.OrderBy(x => x.ReminderDate).Last().ReminderDate;
        //        if (DateTime.Now.AddDays(reminder.MatterReminderDefinition.ExpiryDays * -1) > firstLogDate)
        //        {
        //            reminder.ReminderType = ReminderTypeEnum.ExpiredReminder;
        //            return false;
        //        }
        //        else if (DateTime.Now.AddDays(reminder.MatterReminderDefinition.WaitDays * -1) > lastLogDate)
        //        {
        //            reminder.ReminderType = ReminderTypeEnum.FollowupReminder;
        //            if (CheckForReminderExclusion(ref reminder)) return false;
        //            sendReminder = true;
        //        }
        //    }
        //    return sendReminder;
        //}

        private void BuildReminderMsg(MCE.MatterRemindersView reminder)
        {
            string triggerType = null;
            var triggerTypeId = GetEmailTriggerTypeFromReminder((int) reminder.ReminderType, ref triggerType);
            var componentEmails = context.WFComponentEmails.Where(x => x.Enabled && x.MilestoneEmailTriggerTypeId == triggerTypeId).ToList();
            if (componentEmails == null || !componentEmails.Any()) return;

            var bestEmail = GetBestComponentEmail(reminder, componentEmails);
            if (bestEmail != null)
            {
                AddReminderMessage(bestEmail, reminder);
                ReplaceReminderPlaceHolders(reminder);
            }
            else
                reminder.ReminderLog += "Unable to find a matching Milestone Reminder. No message sent.";

        }

        private void AddReminderMessage(WFComponentEmail detail, MCE.MatterRemindersView reminder)
        {
            int? recipientToUse = null;
            if (detail.NotifyStandardRecipients)
            {
                if (reminder.BrokerId.HasValue) recipientToUse = 1;
                else if (reminder.MortMgrId.HasValue) recipientToUse = 2;
                else recipientToUse = 3;
            }

            string emailTriggerType = null;
            reminder.MessageToSend = new EmailEntities.ReminderMessage
            {
                MatterId = reminder.MatterId,
                EmailTriggerTypeId = GetEmailTriggerTypeFromReminder((int)reminder.ReminderType, ref emailTriggerType),
                EmailTriggerType = emailTriggerType,
                ReminderDefinitionId = reminder.MatterReminderDefinition.ReminderDefinitionId,
                MatterWFComponentId = reminder.MatterWFComponentId,
                EmailMessage = detail.EmailMessageBody,
                EmailSubject = detail.EmailMessageSubject,
                SMSMessage = detail.SMSMessageBody,
                NotifyBroker = detail.NotifyBroker || recipientToUse == 1,
                NotifyMortMgr = detail.NotifyMortMgr || recipientToUse == 2,
                NotifyLender = detail.NotifyLender || recipientToUse == 3,
                NotifyOther = detail.NotifyOther,
                NotifyOtherParty = detail.NotifyOtherParty,
                NotifyFileOwner = detail.NotifyFileOwner,
                NotifyBorrower = detail.NotifyBorrower,
                
                OtherEmails = detail.NotifyOtherEmail,
                OtherSMS = detail.NotifyOtherSMS,
                SendEmail = detail.SendMailMessage && (reminder.ExcludeEmail == null || !reminder.ExcludeEmail.Value),
                SendSMS = detail.SendSMSMessage && (reminder.ExcludeSMS == null || !reminder.ExcludeSMS.Value)
            };

            if (reminder.MessageToSend.SendEmail)
            {
                string brokerEmail = "";

                reminder.MessageToSend.EmailTo = GetRecipientAddresses(reminder.MessageToSend, ref brokerEmail);

                if (reminder.MessageToSend.NotifyBroker && reminder.MessageToSend.NotifyBorrower)
                {
                    reminder.MessageToSend.EmailCC = brokerEmail;
                }
            }
            if (reminder.MessageToSend.SendSMS)
                reminder.MessageToSend.SMSTo = GetSMSAddresses(reminder.MessageToSend);
        }
    

        private WFComponentEmail GetBestComponentEmail(MCE.MatterRemindersView reminder, IEnumerable<WFComponentEmail> componentEmails)
        {
            var items = new List<GeneralCustomEntities.BestMatchForCriteria>();
            foreach (var item in componentEmails)
            {
                items.Add(new GeneralCustomEntities.BestMatchForCriteria
                {
                    ID = item.WFComponentEmailId,
                    LenderId = item.LenderId,
                    MatterTypeId = item.MatterTypeId,
                    MortMgrId = item.MortMgrId,
                    StateId = item.StateId,
                    SettlementTypeId = null
                });
            }

            var emailId = CommonMethods.GetBestValueFromSelectedQuery(
                reminder.MatterGroupTypeId,
                null,
                reminder.StateId,
                reminder.LenderId,
                reminder.MortMgrId,
                items);

            return componentEmails.FirstOrDefault(x => x.WFComponentEmailId == emailId);
        }

        private string GetRecipientAddresses(EmailEntities.ReminderMessage email, ref string brokerEmail)
        {
            var mt = context.Matters.AsNoTracking().Where(m => m.MatterId == email.MatterId)
                        .Select(m => new
                        {
                            m.MatterId,
                            fileOwnerEmail = m.User.Email,
                            lenderEmail = m.PrimaryContact1.Email ?? m.Lender.PrimaryContact.Email,
                            mortMgrEmail = m.PrimaryContact2.Email ?? m.MortMgr.PrimaryContact.Email,
                            brokerEmail = m.Broker.PrimaryContact.Email,
                            BorrowerDetails = m.MatterParties.Where(mp => mp.PartyTypeId == (int)Enums.MatterPartyTypeEnum.Borrower)
                                                .Select(p => new { p.Firstname, p.Lastname, p.Email, p.Mobile }),
                            OtherPartyEmails = m.MatterOtherParties.Where(mp=>!string.IsNullOrEmpty(mp.Email)),
                            m.CCNotifications,
                            currUserEmail = GlobalVars.CurrentUser.Email
                        }).FirstOrDefault();

            string retAddr = "";

            if (email.NotifyLender && !string.IsNullOrWhiteSpace(mt.lenderEmail))
                retAddr += mt.lenderEmail + ";";
            if (email.NotifyMortMgr && !string.IsNullOrWhiteSpace(mt.mortMgrEmail))
                retAddr += mt.mortMgrEmail + ";";
            if (email.NotifyBroker && !string.IsNullOrWhiteSpace(mt.brokerEmail))
            {
                if (!email.NotifyBorrower)
                {
                    retAddr += mt.brokerEmail + ";";
                }
                else
                {
                    brokerEmail = mt.brokerEmail + ";";
                } 
            }
            if (email.NotifyFileOwner && !string.IsNullOrWhiteSpace(mt.fileOwnerEmail))
                retAddr += mt.fileOwnerEmail + ";";

            if (email.NotifyBorrower)
            {
                foreach (var em in mt.BorrowerDetails.Where(x => !string.IsNullOrWhiteSpace(x.Email)))
                    retAddr += em.Email + ";";
            }

            if (email.NotifyOtherParty)
            {
                foreach (var em in mt.OtherPartyEmails)
                    retAddr += em.Email + ";";
            }

            if (email.NotifyOther && !string.IsNullOrWhiteSpace(email.OtherEmails))
                retAddr += email.OtherEmails + ";";

            if (!string.IsNullOrWhiteSpace(retAddr))
                return retAddr.Substring(0, retAddr.Length - 1);

            return retAddr;
        }

        private string GetSMSAddresses(EmailEntities.ReminderMessage msg)
        {
            var mt = context.Matters.AsNoTracking().Where(m => m.MatterId == msg.MatterId)
            .Select(m => new
            {
                m.MatterId,
                fileOwnerMobile = m.User.Mobile,
                lenderMobile = m.PrimaryContact1.Mobile ?? m.Lender.PrimaryContact.Mobile,
                mortMgrMobile = m.PrimaryContact2.Mobile ?? m.MortMgr.PrimaryContact.Mobile,
                brokerMobile = m.PrimaryContact.Mobile ?? m.Broker.PrimaryContact.Mobile,
                BorrowerDetails = m.MatterParties.Where(mp => mp.PartyTypeId == (int)Enums.MatterPartyTypeEnum.Borrower)
                                    .Select(p => new { p.Firstname, p.Lastname, p.Email, p.Mobile }),
                GuarantorDetails = m.MatterParties.Where(mp => mp.PartyTypeId == (int)Enums.MatterPartyTypeEnum.Guarantor)
                                    .Select(p => new { p.Firstname, p.Lastname, p.Email, p.Mobile }),
                OtherPartyDetails = m.MatterOtherParties.Where(mp=>mp.Phone.Substring(0,2) == "04"),
                m.CCNotifications,
                currUserMobile = GlobalVars.CurrentUser.Mobile
            }).FirstOrDefault();

            string retAddr = "";
            var appendToAddress = GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_MSASMSAddress, context);


            if (msg.NotifyLender && !string.IsNullOrWhiteSpace(mt.lenderMobile))
                retAddr += StripMobileNo(mt.lenderMobile) + ";";
            if (msg.NotifyMortMgr && !string.IsNullOrWhiteSpace(mt.mortMgrMobile))
                retAddr += StripMobileNo(mt.mortMgrMobile) + ";";
            if (msg.NotifyBroker && !string.IsNullOrWhiteSpace(mt.brokerMobile))
                retAddr += StripMobileNo(mt.brokerMobile) + ";";
         
            if (msg.NotifyFileOwner && !string.IsNullOrWhiteSpace(mt.fileOwnerMobile))
                retAddr += StripMobileNo(mt.fileOwnerMobile) + ";";

            if (msg.NotifyOtherParty)
            {
                foreach (var op in mt.OtherPartyDetails)
                {
                    retAddr += StripMobileNo(op.Phone);
                }
            }

            if (msg.NotifyBorrower)
            {
                var bt = mt.BorrowerDetails.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(bt?.Mobile))
                    retAddr += StripMobileNo(bt.Mobile) + ";";

                var gt = mt.GuarantorDetails.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(gt?.Mobile))
                    retAddr += StripMobileNo(gt.Mobile) + ";";
            }
            if (msg.NotifyOther && !string.IsNullOrWhiteSpace(msg.OtherSMS))
                retAddr += StripMobileNo(msg.OtherSMS) + ";";

            if (!string.IsNullOrWhiteSpace(retAddr))
            {
                string fullSMSAddr = "";
                foreach (var smsNo in retAddr.Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries))
                    fullSMSAddr += smsNo + appendToAddress + ";";

                return fullSMSAddr;
            }

            return retAddr;

        }

        private string StripMobileNo(string mobileNo)
        {
            if (mobileNo == null) return null;

            string cleanMobileNo1 = mobileNo.Trim().Replace(" ", null).Replace("(", null).Replace(")", null);

            var retVal = "";
            foreach (var mobNo in cleanMobileNo1.Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries))
            {
                var retNo = mobNo;

                if (retNo.Substring(0, 3) == "+61")
                    retNo = retNo.Substring(3);

                if (retNo.Length == 9 && retNo[0] == '4')
                    retNo = "0" + retNo;

                if (retNo.Length == 10 && retNo.Substring(0, 2) == "04")
                    retVal += retNo + ";";
            }
            if (retVal.Length > 0)
                return retVal.Substring(0, retVal.Length - 1);

            return null;
        }

        //private bool CheckIfInitialOrExpiredReminderLog(MCE.MatterRemindersView reminder, ref List<MatterWFReminderLog> reminderLogs)
        //{
        //    reminderLogs = context.MatterWFReminderLogs.Where(x => x.MatterWFComponentId == reminder.MatterWFComponentId).OrderBy(x => x.ReminderDate).ToList();
        //    if (reminderLogs == null || !reminderLogs.Any())
        //    {
        //        var completedEventDate = context.MatterEvents.Where
        //            (x => x.MatterWFComponentId == reminder.CompletedMatterWFComponentId &&
        //                  x.MatterEventStatusTypeId == (int) MatterEventStatusTypeEnum.Good &&
        //                  x.MatterEventTypeId == (int) MatterEventTypeList.MilestoneComplete)?.FirstOrDefault()?.EventDate;

        //        reminder.ReminderType = ReminderTypeEnum.Initial;
        //        InsertReminderLog(reminder, "Initial Date of Completed Event", null, completedEventDate.HasValue ? completedEventDate.Value : DateTime.Now);
        //        return true;
        //    }

        //    if (reminderLogs.Any(x => x.ReminderLogTypeId == (int) ReminderTypeEnum.ExpiredReminder))
        //        return true;

        //    return false;
        //}

        private void SetReminderExclusions(MCE.MatterRemindersView reminder)
        {
            if (reminder.StopRemindersMatter.HasValue && reminder.StopRemindersMatter.Value)
            {
                reminder.ReminderLog = $"{(GetReminderType(reminder.ReminderType))} Reminder not sent due to Stop Reminders set on Matter";
            }
            else if (reminder.StopRemindersMilestone.HasValue && reminder.StopRemindersMilestone.Value)
            {
                reminder.ReminderLog = $"{(GetReminderType(reminder.ReminderType))} Reminder not sent due to Stop Reminders set on Milestone";
            }
            else if (reminder.ReminderType == ReminderTypeEnum.FirstReminder && reminder.MatterReminderDefinition.ExcludeESignOnInitial && reminder.HasEsign)
            {
                reminder.ReminderLog = $"{(GetReminderType(reminder.ReminderType))} Reminder not sent due to ESign Exclusion";
            }
            else
            {
                var lenderExclusions = context.LenderSendReminderExcls.Where(x => x.LenderId == reminder.LenderId && x.WFComponentId == reminder.WFComponentId);
                if (lenderExclusions.Any())
                {
                    reminder.ExcludeEmail = lenderExclusions.First().ExcludeEmailReminders;
                    reminder.ExcludeSMS = lenderExclusions.First().ExcludeSMSReminders;
                    if (reminder.ExcludeEmail.Value)
                    {
                        reminder.ReminderLog = $"{(GetReminderType(reminder.ReminderType))} Email Reminder not sent due to Lender Exclusion";
                    }
                    //if (reminder.ExcludeSMS.Value)
                    //{
                    //    reminder.ReminderLog = $"{(GetReminderType(reminder.ReminderType))} SMS Reminder not sent due to Lender Exclusion";
                    //}
                }
            }
            if (!string.IsNullOrWhiteSpace(reminder.ReminderLog))
                reminder.ReminderLogDate = DateTime.Now;

        }

        private void SetExclusionValues(ref MCE.MatterRemindersView reminder)
        {
            var rem = reminder;

            if (!reminder.StopRemindersMatter.HasValue || !reminder.StopRemindersMatter.Value)
            {
                reminder.StopRemindersMilestone = context.MatterWFReminders.FirstOrDefault(x => x.MatterWFComponentId == rem.MatterWFComponentId)?.StopReminders ?? false;
            }

            if (reminder.ReminderType == ReminderTypeEnum.FirstReminder && reminder.WFComponentId == (int)WFComponentEnum.DocsReturned)
            {
                reminder.HasEsign = context.Matters.FirstOrDefault(x => x.MatterId == rem.MatterId)?.IsDigiDocs ?? false;
            }
        }

        private string GetReminderType(ReminderTypeEnum? reminderType)
        {
            switch (reminderType)
            {
                case ReminderTypeEnum.FirstReminder:
                    return "First";

                case ReminderTypeEnum.FollowupReminder:
                    return "Followup";
                default:
                    return null;
            }
        }

        public void InsertReminderLog(int mwfId, int reminderLogType, int reminderDefinitionId, string log, string recipient, DateTime reminderDate)
        {
            context.MatterWFReminderLogs.Add(new MatterWFReminderLog
            {
                MatterWFComponentId = mwfId,
                ReminderDefinitionId = reminderDefinitionId,
                ReminderDate = reminderDate,
                ReminderLog = log,
                ReminderLogTypeId = reminderLogType,
                Recipient = string.IsNullOrEmpty(recipient) ? "" : recipient.TruncateLongString(256),
                UpdatedDate = DateTime.Now,
                UpdatedByUserId = GlobalVars.CurrentUserId ?? DomainConstants.SystemUserId
            });
        }

        public void InsertReminderLog(MCE.MatterRemindersView reminder, string log, string recipient, DateTime reminderDate)
        {
            InsertReminderLog(reminder.MatterWFComponentId, (int) reminder.ReminderType, reminder.MatterReminderDefinition.ReminderDefinitionId,  log, recipient, reminderDate);
        }

        private MCE.MatterRemindersView GetBestReminderDefinition(IEnumerable<MCE.MatterRemindersView> reminders)
        {
            var items = new List<GeneralCustomEntities.BestMatchForCriteria>();
            foreach (var rem in reminders)
            {
                var def = rem.MatterReminderDefinition;
                
                items.Add(new GeneralCustomEntities.BestMatchForCriteria
                {
                    ID = def.ReminderDefinitionId,
                    LenderId = def.LenderId,
                    MatterTypeId = def.MatterTypeId,
                    MortMgrId = def.MortMgrId,
                    StateId = def.StateId,
                    SettlementTypeId = null
                });
            }

            var firstRem = reminders.First();
            var remDefId = CommonMethods.GetBestValueFromSelectedQuery(
                firstRem.MatterGroupTypeId,
                null,
                firstRem.StateId,
                firstRem.LenderId,
                firstRem.MortMgrId,
                items);

            return reminders.FirstOrDefault(x => x.MatterReminderDefinition.ReminderDefinitionId == remDefId);
        }

        public IEnumerable<MCE.MatterRemindersView> GetRemindersForUpdating()
        {
            return
            (from rem in context.ReminderDefinitions
             join mwf in context.MatterWFComponents on rem.WFComponentId equals mwf.WFComponentId
             join m in context.Matters on mwf.MatterId equals m.MatterId
             join wfCpltd in context.MatterWFComponents on rem.CompletedWFComponentId equals wfCpltd.WFComponentId

             where rem.Enabled && 
             !(m.MatterStatusTypeId == (int)MatterStatusTypeEnum.NotProceeding || m.MatterStatusTypeId == (int)MatterStatusTypeEnum.OnHold) &&
             (wfCpltd.WFComponentStatusTypeId == (int) MatterWFComponentStatusTypeEnum.Complete && wfCpltd.MatterId == m.MatterId) &&
             (mwf.WFComponentStatusTypeId == (int) MatterWFComponentStatusTypeEnum.Starting || mwf.WFComponentStatusTypeId == (int) MatterWFComponentStatusTypeEnum.InProgress) &&
             (rem.LenderId == null || rem.LenderId == m.LenderId) &&
             (rem.MatterTypeId == null || rem.MatterTypeId == m.MatterGroupTypeId) &&
             (rem.MortMgrId == null || rem.MortMgrId == m.MortMgrId) &&
             (rem.StateId == null || rem.StateId == m.StateId)
             select new
             MCE.MatterRemindersView
             {
                 MatterId = m.MatterId,
                 MatterGroupTypeId = m.MatterGroupTypeId,
                 StopRemindersMatter = m.StopReminders,
                 LenderId = m.LenderId,
                 MortMgrId = m.MortMgrId,
                 BrokerId = m.BrokerId,
                 StateId = m.StateId,
                 WFComponentId = mwf.WFComponentId,
                 MatterWFComponentId = mwf.MatterWFComponentId,
                 CompletedMatterWFComponentId = wfCpltd.MatterWFComponentId,
                 MatterReminderDefinition = rem
             }).ToList();    
        }




        public void SendAutomatedReminders(List<EmailEntities.ReminderMessage> messages)
        {
            using (var eRep = new EmailsRepository(context))
            {
                foreach (var msg in messages)
                {
                    try
                    {
                        if (msg.SendEmail)
                        {
                            eRep.SendEmail(msg.EmailTo, msg.EmailCC, msg.EmailSubject, msg.EmailMessage);
                            InsertReminderLog(msg.MatterWFComponentId, GetReminderTypeFromEmailTrigger(msg.EmailTriggerTypeId), msg.ReminderDefinitionId.Value,
                                $"{msg.EmailTriggerType} Email has been sent", msg.EmailTo, DateTime.Now);
                        }
                    }
                    catch (Exception ex)
                    {
                        Handlers.ErrorHandler.LogError(ex);
                        InsertReminderLog(msg.MatterWFComponentId, GetReminderTypeFromEmailTrigger(msg.EmailTriggerTypeId), msg.ReminderDefinitionId.Value,
                            $"Error - Could not send {msg.EmailTriggerType} Email", msg.EmailTo, DateTime.Now);
                    }

                    try
                    {
                        if (msg.SendSMS)
                        {
                            eRep.SendEmail(msg.SMSTo, null, "SMS from MSA National", msg.SMSMessage);
                            InsertReminderLog(msg.MatterWFComponentId, GetReminderTypeFromEmailTrigger(msg.EmailTriggerTypeId), msg.ReminderDefinitionId.Value,
                                $"{msg.EmailTriggerType} SMS has been sent", msg.SMSTo, DateTime.Now);
                        }
                    }
                    catch (Exception ex)
                    {
                        Handlers.ErrorHandler.LogError(ex);
                        InsertReminderLog(msg.MatterWFComponentId, GetReminderTypeFromEmailTrigger(msg.EmailTriggerTypeId), msg.ReminderDefinitionId.Value,
                            $"Error - Could not send {msg.EmailTriggerType} SMS", msg.SMSTo, DateTime.Now);
                    }

                }
                context.SaveChanges();
            }
        }

        //public void SendAutomatedReminderEmails(List<EmailEntities.ReminderMessage> messages)
        //{
        //    foreach (var msg in messages)
        //    {
        //        try
        //        {
        //            EmailsService.SendEmail(msg.EmailTo, msg.EmailCC, Slick_Domain.GlobalVars.GetGlobalTxtVar(Slick_Domain.Common.DomainConstants.GlobalVars_FallbackEmail),
        //                                        msg.EmailSubject, msg.EmailMessage);

        //            var emailRecipient = string.IsNullOrEmpty(msg.EmailTo) ? "" : msg.EmailTo.TruncateLongString(256);
        //            InsertReminderLog(msg.MatterWFComponentId, GetReminderTypeFromEmailTrigger(msg.EmailTriggerTypeId), msg.ReminderDefinitionId.Value,
        //                $"{msg.EmailTriggerType} Email has been sent", emailRecipient, DateTime.Now);
        //            context.SaveChanges();
        //        }
        //        catch (Exception ex)
        //        {
        //            Handlers.ErrorHandler.LogError(ex);
        //            var emailRecipient = string.IsNullOrEmpty(msg.EmailTo) ? "" : msg.EmailTo.TruncateLongString(256);
        //            InsertReminderLog(msg.MatterWFComponentId, GetReminderTypeFromEmailTrigger(msg.EmailTriggerTypeId), msg.ReminderDefinitionId.Value,
        //                $"Error - Could not send {msg.EmailTriggerType} Email", emailRecipient, DateTime.Now);
        //            context.SaveChanges();
        //        }
        //    }
        //}

        private int GetReminderTypeFromEmailTrigger(int emailTriggerTypeId)
        {
            switch (emailTriggerTypeId)
            {
                case (int) MilestoneEmailTriggerTypeEnum.FirstReminder:
                    return (int) ReminderTypeEnum.FirstReminder;

                case (int) MilestoneEmailTriggerTypeEnum.FollowupReminder:
                    return (int) ReminderTypeEnum.FollowupReminder;

                default:
                    return (int)ReminderTypeEnum.General;
            }
        }

        private int GetEmailTriggerTypeFromReminder(int reminderTypeId, ref string triggerType)
        {
            switch (reminderTypeId)
            {
                case (int) ReminderTypeEnum.FirstReminder:
                    {
                        triggerType = "First Reminder";
                        return (int) MilestoneEmailTriggerTypeEnum.FirstReminder;
                    }

                case (int) ReminderTypeEnum.FollowupReminder:
                    {
                        triggerType = "Followup Reminder";
                        return (int) MilestoneEmailTriggerTypeEnum.FollowupReminder;
                    }
                    
                default:
                    {
                        triggerType = "First Reminder";
                        return (int) MilestoneEmailTriggerTypeEnum.FirstReminder;
                    }
            }
        }

        //public void SendAutomatedReminderSMS(List<EmailEntities.ReminderMessage> messages)
        //{
        //    foreach (var sms in messages)
        //    {
        //        //emailModel = emailRep.BuildEmailPlaceHolderModelWithMatterWFComponent(sms.MatterId, sms.MatterWFComponentId, true);
        //        //if (!sms.NotifyBroker) emailModel.BrokerName = null;
        //        //if (!sms.NotifyLender) emailModel.LenderName = null;
        //        //if (!sms.NotifyMortMgr) emailModel.MortMgrName = null;
        //        //if (!sms.NotifyBorrower) emailModel.BorrowerName = null;
        //        //emailModel.EmailMobiles.Other = sms.OtherSMS;

        //        try
        //        {
        //            EmailsService.SendSMS(sms.SMSMessage, emailModel: emailModel);
        //            var emailRecipient = string.IsNullOrEmpty(sms.SMSTo) ? "" : emailModel.EmailRecipient.TruncateLongString(256);
        //            InsertReminderLog(thisSMS.MatterWFComponentId, GetReminderTypeFromEmailTrigger(thisSMS.EmailTriggerTypeId), thisSMS.ReminderDefinitionId.Value,
        //                $"{thisSMS.EmailTriggerType} SMS has been sent", emailRecipient, DateTime.Now);
        //            context.SaveChanges();
        //        }
        //        catch (Exception ex)
        //        {
        //            Handlers.ErrorHandler.LogError(ex);
        //            var emailRecipient = string.IsNullOrEmpty(emailModel.EmailRecipient) ? "" : emailModel.EmailRecipient.TruncateLongString(256);
        //            InsertReminderLog(thisSMS.MatterWFComponentId, GetReminderTypeFromEmailTrigger(thisSMS.EmailTriggerTypeId), thisSMS.ReminderDefinitionId.Value,
        //                $"Error - Could not send {thisSMS.EmailTriggerType} SMS", emailRecipient, DateTime.Now);
        //            context.SaveChanges();
        //        }
        //    }
        //}

        public IEnumerable<EmailEntities.ReminderDefinitionView> GetReminderDefinitionsView()
        {
            return context.ReminderDefinitions.AsNoTracking()
            .Select(s => new
            {
                s.ReminderDefinitionId,
                s.CompletedWFComponentId,
                s.ExpiryDays,
                s.InitialWaitDays,
                s.Enabled,
                s.LenderId,
                s.Lender.LenderName,
                s.MatterTypeId,
                s.MatterType.MatterTypeName,
                s.MortMgrId,
                s.MortMgr.MortMgrName,
                s.WaitDays,
                s.ReminderName,
                ReminderWFComponentId = s.WFComponentId,
                CompletedWFComponentName = s.WFComponent.WFComponentName,
                ReminderWFComponentName = s.WFComponent1.WFComponentName,
                s.ExcludeESignOnInitial,
                s.StateId,
                s.State.StateName,
                s.UpdatedByUserId,
                s.User.Username,
                s.UpdatedDate,
                isDirty = false
            })
            .ToList()
            .Select(s2 => new EmailEntities.ReminderDefinitionView
            {
                Id = s2.ReminderDefinitionId,
                CompletedWFComponentId = s2.CompletedWFComponentId,
                ExpiryDays = s2.ExpiryDays,
                InitialDays = s2.InitialWaitDays,
                IsEnabled = s2.Enabled,
                ExcludeEsignOnInitial = s2.ExcludeESignOnInitial,
                LenderId = s2.LenderId ?? DomainConstants.AnySelectionId,
                LenderName = s2.LenderName ?? "-- Any Lender --",
                MatterTypeId = s2.MatterTypeId ?? DomainConstants.AnySelectionId,
                MatterTypeName = s2.MatterTypeName ?? "-- Any Matter Type --",
                MortMgrId = s2.MortMgrId ?? DomainConstants.AnySelectionId,
                MortMgrName = s2.MortMgrName ?? "-- Any Mort Mgr --",
                StateId = s2.StateId ?? DomainConstants.AnySelectionId,
                StateName = s2.StateName ?? "-- Any State --",
                NextFollowupDays = s2.WaitDays,
                ReminderName = s2.ReminderName,
                ReminderWFComponentId = s2.ReminderWFComponentId,
                CompletedWFComponentName = s2.CompletedWFComponentName,
                ReminderWFComponentName = s2.ReminderWFComponentName,
               
                UpdatedById = s2.UpdatedByUserId,
                UpdatedByUserName = s2.Username,
                UpdatedDate = s2.UpdatedDate,
                isDirty = false
            }).ToList();
        }





        private void ReplaceReminderPlaceHolders(MCE.MatterRemindersView reminder)
        {
            var eRep = new EmailsRepository(context);

            //Add Standard Email Placeholders
            var emailPlaceHolderValues = eRep.LinkPlaceHoldersToMatterValues(reminder.MatterId);

            reminder.MessageToSend.EmailSubject = eRep.ReplaceEmailPlaceHolders(emailPlaceHolderValues, reminder.MessageToSend.EmailSubject);
            reminder.MessageToSend.EmailMessage = eRep.ReplaceEmailPlaceHolders(emailPlaceHolderValues, reminder.MessageToSend.EmailMessage);

            reminder.MessageToSend.SMSMessage = eRep.ReplaceEmailPlaceHolders(emailPlaceHolderValues, reminder.MessageToSend.SMSMessage);
        }
    }
}
