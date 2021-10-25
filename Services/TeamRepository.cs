using System;
using System.Collections.Generic;
using System.Linq;
using Slick_Domain.Models;
using Slick_Domain.Interfaces;
using Slick_Domain.Entities;
using Slick_Domain.Common;
using Slick_Domain.Enums;
using Check = System.Predicate<object>;
//using DocuSign.eSign.Model;
using Newtonsoft.Json;
//using static Slick_Domain.Entities.DocuSignCustomEntities;
using static Slick_Domain.Entities.TeamCustomEntities;
using Slick_Domain.Extensions;
using System.Threading.Tasks;
using System.Collections.ObjectModel;

namespace Slick_Domain.Services
{
    public class TeamRepository : IDisposable
    {
        private readonly SlickContext context;

        public TeamRepository(SlickContext Context)
        {
            context = Context;
        }

        public void CheckLenderId()
        {


            if (CheckSlickDb(new Check(x => (x as Matter).Lender.LenderName == "Rams")) && CheckSlickDb(new Check(x => string.IsNullOrEmpty((x as MatterWFXML).LenderXML))))
            {

            }
        }

        public bool CheckSlickDb(Predicate<object> x, UnitOfWork uow = null)
        {
            if (uow == null)
            {
                using (uow = new UnitOfWork())
                {
                    return CheckSlickDb(x, uow);
                }
            }

            switch (x)
            {
                case Predicate<Matter> m:
                    return uow.Context.Matters.Any(f => m(f));
                default:
                    return false;

            }


        }

        public List<TeamCustomEntities.TeamView> GetTeamList()
        {
            var teamList = context.Teams.Select(t => new TeamCustomEntities.TeamView()
            {
                TeamId = t.TeamId,
                TeamName = t.TeamName,
                TeamTypeId = t.TeamTypeId,
                TeamTypeName = t.TeamType.TeamTypeName,
                TeamDescription = t.TeamDescription,
                LenderIds = t.TeamLenders.Select(l => l.LenderId).ToList(),
                StateId = t.StateId.HasValue ? t.StateId.Value : -1,
                StateName = t.StateId.HasValue ? t.State.StateName : "All",
                LenderNames = "All",
                UpdatedByUserId = t.UpdatedByUserId,
                UpdatedByUsername = context.Users.Where(u => u.UserId == t.UpdatedByUserId).Select(x => x.Username).FirstOrDefault(),
                MemberCount = t.UserTeams.Distinct().Count(),
                Enabled = t.Enabled
            }).ToList();


            foreach (var team in teamList)
            {
                if (team.LenderIds != null && team.LenderIds.Count > 0)
                {
                    team.LenderNames = string.Join(",\n", context.Lenders.Where(l => team.LenderIds.Contains(l.LenderId)).Select(x => x.LenderName).ToList());
                }
            }

            return teamList.ToList();
        }

        public List<TeamCustomEntities.TeamView> GetTeamsForUser(int userId)
        {
            var ret = context.UserTeams.Where(ut => ut.UserId == userId && ut.Team.Enabled).Select
                (
                    x => new TeamCustomEntities.TeamView()
                    {
                        TeamId = x.TeamId,
                        TeamName = x.Team.TeamName,
                        TeamDescription = x.Team.TeamDescription,
                        LenderIds = x.Team.TeamLenders.Select(l => l.LenderId).ToList(),
                        UpdatedByUserId = x.Team.UpdatedByUserId,
                        UpdatedByUsername = context.Users.Where(u => u.UserId == x.Team.UpdatedByUserId).Select(z => z.Username).FirstOrDefault(),
                        MemberCount = x.Team.UserTeams.Distinct().Count(),
                        Enabled = x.Team.Enabled
                    }
                ).ToList();
            return ret;
        }
        public List<TeamCustomEntities.TeamView> GetTeamsForUser(int userId, int teamRoleTypeId)
        {
            var ret = context.UserTeams.Where(ut => ut.UserId == userId && ut.Team.Enabled && ut.UserTeamRoles.Any(x => x.TeamRoleTypeId == teamRoleTypeId)).Select
                (
                    x => new TeamCustomEntities.TeamView()
                    {
                        TeamId = x.TeamId,
                        TeamName = x.Team.TeamName,
                        TeamDescription = x.Team.TeamDescription,
                        LenderIds = x.Team.TeamLenders.Select(l => l.LenderId).ToList(),
                        UpdatedByUserId = x.Team.UpdatedByUserId,
                        UpdatedByUsername = context.Users.Where(u => u.UserId == x.Team.UpdatedByUserId).Select(z => z.Username).FirstOrDefault(),
                        MemberCount = x.Team.UserTeams.Distinct().Count(),
                        Enabled = x.Team.Enabled
                    }
                ).ToList();
            return ret;
        }

