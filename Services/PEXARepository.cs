using Slick_Domain.Entities;
using Slick_Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using Slick_Domain.Enums;

namespace Slick_Domain.Services
{
    public class PEXARepository : SlickRepository
    {
        public PEXARepository(SlickContext context) : base(context)
        {
        }

        public MatterPexaDetail GetPexaDetail(int matterId)
        {
            return context.MatterPexaDetails.FirstOrDefault(m => m.MatterId == matterId);
        }

        public MatterWFPexaDetail GetPexaWFDetail(int matterWFComponentId, int matterId)
        {
            var pexaDetail = context.MatterWFPexaDetails.FirstOrDefault(m => m.MatterWFComponentId == matterWFComponentId);
            if (pexaDetail == null)
            {
                pexaDetail = new MatterWFPexaDetail();
                pexaDetail.MatterWFComponentId = matterWFComponentId;
                pexaDetail.NominatedSettlementDate = DateTime.Today.AddDays(21);

                var tmppexaDetail = GetPexaDetail(matterId);
                if (tmppexaDetail != null)
                {
                    pexaDetail.NominatedSettlementDate = tmppexaDetail.NominatedSettlementDate;
                }
            }
            return pexaDetail;
        }


     
        public PexaDocumentDetailView GetMortgageeDataForPexaDocumentsSigned(int matterId)
        {
            var model = context.Matters.Where(x => x.MatterId == matterId)
                        .Select(x => new
                        {
                            x.MortgageeId,
                            x.Mortgagee.MortgageeName,
                            x.Mortgagee.CompanyACN,
                            x.Mortgagee.CompanyACLN,
                            x.Mortgagee.StreetNo,
                            x.Mortgagee.StreetName,
                            x.Mortgagee.StreetType,
                            x.Mortgagee.Suburb,
                            x.Mortgagee.State,
                            x.Mortgagee.Postcode,
                            TitleRefs = x.MatterSecurities.SelectMany(y => y.MatterSecurityTitleRefs.Select(z => z.TitleReference))
                        }).FirstOrDefault();

            if (model == null) return null;

            return new PexaDocumentDetailView
            {
                ACN = model.CompanyACN,
                CreditLicence = model.CompanyACLN,
                MortgageeId = model.MortgageeId,
                MortgageeName = model.MortgageeName,
                AddressDetails = string.IsNullOrEmpty(model.StreetNo) ? string.Empty :
                                   Common.EntityHelper.FormatAddressDetails($"{model.StreetNo} {model.StreetName} {model.StreetType}", model.Suburb, model.State, model.Suburb),
                TitleRefs = model.TitleRefs
            };
        }

        private List<PexaWorkspaceEntity> GetPexaWorkspacesForMatter(int matterId)
        {
            return context.MatterPexaWorkspaces
                .Where(m => m.MatterId == matterId)
                .Select(x => new PexaWorkspaceEntity
                {
                    PexaWorkSpaceId = x.PexaWorkspaceId,
                    PexaWorkSpace = x.PexaWorkspace.PexaWorkspaceNo,
                    PexaWorkSpaceStatus = x.PexaWorkspace.PexaWorkspaceStatusTypeId != null? x.PexaWorkspace.PexaWorkspaceStatusType.PexaWorkspaceStatusTypeName :  "",

                })
                .ToList();
        }

        public IEnumerable<PexaWorkspaceEntity> GetPexaWFWorkspaces(int matterWFComponentId, int matterId)
        {
            var pexaWorkspaces = 
                context.MatterWFPexaWorkspaces.Where(m => m.MatterWFComponentId == matterWFComponentId)
                .Select(x=> new PexaWorkspaceEntity
                {
                    PexaWorkSpaceId = x.PexaWorkspaceId,
                    PexaWorkSpace = x.PexaWorkspace.PexaWorkspaceNo,
                    PexaWorkSpaceStatus = x.PexaWorkspace.PexaWorkspaceStatusTypeId != null ? x.PexaWorkspace.PexaWorkspaceStatusType.PexaWorkspaceStatusTypeName : "",
                })
                .ToList();
            

            if (!pexaWorkspaces.Any())
            {
                pexaWorkspaces = GetPexaWorkspacesForMatter(matterId);
            }

            return pexaWorkspaces;
        }

