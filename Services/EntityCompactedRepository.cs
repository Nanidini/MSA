using Slick_Domain.Common;
using Slick_Domain.Entities;
using Slick_Domain.Models;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Collections.ObjectModel;

namespace Slick_Domain.Services
{
    /// <summary>
    /// Compacting Entity Repository, used for compacting models into smaller objects. 
    /// </summary>
    public class EntityCompactedRepository : SlickRepository
    {

        public EntityCompactedRepository(SlickContext Context) : base(Context)
        {
        }
        /// <summary>
        /// Get all reissue reasons for a matter. 
        /// </summary>
        /// <returns>Return all the possible reasons for reissuing documents.</returns>
        public IEnumerable<EntityCompacted> GetReissueReasons()
        {
            return
                context.Reasons.AsNoTracking().Where(m=>m.ReasonGroupTypeId==9).Select(m => new EntityCompacted { Id = m.ReasonId, Details = m.ReasonTxt })
                .ToList();
        }

        public IEnumerable<EntityCompacted> GetCustodianIssueResponsibleParties()
        {
            return context.CustodianIssueResponsiblePartyTypes.Select(x => new EntityCompacted { Id = x.CustodianIssueResponsiblePartyTypeId, Details = x.CustodianIssueResponsiblePartyTypeName }).ToList();
        }

        /// <summary>
        /// Insert the reissue reason to the database .
        /// </summary>
        /// <param name="reason"></param>
        public void InsertReissueReasons(Reason reason)
        {
            context.Reasons.Add(reason);
        }
        public IEnumerable<EntityCompacted> GetSettlementTypesCompacted()
        {
            return
                 context.SettlementTypes.AsNoTracking()
                .Select(m2 => new EntityCompacted { Id = m2.SettlementTypeId, Details = m2.SettlementTypeName })
                .ToList();
        }

        public IEnumerable<EntityCompacted> GetStatesCompacted()
        {
            return
                 context.States.AsNoTracking()
                .Select(m2 => new EntityCompacted { Id = m2.StateId, Details = m2.StateName })
                .ToList();
        }

        public IEnumerable<EntityCompacted> GetSelfActingTypesCompacted()
        {
            return context.EmailSelfActingTypes.Select(m => new EntityCompacted { Id = m.SelfActingTypeId, Details = m.SelfActingTypeName })
                .ToList();
        }
        public IEnumerable<EntityCompacted> GetLendersCompacted(bool isEnabled = true)
        {
            return
                 context.Lenders.AsNoTracking()
                .Where(s => s.Enabled == isEnabled)
                .OrderBy(o => o.LenderName)
                .Select(m2 => new EntityCompacted { Id = m2.LenderId, Details = m2.LenderName, RelatedDetail = m2.LenderNameShort, AdditionalFlag = m2.SSORequiredDefault })
                .ToList();
        }

        public IEnumerable<EntityCompacted> GetFileLookupTypesCompacted()
        {
            return
                 context.FileLookupTypes.AsNoTracking()
                .OrderBy(o => o.FileLookupTypeName)
                .Select(m2 => new EntityCompacted { Id = m2.FileLookupTypeId, Details = m2.FileLookupTypeName })
                .ToList();
        }

        public IEnumerable<EntityCompacted> GetMatterTypesCompacted(bool IsMatterGroup)
        {
            return
                 context.MatterTypes.AsNoTracking()
                .Where(s => s.Enabled == true && s.IsMatterGroup == IsMatterGroup)
                .OrderBy(o => o.MatterTypeName)
                .Select(m2 => new EntityCompacted { Id = m2.MatterTypeId, Details = m2.MatterTypeName, RelatedDetail = m2.MatterTypeDesc })
                .ToList();
        }

        public IEnumerable<EntityCompacted> GetAllMatterTypesCompacted()
        {
            return
                 context.MatterTypes.AsNoTracking()
                .Where(s => s.Enabled == true)
                .OrderBy(o => o.MatterTypeName)
                .Select(m2 => new EntityCompacted { Id = m2.MatterTypeId, Details = m2.MatterTypeName, RelatedDetail = m2.MatterTypeDesc })
                .ToList();
        }

