using Slick_Domain.Common;
using Slick_Domain.Entities;
using Slick_Domain.Interfaces;
using Slick_Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using Slick_Domain.Enums;
using System.Data.Entity;

namespace Slick_Domain.Services
{
    public class SettlementRepository : SlickRepository
    {
        public SettlementRepository(SlickContext context) : base(context)
        {
        }



        private IEnumerable<MatterCustomEntities.MatterFinFundingView> StringToFundingAmount(string raw)
        {
            List<MatterCustomEntities.MatterFinFundingView> output = new List<MatterCustomEntities.MatterFinFundingView>();

            foreach(var row in raw.Split('~'))
            {
                try
                {
                    var amount = Convert.ToDecimal(row.Split('|')[0]);
                    var description = row.Split('|')[1];
                    output.Add(new MatterCustomEntities.MatterFinFundingView() { Amount = amount, Description = description });
                }
                catch(Exception)
                {
                    output.Add(new MatterCustomEntities.MatterFinFundingView() { Amount = 0.00M, Description = "Error mapping from db: " + row   });
                }
            }
            return output;
        }
        public IEnumerable<MatterCustomEntities.MatterSettlementListView> GetMatterSettlementListViewSproc(int? userId, int? stateId, int? settlementVenueId, DateTime? settlementDate, bool showRegional, int matterGroupTypeId, bool includeSettled, bool includeCancelled)
        {
            return context.sp_Slick_GetSettlementCalendar(userId, stateId, settlementVenueId, settlementDate, showRegional, matterGroupTypeId, includeSettled, includeCancelled).
                GroupBy(s=>s.SettlementScheduleId).Select(x=>x.First()).ToList().Select(m=>
                 new MatterCustomEntities.MatterSettlementListView
                    (
                        m.SettlementScheduleId,
                        m.MatterId,
                        m.MatterTypeGroupId ?? matterGroupTypeId,
                        m.LenderId,
                        m.LenderName,
                        m.MatterDescription,
                        m.LenderRefNo,
                        StringToFundingAmount(m.FundingAmounts).ToList(),
                        m.SettlementDate,
                        m.SettlementTime,
                        m.MatterTypesNames,
                        m.StateId,
                        m.StateName,
                        m.SettlementType,
                        m.PexaWorkspaces,
                        m.PexaWorkspaceStatuses,
                        m.ActiveMilestones,
                        (m.ReconciledDeposits ?? 0) - (m.ReconciledPayments ?? 0),
                        m.FileownerUserId,
                        m.Fullname,
                        m.ReadyForAccounts == 1 ? true : false,
                        m.InternalNotes,
                        m.HighlightColour,
                        m.MatterStatusTypeId != (int)MatterStatusTypeEnum.Settled && m.SettlementScheduleStatusTypeId == (int)SettlementScheduleStatusTypeEnum.NotSettled,
                        m.SettlementScheduleStatusTypeId,
                        m.SettlementScheduleStatusTypeName,
                        m.SettlementCompleteMatterWFComponentId > 0 ? m.SettlementCompleteMatterWFComponentId : (int?)null,
                        m.MatterStatusTypeId,
                        m.MatterStatusTypeName,
                        m.SettlementCompleteMatterWFModuleId > 0 ? m.SettlementCompleteMatterWFModuleId : (int?)null,
                        m.SecurityDetails,
                        m.StateNames,
                        ""
                    )
                 ).ToList();


        }