        public bool UndoMilestone(int matterId, int matterWFComponentId, int displayOrder)
        {
            var wfPexas = context.MatterWFPexaWorkspaces.Where(x=> x.MatterWFComponentId == matterWFComponentId);
            foreach(var wfItem in wfPexas)
            {
                context.MatterWFSecurityWFPexaWorkspaces.RemoveRange(wfItem.MatterWFSecurityWFPexaWorkspaces);
            }
            context.MatterWFPexaWorkspaces.RemoveRange(wfPexas);
            context.MatterWFPexaDetails.RemoveRange(context.MatterWFPexaDetails.Where(x => x.MatterWFComponentId == matterWFComponentId));
            context.SaveChanges();

            if (!context.MatterWFPexaWorkspaces.Any(x=> x.MatterWFComponent.Matter.MatterId == matterId))
            {
                var mPexas = context.MatterPexaWorkspaces.Where(x => x.MatterId == matterId);
                foreach (var mItem in mPexas)
                {
                    context.MatterSecurityMatterPexaWorkspaces.RemoveRange(mItem.MatterSecurityMatterPexaWorkspaces);
                }
                context.MatterPexaWorkspaces.RemoveRange(mPexas);
                context.MatterPexaDetails.RemoveRange(context.MatterPexaDetails.Where(x => x.MatterId == matterId));
            }

            #region Commented Code
            // Ideally want to go back and Repopulate with Matter Details as Below - but way too much work to do this.
            // -- needs to get Proc Docs Security and match to old Matter Security etc. to get true state at time.
            // -- went with above instead which is to just leave Matter - Associated Securities unless last Pexa workspace -no details.
            //var component = context.MatterWFComponents.Where(x => x.MatterId == matterId &&
            //    (x.WFComponentId == (int)Enums.WFComponentEnum.ChangePexaWorkspaces || x.WFComponentId == (int)Enums.WFComponentEnum.PEXAWorkspace) &&
            //     x.DisplayOrder < displayOrder).OrderByDescending(o=> o.DisplayOrder).FirstOrDefault();

            //if (component != null)
            //{
            //    var pexaDetail = context.MatterWFPexaDetails.FirstOrDefault(x => component.MatterWFComponentId == x.MatterWFComponentId);
            //    if (pexaDetail != null)
            //    {
            //        context.MatterPexaDetails.Add(
            //            new MatterPexaDetail
            //            {
            //                MatterId = matterId,
            //                NominatedSettlementDate = pexaDetail.NominatedSettlementDate,
            //                UpdatedByUserId = pexaDetail.UpdatedByUserId,
            //                UpdatedDate = pexaDetail.UpdatedDate
            //            });
            //    }

            //    var pexaWorkSpaces = context.MatterWFPexaWorkspaces.Where(x => x.MatterWFComponentId == component.MatterWFComponentId);
            //    if (pexaWorkSpaces != null && pexaWorkSpaces.Any())
            //    {
            //        foreach(var item in pexaWorkSpaces)
            //        {
            //            context.MatterPexaWorkspaces.Add(new MatterPexaWorkspace
            //            {
            //                MatterId = matterId,
            //                PexaWorkspaceId = item.PexaWorkspaceId,
            //                UpdatedByUserId = item.UpdatedByUserId,
            //                UpdatedDate = item.UpdatedDate
            //            });
            //        }
            //    }
            //}
            #endregion

            context.SaveChanges();
            return true;
        }

        public IEnumerable<PexaAssociatedSecurity> GetAssociatedPexaSecurities(bool useCurrent, int matterId, int mwfId, int displayOrder)
        {
            if (useCurrent)
            {
                return GetAssociatedPexaSecurities(matterId);              
            }
            else
            {
                return GetAssociatedPexaSecurities(matterId, mwfId, displayOrder);
            }
        }

        private IEnumerable<PexaAssociatedSecurity> GetAssociatedPexaSecurities(int matterId, int mwfId, int displayOrder)
        {
            var wfSecurities = new MatterWFRepository(context).GetLastDocPrepSecurityItemsForMatter(matterId, displayOrder);

            if (wfSecurities == null) return null;
            return
                wfSecurities
                .Select(x => new
                {
                    x.StreetAddress,
                    x.Suburb,
                    x.State.StateName,
                    x.PostCode,
                    x.SettlementTypeId,
                    PexaWorkSpaces = x.MatterWFSecurityWFPexaWorkspaces.Select(sps => sps.MatterWFPexaWorkspace.PexaWorkspace).ToList()
                })
                .ToList()
                .Select(x => new PexaAssociatedSecurity
                {
                    SecurityDetail = $"{x.StreetAddress}{Environment.NewLine}{x.Suburb}  {x.StateName}  {x.PostCode}",
                    UniqueDetail = $"{x.StreetAddress}{x.Suburb}{x.PostCode}",
                    SettlementTypeId = x.SettlementTypeId ?? (int)Enums.SettlementTypeEnum.Paper,
                    SecurityPexaWorkSpaces = x.PexaWorkSpaces.Select
                    (sps => new PexaWorkspaceEntity
                    {
                        PexaWorkSpaceId = sps.PexaWorkspaceId,
                        PexaWorkSpace = sps.PexaWorkspaceNo
                    })
                });
        }

        private IEnumerable<PexaAssociatedSecurity> GetAssociatedPexaSecurities(int matterId)
        {
            return
                context.MatterSecurities.AsNoTracking().Where(m => m.MatterId == matterId && !m.Deleted)
                .Select(x => new
                {
                    x.StreetAddress,
                    x.Suburb,
                    x.State.StateName,
                    x.PostCode,
                    x.SettlementTypeId,
                    PexaWorkSpaces = x.MatterSecurityMatterPexaWorkspaces.Select(sps => sps.MatterPexaWorkspace.PexaWorkspace).ToList()
                })
                .ToList()
                .Select(x => new PexaAssociatedSecurity
                {
                    SecurityDetail = $"{x.StreetAddress}{Environment.NewLine}{x.Suburb}  {x.StateName}  {x.PostCode}",
                    UniqueDetail = $"{x.StreetAddress}{x.Suburb}{x.PostCode}",
                    SettlementTypeId = x.SettlementTypeId,
                    SecurityPexaWorkSpaces = x.PexaWorkSpaces.Select
                    (sps => new PexaWorkspaceEntity
                    {
                        PexaWorkSpaceId = sps.PexaWorkspaceId,
                        PexaWorkSpace = sps.PexaWorkspaceNo
                    })
                });
        }

