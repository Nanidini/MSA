using System;
using System.Collections.Generic;
using System.Linq;
using Slick_Domain.Interfaces;
using Slick_Domain.Entities;
using Slick_Domain.Models;
using Slick_Domain.Common;

namespace Slick_Domain.Services
{
    public class FeesRepository : SlickRepository
    {
        public FeesRepository(SlickContext context) : base(context)
        {
        }

        public IEnumerable<FeeView> GetFees()
        {
            var fees = context.Fees
                    .Select(f => new
                    {
                        f.Enabled,
                        f.FeeDescription,
                        f.FeeId,
                        f.FeeName,
                        f.UpdatedByUserId,
                        f.UpdatedDate,
                        f.User.Username,
                        f.VisibleNewLoans,
                        f.VisibleDischarges,
                        f.VisibleConsents,
                        feeDetails = f.FeeDetails.
                          Select(fdt => new FeeDetailView
                          {
                              FeeDetailsId = fdt.FeeDetailsId,
                              FeeId = fdt.FeeId,
                              FeeName = f.FeeName,
                              Enabled = fdt.Enabled,
                              LenderId = fdt.LenderId ?? DomainConstants.AnySelectionId,
                              MatterTypeId = fdt.MatterTypeId ?? DomainConstants.AnySelectionId,
                              MortMgrId = fdt.MortMgrId ?? DomainConstants.AnySelectionId,
                              StateId = fdt.StateId ?? DomainConstants.AnySelectionId,
                              MatterTypeName = fdt.MatterType.MatterTypeName ?? DomainConstants.AnySelection,
                              StateName = fdt.State.StateName ?? DomainConstants.AnySelection,
                              LenderName = fdt.Lender.LenderName ?? DomainConstants.AnySelection,
                              MortMgrName = fdt.MortMgr.MortMgrName ?? DomainConstants.AnySelection,
                              PayableByTypeId = fdt.PayableByTypeId,
                              PayableByTypeName = fdt.PayableType.PayableTypeName,
                              PayableToTypeId = fdt.PayableToTypeId,
                              PayableToTypeName = fdt.PayableType1.PayableTypeName,
                              Instructions = fdt.Instructions,
                              Notes = fdt.Notes,
                              UpdatedByUserId = fdt.UpdatedByUserId,
                              UpdatedByUsername = fdt.User.Username,
                              UpdatedDate = fdt.UpdatedDate,
                              CurrAmount = (decimal?)fdt.FeeDetailsAmounts.OrderByDescending(o => o.DateTo ?? DateTime.MaxValue).FirstOrDefault().Amount,
                              FeeDetailAmounts = fdt.FeeDetailsAmounts
                              .Select(fdta => new FeeDetailAmountView
                              {
                                  FeeDetailsAmountId = fdta.FeeDetailsAmountId,
                                  FeeDetailsId = fdta.FeeDetailsId,
                                  Amount = fdta.Amount,
                                  IsGstFree = fdta.GSTFree,
                                  DateFrom = fdta.DateFrom,
                                  DateTo = fdta.DateTo,
                                  UpdatedByUserId = fdta.UpdatedByUserId,
                                  UpdatedByUsername = fdta.User.Username,
                                  UpdatedDate = fdta.UpdatedDate
                              })
                          })
                    })
                    .Select(f => new FeeView
                    {
                        FeeDescription = f.FeeDescription,
                        FeeDetails = f.feeDetails.ToList(),
                        FeeId = f.FeeId,
                        FeeName = f.FeeName,
                        Enabled = f.Enabled,
                        MatterTypeVisibilityConsent = f.VisibleConsents,
                        MatterTypeVisibilityDischarge = f.VisibleDischarges,
                        MatterTypeVisibilityNewLoan = f.VisibleNewLoans,
                        UpdatedDate = f.UpdatedDate,
                        UpdatedByUsername = f.Username,
                        UpdatedByUserId = f.UpdatedByUserId
                    });

            return fees;
        }

        public IEnumerable<string> GetFeeNameList()
        {
            return context.Fees.Where(x => x.Enabled)
                            .OrderBy(x => x.FeeName)
                            .Select(x => x.FeeName)
                            .ToList();
        }

        public IEnumerable<SettlementFailTransactionActionType> GetSettlementFailedTransactionTypes()
        {
            return context.SettlementFailTransactionActionTypes.ToList();
        }

        public IEnumerable<ExpectedTrustItemForMatterView> GetTrustItemsForMatter(int matterId)
        {
            return context.TrustTransactionItems.AsNoTracking()
                .Where(x => x.MatterId == matterId && x.TransactionDirectionTypeId == (int) Enums.TransactionDirectionTypeEnum.Deposit)
                .Select(x => new ExpectedTrustItemForMatterView
                {
                     Amount = x.Amount,
                     Description = x.Description,
                     ID = x.TrustTransactionItemId,
                     PayableByTypeId = x.PayableTypeId,
                     ExpectedPaymentDate = x.ExpectedPaymentDate,
//                     ReceivedString = x.TrustSummaryId.HasValue ? "Received" : "Not Received",
                    //SettlementFailTransactionActionTypeId = x.SettlementFailTransactionActionTypeId
                 }).ToList();
        }

        //public FeeForMatterView GetFeeFromMatterWFLedger(int matterWFComponentId,string feeDescription)
        //{
        //    return context.MatterWFDisburseLedgerItems.Where(x => x.MatterWFComponentId == matterWFComponentId && x.Description == feeDescription)
        //       .Select(x => new FeeForMatterView
        //       {
        //           Amount = x.Amount,
        //           GST = x.GST,
        //           GstFree = x.GSTFree,
        //           FeeDescription = x.Description,
        //           ID = x.MatterWFDisburseLedgerItemId,
        //           PayableByTypeId = x.PayableByTypeId,
        //           PayableByTypeName = x.PayableType.PayableTypeName
        //       }).FirstOrDefault();
        //}

