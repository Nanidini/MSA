
using Slick_Domain.Models;
using System;
using System.IO;
using System.Linq;
using Slick_Domain.Entities;
using System.Collections.Generic;
using Slick_Domain.Common;
using Slick_Domain.Enums;
using System.Threading.Tasks;
using Slick_Domain.Interfaces;
using System.IO.Packaging;
using Ionic.Zip;

namespace Slick_Domain.Services
{
    /// <summary>
    /// The documents repository class. Responsible for getting documens. Not however that alot of it actually interacts with MatterDocuments
    /// </summary>
    public class DocumentsRepository : SlickRepository
    {
        public DocumentsRepository(SlickContext context) : base(context)
        {

        }

        public bool DocumentIsArchived(int documentMasterId)
        {
            return context.DocumentMasters.First(x => x.DocumentMasterId == documentMasterId).ArchiveStatusTypeId == (int)Slick_Domain.Enums.ArchiveStatusTypeId.Archived;
        }

        public bool ArchiveDocument(int documentMasterId, string pathTo, string pathFrom, ICaller caller)
        {
            return ActionArchiveRequestForDocument(documentMasterId, pathTo, pathFrom, caller, ArchiveStatusTypeId.Archived);
        }

        public bool UnarchiveDocument(int documentMasterId, string pathTo, string pathFrom, ICaller caller)
        {
            return ActionArchiveRequestForDocument(documentMasterId, pathTo, pathFrom, caller, ArchiveStatusTypeId.NotArchived);
        }
        private bool ActionArchiveRequestForDocument(int documentMasterId, string pathTo, string pathFrom, ICaller caller, Slick_Domain.Enums.ArchiveStatusTypeId archiveStatustType)
        {
            bool success = true;
            if (!caller.CallerId.Equals(Slick_Domain.Common.DomainConstants.ArchiveCallerId))
            {
                throw new Exception("Only Archive Web Service can call this function.");
            }
            bool docIsArchived = DocumentIsArchived(documentMasterId);
            if (docIsArchived && archiveStatustType == ArchiveStatusTypeId.Archived)
            {
                throw new Exception("Document is already archived");

            }
            else if (!docIsArchived && ArchiveStatusTypeId.NotArchived == archiveStatustType)
            {
                throw new Exception("Document is already unarchived");
            }

            DocumentMaster documentMaster = context.DocumentMasters.First(x => x.DocumentMasterId == documentMasterId);

            List<Document> succesfullyMovedDocs = new List<Document>();
            try
            {
                documentMaster.Documents.ToList().ForEach(x =>
                {
                    string moveDocPath = pathTo + @"\" + x.DocumentId.ToString() + "." + x.DocumentMaster.DocType;
                    string currentDocPath = pathFrom + @"\" + x.DocumentId.ToString() + "." + x.DocumentMaster.DocType;
                    if (File.Exists(currentDocPath))
                        File.Move(currentDocPath, moveDocPath);
                });
            }
            catch (Exception e)
            {
                succesfullyMovedDocs.ForEach(x =>
                {
                    string moveDocPath = pathTo + @"\" + x.DocumentId.ToString() + "." + x.DocumentMaster.DocType;
                    string currentDocPath = pathFrom + @"\" + x.DocumentId.ToString() + "." + x.DocumentMaster.DocType;
                    File.Move(moveDocPath, currentDocPath);
                });
                success = false;
            }

            if (success)
            {
                documentMaster.ArchiveStatusTypeId = (int)archiveStatustType;
                documentMaster.ArchiveStatusUpdatedDate = System.DateTime.Now;
                context.SaveChanges();
            }
            return success;
        }

        /// <summary>
        /// Gets the Custody Document Views for a Matter.
        /// </summary>
        /// <param name="matterId">The <see cref="Matter.MatterId"/> of the Matter to be getting the documents for.</param>
        /// <returns>An Enumerable Collection of Matter Documents Custody View.</returns>
        public IEnumerable<MatterCustomEntities.MatterDocumentsCustodyView> GetMatterDocumentsCustodyViews(int matterId)
        {
            return GetDocumentCustodyGrid(context.MatterDocuments.AsNoTracking().Where(x => x.MatterId == matterId));
        }
        /// <summary>
        /// Gets the Document Views for a Matter.
        /// </summary>
        /// <param name="matterId">The <see cref="Matter.MatterId"/> of the Matter to be getting the documents for.</param>
        /// <returns>An Enumerable Collection of Matter Documents View.</returns>
        public IEnumerable<MatterCustomEntities.MatterDocumentsView> GetAllDocumentsForMatter(int matterId)
        {
            //return GetDocuments(context.MatterDocuments.AsNoTracking().Where(x => x.MatterId == matterId));
            return GetDocumentsSproc(matterId);
        }

        public MatterCustomEntities.MatterDocumentsView GetMatterDocumentView(int matterDocumentId)
        {
            return GetDocuments(context.MatterDocuments.AsNoTracking().Where(x => x.MatterDocumentId == matterDocumentId)).FirstOrDefault();
        }
        public bool DoesDocumentExistAlready(int matterId, string fileName)
        {
            return context.MatterDocuments.Any(d => d.MatterId == matterId && d.DocumentMaster.DocName == fileName);
        }
        /// <summary>
        /// Gets the Document Views for a Matter based on the <see cref="DocumentDisplayAreaEnum"/>.
        /// </summary>
        /// <param name="matterId">The <see cref="Matter.MatterId"/> of the Matter to be getting the documents for.</param>
        /// <param name="documentDisplayAreaTypeId">The <see cref="DocumentDisplayAreaEnum"/> converted to integer form.</param>
        /// <returns>An Enumerable Collection of Matter Documents Custody View.</returns>
        public IEnumerable<MatterCustomEntities.MatterDocumentsView> GetDocumentsForMatter(int matterId, int documentDisplayAreaTypeId)
        {
            //return GetDocuments(context.MatterDocuments.AsNoTracking().Where(x => x.MatterId == matterId && x.DocumentDisplayAreaTypeId == documentDisplayAreaTypeId));
            return GetDocumentsSproc(matterId, documentDisplayAreaTypeId, filterDeleted: false);

            //return context.MatterDocuments.AsNoTracking().Where(m => m.MatterId == id && m.DocumentDisplayAreaTypeId == documentDisplayAreaTypeId)
            //        .Select(m => new { m.MatterDocumentId, m.MatterId, m.DocumentMasterId,
            //                            docData = m.DocumentMaster.Documents.Where(x => x.IsLatestVersion)
            //                                                        .Select(d => new { d.DocumentId, d.DocModDate, d.VersionNotes, d.User.Username, d.UpdatedDate }).FirstOrDefault(),
            //                           m.DocumentMaster.DocName, m.DocumentMaster.DocType, m.DocumentDisplayAreaTypeId, m.IsDeleted })
            // .ToList()
            // .Select(m => new MatterCustomEntities.MatterDocumentsView
            // {
            //     MatterDocumentId = m.MatterDocumentId,
            //     MatterId = m.MatterId,
            //     DocumentMasterId = m.DocumentMasterId,
            //     DocumentId = m.docData.DocumentId,
            //     DocName = m.DocName,
            //     DocType = m.DocType,
            //     DocModDate = m.docData.DocModDate,
            //     DocumentDisplayAreaTypeId = m.DocumentDisplayAreaTypeId,
            //     VersionNotes = m.docData.VersionNotes,
            //     UpdatedByUsername = m.docData.Username,
            //     UpdatedDate = m.docData.UpdatedDate,
            //     IsDeleted = m.IsDeleted,
            //     IsWordDoc = Common.EntityHelper.IsWordDoc(m.DocType)
            // })
            // .DistinctBy(x => x.DocumentMasterId)
            // .ToList();
        }
        /// <summary>
        /// Gets the documents that aren't deleted for a matter.
        /// </summary>
        /// <param name="matterId">The <see cref="Matter.MatterId"/> to filter by.</param>
        /// <returns>Returns an enumerable collection of documents for a matter that aren't deleited</returns>
        public IEnumerable<MatterCustomEntities.MatterDocumentsView> GetDocuments(int matterId, bool hideDeleted = true)
        {
            //return GetDocuments(context.MatterDocuments.AsNoTracking().Where(x => x.MatterId == matterId && !x.IsDeleted));
            return GetDocumentsSproc(matterId, filterDeleted: hideDeleted);

        }
        /// <summary>
        /// Asynchronous get of the documents that aren't deleted for a matter.
        /// </summary>
        /// <param name="matterId">The <see cref="Matter.MatterId"/> of the Matter to filter by.</param>
        /// <returns>Asynchronously returns an Enumerable collection of matter documents views</returns>
        public async Task<IEnumerable<MatterCustomEntities.MatterDocumentsView>> GetDocumentsAsync(int matterId)
        {
            return await GetDocumentsAsync(context.MatterDocuments.AsNoTracking().Where(x => x.MatterId == matterId && !x.IsDeleted));
        }
        /// <summary>
        /// Gets the documents for a matters certain display area, where <see cref="MatterCustomEntities.MatterDocumentsView.IsDeleted"/> is false.
        /// </summary>
        /// <param name="matterId">The <see cref="Matter.MatterId"/> of the matter</param>
        /// <param name="docDisplayAreaId">The <see cref="DocumentDisplayAreaEnum"/> to filter by.</param>
        /// <returns>An Enumerable Collection of documents that are filtered by the params and where <see cref="MatterCustomEntities.MatterDocumentsView.IsDeleted"/> is false.</returns>
        public IEnumerable<MatterCustomEntities.MatterDocumentsView> GetDocuments(int matterId, int docDisplayAreaId)
        {
            //return GetDocuments(context.MatterDocuments.AsNoTracking().Where(x=> x.MatterId == matterId && x.DocumentDisplayAreaTypeId == docDisplayAreaId && !x.IsDeleted));
            return GetDocumentsSproc(matterId, docDisplayAreaId);
        }
        /// <summary>
        /// Asynchronously gets the document views for <paramref name="docs"/> objects provided.
        /// </summary>
        /// <param name="docs">The Matter Documents to create views for.</param>
        /// <returns>Returns a document view collection asynchronously.</returns>
        private async Task<IEnumerable<MatterCustomEntities.MatterDocumentsView>> GetDocumentsAsync(IQueryable<MatterDocument> docs)
        {
            await Task.Delay(2000);
            var ret = docs.Select(m => new
            {
                m.MatterId,
                m.MatterDocumentId,
                m.DocumentMasterId,
                m.IsPublicVisible,
                m.DocumentStageId,
                docMaster = m.DocumentMaster,
                docData = m.DocumentMaster.Documents
                                           .Where(x => x.IsLatestVersion)
                                           .Select(d => new { d.DocumentId, d.DocModDate, d.VersionNotes, d.User.Username, d.UpdatedDate }).FirstOrDefault(),
                docVersions = m.DocumentMaster.Documents.Count(),
                m.DocumentDisplayAreaTypeId,
                m.DocumentDisplayAreaType.DocumentDisplayAreaTypeName,
                m.WFComponentId,
                m.WFComponent.WFComponentName,
                m.UpdatedDate,
                m.UpdatedByUserId,
                Location = m.MatterDocumentLocations.Where(x => x.Enabled).Select(x => new { x.Enabled, x.DocumentLocationId, x.DocumentLocation.DisplayName, x.DocumentLocation.StreetAddress, x.DocumentLocation.State.StateName, x.DocumentLocation.Postcode }).FirstOrDefault(),
                UpdatedByUsername = m.User.Username,
                m.IsDeleted
            })
             .ToList()
             .Select(m => new MatterCustomEntities.MatterDocumentsView
             {

                 MatterId = m.MatterId,
                 MatterDocumentId = m.MatterDocumentId,
                 DocumentMasterId = m.DocumentMasterId,
                 DocumentId = m.docData.DocumentId,
                 DocName = m.docMaster.DocName,
                 DocType = m.docMaster.DocType,
                 IsEmail = m.docMaster.DocType?.ToUpper() == "EML" || m.docMaster.DocType?.ToUpper() == "MSG",
                 DocDisplayName = GetDocDisplayName(m.docMaster.DocName, m.docVersions),
                 DocModDate = m.docData.DocModDate,
                 DocumentDisplayAreaTypeId = m.DocumentDisplayAreaTypeId,
                 DocumentDisplayAreaTypeName = m.DocumentDisplayAreaTypeName,
                 WFComponentId = m.WFComponentId,
                 WFComponentName = m.WFComponentName,
                 VersionNotes = m.docData.VersionNotes,
                 UpdatedDate = m.UpdatedDate,
                 UpdatedByUserId = m.UpdatedByUserId,
                 UpdatedByUsername = m.UpdatedByUsername,
                 IsDeleted = m.IsDeleted,
                 DocSelected = false,
                 DocVersion = m.docVersions,
                 IsWordDoc = Common.EntityHelper.IsWordDoc(m.docMaster.DocType),
                 IsPublic = m.IsPublicVisible,
                 DocumentStageId = m.DocumentStageId,
                 DocumentLocationId = m.Location != null ? m.Location.DocumentLocationId : null,
                 DocumentLocationName = m.Location != null ?
                    m.Location.DisplayName + "(" + m.Location.StreetAddress + m.Location.StateName + m.Location.Postcode + ")"
                    : null

             })
             .DistinctBy(x => x.DocumentMasterId)
             .ToList();

            return ret;
        }