        public IEnumerable<EntityCompacted> GetDocumentDeliveryTypes()
        {
            return
                 context.DocumentDeliveryTypes.AsNoTracking()
                .OrderBy(o => o.DocumentDeliveryTypeName)
                .Select(m2 => new EntityCompacted { Id = m2.DocumentDeliveryTypeId, Details = m2.DocumentDeliveryTypeName })
                .ToList();
        }

        public IEnumerable<EntityCompacted> GetDocumentSentToTypes()
        {
            return
                 context.DocumentsSentToTypes.AsNoTracking()
                .OrderBy(o => o.DocumentsSentToTypeName)
                .Select(m2 => new EntityCompacted { Id = m2.DocumentsSentToTypeId, Details = m2.DocumentsSentToTypeName })
                .ToList();
        }
        
        public IEnumerable<EntityCompacted> GetMatterNoteTypesCompacted()
        {
            return
                 context.MatterNoteTypes.AsNoTracking()
                .OrderBy(o => o.MatterNoteTypeName)
                .Select(m2 => new EntityCompacted { Id = m2.MatterNoteTypeId, Details = m2.MatterNoteTypeName })
                .ToList();
        }

        public IEnumerable<EntityCompacted> GetMortMgrsCompacted(bool isEnabled = true)
        {
            return
                 context.MortMgrs.AsNoTracking()
                .Where(s => s.Enabled == isEnabled)
                .OrderBy(o => o.MortMgrName)
                .Select(m2 => new EntityCompacted { Id = m2.MortMgrId, Details = m2.MortMgrName, AdditionalFlag = m2.SSORequiredDefault })
                .ToList();
        }

        public IEnumerable<EntityCompacted> GetConsignmentContentTypesCompacted()
        {
            return
                 context.ConsignmentContentTypes.AsNoTracking()
                .OrderBy(o => o.ConsignmentContentTypeName)
                .Select(m2 => new EntityCompacted { Id = m2.ConsignmentContentTypeId, Details = m2.ConsignmentContentTypeName })
                .ToList();
        }

        public IEnumerable<EntityCompacted> GetSettlementCancelReasons()
        {
            var reasons =
                 context.SettlementCancellationReasons.AsNoTracking()
                .Where(s => s.Enabled == true)
                .OrderBy(o => o.SettlementCancellationReasonText)
                .Select(m2 => new EntityCompacted { Id = m2.SettlementCancellationReasonId, Details = m2.SettlementCancellationReasonText })
                .ToList();

            return reasons;
        }

        public IEnumerable<EntityCompacted> GetMortgageesCompacted()
        {
            return
                 context.Mortgagees.AsNoTracking()
                .OrderBy(o => o.MortgageeName)
                .Select(m2 => new EntityCompacted { Id = m2.MortgageeId, Details = m2.MortgageeName })
                .ToList();
        }

        public IEnumerable<EntityCompacted> GetOfficesCompacted()
        {
            return
                 context.Offices.AsNoTracking()
                .OrderBy(o => o.OfficeName)
                .ToList()
                .Select(m2 => new EntityCompacted { Id = m2.OfficeId, Details = m2.OfficeName, RelatedDetail = EntityHelper.FormOfficeAddressDetails(m2)})
                .ToList();
        }

        public IEnumerable<EntityCompacted> GetConsignmentTypesCompacted()
        {
            return
                 context.ConsignmentTypes.AsNoTracking()
                .Select(m2 => new EntityCompacted { Id = m2.ConsignmentTypeId, Details = m2.ConsignmentTypeDesc })
                .ToList();
        }
        public IEnumerable<EntityCompacted> GetSettlementAgentsCompacted()
        {
            return
                 context.SettlementAgents.AsNoTracking()
                .Select(m => new
                {
                    Id = m.SettlementAgentId,
                    m.AgentName,
                    Enabled = m.Enabled,
                    m.StreetAddress,
                    m.Suburb,
                    m.PostCode,
                    m.State.StateName
                })
                .ToList()
                .Select(m2 => new EntityCompacted
                {
                    Id = m2.Id,
                    Details = m2.AgentName,
                    Enabled = m2.Enabled,
                    RelatedDetail = EntityHelper.FormatAddressDetails(m2.StreetAddress,m2.Suburb,m2.StateName,m2.PostCode)
                })
                .ToList()
                ;
        }