        //public FeeForMatterView GetFeeFromMatterLedger(int matterId, string feeDescription)
        //{
        //    return context.MatterLedgerItems.Where(x => x.MatterId == matterId && x.MatterLedgerItemStatusTypeId == (int)Enums.MatterLedgerItemStatusTypeEnum.Ready && x.Description == feeDescription)
        //        .Select(x => new FeeForMatterView
        //        {
        //            Amount = x.Amount,
        //            GST = x.GST,
        //            FeeDescription = x.Description,
        //            ID = x.MatterLedgerItemId,
        //            PayableByTypeId = x.PayableByTypeId,
        //            PayableByTypeName = x.PayableType.PayableTypeName
        //        }).FirstOrDefault();
        //}

        public IEnumerable<FeeForMatterView> GetFeesFromMatterLedger(int matterId)
        {
            var matterGroupId = context.Matters.AsNoTracking().First(x => x.MatterId == matterId).MatterGroupTypeId;
            var qry = context.MatterLedgerItems.AsNoTracking().Where(x => x.MatterId == matterId && 
                                                                            (x.MatterLedgerItemStatusTypeId == (int) Enums.MatterLedgerItemStatusTypeEnum.Ready ||
                                                                             x.MatterLedgerItemStatusTypeId == (int)Enums.MatterLedgerItemStatusTypeEnum.Requested));

            if (matterGroupId == (int)Enums.MatterGroupTypeEnum.NewLoan)
            {
                qry = qry.Where(x => x.PayableToAccountTypeId == (int) Enums.AccountTypeEnum.MSA && x.TransactionTypeId == (int) Enums.TransactionTypeEnum.Invoice);
            }
            
            return 
                qry.Select(i => new
                {
                    i.MatterLedgerItemId,
                    i.TransactionTypeId,
                    i.Description,
                    i.PayableByTypeId,
                    i.PayableByOthersDesc,
                    i.PayableToTypeId,
                    i.PayableToOthersDesc,
                    i.Amount,
                    i.GST,
                    i.GSTFree,
                    i.PaymentNotes,
                    i.MatterLedgerItemStatusTypeId,
                    i.FundsTransferredTypeId,
                    i.UpdatedDate,
                    i.UpdatedByUserId,
                    UpdatedByUsername = i.User.Username
                }).ToList()
                .Select(i => new FeeForMatterView(i.MatterLedgerItemId, i.TransactionTypeId, i.Description, i.PayableByTypeId, i.PayableByOthersDesc,
                                    i.PayableToTypeId, i.PayableToOthersDesc, i.Amount, i.GST, i.GSTFree,
                                    i.PaymentNotes, i.MatterLedgerItemStatusTypeId, i.FundsTransferredTypeId, i.UpdatedDate, i.UpdatedByUserId, i.UpdatedByUsername))
                .ToList();
        }

        public IEnumerable<ExpectedTrustItemForMatterView> GetExpectedTrustAmountForMatter(int matterId)
        {
            return context.TrustTransactionItems.AsNoTracking().Where(x => x.MatterId == matterId && x.TransactionDirectionTypeId == (int)Enums.TransactionDirectionTypeEnum.Deposit)
                .Select(t => new
                {
                    t.TrustTransactionItemId,
                    t.TransactionTypeId,
                    t.PayableTypeId,
                    t.ExpectedPaymentDate,
                    t.Amount,
                    t.Description,
                    t.Notes,
                    t.UpdatedDate,
                    t.UpdatedByUserId,
                    t.User.Username
                }).ToList()
                .Select( t=> new ExpectedTrustItemForMatterView(t.TrustTransactionItemId, t.Description, t.PayableTypeId, t.Amount, t.ExpectedPaymentDate, t.TransactionTypeId, t.Notes,  
                                t.UpdatedDate, t.UpdatedByUserId, t.Username))
                .ToList();
        }

        public IEnumerable<MatterDischargeBankedOverrideView> GetMatterDischargeBankedOverrideForMatter(int matterId)
        {
            return context.MatterDischargeBankedOverrides.AsNoTracking().Where(x => x.MatterId == matterId)
                .Select(t => new
                {
                    t.MatterDischargeBankedOverrideId,
                    t.AccountNo,
                    t.Amount,
                    t.Notes,
                    t.UpdatedDate,
                    t.UpdatedByUserId,
                    t.User.Username
                }).ToList()
                .Select(t => new MatterDischargeBankedOverrideView(t.MatterDischargeBankedOverrideId, t.AccountNo, t.Amount, t.Notes,
                               t.UpdatedDate, t.UpdatedByUserId, t.Username))
                .ToList();
        }


        private string GetBillingStatus(Invoice invoice, ref Enums.FeeBillingStatusEnum billingStatus)
        {
            billingStatus = Enums.FeeBillingStatusEnum.NotBilled;

            if (invoice == null || !invoice.InvoiceSentDate.HasValue)
            {
                return "Not Billed";
            }
            else if(invoice.InvoicePaidDate.HasValue)
            {
                billingStatus = Enums.FeeBillingStatusEnum.Paid;
                return "Paid";
            }
            else if(invoice.InvoiceSentDate.HasValue)
            {
                billingStatus = Enums.FeeBillingStatusEnum.Billed;
                return "Billed";
            }
            return "Not Billed";
        }

        public FeeDetail GetFeeDetails(int matterId, string feeDescription)
        {
            var matterCriteria = GetCriteriaFromMatter(matterId);

            var feeDetails = context.FeeDetails.AsNoTracking().Where(x => x.Fee.FeeName == feeDescription && x.Fee.Enabled);
            var feeCriteria = feeDetails
                .Select(x=> new GeneralCustomEntities.BestMatchForCriteria {ID = x.FeeDetailsId, LenderId = x.LenderId, MortMgrId = x.MortMgrId, MatterTypeId = x.MatterTypeId, StateId = x.StateId, SettlementTypeId = null })
                .ToList();

            if (!feeDetails.Any()) return null;
            
            var feeIds = new List<int?>();
            foreach(var criteria in matterCriteria)
            {
                feeIds.Add(CommonMethods.GetBestValueFromSelectedQuery(criteria.MatterTypeId,criteria.SettlementTypeId, criteria.StateId,criteria.LenderId,criteria.MortMgrId,feeCriteria));
            }

            return feeDetails.FirstOrDefault(x => x.FeeDetailsId == feeIds.FirstOrDefault());
        }

