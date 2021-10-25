using Slick_Domain.Models;
using System;
using System.Linq;
using Slick_Domain.Entities.Milestones;
using System.Collections.Generic;
using Slick_Domain.Entities;

namespace Slick_Domain.Services
{
    public class MilestonesRepository : SlickRepository
    {
        public MilestonesRepository(SlickContext Context) :base(Context)
        {
        }

        public IEnumerable<IssueReasonEntity> GetReasons(int reasonGroupTypeId)
        {
            return context.Reasons.
                Where(x=> x.ReasonGroupTypeId == reasonGroupTypeId)
                .Select(mr => new IssueReasonEntity
                {
                    IssueReasonText = mr.ReasonTxt,
                    IssueReasonId = mr.ReasonId,
                    LenderId = mr.LenderId,
                    UpdatedByUserId = mr.UpdatedByUserId,
                    UpdatedByUsername = mr.User.Username,
                    Highlight = mr.Highlight,
                    UpdatedDate = mr.UpdatedDate,
                    IsEnabled = mr.Enabled
                }).ToList();
        }

        public IEnumerable<IssueActionEntity> GetActionTexts()
        {
            return context.IssueActions.
                Select(ma => new IssueActionEntity
                {
                    ActionText = ma.ActionText,
                    ActionId = ma.IssueActionId,
                    UpdatedByUserId = ma.UpdatedByUserId,
                    UpdatedByUsername = ma.User.Username,
                    UpdatedDate = ma.UpdatedDate,
                    IsEnabled = ma.Enabled
                }).ToList();
        }

        public IEnumerable<MilestoneIssueActionEntity> GetMilestoneIssueActions()
        {
            return GetMilestoneIssueActions(context.MilestoneIssueActions);
        }
        public IEnumerable<MilestoneIssueActionEntity> GetMilestoneIssueActions(int matterId, int wfComponentId)
        {
            return GetMilestoneIssueActions(MilestoneIssueActionsQry(matterId, wfComponentId));
        }

        public IEnumerable<EntityCompacted> GetMilestoneIssueReasonsCompacted(int matterId, int wfComponentId)
        {
            return MilestoneIssueActionsQry(matterId, wfComponentId)
                .Select(x=> new EntityCompacted {Id = x.IssueReasonId, Details = x.Reason.ReasonTxt}).Distinct();
        }

        private IQueryable<MilestoneIssueAction> MilestoneIssueActionsQry(int matterId, int wfComponentId)
        {
            var settlementTypes = context.MatterSecurities.Where(x => x.MatterId == matterId && !x.Deleted)
                .Select(m => m.SettlementTypeId).ToList();

            return context.MilestoneIssueActions
                .Where(x => x.CreatedWFComponentId == wfComponentId && (x.SettlementTypeId == null || settlementTypes.Contains(x.SettlementTypeId.Value)));
        }