        public IEnumerable<MatterCustomEntities.MatterSettlementListView> GetMatterSettlementListView(int? userId, int? stateId, int? settlementVenueId, DateTime? settlementDate, bool showRegional, int matterGroupTypeId, bool includeSettled, bool includeCancelled)
        {
            IQueryable<SettlementSchedule> qry = context.SettlementSchedules.AsNoTracking()
                .Where(s => s.Matter.MatterGroupTypeId == matterGroupTypeId
                && (userId == null || s.Matter.FileOwnerUserId == userId.Value)
                && (stateId == null || s.Matter.StateId == stateId.Value)
                && (!settlementDate.HasValue || s.Matter.SettlementSchedule.SettlementDate == settlementDate.Value)
                && (showRegional || s.SettlementScheduleVenues.Any(x => x.SettlementRegionTypeId != (int)SettlementRegionTypeEnum.Regional))
                && (settlementVenueId == null || s.SettlementScheduleVenues.Any(x => x.SettlementVenueId == settlementVenueId.Value))
                && (includeSettled || s.SettlementScheduleStatusTypeId != (int)SettlementScheduleStatusTypeEnum.Settled)
                && (includeCancelled || s.SettlementScheduleStatusTypeId != (int)SettlementScheduleStatusTypeEnum.Cancelled && s.SettlementScheduleStatusTypeId != (int)SettlementScheduleStatusTypeEnum.Rebooked && s.SettlementScheduleStatusTypeId != (int)SettlementScheduleStatusTypeEnum.Deleted && s.SettlementScheduleStatusTypeId != (int)SettlementScheduleStatusTypeEnum.Failed));
            //if (userId.HasValue) qry = qry.Where(q => q.Matter.FileOwnerUserId == userId.Value);
            //if (stateId.HasValue) qry = qry.Where(q => q.Matter.StateId == stateId.Value);
            //if (settlementDate.HasValue) qry = qry.Where(q => q.SettlementDate == settlementDate.Value);
            //if (!showRegional) qry = qry.Where(s => s.SettlementScheduleVenues.Any(x => x.SettlementRegionTypeId != (int)SettlementRegionTypeEnum.Regional));
            //if (settlementVenueId.HasValue) qry = qry.Where(s => s.SettlementScheduleVenues.Any(v => v.SettlementVenueId == settlementVenueId.Value));
            //if (!includeSettled) qry = qry.Where(s => s.SettlementScheduleStatusTypeId == (int)SettlementScheduleStatusTypeEnum.NotSettled);
            //if (!includeCancelled) qry = qry.Where(s => s.SettlementScheduleStatusTypeId != (int)SettlementScheduleStatusTypeEnum.Cancelled && s.SettlementScheduleStatusTypeId != (int)SettlementScheduleStatusTypeEnum.Rebooked && s.SettlementScheduleStatusTypeId != (int)SettlementScheduleStatusTypeEnum.Deleted && s.SettlementScheduleStatusTypeId != (int)SettlementScheduleStatusTypeEnum.Failed);
            
            return qry.
                Select(m => new
                {
                    m.SettlementScheduleId,
                    m.MatterId,
                    m.Matter.MatterGroupTypeId,
                    m.SettlementDate,
                    m.SettlementTime,
                    m.Matter.MatterStatusTypeId,
                    m.Matter.MatterStatusType.MatterStatusTypeName,
                    m.SettlementScheduleStatusTypeId,
                    m.Matter.LenderId,
                    m.Matter.Lender.LenderName,
                    m.Matter.MatterDescription,
                    m.Matter.LenderRefNo,
                    m.Matter.SecondaryRefNo,
                    m.InternalNotes,
                    FundingAmounts = m.Matter.MatterLedgerItems.Where(x => x.MatterLedgerItemStatusTypeId != (int)MatterLedgerItemStatusTypeEnum.Cancelled && x.PayableToAccountTypeId == (int)AccountTypeEnum.Trust
                                                        && !x.ParentMatterLedgerItemId.HasValue).Select(x=>new MatterCustomEntities.MatterFinFundingView() { Description = x.Description, Amount = x.Amount }),
                    MatterTypes = m.Matter.MatterMatterTypes.Select(a => a.MatterType.MatterTypeName),
                    m.Matter.StateId,
                    m.Matter.State.StateName,
                    PexaWorkspaces = m.Matter.MatterPexaWorkspaces.Where(p => p.MatterSecurityMatterPexaWorkspaces.Any(x => !x.MatterSecurity.Deleted)).Select(p => p.PexaWorkspace.PexaWorkspaceNo),
                    PexaWorkspaceStatuses = m.Matter.MatterPexaWorkspaces.Where(p => p.MatterSecurityMatterPexaWorkspaces.Any(x => !x.MatterSecurity.Deleted)).Select(p => p.PexaWorkspace.PexaWorkspaceStatusType.PexaWorkspaceStatusTypeName),
                    ActiveMilestones = m.Matter.MatterWFComponents.Where(c => c.WFComponentId != (int)WFComponentEnum.CreatePSTPacket && c.WFComponentId != (int)WFComponentEnum.PrintAndCollateFile && (c.WFComponentStatusTypeId == (int)Enums.MatterWFComponentStatusTypeEnum.InProgress || c.WFComponentStatusTypeId == (int)Enums.MatterWFComponentStatusTypeEnum.Starting)
                                                                                                        && (c.DisplayStatusTypeId == (int)Enums.DisplayStatusTypeEnum.Default || c.DisplayStatusTypeId == (int)Enums.DisplayStatusTypeEnum.Display))
                                                                                                        .Select(c => c.WFComponent.WFComponentName),
                    ReconciledDeposits = m.Matter.TrustTransactionItems.Where(t => t.TransactionDirectionTypeId == (int)TransactionDirectionTypeEnum.Deposit &&
                                                        (t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Flagged ||
                                                         t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Reconciled ||
                                                         t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Journalled))
                                               .Select(t => t.Amount).DefaultIfEmpty(0).Sum(),
                    ReconciledPayments = m.Matter.TrustTransactionItems.Where(t => t.TransactionDirectionTypeId == (int)TransactionDirectionTypeEnum.Payment &&
                                                        (t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Flagged ||
                                                         t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Reconciled ||
                                                         t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Journalled))
                                               .Select(t => t.Amount).DefaultIfEmpty(0).Sum(),
                    FileOwnerUserId = m.Matter.FileOwnerUserId,
                    FileOwnerUserName = m.Matter.User.Firstname + " " + m.Matter.User.Lastname,
                    SettlementType = m.Matter.MatterSecurities.Where(x=>!x.Deleted).All(t=>t.SettlementTypeId == (int)SettlementTypeEnum.Paper) ? "Paper" : m.Matter.MatterSecurities.Where(x => !x.Deleted).All(t => t.SettlementTypeId == (int)SettlementTypeEnum.PEXA) ? "PEXA" : "Paper / PEXA",
                    ReadyForAccounts = !m.Matter.MatterWFComponents.Any(c => (c.WFComponentStatusTypeId == (int)Enums.MatterWFComponentStatusTypeEnum.InProgress || c.WFComponentStatusTypeId == (int)Enums.MatterWFComponentStatusTypeEnum.Starting)
                                                                                                        && (c.DisplayStatusTypeId == (int)Enums.DisplayStatusTypeEnum.Default || c.DisplayStatusTypeId == (int)Enums.DisplayStatusTypeEnum.Display)
                                                                                                        && c.WFComponent.AccountsStageId == (int)AccountsStageEnum.NotReadyForAccounts),
                    m.HighlightColour,
                    m.SettlementScheduleStatusType.SettlementScheduleStatusTypeName,
                    SettlementCompleteMatterWFComponentId = m.Matter.MatterWFComponents.Where(c => ((m.Matter.MatterGroupTypeId == (int)MatterGroupTypeEnum.Discharge && c.WFComponentId == (int)WFComponentEnum.SettlementCompletedDischarge) || (m.Matter.MatterGroupTypeId == (int)MatterGroupTypeEnum.NewLoan && c.WFComponentId == (int)WFComponentEnum.SettlementCompleteNewLoans))
                                                                                                        && (c.WFComponentStatusTypeId == (int)Enums.MatterWFComponentStatusTypeEnum.InProgress || c.WFComponentStatusTypeId == (int)Enums.MatterWFComponentStatusTypeEnum.Starting)
                                                                                                        && (c.DisplayStatusTypeId == (int)Enums.DisplayStatusTypeEnum.Default || c.DisplayStatusTypeId == (int)Enums.DisplayStatusTypeEnum.Display))
                                                                                                        .Select(x=>x.MatterWFComponentId).FirstOrDefault(),
                    SettlementCompleteWFModuleId = m.Matter.MatterWFComponents.Where(c => ((m.Matter.MatterGroupTypeId == (int)MatterGroupTypeEnum.Discharge && c.WFComponentId == (int)WFComponentEnum.SettlementCompletedDischarge) || (m.Matter.MatterGroupTypeId == (int)MatterGroupTypeEnum.NewLoan && c.WFComponentId == (int)WFComponentEnum.SettlementCompleteNewLoans))
                                                                                                        && (c.WFComponentStatusTypeId == (int)Enums.MatterWFComponentStatusTypeEnum.InProgress || c.WFComponentStatusTypeId == (int)Enums.MatterWFComponentStatusTypeEnum.Starting)
                                                                                                        && (c.DisplayStatusTypeId == (int)Enums.DisplayStatusTypeEnum.Default || c.DisplayStatusTypeId == (int)Enums.DisplayStatusTypeEnum.Display))
                                                                                                        .Select(x => x.WFComponent.WFModuleId).FirstOrDefault(),
                    SecurityDetails = m.Matter.MatterSecurities.Where(i=>!i.Deleted).Select(x=>new { x.StreetAddress, x.Suburb, x.State.StateName, x.PostCode }),
                    TitleRefDetails = m.Matter.MatterSecurities.SelectMany(x => x.MatterSecurityTitleRefs.Select(t=>t.TitleReference))
                }).ToList()
                .Select(m =>
                    new MatterCustomEntities.MatterSettlementListView
                    (
                        m.SettlementScheduleId,
                        m.MatterId,
                        m.MatterGroupTypeId,
                        m.LenderId,
                        m.LenderName,
                        m.MatterDescription,
                        m.LenderRefNo,
                        m.FundingAmounts.ToList(),
                        m.SettlementDate,
                        m.SettlementTime,
                        String.Join(", ", m.MatterTypes.Distinct()),
                        m.StateId,
                        m.StateName,
                        m.SettlementType,
                        String.Join(", ", m.PexaWorkspaces.Distinct()),
                        String.Join(", ", m.PexaWorkspaceStatuses.Distinct()),
                        String.Join(", ", m.ActiveMilestones.Distinct()),
                        m.ReconciledDeposits - m.ReconciledPayments,
                        m.FileOwnerUserId,
                        m.FileOwnerUserName,
                        m.ReadyForAccounts,
                        m.InternalNotes,
                        m.HighlightColour,
                        m.MatterStatusTypeId != (int)MatterStatusTypeEnum.Settled && m.SettlementScheduleStatusTypeId == (int)SettlementScheduleStatusTypeEnum.NotSettled,
                        m.SettlementScheduleStatusTypeId,
                        m.SettlementScheduleStatusTypeName,
                        m.SettlementCompleteMatterWFComponentId > 0 ? m.SettlementCompleteMatterWFComponentId : (int?)null,
                        m.MatterStatusTypeId,
                        m.MatterStatusTypeName,
                        m.SettlementCompleteWFModuleId > 0 ? m.SettlementCompleteWFModuleId : (int?) null,
                        String.Join("\n", m.SecurityDetails.Distinct().Select(x => x.StreetAddress +", " + x.Suburb + ", " + x.StateName + " " + x.PostCode)),
                        String.Join(", ", m.SecurityDetails.Select(x=>x.StateName).Distinct()),
                        String.Join("\n", m.TitleRefDetails.Distinct())

                    )
                 ).ToList();
        }
        public IEnumerable<MatterCustomEntities.MatterSettlementListView> GetAnticipatedSettlements(int? userId, int? stateId, int? settlementVenueId, DateTime? settlementDate, bool showRegional, int matterGroupTypeId, bool includeSettled, bool includeCancelled, DateTime? startDateRange = null, DateTime? endDateRange = null)
        {
            List<int> validMatterStatuses = new List<int>() { (int)MatterStatusTypeEnum.InProgress, (int)MatterStatusTypeEnum.OnHold };
            List<int> validDisplayStatuses = new List<int>() { (int)DisplayStatusTypeEnum.Default, (int)DisplayStatusTypeEnum.Display };
            List<int> validComponentStatuses = new List<int>() { (int)MatterWFComponentStatusTypeEnum.InProgress, (int)MatterWFComponentStatusTypeEnum.Starting, (int)MatterWFComponentStatusTypeEnum.Complete, (int)MatterWFComponentStatusTypeEnum.OnHold };


            DateTime cutoffStart; 

            IQueryable<Matter> qry = context.Matters.AsNoTracking()
                .Where(s => validMatterStatuses.Contains(s.MatterStatusTypeId) &&
                !s.SettlementScheduleId.HasValue && s.MatterGroupTypeId == matterGroupTypeId
                && (userId == null || s.FileOwnerUserId == userId.Value)
                && (stateId == null || s.StateId == stateId.Value)
                 && (settlementDate == null ||
                    (s.MatterPexaDetails.Any() && s.MatterPexaDetails.FirstOrDefault().NominatedSettlementDate == settlementDate) ||
                    (!s.MatterPexaDetails.Any() && s.AnticipatedSettlementDate.HasValue &&  DbFunctions.TruncateTime(s.AnticipatedSettlementDate.Value) == settlementDate) ||
                    (!s.MatterPexaDetails.Any() && !s.AnticipatedSettlementDate.HasValue && s.MatterWFComponents.FirstOrDefault(w=>w.WFComponentId == (int)WFComponentEnum.FollowupOutstandingRequirements && validDisplayStatuses.Contains(w.DisplayStatusTypeId) && validComponentStatuses.Contains(w.WFComponentStatusTypeId) && w.MatterWFOutstandingReqs.Any()).MatterWFOutstandingReqs.FirstOrDefault().ExpectedSettlementDate == settlementDate)));
             
            //if (userId.HasValue) qry = qry.Where(q => q.Matter.FileOwnerUserId == userId.Value);
            //if (stateId.HasValue) qry = qry.Where(q => q.Matter.StateId == stateId.Value);
            //if (settlementDate.HasValue) qry = qry.Where(q => q.SettlementDate == settlementDate.Value);
            //if (!showRegional) qry = qry.Where(s => s.SettlementScheduleVenues.Any(x => x.SettlementRegionTypeId != (int)SettlementRegionTypeEnum.Regional));
            //if (settlementVenueId.HasValue) qry = qry.Where(s => s.SettlementScheduleVenues.Any(v => v.SettlementVenueId == settlementVenueId.Value));
            //if (!includeSettled) qry = qry.Where(s => s.SettlementScheduleStatusTypeId == (int)SettlementScheduleStatusTypeEnum.NotSettled);
            //if (!includeCancelled) qry = qry.Where(s => s.SettlementScheduleStatusTypeId != (int)SettlementScheduleStatusTypeEnum.Cancelled && s.SettlementScheduleStatusTypeId != (int)SettlementScheduleStatusTypeEnum.Rebooked && s.SettlementScheduleStatusTypeId != (int)SettlementScheduleStatusTypeEnum.Deleted && s.SettlementScheduleStatusTypeId != (int)SettlementScheduleStatusTypeEnum.Failed);


            if(startDateRange.HasValue && endDateRange.HasValue)
            {
                qry = qry.Where(s =>
                  (s.MatterPexaDetails.Any() && s.MatterPexaDetails.FirstOrDefault().NominatedSettlementDate >= startDateRange.Value && s.MatterPexaDetails.FirstOrDefault().NominatedSettlementDate <= endDateRange.Value) ||
                  (!s.MatterPexaDetails.Any() && s.AnticipatedSettlementDate.HasValue &&  DbFunctions.TruncateTime(s.AnticipatedSettlementDate.Value) >= startDateRange.Value && DbFunctions.TruncateTime(s.AnticipatedSettlementDate.Value) <= endDateRange.Value) ||
                  (!s.MatterPexaDetails.Any() && !s.AnticipatedSettlementDate.HasValue && 
                    s.MatterWFComponents.FirstOrDefault(w => w.WFComponentId == (int)WFComponentEnum.FollowupOutstandingRequirements && validDisplayStatuses.Contains(w.DisplayStatusTypeId) && validComponentStatuses.Contains(w.WFComponentStatusTypeId) && w.MatterWFOutstandingReqs.Any()).MatterWFOutstandingReqs.FirstOrDefault().ExpectedSettlementDate >= startDateRange.Value && s.MatterWFComponents.FirstOrDefault(w => w.WFComponentId == (int)WFComponentEnum.FollowupOutstandingRequirements && validDisplayStatuses.Contains(w.DisplayStatusTypeId) && validComponentStatuses.Contains(w.WFComponentStatusTypeId) && w.MatterWFOutstandingReqs.Any()).MatterWFOutstandingReqs.FirstOrDefault().ExpectedSettlementDate <= endDateRange.Value));


            }


            return qry.
                Select(m => new
                {
                    m.SettlementScheduleId,
                    m.MatterId,
                    m.MatterGroupTypeId,
                    PexaDate = m.MatterPexaDetails.Select(n => new { n.NominatedSettlementDate, n.UpdatedDate }).OrderByDescending(o => o.UpdatedDate).FirstOrDefault(),
                    ManualAnticipatedDate = m.AnticipatedSettlementDate.HasValue ? m.AnticipatedSettlementDate : m.MatterWFComponents.OrderByDescending(o => o.UpdatedDate).SelectMany(o => o.MatterWFOutstandingReqs.Where(d => d.ExpectedSettlementDate.HasValue).Select(s => s.ExpectedSettlementDate)).FirstOrDefault(),

                    
                    m.MatterStatusTypeId,
                    m.MatterStatusType.MatterStatusTypeName,
                    SettlementScheduleStatusTypeId = (int)SettlementScheduleStatusTypeEnum.NotSettled,
                    m.LenderId,
                    m.Lender.LenderName,
                    m.MatterDescription,
                    m.LenderRefNo,
                    m.SecondaryRefNo,
                    FundingAmounts = m.MatterLedgerItems.Where(x => x.MatterLedgerItemStatusTypeId != (int)MatterLedgerItemStatusTypeEnum.Cancelled && x.PayableToAccountTypeId == (int)AccountTypeEnum.Trust
                                                        && !x.ParentMatterLedgerItemId.HasValue).Select(x => new MatterCustomEntities.MatterFinFundingView() { Description = x.Description, Amount = x.Amount }),
                    MatterTypes = m.MatterMatterTypes.Select(a => a.MatterType.MatterTypeName),
                    m.StateId,
                    m.State.StateName,
                    PexaWorkspaces = m.MatterPexaWorkspaces.Where(p => p.MatterSecurityMatterPexaWorkspaces.Any(x => !x.MatterSecurity.Deleted)).Select(p => p.PexaWorkspace.PexaWorkspaceNo),
                    PexaWorkspaceStatuses = m.MatterPexaWorkspaces.Where(p => p.MatterSecurityMatterPexaWorkspaces.Any(x => !x.MatterSecurity.Deleted)).Select(p => p.PexaWorkspace.PexaWorkspaceStatusType.PexaWorkspaceStatusTypeName),
                    ActiveMilestones = m.MatterWFComponents.Where(c => (c.WFComponentStatusTypeId == (int)Enums.MatterWFComponentStatusTypeEnum.InProgress || c.WFComponentStatusTypeId == (int)Enums.MatterWFComponentStatusTypeEnum.Starting)
                                                                                                        && (c.DisplayStatusTypeId == (int)Enums.DisplayStatusTypeEnum.Default || c.DisplayStatusTypeId == (int)Enums.DisplayStatusTypeEnum.Display))
                                                                                                        .Select(c => c.WFComponent.WFComponentName),
                 
                    FileOwnerUserId = m.FileOwnerUserId,
                    FileOwnerUserName = m.User.Firstname + " " + m.User.Lastname,
                    SettlementType = m.MatterSecurities.Where(x => !x.Deleted).All(t => t.SettlementTypeId == (int)SettlementTypeEnum.Paper) ? "Paper" : m.MatterSecurities.Where(x => !x.Deleted).All(t => t.SettlementTypeId == (int)SettlementTypeEnum.PEXA) ? "PEXA" : "Paper / PEXA",
                    ReadyForAccounts = !m.MatterWFComponents.Any(c => (c.WFComponentStatusTypeId == (int)Enums.MatterWFComponentStatusTypeEnum.InProgress || c.WFComponentStatusTypeId == (int)Enums.MatterWFComponentStatusTypeEnum.Starting)
                                                                                                        && (c.DisplayStatusTypeId == (int)Enums.DisplayStatusTypeEnum.Default || c.DisplayStatusTypeId == (int)Enums.DisplayStatusTypeEnum.Display)
                                                                                                        && c.WFComponent.AccountsStageId == (int)AccountsStageEnum.NotReadyForAccounts),
                    SettlementScheduleStatusTypeName = "Anticipated",
                    
                    SecurityDetails = m.MatterSecurities.Where(i => !i.Deleted).Select(x => new { x.StreetAddress, x.Suburb, x.State.StateName, x.PostCode }),
                    TitleRefDetails = m.MatterSecurities.SelectMany(x => x.MatterSecurityTitleRefs.Select(t => t.TitleReference))
                }).ToList()
                .Select(m =>
                    new MatterCustomEntities.MatterSettlementListView
                    (
                        m.SettlementScheduleId,
                        m.MatterId,
                        m.MatterGroupTypeId,
                        m.LenderId,
                        m.LenderName,
                        m.MatterDescription,
                        m.LenderRefNo,
                        m.FundingAmounts.ToList(),
                        m.PexaDate != null ? m.PexaDate.NominatedSettlementDate.Date : m.ManualAnticipatedDate.Value.Date,
                        m.PexaDate != null ? m.PexaDate.NominatedSettlementDate.TimeOfDay : m.ManualAnticipatedDate.Value.TimeOfDay,
                        String.Join(", ", m.MatterTypes.Distinct()),
                        m.StateId,
                        m.StateName,
                        m.SettlementType,
                        String.Join(", ", m.PexaWorkspaces.Distinct()),
                        String.Join(", ", m.PexaWorkspaceStatuses.Distinct()),
                        String.Join(", ", m.ActiveMilestones.Distinct()),
                        0,
                        m.FileOwnerUserId,
                        m.FileOwnerUserName,
                        m.ReadyForAccounts,
                        null,
                        null,
                        m.MatterStatusTypeId != (int)MatterStatusTypeEnum.Settled && m.SettlementScheduleStatusTypeId == (int)SettlementScheduleStatusTypeEnum.NotSettled,
                        m.SettlementScheduleStatusTypeId,
                        m.SettlementScheduleStatusTypeName,
                        (int?)null,
                        m.MatterStatusTypeId,
                        m.MatterStatusTypeName,
                        (int?)null,
                        String.Join("\n", m.SecurityDetails.Distinct().Select(x => x.StreetAddress + ", " + x.Suburb + ", " + x.StateName + " " + x.PostCode)),
                        String.Join(", ", m.SecurityDetails.Select(x => x.StateName).Distinct()),
                        String.Join("\n", m.TitleRefDetails.Distinct()),
                        pCanChangeAnticipatedDate: ((DateTime?)m.PexaDate?.NominatedSettlementDate).HasValue
                        
                    )
                 ).ToList();
        }