        public Team SaveTeamDetails(TeamCustomEntities.TeamView teamView)
        {
            Team team = new Team();
            if (teamView.TeamId == -1) //adding new team
            {
                team = new Team
                {
                    TeamName = teamView.TeamName,
                    TeamDescription = teamView.TeamDescription,
                    TeamTypeId = teamView.TeamTypeId,
                    Enabled = teamView.Enabled,
                    UpdatedDate = DateTime.Now,
                    UpdatedByUserId = GlobalVars.CurrentUser.UserId,
                    TeamDailyToPipelineRatio = teamView.TeamDailyToPipelineRatio
                };

                if (teamView.StateId.HasValue && teamView.StateId != -1)
                {
                    team.StateId = teamView.StateId.Value;
                }

                team = context.Teams.Add(team);

            }
            else //updating team
            {
                team = context.Teams.Where(t => t.TeamId == teamView.TeamId).FirstOrDefault();
                team.TeamName = teamView.TeamName;
                team.TeamTypeId = teamView.TeamTypeId;
                team.TeamDescription = teamView.TeamDescription;
                team.Enabled = teamView.Enabled;
                team.UpdatedDate = DateTime.Now;
                team.UpdatedByUserId = GlobalVars.CurrentUser.UserId;
                team.TeamDailyToPipelineRatio = teamView.TeamDailyToPipelineRatio;

                if (teamView.StateId.HasValue && teamView.StateId != -1)
                {
                    team.StateId = teamView.StateId.Value;
                }
                else
                {
                    team.StateId = null;
                }

            }

            return team;
        }
        public void SaveAllocationLog(int matterId, int? teamId, int allocatedToUserId, int teamAllocationTypeId)
        {
            context.UserAllocationLogs.Add(new UserAllocationLog()
            {
                MatterId = matterId,
                TeamId = teamId,
                AllocatedToUserId = allocatedToUserId,
                TeamAllocationTypeId = teamAllocationTypeId,
                AllocatedByUserId = GlobalVars.CurrentUser.UserId,
                AllocatedDate = DateTime.Now
            });
            context.SaveChanges();
        }


        public IEnumerable<TeamCustomEntities.UserTeamListView> GetAllocationPool(int lenderId, int teamTypeId, int? stateId, int? mortMgrId, bool allocationScoreOnlyFromChosenTeam, bool onlyMatchingOffice = false)
        {
            IQueryable<Team> matchingTeams = null;


            bool filterMortMgrId = false;

            if (stateId.HasValue)
            {
                matchingTeams = context.Teams.Where(t => t.Enabled && t.TeamLenders.Any(l => l.LenderId == lenderId && (!l.MortMgrId.HasValue || l.MortMgrId == mortMgrId)) && t.TeamTypeId == teamTypeId && t.TeamAllocationStates.Any(s => s.StateId == stateId));
            }
            else
            {
                matchingTeams = context.Teams.Where(t => t.Enabled && t.TeamLenders.Any(l => l.LenderId == lenderId && (!l.MortMgrId.HasValue || l.MortMgrId == mortMgrId)) && t.TeamTypeId == teamTypeId);
            }

            if (mortMgrId.HasValue && matchingTeams.Any(x => x.TeamLenders.Any(t => t.LenderId == lenderId && t.MortMgrId.HasValue && t.MortMgrId.Value == mortMgrId)))
            {
                matchingTeams = matchingTeams.Where(x => x.TeamLenders.Any(t => t.LenderId == lenderId && t.MortMgrId.HasValue && t.MortMgrId == mortMgrId));
                filterMortMgrId = matchingTeams.Count() == 1;
            }


            if (matchingTeams == null)
            {
                throw new Exception("No team found for matter.");
            }

            List<TeamCustomEntities.UserTeamListView> userPool = new List<TeamCustomEntities.UserTeamListView>();
            foreach (int teamId in matchingTeams.Select(i => i.TeamId).ToList())
            {
                userPool = userPool
                    .Concat(GetUserListForTeam(teamId, lenderId, filterMortMgrId ? mortMgrId : null, allocationScoreOnlyFromChosenTeam, onlyMatchingOffice: onlyMatchingOffice)
                    .Where(
                        u => u.CanBeAllocated && u.AllocationWeight > 0 && !userPool.Select(x => x.UserId).Contains(u.UserId) //for all the lists, add users that aren't already on there. This'll be a bit weird if users are on multiple lists as it will only get their first found parameters.

                    )).ToList();
            }

            return userPool;
        }


