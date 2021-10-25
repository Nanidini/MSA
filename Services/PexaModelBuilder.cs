using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Slick_Domain.Services
{
    public class PexaModelsBuilder
    {
        public PexaModelsBuilder()
        {

        }
        public DateTime ForceAddBusinessDays(DateTime time, int addDays)
        {
            DateTime dateTime = DateTime.Now;
            while(addDays > 0)
            {
                switch (dateTime)
                {
                    case DateTime s when s.DayOfWeek == DayOfWeek.Sunday | s.DayOfWeek == DayOfWeek.Saturday:
                        dateTime.AddDays(1);
                        break;
                    default:
                        dateTime.AddDays(1);
                        addDays--;
                        break;
                }
            }

            return dateTime;
        }
        public Entities.SchemaV2.WorkspaceCreationRequestType BuildWorkspaceCreationRequestTypeModel(int matterId)
        {
            DateTime settlementDateTime = ForceAddBusinessDays(DateTime.Now, 10);

            //using (var uow = new UnitOfWork())
            //{
            //    settlementDateTime = uow.Context.Matters.First(m => m.MatterId == matterId).SettlementSchedule.SettlementDate;
            //}
            Entities.SchemaV2.WorkspaceCreationRequestType wsCrRqTyp = new Entities.SchemaV2.WorkspaceCreationRequestType()
            {
                FinancialSettlement = "",
                SettlementDate = settlementDateTime,
                SettlementDateAndTime = settlementDateTime,
                Jurisdiction = "",
                LandTitleDetails = GetWorkspaceCreationRequestTypeLandTitleDetails(matterId),
//                SubscriberId = GetPexaSubscriberId(matterId),
                RequestLandTitleData = "",
                Role = "",
                SubscriberReference = matterId.ToString(),
                ProjectId = "",
                WorkgroupId = "",
                ProjectName = "",
                SettlementDateAndTimeValue = settlementDateTime,
                SettlementDateAndTimeValueSpecified = true,
                SettlementDateValue = settlementDateTime,
                SettlementDateValueSpecified = true, 
                ParticipantSettlementAcceptanceStatus = "",

            };

            wsCrRqTyp.PartyDetails.Concat(GetPartyDetails(matterId));

            return null;
        }

        public ICollection<Slick_Domain.Entities.SchemaV2.WorkspaceCreationRequestTypePartyDetailsParty> GetPartyDetails(int matterid)
        {

            Slick_Domain.Entities.SchemaV2.WorkspaceCreationRequestTypePartyDetailsParty workspaceCreationRequestTypePartyDetailsParty = null;



            return null;
        }

        public string GetPexaSubscriberId(int matterid)
        {

            return string.Empty;
        }

        public Entities.SchemaV2.WorkspaceCreationRequestTypeLandTitleDetails GetWorkspaceCreationRequestTypeLandTitleDetails(int matterid)
        {
            Entities.SchemaV2.WorkspaceCreationRequestTypeLandTitleDetails workspaceCreationRequestTypeLandTitleDetails = new Entities.SchemaV2.WorkspaceCreationRequestTypeLandTitleDetails()
            {

            };


            return workspaceCreationRequestTypeLandTitleDetails;
        }
    }
}