        public IEnumerable<SettlementGridView> GetSettlementsGrid(DateTime? settlementDate, int? stateId, int? venueId, bool includeRegional, int settlementType)
        {
            //Not Perfect but appears to be working ok in Prod - Noticed in DEV sometimes causes Duplicates due to Settlement Venues being left joined upon multiple times. Left for now.
            // Code kept locally 1.42-v1-B4 Settlements Changes to update this - in case this is a bigger issue then thought.
            var scheds = (from s in context.SettlementSchedules.AsNoTracking()
                          join sv in context.SettlementScheduleVenues.AsNoTracking() on s.SettlementScheduleId equals sv.SettlementScheduleId
                          join svr in context.SettlementRegionTypes.AsNoTracking() on sv.SettlementRegionTypeId equals svr.SettlementRegionTypeId
                          where (svr.SettlementTypeId == settlementType && s.SettlementScheduleStatusType.IsActiveStatus)
                          select s);

            if (settlementDate.HasValue)
            {
                scheds = scheds.Where(x => x.SettlementDate == settlementDate.Value);
            }

            if (stateId.HasValue)
            {
                scheds = (from s in scheds
                          join sv in context.SettlementScheduleVenues.AsNoTracking() on s.SettlementScheduleId equals sv.SettlementScheduleId
                          where sv.StateId == stateId
                          select s);
            }

            var schedsForVenue = scheds;
            if (venueId.HasValue)
            {
                schedsForVenue = (from s in scheds
                                  join sv in context.SettlementScheduleVenues.AsNoTracking() on s.SettlementScheduleId equals sv.SettlementScheduleId
                                  join svr in context.SettlementRegionTypes.AsNoTracking() on sv.SettlementRegionTypeId equals svr.SettlementRegionTypeId
                                  where (svr.SettlementRegionTypeId != (int) Enums.SettlementRegionTypeEnum.Regional) && sv.SettlementVenueId == venueId
                                  select s
                                  );
            }

            if (!includeRegional)
            {
                scheds = (from s in scheds
                          join sv in context.SettlementScheduleVenues.AsNoTracking() on s.SettlementScheduleId equals sv.SettlementScheduleId
                          where sv.SettlementRegionTypeId != (int) Enums.SettlementRegionTypeEnum.Regional
                          select s
                          );

                schedsForVenue =
                    (from s in schedsForVenue
                     join sv in context.SettlementScheduleVenues.AsNoTracking() on s.SettlementScheduleId equals sv.SettlementScheduleId
                     where sv.SettlementRegionTypeId != (int) Enums.SettlementRegionTypeEnum.Regional
                     select s
                          );
            }

            return BuildSettlementViews(schedsForVenue, scheds);
        }