        public TeamCustomEntities.UserTeamListView GetFileOwnerToAllocate(int lenderId, int teamTypeId, int? stateId, int? mortMgrId, bool allocationScoreOnlyFromChosenTeam = false, bool onlyMatchingOffice = false)
        {
            var userPool = GetAllocationPool(lenderId, teamTypeId, stateId, mortMgrId, allocationScoreOnlyFromChosenTeam, onlyMatchingOffice: onlyMatchingOffice).ToList();

            if (userPool.Count == 0) return null;

            userPool = userPool.OrderBy(u => u.AllocationScore).ToList();

            var firstPotentialMatch = userPool.FirstOrDefault();

            var userToAllocate = firstPotentialMatch;
            if (userToAllocate != null)
            {
                var userDetails = context.Users.Where(u => u.UserId == userToAllocate.UserId).Select(x => new { Firstname = x.Firstname, Lastname = x.Lastname, Email = x.Email }).FirstOrDefault();
                userToAllocate.Firstname = userDetails.Firstname;
                userToAllocate.Lastname = userDetails.Lastname;
                userToAllocate.Email = userDetails.Email;
            }
            return userToAllocate;
        }
        public int GetActiveMattersForUser(int userId, int? lenderId = null, int? mortMgrId = null)
        {
            DateTime cutoff = DateTime.Now.AddMonths(-3);
            return context.Matters.Where(m => m.FileOwnerUserId == userId && (lenderId == null || m.LenderId == lenderId) && (mortMgrId == null || m.MortMgrId == mortMgrId) &&
                (m.MatterStatusTypeId == (int)MatterStatusTypeEnum.InProgress || m.MatterStatusTypeId == (int)MatterStatusTypeEnum.OnHold) &&
            (m.UpdatedDate >= cutoff || m.MatterEvents.Where(e => e.MatterEventTypeId == (int)MatterEventTypeList.MilestoneComplete).Select(x => x.EventDate).Any(d => d >= cutoff)))
            .Count();
        }
        public void SaveTeamLenderRestrictions(int teamId, List<TeamCustomEntities.TeamLenderView> teamLenders)
        {
            var existingLenderViews = context.TeamLenders.Where(t => t.TeamId == teamId).Select(x => new TeamCustomEntities.TeamLenderView() { TeamId = x.TeamId, LenderId = x.LenderId, TeamLenderId = x.TeamLenderId, MortMgrId = x.MortMgrId }).ToList();

            if (teamLenders.Count == 0 && existingLenderViews.Count != 0)
            {
                context.TeamLenders.RemoveRange(context.TeamLenders.Where(t => t.TeamId == teamId));
            }
            else
            {
                //remove no longer valid lenders
                var teamLendersToRemove = existingLenderViews.Where(x => !teamLenders.Any(t => t.MortMgrId == x.MortMgrId && t.LenderId == x.LenderId));
                var teamLendersToAdd = teamLenders.Where(x => !existingLenderViews.Any(t => t.MortMgrId == x.MortMgrId && t.LenderId == x.LenderId));
                foreach (var toAdd in teamLendersToAdd)
                {
                    context.TeamLenders.Add(new TeamLender { LenderId = toAdd.LenderId, TeamId = teamId, MortMgrId = toAdd.MortMgrId });
                }
                foreach (var toRemove in teamLendersToRemove)
                {
                    var toRemoveDb = context.TeamLenders.Where(l => l.LenderId == toRemove.LenderId && l.MortMgrId == toRemove.MortMgrId && l.TeamId == toRemove.TeamId);
                    context.TeamLenders.RemoveRange(toRemoveDb);
                }

            }
        }

        public void SaveTeamUsers(int teamId, List<TeamCustomEntities.UserTeamListView> teamUsers)
        {
            //save the allocation flag for existing users
            foreach (var user in teamUsers)
            {

                var currentUser = context.UserTeams.Where(u => u.UserId == user.UserId && u.TeamId == user.TeamId).FirstOrDefault();
                if (currentUser != null)
                {
                    currentUser.UserCanBeAllocatedFiles = user.CanBeAllocated;
                    currentUser.UserAllocationWeight = user.AllocationWeight;
                }
            }

            var existingUsers = context.UserTeams.Where(t => t.TeamId == teamId).Select(x => new TeamCustomEntities.UserTeamListView() { UserTeamId = x.UserTeamId, TeamId = x.TeamId, UserId = x.UserId }).ToList();

            if (teamUsers.Count == 0 && existingUsers.Count != 0)
            {
                context.UserTeams.RemoveRange(context.UserTeams.Where(t => t.TeamId == teamId));
            }
            else
            {
                //remove users no longer in the team
                var teamUsersToRemove = existingUsers.Where(x => !teamUsers.Select(t => t.UserId).Contains(x.UserId));
                var teamUsersToAdd = teamUsers.Where(x => !existingUsers.Select(t => t.UserId).Contains(x.UserId));
                foreach (var toAdd in teamUsersToAdd)
                {
                    context.UserTeams.Add(new UserTeam { UserId = toAdd.UserId, TeamId = teamId, UserCanBeAllocatedFiles = toAdd.CanBeAllocated, UserAllocationWeight = toAdd.AllocationWeight });
                }
                foreach (var toRemove in teamUsersToRemove)
                {

                    var toRemoveDb = context.UserTeams.Where(t => t.UserId == toRemove.UserId && t.TeamId == toRemove.TeamId);
                    var toRemoveRoles = context.UserTeamRoles.Where(t => toRemoveDb.Select(x => x.UserTeamId).Contains(t.UserTeamId));
                    context.UserTeamRoles.RemoveRange(toRemoveRoles);
                    context.UserTeams.RemoveRange(toRemoveDb);
                }
            }
        }

