using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using Slick_Domain.Entities;
using Slick_Domain.Models;
using Slick_Domain.Enums;
using Slick_Domain.Common;
using System.Diagnostics;
using System.Net.Mail;
using System.Reflection;
using System.Runtime.InteropServices;
using RestSharp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Web.Script.Serialization;
using System.Threading;


namespace Slick_Domain.Services
{
    public class SmartVidService
    {
        public class SmartVideoRequest
        {
            public string projectkey;
            public List<SmartVideoRequestVariable> variables;

        }
        public class AFSHSmartVideoRequest
        {
            public string projectkey;
            public List<AFSHSmartVideoRequestVariable> variables;

        }
        public class ResimacSmartVideoRequest
        {
            public string projectkey;
            public List<ResimacSmartVideoRequestVariable> variables;

        }
        public class AFSHCombinationView
        {
            public string SceneName { get; set; }
            public string Seg1 { get; set; }
            public string Seg2 { get; set; }
            public string Seg3 { get; set; }
            public string Seg4 { get; set; }
            public string Seg5 { get; set; }
        }


      



        public class DeprecatedSmartVideoRequestVariable
        {
            public string id { get; set; }
            public string name_text { get; set; }
            public string name_audio { get; set; }
            public string loan_amount_text { get; set; }
            public string loan_amount_audio { get; set; }
            public string monthly_payment_text { get; set; }
            public string monthly_payment_audio { get; set; }
            public string broker_text { get; set; }
            public string broker_audio { get; set; }
            public string phone_number { get; set; }
            public string address_text { get; set; }
            public string insurance_text { get; set; }
            public string insurance_audio { get; set; }
            public string coborrower_text { get; set; }
            public string coborrower_audio { get; set; }
            public string outgoing_lender_text { get; set; }
            public string who_signs_audio { get; set; }
        }
        public class SmartVideoRequestVariable
        {
            public string id { get; set; } //xxxxxxx
            public string name_text { get; set; } //Zoe
            public string name_audio { get; set; } //https://assets.personalisedvideolab.com/audio/voices/matto/Zoe.mp3
            public string loantype_audio { get; set; } //loan.mp3
            public string loantype_image { get; set; } //loan_image.png
            public string loanamount_audio { get; set; } //https://sydney.smartvideoaustralia.com.au/purse/112512.23
            public string loanamount_text { get; set; } //$112,512.23
            public string day_text { get; set; } //1
            public string month_text { get; set; } //January
            public string date_audio { get; set; } //Jan1.mp3
            public string email_text { get; set; } //mynameemail@emailsite.com
            public string address_text { get; set; } //3a/221 Spring Road, Brisbane\nQueensland 4000
            public string date_text { get; set; } //1st May 2019
            public string repaymentcycle_audio { get; set; } //weekly.mp3
            public string repaymentcycle_text { get; set; } //weekly
            public string firstrepayment_video { get; set; } //firstrepayment_fund.mp4
            public string repaymentdate_audio { get; set; } //May3.mp3
            public string repaymentdate_video { get; set; } //repaymentamount.mp4, repaymentamount_fund.mp4
            public string repaymentamount_video { get; set; }
            public string repaymentmethod_video { get; set; } //repaymentmethod_yes.mp3
            public string bsb1_text { get; set; } //BSB:033-044
            public string acct1_text { get; set; } //ACCT: 98989898
            public string bsb2_text { get; set; } //BSB:033-044
            public string acct2_text { get; set; } //ACCT: 98989898
        }
        public class SmartVideoResponse
        {
            //{"Response":200,"Receipt": 618817,"Count":"2","Tokens": {"asdasdsaddxvc":"e4940ed0904347609ee01b1ac5c5bbd7","1232132132":"8715017e34294607af140708cebfec69"}}
            public string Response { get; set; }
            public string Receipt { get; set; }
            public string Count { get; set; }
            public List<Tokens> Tokens { get; set; }
        }
        public class Tokens
        {
            public string smartvidId { get; set; }
            public string token { get; set; }
        }

        public class AFSHSmartVideoRequestVariable
        {
            public string id { get; set; } //xxxxxxx
            public string render_scene { get; set; }
            public string name_text { get; set; } //Zoe
            public string fullname_text { get; set; }
            public string name_audio { get; set; }
            public string aggregator_text { get; set; }
            public string aggregator_audio { get; set; }
            public string day_text { get; set; }
            public string month_text { get; set; }
            public string date_audio { get; set; }
            public string loanamount_text { get; set; }
            public string address_text { get; set; }
            public string payfrequency_audio { get; set; }
            public string acct_text { get; set; }
            public string bsb_text { get; set; }
            public string phone_text { get; set; }
            public string phone_audio { get; set; }
            public string dob_video { get; set; }
            public string dob_text { get; set; }
        }

        public class ResimacSmartVideoRequestVariable
        {
            public string id { get; set; } //xxxxxxx
            public string name_text { get; set; }
            public string name_audio { get; set; }
            public string loan_video { get; set; }
            public string doc_video { get; set; }
            public string insurance_video { get; set; }
        }


        public static List<AFSHCombinationView> AFSHSceneMatrix = new List<AFSHCombinationView>()
        {
            new AFSHCombinationView(){ SceneName="compileA_scene", Seg1="seg1", Seg2="seg2a", Seg3="seg3a", Seg4="seg4a", Seg5="seg5" },
            new AFSHCombinationView(){ SceneName="compileB_scene", Seg1="seg1", Seg2="seg2b", Seg3="seg3a", Seg4="seg4a", Seg5="seg5" },
            new AFSHCombinationView(){ SceneName="compileC_scene", Seg1="seg1", Seg2="seg2a", Seg3="seg3a", Seg4="seg4b", Seg5="seg5" },
            new AFSHCombinationView(){ SceneName="compileD_scene", Seg1="seg1", Seg2="seg2b", Seg3="seg3a", Seg4="seg4b", Seg5="seg5" },

            new AFSHCombinationView(){ SceneName="compileE_scene", Seg1="seg1", Seg2="seg2a", Seg3="seg3b", Seg4="seg4a", Seg5="seg5" },
            new AFSHCombinationView(){ SceneName="compileF_scene", Seg1="seg1", Seg2="seg2b", Seg3="seg3b", Seg4="seg4a", Seg5="seg5" },
            new AFSHCombinationView(){ SceneName="compileG_scene", Seg1="seg1", Seg2="seg2a", Seg3="seg3b", Seg4="seg4b", Seg5="seg5" },
            new AFSHCombinationView(){ SceneName="compileH_scene", Seg1="seg1", Seg2="seg2b", Seg3="seg3b", Seg4="seg4b", Seg5="seg5" },

            new AFSHCombinationView(){ SceneName="compileI_scene", Seg1="seg1", Seg2="seg2a", Seg3="seg3c", Seg4="seg4a", Seg5="seg5" },
            new AFSHCombinationView(){ SceneName="compileJ_scene", Seg1="seg1", Seg2="seg2b", Seg3="seg3c", Seg4="seg4a", Seg5="seg5" },
            new AFSHCombinationView(){ SceneName="compileK_scene", Seg1="seg1", Seg2="seg2a", Seg3="seg3c", Seg4="seg4b", Seg5="seg5" },
            new AFSHCombinationView(){ SceneName="compileL_scene", Seg1="seg1", Seg2="seg2b", Seg3="seg3c", Seg4="seg4b", Seg5="seg5" }
        };

        public static string GetRenderSceneForAFSHSmartVid(int matterId, SlickContext context, bool ddrHeld)
        {
            //Look the logic for this is pretty loose and only defined in a document sent by Rodd - this determines which of the scenes this video will get
            //based on the logic in AFSH Project Setup Specs_6.10.20 (002).docx as sent by Rodd. It's pretty nasty. 


            var mtDetails = context.Matters.Select(m =>
            new
            {
                m.MatterId,
                LoanTypes = m.MatterLoanAccounts.Select(l => l.LoanDescription).ToList(),
                m.IsConstruction,
            }
            ).FirstOrDefault(m => m.MatterId == matterId);

            string seg1 = "", seg2 ="", seg3 ="", seg4 ="", seg5 = "";

            seg1 = "seg1";
            seg2 = mtDetails.IsConstruction ?? false ? "seg2b" : "seg2a";

            var loantypes = mtDetails.LoanTypes.Distinct().ToList();

            for(int i = 0;i<loantypes.Count();i++)
            {
                var type = loantypes[i];
                if(type.ToUpper().Contains("FIXED") && !type.ToUpper().Contains("IO"))
                {
                    loantypes[i] = "FIXED P&I";
                }
            }
            
            bool hasPandI = loantypes.Any(x => x.ToUpper().Contains("P&I"));
            bool hasIO = loantypes.Any(x => x.ToUpper().Contains("IO"));
            bool hasFixed = loantypes.Any(x => x.ToUpper().Contains("FIXED"));
            
            if (hasPandI && !hasIO)
            {
                seg3 = "seg3a";
            }
            else if (hasIO && !hasPandI)
            {
                seg3 = "seg3b";
            }
            else
            {
                seg3 = "seg3c";
            }

            seg4 = ddrHeld ? "seg4a" : "seg4b";
            seg5 = "seg5";

            var matchScene = AFSHSceneMatrix.FirstOrDefault(s => s.Seg1 == seg1 && s.Seg2 == seg2 && s.Seg3 == seg3 && s.Seg4 == seg4 && s.Seg5 == seg5);

            if (matchScene == null)
            {
                return null;
            }
            else
            {
                return matchScene.SceneName;
            }

        }
        public static AFSHSmartVideoRequest CreateAFSHSmartVidRequest(int matterWFSmartVidId, SlickContext context, int matterPartyId)
        {
            var mwfComp = context.MatterWFSmartVidDetails.Where(x => x.MatterWFSmartVidDetailId == matterWFSmartVidId).Select(x => new { x.MatterWFComponentId, x.MatterWFComponent.MatterId }).FirstOrDefault();
            var mtDetails = context.Matters.Select(m => new
            {
                m.MatterId,
                m.SecondaryRefNo,
                m.LenderRefNo,
                m.MortMgr.MortMgrName,
                m.SettlementSchedule.SettlementDate,
                m.MortMgr.SmartVidMortMgrName,
                m.MortMgr.SmartVidMortMgrAudioName,
                m.MortMgr.SmartVidPhone,
                m.MortMgr.SmartVidMortMgrContactAudioName,
                ddr = m.MatterLoanAccounts.SelectMany(l => l.MatterLoanAccountDirectDebitDetails.Where(d=>d.FrequencyTypeId.HasValue).Select(d=>new { d.DirectDebitFrequencyType.DirectDebitFrequencyTypeName })).ToList()

            }).FirstOrDefault(m => m.MatterId == mwfComp.MatterId);

            Decimal swiftAmount = 0.0M;
            var dbSwift = context.MatterSwiftAmounts.Where(m => m.MatterId == mwfComp.MatterId).Select(x=>x.SwiftAmount).ToList();
            if (dbSwift.Any())
            {
                swiftAmount = dbSwift.FirstOrDefault();
            }
            else
            {
                //mainly a UAT relic - we didn't use to save the specific SWIFT amount variable, so some older AFSH UAT files don't have it, so best we can do is get sum of loan amounts
                //...*shouldn't* be an issue in production
                swiftAmount = context.MatterLoanAccounts.Where(x => x.MatterId == mwfComp.MatterId).Select(x => x.LoanAmount).Sum();
            }
            AFSHSmartVideoRequest request = new AFSHSmartVideoRequest() { variables = new List<AFSHSmartVideoRequestVariable>() };

            var forceDDR = context.MatterMatterTypes.Any(x => x.MatterId == mwfComp.MatterId && x.MatterTypeId == (int)MatterTypeEnum.ExistingSecurity);

            var parties = context.MatterParties.Where(m => m.MatterPartyId == matterPartyId).ToList();
            foreach (var mParty in parties)
            {
                var party = context.MatterWFSmartVidDetailParties.FirstOrDefault(m => m.MatterPartyId == matterPartyId && m.MatterWFSmartVidDetailId == matterWFSmartVidId && m.MatterParty.IsIndividual && m.MatterParty.PartyTypeId == (int)PartyTypeEnum.Borrower);
                AFSHSmartVideoRequestVariable requestVariable = new AFSHSmartVideoRequestVariable()
                {
                    id = "AFSH"+mtDetails.SecondaryRefNo.Replace(" ", "") + "_" + matterWFSmartVidId.ToString(),
                    render_scene = GetRenderSceneForAFSHSmartVid(mwfComp.MatterId, context, forceDDR || mtDetails.ddr.Any()),
                    name_text = CleanName(party.MatterPartyDisplayFirstname),
                    name_audio = $"https://assets.personalisedvideolab.com/audio/voices/natasha/" + CleanName(party.MatterPartyDisplayFirstname).Trim().Replace(" ", "") + ".mp3",
                    aggregator_audio = (mtDetails.SmartVidMortMgrAudioName ?? "Advantedge.mp3"),//(mtDetails.SmartVidMortMgrAudioName ?? ""),
                    aggregator_text = mtDetails.SmartVidMortMgrName ?? "Advantedge",
                    day_text = mtDetails.SettlementDate.ToString("dd"),
                    month_text = mtDetails.SettlementDate.ToString("MMM"),
                    date_audio = mtDetails.SettlementDate.ToString("MMM") + mtDetails.SettlementDate.Day.ToString() + ".mp3", //Jan1.mp3
                    loanamount_text = swiftAmount.ToString("c"),
                    fullname_text = (party.MatterParty.Firstname?.Trim() + " " + party.MatterPartyDisplayLastname?.Trim())?.Trim(),
                    address_text = party.MatterWFSmartVidDetail.DisplayPostalAddress,
                    dob_video="dobno.mp4",
                    phone_text = mtDetails.SmartVidPhone ?? "1300 300 989",
                    phone_audio = (mtDetails.SmartVidMortMgrContactAudioName ?? "1300 300 989.mp3"),
                    payfrequency_audio = mtDetails.ddr.Distinct().Count() == 1 ? mtDetails.ddr.FirstOrDefault().DirectDebitFrequencyTypeName + ".mp3" : null
                };

                request.variables.Add(requestVariable);

            }
            return request;

        }

