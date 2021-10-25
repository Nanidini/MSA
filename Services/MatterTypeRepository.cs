using System;
using System.Collections.Generic;
using System.Linq;
using Slick_Domain.Interfaces;
using Slick_Domain.Models;

namespace Slick_Domain.Services {
    public class MatterTypeRepository : IDisposable {
        private readonly IRepository<MatterType> matterTypeRepository;
        private readonly SlickContext context;

        public MatterTypeRepository(SlickContext Context) {
            matterTypeRepository = new Repository<MatterType>(Context);
            context = Context;
        }

        public List<LookupValue> GetLookupList() {
            return (from wfc in context.MatterTypes
                select new LookupValue() { id = wfc.MatterTypeId, value = wfc.MatterTypeName }).ToList();
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