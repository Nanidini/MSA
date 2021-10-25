using Slick_Domain.Common;
using Slick_Domain.Entities;
using Slick_Domain.Enums;
using Slick_Domain.Models;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Data.Entity;

namespace Slick_Domain.Services
{
    /// <summary>
    /// Consignments Repository. Responsible for getting data for consignment views.
    /// </summary>
    public class ConsignmentsRepository : SlickRepository
    {
        /// <summary>
        /// Standard base cosntructor of <see cref="SlickRepository"/> Class.
        /// </summary>
        /// <param name="context"></param>
        public ConsignmentsRepository(SlickContext context) : base(context)
        {
        }


        /// <summary>
        /// Gets an Enumerable <see cref="ConsignmentView"/> Collection for a specific <see cref="ConsignmentTypeEnum"/>.
        /// </summary>
        /// <param name="consignmentTypeId">An integer casted <see cref="ConsignmentTypeEnum"/>.</param>
        /// <returns>A an Enumerable <see cref="ConsignmentView"/> collection.</returns>
        public IEnumerable<ConsignmentView> GetConsignmentsView(int consignmentTypeId)
        {
            return GetConsignmentsView(context.Consignments.AsNoTracking().Where(x => x.ConsignmentTypeId == consignmentTypeId));
        }
        /// <summary>
        ///  Gets an Enumerable <see cref="ConsignmentMatterView"/> Collection.
        /// </summary>
        /// <param name="consignmentId">The Consignment ID.</param>
        /// <param name="isBoxConsignment">Boolean value for whether the consignment is a box consignment.</param>
        /// <param name="consignmentTypeId">An integer casted <see cref="ConsignmentTypeEnum"/> to filter by.</param>
        /// <returns>Returns an <see cref="ConsignmentMatterView"/>.</returns>
        public IEnumerable<ConsignmentMatterView> GetConsignmentMatters(int consignmentId, bool isBoxConsignment, int consignmentTypeId)
        {
            return GetConsignmentMattersView(context.ConsignmentMatters.Where(x => x.ConsignmentId == consignmentId), isBoxConsignment, consignmentTypeId);
        }
        /// <summary>
        /// Gets an Enumerable <see cref="ConsignmentAllocationView"/> Collection for a delivery, calls <see cref="GetConsignmentDeliveryReferenceAllocations(IQueryable{Consignment})"/>.
        /// </summary>
        /// <param name="deliveryReference">The Delivery Reference Number to find consignments for.</param>
        /// <returns>Returns an Enumerable <see cref="ConsignmentAllocationView"/>.</returns>
        public IEnumerable<ConsignmentAllocationView> GetConsignmentDeliveryReferenceAllocations(string deliveryReference)
        {
            return GetConsignmentDeliveryReferenceAllocations(context.Consignments.AsNoTracking().Where(x => x.DeliveryRefNo == deliveryReference)).ToList();
        }
        /// <summary>
        /// Gets a single <see cref="ConsignmentAllocationView"/> for the consignment.
        /// </summary>
        /// <param name="consignmentId">The Consignment ID to find</param>
        /// <returns>The <see cref="ConsignmentAllocationView"/> for the ID.</returns>
        /// <remarks>Preconditions:
        /// <list type="bullet">
        /// <item>Assumes the ConsignmentId is not null.</item>         
        /// </list>
        /// </remarks>
        public ConsignmentAllocationView GetConsignmentAllocationFromConsignment(int? consignmentId)
        {
            return GetConsignmentDeliveryReferenceAllocations(context.Consignments.AsNoTracking().Where(x => x.ConsignmentId == consignmentId)).FirstOrDefault();
        }
        /// <summary>
        /// Gets the <see cref="ConsignmentAllocationView"/> for the consignments.
        /// </summary>
        /// <param name="consigments">A IQueryable Collection of Consignments to Get Views for</param>
        /// <returns>Returns an Enumerable Collection of <see cref="ConsignmentAllocationView"/>.</returns>
        private IEnumerable<ConsignmentAllocationView> GetConsignmentDeliveryReferenceAllocations(IQueryable<Consignment> consigments)
        {
            return consigments
                 .Select(c => new ConsignmentAllocationView
                 {
                     ConsignmentId = c.ConsignmentId,
                     BarcodeBoxReference = c.BarCodeBoxRef,
                     Category = c.ConsignmentCategory.ConsignmentCategoryName,
                     DeliveryReference = c.DeliveryRefNo,
                     isDirty = false
                 });
        }
        /// <summary>
        /// Gets the last ten consignments for the user based on type
        /// </summary>
        /// <param name="userId">The User Id to find the last 10 viewed Consignments for.</param>
        /// <param name="consignmentTypeId">The <see cref="ConsignmentTypeEnum"/> to filter by, cast as an <see cref="int"/></param>
        /// <returns>Returns an Enumerable Collection of <see cref="ConsignmentView"/>.</returns>
        public IEnumerable<ConsignmentView> GetConsignmentsLastTenView(int userId, int consignmentTypeId)
        {
            return GetConsignmentsView(context.Consignments.AsNoTracking()
                .Where(x => x.ConsignmentTypeId == consignmentTypeId && x.CreatedByUserId == userId)
                .OrderByDescending(o => o.CreatedDate).Take(10));
        }

        /// <summary>
        /// Gets an Enumerable Collection of <see cref="ConsignmentView"/>'s for a <see cref="Matter"/> based on <see cref="ConsignmentTypeEnum"/>.
        /// </summary>
        /// <param name="searchMatterId">The Matter Id to filter by.</param>
        /// <param name="consignmentTypeId">The <see cref="ConsignmentTypeEnum"/> to filter by, casted as an integer.</param>
        /// <returns></returns>
        public IEnumerable<ConsignmentView> GetConsignmentsByMatter(int? searchMatterId, int consignmentTypeId)
        {
            return GetConsignmentsView(
                context.Consignments.AsNoTracking().Where(x => x.ConsignmentTypeId == consignmentTypeId &&
                            x.ConsignmentMatters.Any(m => m.MatterId == searchMatterId)));
        }

        /// <summary>
        /// Gets the First ConsignmentView for a ConsignmentId
        /// </summary>
        /// <param name="consignmentId">The Consignment Id to create the view for.</param>
        /// <returns>Returns the <see cref="ConsignmentView"/> for the Consignment ID provided.</returns>
        public ConsignmentView GetConsignmentView(int consignmentId)
        {
            return GetConsignmentsView(context.Consignments.AsNoTracking()
                .Where(x => x.ConsignmentId == consignmentId))
                .FirstOrDefault();
        }

        /// <summary>
        /// Gets ConsignmentView's for by ConsignmentId's and the <see cref="ConsignmentTypeEnum"/>.
        /// </summary>
        /// <param name="Id">The <see cref="Consignment"/> ID.</param>
        /// <param name="consignmentTypeId">The <see cref="ConsignmentTypeEnum"/> to filter by, casted as an integer.</param>
        /// <returns>An Enumerable Collection of <see cref="ConsignmentView"/> based on the paramaters.</returns>
        public IEnumerable<ConsignmentView> GetConsignmentsById(int Id, int consignmentTypeId)
        {
            return GetConsignmentsView(context.Consignments.AsNoTracking().Where(x => x.ConsignmentTypeId == consignmentTypeId && x.ConsignmentId == Id));
        }

