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
using RME = Slick_Domain.Entities.RelationshipManagerEntities;

namespace Slick_Domain.Services
{
    /// <summary>
    /// The RelationshipManager Repository. Used for getting RelationshipManager information from the RelationshipManager table.
    /// </summary>
    /// <remarks>
    /// Consider Refactoring. Does not extend context as base.
    /// </remarks>
    public class RelationshipManagerRepository : IDisposable
    {
        /// <summary>
        /// The property that interfaces with the database. Has a template of functions. 
        /// </summary>
        /// <remarks>
        /// Seems a bit pointless
        /// </remarks>
        private IRepository<RelationshipManager> relationshipManagerRepository = null;
        /// <summary>
        /// Slickcontext as doesn't extend SlickRepository as base class 
        /// </summary>
        private readonly SlickContext context;

        /// <summary>
        /// Constructor method that assigns the context provided to the inner context field.
        /// </summary>
        /// <param name="Context">The current Slick Context.</param>
        public RelationshipManagerRepository(SlickContext Context)
        {
            context = Context;
        }

        /// <summary>
        /// Return just the RelationshipManager repository. Useful if you don't want to navigate through the context?
        /// </summary>
        /// <returns>A RelationshipManager Repository with methods.</returns>
        private IRepository<RelationshipManager> GetRelationshipManagerRepository()
        {
            return relationshipManagerRepository ?? new Repository<RelationshipManager>(context);
        }


        #region Editable Grid functions
        public IEnumerable<RME.RelationshipManagerGridView> GetAllRelationshipManagerGridViews(bool showDisabled = false)
        {
            return GetRelationshipManagerGridViews(context.RelationshipManagers.Where(x => showDisabled || x.Enabled));
        }

        public IEnumerable<RME.RelationshipManagerGridView> GetAllRelationshipManagerGridViewsForLender(int lenderId, bool showDisabled = false)
        {
            return GetRelationshipManagerGridViews(context.RelationshipManagers.Where(x => x.LenderId == lenderId && (showDisabled || x.Enabled)));
        }

        public RME.RelationshipManagerGridView GetRelationshipManagerView(int relationshipManagerId)
        {
            return GetRelationshipManagerGridViews(context.RelationshipManagers.Where(x => x.RelationshipManagerId == relationshipManagerId)).FirstOrDefault();
        }
       
        public IEnumerable<RME.RelationshipManagerGridView> GetRelationshipManagerGridViews(IQueryable<RelationshipManager> qry)
        {
            return qry.Select(x => new
            {
                x.RelationshipManagerId,
                x.PrimaryContactId,
                x.LenderId,
                x.Lender.LenderName,
                x.PrimaryContact.Firstname,
                x.PrimaryContact.Lastname,
                x.PrimaryContact.Phone,
                x.PrimaryContact.Fax,
                x.PrimaryContact.Mobile,
                x.PrimaryContact.Email,
                x.Enabled,
                x.UpdatedDate,
                x.UpdatedByUserId,
                UpdatedByUserName = x.User.Username,
                BrokerRelationshipManagers = x.BrokerRelationshipManagers
                .Select(b => new
                {
                    b.BrokerId,
                    b.Broker.PrimaryContactId,
                    b.Broker.PrimaryContact.Lastname,
                    b.Broker.PrimaryContact.Firstname,
                    b.Broker.PrimaryContact.Phone,
                    b.Broker.PrimaryContact.Mobile,
                    b.Broker.PrimaryContact.Fax,
                    b.Broker.PrimaryContact.Email,
                    b.Broker.CompanyName,
                    b.Broker.StreetAddress,
                    b.Broker.Suburb,
                    b.Broker.StateId,
                    StateName = b.Broker.StateId.HasValue ? b.Broker.State.StateName : null,
                    b.Broker.PostCode,
                    b.Broker.Enabled,
                    b.Broker.UpdatedDate,
                    b.Broker.UpdatedByUserId,
                    UpdatedByUserName = b.Broker.User.Username,
                    x.RelationshipManagerId
                })
            })
            .ToList()
            .Select(x => new RME.RelationshipManagerGridView(x.RelationshipManagerId, x.PrimaryContactId, x.LenderId, x.LenderName,
                x.Firstname, x.Lastname, x.Phone, x.Fax, x.Mobile, x.Email, x.Enabled,
                x.UpdatedDate, x.UpdatedByUserId, x.UpdatedByUserName,
                x.BrokerRelationshipManagers
                .Select(b => new BrokerEntities.BrokerView(
                    b.BrokerId,
                    b.PrimaryContactId,
                    b.Lastname,
                    b.Firstname,
                    b.Phone,
                    b.Mobile,
                    b.Fax,
                    b.Email,
                    b.CompanyName,
                    b.StreetAddress,
                    b.Suburb,
                    b.StateId,
                    b.StateName,
                    b.PostCode,
                    b.Enabled,
                    b.UpdatedDate,
                    b.UpdatedByUserId,
                    b.UpdatedByUserName,
                    b.RelationshipManagerId)).ToList()));
        }

        #endregion

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