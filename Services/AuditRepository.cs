using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Slick_Domain.Entities;
using Slick_Domain.Models;

namespace Slick_Domain.Services
{
    public class AuditRepository : IDisposable
    {
        private readonly SlickContext context;

        public AuditRepository(SlickContext Context)
        {
            context = Context;
        }

        public IEnumerable<AuditCustomEntities.MatterLedgerItemAuditView> GetMatterLedgerAuditView(int matterId)
        {

            try
            {
                return context.audMatterLedgerItems.Where(m => m.MatterId == matterId)
                            .Select(m => new AuditCustomEntities.MatterLedgerItemAuditView()
                            {
                                audMatterLedgerItemId = m.audMatterLedgerItemId,
                                MatterLedgerItemId = m.MatterLedgerItemId,
                                MatterId = m.MatterId,
                                TransactionTypeId = m.TransactionTypeId,
                                TransactionTypeName = m.TransactionType.TransactionTypeName,
                                LedgerItemSourceTypeId = m.LedgerItemSourceTypeId,
                                LedgerItemSourceTypeName = m.LedgerItemSourceType.LedgerItemSourceTypeName,
                                PayableByAccountTypeId = m.PayableByAccountTypeId,
                                PayableByAccountTypeName = m.AccountType.AccountTypeName,
                                PayableByTypeId = m.PayableByTypeId,
                                PayableByTypeName = m.PayableType.PayableTypeName,
                                PayableByOthersDesc = m.PayableByOthersDesc,
                                PayableToAccountTypeId = m.PayableToAccountTypeId,
                                PayableToAccountTypeName = m.AccountType1.AccountTypeName,
                                PayableToTypeId = m.PayableToTypeId,
                                PayableToTypeName = m.PayableType1.PayableTypeName,
                                PayableToOthersDesc = m.PayableToOthersDesc,
                                EFT_AccountName = m.EFT_AccountName,
                                EFT_BSB = m.EFT_BSB,
                                EFT_AccountNo = m.EFT_AccountNo,
                                EFT_Reference = m.EFT_Reference,
                                Amount = m.Amount,
                                GSTFree = m.GSTFree,
                                Description = m.Description,
                                Instructions = m.Instructions,
                                PaymentNotes = m.PaymentNotes,
                                MatterLedgerItemStatusTypeId = m.MatterLedgerItemStatusTypeId,
                                UpdatedDate = m.UpdatedDate,
                                UpdatedByUserId = m.UpdatedByUserId,
                                UpdatedByUsername = m.UpdatedByUsername,
                                AuditDate = m.AuditDate,
                                AuditAction = m.AuditAction
                            })
                            .ToList();
            }
            catch (Exception ex)
            {
                Handlers.ErrorHandler.LogError(ex);
                return null;
            }
        }

        public IEnumerable<AuditOffice> GetOfficeAudits(int officeId)
        {
            return context.audOffices.Where(m => m.OfficeId == officeId)
                    .Select(m => new AuditOffice
                    {
                        Model = new OfficeView
                        {
                            AddressDetails = m.StreetAddress,
                            Fax = m.Fax,
                            OfficeId = m.OfficeId.Value,
                            OfficeName = m.OfficeName,
                            Phone = m.Phone,
                            PostCode = m.PostCode,
                            StateName = m.State.StateName,
                            Suburb = m.Suburb,
                            UpdatedBy = m.UpdatedByUserName,
                            UpdatedDate = m.UpdatedDate
                        },
                        AuditModel = new AuditModel
                        {
                            AuditAction = m.AuditAction,
                            AuditDate = m.AuditDate
                        }
                    })
                    .ToList();
        }

        public IEnumerable<AuditFee> GetFeeAudits(int feeId)
        {
            return context.audFees.Where(m => m.FeeId == feeId)
                    .Select(m => new AuditFee
                    {
                        Model = new FeeView
                        {
                            FeeId = m.FeeId,
                            FeeName = m.FeeName,
                            FeeDescription = m.FeeDescription,
                            Enabled = m.Enabled,
                            UpdatedByUsername = m.UpdatedByUserName,
                            UpdatedDate = m.UpdatedDate
                        },
                        AuditModel = new AuditModel
                        {
                            AuditAction = m.AuditAction,
                            AuditDate = m.AuditDate
                        }
                    })
                    .ToList();
        }