        /// <summary>
        /// Gets the Consignment Views based on Title Reference ID, and <see cref="ConsignmentTypeEnum"/>
        /// </summary>
        /// <param name="searchTitleRef">The Title Reference ID to search by.</param>
        /// <param name="consignmentTypeId">The <see cref="ConsignmentTypeEnum"/>, cast as an integer.</param>
        /// <returns></returns>
        public IEnumerable<ConsignmentView> GetConsignmentsByTitleRef(string searchTitleRef, int consignmentTypeId)
        {
            return
            GetConsignmentsView
                (from c in context.Consignments.AsNoTracking()
                 join cm in context.ConsignmentMatters on c.ConsignmentId equals cm.ConsignmentId
                 join ms in context.MatterSecurities on cm.MatterId equals ms.MatterId
                 where !ms.Deleted && ms.MatterSecurityTitleRefs.Any(x => x.TitleReference.Contains(searchTitleRef))
                 select c
                 ).DistinctBy(x => x.ConsignmentId).ToList();
        }

        /// <summary>
        /// Gets Consignments based on Filters of Sent Status, Source and Destination Offices.
        /// </summary>
        /// <param name="consignmentTypeId">The <see cref="ConsignmentTypeEnum"/> to filter by, cast as an integer.</param>
        /// <param name="searchStatuses">The different search statuses to filter by.</param>
        /// <param name="searchSourceOffices">The different source offices to filter by.</param>
        /// <param name="searchDestOffices">The different Destination Offices to filter by.</param>
        /// <returns>Returns an Enumerable collection of <see cref="ConsignmentView"/>.</returns>
        public IEnumerable<ConsignmentView> GetConsignmentsView(int consignmentTypeId,
                                                                IEnumerable<EntityCompacted> searchStatuses,
                                                                IEnumerable<EntityCompacted> searchSourceOffices,
                                                                IEnumerable<EntityCompacted> searchDestOffices)
        {
            var consignments = context.Consignments.Where(x => x.ConsignmentTypeId == consignmentTypeId);

            if (searchStatuses != null && !searchStatuses.Any(x => x.Id == DomainConstants.AnySelectionId && x.IsChecked))
            {
                var statusList = searchStatuses.Where(x => x.IsChecked).Select(x => x.Id).ToList();
                consignments = consignments.Where(x => statusList.Contains((x.ConsignmentStatusTypeId)));
            }

            if (searchSourceOffices != null && !searchSourceOffices.Any(x => x.Id == DomainConstants.AnySelectionId && x.IsChecked))
            {
                var officeList = searchSourceOffices.Where(x => x.IsChecked).Select(x => x.Id).ToList();
                consignments = consignments.Where(x => officeList.Contains((x.SourceOfficeId)));
            }

            if (searchDestOffices != null && !searchDestOffices.Any(x => x.Id == DomainConstants.AnySelectionId && x.IsChecked))
            {
                var officeList = searchDestOffices.Where(x => x.IsChecked).Select(x => x.Id).ToList();
                consignments = consignments.Where(x => x.DestOfficeId == null || officeList.Contains((x.DestOfficeId.Value)));
            }

            return GetConsignmentsView(consignments);
        }

        /// <summary>
        /// Gets Consignment Matter Views based on Parameters.
        /// </summary>
        /// <param name="consignments">The Consignments to create the views from.</param>
        /// <param name="isBoxConsignment">Boolean Flag for whether these are box consignment items.</param>
        /// <param name="consignmentTypeId">The <see cref="ConsignmentTypeEnum"/> to filter by, cast as an Integer.</param>
        /// <returns>Returns an enumerable collection of <see cref="ConsignmentMatterView"/> based on the paramters provided.</returns>
        private IEnumerable<ConsignmentMatterView> GetConsignmentMattersView(IQueryable<ConsignmentMatter> consignments, bool isBoxConsignment, int consignmentTypeId)
        {
            var mRep = new MatterRepository(context);
            var items = consignments
                 .Select(s =>
                     new ConsignmentMatterView
                     {
                         ConsignmentMatterId = s.ConsignmentMatterId,
                         ConsignmentId = s.ConsignmentId,
                         MatterId = s.MatterId,
                         CompleteFile = s.CompleteFile,
                         BSB = s.BSB,
                         AccountNo = s.AccountNo,
                         FeeAmount = s.FeeAmount,
                     

                         UpdatedBy = s.User.Username,
                         UpdatedByUserId = s.UpdatedByUserId,
                         UpdatedDate = s.UpdatedDate,
                         isDirty = false,
                        AssociatedMatter = new ConsignmentMatterMatterView
                        {
                            MatterId = s.MatterId,
                            ConsignmentMatterId = s.ConsignmentMatterId,
                            MatterDescription = s.Matter.MatterDescription,
                            MatterStatusTypeId = s.Matter.MatterStatusTypeId,
                            MatterStatus = s.Matter.MatterStatusType.MatterStatusTypeName,
                            Lender = s.Matter.Lender.LenderNameShort,
                            FileOwner = s.Matter.User.Username,
                            LenderRef = s.Matter.LenderRefNo,
                            Settled = s.Matter.Settled,
                            SettlementDate = s.Matter.SettlementSchedule.SettlementDate,
                            Reconciled = false,
                            SecPacketRequired = s.Matter.MatterGroupTypeId == (int)MatterGroupTypeEnum.NewLoan ? s.Matter.Lender.NewLoanSecPacketRequired : s.Matter.Lender.DischargeSecPacketRequired,
                            ArchiveRequired = s.Matter.MatterGroupTypeId == (int)MatterGroupTypeEnum.NewLoan ? s.Matter.Lender.NewLoanArchiveRequired : s.Matter.Lender.DischargeArchiveRequired,
                            CloseMatterStarted = (consignmentTypeId == (int)ConsignmentTypeEnum.SecurityPacket || consignmentTypeId == (int)ConsignmentTypeEnum.Archive) ?  
                                                    s.Matter.MatterWFComponents.Any(x => x.WFComponentId == (int)WFComponentEnum.CloseMatter
                                                    && (x.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.Starting || x.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.InProgress || x.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.Complete)
                                                    && (x.DisplayStatusTypeId == (int)DisplayStatusTypeEnum.Default || x.DisplayStatusTypeId == (int)DisplayStatusTypeEnum.Display))
                                                    : false,
                            CloseMatterCompleted = (consignmentTypeId == (int)ConsignmentTypeEnum.SecurityPacket || consignmentTypeId == (int)ConsignmentTypeEnum.Archive) ?  
                                                    s.Matter.MatterWFComponents.Any(x => x.WFComponentId == (int)WFComponentEnum.CloseMatter
                                                    && (x.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.Complete)
                                                    && (x.DisplayStatusTypeId == (int)DisplayStatusTypeEnum.Default || x.DisplayStatusTypeId == (int)DisplayStatusTypeEnum.Display))
                                                    :false,
                            OnSecPacketList = (consignmentTypeId == (int)ConsignmentTypeEnum.SecurityPacket || consignmentTypeId == (int)ConsignmentTypeEnum.Archive) ? s.Matter.ConsignmentMatters.Any(c => (consignmentTypeId == (int)ConsignmentTypeEnum.SecurityPacket || c.Consignment.ConsignmentTypeId == (int)ConsignmentTypeEnum.SecurityPacket)) : false,
                            OnArchiveList = (consignmentTypeId == (int)ConsignmentTypeEnum.SecurityPacket || consignmentTypeId == (int)ConsignmentTypeEnum.Archive) ? s.Matter.ConsignmentMatters.Any(c =>  (consignmentTypeId == (int)ConsignmentTypeEnum.Archive || c.Consignment.ConsignmentTypeId == (int)ConsignmentTypeEnum.Archive)) : false,
                            StateId = s.Matter.StateId,
                            LenderId = s.Matter.LenderId,
                            MatterGroupTypeId = s.Matter.MatterGroupTypeId,
                             
                             
                        }
                     }).ToList();
 
            // Left as Distinct for nonbanking - presume that is correct.
            List<ConsignmentMatterView> itemsList = null;
            if (consignmentTypeId == (int) ConsignmentTypeEnum.Banking)
            {
                itemsList = items.OrderBy(o => o.ConsignmentId).ThenBy(o => o.MatterId).ToList();
            }
            else
            {
                itemsList = items.OrderBy(o => o.ConsignmentId).ThenBy(o => o.MatterId).DistinctBy(x => x.MatterId).ToList();
            }
            if (consignmentTypeId == (int)ConsignmentCategoryEnum.Archive)
            {
                itemsList.ForEach(s => s.FeeSummaries = context.MatterLedgerItems.Where(x => x.MatterId == s.MatterId &&
                                   x.MatterLedgerItemStatusTypeId != (int)MatterLedgerItemStatusTypeEnum.Cancelled &&
                                   !x.ParentMatterLedgerItemId.HasValue &&
                                   ((x.Matter.MatterGroupTypeId == (int)MatterGroupTypeEnum.NewLoan &&
                                       x.TransactionTypeId == (int)TransactionTypeEnum.Invoice) ||
                                    (x.Matter.MatterGroupTypeId == (int)MatterGroupTypeEnum.Discharge)))
                                                            .Select(x => new FeeSummaryView()
                                                            {
                                                                Amount = x.Amount,
                                                                Description = x.FundsTransferredTypeId != (int)FundsTransferredTypeEnum.UnPaid ? "PAID - " + x.Description : x.Description,
                                                                PayableBy = x.PayableByName,
                                                                PayableTo = x.PayableToName,
                                                                Paid = x.FundsTransferredTypeId != (int)FundsTransferredTypeEnum.UnPaid
                                                            }).ToList());
            }
            if (consignmentTypeId == (int)ConsignmentCategoryEnum.Archive || consignmentTypeId == (int)ConsignmentCategoryEnum.SecurityPacket)
            {
                foreach (var mt in itemsList)
                {
                    mt.AssociatedMatter.CanClose = (consignmentTypeId == (int)ConsignmentTypeEnum.SecurityPacket || consignmentTypeId == (int)ConsignmentTypeEnum.Archive) ?
                               CanMatterBeClosed(mt) : new Tuple<bool, string>(false, null);
                }
            }

            if (!isBoxConsignment)
            {
                foreach (var item in itemsList)
                {
                    item.ContentsView = new System.Collections.ObjectModel.ObservableCollection<ConsignmentContentsView>(
                        consignments.Where(x => x.ConsignmentId == item.ConsignmentId && x.MatterId == item.MatterId)
                        .Select(s => new ConsignmentContentsView
                        {
                            ConsignmentMatterId = s.ConsignmentMatterId,
                            ContentTypeText = s.ConsignmentContentType.ConsignmentContentTypeName ?? s.Contents,
                            Contents = s.Contents,
                            MatterId = s.MatterId,
                            UpdatedBy = s.User.Username,
                            UpdatedByUserId = s.UpdatedByUserId,
                            UpdatedDate = s.UpdatedDate,
                            isDirty = false
                        }));
                }
            }
            else
            {
                itemsList.ForEach(x => x.ContentsView = new System.Collections.ObjectModel.ObservableCollection<ConsignmentContentsView>());
            }

            if (consignmentTypeId == (int)ConsignmentTypeEnum.SecurityPacket)
            {
                GetSecurityPacketDetails(itemsList);
            }

            return itemsList;

        }