        public FeeDetail GetFeeDetails(int matterId, string feeDescription, int? matterTypeId, int? stateId, int? settlementTypeId)
        {
            var matterDets = context.Matters.Where(x => x.MatterId == matterId)
                .Select(x => new { LenderId = x.LenderId, MortMgrId = x.MortMgrId }).FirstOrDefault();
            if (matterDets == null) return null;
                
            var feeDetails = context.FeeDetails.AsNoTracking().Where(x => x.Fee.FeeName == feeDescription && x.Fee.Enabled);
            var feeCriteria = feeDetails
                .Select(x => new GeneralCustomEntities.BestMatchForCriteria { ID = x.FeeDetailsId, LenderId = x.LenderId, MortMgrId = x.MortMgrId, MatterTypeId = x.MatterTypeId, StateId = x.StateId, SettlementTypeId = null })
                .ToList();

            if (!feeDetails.Any()) return null;

            var feeIds = new List<int?>();
           
            feeIds.Add(CommonMethods.GetBestValueFromSelectedQuery(matterTypeId, settlementTypeId, stateId, matterDets.LenderId, matterDets.MortMgrId, feeCriteria));
            
            return feeDetails.FirstOrDefault(x => x.FeeDetailsId == feeIds.FirstOrDefault());
        }

        /// <summary>
        /// Populates criteria from Matter - as there may be multiple securities - returns multiple.
        /// </summary>
        /// <param name="matterId"></param>
        /// <returns></returns>
        private List<GeneralCustomEntities.BestMatchForCriteria> GetCriteriaFromMatter(int matterId)
        {
            return (from m in context.Matters
                    join ms in context.MatterSecurities.Where(x=> !x.Deleted) on m.MatterId equals ms.MatterId into msg
                    from ms in msg.DefaultIfEmpty()
                    where m.MatterId == matterId
                    select new GeneralCustomEntities.BestMatchForCriteria
                    {
                        ID = m.MatterId,
                        LenderId = m.LenderId,
                        MortMgrId = m.MortMgrId,
                        StateId = ms.StateId,
                        SettlementTypeId = ms.SettlementTypeId,
                        MatterTypeId = ms.MatterTypeId
                    }).ToList();
        }

        private class tmpFeeDet
        {
            public FeeDetailsAmount fda { get; set; }
            public int Ranking { get; set; }
        }
        public bool AddConsentBaseFees(MatterCustomEntities.MatterWFComponentView matterWFCompView)
        {


            List<String> pexaWorkspaceNos = new List<String>();
            var mRep = new MatterRepository(context);
            var mv = mRep.GetMatterDetailsCompact(matterWFCompView.MatterId);
            //list of tuple<feelookup, feename, multiplier (number of copies to add) >
            List<Tuple<string, string, int>> feesToAdd = new List<Tuple<string, string, int>>();

            feesToAdd.Add(new Tuple<string, string, int>("Base Fee Consent", "MSA Fee", 1));



            foreach (var fee in feesToAdd)
            {
                FeeDetailsAmount feeDetails = null;
                string feeLookup = fee.Item1;
                //look up best match fee

                if (feeLookup != null)
                {
                    feeDetails = GetFeeDetailsAmount(feeLookup, DateTime.Now, (int)Enums.MatterGroupTypeEnum.Consent, matterWFCompView.LenderId, matterWFCompView.MortMgrId, null);

                    if (feeDetails == null)
                    {
                        feeDetails = GetFeeDetailsAmount(feeLookup, DateTime.Now, (int)Enums.MatterGroupTypeEnum.Consent, matterWFCompView.LenderId, null, null);

                        if (feeDetails == null)
                        {
                            feeDetails = GetFeeDetailsAmount(feeLookup, DateTime.Now, (int)Enums.MatterGroupTypeEnum.Consent, null, null, null);
                        }
                    }

                    if (feeDetails != null && feeDetails.Amount > 0)
                    {
                        if (!context.MatterLedgerItems.Any(f => f.MatterId == matterWFCompView.MatterId && f.Description == fee.Item2 && f.Amount == feeDetails.Amount * fee.Item3))
                        {
                            MatterLedgerItem newMli = new MatterLedgerItem()
                            {
                                MatterId = matterWFCompView.MatterId,
                                LedgerItemSourceTypeId = (int)Slick_Domain.Enums.LedgerItemSourceTypeEnum.TitleCollectionComplete,
                                Amount = feeDetails.Amount * fee.Item3,//number of copies of this fee to add
                                TransactionTypeId = (int)Slick_Domain.Enums.TransactionTypeEnum.Invoice,
                                PayableByTypeId = feeDetails.FeeDetail.PayableByTypeId ?? (int)Slick_Domain.Enums.PayableTypeEnum.Lender,
                                PayableToTypeId = feeDetails.FeeDetail.PayableToTypeId ?? (int)Slick_Domain.Enums.PayableTypeEnum.MSA,
                                PayableByAccountTypeId = (int)Slick_Domain.Enums.AccountTypeEnum.External,
                                PayableToAccountTypeId = (int)Slick_Domain.Enums.AccountTypeEnum.MSA,
                                MatterLedgerItemStatusTypeId = (int)Slick_Domain.Enums.MatterLedgerItemStatusTypeEnum.Ready,
                                FundsTransferredTypeId = (int)Slick_Domain.Enums.FundsTransferredTypeEnum.UnPaid,
                                Description = fee.Item2,
                                UpdatedByUserId = GlobalVars.CurrentUser.UserId,
                                UpdatedDate = DateTime.Now,
                                GSTFree = feeDetails.GSTFree
                            };
                            if (mv.MortMgrId == 49)
                            {
                                newMli.PayableByTypeId = (int)Slick_Domain.Enums.PayableTypeEnum.MortMgr;
                            }

                            context.MatterLedgerItems.Add(newMli);
                            context.SaveChanges();
                        }
                    }
                }
            }
            return true;


        }

