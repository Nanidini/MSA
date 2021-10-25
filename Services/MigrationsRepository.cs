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
    public class MigrationsRepository : IDisposable
    {
        private readonly SlickContext context;

        public MigrationsRepository(SlickContext Context)
        {
            context = Context;
        }


        public IEnumerable<MatterCustomEntities.MigrateMatterEntity> GetComponentsForMigration(bool preProcessing)
        {
            return GetComponentsForMigration(context.Matters.AsNoTracking(),preProcessing);
        }

        public IEnumerable<MatterCustomEntities.MigrateMatterEntity> GetComponentsForMigration(List<int> ids, bool preProcessing)
        {
            return GetComponentsForMigration(context.Matters.AsNoTracking().Where(x => ids.Contains(x.MatterId)),preProcessing);
        }

        private IEnumerable<MatterCustomEntities.MigrateMatterEntity> GetComponentsForMigration(IQueryable<Matter> matters, bool preProcessing)
        {

            return (from m in matters
                    join m2 in context.MIG_Matter on m.MatterId equals m2.MatterId
                    where m2.ToBeProcessed == true && (m2.Processed == null || m2.Processed == false) &&
                    (m2.PreProcessed == !preProcessing || m2.PreProcessed == null && preProcessing)
                    select m)               
            .Select(s2 => new MatterCustomEntities.MigrateMatterEntity
            {
                MatterId = s2.MatterId,
                ComponentId = s2.MatterWFComponents
                    .Where(x=>x.WFComponent.WFComponentName == "Document Preparation" || x.WFComponent.WFComponentName == "Create Front Cover")
                    .OrderByDescending(o => o.MatterWFComponentId).FirstOrDefault().MatterWFComponentId,
                LenderId = s2.LenderId,
                MatterGroupId = s2.MatterGroupTypeId
            })
            .OrderBy(x=>x.MatterId);
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

        public XmlMatterDetailsModel BuildXmlMatterDetailsModel(int matterId)
        {
            return context.Matters.AsNoTracking()
                .Where(t => t.MatterId == matterId)
                .Select(s2 =>
                    new
                    {
                        s2.MatterId,
                        s2.MatterGroupTypeId,
                        Lender = s2.LenderId,
                        LenderName = s2.Lender.LenderName,
                        M_Name = s2.MortMgr.MortMgrName,
                        M_StreetAddress = s2.MortMgr.StreetAddress,
                        M_Suburb = s2.MortMgr.Suburb,
                        M_State = s2.MortMgr.State.StateName,
                        M_PostCode = s2.MortMgr.PostCode,
                        B_Lastname = s2.Broker.PrimaryContact.Lastname,
                        B_Firstname = s2.Broker.PrimaryContact.Firstname,
                        B_Address1 = s2.Broker.StreetAddress,
                        B_Suburb = s2.Broker.Suburb,
                        B_State = s2.Broker.State.StateName,
                        B_PostCode = s2.Broker.PostCode,
                        B_Contact = s2.Broker.PrimaryContact.Lastname,
                        B_Phone = s2.Broker.PrimaryContact.Phone,
                        B_Email = s2.Broker.PrimaryContact.Email,
                        SecurityDetails = s2.MatterSecurities
                        .Select(sc => new Slick_Domain.Entities.SecurityDetail
                        {
                            ID = sc.MatterSecurityId,
                            Address = new AddressDetails
                            {
                                AddressLine1 = sc.StreetAddress,
                                State = sc.State.StateName
                            }
                            , LotDetails = new LotDetail
                            {
                                Description = sc.MatterSecurityTitleRefs.Where(l=> l.MatterSecurityId == sc.MatterSecurityId).
                                Select(ld=> ld.LandDescription).FirstOrDefault()
                            }
                        })
                    })
                .ToList()
                .Select(st => new XmlMatterDetailsModel
                {
                    MatterId = matterId,
                    MatterType = context.MatterTypes.FirstOrDefault(mt => mt.MatterTypeGroupId == st.MatterGroupTypeId).MatterTypeName,
                    LenderId = st.Lender,
                    LenderName = st.LenderName,
                    MortMgr = st.M_Name,
                    SecurityDetails = st.SecurityDetails.ToList()
                })
                .FirstOrDefault();
        }

        #endregion
    }
}
