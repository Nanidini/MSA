using System;
using System.Collections.Generic;
using System.Linq;
using Slick_Domain.Interfaces;
using Slick_Domain.Models;

namespace Slick_Domain.Services {
    /// <summary>
    /// The Discharge Type Repository. 
    /// Refactor - Does Not Extend SlickRepository. 
    /// </summary>
    public class DischargeTypeRepository : IDisposable {
        private readonly IRepository<DischargeType> dischargeTypeRepository;
        private readonly SlickContext context;

        /// <exclude />
        public DischargeTypeRepository(SlickContext Context) {
            dischargeTypeRepository = new Repository<DischargeType>(Context);
            context = Context;
        }
        /// <summary>
        /// Gets the lookup list for discharge types.
        /// </summary>
        /// <returns>A list of the Discharge Types to look up against.</returns>
        public List<LookupValue> GetLookupList() {
            return (from s in context.DischargeTypes
                select new LookupValue() { id = s.DischargeTypeId, value = s.DischargeTypeDesc }).ToList();
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