        public bool AddTitleCollectionCompleteFees(MatterCustomEntities.MatterWFComponentView matterWFCompView)
        {

            
            List<String> pexaWorkspaceNos = new List<String>();
            var mRep = new MatterRepository(context);
            var mv = mRep.GetMatterDetailsCompact(matterWFCompView.MatterId);
            //list of tuple<feelookup, feename, multiplier (number of copies to add) >
            List<Tuple<string, string, int>> feesToAdd = new List<Tuple<string, string, int>>();

            //add pexa fee based on number of distinct workspaces
            foreach (var sec in context.MatterSecurities.Where(m => m.MatterId == matterWFCompView.MatterId && m.MatterSecurityMatterPexaWorkspaces.Any()))
            {
                List<String> secPexaWorkspaceNo = sec.MatterSecurityMatterPexaWorkspaces.Select(x => x.MatterPexaWorkspace.PexaWorkspace.PexaWorkspaceNo).ToList();
                foreach (var pexaWorkspaceNo in secPexaWorkspaceNo)
                {
                    if (!pexaWorkspaceNos.Contains(pexaWorkspaceNo))
                    {
                        pexaWorkspaceNos.Add(pexaWorkspaceNo);
                    }
                }
            }

            //we won't add pexa fee for fast refis at settlement complete - we'll add them at title collection complete. 
            
            if (pexaWorkspaceNos.Count() == 1)
            {
                feesToAdd.Add(new Tuple<string, string, int>("Pexa SingleTitle", "PEXA Fee", 1));
            }

            if (pexaWorkspaceNos.Count() > 1)
            {
                feesToAdd.Add(new Tuple<string, string, int>("Pexa MultipleTitles", "PEXA Fee", 1));
            }
            

            foreach (var fee in feesToAdd)
            {
                FeeDetailsAmount feeDetails = null;
                string feeLookup = fee.Item1;
                //look up best match fee

                if (feeLookup != null)
                {
                    feeDetails = GetFeeDetailsAmount(feeLookup, DateTime.Now, null, matterWFCompView.LenderId, matterWFCompView.MortMgrId, null);

                    if (feeDetails == null)
                    {
                        feeDetails = GetFeeDetailsAmount(feeLookup, DateTime.Now, null, matterWFCompView.LenderId, null, null);

                        if (feeDetails == null)
                        {
                            feeDetails = GetFeeDetailsAmount(feeLookup, DateTime.Now, null, null, null, null);
                        }
                    }

                    if (feeDetails != null && feeDetails.Amount > 0)
                    {

                        MatterLedgerItem newMli = new MatterLedgerItem()
                        {
                            MatterId = matterWFCompView.MatterId,
                            LedgerItemSourceTypeId = (int)Slick_Domain.Enums.LedgerItemSourceTypeEnum.TitleCollectionComplete,
                            Amount = feeDetails.Amount * fee.Item3,//number of copies of this fee to add
                            TransactionTypeId = (int)Slick_Domain.Enums.TransactionTypeEnum.Invoice,
                            PayableByTypeId = feeDetails.FeeDetail.PayableByTypeId ?? (int)Slick_Domain.Enums.PayableTypeEnum.Lender,
                            PayableToTypeId = feeDetails.FeeDetail.PayableToTypeId ?? (int)Slick_Domain.Enums.PayableTypeEnum.MSA,
                            PayableByAccountTypeId = (int)Slick_Domain.Enums.AccountTypeEnum.External,
                            PayableToAccountTypeId = (int)Slick_Domain.Enums.AccountTypeEnum.MSA,
                            MatterLedgerItemStatusTypeId = (int)Slick_Domain.Enums.MatterLedgerItemStatusTypeEnum.Ready,
                            FundsTransferredTypeId = (int)Slick_Domain.Enums.FundsTransferredTypeEnum.UnPaid,
                            Description = fee.Item2,
                            UpdatedByUserId = GlobalVars.CurrentUser.UserId,
                            UpdatedDate = DateTime.Now,
                            GSTFree = feeDetails.GSTFree
                        };
                        if (mv.MortMgrId == 49)
                        {
                            newMli.PayableByTypeId = (int)Slick_Domain.Enums.PayableTypeEnum.MortMgr;
                        }

                        context.MatterLedgerItems.Add(newMli);
                        context.SaveChanges();
                    }
                }
            }
            return true;

            
        }

