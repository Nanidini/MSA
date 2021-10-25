using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using Slick_Domain.Entities;
using Slick_Domain.Models;
using Slick_Domain.Common;

namespace Slick_Domain.Services
{   
    /// <summary>
    /// Backchannel Service Class. Used for sending backchannel messages. 
    /// </summary>
    /// <remarks>
    /// Currently there are no standardise enums or values for stage code, consider revising. 
    /// </remarks>
    public class BackChannelService
    {
        /// <summary>
        /// A method for PREPARING backchannel messages to be sent. The function creates an XML, stores it and logs it to be sent later.
        /// </summary>
        /// <param name="mwfComp">Matter Workflow Component that is being completed.</param>
        /// <param name="stageCode">Stage code of the Matter Workflow Component</param>
        /// <returns>True if succesful (and current true if error)</returns>
        /// <remarks>
        /// Refactor Candidate - Currently, if an error occurs it logs the error but still returns true! Oh oh.
        /// Log is made in local time instead of utc?
        /// </remarks>
        public static bool SendBackChannelMessage(MatterCustomEntities.MatterWFComponentView mwfComp, string stageCode)
        {
            string schemaPath = GlobalVars.GetGlobalTxtVar("BackChannelSchemaPath");
            string outputPath = GlobalVars.GetGlobalTxtVar("BackChannelFolderPath");

            string xmlSchema;

            using (StreamReader sr = new StreamReader(schemaPath))
            {
                xmlSchema = sr.ReadToEnd();
            }

            string lenderRef;
            string clientId; 

            using (var context = new SlickContext())
            {
                    
                if (context.Matters.Where(m => m.MatterId == mwfComp.MatterId).Select(m => m.LenderRefNo).FirstOrDefault() == null)
                    throw (new NullReferenceException("No lender reference on file - backchannel message could not be sent."));

                if (context.Lenders.FirstOrDefault(l => l.LenderId == mwfComp.LenderId).SecondaryRefRequired)
                {
                    if (context.Matters.FirstOrDefault(m => m.MatterId == mwfComp.MatterId).SecondaryRefNo != null)
                        lenderRef = context.Matters.Where(m => m.MatterId == mwfComp.MatterId).Select(m => m.SecondaryRefNo).FirstOrDefault().ToString();
                    else
                        lenderRef = context.Matters.Where(m => m.MatterId == mwfComp.MatterId).Select(m => m.LenderRefNo).FirstOrDefault().ToString();
                }
                else
                {
                    lenderRef = context.Matters.Where(m => m.MatterId == mwfComp.MatterId).Select(m => m.LenderRefNo).FirstOrDefault().ToString();
                }
                clientId = context.Matters.Where(m => m.MatterId == mwfComp.MatterId).Select(m => m.Lender.LenderName).FirstOrDefault().ToString().ToUpper();


            }

            string day = DateTime.Now.Day.ToString();
            string month = DateTime.Now.Month.ToString();
            string year = DateTime.Now.Year.ToString();
            string hour = DateTime.Now.Hour.ToString();
            string minute = DateTime.Now.Minute.ToString();
            string second = DateTime.Now.Second.ToString();

            string oxIdentityString = "";
            string recipientString = "";

            if(mwfComp.LenderId == 139)
            {
                oxIdentityString = "9";
                recipientString = "ADVANTEDGE";
            }
            if(mwfComp.LenderId == 166)
            {
                oxIdentityString = "10";
                recipientString = "LA TROBE FINANCIAL";
            }

            string xmlOutput = xmlSchema.Replace("{OXIDENTITY}", oxIdentityString)
                                        .Replace("{RECIPIENT}", recipientString)
                                        .Replace("{LENDER_LOANID}", lenderRef)
                                        .Replace("{YEAR_STAMP}", year)
                                        .Replace("{MONTH_STAMP}", month)
                                        .Replace("{DAY_STAMP}", day)
                                        .Replace("{HOUR_STAMP}", hour)
                                        .Replace("{MINUTE_STAMP}", minute)
                                        .Replace("{SECOND_STAMP}", second)
                                        .Replace("{OP_STAGECODE}", stageCode);



            BackChannelLog log = new BackChannelLog();

            using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadCommitted))
            {
                try
                {
                    log = new BackChannelLog()
                    {
                        ClientID = clientId,
                        CreatedDate = DateTime.Now,
                        xmlContent = xmlOutput,
                        MatterID = mwfComp.MatterId,
                        Status = "",
                        UpdateDate = null,
                        ProcessDetails = "",
                        MatterWFComponentId = mwfComp.MatterWFComponentId,
                        UpdatedDate = DateTime.Now,
                        UpdatedByUserId = GlobalVars.CurrentUser.UserId,
                    };



                    uow.Context.BackChannelLogs.Add(log);
                    uow.CommitTransaction();

                    //var messages = uow.Context.BackChannelLogs.Where(m => m.MatterWFComponentId == mwfComp.MatterWFComponentId && m.Status == "");
                    //if(messages!=null)
                    //    MessageID = uow.Context.BackChannelLogs.Where(m=>m.MatterWFComponentId == mwfComp.MatterWFComponentId && m.Status == "").FirstOrDefault().MessageId; 
                    //get our new message id for use in file name
                }
                catch (Exception e)
                {
                    Handlers.ErrorHandler.LogError(e);
                    uow.RollbackTransaction();
                }
            }




