using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Slick_Domain.Entities;
using Slick_Domain.Models;
using Slick_Domain.Common;

namespace Slick_Domain.Services
{
    public class GeneralRepository : IDisposable
    {
        private readonly SlickContext context;

        public GeneralRepository(SlickContext Context)
        {
            context = Context;
        }

        public List<GeneralCustomEntities.GeneralStatusBooleanView> GetBoolStatusList()
        {
            return GetBoolStatusList(false);
        }
        public List<GeneralCustomEntities.GeneralStatusBooleanView> GetBoolStatusList(bool inclAny)
        {
            List<GeneralCustomEntities.GeneralStatusBooleanView> retVal = new List<GeneralCustomEntities.GeneralStatusBooleanView>();
            retVal.Add(new GeneralCustomEntities.GeneralStatusBooleanView(null, "-- Any --"));
            retVal.Add(new GeneralCustomEntities.GeneralStatusBooleanView(false, "False"));
            retVal.Add(new GeneralCustomEntities.GeneralStatusBooleanView(true, "True"));
            return retVal;
        }

        public IEnumerable<GeneralCustomEntities.GeneralCheckList> GetGeneralCheckList_State(bool defValue, bool inclSelectAll, bool inclNullSelect)
        {
            try
            {
                var liTmp = context.States
                            .Select(l => new { l.StateId, l.StateName })
                            .ToList()
                            .Select(l => new GeneralCustomEntities.GeneralCheckList(defValue, l.StateId, l.StateName))
                            .ToList();


                if (inclSelectAll)
                    liTmp.Insert(0, new GeneralCustomEntities.GeneralCheckList(defValue, -1, "-- All States --"));

                if (inclNullSelect)
                    liTmp.Add(new GeneralCustomEntities.GeneralCheckList(defValue, 0, "-- No State --"));

                return liTmp;
            }
            catch (Exception ex)
            {
                Handlers.ErrorHandler.LogError(ex);
                return null;
            }
        }

        public IEnumerable<GeneralCustomEntities.GeneralCheckList> GetGeneralCheckList_DischargeType(bool checkedValue, bool inclSelectAll, bool inclNullSelect)
        {
            try
            {
                var resTmp = context.DischargeTypes
                            .Select(l => new { l.DischargeTypeId, l.DischargeTypeName })
                            .ToList()
                            .Select(l => new GeneralCustomEntities.GeneralCheckList(checkedValue, l.DischargeTypeId, l.DischargeTypeName))
                            .ToList();


                if (inclSelectAll)
                    resTmp.Insert(0, new GeneralCustomEntities.GeneralCheckList(checkedValue, -1, "-- Select All --"));

                if (inclNullSelect)
                    resTmp.Add(new GeneralCustomEntities.GeneralCheckList(checkedValue, 0, "-- No Discharge Type --"));

                return resTmp;
            }
            catch (Exception ex)
            {
                Handlers.ErrorHandler.LogError(ex);
                return null;
            }

        }

       
        public IEnumerable<GeneralCustomEntities.GeneralCheckList> GetGeneralCheckList_WFComponent(bool defValue, bool inclSelectAll, bool inclNullSelect)
        {
            try
            {
                var mmgrsTmp = context.WFComponents
                            .Where(w => w.Enabled)
                            .Select(l => new { l.WFComponentId, l.WFComponentName })
                            .ToList()
                            .Select(l => new GeneralCustomEntities.GeneralCheckList(defValue, l.WFComponentId, l.WFComponentName))
                            .OrderBy(o => o.Name)
                            .ToList();


                if (inclSelectAll)
                    mmgrsTmp.Insert(0, new GeneralCustomEntities.GeneralCheckList(defValue, -1, "-- All Milestones --"));

                if (inclNullSelect)
                    mmgrsTmp.Add(new GeneralCustomEntities.GeneralCheckList(defValue, 0, "-- No Milestone --"));

                return mmgrsTmp;
            }
            catch (Exception ex)
            {
                Handlers.ErrorHandler.LogError(ex);
                return null;
            }

        }
        public IEnumerable<GeneralCustomEntities.GeneralCheckList> GetGeneralCheckList_SettlementType(bool defValue, bool inclSelectAll, bool inclNullSelect)
        {
            try
            {
                var mmgrsTmp = context.SettlementTypes
                            .Select(l => new { l.SettlementTypeId, l.SettlementTypeName })
                            .ToList()
                            .Select(l => new GeneralCustomEntities.GeneralCheckList(defValue, l.SettlementTypeId, l.SettlementTypeName))
                            .OrderBy(o => o.Name)
                            .ToList();

                if (inclSelectAll)
                    mmgrsTmp.Insert(0, new GeneralCustomEntities.GeneralCheckList(defValue, -1, "-- All Settlement Types --"));

                if (inclNullSelect)
                    mmgrsTmp.Add(new GeneralCustomEntities.GeneralCheckList(defValue, 0, "-- No Settlement Types --"));

                return mmgrsTmp;
            }
            catch (Exception ex)
            {
                Handlers.ErrorHandler.LogError(ex);
                return null;
            }
        }
        public IEnumerable<GeneralCustomEntities.GeneralCheckList> GetGeneralCheckList_TransactionStatus(bool defValue, bool inclSelectAll, bool inclNullSelect)
        {
            try
            {

                var tsTmp = context.TrustSummaryStatusTypes
                            .Select(l => new { l.TrustSummaryStatusTypeId, l.TrustSummaryStatusTypeName })
                            .ToList()
                            .Select(l => new GeneralCustomEntities.GeneralCheckList(defValue, l.TrustSummaryStatusTypeId, l.TrustSummaryStatusTypeName))
                            .OrderBy(o => o.Name)
                            .ToList();

                if (inclSelectAll)
                tsTmp.Insert(0, new GeneralCustomEntities.GeneralCheckList(defValue, -1, "-- Any Transaction Status --"));

                if (inclNullSelect)
                tsTmp.Add(new GeneralCustomEntities.GeneralCheckList(defValue, 0, "-- Not in a Transaction --"));

                return tsTmp;
            }
            catch (Exception ex)
            {
                Handlers.ErrorHandler.LogError(ex);
                return null;
            }
        }

