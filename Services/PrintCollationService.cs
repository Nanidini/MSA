using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Slick_Domain.Entities;
using Slick_Domain.Enums;
using Slick_Domain.Models;
using Slick_Domain.Common;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using NLog;



namespace Slick_Domain.Services
{
    //class PrintCollationService
    //{

    //    public class PrintQueue
    //    {
    //        public List<DocumentPrintJob> BorrowerSignDocs { get; set; }
    //        public List<DocumentPrintJob> BorrowerCopyDocs { get; set; }
    //        public List<DocumentPrintJob> CoBorrowerDocs { get; set; }
    //        public List<DocumentPrintJob> BrokerDocs { get; set; }
    //        public List<DocumentPrintJob> BranchDocs { get; set; }
    //        public List<DocumentPrintJob> FileDocs { get; set; }
    //    }

    //    public class DocumentPrintJob
    //    {
    //        public Byte[] Document { get; set; }
    //        public int Copies { get; set; }
    //    }
    //    public static bool PrintMatterDocuments(MatterCustomEntities.MatterView mt)
    //    {
    //        var mtType = mt.MatterTypeId;
    //        using (var context = new SlickContext())
    //        {
    //            var docs = context.MatterDocuments.Select(m => new
    //            {
    //                m.MatterId,
    //                m.DocumentMaster.Documents.FirstOrDefault(d => d.IsLatestVersion).DocumentId,
    //                m.DocumentMaster.DocName,
    //                m.DocumentMaster.DocType
    //            }
    //            ).Where(m => m.MatterId == mt.MatterId).ToList();


    //            PrintQueue printQueue = new PrintQueue
    //            {
    //                BorrowerSignDocs = new List<DocumentPrintJob>(),
    //                BorrowerCopyDocs = new List<DocumentPrintJob>(),
    //                CoBorrowerDocs = new List<DocumentPrintJob>(),
    //                BrokerDocs = new List<DocumentPrintJob>(),
    //                BranchDocs = new List<DocumentPrintJob>(),
    //                FileDocs = new List<DocumentPrintJob>(),
    //            };

    //            var lenderPrintRepo = context.LenderPrintCollation.Where(lp => lp.LenderId == mt.MatterId);

    //            foreach (var doc in docs)
    //            {

    //                var docPrintColRepo = context.DocumentCollationInfo.Where(d => d.DocName == doc.DocumentMaster.DocName).FirstOrDefault();

    //                if (mt.MatterTypeId == (int)Enums.MatterTypeEnum.Increase && !docPrintColRepo.SendIncrease)
    //                    break;

    //                else if (mt.MatterTypeId == (int)Enums.MatterTypeEnum.Purchase && !docPrintColRepo.SendPurchase)
    //                    break;

    //                else if (mt.MatterTypeId == (int)Enums.MatterTypeEnum.FastRefinance && !docPrintColRepo.SendFastREFI)
    //                    break;

    //                else if (mt.MatterTypeId == (int)Enums.MatterTypeEnum.ClearTitle && !docPrintColRepo.SendClearTitle)
    //                    break;

    //                var path = Path.Combine(GlobalVars.GetGlobalTxtVar(DomainConstants.MatterDocumentsDirectory), mt.MatterId.ToString());

    //                var fileImage = DocumentExport.StreamToPDF(doc.DocName, doc.DocType, path);

    //                var lpInfo = lenderPrintRepo.Where(lp => lp.DocCollateId == docPrintColRepo.DocCollateId).FirstOrDefault();

    //                if (lpInfo.CopiesBorrowerSign > 0 && lpInfo.SendDocument)
    //                {
    //                    DocumentPrintJob BorrowerSignPrintJob = new DocumentPrintJob
    //                    {
    //                        Document = fileImage,
    //                        Copies = lpInfo.CopiesBorrowerSign
    //                    };
    //                    printQueue.BorrowerSignDocs.Add(BorrowerSignPrintJob);
    //                }

    //                if (lpInfo.CopiesBorrowerCopy > 0 && lpInfo.SendDocument)
    //                {
    //                    DocumentPrintJob BorrowerCopyPrintJob = new DocumentPrintJob
    //                    {
    //                        Document = fileImage,
    //                        Copies = lpInfo.CopiesBorrowerCopy
    //                    };
    //                    printQueue.BorrowerCopyDocs.Add(BorrowerCopyPrintJob);
    //                }

    //                if (lpInfo.CopiesCoBorrower > 0 && lpInfo.SendDocument)
    //                {
    //                    DocumentPrintJob CoBorrowerPrintJob = new DocumentPrintJob
    //                    {
    //                        Document = fileImage,
    //                        Copies = lpInfo.CopiesCoBorrower
    //                    };
    //                    printQueue.CoBorrowerDocs.Add(CoBorrowerPrintJob);
    //                }

    //                if (lpInfo.CopiesBroker > 0 && lpInfo.SendDocument)
    //                {
    //                    DocumentPrintJob BrokerPrintJob = new DocumentPrintJob
    //                    {
    //                        Document = fileImage,
    //                        Copies = lpInfo.CopiesBroker
    //                    };
    //                    printQueue.BrokerDocs.Add(BrokerPrintJob);
    //                }

    //                if (lpInfo.CopiesBranch > 0 && lpInfo.SendDocument)
    //                {
    //                    DocumentPrintJob BranchPrintJob = new DocumentPrintJob
    //                    {
    //                        Document = fileImage,
    //                        Copies = lpInfo.CopiesBranch
    //                    };
    //                    printQueue.BranchDocs.Add(BranchPrintJob);
    //                }

    //                if (lpInfo.CopiesFile > 0 && lpInfo.SendDocument)
    //                {
    //                    DocumentPrintJob FilePrintJob = new DocumentPrintJob
    //                    {
    //                        Document = fileImage,
    //                        Copies = lpInfo.CopiesFile
    //                    };
    //                    printQueue.BranchDocs.Add(FilePrintJob);
    //                }



    //            }

    //            /*
    //            Print All button gets pressed:

    //            get all LenderPrintCollation items with LenderId matching matter LenderId
    //            get all DocPrintCollation items from the DocCollateIds for all LenderPrint items
    //                where senddocument not null and this is a mattertype that needs to be sent
        
    //            setup List DocsToPrint of type DocPrintInformation{ LenderPrintCollation,DocPrintCollation} with the relevant information from these two tables

    //            setup Lists for BorrowerDocs, BorrowerCopyDocs, CoBorrowerDocs, BrokerDocs, BranchDocs, FileDocs

    //            For each document to print:

    //                    What type of matter is this ? If the matter type needs to be printed:

    //                        Add number of copies of particular document to list for that particular party


    //            Next document


    //            Once all documents have been added to the temporary queues


    //            For each of the above lists, print in order.

    //            */
    //        }
    //        return true;

    //        return false;

    //    }


    //}
}