        public IEnumerable<EntityCompacted> GetDeliveryCompaniesCompacted()
        {
            return
                 context.ConsignmentDeliveryCompanies.AsNoTracking()
                .Select(m => new EntityCompacted { Id = m.ConsignmentDeliveryCompanyId, Details = m.DeliveryCompanyName, Enabled = m.Enabled })
                .ToList();
        }

        public IEnumerable<EntityCompacted> GetIssueRegistrationTypes()
        {
            return
                 context.IssueRegistrationTypes.AsNoTracking()
                .Select(m => new EntityCompacted { Id = m.IssueRegistrationTypeId, Details = m.IssueRegistrationTypeName })
                .ToList();
        }

        public IEnumerable<EntityCompacted> GetVenuesCompacted(int? stateId = null)
        {
            var venues = (IQueryable<SettlementVenue>) context.SettlementVenues.AsNoTracking();
            if (stateId != null)
            {
                venues = venues.Where(x => x.StateId == stateId);
            }

            return
                 venues
                .Select(x=> new {x.StateId, x.State.StateName, x.VenueName,x.SettlementVenueId, x.Suburb,x.PostCode,x.StreetAddress})
                .OrderBy(o => o.VenueName)
                .ToList()
                .Select(m2 => new EntityCompacted
                {
                    Id = m2.SettlementVenueId,
                    Details = stateId == null ? EntityHelper.FormatDetailsStringWithComma(m2.VenueName,m2.StateName) : m2.VenueName,
                    RelatedId = m2.StateId,
                    RelatedDetail = Common.EntityHelper.FormVenueAddressDetails(m2.StreetAddress,m2.Suburb,m2.StateName,m2.PostCode) })
                .ToList();
        }

        public IEnumerable<EntityCompacted> GetVenuesForMatterCompacted(int matterId)
        {
            return context.Matters.Where(m => m.MatterId == matterId)
                                    .Select(v => v.SettlementSchedule.SettlementScheduleVenues)
                                    .FirstOrDefault()
                                    .Select(v => new EntityCompacted()
                                    {
                                        Id = v.SettlementScheduleVenueId,
                                        Details = v.SettlementVenueId.HasValue ? v.SettlementVenue.VenueName?.Trim() : v.RG_Venue?.Trim()
                                    })
                                    .ToList();
        }

        public IEnumerable<EntityCompacted> GetPexaWorkSpacesCompacted()
        {
            return context.PexaWorkspaces
                .Select(x => new EntityCompacted { Id = x.PexaWorkspaceId, Details = x.PexaWorkspaceNo, Enabled = x.Enabled });
        }

        public IEnumerable<EntityCompacted> GetVenuesWithDetail(int? stateId = null)
        {
            var venues = (IQueryable<SettlementVenue>)context.SettlementVenues.AsNoTracking();
            if (stateId != null)
            {
                venues = venues.Where(x => x.StateId == stateId);
            }
              
            return
                 venues                
                .Select(x => new { x.State.StateName, x.VenueName, x.SettlementVenueId, x.Suburb, x.PostCode, x.StreetAddress })
                .OrderBy(o=>o.StateName).ThenBy(o => o.VenueName)
                .ToList()
                .Select(m2 => new EntityCompacted
                {
                    Id = m2.SettlementVenueId,
                    Details = m2.VenueName,
                    RelatedDetail = m2.StateName,
                    ConcatenatedDetail = m2.VenueName + " " + m2.StateName
                })
                .ToList();
        }

        public IEnumerable<EntityCompacted> GetSettlementClerksCompacted()
        {
            return
                 context.SettlementClerks.AsNoTracking()
                .OrderBy(o => o.State.StateName).ThenBy(o=> o.Lastname).ThenBy(o => o.Firstname)
                .ToList()
                .Select(m => new EntityCompacted
                {
                    Id = m.SettlementClerkId,
                    Details = EntityHelper.GetFullName(m.Lastname,m.Firstname),
                    RelatedDetail = m.State.StateName,
                    ConcatenatedDetail = EntityHelper.GetFullName(m.Lastname, m.Firstname) + " " + m.State.StateName
                })
                .ToList();
        }

