using System;
using System.Collections.Generic;
using System.Linq;
using Slick_Domain.Interfaces;
using Slick_Domain.Entities;
using Slick_Domain.Models;

namespace Slick_Domain.Services {
    public class WFComponentRepository : IDisposable {
        private readonly IRepository<WFComponent> wfComponentRepository;
        private readonly SlickContext context;

        public WFComponentRepository(SlickContext Context) {
            wfComponentRepository = new Repository<WFComponent>(Context);
            context = Context;
        }

        public List<LookupValue> GetLookupList() {
            return (from wfc in context.WFComponents
                select new LookupValue() { id = wfc.WFComponentId, value = wfc.WFComponentName }).ToList();
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