        private IEnumerable<MatterCustomEntities.MatterDocumentsView> GetDocumentsSproc(int matterId, int? documentDisplayAreaTypeId = null, bool? filterDeleted = null)
        {
            return context.sp_Slick_GetMatterDocuments(matterId, documentDisplayAreaTypeId, filterDeleted)
                .Select(m => new MatterCustomEntities.MatterDocumentsView
                {

                    MatterId = m.MatterId,
                    MatterDocumentId = m.MatterDocumentId,
                    DocumentMasterId = m.DocumentMasterId,
                    DocumentId = m.DocumentId,
                    DocName = m.DocName,
                    DocType = m.DocType,
                    IsEmail = m.DocType?.ToUpper() == "EML" || m.DocType?.ToUpper() == "MSG",
                    DocDisplayName = GetDocDisplayName(m.DocName, m.DocVersions ?? 1),
                    DocModDate = m.DocModDate,
                    DocumentDisplayAreaTypeId = m.DocumentDisplayAreaTypeId,
                    DocumentDisplayAreaTypeName = m.DocumentDisplayAreaTypeName,
                    WFComponentId = m.WFComponentId,
                    WFComponentName = m.WFComponentName,
                    VersionNotes = m.VersionNotes,
                    UpdatedDate = m.DocumentUpdatedDate < m.MatterDocumentUpdatedDate ? m.MatterDocumentUpdatedDate : m.DocumentUpdatedDate,
                    UpdatedByUserId = m.UpdatedByUserId,
                    UpdatedByUsername = m.UpdatedUsername,
                    IsDeleted = m.IsDeleted,
                    DocSelected = false,
                    DocVersion = m.DocVersions,
                    IsWordDoc = Common.EntityHelper.IsWordDoc(m.DocType),
                    IsPublic = m.IsPublicVisible,
                    DocumentStageId = m.DocumentStageId,
                    DocumentLocationId = m.DocumentLocationId,
                    DocumentLocationName = m.DocumentLocationId.HasValue ?
                    m.DocumentLocationDisplayName + "(" + m.StreetAddress + m.StateName + m.Postcode + ")"
                    : null,
                    QASettlementRequired = m.QASettlementRequired,
                    QADocPrepRequired = m.QADocPrepRequired,
                    QASettlementInstructionsRequired = m.QASettlementInstructionsRequired,
                    LockedByLender = m.LockedByLender,
                    FromLoantrak = m.FromLoantrak,
                    ScannedDocument = m.IsScannedOriginal,
                    ScannedByUserId = m.ScannedByUserId,
                    ScannedByUsername = m.ScannedByUsername,
                    ScannedByStateId = m.ScannedByUserStateId

                }).GroupBy(x => x.DocumentMasterId)
                 .Select(x => x.OrderByDescending(o => o.UpdatedDate).First())
                 .OrderByDescending(x => x.UpdatedDate)
                 .ToList();
        }


        /// <summary>
        /// Linearlly gets the document views for <paramref name="docs"/> objects provided.
        /// </summary>
        /// <param name="docs">The Matter Documents to create views for.</param>
        /// <returns>Returns a document view collection.</returns>
        private IEnumerable<MatterCustomEntities.MatterDocumentsView> GetDocuments(IQueryable<MatterDocument> docs)
        {
            var ret = docs.Select(m => new
            {
                m.MatterId,
                m.MatterDocumentId,
                m.DocumentMasterId,
                m.IsPublicVisible,
                m.DocumentStageId,
                docMaster = new { m.DocumentMaster.DocName, m.DocumentMaster.DocType },
                Location = m.MatterDocumentLocations.Where(x => x.Enabled).Select(x => new { x.Enabled, x.DocumentLocationId, x.DocumentLocation.DisplayName, x.DocumentLocation.StreetAddress, x.DocumentLocation.State.StateName, x.DocumentLocation.Postcode }).FirstOrDefault(),
                docData = m.DocumentMaster.Documents
                                           .Where(x => x.IsLatestVersion).Select(d => new { d.DocumentId, d.DocModDate, d.VersionNotes, d.User.Username, d.UpdatedDate })
                                           .FirstOrDefault(),
                docVersions = m.DocumentMaster.Documents.Count(),
                m.DocumentDisplayAreaTypeId,
                m.DocumentDisplayAreaType.DocumentDisplayAreaTypeName,
                m.WFComponentId,
                m.WFComponent.WFComponentName,
                m.UpdatedDate,
                m.UpdatedByUserId,
                UpdatedByUsername = m.User.Username,
                m.IsDeleted,
                m.QADocPrepRequired,
                m.QASettlementInstructionsRequired,
                m.QASettlementRequired
            })
            .ToList()
            .Select(m => new MatterCustomEntities.MatterDocumentsView
            {

                MatterId = m.MatterId,
                MatterDocumentId = m.MatterDocumentId,
                DocumentMasterId = m.DocumentMasterId,
                DocumentId = m.docData.DocumentId,
                DocName = m.docMaster.DocName,
                DocType = m.docMaster.DocType,
                IsEmail = m.docMaster.DocType?.ToUpper() == "EML" || m.docMaster.DocType?.ToUpper() == "MSG",
                DocDisplayName = GetDocDisplayName(m.docMaster.DocName, m.docVersions),
                DocModDate = m.docData.DocModDate,
                DocumentDisplayAreaTypeId = m.DocumentDisplayAreaTypeId,
                DocumentDisplayAreaTypeName = m.DocumentDisplayAreaTypeName,
                WFComponentId = m.WFComponentId,
                WFComponentName = m.WFComponentName,
                VersionNotes = m.docData.VersionNotes,
                UpdatedDate = m.UpdatedDate < m.docData.UpdatedDate ? m.docData.UpdatedDate : m.UpdatedDate,
                UpdatedByUserId = m.UpdatedByUserId,
                UpdatedByUsername = m.UpdatedByUsername,
                IsDeleted = m.IsDeleted,
                DocSelected = false,
                DocVersion = m.docVersions,
                IsWordDoc = Common.EntityHelper.IsWordDoc(m.docMaster.DocType),
                IsPublic = m.IsPublicVisible,
                DocumentStageId = m.DocumentStageId,
                DocumentLocationId = m.Location != null ? m.Location.DocumentLocationId : null,
                DocumentLocationName = m.Location != null ?
                    m.Location.DisplayName + "(" + m.Location.StreetAddress + m.Location.StateName + m.Location.Postcode + ")"
                    : null,
                QASettlementRequired = m.QASettlementRequired,
                QADocPrepRequired = m.QADocPrepRequired,
                QASettlementInstructionsRequired = m.QASettlementInstructionsRequired
            })
             .GroupBy(x => x.DocumentMasterId)
             .Select(x => x.OrderByDescending(o => o.UpdatedDate).First())
             .ToList();

            return ret;
        }
        /// <summary>
        /// Linearlly gets the Custody document views for <paramref name="docs"/> objects provided.
        /// </summary>
        /// <param name="docs">The Matter Documents to create views for.</param>
        /// <returns>Returns a Custody document view collection.</returns>
        private IEnumerable<MatterCustomEntities.MatterDocumentsCustodyView> GetDocumentCustodyGrid(IQueryable<MatterDocument> docs)
        {
            var ret = docs.Select(m => new
            {
                m.MatterId,
                m.MatterDocumentId,
                m.DocumentMasterId,
                m.IsPublicVisible,
                m.DocumentStageId,
                docMaster = m.DocumentMaster,
                docData = m.DocumentMaster.Documents
                                           .Where(x => x.IsLatestVersion)
                                           .Select(d => new { d.DocumentId, d.DocModDate, d.VersionNotes, d.User.Username, d.UpdatedDate }).FirstOrDefault(),

                Location = m.MatterDocumentLocations.Select(x => new { x.Enabled, x.DocumentLocationId, x.DocumentLocation }).FirstOrDefault(x => x.Enabled),
                docVersions = m.DocumentMaster.Documents.Count(),
                m.DocumentDisplayAreaTypeId,
                m.DocumentDisplayAreaType.DocumentDisplayAreaTypeName,
                m.WFComponentId,
                m.WFComponent.WFComponentName,
                m.UpdatedDate,
                m.UpdatedByUserId,
                UpdatedByUsername = m.User.Username,
                m.IsDeleted
            })
             .ToList()
             .Select(m => new MatterCustomEntities.MatterDocumentsCustodyView
             {

                 MatterId = m.MatterId,
                 MatterDocumentId = m.MatterDocumentId,
                 DocumentMasterId = m.DocumentMasterId,
                 DocumentId = m.docData.DocumentId,
                 DocName = m.docMaster.DocName,
                 DocType = m.docMaster.DocType,
                 DocDisplayName = GetDocDisplayName(m.docMaster.DocName, m.docVersions),
                 DocModDate = m.docData.DocModDate,
                 DocumentDisplayAreaTypeId = m.DocumentDisplayAreaTypeId,
                 DocumentDisplayAreaTypeName = m.DocumentDisplayAreaTypeName,
                 WFComponentId = m.WFComponentId,
                 WFComponentName = m.WFComponentName,
                 VersionNotes = m.docData.VersionNotes,
                 UpdatedDate = m.UpdatedDate,
                 UpdatedByUserId = m.UpdatedByUserId,
                 UpdatedByUsername = m.UpdatedByUsername,
                 IsDeleted = m.IsDeleted,
                 DocSelected = false,
                 DocVersion = m.docVersions,
                 IsWordDoc = Common.EntityHelper.IsWordDoc(m.docMaster.DocType),
                 IsPublic = m.IsPublicVisible,
                 DocumentStageId = m.DocumentStageId,
                 DocumentLocationId = m.Location != null ? m.Location.DocumentLocationId : null,
                 DocumentLocationName = m.Location != null ?
                    m.Location.DocumentLocation.DisplayName + "(" + m.Location.DocumentLocation.StreetAddress + m.Location.DocumentLocation.State.StateName + m.Location.DocumentLocation.Postcode + ")"
                    : null,
                 IsDirty = false,
             })
             .DistinctBy(x => x.DocumentMasterId)
             .ToList();

            return ret;
        }
        /// <summary>
        /// Gets documents for web view based on the specification of the precedents screen checkbox
        /// </summary>
        /// <param name="matterId">The ID of the matter</param>
        /// <returns>A collection of document views that are eligible for view on the Web</returns>
        public IEnumerable<MatterCustomEntities.MatterDocumentsWebView> GetDocumentsWeb(int matterId)
        {
            bool settlementHasCompletedForMatter = false;
            bool sendDocumentsHasCompletedForMatter = false; 
            List<MatterCustomEntities.MatterDocumentsWebView> result = new List<MatterCustomEntities.MatterDocumentsWebView>();
            var mwfRep = new MatterWFRepository(context);
            
            settlementHasCompletedForMatter = mwfRep.HasMilestoneCompleted(matterId,(int)Slick_Domain.Enums.WFComponentEnum.SettlementCompleteNewLoans,true);
            sendDocumentsHasCompletedForMatter = mwfRep.HasMilestoneCompleted(matterId, (int)Slick_Domain.Enums.WFComponentEnum.SendProcessedDocs, true);
            
            List<MatterCustomEntities.MatterDocumentsView> allDocuments = null;
            if (sendDocumentsHasCompletedForMatter) allDocuments = GetDocumentsForMatter(matterId, (int)Enums.DocumentDisplayAreaEnum.DocPack).ToList() ;
            if (settlementHasCompletedForMatter) allDocuments.AddRange(GetDocumentsForMatter(matterId, (int)Enums.DocumentDisplayAreaEnum.SettlementPack).Where(x => x.IsLatestVersion && !x.IsDeleted).ToList());
            var d = allDocuments == null ? new List<MatterCustomEntities.MatterDocumentsView>(): allDocuments.ToList();
            if (d.Count() > 0)
            {
                foreach (var document in allDocuments)
                {
                    var matterWfDoc = context.MatterWFDocuments.Where(x => x.DocumentId == document.DocumentId).FirstOrDefault();
                    if (matterWfDoc == null) continue;

                    var precedentIsPublic = context.Precedents.Where(x => x.PrecedentId == matterWfDoc.PrecedentId).Select(x => x.IsPublicVisible)?.FirstOrDefault();
                    if (precedentIsPublic == true && document.MatterDocumentId.HasValue)
                    {
                        result.Add(new MatterCustomEntities.MatterDocumentsWebView()
                        {
                            MatterDocumentId = document.MatterDocumentId.Value,
                            DocName = document.DocName,
                            DocModDate = document.DocModDate,
                            UpdatedByUserFullname = document.UpdatedByUsername,
                            UpdatedDate = document.UpdatedDate,
                            UpdatedByUsername = document.UpdatedByUsername,
                            UpdatedByUserType = "",
                        });
                    }

                }
            }
            else result = null;
            return result;
            
            
        }

       

