using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Slick_Domain.Interfaces;
using Slick_Domain.Entities;
using Slick_Domain.Models;
using System.ComponentModel;

namespace Slick_Domain.Services
{
    public class LenderRepository : SlickRepository
    {
        public LenderRepository(SlickContext context) : base(context)
        {

        }
        public IEnumerable<LenderOfficeHoursView> GetLenderOfficeHours(int lenderId)
        {
            return context.LenderOfficeHours.Where(l => l.LenderId == lenderId).Select(x => new LenderOfficeHoursView()
            {
                isDirty = false,
                LenderOfficeHoursId = x.LenderOfficeHoursId,
                LenderId = x.LenderId,
                StateId = x.StateId,
                OpeningTime = x.OpeningTime,
                ClosingTime = x.ClosingTime,
                UpdatedByUserId = x.UpdatedByUserId,
                UpdatedDate = x.UpdatedDate,
            });
        }
        private IEnumerable<LenderCustomEntities.LenderView> GetLendersView(IQueryable<Lender> ldrQry)
        {
            try
            {
                return ldrQry
                            .Select(l => new
                            {
                                l.LenderId,
                                l.LenderName,
                                l.LenderNameShort,
                                l.LenderDisplayCode,
                                l.InvoiceEmail,
                                l.SecondaryRefName,
                                l.SecondaryRefRequired,
                                l.LenderSignatureDirectory,
                                l.HighFundingRestriction,
                                l.StreetAddress,
                                l.SSORequiredDefault,
                                l.Suburb,
                                l.StateId,
                                l.State1.StateName,
                                l.PostCode,
                                l.PostalAddress,
                                l.PostalSuburb,
                                l.PostalStateId,
                                PostalStateName = l.State.StateName,
                                l.PostalPostCode,
                                l.PrimaryContact.Lastname,
                                l.PrimaryContact.Firstname,
                                l.PrimaryContact.Phone,
                                l.PrimaryContact.Mobile,
                                l.PrimaryContact.Fax,
                                l.PrimaryContact.Email,
                                l.HotDocsCode,
                                l.Enabled,
                                l.UpdatedDate,
                                l.UpdatedByUserId,
                                l.User.Username,
                                l.XMLEnabled,
                                l.PrimaryContactId,
                                l.DefaultMortMgrId,
                                l.CanChangeDefaultMortMgr,
                                l.BackChannelEnable,
                                l.SmartVidEnabled,
                                l.SettlementPendDays,
                                l.NumberFreeCheques,
                                l.MatterOnHoldSLATypeId,
                                l.LoanTrakFilterDays,
                                l.LoanExpiryDays,
                                l.RelationshipManagerApplicable,
                                l.SlickListRequiredToClose,
                                l.NewLoanArchiveRequired,
                                l.NewLoanSecPacketRequired,
                                l.DischargeArchiveRequired,
                                l.DischargeSecPacketRequired,
                                l.StrataInsuranceRequired, 
                                l.ExistingInsuranceRequired,
                                l.CertReportName,
                                l.CertRequiresInsurance,
                                l.HasReportCertification,
                                l.HasCertificationEmail,
                                l.CertificationEmailAddress,
                                l.InstructionDeadlineOffsetDays,
                                l.InsurancePolicyAmountRequired,
                                l.InsurancePolicyCompanyRequired,
                                l.InsurancePolicyNoRequired,
                                l.InsurancePolicyTypeRequired,
                                l.SendOnHoldEmailBorrower,
                                l.SendOnHoldEmailBroker,
                                l.SendOnHoldEmailCustomEmail,
                                l.SendOnHoldEmailFileOwner,
                                l.SendOnHoldEmailLender,
                                l.SendOnHoldEmailMortMgr,
                                l.SendOnHoldEmailOtherParty,
                                l.SendOnHoldEmailSecondaryContact,
                                l.DischargesRequireBroker

                            })
                            .ToList()
                            .Select(l => new LenderCustomEntities.LenderView(l.LenderId, l.LenderName, l.LenderNameShort, l.SecondaryRefName, l.SecondaryRefRequired, l.HighFundingRestriction, l.StreetAddress, l.Suburb, l.StateId, l.StateName, l.PostCode, l.PostalAddress,
                            l.PostalSuburb, l.PostalStateId, l.PostalStateName, l.PostalPostCode, l.Lastname, l.Firstname, l.Phone, l.Mobile, l.Fax, l.Email, l.HotDocsCode, l.Enabled, l.UpdatedDate, l.UpdatedByUserId, l.Username, l.XMLEnabled, l.PrimaryContactId, l.DefaultMortMgrId, l.CanChangeDefaultMortMgr, l.BackChannelEnable,l.SmartVidEnabled, l.SettlementPendDays, l.NumberFreeCheques, l.MatterOnHoldSLATypeId, l.InvoiceEmail, l.LoanExpiryDays, l.LoanTrakFilterDays, 
                            l.RelationshipManagerApplicable, l.SlickListRequiredToClose, l.NewLoanArchiveRequired, l.NewLoanSecPacketRequired, l.DischargeArchiveRequired, l.DischargeSecPacketRequired, l.StrataInsuranceRequired, l.ExistingInsuranceRequired, l.HasReportCertification, l.CertRequiresInsurance, l.CertReportName, l.HasCertificationEmail, l.CertificationEmailAddress, l.InstructionDeadlineOffsetDays, l.InsurancePolicyAmountRequired, l.InsurancePolicyCompanyRequired, l.InsurancePolicyNoRequired, l.InsurancePolicyTypeRequired,
                            l.SendOnHoldEmailLender, l.SendOnHoldEmailMortMgr, l.SendOnHoldEmailBroker, l.SendOnHoldEmailBorrower, l.SendOnHoldEmailOtherParty,
                            l.SendOnHoldEmailSecondaryContact, l.SendOnHoldEmailFileOwner, l.SendOnHoldEmailCustomEmail, l.LenderDisplayCode, l.DischargesRequireBroker, l.SSORequiredDefault, l.LenderSignatureDirectory
                            ))
                            .ToList();
            }
            catch (Exception ex)
            {
                Handlers.ErrorHandler.LogError(ex);
                return null;
            }
        }
        public IEnumerable<LenderCustomEntities.LenderView> GetLendersView()
        {
            try
            {
                var qry = context.Lenders.AsNoTracking();
                return GetLendersView(qry);
            }
            catch (Exception ex)
            {
                Handlers.ErrorHandler.LogError(ex);
                return null;
            }
        }