        public void SavePexaDetail(int matterWFComponentId, int matterId, DateTime settlementDate)
        {
            var wfPexaDetail = context.MatterWFPexaDetails.FirstOrDefault(x => x.MatterWFComponentId == matterWFComponentId);
            if (wfPexaDetail == null)
            {
                wfPexaDetail = new MatterWFPexaDetail();
                wfPexaDetail.MatterWFComponentId = matterWFComponentId;
            }

            if (wfPexaDetail.NominatedSettlementDate == settlementDate) return;

            wfPexaDetail.NominatedSettlementDate = settlementDate;
            wfPexaDetail.UpdatedDate = DateTime.Now;
            wfPexaDetail.UpdatedByUserId = GlobalVars.CurrentUser.UserId;
            if (wfPexaDetail.MatterWFPexaDetailId == 0)
                context.MatterWFPexaDetails.Add(wfPexaDetail);

            MatterPexaDetail pexaDetail = context.MatterPexaDetails.FirstOrDefault(m => m.MatterId == matterId);
            if (pexaDetail == null)
            {
                pexaDetail = new MatterPexaDetail();
                pexaDetail.MatterId = matterId;
            }
            pexaDetail.NominatedSettlementDate = settlementDate;

            var outstandingReqs = context.MatterWFOutstandingReqs.Where(x => x.MatterWFComponent.MatterId == matterId && (x.MatterWFComponent.WFComponentStatusTypeId == (int)Enums.MatterWFComponentStatusTypeEnum.InProgress || x.MatterWFComponent.WFComponentStatusTypeId == (int)Enums.MatterWFComponentStatusTypeEnum.Starting));
            if (outstandingReqs.Any())
            {
                foreach(var req in outstandingReqs)
                {
                    req.ExpectedSettlementDate = settlementDate;
                }
            }

            pexaDetail.UpdatedDate = DateTime.Now;
            pexaDetail.UpdatedByUserId = GlobalVars.CurrentUser.UserId;
            if (pexaDetail.MatterPexaDetailId == 0)
                context.MatterPexaDetails.Add(pexaDetail);

            context.SaveChanges();
        }

        public void SavePexaDetail(int matterWFComponentId, int matterId, bool clearingSettlementDate = false)
        {
            if (clearingSettlementDate)
            {


                MatterPexaDetail pexaDetail = context.MatterPexaDetails.FirstOrDefault(m => m.MatterId == matterId);
                if (pexaDetail == null)
                {
                    pexaDetail = new MatterPexaDetail();
                    pexaDetail.MatterId = matterId;
                }

                if (pexaDetail.MatterPexaDetailId != 0)
                    context.MatterPexaDetails.Remove(pexaDetail);

                context.SaveChanges();
            }
        }

        public DateTime? RetrieveNominatedSettlementDate(int matterId, int matterWFComponentId, bool detailsEnabled, ref bool canChangeDate)
        {
            canChangeDate = false;
            DateTime? settlementDate = null;

            if (detailsEnabled)
            {
                settlementDate = context.Matters.FirstOrDefault(x => x.MatterId == matterId)?.SettlementSchedule?.SettlementDate;
            }
            
            if (!settlementDate.HasValue)
            {
                if (detailsEnabled)
                    canChangeDate = true;

                settlementDate = context.MatterWFPexaDetails.FirstOrDefault(m => m.MatterWFComponentId == matterWFComponentId)?.NominatedSettlementDate;
                if (!settlementDate.HasValue)
                {
                    settlementDate = context.MatterPexaDetails.FirstOrDefault(x => x.MatterId == matterId)?.NominatedSettlementDate;
                }
            }
            
            return settlementDate;
        }
        public int GetPexaPollingFrequency()
        {
            return Int32.Parse(GlobalVars.GetGlobalTxtVar(Slick_Domain.Common.DomainConstants.PexaPollFrequency, context));
        }
        public DateTime GetPexaPollDate()
        {
            return context.PexaServicePolls.FirstOrDefault().PexaServicePollDate;
        }

        public void SetPexaPollDate()
        {
            if (context.PexaServicePolls.Count() == 0)
            {
                PexaServicePoll newPexaServicePoll = new PexaServicePoll() { PexaServicePollDate = DateTime.UtcNow };
                context.PexaServicePolls.Add(newPexaServicePoll);
            }
            else
            {
                context.PexaServicePolls.FirstOrDefault().PexaServicePollDate = DateTime.UtcNow;
            }
            context.SaveChanges();
        }
        public void SetPexaPollDate(DateTime pollTime)
        {
            if (context.PexaServicePolls.Count() == 0)
            {
                PexaServicePoll newPexaServicePoll = new PexaServicePoll() { PexaServicePollDate = pollTime };
                context.PexaServicePolls.Add(newPexaServicePoll);
            }
            else
            {
                context.PexaServicePolls.FirstOrDefault().PexaServicePollDate = pollTime;
            }
            context.SaveChanges();
        }

        public void SetPexaLog(int pexaMattersUpdated, int slickMattersUpdate, List<MatterCustomEntities.PexaLogView> pexaLogs, List<string> unmatchedWorkspaces)
        {
            string logText = "";

            foreach (var workspace in unmatchedWorkspaces)
            {
                logText += $"UNMATCHED WORKSPACE NO: {workspace} |";
            }
            
            foreach (var log in pexaLogs)
            {
                logText += $"MATTER ID: {log.MatterId} - WORKSPACE NO: {log.PexaWorkspaceNo} - STATUS: {log.WorkspaceStatus} |";
            }

            PexaServiceLog newLog = new PexaServiceLog() { PexaMattersUpdated = pexaMattersUpdated, SlickMattersUpdated = slickMattersUpdate, LogText = logText, PexaServiceLogDate = DateTime.UtcNow };

            context.PexaServiceLogs.Add(newLog);
            context.SaveChanges();

        }
     
