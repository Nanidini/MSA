using Slick_Domain.Entities;
using Slick_Domain.Interfaces;
using Slick_Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Slick_Domain.Common;

namespace Slick_Domain.Services
{
    public class NotesRepository : SlickRepository
    {
        public NotesRepository(SlickContext Context) : base(Context)
        {
        }

        private IEnumerable<NotesEntities.NotesView> GetNotesView(IQueryable<MatterNote> notes)
        {
            return notes
                .Select(s2 =>
                    new {
                        s2.HighPriority, s2.IsDeleted, s2.IsPinned, s2.IsPublic, s2.MatterNoteId, s2.MatterId, s2.NoteBody, s2.NoteHeader,
                        s2.UpdatedDate, s2.User.Username, s2.User.Fullname, s2.User.UserType.UserTypeName, s2.MatterNoteType
                    })
                .OrderByDescending(o=>o.IsPinned).ThenByDescending(o2=>o2.UpdatedDate)
                .ToList()
                .Select(nt => new NotesEntities.NotesView
                {
                    Id = nt.MatterNoteId,
                    MatterNoteType = new NotesEntities.NoteTypeView {MatterNoteTypeId = nt.MatterNoteType.MatterNoteTypeId, MatterNoteTypeName = nt.MatterNoteType.MatterNoteTypeName},
                    NoteBody = nt.NoteBody,
                    NoteHeader = nt.NoteHeader,
                    NoteBodySummary = GetNoteSummary(nt.NoteBody),

                    NoteHelper = new NotesEntities.NotesViewHelper
                    {
                        Id = nt.MatterNoteId,
                        Pinned = nt.IsPinned,
                        IsHighPriority = nt.HighPriority,
                        IsStatusUpdate = nt.MatterNoteType.MatterNoteTypeId == (int)Enums.MatterNoteTypeEnum.StatusUpdate,
                        IsPublic = nt.IsPublic,
                        IsDeleted = nt.IsDeleted,
                        UpdatedBy = nt.Username,
                        UpdatedByFullname = nt.Fullname,
                        UpdatedByUserType = nt.UserTypeName,
                        UpdatedDate = nt.UpdatedDate,
                    } 
                })
                .ToList();

        }

        public IEnumerable<NotesEntities.NotesView> GetNotesView(int matterId, bool showDeleted)
        {
            var notes = context.MatterNotes.Where(m => m.MatterId == matterId);

            if (!showDeleted)
                notes = notes.Where(m => m.IsDeleted == false);
        

            return GetNotesView(notes);
        }

        public NotesEntities.NotesView GetSingleNoteView(int id)
        {
            return GetNotesView(context.MatterNotes.Where(x=>x.MatterNoteId == id)).FirstOrDefault();   
        }

        public bool CheckIfAbleToEditNote(int id, string username)
        {
            return (from m in context.MatterNotes
                                   join a in context.audMatterNotes on m.MatterNoteId equals a.MatterNoteId
                                   where m.MatterNoteId == id && a.UpdatedByUsername == username && a.AuditAction == "I"
                                   select m.MatterNoteId).Any();
        }


        public IEnumerable<NotesEntities.StickyNotesView> GetStickyNotesView(int matterId)
        {
            return context.MatterNoteTemps.Where(x=> x.MatterId == matterId)
                .Select(s2 =>
                    new {s2.MatterNoteTempId, s2.NoteBody, s2.User.Username, s2.UpdatedDate, s2.AccentColor, s2.BodyColor }     
                    ).OrderByDescending(n=>n.UpdatedDate)
                .ToList()
                .Select(nt => new NotesEntities.StickyNotesView
                {
                    Id = nt.MatterNoteTempId,
                    NoteBody = nt.NoteBody,
                    NoteBy = $"{nt.Username} {nt.UpdatedDate.ToString("dd-MMM-yyy")}",
                    AccentColor = !String.IsNullOrEmpty(nt.AccentColor) ? nt.AccentColor : Constants.GlobalConstants.DefaultStickyNoteAccent,
                    BodyColor = !String.IsNullOrEmpty(nt.BodyColor) ? nt.BodyColor : Constants.GlobalConstants.DefaultStickyNoteBody,
                })
                .ToList();
        }

