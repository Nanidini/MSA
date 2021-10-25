using Slick_Domain.Common;
using Slick_Domain.Entities;
using Slick_Domain.Enums;
using Slick_Domain.Models;
using System.Collections.Generic;
using System.Linq;
using System;
using MCE = Slick_Domain.Entities.MatterCustomEntities;
using System.Data.Entity;

namespace Slick_Domain.Services
{
    public class TasksRepository : SlickRepository
    {
        public TasksRepository(SlickContext context) : base(context)
        {



        }

        public IEnumerable<MCE.MatterTasksListView> GetActionsViewForUser(int? userId, TaskListTypeEnum taskListType, int? filterToIndex = null)
        {

            //context.Configuration.AutoDetectChangesEnabled = false;
            IQueryable<MatterWFComponent> mwFC = context.MatterWFComponents
                                     .Where(m => 
                                     m.Matter.MatterStatusTypeId != (int) MatterStatusTypeEnum.Closed 
                                     && m.Matter.MatterStatusTypeId != (int) MatterStatusTypeEnum.NotProceeding
                                     && m.WFComponentStatusType.IsActiveStatus && 
                                        (m.DisplayStatusTypeId != (int)DisplayStatusTypeEnum.Inactive && 
                                        m.DisplayStatusTypeId != (int)DisplayStatusTypeEnum.InactiveNeverShown))
                                        .AsNoTracking();

            if (taskListType == TaskListTypeEnum.MyTasks || taskListType == TaskListTypeEnum.OtherUserTasks)
            {
                var items = GetMatterTasksView(mwFC.Where(m => (m.TaskAllocUserId == userId)), taskListType);
                return items;
            }
            else if (taskListType == TaskListTypeEnum.SharedTasks || taskListType == TaskListTypeEnum.OtherUserSharedTasks)
            {
                var items = GetSharedTasksForUser(mwFC, userId.Value, filterToIndex.Value);
                return items;
            }
            else if (taskListType == TaskListTypeEnum.TaskMgrTasks)
            {
                if (filterToIndex.HasValue)
                {
                    var items = GetTasksView(mwFC, userId, filterToIndex);
                    return items;
                }
                else
                {
                    var items = GetTaskManagerTasks(mwFC, userId);
                    return items; 
                }
            }
            else if (taskListType == TaskListTypeEnum.AllTasks)
            {
                var items = GetTasksView(mwFC);
                return items;
            }
            return null;
        }


        private IEnumerable<MCE.MatterTasksListView> GetMatterTasksView(IQueryable<MatterWFComponent> mwfc, TaskListTypeEnum taskListType)
        {

            var tasks = mwfc.
                Select(x => new
                {
                    x.MatterId,
                    x.Matter.MatterDescription,
                    x.Matter.LenderId,
                    x.Matter.MatterGroupTypeId,
                    x.Matter.MortMgrId,
                    x.Matter.StateId,
                    x.Matter.InstructionStateId,
                    x.Matter.Lender.LenderName,
                    LenderRefNo = (x.Matter.MatterGroupTypeId == (int)MatterGroupTypeEnum.NewLoan && x.Matter.LenderId == 139 && x.Matter.MortMgrId != 49) ? x.Matter.SecondaryRefNo : x.Matter.LenderRefNo,
                    x.MatterWFComponentId,
                    x.WFComponent.WFModuleId,
                    CanBulkUpdate = x.WFComponent.WFModule.CanBulkUpdate && !x.WFComponent.BulkUpdateExcludeComponent,
                    x.Matter.MatterStatusTypeId,
                    x.Matter.MatterStatusType.MatterStatusTypeName,
                    x.WFComponentId,
                    x.WFComponent.WFComponentName,
                    ShowLenderBool = ((x.Matter.MatterGroupTypeId == (int)MatterGroupTypeEnum.NewLoan) && (x.Matter.LenderId != 166)),
                    ShowBrokerBool = (x.Matter.MatterGroupTypeId == (int)MatterGroupTypeEnum.NewLoan) && (x.Matter.BrokerId != null),
                    x.AssignedTaskColour,
                    x.AssignedTaskNote,
                    x.DueDate,
                    x.Matter.UpdatedDate,
                    IsMatterPexa = x.Matter.MatterSecurities.Any(y => y.SettlementTypeId == (int)SettlementTypeEnum.PEXA),
                    FileManager = x.Matter.User.Username,
                    UpdatedByUsername = x.User.Username,
                    PexaWorkspaceNos = x.Matter.MatterSecurities.SelectMany(y => y.MatterSecurityMatterPexaWorkspaces.Select(z => z.MatterPexaWorkspace.PexaWorkspace.PexaWorkspaceNo)),
                    PexaWorkspaceStatuses = x.Matter.MatterSecurities.SelectMany(m => m.MatterSecurityMatterPexaWorkspaces.Where(mp => mp.MatterPexaWorkspace.PexaWorkspace.PexaWorkspaceStatusTypeId.HasValue)
                                            .Select(p => p.MatterPexaWorkspace.PexaWorkspace.PexaWorkspaceStatusType.PexaWorkspaceStatusTypeName)),
                    OutstandingItems = x.MatterWFOutstandingReqs.FirstOrDefault().MatterWFOutstandingReqItems.Where(o => !o.Resolved).Select(o => o.OutstandingItemName),
                    LoanType = x.Matter.LoanTypeId.HasValue ? x.Matter.LoanType.LoanTypeName : null

                })
                .AsNoTracking()
                .ToList()
                .Select(m =>
                new MCE.MatterTasksListView
                (
                    m.MatterId,
                    taskListType,
                    m.MatterDescription,
                    m.LenderId,
                    m.MatterGroupTypeId,
                    m.MortMgrId,
                    m.StateId,
                    m.InstructionStateId,
                    m.LenderName,
                    m.LenderRefNo,
                    m.MatterWFComponentId,
                    m.WFModuleId,
                    m.CanBulkUpdate,
                    m.MatterGroupTypeId,
                    m.MatterStatusTypeId,
                    m.MatterStatusTypeName,
                    m.WFComponentId,
                    m.WFComponentName,
                    m.WFComponentId == (int)WFComponentEnum.FollowupOutstandingRequirements,
                    m.ShowLenderBool,
                    m.ShowBrokerBool,
                    m.AssignedTaskColour ?? "#00FFFFFF",
                    m.AssignedTaskNote,
                    !string.IsNullOrEmpty(m.AssignedTaskNote),
                    m.DueDate,
                    m.IsMatterPexa,
                    m.FileManager,
                    m.UpdatedByUsername,
                    m.PexaWorkspaceNos,
                    m.PexaWorkspaceStatuses,
                    m.OutstandingItems,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    m.UpdatedDate,
                    null,
                    null,
                    null,
                    null,
                    m.LoanType
                 ))
            .ToList();

            return tasks.OrderBy(x => x.OutstandingRequirementsBool).ToList();
        }