        public IEnumerable<GeneralCustomEntities.GeneralCheckList> GetGeneralCheckList_SettlementScheduleStatus(bool defValue, bool inclSelectAll, bool inclNullSelect)
        {
            try
            {

                var tsTmp = context.SettlementScheduleStatusTypes
                            .Select(l => new { l.SettlementScheduleStatusTypeId, l.SettlementScheduleStatusTypeName })
                            .ToList()
                            .Select(l => new GeneralCustomEntities.GeneralCheckList(defValue, l.SettlementScheduleStatusTypeId, l.SettlementScheduleStatusTypeName))
                            .OrderBy(o => o.Name)
                            .ToList();

                if (inclSelectAll)
                    tsTmp.Insert(0, new GeneralCustomEntities.GeneralCheckList(defValue, -1, "-- Any Settlement Status --"));

                if (inclNullSelect)
                    tsTmp.Add(new GeneralCustomEntities.GeneralCheckList(defValue, 0, "-- No Settlement Scheduled --"));

                return tsTmp;
            }
            catch (Exception ex)
            {
                Handlers.ErrorHandler.LogError(ex);
                return null;
            }
        }

        public IEnumerable<GeneralCustomEntities.GeneralCheckList> GetGeneralCheckList_SettlementRegionType(bool defValue, bool inclSelectAll, bool inclNullSelect)
        {
            try
            {

                var tsTmp = context.SettlementRegionTypes
                            .Select(l => new { l.SettlementRegionTypeId, l.SettlementRegionTypeName })
                            .ToList()
                            .Select(l => new GeneralCustomEntities.GeneralCheckList(defValue, l.SettlementRegionTypeId, l.SettlementRegionTypeName))
                            .OrderBy(o => o.Name)
                            .ToList();

                if (inclSelectAll)
                    tsTmp.Insert(0, new GeneralCustomEntities.GeneralCheckList(defValue, -1, "-- Any Settlement Region --"));

                if (inclNullSelect)
                    tsTmp.Add(new GeneralCustomEntities.GeneralCheckList(defValue, 0, "-- No Settlement Region --"));

                return tsTmp;
            }
            catch (Exception ex)
            {
                Handlers.ErrorHandler.LogError(ex);
                return null;
            }
        }