        public IEnumerable<AuditPrecedent> GetPrecedentAudits(int id)
        {
            return context.audPrecedents.Where(m => m.PrecedentId == id)
                    .Select(m => new AuditPrecedent
                    {
                        Model = new PrecedentCustomEntities.PrecedentView
                        {
                            PrecedentId = m.PrecedentId,
                            AssemblySwitches = m.AssemblySwitches,
                            Description = m.Description,
                            DocName = m.DocName,
                            DocType = m.DocType,
                            HotDocsId = m.HotDocsId,
                            LenderName = m.Lender.LenderNameShort ?? "-- Any --",
                            MortMgrName = m.MortMgr.MortMgrNameShort ?? "-- Any --",
                            StateName = m.State.StateName ?? "-- Any --",
                            MatterGroupName = m.MatterType.MatterTypeName ?? "-- Any --",
                            PrecedentOwnerTypeName = m.PrecedentOwnerType.PrecedentOwnerTypeName,
                            PrecedentTypeName = m.PrecedentType.PrecedentTypeName,
                            WFComponentName = m.WFComponent.WFComponentName,
                            TemplateFileName = m.TemplateFile,
                            PrecedentStatusTypeName = m.PrecedentStatusType.PrecedentStatusTypeName,
                            UpdatedByUsername = m.User.Username,
                            UpdatedDate = m.UpdatedDate
                        },
                        AuditModel = new AuditModel
                        {
                            AuditAction = m.AuditAction,
                            AuditDate = m.AuditDate
                        }
                    })
                    .ToList();
        }

        public IEnumerable<AuditPrecedentBuildInstructions> GetPrecedentBuildInstructionAudits(int id)
        {
            return context.audPrecedentBuildInstructions.Where(m => m.PrecedentBuildInstructionId == id)
                    .Select(m => new AuditPrecedentBuildInstructions
                    {
                        Model = new PrecedentCustomEntities.PrecedentBuildInstructionsView
                        {
                            ID = m.PrecedentBuildInstructionId,
                            ApplicationName = m.InstructionApplication,
                            MacroName = m.InstructionCmd,
                            BuildOrder = m.BuildOrder,
                            UpdatedByUsername = m.User.Username,
                            UpdatedDate = m.UpdatedDate
                        },
                        AuditModel = new AuditModel
                        {
                            AuditAction = m.AuditAction,
                            AuditDate = m.AuditDate
                        }
                    })
                    .ToList();
        }

        public IEnumerable<AuditMortMgr> GetMortMgrAudits(int mortMgrId)
        {
            return (from m in context.audMortMgrs
                       join s in context.States on m.StateId equals s.StateId into joinedState from states in joinedState.DefaultIfEmpty()
                       join s2 in context.States on m.PostalStateId equals s2.StateId into joinedState2 from postalStates in joinedState2.DefaultIfEmpty()
                       where m.MortMgrId == mortMgrId
                       select new AuditMortMgr
                       {
                           Model = new MortMgrCustomEntities.MortMgrView
                           {
                               CompanyABN = m.CompanyABN,
                               CompanyACLN = m.CompanyACLN,
                               CompanyACN = m.CompanyACN,
                               ContactFirstname = m.ContactFirstname,
                               ContactLastname = m.ContactLastname,
                               Email = m.Email,
                               IsEnabled = m.Enabled,
                               Mobile = m.Mobile,
                               MortMgrId = m.MortMgrId,
                               MortMgrName = m.MortMgrName,
                               MortMgrNameShort = m.MortMgrNameShort,
                               PayeeName = m.PayeeName,
                               PostalAddress = m.PostalAddress,
                               PostalPostCode = m.PostalPostCode,
                               PostalStateName = postalStates.StateName,
                               PostalSuburb = m.PostalSuburb,
                               AddressDetails = m.StreetAddress,
                               Fax = m.Fax,
                               Phone = m.Phone,
                               PostCode = m.PostCode,
                               StateName = states.StateName,
                               Suburb = m.Suburb,                               
                               UpdatedBy = m.UpdatedByUsername,
                               UpdatedDate = m.UpdatedDate
                           },
                           AuditModel = new AuditModel
                           {
                               AuditAction = m.AuditAction,
                               AuditDate = m.AuditDate
                           }
                       })
                    .ToList();
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