        /// <summary>
        /// Populates a MatterSmartVidView based on details from mwfComponent view and matter. 
        /// </summary>
        /// <param name="mwfComponentView"></param>
        /// <returns></returns>
        public static MatterCustomEntities.MatterSmartVidView GetSmartVidDetails(MatterCustomEntities.MatterWFComponentView mwfComponentView, bool isHistoric = false)
        {

            MatterCustomEntities.MatterSmartVidView details = new MatterCustomEntities.MatterSmartVidView();

            details.FundingChannelTypeId = (int)FundingChannelTypeEnum.INSTO;

            details.PostalAddresses = new List<MatterCustomEntities.SmartVidPostalAddress>();

            details.DirectDebitDetails = new List<MatterCustomEntities.SmartVidDirectDebit>();

            using (var context = new SlickContext())
            {


                if (isHistoric)
                {
                    var historic = context.MatterWFSmartVidDetails.Where(m => m.MatterWFComponentId == mwfComponentView.MatterWFComponentId);
                    if (!historic.Any())
                    {
                        return null;
                    }
                    details.Borrowers = new List<MatterCustomEntities.SmartVidBorrower>();
                    details.PostalAddresses = new List<MatterCustomEntities.SmartVidPostalAddress>();
                    details.DirectDebitDetails = new List<MatterCustomEntities.SmartVidDirectDebit>();
                    int currNumber = 1;

                    //get details for the parties
                    foreach (var vid in historic)
                    {
                        if (vid.MatterWFSmartVidDetailParty == null)
                            break;

                        details.Borrowers.Add(new MatterCustomEntities.SmartVidBorrower()
                        {
                            Firstname = vid.MatterWFSmartVidDetailParty.MatterPartyDisplayFirstname,
                            Lastname = vid.MatterWFSmartVidDetailParty.MatterPartyDisplayLastname,
                            Email = vid.MatterWFSmartVidDetailParty.MatterPartySmartvidEmail,
                            Mobile = vid.MatterWFSmartVidDetailParty.MatterPartySmartvidMobile,
                            NameHasSpace = vid.MatterWFSmartVidDetailParty.MatterPartyDisplayFirstname.Contains(" "),
                            MatterPartyId = vid.MatterWFSmartVidDetailParty.MatterPartyId.Value,
                            PartyType = vid.MatterParty.PartyType.PartyTypeName
                        });
                        details.PostalAddresses.Add(new MatterCustomEntities.SmartVidPostalAddress()
                        {
                            Number = currNumber.ToString(),
                            PartyId = vid.MatterPartyId.Value,
                            Street = vid.DisplayPostalAddress //we don't store these separately so this'll have to do
                        });

                        details.FundingChannelTypeId = vid.FundingChannelTypeId;
                        currNumber++;
                    }

                    //if we've got debit details let's get them, unfortunately will only be for new ones after the update to actually store these.
                    foreach (var debit in historic.FirstOrDefault().MatterWFSmartVidDetailDirectDebits)
                    {
                        details.DirectDebitDetails.Add
                        (
                            new MatterCustomEntities.SmartVidDirectDebit()
                            {
                                LoanAccount = debit.LoanAccountNo,
                                Account = debit.AccountNo,
                                BSB = debit.BSB,
                                Frequency = debit.FrequencyTypeId
                            }
                        );
                    }

                    if (!details.DirectDebitDetails.Any())
                    {
                        var thisMatter = context.Matters.Where(m => m.MatterId == mwfComponentView.MatterId).FirstOrDefault();
                        foreach (var loanAccount in thisMatter.MatterLoanAccounts)
                        {
                            if (thisMatter.FundingChannelTypeId == (int)FundingChannelTypeEnum.Fund)
                            {
                                details.DirectDebitDetails.Add(new MatterCustomEntities.SmartVidDirectDebit
                                {
                                    LoanAccount = loanAccount.LoanAccountNo,
                                    Frequency = (int)DirectDebitFrequencyEnum.Monthly
                                });
                            }
                            else
                            {
                                details.DirectDebitDetails.Add(new MatterCustomEntities.SmartVidDirectDebit { LoanAccount = loanAccount.LoanAccountNo, Frequency = (int)DirectDebitFrequencyEnum.Weekly });
                            }
                        }
                        if (details.DirectDebitDetails.Any())
                        {
                            details.DirectDebitDetails.FirstOrDefault().BSB = historic.FirstOrDefault().DirectDebitBSB;
                            details.DirectDebitDetails.FirstOrDefault().Account = historic.FirstOrDefault().DirectDebitAcc;
                            details.DirectDebitDetails.FirstOrDefault().Frequency = historic.FirstOrDefault().DirectDebitFrequencyTypeId;

                            if (details.DirectDebitDetails.Count() > 1)
                            {
                                details.DirectDebitDetails.ElementAt(1).BSB = historic.ElementAt(1).DirectDebitBSB2;
                                details.DirectDebitDetails.ElementAt(1).Account = historic.ElementAt(1).DirectDebitAcc2;
                                details.DirectDebitDetails.ElementAt(1).Frequency = historic.ElementAt(1).DirectDebitFrequencyTypeId;

                            }

                        }

                    }

                    return details;
                }

                //first let's get the matter so we can work out what we need
                var matter = context.Matters.FirstOrDefault(m => m.MatterId == mwfComponentView.MatterId);
                if (matter.FundingChannelTypeId.HasValue)
                    details.FundingChannelTypeId = matter.FundingChannelTypeId.Value;


                //GET BORROWERS NAMES
                details.Borrowers = new List<MatterCustomEntities.SmartVidBorrower>();
                if (matter.MatterParties != null)
                {
                    foreach (var party in matter.MatterParties)
                    {
                        if (party.IsIndividual && (party.PartyTypeId == (int)Enums.PartyTypeEnum.Borrower || party.PartyTypeId == (int)Enums.PartyTypeEnum.BorrowerAndMortgagor || party.PartyTypeId == (int)Enums.PartyTypeEnum.Guarantor || party.PartyTypeId == (int)Enums.PartyTypeEnum.GuarantorAndMortgagor))
                        {
                            MatterCustomEntities.SmartVidBorrower borrowerDetails = new MatterCustomEntities.SmartVidBorrower
                            {
                                Firstname = CleanName(party.Firstname),
                                Lastname = party.Lastname,
                                Email = "",
                                PartyType = party.PartyType.PartyTypeName,
                                MatterPartyId = party.MatterPartyId,
                                Mobile = party.Mobile
                            };
                            if (party.Email != null) borrowerDetails.Email = party.Email;
                            details.Borrowers.Add(borrowerDetails);
                        }
                    }
                    foreach (var loanAccount in matter.MatterLoanAccounts)
                    {
                        if (matter.FundingChannelTypeId == (int)FundingChannelTypeEnum.Fund)
                        {
                            details.DirectDebitDetails.Add(new MatterCustomEntities.SmartVidDirectDebit
                            {
                                LoanAccount = loanAccount.LoanAccountNo,
                                Frequency = (int)DirectDebitFrequencyEnum.Monthly
                            });
                        }
                        else
                        {
                            details.DirectDebitDetails.Add(new MatterCustomEntities.SmartVidDirectDebit { LoanAccount = loanAccount.LoanAccountNo, Frequency = (int)DirectDebitFrequencyEnum.Weekly });
                        }
                    }
                }
                //GET LOAN ACCOUNTS FOR DIRECT DEBIT
                //GET POSTAL ADDRESS
                var securities = matter.MatterSecurities;
                List<int> matterPartyIds = matter.MatterParties.Where(i => i.IsIndividual).Select(m => m.MatterPartyId).ToList();
                int partyCount = 1;

                foreach (var partyId in matterPartyIds)
                {
                    var partyDetails = context.MatterParties.Where(x => x.MatterPartyId == partyId).Select(x => new { x.Firstname, x.Lastname }).FirstOrDefault();

                    if (context.MatterPartyOtherAddresses.Any(x => x.MatterPartyId == partyId))
                    {
                        var partyAdr = context.MatterPartyOtherAddresses.Where(x => x.MatterPartyId == partyId).FirstOrDefault();
                        details.PostalAddresses.Add(new MatterCustomEntities.SmartVidPostalAddress()
                        {
                            PartyName = partyDetails != null ? partyDetails.Firstname + " " + partyDetails.Lastname : $"Party {partyCount}",
                            PartyId = partyId,
                            Street = partyAdr.StreetAddress,
                            Suburb = partyAdr.Suburb,
                            Postcode = partyAdr.Postcode
                        });

                        //details.PostalAddress.Street = partyAdr.StreetAddress;
                        //details.PostalAddress.Suburb = partyAdr.Suburb;
                        //details.PostalAddress.Postcode = partyAdr.Postcode;
                    }
                    else if (securities != null)
                    {
                        if (securities.Count() == 1)
                        {
                            if
                            (
                                (securities.FirstOrDefault().MatterTypeId == (int)Enums.MatterTypeEnum.Refinance) ||
                                (securities.FirstOrDefault().MatterTypeId == (int)Enums.MatterTypeEnum.ExistingSecurity) ||
                                (securities.FirstOrDefault().MatterTypeId == (int)Enums.MatterTypeEnum.ClearTitle)
                            )
                            {

                                var partyAdr = context.MatterPartyOtherAddresses.Where(x => matterPartyIds.Contains(x.MatterPartyId)).FirstOrDefault();

                                details.PostalAddresses.Add(new MatterCustomEntities.SmartVidPostalAddress()
                                {
                                    PartyName = partyDetails != null ? partyDetails.Firstname + " " + partyDetails.Lastname : $"Party {partyCount}",
                                    PartyId = partyId,
                                    Street = context.MatterParties.FirstOrDefault(m => m.MatterPartyId == partyId)?.StreetAddress,
                                    Suburb = context.MatterParties.FirstOrDefault(m => m.MatterPartyId == partyId)?.Suburb,
                                    Postcode = context.MatterParties.FirstOrDefault(m => m.MatterPartyId == partyId)?.Postcode,
                                });
                                //details.PostalAddress.Street = matter.MatterParties.FirstOrDefault().StreetAddress;
                                //details.PostalAddress.Suburb = matter.MatterParties.FirstOrDefault().Suburb;
                                //details.PostalAddress.Postcode = matter.MatterParties.FirstOrDefault().Postcode;
                            }
                            else
                            {
                                //GET ANSWER FILE ADDRESS.
                                //CHECK IF INVESTMENT / OWNER OCCUPIED
                                //IF OWNER OCCUPIED: Security Address of purchased property


                                var partyAdr = context.MatterPartyOtherAddresses.Where(x => matterPartyIds.Contains(x.MatterPartyId)).FirstOrDefault();


                                details.PostalAddresses.Add(new MatterCustomEntities.SmartVidPostalAddress()
                                {
                                    PartyName = partyDetails != null ? partyDetails.Firstname + " " + partyDetails.Lastname : $"Party {partyCount}",
                                    PartyId = partyId,
                                    Street = "|| CHECK IF OWNER OCCUPIED: || " + securities.FirstOrDefault().StreetAddress,
                                    Suburb = securities.FirstOrDefault().Suburb,
                                    Postcode = securities.FirstOrDefault().PostCode
                                });





                                //details.PostalAddress.Street = "|| CHECK IF OWNER OCCUPIED: || " + securities.FirstOrDefault().StreetAddress;
                                //details.PostalAddress.Suburb = securities.FirstOrDefault().Suburb;
                                //details.PostalAddress.Postcode = securities.FirstOrDefault().PostCode;
                            }
                        }
                        else
                        {
                            bool hasPurchase = false;
                            MatterSecurity purchasedSec = null;

                            foreach (var security in securities)
                            {
                                if (security.MatterTypeId == (int)Enums.MatterTypeEnum.Purchase)
                                {
                                    hasPurchase = true;
                                    purchasedSec = security;
                                }
                            }
                            if (hasPurchase)
                            {
                                //CHECK IF INVESTMENT / OWNER OCCUPIED
                                //IF OWNER OCCUPIED: Security Address of purchased property
                                details.PostalAddresses.Add(new MatterCustomEntities.SmartVidPostalAddress()
                                {
                                    PartyName = partyDetails != null ? partyDetails.Firstname + " " + partyDetails.Lastname : $"Party {partyCount}",
                                    PartyId = partyId,
                                    Street = "|| CHECK IF OWNER OCCUPIED: || " + purchasedSec.StreetAddress,
                                    Suburb = purchasedSec.Suburb,
                                    Postcode = purchasedSec.PostCode
                                });

                                //details.PostalAddress.Street = "|| CHECK IF OWNER OCCUPIED: || " + purchasedSec.StreetAddress;
                                //details.PostalAddress.Suburb = purchasedSec.Suburb;
                                //details.PostalAddress.Postcode = purchasedSec.PostCode;
                                //IF INVESTMENT : George to confirm
                            }
                            else
                            {
                                details.PostalAddresses.Add(new MatterCustomEntities.SmartVidPostalAddress()
                                {
                                    PartyName = partyDetails != null ? partyDetails.Firstname + " " + partyDetails.Lastname : $"Party {partyCount}",
                                    PartyId = partyId,
                                    Street = context.MatterParties.FirstOrDefault(m => m.MatterPartyId == partyId)?.StreetAddress,
                                    Suburb = context.MatterParties.FirstOrDefault(m => m.MatterPartyId == partyId)?.Suburb,
                                    Postcode = context.MatterParties.FirstOrDefault(m => m.MatterPartyId == partyId)?.Postcode,
                                });
                            }

                        }
                    }
                    partyCount++;
                }

            }

            details.PaymentFrequencyIsReadOnly = (details.FundingChannelTypeId == (int)FundingChannelTypeEnum.Fund);

            foreach (var detail in details.Borrowers)
            {
                detail.NameHasSpace = !String.IsNullOrEmpty(detail.Firstname) && detail.Firstname.Trim().Contains(' ');
            }

            return details;
        }
        public static void GenerateIndividualVideo(int MWFSmartVidID, SlickContext context)
        {

            List<SmartVideoRequestVariable> projVariables = new List<SmartVideoRequestVariable>();
            projVariables.Add(CreateVideoRequest(MWFSmartVidID, context));



            string SmartVidAuthString = "Basic bHAzZWw2cDhtYjQ1aHRmazcwMW1kbGU9PTo2N2YyNjEwZjExODI4MDUzYWI5ZTE4YmM1YzUwZjg4MQ=="; //"Basic bHAzZWw2cDhtYjQ1aHRmazcwMW1kbGU9PTo2N2YyNjEwZjExODI4MDUzYWI5ZTE4YmM1YzUwZjg4MQ==";
            string SmartVidProjectKey = "roptlfjgmtirsmd3l"; //"rkfltiwm4lw2paioxl
            var client = new RestClient("https://sydney.personalisedvideolab.com/api/render/1132");
            var request = new RestRequest(Method.POST);

            request.AddHeader("cache-control", "no-cache");
            request.AddHeader("Authorization", SmartVidAuthString);
            request.AddHeader("Content-Type", "application/json");

            string requestString;

            SmartVideoRequest svRequest = new SmartVideoRequest
            {
                projectkey = SmartVidProjectKey
            };

            svRequest.variables = projVariables;

            requestString = JsonConvert.SerializeObject(svRequest,
                Newtonsoft.Json.Formatting.Indented,
                new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                });