        public IEnumerable<EntityCompacted> GetComponentsCompacted()
        {
            return
                 context.WFComponents.AsNoTracking()
                .Where(s => s.Enabled == true)
                .OrderBy(o => o.WFComponentName)
                .Select(m2 => new EntityCompacted { Id = m2.WFComponentId, Details = m2.WFComponentName })
                .ToList();
        }

        public IEnumerable<EntityCompacted> GetLoanTypesCompacted()
        {
            return
                context.LoanTypes.Select(x => new EntityCompacted { Id = x.LoanTypeId, Details = x.LoanTypeName }).ToList();
        }
        public IEnumerable<EntityCompacted> GetWFComponentsCompacted()
        {
            return
                 context.WFComponents.AsNoTracking()
                .OrderBy(o => o.WFComponentName)
                .Select(m2 => new EntityCompacted { Id = m2.WFComponentId, Details = m2.WFComponentName, Enabled = m2.Enabled })
                .ToList();
        }

        public IEnumerable<EntityCompacted> GetReasonsCompacted(int reasonGroupType, int lenderId)
        {
            return
                  context.Reasons.AsNoTracking()
                 .Where(s => s.ReasonGroupTypeId == reasonGroupType && s.Enabled == true && (s.LenderId == lenderId || !s.LenderId.HasValue))
                 .OrderBy(o => o.ReasonTxt)
                 .Select(m2 => new EntityCompacted { Id = m2.ReasonId, Details = m2.ReasonTxt })
                 .ToList();
        }
        public IEnumerable<EntityCompacted> GetReasonsCompacted(int reasonGroupType)
        {
            return
                  context.Reasons.AsNoTracking()
                 .Where(s => s.ReasonGroupTypeId == reasonGroupType && s.Enabled == true)
                 .OrderBy(o => o.ReasonTxt)
                 .Select(m2 => new EntityCompacted { Id = m2.ReasonId, Details = m2.ReasonTxt })
                 .ToList();
        }
        public IEnumerable<EntityCompacted> GetReasonGroupsCompacted()
        {
            return
                  context.ReasonGroupTypes.AsNoTracking()
                 .OrderBy(o => o.ReasonGroupTypeName)
                 .Select(m2 => new EntityCompacted { Id = m2.ReasonGroupTypeId, Details = m2.ReasonGroupTypeName })
                 .ToList();
        }

        public IEnumerable<EntityCompacted> GetIssueReasonsCompacted()
        {
            return
                 context.Reasons.AsNoTracking()
                .Where(s => s.ReasonGroupTypeId == (int)Enums.ReasonGroupTypeEnum.Issue && s.Enabled == true)
                .OrderBy(o => o.ReasonTxt)
                .Select(m2 => new EntityCompacted { Id = m2.ReasonId, Details = m2.ReasonTxt })
                .ToList();
        }

        public IEnumerable<EntityCompacted> GetIssueActionsCompacted()
        {
            return
                 context.IssueActions.AsNoTracking()
                .Where(s => s.Enabled == true)
                .OrderBy(o => o.ActionText)
                .Select(m2 => new EntityCompacted { Id = m2.IssueActionId, Details = m2.ActionText })
                .ToList();
        }
        public IEnumerable<EntityCompacted> GetNextActionStepsCompacted()
        {
            return
                 context.NextActionSteps.AsNoTracking()
                .OrderBy(o => o.NextActionStepName)
                .Select(m2 => new EntityCompacted { Id = m2.NextActionStepId, Details = m2.NextActionStepName })
                .ToList();
        }


        public IEnumerable<EntityCompacted> GetEmailTriggerTypesCompacted()
        {
            return
                 context.MilestoneEmailTriggerTypes.AsNoTracking()
                .OrderBy(o => o.MilestoneEmailTriggerTypeName)
                .Select(m2 => new EntityCompacted { Id = m2.MilestoneEmailTriggerTypeId, Details = m2.MilestoneEmailTriggerTypeDesc })
                .ToList();
        }

