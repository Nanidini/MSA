using Slick_Domain.Entities;
using Slick_Domain.Interfaces;
using Slick_Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Slick_Domain.Services
{
    public class OfficeRepository : IDisposable
    {
        private IRepository<Office> officeRepository = null;
        private readonly SlickContext context;

        public OfficeRepository(SlickContext Context)
        {
            context = Context;
        }

        private IRepository<Office> GetOfficeRepository()
        {
            return officeRepository ?? new Repository<Office>(context);
        }

        private IEnumerable<OfficeView> GetOfficesView(IQueryable<Office> officeQry)
        {
            return
                 officeQry
                .Select(s2 =>
                    new
                    {
                        s2.StreetAddress,
                        s2.OfficeId,
                        s2.OfficeName,
                        s2.PostCode,
                        s2.StateId,
                        s2.State.StateName,
                        s2.Suburb,
                        s2.Phone,
                        s2.Fax,
                        s2.User.Username,
                        s2.UpdatedDate
                    })
                .ToList()
                .Select(st => new OfficeView
                {
                    OfficeId = st.OfficeId,
                    AddressDetails = st.StreetAddress,
                    OfficeName = st.OfficeName,
                    StateId = st.StateId,
                    StateName = st.StateName,
                    Suburb = st.Suburb,
                    PostCode = st.PostCode,
                    Phone = st.Phone,
                    //Email = st.Email,
                    Fax = st.Fax,
                    UpdatedDate = st.UpdatedDate,
                    UpdatedBy = st.Username
                })
                .ToList();

        }

        public IEnumerable<OfficeView> GetOffices()
        {
            var qry = context.Offices.AsNoTracking();
            return GetOfficesView(qry);
        }


        public OfficeView GetOfficeView(int officeId)
        {
            var qry = context.Offices.AsNoTracking().Where(o => o.OfficeId == officeId);
            return GetOfficesView(qry).FirstOrDefault();
        }

        public OfficeView GetOfficeView(int stateId, int lenderId)
        {
            var qry = context.Offices.AsNoTracking().Where(o => o.StateId == stateId);
            var retVal =  GetOfficesView(qry).FirstOrDefault();

            var offAcc = context.OfficeAccounts.FirstOrDefault(o => o.Office.StateId == stateId && o.LenderId == lenderId);
            if (offAcc == null)
                offAcc = context.OfficeAccounts.FirstOrDefault(o => o.Office.StateId == stateId && o.LenderId == null);

            if (offAcc != null)
            {
                retVal.EFT_AccountName = offAcc.AccountName;
                retVal.EFT_BSB = offAcc.BSB;
                retVal.EFT_AccountNo = offAcc.AccountNo;
            }

            return retVal;
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