        public IEnumerable<SettlementCancellationReasonEntity> GetSettlementCancellationReasonEntities()
        {       
            return context.SettlementCancellationReasons.
                Select(r => new SettlementCancellationReasonEntity
                {
                    ReasonText = r.SettlementCancellationReasonText,
                    ReasonId = r.SettlementCancellationReasonId,
                    UpdatedByUserId = r.UpdatedByUserId,
                    UpdatedByUserName = r.User.Username,
                    UpdatedDate = r.UpdatedDate,
                    IsEnabled = r.Enabled
                }).ToList();
        }

        public IEnumerable<SettlementCancellationReasonList> GetSettlementCancellationReasonList()
        {
            return context.SettlementCancellationReasons.
                Select(r => new SettlementCancellationReasonList
                {
                    SettlementCancellationReasonId = r.SettlementCancellationReasonId,
                    SettlementCancellationReasonTxt = r.SettlementCancellationReasonText
                }).ToList();
        }


        public IEnumerable<SettlementVenueView> GetSettlementVenues()
        {
            return context.SettlementVenues.AsNoTracking()
                 .Select(s2 =>
                     new SettlementVenueView
                     {
                         VenueId = s2.SettlementVenueId,
                         VenueName = s2.VenueName,
                         StreetAddress = s2.StreetAddress,
                         Suburb = s2.Suburb,
                         PostCode = s2.PostCode,
                         StateId = s2.StateId,
                         State = s2.State.StateName,
                         IsMSAVenue = s2.IsMSAVenue,
                         Chargable = s2.Chargable,
                         Notes = s2.VenueNotes,
                         UpdatedByUserId = s2.UpdatedByUserId,
                         UpdatedByUsername = s2.User.Username,
                         UpdatedDate = s2.UpdatedDate
                     })
                 .ToList();
        }