        public List<MCE.MatterTasksListView> GetMyTasksSproc(int? userId, TaskListTypeEnum taskListType)
        {
            List<MCE.MatterTasksListView> tasks = new List<MCE.MatterTasksListView>();
            if (!userId.HasValue) userId = GlobalVars.CurrentUser.UserId;

            tasks = context
                    .sp_Slick_GetUserTasks(userId)
                    .ToList().Distinct()
                     .Select(m =>
                        new MCE.MatterTasksListView
                        (
                            m.MatterId,
                            taskListType,
                            m.MatterDescription,
                            m.LenderId,
                            m.MatterGroupTypeId,
                            m.MortMgrId,
                            m.StateId,
                            m.InstructionStateId,
                            m.LenderName,
                            m.MortMgrName,
                            m.LenderRefNo,
                            m.MatterWFComponentId,
                            m.WFModuleId,
                            m.CanBulkUpdate == 1,
                            m.MatterGroupTypeId,
                            m.MatterStatusTypeId,
                            m.MatterStatusTypeName,
                            m.WFComponentId,
                            m.WFComponentName,
                            m.WFComponentId == (int)WFComponentEnum.FollowupOutstandingRequirements,
                            m.ShowLenderBool == 1,
                            m.ShowBrokerBool == 1,
                            m.AssignedTaskColour ?? "#00FFFFFF",
                            m.AssignedTaskNote,
                            !string.IsNullOrEmpty(m.AssignedTaskNote),
                            m.DueDate,
                            m.IsMatterPexa == 1,
                            m.FileOwnerUsername,
                            m.UpdatedUsername,
                            m.PexaWorkspaceNos,
                            m.PexaWorkspaceStatuses,
                            m.OutstandingItems,
                            new List<string> { m.MatterTypesNames },
                            m.MatterGroupTypeName,
                            null,
                            null,
                            null,
                            null,
                            m.UpdatedDate,
                            null,
                            null,
                            null,
                            m.SettlementDate.HasValue? (m.SettlementTime.HasValue ? m.SettlementDate.Value.Add(m.SettlementTime.Value) : m.SettlementDate) : null,
                            m.LoanTypeName,
                            m.SecuritiesAwaitingRegistration,
                            m.AllSecurities,
                            m.PexaNominatedSettlementDate,
                            m.Paperless, 
                            m.WorkingFromHome,
                            m.RecheckRequired ?? false,
                            m.IsUrgent,
                            m.IsDigiDocs,
                            m.DocumentRequirements,
                            m.FileOpenedDate,
                            m.IsSelfActing ?? false,
                            m.RegisteredDocuments,
                            m.TitleReferences
                            
                         )).ToList().GroupBy(t=>t.MatterWFComponentId).Select(x=>x.First()).ToList();


            return tasks.OrderBy(o=>o.OutstandingRequirementsBool).ThenBy(x => x.DueDate).ToList();
        }

        public List<MCE.MatterTasksListView> GetSharedTasksSProc(int? userId, TaskListTypeEnum taskListType)
        {
            if (!userId.HasValue) userId = GlobalVars.CurrentUser.UserId;

            var availableSharedTasks = context.SharedTaskAllocUsers.Where(x => x.UserId == userId.Value);
            if (availableSharedTasks == null || !availableSharedTasks.Any()) return null;

            List<MCE.MatterTasksListView> tasks = new List<MCE.MatterTasksListView>();

            foreach(var task in availableSharedTasks)
            {
                tasks = tasks.Concat(context
                    .sp_Slick_GetSharedTasks(task.WFComponentId, task.MatterTypeId, task.StateId, task.LenderId, task.MortMgrId, task.SettlementTypeId)
                    .ToList()
                     .Select(m =>
                        new MCE.MatterTasksListView
                        (
                            m.MatterId,
                            taskListType,
                            m.MatterDescription,
                            m.LenderId,
                            m.MatterGroupTypeId,
                            m.MortMgrId,
                            m.StateId,
                            m.InstructionStateId,
                            m.LenderName,
                            m.MortMgrName,
                            m.LenderRefNo,
                            m.MatterWFComponentId,
                            m.WFModuleId,
                            m.CanBulkUpdate == 1,
                            m.MatterGroupTypeId,
                            m.MatterStatusTypeId,
                            m.MatterStatusTypeName,
                            m.WFComponentId,
                            m.WFComponentName,
                            m.WFComponentId == (int)WFComponentEnum.FollowupOutstandingRequirements,
                            m.ShowLenderBool == 1,
                            m.ShowBrokerBool == 1,
                            m.AssignedTaskColour ?? "#00FFFFFF",
                            m.AssignedTaskNote,
                            !string.IsNullOrEmpty(m.AssignedTaskNote),
                            m.DueDate,
                            m.IsMatterPexa == 1,
                            m.FileOwnerUsername,
                            m.UpdatedUsername,
                            m.PexaWorkspaceNos,
                            m.PexaWorkspaceStatuses,
                            m.OutstandingItems,
                            new List<string> { m.MatterTypesNames },
                            m.MatterGroupTypeName,
                            null,
                            null,
                            null,
                            null,
                            m.UpdatedDate,
                            null,
                            null,
                            null,
                            m.SettlementDate.HasValue ? (m.SettlementTime.HasValue ? m.SettlementDate.Value.Add(m.SettlementTime.Value) : m.SettlementDate) : null,
                            m.LoanTypeName,
                            m.SecuritiesAwaitingRegistration,
                            m.AllSecurities,
                            m.PexaNominatedSettlementDate,
                            m.Paperless,
                            m.WorkingFromHome,
                            m.RecheckRequired ?? false,
                            m.IsUrgent,
                            m.IsDigiDocs,
                            m.DocumentRequirements,
                            m.FileOpenedDate,
                            m.IsSelfActing ?? false,
                            m.RegisteredDocuments,
                            m.TitleReferences
                         ))).ToList();
            }

            return tasks.Distinct().ToList();
        }
        public List<MCE.MatterTasksListView> GetSharedTasksSProcSingleQuery(int? userId, TaskListTypeEnum taskListType)
        {
            if (!userId.HasValue) userId = GlobalVars.CurrentUser.UserId;

            return context
                     .sp_Slick_GetSharedTasks_SingleQuery(userId)
                     .ToList()
                      .Select(m =>
                         new MCE.MatterTasksListView
                         (
                             m.MatterId,
                             taskListType,
                             m.MatterDescription,
                             m.LenderId,
                             m.MatterGroupTypeId,
                             m.MortMgrId,
                             m.StateId,
                             m.InstructionStateId,
                             m.LenderName,
                             m.MortMgrName,
                             m.LenderRefNo,
                             m.MatterWFComponentId,
                             m.WFModuleId,
                             m.CanBulkUpdate == 1,
                             m.MatterGroupTypeId,
                             m.MatterStatusTypeId,
                             m.MatterStatusTypeName,
                             m.WFComponentId,
                             m.WFComponentName,
                             m.WFComponentId == (int)WFComponentEnum.FollowupOutstandingRequirements,
                             m.ShowLenderBool == 1,
                             m.ShowBrokerBool == 1,
                             m.AssignedTaskColour ?? "#00FFFFFF",
                             m.AssignedTaskNote,
                             !string.IsNullOrEmpty(m.AssignedTaskNote),
                             m.DueDate,
                             m.IsMatterPexa == 1,
                             m.FileOwnerUsername,
                             m.UpdatedUsername,
                             m.PexaWorkspaceNos,
                             m.PexaWorkspaceStatuses,
                             m.OutstandingItems,
                             new List<string> { m.MatterTypesNames },
                             m.MatterGroupTypeName,
                             null,
                             null,
                             null,
                             null,
                             m.UpdatedDate,
                             null,
                             null,
                             null,
                             m.SettlementDate.HasValue ? (m.SettlementTime.HasValue ? m.SettlementDate.Value.Add(m.SettlementTime.Value) : m.SettlementDate) : null,
                             m.LoanTypeName,
                             m.SecuritiesAwaitingRegistration,
                             m.AllSecurities,
                             m.PexaNominatedSettlementDate,
                             m.Paperless,
                             m.WorkingFromHome,
                             m.RecheckRequired ?? false,
                             m.IsUrgent,
                             m.IsDigiDocs,
                             m.DocumentRequirements,
                             m.FileOpenedDate,
                             m.IsSelfActing ?? false,
                             m.RegisteredDocuments,
                             m.TitleReferences
                          )).ToList().Distinct().ToList();

        }

