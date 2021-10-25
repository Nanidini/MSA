using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Slick_Domain.Entities;
using Slick_Domain.Enums;
using Slick_Domain.Models;

namespace Slick_Domain.Services
{
    public class PrecedentRepository : IDisposable
    {
        private readonly SlickContext context;

        public PrecedentRepository(SlickContext Context)
        {
            context = Context;
        }

        public void AddPrecedent(Precedent precedent)
        {
                if (!context.Precedents.Where(p=>p==precedent).Any())
                {
                    context.Precedents.Add(precedent);
                }
                else
                {
                   // do nothing
                }

        }

        
    
        public PrecedentCustomEntities.PrecedentView GetPrecedentView(int precedentId)
        {
            var precedents = context.Precedents.AsNoTracking().Where(x => x.PrecedentId == precedentId);
            return GetPrecedentsView(precedents).FirstOrDefault();
        }

        public IEnumerable<PrecedentCustomEntities.PrecedentView> GetPrecedentsView()
        {
            var precedents = (IQueryable<Precedent>) context.Precedents.AsNoTracking();
            return GetPrecedentsView(precedents);
        }

        private IEnumerable<PrecedentCustomEntities.PrecedentView> GetPrecedentsView(IQueryable<Precedent> precedents)
        {
            return precedents
                .Select(s2 =>
                    new PrecedentCustomEntities.PrecedentView
                    {
                        PrecedentId = s2.PrecedentId,
                        HotDocsId = s2.HotDocsId,
                        Description = s2.Description,
                        LenderId = s2.LenderId,
                        LenderName = s2.Lender.LenderName,
                        MortMgrId = s2.MortMgrId,
                        MortMgrName = s2.MortMgr.MortMgrName,
                        StateId = s2.StateId,
                        StateName = s2.State.StateName,
                        MatterGroupId = s2.MatterGroupId,
                        MatterGroupName = s2.MatterGroupId.HasValue ? s2.MatterType.MatterTypeName : "- ALL -",
                        WFComponentId = s2.WFComponentId,
                        WFComponentName = s2.WFComponent.WFComponentName,
                        PrecedentOwnerTypeId = s2.PrecedentOwnerTypeId,
                        PrecedentOwnerTypeName = s2.PrecedentOwnerType.PrecedentOwnerTypeName,
                        PrecedentTypeId = s2.PrecedentTypeId,
                        PrecedentTypeName = s2.PrecedentType.PrecedentTypeName,
                        AssemblySwitches = s2.AssemblySwitches,
                        TemplateFileName = s2.TemplateFile,
                        DocName = s2.DocName,
                        DocType = s2.DocType,
                        PrecedentStatusTypeId = s2.PrecedentStatusTypeId,
                        PrecedentStatusTypeName = s2.PrecedentStatusType.PrecedentStatusTypeName,
                        IsAdhocAllowed = s2.IsAdhocAllowed,
                        IsPublicVisible = s2.IsPublicVisible,
                        UpdatedDate = s2.UpdatedDate,
                        UpdatedByUserId = s2.UpdatedByUserId,
                        UpdatedByUsername = s2.User.Username
                    })
                .OrderBy(s => s.Description)
                .ToList();
        }


        public IEnumerable<PrecedentView> GetPrecedentsForWFComponent(List<PrecedentView> docs, int componentId)
        {
            return from d in docs
                   join p in context.Precedents on d.HotDocsId equals p.HotDocsId
                   where p.WFComponentId == componentId
                   select d;
        }

        public IEnumerable<PrecedentCustomEntities.PrecedentBuildInstructionsView> GetPrecedentBuildInstructions(int id)
        {
            return context.PrecedentBuildInstructions.Where(pb => pb.PrecedentId == id)
                .Select(p=> new 
                {
                    p.PrecedentBuildInstructionId, p.InstructionApplication,p.InstructionCmd,p.BuildOrder,p.UpdatedDate,p.UpdatedByUserId,p.User.Username
                }).ToList()
                .Select(p2=> new PrecedentCustomEntities.PrecedentBuildInstructionsView
                {
                    ID = p2.PrecedentBuildInstructionId,
                    PrecedentId = id,
                    ApplicationName = p2.InstructionApplication,
                    MacroName = p2.InstructionCmd,
                    BuildOrder = p2.BuildOrder,
                    UpdatedDate = p2.UpdatedDate,
                    UpdatedByUserId = p2.UpdatedByUserId,
                    UpdatedByUsername = p2.Username
                })
                .OrderBy(o=>o.BuildOrder);
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
