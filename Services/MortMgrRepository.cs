using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Slick_Domain.Models;
using Slick_Domain.Interfaces;
using Slick_Domain.Entities;
using Slick_Domain.Common;

namespace Slick_Domain.Services
{
    public class MortMgrRepository : IDisposable
    {
        private IRepository<MortMgr> mortMgrRepository = null;
        private readonly SlickContext context;

        public MortMgrRepository(SlickContext Context)
        {
            context = Context;
        }

        private IRepository<MortMgr> GetMortMgrRepository()
        {
            return mortMgrRepository ?? new Repository<MortMgr>(context);
        }

        public IEnumerable<MortMgrCustomEntities.MortMgrView> GetMortMgrsView()
        {
            List<MortMgrCustomEntities.MortMgrView> MortMgrs = context.MortMgrs.AsNoTracking()
                .Select(s2 =>
                    new
                    {
                        s2.StreetAddress,
                        s2.MortMgrId,
                        s2.MortMgrName,
                        s2.MortMgrNameShort,
                        s2.PrimaryContact.Email,
                        s2.Enabled,
                        s2.PrimaryContact.Phone,
                        s2.PrimaryContact.Firstname,
                        s2.PrimaryContact.Lastname,
                        s2.PrimaryContact.Fax,
                        s2.PrimaryContact.Mobile,
                        s2.State1.StateName,
                        s2.Suburb,
                        s2.PostCode,
                        PostalState = s2.State.StateName,
                        s2.CompanyABN,
                        s2.CompanyACLN,
                        s2.CompanyACN,
                        s2.PayeeName,
                        s2.PostalAddress,
                        s2.PostalPostCode,
                        s2.PostalSuburb,
                        s2.User.Username,
                        s2.UpdatedDate,
                        s2.DigiDocsCCEmail,
                        s2.DigiDocs,
                        s2.CoBranding,
                        MortMgrLenders = s2.MortMgrLenders.Where(x => x.MortMgrId == s2.MortMgrId).
                              Select(y => new MortMgrCustomEntities.MortMgrLenderView { Id = y.MortMgrLenderId,
                                  LenderName = y.Lender.LenderName, HotDocsCode = y.LenderMortMgrRef
                              })
                    })
                .ToList()
                .Select(st => new MortMgrCustomEntities.MortMgrView
                {
                    MortMgrId = st.MortMgrId,
                    AddressDetails = st.StreetAddress,
                    MortMgrName = st.MortMgrName,
                    MortMgrNameShort = st.MortMgrNameShort,
                    PostalStateName = st.PostalState,
                    Fax = st.Fax,
                    Mobile = st.Mobile,                   
                    Email = st.Email,
                    Phone = st.Phone,
                    StateName = st.StateName,
                    Suburb = st.Suburb,
                    PostCode = st.PostCode,
                    ContactLastname = st.Lastname,
                    ContactFirstname = st.Firstname,
                    PrimaryContact = EntityHelper.GetFullName(st.Lastname, st.Firstname),
                    CompanyABN = st.CompanyABN,
                    CompanyACLN = st.CompanyACLN,
                    CompanyACN = st.CompanyACN,
                    PayeeName = st.PayeeName,
                    PostalAddress = st.PostalAddress,
                    PostalPostCode = st.PostalPostCode,
                    PostalSuburb = st.PostalSuburb,
                    IsEnabled = st.Enabled,
                    UpdatedBy = st.Username,
                    UpdatedDate = st.UpdatedDate,
                    MortMgrLenders = st.MortMgrLenders,
                    DigiDocsCCEmail = st.DigiDocsCCEmail,
                    CoBranding = st.CoBranding,
                    DigiDocs = st.DigiDocs
                })
                .ToList();

            return MortMgrs;
        }