        private IEnumerable<SettlementGridView> BuildSettlementViews(IQueryable<SettlementSchedule> filteredSettlements, IQueryable<SettlementSchedule> allSettlements)
        {
            var filteredScheds = BuildSettlementViews(filteredSettlements);
            var allScheds = BuildSettlementViews(allSettlements);
                      
            return GetSettlements(allScheds, filteredScheds);
        }

        private IEnumerable<SettlementGridView> GetSettlements(IQueryable<SettlementCalendarView> allScheds, IQueryable<SettlementCalendarView> filteredScheds)
        {
            return
                (from s in filteredScheds
                 group s by new { s.SettlementDate, s.SettlementTime, s.StateId } into sg
                 join svl in context.SettlementVenueLimits on new { sg.Key.SettlementDate, sg.Key.SettlementTime, sg.Key.StateId }
                        equals new { svl.SettlementDate, svl.SettlementTime, svl.State.StateId } into svg
                 from svl in svg.DefaultIfEmpty()
                 join st in context.States on sg.Key.StateId equals st.StateId

                 select new SettlementGridView
                 {
                     ParentId = Guid.NewGuid(),
                     SettlementDate = sg.Key.SettlementDate,
                     SettlementTime = sg.Key.SettlementTime,
                     StateId = sg.Key.StateId,
                     StateName = st.StateName,
                     VenueLimitForDateAndTime = (int?) svl.MaxLimit ?? st.SettlementVenueLimitDefault,
                     NumberOfUnassignedForDate = sg.Count(sc => !(sc.SettlementClerkId.HasValue || sc.SettlementAgentId.HasValue)),
                     NumberOfVenuesForDate = allScheds.Where(sc => sc.SettlementRegionTypeId != (int) Enums.SettlementRegionTypeEnum.Regional && sc.SettlementDate == sg.Key.SettlementDate && sc.SettlementTime == sg.Key.SettlementTime && sg.Key.StateId == sc.StateId).Select(x => x.SettlementVenueId).Distinct().Count(),
                     NumberOfSettlementsForDate = sg.Count(),
                     Schedules = filteredScheds.Where(sc => sc.SettlementDate == sg.Key.SettlementDate && sc.SettlementTime == sg.Key.SettlementTime && sg.Key.StateId == sc.StateId
                    )

                     .Select(scd => new SettlementScheduleView
                     {
                         MatterId = scd.MatterId,
                         MatterDesc = scd.MatterDesc,
                         MatterTypes = scd.MatterTypes,
                         AgentName = scd.SettlementAgent,
                         AgentNameWithState = scd.IsRegional ? scd.SettlementAgent + " " + scd.StateName : null,
                         ClerkId = scd.SettlementClerkId,
                         ClerkName = scd.SettlementClerk,
                         ClerkNameWithState = scd.IsRegional ? null : scd.SettlementClerk + " " + (string.IsNullOrEmpty(scd.SettlementClerk) ? null : scd.StateName).Trim(),                                                                
                         IsRegional = scd.IsRegional,
                         ScheduleId = scd.SettlementScheduleId,
                         VenueId = scd.SettlementVenueId,
                         VenueName = scd.IsRegional ? "REGIONAL" : scd.SettlementVenueName,
                         VenueStateId = scd.SettlementVenueStateId,
                         VenueState = scd.SettlementVenueState,
                         VenueSuburb = scd.Suburb,
                         AddressDetails = scd.IsRegional ? "Regional Location" : "",
                         FileOwner = scd.FileOwner,
                         LenderName = scd.Lender,
                         UpdatedBy = scd.UpdatedBy,
                         UpdatedDate = scd.UpdatedDate
                     })
                        .OrderBy(o => o.IsRegional).ThenBy(o2 => o2.MatterId)
                        .ToList()
                 }).ToList();
        }