            //outputPath += $"\\{clientId}_{MessageID}.xml";

            ////string savedXml = $"<client>{clientId}</client>\n<messageid>{MessageID}</messageid>";
            //string savedXml = $"{MessageID}";

            //if (!File.Exists(outputPath))
            //{
            //    File.WriteAllText(outputPath, savedXml);
            //}

            return true;
        }

        /// <summary>
        /// A Function that PREPARES a message to be sent via backchanneling service when a matter is placed on hold. 
        /// </summary>
        /// <param name="lenderRef">The Lender Reference for the Matter.</param>
        /// <param name="stageCode">The BackChannel Stage Code.</param>
        /// <param name="matterId">The Matter Id in string form.</param>
        /// <param name="clientId">The Matter Client Name in string form, to upper?</param>
        /// <returns>True if successful</returns>
        /// <remarks>
        /// Currently no try catch block, no way it ever returns false. 
        /// </remarks>
        public static bool SendBackChannelOnHoldMessage(string lenderRef, string stageCode, int matterId, string clientId)
        {
            string schemaPath = GlobalVars.GetGlobalTxtVar("BackChannelSchemaPath");
            string outputPath = GlobalVars.GetGlobalTxtVar("BackChannelFolderPath");
            string xmlSchema;

            using (StreamReader sr = new StreamReader(schemaPath))
            {
                xmlSchema = sr.ReadToEnd();
            }

            string day = DateTime.Now.Day.ToString();
            string month = DateTime.Now.Month.ToString();
            string year = DateTime.Now.Year.ToString();
            string hour = DateTime.Now.Hour.ToString();
            string minute = DateTime.Now.Minute.ToString();
            string second = DateTime.Now.Second.ToString();

            string xmlOutput = xmlSchema.Replace("{LENDER_LOANID}", lenderRef)
                                        .Replace("{YEAR_STAMP}", year)
                                        .Replace("{MONTH_STAMP}", month)
                                        .Replace("{DAY_STAMP}", day)
                                        .Replace("{HOUR_STAMP}", hour)
                                        .Replace("{MINUTE_STAMP}", minute)
                                        .Replace("{SECOND_STAMP}", second)
                                        .Replace("{OP_STAGECODE}", stageCode);



            BackChannelLog log = new BackChannelLog();

            int MessageID = 0;

            

            using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadCommitted))
            {
                int latestMWFid = 1;

                var latestwfComp = uow.Context.MatterWFComponents.Where(w => w.MatterId == matterId && 
                    (w.WFComponentStatusTypeId == (int)Enums.MatterWFComponentStatusTypeEnum.InProgress || w.WFComponentStatusTypeId == (int)Enums.MatterWFComponentStatusTypeEnum.OnHold)
                    ).OrderByDescending(w => w.MatterWFComponentId).FirstOrDefault();

                if (latestwfComp != null)
                {
                    latestMWFid = latestwfComp.MatterWFComponentId;
                }

                log = new BackChannelLog()
                {
                    ClientID = clientId,
                    CreatedDate = DateTime.Now,
                    xmlContent = xmlOutput,
                    MatterID = matterId,
                    Status = "",
                    UpdateDate = null,
                    ProcessDetails = "",
                    MatterWFComponentId = latestMWFid,
                    UpdatedDate = DateTime.Now,
                    UpdatedByUserId = GlobalVars.CurrentUser.UserId,

                };

                uow.Context.BackChannelLogs.Add(log);
                uow.CommitTransaction();
                var messages = uow.Context.BackChannelLogs.Where(m => m.MatterWFComponentId == latestMWFid && m.Status == "" && m.MatterID == matterId).OrderByDescending(m => m.CreatedDate);
                if (messages!=null)
                    MessageID = uow.Context.BackChannelLogs.Where(m => m.MatterWFComponentId == latestMWFid && m.Status == "" && m.MatterID == matterId).OrderByDescending(m=>m.CreatedDate).FirstOrDefault().MessageId;
            }


            outputPath += $"\\{clientId}_{MessageID}.xml";

            string savedXml = $"{MessageID}";
            try
            {
                if (!File.Exists(outputPath))
                {
                    File.WriteAllText(outputPath, savedXml);
                }
            }catch(Exception e)
            {
                Handlers.ErrorHandler.LogError(e);
            }


            return true;
        }










    }
}
