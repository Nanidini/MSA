// ***********************************************************************
// Assembly         : Slick_Domain
// Author           : Jeff.Thorpe
// Created          : 09-26-2018
//
// Last Modified By : Jeff.Thorpe
// Last Modified On : 09-26-2018
// ***********************************************************************
// <copyright file="RecentMatterRepository.cs" company="">
//     Copyright ©  2016
// </copyright>
// <summary></summary>
// ***********************************************************************

using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using Slick_Domain.Models;

namespace Slick_Domain.Services {
    /// <summary>
    /// Class RecentMatterRepository.
    /// </summary>
    /// <seealso cref="Slick_Domain.Services.SlickRepository" />
    public class RecentMatterRepository : SlickRepository {
        /// <summary>
        /// The logger
        /// </summary>
        private Logger _logger = LogManager.GetCurrentClassLogger();
        /// <summary>
        /// Initializes a new instance of the <see cref="RecentMatterRepository"/> class.
        /// </summary>
        /// <param name="context">The context.</param>
        public RecentMatterRepository(SlickContext context)
            : base(context) {
        }
        /// <summary>
        /// Gets the recent matters.
        /// </summary>
        /// <param name="userId">The user identifier.</param>
        /// <returns>List&lt;RecentMatterInfo&gt;.</returns>
        public List<RecentMatterInfo> GetRecentMatters(int userId) {
            return (from rm in context.UserPrefRecentMatters
                orderby rm.AccessedDate descending 
                where rm.UserId == userId
                select new RecentMatterInfo {MatterId = rm.MatterId, LenderId = rm.Matter.LenderId, LenderName = rm.Matter.Lender.LenderName, Description = rm.Matter.MatterDescription, LenderRefNo=rm.Matter.LenderRefNo }).Take(20).ToList();
        }
        /// <summary>
        /// Records the recent matter.
        /// </summary>
        /// <param name="userId">The user identifier.</param>
        /// <param name="matterId">The matter identifier.</param>
        /// <returns><c>true</c> if record added, <c>false</c> otherwise.</returns>
        public bool RecordRecentMatter(int userId, int matterId) {
            try {
                var matter = context.UserPrefRecentMatters.FirstOrDefault(rm => rm.UserId == userId && rm.MatterId == matterId);

                if (matter != null) {
                    context.UserPrefRecentMatters.Remove(matter);
                }

                matter = context.UserPrefRecentMatters.Create();

                matter.UserId = userId;//GlobalVars.CurrentUser.UserId;
                matter.MatterId = matterId;
                matter.AccessedDate = DateTime.Now;

                context.UserPrefRecentMatters.Add(matter);
                context.SaveChanges();

                return true;
            }
            catch (Exception e) {
                _logger.Error(e, $"Error recording UserId: {userId} MatterId: {matterId}");
                return false;
            }
        }
    }
}