        private IQueryable<SettlementCalendarView> BuildSettlementViews(IQueryable<SettlementSchedule> filteredSettlements)
        {
            return (from s in filteredSettlements
                    join sv in context.SettlementScheduleVenues on s.SettlementScheduleId equals sv.SettlementScheduleId
                    join m in context.Matters on s.MatterId equals m.MatterId
                    select new SettlementCalendarView
                    {
                        SettlementScheduleId = s.SettlementScheduleId,
                        MatterId = m.MatterId,
                        MatterDesc = m.MatterDescription,
                        MatterTypes = m.MatterMatterTypes.Select(mt => mt.MatterType.MatterTypeName),
                        IsRegional = sv.SettlementRegionTypeId == (int)Enums.SettlementRegionTypeEnum.Regional,
                        SettlementClerkId = sv.SettlementClerkId,
                        SettlementClerk = (sv.SettlementClerk.Firstname ?? "" + " " + sv.SettlementClerk.Lastname).Trim(),
                        SettlementAgentId = sv.SettlementAgentId,
                        SettlementAgent = sv.SettlementAgent.AgentName,
                        SettlementDate = s.SettlementDate,
                        SettlementTime = s.SettlementTime,
                        StateId = sv.StateId,
                        SettlementRegionTypeId = sv.SettlementRegionTypeId,
                        Suburb = sv.SettlementRegionTypeId == (int) Enums.SettlementRegionTypeEnum.Regional ? sv.SettlementAgent.Suburb : sv.SettlementVenue.Suburb,
                        FileOwner = m.User.Username,
                        Lender = m.Lender.LenderName,
                        UpdatedBy = s.User.Username,
                        SettlementVenueId = sv.SettlementVenueId,
                        SettlementVenueName = sv.SettlementVenue.VenueName,
                        SettlementVenueState = sv.SettlementVenue.State.StateName,
                        StateName = sv.State.StateName,
                        SettlementVenueStateId = sv.SettlementVenue.StateId,
                        UpdatedDate = sv.UpdatedDate
                    });
        }