        public Tuple<bool, string> CanMatterBeClosed(ConsignmentMatterView matter)
        {
            bool canClose = true;
            string reason = "";

            if (matter.AssociatedMatter.StateId == (int)StateIdEnum.TAS)
            {
                return new Tuple<bool, string>(true, null);
            }


            if (matter.AssociatedMatter.SecPacketRequired && !matter.AssociatedMatter.OnSecPacketList)
            {
                canClose = false;
                reason = "- Lender requires matters to be on Security Packet SLICK List to close \n";
            }

            if (matter.AssociatedMatter.ArchiveRequired && !matter.AssociatedMatter.OnArchiveList)
            {
                canClose = false;
                reason = "- Lender requires matters to be on Archive SLICK List to close";
            }

            return new Tuple<bool, string>(canClose, reason?.Trim());
        }
        /// <summary>
        /// Matches Security Packet details - BNY Ref, Title Ref, RamsLapno to Security Packet Received and details from matter.
        /// </summary>
        /// <param name="itemsList">The Consignment Matter View's to modify.</param>
        /// <remarks>Refactor - Unsure if void functions can modify a list item. Does this do anything?</remarks>
        private void GetSecurityPacketDetails(List<ConsignmentMatterView> itemsList)
        {
            foreach(var item in itemsList)
            {
                var currItem = item;
                GetSecurityPacketDetails(currItem.AssociatedMatter);
            }
        }