        public string ConvertDocumentToZip(int matterId)
        {
            List<MatterCustomEntities.MatterDocumentsView> allDocuments = null;
            allDocuments = GetDocumentsForMatter(matterId, (int)Enums.DocumentDisplayAreaEnum.DocPack).ToList();
            var path = Path.Combine(GlobalVars.GetGlobalTxtVar(DomainConstants.MatterDocumentsDirectory), matterId.ToString());
            var zipPath = path + ".zip";

            if (allDocuments.Count() > 0)
            {
                using (var stream = new MemoryStream())
                {
                    using (var sw = new StreamWriter(stream))
                    {
                        //sw.Flush();
                        stream.Position = 0;

                        using (ZipFile zipFile = new ZipFile())
                        {
                            foreach (var document in allDocuments)
                            {
                                if (document.DocType.ToLower() != "pdf" && (document.DocType.ToLower() == "doc" || document.DocType.ToLower() == "docx"))
                                {
                                    var pdfFiles = DocumentExport.ExportToPDF(document.DocumentId.ToString(), document.DocName, document.DocType, path);
                                    zipFile.AddFile(pdfFiles, matterId.ToString()); //Adding files 
                                }
                                else
                                {
                                    zipFile.AddFile((path + "\\" + document.DocumentId + "." + document.DocType), matterId.ToString()); //Adding files 
                                }    
                                //zipFile.AddEntry("Records.txt", stream);
                                zipFile.Save(zipPath);
                            }
                        }
                    }
                }
                //foreach (var document in allDocuments)
                //{
                //    //convert files to pdf
                //    var pdfFiles = DocumentExport.ExportToPDF(document.DocumentId.ToString(), document.DocName, document.DocType, path);
                //    if (document.DocType.ToLower() != "pdf" && (document.DocType.ToLower() == "doc" || document.DocType.ToLower() == "docx"))
                //    {
                //        //var fileImage = DocumentExport.StreamToPDF(document.DocumentId.ToString(), document.DocType, path);
                //        var pdfFile = DocumentExport.ExportToPDF(document.DocumentId.ToString(), document.DocName, document.DocType, path);
                //        //AddFileToZip(zipPath, pdfFile);
                //        AddFileToZipMem(zipPath, pdfFile);
                //    }
                //    else
                //    {
                //        //AddFileToZip(zipPath, path + "\\" + document.DocumentId + "." + document.DocType);
                //        AddFileToZipMem(zipPath, path + "\\" + document.DocumentId + "." + document.DocType);
                //    }                  

                //}


            }
            return zipPath;
        }

        private static void AddFileToZip(string zipFilename, string fileToAdd, CompressionOption compression = CompressionOption.Normal)
        {
            using (Package zip = System.IO.Packaging.Package.Open(zipFilename, FileMode.OpenOrCreate))
            {
                string destFilename = ".\\" + Path.GetFileName(fileToAdd);
                Uri uri = PackUriHelper.CreatePartUri(new Uri(destFilename, UriKind.Relative));
                if (zip.PartExists(uri))
                {
                    zip.DeletePart(uri);
                }
                PackagePart part = zip.CreatePart(uri, "", compression);
                using (FileStream fileStream = new FileStream(fileToAdd, FileMode.Open, FileAccess.Read))
                {
                    using (Stream dest = part.GetStream())
                    {
                        fileStream.CopyTo(dest);
                    }
                }
            }
            
        }

        private static void AddFileToZipMem(string zipFilename, string fileToAdd, CompressionOption compression = CompressionOption.Normal)
        {
            using (var memStream = new MemoryStream()) { 
                using (Package zip = System.IO.Packaging.Package.Open(zipFilename, FileMode.OpenOrCreate))
                {
                    string destFilename = ".\\" + Path.GetFileName(fileToAdd);
                    Uri uri = PackUriHelper.CreatePartUri(new Uri(destFilename, UriKind.Relative));
                    if (zip.PartExists(uri))
                    {
                        zip.DeletePart(uri);
                    }
                    PackagePart part = zip.CreatePart(uri, "", compression);
                    using (FileStream fileStream = new FileStream(fileToAdd, FileMode.Open, FileAccess.Read))
                    {
                        using (Stream dest = part.GetStream())
                        {
                            memStream.Position = 0;
                            fileStream.CopyTo(dest);
                            //CopyStream(fileStream, dest);
                        }
                    }
                }
            }

        }
        //private const long BUFFER_SIZE = 4096;

        //private static void CopyStream(System.IO.FileStream inputStream, System.IO.Stream outputStream)
        //{
        //    long bufferSize = inputStream.Length < BUFFER_SIZE ? inputStream.Length : BUFFER_SIZE;
        //    byte[] buffer = new byte[bufferSize];
        //    int bytesRead = 0;
        //    long bytesWritten = 0;
        //    while ((bytesRead = inputStream.Read(buffer, 0, buffer.Length)) != 0)
        //    {
        //        outputStream.Write(buffer, 0, bytesRead);
        //        bytesWritten += bufferSize;
        //    }
        //}

        private static void CopyStream(Stream source, Stream target)
        {
            int bufSize = 0x1000;
            byte[] buf = new byte[bufSize];
            int bytesRead = 0;
            while ((bytesRead = source.Read(buf, 0, bufSize)) > 0)
            {
                target.Write(buf, 0, bytesRead);
            }
        }