        public MortMgrCustomEntities.MortMgrView GetMortMgrView(int id)
        {
            var MortMgr = GetMortMgrRepository().FindById(id);

            if (MortMgr == null) return null;
            var MortMgrView = new MortMgrCustomEntities.MortMgrView
            {
                MortMgrId = MortMgr.MortMgrId,
                PrimaryContactId = MortMgr.PrimaryContactId,
                MortMgrNameShort = MortMgr.MortMgrNameShort,
                SSORequiredDefault = MortMgr.SSORequiredDefault,
                MortMgrDisplayCode = MortMgr.MortMgrDisplayCode,
                AddressDetails = MortMgr.StreetAddress,
                CustomSignaturePath = MortMgr.MortMgrSignatureDirectory,
                MortMgrName = MortMgr.MortMgrName,
                DigiDocsCCEmail = MortMgr.DigiDocsCCEmail,
                CoBranding = MortMgr.CoBranding,
                DigiDocs = MortMgr.DigiDocs,
                Email = MortMgr.PrimaryContact?.Email,
                Phone = MortMgr.PrimaryContact?.Phone,
                Fax = MortMgr.PrimaryContact?.Fax,
                Mobile = MortMgr.PrimaryContact?.Mobile,
                StateId = MortMgr.StateId,
                PostalStateId = MortMgr.PostalStateId,
                StateName = MortMgr.State1?.StateName,
                FundingChannelTypeId = MortMgr.MortMgrFundingChannelTypeId,
                PostalStateName = MortMgr.State?.StateName,
                PostCode = MortMgr.PostCode,
                PrimaryContact = EntityHelper.GetFullName(MortMgr.PrimaryContact?.Lastname, MortMgr.PrimaryContact?.Firstname),
                Suburb = MortMgr.Suburb,
                ContactLastname = MortMgr.PrimaryContact?.Lastname,
                ContactFirstname = MortMgr.PrimaryContact?.Firstname,
                CompanyABN = MortMgr.CompanyABN,
                CompanyACLN = MortMgr.CompanyACLN,
                CompanyACN = MortMgr.CompanyACN,
                PayeeName = MortMgr.PayeeName,
                PostalAddress = MortMgr.PostalAddress,
                PostalPostCode = MortMgr.PostalPostCode,
                PostalSuburb = MortMgr.PostalSuburb,
                IsEnabled = MortMgr.Enabled,
                UpdatedDate = MortMgr.UpdatedDate,
                UpdatedBy = MortMgr.User.Username,
                IsParent = MortMgr.IsParent,
                SendPostSettlementLetter = MortMgr.SendPostSettlementLetter,
                ParentMortMgrId = MortMgr.ParentMortMgrId,
                MortMgrTypeId = MortMgr.MortMgrTypeId,

                SmartVidPhone = MortMgr.SmartVidPhone,
                SmartVidEmail = MortMgr.SmartVidEmail,
                SmartVidContactURL = MortMgr.SmartVidContactURL,
                SmartVidContactIconPath = MortMgr.SmartVidContactIconPath,
                SmartVidBannerPath = MortMgr.SmartVidBannerPath,
                SmartVidStylePath = MortMgr.SmartVidStylePath,
                SmartVidMortMgrName = MortMgr.SmartVidMortMgrName,
                SmartVidMortMgrAudioName = MortMgr.SmartVidMortMgrAudioName,
                SmartVidMortMgrContactAudioName = MortMgr.SmartVidMortMgrContactAudioName,

                SendEstimatedCostsStatement = MortMgr.SendEstimatedCostsStatement,

                OverrideLenderOnHoldEmail = MortMgr.OverrideLenderOnHoldEmail,
                SendOnHoldEmailLender = MortMgr.SendOnHoldEmailLender,
                SendOnHoldEmailBroker = MortMgr.SendOnHoldEmailBroker,
                SendOnHoldEmailMortMgr = MortMgr.SendOnHoldEmailMortMgr,
                SendOnHoldEmailBorrower = MortMgr.SendOnHoldEmailBorrower,
                SendOnHoldEmailOtherParty = MortMgr.SendOnHoldEmailOtherParty,
                SendOnHoldEmailSecondaryContact = MortMgr.SendOnHoldEmailSecondaryContact,
                SendOnHoldEmailFileOwner = MortMgr.SendOnHoldEmailFileOwner,
                SendOnHoldEmailCustomEmail = MortMgr.SendOnHoldEmailCustomEmail,

                MortMgrLenders = MortMgr.MortMgrLenders.Select(x => new MortMgrCustomEntities.MortMgrLenderView
                {
                    Id = x.MortMgrLenderId,
                    LenderId = x.LenderId,
                    LenderName = x.Lender.LenderName,
                    HotDocsCode = x.LenderMortMgrRef
                }).ToList()
            };

            return MortMgrView;
        }