        public IEnumerable<GeneralCustomEntities.GeneralCheckList> GetGeneralCheckList_TransactionDirectionType(bool defValue, bool inclSelectAll, bool inclNullSelect)
        {
            try
            {

                var tsTmp = context.TransactionDirectionTypes
                            .Select(l => new { l.TransactionDirectionTypeId, l.TransactionDirectionTypeName })
                            .ToList()
                            .Select(l => new GeneralCustomEntities.GeneralCheckList(defValue, l.TransactionDirectionTypeId, l.TransactionDirectionTypeName))
                            .OrderBy(o => o.Name)
                            .ToList();

                if (inclSelectAll)
                    tsTmp.Insert(0, new GeneralCustomEntities.GeneralCheckList(defValue, -1, "-- Any Transaction Direction --"));

                if (inclNullSelect)
                    tsTmp.Add(new GeneralCustomEntities.GeneralCheckList(defValue, 0, "-- No Transaction Direction --"));

                return tsTmp;
            }
            catch (Exception ex)
            {
                Handlers.ErrorHandler.LogError(ex);
                return null;
            }
        }

        public IEnumerable<GeneralCustomEntities.GeneralCheckList> GetGeneralCheckList_IssueReason(bool defValue, bool inclSelectAll)
        {
            try
            {

                var tsTmp = context.Reasons
                            .Where(x=> x.ReasonGroupTypeId == (int)Enums.ReasonGroupTypeEnum.Issue)
                            .Select(l => new { l.ReasonId, l.ReasonTxt })
                            .ToList()
                            .Select(l => new GeneralCustomEntities.GeneralCheckList(defValue, l.ReasonId, l.ReasonTxt))
                            .OrderBy(o => o.Name)
                            .ToList();

                if (inclSelectAll)
                    tsTmp.Insert(0, new GeneralCustomEntities.GeneralCheckList(defValue, -1, "-- Any Issue Reason --"));

                return tsTmp;
            }
            catch (Exception ex)
            {
                Handlers.ErrorHandler.LogError(ex);
                return null;
            }
        }

        public IEnumerable<GeneralCustomEntities.GeneralCheckList> GetGeneralCheckList_CustodianIssueType(bool defValue, bool inclSelectAll)
        {
            try
            {

                var tsTmp = context.PossibleCustodianLetterIssues
                            .Where(x => x.Enabled)
                            .Select(l => new { l.PossibleCustodianLetterIssueId, l.ItemDesc})
                            .ToList()
                            .Select(l => new GeneralCustomEntities.GeneralCheckList(defValue, l.PossibleCustodianLetterIssueId, l.ItemDesc))
                            .OrderBy(o => o.Name)
                            .ToList();

                if (inclSelectAll)
                    tsTmp.Insert(0, new GeneralCustomEntities.GeneralCheckList(defValue, -1, "-- Any Issue Reason --"));

                return tsTmp;
            }
            catch (Exception ex)
            {
                Handlers.ErrorHandler.LogError(ex);
                return null;
            }
        }

        public IEnumerable<GeneralCustomEntities.GeneralCheckList> GetGeneralCheckList_DocQAFilterType(bool defValue, bool inclSelectAll)
        {
            return new List<GeneralCustomEntities.GeneralCheckList>
            {
                new GeneralCustomEntities.GeneralCheckList (defValue, (int)Enums.QADocFilterType.All, "- ANY QA TYPE -"),
                new GeneralCustomEntities.GeneralCheckList (false, (int)Enums.QADocFilterType.NoQA, "No QA Required"),
                new GeneralCustomEntities.GeneralCheckList (false, (int)Enums.QADocFilterType.QADocPrep, "QA Doc Prep"),
                new GeneralCustomEntities.GeneralCheckList (false, (int)Enums.QADocFilterType.QASettlement, "QA Settlement"),
                new GeneralCustomEntities.GeneralCheckList (false, (int)Enums.QADocFilterType.QASettlementInstructions, "QA Settlement Instructions")
            };
        }