        public SettlementClerksView GetSettlementClerksView(int id)
        {
            IQueryable<SettlementClerk> clerks = context.SettlementClerks.AsNoTracking().Where(m => m.SettlementClerkId == id);
            return GetSettlementClerksView(clerks).FirstOrDefault();
        }

        public IEnumerable<SettlementClerksView> GetSettlementClerksView()
        {
            IQueryable<SettlementClerk> clerks = context.SettlementClerks.AsNoTracking();
            return GetSettlementClerksView(clerks);
        }

        private IEnumerable<SettlementClerksView> GetSettlementClerksView(IQueryable<SettlementClerk> clerks)
        {
            return clerks
                .Select(s2 =>
                    new {
                        s2.SettlementClerkId,
                        s2.Lastname,
                        s2.Firstname,
                        s2.State.StateName,
                        s2.Email,
                        s2.PhoneWork,
                        s2.PhoneMobile,
                        s2.PhoneFax,
                        s2.Notes,
                        s2.UpdatedDate,
                        s2.UpdatedByUserId,
                        UpdatedByUsername = s2.User.Username
                    })
                .OrderBy(s => s.Lastname).ThenBy(o => o.Firstname)
                .ToList()
                .Select(s => new SettlementClerksView
                {
                    SettlementClerkId = s.SettlementClerkId,
                    LastName = s.Lastname,
                    FirstName = s.Firstname,
                    StateName = s.StateName,
                    Email = s.Email,
                    Phone = s.PhoneWork,
                    Mobile = s.PhoneMobile,
                    Fax = s.PhoneFax,
                    Notes = s.Notes,
                    UpdatedDate = s.UpdatedDate,
                    UpdatedByUsername = s.UpdatedByUsername
                })
                .ToList();
        }