        public IEnumerable<EntityCompacted> GetDueDateFormulaTypesCompacted()
        {
            return
                 context.WFComponentDueDateFormulas.AsNoTracking()
                .OrderBy(o => o.WFComponentDueDateFormulaName)
                .Select(m2 => new EntityCompacted { Id = m2.WFComponentDueDateFormulaId, Details = m2.WFComponentDueDateFormulaName })
                .ToList();
        }
        public IEnumerable<EntityCompacted> GetDueTimeFormulaTypesCompacted()
        {
            return
                context.WFComponentDueTimeFormulas.AsNoTracking()
                .Select(m => new EntityCompacted { Id = m.WFComponentDueTimeFormulaId, Details = m.WFComponentDueTimeFormulaDesc })
                .ToList();
        }
        public IEnumerable<EntityCompacted> GetPrecedentTypesCompacted()
        {
            return
                 context.PrecedentTypes.AsNoTracking()
                .OrderBy(o => o.PrecedentTypeName)
                .Select(m2 => new EntityCompacted { Id = m2.PrecedentTypeId, Details = m2.PrecedentTypeName })
                .ToList();
        }

        public IEnumerable<EntityCompacted> GetPrecedentOwnerTypesCompacted()
        {
            return
                 context.PrecedentOwnerTypes.AsNoTracking()
                .OrderBy(o => o.PrecedentOwnerTypeName)
                .Select(m2 => new EntityCompacted { Id = m2.PrecedentOwnerTypeId, Details = m2.PrecedentOwnerTypeName })
                .ToList();
        }

        public IEnumerable<EntityCompacted> GetPrecedentStatusTypesCompacted()
        {
            return
                 context.PrecedentStatusTypes.AsNoTracking()
                .OrderBy(o => o.PrecedentStatusTypeName)
                .Select(m2 => new EntityCompacted { Id = m2.PrecedentStatusTypeId, Details = m2.PrecedentStatusTypeName })
                .ToList();
        }

        public IEnumerable<EntityCompacted> GetTaskAllocationTypesCompacted(bool includeUser = true)
        {
            return context.TaskAllocTypes.AsNoTracking()
                .Where(x=> includeUser || x.TaskAllocTypeId != (int)Enums.TaskAllocationTypeEnum.User )
                .OrderBy(x=> x.TaskAllocTypeName)
                .Select(m => new EntityCompacted
                {
                    Id = m.TaskAllocTypeId, Details = m.TaskAllocTypeName
                }).ToList();
        }

        public IEnumerable<EntityCompacted> GetUsersCompacted(bool isExternal)
        {
            return
                 context.Users.AsNoTracking()
                .Where(s => s.Enabled == true && s.UserType.IsExternal == isExternal)
                .OrderBy(o => o.Firstname).ThenBy(o => o.Lastname)
                .ToList()
                .Select(m => new EntityCompacted
                {
                    Id = m.UserId,
                    Details = m.Fullname
                })
                .ToList();
        }
        //For pexa convos hub: get all file owner users (plus that user themselves so it doesn't error out)
        public IEnumerable<EntityCompacted> GetFileOwnersCompacted(int userId)
        {
            return
                 context.Users.AsNoTracking()
                .Where(s => s.Enabled == true && s.UserType.IsExternal == false && (s.UserId == userId || s.Matters.Any()))
                .OrderBy(o => o.Firstname).ThenBy(o => o.Lastname)
                .ToList()
                .Select(m => new EntityCompacted
                {
                    Id = m.UserId,
                    Details = m.Fullname
                })
                .ToList();
        }
        public IEnumerable<UserCompacted> GetCompactUsersCompacted(bool isExternal)
        {
            return
                 context.Users.AsNoTracking()
                .Where(s => s.Enabled == true && s.UserType.IsExternal == isExternal)
                .OrderBy(o => o.Firstname).ThenBy(o => o.Lastname)
                .ToList()
                .Select(m => new UserCompacted
                {
                    Id = m.UserId,
                    Details = m.Fullname,
                    WFH = m.WorkingFromHome,
                })
                .ToList();
        }

        public IEnumerable<EntityCompacted> GetAllUsersCompacted()
        {
            return
                 context.Users.AsNoTracking()
                .OrderBy(o => o.Lastname).ThenBy(o => o.Firstname)
                .ToList()
                .Select(m => new EntityCompacted
                {
                    Id = m.UserId,
                    Details = m.Fullname,
                    Enabled = m.Enabled
                })
                .ToList();
        }

