using Slick_Domain.Models;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;

namespace Slick_Domain.Services {

    public class ReportsRepository : SlickRepository {

        public ReportsRepository(SlickContext context)
            : base(context) {
        }

        public Slick_Domain.Entities.GeneralCustomEntities.ReportView GetReportView(int reportId)
        {
            return context.Reports.Select(r => new Entities.GeneralCustomEntities.ReportView() { ReportId = r.ReportId, ReportName = r.ReportName, ReportServerReportName = r.ReportServerReportName, ReportDisplayName = r.ReportDisplayName, HasParams = r.ReportGridItems.Any() })
                .FirstOrDefault(r => r.ReportId == reportId);
        }

        public Slick_Domain.Entities.GeneralCustomEntities.ReportView GetReportView(Report dbReport)
        {
            if(dbReport != null)
            {
                return GetReportView(dbReport.ReportId);
            }
            else
            {
                return null;
            }
        }
        public ReportUserTemplate GetUserReport(int userReportId) {
            var report = (
                from r in context.ReportUserTemplates
                join rp in context.ReportUserTemplateParams on r.ReportUserTemplateId equals rp.ReportUserTemplateParamId
                join rt in context.ReportParamTypes on rp.ReportParam.ReportParamTypeId equals rt.ReportParamTypeId
                where r.ReportUserTemplateId == userReportId 
                select r).FirstOrDefault();

            return report;
        }

        public ReportUserTemplate GetUserReport(int reportId, int userId) {
            var report = (
                from r in context.ReportUserTemplates
                join rp in context.ReportUserTemplateParams on r.ReportUserTemplateId equals  rp.ReportUserTemplateParamId
                join rt in context.ReportParamTypes on rp.ReportParam.ReportParamTypeId equals rt.ReportParamTypeId
                where r.ReportId == reportId && r.UserId == userId
                select r).FirstOrDefault();

            return report;
        }

        public Report GetReport(int reportId) {
            return (from r in context.Reports
                    where r.ReportId == reportId
                    select r).FirstOrDefault();
        }

        public IEnumerable<Report> GetAvailableReports() {
            return from r in context.Reports
                   select r;
        }

        public IEnumerable<ReportUserTemplate> GetRecentReports(int userId) {
            return from r in context.ReportUserTemplates
                   where r.UserId == userId
                   orderby r.TemplateName
                   select r;
        }
    }
}