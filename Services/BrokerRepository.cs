using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Slick_Domain.Interfaces;
using Slick_Domain.Entities;
using Slick_Domain.Models;
using System.ComponentModel;
using Slick_Domain.Common;

namespace Slick_Domain.Services
{
    /// <summary>
    /// The Broker Repository. Used for getting broker information from the broker table.
    /// </summary>
    /// <remarks>
    /// Consider Refactoring. Does not extend context as base.
    /// </remarks>
    public class BrokerRepository : IDisposable
    {
        /// <summary>
        /// The property that interfaces with the database. Has a template of functions. 
        /// </summary>
        /// <remarks>
        /// Seems a bit pointless
        /// </remarks>
        private IRepository<Broker> brokerRepository = null;
        /// <summary>
        /// Slickcontext as doesn't extend SlickRepository as base class 
        /// </summary>
        private readonly SlickContext context;

        /// <summary>
        /// Constructor method that assigns the context provided to the inner context field.
        /// </summary>
        /// <param name="Context">The current Slick Context.</param>
        public BrokerRepository(SlickContext Context)
        {
            context = Context;
        }

        /// <summary>
        /// Return just the broker repository. Useful if you don't want to navigate through the context?
        /// </summary>
        /// <returns>A Broker Repository with methods.</returns>
        private IRepository<Broker> GetBrokerRepository()
        {
            return brokerRepository ?? new Repository<Broker>(context);
        }
        /// <summary>
        /// A function that gets all brokers from the database.
        /// </summary>
        /// <returns>All Brokers</returns>
        /// <remarks>
        /// Seems to be getting used in Slick Security. Could function exist in BrokerRepository?
        /// <see cref="SlickSecurity.RegisterBroker(User, bool, bool, bool)"/>
        /// <seealso cref="<see cref="SlickSecurity.RequestRegisterBroker(BrokerInfo)"/>"/>
        /// </remarks>
        public IQueryable<Broker> GetBrokerDetails()
        {
            IQueryable<Broker> brokers = context.Brokers;
            return brokers;
        }

        /// <summary>
        /// A function that creates the custom <see cref="BrokerEntities.BrokerGridView"/> view for brokers supplied.
        /// </summary>
        /// <param name="brokerList">List of Brokers to create views for.</param>
        /// <returns>An enumerable collection of <see cref="BrokerEntities.BrokerGridView"/> to be used.</returns>
        /// 
        private IEnumerable<BrokerEntities.BrokerGridView> GetBrokerGridViewList(IQueryable<Broker> brokerList)
        {
            return brokerList
                    .Select(s2 =>
                        new {
                            s2.BrokerId,
                            s2.PrimaryContactId,
                            s2.PrimaryContact.Lastname,
                            s2.PrimaryContact.Firstname,
                            s2.PrimaryContact.Phone,
                            s2.PrimaryContact.Mobile,
                            s2.PrimaryContact.Fax,
                            s2.PrimaryContact.Email,
                            s2.StreetAddress,
                            s2.CompanyName,
                            s2.Suburb,
                            s2.State.StateName,
                            s2.PostCode,
                            s2.Enabled,
                            s2.UpdatedDate,
                            s2.User.Username,
                            MatterCount = s2.Matters.Count()
                        })
                        //.Take(5)
                    .ToList()
                    .Select(st => new BrokerEntities.BrokerGridView
                    {
                        BrokerId = st.BrokerId,
                        PrimaryContactId = st.PrimaryContactId,
                        Fullname = EntityHelper.GetFullName(st.Lastname, st.Firstname),
                        Phone = st.Phone,
                        Mobile = st.Mobile,
                        Fax = st.Fax,
                        Email = st.Email,
                        CompanyName = st.CompanyName,
                        StreetAddress = st.StreetAddress,
                        Suburb = st.Suburb,
                        StateName = st.StateName,
                        PostCode = st.PostCode,
                        Enabled = st.Enabled,
                        UpdatedDate = st.UpdatedDate,
                        UpdatedByUsername = st.Username,
                        MatterCount = st.MatterCount
                    })
                    .ToList();
        }
        /// <summary>
        /// Gets all Brokers and formats them as <see cref="BrokerEntities.BrokerGridView"/>.
        /// </summary>
        /// <returns>Enumberable Collection of <see cref="BrokerEntities.BrokerGridView"/></returns>
        public IEnumerable<BrokerEntities.BrokerGridView> GetBrokersGridView()
        {
            IQueryable<Broker> brokers = context.Brokers.AsNoTracking();
            return GetBrokerGridViewList(brokers);
        }


        /// <summary>
        /// Gets Enumerable <see cref="BrokerEntities.BrokerGridView"/>with boolean argument for whether the brokers should be active brokers or all brokers.
        /// </summary>
        /// <param name="ActiveOnly">Boolean argument for if the brokers should be active or active and disabled</param>
        /// <returns>Enumberable Collection of <see cref="BrokerEntities.BrokerGridView"/></returns>
        public IEnumerable<BrokerEntities.BrokerGridView> GetBrokersGridView(bool ActiveOnly)
        {
            IQueryable<Broker> brokers;
            if (ActiveOnly)
                brokers = context.Brokers.AsNoTracking().Where(b => b.Enabled);
            else
                brokers = context.Brokers.AsNoTracking();
            return GetBrokerGridViewList(brokers);
        }
        /// <summary>
        /// Gets a <see cref="BrokerEntities.BrokerGridView"/> for an individual Broker.
        /// </summary>
        /// <param name="brokerId">The Id of the broker to get the view for </param>
        /// <returns>Returns a <see cref="BrokerEntities.BrokerGridView"/> for the broker id supplied</returns>
        /// <remarks>
        /// A precondition of this function is that the BrokerId is valid!
        /// </remarks>
        public BrokerEntities.BrokerGridView GetBrokerGridView(int brokerId)
        {
            var brokers = context.Brokers.AsNoTracking().Where(b=> b.BrokerId == brokerId);
            return GetBrokerGridViewList(brokers).FirstOrDefault();
        }