        public SettlementAgentsView GetSettlementAgentsView(int id)
        {
            IQueryable<SettlementAgent> Agents = context.SettlementAgents.AsNoTracking().Where(m => m.SettlementAgentId == id);
            return GetSettlementAgentsView(Agents).FirstOrDefault();
        }

        public IEnumerable<SettlementAgentsView> GetSettlementAgentsView()
        {
            IQueryable<SettlementAgent> Agents = context.SettlementAgents.AsNoTracking();
            return GetSettlementAgentsView(Agents);
        }

        private IEnumerable<SettlementAgentsView> GetSettlementAgentsView(IQueryable<SettlementAgent> Agents)
        {
            return Agents
                .Select(s2 =>
                    new SettlementAgentsView
                    {
                        SettlementAgentId = s2.SettlementAgentId,
                        AgentName = s2.AgentName,
                        StreetAddress = s2.StreetAddress,
                        StateName = s2.State.StateName,
                        Suburb = s2.Suburb,
                        PostCode = s2.PostCode,
                        Email = s2.Email,
                        Phone = s2.Phone,
                        Mobile = s2.Mobile,
                        Fax = s2.Fax,
                        IsEnabled = s2.Enabled,
                        UpdatedDate = s2.UpdatedDate,
                        UpdatedByUserId = s2.UpdatedByUserId,
                        UpdatedByUsername = s2.User.Username
                    })
                .OrderBy(s => s.AgentName)
                .ToList();
        }

        public IEnumerable<SettlementAgentsView> GetSettlementAgentsForMatter(int matterId)
        {
            return GetSettlementAgentsViewForMatter(matterId)
                .DistinctBy(x => x.AgentName);
        }

        public IEnumerable<SettlementAgentsView> GetSettlementAgentsViewForMatter(int matterId)
        {
            return context.SettlementScheduleVenues.Where(x => x.SettlementRegionTypeId == (int) Enums.SettlementRegionTypeEnum.Regional && x.SettlementSchedule.MatterId == matterId && x.SettlementSchedule.SettlementScheduleStatusType.IsActiveStatus)
                .Select(s2 =>
                    new SettlementAgentsView
                    {
                        SettlementScheduleVenueId = s2.SettlementScheduleVenueId,                        
                        SettlementScheduleId = s2.SettlementScheduleId,
                        SettlementDate = s2.SettlementSchedule.SettlementDate,
                        SettlementTime = s2.SettlementSchedule.SettlementTime,
                        SettlementStateId = s2.StateId,
                        SettlementAgentId = s2.SettlementAgentId,
                        AgentName = s2.SettlementAgent.AgentName,
                        Suburb = s2.SettlementAgent.Suburb,
                        PostCode = s2.SettlementAgent.PostCode,
                        Contact = s2.RG_Contact,
                        Email = s2.RG_Email == null ? s2.SettlementAgent.Email : s2.RG_Email,
                        Phone = s2.RG_Phone == null ? s2.SettlementAgent.Phone : s2.RG_Phone,
                        Fax = s2.RG_Fax == null ? s2.SettlementAgent.Fax : s2.RG_Fax,
                        AgentFee = s2.RG_AgentFee
                    })
                .OrderBy(s => s.AgentName).ToList();

        }
    }
}