        /// <summary>
        /// Returns either 1 item for the Consignment Type OR the full list of Categories for General consignment
        /// </summary>
        /// <param name="consignmentTypeId"></param>
        /// <returns></returns>
        public IEnumerable<EntityCompacted> GetConsignmentCategoriesCompacted(int consignmentTypeId)
        {
            var consignmentCategories = context.ConsignmentCategories.Where(x => x.ConsignmentCategoryId == consignmentTypeId && x.SystemUse == true);
            if (consignmentCategories == null || !consignmentCategories.Any())
            {
                consignmentCategories = context.ConsignmentCategories;
            }

            var categories =
             consignmentCategories.Select(m2 => new EntityCompacted
                {
                Id = m2.ConsignmentCategoryId,
                Details = m2.ConsignmentCategoryName,
                IsChecked = m2.SystemUse ?? false }
            ).ToList();

            // Add hardcoded category if - Archive. -- Ideally should have new ConsignmentTypeCategory table matching in Refs - did not want new model so like this for now.
            if (consignmentTypeId == (int)Enums.ConsignmentTypeEnum.Archive)
            {
                int? archiveMSACustodianId = context.ConsignmentCategories.Where(n => n.ConsignmentCategoryName.Contains("Archive MSA Custodian")).Select(x=>x.ConsignmentCategoryId).FirstOrDefault();

                if (archiveMSACustodianId.HasValue) {
                    categories.Add(new EntityCompacted
                    {
                        Id = archiveMSACustodianId.Value,
                        Details = "Archive MSA Custodian",
                        IsChecked = false
                    });
                }


                int? MNPId = context.ConsignmentCategories.Where(n => n.ConsignmentCategoryName == "MNP").Select(x => x.ConsignmentCategoryId).FirstOrDefault();
                if (MNPId.HasValue)
                {
                    categories.Add(new EntityCompacted
                    {
                        Id = MNPId.Value,
                        Details = "MNP",
                        IsChecked = false
                    });
                }
            }


            return categories;
        }

        public IEnumerable<EntityCompacted> GetConsignmentStatusesCompacted()
        {
            var statuses =
                 context.ConsignmentStatusTypes.AsNoTracking()
                .Select(m2 => new EntityCompacted { Id = m2.ConsignmentStatusTypeId, Details = m2.ConsignmentStatusTypeName })
                .ToList();

            statuses.Insert(0, new EntityCompacted { Id = DomainConstants.AnySelectionId, Details = "Select All Statuses" });

            return statuses;
        }

        public IEnumerable<EntityCompacted> GetOfficesForFilterCompacted()
        {
            var offices =
                context.Offices.AsNoTracking()
               .Select(m2 => new EntityCompacted { Id = m2.OfficeId, Details = m2.OfficeName })
               .ToList();

            offices.Insert(0, new EntityCompacted { Id = DomainConstants.AnySelectionId, Details = "Select All Offices" });

            return offices;
        }

        public IEnumerable<EntityCompacted> GetTransactionTypesCompacted()
        {
            var tranTypes =
                context.TransactionTypes.AsNoTracking()
               .Select(m2 => new EntityCompacted { Id = m2.TransactionTypeId, Details = m2.TransactionTypeName })
               .ToList();

            return tranTypes;
        }

        public IEnumerable<EntityCompacted> GetTransactionDirectionTypesCompacted()
        {
            var tranDirectionTypes =
                context.TransactionDirectionTypes.AsNoTracking()
               .Select(m2 => new EntityCompacted { Id = m2.TransactionDirectionTypeId, Details = m2.TransactionDirectionTypeName })
               .ToList();

            return tranDirectionTypes;
        }

        public IEnumerable<EntityCompacted> GetPayableByTypesCompacted()
        {
            var tranTypes =
                context.PayableTypes.AsNoTracking()
               .Select(m2 => new EntityCompacted { Id = m2.PayableTypeId, Details = m2.PayableTypeName })
               .ToList();

            return tranTypes;
        }