        private IEnumerable<MCE.MatterTasksListView> GetSharedTasksForUser(IQueryable<MatterWFComponent> mwFC, int userId, int indexToFilter)
        {
            mwFC = mwFC.Where(x => (x.TaskAllocTypeId == (int) TaskAllocationTypeEnum.Shared || x.TaskAllocTypeId == (int)TaskAllocationTypeEnum.Manila) && x.TaskAllocUserId == null);
            var availableSharedTasks = context.SharedTaskAllocUsers.Where(x => x.UserId == userId);
            if (availableSharedTasks == null || !availableSharedTasks.Any()) return null;

            var shared = 
                availableSharedTasks.Select(x=>new {
                    x.WFComponentId,
                    x.MatterTypeId,
                    x.StateId,
                    x.LenderId,
                    x.MortMgrId,
                    x.SettlementTypeId
                })
                .ToList()[indexToFilter];

            var filteredMWFC = mwFC.AsNoTracking().Where(t =>
                (t.WFComponentId == shared.WFComponentId) &&
                (shared.MatterTypeId == null || t.Matter.MatterGroupTypeId == shared.MatterTypeId || t.Matter.MatterMatterTypes.Any(z => z.MatterTypeId == shared.MatterTypeId)) &&
                (shared.StateId == null || t.Matter.StateId == shared.StateId || t.Matter.InstructionStateId == shared.StateId) &&
                (shared.LenderId == null || t.Matter.LenderId == shared.LenderId) &&
                (shared.MortMgrId == null || t.Matter.MortMgrId == shared.MortMgrId) &&
                (shared.SettlementTypeId == null || (t.Matter.MatterSecurities.Any(st => st.SettlementTypeId == (int)SettlementTypeEnum.PEXA) && shared.SettlementTypeId == (int)SettlementTypeEnum.PEXA) || (!t.Matter.MatterSecurities.Any(st => st.SettlementTypeId == (int)SettlementTypeEnum.PEXA) && shared.SettlementTypeId == (int)SettlementTypeEnum.Paper)));
                    

     
            
               return GetMatterTasksView(filteredMWFC, TaskListTypeEnum.SharedTasks);
            

        }

        private IEnumerable<MCE.MatterTasksListView> GetTaskManagerTasks(IQueryable<MatterWFComponent> mwfc, int? userId)
        {
            var filters = context.TaskAllocUserFilters.Where(x => x.UserId == userId)
                .Select(x => new { x.LenderId, x.MortMgrId, x.StateId, x.MatterTypeId, x.MatterMatterTypeId, x.SettlementTypeId, x.WFComponentId });

            var mwfcFiltered = mwfc.Where(t => 
            filters.Any
            (f =>
                  (f.LenderId == null || f.LenderId.Value == t.Matter.LenderId) &&
                  (f.MortMgrId == null || f.MortMgrId.Value == t.Matter.MortMgrId) &&
                  (f.WFComponentId == null || f.WFComponentId.Value == t.WFComponentId) &&
                  (f.MatterTypeId == null || f.MatterTypeId.Value == t.Matter.MatterGroupTypeId) &&
                  (f.MatterMatterTypeId == null || !t.Matter.MatterMatterTypes.Any() || t.Matter.MatterMatterTypes.Any(m=>m.MatterTypeId == f.MatterMatterTypeId.Value)) &&
                  (f.SettlementTypeId == null || t.Matter.MatterSecurities.Any(s=>s.SettlementTypeId == f.SettlementTypeId.Value)) && 
                  (f.StateId == null || f.StateId.Value == t.Matter.StateId) 
            ));



            return GetTasksView(mwfcFiltered);
        }