        private IEnumerable<MilestoneIssueActionEntity> GetMilestoneIssueActions(IQueryable<MilestoneIssueAction> issueActions)
        {
            return issueActions
                    .Select(mi => new MilestoneIssueActionEntity
                    {
                        IssueActionId = mi.IssueActionId,
                        IssueActionText = mi.IssueAction.ActionText,
                        IssueReasonId = mi.IssueReasonId,
                        IssueReasonText = mi.Reason.ReasonTxt,
                        MilestoneIssueActionId = mi.MilestoneIssueActionId,
                        NextActionStepId = mi.NextActionStepId,
                        NextActionStep = mi.NextActionStep.NextActionStepName,
                        CreatedWFComponentId = mi.CreatedWFComponentId,
                        DisplayedWFComponentId = mi.DisplayedWFComponentId,
                        CreatedWFComponentName = mi.WFComponent.WFComponentName,
                        DisplayedWFComponentName = mi.WFComponent1.WFComponentName,
                        RestartWFComponentId = mi.RestartWFComponentId,
                        RestartWFComponentName = mi.WFComponent2.WFComponentName,
                        SettlementTypeId = mi.SettlementTypeId,
                        SettlementTypeName = mi.SettlementType.SettlementTypeName,
                        OnHold = mi.PlaceMatterOnHold,
                        UpdatedByUserId = mi.UpdatedByUserId,
                        UpdatedDate = mi.UpdatedDate,
                        UpdatedByUsername = mi.User.Username
                    })
                    .OrderBy(o=>o.IssueReasonText)
                    .ToList();
        }
        public IEnumerable<SuggestedDocNameEntity> GetSuggestedDocNames(bool ShowDisabledFlag)
        {
            if (ShowDisabledFlag)
                return context.SuggestedDocNames.Select(x => new SuggestedDocNameEntity { SuggestedDocNameId = x.SuggestedDocNameId, DocumentName = x.DocumentName, Enabled = x.Enabled, LenderId = x.LenderId ?? -1, QADocPrepRequired = x.QADocPrepRequired, QASettlementInstructionsRequired = x.QASettlementInstructionsRequired, QASettlementRequired = x.QASettlementRequired }).Distinct();
            else
                return context.SuggestedDocNames.Where(x=>x.Enabled == true).Select(x => new SuggestedDocNameEntity { SuggestedDocNameId = x.SuggestedDocNameId, DocumentName = x.DocumentName, LenderId = x.LenderId ?? -1, Enabled = x.Enabled, QADocPrepRequired = x.QADocPrepRequired, QASettlementInstructionsRequired = x.QASettlementInstructionsRequired, QASettlementRequired = x.QASettlementRequired }).Distinct();
        }
        public IEnumerable<MatterWFIssueEntity> GetDisplayedMatterWFIssues(int matterId, int wfComponentId)
        {
            return GetMatterWFIssues(context.MatterWFIssues.Where(x => x.MatterWFComponent.MatterId == matterId && (x.DisplayedWFComponentId == wfComponentId || ((wfComponentId == (int)Slick_Domain.Enums.WFComponentEnum.QASettlement || wfComponentId == (int)Slick_Domain.Enums.WFComponentEnum.DocPreparationQA) && x.MatterWFComponent.WFComponentId == wfComponentId))));
        }

        public IEnumerable<MatterWFIssueEntity> GetCreatedMatterWFIssues(int _matterWFComponentId)
        {
            return GetMatterWFIssues(context.MatterWFIssues.Where
                (x => x.RaisedMatterWFComponentId == _matterWFComponentId && x.DisplayedMatterWFComponentId != _matterWFComponentId));
        }

        private IEnumerable<MatterWFIssueEntity> GetMatterWFIssues(IQueryable<MatterWFIssue> issues)
        {
            return issues.
                Select(i => new MatterWFIssueEntity
                {
                    MatterWFIssueId = i.MatterWFIssueId,
                    ActionStepId = i.ActionStepId,
                    ActionStepName = i.NextActionStep.NextActionStepName,
                    RaisedMatterWFComponentId = i.RaisedMatterWFComponentId,
                    DisplayMatterWFComponentId = i.DisplayedMatterWFComponentId,
                    HistoricalMatterWFComponentId = i.HistoricalMatterWFComponentId,
                    IsMatterOnHold = i.MatterOnHold,
                    IssueActionId = i.IssueActionId,
                    IssueActionText = i.IssueActionText,
                    IssueReasonId = i.IssueReasonId,
                    IssueReasonText = i.IssueReasonText,
                    DisplayedWFComponentId = i.DisplayedWFComponentId,
                    DisplayedWFComponentName = i.WFComponent.WFComponentName,
                    RestartWFComponentId = i.RestartWFComponentId,
                    RestartWFComponentName = i.WFComponent1.WFComponentName,
                    Notes = i.Notes,
                    Resolved = i.Resolved,
                    ResolvedBy = i.User2.Username,
                    ResolvedByUserId = i.ResolvedByUserId,
                    ResolvedDate = i.ResolvedDate,
                    FollowUpDate = i.FollowUpDate,
                    UpdatedByUserId = i.UpdatedByUserId,
                    UpdatedByUsername = i.User1.Username,
                    UpdatedDate = i.UpdatedDate,
                    IssueType = i.IssueTypeId,
                    IssueRegistrationType = i.IssueRegistrationType.IssueRegistrationTypeName,
                    IssueRegistrationOtherDesc = i.IssueRegistrationOtherDesc,
                    WFRequisitionId = i.MatterWFConfirmRegistrationId,
                    RequisitionDate = i.MatterWFConfirmRegistration.RequisitionDate,
                    RequisitionAmount = i.MatterWFConfirmRegistration.RegistrationAmount,
                    RequisitionExpiryDate = i.MatterWFConfirmRegistration.RegistrationRequisitionExpiry,
                    RequisitionCaveatLodged = i.MatterWFConfirmRegistration.RegistrationCaveatLodged
                }).ToList();
        }
    }
}