        public void SaveTeamUserRoles(int teamId, List<TeamCustomEntities.UserTeamListView> teamUsers)
        {
            foreach (var updatedUser in teamUsers)
            {
                var userTeam = GetUserTeamViewForUser(updatedUser.UserId, teamId);

                //remove roles that no longer apply
                if (userTeam.IsGeneralUser && !updatedUser.IsGeneralUser)
                {
                    var roleToRemove = context.UserTeamRoles.Where(u => u.UserTeamId == userTeam.UserTeamId && u.TeamRoleTypeId == (int)TeamRoleTypeEnum.GeneralUser);
                    context.UserTeamRoles.RemoveRange(roleToRemove);
                }

                if (userTeam.IsQA && !updatedUser.IsQA)
                {
                    var roleToRemove = context.UserTeamRoles.Where(u => u.UserTeamId == userTeam.UserTeamId && u.TeamRoleTypeId == (int)TeamRoleTypeEnum.QA);
                    context.UserTeamRoles.RemoveRange(roleToRemove);
                }

                if (userTeam.IsTeamAdmin && !updatedUser.IsTeamAdmin)
                {
                    var roleToRemove = context.UserTeamRoles.Where(u => u.UserTeamId == userTeam.UserTeamId && u.TeamRoleTypeId == (int)TeamRoleTypeEnum.TeamAdmin);
                    context.UserTeamRoles.RemoveRange(roleToRemove);
                }

                if (userTeam.IsTeamLeader && !updatedUser.IsTeamLeader)
                {
                    var roleToRemove = context.UserTeamRoles.Where(u => u.UserTeamId == userTeam.UserTeamId && u.TeamRoleTypeId == (int)TeamRoleTypeEnum.TeamLeader);
                    context.UserTeamRoles.RemoveRange(roleToRemove);
                }

                //now add new roles
                if (updatedUser.IsGeneralUser && !userTeam.IsGeneralUser)
                {
                    context.UserTeamRoles.Add(new UserTeamRole { UserTeamId = userTeam.UserTeamId, TeamRoleTypeId = (int)TeamRoleTypeEnum.GeneralUser });
                }
                if (updatedUser.IsQA && !userTeam.IsQA)
                {
                    context.UserTeamRoles.Add(new UserTeamRole { UserTeamId = userTeam.UserTeamId, TeamRoleTypeId = (int)TeamRoleTypeEnum.QA });
                }
                if (updatedUser.IsTeamAdmin && !userTeam.IsTeamAdmin)
                {
                    context.UserTeamRoles.Add(new UserTeamRole { UserTeamId = userTeam.UserTeamId, TeamRoleTypeId = (int)TeamRoleTypeEnum.TeamAdmin });
                }
                if (updatedUser.IsTeamLeader && !userTeam.IsTeamLeader)
                {
                    context.UserTeamRoles.Add(new UserTeamRole { UserTeamId = userTeam.UserTeamId, TeamRoleTypeId = (int)TeamRoleTypeEnum.TeamLeader });
                }
            }
        }
        public void SaveTeamRolePrivileges(int teamId, List<TeamCustomEntities.TeamPrivilegeView> teamPrivilegeViews)
        {
            //start by deleting all the team privileges, then we'll rebuild them. 
            var toDelete = context.TeamRolePrivileges.Where(t => t.TeamId == teamId);
            context.TeamRolePrivileges.RemoveRange(toDelete);
            foreach (var privilege in teamPrivilegeViews)
            {
                if (privilege.GeneralPermissions)
                {
                    context.TeamRolePrivileges.Add(
                        new TeamRolePrivilege
                        {
                            TeamId = teamId,
                            TeamRoleTypeId = privilege.TeamRoleId,
                            TeamPrivilegeTypeId = (int)Enums.TeamPrivilegeTypeEnum.GeneralPermissions
                        });
                }
                if (privilege.QAMilestones)
                {
                    context.TeamRolePrivileges.Add(
                        new TeamRolePrivilege
                        {
                            TeamId = teamId,
                            TeamRoleTypeId = privilege.TeamRoleId,
                            TeamPrivilegeTypeId = (int)Enums.TeamPrivilegeTypeEnum.QAMilestones
                        });
                }
                if (privilege.Reports)
                {
                    context.TeamRolePrivileges.Add(
                        new TeamRolePrivilege
                        {
                            TeamId = teamId,
                            TeamRoleTypeId = privilege.TeamRoleId,
                            TeamPrivilegeTypeId = (int)Enums.TeamPrivilegeTypeEnum.Reports
                        });
                }
                if (privilege.TeamAdministration)
                {
                    context.TeamRolePrivileges.Add(
                        new TeamRolePrivilege
                        {
                            TeamId = teamId,
                            TeamRoleTypeId = privilege.TeamRoleId,
                            TeamPrivilegeTypeId = (int)Enums.TeamPrivilegeTypeEnum.TeamAdministration
                        });
                }
                if (privilege.AccountsGeneral)
                {
                    context.TeamRolePrivileges.Add(
                        new TeamRolePrivilege
                        {
                            TeamId = teamId,
                            TeamRoleTypeId = privilege.TeamRoleId,
                            TeamPrivilegeTypeId = (int)Enums.TeamPrivilegeTypeEnum.AccountsGeneral
                        });
                }
                if (privilege.AccountsAdmin)
                {
                    context.TeamRolePrivileges.Add(
                        new TeamRolePrivilege
                        {
                            TeamId = teamId,
                            TeamRoleTypeId = privilege.TeamRoleId,
                            TeamPrivilegeTypeId = (int)Enums.TeamPrivilegeTypeEnum.AccountsAdmin
                        });
                }
                if (privilege.FullPermissions)
                {
                    context.TeamRolePrivileges.Add(
                        new TeamRolePrivilege
                        {
                            TeamId = teamId,
                            TeamRoleTypeId = privilege.TeamRoleId,
                            TeamPrivilegeTypeId = (int)Enums.TeamPrivilegeTypeEnum.FullPermissions
                        });
                }
            }
        }