        public void GetSecurityPacketDetails(ConsignmentMatterMatterView item)
        {
            if (item == null) return;
            var matterId = item.MatterId;

            var securityPacketItems = context.ConsignmentSecurityPacketTitles.AsNoTracking().FirstOrDefault(x => x.MatterId == matterId);
            var ramsLapNo = context.Matters.AsNoTracking().Where(x => x.MatterId == matterId).Select(x => x.MatterDischarge).FirstOrDefault()?.RAMSLapsNo;
            var titleRefs = context.MatterSecurities.Where(x => x.MatterId == matterId).SelectMany(x => x.MatterSecurityTitleRefs).Select(x => x.TitleReference).ToList();

            item.RAMSLapNo = string.IsNullOrEmpty(ramsLapNo) ? securityPacketItems?.RamsLapNo : ramsLapNo;
            item.BNYReference = securityPacketItems?.BNYReference;

            if (titleRefs != null && titleRefs.Any())
            {
                if (titleRefs.Count == 1)
                {
                    item.TitleReference = titleRefs.First();
                }
                else
                {
                    item.TitleReference = securityPacketItems?.TitleReference;
                }
            }
        }
        /// <summary>
        /// Gets the <see cref="ConsignmentMatterMatterView"/> for a Consignment for a specific <see cref="Matter.MatterId"/>.
        /// </summary>
        /// <param name="_consignmentId">The Consignment Id to filter by.</param>
        /// <param name="matterId">The Matter Id to present the view for.</param>
        /// <returns>Returns the constructed <see cref="ConsignmentMatterView"/> for the parameters supplied.</returns>
        public ConsignmentMatterMatterView GetMatterDetailsForConsignment(int _consignmentId, int matterId, int consignmentTypeId)
        {
            var mt =
                 context.Matters.Where(x => x.MatterId == matterId).Select(s =>
                  new ConsignmentMatterMatterView
                  {
                      MatterId = matterId,
                      MatterDescription = s.MatterDescription,
                      MatterStatusTypeId = s.MatterStatusTypeId,
                      MatterStatus = s.MatterStatusType.MatterStatusTypeName,
                      CloseMatterStarted = s.MatterWFComponents.Any(x => x.WFComponentId == (int)WFComponentEnum.CloseMatter
                                    && (x.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.Starting || x.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.InProgress || x.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.Complete)
                                    && (x.DisplayStatusTypeId == (int)DisplayStatusTypeEnum.Default || x.DisplayStatusTypeId == (int)DisplayStatusTypeEnum.Display)),
                      CloseMatterCompleted = s.MatterWFComponents.Any(x => x.WFComponentId == (int)WFComponentEnum.CloseMatter
                                             && (x.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.Complete)
                                             && (x.DisplayStatusTypeId == (int)DisplayStatusTypeEnum.Default || x.DisplayStatusTypeId == (int)DisplayStatusTypeEnum.Display)),
                      Lender = s.Lender.LenderNameShort,
                      FileOwner = s.User.Username,
                      LenderRef = s.LenderRefNo,
                      Settled = s.Settled,
                      SettlementDate = s.SettlementSchedule.SettlementDate,
                      Reconciled = false,
                      LenderId = s.LenderId,
                      StateId = s.StateId,
                      MatterGroupTypeId = s.MatterGroupTypeId
                  }).FirstOrDefault();


            if(mt!=null && (consignmentTypeId == (int)ConsignmentTypeEnum.Archive || consignmentTypeId == (int)ConsignmentTypeEnum.SecurityPacket))
            {
                mt.CanClose = (consignmentTypeId == (int)ConsignmentTypeEnum.SecurityPacket || consignmentTypeId == (int)ConsignmentTypeEnum.Archive) ?
                          new MatterRepository(context).CanMatterBeClosed(mt.MatterId, mt.LenderId, mt.StateId, mt.MatterGroupTypeId, consignmentTypeId) :
                          new Tuple<bool, string>(false, null);
            }

            return mt;
        }
        /// <summary>
        /// Gets the <see cref="ConsignmentView"/> for every <see cref="Consignment"/> provided.
        /// </summary>
        /// <remarks>
        /// <para>To get the destination of the <see cref="Consignment"/> Item, the function calls <see cref="GetDestination(int, string, string, string, string)"/>.</para>
        /// <para>It also changes the date received and received by based on whether the Item has been received by MSA yet.</para>
        /// </remarks>
        /// <param name="consignments">An Enumerable Collections of <see cref="Consignment"/> objects to construct Views for.</param>
        /// <returns>The constructed <see cref="ConsignmentView"/> objects in an Enumerable Collection.</returns>
        private IEnumerable<ConsignmentView> GetConsignmentsView(IQueryable<Consignment> consignments)
        {
            var items = consignments
                .Select(s =>
                    new ConsignmentView
                    {
                        Id = Guid.NewGuid(),
                        ConsignmentId = s.ConsignmentId,
                        ConsignmentTypeId = s.ConsignmentTypeId,
                        BarcodeBoxReference = s.BarCodeBoxRef,
                        ConsignmentType = s.ConsignmentType.ConsignmentTypeDesc,
                        DateReceived = s.DateReceived,
                        DateCollected = s.DateCollected,
                        DateSent = s.DateSent,
                        DeliveryCompany = s.ConsignmentDeliveryCompany.DeliveryCompanyName,
                        DeliveryCompanyId = s.ConsignmentDeliveryCompanyId,
                        DeliveryReference = s.DeliveryRefNo,
                        DestinationAgent = s.SettlementAgent.AgentName,
                        DestinationAgentId = s.DestSettlementAgentId,
                        DestinationOffice = s.Office1.OfficeName,
                        DestinationOfficeId = s.DestOfficeId,
                        TrusteeCompanyId = s.DestTrusteeCompanyId,
                        DestinationTrustee = s.ConsignmentTrustee.ConsignmentTrusteeName,
                        SourceOffice = s.Office.OfficeName,
                        SourceOfficeId = s.SourceOfficeId,
                        CategoryType = s.ConsignmentCategory.ConsignmentCategoryDesc,
                        CategoryTypeId = s.ConsignmentCategoryId,
                        OtherDestinationAddress = s.DestOther_StreetAddress,
                        OtherDestinationName = s.DestOther_Name,
                        OtherDestinationPostCode = s.DestOther_PostCode,
                        OtherDestinationState = s.State.StateName,
                        OtherDestinationStateId = s.DestOther_StateId,
                        OtherDestinationSuburb = s.DestOther_Suburb,
                        AttentionForUserId = s.AttentionUserId,
                        AttentionFor = s.User.Username,
                        CreatedBy = s.User1.Username,
                        CreatedByUserId = s.CreatedByUserId,
                        CreatedDate = s.CreatedDate,
                        ReceivedBy = s.User2.Username,
                        ReceivedByUserId = s.ReceivedByUserId,
                        UpdatedBy = s.User3.Username,
                        UpdatedByUserId = s.UpdatedByUserId,
                        StatusType = s.ConsignmentStatusType.ConsignmentStatusTypeDesc,
                        StatusTypeId = s.ConsignmentStatusTypeId,
                        UpdatedDate = s.UpdatedDate,
                        AllMattersReconciled = false
                    })
                .OrderByDescending(o => o.DateSent).ThenByDescending(o2 => o2.DateReceived)
                .ToList();

            items.ForEach(x =>
            {
                x.Destination = GetDestination(x.CategoryTypeId, x.DestinationOffice, x.DestinationAgent, x.OtherDestinationName, x.DestinationTrustee);
                x.DateReceivedString = x.CategoryTypeId != (int) ConsignmentCategoryEnum.MSAOffice ? "N/A" : x.DateReceived.HasValue ? x.DateReceived.Value.ToShortDateString() : null;
                x.ReceivedBy = x.CategoryTypeId != (int) ConsignmentCategoryEnum.MSAOffice ? "N/A" : x.ReceivedBy;
            });

            return items;
        }
        /// <summary>
        /// Gets a List of <see cref="ConsignmentSecurityPacketListView"/> objects for the Title Reference supplied.
        /// </summary>
        /// <remarks><para>If there are no results initially for the title reference, the function tries to add "Vol " to it if it is not contained in the string.</para></remarks>
        /// <param name="titleReference">The Title Reference to filter by.</param>
        /// <returns>Returns A List of <see cref="ConsignmentSecurityPacketListView"/> objects.</returns>
        public List<ConsignmentSecurityPacketListView> GetMatterDetailsForTitleReference(string titleReference)
        {
            var titles = GetMatterDetailsForTitle(titleReference);
            if (titles.Count == 0)
            {
                if (!titleReference.Contains("Vol"))
                {
                    titleReference = titleReference.Replace("/", " ").Replace(":", " ").Replace("-", " ");
                    var index = titleReference.IndexOf(" ");
                    titleReference = "Vol: " + titleReference.Substring(0, index > 0 ? titleReference.Substring(0, index).Length : titleReference.Length);
                    titles = GetMatterDetailsForTitle(titleReference);
                }
            }
            return titles;
        }