        public bool AddSettlementCompleteFees(MatterCustomEntities.MatterWFComponentView matterWFCompView)
        {
            List<String> pexaWorkspaceNos = new List<String>();
            var mRep = new MatterRepository(context);
            var mv = mRep.GetMatterDetailsCompact(matterWFCompView.MatterId);
            //list of tuple<feelookup, feename, multiplier (number of copies to add) >
            List<Tuple<string, string, int>> feesToAdd = new List<Tuple<string, string, int>>();



            if (matterWFCompView.LenderId == 139)
            {
                //add pexa fee based on number of distinct workspaces
                foreach (var sec in context.MatterSecurities.Where(m => m.MatterId == matterWFCompView.MatterId && m.MatterSecurityMatterPexaWorkspaces.Any()))
                {
                    List<String> secPexaWorkspaceNo = sec.MatterSecurityMatterPexaWorkspaces.Select(x => x.MatterPexaWorkspace.PexaWorkspace.PexaWorkspaceNo).ToList();
                    foreach (var pexaWorkspaceNo in secPexaWorkspaceNo)
                    {
                        if (!pexaWorkspaceNos.Contains(pexaWorkspaceNo))
                        {
                            pexaWorkspaceNos.Add(pexaWorkspaceNo);
                        }
                    }
                }
                if (pexaWorkspaceNos.Count() == 0)
                {
                    if (context.MatterSecurities.Any(m => m.MatterId == matterWFCompView.MatterId && m.SettlementTypeId == (int)Enums.SettlementTypeEnum.PEXA))
                    {
                        pexaWorkspaceNos = context.MatterPexaWorkspaces.Where(m => m.MatterId == (int)matterWFCompView.MatterId).Select(p => p.PexaWorkspace.PexaWorkspaceNo).ToList().Distinct().ToList();
                    }
                }
                //we won't add pexa fee for fast refis at settlement complete - we'll add them at title collection complete. 

                if (!context.MatterMatterTypes.Where(mt => mt.MatterId == matterWFCompView.MatterId).Any(x => x.MatterTypeId == (int)Slick_Domain.Enums.MatterTypeEnum.FastRefinance))
                {
                    if (pexaWorkspaceNos.Count() == 1)
                    {
                        feesToAdd.Add(new Tuple<string, string, int>("Pexa SingleTitle", "PEXA Fee", 1));
                    }

                    if (pexaWorkspaceNos.Count() > 1)
                    {
                        feesToAdd.Add(new Tuple<string, string, int>("Pexa MultipleTitles", "PEXA Fee", 1));
                    }
                }

                //add agent fee
                if (matterWFCompView.MortMgrId.HasValue && matterWFCompView.MortMgrId == 49) //SORRY
                {
                    int agentFeesToAdd = context.Matters.FirstOrDefault(m => m.MatterId == matterWFCompView.MatterId).SettlementSchedule.SettlementScheduleVenues
                        .Count(x => x.SettlementRegionTypeId == (int)Slick_Domain.Enums.SettlementRegionTypeEnum.Regional);

                    if (agentFeesToAdd > 0)
                    {
                        feesToAdd.Add(new Tuple<string, string, int>("Agent Fee", "Agent Fee", agentFeesToAdd));
                    }
                }

                //Postage Fee
                int expressPostFeesToAdd = context.MatterWFSendProcessedDocs.Count(x => x.MatterWFComponent.MatterId == matterWFCompView.MatterId && !String.IsNullOrEmpty(x.ExpressPostSentTracking));
                expressPostFeesToAdd += context.MatterWFSendProcessedDocs.Count(x => x.MatterWFComponent.MatterId == matterWFCompView.MatterId && !String.IsNullOrEmpty(x.ExpressPostReceiveTracking));

                if (expressPostFeesToAdd > 0)
                {
                    feesToAdd.Add(new Tuple<string, string, int>("Express Post", "Express Post Fee", expressPostFeesToAdd));
                }




                List<string> noFeeItems = new List<string>()
                {
                    "LAND TITLES REGISTRATION FEE",
                    "LAND TITLES REGISTRATIN FEE",
                    "STAMP DUTY",
                    "TITLE SEARCH",
                    "PEXA FEE",
                    "AGENT FEE",
                    "MSA FEE",
                    "RE-DOCUMENTATION FEE",
                    "REDOC FEE"
                };

                //TT Fees
                int ttFeesToAdd = 0;
                if (matterWFCompView.LenderId == 139)
                {
                    ttFeesToAdd = context.MatterLedgerItems
                    .Count(x => x.MatterId == matterWFCompView.MatterId &&
                    x.TransactionTypeId == (int)Slick_Domain.Enums.TransactionTypeEnum.TTFree &&
                    x.PayableToTypeId != (int)Slick_Domain.Enums.PayableTypeEnum.MSA
                    && x.PayableToName.ToUpper() != "ADVANTEDGE FINANCIAL SERVICES PTY LTD"
                    && !noFeeItems.Any(f => x.Description.ToUpper().Contains(f.ToUpper())));
                }
                else
                {
                    ttFeesToAdd = context.MatterLedgerItems
                    .Count(x => x.MatterId == matterWFCompView.MatterId && x.TransactionTypeId == (int)Slick_Domain.Enums.TransactionTypeEnum.TTFree && x.PayableToTypeId != (int)Slick_Domain.Enums.PayableTypeEnum.MSA);
                }


                if (ttFeesToAdd > 0)
                {
                    feesToAdd.Add(new Tuple<string, string, int>("TT SameDay", "TT for Borrower", ttFeesToAdd));
                }

                //EFT Fees




                int eftFeesToAdd = 0;

                if (matterWFCompView.LenderId == 139)
                {
                    eftFeesToAdd = context.MatterLedgerItems
                    .Count(x => x.MatterId == matterWFCompView.MatterId &&
                    x.TransactionTypeId == (int)Slick_Domain.Enums.TransactionTypeEnum.EFTFree
                    && x.PayableToTypeId != (int)Slick_Domain.Enums.PayableTypeEnum.MSA
                    && x.PayableToName.ToUpper() != "ADVANTEDGE FINANCIAL SERVICES PTY LTD"
                    && !noFeeItems.Any(f => x.Description.ToUpper().Contains(f.ToUpper())));
                }
                else
                {
                    eftFeesToAdd = context.MatterLedgerItems
                   .Count(x => x.MatterId == matterWFCompView.MatterId && x.TransactionTypeId == (int)Slick_Domain.Enums.TransactionTypeEnum.EFTFree && x.PayableToTypeId != (int)Slick_Domain.Enums.PayableTypeEnum.MSA);
                }
                if (eftFeesToAdd > 0)
                {
                    feesToAdd.Add(new Tuple<string, string, int>("EFT", "EFT for Borrower", eftFeesToAdd));
                }


                //Bank Cheque
                int bankChequeFeesToAdd = context.MatterLedgerItems
                    .Count(x => x.MatterId == matterWFCompView.MatterId && (x.TransactionTypeId == (int)Slick_Domain.Enums.TransactionTypeEnum.ChequeFree || x.TransactionTypeId == (int)Slick_Domain.Enums.TransactionTypeEnum.PPSFree));

                if (bankChequeFeesToAdd > 0)
                {
                    feesToAdd.Add(new Tuple<string, string, int>("Bank Cheque Issue", "Bank Cheque for Borrower", bankChequeFeesToAdd));
                }

                //BPAY
                int bpayFeesToAdd = context.MatterLedgerItems
                    .Count(x => x.MatterId == matterWFCompView.MatterId && x.TransactionTypeId == (int)Slick_Domain.Enums.TransactionTypeEnum.BPayFree && x.PayableToTypeId != (int)Slick_Domain.Enums.PayableTypeEnum.MSA);

                if (bpayFeesToAdd > 0)
                {
                    feesToAdd.Add(new Tuple<string, string, int>("EFT", "BPAY Fee for Borrower", bpayFeesToAdd));
                }
            }
            else if(matterWFCompView.LenderId == 171)
            {

                List<string> noFeeItems = new List<string>()
                {
                   "LAND TITLES REGISTRATION FEE",
                   "STAMP DUTY",
                   "TITLE SEARCH",
                   "PEXA FEE",
                   "AGENT FEE",
                   "MSA FEE",
                   "RE-DOCUMENTATION FEE",
                   "REDOC FEE"
                };

                //TT Fees
                int ttFeesToAdd = 0;
                if (matterWFCompView.LenderId == 171)
                {
                    ttFeesToAdd = context.MatterLedgerItems
                    .Count(x => x.MatterId == matterWFCompView.MatterId &&
                    x.TransactionTypeId == (int)Slick_Domain.Enums.TransactionTypeEnum.TTFree &&
                    x.PayableToTypeId != (int)Slick_Domain.Enums.PayableTypeEnum.MSA
                    && x.PayableToName.ToUpper() != "JUDO BANK PTY LTD"
                    && !noFeeItems.Any(f => x.Description.ToUpper().Contains(f.ToUpper())));
                }
                else
                {
                    ttFeesToAdd = context.MatterLedgerItems
                    .Count(x => x.MatterId == matterWFCompView.MatterId && x.TransactionTypeId == (int)Slick_Domain.Enums.TransactionTypeEnum.TTFree && x.PayableToTypeId != (int)Slick_Domain.Enums.PayableTypeEnum.MSA);
                }


                if (ttFeesToAdd > 0)
                {
                    feesToAdd.Add(new Tuple<string, string, int>("TT SameDay", "TT for Borrower", ttFeesToAdd));
                }
            }

            foreach (var fee in feesToAdd)
            {
                FeeDetailsAmount feeDetails = null;
                string feeLookup = fee.Item1;
                //look up best match 
             
                int matterTypeId = matterWFCompView.MatterTypeGroupId == (int)Enums.MatterGroupTypeEnum.NewLoan ? (int)Enums.MatterTypeEnum.Production : (int)Enums.MatterTypeEnum.Discharge;

                if (feeLookup != null)
                {
                    feeDetails = GetFeeDetailsAmount(feeLookup, DateTime.Now, matterTypeId, matterWFCompView.LenderId, matterWFCompView.MortMgrId, null);

                    if (feeDetails == null)
                    {
                        feeDetails = GetFeeDetailsAmount(feeLookup, DateTime.Now, matterTypeId, matterWFCompView.LenderId, null, null);

                        if (feeDetails == null)
                        {
                            feeDetails = GetFeeDetailsAmount(feeLookup, DateTime.Now, matterTypeId, null, null, null);
                        }
                    }

                    if (feeDetails != null && feeDetails.Amount > 0)
                    {

                        MatterLedgerItem newMli = new MatterLedgerItem()
                        {
                            MatterId = matterWFCompView.MatterId,
                            LedgerItemSourceTypeId = (int)Slick_Domain.Enums.LedgerItemSourceTypeEnum.SettlementComplete,
                            Amount = feeDetails.Amount * fee.Item3,//number of copies of this fee to add
                            TransactionTypeId = (int)Slick_Domain.Enums.TransactionTypeEnum.Invoice,
                            PayableByTypeId = feeDetails.FeeDetail.PayableByTypeId ?? (int)Slick_Domain.Enums.PayableTypeEnum.Lender,
                            PayableToTypeId = feeDetails.FeeDetail.PayableToTypeId ?? (int)Slick_Domain.Enums.PayableTypeEnum.MSA,
                            PayableByAccountTypeId = (int)Slick_Domain.Enums.AccountTypeEnum.External,
                            PayableToAccountTypeId = (int)Slick_Domain.Enums.AccountTypeEnum.MSA,
                            MatterLedgerItemStatusTypeId = (int)Slick_Domain.Enums.MatterLedgerItemStatusTypeEnum.Ready,
                            FundsTransferredTypeId = (int)Slick_Domain.Enums.FundsTransferredTypeEnum.UnPaid,
                            Description = fee.Item2,
                            UpdatedByUserId = GlobalVars.CurrentUser.UserId,
                            UpdatedDate = DateTime.Now,
                            GSTFree = feeDetails.GSTFree
                        };
                        if(mv.MortMgrId == 49)
                        {
                            newMli.PayableByTypeId = (int)Slick_Domain.Enums.PayableTypeEnum.MortMgr;
                        }

                        if (!context.MatterLedgerItems.Any(x => x.MatterId == matterWFCompView.MatterId && x.Description == newMli.Description && x.TransactionTypeId == (int)Enums.TransactionTypeEnum.Invoice))
                        {
                            context.MatterLedgerItems.Add(newMli);
                            context.SaveChanges();
                        }
                        else
                        {
                            System.Console.WriteLine($"- Was going to add {newMli.Description} to {matterWFCompView.MatterId} but it was already there!");
                        }
                    }
                }
            }
            return true;

        }