        public void DeleteTeam(int teamId)
        {

            var tlToRemove = context.TeamLenders.Where(t => t.TeamId == teamId);
            context.TeamLenders.RemoveRange(tlToRemove);

            var utToRemove = context.UserTeams.Where(t => t.TeamId == teamId);
            var utRoleToRemove = context.UserTeamRoles.Where(t => utToRemove.Select(x => x.UserTeamId).Contains(t.UserTeamId));

            context.UserTeamRoles.RemoveRange(utRoleToRemove);
            context.UserTeams.RemoveRange(utToRemove);

            var trpToRemove = context.TeamRolePrivileges.Where(t => t.TeamId == teamId);
            context.TeamRolePrivileges.RemoveRange(trpToRemove);

            var teamToRemove = context.Teams.Where(t => t.TeamId == teamId).FirstOrDefault();
            context.Teams.Remove(teamToRemove);


        }
        public TeamCustomEntities.TeamView GetTeamView(int teamId)
        {
            var team = context.Teams.Select(t => new TeamCustomEntities.TeamView()
            {
                TeamId = t.TeamId,
                TeamName = t.TeamName,
                TeamDescription = t.TeamDescription,
                TeamTypeId = t.TeamTypeId,
                TeamTypeName = t.TeamType.TeamTypeName,
                StateId = t.StateId.HasValue ? t.StateId.Value : -1,
                StateName = t.StateId.HasValue ? t.State.StateName : "All",
                TeamDailyToPipelineRatio = t.TeamDailyToPipelineRatio ?? 0.5M,
                LenderIds = t.TeamLenders.Select(l => l.LenderId).ToList(),
                LenderNames = "All",
                UpdatedByUserId = t.UpdatedByUserId,
                UpdatedDate = t.UpdatedDate,
                UpdatedByUsername = context.Users.Where(u => u.UserId == t.UpdatedByUserId).Select(x => x.Username).FirstOrDefault(),
                MemberCount = t.UserTeams.Distinct().Count(),
                Enabled = t.Enabled
            }).Where(t => t.TeamId == teamId).FirstOrDefault();

            if (team.LenderIds != null && team.LenderIds.Count > 0)
            {
                team.LenderNames = string.Join(",\n", context.Lenders.Where(l => team.LenderIds.Contains(l.LenderId)).Select(x => x.LenderName).ToList());
            }

            team.isDirty = false;

            return team;
        }

        public List<TeamCustomEntities.TeamLenderView> GetTeamLenderViews(int teamId)
        {
            return context.TeamLenders.Where(t => t.TeamId == teamId).Select(t => new TeamCustomEntities.TeamLenderView()
            {
                TeamId = t.TeamId,
                LenderName = t.Lender.LenderName,
                TeamLenderId = t.TeamLenderId,
                LenderId = t.LenderId,
                MortMgrId = t.MortMgrId,
                MortMgrName = t.MortMgrId.HasValue ? t.MortMgr.MortMgrName : null,
                isDirty = false

            }).ToList();
        }

        public int GetAllocationMethod(int teamId, List<TeamCustomEntities.UserTeamListView> team)
        {
            int method = (int)TeamAllocationTypeEnum.Pipeline;
            int totalDaily = team.Sum(x => x.DailyAllocatedToday) + team.Sum(x => x.ManualAllocatedToday);
            int totalPipeline = team.Sum(x => x.PipelineAllocatedToday);
            decimal dailyPercentage = team.FirstOrDefault()?.DailyToPipelineRatio ?? 0.5M;
            decimal pipelinePercentage = 1 - dailyPercentage;

            if ((totalDaily * pipelinePercentage) <= (totalPipeline * dailyPercentage))
            {
                method = (int)TeamAllocationTypeEnum.Daily;
            }

            return method;
        }


