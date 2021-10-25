using System;
using System.Collections.Generic;
using System.Linq;
using Slick_Domain.Models;
using Slick_Domain.Interfaces;
using Slick_Domain.Entities;
using Slick_Domain.Common;
using Slick_Domain.Enums;
using Slick_Common.Extensions;
using ExternalUserPrivilegeType = Slick_Domain.Enums.ExternalUserPrivilegeType;
using static Slick_Domain.Entities.UserCustomEntities;
using MatterDigiDocsEnvelope = Slick_Domain.Entities.MatterDigiDocsEnvelope;
using System.Collections.ObjectModel;

namespace Slick_Domain.Services
{
    public class UserRepository : IDisposable
    {
        private readonly IRepository<User> userRepository;
        private readonly SlickContext context;

        public UserRepository(SlickContext Context)
        {
            userRepository = new Repository<User>(Context);
            context = Context;
        }

        public string GetUsernameForId(int id)
        {
            return userRepository.FindById(id)?.Username;
        }

        public string GetUserFullNameForId(int id)
        {
            var user = userRepository.AllAsQueryNoTracking.Where(x => x.UserId == id)
                .Select(x => new { x.Firstname, x.Lastname })
                .FirstOrDefault();

            if (user != null)
            {
                return EntityHelper.GetFullName(user.Lastname, user.Firstname);
            }

            return string.Empty;
        }


        public string GetNewsUserFullNameForId(int? id)
        {
            var user = userRepository.AllAsQueryNoTracking.Where(x => x.UserId == id)
                .Select(x => new { x.Firstname, x.Lastname })
                .FirstOrDefault();

            if (user != null)
            {
                return EntityHelper.GetFullName(user.Lastname, user.Firstname);
            }

            return string.Empty;
        }

        public User GetUser(int id)
        {
            return userRepository.FindById(id);
        }

        private string GetCompanyName(int UserTypeId, string LenderName, string MortMgrName, string BrokerName)
        {
            switch (UserTypeId)
            {
                case (int)Slick_Domain.Enums.UserTypeEnum.Internal: // Internal
                    return GlobalVars.GetCompanyName;
                case (int)Slick_Domain.Enums.UserTypeEnum.Lender: // Internal
                    return LenderName;
                case (int)Slick_Domain.Enums.UserTypeEnum.MortMgr: // Internal
                    return MortMgrName;
                case (int)Slick_Domain.Enums.UserTypeEnum.Broker: // Internal
                    return BrokerName;
                default: return "";
            }
        }

        public bool UserIsAuthorisedForPrivilegeType(int userId, ExternalUserPrivilegeType type)
        {
            User u = context.Users.FirstOrDefault(f => f.UserId == userId);
            if (u == null) return false;

            return u.ExternalUserPrivileges.Any(x => x.ExternalUserPrivilegeTypeId == (int)type);
        }

        //Used by Admin User List
        public IEnumerable<UserCustomEntities.UserGridView> GetUserGrid(string userScope, string srchTxt)
        {
            var tmp = context.Users
                      .Select(u => new
                      {
                          u.UserId,
                          u.Username,
                          u.Enabled,
                          u.Lastname,
                          u.Firstname,
                          u.DisplayInitials,
                          u.State.StateName,
                          u.Email,
                          u.UserTypeId,
                          u.UserType.UserTypeName,
                          u.UserType.IsExternal,
                          u.Lender.LenderName,
                          u.MortMgr.MortMgrName,
                          BrokerLastname = u.Broker.PrimaryContact.Lastname,
                          BrokerFirstname = u.Broker.PrimaryContact.Firstname,
                          u.FailedLogins,
                          u.LastLoginDate,
                          u.UpdatedDate,
                          u.UpdatedByUserId,
                          UpdatedByUsername = u.User2.Username
                      });

            if (!string.IsNullOrEmpty(srchTxt))
                tmp = tmp.Where(u => u.Username.Contains(srchTxt) || u.Lastname.Contains(srchTxt) || u.Firstname.Contains(srchTxt) || u.DisplayInitials == srchTxt);

            switch (userScope)
            {
                case "I":
                    tmp = tmp.Where(t => t.IsExternal == false);
                    break;
                case "E":
                    tmp = tmp.Where(t => t.IsExternal == true);
                    break;
            }

            var results = tmp.ToList().Select(u => new UserCustomEntities.UserGridView(u.UserId, u.Username, u.Enabled,
                            EntityHelper.GetFullName(u.Lastname, u.Firstname), u.DisplayInitials, u.StateName, u.Email, u.LastLoginDate, u.UserTypeName,
                            GetCompanyName(u.UserTypeId, u.LenderName, u.MortMgrName, EntityHelper.GetFullName(u.BrokerLastname, u.BrokerFirstname)), u.FailedLogins, u.UpdatedDate, u.UpdatedByUserId, u.UpdatedByUsername));

            return results;


        }
        public IEnumerable<UserCustomEntities.UserGridView> GetUserGridEnableFlag(string userScope, string srchTxt, bool showDisabled)
        {
            var tmp = context.Users
                      .Select(u => new
                      {
                          u.UserId,
                          u.Username,
                          u.Enabled,
                          u.Lastname,
                          u.Firstname,
                          u.DisplayInitials,
                          u.State.StateName,
                          u.Email,
                          u.UserTypeId,
                          u.UserType.UserTypeName,
                          u.UserType.IsExternal,
                          u.Lender.LenderName,
                          u.MortMgr.MortMgrName,
                          BrokerLastname = u.Broker.PrimaryContact.Lastname,
                          BrokerFirstname = u.Broker.PrimaryContact.Firstname,
                          u.FailedLogins,
                          u.LastLoginDate,
                          u.UpdatedDate,
                          u.UpdatedByUserId,
                          UpdatedByUsername = u.User2.Username
                      });

            if (!string.IsNullOrEmpty(srchTxt))
                tmp = tmp.Where(u => u.Username.Contains(srchTxt) || u.Lastname.Contains(srchTxt) || u.Firstname.Contains(srchTxt) || u.DisplayInitials == srchTxt);

            switch (userScope)
            {
                case "I":
                    tmp = tmp.Where(t => t.IsExternal == false);
                    break;
                case "E":
                    tmp = tmp.Where(t => t.IsExternal == true);
                    break;
            }

            if (!showDisabled)
            {
                tmp = tmp.Where(t => t.Enabled == true);
            }

            var results = tmp.ToList().Select(u => new UserCustomEntities.UserGridView(u.UserId, u.Username, u.Enabled,
                            EntityHelper.GetFullName(u.Lastname, u.Firstname), u.DisplayInitials, u.StateName, u.Email, u.LastLoginDate, u.UserTypeName,
                            GetCompanyName(u.UserTypeId, u.LenderName, u.MortMgrName, EntityHelper.GetFullName(u.BrokerLastname, u.BrokerFirstname)), u.FailedLogins, u.UpdatedDate, u.UpdatedByUserId, u.UpdatedByUsername));

            return results;


        }

        public IEnumerable<UserCustomEntities.UserSkillView> GetUserSkills(int userId)
        {
            return context.UserSkills.Where(x => x.UserId == userId && !x.ParentSkillId.HasValue).Select(s => new UserCustomEntities.UserSkillView()
            {
                UserSkillId = s.UserSkillId,
                SkillName = s.SkillType.SkillTypeName,
                SkillTypeId = s.SkillTypeId,
                InEditMode = false,
                LenderId = s.SpecificLenderId,
                IsLenderSpecific = s.SpecificLenderId.HasValue,
                LenderName = s.SpecificLenderId.HasValue ? s.Lender.LenderName : null,
                ChildSkills = s.UserSkill1.Select(c => new { c.UserSkillId, c.SkillType.SkillTypeName, c.Notes, c.SkillTypeId }).ToList().Select(c => new UserCustomEntities.UserSkillChildView()
                {
                    UserSkillId = c.UserSkillId,
                    SkillName = c.SkillTypeName,
                    SkillNotes = c.Notes,
                    SkillTypeId = c.SkillTypeId,
                    InEditMode = false
                }).ToList(),
                HasChildSkills = s.UserSkill1.Any()

            }).ToList();
        }

        public IEnumerable<UserShortcutsView> GetUserShortcuts(int userId)
        {
            return context.UserShortcuts.Where(l => l.UserId == userId).Select(x => new UserShortcutsView()
            {
                isDirty = false,
                UserShortcutId = x.UserShortcutId,
                UserId = x.UserId,
                ShortcutTypeId = x.ShortcutTypeId,
                Shortcut = x.Shortcut,
                DisplayOrder = x.DisplayOrder,
                ShortcutName = x.ShortcutName,
            }).OrderByDescending(x => x.DisplayOrder);
        }

        public IEnumerable<MatterDigiDocsEnvelope> GetEnvelopes(int matterId)
        {
            return context.MatterDigiDocsEnvelopes.Where(x => x.MatterId == matterId).Select(l => new MatterDigiDocsEnvelope()
            {
                MatterDigiDocsEnvelopeId = l.MatterDigiDocsEnvelopeId,
                MatterId = l.MatterId,
                MatterWFComponentId = l.MatterWFComponentId,
                DocuSignEnvelopeIdentifier = l.DocuSignEnvelopeIdentifier,
                DigiDocsEnvelopeStatusTypeId = l.DigiDocsEnvelopeStatusTypeId,
                CreatedDate = l.CreatedDate,
                SentDate = l.SentDate,
                DeliveredDate = l.DeliveredDate,
                UpdatedDate = l.UpdatedDate,
                UpdatedByUserId = l.UpdatedByUserId
            }).ToList();
        }

        public IEnumerable<ReportUserTemplates> GetSavedReports(int userId)
        {
            return context.ReportUserTemplates.Where(x => x.UserId == userId).Select(l => new ReportUserTemplates()
            {
                ReportUserTemplateId = l.ReportUserTemplateId,
                UserId = l.UserId,
                ReportId = l.ReportId,
                TemplateName = l.TemplateName,
                UpdatedDate = l.UpdatedDate,
                UpdatedByUserId = l.UpdatedByUserId
            }).ToList();
        }