        /// <summary>
        /// Gets a <see cref="List{ConsignmentSecurityPacketListView}"/> of matters that match the title reference provided. 
        /// </summary>
        /// <remarks>
        /// Uses <see cref="SearchAndBuildConsignmentsForTitle(List{string})"/> with <paramref name="titleReference"/>
        /// </remarks>
        /// <param name="titleReference"></param>
        /// <returns>Returns a <see cref="List{ConsignmentSecurityPacketListView}"/> filtered by the Title Reference</returns>
        private List<ConsignmentSecurityPacketListView> GetMatterDetailsForTitle(string titleReference)
        {
            var consignments = new List<ConsignmentSecurityPacketListView>();

            string searchTitle = titleReference;
            if (titleReference.Contains("/") || titleReference.Contains("-") || titleReference.Contains(":"))
            {
                var titlesToMatch = GetTitleReferenceMatches(new List<string> { titleReference });
                consignments = SearchAndBuildConsignmentsForTitle(titlesToMatch);
            }
            else
            {
                consignments = SearchAndBuildConsignmentsForTitle(searchTitle);
            }

            return consignments;
        }

        /// <summary>
        /// Get all <see cref="ConsignmentSecurityPacketListView"/> items that match the Title References.
        /// </summary>
        /// <param name="titlesToMatch"><see cref="List{string}"/> of Title References to be matched against.</param>
        /// <returns>A List of matched <see cref="ConsignmentSecurityPacketListView"/> items against the <paramref name="titlesToMatch"/></returns>
        private List<ConsignmentSecurityPacketListView> SearchAndBuildConsignmentsForTitle(List<string> titlesToMatch)
        {
            var matterDetails = (from t in context.MatterSecurityTitleRefs
                                 join s in context.MatterSecurities on t.MatterSecurityId equals s.MatterSecurityId
                                 join m in context.Matters on s.MatterId equals m.MatterId
                                 join tm in titlesToMatch on 1 equals 1
                                 where !s.Deleted && m.MatterStatusTypeId != (int) MatterStatusTypeEnum.Closed && t.TitleReference.Contains(tm)
                                 orderby m.MatterStatusTypeId
                                 select new { m.MatterId, m.MatterDescription, m.MatterStatusTypeId, m.MatterStatusType.MatterStatusTypeName, t.TitleReference, m.MatterDischarge.RAMSLapsNo, m.MatterLoanAccounts });

            var consignments = new List<ConsignmentSecurityPacketListView>();
            foreach (var detail in matterDetails)
            {
                consignments.Add(new ConsignmentSecurityPacketListView
                {
                    Borrowers = detail.MatterDescription,
                    CreatedByUserId = GlobalVars.CurrentUser.UserId,
                    CreatedByUserName = GlobalVars.CurrentUser.Username,
                    MatterId = detail.MatterId.ToString(),
                    MatterStatusTypeId = detail.MatterStatusTypeId,
                    MatterStatusType = detail.MatterStatusTypeName,
                    TitleReference = detail.TitleReference,
                    RamsLapsNo = detail.RAMSLapsNo,
                    LoanAccounts = EntityHelper.BuildLoanAccountsString(detail.MatterLoanAccounts)
                });
            }
            return consignments;
        }
        /// <summary>
        /// Search and Build Consignments for Title Reference Provided. 
        /// </summary>
        /// <param name="titleReference">The Title Reference to be searched for </param>
        /// <returns>Returns a <see cref="List{ConsignmentSecurityPacketListView}"/> of Consignments that match the title reference/s.</returns>
        private List<ConsignmentSecurityPacketListView> SearchAndBuildConsignmentsForTitle(string titleReference)
        {

            var matterDetails = (from t in context.MatterSecurityTitleRefs
                                 join s in context.MatterSecurities on t.MatterSecurityId equals s.MatterSecurityId
                                 join m in context.Matters on s.MatterId equals m.MatterId
                                 where !s.Deleted && m.MatterStatusTypeId != (int) MatterStatusTypeEnum.Closed && t.TitleReference == titleReference
                                 orderby m.MatterStatusTypeId
                                 select m)
                                 .Include("MatterLoanAccounts").Include("MatterDischarge").Include("MatterStatusType");

            var consignments = new List<ConsignmentSecurityPacketListView>();
            foreach (var detail in matterDetails)
            {
                consignments.Add(new ConsignmentSecurityPacketListView
                {
                    Borrowers = detail.MatterDescription,
                    CreatedByUserId = GlobalVars.CurrentUser.UserId,
                    CreatedByUserName = GlobalVars.CurrentUser.Username,
                    MatterId = detail.MatterId.ToString(),
                    MatterStatusTypeId = detail.MatterStatusTypeId,
                    MatterStatusType = detail.MatterStatusType.MatterStatusTypeName,
                    TitleReference = titleReference,
                    RamsLapsNo = detail.MatterDischarge.RAMSLapsNo,
                    LoanAccounts = EntityHelper.BuildLoanAccountsString(detail.MatterLoanAccounts)
                });
            }
            return consignments;
        }
        /// <summary>
        /// Updates umatched titles with 
        /// </summary>
        /// <remarks>
        /// Updates the title to match it if there are consignment security packet itles.</remarks>
        /// <param name="unmatchedTitles">The umatched Title Consignment pairs</param>
        /// <param name="matterId">A <see cref="Matter.MatterId"/> to filter by</param>
        public void UpdateUnmatchedSecurityPacketTitles(IEnumerable<ConsignmentTitleReferenceMatch> unmatchedTitles, int matterId)
        {
            var matter = context.Matters.Where(x => x.MatterId == matterId).FirstOrDefault();
            var UserId = GlobalVars.CurrentUser.UserId;
            foreach (var unmatchedTitle in unmatchedTitles.Where(x=> x.TitleSelected == true))
            {
                var title = context.ConsignmentSecurityPacketTitles.FirstOrDefault(x => x.ConsignmentSecurityPacketTitleId == unmatchedTitle.ID);
                if (title != null)
                {
                    title.MatterId = matterId;
                    title.RamsLapNo = matter.MatterDischarge?.RAMSLapsNo;
                    title.Borrowers = matter.MatterDescription;
                    title.LoanAccounts = EntityHelper.BuildLoanAccountsString(matter.MatterLoanAccounts);
                    title.UpdatedByUserId = GlobalVars.CurrentUser.UserId;
                    title.UpdatedDate = DateTime.Now;

                 //   if (!unmatchedTitle.ExactMatch) - title ref - sometimes missing not sure why so always save.
                 //   {
                        title.TitleReference = unmatchedTitle.MatchedToTitleReference;
                //    }
                }
                context.SaveChanges();
            }
        }

