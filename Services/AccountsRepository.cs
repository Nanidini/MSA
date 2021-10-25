using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Slick_Domain.Entities;
using Slick_Domain.Models;
using Slick_Domain.Enums;

namespace Slick_Domain.Services
{
    /// <summary>
    /// Contains key functionality for MatterLedgerItem manipulation, trust transaction item manipulation, and all updating / obtaining of this data for live views.
    /// <para>Many of these getter functions are mirrored in <see cref="MatterWFRepository"/> to obtain historic records of this information at that state in the workflow.</para>
    /// </summary>
    public class AccountsRepository : IDisposable
    {
        private readonly SlickContext context;
        /// <summary>
        /// Constructor, sets the context of this repository to the context it is fed by the UOW / Context parent instatiating it. 
        /// </summary>
        /// <param name="Context"></param>
        public AccountsRepository(SlickContext Context)
        {
            context = Context;
        }
        public AccountsRepository(bool noConnection)
        {
            //there are several methods here which don't actually use the database... rather than moving them all i made a constructor for this repo with no connection to use those - this is shit though so should remove it. -RQ
            context = null;
        }
        // Methods for General Ledger
        #region Matter Ledger Methods
        /// <summary>
        /// Creates entity view MatterLedgerItemView for a IQueryable of <see cref="MatterLedgerItem"/>
        /// </summary>
        /// <param name="matterLedgerItems">Queryable of <see cref="MatterLedgerItem"/> to obtain views for.</param>
        /// <returns>IEnumerable of <see cref="AccountsCustomEntities.MatterLedgerItemView"/></returns>
        public IEnumerable<AccountsCustomEntities.MatterLedgerItemView> GetMatterLedgersView(IQueryable<MatterLedgerItem> matterLedgerItems)
        {
            return matterLedgerItems.Select(m => new
            {
                m.MatterLedgerItemId,
                m.MatterId,
                m.Matter.MatterDescription,
                m.Matter.LenderId,
                m.Matter.Lender.LenderName,
                m.Matter.Lender.LenderNameShort,
                m.Matter.MortMgrId,
                m.Matter.MortMgr.MortMgrName,
                m.Matter.StateId,
                m.Matter.State.StateName,
                m.Matter.LenderRefNo,
                m.LedgerItemSourceTypeId,
                m.LedgerItemSourceType.LedgerItemSourceTypeName,
                m.Matter.Settled,
                SettlementDate = (DateTime?)m.Matter.SettlementSchedule.SettlementDate,
                m.TransactionTypeId,
                m.TransactionType.TransactionTypeName,
                m.Amount,
                m.GST,
                m.Description,
                m.PayableByTypeId,
                PayableByTypeName = m.PayableType.PayableTypeName,
                m.MatterLedgerItemStatusTypeId,
                m.MatterLedgerItemStatusType.MatterLedgerItemStatusTypeName,
                m.InvoiceId,
                m.Invoice.InvoiceNo,
                m.Invoice.InvoiceSentDate,
                m.Invoice.InvoicePaidDate,
                m.UpdatedDate,
                m.UpdatedByUserId,
                UpdatedByUsername = m.User.Username,
                UpdatedByLastname = m.User.Lastname,
                UpdatedByFirstname = m.User.Firstname
            }).ToList()
            .Select(m => new AccountsCustomEntities.MatterLedgerItemView(m.MatterLedgerItemId, m.MatterId, m.MatterDescription, m.LenderId, m.LenderName, m.LenderNameShort, m.MortMgrId, m.MortMgrName, m.StateId, m.StateName, m.LenderRefNo, 
                        m.LedgerItemSourceTypeId, m.LedgerItemSourceTypeName, m.Settled, m.SettlementDate, m.TransactionTypeId, m.TransactionTypeName, m.Amount, m.GST, m.Description, m.PayableByTypeId,
                        m.PayableByTypeName, m.MatterLedgerItemStatusTypeId, m.MatterLedgerItemStatusTypeName, m.InvoiceId, m.InvoiceNo, m.InvoiceSentDate, m.InvoicePaidDate, m.UpdatedDate, m.UpdatedByUserId, m.UpdatedByUsername,
                        Common.EntityHelper.GetFullName(m.UpdatedByLastname, m.UpdatedByFirstname)))
               .ToList();



        }

        /// <summary>
        /// Gets all MatterLedgerItemViews for matter
        /// </summary>
        /// <param name="matterId">Matter to get ledger items for</param>
        /// <returns>IEnumerable of <see cref="AccountsCustomEntities.MatterLedgerItemView"/></returns>
        public IEnumerable<AccountsCustomEntities.MatterLedgerItemView> GetMatterLedgerViewForMatter(int matterId)
        {
            IQueryable<MatterLedgerItem> ledgerItems = context.MatterLedgerItems.AsNoTracking().Where(m => m.MatterId == matterId && m.MatterLedgerItemStatusTypeId==(int)Enums.MatterLedgerItemStatusTypeEnum.Ready);
            
            return GetMatterLedgersView(ledgerItems);
        }

        /// <summary>
        /// Gets all Invoice Items for matter matching the parameters provided.
        /// </summary>
        /// <param name="matterId">REQUIRED: Matter to get invoice for</param>
        /// <param name="invoiceStatusTypeId"><list type="bullet">
        ///                                   <item>-1 to obtain All.</item>
        ///                                   <item>0 to obtain Not Invoiced.</item>
        ///                                   <item>1 to obtain Invoiced but not Paid.</item>
        ///                                   <item>1 to obtain Invoiced and Paid.</item>
        ///                                   </list> </param>
        /// <param name="settledStatusId"><list type="bullet">
        ///                                   <item>-1 to obtain all.</item>
        ///                                   <item>0 to obtain Not Settled.</item>
        ///                                   <item>1 to obtain Settled.</item>
        ///                               </list>
        ///                               </param>
        /// <param name="dateFrom">IF NON-NULL: Only return items where Matter settlement date greater than / equal to this date</param>
        /// <param name="dateTo">IF NON-NULL: Only return items where Matter settlement date less than / equal to this date</param>
        /// <returns></returns>
        public IEnumerable<AccountsCustomEntities.MatterLedgerItemView> GetMatterLedgerViewForMatter(int matterId, int invoiceStatusTypeId, int settledStatusId, DateTime? dateFrom, DateTime? dateTo)
        {
            IQueryable<MatterLedgerItem> ledgerItems = context.MatterLedgerItems.AsNoTracking().Where(m => m.MatterId == matterId && 
                                                                        m.TransactionTypeId == (int)TransactionTypeEnum.Invoice && 
                                                                        m.MatterLedgerItemStatusTypeId != (int)Enums.MatterLedgerItemStatusTypeEnum.Cancelled);


            if (invoiceStatusTypeId != -1)
            {
                switch (invoiceStatusTypeId)
                {
                    case 0:     //Not Invoiced
                        ledgerItems = ledgerItems.Where(m => !m.InvoiceId.HasValue);
                        break;
                    case 1:     //Invoiced but not paid
                        ledgerItems = ledgerItems.Where(m => m.InvoiceId.HasValue && m.Invoice.InvoiceSentDate.HasValue);
                        break;
                    case 2:     //Invoiced and paid
                        ledgerItems = ledgerItems.Where(m => m.InvoiceId.HasValue && m.Invoice.InvoicePaidDate.HasValue);
                        break;
                }
            }

            if (settledStatusId != -1)
            {
                switch (settledStatusId)
                {
                    case 0:     //Not Settled
                        ledgerItems = ledgerItems.Where(m => !m.Matter.Settled);
                        break;
                    case 1:     //Settled
                        ledgerItems = ledgerItems.Where(m => m.Matter.Settled);
                        break;
                }
            }

            if (dateFrom.HasValue)
                ledgerItems = ledgerItems.Where(m => m.Matter.SettlementSchedule.SettlementDate >= dateFrom);

            if (dateTo.HasValue)
                ledgerItems = ledgerItems.Where(m => m.Matter.SettlementSchedule.SettlementDate <= dateTo);

            return GetMatterLedgersView(ledgerItems);
        }

        /// <summary>
        /// Override to call the Save Fee for Matter Function
        /// </summary>
        /// <param name="feeName">Name of the fee to add</param>
        /// <param name="payableByTypeId"><see cref="PayableTypeEnum"/> of who is paying the fee</param>
        /// <param name="mv">Compact Matter View (<see cref="MatterCustomEntities.MatterViewCompact"/>) to get matter details from</param>
        public void SaveFeeForMatter(string feeName, int? payableByTypeId, MatterCustomEntities.MatterViewCompact mv)
        {
            SaveFeeForMatter(feeName, payableByTypeId, mv.MatterId, mv.MatterGroupTypeId, mv.StateId, mv.LenderId, mv.MortMgrId);
        }

        /// <summary>
        /// Obtains and adds the best fit fee based on name, date, matter group type, state, lender and mortgage manager.
        /// </summary>
        /// <param name="feeName">Name of the fee to add</param>
        /// <param name="payableByTypeId"><see cref="PayableTypeEnum"/> of who is paying the fee</param>
        /// <param name="matterId">ID of the <see cref="Matter"/> to add the fee to</param>
        /// <param name="matterTypeId">ID of the matter's <see cref="MatterGroupTypeEnum"/> (new loan / discharge / consent) to obtain fee from, if applicable.</param>
        /// <param name="stateId">ID Of the matter's <see cref="StateIdEnum"/> to obtain fee from, if applicable.</param>
        /// <param name="lenderId">ID of the matter's <see cref="Lender"/> to obtain fee for, if applicable.</param>
        /// <param name="mortMgrId">ID of the matter's <see cref="MortMgr"/> to obtain fee for, if applicable.</param>
        public void SaveFeeForMatter(string feeName, int? payableByTypeId, int matterId, int? matterTypeId, int? stateId, int? lenderId, int? mortMgrId)
        {
            var fRep = new FeesRepository(context);
            var flv = fRep.GetBestFeeListView(feeName, DateTime.Today, matterTypeId, stateId, lenderId, mortMgrId);

            if (flv != null)
                SaveFeeForMatter(flv.FeeDescription, flv.Amount, payableByTypeId.HasValue ? payableByTypeId.Value : flv.PayableByTypeId.HasValue ? flv.PayableByTypeId.Value : (int)PayableTypeEnum.Borrower, matterId);
        }

        /// <summary>
        /// Save a fee to <see cref="MatterLedgerItem"/> that has either been obtained by <see cref="FeesRepository.GetBestFeeListView(string, DateTime, int?, int?, int?, int?)">GetBestFeeListView</see> (or override) 
        /// or is a custom fee.
        /// </summary>
        /// <param name="feeDesc">Description of the Fee</param>
        /// <param name="feeAmount">Amount to charge</param>
        /// <param name="payableByTypeId">ID of the Fee's <see cref="PayableTypeEnum"/> of who is paying the fee</param>
        /// <param name="matterId">ID of the <see cref="Matter"/> to add the fee to</param>
        public void SaveFeeForMatter(string feeDesc, decimal feeAmount, int payableByTypeId, int matterId)
        {
            var mRep = new MatterRepository(context);
            var mv = mRep.GetMatterDetailsCompact(matterId);

            var mli = new MatterLedgerItem();
            mli.MatterId = mv.MatterId;
            if (mv.MatterGroupTypeId == (int)MatterGroupTypeEnum.Discharge)
            {
                if (mv.IsSelfActing)
                {
                    if (mv.IsPEXA)
                        mli.TransactionTypeId = (int)TransactionTypeEnum.PEXA;
                    else
                        mli.TransactionTypeId = (int)TransactionTypeEnum.EFT;
                    mli.PayableByAccountTypeId = (int)AccountTypeEnum.Trust;
                    mli.PayableByTypeId = (int)PayableTypeEnum.Borrower;
                }
                else
                {
                    mli.TransactionTypeId = (int)TransactionTypeEnum.Cheque;
                    mli.PayableByAccountTypeId = (int)AccountTypeEnum.External;
                    mli.PayableByTypeId = (int)PayableTypeEnum.Lender;
                }
            }
            if (mv.MatterGroupTypeId != (int)MatterGroupTypeEnum.Discharge)
            {
                if (payableByTypeId == (int)PayableTypeEnum.Borrower)
                {
                    if (mv.IsPEXA)
                        mli.TransactionTypeId = (int)TransactionTypeEnum.PEXA;
                    else
                        mli.TransactionTypeId = (int)TransactionTypeEnum.EFT;

                    mli.PayableByAccountTypeId = (int)AccountTypeEnum.Trust;
                    mli.PayableByTypeId = (int)PayableTypeEnum.Borrower;
                }
                else
                {
                    mli.TransactionTypeId = (int)TransactionTypeEnum.Invoice;
                    mli.PayableByAccountTypeId = (int)AccountTypeEnum.External;
                    mli.PayableByTypeId = payableByTypeId;
                }
            }
            mli.PayableToAccountTypeId = (int)AccountTypeEnum.MSA;
            mli.PayableToTypeId = (int)PayableTypeEnum.MSA;

            mli.LedgerItemSourceTypeId = (int)LedgerItemSourceTypeEnum.SLICK;
            mli.Amount = feeAmount;
            mli.GSTFree = false;
            mli.Description = feeDesc;
            mli.MatterLedgerItemStatusTypeId = (int)MatterLedgerItemStatusTypeEnum.Ready;
            mli.FundsTransferredTypeId = (int)FundsTransferredTypeEnum.UnPaid;
            mli.ChequePrepared = false;
            mli.UpdatedDate = DateTime.Now;
            mli.UpdatedByUserId = GlobalVars.CurrentUser.UserId;


            context.MatterLedgerItems.Add(mli);
            context.SaveChanges();

            UpdateExpectedTrustTransactions(mv.MatterId);
        }

        /// <summary>
        /// Scrub a fee of given name from a Matter's invoice items.
        /// </summary>
        /// <param name="feeName">Name of the fee to find and remove from Matter</param>
        /// <param name="mv">Compact Matter Details in <see cref="MatterCustomEntities.MatterViewCompact"/> for the matter to add fees to</param>
        public void RemoveFeeForMatter(string feeName, MatterCustomEntities.MatterViewCompact mv)
        {
            RemoveFeeForMatter(feeName,null, mv);
        }

        


        /// <summary>
        /// Scrub a fee of given name paid by <see cref="PayableTypeEnum"/> (or null for anyone) from a Matter's invoice items.
        /// </summary>
        /// <param name="feeName">Name of the fee to find and remove from Matter</param>
        /// <param name="payableByTypeId">ID of the <see cref="PayableTypeEnum"/> to restrict removable fees from, or null for no restriction.</param>
        /// <param name="matterView">Compact Matter Details in <see cref="MatterCustomEntities.MatterViewCompact"/> for the matter to add fees to</param>
        public void RemoveFeeForMatter(string feeName, int? payableByTypeId, MatterCustomEntities.MatterViewCompact matterView)
        {
            var fd = context.Fees.FirstOrDefault(f => f.FeeName == feeName);

            if (fd != null)
            {

                var qry = context.MatterLedgerItems.Where(x => x.MatterId == matterView.MatterId &&
                                            x.PayableToAccountTypeId == (int)AccountTypeEnum.MSA &&
                                            x.PayableToTypeId == (int)PayableTypeEnum.MSA &&
                                            x.Description == fd.FeeDescription);

                if (matterView.MatterGroupTypeId == (int)MatterGroupTypeEnum.Discharge)
                {
                    if (matterView.IsSelfActing)
                    {
                        if (matterView.IsPEXA)
                            qry = qry.Where(x => x.TransactionTypeId == (int)TransactionTypeEnum.PEXA);
                        else
                            qry = qry.Where(x => x.TransactionTypeId == (int)TransactionTypeEnum.EFT);
                        qry = qry.Where(x => x.PayableByAccountTypeId == (int)AccountTypeEnum.Trust &&
                                         x.PayableByTypeId == (int)PayableTypeEnum.Borrower);
                    }
                    else
                    {
                        qry = qry.Where(x => x.TransactionTypeId == (int)TransactionTypeEnum.Cheque &&
                                             x.PayableByAccountTypeId == (int)AccountTypeEnum.External &&
                                             x.PayableByTypeId == (int)PayableTypeEnum.Lender);
                    }
                }
                else
                {
                    if (payableByTypeId == (int)PayableTypeEnum.Borrower)
                    {
                        if (matterView.IsPEXA)
                            qry = qry.Where(x => x.TransactionTypeId == (int)TransactionTypeEnum.PEXA);
                        else
                            qry = qry.Where(x => x.TransactionTypeId == (int)TransactionTypeEnum.EFT);

                        qry = qry.Where(x => x.PayableByAccountTypeId == (int)AccountTypeEnum.Trust &&
                                         x.PayableByTypeId == (int)PayableTypeEnum.Borrower);
                    }
                    else if(payableByTypeId == (int)PayableTypeEnum.Lender)
                    {
                        qry = qry.Where(x => x.TransactionTypeId == (int)TransactionTypeEnum.Invoice &&
                                             x.PayableToAccountTypeId == (int)AccountTypeEnum.External &&
                                             x.PayableByTypeId == payableByTypeId);
                    }
                }

                foreach (MatterLedgerItem mli in qry)
                {
                    RemoveMatterLedgerItem(mli);
                }
                context.SaveChanges();
            }

        }

        /// <summary>
        /// Get <see cref="AccountsCustomEntities.MatterLedgerItemView"/>s of all <see cref="MatterLedgerItem"/>s optionally filtered by invoice status, matter settlement status, settlement date.
        /// </summary>
        /// <param name="lenderList">Selected Lenders stored in an IEnumerable of <see cref="GeneralCustomEntities.GeneralCheckList"/> </param>       
        /// <param name="invoiceStatusTypeId"><list type="bullet">
        ///                                   <item>-1 to obtain All.</item>
        ///                                   <item>0 to obtain Not Invoiced.</item>
        ///                                   <item>1 to obtain Invoiced but not Paid.</item>
        ///                                   <item>1 to obtain Invoiced and Paid.</item>
        ///                                   </list> </param>
        /// <param name="settledStatusId"><list type="bullet">
        ///                                   <item>-1 to obtain All.</item>
        ///                                   <item>0 to obtain Not Settled.</item>
        ///                                   <item>1 to obtain Settled.</item>
        ///                               </list>
        ///                               </param>
        /// <param name="dateFrom">IF NON-NULL: Only return items where Matter settlement date greater than / equal to this date</param>
        /// <param name="dateTo">IF NON-NULL: Only return items where Matter settlement date less than / equal to this date</param>
        /// <returns></returns>
        public IEnumerable<AccountsCustomEntities.MatterLedgerItemView> GetMatterLedgerViewForMatters(IEnumerable<GeneralCustomEntities.GeneralCheckList> lenderList,
                                                                                                int invoiceStatusTypeId, int settledStatusId, DateTime? dateFrom, DateTime? dateTo)
        {

            IQueryable<MatterLedgerItem> ledgerItems = context.MatterLedgerItems.AsNoTracking().Where(m => m.MatterLedgerItemStatusTypeId == (int)Enums.MatterLedgerItemStatusTypeEnum.Ready);

            //Lender List selections
            if (lenderList.Where(l => l.Id == -1 && l.IsChecked).Count() == 0)
            {
                //Must be some specific Lenders selected, but not the Select-All
                var llIst = lenderList.Where(l => l.IsChecked == true).Select(l => l.Id).ToList();
                ledgerItems = ledgerItems.Where(m => llIst.Contains((m.Matter.LenderId)));
            }

            if (invoiceStatusTypeId != -1)
            {
                switch (invoiceStatusTypeId)
                {
                    case 0:     //Not Invoiced
                        ledgerItems = ledgerItems.Where(m => !m.InvoiceId.HasValue);
                        break;
                    case 1:     //Invoiced but not paid
                        ledgerItems = ledgerItems.Where(m => m.InvoiceId.HasValue && !m.Invoice.InvoicePaidDate.HasValue);
                        break;
                    case 2:     //Invoiced and paid
                        ledgerItems = ledgerItems.Where(m => m.InvoiceId.HasValue && m.Invoice.InvoicePaidDate.HasValue);
                        break;
                }
            }

            if (settledStatusId != -1)
            {
                //Need to always include "Cancelled" 
                switch (settledStatusId)
                {
                    case 0:     //Not Settled
                        ledgerItems = ledgerItems.Where(m => !m.Matter.Settled);
                        break;
                    case 1:     //Settled
                        ledgerItems = ledgerItems.Where(m => m.Matter.Settled || m.Matter.MatterStatusTypeId == (int)Enums.MatterStatusTypeEnum.NotProceeding);
                        break;
                }
            }

            if (dateFrom.HasValue)
                ledgerItems = ledgerItems.Where(m => m.Matter.SettlementSchedule.SettlementDate >= dateFrom);

            if (dateTo.HasValue)
                ledgerItems = ledgerItems.Where(m => m.Matter.SettlementSchedule.SettlementDate <= dateTo);

            return GetMatterLedgersView(ledgerItems);
        }

        /// <summary>
        /// Obtain <see cref="AccountsCustomEntities.MatterLedgerItemView"/>s for all <see cref="MatterLedgerItem"/>s for a <see cref="Matter"/> payable by Borrower.
        /// </summary>
        /// <param name="matterId">Id of the <see cref="Matter"/> to get items from.</param>
        /// <returns>IEnumerable of <see cref="AccountsCustomEntities.MatterLedgerItemView"/></returns>
        public IEnumerable<AccountsCustomEntities.MatterLedgerItemView> GetBorrowerMatterLedgersView(int matterId)
        {
            IQueryable<MatterLedgerItem> ledgerItems = context.MatterLedgerItems.AsNoTracking().Where(m => m.MatterId == matterId && m.MatterLedgerItemStatusTypeId == (int)Enums.MatterLedgerItemStatusTypeEnum.Ready && m.PayableByTypeId == (int)PayableTypeEnum.Borrower);
            return GetMatterLedgersView(ledgerItems);
        }
        /// <summary>
        /// Obtain <see cref="AccountsCustomEntities.MatterLedgerItemView"/>s for all <see cref="MatterLedgerItem"/>s payable by Borrower for any matters settling on a given date.
        /// </summary>
        /// <param name="settlementDate">Settlement Date to obtain results for</param>
        /// <returns>IEnumerable of <see cref="AccountsCustomEntities.MatterLedgerItemView"/></returns>
        public IEnumerable<AccountsCustomEntities.MatterLedgerItemView> GetBorrowerMatterLedgersView(DateTime settlementDate)
        {
            IQueryable<MatterLedgerItem> ledgerItems = context.MatterLedgerItems.AsNoTracking().Where(m => m.PayableByTypeId == (int)PayableTypeEnum.Borrower && m.MatterLedgerItemStatusTypeId == (int)Enums.MatterLedgerItemStatusTypeEnum.Ready && m.Matter.SettlementSchedule.SettlementDate == settlementDate);
            return GetMatterLedgersView(ledgerItems);
        }
        /// <summary>
        /// Obtain <see cref="AccountsCustomEntities.MatterLedgerItemView"/>s for all <see cref="MatterLedgerItem"/>s payable by Borrower for any matters settling on a given date, optionally filtered by lender, state, whether or not invoiced, and whether or not reconciled. 
        /// </summary>
        /// <param name="settlementDateFrom">IF NON-NULL: Only return items where Matter settlement date greater than / equal to this date</param>
        /// <param name="settlementDateTo">IF NON-NULL: Only return items where Matter settlement date less than / equal to this date</param>
        /// <param name="lenderId">ID of <see cref="Lender"/> to filter borrower ledger items by, or null for all.</param>
        /// <param name="stateId">ID of <see cref="StateIdEnum"/> to filter borrower ledger items by, or null for all.</param>
        /// <param name="IsInvoiced">Bool for whether or not to only show invoiced / uninvoiced items - or null for both.</param>
        /// <param name="IsReconciled">Bool for whether or not to only show reconciled / unreconciled items - or null for both.</param>
        /// <returns>IEnumerable of <see cref="AccountsCustomEntities.MatterLedgerItemView"/></returns>
        public IEnumerable<AccountsCustomEntities.MatterLedgerItemView> GetBorrowerMatterLedgersView(DateTime? settlementDateFrom, DateTime? settlementDateTo, int? lenderId, int? stateId, bool? IsInvoiced, bool? IsReconciled)
        {

            IQueryable<MatterLedgerItem> ledgerItems = context.MatterLedgerItems.AsNoTracking()
                .Where(m => m.PayableByTypeId == (int)PayableTypeEnum.Borrower && m.MatterLedgerItemStatusTypeId == (int)Enums.MatterLedgerItemStatusTypeEnum.Ready);

            if (settlementDateFrom.HasValue)
                ledgerItems = ledgerItems.Where(l => l.Matter.SettlementSchedule.SettlementDate >= settlementDateFrom);
            if (settlementDateTo.HasValue)
                ledgerItems = ledgerItems.Where(l => l.Matter.SettlementSchedule.SettlementDate <= settlementDateTo);
            if (lenderId.HasValue)
                ledgerItems = ledgerItems.Where(l => l.Matter.LenderId == lenderId);
            if (stateId.HasValue)
                ledgerItems = ledgerItems.Where(l => l.Matter.StateId == stateId);
            if (IsInvoiced.HasValue)
            {
                if (IsInvoiced.Value)
                    ledgerItems = ledgerItems.Where(l => l.Invoice.InvoiceSentDate.HasValue);
                else
                    ledgerItems = ledgerItems.Where(l => !l.Invoice.InvoiceSentDate.HasValue);
            }
            if (IsReconciled.HasValue)
            {
                if (IsReconciled.Value)
                    ledgerItems = ledgerItems.Where(l => l.Invoice.InvoicePaidDate.HasValue);
                else
                    ledgerItems = ledgerItems.Where(l => !l.Invoice.InvoicePaidDate.HasValue);
            }
            return GetMatterLedgersView(ledgerItems);
        }

        /// <summary>
        /// Attempt to obtain pre-fill ledger item data for a <see cref="MatterLedgerItem"/> from <see cref="LenderDisbursementOption"/> and <see cref="MortMgrDisbursementOption"/> 
        /// by matching on lender, mortgage manager, funding channel, whether or not the matter is fast refinance, etc.
        /// </summary>
        /// <param name="mli">Partially filled <see cref="MatterLedgerItem"/> to update</param>
        /// <param name="disbursementOptionlookup">Description to check against the DisbursementOption tables to obtain details for</param>
        /// <param name="policyNo">Optional Insurance Policy Number to use as reference for Title Insurance disbursements.</param>
        public void UpdateEFTforMLI(MatterLedgerItem mli, string disbursementOptionlookup, string policyNo = null/*, bool fastRefi = false*/)
        {
        
            var mv = context.Matters.AsNoTracking().Where(m => m.MatterId == mli.MatterId).Select(m => new { m.StateId, m.LenderId, m.LenderRefNo, m.MortMgrId, m.FundingChannelTypeId, m.SecondaryRefNo }).FirstOrDefault();
            bool isFastRefi = context.MatterMatterTypes.Where(m => m.MatterId == mli.MatterId).Any(x => x.MatterTypeId == (int)Enums.MatterTypeEnum.FastRefinance || x.MatterTypeId == (int)Enums.MatterTypeEnum.RapidRefinance);
            //we also need to check for any fastrefi specific fees first, before going on to non fast refis. 
            LenderDisbursementOption eftAcc = null;
            //check for mortmgr specific options first, then fall back to lender ones.

            if (mv.FundingChannelTypeId.HasValue)
            {
                if (mv.MortMgrId.HasValue)
                {
                    if (isFastRefi)
                    {
                        eftAcc = context.LenderDisbursementOptions.AsNoTracking().FirstOrDefault(l => l.LenderId == mv.LenderId && l.MortMgrId == mv.MortMgrId && l.FastRefiSpecific && l.DisbursementOption == disbursementOptionlookup && l.FundingChannelTypeId == mv.FundingChannelTypeId);
                        if (eftAcc == null)
                        {
                            eftAcc = context.LenderDisbursementOptions.AsNoTracking().FirstOrDefault(l => l.LenderId == mv.LenderId && l.MortMgrId == mv.MortMgrId && l.FastRefiSpecific && l.DisbursementOption == disbursementOptionlookup);

                            if (eftAcc == null)
                            {
                                eftAcc = context.LenderDisbursementOptions.AsNoTracking().FirstOrDefault(l => l.LenderId == mv.LenderId && l.MortMgrId == null && l.FastRefiSpecific && l.DisbursementOption == disbursementOptionlookup && l.FundingChannelTypeId == mv.FundingChannelTypeId);

                                if (eftAcc == null)
                                {
                                    eftAcc = context.LenderDisbursementOptions.AsNoTracking().FirstOrDefault(l => l.LenderId == mv.LenderId && l.MortMgrId == null && l.FastRefiSpecific && l.DisbursementOption == disbursementOptionlookup);
                                }
                            }
                        }
                    }
                    
                    if(eftAcc == null)
                    {
                        eftAcc = context.LenderDisbursementOptions.AsNoTracking().FirstOrDefault(l => l.LenderId == mv.LenderId && l.MortMgrId == mv.MortMgrId && !l.FastRefiSpecific &&  l.DisbursementOption == disbursementOptionlookup && l.FundingChannelTypeId == mv.FundingChannelTypeId);
                        if (eftAcc == null)
                        {
                            eftAcc = context.LenderDisbursementOptions.AsNoTracking().FirstOrDefault(l => l.LenderId == mv.LenderId && l.MortMgrId == mv.MortMgrId && !l.FastRefiSpecific && l.DisbursementOption == disbursementOptionlookup);

                            if (eftAcc == null)
                            {
                                eftAcc = context.LenderDisbursementOptions.AsNoTracking().FirstOrDefault(l => l.LenderId == mv.LenderId && l.MortMgrId == null && !l.FastRefiSpecific && l.DisbursementOption == disbursementOptionlookup && l.FundingChannelTypeId == mv.FundingChannelTypeId);

                                if (eftAcc == null)
                                {
                                    eftAcc = context.LenderDisbursementOptions.AsNoTracking().FirstOrDefault(l => l.LenderId == mv.LenderId && l.MortMgrId == null && !l.FastRefiSpecific && l.DisbursementOption == disbursementOptionlookup);
                                }
                            }
                        }
                    }
                }
                else
                {

                    if (isFastRefi)
                    {
                        eftAcc = context.LenderDisbursementOptions.AsNoTracking().FirstOrDefault(l => l.LenderId == mv.LenderId && l.FastRefiSpecific && l.MortMgrId == null && l.DisbursementOption == disbursementOptionlookup && l.FundingChannelTypeId == mv.FundingChannelTypeId);

                        if (eftAcc == null)
                        {
                            eftAcc = context.LenderDisbursementOptions.AsNoTracking().FirstOrDefault(l => l.LenderId == mv.LenderId && l.FastRefiSpecific && l.MortMgrId == null && l.DisbursementOption == disbursementOptionlookup);
                        }
                    }
                    if (eftAcc == null)
                    {

                        eftAcc = context.LenderDisbursementOptions.AsNoTracking().FirstOrDefault(l => l.LenderId == mv.LenderId && l.MortMgrId == null && !l.FastRefiSpecific && l.DisbursementOption == disbursementOptionlookup && l.FundingChannelTypeId == mv.FundingChannelTypeId);

                        if (eftAcc == null)
                        {
                            eftAcc = context.LenderDisbursementOptions.AsNoTracking().FirstOrDefault(l => l.LenderId == mv.LenderId && l.MortMgrId == null && !l.FastRefiSpecific && l.DisbursementOption == disbursementOptionlookup);
                        }
                    }
                }
            }
            else
            {

                if (isFastRefi)
                {
                    eftAcc = context.LenderDisbursementOptions.AsNoTracking().FirstOrDefault(l => l.LenderId == mv.LenderId && l.MortMgrId == mv.MortMgrId && l.FastRefiSpecific && l.DisbursementOption == disbursementOptionlookup);

                    if (eftAcc == null)
                    {
                        eftAcc = context.LenderDisbursementOptions.AsNoTracking().FirstOrDefault(l => l.LenderId == mv.LenderId && l.MortMgrId == null && l.FastRefiSpecific && l.DisbursementOption == disbursementOptionlookup);
                    }

                }
                if (eftAcc == null)
                {
                    eftAcc = context.LenderDisbursementOptions.AsNoTracking().FirstOrDefault(l => l.LenderId == mv.LenderId && l.MortMgrId == mv.MortMgrId && !l.FastRefiSpecific && l.DisbursementOption == disbursementOptionlookup);

                    if (eftAcc == null)
                    {
                        eftAcc = context.LenderDisbursementOptions.AsNoTracking().FirstOrDefault(l => l.LenderId == mv.LenderId && l.MortMgrId == null && !l.FastRefiSpecific && l.DisbursementOption == disbursementOptionlookup);
                    }
                }
            }
            

            if (eftAcc != null)
            {
                if (eftAcc.PayableTypeId != null) mli.PayableToTypeId = eftAcc.PayableTypeId.Value;
                if (eftAcc.PayableTypeId == (int)PayableTypeEnum.MSA)
                {
                    // Set Payable To Account
                    mli.PayableToAccountTypeId = (int)AccountTypeEnum.MSA;
                    // Add EFT Data for MSA

                    var offRep = new Services.OfficeRepository(context);
                    var officeRec = offRep.GetOfficeView(mv.StateId, mv.LenderId);
                    if (officeRec != null && officeRec.EFT_BSB != null && officeRec.EFT_AccountNo != null)
                    {
                        mli.EFT_AccountName = officeRec.EFT_AccountName;
                        mli.EFT_BSB = officeRec.EFT_BSB;
                        mli.EFT_AccountNo = officeRec.EFT_AccountNo;
                        mli.EFT_Reference = mli.MatterId.ToString();
                    }
                } else
                {
                    // Set Payable To Account
                    mli.PayableToAccountTypeId = (int)AccountTypeEnum.External;
                }

                // Set EFT reference
                switch (eftAcc.TransactionReferenceTypeId)
                {
                    case (int)Enums.TransactionReferenceType.MatterId:
                        mli.EFT_Reference = mli.MatterId.ToString();
                        break;

                    case (int)Enums.TransactionReferenceType.LenderRef:
                        mli.EFT_Reference = mv.LenderRefNo;
                       
                        break;

                    case (int)Enums.TransactionReferenceType.SecondaryRef:
                        mli.EFT_Reference = mv.SecondaryRefNo;
                        break;

                    default:
                        break;
                }

                if (mli.EFT_Reference != null && mli.EFT_Reference.Length > 64)
                {
                    mli.EFT_Reference = mli.EFT_Reference.Substring(0, 58) + "...";
                }
                else if (String.IsNullOrEmpty(mli.EFT_Reference))
                {
                    mli.EFT_Reference = context.Matters.FirstOrDefault(m => m.MatterId == mli.MatterId).LenderRefNo;
                }

                // Set Transaction Type and its parent type
                if (eftAcc.TransactionTypeId != null) mli.TransactionTypeId = eftAcc.TransactionTypeId.Value;
                if (eftAcc.TransactionTypeId != null && eftAcc.TransactionType.ParentTransactionTypeId != null)
                {
                    //mli.TransactionType = context.TransactionTypes.AsNoTracking().Where(t => t.TransactionTypeId == eftAcc.TransactionTypeId).FirstOrDefault();
                    if (mli.TransactionType != null)
                        mli.TransactionType.ParentTransactionTypeId = eftAcc.TransactionType.ParentTransactionTypeId.Value;
                }

                // General EFT DATA from Admin Panel
                if (!String.IsNullOrEmpty(eftAcc.EFT_AccountName)) mli.EFT_AccountName = eftAcc.EFT_AccountName;
                if (!String.IsNullOrEmpty(eftAcc.EFT_BSB)) mli.EFT_BSB = eftAcc.EFT_BSB;
                if (!String.IsNullOrEmpty(eftAcc.EFT_AccountNo)) mli.EFT_AccountNo = eftAcc.EFT_AccountNo;

                // Add References
                if (disbursementOptionlookup.ToUpper().Contains("TITLE INSURANCE") && !disbursementOptionlookup.ToUpper().Contains("PEXA FEE CT")) // EFT Reference for Title Insurance is the Insurance data
                {
                    if (policyNo != null) //This exists because milestone Doc Preparation, does not commit the Policy number until the milestone is completed. The temporary repo's policy number is passed on to this function as an optional value
                    {
                        mli.EFT_Reference = policyNo;
                    }
                    else
                    {
                        // Load separate context for Title Insurance since it only occurs once or twice per matter
                        var mvFT = context.Matters.AsNoTracking().Where(m => m.MatterId == mli.MatterId).Select(m => new { m.LenderId, m.MatterFirstTitles }).FirstOrDefault();
                        //override EFT reference
                        mli.EFT_Reference = mvFT.MatterFirstTitles.Select(x => x.PolicyNumber).FirstOrDefault();
                    }
                    
                }
                else if (eftAcc.PayableTypeId == (int)PayableTypeEnum.MortMgr || eftAcc.PayableTypeId == (int)PayableTypeEnum.Lender)
                {
                    // Determining EFT data for Mortgage Manager and MSA: If MM fails, use Lender
                    // This will override the current EFT data from Mortgage Manager EFT Data (instead of Lender)
                    bool eftDone = false;
                    if (eftAcc.PayableTypeId == (int)PayableTypeEnum.MortMgr)
                    {
                        var eftAccMM = context.MortMgrDisbursementOptions.AsNoTracking().FirstOrDefault(l => l.MortMgrId == mv.MortMgrId && l.DisbursementOption == disbursementOptionlookup);
                        if (eftAccMM != null)
                        {
                            mli.EFT_AccountName = eftAccMM.EFT_AccountName;
                            mli.EFT_BSB = eftAccMM.EFT_BSB;
                            mli.EFT_AccountNo = eftAccMM.EFT_AccountNo;
                            mli.EFT_Reference = mv.LenderId == 139 ? mv.SecondaryRefNo : mv.LenderRefNo;
                            eftDone = true;
                        }
                    }

                    if (!eftDone)
                    {
                        if (eftAcc == null)
                        {
                            mli.EFT_Reference = mv.LenderRefNo;
                        }
                    }
                }
                else
                {
                    mli.EFT_Reference = mli.MatterId.ToString();
                }

            }

            else if (mli.PayableToTypeId == (int)PayableTypeEnum.MSA)
            {
                // Set Payable To Account
                mli.PayableToAccountTypeId = (int)AccountTypeEnum.MSA;

                // Add EFT Data for MSA
                var offRep = new Services.OfficeRepository(context);
                var officeRec = offRep.GetOfficeView(mv.StateId, mv.LenderId);
                if (officeRec != null && officeRec.EFT_BSB != null && officeRec.EFT_AccountNo != null)
                {
                    mli.EFT_AccountName = officeRec.EFT_AccountName;
                    mli.EFT_BSB = officeRec.EFT_BSB;
                    mli.EFT_AccountNo = officeRec.EFT_AccountNo;
                    mli.EFT_Reference = mli.MatterId.ToString();
                }
            }

        }

        public void setPayeeSelection(MatterCustomEntities.MatterFinDisbursementView rec, EntityCompacted item)
        {
            if (item != null)
            {
                rec.SelectedPayee = item;
            }
            else
            {
                rec.SelectedPayee = null;
            }
        }

        /// <summary>
        /// Attempt to obtain pre-fill ledger item data for a <see cref="MatterCustomEntities.MatterFinDisbursementView"/> from <see cref="LenderDisbursementOption"/> and <see cref="MortMgrDisbursementOption"/> 
        /// by matching on lender, mortgage manager, funding channel, whether or not the matter is fast refinance, etc.
        /// </summary>
        /// <param name="mli">Partially filled <see cref="MatterLedgerItem"/> to update</param>
        /// <param name="disbursementOptionlookup"></param>
        public void GetEFTforMFDV(MatterCustomEntities.MatterFinDisbursementView mfdv, string disbursementOptionlookup)
        {

            mfdv.PreviousDescription = mfdv.Description; // Save prior description so before this function is called, we check if we should not reset the data
            var mv = context.Matters.AsNoTracking().Where(m => m.MatterId == mfdv.MatterId).Select(m => new { m.StateId, m.LenderId, m.LenderRefNo, m.MortMgrId, m.FundingChannelTypeId}).FirstOrDefault();
            //var eftAcc = context.LenderDisbursementOptions.AsNoTracking().FirstOrDefault(l => l.LenderId == mv.LenderId && l.DisbursementOption == disbursementOptionlookup);
            bool isFastRefi = context.MatterMatterTypes.Where(m => m.MatterId == mfdv.MatterId).Any(x => x.MatterTypeId == (int)Enums.MatterTypeEnum.FastRefinance || x.MatterTypeId == (int)Enums.MatterTypeEnum.RapidRefinance);

            LenderDisbursementOption eftAcc = null;


            //Check for fees specific to lender, mort mgr, funding channel, fast refi, etc. 


            if (mv.FundingChannelTypeId.HasValue)
            {
                if (mv.MortMgrId.HasValue) //if the matter has a mortgage manager, first check for any mort mgr specific options - then, failing that, do the non mort mgr checks.
                {
                    if (isFastRefi)
                    {
                        eftAcc = context.LenderDisbursementOptions.AsNoTracking().FirstOrDefault(l => l.LenderId == mv.LenderId && l.FastRefiSpecific && l.MortMgrId == mv.MortMgrId.Value && l.DisbursementOption == disbursementOptionlookup && l.FundingChannelTypeId == mv.FundingChannelTypeId);
                        if (eftAcc == null)
                        {
                            eftAcc = context.LenderDisbursementOptions.AsNoTracking().FirstOrDefault(l => l.LenderId == mv.LenderId && l.FastRefiSpecific && l.MortMgrId == mv.MortMgrId.Value && l.DisbursementOption == disbursementOptionlookup && !l.FundingChannelTypeId.HasValue);

                            if (eftAcc == null)
                            {
                                eftAcc = context.LenderDisbursementOptions.AsNoTracking().FirstOrDefault(l => l.LenderId == mv.LenderId && l.FastRefiSpecific && l.MortMgrId == mv.MortMgrId.Value && l.DisbursementOption == disbursementOptionlookup);
                                if (eftAcc == null)
                                {
                                    eftAcc = context.LenderDisbursementOptions.AsNoTracking().FirstOrDefault(l => l.LenderId == mv.LenderId && l.FastRefiSpecific && l.MortMgrId == null && l.DisbursementOption == disbursementOptionlookup && l.FundingChannelTypeId == mv.FundingChannelTypeId);
                                    if (eftAcc == null)
                                    {
                                        eftAcc = context.LenderDisbursementOptions.AsNoTracking().FirstOrDefault(l => l.LenderId == mv.LenderId && l.FastRefiSpecific && l.MortMgrId == null && l.DisbursementOption == disbursementOptionlookup && !l.FundingChannelTypeId.HasValue);
                                        if (eftAcc == null)
                                        {
                                            eftAcc = context.LenderDisbursementOptions.AsNoTracking().FirstOrDefault(l => l.LenderId == mv.LenderId && l.FastRefiSpecific && l.MortMgrId == null && l.DisbursementOption == disbursementOptionlookup);
                                        }
                                    }
                                }
                            }
                        }
                    }

                    if (eftAcc == null)
                    {
                        eftAcc = context.LenderDisbursementOptions.AsNoTracking().FirstOrDefault(l => l.LenderId == mv.LenderId && !l.FastRefiSpecific && l.MortMgrId == mv.MortMgrId.Value && l.DisbursementOption == disbursementOptionlookup && l.FundingChannelTypeId == mv.FundingChannelTypeId);
                        if (eftAcc == null)
                        {
                            eftAcc = context.LenderDisbursementOptions.AsNoTracking().FirstOrDefault(l => l.LenderId == mv.LenderId && !l.FastRefiSpecific && l.MortMgrId == mv.MortMgrId.Value && l.DisbursementOption == disbursementOptionlookup && !l.FundingChannelTypeId.HasValue);

                            if (eftAcc == null)
                            {
                                eftAcc = context.LenderDisbursementOptions.AsNoTracking().FirstOrDefault(l => l.LenderId == mv.LenderId && !l.FastRefiSpecific && l.MortMgrId == mv.MortMgrId.Value && l.DisbursementOption == disbursementOptionlookup);
                                if (eftAcc == null)
                                {
                                    eftAcc = context.LenderDisbursementOptions.AsNoTracking().FirstOrDefault(l => l.LenderId == mv.LenderId && !l.FastRefiSpecific && l.MortMgrId == null && l.DisbursementOption == disbursementOptionlookup && l.FundingChannelTypeId == mv.FundingChannelTypeId);
                                    if (eftAcc == null)
                                    {
                                        eftAcc = context.LenderDisbursementOptions.AsNoTracking().FirstOrDefault(l => l.LenderId == mv.LenderId && !l.FastRefiSpecific && l.MortMgrId == null && l.DisbursementOption == disbursementOptionlookup && !l.FundingChannelTypeId.HasValue);
                                        if (eftAcc == null)
                                        {
                                            eftAcc = context.LenderDisbursementOptions.AsNoTracking().FirstOrDefault(l => l.LenderId == mv.LenderId && !l.FastRefiSpecific && l.MortMgrId == null && l.DisbursementOption == disbursementOptionlookup);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    if (isFastRefi)
                    {
                        eftAcc = context.LenderDisbursementOptions.AsNoTracking().FirstOrDefault(l => l.LenderId == mv.LenderId && l.FastRefiSpecific && l.MortMgrId == null && l.DisbursementOption == disbursementOptionlookup && l.FundingChannelTypeId == mv.FundingChannelTypeId);
                        if (eftAcc == null)
                        {
                            eftAcc = context.LenderDisbursementOptions.AsNoTracking().FirstOrDefault(l => l.LenderId == mv.LenderId && l.FastRefiSpecific && l.MortMgrId == null && l.DisbursementOption == disbursementOptionlookup && !l.FundingChannelTypeId.HasValue);
                            if (eftAcc == null)
                            {
                                eftAcc = context.LenderDisbursementOptions.AsNoTracking().FirstOrDefault(l => l.LenderId == mv.LenderId && l.FastRefiSpecific && l.MortMgrId == null && l.DisbursementOption == disbursementOptionlookup);
                            }
                        }
                    }

                    if (eftAcc == null)
                    {
                        eftAcc = context.LenderDisbursementOptions.AsNoTracking().FirstOrDefault(l => l.LenderId == mv.LenderId && !l.FastRefiSpecific && l.MortMgrId == null && l.DisbursementOption == disbursementOptionlookup && l.FundingChannelTypeId == mv.FundingChannelTypeId);
                        if (eftAcc == null)
                        {
                            eftAcc = context.LenderDisbursementOptions.AsNoTracking().FirstOrDefault(l => l.LenderId == mv.LenderId && !l.FastRefiSpecific && l.MortMgrId == null && l.DisbursementOption == disbursementOptionlookup && !l.FundingChannelTypeId.HasValue);
                            if (eftAcc == null)
                            {
                                eftAcc = context.LenderDisbursementOptions.AsNoTracking().FirstOrDefault(l => l.LenderId == mv.LenderId && !l.FastRefiSpecific && l.MortMgrId == null && l.DisbursementOption == disbursementOptionlookup);
                            }
                        }
                    }
                }
            }
            else
            {
                if (mv.MortMgrId.HasValue)
                {
                    if (isFastRefi)
                    {
                        eftAcc = context.LenderDisbursementOptions.AsNoTracking().FirstOrDefault(l => l.LenderId == mv.LenderId && l.FastRefiSpecific && l.MortMgrId == mv.MortMgrId.Value && l.DisbursementOption == disbursementOptionlookup);
                        if(eftAcc == null)
                        {
                            eftAcc = context.LenderDisbursementOptions.AsNoTracking().FirstOrDefault(l => l.LenderId == mv.LenderId && l.FastRefiSpecific && l.MortMgrId == null && l.DisbursementOption == disbursementOptionlookup);
                        }
                    }
                    if (eftAcc == null)
                    {
                        eftAcc = context.LenderDisbursementOptions.AsNoTracking().FirstOrDefault(l => l.LenderId == mv.LenderId && !l.FastRefiSpecific &&  l.MortMgrId == mv.MortMgrId.Value && l.DisbursementOption == disbursementOptionlookup);
                    }
                }
                if (eftAcc == null)
                {
                    if (isFastRefi)
                    {
                        eftAcc = context.LenderDisbursementOptions.AsNoTracking().FirstOrDefault(l => l.LenderId == mv.LenderId && l.FastRefiSpecific &&  l.MortMgrId == null && l.DisbursementOption == disbursementOptionlookup);
                    }
                    if (eftAcc == null)
                    {
                        eftAcc = context.LenderDisbursementOptions.AsNoTracking().FirstOrDefault(l => l.LenderId == mv.LenderId && l.MortMgrId == null && !l.FastRefiSpecific && l.DisbursementOption == disbursementOptionlookup);
                    }
                }
            }

            if (eftAcc != null)
            {
                
                // Payment Method from Admin Panel
                if (eftAcc.PayableTypeId.HasValue)
                {
                    mfdv.PayableToTypeId = eftAcc.PayableTypeId.Value;
                }
                
                mfdv.PayableToAccountTypeId = (int)AccountTypeEnum.External;
                if (mfdv.PayableToTypeId == (int)PayableTypeEnum.MSA)
                {
                    mfdv.PayableToAccountTypeId = (int)AccountTypeEnum.MSA;
                    // Add EFT Data for MSA
                    var offRep = new Services.OfficeRepository(context);
                    var officeRec = offRep.GetOfficeView(mv.StateId, mv.LenderId);
                    if (officeRec != null && officeRec.EFT_BSB != null && officeRec.EFT_AccountNo != null) //Double check for any empty fields that are important
                    {
                        mfdv.EFT_AccountName = officeRec.EFT_AccountName;
                        mfdv.EFT_BSB = officeRec.EFT_BSB;
                        mfdv.EFT_AccountNo = officeRec.EFT_AccountNo;
                        mfdv.EFT_Reference = mfdv.MatterId.ToString();
                    }
                }

                if (eftAcc.TransactionTypeId != null)
                {
                    mfdv.TransactionTypeId = eftAcc.TransactionTypeId.Value;
                    if (eftAcc.TransactionType.ParentTransactionTypeId == null) mfdv.ParentTransactionTypeId = eftAcc.TransactionTypeId.Value;
                }
                //General EFT DATA from Admin Panel - Will be overwritten by different types of payable
                if (!String.IsNullOrEmpty(eftAcc.EFT_AccountName)) mfdv.EFT_AccountName = eftAcc.EFT_AccountName;
                if (!String.IsNullOrEmpty(eftAcc.EFT_BSB)) mfdv.EFT_BSB = eftAcc.EFT_BSB;
                if (!String.IsNullOrEmpty(eftAcc.EFT_AccountNo)) mfdv.EFT_AccountNo = eftAcc.EFT_AccountNo;
                

                //Add References
                if (disbursementOptionlookup.ToUpper().Contains("TITLE INSURANCE") && !disbursementOptionlookup.ToUpper().Contains("PEXA FEE CT")) // EFT Reference for Title Insurance is the Insurance data
                {
                    // Load separate context for Title Insurance since it only occurs once or twice per matter
                    var mvFT = context.Matters.AsNoTracking().Where(m => m.MatterId == mfdv.MatterId).Select(m => new { m.LenderId, m.MatterFirstTitles }).FirstOrDefault();
                    //override EFT reference
                    if (mvFT != null)
                    {
                        mfdv.EFT_Reference = mvFT.MatterFirstTitles.Select(x => x.PolicyNumber).FirstOrDefault();
                    } else
                    {
                        mfdv.EFT_Reference = "";     
                    }
                    
                }
                else if (mfdv.PayableToTypeId == (int)PayableTypeEnum.MortMgr || mfdv.PayableToTypeId == (int)PayableTypeEnum.Lender)
                {
                    // Determining EFT data for Mortgage Manager and MSA: If MM fails, use Lender
                    // This will override the current EFT data from Mortgage Manager EFT Data (instead of Lender)
                    bool eftDone = false;
                    if (mfdv.PayableToTypeId == (int)PayableTypeEnum.MortMgr)
                    {
                        var eftAccMM = context.MortMgrDisbursementOptions.AsNoTracking().FirstOrDefault(l => l.MortMgrId == mv.MortMgrId && l.DisbursementOption == disbursementOptionlookup);
                        if (eftAccMM != null)
                        {
                            mfdv.EFT_AccountName = eftAccMM.EFT_AccountName;
                            mfdv.EFT_BSB = eftAccMM.EFT_BSB;
                            mfdv.EFT_AccountNo = eftAccMM.EFT_AccountNo;
                            mfdv.EFT_Reference = mv.LenderRefNo;
                            eftDone = true;
                        }
                    }

                    if (!eftDone)
                    {
                        if (eftAcc != null)
                        {
                            mfdv.EFT_Reference = mv.LenderRefNo;
                        }
                    }
                }
                else 
                {
                    mfdv.EFT_Reference = mfdv.MatterId.ToString();
                }

                // Set EFT reference
                switch (eftAcc.TransactionReferenceTypeId)
                {

                    case (int)Enums.TransactionReferenceType.MatterId:
                        mfdv.EFT_Reference = mfdv.MatterId.ToString();
                        break;
                    case (int)Enums.TransactionReferenceType.LenderRef:
                        mfdv.EFT_Reference = context.Matters.FirstOrDefault(m => m.MatterId == mfdv.MatterId).LenderRefNo;
                        break;
                    case (int)Enums.TransactionReferenceType.SecondaryRef:
                        mfdv.EFT_Reference = context.Matters.FirstOrDefault(m => m.MatterId == mfdv.MatterId).SecondaryRefNo;
                        break;
                    default:
                        break;
                }



                // If Default Transaction Type Exists, select it
                if (eftAcc.TransactionTypeId.HasValue)
                {
                    var mRep = new MatterRepository(context);
                    var matterView = mRep.GetMatterDetailsCompact(mfdv.MatterId);

                    var existingTypes = new List<int>() { (int)MatterTypeEnum.ExistingSecurity, (int)MatterTypeEnum.Increase };

                    matterView.IsPEXA = context.MatterSecurities.Where(d => d.MatterId == matterView.MatterId && d.Deleted == false)
                    .All(s => s.SettlementTypeId == (int)SettlementTypeEnum.PEXA || (s.SettlementTypeId == (int)SettlementTypeEnum.Paper &&
                    existingTypes.Contains(s.MatterTypeId)));

                    bool pexaWorkspaceIsSetUp = context.MatterPexaWorkspaces.Any(x => x.MatterId == mfdv.MatterId && x.MatterSecurityMatterPexaWorkspaces.Any());
                    
                    // PEXA condition here
                    if (matterView.IsPEXA && pexaWorkspaceIsSetUp && !disbursementOptionlookup.ToUpper().Contains("PEXA FEE CT"))
                    {
                        mfdv.ParentTransactionTypeId = (int)TransactionTypeEnum.PEXA;
                        mfdv.TransactionTypeId = (int)TransactionTypeEnum.PEXA;
                        mfdv.TransactionTypeName = "PEXA";

                        switch (mfdv.TransactionTypeId)
                        {
                            case (int)TransactionTypeEnum.EFT:
                            case (int)TransactionTypeEnum.EFTFree:
                            case (int)TransactionTypeEnum.TT:
                            case (int)TransactionTypeEnum.TTFree:
                                mfdv.EFT_Reference = mfdv.MatterId.ToString();
                                break;
                            default:
                                mfdv.EFT_Reference = "";
                                break;
                        }

                    }
                    else
                    {
                        if (eftAcc.TransactionType.ParentTransactionTypeId != null)
                        {
                            mfdv.ParentTransactionTypeId = eftAcc.TransactionType.ParentTransactionTypeId.Value;
                        }
                        mfdv.TransactionTypeId = eftAcc.TransactionTypeId.Value;
                        mfdv.TransactionTypeName = eftAcc.TransactionType.TransactionTypeDesc;
                    }
                   
                    mfdv.ShowBPayDetails = false;
                    mfdv.ShowEFTDetails = false;
                    mfdv.ShowPEXADetails = false;
                    mfdv.TransactionCanBePPS = false;
                    mfdv.TransactionCanBeFree = false;

                    switch (mfdv.TransactionTypeId)
                    {
                        case (int)TransactionTypeEnum.EFT:
                            mfdv.ShowEFTDetails = true;
                            mfdv.TransactionCanBeFree = true;
                            mfdv.TransactionIsPPS = false;
                            break;
                        case (int)TransactionTypeEnum.EFTFree:
                            mfdv.ShowEFTDetails = true;
                            mfdv.TransactionCanBeFree = true;
                            mfdv.TransactionIsFree = true;
                            mfdv.TransactionIsPPS = false;
                            break;
                        case (int)TransactionTypeEnum.TT:
                            mfdv.ShowEFTDetails = true;
                            mfdv.TransactionCanBeFree = true;
                            mfdv.TransactionIsPPS = false;
                            break;
                        case (int)TransactionTypeEnum.TTFree:
                            mfdv.ShowEFTDetails = true;
                            mfdv.TransactionCanBeFree = true;
                            mfdv.TransactionIsPPS = false;
                            break;
                        case (int)TransactionTypeEnum.BPay:
                            mfdv.ShowBPayDetails = true;
                            mfdv.TransactionCanBeFree = true;
                            mfdv.TransactionIsPPS = false;
                            break;
                        case (int)TransactionTypeEnum.BPayFree:
                            mfdv.ShowBPayDetails = true;
                            mfdv.TransactionCanBeFree = true;
                            mfdv.TransactionIsFree = true;
                            mfdv.TransactionIsPPS = false;
                            break;
                        case (int)TransactionTypeEnum.PEXA:
                            mfdv.ShowPEXADetails = true;
                            mfdv.TransactionIsPPS = false;
                            break;
                        case (int)TransactionTypeEnum.Cheque:
                            mfdv.TransactionCanBeFree = true;
                            mfdv.TransactionCanBePPS = true;
                            break;
                        case (int)TransactionTypeEnum.ChequeFree:
                            mfdv.TransactionCanBeFree = true;
                            mfdv.TransactionCanBePPS = true;
                            mfdv.TransactionIsFree = true;
                            break;
                        case (int)TransactionTypeEnum.TrustCheque:
                            mfdv.TransactionCanBeFree = true;
                            break;
                        case (int)TransactionTypeEnum.TrustChequeFree:
                            mfdv.TransactionCanBeFree = true;
                            mfdv.TransactionIsFree = true;
                            break;
                        case (int)TransactionTypeEnum.PPS:
                            mfdv.TransactionCanBeFree = true;
                            mfdv.TransactionCanBePPS = true;
                            mfdv.TransactionIsPPS = true;
                            break;
                        case (int)TransactionTypeEnum.PPSFree:
                            mfdv.TransactionCanBeFree = true;
                            mfdv.TransactionCanBePPS = true;
                            mfdv.TransactionIsFree = true;
                            mfdv.TransactionIsPPS = true;
                            break;
                        default:
                            break;
                    }

                }

                // If Other Party Name is not null or empty, override PayTo section
                // Else use the default the data from Admin session
                if (!string.IsNullOrEmpty(eftAcc.OtherPartyName))
                {
                    mfdv.PayableToOthersDesc = eftAcc.OtherPartyName;
                    mfdv.PayeeSearchText = eftAcc.OtherPartyName;
                    mfdv.PayeeText = eftAcc.OtherPartyName;
                   
                } else
                {
                    var cRep = new EntityCompactedRepository(context);
                    var PayableToTypeList_Disb = cRep.GetPayableTypeNamesExpanded(mfdv.MatterId);
                    if (eftAcc.PayableTypeId.HasValue) {
                        var nameFromPayeeSearch = PayableToTypeList_Disb.FirstOrDefault(x => x.RelatedDetail == eftAcc.PayableType.PayableTypeName);
                        if ((nameFromPayeeSearch == null && eftAcc.PayableTypeId == (int)PayableTypeEnum.MortMgr)) // Default to Lender if Mortgage Manager Fails
                        {
                            nameFromPayeeSearch = PayableToTypeList_Disb.FirstOrDefault(x => x.RelatedId == (int)PayableTypeEnum.Lender);
                        }
                        if (nameFromPayeeSearch != null)
                        {
                            mfdv.SelectedPayee = nameFromPayeeSearch;
                            mfdv.PayableToOthersDesc = mfdv.SelectedPayee.Details;
                            mfdv.PayeeSearchText = mfdv.SelectedPayee.Details;
                            mfdv.PayeeText = mfdv.SelectedPayee.Details;
                        }
                        
                    }
                }
            }


            if (context.MatterMatterTypes.Any(m=>m.MatterId == mfdv.MatterId && m.MatterTypeId == (int)Slick_Domain.Enums.MatterTypeEnum.FastRefinance) && !String.IsNullOrEmpty(mfdv.PayableByOthersDesc) && !String.IsNullOrEmpty(mfdv.EFT_AccountNo))
            {
                var fastRefiDetailToUse = context.MatterFastRefiDetails
                .Where(x => !String.IsNullOrWhiteSpace(x.EFT_AccountName) && x.EFT_AccountName.Contains(mfdv.PayableToOthersDesc)).Select(s => new { s.EFT_AccountName, s.EFT_AccountNo, s.EFT_BSB }).FirstOrDefault();
                if (fastRefiDetailToUse != null)
                {
                    mfdv.EFT_AccountName = fastRefiDetailToUse.EFT_AccountName;
                    mfdv.EFT_AccountNo = fastRefiDetailToUse.EFT_AccountNo;
                    mfdv.EFT_BSB = fastRefiDetailToUse.EFT_BSB;
                    mfdv.TransactionTypeId = (int)TransactionTypeEnum.EFT;

                    string mtSecondaryRefNo = context.Matters.FirstOrDefault(m => m.MatterId == mfdv.MatterId)?.SecondaryRefNo;

                    if (!string.IsNullOrEmpty(mtSecondaryRefNo))
                    {
                        if (context.MatterParties.FirstOrDefault(x => x.MatterId == mfdv.MatterId && !String.IsNullOrEmpty(x.Lastname)) != null)
                            mfdv.EFT_Reference = mtSecondaryRefNo + " - " + context.MatterParties.FirstOrDefault(x => x.MatterId == mfdv.MatterId && !String.IsNullOrEmpty(x.Lastname)).Lastname;
                        else mfdv.EFT_Reference = mtSecondaryRefNo;
                    }
                }
            }
        }

        /// <summary>
        /// Splits a Trust Transaction Item into two parts based on amounts. If the TTI only links to one <see cref="MatterLedgerItem"/> then this will also be split into two and each new ledger item linked to the respective new trust transaction item.
        /// </summary>
        /// <remarks>
        /// Typically used for when only a portion of an amount has been received by accounts, so part needs to be flagged while other part remains outstanding. 
        /// </remarks>
        /// <param name="originalTTIview">The <see cref="AccountsCustomEntities.TrustTransactionItemView"/> view of the original Trust Transaction Item to split.</param>
        /// <param name="amount1">The amount to save in the first trust transaction item.</param>
        /// <param name="amount2">The amount to save in the second trust transaction item.</param>
        /// <returns></returns>
        public bool SplitTrustTransactionItem(AccountsCustomEntities.TrustTransactionItemView originalTTIview, decimal amount1, decimal amount2)
        {
            bool saved = false;
            bool pexaGroupSplit = false;
            //first get and update the existing tti.
            var tti = context.TrustTransactionItems.Where(t => t.TrustTransactionItemId == originalTTIview.TrustTransactionItemId).FirstOrDefault();
            tti.Amount = amount1;
            
            //we need to update the amount
            var originalMLIqry = context.MatterLedgerItems.Where(m => m.MatterLedgerItemTrustTransactionItems.Select(t => t.TrustTransactionItemId).Contains(tti.TrustTransactionItemId));

            MatterLedgerItem origMLI = null;

            if (originalMLIqry.Count() > 1)
            {
                if (originalMLIqry.Select(x => x.TransactionTypeId).Where(t=> t != (int)TransactionTypeEnum.PEXA || t != (int)TransactionTypeEnum.PEXAdebit || t != (int)TransactionTypeEnum.PEXAsingle) !=null)
                {
                    var settled = context.Matters.Where(m => m.MatterId == tti.MatterId).Select(x => x.Settled).FirstOrDefault();
                    if (settled)
                    {
                        pexaGroupSplit = true;
                    }
                    else
                    {
                        throw (new Exception(message: "Can not split a grouped, PEXA transaction when Matter has not settled"));
                    }
                }
                else throw (new Exception(message: "Can not split a grouped, non-PEXA transaction"));
            }
            else if(originalMLIqry.Count() == 1)
            {
                origMLI = originalMLIqry.FirstOrDefault();
                origMLI.Amount = amount1;
            }
            if (pexaGroupSplit)
            {
                //tti.Description = originalMLIqry.FirstOrDefault().Description;
                tti.Description = "Various PEXA items";
            }
            context.SaveChanges();
            //now we need to add a new tti and mli - will be the exact same as the original but split

            TrustTransactionItem newTti = context.TrustTransactionItems.AsNoTracking().Where(t => t.TrustTransactionItemId == tti.TrustTransactionItemId).FirstOrDefault();

            newTti.Amount = amount2;

            if (pexaGroupSplit)
            {
                newTti.Description = "Various PEXA items";
            }

            context.TrustTransactionItems.Add(newTti);

            context.SaveChanges();

            //now the new mli

            if (origMLI != null && !pexaGroupSplit)
            {
                MatterLedgerItem newMLI = context.MatterLedgerItems.AsNoTracking().Where(m=>m.MatterLedgerItemId == origMLI.MatterLedgerItemId).FirstOrDefault();

                newMLI.Amount = amount2;
                newMLI.FundingRequestId = null;
                newMLI.FundingRequestMatterLedgerItems = null;
                newMLI.FundingRequest = null;

                context.MatterLedgerItems.Add(newMLI);

                context.SaveChanges();

                //set up the link between our new trust transaction and new mli 
                context.MatterLedgerItemTrustTransactionItems.Add(
                    new MatterLedgerItemTrustTransactionItem()
                    {
                        MatterLedgerItemId = newMLI.MatterLedgerItemId,
                        TrustTransactionItemId = newTti.TrustTransactionItemId
                    }
                 );

                context.SaveChanges();

            }

            saved = true;

            return saved;
        }

        /// <summary>
        /// Obtain the valid transaction types for ledger items and fill in the various UI flags in <see cref="MatterCustomEntities.MatterFinDisbursementView"/>, clear EFT details if changing from an EFT to BPAY as these store their details in the same fields. 
        /// </summary>
        /// <param name="mfdv"></param>
        /// <param name="selectedItemId"></param>
        /// <param name="clrEFT"></param>
        public void GetTransactionsforMFDV(MatterCustomEntities.MatterFinDisbursementView mfdv, int selectedItemId, bool clrEFT)
        {
            if (clrEFT) // Clear EXCEPTIONS
            {
                // Transaction Free and non-Transaction free options should not clear the data
                if (selectedItemId == (int)TransactionTypeEnum.BPay || selectedItemId == (int)TransactionTypeEnum.BPayFree)
                {
                    if (mfdv.TransactionTypeId == (int)TransactionTypeEnum.BPay || mfdv.TransactionTypeId == (int)TransactionTypeEnum.BPayFree)
                    {
                        clrEFT = false;
                    }
                }
                else if (selectedItemId == (int)TransactionTypeEnum.EFT || selectedItemId == (int)TransactionTypeEnum.EFTFree)
                {
                    if (mfdv.TransactionTypeId == (int)TransactionTypeEnum.EFT || mfdv.TransactionTypeId == (int)TransactionTypeEnum.EFTFree)
                    {
                        clrEFT = false;
                    }
                }
            }

            // Clear all previous data
            if (clrEFT)
            {
                mfdv.EFT_AccountName = null;
                mfdv.EFT_AccountNo = null;
                mfdv.EFT_BSB = null;
                mfdv.EFT_Reference = null;

                List<int> chequeTypes = new List<int>() { (int)TransactionTypeEnum.Cheque, (int)TransactionTypeEnum.ChequeFree, (int)TransactionTypeEnum.PPS, (int)TransactionTypeEnum.PPSFree, (int)TransactionTypeEnum.TrustCheque, (int)TransactionTypeEnum.TrustChequeFree };
                if (!chequeTypes.Contains(selectedItemId))
                {
                    mfdv.Instructions = null;
                }
            }

            // UI visbility

            //DEFAULT
 
            mfdv.ShowBPayDetails = false;
            mfdv.ShowEFTDetails = false;
            mfdv.ShowPEXADetails = false;
            mfdv.TransactionCanBeFree = false;
            mfdv.TransactionCanBePPS = false;
            

            switch (selectedItemId)
            {
                case (int)TransactionTypeEnum.EFT:
                    mfdv.ShowEFTDetails = true;
                    mfdv.TransactionCanBeFree = true;
                    mfdv.ParentTransactionTypeId = (int)TransactionTypeEnum.EFT;
                    mfdv.TransactionIsPPS = false;
                    break;
                case (int)TransactionTypeEnum.EFTFree:
                    mfdv.ShowEFTDetails = true;
                    mfdv.TransactionCanBeFree = true;
                    mfdv.ParentTransactionTypeId = (int)TransactionTypeEnum.EFT;
                    mfdv.TransactionIsPPS = false;
                    break;
                case (int)TransactionTypeEnum.TT:
                    mfdv.ShowEFTDetails = true;
                    mfdv.TransactionCanBeFree = true;
                    mfdv.ParentTransactionTypeId = (int)TransactionTypeEnum.TT;
                    mfdv.TransactionIsPPS = false;
                    break;
                case (int)TransactionTypeEnum.TTFree:
                    mfdv.ShowEFTDetails = true;
                    mfdv.TransactionCanBeFree = true;
                    mfdv.ParentTransactionTypeId = (int)TransactionTypeEnum.TT;
                    mfdv.TransactionIsPPS = false;
                    break;
                case (int)TransactionTypeEnum.BPay:
                    mfdv.ShowBPayDetails = true;
                    mfdv.TransactionCanBeFree = true;
                    mfdv.ParentTransactionTypeId = (int)TransactionTypeEnum.BPay;
                    mfdv.TransactionIsPPS = false;
                    break;
                case (int)TransactionTypeEnum.BPayFree:
                    mfdv.ShowBPayDetails = true;
                    mfdv.TransactionCanBeFree = true;
                    mfdv.ParentTransactionTypeId = (int)TransactionTypeEnum.BPay;
                    mfdv.TransactionIsPPS = false;
                    break;
                case (int)TransactionTypeEnum.PEXA:
                    mfdv.ShowPEXADetails = true;
                    mfdv.ParentTransactionTypeId = (int)TransactionTypeEnum.PEXA;
                    mfdv.TransactionIsPPS = false;
                    break;
                case (int)TransactionTypeEnum.Cheque:
                    mfdv.TransactionCanBeFree = true;
                    mfdv.TransactionCanBePPS = true;
                    mfdv.ParentTransactionTypeId = (int)TransactionTypeEnum.Cheque;
                    break;
                case (int)TransactionTypeEnum.ChequeFree:
                    mfdv.TransactionCanBeFree = true;
                    mfdv.TransactionCanBePPS = true;
                    mfdv.ParentTransactionTypeId = (int)TransactionTypeEnum.Cheque;
                    break;
                case (int)TransactionTypeEnum.PPS:
                    mfdv.TransactionCanBeFree = true;
                    mfdv.TransactionCanBePPS = true;
                    mfdv.TransactionIsPPS = true;
                    mfdv.ParentTransactionTypeId = (int)TransactionTypeEnum.Cheque;

                    break;
                case (int)TransactionTypeEnum.PPSFree:
                    mfdv.TransactionCanBeFree = true;
                    mfdv.TransactionCanBePPS = true;
                    mfdv.TransactionIsPPS = true;
                    mfdv.ParentTransactionTypeId = (int)TransactionTypeEnum.Cheque;
                    break;
                case (int)TransactionTypeEnum.TrustCheque:
                    mfdv.TransactionCanBeFree = true;
                    mfdv.TransactionIsPPS = false;
                    mfdv.ParentTransactionTypeId = (int)TransactionTypeEnum.TrustCheque;
                    break;
                case (int)TransactionTypeEnum.TrustChequeFree:
                    mfdv.TransactionCanBeFree = true;
                    mfdv.TransactionIsPPS = false;
                    mfdv.ParentTransactionTypeId = (int)TransactionTypeEnum.TrustCheque;
                    break;
                default:
                    break;
            }

        }


        public void GetTransactionsforMatterFinRetained(MatterCustomEntities.MatterFinRetainedView ret, int selectedItemId, bool clrEFT)
        {
            if (clrEFT) // Clear EXCEPTIONS
            {
                // Transaction Free and non-Transaction free options should not clear the data
                if (selectedItemId == (int)TransactionTypeEnum.BPay || selectedItemId == (int)TransactionTypeEnum.BPayFree)
                {
                    if (ret.TransactionTypeId == (int)TransactionTypeEnum.BPay || ret.TransactionTypeId == (int)TransactionTypeEnum.BPayFree)
                    {
                        clrEFT = false;
                    }
                }
                else if (selectedItemId == (int)TransactionTypeEnum.EFT || selectedItemId == (int)TransactionTypeEnum.EFTFree)
                {
                    if (ret.TransactionTypeId == (int)TransactionTypeEnum.EFT || ret.TransactionTypeId == (int)TransactionTypeEnum.EFTFree)
                    {
                        clrEFT = false;
                    }
                }


            }

            // Clear all previous data
            if (clrEFT)
            {
                ret.EFT_AccountName = null;
                ret.EFT_AccountNo = null;
                ret.EFT_BSB = null;
                ret.EFT_Reference = null;
            }

            // UI visbility

            //DEFAULT

            ret.ShowBPayDetails = false;
            ret.ShowEFTDetails = false;
            ret.ShowPEXADetails = false;
            ret.TransactionCanBeFree = false;
            ret.TransactionCanBePPS = false;


            switch (selectedItemId)
            {
                case (int)TransactionTypeEnum.EFT:
                    ret.ShowEFTDetails = true;
                    ret.TransactionCanBeFree = true;
                    ret.ParentTransactionTypeId = (int)TransactionTypeEnum.EFT;
                    ret.TransactionIsPPS = false;
                    break;
                case (int)TransactionTypeEnum.EFTFree:
                    ret.ShowEFTDetails = true;
                    ret.TransactionCanBeFree = true;
                    ret.ParentTransactionTypeId = (int)TransactionTypeEnum.EFT;
                    ret.TransactionIsPPS = false;
                    break;
                case (int)TransactionTypeEnum.TT:
                    ret.ShowEFTDetails = true;
                    ret.TransactionCanBeFree = true;
                    ret.ParentTransactionTypeId = (int)TransactionTypeEnum.TT;
                    ret.TransactionIsPPS = false;
                    break;
                case (int)TransactionTypeEnum.TTFree:
                    ret.ShowEFTDetails = true;
                    ret.TransactionCanBeFree = true;
                    ret.ParentTransactionTypeId = (int)TransactionTypeEnum.TT;
                    ret.TransactionIsPPS = false;
                    break;
                case (int)TransactionTypeEnum.BPay:
                    ret.ShowBPayDetails = true;
                    ret.TransactionCanBeFree = true;
                    ret.ParentTransactionTypeId = (int)TransactionTypeEnum.BPay;
                    ret.TransactionIsPPS = false;
                    break;
                case (int)TransactionTypeEnum.BPayFree:
                    ret.ShowBPayDetails = true;
                    ret.TransactionCanBeFree = true;
                    ret.ParentTransactionTypeId = (int)TransactionTypeEnum.BPay;
                    ret.TransactionIsPPS = false;
                    break;
                case (int)TransactionTypeEnum.PEXA:
                    ret.ShowPEXADetails = true;
                    ret.ParentTransactionTypeId = (int)TransactionTypeEnum.PEXA;
                    ret.TransactionIsPPS = false;
                    break;
                case (int)TransactionTypeEnum.Cheque:
                    ret.TransactionCanBeFree = true;
                    ret.TransactionCanBePPS = true;
                    ret.ParentTransactionTypeId = (int)TransactionTypeEnum.Cheque;
                    break;
                case (int)TransactionTypeEnum.ChequeFree:
                    ret.TransactionCanBeFree = true;
                    ret.TransactionCanBePPS = true;
                    ret.ParentTransactionTypeId = (int)TransactionTypeEnum.Cheque;
                    break;
                case (int)TransactionTypeEnum.PPS:
                    ret.TransactionCanBeFree = true;
                    ret.TransactionCanBePPS = true;
                    ret.TransactionIsPPS = true;
                    ret.ParentTransactionTypeId = (int)TransactionTypeEnum.Cheque;

                    break;
                case (int)TransactionTypeEnum.PPSFree:
                    ret.TransactionCanBeFree = true;
                    ret.TransactionCanBePPS = true;
                    ret.TransactionIsPPS = true;
                    ret.ParentTransactionTypeId = (int)TransactionTypeEnum.Cheque;
                    break;
                case (int)TransactionTypeEnum.TrustCheque:
                    ret.TransactionCanBeFree = true;
                    ret.TransactionIsPPS = false;
                    ret.ParentTransactionTypeId = (int)TransactionTypeEnum.TrustCheque;
                    break;
                case (int)TransactionTypeEnum.TrustChequeFree:
                    ret.TransactionCanBeFree = true;
                    ret.TransactionIsPPS = false;
                    ret.ParentTransactionTypeId = (int)TransactionTypeEnum.TrustCheque;
                    break;
                default:
                    break;
            }

        }

        #endregion

        #region FinancialGrid Methods
        /// <summary>
        /// Obtains the Loan Amount / Expected Funds for a matter.
        /// </summary>
        /// <remarks>
        /// Previously this would simply add all the amounts of each loan account for the matter - which didn't work for Increases. 
        /// <para>Now precedents send back a SWIFT amount which gets used if it is obtained. Failing this, the function falls back to the old method</para>
        /// </remarks>
        /// <param name="matterId">The ID of the <see cref="Matter"/> to obtain the amount for.</param>
        /// <returns>The decimal amount in dollars of the Matter's Swift amount</returns>
        public decimal GetLoanAmount(int matterId)
        {
            var matterSwiftAmounts = context.MatterSwiftAmounts.Where(x => x.MatterId == matterId);
            if (matterSwiftAmounts.Any())
            {
                return matterSwiftAmounts.Sum(x => x.SwiftAmount);
            }

            var loans = context.MatterLoanAccounts.Where(m => m.MatterId == matterId);
            if (loans.Any())
            {
                return loans.Sum(m => m.LoanAmount);
            }
            return 0;
            //return context.MatterLoanAccounts.Where(m => m.MatterId == matterId).Sum(m => m.LoanAmount);
        }

        /// <summary>
        /// Gets <see cref="MatterCustomEntities.MatterFinRetainedView"/>s of all current amounts retained by lender for a <see cref="Matter"/>.
        /// </summary>
        /// <param name="matterId"><see cref="Matter.MatterId"/> for the Matter to obtain retained amounts for.</param>
        /// <returns><see cref="MatterCustomEntities.MatterFinRetainedView"/>s for each retained item for the matter.</returns>
        public IEnumerable<MatterCustomEntities.MatterFinRetainedView> GetMatterFinRetained(int matterId)
        {
            return context.MatterFinanceRetainedByLenders.Where(x => x.MatterId == matterId)
                .Select(x => new MatterCustomEntities.MatterFinRetainedView
                {
                    MatterFinanceRetainedByLenderId = x.MatterFinanceRetainedByLenderId,
                    MatterId = x.MatterId,
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
                    IsDirty = false
                });
        }

        /// <summary>
        /// Gets all <see cref="MatterCustomEntities.MatterFinFundingView"/>s for <see cref="MatterLedgerItem"/>s payable to MSA's trust account for a <see cref="Matter"/ >
        /// </summary>
        /// <param name="matterId"><see cref="Matter.MatterId"/> for the Matter to obtain funding amounts for.</param>
        /// <param name="showCancelled">Flag to determine whether or not to include Cancelled deposits in this list</param>
        /// <returns><see cref="MatterCustomEntities.MatterFinFundingView"/>s for each funding item for the matter.</returns>
        public IEnumerable<MatterCustomEntities.MatterFinFundingView> GetMatterFinFunding(int matterId, bool showCancelled)
        {
            var qry = context.MatterLedgerItems.Where(x => x.MatterId == matterId && x.PayableToAccountTypeId == (int)AccountTypeEnum.Trust
                                                        && !x.ParentMatterLedgerItemId.HasValue);
            if (!showCancelled)
                qry = qry.Where(x => x.MatterLedgerItemStatusTypeId != (int)MatterLedgerItemStatusTypeEnum.Cancelled);

            return GetMatterFinFunding(qry);
        }

        /// <summary>
        /// Creates <see cref="MatterCustomEntities.MatterFinFundingView"/>s for items payable to MSA's trust account for an <see cref="IQueryable"/> of <see cref="MatterLedgerItem"/>s  / >
        /// </summary>
        /// <param name="qry"></param>
        /// <returns><see cref="MatterCustomEntities.MatterFinFundingView"/>s for each item in the original query.</returns>
        public IEnumerable<MatterCustomEntities.MatterFinFundingView> GetMatterFinFunding(IQueryable<MatterLedgerItem> qry)
        {
            return qry
                .Select(x => new {
                    x.MatterLedgerItemId,
                    x.MatterId,
                    x.Matter.MatterGroupTypeId,
                    x.TransactionTypeId,
                    x.TransactionType.TransactionTypeName,
                    x.Description,
                    x.PayableByTypeId,
                    x.PayableToOthersDesc,
                    x.PayableByOthersDesc,
                    PayableByTypeName = x.PayableType.PayableTypeName,
                    x.Amount,
                    x.GSTFree,
                    x.GST,
                    x.ExpectedPaymentDate,
                    SettlementDate = (DateTime?)x.Matter.SettlementSchedule.SettlementDate,
                    x.Instructions,
                    x.MatterLedgerItemStatusTypeId,
                    x.FundsTransferredTypeId,
                    x.ParentMatterLedgerItemId,
                    x.UpdatedDate,
                    x.UpdatedByUserId,
                    x.FundingRequestId,
                    x.Matter.LenderId,
                    FundingRequestStatusTypeId = x.FundingRequest != null ? (int?)x.FundingRequest.FundingRequestStatusTypeId : (int?)null,
                    IsReadOnly = x.FundingRequest != null && x.FundingRequest.FundingRequestStatusTypeId == (int)FundingRequestStatusTypeEnum.Locked,
                    UpdatedByUsername = x.User.Username
                }).ToList().Select(x => new MatterCustomEntities.MatterFinFundingView(x.MatterLedgerItemId, x.MatterId, x.MatterGroupTypeId ,0, 0, x.MatterLedgerItemStatusTypeId, x.FundsTransferredTypeId, x.TransactionTypeId, x.TransactionTypeName, x.PayableByTypeId,
                                        x.PayableByOthersDesc, x.Description, x.Amount, x.GSTFree, x.GST, x.ExpectedPaymentDate, x.SettlementDate, x.Instructions, x.MatterLedgerItemStatusTypeId,
                                        x.ParentMatterLedgerItemId.HasValue, x.ParentMatterLedgerItemId, x.UpdatedDate, x.UpdatedByUserId, x.UpdatedByUsername, x.FundingRequestId, x.LenderId, readOnly: x.IsReadOnly, fundingRequestStatusTypeId: x.FundingRequestStatusTypeId)).OrderBy(x => x.DisplayOrder).ToList();
        }

        /// <summary>
        /// Gets a <see cref="MatterCustomEntities.MatterFinFundingView"/> for a single <see cref="MatterLedgerItem"/>
        /// </summary>
        /// <param name="matterLedgerItemId">ID for the <see cref="MatterLedgerItem"/></param>
        /// <returns><see cref="MatterCustomEntities.MatterFinFundingView"/> for <see cref="MatterLedgerItem"/></returns>
        public MatterCustomEntities.MatterFinFundingView GetMatterFinFundingView(int matterLedgerItemId)
        {
            var qry = context.MatterLedgerItems.Where(x => x.MatterLedgerItemId == matterLedgerItemId);
            return GetMatterFinFunding(qry).FirstOrDefault();
        }

        /// <summary>
        /// Gets <see cref="MatterCustomEntities.MatterFinDisbursementView"/>s for cancelled funding / shortfall amounts (depending on showCancelledShortfallFlag) that MSA is sending back. <see cref="Matter"/>
        /// </summary>
        /// <param name="matterId"><see cref="Matter.MatterId"/> for the Matterto obtain cancelled finances for</param>
        /// <param name="showCancelledShortfall">- IF FALSE: Obtains cancelled funding amounts other than CANCELLED SHORTFALL
        ///                                      - IF TRUE: Obtains only CANCELLED SHORTFALL amounts</param>
        /// <returns><see cref="MatterCustomEntities.MatterFinDisbursementView"/> for <see cref="MatterLedgerItem"/></returns>
        public IEnumerable<MatterCustomEntities.MatterFinDisbursementView> GetMatterFinCancelled(int matterId, bool showCancelledShortfall)
        {
            var qry = context.MatterLedgerItems.Where(x => x.MatterId == matterId && x.PayableByAccountTypeId == (int)AccountTypeEnum.Trust &&
                                                                   x.ParentMatterLedgerItemId.HasValue);

            if (showCancelledShortfall)
            {
                qry = qry.Where(x => x.Description.ToUpper().Contains("CANCELLED SHORTFALL"));
            }
            else
            {
                qry = qry.Where(x=> !x.Description.ToUpper().Contains("CANCELLED SHORTFALL"));
            }
            return GetMatterFinDisbursements(qry);
        }

        /// <summary>
        /// Gets <see cref="MatterCustomEntities.MatterFinDisbursementView"/>s for a query of <see cref="MatterLedgerItem"/>s for use in financial grids.
        /// </summary>
        /// <param name="qry">Query of <see cref="MatterLedgerItem"/>s</param>
        /// <returns>IEnumerable of <see cref="MatterCustomEntities.MatterFinDisbursementView"/>s</returns>        
        public IEnumerable<MatterCustomEntities.MatterFinDisbursementView> GetMatterFinDisbursements(IQueryable<MatterLedgerItem> qry, bool displayOnly = false)
        {
            return qry
                .Select(x => new
                {
                    Id = x.MatterLedgerItemId,
                    x.MatterLedgerItemStatusTypeId,
                    x.MatterId,
                    x.Matter.Lender.LenderName,
                    x.Matter.MortMgr.MortMgrName,
                    x.Matter.MatterParties,
                    x.TransactionTypeId,
                    x.TransactionType.TransactionTypeDesc,
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
                    x.MatterLoanAccount.LoanAccountNo,
                    x.Amount,
                    x.GST,
                    x.GSTFree,
                    x.Description,
                    x.Instructions,
                    x.PaymentNotes,
                    x.FundsTransferredTypeId,
                    x.ParentMatterLedgerItemId,
                    x.ChequePrepared,
                    FundingRequestStatusTypeId = (int?)x.FundingRequest.FundingRequestStatusTypeId,
                    PPSRequestStatusTypeId = (int?)x.PPSRequest.FundingRequestStatusTypeId,
                    InvoiceSentDate = (DateTime?)x.Invoice.InvoiceSentDate,
                    InvoicePaidDate = (DateTime?)x.Invoice.InvoicePaidDate,
                    DischargeSummaryStatusTypeId = (int?)x.Matter.SettlementSchedule.DischargeSummary.DischargeSummaryStatusTypeId,
                    x.UpdatedDate,
                    x.UpdatedByUserId,
                    x.MatterLoanAccountId,
                    UpdatedByUsername = x.User.Username,
                    IsDirty = false,
                    IsReadOnly = false,
                    x.LinkedToMatterId,
                    x.Matter1.MatterDescription
                }).ToList()
                .Select(x => new MatterCustomEntities.MatterFinDisbursementView(x.Id, x.MatterId, x.LenderName, x.MortMgrName, x.MatterParties, 0, 0, x.TransactionTypeId, x.TransactionTypeDesc,
                x.PayableByAccountTypeId, x.PayableByTypeId, x.PayableByOthersDesc,
                x.PayableToAccountTypeId, x.PayableToTypeId, x.PayableToOthersDesc,
                x.EFT_AccountName, x.EFT_BSB, x.EFT_AccountNo, x.EFT_Reference, x.MatterLoanAccountId, x.LoanAccountNo, x.Amount, x.GST, x.GSTFree,
                x.Description, null, x.Instructions, x.PaymentNotes, x.FundsTransferredTypeId, x.ChequePrepared, x.MatterLedgerItemStatusTypeId, x.ParentMatterLedgerItemId.HasValue, x.ParentMatterLedgerItemId,
                x.FundingRequestStatusTypeId, x.PPSRequestStatusTypeId, x.DischargeSummaryStatusTypeId, (x.InvoiceSentDate.HasValue || x.InvoicePaidDate.HasValue),
                x.UpdatedDate,
                x.UpdatedByUserId, x.UpdatedByUsername, displayOnly, x.LinkedToMatterId, x.MatterDescription)).OrderBy(x => x.DisplayOrder).ToList();
        }

        /// <summary>
        /// Gets <see cref="MatterCustomEntities.MatterFinDisbursementView"/>s for all non invoice, non child (i.e. returns of cancelled) <see cref="MatterLedgerItem"/>s for a <see cref="Matter"/>
        /// </summary>
        /// <param name="matterId"><see cref="Matter.MatterId"/> for the Matter to obtain disbursements for.</param>
        /// <param name="showCancelled">TRUE if result should include cancelled disbursements, FALSE if only non-cancelled.</param>
        /// <returns>IEnumerable of <see cref="MatterCustomEntities.MatterFinDisbursementView"/>s</returns>
        public IEnumerable<MatterCustomEntities.MatterFinDisbursementView> GetMatterFinDisbursements(int matterId, bool showCancelled, bool displayOnly = false)
        {
            var qry = context.MatterLedgerItems.Where(x => x.MatterId == matterId && x.PayableByAccountTypeId == (int)AccountTypeEnum.Trust
                                            && !x.ParentMatterLedgerItemId.HasValue && x.TransactionTypeId != (int)TransactionTypeEnum.Invoice);
            if (!showCancelled)
                qry = qry.Where(x => x.MatterLedgerItemStatusTypeId != (int)MatterLedgerItemStatusTypeEnum.Cancelled);

            return GetMatterFinDisbursements(qry, displayOnly);
        }

        /// <summary>
        /// Gets <see cref="MatterCustomEntities.MatterFinDisbursementView"/>s for all invoiceable items for <see cref="Matter"/>. 
        /// </summary>
        /// <remarks>
        /// For New Loans - invoiceable items are items with transaction type = Invoice
        /// For Discharges - this is all ledger items (as there is no financial grid for discharges)
        /// </remarks>
        /// <param name="matterId"><see cref="Matter.MatterId"/> for the Matter to obtain fees for.</param>
        /// <param name="showCancelled">TRUE if result should include cancelled fees, FALSE if only non-cancelled.</param>
        /// <returns>IEnumerable of <see cref="MatterCustomEntities.MatterFinDisbursementView"/>s</returns>
        public IEnumerable<MatterCustomEntities.MatterFinDisbursementView> GetMatterFinDisbursementFees(int matterId, bool showCancelled)
        {
            var qry = context.MatterLedgerItems.Where(x => x.MatterId == matterId && !x.ParentMatterLedgerItemId.HasValue &&
                                                            ((x.Matter.MatterGroupTypeId == (int)MatterGroupTypeEnum.NewLoan &&
                                                                x.TransactionTypeId == (int)TransactionTypeEnum.Invoice) ||
                                                                  (x.Matter.MatterGroupTypeId == (int)MatterGroupTypeEnum.Consent) ||
                                                             (x.Matter.MatterGroupTypeId == (int)MatterGroupTypeEnum.Discharge)));
            if (!showCancelled)
                qry = qry.Where(x => x.MatterLedgerItemStatusTypeId != (int)MatterLedgerItemStatusTypeEnum.Cancelled);

            return GetMatterFinDisbursements(qry);
        }
        /// <summary>
        /// Gets <see cref="MatterCustomEntities.MatterFinDisbursementView"/>s for all unpaid Cheque <see cref="MatterLedgerItem"/>s for a <see cref="Matter"/>
        /// to display in the Cancel Delay Settlement screen. 
        /// </summary>
        /// <param name="matterId"><see cref="Matter.MatterId"/> for the Matter to obtain fees for.</param>
        /// <returns>IEnumerable of <see cref="MatterCustomEntities.MatterFinDisbursementView"/>s</returns>
        public IEnumerable<MatterCustomEntities.MatterFinDisbursementView> GetMatterFinDisbursementsForCancelDelay(int matterId)
        {
            var TTList = new int[]
            {
                (int)TransactionTypeEnum.Cheque,
                (int)TransactionTypeEnum.ChequeFree,
                (int)TransactionTypeEnum.PPS, //George didn't want these to appear in cancel settlement //yes he did 
                (int)TransactionTypeEnum.PPSFree, //George didn't want these to appear in cancel settlement //yes he did 
                //(int)TransactionTypeEnum.TrustCheque /
            };

            var qry = context.MatterLedgerItems.Where(x => x.MatterId == matterId && x.PayableByAccountTypeId == (int)AccountTypeEnum.Trust &&
                                            !x.ParentMatterLedgerItemId.HasValue && TTList.Contains(x.TransactionTypeId) &&
                                            (x.MatterLedgerItemStatusTypeId == (int)MatterLedgerItemStatusTypeEnum.Ready ||
                                             x.MatterLedgerItemStatusTypeId == (int)MatterLedgerItemStatusTypeEnum.Requested));

            return GetMatterFinDisbursements(qry);
        }


        /// <summary>
        /// Gets a <see cref="MatterCustomEntities.MatterFinDisbursementView"/> for a single <see cref="MatterLedgerItem"/>
        /// </summary>
        /// <param name="matterLedgerItemId">The ID of the <see cref="MatterLedgerItem"/> to get a <see cref="MatterCustomEntities.MatterFinDisbursementView"/> for</param>
        /// <returns>IEnumerable of <see cref="MatterCustomEntities.MatterFinDisbursementView"/>s</returns>
        public MatterCustomEntities.MatterFinDisbursementView GetMatterFinDisbursementView(int matterLedgerItemId)
        {
            var qry = context.MatterLedgerItems.Where(x => x.MatterLedgerItemId == matterLedgerItemId);

            return GetMatterFinDisbursements(qry).FirstOrDefault();

        }

        /// <summary>
        /// Gets <see cref="MatterCustomEntities.MatterFinDisbursementView"/>s of all cancelled, non invoiceable items for a <see cref="Matter"/>
        /// </summary>
        /// <param name="matterId">The ID of the <see cref="Matter"/> to obtain views for</param>
        /// <returns>IEnumerable of <see cref="MatterCustomEntities.MatterFinDisbursementView"/>s</returns>
        public IEnumerable<MatterCustomEntities.MatterFinDisbursementView> GetMatterFinDisbursementsCancelled(int matterId)
        {
            var qry = context.MatterLedgerItems.Where(x => x.MatterId == matterId && x.PayableToAccountTypeId == (int)AccountTypeEnum.Trust
                                            && x.ParentMatterLedgerItemId.HasValue && x.TransactionTypeId != (int)TransactionTypeEnum.Invoice);

            return GetMatterFinDisbursements(qry);

        }

        /// <summary>
        /// Gets <see cref="MatterCustomEntities.MatterFinDisbursementView"/>s of all Cancelled Cheques for a <see cref="Matter"/>
        /// </summary>
        /// <param name="matterId"></param>
        /// <param name="showAllCancelled"></param>
        /// <returns>IEnumerable of <see cref="MatterCustomEntities.MatterFinDisbursementView"/>s</returns>
        public IEnumerable<MatterCustomEntities.MatterFinDisbursementView> GetCancelledCheques(int matterId, bool showAllCancelled)
        {
            var TTList = new int[]
            {
                (int)TransactionTypeEnum.Cheque,
                (int)TransactionTypeEnum.ChequeFree,
                (int)TransactionTypeEnum.TrustCheque
            };

            var qry = context.MatterLedgerItems.Where(x => x.MatterId == matterId && x.PayableByAccountTypeId == (int)AccountTypeEnum.Trust
                                            && x.MatterLedgerItemStatusTypeId == (int)MatterLedgerItemStatusTypeEnum.Cancelled
                                            && TTList.Contains(x.TransactionTypeId));
            if (!showAllCancelled)
                qry = qry.Where(x => !x.ChequePrepared);

            var retVal = GetMatterFinDisbursements(qry).ToList();
            retVal.ForEach(x => x.IsChecked = !x.ChequePrepared);

            return retVal;
        }

        /// <summary>
        /// Gets <see cref="MatterCustomEntities.MatterFinDisbursementView"/>s for all cheque type transactions for a <see cref="Matter"/>.
        /// </summary>
        /// <param name="matterId">The <see cref="Matter.MatterId"/> for the Matter to obtain cheques for</param>
        /// <param name="showAll">Whether or not to include cancelled cheques in the result</param>
        /// <returns>IEnumerable of <see cref="MatterCustomEntities.MatterFinDisbursementView"/></returns>
        public IEnumerable<MatterCustomEntities.MatterFinDisbursementView> GetManualCheques(int matterId, bool showAll)
        {
            var TTList = new int[]
            {
                (int)TransactionTypeEnum.Cheque,
                (int)TransactionTypeEnum.ChequeFree,
                (int)TransactionTypeEnum.TrustCheque
            };

            var qry = context.MatterLedgerItems.Where(x => x.MatterId == matterId && x.PayableByAccountTypeId == (int)AccountTypeEnum.Trust
                                            && x.MatterLedgerItemStatusTypeId != (int)MatterLedgerItemStatusTypeEnum.Cancelled
                                            && TTList.Contains(x.TransactionTypeId));

            if (!showAll)
                qry = qry.Where(x => !x.ChequePrepared);

            var retVal = GetMatterFinDisbursements(qry).ToList();
            retVal.ForEach(x => x.IsChecked = !x.ChequePrepared);

            return retVal;

        }

        /// <summary>
        /// Required to make any changes / create the initial <see cref="MatterLedgerItem"/> for the "Expected Funds in Trust" item for new loan matters.
        /// </summary>
        /// <param name="matterId">The ID of the <see cref="Matter"/> to refresh / create the Expected Funds in Trust item for.</param>
        public void UpdateExpectedFundsFromLender(int matterId)
        {
            var loanAmount = GetLoanAmount(matterId);
            var retainedList = GetMatterFinRetained(matterId);
            var fundingList = GetMatterFinFunding(matterId, false);

            decimal expectedFundsFromLender = loanAmount - retainedList.Sum(x => x.Amount);

            var fList = fundingList.Where(x => x.Description == "Expected Funds from Lender" &&
                                                                x.PayableByTypeId == (int)PayableTypeEnum.Lender).ToList();
            if (context.Matters.FirstOrDefault(m=>m.MatterId == matterId).MatterGroupTypeId == (int)MatterGroupTypeEnum.NewLoan)
            {
                if (expectedFundsFromLender <= 0) expectedFundsFromLender = 0.0001M;
            }
            //Only need to fix if it's different
            if (expectedFundsFromLender != fList.Sum(x => x.Amount))
            {
                fList.ForEach(x => x.IsChecked = false);

                //Eliminate the Funding Amounts we cannot change
                foreach (var fRec in fList.Where(x => x.Description == "Expected Funds from Lender" &&
                                                      x.PayableByTypeId == (int)PayableTypeEnum.Lender &&
                                                      x.FundsTransferredTypeId != (int)FundsTransferredTypeEnum.UnPaid))
                {
                    expectedFundsFromLender = expectedFundsFromLender - fRec.Amount;
                    fRec.IsChecked = true;
                }

      

                if (expectedFundsFromLender > 0)
                {
                    MatterLedgerItem mlRec;
                    //Find one we can modify
                    var fRec = fList.FirstOrDefault(x => x.MatterLedgerItemStatusTypeId == (int)MatterLedgerItemStatusTypeEnum.Ready &&
                                        x.FundsTransferredTypeId != (int)FundsTransferredTypeEnum.UnPaid);
                    if (fRec == null)
                    {
                        //Nope... create a new one
                        mlRec = new MatterLedgerItem()
                        {
                            MatterId = matterId,
                            TransactionTypeId = (int)TransactionTypeEnum.EFT,
                            LedgerItemSourceTypeId = (int)LedgerItemSourceTypeEnum.SLICK,
                            PayableByAccountTypeId = (int)AccountTypeEnum.External,
                            PayableByTypeId = (int)Slick_Domain.Enums.PayableTypeEnum.Lender,
                            PayableToAccountTypeId = (int)AccountTypeEnum.Trust,
                            PayableToTypeId = (int)Slick_Domain.Enums.PayableTypeEnum.MSA,
                            Amount = expectedFundsFromLender,
                            GSTFree = true,
                            Description = expectedFundsFromLender > 0.0001M ? "Expected Funds from Lender" : "No Funds Required",
                            MatterLedgerItemStatusTypeId = (int)MatterLedgerItemStatusTypeEnum.Ready,
                            FundsTransferredTypeId = (int)FundsTransferredTypeEnum.UnPaid,
                            ChequePrepared = false
                        };
                    }
                    else
                    {
                        mlRec = context.MatterLedgerItems.FirstOrDefault(m => m.MatterLedgerItemId == fRec.MatterLedgerItemId);
                        fRec.IsChecked = true;
                    }


                    mlRec.Amount = expectedFundsFromLender;
                    mlRec.UpdatedDate = DateTime.Now;
                    mlRec.UpdatedByUserId = Slick_Domain.GlobalVars.CurrentUser.UserId;

                    if (mlRec.MatterLedgerItemId == 0)
                        context.MatterLedgerItems.Add(mlRec);

                }

                //Cleanup - Remove any items which are not "Checked" and aren't a lender subsidy
                foreach (var fRec in fList.Where(x => !x.IsChecked /*&& !x.Description.Contains(Slick_Domain.Common.DomainConstants.SubsidyDescription)*/))
                {
                    var mRec = context.MatterLedgerItems.FirstOrDefault(m => m.MatterLedgerItemId == fRec.MatterLedgerItemId);
                    var mlittis = context.MatterLedgerItemTrustTransactionItems.Where(t => t.MatterLedgerItemId == mRec.MatterLedgerItemId);
                    context.MatterLedgerItemTrustTransactionItems.RemoveRange(mlittis);
                    context.MatterLedgerItems.Remove(mRec);
                }

                ////reset expected payment date for unchecked lender subsidy
                //foreach(var fRec in fList.Where(x=>!x.IsChecked && x.Description.Contains(Slick_Domain.Common.DomainConstants.SubsidyDescription)))
                //{
                //    var mli = context.MatterLedgerItems.FirstOrDefault(m => m.MatterLedgerItemId == fRec.MatterLedgerItemId);
                //    mli.ExpectedPaymentDate = null;
                //}

                context.SaveChanges();
            }
        }

        /// <summary>
        /// If cancelling settlement, rebuild any lender subsidy that got returned in the financial grid as an unpaid amount to 
        /// receive again for the next settlement.
        /// </summary>
        /// <param name="disbView">The <see cref="MatterCustomEntities.MatterFinFundingView"/> of the original Subsidy amount.</param>
        public void RebuildLenderSubsidy(MatterCustomEntities.MatterFinFundingView disbView)
        {
            MatterLedgerItem newMli = new MatterLedgerItem();
            newMli.MatterId = disbView.MatterId;
            newMli.TransactionTypeId = disbView.TransactionTypeId;
            newMli.LedgerItemSourceTypeId = (int)Enums.LedgerItemSourceTypeEnum.SLICK;
            newMli.MatterLedgerItemStatusTypeId = (int)Enums.MatterLedgerItemStatusTypeEnum.Ready;
            newMli.FundsTransferredTypeId = (int)Enums.FundsTransferredTypeEnum.UnPaid; 
            newMli.Description = disbView.Description;
            newMli.PayableByAccountTypeId = (int)Enums.AccountTypeEnum.External;
            newMli.PayableByTypeId = disbView.PayableByTypeId;
            newMli.PayableByOthersDesc = disbView.PayableByOthersDesc;
            newMli.PayableToAccountTypeId = (int)Enums.AccountTypeEnum.Trust;
            newMli.PayableToTypeId = (int)Enums.PayableTypeEnum.MSA;
            newMli.PayableToOthersDesc = null;
            newMli.UpdatedByUserId = GlobalVars.CurrentUser.UserId;
            newMli.UpdatedDate = DateTime.Now;
            newMli.Amount = disbView.Amount;
            newMli.GSTFree = disbView.GSTFree;
            newMli.GST = disbView.GST;
            newMli.ExpectedPaymentDate = null;

            context.MatterLedgerItems.Add(newMli);
            context.SaveChanges();
        }

        #endregion

        //Trust Transaction Methods
        #region TrustTransaction methods
        
        /// <summary>
        /// Override to UpdateExpectedTrustTransactions to force a SaveChanges() at the end - 
        /// used when updating expected trust transactions doesn't need to be done in a transaction with other methods and can be immediately committed. 
        /// </summary>
        /// <param name="matterId"></param>
        /// <returns>Number of trust transaction items modified / created / removed.</returns>
        public int UpdateExpectedTrustTransactions(int matterId)
        {
            return UpdateExpectedTrustTransactions(matterId, true);
        }

        /// <summary>
        /// Update all <see cref="TrustTransactionItem"/>s for a <see cref="Matter"/> for both Deposits and Payments by calling
        /// <see cref="UpdateExpectedTrustTransactionsForDirection(int, IEnumerable{AccountsCustomEntities.TB_TrustTransactionItem}, int, bool)"/>.
        /// <para>Expected trust transaction items are obtained from a matter's existing TTIs and <see cref="MatterLedgerItem"/>s.</para> 
        /// If changes are made, updates the matter's overall reconciled status based on the status of all items.
        /// </summary>
        /// <param name="matterId">The ID of the <see cref="Matter"/> to obtain the trust transactions for</param>
        /// <param name="saveContext">Only saves if true - otherwise can be used to check IF any changes will be required.</param>
        /// <returns>The number of items that will be / were changed.</returns>
        public int UpdateExpectedTrustTransactions(int matterId, bool saveContext)
        {
            int retval = 0;
            var expTTIs = GetExpectedTrustTransactionItems(matterId);

            retval += UpdateExpectedTrustTransactionsForDirection(matterId, expTTIs, (int)Enums.TransactionDirectionTypeEnum.Deposit, saveContext);
            retval += UpdateExpectedTrustTransactionsForDirection(matterId, expTTIs, (int)Enums.TransactionDirectionTypeEnum.Payment, saveContext);

            if (retval > 0)
            {
                UpdateMatterReconciledStatus(matterId, saveContext);
            }

            if (saveContext)
            {
                context.SaveChanges();
            }

            return retval;
        }

        /// <summary>
        /// Obsolete - replaced with <see cref="UpdateExpectedTrustTransactionsForDirection(int, IEnumerable{AccountsCustomEntities.TB_TrustTransactionItem}, int, bool)"/>
        /// with (int)Enums.TransactionDirectionTypeEnum.Deposit as the direction parameter. 
        /// </summary>
        /// <param name="matterId">The <see cref="Matter.MatterId"/> for the Matter </param>
        /// <param name="saveContext"></param>
        /// <returns>Number of trust transactions changed.</returns>
        public int UpdateExpectedTrustDeposits(int matterId, bool saveContext)
        {
            int retval = 0;
            var expTTIs = GetExpectedTrustTransactionItems(matterId);
            retval += UpdateExpectedTrustTransactionsForDirection(matterId, expTTIs, (int)Enums.TransactionDirectionTypeEnum.Deposit, false);
            if (retval > 0)
                UpdateMatterReconciledStatus(matterId, saveContext);
            if (saveContext)
                context.SaveChanges();
            return retval;
        }
        /// <summary>
        /// Obsolete - replaced with <see cref="UpdateExpectedTrustTransactionsForDirection(int, IEnumerable{AccountsCustomEntities.TB_TrustTransactionItem}, int, bool)"/>
        /// with (int)Enums.TransactionDirectionTypeEnum.Payment as the direction parameter. 
        /// </summary>
        /// <param name="matterId">The <see cref="Matter.MatterId"/> for the Matter </param>
        /// <param name="saveContext"></param>
        /// <returns>Number of trust transactions changed.</returns>
        public int UpdateExpectedTrustPayments(int matterId, bool saveContext)
        {
            int retval = 0;
            var expTTIs = GetExpectedTrustTransactionItems(matterId);
            retval += UpdateExpectedTrustTransactionsForDirection(matterId, expTTIs, (int)Enums.TransactionDirectionTypeEnum.Payment, false);
            if (retval > 0)
                UpdateMatterReconciledStatus(matterId, saveContext);
            if (saveContext)
                context.SaveChanges();
            return retval;
        }

        /// <summary>
        /// The primary function used to generate and modify a matter's Trust Transaction Items for a specified payment direction. 
        /// </summary>
        /// <remarks>
        /// This method is hugely complex. 
        /// It works completely differently (and should potentially be 4 different methods for readability) for each combination of New Loan / Discharge + Deposit / Payment as the rules and logic used behind grouping
        /// and checking these with some basic validation. 
        /// </remarks>
        /// <param name="matterId">The <see cref="Matter.MatterId"/> for the Matter to uppdate</param>
        /// <param name="expTTIs"><see cref="AccountsCustomEntities.TB_TrustTransactionItem"/> views for the all expected trust items for the matter as generated by <see cref="AccountsRepository.GetExpectedTrustTransactionItems(int, int)"/> etc.</param>
        /// <param name="transactionDirectionTypeId"><see cref="Enums.TransactionDirectionTypeEnum"/> direction to update</param>
        /// <param name="saveContext">Whether or not to save the database changes at the end of this method - some uses of this will only save after other methods have completed to ensure transactional integrity.</param>
        /// <returns>Number of trust transaction items changed.</returns>
        private int UpdateExpectedTrustTransactionsForDirection(int matterId, IEnumerable<AccountsCustomEntities.TB_TrustTransactionItem> expTTIs, int transactionDirectionTypeId, bool saveContext)
        {
            int trustItemsChanged = 0;
            List<AccountsCustomEntities.TB_ExpectedTrustTransactionItemsGrouped> expTTISummary;
            List<AccountsCustomEntities.TB_TrustTransactionItem> curTTIs;

            var mv = context.Matters.Where(m => m.MatterId == matterId)
                                    .Select(m => new {m.MatterId, m.MatterGroupTypeId, SettlementDate = (DateTime?)m.SettlementSchedule.SettlementDate })
                                    .FirstOrDefault();

            //Certain transaction types can have their matter ledger items grouped when they are being paid to the same parties - i.e. multiple MLI => 1 TTI 
            var eftList = new List<int>()
            {
                (int)TransactionTypeEnum.BPay,
                (int)TransactionTypeEnum.BPayFree,
                (int)TransactionTypeEnum.EFT,
                (int)TransactionTypeEnum.EFTFree,
                (int)TransactionTypeEnum.TT,
                (int)TransactionTypeEnum.TTFree
            };

            if (transactionDirectionTypeId == (int)TransactionDirectionTypeEnum.Deposit)
            {
                if (mv.MatterGroupTypeId == (int)MatterGroupTypeEnum.Discharge)
                {

                    expTTISummary = expTTIs.Where(m => m.TransactionDirectionTypeId == (int)Enums.TransactionDirectionTypeEnum.Deposit &&
                                                        eftList.Contains(m.TransactionTypeId))
                                .GroupBy(g => new { g.TransactionTypeId, g.PayableTypeId, g.PayerPayee, g.ExpectedPaymentDate })
                                .Select(m => new AccountsCustomEntities.TB_ExpectedTrustTransactionItemsGrouped(
                                                    m.Key.TransactionTypeId, m.Key.PayableTypeId, m.Key.PayerPayee,
                                                    null, null, null, null,
                                                    m.Key.ExpectedPaymentDate, m.Sum(x => x.Amount),
                                                    m.Select(x => x.Description),
                                                    m.Select(x => x.MatterLedgerItemIds),
                                                    m.Max(x => x.FundsTransferredTypeId)
                                                    ))
                                .Union(
                                    expTTIs.Where(m => m.TransactionDirectionTypeId == (int)Enums.TransactionDirectionTypeEnum.Deposit &&
                                           !eftList.Contains(m.TransactionTypeId))
                                .Select(m => new AccountsCustomEntities.TB_ExpectedTrustTransactionItemsGrouped()
                                {
                                    TransactionTypeId = m.TransactionTypeId,
                                    PayableTypeId = m.PayableTypeId,
                                    PayerPayee = m.PayerPayee,
                                    ExpectedPaymentDate = m.ExpectedPaymentDate,
                                    Amount = m.Amount,
                                    Description = m.Description,
                                    MatterLedgerItemIds = m.MatterLedgerItemIds,
                                    ProcessOrder = m.FundsTransferredTypeId,
                                    TTI_Matched = false
                                }
                                ))
                                .ToList();
                }
               
                else //New Loan - we don't want to do the same grouping for deposits as discharges does - but we do want to combine the lender subsidy amount + the expected funds amount 
                     //to just show as the one trust transaction item, as it is only relevant to the matter financial grid / documents but does get received as one amount. 
                {
                    expTTISummary = expTTIs.Where(m => m.TransactionDirectionTypeId == (int)Enums.TransactionDirectionTypeEnum.Deposit)
                                    .Select(m => new AccountsCustomEntities.TB_ExpectedTrustTransactionItemsGrouped()
                                    {
                                        TransactionTypeId = m.TransactionTypeId,
                                        PayableTypeId = m.PayableTypeId,
                                        PayerPayee = m.PayerPayee,
                                        ExpectedPaymentDate = m.ExpectedPaymentDate,
                                        Amount = m.Amount,
                                        Description = m.Description,
                                        Notes = m.Notes,
                                        MatterLedgerItemIds = m.MatterLedgerItemIds,
                                        ProcessOrder = m.FundsTransferredTypeId,
                                        TTI_Matched = false
                                    }
                                    ).ToList();
                    //group lender subsidy in with their corresponding expected funds... god this is horrid
                    var expTTISummaryClone = expTTISummary.ToList();
                    foreach (var expTTI in expTTISummaryClone)
                    {
                        if (expTTI.Description.Contains(Slick_Domain.Common.DomainConstants.SubsidyDescription))
                        {
                            //find the corresponding expected funds to latch onto
                            var expFunds = expTTISummary.Where(t => t.Description.ToUpper().Contains("EXPECTED FUNDS") && t.ExpectedPaymentDate == expTTI.ExpectedPaymentDate && t.Notes == expTTI.Notes && t.ProcessOrder == expTTI.ProcessOrder).FirstOrDefault();

                            if (expFunds == null && mv.SettlementDate.HasValue)
                            {
                                expFunds = expTTISummary.Where(t => t.Description.ToUpper().Contains("EXPECTED FUNDS") && t.ExpectedPaymentDate == null && t.Notes == expTTI.Notes && t.ProcessOrder == expTTI.ProcessOrder).FirstOrDefault();
                                //retained may have added payment date so we'll have to be looser with our match. the current expected funds will have an expected payment date of settlement date 
                                //and cancelled MLI have a specific note for both subisdy and exp funds item which is why I do the note equivalence check. ugly, i know.
                            }

                            if (expFunds != null) //if we still haven't found the expected funds then something freaky is going on and we just have to deal with it.
                            {
                                expFunds.Amount += expTTI.Amount;
                                expFunds.MatterLedgerItemIds = expFunds.MatterLedgerItemIds.Concat(expTTI.MatterLedgerItemIds).ToList();
                                expTTISummary.Remove(expTTI);
                            }
                        }
                    }
                }
            }
            else
            {




                var eftItems = expTTIs.Where(m => m.TransactionDirectionTypeId == (int)Enums.TransactionDirectionTypeEnum.Payment &&
                                                     m.PayableTypeId == (int)PayableTypeEnum.MSA &&
                                                     eftList.Contains(m.TransactionTypeId))
                            .GroupBy(g => new { g.TransactionTypeId, g.PayableTypeId, g.EFT_AccountName, g.EFT_BSB, g.EFT_AccountNo, g.EFT_Reference, g.PayerPayee, g.ExpectedPaymentDate, g.FundsTransferredTypeId })
                            .Select(m => new AccountsCustomEntities.TB_ExpectedTrustTransactionItemsGrouped(
                                                m.Key.TransactionTypeId, m.Key.PayableTypeId, m.Key.PayerPayee,
                                                m.Key.EFT_AccountName, m.Key.EFT_BSB, m.Key.EFT_AccountNo, m.Key.EFT_Reference,
                                                m.Key.ExpectedPaymentDate, m.Sum(x => x.Amount),
                                                m.Select(x => x.Description),
                                                m.Select(x => x.MatterLedgerItemIds),
                                                m.Max(x => x.FundsTransferredTypeId)
                                                ));

                var groupedPexaItems = expTTIs.Where(m => m.TransactionDirectionTypeId == (int)Enums.TransactionDirectionTypeEnum.Payment &&
                                           (m.TransactionTypeId == (int)TransactionTypeEnum.PEXA || m.TransactionTypeId == (int)TransactionTypeEnum.PEXAdebit))
                                .GroupBy(g => new { g.TransactionDirectionTypeId })
                                .Select(m => new AccountsCustomEntities.TB_ExpectedTrustTransactionItemsGrouped(m.ToList()));

                var otherItems = expTTIs.Where(m => m.TransactionDirectionTypeId == (int)Enums.TransactionDirectionTypeEnum.Payment &&
                                      !((eftList.Contains(m.TransactionTypeId) && m.PayableTypeId == (int)PayableTypeEnum.MSA) ||
                                         (m.TransactionTypeId == (int)TransactionTypeEnum.PEXA || m.TransactionTypeId == (int)TransactionTypeEnum.PEXAdebit)))
                                .Select(m => new AccountsCustomEntities.TB_ExpectedTrustTransactionItemsGrouped()
                                {
                                    TransactionTypeId = m.TransactionTypeId,
                                    EFT_AccountName = m.EFT_AccountName,
                                    EFT_BSB = m.EFT_BSB,
                                    EFT_AccountNo = m.EFT_AccountNo,
                                    EFT_Reference = m.EFT_Reference,
                                    PayableTypeId = m.PayableTypeId,
                                    PayerPayee = m.PayerPayee,
                                    ExpectedPaymentDate = m.ExpectedPaymentDate,
                                    Amount = m.Amount,
                                    Description = m.Description,
                                    Notes = m.Notes,
                                    MatterLedgerItemIds = m.MatterLedgerItemIds,
                                    ProcessOrder = m.FundsTransferredTypeId,
                                    TTI_Matched = false
                                }
                                );
                //payments we're going to group as much as possible since this is how we will be sending them out from the MSA account. 
                expTTISummary = 
                            eftItems.Union(groupedPexaItems)
                            .Union(otherItems)
                            .ToList();






                //again - lump any payments of returned lender subsidies with corresponding returned expected funds 
                var expTTISummaryClone = expTTISummary.ToList();
                foreach (var expTTI in expTTISummaryClone)
                {
                    if(expTTI.Amount == 85.0M)
                    {
                        bool breakpoint = true;
                    }
                    if (expTTI.Description.Contains(Slick_Domain.Common.DomainConstants.SubsidyDescription) && expTTI.Notes != null && expTTI.Notes.ToUpper().Contains("CANCEL"))
                    {
                        //find the corresponding expected funds to latch onto
                        var expFunds = expTTISummary.Where(t => t.Description.ToUpper().Contains("EXPECTED FUNDS") && t.ExpectedPaymentDate == expTTI.ExpectedPaymentDate && t.Notes != null && t.Notes.ToUpper().Contains("CANCEL")).FirstOrDefault();
                        if (expFunds != null)
                        {
                            expFunds.Amount += expTTI.Amount;
                            expFunds.MatterLedgerItemIds = expFunds.MatterLedgerItemIds.Concat(expTTI.MatterLedgerItemIds).ToList();
                            expTTISummary.Remove(expTTI);
                        }
                        
                    }
                }
            }

            //Get the existing trust transactions, if any
            curTTIs = context.TrustTransactionItems
                        .Where(t => t.MatterId == matterId && t.TransactionDirectionTypeId == transactionDirectionTypeId &&
                                    t.TrustTransactionStatusTypeId != (int)TrustTransactionStatusTypeEnum.Reversed &&
                                    (t.TransactionDirectionTypeId == (int)TransactionDirectionTypeEnum.Deposit ||
                                     (t.TransactionDirectionTypeId == (int)TransactionDirectionTypeEnum.Payment &&
                                      (t.TrustTransactionJournal.JournalBatchId.HasValue || t.TrustTransactionStatusTypeId != (int)TrustTransactionStatusTypeEnum.Journalled))) &&
                                    (!t.TrustSummaryId.HasValue ||
                                      (t.TrustSummaryId.HasValue && t.TrustSummary.TrustSummaryStatusTypeId != (int)TrustSummaryStatusTypeEnum.Reversed)))
                        .Select(t => new { t.TrustTransactionItemId, t.TransactionTypeId, t.PayableTypeId, t.PayerPayee,
                                           t.EFT_AccountName, t.EFT_BSB, t.EFT_AccountNo, t.EFT_Reference, t.ExpectedPaymentDate,
                                           t.Amount, t.Description, t.TrustTransactionStatusTypeId,
                                           TrustSummaryStatusTypeId = (int?)t.TrustSummary.TrustSummaryStatusTypeId,
                                           Notes = t.Notes,
                                           mlis = t.MatterLedgerItemTrustTransactionItems.Select(x => x.MatterLedgerItemId),
                                           }).ToList()
                        .Select(t => new AccountsCustomEntities.TB_TrustTransactionItem(t.TrustTransactionItemId, t.TransactionTypeId, t.PayableTypeId,
                                t.PayerPayee, t.EFT_AccountName, t.EFT_BSB, t.EFT_AccountNo, t.EFT_Reference,
                                t.ExpectedPaymentDate, t.Amount, t.Description, t.TrustTransactionStatusTypeId,
                                t.TrustSummaryStatusTypeId, 0,  t.mlis, t.Notes)).ToList();


            //Clear out the exact matches first (i.e. remove from the queue of "new" ttis to create any that are matches of existing ttis so we don't duplicate.)
            foreach (var expTTI in expTTISummary.OrderByDescending(x => x.ProcessOrder))
            {
                AccountsCustomEntities.TB_TrustTransactionItem srchTTI = null;

                if (transactionDirectionTypeId == (int)TransactionDirectionTypeEnum.Deposit)
                {

                    srchTTI = curTTIs.Where(t => !t.TTI_Matched && t.TransactionTypeId == expTTI.TransactionTypeId &&
                                                    t.PayerPayee == expTTI.PayerPayee &&
                                                    t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Journalled &&
                                                    t.Amount == expTTI.Amount &&
                                                    t.Notes == expTTI.Notes)
                                     .OrderBy(x => x.TrustTransactionStatusTypeId)
                                     .FirstOrDefault();

                    if (srchTTI == null)
                    {
                        // Look for a single TTI by TT, PP, Date, Amt
                        srchTTI = curTTIs.FirstOrDefault(t => !t.TTI_Matched &&
                                                    t.TransactionTypeId == expTTI.TransactionTypeId &&
                                                    t.PayerPayee == expTTI.PayerPayee &&
                                                    t.ExpectedPaymentDate == expTTI.ExpectedPaymentDate &&
                                                    
                                                    t.Amount == expTTI.Amount &&
                                                    t.Notes == expTTI.Notes);
                        if (srchTTI == null)
                        {
                            // Matching a single TTI by TT, PP, Amt
                            srchTTI = curTTIs.FirstOrDefault(t => !t.TTI_Matched && 
                                                    t.TransactionTypeId == expTTI.TransactionTypeId && 
                                                    !t.ExpectedPaymentDate.HasValue &&
                                                    t.Notes == expTTI.Notes && 
                                                    t.PayerPayee == expTTI.PayerPayee &&
                                         
                                                    t.Amount == expTTI.Amount);
                            if (srchTTI == null)
                            {
                                // Look for match on TT and Amount, that has already been paid
                                srchTTI = curTTIs.FirstOrDefault(t => !t.TTI_Matched && t.TransactionTypeId == expTTI.TransactionTypeId &&
                                                                t.PayableTypeId == expTTI.PayableTypeId &&
                                                                t.Notes == expTTI.Notes &&
                                                           
                                                                t.Amount == expTTI.Amount);
                                if (srchTTI == null)
                                {

                                    srchTTI = curTTIs.FirstOrDefault(t => !t.TTI_Matched && t.TransactionTypeId == expTTI.TransactionTypeId &&
                                                                t.Notes == expTTI.Notes &&
                                                                t.Amount == expTTI.Amount);
                                    // stop this from being incorrectly lumped in with non cancelled amounts
                                    if(srchTTI == null && expTTI.Notes == "Received Funds of Cancelled Settlement")
                                    {
                                        srchTTI = curTTIs.FirstOrDefault(t => !t.TTI_Matched && t.TransactionTypeId == expTTI.TransactionTypeId &&
                                                                t.Amount == expTTI.Amount && t.PayerPayee == expTTI.PayerPayee && t.ExpectedPaymentDate == expTTI.ExpectedPaymentDate); 
                                    }
                                }
                            }
                        }
                    }
                }
                else             //Payments
                {
                    
               
                    // Look for a single TTI by TT, EFT, PP, Date and Amt
                    srchTTI = curTTIs.FirstOrDefault(t => !t.TTI_Matched && t.TransactionTypeId == expTTI.TransactionTypeId && t.PayerPayee == expTTI.PayerPayee &&
                                                    t.EFT_AccountName == expTTI.EFT_AccountName && t.EFT_BSB == expTTI.EFT_BSB && 
                                                    t.EFT_AccountNo == expTTI.EFT_AccountNo && t.EFT_Reference == expTTI.EFT_Reference &&
                                                    t.ExpectedPaymentDate == expTTI.ExpectedPaymentDate && t.Amount == expTTI.Amount);
                    if (srchTTI == null)
                    {
                        // Look for a single TTI by TT, EFT, PP and Amt
                        srchTTI = curTTIs.FirstOrDefault(t => !t.TTI_Matched && t.TransactionTypeId == expTTI.TransactionTypeId && t.PayerPayee == expTTI.PayerPayee &&
                                                        t.EFT_AccountName == expTTI.EFT_AccountName && t.EFT_BSB == expTTI.EFT_BSB && 
                                                        t.EFT_AccountNo == expTTI.EFT_AccountNo && t.EFT_Reference == expTTI.EFT_Reference &&
                                                        !t.ExpectedPaymentDate.HasValue && t.Amount == expTTI.Amount);
                        if (srchTTI == null)
                        {
                            // Look for match on TT, PP and Amount
                            srchTTI = curTTIs.FirstOrDefault(t => !t.TTI_Matched && t.TransactionTypeId == expTTI.TransactionTypeId &&
                                                                t.Amount == expTTI.Amount && t.PayerPayee == expTTI.PayerPayee);

                            if (srchTTI == null)
                            {
                                // Look for match on TT and Amount (but check notes are equal to avoid matching cancelled funds with non cancelled)
                                srchTTI = curTTIs.FirstOrDefault(t => !t.TTI_Matched && t.TransactionTypeId == expTTI.TransactionTypeId &&
                                    t.Amount == expTTI.Amount && t.TTI_Paid );
                            }
                        }
                    }
                }


                if (srchTTI != null) //this expected TTI has already been created - check if any details need to be updated 
                {
                    //Only update changes if it hasn't been paid
                    if (!srchTTI.TTI_Paid &&
                        (srchTTI.Description != expTTI.Description ||
                        srchTTI.Notes != expTTI.Notes ||
                        srchTTI.ExpectedPaymentDate != expTTI.ExpectedPaymentDate ||
                        srchTTI.EFT_AccountName != expTTI.EFT_AccountName ||
                        srchTTI.EFT_BSB != expTTI.EFT_BSB ||
                        srchTTI.EFT_AccountNo != expTTI.EFT_AccountNo ||
                        srchTTI.EFT_Reference != expTTI.EFT_Reference
                        ))
                    {
                        srchTTI.Description = expTTI.Description;
                        srchTTI.Notes = expTTI.Notes;
                        srchTTI.ExpectedPaymentDate = expTTI.ExpectedPaymentDate;
                        srchTTI.EFT_AccountName = expTTI.EFT_AccountName;
                        srchTTI.EFT_BSB = expTTI.EFT_BSB;
                        srchTTI.EFT_AccountNo = expTTI.EFT_AccountNo;
                        srchTTI.EFT_Reference = expTTI.EFT_Reference;
                        srchTTI.IsDirty = true;
                    }
                    if(srchTTI.Notes == null && expTTI.ProcessOrder == (int)TrustTransactionStatusTypeEnum.Reversed)
                    {
                        srchTTI.Notes = expTTI.Notes;
                        srchTTI.IsDirty = true;
                        context.TrustTransactionItems.FirstOrDefault(t => t.TrustTransactionItemId == srchTTI.TrustTransactionItemId).Notes = expTTI.Notes;
                        context.SaveChanges();
                    }
                    srchTTI.MLIs_Matched = CheckMLIsMatch(srchTTI.MatterLedgerItemIds, expTTI.MatterLedgerItemIds);
                    if (!srchTTI.MLIs_Matched)
                    {
                        srchTTI.MatterLedgerItemIds = expTTI.MatterLedgerItemIds;
                        srchTTI.IsDirty = true;
                    }
                    srchTTI.TTI_Matched = true;
                    expTTI.TTI_Matched = true;
                }

            }

            //In many cases, the total of the expTTI's matches a reconciled/flagged amount - these can be removed from the creation queue.)
            var expTTIsum = expTTISummary.Where(t => !t.TTI_Matched).Sum(x => x.Amount);
            foreach (var curTTI in curTTIs.Where(t => !t.TTI_Matched && 
                                                       (t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Flagged ||
                                                        t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Reconciled ||
                                                        t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.UserFlagged)))
            {
                if (curTTI.Amount == expTTIsum)
                {
                    foreach (var expTTI in expTTISummary.Where(t => !t.TTI_Matched))
                    {
                        expTTI.TTI_Matched = true;

                    }
                    curTTI.TTI_Matched = true;

                    if (mv.MatterGroupTypeId != (int)MatterGroupTypeEnum.Discharge ||
                         (mv.MatterGroupTypeId == (int)MatterGroupTypeEnum.Discharge && transactionDirectionTypeId == (int)TransactionDirectionTypeEnum.Payment))
                    {
                        var mlis = new List<int>();
                        foreach (var expTTI in expTTISummary.Where(t => !t.TTI_Matched))
                            foreach (var mli in expTTI.MatterLedgerItemIds)
                                mlis.Add(mli);

                        curTTI.MLIs_Matched = CheckMLIsMatch(curTTI.MatterLedgerItemIds, mlis);
                    }
                    else
                        curTTI.MLIs_Matched = true;

                }
            }

            //Or the other way around... total of currTTI's matches ExpTTI
            var currTTISum = curTTIs.Where(t => !t.TTI_Matched &&
                                                       (t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Flagged ||
                                                        t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Reconciled ||
                                                        t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.UserFlagged))
                                    .Sum(x => x.Amount);
            foreach (var expTTI in expTTISummary.Where(t => !t.TTI_Matched))
            {
                if (expTTI.Amount == currTTISum)
                {
                    foreach (var curTTI in curTTIs.Where(t => !t.TTI_Matched &&
                                                        (t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Flagged ||
                                                        t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Reconciled ||
                                                        t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.UserFlagged)))
                    {
                        curTTI.TTI_Matched = true;
                    }

                    expTTI.TTI_Matched = true;
                }
            }


            //Now more complicated searches, use only items that have been paid
            //  Search for a match and reduce it from the expected amount, then add the remainder
            foreach (var expTTI in expTTISummary.Where(t => !t.TTI_Matched))
            {
                //Create a temp TTI record for this mti
                var tmpTTI = new AccountsCustomEntities.TB_TrustTransactionItem()
                {
                    TransactionDirectionTypeId = transactionDirectionTypeId,
                    TrustTransactionItemId = 0,
                    TransactionTypeId = expTTI.TransactionTypeId,
                    PayableTypeId = expTTI.PayableTypeId,
                    PayerPayee = expTTI.PayerPayee,
                    EFT_AccountName = expTTI.EFT_AccountName,
                    EFT_BSB = expTTI.EFT_BSB,
                    EFT_AccountNo = expTTI.EFT_AccountNo,
                    EFT_Reference = expTTI.EFT_Reference,
                    ExpectedPaymentDate = expTTI.ExpectedPaymentDate,
                    Amount = expTTI.Amount,      
                    Description = expTTI.Description,
                    Notes = expTTI.Notes,
                    TrustTransactionStatusTypeId = (int)TrustTransactionStatusTypeEnum.Expected,
                    MatterLedgerItemIds = expTTI.MatterLedgerItemIds,
                    TTI_Matched = true
                };

            //Does this match 
                if (transactionDirectionTypeId == (int)TransactionDirectionTypeEnum.Deposit)
                {
                    // Look for multiple TTIs by TT, PP, Date, Amt
                    var srchTTIs = curTTIs.Where(t => !t.TTI_Matched && t.TransactionTypeId == expTTI.TransactionTypeId && 
                                                    t.TrustTransactionStatusTypeId != (int)TrustTransactionStatusTypeEnum.Expected &&
                                                    t.PayerPayee == expTTI.PayerPayee && t.ExpectedPaymentDate == expTTI.ExpectedPaymentDate).ToList();
                    if (srchTTIs.Sum(x => x.Amount) == expTTI.Amount)
                    {
                        foreach ( var srchtti in srchTTIs)
                        {
                            if (srchtti.Description != expTTI.Description)
                            {
                                srchtti.Description = expTTI.Description;
                                srchtti.IsDirty = true;
                            }
                            srchtti.MLIs_Matched = CheckMLIsMatch(srchtti.MatterLedgerItemIds, expTTI.MatterLedgerItemIds);
                            if (!srchtti.MLIs_Matched)
                            {
                                srchtti.MatterLedgerItemIds = expTTI.MatterLedgerItemIds;
                                srchtti.IsDirty = true;
                            }
                            srchtti.TTI_Matched = true;
                        }
                        tmpTTI.Amount = 0;
                    }
                    else
                    {
                        // Look for multiple TTIs by TT
                        srchTTIs = curTTIs.Where(t => !t.TTI_Matched && t.TransactionTypeId == expTTI.TransactionTypeId && t.TrustTransactionStatusTypeId != (int)TrustTransactionStatusTypeEnum.Journalled &&
                                                    t.TrustTransactionStatusTypeId != (int)TrustTransactionStatusTypeEnum.Expected).ToList();



                        if (srchTTIs.Count() > 0 && srchTTIs.Sum(x => x.Amount) <= tmpTTI.Amount)
                        {
                        //first check through to see if this matches a journalled amount as well
                            var srchCopy = srchTTIs.ToList();
                            foreach (var srchTTI in srchCopy)
                            {
                                var match = context.TrustTransactionItems.Where(t => t.MatterId == matterId && t.TransactionDirectionTypeId == transactionDirectionTypeId && 
                                                                                            t.TrustTransactionJournalId != null &&
                                                                                            t.PayerPayee == srchTTI.PayerPayee &&
                                                                                            t.Amount == srchTTI.Amount && 
                                                                                            t.ExpectedPaymentDate == srchTTI.ExpectedPaymentDate).FirstOrDefault();
                                if (match != null)
                                {
                                    curTTIs.Where(t => t.TrustTransactionItemId == srchTTI.TrustTransactionItemId).FirstOrDefault().TTI_Matched = true;

                                    srchTTIs.Remove(srchTTI);
                                }
                            }

                            // There are some matching items
                            foreach (var srchtti in srchTTIs)
                            {
                                if (srchtti.Description != expTTI.Description)
                                {
                                    srchtti.Description = expTTI.Description;
                                    srchtti.IsDirty = true;
                                }
                                srchtti.MLIs_Matched = CheckMLIsMatch(srchtti.MatterLedgerItemIds, expTTI.MatterLedgerItemIds);
                                if (!srchtti.MLIs_Matched)
                                {
                                    srchtti.MatterLedgerItemIds = expTTI.MatterLedgerItemIds;
                                    srchtti.IsDirty = true;
                                }
                                srchtti.TTI_Matched = true;
                            }

                            tmpTTI.Amount = tmpTTI.Amount - srchTTIs.Sum(x => x.Amount);

                            if (tmpTTI.Amount !=  0)
                            {
                            //Maybe we can add it onto an currTTI which is "Unpaid"
                                if (srchTTIs.Any(t => !t.TTI_Paid))
                                {
                                    var tti = srchTTIs.FirstOrDefault(t => !t.TTI_Paid);
                                    tti.Amount = tti.Amount + tmpTTI.Amount;
                                    tti.TrustTransactionStatusTypeId = (int)TrustTransactionStatusTypeEnum.Expected;
                                    tti.TTI_Matched = true;
                                    tti.IsDirty = true;
                                    tmpTTI.Amount = 0;
                                }
                            }
                        }
                    }
                }
                else             //Payments
                {
                    // Look for multiple TTIs by TT, EFT, PP
                    var srchTTIs = curTTIs.Where(t => !t.TTI_Matched && t.TransactionTypeId == expTTI.TransactionTypeId &&
                                                    t.TrustTransactionStatusTypeId != (int)TrustTransactionStatusTypeEnum.Expected &&
                                                    t.EFT_AccountName == expTTI.EFT_AccountName && t.EFT_BSB == expTTI.EFT_BSB && t.EFT_AccountNo == expTTI.EFT_AccountNo &&
                                                    t.PayerPayee == expTTI.PayerPayee).ToList();

                    if (srchTTIs.Count() > 0 && srchTTIs.Sum(x => x.Amount) <= tmpTTI.Amount)
                    {
                        // There are some matching items
                        foreach (var srchtti in srchTTIs)
                        {
                            if (srchtti.Description != expTTI.Description)
                            {
                                srchtti.Description = expTTI.Description;
                                srchtti.IsDirty = true;
                            }
                            srchtti.MLIs_Matched = CheckMLIsMatch(srchtti.MatterLedgerItemIds, expTTI.MatterLedgerItemIds);
                            if (!srchtti.MLIs_Matched)
                            {
                                srchtti.MatterLedgerItemIds = expTTI.MatterLedgerItemIds;
                                srchtti.IsDirty = true;
                            }
                            srchtti.TTI_Matched = true;
                        }
                        tmpTTI.Amount = tmpTTI.Amount - srchTTIs.Sum(x => x.Amount);

                        if (tmpTTI.Amount != 0)
                        {
                            //Maybe we can add it onto an currTTI which is "Unpaid"
                            if (srchTTIs.Any(t => !t.TTI_Paid))
                            {
                                var tti = srchTTIs.FirstOrDefault(t => !t.TTI_Paid);
                                tti.Amount = tti.Amount + tmpTTI.Amount;
                                tti.TrustTransactionStatusTypeId = (int)TrustTransactionStatusTypeEnum.Expected;
                                tti.TTI_Matched = true;
                                tti.IsDirty = true;
                                tmpTTI.Amount = 0;
                            }
                        }
                    }
                    else
                    {
                        // Look for multiple TTIs by TT, PP
                        srchTTIs = curTTIs.Where(t => !t.TTI_Matched && t.TransactionTypeId == expTTI.TransactionTypeId &&
                                                    t.TrustTransactionStatusTypeId != (int)TrustTransactionStatusTypeEnum.Expected &&
                                                    t.PayerPayee == expTTI.PayerPayee).ToList();
                        if (srchTTIs.Count() > 0 && srchTTIs.Sum(x => x.Amount) <= tmpTTI.Amount)
                        {
                            // There are some matching items
                            foreach (var srchtti in srchTTIs)
                            {
                                if (srchtti.Description != expTTI.Description)
                                {
                                    srchtti.Description = expTTI.Description;
                                    srchtti.IsDirty = true;
                                }
                                if (srchtti.Notes != expTTI.Notes)
                                {
                                    srchtti.Notes = expTTI.Notes;
                                    srchtti.IsDirty = true;
                                }
                                srchtti.MLIs_Matched = CheckMLIsMatch(srchtti.MatterLedgerItemIds, expTTI.MatterLedgerItemIds);
                                if (!srchtti.MLIs_Matched)
                                {
                                    srchtti.MatterLedgerItemIds = expTTI.MatterLedgerItemIds;
                                    srchtti.IsDirty = true;
                                }
                                srchtti.TTI_Matched = true;
                            }
                            tmpTTI.Amount = tmpTTI.Amount - srchTTIs.Sum(x => x.Amount);

                            if (tmpTTI.Amount != 0)
                            {
                                //Maybe we can add it onto an currTTI which is "Unpaid"
                                if (srchTTIs.Any(t => !t.TTI_Paid))
                                {
                                    var tti = srchTTIs.FirstOrDefault(t => !t.TTI_Paid);
                                    tti.Amount = tti.Amount + tmpTTI.Amount;
                                    tti.TrustTransactionStatusTypeId = (int)TrustTransactionStatusTypeEnum.Expected;
                                    tti.TTI_Matched = true;
                                    tti.IsDirty = true;
                                    tmpTTI.Amount = 0;
                                }
                            }
                        }
                    }
                }

                if (tmpTTI.Amount > 0)
                {
                    curTTIs.Add(tmpTTI);
                }
            }


            //update unmatched, unpaid, but modifed trust transaction items. 
            foreach (var tti in curTTIs.Where(t => !t.TTI_Paid && (t.IsDirty || !t.TTI_Matched)))
            {
                TrustTransactionItem tmpTTI;
                if (tti.TTI_Matched)
                {
                    if (tti.TrustTransactionItemId == 0)
                    {
                        tmpTTI = new TrustTransactionItem()
                        {
                            MatterId = matterId,
                            TransactionDirectionTypeId = tti.TransactionDirectionTypeId,
                            TrustTransactionStatusTypeId = tti.TrustTransactionStatusTypeId,
                            PaymentPrepared = false
                        };
                        if (tmpTTI.PayableTypeId == (int)PayableTypeEnum.OtherParty)
                            tmpTTI.PayableOthersDesc = tti.PayerPayee;
                    }
                    else
                    {
                        tmpTTI = context.TrustTransactionItems.FirstOrDefault(t => t.TrustTransactionItemId == tti.TrustTransactionItemId);
                    }
                    if (tmpTTI.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Expected)
                    {
                        tmpTTI.TransactionTypeId = tti.TransactionTypeId;
                        tmpTTI.PayableTypeId = tti.PayableTypeId;
                        tmpTTI.EFT_AccountName = tti.EFT_AccountName;
                        tmpTTI.EFT_BSB = tti.EFT_BSB;
                        tmpTTI.EFT_AccountNo = tti.EFT_AccountNo;
                        tmpTTI.EFT_Reference = tti.EFT_Reference;
                        tmpTTI.PayerPayee = tti.PayerPayee;
                        tmpTTI.Description = tti.Description;
                        tmpTTI.Notes = tti.Notes;
                        tmpTTI.ExpectedPaymentDate = tti.ExpectedPaymentDate;
                        tmpTTI.Amount = tti.Amount;
                    }
                    tmpTTI.UpdatedDate = DateTime.Now;
                    tmpTTI.UpdatedByUserId = GlobalVars.CurrentUser.UserId;

                    if (tti.TrustTransactionItemId == 0)
                    {
                        context.TrustTransactionItems.Add(tmpTTI);
                        context.SaveChanges();
                        tti.TrustTransactionItemId = tmpTTI.TrustTransactionItemId;
                    }

                    trustItemsChanged++;
                }
                else    //Unmatched Transaction
                {
                    //If this is an "Expected" transaction, then remove it
                    if (tti.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Expected)
                    {
                        //This Trust Item from wherever it exists
                        var mlitis = context.MatterLedgerItemTrustTransactionItems.Where(t => t.TrustTransactionItemId == tti.TrustTransactionItemId);
                        context.MatterLedgerItemTrustTransactionItems.RemoveRange(mlitis);
                        context.SaveChanges();

                        tmpTTI = context.TrustTransactionItems.FirstOrDefault(t => t.TrustTransactionItemId == tti.TrustTransactionItemId);
                        context.TrustTransactionItems.Remove(tmpTTI);
                        trustItemsChanged++;
                    }
                }
            }

            //Fix "links" to MatterLedgerItem
            foreach (var tti in curTTIs.Where(t => !t.MLIs_Matched))
            {
                var mlitis = context.MatterLedgerItemTrustTransactionItems.Where(t => t.TrustTransactionItemId == tti.TrustTransactionItemId);
                context.MatterLedgerItemTrustTransactionItems.RemoveRange(mlitis);
                context.SaveChanges();
                foreach (var id in tti.MatterLedgerItemIds)
                {
                    var mliti = new MatterLedgerItemTrustTransactionItem()
                    {
                        MatterLedgerItemId = id,
                        TrustTransactionItemId = tti.TrustTransactionItemId
                    };
                    var existingLink = context.MatterLedgerItemTrustTransactionItems.Where(m => m.MatterLedgerItemId == mliti.MatterLedgerItemId && m.TrustTransactionItemId == mliti.TrustTransactionItemId).FirstOrDefault();
                    if (existingLink == null)
                    {

                        if (context.TrustTransactionItems.Where(m => m.TrustTransactionItemId == mliti.TrustTransactionItemId).FirstOrDefault() != null && context.MatterLedgerItems.Where(m => m.MatterLedgerItemId == mliti.MatterLedgerItemId).FirstOrDefault() != null)
                        {
                            context.MatterLedgerItemTrustTransactionItems.Add(mliti);
                            trustItemsChanged++;
                        }
                    }
                }
            }

            if (trustItemsChanged > 0 && saveContext)
                context.SaveChanges();

            
            return trustItemsChanged;
        }
        /// <summary>
        /// Checks if every Integer in List 1 exists in List 2
        /// </summary>
        /// <param name="mlis1">Int List 1</param>
        /// <param name="mlis2">Int List 2</param>
        /// <returns>True if match, False if no match</returns>
        private bool CheckMLIsMatch(IEnumerable<int> mlis1, IEnumerable<int> mlis2 )
        {
            if (mlis1.Count() != mlis2.Count())
                return false;

            foreach (var id in mlis1)
            {
                if (!mlis2.Any(x => x == id))
                    return false;
            }

            return true;

        }

        /// <summary>
        /// Simplified MLI class for easy access to the parent transaction type of a <see cref="MatterLedgerItem"/>, to easily treat all variations of EFT as EFT, etc.
        /// </summary>
        class mliPlus
        {
            public MatterLedgerItem mli { get; set; }
            public int? ParentTransactionTypeId { get; set; }

            public mliPlus() { }
        }

        /// <summary>
        /// Get views for all expected <see cref="TrustTransactionItem"/>s for a <see cref="Matter"/> from <see cref="MatterLedgerItem"/> filtered based on settlement date, expected payment dates, etc.
        /// </summary>
        /// <remarks>
        /// Populates the list of expTti used in <see cref="AccountsRepository.UpdateExpectedTrustTransactionsForDirection(int, IEnumerable{AccountsCustomEntities.TB_TrustTransactionItem}, int, bool)"/>.
        /// </remarks>
        /// <param name="matterId"><see cref="Matter.MatterID"/> for the matter to get expected trust transaction items for.</param>
        /// <param name="includeShortfall">Flag to determine whether to include shortfall amounts in this amount regardless of whether or not settlement has been booked.</param>
        /// <returns></returns>
        public IEnumerable<AccountsCustomEntities.TB_TrustTransactionItem> GetExpectedTrustTransactionItems(int matterId, int includeShortfall = 0)
        {
            var mRep = new MatterRepository(context);
            var mv = mRep.GetMatterDetailsCompact(matterId);
            var mwfRep = new MatterWFRepository(context);
            List<AccountsCustomEntities.TB_TrustTransactionItem> ttis = new List<AccountsCustomEntities.TB_TrustTransactionItem>();
            if (mv == null)
                return ttis;

            //No Discharge TRUST items for any matters settling before 16-Apr-2018
            if (mv.MatterGroupTypeId == (int)MatterGroupTypeEnum.Discharge && 
                    ((mv.SettlementDate.HasValue && mv.SettlementDate < new DateTime(2018, 04, 16, 0, 0, 0))
                    || !mwfRep.HasMilestoneCompleted(matterId, (int)WFComponentEnum.PrepareSettlementInstrDisch, true)))
                return ttis;

            //No Bluestone Trust Items for any matter settling before 29-Jan-2019
            if (mv.MatterGroupTypeId == (int)MatterGroupTypeEnum.NewLoan &&
                mv.LenderId == 43 && 
                mv.SettlementDate.HasValue && mv.SettlementDate < new DateTime(2019, 1, 29, 0, 0, 0))
                return ttis;

            //No MyState Trust Items for any matter settling before 15-Mar-2019
            if (mv.MatterGroupTypeId == (int)MatterGroupTypeEnum.NewLoan &&
                mv.LenderId == 46 &&
                mv.SettlementDate.HasValue && mv.SettlementDate < new DateTime(2019, 3, 15, 0, 0, 0))
                return ttis;

            //No Trust Items for TASMANIA or SOUTH AUSTRALIA state matters
            if (mv.StateId == (int)StateIdEnum.TAS || mv.StateId == (int)StateIdEnum.SA)
                return ttis;


            var mlis = context.MatterLedgerItems.Where(m => m.MatterId == matterId &&
                                                                    m.MatterLedgerItemStatusTypeId != (int)MatterLedgerItemStatusTypeEnum.NotProceeding &&
                                                                    m.TransactionTypeId != (int)TransactionTypeEnum.Invoice)
                                                         .Select(x => new mliPlus()
                                                         {
                                                             mli = x,
                                                             ParentTransactionTypeId = x.TransactionType.ParentTransactionTypeId
                                                         }).ToList();
            
            foreach (var mlip in context.MatterLedgerItems.Where(m => m.MatterId == matterId &&
                                                                    m.MatterLedgerItemStatusTypeId != (int)MatterLedgerItemStatusTypeEnum.NotProceeding &&
                                                                    m.TransactionTypeId != (int)TransactionTypeEnum.Invoice )
                                                         .Select(x => new mliPlus() { mli = x,
                                                                    ParentTransactionTypeId =x.TransactionType.ParentTransactionTypeId}))
        
            {
                if (mv.SettlementDate.HasValue || mlip.mli.ExpectedPaymentDate.HasValue)
                {
                    if (mv.MatterGroupTypeId == (int)MatterGroupTypeEnum.NewLoan || mv.MatterGroupTypeId == (int)MatterGroupTypeEnum.Consent)
                    {
                        if (mlip.mli.PayableByAccountTypeId == (int)AccountTypeEnum.Trust)
                            ttis.Add(BuildTTIfromMLI(mlip, (int)TransactionDirectionTypeEnum.Payment));
                        else
                            ttis.Add(BuildTTIfromMLI(mlip, (int)TransactionDirectionTypeEnum.Deposit));
                    }

                    if (mv.MatterGroupTypeId == (int)MatterGroupTypeEnum.Discharge)
                    {
                        var transchqList = new List<int>();
                        transchqList.Add((int)TransactionTypeEnum.EFT);
                        transchqList.Add((int)TransactionTypeEnum.EFTFree);
                        transchqList.Add((int)TransactionTypeEnum.PEXA);
                        transchqList.Add((int)TransactionTypeEnum.PEXAsingle);
                        transchqList.Add((int)TransactionTypeEnum.PEXAdebit);
                        transchqList.Add((int)TransactionTypeEnum.TT);
                        transchqList.Add((int)TransactionTypeEnum.TTFree);

                        if (mv.IsSelfActing &&
                             transchqList.Contains(mlip.mli.TransactionTypeId))
                        {
                            ttis.Add(BuildTTIfromMLI(mlip, (int)Enums.TransactionDirectionTypeEnum.Deposit, (int)PayableTypeEnum.Borrower, null));
                            ttis.Add(BuildTTIfromMLI(mlip, (int)Enums.TransactionDirectionTypeEnum.Payment));
                        }
                    }

                    
                }
                else if (mlip.mli.Description == "Shortfall" && includeShortfall == 1)
                {
                    if (mv.MatterGroupTypeId == (int)MatterGroupTypeEnum.NewLoan)
                    {
                        var newTti = BuildTTIfromMLI(mlip, (int)TransactionDirectionTypeEnum.Deposit);
                        if (mlip.mli.ExpectedPaymentDate.HasValue)
                        {
                            newTti.ExpectedPaymentDate = mlip.mli.ExpectedPaymentDate;
                        }
                        ttis.Add(newTti);
                    }
                }
            }





            return ttis;

        }


        /// <summary>
        /// Calls <see cref="AccountsRepository.BuildTTIfromMLI(int, int, int?, string)"/> for a <see cref="mliPlus"/> with no overriding payableTypeId or payableOthersDesc set.
        /// </summary>
        /// <param name="mli">The <see cref="mliPlus"/> to construct a trust transaction item for.</param>
        /// <param name="transactionDirectionTypeId">(int)<see cref="TransactionDirectionTypeEnum"/> for this transaction - are we constructing a deposit or payment TTI? </param>
        /// <returns></returns>
        private AccountsCustomEntities.TB_TrustTransactionItem BuildTTIfromMLI(mliPlus mli, int transactionDirectionTypeId)
        {
            return BuildTTIfromMLI(mli, transactionDirectionTypeId, null, null);
        }

        /// <summary>
        /// Create a <see cref="AccountsCustomEntities.TB_TrustTransactionItem"/> for an <see cref="mliPlus"/>.
        /// </summary>
        /// <remarks>If payableOthersDesc is supplied, this will only have any impact if payableTypeId is also supplied.</remarks>
        /// <param name="mlip"></param>
        /// <param name="transactionDirectionTypeId"></param>
        /// <param name="payableTypeId">If provided override the <see cref="MatterLedgerItem"/>'s PayableTypeId with a provided (int)<see cref="PayableTypeEnum"/></param>
        /// <param name="payableOthersDesc">If provided override the <see cref="MatterLedgerItem"/>'s PayableOthersDesc with the supplied string.</param>
        /// <returns>
        /// The new <see cref="AccountsCustomEntities.TB_TrustTransactionItem"/> generated from the <see cref="mliPlus"/>
        /// </returns>
        private AccountsCustomEntities.TB_TrustTransactionItem BuildTTIfromMLI(mliPlus mlip, int transactionDirectionTypeId, int? payableTypeId, string  payableOthersDesc)
        {
            AccountsCustomEntities.TB_TrustTransactionItem tti = new AccountsCustomEntities.TB_TrustTransactionItem();
            tti.TransactionDirectionTypeId = transactionDirectionTypeId;
            if (transactionDirectionTypeId == (int)TransactionDirectionTypeEnum.Deposit)
            {
                tti.TransactionTypeId = (int)TransactionTypeEnum.EFT;
                if (payableTypeId.HasValue)
                {
                    tti.PayableTypeId = payableTypeId.Value;
                    tti.PayerPayee = GetPayerPayee(mlip.mli.MatterId, payableTypeId.Value, payableOthersDesc);
                }
                else
                {
                    tti.PayableTypeId = mlip.mli.PayableByTypeId;
                    tti.PayerPayee = GetPayerPayee(mlip.mli.MatterId, mlip.mli.PayableByTypeId, mlip.mli.PayableByOthersDesc);
                }
            }
            else
            {
                if (payableTypeId.HasValue)
                {
                    tti.PayableTypeId = payableTypeId.Value;
                    tti.PayerPayee = GetPayerPayee(mlip.mli.MatterId, payableTypeId.Value, payableOthersDesc);
                }
                else
                {
                    tti.PayableTypeId = mlip.mli.PayableToTypeId;
                    tti.PayerPayee = GetPayerPayee(mlip.mli.MatterId, mlip.mli.PayableToTypeId, mlip.mli.PayableToOthersDesc);
                }

                if (mlip.ParentTransactionTypeId.HasValue &&
                            mlip.mli.TransactionTypeId != (int)TransactionTypeEnum.PEXAsingle &&
                            mlip.mli.TransactionTypeId != (int)TransactionTypeEnum.PPS && 
                            mlip.mli.TransactionTypeId != (int)TransactionTypeEnum.PPSFree)
                    tti.TransactionTypeId = mlip.ParentTransactionTypeId.Value;
                else
                    tti.TransactionTypeId = mlip.mli.TransactionTypeId;
            }
            tti.EFT_AccountName = mlip.mli.EFT_AccountName;
            tti.EFT_BSB = mlip.mli.EFT_BSB;
            tti.EFT_AccountNo = mlip.mli.EFT_AccountNo;
            tti.EFT_Reference = mlip.mli.EFT_Reference;
            tti.Amount = decimal.Round(mlip.mli.Amount, 2);
            tti.Description = mlip.mli.Description;
            tti.Notes = mlip.mli.PaymentNotes;
            tti.ExpectedPaymentDate = mlip.mli.ExpectedPaymentDate;
            tti.TrustTransactionStatusTypeId = (int)TrustTransactionStatusTypeEnum.Expected;
            tti.FundsTransferredTypeId = mlip.mli.FundsTransferredTypeId;

            tti.MatterLedgerItemIds.Add(mlip.mli.MatterLedgerItemId);

            return tti;
        }

        //private string GetPayerPayee(MatterLedgerItem mli, int transactionDirectionTypeId)
        //{
        //    if (transactionDirectionTypeId == (int)Enums.TransactionDirectionTypeEnum.Deposit)
        //        return GetPayerPayee(mli.MatterId, mli.PayableByTypeId, mli.PayableByOthersDesc);
        //    else
        //        return GetPayerPayee(mli.MatterId, mli.PayableToTypeId, mli.PayableToOthersDesc);

        //}

        /// <summary>
        /// Construct the PayerPayee string for a combination of <see cref="Matter"/>, (int)<see cref="PayableTypeEnum"/> and payableOthersDesc.
        /// </summary>
        /// <param name="matterId"><see cref="Matter.MatterId"/> for the Matter this item belongs to.</param>
        /// <param name="payableTypeId">(int)<see cref="PayableTypeEnum"/> for the party that will be paying this out.</param>
        /// <param name="payableOthersDesc">Description to be used if payable by <see cref="PayableTypeEnum.OtherParty"/></param>
        /// <returns></returns>
        private string GetPayerPayee(int matterId, int payableTypeId, string payableOthersDesc)
        {
            try
            {
                string PayerPayee = "";

                var mt = context.Matters.AsNoTracking().Where(m => m.MatterId == matterId)
                                .Select(m => new
                                {
                                    m.Lender.LenderName,
                                    m.MortMgr.MortMgrName,
                                    Party = m.MatterParties.FirstOrDefault(p => p.PartyTypeId == (int)MatterPartyTypeEnum.Borrower)
                                })
                                .FirstOrDefault();

                switch (payableTypeId)
                {
                    case (int)Enums.PayableTypeEnum.Borrower:
                        if (mt.Party != null)
                            if (mt.Party.IsIndividual)
                            {
                                PayerPayee = (mt.Party.Firstname + " " + mt.Party.Lastname).Trim();
                            }
                            else if(!String.IsNullOrWhiteSpace(mt.Party.CompanyName))
                            {
                                PayerPayee = (mt.Party.CompanyName).Trim();
                            }
                            else
                            {
                                PayerPayee = "Borrower";
                            }
                        else
                            PayerPayee = "Borrower";
                        break;
                    case (int)Enums.PayableTypeEnum.Lender:
                        if (!String.IsNullOrEmpty(mt.LenderName))
                            PayerPayee = mt.LenderName.Trim();
                        else
                            PayerPayee = "Lender";
                        break;
                    case (int)Enums.PayableTypeEnum.MSA:
                        PayerPayee = "MSA National";
                        break;
                    case (int)Enums.PayableTypeEnum.MortMgr:
                        if (!String.IsNullOrEmpty(mt.MortMgrName))
                            PayerPayee = mt.MortMgrName.Trim();
                        else
                            PayerPayee = "Mort Mgr";
                        break;
                    case (int)Enums.PayableTypeEnum.OtherParty:
                        if (!String.IsNullOrEmpty(payableOthersDesc))
                            PayerPayee = payableOthersDesc.Trim();
                        else
                            PayerPayee = "Other Party";
                        break;
                }


                return PayerPayee;
            }
            catch (Exception ex)
            {
                Slick_Domain.Handlers.ErrorHandler.LogError(ex);
                return "";
            }
        }

        /// <summary>
        /// Update <see cref="MatterLedgerItem"/>/s linked to a <see cref="TrustTransactionItem"/> when the Trust item is updated - pass on funds transferred type and the payment to the MLI.
        /// </summary>
        /// <param name="tti"><see cref="AccountsCustomEntities.TrustTransactionItemView"/> of the updated <see cref="TrustTransactionItem"/></param>
        /// <param name="fundsTransferredTypeId">(int)<see cref="FundsTransferredTypeEnum"/> to pass on to the <see cref="MatterLedgerItem"/></param>
        /// <param name="paymentDate">If this trust transaction item has been paid, the date to pass on as the new <see cref="MatterLedgerItem.ExpectedPaymentDate"/></param>
        public void UpdateMLIFundsTransferred(AccountsCustomEntities.TrustTransactionItemView tti, int fundsTransferredTypeId, DateTime? paymentDate)
        {
            var mlittis = context.MatterLedgerItemTrustTransactionItems.Where(t => t.TrustTransactionItemId == tti.TrustTransactionItemId);

            if (fundsTransferredTypeId == (int)FundsTransferredTypeEnum.UnPaid)
            {
                if (mlittis.Count() == 0) //Search for matterledgeritem that has no longer been linked, remove the expected payment date so that it gets swept up when cleaning up at cancel settlement 
                {
                    var mli = context.MatterLedgerItems.Where(m => m.MatterId == tti.MatterId && m.Amount == tti.Amount && m.ExpectedPaymentDate == tti.ExpectedPaymentDate
                                                                && m.TransactionTypeId == tti.TransactionTypeId && m.EFT_AccountName == tti.EFT_AccountName && m.Description == tti.Description
                                                                && m.EFT_BSB == tti.EFT_BSB && m.EFT_Reference == tti.EFT_Reference && m.EFT_AccountNo == tti.EFT_AccountNo).FirstOrDefault();

                    if (mli != null) mli.ExpectedPaymentDate = null;

                }
            }
            foreach (var mlitti in mlittis)
            {
                var mli = context.MatterLedgerItems.FirstOrDefault(m => m.MatterLedgerItemId == mlitti.MatterLedgerItemId);
                mli.FundsTransferredTypeId = fundsTransferredTypeId;
                if (fundsTransferredTypeId != (int)FundsTransferredTypeEnum.UnPaid)
                    mli.ExpectedPaymentDate = paymentDate;
                else
                    mli.ExpectedPaymentDate = null;
            }

            context.SaveChanges();
        }

        /// <summary>
        /// Construct <see cref="AccountsCustomEntities.TrustTransactionItemView"/>s for a query of <see cref="TrustTransactionItem"/>
        /// </summary>
        /// <param name="trustTransactionItemsQry">The <see cref="IQueryable"/> of <see cref="TrustTransactionItem"/>s to construct views for.</param>
        /// <returns>IEnumerable of <see cref="AccountsCustomEntities.TrustTransactionItemView"/>s for <see cref="TrustTransactionItem"/>s.</returns>
        public IEnumerable<AccountsCustomEntities.TrustTransactionItemView> GetTrustTransactionItemsView(IQueryable<TrustTransactionItem> trustTransactionItemsQry)
        {
            return trustTransactionItemsQry
                .Select(m => new
                {
                    m.TrustTransactionItemId,
                    m.MatterId,
                    m.Matter.MatterDescription,
                    m.Matter.MatterType.MatterTypeName,
                    m.Matter.LenderId,
                    m.Matter.Lender.LenderName,
                    m.Matter.Lender.LenderNameShort,
                    m.Matter.StateId,
                    m.Matter.State.StateName,
                    SettlementDate = (DateTime?)m.Matter.SettlementSchedule.SettlementDate,
                    m.Matter.Settled,
                    m.Matter.LenderRefNo,
                    m.Matter.SecondaryRefNo,
                    m.Matter.Lender.SecondaryRefName,
                    FileOwnerUser = m.Matter.User,
                    m.TransactionDirectionTypeId,
                    m.TransactionDirectionType.TransactionDirectionTypeName,
                    m.TransactionTypeId,
                    m.TransactionType.TransactionTypeName,
                    m.TransactionType.ReportDisplayName,
                    m.ExpectedPaymentDate,
                    m.PayableTypeId,
                    m.PayableOthersDesc,
                    m.PayerPayee,
                    m.EFT_AccountName,
                    m.EFT_BSB,
                    m.EFT_AccountNo,
                    m.EFT_Reference,
                    m.Amount,
                    m.Description,
                    m.Notes,
                    m.PaymentPrepared,
                    m.TrustTransactionStatusTypeId,
                    m.TrustTransactionStatusType.TrustTransactionStatusTypeName,
                    JournalDetails = m.TrustTransactionJournals1.Select(p=>new {p.JournalBatchId, p.JournalDate, p.TrustTransactionJournalId, p.User.Firstname, p.User.Lastname }).FirstOrDefault(),
                    TrustTransactionStatusBackgroundColour = m.TrustTransactionStatusType.BackgroundColour,
                    TrustSummaryId = (int?)m.TrustSummaryId,
                    m.TrustSummary.TrustSummaryNo,
                    m.TrustTransactionJournalId,
                    TransactionDate = m.TrustSummaryId.HasValue ? (DateTime?)m.TrustSummary.PaymentDate :
                                        m.TrustTransactionJournalId.HasValue ? (DateTime?)m.TrustTransactionJournal.CurrTransactionDate : null,
                    m.UpdatedDate,
                    m.UpdatedByUserId,
                    UpdatedByUsername = m.User.Username,
                }).ToList().Select(m => new AccountsCustomEntities.TrustTransactionItemView(m.MatterId, m.MatterDescription, m.LenderId, m.LenderName, m.LenderRefNo, m.SecondaryRefNo, m.SecondaryRefName,
                        m.TrustTransactionItemId,m.TrustSummaryId, m.TransactionDirectionTypeId, m.TransactionDirectionTypeName, m.TransactionTypeId, 
                        m.TransactionTypeName, m.ReportDisplayName, m.PayableTypeId, m.PayableOthersDesc, m.EFT_AccountName, m.EFT_BSB, m.EFT_AccountNo, m.EFT_Reference, 
                        m.StateId, m.StateName, m.SettlementDate, m.Settled, m.TrustTransactionStatusTypeId, m.TrustTransactionStatusTypeName,
                        m.TrustTransactionStatusBackgroundColour, m.TrustSummaryNo, m.TransactionDate,
                        m.PayerPayee, m.Amount, m.Description, m.Notes, m.ExpectedPaymentDate, m.PaymentPrepared,                        
                        0, 0, 0, 0, m.TrustTransactionJournalId, null, null, m.JournalDetails?.JournalBatchId, m.JournalDetails?.JournalDate, m.JournalDetails?.TrustTransactionJournalId, m.JournalDetails?.Firstname?.Trim() +" " + m.JournalDetails?.Lastname?.Trim()))
               .ToList();
        }

        /// <summary>
        /// Calls <see cref="AccountsRepository.GetTrustTransactionItemsView(IQueryable{TrustTransactionItem})"/> for all Trust Items for a <see cref="Matter"/>
        /// </summary>
        /// <param name="matterId"><see cref="Matter.MatterId"/></param>
        /// <returns>IEnumerable of <see cref="AccountsCustomEntities.TrustTransactionItemView"/> for a matter's <see cref="TrustTransactionItem"/>s.</returns>
        public IEnumerable<AccountsCustomEntities.TrustTransactionItemView> GetTrustTransactionItemsView(int matterId)
        {
            IQueryable<TrustTransactionItem> qry = context.TrustTransactionItems.AsNoTracking();

            qry = qry.Where(t => t.MatterId == matterId && t.Matter.MatterStatusTypeId != (int)Enums.MatterStatusTypeEnum.NotProceeding);
            return GetTrustTransactionItemsView(qry);
        }

        /// <summary>
        /// Calls <see cref="AccountsRepository.GetTrustTransactionItemsView(IQueryable{TrustTransactionItem})"/> for all Trust Items for a <see cref="Matter"/> for a transaction direction.
        /// </summary>
        /// <param name="transactionDirectionType">The <see cref="Enums.TransactionDirectionTypeEnum"> direction to get items for.</see></param>
        /// <param name="matterId"><see cref="Matter.MatterId"/> of the Matter to get trust items for.</param>
        /// <returns>IEnumerable of <see cref="AccountsCustomEntities.TrustTransactionItemView"/> for a matter's <see cref="TrustTransactionItem"/>s.</returns>
        public IEnumerable<AccountsCustomEntities.TrustTransactionItemView> GetTrustTransactionItemsView(Enums.TransactionDirectionTypeEnum transactionDirectionType, int matterId)
        {
            IQueryable<TrustTransactionItem> qry = context.TrustTransactionItems.AsNoTracking();

            qry = qry.Where(t => t.TransactionDirectionTypeId == (int)transactionDirectionType && t.MatterId == matterId && t.Matter.MatterStatusTypeId != (int)Enums.MatterStatusTypeEnum.NotProceeding);
            return GetTrustTransactionItemsView(qry);
        }

        /// <summary>
        /// Calls <see cref="AccountsRepository.GetTrustTransactionItemsView(IQueryable{TrustTransactionItem})"/> for all <see cref="TrustTransactionItem"/>s for a particular direction, filtered by a combination of selections.
        /// </summary>
        /// <param name="transactionDirectionType">The <see cref="Enums.TransactionDirectionTypeEnum"/> direction to get trust items for</param>
        /// <param name="settlementDate">The date of settlement / expected payment date to get items for. (Optional)</param>
        /// <param name="lenderList"><see cref="GeneralCustomEntities.GeneralCheckList"/> list of different lenders to show items for. (Optional)</param>
        /// <param name="summaryStatusList"><see cref="EntityCompacted"> of <see cref="TrustSummary"/>s to filter by. (Optional)</see></param>
        /// <param name="stateId">ID of a Matter's Office <see cref="StateIdEnum"/>. (Optional)</param>
        /// <param name="transactionTypeId">The <see cref="TransactionTypeEnum"/> to filter the result set. (Optional)</param>
        /// <param name="isSettled">A matter's settled status to filter by. (Optional)</param>
        /// <returns>IEnumerable of <see cref="AccountsCustomEntities.TrustTransactionItemView"/>s for <see cref="TrustTransactionItem"/>s.</returns>
        public IEnumerable<AccountsCustomEntities.TrustTransactionItemView> GetTrustTransactionItemsView(Enums.TransactionDirectionTypeEnum transactionDirectionType, DateTime? settlementDate, IEnumerable<GeneralCustomEntities.GeneralCheckList> lenderList, IEnumerable<EntityCompacted> summaryStatusList, int? stateId, int? transactionTypeId, bool? isSettled)
        {
            //Are any of the lenders or status items checked?
            if (lenderList.Where(l => l.IsChecked == true).Count() == 0 || summaryStatusList.Where(l => l.IsChecked == true).Count() == 0)
                return new List<AccountsCustomEntities.TrustTransactionItemView>();

            IQueryable<TrustTransactionItem> qry = context.TrustTransactionItems.AsNoTracking();
     
            qry = qry.Where(m => m.Matter.MatterStatusTypeId != (int)Enums.MatterStatusTypeEnum.NotProceeding);

            qry = qry.Where(m => m.TransactionDirectionTypeId == (int)transactionDirectionType);

            if (settlementDate.HasValue)
                qry = qry.Where(m => ((m.ExpectedPaymentDate.HasValue && m.ExpectedPaymentDate == settlementDate) ||
                                     (!m.ExpectedPaymentDate.HasValue && m.Matter.SettlementSchedule.SettlementDate == settlementDate)));

            //Lender List selections
            if (lenderList.Where(l => l.Id == -1 && l.IsChecked).Count() == 0)
            {
                //Must be some Lenders selected, but not the Select-All
                var llIst = lenderList.Where(l => l.IsChecked == true).Select(l => l.Id).ToList();

                qry = qry.Where(m => llIst.Contains((m.Matter.LenderId)));
            }

            if (stateId.HasValue)
                qry = qry.Where(m => m.Matter.StateId == stateId);

            if (transactionTypeId.HasValue)
                qry = qry.Where(m => m.TransactionTypeId == transactionTypeId);

            if (isSettled.HasValue)
                qry = qry.Where(m => m.Matter.Settled == isSettled);


            return GetTrustTransactionItemsView(qry);
        }


        public List<MatterCustomEntities.MatterGridHistoryView> GetGridHistoryForMatter(int matterId)
        {
            List<MatterCustomEntities.MatterGridHistoryView> grids = new List<MatterCustomEntities.MatterGridHistoryView>();
            List<int> snapshotMilestones = new List<int>() { (int)WFComponentEnum.ConsentMatterDetails, (int)WFComponentEnum.FundsReceived, (int)WFComponentEnum.PrepareSettlementInstr, (int)WFComponentEnum.QASettlementInstructions, (int)WFComponentEnum.RapidRefiPrepareSettlementInstructionsQA, (int)WFComponentEnum.PrepareSettlementInstrDisch, (int)WFComponentEnum.PrepareSettlementDischQA, (int)WFComponentEnum.PrepareSettlementPEXA };

            var comps = context.MatterWFLedgerItems.Where(m => snapshotMilestones.Contains(m.MatterWFComponent.WFComponentId) && m.MatterWFComponent.MatterId == matterId)
                .Select(m => new
                {
                    m.MatterWFComponentId,
                    m.MatterWFComponent.WFComponentId,
                    m.MatterWFComponent.WFComponent.WFComponentName,
                    m.User.Fullname,
                    m.UpdatedDate
                })
                .ToList()
                .GroupBy(m=>m.MatterWFComponentId);

            var mwfRep = new MatterWFRepository(context);

            foreach (var comp in comps)
            {
                var fundHistory = mwfRep.GetMatterFinFunding(comp.Key);
                var disbHistory = mwfRep.GetMatterFinDisbursements(comp.Key);
                var retHistory = mwfRep.GetMatterFinRetained(comp.Key);

                grids.Add(new MatterCustomEntities.MatterGridHistoryView()
                {
                    MatterWFComponentId = comp.Key,
                    WFComponentId = comp.First().WFComponentId,
                    WFComponentName = comp.First().WFComponentName,
                    RetainedItems = retHistory.ToList(),
                    FundingItems = fundHistory.ToList(),
                    DisbursementItems = disbHistory.ToList(),
                    UpdatedDate = comp.OrderByDescending(u=>u.UpdatedDate).FirstOrDefault().UpdatedDate,
                    UpdatedByUserName = comp.OrderByDescending(u => u.UpdatedDate).FirstOrDefault().Fullname,
                });
            }

            return grids.OrderByDescending(o=>o.UpdatedDate).ToList();
        }


        public List<string> CheckIfTrustTransactionItemsChanged(ref List<AccountsCustomEntities.TrustTransactionItemView> createList, ref bool refreshAndBreak)
        {
            List<string> errors = new List<string>();

            var clonedCreateList = createList.ToList();
            foreach (var item in clonedCreateList)
            {
                string errorStartString = $"- {item.MatterId} - {item.Amount.ToString("c")} - {item.Description}\n\t";
                var dbItem = context.TrustTransactionItems.FirstOrDefault(i => i.TrustTransactionItemId == item.TrustTransactionItemId);
                if (dbItem == null)
                {
                    errors.Add($"{errorStartString}Trust Transaction item no longer exists");
                    refreshAndBreak = true;
                    createList.Remove(item);
                }
                else if (item.ExpectedPaymentDate != dbItem.ExpectedPaymentDate)
                {
                    string oldDate = item.ExpectedPaymentDate.HasValue ? item.ExpectedPaymentDate.Value.ToString("dd/MMM") : "N/A";
                    string newDate = dbItem.ExpectedPaymentDate.HasValue ? dbItem.ExpectedPaymentDate.Value.ToString("dd/MMM") : "N/A";
                    errors.Add($"{errorStartString}Expected payment date has changed - changed from {oldDate} to {newDate} by {dbItem.User.Username}");
                    refreshAndBreak = true;
                    createList.Remove(item);
                }
                else if (item.SettlementDate != dbItem.Matter.SettlementSchedule?.SettlementDate)
                {
                    string oldDate = item.SettlementDate.HasValue ? item.SettlementDate.Value.ToString("dd/MMM") : "N/A";
                    string newDate = dbItem.Matter.SettlementSchedule != null ? dbItem.Matter.SettlementSchedule.SettlementDate.ToString("dd/MMM") : "N/A";
                    errors.Add($"{errorStartString}Settlement date has changed - changed from {oldDate} to {newDate} by {dbItem.User.Username}");
                    refreshAndBreak = true;
                    createList.Remove(item);
                }
                else if (item.Amount != dbItem.Amount)
                {
                    errors.Add($"{errorStartString}Amount has changed - changed from {item.Amount} to {dbItem.Amount} by {dbItem.User.Username}");
                    refreshAndBreak = true;
                    createList.Remove(item);
                }
                else if (item.PayableTypeId != dbItem.PayableTypeId)
                {
                    string oldPayableType = Enum.GetName(typeof(PayableTypeEnum), item.PayableTypeId);
                    string newPayableType = Enum.GetName(typeof(PayableTypeEnum), dbItem.PayableTypeId);
                    errors.Add($"{errorStartString}Payable type has changed - changed from {oldPayableType} to {newPayableType} by {dbItem.User.Username}");
                    refreshAndBreak = true;
                    createList.Remove(item);
                }
                else if (item.TrustTransactionStatusTypeId != dbItem.TrustTransactionStatusTypeId)
                {
                    errors.Add($"{errorStartString}Transaction status has changed - changed from {item.TrustTransactionStatusTypeName} to {dbItem.TrustTransactionStatusType.TrustTransactionStatusTypeName} by {dbItem.User.Username}");
                    refreshAndBreak = true;
                    createList.Remove(item);
                }
            }


            return errors;

        }

        public void FlagTrustTransactionItems(ref List<AccountsCustomEntities.TrustTransactionItemView> createList)
        {
            foreach(var item in createList)
            {
                var tti = context.TrustTransactionItems.FirstOrDefault(i => i.TrustTransactionItemId == item.TrustTransactionItemId);

                if (tti == null || tti.TrustSummaryId.HasValue) //deleted or already flagged! get outta here
                {
                    continue;
                }

                var tSummary = CreateFlaggedTrustSummaryForTrustTransactionItem(item); //a trust summary is the grouped item that appears on the reconciliation page, and where the receipt number is stored
                context.TrustSummaries.Add(tSummary); 
                context.SaveChanges();
                tti.TrustSummaryId = tSummary.TrustSummaryId;
                tti.TrustTransactionStatusTypeId = (int)TrustTransactionStatusTypeEnum.Flagged; //update the status and link tti to trust summary
                tti.UpdatedDate = DateTime.Now;
                tti.UpdatedByUserId = GlobalVars.CurrentUser.UserId;
                UpdateMLIFundsTransferred(item, (int)FundsTransferredTypeEnum.Flagged, tSummary.PaymentDate); //update all matter ledger items linked to this trust item
                context.SaveChanges();
            }
        }


        public void FlagTrustTransactionItemsGrouped(List<AccountsCustomEntities.TrustTransactionItemView> createList)
        {

            foreach(var item in createList)
            {
                if (item.TransactionTypeId == (int)TransactionTypeEnum.EFTFree)
                {
                    item.TransactionTypeId = (int)TransactionTypeEnum.EFT;
                }
                else if (item.TransactionTypeId == (int)TransactionTypeEnum.ChequeFree || 
                        item.TransactionTypeId == (int)TransactionTypeEnum.PPS || 
                        item.TransactionTypeId == (int)TransactionTypeEnum.PPSFree)
                {
                    item.TransactionTypeId = (int)TransactionTypeEnum.Cheque;
                }
                else if(item.TransactionTypeId == (int)TransactionTypeEnum.BPayFree)
                {
                    item.TransactionTypeId = (int)TransactionTypeEnum.BPayFree;
                }
            }

            if(createList.Select(x=>x.TransactionTypeId).Distinct().Count() > 1)
            {
                throw (new Exception("Multiple distinct transaction types attempting to be flagged as one item"));
            }

            int ttiId = createList.First().TrustTransactionItemId;

            var firstTti = context.TrustTransactionItems.FirstOrDefault(i => i.TrustTransactionItemId == ttiId);

            if (firstTti == null || firstTti.TrustSummaryId.HasValue) //deleted or already flagged! get outta here
            {
                return;
            }

            var tSummary = CreateFlaggedTrustSummaryForTrustTransactionItem(createList.First()); //a trust summary is the grouped item that appears on the reconciliation page, and where the receipt number is stored
            context.TrustSummaries.Add(tSummary);
            context.SaveChanges();

            foreach (var ttiView in createList)
            {
                var tti = context.TrustTransactionItems.FirstOrDefault(i => i.TrustTransactionItemId == ttiView.TrustTransactionItemId);
                tti.TrustSummaryId = tSummary.TrustSummaryId;
                tti.TrustTransactionStatusTypeId = (int)TrustTransactionStatusTypeEnum.Flagged; //update the status and link tti to trust summary
                tti.UpdatedDate = DateTime.Now;
                tti.UpdatedByUserId = GlobalVars.CurrentUser.UserId;
                UpdateMLIFundsTransferred(ttiView, (int)FundsTransferredTypeEnum.Flagged, tSummary.PaymentDate); //update all matter ledger items linked to this trust item
            }

            tSummary.Amount = createList.Sum(x => x.Amount);

            int numPayers = createList.Select(p => p.PayerPayee).Distinct().Count();
            if (numPayers > 1) //if there are multiple payers
            {
                tSummary.PayerPayee = numPayers + " " + createList.First().TransactionTypeName + " Payees";
            }



            context.SaveChanges();
            
        }




        public TrustSummary CreateFlaggedTrustSummaryForTrustTransactionItem(AccountsCustomEntities.TrustTransactionItemView tti)
        {
            TrustSummary tr = new TrustSummary();
            var tac = GetTrustAccountForMatter(tti.MatterId);
            int trustAccountId = 0;
            int stateId = 0;
            if (tac != null)
            {
                trustAccountId = tac.TrustAccountId;
                stateId = tac.State.StateId;
            }

            var newTrustSumId = context.TrustSummaries.AsNoTracking().Select(t => t.TrustSummaryId).OrderByDescending(t => t).FirstOrDefault() + 1;

            tr.TrustSummaryNo = $"- {newTrustSumId} Not Yet Reconciled - ";
            tr.TrustAccountId = trustAccountId;
            tr.TransactionDirectionTypeId = tti.TransactionDirectionTypeId;
            tr.TransactionTypeId = tti.TransactionTypeId;
            tr.EFT_AccountName = tti.EFT_AccountName;
            tr.EFT_BSB = tti.EFT_BSB;
            tr.EFT_AccountNo = tti.EFT_AccountNo;
            tr.EFT_Reference = tti.EFT_Reference;
            tr.StateId = stateId;
            tr.PaymentDate = GetCurrentTransactionDate(trustAccountId);
            tr.PayerPayee = tti.PayerPayee;
            tr.Amount = tti.Amount;
            tr.TrustSummaryStatusTypeId = (int)Slick_Domain.Enums.TrustSummaryStatusTypeEnum.Flagged;
            tr.Notes = "Flagged by Westpac Automation";
            tr.UpdatedDate = DateTime.Now;
            tr.UpdatedByUserId = GlobalVars.CurrentUser.UserId;
            return tr;
        }


        /// <summary>
        /// Calls <see cref="AccountsRepository.GetTrustTransactionItemsView(IQueryable{TrustTransactionItem})"/> for all <see cref="TrustTransactionItem"/>s for a <see cref="Matter"/> and a Checklist of <see cref="TrustTransactionStatusType"/>s.
        /// </summary>
        /// <param name="transactionDirectionTypeId">The <see cref="TransactionDirectionTypeEnum"/> to get transactions for.</param>
        /// <param name="matterId">The <see cref="Matter.MatterId"/> to get transactions for.</param>
        /// <param name="trustTransactionStatusList">The list of <see cref="TrustTransactionStatusTypeEnum"/>s to get transactions for.</param>
        /// <returns>IEnumerable of <see cref="AccountsCustomEntities.TrustTransactionItemView"/>s for <see cref="TrustTransactionItem"/>s.</returns>
        public IEnumerable<AccountsCustomEntities.TrustTransactionItemView> GetTrustTransactionItemsView(int transactionDirectionTypeId, int matterId, IEnumerable<GeneralCustomEntities.GeneralCheckList> trustTransactionStatusList)
        {
            //Are any of the transaction status items checked?
            if (trustTransactionStatusList.Where(l => l.IsChecked == true).Count() == 0)
                return new List<AccountsCustomEntities.TrustTransactionItemView>();


            IQueryable<TrustTransactionItem> qry = context.TrustTransactionItems.AsNoTracking();

            qry = qry.Where(t => t.TransactionDirectionTypeId == (int)transactionDirectionTypeId && t.MatterId == matterId && t.Matter.MatterStatusTypeId != (int)Enums.MatterStatusTypeEnum.NotProceeding);

            //Transaction Item Status List selections
            if (trustTransactionStatusList.Where(l => l.Id == -1 && l.IsChecked).Count() == 0)
            {
                //Must be some Transaction Item Status's selected, but not the Select-All
                var llIst = trustTransactionStatusList.Where(l => l.IsChecked == true).Select(l => l.Id).ToList();

                qry = qry.Where(t => llIst.Contains(t.TrustTransactionStatusTypeId));
            }
            return GetTrustTransactionItemsView(qry);
        }
        /// <summary>
        /// Gets a singular <see cref="AccountsCustomEntities.TrustTransactionItemView"/> for a <see cref="TrustTransactionItem"/>. 
        /// </summary>
        /// <param name="trustTransactionItemId">The <see cref="TrustTransactionItem.TrustTransactionItemId"/> of the <see cref="TrustTransactionItem"/> to get a view for</param>
        /// <returns>A single <see cref="AccountsCustomEntities.TrustTransactionItemView"/> for a <see cref="TrustTransactionItem"/></returns>
        public AccountsCustomEntities.TrustTransactionItemView GetTrustTransactionItemView(int trustTransactionItemId)
        {
            IQueryable<TrustTransactionItem> qry = context.TrustTransactionItems.AsNoTracking();

            qry = qry.Where(t => t.TrustTransactionItemId == trustTransactionItemId);
            return GetTrustTransactionItemsView(qry).FirstOrDefault();
        }
        /// <summary>
        /// Checks if the <see cref="TrustTransactionItem"/>s that are being FLAGGED will cause a <see cref="Matter"/> to be overdrawn, where the flagged money out is more than the flagged money in.
        /// </summary>
        /// <param name="items">IEnumerable of the <see cref="AccountsCustomEntities.TrustTransactionItemView"/>s that are being flagged to check.</param>
        /// <returns>If there are any warning messages raised by flagging this payment, a string of these.</returns>
        public string GetTrustTransactionFlagWarnings(IEnumerable<AccountsCustomEntities.TrustTransactionItemView> items)
        {
            var retVal = "";
           
            foreach (var itm in items.Select(m => new { m.TransactionDirectionTypeId, m.MatterId }).Distinct())
            {
                if (itm.TransactionDirectionTypeId == (int)Slick_Domain.Enums.TransactionDirectionTypeEnum.Payment)
                {
                    var trustTrans = GetTrustTransactionItemsView(itm.MatterId).ToList();

                    var flaggedDeposits = trustTrans.Where(t => t.TransactionDirectionTypeId == (int)TransactionDirectionTypeEnum.Deposit &&
                                        (t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Flagged ||
                                            t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Reconciled ||
                                            t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Journalled))
                                        .Select(t => t.Amount).DefaultIfEmpty(0).Sum();

                    var flaggedPayments = trustTrans.Where(t => t.TransactionDirectionTypeId == (int)TransactionDirectionTypeEnum.Payment &&
                                        (t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Flagged ||
                                            t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Reconciled ||
                                            t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Journalled))
                                        .Select(t => t.Amount).DefaultIfEmpty(0).Sum();

                    var flaggedAmt = items.Where(i => i.TransactionDirectionTypeId == itm.TransactionDirectionTypeId && i.MatterId == itm.MatterId &&
                                                        i.TrustTransactionStatusTypeId != (int)TrustTransactionStatusTypeEnum.Flagged &&
                                                        i.TrustTransactionStatusTypeId != (int)TrustTransactionStatusTypeEnum.Reconciled).Select(t => t.Amount).DefaultIfEmpty(0).Sum();

                    if (flaggedPayments + flaggedAmt > flaggedDeposits)
                        retVal += "Matter No " + itm.MatterId.ToString() + " will be OVERDRAWN if you Flag this Payment." + Environment.NewLine;
                }
            }
            

            return retVal;

        }
        //public string GetTrustTransactionUnFlagWarnings(IEnumerable<AccountsCustomEntities.TrustTransactionItemView> items)
        //{
        //    var retVal = "";
        //    using (var context = new SlickContext())
        //    {
        //        var aRep = new AccountsRepository(context);
        //        foreach (var itm in items.Select(m => new { m.TransactionDirectionTypeId, m.MatterId }).Distinct())
        //        {
        //            if (itm.TransactionDirectionTypeId == (int)Slick_Domain.Enums.TransactionDirectionTypeEnum.Deposit)
        //            {
        //                var trustTrans = aRep.GetTrustTransactionItemsView(itm.MatterId).ToList();

        //                var flaggedDeposits = trustTrans.Where(t => t.TransactionDirectionTypeId == (int)TransactionDirectionTypeEnum.Deposit &&
        //                                    (t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Flagged ||
        //                                     t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Reconciled))
        //                                    .Select(t => t.Amount).DefaultIfEmpty(0).Sum();

        //                var flaggedPayments = trustTrans.Where(t => t.TransactionDirectionTypeId == (int)TransactionDirectionTypeEnum.Payment &&
        //                                    (t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Flagged ||
        //                                     t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Reconciled))
        //                                    .Select(t => t.Amount).DefaultIfEmpty(0).Sum();

        //                var unflaggedAmt = items.Where(i => i.TransactionDirectionTypeId == itm.TransactionDirectionTypeId && i.MatterId == itm.MatterId).Select(t => t.Amount).DefaultIfEmpty(0).Sum();
        //                if (flaggedDeposits - flaggedPayments - unflaggedAmt < 0)
        //                    retVal += "Matter No " + itm.MatterId.ToString() + " will be OVERDRAWN if you UnFlag this Deposit." + Environment.NewLine;
        //            }
        //        }
        //    }

        //    return retVal;
        //}

        /// <summary>
        /// Checks if the <see cref="TrustTransactionItem"/>s that are being RECONCILED will cause a <see cref="Matter"/> to be overdrawn, where the flagged money out is more than the flagged money in.
        /// </summary>
        /// <param name="items">IEnumerable of the <see cref="AccountsCustomEntities.TrustTransactionItemView"/>s that are being reconciled to check.</param>
        /// <returns>If there are any warning messages raised by reconciling this payment, a string of these.</returns>
        public string GetTrustTransactionReconcileWarnings(IEnumerable<AccountsCustomEntities.TrustTransactionItemView> items)
        {
            var retVal = "";
            
            foreach (var itm in items.Select(m => new { m.TransactionDirectionTypeId, m.MatterId }).Distinct())
            {
                if (itm.TransactionDirectionTypeId == (int)Slick_Domain.Enums.TransactionDirectionTypeEnum.Payment)
                {
                    var trustTrans = GetTrustTransactionItemsView(itm.MatterId).ToList();


                    var reconDeposits = trustTrans.Where(t => t.TransactionDirectionTypeId == (int)TransactionDirectionTypeEnum.Deposit &&
                                            t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Reconciled)
                                            .Select(t => t.Amount).DefaultIfEmpty(0).Sum();

                    var reconPayments = trustTrans.Where(t => t.TransactionDirectionTypeId == (int)TransactionDirectionTypeEnum.Payment &&
                                            t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Reconciled )
                                            .Select(t => t.Amount).DefaultIfEmpty(0).Sum();

                    var reconAmt = items.Where(i => i.TransactionDirectionTypeId == itm.TransactionDirectionTypeId && i.MatterId == itm.MatterId && i.TrustTransactionStatusTypeId != (int)TrustTransactionStatusTypeEnum.Journalled).Select(t => t.Amount).DefaultIfEmpty(0).Sum();

                    if (reconPayments + reconAmt > reconDeposits)
                        retVal += "Matter No " + itm.MatterId.ToString() + " will be OVERDRAWN if you Reconcile this Payment." + Environment.NewLine;
                }
            }
            

            return retVal;
        }

        /// <summary>
        /// Checks if the <see cref="TrustTransactionItem"/>s that are being UNRECONCILED have already had their payment date fully reconciled, in which case it can not be unreconciled. 
        /// </summary>
        /// <param name="items">IEnumerable of the <see cref="AccountsCustomEntities.TrustTransactionItemView"/>s that are being unreconciled to check.</param>
        /// <returns>If there are any warning messages raised by unreconciling this payment, a string of these.</returns>
        public string GetTrustTransactionUnReconcileErrors(IEnumerable<AccountsCustomEntities.TrustTransactionItemView> items)
        {
            var retVal = "";
            
            foreach (var itm in items.Select(m => new { m.TrustSummaryId }).Distinct())
            {
                var ts = context.TrustSummaries.AsNoTracking().FirstOrDefault(t => t.TrustSummaryId == itm.TrustSummaryId);
                if (ts.TrustBalanceId.HasValue)
                    retVal += "This transaction is already part of a Daily Reconciliation. Cannot be Unreconciled";
            }
            

            return retVal;
        }

        /// <summary>
        /// Checks if the <see cref="TrustTransactionItem"/>s that are being UNRECONCILED will cause a <see cref="Matter"/> to be overdrawn, where the flagged money out is more than the flagged money in.
        /// </summary>
        /// <param name="items">IEnumerable of the <see cref="AccountsCustomEntities.TrustTransactionItemView"/>s that are being unreconciled to check.</param>
        /// <returns>If there are any warning messages raised by unreconciling this payment, a string of these.</returns>
        public string GetTrustTransactionUnReconcileWarnings(IEnumerable<AccountsCustomEntities.TrustTransactionItemView> items)
        {
            var retVal = "";
           
            foreach (var itm in items.Select(m => new { m.TransactionDirectionTypeId, m.MatterId }).Distinct())
            {
                if (itm.TransactionDirectionTypeId == (int)Slick_Domain.Enums.TransactionDirectionTypeEnum.Deposit)
                {
                    var trustTrans = GetTrustTransactionItemsView(itm.MatterId).ToList();

                    var reconDeposits = trustTrans.Where(t => t.TransactionDirectionTypeId == (int)TransactionDirectionTypeEnum.Deposit &&
                                            t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Reconciled)
                                            .Select(t => t.Amount).DefaultIfEmpty(0).Sum();

                    var reconPayments = trustTrans.Where(t => t.TransactionDirectionTypeId == (int)TransactionDirectionTypeEnum.Payment &&
                                            t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Reconciled)
                                            .Select(t => t.Amount).DefaultIfEmpty(0).Sum();

                    var reconAmt = items.Where(i => i.TransactionDirectionTypeId == itm.TransactionDirectionTypeId && i.MatterId == itm.MatterId).Select(t => t.Amount).DefaultIfEmpty(0).Sum();
                    if (reconPayments > (reconDeposits - reconAmt))
                        retVal += "Matter No " + itm.MatterId.ToString() + " will be OVERDRAWN if you UnReconcile this Deposit." + Environment.NewLine;
                }
            }
            

            return retVal;
        }

        /// <summary>
        /// Checks if the <see cref="TrustTransactionItem"/>s that are being JOURNALLED from one matter to another will cause either <see cref="Matter"/> to be overdrawn, where the flagged money out is more than the flagged money in.
        /// </summary>
        /// <param name="oldMTTI">The existing <see cref="TrustTransactionItem"/> that is to be journalled</param>
        /// <param name="newMatterId">The <see cref="Matter.MatterId"/> that the TrustTransactionItem is being journalled to.</param>
        /// <returns>If there are any warning messages raised by journalling this payment, a string of these.</returns>
        public string GetJournalTransactionWarnings(AccountsCustomEntities.TrustTransactionItemView oldMTTI, int newMatterId)
        {
            var retVal = "";

            // Check old matters
            if (oldMTTI.TransactionDirectionTypeId == (int)Slick_Domain.Enums.TransactionDirectionTypeEnum.Deposit)
            {
                var trustTrans = GetTrustTransactionItemsView(oldMTTI.MatterId).ToList();

                var reconDeposits = trustTrans.Where(t => t.TransactionDirectionTypeId == (int)TransactionDirectionTypeEnum.Deposit &&
                                        (t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Reconciled ||
                                            t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Journalled))
                                        .Select(t => t.Amount).DefaultIfEmpty(0).Sum();

                var reconPayments = trustTrans.Where(t => t.TransactionDirectionTypeId == (int)TransactionDirectionTypeEnum.Payment &&
                                        (t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Reconciled ||
                                            t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Journalled))
                                        .Select(t => t.Amount).DefaultIfEmpty(0).Sum();

                if (reconPayments > reconDeposits - oldMTTI.Amount)
                    retVal += "Matter No " + oldMTTI.MatterId.ToString() + " will be OVERDRAWN if you Journal this Deposit." + Environment.NewLine;
            }
            else
            {
                var trustTrans = GetTrustTransactionItemsView(newMatterId).ToList();

                var reconDeposits = trustTrans.Where(t => t.TransactionDirectionTypeId == (int)TransactionDirectionTypeEnum.Deposit &&
                                        t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Reconciled)
                                        .Select(t => t.Amount).DefaultIfEmpty(0).Sum();

                var reconPayments = trustTrans.Where(t => t.TransactionDirectionTypeId == (int)TransactionDirectionTypeEnum.Payment &&
                                        t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Reconciled)
                                        .Select(t => t.Amount).DefaultIfEmpty(0).Sum();

                if (reconPayments + oldMTTI.Amount > reconDeposits )
                    retVal += "Matter No " + newMatterId.ToString() + " will be OVERDRAWN if you Journal this Payment." + Environment.NewLine;
            }
            

            return retVal;

        }

        /// <summary>
        /// If valid (not a journalled or reversed item), remove a <see cref="TrustTransactionItem"/> from the database.
        /// </summary>
        /// <param name="trustTransactionId">The <see cref="TrustTransactionItem.TrustTransactionItemId"/> of the item to remove.</param>
        /// <returns>True if the item has been removed, False if it could not be removed.</returns>
        public bool RemoveTrustTransaction(int trustTransactionId)
        {
            var tti = context.TrustTransactionItems.FirstOrDefault(t => t.TrustTransactionItemId == trustTransactionId);

            if (tti.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Journalled ||
                tti.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Reversed)
            {               
                return false;
            }

            var mlittis = context.MatterLedgerItemTrustTransactionItems.Where(x => x.TrustTransactionItemId == tti.TrustTransactionItemId);
            foreach (var mlitti in mlittis)
            {
                //mark related ledger items as "unpaid"
                var mli = context.MatterLedgerItems.FirstOrDefault(m => m.MatterLedgerItemId == mlitti.MatterLedgerItemId);
                mli.FundsTransferredTypeId = (int)FundsTransferredTypeEnum.UnPaid;
                
            }
            context.MatterLedgerItemTrustTransactionItems.RemoveRange(mlittis);
            context.TrustTransactionItems.Remove(tti);

            return true;
        }

        #endregion


        #region Invoice Methods
        /// <summary>
        /// Gets <see cref="AccountsCustomEntities.InvoiceView"/>s for a query of <see cref="Invoice"/>s.
        /// </summary>
        /// <param name="invoices">IQueryable of <see cref="Invoice"/></param>
        /// <returns>IEnumerable of <see cref="AccountsCustomEntities.InvoiceView"/>s</returns>
        private IEnumerable<AccountsCustomEntities.InvoiceView> GetInvoiceViews(IQueryable<Invoice> invoices)
        {
            return invoices.Select(i => new
            {
                i.InvoiceId,
                i.PayableByTypeId,
                PayableByTypeName = i.PayableType.PayableTypeName,
                i.InvoiceNo,
                i.InvoiceRecipient,
                i.RecipientAddress,
                i.InvoiceDesc,
                i.InvoiceTotal,
                i.InvoiceGST,
                i.InvoiceSentDate,
                i.InvoicePaidDate,
                i.UpdatedDate,
                i.UpdatedByUserId,
                i.User.Username
            }).ToList()
            .Select(i => new AccountsCustomEntities.InvoiceView(i.InvoiceId, i.PayableByTypeId, i.PayableByTypeName, i.InvoiceNo, i.InvoiceRecipient, i.RecipientAddress, i.InvoiceDesc, i.InvoiceTotal, i.InvoiceGST, i.InvoiceSentDate,
                                                                i.InvoicePaidDate, i.UpdatedDate, i.UpdatedByUserId, i.Username))
             .ToList();                                                 
        }
        /// <summary>
        /// Gets <see cref="AccountsCustomEntities.InvoiceView"/>s for <see cref="Invoice"/>s optionally filtered by specific matter, specific sent status, and specific paid status.
        /// </summary>
        /// <param name="matterId">(OPTIONAL) If not null: <see cref="Matter.MatterId"/> </param>
        /// <param name="isSent">(OPTIONAL) If not null: Filter based on invoice sent / not sent.</param>
        /// <param name="isPaid">(OPTIONAL) If not null: Filter based on invoice paid / not paid.</param>
        /// <returns>IEnumerable of <see cref="AccountsCustomEntities.InvoiceView"/>s</returns>
        public IEnumerable<AccountsCustomEntities.InvoiceView> GetInvoiceViews(int? matterId, bool? isSent, bool? isPaid)
        {
            IQueryable<Invoice> invoices = context.Invoices.AsNoTracking();

            if (matterId.HasValue)
            {
                var iList = context.MatterLedgerItems.Where(m => m.MatterId == matterId.Value && m.InvoiceId != null).Select(i=> i.InvoiceId).Distinct();

                invoices = invoices.Where(i => iList.Contains(i.InvoiceId));

            }

            if (isSent.HasValue)
            {
                if (isSent.Value)
                    invoices = invoices.Where(i => i.InvoiceSentDate != null);
                else
                    invoices = invoices.Where(i => i.InvoiceSentDate == null);
            }

            if (isPaid.HasValue)
            {
                if (isPaid.Value)
                    invoices = invoices.Where(i => i.InvoicePaidDate != null);
                else
                    invoices = invoices.Where(i => i.InvoicePaidDate == null);
            }


            return GetInvoiceViews(invoices);
        }

        /// <summary>
        /// Get a single <see cref="AccountsCustomEntities.InvoiceView"/> for a <see cref="Invoice"/>
        /// </summary>
        /// <param name="invoiceId"><see cref="Invoice.InvoiceId"/> to get a view for.</param>
        /// <returns></returns>
        public AccountsCustomEntities.InvoiceView GetInvoiceView(int invoiceId)
        {
            IQueryable<Invoice> invoices = context.Invoices.AsNoTracking().Where(i => i.InvoiceId == invoiceId);

            return GetInvoiceViews(invoices).FirstOrDefault();
        }

        /// <summary>
        /// Gets <see cref="AccountsCustomEntities.InvoiceItemView"/>s for all items on an individual <see cref="Invoice"/>
        /// </summary>
        /// <param name="invoiceId">The <see cref="Invoice.InvoiceId"/> to get items for</param>
        /// <returns>IEnumerable of <see cref="AccountsCustomEntities.InvoiceItemView"/>s</returns>
        public IEnumerable<AccountsCustomEntities.InvoiceItemView> GetInvoiceItemsView(int invoiceId)
        {
            return context.MatterLedgerItems.Where(m => m.InvoiceId == invoiceId)
                .Select(m => new
                {
                    InvoiceId = m.InvoiceId.Value,
                    m.Invoice.InvoiceNo,
                    m.MatterLedgerItemId,
                    m.MatterId,
                    m.Matter.BusinessTypeId,
                    m.Matter.BusinessType.BusinessTypeName,
                    m.Matter.LoanTypeId,
                    m.Matter.LoanType.LoanTypeName,
                    m.Matter.MatterDescription,
                    m.LedgerItemSourceTypeId,
                    m.LedgerItemSourceType.LedgerItemSourceTypeName,
                    SettlementDate = (DateTime?)m.Matter.SettlementSchedule.SettlementDate,
                    m.Amount,
                    m.GST,
                    m.Description,
                    m.PayableByTypeId,
                    PayableByTypeName = m.PayableType.PayableTypeName,
                    m.PaymentNotes,
                    m.UpdatedDate,
                    m.UpdatedByUserId,
                    m.User.Username,
                    m.User.Lastname,
                    m.User.Firstname
                }).ToList()
                .Select(m => new AccountsCustomEntities.InvoiceItemView(m.InvoiceId, m.InvoiceNo, m.MatterLedgerItemId, m.MatterId, m.MatterDescription, m.LedgerItemSourceTypeId, 
                            m.LedgerItemSourceTypeName, m.SettlementDate, m.Amount, m.GST, m.Description, m.PayableByTypeId, m.PayableByTypeName, 
                            m.UpdatedDate, m.UpdatedByUserId, m.Username, Common.EntityHelper.GetFullName(m.Lastname, m.Firstname), m.BusinessTypeId, m.BusinessTypeName, m.LoanTypeId, m.LoanTypeName)
                ).ToList();
        }

        /// <summary>
        /// Generates potential <see cref="Invoice"/>s as <see cref="AccountsCustomEntities.InvoiceView"/>s. 
        /// </summary>
        /// <param name="lenderList"><see cref="GeneralCustomEntities.GeneralCheckList"/> of <see cref="Lender"/>s to filter by</param>
        /// <param name="mortMgrList"><see cref="GeneralCustomEntities.GeneralCheckList"/> of <see cref="MortMgr"/>s to filter by</param>
        /// <param name="stateId">(OPTIONAL NULLABLE) A Matter's Office <see cref="State.StateId"/>s to filter by</param>
        /// <param name="matterTypeId">(OPTIONAL NULLABLE) <see cref="MatterTypeEnum"/></param>
        /// <param name="dateFrom">(OPTIONAL NULLABLE) Start Payment / Settlement Date to filter by</param>
        /// <param name="dateTo">(OPTIONAL NULLABLE) End Payment / Settlement Date to filter by</param>
        /// <returns>IEnumerable of <see cref="AccountsCustomEntities.InvoiceItemView"/>s for the potential invoice items</returns>
        public IEnumerable<AccountsCustomEntities.InvoiceView> GenerateInvoices(IEnumerable<GeneralCustomEntities.GeneralCheckList> lenderList,
                                                                                                IEnumerable<GeneralCustomEntities.GeneralCheckList> mortMgrList,
                                                                                                int? stateId, int? matterTypeId,
                                                                                                DateTime? dateFrom, DateTime? dateTo, bool showSpecificInvoicesMatters)
        {

            //Are any of the lenders or MM's checked?
            if (lenderList.Where(l => l.IsChecked == true).Count() == 0 && mortMgrList.Where(l => l.IsChecked == true).Count() == 0)
                return null;

            //1 Get all the Matter Ledger Items for the criteria

            IQueryable<MatterLedgerItem> ledgerItems = context.MatterLedgerItems.AsNoTracking().Where(m => m.MatterLedgerItemStatusTypeId == (int)Enums.MatterLedgerItemStatusTypeEnum.Ready &&
                                                                                                            m.TransactionTypeId == (int)TransactionTypeEnum.Invoice);

            //some types of matter need their own invoice, we do that here
            if (!showSpecificInvoicesMatters)
            {
                ledgerItems = ledgerItems.Where(m => !(m.Matter.IsMQConversion || (m.Matter.Lender.LenderName.ToUpper().Contains("MYSTATE") && m.Matter.LoanTypeId == (int)LoanTypeEnum.Commercial)));
            }
            else
            {
                ledgerItems = ledgerItems.Where(m => m.Matter.IsMQConversion || (m.Matter.Lender.LenderName.ToUpper().Contains("MYSTATE") && m.Matter.LoanTypeId == (int)LoanTypeEnum.Commercial));
            }


            //Only selecting from matters which have settled and haven't been invoiced
            ledgerItems = ledgerItems.Where(m => !m.InvoiceId.HasValue &&
                                                 (m.Matter.Settled == true || m.Matter.MatterStatusTypeId == (int)Enums.MatterStatusTypeEnum.NotProceeding));

            //Lender List selections
            if (lenderList.Where(l => l.IsChecked == true).Count() > 0 && lenderList.Where(l => l.Id == -1 && l.IsChecked).Count() == 0)
            {
                //Must be some Lenders selected, but not the Select-All
                var llIst = lenderList.Where(l => l.IsChecked == true).Select(l => l.Id).ToList();

                ledgerItems = ledgerItems.Where(m => llIst.Contains((m.Matter.LenderId)));

            }

            //Mortgage Mgr selections
            if (mortMgrList.Where(l => l.IsChecked == true).Count() > 0 && mortMgrList.Where(l => l.Id == -1 && l.IsChecked).Count() == 0)
            {
                //Must be some Lenders selected, but not the Select-All
                var mmList = mortMgrList.Where(l => l.IsChecked == true).Select(l => l.Id).ToList();

                //Mortgage Mgr can be null. If the "include null" option is selected (value=0) need to include in the where
                if (mortMgrList.Where(l => l.Id == 0 && l.IsChecked).Count() == 1)
                    ledgerItems = ledgerItems.Where(m => mmList.Contains((m.Matter.MortMgrId.Value)) || !m.Matter.MortMgrId.HasValue);
                else
                    ledgerItems = ledgerItems.Where(m => mmList.Contains((m.Matter.MortMgrId.Value)));
            }
            if (matterTypeId.HasValue)
                ledgerItems = ledgerItems.Where(m => m.Matter.MatterGroupTypeId == matterTypeId.Value);

            if (stateId.HasValue)
                ledgerItems = ledgerItems.Where(m => m.Matter.StateId == stateId);

            if (dateFrom.HasValue)
                ledgerItems = ledgerItems.Where(m => m.Matter.SettlementSchedule.SettlementDate >= dateFrom ||
                                                        (m.Matter.MatterStatusTypeId == (int)Enums.MatterStatusTypeEnum.NotProceeding && m.Matter.UpdatedDate >= dateFrom));

            if (dateTo.HasValue)
                ledgerItems = ledgerItems.Where(m => m.Matter.SettlementSchedule.SettlementDate <= dateTo ||
                                                        (m.Matter.MatterStatusTypeId == (int)Enums.MatterStatusTypeEnum.NotProceeding && m.Matter.UpdatedDate <= dateTo));

            IEnumerable<AccountsCustomEntities.MatterLedgerItemView> invItemsList = GetMatterLedgersView(ledgerItems);

            //Now Build list of invoices based on these items
            List<AccountsCustomEntities.InvoiceView> invList = new List<AccountsCustomEntities.InvoiceView>();
            AccountsCustomEntities.InvoiceView iv;
            int fakeId = 1;     //Give these invoices a fakeId, so I can locate them in the list later on
            //Mortgage manager
            var tmpList = invItemsList.Where(m => m.PayableByTypeId == (int)PayableTypeEnum.MortMgr && m.MortMgrId.HasValue).Select(m => m.MortMgrId.Value).Distinct();
            foreach (int mmId in tmpList)
            {
                iv = new AccountsCustomEntities.InvoiceView();
                iv.InvoiceId = fakeId++;
                iv.PayableByTypeId = (int)PayableTypeEnum.MortMgr;
                iv.PayableByTypeName = "MortMgr";
                iv.InvoiceDesc = "Payable by Mortgage Manager";

                var mm = context.MortMgrs.Where(m => m.MortMgrId == mmId).Select(m=> new {m.MortMgrId, m.MortMgrName, m.MortMgrNameShort, m.StreetAddress, m.Suburb, m.State.StateName, m.PostCode }).FirstOrDefault();

                iv.InvoiceRecipient = mm.MortMgrName;
                iv.RecipientAddress = mm.StreetAddress;
                if (!string.IsNullOrEmpty(mm.Suburb) || !string.IsNullOrEmpty(mm.StateName) || !string.IsNullOrEmpty(mm.PostCode))
                    iv.RecipientAddress += "\n";
                iv.RecipientAddress += mm.Suburb + " " + mm.StateName + " " + mm.PostCode;
                iv.UpdatedDate = DateTime.Now;
                iv.UpdatedByUserId = GlobalVars.CurrentUser.UserId;
                iv.UpdatedByUsername = GlobalVars.CurrentUser.Username;

                iv.InvoiceItems = new List<AccountsCustomEntities.InvoiceItemView>();
                var itemsList = invItemsList.Where(m => m.PayableByTypeId == (int)PayableTypeEnum.MortMgr && m.MortMgrId == mmId).OrderBy(o => o.SettlementDate);
                if (dateTo.HasValue)
                    iv.InvoiceNo = CreateInvoiceNo(mm.MortMgrNameShort, dateTo.Value);
                else
                    iv.InvoiceNo = CreateInvoiceNo(mm.MortMgrNameShort, itemsList.Last().SettlementDate.Value);

                foreach (var item in itemsList)
                {
                    var iiv = new AccountsCustomEntities.InvoiceItemView();
                    iiv.MatterLedgerItemId = item.MatterLedgerItemId;
                    iiv.MatterId = item.MatterId;
                    iiv.MatterDescription = item.MatterDescription;
                    iiv.LedgerItemSourceTypeId = item.LedgerItemSourceTypeId;
                    iiv.LedgerItemSourceTypeName = item.LedgerItemSourceTypeName;
                    iiv.SettlementDate = item.SettlementDate;
                    iiv.Amount = item.Amount;
                    iiv.GST = item.GST;
                    iiv.Description = item.Description;
                    iiv.PayableByTypeId = item.PayableByTypeId;
                    iiv.PayableByTypeName = item.PayableByTypeName;
                    iiv.UpdatedDate = item.UpdatedDate;
                    iiv.UpdatedByUserId = item.UpdatedByUserId;
                    iiv.UpdatedByUsername = item.UpdatedByUsername;

                    iv.InvoiceItems.Add(iiv);
                }
                iv.ItemCount = iv.InvoiceItems.Count();
                iv.InvoiceTotal = iv.InvoiceItems.Sum(m => m.Amount);
                iv.InvoiceGST = iv.InvoiceItems.Sum(m => m.GST);

                invList.Add(iv);
            }

            //Lender manager
            tmpList = invItemsList.Where(m => m.PayableByTypeId == (int)PayableTypeEnum.Lender).Select(m => m.LenderId).Distinct();
            foreach (int lendId in tmpList)
            {
                iv = new AccountsCustomEntities.InvoiceView();
                iv.PayableByTypeId = (int)PayableTypeEnum.Lender;
                iv.PayableByTypeName = "Lender";
                iv.InvoiceDesc = "Payable by Lender";

                var ldr = context.Lenders.Where(l =>l.LenderId == lendId).Select(l => new { l.LenderId, l.LenderName, l.LenderNameShort, l.StreetAddress, l.Suburb, l.State.StateName, l.PostCode }).FirstOrDefault();

                iv.InvoiceRecipient = ldr.LenderName;
                iv.RecipientAddress = ldr.StreetAddress;
                if (!string.IsNullOrEmpty(ldr.Suburb) || !string.IsNullOrEmpty(ldr.StateName) || !string.IsNullOrEmpty(ldr.PostCode))
                    iv.RecipientAddress += "\n";
                iv.RecipientAddress += ldr.Suburb + " " + ldr.StateName + " " + ldr.PostCode;
                iv.UpdatedDate = DateTime.Now;
                iv.UpdatedByUserId = GlobalVars.CurrentUser.UserId;
                iv.UpdatedByUsername = GlobalVars.CurrentUser.Username;

                iv.InvoiceItems = new List<AccountsCustomEntities.InvoiceItemView>();
                var itemsList = invItemsList.Where(m => m.PayableByTypeId == (int)PayableTypeEnum.Lender && m.LenderId==lendId).OrderBy(o=> o.SettlementDate);
                if (dateTo.HasValue)
                    iv.InvoiceNo = CreateInvoiceNo(ldr.LenderNameShort, dateTo.Value);
                else
                    iv.InvoiceNo = CreateInvoiceNo(ldr.LenderNameShort, itemsList.Last().SettlementDate.Value);
                foreach (var item in itemsList)
                {
                    var iiv = new AccountsCustomEntities.InvoiceItemView();
                    iiv.MatterLedgerItemId = item.MatterLedgerItemId;
                    iiv.MatterId = item.MatterId;
                    iiv.MatterDescription = item.MatterDescription;
                    iiv.LedgerItemSourceTypeId = item.LedgerItemSourceTypeId;
                    iiv.LedgerItemSourceTypeName = item.LedgerItemSourceTypeName;
                    iiv.SettlementDate = item.SettlementDate;
                    iiv.Amount = item.Amount;
                    iiv.GST = item.GST;
                    iiv.Description = item.Description;
                    iiv.PayableByTypeId = item.PayableByTypeId;
                    iiv.PayableByTypeName = item.PayableByTypeName;
                    iiv.UpdatedDate = item.UpdatedDate;
                    iiv.UpdatedByUserId = item.UpdatedByUserId;
                    iiv.UpdatedByUsername = item.UpdatedByUsername;

                    iv.InvoiceItems.Add(iiv);
                }

                iv.ItemCount = iv.InvoiceItems.Count();
                iv.InvoiceTotal = iv.InvoiceItems.Sum(m => m.Amount);
                iv.InvoiceGST = iv.InvoiceItems.Sum(m => m.GST);



                invList.Add(iv);
            }


            return invList;
        }

        /// <summary>
        /// Creates the name of a new <see cref="Invoice"/> by coming a prefix and the month and year of the invoice date. Checks if this is a duplicate name, and if so adds a copy number
        /// </summary>
        /// <param name="prefix">The prefix to use for the invoice - dependent on the <see cref="Lender"/> / <see cref="MortMgr"/> being invoiced.</param>
        /// <param name="invDate">The date that this <see cref="Invoice"/> is for.</param>
        /// <returns>String of the name of the new invoice.</returns>
        private string CreateInvoiceNo(string prefix, DateTime invDate)
        {
            string baseInvNo = prefix + invDate.ToString("-MMM-yy");
            string tmpInvNo = baseInvNo;

            int tmpInc = 1;
            while (context.Invoices.Any(i => i.InvoiceNo == tmpInvNo))
            {
                tmpInc++;
                tmpInvNo = baseInvNo + " (" + tmpInc.ToString() + ")";
            }

            return tmpInvNo;
        }

        /// <summary>
        /// Checks if an invoice's name is unique.
        /// </summary>
        /// <param name="invoiceId">(OPTIONAL / NULLABLE) If updating an existing Invoice, ignore the <see cref="Invoice"/> being updated.</param>
        /// <param name="invoiceNo">The name of the invoice to confirm is unique.</param>
        /// <returns>
        /// True if Unique, False if Non-Unique.
        /// </returns>
        public Boolean InvoiceNoUnique(Int32? invoiceId, String invoiceNo)
        {
            try
            {
                var invcs = context.Invoices.Where(i=>i.InvoiceNo==invoiceNo);
                if (invoiceId.HasValue)
                    invcs = invcs.Where(i =>i.InvoiceId != invoiceId.Value);

                Invoice inv = invcs.FirstOrDefault();
                if (inv != null)
                    return false;
                else
                    return true;

            }
            catch (Exception ex)
            {
                Handlers.ErrorHandler.LogError(ex);
                return false;
            }

        }

        #endregion

        //OBSOLETE TRUST METHODS (hopefully)
        #region Old Trust Methods

        /// <summary>
        /// Gets a <see cref="AccountsCustomEntities.StateTrustAccountList"/> of trust accounts for each state in the system.
        /// </summary>
        /// <returns>IEnumerable of <see cref="AccountsCustomEntities.StateTrustAccountList"/>s for each state's trust account/s</returns>
        public IEnumerable<AccountsCustomEntities.StateTrustAccountList> GetStateTrustAccList()
        {
            var accLst = context.TrustAccounts.Select(a => new { a.TrustAccountId, a.State.StateName, a.AccountName }).OrderBy(o => o.StateName).ToList()
                            .Select(t => new AccountsCustomEntities.StateTrustAccountList(t.TrustAccountId, t.StateName, t.AccountName)).ToList();
            return accLst;
        }

        public int? MarkBorrowPaymentAsReceived(AccountsCustomEntities.MatterLedgerItemView mliV, bool IsReceived)
        {
            try
            {
                if (IsReceived)
                {
                    //Create an "borrower" invoice for the item
                    if (!mliV.InvoiceId.HasValue)
                    {
                        Matter mat = context.Matters.AsNoTracking().FirstOrDefault(m => m.MatterId == mliV.MatterId);
                        Invoice inv = new Invoice();
                        inv.PayableByTypeId = (int)PayableTypeEnum.Borrower;
                        inv.InvoiceNo = "BRW-" + mliV.MatterId.ToString() + "-" + mliV.MatterLedgerItemId.ToString();
                        inv.InvoiceRecipient = mat.MatterDescription;
                        inv.InvoiceDesc = "Borrower Payment Invoice";
                        inv.InvoiceTotal = mliV.Amount;
                        inv.InvoiceGST = mliV.GST;
                        inv.InvoiceSentDate = DateTime.Now;
                        inv.UpdatedDate = DateTime.Now;
                        inv.UpdatedByUserId = GlobalVars.CurrentUser.UserId;
                        context.Invoices.Add(inv);
                        context.SaveChanges();

                        MatterLedgerItem mli = context.MatterLedgerItems.FirstOrDefault(m => m.MatterLedgerItemId == mliV.MatterLedgerItemId);
                        mli.InvoiceId = inv.InvoiceId;
                        context.SaveChanges();

                        mliV.InvoiceId = inv.InvoiceId;
                        mliV.InvoiceSentDate = inv.InvoiceSentDate;
                        mliV.Invoiced = "Yes";

                        return inv.InvoiceId;
                    }
                }
                else
                {
                    //Remove borrower invoice
                    if (mliV.InvoiceId.HasValue)
                    {
                        //Is invoice reconciled?
                        Invoice inv = context.Invoices.FirstOrDefault(i => i.InvoiceId == mliV.InvoiceId);
                        if (inv.InvoicePaidDate.HasValue)
                            return null;
                        else
                        {
                            //Make sure it's the only item with this invoice Id, and if so, remove the invoice
                            if (context.MatterLedgerItems.Where(m => m.InvoiceId == mliV.InvoiceId && m.MatterLedgerItemId != mliV.MatterLedgerItemId).Count() > 0)
                                return null;

                            MatterLedgerItem mli = context.MatterLedgerItems.FirstOrDefault(m => m.MatterLedgerItemId == mliV.MatterLedgerItemId);
                            mli.InvoiceId = null;

                            context.Invoices.Remove(inv);
                            context.SaveChanges();

                            return mliV.InvoiceId.Value;
                        }
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                throw (ex);
            }
        }

        public bool MarkBorrowPaymentAsReconciled(AccountsCustomEntities.MatterLedgerItemView mliV, bool IsReconciled)
        {
            try
            {
                Invoice inv = context.Invoices.FirstOrDefault(i => i.InvoiceId == mliV.InvoiceId);

                if (IsReconciled)
                {
                    if (inv.InvoicePaidDate.HasValue)
                        return false;
                    else
                        inv.InvoicePaidDate = DateTime.Now;
                }
                else
                {
                    if (!inv.InvoicePaidDate.HasValue)
                        return false;
                    else
                        inv.InvoicePaidDate = null;
                }
                inv.UpdatedDate = DateTime.Now;
                inv.UpdatedByUserId = GlobalVars.CurrentUser.UserId;
                context.SaveChanges();

                return true;
            }
            catch (Exception ex)
            {
                throw (ex);
            }
        }



        #endregion


        #region Trust Account Methods
        /// <summary>
        /// Get the list of <see cref="AccountsCustomEntities.TrustAccountTransactionDateView"/>s for all <see cref="TrustAccount"/>s in Slick with their current active reconciliation dates.
        /// </summary>
        /// <returns><see cref="AccountsCustomEntities.TrustAccountTransactionDateView"/>s for all Slick Trust accounts.</returns>
        public IEnumerable<AccountsCustomEntities.TrustAccountTransactionDateView> GetTransactionDateList()
        {
            return context.TrustAccounts.Where(t => t.Enabled)
                .Select(a => new
                {
                    a.TrustAccountId,
                    a.AccountName,
                    a.Bank,
                    a.State.StateName,
                    ttdc = a.TrustAccountTransactionDates.FirstOrDefault(t => t.IsCurrent),
                    ttdh = a.TrustAccountTransactionDates.OrderByDescending(t => t.TrustAccountTransactionDateId)
                })
                .Select(b => new
                {
                    b.TrustAccountId,
                    b.AccountName,
                    b.Bank,
                    b.StateName,
                    TrustAccountTransactionDateId = (int?)b.ttdc.TrustAccountTransactionDateId,
                    TransactionDate = (DateTime?)b.ttdc.TransactionDate,
                    UpdatedDate = (DateTime?)b.ttdc.UpdatedDate,
                    UpdatedByUserId = (int?)b.ttdc.UpdatedByUserId,
                    b.ttdc.User.Username,
                    TransactionDateHistory = b.ttdh
                        .Select(h => new AccountsCustomEntities.TrustAccountTransactionDateView()
                        {
                            TrustAccountTransactionDateId = h.TrustAccountTransactionDateId,
                            TrustAccountId = h.TrustAccountId,
                            TrustAccountName = h.TrustAccount.AccountName,
                            TrustAccountStateName = h.TrustAccount.State.StateName,
                            TransactionDate = h.TransactionDate,
                            Notes = h.Notes,
                            UpdatedDate = h.UpdatedDate,
                            UpdatedByUserId = h.UpdatedByUserId,
                            UpdatedByUsername = h.User.Username
                        }).ToList()
                })
                .ToList()
                .Select(t => new AccountsCustomEntities.TrustAccountTransactionDateView(t.TrustAccountTransactionDateId, t.TrustAccountId, t.AccountName,
                    t.StateName, t.Bank, t.TransactionDate, t.UpdatedDate, t.UpdatedByUserId, t.Username, t.TransactionDateHistory))
                    .ToList();

        }

        /// <summary>
        /// Generates <see cref="AccountsCustomEntities.TrustAccountView"/>s for a query of trust accounts. 
        /// </summary>
        /// <param name="trustAccountQry">Query of Trust Accounts to get views for</param>
        /// <returns>IEnumerable of <see cref="AccountsCustomEntities.TrustAccountView"/>s for the specified <see cref="TrustAccount"/>/s</returns>
        public IEnumerable<AccountsCustomEntities.TrustAccountView> GetTrustAccountViews(IQueryable<TrustAccount> trustAccountQry)
        {
            return
                trustAccountQry.Select(t => new
                {
                    t.TrustAccountId,
                    t.Bank,
                    t.AccountName,
                    t.BSB,
                    t.AccountNo,
                    t.BankStreetAddress,
                    t.BankSuburb,
                    t.BankStateId,
                    BankStateName = t.State.StateName,
                    t.BankPostCode,
                    t.Attention,
                    t.Fax,
                    t.BalanceStartDate,
                    t.OpeningBalance,
                    t.Notes,
                    t.Enabled,
                    t.UpdatedDate,
                    t.UpdatedByUserId,
                    t.User.Username
                }).ToList()
                .Select(t => new AccountsCustomEntities.TrustAccountView(t.TrustAccountId, t.Bank, t.AccountName, t.BSB, t.AccountNo, t.BankStreetAddress,
                            t.BankSuburb, t.BankStateId, t.BankStateName, t.BankPostCode, t.Attention, t.Fax, t.BalanceStartDate, t.OpeningBalance, t.Notes, t.Enabled, t.UpdatedDate, t.UpdatedByUserId, t.Username))
                .ToList();
        }

        /// <summary>
        /// Gets <see cref="AccountsCustomEntities.TrustAccountView"/>s for all <see cref="TrustAccount"/>s in the Slick system.
        /// </summary>
        /// <returns>IEnumerable of <see cref="AccountsCustomEntities.TrustAccountView"/>s for all <see cref="TrustAccount"/>s</returns>
        public IEnumerable<AccountsCustomEntities.TrustAccountView> GetTrustAccountViews()
        {
            IQueryable<TrustAccount> trustAccountQry = context.TrustAccounts;
            return GetTrustAccountViews(trustAccountQry);
        }

        /// <summary>
        /// Gets a <see cref="AccountsCustomEntities.TrustAccountView"/> for a <see cref="TrustAccount"/>
        /// </summary>
        /// <param name="trustAccountId">The <see cref="TrustAccount.TrustAccountId"/> of the account to generate a view for.</param>
        /// <returns><see cref="AccountsCustomEntities.TrustAccountView"/></returns>
        public AccountsCustomEntities.TrustAccountView GetTrustAccountView(int trustAccountId)
        {
            var qry = context.TrustAccounts.Where(t => t.TrustAccountId == trustAccountId);
            return GetTrustAccountViews(qry).FirstOrDefault();
        }

       
        /// <summary>
        /// Gets <see cref="AccountsCustomEntities.TrustAccountView"/>s for all <see cref="TrustAccount"/>s of a given Enabled stats in the Slick system.
        /// </summary>
        /// <param name="pEnabled">Filter to an enabled status</param>
        /// <returns>IEnumerable of <see cref="AccountsCustomEntities.TrustAccountView"/>s for all <see cref="TrustAccount"/>s</returns>
        public IEnumerable<AccountsCustomEntities.TrustAccountView> GetTrustAccountViews(bool pEnabled)
        {
            IQueryable<TrustAccount> trustAccountQry = context.TrustAccounts.Where(t => t.Enabled == pEnabled);
            return GetTrustAccountViews(trustAccountQry);
        }

        /// <summary>
        /// Class used for filtering in selecting best match <see cref="TrustAccount"/> for a particular Lender / State combinations. 
        /// </summary>
        private class tmpAccounts
        {
            public int? StateId { get; set; }
            public int? LenderId { get; set; }
            public int TrustAccountId { get; set; }
            public int ranking { get; set; }
        }
        /// <summary>
        /// Matches the best fit trust account for a Lender / State combination.
        /// </summary>
        /// <param name="stateId">(NULLABLE, OPTIONAL) <see cref="StateIdEnum"/> to filter with.</param>
        /// <param name="lenderId">(NULLABLE, OPTIONAL) <see cref="Lender.LenderId"/> to filter with.</param>
        /// <returns>The <see cref="TrustAccount"/> that was matched</returns>
        public TrustAccount GetTrustAccountForStateLender(int? stateId, int? lenderId)
        {
            List<tmpAccounts> tacs = new List<tmpAccounts>();

            tacs = context.TrustAccountLookups.Select(t => new tmpAccounts()
            {
                StateId = t.StateId,
                LenderId = t.LenderId,
                TrustAccountId = t.TrustAccountId,
                ranking = 0
            }).ToList();

            //Clear out obvious non-matches and increment ranking for exact matches
            if (stateId.HasValue)
            {
                tacs.RemoveAll(t => t.StateId.HasValue && t.StateId != stateId);

                var st_upds = tacs.Where(t => t.StateId == stateId);
                foreach (var upd in st_upds)
                    upd.ranking += 5;
            }

            if (lenderId.HasValue)
            {
                tacs.RemoveAll(t => t.LenderId.HasValue && t.LenderId != lenderId);

                var ld_upds = tacs.Where(t => t.LenderId == lenderId);
                foreach (var upd in ld_upds)
                    upd.ranking += 5;
            }

            //Add 1 point for "null match
            var upds = tacs.Where(t => !t.StateId.HasValue);
            foreach (var upd in upds)
                upd.ranking += 1;

            upds = tacs.Where(t => !t.LenderId.HasValue);
            foreach (var upd in upds)
                upd.ranking += 1;

            int tacId = tacs.OrderByDescending(o => o.ranking).FirstOrDefault().TrustAccountId;

            return context.TrustAccounts.FirstOrDefault(t => t.TrustAccountId == tacId);

        }
        /// <summary>
        /// Obtains the correct <see cref="TrustAccount"/> to use for a <see cref="Matter"/>
        /// </summary>
        /// <param name="matterId"><see cref="Matter.MatterId"/></param>
        /// <returns><see cref="TrustAccount"/> for the matter's State and Lender combination</returns>
        public TrustAccount GetTrustAccountForMatter(int matterId)
        {
            //Need office and lender to get trust account

            var mat = context.Matters.FirstOrDefault(m => m.MatterId == matterId);

            return GetTrustAccountForStateLender(mat.StateId, mat.LenderId);

        }

        /// <summary>
        /// Gets all enabled <see cref="TrustAccount"/>s as <see cref="EntityCompacted"/>s
        /// </summary>
        /// <returns><see cref="EntityCompacted"/> with ID of <see cref="TrustAccount.TrustAccountId"/> and Details of <see cref="TrustAccount.AccountName"/></returns>
        public List<EntityCompacted> GetTrustAccountECList()
        {
            List<EntityCompacted> tmp = new List<EntityCompacted>();
            var tacs = context.TrustAccounts.Where(t => t.Enabled);

            foreach (var tac in tacs)
                tmp.Add(new EntityCompacted(tac.TrustAccountId, tac.AccountName, false));

            return tmp;
        }

        #endregion
   

        #region Trust Summary Methods
        
        /// <summary>
        /// Gets <see cref="AccountsCustomEntities.TrustSummaryView"/>s for a query of <see cref="TrustSummary"/>/s
        /// </summary>
        /// <param name="trustSummaryQry">Query of <see cref="TrustSummary"/> to generate views for</param>
        /// <returns>IEnumerable of <see cref="AccountsCustomEntities.TrustSummaryView"/>s</returns>
        private IEnumerable<AccountsCustomEntities.TrustSummaryView> GetTrustSummaryViews(IQueryable<TrustSummary> trustSummaryQry)
        {
            return trustSummaryQry.Select(t => new
            {
                t.TrustSummaryId,
                t.TrustAccountId,
                t.TrustAccount.AccountName,
                t.TransactionDirectionTypeId,
                t.TransactionDirectionType.TransactionDirectionTypeName,
                t.TransactionTypeId,
                t.TransactionType.TransactionTypeName,
                t.TransactionType.ReportDisplayName,
                t.EFT_AccountName,
                t.EFT_BSB,
                t.EFT_AccountNo,
                t.EFT_Reference,
                t.StateId,
                t.State.StateName,
                t.TrustSummaryNo,
                t.PaymentDate,
                t.PayerPayee,
                t.Amount,
                t.TrustSummaryStatusTypeId,
                t.TrustSummaryStatusType.TrustSummaryStatusTypeName,
                t.TrustSummaryStatusType.BackgroundColor,
                
                t.SummaryLockedDate,
                t.SummaryLockedByUserId,
                SummaryLockedByUsername = t.User1.Username,
                t.SummaryReconciledDate,
                t.SummaryReconciledByUserId,
                SummaryReconciledByUsername = t.User2.Username,
                t.SummaryReversedDate,
                t.SummaryReversedByUserId,
                SummaryReversedByUsername = t.User3.Username,
                t.Notes,
                t.TrustBalanceId,
                BalanceReconciled = (bool?)t.TrustBalance.Reconciled,
                TransactionItems = t.TrustTransactionItems.Where(m => m.TrustTransactionStatusTypeId != (int)TrustTransactionStatusTypeEnum.Journalled)
                            .Select(m => new {m.TrustTransactionItemId, m.MatterId, m.Matter.MatterDescription, m.Matter.LenderId, m.Matter.Lender.LenderName,
                                    m.Matter.LenderRefNo, m.Matter.SecondaryRefNo, m.Matter.Lender.SecondaryRefName, m.Matter.StateId, m.Matter.State.StateName, SettlementDate = (DateTime?)m.Matter.SettlementSchedule.SettlementDate, m.Matter.Settled,
                                    m.TransactionTypeId, m.TransactionType.TransactionTypeName, m.TransactionType.ReportDisplayName, m.PayableTypeId, m.PayableOthersDesc, m.EFT_AccountName,m.EFT_BSB,m.EFT_AccountNo,m.EFT_Reference,
                                    m.PayerPayee, m.Amount, m.Description, m.Notes, m.ExpectedPaymentDate, m.PaymentPrepared,
                                    m.TrustTransactionStatusTypeId, m.TrustTransactionStatusType.TrustTransactionStatusTypeName,
                                    TrustTransactionStatusBackgroundColour = m.TrustTransactionStatusType.BackgroundColour, m.TrustTransactionJournalId
                                     }).ToList(),
                t.UpdatedDate,
                t.UpdatedByUserId,
                t.User.Username
            })
            .ToList()
            .Select(t => new AccountsCustomEntities.TrustSummaryView(t.TrustSummaryId, t.TrustAccountId, t.AccountName, t.TransactionDirectionTypeId, t.TransactionDirectionTypeName,
                                t.TransactionTypeId, t.TransactionTypeName, t.EFT_AccountName, t.EFT_BSB, t.EFT_AccountNo, t.EFT_Reference, t.StateId, t.StateName, t.PaymentDate,
                                t.TrustSummaryNo, 
                                t.PayerPayee, t.Amount, t.TrustSummaryStatusTypeId, t.TrustSummaryStatusTypeName, t.BackgroundColor,
                                t.SummaryLockedDate, t.SummaryLockedByUserId, t.SummaryLockedByUsername,
                                t.SummaryReconciledDate, t.SummaryReconciledByUserId, t.SummaryReconciledByUsername,
                                t.SummaryReversedDate, t.SummaryReversedByUserId, t.SummaryReversedByUsername,
                                t.Notes, t.TrustBalanceId, t.BalanceReconciled,
                                t.TransactionItems.Select(m => new AccountsCustomEntities.TrustTransactionItemView(m.MatterId, m.MatterDescription, 
                                    m.LenderId, m.LenderName, m.LenderRefNo, m.SecondaryRefNo, m.SecondaryRefName, m.TrustTransactionItemId, t.TrustSummaryId, t.TransactionDirectionTypeId, 
                                    t.TransactionDirectionTypeName, m.TransactionTypeId, m.TransactionTypeName, m.ReportDisplayName, m.PayableTypeId, m.PayableOthersDesc, 
                                    m.EFT_AccountName, m.EFT_BSB, m.EFT_AccountNo, m.EFT_Reference, m.StateId, m.StateName, m.SettlementDate, m.Settled,
                                    m.TrustTransactionStatusTypeId, m.TrustTransactionStatusTypeName, m.TrustTransactionStatusBackgroundColour, 
                                    t.TrustSummaryNo, t.PaymentDate, m.PayerPayee, m.Amount, m.Description, 
                                    m.Notes, m.ExpectedPaymentDate, m.PaymentPrepared, 0, 0, 0, 0, m.TrustTransactionJournalId, null, null, null, null, null, null)
                   ).ToList(), t.UpdatedDate, t.UpdatedByUserId, t.Username)).ToList();
        }



        private void ReconcileTrustTransactionItems(List<AccountsCustomEntities.TrustTransactionItemView> trustTransactionItems)
        {
            foreach(var tti in trustTransactionItems)
            {
              
            }
        }



        /// <summary>
        /// Gets a single <see cref="AccountsCustomEntities.TrustSummaryView"/> for a query of a single <see cref="TrustSummary"/>, or null if none exist. 
        /// </summary>
        /// <param name="trustSummaryQry">A query that should only return a single trust summary (i.e. where TrustSummaryId ==...)</param>
        /// <returns>A single <see cref="AccountsCustomEntities.TrustSummaryView"/></returns>
        public AccountsCustomEntities.TrustSummaryView GetTrustSummaryView(IQueryable<TrustSummary> trustSummaryQry)
        {
            return GetTrustSummaryViews(trustSummaryQry).FirstOrDefault();
        }
        /// <summary>
        /// Gets all <see cref="AccountsCustomEntities.TrustSummaryView"/>s filtered by provided parameters
         /// </summary>
        /// <param name="trustAccountId">(REQUIRED) Which <see cref="TrustAccount"/> to show summaries for</param>
        /// <param name="transactionDirectionTypeId">(OPTIONAL / NULLABLE) If not null: which (int)<see cref="Enums.TransactionDirectionTypeEnum"/> to filter by</param>
        /// <param name="summaryStatusList">(OPTIONAL / NULLABLE) List of Summary Statuses to filter by - if any have IsChecked == true, filter by these ones.</param>
        /// <param name="fromDate">(OPTIONAL / NULLABLE) If not null: start of the payment date range to filter by.</param>
        /// <param name="toDate">(OPTIONAL / NULLABLE) If not null: end of the payment date range to filter by.</param>
        /// <returns></returns>
        public IEnumerable<AccountsCustomEntities.TrustSummaryView> GetTrustSummaryViews(int trustAccountId, int? transactionDirectionTypeId, IEnumerable<EntityCompacted> summaryStatusList, DateTime? fromDate, DateTime? toDate)
        {
            IQueryable<TrustSummary> qry = context.TrustSummaries.Where(t=>t.TrustAccountId==trustAccountId);

            if (transactionDirectionTypeId.HasValue)
                qry = qry.Where(t => t.TransactionDirectionTypeId == transactionDirectionTypeId);

            //Summary List selections
            if (summaryStatusList.Where(l => l.Id == -1 && l.IsChecked).Count() == 0)
            {
                //Must be some status selected, but not the Select-All
                var llIst = summaryStatusList.Where(l => l.IsChecked == true).Select(l => l.Id).ToList();
                qry = qry.Where(m => llIst.Contains((m.TrustSummaryStatusTypeId)));
            }            
            //if (srchSummaryStatusId.HasValue)
                         //    qry = qry.Where(t => t.TrustSummaryStatusTypeId == srchSummaryStatusId);


            if (fromDate.HasValue)
                qry = qry.Where(t => t.PaymentDate >= fromDate);

            if (toDate.HasValue)
                qry = qry.Where(t => t.PaymentDate <= toDate);

            return GetTrustSummaryViews(qry);
        }

        /// <summary>
        /// Gets a single <see cref="AccountsCustomEntities.TrustSummaryView"/> for a single <see cref="TrustSummary"/>. 
        /// </summary>
        /// <param name="trustSummaryId">The <see cref="TrustSummary.TrustSummaryId"/> to generate a view for</param>
        /// <returns>A single <see cref="AccountsCustomEntities.TrustSummaryView"/> or null if ID invalid.</returns>
        public AccountsCustomEntities.TrustSummaryView GetTrustSummaryView(int trustSummaryId)
        {
            IQueryable<TrustSummary> qry = context.TrustSummaries.Where(t => t.TrustSummaryId == trustSummaryId);
            return GetTrustSummaryViews(qry).FirstOrDefault();
        }

        /// <summary>
        /// Generates the name of the <see cref="TrustSummary"/> by adding to the existing maximum trust summary number.
        /// </summary>
        /// <param name="transactionDirectionTypeId">Which direction the <see cref="TrustSummary"/> is for, impacts the prefix of the summary number.</param>
        /// <returns>String of the new <see cref="TrustSummary.TrustSummaryNo"/></returns>
        public string BuildTrustSummaryNo(int transactionDirectionTypeId)
        {
            var currTSNo = context.TrustSummaries.Where(x => x.TransactionDirectionTypeId == transactionDirectionTypeId)
                                            .Max(x => x.TrustSummaryNo);

            if (!string.IsNullOrEmpty(currTSNo))
            {
                var tsVal = currTSNo.Substring(1);
                if (Int32.TryParse(tsVal, out int tsInt))
                {
                    if (transactionDirectionTypeId == (int)TransactionDirectionTypeEnum.Deposit)
                        return "R" + (++tsInt).ToString("D6");
                    else
                        return "P" + (++tsInt).ToString("D6");
                }
            }

            return null;
        }



        #endregion

        #region General Accounts Methods

        /// <summary>
        /// Creates a <see cref="GeneralCustomEntities.GeneralCheckList"/> of <see cref="PayableType"/>s
        /// </summary>
        /// <param name="defValue">Default checked true / false value to apply to items in the list</param>
        /// <param name="inclSelectAll">Whether or not to include the "select all" button</param>
        /// <param name="inclNullSelect">Whether or not to include the "null select" button.</param>
        /// <returns>A <see cref="GeneralCustomEntities.GeneralCheckList"/> of <see cref="PayableType"/>s</returns>
        public IEnumerable<GeneralCustomEntities.GeneralCheckList> GetGeneralCheckList_PayableByType(bool defValue, bool inclSelectAll, bool inclNullSelect)
        {
            try
            {
                var liTmp = context.PayableTypes
                            .Select(l => new { l.PayableTypeId, l.PayableTypeName })
                            .ToList()
                            .Select(l => new GeneralCustomEntities.GeneralCheckList(defValue, l.PayableTypeId, l.PayableTypeName))
                            .ToList();


                if (inclSelectAll)
                    liTmp.Insert(0, new GeneralCustomEntities.GeneralCheckList(defValue, -1, "Select All Types"));

                if (inclNullSelect)
                    liTmp.Add(new GeneralCustomEntities.GeneralCheckList(defValue, 0, "No Payable By Type"));

                return liTmp;
            }
            catch (Exception ex)
            {
                Handlers.ErrorHandler.LogError(ex);
                return null;
            }
        }

        /// <summary>
        /// Creates a <see cref="GeneralCustomEntities.GeneralCheckList"/> of <see cref="TrustAccount"/>s
        /// </summary>
        /// <param name="defValue">Default checked true / false value to apply to items in the list</param>
        /// <param name="inclSelectAll">Whether or not to include the "select all" button</param>
        /// <param name="inclNullSelect">Whether or not to include the "null select" button.</param>
        /// <returns>A <see cref="GeneralCustomEntities.GeneralCheckList"/> of <see cref="TrustAccount"/>s</returns>
        public IEnumerable<GeneralCustomEntities.GeneralCheckList> GetGeneralCheckList_TrustAccount(bool defValue, bool inclSelectAll, bool inclNullSelect)
        {
            try
            {

                var tsTmp = context.TrustAccounts
                            .Select(l => new { l.TrustAccountId, l.AccountName })
                            .ToList()
                            .Select(l => new GeneralCustomEntities.GeneralCheckList(defValue, l.TrustAccountId, l.AccountName))
                            .OrderBy(o => o.Name)
                            .ToList();

                if (inclSelectAll)
                    tsTmp.Insert(0, new GeneralCustomEntities.GeneralCheckList(defValue, -1, "-- All Trust Accounts --"));

                if (inclNullSelect)
                    tsTmp.Add(new GeneralCustomEntities.GeneralCheckList(defValue, 0, "-- No Trust Account --"));

                return tsTmp;
            }
            catch (Exception ex)
            {
                Handlers.ErrorHandler.LogError(ex);
                return null;
            }
        }

        /// <summary>
        /// Creates a <see cref="GeneralCustomEntities.GeneralCheckList"/> of <see cref="TransactionType"/>s
        /// </summary>
        /// <param name="defValue">Default checked true / false value to apply to items in the list</param>
        /// <param name="inclSelectAll">Whether or not to include the "select all" button</param>
        /// <returns>A <see cref="GeneralCustomEntities.GeneralCheckList"/> of <see cref="TransactionType"/>s</returns>
        public IEnumerable<GeneralCustomEntities.GeneralCheckList> GetGeneralCheckList_TransactionType(bool defValue, bool inclSelectAll)
        {
            try
            {

                var tsTmp = context.TransactionTypes
                            .Select(l => new { l.TransactionTypeId, l.TransactionTypeName})
                            .ToList()
                            .Select(l => new GeneralCustomEntities.GeneralCheckList(defValue, l.TransactionTypeId, l.TransactionTypeName))
                            .OrderBy(o => o.Name)
                            .ToList();

                if (inclSelectAll)
                    tsTmp.Insert(0, new GeneralCustomEntities.GeneralCheckList(defValue, -1, "-- All Transaction Types --"));


                return tsTmp;
            }
            catch (Exception ex)
            {
                Handlers.ErrorHandler.LogError(ex);
                return null;
            }
        }

        /// <summary>
        /// Creates a <see cref="GeneralCustomEntities.GeneralCheckList"/> of <see cref="TrustTransactionStatusType"/>s
        /// </summary>
        /// <param name="inclSelectAll">Whether or not to include the "select all" button</param>
        /// <returns>A <see cref="GeneralCustomEntities.GeneralCheckList"/> of <see cref="TrustTransactionStatusType"/>s</returns>
        public IEnumerable<GeneralCustomEntities.GeneralCheckList> GetGeneralCheckList_TrustTransactionStatus(bool inclSelectAll)
        {
            try
            {
                var retVal = new List<GeneralCustomEntities.GeneralCheckList>();

                retVal.Add(new GeneralCustomEntities.GeneralCheckList(true, 6, "Manual"));
                retVal.Add(new GeneralCustomEntities.GeneralCheckList(true, 5, "Expected"));
                retVal.Add(new GeneralCustomEntities.GeneralCheckList(true, 1, "Flagged"));
                retVal.Add(new GeneralCustomEntities.GeneralCheckList(true, 2, "Reconciled"));
                retVal.Add(new GeneralCustomEntities.GeneralCheckList(true, 3, "Reversed"));
                retVal.Add(new GeneralCustomEntities.GeneralCheckList(true, 4, "Journalled"));

                if (inclSelectAll)
                    retVal.Insert(0, new GeneralCustomEntities.GeneralCheckList(false, -1, "-- Any Trust Transaction Status --"));

                return retVal;
            }
            catch (Exception ex)
            {
                Handlers.ErrorHandler.LogError(ex);
                return null;
            }
        }

        #endregion

        #region Trust Summary Builder Methods
        //public IEnumerable<AccountsCustomEntities.TB_MatterView> GetTBMatters(int transactionDirectionTypeId, int matterId)
        //{
        //    IQueryable<Matter> qry = context.Matters.Where(m => m.MatterId == matterId);

        //    return GetTBMatters(transactionDirectionTypeId, qry);

        //}

        /// <summary>
        /// Gets limited <see cref="AccountsCustomEntities.TB_MatterView"/>s of matters for use in the TrustTransactionBuilder - filtering based on user selections of <see cref="TransactionDirectionType"/>, payment/settlement dates, <see cref="Lender"/>s, <see cref="State"/>s, etc.
        /// </summary>
        /// <param name="transactionDirectionTypeId">(int) <see cref="TransactionDirectionTypeEnum"/> to obtain matters for.</param>
        /// <param name="settlementStartDate">Start of the date range to retrieve matters that have expected transactions in.</param>
        /// <param name="settlementEndDate">End of the date range to retrieve matters that have expected transactions in.</param>
        /// <param name="lenderList"><see cref="GeneralCustomEntities.GeneralCheckList"/> of <see cref="Lender"/>s to filter matters by.</param>
        /// <param name="stateList"><see cref="GeneralCustomEntities.GeneralCheckList"/> of <see cref="State"/>s to filter matters by.</param>
        /// <param name="transactionTypeList"><see cref="GeneralCustomEntities.GeneralCheckList"/> of <see cref="TransactionType"/>s to filter matters by.</param>
        /// <param name="inclMSATransactions">If true: Include transactions payable to MSA.</param>
        /// <param name="inclInsufficient">If true: Include matters with insufficient funds to settle/</param>
        /// <param name="inclNotSettled">If true: Include matters that have not yet settled.</param>
        /// <param name="inclSettled">If true: Include matters that have settled.</param>
        /// <param name="srchMatterTypeId">(int) <see cref="MatterGroupTypeEnum"/> (New Loans / Discharges / Consents) to filter matters by.</param>
        /// <returns>IEnumerable of <see cref="AccountsCustomEntities.TB_MatterView"/>s for matters that match the filters provided.</returns>
        public IEnumerable<AccountsCustomEntities.TB_MatterView> GetTBMattersOld(int transactionDirectionTypeId,  DateTime? settlementStartDate, DateTime? settlementEndDate, 
                        IEnumerable<GeneralCustomEntities.GeneralCheckList> lenderList, IEnumerable<GeneralCustomEntities.GeneralCheckList> stateList, IEnumerable<GeneralCustomEntities.GeneralCheckList> transactionTypeList,
                        bool inclMSATransactions, bool inclInsufficient, bool inclNotSettled, bool inclSettled, int? srchMatterTypeId = null)
        {
            //Are any of the lenders or MM's checked?
            if (lenderList.Where(l => l.IsChecked == true).Count() == 0 && stateList.Where(l => l.IsChecked == true).Count() == 0)
                return null;

            IQueryable<TrustTransactionItem> qry = context.TrustTransactionItems.AsNoTracking();

            //filter out test matters as we don't want to charge these!!
            qry = qry.Where(m => !m.Matter.IsTestFile);

            if (settlementStartDate.HasValue)
                qry = qry.Where(t => t.Matter.SettlementSchedule.SettlementDate >= settlementStartDate || t.ExpectedPaymentDate >= settlementStartDate);

            if (settlementEndDate.HasValue)
                qry = qry.Where(t => t.Matter.SettlementSchedule.SettlementDate <= settlementEndDate || t.ExpectedPaymentDate <= settlementEndDate);

            if (settlementStartDate.HasValue && settlementEndDate.HasValue)
            {
                //settling on no work good
                if (settlementStartDate == settlementEndDate)
                {
                   qry = qry.Where(t => t.Matter.SettlementSchedule.SettlementDate == settlementEndDate || t.ExpectedPaymentDate == settlementEndDate);
                }
            }

            if (srchMatterTypeId.HasValue)
            {
                qry = qry.Where(t => t.Matter.MatterGroupTypeId == srchMatterTypeId.Value);
            }
            
            if(transactionTypeList.Where(l=>l.IsChecked == true).Count()>0 && transactionTypeList.Where(l => l.Id == -1 && l.IsChecked).Count() == 0)
            {
                var ttypeList = transactionTypeList.Where(l => l.IsChecked == true).Select(l => l.Id).ToList();

                qry = qry.Where(t => ttypeList.Contains((t.TransactionTypeId)));
            }


            //Lender List selections
            if (lenderList.Where(l => l.IsChecked == true).Count() > 0 && lenderList.Where(l => l.Id == -1 && l.IsChecked).Count() == 0)
            {
                //Must be some Lenders selected, but not the Select-All
                var llIst = lenderList.Where(l => l.IsChecked == true).Select(l => l.Id).ToList();

                qry = qry.Where(t => llIst.Contains((t.Matter.LenderId)));

            }

            //State List selections
            if (stateList.Where(l => l.IsChecked == true).Count() > 0 && stateList.Where(l => l.Id == -1 && l.IsChecked).Count() == 0)
            {
                //Must be some Lenders selected, but not the Select-All
                var llIst = stateList.Where(l => l.IsChecked == true).Select(l => l.Id).ToList();
                qry = qry.Where(t => llIst.Contains((t.Matter.StateId)));
            }

            qry = qry.Where(m => !m.Matter.IsTestFile);

            var tList = qry.Select(t => new
            {
                t.TrustTransactionItemId,
                t.MatterId,
                t.Matter.MatterDescription,
                t.Matter.MatterType.MatterTypeName,
                t.Matter.LenderId,
                t.Matter.Lender.LenderName,
                t.Matter.Lender.LenderNameShort,
                t.Matter.LenderRefNo,
                t.Matter.SecondaryRefNo,
                FundingRequestId = (int?)t.MatterLedgerItemTrustTransactionItems.Select(x => x.MatterLedgerItem.FundingRequest.FundingRequestId).FirstOrDefault(),
                FundingRequestName = t.MatterLedgerItemTrustTransactionItems.Select(x=>x.MatterLedgerItem.FundingRequest.FundingRequestNo).FirstOrDefault(),
                t.Matter.Lender.SecondaryRefName,
                t.Matter.StateId,
                t.Matter.State.StateName,
                t.Matter.Settled,
                SettlementDate = (DateTime?)t.Matter.SettlementSchedule.SettlementDate,
                t.TransactionDirectionTypeId,
                t.TransactionDirectionType.TransactionDirectionTypeName,
                t.TransactionTypeId,
                t.TransactionType.TransactionTypeName,
                t.TransactionType.ReportDisplayName,
                t.PayableTypeId,
                t.PayableOthersDesc,
                t.EFT_AccountName,
                t.EFT_BSB,
                t.EFT_AccountNo,
                t.EFT_Reference,
                t.PayerPayee,
                t.Amount,
                t.Description,
                t.Notes,
                t.ExpectedPaymentDate,
                t.PaymentPrepared,
                t.TrustTransactionStatusTypeId,
                t.TrustTransactionStatusType.TrustTransactionStatusTypeName,
                TrustTransactionStatusBackgroundColor = t.TrustTransactionStatusType.BackgroundColour,
                t.TrustSummaryId,
                t.TrustSummary.TrustSummaryNo,
                TrustSummaryTransactionDate = (DateTime?)t.TrustSummary.PaymentDate,
                t.TrustTransactionJournalId
            }).ToList();


            var tbMatters = tList
                .Where(t => t.TransactionDirectionTypeId == transactionDirectionTypeId &&
                            (t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Expected ||
                                t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Manual))
                .GroupBy(g => new
                {
                    g.MatterId,
                    g.MatterDescription,
                    MatterGroupTypeName = g.MatterTypeName,
                    g.LenderId,
                    g.LenderName,
                    g.LenderNameShort,
                    g.LenderRefNo,
                    g.FundingRequestId,
                    g.FundingRequestName,
                    g.SecondaryRefNo,
                    g.SecondaryRefName,
                    g.StateId,
                    g.StateName,
                    g.SettlementDate,
                    g.Settled
                })
            .Select(t => new AccountsCustomEntities.TB_MatterView()
            {
                MatterId = t.Key.MatterId,
                MatterDescription = t.Key.MatterDescription,
                MatterGroupTypeName = t.Key.MatterGroupTypeName,
                LenderId = t.Key.LenderId,
                LenderName = t.Key.LenderName,
                LenderShortName = t.Key.LenderNameShort,
                LenderRefNo = t.Key.LenderRefNo,
                FundingRequestId = t.Key.FundingRequestId,
                FundingRequestName = t.Key.FundingRequestName,
                HasFundingRequest = t.Key.FundingRequestId.HasValue && t.Key.FundingRequestId != 0,
                StateId = t.Key.StateId,
                StateName = t.Key.StateName,
                SettlementDate = t.Key.SettlementDate,
                Settled = t.Key.Settled
            }).ToList();


            for(int i = 0; i< tbMatters.Count; i++)
            {
                var tbMatter = tbMatters[i];
                decimal FlaggedDeposits = 0;
                decimal ExpectedPaymentsAmount = 0;
                decimal JournalledDeposits = 0;
                decimal JournalledPayments = 0;

                //Only need to calculate Flagged / Reconciled amounts for payments
                if (transactionDirectionTypeId == (int)TransactionDirectionTypeEnum.Payment)
                {
                    if (settlementStartDate.HasValue)
                    {
                        var allMatterTrans = context.TrustTransactionItems.AsNoTracking().Where(x => x.MatterId == tbMatter.MatterId).ToList();

                        FlaggedDeposits = allMatterTrans.Where(t => t.MatterId == tbMatter.MatterId && t.TransactionDirectionTypeId == (int)TransactionDirectionTypeEnum.Deposit &&
                                                                        (t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Flagged ||
                                                                        t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Reconciled ||
                                                                        (t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Journalled && //make this include deposits that have been journalled in from other mattters
                                                                        !t.TrustTransactionJournalId.HasValue)))
                                                                        .Select(t => t.Amount).DefaultIfEmpty(0).Sum();
                        ExpectedPaymentsAmount = allMatterTrans.Where(t => t.MatterId == tbMatter.MatterId && t.TransactionDirectionTypeId == (int)TransactionDirectionTypeEnum.Payment &&
                                                                        (t.TrustTransactionStatusTypeId != (int)TrustTransactionStatusTypeEnum.Journalled &&
                                                                         t.TrustTransactionStatusTypeId != (int)TrustTransactionStatusTypeEnum.Reversed))
                                                                        .Select(t => t.Amount).DefaultIfEmpty(0).Sum();
                        JournalledDeposits = tList.Where(t => t.MatterId == tbMatter.MatterId && t.TransactionDirectionTypeId == (int)TransactionDirectionTypeEnum.Deposit &&
                                                                      t.TrustTransactionJournalId.HasValue)
                                                                       .Select(t => t.Amount).DefaultIfEmpty(0).Sum();
                        JournalledPayments = tList.Where(t => t.MatterId == tbMatter.MatterId && t.TransactionDirectionTypeId == (int)TransactionDirectionTypeEnum.Payment &&
                                                                        t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Journalled)
                                                                        .Select(t => t.Amount).DefaultIfEmpty(0).Sum();
                    }
                    else
                    {
                        FlaggedDeposits = tList.Where(t => t.MatterId == tbMatter.MatterId && t.TransactionDirectionTypeId == (int)TransactionDirectionTypeEnum.Deposit &&
                                                                        (t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Flagged ||
                                                                        t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Reconciled || 
                                                                        (t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Journalled && //make this include deposits that have been journalled in from other mattters
                                                                        !t.TrustTransactionJournalId.HasValue)))
                                                                        .Select(t => t.Amount).DefaultIfEmpty(0).Sum();
                        ExpectedPaymentsAmount = tList.Where(t => t.MatterId == tbMatter.MatterId && t.TransactionDirectionTypeId == (int)TransactionDirectionTypeEnum.Payment &&
                                                                        (t.TrustTransactionStatusTypeId != (int)TrustTransactionStatusTypeEnum.Journalled &&
                                                                         t.TrustTransactionStatusTypeId != (int)TrustTransactionStatusTypeEnum.Reversed))
                                                                        .Select(t => t.Amount).DefaultIfEmpty(0).Sum();
                        JournalledDeposits = tList.Where(t => t.MatterId == tbMatter.MatterId && t.TransactionDirectionTypeId == (int)TransactionDirectionTypeEnum.Deposit &&
                                                                       t.TrustTransactionJournalId.HasValue)
                                                                        .Select(t => t.Amount).DefaultIfEmpty(0).Sum();
                        JournalledPayments = tList.Where(t => t.MatterId == tbMatter.MatterId && t.TransactionDirectionTypeId == (int)TransactionDirectionTypeEnum.Payment &&
                                                                        t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Journalled)
                                                                        .Select(t => t.Amount).DefaultIfEmpty(0).Sum();
                       
                    }
                }

                var tItems = tList.Where(t => t.MatterId == tbMatter.MatterId && 
                                                    t.TransactionDirectionTypeId == transactionDirectionTypeId &&
                                                        (t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Expected ||
                                                            t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Manual));
                //Include MSA Transactions
                if (!inclMSATransactions)
                {
                    tItems = tItems.Where(x => !((x.TransactionTypeId == (int)TransactionTypeEnum.EFT || x.TransactionTypeId == (int)TransactionTypeEnum.EFTFree) &&
                                            x.TransactionDirectionTypeId == (int)TransactionDirectionTypeEnum.Payment &&
                                            x.PayableTypeId == (int)PayableTypeEnum.MSA));
                }

                if (!inclInsufficient)
                {
                    tItems = tItems.Where(x => x.TransactionDirectionTypeId == (int)TransactionDirectionTypeEnum.Deposit ||
                                               (x.TransactionDirectionTypeId == (int)TransactionDirectionTypeEnum.Payment && 
                                                 ExpectedPaymentsAmount <= FlaggedDeposits));
                }

                if (!inclNotSettled)
                {
                    tItems = tItems.Where(x => !(x.TransactionDirectionTypeId == (int)TransactionDirectionTypeEnum.Payment &&
                                                 ExpectedPaymentsAmount <= FlaggedDeposits && 
                                                 !(x.Settled || 
                                                    (x.Notes != null && x.Notes.Contains("Returned Funding")))));

                    //tItems = tItems.Where(x => x.TransactionDirectionTypeId == (int)TransactionDirectionTypeEnum.Deposit ||
                    //                           (x.TransactionDirectionTypeId == (int)TransactionDirectionTypeEnum.Payment &&
                    //                             (ExpectedPaymentsAmount > FlaggedDeposits ||
                    //                                (ExpectedPaymentsAmount <= FlaggedDeposits && x.Settled))));
                }

                if (!inclSettled)
                {
                    tItems = tItems.Where(x => !(x.TransactionDirectionTypeId == (int)TransactionDirectionTypeEnum.Payment &&
                                                 ExpectedPaymentsAmount <= FlaggedDeposits &&
                                                 (x.Settled ||
                                                    (x.Notes != null && x.Notes.Contains("Returned Funding")))));

                    //tItems = tItems.Where(x => x.TransactionDirectionTypeId == (int)TransactionDirectionTypeEnum.Deposit ||
                    //                           (x.TransactionDirectionTypeId == (int)TransactionDirectionTypeEnum.Payment &&
                    //                             (ExpectedPaymentsAmount > FlaggedDeposits ||
                    //                                (ExpectedPaymentsAmount <= FlaggedDeposits && !x.Settled))));
                }


                tbMatter.TrustItems = new ObservableCollection<AccountsCustomEntities.TrustTransactionItemView>(tItems
                                        .Select(t => new AccountsCustomEntities.TrustTransactionItemView(t.MatterId, t.MatterDescription, t.LenderId,
                                                t.LenderName, t.LenderRefNo, t.SecondaryRefNo, t.SecondaryRefName, t.TrustTransactionItemId, t.TrustSummaryId, t.TransactionDirectionTypeId,
                                                t.TransactionDirectionTypeName, t.TransactionTypeId, t.TransactionTypeName, t.ReportDisplayName, t.PayableTypeId, t.PayableOthersDesc,
                                                t.EFT_AccountName, t.EFT_BSB, t.EFT_AccountNo, t.EFT_Reference,
                                                t.StateId, t.StateName, t.SettlementDate, t.Settled, t.TrustTransactionStatusTypeId, t.TrustTransactionStatusTypeName,
                                                t.TrustTransactionStatusBackgroundColor, t.TrustSummaryNo, t.TrustSummaryTransactionDate, t.PayerPayee, t.Amount, 
                                                t.Description, t.Notes, t.ExpectedPaymentDate, t.PaymentPrepared, FlaggedDeposits, ExpectedPaymentsAmount, JournalledDeposits, JournalledPayments, 
                                                t.TrustTransactionJournalId, t.FundingRequestId, t.FundingRequestName, null, null, null, null )));


            }


            return tbMatters.Where(t => t.TrustItems.Count > 0);


        }
        public IEnumerable<AccountsCustomEntities.TB_MatterView> GetTBMatters(int transactionDirectionTypeId, DateTime? settlementStartDate, DateTime? settlementEndDate,
                    IEnumerable<GeneralCustomEntities.GeneralCheckList> lenderList, IEnumerable<GeneralCustomEntities.GeneralCheckList> stateList, IEnumerable<GeneralCustomEntities.GeneralCheckList> transactionTypeList,
                    bool inclMSATransactions, bool inclInsufficient, bool inclNotSettled, bool inclSettled, int? srchMatterTypeId = null)
        {
            //Are any of the lenders or MM's checked?
            if (lenderList.Where(l => l.IsChecked == true).Count() == 0 && stateList.Where(l => l.IsChecked == true).Count() == 0)
                return null;

            IQueryable<TrustTransactionItem> qry = context.TrustTransactionItems.AsNoTracking();

            //filter out test matters as we don't want to charge these!!
            qry = qry.Where(m => !m.Matter.IsTestFile);

            if (settlementStartDate.HasValue)
                qry = qry.Where(t => t.Matter.SettlementSchedule.SettlementDate >= settlementStartDate || t.ExpectedPaymentDate >= settlementStartDate);

            if (settlementEndDate.HasValue)
                qry = qry.Where(t => t.Matter.SettlementSchedule.SettlementDate <= settlementEndDate || t.ExpectedPaymentDate <= settlementEndDate);

            if (settlementStartDate.HasValue && settlementEndDate.HasValue)
            {
                //settling on no work good
                if (settlementStartDate == settlementEndDate)
                {
                    qry = qry.Where(t => t.Matter.SettlementSchedule.SettlementDate == settlementEndDate || t.ExpectedPaymentDate == settlementEndDate);
                }
            }

            if (srchMatterTypeId.HasValue)
            {
                qry = qry.Where(t => t.Matter.MatterGroupTypeId == srchMatterTypeId.Value);
            }

            if (transactionTypeList.Where(l => l.IsChecked == true).Count() > 0 && transactionTypeList.Where(l => l.Id == -1 && l.IsChecked).Count() == 0)
            {
                var ttypeList = transactionTypeList.Where(l => l.IsChecked == true).Select(l => l.Id).ToList();

                qry = qry.Where(t => ttypeList.Contains((t.TransactionTypeId)));
            }


            //Lender List selections
            if (lenderList.Where(l => l.IsChecked == true).Count() > 0 && lenderList.Where(l => l.Id == -1 && l.IsChecked).Count() == 0)
            {
                //Must be some Lenders selected, but not the Select-All
                var llIst = lenderList.Where(l => l.IsChecked == true).Select(l => l.Id).ToList();

                qry = qry.Where(t => llIst.Contains((t.Matter.LenderId)));

            }

            //State List selections
            if (stateList.Where(l => l.IsChecked == true).Count() > 0 && stateList.Where(l => l.Id == -1 && l.IsChecked).Count() == 0)
            {
                //Must be some Lenders selected, but not the Select-All
                var llIst = stateList.Where(l => l.IsChecked == true).Select(l => l.Id).ToList();
                qry = qry.Where(t => llIst.Contains((t.Matter.StateId)));
            }

            qry = qry.Where(m => !m.Matter.IsTestFile);

            var tList = qry.Select(t => new
            {
                TrustTransactionItemId = t.TrustTransactionItemId,
                MatterId = t.MatterId,
                MatterDescription = t.Matter.MatterDescription,
                MatterTypeName = t.Matter.MatterType.MatterTypeName,
                LenderId = t.Matter.LenderId,
                LenderName = t.Matter.Lender.LenderName,
                LenderNameShort = t.Matter.Lender.LenderNameShort,
                LenderRefNo = t.Matter.LenderRefNo,
                SecondaryRefNo = t.Matter.SecondaryRefNo,
                FundingRequestId = (int?)t.MatterLedgerItemTrustTransactionItems.Select(x => x.MatterLedgerItem.FundingRequest.FundingRequestId).FirstOrDefault(),
                FundingRequestName = t.MatterLedgerItemTrustTransactionItems.Select(x => x.MatterLedgerItem.FundingRequest.FundingRequestNo).FirstOrDefault(),
                SecondaryRefName = t.Matter.Lender.SecondaryRefName,
                StateId = t.Matter.StateId,
                StateName = t.Matter.State.StateName,
                Settled = t.Matter.Settled,
                SettlementDate = (DateTime?)t.Matter.SettlementSchedule.SettlementDate,
                TransactionDirectionTypeId = t.TransactionDirectionTypeId,
                TransactionDirectionTypeName = t.TransactionDirectionType.TransactionDirectionTypeName,
                TransactionTypeId = t.TransactionTypeId,
                TransactionTypeName = t.TransactionType.TransactionTypeName,
                ReportDisplayName = t.TransactionType.ReportDisplayName,
                PayableTypeId = t.PayableTypeId,
                PayableOthersDesc = t.PayableOthersDesc,
                EFT_AccountName = t.EFT_AccountName,
                EFT_BSB = t.EFT_BSB,
                EFT_AccountNo = t.EFT_AccountNo,
                EFT_Reference = t.EFT_Reference,
                PayerPayee = t.PayerPayee,
                Amount = t.Amount,
                Description = t.Description,
                Notes = t.Notes,
                ExpectedPaymentDate = t.ExpectedPaymentDate,
                PaymentPrepared = t.PaymentPrepared,
                MatterMatterTypes = t.Matter.MatterMatterTypes.Select(m=>m.MatterType.MatterTypeName).Distinct(),
                CertificationDetails = t.Matter.SettlementSchedule.MatterCertificationQueues.Any(x => x.RequestStatusTypeId == (int)CertificationRequestStatusTypeEnum.Retrieved) ?
                    t.Matter.SettlementSchedule.MatterCertificationQueues.Where(x => x.RequestStatusTypeId == (int)CertificationRequestStatusTypeEnum.Retrieved)
                    .Select(o => new { o.AccountDetailsVerified, o.AmountVerified, o.SettlementDateVerified, o.UpdatedDate, o.MatterDocumentId }).ToList()
                    .OrderByDescending(s => s.UpdatedDate)
                        .Select(x => new AccountsCustomEntities.CertificationDetailsView()
                        {
                            CertificationRetrieved = true,
                            CertificationPassed = x.AccountDetailsVerified && x.AmountVerified && x.SettlementDateVerified,
                            MatterDocumentId = x.MatterDocumentId
                        }).FirstOrDefault() : new AccountsCustomEntities.CertificationDetailsView() { CertificationRetrieved = false, CertificationPassed = false, MatterDocumentId = null },
                TrustTransactionStatusTypeId = t.TrustTransactionStatusTypeId,
                TrustTransactionStatusTypeName = t.TrustTransactionStatusType.TrustTransactionStatusTypeName,
                TrustTransactionStatusBackgroundColor = t.TrustTransactionStatusType.BackgroundColour,
                TrustSummaryId  = t.TrustSummaryId,
                TrustSummaryNo = t.TrustSummary.TrustSummaryNo,
                TrustSummaryTransactionDate = (DateTime?)t.TrustSummary.PaymentDate,
                TrustTransactionJournalId = t.TrustTransactionJournalId
            }).OrderBy(o=>o.MatterId).ToList()
            .Select(t => new AccountsCustomEntities.TrustListView
            {
                TrustTransactionItemId = t.TrustTransactionItemId,
                MatterId = t.MatterId,
                MatterDescription = t.MatterDescription,
                MatterTypeName = t.MatterTypeName,
                LenderId = t.LenderId,
                LenderName = t.LenderName,
                MatterMatterTypes = string.Join(", ", t.MatterMatterTypes),
                LenderNameShort = t.LenderNameShort,
                LenderRefNo = t.LenderRefNo,
                SecondaryRefNo = t.SecondaryRefNo,
                FundingRequestId = t.FundingRequestId,
                FundingRequestName = t.FundingRequestName,
                SecondaryRefName = t.SecondaryRefName,
                StateId = t.StateId,
                StateName = t.StateName,
                Settled = t.Settled,
                SettlementDate = (DateTime?)t.SettlementDate,
                TransactionDirectionTypeId = t.TransactionDirectionTypeId,
                TransactionDirectionTypeName = t.TransactionDirectionTypeName,
                TransactionTypeId = t.TransactionTypeId,
                TransactionTypeName = t.TransactionTypeName,
                ReportDisplayName = t.ReportDisplayName,
                PayableTypeId = t.PayableTypeId,
                PayableOthersDesc = t.PayableOthersDesc,
                EFT_AccountName = t.EFT_AccountName,
                EFT_BSB = t.EFT_BSB,
                EFT_AccountNo = t.EFT_AccountNo,
                EFT_Reference = t.EFT_Reference,
                PayerPayee = t.PayerPayee,
                Amount = t.Amount,
                Description = t.Description,
                Notes = t.Notes,
                ExpectedPaymentDate = t.ExpectedPaymentDate,
                PaymentPrepared = t.PaymentPrepared,
                CertificationDetails = t.CertificationDetails,
                TrustTransactionStatusTypeId = t.TrustTransactionStatusTypeId,
                TrustTransactionStatusTypeName = t.TrustTransactionStatusTypeName,
                TrustTransactionStatusBackgroundColor = t.TrustTransactionStatusBackgroundColor,
                TrustSummaryId = t.TrustSummaryId,
                TrustSummaryNo = t.TrustSummaryNo,
                TrustSummaryTransactionDate = (DateTime?)t.TrustSummaryTransactionDate,
                TrustTransactionJournalId = t.TrustTransactionJournalId
            }).ToList();


            var tbMatters = tList
                .Where(t => t.TransactionDirectionTypeId == transactionDirectionTypeId &&
                            (t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Expected ||
                                t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Manual))
                .GroupBy(g => new
                {
                    g.MatterId,
                    g.MatterDescription,
                    MatterGroupTypeName = g.MatterTypeName,
                    g.LenderId,
                    g.LenderName,
                    g.LenderNameShort,
                    g.LenderRefNo,
                    g.FundingRequestId,
                    g.FundingRequestName,
                    g.SecondaryRefNo,
                    g.SecondaryRefName,
                    g.StateId,
                    g.StateName,
                    g.SettlementDate,
                    g.Settled,
                    g.MatterMatterTypes
                })
            .Select(t => new AccountsCustomEntities.TB_MatterView()
            {
                MatterId = t.Key.MatterId,
                MatterDescription = t.Key.MatterDescription,
                MatterGroupTypeName = t.Key.MatterGroupTypeName,
                MatterMatterTypes = t.Key.MatterMatterTypes,
                LenderId = t.Key.LenderId,
                LenderName = t.Key.LenderName,
                LenderShortName = t.Key.LenderNameShort,
                LenderRefNo = t.Key.LenderRefNo,
                SecondaryRefNo = String.IsNullOrEmpty(t.Key.SecondaryRefNo) ? "N/A" : t.Key.SecondaryRefNo,
                FundingRequestId = t.Key.FundingRequestId,
                FundingRequestName = t.Key.FundingRequestName,
                HasFundingRequest = t.Key.FundingRequestId.HasValue && t.Key.FundingRequestId != 0,
                StateId = t.Key.StateId,
                StateName = t.Key.StateName,
                SettlementDate = t.Key.SettlementDate,
                Settled = t.Key.Settled,
                CertificationDetails = t.First().CertificationDetails,
                TrustItems = GetTrustItems(transactionDirectionTypeId, tList, inclInsufficient, inclMSATransactions, inclNotSettled, inclSettled, settlementStartDate, settlementEndDate,
                t.Key.MatterId, t.Key.MatterDescription, t.Key.MatterGroupTypeName, t.Key.LenderId, t.Key.LenderName, t.Key.LenderRefNo, t.Key.FundingRequestId,
                t.Key.FundingRequestName, t.Key.FundingRequestId.HasValue && t.Key.FundingRequestId != 0, t.Key.StateId, t.Key.StateName, t.Key.SettlementDate,
                t.Key.Settled)
            }).ToList();


     
            return tbMatters.Where(t => t.TrustItems.Count > 0);


        }

        private ObservableCollection<AccountsCustomEntities.TrustTransactionItemView> GetTrustItems(int transactionDirectionTypeId, 
            List<AccountsCustomEntities.TrustListView> tList, bool inclInsufficient, bool inclMSATransactions, bool inclNotSettled, 
            bool inclSettled, DateTime? settlementStartDate, DateTime? settlementEndDate, int MatterId, string MatterDescription, string MatterGroupTypeName, int LenderId, 
            string LenderName, string LenderRefNo, int? FundingRequestId,
                string FundingRequestName, bool HasFundingRequest, int StateId, string StateName, DateTime? SettlementDate,
                bool Settled)
        {
            
                decimal FlaggedDeposits = 0;
                decimal ExpectedPaymentsAmount = 0;
                decimal JournalledDeposits = 0;
                decimal JournalledPayments = 0;

                //Only need to calculate Flagged / Reconciled amounts for payments
                if (transactionDirectionTypeId == (int)TransactionDirectionTypeEnum.Payment)
                {
                    if (settlementStartDate.HasValue)
                    {
                        var allMatterTrans = context.TrustTransactionItems.Where(m => m.MatterId == MatterId)
                        .Select(x => new AccountsCustomEntities.TrustTransactionItemListViewLight
                        {
                            MatterId = x.MatterId,
                            TrustTransactionItemId = x.TrustTransactionItemId,
                            TransactionDirectionTypeId = x.TransactionDirectionTypeId,
                            PayerPayee = x.PayerPayee,
                            Amount = x.Amount,
                            TrustTransactionStatusTypeId = x.TrustTransactionStatusTypeId,
                            TrustTransactionStatusTypeName = x.TrustTransactionStatusType.TrustTransactionStatusTypeName,
                            TrustTransactionJournalId = x.TrustTransactionJournalId
                        }).ToList();

                        FlaggedDeposits = allMatterTrans.Where(t => t.MatterId == MatterId && t.TransactionDirectionTypeId == (int)TransactionDirectionTypeEnum.Deposit &&
                                                                        (t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Flagged ||
                                                                        t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Reconciled ||
                                                                        (t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Journalled && //make this include deposits that have been journalled in from other mattters
                                                                        !t.TrustTransactionJournalId.HasValue)))
                                                                        .Select(t => t.Amount).DefaultIfEmpty(0).Sum();
                        ExpectedPaymentsAmount = allMatterTrans.Where(t => t.MatterId == MatterId && t.TransactionDirectionTypeId == (int)TransactionDirectionTypeEnum.Payment &&
                                                                        (t.TrustTransactionStatusTypeId != (int)TrustTransactionStatusTypeEnum.Journalled &&
                                                                         t.TrustTransactionStatusTypeId != (int)TrustTransactionStatusTypeEnum.Reversed))
                                                                        .Select(t => t.Amount).DefaultIfEmpty(0).Sum();
                        JournalledDeposits = tList.Where(t => t.MatterId == MatterId && t.TransactionDirectionTypeId == (int)TransactionDirectionTypeEnum.Deposit &&
                                                                      t.TrustTransactionJournalId.HasValue)
                                                                       .Select(t => t.Amount).DefaultIfEmpty(0).Sum();
                        JournalledPayments = tList.Where(t => t.MatterId == MatterId && t.TransactionDirectionTypeId == (int)TransactionDirectionTypeEnum.Payment &&
                                                                        t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Journalled)
                                                                        .Select(t => t.Amount).DefaultIfEmpty(0).Sum();
                    }
                    else
                    {
                        FlaggedDeposits = tList.Where(t => t.MatterId == MatterId && t.TransactionDirectionTypeId == (int)TransactionDirectionTypeEnum.Deposit &&
                                                                        (t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Flagged ||
                                                                        t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Reconciled ||
                                                                        (t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Journalled && //make this include deposits that have been journalled in from other mattters
                                                                        !t.TrustTransactionJournalId.HasValue)))
                                                                        .Select(t => t.Amount).DefaultIfEmpty(0).Sum();
                        ExpectedPaymentsAmount = tList.Where(t => t.MatterId == MatterId && t.TransactionDirectionTypeId == (int)TransactionDirectionTypeEnum.Payment &&
                                                                        (t.TrustTransactionStatusTypeId != (int)TrustTransactionStatusTypeEnum.Journalled &&
                                                                         t.TrustTransactionStatusTypeId != (int)TrustTransactionStatusTypeEnum.Reversed))
                                                                        .Select(t => t.Amount).DefaultIfEmpty(0).Sum();
                        JournalledDeposits = tList.Where(t => t.MatterId == MatterId && t.TransactionDirectionTypeId == (int)TransactionDirectionTypeEnum.Deposit &&
                                                                       t.TrustTransactionJournalId.HasValue)
                                                                        .Select(t => t.Amount).DefaultIfEmpty(0).Sum();
                        JournalledPayments = tList.Where(t => t.MatterId == MatterId && t.TransactionDirectionTypeId == (int)TransactionDirectionTypeEnum.Payment &&
                                                                        t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Journalled)
                                                                        .Select(t => t.Amount).DefaultIfEmpty(0).Sum();

                    }
                }

                var tItems = tList.Where(t => t.MatterId == MatterId &&
                                              t.TransactionDirectionTypeId == transactionDirectionTypeId &&
                                              (t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Expected ||
                                             t.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Manual));
                //Include MSA Transactions
                if (!inclMSATransactions)
                {
                    tItems = tItems.Where(x => !((x.TransactionTypeId == (int)TransactionTypeEnum.EFT || x.TransactionTypeId == (int)TransactionTypeEnum.EFTFree) &&
                                            x.TransactionDirectionTypeId == (int)TransactionDirectionTypeEnum.Payment &&
                                            x.PayableTypeId == (int)PayableTypeEnum.MSA));
                }

                if (!inclInsufficient)
                {
                    tItems = tItems.Where(x => x.TransactionDirectionTypeId == (int)TransactionDirectionTypeEnum.Deposit ||
                                               (x.TransactionDirectionTypeId == (int)TransactionDirectionTypeEnum.Payment &&
                                                 ExpectedPaymentsAmount <= FlaggedDeposits));
                }

                if (!inclNotSettled)
                {
                    tItems = tItems.Where(x => !(x.TransactionDirectionTypeId == (int)TransactionDirectionTypeEnum.Payment &&
                                                 ExpectedPaymentsAmount <= FlaggedDeposits &&
                                                 !(x.Settled ||
                                                    (x.Notes != null && x.Notes.Contains("Returned Funding")))));

                    //tItems = tItems.Where(x => x.TransactionDirectionTypeId == (int)TransactionDirectionTypeEnum.Deposit ||
                    //                           (x.TransactionDirectionTypeId == (int)TransactionDirectionTypeEnum.Payment &&
                    //                             (ExpectedPaymentsAmount > FlaggedDeposits ||
                    //                                (ExpectedPaymentsAmount <= FlaggedDeposits && x.Settled))));
                }

                if (!inclSettled)
                {
                    tItems = tItems.Where(x => !(x.TransactionDirectionTypeId == (int)TransactionDirectionTypeEnum.Payment &&
                                                 ExpectedPaymentsAmount <= FlaggedDeposits &&
                                                 (x.Settled ||
                                                    (x.Notes != null && x.Notes.Contains("Returned Funding")))));

                    //tItems = tItems.Where(x => x.TransactionDirectionTypeId == (int)TransactionDirectionTypeEnum.Deposit ||
                    //                           (x.TransactionDirectionTypeId == (int)TransactionDirectionTypeEnum.Payment &&
                    //                             (ExpectedPaymentsAmount > FlaggedDeposits ||
                    //                                (ExpectedPaymentsAmount <= FlaggedDeposits && !x.Settled))));
                }


                return new ObservableCollection<AccountsCustomEntities.TrustTransactionItemView>(tItems
                                        .Select(t => new AccountsCustomEntities.TrustTransactionItemView(t.MatterId, t.MatterDescription, t.LenderId,
                                                t.LenderName, t.LenderRefNo, t.SecondaryRefNo, t.SecondaryRefName, t.TrustTransactionItemId, t.TrustSummaryId, t.TransactionDirectionTypeId,
                                                t.TransactionDirectionTypeName, t.TransactionTypeId, t.TransactionTypeName, t.ReportDisplayName, t.PayableTypeId, t.PayableOthersDesc,
                                                t.EFT_AccountName, t.EFT_BSB, t.EFT_AccountNo, t.EFT_Reference,
                                                t.StateId, t.StateName, t.SettlementDate, t.Settled, t.TrustTransactionStatusTypeId, t.TrustTransactionStatusTypeName,
                                                t.TrustTransactionStatusBackgroundColor, t.TrustSummaryNo, t.TrustSummaryTransactionDate, t.PayerPayee, t.Amount,
                                                t.Description, t.Notes, t.ExpectedPaymentDate, t.PaymentPrepared, FlaggedDeposits, ExpectedPaymentsAmount, JournalledDeposits, JournalledPayments, t.TrustTransactionJournalId, t.FundingRequestId, t.FundingRequestName, null, null, null, null, 
                                                t.CertificationDetails)));


            


        }


        //public IEnumerable<AccountsCustomEntities.TB_MatterView> GetTBMatters(int transactionDirectionTypeId, IQueryable<Matter> qry)
        //{
        //    var matList = qry.Select(t => new
        //    {
        //        t.MatterId,
        //        t.MatterDescription,
        //        t.MatterType.MatterTypeName,
        //        t.LenderId,
        //        t.Lender.LenderName,
        //        t.Lender.LenderNameShort,
        //        t.StateId,
        //        t.State.StateName,
        //        SettlementDate = (DateTime?)t.SettlementSchedule.SettlementDate,
        //        t.TrustTransactionsPendingOverride
        //    }).ToList()
        //                .Select(m => new AccountsCustomEntities.TB_MatterView(m.MatterId, m.MatterDescription, m.MatterTypeName, m.LenderId, m.LenderName, m.LenderNameShort,
        //                m.StateId, m.StateName, m.SettlementDate, m.TrustTransactionsPendingOverride, null))
        //    .ToList();

        //    foreach (var tbMV in matList)
        //    {
        //        List<AccountsCustomEntities.TrustTransactionItemView> tmpItems;


        //        if (tbMV.TrustTransactionsBalanceOverride)
        //            tmpItems = GetMatterTrustTransactionsView(transactionDirectionTypeId, tbMV.MatterId, false, false, false, false, false, true).ToList();
        //        else
        //            tmpItems = GetMatterTrustTransactionsView(transactionDirectionTypeId, tbMV.MatterId, false, false, false, false, true, true).ToList();
        //        //tmpItems.RemoveAll(t => t.TrustSummaryId.HasValue);
        //        tbMV.TrustItems = new ObservableCollection<AccountsCustomEntities.TrustTransactionItemView>(tmpItems);
        //    }

        //    //Remove Matters that have no trust items
        //    matList.RemoveAll(m => m.TrustItems.Count() == 0);

        //    return matList;
        //}

        /// <summary>
        /// Given a list of <see cref="AccountsCustomEntities.TB_MatterView"/>s, construct a list of <see cref="AccountsCustomEntities.TB_PayerPayeeView"/>s for the items for use in the other display mode on the TrustTransactionBuilder.
        /// </summary>
        /// <param name="matterList">The original IEnumerable of <see cref="AccountsCustomEntities.TB_MatterView"/>s to base off.</param>
        /// <returns>IEnumerable of <see cref="AccountsCustomEntities.TB_PayerPayeeView"/>s showing PayerPayees, transaction types and transaction references.</returns>
        public IEnumerable<AccountsCustomEntities.TB_PayerPayeeView> BuildPayerPayeeViewList(IEnumerable<AccountsCustomEntities.TB_MatterView> matterList)
        {
            var ppvList = new List<AccountsCustomEntities.TB_PayerPayeeView>();

            foreach (var tbMV in matterList)
            {
                foreach (var mttv in tbMV.TrustItems)
                {
                    var ppv = ppvList.FirstOrDefault(p => p.PayerPayee == mttv.PayerPayee && p.TransactionTypeId == mttv.TransactionTypeId &&
                                                        p.EFT_AccountName == mttv.EFT_AccountName && p.EFT_BSB == mttv.EFT_BSB &&
                                                        p.EFT_AccountNo == mttv.EFT_AccountNo);
                    if (ppv == null)
                    {
                        ppv = new AccountsCustomEntities.TB_PayerPayeeView(mttv.PayerPayee, mttv.TransactionTypeId, mttv.TransactionTypeName, mttv.TransactionTypeDisplayName,
                                                                             mttv.EFT_AccountName, mttv.EFT_BSB, mttv.EFT_AccountNo, 
                                                                             new List<AccountsCustomEntities.TrustTransactionItemView>());
                        ppvList.Add(ppv);
                    }
                    ppv.TrustItems.Add(mttv);
                }
            }

            return ppvList;
        }

        //public IEnumerable<AccountsCustomEntities.TrustSummaryView> GenerateTrustSummaries(IEnumerable<AccountsCustomEntities.TB_MatterView> matterItems)
        //{
        //    List<AccountsCustomEntities.TrustSummaryView> summList = new List<AccountsCustomEntities.TrustSummaryView>();
        //    AccountsCustomEntities.TrustSummaryView tsv = null;

        //    if (matterItems == null || matterItems.Count() == 0)
        //        return summList;

        //    foreach (var mItem in matterItems)
        //    {
        //        var tac = GetTrustAccountForMatter(mItem.MatterId);
        //        var paymentDate = GetCurrentTransactionDate(tac.TrustAccountId);

        //        foreach (var tItem in mItem.TrustItems.Where(t => !t.TrustSummaryId.HasValue))
        //        {
        //            tsv = null;

        //            if (tItem.TransactionDirectionTypeId == (int)Enums.TransactionDirectionTypeEnum.Deposit)
        //            {
        //                tsv = summList.FirstOrDefault(s => s.PayerPayee == tItem.PayerPayee &&
        //                                                   s.TransactionTypeId == tItem.TransactionTypeId &&
        //                                                   s.TrustTransactionItems.Any(t => t.MatterId == mItem.MatterId));
        //            }
        //            else
        //            {
        //                if (tItem.PayerPayee == "MSA National")
        //                {
        //                    tsv = summList.FirstOrDefault(s => s.PaymentDate == mItem.SettlementDate &&
        //                                                   s.PayerPayee == tItem.PayerPayee &&
        //                                                   s.TransactionTypeId == tItem.TransactionTypeId);
        //                }
        //                else
        //                {

        //                }
        //                //tsv = summList.FirstOrDefault(s => s.TrustItems.Any(t => t.TransactionDirectionTypeId == tItem.TransactionDirectionTypeId
        //                //                                    && t.MatterStateId == tItem.MatterStateId
        //                //                                    && t.TransactionTypeId == tItem.TransactionTypeId && t.ExpectedPaymentDate == tItem.ExpectedPaymentDate &&
        //                //                                    t.PayableToTypeId == tItem.PayableToTypeId &&
        //                //                                    (t.PayableToTypeId == (int)Enums.PayableTypeEnum.MSA ||
        //                //                                     t.PayableToTypeId == (int)Enums.PayableTypeEnum.Lender && t.LenderId == tItem.LenderId ||
        //                //                                     t.PayableToTypeId == (int)Enums.PayableTypeEnum.Borrower && t.MatterId == tItem.MatterId ||
        //                //                                     t.PayableToTypeId == (int)Enums.PayableTypeEnum.OtherParty && t.PayableToOthersDesc == tItem.PayableToOthersDesc)));

        //                //if (tsv == null)
        //                //{
        //                //    IQueryable<TrustSummary> qry = context.TrustSummaries.Where(s => s.TrustSummaryStatusTypeId == (int)Enums.TrustSummaryStatusTypeEnum.Building &&
        //                //                                    s.MatterTrustItems.Any(t => t.TransactionDirectionTypeId == tItem.TransactionDirectionTypeId
        //                //                                    && t.Matter.StateId == tItem.MatterStateId
        //                //                                    && t.TransactionTypeId == tItem.TransactionTypeId && t.ExpectedPaymentDate == tItem.ExpectedPaymentDate &&
        //                //                                    t.PayableToTypeId == tItem.PayableToTypeId &&
        //                //                                    (t.PayableToTypeId == (int)Enums.PayableTypeEnum.MSA ||
        //                //                                     t.PayableToTypeId == (int)Enums.PayableTypeEnum.Lender && t.Matter.LenderId == tItem.LenderId ||
        //                //                                     t.PayableToTypeId == (int)Enums.PayableTypeEnum.Borrower && t.MatterId == tItem.MatterId ||
        //                //                                     t.PayableToTypeId == (int)Enums.PayableTypeEnum.OtherParty && t.PayableToOthersDesc == tItem.PayableToOthersDesc)));
        //                //    tsv = GetTrustSummaryView(qry);
        //                //    if (tsv != null)
        //                //        summList.Add(tsv);
        //                //}

        //            }

        //            if (tsv == null)
        //            {
        //                tsv = new AccountsCustomEntities.TrustSummaryView();
        //                tsv.TrustAccountId = tac.TrustAccountId;
        //                tsv.TransactionDirectionTypeId = tItem.TransactionDirectionTypeId;
        //                tsv.TransactionDirectionTypeName = tItem.TransactionDirectionTypeName;
        //                tsv.TransactionTypeId = tItem.TransactionTypeId;
        //                tsv.TransactionTypeName = tItem.TransactionTypeName;
        //                tsv.StateId = mItem.StateId;
        //                tsv.StateName = mItem.StateName;
        //                tsv.PayerPayee = tItem.PayerPayee;
        //                tsv.TrustSummaryNo = BuildTrustSummaryNo(mItem.LenderShortName, tItem.TransactionDirectionTypeId, mItem.MatterId, paymentDate);
        //                tsv.PaymentDate = paymentDate;


        //                tsv.TrustSummaryStatusTypeId = (int)Enums.TrustSummaryStatusTypeEnum.Proposed;
        //                tsv.TrustSummaryStatusTypeName = Enums.TrustSummaryStatusTypeEnum.Proposed.ToString();

        //                tsv.TrustTransactionItems = new System.Collections.ObjectModel.ObservableCollection<AccountsCustomEntities.TrustTransactionItemView>();

        //                summList.Add(tsv);
        //            }

        //            tsv.TrustTransactionItems.Add(tItem);
        //            tsv.Amount = tsv.TrustTransactionItems.Sum(t => t.Amount);
        //        }
        //    }
        //    return summList;
        //}

        //public AccountsCustomEntities.TrustSummaryView GenerateTrustSummary(IEnumerable<AccountsCustomEntities.MatterTrustTransactionItemView> trustItems)
        //{
        //    AccountsCustomEntities.TrustSummaryView tsv = null;
        //    if (trustItems == null && trustItems.Count() == 0)
        //        return null;

        //    //foreach (var ti in trustItems)
        //    //{
        //    //    var tItem = context.MatterTrustItems.FirstOrDefault(t => t.MatterTrustItemId == ti.MatterTrustItemId);
        //    //    if (tsv == null)
        //    //    {
        //    //        tsv = new AccountsCustomEntities.TrustSummaryView();
        //    //        var tac = GetTrustAccountForStateLender(tItem.Matter.StateId, tItem.Matter.LenderId);
        //    //        if (tac == null)
        //    //            throw (new Exception("Could not find Trust Account for State/Lender"));
        //    //        tsv.TrustAccountId = tac.TrustAccountId;
        //    //        tsv.StateId = tItem.Matter.StateId;
        //    //        tsv.StateName = tItem.Matter.State.StateName;
        //    //        tsv.TransactionDirectionTypeId = tItem.TransactionDirectionTypeId;
        //    //        tsv.TransactionDirectionTypeName = tItem.TransactionDirectionType.TransactionDirectionTypeName;
        //    //        tsv.TransactionTypeId = tItem.TransactionTypeId;
        //    //        tsv.TransactionTypeName = tItem.TransactionType.TransactionTypeName;
        //    //        if (tItem.ExpectedPaymentDate.HasValue)
        //    //            tsv.PaymentDate = tItem.ExpectedPaymentDate.Value;
        //    //        else
        //    //            tsv.PaymentDate = DateTime.Today;
        //    //        tsv.PayerPayee = Common.EntityHelper.GetPayerPayee(tItem.TransactionDirectionTypeId, tItem.Matter.Lender.LenderName, tItem.PayableByTypeId, tItem.PayableByType.PayableByTypeName,
        //    //                    tItem.Matter.MatterParties.FirstOrDefault(p => p.PartyTypeId == (int)Enums.PartyTypeEnum.Borrower), tItem.PayableToTypeId, tItem.PayableToType.PayableToTypeName,
        //    //                    tItem.PayableToOthersDesc);

        //    //        tsv.TrustSummaryStatusTypeId = (int)Enums.TrustSummaryStatusTypeEnum.Manual;
        //    //        tsv.TrustSummaryStatusTypeName = Enums.TrustSummaryStatusTypeEnum.Manual.ToString();

        //    //        tsv.TrustSummaryNo = BuildTrustSummaryNo(tItem.Matter.Lender.LenderNameShort, ti.TransactionDirectionTypeId, tItem.MatterId, tsv.PaymentDate);
        //    //        //tsv.TrustItems = new System.Collections.ObjectModel.ObservableCollection<AccountsCustomEntities.TrustSummaryTrustItemsGridView>();
        //    //        //tsv.Amount = tsv.TrustItems.Sum(t => t.Amount);
        //    //    }
        //    //    //var addItem = new AccountsCustomEntities.TrustSummaryTrustItemsGridView(tItem.MatterTrustItemId, tItem.MatterId, tItem.Matter.MatterDescription, tItem.Matter.StateId, tItem.Matter.State.StateName,
        //    //    //                            tItem.Matter.Lender.LenderName, tItem.Matter.Lender.LenderNameShort, ti.PayerPayee, tItem.Amount, ti.Description, tItem.Matter.SettlementSchedule?.SettlementDate);
        //    //    //addItem.IsDirty = true;
        //    //    //tsv.TrustItems.Add(addItem);
        //    //    //tsv.Amount = tsv.TrustItems.Sum(t => t.Amount);
        //    //}

        //    return tsv;
        //}

        #endregion

        #region Trust Balance Methods

        /// <summary>
        /// Construct <see cref="AccountsCustomEntities.TrustBalanceView"/>s for a given query of <see cref="TrustBalance"/>s.
        /// </summary>
        /// <param name="trustBalanceQry">The <see cref="TrustBalance"/>s to generate views for</param>
        /// <returns>An IEnumerable of <see cref="AccountsCustomEntities.TrustBalanceView"/>s</returns>
        public IEnumerable<AccountsCustomEntities.TrustBalanceView> GetTrustBalanceViews(IQueryable<TrustBalance> trustBalanceQry)
        {
            return trustBalanceQry.Select(t => new
            {
                t.TrustBalanceId,
                t.TrustAccountId,
                t.TrustAccount.AccountName,
                t.TrustAccount.BankStateId,
                t.BalanceStartDate,
                t.BalanceEndDate,
                t.OpeningTrustBalance,
                t.ClosingTrustBalance,
                TotalDeposits = t.TrustSummaries.Where(s => s.TransactionDirectionTypeId == (int)Enums.TransactionDirectionTypeEnum.Deposit).Select(s => s.Amount).DefaultIfEmpty(0).Sum(),
                TotalPayments = t.TrustSummaries.Where(s => s.TransactionDirectionTypeId == (int)Enums.TransactionDirectionTypeEnum.Payment).Select(s => s.Amount).DefaultIfEmpty(0).Sum(),
                t.OpeningBankBalance,
                t.ClosingBankBalance,
                t.TotalAdjustments,
                t.Notes,
                t.Reconciled,
                t.UpdatedDate,
                t.UpdatedByUserId,
                UpdatedByUsername = t.User.Username
            })
            .ToList()
                .Select(t => new AccountsCustomEntities.TrustBalanceView(t.TrustBalanceId, t.TrustAccountId, t.AccountName, t.BankStateId, t.BalanceStartDate, t.BalanceEndDate, t.OpeningTrustBalance, t.ClosingTrustBalance,
                                t.TotalDeposits, t.TotalPayments, t.OpeningBankBalance, t.ClosingBankBalance, t.TotalAdjustments, 
                                t.Notes, t.Reconciled, t.UpdatedDate, t.UpdatedByUserId, t.UpdatedByUsername))
                .ToList();
        }

        /// <summary>
        /// Gets all <see cref="AccountsCustomEntities.TrustBalanceView">s for a given <see cref="TrustAccount"/> ordered by the end date of the balance descending.
        /// </summary>
        /// <param name="trustAccountId">The <see cref="TrustAccount.TrustAccountId"/> to obtain balance views for.</param>
        /// <returns><see cref="AccountsCustomEntities.TrustBalanceView">s for each balance for the trust account.</returns>
        public IEnumerable<AccountsCustomEntities.TrustBalanceView> GetTrustBalanceViews(int trustAccountId)
        {
            IQueryable<TrustBalance> trustBalanceQry = context.TrustBalances.Where(t => t.TrustAccountId == trustAccountId).OrderByDescending(t => t.BalanceEndDate);
            return GetTrustBalanceViews(trustBalanceQry);
        }

        /// <summary>
        /// Gets a single <see cref="AccountsCustomEntities.TrustBalanceView"/> for the first match for a query.
        /// </summary>
        /// <param name="trustBalanceQry">A query of <see cref="TrustBalance"/> that should only return one result as only one will be used.</param>
        /// <returns>A single <see cref="AccountsCustomEntities.TrustBalanceView"/> for the first match for the query, or null if no match.</returns>
        public AccountsCustomEntities.TrustBalanceView GetTrustBalanceView(IQueryable<TrustBalance> trustBalanceQry)
        {
            return GetTrustBalanceViews(trustBalanceQry).FirstOrDefault();
        }

        /// <summary>
        /// Gets a single <see cref="AccountsCustomEntities.TrustBalanceView"/> for a single <see cref="TrustBalance"/>.
        /// </summary>
        /// <param name="trustBalanceId"><see cref="TrustBalance.TrustBalanceId"/> of the <see cref="TrustBalance"/> to obtain.</param>
        /// <returns>A single <see cref="AccountsCustomEntities.TrustBalanceView"/> for the trust balance with that ID or null if none exists.</returns>
        public AccountsCustomEntities.TrustBalanceView GetTrustBalanceView(int trustBalanceId)
        {
            IQueryable<TrustBalance> trustBalanceQry = context.TrustBalances.Where(t => t.TrustBalanceId == trustBalanceId);
            return GetTrustBalanceView(trustBalanceQry);
        }

        /// <summary>
        /// APPEARS TO BE OBSOLETE: Gets the matching <see cref="AccountsCustomEntities.TrustBalanceView"/> for a given <see cref="TrustAccount"/> and a given Balance date.
        /// </summary>
        /// <remarks>
        /// No references to this function anywhere else in the code and the date filtering doesn't seem like it would work anyway.
        /// </remarks>
        /// <param name="trustAccountId">The <see cref="TrustAccount.TrustAccountId"/> of the <see cref="TrustAccount"/> to obtain.</param>
        /// <param name="balanceDate">What date to find the <see cref="TrustBalance"/> for</param>
        /// <returns>A single <see cref="AccountsCustomEntities.TrustBalanceView"/> for the trust balance with that ID or null if none exists.</returns>
        public AccountsCustomEntities.TrustBalanceView GetTrustBalanceView(int trustAccountId, DateTime balanceDate)
        {
            IQueryable<TrustBalance> trustBalanceQry = context.TrustBalances.Where(t => t.TrustAccountId == trustAccountId && t.BalanceStartDate <= balanceDate && t.BalanceEndDate >= balanceDate);
            return GetTrustBalanceView(trustBalanceQry);
        }

        /// <summary>
        /// Get all the <see cref="AccountsCustomEntities.TrustSummaryView"/>s for all the items on a <see cref="TrustBalance"/>, optionally filtered by (int)<see cref="TransactionDirectionTypeEnum"/>.
        /// </summary>
        /// <param name="trustBalanceId">The <see cref="TrustBalance.TrustBalanceId"/> to get summary views for.</param>
        /// <param name="transactionDirectionTypeId">(OPTIONAL / NULLABLE) (int?)<see cref="TransactionDirectionTypeEnum"/> to filter items by.</param>
        /// <returns>IEnumerable of <see cref="AccountsCustomEntities.TrustSummaryView"/>s for the trust summaries matching the given filters or null if none found.</returns>
        public IEnumerable<AccountsCustomEntities.TrustSummaryView> GetTrustSummariesForTrustBalance(int trustBalanceId, int? transactionDirectionTypeId)
        {
            IQueryable<TrustSummary> qry = context.TrustSummaries.Where(t => t.TrustBalanceId == trustBalanceId);
            if (transactionDirectionTypeId.HasValue)
                qry = qry.Where(t => t.TransactionDirectionTypeId == transactionDirectionTypeId);
            return GetTrustSummaryViews(qry);
        }

        /// <summary>
        /// Gets <see cref="AccountsCustomEntities.TrustSummaryView"/>s for a all flagged / reconciled <see cref="TrustSummary"/>s for a given <see cref="TrustAccount"/> matching given filters.
        /// </summary>
        /// <param name="trustAccountId">The <see cref="TrustAccount.TrustAccountId"/> to obtain views for.</param>
        /// <param name="transactionDirectionTypeId">(NULLABLE) The (int?)<see cref="TransactionDirectionTypeEnum"/> to filter items by, or both directions if true.</param>
        /// <param name="startDate">The start payment date to filter trust summaries by.</param>
        /// <param name="endDate">The end payment date to filter trust summaries by.</param>
        /// <param name="showFlagged">Flag to include "flagged" items in the result set.</param>
        /// <param name="showReconciled">Flag to include "reconciled" items in the result set.</param>
        /// <returns>IEnumerable of <see cref="AccountsCustomEntities.TrustSummaryView"/>s for the trust summaries matching the given filters or null if none found.</returns>
        public IEnumerable<AccountsCustomEntities.TrustSummaryView> GetTrustSummariesForTrustBalance(int trustAccountId, int? transactionDirectionTypeId, DateTime startDate, DateTime endDate, bool showFlagged, bool showReconciled)
        {
            IQueryable<TrustSummary> qry = context.TrustSummaries.Where(t => !t.TrustBalanceId.HasValue && t.TrustAccountId == trustAccountId && t.PaymentDate >= startDate && t.PaymentDate <= endDate
                                                                            );
            if (transactionDirectionTypeId.HasValue)
                qry = qry.Where(t => t.TransactionDirectionTypeId == transactionDirectionTypeId);
            if (!showFlagged && !showReconciled)
                return new List<AccountsCustomEntities.TrustSummaryView>();
            else if (showFlagged && showReconciled)
                qry = qry.Where(t => (t.TrustSummaryStatusTypeId == (int)Enums.TrustSummaryStatusTypeEnum.Flagged || t.TrustSummaryStatusTypeId == (int)Enums.TrustSummaryStatusTypeEnum.Reconciled));
            else if (showFlagged)
                qry = qry.Where(t => t.TrustSummaryStatusTypeId == (int)Enums.TrustSummaryStatusTypeEnum.Flagged);
            else
                qry = qry.Where(t => (t.TrustSummaryStatusTypeId == (int)Enums.TrustSummaryStatusTypeEnum.Reconciled));

            return GetTrustSummaryViews(qry);
        }

        /// <summary>
        /// Obtain <see cref="AccountsCustomEntities.TrustAdjustmentView"/>s for all <see cref="TrustAdjustment"/>s made for a given <see cref="TrustAccount"/> optionally filtered between given dates, with reversed adjustments optionally filtered out.
        /// </summary>
        /// <param name="trustAccountId">The <see cref="TrustAccount.TrustAccountId"/> to get adjustments for.</param>
        /// <param name="balanceReadOnly">Determines whether or not the result set will be marked as read only.</param>
        /// <param name="showReversed">Whether or not to include reversed adjustments in the result set.</param>
        /// <param name="startDate">The start date of adjustments to include</param>
        /// <param name="endDate">The end date of adjustments to include</param>
        /// <returns>IEnumerable of matching <see cref="AccountsCustomEntities.TrustAdjustmentView"/>s</returns>
        public IEnumerable<AccountsCustomEntities.TrustAdjustmentView> GetTrustAdjustmentsForTrustAccount(int trustAccountId, bool balanceReadOnly, bool showReversed, DateTime? startDate, DateTime? endDate)
        {
            IQueryable<TrustAdjustment> trustAdjustmentQry = context.TrustAdjustments.Where(t => t.TrustAccountId == trustAccountId);
            if (!showReversed && !endDate.HasValue)
                trustAdjustmentQry = trustAdjustmentQry.Where(t => !t.ReverseTrustAdjustmentId.HasValue);
            if (!showReversed && endDate.HasValue)
                trustAdjustmentQry = trustAdjustmentQry.Where(t => !t.ReverseTrustAdjustmentId.HasValue ||
                                                            (t.ReverseTrustAdjustmentId.HasValue && t.TrustAdjustment2.AdjustmentDate > endDate));
            if (startDate.HasValue)
                trustAdjustmentQry = trustAdjustmentQry.Where(t => t.AdjustmentDate >= startDate);
            if (endDate.HasValue)
                trustAdjustmentQry = trustAdjustmentQry.Where(t => t.AdjustmentDate <= endDate);

            return GetTrustAdjustmentViews(balanceReadOnly, trustAdjustmentQry);
        }

        /// <summary>
        /// Gets the ID of the most recent <see cref="TrustBalance"/> for a given <see cref="TrustAccount"/>
        /// </summary>
        /// <param name="trustAccountId">The <see cref="TrustAccount.TrustAccountId"/> to get the latest balance ID for</param>
        /// <returns><see cref="TrustBalance.TrustBalanceId"/> of the most recent <see cref="TrustBalance"/> by balance end date, -1 if no balances for the given account.</returns>
        public int GetLastTrustBalanceId(int trustAccountId)
        {
            var tbRec = context.TrustBalances.Where(t => t.TrustAccountId == trustAccountId).OrderByDescending(t => t.BalanceEndDate).FirstOrDefault();

            if (tbRec == null)
                return -1;
            else
                return tbRec.TrustBalanceId;
        }

        /// <summary>
        /// Get the current Reconciliation date for a <see cref="TrustAccount"/> to use as the payment date for any items flagged / reconciled.  
        /// </summary>
        /// <param name="trustAccountId">The <see cref="TrustAccount.TrustAccountId"/> to check </param>
        /// <returns>The date of the current TrustAccountTransactionDate</returns>
        public DateTime GetCurrentTransactionDate(int trustAccountId)
        {
            var tatd = context.TrustAccountTransactionDates.FirstOrDefault(t => t.TrustAccountId == trustAccountId && t.IsCurrent);
            if (tatd != null)
                return tatd.TransactionDate;
            else
                return DateTime.Today;
        }

        /// <summary>
        /// Gets the next business day to use for new a new Trust Balance by adding a day to the end balance date 
        /// </summary>
        /// <param name="trustAccountId">The <see cref="TrustAccount.TrustAccountId"/> that the new trustbalance will be for</param>
        /// <returns>DateTime of the new reconciliation date.</returns>
        public DateTime GetNextTrustBalanceDate(int trustAccountId)
        {
            var tbRec = context.TrustBalances.Where(t => t.TrustAccountId == trustAccountId).OrderByDescending(t => t.BalanceEndDate).FirstOrDefault();

            if (tbRec == null)
            {
                //No data for this Trust Balance. Get Opening balance date
                var taRec = context.TrustAccounts.Where(t => t.TrustAccountId == trustAccountId).FirstOrDefault();
                if (taRec == null)
                {
                    //Trust Account doesn't exist! Throw exception
                    Exception ex = new Exception("TrustAccountId : " + trustAccountId.ToString() + " doesn't exist");
                    throw (ex);
                }
                else
                    return taRec.BalanceStartDate;
            }
            else
            {
                return tbRec.BalanceEndDate.AddDays(1);
            }
        }

        /// <summary>
        /// Gets the last saved <see cref="TrustBalance"/> for a given <see cref="TrustAccount"/> before a given date.
        /// </summary>
        /// <param name="trustAccountId">The <see cref="TrustAccount.TrustAccountId"/> to get the last saved balance for.</param>
        /// <param name="balanceStartDate">The new date to find the last trust balance before.</param>
        /// <returns>The previous <see cref="TrustBalance"/> for the account.</returns>
        public TrustBalance GetPrevTrustBalance(int trustAccountId, DateTime balanceStartDate)
        {
            return context.TrustBalances.Where(t => t.TrustAccountId == trustAccountId && t.BalanceEndDate < balanceStartDate).OrderByDescending(t => t.BalanceEndDate).FirstOrDefault();
        }

        /// <summary>
        /// APPEARS TO BE OBSOLETE: Get the overall balance of all trust adjustments for a given trust account paid before a given date.
        /// </summary>
        /// <param name="trustAccountId">The <see cref="TrustAccount.TrustAccountId"/> to get the adjustment balance for.</param>
        /// <param name="srchDate"></param>
        /// <returns>The decimal amount of the sum of all deposit adjustments minus the sum of all payment adjustments.</returns>
        public decimal GetTrustAdjustmentsBalance(int trustAccountId, DateTime srchDate)
        {
            return context.TrustAdjustments.Where(t => t.TrustAccountId == trustAccountId && t.AdjustmentDate <= srchDate && t.TransactionDirectionTypeId == (int)Enums.TransactionDirectionTypeEnum.Deposit).Sum(t => t.Amount) -
                    context.TrustAdjustments.Where(t => t.TrustAccountId == trustAccountId && t.AdjustmentDate <= srchDate && t.TransactionDirectionTypeId == (int)Enums.TransactionDirectionTypeEnum.Payment).Sum(t => t.Amount);
        }

        /// <summary>
        /// APPEARS TO BE OBSOLETE: Get the overall balance of all trust summaries for a given trust account paid before a given date.
        /// </summary>
        /// <param name="trustAccountId">The <see cref="TrustAccount.TrustAccountId"/> to get the balance for.</param>
        /// <param name="srchDate"></param>
        /// <returns>The decimal amount of the sum of all deposits minus the sum of all payments.</returns>
        public decimal GetTrustSummaryBalance(int trustAccountId, DateTime srchDate)
        {
            return context.TrustSummaries.Where(t => t.TrustAccountId == trustAccountId && t.PaymentDate <= srchDate && t.TransactionDirectionTypeId == (int)Enums.TransactionDirectionTypeEnum.Deposit && t.TrustSummaryStatusTypeId == (int)Enums.TrustSummaryStatusTypeEnum.Reconciled).Sum(t => t.Amount) -
                    context.TrustSummaries.Where(t => t.TrustAccountId == trustAccountId && t.PaymentDate <= srchDate && t.TransactionDirectionTypeId == (int)Enums.TransactionDirectionTypeEnum.Payment && t.TrustSummaryStatusTypeId == (int)Enums.TrustSummaryStatusTypeEnum.Reconciled).Sum(t => t.Amount);
        }

        //public decimal GetTrustBalanceClosingAmt(int trustAccountId, DateTime endDate)
        //{
        //    if (!context.TrustBalances.Any(t => t.TrustAccountId == trustAccountId && t.BalanceEndDate == endDate))
        //        {
        //        var taRec = context.TrustAccounts.Where(t => t.TrustAccountId == trustAccountId).FirstOrDefault();
        //        if (taRec == null)
        //        {
        //            //Trust Account doesn't exist! Throw exception
        //            Exception ex = new Exception("TrustAccountId : " + trustAccountId.ToString() + " doesn't exist");
        //            throw (ex);
        //        }
        //        else
        //            return taRec.OpeningBalance;

        //    }
        //    else

        //        return context.TrustBalances.FirstOrDefault(t => t.TrustAccountId == trustAccountId && t.BalanceEndDate == endDate).ClosingBalance;
        //}

        /// <summary>
        /// Search for a <see cref="TrustBalance"/> for a <see cref="TrustAccount"/> given date, if there is one, return if it has been reconciled.
        /// </summary>
        /// <param name="trustAccountId">The <see cref="TrustAccount.TrustAccountId"/> to check</param>
        /// <param name="chkDate">The date to check if reconciled.</param>
        /// <returns>If there is a trust balance, True/False depending on <see cref="<see cref="TrustBalance.Reconciled"/> of said balance. False if no balance includes that date.</returns>
        public bool IsTrustBalanceDateReconciled(int trustAccountId, DateTime chkDate)
        {
            var tbRec = context.TrustBalances.AsNoTracking().FirstOrDefault(t => t.TrustAccountId == trustAccountId && t.BalanceStartDate <= chkDate && t.BalanceEndDate >= chkDate);
            if (tbRec == null)
                return false;
            else
                return tbRec.Reconciled;
        }

        //public IEnumerable<AccountsCustomEntities.TrustBalanceItemsView> BuildTrustBalanceItemsView(int trustAccountId, DateTime balanceDate, decimal openingBalance)
        //{
        //    List<AccountsCustomEntities.TrustBalanceItemsView> retList = new List<AccountsCustomEntities.TrustBalanceItemsView>();

        //    AccountsCustomEntities.TrustBalanceItemsView addItem;

        //    //1 - Add the Trust Data
        //    var trs = context.TrustSummaries.Where(t => t.TrustAccountId == trustAccountId && t.PaymentDate == balanceDate.Date && t.TrustSummaryStatusTypeId == (int)Enums.TrustSummaryStatusTypeEnum.Reconciled);
        //    foreach (var tcr in trs)
        //    {
        //        addItem = new AccountsCustomEntities.TrustBalanceItemsView();
        //        addItem.BalanceDate = balanceDate.Date;
        //        addItem.TrustSummaryId = tcr.TrustSummaryId;
        //        addItem.TrustSummaryNo = tcr.TrustSummaryNo;
        //        if (tcr.TransactionDirectionTypeId == (int)Enums.TransactionDirectionTypeEnum.Deposit)
        //        {
        //            addItem.Payer = tcr.PayerPayee;
        //            addItem.Credit = tcr.Amount;
        //            addItem.Payee = null;
        //            addItem.Debit = null;
        //        }
        //        else
        //        {
        //            addItem.Payer = null;
        //            addItem.Credit = null;
        //            addItem.Payee = tcr.PayerPayee;
        //            addItem.Debit = tcr.Amount;
        //        }
        //        //addItem.TrustItems = GetMatterTrustItemsView(addItem.TrustSummaryId.Value).ToList();

        //        retList.Add(addItem);
        //    }

        //    //3 - Any Adjustments
        //    var adjs = context.TrustAdjustments.Where(t => t.TrustAccountId == trustAccountId && t.AdjustmentDate == balanceDate);
        //    foreach (var adj in adjs)
        //    {
        //        addItem = new AccountsCustomEntities.TrustBalanceItemsView();
        //        addItem.BalanceDate = balanceDate.Date;
        //        addItem.TrustAdjustmentId = adj.TrustAdjustmentId;
        //        if (adj.Credit.HasValue)
        //        {
        //            addItem.Payer = "Adjustment#" + adj.TrustAdjustmentId.ToString();
        //            addItem.Credit = adj.Credit;
        //        }
        //        if (adj.Debit.HasValue)
        //        {
        //            addItem.Payee = "Adjustment#" + adj.TrustAdjustmentId.ToString();
        //            addItem.Debit = adj.Debit;
        //        }
        //        retList.Add(addItem);
        //    }


        //    //Now sort list
        //    IComparer<AccountsCustomEntities.TrustBalanceItemsView> cmp = new AccountsCustomEntities.TrustBalanceItemsViewComparer();
        //    retList.Sort(cmp);

        //    //Finally update the Opening / Closing Balances

        //    decimal currBalance = openingBalance;
        //    foreach (var tbi in retList)
        //    {
        //        tbi.OpeningBalance = currBalance;
        //        currBalance = currBalance + (tbi.Credit.HasValue ? tbi.Credit.Value : 0) - (tbi.Debit.HasValue ? tbi.Debit.Value : 0);
        //        tbi.ClosingBalance = currBalance;
        //    }

        //    return retList;
        //}

        //This one gets items which are already "locked in", using the TrustBalanceId
        //public IEnumerable<AccountsCustomEntities.TrustBalanceItemsView> GetTrustBalanceItemsView(int trustBalanceId)
        //{
        //    List<AccountsCustomEntities.TrustBalanceItemsView> retList = new List<AccountsCustomEntities.TrustBalanceItemsView>();

        //    var tb = context.TrustBalances.FirstOrDefault(t => t.TrustBalanceId == trustBalanceId);
        //    if (tb == null)
        //    {
        //        //Bad!
        //        Exception ex = new Exception("TrustBalanceId : " + trustBalanceId.ToString() + " not found.");
        //        throw (ex);
        //    }

        //    AccountsCustomEntities.TrustBalanceItemsView addItem;

        //    //1 - Add the Credits
        //    var trs = context.TrustSummaries.Where(t => t.TrustBalanceId == trustBalanceId);
        //    foreach (var tcr in trs)
        //    {
        //        addItem = new AccountsCustomEntities.TrustBalanceItemsView();
        //        addItem.BalanceDate = tb.BalanceStartDate.Date;
        //        addItem.TrustSummaryId = tcr.TrustSummaryId;
        //        if (tcr.TransactionDirectionTypeId == (int)Enums.TransactionDirectionTypeEnum.Deposit)
        //        {
        //            addItem.Payer = tcr.PayerPayee;
        //            addItem.Credit = tcr.Amount;
        //            addItem.Payee = null;
        //            addItem.Debit = null;
        //        }
        //        else
        //        {
        //            addItem.Payer = null;
        //            addItem.Credit = null;
        //            addItem.Payee = tcr.PayerPayee;
        //            addItem.Debit = tcr.Amount;
        //        }
        //        retList.Add(addItem);
        //    }

        //    //3 - Any Adjustments
        //    var adjs = context.TrustAdjustments.Where(t => t.TrustBalanceId == trustBalanceId);
        //    foreach (var adj in adjs)
        //    {
        //        addItem = new AccountsCustomEntities.TrustBalanceItemsView();
        //        addItem.BalanceDate = tb.BalanceStartDate.Date;
        //        addItem.TrustAdjustmentId = adj.TrustAdjustmentId;
        //        if (adj.Credit.HasValue)
        //        {
        //            addItem.Payer = "Adjustment#" + adj.TrustAdjustmentId.ToString();
        //            addItem.Credit = adj.Credit;
        //        }
        //        if (adj.Debit.HasValue)
        //        {
        //            addItem.Payee = "Adjustment#" + adj.TrustAdjustmentId.ToString();
        //            addItem.Debit = adj.Debit;
        //        }
        //        retList.Add(addItem);
        //    }


        //    //Now sort list
        //    IComparer<AccountsCustomEntities.TrustBalanceItemsView> cmp = new AccountsCustomEntities.TrustBalanceItemsViewComparer();
        //    retList.Sort(cmp);

        //    //Finally update the Opening / Closing Balances

        //    decimal currBalance = tb.OpeningBalance;
        //    foreach (var tbi in retList)
        //    {
        //        tbi.OpeningBalance = currBalance;
        //        currBalance = currBalance + (tbi.Credit.HasValue ? tbi.Credit.Value : 0) - (tbi.Debit.HasValue ? tbi.Debit.Value : 0);
        //        tbi.ClosingBalance = currBalance;
        //    }

        //    return retList;
        //}

        /// <summary>
        /// OBSOLETE: Sees if it is valid to recocile a transaction for a given date by checking if the date is before or equal to the opening balance date of a <see cref="TrustAccount"/>.
        /// </summary>
        /// <param name="trustAccountId">The <see cref="TrustAccount.TrustAccountId"/></param>
        /// <param name="balanceDate">The date that is is being reconciled.</param>
        /// <returns>True if the date is not the opening date of the trust account, false if it isn't</returns>
        /// <remarks>I have absolutely no idea what this function was even trying to do but it looks like it didn't do it very well.</remarks>
        public bool CanReconcile(int trustAccountId, DateTime balanceDate)
        {
            var chkDate = balanceDate.AddDays(-1);
            var tmp = context.TrustBalances.FirstOrDefault(t => t.TrustAccountId == trustAccountId && t.BalanceStartDate == chkDate);
            if (tmp != null && tmp.Reconciled)
                return true;
            else
            {
                //Unless this is the opening balance date for this account, cannot reconcile
                var tmp2 = context.TrustAccounts.FirstOrDefault(t => t.TrustAccountId == trustAccountId);
                if (tmp2 != null && tmp2.BalanceStartDate == balanceDate)
                    return true;
                else
                    return false;
            }
        }

        #endregion

        #region Trust Adjustment Methods
        /// <summary>
        /// Maps a query <see cref="TrustAdjustment"/> to <see cref="AccountsCustomEntities.TrustAdjustmentView"/>s, setting the ReadOnly flag as provided.
        /// </summary>
        /// <param name="balanceReadOnly">Whether or not the returned list is editable or not.</param>
        /// <param name="trustAdjustmentQry">The original query of <see cref="TrustAdjustment"/>s</param>
        /// <returns>IEnumerable of <see cref="AccountsCustomEntities.TrustAdjustmentView"/>s</returns>
        public IEnumerable<AccountsCustomEntities.TrustAdjustmentView> GetTrustAdjustmentViews(bool balanceReadOnly, IQueryable<TrustAdjustment> trustAdjustmentQry)
        {
            return trustAdjustmentQry.Select(t => new {
                t.TrustAdjustmentId,
                t.TrustAccountId,
                t.AdjustmentDate,
                t.TransactionDirectionTypeId,
                t.TransactionDirectionType.TransactionDirectionTypeName,
                t.Amount,
                t.Reason,
                t.ReverseTrustAdjustmentId,
                CurrTransDate = (DateTime?)t.TrustAccount.TrustAccountTransactionDates.FirstOrDefault(a => a.IsCurrent).TransactionDate,
                t.UpdatedDate,
                t.UpdatedByUserId,
                t.User.Username
            })
            .ToList()
                .Select(t => new AccountsCustomEntities.TrustAdjustmentView(t.TrustAdjustmentId, t.TrustAccountId, t.AdjustmentDate, t.TransactionDirectionTypeId, t.TransactionDirectionTypeName, t.Amount, t.Reason, t.ReverseTrustAdjustmentId, balanceReadOnly, t.CurrTransDate, t.UpdatedDate, t.UpdatedByUserId, t.Username))
                .ToList();
        }

        /// <summary>
        /// Gets <see cref="AccountsCustomEntities.TrustAdjustmentView"/>s for a given <see cref="TrustAccount"/>, optionally filtered by adjustment dates between a given range.
        /// </summary>
        /// <param name="trustAccountId">The <see cref="TrustAccount.TrustAccountId"/> to get adjustments for.</param>
        /// <param name="balanceReadOnly">Whether or not the returned list is editable or not.</param>
        /// <param name="startDate">(NULLABLE) If provided, the start cutoff date to filter returned adjustments by.</param>
        /// <param name="endDate">(NULLABLE) If provided, the end cutoff date to filter returned adjustments by.</param>
        /// <returns>IEnumerable of <see cref="AccountsCustomEntities.TrustAdjustmentView"/>s</returns>
        public IEnumerable<AccountsCustomEntities.TrustAdjustmentView> GetTrustAdjustmentViews(int trustAccountId, bool balanceReadOnly, DateTime? startDate, DateTime? endDate)
        {
            IQueryable<TrustAdjustment> trustAdjustmentQry = context.TrustAdjustments.Where(t => t.TrustAccountId == trustAccountId);

            if (startDate.HasValue)
                trustAdjustmentQry = trustAdjustmentQry.Where(t => t.AdjustmentDate >= startDate);

            if (endDate.HasValue)
                trustAdjustmentQry = trustAdjustmentQry.Where(t => t.AdjustmentDate <= endDate);

            return GetTrustAdjustmentViews(balanceReadOnly,trustAdjustmentQry);
        }

        /// <summary>
        /// Gets the balance of Trust Adjustments for a given <see cref="TrustAccount"/>
        /// </summary>
        /// <param name="trustAccountId">The <see cref="TrustAccount.TrustAccountId"/> to obtain a balance for.</param>
        /// <returns>Decimal amount of the sum of deposit adjustments minus the sum of payment adjustments.</returns>
        public decimal GetTrustAdjustmentBalance(int trustAccountId)
        {
            var deps = context.TrustAdjustments.AsNoTracking().Where(t => t.TransactionDirectionTypeId == (int)Enums.TransactionDirectionTypeEnum.Deposit).Select(t => t.Amount).DefaultIfEmpty(0).Sum();
            var pmts = context.TrustAdjustments.AsNoTracking().Where(t => t.TransactionDirectionTypeId == (int)Enums.TransactionDirectionTypeEnum.Payment).Select(t => t.Amount).DefaultIfEmpty(0).Sum();

            return deps - pmts;
        }


        #endregion

        #region Matter Methods

        /// <summary>
        /// If the amounts reconciled in equals the ammount reconciled out, then the matter has been reconciled, so set <see cref="Matter.TransactionsReconciled"/> to true, and set the <see cref="Matter.TransactionsReconciledDate"/>.
        /// </summary>
        /// <param name="matterId"><see cref="Matter.MatterId"/> for the Matter  </param>
        /// <param name="saveContext">If false - checks if the matter SHOULD be marked as reconciled without saving to the database, otherwise saves to the matter.</param>
        /// <returns>TRUE if the sum of reconciled deposits equals the sum of reconciled payments, otherwise false.</returns>
        public bool UpdateMatterReconciledStatus(int matterId, bool saveContext)
        {
            var ttis = context.TrustTransactionItems.AsNoTracking().Where(t => t.MatterId == matterId &&
                                                                               (!t.TrustTransactionJournalId.HasValue ||
                                                                                t.TrustTransactionJournal.ToTrustTransactionItemId == t.TrustTransactionItemId))
                                                                   .Select(t => new { t.TransactionDirectionTypeId, TrustSummaryStatusTypeId = (int?)t.TrustSummary.TrustSummaryStatusTypeId, t.Amount }).ToList();
            var recDeps = ttis.Where(t => t.TransactionDirectionTypeId == (int)Enums.TransactionDirectionTypeEnum.Deposit && t.TrustSummaryStatusTypeId == (int)Enums.TrustSummaryStatusTypeEnum.Reconciled).Select(t => t.Amount).DefaultIfEmpty(0).Sum();
            var recPmts = ttis.Where(t => t.TransactionDirectionTypeId == (int)Enums.TransactionDirectionTypeEnum.Payment && t.TrustSummaryStatusTypeId == (int)Enums.TrustSummaryStatusTypeEnum.Reconciled).Select(t => t.Amount).DefaultIfEmpty(0).Sum();

            var mt = context.Matters.FirstOrDefault(m => m.MatterId == matterId);
            bool reconciledStatus = (recDeps == recPmts);

            if (reconciledStatus != mt.TransactionsReconciled)
            {
                mt.TransactionsReconciled = reconciledStatus;
                mt.TransactionsReconciledDate = DateTime.Now;
                //UpdateMSAInvoiceItems(mt.MatterId);
                if (saveContext)
                    context.SaveChanges();
                return true;
            }
            else
                return false;
        }

        //private void UpdateMSAInvoiceItems(int matterId)
        //{
        //    var ttis = context.TrustTransactionItems.AsNoTracking()
        //                .Where(t => t.MatterId == matterId &&
        //                            (!t.TrustTransactionJournalId.HasValue ||
        //                              t.TrustTransactionJournal.ToTrustTransactionItemId == t.TrustTransactionItemId) && 
        //                            t.TransactionDirectionTypeId == (int)Slick_Domain.Enums.TransactionDirectionTypeEnum.Payment &&
        //                            t.TrustSummary.TrustSummaryStatusTypeId == (int)Enums.TrustSummaryStatusTypeEnum.Reconciled &&
        //                            t.PayerPayee == "MSA National")
        //                .ToList();

        //    var InvItems = context.MatterLedgerItems
        //                .Where(i => i.MatterId == matterId &&
        //                            i.PayableByTypeId == (int)Enums.PayableTypeEnum.Borrower &&
        //                            !i.InvoiceId.HasValue)
        //                .ToList();

        //    //Are they the same total?
        //    if (ttis.Select(t => t.Amount).DefaultIfEmpty(0).Sum() == InvItems.Select(i => i.Amount).DefaultIfEmpty(0).Sum())
        //    {
        //        Matter mt = context.Matters.FirstOrDefault(m => m.MatterId == matterId);
        //        //Create a New Invoice for these items
        //        Invoice inv = new Invoice();
        //        inv.PayableByTypeId = (int)Enums.PayableTypeEnum.Borrower;
        //        inv.InvoiceNo = CreateInvoiceNo(mt.Lender.LenderNameShort, DateTime.Now);
        //        inv.InvoiceRecipient = "MSA National";
        //        inv.InvoiceDesc = "Auto-Generated Invoice";
        //        inv.InvoiceTotal = InvItems.Select(i => i.Amount).DefaultIfEmpty(0).Sum();
        //        inv.InvoiceGST = InvItems.Select(i => i.GST).DefaultIfEmpty(0).Sum();
        //        inv.InvoiceSentDate = DateTime.Now;
        //        inv.InvoicePaidDate = DateTime.Now;
        //        inv.UpdatedDate = DateTime.Now;
        //        inv.UpdatedByUserId = GlobalVars.CurrentUser.UserId;

        //        context.Invoices.Add(inv);
        //        context.SaveChanges();

        //        foreach (var invItm in InvItems)
        //            invItm.InvoiceId = inv.InvoiceId;
        //    }
        //    else
        //    {

        //    }
        //}



        //public bool UpdateTrustTransactionsPending(int matterId)
        //{
        //    return UpdateTrustTransactionsPending(matterId, true, true, true);
        //}
        //public bool UpdateTrustTransactionsPending(int matterId, bool saveContext, bool fixStatus, bool fixTTIs)
        //{
        //    // Usually called after a new trust record, so probably Pending
        //    // Unless no "proposed transactions" are a result of the changes.

        //    bool transPending = true;

        //    if (!context.MatterTrustItems.Any(m => m.MatterId == matterId))
        //        transPending = false;
        //    else
        //    {
        //        var mtivD = GetMatterTrustTransactionsView((int)TransferDirectionTypeEnum.Deposit, matterId, false, false, false, false, true, false);
        //        if (mtivD.Count() == 0)
        //        {
        //            var mtivP = GetMatterTrustTransactionsView((int)TransferDirectionTypeEnum.Payment, matterId, false, false, false, false, true, false);
        //            if (mtivP.Count() == 0)
        //                transPending = false;
        //        }
        //    }

        //    var mat = context.Matters.FirstOrDefault(m => m.MatterId == matterId);
        //    if (transPending != mat.TrustTransactionsPending)
        //    {
        //        if (!fixStatus)
        //        {
        //            mat.TrustTransactionsPending = transPending;
        //            mat.TrustTransactionsPendingOverride = false;
        //            mat.TrustTransactionsPendingOverrideByUserId = null;
        //            mat.TrustTransactionsPendingOverrideDate = null;

        //            if (saveContext)
        //                context.SaveChanges();
        //        }
        //        return true;
        //    }
        //    return false;
        //}


        //public void CreateTrustTransForLedgerItem(MatterLedgerItem mli, int matterGroupTypeId)
        //{
        //    CreateTrustTransForLedgerItem(mli, matterGroupTypeId, false);
        //}
        //public void CreateTrustTransForLedgerItem(MatterLedgerItem mli, int matterGroupTypeId, bool currTrustTransChanged)
        //{
        //    bool trustTransChanged = false;

        //    if (matterGroupTypeId == (int)MatterGroupTypeEnum.NewLoan)
        //    {
        //        int tmpTransDirId;
        //        if (mli.PayableByAccountTypeId == (int)AccountTypeEnum.Trust)
        //            tmpTransDirId = (int)TransactionDirectionTypeEnum.Payment;
        //        else
        //            tmpTransDirId = (int)TransactionDirectionTypeEnum.Deposit;

        //        AddMatterTrustItem(mli, tmpTransDirId, mli.PayableByTypeId, mli.PayableToTypeId);
        //        trustTransChanged = true;
        //    }

        //    if (matterGroupTypeId == (int)MatterGroupTypeEnum.Discharge)
        //    {
        //        var mRep = new MatterRepository(context);
        //        var mv = mRep.GetMatterDetailsCompact(mli.MatterId);
        //        if (mv.IsSelfActing || mli.TransactionTypeId == (int)TransactionTypeEnum.PEXAdebit)
        //        {
        //            AddMatterTrustItem(mli, (int)Enums.TransactionDirectionTypeEnum.Deposit, mli.PayableByTypeId, (int)PayableTypeEnum.MSA);
        //            AddMatterTrustItem(mli, (int)Enums.TransactionDirectionTypeEnum.Payment, mli.PayableByTypeId, mli.PayableToTypeId);
        //            trustTransChanged = true;
        //        }
        //    }

        //    if (trustTransChanged || currTrustTransChanged)
        //    {
        //        UpdateTrustTransactionsPending(mli.MatterId);
        //    }
        //}

        //public void UpdateMatterTrustItem(MatterLedgerItem mli, int matterGroupTypeId)
        //{
        //    bool trustTransChanged = false;
        //    trustTransChanged = RemoveMatterTrustItems(mli);
        //    CreateTrustTransForLedgerItem(mli, matterGroupTypeId, trustTransChanged);
        //}

        //public bool RemoveMatterTrustItems(MatterLedgerItem mli)
        //{
        //    var mtis = context.MatterTrustItems.Where(m => m.MatterLedgerItemId == mli.MatterLedgerItemId);
        //    if (mtis.Count() > 0)
        //    {
        //        context.MatterTrustItems.RemoveRange(mtis);
        //        context.SaveChanges();
        //        return true;
        //    }
        //    else
        //        return false;
        //}

        //

        /// <summary>
        /// Remove all unpaid <see cref="MatterLedgerItem"/>s from a <see cref="Matter"/>. 
        /// </summary>
        /// <param name="matterId"><see cref="Matter.MatterId"/> for the Matter to remove from.</param>
        public void RemoveLedgerItemsByMatter(int matterId)
        {
            RemoveLedgerItemsBySource(matterId, -1);
        }

        /// <summary>
        /// Remove all unpaid <see cref="MatterLedgerItem"/>s from a <see cref="Matter"/> that were created at a certain stage in a Matter's workflow.
        /// </summary>
        /// <example>
        /// <code>
        /// //e.g. if cancelling settlement for matter 3000000 :
        /// //Remove all unpaid Ledger Items for matter 3000000 that were created at the Prepare Settlement Instructions milestone (or via the side grid.) 
        /// uow.GetAccountsRepositoryInstance().RemoveLedgerItemsBySource(3000000, (int)LedgerItemSourceTypeEnum.PrepareSettlementInstr);
        /// </code>
        /// </example>
        /// <param name="matterId"><see cref="Matter.MatterId"/> for the Matter to remove items from. </param>
        /// <param name="ledgerItemSourceTypeId">(int)<see cref="LedgerItemSourceTypeEnum.PrepareSettlementInstr"/> of the source type to remove for - or -1 for ALL unpaid items.</param>
        public void RemoveLedgerItemsBySource(int matterId, int ledgerItemSourceTypeId)
        {
            bool UpdateTrust = false;

            var mfr = context.MatterFinanceRetainedByLenders.Where(m => m.MatterId == matterId && (ledgerItemSourceTypeId == -1 || m.LedgerItemSourceTypeId == ledgerItemSourceTypeId));
            if (mfr.Count() > 0)
            {
                context.MatterFinanceRetainedByLenders.RemoveRange(mfr);
                UpdateTrust = true;
            }

            //if an expected funds has been split using the split MLI function (and are unpaid), combine it back together at this stage before any further changes. 
            var splitExpectedFunds = context.MatterLedgerItems.Where(m => m.MatterId == matterId && m.PayableToTypeId == (int)Enums.PayableTypeEnum.MSA 
                        && m.Description.Contains("Expected Funds") 
                        && m.MatterLedgerItemStatusTypeId == (int)Enums.MatterLedgerItemStatusTypeEnum.Ready
                        && m.FundsTransferredTypeId == (int)FundsTransferredTypeEnum.UnPaid && m.InvoiceId == null)
                        .OrderBy(t=>t.MatterLedgerItemId);

            var originalFund = splitExpectedFunds.FirstOrDefault();

            foreach (var mli in splitExpectedFunds.ToList())
            {
                if(mli.MatterLedgerItemId != originalFund.MatterLedgerItemId && mli.EFT_AccountNo == originalFund.EFT_AccountNo)
                {
                    originalFund.Amount += mli.Amount;
                    RemoveMatterLedgerItem(mli);
                }
                UpdateTrust = true;
            }

            context.SaveChanges();
            

            var mlis = context.MatterLedgerItems.Where(m => m.MatterId == matterId && (ledgerItemSourceTypeId == -1 || m.LedgerItemSourceTypeId == ledgerItemSourceTypeId)
                                        && m.MatterLedgerItemStatusTypeId == (int)Enums.MatterLedgerItemStatusTypeEnum.Ready
                                        && m.FundsTransferredTypeId == (int)FundsTransferredTypeEnum.UnPaid && m.InvoiceId == null);
            
            

            if (mlis.Count() > 0)
            {
                foreach (var mli in mlis.ToList())
                {
                    //get any trust items and links that also need to be removed related to these items
                    var ttisToRemove = context.TrustTransactionItems.Where(m => m.MatterLedgerItemTrustTransactionItems.Any(x => x.MatterLedgerItemId == mli.MatterLedgerItemId) && m.TrustTransactionStatusTypeId == (int)TrustTransactionStatusTypeEnum.Expected);
                    var ttiMlisToRemove = context.MatterLedgerItemTrustTransactionItems.Where(m => m.MatterLedgerItemId == mli.MatterLedgerItemId);
                    context.MatterLedgerItemTrustTransactionItems.RemoveRange(ttiMlisToRemove);
                    context.SaveChanges();
                    var moreTtiMlisToRemove = context.MatterLedgerItemTrustTransactionItems.Where(t => ttisToRemove.Any(x => x.TrustTransactionItemId == t.TrustTransactionItemId));
                    context.MatterLedgerItemTrustTransactionItems.RemoveRange(moreTtiMlisToRemove);
                    context.SaveChanges();
                    //remove the linking entries
                   
                    if (ttisToRemove.Count() > 0)
                    {
                        context.TrustTransactionItems.RemoveRange(ttisToRemove);
                        context.SaveChanges();
                    }
                    RemoveMatterLedgerItem(mli);

                }
                UpdateTrust = true;
            }

            if (UpdateTrust)
            {
                context.SaveChanges();
                UpdateExpectedTrustTransactions(matterId);
            }
        }

        /// <summary>
        /// Remove a <see cref="MatterLedgerItem"/> and all <see cref = "MatterLedgerItemTrustTransactionItem"/> links and <see cref="FundingRequestMatterLedgerItem"/> links as well.
        /// </summary>
        /// <param name="mli"><see cref="MatterLedgerItem"/> to remove.</param>
        public void RemoveMatterLedgerItem(MatterLedgerItem mli)
        {
            if (mli.MatterLedgerItemStatusTypeId == (int)Enums.MatterLedgerItemStatusTypeEnum.Ready &&
                mli.FundsTransferredTypeId == (int)FundsTransferredTypeEnum.UnPaid && mli.InvoiceId == null)
            {
                var mlittis = context.MatterLedgerItemTrustTransactionItems.Where(t => t.MatterLedgerItemId == mli.MatterLedgerItemId);
                context.MatterLedgerItemTrustTransactionItems.RemoveRange(mlittis);

                if (mli.FundingRequestId.HasValue ||
                          context.FundingRequestMatterLedgerItems.Any(x => x.MatterLedgerItemId == mli.MatterLedgerItemId))
                    mli.MatterLedgerItemStatusTypeId = (int)MatterLedgerItemStatusTypeEnum.Cancelled;
                else
                    context.MatterLedgerItems.Remove(mli);

            }

        }

        #endregion

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

        #region XmlDisbursementGrid
            
        #endregion
    }
}