        public IEnumerable<MatterCustomEntities.MatterAllIssuesView> GetUserPSTIssues(int userId, DateTime startDate, DateTime endDate)
        {
            endDate = endDate.AddDays(1).Date.AddMinutes(-1);

            DateTime startDateUTC = startDate.ToUniversalTime();
            DateTime endDateUTC = endDate.ToUniversalTime();

            List<MatterCustomEntities.MatterAllIssuesView> allIssues = new List<MatterCustomEntities.MatterAllIssuesView>();

            var custIssues = context.MatterWFLetterToCustodians.Where(x => x.MatterWFComponent.Matter.FileOwnerUserId == userId
                  && x.MatterWFLetterToCustodianIssues.Any(i => i.IssueCreatedDate > startDate && i.IssueCreatedDate < endDate))
                  .Select(x =>
                  new MatterCustomEntities.GroupedPSTIssueView()
                  {
                      MatterWFComponentId = x.MatterWFComponent.MatterId,
                      WFComponentId = x.MatterWFComponent.WFComponentId,
                      WFComponentName = x.MatterWFComponent.WFComponent.WFComponentName,
                      MatterId = x.MatterWFComponent.MatterId,
                      LenderId = x.MatterWFComponent.Matter.LenderId,
                      LenderRefNo = x.MatterWFComponent.Matter.LenderRefNo,
                      LenderName = x.MatterWFComponent.Matter.Lender.LenderName,
                      MatterDescription = x.MatterWFComponent.Matter.MatterDescription,
                      SettlementDate = x.MatterWFComponent.Matter.SettlementSchedule.SettlementDate,
                      Issues = x.MatterWFLetterToCustodianIssues.Where(i => i.IssueCreatedDate > startDate && i.IssueCreatedDate < endDate)
                      .Select(p => new MatterCustomEntities.PSTIssueView()
                      {

                          MatterWFLetterToCustodianIssueId = p.MatterWFLetterToCustodianIssueId,
                          IssueDescription = " - " + p.IssueComments,
                          IssueText = p.PossibleCustodianLetterIssue.ItemDesc,
                          IssueCreatedDate = p.IssueCreatedDate,
                          IssueResolvedDate = p.IssueResolvedDate,
                          Resolved = p.IssueResolvedDate.HasValue,
                          MatterWFComponentId = p.MatterWFLetterToCustodian.MatterWFComponentId,
                          WFComponentId = p.MatterWFLetterToCustodian.MatterWFComponent.WFComponentId,
                          WFComponentName = p.MatterWFLetterToCustodian.MatterWFComponent.WFComponent.WFComponentName,
                          DocumentNames = p.MatterWFLetterToCustodianIssueItems.Select(d => d.IssueDocumentName).ToList(),
                          IsAcknowledged = p.IssueAcknowledged
                      }).ToList()
                  }).ToList();

            var matterWFIssues = context.MatterWFIssues.Where(x =>
                 x.MatterWFComponent.Matter.FileOwnerUserId == userId &&
                 (x.MatterWFComponent.WFComponentId == (int)WFComponentEnum.LodgementIssue || x.MatterWFComponent.WFComponentId == (int)WFComponentEnum.Requisition) &&
                 x.IssueRaisedDate > startDateUTC && x.IssueRaisedDate < endDateUTC)

                 .Select(x => new
                 {
                     IssueDescription = x.Reason.ReasonTxt + (x.Notes != null ? " - " : "") + x.Notes,
                     x.Resolved,
                     IssueResolvedDate = x.ResolvedDate.HasValue ? x.ResolvedDate : (DateTime?)null,
                     MatterWFComponentId = x.RaisedMatterWFComponentId,
                     x.MatterWFComponent.WFComponentId,
                     x.MatterWFComponent.WFComponent.WFComponentName,
                     x.MatterWFComponent.Matter.SettlementSchedule.SettlementDate,
                     x.MatterWFComponent.MatterId,
                     x.MatterWFComponent.Matter.MatterDescription,
                     x.MatterWFComponent.Matter.LenderRefNo,
                     x.MatterWFComponent.Matter.LenderId,
                     Acknowledged = x.IssueAcknowledged,
                     x.MatterWFIssueId
                 }).ToList()
                 .Select(x => new MatterCustomEntities.PSTIssueView()
                 {
                     MatterWFIssueId = x.MatterWFIssueId,
                     IssueDescription = x.IssueDescription,
                     Resolved = x.Resolved,
                     IssueResolvedDate = x.IssueResolvedDate.HasValue ? x.IssueResolvedDate.Value.ToLocalTime() : x.IssueResolvedDate,
                     MatterWFComponentId = x.MatterWFComponentId,
                     WFComponentId = x.WFComponentId,
                     WFComponentName = x.WFComponentName,
                     SettlementDate = x.SettlementDate,
                     MatterId = x.MatterId,
                     MatterDescription = x.MatterDescription,
                     LenderRefNo = x.LenderRefNo,
                     LenderId = x.LenderId,
                     IsAcknowledged = x.Acknowledged
                 })
            .ToList()
            .GroupBy(x => x.MatterWFComponentId)
            .Select
            (
                 x => new MatterCustomEntities.GroupedPSTIssueView()
                 {
                     MatterId = x.First().MatterId,
                     MatterDescription = x.First().MatterDescription,
                     LenderId = x.First().LenderId,
                     LenderName = x.First().LenderName,
                     LenderRefNo = x.First().LenderRefNo,
                     MatterWFComponentId = x.First().MatterWFComponentId,
                     SettlementDate = x.First().SettlementDate,
                     WFComponentId = x.First().WFComponentId,
                     WFComponentName = x.First().WFComponentName,
                     Issues = x.ToList()
                 }
            );

            var activeStatuses = new List<int>() { (int)MatterWFComponentStatusTypeEnum.InProgress, (int)MatterWFComponentStatusTypeEnum.Starting };

            var requisitions = context.MatterWFConfirmRegistrations.Where(x =>
            x.MatterWFComponent.Matter.FileOwnerUserId == userId && x.RequisitionDate > startDate && x.RequisitionDate < endDate)
                .Select(x =>
                new
                {
                    x.RequisitionDate,
                    x.MatterWFComponentId,
                    x.MatterWFComponent.WFComponentStatusTypeId,
                    x.MatterWFComponent.WFComponentId,
                    x.MatterWFComponent.WFComponent.WFComponentName,
                    x.RegistrationPaid,
                    EventDates = x.MatterWFComponent.MatterEvents.Where(e => e.MatterEventTypeId == (int)MatterEventTypeList.MilestoneComplete).Select(e => e.EventDate),
                    x.UpdatedDate,
                    x.MatterWFComponent.Matter.SettlementSchedule.SettlementDate,
                    x.MatterWFComponent.MatterId,
                    x.MatterWFComponent.Matter.MatterDescription,
                    x.MatterWFComponent.Matter.LenderRefNo,
                    x.MatterWFComponent.Matter.LenderId,
                }).ToList()
                .Select(x => new MatterCustomEntities.PSTIssueView()
                {
                    IssueDescription = $"Requisition: {x.RequisitionDate.Value.ToString("dd/MM/yy")}",
                    Resolved = activeStatuses.Contains(x.WFComponentStatusTypeId) ? (x.RegistrationPaid ?? false) : true,
                    IssueResolvedDate = x.EventDates.Any() ? x.EventDates.FirstOrDefault().ToLocalTime() : (activeStatuses.Contains(x.WFComponentStatusTypeId) ? (x.RegistrationPaid ?? false) : true) ? x.UpdatedDate : (DateTime?)null,
                    MatterWFComponentId = x.MatterWFComponentId,
                    WFComponentId = x.WFComponentId,
                    WFComponentName = x.WFComponentName,
                    SettlementDate = x.SettlementDate,
                    MatterId = x.MatterId,
                    MatterDescription = x.MatterDescription,
                    LenderRefNo = x.LenderRefNo,
                    LenderId = x.LenderId,
                }).ToList().GroupBy(x => x.MatterWFComponentId)
               .Select
               (
                    x => new MatterCustomEntities.GroupedPSTIssueView()
                    {
                        MatterId = x.First().MatterId,
                        MatterDescription = x.First().MatterDescription,
                        LenderId = x.First().LenderId,
                        LenderName = x.First().LenderName,
                        LenderRefNo = x.First().LenderRefNo,
                        MatterWFComponentId = x.First().MatterWFComponentId,
                        SettlementDate = x.First().SettlementDate,
                        WFComponentId = x.First().WFComponentId,
                        WFComponentName = x.First().WFComponentName,
                        Issues = x.ToList()
                    }
               );


            allIssues = custIssues.Concat(matterWFIssues).Concat(requisitions)
                .GroupBy(x => x.MatterId).Select(x => new MatterCustomEntities.MatterAllIssuesView()
                {
                    MatterId = x.Key,
                    LenderName = x.First().LenderName,
                    LenderRefNo = x.First().LenderRefNo,
                    SettlementDate = x.First().SettlementDate,
                    MatterDescription = x.First().MatterDescription,
                    AllIssues = x.ToList().OrderBy(o => o.Issues.Any(r => !r.Resolved)).ToList()
                }).OrderByDescending(o => o.AllIssues.Any(i => i.Issues.Any(r => !r.Resolved))).ThenBy(s => s.SettlementDate).ToList();
            foreach (var iss in allIssues)
            {
                foreach (var childIss in iss.AllIssues)
                {
                    childIss.Issues = childIss.Issues.OrderByDescending(o => o.Resolved).ThenBy(r => r.IssueResolvedDate).ToList();
                    if (childIss.Issues.Count > 1)
                    {
                        childIss.WFComponentName = childIss.WFComponentName + "s";
                    }
                    foreach (var issue in childIss.Issues.Where(d => d.DocumentNames != null && d.DocumentNames.Any()))
                    {
                        issue.IssueDescription = string.Join(", ", issue.DocumentNames) + issue.IssueDescription;
                    }
                }
            }

            return allIssues;
        }
        public List<UserCustomEntities.WorkDayView> LoadUserWorkDays( int userId, int workWeekId )
        {

            var toReturn = new List<UserCustomEntities.WorkDayView>()
            {
                    new UserCustomEntities.WorkDayView((int)DayOfWeek.Monday, "Monday"),
                    new UserCustomEntities.WorkDayView((int)DayOfWeek.Tuesday, "Tuesday"),
                    new UserCustomEntities.WorkDayView((int)DayOfWeek.Wednesday, "Wednesday"),
                    new UserCustomEntities.WorkDayView((int)DayOfWeek.Thursday, "Thursday"),
                    new UserCustomEntities.WorkDayView((int)DayOfWeek.Friday, "Friday"),
                    new UserCustomEntities.WorkDayView((int)DayOfWeek.Saturday, "Saturday"),
                    new UserCustomEntities.WorkDayView((int)DayOfWeek.Sunday , "Sunday"),

            };

            var existingWorkDays = context.UserWorkDays.Where(w => w.UserId == userId && w.WorkWeekId == workWeekId)
                    .Select(d => new { d.DayIndex, d.IsFullDay, Hours = d.UserWorkDayHours.Select(h => h.TimeIndex).ToList() }).ToList();

            foreach(var day in existingWorkDays)
            {
                var match = toReturn.FirstOrDefault(d => d.DayIndex == day.DayIndex);
                if(match != null)
                {
                    match.FullDay = day.IsFullDay;
                    if (!match.FullDay)
                    {
                        foreach(var hour in day.Hours)
                        {
                            match.Hours.FirstOrDefault(h => h.HourIndex == (float)hour).Checked = true;
                        }
                    }
                }
            }

            return toReturn;
        
        }

        public void SaveUserWorkHours(int userId, bool isFulltime, List<UserCustomEntities.WorkWeekView> weeks)
        {
            var existingWorkHours = context.UserWorkDayHours.Where(w => w.UserWorkDay.UserId == userId);
            context.UserWorkDayHours.RemoveRange(existingWorkHours);
            context.SaveChanges();
            var existingWorkDays = context.UserWorkDays.Where(w => w.UserId == userId);
            context.UserWorkDays.RemoveRange(existingWorkDays);
            context.SaveChanges();
            if (!isFulltime)
            {
                foreach (var week in weeks)
                {
                    foreach (var day in week.WorkDays)
                    {
                        var dbWorkDay = new UserWorkDay() { UserId = userId, DayIndex = day.DayIndex, IsFullDay = day.FullDay, WorkWeekId = week.WorkWeekId };
                        context.UserWorkDays.Add(dbWorkDay);
                        context.SaveChanges();
                        if (!day.FullDay)
                        {
                            foreach (var hour in day.Hours.Where(c => c.Checked))
                            {
                                context.UserWorkDayHours.Add(new UserWorkDayHour() { TimeIndex = Convert.ToDecimal(hour.HourIndex), UserWorkDayId = dbWorkDay.UserWorkDayId });
                            }
                        }
                    }
                }
            }
            context.SaveChanges();

        }

        public void SaveUserSkills(int userId, List<UserCustomEntities.UserSkillView> skills)
        {
            //start by just wiping anything in there
            context.UserSkills.RemoveRange(context.UserSkills.Where(u => u.UserId == userId));
            context.SaveChanges();
            //then add top level skills first, then their children
            foreach (var skill in skills)
            {
                var newSkill = context.UserSkills.Add(new UserSkill() { UserId = userId, SkillTypeId = skill.SkillTypeId, UpdatedDate = DateTime.Now, UpdatedByUserId = GlobalVars.CurrentUser.UserId, SpecificLenderId = skill.LenderId });
                context.SaveChanges();
                if (skill.ChildSkills != null)
                {
                    foreach (var child in skill.ChildSkills)
                    {
                        var newChildSkill = context.UserSkills.Add(new UserSkill() { UserId = userId, SkillTypeId = child.SkillTypeId, Notes = child.SkillNotes, UpdatedDate = DateTime.Now, UpdatedByUserId = GlobalVars.CurrentUser.UserId, SpecificLenderId = skill.LenderId, ParentSkillId = newSkill.UserSkillId });
                        context.SaveChanges();
                    }
                }
            }
        }


        public IEnumerable<EntityCompacted> GetPossibleParentSkills()
        {
            return context.SkillTypes.Where(x => !x.ParentSkillTypeId.HasValue).Select(x => new EntityCompacted() { Id = x.SkillTypeId, Details = x.SkillTypeName }).ToList();
        }

        public IEnumerable<UserCustomEntities.PossibleUserChildSkill> GetAllPossibleChildSkills()
        {
            return context.SkillTypes.Where(x => x.ParentSkillTypeId.HasValue).Select(x => new UserCustomEntities.PossibleUserChildSkill() { SkillId = x.SkillTypeId, SkillName = x.SkillTypeName, ParentSkillId = x.ParentSkillTypeId.Value }).ToList();
        }

        public IEnumerable<UserCustomEntities.PossibleUserSkill> GetAllPossibleSkillsGridView()
        {
            return context.SkillTypes.Where(x => !x.ParentSkillTypeId.HasValue).Select(x => new UserCustomEntities.PossibleUserSkill()
            {
                SkillId = x.SkillTypeId,
                SkillName = x.SkillTypeName,
                SubSkills = x.SkillType1.Select(s => new UserCustomEntities.PossibleUserChildSkill() { SkillId = s.SkillTypeId, SkillName = s.SkillTypeName, ParentSkillId = x.SkillTypeId }).ToList()
            }).ToList();
        }

        public UserCustomEntities.UserGridView GetUserView(int userId)
        {
            return context.Users
               .Where(u => u.UserId == userId)
            .Select(u => new
            {
                u.UserId,
                u.Username,
                u.Enabled,
                u.Lastname,
                u.Firstname,
                u.DisplayInitials,
                u.State.StateName,
                u.Email,
                u.UserType.UserTypeName,
                u.UserTypeId,
                u.Lender.LenderName,
                u.MortMgr.MortMgrName,
                BrokerLastname = u.Broker.PrimaryContact.Lastname,
                BrokerFirstname = u.Broker.PrimaryContact.Firstname,
                u.FailedLogins,
                u.LastLoginDate,
                u.UpdatedDate,
                u.UpdatedByUserId,
                UpdatedByUsername = u.User2.Username

            })
             .ToList()
             .Select(u => new UserCustomEntities.UserGridView(u.UserId, u.Username, u.Enabled,
                EntityHelper.GetFullName(u.Lastname, u.Firstname), u.DisplayInitials, u.StateName, u.Email, u.LastLoginDate, u.UserTypeName,
                GetCompanyName(u.UserTypeId, u.LenderName, u.MortMgrName, EntityHelper.GetFullName(u.BrokerLastname, u.BrokerFirstname)), u.FailedLogins, u.UpdatedDate, u.UpdatedByUserId, u.UpdatedByUsername))
                .FirstOrDefault();
        }
        public void SoftDeleteUser(UnitOfWork uow, int userId, ref bool disable)
        {
            context.Users.FirstOrDefault(u => u.UserId == userId).Enabled = false;
            context.SaveChanges();
        }
        public void DeleteUser(UnitOfWork uow, int userId, ref bool disable)
        {
            try
            {
                var pRep = uow.GetRepositoryInstance<UserPrivilege>();
                pRep.DeleteAll(pRep.AllAsQuery.Where(x => x.UserId == userId));

                var uprfRep = uow.GetRepositoryInstance<UserPreference>();
                uprfRep.DeleteAll(uprfRep.AllAsQuery.Where(x => x.UserId == userId));

                var uprfrecRep = uow.GetRepositoryInstance<UserPrefRecentMatter>();
                uprfrecRep.DeleteAll(uprfrecRep.AllAsQuery.Where(x => x.UserId == userId));

                var uprwstRep = uow.GetRepositoryInstance<UserPrefWindowState>();
                uprwstRep.DeleteAll(uprwstRep.AllAsQuery.Where(x => x.UserId == userId));

                var contRep = uow.GetRepositoryInstance<Contact>();
                contRep.DeleteAll(contRep.AllAsQuery.Where(x => x.ContactOwnerUserId == userId));

                var utRep = uow.GetRepositoryInstance<ReportUserTemplate>();
                var deletedTemplates = new List<ReportUserTemplate>();

                var dbRep = uow.GetRepositoryInstance<Dashboard>();
                var dbcontRep = uow.GetRepositoryInstance<DashboardContent>();
                var dashboards = dbRep.AllAsQuery.Where(x => x.UserId == userId).ToList();
                foreach (var db in dashboards)
                {
                    deletedTemplates.AddRange(dbcontRep.AllAsQuery.Where(x => x.DashboardId == db.DashboardId && x.ReportUserTemplateId.HasValue)
                        .Select(y => y.ReportUserTemplate).ToList());

                    dbcontRep.DeleteAll(dbcontRep.AllAsQuery.Where(x => x.DashboardId == db.DashboardId));
                }
                dbRep.DeleteAll(dbRep.AllAsQuery.Where(x => x.UserId == userId));

                var utparmRep = uow.GetRepositoryInstance<ReportUserTemplateParam>();
                var templates = utRep.AllAsQuery.Where(x => x.UserId == userId).ToList().Union(deletedTemplates);
                foreach (var template in templates)
                {
                    utparmRep.DeleteAll(utparmRep.AllAsQuery.Where(x => x.ReportUserTemplateId == template.ReportUserTemplateId));
                }
                utRep.DeleteAll(templates);

                var stauRep = uow.GetRepositoryInstance<SharedTaskAllocUser>();
                stauRep.DeleteAll(stauRep.AllAsQuery.Where(x => x.UserId == userId));

                var taaRep = uow.GetRepositoryInstance<TaskAllocAssignment>();
                taaRep.DeleteAll(taaRep.AllAsQuery.Where(x => x.UserId == userId || x.OriginalUserId == userId));

                var taufRep = uow.GetRepositoryInstance<TaskAllocUserFilter>();
                taufRep.DeleteAll(taufRep.AllAsQuery.Where(x => x.UserId == userId));

                var mrvwRep = uow.GetRepositoryInstance<MatterReviewedHistory>();
                mrvwRep.DeleteAll(mrvwRep.AllAsQuery.Where(x => x.LastReviewedByUserId == userId));

                var uRep = uow.GetRepositoryInstance<User>();
                uRep.Remove(uRep.FindById(userId));
            }
            catch (Exception ex)
            {
                if (ex.Message == DomainConstants.ForeignKeyConstraint)
                {
                    disable = true;
                }
                else
                    throw;
            }
        }