        /// <summary>
        /// Hard coded restriction to Lender and Borrower
        /// </summary>
        /// <returns></returns>
        public IEnumerable<EntityCompacted> GetPayableByTypesRestricted()
        {
            var tranTypes =
                context.PayableTypes.AsNoTracking()
                .Where(x=> x.PayableTypeId == (int)Enums.PayableTypeEnum.Lender || x.PayableTypeId == (int) Enums.PayableTypeEnum.Borrower)
               .Select(m2 => new EntityCompacted { Id = m2.PayableTypeId, Details = m2.PayableTypeName })
               .ToList();

            return tranTypes;
        }
        public IEnumerable<MatterCustomEntities.MatterFastRefiDetailView> GetPayableTypeNamesFastRefi(int matterId)
        {
            return context.MatterFastRefiDetails.Where(m => m.MatterId == matterId).Select(x => new MatterCustomEntities.MatterFastRefiDetailView() { MatterId = x.MatterId, ContactEmail = x.ContactEmail, ContactFax = x.ContactFax, EFT_AccountName = x.EFT_AccountName, EFT_AccountNo = x.EFT_AccountNo, EFT_BSB = x.EFT_BSB })
                .ToList().GroupBy(d => d.EFT_AccountName).Select(s => s.First()).ToList();
        }
        public IEnumerable<EntityCompacted> GetPayableTypeNamesExpanded(int matterId)
        {
            var mv = context.Matters.Where(m => m.MatterId == matterId)
                            .Select(m => new { m.Lender.LenderName, m.MortMgr.MortMgrName, m.MatterParties })
                            .FirstOrDefault();

            List<EntityCompacted> retList = new List<EntityCompacted>();

            if (mv != null)
            {
                retList.Add(new EntityCompacted() { Id = 1, Details = mv.LenderName, ConcatenatedDetail = mv.LenderName + " [Lender]", RelatedDetail = "Lender", RelatedId = 2 });
                if (mv.MortMgrName != null)
                    retList.Add(new EntityCompacted() { Id = 2, Details = mv.MortMgrName, ConcatenatedDetail = mv.MortMgrName + " [Mort Mgr]", RelatedDetail = "MortMgr", RelatedId = 3 });
            }
            retList.Add(new EntityCompacted() { Id = 3, Details = "MSA National", ConcatenatedDetail = "MSA National", RelatedDetail="MSA", RelatedId = 1 });

            if (mv != null && mv.MatterParties.Count(x => x.PartyTypeId == (int)Enums.PartyTypeEnum.Borrower || x.PartyTypeId == (int)Enums.PartyTypeEnum.BorrowerAndMortgagor) > 0)
                retList.Add(new EntityCompacted() { Id = 4, Details = EntityHelper.GetBorrowersFromParty(mv.MatterParties), ConcatenatedDetail = EntityHelper.GetBorrowersFromParty(mv.MatterParties) + " [Borrower]", RelatedDetail = "Borrower", RelatedId = 4 });

            return retList;
        }

        public IEnumerable<EntityCompacted> GetPayableTypes()
        {
            var tranTypes =
                context.PayableTypes.AsNoTracking()
               .Select(m2 => new EntityCompacted { Id = m2.PayableTypeId, Details = m2.PayableTypeDesc })
               .ToList();

            return tranTypes;
        }

        public IEnumerable<EntityCompacted> GetTrusteeCompaniesCompacted()
        {
            return context.ConsignmentTrustees.AsNoTracking()
                .Select(s => new EntityCompacted { Id = s.ConsignmentTrusteeId, Details = s.ConsignmentTrusteeName }).ToList();
        }