        public IEnumerable<GeneralCustomEntities.GeneralCheckList> GetGeneralCheckList_FundingChannelType(bool defValue, bool inclSelectAll)
        {
            return new List<GeneralCustomEntities.GeneralCheckList> 
            { new GeneralCustomEntities.GeneralCheckList (defValue, (int)Enums.QADocFilterType.All, "- All Funding Channel Types -"), }.Concat(context.FundingChannelTypes.Select(g=>new { g.FundingChannelTypeId, g.FundingChannelTypeName }).ToList().Select(g=>new GeneralCustomEntities.GeneralCheckList(defValue, g.FundingChannelTypeId, g.FundingChannelTypeName)).ToList());
        }
        public IEnumerable<GeneralCustomEntities.GeneralCheckList> GetGeneralCheckList_LoanType(bool defValue, bool inclSelectAll)
        {
            return new List<GeneralCustomEntities.GeneralCheckList>
            {
                new GeneralCustomEntities.GeneralCheckList (defValue, (int)Enums.QADocFilterType.All, "- All Loan Types -"), }
            .Concat(context.LoanTypes.Select(g => new { g.LoanTypeId, g.LoanTypeName}).ToList()
            .Select(g => new GeneralCustomEntities.GeneralCheckList(defValue, g.LoanTypeId, g.LoanTypeName)).ToList());
        }
        /// <summary>
        /// PEXA status types for reports
        /// </summary>
        /// <param name="defValue"></param>
        /// <param name="inclSelectAll"></param>
        /// <returns></returns>
        public IEnumerable<GeneralCustomEntities.GeneralCheckList> GetGeneralCheckList_PexaStatusType(bool defValue, bool inclSelectAll)
        {
            try
            {

                var tsTmp = context.PexaWorkspaceStatusTypes
                            .Select(l => new { l.PexaWorkspaceStatusTypeId, l.PexaWorkspaceStatusTypeName})
                            .ToList()
                            .Select(l => new GeneralCustomEntities.GeneralCheckList(defValue, l.PexaWorkspaceStatusTypeId, l.PexaWorkspaceStatusTypeName))
                            .OrderBy(o => o.Name)
                            .ToList();

                if (inclSelectAll)
                    tsTmp.Insert(0, new GeneralCustomEntities.GeneralCheckList(defValue, -1, "-- Any Status Type --"));

                return tsTmp;
            }
            catch (Exception ex)
            {
                Handlers.ErrorHandler.LogError(ex);
                return null;
            }
        }
        /// <summary>
        /// Issue Registration Types - Confirm Registration - Whose Issue - Liability
        /// </summary>
        /// <param name="defValue"></param>
        /// <param name="inclSelectAll"></param>
        /// <returns></returns>
        public IEnumerable<GeneralCustomEntities.GeneralCheckList> GetGeneralCheckList_LiabilityType(bool defValue, bool inclSelectAll)
        {
            try
            {

                var tsTmp = context.IssueRegistrationTypes
                            .Select(l => new { l.IssueRegistrationTypeId, l.IssueRegistrationTypeName })
                            .ToList()
                            .Select(l => new GeneralCustomEntities.GeneralCheckList(defValue, l.IssueRegistrationTypeId, l.IssueRegistrationTypeName))
                            .OrderBy(o => o.Name)
                            .ToList();

                if (inclSelectAll)
                    tsTmp.Insert(0, new GeneralCustomEntities.GeneralCheckList(defValue, -1, "-- Any Liability Type --"));

                return tsTmp;
            }
            catch (Exception ex)
            {
                Handlers.ErrorHandler.LogError(ex);
                return null;
            }
        }

        public IEnumerable<GeneralCustomEntities.GeneralCheckList> GetGeneralCheckList_ActionType(bool isChecked)
        {
            return new List<GeneralCustomEntities.GeneralCheckList>
            {
                new GeneralCustomEntities.GeneralCheckList {Id = -1, Name = "-- Any Action --", IsChecked = isChecked },
                new GeneralCustomEntities.GeneralCheckList {Id = 1, Name = "Matter Note", IsChecked = isChecked },
                new GeneralCustomEntities.GeneralCheckList {Id = 2, Name = "Save Email", IsChecked = isChecked },
                new GeneralCustomEntities.GeneralCheckList {Id = 3, Name = "Save Document", IsChecked = isChecked }
            };
        }

        public PayableType GetPayableByType(int payableByTypeId)
        {
            return context.PayableTypes.FirstOrDefault(p => p.PayableTypeId == payableByTypeId);
        }