        public void DisableUser(int userId)
        {
            var usr = context.Users.FirstOrDefault(x => x.UserId == userId);
            if (usr == null) return;

            usr.Enabled = false;
            usr.UpdatedByUserId = GlobalVars.CurrentUser.UserId;
            usr.UpdatedDate = DateTime.Now;
            context.SaveChanges();
        }

        public UserCustomEntities.UserDetails GetUserDetails(int userId)
        {
            var retVal = context.Users
               .Where(u => u.UserId == userId)
            .Select(u => new
            {
                u.UserId,
                u.Username,
                u.ShowVaccinatedBorder,
                u.Enabled,
                u.SSORequired,
                u.Lastname,
                u.Firstname,
                u.DisplayInitials,
                u.StateId,
                u.State.StateName,
                u.UserTypeId,
                u.UserType.UserTypeName,
                u.LenderId,
                u.Lender.LenderName,
                u.MortMgrId,
                u.MortMgr.MortMgrName,
                u.BrokerId,
                BrokerLastname = u.Broker.PrimaryContact.Lastname,
                BrokerFirstname = u.Broker.PrimaryContact.Firstname,
                u.Email,
                u.Phone,
                u.Mobile,
                u.PreferredNoteBodyColor,
                u.PreferredNoteAccentColor,
                u.Fax,
                u.OfficeExt,
                u.Birthday,
                u.OffshoreUser,
                u.ProfilePhoto,
                u.LastLoginDate,
                u.PasswordSalt,
                u.FailedLogins,
                u.FailedLoginBanDate,
                u.PwdResetGuid,
                u.PwdResetExpiry,
                u.Notes,
                u.UpdatedDate,
                u.UpdatedByUserId,
                UpdatedByUsername = u.User2.Username,
                u.HasStateRestrictions,
                u.HideFromContacts,
                u.DefaultPrivateNotes,
                u.RelationshipManagerId,
                u.WorkingFromHome,
                u.HasMatterTypeRestrictions,
                u.SignatureFont,
                u.SignatureInitialOnly,
                u.EmploymentStatusTypeId,
                u.ExternalEmail
            })
             .ToList()
             .Select(u => new UserCustomEntities.UserDetails(u.UserId, u.Username, u.Enabled, u.Lastname, u.Firstname, u.DisplayInitials, u.StateId, u.StateName, u.UserTypeId, u.UserTypeName,
                        u.LenderId, u.LenderName, u.MortMgrId, u.MortMgrName, u.BrokerId, EntityHelper.GetFullName(u.BrokerLastname, u.BrokerFirstname), u.Email, u.ExternalEmail, u.Phone, u.Mobile, u.Fax,
                        u.OfficeExt, u.Birthday, u.ProfilePhoto, u.LastLoginDate,
                        u.PasswordSalt, u.FailedLogins, u.FailedLoginBanDate, u.PwdResetGuid, u.PwdResetExpiry, u.Notes, u.UpdatedDate, u.UpdatedByUserId, u.UpdatedByUsername, u.HasStateRestrictions, u.HideFromContacts, u.DefaultPrivateNotes, u.PreferredNoteBodyColor, u.PreferredNoteAccentColor, u.RelationshipManagerId, u.WorkingFromHome, u.HasMatterTypeRestrictions, u.SignatureFont, u.SignatureInitialOnly, null, u.OffshoreUser, u.SSORequired, u.EmploymentStatusTypeId, u.ShowVaccinatedBorder))
                .FirstOrDefault();

            // Check if there's a profile picture
            var filePath = System.IO.Path.Combine(GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_UserProfilePictureDirectory, context), userId.ToString()) + ".jpeg";

            if (System.IO.File.Exists(filePath))
            {
                retVal.ProfilePhoto = System.IO.File.ReadAllBytes(filePath);
            }
            else
            {
                var fallbackPath = System.IO.Path.Combine(GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_UserProfilePictureDirectory, context), "NoUserImg.jpg");
                retVal.ProfilePhoto = System.IO.File.ReadAllBytes(fallbackPath);
            }

            // Now add privileges
            //retVal.UserPrivileges =
            IEnumerable<UserPrivilege> usrPrivs = context.UserPrivileges.Where(u => u.UserId == userId);
            foreach (UserPrivilege up in usrPrivs)
            {
                switch (up.PrivilegeTypeId)
                {
                    case (int)Enums.PrivilegeType.Admin:
                        retVal.UserPrivileges.Administrator = true;
                        break;
                    case (int)Enums.PrivilegeType.User:
                        retVal.UserPrivileges.GeneralUser = true;
                        break;
                    case (int)Enums.PrivilegeType.UserAdmin:
                        retVal.UserPrivileges.UserAdmin = true;
                        break;
                    case (int)Enums.PrivilegeType.ExtUserAdmin:
                        retVal.UserPrivileges.ExtUserAdmin = true;
                        break;
                    case (int)Enums.PrivilegeType.AccountsAdmin:
                        retVal.UserPrivileges.AccountsAdmin = true;
                        break;
                    case (int)Enums.PrivilegeType.SettlementsAdmin:
                        retVal.UserPrivileges.SettlementsAdmin = true;
                        break;
                    case (int)Enums.PrivilegeType.ReportsUser:
                        retVal.UserPrivileges.ReportsUser = true;
                        break;
                    case (int)Enums.PrivilegeType.ReportsAccounts:
                        retVal.UserPrivileges.ReportsAccounts = true;
                        break;
                    case (int)Enums.PrivilegeType.ReportsAll:
                        retVal.UserPrivileges.ReportsAll = true;
                        break;
                    case (int)Enums.PrivilegeType.ReportsMgmt:
                        retVal.UserPrivileges.ReportsMgmt = true;
                        break;
                    case (int)Enums.PrivilegeType.AmendFrontCoverAll:
                        retVal.UserPrivileges.AmendFrontCoverAll = true;
                        break;
                    case (int)Enums.PrivilegeType.Tester:
                        retVal.UserPrivileges.Tester = true;
                        break;
                    case (int)Enums.PrivilegeType.PEXAConversations:
                        retVal.UserPrivileges.PEXAConversations = true;
                        break;
                    case (int)Enums.PrivilegeType.DischargesAdmin:
                        retVal.UserPrivileges.DischargesAdmin = true;
                        break;
                    case (int)Enums.PrivilegeType.CanUnarchive:
                        retVal.UserPrivileges.CanUnarchive = true;
                        break;
                    case (int)Enums.PrivilegeType.SlickMessages:
                        retVal.UserPrivileges.SlickMessages = true;
                        break;
                    case (int)Enums.PrivilegeType.VoidEnvelope:
                        retVal.UserPrivileges.VoidEnvelopes = true;
                        break;
                    case (int)Enums.PrivilegeType.SlickipediaAdmin:
                        retVal.UserPrivileges.SlickipediaAdmin = true;
                        break;
                    case (int)Enums.PrivilegeType.HRAdmin:
                        retVal.UserPrivileges.HRAdmin = true;
                        break;
                    case (int)Enums.PrivilegeType.BannerAdmin:
                        retVal.UserPrivileges.BannerAdmin = true;
                        break;
                    case (int)Enums.PrivilegeType.RosterAdmin:
                        retVal.UserPrivileges.RosterAdmin = true;
                        break;
                    case (int)Enums.PrivilegeType.BrokerDetails:
                        retVal.UserPrivileges.ChangeBrokerDetails = true;
                        break;
                    case (int)Enums.PrivilegeType.MergeBrokers:
                        retVal.UserPrivileges.MergeBrokers = true;
                        break;
                    case (int)Enums.PrivilegeType.RegenerateDocs:
                        retVal.UserPrivileges.RegenerateDocs = true;
                        break;

                }
            }

