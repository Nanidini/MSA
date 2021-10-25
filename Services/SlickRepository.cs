using Slick_Domain.Extensions;
using Slick_Domain.Models;
using System;

namespace Slick_Domain.Services
{
    public class SlickRepository : IDisposable, SlickTastic
    {
        protected readonly SlickContext context;
        public SlickRepository(SlickContext context)
        {
            this.context = context;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        public Slicktionary ErrorResults { get; set; }

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

        public bool IsErrorFree()
        {
            return ErrorResults.Count == 0;
        }

        public Slicktionary GetErrorResults()
        {
            return ErrorResults;
        }
        #endregion
    }
}