        public IEnumerable<MCE.MatterTasksListView> GetTaskManagerTasksSProcSingleQuery(int userId)
        {
  
            List<MCE.MatterTasksListView> tasks = new List<MCE.MatterTasksListView>();
  
            var taskResult =
                context
                .sp_Slick_GetTaskAllocationManagerTasks_SingleQuery
                (userId);

            tasks = taskResult
            .Select(s => new MCE.MatterTasksListView(
                s.MatterId,
                TaskListTypeEnum.TaskMgrTasks,
                s.MatterDescription,
                s.LenderId,
                s.MatterGroupTypeId,
                s.MortMgrId,
                s.MortMgrName,
                s.StateId,
                s.InstructionStateId,
                s.LenderName,
                s.LenderRefNo,
                s.MatterWFComponentId,
                s.WFModuleId,
                s.CanBulkUpdate,
                s.MatterGroupTypeId,
                s.MatterStatusTypeId,
                s.MatterStatusTypeName,
                s.WFComponentId,
                s.WFComponentName,
                !String.IsNullOrEmpty(s.OutstandingItems),
                s.ShowBrokerBool == 1 ? true : false,
                s.ShowLenderBool == 1 ? true : false,
                s.AssignedTaskColour,
                s.AssignedTaskNote,
                !String.IsNullOrEmpty(s.AssignedTaskNote),
                s.DueDate,
                s.IsMatterPexa == 1 ? true : false,
                s.FileManager,
                s.UpdatedByUserName,
                s.PexaWorkspaceNos,
                s.PexaWorkspaceStatuses,
                s.OutstandingItems,
                s.MatterTypesNames,
                s.MatterGroupTypeName,
                s.TaskAllocUsername,
                null,
                s.TaskAllocUserId,
                s.TaskAllocTypeId,
                s.UpdatedDate,
                s.TaskAllocUserId.HasValue ? (int)TaskAllocationEnum.AllocatedToUser : (s.TaskAllocTypeId == (int)TaskAllocationTypeEnum.Shared ? (int)TaskAllocationEnum.SharedList : s.TaskAllocTypeId == (int)TaskAllocationTypeEnum.Manila ? (int)TaskAllocationEnum.Manila : (int)TaskAllocationEnum.Unallocated),
                s.DfltTaskAllocType,
                s.DfltTaskAllocComp,
                s.DischargeTypeName,
                s.SettlementDate.HasValue ? s.SettlementDate.Value.Add(s.SettlementTime.Value) : (DateTime?)null,
                s.LoanTypeName,
                s.SecurityStates,
                s.SecuritiesAwaitingRegistration,
                s.AllSecurities,
                s.PartiallyRegistered == 1 ? true : false,
                s.ReconciledBalance ?? 0M,
                s.AccountsActionStatusTypeId,
                s.AccountsActionStatusTypeName,
                s.AccountsActioningUserId,
                s.AccountsActionUserName,
                s.AccountsStageId,
                s.AccountsStageName,
                s.PexaNominatedSettlementDate,
                s.Paperless,
                s.WorkingFromHome,
                s.RecheckRequired ?? false,
                s.IsUrgent,
                s.IsDigiDocs,
                s.DocDeliveryMethods,
                s.DocumentRequirements,
                s.FileOpenedDate,
                s.LastViewedDate,
                s.LastViewedBy,
                s.PSTIssues,
                s.IsSelfActing ?? false,
                s.RegisteredDocuments,
                s.TitleReferences
            )).ToList();


            return tasks.OrderBy(x => x.OutstandingRequirementsBool).ToList();
        }
        public MCE.MatterTasksListView GetIndvidualTaskManagerItem(int matterWFComponentId)
        {
          
            var taskResult =
                context
                .sp_Slick_GetTaskAllocationManagerTasks(null,null,null,null, null, null, null, null, matterWFComponentId);

            return taskResult
            .Select(s => new MCE.MatterTasksListView
            (
                s.MatterId,
                TaskListTypeEnum.TaskMgrTasks,
                s.MatterDescription,
                s.LenderId,
                s.MatterGroupTypeId,
                s.MortMgrId,
                s.MortMgrName,
                s.StateId,
                s.InstructionStateId,
                s.LenderName,
                s.LenderRefNo,
                s.MatterWFComponentId,
                s.WFModuleId,
                s.CanBulkUpdate,
                s.MatterGroupTypeId,
                s.MatterStatusTypeId,
                s.MatterStatusTypeName,
                s.WFComponentId,
                s.WFComponentName,
                !String.IsNullOrEmpty(s.OutstandingItems),
                s.ShowBrokerBool == 1 ? true : false,
                s.ShowLenderBool == 1 ? true : false,
                s.AssignedTaskColour,
                s.AssignedTaskNote,
                !String.IsNullOrEmpty(s.AssignedTaskNote),
                s.DueDate,
                s.IsMatterPexa == 1 ? true : false,
                s.FileManager,
                s.UpdatedByUserName,
                s.PexaWorkspaceNos,
                s.PexaWorkspaceStatuses,
                s.OutstandingItems,
                s.MatterTypesNames,
                s.MatterGroupTypeName,
                s.TaskAllocUsername,
                null,
                s.TaskAllocUserId,
                s.TaskAllocTypeId,
                s.UpdatedDate,
                s.TaskAllocUserId.HasValue ? (int)TaskAllocationEnum.AllocatedToUser : (s.TaskAllocTypeId == (int)TaskAllocationTypeEnum.Shared ? (int)TaskAllocationEnum.SharedList : s.TaskAllocTypeId == (int)TaskAllocationTypeEnum.Manila ? (int)TaskAllocationEnum.Manila : (int)TaskAllocationEnum.Unallocated),
                s.DfltTaskAllocType,
                s.DfltTaskAllocComp,
                s.DischargeTypeName,
                s.SettlementDate.HasValue ? s.SettlementDate.Value.Add(s.SettlementTime.Value) : (DateTime?)null,
                s.LoanTypeName,
                s.SecurityStates,
                s.SecuritiesAwaitingRegistration,
                s.AllSecurities,
                s.PartiallyRegistered == 1 ? true : false,
                s.ReconciledBalance ?? 0M,
                s.AccountsActionStatusTypeId,
                s.AccountsActionStatusTypeName,
                s.AccountsActioningUserId,
                s.AccountsActionUserName,
                s.AccountsStageId,
                s.AccountsStageName,
                s.PexaNominatedSettlementDate,
                s.Paperless,
                s.WorkingFromHome,
                s.RecheckRequired ?? false,
                s.IsUrgent,
                s.IsDigiDocs,
                s.DocDeliveryMethods,
                s.DocumentRequirements,
                s.FileOpenedDate,
                s.LastViewedDate,
                s.LastViewedBy,
                s.PSTIssues,
                s.IsSelfActing ?? false,
                s.RegisteredDocuments,
                s.TitleReferences
            )).FirstOrDefault();
        }

