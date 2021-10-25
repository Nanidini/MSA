using Slick_Domain.Interfaces;
using Slick_Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Slick_Domain.Services
{
    public class ReissueReasonRepository : IDisposable
    {
        private readonly IRepository<ReissueReason> reissueReasonRepository;
        private readonly SlickContext context;
        public ReissueReasonRepository(SlickContext Context)
        {
            context = Context;
            reissueReasonRepository = new Repository<ReissueReason>(context);

        }

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                context.Dispose();
                disposedValue = true;
            }
        }

        public bool addReissueReason(int reissueReasonId, string reissueReasonText, int userId, int matterId, int matterWfComponentId)
        {
            try
            {
                ReissueReason reissueReason = new ReissueReason();
                reissueReason.MatterId = matterId;
                reissueReason.ReissueTxt = reissueReasonText;
                reissueReason.MatterWFComponentId = matterWfComponentId; 
                reissueReason.ReissueTypeId = reissueReasonId;
                reissueReason.UpdatedDate = System.DateTime.Now;
                reissueReason.UpdatedByUserId = GlobalVars.CurrentUser.UserId;
                reissueReasonRepository.Add(reissueReason);
                context.SaveChanges();
            }
            catch(Exception ex)
            {
                Slick_Domain.Handlers.ErrorHandler.LogError(ex);
                
                return false; 
            }
            return true;
        }


        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }

    }
}