        /// <summary>
        /// Gets unmatched TitleReferences from Consignments - matches against colons, hyphens and slashes - CT 111:111 is same as CT 111/111
        /// </summary>
        /// <param name="matterId"></param>
        /// <param name="securityDetails"></param>
        /// <param name="update"></param>
        /// <returns></returns>
        public List<ConsignmentTitleReferenceMatch> GetSecurityPacketTitlesUnMatched(int matterId, List<SecurityDetail> securityDetails)
        {
            var titleRefs = securityDetails.SelectMany(x => x.TitleReferences.Select(y => y.Reference)).Where(x=> x != null).ToList();
            var matchedTitles = new List<ConsignmentTitleReferenceMatch>();
            var titlesToMatch = GetTitleReferenceMatches(titleRefs);

            var titlesWithRemovedSymbols = new List<ConsignmentTitleReferenceMatch>();
            foreach (var title in titleRefs)
            {
                var titleReplace = title.Replace("/", " ").Replace(":", " ").Replace("-", " ");
                titlesWithRemovedSymbols.Add(new ConsignmentTitleReferenceMatch
                {
                    MatchedToTitleReference = title, TitleReference = titleReplace
                });
            }

            var consignmentTitles =
               context.ConsignmentSecurityPacketTitles
               .Where(x => x.MatterId == null && titlesToMatch.Any(s => s.Contains(x.TitleReference)))
               .Select(t => new { t.ConsignmentSecurityPacketTitleId, t.TitleReference });


            foreach (var title in consignmentTitles)
            {
                var titleMatch = title.TitleReference.Replace("/", " ").Replace(":", " ").Replace("-"," ");

                matchedTitles.Add(new ConsignmentTitleReferenceMatch
                {
                    ID = title.ConsignmentSecurityPacketTitleId,
                    ExactMatch = titleRefs.Contains(title.TitleReference),
                    TitleReference = title.TitleReference,
                    MatchedToTitleReference = titlesWithRemovedSymbols.FirstOrDefault(x => x.TitleReference.Contains(titleMatch))?.MatchedToTitleReference
                });
            }

            return matchedTitles;


        }
        /// <summary>
        /// Get Title Reference Matches if members contan certain elements, gets replaced and then added to <paramref name="titlesToMatch"/>.
        /// </summary>
        /// <param name="titleRefs">A <see cref="List{string}"/> of Title References.</param>
        /// <returns>Returns a <see cref="List{string}"/> of formatted Title References.</returns>
        private List<string> GetTitleReferenceMatches(List<string> titleRefs)
        {
            var titlesToMatch = new List<string>();
            foreach (var title in titleRefs)
            {
                titlesToMatch.Add(title);
                var replaceTitle = title;
                if (title.Contains("/"))
                {
                    replaceTitle = replaceTitle.Replace("/", ":");
                    titlesToMatch.Add(replaceTitle);
                    replaceTitle = replaceTitle.Replace(":", "-");
                    titlesToMatch.Add(replaceTitle);
                    replaceTitle = replaceTitle.Replace("-", " Folio ");
                    titlesToMatch.Add(replaceTitle);
                }
                else if (title.Contains(":"))
                {
                    replaceTitle = replaceTitle.Replace(":", "/");
                    titlesToMatch.Add(replaceTitle);
                    replaceTitle = replaceTitle.Replace("/", "-");
                    titlesToMatch.Add(replaceTitle);
                    replaceTitle = replaceTitle.Replace("-", " Folio ");
                    titlesToMatch.Add(replaceTitle);
                }
                else if (title.Contains("-"))
                {
                    replaceTitle = replaceTitle.Replace("-", "/");
                    titlesToMatch.Add(replaceTitle);
                    replaceTitle = replaceTitle.Replace("/", ":");
                    titlesToMatch.Add(replaceTitle);
                    replaceTitle = replaceTitle.Replace(":", " Folio ");
                    titlesToMatch.Add(replaceTitle);
                }
                else if (title.Contains(" Folio "))
                {
                    replaceTitle = replaceTitle.Replace(" Folio ", " /");
                    titlesToMatch.Add(replaceTitle);
                    replaceTitle = replaceTitle.Replace("/", ":");
                    titlesToMatch.Add(replaceTitle);
                    replaceTitle = replaceTitle.Replace(":", "-");
                    titlesToMatch.Add(replaceTitle);
                }
            }
            return titlesToMatch;
        }
        /// <summary>
        /// Returns the string of the Consignment Destination based on <see cref="ConsignmentCategoryEnum"/>
        /// </summary>
        /// <param name="destinationTypeId">The <see cref="ConsignmentCategoryEnum"/> cast as an integer</param>
        /// <param name="destOffice">The Destination Office string.</param>
        /// <param name="destSettlementAgent">The Destination Settlement Agent.</param>
        /// <param name="destOtherName">Destination Other Name string.</param>
        /// <param name="destTrustee">Trustee Destination in string format.</param>
        /// <returns>The Destination Name based on the <paramref name="destinationTypeId"/></returns>
        private string GetDestination(int destinationTypeId, string destOffice, string destSettlementAgent, string destOtherName, string destTrustee)
        {
            switch (destinationTypeId)
            {
                case (int) ConsignmentCategoryEnum.MSAOffice:
                    return destOffice;

                case (int) ConsignmentCategoryEnum.Agent:
                    return destSettlementAgent;

                case (int) ConsignmentCategoryEnum.SecurityPacket:
                    return destTrustee;

                case (int) ConsignmentCategoryEnum.Archive:
                    return string.Empty;

                default:
                    return destOtherName;
            }

        }
        /// <summary>
        /// Get The <see cref="ConsignmentDeliveryCompanyView"/> based on the <paramref name="Id"/>
        /// </summary>
        /// <param name="Id">A <see cref="ConsignmentDeliveryCompany.ConsignmentDeliveryCompanyId"/> to filter by.</param>
        /// <returns>Returns the <see cref="ConsignmentDeliveryCompanyView"/> for the <paramref name="Id"/></returns>
        public ConsignmentDeliveryCompanyView GetDeliveryCompanyView(int Id)
        {
            var rep = new Repository<ConsignmentDeliveryCompany>(context);
            var company = rep.FindById(Id);

            if (company == null) return null;

            return new ConsignmentDeliveryCompanyView
            {
                ConsignmentDeliveryCompanyId = company.ConsignmentDeliveryCompanyId,
                DeliveryCompanyName = company.DeliveryCompanyName,
                Enabled = company.Enabled,
                UpdatedBy = company.User.Username,
                UpdatedByUserId = company.UpdatedByUserId
            };
        }
        /// <summary>
        /// Get an Enumerable collection of all <see cref="ConsignmentDeliveryCompanyView"/>.
        /// </summary>
        /// <returns>All Consignment Delivery Views.</returns>
        public IEnumerable<ConsignmentDeliveryCompanyView> GetDeliveryCompanies()
        {
            return context.ConsignmentDeliveryCompanies.
                Select(company => new ConsignmentDeliveryCompanyView
                {
                    ConsignmentDeliveryCompanyId = company.ConsignmentDeliveryCompanyId,
                    DeliveryCompanyName = company.DeliveryCompanyName,
                    Enabled = company.Enabled,
                    UpdatedBy = company.User.Username,
                    UpdatedByUserId = company.UpdatedByUserId,
                    UpdatedDate = company.UpdatedDate
                }).ToList();
        }
        /// <summary>
        /// Get all Consighnment Content Type Views
        /// </summary>
        /// <returns>A Collection of all Consignment Content Types</returns>
        public IEnumerable<ConsignmentContentTypeView> GetConsignmentContentTypes()
        {
            return context.ConsignmentContentTypes.
                Select(c => new ConsignmentContentTypeView
                {
                    ConsignmentContentTypeId = c.ConsignmentContentTypeId,
                    ConsignmentContentTypeName = c.ConsignmentContentTypeName,
                    ConsignmentContentTypeDesc = c.ConsignmentContentTypeDesc,
                    UpdatedBy = c.User.Username,
                    UpdatedByUserId = c.UpdatedByUserId,
                    UpdatedDate = c.UpdatedDate
                }).ToList();
        }