        public IEnumerable<MCE.MatterTasksListView> GetTaskManagerTasksSProc(int userId, int indexToFilter, int? matterWFComponentId = null)
        {
            var matchedList = new List<int>();
            var filteredTasks = context.TaskAllocUserFilters.Where(x => x.UserId == userId).ToList();
            if (filteredTasks == null || !filteredTasks.Any()) return null;

            List<MCE.MatterTasksListView> tasks = new List<MCE.MatterTasksListView>();

            var filter = filteredTasks[indexToFilter];
            if (filter == null) return null;

            var taskResult =
                context
                .sp_Slick_GetTaskAllocationManagerTasks
                (filter.LenderId, filter.MortMgrId, filter.WFComponentId, filter.MatterTypeId, filter.MatterMatterTypeId, filter.SettlementTypeId, filter.StateId, filter.LoanTypeId, matterWFComponentId);
                tasks = taskResult
                .Select(s => new MCE.MatterTasksListView(
                    s.MatterId,
                    TaskListTypeEnum.TaskMgrTasks,
                    s.MatterDescription,
                    s.LenderId,
                    s.MatterGroupTypeId,
                    s.MortMgrId,
                    s.MortMgrName,
                    s.StateId,
                    s.InstructionStateId,
                    s.LenderName,
                    s.LenderRefNo,
                    s.MatterWFComponentId,
                    s.WFModuleId,
                    s.CanBulkUpdate,
                    s.MatterGroupTypeId,
                    s.MatterStatusTypeId,
                    s.MatterStatusTypeName,
                    s.WFComponentId,
                    s.WFComponentName,
                    !String.IsNullOrEmpty(s.OutstandingItems),
                    s.ShowBrokerBool == 1 ? true : false,
                    s.ShowLenderBool == 1 ? true : false,
                    s.AssignedTaskColour,
                    s.AssignedTaskNote,
                    !String.IsNullOrEmpty(s.AssignedTaskNote),
                    s.DueDate,
                    s.IsMatterPexa == 1 ? true : false,
                    s.FileManager,
                    s.UpdatedByUserName,
                    s.PexaWorkspaceNos,
                    s.PexaWorkspaceStatuses,
                    s.OutstandingItems,
                    s.MatterTypesNames,
                    s.MatterGroupTypeName,
                    s.TaskAllocUsername,
                    null,
                    s.TaskAllocUserId,
                    s.TaskAllocTypeId,
                    s.UpdatedDate, 
                    s.TaskAllocUserId.HasValue ? (int)TaskAllocationEnum.AllocatedToUser : (s.TaskAllocTypeId == (int)TaskAllocationTypeEnum.Shared ? (int)TaskAllocationEnum.SharedList : s.TaskAllocTypeId == (int)TaskAllocationTypeEnum.Manila ? (int)TaskAllocationEnum.Manila : (int)TaskAllocationEnum.Unallocated),
                    s.DfltTaskAllocType,
                    s.DfltTaskAllocComp,
                    s.DischargeTypeName,
                    s.SettlementDate.HasValue ? s.SettlementDate.Value.Add(s.SettlementTime.Value) : (DateTime?)null,
                    s.LoanTypeName,
                    s.SecurityStates,
                    s.SecuritiesAwaitingRegistration,
                    s.AllSecurities,
                    s.PartiallyRegistered == 1 ? true : false,
                    s.ReconciledBalance ?? 0M,
                    s.AccountsActionStatusTypeId,
                    s.AccountsActionStatusTypeName,
                    s.AccountsActioningUserId,
                    s.AccountsActionUserName,
                    s.AccountsStageId,
                    s.AccountsStageName,
                    s.PexaNominatedSettlementDate,
                    s.Paperless,
                    s.WorkingFromHome,
                    s.RecheckRequired ?? false,
                    s.IsUrgent,
                    s.IsDigiDocs,
                    s.DocDeliveryMethods,
                    s.DocumentRequirements,
                    s.FileOpenedDate,
                    s.LastViewedDate,
                    s.LastViewedBy,
                    s.PSTIssues,
                    s.IsSelfActing ?? false,
                    s.RegisteredDocuments,
                    s.TitleReferences

                )).ToList();


            return tasks.OrderBy(x => x.OutstandingRequirementsBool).ToList();
        }
        public IEnumerable<MCE.MatterTasksListView> GetAllTaskManagerTasksSProc()
        {
            List<MCE.MatterTasksListView> tasks = new List<MCE.MatterTasksListView>();

            var taskResult =
                context
                .sp_Slick_GetTaskAllocationManagerTasks
                (null,null,null,null,null,null,null,null, null);

            tasks = taskResult.Select(s => new MCE.MatterTasksListView(
                    s.MatterId,
                    TaskListTypeEnum.TaskMgrTasks,
                    s.MatterDescription,
                    s.LenderId,
                    s.MatterGroupTypeId,
                    s.MortMgrId,
                    s.MortMgrName,
                    s.StateId,
                    s.InstructionStateId,
                    s.LenderName,
                    s.LenderRefNo,
                    s.MatterWFComponentId,
                    s.WFModuleId,
                    s.CanBulkUpdate,
                    s.MatterGroupTypeId,
                    s.MatterStatusTypeId,
                    s.MatterStatusTypeName,
                    s.WFComponentId,
                    s.WFComponentName,
                    !String.IsNullOrEmpty(s.OutstandingItems),
                    s.ShowBrokerBool == 1 ? true : false,
                    s.ShowLenderBool == 1 ? true : false,
                    s.AssignedTaskColour,
                    s.AssignedTaskNote,
                    !String.IsNullOrEmpty(s.AssignedTaskNote),
                    s.DueDate,
                    s.IsMatterPexa == 1 ? true : false,
                    s.FileManager,
                    s.UpdatedByUserName,
                    s.PexaWorkspaceNos,
                    s.PexaWorkspaceStatuses,
                    s.OutstandingItems,
                    s.MatterTypesNames,
                    s.MatterGroupTypeName,
                    s.TaskAllocUsername,
                    null,
                    s.TaskAllocUserId,
                    s.TaskAllocTypeId,
                    s.UpdatedDate,
                    s.TaskAllocUserId.HasValue ? (int)TaskAllocationEnum.AllocatedToUser : (s.TaskAllocTypeId == (int)TaskAllocationTypeEnum.Shared ? (int)TaskAllocationEnum.SharedList : s.TaskAllocTypeId == (int)TaskAllocationTypeEnum.Manila ? (int)TaskAllocationEnum.Manila : (int)TaskAllocationEnum.Unallocated),
                    s.DfltTaskAllocType,
                    s.DfltTaskAllocComp,
                    s.DischargeTypeName,
                    s.SettlementDate.HasValue ? s.SettlementDate.Value.Add(s.SettlementTime.Value) : (DateTime?)null,
                    s.LoanTypeName,
                    s.SecurityStates,
                    s.SecuritiesAwaitingRegistration,
                    s.AllSecurities,
                    s.PartiallyRegistered == 1 ? true : false,
                    s.ReconciledBalance ?? 0M,
                    s.AccountsActionStatusTypeId,
                    s.AccountsActionStatusTypeName,
                    s.AccountsActioningUserId,
                    s.AccountsActionUserName,
                    s.AccountsStageId,
                    s.AccountsStageName,
                    s.PexaNominatedSettlementDate,
                    s.Paperless,
                    s.WorkingFromHome,
                    s.RecheckRequired ?? false,
                    s.IsUrgent,
                    s.IsDigiDocs,
                    s.DocDeliveryMethods,
                    s.DocumentRequirements,
                    s.FileOpenedDate,
                    s.LastViewedDate,
                    s.LastViewedBy,
                    s.PSTIssues,
                    s.IsSelfActing ?? false,
                    s.RegisteredDocuments,
                    s.TitleReferences



                )).ToList();



            return tasks.OrderBy(x => x.OutstandingRequirementsBool).ToList();
        }
        private IEnumerable<MCE.MatterTasksListView> GetTasksView(IQueryable<MatterWFComponent> mwfc, int? userId, int? filterToIndex = null)
        {
            var matchedList = new List<int>();
            var filteredTasks = context.TaskAllocUserFilters.Where(x => x.UserId == userId);
            if (filteredTasks == null || !filteredTasks.Any()) return null;

            List<MCE.MatterTasksListView> tasks = new List<MCE.MatterTasksListView>();

           

            //foreach (var f in filteredTasks)
            if (!filterToIndex.HasValue)
            {
                foreach (var f in filteredTasks)
                {
                    var mwfcq = mwfc;
                    if (f.MatterTypeId.HasValue) mwfcq = mwfcq.Where(x => x.Matter.MatterGroupTypeId == f.MatterTypeId);
                    if (f.LenderId.HasValue) mwfcq = mwfcq.Where(x => x.Matter.LenderId == f.LenderId);
                    if (f.MortMgrId.HasValue) mwfcq = mwfcq.Where(x => x.Matter.MortMgrId == f.MortMgrId);
                    if (f.StateId.HasValue) mwfcq = mwfcq.Where(x => x.Matter.StateId == f.StateId);
                    if (f.WFComponentId.HasValue) mwfcq = mwfcq.Where(x => x.WFComponentId == f.WFComponentId);
                    if (f.SettlementTypeId.HasValue) mwfcq = mwfcq.Where(x => x.Matter.MatterSecurities.Any(y => y.SettlementTypeId == f.SettlementTypeId));
                    if (f.MatterMatterTypeId.HasValue) mwfcq = mwfcq.Where(x => x.Matter.MatterMatterTypes.Select(y => y.MatterTypeId).Any(m => m == f.MatterMatterTypeId) || !x.Matter.MatterMatterTypes.Any());
                    //if there's a restriction on matter sub-types show any that match + any that don't have any matter subtypes since these don't exist til doc prep.
                    //matchedList.AddRange(from c in mwfcq select c.MatterWFComponentId);
                    //matchedList.AddRange(mwfcq.Select(x => x.MatterWFComponentId));

                    //tasks.AddRange(GetTasksView(mwfcq));
                    if (mwfcq.Any()) tasks.AddRange(GetTasksView(mwfcq));
                }
            }
            else
            {
                if (filterToIndex < filteredTasks.Count()) //for partial loading
                {
                    var mwfcq = mwfc;

                    var f = filteredTasks.Select(x => new { x.MatterTypeId, x.LenderId, x.MortMgrId, x.StateId, x.WFComponentId, x.SettlementTypeId, x.MatterMatterTypeId }).ToList()[filterToIndex.Value];

                    if (f.MatterTypeId.HasValue) mwfcq = mwfcq.Where(x => x.Matter.MatterGroupTypeId == f.MatterTypeId);
                    if (f.LenderId.HasValue) mwfcq = mwfcq.Where(x => x.Matter.LenderId == f.LenderId);
                    if (f.MortMgrId.HasValue) mwfcq = mwfcq.Where(x => x.Matter.MortMgrId == f.MortMgrId);
                    if (f.StateId.HasValue) mwfcq = mwfcq.Where(x => x.Matter.StateId == f.StateId);
                    if (f.WFComponentId.HasValue) mwfcq = mwfcq.Where(x => x.WFComponentId == f.WFComponentId);
                    if (f.SettlementTypeId.HasValue) mwfcq = mwfcq.Where(x => x.Matter.MatterSecurities.Any(y => y.SettlementTypeId == f.SettlementTypeId));
                    if (f.MatterMatterTypeId.HasValue) mwfcq = mwfcq.Where(x => x.Matter.MatterMatterTypes.Select(y => y.MatterTypeId).Any(m => m == f.MatterMatterTypeId) || !x.Matter.MatterMatterTypes.Any());

                    return GetTasksView(mwfcq);
                }
            }
            //if (matchedList.Any())
            //{
            //    //matchedList = matchedList.Distinct().ToList();
            //    //mwfc = mwfc.Where(x => matchedList.Contains(x.MatterWFComponentId));

            //    //return GetTasksView(mwfc);
            //}

            return tasks.DistinctBy(x=>x.MatterWFComponentId);
        }