            request.AddParameter("undefined", requestString, ParameterType.RequestBody);

            //EXECUTE
            IRestResponse response = client.Execute(request);

            //GET THE RESPONSE
            string responseText = response.Content;
            JavaScriptSerializer serializer = new JavaScriptSerializer();

            Console.WriteLine(responseText);

            JObject smartVidResponse = JObject.Parse(responseText);

            SmartVideoResponse ResponseObj = new SmartVideoResponse()
            {
                Response = smartVidResponse["Response"].ToString(),
                Receipt = smartVidResponse["Receipt"].ToString(),
                Count = smartVidResponse["Count"].ToString(),
            };

            List<Tokens> tokens = new List<Tokens>();

            var tokenItems = smartVidResponse["Tokens"];

            foreach (var token in tokenItems)
            {
                JProperty jProperty = token.ToObject<JProperty>();
                string smartVidId = token.First().ToString();

                string tokenStr = jProperty.Name;
                int dbSmartVidId = Convert.ToInt32(tokenStr.Substring(tokenStr.IndexOf("_") + 1));

                var thisVid = context.MatterWFSmartVidDetails.Where(v => v.MatterWFSmartVidDetailId == dbSmartVidId).FirstOrDefault();
                thisVid.ResponseUrl = Slick_Domain.GlobalVars.GetGlobalTxtVar("SmartVidVideoBaseUrl") + smartVidId + ".mp4";
                thisVid.ResponseToken = smartVidId;
                //thisVid.VideoSent = true;
                thisVid.UpdatedDate = DateTime.Now;

                string serverPrefix = "";
                if (Slick_Domain.GlobalVars.GetGlobalTxtVar(Slick_Domain.Common.DomainConstants.GlobalVars_TestingEmails).ToUpper() == "TRUE") serverPrefix = "UAT ";

                string subject = $"{serverPrefix}SMARTVID GENERATED - {tokenStr} | MATTER: {thisVid.MatterWFComponent.MatterId}";


                string body = $"<b>MATTER:</b> {thisVid.MatterWFComponent.MatterId}\n<b>VIDEO:</b>\n{thisVid.ResponseUrl}\n<b>LANDING PAGE:\n</b> {GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_SmartVidLandingPageRoot)}" + smartVidId + $"\n<b>REQUEST:</b>\n{requestString}";
                body = System.Text.RegularExpressions.Regex.Replace(body, @"\r\n?|\n", "<br>");

                var eRep = new EmailsRepository(context);
                eRep.SendEmail("george.kogios@msanational.com.au", "robert.quinn@msanational.com.au", subject, body);

            }

            context.SaveChanges();

            ResponseObj.Tokens = tokens;


        }


        public static void GenerateIndividualAFSHVideo(int MWFSmartVidID, SlickContext context, int matterPartyId)
        {


            AFSHSmartVideoRequest svRequest = CreateAFSHSmartVidRequest(MWFSmartVidID, context, matterPartyId);

            string SmartVidAuthString = "Basic bHAzZWw2cDhtYjQ1aHRmazcwMW1kbGU9PTo2N2YyNjEwZjExODI4MDUzYWI5ZTE4YmM1YzUwZjg4MQ=="; //"Basic bHAzZWw2cDhtYjQ1aHRmazcwMW1kbGU9PTo2N2YyNjEwZjExODI4MDUzYWI5ZTE4YmM1YzUwZjg4MQ==";
            string SmartVidProjectKey = "glto40mel4msWoFp"; //"rkfltiwm4lw2paioxl
            var client = new RestClient("https://sydney.personalisedvideolab.com/api/render/1217");
            var request = new RestRequest(Method.POST);

            request.AddHeader("cache-control", "no-cache");
            request.AddHeader("Authorization", SmartVidAuthString);
            request.AddHeader("Content-Type", "application/json");

            string requestString;

            svRequest.projectkey = SmartVidProjectKey;
            

            requestString = JsonConvert.SerializeObject(svRequest,
                Newtonsoft.Json.Formatting.Indented,
                new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                });



            request.AddParameter("undefined", requestString, ParameterType.RequestBody);

            //EXECUTE
            IRestResponse response = client.Execute(request);

            //GET THE RESPONSE
            string responseText = response.Content;
            JavaScriptSerializer serializer = new JavaScriptSerializer();

            Console.WriteLine(responseText);

            JObject smartVidResponse = JObject.Parse(responseText);

            SmartVideoResponse ResponseObj = new SmartVideoResponse()
            {
                Response = smartVidResponse["Response"].ToString(),
                Receipt = smartVidResponse["Receipt"].ToString(),
                Count = smartVidResponse["Count"].ToString(),
            };

            List<Tokens> tokens = new List<Tokens>();

            var tokenItems = smartVidResponse["Tokens"];

            foreach (var token in tokenItems)
            {
                JProperty jProperty = token.ToObject<JProperty>();
                string smartVidId = token.First().ToString();

                string tokenStr = jProperty.Name;
                int dbSmartVidId = Convert.ToInt32(tokenStr.Substring(tokenStr.IndexOf("_") + 1));

                var thisVid = context.MatterWFSmartVidDetails.Where(v => v.MatterWFSmartVidDetailId == dbSmartVidId).FirstOrDefault();
                thisVid.ResponseUrl = @"https://msavideos.personalisedvideolab.com/" + smartVidId + ".mp4";
                thisVid.ResponseToken = smartVidId;
                //thisVid.VideoSent = true;
                thisVid.UpdatedDate = DateTime.Now;

                string serverPrefix = "";
                if (Slick_Domain.GlobalVars.GetGlobalTxtVar(Slick_Domain.Common.DomainConstants.GlobalVars_TestingEmails).ToUpper() == "TRUE") serverPrefix = "UAT ";



                var matterId = context.MatterWFComponents.FirstOrDefault(m => m.MatterWFComponentId == thisVid.MatterWFComponentId).MatterId;

                string subject = $"{serverPrefix}AFSH SMARTVID GENERATED - {tokenStr} | MATTER: {matterId}";


                string body = $"<b>MATTER:</b> {matterId}\n<b>VIDEO:</b>\n{thisVid.ResponseUrl}\n<b>LANDING PAGE:\n</b> {GlobalVars.GetGlobalTxtVar("AFSHLoantrakBaseURL", context)}" + thisVid.PageToken + $"\n<b>REQUEST:</b>\n{requestString}";
                var mtDetails = context.Matters.Where(m=>m.MatterId == matterId).Select(m => new
                {
                    MatterId = matterId,
                    Swift = m.MatterSwiftAmounts.Any()? m.MatterSwiftAmounts.FirstOrDefault().SwiftAmount :(decimal?) null,
                    LoanAccDetails = m.MatterLoanAccounts.Where(x => x.MatterId == matterId)
                        .Select(x => new
                        {
                            x.LoanAccountNo,
                            x.LoanAmount, 
                            x.LoanDescription,
                            details = x.MatterLoanAccountDirectDebitDetails.Where(l => l.FrequencyTypeId.HasValue).Select(l => l.DirectDebitFrequencyType.DirectDebitFrequencyTypeName).FirstOrDefault() }) }).FirstOrDefault();

                body += $"\n<b>SWIFT AMOUNT: </b> {(mtDetails.Swift.HasValue ? mtDetails.Swift.Value.ToString("c") : "NOT SAVED")}";
                if (mtDetails.LoanAccDetails.Any())
                {
                    body += $"\n<b>DDR DETAILS: </b>";
                    foreach(var acc in mtDetails.LoanAccDetails)
                    {
                        string descToPrint = acc.LoanDescription;
                        if(!string.IsNullOrEmpty(descToPrint) && descToPrint.ToUpper().Contains("FIXED") && !descToPrint.ToUpper().Contains("IO"))
                        {
                            descToPrint = "FIXED P&I";
                        }
                        body += $" - {acc.LoanAccountNo} / {acc.LoanAmount.ToString("c")} / {descToPrint} / Payment Frequency : {acc.details}";
                    }
                }

                body = System.Text.RegularExpressions.Regex.Replace(body, @"\r\n?|\n", "<br>");

                var eRep = new EmailsRepository(context);
                eRep.SendEmail("george.kogios@msanational.com.au", "robert.quinn@msanational.com.au", subject, body);
                MatterNote mn = new MatterNote()
                {
                    MatterId = matterId,
                    MatterNoteTypeId = (int)Slick_Domain.Enums.MatterNoteTypeEnum.StatusUpdate,
                    NoteHeader = $"SmartVideo Generated",
                    NoteBody = $"<span style = 'font-family:Calibri; font-size:12pt;'><b>SmartVideo Generated successfully</b><br><a href = {thisVid.ResponseUrl}>DIRECT LINK TO VIDEO</a><p>GENERATION TEXT:</p><p>{body}</p>",
                    IsPublic = false,
                    HighPriority = false,
                    IsPinned = false,
                    IsDeleted = false,
                    UpdatedByUserId = GlobalVars.CurrentUser.UserId,
                    UpdatedDate = DateTime.Now
                };
                context.MatterNotes.Add(mn);

            }

            context.SaveChanges();