        /// <summary>
        /// Gets the <see cref="BrokerEntities.BrokerView"/> for individual Broker Id.
        /// </summary>
        /// <param name="brokerId">The ID of the Broker.</param>
        /// <returns>The <see cref="BrokerEntities.BrokerView"/> for the specified Broker.</returns>
        public BrokerEntities.BrokerView GetBrokerView(int brokerId)
        {
            var broker = GetBrokerRepository().FindById(brokerId);

            var brokerView = new BrokerEntities.BrokerView(brokerId, broker.PrimaryContactId, broker.PrimaryContact.Lastname, broker.PrimaryContact.Firstname,
                    broker.PrimaryContact.Phone, broker.PrimaryContact.Mobile, broker.PrimaryContact.Fax, broker.PrimaryContact.Email,
                    broker.CompanyName, broker.StreetAddress, broker.Suburb, broker.StateId, broker.State?.StateName, broker.PostCode,
                    broker.Enabled, broker.UpdatedDate, broker.UpdatedByUserId, broker.User.Username, null);

            return brokerView;
        }

        /// <summary>
        /// Gets the Enumerable <see cref="BrokerEntities.BrokerLenderCodeView"/> for a Broker for diferent lenders. 
        /// </summary>
        /// <param name="brokerId">The ID of the Broker.</param>
        /// <returns>Returns an Enumerable Collection of <see cref="BrokerEntities.BrokerLenderCodeView"/></returns>
        public IEnumerable<BrokerEntities.BrokerLenderCodeView> GetBrokerCodes(int brokerId)
        {
            return context.BrokerLenderCodes.Where(x=>x.BrokerId==brokerId)
                .Select(x => new BrokerEntities.BrokerLenderCodeView { BrokerLenderCodeId = x.BrokerLenderCodeId,
                                                                                 BrokerId = x.BrokerId,
                                                                                 LenderId = x.LenderId,
                                                                               LenderName = x.Lender.LenderName,
                                                                               UniqueCode = x.UniqueCode
                });
        }


        

        /// <summary>
        /// Returns a Broker from the PersonCompanyDetails Model.
        /// </summary>
        /// <param name="model">The PersonCompanyDetails model from the xml extraction.</param>
        /// <returns>A Broker class of the broker from the model. Or Null if not found.</returns>
        public Broker GetBrokerFromXmlMatterModel(PersonCompanyDetails model, int lenderId, string mortMgrRef = null)
        {
            //if  (model.CompanyName == null) return null;
            //var brokers = context.Brokers.Where(b => b.CompanyName == model.CompanyName && b.Enabled);
            //Broker broker = null;
            //if (brokers.Any())
            //{
            //    broker = (from b in brokers join c in context.PrimaryContacts on b.PrimaryContactId equals c.PrimaryContactId
            //                   where (c.Firstname == model.FirstName && c.Lastname == model.LastName)
            //                          || (c.Firstname == model.FirstName && c.Lastname == null)
            //                   orderby c.Firstname descending // Get null last
            //                   select b)?.FirstOrDefault();

            //    return broker;
            //}
            //return null;
            if(model.LookupId == "BROKER")
            {
                model.LookupId = null;
            }
            
            if(mortMgrRef != null && model.LookupId != null)
            {
                var codeBrokers = context.BrokerLenderCodes.Where(b => b.LenderId == lenderId && b.UniqueCode == mortMgrRef + model.LookupId).Select(b=>b.Broker);
                if (codeBrokers.Any())
                {
                    return codeBrokers.FirstOrDefault();
                }
            }
            if(model.LookupId != null && (lenderId == 139 || lenderId == 1))
            {
                var codeBrokers = context.BrokerLenderCodes.Where(b => b.LenderId == lenderId && b.UniqueCode == model.LookupId).Select(b => b.Broker);
                if (codeBrokers.Any() && codeBrokers.FirstOrDefault().PrimaryContact.Email == model.Email )
                {
                    return codeBrokers.FirstOrDefault();
                }
            }

            var brokers = context.Brokers.Where(b => b.Enabled &&
                (b.PrimaryContact.Email == model.Email && (b.PrimaryContact.Mobile == model.Mobile ||
                    b.PrimaryContact.Phone == model.Mobile))).OrderByDescending(b => b.Matters.Count()); ;
            if (brokers.Any())
            {
                return brokers.FirstOrDefault();
            }
            return null;
        }


        /// <summary>
        /// Gets the migrated broker from the Migrated table in the database based on a Matter Id.
        /// </summary>
        /// <param name="matterId"></param>
        /// <returns></returns>
        public Broker GetMigratedBroker(int matterId)
        {
            var brokerDets = context.MIG_Brokers.FirstOrDefault(x => x.MatterId == matterId);
            if (brokerDets == null || brokerDets.Firstname == null || brokerDets.Lastname == null) return null;
            var firstchar = brokerDets.Firstname.Substring(0, 1);

            return context.PrimaryContacts.FirstOrDefault
                (x => x.Email.ToLower() == brokerDets.Email.ToLower() && x.Lastname.ToLower() == brokerDets.Lastname.ToLower() && x.Firstname.StartsWith(firstchar)) 
                ?.Brokers.FirstOrDefault();
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