        public PayableType GetPayableToType(int payableToTypeId)
        {
            return context.PayableTypes.FirstOrDefault(p => p.PayableTypeId == payableToTypeId);
        }

        public TransactionType GetTransactionType(int transactionTypeId)
        {
            return context.TransactionTypes.FirstOrDefault(t => t.TransactionTypeId == transactionTypeId);
        }

        public GeneralCustomEntities.StateView GetStateView(int stateId)
        {
            return context.States.Where(s => s.StateId == stateId)
                .Select(s => new { s.StateId, s.StateName, s.StateDesc })
                .ToList()
                .Select(s => new GeneralCustomEntities.StateView(s.StateId, s.StateName, s.StateDesc))
                .FirstOrDefault();                
        }

        public IEnumerable<GeneralCustomEntities.StateView> GetStatesView()
        {
            return context.States
                .Select(s=> new { s.StateId, s.StateName, s.StateDesc})
                .ToList()
                .Select(s => new GeneralCustomEntities.StateView(s.StateId, s.StateName, s.StateDesc))
                .ToList();
        }

        public IEnumerable<FileLookupView> GetFileLookups()
        {
            var recs = (from fl in context.FileLookups
                        join u in context.Users on fl.UpdatedByUserId equals u.UserId
                        select new
                        {
                            fl.FileLookupId,
                            fl.FileLookupTypeId,
                            fl.FileLookupType.FileLookupTypeName,
                            fl.FileName,
                            fl.LenderId,
                            fl.Lender.LenderName,
                            fl.MatterTypeId,
                            fl.MatterType.MatterTypeName,
                            fl.SequenceNo,
                            fl.UpdatedByUserId,
                            fl.UpdatedDate,
                            u.Username
                        }).ToList()
            .Select(f => new FileLookupView
            {
                FileLookupId = f.FileLookupId,
                FileLookupTypeId = f.FileLookupTypeId,
                FileLookupTypeName = f.FileLookupTypeName,
                FileName = f.FileName,
                LenderId = f.LenderId ?? DomainConstants.AnySelectionId,
                LenderName = f.LenderName ?? DomainConstants.AnySelection,
                MatterTypeId = f.MatterTypeId ?? DomainConstants.AnySelectionId,
                MatterTypeName = f.MatterTypeName ?? DomainConstants.AnySelection,
                SequenceNo = f.SequenceNo,
                UpdatedByUsername = f.Username,
                UpdatedDate = f.UpdatedDate,
                UpdatedByUserId = f.UpdatedByUserId
            }).ToList();

            return recs;
        }

        public IQueryable<GeneralCustomEntities.SlickSupportTicketView> GetSupportTickets(bool showResolved = true)
        {
            IQueryable<GeneralCustomEntities.SlickSupportTicketView> list = context.SlickSupportTickets.Select(x => new GeneralCustomEntities.SlickSupportTicketView
            {
                TicketId = x.SlickSupportTicketId,
                SlickSupportIssueStatusId = x.SlickSupportStatusTypeId,
                SlickSupportIssueStatusName = x.SlickSupportStatusType.SlickSupportStatusTypeName,
                SlickSupportIssueDetails = x.SlickSupportIssueDetails,
                SlickSupportTicketMatterId = x.SlickSupportTicketMatterId,
                RequestByUserId = x.RequestByUserId,
                RequestByUserName = x.User.Username,
                RequestByUserEmail = x.User.Email,
                RequestDate = x.RequestDate,
                TicketTypeId = x.SlickSupportTicketTypeId,
                TicketTypeName = x.SlickSupportTicketType.SlickSupportTicketTypeName,
                Notes = x.Notes,
                UpdatedDate = x.UpdatedDate,
                UpdatedByUserId = x.UpdatedByUserId,
                UpdatedByUserName = x.UpdatedByUserId.HasValue ? x.User1.Username : null
            });

            if (!showResolved) list = list.Where(x => x.SlickSupportIssueStatusId == (int)Enums.SlickSupportStatusEnum.Investigating || x.SlickSupportIssueStatusId == (int)Enums.SlickSupportStatusEnum.RequestMade);

            return list;
        }