        public FeeDetailsAmount GetFeeDetailsAmount(string feeName, DateTime feeDate, int? matterTypeId, int? lenderId, int? mortMgrId, int? stateId)
        {
            //CHANGE RQ 04-06-2019 to make fees work on the date of change
            var feeItems = context.FeeDetailsAmounts.Where(fda => fda.FeeDetail.Fee.FeeName == feeName && fda.FeeDetail.Enabled && (!fda.DateFrom.HasValue || fda.DateFrom.Value <= feeDate) && (!fda.DateTo.HasValue || fda.DateTo.Value >= feeDate))
                    .Select(f => new tmpFeeDet
                    {
                        fda = f,
                        Ranking = 0
                    }).ToList();

            if (matterTypeId.HasValue) feeItems.RemoveAll(x => x.fda.FeeDetail.MatterTypeId.HasValue && x.fda.FeeDetail.MatterTypeId != matterTypeId);
            if (lenderId.HasValue) feeItems.RemoveAll(x => x.fda.FeeDetail.LenderId.HasValue && x.fda.FeeDetail.LenderId != lenderId);
            if (mortMgrId.HasValue) feeItems.RemoveAll(x => x.fda.FeeDetail.MortMgrId.HasValue && x.fda.FeeDetail.MortMgrId != mortMgrId);
            if (stateId.HasValue) feeItems.RemoveAll(x => x.fda.FeeDetail.StateId.HasValue && x.fda.FeeDetail.StateId != stateId);

            foreach (var item in feeItems)
            {
                if (item.fda.FeeDetail.LenderId.HasValue && item.fda.FeeDetail.LenderId == lenderId) item.Ranking += 8;
                if (item.fda.FeeDetail.MortMgrId.HasValue && item.fda.FeeDetail.MortMgrId == mortMgrId) item.Ranking += 7;
                if (item.fda.FeeDetail.StateId.HasValue && item.fda.FeeDetail.StateId == stateId) item.Ranking += 6;
                if (item.fda.FeeDetail.MatterTypeId.HasValue && item.fda.FeeDetail.MatterTypeId == matterTypeId) item.Ranking += 5;
                if (!item.fda.FeeDetail.LenderId.HasValue) item.Ranking += 1;
                if (!item.fda.FeeDetail.MortMgrId.HasValue) item.Ranking += 1;
                if (!item.fda.FeeDetail.StateId.HasValue) item.Ranking += 1;
                if (!item.fda.FeeDetail.MatterTypeId.HasValue) item.Ranking += 1;
            }
            return feeItems.OrderByDescending(o => o.Ranking).FirstOrDefault()?.fda;
        }

