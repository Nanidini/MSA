using System;
using System.Collections.Generic;
using System.Linq;
using Slick_Domain.Interfaces;
using Slick_Domain.Models;

namespace Slick_Domain.Services {
    public class StateRepository : IDisposable {
        private readonly IRepository<State> stateRepository;
        private readonly SlickContext context;

        public StateRepository(SlickContext Context) {
            stateRepository = new Repository<State>(Context);
            context = Context;
        }

        public List<LookupValue> GetLookupList() {
            return (from s in context.States
                select new LookupValue() { id = s.StateId, value = s.StateName }).ToList();
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