        /// <summary>
        /// Gets the Consignment View From a Matter.
        /// </summary>
        /// <param name="consignmentId">The <see cref="Consignment.ConsignmentId"/></param>
        /// <param name="matterId">The <see cref="Matter.MatterId"/></param>
        /// <param name="id">The Graphical User ID </param>
        /// <returns>Returns the Matters Consignment view.</returns>
        public ConsignmentMatterView GetConsignmentViewFromMatter(int consignmentId, int matterId, Guid id)
        {
            int consignmentTypeId = context.Consignments.FirstOrDefault(c => c.ConsignmentId == consignmentId)?.ConsignmentTypeId ?? 0;

            var mt = context.Matters.Where(x => x.MatterId == matterId).
                Select(s =>
                    new ConsignmentMatterView
                    {
                        Id = id,
                        ConsignmentId = consignmentId,
                        MatterId = matterId,
                        isDirty = true,
                        AssociatedMatter = new ConsignmentMatterMatterView
                        {
                            FileOwner = s.User.Username,
                            MatterDescription = s.MatterDescription,
                            MatterStatusTypeId = s.MatterStatusTypeId,
                            MatterStatus = s.MatterStatusType.MatterStatusTypeName,
                            CloseMatterStarted = s.MatterWFComponents.Any(x => x.WFComponentId == (int)WFComponentEnum.CloseMatter
                                          && (x.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.Starting || x.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.InProgress || x.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.Complete)
                                          && (x.DisplayStatusTypeId == (int)DisplayStatusTypeEnum.Default || x.DisplayStatusTypeId == (int)DisplayStatusTypeEnum.Display)),
                            CloseMatterCompleted = s.MatterWFComponents.Any(x => x.WFComponentId == (int)WFComponentEnum.CloseMatter
                                                   && (x.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.Complete)
                                                   && (x.DisplayStatusTypeId == (int)DisplayStatusTypeEnum.Default || x.DisplayStatusTypeId == (int)DisplayStatusTypeEnum.Display)),
                            CanClose = new Tuple<bool, string>(false, null),
                            Lender = s.Lender.LenderNameShort,
                            LenderRef = s.LenderRefNo,
                            Settled = s.Settled,
                            SettlementDate = s.SettlementSchedule.SettlementDate,
                            Reconciled = false,
                            MatterId = s.MatterId,
                            LenderId = s.LenderId,
                            StateId = s.StateId,
                            MatterGroupTypeId = s.MatterGroupTypeId
                        }
                    }
                ).FirstOrDefault();

            mt.AssociatedMatter.CanClose = (consignmentTypeId == (int)ConsignmentTypeEnum.SecurityPacket || consignmentTypeId == (int)ConsignmentTypeEnum.Archive) ?
                                  new MatterRepository(context).CanMatterBeClosed(mt.AssociatedMatter.MatterId, mt.AssociatedMatter.LenderId, mt.AssociatedMatter.StateId, mt.AssociatedMatter.MatterGroupTypeId, consignmentTypeId) :
                                  new Tuple<bool, string>(false, null);

            return mt;
        }
        /// <summary>
        /// Load Consignments data onto a Matter 
        /// </summary>
        /// <param name="currMatter">The Current <see cref="Matter"/> view to load the consignments data onto.</param>
        public void LoadConsignmentsForMatter(ref MatterCustomEntities.MatterView currMatter)
        {
            if (currMatter == null) return;
            int matterId = currMatter.MatterId;
            var consignmentList = new List<ConsignmentDisplayView>();
            var consignmentDocsList = new List<ConsignmentDisplayDocView>();

            var consignments =
                (from c in context.Consignments
                 join cm in context.ConsignmentMatters on c.ConsignmentId equals cm.ConsignmentId
                 where cm.MatterId == matterId
                 select new
                 {
                     c.ConsignmentId,
                     c.ConsignmentTypeId,
                     c.ConsignmentType.ConsignmentTypeName,
                     c.DeliveryRefNo,
                     c.BarCodeBoxRef,
                     c.DateSent,
                     c.ConsignmentCategoryId,
                     c.ConsignmentCategory.ConsignmentCategoryName,
                     SourceOfficeName = c.Office.OfficeName,
                     DestOfficeName = c.Office1.OfficeName,
                     DestAgent = c.SettlementAgent.AgentName,
                     DestOtherName = c.DestOther_Name,
                     DestTrustee = c.ConsignmentTrustee.ConsignmentTrusteeName,
                     AccountNo = cm.AccountNo,
                     BSB = cm.BSB,
                     FeeAmount = cm.FeeAmount,
                 }).DistinctBy(c => c.ConsignmentId).ToList();

            int i = 1;
            foreach (var c in consignments)
            {
                var displayView =
                    (new ConsignmentDisplayView(c.ConsignmentTypeId)
                    {
                        ConsignmentId = c.ConsignmentId,
                        ConsignmentType = c.ConsignmentTypeName,
                        DeliveryReference = c.DeliveryRefNo,
                        BarCodeReference = c.BarCodeBoxRef,
                        SourceOfficeName = c.SourceOfficeName,
                        CategoryName = c.ConsignmentCategoryName,
                        DateSent = c.DateSent,
                        Destination = GetDestination(c.ConsignmentCategoryId, c.DestOfficeName, c.DestAgent, c.DestOtherName, c.DestTrustee),
                        RowNo = i
                    });

                displayView.DateSentVisibility = displayView.DateSent.HasValue;

                if (displayView.ConsignmentTypeEnum == ConsignmentTypeEnum.Banking)
                {
                    displayView.BankingString = FormatBankingString(c.AccountNo, c.BSB, c.FeeAmount);
                }

                consignmentList.Add(displayView);
                i++;
            }
            currMatter.Consignments = consignmentList;

            var consignmentWithDocs = context.Consignments.AsNoTracking().Where(c => c.ConsignmentMatters.Any(cm => !cm.CompleteFile && cm.MatterId == matterId));

            currMatter.ConsignmentsWithDocs = consignmentWithDocs
                .Select(x => new ConsignmentWithDocsView
                {
                    ConsignmentId = x.ConsignmentId,
                    DisplayDocs = x.ConsignmentMatters.Where(w => w.MatterId == matterId).Select(cm2 =>
                              new ConsignmentDisplayDocView { ConsignmentId = cm2.ConsignmentId, Contents = cm2.Contents, ContentType = cm2.ConsignmentContentType.ConsignmentContentTypeName })
                }).ToList();
        }

        /// <summary>
        /// Load data for matter consignments into a limited class so that the entire matter view isn't required.
        /// </summary>
        /// <param name="matterId"></param>
        /// <returns></returns>
        public MatterConsignmentsView LoadConsignmentsForMatterId(int matterId)