        public List<TeamCustomEntities.UserTeamListView> GetUserListForTeam(int teamId, int? lenderId = null, int? mortMgrId = null, bool allocationScoreOnlyFromTeam = false, bool onlyMatchingOffice = false, bool loadDocusign = false)
        {
            List<TeamCustomEntities.UserTeamListView> retList = new List<TeamCustomEntities.UserTeamListView>();

            retList = context.UserTeams.Where(t => t.TeamId == teamId).Where(x => !onlyMatchingOffice || (x.User.StateId == x.Team.StateId))
                .Select(t => new TeamCustomEntities.UserTeamListView
                {
                    UserTeamId = t.UserTeamId,
                    UserId = t.UserId,
                    TeamId = t.TeamId,
                    Username = t.User.Username,
                    Fullname = t.User.Fullname,
                    Email = t.User.Email,
                    CanBeAllocated = t.UserCanBeAllocatedFiles,
                    ActiveFileownerMatters = 0,
                    AllocationWeight = t.UserAllocationWeight,
                    IsGeneralUser = t.UserTeamRoles.Any(x => x.TeamRoleTypeId == (int)Enums.TeamRoleTypeEnum.GeneralUser),
                    IsQA = t.UserTeamRoles.Any(x => x.TeamRoleTypeId == (int)Enums.TeamRoleTypeEnum.QA),
                    IsTeamAdmin = t.UserTeamRoles.Any(x => x.TeamRoleTypeId == (int)Enums.TeamRoleTypeEnum.TeamAdmin),
                    IsTeamLeader = t.UserTeamRoles.Any(x => x.TeamRoleTypeId == (int)Enums.TeamRoleTypeEnum.TeamLeader),
                    DailyToPipelineRatio = t.Team.TeamDailyToPipelineRatio ?? 0.5M,


                }).ToList();
            

            var emailList = retList.Select(x => x.Email).ToList();
            //get stats on how many files people have been allocated today
            int? teamid = teamId;
            foreach (var user in retList)
            {
                user.ActiveFileownerMatters = GetActiveMattersForUser(user.UserId, lenderId, mortMgrId);
                user.DailyAllocatedToday = GetDailyAllocatedMatters(user.UserId, (int)TeamAllocationTypeEnum.Daily, lenderId, mortMgrId, allocationScoreOnlyFromTeam ? teamid : null);
                user.PipelineAllocatedToday = GetDailyAllocatedMatters(user.UserId, (int)TeamAllocationTypeEnum.Pipeline, lenderId, mortMgrId, allocationScoreOnlyFromTeam ? teamid : null);
                user.ManualAllocatedToday = GetDailyAllocatedMatters(user.UserId, (int)TeamAllocationTypeEnum.Manual, lenderId, mortMgrId, allocationScoreOnlyFromTeam ? teamid : null);
                user.ReallocatedToday = GetDailyAllocatedMatters(user.UserId, (int)TeamAllocationTypeEnum.Reallocated, lenderId, mortMgrId, allocationScoreOnlyFromTeam ? teamid : null);
                user.TotalAllocatedToday = user.DailyAllocatedToday + user.ManualAllocatedToday + user.PipelineAllocatedToday;
                
            }

            int allocationMethodToUse = GetAllocationMethod(teamId, retList);

            foreach (var user in retList)
            {
                if (user.AllocationWeight != 0)
                {
                    if (allocationMethodToUse == (int)TeamAllocationTypeEnum.Pipeline || allocationMethodToUse == (int)TeamAllocationTypeEnum.Manual)
                    {
                        user.AllocationScore = user.ActiveFileownerMatters * (1 / user.AllocationWeight); //lowest score gets the file
                        user.ThisAllocationType = allocationMethodToUse;
                    }
                    else
                    {
                        user.AllocationScore = ((user.DailyAllocatedToday + user.ManualAllocatedToday) * user.DailyToPipelineRatio) * (1 / user.AllocationWeight);
                        user.ThisAllocationType = allocationMethodToUse;
                    }
                }
                else
                {
                    user.AllocationScore = 0;
                }
                user.isDirty = false;
            }

            return retList;
        }        