        /// <summary>
        /// Gets the matter's documents for web view. 
        /// </summary>
        /// <param name="matterId">The <see cref="Matter.MatterId"/></param>
        /// <returns>Returns an enumerable collection of Matter Documents.</returns>
        public IEnumerable<MatterCustomEntities.MatterDocumentsWebView> GetDocumentsForWeb(int matterId)
        {
            //check if docs have been sent

            IEnumerable<MatterCustomEntities.MatterDocumentsWebView> returnList = null;
            var qry = context.MatterDocuments.AsNoTracking().Where(x => x.MatterId == matterId);

            var mwfRep = new MatterWFRepository(context);
            var f = qry.Count();
            var wfComps = mwfRep.GetMatterComponentsForMatter(qry.Select(x => x.MatterId).First());

            if (mwfRep.HasMilestoneCompleted(wfComps, (int)WFComponentEnum.SendProcessedDocs)) {
                qry = qry.Where(x => x.DocumentDisplayAreaTypeId == (int)Enums.DocumentDisplayAreaEnum.DocPack &&
                                                              !x.IsDeleted &&
                                                              x.Matter.MatterType.MatterTypeId != (int)Enums.MatterTypeEnum.Discharge);
                var docsList = qry.Select(m => new
                {
                    m.MatterDocumentId,
                    m.DocumentMaster.DocName,
                    DocData = m.DocumentMaster.Documents.Where(x => x.IsLatestVersion)
                                                       .Select(d => new { d.DocumentId, d.DocModDate, d.VersionNotes, d.User1.Username, UserFullname = d.User1.Fullname, d.UpdatedDate, d.User1.UserType }).FirstOrDefault(),
                }).ToList()
                          .Select(m => new MatterCustomEntities.MatterDocumentsWebView
                          {
                              MatterDocumentId = m.MatterDocumentId,
                              DocName = m.DocName,
                              DocModDate = m.DocData.DocModDate,
                              UpdatedDate = m.DocData.UpdatedDate,
                              UpdatedByUsername = m.DocData.Username,
                              UpdatedByUserFullname = m.DocData.UserFullname,
                              UpdatedByUserType = m.DocData.UserType.UserTypeName

                          }).Where(m => m.DocName.ToLower().Contains("frontcover") == false && m.DocName.ToLower().Contains("front cover") == false).ToList();

                if (mwfRep.HasMilestoneCompleted(wfComps, (int)WFComponentEnum.SettlementCompleteNewLoans))
                {
                    var qry2 = context.MatterDocuments.AsNoTracking().Where(x => x.MatterId == matterId).Where(x =>
                        x.DocumentDisplayAreaTypeId == (int)Enums.DocumentDisplayAreaEnum.SettlementPack);
                    var settlementDocs = qry2.Select(m => new
                    {
                        m.MatterDocumentId,
                        m.DocumentMaster.DocName,
                        DocData = m.DocumentMaster.Documents.Where(x => x.IsLatestVersion)
                                                       .Select(d => new { d.DocumentId, d.DocModDate, d.VersionNotes, d.User1.Username, UserFullname = d.User1.Fullname, d.UpdatedDate, d.User1.UserType }).FirstOrDefault(),
                    }).ToList().Select(m => new MatterCustomEntities.MatterDocumentsWebView
                    {   
                        MatterDocumentId = m.MatterDocumentId,
                        DocName = m.DocName,
                        DocModDate = m.DocData.DocModDate,
                        UpdatedDate = m.DocData.UpdatedDate,
                        UpdatedByUsername = m.DocData.Username,
                        UpdatedByUserFullname = m.DocData.UserFullname,
                        UpdatedByUserType = m.DocData.UserType.UserTypeName
                    }).Where(m => m.DocName.ToLower().Contains("settlement confirmation letter")).ToList();

                    returnList = (settlementDocs);
                    return returnList;
                }
                return docsList;
            } else
            {
                return null;
            }


        }
        /// <summary>
        /// Gets the Documents view for the web view.
        /// </summary>
        /// <param name="matterId">The <see cref="Matter.MatterId"/> to search by.</param>
        /// <returns>An eneumerable collection of documents where the document is the latest version.</returns>
        public IEnumerable<MatterCustomEntities.MatterDocumentsWebView> GetAttachmentsForWeb(int matterId)
        {
            var qry = context.MatterDocuments.AsNoTracking().Where(x => x.MatterId == matterId);
            qry = qry.Where(x => x.DocumentDisplayAreaTypeId == (int)Enums.DocumentDisplayAreaEnum.Attachments && !x.IsDeleted);
            return qry.Select(m => new
            {
                m.MatterDocumentId,
                m.DocumentMaster.DocName,
                DocData = m.DocumentMaster.Documents.Where(x => x.IsLatestVersion)
                                                   .Select(d => new { d.DocumentId, d.DocModDate, d.VersionNotes, d.User1.Username, UserFullname = d.User1.Fullname, d.UpdatedDate, d.User1.UserTypeId, d.User1.UserType }).FirstOrDefault(),
            }).ToList().Where(m => m.DocData.UserTypeId != 1)                      //don't show internal docs attached 
                      .Select(m => new MatterCustomEntities.MatterDocumentsWebView
                      {
                          MatterDocumentId = m.MatterDocumentId,
                          DocName = m.DocName,
                          DocModDate = m.DocData.DocModDate,
                          UpdatedDate = m.DocData.UpdatedDate,
                          UpdatedByUsername = m.DocData.Username,
                          UpdatedByUserFullname = m.DocData.UserFullname,
                          UpdatedByUserType = m.DocData.UserType.UserTypeName

                      }).ToList();
        }
        /// <summary>
        /// Gets the Matter Custody Centre Request Views for a matter.
        /// </summary>
        /// <remarks>Potential Refactor? I get why it's here haha but maybe we should put this somewhere else</remarks>
        /// <param name="matterId">The <see cref="Matter.MatterId"/> of the matter to filter by.</param>
        /// <returns></returns>
        public IEnumerable<MatterCustomEntities.MatterCustodyCentreRequestView> GetMatterCustodyCentreRequestsForMatter(int matterId)
        {
            var ret = context.MatterCustodyCentreRequests.Where(m => m.MatterId == matterId)
                .Select(s => new MatterCustomEntities.MatterCustodyCentreRequestView()
                {
                    MatterCustodyCentreRequestId = s.MatterCustodyCentreRequestId,
                    MatterId = s.MatterId,
                    RequestStatusId = s.RequestStatusId,
                    RequestStatusName = s.CustodyRequestStatusType.CustodyRequestStatusName,
                    RequestedByUserId = s.RequestedByUserId,
                    RequestedByName = s.User.Fullname,
                    LenderName = s.Matter.Lender.LenderName,
                    LenderId = s.Matter.Lender.LenderId,
                    RequestDate = s.RequestedDate,
                    RequestedLocation = s.DocumentLocation.StreetAddress + ", " + s.DocumentLocation.Suburb + " " + s.DocumentLocation.State.StateName + " " + s.DocumentLocation.Postcode,
                    PostageDetails = s.PostageDetails,
                    Notes = s.Notes,
                    UpdatedByUserName = s.User1.Username,
                    UpdatedByUserId = s.UpdatedByUserId,
                    UpdatedDate = s.UpdatedDate
                });
            return ret;
        }
        /// <summary>
        /// Get all the matter custody centre requests in their views.
        /// </summary>
        /// <returns>All matter custody centre requests in their views</returns>
        public IEnumerable<MatterCustomEntities.MatterCustodyCentreRequestView> GetMatterCustodyCentreRequests()
        {
            var ret = context.MatterCustodyCentreRequests
                .Select(s => new MatterCustomEntities.MatterCustodyCentreRequestView()
                {
                    MatterCustodyCentreRequestId = s.MatterCustodyCentreRequestId,
                    MatterId = s.MatterId,
                    RequestStatusId = s.RequestStatusId,
                    RequestStatusName = s.CustodyRequestStatusType.CustodyRequestStatusName,
                    RequestedByUserId = s.RequestedByUserId,
                    RequestedByName = s.User.Fullname,
                    LenderName = s.Matter.Lender.LenderName,
                    LenderId = s.Matter.Lender.LenderId,
                    RequestDate = s.RequestedDate,
                    RequestedLocation = s.DocumentLocation.StreetAddress + ", " + s.DocumentLocation.Suburb + " " + s.DocumentLocation.State.StateName + " " + s.DocumentLocation.Postcode,
                    PostageDetails = s.PostageDetails,
                    Notes = s.Notes,
                    UpdatedByUserName = s.User1.Username,
                    UpdatedByUserId = s.UpdatedByUserId,
                    UpdatedDate = s.UpdatedDate
                });
            return ret;
        }
        public MatterCustomEntities.MatterCustodyCentreRequestView GetMatterCustodyCentreRequestForId(int MatterCustodyCentreRequestId)
        {
            var ret = context.MatterCustodyCentreRequests.Where(m => m.MatterCustodyCentreRequestId == MatterCustodyCentreRequestId)
                .Select(s => new MatterCustomEntities.MatterCustodyCentreRequestView()
                {
                    MatterCustodyCentreRequestId = s.MatterCustodyCentreRequestId,
                    MatterId = s.MatterId,
                    RequestStatusId = s.RequestStatusId,
                    RequestStatusName = s.CustodyRequestStatusType.CustodyRequestStatusName,
                    RequestedByUserId = s.RequestedByUserId,
                    RequestedByName = s.User.Fullname,
                    RequestDate = s.RequestedDate,
                    RequestedLocation = s.RequestedLocationId.HasValue ? s.DocumentLocation.StreetAddress + ", " + s.DocumentLocation.Suburb + " " + s.DocumentLocation.State.StateName + " " + s.DocumentLocation.Postcode : null,
                    RequestedLocationId = s.RequestedLocationId,
                    PostageDetails = s.PostageDetails,
                    Notes = s.Notes,
                    UpdatedByUserName = s.User1.Username,
                    UpdatedByUserId = s.UpdatedByUserId,
                    UpdatedDate = s.UpdatedDate,
                    RequestTypeId = s.RequestTypeId
                }).FirstOrDefault();
            return ret;
        }
        /// <summary>
        /// Get Document Location Views for all enabled locations.
        /// </summary>
        /// <returns>Returns the document locations that are enabled in a collection.</returns>
        public IEnumerable<MatterCustomEntities.DocumentLocationView> GetDocumentLocations()
        {
            return context.DocumentLocations.Where(d => d.Enabled == true).Select(x => new MatterCustomEntities.DocumentLocationView
            {
                DocumentLocationId = x.DocumentLocationId,
                Enabled = x.Enabled,
                DisplayName = x.DisplayName != null ? x.DisplayName + " ( " + x.StreetAddress + ", " + x.Suburb + " " + x.State.StateName + " " + x.Postcode + " )" :
                              x.StreetAddress + ", " + x.Suburb + " " + x.State.StateName + " " + x.Postcode,
                AddressString = x.StreetAddress + ", " + x.Suburb + " " + x.State.StateName + " " + x.Postcode,
                StreetAddress = x.StreetAddress,
                Suburb = x.Suburb,
                StateName = x.State.StateName,
                Postcode = x.Postcode,
                StateId = x.StateId,
                LenderId = x.LenderId,
                LenderName = x.LenderId.HasValue ? x.Lender.LenderName : null
            });
        }
        /// <summary>
        /// Update the location of custody documents based on the views. 
        /// </summary>
        /// <param name="views">The collection of views to update.</param>
        /// <returns>True for successful update, and false for a failed update.</returns>
        public bool UpdateDocumentLocations(IEnumerable<MatterCustomEntities.MatterDocumentsCustodyView> views)
        {
            try
            {
                foreach (var doc in views)
                {
                    var existingDocLocations = context.MatterDocumentLocations.Where(d => d.MatterDocumentId == doc.MatterDocumentId && d.Enabled == true);
                    if (existingDocLocations.Count() != 0)
                    {
                        //If there are already matterdocumentlocations set for this doc, update them first. 
                        foreach (var existingDocLocation in existingDocLocations)
                        {
                            existingDocLocation.Enabled = false;
                            context.MatterDocumentLocations.Add(new MatterDocumentLocation()
                            {
                                MatterDocumentId = doc.MatterDocumentId.Value,
                                DocumentLocationId = doc.DocumentLocationId != -1 ? doc.DocumentLocationId : null,
                                MatterCustodyCentreRequestId = existingDocLocation.MatterCustodyCentreRequestId,
                                UpdatedDate = DateTime.Now,
                                UpdatedByUserId = GlobalVars.CurrentUser.UserId,
                                Enabled = true
                            });
                        }
                    }
                    else
                    {
                        //if not, let's make one
                        context.MatterDocumentLocations.Add(new MatterDocumentLocation()
                        {
                            MatterDocumentId = doc.MatterDocumentId.Value,
                            DocumentLocationId = doc.DocumentLocationId != -1 ? doc.DocumentLocationId : null,
                            UpdatedDate = DateTime.Now,
                            UpdatedByUserId = GlobalVars.CurrentUser.UserId,
                            Enabled = true
                        });
                    }

                    context.SaveChanges();
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        /// <summary>
        /// Returns a New <see cref="MatterCustodyCentreRequest"/> based on the <paramref name="CustodyCentreRequestView"/>
        /// </summary>
        /// <remarks>Seems to be a precondition that everything is validate - refactor?</remarks>
        /// <param name="CustodyCentreRequestView">The View to base the new Matter Custody Centre Rqeuest View on</param>
        /// <returns>Returns the new object</returns>
        public MatterCustodyCentreRequest NewCustodyRequestViewToEntity(MatterCustomEntities.MatterCustodyCentreRequestView CustodyCentreRequestView)
        {
            var ret = new MatterCustodyCentreRequest()
            {
                RequestedByUserId = CustodyCentreRequestView.RequestedByUserId,
                RequestedDate = DateTime.Now,
                RequestedLocationId = CustodyCentreRequestView.RequestedLocationId,
                RequestTypeId = CustodyCentreRequestView.RequestTypeId,
                RequestStatusId = (int)Slick_Domain.Enums.CustodyCentreRequestStatusTypeEnum.Requested,
                UpdatedByUserId = CustodyCentreRequestView.UpdatedByUserId,
                UpdatedDate = DateTime.Now,
                Notes = CustodyCentreRequestView.Notes,
                MatterId = CustodyCentreRequestView.MatterId
            };
            return ret;
        }
        /// <summary>
        /// Adds a new document location to the database.
        /// </summary>
        /// <param name="view">The view to base the new item off.</param>
        /// <returns>Returns the <see cref="DocumentLocation.DocumentLocationId"/> for the new item.</returns>
        public int AddNewDocumentLocation(MatterCustomEntities.DocumentLocationView view)
        {
            int newId = 0;

            var newDocLoc = context.DocumentLocations.Add(new DocumentLocation
            {
                DisplayName = view.DisplayName,
                StreetAddress = view.StreetAddress,
                Suburb = view.Suburb,
                Postcode = view.Postcode,
                StateId = view.StateId,
                LenderId = view.LenderId,
                Enabled = true
            });

            context.SaveChanges();

            newId = newDocLoc.DocumentLocationId;

            return newId;
        }
        /// <summary>
        /// Creates a new custody centre request and request document object for each of the <paramref name="requestedDocs"/>.
        /// </summary>
        /// <param name="CustodyCentreRequestView">The Request view where data is retrieved from.</param>
        /// <param name="requestedDocs">The documents being requested.</param>
        /// <returns>an int of the new <see cref="MatterCustodyCentreRequest.MatterCustodyCentreRequestId"/></returns>
        public int NewCustodyCentreRequest(MatterCustomEntities.MatterCustodyCentreRequestView CustodyCentreRequestView, List<MatterCustomEntities.MatterDocumentsView> requestedDocs)
        {
            var newReq = context.MatterCustodyCentreRequests.Add(NewCustodyRequestViewToEntity(CustodyCentreRequestView));
            context.SaveChanges();

            int newId = newReq.MatterCustodyCentreRequestId;

            foreach (var doc in requestedDocs)
            {
                var CustodyCentreRequestDoc = new CustodyCentreRequestDocument()
                {
                    MatterDocumentId = doc.MatterDocumentId.Value,
                    MatterCustodyCentreRequestId = newId
                };
                context.CustodyCentreRequestDocuments.Add(CustodyCentreRequestDoc);
            }
            context.SaveChanges();
            return newId;

        }
        // Gets the latest version of Documents only
        /// <summary>
        /// Mark the custody documents as sent on the Custody Centre Request that matches <paramref name="view"/>
        /// </summary>
        /// <remarks>
        /// No false return ever! refactor? 
        /// Check Preconditions. 
        /// </remarks>
        /// <param name="view">The view to find the request from</param>
        /// <returns>True if successful</returns>
        public bool MarkCustodyDocumentsAsSent(MatterCustomEntities.MatterCustodyCentreRequestView view)
        {
            var requestToUpdate = context.MatterCustodyCentreRequests.Where(c => c.MatterCustodyCentreRequestId == view.MatterCustodyCentreRequestId).FirstOrDefault();

            requestToUpdate.RequestStatusId = (int)Slick_Domain.Enums.CustodyCentreRequestStatusTypeEnum.Sent;
            requestToUpdate.UpdatedByUserId = GlobalVars.CurrentUser.UserId;
            requestToUpdate.UpdatedDate = DateTime.Now;
            requestToUpdate.Notes = view.Notes;
            requestToUpdate.PostageDetails = view.PostageDetails;

            context.SaveChanges();

            return true;
        }

        /// <summary>
        /// Update the Matter Custody Centre Request based on the <paramref name="view"/> <see cref="MatterCustomEntities.MatterCustodyCentreRequestView.MatterCustodyCentreRequestId"/>
        /// </summary>
        /// <param name="view">The view that is to be filtered by</param>
        /// <returns>True for success.</returns>
        public bool MarkCustodyDocumentsAsReceived(MatterCustomEntities.MatterCustodyCentreRequestView view)
        {
            var requestToUpdate = context.MatterCustodyCentreRequests.Where(c => c.MatterCustodyCentreRequestId == view.MatterCustodyCentreRequestId).FirstOrDefault();

            requestToUpdate.RequestStatusId = (int)Slick_Domain.Enums.CustodyCentreRequestStatusTypeEnum.Received;
            requestToUpdate.UpdatedByUserId = GlobalVars.CurrentUser.UserId;
            requestToUpdate.UpdatedDate = DateTime.Now;
            requestToUpdate.Notes = view.Notes;
            requestToUpdate.PostageDetails = view.PostageDetails;

            context.SaveChanges();

            return true;
        }
        /// <summary>
        /// Checks for whether a request already exists for a matter.
        /// </summary>
        /// <param name="displayName">Unused paramater.</param>
        /// <param name="matterId"><see cref="Matter.MatterId"/> to check for requests.</param>
        /// <returns>Returns true if any request exists.</returns>
        public bool DoesCustodyRequestExist(int displayName, int matterId)
        {
            return context.MatterCustodyCentreRequests.Where((m) => m.MatterId == matterId && m.CustodyRequestStatusType.CustodyRequestStatusId == (int)Slick_Domain.Enums.CustodyCentreRequestStatusTypeEnum.Requested).Any();
        }
        public bool DoesCustodyDocumentRequestExist(int custodyCentreDocumentId)
        {            
            return context.CustodyCentreDocumentRequests.Where((m) => m.CustodyCentreDocumentId == custodyCentreDocumentId).Any();
        }
        /// <summary>
        /// Get the documents display name based on the name and version
        /// </summary>
        /// <param name="docName">The documents name.</param>
        /// <param name="versionCount">The version of the document.</param>
        /// <returns>Returns the concatenated string.</returns>
        private string GetDocDisplayName(string docName, int versionCount)
        {
            string retval = docName;
            if (versionCount > 1)
                retval += " (" + versionCount.ToString() + ")";
            return retval;
        }
        /// <summary>
        /// Gets the version history for the<paramref name="documentMasterId"/> supplied.
        /// </summary>
        /// <param name="documentMasterId">The <see cref="Document.DocumentMasterId"/> of the document to filter by.</param>
        /// <returns>Returns an enumerable collection of document views of the Matter Document for the different versions.</returns>
        public IEnumerable<MatterCustomEntities.MatterDocumentsView> GetDocVersionHistory(int documentMasterId)
        {
            return context.Documents.AsNoTracking().Where(m => m.DocumentMasterId == documentMasterId)
                    .Select(m => new MatterCustomEntities.MatterDocumentsView
                    {
                        DocumentMasterId = m.DocumentMasterId,
                        DocumentId = m.DocumentId,
                        DocName = m.DocumentMaster.DocName,
                        DocType = m.DocumentMaster.DocType,
                        DocVersion = m.VersionNo,
                        IsLatestVersion = m.IsLatestVersion,
                        DocModDate = m.DocModDate,
                        VersionNotes = m.VersionNotes,
                        UpdatedDate = m.UpdatedDate,
                        UpdatedByUserId = m.UpdatedByUserId,
                        UpdatedByUsername = m.User.Username,
                        EditedDate = m.EditedDate,
                        EditedByUsername = m.User1.Username
                    })
                    .ToList();
        }
        /// <summary>
        /// Saves the document to the print queue to be printed.
        /// </summary>
        /// <param name="queue">The collection of Items to go into the print queue.</param>
        /// <param name="matterId">The <see cref="Matter.MatterId"/> of the related matter</param>
        /// <param name="isPriority">Flag for whether the print item is a priority or not.</param>
        public void SavePrintDocumentQueue(List<MatterCustomEntities.MatterPrintCollateQueueView> queue, int matterId, bool isPriority, bool sendToVic)
        {
            var docs = queue.OrderBy(o => o.Order).ToList();
            int? newBatchNo = context.PrintCollateQueues.Select(x => x.PrintBatchNo).Distinct().OrderByDescending(x => x).FirstOrDefault();
            if (!newBatchNo.HasValue)
            {
                newBatchNo = 0;
            }
            else
            {
                newBatchNo++;
            }

            foreach (var doc in docs)
            {
                context.PrintCollateQueues.Add(new PrintCollateQueue()
                {
                    MatterId = matterId,
                    MatterDocumentId = doc.MatterDocumentId,
                    DocPath = doc.DocPath,
                    Printed = false,
                    Copies = doc.Copies,
                    DocRecipientTypeId = doc.DocRecipientTypeId,
                    RequestDate = DateTime.UtcNow,
                    RequestedByUserId = GlobalVars.CurrentUser.UserId,
                    PrintStapled = doc.PrintStapled,
                    PrintColour = doc.PrintColour,
                    PrintDoubleSided = doc.PrintDoubleSided,
                    IsPriority = isPriority,
                    SendToVIC = sendToVic,
                    PrintOrder = doc.Order,
                    PrintBatchNo = newBatchNo.Value
                });
                context.SaveChanges();
            }
        }
        /// <summary>
        /// Gets the print queue for a <paramref name="matterId"/>
        /// </summary>
        /// <remarks>ORDER: Broker, lender, mortmgr, per borrower,  per guarantor, Branch, internal addtional</remarks>
        /// <param name="matterId">The <see cref="Matter.MatterId"/> of the matter to get the queue of</param>
        /// <returns>A List of Print Queue View items</returns>
        public List<MatterCustomEntities.MatterPrintCollateQueueView> GetDocumentPrintQueue(int matterId, bool backchannel = false)
        {
            List<MatterCustomEntities.MatterPrintCollateDocView> docsToPrint = new List<MatterCustomEntities.MatterPrintCollateDocView>();
            List<MatterCustomEntities.MatterPrintCollateQueueView> printQueue = new List<MatterCustomEntities.MatterPrintCollateQueueView>();
            var docs = GetDocumentsForMatter(matterId, (int)Enums.DocumentDisplayAreaEnum.DocPack).Where(d => !d.IsDeleted).ToList();

            bool isEsign = context.Matters.FirstOrDefault(m => m.MatterId == matterId).IsDigiDocs;

            for (int i = 0; i < docs.Count; i++)
            {
                var doc = docs[i];
                //var docDetails = context.Documents.Where(x => x.DocumentMasterId == doc.DocumentMasterId && x.IsLatestVersion).Select(d=> new { d.DocumentId, d.DocumentMaster.DocType }).FirstOrDefault();
                int? precId = context.MatterWFDocuments.FirstOrDefault(d => d.DocumentId == doc.DocumentId)?.PrecedentId;
                if (!precId.HasValue)
                {
                    precId = (int?)context.Documents.Where(d => d.DocumentMasterId == doc.DocumentMasterId && d.MatterWFDocuments.Any()).OrderByDescending(d => d.UpdatedDate)?.SelectMany(x => x.MatterWFDocuments.Select(p => p.PrecedentId)).FirstOrDefault();
                    if (precId == 0) precId = null;
                }

                if (precId.HasValue && precId > 0)
                {
                    var precDetail = context.Precedents.FirstOrDefault(p => p.PrecedentId == precId);
                    if ((isEsign && !backchannel) && !precDetail.PrintableESign) continue; // We don't need it for eSign unless it is backchannel or we flag the eSign precedent as printable

                    var newDoc = new MatterCustomEntities.MatterPrintCollateDocView()
                    {
                        MatterDocumentId = doc.MatterDocumentId.Value,
                        DocumentMasterId = doc.DocumentMasterId.Value,
                        DocName = doc.DocDisplayName,
                        DocPath = GetDocPath(matterId, doc.DocumentId.ToString() + "." + doc.DocType),
                        CopiesLender = precDetail.CopiesLender ?? 0,
                        CopiesMortMgr = precDetail.CopiesMortMgr ?? 0,
                        CopiesPerBorrower = precDetail.CopiesPerBorrower ?? 0,
                        CopiesPerGuarantor = precDetail.CopiesPerGuarantor ?? 0,
                        CopiesBranch = precDetail.CopiesBranch ?? 0,
                        CopiesBroker = precDetail.CopiesBroker ?? 0,
                        CopiesInternal = precDetail.CopiesInternal ?? 0,
                        CopiesAdditional = precDetail.CopiesAdditional ?? 0,
                        CopiesSignedPerGuarantor = precDetail.CopiesSignedGuarantor,
                        WatermarkLender = precDetail.ApplyWatermarkLender,
                        WatermarkAdditional = precDetail.ApplyWatermarkAdditional,
                        WatermarkBranch = precDetail.ApplyWatermarkBranch,
                        WatermarkBroker = precDetail.ApplyWatermarkBroker,
                        WatermarkInternal = precDetail.ApplyWatermarkInternal,
                        WatermarkMortMgr = precDetail.ApplyWatermarkMortMgr,
                        WatermarkPerBorrower = precDetail.ApplyWatermarkBorrower,
                        WatermarkPerGuarantor = precDetail.ApplyWatermarkGuarantor,
                        WatermarkSignedGuarantor = precDetail.ApplyWatermarkSignedGuarantor,
                        PrintStapled = precDetail.PrintStapled ?? false,
                        PrintColour = precDetail.PrintColour ?? false,
                        PrintDoubleSided = precDetail.PrintDoubleSided ?? false,
                        PrintPrimaryBorrowerOnly = precDetail.PrintPrimaryBorrowerOnly,
                        PrecedentPrintOrder = precDetail.PrintOrder ?? 99999,

                        ToSkip = false
                    };

                    if (newDoc.CopiesLender != 0 ||
                        newDoc.CopiesBroker != 0 ||
                        newDoc.CopiesInternal != 0 ||
                        newDoc.CopiesPerBorrower != 0 ||
                        newDoc.CopiesMortMgr != 0 ||
                        newDoc.CopiesPerGuarantor != 0 ||
                        newDoc.CopiesBranch != 0 ||
                        newDoc.CopiesAdditional != 0)
                    {
                        docsToPrint.Add(newDoc);
                    }
                }
            }

            docsToPrint = docsToPrint.OrderBy(d => d.PrecedentPrintOrder).ThenBy(d => d.DocumentMasterId).ToList();
            int currOrder = 0;
            foreach (var doc in docsToPrint.Where(x => x.CopiesInternal != 0))
            {
                printQueue.Add
                    (
                        new MatterCustomEntities.MatterPrintCollateQueueView()
                        {
                            Order = currOrder,
                            Copies = doc.CopiesInternal,
                            DocumentName = doc.DocName,
                            DocPath = doc.DocPath,
                            MatterDocumentId = doc.MatterDocumentId,
                            DocRecipientName = "Internal",
                            DocRecipientTypeId = (int)Enums.DocRecipientTypeEnum.Internal,
                            PrintColour = doc.PrintColour,
                            PrintDoubleSided = doc.PrintDoubleSided,
                            PrintStapled = doc.PrintStapled,
                            IsPriority = false,
                            ApplyWatermark = doc.WatermarkInternal
                        }
                    );
                currOrder++;
            }

            //ORDER: Broker, lender, mortmgr, per borrower,  per guarantor, Branch, internal addtional/
            foreach (var doc in docsToPrint.Where(x => x.CopiesBroker != 0))
            {
                printQueue.Add
                    (
                        new MatterCustomEntities.MatterPrintCollateQueueView()
                        {
                            Order = currOrder,
                            Copies = doc.CopiesBroker,
                            DocumentName = doc.DocName,
                            MatterDocumentId = doc.MatterDocumentId,
                            DocPath = doc.DocPath,
                            DocRecipientName = "Broker",
                            DocRecipientTypeId = (int)Enums.DocRecipientTypeEnum.Broker,
                            PrintColour = doc.PrintColour,
                            PrintDoubleSided = doc.PrintDoubleSided,
                            PrintStapled = doc.PrintStapled,
                            IsPriority = false,
                            ApplyWatermark = isEsign ? true : doc.WatermarkBroker

                        }
                    );
                currOrder++;
            }
            foreach (var doc in docsToPrint.Where(x => x.CopiesLender != 0))
            {
                printQueue.Add
                    (
                        new MatterCustomEntities.MatterPrintCollateQueueView()
                        {
                            Order = currOrder,
                            Copies = doc.CopiesLender,
                            DocumentName = doc.DocName,
                            MatterDocumentId = doc.MatterDocumentId,
                            DocPath = doc.DocPath,
                            DocRecipientName = "Lender",
                            DocRecipientTypeId = (int)Enums.DocRecipientTypeEnum.Lender,
                            PrintColour = doc.PrintColour,
                            PrintDoubleSided = doc.PrintDoubleSided,
                            PrintStapled = doc.PrintStapled,
                            IsPriority = false,
                            ApplyWatermark = isEsign ? true : doc.WatermarkLender

                        }
                    );
                currOrder++;
            }

            if (context.Matters.FirstOrDefault(m => m.MatterId == matterId).MortMgrId.HasValue)
            {
                foreach (var doc in docsToPrint.Where(x => x.CopiesMortMgr != 0))
                {
                    printQueue.Add
                        (
                            new MatterCustomEntities.MatterPrintCollateQueueView()
                            {
                                Order = currOrder,
                                DocumentName = doc.DocName,
                                MatterDocumentId = doc.MatterDocumentId,
                                Copies = doc.CopiesMortMgr,
                                DocPath = doc.DocPath,
                                DocRecipientName = "MortMgr",
                                DocRecipientTypeId = (int)Enums.DocRecipientTypeEnum.MortMgr,
                                PrintColour = doc.PrintColour,
                                PrintDoubleSided = doc.PrintDoubleSided,
                                PrintStapled = doc.PrintStapled,
                                IsPriority = false,
                                ApplyWatermark = isEsign ? true : doc.WatermarkMortMgr
                            }
                        );
                    currOrder++;
                }
            }

            int numBorrowers = context.MatterParties.Where(m => m.MatterId == matterId).Count(x => x.PartyTypeId == (int)Enums.MatterPartyTypeEnum.Borrower);

            int coborrowerIndex = 1;

            for (int i = 0; i < numBorrowers; i++)
            {
                int whichCoborrowerletter = 1;
                foreach (var doc in docsToPrint.Where(d => d.CopiesPerBorrower > 0))
                {

                    bool shouldSkip = false;


                    if (doc.ToSkip == true) shouldSkip = true;

                    if (doc.PrintPrimaryBorrowerOnly && i != 0) shouldSkip = true;

                    if (doc.DocName.ToUpper().Contains("CO-BORROWER") && i != coborrowerIndex)
                    {
                        shouldSkip = true;
                    }

                    if (!shouldSkip)
                    {
                        printQueue.Add
                            (
                                new MatterCustomEntities.MatterPrintCollateQueueView()
                                {
                                    Order = currOrder,
                                    Copies = doc.CopiesPerBorrower,
                                    DocPath = doc.DocPath,
                                    DocumentName = doc.DocName,
                                    MatterDocumentId = doc.MatterDocumentId,
                                    DocRecipientName = "Borrower",
                                    DocRecipientTypeId = (int)Enums.DocRecipientTypeEnum.Borrower,
                                    PrintColour = doc.PrintColour,
                                    PrintDoubleSided = doc.PrintDoubleSided,
                                    PrintStapled = doc.PrintStapled,
                                    IsPriority = false,
                                    ApplyWatermark = isEsign ? true : doc.WatermarkPerBorrower
                                }
                            );
                        currOrder++;
                        if (doc.DocName.ToUpper().Contains("CO-BORROWER"))
                        {
                            coborrowerIndex++;
                            whichCoborrowerletter++;
                            doc.ToSkip = true;
                        }
                    }


                }
            }

            int numGuarantors = context.MatterParties.Where(m => m.MatterId == matterId).Count(x => x.PartyTypeId == (int)Enums.MatterPartyTypeEnum.Guarantor);

            int signedCoguarantorIndex = 1;


            for (int i = 0; i < numGuarantors; i++)
            {
                int whichCoGuarantorLetter = 1;

                foreach (var doc in docsToPrint.Where(x => x.CopiesSignedPerGuarantor != 0))
                {
                    bool shouldSkip = false;
                    if (doc.ToSkip == true) shouldSkip = true;

                    if (doc.DocName.ToUpper().Contains("CO-GUARANTOR") && i != signedCoguarantorIndex)
                    {
                        shouldSkip = true;
                    }
                    if (!shouldSkip)
                    {
                        printQueue.Add
                        (
                            new MatterCustomEntities.MatterPrintCollateQueueView()
                            {
                                Order = currOrder,
                                Copies = doc.CopiesPerGuarantor,
                                DocumentName = doc.DocName,
                                MatterDocumentId = doc.MatterDocumentId,
                                DocPath = doc.DocPath,
                                DocRecipientName = "Guarantor",
                                DocRecipientTypeId = (int)Enums.DocRecipientTypeEnum.Guarantor,
                                PrintColour = doc.PrintColour,
                                PrintDoubleSided = doc.PrintDoubleSided,
                                PrintStapled = doc.PrintStapled,
                                IsPriority = false,
                                ApplyWatermark = isEsign ? true : doc.WatermarkSignedGuarantor
                            }
                        );
                        currOrder++;
                        if (doc.DocName.ToUpper().Contains("CO-GUARANTOR"))
                        {
                            signedCoguarantorIndex++;
                            whichCoGuarantorLetter++;
                            doc.ToSkip = true;
                        }
                    }

                }
            }



            int coguarantorIndex = 1;


            for (int i = 0; i < numGuarantors; i++)
            {
                int whichCoGuarantorLetter = 1;

                foreach (var doc in docsToPrint.Where(x => x.CopiesPerGuarantor != 0))
                {
                    bool shouldSkip = false;
                    if (doc.ToSkip == true) shouldSkip = true;

                    if (doc.DocName.ToUpper().Contains("CO-GUARANTOR") && i != coguarantorIndex)
                    {
                        shouldSkip = true;
                    }
                    if (!shouldSkip)
                    {
                        printQueue.Add
                            (
                                new MatterCustomEntities.MatterPrintCollateQueueView()
                                {
                                    Order = currOrder,
                                    Copies = doc.CopiesPerGuarantor,
                                    DocumentName = doc.DocName,
                                    MatterDocumentId = doc.MatterDocumentId,
                                    DocPath = doc.DocPath,
                                    DocRecipientName = "Guarantor",
                                    DocRecipientTypeId = (int)Enums.DocRecipientTypeEnum.Guarantor,
                                    PrintColour = doc.PrintColour,
                                    PrintDoubleSided = doc.PrintDoubleSided,
                                    PrintStapled = doc.PrintStapled,
                                    IsPriority = false,
                                    ApplyWatermark = isEsign ? true : doc.WatermarkPerGuarantor

                                }
                            );
                        currOrder++;
                        if (doc.DocName.ToUpper().Contains("CO-GUARANTOR"))
                        {
                            coguarantorIndex++;
                            whichCoGuarantorLetter++;
                            doc.ToSkip = true;
                        }
                    }

                }
            }

            foreach (var doc in docsToPrint.Where(x => x.CopiesBranch != 0))
            {
                printQueue.Add
                    (
                        new MatterCustomEntities.MatterPrintCollateQueueView()
                        {
                            Order = currOrder,
                            Copies = doc.CopiesBranch,
                            DocumentName = doc.DocName,
                            MatterDocumentId = doc.MatterDocumentId,
                            DocPath = doc.DocPath,
                            DocRecipientName = "Branch",
                            DocRecipientTypeId = (int)Enums.DocRecipientTypeEnum.Branch,
                            PrintColour = doc.PrintColour,
                            PrintDoubleSided = doc.PrintDoubleSided,
                            PrintStapled = doc.PrintStapled,
                            IsPriority = false,
                            ApplyWatermark = isEsign ? true : doc.WatermarkBranch
                        }
                    );
                currOrder++;
            }


            foreach (var doc in docsToPrint.Where(x => x.CopiesAdditional != 0))
            {
                printQueue.Add
                    (
                        new MatterCustomEntities.MatterPrintCollateQueueView()
                        {
                            Order = currOrder,
                            Copies = doc.CopiesAdditional,
                            DocumentName = doc.DocName,
                            DocPath = doc.DocPath,
                            MatterDocumentId = doc.MatterDocumentId,
                            DocRecipientName = "Additional",
                            DocRecipientTypeId = (int)Enums.DocRecipientTypeEnum.Additional,
                            PrintColour = doc.PrintColour,
                            PrintDoubleSided = doc.PrintDoubleSided,
                            PrintStapled = doc.PrintStapled,
                            IsPriority = false,
                            ApplyWatermark = isEsign ? true : doc.WatermarkAdditional
                        }
                    );
                currOrder++;
            }


            return printQueue;

        }
        /// <summary>
        /// Gets the possible documents to print for <paramref name="matterId"/>. 
        /// </summary>
        /// <param name="matterId">The <see cref="Matter.MatterId"/> to get possible documents for.</param>
        /// <returns>A list of possible Matter P&C Docs to print</returns>
        public List<MatterCustomEntities.MatterPrintCollateDocView> GetPossibleDocsToPrint(int matterId)
        {
            var docs = GetAllDocumentsForMatter(matterId);
            List<MatterCustomEntities.MatterPrintCollateDocView> result = new List<MatterCustomEntities.MatterPrintCollateDocView>();

            List<string> validDocTypes = new List<string>() { "PDF", "DOC", "DOCX" };

            result = docs.Where(d => validDocTypes.Contains(d.DocType.ToUpper()) && d.MatterDocumentId.HasValue && !d.IsDeleted).Select(x => new MatterCustomEntities.MatterPrintCollateDocView
            {
                MatterDocumentId = x.MatterDocumentId.Value,
                DocName = x.DocName,
                DocPack = x.DocumentDisplayAreaTypeName,
                DocPath = GetDocPath(matterId, x.DocumentId.ToString() + "." + x.DocType)
            }
            ).ToList();



            return result;
        }
        /// <summary>
        /// Gets the document path for a matter by combining the matterid and doc name.
        /// </summary>
        /// <remarks>
        /// Potential refactor, idk if this belongs here or in some helper class
        /// </remarks>
        /// <param name="matterId">The <see cref="Matter.MatterId"/></param>
        /// <param name="docName">The name of the document to get the path for</param>
        /// <returns>A formatted string that is the resulting path.</returns>
        public string GetDocPath(int matterId, string docName)
        {
            return Path.Combine(matterId.ToString(), docName);
        }
        /// <summary>
        /// Get the Matter Document view for a specific matter document ID.
        /// </summary>
        /// <param name="matterDocumentId">The Matter Document Id to get the view for.</param>
        /// <returns>The constructed Document view.</returns>
        public MatterCustomEntities.MatterDocumentsView GetMatterDocView(int matterDocumentId)
        {
            var md = context.MatterDocuments.Where(m => m.MatterDocumentId == matterDocumentId)
                                    .Select(m => new
                                    {
                                        m.MatterId,
                                        m.DocumentMasterId,
                                        m.DocumentDisplayAreaTypeId,
                                        m.DocumentDisplayAreaType.DocumentDisplayAreaTypeName,
                                        m.WFComponentId,
                                        m.WFComponent.WFComponentName
                                    }).FirstOrDefault();

            if (md == null)
                return null;

            return context.Documents.AsNoTracking().Where(m => m.DocumentMasterId == md.DocumentMasterId && m.IsLatestVersion)
                        .Select(d => new MatterCustomEntities.MatterDocumentsView
                        {
                            MatterDocumentId = matterDocumentId,
                            MatterId = md.MatterId,
                            DocumentMasterId = d.DocumentMasterId,
                            DocumentId = d.DocumentId,
                            DocName = d.DocumentMaster.DocName,
                            DocType = d.DocumentMaster.DocType,
                            DocVersion = d.VersionNo,
                            IsLatestVersion = d.IsLatestVersion,
                            DocumentDisplayAreaTypeId = md.DocumentDisplayAreaTypeId,
                            DocumentDisplayAreaTypeName = md.DocumentDisplayAreaTypeName,
                            WFComponentId = md.WFComponentId,
                            WFComponentName = md.WFComponentName,
                            DocModDate = d.DocModDate,
                            VersionNotes = d.VersionNotes,
                            UpdatedDate = d.UpdatedDate,
                            UpdatedByUserId = d.UpdatedByUserId,
                            UpdatedByUsername = d.User.Username
                        })
             .FirstOrDefault();
        }





        /// <summary>
        /// Called within a Transaction to save documents to database.
        /// </summary>
        /// <param name="doc">The document to save</param>
        /// <param name="matterDoc">The matter document being saved.</param>
        public bool SaveDocument(ref DocumentInfo doc, ref MatterDocument matterDoc, GeneralCustomEntities.DocQATypesView qaDetails = null)
        {


            int docMasterId = SaveDocMasterToDB(doc);
            doc.ID = SaveVersionToDB(docMasterId, doc.ModDate);
            matterDoc = SaveMatterDocument(docMasterId, doc.MatterId, doc.DocumentDisplayAreaType, qaDetails: qaDetails);
            return true;
        }
        /// <summary>
        /// Saves the document for a user. 
        /// </summary>
        /// <param name="doc">The document to be saved</param>
        /// <param name="matterDoc">The Matter Document details</param>
        /// <param name="userId">The <see cref="User.UserId"/> to save the document for.</param>
        /// <returns>True on success.</returns>
        /// 
        
        /*public bool SaveDocumentForUser(ref DocumentInfo doc, ref MatterDocument matterDoc, int userId, bool DocPrepQA = false, bool SettlementQA = false, bool SettlemetnInstructionQA = false)
        {
            int docMasterId = SaveDocMasterToDB(doc, userId);
            doc.ID = SaveVersionToDBForUser(docMasterId, doc.ModDate, userId);
            matterDoc = SaveMatterDocumentForUser(docMasterId, doc.MatterId, doc.DocumentDisplayAreaType, userId, isPublic: false,  markQA: markQA);
            return true;
        }*/


        public int SaveDocumentForUser(ref DocumentInfo doc, ref MatterDocument matterDoc, int userId, bool docPrepQA = false, bool settlementQA = false, bool settlementInstructionQA = false, bool isScannedDocument = false)
        {

            int docMasterId = SaveDocMasterToDB(doc, userId);
 
            doc.ID = SaveVersionToDBForUser(docMasterId, doc.ModDate, userId);
            //matterDoc = SaveMatterDocumentForUser(docMasterId, doc.MatterId, doc.DocumentDisplayAreaType, userId, isPublic: false, DocPrepQA: DocPrepQA, SettlementQA: SettlementQA, SettlemetnInstructionQA: SettlemetnInstructionQA);
            matterDoc = SaveMatterDocumentForUser(docMasterId, doc.MatterId, doc.DocumentDisplayAreaType, doc.FromLoantrak, doc.LoantrakUploadTypeId, userId, isPublic: false, docPrepQA: docPrepQA, settlementQA: settlementQA, settlementInstructionQA: settlementInstructionQA, isScannedDocument: isScannedDocument);
            return matterDoc.MatterDocumentId;
        }

        // 
        /// <summary>
        /// Save Report always finds a report if it exists (regardless of when) and replaces it with later version
        /// </summary>
        /// <param name="doc">The document to be saved.</param>
        /// <param name="matterDoc">The document with the details required by a matter to be saved.</param>
        /// <returns></returns>
        public bool SaveReport(ref DocumentInfo doc, ref MatterDocument matterDoc)
        {
            int? docMasterId = GetExistingMasterDocument(doc.MatterId, doc.FileName, doc.FileType, (int)doc.DocumentDisplayAreaType);
            if (!docMasterId.HasValue)
                docMasterId = SaveDocMasterToDB(doc);
            else
            {
                var existingDoc = context.DocumentMasters.FirstOrDefault(d => d.DocumentMasterId == docMasterId.Value);
                existingDoc.UpdatedByUserId = GlobalVars.CurrentUser.UserId;
                existingDoc.UpdatedDate = DateTime.Now;
            }
            doc.ID = SaveVersionToDB(docMasterId.Value, doc.ModDate);
            matterDoc = SaveMatterDocument(docMasterId.Value, doc.MatterId, doc.DocumentDisplayAreaType);

            return true;
        }

        private string ValidSymbols(string input)
        {
            char[] validCharacters = new char[] 
            {
                'a','b','c','d','e','f','g','h','i','j','k','l','m','n','o','p','q','r','s','t','u','v','w','x','y','z',
                'A','B','C','D','E','F','G', 'H','I','J','K','L','M','N','O','P','Q','R','S','T','U','V','W','X','Y','Z',
                '0','1','2','3','4','5','6','7','8','9', ' ', '_','-','.','(',')', '&', '*', '$','%',':'
            };

            var validChars = input?.Where(c => validCharacters.Contains(c))?.ToArray();

            return new string(validChars);
        }


        /// <summary>
        /// Save the master document to the database
        /// </summary>
        /// <param name="docInfo"></param>
        /// <returns>The <see cref="DocumentMaster.DocumentMasterId"/></returns>
        private int SaveDocMasterToDB(DocumentInfo docInfo)
        {
            DocumentMaster doc = new DocumentMaster();
            var rep = new Repository<DocumentMaster>(context);

            if (context.MatterDocuments.Any(x => x.MatterId == docInfo.MatterId && x.DocumentMaster.DocName == docInfo.FileName && !x.LockedByLender && (int)docInfo.DocumentDisplayAreaType == x.DocumentDisplayAreaTypeId && x.DocumentMaster.DocType == docInfo.FileType))
            {
                doc = context.MatterDocuments.Where(x => x.MatterId == docInfo.MatterId && x.DocumentMaster.DocName == docInfo.FileName && (int)docInfo.DocumentDisplayAreaType == x.DocumentDisplayAreaTypeId && x.DocumentMaster.DocType == docInfo.FileType)
                    .OrderByDescending(x => x.UpdatedDate).FirstOrDefault().DocumentMaster;

                doc.DocName = ValidSymbols(doc.DocName);
                doc.UpdatedDate = DateTime.Now;
                doc.UpdatedByUserId = GlobalVars.CurrentUser.UserId;
                context.SaveChanges();

                return doc.DocumentMasterId;
            }
            else
            {


                doc.DocName = docInfo.FileName.ToSafeFileName();
                doc.DocType = docInfo.FileType;
                doc.UpdatedByUserId = GlobalVars.CurrentUser.UserId;
                doc.UpdatedDate = DateTime.Now;

                rep.Add(doc);

                return doc.DocumentMasterId;
            }
        }

        /// <summary>
        /// Saves the document master to the database, with argument for specifying the <see cref="User.UserId"/> 
        /// </summary>
        /// <param name="docInfo">The document to be saved</param>
        /// <param name="userId">The <see cref="User.UserId"/> to make it updated by</param>
        /// <returns>Returns the new <see cref="Document.DocumentMasterId"/></returns>
        private int SaveDocMasterToDB(DocumentInfo docInfo, int userId)
        {
            DocumentMaster doc = new DocumentMaster();
            var rep = new Repository<DocumentMaster>(context);

            doc.DocName = docInfo.FileName;
            doc.DocType = docInfo.FileType;
            if (GlobalVars.CurrentUser == null)
            {
                EmailsService.SendSimpleNoReplyEmail(new List<string>() { "daniel.hwang@msanational.com.au" }, new List<string>(), "BAD CRASH", "SUSPECTING CRASH... " + Environment.UserName + " / " + System.Security.Principal.WindowsIdentity.GetCurrent().Name + " trying to write to email");

            }
            doc.UpdatedByUserId = userId;
            doc.UpdatedDate = DateTime.Now;

            rep.Add(doc);

            return doc.DocumentMasterId;
        }
        /// <summary>
        /// Saves the matter document for the matter based on the <see cref="DocumentMaster.DocumentMasterId"/>
        /// </summary>
        /// <param name="docId">The <see cref="DocumentMaster.DocumentMasterId"/></param>
        /// <param name="matterId">The <see cref="Matter.MatterId"/></param>
        /// <param name="docType">The <see cref="DocumentDisplayAreaEnum"/> to classify by.</param>
        /// <param name="isPublic">Boolean value for whether the document is publicly visible.</param>
        /// <returns>The newly created Matter Document.</returns>
        private MatterDocument SaveMatterDocument(int docId, int matterId, Enums.DocumentDisplayAreaEnum docType, bool isPublic = false, GeneralCustomEntities.DocQATypesView qaDetails = null)
        {
            var rep = new Repository<MatterDocument>(context);

            MatterDocument matterDoc = new MatterDocument();

            matterDoc.MatterId = matterId;

            matterDoc.IsDeleted = false;
            matterDoc.DocumentMasterId = docId;
            matterDoc.UpdatedByUserId = GlobalVars.CurrentUser.UserId;
            matterDoc.UpdatedDate = DateTime.Now;
            matterDoc.DocumentDisplayAreaTypeId = (int)docType;
            matterDoc.IsPublicVisible = isPublic;

            if (qaDetails != null)
            {
                matterDoc.QADocPrepRequired = qaDetails.QADocPrepRequired;
                matterDoc.QASettlementRequired = qaDetails.QASettlementRequired;
                matterDoc.QASettlementInstructionsRequired = qaDetails.QASettlementInstructionsRequired;
            }

            rep.Add(matterDoc);

            return matterDoc;
        }
        /// <summary>
        /// Overload of <see cref="SaveMatterDocument(int, int, DocumentDisplayAreaEnum, bool)"/> to incldue the <see cref="User.UserId"/> in <paramref name="userId"/>
        /// </summary>
        /// <param name="docId"></param>
        /// <param name="matterId"></param>
        /// <param name="docType"></param>
        /// <param name="userId">The <see cref="User.UserId"/> to make the matter document update by.</param>
        /// <param name="isPublic"></param>
        /// <returns></returns>
        private MatterDocument SaveMatterDocumentForUser(int docId, int matterId, Enums.DocumentDisplayAreaEnum docType, bool fromLoantrak, int? loantrakUploadTypeId, int userId, bool isPublic = false, bool docPrepQA = false, bool settlementQA = false, bool settlementInstructionQA = false, bool isScannedDocument = false)
        {
            var rep = new Repository<MatterDocument>(context);

            MatterDocument matterDoc = new MatterDocument();

            matterDoc.MatterId = matterId;
            matterDoc.QADocPrepRequired = docPrepQA;
            matterDoc.QASettlementRequired = settlementQA;
            matterDoc.QASettlementInstructionsRequired = settlementInstructionQA;
            matterDoc.IsDeleted = false;
            matterDoc.DocumentMasterId = docId;
            matterDoc.UpdatedByUserId = userId;
            matterDoc.UpdatedDate = DateTime.Now;
            matterDoc.DocumentDisplayAreaTypeId = (int)docType;
            matterDoc.IsPublicVisible = isPublic;
            matterDoc.FromLoantrak = fromLoantrak;
            matterDoc.LoantrakUploadTypeId = loantrakUploadTypeId;
            if (isScannedDocument)
            {
                matterDoc.IsScannedOriginal = true;
                matterDoc.ScannedByUserId = userId;
            }
            rep.Add(matterDoc);

            return matterDoc;
        }

        /// <summary>
        /// Saves a new version of the document to the database.
        /// </summary>
        /// <remarks>
        /// Checks if a document already exists. Otherwise constructs a new Document!
        /// </remarks>
        /// <param name="docId">The <see cref="DocumentMaster.DocumentMasterId"/></param>
        /// <param name="modDate">The modified date</param>
        /// <param name="reason">The reason for a new version.</param>
        /// <param name="isEditing">Check for whether the document is for editing.</param>
        /// <returns>The new <see cref="Document.DocumentId"/> created.</returns>
        public int SaveVersionToDB(int docId, DateTime? modDate, string reason = null, bool isEditing = false)
        {
            var rep = new Repository<Document>(context);
            var oldDoc = rep.AllAsQuery.FirstOrDefault(dv => dv.DocumentMasterId == docId && dv.IsLatestVersion == true);
            if (oldDoc != null)
            {
                oldDoc.IsLatestVersion = false;
                oldDoc.UpdatedByUserId = GlobalVars.CurrentUser.UserId;
                oldDoc.UpdatedDate = DateTime.Now;
                rep.Update(oldDoc);
            }

            var doc = new Document
            {
                DocModDate = modDate.HasValue ? modDate.Value : DateTime.Now,
                DocumentMasterId = docId,
                VersionNotes = reason,
                IsLatestVersion = true,
                VersionNo = (oldDoc?.VersionNo ?? 0) + 1,
                UpdatedByUserId = GlobalVars.CurrentUser.UserId,
                UpdatedDate = DateTime.Now
            };

            context.DocumentMasters.FirstOrDefault(d => d.DocumentMasterId == docId).ArchiveStatusTypeId = (int)ArchiveStatusTypeId.NotArchived;

            if (isEditing)
            {
                doc.Edited = true;
                doc.EditedByUserId = GlobalVars.CurrentUser.UserId;
                doc.EditedDate = DateTime.Now;
                doc.EditedNotes = reason;
            }

            rep.Add(doc);
            return doc.DocumentId;
        }

        /// <summary>
        /// Set all docs for a <see cref="Matter"/> for a given <see cref="DocumentDisplayAreaType"/> to "deleted" - doesn't remove from file system
        /// </summary>
        /// <param name="matterId"><see cref="Matter.MatterId"/> for the matter to clear documents from</param>
        /// <param name="pack">The <see cref="Enums.DocumentDisplayAreaEnum"/> to clear</param>
        public void SoftDeleteDocsByPack(int matterId, Enums.DocumentDisplayAreaEnum pack)
        {
            var docs = context.MatterDocuments.Where(m => m.MatterId == matterId && m.DocumentDisplayAreaTypeId == (int)pack).Select(x => x.MatterDocumentId).ToList();
            foreach (var doc in docs)
            {
                DeleteUnDeleteDocument(doc, true, "Client Rework Required");
            }
        }


        public int SaveVersionToDBForUser(int docId, DateTime? modDate, int userId, string reason = null, bool isEditing = false)
        {

            var rep = new Repository<Document>(context);
            var oldDoc = rep.AllAsQuery.FirstOrDefault(dv => dv.DocumentMasterId == docId && dv.IsLatestVersion == true);
            if (oldDoc != null)
            {
                oldDoc.IsLatestVersion = false;
                oldDoc.UpdatedByUserId = GlobalVars.CurrentUser.UserId;
                oldDoc.UpdatedDate = DateTime.Now;
                rep.Update(oldDoc);
            }

            var doc = new Document
            {
                DocModDate = modDate.HasValue ? modDate.Value : DateTime.Now,
                DocumentMasterId = docId,
                VersionNotes = reason,
                IsLatestVersion = true,
                VersionNo = (oldDoc?.VersionNo ?? 0) + 1,
                UpdatedByUserId = userId,
                UpdatedDate = DateTime.Now
            };

            if (isEditing)
            {
                doc.Edited = true;
                doc.EditedByUserId = GlobalVars.CurrentUser.UserId;
                doc.EditedDate = DateTime.Now;
                doc.EditedNotes = reason;
            }

            rep.Add(doc);
            return doc.DocumentId;
        }
        /// <summary>
        /// Get an existing matter document
        /// </summary>
        /// <param name="matterId"></param>
        /// <param name="docName"></param>
        /// <param name="docType"></param>
        /// <param name="docDisplayAreaTypeId"></param>
        /// <returns>The existing <see cref="DocumentMaster.DocumentMasterId"/>.</returns>
        public int? GetExistingMasterDocument(int matterId, string docName, string docType, int? docDisplayAreaTypeId = null)
        {
            var existingMaster = (from m in context.MatterDocuments.AsNoTracking()
                                  join d in context.DocumentMasters.AsNoTracking() on m.DocumentMasterId equals d.DocumentMasterId
                                  where m.MatterId == matterId && d.DocName == docName &&
                                  (d.DocType == docType || ((d.DocType.ToUpper() == "DOCX" || d.DocType.ToUpper() == "DOC") && (docType.ToUpper() == "DOCX" || docType.ToUpper() == "DOC")))
                                  select m)?.FirstOrDefault();


            if (existingMaster != null && existingMaster.DocumentMaster.DocType != docType)
            {
                existingMaster.DocumentMaster.DocType = docType;
                context.DocumentMasters.FirstOrDefault(d => d.DocumentMasterId == existingMaster.DocumentMasterId).DocType = docType;
                context.SaveChanges();
            }

            if (existingMaster != null && docDisplayAreaTypeId.HasValue)
            {
                if (existingMaster.DocumentDisplayAreaTypeId != docDisplayAreaTypeId)
                {
                    return null;
                }
            }
            return existingMaster?.DocumentMasterId;
        }
        /// <summary>
        /// Undo deleting of documents.
        /// </summary>
        /// <param name="docId">the <see cref="MatterDocument.MatterDocumentId"/></param>
        /// <param name="isToBeDeleted"></param>
        /// <param name="reason"></param>
        /// <returns>True if successful.</returns>
        public bool DeleteUnDeleteDocument(int docId, bool isToBeDeleted, string reason)
        {
            var oldDoc = context.MatterDocuments.FirstOrDefault(x => x.MatterDocumentId == docId);
            if (oldDoc == null) return false;

            oldDoc.IsDeleted = isToBeDeleted;
            oldDoc.UpdatedByUserId = GlobalVars.CurrentUser.UserId;
            oldDoc.UpdatedDate = DateTime.Now;
            context.SaveChanges();

            UpdateVersionWithReason(oldDoc.IsDeleted ? "Deleted: " + reason : "Recovered: " + reason, oldDoc.DocumentMasterId);
            return true;
        }
        /// <summary>
        /// Update the document version with reasons.
        /// </summary>
        /// <param name="reason">The update reason</param>
        /// <param name="id">The <see cref="DocumentMaster.DocumentMasterId"/> to be updating.</param>
        public void UpdateVersionWithReason(string reason, int id)
        {
            if (string.IsNullOrEmpty(reason)) return;
            var version = context.Documents.FirstOrDefault(dv => dv.DocumentMasterId == id && dv.IsLatestVersion == true);
            if (version == null) return;

            version.VersionNotes = version.VersionNotes + " " + reason ?? reason;
            version.UpdatedByUserId = GlobalVars.CurrentUser.UserId;
            version.UpdatedDate = DateTime.Now;
            context.SaveChanges();
        }
        /// <summary>
        /// Save the new version of a file. 
        /// </summary>
        /// <param name="matterId">The <see cref="Matter.MatterId"/></param>
        /// <param name="filetype">The file type of the document</param>
        /// <param name="oldDocId">The old <see cref="MatterDocument.MatterDocumentId"/></param>
        /// <param name="newDocId">The new <see cref="MatterDocument.MatterDocumentId"/></param>
        /// <returns>A string of the fully qualified file.</returns>
        public string SaveVersion(int matterId, string filetype, int oldDocId, int newDocId)
        {
            string oldPath = getDocPath(filetype, oldDocId, matterId);
            var dir = getDocPath(matterId);
            if (dir == null || !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string newPath = getDocPath(filetype, newDocId, matterId);
            if (!File.Exists(newPath))
            {
                File.Copy(oldPath, newPath);
            }
            return newPath;
        }
        /// <summary>
        /// Gets the full file path of a document.
        /// </summary>
        /// <param name="fileType">The file type, eg. ".docx"</param>
        /// <param name="docId">The <see cref="MatterDocument.MatterDocumentId"/></param>
        /// <param name="matterId">The <see cref="Matter.MatterId"/></param>
        /// <returns>A string of the full path to the document.</returns>
        private string getDocPath(string fileType, int docId, int matterId)
        {
            return Path.Combine(GlobalVars.GetGlobalTxtVar(DomainConstants.MatterDocumentsDirectory, context),
                    matterId.ToString(),
                    string.Format("{0}.{1}", docId.ToString(), string.IsNullOrEmpty(fileType) ? "txt" : fileType));
        }
        /// <summary>
        /// Get the document path for a matter.
        /// </summary>
        /// <param name="matterId">The <see cref="Matter.MatterId"/></param>
        /// <returns></returns>
        private string getDocPath(int matterId)
        {
            var path = Path.Combine(GlobalVars.GetGlobalTxtVar(DomainConstants.MatterDocumentsDirectory, context), matterId.ToString());
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            return path;
        }
        //
        public string GetHotDocsIdForPrecId(int precId)
        {
            var prec = context.Precedents.FirstOrDefault(x => x.PrecedentId == precId);
            if (prec != null)
                return prec.HotDocsId;
            else
                return null;
        }

        #region Perpetual Integration
        public void UpdatePerpetualQueue(List<int> itemIds, int perpetualStatusType)
        {
            var custodianItems = context.MatterWFLetterToCustodianItems.Where(d => itemIds.Contains(d.MatterWFLetterToCustodianItemId));
            foreach (var custodianItem in custodianItems)
            {
                custodianItem.PerpetualStatusTypeId = perpetualStatusType;
                custodianItem.SentToPerpetualDate = DateTime.UtcNow;
            }
            context.SaveChanges();
        }

        public IEnumerable<MatterCustomEntities.MatterCustodianDocumentQueueView> GetPerpetualQueue(int? lendlenderCustodianFtpEndpointId = null)
        {
            var rootPath = GlobalVars.GetGlobalTxtVar(DomainConstants.MatterDocumentsDirectory, context);
            var archivePath = GlobalVars.GetGlobalTxtVar(DomainConstants.MatterArchivedDocumentsDirectory, context);
            int? lenderId = null;
            if (lendlenderCustodianFtpEndpointId.HasValue)
            {
                lenderId = context.LenderCustodianIntegrations.Where(x => x.LenderCustodianIntegrationId == lendlenderCustodianFtpEndpointId).Select(x => x.LenderId).FirstOrDefault();
            }
                
            var queue = context.MatterWFLetterToCustodianItems.Where(x => x.MatterWFLetterToCustodian.MatterWFComponent.WFComponentStatusTypeId == (int)MatterWFComponentStatusTypeEnum.Complete 
            && x.MatterWFLetterToCustodian.MatterWFComponent.Matter.LenderId == lenderId && x.Attached && x.IsDigital && x.MatterDocumentId.HasValue && !x.SentToPerpetualDate.HasValue && x.PerpetualStatusTypeId == (int)Enums.PerpetualStatusTypeEnum.Ready).Select(
            x => new MatterCustomEntities.MatterCustodianDocumentQueueView
            {
                MatterWFLetterToCustodianItemId = x.MatterWFLetterToCustodianItemId,
                MatterWFComponentId = x.MatterWFLetterToCustodian.MatterWFComponentId,
                MatterId = x.MatterWFLetterToCustodian.MatterWFComponent.MatterId,
                DocumentId = x.MatterDocument.DocumentMaster.Documents.Where(d => d.IsLatestVersion).FirstOrDefault().DocumentId,
                DocName = x.DocumentName,
                DocType = x.MatterDocument.DocumentMaster.DocType,
                FilePath = ""
            }).ToList();

            foreach (var item in queue)
            {
                item.FilePath = Path.Combine(rootPath,
                    item.MatterId.ToString(),
                    string.Format("{0}.{1}", item.DocumentId.ToString(), string.IsNullOrEmpty(item.DocType) ? "txt" : item.DocType));
                if (!File.Exists(item.FilePath))
                {
                    item.FilePath = Path.Combine(archivePath,
                    item.MatterId.ToString(),
                    string.Format("{0}.{1}", item.DocumentId.ToString(), string.IsNullOrEmpty(item.DocType) ? "txt" : item.DocType));
                    if (!File.Exists(item.FilePath))
                    {
                        throw new InvalidOperationException("Missing File: " + item.FilePath);
                    }
                }
            }

            return queue;

        }
        #endregion
        public IEnumerable<MatterCustomEntities.DischargeFormsView> GetDischargeForms()
        {
            return context.DischargeForms.AsNoTracking()
                    .Select(m => new MatterCustomEntities.DischargeFormsView
                    {
                        DischargeFormId = m.DischargeFormId,
                        DischargeFormName = m.DischargeFormName,
                        DischargeFormDescription = m.DischargeFormDescription,
                        DischargeFormURL = m.DischargeFormURL,
                        DischargeFormFilename = m.DischargeFormURL,
                        UpdatedByUserId = m.UpdatedByUserId,
                        UpdatedDate = m.UpdatedDate,
                        //DocName = m.DocumentMaster.DocName,
                        //DocType = m.DocumentMaster.DocType,
                    })
                    .ToList();
        }

        public IEnumerable<MatterCustomEntities.MatterDocumentsView> GetRegistrationDocumentsToPrintForMatter(int matterId)
        {
            var allDocs = GetAllDocumentsForMatter(matterId).Where(d => !d.IsDeleted);
            var searchDocs = GetRegistrationDocumentsToSearchForMatter(matterId);
            var matchedDocs = new List<MatterCustomEntities.MatterDocumentsView>();

            foreach (var searchDoc in searchDocs)
            {
                List<int> validDisplayAreas = new List<int>();

                if (searchDoc.SearchDocPack) validDisplayAreas.Add((int)DocumentDisplayAreaEnum.DocPack);
                if (searchDoc.SearchVerifiedDocs) validDisplayAreas.Add((int)DocumentDisplayAreaEnum.ExecutedDocs);
                if (searchDoc.SearchSettlementPack) validDisplayAreas.Add((int)DocumentDisplayAreaEnum.SettlementPack);
                if (searchDoc.SearchFastREFISettlementPack) validDisplayAreas.Add((int)DocumentDisplayAreaEnum.FastRefiSettlementPack);

                if (validDisplayAreas.Any())
                {
                    var matches = allDocs.Where(d => validDisplayAreas.Contains(d.DocumentDisplayAreaTypeId) && d.DocDisplayName.ToLower().Contains(searchDoc.SearchText.ToLower()));
                    if (matches.Any())
                    {
                        matchedDocs =  matchedDocs.Concat(matches).ToList();
                    }
                }
            }

            return matchedDocs.DistinctBy(d=>d.DocumentId).ToList();
        }


        public IEnumerable<MatterCustomEntities.RegistrationPrintingDocumentView> GetRegistrationDocumentsToSearchForMatter(int matterId)
        {
            var mtDetails = context.Matters.Where(m => m.MatterId == matterId).Select(m =>
              new
              {
                  m.MatterId,
                  m.LenderId,
                  m.MortMgrId,
                  m.IsDigiDocs,
                  BranchStateId = m.StateId,
                  SecurityDetails = m.MatterSecurities.Where(d => !d.Deleted).Select(s => new { s.StateId, s.SettlementTypeId }).ToList(),
                  MatterTypeIds = m.MatterMatterTypes.Select(t => t.MatterTypeId).ToList(),
              }).FirstOrDefault();

            var secStates = mtDetails.SecurityDetails?.Select(s => s.StateId)?.ToList();
            var settlementTypes = mtDetails.SecurityDetails?.Select(s => s.SettlementTypeId)?.ToList();

            return GetRegistrationDocumentsForQry
                (
                    context.RegistrationPrintingDocuments.Where(m =>
                        (!m.LenderId.HasValue || m.LenderId == mtDetails.LenderId) &&
                        (!m.MortMgrId.HasValue || m.MortMgrId == mtDetails.MortMgrId) &&
                        (!m.BranchStateId.HasValue || m.BranchStateId == mtDetails.BranchStateId) &&
                        (!m.DigiDocsType.HasValue || m.DigiDocsType == mtDetails.IsDigiDocs) &&
                        (!m.SecurityStateId.HasValue || secStates.Any(s => s == m.SecurityStateId)) &&
                        (!m.SettlementTypeId.HasValue || settlementTypes.Any(s => s == m.SettlementTypeId)) &&
                        (!m.MatterTypeId.HasValue || mtDetails.MatterTypeIds.Any(s => s == m.MatterTypeId))
                    )
                ).ToList();
            

        }
        public IEnumerable<MatterCustomEntities.RegistrationPrintingDocumentView> GetRegistrationDocumentsForQry(IQueryable<RegistrationPrintingDocument> qry, bool isForAdminPanel = false)
        {
            var items = qry.Select(x => new MatterCustomEntities.RegistrationPrintingDocumentView()
            {
                RegistrationPrintingDocumentId = x.RegistrationPrintingDocumentId,
                SearchText = x.SearchText,
                LenderId = x.LenderId,
                MortMgrId = x.MortMgrId,
                SecurityStateId = x.SecurityStateId,
                SecurityStateName = x.State.StateName,
                BranchStateId = x.BranchStateId,
                BranchStateName = x.State1.StateName,
                MatterTypeId = x.MatterTypeId,
                SettlementTypeId = x.SettlementTypeId,
                DigiDocsType = x.DigiDocsType,
                DigiDocsTypeId = !x.DigiDocsType.HasValue ? 0 : x.DigiDocsType.Value ? 1 : 2,
                SearchDocPack = x.SearchDocPack,
                SearchAdditionalDocs = x.SearchAdditionalDocs,
                SearchSettlementPack = x.SearchSettlementPack,
                SearchVerifiedDocs = x.SearchVerifiedDocs,
                SearchFastREFISettlementPack = x.SearchFastREFISettlementPack,
                UpdatedByUserId = x.UpdatedByUserId,
                UpdatedByUserName = x.User.Username,
                UpdatedDate = x.UpdatedDate
            });
            if (isForAdminPanel)
            {
                items = items.Select(x => new MatterCustomEntities.RegistrationPrintingDocumentView()
                {
                    RegistrationPrintingDocumentId = x.RegistrationPrintingDocumentId,
                    SearchText = x.SearchText,
                    LenderId = x.LenderId ?? -1,
                    MortMgrId = x.MortMgrId ?? -1,
                    SecurityStateId = x.SecurityStateId ?? -1,
                    SecurityStateName = x.SecurityStateName,
                    BranchStateId = x.BranchStateId ?? -1,
                    BranchStateName = x.BranchStateName,
                    MatterTypeId = x.MatterTypeId ?? -1,
                    SettlementTypeId = x.SettlementTypeId ?? -1,
                    DigiDocsType = x.DigiDocsType,
                    DigiDocsTypeId = !x.DigiDocsType.HasValue ? 0 : x.DigiDocsType.Value ? 1 : 2,
                    SearchDocPack = x.SearchDocPack,
                    SearchAdditionalDocs = x.SearchAdditionalDocs,
                    SearchSettlementPack = x.SearchSettlementPack,
                    SearchVerifiedDocs = x.SearchVerifiedDocs,
                    SearchFastREFISettlementPack = x.SearchFastREFISettlementPack,
                    UpdatedByUserId = x.UpdatedByUserId,
                    UpdatedByUserName = x.UpdatedByUserName,
                    UpdatedDate = x.UpdatedDate
                });
            }
            return items;
        }

    }
}
