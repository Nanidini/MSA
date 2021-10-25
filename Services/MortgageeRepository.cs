using Slick_Domain.Common;
using Slick_Domain.Entities;
using Slick_Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Slick_Domain.Services
{
    public class MortgageeRepository : IDisposable
    {
        private readonly SlickContext context;

        public MortgageeRepository(SlickContext Context)
        {
            context = Context;
        }

        public IEnumerable<MortgageeView> GetMortgagees()
        {
           return
             context.Mortgagees
                .Select(m => new MortgageeView
                {
                    MortgageeId = m.MortgageeId,
                    MortgageeName = m.MortgageeName,
                    MortgageeNameShort = m.MortgageeNameShort,
                    CompanyABN = m.CompanyABN,
                    CompanyACLN = m.CompanyACLN,
                    CompanyACN = m.CompanyACN,
                    Level = m.Level,
                    StreetNo = m.StreetNo,
                    StreetName = m.StreetName,
                    StreetType = m.StreetType,
                    Suburb = m.Suburb,
                    StateName = m.State,
                    PostCode = m.Postcode,
                    ApcaUserId = m.APCAUserId,

                    MortgageeLenders = m.MortgageeLenders
                    .Select(ml => new MortgageeLenderView
                    {
                          MortgageeLenderId = ml.MortgageeLenderId,
                          MortgageeId = ml.MortgageeId,
                          LenderId = ml.LenderId,
                          LenderName = ml.Lender.LenderName,
                          MortgageeLenderMems = ml.MortgageeLenderMems
                                .Select(mm => new MortgageeLenderMemView
                                {
                                    MortgageeLenderMemId = mm.MortgageeLenderMemId,
                                    MortgageeLenderId = mm.MortgageeLenderId,
                                    Category = mm.Category,
                                    MortMemNoNSW = mm.MortMemNoNSW,
                                    MortMemNoVIC = mm.MortMemNoVIC,
                                    MortMemNoQLD = mm.MortMemNoQLD,
                                    MortMemNoSA = mm.MortMemNoSA,
                                    MortMemNoWA = mm.MortMemNoWA,
                                    MortMemNoTAS = mm.MortMemNoTAS,
                                    MortMemNoACT = mm.MortMemNoACT,
                                    MortMemNoNT = mm.MortMemNoNT                                    
                                }),
                          PoaACTNo = ml.PoAACTNo,
                          PoaNSWBook = ml.PoANSWBook,
                          PoaNSWNo = ml.PoANSWNo,
                          PoaNTNo = ml.PoANTNo,
                          PoaQLDDate = ml.PoAQLDDate,
                          PoaQLDNo = ml.PoAQLDNo,
                          PoaSADate = ml.PoASADate,
                          PoaSANo = ml.PoASANo,
                          PoaVICBook = ml.PoAVICBook,
                          PoaVICDate = ml.PoAVICDate,
                          PoaVICItem = ml.PoAVICItem,
                          PoaVICPage = ml.PoAVICPage,
                          PoaWADate = ml.PoAWADate,
                          PoaWANo = ml.PoAWANo,
                          PoaTASDate = ml.PoATASDate,
                          PoaTASNo = ml.PoATASNo,
                          UpdatedByUserId = ml.UpdatedByUserId,
                          UpdatedByUsername = ml.User.Username,
                          UpdatedDate = ml.UpdatedDate
                    })
                });
        }

        public IEnumerable<BankAccountsView> GetBankAccounts()
        {
            return context.BankAccounts.AsNoTracking()
                 .Select(s2 =>
                     new
                     {
                         s2.AccountName,
                         s2.AccountNo,
                         s2.AccountType,
                         s2.Bank,
                         s2.BankAccountId,
                         s2.BSB,
                         s2.LenderId,
                         s2.Lender.LenderName,
                         s2.MortgageeId,
                         s2.Mortgagee.MortgageeName,
                         s2.MortMgrId,
                         s2.MortMgr.MortMgrName,
                         s2.OtherOrgName,
                         s2.UpdatedByUserId,
                         s2.User.Username,
                         s2.UpdatedDate
                  })
                 .ToList()
                 .Select(ba => new BankAccountsView
                 {
                     AccountName = ba.AccountName,
                     AccountNo = ba.AccountNo,
                     AccountType = ba.AccountType,
                     Bank = ba.Bank,
                     BankAccountId = ba.BankAccountId,
                     BSB = ba.BSB,
                     LenderId = ba.LenderId ?? DomainConstants.AnySelectionId,
                     LenderName = ba.LenderName ?? DomainConstants.AnySelection,
                     MortgageeId = ba.MortgageeId ?? DomainConstants.AnySelectionId,
                     MortgageeName = ba.MortgageeName ?? DomainConstants.AnySelection,
                     MortMgrId = ba.MortMgrId ?? DomainConstants.AnySelectionId,
                     MortMgrName = ba.MortMgrName ?? DomainConstants.AnySelection,
                     OtherOrgName = ba.OtherOrgName,
                     UpdatedByUserId = ba.UpdatedByUserId,
                     UpdatedByUsername = ba.Username,
                     UpdatedDate = ba.UpdatedDate
                 })
                 .ToList();

        }

        public string GetMortgageeForLenderACN(int lenderId, string mtgeeACN, ref int? mtgeeId)
        {
            var lenders = from m in context.Mortgagees
                          join l in context.MortgageeLenders on m.MortgageeId equals l.MortgageeId
                          where l.LenderId == lenderId
                          select m;

            if (!lenders.Any()) return null;
            if (lenders.Count() == 1)
            {
                mtgeeId = lenders.First().MortgageeId;
                return lenders.First().MortgageeName;
            }
            else
            {
                if (string.IsNullOrEmpty(mtgeeACN)) return null;

                mtgeeACN = mtgeeACN.Replace(" ", "");
                var lender = lenders.FirstOrDefault(x => x.CompanyACN.Replace(" ", "") == mtgeeACN || x.CompanyABN.Replace(" ", "") == mtgeeACN);
                mtgeeId = lender?.MortgageeId;
                return lender?.MortgageeName;
            }                
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