        public int GetDailyAllocatedMatters(int userId, int? allocationTypeId, int? lenderId = null, int? mortMgrId = null, int? teamId = null)
        {
            DateTime startOfDay = DateTime.Now.Date;
            IQueryable<UserAllocationLog> qry;

            if (teamId.HasValue)
            {
                qry = context.UserAllocationLogs.Where(u => u.AllocatedToUserId == userId && u.AllocatedDate >= startOfDay && u.TeamId == teamId.Value);
            }
            else
            {
                qry = context.UserAllocationLogs.Where(u => u.AllocatedToUserId == userId && u.AllocatedDate >= startOfDay && (lenderId == null || u.Matter.LenderId == lenderId.Value) && (mortMgrId == null || u.Matter.MortMgrId == mortMgrId.Value));
            }


            if (allocationTypeId.HasValue) qry = qry.Where(u => u.TeamAllocationTypeId == allocationTypeId);

            return qry.Count();
        }
        public TeamCustomEntities.UserTeamListView GetUserTeamViewForUser(int userId, int teamId)
        {
            return context.UserTeams.Where(t => t.TeamId == teamId && t.UserId == userId)
                .Select(t => new TeamCustomEntities.UserTeamListView
                {
                    UserTeamId = t.UserTeamId,
                    UserId = t.UserId,
                    TeamId = t.TeamId,
                    Username = t.User.Username,
                    Fullname = t.User.Fullname,
                    CanBeAllocated = t.UserCanBeAllocatedFiles,
                    AllocationWeight = t.UserAllocationWeight,
                    IsGeneralUser = t.UserTeamRoles.Any(x => x.TeamRoleTypeId == (int)Enums.TeamRoleTypeEnum.GeneralUser),
                    IsQA = t.UserTeamRoles.Any(x => x.TeamRoleTypeId == (int)Enums.TeamRoleTypeEnum.QA),
                    IsTeamAdmin = t.UserTeamRoles.Any(x => x.TeamRoleTypeId == (int)Enums.TeamRoleTypeEnum.TeamAdmin),
                    IsTeamLeader = t.UserTeamRoles.Any(x => x.TeamRoleTypeId == (int)Enums.TeamRoleTypeEnum.TeamLeader)
                }).FirstOrDefault();

        }



        public List<TeamCustomEntities.UserTeamListView> GetUserTeamViewsForUser(int userId)
        {
            return context.UserTeams.Where(t => t.UserId == userId)
                .Select(t => new TeamCustomEntities.UserTeamListView
                {
                    UserTeamId = t.UserTeamId,
                    UserId = t.UserId,
                    TeamId = t.TeamId,
                    Username = t.User.Username,
                    Fullname = t.User.Fullname,
                    CanBeAllocated = t.UserCanBeAllocatedFiles,
                    AllocationWeight = t.UserAllocationWeight,
                    ActiveFileownerMatters = GetActiveMattersForUser(userId, null, null),
                    IsGeneralUser = t.UserTeamRoles.Any(x => x.TeamRoleTypeId == (int)Enums.TeamRoleTypeEnum.GeneralUser),
                    IsQA = t.UserTeamRoles.Any(x => x.TeamRoleTypeId == (int)Enums.TeamRoleTypeEnum.QA),
                    IsTeamAdmin = t.UserTeamRoles.Any(x => x.TeamRoleTypeId == (int)Enums.TeamRoleTypeEnum.TeamAdmin),
                    IsTeamLeader = t.UserTeamRoles.Any(x => x.TeamRoleTypeId == (int)Enums.TeamRoleTypeEnum.TeamLeader)
                }).ToList();
        }




        public List<TeamCustomEntities.TeamPrivilegeView> GetTeamPrivilegesForTeam(int teamId)
        {
            return context.TeamRoleTypes //for each of the different types of role, get the different permissions assigned for this team.
                .Select(x => new TeamCustomEntities.TeamPrivilegeView
                {
                    TeamId = teamId,
                    TeamRoleId = x.TeamRoleTypeId,
                    TeamRoleName = x.TeamRoleName,
                    QAMilestones = context.TeamRolePrivileges.Any(z => z.TeamId == teamId && z.TeamRoleTypeId == x.TeamRoleTypeId && z.TeamPrivilegeTypeId == (int)Enums.TeamPrivilegeTypeEnum.QAMilestones),
                    FullPermissions = context.TeamRolePrivileges.Any(z => z.TeamId == teamId && z.TeamRoleTypeId == x.TeamRoleTypeId && z.TeamPrivilegeTypeId == (int)Enums.TeamPrivilegeTypeEnum.FullPermissions),
                    GeneralPermissions = context.TeamRolePrivileges.Any(z => z.TeamId == teamId && z.TeamRoleTypeId == x.TeamRoleTypeId && z.TeamPrivilegeTypeId == (int)Enums.TeamPrivilegeTypeEnum.GeneralPermissions),
                    Reports = context.TeamRolePrivileges.Any(z => z.TeamId == teamId && z.TeamRoleTypeId == x.TeamRoleTypeId && z.TeamPrivilegeTypeId == (int)Enums.TeamPrivilegeTypeEnum.Reports),
                    TeamAdministration = context.TeamRolePrivileges.Any(z => z.TeamId == teamId && z.TeamRoleTypeId == x.TeamRoleTypeId && z.TeamPrivilegeTypeId == (int)Enums.TeamPrivilegeTypeEnum.TeamAdministration),
                    AccountsGeneral = context.TeamRolePrivileges.Any(z => z.TeamId == teamId && z.TeamRoleTypeId == x.TeamRoleTypeId && z.TeamPrivilegeTypeId == (int)Enums.TeamPrivilegeTypeEnum.AccountsGeneral),
                    AccountsAdmin = context.TeamRolePrivileges.Any(z => z.TeamId == teamId && z.TeamRoleTypeId == x.TeamRoleTypeId && z.TeamPrivilegeTypeId == (int)Enums.TeamPrivilegeTypeEnum.AccountsAdmin),
                    isDirty = false
                }
                ).ToList();
        }


