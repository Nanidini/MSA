using System;
using System.Collections.Generic;
using System.Linq;
using Slick_Domain.Interfaces;
using Slick_Domain.Models;

namespace Slick_Domain.Services {
    public class SettlementTypeRepository : IDisposable {
        private readonly IRepository<SettlementType> settlementTypeRepository;
        private readonly SlickContext context;

        /// <exclude />
        public SettlementTypeRepository(SlickContext Context) {
            settlementTypeRepository = new Repository<SettlementType>(Context);
            context = Context;
        }

        public List<LookupValue> GetLookupList() {
            return (from s in context.SettlementTypes
                select new LookupValue() { id = s.SettlementTypeId, value = s.SettlementTypeDesc }).ToList();
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
    public class SecurityStateRepository : IDisposable {
        private readonly IRepository<State> securityStateRepository;
        private readonly SlickContext context;

        /// <exclude />
        public SecurityStateRepository(SlickContext Context) {
            securityStateRepository = new Repository<State>(Context);
            context = Context;
        }

        public List<LookupValue> GetLookupList() {
            return (from s in context.SettlementTypes
                select new LookupValue() { id = s.SettlementTypeId, value = s.SettlementTypeDesc }).ToList();
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