        public decimal? GetFeeAmount(string feeName, DateTime feeDate, int? matterTypeId, int? lenderId, int? mortMgrId, int? stateId)
        {
            return GetFeeDetailsAmount(feeName, feeDate, matterTypeId, lenderId, mortMgrId, stateId)?.Amount;
        }

        public IEnumerable<EntityCompacted> GetFeesCompacted()
        {
            var fees = context.Fees.AsNoTracking().Where(x=>x.Enabled)
               .Select(m => new EntityCompacted { Id = m.FeeId, Details = m.FeeName })
               .OrderBy(o => o.Details)
               .ToList();

            return fees;
        }

        public IEnumerable<FeeListView> GetFeeList(int matterId)
        {
            var mt = context.Matters.AsNoTracking().FirstOrDefault(x => x.MatterId == matterId);
            return GetFeeList(mt.MatterGroupTypeId, mt.StateId, mt.LenderId, mt.MortMgrId);
        }

        public IEnumerable<FeeListView> GetFeeList(int matterTypeId, int stateId, int lenderId, int? mortMgrId)
        {

            //1 - Get list of possible Fees for this matter type
            List<Fee> feeList = new List<Fee>();

            switch (matterTypeId)
            {
                case (int)Enums.MatterGroupTypeEnum.NewLoan:
                    feeList = context.Fees.AsNoTracking().Where(x => x.Enabled && x.VisibleNewLoans).ToList();
                    break;
                case (int)Enums.MatterGroupTypeEnum.Discharge:
                    feeList = context.Fees.AsNoTracking().Where(x => x.Enabled && x.VisibleDischarges).ToList();
                    break;
                case (int)Enums.MatterGroupTypeEnum.Consent:
                    feeList = context.Fees.AsNoTracking().Where(x => x.Enabled && x.VisibleConsents).ToList();
                    break;

            }

            //2 - Get Best Fee details for each fee
            List<FeeListView> retFeeList = new List<FeeListView>();
            foreach (var feeItem in feeList.OrderBy(f => f.FeeName))
            {
                FeeListView flv = GetBestFeeListView(feeItem.FeeId, DateTime.Today, matterTypeId, stateId, lenderId, mortMgrId);
                if (flv != null)
                    retFeeList.Add(flv);
            }

            return retFeeList;
        }


        public FeeListView GetBestFeeListView(string feeName, DateTime feeDate, int? matterTypeId, int? stateId, int? lenderId, int? mortMgrId)
        {
            var feeId = context.Fees.FirstOrDefault(x => x.FeeName == feeName)?.FeeId;
            if (feeId.HasValue)
                return GetBestFeeListView(feeId.Value, feeDate, matterTypeId, stateId, lenderId, mortMgrId);
            else
                return null;
        }
        public FeeListView GetBestFeeListView(int feeId, DateTime feeDate, int? matterTypeId, int? stateId, int? lenderId, int? mortMgrId)
        {   
            //CHANGE RQ 04-06-2019 to make fees work on the date of change
            var feeListItems = context.FeeDetailsAmounts.Where(fda => fda.FeeDetail.Fee.FeeId == feeId && fda.FeeDetail.Enabled && (!fda.DateFrom.HasValue || fda.DateFrom.Value <= feeDate) && (!fda.DateTo.HasValue || fda.DateTo.Value >= feeDate)) 
                .Select(f => new BestFeeListView
                {
                    FeeId = f.FeeDetail.FeeId,
                    FeeDetailsId = f.FeeDetailsId,
                    FeeDetailsAmountId = f.FeeDetailsAmountId,
                    FeeName = f.FeeDetail.Fee.FeeName,
                    FeeDescription = f.FeeDetail.Fee.FeeDescription,
                    PayableByTypeId = f.FeeDetail.PayableByTypeId,
                    PayableByTypeName = f.FeeDetail.PayableType.PayableTypeName,
                    Amount = f.Amount,
                    GSTFree = f.GSTFree,
                    MatterTypeId = f.FeeDetail.MatterTypeId,
                    LenderId = f.FeeDetail.LenderId,
                    MortMgrId = f.FeeDetail.MortMgrId,
                    StateId = f.FeeDetail.StateId,
                    Ranking = 0
                }).ToList();

            if (matterTypeId.HasValue) feeListItems.RemoveAll(x => x.MatterTypeId.HasValue && x.MatterTypeId != matterTypeId);
            if (lenderId.HasValue) feeListItems.RemoveAll(x => x.LenderId.HasValue && x.LenderId != lenderId);
            if (mortMgrId.HasValue) feeListItems.RemoveAll(x => x.MortMgrId.HasValue && x.MortMgrId != mortMgrId);
            if (stateId.HasValue) feeListItems.RemoveAll(x => x.StateId.HasValue && x.StateId != stateId);

            foreach (var item in feeListItems)
            {
                if (item.LenderId.HasValue && item.LenderId == lenderId) item.Ranking += 8;
                if (item.MortMgrId.HasValue && item.MortMgrId == mortMgrId) item.Ranking += 7;
                if (item.StateId.HasValue && item.StateId == stateId) item.Ranking += 6;
                if (item.MatterTypeId.HasValue && item.MatterTypeId == matterTypeId) item.Ranking += 5;
                if (!item.LenderId.HasValue) item.Ranking += 1;
                if (!item.MortMgrId.HasValue) item.Ranking += 1;
                if (!item.StateId.HasValue) item.Ranking += 1;
                if (!item.MatterTypeId.HasValue) item.Ranking += 1;
            }

            if (feeListItems.Count == 0)
                return null;
            else
                return feeListItems.OrderByDescending(o => o.Ranking)
                    .Select(f => new FeeListView
                    {
                        FeeId = f.FeeId,
                        FeeDetailsId = f.FeeDetailsId,
                        FeeDetailsAmountId = f.FeeDetailsAmountId,
                        FeeName = f.FeeName,
                        FeeDescription = f.FeeDescription,
                        PayableByTypeId = f.PayableByTypeId,
                        PayableByTypeName = f.PayableByTypeName,
                        Amount = f.Amount,
                        GSTFree = f.GSTFree
                    })
                    .FirstOrDefault();

        }

