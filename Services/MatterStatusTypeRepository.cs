using System;
using System.Collections.Generic;
using System.Linq;
using Slick_Domain.Interfaces;
using Slick_Domain.Models;

namespace Slick_Domain.Services {
    public class MatterStatusTypeRepository : IDisposable {
        private readonly IRepository<MatterStatusType> matterStatusTypeRepository;
        private readonly SlickContext context;

        public MatterStatusTypeRepository(SlickContext Context) {
            matterStatusTypeRepository = new Repository<MatterStatusType>(Context);
            context = Context;
        }

        public List<LookupValue> GetLookupList() {
            return (from wfc in context.MatterStatusTypes
                select new LookupValue() { id = wfc.MatterStatusTypeId, value = wfc.MatterStatusTypeName }).ToList();
        }

        #region IDisposable Support

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing) {
            if (!disposedValue) {
                context.Dispose();
                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose() {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }

        #endregion
    }
}