        private string GetNoteSummary(string noteBody)
        {
            // Supposed to get the First 100 chars of the BODY
            //
            //TODO: Maybe -  NEED to Split out the HTML Elements - etc.
            // So need a Complete HTML Picture - All paragraphs, Spans etc.
            //if (string.IsNullOrEmpty(noteBody)) return string.Empty;

            //string endSpanString = @"\"">";

            //var firstSpanPos = noteBody.IndexOf("<span");
            //var lastSpanPos = noteBody.LastIndexOf("<span");
            //var firstEndSpanPos = noteBody.IndexOf(endSpanString, firstSpanPos);

            
            

            

            return noteBody;
        }

        public void AddNewNote(int matterId, string noteHeader, string noteBody, int userId, Enums.NoteTypeEnum noteType, bool isPublic = false, bool pinned = false)
        {

            MatterNote mn = new MatterNote()
            {
                MatterId = matterId,
                MatterNoteTypeId = (int)noteType,
                NoteHeader = noteHeader,
                NoteBody = noteBody,
                IsPublic = isPublic,
                HighPriority = false,
                IsPinned = pinned,
                IsDeleted = false,
                UpdatedByUserId = userId,
                UpdatedDate = DateTime.Now
            };

            context.MatterNotes.Add(mn);
            context.SaveChanges();
        }