        public IEnumerable<LenderCustomEntities.LenderView> GetLendersCurrView()
        {
            try
            {
                var qry = context.Lenders.AsNoTracking().Where(l => l.Enabled);
                return GetLendersView(qry);
            }
            catch (Exception ex)
            {
                Handlers.ErrorHandler.LogError(ex);
                return null;
            }
        }

        public LenderCustomEntities.LenderView GetLenderView(int lenderId)
        {
            try
            {
                var qry = context.Lenders.AsNoTracking().Where(l => l.LenderId == lenderId);
                return GetLendersView(qry).FirstOrDefault();
            }
            catch (Exception ex)
            {
                Handlers.ErrorHandler.LogError(ex);
                return null;
            }
        }


        private IEnumerable<EntityCompacted> GetLendersCompactView(IQueryable<Lender> ldrQry)
        {
            try
            {
                return ldrQry
                            .Select(l => new {
                                l.LenderId,
                                l.LenderName,
                            })
                            .ToList()
                            .Select(l => new EntityCompacted(l.LenderId, l.LenderName))
                            .ToList();
            }
            catch (Exception ex)
            {
                Handlers.ErrorHandler.LogError(ex);
                return null;
            }
        }

        public IEnumerable<EntityCompacted> GetLendersCompactView()
        {
            try
            {
                var qry = context.Lenders.AsNoTracking();
                return GetLendersCompactView(qry);
            }
            catch (Exception ex)
            {
                Handlers.ErrorHandler.LogError(ex);
                return null;
            }
        }