        public List<int> UpdatePexaWorkspace(string pexaWorkspaceNo, string pexaWorkspaceStatus)
        {
            int matchedPexaWorkspaceStatus = 0;
            var match = context.PexaWorkspaceStatusTypes.Where(p => p.PexaWorkspaceStatusTypeName.Replace(" ", "") == pexaWorkspaceStatus.Replace(" ", "")).FirstOrDefault();
            if (match == null)
            {
                return new List<int>();
            }
            else
            {
                matchedPexaWorkspaceStatus = match.PexaWorkspaceStatusTypeId;
            }

            var mwfRep = new MatterWFRepository(context);

            List<int> mattersUpdated = new List<int>();

            var existingWorkspaces = context.PexaWorkspaces.Where(p => p.PexaWorkspaceNo == pexaWorkspaceNo).ToList();


            foreach (var workspace in existingWorkspaces)
            {
                workspace.PexaWorkspaceStatusTypeId = matchedPexaWorkspaceStatus;
                context.SaveChanges();

                foreach (var MatterPexaWorkspace in workspace.MatterPexaWorkspaces.ToList())
                {
                    //only include if assigned to a security.
                    if(!context.Matters.FirstOrDefault(m=>m.MatterId == MatterPexaWorkspace.MatterId).MatterSecurities.Any(x=>x.MatterSecurityMatterPexaWorkspaces.Any(p=>p.MatterPexaWorkspaceId == MatterPexaWorkspace.MatterPexaWorkspaceId)) )
                    {
                        continue;
                    }

                    if (!MatterPexaWorkspace.Matter.SettlementScheduleId.HasValue)
                    {
                        continue;
                    }
                    

                    mattersUpdated.Add(MatterPexaWorkspace.MatterId);
                    
                    //get list of status types with the components they should mark off
                    var compsForPexaWorkspaceStatus = context.PexaWorkspaceStatusTypeWfComponents.Where(p => p.PexaWorkspaceStatusTypeId == matchedPexaWorkspaceStatus).ToList();
                    bool sameSettlementDates = true;
                    bool futureSettlementDate = false;

                    foreach (var compToMark in compsForPexaWorkspaceStatus)
                    {
                        var allComps = mwfRep.GetMatterComponentsForMatter(MatterPexaWorkspace.MatterId);
                        var validComps = allComps.Where(w => w.WFComponentId == compToMark.WFComponentId
                                        && w.DisplayStatusTypeId != (int)Enums.DisplayStatusTypeEnum.Hide 
                                        && w.WFComponentStatusTypeId != (int)Enums.MatterWFComponentStatusTypeEnum.Complete
                                        && w.WFComponentStatusTypeId != (int)Enums.MatterWFComponentStatusTypeEnum.Deleted).ToList();
                        var comp = validComps.FirstOrDefault();

                        if (comp != null && (comp.WFComponentStatusTypeId == (int)Enums.MatterWFComponentStatusTypeEnum.InProgress || comp.WFComponentStatusTypeId == (int)Enums.MatterWFComponentStatusTypeEnum.Starting))
                        {

                            if (compToMark.PexaWFActionTypeId == (int)Enums.PexaWFActionTypeEnum.Complete)
                            {

                                if (comp.WFComponentId == (int)Enums.WFComponentEnum.SettlementCompletedDischarge || comp.WFComponentId == (int)Enums.WFComponentEnum.SettlementCompleteNewLoans)
                                {
                                    //First, check if it's a hybrid matter - hybrid matters can't automatically be marked as settlement complete 
                                    var securities = context.Matters.Where(m => m.MatterId == comp.MatterId).FirstOrDefault().MatterSecurities;
                                    if (!securities.Any(x=>x.SettlementTypeId != (int)Enums.SettlementTypeEnum.PEXA))
                                    {
                                        //now check if all pexa workspaces for this mater are marked as some kind of complete milestone
                                        bool canComplete = true;
                                        bool confirmationLetterExists = false;
                                        var validWorkspacesForMatter = securities.Select(t => t.MatterSecurityMatterPexaWorkspaces).ToList();
                                        List<int> matterPexaWorkspaceIds = new List<int>();
                                        foreach (var ws in validWorkspacesForMatter)
                                        {
                                            foreach(var id in ws)
                                            {
                                                matterPexaWorkspaceIds.Add(id.MatterPexaWorkspaceId);
                                            }
                                        }

                                        var workspacesForMatter = context.MatterPexaWorkspaces.Where(m => matterPexaWorkspaceIds.Contains(m.MatterPexaWorkspaceId));

                                        foreach (var mtPexaWorkspace in workspacesForMatter)
                                        {
                                            //"valid" statuses for this component - we check if all the pexa workspaces are now at a point where we want to click over the next action list
                                            var validStatuses = context.PexaWorkspaceStatusTypeWfComponents.Where(p => p.WFComponentId == comp.WFComponentId && p.PexaWFActionTypeId == (int)Enums.PexaWFActionTypeEnum.Complete)
                                                                                                                                                                     .Select(x=>x.PexaWorkspaceStatusTypeId).ToList();
                                            
                                            if (!mtPexaWorkspace.PexaWorkspace.PexaWorkspaceStatusTypeId.HasValue || !validStatuses.Contains(mtPexaWorkspace.PexaWorkspace.PexaWorkspaceStatusTypeId.Value))
                                            {
                                                canComplete = false;
                                            }
                                    
                                        }

                                        //get all the different settlement dates
                                        var matterIds = workspace.MatterPexaWorkspaces.Select(x => x.MatterId).Distinct();
                                        
                                        var settlementDates = context.Matters
                                            .Where(m => matterIds.Contains(m.MatterId) && m.SettlementScheduleId.HasValue && m.MatterSecurities.Any(x=>x.MatterSecurityMatterPexaWorkspaces
                                            .Any(p=>p.MatterPexaWorkspace.PexaWorkspaceId == workspace.PexaWorkspaceId)))
                                            .Select(x => new { x.MatterId, x.SettlementSchedule.SettlementDate, x.User.Firstname, x.User.Lastname, x.User.Email })
                                            .Distinct().ToList();

                                        if (settlementDates.Select(x=>x.SettlementDate).Distinct().Count() > 1)
                                        {
                                            sameSettlementDates = false; //multiple settlement dates 
                                        }

                                        if (settlementDates.Any(x=>x.SettlementDate.Date > DateTime.Now.Date))
                                        {
                                            futureSettlementDate = true; 
                                        }
                                        if(comp.Matter.MatterGroupTypeId == (int)MatterGroupTypeEnum.NewLoan && comp.Matter.LenderId != 1)
                                        {
                                            confirmationLetterExists = context.MatterDocuments.Any(x => x.MatterId == comp.MatterId && !x.IsDeleted && x.DocumentDisplayAreaTypeId == (int)Slick_Domain.Enums.DocumentDisplayAreaEnum.SettlementPack && x.DocumentMaster.DocName.ToUpper().Contains("SETTLEMENT CONFIRMATION LETTER"));
                                        }
                                        else
                                        {
                                            confirmationLetterExists = true;
                                        }
                                        if (canComplete && sameSettlementDates && !futureSettlementDate && confirmationLetterExists)
                                        {

                                            bool stopAutoEmails = context.Matters.FirstOrDefault(m => m.MatterId == comp.MatterId).StopAutomatedEmails ?? false;
                                            mwfRep.MarkComponentAsComplete(comp.MatterWFComponentId); //mark the requested component as complete.
                                            mwfRep.AddMatterEvent(comp.MatterId, comp.MatterWFComponentId, Slick_Domain.Enums.MatterEventTypeList.MilestoneComplete, "Milestone completed in PEXA", true);

                                            context.Matters.Where(m => m.MatterId == comp.MatterId).Select(x => x.SettlementSchedule).FirstOrDefault().SettlementScheduleStatusTypeId = (int)Enums.SettlementScheduleStatusTypeEnum.Settled;
                                            context.SaveChanges();

                                            //send milestone complete auto email from noreply email address 
                                            if (!stopAutoEmails)
                                            {
                                                mwfRep.SendAutomatedEmails(comp.MatterId, comp.WFComponentId, comp.MatterWFComponentId, (int)Enums.MilestoneEmailTriggerTypeEnum.Completed, forceNoReply: true);
                                                mwfRep.CheckAndSendBackChannelMessages(comp.MatterWFComponentId, comp.WFComponentId, comp.Matter.LenderId, comp.Matter.MortMgrId, BackChannelMessageTriggerTypeEnum.Completed, comp.Matter.MatterGroupTypeId);
                                            }

                                            //mark the matter status as "settled"
                                            var matStatus = context.Matters.Where(m => m.MatterId == comp.MatterId).FirstOrDefault().MatterStatusTypeId = (int)Enums.MatterStatusTypeEnum.Settled;
                                            context.Matters.Where(m => m.MatterId == comp.MatterId).FirstOrDefault().Settled = true; 

                                            context.SaveChanges();

                                            if (comp.Matter.LenderId == 139 && comp.Matter.MatterGroupTypeId == (int)Enums.MatterGroupTypeEnum.NewLoan)
                                            {
                                                //crazy settlement complete fees for ubank / afs.
                                                var mwfComponentView = new MatterWFRepository(context).GetMatterWFComponentView(comp.MatterWFComponentId);
                                                new FeesRepository(context).AddSettlementCompleteFees(mwfComponentView);
                                            }


                                            //var nextComp = allComps.Where((x) => comp.DisplayOrder + 1 == x.DisplayOrder).FirstOrDefault();
                                            //var nextComp = mwfRep.GetNextMatterWFComponent(mwfRep.GetMatterWFComponentView(comp.MatterWFComponentId));
                                            //nextComp.UpdatedDate = System.DateTime.Now;
                                            //nextComp.WFComponentStatusTypeId = (int)Slick_Domain.Enums.MatterWFComponentStatusTypeEnum.InProgress;

                                            //mwfRep.AddMatterEvent(nextComp.MatterId, nextComp.MatterWFComponentId, Slick_Domain.Enums.MatterEventTypeList.MilestoneStarted, "Milestone in PEXA", true);
                                            //context.SaveChanges();

                                            //if (!stopAutoEmails)
                                            //{
                                            //    mwfRep.SendAutomatedEmails(nextComp.MatterId, nextComp.WFComponentId, nextComp.MatterWFComponentId, (int)Enums.MilestoneEmailTriggerTypeEnum.Started, forceNoReply: true);
                                            //    mwfRep.CheckAndSendBackChannelMessages(nextComp.MatterWFComponentId, nextComp.WFComponentId, nextComp.Matter.LenderId, nextComp.Matter.MortMgrId, BackChannelMessageTriggerTypeEnum.Started, nextComp.Matter.MatterGroupTypeId);
                                            //}
                                            //context.SaveChanges();

                                            mwfRep.ActivateComponents(MatterPexaWorkspace.MatterId, true, true);
                                            context.SaveChanges();

                                            mwfRep.AllocateTasks(MatterPexaWorkspace.MatterId);
                                            mwfRep.UpdateAllDueDates(MatterPexaWorkspace.MatterId, comp.WFComponentId);
                                            context.SaveChanges();

                                        }
                                        else if (canComplete && !sameSettlementDates)
                                        {
                                            int fileOwnerUserId = context.Matters.Where(m => m.MatterId == MatterPexaWorkspace.MatterId).Select(u => u.FileOwnerUserId).FirstOrDefault();
                                            string emailToSendTo = context.Users.Where(e => e.UserId == fileOwnerUserId).FirstOrDefault().Email;

                                            if (Slick_Domain.GlobalVars.GetGlobalTxtVar(Slick_Domain.Common.DomainConstants.GlobalVars_TestingEmails, context).ToUpper() == "TRUE")
                                            {
                                                emailToSendTo = GlobalVars.GetGlobalTxtVar(Common.DomainConstants.GlobalVars_TestingEmail, context);
                                            }

                                            //wow tuples now we're cooking with gas
                                            List<Tuple<int, DateTime?, string, string, string>> mattersSettlementDates = new List<Tuple<int, DateTime?, string, string, string>>();

                                            mattersSettlementDates = settlementDates.Select(x => new Tuple<int, DateTime?, string, string, string>(x.MatterId, x.SettlementDate, x.Firstname, x.Lastname, x.Email)).ToList();

                                            if (!MatterPexaWorkspace.EmailSentFlag.HasValue || MatterPexaWorkspace.EmailSentFlag.Value == false)
                                            {
                                                EmailsService.SendPexaDiffSettlementDatesEmail(emailToSendTo, MatterPexaWorkspace.MatterId, mattersSettlementDates, pexaWorkspaceNo, pexaWorkspaceStatus);
                                                MatterPexaWorkspace.EmailSentFlag = true;
                                                context.SaveChanges();
                                            }
                                        }
                                        else if (canComplete && sameSettlementDates && futureSettlementDate)
                                        {
                                            int fileOwnerUserId = context.Matters.Where(m => m.MatterId == MatterPexaWorkspace.MatterId).Select(u => u.FileOwnerUserId).FirstOrDefault();
                                            string emailToSendTo = context.Users.Where(e => e.UserId == fileOwnerUserId).FirstOrDefault().Email;

                                            if (Slick_Domain.GlobalVars.GetGlobalTxtVar(Slick_Domain.Common.DomainConstants.GlobalVars_TestingEmails, context).ToUpper() == "TRUE")
                                            {
                                                emailToSendTo = GlobalVars.GetGlobalTxtVar(Common.DomainConstants.GlobalVars_TestingEmail, context);
                                            }

                                            //wow tuples now we're cooking with gas
                                            List<Tuple<int, DateTime?, string, string, string>> mattersSettlementDates = new List<Tuple<int, DateTime?, string, string, string>>();

                                            mattersSettlementDates = settlementDates.Select(x => new Tuple<int, DateTime?, string, string, string>(x.MatterId, x.SettlementDate, x.Firstname, x.Lastname, x.Email)).ToList();

                                            if (!MatterPexaWorkspace.EmailSentFlag.HasValue || MatterPexaWorkspace.EmailSentFlag.Value == false)
                                            {
                                                EmailsService.SendPexaFutureSettlementDatesEmail(emailToSendTo, MatterPexaWorkspace.MatterId, mattersSettlementDates, pexaWorkspaceNo, pexaWorkspaceStatus);
                                                MatterPexaWorkspace.EmailSentFlag = true;
                                                context.SaveChanges();
                                            }
                                        }
                                        else if(!confirmationLetterExists)
                                        {
                                            var fileownerDetails = context.Matters.Where(m => m.MatterId == MatterPexaWorkspace.MatterId).Select(u => new { u.User.Email, u.User.Firstname }).FirstOrDefault();
                                            string emailToSendTo = fileownerDetails.Email;
                                            if (Slick_Domain.GlobalVars.GetGlobalTxtVar(Slick_Domain.Common.DomainConstants.GlobalVars_TestingEmails, context).ToUpper() == "TRUE")
                                            {
                                                emailToSendTo = GlobalVars.GetGlobalTxtVar(Common.DomainConstants.GlobalVars_TestingEmail, context);
                                            }

                                            if (!MatterPexaWorkspace.EmailSentFlag.HasValue || MatterPexaWorkspace.EmailSentFlag.Value == false)
                                            {
                                                var erep = new EmailsRepository(context);
                                                string body = $"<span style='font-family:Calibri;font-size:11pt;'> <p>Hi {fileownerDetails.Firstname},</p> <p style='font-family: Calibri; font-size:11pt;'> Pexa Workspace <b>{pexaWorkspaceNo}</b> has now been settled, and is at the stage of <b>{pexaWorkspaceStatus}</b>.</br>"
                                                    + "However, settlement could not be completed as no Settlement Confirmation Letter was found in the settlement pack.</br>Please run the letter and complete settlement manually.</br>Kind regards, Alfred.</p></span>";
                                                erep.SendEmail(emailToSendTo, null, $"SETTLEMENT CONFIRMATION LETTER MISSING FROM {comp.MatterId}", body);
                                                MatterPexaWorkspace.EmailSentFlag = true;
                                                context.SaveChanges();
                                            }
                                        }
                                    }
                                }
                                //FIX ME 
                                //this is where the else can go when we want to support marking off other components - there's always going to be other conditions though so no generalised code just yet
                            }
                        }
                        
                        else if(comp == null || comp.WFComponentStatusTypeId != (int)Enums.MatterWFComponentStatusTypeEnum.Complete) 
                        {
                            if(comp == null)
                            {
                                if (compToMark != null)
                                {
                                    if (!context.MatterWFComponents.Any(x => x.MatterId == MatterPexaWorkspace.MatterId && x.WFComponentId == compToMark.WFComponentId))
                                    {
                                        //this means that the component doesn't even eixst in the workflow and can be ignored.
                                        continue;
                                    }

                                    else if (context.MatterWFComponents.Any(x => x.MatterId == MatterPexaWorkspace.MatterId
                                                && x.WFComponentId == compToMark.WFComponentId
                                                && x.WFComponentStatusTypeId == (int)Enums.MatterWFComponentStatusTypeEnum.Complete))
                                    {
                                        continue;
                                    }
                                }



                            }

                            //if we tried to complete the milestone but it hadn't yet been started, email file owner. Don't email if settlement's already been completed because that's fine I guess

                            int fileOwnerUserId = context.Matters.Where(m => m.MatterId == MatterPexaWorkspace.MatterId).Select(u => u.FileOwnerUserId).FirstOrDefault();
                            string emailToSendTo = context.Users.Where(e => e.UserId == fileOwnerUserId).FirstOrDefault().Email;

                            if (Slick_Domain.GlobalVars.GetGlobalTxtVar(Slick_Domain.Common.DomainConstants.GlobalVars_TestingEmails, context).ToUpper() == "TRUE")
                            {
                                emailToSendTo = GlobalVars.GetGlobalTxtVar(Common.DomainConstants.GlobalVars_TestingEmail, context);
                            }
                            if (!MatterPexaWorkspace.EmailSentFlag.HasValue || MatterPexaWorkspace.EmailSentFlag == false)
                            {
                                EmailsService.SendPexaExceptionEmail(emailToSendTo, MatterPexaWorkspace.MatterId, pexaWorkspaceNo, pexaWorkspaceStatus);
                                MatterPexaWorkspace.EmailSentFlag = true;
                                context.SaveChanges();
                            }
                        }
                    }
                }
            }

            return mattersUpdated;
        }
        public IEnumerable<PexaConversationView> GetConversationViews(IQueryable<PexaConversation> qry)
        {
            return qry
                .Select(x => new
                {
                    x.PexaConversationId,
                    x.PexaWorkspace.MatterPexaWorkspaces.FirstOrDefault().Matter.MatterGroupTypeId,
                    MatterDetails = x.PexaWorkspace.MatterPexaWorkspaces.Select(p=>new { p.MatterId, p.Matter.MatterDescription }).ToList(),
                    WFComponentIds = x.PexaWorkspace.MatterPexaWorkspaces.SelectMany(p=>p.Matter.MatterWFComponents
                        .Where(c=>(c.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.InProgress || c.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.Starting)
                        && (c.DisplayStatusTypeId == (int)DisplayStatusTypeEnum.Default || c.DisplayStatusTypeId == (int)DisplayStatusTypeEnum.Display)).Select(c=>c.WFComponentId)).ToList(),
                    x.APIConversationId,
                    x.PexaWorkspaceId,
                    x.PexaWorkspace.PexaWorkspaceNo,
                    x.PexaConversationCategoryTypeId,
                    x.PexaConversationCategoryType.PexaConversationCategoryTypeName,
                    ConversationDate = x.ConversationDate,
                    x.HighPriority,
                    x.ConversationAuthor,
                    x.ConversationAuthorSubscriberTypeId,
                    x.PexaSubscriberType.PexaSubscriberTypeName,
                    x.APIConversationAuthorSubscriberId,
                    x.ConversationSubject,
                    Messages = x.PexaConversationMessages.OrderBy(o => o.MessageDate)
                        .Select(m => new
                        {
                            m.PexaConversationMessageId,
                            m.PexaConversationId,
                            m.APIMessageId,
                            MessageDate = m.MessageDate,
                            m.MessageAuthor,
                            m.APIMessageAuthorId,
                            m.MessageAuthorSubscriberTypeId,
                            m.PexaSubscriberType.PexaSubscriberTypeName,
                            m.MessageBody,
                            m.ActionRequired,
                            m.MessageViewedDate,
                            m.MessageViewedByUserId,
                            Username = m.User != null ? m.User.Username : null
                        })
                }).OrderByDescending(x => x.ConversationDate)
                .ToList()
                .Select(x =>
                new PexaConversationView
                (
                    x.PexaConversationId,
                    x.MatterDetails.Select(m=>m.MatterId).ToList(),
                    x.WFComponentIds,
                    x.MatterGroupTypeId,
                    x.APIConversationId,
                    x.PexaWorkspaceId,
                    x.PexaWorkspaceNo,
                    x.PexaConversationCategoryTypeId,
                    x.PexaConversationCategoryTypeName,
                    x.ConversationDate,
                    x.HighPriority,
                    x.ConversationAuthor,
                    x.ConversationAuthorSubscriberTypeId,
                    x.PexaSubscriberTypeName,
                    x.APIConversationAuthorSubscriberId,
                    x.ConversationSubject,
                    x.Messages.ToList().Select(m => new
                        PexaConversationMessageView(
                            m.PexaConversationMessageId,
                            m.PexaConversationId,
                            m.APIMessageId,
                            m.MessageDate,
                            m.MessageAuthor,
                            m.APIMessageAuthorId,
                            m.MessageAuthorSubscriberTypeId,
                            m.PexaSubscriberTypeName,
                            m.MessageBody,
                            m.ActionRequired,
                            m.MessageViewedDate,
                            m.MessageViewedByUserId,
                            m.Username,
                            x.PexaWorkspaceNo,
                            x.MatterDetails.Select(c=>c.MatterId).ToList(),
                            x.MatterDetails.Select(c=>c.MatterDescription).ToList(),
                            x.ConversationSubject
                            )).ToList(),
                    x.MatterDetails.Select(d=>new LimitedPexaMatterView() {
                        MatterId = d.MatterId,MatterDescription = d.MatterDescription,
                        AbbreviatedMatterDescription = x.MatterDetails.Count > 1 ? (d.MatterDescription.Length > 15 ? d.MatterDescription.Substring(0,12).Trim() + "..." : d.MatterDescription) : (d.MatterDescription.Length > 35 ? d.MatterDescription.Substring(0, 32).Trim() + "..." : d.MatterDescription),
                        HasMultipleMatters =x.MatterDetails.Count > 1 }).ToList()
                )).ToList();

        }
        public IEnumerable<PexaConversationView> GetConversationsForMatter(int matterId)
        {
            return GetConversationViews(context.PexaConversations.AsNoTracking().Where(p => p.PexaWorkspace.MatterPexaWorkspaces.Any(x => x.MatterId == matterId)));
        }
        public IEnumerable<PexaConversationView> GetConversationsForUser(int userId, bool includeSettled = true)
        {
            return GetConversationViews(
                context.PexaConversations.AsNoTracking().Where(p => p.PexaWorkspace.MatterPexaWorkspaces.Any(x => (includeSettled || !x.Matter.Settled) && x.Matter.FileOwnerUserId == userId && (x.Matter.MatterStatusTypeId == (int)Slick_Domain.Enums.MatterStatusTypeEnum.InProgress || x.Matter.MatterStatusTypeId == (int)Slick_Domain.Enums.MatterStatusTypeEnum.OnHold || x.Matter.MatterStatusTypeId == (int)Slick_Domain.Enums.MatterStatusTypeEnum.Settled))));
        }