            ResponseObj.Tokens = tokens;


        }

        public static void GenerateIndividualAFSHVideoNewConnection(int MWFSmartVidID, int matterPartyId)
        {


            AFSHSmartVideoRequest svRequest = new AFSHSmartVideoRequest();
            using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadCommitted))
            {
                svRequest = CreateAFSHSmartVidRequest(MWFSmartVidID, uow.Context, matterPartyId);
            }

            string SmartVidAuthString = "Basic bHAzZWw2cDhtYjQ1aHRmazcwMW1kbGU9PTo2N2YyNjEwZjExODI4MDUzYWI5ZTE4YmM1YzUwZjg4MQ=="; //"Basic bHAzZWw2cDhtYjQ1aHRmazcwMW1kbGU9PTo2N2YyNjEwZjExODI4MDUzYWI5ZTE4YmM1YzUwZjg4MQ==";
            string SmartVidProjectKey = "glto40mel4msWoFp"; //"rkfltiwm4lw2paioxl
            var client = new RestClient("https://sydney.personalisedvideolab.com/api/render/1217");
            var request = new RestRequest(Method.POST);

            request.AddHeader("cache-control", "no-cache");
            request.AddHeader("Authorization", SmartVidAuthString);
            request.AddHeader("Content-Type", "application/json");

            string requestString;

            svRequest.projectkey = SmartVidProjectKey;


            requestString = JsonConvert.SerializeObject(svRequest,
                Newtonsoft.Json.Formatting.Indented,
                new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                });



            request.AddParameter("undefined", requestString, ParameterType.RequestBody);

            //EXECUTE
            IRestResponse response = client.Execute(request);

            //GET THE RESPONSE
            string responseText = response.Content;
            JavaScriptSerializer serializer = new JavaScriptSerializer();

            Console.WriteLine(responseText);

            JObject smartVidResponse = JObject.Parse(responseText);

            SmartVideoResponse ResponseObj = new SmartVideoResponse()
            {
                Response = smartVidResponse["Response"].ToString(),
                Receipt = smartVidResponse["Receipt"].ToString(),
                Count = smartVidResponse["Count"].ToString(),
            };

            List<Tokens> tokens = new List<Tokens>();

            var tokenItems = smartVidResponse["Tokens"];

            using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadCommitted))
            {
                foreach (var token in tokenItems)
                {
                    JProperty jProperty = token.ToObject<JProperty>();
                    string smartVidId = token.First().ToString();

                    string tokenStr = jProperty.Name;
                    int dbSmartVidId = Convert.ToInt32(tokenStr.Substring(tokenStr.IndexOf("_") + 1));

                    var thisVid = uow.Context.MatterWFSmartVidDetails.Where(v => v.MatterWFSmartVidDetailId == dbSmartVidId).FirstOrDefault();
                    thisVid.ResponseUrl = @"https://msavideos.personalisedvideolab.com/" + smartVidId + ".mp4";
                    thisVid.ResponseToken = smartVidId;
                    //thisVid.VideoSent = true;
                    thisVid.UpdatedDate = DateTime.Now;

                    string serverPrefix = "";
                    if (Slick_Domain.GlobalVars.GetGlobalTxtVar(Slick_Domain.Common.DomainConstants.GlobalVars_TestingEmails).ToUpper() == "TRUE") serverPrefix = "UAT ";



                    var matterId = uow.Context.MatterWFComponents.FirstOrDefault(m => m.MatterWFComponentId == thisVid.MatterWFComponentId).MatterId;

                    string subject = $"{serverPrefix}AFSH SMARTVID GENERATED - {tokenStr} | MATTER: {matterId}";


                    string body = $"<b>MATTER:</b> {matterId}\n<b>VIDEO:</b>\n{thisVid.ResponseUrl}\n<b>LANDING PAGE:\n</b> {GlobalVars.GetGlobalTxtVar("AFSHLoantrakBaseURL", uow.Context)}" + thisVid.PageToken + $"\n<b>REQUEST:</b>\n{requestString}";
                    var mtDetails = uow.Context.Matters.Where(m => m.MatterId == matterId).Select(m => new
                    {
                        MatterId = matterId,
                        Swift = m.MatterSwiftAmounts.Any() ? m.MatterSwiftAmounts.FirstOrDefault().SwiftAmount : (decimal?)null,
                        LoanAccDetails = m.MatterLoanAccounts.Where(x => x.MatterId == matterId)
                              .Select(x => new
                              {
                                  x.LoanAccountNo,
                                  x.LoanAmount,
                                  x.LoanDescription,
                                  details = x.MatterLoanAccountDirectDebitDetails.Where(l => l.FrequencyTypeId.HasValue).Select(l => l.DirectDebitFrequencyType.DirectDebitFrequencyTypeName).FirstOrDefault()
                              })
                    }).FirstOrDefault();

                    body += $"\n<b>SWIFT AMOUNT: </b> {(mtDetails.Swift.HasValue ? mtDetails.Swift.Value.ToString("c") : "NOT SAVED")}";
                    if (mtDetails.LoanAccDetails.Any())
                    {
                        body += $"\n<b>DDR DETAILS: </b>";
                        foreach (var acc in mtDetails.LoanAccDetails)
                        {
                            string descToPrint = acc.LoanDescription;
                            if (!string.IsNullOrEmpty(descToPrint) && descToPrint.ToUpper().Contains("FIXED") && !descToPrint.ToUpper().Contains("IO"))
                            {
                                descToPrint = "FIXED P&I";
                            }
                            body += $" - {acc.LoanAccountNo} / {acc.LoanAmount.ToString("c")} / {descToPrint} / Payment Frequency : {acc.details}";
                        }
                    }

                    body = System.Text.RegularExpressions.Regex.Replace(body, @"\r\n?|\n", "<br>");

                    var eRep = new EmailsRepository(uow.Context);
                    eRep.SendEmail("george.kogios@msanational.com.au", "robert.quinn@msanational.com.au", subject, body);
                    MatterNote mn = new MatterNote()
                    {
                        MatterId = matterId,
                        MatterNoteTypeId = (int)Slick_Domain.Enums.MatterNoteTypeEnum.StatusUpdate,
                        NoteHeader = $"SmartVideo Generated",
                        NoteBody = $"<span style = 'font-family:Calibri; font-size:12pt;'><b>SmartVideo Generated successfully</b><br><a href = {thisVid.ResponseUrl}>DIRECT LINK TO VIDEO</a><p>GENERATION TEXT:</p><p>{body}</p>",
                        IsPublic = false,
                        HighPriority = false,
                        IsPinned = false,
                        IsDeleted = false,
                        UpdatedByUserId = GlobalVars.CurrentUser.UserId,
                        UpdatedDate = DateTime.Now
                    };
                    uow.Context.MatterNotes.Add(mn);

                }

                uow.Save();
                uow.CommitTransaction();
            }

            ResponseObj.Tokens = tokens;


        }




        #region Email AFSH Videos
        public static void SendAFSHSmartVids()
        {

            using(var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadCommitted))
            {

            }
        }

        public static bool OverrideTesting { get; set; }
        public static bool ForceTesting { get; set; }
        public static string EmailTemplate { get; set; }
        public static string SMSTemplate { get; set; }
        public static string LandingPageRoot { get; set; }
        public class MatterEmailView
        {
            public int MatterId { get; set; }
            public string MatterDescription { get; set; }
            public string LenderRefNo { get; set; }
            public string LenderSecondaryRefNo { get; set; }
        }

        private static List<int> GetSmartVidsToSend()
        {
            using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadUncommitted))
            {
                return uow.Context.MatterWFSmartVidDetails.Where(x => x.LenderId == 139 && x.Enabled && x.CheckedForSending && !x.VideoSent && x.MatterWFComponent.Matter.Settled).Select(x => x.MatterWFSmartVidDetailId).ToList();
            }
        }
        public static void SendAFSHSmartVidEmails(List<int> idsToCreate = null)
        {
            bool testing = GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmails).ToUpper() == "TRUE";

            LandingPageRoot = GlobalVars.GetGlobalTxtVar("AFSHLoantrakBaseURL");
            Console.WriteLine("STARTING PROCESS: SmartVidEmailer");
            Console.WriteLine("---------------------------------");


            string filepath = "\\\\msa.local\\pub\\SLICK_RO\\ConstantDocs\\SmartVidTemplates\\SmartVidAFSHEmail.txt";
            Console.WriteLine($"Loading Email Template - {filepath}");
            try
            {
                EmailTemplate = LoadTemplate(filepath);
            }
            catch (Exception e)
            {
                Console.WriteLine("EXCEPTION: " + e.Message);
                if (String.IsNullOrEmpty(e.InnerException.Message))
                {
                    Console.WriteLine("INNER EXCEPTION: " + e.InnerException.Message);
                }
            }

            //this is the list of matters we update to use in an email at the end. 
            List<MatterEmailView> matterIdsSent = new List<MatterEmailView>();


            filepath = "\\\\msa.local\\pub\\SLICK_RO\\ConstantDocs\\SmartVidTemplates\\SmartVidAFSHSMS.txt";
            Console.WriteLine($"Loading SMS Template - {filepath}");
            try
            {
                SMSTemplate = LoadTemplate(filepath);
            }
            catch (Exception e)
            {
                Console.WriteLine("EXCEPTION: " + e.Message);
                if (String.IsNullOrEmpty(e.InnerException.Message))
                {
                    Console.WriteLine("INNER EXCEPTION: " + e.InnerException.Message);
                }
            }

            if (String.IsNullOrEmpty(EmailTemplate) || String.IsNullOrEmpty(SMSTemplate))
            {
                Console.WriteLine("TEMPLATES COULD NOT BE OPENED OR ARE EMPTY.");
                Console.WriteLine("EXITING.");
                return;
            }

            Console.WriteLine("---------------------------------");

            if (idsToCreate == null || idsToCreate.Count == 0)
            {
                idsToCreate = GetSmartVidsToSend();
            }
            if (idsToCreate.Count() == 0)
            {
                Console.WriteLine($"FOUND {idsToCreate.Count} VIDEOS TO SEND");
                Console.WriteLine("---------------------------------");
            }

            int loopCount = 0;
            int maxLoops = 1;
            //loop this process until we've got nothing left to send or have tried MaxLoop times. the process could be pretty slow so this is in case we have any naughty filemanagers booking settlements late at night.
            while (idsToCreate.Count() > 0 && loopCount < maxLoops)
            {

                if (idsToCreate.Count() > 1)
                    Console.WriteLine($"FOUND {idsToCreate.Count} VIDEOS TO SEND");
                else
                    Console.WriteLine($"FOUND 1 VIDEO TO SEND");

                Console.WriteLine("---------------------------------");


                foreach (var id in idsToCreate)
                {

                    bool emailSent = false;
                    bool smsSent = false;
                    Console.WriteLine($"SENDING VID - {id}");
                    using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadCommitted))
                    {
                        var details = uow.Context.MatterWFSmartVidDetails.FirstOrDefault(x => x.MatterWFSmartVidDetailId == id);
                        var partyDetails = uow.Context.MatterWFSmartVidDetailParties.FirstOrDefault(x => x.MatterWFSmartVidDetailId == id);

                        //FIRST check that the matter hasn't already settled - disable if it has.
                        //var mt = uow.Context.Matters.FirstOrDefault(m => m.MatterId == details.MatterWFComponent.MatterId);
                        //if (mt.Settled || (mt.SettlementSchedule != null && (mt.SettlementSchedule.SettlementDate + mt.SettlementSchedule.SettlementTime) < DateTime.Now))
                        //{
                        //    details.Enabled = false;
                        //    continue;
                        //}
                        if ((partyDetails != null && !String.IsNullOrEmpty(partyDetails.MatterPartySmartvidMobile)) || !String.IsNullOrEmpty(details.MatterParty?.Mobile))
                        {

                            string mobile = "";
                            if (partyDetails != null && !String.IsNullOrEmpty(partyDetails.MatterPartySmartvidMobile))
                            {
                                mobile = partyDetails.MatterPartySmartvidMobile;
                            }
                            else
                            {
                                mobile = details.MatterParty?.Mobile;
                            }
                            mobile = mobile.Replace("(614)", "04");

                            Console.WriteLine("SENDING SMS TO: " + mobile);
                            try
                            {

                                if (!SendSmartVidTxt(uow, details, mobile, testing))
                                {
                                    Console.WriteLine("Unable to send Smart Vid SMS");
                                }
                                else
                                {
                                    Console.WriteLine("Smart Vid SMS sent successfully");
                                    matterIdsSent.Add(new MatterEmailView()
                                    {
                                        MatterId = details.MatterWFComponent.MatterId,
                                        MatterDescription = details.MatterWFComponent.Matter.MatterDescription,
                                        LenderRefNo = details.MatterWFComponent.Matter.LenderRefNo,
                                        LenderSecondaryRefNo = details.MatterWFComponent.Matter.SecondaryRefNo
                                    });
                                    smsSent = true;
                                }

                            }
                            catch (Exception e)
                            {
                                Console.WriteLine("EXCEPTION: " + e.Message);
                                if (String.IsNullOrEmpty(e.InnerException.Message))
                                {
                                    Console.WriteLine("INNER EXCEPTION: " + e.InnerException.Message);
                                }
                            }
                        }
                        if ((partyDetails != null && !String.IsNullOrEmpty(partyDetails.MatterPartySmartvidEmail)) || !String.IsNullOrEmpty(details.MatterParty?.Email))
                        {
                            try
                            {
                                string email = "";
                                if (partyDetails != null && !String.IsNullOrEmpty(partyDetails.MatterPartySmartvidEmail))
                                {
                                    email = partyDetails.MatterPartySmartvidEmail;
                                }
                                else
                                {
                                    email = details.MatterParty?.Email;
                                }


                                Console.WriteLine("SENDING EMAIL TO: " + email);

                                if (!SendSmartVidEmail(uow, details, partyDetails, email, testing))
                                {
                                    Console.WriteLine("Unable to send Smart Vid Email");
                                }
                                else
                                {
                                    Console.WriteLine("Smart Vid Email sent successfully");
                                    matterIdsSent.Add(new MatterEmailView()
                                    {
                                        MatterId = details.MatterWFComponent.MatterId,
                                        MatterDescription = details.MatterWFComponent.Matter.MatterDescription,
                                        LenderRefNo = details.MatterWFComponent.Matter.LenderRefNo,
                                        LenderSecondaryRefNo = details.MatterWFComponent.Matter.SecondaryRefNo
                                    });
                                    emailSent = true;

                                }
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine("EXCEPTION: " + e.Message);
                                if (String.IsNullOrEmpty(e.InnerException.Message))
                                {
                                    Console.WriteLine("INNER EXCEPTION: " + e.InnerException.Message);
                                }
                            }
                        }

                    
                        Console.WriteLine("---");

                        if (!ForceTesting)
                        {
                            details.VideoSent = emailSent || smsSent;

                            if (emailSent)
                            {
                                details.EmailSentDate = DateTime.Now;
                                details.UpdatedDate = DateTime.Now;
                            }
                            if (smsSent)
                            {
                                details.SMSSentDate = DateTime.Now;
                                details.UpdatedDate = DateTime.Now;
                            }

                            if (emailSent || smsSent)
                            {
                                var noteRep = new Slick_Domain.Services.NotesRepository(uow.Context);
                                noteRep.SaveSmartVidsSentNote(details, emailSent, smsSent);
                            }
                        }

                        uow.Save();
                        uow.CommitTransaction();
                    }
                    Thread.Sleep(1000); //don't bombard SMTP or database
                }

                idsToCreate = GetSmartVidsToSend();
                if (ForceTesting)
                {
                    idsToCreate = new List<int>();
                }
                loopCount++;
            }

            matterIdsSent = matterIdsSent.DistinctBy(x => x.MatterId).ToList();

            string body = $"<span style=\"font-family: 'Comic Sans MS';\"><p>Hi cool cats,</p><p>The below SmartVideos have been sent to borrowers:</p>";

            foreach (var id in matterIdsSent)
            {
                body += $"- <b>{id.MatterId}</b> / {id.LenderRefNo} / {id.LenderSecondaryRefNo} : <i>{id.MatterDescription}</i> </br>";
            }

            body += "<p>Please update LoanCloser manually until the cool API works.</p><p>Thanks <3<br/>Alfred xoxo</p></span>";

            if (matterIdsSent.Count() > 0)
            {
                using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadUncommitted))
                {
                    var eRep = uow.GetEmailsRepositoryInstance();
                    eRep.SendEmail("george.kogios@msanational.com.au", "robert.quinn@msanational.com.au", "SMARTVIDEOS SENT", body, true);
                }
            }

            Console.WriteLine("---------------------------------");
            Console.WriteLine("PROCESS COMPLETE");
            Console.WriteLine("---------------------------------");

        }
  
        private static bool SendSmartVidEmail(UnitOfWork uow, MatterWFSmartVidDetail detail, MatterWFSmartVidDetailParty partyDetails, string address, bool testing)
        {
            try
            {


                detail.PageToken = null;
                if(detail.PageToken == null)
                {
                    var newToken = SlickSecurity.RandomStringURLsafe(25, detail.MatterWFSmartVidDetailId * partyDetails.MatterWFSmartVidDetailPartyId);

                    int seedIterator = 1;
                    while (uow.Context.MatterWFSmartVidDetails.Any(v=>v.PageToken == newToken))
                    {
                        newToken = SlickSecurity.RandomStringURLsafe(25, detail.MatterWFSmartVidDetailId * partyDetails.MatterWFSmartVidDetailPartyId + seedIterator);
                        seedIterator++;
                    }

                    uow.Context.MatterWFSmartVidDetails.FirstOrDefault(x => x.MatterWFSmartVidDetailId == detail.MatterWFSmartVidDetailId).PageToken = newToken;
                    uow.Save();
                    detail.PageToken = newToken;
                }


                string landingPageUrl = LandingPageRoot + detail.PageToken;
                string firstName = "";
                string lastName = "";

                string bannerImagePath = "";

                if (partyDetails != null)
                {
                    firstName = partyDetails.MatterPartyDisplayFirstname;
                    lastName = partyDetails.MatterPartyDisplayLastname;
                }
                else
                {
                    firstName = detail.MatterParty.Firstname;
                    lastName = detail.MatterParty.Lastname;
                }

                var mmDetails = uow.Context.MortMgrs.Where(m => m.MortMgrId == detail.MatterWFComponent.Matter.MortMgrId).Select(
                    x => new
                    {
                        Name = x.SmartVidMortMgrName ?? x.MortMgrName,
                        Email = x.SmartVidEmail ?? "CustomerCare@Advantedge.com.au",
                        Phone = x.SmartVidPhone ?? "1300 300 989",
                        Banner = x.SmartVidBannerPath ?? "NotSetup"
                    }
                    ).FirstOrDefault();

                string fullBannerPath = Path.Combine(GlobalVars.GetGlobalTxtVar("BrandingDirectory", uow.Context), mmDetails.Banner.Replace("/","\\"), "logo.png");
                if (!File.Exists(fullBannerPath))
                {
                    fullBannerPath = Path.Combine(GlobalVars.GetGlobalTxtVar("BrandingDirectory", uow.Context), "Advantedge", "logo.png");
                }


                string attachedDocPath = Path.Combine(GlobalVars.GetGlobalTxtVar("BrandingDirectory", uow.Context), mmDetails.Banner.Replace("/", "\\"), "Update Contact Details.pdf");
                if (!File.Exists(attachedDocPath))
                {
                    attachedDocPath = Path.Combine(GlobalVars.GetGlobalTxtVar("BrandingDirectory", uow.Context), "Advantedge", "Update Contact Details.pdf");
                }

                bannerImagePath = fullBannerPath;

                string shortFirstName = Slick_Domain.Services.SmartVidService.CleanName(firstName);
                //string subject = $"Enquiry - {detail.MatterParty.Firstname?.Trim()} {detail.MatterParty.Lastname?.Trim()} - {detail.MatterWFComponent.Matter.SecondaryRefNo} / {detail.MatterWFComponent.Matter.LenderRefNo} / MSA{detail.MatterWFComponent.MatterId}";
                string subject = $"Click and Confirm: {mmDetails.Name} Settlement Video - {firstName} {lastName} / {detail.MatterWFComponent.Matter.SecondaryRefNo} / MSA{detail.MatterWFComponent.MatterId}";
                string body = EmailTemplate.Replace("{{TimeOfDay}}", Slick_Domain.Services.EmailsService.GetTimeOfDay(DateTime.Now))
                                           .Replace("{{MortMgrName}}", mmDetails.Name)
                                           .Replace("{{MortMgrPhone}}", mmDetails.Phone)
                                           .Replace("{{MortMgrEmail}}", mmDetails.Email)
                                           .Replace("{{Name}}", shortFirstName)
                                           .Replace("{{LandingPageUrl}}", landingPageUrl);

                var eRep = uow.GetEmailsRepositoryInstance();

                if ((testing && !OverrideTesting) || ForceTesting)
                {
                    subject += " WOULD HAVE BEEN SENT TO - " + address;
                    eRep.SendEmail("george.kogios@msanational.com.au", null, subject, body, isHTML: true, bannerImagePath: bannerImagePath, attachedDocPath: attachedDocPath);
                }
                else
                {
                    eRep.SendEmail(address, null, subject, body, isHTML: true, bannerImagePath: bannerImagePath, attachedDocPath: attachedDocPath);
                }
            }
            catch (Exception e)
            {
                uow.Context.SmartVidEmailLogs.Add
                (
                    new SmartVidEmailLog
                    {
                        MatterWFSmartVidDetailId = detail.MatterWFSmartVidDetailId,
                        LogDate = DateTime.Now,
                        Recipient = address,
                        IsSMS = false,
                        Successful = false,
                        ExceptionMessage = e.Message,
                        InnerExceptionMessage = e.InnerException?.Message
                    }
                );
                uow.Save();
                throw e;
            }
            if (!ForceTesting)
            {
                uow.Context.SmartVidEmailLogs.Add
                   (
                       new SmartVidEmailLog
                       {
                           MatterWFSmartVidDetailId = detail.MatterWFSmartVidDetailId,
                           LogDate = DateTime.Now,
                           Recipient = address,
                           IsSMS = false,
                           Successful = true
                       }
                   );
            }
            uow.Save();
            return true;
        }
        public static bool SendSmartVidTxt(UnitOfWork uow, MatterWFSmartVidDetail detail, string mobile, bool testing)
        {
            string origRecipient = "";
            try
            {
                var mmDetails = uow.Context.MortMgrs.Where(m => m.MortMgrId == detail.MatterWFComponent.Matter.MortMgrId).Select(
                 x => new
                 {
                     Name = x.SmartVidMortMgrName ?? x.MortMgrName,
                     Email = x.SmartVidEmail ?? "customercare@advantedge.com.au",
                     Phone = x.SmartVidPhone ?? "1300 300 989"
                 }
                 ).FirstOrDefault();

                string firstName = "";
                string lastName = "";

                var partyDetails = detail.MatterWFSmartVidDetailParty;
                if (partyDetails != null)
                {
                    firstName = partyDetails.MatterPartyDisplayFirstname;
                    lastName = partyDetails.MatterPartyDisplayLastname;
                }
                else
                {
                    firstName = detail.MatterParty.Firstname;
                    lastName = detail.MatterParty.Lastname;
                }
                var shortFirstName = CleanName(firstName);


                if (detail.AccessCode == null)
                {
                    var newAccessCode = SlickSecurity.RandomNumericString(6);
                    uow.Context.MatterWFSmartVidDetails.FirstOrDefault(x => x.MatterWFSmartVidDetailId == detail.MatterWFSmartVidDetailId).AccessCode = "MSA-" + newAccessCode;
                    uow.Save();
                    detail.AccessCode = newAccessCode;
                }

                string accessCode = detail.AccessCode;

                if (SMSTemplate == null)
                {
                    var filepath = "\\\\msa.local\\pub\\SLICK_RO\\ConstantDocs\\SmartVidTemplates\\SmartVidAFSHSMS.txt";
                    Console.WriteLine($"Loading SMS Template - {filepath}");
                    
                    SMSTemplate = LoadTemplate(filepath);
                    
                }


                string txtToSend = SMSTemplate.Replace("{{TimeOfDay}}", Slick_Domain.Services.EmailsService.GetTimeOfDay(DateTime.Now))
                                           .Replace("{{MortMgrName}}", mmDetails.Name)
                                           .Replace("{{MortMgrPhone}}", mmDetails.Phone)
                                           .Replace("{{MortMgrEmail}}", mmDetails.Email)
                                           .Replace("{{Name}}", shortFirstName)
                                           .Replace("{{AccessCode}}", accessCode);

                string cleanMobile = mobile.Replace(" ", "");

                if (cleanMobile[0] == '4')
                {
                    cleanMobile = "0" + cleanMobile;
                }

                var recipient = cleanMobile + "@now.macquarieview.com";
                origRecipient = recipient;
                var eRep = uow.GetEmailsRepositoryInstance();

                txtToSend = txtToSend.Replace("<br>", "\n");
    

                if ((testing && !OverrideTesting) || ForceTesting)
                {
                    txtToSend = $"WOULD HAVE BEEN SENT TO: {cleanMobile}\n" + txtToSend;
                    //recipient = "0401727303@now.macquarieview.com";
                    //eRep.SendEmail(recipient, null, null, txtToSend, false);
                    cleanMobile = "0401727303";
                    SmsService.SendMacquarieNowSms(cleanMobile, txtToSend);
                }
                else
                {

                    //eRep.SendEmail(recipient, null, null, txtToSend, false);
                    SmsService.SendMacquarieNowSms(cleanMobile, txtToSend);
                }

            }
            catch (Exception e)
            {
                uow.Context.SmartVidEmailLogs.Add
                 (
                     new SmartVidEmailLog
                     {
                         MatterWFSmartVidDetailId = detail.MatterWFSmartVidDetailId,
                         LogDate = DateTime.Now,
                         Recipient = origRecipient,
                         IsSMS = true,
                         Successful = false,
                         ExceptionMessage = e.Message,
                         InnerExceptionMessage = e.InnerException?.Message
                     }
                 );
                throw e;
            }
            if (!ForceTesting)
            {
                uow.Context.SmartVidEmailLogs.Add
               (
                   new SmartVidEmailLog
                   {
                       MatterWFSmartVidDetailId = detail.MatterWFSmartVidDetailId,
                       LogDate = DateTime.Now,
                       Recipient = origRecipient,
                       IsSMS = true,
                       Successful = true
                   }
               );
            }
            uow.Save();

            return true;
        }
        private static string LoadTemplate(string filepath)
        {
            if (!File.Exists(filepath))
            {
                return null;
            }
            else
            {
                return File.ReadAllText(filepath);
            }
        }

        public static string ShortenUrl(string url)
        {
            return url;
        }


        public static void GenerateResimacSmartVideos(int matterId, int lenderId, int matterWFComponentId)
        {
            List<ResimacSmartVideoRequestVariable> videosToGenerate = new List<ResimacSmartVideoRequestVariable>();
            using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadUncommitted))
            {
                var procDocsDetails = uow.Context.MatterWFSendProcessedDocs.Where(d => d.MatterWFComponentId == matterWFComponentId)
                    .Select(d => new { d.DocumentDeliveryTypeId }).ToList();
                var mtDetails = uow.Context.Matters.Where(p => p.MatterId == matterId)
                    .Select(p => new
                    {
                        MatterTypes = p.MatterMatterTypes.Select(m => m.MatterTypeId).ToList(),
                        IsConstruction = p.IsConstruction ?? false,
                        Securities = p.MatterSecurities.Where(d => !d.Deleted).Select(s => s.IsStrata)
                    }
                    ).ToList()
                    .Select(p => new ResimacSmartVideoRequestVariable()
                    {
                        loan_video = p.MatterTypes.All(t => t == (int)MatterTypeEnum.Purchase) ? "purchase.mp4" : "loan.mp4",
                        doc_video = procDocsDetails.Any(d => d.DocumentDeliveryTypeId == (int)DocumentDeliveryTypeEnum.ExpressPostNoReturnEnvelope || d.DocumentDeliveryTypeId == (int)DocumentDeliveryTypeEnum.ExpressPostReturnEnvelope) ?
                            "doc2.mp4" : "doc1.mp4",
                        insurance_video = p.IsConstruction == true ? "insurance4.mp4" :
                            p.Securities.All(s => s == true) ? "insurance2.mp4" :
                            p.Securities.All(s => s == false) ? "insurance1.mp4"
                            : "insurance3.mp4"
                    }).FirstOrDefault();

                var partyDetails = uow.Context.MatterParties.Where(m => m.MatterId == matterId && m.IsIndividual)
                    .Select(p => new
                {
                    p.MatterPartyId,
                    p.Firstname,
                    p.DisplayFirstname,
                    p.Lastname
                }).ToList();

                foreach(var party in partyDetails)
                {
                    string firstNameToUse = !string.IsNullOrEmpty(party.DisplayFirstname) ? party.DisplayFirstname : string.IsNullOrEmpty(party.Firstname) ? CleanName(party.Firstname) : party.Lastname;

                    var newToken = SlickSecurity.RandomStringURLsafe(25, matterWFComponentId * party.MatterPartyId);

                    int seedIterator = 1;

                    while (uow.Context.MatterWFSmartVidDetails.Any(v => v.PageToken == newToken))
                    {
                        newToken = SlickSecurity.RandomStringURLsafe(25, matterWFComponentId * party.MatterPartyId + seedIterator);
                        seedIterator++;
                    }

                    MatterWFSmartVidDetail vid = new MatterWFSmartVidDetail()
                    {
                        Enabled = true,
                        VideoSent = false,
                        MatterWFComponentId = matterWFComponentId,
                        LenderId = lenderId,
                        MatterPartyId = party.MatterPartyId,
                        PageToken = newToken,
                        AccessCode = SlickSecurity.RandomNumericString(6),
                        CheckedForSending = false,
                        FundingChannelTypeId = (int)FundingChannelTypeEnum.FirstParty,
                        UpdatedByUserId = GlobalVars.CurrentUser.UserId,
                        UpdatedDate = DateTime.Now
                    };

                    uow.Context.MatterWFSmartVidDetails.Add(vid);
                    uow.Save();

                    var variables = mtDetails;

                    variables.name_text = firstNameToUse?.Trim();
                    variables.name_audio = firstNameToUse?.Trim() + ".mp3";
                    variables.id = vid.MatterWFSmartVidDetailId.ToString();
                    
                    videosToGenerate.Add(variables);

                }

                uow.CommitTransaction();

            }

            foreach (var video in videosToGenerate)
            {

              


                string SmartVidAuthString = DomainConstants.ResimacSmartVidAuth; //"Basic bHAzZWw2cDhtYjQ1aHRmazcwMW1kbGU9PTo2N2YyNjEwZjExODI4MDUzYWI5ZTE4YmM1YzUwZjg4MQ==";
                string SmartVidProjectKey = DomainConstants.ResimacSmartVidProjectKey; //"rkfltiwm4lw2paioxl
                var client = new RestClient(DomainConstants.ResimacSmartVidEndpoint);
                var request = new RestRequest(Method.POST);

                request.AddHeader("cache-control", "no-cache");
                request.AddHeader("Authorization", SmartVidAuthString);
                request.AddHeader("Content-Type", "application/json");

                string requestString;

                ResimacSmartVideoRequest svRequest = new ResimacSmartVideoRequest
                {
                    projectkey = SmartVidProjectKey,
                    variables = new List<ResimacSmartVideoRequestVariable>() { video }
                };


                requestString = JsonConvert.SerializeObject(svRequest,
                        Newtonsoft.Json.Formatting.Indented,
                        new JsonSerializerSettings
                        {
                            NullValueHandling = NullValueHandling.Ignore
                        });



                request.AddParameter("undefined", requestString, ParameterType.RequestBody);

                //EXECUTE
                IRestResponse response = client.Execute(request);

                //GET THE RESPONSE
                string responseText = response.Content;
                JavaScriptSerializer serializer = new JavaScriptSerializer();

                Console.WriteLine(responseText);

                JObject smartVidResponse = JObject.Parse(responseText);

                SmartVideoResponse ResponseObj = new SmartVideoResponse()
                {
                    Response = smartVidResponse["Response"].ToString(),
                    Receipt = smartVidResponse["Receipt"].ToString(),
                    Count = smartVidResponse["Count"].ToString(),
                };

                List<Tokens> tokens = new List<Tokens>();

                var tokenItems = smartVidResponse["Tokens"];

                using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadCommitted))
                {
                    foreach (var token in tokenItems)
                    {
                        JProperty jProperty = token.ToObject<JProperty>();
                        string smartVidId = token.First().ToString();

                        string tokenStr = jProperty.Name;
                        int dbSmartVidId = Convert.ToInt32(tokenStr.Substring(tokenStr.IndexOf("_") + 1));

                        var thisVid = uow.Context.MatterWFSmartVidDetails.Where(v => v.MatterWFSmartVidDetailId == dbSmartVidId).FirstOrDefault();
                        thisVid.ResponseUrl = Slick_Domain.GlobalVars.GetGlobalTxtVar("SmartVidVideoBaseUrl", uow.Context) + smartVidId + ".mp4";
                        thisVid.ResponseToken = smartVidId;
                        //thisVid.VideoSent = true;
                        thisVid.UpdatedDate = DateTime.Now;

                        string serverPrefix = "";
                        if (Slick_Domain.GlobalVars.GetGlobalTxtVar(Slick_Domain.Common.DomainConstants.GlobalVars_TestingEmails).ToUpper() == "TRUE") serverPrefix = "UAT ";

                        string subject = $"{serverPrefix}SMARTVID GENERATED - {tokenStr} | MATTER: {matterId}";
                        string body = $"<b>MATTER:</b> {matterId}\n<b>VIDEO:</b>\n{thisVid.ResponseUrl}\n<b>LANDING PAGE:\n</b>{GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_SmartVidLandingPageRoot)}" + smartVidId + $"\n<b>REQUEST:</b>\n{requestString}";
                        body = System.Text.RegularExpressions.Regex.Replace(body, @"\r\n?|\n", "<br>");

                        var eRep = new EmailsRepository(uow.Context);
                        eRep.SendEmail("george.kogios@msanational.com.au", "robert.quinn@msanational.com.au", subject, body);
                        var nRep = new NotesRepository(uow.Context);

                        uow.Save();
                        //not implemented sender 
                        //nRep.AddWebNote(matterId, subject, $"New smartvid generated at {System.DateTime.Now.ToString()}. Email was sent to xxx",GlobalVars.CurrentUser.UserId);
                    }
                    uow.CommitTransaction();
                }


            }

        }

        #endregion
        //public static void GenerateAFSHIndividualVideo(int MWFSmartVidID, SlickContext context, int matterId)
        //{

        //    List<AFSH> projVariables = new List<SmartVideoRequestVariable>();
        //    projVariables = CreateAFSHSmartVidRequest(MWFSmartVidID, context).variables;




        //    string SmartVidAuthString = "Basic bHAzZWw2cDhtYjQ1aHRmazcwMW1kbGU9PTo2N2YyNjEwZjExODI4MDUzYWI5ZTE4YmM1YzUwZjg4MQ=="; //"Basic bHAzZWw2cDhtYjQ1aHRmazcwMW1kbGU9PTo2N2YyNjEwZjExODI4MDUzYWI5ZTE4YmM1YzUwZjg4MQ==";
        //    string SmartVidProjectKey = "roptlfjgmtirsmd3l"; //"rkfltiwm4lw2paioxl
        //    var client = new RestClient("https://sydney.personalisedvideolab.com/api/render/1132");
        //    var request = new RestRequest(Method.POST);

        //    request.AddHeader("cache-control", "no-cache");
        //    request.AddHeader("Authorization", SmartVidAuthString);
        //    request.AddHeader("Content-Type", "application/json");

        //    string requestString;

        //    SmartVideoRequest svRequest = new SmartVideoRequest
        //    {
        //        projectkey = SmartVidProjectKey
        //    };

        //    svRequest.variables = projVariables;

        //    requestString = JsonConvert.SerializeObject(svRequest,
        //            Newtonsoft.Json.Formatting.Indented,
        //            new JsonSerializerSettings
        //            {
        //                NullValueHandling = NullValueHandling.Ignore
        //            });



        //    request.AddParameter("undefined", requestString, ParameterType.RequestBody);

        //    //EXECUTE
        //    IRestResponse response = client.Execute(request);

        //    //GET THE RESPONSE
        //    string responseText = response.Content;
        //    JavaScriptSerializer serializer = new JavaScriptSerializer();

        //    Console.WriteLine(responseText);

        //    JObject smartVidResponse = JObject.Parse(responseText);

        //    SmartVideoResponse ResponseObj = new SmartVideoResponse()
        //    {
        //        Response = smartVidResponse["Response"].ToString(),
        //        Receipt = smartVidResponse["Receipt"].ToString(),
        //        Count = smartVidResponse["Count"].ToString(),
        //    };

        //    List<Tokens> tokens = new List<Tokens>();

        //    var tokenItems = smartVidResponse["Tokens"];

        //    foreach (var token in tokenItems)
        //    {
        //        JProperty jProperty = token.ToObject<JProperty>();
        //        string smartVidId = token.First().ToString();

        //        string tokenStr = jProperty.Name;
        //        int dbSmartVidId = Convert.ToInt32(tokenStr.Substring(tokenStr.IndexOf("_") + 1));

        //        var thisVid = context.MatterWFSmartVidDetails.Where(v => v.MatterWFSmartVidDetailId == dbSmartVidId).FirstOrDefault();
        //        thisVid.ResponseUrl = Slick_Domain.GlobalVars.GetGlobalTxtVar("SmartVidVideoBaseUrl") + smartVidId + ".mp4";
        //        thisVid.ResponseToken = smartVidId;
        //        //thisVid.VideoSent = true;
        //        thisVid.UpdatedDate = DateTime.Now;

        //        string serverPrefix = "";
        //        if (Slick_Domain.GlobalVars.GetGlobalTxtVar(Slick_Domain.Common.DomainConstants.GlobalVars_TestingEmails).ToUpper() == "TRUE") serverPrefix = "UAT ";

        //        string subject = $"{serverPrefix}SMARTVID GENERATED - {tokenStr} | MATTER: {matterId}";
        //        string body = $"<b>MATTER:</b> {matterId}\n<b>VIDEO:</b>\n{thisVid.ResponseUrl}\n<b>LANDING PAGE:\n</b>{GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_SmartVidLandingPageRoot)}" + smartVidId + $"\n<b>REQUEST:</b>\n{requestString}";
        //        body = System.Text.RegularExpressions.Regex.Replace(body, @"\r\n?|\n", "<br>");

        //        var eRep = new EmailsRepository(context);
        //        eRep.SendEmail("george.kogios@msanational.com.au", "robert.quinn@msanational.com.au", subject, body);
        //        var nRep = new NotesRepository(context);
        //        //not implemented sender 
        //        //nRep.AddWebNote(matterId, subject, $"New smartvid generated at {System.DateTime.Now.ToString()}. Email was sent to xxx",GlobalVars.CurrentUser.UserId);
        //    }



        //    context.SaveChanges();

        //    ResponseObj.Tokens = tokens;


        //}

        public static void GenerateIndividualVideo(int MWFSmartVidID, SlickContext context, int matterId)
        {

            List<SmartVideoRequestVariable> projVariables = new List<SmartVideoRequestVariable>();
            projVariables.Add(CreateVideoRequest(MWFSmartVidID, context));



            string SmartVidAuthString = "Basic bHAzZWw2cDhtYjQ1aHRmazcwMW1kbGU9PTo2N2YyNjEwZjExODI4MDUzYWI5ZTE4YmM1YzUwZjg4MQ=="; //"Basic bHAzZWw2cDhtYjQ1aHRmazcwMW1kbGU9PTo2N2YyNjEwZjExODI4MDUzYWI5ZTE4YmM1YzUwZjg4MQ==";
            string SmartVidProjectKey = "roptlfjgmtirsmd3l"; //"rkfltiwm4lw2paioxl
            var client = new RestClient("https://sydney.personalisedvideolab.com/api/render/1132");
            var request = new RestRequest(Method.POST);

            request.AddHeader("cache-control", "no-cache");
            request.AddHeader("Authorization", SmartVidAuthString);
            request.AddHeader("Content-Type", "application/json");

            string requestString;

            SmartVideoRequest svRequest = new SmartVideoRequest
            {
                projectkey = SmartVidProjectKey
            };

            svRequest.variables = projVariables;

            requestString = JsonConvert.SerializeObject(svRequest,
                    Newtonsoft.Json.Formatting.Indented,
                    new JsonSerializerSettings
                    {
                        NullValueHandling = NullValueHandling.Ignore
                    });



            request.AddParameter("undefined", requestString, ParameterType.RequestBody);

            //EXECUTE
            IRestResponse response = client.Execute(request);

            //GET THE RESPONSE
            string responseText = response.Content;
            JavaScriptSerializer serializer = new JavaScriptSerializer();

            Console.WriteLine(responseText);

            JObject smartVidResponse = JObject.Parse(responseText);

            SmartVideoResponse ResponseObj = new SmartVideoResponse()
            {
                Response = smartVidResponse["Response"].ToString(),
                Receipt = smartVidResponse["Receipt"].ToString(),
                Count = smartVidResponse["Count"].ToString(),
            };

            List<Tokens> tokens = new List<Tokens>();

            var tokenItems = smartVidResponse["Tokens"];

            foreach (var token in tokenItems)
            {
                JProperty jProperty = token.ToObject<JProperty>();
                string smartVidId = token.First().ToString();

                string tokenStr = jProperty.Name;
                int dbSmartVidId = Convert.ToInt32(tokenStr.Substring(tokenStr.IndexOf("_") + 1));

                var thisVid = context.MatterWFSmartVidDetails.Where(v => v.MatterWFSmartVidDetailId == dbSmartVidId).FirstOrDefault();
                thisVid.ResponseUrl = Slick_Domain.GlobalVars.GetGlobalTxtVar("SmartVidVideoBaseUrl") + smartVidId + ".mp4";
                thisVid.ResponseToken = smartVidId;
                //thisVid.VideoSent = true;
                thisVid.UpdatedDate = DateTime.Now;

                string serverPrefix = "";
                if (Slick_Domain.GlobalVars.GetGlobalTxtVar(Slick_Domain.Common.DomainConstants.GlobalVars_TestingEmails).ToUpper() == "TRUE") serverPrefix = "UAT ";

                string subject = $"{serverPrefix}SMARTVID GENERATED - {tokenStr} | MATTER: {matterId}";
                string body = $"<b>MATTER:</b> {matterId}\n<b>VIDEO:</b>\n{thisVid.ResponseUrl}\n<b>LANDING PAGE:\n</b>{GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_SmartVidLandingPageRoot)}" + smartVidId + $"\n<b>REQUEST:</b>\n{requestString}";
                body = System.Text.RegularExpressions.Regex.Replace(body, @"\r\n?|\n", "<br>");

                var eRep = new EmailsRepository(context);
                eRep.SendEmail("george.kogios@msanational.com.au", "robert.quinn@msanational.com.au", subject, body);
                var nRep = new NotesRepository(context);
                //not implemented sender 
                //nRep.AddWebNote(matterId, subject, $"New smartvid generated at {System.DateTime.Now.ToString()}. Email was sent to xxx",GlobalVars.CurrentUser.UserId);
            }



            context.SaveChanges();

            ResponseObj.Tokens = tokens;


        }

        public static void GenerateIndividualVideo(int MWFSmartVidID)
        {
            using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadCommitted))
            {
                GenerateIndividualVideo(MWFSmartVidID, uow.Context);
                uow.Save();
                uow.CommitTransaction();
            }
        }
        public static void GenerateIndividualAFSHVideo(int MWFSmartVidID)
        {
            using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadCommitted))
            {
                foreach (var party in uow.Context.MatterWFSmartVidDetailParties.Where(x => x.MatterWFSmartVidDetailId == MWFSmartVidID && x.MatterPartyId.HasValue))
                {
                    GenerateIndividualAFSHVideo(MWFSmartVidID, uow.Context, party.MatterPartyId.Value);
                }
                uow.Save();
                uow.CommitTransaction();
            }
        }
        public static SmartVideoRequestVariable CreateVideoRequest(int MWFSmartVidId, SlickContext context)
        {
            string baseUrl = "https://assets.personalisedvideolab.com/";
            string baseUrlSyd = "https://sydney.smartvideoaustralia.com.au/";

            var mwfVidDetail = context.MatterWFSmartVidDetails.AsNoTracking().Where(m => m.MatterWFSmartVidDetailId == MWFSmartVidId).FirstOrDefault();
            var mwfPartyDetail = context.MatterWFSmartVidDetailParties.AsNoTracking().Where(m => m.MatterWFSmartVidDetailId == mwfVidDetail.MatterWFSmartVidDetailId).FirstOrDefault();

            var matterDetails = mwfVidDetail.MatterWFComponent.Matter;
            //var loanAccount = matterDetails.MatterLoanAccounts.FirstOrDefault(); ///TEMP:  NEED TO INCLUDE LOANACCOUNT ID IN MWFSMARTVID
            var loanAmount = matterDetails.MatterLoanAccounts.Sum(x => x.LoanAmount);

            DateTime settlementDate = matterDetails.SettlementSchedule != null ? matterDetails.SettlementSchedule.SettlementDate : DateTime.Today;

            SmartVideoRequestVariable variable = new SmartVideoRequestVariable
            {
                id = "LA" + context.MatterLoanAccounts.FirstOrDefault(x => x.MatterId == matterDetails.MatterId).LoanAccountNo + "_" + MWFSmartVidId.ToString(), //xxxxxxx
                name_text = mwfPartyDetail != null ? mwfPartyDetail.MatterPartyDisplayFirstname : mwfVidDetail.MatterParty.Firstname, //Zoe
                name_audio = baseUrl + "audio/voices/matto/" + (mwfPartyDetail != null ? mwfPartyDetail.MatterPartyDisplayFirstname : mwfVidDetail.MatterParty.Firstname) + ".mp3", //https://assets.personalisedvideolab.com/audio/voices/matto/Zoe.mp3

                //TEST TILL GET LOAN TYPE---------------------------------------------------------------------------------------------------------------
                loantype_audio = "loan.mp3", //loan.mp3
                loantype_image = "loan_image.png", //loan_image.png
                loanamount_audio = baseUrlSyd + "purse/" + loanAmount, //https://sydney.smartvideoaustralia.com.au/purse/112512.23
                loanamount_text = "$" + String.Format("{0:n}", loanAmount), //$112,512.23
                //--------------------------------------------------------------------------------------------------------------------------------------

                day_text = settlementDate.Day.ToString(), //1
                month_text = settlementDate.ToString("MMMM"), //January
                date_audio = settlementDate.ToString("MMM") + settlementDate.Day.ToString() + ".mp3", //Jan1.mp3
                email_text = mwfPartyDetail != null ? (!String.IsNullOrEmpty(mwfPartyDetail.MatterPartySmartvidEmail) ? mwfPartyDetail.MatterPartySmartvidEmail : "No email details on file")
                    : (!String.IsNullOrEmpty(mwfVidDetail.MatterParty.Email) ? mwfVidDetail.MatterParty.Email : "No email details on file"),
                address_text = mwfVidDetail.DisplayPostalAddress.Trim(), //3a/221 Spring Road, Brisbane\nQueensland 4000
                                                                         //date_text = settlementDate.ToShortDateString(), //1st May 2019
                                                                         //repaymentdate_audio = "May3.mp3", //May3.mp3

            };

            if (mwfVidDetail.FundingChannelTypeId == (int)Enums.FundingChannelTypeEnum.Hybrid || mwfVidDetail.FundingChannelTypeId == (int)Enums.FundingChannelTypeEnum.INSTO)
            {
                variable.repaymentdate_video = "repaymentdate_insto.mp4"; //firstrepayment_fund.mp4
                variable.repaymentamount_video = "repaymentamount.mp4"; //repaymentamount.mp4 
            }
            else
            {
                variable.repaymentdate_video = "repaymentdate_fund.mp4"; //firstrepayment_fund.mp4
                variable.repaymentamount_video = "repaymentamount_fund.mp4";
            }

            if (mwfVidDetail.DirectDebitFrequencyTypeId != null)
            {
                variable.repaymentmethod_video = "repaymentmethod_yes.mp4"; //repaymentmethod_yes.mp3
                variable.bsb1_text = "BSB: " + "***-" + mwfVidDetail.DirectDebitBSB.Substring(mwfVidDetail.DirectDebitBSB.Length - 3, 3); //BSB:033-044
                variable.acct1_text = "Account: " + "**** " + mwfVidDetail.DirectDebitAcc.Substring(mwfVidDetail.DirectDebitAcc.Length - 4, 4); //ACCT: 98989898

                if (!String.IsNullOrEmpty(mwfVidDetail.DirectDebitAcc2) && !String.IsNullOrEmpty(mwfVidDetail.DirectDebitBSB2))
                {
                    variable.bsb2_text = mwfVidDetail.DirectDebitBSB2;
                    variable.acct2_text = mwfVidDetail.DirectDebitAcc2;
                }

                variable.repaymentcycle_audio = mwfVidDetail.DirectDebitFrequencyType.DirectDebitFrequencyTypeName.ToLower() + ".mp3"; //weekly.mp3
                variable.repaymentcycle_text = mwfVidDetail.DirectDebitFrequencyType.DirectDebitFrequencyTypeName.ToLower(); //weekly
            }
            else
            {
                variable.repaymentmethod_video = "repaymentmethod_no.mp4";
            }

            //work out what loan type. We have the default generic "loan" one already set, but if it's a simple matter with just one type then we can use that instead. 
            List<int> allMatterTypes = new List<int>();

            foreach (var sec in matterDetails.MatterSecurities)
            {
                allMatterTypes.Add(sec.MatterType.MatterTypeId);
            }

            IEnumerable<int> types = allMatterTypes;
            types = types.Distinct();

            if (types.Count() == 1)
            {

                switch (types.FirstOrDefault())
                {
                    case (int)Enums.MatterTypeEnum.ClearTitle:
                        variable.loantype_audio = "cleartitleloan.mp3";
                        variable.loantype_image = "cleartitleloan_image.png";
                        break;
                    case (int)Enums.MatterTypeEnum.Refinance:
                    case (int)Enums.MatterTypeEnum.FastRefinance:
                        variable.loantype_audio = "refinanceloan.mp3";
                        variable.loantype_image = "refinanceloan_image.png";
                        break;
                    case (int)Enums.MatterTypeEnum.Purchase:
                        variable.loantype_audio = "purchaseloan.mp3";
                        variable.loantype_image = "purchaseloan_image.png";
                        break;
                    case (int)Enums.MatterTypeEnum.Increase:
                        variable.loantype_audio = "increaseloan.mp3";
                        variable.loantype_image = "increaseloan_image.png";
                        break;
                    default:
                        break;
                }
            }
            return variable;

        }
        public static void BulkVideoRequest()
        {
            using (var context = new SlickContext())
            {
                //GET ALL UNSENT, ENABLED VIDEOS.
                List<SmartVideoRequestVariable> projVariables = new List<SmartVideoRequestVariable>();
                foreach (var vids in context.MatterWFSmartVidDetails.Where(x => x.Enabled == true && x.VideoSent == false && x.MatterWFComponent.Matter.LenderId == 166))
                {
                    var vidRequest = CreateVideoRequest(vids.MatterWFSmartVidDetailId, context);
                    projVariables.Add(vidRequest);
                }

                string SmartVidAuthString = "Basic bHAzZWw2cDhtYjQ1aHRmazcwMW1kbGU9PTo2N2YyNjEwZjExODI4MDUzYWI5ZTE4YmM1YzUwZjg4MQ=="; //"Basic bHAzZWw2cDhtYjQ1aHRmazcwMW1kbGU9PTo2N2YyNjEwZjExODI4MDUzYWI5ZTE4YmM1YzUwZjg4MQ==";
                string SmartVidProjectKey = "roptlfjgmtirsmd3l"; //"rkfltiwm4lw2paioxl
                var client = new RestClient("https://sydney.personalisedvideolab.com/api/render/1132");
                var request = new RestRequest(Method.POST);

                request.AddHeader("cache-control", "no-cache");
                request.AddHeader("Authorization", SmartVidAuthString);
                request.AddHeader("Content-Type", "application/json");

                string requestString;

                SmartVideoRequest svRequest = new SmartVideoRequest
                {
                    projectkey = SmartVidProjectKey
                };

                svRequest.variables = projVariables;

                requestString = JsonConvert.SerializeObject(svRequest,
                        Newtonsoft.Json.Formatting.Indented,
                        new JsonSerializerSettings
                        {
                            NullValueHandling = NullValueHandling.Ignore
                        });

                request.AddParameter("undefined", requestString, ParameterType.RequestBody);

                //EXECUTE
                IRestResponse response = client.Execute(request);

                //GET THE RESPONSE
                string responseText = response.Content;
                JavaScriptSerializer serializer = new JavaScriptSerializer();

                Console.WriteLine(responseText);

                JObject smartVidResponse = JObject.Parse(responseText);

                SmartVideoResponse ResponseObj = new SmartVideoResponse()
                {
                    Response = smartVidResponse["Response"].ToString(),
                    Receipt = smartVidResponse["Receipt"].ToString(),
                    Count = smartVidResponse["Count"].ToString(),
                };

                List<Tokens> tokens = new List<Tokens>();

                var tokenItems = smartVidResponse["Tokens"];

                foreach (var token in tokenItems)
                {
                    JProperty jProperty = token.ToObject<JProperty>();
                    string smartVidId = token.First().ToString();
                    string tokenStr = jProperty.Name;

                    int dbSmartVidId = Convert.ToInt32(tokenStr.Substring(tokenStr.IndexOf("_") + 1));

                    var thisVid = context.MatterWFSmartVidDetails.Where(v => v.MatterWFSmartVidDetailId == dbSmartVidId).FirstOrDefault();
                    thisVid.ResponseUrl = Slick_Domain.GlobalVars.GetGlobalTxtVar("SmartVidVideoBaseUrl") + smartVidId + ".mp4";
                    //thisVid.VideoSent = true;
                    thisVid.UpdatedDate = DateTime.Now;

                }
                context.SaveChanges();
                ResponseObj.Tokens = tokens;
            }
        }

        public static string CleanName(String name)
        {
            name = name.Trim();
            //Daniel "The Chang" Hwang's fault. Try and remove the second name if it looks like it's a middle name rather than a second half of a first name.
            if (name.Contains(" "))
            {
                if (name.Length > 12)
                {
                    int spaceIndex = name.IndexOf(" ");
                    name = name.Substring(0, spaceIndex);
                }
            }
            return name;
        }

        /// <summary>
        /// Adds a new entry to dbo.MatterWFSmartVidDetails with the data that gets gathered at book settlement milestone.
        /// Does not send the video, simply sets it up to be sent when the automatic scheduler runs.
        /// </summary>
        /// <param name="uow"></param>
        /// <param name="matter"></param>
        /// <param name="mwfComponentId"></param>
        /// <param name="lenderId"></param>
        /// <param name="smartVid"></param>
        /// <param name="borrower"></param>
        /// <returns></returns>
        public static bool AddNewVideo(UnitOfWork uow, int matterId, int mwfComponentId, int lenderId, MatterCustomEntities.MatterSmartVidView smartVid, MatterCustomEntities.SmartVidBorrower borrower)
        {


            var thisParty = uow.Context.MatterParties.Where(p => p.MatterPartyId == borrower.MatterPartyId).FirstOrDefault();

            MatterWFSmartVidDetail detail = new MatterWFSmartVidDetail
            {
                Enabled = true,
                VideoSent = false,
                LenderId = lenderId,
                MatterPartyId = thisParty.MatterPartyId,
                MatterWFComponentId = mwfComponentId,
                DisplayPostalAddress = /*smartVid.PostalAddress.Number + " " +*/
                                       smartVid.PostalAddresses.FirstOrDefault(x => x.PartyId == thisParty.MatterPartyId).Street + ", \n" +
                                       smartVid.PostalAddresses.FirstOrDefault(x => x.PartyId == thisParty.MatterPartyId).Suburb + ", " +
                                       smartVid.PostalAddresses.FirstOrDefault(x => x.PartyId == thisParty.MatterPartyId).Postcode,
                FundingChannelTypeId = smartVid.FundingChannelTypeId,
                DirectDebitBSB = null,
                DirectDebitAcc = null,
                UpdatedDate = DateTime.Now,
                UpdatedByUserId = GlobalVars.CurrentUser.UserId

            };

            uow.Save();


            List<MatterWFSmartVidDetailDirectDebit> debitLogs = new List<MatterWFSmartVidDetailDirectDebit>();

            if (smartVid.DirectDebitDetails.FirstOrDefault() != null)
            {
                detail.DirectDebitFrequencyTypeId = smartVid.DirectDebitDetails.FirstOrDefault().Frequency.Value;
            }
            else
            {
                detail.DirectDebitFrequencyTypeId = null;
            }

            if (detail.DirectDebitFrequencyTypeId == 0)
            {
                detail.DirectDebitFrequencyTypeId = null;
            }
            else
            {
                //Censor parts of the BSB
                foreach (var directDebitDetail in smartVid.DirectDebitDetails)
                {

                    debitLogs.Add(new MatterWFSmartVidDetailDirectDebit()
                    {
                        LoanAccountNo = directDebitDetail.LoanAccount,
                        AccountNo = directDebitDetail.Account,
                        BSB = directDebitDetail.BSB,
                        FrequencyTypeId = directDebitDetail.Frequency.Value
                    });
                    uow.Save();

                    if (directDebitDetail.BSB != null) directDebitDetail.BSB = "***-" + directDebitDetail.BSB.Substring(directDebitDetail.BSB.Length - 3, 3);

                    int accStart = directDebitDetail.Account.Length - 4 < 0 ? 0 : directDebitDetail.Account.Length - 4;


                    if (directDebitDetail.Account != null) directDebitDetail.Account = "**** " + directDebitDetail.Account.Substring(accStart, directDebitDetail.Account.Length - accStart);

                    detail.DirectDebitAcc = smartVid.DirectDebitDetails.Select(x => x.Account).FirstOrDefault();
                    detail.DirectDebitBSB = smartVid.DirectDebitDetails.Select(x => x.BSB).FirstOrDefault();

                    //current vid script supports a max of 2 account details so that's what's going on here.
                    if (smartVid.DirectDebitDetails.Count() > 1)
                    {

                        detail.DirectDebitAcc2 = smartVid.DirectDebitDetails.ElementAt(1).Account;
                        detail.DirectDebitBSB2 = smartVid.DirectDebitDetails.ElementAt(1).BSB;

                        //detail.DirectDebitAcc2 = smartVid.DirectDebitDetails.Select(x => x.Account).ToList()[1];
                        detail.DirectDebitAcc2 = "Account: " + "**** " + detail.DirectDebitAcc2.Substring(detail.DirectDebitAcc2.Length - 4, 4);
                        detail.DirectDebitBSB2 = "BSB: " + "***-" + detail.DirectDebitBSB2.Substring(detail.DirectDebitBSB2.Length - 3, 3);
                        //detail.DirectDebitBSB2 = smartVid.DirectDebitDetails.Select(x => x.BSB).ToList()[1];
                    }
                    else
                    {
                        detail.DirectDebitBSB2 = "Your";
                        detail.DirectDebitAcc2 = "account number";
                    }
                }
            }

            uow.Context.MatterWFSmartVidDetails.Add(detail);
            uow.Save();

            foreach (var debit in debitLogs)
            {
                debit.MatterWFSmartVidDetailId = detail.MatterWFSmartVidDetailId;
                uow.Context.MatterWFSmartVidDetailDirectDebits.Add(debit);
                uow.Save();
            }

            MatterWFSmartVidDetailParty smartvidParty = new MatterWFSmartVidDetailParty()
            {
                MatterWFSmartVidDetailId = detail.MatterWFSmartVidDetailId,
                MatterPartyId = thisParty.MatterPartyId,
                MatterPartyDisplayFirstname = CleanName(borrower.Firstname),
                MatterPartyDisplayLastname = borrower.Lastname,
                MatterPartySmartvidEmail = borrower.Email,
                MatterPartySmartvidMobile = borrower.Mobile,
            };

            uow.Context.MatterWFSmartVidDetailParties.Add(smartvidParty);
            uow.Save();


            try
            {
                uow.Save();
                GenerateIndividualVideo(detail.MatterWFSmartVidDetailId, uow.Context, matterId);
                return true;
            }
            catch (Exception e)
            {
                uow.RollbackTransaction();
                throw e;
            }
        }
        public class MatterPartyAddress
        {
            public string StreetAddress { get; set; }
            public string Suburb { get; set; }
            public string Postcode { get; set; }
            public int? StateId { get; set; }
            public string StateName { get; set; }
            public int AddressTypeId { get; set; }
          
        }
        public static bool AddNewAFSHVideo(UnitOfWork uow, int matterId, int mwfComponentId, int matterPartyId)
        {

            var thisParty = uow.Context.MatterParties.Where(p => p.MatterPartyId == matterPartyId)
                .Select(x=>new
                {
                    Firstname = x.DisplayFirstname ?? x.Firstname ?? x.Lastname,
                    x.Lastname,
                    x.MatterPartyId,
                    x.Email,
                    x.Mobile,
                    PostSettlementAddress = 
                    x.Matter.IsConstruction == true ? 
                        new MatterPartyAddress()
                        {
                            StreetAddress = x.StreetAddress,
                            Suburb = x.Suburb,
                            Postcode = x.Postcode,
                            AddressTypeId = 0,
                            StateName = x.State.StateName
                        }  
                    : x.MatterPartyOtherAddresses.Select(p=>new MatterPartyAddress()
                        {
                            StreetAddress = p.StreetAddress,
                            Suburb = p.Suburb,
                            Postcode = p.Postcode,
                            AddressTypeId = p.AddressTypeId,
                            StateName = p.State.StateName
                        }).FirstOrDefault(),

                })
                .FirstOrDefault();

            int seedIterator = 1;

            var newToken = SlickSecurity.RandomStringURLsafe(25, mwfComponentId * matterPartyId);

            while (uow.Context.MatterWFSmartVidDetails.Any(v => v.PageToken == newToken))
            {
                newToken = SlickSecurity.RandomStringURLsafe(25, mwfComponentId * matterPartyId + seedIterator);
                seedIterator++;
            }

            MatterWFSmartVidDetail detail = new MatterWFSmartVidDetail
            {
                Enabled = true,
                VideoSent = false,
                LenderId = 139,
                MatterPartyId = thisParty.MatterPartyId,
                MatterWFComponentId = mwfComponentId,
                DisplayPostalAddress = /*smartVid.PostalAddress.Number + " " +*/
                                       thisParty.PostSettlementAddress.StreetAddress + ", \n" +
                                       thisParty.PostSettlementAddress.Suburb + "," + "\n"+
                                       thisParty.PostSettlementAddress.StateName + " " +  thisParty.PostSettlementAddress.Postcode, 
                FundingChannelTypeId = (int)FundingChannelTypeEnum.FirstParty,
                DirectDebitBSB = null,
                DirectDebitAcc = null,
                UpdatedDate = DateTime.Now,
                UpdatedByUserId = GlobalVars.CurrentUser.UserId,
                PageToken = newToken,
                AccessCode = SlickSecurity.RandomNumericString(6),
                CheckedForSending = true
                

            };

            uow.Save();


            List<MatterWFSmartVidDetailDirectDebit> debitLogs = new List<MatterWFSmartVidDetailDirectDebit>();


            uow.Context.MatterWFSmartVidDetails.Add(detail);
            uow.Save();

            //foreach (var debit in debitLogs)
            //{
            //    debit.MatterWFSmartVidDetailId = detail.MatterWFSmartVidDetailId;
            //    uow.Context.MatterWFSmartVidDetailDirectDebits.Add(debit);
            //    uow.Save();
            //}


            MatterWFSmartVidDetailParty smartvidParty = new MatterWFSmartVidDetailParty()
            {
                MatterWFSmartVidDetailId = detail.MatterWFSmartVidDetailId,
                MatterPartyId = thisParty.MatterPartyId,
                MatterPartyDisplayFirstname = thisParty.Firstname,
                MatterPartyDisplayLastname = thisParty.Lastname,
                MatterPartySmartvidEmail = thisParty.Email,
                MatterPartySmartvidMobile = thisParty.Mobile,
            };

            uow.Context.MatterWFSmartVidDetailParties.Add(smartvidParty);
            uow.Save();


            try
            {
                uow.Save();
                GenerateIndividualAFSHVideo(detail.MatterWFSmartVidDetailId, uow.Context, matterPartyId);
                return true;
            }
            catch (Exception e)
            {
                uow.RollbackTransaction();
                throw e;
            }
        }
        public static bool AddNewAFSHVideoNewTransaction(int matterId, int mwfComponentId, int matterPartyId)
        {
            int matterWFSmartVidDetailId = 0;
            using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadCommitted))
            {
                var thisParty = uow.Context.MatterParties.Where(p => p.MatterPartyId == matterPartyId)
                    .Select(x => new
                    {
                        Firstname = x.DisplayFirstname ?? x.Firstname ?? x.Lastname,
                        x.Lastname,
                        x.MatterPartyId,
                        x.Email,
                        x.Mobile,
                        PostSettlementAddress =
                        x.Matter.IsConstruction == true ?
                            new MatterPartyAddress()
                            {
                                StreetAddress = x.StreetAddress,
                                Suburb = x.Suburb,
                                Postcode = x.Postcode,
                                AddressTypeId = 0,
                                StateName = x.State.StateName
                            }
                        : x.MatterPartyOtherAddresses.Select(p => new MatterPartyAddress()
                        {
                            StreetAddress = p.StreetAddress,
                            Suburb = p.Suburb,
                            Postcode = p.Postcode,
                            AddressTypeId = p.AddressTypeId,
                            StateName = p.State.StateName
                        }).FirstOrDefault(),

                    })
                    .FirstOrDefault();

                int seedIterator = 1;

                var newToken = SlickSecurity.RandomStringURLsafe(25, mwfComponentId * matterPartyId);

                while (uow.Context.MatterWFSmartVidDetails.Any(v => v.PageToken == newToken))
                {
                    newToken = SlickSecurity.RandomStringURLsafe(25, mwfComponentId * matterPartyId + seedIterator);
                    seedIterator++;
                }

                MatterWFSmartVidDetail detail = new MatterWFSmartVidDetail
                {
                    Enabled = true,
                    VideoSent = false,
                    LenderId = 139,
                    MatterPartyId = thisParty.MatterPartyId,
                    MatterWFComponentId = mwfComponentId,
                    DisplayPostalAddress = /*smartVid.PostalAddress.Number + " " +*/
                                           thisParty.PostSettlementAddress.StreetAddress + ", \n" +
                                           thisParty.PostSettlementAddress.Suburb + "," + "\n" +
                                           thisParty.PostSettlementAddress.StateName + " " + thisParty.PostSettlementAddress.Postcode,
                    FundingChannelTypeId = (int)FundingChannelTypeEnum.FirstParty,
                    DirectDebitBSB = null,
                    DirectDebitAcc = null,
                    UpdatedDate = DateTime.Now,
                    UpdatedByUserId = GlobalVars.CurrentUser.UserId,
                    PageToken = newToken,
                    AccessCode = SlickSecurity.RandomNumericString(6),
                    CheckedForSending = true


                };

                uow.Save();


                List<MatterWFSmartVidDetailDirectDebit> debitLogs = new List<MatterWFSmartVidDetailDirectDebit>();


                uow.Context.MatterWFSmartVidDetails.Add(detail);
                uow.Save();

                matterWFSmartVidDetailId = detail.MatterWFSmartVidDetailId;

                //foreach (var debit in debitLogs)
                //{
                //    debit.MatterWFSmartVidDetailId = detail.MatterWFSmartVidDetailId;
                //    uow.Context.MatterWFSmartVidDetailDirectDebits.Add(debit);
                //    uow.Save();
                //}


                MatterWFSmartVidDetailParty smartvidParty = new MatterWFSmartVidDetailParty()
                {
                    MatterWFSmartVidDetailId = detail.MatterWFSmartVidDetailId,
                    MatterPartyId = thisParty.MatterPartyId,
                    MatterPartyDisplayFirstname = thisParty.Firstname,
                    MatterPartyDisplayLastname = thisParty.Lastname,
                    MatterPartySmartvidEmail = thisParty.Email,
                    MatterPartySmartvidMobile = thisParty.Mobile,
                };

                uow.Context.MatterWFSmartVidDetailParties.Add(smartvidParty);
                uow.Save();
                uow.CommitTransaction();
            }
           
            try
            {
                GenerateIndividualAFSHVideoNewConnection(matterWFSmartVidDetailId, matterPartyId);
            }
            catch(Exception e)
            {
                Handlers.ErrorHandler.LogError(e);
                return false;
            }
            return true;
            
        
        }

        /// <summary>
        /// Iterate through dbo.SmartVidDetail, send all enabled videos that have not yet been sent. 
        /// Retrieve URLs and insert into same table, send out emails.
        /// TODO: Change to global variables, generalise for all lenders since this is very much latrobe limited right now. 
        /// </summary>
        /// <returns></returns>

        //public static bool DeprecatedSendSmartVideos()
        //{
        //    string SmartVidAuthString = ""; //"Basic bHAzZWw2cDhtYjQ1aHRmazcwMW1kbGU9PTo2N2YyNjEwZjExODI4MDUzYWI5ZTE4YmM1YzUwZjg4MQ==";
        //    string SmartVidProjectKey = "roptlfjgmtirsmd3l"; //"rkfltiwm4lw2paioxl
        //    var client = new RestClient("https://sydney.personalisedvideolab.com/api/render/1132");
        //    var request = new RestRequest(Method.POST);

        //    request.AddHeader("cache-control", "no-cache");
        //    request.AddHeader("Authorization", SmartVidAuthString);
        //    request.AddHeader("Content-Type", "application/json");

        //    string requestString;

        //    SmartVideoRequest svRequest = new SmartVideoRequest
        //    {
        //        projectkey = SmartVidProjectKey
        //    };

        //    List<DeprecatedSmartVideoRequestVariable> projVariables = new List<DeprecatedSmartVideoRequestVariable>();


        //    using (var context = new SlickContext())
        //    {
        //        var smartVidList = context.MatterWFSmartVidDetails.Where(v => v.Enabled == true && v.VideoSent == false);
        //        foreach (var vid in smartVidList)
        //        {
        //            //Send vids when given variable names
        //            DeprecatedSmartVideoRequestVariable projVariable = new DeprecatedSmartVideoRequestVariable
        //            {
        //                id = vid.MatterWFSmartVidDetailId.ToString(),
        //            };
        //        }
        //    }

        //    //svRequest.variables = projVariables;

        //    requestString = JsonConvert.SerializeObject(svRequest,
        //                    Newtonsoft.Json.Formatting.Indented,
        //                    new JsonSerializerSettings
        //                    {
        //                        NullValueHandling = NullValueHandling.Ignore
        //                    });

        //    request.AddParameter("undefined", requestString, ParameterType.RequestBody);

        //    //EXECUTE
        //    IRestResponse response = client.Execute(request);

        //    //GET THE RESPONSE
        //    string responseText = response.Content;
        //    JavaScriptSerializer serializer = new JavaScriptSerializer();

        //    Console.WriteLine(responseText);

        //    JObject smartVidResponse = JObject.Parse(responseText);

        //    SmartVideoResponse ResponseObj = new SmartVideoResponse()
        //    {
        //        Response = smartVidResponse["Response"].ToString(),
        //        Receipt = smartVidResponse["Receipt"].ToString(),
        //        Count = smartVidResponse["Count"].ToString(),
        //    };

        //    List<Tokens> tokens = new List<Tokens>();

        //    var tokenItems = smartVidResponse["Tokens"];
        //    using (var context = new SlickContext())
        //    {
        //        foreach (var token in tokenItems)
        //        {
        //            JProperty jProperty = token.ToObject<JProperty>();
        //            string smartVidId = token.First().ToString();
        //            tokens.Add(new Tokens
        //            {
        //                smartvidId = jProperty.Name,
        //                token = smartVidId
        //            });
        //            var thisVid = context.MatterWFSmartVidDetails.Where(v => v.MatterWFSmartVidDetailId == Convert.ToInt32(smartVidId)).FirstOrDefault();
        //            thisVid.ResponseUrl = Slick_Domain.GlobalVars.GetGlobalTxtVar("SmartVidVideoBaseUrl") + smartVidId + ".mp4";
        //            //thisVid.VideoSent = true;
        //            //SEND THE EMAIL HERE
        //        }
        //        context.SaveChanges();
        //    }

        //    ResponseObj.Tokens = tokens;
        //    return true;


        //}

    }
}