        private IEnumerable<MCE.MatterTasksListView> GetTasksView(IQueryable<MatterWFComponent> mwfc)
        {
           
            mwfc = mwfc.Where(mt => (mt.Matter.MatterGroupTypeId == (int)Enums.MatterGroupTypeEnum.NewLoan) ||
                (mt.Matter.MatterGroupTypeId == (int)Enums.MatterGroupTypeEnum.Consent) ||
                (mt.Matter.MatterGroupTypeId == (int)Enums.MatterGroupTypeEnum.Discharge && mt.Matter.FileOpenedDateOnly >= new DateTime(2019, 5, 19)))
                .OrderBy(x=>x.MatterWFComponentId);

            return mwfc.Select
                (m => new
                {
                    m.MatterId,
                    m.Matter.MatterDescription,
                    m.Matter.LenderId,
                    m.Matter.Lender.LenderName,
                    m.Matter.LenderRefNo,
                    m.Matter.MortMgrId,
                    m.Matter.MortMgr.MortMgrName,
                    InstructionStateId = m.Matter.InstructionStateId,
                    InstructionStateName = m.Matter.State1.StateName,
                    m.MatterWFComponentId,
                    m.WFComponent.WFModuleId,
                    CanBulkUpdate = m.WFComponent.WFModule.CanBulkUpdate && !m.WFComponent.BulkUpdateExcludeComponent,
                    ShowLenderBool = ((m.Matter.MatterGroupTypeId == (int)MatterGroupTypeEnum.NewLoan) && (m.Matter.LenderId != 166)),
                    ShowBrokerBool = (m.Matter.MatterGroupTypeId == (int)MatterGroupTypeEnum.NewLoan) && (m.Matter.BrokerId != null),
                    m.WFComponentId,
                    m.WFComponent.WFComponentName,
                    m.DueDate,
                    m.Matter.StateId,
                    m.Matter.State.StateName,
                    m.Matter.MatterDischarge.DischargeType.DischargeTypeName,
                    m.Matter.MatterStatusType.MatterStatusTypeName,
                    m.Matter.MatterStatusTypeId,
                    TaskAllocatedTo = m.User1.Username,
                    TaskAllocatedToInitials = m.User1.DisplayInitials,
                    m.AssignedTaskNote,
                    m.TaskAllocTypeId,
                    m.TaskAllocUserId,
                    m.AssignedTaskColour,
                    DfltTaskAllocType = m.TaskAllocType.TaskAllocTypeName,
                    DfltTaskComponent = m.WFComponent1.WFComponentName,
                    m.UpdatedDate,
                    UpdatedByUsername = m.User.Username,
                    OutstandingRequirementsBool = m.WFComponentId == (int)WFComponentEnum.FollowupOutstandingRequirements,
                    MatterTypes = m.Matter.MatterMatterTypes.Select(x => x.MatterType.MatterTypeName),
                    MatterTypeIds = m.Matter.MatterMatterTypes.Select(x => x.MatterTypeId),
                    MatterTypeGroup = m.Matter.MatterType.MatterTypeName,
                    MatterTypeGroupId = m.Matter.MatterGroupTypeId,
                    FileManager = m.Matter.User.Username,
                    IsMatterPexa = m.Matter.MatterSecurities.Any(y => y.SettlementTypeId == (int)SettlementTypeEnum.PEXA),
                    CreationDate = m.Matter.FileOpenedDateOnly,
                    PexaWorkspaceNos = m.Matter.MatterSecurities.SelectMany(y => y.MatterSecurityMatterPexaWorkspaces.Select(z => z.MatterPexaWorkspace.PexaWorkspace.PexaWorkspaceNo)),
                    PexaWorkspaceStatuses = m.Matter.MatterSecurities.SelectMany(z => z.MatterSecurityMatterPexaWorkspaces.Where(mp => mp.MatterPexaWorkspace.PexaWorkspace.PexaWorkspaceStatusTypeId.HasValue)
                                            .Select(p => p.MatterPexaWorkspace.PexaWorkspace.PexaWorkspaceStatusType.PexaWorkspaceStatusTypeName)),
                    OutstandingItems = m.MatterWFOutstandingReqs.FirstOrDefault().MatterWFOutstandingReqItems.Where(o => !o.Resolved).Select(o => o.OutstandingItemName),
                    SettlementDate = m.Matter.SettlementSchedule != null ? (DateTime?)m.Matter.SettlementSchedule.SettlementDate : (DateTime?)null,
                    SettlementTime = m.Matter.SettlementSchedule != null ? (TimeSpan?)m.Matter.SettlementSchedule.SettlementTime : (TimeSpan?)null,
                    LoanType = m.Matter.LoanTypeId.HasValue ? m.Matter.LoanType.LoanTypeName : null
                })
                .AsNoTracking()
                .ToList().Select(s => new MCE.MatterTasksListView
                (
                    s.MatterId,
                    TaskListTypeEnum.TaskMgrTasks,
                    s.MatterDescription,
                    s.LenderId,
                    s.MatterTypeGroupId,
                    s.MortMgrId,
                    s.StateId,
                    s.InstructionStateId,
                    s.LenderName,
                    s.LenderRefNo,
                    s.MatterWFComponentId,
                    s.WFModuleId,
                    s.CanBulkUpdate,
                    s.MatterTypeGroupId,
                    s.MatterStatusTypeId,
                    s.MatterStatusTypeName,
                    s.WFComponentId,
                    s.WFComponentName,
                    s.OutstandingRequirementsBool,
                    s.ShowBrokerBool,
                    s.ShowLenderBool,
                    s.AssignedTaskColour,
                    s.AssignedTaskNote,
                    String.IsNullOrEmpty(s.AssignedTaskNote),
                    s.DueDate,
                    s.IsMatterPexa,
                    s.FileManager,
                    s.UpdatedByUsername,
                    s.PexaWorkspaceNos,
                    s.PexaWorkspaceStatuses,
                    s.OutstandingItems,
                    s.MatterTypes,
                    s.MatterTypeGroup,
                    s.TaskAllocatedTo,
                    s.TaskAllocatedToInitials,
                    //s.TaskAllocUserId.HasValue ? EntityHelper.GetFullName(s.TaskLastName, s.TaskFirstName) : string.Empty,
                    //EntityHelper.GetInitials(s.TaskLastName, s.TaskFirstName),
                    s.TaskAllocUserId,
                    s.TaskAllocTypeId,
                    s.UpdatedDate,
                    s.TaskAllocUserId.HasValue ? (int)TaskAllocationEnum.AllocatedToUser : (s.TaskAllocTypeId == (int)TaskAllocationTypeEnum.Shared ? (int)TaskAllocationEnum.SharedList : s.TaskAllocTypeId == (int)TaskAllocationTypeEnum.Manila ? (int)TaskAllocationEnum.Manila : (int)TaskAllocationEnum.Unallocated),
                    s.DfltTaskAllocType,
                    s.DfltTaskComponent,
                    s.SettlementDate.HasValue ? s.SettlementDate.Value.Add(s.SettlementTime.Value) : (DateTime?)null,
                    s.LoanType
                )).ToList();

            
            ////(s => new MCE.MatterTasksListView
            ////{
            ////    MatterId = s.MatterId,
            ////    TaskListType = TaskListTypeEnum.TaskMgrTasks,
            ////    MatterDescription = s.MatterDescription,
            ////    LenderName = s.LenderName,
            ////    LenderRefNo = s.LenderRefNo,
            ////    MatterWFComponentId = s.MatterWFComponentId,
            ////    CanBulkUpdate = s.CanBulkUpdate,
            ////    WFModuleId = s.WFModuleId,
            ////    WFComponentName = s.WFComponentName,
            ////    StateName = s.StateName,
            ////    DischargeType = s.DischargeTypeName,
            ////    MatterStatus = s.MatterStatusTypeName,
            ////    MatterStatusId = s.MatterStatusTypeId,
            ////    MatterTypeGroupId = s.MatterTypeGroupId,
            ////    DueDate = s.DueDate,
            ////    IsPastDue = s.DueDate.HasValue && s.DueDate.Value.Date < DateTime.Now.Date,
            ////    TaskAllocatedTo = s.TaskAllocUserId.HasValue ? EntityHelper.GetFullName(s.TaskLastName, s.TaskFirstName) : string.Empty,
            ////    TaskAllocatedInitials = EntityHelper.GetInitials(s.TaskLastName, s.TaskFirstName),
            ////    TaskAllocatedToId = s.TaskAllocUserId.HasValue ? s.TaskAllocUserId :
            ////                (s.TaskAllocTypeId == (int)TaskAllocationTypeEnum.Shared ? (int)TaskAllocationEnum.SharedList : s.TaskAllocTypeId == (int)TaskAllocationTypeEnum.Manila ? (int)TaskAllocationEnum.Manila : (int)TaskAllocationEnum.Unallocated),
            ////    TaskAllocatedUserId = s.TaskAllocUserId,
            ////    DfltTaskAllocation = s.DfltTaskAllocType,
            ////    DfltTaskWFComponentAllocation = s.DfltTaskComponent,
            ////    TaskAllocationType = s.TaskAllocUserId.HasValue ? (int)TaskAllocationEnum.AllocatedToUser : (s.TaskAllocTypeId == (int)TaskAllocationTypeEnum.Shared ? (int)TaskAllocationEnum.SharedList : s.TaskAllocTypeId == (int)TaskAllocationTypeEnum.Manila ? (int)TaskAllocationEnum.Manila : (int)TaskAllocationEnum.Unallocated),
            ////    MatterTypes = EntityHelper.BuildConcatenatedString(s.MatterTypes) ?? s.MatterTypeGroup,
            ////    IsMatterPexa = s.IsMatterPexa,
            ////    PaperPexa = s.IsMatterPexa ? "PEXA" : "Paper",
            ////    PexaWorkspacesStr = EntityHelper.BuildConcatenatedString(s.MatterPexaWorkspaces.Select(x=>x.PexaWorkspace.PexaWorkspaceNo)) ?? null,
            ////    PexaWorkspaceStatusesStr = EntityHelper.BuildConcatenatedString(s.MatterPexaWorkspaces.Select(x => x.PexaWorkspace.PexaWorkspaceStatusType?.PexaWorkspaceStatusTypeName)) ?? null,
            ////    UpdatedByUsername = s.UpdatedByUsername,
            ////    UpdatedDate = s.UpdatedDate,
            ////    SettlementDate = s.SettlementSchedule?.SettlementDate.Add(s.SettlementSchedule.SettlementTime)
            ////});

            //var test = mwfc.Where(m => m.MatterId == 3031149);
            //var test2 = tasks.Where(m => m.MatterId == 3031149);
            
        }