        public IEnumerable<GeneralCustomEntities.HolidayView> GetHolidaysInRange(DateTime startDate, DateTime endDate)
        {
            return context.Holidays.Where(d => d.HolidayDate >= startDate && d.HolidayDate <= endDate)
                .Select(h => new { h.HolidayDate, h.HolidayName, h.IsNational, StateIds = h.HolidayStates.Select(s=>s.StateId).ToList()})
                .ToList()
                .Select(h => new GeneralCustomEntities.HolidayView(h.HolidayName, h.IsNational, h.HolidayDate, h.StateIds)).ToList();

        }
        public IEnumerable<Holiday> GetHolidaysList(int stateId)
        {
            return CommonMethods.GetHolidayList(context, stateId);
        }

        public string GetReportServerReportName(string reportName)
        {
            return context.Reports.FirstOrDefault(r => r.ReportName == reportName)?.ReportServerReportName;
        }

        public List<EntityCompacted> GetReportDisplaySelectorList()
        {
            List<EntityCompacted> tmp = new List<EntityCompacted>();

            tmp.Add(new EntityCompacted(-1, "Chart and List", true));
            tmp.Add(new EntityCompacted(0, "Chart only", false));
            tmp.Add(new EntityCompacted(1, "List only", false));
            return tmp;
        }
        public IEnumerable<GeneralCustomEntities.SavedReportsGridView> GetSavedReportsList(int userId)
        {
            return GetSavedReportsList(context.ReportUserTemplates.Where(u => u.UserId == userId));
        }

        public IEnumerable<GeneralCustomEntities.SavedReportsGridView> GetSavedReportsList(int userId, int reportId)
        {
            return GetSavedReportsList(context.ReportUserTemplates.Where(u => u.UserId == userId && u.ReportId == reportId));
        }

        public IEnumerable<GeneralCustomEntities.SavedReportsGridView> GetSavedReportsList(IQueryable<ReportUserTemplate> qry)
        {
            return qry.Select(r => new
            {
                    ReportUserTemplateId = r.ReportUserTemplateId,
                    UserId = r.UserId,
                    UserFullname = r.User.Fullname,
                    ReportId = r.ReportId,
                    ReportDisplayName = r.Report.ReportDisplayName,
                    TemplateName = r.TemplateName,
                    StartDateOffset = r.ReportUserTemplateParams.FirstOrDefault(p => p.ReportUserTemplateId == r.ReportUserTemplateId && p.ReportParam.ParameterName == "StartDate").ParameterDateOffSet,
                    EndDateOffset = r.ReportUserTemplateParams.FirstOrDefault(p => p.ReportUserTemplateId == r.ReportUserTemplateId && p.ReportParam.ParameterName == "EndDate").ParameterDateOffSet,
                    UpdatedDate = r.UpdatedDate,
                    UpdatedByUserId = r.UpdatedByUserId,
                    UpdatedByUsername = r.User1.Username
                }).ToList()
            .Select(r => new GeneralCustomEntities.SavedReportsGridView(r.ReportUserTemplateId, r.UserId, r.UserFullname, r.ReportId, r.ReportDisplayName, r.TemplateName,
                        r.StartDateOffset, r.EndDateOffset, r.UpdatedDate, r.UpdatedByUserId, r.UpdatedByUsername)).ToList();
        }


        //public IEnumerable<Dashboard> GetDashboardList(int userId)
        //{
        //    return context.Dashboards.Where(u => u.UserId == userId);
        //}
        public IEnumerable<GeneralCustomEntities.DashboardView> GetDashboardList(int userId)
        {
            return context.Dashboards.Where(u => u.UserId == userId).Select(d=> new GeneralCustomEntities.DashboardView()
            {
                DashboardId = d.DashboardId,
                DashboardName = d.DashboardName,
                DashboardTitle = d.DashboardTitle,
                UpdatedByUserId = d.UpdatedByUserId,
                UpdatedDate = d.UpdatedDate, 
                UserId = d.UserId
            });
        }

        public IEnumerable<string> GetAdditionalDocPrefixList(int lenderId)
        {
            List<string> ret = new List<string>();

            ret = context.AdditionalDocPrefixes.Where(x => !x.LenderId.HasValue || x.LenderId == lenderId).Select(x => x.PrefixText).ToList();
            //ret.Insert(0, "");
            return ret;

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