        public IEnumerable<PexaConversationView> GetUnassignedPexaConversations()
        {
            return GetConversationViews(context.PexaConversations.AsNoTracking().Where(p => !p.PexaWorkspace.MatterPexaWorkspaces.Any()));
        }


        public bool SendPexaMessage(int pexaConversationId, string newMessageBody, bool newMessageActionRequired)
        {
            bool success = true;

            

            return success;
        }
        public IEnumerable<MatterCustomEntities.MatterPexaWorkspacesView> GetMatterPexaWorkspacesWithConvosViewForUser(int userId, bool showSettled = true)
        {
            return context.PexaConversations.Where(p => p.PexaWorkspace.MatterPexaWorkspaces.Any(x => x.Matter.FileOwnerUserId == userId
            && (x.Matter.MatterStatusTypeId == (int)Slick_Domain.Enums.MatterStatusTypeEnum.InProgress ||
            x.Matter.MatterStatusTypeId == (int)Slick_Domain.Enums.MatterStatusTypeEnum.OnHold ||
            x.Matter.MatterStatusTypeId == (int)Slick_Domain.Enums.MatterStatusTypeEnum.Settled)))
            .SelectMany(x => x.PexaWorkspace.MatterPexaWorkspaces)
            .Where(x => x.Matter.FileOwnerUserId == userId && (showSettled || x.Matter.MatterStatusTypeId != (int)MatterStatusTypeEnum.Closed && x.Matter.MatterStatusTypeId != (int)MatterStatusTypeEnum.Settled && x.Matter.MatterStatusTypeId != (int)MatterStatusTypeEnum.NotProceeding) )
            .Select(x => new
            {
                x.MatterId,
                x.Matter.MatterDescription,
                x.Matter.Lender.LenderName,
                CurrMilestones = x.Matter.MatterWFComponents.Where(m => (m.DisplayStatusTypeId == (int)DisplayStatusTypeEnum.Default || m.DisplayStatusTypeId == (int)DisplayStatusTypeEnum.Display) &&
                    (m.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.InProgress || m.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.Starting))
                    .Select(c => new { c.WFComponentId, c.WFComponent.WFComponentName }),
                x.Matter.LenderRefNo,
                x.Matter.SecondaryRefNo,
                SettlementDate = x.Matter.SettlementSchedule != null ? (DateTime?)x.Matter.SettlementSchedule.SettlementDate : (DateTime?)null,
                SettlementTime = x.Matter.SettlementSchedule != null ? (TimeSpan?)x.Matter.SettlementSchedule.SettlementTime : (TimeSpan?)null,
                UnreadCounts = x.Matter.MatterPexaWorkspaces.Select(p=>new MatterCustomEntities.PexaWorkspaceUnreadConvoCountView() { PexaWorkspaceId = p.PexaWorkspaceId, UnreadMessages = p.PexaWorkspace.PexaConversations.Count(c => c.PexaConversationMessages.Any(m => !m.MessageViewedDate.HasValue)) }).ToList(),
                PexaWorkspaces = x.Matter.MatterPexaWorkspaces.Select(p => new MatterCustomEntities.PexaWorkspaceViewLimited() { PexaWorkspaceId = p.PexaWorkspaceId, PexaWorkspaceNo = p.PexaWorkspace.PexaWorkspaceNo }).ToList(),
                LatestMessageDate = x.PexaWorkspace.PexaConversations.Select(d=>d.ConversationDate).Max()
            })
            .ToList()
            .Select(p => new MatterCustomEntities.MatterPexaWorkspacesView
            (
                p.MatterId, 
                p.MatterDescription,
                p.LenderName,
                p.LenderRefNo,
                p.SecondaryRefNo,
                p.UnreadCounts,
                p.PexaWorkspaces,
                p.CurrMilestones.Select(x=>x.WFComponentId).ToList(),
                String.Join(", ", p.CurrMilestones.Select(x=>x.WFComponentName)),
                p.SettlementDate,
                p.SettlementTime,
                p.LatestMessageDate
            )).ToList();
        }