        public IEnumerable<TaskAllocationView> GetSharedTasksView()
        {
            return context.SharedTaskAllocUsers.
                Select(x => new TaskAllocationView
                {
                    Id = Guid.NewGuid(),
                    TaskAllocationId = x.SharedTaskAllocUserId,
                    LenderId = x.LenderId ?? DomainConstants.AnySelectionId,
                    LenderName = x.Lender.LenderName ?? DomainConstants.AnySelection,
                    MatterTypeId = x.MatterTypeId ?? DomainConstants.AnySelectionId,
                    MatterTypeName = x.MatterType.MatterTypeName ?? DomainConstants.AnySelection,
                    MortMgrId = x.MortMgrId ?? DomainConstants.AnySelectionId,
                    MortMgrName = x.MortMgr.MortMgrName ?? DomainConstants.AnySelection,
                    StateId = x.StateId ?? DomainConstants.AnySelectionId,
                    StateName = x.State.StateName ?? DomainConstants.AnySelection,
                    SettlementTypeId = x.SettlementTypeId ?? DomainConstants.AnySelectionId,
                    SettlementTypeName = x.SettlementType.SettlementTypeName ?? DomainConstants.AnySelection,
                    WFComponentId = x.WFComponentId,
                    AllocUserId = x.UserId,
                    AllocUserName = x.User.Username,
                    UpdatedByUserId = x.UpdatedByUserId,
                    UpdatedBy = x.User1.Username,
                    UpdatedDate = x.UpdatedDate
                });
        }

