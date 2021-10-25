using System;
using System.Collections.Generic;
using System.Linq;
using Slick_Domain.Models;
using Slick_Domain.Interfaces;
using Slick_Domain.Entities;
using Slick_Domain.Common;
using Slick_Domain.Enums;
using Check = System.Predicate<object>;
using DocuSign.eSign.Model;
using Newtonsoft.Json;
using static Slick_Domain.Entities.FeedbackCustomEntities;
using Slick_Domain.Extensions;
using System.Threading.Tasks;
using System.Collections.ObjectModel;

namespace Slick_Domain.Services
{
    public class FeedbackRepository : IDisposable
    {
        private readonly SlickContext context;

        public FeedbackRepository(SlickContext Context)
        {
            context = Context;
        }

        public IEnumerable<MSAFeedbackDetailsView> GetMSAFeedbackDetailsGridViews(IQueryable<MSAFeedback> qry)
        {
            return qry.Select(f => new MSAFeedbackDetailsView()
            {
                MSAFeedbackId = f.MSAFeedbackId, 
                IssueCategoryTypeId = f.IssueCategoryTypeId,
                IssueCategoryTypeName = f.Reason.ReasonTxt,
                FeedbackStatusTypeId = f.FeedbackStatusTypeId,
                FeedbackStatusTypeName = f.FeedbackStatusType.FeedbackStatusTypeName,
                RelatesToMatterId = f.RelatesToMatterId,
                RelatesToMatterBroker = (f.Matter.Broker.PrimaryContact.Firstname + " " + f.Matter.Broker.PrimaryContact.Lastname).Trim(),
                RelatesToMatterDesc = f.Matter.MatterDescription,
                RelatesToMatterLender = f.Matter.Lender.LenderName,
                RelatesToMatterMortMgr = f.Matter.MortMgr.MortMgrName,
                RelatesToMatterOtherParty = f.Matter.MatterOtherParties.FirstOrDefault().Name,
                RaisedAgainstTypeId = f.RaisedAgainstTypeId,
                RaisedAgainstTypeName = f.FeedbackAgainstType.FeedbackAgainstTypeName,
                RaisedAgainstUserId = f.RaisedAgainstUserId,
                RaisedAgainstUserName = f.User.Fullname,
                RaisedAgainstTeamId = f.RaisedAgainstTeamId,
                RaisedAgaintTeamName = f.Team.TeamName,
                RaisedAgainstOtherDesc = f.RaisedAgainstOtherDesc,
                RaisedByTypeId = f.RaisedByTypeId,
                RaisedByTypeName = f.FeedbackRaisedByType.FeedbackRaisedByTypeName,
                RaisedAgainstSummary = ((f.RaisedAgainstUserId.HasValue? f.User.Fullname : f.RaisedAgainstTeamId.HasValue? f.Team.TeamName : f.RaisedAgainstOtherDesc ?? "")),
                AssignedToUserId = f.AssignedToUserId,
                AssignedToUserName = f.User1.Fullname ?? "- UNASSIGNED -",
                UpdatedByUserId = f.UpdatedByUserId,
                UpdatedByUserName = f.User2.Fullname,
                UpdatedDate = f.UpdatedDate,
                Deleted = f.Deleted,
            }).ToList().OrderByDescending(o=>o.UpdatedDate);
        }
        public MSAFeedbackDetailsView GetMSAFeedbackDetailsView(int msaFeedbackId)
        {
            return context.MSAFeedbacks.Where(f=>f.MSAFeedbackId == msaFeedbackId).Select(f => new MSAFeedbackDetailsView()
            {
                MSAFeedbackId = f.MSAFeedbackId,
                IssueCategoryTypeId = f.IssueCategoryTypeId,
                IssueCategoryTypeName = f.Reason.ReasonTxt,
                FeedbackStatusTypeId = f.FeedbackStatusTypeId,
                FeedbackStatusTypeName = f.FeedbackStatusType.FeedbackStatusTypeName,
                RaisedByTypeName = f.FeedbackRaisedByType.FeedbackRaisedByTypeName,
                RelatesToMatterId = f.RelatesToMatterId,
                LinkedToMatter = f.RelatesToMatterId.HasValue,
                RelatesToMatterBroker = (f.Matter.Broker.PrimaryContact.Firstname + " " + f.Matter.Broker.PrimaryContact.Lastname).Trim(),
                RelatesToMatterDesc = f.Matter.MatterDescription,
                RelatesToMatterLender = f.Matter.Lender.LenderName,
                RelatesToMatterMortMgr = f.Matter.MortMgr.MortMgrName,
                RelatesToMatterOtherParty = f.Matter.MatterOtherParties.FirstOrDefault().Name,
                RaisedAgainstTypeId = f.RaisedAgainstTypeId,
                RaisedAgainstTypeName = f.FeedbackAgainstType.FeedbackAgainstTypeName,
                RaisedAgainstUserId = f.RaisedAgainstUserId,
                RaisedAgainstUserName = f.User.Fullname,
                RaisedAgainstTeamId = f.RaisedAgainstTeamId,
                RaisedAgaintTeamName = f.Team.TeamName,
                RaisedAgainstOtherDesc = f.RaisedAgainstOtherDesc,
                RaisedByTypeId = f.RaisedByTypeId,
                RaisedAgainstSummary = ((f.RaisedAgainstUserId.HasValue ? f.User.Fullname : f.RaisedAgainstTeamId.HasValue ? f.Team.TeamName : f.RaisedAgainstOtherDesc ?? "")),
                AssignedToUserId = f.AssignedToUserId,
                AssignedToUserName = f.User2.Fullname ?? "- UNASSIGNED -",
                IssueNotes = f.IssueNotes,
                IssueSummary = f.IssueSummary,
                UpdatedByUserId = f.UpdatedByUserId,
                UpdatedByUserName = f.User2.Fullname,
                UpdatedDate = f.UpdatedDate,
                Deleted = f.Deleted,
                FeedbackDocuments = f.MSAFeedbackDocuments.Where(d=>!d.Deleted).Select(d => new MSAFeedbackDocumentView()
                {
                    MSAFeedbackDocumentId = d.MSAFeedbackDocumentId,
                    MSAFeedbackId = f.MSAFeedbackId,
                    DocumentDisplayName = d.DocumentDisplayName,
                    DocType = d.DocType,
                    Deleted = d.Deleted,
                    UpdatedByUserId = d.UpdatedByUserId,
                    UpdatedByUsername = d.User.Username,
                    UpdatedDate = d.UpdatedDate,
                }).ToList(),
                FeedbackTimeline = f.MSAFeedbackTimelines.Select(t => new MSAFeedbackTimelineView()
                {
                    MSAFeedbackId = f.MSAFeedbackId,
                    EventDescription = t.EventDescription,
                    TimelineEventTypeId = t.TimelineEventTypeId,
                    TimelineEventTypeName = t.Reason.ReasonTxt,
                    MSAFeedbackTimelineId = t.MSAFeedbackTimelineId,
                    TimelineDate = t.TimelineDate,
                    UpdatedByUsername = t.User.Username,
                    UpdatedByUserId = t.UpdatedByUserId,
                    UpdatedDate = t.UpdatedDate
                }).ToList().OrderBy(o=>o.TimelineDate).ToList(),
                ResolutionDetails = f.MSAFeedbackResolutions.Select(r => new MSAFeedbackResolutionView()
                {
                    MSAFeedbackId = f.MSAFeedbackId,
                    MSAFeedbackResolutionId = r.MSAFeedbackResolutionId,
                    ResolutionCategoryTypeId = r.ResolutionCategoryTypeId,
                    ResolutionCategoryTypeName = r.Reason.ReasonTxt,
                    ResolutionCategoryDesc = r.ResolutionCategoryDesc,
                    ResolutionActionTypeName = r.Reason1.ReasonTxt,
                    ResolutionActionTypeId = r.ResolutionActionTypeId,
                    ResolutionActionDesc = r.ResolutionActionDesc,
                    UpdatedByUserId = r.UpdatedByUserId,
                    UpdatedDate = r.UpdatedDate
                }).ToList()
            }).FirstOrDefault();
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
        
        

    }
}
