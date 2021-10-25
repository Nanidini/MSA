using Slick_Domain.Common;
using Slick_Domain.Entities;
using Slick_Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Slick_Domain.Services
{
    public class OutstandingRequirementsRepository : SlickRepository
    {
        public OutstandingRequirementsRepository(SlickContext context) : base(context)
        {
        }
        public IEnumerable<PossibleOutstandingItemView> GetUserFavouriteOutstandings(int userId)
        {
            return context.sp_Slick_GetUserFavouriteOutstandings(userId).Select(x => new PossibleOutstandingItemView { ItemDescription = x.OutstandingItemName });
        }
        public IEnumerable<PossibleOutstandingItemView> GetPossibleOutstandingsForMatter(MatterCustomEntities.MatterWFComponentView matterWFCompView)
        {

            var matterTypes = context.MatterMatterTypes.Where(x => x.MatterId == matterWFCompView.MatterId)
                .DistinctBy(y => y.MatterTypeId)
                .Select(x=> (int?)x.MatterTypeId).ToList();

            var secs = context.MatterSecurities.Where(x => x.MatterId == matterWFCompView.MatterId)
                .Select(x => (int?)x.SettlementTypeId).Distinct().ToList();

            var secStates = context.MatterSecurities.Where(x => x.MatterId == matterWFCompView.MatterId)
                .Select(y => y.StateId).Distinct().ToList();

            var outstandingItems = context.OutstandingItems.Where(
                x => (x.LenderId == null || x.LenderId == matterWFCompView.LenderId) &&
                     (x.MatterTypeId == null || matterTypes.Contains(x.MatterTypeId) &&
                     (x.MortMgrId == null || x.MortMgrId == matterWFCompView.MortMgrId) &&
                     (x.SettlementTypeId == null || secs.Contains(x.SettlementTypeId)) &&
                     (x.StateId == null || x.StateId == matterWFCompView.StateId)
                     ))
                 .Select(y => new PossibleOutstandingItemView
                 {
                     ItemType = y.OutstandingItemTypeId,
                     ItemDescription = y.ItemDesc
                 })
                 .ToList();
            

            return outstandingItems;
        }

        public IEnumerable<PossibleOutstandingItemAdminView> GetPossibleOutstandingItemsView()
        {
            return context.OutstandingItems.AsNoTracking()
                .Select(s => new
                {
                    s.OutstandingItemId,
                    s.OutstandingItemTypeId,
                    s.OutstandingItemType.OutstandingItemTypeName,
                    s.ItemDesc,
                    s.LenderId,
                    s.Lender.LenderName,
                    s.MatterTypeId,
                    s.MatterType.MatterTypeName,
                    s.MortMgrId,
                    s.MortMgr.MortMgrName,
                    s.SettlementTypeId,
                    s.SettlementType.SettlementTypeName,
                    s.StateId,
                    s.State.StateName,
                    s.UpdatedByUserId,
                    s.User.Username,
                    s.UpdatedDate
                })
                .ToList()
                .Select(s2 => new PossibleOutstandingItemAdminView
                {
                    Id = s2.OutstandingItemId,
                    ItemDescription = s2.ItemDesc,
                    LenderId = s2.LenderId ?? DomainConstants.AnySelectionId,
                    LenderName = s2.LenderName ?? "-- Any Lender --",
                    MatterTypeId = s2.MatterTypeId ?? DomainConstants.AnySelectionId,
                    MatterTypeName = s2.MatterTypeName ?? "-- Any Matter Type --",
                    MortMgrId = s2.MortMgrId ?? DomainConstants.AnySelectionId,
                    MortMgrName = s2.MortMgrName ?? "-- Any Mort Mgr --",
                    OutstandingItemTypeId = s2.OutstandingItemTypeId,
                    OutstandingItemTypeName = s2.OutstandingItemTypeName,
                    SettlementTypeId = s2.SettlementTypeId ?? DomainConstants.AnySelectionId,
                    SettlementTypeName = s2.SettlementTypeName ?? "-- Any Settlement Type --",
                    StateId = s2.StateId ?? DomainConstants.AnySelectionId,
                    StateName = s2.StateName ?? "-- Any State --",
                    UpdatedById = s2.UpdatedByUserId,
                    UpdatedByUserName = s2.Username,
                    UpdatedDate = s2.UpdatedDate,
                    isDirty = false
                }).ToList();
        }

        public IEnumerable<OutstandingItemView> GetOutstandingItemsForMWF(int matterWFComponentId)
        {
            var items = context.MatterWFOutstandingReqItems.Where(x => x.MatterWFOutstandingReq.MatterWFComponent.MatterWFComponentId == matterWFComponentId);
            return GetOutstandingItemsForMatter(items);
        }


        public IEnumerable<OutstandingItemView> GetPreviousOutstandingResolvedItemsForMatter(int matterId, int matterWFComponentId)
        {
            var items = from ori in context.MatterWFOutstandingReqItems
                        join or in context.MatterWFOutstandingReqs on ori.MatterWFOutstandingReqId equals or.MatterWFOutstandingReqId
                        join w in context.MatterWFComponents on or.MatterWFComponentId equals w.MatterWFComponentId
                        where w.MatterId == matterId && w.MatterWFComponentId != matterWFComponentId
                        select ori;

            return GetOutstandingItemsForMatter(items);
        }

        private IEnumerable<OutstandingItemView> GetOutstandingItemsForMatter(IQueryable<MatterWFOutstandingReqItem> items)
        {
            return items
                .Select(x => new OutstandingItemView
                {
                    IsIssue = x.IsIssue,
                    IsDeleted = x.IsDeleted,
                    IssueCreatedDate = x.IssueCreatedDate,
                    IssueDetails = x.IssueDetails,
                    MatterWFSecurityId = x.MatterWFSecurityId,
                    OutstandingItemName = x.OutstandingItemName,
                    OutstandingItemId = x.MatterWFOutstandingReqItemId,
                    MatterWFComponentId = x.MatterWFOutstandingReq.MatterWFComponentId,
                    Resolved = x.Resolved,
                    ResponsiblePartyId = x.OutstandingResponsiblePartyTypeId ?? -1,
                    IssueResolvedDate = x.IssueResolvedDate,
                    UpdatedDate = (DateTime?) x.UpdatedDate,
                    UpdatedByUsername = x.User.Username,
                    isDirty = false
                });
        }

        public MatterWFOutstandingReq GetOutstandingRequirementForMatter(int matterWFComponentId)
        {
            return context.MatterWFOutstandingReqs
                .Where(x => x.MatterWFComponentId == matterWFComponentId).FirstOrDefault();
        }

        public IEnumerable<EntityCompacted> GetCurrentSecurities(int matterId, int displayOrder)
        {
            var wfSecurities = new MatterWFRepository(context).GetLastDocPrepSecurityItemsForMatter(matterId, displayOrder).ToList();

            if (wfSecurities == null || !wfSecurities.Any()) return null;

            var securities =
            wfSecurities.Select(x => new EntityCompacted
            {
                Id = x.MatterWFProcDocsSecurityId,
                Details = EntityHelper.FormatAddressDetails(x.StreetAddress, x.Suburb, x.State.StateName, x.PostCode)
            }).ToList();

            securities.Insert(0, new EntityCompacted { Id = DomainConstants.AnySelectionId, Details = "-- Not Applicable --" });

            if (wfSecurities == null) return null;

            return securities;
        }

        

    }
}