        public List<TeamCustomEntities.TeamStateView> GetTeamStates(int teamId)
        {
            return context.States
                .Select(x => new TeamCustomEntities.TeamStateView
                {
                    StateId = x.StateId,
                    StateName = x.StateName,
                    TeamId = teamId,
                    CanAllocate = context.TeamAllocationStates.Any(t => t.TeamId == teamId && t.StateId == x.StateId),
                    isDirty = false
                }).ToList();
        }

        public void SaveTeamStates(int teamId, List<TeamCustomEntities.TeamStateView> stateList)
        {
            var toAdd = stateList.Where(x => x.CanAllocate);
            var toRemove = stateList.Where(x => !x.CanAllocate);

            foreach (var state in toAdd)
            {
                if (!context.TeamAllocationStates.Any(s => s.StateId == state.StateId && s.TeamId == teamId))
                {
                    context.TeamAllocationStates.Add(new TeamAllocationState() { StateId = state.StateId, TeamId = teamId });
                }
            }
            foreach (var state in toRemove)
            {
                var removeRange = context.TeamAllocationStates.Where(s => s.StateId == state.StateId && s.TeamId == teamId);
                if (removeRange.Count() != 0)
                {
                    context.TeamAllocationStates.RemoveRange(removeRange);
                }
            }

        }

        public bool IsUserInAnyTeamOfType(int userId, int teamTypeId)
        {
            return context.Teams.Any(t => t.TeamTypeId == teamTypeId && t.UserTeams.Any(u => u.UserId == userId));
        }
        public bool IsUserQA(int userId, int lenderId)
        {
            bool userIsQA = false;

            var teamIds = context.Teams.Where(t => t.Enabled && t.TeamTypeId == (int)TeamTypeEnum.Settlements && (t.TeamLenders.Count() == 0 || t.TeamLenders.Any(l => l.LenderId == lenderId))).Select(x => x.TeamId).ToList();
            if (teamIds != null && teamIds.Count() > 0)
            {
                var allTeamUserIds = context.Users.Where(t => t.UserTeams.Any(x => teamIds.Contains(x.TeamId))).Select(u => u.UserId).ToList();
                if (allTeamUserIds != null && allTeamUserIds.Count() > 0)
                {
                    var userTeamsWithQA = context.UserTeams.Where(ut => allTeamUserIds.Contains(ut.UserId) && teamIds.Contains(ut.TeamId) && ut.UserTeamRoles.Any(utr => utr.TeamRoleTypeId == (int)TeamRoleTypeEnum.QA))
                                            .Select(z => new { z.UserId, z.User.Email }).ToList();
                    if (userTeamsWithQA != null && userTeamsWithQA.Count() > 0)
                    {
                        if (userTeamsWithQA.Select(x => x.UserId).ToList().Contains(userId))
                        {
                            userIsQA = true;
                        }
                    }
                }
            }

            return userIsQA;
        }
        public bool IsUserDocPrepQA(int userId, int lenderId)
        {
            bool userIsQA = false;

            var teamIds = context.Teams.Where(t => t.Enabled && t.TeamTypeId == (int)TeamTypeEnum.DocPrep && (t.TeamLenders.Count() == 0 || t.TeamLenders.Any(l => l.LenderId == lenderId))).Select(x => x.TeamId).ToList();
            if (teamIds != null && teamIds.Count() > 0)
            {
                var allTeamUserIds = context.Users.Where(t => t.UserTeams.Any(x => teamIds.Contains(x.TeamId))).Select(u => u.UserId).ToList();
                if (allTeamUserIds != null && allTeamUserIds.Count() > 0)
                {
                    var userTeamsWithQA = context.UserTeams.Where(ut => allTeamUserIds.Contains(ut.UserId) && teamIds.Contains(ut.TeamId) && ut.UserTeamRoles.Any(utr => utr.TeamRoleTypeId == (int)TeamRoleTypeEnum.QA))
                                            .Select(z => new { z.UserId, z.User.Email }).ToList();
                    if (userTeamsWithQA != null && userTeamsWithQA.Count() > 0)
                    {
                        if (userTeamsWithQA.Select(x => x.UserId).ToList().Contains(userId))
                        {
                            userIsQA = true;
                        }
                    }
                }
            }

            return userIsQA;
        }
        public bool IsDischargeQA(int userId)
        {
            bool userIsQA = false;

            var teams = context.Teams.Where(t => t.Enabled && t.TeamTypeId == (int)TeamTypeEnum.Discharges).ToList();


            var teamRoles = teams.SelectMany(u => u.UserTeams.Where(r => r.UserTeamRoles.Any(t => t.TeamRoleTypeId == (int)TeamRoleTypeEnum.QA))).ToList();

            userIsQA = teamRoles.Select(t => t.UserId).Any(u => u == userId);

            return userIsQA;
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