        public IEnumerable<MortMgrCustomEntities.MortMgrLenderView> GetMortMgrLenders(int id)
        {
            return context.MortMgrLenders
                    .Where(l => l.MortMgrId == id)
                    .Select(m => new { m.MortMgrLenderId, m.Lender.LenderId, m.Lender.LenderName, m.LenderMortMgrRef })
                    .ToList()
                    .Select(x => new MortMgrCustomEntities.MortMgrLenderView
                    {
                        Id = x.MortMgrLenderId,
                        LenderId = x.LenderId,
                        LenderName = x.LenderName,
                        HotDocsCode = x.LenderMortMgrRef
                    });
        }

        public IEnumerable<MortMgrCustomEntities.MortMgrDisbursementOptionView> GetMortMgrDisbursementOptionList(int mortMgrId, bool isFunding)
        {
            return
                context.MortMgrDisbursementOptions.Where(x => x.MortMgrId == mortMgrId && x.IsFunding == isFunding)
                .Select(x => new MortMgrCustomEntities.MortMgrDisbursementOptionView
                {
                    MortMgrDisbursementOptionId = x.MortMgrDisbursementOptionId,
                    MortMgrId = x.MortMgrId,
                    DisbursementOption = x.DisbursementOption,
                    FeeId = x.FeeId.HasValue ? x.FeeId : -1,
                    FeeName = x.Fee.FeeName,
                    EFT_AccountName = x.EFT_AccountName,
                    EFT_BSB = x.EFT_BSB,
                    EFT_AccountNo = x.EFT_AccountNo,
                    IsVisible = x.IsVisible,
                    DisplayOrder = x.DisplayOrder,
                    UpdatedDate = x.UpdatedDate,
                    UpdatedByUserId = x.UpdatedByUserId,
                    UpdatedByUsername = x.User.Username,
                    IsDirty = false
                }).ToList();
        }



        public IEnumerable<GeneralCustomEntities.GeneralCheckList> GetGeneralCheckList(IEnumerable<GeneralCustomEntities.GeneralCheckList> lenderList, bool checkedValue, bool inclSelectAll, bool inclNoMortMgr)
        {
            try
            {
                var Ids = lenderList.Where(l => l.IsChecked).Select(l => l.Id).ToArray();

                var gg = (from m in context.MortMgrs
                          join ml in context.MortMgrLenders.Where(p => Ids.Contains(p.LenderId)).AsEnumerable() on m.MortMgrId equals ml.MortMgrId
                          select new GeneralCustomEntities.GeneralCheckList { IsChecked = checkedValue, Id = m.MortMgrId, Name = m.MortMgrName })
                          .OrderBy(o => o.Name).ToList();

                if (inclNoMortMgr)
                    gg.Insert(0, new GeneralCustomEntities.GeneralCheckList(checkedValue, 0, "-- No Mort Mgr --"));
                if (inclSelectAll)
                    gg.Insert(0, new GeneralCustomEntities.GeneralCheckList(checkedValue, -1, "-- Select All --"));

                return gg;
            }
            catch (Exception ex)
            {
                Handlers.ErrorHandler.LogError(ex);
                return null;
            }
        }

        public List<LookupValue> GetLookupList() {
            return (from mm in context.MortMgrs
                select new LookupValue() { id = mm.MortMgrId, value = mm.MortMgrName }).ToList();
        }
        

        public List<LookupValue> GetLenderMortMgrLookupList(User user)
        {
            return (user.MortMgrs.Select(mm => new LookupValue() { id = mm.MortMgrId, value = mm.MortMgrName }).ToList());
        }

        public IEnumerable<EntityCompacted> GetChildMortMgrs(int mortMgrId)
        {
            return context.MortMgrs.Where(i => i.ParentMortMgrId == mortMgrId).Select(x => new EntityCompacted { Id = x.MortMgrId, Details = x.MortMgrName });
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

        public IEnumerable<MortMgr> GetMortMgrs(int? lenderID)
        {
            var mortMgrs = context.MortMgrs.AsNoTracking().Where(x => x.Enabled == true);
            if(lenderID.HasValue)
            {
                mortMgrs = from m in mortMgrs
                           join l in context.MortMgrLenders on m.MortMgrId equals l.MortMgrId
                           where l.LenderId == lenderID
                           select m; 
            }
            return mortMgrs.OrderBy(x => x.MortMgrName).ToList();
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