        public IEnumerable<PexaConversationTemplateView> GetTemplatesForParent(int? parentTemplateId)
        {
            return context.PexaConversationTemplates.Where(x => x.ParentTemplateId == parentTemplateId)
                .Select(x => new PexaConversationTemplateView
                {
                    ParentTemplateId = parentTemplateId,
                    PexaConversationTemplateId = x.PexaConversationTemplateId,
                    TemplateName = x.DisplayName,
                    IsCategory = String.IsNullOrEmpty(x.TemplateContent),
                    isDirty = false,
                    DisplayOrder = x.DisplayOrder,
                    TemplateContent = x.TemplateContent,
                }).OrderBy(o=>o.DisplayOrder).ToList();
        }

        public IEnumerable<PexaConversationTemplateView> GetAllNonCategoryTemplates()
        {
          return context.PexaConversationTemplates.Where(x => !String.IsNullOrEmpty(x.TemplateContent))
          .Select(x => new PexaConversationTemplateView
          {
              ParentTemplateId = x.ParentTemplateId,
              ParentTemplateName = x.ParentTemplateId.HasValue ? x.PexaConversationTemplate2.DisplayName : "",
              HasParent = x.ParentTemplateId.HasValue,
              PexaConversationTemplateId = x.PexaConversationTemplateId,
              TemplateName = x.DisplayName,
              IsCategory = String.IsNullOrEmpty(x.TemplateContent),
              isDirty = false,
              DisplayOrder = x.DisplayOrder,
              TemplateContent = x.TemplateContent,
          }).OrderBy(o => o.DisplayOrder).ToList();
        }
    }
}