        {
    
            var consignmentList = new List<ConsignmentDisplayView>();
            var consignmentDocsList = new List<ConsignmentDisplayDocView>();

            var consignments =
                (from c in context.Consignments
                 join cm in context.ConsignmentMatters on c.ConsignmentId equals cm.ConsignmentId
                 where cm.MatterId == matterId
                 select new
                 {
                     c.ConsignmentId,
                     c.ConsignmentTypeId,
                     c.ConsignmentType.ConsignmentTypeName,
                     c.DeliveryRefNo,
                     c.BarCodeBoxRef,
                     c.DateSent,
                     c.ConsignmentCategoryId,
                     c.ConsignmentCategory.ConsignmentCategoryName,
                     SourceOfficeName = c.Office.OfficeName,
                     DestOfficeName = c.Office1.OfficeName,
                     DestAgent = c.SettlementAgent.AgentName,
                     DestOtherName = c.DestOther_Name,
                     DestTrustee = c.ConsignmentTrustee.ConsignmentTrusteeName,
                     AccountNo = cm.AccountNo,
                     BSB = cm.BSB,
                     FeeAmount = cm.FeeAmount,
                 }).DistinctBy(c => c.ConsignmentId).ToList();

            int i = 1;
            foreach (var c in consignments)
            {
                var displayView =
                    (new ConsignmentDisplayView(c.ConsignmentTypeId)
                    {
                        ConsignmentId = c.ConsignmentId,
                        ConsignmentType = c.ConsignmentTypeName,
                        DeliveryReference = c.DeliveryRefNo,
                        BarCodeReference = c.BarCodeBoxRef,
                        SourceOfficeName = c.SourceOfficeName,
                        CategoryName = c.ConsignmentCategoryName,
                        DateSent = c.DateSent,
                        Destination = GetDestination(c.ConsignmentCategoryId, c.DestOfficeName, c.DestAgent, c.DestOtherName, c.DestTrustee),
                        RowNo = i
                    });

                displayView.DateSentVisibility = displayView.DateSent.HasValue;

                if (displayView.ConsignmentTypeEnum == ConsignmentTypeEnum.Banking)
                {
                    displayView.BankingString = FormatBankingString(c.AccountNo, c.BSB, c.FeeAmount);
                }

                consignmentList.Add(displayView);
                i++;
            }

            var toReturn = new MatterConsignmentsView();

            toReturn.Consignments = consignmentList;

            var consignmentWithDocs = context.Consignments.AsNoTracking().Where(c => c.ConsignmentMatters.Any(cm => !cm.CompleteFile && cm.MatterId == matterId));

            toReturn.ConsignmentsWithDocs = consignmentWithDocs
                .Select(x => new ConsignmentWithDocsView
                {
                    ConsignmentId = x.ConsignmentId,
                    DisplayDocs = x.ConsignmentMatters.Where(w => w.MatterId == matterId).Select(cm2 =>
                              new ConsignmentDisplayDocView { ConsignmentId = cm2.ConsignmentId, Contents = cm2.Contents, ContentType = cm2.ConsignmentContentType.ConsignmentContentTypeName })
                }).ToList();

            toReturn.AnyWithDocs = toReturn.ConsignmentsWithDocs.Any();

            return toReturn;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public IEnumerable<ConsignmentSecurityPacketListView> GetSecurityPacketsLastTenView(int userId)
        {
            return GetSecurityPacketListView(context.ConsignmentSecurityPacketTitles
                .Where(x => x.CreatedByUserId == userId).OrderByDescending(o => o.DateEntered).Take(10));
        }

        public IEnumerable<ConsignmentSecurityPacketListView> GetSecurityPacketTitlesByMatter(int matterId)
        {
            return GetSecurityPacketListView(context.ConsignmentSecurityPacketTitles.Where(x => x.MatterId == matterId));
        }

        public IEnumerable<ConsignmentSecurityPacketListView> GetSecurityPacketTitlesByROSNumber(string rosNumber)
        {
            return GetSecurityPacketListView(context.ConsignmentSecurityPacketTitles.Where(x => x.RamsLapNo == rosNumber));
        }

        public IEnumerable<ConsignmentSecurityPacketListView> GetSecurityPacketTitlesByTitleRef(string title)
        {
            return GetSecurityPacketListView(context.ConsignmentSecurityPacketTitles.Where(x => x.TitleReference == title));
        }

        public IEnumerable<ConsignmentSecurityPacketListView> GetSecurityPacketTitlesByBNYReference(string reference)
        {
            return GetSecurityPacketListView(context.ConsignmentSecurityPacketTitles.Where(x => x.BNYReference == reference));
        }

        public IEnumerable<ConsignmentSecurityPacketListView> GetSecurityPacketTitlesByDateEntered(DateTime dateEntered, int selectedMatch)
        {
            var date1 = dateEntered.Date;
            var date2 = date1.AddDays(1);
            var items = context.ConsignmentSecurityPacketTitles.Where(x => x.DateEntered >= date1 && x.DateEntered < date2);
            return GetMatchedSecurityPacketListItems(items, selectedMatch);
        }

        public IEnumerable<ConsignmentSecurityPacketListView> GetSecurityPacketTitlesByGreaterThanDateEntered(DateTime dateEntered, int selectedMatch)
        {
            var date1 = dateEntered.Date.AddDays(1);
            var items = context.ConsignmentSecurityPacketTitles.Where(x => x.DateEntered > date1);
            return GetMatchedSecurityPacketListItems(items, selectedMatch);
        }

        private IEnumerable<ConsignmentSecurityPacketListView> GetMatchedSecurityPacketListItems(IQueryable<ConsignmentSecurityPacketTitle> items, int selectedMatch)
        {
            if (selectedMatch ==  (int)ConsignmentSecurityPacketMatchedEnum.Matched)
            {
                items = items.Where(x => x.MatterId != null);
            }
            else if (selectedMatch == (int)ConsignmentSecurityPacketMatchedEnum.Unmatched)
            {
                items = items.Where(x => x.MatterId == null);
            }
            return GetSecurityPacketListView(items);
        }

        private IEnumerable<ConsignmentSecurityPacketListView> GetSecurityPacketListView(IQueryable<ConsignmentSecurityPacketTitle> consignments)
        {
            return
             consignments.Select(x => new ConsignmentSecurityPacketListView
             {
                 Id = Guid.NewGuid(),
                 ConsignmentSecurityPacketTitleId = x.ConsignmentSecurityPacketTitleId,
                 BnyReference = x.BNYReference,
                 Borrowers = x.Borrowers,
                 DateEntered = x.DateEntered,
                 LoanAccounts = x.LoanAccounts,
                 MatterId = x.MatterId.ToString(),
                 //CP: 9439 LenderId = x.LenderId,
                 RamsLapsNo = x.RamsLapNo,
                 TitleReference = x.TitleReference,
                 Notes = x.Notes,
                 CreatedByUserId = x.CreatedByUserId,
                 CreatedByUserName = x.User.Username,
                 UpdatedById = x.UpdatedByUserId,
                 UpdatedByUserName = x.User1.Username,
                 UpdatedDate = x.UpdatedDate,
                 isDirty = false
             });
        }
        /// <summary>
        /// Formats a banking string
        /// </summary>
        /// <param name="accountNo">The Account Number.</param>
        /// <param name="BSB">The BSB Number</param>
        /// <param name="feeAmount">The Fee amount</param>
        /// <returns></returns>
        private string FormatBankingString(string accountNo, string BSB, decimal? feeAmount)
        {
            return string.Format("{0}{1}{2}",
                string.IsNullOrEmpty(accountNo) ? string.Empty : $"Acct: {accountNo},",
                string.IsNullOrEmpty(BSB) ? string.Empty : $"BSB: {BSB},",
                feeAmount.HasValue ? string.Format("Amt: {0:c}", feeAmount) : string.Empty
                );
        }
        /// <summary>
        /// Delete a <see cref="Consignment"/> from the database.
        /// </summary>
        /// <param name="id">The <see cref="Consignment.ConsignmentId"/> to be deleted</param>
        public void DeleteConsignment(int id)
        {
            var consignment = context.Consignments.FirstOrDefault(x => x.ConsignmentId == id);
            if (consignment == null) return;

            context.ConsignmentMatters.RemoveRange(context.ConsignmentMatters.Where(x => x.ConsignmentId == id));
            context.Consignments.Remove(consignment);
            context.SaveChanges();
        }
    }
}