        public void AddWebNote(int matterId, string noteHeader, string noteBody, int userId, bool isPrivate = false, bool msaAndLenderOnly = false, int? overrideNoteTypeId = null)
        {
            int notetypeid = 9;
            if (!msaAndLenderOnly)
                notetypeid = 7;

            if (overrideNoteTypeId.HasValue)
            {
                notetypeid = overrideNoteTypeId.Value;
            }

            MatterNote mn = new MatterNote()
            {
                MatterId = matterId,
                MatterNoteTypeId = notetypeid,
                NoteHeader = noteHeader,
                NoteBody = noteBody,
                IsPublic = true,
                HighPriority = false,
                IsPinned = false,
                IsDeleted = false,
                UpdatedByUserId = userId,
                UpdatedDate = DateTime.Now
            };

            context.MatterNotes.Add(mn);
            context.SaveChanges();
        }
        public void SaveSmartVidsSentNote(MatterWFSmartVidDetail details, bool emailSent, bool smsSent)
        {
            MatterNote mn = new MatterNote()
            {
                MatterId = details.MatterWFComponent.MatterId,
                MatterNoteTypeId = (int)Slick_Domain.Enums.MatterNoteTypeEnum.StatusUpdate,
                NoteHeader = $"SmartVideo Sent to {details.MatterWFSmartVidDetailParty.MatterPartyDisplayFirstname} {details.MatterWFSmartVidDetailParty.MatterPartyDisplayLastname}",
                NoteBody=$"<span style = 'font-family:Calibri; font-size:12pt;'><b>SmartVideo sent successfully</b><br><a href = {details.ResponseUrl}>DIRECT LINK TO VIDEO</a>",
                IsPublic = true,
                HighPriority = false,
                IsPinned = false,
                IsDeleted = false,
                UpdatedByUserId = details.MatterWFComponent.Matter.FileOwnerUserId,
                UpdatedDate = DateTime.Now
            };

            if (emailSent)
            {
                mn.NoteBody += $"<br>Landing Page Sent via Email to <b>{details.MatterWFSmartVidDetailParty.MatterPartySmartvidEmail}</b>";
            }
            if (smsSent)
            {
                mn.NoteBody += $"<br>Landing Page Sent via SMS to <b>{details.MatterWFSmartVidDetailParty.MatterPartySmartvidMobile}</b>";
            }

            context.MatterNotes.Add(mn);

        }
        public int SaveSimpleNote(int matterId, int? matterNoteTypeId, string subject, string body, bool isPublic, bool isPriority, bool isPinned, int? userId = null)
        {
            MatterNote mn = new MatterNote()
            {
                MatterId = matterId,
                MatterNoteTypeId = matterNoteTypeId ?? (int)Slick_Domain.Enums.MatterNoteTypeEnum.StatusUpdate,
                NoteHeader = subject,
                NoteBody = "<div style='font-family: Calibri'>" + body + "</div>",
                IsPublic = isPublic,
                HighPriority = isPriority,
                IsPinned = isPinned,
                IsDeleted = false,
                UpdatedByUserId = userId ?? GlobalVars.CurrentUser.UserId,
                UpdatedDate = DateTime.Now
            };

         
            context.MatterNotes.Add(mn);
            context.SaveChanges();
            return mn.MatterNoteId;

        }
        public void SaveFastRefiToRefiNote(int matterId, int selReasonId, string reason)
        {
            MatterNote mn = new MatterNote()
            {
                MatterId = matterId,
                MatterNoteTypeId = (int)Slick_Domain.Enums.MatterNoteTypeEnum.StatusUpdate,
                NoteHeader = "Matter Changed from Fast Refinance",
                NoteBody = $"Matter has been changed from Fast Refinance to Refinance<br><b>Reason:</b> {context.Reasons.FirstOrDefault(r=>r.ReasonId == selReasonId).ReasonTxt}",
                IsPublic = false,
                HighPriority = false,
                IsPinned = false,
                IsDeleted = false,
                UpdatedByUserId = GlobalVars.CurrentUser.UserId,
                UpdatedDate = DateTime.Now
            };

            if(!String.IsNullOrEmpty(reason))
            {
                mn.NoteBody += $"<br><b>Details:</b> {reason}";
            }

            context.MatterNotes.Add(mn);
            context.SaveChanges();
            
            
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="lenderId"></param>
        /// <param name="pollType"></param>
        /// <returns></returns>
        public DateTime GetBackChannelPollDate(int lenderId, Enums.BackChannelPollTypeEnum pollType)
        {
            DateTime? pollDate = context.LenderBackChannelPolls.Where(x => x.LenderId == lenderId && x.BackChannelPollTypeId == (int)pollType).FirstOrDefault()?.PollDate;
            if (pollDate.HasValue)
            {
                return pollDate.Value;
            }
            else //if they've never polled this is our first poll, so set the date to now
            {
                context.LenderBackChannelPolls.Add(new LenderBackChannelPoll() { LenderId = lenderId, BackChannelPollTypeId = (int)pollType, PollDate = DateTime.Now });
                context.SaveChanges();
                return DateTime.Now;
            }
        }
        /// <summary>
        /// Set the BackC
        /// </summary>
        /// <param name="lenderId"></param>
        /// <param name="pollType"></param>
        /// <param name="pollDate"></param>
        public void SetBackChannelPollDate(int lenderId, Enums.BackChannelPollTypeEnum pollType, DateTime pollDate)
        {
            context.LenderBackChannelPolls.Where(x => x.LenderId == lenderId && x.BackChannelPollTypeId == (int)pollType).FirstOrDefault().PollDate = pollDate;
            context.SaveChanges();
        }
        /// <summary>
        /// Get all matter notes left since last poll date for given lender.
        /// </summary>
        /// <param name="lenderId"></param>
        /// <returns></returns>
        public List<Slick_Domain.Entities.MatterCustomEntities.BackChannelNoteView> GetNotesForPoll(int lenderId)
        {

            DateTime pollDate = GetBackChannelPollDate(lenderId, Enums.BackChannelPollTypeEnum.Notes);

            DateTime cutoff = pollDate.AddHours(-11);

            int incrementTime = Int32.Parse(GlobalVars.GetGlobalTxtVar("LaTrobeNotePollWaitTime", context));

            pollDate = pollDate.ToUniversalTime();
            int parsed = 0;

            List<MatterCustomEntities.BackChannelNoteView> notes =
                context.MatterNotes.Where(x => x.UpdatedDate > cutoff && x.Matter.LenderId == lenderId && !x.User.DefaultPrivateNotes && x.IsPublic && !x.IsDeleted && !x.Matter.IsTestFile && !string.IsNullOrEmpty(x.Matter.SecondaryRefNo))
                .Select(x => new { x.MatterId, x.Matter.LenderRefNo, x.Matter.SecondaryRefNo, x.User.Username, x.UpdatedDate, x.User.StateId, x.MatterNoteType.MatterNoteTypeName, x.NoteHeader, x.NoteBody, value = 0, isInteger = false })
                .ToList()
                .Where(x => x.UpdatedDate.ToUniversalTime() >= pollDate
                    && Int32.TryParse(x.SecondaryRefNo, out parsed))
                .Select(x =>
                    new MatterCustomEntities.BackChannelNoteView
                    (
                        x.MatterId, 
                        x.SecondaryRefNo,
                        x.SecondaryRefNo,
                        x.Username,
                        x.UpdatedDate, 
                        x.MatterNoteTypeName.ToUpper(), 
                        x.NoteHeader, 
                        x.NoteBody,
                        x.StateId
                    )
                ).ToList();


            return notes; 

        }



    }
}
