using System;
using System.Collections.Generic;
using System.Linq;
using Slick_Domain.Entities;
using Slick_Domain.Models;
using System.Data.SqlClient;
using Slick_Domain.Interfaces;
using Slick_Domain.Common;
using Slick_Domain.Enums;
using MCE = Slick_Domain.Entities.MatterCustomEntities;
using System.Text;
using System.Data.Entity.Infrastructure;
using System.IO;
namespace Slick_Domain.Services
{
    public class MatterWFRepository : SlickRepository
    {
        private IRepository<WFTemplate> templateRepository = null;

        public MatterWFRepository(SlickContext Context) : base(Context)
        {
        }

        public MatterCustomEntities.MatterWFComponentView GetMatterWFComponentView(int matterWFComponentId)
        {
            IQueryable<MatterWFComponent> mwfComponents = context.MatterWFComponents.AsNoTracking().Where(m => m.MatterWFComponentId == matterWFComponentId);
            return GetMatterWFComponentsView(mwfComponents).FirstOrDefault();
        }


        public IEnumerable<MatterCustomEntities.MatterWFOutgoingLenderStatusUpdateView> GetOutgoingLenderHistoryForComponent(int matterWFComponentId, bool showDeleted = false)
        {
            return context.MatterWFOutgoingLenderStatusUpdates.Where(m => m.MatterWFComponentId == matterWFComponentId && (!m.Deleted || showDeleted))
                .Select(m => new MCE.MatterWFOutgoingLenderStatusUpdateView()
                {
                    MatterWFOutgoingLenderStatusUpdateId = m.MatterWFOutgoingLenderStatusUpdateId,
                    OutgoingLenderStatusTypeName = m.OutgoingLenderStatusType.OutgoingLenderStatusTypeName,
                    StatusColour = m.OutgoingLenderStatusType.StatusColour,
                    MatterWFComponentId = m.MatterWFComponentId,
                    OutgoingLenderStatusTypeId = m.OutgoingLenderStatusTypeId,
                    DateSent = m.DateSent,
                    DateReceived = m.DateReceived,
                    SentByTypeId = m.DischargeAuthoritySentByTypeId,
                    SentByTypeName = m.DischargeAuthoritySentByType.DischargeAuthoritySentByTypeName,
                    Notes = m.Notes,
                    FollowUpDate = m.FollowUpDate,
                    Deleted = m.Deleted,
                    DateReceivedRequired = m.OutgoingLenderStatusType.DateReceivedRequired,
                    DateSentRequired = m.OutgoingLenderStatusType.DateSentRequired,
                    FollowUpDateRequired = m.OutgoingLenderStatusType.FollowUpDateRequired,
                    NotesRequired = m.OutgoingLenderStatusType.NotesRequired,
                    SentByRequired = m.OutgoingLenderStatusType.SentByRequired,
                    UpdatedByUserId = m.UpdatedByUserId,
                    UpdatedByUserName = m.User.Username,
                    UpdatedDate = m.UpdatedDate                    
                })
                .ToList()
                .OrderByDescending(u=>u.UpdatedDate)
                .ToList();
        }


        #region Milestone Processing Guide methods
        public MatterCustomEntities.MilestoneProcessingGuideView GetBestProcessingGuideForComponent(MatterCustomEntities.MatterWFComponentView mwfComp)
        {
            List<int> settlementTypes = new List<int>();
            List<int> matterTypes = new List<int>();


            settlementTypes = context.MatterSecurities.Where(s => !s.Deleted && s.MatterId == mwfComp.MatterId).Select(x => x.SettlementTypeId).Distinct().ToList();
            matterTypes = context.MatterMatterTypes.Where(m => m.MatterId == mwfComp.MatterId).Select(x => x.MatterTypeId).Distinct().ToList();
            bool isDigiDocs = context.Matters.Where(m => m.MatterId == mwfComp.MatterId).Select(x => x.IsDigiDocs).FirstOrDefault();

            var possibleMatches = GetMilestoneProcessingGuideViews(
                context.WFComponentProcessingGuides
                .Where(x => x.Enabled &&
                    x.WFComponentProcessingGuideComponents.Any(w => w.WFComponentId == mwfComp.WFComponentId) &&
                    (!x.LenderId.HasValue || x.LenderId == mwfComp.LenderId) &&
                    (!x.MortMgrId.HasValue || x.MortMgrId == mwfComp.MortMgrId) &&
                    (!x.SpecificSettlementTypeId.HasValue || settlementTypes.Any(s => s == x.SpecificSettlementTypeId.Value)) &&
                    (!x.SpecificForDigiDocs.HasValue || x.SpecificForDigiDocs == isDigiDocs) &&
                    (x.MatterGroupTypeId == mwfComp.MatterTypeGroupId) &&
                    (!x.WFComponentProcessingGuideMatterTypes.Any() || x.WFComponentProcessingGuideMatterTypes.All(t => matterTypes.Contains(t.MatterTypeId)))
                    )
                ).ToList();

            foreach (var match in possibleMatches)
            {
                if (match.LenderId.HasValue)
                {
                    match.Ranking++;
                }
                if (match.MortMgrId.HasValue)
                {
                    match.Ranking++;
                }
                if (match.SpecificForDigiDocs.HasValue)
                {
                    match.Ranking++;
                }
                if (match.MatterTypes.Any())
                {
                    foreach (var type in matterTypes.Where(m => match.MatterTypes.Any(t => t.MatterTypeId == m)))
                    {
                        match.Ranking++;
                    }
                }
                if (match.SpecificSettlementTypeId.HasValue)
                {
                    match.Ranking++;
                }
            }

            return possibleMatches.OrderByDescending(o => o.Ranking).FirstOrDefault();
        }
        public IEnumerable<WFCustomEntities.WFBackChannelMessageDefinitionView> GetAllBackChannelMessageDefinitions()
        {
            return GetBackchannelDefinitionViews(context.WFComponentBackchannelMessageDefinitions);
        }
        public IEnumerable<WFCustomEntities.WFBackChannelMessageDefinitionView> GetBackchannelDefinitionViews(IQueryable<WFComponentBackchannelMessageDefinition> qry)
        {
            return qry.Select(x => new WFCustomEntities.WFBackChannelMessageDefinitionView()
            {
                isDirty = false,
                WFComponentLenderBackchannelMessageId = x.WFComponentLenderBackchannelMessageId,
                LenderId = x.LenderId,
                MortMgrId = x.MortMgrId,
                LoanTypeId = x.LoanTypeId ?? -1,
                Message = x.Message,
                Code = x.Code,
                MatterGroupTypeId = x.MatterGroupTypeId,
                WFComponentId = x.WFComponentId,
                TriggerTypeId = x.BackChannelMessageTriggerTypeId,
                UpdatedDate = x.UpdatedDate,
                UpdatedByUserId = x.UpdatedByUserId,
                UpdatedByUserName = x.User.Username
            }).ToList();
        }

        public IEnumerable<MatterCustomEntities.MilestoneProcessingGuideView> GetAllMilestoneProcessingGuideViews()
        {
            return GetMilestoneProcessingGuideViews(context.WFComponentProcessingGuides);
        }

        public IEnumerable<MatterCustomEntities.MilestoneProcessingGuideView> GetMilestoneProcessingGuideViews(IQueryable<WFComponentProcessingGuide> qry)
        {
            return qry.Select(g => new
            {
                g.WFComponentProcessingGuideId,
                g.Enabled,
                g.GuideName,
                g.GuideFilePath,
                g.GuideExternalUrl,
                g.SlickipediaPageId,
                g.MatterGroupTypeId,
                MatterGroupTypeName = g.MatterType.MatterTypeName,
                g.LenderId,
                LenderName = g.LenderId.HasValue ? g.Lender.LenderName : "- ANY -",
                g.MortMgrId,
                MortMgrName = g.MortMgrId.HasValue ? g.MortMgr.MortMgrName : "- ANY -",
                g.SpecificForDigiDocs,
                SpecificForDigiDocsTypeDesc = g.SpecificForDigiDocs.HasValue ? (g.SpecificForDigiDocs == true ? "DigiDocs Only" : "Non-DigiDocs Only") : "- ANY -",
                g.SpecificSettlementTypeId,
                SpecificSettlementTypeName = g.SpecificSettlementTypeId.HasValue ? g.SettlementType.SettlementTypeName : null,
                g.LastCheckedDate,
                g.LastCheckedByUserId,
                CheckedUsername = g.User.Username,
                g.UpdatedDate,
                g.UpdatedByUserId,
                UpdatedUsername = g.User1.Username,
                MatterTypes = g.WFComponentProcessingGuideMatterTypes.Select(m => new MCE.MilestoneProcessingGuideMatterTypeView()
                {
                    WFComponentProcessingGuideMatterTypeId = m.WFComponentProcessingGuideMatterTypeId,
                    WFComponentProcessingGuideId = g.WFComponentProcessingGuideId,
                    MatterTypeId = m.MatterTypeId,
                    MatterTypeName = m.MatterType.MatterTypeName
                }).ToList(),
                WFComponents = g.WFComponentProcessingGuideComponents.Select(w => new MCE.MilestoneProcessingGuideComponentView()
                {
                    WFComponentId = w.WFComponentId,
                    WFComponentName = w.WFComponent.WFComponentName,
                    WFComponentProcessingGuideId = w.WFComponentProcessingGuideId,
                    WFComponentProcessingGuideComponentId = w.WFComponentProcessingGuideComponentId
                }).ToList()
            })
            .ToList().Select(g => new MatterCustomEntities.MilestoneProcessingGuideView
            {
                WFComponentProcessingGuideId = g.WFComponentProcessingGuideId,
                Enabled = g.Enabled,
                GuideName = g.GuideName,
                GuideFilePath = g.GuideFilePath,
                GuideExternalUrl = g.GuideExternalUrl,
                SlickipediaPageId = g.SlickipediaPageId,
                MatterGroupTypeId = g.MatterGroupTypeId,
                MatterGroupTypeName = g.MatterGroupTypeName,
                LenderId = g.LenderId,
                LenderName = g.LenderName,
                MortMgrId = g.MortMgrId,
                MortMgrName = g.MortMgrName,
                SpecificForDigiDocs = g.SpecificForDigiDocs,
                SpecificForDigiDocsTypeDesc = g.SpecificForDigiDocsTypeDesc,
                SpecificSettlementTypeId = g.SpecificSettlementTypeId,
                SpecificSettlementTypeName = g.SpecificSettlementTypeName,
                LastCheckedDate = g.LastCheckedDate,
                LastCheckedByUserId = g.LastCheckedByUserId,
                LastCheckedByUserName = g.CheckedUsername,
                MatterTypes = g.MatterTypes,
                WFComponents = g.WFComponents,
                WFComponentNamesConcat = string.Join(", ", g.WFComponents.OrderBy(x => x.WFComponentId).Select(x => x.WFComponentName).ToList()),
                UpdatedByUserId = g.UpdatedByUserId,
                UpdatedDate = g.UpdatedDate,
                UpdatedByUserName = g.UpdatedUsername,
            });
        }

        #endregion
        public bool MarkMilestoneAsUrgent(MatterCustomEntities.MatterWFComponentView matterWFCompView, string comment)
        {

            var currDetails = context.MatterWFComponents.Where(m => m.MatterWFComponentId == matterWFCompView.MatterWFComponentId).FirstOrDefault();
            var currComment = currDetails.AssignedTaskNote;
            var currColourStr = currDetails.AssignedTaskColour;

            if (!string.IsNullOrEmpty(currComment))
            {
                currComment = " | " + currComment.Replace("** URGENT ** ", "");
            }
            if (!string.IsNullOrEmpty(comment))
            {
                currComment += "- " + comment + currComment;
            }
            currDetails.AssignedTaskNote = "** URGENT ** " + currComment;
            currDetails.AssignedTaskColour = "#FFFF0000";
            currDetails.IsUrgent = true;

            return true;

        }

        //since there are so many i'll be splitting it into chunks of 1000 - return of this is if there is more data to get
        public bool AddArchiveQueues(int chunkSize)
        {

            DateTime cutoffDate = DateTime.UtcNow.AddMonths(-6);
            var matters = context.Matters.Select(x => new
            {
                x.MatterId,
                x.MatterGroupTypeId,
                x.MatterStatusTypeId,
                x.ArchiveStatusTypeId,
                AlreadyOnList = x.MatterArchiveQueues.Any(m => m.ArchiveStatusTypeId != (int)ArchiveStatusTypeEnum.Archived),
                AnyUnarchived = x.MatterDocuments.Any(d => d.DocumentMaster.ArchiveStatusTypeId != (int)Slick_Domain.Enums.ArchiveStatusTypeEnum.NotArchived),
                ForceArchive = x.MatterArchiveQueues.Any() || x.MatterStatusTypeId == (int)MatterStatusTypeEnum.NotProceeding || x.MatterStatusTypeId == (int)MatterStatusTypeEnum.Closed,
                MilestoneCompletedDate =
                    (DateTime?)x.MatterEvents.Where(e => e.MatterEventTypeId == (int)MatterEventTypeList.MilestoneComplete && e.MatterWFComponent.WFComponentId == (x.MatterGroupTypeId == (int)MatterGroupTypeEnum.Discharge ? (int)WFComponentEnum.CloseMatter : (x.MatterWFComponents.Any(m => m.WFComponentId == (int)WFComponentEnum.SendLetterToCustodian) ? (int)WFComponentEnum.SendLetterToCustodian : (int)WFComponentEnum.SendSecurityDocsToLender))).Select(e => e.EventDate).OrderByDescending(e => e).FirstOrDefault() ?? (DateTime?)null
            })
            .Where(x => x.MatterId > 2000000 && !x.AlreadyOnList && (x.ArchiveStatusTypeId == (int)ArchiveStatusTypeEnum.NotArchived || !x.AnyUnarchived) && (x.MatterStatusTypeId == (int)Slick_Domain.Enums.MatterStatusTypeEnum.Closed || x.MatterStatusTypeId == (int)MatterStatusTypeEnum.NotProceeding || x.MatterStatusTypeId == (int)Slick_Domain.Enums.MatterStatusTypeEnum.Settled) && (x.ForceArchive || (x.MilestoneCompletedDate.HasValue && x.MilestoneCompletedDate > new DateTime() && x.MilestoneCompletedDate < cutoffDate)))
            .OrderBy(x => x.MatterId)
            .Take(chunkSize).ToList();



            foreach (var matter in matters)
            {
                context.MatterArchiveQueues.Add(new MatterArchiveQueue()
                {
                    MatterId = matter.MatterId,
                    IsFullMatter = true,
                    ArchiveStatusTypeId = (int)ArchiveStatusTypeEnum.NotArchived,
                    MatterArchiveTypeId = (int)MatterArchiveTypeEnum.Archive,
                    QueueCreatedDate = DateTime.Now,
                    QueueCreatedByUserId = GlobalVars.CurrentUser.UserId,
                    QueueUpdatedDate = DateTime.Now
                });
            }
            return matters.Count == chunkSize;
        }


        public MatterWFComponent AddMatterWFCompIntoExistingMatterWFComp(int matterId, WFComponentEnum wFComponentEnum, string reason, bool active = false)
        {
            //int componentId = (int)wFComponentEnum;
            //var existingMatterWfCompFlow = context.MatterWFComponents.AsNoTracking().Where(m => m.MatterId == matterId);
            var ActiveMatterWfComp = GetLastActiveComponentForMatter(matterId);
            if (ActiveMatterWfComp == null)
            {
                ActiveMatterWfComp = GetLastCompletedComponentForMatter(matterId);
            }


            //var currExistingWf = existingMatterWfCompFlow.Where(m => m.MatterWFComponentId == componentId).FirstOrDefault();
            //foreach(var wfComp in existingMatterWfCompFlow)
            //{
            //    if (wfComp.DisplayOrder >= ActiveMatterWfComp.DisplayOrder)
            //    {
            //        wfComp.DisplayOrder++;
            //    }
            //}
            //public int ComponentInsert(MatterCustomEntities.MatterWFComponentView matterWFComponentView, List<MatterCustomEntities.MatterWFComponentBuildView> addMWFComponents, Enums.WFComponentAddEnum insertType, bool addStartDependency, bool addEndDependency, bool resetNextComponentStatus)
            MatterWFComponent newMWFc = new MatterWFComponent();
            if (active)
            {
                newMWFc = ComponentInsertDisplay(matterId, (int)wFComponentEnum, (int)Enums.MatterWFComponentStatusTypeEnum.InProgress, WFComponentAddEnum.Insert, false, ActiveMatterWfComp.DisplayOrder, reason, true);
            }
            else
            {
                newMWFc = ComponentInsertDisplay(matterId, (int)wFComponentEnum, (int)Enums.MatterWFComponentStatusTypeEnum.NotStarted, WFComponentAddEnum.Insert, false, ActiveMatterWfComp.DisplayOrder, reason, true);
            }
            context.SaveChanges();
            return newMWFc;

        }
        private MatterWFComponent ComponentInsertDisplay(int matterId, int addWFComponentId, int matterWFCompStatusId, Enums.WFComponentAddEnum insertType, bool addDependency, int displayOrder, string reason, bool isVisible = true)
        {
            //1 - Get Display order and matterId for this component. Also get the existing "current" milestone for dependency building
            //MatterWFComponent comp = context.MatterWFComponents.Where(m => m.MatterWFComponentId == matterWFComponentId).FirstOrDefault();
            int lastWFCompId = GetLastActiveComponentForMatter(matterId).MatterWFComponentId;
            if (lastWFCompId == 0) lastWFCompId = GetFirstActiveOrLastCompletedComponentForMatter(matterId).MatterWFComponentId;


            //2- Increment display order of components to make way for the new one
            int startDisplayOrder;
            if (insertType == Enums.WFComponentAddEnum.Insert)
                startDisplayOrder = displayOrder;
            else
                startDisplayOrder = displayOrder + 1;
            IEnumerable<MatterWFComponent> wfComps = context.MatterWFComponents.Where(m => m.MatterId == matterId && m.DisplayOrder >= startDisplayOrder);
            foreach (MatterWFComponent wfComp in wfComps)
                wfComp.DisplayOrder += 1;


            //3 - Create new component
            MatterWFComponent newComp = new MatterWFComponent();
            newComp.MatterId = matterId;
            newComp.WFComponentId = addWFComponentId;
            newComp.CurrProcedureNo = 0;
            newComp.WFComponentStatusTypeId = matterWFCompStatusId;
            newComp.DisplayOrder = startDisplayOrder;
            newComp.DisplayStatusTypeId = (int)DisplayStatusTypeEnum.Display;
            newComp.DefTaskAllocTypeId = 1;
            AddDueDateInfo(newComp);
            newComp.UpdatedDate = DateTime.Now;
            newComp.UpdatedByUserId = GlobalVars.CurrentUserId ?? Slick_Domain.Common.DomainConstants.SystemUserId;

            context.MatterWFComponents.Add(newComp);

            List<MatterWFComponentDependent> addedDeps = new List<MatterWFComponentDependent>();
            try { var resp = context.SaveChanges(); }
            catch (Exception ex)
            {
                Slick_Domain.Handlers.ErrorHandler.LogError(ex);
            }

            //4 - Link in the dependencies
            if (addDependency)
            {
                if (insertType == Enums.WFComponentAddEnum.Insert)
                {
                    //Create a dependency between existing component and newly inserted component

                    MatterWFComponentDependent newDepend = new MatterWFComponentDependent();
                    newDepend.MatterWFComponentId = newComp.MatterWFComponentId;
                    newDepend.DependentMatterWFComponentId = lastWFCompId;
                    //check if that dependency already somehow exists

                    if (!context.MatterWFComponentDependents.Any(w =>
                                        (w.MatterWFComponentId == newDepend.MatterWFComponentId && w.DependentMatterWFComponentId == newDepend.DependentMatterWFComponentId) ||
                                        (w.MatterWFComponentId == newDepend.DependentMatterWFComponentId && w.MatterWFComponentId == newDepend.MatterWFComponentId)))
                    {
                        if (!addedDeps.Any(w =>
                                         (w.MatterWFComponentId == newDepend.MatterWFComponentId && w.DependentMatterWFComponentId == newDepend.DependentMatterWFComponentId) ||
                                         (w.MatterWFComponentId == newDepend.DependentMatterWFComponentId && w.MatterWFComponentId == newDepend.MatterWFComponentId)))
                        {
                            context.MatterWFComponentDependents.Add(newDepend);
                            addedDeps.Add(newDepend);

                        }
                    }
                }

                if (insertType == Enums.WFComponentAddEnum.Add)
                {
                    //Replicate all dependencies to the existing component to the newly added component
                    IEnumerable<MatterWFComponentDependent> newDepends = context.MatterWFComponentDependents.Where(m => m.DependentMatterWFComponentId == newComp.MatterWFComponentId);
                    foreach (MatterWFComponentDependent dep in newDepends)
                    {
                        //                        dep.DependentMatterWFComponentId = newComp.MatterWFComponentId;
                        MatterWFComponentDependent newDepend = new MatterWFComponentDependent();
                        newDepend.MatterWFComponentId = dep.MatterWFComponentId;
                        newDepend.DependentMatterWFComponentId = newComp.MatterWFComponentId;

                        if (!context.MatterWFComponentDependents.Any(w =>
                                         (w.MatterWFComponentId == newDepend.MatterWFComponentId && w.DependentMatterWFComponentId == newDepend.DependentMatterWFComponentId) ||
                                         (w.MatterWFComponentId == newDepend.DependentMatterWFComponentId && w.MatterWFComponentId == newDepend.MatterWFComponentId)))
                        {
                            if (!addedDeps.Any(w =>
                                             (w.MatterWFComponentId == newDepend.MatterWFComponentId && w.DependentMatterWFComponentId == newDepend.DependentMatterWFComponentId) ||
                                             (w.MatterWFComponentId == newDepend.DependentMatterWFComponentId && w.MatterWFComponentId == newDepend.MatterWFComponentId)))
                            {
                                context.MatterWFComponentDependents.Add(newDepend);
                                addedDeps.Add(newDepend);

                            }
                        }
                    }
                }

                //Search forward to see if there are any future milestones which are dependent on this type of milestone. If so, link it in
                foreach (var wfComp in wfComps)
                {
                    if (context.MatterWFComponentDependents.Any(m => m.MatterWFComponentId == wfComp.MatterWFComponentId && m.MatterWFComponent1.WFComponentId == addWFComponentId))
                    {
                        MatterWFComponentDependent newDepend = new MatterWFComponentDependent();
                        newDepend.MatterWFComponentId = wfComp.MatterWFComponentId;
                        newDepend.DependentMatterWFComponentId = newComp.MatterWFComponentId;

                        if (!context.MatterWFComponentDependents.Any(w =>
                                           (w.MatterWFComponentId == newDepend.MatterWFComponentId && w.DependentMatterWFComponentId == newDepend.DependentMatterWFComponentId) ||
                                           (w.MatterWFComponentId == newDepend.DependentMatterWFComponentId && w.MatterWFComponentId == newDepend.MatterWFComponentId)))
                        {
                            if (!addedDeps.Any(w =>
                                             (w.MatterWFComponentId == newDepend.MatterWFComponentId && w.DependentMatterWFComponentId == newDepend.DependentMatterWFComponentId) ||
                                             (w.MatterWFComponentId == newDepend.DependentMatterWFComponentId && w.MatterWFComponentId == newDepend.MatterWFComponentId)))
                            {
                                context.MatterWFComponentDependents.Add(newDepend);
                                addedDeps.Add(newDepend);

                            }
                        }
                    }
                }
                AddMatterEvent(matterId, newComp.MatterWFComponentId, MatterEventTypeList.MilestoneStarted, "Client Reissue " + reason);

            }
            return newComp;
        }

        public void PopulateMatterWFCompViewMissingValues(ref MatterCustomEntities.MatterWFComponentView mwfCompView)
        {
            var compView = mwfCompView;
            var matter = context.Matters.FirstOrDefault(x => x.MatterId == compView.MatterId);

            if (matter != null)
            {
                mwfCompView.LenderId = matter.LenderId;
                mwfCompView.FileOwnerId = matter.FileOwnerUserId;
                mwfCompView.MatterStatusId = matter.MatterStatusTypeId;
                mwfCompView.MatterTypeGroupId = matter.MatterGroupTypeId;
                mwfCompView.MortMgrId = matter.MortMgrId;
                mwfCompView.StateId = matter.StateId;
            }
        }

        /// <summary>
        /// Inserts an Adhoc component on Save of details.
        /// </summary>
        /// <param name="matterId"></param>
        /// <param name="component"></param>
        /// <returns></returns>
        public MatterWFComponent InsertAdhocComponent(int matterId, WFComponentEnum component)
        {
            var componentId = GetLastCompletedComponentForMatter(matterId)?.MatterWFComponentId;
            return ComponentInsert(componentId.Value, (int)component, (int)MatterWFComponentStatusTypeEnum.Complete, WFComponentAddEnum.Add, false, isVisible: false);
        }

        public bool IsComponentAfterRestartComponent(int matterId, MatterCustomEntities.MatterWFComponentView matterWFCompView, WFModuleEnum moduleToCheck)
        {
            int? loanTypeId = null;
            if (matterWFCompView == null)
            {
                var activeComponent = GetFirstActiveComponentForMatter(matterId);
                if (activeComponent != null)
                {
                    matterWFCompView = GetMatterWFComponentView(activeComponent.MatterWFComponentId);
                };
            }
            if (CheckIfAdhocModuleComponent(moduleToCheck, matterWFCompView)) return true;

            var mtDetails = context.Matters.Select(x => new { x.MatterId, x.LoanTypeId, IsSelfActing = x.MatterDischarge != null ? x.MatterDischarge.IsSelfActing : (bool?)null }).FirstOrDefault(m => m.MatterId == matterId);

            loanTypeId = mtDetails.LoanTypeId;

            foreach (var matSecs in context.MatterSecurities.AsNoTracking().Where(m => m.MatterId == matterId && !m.Deleted))
            {
                int? wfTemplateID = GetBestWorkFlowTemplateIdForMatter(matSecs.MatterTypeId, matterWFCompView.StateId, matterWFCompView.LenderId, matterWFCompView.MortMgrId, loanTypeId, mtDetails.IsSelfActing);
                if (wfTemplateID.HasValue)
                {
                    var components = context.WFTemplateComponents.OrderBy(o => o.DisplayOrder).Where(w => w.WFTemplateId == wfTemplateID);
                    var moduleComponentDisplayOrder = components.FirstOrDefault(x => x.WFComponent.WFModuleId == (int)moduleToCheck)?.DisplayOrder;
                    var componentToCheckDisplayOrder = components.FirstOrDefault(x => x.WFComponentId == matterWFCompView.WFComponentId)?.DisplayOrder;

                    if (componentToCheckDisplayOrder.HasValue && moduleComponentDisplayOrder.HasValue &&
                        componentToCheckDisplayOrder > moduleComponentDisplayOrder)
                        return true;
                }
            }

            return false;
        }

        public MatterWFComponent InsertComponentBeforeCurrentActive(int wfComponentId, int matterId)
        {
            var currentComp = GetLastActiveComponentForMatterIgnoringFloaters(matterId);
            currentComp.WFComponentStatusTypeId = (int)Enums.MatterWFComponentStatusTypeEnum.NotStarted;
            currentComp.DisplayOrder = currentComp.DisplayOrder + 1;

            context.MatterWFComponentDependents.RemoveRange(context.MatterWFComponentDependents.Where(x => x.MatterWFComponentId == currentComp.MatterWFComponentId));
            MatterWFComponent newComp = new MatterWFComponent()
            {
                WFComponentId = wfComponentId,
                MatterId = matterId,
                WFComponentStatusTypeId = (int)MatterWFComponentStatusTypeEnum.Starting,
                CurrProcedureNo = 0,
                TaskAllocTypeId = (int)TaskAllocationTypeEnum.User,
                DefTaskAllocTypeId = (int)TaskAllocationTypeEnum.User,
                TaskAllocUserId = GlobalVars.CurrentUser.UserId,
                DisplayStatusTypeId = (int)DisplayStatusTypeEnum.Default,
                DisplayOrder = currentComp.DisplayOrder - 1,
                UpdatedDate = DateTime.Now,
                UpdatedByUserId = GlobalVars.CurrentUser.UserId,
            };
            context.MatterWFComponents.Add(newComp);
            context.SaveChanges();

            context.MatterWFComponentDependents.Add(new MatterWFComponentDependent() { MatterWFComponentId = currentComp.MatterWFComponentId, DependentMatterWFComponentId = newComp.MatterWFComponentId });


            AddMatterEvent(matterId, newComp.MatterWFComponentId, MatterEventTypeList.MilestoneInserted, $"INSERTED BEFORE \"{currentComp.WFComponent.WFComponentName}\"", eventCreatedByNotes: "By Re-send Letter to Custodian");

            return newComp;

        }



        public IEnumerable<MCE.SendProcessedDocsView> GetSendProcessedDocsItemsForMatter(int matterId, int matterWFComponentId)
        {
            var sendProcessedDocs =
                context.MatterWFSendProcessedDocs.Where(x => x.MatterWFComponentId == matterWFComponentId)
                .Select(x => new MCE.SendProcessedDocsView
                {
                    MatterWFId = x.MatterWFSendProcessedDocsId,
                    DeliveryTypeId = x.DocumentDeliveryTypeId,
                    DocsSentToLabel = x.DocsSentToLabel,
                    DocsSentTo = x.NameDocsSentTo,
                    EmailSentTo = x.EmailDocsSentTo,
                    
                    ExpressPostReceiveTracking = x.ExpressPostReceiveTracking,
                    ExpressPostSendTracking = x.ExpressPostSentTracking,
                    isDirty = false
                }).ToList();

            if (sendProcessedDocs != null && sendProcessedDocs.Any()) return sendProcessedDocs;

            return context.MatterParties.Where(x => x.MatterId == matterId)
                .Select(x => new MCE.SendProcessedDocsView
                {
                    MatterPartyId = x.MatterPartyId,
                    DeliveryTypeId = x.DocumentDeliveryTypeId,
                    DocsSentTo = x.NameDocsSentTo,
                    DocsSentToLabel = x.DocsSentToLabel,
                    EmailSentTo = x.EmailDocsSentTo,
                    ExpressPostReceiveTracking = x.ExpressPostReceiveTracking,
                    ExpressPostSendTracking = x.ExpressPostSendTracking,
                    isDirty = false
                }).ToList();

        }

        public void UpdateUnstartedPexaMilestones(int matterId)
        {
            var pexaMilestones = context.MatterWFComponents.Where(x => x.MatterId == matterId &&
                            x.DisplayStatusTypeId == (int)DisplayStatusTypeEnum.InactiveNeverShown && x.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.NotStarted)
                            .ToList();

            foreach (var milestone in pexaMilestones)
            {
                milestone.DisplayStatusTypeId = (int)DisplayStatusTypeEnum.Inactive;
            }
            context.SaveChanges();
        }

        public IEnumerable<WFCustomEntities.WFComponentPrecedentSeedView> GetPrecedentSeeds(List<int> precedentIds)
        {
            var precedents = context.Precedents.Where
                (p => precedentIds.Contains(p.PrecedentId) && p.PrecedentStatusTypeId != (int)PrecedentStatusTypeEnum.Obsolete)
                .Select(x => new
                {
                    x.PrecedentId,
                    x.HotDocsId,
                    x.Description,
                    x.PrecedentTypeId,
                    x.AssemblySwitches,
                    x.TemplateFile,
                    x.DocName,
                    x.DocType
                })

                .ToList()
                .Select(x => new WFCustomEntities.WFComponentPrecedentSeedView
                {
                    HotDocsId = x.HotDocsId,
                    PrecedentId = x.PrecedentId,
                    PrecedentDescription = x.Description,
                    PrecedentTypeId = x.PrecedentTypeId,
                    PrecedentAssemblySwitches = x.AssemblySwitches,
                    PrecedentInterviewRequired = !string.IsNullOrEmpty(x.AssemblySwitches) && x.AssemblySwitches.Contains(DomainConstants.HotDocsNoAssemblySwitches),
                    PrecedentTemplateFile = x.TemplateFile,
                    PrecedentDocName = x.DocName,
                    PrecedentDocType = x.DocType
                }).ToList();

            return precedents;
        }

        public void MakeInactiveMilestoneActive(int matterId, int matterWFComponentId, int wfcomponentId, bool nextMilestone)
        {
            var components = GetValidComponentsForMatter(matterId);

            var currComponent = components.FirstOrDefault(x => x.MatterWFComponentId == matterWFComponentId);
            if (currComponent == null) return;

            MatterWFComponent updComponent = null;
            if (nextMilestone)
            {
                updComponent = components.FirstOrDefault(x => x.DisplayOrder > currComponent.DisplayOrder && x.WFComponentId == wfcomponentId);
            }
            else
            {
                updComponent = components.FirstOrDefault(x => x.DisplayOrder < currComponent.DisplayOrder && x.WFComponentId == wfcomponentId);
            }

            if (updComponent == null) return;

            updComponent.DisplayStatusTypeId = (int)DisplayStatusTypeEnum.Default;
            context.SaveChanges();
        }

        public bool HasOutstandingUnresolvedIssues(int matterId, int matterWFComponentId)
        {
            var issues = (from mwc in context.MatterWFComponents
                          join mwi in context.MatterWFIssues on mwc.MatterWFComponentId equals mwi.DisplayedMatterWFComponentId
                          where mwc.MatterId == matterId && mwi.DisplayedMatterWFComponentId != matterWFComponentId
                          && mwi.MatterOnHold == true && !mwi.Resolved
                          select mwc).Any();

            return false;
        }

        public MatterCustomEntities.MatterOtherPartyView GetMatterOtherParty(int id)
        {
            return context.MatterOtherParties.Where(x => x.MatterId == id)
                .Select(x => new MatterCustomEntities.MatterOtherPartyView
                {
                    Address = x.Address,
                    Contact = x.Contact,
                    Name = x.Name,
                    Email = x.Email,
                    Fax = x.Fax,
                    Mobile = x.Mobile,
                    OtherDetails = x.OtherDetails,
                    Phone = x.Phone,
                    PostCode = x.Postcode,
                    Reference = x.Reference,
                    StateId = x.StateId,
                    StateName = x.State.StateName,
                    Suburb = x.Suburb
                }).FirstOrDefault();
        }

        public MatterCustomEntities.MatterOtherPartyView GetMatterWFOtherParty(int id)
        {
            return context.MatterWFProcDocsOtherParties.Where(x => x.MatterWFComponentId == id)
                .Select(x => new MatterCustomEntities.MatterOtherPartyView
                {
                    Address = x.Address,
                    Contact = x.Contact,
                    Name = x.Name,
                    Email = x.Email,
                    Fax = x.Fax,
                    OtherDetails = x.OtherDetails,
                    Phone = x.Phone,
                    Mobile = x.Mobile,
                    PostCode = x.Postcode,
                    Reference = x.Reference,
                    StateId = x.StateId,
                    StateName = x.State.StateName,
                    Suburb = x.Suburb
                }).FirstOrDefault();
        }

        public IQueryable<MatterWFProcDocsSecurity> GetLastDocPrepSecurityItemsForMatter(int matterId, int displayOrder)
        {
            var procDocsId = new MatterWFRepository(context).GetLastCompletedComponentIdForMatter(matterId, (int)WFComponentEnum.DocPreparation, displayOrder);
            return context.MatterWFProcDocsSecurities.Where(x => x.MatterWFComponentId == procDocsId);
        }

        public void MarkComponentAsComplete(int matterId, WFComponentEnum wfComponentId, MatterWFComponentStatusTypeEnum matterWFComponentStatusTypeEnum)
        {
            var comp = context.MatterWFComponents.Where((m) => m.MatterId == matterId && m.WFComponentStatusTypeId == (int)matterWFComponentStatusTypeEnum && m.WFComponentId == (int)wfComponentId).FirstOrDefault();
            comp.UpdatedDate = System.DateTime.Now;
            comp.WFComponentStatusTypeId = (int)MatterWFComponentStatusTypeEnum.Complete;
            context.SaveChanges();
            AddMatterEvent(matterId, comp.MatterWFComponentId, MatterEventTypeList.MilestoneComplete, "Automatically Updated");
        }


        public int? GetLastCompletedComponentIdForMatter(int matterId, int wfComponentId, int displayOrder)
        {
            return context.MatterWFComponents.Where(x => x.DisplayOrder < displayOrder).OrderByDescending(x => x.DisplayOrder)
                .FirstOrDefault(
                x => x.MatterId == matterId &&
                x.WFComponentId == wfComponentId &&
                x.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.Complete
                )?.MatterWFComponentId;
        }

        /// <summary>
        /// For Adhoc components the module will usually be past
        /// </summary>
        /// <param name="moduleToCheck"></param>
        /// <param name="matterWFCompView"></param>
        /// <returns></returns>
        private bool CheckIfAdhocModuleComponent(WFModuleEnum moduleToCheck, MatterCustomEntities.MatterWFComponentView matterWFCompView)
        {
            return moduleToCheck == WFModuleEnum.BookSettlement && matterWFCompView.WFComponentId == (int)WFComponentEnum.ChangeSettlementPack;
        }

        public bool AllocateTasks(int matterId)
        {
            try
            {
                var components = context.MatterWFComponents.Where(x => x.MatterId == matterId && (x.WFComponentStatusTypeId == (int)Enums.MatterWFComponentStatusTypeEnum.Starting
                                                                                                 || x.WFComponentStatusTypeId == (int)Enums.MatterWFComponentStatusTypeEnum.InProgress)).ToList();

                if (components != null && components.Any())
                {
                    foreach (var comp in components)
                    {
                        var currComp = comp;
                        AllocateTaskUserToComponent(ref currComp);
                    }
                }
            }
            catch (Exception ex)
            {
                Slick_Domain.Handlers.ErrorHandler.LogError(ex);
                return false;
            }
            return true;
        }

        public bool AllocateTaskToUser(int matterWfComponentId, int userId)
        {
            try
            {
                var comp = context.MatterWFComponents.Where(m => m.MatterWFComponentId == matterWfComponentId).FirstOrDefault();
                if (userId < 1)
                {
                    comp.TaskAllocTypeId = (int)Slick_Domain.Enums.TaskAllocationTypeEnum.Shared;
                }
                else
                {
                    comp.TaskAllocUserId = userId;
                    comp.TaskAllocTypeId = (int)Slick_Domain.Enums.TaskAllocationTypeEnum.User;
                }
                context.SaveChanges();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }


        //public MatterCustomEntities.MatterWFComponentView GetMatterWFComponentViewGG(int matterWFComponentId)
        //{
        //    IQueryable<MatterWFComponent> ggSet = context.MatterWFComponents
        //            .Where(m => m.MatterWFComponentId == matterWFComponentId);

        //    //context.Database.Log = s => System.Diagnostics.Debug.WriteLine(s);
        //    var  retVal = ggSet
        //            .Select(m=> new
        //            {
        //                m.MatterWFComponentId,
        //                m.MatterId,
        //                m.Matter.FileOwnerUserId,
        //                FileOwnerName = m.Matter.User1.Username,
        //                m.Matter.MatterTypeGroupId,
        //                m.Matter.MatterStatusTypeId,
        //                m.Matter.StateId,
        //                m.Matter.LenderId,
        //                m.Matter.MortMgrId,
        //                m.WFComponentId,
        //                m.WFComponent.WFComponentName,
        //                m.WFComponent.WFModuleId,
        //                m.CurrProcedureNo,
        //                m.WFComponentStatusTypeId,
        //                StatusTypeName = m.WFComponentStatusType.WFComponentStatusTypeName,
        //                m.WFComponentStatusType.IsActiveStatus,
        //                m.IsVisible,
        //                m.DisplayOrder,


        //                m.UpdatedDate,
        //                m.UpdatedByUserId,
        //                UpdatedByUsername = m.User.Username,
        //                UpdatedByLastname = m.User.Lastname,
        //                UpdatedByFirstname = m.User.Firstname
        //            }).ToList()
        //            .Select(m => new MatterCustomEntities.MatterWFComponentView(m.MatterWFComponentId, 0, false, false, m.MatterId, m.FileOwnerUserId, m.FileOwnerName, m.MatterTypeGroupId, m.MatterStatusTypeId, m.StateId,
        //                        m.LenderId, m.MortMgrId, m.WFComponentId, m.WFComponentName, m.WFModuleId, m.CurrProcedureNo,
        //                        m.WFComponentStatusTypeId, m.StatusTypeName, m.IsActiveStatus,

        //                        m.DisplayOrder, m.UpdatedDate, m.UpdatedByUserId, m.UpdatedByUsername, m.UpdatedByLastname,
        //                        m.UpdatedByFirstname, m.IsVisible)).FirstOrDefault();

        //    return retVal;
        //}


        private IRepository<WFTemplate> GetTemplateRepository()
        {
            return templateRepository ?? new Repository<WFTemplate>(context);
        }

        public DateTime? GetEventEndDate(MatterWFComponent comp)
        {
            return (DateTime?)comp.MatterEvents.OrderByDescending(o => o.EventDate).FirstOrDefault(e => e.MatterEventTypeId == (int)Enums.MatterEventTypeList.MilestoneComplete && e.MatterEventStatusTypeId == (int)Enums.MatterEventStatusTypeEnum.Good).EventDate;
        }

        private IEnumerable<MatterCustomEntities.MatterWFComponentView> GetMatterWFComponentsView(IQueryable<MatterWFComponent> mwfComponents)
        {
            //context.Database.Log = s => System.Diagnostics.Debug.WriteLine(s);
            IEnumerable<MatterCustomEntities.MatterWFComponentView> retVal =
                    mwfComponents
                    .Select(m => new
                    {
                        m.MatterWFComponentId,
                        m.MatterId,
                        m.Matter.LenderRefNo,
                        m.Matter.FileOwnerUserId,
                        m.Matter.LoanTypeId,
                        m.Matter.SettlementScheduleId,
                        m.WFComponent.AccountsStageId,
                        m.SlaveToMatterWFComponentId,
                        FileOwnerName = m.Matter.User.Username,
                        m.Matter.MatterGroupTypeId,
                        m.Matter.MatterStatusTypeId,
                        m.Matter.StateId,
                        m.Matter.LenderId,
                        m.Matter.Lender.LenderName,
                        m.Matter.MortMgrId,
                        m.WFComponentId,
                        m.WFComponent.WFComponentName,
                        m.WFComponent.WFModuleId,
                        m.CurrProcedureNo,
                        m.WFComponentStatusTypeId,
                        StatusTypeName = m.WFComponentStatusType.WFComponentStatusTypeName,
                        m.WFComponentStatusType.IsActiveStatus,
                        m.DisplayOrder,
                        m.DisplayStatusTypeId,
                        m.DisplayStatusType.DisplayStatusTypeName,
                        m.DisplayStatusType.DisplayStatusTypeDesc,
                        m.TaskAllocUserId,
                        ReAssignUsername = m.User1.Username,
                        m.TaskAllocTypeId,
                        EventStartDate = (DateTime?)m.MatterEvents.FirstOrDefault(e => e.MatterEventTypeId == (int)Enums.MatterEventTypeList.MilestoneStarted && e.MatterEventStatusTypeId == (int)Enums.MatterEventStatusTypeEnum.Good).EventDate,
                        m.DueDate,
                        //EventEndDate = GetEventEndDate(m),
                        EventEndDate = (DateTime?)m.MatterEvents.FirstOrDefault(e => e.MatterEventTypeId == (int)Enums.MatterEventTypeList.MilestoneComplete && e.MatterEventStatusTypeId == (int)Enums.MatterEventStatusTypeEnum.Good).EventDate,
                        EventCompletedDetails = m.MatterEvents.Where(e => e.MatterEventTypeId == (int)MatterEventTypeList.MilestoneComplete && e.MatterEventStatusTypeId == (int)MatterEventStatusTypeEnum.Good).Select(x => new { x.MatterEventTypeId, x.User.Username, x.User.UserId, x.MatterEventStatusTypeId }).FirstOrDefault(),
                        m.UpdatedDate,
                        m.UpdatedByUserId,
                        UpdatedByUsername = m.User.Username,
                        UpdatedByLastname = m.User.Lastname,
                        UpdatedByFirstname = m.User.Firstname,
                        ShowStartReminders = m.WFComponent.ReminderDefinitions1.Any(x => x.Enabled) && m.MatterWFReminders.Any(x => x.StopReminders),
                        ShowStopReminders = m.WFComponent.ReminderDefinitions1.Any(x => x.Enabled) && !m.MatterWFReminders.Any(x => x.StopReminders),
                        m.IsUrgent
                    }).ToList()
                    .Select(m => new MatterCustomEntities.MatterWFComponentView(m.MatterWFComponentId, 0, false, false, m.MatterId,
                                m.FileOwnerUserId, m.FileOwnerName, m.MatterGroupTypeId, m.MatterStatusTypeId, m.StateId,
                                m.LenderId, m.LenderName, m.MortMgrId, m.WFComponentId, m.WFComponentName, m.WFModuleId, m.CurrProcedureNo,
                                m.WFComponentStatusTypeId, m.StatusTypeName, m.IsActiveStatus,
                                m.TaskAllocUserId, m.ReAssignUsername, m.TaskAllocTypeId,
                                m.EventStartDate, m.DueDate, m.EventEndDate,
                                m.DisplayOrder, m.DisplayStatusTypeId, m.DisplayStatusTypeName, m.DisplayStatusTypeDesc,
                                m.UpdatedDate, m.UpdatedByUserId, m.UpdatedByUsername, m.UpdatedByLastname,
                                m.UpdatedByFirstname, m.EventCompletedDetails?.UserId ?? 0, m.EventCompletedDetails?.Username, m.LenderRefNo, m.ShowStartReminders, m.ShowStopReminders, m.AccountsStageId, m.IsUrgent, m.SlaveToMatterWFComponentId, m.LoanTypeId, m.SettlementScheduleId))
                    .ToList();

            return retVal;
        }
        private IEnumerable<MatterCustomEntities.MatterWFComponentView> GetMatterWFComponentsViewSproc(int matterId)
        {
            //context.Database.Log = s => System.Diagnostics.Debug.WriteLine(s);
            var retVal = context.sp_Slick_GetMatterWFComponentsForMatter(matterId)
                    .Select(m => new
                    {
                        m.MatterWFComponentId,
                        m.MatterId,
                        m.LenderRefNo,
                        m.FileOwnerUserId,
                        m.LoanTypeId,
                        m.SettlementScheduleId,
                        m.AccountsStageId,
                        m.SlaveToMatterWFComponentId,
                        m.FileOwnerName,
                        m.MatterGroupTypeId,
                        m.MatterStatusTypeId,
                        m.StateId,
                        m.LenderId,
                        m.LenderName,
                        m.MortMgrId,
                        m.WFComponentId,
                        m.WFComponentName,
                        m.WFModuleId,
                        m.CurrProcedureNo,
                        m.WFComponentStatusTypeId,
                        StatusTypeName = m.WFComponentStatusTypeName,
                        m.IsActiveStatus,
                        m.DisplayOrder,
                        m.DisplayStatusTypeId,
                        m.DisplayStatusTypeName,
                        m.DisplayStatusTypeDesc,
                        m.TaskAllocUserId,
                        m.ReAssignUsername,
                        m.TaskAllocTypeId,
                        m.EventStartDate,
                        m.DueDate,
                        //EventEndDate = GetEventEndDate(m),
                        m.EventEndDate,
                        EventCompletedDetails = new { m.CompleteEventTypeId, m.CompleteEventUserName, m.CompleteEventUserId, m.CompleteEventStatusTypeId },
                        m.UpdatedDate,
                        m.UpdatedByUserId,
                        m.UpdatedByUsername,
                        m.UpdatedByLastname,
                        m.UpdatedByFirstname,
                        ShowStartReminders = m.ReminderDefinitionId.HasValue && m.StoppedWFReminderId.HasValue,
                        ShowStopReminders = m.ReminderDefinitionId.HasValue && !m.StoppedWFReminderId.HasValue,
                        m.IsUrgent
                    }).ToList().OrderByDescending(m => m.EventEndDate).GroupBy(m => m.MatterWFComponentId).Select(s => s.First())
                    .Select(m => new MatterCustomEntities.MatterWFComponentView(m.MatterWFComponentId, 0, false, false, m.MatterId,
                                m.FileOwnerUserId, m.FileOwnerName, m.MatterGroupTypeId, m.MatterStatusTypeId, m.StateId,
                                m.LenderId, m.LenderName, m.MortMgrId, m.WFComponentId, m.WFComponentName, m.WFModuleId, m.CurrProcedureNo,
                                m.WFComponentStatusTypeId, m.StatusTypeName, m.IsActiveStatus,
                                m.TaskAllocUserId, m.ReAssignUsername, m.TaskAllocTypeId,
                                m.EventStartDate, m.DueDate, m.EventEndDate,
                                m.DisplayOrder, m.DisplayStatusTypeId, m.DisplayStatusTypeName, m.DisplayStatusTypeDesc,
                                m.UpdatedDate, m.UpdatedByUserId, m.UpdatedByUsername, m.UpdatedByLastname,
                                m.UpdatedByFirstname, m.EventCompletedDetails?.CompleteEventUserId ?? 0, m.EventCompletedDetails?.CompleteEventUserName, m.LenderRefNo, m.ShowStartReminders, m.ShowStopReminders, m.AccountsStageId, m.IsUrgent, m.SlaveToMatterWFComponentId, m.LoanTypeId, m.SettlementScheduleId))
                    .ToList();

            return retVal;
        }

        public IEnumerable<EntityCompacted> GetMilestoneQuickTexts(int wFComponentId)
        {
            return context.WFComponentQuickResponses.AsNoTracking().Where(x => x.WFComponentId == wFComponentId)
                .Select(x => new EntityCompacted { Id = x.WFComponentQuickResponseId, Details = x.QuickResponseTitle, RelatedDetail = x.QuicResponseText })
                .ToList();
        }

        public IEnumerable<MatterCustomEntities.MatterWFComponentView> GetMatterWFComponentsView(int matterId, bool showHidden)
        {
            IQueryable<MatterWFComponent> mwfComponents =
                context.MatterWFComponents.AsNoTracking()
                .Where(m => m.MatterId == matterId && m.DisplayStatusTypeId != (int)DisplayStatusTypeEnum.Inactive && m.DisplayStatusTypeId != (int)DisplayStatusTypeEnum.InactiveNeverShown);

            var mwfList = GetMatterWFComponentsView(mwfComponents)
                .OrderBy(m => m.DisplayOrder);
            SetMatterWFComponentsViewDuplicates(mwfList);
            SetMatterWFComponentsVisibility(mwfList, showHidden);
            foreach (var comp in mwfList.Where(w => w.WFComponentId == (int)WFComponentEnum.FileManagerAcknowledgement))
            {
                comp.IsVisible = true;
                comp.IsDuplicate = false;
            }



            return mwfList;
        }



        public IEnumerable<MatterCustomEntities.MatterWFComponentView> GetMatterWFComponentsViewSproc(int matterId, bool showHidden)
        {
            var mwfList = GetMatterWFComponentsViewSproc(matterId)
                .OrderBy(m => m.DisplayOrder);
            SetMatterWFComponentsViewDuplicates(mwfList);
            SetMatterWFComponentsVisibility(mwfList, showHidden);
            foreach (var comp in mwfList.Where(w => w.WFComponentId == (int)WFComponentEnum.FileManagerAcknowledgement))
            {
                comp.IsVisible = true;
                comp.IsDuplicate = false;
            }



            return mwfList;
        }

        public void SetMatterWFComponentsViewDuplicates(IEnumerable<MatterCustomEntities.MatterWFComponentView> mwfList)
        {
            foreach (var mwfComp in mwfList)
            {
                foreach (var mwfCompChk in mwfList)
                {
                    if (mwfCompChk.MatterWFComponentId != mwfComp.MatterWFComponentId && mwfCompChk.DisplayOrder > mwfComp.DisplayOrder && mwfComp.WFComponentId != (int)WFComponentEnum.FileManagerAcknowledgement &&
                        (mwfCompChk.DisplayStatusTypeId == (int)DisplayStatusTypeEnum.Default || mwfCompChk.DisplayStatusTypeId == (int)DisplayStatusTypeEnum.Display)
                        && mwfCompChk.WFComponentId == mwfComp.WFComponentId)
                    {
                        mwfComp.IsDuplicate = true;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Fixes gaps in display order due to component removal
        /// </summary>
        /// <param name="matterId"></param>
        /// <param name="currDisplayOrder"></param>
        public void FixMilestoneDisplayOrder(int matterId, int? currDisplayOrder)
        {
            if (!currDisplayOrder.HasValue) return;

            var mwfs = context.MatterWFComponents.Where(x => x.MatterId == matterId &&
                    (x.WFComponentStatusTypeId != (int)MatterWFComponentStatusTypeEnum.Cancelled &&
                    x.WFComponentStatusTypeId != (int)MatterWFComponentStatusTypeEnum.Deleted)).OrderBy(x => x.DisplayOrder).ToList();

            if (mwfs == null || !mwfs.Any()) return;

            var matterComponents = mwfs.Where(x => x.DisplayOrder > currDisplayOrder).OrderBy(x => x.DisplayOrder);

            int displayOrder = currDisplayOrder.Value;
            if (!GetStartingDisplayOrder(ref displayOrder, mwfs, matterId)) return;

            foreach (var component in matterComponents)
            {
                component.DisplayOrder = displayOrder;
                displayOrder++;
            }
            context.SaveChanges();
        }

        private bool GetStartingDisplayOrder(ref int displayOrder, List<MatterWFComponent> mwfs, int matterId)
        {
            bool dupDisplay = true;
            int index = 0;
            int currDisplay = displayOrder;
            int endlessLoopCheck = 300;
            int mwfCount = mwfs.Count();
            while (dupDisplay && index < endlessLoopCheck)
            {
                if (mwfs.Any(x => x.DisplayOrder == currDisplay))
                {
                    currDisplay++;
                }
                else
                {
                    dupDisplay = false;
                    displayOrder = currDisplay;
                }
                index++;
                if (index > mwfCount + 1)
                {
                    return false;
                }
            }
            if (index > endlessLoopCheck)
            {
                throw new Exception($"Matter: {matterId} broke endless loop check - Fix Milestone Display Order");
            }
            return true;
        }

        /// <summary>
        /// Fixes Milestone ReOrder after - Reinstruct UNDO - It is a HACK to fix the order. - DELETE if other code is fixed to undo - build workflow
        /// </summary>
        /// <param name="matterId"></param>
        /// <param name="matterWFComponentId"></param>
        public void FixMilestoneReOrder(int matterId, int? currDisplayOrder)
        {
            if (!currDisplayOrder.HasValue) return;

            var matterComponents = context.MatterWFComponents.Where(x => x.MatterId == matterId).OrderBy(x => x.DisplayOrder);

            bool first = true;
            bool dupRemoved = false;
            int dupComponentId = 0;

            var matterEvents = context.MatterEvents.Where(x => x.MatterId == matterId).ToList();
            currDisplayOrder = currDisplayOrder - 1;
            int displayOrder = currDisplayOrder.Value; //Get display order PRIOR to the Restart item
            foreach (var component in matterComponents.Where(x => x.DisplayOrder >= currDisplayOrder))
            {
                if (first)
                {
                    first = false;
                    dupComponentId = component.WFComponentId;
                    continue;
                }

                if (component.WFComponentId == dupComponentId)
                {
                    dupRemoved = true;
                    if (matterEvents != null && matterEvents.Any(x => x.MatterWFComponentId == component.MatterWFComponentId))
                    {
                        SoftDeleteComponent(component);
                        continue;
                    }

                    context.MatterWFComponentDependents.RemoveRange(context.MatterWFComponentDependents.Where(x => x.MatterWFComponentId == component.MatterWFComponentId).ToList());
                    context.MatterWFComponentDependents.RemoveRange(context.MatterWFComponentDependents.Where(x => x.DependentMatterWFComponentId == component.MatterWFComponentId).ToList());

                    //delete backchannel logs if any
                    var bcToDelete = context.BackChannelLogs.Where(x => x.MatterWFComponentId == component.MatterWFComponentId);
                    context.BackChannelLogs.RemoveRange(bcToDelete);

                    SoftDeleteComponent(component);
                }
                else
                {
                    if (dupRemoved)
                    {
                        displayOrder++;
                        component.DisplayOrder = displayOrder;
                    }
                    else
                    {
                        //Code is not necessary - should be nothing to do.
                        break;
                    }

                }

                context.SaveChanges();
            }

            ActivateComponents(matterId);

        }



        public void SetMatterWFComponentsVisibility(IEnumerable<MatterCustomEntities.MatterWFComponentView> mwfList, bool showHidden)
        {
            foreach (var mwfComp in mwfList)
            {
                if ((mwfComp.CurrStatusTypeId == (int)MatterWFComponentStatusTypeEnum.Deleted) ||
                    (!showHidden &&
                      ((mwfComp.IsDuplicate && mwfComp.DisplayStatusTypeId != (int)DisplayStatusTypeEnum.Display) ||
                       (mwfComp.DisplayStatusTypeId == (int)DisplayStatusTypeEnum.Hide)) &&
                       !mwfComp.CurrStatusIsActive
                    )
                   )
                {
                    mwfComp.IsVisible = false;
                }
                else
                {
                    mwfComp.IsVisible = true;
                }
            }
        }

        private IEnumerable<MatterCustomEntities.MatterWFComponentParamView> GetMatterWFComponentParamsView(IQueryable<MatterWFComponent> components)
        {
            return
                (from c in components
                 join p in context.WFComponentParams on c.WFComponentId equals p.WFComponentId
                 select new
                 {
                     c.MatterWFComponentId,
                     c.WFComponentId,
                     p.WFParamCtlNum,
                     p.WFParameter.WFParameterName,
                     p.WFParameter.WFParameterTypeId,
                     p.WFParameter.WFParameterType.WFParameterTypeName,
                     p.ParamValue
                 }).ToList()
                       .Select(m => new MatterCustomEntities.MatterWFComponentParamView(m.MatterWFComponentId, m.WFComponentId, m.WFParamCtlNum, m.WFParameterName, m.WFParameterTypeId, m.WFParameterTypeName, m.ParamValue))
                    .ToList();

        }

        /// <summary>
        /// Orphans out a component - not considered part of workflow even though still attached to matter.
        /// </summary>
        /// <param name="matterWFComponentId"></param>
        public void SoftDeleteComponent(int matterWFComponentId)
        {
            var component = context.MatterWFComponents.FirstOrDefault(x => x.MatterWFComponentId == matterWFComponentId);
            var currDisplayOrder = component.DisplayOrder;
            SoftDeleteComponent(component);
            if (component != null)
            {
                FixMilestoneDisplayOrder(component.MatterId, currDisplayOrder);
            }
        }

        /// <summary>
        /// Orphans out a component - not considered part of workflow even though still attached to matter.
        /// </summary>
        /// <param name="component"></param>
        public void SoftDeleteComponent(MatterWFComponent component)
        {
            if (component != null)
            {
                component.DisplayOrder = -1;
                component.WFComponentStatusTypeId = (int)MatterWFComponentStatusTypeEnum.Deleted;
                component.UpdatedByUserId = GlobalVars.CurrentUserId ?? Slick_Domain.Common.DomainConstants.SystemUserId;
                component.UpdatedDate = DateTime.Now;
                if (component.ReissueReasons.Any())
                {
                    var comp = context.MatterWFComponents.Where(m => m.MatterId == component.MatterId && m.MatterWFComponentId != component.MatterWFComponentId && m.WFComponentId == component.WFComponentId)
                        .OrderByDescending(u => u.UpdatedDate).FirstOrDefault();
                    if (comp != null)
                    {
                        foreach (var reason in component.ReissueReasons)
                        {
                            context.ReissueReasons.Add(new ReissueReason()
                            {
                                MatterWFComponentId = comp.MatterWFComponentId,
                                MatterId = comp.MatterId,
                                ReissueTxt = reason.ReissueTxt,
                                ReissueTypeId = reason.ReissueTypeId,
                                UpdatedDate = reason.UpdatedDate,
                                UpdatedByUserId = reason.UpdatedByUserId
                            }
                            );
                        }
                    }

                }
            }
        }

        public bool HasXmlMatterDetailsForMatter(int matterId)
        {
            return context.MatterWFXMLs.AsNoTracking().Any(mx => mx.MatterWFComponent.MatterId == matterId);
        }


        public bool HasMilestoneCompleted(int matterId, int wfcomponentId, bool ignoreHidden = false)
        {
            if (!ignoreHidden) return context.MatterWFComponents.Any(x => x.MatterId == matterId &&
                                                   x.WFComponentId == wfcomponentId &&
                                                   x.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.Complete);
            else
            {
                bool complete = false;
                var wfComp = context.MatterWFComponents.Where(x => x.MatterId == matterId && x.WFComponentId == wfcomponentId).OrderByDescending(x => x.DisplayOrder)
                    .OrderByDescending(x => x.DisplayOrder).FirstOrDefault();
                if (wfComp != null)
                {
                    complete = (wfComp.DisplayStatusTypeId == (int)DisplayStatusTypeEnum.Display || wfComp.DisplayStatusTypeId == (int)DisplayStatusTypeEnum.Default)
                                          && wfComp.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.Complete;
                }
                return complete;
            }
        }

        public bool IsMilestoneNotComplete(int matterId, int wfcomponentId)
        {
            return context.MatterWFComponents.Any(x => x.MatterId == matterId &&
                                                  x.WFComponentId == wfcomponentId &&
                                                  (x.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.InProgress ||
                                                   x.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.NotStarted ||
                                                   x.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.Starting));
        }





        public void CheckMilestoneCompletion(int matterId,
            ref bool hasDocPrepBeenCompleted, ref bool hasBookSettlementBeenCompleted, ref bool hasPrepareSettlementBeenCompleted, ref bool hasDocsReturnedBeenCompleted, ref bool hasSendProcessedDocsBeenCompleted, ref bool hasConsentMatterDetailsBeenCompleted)
        {
            var components = GetMatterComponentsForMatter(matterId);

            hasDocPrepBeenCompleted = HasMilestoneCompleted(components, (int)WFComponentEnum.DocPreparation);
            hasBookSettlementBeenCompleted = HasMilestoneCompleted(components, (int)WFComponentEnum.BookSettlement) || HasMilestoneCompleted(components, (int)WFComponentEnum.FastREFIBookSettlementFunding);
            hasPrepareSettlementBeenCompleted = HasMilestoneCompleted(components, (int)WFComponentEnum.PrepareSettlementInstrDisch) ||
                                                HasMilestoneCompleted(components, (int)WFComponentEnum.PrepareSettlementInstr);
            hasDocsReturnedBeenCompleted = HasMilestoneCompleted(components, (int)WFComponentEnum.DocsReturned);
            hasSendProcessedDocsBeenCompleted = HasMilestoneCompleted(components, (int)WFComponentEnum.SendProcessedDocs);
            hasConsentMatterDetailsBeenCompleted = HasMilestoneCompleted(components, (int)WFComponentEnum.ConsentMatterDetails);
        }

        public List<MatterWFComponent> GetMatterComponentsForMatter(int matterId)
        {
            return context.MatterWFComponents.Where(x => x.MatterId == matterId &&
                (x.WFComponentStatusTypeId != (int)MatterWFComponentStatusTypeEnum.Cancelled ||
                 x.WFComponentStatusTypeId != (int)MatterWFComponentStatusTypeEnum.Deleted))
                .OrderBy(o => o.DisplayOrder).ToList();
        }

        public List<MatterWFComponent> GetMatterComponentsForMatters(List<int> matters)
        {
            return context.MatterWFComponents.Where(x =>
                (x.WFComponentStatusTypeId != (int)MatterWFComponentStatusTypeEnum.Cancelled ||
                 x.WFComponentStatusTypeId != (int)MatterWFComponentStatusTypeEnum.Deleted) &&
                 matters.Contains(x.MatterId)).ToList();
        }

        public List<MatterWFComponent> GetMatterComponentsForUser(User usr)
        {
            var qry = context.MatterWFComponents.Where(x =>
                (x.WFComponentStatusTypeId != (int)MatterWFComponentStatusTypeEnum.Cancelled ||
                 x.WFComponentStatusTypeId != (int)MatterWFComponentStatusTypeEnum.Deleted));

            switch (usr.UserTypeId)
            {
                case (int)UserTypeEnum.Lender:
                    qry = qry.Where(m => m.Matter.LenderId == usr.LenderId);
                    break;
                case (int)UserTypeEnum.MortMgr:
                    qry = qry.Where(m => m.Matter.MortMgrId == usr.MortMgrId);
                    break;
                case (int)UserTypeEnum.Broker:
                    qry = qry.Where(m => m.Matter.BrokerId == usr.BrokerId);
                    break;
            }

            return qry.ToList();
        }

        public bool HasBookSettlementCompleted(int matterId)
        {
            var components = GetMatterComponentsForMatter(matterId);
            return HasMilestoneCompleted(components, (int)WFComponentEnum.BookSettlement) || HasMilestoneCompleted(components, (int)WFComponentEnum.FastREFIBookSettlementFunding);
        }

        public bool HasSettlementCompleted(int matterId, int matterTypeId)
        {
            var components = GetMatterComponentsForMatter(matterId);

            return
                HasMilestoneCompleted(components,
                 matterTypeId == (int)MatterGroupTypeEnum.Discharge ?
                 (int)WFComponentEnum.SettlementCompletedDischarge : (int)WFComponentEnum.SettlementCompleteNewLoans);

        }

        public bool IsMilestoneNotStarted(List<MatterWFComponent> components, int wfComponentId)
        {
            var milestoneComponents = components.Where(x => x.WFComponentId == wfComponentId);
            if (!milestoneComponents.Any())
                return false;

            if (milestoneComponents.Any(y => y.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.NotStarted))
                return true;
            return false;
        }
        public bool IsMilestoneActive(List<MatterWFComponent> components, int wfComponentId)
        {
            var milestoneComponents = components.Where(x => x.WFComponentId == wfComponentId);
            if (!milestoneComponents.Any())
                return false;

            if (milestoneComponents.Any(y => y.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.InProgress ||
                                               y.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.Starting))
                return true;
            return false;
        }

        // Refactoring Required
        // THIS FUNCTION LOOKS WRONG
        public bool HasMilestoneCompleted(List<MatterWFComponent> components, int wfComponentId)
        {
            var milestoneComponents = components.Where(x => x.WFComponentId == wfComponentId);
            if (!milestoneComponents.Any())
                return false;

            if (milestoneComponents.All(x => x.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.Complete || x.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.Deleted))
                return true;
            return false;
        }


        public bool HasMilestoneCompletedNotDeleted(List<MatterWFComponent> components, int wfComponentId)
        {
            var milestoneComponents = components.Where(x => x.WFComponentId == wfComponentId);
            if (!milestoneComponents.Any())
                return false;

            if (milestoneComponents.All(x => x.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.Complete && (x.DisplayStatusTypeId == (int)DisplayStatusTypeEnum.Default || x.DisplayStatusTypeId == (int)DisplayStatusTypeEnum.Display)))
                return true;
            return false;
        }

        public IEnumerable<MatterCustomEntities.MatterWFComponentParamView> GetMatterWFComponentParamsView(int matterWFComponentId)
        {
            IQueryable<MatterWFComponent> components = context.MatterWFComponents.Where(m => m.MatterWFComponentId == matterWFComponentId);
            return GetMatterWFComponentParamsView(components);
        }

        public int GetEarliestWFComponentForMatter(int matterId, IEnumerable<int?> components)
        {
            return (from w in CurrentMatterWFComponentsForDisplay()
                    join c in components on w.WFComponentId equals c
                    where w.MatterId == matterId
                    orderby w.DisplayOrder
                    select w.WFComponentId
                    ).Take(1).First();
        }

        public int? GetEarliestMatterWFComponentForMatter(int matterId, IEnumerable<int?> components)
        {
            var mwfc = (from w in CurrentMatterWFComponentsForDisplay()
                        join c in components on w.WFComponentId equals c
                        where w.MatterId == matterId
                        orderby w.DisplayOrder
                        select w.MatterWFComponentId
                    );

            if (mwfc != null && mwfc.Any())
            {
                return mwfc.Take(1).First();
            }
            return null;
        }

        public MatterWFComponent GetLatestMatterWFComponentForMatterComponent(int matterId, int wfComponentId, bool includeInactive = false)
        {
            var currComponents = includeInactive ? CurrentMatterWFComponentsForDisplayWithInactive() : CurrentMatterWFComponentsForDisplay();

            return currComponents
                .Where(x => x.MatterId == matterId && x.WFComponentId == wfComponentId)
                .OrderByDescending(w => w.DisplayOrder)
                .FirstOrDefault();
        }

        public MatterWFComponent GetLatestMatterWFComponentForMatterComponent(List<MatterWFComponent> components, int wfComponentId)
        {
            return components
                .Where(x => x.WFComponentId == wfComponentId)
                .OrderByDescending(w => w.DisplayOrder)
                .FirstOrDefault();
        }

        public void AddMatterEvent(MatterCustomEntities.MatterWFComponentView mwfComp, Slick_Domain.Enums.MatterEventTypeList matterEventTypeId, string eventNotes)
        {
            MatterEvent me = new MatterEvent();
            me.MatterId = mwfComp.MatterId;
            me.MatterWFComponentId = mwfComp.MatterWFComponentId;
            me.MatterEventTypeId = (int)matterEventTypeId;
            me.MatterEventStatusTypeId = (int)Enums.MatterEventStatusTypeEnum.Good;
            me.EventNotes = eventNotes;
            me.EventDate = DateTime.Now.ToUniversalTime();
            me.EventByUserId = GlobalVars.CurrentUserId ?? Slick_Domain.Common.DomainConstants.SystemUserId;

            context.MatterEvents.Add(me);

            Matter mt = context.Matters.FirstOrDefault(m => m.MatterId == me.MatterId);
            mt.WarehouseUpdateReq = true;

            context.SaveChanges();
        }

        public int AddMatterEvent(int matterId, int? matterWFComponentId, MatterEventTypeList matterEventTypeId, string eventNotes, bool isAutomatedEvent = false,
            MatterEventStatusTypeEnum matterEventStatusTypeEnum = MatterEventStatusTypeEnum.Good, string eventCreatedByNotes = null, int? reasonId = null, string reasonNotes = null, int? updatedByUserId = null)
        {
            MatterEvent me = new MatterEvent();
            me.MatterId = matterId;
            me.MatterWFComponentId = matterWFComponentId.HasValue ? matterWFComponentId > 0 ? matterWFComponentId : null : null;
            me.MatterEventTypeId = (int)matterEventTypeId;
            me.MatterEventStatusTypeId = (int)matterEventStatusTypeEnum;
            me.EventNotes = eventNotes;
            me.EventDate = DateTime.Now.ToUniversalTime();
            me.EventByUserId = updatedByUserId == null ? (isAutomatedEvent ? DomainConstants.SystemUserId : GlobalVars.CurrentUser.UserId) : (int)updatedByUserId;
            me.EventCreatedByNotes = eventCreatedByNotes;

            context.MatterEvents.Add(me);



            context.SaveChanges();

            //if (reasonId.HasValue && reasonId > 0)
            //{
            //    MatterEventReason dbReason = new MatterEventReason() { ReasonId = reasonId.Value, MatterEventReasonNotes = reasonNotes };
            //    context.MatterEventReasons.Add(dbReason);
            //    context.SaveChanges();
            //    context.MatterEvents.FirstOrDefault(m => m.MatterEventId == me.MatterEventId).MatterEventReasonId = dbReason.MatterEventReasonId;
            //    context.SaveChanges();
            //}



            return me.MatterEventId;
        }

        public int AddMatterEventBackdate(int matterId, int? matterWFComponentId, MatterEventTypeList matterEventTypeId, string eventNotes, bool isAutomatedEvent = false,
           MatterEventStatusTypeEnum matterEventStatusTypeEnum = MatterEventStatusTypeEnum.Good, string eventCreatedByNotes = null, DateTime? ManualDate = null, int? reasonId = null, string reasonNotes = null)
        {
            MatterEvent me = new MatterEvent();
            me.MatterId = matterId;
            me.MatterWFComponentId = matterWFComponentId;
            me.MatterEventTypeId = (int)matterEventTypeId;
            me.MatterEventStatusTypeId = (int)matterEventStatusTypeEnum;
            me.EventNotes = eventNotes;
            me.EventDate = ManualDate.Value.ToUniversalTime();
            me.EventByUserId = isAutomatedEvent ? DomainConstants.SystemUserId : GlobalVars.CurrentUser.UserId;
            me.EventCreatedByNotes = eventCreatedByNotes;

            context.MatterEvents.Add(me);

            context.SaveChanges();
            //if (reasonId.HasValue && reasonId > 0)
            //{
            //    MatterEventReason dbReason = new MatterEventReason() { ReasonId = reasonId.Value, MatterEventReasonNotes = reasonNotes };
            //    context.MatterEventReasons.Add(dbReason);
            //    context.SaveChanges();
            //    context.MatterEvents.FirstOrDefault(m => m.MatterEventId == me.MatterEventId).MatterEventReasonId = dbReason.MatterEventReasonId;
            //    context.SaveChanges();
            //}

            return me.MatterEventId;
        }


        public void StartMatterWFComponent(int matterWFComponentId)
        {
            var mwf = context.MatterWFComponents.FirstOrDefault(m => m.MatterWFComponentId == matterWFComponentId);
            mwf.WFComponentStatusTypeId = (int)Enums.MatterWFComponentStatusTypeEnum.InProgress;
            mwf.UpdatedDate = DateTime.Now;
            mwf.UpdatedByUserId = GlobalVars.CurrentUserId ?? Slick_Domain.Common.DomainConstants.SystemUserId;
            context.SaveChanges();
            UpdateAllDueDates(mwf.MatterId, mwf.WFComponentId);
            //AddMatterEvent(matterWFComponentId, Slick_Domain.Enums.MatterEventTypeList.MilestoneStarted, null);
        }

        public void CloseMatter(int matterId)
        {
            var mat = context.Matters.FirstOrDefault(m => m.MatterId == matterId);
            mat.MatterStatusTypeId = (int)Enums.MatterStatusTypeEnum.Closed;
            mat.UpdatedDate = DateTime.Now;
            mat.UpdatedByUserId = GlobalVars.CurrentUserId ?? GlobalVars.CurrentUserId ?? Slick_Domain.Common.DomainConstants.SystemUserId;
            context.SaveChanges();
        }

        public void UpdateFileOwnerForActiveComponents(int matterId, int fileOwnerUserId)
        {
            var mwfComponents = context.MatterWFComponents.Where(x => x.MatterId == matterId && x.WFComponentStatusType.IsActiveStatus).ToList();
            foreach (var wf in mwfComponents)
            {
                if (wf.TaskAllocTypeId == (int)TaskAllocationTypeEnum.FileManager)
                {
                    wf.TaskAllocUserId = fileOwnerUserId;
                    wf.UpdatedByUserId = GlobalVars.CurrentUserId ?? Slick_Domain.Common.DomainConstants.SystemUserId;
                    wf.UpdatedDate = DateTime.Now;
                }

            }
            context.SaveChanges();
        }

        public void ActivateInactiveMilestones(int matterId, bool sendEmails = true)
        {

            var securities = context.MatterSecurities.Where(x => x.MatterId == matterId && !x.Deleted).ToList();

            if (securities == null || !securities.Any()) //if no securities then we are probably restarting at check instructions
            {
                //var checkInstructions = GetValidComponentsForMatter(matterId).Where(w=>w.WFComponentId == 
                //    (int)WFComponentEnum.CheckInstructions && (w.WFComponentStatusTypeId != (int)MatterWFComponentStatusTypeEnum.Deleted || 
                //                                                w.WFComponentStatusTypeId != (int)MatterWFComponentStatusTypeEnum.Cancelled || 
                //                                                w.WFComponentStatusTypeId != (int)MatterWFComponentStatusTypeEnum.Complete))
                //                                                                                                            .FirstOrDefault(); 
                //if (checkInstructions != null)
                //{
                //    checkInstructions.WFComponentStatusTypeId = (int)MatterWFComponentStatusTypeEnum.Starting;
                //    context.SaveChanges();
                ActivateComponents(matterId);
                return;

            }


            var hasPexa = securities.Any(x => x.SettlementTypeId == (int)SettlementTypeEnum.PEXA);
            var hasPaper = securities.Any(x => x.SettlementTypeId == (int)SettlementTypeEnum.Paper);
            var hasPexaClearTitle = hasPexa && securities.Any(x => x.SettlementTypeId == (int)SettlementTypeEnum.PEXA && x.MatterTypeId == (int)MatterTypeEnum.ClearTitle);
            var allPaper = securities.All(x => x.SettlementTypeId == (int)SettlementTypeEnum.Paper);

            var components = GetValidComponentsForMatter(matterId).Where(x => x.SettlementTypeId == (int)SettlementTypeEnum.Paper || x.SettlementTypeId == (int)SettlementTypeEnum.PEXA).ToList();

            foreach (var c in components.Where(x => x.SettlementTypeId == (int)SettlementTypeEnum.PEXA))
            {
                if (allPaper && c.WFComponentId == (int)WFComponentEnum.PEXAWorkspace)
                {
                    c.DisplayStatusTypeId = c.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.NotStarted ?
                                (int)DisplayStatusTypeEnum.InactiveNeverShown : (int)DisplayStatusTypeEnum.Inactive;
                }
                else
                {
                    c.DisplayStatusTypeId = hasPexa ? (int)DisplayStatusTypeEnum.Default : (int)DisplayStatusTypeEnum.Inactive;
                }
            }

            foreach (var c in components.Where(x => x.SettlementTypeId == (int)SettlementTypeEnum.Paper))
            {
                c.DisplayStatusTypeId = hasPaper ? (int)DisplayStatusTypeEnum.Default : (int)DisplayStatusTypeEnum.Inactive;
            }
            context.SaveChanges();

            ActivateComponents(matterId, sendEmails, false);
        }

        private IEnumerable<MatterWFComponent> GetValidComponentsForMatter(int matterId)
        {
            return context.MatterWFComponents.Where(x => x.MatterId == matterId &&
               (x.WFComponentStatusTypeId != (int)MatterWFComponentStatusTypeEnum.Cancelled || x.WFComponentStatusTypeId != (int)MatterWFComponentStatusTypeEnum.Deleted)
               && x.DisplayStatusTypeId != (int)DisplayStatusTypeEnum.InactiveNeverShown)
                        .OrderByDescending(o => o.DisplayOrder);
        }

        public MatterCustomEntities.MatterWFComponentView GetNextMatterWFComponentView(MatterCustomEntities.MatterWFComponentView currMWFCompView)
        {
            IQueryable<MatterWFComponent> mwfComponents = CurrentMatterWFComponentsForDisplay()
                .Where(m => m.MatterId == currMWFCompView.MatterId && m.DisplayOrder > currMWFCompView.DisplayOrder);

            MatterCustomEntities.MatterWFComponentView retVal = GetMatterWFComponentsView(mwfComponents).OrderBy(m => m.DisplayOrder).FirstOrDefault();

            if (retVal == null)
                return currMWFCompView;
            else
                return retVal;
        }

        public MatterWFComponent GetPrevMatterWFComponent(int matterWFComponentId)
        {
            var currComponent = context.MatterWFComponents.FirstOrDefault(x => x.MatterWFComponentId == matterWFComponentId);
            if (currComponent == null) return null;

            IQueryable<MatterWFComponent> mwfComponents = CurrentMatterWFComponentsForDisplay()
                .Where(m => m.MatterId == currComponent.MatterId && m.DisplayOrder < currComponent.DisplayOrder);

            return mwfComponents.OrderByDescending(m => m.DisplayOrder).FirstOrDefault();
        }

        public int? GetPrevWFComponentId(int matterId, int displayOrder, ref int? lastMatterWFComponentId)
        {
            var component = CurrentMatterWFComponentsForDisplay()
                .Where(x => x.MatterId == matterId && x.DisplayOrder < displayOrder)
                .OrderByDescending(x => x.DisplayOrder).FirstOrDefault();

            lastMatterWFComponentId = component?.MatterWFComponentId;
            return component?.WFComponentId;
        }

        private IQueryable<MatterWFComponent> CurrentMatterWFComponentsForDisplay()
        {
            return context.MatterWFComponents.Where(x => x.WFComponentStatusTypeId != (int)MatterWFComponentStatusTypeEnum.Cancelled
                                                               && x.WFComponentStatusTypeId != (int)MatterWFComponentStatusTypeEnum.Deleted
                                                               && x.DisplayStatusTypeId != (int)DisplayStatusTypeEnum.Inactive
                                                                && x.DisplayStatusTypeId != (int)DisplayStatusTypeEnum.InactiveNeverShown);
        }

        private IQueryable<MatterWFComponent> CurrentMatterWFComponentsForDisplayWithInactive()
        {
            return context.MatterWFComponents.Where(x => x.WFComponentStatusTypeId != (int)MatterWFComponentStatusTypeEnum.Cancelled
                                                               && x.WFComponentStatusTypeId != (int)MatterWFComponentStatusTypeEnum.Deleted
                                                                && x.DisplayStatusTypeId != (int)DisplayStatusTypeEnum.InactiveNeverShown);
        }


        public List<MatterWFComponent> ClientReissue(int matterId, string inputText)
        {
            List<MatterWFComponent> reissueList = new List<MatterWFComponent>();
            WFComponentEnum[] ReissueEnums =
            {
                WFComponentEnum.DocPreparation,
                WFComponentEnum.DocPreparationQA,
                WFComponentEnum.SendProcessedDocs
            };

            //int matterId = CurrMatter.matterId;

            var existingComps = GetMatterComponentsForMatter(matterId);

            if (existingComps.Any(x => x.WFComponentId == (int)WFComponentEnum.DocumentsApprovedByLender))
            {
                ReissueEnums = new WFComponentEnum[]
                   {
                        WFComponentEnum.DocPreparation,
                        WFComponentEnum.DocPreparationQA,
                        WFComponentEnum.DocumentsApprovedByLender,
                        WFComponentEnum.SendProcessedDocs
                    };
            }

            var activeComps = existingComps.Where(w => w.WFComponentStatusTypeId == (int)Slick_Domain.Enums.MatterWFComponentStatusTypeEnum.InProgress || w.WFComponentStatusTypeId == (int)Slick_Domain.Enums.MatterWFComponentStatusTypeEnum.Starting);
            bool active = true;

            var ActiveMatterWfComp = GetLastActiveComponentForMatter(matterId);
            var LastCompletedComp = GetLastCompletedComponentForMatter(matterId);

            MatterWFComponent LastAddedComp = new MatterWFComponent();

            foreach (var reissueComp in ReissueEnums)
            {
                //if (!activeComps.Any(w => w.WFComponentId == (int)reissueComp))
                //{
                int PrevAddedCompId = LastAddedComp.MatterWFComponentId;
                int PrevAddedComponent = LastAddedComp.WFComponentId;
                var existingComp = existingComps.Where(w => w.WFComponentId == (int)reissueComp).FirstOrDefault();
                LastAddedComp = AddMatterWFCompIntoExistingMatterWFComp(matterId, reissueComp, inputText, active);
                reissueList.Add(LastAddedComp);
                if (existingComp != null)
                {
                    var newComp = context.MatterWFComponents.Where(m => m.MatterWFComponentId == LastAddedComp.MatterWFComponentId).FirstOrDefault();
                    newComp.WFComponentDueDateFormulaId = existingComp.WFComponentDueDateFormulaId;
                    newComp.DueDateFormulaOffset = existingComp.DueDateFormulaOffset;
                    newComp.DefTaskAllocTypeId = existingComp.DefTaskAllocTypeId;
                    newComp.DefTaskAllocWFComponentId = existingComp.DefTaskAllocWFComponentId;
                    context.SaveChanges();
                }//this will help us with due dates + allocation
                if (active)//the first milestone will be active so doesn't need dependencies. The next ones need to be dependent of the previous milestones. (active flag is set just for the docprep / first milestone in the list)
                {
                    AddMatterEvent(matterId, LastAddedComp.MatterWFComponentId, MatterEventTypeList.MilestoneInserted, $"INSERTED AFTER \"{LastCompletedComp.WFComponent.WFComponentName}\"", eventCreatedByNotes: "By Client Reissue");
                }
                else
                {
                    context.MatterWFComponentDependents.Add(new MatterWFComponentDependent()
                    {
                        MatterWFComponentId = LastAddedComp.MatterWFComponentId,
                        DependentMatterWFComponentId = PrevAddedCompId
                    });
                    context.SaveChanges();

                    AddMatterEvent(matterId, LastAddedComp.MatterWFComponentId,
                        MatterEventTypeList.MilestoneInserted, $"INSERTED AFTER \"{context.WFComponents.Where(w => w.WFComponentId == PrevAddedComponent).Select(x => x.WFComponentName).FirstOrDefault()}\"",
                        eventCreatedByNotes: "By Client Reissue");

                }
                active = false;
                //}


            }


            context.MatterWFComponentDependents.RemoveRange(context.MatterWFComponentDependents.Where(x => x.MatterWFComponentId == ActiveMatterWfComp.MatterWFComponentId));
            context.SaveChanges();
            MatterWFComponentDependent newDep = new MatterWFComponentDependent() { MatterWFComponentId = ActiveMatterWfComp.MatterWFComponentId, DependentMatterWFComponentId = LastAddedComp.MatterWFComponentId };

            if (!context.MatterWFComponentDependents.Any(x => x.MatterWFComponentId == newDep.MatterWFComponentId || x.MatterWFComponentDependentId == newDep.DependentMatterWFComponentId))
            {
                context.MatterWFComponentDependents.Add(newDep);
            }

            ActiveMatterWfComp.WFComponentStatusTypeId = (int)Slick_Domain.Enums.MatterWFComponentStatusTypeEnum.NotStarted;
            context.SaveChanges();

            AllocateTasks(matterId);
            UpdateDueDates(matterId);

            return reissueList;
        }

        public MatterWFComponent InsertFloater(int matterId, int displayOrder, int wfComponentId, TaskAllocationTypeEnum allocType, DateTime dueDate, int? slaveToMilestoneId = null)
        {
            var toShift = context.MatterWFComponents.Where(x => x.MatterId == matterId && x.DisplayOrder > displayOrder);
            foreach (var comp in toShift)
            {
                comp.DisplayOrder += 1;
            }
            context.SaveChanges();
            MatterWFComponent newComp = new MatterWFComponent()
            {
                WFComponentId = wfComponentId,
                DisplayOrder = displayOrder + 1,
                MatterId = matterId,
                WFComponentStatusTypeId = (int)MatterWFComponentStatusTypeEnum.Starting,
                DisplayStatusTypeId = (int)Slick_Domain.Enums.DisplayStatusTypeEnum.Default,
                UpdatedByUserId = (GlobalVars.CurrentUser.UserId == 0 ? 1 : GlobalVars.CurrentUser.UserId),
                UpdatedDate = DateTime.Now,
                DueDateReassigned = true,
                DueDate = dueDate,
                DefTaskAllocTypeId = (int)allocType,
                TaskAllocTypeId = (int)allocType,
                SlaveToMatterWFComponentId = slaveToMilestoneId
            };
            context.MatterWFComponents.Add(newComp);
            context.SaveChanges();
            AddMatterEvent(matterId, newComp.MatterWFComponentId, MatterEventTypeList.MilestoneStarted, null);

            context.SaveChanges();
            return newComp;
        }
        public MatterWFComponent InsertFloaterInclusive(int matterId, int displayOrder, int wfComponentId, TaskAllocationTypeEnum allocType, DateTime? dueDate, int? slaveToMilestoneId = null, int? dueDateFormulaId = null, int? dueTimeFormulaId = null)
        {
            var toShift = context.MatterWFComponents.Where(x => x.MatterId == matterId && x.DisplayOrder >= displayOrder);
            foreach (var comp in toShift)
            {
                comp.DisplayOrder += 1;
            }
            context.SaveChanges();
            MatterWFComponent newComp = new MatterWFComponent()
            {
                WFComponentId = wfComponentId,
                DisplayOrder = displayOrder + 1,
                MatterId = matterId,
                WFComponentStatusTypeId = (int)MatterWFComponentStatusTypeEnum.Starting,
                DisplayStatusTypeId = (int)Slick_Domain.Enums.DisplayStatusTypeEnum.Default,
                UpdatedByUserId = (GlobalVars.CurrentUser.UserId == 0 ? 1 : GlobalVars.CurrentUser.UserId),
                UpdatedDate = DateTime.Now,
                WFComponentDueDateFormulaId = dueDateFormulaId,
                WFComponentDueTimeFormulaId = dueTimeFormulaId,
                DueDateReassigned = dueDate.HasValue,
                DueDate = dueDate,
                DefTaskAllocTypeId = (int)allocType,
                TaskAllocTypeId = (int)allocType,
                SlaveToMatterWFComponentId = slaveToMilestoneId
            };
            context.MatterWFComponents.Add(newComp);
            context.SaveChanges();
            AddMatterEvent(matterId, newComp.MatterWFComponentId, MatterEventTypeList.MilestoneStarted, null);

            context.SaveChanges();
            return newComp;
        }

        public List<MatterWFComponent> InsertMilestones(int matterId, string inputText, WFComponentEnum[] milestones, int? taskAllocTypeId = null, int? slaveToMilestoneId = null)
        {
            List<MatterWFComponent> reissueList = new List<MatterWFComponent>();

            List<int> milestoneIds = milestones.Select(w => (int)w).ToList();
            context.MatterWFComponents.Where(w => w.MatterId == matterId && milestoneIds.Contains(w.WFComponentId)).ToList()
                .ForEach(x => { x.DisplayStatusTypeId = (int)DisplayStatusTypeEnum.Hide; });

            //int matterId = CurrMatter.matterId;

            var existingComps = GetMatterComponentsForMatter(matterId);

            var activeComps = existingComps.Where(w => w.WFComponentStatusTypeId == (int)Slick_Domain.Enums.MatterWFComponentStatusTypeEnum.InProgress || w.WFComponentStatusTypeId == (int)Slick_Domain.Enums.MatterWFComponentStatusTypeEnum.Starting);
            bool active = true;

            var activeMatterWfComp = GetLastActiveComponentForMatter(matterId);
            var lastCompletedComp = GetLastCompletedComponentForMatter(matterId);

            MatterWFComponent lastAddedComp = new MatterWFComponent();

            foreach (var milestone in milestones)
            {
                //if (!activeComps.Any(w => w.WFComponentId == (int)reissueComp))
                //{
                int prevAddedCompId = lastAddedComp.MatterWFComponentId;
                int prevAddedComponent = lastAddedComp.WFComponentId;
                var existingComp = existingComps.Where(w => w.WFComponentId == (int)milestone).FirstOrDefault();
                lastAddedComp = AddMatterWFCompIntoExistingMatterWFComp(matterId, milestone, inputText, active);
                if (slaveToMilestoneId.HasValue)
                {
                    lastAddedComp.SlaveToMatterWFComponentId = slaveToMilestoneId;
                }
                reissueList.Add(lastAddedComp);
                if (existingComp != null)
                {
                    var newComp = context.MatterWFComponents.Where(m => m.MatterWFComponentId == lastAddedComp.MatterWFComponentId).FirstOrDefault();
                    newComp.WFComponentDueDateFormulaId = existingComp.WFComponentDueDateFormulaId;
                    newComp.DueDateFormulaOffset = existingComp.DueDateFormulaOffset;
                    newComp.DefTaskAllocTypeId = existingComp.DefTaskAllocTypeId;
                    newComp.DefTaskAllocWFComponentId = existingComp.DefTaskAllocWFComponentId;
                    context.SaveChanges();
                }//this will help us with due dates + allocation

                if (taskAllocTypeId.HasValue)
                {
                    lastAddedComp.DefTaskAllocTypeId = taskAllocTypeId.Value;
                }
                if (active)//the first milestone will be active so doesn't need dependencies. The next ones need to be dependent of the previous milestones. (active flag is set just for the docprep / first milestone in the list)
                {
                    AddMatterEvent(matterId, lastAddedComp.MatterWFComponentId, MatterEventTypeList.MilestoneInserted, $"INSERTED AFTER \"{lastCompletedComp.WFComponent.WFComponentName}\"", eventCreatedByNotes: $"By {inputText}");
                }
                else
                {
                    context.MatterWFComponentDependents.Add(new MatterWFComponentDependent()
                    {
                        MatterWFComponentId = lastAddedComp.MatterWFComponentId,
                        DependentMatterWFComponentId = prevAddedCompId
                    });
                    context.SaveChanges();

                    AddMatterEvent(matterId, lastAddedComp.MatterWFComponentId,
                        MatterEventTypeList.MilestoneInserted, $"INSERTED AFTER \"{context.WFComponents.Where(w => w.WFComponentId == prevAddedComponent).Select(x => x.WFComponentName).FirstOrDefault()}\"", eventCreatedByNotes: $"By {inputText}"
                        );

                }
                active = false;
                //}


            }


            context.MatterWFComponentDependents.RemoveRange(context.MatterWFComponentDependents.Where(x => x.MatterWFComponentId == activeMatterWfComp.MatterWFComponentId));
            context.SaveChanges();
            MatterWFComponentDependent newDep = new MatterWFComponentDependent() { MatterWFComponentId = activeMatterWfComp.MatterWFComponentId, DependentMatterWFComponentId = lastAddedComp.MatterWFComponentId };

            if (!context.MatterWFComponentDependents.Any(x => x.MatterWFComponentId == newDep.MatterWFComponentId || x.MatterWFComponentDependentId == newDep.DependentMatterWFComponentId))
            {
                context.MatterWFComponentDependents.Add(newDep);
            }

            activeMatterWfComp.WFComponentStatusTypeId = (int)Slick_Domain.Enums.MatterWFComponentStatusTypeEnum.NotStarted;
            context.SaveChanges();

            AllocateTasks(matterId);
            UpdateDueDates(matterId);

            return reissueList;
        }
        public MatterCustomEntities.MatterWFComponentView GetPrevMatterWFComponentView(int matterWFComponentId)
        {
            var currComponent = context.MatterWFComponents.FirstOrDefault(x => x.MatterWFComponentId == matterWFComponentId);
            if (currComponent == null) return null;

            IQueryable<MatterWFComponent> mwfComponents =
                CurrentMatterWFComponentsForDisplay().Where(m => m.MatterId == currComponent.MatterId && m.DisplayOrder < currComponent.DisplayOrder);

            MatterCustomEntities.MatterWFComponentView retVal = GetMatterWFComponentsView(mwfComponents).OrderByDescending(m => m.DisplayOrder).FirstOrDefault();

            return retVal;
        }


        public MatterCustomEntities.MatterWFComponentView GetPrevMatterWFComponentView(MatterCustomEntities.MatterWFComponentView currMWFCompView)
        {
            IQueryable<MatterWFComponent> mwfComponents = CurrentMatterWFComponentsForDisplay()
                .Where(m => m.MatterId == currMWFCompView.MatterId && m.DisplayOrder < currMWFCompView.DisplayOrder);
            MatterCustomEntities.MatterWFComponentView retVal = GetMatterWFComponentsView(mwfComponents).OrderByDescending(m => m.DisplayOrder).FirstOrDefault();

            if (retVal == null)
                return currMWFCompView;
            else
                return retVal;
        }

        public MatterCustomEntities.MatterWFComponentView ProgressToNextMileStone(MatterCustomEntities.MatterWFComponentView currMWFCompView, bool sendEmails = true, bool forceNoReply = false, int updatedByUserId = 0)
        {
            if(updatedByUserId == 0)
            {
                if (GlobalVars.CurrentUser?.IsBackgroundWorker == true)
                {
                    forceNoReply = true;
                    updatedByUserId = GlobalVars.CurrentUser.UserId;

                }
                else
                {
                    updatedByUserId = GlobalVars.CurrentUserId.HasValue && GlobalVars.CurrentUserId > 0 ? GlobalVars.CurrentUserId.Value : Slick_Domain.Common.DomainConstants.SystemUserId;
                }
            }
            

            MatterCustomEntities.MatterWFComponentView retVal = currMWFCompView;

            Repository<MatterWFComponent> mrep = new Repository<MatterWFComponent>(context);

            MatterWFComponent mtc = context.MatterWFComponents.FirstOrDefault(m => m.MatterWFComponentId == currMWFCompView.MatterWFComponentId);
            mtc.WFComponentStatusTypeId = (int)Slick_Domain.Enums.MatterWFComponentStatusTypeEnum.Complete;
            mtc.TaskAllocUserId = null;
            mtc.TaskAllocTypeId = null;
            mtc.UpdatedByUserId = updatedByUserId;
            mtc.UpdatedDate = DateTime.Now;

            List<int> milestonesRequiringDocPackSaving = new List<int>() { (int)WFComponentEnum.SendProcessedDocs, (int)WFComponentEnum.SettlementCompletedDischarge, (int)WFComponentEnum.SettlementCompleteNewLoans };


            if (currMWFCompView.LenderName?.ToUpper()?.Contains("RACQ") == true && milestonesRequiringDocPackSaving.Contains(currMWFCompView.WFComponentId))
            {
                SaveMatterWFComponentDocumentState(currMWFCompView.MatterId, currMWFCompView.MatterWFComponentId);
            }


            AddMatterEvent(mtc.MatterId, mtc.MatterWFComponentId, Slick_Domain.Enums.MatterEventTypeList.MilestoneComplete, null, updatedByUserId: updatedByUserId);

            if (mtc.WFComponentId == (int)WFComponentEnum.CloseMatter)
            {
                mtc.Matter.MatterStatusTypeId = (int)MatterStatusTypeEnum.Closed;
            }
            if (currMWFCompView.WFComponentId == (int)Enums.WFComponentEnum.PrepareSettlementInstrDisch)
            {
                var aRep = new AccountsRepository(context);
                aRep.UpdateExpectedTrustTransactions(currMWFCompView.MatterId);
                context.SaveChanges();
            }

            //Now Activate all components which have all their dependencies completed
            ActivateComponents(currMWFCompView.MatterId, sendEmails, forceNoReply, updatedByUserId: updatedByUserId);

            if (sendEmails)
            {
                SendAutomatedEmails(currMWFCompView.MatterId, currMWFCompView.WFComponentId, currMWFCompView.MatterWFComponentId, (int)MilestoneEmailTriggerTypeEnum.Completed, forceNoReply: forceNoReply, useSmtp: true);

                if (!context.Matters.FirstOrDefault(m => m.MatterId == currMWFCompView.MatterId).IsTestFile)
                {
                    CheckAndSendBackChannelMessages(currMWFCompView.MatterWFComponentId, currMWFCompView.WFComponentId, currMWFCompView.LenderId, currMWFCompView.MortMgrId, BackChannelMessageTriggerTypeEnum.Completed, currMWFCompView.MatterTypeGroupId);
                }
            }

            return GetNextMatterWFComponentView(currMWFCompView);
        }

        public void CheckAndSendBackChannelMessages(int matterWFCompId, int wfComponentId, int lenderId, int? mortMgrId, BackChannelMessageTriggerTypeEnum trigger, int matterGroupTypeId)
        {

            int? loanTypeId = context.MatterWFComponents.Select(x => new { x.MatterWFComponentId, x.Matter.LoanTypeId }).FirstOrDefault(m => m.MatterWFComponentId == matterWFCompId).LoanTypeId;
            //if there are any set up for mortgage manager specific, use them otherwise use the non-mortgage manager specific ones if they exist.
            if (context.Lenders.Where(l => l.LenderId == lenderId).Select(x => x.BackChannelEnable).FirstOrDefault())
            {
                if (context.WFComponentBackchannelMessageDefinitions.Any(x => x.LenderId == lenderId && x.WFComponentId == wfComponentId && x.BackChannelMessageTriggerTypeId == (int)trigger))
                {

                    var messages = context.WFComponentBackchannelMessageDefinitions.Where(x => x.LenderId == lenderId && x.MortMgrId == mortMgrId && x.WFComponentId == wfComponentId && x.BackChannelMessageTriggerTypeId == (int)trigger && (!x.MatterGroupTypeId.HasValue || x.MatterGroupTypeId == matterGroupTypeId));
                    if (!messages.Any())
                    {
                        messages = context.WFComponentBackchannelMessageDefinitions.Where(x => x.LenderId == lenderId && !x.MortMgrId.HasValue && x.WFComponentId == wfComponentId && x.BackChannelMessageTriggerTypeId == (int)trigger && (!x.MatterGroupTypeId.HasValue || x.MatterGroupTypeId == matterGroupTypeId));
                    }

                    messages = messages.Where(l => l.LoanTypeId == null || l.LoanTypeId == loanTypeId);

                    if (messages.Any(x => x.LoanTypeId.HasValue))
                    {
                        messages = messages.Where(l => l.LoanTypeId.HasValue);
                    }


                    if (messages.Any())
                    {

                        foreach (var message in messages.Where(c => !string.IsNullOrEmpty(c.Code)))
                        {
                            var existingMessage = context.MatterWFComponentBackchannelMessages.FirstOrDefault(x => x.MatterWFComponentId == matterWFCompId && x.Code == message.Code && x.QueueItemStatusTypeId == (int)QueueItemStatusTypeEnum.Waiting);
                            if (existingMessage != null)
                            {
                                existingMessage.MatterWFComponentId = matterWFCompId;
                                existingMessage.Code = message.Code;
                                existingMessage.Message = message.Message;
                                existingMessage.MessageByUserId = GlobalVars.CurrentUser.UserId;
                                existingMessage.MessageDate = DateTime.UtcNow;
                                existingMessage.RetryCount = 0;
                                existingMessage.QueueItemStatusTypeId = (int)Enums.QueueItemStatusTypeEnum.Waiting;
                            }
                            else
                            {
                                context.MatterWFComponentBackchannelMessages.Add
                                (
                                    new MatterWFComponentBackchannelMessage()
                                    {
                                        MatterWFComponentId = matterWFCompId,
                                        Code = message.Code,
                                        Message = message.Message,
                                        MessageByUserId = GlobalVars.CurrentUser.UserId,
                                        MessageDate = DateTime.UtcNow,
                                        RetryCount = 0,
                                        QueueItemStatusTypeId = (int)Enums.QueueItemStatusTypeEnum.Waiting
                                    }
                                );
                                context.SaveChanges();
                            }
                        }
                    }
                }
            }
        }

        public string GetLoanTrakEmailEnd(int wfComponentId, int brokerId)
        {
            string ret = "";
            bool hasExisting = false;
            var existingUsers = context.Users.Where(b => b.BrokerId == brokerId);
            DateTime cutoff = DateTime.Now.AddMonths(-1).Date;
            if (existingUsers.Any(x => x.LastLoginDate >= cutoff))
            {
                hasExisting = true;
            }

            var templateDir = Slick_Domain.GlobalVars.GetGlobalTxtVar("LoantrakEmailTemplates", context);
            string templateFilename = "";
            if (hasExisting)
            {
                //ret += "You can track the status of all of your loans across multiple lenders instructed to MSA National via our new LoanTrak portal at www.loantrak.com.au <br>" +
                //    "If you have any questions or need assistance with LoanTrak, please contact us on support@msanational.com.au or 02 8719 4000";
                templateFilename = "ExistingBrokerEmail.html";
            }
            else
            {
                if (wfComponentId == (int)WFComponentEnum.CreateMatter)
                {
                    templateFilename = "NewBrokerCMEmail.html";
                }
                else
                {
                    templateFilename = "NewBrokerEmail.html";
                }
            }
            var path = Path.Combine(templateDir, templateFilename);
            if (File.Exists(path))
            {
                string toAppend = File.ReadAllText(path);
                if (!String.IsNullOrEmpty(Slick_Domain.Common.CommonMethods.ConvertToPlainText(toAppend)))
                {
                    ret += "<hr></hr><span style='font-family: Calibri;font-size:10pt;'><p><b>LoanTrak</b></p>";
                    ret += File.ReadAllText(path) + "</span>";
                }
            }

            return ret;
        }

        public void SendAutomatedEmails(int matterId, int wfComponentId, int matterWFComponentId, int milestoneTrigger, bool forceNoReply = false, bool useSmtp = false, bool onlySendNoReplyEmails = false, string appendMessage = null)
        {
            var matter = new MatterRepository(context).GetMatterDetailsCompact(matterId);
            if (matter.StopAutomatedEmails.HasValue && matter.StopAutomatedEmails.Value) return;

            var matterWFComp = new MatterWFRepository(context).GetMatterWFComponentView(matterWFComponentId);

            var emailRep = new EmailsRepository(context);
            var emailsToSend = emailRep.GetEmailsToSendForComponentId(matter, wfComponentId, milestoneTrigger);
            if (onlySendNoReplyEmails && emailsToSend != null && emailsToSend.Any())
            {
                emailsToSend = emailsToSend.Where(e => e.WFComponentId == (int)WFComponentEnum.DocsReturned || (e.SendNoReply && !e.IsPopupEmail));
            }
            

            if (emailsToSend != null && emailsToSend.Any())
            {
                foreach (var email in emailsToSend)
                {
                    if (!string.IsNullOrEmpty(appendMessage))
                    {
                        email.EmailMessage += appendMessage;
                    }
                    var emailModel = BuildEmailPlaceHolderModel(emailRep, matterId, wfComponentId, matterWFComponentId);

                    UpdateCCNotificationsForEmailModel(ref emailModel, email);
                    //EmailsService.SendAutomatedEmail(email.EmailSubject, email.EmailMessage, emailModel, email.AttachedDocNames, matterId);

                    if ((GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_SendWFComponentEmails, context).ToUpper() == DomainConstants.True.ToUpper()
                         && GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_RunMode, context) == DomainConstants.Production) ||
                         GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmails, context)?.ToUpper() == DomainConstants.True.ToUpper())
                    {
                        if (forceNoReply == true)
                        {
                            email.SendNoReply = true;
                        }

                        if (email.NotifyBroker && matter.BrokerId.HasValue)
                        {
                            email.EmailMessage += GetLoanTrakEmailEnd(wfComponentId, matter.BrokerId.Value);
                        }

                        emailModel.ConvertAttachmentsToPDF = email.ConvertAttachmentsToPDF;
                        emailModel.CCEmail = email.CustomCCEmail ?? "";
                        emailModel.BCCEmail = email.CustomBCCEmail ?? "";



                        var partyDetails = context.MatterParties.Where(mp => mp.MatterId == matterId && mp.PartyTypeId == (int)Enums.MatterPartyTypeEnum.Borrower)
                            .Select(p => new { p.Firstname, p.Lastname, p.Email, p.Mobile, p.CompanyName, p.IsIndividual }).ToList();

                        int partyCount = partyDetails.Count();

                        if (email.NotifyBorrower && partyCount > 1)
                        {

                            for (int i = 0; i < partyCount; i++)
                            {
                                var copyModel = emailModel.MakeCopy();

                                copyModel.EmailMobiles.BorrowerMobiles = null;
                                copyModel.EmailMobiles.Borrower = null;


                                var borr = partyDetails[i];

                                if (!string.IsNullOrEmpty(borr.Mobile) && !string.IsNullOrEmpty(emailModel.EmailMobiles.BorrowerMobiles))
                                {
                                    string cleanMobile = borr.Mobile.Trim();
                                    if (cleanMobile.Length == 9)
                                        if (cleanMobile.Substring(0, 1) == "4")
                                            cleanMobile = "0" + cleanMobile;
                                    if (cleanMobile.Length == 10)
                                        if (cleanMobile.Substring(0, 2) == "04")
                                            copyModel.EmailMobiles.BorrowerMobiles += cleanMobile + GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_MSASMSAddress, context) + ";";
                                }
                                if (!string.IsNullOrEmpty(borr.Email) && !string.IsNullOrEmpty(emailModel.EmailMobiles.Borrower))
                                {
                                    copyModel.EmailMobiles.Borrower = borr.Email;
                                }


                                if (email.IsPopupEmail && !forceNoReply)
                                {
                                    if (email.SendEmail)
                                    {

                                        EmailsService.SendAutomatedEmail(email.EmailSubject, email.EmailMessage, copyModel, email.AttachedDocNames, matterId, sendEmail: true, sendSms: false, existingContext: context, isPopUpEmail: true, lenderDocs: email.LenderDocs, useCustomSignature: email.ApplyCustomSignature, customSignatureDirectory: email.SignaturePath, comp: matterWFComp, partyIndex: i);

                                    }
                                    if (email.SendSMS)
                                    {
                                        EmailsService.SendAutomatedEmail(email.EmailSubject, email.SMSMessage, copyModel, email.AttachedDocNames, matterId, sendEmail: false, sendSms: true, existingContext: context, isPopUpEmail: true, lenderDocs: email.LenderDocs, comp: matterWFComp, partyIndex: i);
                                    }

                                }
                                else
                                {
                                    if (email.SendEmail)
                                    {
                                        bool sendFromNoReply = email.SendNoReply || forceNoReply;


                                        EmailsService.SendAutomatedEmail(email.EmailSubject, email.EmailMessage, copyModel, email.AttachedDocNames, matterId, sendEmail: true, sendSms: false, noReply: sendFromNoReply, existingContext: context, lenderDocs: email.LenderDocs, useCustomSignature: email.ApplyCustomSignature, customSignatureDirectory: email.SignaturePath, comp: matterWFComp, partyIndex: i);

                                    }
                                    if (email.SendSMS)
                                    {
                                        EmailsService.SendAutomatedEmail(email.EmailSubject, email.SMSMessage, copyModel, email.AttachedDocNames, matterId, sendEmail: false, sendSms: true, noReply: true, existingContext: context, lenderDocs: email.LenderDocs, comp: matterWFComp, partyIndex: i);
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (email.IsPopupEmail && !forceNoReply)
                            {
                                if (email.SendEmail)
                                {

                                    EmailsService.SendAutomatedEmail(email.EmailSubject, email.EmailMessage, emailModel, email.AttachedDocNames, matterId, sendEmail: true, sendSms: false, existingContext: context, isPopUpEmail: true, lenderDocs: email.LenderDocs, useCustomSignature: email.ApplyCustomSignature, customSignatureDirectory: email.SignaturePath, comp: matterWFComp);

                                }
                                if (email.SendSMS)
                                {
                                    EmailsService.SendAutomatedEmail(email.EmailSubject, email.SMSMessage, emailModel, email.AttachedDocNames, matterId, sendEmail: false, sendSms: true, existingContext: context, isPopUpEmail: true, lenderDocs: email.LenderDocs, comp: matterWFComp);
                                }

                            }
                            else
                            {
                                if (email.SendEmail)
                                {
                                    bool sendFromNoReply = email.SendNoReply || forceNoReply;


                                    EmailsService.SendAutomatedEmail(email.EmailSubject, email.EmailMessage, emailModel, email.AttachedDocNames, matterId, sendEmail: true, sendSms: false, noReply: sendFromNoReply, existingContext: context, lenderDocs: email.LenderDocs, useCustomSignature: email.ApplyCustomSignature, customSignatureDirectory: email.SignaturePath, comp: matterWFComp);

                                }
                                if (email.SendSMS)
                                {
                                    EmailsService.SendAutomatedEmail(email.EmailSubject, email.SMSMessage, emailModel, email.AttachedDocNames, matterId, sendEmail: false, sendSms: true, noReply: true, existingContext: context, lenderDocs: email.LenderDocs, comp: matterWFComp);
                                }
                            }
                        }
                    }



                }
            }
        }


        public EmailEntities.EmailPlaceHolderModel BuildEmailPlaceHolderModel(EmailsRepository emailRep, int matterId, int wfComponentId, int matterWFComponentId)
        {
            EmailEntities.EmailPlaceHolderModel emailModel = null;
            if (matterWFComponentId == 0)
            {
                emailModel = emailRep.BuildEmailPlaceHolderModelWithWFComponent(matterId, wfComponentId);
            }
            else
            {
                emailModel = emailRep.BuildEmailPlaceHolderModelWithMatterWFComponent(matterId, matterWFComponentId);
            }



            if (emailModel?.WFComponentId == (int)WFComponentEnum.SendProcessedDocs)
            {
                emailModel.SendProcDocsDetails = emailRep.BuildSendProcDocsEmailModel(matterId);
            }

            return emailModel;
        }

        public void PublicUpdateCCNotificationsForEmailModel(ref EmailEntities.EmailPlaceHolderModel emailModel, EmailEntities.ReminderMessage email)
        {
            UpdateCCNotificationsForEmailModel(ref emailModel, email);
        }

        public void UpdateCCNotificationsForEmailModel(ref EmailEntities.EmailPlaceHolderModel emailModel, EmailEntities.ReminderMessage email)
        {

            if (!email.NotifyBroker)
            {
                //emailModel.BrokerName = null;
                emailModel.EmailMobiles.Broker = null;
                emailModel.EmailMobiles.BrokerMobile = null;

            }
            if (!email.NotifyLender)
            {
                //emailModel.LenderName = null;
                emailModel.EmailMobiles.Lender = null;
            }
            if (!email.NotifySecondaryContacts)
            {
                emailModel.EmailMobiles.SecondaryContacts = null;
            }
            if (!email.NotifyMortMgr)
            {
                //emailModel.MortMgrName = null;
                emailModel.EmailMobiles.MortMgr = null;
            }
            if (!email.NotifyBorrower)
            {
                //emailModel.BorrowerName = null;
                emailModel.EmailMobiles.Borrower = null;
                emailModel.EmailMobiles.BorrowerMobiles = null;
            }
            if (!email.NotifyOtherParty)
            {
                //emailModel.OtherPartyName = null;
                emailModel.EmailMobiles.OtherParty = null;
            }
            if (!email.NotifyFileOwner)
            {
                emailModel.EmailMobiles.FileOwner = null;
            }
            if (!email.NotifyRelationshipManager)
            {
                //emailModel.RelationshipManagerName = null;
                emailModel.EmailMobiles.RelationshipManager = null;
            }
            if (!email.SendSMS && email.NotifyBroker && email.NotifyBorrower && !string.IsNullOrEmpty(emailModel.EmailMobiles.Borrower) && !string.IsNullOrEmpty(emailModel.EmailMobiles.Borrower))
            {
                if (string.IsNullOrEmpty(emailModel.EmailMobiles.CCEmails)) emailModel.EmailMobiles.CCEmails = "";
                emailModel.EmailMobiles.CCEmails += emailModel.EmailMobiles.Broker;
                emailModel.EmailMobiles.Broker = null;
            }

            var ccs = emailModel.EmailMobiles.CCEmails ?? string.Empty;
            bool useSemiColon = !string.IsNullOrEmpty(ccs);

            //if (email.NotifyFileOwner)
            //{
            //    if (!string.IsNullOrEmpty(ccs) && !string.IsNullOrEmpty(emailModel.EmailMobiles.FileOwner) && !ccs.Contains(emailModel.EmailMobiles.FileOwner))
            //    {
            //        notifications.AppendFormat("{0}{1}{2}", useSemiColon ? ";" : "", emailModel.EmailMobiles.FileOwner, ";");
            //        useSemiColon = false;
            //    }
            //}

            emailModel.EmailMobiles.Other = null;
            if (email.NotifyOther && !string.IsNullOrEmpty(email.OtherEmails))
            {
                emailModel.EmailMobiles.Other = email.OtherEmails;
            }

            //if (!string.IsNullOrEmpty(ccs)) notifications.AppendFormat("{0}{1}{2}", useSemiColon ? ";" : "", ccs, ";");

            //string newCC = notifications.ToString();
            //if(!string.IsNullOrEmpty(newCC) && newCC.First() == ';')
            //{
            //    newCC = newCC.Substring(1, newCC.Length-1);
            //}

            //emailModel.EmailMobiles.CCEmails = newCC.ToString();

            if (string.IsNullOrEmpty(emailModel.EmailMobiles.CCEmails)) emailModel.EmailMobiles.CCEmails = null;
        }

        public void SendPreviewEmail(string emailSubject, string emailBody)
        {
            var email = new EmailEntities.ReminderMessage
            {
                EmailMessage = emailBody,
                EmailSubject = emailSubject,
            };

            var emailAddress = GlobalVars.CurrentUser.Email;
            var user = EntityHelper.GetFullName(GlobalVars.CurrentUser.LastName, GlobalVars.CurrentUser.FirstName);
            var model = new EmailEntities.EmailPlaceHolderModel
            {
                BrokerName = "My BrokerName",
                CurrentUser = user,
                FileOwner = user,
                FileOwnerEmail = "JoeBloggs@abc.com.au",
                FileOwnerPhone = "0412345678",
                LenderRefNo = "My LenderRef",
                MatterType = "Purchase",
                MortMgrName = "My MortMgr",
                BorrowerName = "Joe Bloggs",
                LenderName = "My Lender",
                LoanAmount = 425456.ToString("c"),
                MatterId = "999",
                Milestone = "Test Milestone",
                TodaysDate = DateTime.Now.ToShortDateString(),
                Parties = "BLOGGS J",
                SecurityAddress = "1 Testing Street Perth WA 6000",
                TitleReferences = "111/222",
                SettlementDate = DateTime.Now.ToString("dd-MMM-yyyy"),
                SettlementTime = DateTime.Now.ToString("HH:mm tt"),
                SettlementVenue = "My Settlement Venue",
                ChequeCollectionBranch = "My Cheque Collection Branch",
                EmailMobiles = new EmailEntities.EmailMobileAddresses
                {
                    CurrentUser = emailAddress,
                }
            };

            model.MatterDetails = string.Format("{0} {1} {2}",
               string.IsNullOrEmpty(model.Parties) ? null : "LOAN TO " + model.Parties,
               string.IsNullOrEmpty(model.LenderRefNo) ? null : "LFID " + model.LenderRefNo,
               string.IsNullOrEmpty(model.LoanAmount) ? null : model.LoanAmount);

            EmailsService.SendAutomatedEmail(email.EmailSubject, email.EmailMessage, model);
        }

        public void ActivateComponents(int matterId)
        {
            ActivateComponents(matterId, true, false);
        }
        public void ActivateComponents(int matterId, bool notifications, bool forceNoReply, int? updatedByUserId = null)
        {
            var CompList = context.MatterWFComponents.Where(m => m.MatterId == matterId && m.WFComponentId != (int)WFComponentEnum.CreatePSTPacket && 
                                                (m.WFComponentStatusTypeId == (int)Enums.MatterWFComponentStatusTypeEnum.NotStarted ||
                                                 m.WFComponentStatusTypeId == (int)Enums.MatterWFComponentStatusTypeEnum.InProgress ||
                                                 m.WFComponentStatusTypeId == (int)Enums.MatterWFComponentStatusTypeEnum.Starting) &&
                                                 (m.DisplayStatusTypeId == (int)DisplayStatusTypeEnum.Default ||
                                                 m.DisplayStatusTypeId == (int)DisplayStatusTypeEnum.Display ||
                                                 m.DisplayStatusTypeId == (int)DisplayStatusTypeEnum.Hide)
                                                ).ToList();

            foreach (var c in CompList)
            {
                var comp = c;

                bool AllDependsComplete = true;

                var dependList = context.MatterWFComponentDependents.Where(m => m.MatterWFComponentId == comp.MatterWFComponentId);

                foreach (var dependComp in dependList)
                {
                    var compChk = context.MatterWFComponents.Where(m => m.MatterWFComponentId == dependComp.DependentMatterWFComponentId).Select(x => new { x.WFComponentStatusTypeId, x.DisplayStatusTypeId }).FirstOrDefault();
                    if (compChk.WFComponentStatusTypeId != (int)Enums.MatterWFComponentStatusTypeEnum.Deleted &&
                        compChk.WFComponentStatusTypeId != (int)Enums.MatterWFComponentStatusTypeEnum.Complete &&
                        compChk.WFComponentStatusTypeId != (int)Enums.MatterWFComponentStatusTypeEnum.Cancelled &&
                        compChk.DisplayStatusTypeId != (int)DisplayStatusTypeEnum.Inactive &&
                        compChk.DisplayStatusTypeId != (int)DisplayStatusTypeEnum.InactiveNeverShown)
                    {
                        AllDependsComplete = false;
                        break;
                    }
                }

                if (AllDependsComplete && comp.WFComponentStatusTypeId == (int)Slick_Domain.Enums.MatterWFComponentStatusTypeEnum.NotStarted)
                {
                    comp.WFComponentStatusTypeId = (int)Slick_Domain.Enums.MatterWFComponentStatusTypeEnum.Starting;
                    comp.UpdatedByUserId = GlobalVars.CurrentUserId ?? Slick_Domain.Common.DomainConstants.SystemUserId;
                    comp.UpdatedDate = DateTime.Now;

                    AllocateTaskUserToComponent(ref comp);
                    if (updatedByUserId == null)
                    {
                        AddMatterEvent(comp.MatterId, comp.MatterWFComponentId, Slick_Domain.Enums.MatterEventTypeList.MilestoneStarted, null);
                    } else
                    {
                        AddMatterEvent(comp.MatterId, comp.MatterWFComponentId, Slick_Domain.Enums.MatterEventTypeList.MilestoneStarted, null, updatedByUserId: updatedByUserId);
                    }
                    

                    //if (comp.WFComponentId == (int)WFComponentEnum.QASettlement && comp.Matter.LenderId == 1 && comp.Matter.MatterGroupTypeId == (int)MatterGroupTypeEnum.NewLoan)
                    //{
                    //    var noteRep = new NotesRepository(context);
                    //    noteRep.SaveSimpleNote(comp.MatterId, (int)MatterNoteTypeEnum.StatusUpdate, "File in Settlement QA", "This file has progressed to QA Settlement", true, false, false);
                    //}

                    if (notifications)
                    {
                        SendAutomatedEmails(comp.MatterId, comp.WFComponentId, comp.MatterWFComponentId, (int)Enums.MilestoneEmailTriggerTypeEnum.Started, forceNoReply: forceNoReply, useSmtp: true);
                        var mtDetails = context.Matters.Where(m => m.MatterId == comp.MatterId).Select(x => new { x.LenderId, x.MortMgrId, x.IsTestFile, x.MatterGroupTypeId }).FirstOrDefault();
                        if (!mtDetails.IsTestFile)
                        {

                            CheckAndSendBackChannelMessages(comp.MatterWFComponentId, comp.WFComponentId, mtDetails.LenderId, mtDetails.MortMgrId, BackChannelMessageTriggerTypeEnum.Started, mtDetails.MatterGroupTypeId);
                        }
                    }

                }
                else if (!AllDependsComplete && comp.WFComponentStatusTypeId != (int)Slick_Domain.Enums.MatterWFComponentStatusTypeEnum.NotStarted)
                {
                    comp.WFComponentStatusTypeId = (int)Slick_Domain.Enums.MatterWFComponentStatusTypeEnum.NotStarted;
                    comp.UpdatedByUserId = GlobalVars.CurrentUserId ?? Slick_Domain.Common.DomainConstants.SystemUserId;
                    comp.UpdatedDate = DateTime.Now;
                    comp.TaskAllocTypeId = comp.DefTaskAllocTypeId;
                    AddMatterEvent(comp.MatterId, comp.MatterWFComponentId, Slick_Domain.Enums.MatterEventTypeList.MilestoneUndone,
                        $"Milestone status reset to Not Started due to a dependency change {DateTime.Now.ToShortDateString()} {DateTime.Now.ToShortTimeString()}");
                }
            }
        }

        private void AllocateTaskUserToComponent(ref MatterWFComponent comp)
        {
            var mwf = comp;
            int? taskAllocTypeId = comp.DefTaskAllocTypeId;
            if (comp.TaskAllocUserId != null)
            {
                comp.TaskAllocTypeId = (int)TaskAllocationTypeEnum.User;
            } //has already been manually allocated
            else if (comp.TaskAllocTypeId == (int)TaskAllocationTypeEnum.Manila)
            {
                taskAllocTypeId = (int)TaskAllocationTypeEnum.Manila;
            }
            else if (comp.TaskAllocTypeId == (int)TaskAllocationTypeEnum.Shared)
            {
                taskAllocTypeId = (int)TaskAllocationTypeEnum.Shared;
            }
            else
            {
                comp.TaskAllocTypeId = taskAllocTypeId;
                switch (taskAllocTypeId)
                {
                    case (int)TaskAllocationTypeEnum.FileManager:
                        comp.TaskAllocUserId = context.Matters.FirstOrDefault(x => x.MatterId == mwf.MatterId)?.FileOwnerUserId;
                        break;

                    case (int)TaskAllocationTypeEnum.Milestone:
                        var component = GetLastCompletedMatterWFComponent(mwf);

                        if (component != null)
                        {
                            comp.TaskAllocUserId = context.MatterEvents.FirstOrDefault(x => x.MatterWFComponentId == component.MatterWFComponentId
                                    && x.MatterEventStatusTypeId == (int)MatterEventStatusTypeEnum.Good
                                    && x.MatterEventTypeId == (int)MatterEventTypeList.MilestoneComplete)?.EventByUserId;
                        }
                        break;
                }

                if (comp.TaskAllocTypeId != (int)TaskAllocationTypeEnum.Shared && comp.TaskAllocTypeId != (int)TaskAllocationTypeEnum.Manila && !comp.TaskAllocUserId.HasValue)
                {
                    comp.TaskAllocTypeId = (int)TaskAllocationTypeEnum.Unallocated;
                }
            }
            CheckForTaskOverrideUser(ref comp);

            var components = context.MatterWFComponents.Where
                (x => x.DefTaskAllocTypeId == (int)TaskAllocationTypeEnum.Milestone && x.DefTaskAllocWFComponentId == mwf.WFComponentId && x.MatterId == mwf.MatterId
                && x.WFComponentStatusTypeId != (int)Enums.MatterWFComponentStatusTypeEnum.Complete && x.WFComponentStatusTypeId != (int)Enums.MatterWFComponentStatusTypeEnum.Deleted
                && x.WFComponentStatusTypeId != (int)Enums.MatterWFComponentStatusTypeEnum.Cancelled);
            foreach (var c in components)
            {
                c.TaskAllocTypeId = comp.TaskAllocTypeId;
                c.TaskAllocUserId = comp.TaskAllocUserId;
            }

            context.SaveChanges();
        }

        private void CheckForTaskOverrideUser(ref MatterWFComponent comp)
        {
            if (!context.TaskAllocAssignments.Any()) return;

            var matter = new MatterRepository(context).GetMatterDetailsCompact(comp.MatterId);
            if (matter == null) return;

            //var overrideUser = CheckForTaskOverideUser(matter, comp.TaskAllocUserId);
            int? overrideUser = null;
            if (comp.TaskAllocUserId.HasValue)
            {
                overrideUser = CheckForTaskOverideUser(matter, comp.TaskAllocUserId);
            }
            else
            {
                overrideUser = CheckForTaskOverideUser(matter, null);
            }

            if (overrideUser.HasValue)
            {
                comp.TaskAllocUserId = overrideUser;
                comp.TaskAllocTypeId = (int)TaskAllocationTypeEnum.User;
            }
        }

        /// <summary>
        /// Checks current User Allocation and Overrides when necessary.
        /// 1st Override - with Reassigned User only.
        /// 2nd Override - no User - just assign all tasks to new user.
        /// </summary>
        /// <param name="matter"></param>
        /// <param name="currentUserId"></param>
        /// <returns></returns>
        private int? CheckForTaskOverideUser(MCE.MatterViewCompact matter, int? currentUserId)
        {
            var matchedItems = new List<GeneralCustomEntities.BestMatchForCriteria>();
            var items = context.TaskAllocAssignments.Where
                (x => (x.DateFrom == null || x.DateFrom < DateTime.Now) && (x.DateTo == null || x.DateTo > DateTime.Now) && x.OriginalUserId == currentUserId);

            foreach (var item in items)
            {
                matchedItems.Add(new GeneralCustomEntities.BestMatchForCriteria
                {
                    ID = item.TaskAllocAssignmentId,
                    LenderId = item.LenderId,
                    MatterTypeId = item.MatterTypeId,
                    MortMgrId = item.MortMgrId,
                    SettlementTypeId = item.SettlementTypeId,
                    StateId = item.StateId
                });
            }

            if (!matchedItems.Any()) return null;

            var bestMatchedId = CommonMethods.GetBestValueFromSelectedQuery(matter.MatterGroupTypeId,
                matter.IsPEXA ? (int)SettlementTypeEnum.PEXA : (int)SettlementTypeEnum.Paper, matter.StateId, matter.LenderId, matter.MortMgrId, matchedItems);

            if (bestMatchedId.HasValue)
            {
                return items.FirstOrDefault(x => x.TaskAllocAssignmentId == bestMatchedId.Value)?.UserId;
            }

            return null;
        }

        private MatterWFComponent GetLastCompletedMatterWFComponent(MatterWFComponent mwf)
        {
            return context.MatterWFComponents.Where
                (x => x.MatterId == mwf.MatterId &&
                    x.WFComponentId == mwf.DefTaskAllocWFComponentId &&
                    x.DisplayOrder < mwf.DisplayOrder &&
                    x.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.Complete).OrderByDescending(o => o.DisplayOrder)
                    .FirstOrDefault();
        }

        public void ResetMilestoneToInProgress(int matterId)
        {
            var matterWFComp = context.MatterWFComponents.OrderByDescending(o => o.DisplayOrder)
                .Where(x => x.MatterId == matterId && x.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.Complete)
                .FirstOrDefault();

            if (matterWFComp != null)
            {
                matterWFComp.WFComponentStatusTypeId = (int)MatterWFComponentStatusTypeEnum.InProgress;
            }
            context.SaveChanges();
        }

        public bool CanUndoMilestone(MatterCustomEntities.MatterWFComponentView matterWFCompView, ref string message)
        {

            return true;
        }

        /// <summary>
        /// Gets all documents for Matter except current matterwfcomponent - for Deletion
        /// </summary>
        /// <param name="matterId"></param>
        /// <param name="matterWFComponentId"></param>
        /// <param name="componentId"></param>
        /// <returns></returns>
        public IEnumerable<MatterDocument> GetExistingDocumentsForMatter(int matterId, int matterWFComponentId, int componentId)
        {
            return (from md in context.MatterDocuments
                    join dm in context.DocumentMasters on md.DocumentMasterId equals dm.DocumentMasterId
                    join dv in context.Documents on dm.DocumentMasterId equals dv.DocumentMasterId
                    join wd in context.MatterWFDocuments on dv.DocumentId equals wd.DocumentId
                    where md.MatterId == matterId && wd.MatterWFComponentId != matterWFComponentId && md.WFComponentId == componentId
                    select md);
        }

        private void UndoCompleteMatterWFComponent(MatterWFComponent unCompleteComp, string DemoteReasonTxt)
        {
            var compDependents = context.MatterWFComponentDependents.Where(m => m.DependentMatterWFComponentId == unCompleteComp.MatterWFComponentId && (m.MatterWFComponent.WFComponentStatusType.IsActiveStatus || m.MatterWFComponent.WFComponentStatusTypeId == (int)Enums.MatterWFComponentStatusTypeEnum.Complete))
        .Select(m => m.MatterWFComponent).ToList();
            foreach (var comp in compDependents)
            {
                if (comp.WFComponentStatusTypeId == (int)Enums.MatterWFComponentStatusTypeEnum.Complete)
                    UndoCompleteMatterWFComponent(comp, DemoteReasonTxt);

                //1 - Mark the events for this component to be "demoted" as "not SLA include"
                var matterEvents = context.MatterEvents.Where(m => m.MatterWFComponentId == comp.MatterWFComponentId && m.MatterEventStatusTypeId == (int)Enums.MatterEventStatusTypeEnum.Good);
                foreach (var me in matterEvents)
                    me.MatterEventStatusTypeId = (int)Enums.MatterEventStatusTypeEnum.Undone;

                comp.WFComponentStatusTypeId = (int)Enums.MatterWFComponentStatusTypeEnum.NotStarted;
                comp.UpdatedDate = DateTime.Now;
                comp.UpdatedByUserId = GlobalVars.CurrentUserId ?? Slick_Domain.Common.DomainConstants.SystemUserId;

                //Event for the undo
                AddMatterEvent(comp.MatterId, comp.MatterWFComponentId, Enums.MatterEventTypeList.MilestoneUndone, DemoteReasonTxt);

            }

        }

        public MatterCustomEntities.MatterWFComponentView DemoteToPreviousMileStone(MatterCustomEntities.MatterWFComponentView currMWFCompView, string DemoteReasonTxt)
        {
            MatterCustomEntities.MatterWFComponentView retVal = currMWFCompView;

            //1 - Mark the events for this component to be "demoted" as "not SLA include"
            var matterEvents = context.MatterEvents.Where(m => m.MatterWFComponentId == currMWFCompView.MatterWFComponentId && m.MatterEventTypeId != (int)Enums.MatterEventTypeList.MilestoneUndone && m.MatterEventStatusTypeId == (int)Enums.MatterEventStatusTypeEnum.Good);
            foreach (var me in matterEvents)
                me.MatterEventStatusTypeId = (int)Enums.MatterEventStatusTypeEnum.Undone;


            var componentsForMatter = CurrentMatterWFComponentsForDisplay().Where(x => x.MatterId == currMWFCompView.MatterId);
            MatterWFComponent mwfCurr = componentsForMatter.Where(m => m.MatterWFComponentId == currMWFCompView.MatterWFComponentId).FirstOrDefault();
            mwfCurr.WFComponentStatusTypeId = (int)Slick_Domain.Enums.MatterWFComponentStatusTypeEnum.NotStarted;
            mwfCurr.TaskAllocUserId = null;
            mwfCurr.TaskAllocTypeId = null;
            mwfCurr.UpdatedByUserId = GlobalVars.CurrentUserId ?? Slick_Domain.Common.DomainConstants.SystemUserId;
            mwfCurr.UpdatedDate = DateTime.Now;

            //Event for the undo
            AddMatterEvent(mwfCurr.MatterId, mwfCurr.MatterWFComponentId, Enums.MatterEventTypeList.MilestoneUndone, DemoteReasonTxt);

            var lastComponent = componentsForMatter.OrderByDescending(x => x.DisplayOrder).FirstOrDefault(x => x.DisplayOrder < currMWFCompView.DisplayOrder);
            if (lastComponent != null)
            {
                var mevLastComplete = FindLastCompletedEventComponent(lastComponent.MatterWFComponentId);
                if (mevLastComplete != null)
                {
                    mevLastComplete.MatterEventStatusTypeId = (int)Enums.MatterEventStatusTypeEnum.Undone;
                }

                UndoCompleteMatterWFComponent(lastComponent, DemoteReasonTxt);

                lastComponent.WFComponentStatusTypeId = (int)Slick_Domain.Enums.MatterWFComponentStatusTypeEnum.InProgress;
                AllocateTaskUserToComponent(ref lastComponent);
                lastComponent.UpdatedByUserId = GlobalVars.CurrentUserId ?? Slick_Domain.Common.DomainConstants.SystemUserId;
                lastComponent.UpdatedDate = DateTime.Now;

                context.SaveChanges();

                retVal = GetMatterWFComponentView(lastComponent.MatterWFComponentId);
            }
            else
                retVal = GetMatterWFComponentView(mwfCurr.MatterWFComponentId);

            return retVal;
        }

        private MatterEvent FindLastCompletedEventComponent(int matterWFComponentId)
        {
            return
                context.MatterEvents.Where(m => m.MatterWFComponent.MatterWFComponentId == matterWFComponentId &&
                m.MatterEventTypeId == (int)MatterEventTypeList.MilestoneComplete)
                .OrderByDescending(m => m.MatterEventId).FirstOrDefault();
        }

        public void MarkComponentAsNotStarted(int matterWFComponentId, string eventNotes = null)
        {
            MatterWFComponent mwfCurr = context.MatterWFComponents.Where(m => m.MatterWFComponentId == matterWFComponentId).FirstOrDefault();

            if (mwfCurr.WFComponentStatusTypeId == (int)Slick_Domain.Enums.MatterWFComponentStatusTypeEnum.Starting ||
                mwfCurr.WFComponentStatusTypeId == (int)Slick_Domain.Enums.MatterWFComponentStatusTypeEnum.InProgress ||
                mwfCurr.WFComponentStatusTypeId == (int)Slick_Domain.Enums.MatterWFComponentStatusTypeEnum.Complete)
            {
                //1 - Mark the events for this component to be "demoted" as "not SLA include"
                var matterEvents = context.MatterEvents.Where(m => m.MatterWFComponentId == mwfCurr.MatterWFComponentId && m.MatterEventTypeId != (int)Enums.MatterEventTypeList.MilestoneUndone && m.MatterEventStatusTypeId == (int)Enums.MatterEventStatusTypeEnum.Good);
                foreach (var me in matterEvents)
                {
                    me.MatterEventStatusTypeId = (int)Enums.MatterEventStatusTypeEnum.Undone;
                    me.EventNotes +=  eventNotes ?? "Event Undone from a Mark as Not Started command. ";
                }

                AddMatterEvent(mwfCurr.MatterId, mwfCurr.MatterWFComponentId, MatterEventTypeList.MilestoneUndone, eventNotes ?? "Status manually updated from a Mark as Not Started command.");

                mwfCurr.WFComponentStatusTypeId = (int)Slick_Domain.Enums.MatterWFComponentStatusTypeEnum.NotStarted;
                mwfCurr.UpdatedByUserId = GlobalVars.CurrentUserId ?? Slick_Domain.Common.DomainConstants.SystemUserId;
                mwfCurr.UpdatedDate = DateTime.Now;
                context.SaveChanges();
            }

        }

        public void MarkComponentAsInProgress(int matterWFComponentId, string eventNotes= null)
        {
            MatterWFComponent mwfCurr = context.MatterWFComponents.Where(m => m.MatterWFComponentId == matterWFComponentId).FirstOrDefault();

            if (mwfCurr.WFComponentStatusTypeId == (int)Slick_Domain.Enums.MatterWFComponentStatusTypeEnum.NotStarted ||
                mwfCurr.WFComponentStatusTypeId == (int)Slick_Domain.Enums.MatterWFComponentStatusTypeEnum.Complete)
            {
                AddMatterEvent(mwfCurr.MatterId, mwfCurr.MatterWFComponentId, MatterEventTypeList.MilestoneStarted, eventNotes ?? "Start Status manually updated");
                if (mwfCurr.WFComponentId == (int)WFComponentEnum.CloseMatter && mwfCurr.Matter.MatterStatusTypeId == (int)MatterStatusTypeEnum.Closed)
                {
                    mwfCurr.Matter.MatterStatusTypeId = (int)MatterStatusTypeEnum.Settled;
                }
                mwfCurr.WFComponentStatusTypeId = (int)Slick_Domain.Enums.MatterWFComponentStatusTypeEnum.InProgress;
                mwfCurr.UpdatedByUserId = GlobalVars.CurrentUserId ?? Slick_Domain.Common.DomainConstants.SystemUserId;
                mwfCurr.UpdatedDate = DateTime.Now;
                AllocateTaskUserToComponent(ref mwfCurr);
                context.SaveChanges();

            }

        }

        public void MarkComponentAsComplete(int matterWFComponentId)
        {
            MatterWFComponent mwfCurr = context.MatterWFComponents.Where(m => m.MatterWFComponentId == matterWFComponentId).FirstOrDefault();

            if (mwfCurr.WFComponentStatusTypeId == (int)Slick_Domain.Enums.MatterWFComponentStatusTypeEnum.NotStarted ||
                mwfCurr.WFComponentStatusTypeId == (int)Slick_Domain.Enums.MatterWFComponentStatusTypeEnum.Starting ||
                mwfCurr.WFComponentStatusTypeId == (int)Slick_Domain.Enums.MatterWFComponentStatusTypeEnum.InProgress)
            {
                if (mwfCurr.WFComponentStatusTypeId == (int)Slick_Domain.Enums.MatterWFComponentStatusTypeEnum.NotStarted)
                {
                    //Need to add starting and ending event
                    AddMatterEvent(mwfCurr.MatterId, mwfCurr.MatterWFComponentId, MatterEventTypeList.MilestoneStarted,
                        $"Status manually updated on {DateTime.Now.ToShortDateString()} {DateTime.Now.ToShortTimeString()} ");
                }

                AddMatterEvent(mwfCurr.MatterId, mwfCurr.MatterWFComponentId, MatterEventTypeList.MilestoneComplete,
                    $"Status manually updated on {DateTime.Now.ToShortDateString()} {DateTime.Now.ToShortTimeString()} ");

                mwfCurr.WFComponentStatusTypeId = (int)Slick_Domain.Enums.MatterWFComponentStatusTypeEnum.Complete;
                mwfCurr.UpdatedByUserId = GlobalVars.CurrentUserId ?? Slick_Domain.Common.DomainConstants.SystemUserId;
                mwfCurr.UpdatedDate = DateTime.Now;
                context.SaveChanges();
            }

        }

        //For Admin Emergency fixes only!
        public void RemoveComponent(int matterWFComponentId)
        {
            var mwfComp = context.MatterWFComponents.FirstOrDefault(c => c.MatterWFComponentId == matterWFComponentId);
            if (mwfComp != null)
            {
                var deps = context.MatterWFComponentDependents.Where(m => m.MatterWFComponentId == matterWFComponentId);
                context.MatterWFComponentDependents.RemoveRange(deps);
                deps = context.MatterWFComponentDependents.Where(m => m.DependentMatterWFComponentId == matterWFComponentId);
                context.MatterWFComponentDependents.RemoveRange(deps);
                var evts = context.MatterEvents.Where(m => m.MatterWFComponentId == matterWFComponentId);
                context.MatterEvents.RemoveRange(evts);



                //delete backchannel logs if any
                var bcToDelete = context.BackChannelLogs.Where(x => x.MatterWFComponentId == mwfComp.MatterWFComponentId);
                context.BackChannelLogs.RemoveRange(bcToDelete);

                context.MatterWFComponents.Remove(mwfComp);



                var mwfComps = context.MatterWFComponents.Where(x => x.MatterId == mwfComp.MatterId && x.DisplayOrder > mwfComp.DisplayOrder).OrderBy(x => x.DisplayOrder);

                int displayOrder = mwfComp.DisplayOrder;
                foreach (var comp in mwfComps)
                {
                    comp.DisplayOrder = displayOrder;
                    displayOrder++;
                }

                context.SaveChanges();
            }

        }

        public void HideComponent(int matterWFComponentId)
        {
            MatterWFComponent mwfCurr = context.MatterWFComponents.Where(m => m.MatterWFComponentId == matterWFComponentId).FirstOrDefault();

            mwfCurr.DisplayStatusTypeId = (int)DisplayStatusTypeEnum.Hide;
            mwfCurr.UpdatedByUserId = GlobalVars.CurrentUserId ?? Slick_Domain.Common.DomainConstants.SystemUserId;
            mwfCurr.UpdatedDate = DateTime.Now;

            context.SaveChanges();
        }

        public void ShowComponent(int matterWFComponentId)
        {
            MatterWFComponent mwfCurr = context.MatterWFComponents.Where(m => m.MatterWFComponentId == matterWFComponentId).FirstOrDefault();

            mwfCurr.DisplayStatusTypeId = (int)DisplayStatusTypeEnum.Default;
            mwfCurr.UpdatedByUserId = GlobalVars.CurrentUserId ?? Slick_Domain.Common.DomainConstants.SystemUserId;
            mwfCurr.UpdatedDate = DateTime.Now;

            context.SaveChanges();
        }

        public void ShowAlwaysComponent(int matterWFComponentId)
        {
            MatterWFComponent mwfCurr = context.MatterWFComponents.Where(m => m.MatterWFComponentId == matterWFComponentId).FirstOrDefault();

            mwfCurr.DisplayStatusTypeId = (int)DisplayStatusTypeEnum.Display;
            mwfCurr.UpdatedByUserId = GlobalVars.CurrentUserId ?? Slick_Domain.Common.DomainConstants.SystemUserId;
            mwfCurr.UpdatedDate = DateTime.Now;

            context.SaveChanges();
        }

        //public MatterWFCreateMatter AddMatterWFCreateMatter(MatterWFComponent mwfComp)
        //{
        //    MatterWFCreateMatter mh = new MatterWFCreateMatter();
        //    Matter m = context.Matters.Find(mwfComp.MatterId);
        //    mh.MatterWFComponentId = mwfComp.MatterWFComponentId;
        //    mh.MatterDescription = m.MatterDescription;
        //    mh.MatterTypeGroupId = m.MatterTypeGroupId;
        //    mh.StateId = m.StateId;
        //    mh.FileOwnerUserId = m.FileOwnerUserId;

        //    mh.LenderId = m.LenderId;
        //    mh.MortMgrId = m.MortMgrId;
        //    mh.BrokerId = m.BrokerId;
        //    mh.CostAgreementDate = m.CostAgreementDate;

        //    mh.UpdatedDate = DateTime.Now;
        //    mh.UpdatedByUserId = Slick_Domain.Common.DomainConstants.SystemUserId;

        //    context.MatterWFCreateMatters.Add(mh);
        //    context.SaveChanges();
        //    return mh;
        //}

        public MatterCustomEntities.MatterWFComponentView CreateTempComponent(MatterCustomEntities.MatterWFComponentView mwfCompView, int tmpWFcompId)
        {
            WFComponent wfComp = context.WFComponents.Where(w => w.WFComponentId == tmpWFcompId).FirstOrDefault();

            return new MatterCustomEntities.MatterWFComponentView(mwfCompView.MatterWFComponentId, 0, true, false, mwfCompView.MatterId, mwfCompView.FileOwnerId, mwfCompView.FileOwnerName, mwfCompView.MatterTypeGroupId, mwfCompView.MatterStatusId, mwfCompView.StateId, mwfCompView.LenderId, mwfCompView.LenderName, mwfCompView.MortMgrId, wfComp.WFComponentId, wfComp.WFComponentName,
                            wfComp.WFModuleId, 0, 2, "In Progress", true, null, null, null, null, null, null, mwfCompView.DisplayOrder, (int)DisplayStatusTypeEnum.Default, "Default", "Default", DateTime.Now, GlobalVars.CurrentUser.UserId, GlobalVars.CurrentUser.Username, GlobalVars.CurrentUser.LastName, GlobalVars.CurrentUser.FirstName, 0, null, null, false, false, wfComp.AccountsStageId, false, null);
        }
        //public MatterCustomEntities.MatterWFComponentView CreateTempComponent(int matterId, int matterWFComponentId, int wfCompId)
        //{
        //    Matter mat = context.Matters.Where(m => m.MatterId== matterId).FirstOrDefault();
        //    WFComponent wfComp = context.WFComponents.Where(w => w.WFComponentId == wfCompId).FirstOrDefault();

        //    return new MatterCustomEntities.MatterWFComponentView(matterWFComponentId, 0, true, false, matterId, mat.MatterTypeGroupId, mat.MatterStatusTypeId, mat.StateId, mat.LenderId, mat.MortMgrId, wfComp.WFComponentId, wfComp.WFComponentName,
        //                    wfComp.WFModuleId, 0, 2, "In Progress", true, -1, DateTime.Now, GlobalVars.CurrentUser.UserId, GlobalVars.CurrentUser.Username, GlobalVars.CurrentUser.LastName, GlobalVars.CurrentUser.FirstName);
        //}
        public IEnumerable<MatterCustomEntities.MatterWFComponentParamView> CreateTempComponentParams(int wfCompId)
        {
            return
                 context.WFComponentParams.Where(c => c.WFComponentId == wfCompId)
                .Select(c => new
                {
                    c.WFParamCtlNum,
                    c.WFParameter.WFParameterName,
                    c.WFParameter.WFParameterTypeId,
                    c.WFParameter.WFParameterType.WFParameterTypeName,
                    c.ParamValue
                }).ToList()
                .Select(m => new MatterCustomEntities.MatterWFComponentParamView(-1, wfCompId, m.WFParamCtlNum, m.WFParameterName, m.WFParameterTypeId, m.WFParameterTypeName, m.ParamValue))
                .ToList();

        }

        public MatterWFComponent AddMatterWFCancelComponent(int matterId)
        {
            //Remove unstarted components
            //RemoveUnStartedComponents(matterId, true);

            // Find Last Active component
            MatterWFComponent mwCompLast = context.MatterWFComponents.Where(m => m.MatterId == matterId && m.WFComponentStatusType.IsActiveStatus).OrderByDescending(m => m.DisplayOrder).FirstOrDefault();

            //Cancel any Actiuve components
            //IEnumerable<MatterWFComponent> wfComps = context.MatterWFComponents.Where(m => m.MatterId == matterId && m.MatterWFComponentStatusTypeEnum.IsActiveStatus == true);
            //foreach (MatterWFComponent wfComp in wfComps)
            //{
            //    wfComp.WFComponentStatusTypeId = -1;
            //}

            // Increment display order of components past this one
            IEnumerable<MatterWFComponent> wfComps = context.MatterWFComponents.Where(m => m.MatterId == matterId && m.DisplayOrder > mwCompLast.DisplayOrder);
            foreach (MatterWFComponent wfComp in wfComps)
            {
                wfComp.DisplayOrder += 1;
            }


            //Create the Cancel Component
            MatterWFComponent mwCompCancel = new MatterWFComponent();
            mwCompCancel.MatterId = matterId;
            mwCompCancel.WFComponentId = Slick_Domain.Common.DomainConstants.SystemUserId; //Change details component
            mwCompCancel.CurrProcedureNo = 0;
            mwCompCancel.WFComponentStatusTypeId = -1;    //In Progress
            mwCompCancel.DisplayOrder = mwCompLast.DisplayOrder + 1;
            mwCompCancel.UpdatedDate = DateTime.Now;
            mwCompCancel.UpdatedByUserId = Slick_Domain.GlobalVars.CurrentUser.UserId;
            mwCompCancel.DefTaskAllocTypeId = (int)TaskAllocationTypeEnum.Unallocated;
            context.MatterWFComponents.Add(mwCompCancel);

            context.SaveChanges();

            return mwCompCancel;
        }
        public MatterWFComponent AddMatterWFRestoreComponent(int matterId)
        {
            // Find Last Cancel component
            MatterWFComponent mwCompLast = context.MatterWFComponents.Where(m => m.MatterId == matterId && m.WFComponentStatusTypeId == -1).OrderByDescending(m => m.DisplayOrder).FirstOrDefault();

            // Increment display order of components past this one
            IEnumerable<MatterWFComponent> wfComps = context.MatterWFComponents.Where(m => m.MatterId == matterId && m.DisplayOrder > mwCompLast.DisplayOrder);
            foreach (MatterWFComponent wfComp in wfComps)
            {
                wfComp.DisplayOrder += 1;
            }

            //Create the Restore Component
            MatterWFComponent mwCompRestore = new MatterWFComponent();
            mwCompRestore.MatterId = matterId;
            mwCompRestore.WFComponentId = 5; //Change details component
            mwCompRestore.CurrProcedureNo = 0;
            mwCompRestore.WFComponentStatusTypeId = 4;
            mwCompRestore.DisplayOrder = mwCompLast.DisplayOrder + 1;
            mwCompRestore.UpdatedDate = DateTime.Now;
            mwCompRestore.UpdatedByUserId = Slick_Domain.GlobalVars.CurrentUser.UserId;
            mwCompRestore.DefTaskAllocTypeId = (int)TaskAllocationTypeEnum.Unallocated;
            context.MatterWFComponents.Add(mwCompRestore);

            context.SaveChanges();

            return mwCompRestore;
        }
        //public void RevisePexaWorkflow(int matterId)
        //{
        //    var matter = context.Matters.FirstOrDefault(x => x.MatterId == matterId);
        //    if (matter == null) return;

        //    bool isPexaEnabled = matter.PexaEnabled;

        //    var components =
        //      (from mwc in context.MatterWFComponents
        //       join wfc in context.WFComponents on mwc.WFComponentId equals wfc.WFComponentId
        //       join wcp in context.WFComponentParams on wfc.WFComponentId equals wcp.WFComponentId
        //       join wfmp in context.WFModuleParams on wcp.WFModuleParamId equals wfmp.WFModuleParamId
        //       where mwc.MatterId == matterId && wfmp.ParamName == "IsPexa" && wcp.ParamValue == "True"
        //       && isPexaEnabled != mwc.IsVisible
        //       select mwc).ToList();

        //    foreach (var component in components)
        //    {
        //        component.IsVisible = isPexaEnabled;
        //        component.UpdatedDate = DateTime.Now;
        //        component.UpdatedByUserId = Slick_Domain.Common.DomainConstants.SystemUserId;
        //        if (component.WFComponentStatusTypeId != (int)Enums.MatterWFComponentStatusTypeEnum.Complete)
        //        {
        //            component.WFComponentStatusTypeId = isPexaEnabled ? (int)Enums.MatterWFComponentStatusTypeEnum.InProgress : (int)Enums.MatterWFComponentStatusTypeEnum.NotStarted;
        //        }
        //    }
        //    context.SaveChanges();
        //}



        /// <summary>
        /// Used by Temporary Milestones - like Docs Sent to Agent
        /// </summary>
        /// <param name="matterWFComponentId"></param>
        /// <param name="componentName"></param>
        private void UpdateComponentEventsAfterMilestoneRemoval(int matterId, int matterWFComponentId, string componentName)
        {


            var undoEventId = AddMatterEvent(matterId, matterWFComponentId, MatterEventTypeList.MilestoneUndone,
                $"Remove Milestone {(componentName ?? "")} on {DateTime.Now.ToShortDateString()} by {GlobalVars.CurrentUser.FullName}");

            var events = RetrieveStartAndCompletedEvents(context, matterWFComponentId);

            if (events != null && events.Any())
            {
                foreach (var ev in events)
                {
                    ev.MatterEventStatusTypeId = (int)MatterEventStatusTypeEnum.Undone;
                    if (!string.IsNullOrEmpty(ev.EventNotes)) ev.EventNotes += Environment.NewLine;
                    ev.EventNotes += FormatEventNotes(MatterEventTypeList.MilestoneUndone, componentName);
                }
            }
            context.SaveChanges();
        }

        private string FormatEventNotes(MatterEventTypeList eventType, string componentName)
        {
            return string.Format("{0} Milestone {1} on {2} by {3}",
                eventType == MatterEventTypeList.MilestoneUndone ? "Undo" : "Restart",
                componentName ?? "",
                DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString(),
                GlobalVars.CurrentUser.FullName);
        }

        private IEnumerable<MatterEvent> RetrieveStartAndCompletedEvents(SlickContext context, int matterWFComponentId)
        {
            return context.MatterEvents.Where
                   (x => x.MatterWFComponentId == matterWFComponentId &&
                       x.MatterEventStatusTypeId == (int)MatterEventStatusTypeEnum.Good &&
                       (x.MatterEventTypeId == (int)MatterEventTypeList.MilestoneStarted || x.MatterEventTypeId == (int)MatterEventTypeList.MilestoneComplete)
           );
        }

        public int AddMatterEventForRestart(int matterId, int matterWFComponentId, string componentName, string restartedFromNotes)
        {
            return AddMatterEvent(matterId, matterWFComponentId, MatterEventTypeList.MilestoneRestarted,
                $"Restart Milestone {(componentName ?? "")} on {DateTime.Now.ToShortDateString()} {DateTime.Now.ToShortTimeString()} by {GlobalVars.CurrentUser.FullName}",
                eventCreatedByNotes: restartedFromNotes);
        }

        public void UpdateComponentEventsAfterRestart(int matterId, int matterWFComponentId, string componentName, string restartedFromNotes)
        {

            var restartEventId = AddMatterEventForRestart(matterId, matterWFComponentId, componentName, restartedFromNotes);
            context.SaveChanges();
            var events = RetrieveStartAndCompletedEvents(context, matterWFComponentId);
            if (events != null && events.Any())
            {
                foreach (var ev in events)
                {
                    if (ev.MatterEventTypeId == (int)MatterEventTypeList.MilestoneStarted)
                    {
                        ev.MatterEventStatusTypeId = (int)MatterEventStatusTypeEnum.Cancelled;
                        if (!string.IsNullOrEmpty(ev.EventNotes)) ev.EventNotes += Environment.NewLine;
                        ev.EventNotes += FormatEventNotes(MatterEventTypeList.MilestoneRestarted, componentName);

                    }
                }
            }

            context.SaveChanges();
        }

        public void UpdateComponentEventsAfterUndoMilestone(int matterId, int matterWFComponentId, string componentName)
        {

            UpdateEventsToUndoneStatus(matterWFComponentId, MatterEventTypeList.MilestoneStarted, componentName);

            var components = CurrentMatterWFComponentsForDisplay().Where(x => x.MatterId == matterId).ToList();
            var currComponentDisplayOrder = components.First(x => x.MatterWFComponentId == matterWFComponentId).DisplayOrder;
            var prevComponent = components.OrderByDescending(x => x.DisplayOrder).FirstOrDefault(x => x.DisplayOrder < currComponentDisplayOrder);

            if (prevComponent != null)
            {
                prevComponent.TaskAllocUserId = null;
                prevComponent.TaskAllocTypeId = null;
                UpdateEventsToUndoneStatus(prevComponent.MatterWFComponentId, MatterEventTypeList.MilestoneComplete, componentName);
            }
        }

        private void UpdateEventsToUndoneStatus(int matterWFComponentId, MatterEventTypeList eventType, string componentName)
        {
            var events = RetrieveStartAndCompletedEvents(context, matterWFComponentId);

            if (events != null && events.Any())
            {
                foreach (var ev in events)
                {
                    if (ev.MatterEventTypeId == (int)eventType)
                    {
                        ev.MatterEventStatusTypeId = (int)MatterEventStatusTypeEnum.Undone;
                        if (!string.IsNullOrEmpty(ev.EventNotes)) ev.EventNotes += Environment.NewLine;
                        ev.EventNotes += FormatEventNotes(MatterEventTypeList.MilestoneUndone, componentName);
                    }
                }
            }
            context.SaveChanges();
        }

        public MatterWFComponent GetNextMatterWFComponent(MatterCustomEntities.MatterWFComponentView matterWFCompView)
        {
            return CurrentMatterWFComponentsForDisplay().Where
                (m => m.MatterId == matterWFCompView.MatterId && m.DisplayOrder > matterWFCompView.DisplayOrder && m.DisplayStatusTypeId != (int)DisplayStatusTypeEnum.Hide && m.DisplayStatusTypeId != (int)DisplayStatusTypeEnum.InactiveNeverShown)
                .OrderBy(o => o.DisplayOrder).FirstOrDefault();
        }

        public MatterWFComponent GetNextMatterWFComponent(MatterCustomEntities.MatterWFComponentView matterWFCompView, WFComponentEnum componentToGet)
        {
            return CurrentMatterWFComponentsForDisplay().Where
                (m => m.MatterId == matterWFCompView.MatterId && m.DisplayOrder > matterWFCompView.DisplayOrder &&
                        m.WFComponentId == (int)componentToGet)
                .OrderBy(o => o.DisplayOrder).FirstOrDefault();
        }

        private MatterWFComponent NextMilestoneForComponent(MatterCustomEntities.MatterWFComponentView matterWFCompView, WFComponentEnum componentToCheck)
        {
            return CurrentMatterWFComponentsForDisplay().Where
                (m => m.MatterId == matterWFCompView.MatterId && m.DisplayOrder > matterWFCompView.DisplayOrder && m.WFComponentId == (int)componentToCheck)
                .OrderBy(o => o.DisplayOrder).FirstOrDefault();
        }

        public bool NextMilestoneCheck(MatterCustomEntities.MatterWFComponentView matterWFCompView, WFComponentEnum componentToCheck)
        {
            var nextWFComp = NextMilestoneForComponent(matterWFCompView, componentToCheck);
            if (nextWFComp != null && nextWFComp.WFComponentId == (int)componentToCheck)
                return true;
            else
                return false;
        }

        public void RemoveNextMilestoneComponent(MatterCustomEntities.MatterWFComponentView matterWFCompView, WFComponentEnum componentToCheck)
        {
            var nextComponent = NextMilestoneForComponent(matterWFCompView, componentToCheck);
            if (nextComponent != null)
            {

                context.MatterWFComponentDependents.RemoveRange(context.MatterWFComponentDependents.Where(x => x.MatterWFComponentId == nextComponent.MatterWFComponentId
                                                                                            || x.DependentMatterWFComponentId == nextComponent.MatterWFComponentId));

                SoftDeleteComponent(nextComponent.MatterWFComponentId);

                UpdateComponentEventsAfterMilestoneRemoval(nextComponent.MatterId, nextComponent.MatterWFComponentId, GetComponentDescription(componentToCheck));

                //FixMilestoneDisplayOrder(matterWFCompView.MatterId, matterWFCompView.DisplayOrder);
            }
        }

        private string GetComponentDescription(WFComponentEnum component)
        {
            switch (component)
            {
                case WFComponentEnum.DocsSentToAgent:
                    return "Docs Sent to Agent";
            }
            return string.Empty;
        }

        private void addDependancies(IEnumerable<MatterWFComponent> wfComps, int addWFComponentId, MatterWFComponent newComp)
        {


            //Replicate all dependencies to the existing component to the newly added component

            IEnumerable<MatterWFComponentDependent> newDepends = context.MatterWFComponentDependents.Where(m => m.DependentMatterWFComponentId == addWFComponentId);
            if (newDepends != null)
            {
                foreach (MatterWFComponentDependent dep in newDepends)
                {
                    //                        dep.DependentMatterWFComponentId = newComp.MatterWFComponentId;
                    MatterWFComponentDependent newDepend = new MatterWFComponentDependent();
                    newDepend.MatterWFComponentId = dep.MatterWFComponentId;
                    newDepend.DependentMatterWFComponentId = newComp.MatterWFComponentId;
                    if (!context.MatterWFComponentDependents.Any(w => (w.MatterWFComponentId == newDepend.MatterWFComponentId && w.DependentMatterWFComponentId == newDepend.DependentMatterWFComponentId) ||
                                                                    (w.MatterWFComponentId == newDepend.DependentMatterWFComponentId && w.MatterWFComponentId == newDepend.MatterWFComponentDependentId)))
                    {
                        context.MatterWFComponentDependents.Add(newDepend);
                        context.SaveChanges();
                    }
                }
            }
            //Search forward to see if there are any future milestones which are dependent on this type of milestone. If so, link it in
            foreach (var wfComp in wfComps)
            {
                if (context.MatterWFComponentDependents.Any(m => m.MatterWFComponentId == wfComp.MatterWFComponentId && m.MatterWFComponent1.WFComponentId == addWFComponentId))
                {
                    MatterWFComponentDependent newDepend = new MatterWFComponentDependent();
                    newDepend.MatterWFComponentId = wfComp.MatterWFComponentId;
                    newDepend.DependentMatterWFComponentId = newComp.MatterWFComponentId;
                    if (!context.MatterWFComponentDependents.Any(w => (w.MatterWFComponentId == newDepend.MatterWFComponentId && w.DependentMatterWFComponentId == newDepend.DependentMatterWFComponentId) ||
                                                                (w.MatterWFComponentId == newDepend.DependentMatterWFComponentId && w.MatterWFComponentId == newDepend.MatterWFComponentDependentId)))
                    {
                        context.MatterWFComponentDependents.Add(newDepend);

                        context.SaveChanges();
                    }
                }
            }
        }




        //newmatterwfcomponent needs matternumber
        public int insertcomponentinexistingworkflow(IEnumerable<MatterWFComponent> wfComps, MatterWFComponent previousMatterWfComponent, MatterWFComponent newMatterWfComponent)
        {
            foreach (MatterWFComponent wfComp in wfComps)
            {
                if (wfComp.DisplayOrder > previousMatterWfComponent.DisplayOrder)
                {
                    wfComp.DisplayOrder++;
                }
            }

            newMatterWfComponent.DisplayOrder = previousMatterWfComponent.DisplayOrder++;

            context.MatterWFComponents.Add(newMatterWfComponent);
            context.SaveChanges();
            addDependancies(wfComps, newMatterWfComponent.MatterWFComponentId, newMatterWfComponent);

            return 0;
        }

        public MatterWFComponent InsertComponent(int matterWFComponentId, int insertWFComponentId, int matterWFCompStatusId, bool addDependency)
        {
            return ComponentInsert(matterWFComponentId, insertWFComponentId, matterWFCompStatusId, Enums.WFComponentAddEnum.Insert, addDependency);
        }

        // NOTE:
        //The following is copy of AddComponent, ComponentInsert etc. - CODE can be removed once NEW LOANS GOES IN
        // Code works for DOCS SENT TO AGENT ONLY - can be Reused to pass in DEFAULT Task Allocation - if it is then rename / refactor appropriately with other routines
        // CAN BE REMOVED for NEW LOANS version - requirements - INACTIVE milestones
        //public int AddDocsSentToAgentComponent(int matterWFComponentId, int insertWFComponentId, int matterWFCompStatusId, bool addDependency, int dfltTaskAllocationType = (int) TaskAllocationTypeEnum.Unallocated)
        //{
        //    return ComponentInsertDocsSentToAgent(matterWFComponentId, insertWFComponentId, matterWFCompStatusId, Enums.WFComponentAddEnum.Add, addDependency,isVisible:true, dfltTaskAllocation:dfltTaskAllocationType);
        //}

        //private int ComponentInsertDocsSentToAgent(int matterWFComponentId, int addWFComponentId, int matterWFCompStatusId, Enums.WFComponentAddEnum insertType, bool addDependency, bool isVisible, int dfltTaskAllocation)
        //{
        //    //1 - Get Display order and matterId for this component
        //    MatterWFComponent comp = context.MatterWFComponents.Where(m => m.MatterWFComponentId == matterWFComponentId).FirstOrDefault();

        //    //2- Increment display order of components to make way for the new one
        //    int startDisplayOrder;
        //    if (insertType == Enums.WFComponentAddEnum.Insert)
        //        startDisplayOrder = comp.DisplayOrder;
        //    else
        //        startDisplayOrder = comp.DisplayOrder + 1;
        //    IEnumerable<MatterWFComponent> wfComps = context.MatterWFComponents.Where(m => m.MatterId == comp.MatterId && m.DisplayOrder >= startDisplayOrder);
        //    foreach (MatterWFComponent wfComp in wfComps)
        //        wfComp.DisplayOrder += 1;

        //    //3 - Create new component
        //    MatterWFComponent newComp = new MatterWFComponent();
        //    newComp.MatterId = comp.MatterId;
        //    newComp.WFComponentId = addWFComponentId;
        //    newComp.CurrProcedureNo = 0;
        //    newComp.WFComponentStatusTypeId = matterWFCompStatusId;
        //    newComp.DisplayOrder = startDisplayOrder;
        //    newComp.DisplayStatusTypeId = (int) DisplayStatusTypeEnum.Default;
        //    newComp.DefTaskAllocTypeId = dfltTaskAllocation;
        //    AddDueDateInfo(newComp);
        //    newComp.UpdatedDate = DateTime.Now;
        //    newComp.UpdatedByUserId = Slick_Domain.Common.DomainConstants.SystemUserId;
        //    context.MatterWFComponents.Add(newComp);

        //    context.SaveChanges();

        //    //4 - Link in the dependencies
        //    if (addDependency)
        //    {
        //        if (insertType == Enums.WFComponentAddEnum.Insert)
        //        {
        //            //Create a dependency between existing component and newly inserted componenet
        //            MatterWFComponentDependent newDepend = new MatterWFComponentDependent();
        //            newDepend.MatterWFComponentId = matterWFComponentId;
        //            newDepend.DependentMatterWFComponentId = newComp.MatterWFComponentId;
        //            context.MatterWFComponentDependents.Add(newDepend);
        //        }

        //        if (insertType == Enums.WFComponentAddEnum.Add)
        //        {
        //            //Replicate all dependencies to the existing component to the newly added component
        //            IEnumerable<MatterWFComponentDependent> newDepends = context.MatterWFComponentDependents.Where(m => m.DependentMatterWFComponentId == matterWFComponentId);
        //            foreach (MatterWFComponentDependent dep in newDepends)
        //            {
        //                //                        dep.DependentMatterWFComponentId = newComp.MatterWFComponentId;
        //                MatterWFComponentDependent newDepend = new MatterWFComponentDependent();
        //                newDepend.MatterWFComponentId = dep.MatterWFComponentId;
        //                newDepend.DependentMatterWFComponentId = newComp.MatterWFComponentId;
        //                context.MatterWFComponentDependents.Add(newDepend);
        //            }
        //        }
        //        context.SaveChanges();
        //    }

        //    return newComp.MatterWFComponentId;
        //}



        // END DOCS SENT TO AGENT - Removal code

        public MatterWFComponent InsertFloatingCompIntoWorkflow(int insertAfterMatterWFComponentId, int newWFComponentId, int taskAllocType, int? dueDateFormula, int? dueDateDaysOffset, int? dueTimeFormula = null, int? dueTimeHourOffset = null,  DateTime? dueDate = null)
        {
            MatterWFComponent insertAfterComp = context.MatterWFComponents.Where(m => m.MatterWFComponentId == insertAfterMatterWFComponentId).FirstOrDefault();

            int newDisplayOrder = -10;

            MatterWFComponent addedComp = new MatterWFComponent()
            {
                MatterId = insertAfterComp.MatterId,
                DisplayOrder = newDisplayOrder,
                WFComponentId = newWFComponentId,
                DefTaskAllocTypeId = taskAllocType,
                WFComponentDueDateFormulaId = dueDateFormula,
                WFComponentDueTimeFormulaId = dueTimeFormula,
                DisplayStatusTypeId = (int)DisplayStatusTypeEnum.Default,
                CurrProcedureNo = 0,
                WFComponentStatusTypeId = (int)MatterWFComponentStatusTypeEnum.NotStarted,
                UpdatedDate = DateTime.Now,
                UpdatedByUserId = DomainConstants.SystemUserId
            };

            context.MatterWFComponents.Add(addedComp);
            context.SaveChanges();

            AddMatterEvent(addedComp.MatterId, addedComp.MatterWFComponentId, MatterEventTypeList.MilestoneInserted, $"PST PACKET INSERTED AFTER \"{insertAfterComp.WFComponent.WFComponentName}\"", eventCreatedByNotes: "By PST Packet Milestone Bot");

            MatterWFComponentDependent newDepend = new MatterWFComponentDependent();

            newDepend.MatterWFComponentId = addedComp.MatterWFComponentId;
            newDepend.DependentMatterWFComponentId = insertAfterMatterWFComponentId;

            context.MatterWFComponentDependents.Add(newDepend);

            context.SaveChanges();

            ActivateComponents(insertAfterComp.MatterId, false, true, DomainConstants.SystemUserId);
            UpdateDueDates(insertAfterComp.MatterId);

            return addedComp;
        }

        

        public MatterWFComponent AddComponent(int matterWFComponentId, int insertWFComponentId, int matterWFCompStatusId, bool addDependency)
        {
            return ComponentInsert(matterWFComponentId, insertWFComponentId, matterWFCompStatusId, Enums.WFComponentAddEnum.Add, addDependency, (int)TaskAllocationTypeEnum.Unallocated);
        }
        public MatterWFComponent AddComponent(int matterWFComponentId, int insertWFComponentId, int matterWFCompStatusId, bool addDependency, int dfltTaskAllocation)
        {
            return ComponentInsert(matterWFComponentId, insertWFComponentId, matterWFCompStatusId, Enums.WFComponentAddEnum.Add, addDependency, dfltTaskAllocation);
        }
        private MatterWFComponent ComponentInsert(int matterWFComponentId, int addWFComponentId, int matterWFCompStatusId, Enums.WFComponentAddEnum insertType, bool addDependency, bool isVisible = true)
        {
            return ComponentInsert(matterWFComponentId, addWFComponentId, matterWFCompStatusId, insertType, addDependency, (int)TaskAllocationTypeEnum.Unallocated, isVisible);
        }
        private MatterWFComponent ComponentInsert(int matterWFComponentId, int addWFComponentId, int matterWFCompStatusId, Enums.WFComponentAddEnum insertType, bool addDependency, int dfltTaskAllocation, bool isVisible = true)
        {
            //1 - Get Display order and matterId for this component
            MatterWFComponent comp = context.MatterWFComponents.Where(m => m.MatterWFComponentId == matterWFComponentId).FirstOrDefault();

            //2- Increment display order of components to make way for the new one
            int startDisplayOrder;
            if (insertType == Enums.WFComponentAddEnum.Insert)
                startDisplayOrder = comp.DisplayOrder;
            else
                startDisplayOrder = comp.DisplayOrder + 1;
            IEnumerable<MatterWFComponent> wfComps = context.MatterWFComponents.Where(m => m.MatterId == comp.MatterId && m.DisplayOrder >= startDisplayOrder);
            foreach (MatterWFComponent wfComp in wfComps)
                wfComp.DisplayOrder += 1;

            //3 - Create new component
            MatterWFComponent newComp = new MatterWFComponent();
            newComp.MatterId = comp.MatterId;
            newComp.WFComponentId = addWFComponentId;
            newComp.CurrProcedureNo = 0;
            newComp.WFComponentStatusTypeId = matterWFCompStatusId;
            newComp.DisplayOrder = startDisplayOrder;
            newComp.DisplayStatusTypeId = (int)DisplayStatusTypeEnum.Default;
            newComp.DefTaskAllocTypeId = dfltTaskAllocation;
            AddDueDateInfo(newComp);
            newComp.UpdatedDate = DateTime.Now;
            newComp.UpdatedByUserId = GlobalVars.CurrentUserId ?? Slick_Domain.Common.DomainConstants.SystemUserId;
            context.MatterWFComponents.Add(newComp);

            context.SaveChanges();

            //4 - Link in the dependencies
            if (addDependency)
            {
                if (insertType == Enums.WFComponentAddEnum.Insert)
                {
                    //Create a dependency between existing component and newly inserted componenet
                    MatterWFComponentDependent newDepend = new MatterWFComponentDependent();
                    newDepend.MatterWFComponentId = matterWFComponentId;
                    newDepend.DependentMatterWFComponentId = newComp.MatterWFComponentId;
                    context.MatterWFComponentDependents.Add(newDepend);
                }

                if (insertType == Enums.WFComponentAddEnum.Add)
                {
                    //Replicate all dependencies to the existing component to the newly added component
                    IEnumerable<MatterWFComponentDependent> newDepends = context.MatterWFComponentDependents.Where(m => m.DependentMatterWFComponentId == matterWFComponentId);
                    foreach (MatterWFComponentDependent dep in newDepends)
                    {
                        //                        dep.DependentMatterWFComponentId = newComp.MatterWFComponentId;
                        MatterWFComponentDependent newDepend = new MatterWFComponentDependent();
                        newDepend.MatterWFComponentId = dep.MatterWFComponentId;
                        newDepend.DependentMatterWFComponentId = newComp.MatterWFComponentId;
                        context.MatterWFComponentDependents.Add(newDepend);
                    }
                }

                //Search forward to see if there are any future milestones which are dependent on this type of milestone. If so, link it in
                foreach (var wfComp in wfComps)
                {
                    if (context.MatterWFComponentDependents.Any(m => m.MatterWFComponentId == wfComp.MatterWFComponentId && m.MatterWFComponent1.WFComponentId == addWFComponentId))
                    {
                        MatterWFComponentDependent newDepend = new MatterWFComponentDependent();
                        newDepend.MatterWFComponentId = wfComp.MatterWFComponentId;
                        newDepend.DependentMatterWFComponentId = newComp.MatterWFComponentId;
                        context.MatterWFComponentDependents.Add(newDepend);
                    }
                }

                context.SaveChanges();
            }

            return newComp;
        }

        private void AddDueDateInfo(MatterWFComponent newComp)
        {
            if (newComp.WFComponentId == (int)Enums.WFComponentEnum.DocsSentToAgent)
            {
                newComp.WFComponentDueDateFormulaId = 4;
                newComp.DueDateFormulaOffset = 3;
            }
        }


        public int ComponentInsert(MatterCustomEntities.MatterWFComponentView matterWFComponentView, List<MatterCustomEntities.MatterWFComponentBuildView> addMWFComponents, Enums.WFComponentAddEnum insertType, bool addStartDependency, bool addEndDependency, bool resetNextComponentStatus)
        {
            IEnumerable<MatterWFComponent> matterWFComps = context.MatterWFComponents.Where(m => m.MatterId == matterWFComponentView.MatterId);

            //Increment the display orders to make space for components to be inserted.
            int displayOrderIncStart = matterWFComponentView.DisplayOrder;
            if (insertType == Enums.WFComponentAddEnum.Add)
                displayOrderIncStart++;

            //Things we'll need for later
            MatterWFComponent insertPrev = matterWFComps.OrderByDescending(m => m.DisplayOrder).FirstOrDefault(m => m.DisplayOrder < displayOrderIncStart);
            MatterWFComponent insertNext = matterWFComps.OrderBy(m => m.DisplayOrder).FirstOrDefault(m => m.DisplayOrder >= displayOrderIncStart);


            //Increment display order of components to make way for the new one
            foreach (var incrComp in matterWFComps.Where(m => m.DisplayOrder >= displayOrderIncStart))
            {
                incrComp.DisplayOrder = incrComp.DisplayOrder + addMWFComponents.Count;
            }

            int tmpCount = displayOrderIncStart;
            foreach (var addMWFComponent in addMWFComponents)
            {
                MatterWFComponent newComp = new MatterWFComponent();
                newComp.MatterId = matterWFComponentView.MatterId;
                newComp.WFComponentId = addMWFComponent.WFComponentId;

                newComp.CurrProcedureNo = 0;
                newComp.WFComponentStatusTypeId = addMWFComponent.WFComponentStatusTypeId;
                newComp.DisplayOrder = tmpCount;
                newComp.DisplayStatusTypeId = addMWFComponent.DisplayStatusTypeId;
                newComp.UpdatedDate = DateTime.Now;
                newComp.UpdatedByUserId = GlobalVars.CurrentUserId ?? Slick_Domain.Common.DomainConstants.SystemUserId;
                newComp.DefTaskAllocTypeId = (int)TaskAllocationTypeEnum.Unallocated;
                context.MatterWFComponents.Add(newComp);
                context.SaveChanges();
                addMWFComponent.MatterWFComponentId = newComp.MatterWFComponentId;
                tmpCount++;
            }

            //Now Dependency fun...
            foreach (var addMWFComponent in addMWFComponents.OrderBy(o => o.DisplayOrder))
            {
                foreach (var mwfDep in addMWFComponent.DependentMatterWFComponents)
                {
                    var depComp = matterWFComps.OrderByDescending(o => o.DisplayOrder).First(m => m.WFComponentId == mwfDep.DependentWFComponentId);

                    if (depComp != null)
                    {
                        MatterWFComponentDependent newMWFDep = new MatterWFComponentDependent();
                        newMWFDep.MatterWFComponentId = addMWFComponent.MatterWFComponentId.Value;
                        newMWFDep.DependentMatterWFComponentId = depComp.MatterWFComponentId;
                        context.MatterWFComponentDependents.Add(newMWFDep);
                        context.SaveChanges();
                        mwfDep.MatterWFComponentDependentId = newMWFDep.MatterWFComponentDependentId;
                    }
                }
            }

            if (addStartDependency)
            {
                var firstAddMWFComp = addMWFComponents.OrderBy(o => o.DisplayOrder).First();
                if (firstAddMWFComp != null && insertPrev != null)
                {
                    //Does dependency already exist
                    if (!context.MatterWFComponentDependents.Any(m => m.MatterWFComponentId == firstAddMWFComp.MatterWFComponentId && m.DependentMatterWFComponentId == insertPrev.MatterWFComponentId))
                    {
                        MatterWFComponentDependent newMWFDep = new MatterWFComponentDependent();
                        newMWFDep.MatterWFComponentId = firstAddMWFComp.MatterWFComponentId.Value;
                        newMWFDep.DependentMatterWFComponentId = insertPrev.MatterWFComponentId;
                        context.MatterWFComponentDependents.Add(newMWFDep);
                        context.SaveChanges();
                    }
                }
            }

            if (addEndDependency)
            {
                var lastAddMWFComp = addMWFComponents.OrderBy(o => o.DisplayOrder).Last();
                if (lastAddMWFComp != null && insertNext != null)
                {
                    //Does dependency already exist
                    if (!context.MatterWFComponentDependents.Any(m => m.MatterWFComponentId == lastAddMWFComp.MatterWFComponentId && m.DependentMatterWFComponentId == insertNext.MatterWFComponentId))
                    {
                        MatterWFComponentDependent newMWFDep = new MatterWFComponentDependent();
                        newMWFDep.MatterWFComponentId = insertNext.MatterWFComponentId;
                        newMWFDep.DependentMatterWFComponentId = lastAddMWFComp.MatterWFComponentId.Value;
                        context.MatterWFComponentDependents.Add(newMWFDep);
                        context.SaveChanges();
                    }
                }
            }

            if (resetNextComponentStatus && insertNext != null)
            {
                insertNext.WFComponentStatusTypeId = (int)Enums.MatterWFComponentStatusTypeEnum.NotStarted;
                insertNext.UpdatedDate = DateTime.Now;
                insertNext.UpdatedByUserId = GlobalVars.CurrentUserId ?? Slick_Domain.Common.DomainConstants.SystemUserId;

                //1 - Mark the events for this component to be "demoted" as "Cancelled"
                var matterEvents = context.MatterEvents.Where(m => m.MatterWFComponentId == insertNext.MatterWFComponentId && m.MatterEventStatusTypeId == (int)MatterEventStatusTypeEnum.Good);
                foreach (var me in matterEvents)
                    me.MatterEventStatusTypeId = (int)MatterEventStatusTypeEnum.Cancelled;

                //Event for the undo
                AddMatterEvent(insertNext.MatterId, insertNext.MatterWFComponentId, MatterEventTypeList.MilestoneUndone, "Milestone reset due to inserted tasks");
            }


            ActivateComponents(matterWFComponentView.MatterId);

            if (addMWFComponents.FirstOrDefault().MatterWFComponentId.HasValue)
                return addMWFComponents.FirstOrDefault().MatterWFComponentId.Value;
            else
                return 0;
        }
        public MatterWFComponent GetFirstActiveComponentForMatterNonExclusive(int matterId)
        {
            return context.MatterWFComponents.Where(m => m.MatterId == matterId &&
                                                        m.DisplayStatusTypeId != (int)DisplayStatusTypeEnum.Inactive && m.DisplayStatusTypeId != (int)DisplayStatusTypeEnum.InactiveNeverShown)
                    .OrderBy(m => m.DisplayOrder).FirstOrDefault();
        }
        public MatterWFComponent GetFirstActiveComponentForMatter(int matterId)
        {
            var toReturn = context.MatterWFComponents.Where(m => m.MatterId == matterId && m.WFComponentStatusType.IsActiveStatus && m.WFComponentId != (int)WFComponentEnum.PEXAWorkspace
            && m.WFComponentId != (int)WFComponentEnum.FileManagerAcknowledgement
            && m.WFComponentId != (int)WFComponentEnum.PrintAndCollateFile && m.WFComponentId != (int)WFComponentEnum.ReworkReview && m.WFComponentId != (int)WFComponentEnum.Requisition
            && m.WFComponentId != (int)WFComponentEnum.ChangePexaWorkspaces && m.WFComponentId != (int)WFComponentEnum.CreatePSTPacket && m.WFComponentId != (int)WFComponentEnum.IDYouInvitation && m.WFComponentId != (int)WFComponentEnum.IDYouReportApproved && m.WFComponentId != (int)WFComponentEnum.IDYouCompleted && m.WFComponentId != (int)WFComponentEnum.IDYouReportSent &&
                                                        m.DisplayStatusTypeId != (int)DisplayStatusTypeEnum.Inactive && m.DisplayStatusTypeId != (int)DisplayStatusTypeEnum.InactiveNeverShown)
                    .OrderBy(m => m.DisplayOrder).FirstOrDefault();

            if (toReturn == null)
            {
                return context.MatterWFComponents.Where(m => m.MatterId == matterId && m.WFComponentStatusType.IsActiveStatus && m.WFComponentId != (int)WFComponentEnum.PEXAWorkspace
                && m.WFComponentId != (int)WFComponentEnum.ChangePexaWorkspaces && m.WFComponentId != (int)WFComponentEnum.CreatePSTPacket && m.WFComponentId != (int)WFComponentEnum.FileManagerAcknowledgement && m.WFComponentId != (int)WFComponentEnum.ReworkReview && m.WFComponentId != (int)WFComponentEnum.IDYouReportApproved && m.WFComponentId != (int)WFComponentEnum.IDYouInvitation && m.WFComponentId != (int)WFComponentEnum.IDYouCompleted && m.WFComponentId != (int)WFComponentEnum.IDYouReportSent && m.WFComponentId != (int)WFComponentEnum.Requisition &&
                                                        m.DisplayStatusTypeId != (int)DisplayStatusTypeEnum.Inactive && m.DisplayStatusTypeId != (int)DisplayStatusTypeEnum.InactiveNeverShown)
                    .OrderBy(m => m.DisplayOrder).FirstOrDefault();
            }
            else
            {
                return toReturn;
            }
        }
        public MatterWFComponent GetLastActiveComponentForMatterIgnoringFloaters(int matterId)
        {
            var toReturn = context.MatterWFComponents.Where(m => m.MatterId == matterId && m.WFComponentStatusType.IsActiveStatus && m.WFComponentId != (int)WFComponentEnum.FileManagerAcknowledgement && m.WFComponentId != (int)WFComponentEnum.PEXAWorkspace && m.WFComponentId != (int)WFComponentEnum.PrintAndCollateFile && m.WFComponentId != (int)WFComponentEnum.ChangePexaWorkspaces &&
                                                        m.DisplayStatusTypeId != (int)DisplayStatusTypeEnum.Inactive && m.DisplayStatusTypeId != (int)DisplayStatusTypeEnum.InactiveNeverShown)
                    .OrderByDescending(m => m.DisplayOrder).FirstOrDefault();
            if (toReturn == null)
            {
                return context.MatterWFComponents.Where(m => m.MatterId == matterId && m.WFComponentStatusType.IsActiveStatus && m.WFComponentId != (int)WFComponentEnum.FileManagerAcknowledgement && m.WFComponentId != (int)WFComponentEnum.PEXAWorkspace && m.WFComponentId != (int)WFComponentEnum.ChangePexaWorkspaces &&
                                                       m.DisplayStatusTypeId != (int)DisplayStatusTypeEnum.Inactive && m.DisplayStatusTypeId != (int)DisplayStatusTypeEnum.InactiveNeverShown)
                    .OrderByDescending(m => m.DisplayOrder).FirstOrDefault();
            }
            else return toReturn;
        }
        public MatterWFComponent GetFirstActiveOrLastCompletedComponentForMatter(int matterId)
        {
            var component = GetFirstActiveComponentForMatter(matterId);
            if (component == null)
            {
                component = GetLastCompletedComponentForMatter(matterId);
            }
            return component;
        }

        public MatterWFComponent GetLastCompletedComponentForMatter(int matterId)
        {
            return context.MatterWFComponents.Where
                (m => m.MatterId == matterId && m.WFComponentStatusType.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.Complete
                && m.WFComponentId != (int)WFComponentEnum.FileManagerAcknowledgement && m.WFComponentId != (int)WFComponentEnum.ReworkReview && m.WFComponentId != (int)WFComponentEnum.Requisition
                && m.WFComponentId != (int)WFComponentEnum.CustodianIssue && m.WFComponentId != (int)WFComponentEnum.LodgementIssue

                )
                    .OrderByDescending(m => m.DisplayOrder).FirstOrDefault();
        }

        public MatterWFComponent GetLastActiveComponentForMatter(int matterId)
        {
            return context.MatterWFComponents.Where
                (m => m.MatterId == matterId && m.WFComponentId != (int)WFComponentEnum.FileManagerAcknowledgement &&
                        (m.WFComponentStatusType.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.InProgress ||
                         m.WFComponentStatusType.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.Starting))
                    .OrderByDescending(m => m.DisplayOrder).FirstOrDefault();
        }

        public IEnumerable<WFCustomEntities.WorkflowTemplateView> GetTemplatesGrid()
        {
            List<WFCustomEntities.WorkflowTemplateView> templates = context.WFTemplates.AsNoTracking()
                .Select(s2 =>
                    new
                    {
                        s2.WFTemplateId,
                        s2.WFTemplateName,
                        s2.MatterType.MatterTypeName,
                        s2.MortMgr.MortMgrName,
                        s2.State.StateName,
                        s2.Lender.LenderName,
                        s2.Notes,
                        s2.Enabled,
                        s2.DischargeSelfActing,
                        s2.User.Username,
                        s2.UpdatedDate
                    })
                .ToList()
                .Select(st => new WFCustomEntities.WorkflowTemplateView
                {
                    Id = st.WFTemplateId,
                    MatterType = st.MatterTypeName ?? DomainConstants.AnySelection,
                    LenderName = st.LenderName ?? DomainConstants.AnySelection,
                    MortgageManager = st.MortMgrName ?? DomainConstants.AnySelection,
                    TemplateName = st.WFTemplateName,
                    StateName = st.StateName ?? DomainConstants.AnySelection,
                    DischargeSelfActingType = st.DischargeSelfActing.HasValue ? (st.DischargeSelfActing == true ? "YES" : "NO") : " - EITHER - ",
                    Notes = st.Notes,
                    IsEnabled = st.Enabled,
                    UpdatedDate = st.UpdatedDate,
                    UpdatedBy = st.Username
                })
                .ToList();

            return templates;

        }

        public WFCustomEntities.WorkflowTemplateView GetWFTemplate(int id)
        {
            var item = GetTemplateRepository().FindById(id);

            var template = new WFCustomEntities.WorkflowTemplateView()
            {
                Id = item.WFTemplateId,
                LenderName = item.Lender?.LenderName ?? DomainConstants.AnySelection,
                MatterType = item.MatterType?.MatterTypeName,
                MortgageManager = item.MortMgr?.MortMgrName ?? DomainConstants.AnySelection,
                TemplateName = item.WFTemplateName,
                StateName = item.State?.StateName ?? DomainConstants.AnySelection,
                DischargeSelfActingType = item.DischargeSelfActing.HasValue ? (item.DischargeSelfActing == true ? "YES" : "NO") : " - EITHER - ",

                Notes = item.Notes,
                IsEnabled = item.Enabled,
                UpdatedDate = item.UpdatedDate,
                UpdatedBy = item.User.Username
            };

            return template;
        }

        public IEnumerable<WFCustomEntities.WFTemplateComponentView> GetWFTemplateComponents(int templateId)
        {
            return (from wf in context.WFTemplateComponents
                    join wfd in context.WFTemplateComponentDependents on wf.WFTemplateComponentId equals wfd.WFTemplateComponentId into wfdg
                    where wf.WFTemplateId == templateId
                    select new
                    {
                        wf.WFTemplateId,
                        wf.WFTemplateComponentId,
                        wf.WFComponentId,
                        wf.WFTemplate.WFTemplateName,
                        wf.WFComponent.WFComponentName,
                        wf.WFComponentDueDateFormulaId,
                        wf.WFComponentDueDateFormula.WFComponentDueDateFormulaName,
                        wf.WFComponentDueTimeFormulaId,
                        wf.WFComponentDueTimeFormula.WFComponentDueTimeFormulaName,
                        wf.DueDateFormulaOffset,
                        wf.WFComponentDueTimeHourOffset,
                        wf.DisplayOrder,
                        wf.DefTaskAllocTypeId,
                        wf.TaskAllocType.TaskAllocTypeName,
                        wf.DefTaskAllocWFComponentId,
                        DefTaskAllocComponent = wf.WFComponent1.WFComponentName,
                        wf.WFTemplateComponentDependents,
                        wf.User.Username,
                        wf.UpdatedDate,
                        wf.IsInactive,
                        wf.SettlementTypeId,
                        wf.SettlementType.SettlementTypeName,
                        Dependents = wfdg.Select(d => new { d.WFTemplateComponentDependentId, d.DependentWFComponentId, DependentWFComponentName = d.WFComponent.WFComponentName })
                    }).ToList()
                .Select(st => new WFCustomEntities.WFTemplateComponentView(st.WFTemplateComponentId, st.WFComponentId, st.WFTemplateId, st.WFTemplateName, st.WFComponentName, st.WFComponentDueDateFormulaId, st.WFComponentDueDateFormulaName, st.DueDateFormulaOffset, st.WFComponentDueTimeHourOffset,
                                                 st.WFComponentDueTimeFormulaId, st.WFComponentDueTimeFormulaName,
                                                 st.DisplayOrder, st.UpdatedDate, st.Username, st.DefTaskAllocTypeId, st.TaskAllocTypeName, st.DefTaskAllocWFComponentId, st.DefTaskAllocComponent, st.IsInactive,
                                                 st.SettlementTypeId, st.SettlementTypeName,
                                                 st.Dependents.Select(d => new WFCustomEntities.WFTemplateComponentDependentView
                                                 {
                                                     WFTemplateComponentDependentId = d.WFTemplateComponentDependentId,
                                                     WFTemplateComponentId = st.WFTemplateComponentId,
                                                     DependentWFComponentId = d.DependentWFComponentId,
                                                     DependentWFComponentName = d.DependentWFComponentName,
                                                     IsDirty = false
                                                 }).ToList()
                ))
                .OrderBy(o => o.DisplayOrder)
                .ToList();
        }

        public WFTemplateComponent GetChildWFTemplateComponent(int? templateId, int componentId, int? lenderId, int? mortMgrId, int? stateId)
        {
            if (templateId == null) return null;
            var matterTypeGroupId = context.WFTemplates.Where(w => w.WFTemplateId == templateId.Value).FirstOrDefault()?.MatterTypeId;

            if (!matterTypeGroupId.HasValue) return null;

            return (from mt in context.MatterTypes
                    join wft in context.WFTemplates on mt.MatterTypeId equals wft.MatterTypeId
                    join tc in context.WFTemplateComponents on wft.WFTemplateId equals tc.WFTemplateId
                    join wc in context.WFComponents on tc.WFComponentId equals wc.WFComponentId
                    where mt.MatterTypeGroupId == matterTypeGroupId.Value && tc.WFComponentId == componentId && wft.WFTemplateId != templateId
                          && wft.LenderId == lenderId && wft.MortMgrId == mortMgrId && wft.StateId == stateId
                    select tc).FirstOrDefault();
        }

        //private EntityCompacted GetGroupMatterForChildTemplateMatterType(int matterTypeId)
        //{
        //    var matterType = context.MatterTypes.Where(w => w.MatterTypeId == matterTypeId).FirstOrDefault();
        //    if (matterType == null) return null;
        //    return new EntityCompacted { Id = matterType.MatterTypeGroupId.Value, Details = matterType.MatterType2.MatterTypeName };
        //}

        //public IEnumerable<WFTemplateChild> GetAllSiblingWFTemplateComponents(int matterTypeId, int currTemplateId)
        //{
        //    var matterType = GetGroupMatterForChildTemplateMatterType(matterTypeId);
        //    if (matterType == null) return null;

        //    //This Requires Work if it is to happen - presume it is passing in a new Entity back
        //    var matterTypeGroupId = matterType.Id;
        //    var matterTypeGroupName = matterType.Details;

        //    var templates = new List<WFTemplateChild>();

        //    return templates;
        //}

        //public IEnumerable<WFCustomEntities.WFTemplateComponentView> GetWFTemplateGroupComponents(int matterTypeId, int? stateId, int? lenderId, int? mortMgrId)
        //{
        //    var groupMatterTypeId = context.MatterTypes.Where(w => w.MatterTypeId == matterTypeId).FirstOrDefault()?.MatterTypeGroupId;
        //    if (groupMatterTypeId == null) return null;

        //    var groupTemplateId = GetBestWorkFlowTemplateIdForMatter(groupMatterTypeId.Value, null, stateId, lenderId, mortMgrId);
        //    if (groupTemplateId == null) return null;

        //    return GetWFTemplateComponents(groupTemplateId.Value);
        //}

        public int? GetBestWorkFlowTemplateIdForMatter(int matterTypeId, int? stateId, int? lenderId, int? mortMgrId, int? loanTypeId = null, bool? selfActingTypeId = null)
        {
            //No longer having any templates for Matter Groups

            // If a matter group (from Create Matter), then find the "group default" matter type and use that template.


            var defMatterTypeId = GetMatterGroupDefaultMatterType(matterTypeId);

            var templates = context.WFTemplates
                .Where(w => ((w.MatterTypeId == defMatterTypeId || !w.MatterTypeId.HasValue) || (loanTypeId != null && w.LoanTypeId == loanTypeId)) && w.Enabled
                            && ((!selfActingTypeId.HasValue && !w.DischargeSelfActing.HasValue) || (!w.DischargeSelfActing.HasValue || w.DischargeSelfActing == selfActingTypeId))
                            && (matterTypeId != (int)MatterGroupTypeEnum.Consent || w.MatterTypeId.HasValue && w.MatterType.MatterTypeGroupId == (int)MatterGroupTypeEnum.Consent));

            var matterGroupTypeId = context.MatterTypes.Where(m => m.MatterTypeId == defMatterTypeId).Select(m => m.MatterTypeGroupId).FirstOrDefault();
            if (matterGroupTypeId == (int)MatterGroupTypeEnum.Consent)
            {
                templates = templates.Where(w => w.MatterType.MatterTypeGroupId == matterGroupTypeId);
            }

            var items = new List<GeneralCustomEntities.BestMatchForCriteria>();
            foreach (var wfTmp in templates)
            {
                items.Add(new GeneralCustomEntities.BestMatchForCriteria
                {
                    ID = wfTmp.WFTemplateId,
                    LenderId = wfTmp.LenderId,
                    MatterTypeId = wfTmp.MatterTypeId,
                    MortMgrId = wfTmp.MortMgrId,
                    StateId = wfTmp.StateId,
                    LoanTypeId = wfTmp.LoanTypeId,
                    isSpecific = wfTmp.DischargeSelfActing.HasValue
                });
            }

            return CommonMethods.GetBestValueFromSelectedQuery(defMatterTypeId, null, stateId, lenderId, mortMgrId, items, loanTypeId);
        }

        private int GetMatterGroupDefaultMatterType(int matterTypeId)
        {
            var mt = context.MatterTypes.FirstOrDefault(m => m.MatterTypeId == matterTypeId);

            if (!mt.IsMatterGroup)
                return mt.MatterTypeId;

            var mtDef = context.MatterTypes.FirstOrDefault(m => m.MatterTypeGroupId == matterTypeId && m.IsGroupDefault);

            if (mtDef != null)
                return mtDef.MatterTypeId;
            else
                throw (new Exception("Error finding Default TemplateId for Matter Group"));

        }

        public IEnumerable<EntityCompacted> GetCompletedComponentsForRestart
            (int matterId, int? currId, bool includeCurrentComponent = false, bool withInactive = false,
            int startFromComponent = (int)WFComponentEnum.CreateMatter, bool inclStartComponent = false)
        {
            context.SaveChanges();

            var components = context.MatterWFComponents.AsNoTracking()
                 .Where(x => x.WFComponentId != (int)WFComponentEnum.FileManagerAcknowledgement  && (x.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.Complete || x.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.Deleted) &&
                (x.MatterId == matterId && x.DisplayOrder != 1));
                
                //.Where(x => !((x.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.InProgress ||
                // x.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.NotStarted ||
                // x.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.Starting ||
                // x.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.OnHold) || !x.WFComponent.CanRestartMilestone));

            var startComponentDisplayOrder = components.Where(x => x.WFComponentId == startFromComponent)
                .OrderByDescending(x => x.DisplayOrder).FirstOrDefault()?.DisplayOrder ?? 1;

            if (inclStartComponent)
            {
                components = components.Where(x => x.DisplayOrder >= startComponentDisplayOrder);
            }
            else
            {
                components = components.Where(x => x.DisplayOrder > startComponentDisplayOrder);
            }

            if (currId.HasValue && !includeCurrentComponent)
            {
                components = components.Where(x => x.WFComponentId != currId);
            }

            //var excludeComponents = context.MatterWFComponents.AsNoTracking().Where(x => x.MatterId == matterId &&
            //    (x.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.InProgress ||
            //     x.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.NotStarted ||
            //     x.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.Starting ||
            //     x.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.OnHold) || !x.WFComponent.CanRestartMilestone
            //     ).Select(x => new EntityCompacted { Id = x.WFComponentId }).ToList();

            components = components.Where(x => x.WFComponentId != (int)WFComponentEnum.IDYouCompleted && x.WFComponentId != (int)WFComponentEnum.IDYouInvitation && x.WFComponentId != (int)WFComponentEnum.IDYouReportApproved && x.WFComponentId != (int)WFComponentEnum.IDYouReportSent && x.WFComponentId != (int)WFComponentEnum.Requisition && x.WFComponentId != (int)WFComponentEnum.CreatePSTPacket && x.WFComponentId != (int)WFComponentEnum.LodgementIssue && x.WFComponentId != (int)WFComponentEnum.CustodianAuditIssue && x.WFComponentId != (int)WFComponentEnum.CustodianIssue);

            components = components.Where(x => !(x.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.InProgress ||
                x.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.NotStarted ||
                x.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.Starting ||
                x.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.OnHold) || !x.WFComponent.CanRestartMilestone
                 );

            if (withInactive)
            {
                return components.Select(s => new { s.WFComponentId, s.WFComponent.WFComponentName, s.DisplayStatusTypeId, s.WFComponent.CanRestartMilestone, s.WFComponentStatusTypeId, s.DisplayOrder }).ToList()
                   .ToList().OrderBy(o=>o.DisplayOrder).GroupBy(s => new { s.WFComponentId, s.WFComponentName, s.DisplayStatusTypeId })
                    .Select(g=>new EntityCompacted { Id = g.Key.WFComponentId, Details = g.Key.WFComponentName, IsChecked = g.Key.DisplayStatusTypeId == (int)DisplayStatusTypeEnum.Inactive })
                        .Distinct().ToList();
            }
            else
            {
                return components.Select(s => new { s.WFComponentId, s.WFComponent.WFComponentName, s.DisplayStatusTypeId, s.WFComponent.CanRestartMilestone, s.WFComponentStatusTypeId, s.DisplayOrder }).ToList()
                   .ToList().OrderBy(o => o.DisplayOrder).GroupBy(s => new { s.WFComponentId, s.WFComponentName, s.DisplayStatusTypeId })
                   .Select(g => new EntityCompacted { Id = g.Key.WFComponentId, Details = g.Key.WFComponentName})
                       .Distinct().ToList();
            }

            ////return GetCompletedComponentsForRestart(components, includeCurrentComponent, withInactive).ToList()
            //    .Except(excludeComponents)
            //    .ToList();
        }

        private IEnumerable<EntityCompacted> GetCompletedComponentsForRestart(IQueryable<MatterWFComponent> components, bool includeCurrentComponent, bool withInactive)
        {
            if (withInactive)
            {
                return (from s in components
                        orderby s.DisplayOrder
                        group s by new { s.WFComponentId, s.WFComponent.WFComponentName, s.DisplayStatusTypeId } into g
                        select new EntityCompacted { Id = g.Key.WFComponentId, Details = g.Key.WFComponentName, IsChecked = g.Key.DisplayStatusTypeId == (int)DisplayStatusTypeEnum.Inactive })
                        .Distinct().ToList();
            }
            else
            {
                return (from s in components
                        orderby s.DisplayOrder
                        group s by new { s.WFComponentId, s.WFComponent.WFComponentName } into g
                        select new EntityCompacted { Id = g.Key.WFComponentId, Details = g.Key.WFComponentName }).Distinct().ToList();
            }
        }

        public IEnumerable<EntityCompacted> GetRestartComponentsForWFComponent(int? restartComponentId)
        {
            return context.MatterWFComponents.Where(m => m.WFComponentId == restartComponentId)
               .OrderBy(o => o.DisplayOrder)
               .Select(s => new EntityCompacted { Id = s.WFComponentId, Details = s.WFComponent.WFComponentName, RelatedId = s.MatterWFComponentId })
               .ToList();
        }

        public MatterCustomEntities.MatterWFIssueDetailView GetMatterWFIssueDetailView(int matterWFComponentId)
        {
            return context.MatterWFIssueDetails.AsNoTracking()
                    .Where(m => m.MatterWFComponentId == matterWFComponentId)
                    .Select(m => new MatterCustomEntities.MatterWFIssueDetailView
                    {
                        ID = m.MatterWFIssueDetailId,
                        MatterWFComponentId = m.MatterWFComponentId,
                        QuestionResponse = m.QuestionResponse,
                        ActionCompleted = m.ActionCompleted,
                        UpdatedDate = m.UpdatedDate,
                        UpdatedByUserId = m.UpdatedByUserId,
                        UpdatedByUsername = m.User.Username
                    }).FirstOrDefault();
        }




        public XmlMatterDetailsModel BuildXmlMatterDetailsModel(int matterId)
        {
            return context.Matters.AsNoTracking()
                .Where(t => t.MatterId == matterId)
                .Select(s2 =>
                    new
                    {
                        s2.MatterId,
                        s2.MatterGroupTypeId,
                        s2.FileOwnerUserId,
                        s2.LenderId,
                        s2.LenderRefNo,
                        LenderCode = s2.Lender.HotDocsCode,
                        s2.Lender.LenderName,
                        s2.MortMgrId,
                        M_Name = s2.MortMgr.MortMgrName,
                        M_StreetAddress = s2.MortMgr.StreetAddress,
                        M_Suburb = s2.MortMgr.Suburb,
                        M_State = s2.MortMgr.State.StateName,
                        M_PostCode = s2.MortMgr.PostCode,
                        M_ContactLastname = s2.MortMgr.PrimaryContact.Lastname,
                        M_ContactFirstname = s2.MortMgr.PrimaryContact.Firstname,
                        M_Phone = s2.MortMgr.PrimaryContact.Phone,
                        M_Email = s2.MortMgr.PrimaryContact.Email,
                        M_MortMgrCode = s2.MortMgr.MortMgrLenders.FirstOrDefault(x => x.MortMgrId == s2.MortMgrId && x.LenderId == s2.LenderId).LenderMortMgrRef,
                        s2.BrokerId,
                        OfficeId = (int?)s2.State.Offices.FirstOrDefault(o => o.StateId == s2.StateId).OfficeId,
                        B_Lastname = s2.Broker.PrimaryContact.Lastname,
                        B_Firstname = s2.Broker.PrimaryContact.Firstname,
                        B_Address1 = s2.Broker.StreetAddress,
                        B_Suburb = s2.Broker.Suburb,
                        B_State = s2.Broker.State.StateName,
                        B_PostCode = s2.Broker.PostCode,
                        B_Contact = s2.Broker.PrimaryContact.Lastname,
                        B_Phone = s2.Broker.PrimaryContact.Phone,
                        B_Email = s2.Broker.PrimaryContact.Email
                    })
                .ToList()
                .Select(st => new XmlMatterDetailsModel
                {
                    MatterId = matterId,
                    MatterType = GetSingleMatterTypeNameForGroup(st.MatterGroupTypeId),
                    FileOwnerId = st.FileOwnerUserId,
                    LenderId = st.LenderId,
                    Lender = st.LenderCode,
                    LenderName = st.LenderName,
                    MortMgrId = st.MortMgrId,
                    MortMgrCode = st.M_MortMgrCode,
                    MortMgr = st.M_Name,
                    BrokerId = st.BrokerId,
                    OfficeId = st.OfficeId,
                    LenderReferenceId = st.LenderRefNo,
                    BrokerDetails = new PersonCompanyDetails
                    {
                        Address = new AddressDetails
                        {
                            AddressLine1 = st.B_Address1,
                            PostCode = st.B_PostCode,
                            State = st.B_State,
                            Suburb = st.B_Suburb
                        },
                        FirstName = st.B_Firstname,
                        LastName = st.B_Lastname,
                        Email = st.B_Email,
                        Mobile = st.B_Phone
                    },
                    MortMgrDetails = new PersonCompanyDetails
                    {
                        Address = new AddressDetails
                        {
                            AddressLine1 = st.M_StreetAddress,
                            PostCode = st.M_PostCode,
                            State = st.M_State,
                            Suburb = st.M_Suburb
                        },
                        FullName = st.M_Name,
                        Email = st.M_Email,
                        Mobile = st.M_Phone
                    }
                })
                .FirstOrDefault();
        }

        private string GetSingleMatterTypeNameForGroup(int id)
        {
            if (context.MatterTypes.Count(mt => mt.MatterTypeGroupId == id) == 1)
                return context.MatterTypes.First(mt => mt.MatterTypeGroupId == id).MatterTypeName;
            else
                return null;
        }

        public IEnumerable<XmlMatterDetail> GetXmlMatterDetails(bool useWFComponent, int id)
        {
            if (useWFComponent)
            {
                return context.MatterWFXMLs.AsNoTracking().Where(mx => mx.MatterWFComponentId == id)
                    .Select(s2 => new
                    {
                        s2.LenderXML,
                        s2.SequenceNo,
                        s2.Filename
                    }
                    )
                    .OrderBy(o => o.SequenceNo).ToList()
                    .Select(s3 => new XmlMatterDetail
                    {
                        FileName = s3.Filename,
                        SequenceNo = s3.SequenceNo,
                        LenderXml = s3.LenderXML
                    }).ToList();
            }
            else
            {
                return context.MatterXMLs.AsNoTracking().Where(mx => mx.MatterId == id)
                    .Select(s2 => new
                    {
                        s2.LenderXML,
                        s2.SequenceNo,
                        s2.Filename
                    }
                    )
                    .OrderBy(o => o.SequenceNo).ToList()
                    .Select(s3 => new XmlMatterDetail
                    {
                        FileName = s3.Filename,
                        SequenceNo = s3.SequenceNo,
                        LenderXml = s3.LenderXML
                    }).ToList()
                    .OrderBy(o => o.SequenceNo);
            }
        }

        public IEnumerable<MatterCustomEntities.MatterWFProcDocsDocumentsView> GetOtherPrecedents(int? lenderId)
        {
            IEnumerable<Precedent> precDocs = context.Precedents.Where(x => x.IsAdhocAllowed);
            if (lenderId.HasValue)
                precDocs = precDocs.Where(m => m.LenderId == lenderId.Value || m.LenderId == null);

            return precDocs
                .Select(s => new
                {
                    s.HotDocsId,
                    s.PrecedentId,
                    s.DocName,
                    s.DocType,
                    s.IsPublicVisible,
                    s.TemplateFile,
                    s.AssemblySwitches,
                    s.Lender?.LenderName,
                    s.MatterType?.MatterTypeName,
                    s.MortMgr?.MortMgrName,
                    s.State?.StateName,
                    s.PrecedentTypeId
                }).ToList()
               .Select(s2 => new MatterCustomEntities.MatterWFProcDocsDocumentsView
               {
                   PrecedentId = s2.PrecedentId,
                   IsPublic = s2.IsPublicVisible,
                   HotDocsId = s2.HotDocsId,
                   Lender = s2.LenderName ?? DomainConstants.AnySelection,
                   LenderSortVal = s2.LenderName ?? "zzzWillGoLast",
                   MatterType = s2.MatterTypeName ?? DomainConstants.AnySelection,
                   MortMgr = s2.MortMgrName ?? DomainConstants.AnySelection,
                   State = s2.StateName ?? DomainConstants.AnySelection,
                   DocName = s2.DocName,
                   FileName = s2.DocName.ToSafeFileName(),
                   DocType = s2.DocType,
                   TemplateFile = s2.TemplateFile,
                   HotDocsSwitches = s2.AssemblySwitches,
                   FromHotDocs = false,
                   IsExtractedDocument = false,
                   PrecedentTypeId = s2.PrecedentTypeId
               })
               .OrderBy(x => x.LenderSortVal).ThenBy(x => x.DocName)
               .ToList();

        }

        public IEnumerable<MCE.MatterWFProcDocsDocumentsView> GetPrecedents(List<PrecedentView> precedents)
        {
            var docIds = precedents.Select(x => x.HotDocsId).ToArray();
            var docsToExtract =
                 (from p in context.Precedents
                  join h in docIds on p.HotDocsId equals h
                  select new
                  {
                      p.HotDocsId,
                      p.PrecedentId,
                      p.IsPublicVisible,
                      p.DocName,
                      p.DocType,
                      p.TemplateFile,
                      HotDocsSwitches = p.AssemblySwitches,
                      IsGenerated = false,
                      FromHotDocs = true,
                      IsToBeGenerated = true,
                      IsExtractedDocument = true,
                      IsToBeExtracted = true,
                      p.PrecedentTypeId
                  })
                  .ToList();

            var newDocs = new List<MCE.MatterWFProcDocsDocumentsView>();


            foreach (var p in precedents.Where(p => !string.IsNullOrEmpty(p.HotDocsId)))
            {


                var d = docsToExtract.FirstOrDefault(x => x.HotDocsId == p.HotDocsId);
                if (d != null)
                {
                    try
                    {


                        newDocs.Add(new MCE.MatterWFProcDocsDocumentsView
                        {
                            HotDocsId = d.HotDocsId,
                            PrecedentId = d.PrecedentId,
                            IsPublic = d.IsPublicVisible,
                            DocName = p.HotDocsDesc,
                            FileName = p.HotDocsDesc.ToSafeFileName(),
                            DocType = d.DocType,
                            TemplateFile = d.TemplateFile,
                            HotDocsSwitches = d.HotDocsSwitches == null ? "" : d.HotDocsSwitches + (!p.InterviewRequired ? d.HotDocsSwitches.Contains(DomainConstants.HotDocsNoAssemblySwitches) ? "" : DomainConstants.HotDocsNoAssemblySwitches : ""),
                            IsGenerated = false,
                            FromHotDocs = true,
                            IsToBeGenerated = true,
                            IsExtractedDocument = true,
                            IsToBeExtracted = true,
                            PrecedentTypeId = d.PrecedentTypeId,
                            IsInterviewRequired = p.InterviewRequired,
                            ParameterName = p.HotDocsParameterName,
                            ParameterValue = p.HotDocsParameterValue
                        });
                    }
                    catch (NullReferenceException e)
                    {
                        throw new PrecedentMissingException($"Precedent {p.HotDocsId} - {d.PrecedentId} is missing required fields - Please Check and add as Required. Message {e.Message}");
                    }

                }
                else
                    throw new PrecedentMissingException($"Precedent {p.HotDocsId} is missing - Please Check and add as Required");
            }

            return newDocs;
        }

        public IEnumerable<MCE.MatterWFProcDocsDocumentsView> GetPrecedentsForMatter(int matterWFComponentId, int section = 0)
        {
            var precedents =
                 (from pd in context.MatterWFDocuments
                  join p in context.Precedents on pd.PrecedentId equals p.PrecedentId
                  join dv in context.Documents on pd.DocumentId equals dv.DocumentId into joinedDV
                  from dv2 in joinedDV.DefaultIfEmpty()
                  join dm in context.DocumentMasters on dv2.DocumentMasterId equals dm.DocumentMasterId into joinedDM
                  from dm2 in joinedDM.DefaultIfEmpty()
                  where pd.MatterWFComponentId == matterWFComponentId && pd.SectionNo == section
                  select new MCE.MatterWFProcDocsDocumentsView
                  {
                      HotDocsId = p.HotDocsId,
                      PrecedentId = p.PrecedentId,
                      IsPublic = p.IsPublicVisible,
                      DocName = dm2.DocName ?? p.DocName,
                      FileName = (dm2.DocName ?? p.DocName),
                      DocType = p.DocType,
                      TemplateFile = p.TemplateFile,
                      HotDocsSwitches = p.AssemblySwitches,
                      IsGenerated = pd.DocumentId.HasValue,
                      DocumentMasterId = dv2.DocumentMasterId,
                      DocumentVersionId = pd.DocumentId,
                      GeneratedText = pd.DocumentId.HasValue ? "Doc Generated" : null,
                      GeneratedDate = pd.UpdatedDate,
                      ParameterName = pd.HotDocsParamName,
                      ParameterValueString = pd.HotDocsParamValue,
                      IsToBeGenerated = false,
                      FromHotDocs = pd.FromHotDocs,
                      DocumentSource = pd.FromHotDocs ? DomainConstants.FromSourceSlick : DomainConstants.FromSourceManual,
                      IsExtractedDocument = true,
                      SectionNo = section,
                      PrecedentTypeId = p.PrecedentTypeId
                  })
                  .ToList();

            foreach (var p in precedents)
            {
                p.ParameterValue = int.Parse(p.ParameterValue.ToString());
                p.FileName = p.FileName.ToSafeFileName();
            }

            return precedents;
        }



        public IEnumerable<WFCustomEntities.WFComponentPrecedentSeedView> GetPrecedentSeedsForWFComponent(List<WFComponentPrecedentSeed> precedents, Func<WFComponentPrecedentSeed, bool> whereClause)
        {
            return precedents.Where(whereClause)
                .Select(p => new
                {
                    p.WFComponentPrecedentSeedId,
                    p.WFComponentId,
                    p.SectionNo,
                    p.MatterTypeId,
                    p.LenderId,
                    p.MortMgrId,
                    p.StateId,
                    p.PrecedentId,
                    p.Precedent.Description,
                    p.InterviewRequired,
                    p.Precedent.AssemblySwitches,
                    p.Precedent.TemplateFile,
                    p.Precedent.DocName,
                    p.Precedent.DocType,
                    p.Precedent.HotDocsId,
                    p.DisplayOrder,
                    p.UpdatedDate,
                    p.UpdatedByUserId,
                    p.Precedent.PrecedentTypeId,
                    p.Precedent.IsPublicVisible
                })
                .OrderBy(o => o.DisplayOrder)
                .ToList()
                .Select(p => new WFCustomEntities.WFComponentPrecedentSeedView(p.WFComponentPrecedentSeedId, p.WFComponentId, p.SectionNo, p.MatterTypeId, p.LenderId, p.MortMgrId, p.StateId, p.PrecedentId,
                                    p.Description, p.InterviewRequired, p.AssemblySwitches, p.TemplateFile, p.DocName, p.DocType, p.DisplayOrder, p.UpdatedDate, p.UpdatedByUserId, p.HotDocsId, p.PrecedentTypeId, p.IsPublicVisible))
                                    .ToList();

        }
        public IEnumerable<WFCustomEntities.WFComponentPrecedentSeedView> GetPrecedentSeedsForWFComponent(int wfComponentId, int? matterTypeId, int? lenderId, int? mortMgrId, int? stateId)
        {
            var precedents = context.WFComponentPrecedentSeeds.Where(w => w.WFComponentId == wfComponentId && w.Precedent.PrecedentStatusTypeId != (int)Enums.PrecedentStatusTypeEnum.Obsolete).ToList();
            Func<WFComponentPrecedentSeed, bool> whereClause;

            //Now find the best match

            //First try everything
            whereClause = (p => p.MatterTypeId == matterTypeId && p.MortMgrId == mortMgrId && p.LenderId == lenderId && p.StateId == stateId);
            if (precedents.Count(whereClause) > 0)
                return GetPrecedentSeedsForWFComponent(precedents, whereClause);

            //Matter Type, MM, Lender
            whereClause = (p => p.MatterTypeId == matterTypeId && p.MortMgrId == mortMgrId && p.LenderId == lenderId && p.StateId == null);
            if (precedents.Count(whereClause) > 0)
                return GetPrecedentSeedsForWFComponent(precedents, whereClause);

            //Matter Type, MM, State
            whereClause = (p => p.MatterTypeId == matterTypeId && p.MortMgrId == mortMgrId && p.LenderId == null && p.StateId == stateId);
            if (precedents.Count(whereClause) > 0)
                return GetPrecedentSeedsForWFComponent(precedents, whereClause);

            //Matter Type, Lender, State
            whereClause = (p => p.MatterTypeId == matterTypeId && p.MortMgrId == null && p.LenderId == lenderId && p.StateId == stateId);
            if (precedents.Count(whereClause) > 0)
                return GetPrecedentSeedsForWFComponent(precedents, whereClause);

            whereClause = (p => p.MatterTypeId == matterTypeId && p.MortMgrId == null && p.LenderId == lenderId && p.StateId == null);
            if (precedents.Count(whereClause) > 0)
                return GetPrecedentSeedsForWFComponent(precedents, whereClause);

            //MM, Lender, State
            whereClause = (p => p.MatterTypeId == null && p.MortMgrId == mortMgrId && p.LenderId == lenderId && p.StateId == stateId);
            if (precedents.Count(whereClause) > 0)
                return GetPrecedentSeedsForWFComponent(precedents, whereClause);

            //MM, Lender
            whereClause = (p => p.MatterTypeId == null && p.MortMgrId == mortMgrId && p.LenderId == lenderId && p.StateId == null);
            if (precedents.Count(whereClause) > 0)
                return GetPrecedentSeedsForWFComponent(precedents, whereClause);

            //MM, State
            whereClause = (p => p.MatterTypeId == null && p.MortMgrId == mortMgrId && p.LenderId == null && p.StateId == stateId);
            if (precedents.Count(whereClause) > 0)
                return GetPrecedentSeedsForWFComponent(precedents, whereClause);

            //Lender, State
            whereClause = (p => p.MatterTypeId == null && p.MortMgrId == null && p.LenderId == lenderId && p.StateId == stateId);
            if (precedents.Count(whereClause) > 0)
                return GetPrecedentSeedsForWFComponent(precedents, whereClause);

            //Matter Type Only
            whereClause = (p => p.MatterTypeId == matterTypeId && p.MortMgrId == null && p.LenderId == null && p.StateId == null);
            if (precedents.Count(whereClause) > 0)
                return GetPrecedentSeedsForWFComponent(precedents, whereClause);

            //MM Only
            whereClause = (p => p.MatterTypeId == null && p.MortMgrId == mortMgrId && p.LenderId == null && p.StateId == null);
            if (precedents.Count(whereClause) > 0)
                return GetPrecedentSeedsForWFComponent(precedents, whereClause);

            //Lender Only
            whereClause = (p => p.MatterTypeId == null && p.MortMgrId == null && p.LenderId == lenderId && p.StateId == null);
            if (precedents.Count(whereClause) > 0)
                return GetPrecedentSeedsForWFComponent(precedents, whereClause);

            //State Only
            whereClause = (p => p.MatterTypeId == null && p.MortMgrId == null && p.LenderId == null && p.StateId == stateId);
            if (precedents.Count(whereClause) > 0)
                return GetPrecedentSeedsForWFComponent(precedents, whereClause);

            //No criteria
            whereClause = (p => p.MatterTypeId == null && p.MortMgrId == null && p.LenderId == null && p.StateId == null);
            if (precedents.Count(whereClause) > 0)
                return GetPrecedentSeedsForWFComponent(precedents, whereClause);

            return null;

        }


        public WFCustomEntities.WFComponentView GetWFComponentView(int wfComponentId)
        {
            try
            {
                return context.WFComponents
                            .Where(c => c.WFComponentId == wfComponentId)
                            .Select(c => new { c.WFComponentId, c.WFComponentName, c.WFModuleId, c.WFModule.ModuleName, c.Enabled, c.SystemUseOnly, c.UpdatedDate, c.UpdatedByUserId, c.User.Username })
                            .ToList()
                            .Select(c => new WFCustomEntities.WFComponentView(c.WFComponentId, c.WFComponentName, c.WFModuleId, c.ModuleName, c.Enabled, c.SystemUseOnly, c.UpdatedDate, c.UpdatedByUserId, c.Username))
                            .FirstOrDefault();
            }
            catch (Exception ex)
            {
                Handlers.ErrorHandler.LogError(ex);
                return null;
            }

        }

        public IEnumerable<WFCustomEntities.WFComponentView> GetWFComponentsView()
        {
            try
            {
                return context.WFComponents
                             .Select(c => new { c.WFComponentId, c.WFComponentName, c.WFModuleId, c.WFModule.ModuleName, c.Enabled, c.SystemUseOnly, c.UpdatedDate, c.UpdatedByUserId, c.User.Username })
                             .ToList()
                             .Select(c => new WFCustomEntities.WFComponentView(c.WFComponentId, c.WFComponentName, c.WFModuleId, c.ModuleName, c.Enabled, c.SystemUseOnly, c.UpdatedDate, c.UpdatedByUserId, c.Username))
                             .ToList();
            }
            catch (Exception ex)
            {
                Handlers.ErrorHandler.LogError(ex);
                return null;
            }

        }



        public void RestartWorkflowAtMilestone(MCE.MatterWFComponentView component, int WFComponentIdRestart, int allocatedUserId = 0)
        {
            if (component.AccountsStageId == (int)AccountsStageEnum.ReadyForAccounts && context.WFComponents.FirstOrDefault(w => w.WFComponentId == WFComponentIdRestart).AccountsStageId == (int)AccountsStageEnum.NotReadyForAccounts)
            {
                foreach (var warning in context.MatterWarningEmails.Where(m => m.MatterId == component.MatterId && m.WarningEmailTypeId == (int)WarningEmailTypeEnum.SettlementAtRisk))
                {
                    warning.Overriden = true;
                }
            }

            context.SaveChanges();
            //RemoveComponentsWithoutEventsFromFirstActive(component.MatterId, false, "Milestone removed due to Re-Start");
            RemoveComponentsWithoutEventsFrom(component, false, "Milestone removed due to Re-Start", true);
            context.SaveChanges();


            List<int> templateIds = new List<int>();
            var securities = context.MatterSecurities.Where(m => m.MatterId == component.MatterId && !m.Deleted);
            var mtDetails = context.Matters.Select(x => new { x.MatterId, x.LoanTypeId, IsSelfActing = x.MatterDischarge != null ? x.MatterDischarge.IsSelfActing : (bool?)null }).FirstOrDefault(m => m.MatterId == component.MatterId);
            int? loanTypeId = mtDetails.LoanTypeId;

            if (securities != null && securities.Any())
            {
                foreach (var matSecs in securities)
                {
                    int? wfTemplateID = GetBestWorkFlowTemplateIdForMatter(matSecs.MatterTypeId, component.StateId, component.LenderId, component.MortMgrId, loanTypeId, mtDetails.IsSelfActing);
                    if (wfTemplateID.HasValue && !templateIds.Any(t => t == wfTemplateID.Value))
                        templateIds.Add(wfTemplateID.Value);
                }
            }
            else
            {
                var templateId = GetBestWorkFlowTemplateIdForMatter(component.MatterTypeGroupId, component.StateId, component.LenderId, component.MortMgrId, loanTypeId, mtDetails.IsSelfActing);
                if (templateId.HasValue)
                {
                    templateIds.Add(templateId.Value);
                }
            }

            MergeMatterWorkflow(component.MatterId, component.MatterWFComponentId, templateIds, WFComponentIdRestart, overrideInactive: true, allocatedUserId: allocatedUserId);
            context.SaveChanges();
            DeleteInactiveComponents(component.MatterId, component.DisplayOrder, true);
            context.SaveChanges();
        }

        //public void MergeMatterWorkflow(int matterId, int componentId, int templateId, bool isTemplateGroup)
        //{
        //    var sqlParam1 = new SqlParameter("@matterId", System.Data.SqlDbType.Int);
        //    var sqlParam2 = new SqlParameter("@matterWFComponentIdStart", System.Data.SqlDbType.Int);
        //    var sqlParam3 = new SqlParameter("@wfTemplateId", System.Data.SqlDbType.Int);
        //    var sqlParam4 = new SqlParameter("@userId", System.Data.SqlDbType.Int);
        //    sqlParam1.Value = matterId;
        //    sqlParam2.Value = componentId;
        //    sqlParam3.Value = templateId;
        //    sqlParam4.Value = Slick_Domain.Common.DomainConstants.SystemUserId;
        //    object[] sqlParams = new object[] { sqlParam1, sqlParam2, sqlParam3, sqlParam4 };

        //    context.Database.ExecuteSqlCommand("spMergeMatterWorkFlow @matterId,@matterWFComponentIdStart, @wfTemplateId,@userId", sqlParams);

        //}

        //public void RemoveComponentsWithoutEventsFromFirstActive(int matterId, bool inclStartingComponent, string eventNotes)
        //{
        //    var mwfComp = GetFirstActiveComponentForMatter(matterId);
        //    if (mwfComp != null)
        //    {
        //        RemoveComponentsWithoutEventsFrom(matterId, mwfComp.MatterWFComponentId, mwfComp.DisplayOrder, inclStartingComponent, eventNotes);
        //    }
        //}

        /// <summary>
        /// Removes components that don't have any events - Other Rows should be deleted in the corresponding UndoMilestone event instead.
        /// </summary>
        /// <param name="matterId"></param>
        /// <param name="mwfCompId"></param>
        /// <param name="displayOrder"></param>
        /// <param name="inclStartingComponent"></param>
        /// <param name="eventNotes"></param>
        public void RemoveComponentsWithoutEventsFrom(MCE.MatterWFComponentView mwfComponentView, bool inclStartingComponent, string eventNotes, bool softDeleteWhenHasEvents = false)
        {
            var mwfComps =
                context.MatterWFComponents.Where(m => m.MatterId == mwfComponentView.MatterId && m.WFComponentStatusTypeId != (int)Enums.MatterWFComponentStatusTypeEnum.Deleted)
                .Select(m=>new { m.MatterWFComponentId, m.DisplayOrder, m.WFComponentId }).ToList();

            if (inclStartingComponent)
            {
                var mwfCompsFiltered = mwfComps.Where(m => m.DisplayOrder >= mwfComponentView.DisplayOrder && m.WFComponentId != (int)WFComponentEnum.PrintAndCollateFile && m.WFComponentId != (int)WFComponentEnum.CreatePSTPacket && m.WFComponentId != (int)WFComponentEnum.IDYouCompleted && m.WFComponentId != (int)WFComponentEnum.IDYouReportApproved && m.WFComponentId != (int)WFComponentEnum.IDYouInvitation && m.WFComponentId != (int)WFComponentEnum.IDYouReportSent && m.WFComponentId != (int)WFComponentEnum.ReworkReview && m.WFComponentId != (int)WFComponentEnum.Requisition && m.WFComponentId != (int)WFComponentEnum.FileManagerAcknowledgement).OrderBy(o => o.DisplayOrder).ToList();
                DeleteComponents(mwfComponentView.MatterId, mwfCompsFiltered.Select(m=>m.MatterWFComponentId), softDeleteWhenHasEvents);
            }
            else
            {
                var mwfCompsFiltered = mwfComps.Where(m => m.DisplayOrder > mwfComponentView.DisplayOrder && m.WFComponentId != (int)WFComponentEnum.PrintAndCollateFile && m.WFComponentId != (int)WFComponentEnum.CreatePSTPacket && m.WFComponentId != (int)WFComponentEnum.IDYouReportApproved && m.WFComponentId != (int)WFComponentEnum.IDYouCompleted && m.WFComponentId != (int)WFComponentEnum.IDYouInvitation && m.WFComponentId != (int)WFComponentEnum.IDYouReportSent && m.WFComponentId != (int)WFComponentEnum.ReworkReview && m.WFComponentId != (int)WFComponentEnum.Requisition && m.WFComponentId != (int)WFComponentEnum.FileManagerAcknowledgement).OrderBy(o => o.DisplayOrder).ToList();
                DeleteComponents(mwfComponentView.MatterId, mwfCompsFiltered.Select(m => m.MatterWFComponentId), softDeleteWhenHasEvents);
            }

        }

        public void SaveMatterWFComponentDocumentState(int matterId, int matterWfComponentId)
        {
            context.MatterWFComponentDocuments.RemoveRange(context.MatterWFComponentDocuments.Where(d => d.MatterWFComponentId == matterWfComponentId));
            var dRep = new DocumentsRepository(context);
            var docPack = dRep.GetAllDocumentsForMatter(matterId);
            foreach (var doc in docPack.Where(d => d.DocumentMasterId.HasValue && d.MatterDocumentId.HasValue))
            {
                context.MatterWFComponentDocuments.Add(new MatterWFComponentDocument()
                {
                    MatterWFComponentId = matterWfComponentId,
                    DocumentId = doc.DocumentId,
                    DocumentMasterId = doc.DocumentMasterId.Value,
                    MatterDocumentId = doc.MatterDocumentId.Value,
                });
            }

        }

        //private void DeleteComponents(int matterId, IEnumerable<MatterWFComponent> mwfComps, bool softDeleteWhenHasEvents)
        //{
        //    context.SaveChanges();
        //    //var matterEvents = context.MatterEvents.Where(x => x.MatterId == matterId).Select(m=>new { m.MatterWFComponentId }).ToList();
        //    //var comps = mwfComps;
        //    foreach (var mwfComp in mwfComps.ToList())
        //    {
        //        try
        //        {
        //            if (mwfComp.MatterWFSettlementTimeChanges?.Any(m => m.MatterWFComponentId == mwfComp.MatterWFComponentId) == true
        //                || mwfComp.ReissueReasons?.Any(m=>m.MatterWFComponentId == mwfComp.MatterWFComponentId) == true
        //                || mwfComp.BackChannelLogs?.Any(x=>x.MatterWFComponentId == mwfComp.MatterWFComponentId) == true
        //                || mwfComp.MatterWFLetterToCustodians?.Any(x=>x.MatterWFComponentId == mwfComp.MatterWFComponentId) == true
        //                || mwfComp.MatterWFXMLs?.Any(x=>x.MatterWFComponentId == mwfComp.MatterWFComponentId) == true
        //                || mwfComp.MatterWFDocuments?.Any(x=>x.MatterWFComponentId == mwfComp.MatterWFComponentId) == true
        //                || mwfComp.MatterWFIssues?.Any(x=>x.RaisedMatterWFComponentId == mwfComp.MatterWFComponentId || x.HistoricalMatterWFComponentId == mwfComp.MatterWFComponentId || x.DisplayedWFComponentId == mwfComp.MatterWFComponentId || x.RestartWFComponentId == mwfComp.MatterWFComponentId || x.DisplayedMatterWFComponentId == mwfComp.MatterWFComponentId) == true
        //                || mwfComp.MatterWFIssues1?.Any(x => x.RaisedMatterWFComponentId == mwfComp.MatterWFComponentId || x.HistoricalMatterWFComponentId == mwfComp.MatterWFComponentId || x.DisplayedWFComponentId == mwfComp.MatterWFComponentId || x.RestartWFComponentId == mwfComp.MatterWFComponentId || x.DisplayedMatterWFComponentId == mwfComp.MatterWFComponentId) == true
        //                || mwfComp.MatterWFIssues2?.Any(x => x.RaisedMatterWFComponentId == mwfComp.MatterWFComponentId || x.HistoricalMatterWFComponentId == mwfComp.MatterWFComponentId || x.DisplayedWFComponentId == mwfComp.MatterWFComponentId || x.RestartWFComponentId == mwfComp.MatterWFComponentId || x.DisplayedMatterWFComponentId == mwfComp.MatterWFComponentId) == true
        //                || mwfComp.MatterWFFollowUps?.Any() == true
        //                || mwfComp.MatterWFLedgerItems?.Any() == true
        //                || mwfComp.MatterWFSmartVidDetails?.Any(x=>x.MatterWFComponentId ==  mwfComp.MatterWFComponentId) == true
        //                || mwfComp.MatterWFCreateMatters?.Any(x=>x.MatterWFComponentId == mwfComp.MatterWFComponentId) == true)
        //            {
        //                if (softDeleteWhenHasEvents)
        //                {
        //                    SoftDeleteComponent(mwfComp);
        //                }
        //                continue;
        //            }
        //            if (mwfComp.MatterEvents?.Any(x => x.MatterWFComponentId == mwfComp.MatterWFComponentId) == true
        //                || mwfComp.MatterWFAnswer != null)
        //            {

        //                if (softDeleteWhenHasEvents)
        //                {
        //                    SoftDeleteComponent(mwfComp);
        //                }
        //                continue;
        //            }

        //            var deps = context.MatterWFComponentDependents.Where(m => m.MatterWFComponentId == mwfComp.MatterWFComponentId);
        //            context.MatterWFComponentDependents.RemoveRange(deps);
        //            context.SaveChanges();

        //            deps = context.MatterWFComponentDependents.Where(m => m.DependentMatterWFComponentId == mwfComp.MatterWFComponentId);
        //            context.MatterWFComponentDependents.RemoveRange(deps);
        //            context.SaveChanges();

        //            //delete backchannel logs if any
        //            var bcToDelete = context.BackChannelLogs.Where(x => x.MatterWFComponentId == mwfComp.MatterWFComponentId);
        //            context.BackChannelLogs.RemoveRange(bcToDelete);
        //            context.SaveChanges();

        //            context.MatterWFComponents.Remove(mwfComp);
        //            context.SaveChanges();

        //        }
        //        catch (Exception ex)
        //        {
        //            if (ex.Message != null && ex.Message.Contains("DELETE statement conflicted with the REFERENCE") && softDeleteWhenHasEvents)
        //            {
        //                SoftDeleteComponent(mwfComp);
        //                continue;
        //            }
        //            else
        //            {
        //                // This should not Error as there are no Events
        //                SoftDeleteComponent(mwfComp);

        //                Handlers.ErrorHandler.LogError(ex, $"{mwfComp.MatterWFComponentId} - Removing MWF");
        //                //throw new Exception($"{mwfComp.MatterWFComponentId} - Removing MWF - Should have No Events - Soft Delete issue - {ex.Message}");
        //            }
        //        }
        //    }

        //    context.SaveChanges();
        //}

        private void DeleteComponents(int matterId, IEnumerable<int> mwfComps, bool softDeleteWhenHasEvents)
        {
            context.SaveChanges();
            var matterEvents = context.MatterEvents.Where(x => x.MatterId == matterId).ToList();
            foreach (var mwfComp in mwfComps.ToList())
            {
                try
                {
                    if (context.MatterWFSettlementTimeChanges.Any(m => m.MatterWFComponentId == mwfComp)
                        || context.ReissueReasons.Any(m => m.MatterWFComponentId == mwfComp)
                        || context.BackChannelLogs.Any(x => x.MatterWFComponentId == mwfComp)
                        || context.MatterWFLetterToCustodians.Any(x => x.MatterWFComponentId == mwfComp)
                        || context.MatterWFXMLs.Any(x => x.MatterWFComponentId == mwfComp)
                        || context.MatterWFComponentDocuments.Any(x => x.MatterWFComponentId == mwfComp)
                        || context.MatterWFIssues.Any(x => x.RaisedMatterWFComponentId == mwfComp || x.HistoricalMatterWFComponentId == mwfComp || x.DisplayedWFComponentId == mwfComp || x.RestartWFComponentId == mwfComp || x.DisplayedMatterWFComponentId == mwfComp)
                        || context.MatterWFSmartVidDetails.Any(x => x.MatterWFComponentId == mwfComp)
                        || context.MatterWFCreateMatters.Any(x => x.MatterWFComponentId == mwfComp))
                    {
                        if (softDeleteWhenHasEvents)
                        {
                            SoftDeleteComponent(mwfComp);
                        }
                        continue;
                    }
                    if (matterEvents != null && matterEvents.Any(x => x.MatterWFComponentId == mwfComp)
                        || context.MatterWFAnswers.AsNoTracking().Where(x => x.MatterWFComponentId == mwfComp).Any())
                    {

                        if (softDeleteWhenHasEvents)
                        {
                            SoftDeleteComponent(mwfComp);
                        }
                        continue;
                    }

                    context.SaveChanges();

                    //context.MatterWFComponentDependents.RemoveRange(context.MatterWFComponentDependents.Where(m => m.MatterWFComponentId == mwfComp.MatterWFComponentId));

                    context.MatterWFComponentDependents.RemoveRange(context.MatterWFComponentDependents.Where(m => m.MatterWFComponentId == mwfComp || m.DependentMatterWFComponentId == mwfComp));
                    context.SaveChanges();

                    //delete backchannel logs if any
                    var bcToDelete = context.BackChannelLogs.Where(x => x.MatterWFComponentId == mwfComp);
                    context.BackChannelLogs.RemoveRange(bcToDelete);
                    context.SaveChanges();
                    //context.MatterWFComponents.Remove(mwfComp);

                    SoftDeleteComponent(mwfComp);

                    context.SaveChanges();


                }
                catch (Exception ex)
                {
                    if (ex.Message != null && ex.Message.Contains("DELETE statement conflicted with the REFERENCE") && softDeleteWhenHasEvents)
                    {
                        SoftDeleteComponent(mwfComp);
                        continue;
                    }
                    else
                    {
                        // This should not Error as there are no Events
                        throw new Exception($"{mwfComp} - Removing MWF - Should have No Events - Soft Delete issue - {ex.Message}");
                    }
                }
            }
            //var added = context.ChangeTracker.Entries().Where(e => e.State == System.Data.Entity.EntityState.Added);
            //var deleted = context.ChangeTracker.Entries().Where(e => e.State == System.Data.Entity.EntityState.Deleted);
            //var modified = context.ChangeTracker.Entries().Where(e => e.State == System.Data.Entity.EntityState.Modified);

            context.SaveChanges();
        }


        /// <summary>
        /// Deletes Inactive and Inactive never shown components so Workflow milestones are cleaned up due to Pexa etc. on Restarts
        /// - As per - RemoveComponentsWithoutEventsFrom
        /// </summary>
        /// <param name="matterId"></param>
        /// <param name="mwfComps"></param>
        private bool DeleteInactiveComponents(int matterId, int displayOrder, bool softDelete)
        {
            IQueryable<MatterWFComponent> mwfComps =
               context.MatterWFComponents.Where(m => m.MatterId == matterId && m.WFComponentStatusTypeId != (int)MatterWFComponentStatusTypeEnum.Deleted);

            var inactiveComponents = mwfComps.Where(m => m.MatterId == matterId && m.DisplayOrder < displayOrder &&
                                             (m.DisplayStatusTypeId == ((int)DisplayStatusTypeEnum.Inactive) || m.DisplayStatusTypeId == (int)DisplayStatusTypeEnum.InactiveNeverShown) &&
                                             m.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.NotStarted).
                                             OrderBy(x => x.DisplayOrder).ToList();

            if (inactiveComponents == null || !inactiveComponents.Any()) return false;

            List<Tuple<int,int>> dupInactives = new List<Tuple<int, int>>();
            var dupItems = mwfComps.ToList().OrderBy(x => x.DisplayOrder).GroupBy(x => new { x.WFComponentId }).Where(x => x.Skip(1).Any()).ToList();

            foreach (var dup in dupItems)
            {
                var dupes = inactiveComponents.Where(x => x.WFComponentId == dup.Key.WFComponentId).Select(x => new Tuple<int, int>(x.MatterWFComponentId, x.DisplayOrder));
                dupInactives.AddRange(dupes);
            }

            if (dupInactives.Any())
            {
                var firstInactiveDisplayOrder = dupInactives.First().Item2;
                if (firstInactiveDisplayOrder == 0) firstInactiveDisplayOrder = 1;
                DeleteComponents(matterId, dupInactives.Select(i=>i.Item1).ToList(), softDelete);
                FixMilestoneDisplayOrder(matterId, firstInactiveDisplayOrder);
                return true;
            }

            return false;
        }


        //private void RemoveCurrentComponentAndEvents(MatterWFComponent mwfComp)
        //{
        //    var deps = context.MatterWFComponentDependents.Where(m => m.MatterWFComponentId == mwfComp.MatterWFComponentId);
        //    context.MatterWFComponentDependents.RemoveRange(deps);
        //    deps = context.MatterWFComponentDependents.Where(m => m.DependentMatterWFComponentId == mwfComp.MatterWFComponentId);
        //    context.MatterWFComponentDependents.RemoveRange(deps);
        //    var mwEvs = context.MatterEvents.Where(m => m.MatterWFComponentId == mwfComp.MatterWFComponentId);
        //    context.MatterEvents.RemoveRange(mwEvs);
        //    context.MatterWFComponents.Remove(mwfComp);
        //    context.SaveChanges();
        //}

        //private void RemoveSQLDependencies(MatterWFComponent mwfComp)
        //{
        //    var mwfCertify = context.MatterWFCertifies.FirstOrDefault(m => m.MatterWFComponentId == mwfComp.MatterWFComponentId);
        //    if (mwfCertify != null)
        //        context.MatterWFCertifies.Remove(mwfCertify);

        //    var mwfCreateMatter = context.MatterWFCreateMatters.FirstOrDefault(m => m.MatterWFComponentId == mwfComp.MatterWFComponentId);
        //    if (mwfCreateMatter != null)
        //        context.MatterWFCreateMatters.Remove(mwfCreateMatter);

        //    var mwfDisburse = context.MatterWFDisburses.FirstOrDefault(m => m.MatterWFComponentId == mwfComp.MatterWFComponentId);
        //    if (mwfDisburse != null)
        //        context.MatterWFDisburses.Remove(mwfDisburse);

        //    var mwfDisburseLedgerItem = context.MatterWFDisburseLedgerItems.FirstOrDefault(m => m.MatterWFComponentId == mwfComp.MatterWFComponentId);
        //    if (mwfDisburseLedgerItem != null)
        //        context.MatterWFDisburseLedgerItems.Remove(mwfDisburseLedgerItem);

        //    var mwfDisburseTrustItem = context.MatterWFDisburseTrustItems.FirstOrDefault(m => m.MatterWFComponentId == mwfComp.MatterWFComponentId);
        //    if (mwfDisburseTrustItem != null)
        //        context.MatterWFDisburseTrustItems.Remove(mwfDisburseTrustItem);

        //    var mwfDocument = context.MatterWFDocuments.FirstOrDefault(m => m.MatterWFComponentId == mwfComp.MatterWFComponentId);
        //    if (mwfDocument != null)
        //        context.MatterWFDocuments.Remove(mwfDocument);

        //    DeleteFollowupDependencies(mwfComp.MatterWFComponentId);

        //    context.MatterWFIssues.RemoveRange(context.MatterWFIssues.Where(m => m.CreatedMatterWFComponentId == mwfComp.MatterWFComponentId));
        //    context.MatterWFIssues.RemoveRange(context.MatterWFIssues.Where(m => m.DisplayedMatterWFComponentId == mwfComp.MatterWFComponentId));
        //    context.MatterWFIssues.RemoveRange(context.MatterWFIssues.Where(m => m.HistoricalMatterWFComponentId == mwfComp.MatterWFComponentId));
        //    context.MatterWFIssueDetails.RemoveRange(context.MatterWFIssueDetails.Where(m => m.MatterWFComponentId == mwfComp.MatterWFComponentId));

        //    DeleteProcDocsDependencies(mwfComp.MatterWFComponentId);

        //    context.MatterWFPostSettlementInstructions.RemoveRange(context.MatterWFPostSettlementInstructions.Where(m => m.MatterWFComponentId == mwfComp.MatterWFComponentId));

        //    var mwfSettlementComplete = context.MatterWFSettlementCompletes.FirstOrDefault(m => m.MatterWFComponentId == mwfComp.MatterWFComponentId);
        //    if (mwfSettlementComplete != null)
        //        context.MatterWFSettlementCompletes.Remove(mwfSettlementComplete);

        //    var mwfSettlementSchedule = context.MatterWFSettlementSchedules.FirstOrDefault(m => m.MatterWFComponentId == mwfComp.MatterWFComponentId);
        //    if (mwfSettlementSchedule != null)
        //        context.MatterWFSettlementSchedules.Remove(mwfSettlementSchedule);

        //    var mwfSpecialTask = context.MatterWFSpecialTasks.FirstOrDefault(m => m.MatterWFComponentId == mwfComp.MatterWFComponentId);
        //    if (mwfSpecialTask != null)
        //        context.MatterWFSpecialTasks.Remove(mwfSpecialTask);

        //    var mwfVerifyWait = context.MatterWFVerifyWaits.FirstOrDefault(m => m.MatterWFComponentId == mwfComp.MatterWFComponentId);
        //    if (mwfVerifyWait != null)
        //    {
        //        var mwfVerifyWaitDoc = context.MatterWFVerifyWaitDocs.FirstOrDefault(m => m.MatterWFVerifyWaitId == mwfVerifyWait.MatterWFVerifyWaitId);
        //        if (mwfVerifyWaitDoc != null)
        //            context.MatterWFVerifyWaitDocs.Remove(mwfVerifyWaitDoc);

        //        var mwfVerifyWaitHistory = context.MatterWFVerifyWaitHistories.FirstOrDefault(m => m.MatterWFVerifyWaitId == mwfVerifyWait.MatterWFVerifyWaitId);
        //        if (mwfVerifyWaitHistory != null)
        //        {
        //            var mwfVerifyWaitHistoryDoc = context.MatterWFVerifyWaitHistoryDocs.FirstOrDefault(m => m.MatterWFVerifyWaitHistoryId == mwfVerifyWaitHistory.MatterWFVerifyWaitHistoryId);
        //            if (mwfVerifyWaitHistoryDoc != null)
        //                context.MatterWFVerifyWaitHistoryDocs.Remove(mwfVerifyWaitHistoryDoc);

        //            context.MatterWFVerifyWaitHistories.Remove(mwfVerifyWaitHistory);
        //        }
        //        context.MatterWFVerifyWaits.Remove(mwfVerifyWait);
        //    }

        //    var mwfXML = context.MatterWFXMLs.FirstOrDefault(m => m.MatterWFComponentId == mwfComp.MatterWFComponentId);
        //    if (mwfXML != null)
        //        context.MatterWFXMLs.Remove(mwfXML);
        //}

        public void DeleteFollowupDependencies(int matterWFComponentId)
        {
            var mwfFollowUp = context.MatterWFFollowUps.FirstOrDefault(m => m.MatterWFComponentId == matterWFComponentId);
            if (mwfFollowUp != null)
            {
                var mwfFollowUpHistory = context.MatterWFFollowUpHistories.Where(m => m.MatterWFFollowupId == mwfFollowUp.MatterWFFollowUpId);
                context.MatterWFFollowUpHistories.RemoveRange(mwfFollowUpHistory);
                context.MatterWFFollowUps.Remove(mwfFollowUp);
            }

        }

        public void DeleteProcDocsDependencies(int matterWFComponentId)
        {
            context.MatterWFProcDocsLoanAccounts.RemoveRange(context.MatterWFProcDocsLoanAccounts.Where(m => m.MatterWFComponentId == matterWFComponentId));
            context.MatterWFProcDocsMatterTypes.RemoveRange(context.MatterWFProcDocsMatterTypes.Where(m => m.MatterWFComponentId == matterWFComponentId));
            context.MatterWFProcDocsParties.RemoveRange(context.MatterWFProcDocsParties.Where(m => m.MatterWFComponentId == matterWFComponentId));
            context.MatterWFProcDocsOtherParties.RemoveRange(context.MatterWFProcDocsOtherParties.Where(x => x.MatterWFComponentId == matterWFComponentId));

            var mwfProcDocsSecurity = context.MatterWFProcDocsSecurities.FirstOrDefault(m => m.MatterWFComponentId == matterWFComponentId);
            if (mwfProcDocsSecurity != null)
            {
                context.MatterWFProcDocsSecurityParties.RemoveRange(context.MatterWFProcDocsSecurityParties.Where(m => m.MatterWFProcDocsSecurityId == mwfProcDocsSecurity.MatterWFProcDocsSecurityId));
                context.MatterWFProcDocsSecurityTitleRefs.RemoveRange(context.MatterWFProcDocsSecurityTitleRefs.Where(m => m.MatterWFProcDocsSecurityId == mwfProcDocsSecurity.MatterWFProcDocsSecurityId));
                context.MatterWFSecurityDocuments.RemoveRange(context.MatterWFSecurityDocuments.Where(m => m.MatterWFSecurityId == mwfProcDocsSecurity.MatterWFProcDocsSecurityId));
                context.MatterWFSecurityWFPexaWorkspaces.RemoveRange(context.MatterWFSecurityWFPexaWorkspaces.Where(m => m.MatterWFSecurityId == mwfProcDocsSecurity.MatterWFProcDocsSecurityId));
                context.MatterWFProcDocsSecurities.Remove(mwfProcDocsSecurity);
            }

            var mwfSettleInstruct = context.MatterWFSettlementInstructions.FirstOrDefault(m => m.MatterWFComponentId == matterWFComponentId);
            if (mwfSettleInstruct != null) context.MatterWFSettlementInstructions.Remove(mwfSettleInstruct);

            var mwfProcDoc = context.MatterWFProcessDocs.FirstOrDefault(m => m.MatterWFComponentId == matterWFComponentId);
            if (mwfProcDoc != null) context.MatterWFProcessDocs.Remove(mwfProcDoc);
        }

        public void ChangeMatterFromFastRefiToRefi(int matterId, int matterWFCompViewId, int selReasonId, string reason)
        {
            MatterCustomEntities.MatterWFComponentView matterWFCompView = GetMatterWFComponentView(matterWFCompViewId);

            var fastRefiTypes = context.MatterMatterTypes.Where(m => m.MatterId == matterId && m.MatterTypeId == (int)MatterTypeEnum.FastRefinance);

            var fastRefiSetSched = context.Matters.FirstOrDefault(m => m.MatterId == matterId).SettlementSchedule1;
            if (fastRefiSetSched != null)
            {
                context.Matters.FirstOrDefault(m => m.MatterId == matterId).FastRefiSettlementScheduleId = null;
                context.SaveChanges();
            }

            foreach (var mtType in fastRefiTypes)
            {
                mtType.MatterTypeId = (int)MatterTypeEnum.Refinance;
            }

            context.SaveChanges();

            var fastRefiSecurities = context.MatterSecurities.Where(s => s.MatterId == matterId && s.MatterTypeId == (int)MatterTypeEnum.FastRefinance);

            foreach (var sec in fastRefiSecurities)
            {
                sec.MatterTypeId = (int)MatterTypeEnum.Refinance;
                context.SaveChanges();
            }

            RebuildMatterWorkflow(matterWFCompView);
            context.SaveChanges();

            var noteRep = new NotesRepository(context);
            noteRep.SaveFastRefiToRefiNote(matterId, selReasonId, reason);

            string reasonTextForNote = context.Reasons.FirstOrDefault(r => r.ReasonId == selReasonId).ReasonTxt;
            if (!String.IsNullOrEmpty(reason))
            {
                reasonTextForNote += " - " + reason;
            }

            context.MatterWFFastRefiToRefis.Add(new MatterWFFastRefiToRefi() { MatterWFComponentId = matterWFCompView.MatterWFComponentId, ReasonId = selReasonId, ReasonDetails = reason, ReversionDate = DateTime.UtcNow, RevertedByUserId = GlobalVars.CurrentUserId ?? Slick_Domain.Common.DomainConstants.SystemUserId });
            context.SaveChanges();


            AddMatterEvent(matterWFCompView, MatterEventTypeList.MatterTypeChanged, $"Matter changed from FAST REFINANCE to REFINANCE - Reason: {reasonTextForNote}");

            if (!GetMatterComponentsForMatter(matterId).Any(x => x.WFComponentId == (int)WFComponentEnum.ConfirmOutgoingLender && (x.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.NotStarted || x.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.InProgress)))
            {
                if (matterWFCompView.WFComponentId != (int)WFComponentEnum.ConfirmOutgoingLender)
                {
                    var insertedComp = ComponentInsert(matterWFCompView.MatterWFComponentId, (int)WFComponentEnum.ConfirmOutgoingLender, (int)Enums.MatterWFComponentStatusTypeEnum.InProgress, WFComponentAddEnum.Add, false, true);
                    insertedComp.TaskAllocUserId = context.Matters.FirstOrDefault(m => m.MatterId == matterId).FileOwnerUserId;
                    insertedComp.DueDate = DateTime.Now;
                    context.MatterWFComponentDependents.Add(new MatterWFComponentDependent() { MatterWFComponentId = insertedComp.MatterWFComponentId, DependentMatterWFComponentId = matterWFCompView.MatterWFComponentId });
                }
            }
        }
        public void RebuildMatterWorkflow(MatterCustomEntities.MatterWFComponentView matterWFCompView, bool forceIDYouWorkflow = false)
        {
            RemoveComponentsWithoutEventsFrom(matterWFCompView, inclStartingComponent: false, eventNotes: "Milestone removed due to a Process Docs action");

            var rep = new Slick_Domain.Services.Repository<MatterSecurity>(context);
            var matSecs = rep.AllAsQueryNoTracking.Where(m => m.MatterId == matterWFCompView.MatterId && !m.Deleted);
            var mtDetails = context.Matters.Select(x => new { x.MatterId, x.LoanTypeId, IsSelfActing = x.MatterDischarge != null ? x.MatterDischarge.IsSelfActing : (bool?)null }).FirstOrDefault(m => m.MatterId == matterWFCompView.MatterId);
            int? loanTypeId = mtDetails.LoanTypeId;

            if (!matSecs.Any())
            {
                matSecs = new List<MatterSecurity>() { new MatterSecurity() { MatterId = matterWFCompView.MatterId, MatterTypeId = GetMatterGroupDefaultMatterType(matterWFCompView.MatterTypeGroupId) } }.AsQueryable();
            }

            foreach (var matSec in matSecs)
            {
                bool currentMilestoneToBeRemoved = false;
                int? wfTemplateID = GetBestWorkFlowTemplateIdForMatter(matSec.MatterTypeId, matterWFCompView.StateId, matterWFCompView.LenderId, matterWFCompView.MortMgrId, loanTypeId, mtDetails.IsSelfActing);
                int dirtyCompId = 0;
                if (wfTemplateID.HasValue)
                {
                    var comps = context.WFTemplateComponents.Where(c => c.WFTemplateId == wfTemplateID);
                    var existingMatterComps = GetMatterComponentsForMatter(matterWFCompView.MatterId).Where(d => d.WFComponentStatusTypeId == (int)Enums.MatterWFComponentStatusTypeEnum.Complete && (d.DisplayStatusTypeId == (int)DisplayStatusTypeEnum.Display || d.DisplayStatusTypeId == (int)DisplayStatusTypeEnum.Default));
                    var matterCompsToHide = existingMatterComps
                        .Where(w => !comps.Any(x => x.WFComponentId == w.WFComponentId));

                    if (!comps.Any(c => c.WFComponentId == matterWFCompView.WFComponentId))
                    {
                        var dirtyComp = context.MatterWFComponents.FirstOrDefault(w => w.MatterWFComponentId == matterWFCompView.MatterWFComponentId);
                        dirtyComp.WFComponentStatusTypeId = (int)Enums.MatterWFComponentStatusTypeEnum.NotStarted;
                        dirtyComp.DisplayStatusTypeId = (int)Enums.DisplayStatusTypeEnum.Hide;
                        dirtyCompId = dirtyComp.MatterWFComponentId;

                        context.MatterWFComponentDependents.RemoveRange
                        (
                            context.MatterWFComponentDependents.Where(w => w.DependentMatterWFComponentId == dirtyCompId)
                        );

                        context.SaveChanges();

                        var completedComps = existingMatterComps.Where(w => comps.Any(c => c.WFComponentId == w.WFComponentId)).OrderByDescending(o => o.DisplayOrder);
                        //get the last completed milestone that will still exist in the new workflow
                        matterWFCompView = GetMatterWFComponentView(completedComps.FirstOrDefault().MatterWFComponentId);
                        currentMilestoneToBeRemoved = true;
                    }

                    foreach (var comp in matterCompsToHide)
                    {
                        comp.DisplayStatusTypeId = (int)Enums.DisplayStatusTypeEnum.Hide;
                    }


                    MergeMatterWorkflow(matterWFCompView.MatterId, matterWFCompView.MatterWFComponentId, true, wfTemplateID.Value, matterWFCompView.WFComponentId, forceIDYouWorkflow: forceIDYouWorkflow);
                    context.SaveChanges();
                    if (currentMilestoneToBeRemoved)
                    {
                        StartMatterWFComponent(GetNextMatterWFComponent(matterWFCompView).MatterWFComponentId);
                        context.SaveChanges();
                        MarkComponentAsNotStarted(dirtyCompId);
                        context.SaveChanges();
                    }


                }
            }
        }


        public int? FindDocPrepWFComponentInTemplate(int wfTemplateId)
        {
            return context.WFTemplateComponents.OrderByDescending(o => o.DisplayOrder).FirstOrDefault(w => w.WFTemplateId == wfTemplateId &&
                                  (w.WFComponent.WFModuleId == (int)WFModuleEnum.ProcessDocs
                                      || w.WFComponent.WFModuleId == (int)WFModuleEnum.WorkFlowTester))?.WFComponentId;
        }

        public int? FindComponentInTemplate(int wfTemplateId, WFModuleEnum wfModule)
        {
            return context.WFTemplateComponents.OrderByDescending(o => o.DisplayOrder).FirstOrDefault(w => w.WFTemplateId == wfTemplateId &&
                                  (w.WFComponent.WFModuleId == (int)wfModule))?.WFComponentId;
        }

        /// <summary>
        /// Builds 1 lot of Components List by merging any Templates - e.g. Purchase and Refinance Milestones
        /// </summary>
        /// <param name="wfTemplateIds"></param>
        /// <param name="addListWFComponentIdStart"></param>
        /// <returns></returns>
        private List<WFCustomEntities.WFBuildComponentList> BuildWFComponentList(List<int> wfTemplateIds, int? addListWFComponentIdStart, bool overrideInactive)
        {
            List<WFCustomEntities.WFBuildComponentList> retList = new List<WFCustomEntities.WFBuildComponentList>();

            var masterList = new List<WFCustomEntities.WFBuildComponentList>();


            foreach (var wfTemplateId in wfTemplateIds)
            {
                //Clear matches in masterList
                foreach (var cl in masterList.Where(m => m.IsMatched))
                    cl.IsMatched = false;


                var addList = context.WFTemplateComponents.Where(t => t.WFTemplateId == wfTemplateId).OrderBy(o => o.DisplayOrder)
                        .Select(t => new
                        {
                            t.WFComponentId,
                            t.DefTaskAllocTypeId,
                            t.DefTaskAllocWFComponentId,
                            t.DisplayOrder,
                            t.WFComponentDueDateFormulaId,
                            t.DueDateFormulaOffset,
                            t.WFComponentDueTimeHourOffset,
                            t.WFComponentDueTimeFormulaId,
                            t.IsInactive,
                            t.SettlementTypeId,
                            gg = t.WFTemplateComponentDependents.Select(d => d.DependentWFComponentId).ToList()
                        }
                             ).ToList()
                        .Select(t => new WFCustomEntities.WFBuildComponentList(t.WFComponentId, t.DisplayOrder, t.WFComponentDueDateFormulaId, t.DueDateFormulaOffset, t.WFComponentDueTimeHourOffset, t.WFComponentDueTimeFormulaId, t.DefTaskAllocTypeId, t.DefTaskAllocWFComponentId,
                         overrideInactive && t.WFComponentId == addListWFComponentIdStart ? false : t.IsInactive,
                        t.SettlementTypeId, t.gg)).ToList();

                // Match Same Components
                var ltmp = from m in masterList
                           join a in addList on m.WFComponentId equals a.WFComponentId
                           select m;

                foreach (var m in ltmp)
                    m.IsMatched = true;

                ltmp = from m in masterList
                       join a in addList on m.WFComponentId equals a.WFComponentId
                       select a;

                foreach (var m in ltmp)
                    m.IsMatched = true;


                int tmpOrderBy = 1;
                retList = new List<WFCustomEntities.WFBuildComponentList>();
                foreach (var ml in masterList.OrderBy(o => o.DisplayOrder))
                {
                    if (ml.IsMatched)
                    {
                        //Loop through addlist until match record
                        foreach (var al in addList.Where(a => !a.IsProcessed).OrderBy(o => o.DisplayOrder))
                        {
                            al.IsProcessed = true;
                            if (ml.WFComponentId == al.WFComponentId)
                                //Stop looping through addlist. Master will add this component
                                break;

                            retList.Add(new WFCustomEntities.WFBuildComponentList(al.WFComponentId, tmpOrderBy, al.DueDateFormulaTypeId, al.DueDateFormulaOffset, al.DueDateHourOffset, al.DueDateTimeTypeId, al.DefTaskAllocTypeId, al.DefTaskAllocWFComponentId, al.IsInactive, al.SettlementTypeId, al.DependentWFComponentIds));
                            tmpOrderBy++;

                        }
                    }

                    //Add Master
                    retList.Add(new WFCustomEntities.WFBuildComponentList(ml.WFComponentId, tmpOrderBy, ml.DueDateFormulaTypeId, ml.DueDateFormulaOffset, ml.DueDateHourOffset, ml.DueDateTimeTypeId, ml.DefTaskAllocTypeId, ml.DefTaskAllocWFComponentId, ml.IsInactive, ml.SettlementTypeId, ml.DependentWFComponentIds));
                    tmpOrderBy++;
                }
                //Add any remaining "AddList" records
                foreach (var al in addList.Where(a => !a.IsProcessed).OrderBy(o => o.DisplayOrder))
                {
                    al.IsProcessed = true;
                    retList.Add(new WFCustomEntities.WFBuildComponentList(al.WFComponentId, tmpOrderBy, al.DueDateFormulaTypeId, al.DueDateFormulaOffset, al.DueDateHourOffset, al.DueDateTimeTypeId, al.DefTaskAllocTypeId, al.DefTaskAllocWFComponentId, al.IsInactive, al.SettlementTypeId, al.DependentWFComponentIds));
                    tmpOrderBy++;
                }

                masterList = retList;
            }

            //If addListWFComponentIdStart has a value, return only those items after this component
            if (addListWFComponentIdStart.HasValue)
            {
                var tmpDisplOrder = retList.FirstOrDefault(r => r.WFComponentId == addListWFComponentIdStart)?.DisplayOrder;
                if (tmpDisplOrder.HasValue)
                    return retList.Where(d => d.DisplayOrder >= tmpDisplOrder).ToList();
            }


            return retList;

        }

        /// <summary>
        /// Removes Items from Workflow that are not completed yet - so there are not duplicates
        /// </summary>
        /// <param name="baseWorkFlow"></param>
        /// <param name="maxBaseWorkFlowDisplayOrderCheck"></param>
        /// <param name="addList"></param>
        /// <param name="addListWFComponentIdStart"></param>
        private void CleanAddList(List<MatterCustomEntities.MatterWFComponentBuildView> baseWorkFlow, int maxBaseWorkFlowDisplayOrderCheck, List<WFCustomEntities.WFBuildComponentList> addList, int? addListWFComponentIdStart, int dontLookRobbie = 0)
        {
            // Removes items from the addlist which are repeats of existing components which are not yet completed

            List<WFCustomEntities.WFBuildComponentList> chkList = addList;

            foreach (var baseRec in baseWorkFlow.Where(w => w.DisplayOrder <= maxBaseWorkFlowDisplayOrderCheck - 1 + dontLookRobbie).OrderBy(o => o.DisplayOrder))
            {
                if (!(baseRec.WFComponentStatusTypeId == (int)Enums.MatterWFComponentStatusTypeEnum.Deleted ||
                    baseRec.WFComponentStatusTypeId == (int)Enums.MatterWFComponentStatusTypeEnum.Cancelled ||
                    baseRec.WFComponentStatusTypeId == (int)Enums.MatterWFComponentStatusTypeEnum.Complete) && !baseRec.IsInactive)
                {
                    //Is it in the addList?
                    if (addList.Any(w => w.WFComponentId == baseRec.WFComponentId))
                    {
                        addList.Remove(addList.FirstOrDefault(w => w.WFComponentId == baseRec.WFComponentId));
                    }
                }
            }
        }
        private List<MatterCustomEntities.MatterWFDependencyBuildView> mergeMatterWFDependencies(List<MatterCustomEntities.MatterWFDependencyBuildView> aList, List<MatterCustomEntities.MatterWFDependencyBuildView> bList)
        {
            List<MatterCustomEntities.MatterWFDependencyBuildView> retList = new List<MatterCustomEntities.MatterWFDependencyBuildView>();
            foreach (var aRec in aList)
                retList.Add(aRec);

            foreach (var bRec in bList)
                if (!retList.Any(m => m.DependentWFComponentId == bRec.DependentWFComponentId))
                    retList.Add(bRec);

            return retList;
        }

        public int? AppendWorkflowComponent(MatterCustomEntities.MatterWFComponentView addComp)
        {
            var lastComp = context.MatterWFComponents.OrderByDescending(m => m.DisplayOrder).FirstOrDefault(m => m.MatterId == addComp.MatterId);
            if (lastComp != null)
            {
                MatterWFComponent mwfComp = new MatterWFComponent();
                mwfComp.MatterId = addComp.MatterId;
                mwfComp.WFComponentId = addComp.WFComponentId;
                mwfComp.CurrProcedureNo = 0;
                mwfComp.WFComponentStatusTypeId = addComp.CurrStatusTypeId;
                mwfComp.DisplayOrder = lastComp.DisplayOrder + 1;
                mwfComp.DisplayStatusTypeId = (int)Enums.DisplayStatusTypeEnum.Default;
                mwfComp.UpdatedDate = DateTime.Now;
                mwfComp.DefTaskAllocTypeId = (int)TaskAllocationTypeEnum.Unallocated;
                mwfComp.UpdatedByUserId = GlobalVars.CurrentUserId ?? Slick_Domain.Common.DomainConstants.SystemUserId;
                context.MatterWFComponents.Add(mwfComp);
                context.SaveChanges();
                return mwfComp.MatterWFComponentId;
            }
            else
                return null;

        }

        public bool MergeMatterWorkflowMergeMatterWorkflow(int matterId, int? matterWFComponentIdStart, int templateId, int? addListWFComponentIdStart)
        {
            return MergeMatterWorkflow(matterId, matterWFComponentIdStart, false, templateId, addListWFComponentIdStart);
        }
        public bool MergeMatterWorkflow(int matterId, int? matterWFComponentIdStart, bool includeStartingComponent, int templateId, int? addListWFComponentIdStart, int? updatedByUserId = null, bool forceIDYouWorkflow = false)
        {
            List<int> wfTemplateList = new List<int>();
            wfTemplateList.Add(templateId);

            return MergeMatterWorkflow(matterId, matterWFComponentIdStart, includeStartingComponent, wfTemplateList, addListWFComponentIdStart, updatedUserId: updatedByUserId, insertIDYouWorkflow: forceIDYouWorkflow);
        }
        public bool MergeMatterWorkflow(int matterId, int? matterWFComponentIdStart, List<int> templateIdList, int? addListWFComponentIdStart, bool overrideInactive = false, int allocatedUserId = 0, bool isAuto = false)
        {
            return MergeMatterWorkflow(matterId, matterWFComponentIdStart, false, templateIdList, addListWFComponentIdStart, overrideInactive, allocateUserId: allocatedUserId);
        }
        public bool MergeMatterWorkflow(int matterId, int? matterWFComponentIdStart, bool includeStartingComponent, List<int> templateIdList, int? addListWFComponentIdStart, bool overrideInactive = false, int allocateUserId = 1, int? updatedUserId = null, bool insertIDYouWorkflow = false)
        {
            try
            {
                List<MatterCustomEntities.MatterWFComponentBuildView> baseWorkFlow = new List<MatterCustomEntities.MatterWFComponentBuildView>();
                List<MatterCustomEntities.MatterWFComponentBuildView> mergedWorkFlow = new List<MatterCustomEntities.MatterWFComponentBuildView>();

                // Build the base workflow from existing
                IQueryable<MatterWFComponent> mwfcs = context.MatterWFComponents.Where
                    (m => m.MatterId == matterId && m.WFComponentStatusTypeId != (int)Enums.MatterWFComponentStatusTypeEnum.Deleted).OrderBy(m => m.DisplayOrder);

                string lenderName = context.Matters.Where(m=>m.MatterId == matterId).Select(l=>l.Lender.LenderName).FirstOrDefault();
                foreach (MatterWFComponent mwfc in mwfcs)
                {
                    MatterCustomEntities.MatterWFComponentBuildView tmp = new MatterCustomEntities.MatterWFComponentBuildView(
                    mwfc.MatterWFComponentId, mwfc.WFComponentId,
                        (mwfc.WFComponentStatusTypeId == (int)Enums.MatterWFComponentStatusTypeEnum.Deleted),
                        mwfc.WFComponentStatusTypeId,
                        mwfc.DisplayOrder, mwfc.DisplayStatusTypeId, mwfc.WFComponentDueDateFormulaId, mwfc.DueDateFormulaOffset, mwfc.WFComponentDueTimeHourOffset, mwfc.WFComponentDueTimeFormulaId, mwfc.DefTaskAllocTypeId,
                        mwfc.DefTaskAllocWFComponentId,
                        mwfc.DisplayStatusTypeId == (int)DisplayStatusTypeEnum.Inactive ? true : false,
                        mwfc.SettlementTypeId,
                    mwfc.MatterWFComponentDependents
                        .Select(m => new MatterCustomEntities.MatterWFDependencyBuildView(m.MatterWFComponentDependentId, m.MatterWFComponentId, m.MatterWFComponent.WFComponentId, m.DependentMatterWFComponentId, m.MatterWFComponent1.WFComponentId))
                        .ToList()
                    );
                   
                    baseWorkFlow.Add(tmp);
                }

                //check if IDyou workflow is required
                if(insertIDYouWorkflow || (lenderName?.ToUpper()?.Contains("THINK TANK") == true)) CheckForIDYouWorkflow(matterId, baseWorkFlow,  ref mergedWorkFlow, insertIDYouWorkflow);




                //get displayorderStart for starting baseflow component
                int baseStartDisplayOrder = 0;
                if (matterWFComponentIdStart.HasValue)
                {
                    int? tmpInt = baseWorkFlow.FirstOrDefault(m => m.MatterWFComponentId == matterWFComponentIdStart)?.DisplayOrder;
                    if (tmpInt.HasValue)
                        baseStartDisplayOrder = tmpInt.Value;
                }
                if (!includeStartingComponent)
                    baseStartDisplayOrder++;

                //Build the add workflow from addlist
                List<WFCustomEntities.WFBuildComponentList> addList = BuildWFComponentList(templateIdList, addListWFComponentIdStart, overrideInactive);
                CleanAddList(baseWorkFlow, baseStartDisplayOrder, addList, addListWFComponentIdStart);

                //mergelist needs to start with all matched=false
                foreach (var mltmp in addList.Where(m => m.IsMatched))
                    mltmp.IsMatched = false;


                // Match Same Components
                var ltmp = from m in baseWorkFlow
                           join a in addList on m.WFComponentId equals a.WFComponentId
                           where !m.IsDeleted && m.DisplayOrder >= baseStartDisplayOrder // && a.DisplayOrder > mergeStartDisplayOrder
                           select m;
                foreach (var m in ltmp)
                    m.IsMatched = true;
                var atmp = from m in baseWorkFlow
                           join a in addList on m.WFComponentId equals a.WFComponentId
                           where !m.IsDeleted && m.DisplayOrder >= baseStartDisplayOrder // && a.DisplayOrder > mergeStartDisplayOrder
                           select a;
                foreach (var m in atmp)
                    m.IsMatched = true;


                //Loop through base until we get to starting point
                int newDisplayOrder = 1;
                foreach (var btmp in baseWorkFlow.Where(m => m.DisplayOrder < baseStartDisplayOrder).OrderBy(o => o.DisplayOrder))
                {
                    mergedWorkFlow.Add(new MatterCustomEntities.MatterWFComponentBuildView(btmp.MatterWFComponentId, btmp.WFComponentId, btmp.IsDeleted,
                        btmp.WFComponentStatusTypeId, newDisplayOrder, btmp.DisplayStatusTypeId, btmp.WFComponentDueDateFormulaId, btmp.DueDateFormulaOffset, btmp.DueDateHourOffset, btmp.WFComponentDueTimeFormulaId,
                        btmp.DefAllocTaskTypeId, btmp.DefAllocTaskWFComponentId, btmp.IsInactive, btmp.SettlementTypeId,
                        btmp.DependentMatterWFComponents));
                    newDisplayOrder++;
                }

                //loop through base
                List<MatterCustomEntities.MatterWFDependencyBuildView> addDependsForMatched = new List<MatterCustomEntities.MatterWFDependencyBuildView>();
                foreach (var btmp in baseWorkFlow.Where(m => m.DisplayOrder >= baseStartDisplayOrder))
                {
                    //is it a matching record?
                    if (btmp.IsMatched)
                    {
                        //then loop though addlist until we reach this match
                        foreach (var al in addList.Where(a => !a.IsProcessed).OrderBy(o => o.DisplayOrder))
                        {
                            al.IsProcessed = true;
                            if (al.WFComponentId == btmp.WFComponentId)
                            {
                                addDependsForMatched = al.DependentWFComponentIds.Select(c => new MatterCustomEntities.MatterWFDependencyBuildView(null, null, al.WFComponentId, null, c)).ToList();
                                //Stop looping through addlist. Master will add this component
                                break;
                            }

                            mergedWorkFlow.Add(new MatterCustomEntities.MatterWFComponentBuildView(null, al.WFComponentId, false,
                                    (int)Enums.MatterWFComponentStatusTypeEnum.NotStarted, newDisplayOrder, (int)DisplayStatusTypeEnum.Default,
                                    al.DueDateFormulaTypeId, al.DueDateFormulaOffset, al.DueDateHourOffset, al.DueDateTimeTypeId, al.DefTaskAllocTypeId, al.DefTaskAllocWFComponentId, al.IsInactive, al.SettlementTypeId,
                                    al.DependentWFComponentIds.Select(c => new MatterCustomEntities.MatterWFDependencyBuildView(null, null, al.WFComponentId, null, c)).ToList()));
                            newDisplayOrder++;
                        }

                    }

                    //Add base record
                    mergedWorkFlow.Add(new MatterCustomEntities.MatterWFComponentBuildView(btmp.MatterWFComponentId, btmp.WFComponentId, btmp.IsDeleted, btmp.WFComponentStatusTypeId, newDisplayOrder, (int)DisplayStatusTypeEnum.Default,
                            btmp.WFComponentDueDateFormulaId, btmp.DueDateFormulaOffset, btmp.DueDateHourOffset, btmp.WFComponentDueTimeFormulaId, btmp.DefAllocTaskTypeId, btmp.DefAllocTaskWFComponentId, btmp.IsInactive, btmp.SettlementTypeId,
                            mergeMatterWFDependencies(btmp.DependentMatterWFComponents, addDependsForMatched)));
                    addDependsForMatched = new List<MatterCustomEntities.MatterWFDependencyBuildView>();
                    newDisplayOrder++;
                }

                //Add any remaining "AddList" records
                foreach (var al in addList.Where(a => !a.IsProcessed).OrderBy(o => o.DisplayOrder))
                {
                    al.IsProcessed = true;
                    mergedWorkFlow.Add(new MatterCustomEntities.MatterWFComponentBuildView(null, al.WFComponentId, false, (int)Enums.MatterWFComponentStatusTypeEnum.NotStarted, newDisplayOrder, (int)DisplayStatusTypeEnum.Default,
                            al.DueDateFormulaTypeId, al.DueDateFormulaOffset, al.DueDateHourOffset, al.DueDateTimeTypeId, al.DefTaskAllocTypeId, al.DefTaskAllocWFComponentId, al.IsInactive, al.SettlementTypeId,
                            al.DependentWFComponentIds.Select(c => new MatterCustomEntities.MatterWFDependencyBuildView(null, null, al.WFComponentId, null, c)).ToList()));
                    newDisplayOrder++;
                }


                //Check if matter is urgent and apply to milestone if required
                bool matterIsEscalated = context.Matters.Where(m => m.MatterId == matterId).Select(m => m.IsEscalated).FirstOrDefault();
                List<int> compsToEscalate = new List<int>() { (int)WFComponentEnum.CheckInstructions, (int)WFComponentEnum.DocPreparation, (int)WFComponentEnum.DocPreparationQA, (int)WFComponentEnum.CreateFrontCover, (int)WFComponentEnum.SendProcessedDocs };

                //now process these records.

                //1 - Create components (if necessary) and update display order
                foreach (var mergeRec in mergedWorkFlow)
                {
                    if (!mergeRec.MatterWFComponentId.HasValue)
                    {
                        MatterWFComponent mwc = new MatterWFComponent();
                        mwc.MatterId = matterId;
                        mwc.WFComponentId = mergeRec.WFComponentId;
                        mwc.IsUrgent = matterIsEscalated && compsToEscalate.Contains(mergeRec.WFComponentId);

                        if (mwc.IsUrgent)
                        {
                            mwc.AssignedTaskColour = "#FFFF0000";
                            mwc.AssignedTaskNote = "**ESCALATED**";
                        }

                        mwc.CurrProcedureNo = 0;
                        mwc.WFComponentStatusTypeId = (int)MatterWFComponentStatusTypeEnum.NotStarted;
                        mwc.DisplayOrder = mergeRec.DisplayOrder;
                        mwc.DisplayStatusTypeId = mergeRec.IsInactive ? (int)DisplayStatusTypeEnum.Inactive : (int)DisplayStatusTypeEnum.Default;
                        mwc.WFComponentDueDateFormulaId = mergeRec.WFComponentDueDateFormulaId;

                        mwc.WFComponentDueTimeFormulaId = mergeRec.WFComponentDueTimeFormulaId;
                        mwc.WFComponentDueTimeHourOffset = mergeRec.DueDateHourOffset;


                        mwc.DueDateFormulaOffset = mergeRec.DueDateFormulaOffset;
                        mwc.DefTaskAllocTypeId = mergeRec.DefAllocTaskTypeId;
                        mwc.DefTaskAllocWFComponentId = mergeRec.DefAllocTaskWFComponentId;
                        mwc.SettlementTypeId = mergeRec.SettlementTypeId;

                        mwc.UpdatedDate = DateTime.Now;
                        mwc.UpdatedByUserId = updatedUserId ?? Slick_Domain.GlobalVars.CurrentUser.UserId;
                        if (addListWFComponentIdStart.HasValue)
                        {
                            if (allocateUserId > 1 && mwc.WFComponentId == addListWFComponentIdStart)
                            {
                                mwc.TaskAllocTypeId = (int)Enums.TaskAllocationTypeEnum.User;
                                mwc.TaskAllocUserId = allocateUserId;

                            }
                            else
                            {
                                if (allocateUserId == -1)
                                {
                                    mwc.TaskAllocTypeId = (int)Enums.TaskAllocationTypeEnum.Shared;
                                }
                                if (allocateUserId == (int)Enums.TaskAllocationEnum.Manila)
                                {
                                    mwc.TaskAllocTypeId = (int)Enums.TaskAllocationTypeEnum.Manila;
                                }
                            }
                        }

                        context.MatterWFComponents.Add(mwc);

                        context.SaveChanges();
                        mergeRec.MatterWFComponentId = mwc.MatterWFComponentId;
                    }
                    else
                    {
                        MatterWFComponent mwc = context.MatterWFComponents.FirstOrDefault(m => m.MatterWFComponentId == mergeRec.MatterWFComponentId);
                        if (mwc.DisplayOrder != mergeRec.DisplayOrder)
                        {
                            mwc.DisplayOrder = mergeRec.DisplayOrder;
                            mwc.UpdatedDate = DateTime.Now;
                            mwc.UpdatedByUserId = updatedUserId ?? Slick_Domain.GlobalVars.CurrentUser.UserId;
                            context.SaveChanges();
                        }
                    }
                }

                //2 - Update Dependencies
                foreach (var mergeRec in mergedWorkFlow.Where(m => m.WFComponentStatusTypeId != (int)Enums.MatterWFComponentStatusTypeEnum.Cancelled &&
                                                                   m.WFComponentStatusTypeId != (int)Enums.MatterWFComponentStatusTypeEnum.Complete &&
                                                                   m.WFComponentStatusTypeId != (int)Enums.MatterWFComponentStatusTypeEnum.Deleted &&
                                                                   m.MatterWFComponentId != matterWFComponentIdStart))
                {
                    foreach (var dependRec in mergeRec.DependentMatterWFComponents)
                    {
                        var depComp = mergedWorkFlow.OrderByDescending(o => o.DisplayOrder).FirstOrDefault(m => m.WFComponentId == dependRec.DependentWFComponentId);

                        if (depComp != null)
                        {
                            if (dependRec.MatterWFComponentDependentId.HasValue && dependRec.DependentMatterWFComponentId != depComp.MatterWFComponentId)
                            {
                                //Dependecy must have changed. Remove existing one
                                var depTmp = context.MatterWFComponentDependents.FirstOrDefault(m => m.MatterWFComponentDependentId == dependRec.MatterWFComponentDependentId);
                                context.MatterWFComponentDependents.Remove(depTmp);
                                dependRec.MatterWFComponentDependentId = null;
                            }


                            if (!dependRec.MatterWFComponentDependentId.HasValue || dependRec.DependentMatterWFComponentId != depComp.MatterWFComponentId)
                            {
                                //New record required
                                MatterWFComponentDependent newMWFDep = new MatterWFComponentDependent();
                                newMWFDep.MatterWFComponentId = mergeRec.MatterWFComponentId.Value;
                                newMWFDep.DependentMatterWFComponentId = depComp.MatterWFComponentId.Value;

                                if (!context.MatterWFComponentDependents.Any(w => w.MatterWFComponentId == newMWFDep.MatterWFComponentId && w.DependentMatterWFComponentId == newMWFDep.DependentMatterWFComponentId))
                                {
                                    context.MatterWFComponentDependents.Add(newMWFDep);
                                    context.SaveChanges();
                                }
                                dependRec.MatterWFComponentDependentId = newMWFDep.MatterWFComponentDependentId;
                            }
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                throw (ex);
            }
        }


        public void CheckForIDYouWorkflow(int matterId, List<MCE.MatterWFComponentBuildView> baseWorkflow, ref List<MCE.MatterWFComponentBuildView> mergedWorkFlow, bool forceWorkflow = false)
        {
            var mtDetails = context.Matters.Where(m => m.MatterId == matterId).Select(m => new { m.Lender.LenderName, specialConditions = m.MatterSpecialConditions.Select(s => s.SpecialConditionDesc.ToUpper()).ToList() }).FirstOrDefault();


            List<string> IDYouTriggerPhrases = new List<string>() { "You are required to undertake a verification of identity".ToUpper(), "Prior to settlement our solicitors will be required to undertake a Verification of Identity".ToUpper(), "Approval will be subject to completed Verification of identity for all applicants prior to settlement of loan and is to be completed by Think Tank Solicitors".ToUpper() };
            if (forceWorkflow == true || ( mtDetails.LenderName.ToUpper().Contains("THINK TANK") && mtDetails.specialConditions.Any(c=>IDYouTriggerPhrases.Any(t=>c.ToUpper().Contains(t)))))
            {
                List<int> wfComponentIdsToInsert = new List<int>() { (int)WFComponentEnum.IDYouInvitation, (int)WFComponentEnum.IDYouCompleted, (int)WFComponentEnum.IDYouReportSent, (int)WFComponentEnum.IDYouReportApproved };
                var displayOrder = -5;

                int? prevInsertedComp = null;
                foreach (var comp in wfComponentIdsToInsert)
                {
                    var matchComp = baseWorkflow?.FirstOrDefault(m => m.WFComponentId == comp);

                    if (matchComp == null)
                    {
                        List<MCE.MatterWFDependencyBuildView> dependencies = new List<MCE.MatterWFDependencyBuildView>();

                        if (prevInsertedComp.HasValue)
                        {
                            dependencies.Add(new MCE.MatterWFDependencyBuildView(null, null, comp, baseWorkflow?.FirstOrDefault(m => m.WFComponentId == prevInsertedComp.Value)?.MatterWFComponentId, prevInsertedComp.Value));
                        }

                        int taskAllocType = comp == (int)WFComponentEnum.IDYouInvitation || comp == (int)WFComponentEnum.IDYouCompleted ? (int)TaskAllocationTypeEnum.Shared : (int)TaskAllocationTypeEnum.FileManager;

                        mergedWorkFlow.Add(new MatterCustomEntities.MatterWFComponentBuildView(null, comp, false,
                           (int)Enums.MatterWFComponentStatusTypeEnum.NotStarted, displayOrder, (int)DisplayStatusTypeEnum.Default, null, null, null, null, taskAllocType, null, false, null,
                           dependencies));

                        displayOrder++;
                    }

                    prevInsertedComp = comp;
                }
            }
        }

        public MatterCustomEntities.MatterWFFollowUpView GetMatterWFFollowUpViewFromCompId(int matterWFComponentId)
        {
            return context.MatterWFFollowUps.Where(m => m.MatterWFComponentId == matterWFComponentId)
                .Select(m => new MatterCustomEntities.MatterWFFollowUpView
                {
                    MatterWFFollowUpId = m.MatterWFFollowUpId,
                    MatterWFComponentId = m.MatterWFComponentId,
                    QuestionResponse = m.QuestionResponse,
                    MilestoneNotes = m.MilestoneNotes,
                    MilestoneActionTypeId = m.MilestoneActionTypeId,
                    FollowUpDate = m.FollowUpDate,
                    RestartWFComponentId = m.RestartWFComponentId,
                    IncludeNotesInMatterNotes = m.IncludeNotesInMatterNotes,
                    UpdatedDate = m.UpdatedDate,
                    UpdatedByUserId = m.UpdatedByUserId,
                    UpdatedByUsername = m.User.Username
                }).FirstOrDefault();
        }
        public IEnumerable<MatterCustomEntities.MatterWFFollowUpView> GetMatterWFFollowUpsView(int matterWFComponentId)
        {
            return context.MatterWFFollowUps.Where(m => m.MatterWFComponentId == matterWFComponentId)
                .Select(m => new MatterCustomEntities.MatterWFFollowUpView
                {
                    MatterWFFollowUpId = m.MatterWFFollowUpId,
                    MatterWFComponentId = m.MatterWFComponentId,
                    QuestionResponse = m.QuestionResponse,
                    MilestoneNotes = m.MilestoneNotes,
                    MilestoneActionTypeId = m.MilestoneActionTypeId,
                    FollowUpDate = m.FollowUpDate,
                    RestartWFComponentId = m.RestartWFComponentId,
                    IncludeNotesInMatterNotes = m.IncludeNotesInMatterNotes,
                    UpdatedDate = m.UpdatedDate,
                    UpdatedByUserId = m.UpdatedByUserId,
                    UpdatedByUsername = m.User.Username
                }).ToList();
        }
        public IEnumerable<MatterCustomEntities.MatterWFFollowUpHistoryView> GetMatterWFFollowUpHistoriesView(int matterWFFollowUpId)
        {
            return context.MatterWFFollowUpHistories.Where(m => m.MatterWFFollowupId == matterWFFollowUpId)
                .Select(m => new MatterCustomEntities.MatterWFFollowUpHistoryView
                {
                    MatterWFFollowUpHistoryId = m.MatterWFFollowUpHistoryId,
                    MatterWFFollowUpId = m.MatterWFFollowupId,
                    MilestoneNotes = m.MilestoneNotes,
                    FollowUpDate = m.FollowUpDate,
                    UpdatedDate = m.UpdatedDate,
                    UpdatedByUserId = m.UpdatedByUserId,
                    UpdatedByUsername = m.User.Username
                }).ToList();
        }


        public void UpdateDueDates(int matterId)
        {
            var mat = context.Matters.FirstOrDefault(m => m.MatterId == matterId);
            var holidayList = CommonMethods.GetHolidayList(context, mat.InstructionStateId ?? mat.StateId);

            var mwfcsFull = context.MatterWFComponents.Where(m => m.MatterId == matterId).OrderBy(o => o.DisplayOrder);
            var mwfcsNotComplete = mwfcsFull.Where(m => m.WFComponentStatusType.IsActiveStatus || m.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.NotStarted);

            if (mwfcsNotComplete != null && mwfcsNotComplete.Any())
            {
                foreach (var mwfc in mwfcsNotComplete)
                {



                    if (mwfc.DueDateReassigned.HasValue && mwfc.DueDateReassigned.Value) continue;
                    if (mwfc.WFComponentDueDateFormulaId.HasValue && mwfc.DueDateFormulaOffset.HasValue)
                    {
                        mwfc.UpdatedDate = System.DateTime.UtcNow;
                        var newDueDate = CalculateDueDate(mwfcsFull, mwfc, holidayList);
                        //if (newDueDate.HasValue && newDueDate.Value.TimeOfDay.Hours == 0) newDueDate = newDueDate.Value.AddHours(17);
                        mwfc.DueDate = newDueDate;
                        //if (mat.DueDateOffsetMinutes.HasValue && mwfc.DueDate.HasValue)
                        //{
                        //    mwfc.DueDate = TimeZoneHelper.AddOffsetMinutes(mwfc.DueDate.Value, mat.DueDateOffsetMinutes.Value, mat.StateId);
                        //}
                    }
                    else
                        mwfc.DueDate = null;
                }
                context.SaveChanges();
            }
        }


        public int InitialiseCreateMatterWFComponent(int matterId, DateTime? startedDate)
        {
            MatterWFComponent matterWfComponent = new MatterWFComponent()
            {
                MatterId = matterId,
                WFComponentId = (int)Slick_Domain.Enums.WFComponentEnum.CreateMatter,
                WFComponentStatusTypeId = (int)Slick_Domain.Enums.MatterWFComponentStatusTypeEnum.Starting,
                DisplayOrder = 1,
                DisplayStatusTypeId = (int)Slick_Domain.Enums.DisplayStatusTypeEnum.Display,
                UpdatedByUserId = GlobalVars.CurrentUserId ?? Slick_Domain.Common.DomainConstants.SystemUserId,
                UpdatedDate = startedDate ?? System.DateTime.UtcNow,
                DefTaskAllocTypeId = (int)Slick_Domain.Enums.TaskAllocationTypeEnum.Unallocated,
            };
            context.MatterWFComponents.Add(matterWfComponent);
            context.SaveChanges();
            //using (UnitOfWork uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadCommitted))
            //{
            //    try
            //    {
            //        uow.Context.MatterWFComponents.Add(matterWfComponent);
            //        uow.Save();
            //      //  uow.CommitTransaction();
            //    }
            //    catch (Exception ex)
            //    {
            //        uow.RollbackTransaction();
            //        throw ex;
            //    }
            //}
            return matterWfComponent.MatterWFComponentId;
        }

        public bool UpdateAllDueDates(int matterId, int wfComponentId)
        {
            return UpdateAllDueDates(matterId, wfComponentId, true);
        }
        public bool UpdateAllDueDates(int matterId, int wfComponentId, bool saveContext)
        {
            bool retval = false;
            var mt = context.Matters.AsNoTracking().Where(m => m.MatterId == matterId)
                                .Select(m => new WFCustomEntities.WFComponentDueDateMatterDetails
                                {
                                    MatterId = m.MatterId,
                                    StateId = m.InstructionStateId ?? m.StateId,
                                    FileOpenedDate = m.CheckInstructionsDateTime ?? (m.CheckInstructionsDate > new DateTime() ? m.CheckInstructionsDate : m.FileOpenedDate),
                                    SettlementDate = m.SettlementSchedule.SettlementDate,
                                    SettlementTime = m.SettlementSchedule.SettlementTime,
                                    InstructionDeadlineDate = m.InstructionDeadlineDate,
                                    FastRefiSettlementDate = m.SettlementSchedule1.SettlementDate,
                                    FastRefiSettlementTime = m.SettlementSchedule1.SettlementTime
                                }
                                )
                                .FirstOrDefault();




            if (mt.SettlementDate.HasValue && mt.SettlementTime.HasValue) mt.SettlementDate.Value.AddTicks(mt.SettlementTime.Value.Ticks);

            if (mt == null) return false;

            var holidayList = CommonMethods.GetHolidayList(context, mt.StateId).ToList();

            var mwfcsFull = context.MatterWFComponents.Where(m => m.MatterId == matterId).ToList();

            var mwfcsLatest = mwfcsFull.GroupBy(g => new { g.WFComponentId })
                                .Select(m => new { m.Key.WFComponentId, maxDisplay = m.Max(x => x.DisplayOrder) });

            int? DueDateOffsetMinutes = context.Matters.AsNoTracking().Where(m => m.MatterId == matterId).Select(x => x.DueDateOffsetMinutes).FirstOrDefault();

            foreach (var mwfLatest in mwfcsLatest.Where(x => wfComponentId == 0 || x.WFComponentId == wfComponentId))
            {
                var mwfc = mwfcsFull.FirstOrDefault(m => m.WFComponentId == mwfLatest.WFComponentId && m.DisplayOrder == mwfLatest.maxDisplay);
                if (mwfc.DueDateReassigned.HasValue && mwfc.DueDateReassigned.Value) continue;

                DateTime? newDueDate;

                if (mwfc.WFComponentDueDateFormulaId.HasValue && mwfc.DueDateFormulaOffset.HasValue)
                {
                    var ddf = GetDueDateFormula(mwfc, mt);
                    DateTime? existingDueDate = mwfc.DueDate;

                    newDueDate = CalculateDueDate(mwfcsFull, ddf, holidayList, mt.StateId, DueDateOffsetMinutes);
                }
                else
                    newDueDate = null;

                if (mwfc.DueDate != newDueDate)
                {
                    if (newDueDate.HasValue && newDueDate.Value.TimeOfDay.Hours == 0) newDueDate = newDueDate.Value.AddHours(17);
                    mwfc.DueDate = newDueDate;
                    retval = true;
                }
            }

            context.Matters.Where(m => m.MatterId == matterId).FirstOrDefault().DueDateOffsetMinutes = 0;

            if (saveContext)
                context.SaveChanges();
            return retval;

        }


        private WFCustomEntities.WFComponentDueDateFormulaView GetDueDateFormula(MatterWFComponent matterWFComponent, WFCustomEntities.WFComponentDueDateMatterDetails mt)
        {
            if (matterWFComponent == null || !matterWFComponent.WFComponentDueDateFormulaId.HasValue || !matterWFComponent.DueDateFormulaOffset.HasValue)
                return null;

            var toReturn = context.WFComponentDueDateFormulas.AsNoTracking().Where(x => x.WFComponentDueDateFormulaId == matterWFComponent.WFComponentDueDateFormulaId)
                                .Select(m => new WFCustomEntities.WFComponentDueDateFormulaView
                                {
                                    WFComponentDueDateFormulaId = m.WFComponentDueDateFormulaId,
                                    WFComponentDueDateFormulaName = m.WFComponentDueDateFormulaName,
                                    DueDateOffsetRangeType = m.DueDateOffsetRangeType,
                                    DueDateOffsetForward = m.DueDateOffsetForward,
                                    DueDateOffsetAmount = matterWFComponent.DueDateFormulaOffset.Value,
                                    FromFileOpened = m.FromFileOpened,
                                    FileOpenedDate = mt.FileOpenedDate,
                                    InstructionDeadlineDate = mt.InstructionDeadlineDate,
                                    FastRefiSettlementDate = mt.FastRefiSettlementDate,
                                    FromSettlementDate = m.FromSettlementDate,
                                    FromInstructionDeadline = m.FromInstructionDeadline,
                                    FromFastRefiSettlementDate = m.FromFastRefiSettlementDate,
                                    SettlementDate = mt.SettlementDate,
                                    FromWFComponentId = m.FromWFComponentId,
                                    FromComponentStartDate = m.FromComponentStartDate,
                                })
                                  .FirstOrDefault();

            if (matterWFComponent.WFComponentDueTimeFormulaId.HasValue)
            {

                toReturn.WFComponentDueTimeId = matterWFComponent.WFComponentDueTimeFormulaId;

                toReturn.WFComponentDueTimeName = matterWFComponent.WFComponentDueTimeFormula.WFComponentDueTimeFormulaName;
            }

            if (matterWFComponent.WFComponentDueTimeHourOffset.HasValue)
            {
                toReturn.DueDateOffsetHours = matterWFComponent.WFComponentDueTimeHourOffset;
            }
            return toReturn;
        }

        private DateTime? CalculateDueDate(IEnumerable<MatterWFComponent> mwfcs, WFCustomEntities.WFComponentDueDateFormulaView ddf, IEnumerable<Holiday> holidayList, int? stateId = null, int? offsetMinutes = null, DateTime? existingDueDate = null)
        {
            DateTime? tmpDate = null;
            DateTime? retVal = null;

            int tmpOffset;

            if (ddf == null)
                return null;

            if (ddf.FromFileOpened)
                tmpDate = ddf.FileOpenedDate;
            else if (ddf.FromSettlementDate)
                tmpDate = ddf.SettlementDate;
            else if (ddf.FromInstructionDeadline)
                tmpDate = ddf.InstructionDeadlineDate.HasValue ? ddf.InstructionDeadlineDate.Value.ToLocalTime() : tmpDate = null;
            else if (ddf.FromFastRefiSettlementDate)
                tmpDate = ddf.FastRefiSettlementDate.HasValue ? ddf.FastRefiSettlementDate.Value : tmpDate = null;
            else if (ddf.FromWFComponentId.HasValue)
            {
                MatterWFComponent latestMWFComp = mwfcs.OrderByDescending(o => o.DisplayOrder).FirstOrDefault(m => m.WFComponentId == ddf.FromWFComponentId);
                if (latestMWFComp != null)
                {
                    MatterEvent me = context.MatterEvents.OrderByDescending(o => o.EventDate).FirstOrDefault(e => e.MatterWFComponentId == latestMWFComp.MatterWFComponentId && e.MatterEventTypeId == (int)Enums.MatterEventTypeList.MilestoneComplete && e.MatterEventStatusTypeId == (int)MatterEventStatusTypeEnum.Good);

                    if (me != null)
                        tmpDate = me.EventDate.ToLocalTime();
                }
            }
            else if (ddf.FromComponentStartDate)
            {
                MatterWFComponent latestMWFComp = mwfcs.OrderByDescending(o => o.DisplayOrder).FirstOrDefault(m => m.WFComponentId == ddf.FromWFComponentId);
                if (latestMWFComp != null)
                {
                    MatterEvent me = context.MatterEvents.OrderByDescending(o => o.EventDate).FirstOrDefault(e => e.MatterWFComponentId == latestMWFComp.MatterWFComponentId && e.MatterEventTypeId == (int)Enums.MatterEventTypeList.MilestoneStarted && e.MatterEventStatusTypeId == (int)MatterEventStatusTypeEnum.Good);

                    if (me != null)
                        tmpDate = me.EventDate.ToLocalTime();
                    else
                        tmpDate = DateTime.Now.ForceBusinessHours(GlobalVars.CurrentUser.StateId, context, latestMWFComp.Matter.LenderId);
                }
            }
            //else if(ddf.)

            if (tmpDate != null)
            {
                tmpOffset = ddf.DueDateOffsetAmount;
                if (!ddf.DueDateOffsetForward)
                    tmpOffset = 0 - tmpOffset;

                switch (ddf.DueDateOffsetRangeType)
                {
                    case "d":
                        retVal = tmpDate.Value.AddBusinessDays(tmpOffset, holidayList);
                        break;
                    case "m":
                        retVal = tmpDate.Value.AddBusinessMonths(tmpOffset, holidayList);
                        break;

                }
                tmpDate = retVal;

            }


            if (ddf.WFComponentDueTimeId.HasValue && ddf.WFComponentDueTimeId.Value != (int)Enums.DueTimeFormulaEnum.MatchTime)
            {
                if (tmpDate.HasValue)
                {
                    DateTime newTmpDate = new DateTime(tmpDate.Value.Date.Year, tmpDate.Value.Date.Month, tmpDate.Value.Date.Day);
                    switch (ddf.WFComponentDueTimeId.Value)
                    {
                        case (int)Enums.DueTimeFormulaEnum.Midday:
                            newTmpDate = newTmpDate.AddHours(12);
                            break;
                        case (int)Enums.DueTimeFormulaEnum.Midnight:
                            break;
                        case (int)Enums.DueTimeFormulaEnum.StartOfDay:
                            TimeSpan? openTime = context.Offices.Where(t => t.StateId == stateId).FirstOrDefault().OfficeHoursOpen;
                            if (!openTime.HasValue)
                            {
                                openTime = new TimeSpan(8, 30, 0);
                            }
                            newTmpDate = newTmpDate.AddTicks(openTime.Value.Ticks);
                            break;
                        case (int)Enums.DueTimeFormulaEnum.EndOfDay:
                            TimeSpan? closeTime = context.Offices.Where(t => t.StateId == stateId).FirstOrDefault().OfficeHoursClose;
                            if (!closeTime.HasValue)
                            {
                                closeTime = new TimeSpan(17, 0, 0);
                            }
                            newTmpDate = newTmpDate.AddTicks(closeTime.Value.Ticks);
                            break;
                        default:
                            newTmpDate = tmpDate.Value;
                            break;
                    }
                    tmpDate = newTmpDate;


                }
            }

            if (offsetMinutes.HasValue && tmpDate.HasValue && stateId.HasValue && !existingDueDate.HasValue)
            {
                if (ddf.FromFileOpened || ddf.FromInstructionDeadline)
                {
                    tmpDate = TimeZoneHelper.AddOffsetMinutes(tmpDate.Value, offsetMinutes.Value, stateId.Value, context);
                }
            }

            if (tmpDate.HasValue && ddf.DueDateOffsetHours.HasValue)
            {
                if (ddf.WFComponentDueTimeId.HasValue && ddf.WFComponentDueTimeId.Value == (int)Enums.DueTimeFormulaEnum.Midnight)
                {
                    tmpDate = OffsetBusinessHours(tmpDate.Value, ddf.DueDateOffsetHours.Value, stateId, forceBusinessHours: false);
                }
                else
                {
                    tmpDate = OffsetBusinessHours(tmpDate.Value, ddf.DueDateOffsetHours.Value, stateId);
                }
            }
            if (tmpDate.HasValue) retVal = tmpDate;
            return retVal;
        }



        private DateTime? CalculateDueDate(IEnumerable<MatterWFComponent> mwfcs, MatterWFComponent mwfc, IEnumerable<Holiday> holidayList)
        {

            DateTime? tmpDate = null;
            DateTime? retVal = null;

            int tmpOffset;

            if (mwfc.WFComponentDueDateFormula == null)
            {
                if (!mwfc.WFComponentDueDateFormulaId.HasValue)
                    return null;

                mwfc.WFComponentDueDateFormula = context.WFComponentDueDateFormulas.FirstOrDefault(f => f.WFComponentDueDateFormulaId == mwfc.WFComponentDueDateFormulaId);
                if (mwfc.WFComponentDueDateFormula == null)
                    return null;
            }

            if (mwfc.WFComponentDueDateFormula.FromFileOpened)
                tmpDate = mwfc.Matter.CheckInstructionsDateTime ?? mwfc.Matter.CheckInstructionsDate;
            else if (mwfc.WFComponentDueDateFormula.FromSettlementDate && mwfc.Matter.SettlementScheduleId.HasValue)
                tmpDate = mwfc.Matter.SettlementSchedule.SettlementDate.AddTicks(mwfc.Matter.SettlementSchedule.SettlementTime.Ticks);
            else if (mwfc.WFComponentDueDateFormula.FromInstructionDeadline)
                tmpDate = mwfc.Matter.InstructionDeadlineDate.HasValue ? mwfc.Matter.InstructionDeadlineDate.Value.ToLocalTime() : tmpDate = null;
            else if (mwfc.WFComponentDueDateFormula.FromComponentStartDate)
            {
                if (mwfc != null)
                {
                    MatterEvent me = context.MatterEvents.OrderByDescending(o => o.EventDate)
                        .FirstOrDefault(e => e.MatterWFComponentId == mwfc.MatterWFComponentId && e.MatterEventTypeId == (int)Enums.MatterEventTypeList.MilestoneStarted);
                    if (me != null)
                        tmpDate = me.EventDate.ToLocalTime();
                    else
                        tmpDate = DateTime.Now.ForceBusinessHours(GlobalVars.CurrentUser.StateId, context, mwfc.Matter.LenderId);
                }
            }
            else if (mwfc.WFComponentDueDateFormula.FromWFComponentId.HasValue)
            {
                MatterWFComponent latestMWFComp = mwfcs.OrderByDescending(o => o.DisplayOrder).FirstOrDefault(m => m.WFComponentId == mwfc.WFComponentDueDateFormula.FromWFComponentId);
                if (latestMWFComp != null)
                {
                    MatterEvent me = context.MatterEvents.OrderByDescending(o => o.EventDate).FirstOrDefault(e => e.MatterWFComponentId == latestMWFComp.MatterWFComponentId && e.MatterEventTypeId == (int)Enums.MatterEventTypeList.MilestoneComplete && e.MatterEventStatusTypeId == (int)MatterEventStatusTypeEnum.Good);

                    if (me != null)
                        tmpDate = me.EventDate.ToLocalTime();

                    //ReWorks reset the CheckInstructionDate. Check to make sure the component event date isn't before the CheckInstructionDate
                    // and therefore no longer relevant for the Due Date calcs.
                    if (tmpDate < mwfc.Matter.CheckInstructionsDate)
                        tmpDate = null;
                }
                else if (mwfc.WFComponentDueDateFormula.FromWFComponentId == 53)
                {
                    //Small Hack... if FromWFComponentId=53 (Doc Prep), and it was not found... try Doc Prep Tester (68)
                    latestMWFComp = mwfcs.OrderByDescending(o => o.DisplayOrder).FirstOrDefault(m => m.WFComponentId == 68);
                    if (latestMWFComp != null)
                    {
                        MatterEvent me = context.MatterEvents.OrderByDescending(o => o.EventDate).FirstOrDefault(e => e.MatterWFComponentId == latestMWFComp.MatterWFComponentId && e.MatterEventTypeId == (int)Enums.MatterEventTypeList.MilestoneComplete && e.MatterEventStatusTypeId == (int)MatterEventStatusTypeEnum.Good);

                        if (me != null)
                            tmpDate = me.EventDate.ToLocalTime();
                    }

                }

            }

            if (tmpDate.HasValue && tmpDate.Value.Year < 1970) //incase it's one of the broken matters that somehow got check instructions date as 01/01/0001 - valid as date but not as datetime so causes exception
            {
                tmpDate = null;
            }

            if (mwfc.WFComponentDueTimeFormulaId.HasValue && mwfc.WFComponentDueTimeFormulaId.Value != (int)Enums.DueTimeFormulaEnum.MatchTime)
            {
                if (tmpDate.HasValue)
                {
                    DateTime newTmpDate = new DateTime(tmpDate.Value.Date.Year, tmpDate.Value.Date.Month, tmpDate.Value.Date.Day);
                    switch (mwfc.WFComponentDueTimeFormulaId.Value)
                    {
                        case (int)Enums.DueTimeFormulaEnum.Midday:
                            newTmpDate = newTmpDate.AddHours(12);
                            break;
                        case (int)Enums.DueTimeFormulaEnum.Midnight:
                            break;
                        case (int)Enums.DueTimeFormulaEnum.StartOfDay:
                            TimeSpan? openTime = context.Offices.Where(t => t.StateId == mwfc.Matter.StateId).FirstOrDefault().OfficeHoursOpen;
                            if (!openTime.HasValue)
                            {
                                openTime = new TimeSpan(8, 30, 0);
                            }
                            newTmpDate = newTmpDate.AddTicks(openTime.Value.Ticks);
                            break;
                        case (int)Enums.DueTimeFormulaEnum.EndOfDay:
                            TimeSpan? closeTime = context.Offices.Where(t => t.StateId == mwfc.Matter.StateId).FirstOrDefault().OfficeHoursClose;
                            if (!closeTime.HasValue)
                            {
                                closeTime = new TimeSpan(17, 0, 0);
                            }
                            newTmpDate = newTmpDate.AddTicks(closeTime.Value.Ticks);
                            break;
                        default:
                            newTmpDate = tmpDate.Value;
                            break;
                    }
                    tmpDate = newTmpDate;


                }
            }



            if (tmpDate != null && mwfc.DueDateFormulaOffset.HasValue)
            {
                tmpOffset = mwfc.DueDateFormulaOffset.Value;
                if (!mwfc.WFComponentDueDateFormula.DueDateOffsetForward)
                    tmpOffset = 0 - tmpOffset;

                switch (mwfc.WFComponentDueDateFormula.DueDateOffsetRangeType)
                {
                    case "d":
                        retVal = tmpDate.Value.AddBusinessDays(tmpOffset, holidayList);
                        break;
                    case "m":
                        retVal = tmpDate.Value.AddBusinessMonths(tmpOffset, holidayList);
                        break;
                }

                tmpDate = retVal;
            }


            if (tmpDate != null && mwfc.WFComponentDueTimeHourOffset.HasValue)
            {
                if (tmpDate.HasValue && mwfc.WFComponentDueTimeHourOffset.HasValue)
                {
                    if (mwfc.WFComponentDueTimeFormulaId.HasValue && mwfc.WFComponentDueTimeFormulaId.Value == (int)Enums.DueTimeFormulaEnum.Midnight)
                    {
                        tmpDate = OffsetBusinessHours(tmpDate.Value, mwfc.WFComponentDueTimeHourOffset.Value, mwfc.Matter.StateId, forceBusinessHours: false);
                    }
                    else
                    {
                        tmpDate = OffsetBusinessHours(tmpDate.Value, mwfc.WFComponentDueTimeHourOffset.Value, mwfc.Matter.StateId);
                    }
                }
            }


            if (tmpDate.HasValue) retVal = tmpDate;
            return retVal;
        }
        //OffsetBusinessHours
        public DateTime OffsetBusinessHours(DateTime dueDateTime, int numHours, int? stateId, bool forceBusinessHours = true)
        {
            if (!forceBusinessHours)
            {
                return dueDateTime.AddHours(numHours);
            }

            DateTime openingTime;
            DateTime closingTime;
            List<Holiday> holidayList;
            Office office = null;

            int direction = 1;

            if (numHours < 0)
            {
                direction = -1;
            }

            holidayList = context.Holidays.Where(h => h.IsNational || h.HolidayStates.Any(s => s.StateId == stateId)).ToList();
            if (stateId.HasValue)
            {
                office = context.Offices.AsNoTracking().Where(x => x.StateId == stateId).FirstOrDefault();
            }
            else
            {
                stateId = 1;
            }

            if (office != null)
            {
                TimeSpan openingTs = office.OfficeHoursOpen;
                TimeSpan closingTs = office.OfficeHoursClose;

                openingTime = new DateTime();
                openingTime = openingTime.AddTicks(openingTs.Ticks);

                closingTime = new DateTime();
                closingTime = closingTime.AddTicks(closingTs.Ticks);
            }
            else
            {
                openingTime = new DateTime().AddHours(8).AddMinutes(30);
                closingTime = new DateTime().AddHours(17);
            }

            DateTime returnDueDateTime = dueDateTime.AddHours(numHours);

            DateTime specificOpeningTime = new DateTime(returnDueDateTime.Year, returnDueDateTime.Month, returnDueDateTime.Day, openingTime.Hour, openingTime.Minute, openingTime.Second);
            DateTime specificClosingTime = new DateTime(returnDueDateTime.Year, returnDueDateTime.Month, returnDueDateTime.Day, closingTime.Hour, closingTime.Minute, closingTime.Second);


            //after business hours
            if (returnDueDateTime > specificClosingTime)
            {
                var diff = (returnDueDateTime - specificClosingTime).TotalMinutes;
                returnDueDateTime = returnDueDateTime.AddBusinessDays(1, stateId.Value, context);
                returnDueDateTime = new DateTime(returnDueDateTime.Year, returnDueDateTime.Month, returnDueDateTime.Day, openingTime.Hour, openingTime.Minute, openingTime.Second);
                returnDueDateTime = returnDueDateTime.AddMinutes(diff);
                return returnDueDateTime;
            }
            else if (returnDueDateTime < specificOpeningTime)
            {
                var diff = (specificOpeningTime - returnDueDateTime).TotalMinutes;
                returnDueDateTime = returnDueDateTime.AddBusinessDays(-1, stateId.Value, context);
                returnDueDateTime = new DateTime(returnDueDateTime.Year, returnDueDateTime.Month, returnDueDateTime.Day, closingTime.Hour, closingTime.Minute, closingTime.Second);
                returnDueDateTime = returnDueDateTime.AddMinutes(-1 * diff);
                return returnDueDateTime;
            }
            else
            {
                return returnDueDateTime;
            }

        }
        //MatterWFComp Financial Methods

        public decimal GetLoanAmount(int matterWFComponentId)
        {
            var mwfComp = context.MatterWFComponents.FirstOrDefault(m => m.MatterWFComponentId == matterWFComponentId);
            if (mwfComp != null)
            {
                return mwfComp.LoanAmount ?? 0;
            }
            return 0;
        }

        public IEnumerable<MCE.MatterFinRetainedView> GetMatterFinRetained(int matterWFComponentId)
        {
            return context.MatterWFAmountsRetaineds.Where(x => x.MatterWFComponentId == matterWFComponentId)
                .Select(x => new MCE.MatterFinRetainedView
                {
                    MatterWFAmountsRetainedId = x.MatterWFAmountsRetainedId,
                    MatterId = x.MatterWFComponent.MatterId,
                    MatterWFComponentId = matterWFComponentId,
                    Description = x.Description,
                    Amount = x.Amount,
                    UpdatedDate = x.UpdatedDate,
                    UpdatedByUserId = x.UpdatedByUserId,
                    UpdatedByUsername = x.User.Username,
                    EFT_AccountName = x.EFT_AccountName,
                    EFT_AccountNo = x.EFT_AccountNo,
                    EFT_Reference = x.EFT_Reference,
                    EFT_BSB = x.EFT_BSB,
                    TransactionTypeId = x.TransactionTypeId,
                    TransactionTypeName = x.TransactionType.TransactionTypeName,
                    ParentTransactionTypeId = x.TransactionType.ParentTransactionTypeId,
                    PayableToOthersDesc = x.PayableToOthersDesc,
                    PayableToTypeId = x.PayableToTypeId,
                    IsDirty = false,
                    IsReadOnly = true
                });
        }

        public IEnumerable<MCE.MatterFinFundingView> GetMatterFinFunding(int matterWFComponentId)
        {
            var qry = context.MatterWFLedgerItems.Where(x => x.MatterWFComponentId == matterWFComponentId &&
                                                        x.PayableToAccountTypeId == (int)AccountTypeEnum.Trust &&
                                                        !x.IsCancelledFunding);
            return GetMatterFinFunding(qry);
        }
        public IEnumerable<MCE.MatterFinDisbursementView> GetMatterFinCancelled(int matterWFComponentId, bool showCancelledShortfall = false)
        {
            var qry = context.MatterWFLedgerItems.Where(x => x.MatterWFComponentId == matterWFComponentId &&
                                                        x.PayableByAccountTypeId == (int)AccountTypeEnum.Trust &&
                                                        x.IsCancelledFunding);

            if (showCancelledShortfall)
            {
                qry = qry.Where(x => x.Description.ToUpper().Contains("CANCELLED SHORTFALL"));
            }
            else
            {
                qry = qry.Where(x => !x.Description.ToUpper().Contains("CANCELLED SHORTFALL"));
            }

            return GetMatterFinDisbursements(qry);
        }
        public IEnumerable<MCE.MatterFinFundingView> GetMatterFinFunding(IQueryable<MatterWFLedgerItem> qry)
        {
            return qry
                .Select(x => new
                {
                    x.MatterWFLedgerItemId,
                    x.MatterWFComponent.MatterId,
                    x.MatterWFComponent.Matter.MatterGroupTypeId,
                    x.MatterWFComponentId,
                    x.TransactionTypeId,
                    x.TransactionType.TransactionTypeName,
                    x.Description,
                    x.PayableByTypeId,
                    x.PayableToOthersDesc,
                    PayableByTypeName = x.PayableType.PayableTypeName,
                    x.Amount,
                    x.GSTFree,
                    x.GST,
                    x.ExpectedPaymentDate,
                    x.Instructions,
                    x.MatterLedgerItemStatusTypeId,
                    x.FundsTransferredTypeId,
                    x.IsCancelledFunding,
                    x.UpdatedDate,
                    x.UpdatedByUserId,
                    UpdatedByUsername = x.User.Username,
                    x.FundingRequestId,
                    FundingRequestStatusTypeId = x.FundingRequest != null ? (int?)x.FundingRequest.FundingRequestStatusTypeId : (int?)null,
                    IsReadOnly = x.FundingRequest != null && x.FundingRequest.FundingRequestStatusTypeId == (int)FundingRequestStatusTypeEnum.Locked,
                    x.MatterWFComponent.Matter.LenderId
                }).ToList()
                .Select(x => new MCE.MatterFinFundingView(0, x.MatterId, x.MatterGroupTypeId,  x.MatterWFLedgerItemId, x.MatterWFComponentId, x.MatterLedgerItemStatusTypeId, x.FundsTransferredTypeId, x.TransactionTypeId, x.TransactionTypeName, x.PayableByTypeId,
                                        x.PayableToOthersDesc, x.Description, x.Amount, x.GSTFree, x.GST, x.ExpectedPaymentDate, x.ExpectedPaymentDate, x.Instructions, x.MatterLedgerItemStatusTypeId,
                                        x.IsCancelledFunding, 0, x.UpdatedDate, x.UpdatedByUserId, x.UpdatedByUsername, x.FundingRequestId, x.LenderId, readOnly: x.IsReadOnly, fundingRequestStatusTypeId: x.FundingRequestStatusTypeId)).OrderBy(x => x.DisplayOrder).ToList();
        }

        public IEnumerable<MCE.MatterFinDisbursementView> GetMatterFinDisbursements(int matterWFComponentId)
        {
            var qry = context.MatterWFLedgerItems.Where(x => x.MatterWFComponentId == matterWFComponentId &&
                                                        x.PayableByAccountTypeId == (int)AccountTypeEnum.Trust &&
                                                        !x.IsCancelledFunding &&
                                                        x.TransactionTypeId != (int)TransactionTypeEnum.Invoice);
            return GetMatterFinDisbursements(qry);
        }
        public IEnumerable<MCE.MatterFinDisbursementView> GetMatterFinDisbursementsCancelled(int matterWFComponentId)
        {
            var qry = context.MatterWFLedgerItems.Where(x => x.MatterWFComponentId == matterWFComponentId &&
                                                        x.PayableToAccountTypeId == (int)AccountTypeEnum.Trust &&
                                                        x.IsCancelledFunding &&
                                                        x.TransactionTypeId != (int)TransactionTypeEnum.Invoice);

            return GetMatterFinDisbursements(qry);

        }
        public IEnumerable<MCE.MatterFinDisbursementView> GetMatterFinDisbursements(IQueryable<MatterWFLedgerItem> qry)
        {
            return qry
                .Select(x => new
                {
                    x.MatterWFLedgerItemId,
                    x.MatterWFComponent.MatterId,
                    x.MatterWFComponent.Matter.Lender.LenderName,
                    x.MatterWFComponent.Matter.MortMgr.MortMgrName,
                    x.MatterWFComponent.Matter.MatterParties,
                    x.MatterWFComponentId,
                    x.MatterLedgerItemStatusTypeId,
                    x.TransactionTypeId,
                    x.TransactionType.TransactionTypeName,
                    x.PayableByAccountTypeId,
                    x.PayableByTypeId,
                    x.PayableByOthersDesc,
                    x.PayableToAccountTypeId,
                    x.PayableToTypeId,
                    x.PayableToOthersDesc,
                    x.EFT_AccountName,
                    x.EFT_BSB,
                    x.EFT_AccountNo,
                    x.EFT_Reference,
                    x.MatterLoanAccountId,
                    x.MatterLoanAccount.LoanAccountNo,
                    x.Amount,
                    x.GST,
                    x.GSTFree,
                    x.Description,
                    x.Instructions,
                    x.PaymentNotes,
                    x.FundsTransferredTypeId,
                    x.IsCancelledFunding,
                    x.UpdatedDate,
                    x.UpdatedByUserId,
                    UpdatedByUsername = x.User.Username,
                    IsDirty = false,
                    IsReadOnly = false,
                    x.LinkedToMatterId,
                    x.Matter.MatterDescription
                }).ToList()
                .Select(x => new MCE.MatterFinDisbursementView(0, x.MatterId, x.LenderName, x.MortMgrName, x.MatterParties, x.MatterWFLedgerItemId, x.MatterWFComponentId, x.TransactionTypeId,
                x.TransactionTypeName,
                x.PayableByAccountTypeId, x.PayableByTypeId, x.PayableByOthersDesc,
                x.PayableToAccountTypeId, x.PayableToTypeId, x.PayableToOthersDesc,
                x.EFT_AccountName, x.EFT_BSB, x.EFT_AccountNo, x.EFT_Reference, x.MatterLoanAccountId, x.LoanAccountNo, x.Amount, x.GST, x.GSTFree,
                x.Description, null, x.Instructions, x.PaymentNotes, x.FundsTransferredTypeId, false, x.MatterLedgerItemStatusTypeId, x.IsCancelledFunding, null, null, null, null, false, x.UpdatedDate,
                x.UpdatedByUserId, x.UpdatedByUsername, pLinkedToMatterId: x.LinkedToMatterId, pLinkedMatterDesc: x.MatterDescription)).OrderBy(x => x.DisplayOrder).ToList();
        }





        public void CheckSettlementPackVisibility(int matterId, int lenderId,
            ref bool displayEFTAuth, ref bool displayLetterToWestpac, ref bool displaySettlementInstructions, ref bool displayStampingAndRegistration, ref bool displayRedrawLetter, ref bool displayFastRefi, ref bool displayPostSettlementMortMgrLetter, ref bool displayEstimateCostsStatement)
        {
            var matterLedgerItems = new AccountsRepository(context).GetMatterFinDisbursements(matterId, false, true).ToList();

            displayEFTAuth = matterLedgerItems.Any(x => x.TransactionTypeId == (int)TransactionTypeEnum.BPay ||
                                                       x.TransactionTypeId == (int)TransactionTypeEnum.BPayFree ||
                                                       x.TransactionTypeId == (int)TransactionTypeEnum.EFT ||
                                                       x.TransactionTypeId == (int)TransactionTypeEnum.EFTFree ||
                                                       x.TransactionTypeId == (int)TransactionTypeEnum.TT ||
                                                       x.TransactionTypeId == (int)TransactionTypeEnum.TTFree
                                                       );

            displayLetterToWestpac = matterLedgerItems.Any(x => x.TransactionTypeId == (int)TransactionTypeEnum.Cheque ||
                                                               x.TransactionTypeId == (int)TransactionTypeEnum.ChequeFree);

            displaySettlementInstructions = matterLedgerItems.Any(x => x.TransactionTypeId == (int)TransactionTypeEnum.Cheque ||
                                                                      x.TransactionTypeId == (int)TransactionTypeEnum.ChequeFree ||
                                                                      x.TransactionTypeId == (int)TransactionTypeEnum.PPS ||
                                                                      x.TransactionTypeId == (int)TransactionTypeEnum.PPSFree);

            int? mortMgrId = context.Matters.FirstOrDefault(m => m.MatterId == matterId).MortMgrId;
            if (mortMgrId.HasValue)
            {
                displayPostSettlementMortMgrLetter = context.MortMgrs.FirstOrDefault(mm => mm.MortMgrId == mortMgrId).SendPostSettlementLetter;
                displayEstimateCostsStatement = context.MortMgrs.FirstOrDefault(mm => mm.MortMgrId == mortMgrId).SendEstimatedCostsStatement;
            }

            displayStampingAndRegistration = context.MatterSecurities.Any(x => x.MatterId == matterId && x.SettlementTypeId == (int)SettlementTypeEnum.Paper);
            displayFastRefi = context.MatterMatterTypes.Any(x => x.MatterId == matterId && x.MatterTypeId == (int)MatterTypeEnum.FastRefinance);
            //var hideRedraw = context.Lenders.FirstOrDefault(x => x.LenderId == lenderId)?.HideSettlementPackRedrawLetter;
            if (lenderId == 139)
            {
                displayRedrawLetter = matterLedgerItems.Any(x => x.Description.ToUpper().Contains("REDRAW") && !x.Description.ToUpper().Contains("ADDITIONAL REDRAW FROM LENDER"));
            }
        }


        public void MarkOffOutstandingRequirements(List<int> resolvedOutstandingReqItemIds, int updatedByUserId)
        {

            MatterWFOutstandingReq matterWFOutstandingReq = null;
            foreach (var outstanding in resolvedOutstandingReqItemIds)
            {
                var oi = context.MatterWFOutstandingReqItems.FirstOrDefault(o => o.MatterWFOutstandingReqItemId == outstanding);
                oi.IsIssue = false;
                oi.IssueResolvedDate = DateTime.Now;
                oi.Resolved = true;
                oi.UpdatedByUserId = updatedByUserId;
                oi.UpdatedDate = DateTime.Now;
                matterWFOutstandingReq = oi.MatterWFOutstandingReq;

            }
            context.SaveChanges();
            GlobalVars.CurrentUser.UserId = updatedByUserId;
            if (matterWFOutstandingReq.MatterWFOutstandingReqItems.All(x => x.Resolved || x.IsDeleted)) //all these outstanding requirements are resolved, let's try finish that milestone.
            {
                var mwfView = GetMatterWFComponentView(matterWFOutstandingReq.MatterWFComponentId);
                ProgressToNextMileStone(mwfView, sendEmails: true, forceNoReply: true, updatedByUserId: updatedByUserId);
                UpdateDueDates(matterWFOutstandingReq.MatterWFComponent.MatterId);
            }
            context.SaveChanges();
            GlobalVars.CurrentUser.UserId = Slick_Domain.Common.DomainConstants.SystemUserId;
        }

    }
}