        public IEnumerable<TaskAllocationView> GetTaskFilterView()
        {
            return context.TaskAllocUserFilters.
               Select(x => new TaskAllocationView
               {
                   Id = Guid.NewGuid(),
                   TaskAllocationId = x.TaskAllocUserFilterId,
                   LenderId = x.LenderId ?? DomainConstants.AnySelectionId,
                   MatterTypeId = x.MatterTypeId ?? DomainConstants.AnySelectionId,
                   MatterMatterTypeId = x.MatterMatterTypeId ?? DomainConstants.AnySelectionId,
                   MortMgrId = x.MortMgrId ?? DomainConstants.AnySelectionId,
                   StateId = x.StateId ?? DomainConstants.AnySelectionId,
                   WFComponentId = x.WFComponentId ?? DomainConstants.AnySelectionId,
                   SettlementTypeId = x.SettlementTypeId ?? DomainConstants.AnySelectionId,
                   LoanTypeId = x.LoanTypeId ?? DomainConstants.AnySelectionId,
                   AllocUserId = x.UserId,
                   UpdatedByUserId = x.UpdatedByUserId,
                   UpdatedBy = x.User1.Username,
                   UpdatedDate = x.UpdatedDate
               });
        }
        public IEnumerable<TaskAllocationView> GetTaskFiltersView(int userId)
        {
            return context.TaskAllocUserFilters.Where(u=>u.UserId == userId).
               Select(x => new TaskAllocationView
               {
                   Id = Guid.NewGuid(),
                   TaskAllocationId = x.TaskAllocUserFilterId,
                   LenderId = x.LenderId ?? DomainConstants.AnySelectionId,
                   MatterTypeId = x.MatterTypeId ?? DomainConstants.AnySelectionId,
                   MatterMatterTypeId = x.MatterMatterTypeId ?? DomainConstants.AnySelectionId,
                   MortMgrId = x.MortMgrId ?? DomainConstants.AnySelectionId,
                   StateId = x.StateId ?? DomainConstants.AnySelectionId,
                   WFComponentId = x.WFComponentId ?? DomainConstants.AnySelectionId,
                   SettlementTypeId = x.SettlementTypeId ?? DomainConstants.AnySelectionId,
                   LoanTypeId = x.LoanTypeId ?? DomainConstants.AnySelectionId,
                   AllocUserId = x.UserId,
                   UpdatedByUserId = x.UpdatedByUserId,
                   UpdatedBy = x.User1.Username,
                   UpdatedDate = x.UpdatedDate
               });
        }
        public IEnumerable<TaskAllocationView> GetTaskAllocationAssignmentsView()
        {
            return context.TaskAllocAssignments.
               Select(x => new TaskAllocationView
               {
                   Id = Guid.NewGuid(),
                   TaskAllocationId = x.TaskAllocAssignmentId,
                   LenderId = x.LenderId ?? DomainConstants.AnySelectionId,
                   MatterTypeId = x.MatterTypeId ?? DomainConstants.AnySelectionId,
                   MortMgrId = x.MortMgrId ?? DomainConstants.AnySelectionId,
                   StateId = x.StateId ?? DomainConstants.AnySelectionId,
                   WFComponentId = x.WFComponentId ?? DomainConstants.AnySelectionId,
                   SettlementTypeId = x.SettlementTypeId ?? DomainConstants.AnySelectionId,
                   OriginalUserId = x.OriginalUserId,
                   AllocUserId = x.UserId,
                   AllocatedFromDate = x.DateFrom,
                   AllocatedToDate = x.DateTo,
                   UpdatedByUserId = x.UpdatedByUserId,
                   UpdatedBy = x.User2.Username,
                   UpdatedDate = x.UpdatedDate,
                   isDirty = false
               });
        }

        public List<EntityCompacted> ValidateSharedTasks(IEnumerable<MatterCustomEntities.MatterTasksListView> view)
        {
            if (view == null || !view.Any()) return null;

            var availableSharedTasks = context.SharedTaskAllocUsers;
            if (availableSharedTasks == null || !availableSharedTasks.Any()) return null;

            var sharedTasksForUser = availableSharedTasks.Where(x => x.UserId ==  GlobalVars.CurrentUser.UserId);
            var sharedWarnings = new List<EntityCompacted>();

            foreach(var item in view)
            {
                if (!availableSharedTasks.Any(x=> x.WFComponentId == item.WFComponentId 
                        && (x.MatterTypeId == null || x.MatterTypeId == item.MatterTypeId)
                        && (x.LenderId == null || x.LenderId == item.LenderId)
                        && (x.StateId == null || x.StateId == item.StateId)
                        && (x.MortMgrId == null || x.MortMgrId == item.MortMgrId)
                        && (x.SettlementTypeId == null || 
                            (x.SettlementTypeId == (int)SettlementTypeEnum.PEXA && item.IsMatterPexa) 
                            || (x.SettlementTypeId == (int)SettlementTypeEnum.Paper && !item.IsMatterPexa)) 
                   ))
                {
                    sharedWarnings.Add(new EntityCompacted { Id = item.MatterWFComponentId, Details = item.WFComponentName, RelatedId = item.MatterId, IsChecked = true });
                    continue;
                }

                if ((sharedTasksForUser == null || !sharedTasksForUser.Any()) ||
                        (
                        !sharedTasksForUser.Any(x => x.WFComponentId == item.WFComponentId
                        && (x.MatterTypeId == null || x.MatterTypeId == item.MatterTypeId)
                        && (x.LenderId == null || x.LenderId == item.LenderId)
                        && (x.StateId == null || x.StateId == item.StateId)
                        && (x.MortMgrId == null || x.MortMgrId == item.MortMgrId)
                        && (x.SettlementTypeId == null ||
                            (x.SettlementTypeId == (int) SettlementTypeEnum.PEXA && item.IsMatterPexa)
                            || (x.SettlementTypeId == (int) SettlementTypeEnum.Paper && !item.IsMatterPexa))
                   )))
                {
                    sharedWarnings.Add(new EntityCompacted { Id = item.MatterWFComponentId, Details = item.WFComponentName, RelatedId = item.MatterId, IsChecked = false });
                    continue;
                }
            }

            return sharedWarnings;
        }
    }
}