        public IEnumerable<string> GetAgentSuburbs()
        {
            return context.SettlementAgents.DistinctBy(x => x.Suburb)
                .Where(x => x.Suburb != null)
                .Select(x => x.Suburb).ToList();
        }
        public IEnumerable<EntityCompacted> GetParentMortMgrsCompacted()
        {
            return context.MortMgrs.Where(x => x.Enabled && x.IsParent).Select(x => new EntityCompacted { Id = x.MortMgrId, Details = x.MortMgrName });
        }
        public IEnumerable<EntityCompacted> GetMortMgrFundingChannelTypesForLender(int lenderId)
        {
            return context.MortMgrValidFundingChannelTypes.Where(x => x.LenderId == lenderId).Select(x => new EntityCompacted { Id = x.MortMgrFundingChannelType.MortMgrFundingChannelTypeId, Details = x.MortMgrFundingChannelType.MortMgrFundingChannelTypeName });
        }
        public IEnumerable<EntityCompacted> GetChildMortMgrsCompacted()
        {
            return context.MortMgrs.Where(x => x.Enabled && !x.IsParent).Select(x => new EntityCompacted { Id = x.MortMgrId, Details = x.MortMgrName });
        }
        public IEnumerable<EntityCompacted> GetMortMgrTypesCompacted()
        {
            return context.MortMgrTypes.Select(x => new EntityCompacted() { Id = x.MortMgrTypeId, Details= x.MortMgrTypeName });
        }
        public IEnumerable<EntityCompacted> GetFeeListCompacted(bool inclNoFee)
        {
            List<EntityCompacted> retList = new List<EntityCompacted>();


            retList = context.Fees.AsNoTracking().Where(f => f.Enabled)
                .OrderBy(f => f.FeeName)
                .Select(s => new EntityCompacted { Id = s.FeeId, Details = s.FeeName }).ToList();

            if (inclNoFee)
                retList.Add(new EntityCompacted() { Id = -1, Details = "-- No Fee --"});

            return retList;
        }

        public IEnumerable<EntityCompacted> GetDisbursementDescriptionsCompacted(int matterId, bool isFunding)
        {
            var mt = context.Matters.AsNoTracking().FirstOrDefault(m => m.MatterId == matterId);
            bool isFastRefi = context.MatterMatterTypes.Any(x => x.MatterId == matterId && x.MatterTypeId == (int)Enums.MatterTypeEnum.FastRefinance || x.MatterTypeId == (int)Enums.MatterTypeEnum.RapidRefinance);
            var states = mt.MatterSecurities.Select(x => x.StateId).Distinct().ToList();

            List<EntityCompacted> retList = new List<EntityCompacted>();

            if (mt.MortMgrId.HasValue)
            {
                var mdos = context.MortMgrDisbursementOptions.Where(m => m.MortMgrId == mt.MortMgrId.Value && m.IsFunding==isFunding && m.IsVisible).OrderBy(o => o.DisplayOrder);
                
                foreach (var mdo in mdos)
                    retList.Add(new EntityCompacted() { Id = mdo.MortMgrDisbursementOptionId, Details = mdo.DisbursementOption, RelatedDetail = "M" });

                if (!retList.Any())
                {
                    var ldos = context.LenderDisbursementOptions.Where(m => m.LenderId == mt.LenderId && (m.MortMgrId == null || m.MortMgrId == mt.MortMgrId) && m.IsFunding == isFunding && m.IsVisible && (m.StateId == null || states.Contains(m.StateId.Value))).OrderBy(o => o.DisplayOrder);
                    if (!isFastRefi)
                    {
                        ldos = ldos.Where(f => f.FastRefiSpecific == false).OrderBy(o => o.DisplayOrder);
                    }
                    foreach (var ldo in ldos)
                    {
                        retList.Add(new EntityCompacted() { Id = ldo.LenderDisbursementOptionId, Details = ldo.DisbursementOption, RelatedDetail = "L" });
                    }
                }
            }

            if (!retList.Any())
            {
                var ldos = context.LenderDisbursementOptions.Where(m => m.LenderId == mt.LenderId && m.MortMgrId == null && m.IsFunding == isFunding && m.IsVisible && (m.StateId == null || states.Contains(m.StateId.Value))).OrderBy(o => o.DisplayOrder);
                foreach (var ldo in ldos)
                    retList.Add(new EntityCompacted() { Id = ldo.LenderDisbursementOptionId, Details = ldo.DisbursementOption, RelatedDetail = "L" });
                if (!isFastRefi)
                {
                    ldos = ldos.Where(f => f.FastRefiSpecific == false).OrderBy(o => o.DisplayOrder);
                }
            }

            retList.Add(new EntityCompacted(-1, "Linked Matter"));
            return retList;
        }
        public IEnumerable<EntityCompacted> GetFundingRequestStatusCompacted()
        {
            return
                context.FundingRequestStatusTypes.AsNoTracking()
               .Select(x => new EntityCompacted { Id = x.FundingRequestStatusTypeId, Details = x.FundingRequestStatusTypeName })
               .ToList();
        }
    }
}