            return retVal;
        }

        //Not sure if used
        public UserCustomEntities.UserGridView UpdateUserInGridView(int id)
        {
            var item = userRepository.FindById(id);

            var user = new UserCustomEntities.UserGridView()
            {
                UserId = item.UserId,
                Username = item.Username,
                Enabled = item.Enabled,
                FullName = EntityHelper.GetFullName(item.Lastname, item.Firstname),
                StateName = item.State.StateName,
                Email = item.Email,
                UserTypeName = item.UserType.UserTypeName,
                UserTypeCompanyName =
                        GetCompanyName(item.UserTypeId,
                                       item.Lender?.LenderName,
                                       item.MortMgr?.MortMgrName,
                                       EntityHelper.GetFullName(item.Broker.PrimaryContact.Lastname, item.Broker.PrimaryContact.Firstname))
            };
            return user;
        }

        public IEnumerable<EntityCompacted> GetUsersCompacted()
        {
            return
                 context.Users.AsNoTracking()
                .Where(s => s.Enabled == true)
                .OrderBy(o => o.Lastname).ThenBy(o => o.Firstname)
                .ToList()
                .Select(m => new EntityCompacted
                {
                    Id = m.UserId,
                    Details = string.Format("{1}, {0}", m.Firstname, m.Lastname)
                })
                .ToList();
        }

        public IEnumerable<EntityCompacted> GetUsersCompacted(bool isExternal)
        {
            return
                 context.Users.AsNoTracking()
                .Where(s => s.Enabled == true && s.UserType.IsExternal == isExternal)
                .OrderBy(o => o.Lastname).ThenBy(o => o.Firstname)
                .ToList()
                .Select(m => new EntityCompacted
                {
                    Id = m.UserId,
                    Details = string.Format("{1}, {0}", m.Firstname, m.Lastname)
                })
                .ToList();
        }

        public IEnumerable<EntityCompacted> GetUserNamesCompacted(bool isExternal)
        {
            return
                 context.Users.AsNoTracking()
                .Where(s => s.Enabled == true && s.UserType.IsExternal == isExternal)
                .OrderBy(o => o.Username)
                .ToList()
                .Select(m => new EntityCompacted
                {
                    Id = m.UserId,
                    Details = m.Username
                })
                .ToList();
        }



        public IEnumerable<UserCustomEntities.UserListView> GetIntUserList()
        {
            IQueryable<User> usrQry = context.Users.Where(u => u.Enabled == true && u.UserType.IsExternal == false);
            return usrQry
                 .Select(u => new { u.UserId, u.Username, u.Enabled, u.Firstname, u.Lastname, u.StateId, u.State.StateName })
                 .ToList()
                 .Select(u => new UserCustomEntities.UserListView(u.UserId, u.Username, u.Enabled, EntityHelper.GetFullName(u.Lastname, u.Firstname), u.StateId, u.StateName))
                 .ToList();
        }

        public void UserPrefAddRecentMatter(int matterId)
        {
            UserPrefRecentMatter uprm = context.UserPrefRecentMatters.Where(u => u.UserId == GlobalVars.CurrentUser.UserId && u.MatterId == matterId).FirstOrDefault();
            if (uprm == null)
            {
                //Doesn't exist... add it

                uprm = new UserPrefRecentMatter();
                uprm.UserId = GlobalVars.CurrentUser.UserId;
                uprm.MatterId = matterId;
                uprm.AccessedDate = DateTime.Now;
                context.UserPrefRecentMatters.Add(uprm);

            }
            else
            {
                //Updated accessed date
                uprm.AccessedDate = DateTime.Now;
                context.UserPrefRecentMatters.Attach(uprm);
                context.Entry(uprm).State = System.Data.Entity.EntityState.Modified;
            }
            context.SaveChanges();

            // and remove oldest if > 6 exists
            IEnumerable<UserPrefRecentMatter> uprmLast = context.UserPrefRecentMatters.Where(u => u.UserId == GlobalVars.CurrentUser.UserId && !u.PinnedDate.HasValue).OrderByDescending(u => u.AccessedDate).Skip(20).ToList();
            if (uprmLast.Count() > 0)
                context.UserPrefRecentMatters.RemoveRange(uprmLast);

            context.SaveChanges();
        }


        public IEnumerable<UserCustomEntities.UserRecentMattersView> GetRecentMattersCompacted(int userId)
        {
            var qry = context.UserPrefRecentMatters.AsNoTracking()
                .Where(s => (s.Matter.MatterStatusTypeId != (int)Slick_Domain.Enums.MatterStatusTypeEnum.Closed || s.Matter.MatterStatusTypeId != (int)Slick_Domain.Enums.MatterStatusTypeEnum.NotProceeding) && s.UserId == userId)
                .OrderByDescending(o=>o.PinnedDate.HasValue).ThenByDescending(o=>o.PinnedDate).ThenByDescending(o => o.AccessedDate).Select(m => m.Matter);

            var items =  GetRecentMattersViewForQuery(qry).ToList();

            if (items.Any(p => p.PinnedDate.HasValue))
            {
                var lastPinned = items.LastOrDefault(p => p.PinnedDate.HasValue);
                lastPinned.IsLastPinned = true;

                var insertSeparatorAt = items.IndexOf(lastPinned) + 1;
                try
                {
                    items.Insert(insertSeparatorAt, new UserRecentMattersView() { IsSeparator = true });
                }
                catch
                {
                    //just means that there's no need to put a separator anyway as the pinned matter is the last matter in the list. Can't think of when this would even happen but hey, better than throwing an error.
                }
             }

            return items;
        }
        public IEnumerable<UserCustomEntities.UserRecentMattersView> GetRecentMatterViewForMatter(int matterId)
        {
            var qry = context.Matters.AsNoTracking().Where(s => s.MatterId == matterId);
            return GetRecentMattersViewForQuery(qry);
        }

        public IEnumerable<UserCustomEntities.UserRecentMattersView> GetRecentMattersViewForQuery (IQueryable<Matter> matters)
        {
            List<int> activeComponentStatuses = new List<int>() { (int)MatterWFComponentStatusTypeEnum.InProgress, (int)MatterWFComponentStatusTypeEnum.Starting };
            List<int> displayComponentStatuses = new List<int>() { (int)DisplayStatusTypeEnum.Default, (int)DisplayStatusTypeEnum.Display };
            List<int> prepareSettlementInstr = new List<int>() { (int)WFComponentEnum.PrepareSettlementInstr, (int)WFComponentEnum.PrepareSettlementInstrDisch, (int)WFComponentEnum.PrepareSettlementPEXA };


            return matters.Select(m2 => new
            {
                m2.MatterId,
                m2.MatterDescription,
                LenderId = m2.LenderId,
                LenderCode = m2.MortMgr.MortMgrDisplayCode ?? m2.Lender.LenderDisplayCode ?? m2.Lender.LenderNameShort,
                DisplayLenderRef = m2.LenderId == 139 && m2.SecondaryRefNo != null ? m2.SecondaryRefNo : m2.LenderRefNo,
                LenderRefNo = m2.LenderRefNo,
                SecondaryRefNo = m2.SecondaryRefNo,
                SecondaryRefName = m2.Lender.SecondaryRefName,
                m2.MatterStatusTypeId,
                m2.IsTestFile,
                MortMgrRef = m2.MortMgr.MortMgrDisplayCode,
                LenderFullName = m2.Lender.LenderName,
                MortMgrFullname = m2.MortMgr.MortMgrName,
                SettlementDate = m2.SettlementScheduleId.HasValue ? (DateTime?)m2.SettlementSchedule.SettlementDate : (DateTime?)null,
                SettlementTime = m2.SettlementScheduleId.HasValue? (TimeSpan?)m2.SettlementSchedule.SettlementTime : (TimeSpan?)null,
                MatterStatus = m2.MatterStatusType.MatterStatusTypeName,
                Settled = m2.Settled,
                Fileowner = m2.User.Fullname,
                MatterGroupType = m2.MatterType.MatterTypeName,
                MatterTypes =  m2.MatterMatterTypes.Select(m => m.MatterType.MatterTypeName).ToList(),
                ActiveMilestones = m2.MatterWFComponents.Where(w => activeComponentStatuses.Contains(w.WFComponentStatusTypeId) && displayComponentStatuses.Contains(w.DisplayStatusTypeId)).Select(w=>w.WFComponent.WFComponentName).ToList(),
                PrepareSettlementInstructionsComplete = m2.MatterWFComponents.Any(w=>w.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.Complete && prepareSettlementInstr.Contains(w.WFComponentId) && displayComponentStatuses.Contains(w.DisplayStatusTypeId)),
                Securities = m2.MatterSecurities.Where(d=>!d.Deleted).Select(s=>s.StreetAddress + ", " + s.Suburb + ", " + s.State.StateName + ", " + s.PostCode).ToList(),
                PinnedDate = (DateTime?) m2.UserPrefRecentMatters.Where(m=>m.UserId == GlobalVars.CurrentUser.UserId).Select(d=>d.PinnedDate).FirstOrDefault()
            })
            .ToList()
            .Select(m => new UserCustomEntities.UserRecentMattersView(m.MatterId, m.LenderId, m.MatterDescription, m.MatterStatusTypeId, m.IsTestFile, m.LenderCode, m.DisplayLenderRef, m.LenderRefNo, m.SecondaryRefNo, m.SecondaryRefName, m.LenderFullName, m.MortMgrFullname, m.Fileowner, m.MatterGroupType, m.MatterTypes, m.SettlementDate, m.SettlementTime, m.Settled, m.MatterStatus, m.ActiveMilestones.ToList(), /*m.PrepareSettlementInstructionsComplete*/ false, m.Securities, m.PinnedDate ))
            .ToList();
        }
        private IEnumerable<UserCustomEntities.ContactsListView> GetContactsList(IQueryable<Contact> contactsQry)
        {
            return contactsQry
                .Select(c => new
                {
                    c.ContactId,
                    c.ContactTypeId,
                    c.ContactOwnerUserId,
                    c.Firstname,
                    c.Lastname,
                    c.Fullname,
                    c.CompanyName,
                    c.StreetAddress,
                    c.Suburb,
                    c.StateId,
                    c.State.StateName,
                    c.PostCode,
                    c.Phone,
                    c.Mobile,
                    c.Fax,
                    c.Email,
                    c.ProfilePhoto,
                    c.Notes,
                    c.UpdatedDate,
                    c.UpdatedByUserId,
                    UpdatedByUsername = c.User1.Username
                }).ToList()
            .Select(c => new UserCustomEntities.ContactsListView(c.ContactId, c.ContactTypeId, c.ContactOwnerUserId, null, c.Firstname, c.Lastname, c.Fullname, c.CompanyName, c.StreetAddress,
                            c.Suburb, c.StateId, c.StateName, c.PostCode, c.Phone, c.Mobile, c.Fax, c.Email, c.ProfilePhoto, c.Notes, c.UpdatedDate, c.UpdatedByUserId, c.UpdatedByUsername))
                            .ToList();

        }

        private IEnumerable<UserCustomEntities.ContactsListView> GetContactsList(IQueryable<User> contactsQry)
        {
            return contactsQry.Where(u => !u.HideFromContacts)
            .Select(u => new
            {
                u.UserId,
                u.Firstname,
                u.Lastname,
                u.Fullname,
                CompanyName = "MSA National",
                u.StateId,
                u.State.StateName,
                u.Phone,
                u.Mobile,
                u.Fax,
                u.Email,
                u.ProfilePhoto,
                u.Notes,
                u.UpdatedDate,
                u.UpdatedByUserId,
                UpdatedByUsername = u.User2.Username
            }).ToList()
            .Select(c => new UserCustomEntities.ContactsListView(null, (int)Slick_Domain.Enums.ContactTypeEnum.Individual, null, c.UserId, c.Firstname, c.Lastname, c.Fullname, c.CompanyName, null,
                    null, c.StateId, c.StateName, null, c.Phone, c.Mobile, c.Fax, c.Email, c.ProfilePhoto, c.Notes, c.UpdatedDate, c.UpdatedByUserId, c.UpdatedByUsername))
                    .ToList();

        }

        public IEnumerable<UserCustomEntities.ContactsListView> GetContactsList(bool inclMSA, bool inclGlobal, bool inclPersonal, int? personalUserId)
        {
            try
            {
                var retList = new List<UserCustomEntities.ContactsListView>();


                if (inclMSA)
                {
                    IQueryable<Slick_Domain.Models.User> us = context.Users.Where(u => u.UserTypeId == (int)Enums.UserTypeEnum.Internal && u.Enabled && !u.HideFromContacts);
                    retList.AddRange(GetContactsList(us));
                }


                IQueryable<Contact> cl = context.Contacts;
                if (inclGlobal && inclPersonal)
                    cl = cl.Where(c => c.ContactOwnerUserId == null || c.ContactOwnerUserId == personalUserId);
                else if (inclGlobal)
                    cl = cl.Where(c => c.ContactOwnerUserId == null);
                else
                    cl = cl.Where(c => c.ContactOwnerUserId == personalUserId);

                retList.AddRange(GetContactsList(cl));


                return retList.OrderBy(o => o.Fullname);

            }
            catch (Exception ex)
            {
                Handlers.ErrorHandler.LogError(ex);
                return null;
            }
        }

        public UserCustomEntities.ContactsListView GetContactListView(int contactId)
        {
            IQueryable<Contact> cl = context.Contacts.Where(c => c.ContactId == contactId);

            return GetContactsList(cl).FirstOrDefault();
        }

        public UserCustomEntities.ContactsListView GetUserContactListView(int userId)
        {
            IQueryable<User> us = context.Users.Where(c => c.UserId == userId);

            return GetContactsList(us).FirstOrDefault();
        }



        private IEnumerable<UserCustomEntities.QuickSearchContactsView> GetQuickSearchContacts(IQueryable<Contact> contactsQry)
        {
            return contactsQry.Select(c => new { c.ContactTypeId, c.ContactId, c.Firstname, c.Lastname, c.Fullname, c.CompanyName, c.Phone, c.Mobile, c.Email, c.State.StateName, c.ProfilePhoto })
                .ToList()
                .Select(c => new UserCustomEntities.QuickSearchContactsView(c.ContactTypeId, null, c.ContactId, c.Firstname, c.Lastname, c.CompanyName, c.Phone, c.Mobile, c.Email, c.StateName, "Custom Contact", c.ProfilePhoto, false, false))
                .ToList();
        }
        private IEnumerable<UserCustomEntities.QuickSearchContactsView> GetQuickSearchContacts(IQueryable<User> contactsQry)
        {
            return contactsQry.Where(u => !u.HideFromContacts).Select(u => new { u.UserId, u.Firstname, u.Lastname, u.Fullname, u.Phone, u.Mobile, u.Email, u.State.StateName, u.Notes, u.ProfilePhoto, u.WorkingFromHome, u.ShowVaccinatedBorder })
                .ToList()
                .Select(u => new UserCustomEntities.QuickSearchContactsView((int)ContactTypeEnum.Individual, u.UserId, null, u.Firstname, u.Lastname, null, u.Phone, u.Mobile, u.Email, u.StateName, u.Notes, u.ProfilePhoto, u.WorkingFromHome, u.ShowVaccinatedBorder))
                .ToList();
        }

        public IEnumerable<UserCustomEntities.QuickSearchContactsView> GetQuickSearchContacts()
        {
            var retList = new List<UserCustomEntities.QuickSearchContactsView>();

            IQueryable<Contact> cl = context.Contacts.Where(x => x.ContactOwnerUserId == null || x.ContactOwnerUserId == GlobalVars.CurrentUser.UserId);

            retList.AddRange(GetQuickSearchContacts(cl));

            IQueryable<User> us = context.Users.Where(u => u.UserTypeId == (int)Slick_Domain.Enums.UserTypeEnum.Internal && u.Enabled);
            retList.AddRange(GetQuickSearchContacts(us));

            return retList.OrderBy(o => o.DisplayName);
        }

        public void DeleteMessage(int messageId)
        {
            context.SlickMessageTeams.RemoveRange(context.SlickMessageTeams.Where(m => m.SlickMessageId == messageId));
            context.SlickMessageUsers.RemoveRange(context.SlickMessageUsers.Where(m => m.SlickMessageId == messageId));
            context.SlickMessageLikes.RemoveRange(context.SlickMessageLikes.Where(m => m.SlickMessageId == messageId));

            context.SaveChanges();
            context.SlickMessages.RemoveRange(context.SlickMessages.Where(m => m.SlickMessageId == messageId));
            context.SaveChanges();


        }
        public void SaveExistingMessage(UserCustomEntities.MessageView message, List<int> messageUsers, List<int> messageTeams)
        {
            var existingMessage = context.SlickMessages.Where(m => m.SlickMessageId == message.MessageId).FirstOrDefault();

            existingMessage.Subject = message.MessageSubject;
            existingMessage.MessageBody = message.MessageBody;
            existingMessage.MessageByUserId = GlobalVars.CurrentUser.UserId;
            existingMessage.MessageIsGlobal = message.MessageIsGlobal;
            existingMessage.SlickMessageTypeId = message.MessageTypeId;
            existingMessage.IsPinned = message.MessageIsPinned;
            existingMessage.MessageExpiryDate = message.MessageExpiryDate;

            existingMessage.RelatesToMatterId = message.MatterId;
            existingMessage.RelatesToLenderId = message.RelatesToLenderId;
            existingMessage.RelatesToUserId = message.RelatesToUserId;


            existingMessage.MessageDate = DateTime.Now;

            context.SaveChanges();

            //delete existing user / team links and rebuild out of laziness.
            context.SlickMessageUsers.RemoveRange(context.SlickMessageUsers.Where(m => m.SlickMessageId == message.MessageId));
            context.SlickMessageTeams.RemoveRange(context.SlickMessageTeams.Where(m => m.SlickMessageId == message.MessageId));
            context.SaveChanges();

            if (!message.MessageIsGlobal)
            {
                foreach (var user in messageUsers)
                {
                    context.SlickMessageUsers.Add(new SlickMessageUser { UserId = user, SlickMessageId = message.MessageId });
                }
                foreach (var team in messageTeams)
                {
                    context.SlickMessageTeams.Add(new SlickMessageTeam { TeamId = team, SlickMessageId = message.MessageId });
                }
                context.SaveChanges();
            }

        }
        public int AddNewMessage(UserCustomEntities.MessageView message, List<int> messageUsers, List<int> messageTeams)
        {


            var newMessage = context.SlickMessages.Add
                (
                    new SlickMessage
                    {
                        Subject = message.MessageSubject,
                        MessageBody = message.MessageBody,
                        MessageByUserId = GlobalVars.CurrentUser.UserId,
                        MessageIsGlobal = message.MessageIsGlobal,
                        SlickMessageTypeId = message.MessageTypeId,
                        MessageDate = DateTime.Now,
                        MessageExpiryDate = message.MessageExpiryDate,
                        IsPinned = message.MessageIsPinned,
                        RelatesToMatterId = message.MatterId,
                        RelatesToLenderId = message.RelatesToLenderId,
                        RelatesToUserId = message.RelatesToUserId
                    }
                );
            context.SaveChanges();

            if (!message.MessageIsGlobal)
            {
                foreach (var user in messageUsers)
                {
                    context.SlickMessageUsers.Add(new SlickMessageUser { UserId = user, SlickMessageId = newMessage.SlickMessageId });
                }
                foreach (var team in messageTeams)
                {
                    context.SlickMessageTeams.Add(new SlickMessageTeam { TeamId = team, SlickMessageId = newMessage.SlickMessageId });
                }
                context.SaveChanges();

            }

            return newMessage.SlickMessageId;
        }


        public List<UserCustomEntities.MessageView> GetUserMessages(int userId)
        {
            List<UserCustomEntities.MessageView> userMessages = new List<UserCustomEntities.MessageView>();
            List<int> messageIdsToGet = new List<int>();
            //var userMessageIds = context.SlickMessageUsers.Where(u => u.UserId == userId).Select(m => m.SlickMessageId).ToList();

            //messageIdsToGet = messageIdsToGet.Concat(userMessageIds).ToList();

            //var teams = context.UserTeams.Where(u => u.UserId == userId).Select(t => t.TeamId).ToList();
            //var teamMessageIds = context.SlickMessageTeams.Where(u => teams.Contains(u.TeamId)).Select(m => m.SlickMessageId).ToList();

            //messageIdsToGet = messageIdsToGet.Concat(teamMessageIds).ToList();

            //messageIdsToGet = messageIdsToGet.Distinct().ToList();

            var filterDate = DateTime.Now.Date;

            userMessages = context.SlickMessages.Where(m => (!m.MessageExpiryDate.HasValue || m.MessageExpiryDate.Value > DateTime.Now))
                .Select(y => new UserCustomEntities.MessageView
                {
                    MessageId = y.SlickMessageId,
                    MessageSubject = y.Subject,
                    MessageBody = y.MessageBody,
                    MessageIsGlobal = y.MessageIsGlobal,
                    MessageIsPinned = y.IsPinned,
                    MessageTypeId = y.SlickMessageTypeId,
                    MessageTypeName = y.SlickMessageType.SlickMessageTypeName,
                    MessageDate = y.MessageDate,
                    MessageByName = y.User1.Fullname,
                    LikeCount = y.SlickMessageLikes.Count(),
                    MessageLiked = y.SlickMessageLikes.Any(u => u.UserId == userId),
                    TeamUserIds =  y.SlickMessageTeams.SelectMany(t => t.Team.UserTeams.Select(u => u.UserId)).ToList(),
                    SpecificUserIds = y.SlickMessageUsers.Select(u => u.UserId).ToList()
                }).ToList().Where(m=> m.MessageIsGlobal || m.TeamUserIds.Contains(userId) || m.SpecificUserIds.Contains(userId))
                .OrderByDescending(o => o.MessageIsPinned).ThenByDescending(x => x.MessageDate)
                .ToList();

            return userMessages;

        }

        public IEnumerable<UserCustomEntities.ActiveUserGridView> GetActiveUsers(int workWeekId, bool todayOnly = true)
        {
            //changed this function around to do the more complex user filtering on the client end rather than the database
            DateTime cutoffDate = DateTime.Now.Date;
            return context.Users.Where(l => l.LastLoginDate.HasValue && (!todayOnly || l.LastLoginDate.Value >= cutoffDate) && l.Enabled && l.UserTypeId == (int)UserTypeEnum.Internal)
                .Select(u => new
                {
                    u.UserId,
                    u.Firstname,
                    u.Lastname,
                    u.Email,
                    u.OfficeExt,
                    u.Phone,
                    u.LastLoginDate,
                    u.WorkingFromHome,
                    UserHasSkills = u.UserSkills1.Any(),
                    u.Notes,
                    u.State.StateName,
                    u.StateId,
                    u.UserIsFulltime,
                    u.EmploymentStatusTypeId,
                    UserWorkDays = u.UserWorkDays.Where(w=>w.WorkWeekId == workWeekId).Select(d => new { d.WorkWeekId, d.DayIndex, UserWorkDayHours = d.UserWorkDayHours.Select(h=>h.TimeIndex).ToList(), d.IsFullDay }),
                    UserSkills1 = u.UserSkills1.Select(s => new { s.SkillType.SkillTypeName, s.SpecificLenderId, s.Lender.LenderName, s.SkillTypeId, s.ParentSkillId, ChildSkills = s.UserSkill1.Select(c=> new { c.SkillType.SkillTypeName, c.SkillTypeId, c.Notes }).ToList()}).ToList()
                })
                .ToList()
                .Select(u => new UserCustomEntities.ActiveUserGridView
                {
                    UserId = u.UserId,
                    UserName = (u.Firstname?.Trim() + " " + u.Lastname?.Trim())?.Trim(),
                    EmailAddress = u.Email,
                    PhoneNumber = !String.IsNullOrEmpty(u.OfficeExt) ? u.OfficeExt : u.Phone,
                    LastLoginDate = u.LastLoginDate.Value,
                    WorkingFromHome = u.WorkingFromHome,
                    UserHasSkills = u.UserHasSkills,
                    Role = u.Notes,
                    StateName = u.StateName,
                    StateId = u.StateId,
                    EmploymentStatusTypeId = u.EmploymentStatusTypeId,
                    UserWorkDays = u.UserWorkDays.Select(d => new UserCustomEntities.WorkDayView(d.DayIndex, Enum.GetName(typeof(DayOfWeek), d.DayIndex), d.IsFullDay) { Hours = d.UserWorkDayHours.Select(h => new WorkHours(d.DayIndex, (float)h)).ToList() }).ToList(),
                    UserParentSkills = u.UserSkills1.Where(x => !x.ParentSkillId.HasValue)
                        .Select(s => new UserCustomEntities.UserSkillView()
                        {
                            SkillName = s.SkillTypeName,
                            IsLenderSpecific = s.SpecificLenderId.HasValue,
                            LenderId = s.SpecificLenderId,
                            LenderName = s.SpecificLenderId.HasValue ? s.LenderName : "",
                            SkillTypeId = s.SkillTypeId,
                            HasChildSkills = s.ChildSkills.Any(),
                            ChildSkills = s.ChildSkills.Select(c => new UserCustomEntities.UserSkillChildView()
                            {
                                SkillTypeId = c.SkillTypeId,
                                SkillName = c.SkillTypeName,
                                SkillNotes = c.Notes
                            }).ToList()
                        }).ToList()

                }).ToList().OrderByDescending(s=>s.StateId == GlobalVars.CurrentUser.StateId).ThenBy(s=>s.StateName);
        }

        public IEnumerable<GeneralCustomEntities.GeneralCheckList> GetGeneralCheckList(bool defValue, bool inclSelectAll, bool inclNullSelect)
        {
            try
            {
                var liTmp = context.Users.Where(u => !u.UserType.IsExternal && u.Enabled)
                            .Select(l => new { l.UserId, l.Fullname })
                            .ToList()
                            .Select(l => new GeneralCustomEntities.GeneralCheckList(defValue, l.UserId, l.Fullname))
                            .OrderBy(x => x.Name)
                            .ToList();


                if (inclSelectAll)
                    liTmp.Insert(0, new GeneralCustomEntities.GeneralCheckList(defValue, -1, "-- Select All Users --"));

                if (inclNullSelect)
                    liTmp.Add(new GeneralCustomEntities.GeneralCheckList(defValue, 0, "-- No User --"));

                return liTmp;
            }
            catch (Exception ex)
            {
                Handlers.ErrorHandler.LogError(ex);
                return null;
            }
        }

        public List<LookupValue> GetLookupList()
        {
            return (from u in context.Users
                    select new LookupValue() { id = u.UserId, value = u.Fullname }).ToList();
        }

        public int AddNewNews(UserCustomEntities.MSANewsView news)
        {


            var newNews = context.MSANews.Add(
                                new MSANew
                                {
                                    Title = news.Title,
                                    Content = news.Content,
                                    Visible = news.Visible,
                                    NewsDisplayDate = Convert.ToDateTime(news.NewsDisplayDate),
                                    RelevantStartDate = news.RelevantStartDate,
                                    RelevantEndDate = news.RelevantEndDate,
                                    NewsCreatedByUserId = GlobalVars.CurrentUser.UserId,
                                    NewsCreatedDate = DateTime.Now,
                                    NewsUpdatedByUserId = GlobalVars.CurrentUser.UserId,
                                    NewsUpdatedDate = DateTime.Now,
                                    NewsSummary = news.NewsSummary,
                                    DisplayNewsDate = news.DisplayNewsDate,
                                    SEOContent = news.SEOContent,
                                    Pinned = news.Pinned
                                }
                            );
            context.SaveChanges();



            return newNews.MSANewsId;
        }

        //public void SaveExistingNews(UserCustomEntities.MSANewsView news)
        //{
        //    var existingNews = context.MSANews.Where(m => m.MSANewsId == news.MSANewsId).FirstOrDefault();

        //    existingNews.Title = news.Title;
        //    existingNews.Content = news.Content;
        //    existingNews.Visible = news.Visible;
        //    existingNews.NewsDisplayDate = Convert.ToDateTime(news.NewsDisplayDate);
        //    existingNews.RelevantStartDate = news.RelevantStartDate;
        //    existingNews.RelevantEndDate = news.RelevantEndDate;
        //    existingNews.NewsUpdatedByUserId = GlobalVars.CurrentUser.UserId;
        //    existingNews.NewsUpdatedDate = DateTime.Now;
        //    existingNews.NewsSummary = news.NewsSummary;
        //    existingNews.DisplayNewsDate = news.DisplayNewsDate;
        //    existingNews.Pinned = news.Pinned;

        //    context.SaveChanges();


        //}

        public void SaveExistingNews(UserCustomEntities.MSANewsView news)
        {
            var existingNews = context.MSANews.Where(m => m.MSANewsId == news.MSANewsId).FirstOrDefault();

            existingNews.Title = news.Title;
            existingNews.Content = news.Content;
            existingNews.Visible = news.Visible;
            existingNews.NewsDisplayDate = Convert.ToDateTime(news.NewsDisplayDate);
            existingNews.RelevantStartDate = news.RelevantStartDate;
            existingNews.RelevantEndDate = news.RelevantEndDate;

            if (existingNews.NewsCreatedByUserId == null)
            {
                if (!string.IsNullOrEmpty(news.NewsCreatedByCustomName) && !string.IsNullOrWhiteSpace(news.NewsCreatedByCustomName))
                {
                    existingNews.NewsCreatedByUserId = null;
                }
                else existingNews.NewsCreatedByUserId = GlobalVars.CurrentUser.UserId;
            }
            else
            {
                if (!string.IsNullOrEmpty(news.NewsCreatedByCustomName) && !string.IsNullOrWhiteSpace(news.NewsCreatedByCustomName))
                {
                    existingNews.NewsCreatedByUserId = null;
                }
            }

            existingNews.NewsUpdatedByUserId = GlobalVars.CurrentUser.UserId;
            existingNews.NewsUpdatedDate = DateTime.Now;
            existingNews.NewsSummary = news.NewsSummary;
            existingNews.DisplayNewsDate = news.DisplayNewsDate;
            existingNews.NewsCreatedByCustomName = news.NewsCreatedByCustomName;
            existingNews.SEOContent = news.SEOContent;
            existingNews.Pinned = news.Pinned;
            context.SaveChanges();
        }

        public void DeleteNews(int newsId)
        {
            context.MSANews.RemoveRange(context.MSANews.Where(m => m.MSANewsId == newsId));
            context.SaveChanges();


        }


        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                context.Dispose();
                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion

        public User GetUserByPredicate(Predicate<User> pred)
        {
            return context.Users.FirstOrDefault(u => pred(u));
        }
        public User GetUserByUserName(string username)
        {

            return context.Users.FirstOrDefault(u => u.Username.ToUpper().Trim() == username.ToUpper().Trim());
        }

        public UserInfo GetUserInfoByUserName(string username)
        {

            var user = (from u in context.Users
                        where u.Username == username && u.Enabled
                        select new UserInfo
                        {
                            UserId = u.UserId,
                            Username = u.Username,
                            Name = u.Firstname + " " + u.Lastname,
                            UserTypeId = u.UserType.UserTypeId,
                            UserType = new UserTypeInfo { UserTypeId = (UserTypeEnum)u.UserType.UserTypeId, Name = u.UserType.UserTypeName, IsExternal = u.UserType.IsExternal },
                            State = new StateInfo { id = u.State.StateId, value = u.State.StateName },
                            BrokerId = u.BrokerId,
                            BrokerName = u.Broker != null ? u.Broker.CompanyName : string.Empty,
                            LenderId = u.LenderId,
                            LenderName = u.Lender != null ? u.Lender.LenderName : string.Empty,
                            MortManagerId = u.MortMgrId,
                            MortManagerName = u.MortMgr != null ? u.MortMgr.MortMgrName : string.Empty,
                            Email = u.Email,
                            Mobile = u.Mobile,
                            PwdSalt = u.PasswordSalt,
                            Password = u.Password,
                            FailedLogins = u.FailedLogins,
                            FailedLoginBanDate = u.FailedLoginBanDate,
                            PwdResetCode = u.PwdResetCode,
                            PwdResetExpiry = u.PwdResetExpiry,
                            LastLoginDate = u.LastLoginDate,
                            RelationshipManagerId = u.RelationshipManagerId,
                            StateId = u.StateId,
                            SSORequired = u.SSORequired
                        }).FirstOrDefault();

            return user;
        }

        public bool Update(User user)
        {
            try
            {

                context.Users.Attach(user);
                context.SaveChanges();

                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public bool WebUserValid(string userName)
        {
            return context.Users.Any(u => u.Username == userName && u.UserTypeId != (int)UserTypeEnum.Internal
                                            && u.Enabled);
        }

        public string GenerateForgottenPasswordCode(string username)
        {
            try
            {
                var userRec = context.Users.FirstOrDefault(u => u.Username == username && u.Enabled);

                if (userRec == null)
                    return null;

                var tmp = Slick_Domain.GlobalVars.GetGlobalTxtVar("PwdResetExpiryMinutes", context);

                //------------------------------------------------------------------------------------------------
                //RQ: should check if there's an existing password code, return it again if there is and extend the time. 
                //check if there's already a non-expired code for the user
                if (userRec.PwdResetExpiry != null)
                {
                    if (userRec.PwdResetExpiry < DateTime.Now)
                    {
                        //don't generate code, but extend the time on the existing code and return it again 
                        if (int.TryParse(tmp, out int oldPwdExp))
                            userRec.PwdResetExpiry = DateTime.Now.AddMinutes(oldPwdExp);
                        else
                            userRec.PwdResetExpiry = DateTime.Now.AddMinutes(20);

                        context.SaveChanges();

                        return userRec.PwdResetCode;
                    }
                }
                //--------------------------------------------------------------------------------------------   

                Random rNum = new Random();
                var code = rNum.Next(1000000).ToString("000000");



                var hashCode = Slick_Domain.Common.SlickSecurity.ComputeHash(code, Convert.FromBase64String(userRec.PasswordSalt));

                userRec.PwdResetCode = hashCode;
                if (int.TryParse(tmp, out int pwdExp))
                    userRec.PwdResetExpiry = DateTime.Now.AddMinutes(pwdExp);
                else
                    userRec.PwdResetExpiry = DateTime.Now.AddMinutes(20);
                context.SaveChanges();

                return code;
            }
            catch (Exception ex)
            {
                Handlers.ErrorHandler.LogError(ex);
                return null;
            }
        }
        //
        public IEnumerable<UserCustomEntities.UserGridView> GetLenderListForUser(User user, bool? Enabled)
        {

            var tmp = context.Users
                     .Select(u => new
                     {
                         u.UserId,
                         u.Username,
                         u.Enabled,
                         u.Lastname,
                         u.Firstname,
                         u.DisplayInitials,
                         u.State.StateName,
                         u.Email,
                         u.UserTypeId,
                         u.UserType.UserTypeName,
                         u.UserType.IsExternal,
                         u.LenderId,
                         u.Lender.LenderName,
                         u.MortMgr.MortMgrName,
                         BrokerLastname = u.Broker.PrimaryContact.Lastname,
                         BrokerFirstname = u.Broker.PrimaryContact.Firstname,
                         u.FailedLogins,
                         u.LastLoginDate,
                         u.UpdatedDate,
                         u.UpdatedByUserId,
                         UpdatedByUsername = u.User2.Username
                     });
            //}).Where(x => x.LenderId == user.LenderId && x.UserTypeId == (int)UserTypeEnum.Lender && x.UserId != user.UserId);
            if (Enabled != null)
            {
                tmp = tmp.Where(x => x.LenderId == user.LenderId && x.UserTypeId == (int)UserTypeEnum.Lender && x.UserId != user.UserId && x.Enabled == Enabled);
            }
            else
            {
                tmp = tmp.Where(x => x.LenderId == user.LenderId && x.UserTypeId == (int)UserTypeEnum.Lender && x.UserId != user.UserId);
            }
            var results = tmp.ToList().Select(u => new UserCustomEntities.UserGridView(u.UserId, u.Username, u.Enabled,
                                       EntityHelper.GetFullName(u.Lastname, u.Firstname), u.DisplayInitials, u.StateName, u.Email, u.LastLoginDate, u.UserTypeName,
                                       GetCompanyName(u.UserTypeId, u.LenderName, u.MortMgrName, EntityHelper.GetFullName(u.BrokerLastname, u.BrokerFirstname)), u.FailedLogins, u.UpdatedDate, u.UpdatedByUserId, u.UpdatedByUsername));
            return results;

        }
        public int SaveUser(int pUserId, string pUsername, bool pEnabled, string pLastname, string pFirstname, string pDisplayInitials, int pStateId, string pStateName,
                    int pUserTypeId, string pUserTypeName, int? pLenderId, string pLenderName, int? pMortMgrId, string pMortMgrName, int? pBrokerId,
                    string pBrokerName, string pEmail, string pPhone, string pMobile, string pFax, string pOfficeExt, DateTime? pBirthday, byte[] pProfilePhoto, DateTime? pLastLoginDate, string pPasswordSalt, int? pFailedLogins, DateTime? pFailedLoginBanDate, Guid? pPwdResetGuid,
                    DateTime? pPwdResetGuidExpiry, string pNotes, int pUpdatedByUserId, string pUpdatedByUsername, bool pHasStateRestrictions, bool pHideFromContacts, bool pPrivateNotesDefault, string pPreferredNoteBodyColour, string pPreferredNoteAccentColor,
                    int? pRelationshipManagerId, bool pWorkingFromHome, bool pHasMatterTypeRestrictions, int[] pStateRestrictions, int[] pMatterTypeRestrictions, int[] pUserPrivileges)
        {
            var uow = new UnitOfWork();
            User usr;
            int UserId = 0;
            if (pUserId > 0)
                usr = uow.Context.Users.FirstOrDefault(u => u.UserId == pUserId);
            else
                usr = new User();

            //CopyToUser(usr);
            usr.UserId = pUserId;
            usr.Username = pUsername;
            usr.Enabled = pEnabled;
            usr.Lastname = pLastname;
            usr.Firstname = pFirstname;
            usr.DisplayInitials = pDisplayInitials;
            usr.StateId = pStateId;
            //usr.StateName = pStateName;
            usr.UserTypeId = pUserTypeId;
            //usr.UserTypeName = pUserTypeName;
            usr.HasMatterTypeRestrictions = pHasMatterTypeRestrictions;
            usr.LenderId = pLenderId;
            //usr.LenderName = pLenderName;
            usr.MortMgrId = pMortMgrId;
            //usr.MortMgrName = pMortMgrName;
            usr.RelationshipManagerId = pRelationshipManagerId;
            usr.BrokerId = pBrokerId;
            //usr.BrokerName = pBrokerName;
            usr.Notes = pNotes;
            usr.HideFromContacts = pHideFromContacts;
            usr.WorkingFromHome = pWorkingFromHome;
            usr.Email = pEmail;
            usr.Phone = pPhone;
            usr.Mobile = pMobile;
            usr.Fax = pFax;
            usr.OfficeExt = pOfficeExt;
            usr.Birthday = pBirthday;
            usr.ProfilePhoto = pProfilePhoto;
            usr.LastLoginDate = pLastLoginDate;
            usr.PasswordSalt = pPasswordSalt;
            usr.FailedLogins = pFailedLogins;
            usr.FailedLoginBanDate = pFailedLoginBanDate;
            usr.PwdResetGuid = pPwdResetGuid;
            usr.PwdResetExpiry = pPwdResetGuidExpiry;
            usr.UpdatedDate = DateTime.Now;//pUpdatedDate;
            usr.UpdatedByUserId = pUpdatedByUserId;
            //usr.UpdatedByUsername = pUpdatedByUsername;
            usr.HasStateRestrictions = pHasStateRestrictions;
            usr.PreferredNoteBodyColor = !String.IsNullOrEmpty(pPreferredNoteBodyColour) ? pPreferredNoteBodyColour : Constants.GlobalConstants.DefaultStickyNoteBody;
            usr.PreferredNoteAccentColor = !String.IsNullOrEmpty(pPreferredNoteAccentColor) ? pPreferredNoteAccentColor : Constants.GlobalConstants.DefaultStickyNoteAccent;
            //UpdatedByString = "Updated by " + UpdatedByUsername + " on " + UpdatedDate.ToString("dd-MMM-yy hh:mm tt");
            //UserPrivileges = new UserPrivilegesClass();

            if (usr.UserId == 0)
            {
                if (uow.Context.Users.Select(u => new { u.Email, u.UserTypeId, u.Enabled }).Where(u => u.Email == usr.Email && u.Enabled == usr.Enabled && u.UserTypeId != (int)Slick_Domain.Enums.UserTypeEnum.Internal).FirstOrDefault() == null)
                {
                    if (usr.UserTypeId == (int)Slick_Domain.Enums.UserTypeEnum.Broker || usr.UserTypeId == (int)Slick_Domain.Enums.UserTypeEnum.Lender || usr.UserTypeId == (int)Slick_Domain.Enums.UserTypeEnum.MortMgr)
                    {
                        //check if they have an email address, and if they are a broker, that they have a mobile.
                        if (usr.Email == "" || usr.Email == null || (usr.Mobile == "" && usr.LenderId == null && usr.MortMgrId == null) || (usr.Mobile == null && usr.LenderId == null && usr.MortMgrId == null))
                        {
                            throw new Exception("Loantrak account could not be created, either mobile or email not entered.");
                        }
                        //If we are making a broker or lender, they need to have a loantrak account created. 
                        string tempPassword = $"MSA_LT_{SlickSecurity.RandomString(5)}";
                        Random rNum = new Random();
                        string saltedPassword = tempPassword;
                        var saltString = string.Empty;
                        SlickSecurity.CreatePassword(ref saltedPassword, ref saltString);
                        usr.Password = saltedPassword;
                        usr.PasswordSalt = saltString;

                        uow.Context.Users.Add(usr);
                        uow.Save();
                        //uow.CommitTransaction();
                        //EmailsService.SendTempPasswordEmail(usr, tempPassword, usr.UserTypeId == (int)Slick_Domain.Enums.UserTypeEnum.Lender);
                        EmailsService.SendTempPasswordEmail(usr, tempPassword, false);
                        //SlickMessageBoxes.ShowShortInfoMessage("Loantrak Account Created and Email Sent", "User has been sent Loantrak email and new password.");
                        UserId = usr.UserId;
                    }
                    else
                    {
                        uow.Context.Users.Add(usr);
                        uow.Save();
                        UserId = usr.UserId;
                    }
                }
                else
                {
                    throw new Exception("Duplicated username");
                }
            }
            if (pUserId > 0) UserId = pUserId;
            //if (Model.UserId.HasValue)
            //{
            //    var rep = uow.GetRepositoryInstance<User>();
            //    var userEntity = uow.GetRepositoryInstance<User>().AllAsQueryNoTracking.FirstOrDefault(x => x.UserId == usr.UserId);
            //    if (userEntity == null || !CheckRefresh(userEntity.UpdatedDate, Model.UpdatedDate)) return false;
            //}
            if (UserId > 0)
            {
                if (pUserPrivileges != null) UpdateExternalUserPrivileges(uow, UserId, pUserPrivileges);
                UpdateUserStateRestrictions(uow, UserId, pHasStateRestrictions, pStateRestrictions);
                UpdateUserMatterTypeRestrictions(uow, UserId, pHasMatterTypeRestrictions, pMatterTypeRestrictions, pUpdatedByUserId);

            }
            uow.Save();
            //uow.CommitTransaction();
            //GlobalVars.RefreshCurrentUser(uow);
            //return true;
            return UserId;
        }

        public int SaveSSOUser(int pUserId, string pUsername, bool pEnabled, string pLastname, string pFirstname, string pDisplayInitials, int pStateId, string pStateName,
                    int pUserTypeId, string pUserTypeName, int? pLenderId, string pLenderName, int? pMortMgrId, string pMortMgrName, int? pBrokerId,
                    string pBrokerName, string pEmail, string pPhone, string pMobile, string pFax, string pOfficeExt, DateTime? pBirthday, byte[] pProfilePhoto, DateTime? pLastLoginDate, string pPasswordSalt, int? pFailedLogins, DateTime? pFailedLoginBanDate, Guid? pPwdResetGuid,
                    DateTime? pPwdResetGuidExpiry, string pNotes, int pUpdatedByUserId, string pUpdatedByUsername, bool pHasStateRestrictions, bool pHideFromContacts, bool pPrivateNotesDefault, string pPreferredNoteBodyColour, string pPreferredNoteAccentColor,
                    int? pRelationshipManagerId, bool pWorkingFromHome, bool pHasMatterTypeRestrictions, int[] pStateRestrictions, int[] pMatterTypeRestrictions, int[] pUserPrivileges, bool pSSORequired)
        {
            var uow = new UnitOfWork();
            User usr;
            int UserId = 0;
            if (pUserId > 0)
                usr = uow.Context.Users.FirstOrDefault(u => u.UserId == pUserId);
            else
                usr = new User();

            //CopyToUser(usr);
            usr.UserId = pUserId;
            usr.Username = pUsername;
            usr.Enabled = pEnabled;
            usr.Lastname = pLastname;
            usr.Firstname = pFirstname;
            //usr.DisplayInitials = pDisplayInitials;
            usr.StateId = pStateId;
            //usr.StateName = pStateName;
            usr.UserTypeId = pUserTypeId;
            //usr.UserTypeName = pUserTypeName;
            usr.HasMatterTypeRestrictions = pHasMatterTypeRestrictions;
            usr.LenderId = pLenderId;
            //usr.LenderName = pLenderName;
            usr.MortMgrId = pMortMgrId;
            //usr.MortMgrName = pMortMgrName;
            usr.RelationshipManagerId = pRelationshipManagerId;
            usr.BrokerId = pBrokerId;
            //usr.BrokerName = pBrokerName;
            //usr.Notes = pNotes;
            //usr.HideFromContacts = pHideFromContacts;
            //usr.WorkingFromHome = pWorkingFromHome;
            usr.Email = pEmail;
            //usr.Phone = pPhone;
            usr.Mobile = pMobile;
            //usr.Fax = pFax;
            // usr.OfficeExt = pOfficeExt;
            //usr.Birthday = pBirthday;
            //usr.ProfilePhoto = pProfilePhoto;
            //usr.LastLoginDate = pLastLoginDate;
            //usr.PasswordSalt = pPasswordSalt;
            //usr.FailedLogins = pFailedLogins;
            //usr.FailedLoginBanDate = pFailedLoginBanDate;
            //usr.PwdResetGuid = pPwdResetGuid;
            //usr.PwdResetExpiry = pPwdResetGuidExpiry;
            usr.UpdatedDate = DateTime.Now;//pUpdatedDate;
            usr.UpdatedByUserId = pUpdatedByUserId;
            //usr.UpdatedByUsername = pUpdatedByUsername;
            usr.HasStateRestrictions = pHasStateRestrictions;
            //usr.PreferredNoteBodyColor = !String.IsNullOrEmpty(pPreferredNoteBodyColour) ? pPreferredNoteBodyColour : Constants.GlobalConstants.DefaultStickyNoteBody;
            //usr.PreferredNoteAccentColor = !String.IsNullOrEmpty(pPreferredNoteAccentColor) ? pPreferredNoteAccentColor : Constants.GlobalConstants.DefaultStickyNoteAccent;
            //UpdatedByString = "Updated by " + UpdatedByUsername + " on " + UpdatedDate.ToString("dd-MMM-yy hh:mm tt");
            //UserPrivileges = new UserPrivilegesClass();
            usr.SSORequired = pSSORequired;


            if (usr.UserId == 0)
            {
                if (uow.Context.Users.Select(u => new { u.Email, u.UserTypeId, u.Enabled }).Where(u => u.Email == usr.Email && u.Enabled == usr.Enabled && u.UserTypeId != (int)Slick_Domain.Enums.UserTypeEnum.Internal).FirstOrDefault() == null)
                {
                    if (usr.UserTypeId == (int)Slick_Domain.Enums.UserTypeEnum.Broker || usr.UserTypeId == (int)Slick_Domain.Enums.UserTypeEnum.Lender || usr.UserTypeId == (int)Slick_Domain.Enums.UserTypeEnum.MortMgr)
                    {
                        //check if they have an email address, and if they are a broker, that they have a mobile.
                        if (usr.Email == "" || usr.Email == null || (usr.Mobile == "" && usr.LenderId == null && usr.MortMgrId == null) || (usr.Mobile == null && usr.LenderId == null && usr.MortMgrId == null))
                        {
                            throw new Exception("Loantrak account could not be created, either mobile or email not entered.");
                        }
                        //If we are making a broker or lender, they need to have a loantrak account created. 
                        string tempPassword = $"MSA_LT_{SlickSecurity.RandomString(5)}";
                        Random rNum = new Random();
                        string saltedPassword = tempPassword;
                        var saltString = string.Empty;
                        SlickSecurity.CreatePassword(ref saltedPassword, ref saltString);
                        usr.Password = null;//saltedPassword;
                        usr.PasswordSalt = saltString;

                        uow.Context.Users.Add(usr);
                        uow.Save();
                        //uow.CommitTransaction();
                        //EmailsService.SendTempPasswordEmail(usr, tempPassword, usr.UserTypeId == (int)Slick_Domain.Enums.UserTypeEnum.Lender);
                        //SlickMessageBoxes.ShowShortInfoMessage("Loantrak Account Created and Email Sent", "User has been sent Loantrak email and new password.");
                        UserId = usr.UserId;
                    }
                    else
                    {
                        uow.Context.Users.Add(usr);
                        uow.Save();
                        UserId = usr.UserId;
                    }
                }
                else
                {
                    throw new Exception("Duplicated username");
                }
            }
            if (pUserId > 0) UserId = pUserId;
            //if (Model.UserId.HasValue)
            //{
            //    var rep = uow.GetRepositoryInstance<User>();
            //    var userEntity = uow.GetRepositoryInstance<User>().AllAsQueryNoTracking.FirstOrDefault(x => x.UserId == usr.UserId);
            //    if (userEntity == null || !CheckRefresh(userEntity.UpdatedDate, Model.UpdatedDate)) return false;
            //}
            if (UserId > 0)
            {
                //var usrToUpdate = context.Users.FirstOrDefault(x => x.UserId == UserId);
                //if (usrToUpdate != null)
                //{
                //    usr.Firstname = pFirstname;
                //    usr.Lastname = pLastname;
                //    usr.UpdatedByUserId = GlobalVars.CurrentUser.UserId;
                //    usr.UpdatedDate = DateTime.Now;
                //    context.SaveChanges();
                //}                

                if (pUserPrivileges != null) UpdateExternalUserPrivileges(uow, UserId, pUserPrivileges);
                UpdateUserStateRestrictions(uow, UserId, pHasStateRestrictions, pStateRestrictions);
                UpdateUserMatterTypeRestrictions(uow, UserId, pHasMatterTypeRestrictions, pMatterTypeRestrictions, pUpdatedByUserId);

            }
            uow.Save();
            //uow.CommitTransaction();
            //GlobalVars.RefreshCurrentUser(uow);
            //return true;
            return UserId;
        }
        private void UpdateExternalUserPrivileges(UnitOfWork uow, int UserId, int[] UserPrivileges)
        {
            uow.Context.ExternalUserPrivileges.RemoveRange(uow.Context.ExternalUserPrivileges.Where(u => u.UserId == UserId));
            uow.Save();
            foreach (var priv in UserPrivileges)
            {
                uow.Context.ExternalUserPrivileges.Add(new ExternalUserPrivilege() { UserId = UserId, ExternalUserPrivilegeTypeId = priv });
            }
            uow.Save();
        }
        private void UpdateUserStateRestrictions(UnitOfWork uow, int UserId, bool HasStateRestrictions, int[] StateRestrictions)
        {
            var toRemove = uow.Context.UserStateRestrictions.Where(x => x.UserId == UserId);
            uow.Context.UserStateRestrictions.RemoveRange(toRemove);
            uow.Save();

            if (HasStateRestrictions)
            {
                foreach (var state in StateRestrictions)
                {
                    uow.Context.UserStateRestrictions.Add(new UserStateRestriction() { UserId = UserId, StateId = state });
                    uow.Save();
                }
                User user = uow.Context.Users.Where(u => u.UserId == UserId).First();
                user.HasStateRestrictions = true;
                uow.Save();
            }
        }
        private void UpdateUserMatterTypeRestrictions(UnitOfWork uow, int UserId, bool HasMatterTypeRestrictions, int[] TypeRestrictionChecklist, int UpdatedByUserId)
        {
            if (!HasMatterTypeRestrictions)
            {
                uow.Context.UserMatterTypeRestrictions.RemoveRange(uow.Context.UserMatterTypeRestrictions.Where(u => u.UserId == UserId));
            }
            else
            {
                var toRemove = uow.Context.UserMatterTypeRestrictions.Where(x => x.UserId == UserId);
                uow.Context.UserMatterTypeRestrictions.RemoveRange(toRemove);
                //foreach (var checkedItem in TypeRestrictionChecklist.Where(x => x.IsChecked))
                foreach (var checkedItem in TypeRestrictionChecklist)
                {
                    //if (checkedItem.Id > 0)
                    //{
                    //uow.Context.UserMatterTypeRestrictions.Add(new UserMatterTypeRestriction() { UserId = UserId, MatterGroupTypeId = checkedItem.Id, UpdatedByUserId = GlobalVars.CurrentUser.UserId, UpdatedDate = DateTime.Now });
                    uow.Context.UserMatterTypeRestrictions.Add(new UserMatterTypeRestriction() { UserId = UserId, MatterGroupTypeId = checkedItem, UpdatedByUserId = UpdatedByUserId, UpdatedDate = DateTime.Now });
                    //}
                }
            }
            uow.Save();
        }
        //public User CopyToUser(User usr)
        //{
        //    usr.Username = Username;
        //    usr.Enabled = Enabled;
        //    usr.Lastname = Lastname;
        //    usr.Firstname = Firstname;
        //    usr.DisplayInitials = DisplayInitials;
        //    usr.StateId = StateId.Value;
        //    usr.UserTypeId = DetermineUserType();
        //    usr.LenderId = IsLender ? LenderId : null;
        //    usr.MortMgrId = IsMortMgr ? MortMgrId : null;
        //    usr.BrokerId = IsBroker ? BrokerId : null;
        //    usr.RelationshipManagerId = IsRelationshipManager ? RelationshipManagerId : null;
        //    usr.Email = Email;
        //    usr.Phone = Phone;
        //    usr.Mobile = Mobile;
        //    usr.Fax = Fax;
        //    usr.OfficeExt = OfficeExt;
        //    usr.Birthday = Birthday;
        //    usr.WorkingFromHome = WorkingFromHome;
        //    usr.HasMatterTypeRestrictions = HasMatterTypeRestrictions;
        //    //usr.ProfilePhoto = ProfilePhoto;
        //    usr.HasStateRestrictions = HasBDMRestrictions;
        //    usr.LastLoginDate = LastLoginDate;
        //    usr.FailedLogins = FailedLogins;
        //    usr.FailedLoginBanDate = FailedLoginBanDate;
        //    usr.PasswordSalt = PasswordSalt;
        //    usr.Notes = Notes;
        //    usr.HideFromContacts = HideFromContacts;
        //    usr.DefaultPrivateNotes = PrivateNotesDefault;
        //    usr.UpdatedDate = DateTime.Now;
        //    usr.UpdatedByUserId = GlobalVars.CurrentUser.UserId;
        //    UpdatedDate = DateTime.Now;
        //    UpdatedByUserId = usr.UpdatedByUserId;
        //    return usr;
        //}

        public UserCustomEntities.UserDetails GetLenderUserDetails(int userId)
        {
            var retVal = context.Users
               .Where(u => u.UserId == userId)
            .Select(u => new
            {
                u.UserId,
                u.Username,
                u.Enabled,
                u.Lastname,
                u.Firstname,
                u.DisplayInitials,
                u.StateId,
                u.State.StateName,
                u.UserTypeId,
                u.UserType.UserTypeName,
                u.LenderId,
                u.Lender.LenderName,
                u.MortMgrId,
                u.MortMgr.MortMgrName,
                u.BrokerId,
                BrokerLastname = u.Broker.PrimaryContact.Lastname,
                BrokerFirstname = u.Broker.PrimaryContact.Firstname,
                u.Email,
                u.Phone,
                u.Mobile,
                u.PreferredNoteBodyColor,
                u.PreferredNoteAccentColor,
                u.Fax,
                u.OfficeExt,
                u.Birthday,
                u.ProfilePhoto,
                u.LastLoginDate,
                u.PasswordSalt,
                u.FailedLogins,
                u.FailedLoginBanDate,
                u.PwdResetGuid,
                u.PwdResetExpiry,
                u.Notes,
                u.UpdatedDate,
                u.UpdatedByUserId,
                UpdatedByUsername = u.User2.Username,
                u.HasStateRestrictions,
                u.HideFromContacts,
                u.DefaultPrivateNotes,
                u.RelationshipManagerId,
                u.WorkingFromHome,
                u.HasMatterTypeRestrictions,
                u.SignatureFont,
                u.SignatureInitialOnly,
                u.OffshoreUser,
                u.SSORequired, 
                u.UserIsFulltime,
                u.EmploymentStatusTypeId,
                u.ExternalEmail
                //,
                //UserStateRestriction = u.HasStateRestrictions == true ?
                //                       u.UserStateRestrictions.Select(t => new { t.UserStateRestrictionId, t.StateId, t.UserId }).Where(t => t.UserId == userId) : null
                //,
                //UserMatterTypeRestriction = u.HasMatterTypeRestrictions == true ?
                //                       u.UserMatterTypeRestrictions.Select(m => new { m.MatterGroupTypeId, m.UserMatterTypeRestrictionId, m.UserId }).Where(m => m.UserId == userId) : null
                //,
                //ExternalUserPrivilege = u.ExternalUserPrivileges.Select(p => new { p.ExternalUserPrivilegeId, p.ExternalUserPrivilegeTypeId, p.UserId }).Where(p => p.UserId == userId)
            })
             .ToList()
             .Select(u => new UserCustomEntities.UserDetails(u.UserId, u.Username, u.Enabled, u.Lastname, u.Firstname, u.DisplayInitials, u.StateId, u.StateName, u.UserTypeId, u.UserTypeName,
                        u.LenderId, u.LenderName, u.MortMgrId, u.MortMgrName, u.BrokerId, EntityHelper.GetFullName(u.BrokerLastname, u.BrokerFirstname), u.Email, u.ExternalEmail, u.Phone, u.Mobile, u.Fax,
                        u.OfficeExt, u.Birthday, u.ProfilePhoto, u.LastLoginDate,
                        u.PasswordSalt, u.FailedLogins, u.FailedLoginBanDate, u.PwdResetGuid, u.PwdResetExpiry, u.Notes, u.UpdatedDate, u.UpdatedByUserId, u.UpdatedByUsername, u.HasStateRestrictions, u.HideFromContacts, u.DefaultPrivateNotes, u.PreferredNoteBodyColor, u.PreferredNoteAccentColor, u.RelationshipManagerId, u.WorkingFromHome, u.HasMatterTypeRestrictions, u.SignatureFont, u.SignatureInitialOnly, null, u.OffshoreUser, u.SSORequired, u.EmploymentStatusTypeId))
                .FirstOrDefault();
            // Check if there's a profile picture
            //var filePath = System.IO.Path.Combine(GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_UserProfilePictureDirectory), userId.ToString()) + ".jpeg";
            //if (System.IO.File.Exists(filePath))
            //{
            //    retVal.ProfilePhoto = System.IO.File.ReadAllBytes(filePath);
            //}
            //else
            //{
            //    var fallbackPath = System.IO.Path.Combine(GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_UserProfilePictureDirectory), "NoUserImg.jpg");
            //    retVal.ProfilePhoto = System.IO.File.ReadAllBytes(fallbackPath);
            //}
            // Now add privileges

            //List<ExternalUserPrivilege> usrExtPrivs = context.ExternalUserPrivileges.Where(u => u.UserId == userId).Select();
            // UserPrivileges = new UserPrivilegesClass();
            var uow = new UnitOfWork();
            var UserStateRestriction = new List<UserCustomEntities.UserStateRestrictionClass>();

            var liTmp = uow.Context.States
                       .Select(l => new { l.StateId, l.StateName })
                       .OrderBy(l => l.StateName)
                       .ToList()
                       .Select(l => new UserCustomEntities.UserStateRestrictionClass(l.StateId, l.StateName, true))
                       .ToList();

            if (liTmp != null)
            {
                List<int> currStateRestrictions = uow.Context.UserStateRestrictions.Where(u => u.UserId == userId).Select(x => x.StateId).ToList();
                foreach (var state in liTmp.Where(x => x.id > 0))
                {
                    state.isChecked = currStateRestrictions.Any(x => x == state.id);
                    UserStateRestriction.Add(new UserCustomEntities.UserStateRestrictionClass(state.id, state.value, state.isChecked));
                }
            }
            retVal.UserStateRestriction = UserStateRestriction;
            var UserExternalPrivileges = new List<UserCustomEntities.UserExternalPrivilegesClass>();
            var privTmp = uow.Context.ExternalUserPrivilegeTypes
                      .Select(e => new { e.ExternalUserPrivilegeTypeId, e.ExternalUserPrivilegeName, e.AppliesLender })
                      .Where(e => e.AppliesLender == true)
                      .ToList()
                      .Select(e => new UserCustomEntities.UserExternalPrivilegesClass(e.ExternalUserPrivilegeTypeId, e.ExternalUserPrivilegeName, true))
                  .ToList();
            if (privTmp != null)
            {
                List<int> currExternalUserPrivileges = uow.Context.ExternalUserPrivileges.Where(u => u.UserId == userId).Select(x => x.ExternalUserPrivilegeTypeId).ToList();
                foreach (var priv in privTmp.Where(x => x.id > 0))
                {
                    priv.isChecked = currExternalUserPrivileges.Any(x => x == priv.id);
                    UserExternalPrivileges.Add(new UserCustomEntities.UserExternalPrivilegesClass(priv.id, priv.value, priv.isChecked));
                }
            }
            retVal.UserExternalPrivileges = UserExternalPrivileges;
            var UserMatterTypeRestriction = new List<UserCustomEntities.UserMatterTypeRestrictionClass>();
            var matterTmp = uow.Context.MatterTypes
                      .Select(m => new { m.MatterTypeId, m.MatterTypeName, m.Enabled, m.IsMatterGroup })
                      .Where(m => m.Enabled == true && m.IsMatterGroup == true)
                      .ToList()
                      .Select(m => new UserCustomEntities.UserStateRestrictionClass(m.MatterTypeId, m.MatterTypeName, true))
                      .ToList();
            if (matterTmp != null)
            {
                List<int> currMatterTypeRestrictions = uow.Context.UserMatterTypeRestrictions.Where(u => u.UserId == userId).Select(x => x.MatterGroupTypeId).ToList();
                foreach (var matter in matterTmp.Where(x => x.id > 0))
                {
                    matter.isChecked = currMatterTypeRestrictions.Any(x => x == matter.id);
                    UserMatterTypeRestriction.Add(new UserCustomEntities.UserMatterTypeRestrictionClass(matter.id, matter.value, matter.isChecked));
                }
            }
            retVal.UserMatterTypeRestriction = UserMatterTypeRestriction;

            return retVal;
        }
        public void DeletExternalUser(UnitOfWork uow, int userId, ref bool disable)
        {
            try
            {
                var pRep = uow.GetRepositoryInstance<ExternalUserPrivilege>();
                pRep.DeleteAll(pRep.AllAsQuery.Where(x => x.UserId == userId));
                var sRep = uow.GetRepositoryInstance<UserStateRestriction>();
                sRep.DeleteAll(sRep.AllAsQuery.Where((x => x.UserId == userId)));
                var mRep = uow.GetRepositoryInstance<UserMatterTypeRestriction>();
                mRep.DeleteAll(mRep.AllAsQuery.Where((x => x.UserId == userId)));
                //var uprfRep = uow.GetRepositoryInstance<UserPreference>();
                //uprfRep.DeleteAll(uprfRep.AllAsQuery.Where(x => x.UserId == userId));
                //var uprfrecRep = uow.GetRepositoryInstance<UserPrefRecentMatter>();
                //uprfrecRep.DeleteAll(uprfrecRep.AllAsQuery.Where(x => x.UserId == userId));
                //var uprwstRep = uow.GetRepositoryInstance<UserPrefWindowState>();
                //uprwstRep.DeleteAll(uprwstRep.AllAsQuery.Where(x => x.UserId == userId));
                //var contRep = uow.GetRepositoryInstance<Contact>();
                //contRep.DeleteAll(contRep.AllAsQuery.Where(x => x.ContactOwnerUserId == userId));
                //var utRep = uow.GetRepositoryInstance<ReportUserTemplate>();
                //var deletedTemplates = new List<ReportUserTemplate>();
                //var dbRep = uow.GetRepositoryInstance<Dashboard>();
                //var dbcontRep = uow.GetRepositoryInstance<DashboardContent>();
                //var dashboards = dbRep.AllAsQuery.Where(x => x.UserId == userId).ToList();
                //foreach (var db in dashboards)
                //{
                //    deletedTemplates.AddRange(dbcontRep.AllAsQuery.Where(x => x.DashboardId == db.DashboardId && x.ReportUserTemplateId.HasValue)
                //        .Select(y => y.ReportUserTemplate).ToList());
                //    dbcontRep.DeleteAll(dbcontRep.AllAsQuery.Where(x => x.DashboardId == db.DashboardId));
                //}
                //dbRep.DeleteAll(dbRep.AllAsQuery.Where(x => x.UserId == userId));
                //var utparmRep = uow.GetRepositoryInstance<ReportUserTemplateParam>();
                //var templates = utRep.AllAsQuery.Where(x => x.UserId == userId).ToList().Union(deletedTemplates);
                //foreach (var template in templates)
                //{
                //    utparmRep.DeleteAll(utparmRep.AllAsQuery.Where(x => x.ReportUserTemplateId == template.ReportUserTemplateId));
                //}
                //utRep.DeleteAll(templates);
                //var stauRep = uow.GetRepositoryInstance<SharedTaskAllocUser>();
                //stauRep.DeleteAll(stauRep.AllAsQuery.Where(x => x.UserId == userId));
                //var taaRep = uow.GetRepositoryInstance<TaskAllocAssignment>();
                //taaRep.DeleteAll(taaRep.AllAsQuery.Where(x => x.UserId == userId || x.OriginalUserId == userId));
                //var taufRep = uow.GetRepositoryInstance<TaskAllocUserFilter>();
                //taufRep.DeleteAll(taufRep.AllAsQuery.Where(x => x.UserId == userId));
                //var mrvwRep = uow.GetRepositoryInstance<MatterReviewedHistory>();
                //mrvwRep.DeleteAll(mrvwRep.AllAsQuery.Where(x => x.LastReviewedByUserId == userId));
                var uRep = uow.GetRepositoryInstance<User>();
                uRep.Remove(uRep.FindById(userId));
            }
            catch (Exception ex)
            {
                if (ex.Message == DomainConstants.ForeignKeyConstraint)
                {
                    disable = true;
                }
                else
                    throw;
            }
        }
        public IEnumerable<UserCustomEntities.UserGridView> GetSearchUserViewQuick(User user, string searchText)
        {
            var tmp = context.Users
                     .Select(u => new
                     {
                         u.UserId,
                         u.Username,
                         u.Enabled,
                         u.Lastname,
                         u.Firstname,
                         u.DisplayInitials,
                         u.State.StateName,
                         u.Email,
                         u.UserTypeId,
                         u.UserType.UserTypeName,
                         u.UserType.IsExternal,
                         u.LenderId,
                         u.Lender.LenderName,
                         u.MortMgr.MortMgrName,
                         BrokerLastname = u.Broker.PrimaryContact.Lastname,
                         BrokerFirstname = u.Broker.PrimaryContact.Firstname,
                         u.FailedLogins,
                         u.LastLoginDate,
                         u.UpdatedDate,
                         u.UpdatedByUserId,
                         UpdatedByUsername = u.User2.Username
                     }).Where(u => u.LenderId == user.LenderId && u.UserId != user.UserId && (u.Username.ToLower().Contains(searchText.ToLower()) || u.Email.ToLower().Contains(searchText.ToLower()) || u.Firstname.ToLower().Contains(searchText.ToLower()) || u.Lastname.ToLower().Contains(searchText.ToLower()))).ToList();
            var results = tmp.ToList().Select(u => new UserCustomEntities.UserGridView(u.UserId, u.Username, u.Enabled,
                                       EntityHelper.GetFullName(u.Lastname, u.Firstname), u.DisplayInitials, u.StateName, u.Email, u.LastLoginDate, u.UserTypeName,
                                       GetCompanyName(u.UserTypeId, u.LenderName, u.MortMgrName, EntityHelper.GetFullName(u.BrokerLastname, u.BrokerFirstname)), u.FailedLogins, u.UpdatedDate, u.UpdatedByUserId, u.UpdatedByUsername));
            return results;
        }
        //public IEnumerable<UserCustomEntities.UserGridView> GetSearchMattersViewQuick(IQueryable<User> users)
        //{
        //    return (from u in users.Select(x => new { x.UserId, x.Username, x.Firstname, x.Lastname,
        //        x.Enabled, x.Email, x.MatterSearch, x.MatterPexaWorkspaces,
        //        x.User.Username })
        //            //join mt in context.MatterMatterTypes.Select(x => new { x.MatterId, x.MatterMatterTypeId, x.MatterTypeId, x.MatterType }) on m.MatterId equals mt.MatterId into mtg
        //            //join ms in context.MatterSecurities.Select(x => new
        //            //{
        //            //    x.MatterId,
        //            //    x.Deleted,
        //            //    x.MatterSecurityId,
        //            //    x.MatterTypeId,
        //            //    x.MatterType,
        //            //    x.SettlementTypeId,
        //            //    x.SettlementType,
        //            //    x.SecurityAssetType,
        //            //    x.StreetAddress,
        //            //    x.Matter.SecondaryRefNo,
        //            //    x.Suburb,
        //            //    x.StateId,
        //            //    x.State,
        //            //    x.PostCode,
        //            //    x.ValuationExpiryDate,
        //            //    x.MatterSecurityTitleRefs
        //            //}) on m.MatterId equals ms.MatterId into msg
        //            //select new
        //            //{
        //            //    m.MatterId,
        //            //    m.MatterDescription,
        //            //    MatterGroupTypeName = m.MatterType.MatterTypeName,
        //            //    m.Lender.LenderName,
        //            //    m.LenderRefNo,
        //            //    m.SecondaryRefNo,
        //            //    m.Username,
        //            //    MatterTypes = mtg.Select(m => new { m.MatterMatterTypeId, m.MatterTypeId, m.MatterType.MatterTypeName }),
        //            //    PexaWorkspaceList = m.MatterPexaWorkspaces.Select(x => x.PexaWorkspace.PexaWorkspaceNo),
        //            //    Securities = msg.Where(x => !x.Deleted).Select(s => new
        //            //    {
        //            //        s.MatterSecurityId,
        //            //        s.MatterTypeId,
        //            //        s.MatterType.MatterTypeName,
        //            //        s.SettlementTypeId,
        //            //        s.SettlementType.SettlementTypeName,
        //            //        s.SecurityAssetType.SecurityAssetTypeName,
        //            //        s.StreetAddress,
        //            //        s.Suburb,
        //            //        s.StateId,
        //            //        s.State.StateName,
        //            //        s.PostCode,
        //            //        s.ValuationExpiryDate,
        //            //        TitleRefs = s.MatterSecurityTitleRefs.Select(t => new { t.MatterSecurityTitleRefId, t.MatterSecurityId, t.TitleReference, t.LandDescription, t.IsRegistered })
        //            //    }),
        //            //    m.MatterSearch.MatterSearchText
        //            //}
        //            ).ToList()
        //        .Select(m => new MCE.MatterSearchListView(m.MatterId, m.MatterDescription, m.Username, m.MatterGroupTypeName, m.LenderName, m.LenderRefNo, m.SecondaryRefNo, m.PexaWorkspaceList?.ToList(),
        //        m.MatterTypes.Select(t => new MCE.MatterMatterTypeView(t.MatterMatterTypeId, m.MatterId, t.MatterTypeId, t.MatterTypeName)).ToList(),
        //        m.Securities.Select(s => new MCE.MatterSecurityView(s.MatterSecurityId, m.MatterId, s.MatterTypeId, s.MatterTypeName, s.SettlementTypeId, s.SettlementTypeName, s.SecurityAssetTypeName, s.StreetAddress,
        //                                    s.Suburb, s.StateId, s.StateName, s.PostCode,
        //                                    s.TitleRefs.Select(t => new MCE.MatterSecurityTitleRefView(t.MatterSecurityTitleRefId, t.MatterSecurityId, t.TitleReference, t.LandDescription, t.IsRegistered)).ToList(), null, s.ValuationExpiryDate)).ToList(),
        //        m.MatterSearchText
        //        ))
        //.ToList();
        //}

        public IEnumerable<UserCustomEntities.MSANewsView> GetMSANewsDetail(int newsId)
        {
            var news = context.MSANews

                .Select(mn => new
                {
                    mn.MSANewsId,
                    mn.Title,
                    mn.Content,
                    mn.Visible,
                    mn.NewsDisplayDate,
                    mn.RelevantStartDate,
                    mn.RelevantEndDate,
                    mn.NewsCreatedByUserId,
                    mn.NewsCreatedDate,
                    mn.NewsUpdatedByUserId,
                    mn.NewsUpdatedDate,
                    mn.NewsSummary,
                    mn.DisplayNewsDate,
                    mn.Pinned,
                    mn.NewsCreatedByCustomName,
                    mn.SEOContent,
                    //Author = GetUserFullNameForId(mn.NewsCreatedByUserId)
                })
            .Where(mn => mn.MSANewsId == newsId && mn.Visible == true
                                    && (mn.RelevantStartDate == null || mn.RelevantStartDate <= DateTime.Today)
                                    && (mn.RelevantEndDate == null || mn.RelevantEndDate >= DateTime.Today))

               .ToList()
            .Select(n => new UserCustomEntities.MSANewsView(n.MSANewsId, n.Title, n.Content, n.Visible, n.NewsDisplayDate, n.RelevantStartDate, n.RelevantEndDate,
               n.NewsCreatedByUserId, n.NewsCreatedDate, n.NewsUpdatedByUserId, n.NewsUpdatedDate,
               n.NewsSummary, n.DisplayNewsDate, n.Pinned, n.NewsCreatedByCustomName, n.SEOContent, n.NewsCreatedByUserId == null ? "" : GetNewsUserFullNameForId(n.NewsCreatedByUserId)))
               .ToList();

            return news;
        }

        public UserCustomEntities.SlickipediaPageView GetSlickipediaPage(int pageId)
        {
            var p = context.SlickipediaPages.Where(x => x.SlickipediaPageId == pageId).Select(z=> new UserCustomEntities.SlickipediaPageView()
            {
                PageName = z.PageName,
                SlickipediaPageId = z.SlickipediaPageId,
                Tags = z.Tags,
                UpdatedByUserId = z.UpdatedByUserId,
                UpdatedDate = z.UpdatedDate,
                Description = z.Description,
                UpdatedByUserName = z.User.Fullname
            }).FirstOrDefault();
            return p;
        }
        public List<SlickipediaPageView> GetSlickipediaUserHistory()
        {
            var p = context.SlickipediaUserHistories.Where(x => x.LastAccessedByUserId == GlobalVars.CurrentUser.UserId && x.SlickipediaPage.Deleted == false).Select(z => z.SlickipediaPage).OrderBy(y=>y.UpdatedDate).ToList(); 
            var history = p.Select(x => new SlickipediaPageView()
            {
                PageName = x.PageName,
                SlickipediaPageId = x.SlickipediaPageId,
                UpdatedByUserId = x.UpdatedByUserId,
                Tags = x.Tags,
                UpdatedDate = x.UpdatedDate,
                Description = x.Description,
                UpdatedByUserName = x.User.Fullname
      
            }).ToList();

            return history;
        }
        public ObservableCollection<UserCustomEntities.SlickipediaPageView> GetSlickipediaPages()
        {
            var p = context.SlickipediaPages.Where(x=>x.Deleted == false).Select(z => new UserCustomEntities.SlickipediaPageView()
            {
                PageName = z.PageName,
                SlickipediaPageId = z.SlickipediaPageId,
                Tags = z.Tags,
                UpdatedByUserId = z.UpdatedByUserId,
                UpdatedByUserName = z.User.Fullname,
                UpdatedDate = z.UpdatedDate,
                Description = z.Description
            }).ToObservableCollection();

            return p;
        }

    }
}