        public IEnumerable<EntityCompacted> GetLendersCurrCompactView()
        {
            try
            {
                var qry = context.Lenders.AsNoTracking().Where(l => l.Enabled);
                return GetLendersCompactView(qry);
            }
            catch (Exception ex)
            {
                Handlers.ErrorHandler.LogError(ex);
                return null;
            }
        }

        public IEnumerable<GeneralCustomEntities.GeneralCheckList> GetGeneralCheckList(bool defValue, bool inclSelectAll)
        {
            try
            {
                var ldrsTmp = context.Lenders
                            .Where(l => l.Enabled == true)
                            .Select(l => new { l.LenderId, l.LenderName })
                            .OrderBy(o => o.LenderName)
                            .ToList()
                            .Select(l => new GeneralCustomEntities.GeneralCheckList(defValue, l.LenderId, l.LenderName))
                            .ToList();


                if (inclSelectAll)
                    ldrsTmp.Insert(0, new GeneralCustomEntities.GeneralCheckList(defValue, -1, "-- All Lenders --"));

                return ldrsTmp;
            }
            catch (Exception ex)
            {
                Handlers.ErrorHandler.LogError(ex);
                return null;
            }

        }

        public Boolean LenderNameUnique(Int32? lenderId, String lenderName)
        {
            try
            {
                var lndrs = context.Lenders.Where(l => l.LenderName == lenderName);
                if (lenderId.HasValue)
                    lndrs = lndrs.Where(l => l.LenderId != lenderId.Value);

                Lender lndr = lndrs.FirstOrDefault();
                if (lndr != null)
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

        public void Delete(Int32 lenderId)
        {
            var mmls = context.MortMgrLenders.Where(m => m.LenderId == lenderId);
            context.MortMgrLenders.RemoveRange(mmls);

            Lender ldr = context.Lenders.Find(lenderId);
            context.Lenders.Remove(ldr);
            context.SaveChanges();
        }

        public List<LookupValue> GetLookupList() {

            return (from l in context.Lenders
                select new LookupValue() {id = l.LenderId, value = l.LenderName}).ToList();
        }

        public List<LookupValue> GetLookupList(User user)
        {

            return (from l in context.Lenders
                    where l.LenderId == user.LenderId
                    select new LookupValue() { id = l.LenderId, value = l.LenderName }).ToList();
        }

        public List<LookupValue> GetMortMgrLookupList(User user)
        {
            return user.Lenders.Select(l=> new LookupValue() { id = l.LenderId, value = l.LenderName } ).ToList();
        }
        public IEnumerable<LenderReturnFundsAccountView> GetLenderReturnFundsAccounts(int lenderId)
        {
            return context.LenderReturnFundsAccounts
                .Where(l => l.LenderId == lenderId)
                .Select(x => new LenderReturnFundsAccountView
                {
                    LenderReturnFundsAccountId = x.LenderReturnFundsAccountId,
                    LenderId = x.LenderId,
                    DisplayName = x.DisplayName,
                    AccountName = x.AccountName,
                    AccountNumber = x.AccountNumber,
                    BSB = x.BSB,
                    Reference = x.Reference,
                    UpdatedByUserId = x.UpdatedByUserId,
                    UpdatedDate = x.UpdatedDate,
                }).ToList();
        }

        public IEnumerable<LenderTrustDetailsView> GetLenderTrustDetails(int lenderId, int? mortMgrId = null, bool showDisabled = false)
        {
            return context.LenderTrustAccountDetails.Where(l => l.LenderId == lenderId && (!mortMgrId.HasValue || (l.MortMgrId == mortMgrId.Value || !l.MortMgrId.HasValue)) && (showDisabled || l.Enabled))
                .Select(t=> new LenderTrustDetailsView()
                {
                    LenderTrustAccountDetailId = t.LenderTrustAccountDetailId,
                    LenderId = t.LenderId,
                    MortMgrId = t.MortMgrId,
                    MatterGroupTypeId = t.MatterGroupTypeId,
                    EFT_AccName = t.EFT_AccName,
                    EFT_AccNo = t.EFT_AccNo,
                    EFT_BSB = t.EFT_BSB,
                    AccountRefNo = t.TrustAccRefNo,
                    UpdatedByUserId = t.UpdatedByUserId,
                    UpdatedByUsername = t.User.Username,
                    UpdatedDate = t.UpdatedDate,
                    Enabled = t.Enabled, 
                    isDirty = false
                });
        }
        public IEnumerable<LenderLetterToCustodianDetailView> GetLenderLetterToCustodianDetails(int lenderId)
        {
            return context.LenderLetterToCustodianDetails.Where(x => x.LenderId == lenderId).Select(x => new LenderLetterToCustodianDetailView
            {
                LenderLetterToCustodianDetailId = x.LenderLetterToCustodianDetailId,
                LenderId = x.LenderId ,
                MortMgrId = x.MortMgrId ?? -1,
                FundingChannelTypeId = x.FundingChannelTypeId ?? -1,
                ReportServerReportName = x.ReportServerReportName,
                CustodianAddressDetails = x.CustodianAddressDetails,
                IsDirty = false,
                IsReadOnly = false,
                UpdatedByUserId = x.UpdatedByUserId,
                UpdatedDate = x.UpdatedDate,
                UpdatedByUserName = x.User.Username
            }).ToList();
        }

        public IEnumerable<LenderReplyAddressView> GetLenderReplyAddresses(int lenderId)
        {
            return context.LenderReplyAddresses.Where(x => x.LenderId == lenderId).Select(x => new LenderReplyAddressView()
            {
                isDirty = false,
                LenderReplyAddressId = x.LenderReplyAddressId,
                LenderId = x.LenderId,
                MortMgrId = x.MortMgrId,
                ReplyAddressTypeId = x.ReplyAddressTypeId,
                Email = x.Email, 
                UpdatedByUserId = x.UpdatedByUserId 
            })
            .Where(l=>l.LenderId == lenderId)
            .ToList();
        }


        public IEnumerable<LenderDischargePayoutEmailAddressView> GetLenderDischargePayoutEmailAddresses(int lenderId)
        {
            return context.LenderDischargePayoutEmailAddresses.Where(l=>l.LenderId == lenderId).Select(x => new LenderDischargePayoutEmailAddressView()
            {
                IsDirty = false,
                LenderDischargePayoutEmailAddressId = x.LenderDischargePayoutEmailAddressId,
                EmailAddress = x.EmailAddressStr,
                LenderId = x.LenderId,
                UpdatedByUserId = x.UpdatedByUserId,
                UpdatedDate = x.UpdatedDate,
                UpdatedByUserName = x.User.Username,
                DischargeTypeId = x.DischargeTypeId
            });
        }

        public IEnumerable<LenderDocumentMasterView> GetLenderDocuments(int lenderId, bool showDeletedDocs)
        {
            return context.LenderDocumentMasters.Where(l => l.LenderId == lenderId && (l.Deleted == false || showDeletedDocs))
                         .Select(d => new
                         {
                             d.LenderDocumentMasterId,
                             LenderId = lenderId,
                             Deleted = d.Deleted,
                             isDirty = false,
                             LatestLenderDocumentVersionDetails = d.LenderDocumentVersions.Where(v => v.IsLatestVersion).Select(v => new { v.LenderDocumentVersionId, v.DocumentName, v.DocType, v.VersionNo }).FirstOrDefault(),
                             VersionCount = d.LenderDocumentVersions.Count(),
                             Emails = d.WFComponentEmailLenderDocuments.Count(),
                             UpdatedByUserId = d.UpdatedByUserId,
                             UpdatedByUsername = d.User.Username,
                             UpdatedDate = d.UpdatedDate
                         }).ToList()
                        .Select(d => new LenderDocumentMasterView()
                        {
                            LenderDocumentMasterId = d.LenderDocumentMasterId,
                            LenderId = lenderId,
                            Deleted = d.Deleted,
                            isDirty = false,
                            LatestLenderDocumentVersionId = d.LatestLenderDocumentVersionDetails.LenderDocumentVersionId,
                            LatestLenderDocumentVersionName = d.LatestLenderDocumentVersionDetails.DocumentName,
                            LatestLenderDocumentVersionType = d.LatestLenderDocumentVersionDetails.DocType,
                            
                            UpdatedByUserId = d.UpdatedByUserId,
                            UpdatedByUserName = d.UpdatedByUsername,
                            UpdatedDate = d.UpdatedDate,
                            NumberOfVersions = d.VersionCount,
                            NumberOfEmails = d.Emails
                        }).ToList();
        }
        public IEnumerable<LenderConsentFeeOptionView> GetLenderConsentFeeOptionsViews(int lenderId)
        {
            var items = GetLenderConsentFeeOptionsForQuery(context.LenderConsentFeeOptions.Where(l => l.LenderId == lenderId)).ToList();
            foreach(var item in items)
            {
                if (!item.ConsentTypeId.HasValue) item.ConsentTypeId = -1;
                if (!item.MortMgrId.HasValue) item.MortMgrId = -1;
                if (!item.MortgageeId.HasValue) item.MortgageeId = -1;
                if (!item.OfficeStateId.HasValue) item.OfficeStateId = -1;
                if (!item.SecurityStateId.HasValue) item.SecurityStateId= -1;
                if (!item.PayableTypeId.HasValue) item.SecurityStateId = -1;

            }
            return items;
        }

        public IEnumerable<LenderConsentFeeOptionView> GetConsentFeesForMatter(int matterId, List<int> securityStates, int? mortgageeId, int? consentTypeId, bool defaultOnly)
        {

            var mtDetails = context.Matters.Select(m => new
            {
                m.MatterId,
                m.LenderId,
                m.MortMgrId,
                m.StateId
            }).FirstOrDefault(m => m.MatterId == matterId);

            var fees = GetLenderConsentFeeOptionsForQuery
                (
                    context.LenderConsentFeeOptions.Where(l =>
                    l.LenderId == mtDetails.LenderId &&
                    !defaultOnly || l.IsDefault && 
                    (!l.MortMgrId.HasValue || l.MortMgrId == mtDetails.MortMgrId) &&
                    (!l.OfficeStateId.HasValue || l.MortMgrId == mtDetails.MortMgrId) &&
                    (!l.SecurityStateId.HasValue || securityStates.Any(s => s == l.SecurityStateId)) &&
                    (!l.MortgageeId.HasValue || l.MortgageeId == mortgageeId) &&
                    (!l.ConsentTypeId.HasValue || l.ConsentTypeId == consentTypeId)
                )).ToList();

            foreach (var fee in fees)
            {
                if (fee.MortMgrId.HasValue)
                {
                    fee.RankingScore += 1;
                }
                if (fee.OfficeStateId.HasValue)
                {
                    fee.RankingScore += 1;
                }
                if (fee.SecurityStateId.HasValue)
                {
                    fee.RankingScore += 1;
                }
                if (fee.MortgageeId.HasValue)
                {
                    fee.RankingScore += 1;
                }
                if (fee.ConsentTypeId.HasValue)
                {
                    fee.RankingScore += 1;
                }
            }

            return fees.OrderBy(o => o.RankingScore).GroupBy(d => d.FeeDescription).Select(f => f.First()).ToList();

        }

        public IEnumerable<LenderConsentFeeOptionView> GetLenderConsentFeeOptionsForQuery(IQueryable<LenderConsentFeeOption> qry)
        {
            return qry.Select(x => new LenderConsentFeeOptionView()
            {
                isDirty = false,
                LenderId = x.LenderId,
                LenderConsentFeeOptionId = x.LenderConsentFeeOptionId,
                MortMgrId = x.MortMgrId,
                MortgageeId = x.MortgageeId,
                ConsentTypeId = x.ConsentTypeId,
                OfficeStateId = x.OfficeStateId,
                SecurityStateId = x.SecurityStateId,
                IsDefault = x.IsDefault,
                FeeDescription = x.FeeDescription,
                PayableTypeId = x.PayableTypeId,
                PayableToOtherName = x.PayableToOtherName,
                Amount = x.Amount,
                UpdatedByUserId = x.UpdatedByUserId,
                UpdatedDate = DateTime.Now
            });
        }
        public IEnumerable<LenderStateEmailView> GetLenderStateSpecificContacts(int lenderId)
        {
            return GetLenderStateEmailViews(context.LenderStateEmails.Where(e => e.Enabled && e.LenderId == lenderId));
        }
        public IEnumerable<LenderStateEmailView> GetLenderStateSpecificContacts(int lenderId, int stateId)
        {
            return GetLenderStateEmailViews(context.LenderStateEmails.Where(e => e.Enabled && e.LenderId == lenderId && e.StateId == stateId));
        }

        public IEnumerable<LenderStateEmailView> GetLenderStateEmailViews(IQueryable<LenderStateEmail> qry)
        {
            return qry.Select(e => new LenderStateEmailView()
            {
                isDirty = false,
                Enabled = e.Enabled,
                StateId = e.StateId,
                LenderStateEmailId = e.LenderStateEmailId,
                LenderId = e.LenderId,
                EmailAddress = e.EmailAddress,
                Override = e.Overrides,
                UpdatedByUserId = e.UpdatedByUserId,
                UpdatedDate = e.UpdatedDate
            }).ToList().OrderBy(s => s.StateId);
        }

        public IEnumerable<LenderDisbursementOptionView> GetLenderDisbursementOptionList(int lenderId, bool isFunding)
        {
            return
                context.LenderDisbursementOptions.Where(x => x.LenderId == lenderId && x.IsFunding == isFunding)
                .Select(x => new LenderDisbursementOptionView
                {
                    LenderDisbursementOptionId = x.LenderDisbursementOptionId,
                    LenderId = x.LenderId,
                    DisbursementOption = x.DisbursementOption,
                    StateId = x.StateId.HasValue ? x.StateId : -1,
                    FeeId = x.FeeId.HasValue ? x.FeeId : -1,
                    FeeName = x.Fee.FeeName,
                    EFT_AccountName = x.EFT_AccountName,
                    EFT_BSB = x.EFT_BSB,
                    EFT_AccountNo = x.EFT_AccountNo,
                    MortMgrId = x.MortMgrId.HasValue ? x.MortMgrId : -1,
                    TransactionReferenceTypeId = x.TransactionReferenceTypeId,
                    RefLookUp = x.RefLookUp,
                    Mortgagee = x.Mortgagee,
                    OtherPartyName = x.OtherPartyName,
                    PayableTypeId = x.PayableTypeId,
                    TransactionTypeId = x.TransactionTypeId.HasValue ? x.TransactionTypeId : -1,
                    FundingChannelTypeId = x.FundingChannelTypeId.HasValue ? x.FundingChannelTypeId : -1,
                    IsVisible = x.IsVisible,
                    DisplayOrder = x.DisplayOrder,
                    UpdatedDate = x.UpdatedDate,
                    UpdatedByUserId = x.UpdatedByUserId,
                    UpdatedByUsername = x.User.Username,
                    FastRefiSpecific = x.FastRefiSpecific,
                    IsDirty = false
                }).ToList();
        }


        public IEnumerable<LenderReminderExclusionView> GetReminderExclusions(int lenderId)
        {
            return
                context.LenderSendReminderExcls.Where(x => x.LenderId == lenderId)
                .Select(x=> new LenderReminderExclusionView
                {
                    //Id = new Guid(),
                    ReminderExclusionId = x.LenderSendReminderExclId,
                    LenderId = lenderId,
                    WFComponentId = x.WFComponentId,
                    WFComponentName = x.WFComponent.WFComponentName,
                    ExcludeEmailReminders = x.ExcludeEmailReminders,
                    ExcludeSMSReminders = x.ExcludeSMSReminders,
                    isDirty = false
                }).ToList();
        }
    }
}