        public void AddAgentFee(int matterId, string agentName, decimal agentFee, string notes, int? settlementStateId)
        {
            var addFee = new MatterLedgerItem();
            var mRep = new MatterRepository(context);
            var mv = mRep.GetMatterDetailsCompact(matterId);

            //For Macquarie additional rules apply to when Agent fee applies - we'll skip if these rules apply.
            if (mv.LenderId == 41)
            {
                if (settlementStateId == (int)Enums.StateIdEnum.ACT)
                {
                    return;
                }
                if (settlementStateId == (int)Enums.StateIdEnum.TAS)
                {
                    if (agentName.ToUpper().Contains("WALLACE WILKINSON & WEBSTER")) //sorry.
                    {
                        return;
                    }
                }
            }


            addFee.MatterId = matterId;
            addFee.TransactionTypeId = (int)Enums.TransactionTypeEnum.Cheque;
            addFee.LedgerItemSourceTypeId = (int) Enums.LedgerItemSourceTypeEnum.BookSettlement;

            if (mv.MatterGroupTypeId == (int)Enums.MatterGroupTypeEnum.NewLoan)
            {
                addFee.PayableByAccountTypeId = (int)Enums.AccountTypeEnum.Trust;
                addFee.PayableByTypeId = (int)Enums.PayableTypeEnum.Borrower;
            }
            else
            {
                addFee.PayableByAccountTypeId = (int)Enums.AccountTypeEnum.External;
                addFee.PayableByTypeId = (int)Enums.PayableTypeEnum.Borrower;
            }
            addFee.PayableToAccountTypeId = (int)Enums.AccountTypeEnum.External;
            addFee.PayableToTypeId = (int) Enums.PayableTypeEnum.OtherParty;
            addFee.PayableToOthersDesc = agentName;
            
            addFee.Amount = agentFee;
            addFee.GSTFree = false;
            addFee.Description = "Settlement Agent Fee";
            addFee.PaymentNotes = notes;
            addFee.Instructions = "Agent to Retain";
            addFee.FundsTransferredTypeId = (int)Enums.FundsTransferredTypeEnum.UnPaid;
            addFee.ChequePrepared = false;
            addFee.MatterLedgerItemStatusTypeId = (int)Enums.MatterLedgerItemStatusTypeEnum.Ready;
            addFee.UpdatedDate = DateTime.Now;
            addFee.UpdatedByUserId = GlobalVars.CurrentUser.UserId;

            context.MatterLedgerItems.Add(addFee);
            context.SaveChanges();

        }

        public void AddFee(int matterId, string feeName, bool addIfExists)
        {
            var feeDet = GetFeeDetails(matterId, feeName);

            if (feeDet != null)
            {
                if (!addIfExists)
                {

                }
            }
        }

        public decimal GetFeeForDisbursementOption(int matterId, string disbursementOption)
        {
            if (!String.IsNullOrEmpty(disbursementOption)) disbursementOption = disbursementOption.Trim();
           
            var mt = context.Matters.AsNoTracking().FirstOrDefault(m => m.MatterId == matterId);
            if (mt == null)
                return 0;

            if (mt.MortMgrId.HasValue)
            {
                var mdoFee = context.MortMgrDisbursementOptions.AsNoTracking().Where(m => m.MortMgrId == mt.MortMgrId.Value && m.DisbursementOption.ToUpper() == disbursementOption.ToUpper())
                                        .Select(m => m.Fee.FeeName).FirstOrDefault();
                if (!String.IsNullOrWhiteSpace(mdoFee))
                {
                    var amt = GetFeeAmount(mdoFee, DateTime.Today, mt.MatterGroupTypeId, mt.LenderId, mt.MortMgrId, mt.StateId);
                    return amt.HasValue ? amt.Value : 0;
                }
            }

            //If we're here, then no MortMgr Fee found... search by Lender
            var ldoFee = context.LenderDisbursementOptions.AsNoTracking().Where(m => m.LenderId == mt.LenderId && m.DisbursementOption.ToUpper() == disbursementOption.ToUpper())
                                    .Select(m => m.Fee.FeeName).FirstOrDefault();
            if (!String.IsNullOrWhiteSpace(ldoFee))
            {
                var amt = GetFeeAmount(ldoFee, DateTime.Today, mt.MatterGroupTypeId, mt.LenderId, mt.MortMgrId, mt.StateId);
                return amt.HasValue ? amt.Value : 0;
            }

            return 0;
        }
    }
}
