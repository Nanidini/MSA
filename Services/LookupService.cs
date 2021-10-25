using System.Collections.Generic;
using System.Linq;
using Slick_Domain.Entities;
using Slick_Domain.Models;
using Slick_Domain.Enums;
namespace Slick_Domain.Services {
    public class LookupService {
        private readonly SlickContext _context;

        public LookupService(SlickContext context) {
            _context = context;
        }

        public List<LookupValue> GetLookups(ReportParamInfo reportParam) {
            List<LookupValue> list = new List<LookupValue>();

            switch (reportParam.ParameterName.ToLower()) {

                case "accounttype":

                    using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadUncommitted))
                    {
                        var repo = uow.GetGeneralRepositoryInstance();
                        return repo.GetGeneralCheckList_ActionType(true).Select(x => new LookupValue() { id = x.Id, value = x.Name }).ToList();
                    }

                case "booleanparam":

                    return list;

                case "dischargesummaryId":

                    using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadUncommitted))
                    {
                        var repo = uow.GetGeneralRepositoryInstance();
                        return repo.GetGeneralCheckList_DischargeType(true,true, true).Select(x => new LookupValue() { id = x.Id, value = x.Name }).ToList();
                    }

                case "dischargetypeid":

                    using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadUncommitted))
                    {
                        var repo = uow.GetGeneralRepositoryInstance();
                        return repo.GetGeneralCheckList_DischargeType(true, true, reportParam.IncludeNullSelect).Select(x => new LookupValue() { id = x.Id, value = x.Name }).ToList();
                    }

                case "errorwfcomponentid":

                    return list;

                case "fileowneruserid":

                    using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadUncommitted))
                    {
                        var repo = uow.GetUserRepositoryInstance();
                        list = repo.GetLookupList();
                        list.Insert(0, new LookupValue() { id = -1, value = "All" });
                    }

                    return list;

                case "invoicepaid":

                    list.Insert(0, new LookupValue() { id = 0, value = "True" });
                    list.Insert(0, new LookupValue() { id = 1, value = "False" });

                    return list;

                case "lenderid":

                    using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadUncommitted))
                    {
                        var repo = uow.GetLendersRepository();
                        list = repo.GetLookupList();
                        list.Insert(0, new LookupValue() { id = -1, value = "All" });
                    }

                    return list;

                case "matchedunmatched":

                    list.Add(new LookupValue() { id = 0, value = "Matched" });
                    list.Add(new LookupValue() { id = 1, value = "Unmatched" });

                    return list;

                case "mattergrouptypeid":

                    using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadUncommitted))
                    {
                        var repo = new MatterTypeRepository(uow.Context);
                        list = repo.GetLookupList();
                        list.Insert(0, new LookupValue() {id = -1, value = "All"});
                    }

                    return list;

                case "matterid":

                    return list;

                case "matterstatustypeid":

                    using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadUncommitted))
                    {
                        var repo = new MatterStatusTypeRepository(uow.Context);
                        list = repo.GetLookupList();
                        list.Insert(0, new LookupValue() { id = -1, value = "All" });
                    }

                    return list;

                case "mortmgrid":

                    using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadUncommitted))
                    {
                        var repo = new MortMgrRepository(uow.Context);
                        list = repo.GetLookupList();
                        list.Insert(0, new LookupValue() {id = -1, value = "All"});
                    }

                    return list;

                case "payablebytypeid":

                    return list;

                case "referenceid":

                    return list;

                case "revenueselectionid":

                    list.Add(new LookupValue() { id = -1, value = "All" });
                    list.Add(new LookupValue() { id = 0, value = "None" });

                    return list;

                case "securitystateid":

                    using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadUncommitted))
                    {
                        var repo = new SecurityStateRepository(uow.Context);
                        list = repo.GetLookupList();
                        list.Insert(0, new LookupValue() {id = -1, value = "All"});
                    }

                    return list;

                case "settlementregiontypeid":

                    using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadUncommitted))
                    {
                        var repo = new GeneralRepository(uow.Context);
                        return repo.GetGeneralCheckList_SettlementRegionType(true, true, reportParam.IncludeNullSelect).Select(x => new LookupValue() { id = x.Id, value = x.Name }).ToList();
                    }

                case "settlementschedulestatustypeid":

                    using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadUncommitted))
                    {
                        var repo = new GeneralRepository(uow.Context);
                        return repo.GetGeneralCheckList_SettlementScheduleStatus(true, true, reportParam.IncludeNullSelect).Select(x => new LookupValue() { id = x.Id, value = x.Name }).ToList();
                    }

                case "settlementstateid":

                    using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadUncommitted))
                    {
                        var repo = new GeneralRepository(uow.Context);
                        return repo.GetGeneralCheckList_State(true, true, reportParam.IncludeNullSelect).Select(x => new LookupValue() { id = x.Id, value = x.Name }).ToList();
                    }

                case "settlementtypeid":

                    using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadUncommitted))
                    {
                        var repo = new GeneralRepository(uow.Context);
                        return repo.GetGeneralCheckList_SettlementType(true, true, reportParam.IncludeNullSelect).Select(x => new LookupValue() { id = x.Id, value = x.Name }).ToList();
                    }

                case "showparts":

                    list.Add(new LookupValue() { id = 0, value = "Chart" });
                    list.Add(new LookupValue() { id = 1, value = "List" });
                    list.Add(new LookupValue() { id = -1, value = "Both" });

                    return list;

                case "singlewfcomponentid":

                    return list;

                case "stateid":

                    using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadUncommitted))
                    {
                        var repo = new GeneralRepository(uow.Context);
                        return repo.GetGeneralCheckList_State(true, true, reportParam.IncludeNullSelect).Select(x => new LookupValue() {id = x.Id, value = x.Name}).ToList();
                    }

                case "transactiondirectiontypeid":

                    using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadUncommitted))
                    {
                        var repo = new GeneralRepository(uow.Context);
                        return repo.GetGeneralCheckList_TransactionDirectionType(true, true, reportParam.IncludeNullSelect).Select(x => new LookupValue() { id = x.Id, value = x.Name }).ToList();
                    }

                case "trustaccountid":

                    return list;

                case "trustaccountid2":

                    return list;

                case "userid":

                    return list;

                case "wfcomponentid":

                    using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadUncommitted))
                    {
                        var repo = new GeneralRepository(uow.Context);
                        return repo.GetGeneralCheckList_WFComponent(true, true, reportParam.IncludeNullSelect).Select(x => new LookupValue() { id = x.Id, value = x.Name }).ToList();
                    }

                default:
                    list.Insert(0, new LookupValue() {id = -1, value = "All"});

                    return list;
            }
        }

        public List<LookupValue> GetLookups(ReportParamInfo reportParam, User user)
        {
            List<LookupValue> list = new List<LookupValue>();

            switch (reportParam.ParameterName.ToLower())
            {

                case "accounttype":

                    using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadUncommitted))
                    {
                        var repo = uow.GetGeneralRepositoryInstance();
                        return repo.GetGeneralCheckList_ActionType(true).Select(x => new LookupValue() { id = x.Id, value = x.Name }).ToList();
                    }

                case "booleanparam":

                    return list;

                case "dischargesummaryId":
                    using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadUncommitted))
                    {
                        var repo = uow.GetGeneralRepositoryInstance();
                        return repo.GetGeneralCheckList_DischargeType(true, true, true).Select(x => new LookupValue() { id = x.Id, value = x.Name }).ToList();
                    }

                case "dischargetypeid":

                    using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadUncommitted))
                    {
                        var repo = uow.GetGeneralRepositoryInstance();
                        return repo.GetGeneralCheckList_DischargeType(true, true, reportParam.IncludeNullSelect).Select(x => new LookupValue() { id = x.Id, value = x.Name }).ToList();
                    }

                case "errorwfcomponentid":

                    return list;

                case "fileowneruserid":

                    using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadUncommitted))
                    {
                        var repo = uow.GetUserRepositoryInstance();
                        list = repo.GetLookupList();
                        list.Insert(0, new LookupValue() { id = -1, value = "All" });
                    }

                    return list;

                case "invoicepaid":

                    list.Insert(0, new LookupValue() { id = 0, value = "True" });
                    list.Insert(0, new LookupValue() { id = 1, value = "False" });

                    return list;

                case "lenderid":

                    using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadUncommitted))
                    {
                        var repo = uow.GetLendersRepository();
                        if (user.LenderId.HasValue)
                        {
                            list.Add(new LookupValue() { id = user.LenderId.Value, value = user.Lender.LenderName });
                        }
                        else if (user.MortMgrId.HasValue)
                        {
                            list = repo.GetMortMgrLookupList(user);
                        }
                        
                        list.Insert(0, new LookupValue() { id = -1, value = "All" });
                    }

                    return list;

                case "matchedunmatched":

                    list.Add(new LookupValue() { id = 0, value = "Matched" });
                    list.Add(new LookupValue() { id = 1, value = "Unmatched" });

                    return list;

                case "mattergrouptypeid":

                    using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadUncommitted))
                    {
                        var repo = new MatterTypeRepository(uow.Context);
                        list = repo.GetLookupList();
                        list.Insert(0, new LookupValue() { id = -1, value = "All" });
                    }

                    return list;

                case "matterid":

                    return list;

                case "matterstatustypeid":

                    using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadUncommitted))
                    {
                        var repo = new MatterStatusTypeRepository(uow.Context);
                        list = repo.GetLookupList();
                        list.Insert(0, new LookupValue() { id = -1, value = "All" });
                    }

                    return list;

                case "mortmgrid":

                    using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadUncommitted))
                    {
                        var repo = new MortMgrRepository(uow.Context);
                        if (user.UserTypeId == (int)UserTypeEnum.Lender)
                        {
                            list = repo.GetLookupList(); 
                        }
                        else if (user.UserTypeId == (int)UserTypeEnum.MortMgr)
                        {
                            list.Insert(0, new LookupValue() { id = user.MortMgrId.Value, value = user.MortMgr.MortMgrName });
                        }

                        list.Insert(0, new LookupValue() { id = -1, value = "All" });
                    }

                    return list;

                case "payablebytypeid":

                    return list;

                case "referenceid":

                    return list;

                case "revenueselectionid":

                    list.Add(new LookupValue() { id = -1, value = "All" });
                    list.Add(new LookupValue() { id = 0, value = "None" });

                    return list;

                case "securitystateid":

                    using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadUncommitted))
                    {
                        var repo = new SecurityStateRepository(uow.Context);
                        list = repo.GetLookupList();
                        list.Insert(0, new LookupValue() { id = -1, value = "All" });
                    }

                    return list;

                case "settlementregiontypeid":

                    using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadUncommitted))
                    {
                        var repo = uow.GetGeneralRepositoryInstance();
                        return repo.GetGeneralCheckList_SettlementRegionType(true, true, reportParam.IncludeNullSelect).Select(x => new LookupValue() { id = x.Id, value = x.Name }).ToList();
                    }

                case "settlementschedulestatustypeid":

                    using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadUncommitted))
                    {
                        var repo = uow.GetGeneralRepositoryInstance();
                        return repo.GetGeneralCheckList_SettlementScheduleStatus(true, true, reportParam.IncludeNullSelect).Select(x => new LookupValue() { id = x.Id, value = x.Name }).ToList();
                    }

                case "settlementstateid":

                    using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadUncommitted))
                    {
                        var repo = uow.GetGeneralRepositoryInstance();
                        return repo.GetGeneralCheckList_State(true, true, reportParam.IncludeNullSelect).Select(x => new LookupValue() { id = x.Id, value = x.Name }).ToList();
                    }

                case "settlementtypeid":

                    using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadUncommitted))
                    {
                        var repo = uow.GetGeneralRepositoryInstance();
                        return repo.GetGeneralCheckList_SettlementType(true, true, reportParam.IncludeNullSelect).Select(x => new LookupValue() { id = x.Id, value = x.Name }).ToList();
                    }

                case "showparts":

                    list.Add(new LookupValue() { id = 0, value = "Chart" });
                    list.Add(new LookupValue() { id = 1, value = "List" });
                    list.Add(new LookupValue() { id = -1, value = "Both" });

                    return list;

                case "singlewfcomponentid":

                    return list;

                case "stateid":

                    using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadUncommitted))
                    {
                        var repo = uow.GetGeneralRepositoryInstance();
                        return repo.GetGeneralCheckList_State(true, true, reportParam.IncludeNullSelect).Select(x => new LookupValue() { id = x.Id, value = x.Name }).ToList();
                    }

                case "transactiondirectiontypeid":

                    using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadUncommitted))
                    {
                        var repo = uow.GetGeneralRepositoryInstance();
                        return repo.GetGeneralCheckList_TransactionDirectionType(true, true, reportParam.IncludeNullSelect).Select(x => new LookupValue() { id = x.Id, value = x.Name }).ToList();
                    }

                case "trustaccountid":

                    return list;

                case "trustaccountid2":

                    return list;

                case "userid":

                    return list;

                case "wfcomponentid":

                    using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadUncommitted))
                    {
                        var repo = uow.GetGeneralRepositoryInstance();
                        return repo.GetGeneralCheckList_WFComponent(true, true, reportParam.IncludeNullSelect).Select(x => new LookupValue() { id = x.Id, value = x.Name }).ToList();
                    }

                default:
                    list.Insert(0, new LookupValue() { id = -1, value = "All" });

                    return list;
            }
        }


    }
}