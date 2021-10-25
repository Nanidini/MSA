using Slick_Domain.Common;
using Slick_Domain.Entities;
using Slick_Domain.Enums;
using Slick_Domain.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Outlook = Microsoft.Office.Interop.Outlook;
using System.Management;
using System.Security.Principal;
using Meziantou.Framework.Win32;
using System.Text.RegularExpressions;

namespace Slick_Domain.Services
{
    public static class EmailsService
    {
        #region "EmailServiceClass"
        private static string emailSubject;
        private static string emailBody;
        private static List<string> attachments;
        private static bool isFinishedEmail;
        private static int endlessLoopCheck;
        private static string replaceValue;
        private static Dictionary<string,Action> emailPlaceHolderActions = null;
        private static EmailEntities.EmailPlaceHolderModel model;

        private static SlickContext _context;


        public static void SendEmail(string subject, string body, ref EmailEntities.EmailPlaceHolderModel emailModel)
        {
            var includeMarketing = GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_IncludeMarketing).ToUpper() == "TRUE";
            var toAddress = GetToEmailAddress(ref emailModel);
            if (string.IsNullOrEmpty(toAddress)) return;

            SmtpClient smtpClient = GetSmtpClient();
            
            MailAddress fromMail = new MailAddress(GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_FallbackEmail));

            MailMessage message = new MailMessage() { ReplyToList = { emailModel.FileOwnerEmail } };

            message.From = fromMail;

            if(emailModel.CCEmail != null)
            {
                foreach (var cc in emailModel.CCEmail.Split(';'))
                {
                    message.CC.Add(cc);
                }
            }
            if (emailModel.BCCEmail != null)
            {
                foreach (var bcc in emailModel.BCCEmail.Split(';'))
                {
                    message.Bcc.Add(bcc);
                }
            }
            var testingEmails = GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmails)?.ToUpper() == "TRUE";
            if (testingEmails)
            {
                var testAddress = GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmail);
                if(message.To != null) message.To.Clear();
                if(message.CC != null) message.CC.Clear();
                if(message.Bcc != null) message.Bcc.Clear();
                foreach (var address in testAddress.Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries))
                {
                    message.To.Add(address);
                }
                                
                subject += $" TESTING - Would be sent to : {toAddress} and CC'd to : {emailModel.EmailMobiles.CCEmails}{emailModel.CCEmail} and BCC'd to : {emailModel.BCCEmail}";
            }
            else
            {
                foreach (var address in toAddress.Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries))
                {
                    message.To.Add(address);
                }
                if (!string.IsNullOrEmpty(emailModel.EmailMobiles.CCEmails))
                {
                    foreach (var ccAddress in emailModel.EmailMobiles.CCEmails.Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        message.CC.Add(ccAddress);
                    }
                }
            }
            
            emailBody = body;
            emailSubject = subject;
            model = emailModel;
            model.EmailRecipient = toAddress + (string.IsNullOrEmpty(emailModel?.EmailMobiles?.CCEmails) ? string.Empty : emailModel.EmailMobiles.CCEmails).ToString();
            ReplaceEmailPlaceHolders();
            message.Body = includeMarketing ? emailBody + RetrieveMarketing() : emailBody;
            message.Subject = emailSubject.Replace("\n","").Replace("\r","");
            message.IsBodyHtml = true;

            smtpClient.Send(message);

            if (!string.IsNullOrEmpty(emailModel.MatterId) && _context != null)
            {
                byte[] messageBytes = Encoding.ASCII.GetBytes($"<head><title>{message.Subject}</title></head><body><b><u>FROM:</u></b>{fromMail.Address}<b><br/><u>TO:</u></b> {message.To}<br/>"
                                + (message.CC.Any() ? $"<b><u>CC:</u></b> {message.CC}<br/>" : "")
                                + (message.Bcc.Any() ? $"<b><u>BCC:</u></b> {message.Bcc}<br/>" : "")
                                + $"<b><u>TIME:</u> </b>{DateTime.Now.ToString()} {TimeZone.CurrentTimeZone.StandardName}<hr>" + message.Body + "</body>"); _context.SaveChanges();
                SaveEmailAsFile(messageBytes, "No-Reply Email - " + message.Subject.ToSafeFileName() + ".html", GlobalVars.CurrentUser.UserId, Int32.Parse(emailModel.MatterId), DocumentDisplayAreaEnum.Attachments, _context);
            }


            var loggingEmails = GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_LoggingEmails)?.ToUpper() == "TRUE";
            if (loggingEmails)
            {
                GeneralCustomEntities.EmailLogView log = new GeneralCustomEntities.EmailLogView(message, emailModel)
                {
                    ModelNoReply = true
                };
                LogEmailData(log);
            }

 

            if (message != null)
            {
                message = null;
            }
        }


        public static bool SendConfirmUserEmail(string email, string code, DateTime codeExpiry)
        {
            //if (GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_SendWFComponentEmails).ToUpper() == DomainConstants.False.ToUpper()
            //    || GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_RunMode) == DomainConstants.Development)
            //    return true;

            try
            {
                SmtpClient smtpClient = new SmtpClient(GlobalVars.GetGlobalTxtVar("MailSMTPServer"))
                {
                    EnableSsl = true
                };

                if (GlobalVars.GetGlobalTxtVar("MailSMTPport") != null)
                    smtpClient.Port = Convert.ToInt32(GlobalVars.GetGlobalTxtVar("MailSMTPport"));


                if (GlobalVars.GetGlobalTxtVar("MailSMTPServer") == null)
                    smtpClient.UseDefaultCredentials = true;
                else
                    smtpClient.Credentials = new System.Net.NetworkCredential(GlobalVars.GetGlobalTxtVar("LoantrakMailUsername"), GlobalVars.GetGlobalTxtVar("LoantrakMailPassword"));

                MailAddress fromMail = new MailAddress(GlobalVars.GetGlobalTxtVar("LoantrakMailUsername"));


                MailAddress toMail = new MailAddress(email);

                MailMessage message = new MailMessage(fromMail, toMail)
                {
                    Subject = "Confirmation Code for your LoanTrak access",

                    Body = "<p style=\"font-family: Arial, sans-serif\" > <u><b>Confirmation Reset Code</u></b></br>"
                };
                message.Body += "</br>To confirm your <i>LoanTrak</i> account, use the code: </br> <b style = \"font-size: 24pt\">" + code + "</b> </br> This code will expire on " + codeExpiry.ToString("dd-MMM-yyyy hh:mm tt") + "</p>";

                message.IsBodyHtml = true;

                smtpClient.Send(message);
            }
            catch (Exception ex)
            {
                Handlers.ErrorHandler.LogError(ex);
                return false;
            }

            return true;
        }

        public static bool SendResetPasswordEmail(string email, string code, DateTime codeExpiry)
        {
            //if (GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_SendWFComponentEmails).ToUpper() == DomainConstants.False.ToUpper()
            //    || GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_RunMode) == DomainConstants.Development)
            //    return true;

            try
            {
                SmtpClient smtpClient = new SmtpClient(GlobalVars.GetGlobalTxtVar("MailSMTPServer"))
                {
                    EnableSsl = true
                };

                if (GlobalVars.GetGlobalTxtVar("MailSMTPport") != null)
                    smtpClient.Port = Convert.ToInt32(GlobalVars.GetGlobalTxtVar("MailSMTPport"));


                if (GlobalVars.GetGlobalTxtVar("MailSMTPServer") == null)
                    smtpClient.UseDefaultCredentials = true;
                else
                    smtpClient.Credentials = new System.Net.NetworkCredential(GlobalVars.GetGlobalTxtVar("LoantrakMailUsername"), GlobalVars.GetGlobalTxtVar("LoantrakMailPassword"));

                MailAddress fromMail = new MailAddress(GlobalVars.GetGlobalTxtVar("LoantrakMailUsername"));

                
                MailAddress toMail = new MailAddress(email);

                MailMessage message = new MailMessage(fromMail, toMail)
                {
                    Subject = "Password Reset Code for your LoanTrak access",

                    Body = "<p style=\"font-family: Arial, sans-serif\" > <u><b>LoanTrak Password Reset Code</u></b></br>"
                };
                message.Body += "</br>To reset your <i>LoanTrak</i> password, use the code: </br> <b style = \"font-size: 24pt\">" + code + "</b> </br> This code will expire on " + codeExpiry.ToString("dd-MMM-yyyy hh:mm tt") + "</p>";
                
                message.IsBodyHtml = true;

                smtpClient.Send(message);
            }
            catch (Exception ex)
            {
                Handlers.ErrorHandler.LogError(ex);
                return false;
            }

            return true;
        }

        public static string GetTimeOfDay(DateTime time)
        {
            string TimeOfDay;
            if (time.Hour < 12)
            {
                TimeOfDay = "morning";
            }
            else if (time.Hour < 17)
            {
                TimeOfDay = "afternoon";
            }
            else
            {
                TimeOfDay = "evening";
            }
            return TimeOfDay;

        }
        public static bool SendPexaExceptionEmail(string email, int matterId, string PexaWorkspaceNo, string statusName)
        {
            //if (GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_SendWFComponentEmails).ToUpper() == DomainConstants.False.ToUpper()
            //    || GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_RunMode) == DomainConstants.Development)
            //    return true;
            //if (GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmails).ToUpper() == "TRUE")
            //{
            //    email = GlobalVars.CurrentUser.Email;
            //}

            string TimeOfDay;
            if (DateTime.Now.Hour < 12)
            {
                TimeOfDay = "morning";
            }
            else if (DateTime.Now.Hour < 17)
            {
                TimeOfDay = "afternoon";
            }
            else
            {
                TimeOfDay = "evening";
            }

            try
            {
                SmtpClient smtpClient = new SmtpClient(GlobalVars.GetGlobalTxtVar("MailSMTPServer"))
                {
                    EnableSsl = true
                };

                if (GlobalVars.GetGlobalTxtVar("MailSMTPport") != null)
                    smtpClient.Port = Convert.ToInt32(GlobalVars.GetGlobalTxtVar("MailSMTPport"));

                if (GlobalVars.GetGlobalTxtVar("MailSMTPServer") == null)
                    smtpClient.UseDefaultCredentials = true;
                else
                    smtpClient.Credentials = new System.Net.NetworkCredential(GlobalVars.GetGlobalTxtVar("MailCredUser"), GlobalVars.GetGlobalTxtVar("MailCredPass"));

                MailAddress fromMail = new MailAddress(GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_FallbackEmail));
                MailAddress toMail = new MailAddress(email);

                MailMessage message = new MailMessage(fromMail, toMail)
                {
                    Subject = $"MATTER {matterId.ToString()} AWAITING COMPLETION OF SETTLEMENT",

                    Body = $"<p style=\"font-family: Arial, sans-serif\" >Good {TimeOfDay},</br>"
                };
                message.Body += $"Pexa Workspace <b>{PexaWorkspaceNo}</b> has now been settled, and is at the stage of <b>{statusName}</b>.</br> " +
                                $"However, the matter's workflow is not currently at the milestone of Settlement Complete.</br>" +
                                $"Please manually mark settlement as completed when ready.</br>";

                message.IsBodyHtml = true;

                smtpClient.Send(message);
            }
            catch (Exception ex)
            {
                Handlers.ErrorHandler.LogError(ex);
                return false;
            }

            return true;
        }
        public static bool SendPexaDiffSettlementDatesEmail(string email, int matterId, List<Tuple<int, DateTime?, string, string, string>> mattersDates, string PexaWorkspaceNo, string statusName)
        {
            //if (GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_SendWFComponentEmails).ToUpper() == DomainConstants.False.ToUpper()
            //    || GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_RunMode) == DomainConstants.Development)
            //    return true;
            //if (GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmails).ToUpper() == "TRUE")
            //{
            //    email = GlobalVars.CurrentUser.Email;
            //}

            string TimeOfDay;
            if (DateTime.Now.Hour < 12)
            {
                TimeOfDay = "morning";
            }
            else if (DateTime.Now.Hour < 17)
            {
                TimeOfDay = "afternoon";
            }
            else
            {
                TimeOfDay = "evening";
            }

            try
            {
                SmtpClient smtpClient = new SmtpClient(GlobalVars.GetGlobalTxtVar("MailSMTPServer"))
                {
                    EnableSsl = true
                };

                if (GlobalVars.GetGlobalTxtVar("MailSMTPport") != null)
                    smtpClient.Port = Convert.ToInt32(GlobalVars.GetGlobalTxtVar("MailSMTPport"));

                if (GlobalVars.GetGlobalTxtVar("MailSMTPServer") == null)
                    smtpClient.UseDefaultCredentials = true;
                else
                    smtpClient.Credentials = new System.Net.NetworkCredential(GlobalVars.GetGlobalTxtVar("MailCredUser"), GlobalVars.GetGlobalTxtVar("MailCredPass"));

                MailAddress fromMail = new MailAddress(GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_FallbackEmail));
                MailAddress toMail = new MailAddress(email);

                MailMessage message = new MailMessage(fromMail, toMail)
                {
                    Subject = $"MATTER {matterId.ToString()} AWAITING COMPLETION OF SETTLEMENT",

                    Body = $"<p style=\"font-family: Arial, sans-serif\" >Good {TimeOfDay},</p>"
                };
                message.Body += $"<p style='font-family: Arial, sans-serif;'>Pexa Workspace <b>{PexaWorkspaceNo}</b> has now been settled, and is at the stage of <b>{statusName}</b>.</br> " +
                                $"However, Settlement could not be marked as complete as multiple matters with different settlement dates were found.</br></p>" +
                                $"<p style='font-family: Arial, sans-serif;'>Details are as follows:</p>";

                foreach(var date in mattersDates.Where(i=>i.Item1 != matterId))
                {
                    if (date.Item2.HasValue)
                    {
                        message.Body += $"<p style='font-family: Arial, sans-serif;'><b>MATTER: </b>{date.Item1}<br><b>SETTLEMENT DATE: </b>{date.Item2.Value.ToShortDateString()}<br><b>FILE OWNER: </b> {date.Item3} {date.Item4} - {date.Item5}</b></p>";
                    }
                    else
                    {
                        message.Body += $"<p style='font-family: Arial, sans-serif;'><b>MATTER: </b>{date.Item1}<br><b>SETTLEMENT DATE: </b><i>N/A</i><br><b>FILE OWNER: </b> {date.Item3} {date.Item4} - {date.Item5}</b></p>";
                    }
                }

                message.Body += $"<p style='font-family: Arial, sans-serif;'>Please manually mark settlement as completed when ready.</br></p>";

                message.IsBodyHtml = true;

                smtpClient.Send(message);
            }
            catch (Exception ex)
            {
                Handlers.ErrorHandler.LogError(ex);
                return false;
            }

            return true;
        }



        public static bool SendPexaFutureSettlementDatesEmail(string email, int matterId, List<Tuple<int, DateTime?, string, string, string>> mattersDates, string PexaWorkspaceNo, string statusName)
        {
            //if (GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_SendWFComponentEmails).ToUpper() == DomainConstants.False.ToUpper()
            //    || GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_RunMode) == DomainConstants.Development)
            //    return true;
            //if (GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmails).ToUpper() == "TRUE")
            //{
            //    email = GlobalVars.CurrentUser.Email;
            //}

            string TimeOfDay;
            if (DateTime.Now.Hour < 12)
            {
                TimeOfDay = "morning";
            }
            else if (DateTime.Now.Hour < 17)
            {
                TimeOfDay = "afternoon";
            }
            else
            {
                TimeOfDay = "evening";
            }

            try
            {
                SmtpClient smtpClient = new SmtpClient(GlobalVars.GetGlobalTxtVar("MailSMTPServer"))
                {
                    EnableSsl = true
                };

                if (GlobalVars.GetGlobalTxtVar("MailSMTPport") != null)
                    smtpClient.Port = Convert.ToInt32(GlobalVars.GetGlobalTxtVar("MailSMTPport"));

                if (GlobalVars.GetGlobalTxtVar("MailSMTPServer") == null)
                    smtpClient.UseDefaultCredentials = true;
                else
                    smtpClient.Credentials = new System.Net.NetworkCredential(GlobalVars.GetGlobalTxtVar("MailCredUser"), GlobalVars.GetGlobalTxtVar("MailCredPass"));

                MailAddress fromMail = new MailAddress(GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_FallbackEmail));
                MailAddress toMail = new MailAddress(email);

                MailMessage message = new MailMessage(fromMail, toMail)
                {
                    Subject = $"MATTER {matterId.ToString()} AWAITING COMPLETION OF SETTLEMENT",

                    Body = $"<p style=\"font-family: Arial, sans-serif\" >Good {TimeOfDay},</p>"
                };
                message.Body += $"<p style='font-family: Arial, sans-serif;'>Pexa Workspace <b>{PexaWorkspaceNo}</b> has now been settled, and is at the stage of <b>{statusName}</b>.</br> " +
                                $"However, Settlement could not be marked as complete as the settlement date in Slick is for the future.</br></p>" +
                                $"<p style='font-family: Arial, sans-serif;'>Details are as follows:</p>";

                foreach (var date in mattersDates.Where(i => i.Item1 != matterId))
                {
                    if (date.Item2.HasValue)
                    {
                        message.Body += $"<p style='font-family: Arial, sans-serif;'><b>MATTER: </b>{date.Item1}<br><b>SETTLEMENT DATE: </b>{date.Item2.Value.ToShortDateString()}<br><b>FILE OWNER: </b> {date.Item3} {date.Item4} - {date.Item5}</b></p>";
                    }
                    else
                    {
                        message.Body += $"<p style='font-family: Arial, sans-serif;'><b>MATTER: </b>{date.Item1}<br><b>SETTLEMENT DATE: </b><i>N/A</i><br><b>FILE OWNER: </b> {date.Item3} {date.Item4} - {date.Item5}</b></p>";
                    }
                }

                message.Body += $"<p style='font-family: Arial, sans-serif;'>Please amend the settlement date and manually mark settlement as completed for these matters when ready.</br></p>";

                message.IsBodyHtml = true;

                smtpClient.Send(message);
            }
            catch (Exception ex)
            {
                Handlers.ErrorHandler.LogError(ex);
                return false;
            }

            return true;
        }









        public static bool SendResetPasswordText(string email, string code, DateTime codeExpiry)
        {
            //if (GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_SendWFComponentEmails).ToUpper() == DomainConstants.False.ToUpper()
            //    || GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_RunMode) == DomainConstants.Development)
            //    return true;

            var Subject = "Password Reset Code for your LoanTrak access";
            var Body = "To reset your LoanTrak password, use the code: " + code + ".  This code will expire on " + codeExpiry.ToString("dd-MMM-yyyy hh:mm tt") + ".";
            try
            {
                SmsService.SendMacquarieNowSms(email, Body, Subject);
                //if (loggingEmails)	
                //{	
                //    GeneralCustomEntities.EmailLogView log = new GeneralCustomEntities.EmailLogView($"{i + 1}/{splits.Count()} : {subject}", split, mobile, emailModel, updatedByUser);	
                //    LogEmailData(log);	
                //}	
            }
            catch (Exception ex)
            {
                Handlers.ErrorHandler.LogError(ex);
                return false;
            }

            //try
            //{
            //    SmtpClient smtpClient = new SmtpClient(GlobalVars.GetGlobalTxtVar("MailSMTPServer"))
            //    {
            //        EnableSsl = true
            //    };

            //    if (GlobalVars.GetGlobalTxtVar("MailSMTPport") != null)
            //        smtpClient.Port = Convert.ToInt32(GlobalVars.GetGlobalTxtVar("MailSMTPport"));

            //    if (GlobalVars.GetGlobalTxtVar("MailSMTPServer") == null)
            //        smtpClient.UseDefaultCredentials = true;
            //    else
            //        smtpClient.Credentials = new System.Net.NetworkCredential(GlobalVars.GetGlobalTxtVar("MailCredUser"), GlobalVars.GetGlobalTxtVar("MailCredPass"));

            //    MailAddress fromMail = new MailAddress(GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_FallbackEmail));
            //    MailAddress toMail = new MailAddress(email);

            //    MailMessage message = new MailMessage(fromMail, toMail)
            //    {
            //        Subject = "Password Reset Code for your LoanTrak access",

            //        Body = "</br>To reset your <i>LoanTrak</i> password, use the code: </br> <b style = \"font-size: 24pt\">" + code + "</b> </br> This code will expire on " + codeExpiry.ToString("dd-MMM-yyyy hh:mm tt") + "</p>",

            //        IsBodyHtml = true
            //    };

            //    smtpClient.Send(message);
            //}
            //catch (Exception ex)
            //{
            //    Handlers.ErrorHandler.LogError(ex);
            //    return false;
            //}

            return true;
        }

        public static bool SendBrokerVerificationText(string mobile, string code, DateTime codeExpiry)
        {
            //if (GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_SendWFComponentEmails).ToUpper() == DomainConstants.False.ToUpper()	
            //    || GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_RunMode) == DomainConstants.Development)	
            //    return true;	
            var Subject = "Loantrak - View Matter Verification Code";
            var Body = "To view matter in Loantrak, use the code: " + code + ".  This code will expire on " + codeExpiry.ToString("dd-MMM-yyyy hh:mm tt") + ".";
            try
            {
                SmsService.SendMacquarieNowSms(mobile, Body, Subject);
                //if (loggingEmails)	
                //{	
                //    GeneralCustomEntities.EmailLogView log = new GeneralCustomEntities.EmailLogView($"{i + 1}/{splits.Count()} : {subject}", split, mobile, emailModel, updatedByUser);	
                //    LogEmailData(log);	
                //}	
            }
            catch (Exception ex)
            {
                Handlers.ErrorHandler.LogError(ex);
                return false;
            }
            return true;
        }

        public static bool SendBrokerVerificationEmail(string email, string code, DateTime codeExpiry)
        {
            //if (GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_SendWFComponentEmails).ToUpper() == DomainConstants.False.ToUpper()
            //    || GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_RunMode) == DomainConstants.Development)
            //    return true;

            try
            {
                SmtpClient smtpClient = new SmtpClient(GlobalVars.GetGlobalTxtVar("MailSMTPServer"))
                {
                    EnableSsl = true
                };

                if (GlobalVars.GetGlobalTxtVar("MailSMTPport") != null)
                    smtpClient.Port = Convert.ToInt32(GlobalVars.GetGlobalTxtVar("MailSMTPport"));

                if (GlobalVars.GetGlobalTxtVar("MailSMTPServer") == null)
                    smtpClient.UseDefaultCredentials = true;
                else
                    smtpClient.Credentials = new System.Net.NetworkCredential(GlobalVars.GetGlobalTxtVar("MailCredUser"), GlobalVars.GetGlobalTxtVar("MailCredPass"));

                MailAddress fromMail = new MailAddress(GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_FallbackEmail));
                MailAddress toMail = new MailAddress(email);

                MailMessage message = new MailMessage(fromMail, toMail)
                {
                    Subject = "Loantrak - View Matter Verification Code",

                    Body = "</br>To view matter in Loantrak, use the code: </br> <b style = \"font-size: 24pt\">" + code + "</b> </br></br>  This code will expire on " + codeExpiry.ToString("dd-MMM-yyyy hh:mm tt") + "</p>",

                    IsBodyHtml = true
                };

                smtpClient.Send(message);
            }
            catch (Exception ex)
            {
                Handlers.ErrorHandler.LogError(ex);
                return false;
            }

            return true;
        }
        public static bool SendLenderSignupEmail(User user, string tempPassword)
        {
            try
            {
                string email = user.Email;
                string templateDirectory = GlobalVars.GetGlobalTxtVar("LoantrakEmailPath");
                SmtpClient smtpClient = new SmtpClient(GlobalVars.GetGlobalTxtVar("MailSMTPServer"))
                {
                    EnableSsl = true
                };

                if (GlobalVars.GetGlobalTxtVar("MailSMTPport") != null)
                    smtpClient.Port = Convert.ToInt32(GlobalVars.GetGlobalTxtVar("MailSMTPport"));

                if (GlobalVars.GetGlobalTxtVar("MailSMTPServer") == null)
                    smtpClient.UseDefaultCredentials = true;
                else
                    smtpClient.Credentials = new System.Net.NetworkCredential(GlobalVars.GetGlobalTxtVar("MailCredUser"), GlobalVars.GetGlobalTxtVar("MailCredPass"));

                MailAddress fromMail = new MailAddress(GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_FallbackEmail));
                MailAddress toMail = new MailAddress(email);

                string messageSubject = "Your LoanTrak Account Login and Password";

                bool testingEmails = GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmails)?.ToUpper() == "TRUE";

                if (testingEmails)
                {
                    toMail = new MailAddress(GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmail));
                    messageSubject += $" | Would have been sent to:{email}";
                }


                MailMessage message = new MailMessage(fromMail, toMail)
                {
                    Subject = messageSubject
                };

                string TimeOfDay;
                if (DateTime.Now.Hour < 12)
                {
                    TimeOfDay = "morning";
                }
                else if (DateTime.Now.Hour < 17)
                {
                    TimeOfDay = "afternoon";
                }
                else
                {
                    TimeOfDay = "evening";
                }

                message.Attachments.Add(new Attachment(Path.Combine(templateDirectory,"LoanTrakUserGuide.pdf")) { Name = "MSANational_LoanTrakUserGuide.pdf" });
                //message.Body = "<p style=\"font-family: Arial, sans-serif\">Good " + TimeOfDay + ", " + user.Firstname + ".</br>";
                //message.Body += "</br>A request was received to create a new LoanTrak account in your name.";
                //message.Body += "</br>An account has been created, and you may now log in using your email address and the following temporary password:";
                //message.Body += "<p style=\"font-family: Arial, sans-serif\" ><b style = \"font-size: 24pt\">" + tempPassword + "</b></p>";
                //message.Body += "<p style=\"font-family: Arial, sans-serif\" >The first time you log in, you will be automatically guided through resetting your password. Please have your mobile phone ready for this process.";
                //message.Body += "</br>If you did not request an account to be created, please ignore this email or contact the MSA support team.</p>";
                //message.Body += "<p style=\"font-family: Arial, sans-serif\" > Kind regards, </br>MSA National";
                //message.Body += "</p></p><p style=\"font-family: Arial, sans-serif\" ><i>This email was sent automatically, replies to this inbox are not monitored. For assistance, please contact George Kogios at george.kogios@msanational.com.au.</i></p>";

                string bodyText = "";

                using (StreamReader reader = File.OpenText(Path.Combine(templateDirectory, "EmailTemplateLender.html")))
                {
                    bodyText = reader.ReadToEnd();
                    bodyText = bodyText.Replace("[TimeOfDay]", TimeOfDay)
                        .Replace("[FirstName]", user.Firstname)
                        .Replace("[UserEmail]", user.Email)
                        .Replace("[TempPassword]", tempPassword);
                }

                message.Body = bodyText;
                message.IsBodyHtml = true;

                smtpClient.Send(message);
            }
            catch (Exception ex)
            {
                Handlers.ErrorHandler.LogError(ex);
                return false;
            }

            return true;

        }

        public static void SendSimpleNoReplyEmail(List<string> emailTo, List<string> emailCC, string emailSubject, string emailBody, List<string> attachments = null)
        {
            try
            {
                SmtpClient smtpClient = new SmtpClient(GlobalVars.GetGlobalTxtVar("MailSMTPServer"))
                {
                    EnableSsl = true
                };

                if (GlobalVars.GetGlobalTxtVar("MailSMTPport") != null)
                    smtpClient.Port = Convert.ToInt32(GlobalVars.GetGlobalTxtVar("MailSMTPport"));

                if (GlobalVars.GetGlobalTxtVar("MailSMTPServer") == null)
                    smtpClient.UseDefaultCredentials = true;
                else
                    smtpClient.Credentials = new System.Net.NetworkCredential(GlobalVars.GetGlobalTxtVar("MailCredUser"), GlobalVars.GetGlobalTxtVar("MailCredPass"));


                MailMessage message = new MailMessage();
                string from = GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_FallbackEmail);
                MailAddress fromMail = new MailAddress(from);

                message.From = fromMail;
                if (attachments != null)
                {
                    foreach (var attachment in attachments)
                    {
                        message.Attachments.Add(new Attachment(attachment));
                    }
                }

                foreach (var address in emailTo)
                {
                    MailAddress toEmail = new MailAddress(address);
                    message.To.Add(toEmail);
                }
                foreach (var address in emailCC)
                {
                    MailAddress ccEmail = new MailAddress(address);
                    message.CC.Add(ccEmail);
                }

                message.Subject = emailSubject;
                message.Body = "<span style='font-family:Calibri;font-size:11pt;'>"+emailBody+"</span>";
                message.IsBodyHtml = true;

                smtpClient.Send(message);
            }
            catch(Exception ex)
            {
                Handlers.ErrorHandler.LogError(ex);
            }
        }


        public static bool SendTempPasswordEmail(User user, string tempPassword, bool isBroker = false)
        {
            //if (GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_SendWFComponentEmails).ToUpper() == DomainConstants.False.ToUpper()
            //    || GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_RunMode) == DomainConstants.Development)
            //    return true;

            try
            {
                string email = user.Email;
                SmtpClient smtpClient = new SmtpClient(GlobalVars.GetGlobalTxtVar("MailSMTPServer"))
                {
                    EnableSsl = true
                };

                if (GlobalVars.GetGlobalTxtVar("MailSMTPport") != null)
                    smtpClient.Port = Convert.ToInt32(GlobalVars.GetGlobalTxtVar("MailSMTPport"));

                if (GlobalVars.GetGlobalTxtVar("MailSMTPServer") == null)
                    smtpClient.UseDefaultCredentials = true;
                else
                    smtpClient.Credentials = new System.Net.NetworkCredential(GlobalVars.GetGlobalTxtVar("LoantrakMailUsername"), GlobalVars.GetGlobalTxtVar("LoantrakMailPassword"));

                MailAddress fromMail = new MailAddress(GlobalVars.GetGlobalTxtVar("LoantrakMailUsername"));



                string templateDirectory = GlobalVars.GetGlobalTxtVar("LoantrakEmailPath");



                MailAddress toMail = new MailAddress(email);




                MailMessage message = new MailMessage(fromMail, toMail)
                {
                    Subject = "Your LoanTrak Account Login and Password"
                };

                var testingEmails = GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmails)?.ToUpper() == "TRUE";
                if (testingEmails)
                {
                    message.Subject += $" | WOULD HAVE BEEN SENT TO {message.To}";
                    message.To.Clear();
                    message.To.Add(GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmail));

                }


                string TimeOfDay;
                if (DateTime.Now.Hour < 12)
                {
                    TimeOfDay = "morning";
                } 
                else if(DateTime.Now.Hour < 17)
                {
                    TimeOfDay = "afternoon";
                }
                else
                {
                    TimeOfDay = "evening";
                }
                message.Attachments.Add(new Attachment(Path.Combine(templateDirectory,"LoanTrakUserGuide.pdf")) {Name = "MSA_LoanTrakUserGuide.pdf"});
                //message.Body = "<p style=\"font-family: Arial, sans-serif\">Good " + TimeOfDay + ", " + user.Firstname + ".</br>";
                //message.Body += "</br>A request was received to create a new LoanTrak account in your name.";
                //message.Body += "</br>An account has been created, and you may now log in using your email address and the following temporary password:";
                //message.Body += "<p style=\"font-family: Arial, sans-serif\" ><b style = \"font-size: 24pt\">" + tempPassword + "</b></p>";
                //message.Body += "<p style=\"font-family: Arial, sans-serif\" >The first time you log in, you will be automatically guided through resetting your password. Please have your mobile phone ready for this process.";
                //message.Body += "</br>If you did not request an account to be created, please ignore this email or contact the MSA support team.</p>";
                //message.Body += "<p style=\"font-family: Arial, sans-serif\" > Kind regards, </br>MSA National";
                //message.Body += "</p></p><p style=\"font-family: Arial, sans-serif\" ><i>This email was sent automatically, replies to this inbox are not monitored. For assistance, please contact George Kogios at george.kogios@msanational.com.au.</i></p>";

                string bodyText = "";
                if (!isBroker)
                {
                    using (StreamReader reader = File.OpenText(Path.Combine(templateDirectory, "EmailTemplate.html")))
                    {
                        bodyText = reader.ReadToEnd();
                        bodyText = bodyText.Replace("[TimeOfDay]", TimeOfDay)
                            .Replace("[FirstName]", user.Firstname)
                            .Replace("[UserEmail]", user.Email)
                            .Replace("[TempPassword]", tempPassword);
                    }
                }
                else
                {
                    using (StreamReader reader = File.OpenText(Path.Combine(templateDirectory, "EmailTemplateBroker.html")))
                    {
                        bodyText = reader.ReadToEnd();
                        bodyText = bodyText.Replace("[TimeOfDay]", TimeOfDay)
                            .Replace("[FirstName]", user.Firstname)
                            .Replace("[UserEmail]", user.Email)
                            .Replace("[TempPassword]", tempPassword);
                    }
                }

                message.Body = bodyText;
                message.IsBodyHtml = true;

                smtpClient.Send(message);
            }
            catch (Exception ex)
            {
                Handlers.ErrorHandler.LogError(ex);
                return false;
            }

            return true;
        }
        public static bool SendNewPasswordEmail(int userId, string tempPassword, bool isBroker = false)
        {
            //if (GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_SendWFComponentEmails).ToUpper() == DomainConstants.False.ToUpper()
            //    || GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_RunMode) == DomainConstants.Development)
            //    return true;

            try
            {
                User user;
                using (var context = new SlickContext())
                {
                    var selUser = context.Users.Select(u=>new{ u.UserId, u.Email, u.Mobile, u.Firstname, u.Lastname, u.UserTypeId  }).Where(u => u.UserId == userId).FirstOrDefault();
                    user = new User { UserId = selUser.UserId, Email = selUser.Email, Mobile = selUser.Mobile, Firstname = selUser.Firstname, Lastname = selUser.Lastname, UserTypeId = selUser.UserTypeId };
                    if (user == null)
                    {
                        return false;
                    }
                }
                string templateDirectory = GlobalVars.GetGlobalTxtVar("LoantrakEmailPath");

                string email = user.Email;
                SmtpClient smtpClient = new SmtpClient(GlobalVars.GetGlobalTxtVar("MailSMTPServer"))
                {
                    EnableSsl = true
                };

                if (GlobalVars.GetGlobalTxtVar("MailSMTPport") != null)
                    smtpClient.Port = Convert.ToInt32(GlobalVars.GetGlobalTxtVar("MailSMTPport"));


                if (GlobalVars.GetGlobalTxtVar("MailSMTPServer") == null)
                    smtpClient.UseDefaultCredentials = true;
                else
                    smtpClient.Credentials = new System.Net.NetworkCredential(GlobalVars.GetGlobalTxtVar("LoantrakMailUsername"), GlobalVars.GetGlobalTxtVar("LoantrakMailPassword"));

                MailAddress fromMail = new MailAddress(GlobalVars.GetGlobalTxtVar("LoantrakMailUsername"));

                MailAddress toMail = new MailAddress(email);

                MailMessage message = new MailMessage(fromMail, toMail)
                {
                    Subject = "Your LoanTrak password has been reset."
                };

                string TimeOfDay;
                if (DateTime.Now.Hour < 12)
                {
                    TimeOfDay = "morning";
                }
                else if (DateTime.Now.Hour < 17)
                {
                    TimeOfDay = "afternoon";
                }
                else
                {
                    TimeOfDay = "evening";
                }
      
                string bodyText = "";

                using (StreamReader reader = File.OpenText(Path.Combine(templateDirectory, "EmailTemplatePWReset.html")))
                {
                    bodyText = reader.ReadToEnd();
                    bodyText = bodyText.Replace("[TimeOfDay]", TimeOfDay)
                        .Replace("[FirstName]", user.Firstname)
                        .Replace("[UserEmail]", user.Email)
                        .Replace("[TempPassword]", tempPassword);
                    if (user.UserTypeId == (int)Enums.UserTypeEnum.Broker)
                    {
                        bodyText = bodyText.Replace("[BrokerResetRequired]", "Upon your first login with these details, you will be automatically guided through resetting your password. Please ensure you have your mobile phone ready for this process.");
                    }
                    else
                    {
                        bodyText = bodyText.Replace("[BrokerResetRequired]", "");
                    }
                }
               

                message.Body = bodyText;
                message.IsBodyHtml = true;

                smtpClient.Send(message);
            }
            catch (Exception ex)
            {
                Handlers.ErrorHandler.LogError(ex);
                return false;
            }

            return true;
        }
        public static bool SendOutstandingItemUploadedEmail(User user, int matterId, string content)
        {


            try
            {
                SmtpClient smtpClient = new SmtpClient(GlobalVars.GetGlobalTxtVar("MailSMTPServer"))
                {
                    EnableSsl = true
                };

                if (GlobalVars.GetGlobalTxtVar("MailSMTPport") != null)
                    smtpClient.Port = Convert.ToInt32(GlobalVars.GetGlobalTxtVar("MailSMTPport"));

                if (GlobalVars.GetGlobalTxtVar("MailSMTPServer") == null)
                    smtpClient.UseDefaultCredentials = true;
                else
                    smtpClient.Credentials = new System.Net.NetworkCredential(GlobalVars.GetGlobalTxtVar("MailCredUser"), GlobalVars.GetGlobalTxtVar("MailCredPass"));

                string email = "";
                string senderName = user.Fullname;
                string senderUserType = user.UserType.UserTypeDesc;
                string senderEmail = user.Email;
                string matterDescription;

                using (var context = new SlickContext())
                {
                    using (var matterRepo = new MatterRepository(context))
                    {

                        var m = matterRepo.GetMatterView(matterId);

                        email = m.FileOwnerUsername.Contains("@") ? m.FileOwnerUsername : m.FileOwnerUsername + "@msanational.com.au";
                        matterDescription = m.MatterDescription;

                    }
                    //.Where(m=>m.MatterId == MatterId).Select(m=> m.FileOwnerUserId)
                }
                MailAddress fromMail = new MailAddress(GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_FallbackEmail));
                MailAddress toMail = new MailAddress(email);

                MailMessage message = new MailMessage(fromMail, toMail)
                {
                    Subject = "LOANTRAK: Outstanding Item Uploaded on Matter " + matterId.ToString() + " - " + matterDescription,

                    Body = $"<p><span style=\"font-family: arial; font-size: '12pt';\"><b>MATTER: {matterId.ToString()} - {matterDescription}</b></span></br>"
                };
                message.Body += $"<p><span style=\"font-family: arial; font-size: '10pt';\"> A new item has been uploaded via LoanTrak. </p></p></p> <b>LEFT BY:</b> </br>{senderName}, {senderUserType}</br></br><b>EMAIL:</b></br>{senderEmail}</br></br></p></span>";
                message.Body += $"<p><span style = \"font-family:arial; font-size:'10pt';\"><hr><b>Outstanding FILE</b><hr>{content}</span></p><hr>";

                message.Body += content;

                message.IsBodyHtml = true;

                smtpClient.Send(message);
            }
            catch (Exception ex)
            {
                Handlers.ErrorHandler.LogError(ex);
                return false;
            }

            return true;
        }

        public static bool SendNoteNotificationEmail(SlickContext context, User user, int matterId, string content)
        {


            try
            {
                SmtpClient smtpClient = new SmtpClient(GlobalVars.GetGlobalTxtVar("MailSMTPServer"))
                {
                    EnableSsl = true
                };

                if (GlobalVars.GetGlobalTxtVar("MailSMTPport") != null)
                    smtpClient.Port = Convert.ToInt32(GlobalVars.GetGlobalTxtVar("MailSMTPport"));

                if (GlobalVars.GetGlobalTxtVar("MailSMTPServer") == null)
                    smtpClient.UseDefaultCredentials = true;
                else
                    smtpClient.Credentials = new System.Net.NetworkCredential(GlobalVars.GetGlobalTxtVar("MailCredUser"), GlobalVars.GetGlobalTxtVar("MailCredPass"));

                string email = "";
                string senderName = user.Fullname;
                string senderUserType = user.UserType.UserTypeDesc; 
                string senderEmail = user.Email;
                string matterDescription;

                
                using (var matterRepo = new MatterRepository(context))
                {

                    var m = matterRepo.GetMatterView(matterId);

                    email = m.FileOwnerUsername.Contains("@")? m.FileOwnerUsername : m.FileOwnerUsername+"@msanational.com.au";
                    matterDescription = m.MatterDescription;
                       
                }
                         //.Where(m=>m.MatterId == MatterId).Select(m=> m.FileOwnerUserId)
                
                MailAddress fromMail = new MailAddress(GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_FallbackEmail));
                MailAddress toMail = new MailAddress(email);

                MailMessage message = new MailMessage(fromMail, toMail)
                {
                    Subject = "LOANTRAK: New Note on Matter " + matterId.ToString() + " - " + matterDescription,

                    Body = $"<p><span style=\"font-family: arial; font-size: '12pt';\"><b>MATTER: {matterId.ToString()} - {matterDescription}</b></span></br>"
                };
                message.Body += $"<p><span style=\"font-family: arial; font-size: '10pt';\"> A new note has been left via LoanTrak. </p></p></p> <b>LEFT BY:</b> </br>{senderName}, {senderUserType}</br></br><b>EMAIL:</b></br>{senderEmail}</br></br></p></span>";
                message.Body += $"<p><span style = \"font-family:arial; font-size:'10pt';\"><hr><b>NOTE CONTENTS</b><hr>{content}</span></p><hr>";

                message.Body += content;
                
                message.IsBodyHtml = true;

                smtpClient.Send(message);
            }
            catch (Exception ex)
            {
                Handlers.ErrorHandler.LogError(ex);
                return false;
            }

            return true;
        }
        public static bool SendNoteConfirmationEmail(SlickContext context, User user, int MatterId, string content)
        {


            try
            {
                SmtpClient smtpClient = new SmtpClient(GlobalVars.GetGlobalTxtVar("MailSMTPServer"))
                {
                    EnableSsl = true
                };

                if (GlobalVars.GetGlobalTxtVar("MailSMTPport") != null)
                    smtpClient.Port = Convert.ToInt32(GlobalVars.GetGlobalTxtVar("MailSMTPport"));

                if (GlobalVars.GetGlobalTxtVar("MailSMTPServer") == null)
                    smtpClient.UseDefaultCredentials = true;
                else
                    smtpClient.Credentials = new System.Net.NetworkCredential(GlobalVars.GetGlobalTxtVar("MailCredUser"), GlobalVars.GetGlobalTxtVar("MailCredPass"));

                string email = user.Email;
                string userFirstName = user.Firstname;
                string FileManagerName;
                string FileManagerEmail;
                string MatterDescription;
                string LenderName;
                string LenderRef;

                var matterRepo = new MatterRepository(context);
                
                var m = matterRepo.GetMatterView(MatterId);

                FileManagerName = m.FileOwnerFullName;
                FileManagerEmail = m.FileOwnerUsername.Contains("@") ? m.FileOwnerUsername : m.FileOwnerUsername + "@msanational.com.au";
                MatterDescription = m.MatterDescription;
                LenderName = m.LenderName;
                LenderRef = m.LenderRefNo;
                
                //.Where(m=>m.MatterId == MatterId).Select(m=> m.FileOwnerUserId)
                
                MailAddress fromMail = new MailAddress(GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_FallbackEmail));
                MailAddress toMail = new MailAddress(email);

                MailMessage message = new MailMessage(fromMail, toMail)
                {
                    Subject = $"LOANTRAK: Your note has been left on Loan {MatterId.ToString()} - {MatterDescription}",
                    //------------------------------------------------------------------------------------------------------
                    Body = $"<p><span style=\"font-family: arial; font-size: '12pt';\"><b>LOAN: {MatterId.ToString()} - {MatterDescription}</b></span></br>"
                };
                message.Body   += $"<span style=\"font-family: arial; font-size: '10pt';\">{LenderName} reference: {LenderRef}</span></p>";
                message.Body   += $"<p><span style=\"font-family: arial; font-size: '10pt';\">Dear {userFirstName},</br></br>Your note has been left, and sent to the loan's MSA National file manager {FileManagerName} at {FileManagerEmail}</span></p></p></p>";
                message.Body   += $"<p><span style = \"font-family:arial; font-size:'10pt';\"><hr><b>NOTE CONTENTS</b><hr>{content}</span></p>";
                message.Body   += $"<hr></p></br><span style = \"font-family:arial; font-size:'10pt';\">Kind regards,</br>MSA National</span></br></p><span style = \"font-family:arial; font-size:'9pt';\"><i>This email was sent automatically and replies to this inbox are not monitored.</i></span>";

                message.IsBodyHtml = true;

                smtpClient.Send(message);
            }
            catch (Exception ex)
            {
                Handlers.ErrorHandler.LogError(ex);
                return false;
            }

            return true;
        }

        //public static void SendSMS(string body, EmailEntities.EmailPlaceHolderModel emailModel)
        //{
        //    var toAddress = GetToEmailAddress(ref emailModel, isSMS:true);
        //    if (string.IsNullOrEmpty(toAddress)) return;

        //    SmtpClient smtpClient = GetSmtpClient();

        //    MailAddress fromMail = new MailAddress(GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_FallbackEmail));

        //    MailMessage message = new MailMessage();
        //    message.From = fromMail;

        //    var testingEmails = GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmails)?.ToUpper() == "TRUE";
            
        //    if (testingEmails)
        //    {
        //        var testAddress = GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmail);
        //        foreach (var address in toAddress.Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries))
        //        {
        //            message.To.Add(address);
        //        }
                
        //        emailSubject = $"TESTING SMS - Items - Would be Sent to {toAddress}";
        //    }
        //    else
        //    {
        //        foreach (var address in toAddress.Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries))
        //        {
        //            message.To.Add(address);
        //        }
        //        emailSubject = "SMS from MSA National";
        //    }

        //    emailBody = body;
        //    model = emailModel;
        //    model.EmailRecipient = toAddress;
        //    ReplaceEmailPlaceHolders();
        //    message.Body = emailBody;
        //    message.Subject = emailSubject;
        //    message.IsBodyHtml = false;

        //    smtpClient.Send(message);
        //}

        private static SmtpClient GetSmtpClient()
        {
            SmtpClient smtpClient = new SmtpClient(GlobalVars.GetGlobalTxtVar("MailSMTPServer"));
            if (GlobalVars.GetGlobalTxtVar("MailSMTPport") != null)
                smtpClient.Port = Convert.ToInt32(GlobalVars.GetGlobalTxtVar("MailSMTPport"));

            if (GlobalVars.GetGlobalTxtVar("MailSMTPServer") == null)
                smtpClient.UseDefaultCredentials = true;
            else
                smtpClient.Credentials = new System.Net.NetworkCredential(GlobalVars.GetGlobalTxtVar("MailCredUser"), GlobalVars.GetGlobalTxtVar("MailCredPass"));

            smtpClient.EnableSsl = true;

            return smtpClient;
        }


        public static string BuildHtmlForEmail(EmailEntities.EmailModel model, bool surplusAccepted = false)
        {
            if (model.IsSelfActing)
            {
                return BuildSelfActingEmail(model, surplusAccepted);
            }
            else
            {
                return BuildNonSelfActingEmail(model, surplusAccepted);
            }
        }

        public static string BuildDischargePayoutSection(List<EmailEntities.EmailLedgerItem> ledgerItems )
        {
            var sb = new StringBuilder();
            int liIndex = 0;

            if (ledgerItems.Any())
            {
                sb.Append($"<table>");
            }
            foreach (var li in ledgerItems)
            {
                liIndex++;
                sb.Append($"<tr><td width=0;><td/><td style='font-size: 11pt; font-family: Calibri;' width='30';>{liIndex}.</td> <td style='font-size: 11pt; font-family: Calibri;' width='300';>{li.PayableTo}</td><td style='font-size: 11pt; font-family: Calibri;'text-align:right;>{li.AmountString}</td></tr>");
            }
            if (ledgerItems.Any())
            {
                sb.Append($"</table>");
            }

            return sb.ToString();
        }

        private static string BuildNonSelfActingEmail(EmailEntities.EmailModel model, bool surplusAccepted = false)
        {
            var sb = new StringBuilder();
            //sb.Append("<style type =\'text/css\'>*{font-family: Calibri; font-size: 16px;} </style>"); didn't work
            sb.Append($"<p><span style='font-size: 14pt; font-family: Calibri;'>Securities for Matter {model.SecuritiesAppended}</span></p>");
            sb.Append($"<p><span style='font-size: 11pt; font-family: Calibri;'>We act for the Mortgagee in this matter, and confirm the following settlement arrangements:</span></p>");
            sb.Append($"<p><table>");
            sb.Append($"<tr><td width=0;><td/><td style='font-size: 11pt; font-family: Calibri;' width='80';>Date:</td><td style='font-size: 11pt; font-family: Calibri;'>{model.SettlementDate}</td></tr>");
            sb.Append($"<tr><td width=0;><td/><td style='font-size: 11pt; font-family: Calibri;' width='80';>Time:</td><td style='font-size: 11pt; font-family: Calibri;'>{model.SettlementTime}</td></tr>");
            sb.Append($"<tr><td width=0;><td/><td style='font-size: 11pt; font-family: Calibri;' width='80';>Venue:</td><td style='font-size: 11pt; font-family: Calibri;'>{model.SettlementVenue}</td></tr>");
            sb.Append("</table></p>");
            sb.Append($"<p><span style='font-size: 11pt; font-family: Calibri;'>At Settlement, we will require the following <b> bank cheques:</b></span></p>");

            sb.Append($"<p><table>");
            int liIndex = 0;
            foreach (var li in model.LedgerItems)
            {
                liIndex++;
                sb.Append($"<tr><td width=0;><td/><td style='font-size: 11pt; font-family: Calibri;' width='30';>{liIndex}.</td> <td style='font-size: 11pt; font-family: Calibri;' width='300';>{li.PayableTo}</td><td style='font-size: 11pt; font-family: Calibri;'text-align:right;>{li.AmountString}</td></tr>");
            }
            sb.Append("</table></p>");
            sb.Append($"<p><span style='font-size: 11pt; font-family: Calibri;'>If settlement is not completed in time for us to bank funds on the day of settlement, additional interest will be payable at settlement.</span></p>");

            if (surplusAccepted)
            {
                sb.Append($"<p><span style='font-size: 11pt; font-family: Calibri;'><b><u>PLEASE NOTE WE CAN ACCEPT SURPLUS FUNDS</u></b></span></p>");
            }
            else
            {
                sb.Append($"<p><span style='font-size: 11pt; font-family: Calibri;'><b><u>PLEASE NOTE WE CANNOT ACCEPT SURPLUS FUNDS</u></b></span></p>");
            }
            sb.Append($"<p><span style='font-size: 11pt; font-family: Calibri;'>Upon receipt of the funds listed above, we will release the following documents:</span></p>");
            foreach (var sec in model.SecurityList)
            {
                sb.Append($"<table>");
                sb.Append($"<tr><td width=0;><td/><td style='font-size: 11pt; font-family: Calibri;' width='100';><b>Property:<b/></td> <td style='font-size: 11pt; font-family: Calibri;'><b>{sec.SecurityAddress}</b></td></tr>");

                liIndex = 0;
                foreach (var doc in model.TitleReferences.Where(x => x.SecurityId == sec.SecurityId))
                {
                    liIndex++;
                    
                    sb.Append($"<tr><td width=0;><td/><td style='font-size: 11pt; font-family: Calibri;' width='100';>{liIndex}.</td> <td style='font-size: 11pt; font-family: Calibri;'>{doc.Title}</td></tr>");
                    
                }

                liIndex = 0;
                foreach (var doc in model.SecurityDocs.Where(x => x.SecurityId == sec.SecurityId))
                {
                    
                        liIndex++;

                        sb.Append($"<tr><td width=0;><td/><td style='font-size: 11pt; font-family: Calibri;' width='100';>{liIndex}.</td> <td style='font-size: 11pt; font-family: Calibri;'>{doc.DocumentName}</td></tr>");
                  
                }

                sb.Append("</table><br/>");
            }
            sb.Append($"<p><span style='font-size: 11pt; font-family: Calibri;'>If you request us to cancel or reschedule settlement, cheque details may change.</span></p>");

            return sb.ToString();
        }

        private static string BuildSelfActingEmail(EmailEntities.EmailModel model, bool surplusAccepted = false)
        {
            var sb = new StringBuilder();
            foreach(var item in model.LedgerItems)
            {
                if (item.Amount == null && !String.IsNullOrEmpty(item.AmountString))
                {
                    item.Amount = decimal.Parse(item.AmountString, System.Globalization.NumberStyles.Any);
                }
            }

            //we don't want to show negative amounts (i.e. surplus) here
            var negativeAmounts = model.LedgerItems.Where(x => x.Amount < 0).ToList();
            model.LedgerItems = model.LedgerItems.Where(x => x.Amount >= 0).ToList();
            var newTotal = model.LedgerItems.Sum(x => x.Amount);
            if (newTotal.HasValue)
            {
                model.LedgerItemsTotal = newTotal.Value.ToString("c");
            }

            //sb.Append("<style type =\'text/css\'>*{font-family: Calibri; font-size: 16px;} </style>");
            sb.Append($"<p><span style='font-size: 14pt; font-family: Calibri;'>Securities for Matter {model.SecuritiesAppended}</span></p>");
            sb.Append($"<p><span style='font-size: 11pt; font-family: Calibri;'>We act for the Mortgagee in this matter, and confirm the following settlement arrangements:</span></p>");
            sb.Append($"<p><table><tr>");
            sb.Append($"<td width=0;><td/><td style='font-size: 11pt; font-family: Calibri;' width='100';>Date:</td><td style='font-size: 11pt; font-family: Calibri;'>{model.SettlementDate}</td>");
            sb.Append("</tr></table></p>");
            sb.Append($"<p><span style='font-size: 11pt; font-family: Calibri;'>At Settlement, we will require the following:</b></span></p>");

            sb.Append($"<p><table>");

            int liIndex = 0;

            foreach (var li in model.LedgerItems)
            {
                liIndex++;
                sb.Append($"<tr><td width=0;><td/><td style='font-size: 11pt; font-family: Calibri;' width='30';>{liIndex}.</td> <td style='font-size: 11pt; font-family: Calibri;' width='300';>{li.PayableTo}</td><td style='font-size: 11pt; font-family: Calibri;'text-align:right;>{li.AmountString}</td></tr>");
            }

            sb.Append("</table></p>");
            sb.Append($"<p><span style='font-size: 11pt; font-family: Calibri;'>We confirm that you will transfer <b> clear funds </b>in the sum of <b>{model.LedgerItemsTotal}</b> into the following account <b><u>prior</b></u> to settlement:</span></p>");

            sb.Append($"<p><table><tr>");
            sb.Append($"<tr><td width=0;><td/><td style='font-size: 11pt; font-family: Calibri;' width='150';>BANK:</td><td style='font-size: 11pt; font-family: Calibri;'><b>{model.BankDetails.BankName}</b></td></tr>");
            sb.Append($"<tr><td width=0;><td/><td style='font-size: 11pt; font-family: Calibri;' width='150';>A/C NAME:</td><td style='font-size: 11pt; font-family: Calibri;'><b>{model.BankDetails.BankAcctName}</b></td></tr>");
            sb.Append($"<tr><td width=0;><td/><td style='font-size: 11pt; font-family: Calibri;' width='150';>BSB:</td><td style='font-size: 11pt; font-family: Calibri;'><b>{model.BankDetails.BankBSB}</b></td></tr>");
            sb.Append($"<tr><td width=0;><td/><td style='font-size: 11pt; font-family: Calibri;' width='150';>A/C NUMBER:</td><td style='font-size: 11pt; font-family: Calibri;'><b>{model.BankDetails.BankAcctNo}</b></td></tr>");
            sb.Append($"<tr><td width=0;><td/><td style='font-size: 11pt; font-family: Calibri;' width='150';>REFERENCE:</td><td style='font-size: 11pt; font-family: Calibri;'><b>{model.MatterId}</b></td></tr>");

            sb.Append("</table></p>");
            sb.Append("</br>");
            sb.Append($"<p><span style='font-size: 11pt; font-family: Calibri;'>If settlement is not completed in time for us to bank funds on the day of settlement, additional interest will be payable at settlement.</span></p>");

            if (surplusAccepted)
            {
                sb.Append($"<p><span style='font-size: 11pt; font-family: Calibri;'><b><u>PLEASE NOTE WE CAN ACCEPT SURPLUS FUNDS</u></b></span></p>");
            }
            else
            {
                sb.Append($"<p><span style='font-size: 11pt; font-family: Calibri;'><b><u>PLEASE NOTE WE CANNOT ACCEPT SURPLUS FUNDS</u></b></span></p>");
            }
            int matterState = 0;
            using (var context = new SlickContext())
            {
                matterState = (context.Matters.Where(m => m.MatterId == model.MatterId).FirstOrDefault().StateId);
            }

            if (matterState == (int)Enums.StateIdEnum.SA || matterState == (int)Enums.StateIdEnum.QLD)
            {
                sb.Append($"<p><span style='font-size: 11pt; font-family: Calibri;'>Upon receipt of the funds listed above, we will release the following documents via email:</span></p>");
            }
            else
            {
                sb.Append($"<p><span style='font-size: 11pt; font-family: Calibri;'>Upon receipt of the funds listed above, we will release the following documents:</span></p>");
            }
            foreach (var sec in model.SecurityList)
            {
                sb.Append($"<table>");
                sb.Append($"<tr><td width=0;><td/><td style='font-size: 11pt; font-family: Calibri;' width='100';><b>Property:<b/></td> <td style='font-size: 11pt; font-family: Calibri;'><b>{sec.SecurityAddress}</b></td></tr>");
                
                liIndex = 0;
                foreach (var doc in model.TitleReferences.Where(x => x.SecurityId == sec.SecurityId))
                {
                    liIndex++;
                    sb.Append($"<tr><td width=0;><td/><td style='font-size: 11pt; font-family: Calibri;' width='100';>{liIndex}.</td> <td style='font-size: 11pt; font-family: Calibri;'>{doc.Title}</td></tr>");
                }

                liIndex = 0;
                foreach (var doc in model.SecurityDocs.Where(x => x.SecurityId == sec.SecurityId))
                {
                    if (!doc.DocumentName.Contains("Discharge of Mortgage"))
                    {
                        liIndex++;
                        sb.Append($"<tr><td width=0;><td/><td style='font-size: 11pt; font-family: Calibri;' width='100';>{liIndex}.</td> <td style='font-size: 11pt; font-family: Calibri;'>{doc.DocumentName}</td></tr>");
                    }
                }

                sb.Append("</table><br/>");
            }

            //sb.Append($"<p><span style='font-size: 11pt; font-family: Calibri;'><b>If you would like the documents posted, kindly have your Identification verified at Australia Post and have same sent to the email noted above.  Alternatively, the documents will be made available for collection in our office.</b></span></p>");
          
                if (matterState == (int)Enums.StateIdEnum.NSW || matterState == (int)Enums.StateIdEnum.WA || matterState == (int)Enums.StateIdEnum.VIC)
                {
                    sb.Append($"<p><span style='font-size: 11pt; font-family: Calibri;'><b>If you would like the documents posted, kindly contact MSA National to obtain the Verification of Identity Form which you are required to complete and verify with Australia Post, and  to the email noted above. Alternatively, the documents will be made available for collection in our office.</b></span></p>");
                }
                
            

            sb.Append($"<p><span style='font-size: 11pt; font-family: Calibri;'>If you request us to cancel or reschedule settlement, cheque details may change.</span></p>");

            return sb.ToString();
        }

        public static void BuildReworkEmail(int matterId, bool docsRequired, MatterWFRework reworkDetails, ref string subject, ref string body, ref List<string> toAddress)
        {
            string reasonDetails = "";
            using (var context = new SlickContext())
            {
                if (reworkDetails.ReasonId.HasValue)
                {
                    reasonDetails = context.Reasons.FirstOrDefault(r => r.ReasonId == reworkDetails.ReasonId).ReasonTxt;
                }

                MatterWFRepository mwfRep = new MatterWFRepository(context);
                EmailsRepository emailRep = new EmailsRepository(context);

                model = mwfRep.BuildEmailPlaceHolderModel(emailRep, matterId, docsRequired? (int)WFComponentEnum.ClientRework : (int)WFComponentEnum.ClientReworkNoDocs, reworkDetails.MatterWFComponentId);

                var contactDetails = context.Matters.Where(m => m.MatterId == matterId).Select(x=>
                new
                {
                    x.LenderId,
                    LenderEmail = x.Lender.PrimaryContact.Email,
                    x.MortMgrId,
                    MortMgrEmail = x.MortMgrId.HasValue ? x.MortMgr.PrimaryContact.Email : "",
                    BrokerEmail = x.BrokerId.HasValue ? x.Broker.PrimaryContact.Email : ""
                }).FirstOrDefault();

                switch (contactDetails.LenderId)
                {
                    //Bluestone
                    case 43:
                        toAddress.Add(contactDetails.LenderEmail);
                        toAddress.Add(contactDetails.BrokerEmail);
                        break;
                    //MyState, LaTrobe, ABL
                    case 46:
                    case 166:
                    case 40:
                        toAddress.Add(contactDetails.BrokerEmail);
                        break;
                    //Advantedge
                    case 139:
                        if(contactDetails.MortMgrId == 49) // UBANK
                        {
                            toAddress.Add(contactDetails.MortMgrEmail);
                        }
                        else
                        {
                            toAddress.Add(contactDetails.BrokerEmail);
                        }
                        break;
                    //Fox Symes
                    case 173:
                        toAddress.Add(contactDetails.LenderEmail);
                        break;
                }

            }

            subject = "Rework Instructions Received {{" + DomainConstants.EmailMatterType + "}} / {{"+ DomainConstants.EmailParties + "}} / {{" + DomainConstants.EmailLenderRefNo + "}} / {{"+ DomainConstants.EmailMatterId + "}}";
            body    = "<p><span style = 'font-size: 14pt; font-family: Calibri; color: #0070C0;'><b>Securities for Matter</b> {{" + DomainConstants.EmailSecurities +"}}</span></p>"
                    + "<p><span style = 'font-size: 11pt; font-family: Calibri;'>Rework Instructions have been received by MSA National, and will be actioned shortly.</p>"
                    + "<p><span style = 'font-size: 11pt; font-family: Calibri; text-indent:2em;'><b>Reason: </b>" + reasonDetails + "</p>";

            if (!String.IsNullOrEmpty(reworkDetails.Notes))
                body += $"<p><span style = 'font-size: 11pt; font-family: Calibri; text-indent:2em;'><b>Details: </b>{reworkDetails.Notes}</p>";

            if (!docsRequired)
                body += $"<p><span style = 'font-size: 11pt; font-family: Calibri; text-indent:0em;'>Please note new documents are NOT required for this rework.</p>";

            body += $"<p><span style = 'font-size: 11pt; font-family: Calibri; text-indent:0em; '>Should you have an enquiry regarding preparation of documents please contact one of our friendly staff on <b>1300 MSA 007</b> (1300 672 007).</p>";
                      //+ "<p><span style = 'font-size: 11pt; font-family: Calibri; text-indent:0em;'>{{" + DomainConstants.EmailFileOwner + "}} is your personal file manager and can be contacted on the below details once documents are issued:</p>"
                      //+ "<p><span style = 'font-size: 11pt; font-family: Calibri; text-indent:2em;'><b>Contact Number: </b>{{" + DomainConstants.EmailFileOwnerPhone + "}}</p>"
                      //+ "<p><span style = 'font-size: 11pt; font-family: Calibri; text-indent:2em;'><b>Email: </b>{{" + DomainConstants.EmailFileOwnerEmail +"}}</p>";

            
            ReplaceEmailPlaceHolders(model, ref subject, ref body);



        }
        public static string BuildFundsAvailableHtmlForEmail(EmailEntities.EmailModel model)
        {
            var sb = new StringBuilder();
            //sb.Append("<style type =\'text/css\'>*{font-family: Calibri; font-size: 16px;} </style>");
            sb.Append($"<p><span style='font-size: 14pt; font-family: Calibri;'>Securities for Matter {model.SecuritiesAppended}</span></p>");
           

            sb.Append($"<p><span style='font-size: 16px; font-family: Calibri;'><b>Matter Funding Summary</b></span></p>");

            sb.Append($"<p><span style='font-size: 11pt; font-family: Calibri;'>Good {GetTimeOfDay(DateTime.Now)},</span></p>");

            sb.Append($"<p><span style='font-size: 11pt; font-family: Calibri;'>Please see below for a breakdown of funds.</span></p>");

            sb.Append($"<p><span style='font-size: 14pt; font-family: Calibri;'><b>Loan Amount = {(model.BookingDetails.LoanAmount.ToString("C"))}</b></span></p>");

            //sb.Append("<table width='650'>");
            //sb.Append($"<tr><td style='font-size: 11pt; font-family: Calibri;' width='450';><u>Total Amount</u></td>");
            //sb.Append($"<td style='font-size: 11pt; font-family: Calibri;'width='200'; align='right'><b>{(model.BookingDetails.LoanAmount.ToString("C"))}</b></td></tr>");
            //sb.Append("</table><br/>");

            //Lender Items
            if (model.BookingDetails.LenderRetainedItems.Any())
            {
                sb.Append("<table style='margin - left:10;' width='650'>");
                sb.Append("<tr><td width='30'>&nbsp;</td><td colspan=2 style='font-size: 12pt; font-family: Calibri;'><b>Less Amounts Retained by Lender</b></th></tr>");
                foreach (var lri in model.BookingDetails.LenderRetainedItems)
                {
                    sb.Append($"<tr><td>&nbsp;</td><td style='font-size: 12pt; font-family: Calibri;' width='370'>{lri.Description}</td>");
                    sb.Append($"<td style='font-size: 12pt; font-family: Calibri;'width='250'; align='right'>{(lri.Amount.Value.ToString("C"))}</td></tr>");
                }
                sb.Append($"<tr><td>&nbsp;</td><td style='font-size: 12pt; font-family: Calibri;' align='right'><b>Total = </b></td>");
                sb.Append($"<td style='font-size: 12pt; font-family: Calibri;' align='right'><b>{(model.BookingDetails.LenderRetainedItems.Sum(x => x.Amount.Value).ToString("C"))}</b></td></tr>");
                sb.Append("</table><br/>");
            }

            //Fund Items
            if (model.BookingDetails.ExpectedFundsItems.Any())
            {
                sb.Append("<table style='margin - left:10;' width='650'>");
                sb.Append("<tr><td width='30'>&nbsp;</td><td colspan=2 style='font-size: 11pt; font-family: Calibri;'><b>Funding Amounts</b></th></tr>");

                foreach (var lri in model.BookingDetails.ExpectedFundsItems)
                {
                    sb.Append($"<tr><td>&nbsp;</td><td style='font-size: 11pt; font-family: Calibri;'width='370';>{lri.Description}</td>");
                    sb.Append($"<td style='font-size: 11pt; font-family: Calibri;'width='250'; align='right'>{(lri.Amount.Value.ToString("C"))}</td></tr>");
                }
                sb.Append($"<tr><td>&nbsp;</td><td style='font-size: 11pt; font-family: Calibri;' align='right';><b>Funding Amounts Total =</b></td>");
                sb.Append($"<td style='font-size: 11pt; font-family: Calibri;' align='right'><b>{(model.BookingDetails.ExpectedFundsItems.Sum(x => x.Amount.Value).ToString("C"))}</b></td></tr>");
                sb.Append("</table><br/>");
            }

            //Fee Deductions
            sb.Append("<table style='margin - left:10;' width='650'>");
            sb.Append("<tr><td width='30'>&nbsp;</td><td colspan=4 style='font-size: 11pt; font-family: Calibri;'><b>Deductions Amounts</b></th></tr>");
            int liIndex = 0;
            foreach (var li in model.LedgerItems)
            {
                liIndex++;
                sb.Append($"<tr><td width='30'>&nbsp;</td><td width='25' style='font-size: 11pt; font-family: Calibri;'>{liIndex}.</td>");
                sb.Append($"<td width='250' style='font-size: 11pt; font-family: Calibri;'>{li.Description}</td>");
                sb.Append($"<td style='font-size: 11pt; font-family: Calibri;' width='200'>{li.PayableTo}</td>");
                sb.Append($"<td style='font-size: 11pt; font-family: Calibri;' align='right' width='175'>{li.AmountString}</td></tr>");
            }
            sb.Append($"<tr><td colspan='4' style='font-size: 11pt; font-family: Calibri;' align='right'><b>Deductions Total =</b></td>");
            sb.Append($"<td style='font-size: 11pt; font-family: Calibri;' align='right'><b>{model.LedgerItemsTotal}</b></td></tr>");
            sb.Append("</table><br/>");


            //Total
            var total = (model.BookingDetails.ExpectedFundsItems.Any() ? model.BookingDetails.ExpectedFundsItems.Sum(x => x.Amount.Value) : model.BookingDetails.LoanAmount)
                - model.LedgerItems.Sum(x => x.Amount.Value);

            sb.Append("<table width='650'>");
            sb.Append($"<tr><td style='font-size: 16pt; font-family: Calibri;' width='450'><b>Available Funds</b></td>");
            sb.Append($"<td style='font-size: 16pt; font-family: Calibri;' width='200' align='right'><b>{(total.ToString("C"))}</b></td></tr>");
            sb.Append("</table><br/>");

            return sb.ToString();
        }
        public static string EscapeHtml(string input)
        {
            return input?.Replace("<", "&lt;")?.Replace(">", "&gt;")?.Replace("&", "&amp;");
        }
        public static string BuildReadyToBookHtmlForEmail(EmailEntities.EmailModel model)
        {
            var sb = new StringBuilder();
            //sb.Append("<style type =\'text/css\'>*{font-family: Calibri; font-size: 16px;} </style>");
            sb.Append($"<p><span style='font-size: 14pt; font-family: Calibri;'>Securities for Matter {model.SecuritiesAppended}</span></p>");

            if (model.LenderId == 139 && (model.BookingDetails.OutstandingItems == null || !model.BookingDetails.OutstandingItems.Any(r=>!r.Resolved)))
            {

                sb.Append($"<p><span style='font-size: 16px; font-family: Calibri;'><b>Settlement Booking</b></span></p>");
                if (model.LenderId == 139 && model.SecurityList.Any(x => x.StateId == (int)StateIdEnum.TAS))
                {
                    //sb.Append($"<p><span style='font-size: 11pt; font-family: Calibri;'>**NB** If settlement is due to take place in Hobart, please contact our Hobart sett</span></p>");
                }

                sb.Append($"<p><span style='font-size: 11pt; font-family: Calibri;'>We confirm that we are ready to settle. Any shortfalls are to be organised with your client directly. The lender does not permit us to debit your client's bank account.</span></p>");
                sb.Append($"<p><span style='font-size: 11pt; font-family: Calibri;'>Settlement Booking: please provide settlement details.</span><p>");
                sb.Append($"<p><span style='font-size: 11pt; font-family: Calibri;'>Settlement is to be booked </span>");
                sb.Append($"<span style='font-size: 11pt; font-family: Calibri;'><b><i>{(model.BookingDetails.SettlementClearanceDays.HasValue ? model.BookingDetails.SettlementClearanceDays.Value.ToString(): "3")} clear working days </b></i></span>");
                sb.Append($"<span style='font-size: 16px; font-family: Calibri; font-weight: Bold;'>prior to BUT NOT INCLUDING the day of settlement.</span></p>");
                sb.Append($"<p><span style='font-size: 11pt; font-family: Calibri;'>Daily settlement times are between 2.00 pm and 3.30 pm Monday to Friday (excluding holidays).</span></p>");

                if (model.LenderId != 41)
                {
                    sb.Append($"<p><span style='font-size: 11pt; font-family: Calibri;'>Cancellations will incur a fee of {(model.BookingDetails.SettlementCancellationFee.Value.ToString("c"))} (GST incl.)</span>");
                    sb.Append($"<span style='font-size: 11pt; font-family: Calibri;'> plus bank cheque fees (if applicable) – no cancellation fee applies where request to cancel or reschedule sent via return email prior to 12pm AEST on the day prior to settlement. Settlements outside of the CBD will incur additional bank fees, courier fees and settlement agency fees.</span>");
                    sb.Append($"</p>");
                }
            }
            else
            {
                sb.Append($"<p><span style='font-size: 11pt; font-family: Calibri;'>We acknowledge receipt of loan documents in relation to the above matter.</span></p>");

                if (model.BookingDetails.OutstandingItems != null && model.BookingDetails.OutstandingItems.Any())
                {
                    sb.Append($"<p><span style='font-size: 11pt; font-family: Calibri;'>The following items remain outstanding.</span><br/><br/>");
                    foreach (var item in model.BookingDetails.OutstandingItems)
                    {
                        sb.Append($"<span style='font-size: 11pt; font-family: Calibri;'>&nbsp;&nbsp;-&nbsp&nbsp;{EscapeHtml(item.OutstandingItemName)}");
                        if (!string.IsNullOrEmpty(item.IssueDetails))
                        {
                            sb.Append($"&nbsp;&nbsp;&ndash;&nbsp;&nbsp;{EscapeHtml(item.IssueDetails)}");
                        }
                        if (!string.IsNullOrEmpty(item.SecurityDetails))
                        {
                            sb.Append($"&nbsp;{EscapeHtml(item.SecurityDetails)}");
                        }
                        sb.Append("</span><br/>");
                    }
                    sb.Append("</p>");
                }





                sb.Append($"<p><span style='font-size: 16px; font-family: Calibri;'><b>Settlement Booking</b></span></p>");
                if (model.LenderId == 139 && model.SecurityList.Any(x => x.StateId == (int)StateIdEnum.TAS))
                {
                    //sb.Append($"<p><span style='font-size: 11pt; font-family: Calibri;'>**NB** If settlement is due to take place in Hobart, please contact our Hobart sett</span></p>");
                }

                sb.Append($"<p><span style='font-size: 11pt; font-family: Calibri;'>Upon receipt of the above documents and or requirements we will be in a position to settle.</span></p>");
                sb.Append($"<p><span style='font-size: 11pt; font-family: Calibri;'>Settlement is to be booked </span>");
                sb.Append($"<span style='font-size: 11pt; font-family: Calibri;'><b><i>{model.BookingDetails.SettlementClearanceDays} clear working days </b></i></span>");
                sb.Append($"<span style='font-size: 16px; font-family: Calibri; font-weight: Bold;'>prior to BUT NOT INCLUDING the day of settlement.</span></p>");
                sb.Append($"<p><span style='font-size: 11pt; font-family: Calibri;'>Daily settlement times are between 2.00 pm and 3.30 pm Monday to Friday (excluding holidays).</span></p>");
                sb.Append($"<p><span style='font-size: 11pt; font-family: Calibri;'>Cancellations will incur a fee of {(model.BookingDetails.SettlementCancellationFee.Value.ToString("c"))} (GST incl.)</span>");
                sb.Append($"<span style='font-size: 11pt; font-family: Calibri;'> plus bank cheque fees (if applicable) – settlement to be cancelled via return email prior to 12pm AEST on the day prior to settlement.<br/>If not settling in Pexa settlements outside of the CBD will incur additional bank fees, courier fees and settlement agency fees.</span>");
                sb.Append($"</p>");
            }

            sb.Append($"<p><span style='font-size: 14pt; font-family: Calibri;'><b>Loan Amount = {(model.BookingDetails.LoanAmount.ToString("C"))}</b></span></p>");

                //sb.Append("<table width='650'>");
                //sb.Append($"<tr><td style='font-size: 11pt; font-family: Calibri;' width='450';><u>Total Amount</u></td>");
                //sb.Append($"<td style='font-size: 11pt; font-family: Calibri;'width='200'; align='right'><b>{(model.BookingDetails.LoanAmount.ToString("C"))}</b></td></tr>");
                //sb.Append("</table><br/>");
           

            //Lender Items
            if (model.BookingDetails.LenderRetainedItems.Any())
                {
                    sb.Append("<table style='margin - left:10;' width='650'>");
                    sb.Append("<tr><td width='30'>&nbsp;</td><td colspan=2 style='font-size: 12pt; font-family: Calibri;'><b>Less Amounts Retained by Lender</b></th></tr>");
                    foreach (var lri in model.BookingDetails.LenderRetainedItems)
                    {
                        sb.Append($"<tr><td>&nbsp;</td><td style='font-size: 12pt; font-family: Calibri;' width='370'>{lri.Description}</td>");
                        sb.Append($"<td style='font-size: 12pt; font-family: Calibri;'width='250'; align='right'>{(lri.Amount.Value.ToString("C"))}</td></tr>");
                    }
                    sb.Append($"<tr><td>&nbsp;</td><td style='font-size: 12pt; font-family: Calibri;' align='right'><b>Total = </b></td>");
                    sb.Append($"<td style='font-size: 12pt; font-family: Calibri;' align='right'><b>{(model.BookingDetails.LenderRetainedItems.Sum(x => x.Amount.Value).ToString("C"))}</b></td></tr>");
                    sb.Append("</table><br/>");
                }
            //Fund Items
            if (model.BookingDetails.ExpectedFundsItems.Any())
            {
                sb.Append("<table style='margin - left:10;' width='650'>");
                sb.Append("<tr><td width='30'>&nbsp;</td><td colspan=2 style='font-size: 11pt; font-family: Calibri;'><b>Funding Amounts</b></th></tr>");

                foreach (var lri in model.BookingDetails.ExpectedFundsItems)
                {
                    sb.Append($"<tr><td>&nbsp;</td><td style='font-size: 11pt; font-family: Calibri;'width='370';>{lri.Description}</td>");
                    sb.Append($"<td style='font-size: 11pt; font-family: Calibri;'width='250'; align='right'>{(lri.Amount.Value.ToString("C"))}</td></tr>");
                }
                sb.Append($"<tr><td>&nbsp;</td><td style='font-size: 11pt; font-family: Calibri;' align='right';><b>Funding Amounts Total =</b></td>");
                sb.Append($"<td style='font-size: 11pt; font-family: Calibri;' align='right'><b>{(model.BookingDetails.ExpectedFundsItems.Sum(x => x.Amount.Value).ToString("C"))}</b></td></tr>");
                sb.Append("</table><br/>");
            }

            //Fee Deductions
            sb.Append("<table style='margin - left:10;' width='650'>");
            sb.Append("<tr><td width='30'>&nbsp;</td><td colspan=4 style='font-size: 11pt; font-family: Calibri;'><b>Deductions Amounts</b></th></tr>");
            int liIndex = 0;
            foreach (var li in model.LedgerItems)
            {
                liIndex++;
                sb.Append($"<tr><td width='30'>&nbsp;</td><td width='25' style='font-size: 11pt; font-family: Calibri;'>{liIndex}.</td>");
                sb.Append($"<td width='250' style='font-size: 11pt; font-family: Calibri;'>{li.Description}</td>");
                sb.Append($"<td style='font-size: 11pt; font-family: Calibri;' width='200'>{li.PayableTo}</td>");
                sb.Append($"<td style='font-size: 11pt; font-family: Calibri;' align='right' width='175'>{li.AmountString}</td></tr>");
            }
            sb.Append($"<tr><td colspan='4' style='font-size: 11pt; font-family: Calibri;' align='right'><b>Deductions Total =</b></td>");
            sb.Append($"<td style='font-size: 11pt; font-family: Calibri;' align='right'><b>{model.LedgerItemsTotal}</b></td></tr>");
            sb.Append("</table><br/>");


            //Total
            var total = (model.BookingDetails.ExpectedFundsItems.Any() ? model.BookingDetails.ExpectedFundsItems.Sum(x => x.Amount.Value) : model.BookingDetails.LoanAmount)
                -       model.LedgerItems.Sum(x => x.Amount.Value);

            sb.Append("<table width='650'>");
            sb.Append($"<tr><td style='font-size: 16pt; font-family: Calibri;' width='450'><b>Available Funds</b></td>");
            sb.Append($"<td style='font-size: 16pt; font-family: Calibri;' width='200' align='right'><b>{(total.ToString("C"))}</b></td></tr>");
            sb.Append("</table><br/>");


            if (model.IsPurchase && model.BookingDetails.IsPaper)
            {
                sb.Append($"<p><span style='font-size: 20px; font-family: Calibri;'><b>Note</b></span></p>");
                if (model.BookingDetails.NumberOfFreeCheques.HasValue)
                {
                    sb.Append($"<p><span style='font-size: 11pt; font-family: Calibri;'>The first {model.BookingDetails.NumberOfFreeCheques} bank cheques directed by you are free.");
                    if (model.BookingDetails.BankChequeFee.HasValue)
                    {
                        sb.Append($" Each subsequent cheque is {model.BookingDetails.BankChequeFee.Value.ToString("c")}.</span></p>");
                    }
                }
                sb.Append($"<p><span style='font-size: 11pt; font-family: Calibri;'>The Direction to Pay must be in writing sent to us by fax or email {(model.LenderId == 139 ? "<b>12.00 pm one (1) day" : "<b>4.00 pm two (2) days")} prior to settlement.</b></span>");
                if (model.LenderId != 139 || model.BookingDetails.OutstandingItems?.Any() == true)
                {
                    sb.Append($"<p><span style='font-size: 11pt; font-family: Calibri;'>Any shortfalls are to be organised with your client directly.  Authority to debit your client's account is not available.</span></p>");
                }
            }

            return sb.ToString();
        }


        public static string BuildNominatedDatePlaceholder(UnitOfWork uow, EmailEntities.EmailModel model)
        {

            string placeholder = "";
            DateTime? nominatedPexaDate = uow.Context.MatterPexaDetails.Where(d => d.MatterId == model.MatterId).Select(d => d.NominatedSettlementDate).FirstOrDefault();
            DateTime? bookedDateTime = uow.Context.Matters.Where(m => m.MatterId == model.MatterId)?.FirstOrDefault().SettlementSchedule?.SettlementDate;

            
            if (bookedDateTime.HasValue && bookedDateTime != new DateTime())
            {
                TimeSpan bookedTime = uow.Context.Matters.Where(m => m.MatterId == model.MatterId).FirstOrDefault().SettlementSchedule.SettlementTime;
                bookedDateTime = bookedDateTime.Value.Add(bookedTime);
                placeholder +=  $"<p style='font-family: Calibri; font-size: 11pt; color: #0070C0;'>Settlement has been booked for {bookedDateTime.Value.ToString("h:mm tt")}, <b>{bookedDateTime.Value.ToString("dd/MM/yyyy")}</b>";
            }
            else if (nominatedPexaDate.HasValue && nominatedPexaDate != new DateTime())
            {
                string nomDateStr = nominatedPexaDate.HasValue ? "<b>PEXA Proposed Settlement Date: <u>" + nominatedPexaDate.Value.ToString("dddd, dd MMMM yyyy") + ".</b></u> This date is subject to change by any of the parties, even on the proposed day of settlement." : "";

                placeholder += $"<span style=\"font-size: 16px; font-family: Calibri; color: #0070C0;\">{nomDateStr}<p></span>";
            }

            return placeholder;
        }



        public static string BuildOutstandingsEmail(UnitOfWork uow, EmailEntities.EmailModel model,bool splitResponsibilities=false, Slick_Domain.Enums.OutstandingResponsiblePartyTypeEnum? outstandingResponsiblePartyTypeEnum=null, bool includeSecondaryPayouts = false, bool includeGrid = false, List<OutstandingItemView> resolvedItems = null)
        {
            var sb = new StringBuilder();


            //sb.Append("<style type =\'text/css\'>*{font-family: Calibri; font-size: 16px;} </style>");
            sb.Append($"<p><span style=\"font-size: 18px; font-family: Calibri; color: #0070C0;\">Securities for Matter {model.SecuritiesAppended}</span></p>");

            sb.Append(BuildNominatedDatePlaceholder(uow, model));



            sb.Append($"<p><span style=\"font-size: 16px; font-family: Calibri;\">Good {GetTimeOfDay(DateTime.Now)},</span></p>");

            if(resolvedItems != null && outstandingResponsiblePartyTypeEnum != null)
            {
                resolvedItems = resolvedItems.Where(r => r.ResponsiblePartyId == (int)outstandingResponsiblePartyTypeEnum.Value || !r.ResponsiblePartyId.HasValue).ToList();
            }
            if (resolvedItems?.Any() == true)
            {
                string label = resolvedItems.Count > 1 ? "items" : "item";
           
                sb.Append($"<p><span style=\"font-size: 16px; font-family: Calibri;\"><u>We acknowledge receipt of the following {label} in relation to the above matter.</u></span><br/><br/>");
                int itemIndex = 1;
                foreach (var item in resolvedItems)
                {
                    sb.Append($"<span style=\"font-size: 16px; text-indent: 2em; font-family: Calibri;\";>&emsp;{(resolvedItems.Count() > 1 ? itemIndex.ToString() + "." : " - ")}&emsp;{EscapeHtml(item.OutstandingItemName)}");
                    if (!string.IsNullOrEmpty(item.IssueDetails))
                    {
                        sb.Append($"&nbsp;&nbsp;&ndash;&nbsp;&nbsp;{EscapeHtml(item.IssueDetails)}");
                    }
                    if (!string.IsNullOrEmpty(item.SecurityDetails))
                    {
                        sb.Append($"&nbsp;{item.SecurityDetails}");
                    }
                    sb.Append("</span><br/>");
                    itemIndex++;
                }
                sb.Append("</p>");
            }
            else
            {
                sb.Append($"<p><span style=\"font-size: 16px; font-family: Calibri;\">We acknowledge receipt of loan documents in relation to the above matter.</span></p>");

            }

            if (model.BookingDetails.OutstandingItems != null && model.BookingDetails.OutstandingItems.Any())
            {
                if (splitResponsibilities)
                {
                    foreach (var party in model.BookingDetails.OutstandingItems.Where(i => i.Resolved == false).Select(x => x.ResponsiblePartyId).Distinct()) //find which different party types we have selected - only email about unresolved items
                    {
                        var itemsForEmail = model.BookingDetails.OutstandingItems.Where(x => x.ResponsiblePartyId == party && x.Resolved == false);
                        switch (party)
                        {
                            case (int)OutstandingResponsiblePartyTypeEnum.Borrower:
                                sb.Append(BuildOutstandingItemsPerParty(itemsForEmail, "Borrower").ToString());
                                break;
                            case (int)OutstandingResponsiblePartyTypeEnum.Broker:
                                sb.Append(BuildOutstandingItemsPerParty(itemsForEmail, model.LenderId == 1 ? "RAMS Home Loan Manager" : "Broker").ToString());
                                break;
                            case (int)OutstandingResponsiblePartyTypeEnum.Lender:
                                sb.Append(BuildOutstandingItemsPerParty(itemsForEmail, "Lender").ToString());
                                break;
                            case (int)OutstandingResponsiblePartyTypeEnum.MortMgr:
                                if (!String.IsNullOrEmpty(model.MortMgrName))
                                {
                                    sb.Append(BuildOutstandingItemsPerParty(itemsForEmail, model.MortMgrName).ToString());
                                }
                                else
                                {
                                    sb.Append(BuildOutstandingItemsPerParty(itemsForEmail, "Mortgage Manager").ToString());
                                }
                                break;
                            case (int)OutstandingResponsiblePartyTypeEnum.MSA:
                                sb.Append(BuildOutstandingItemsPerParty(itemsForEmail, "MSA National").ToString());
                                break;
                            case (int)OutstandingResponsiblePartyTypeEnum.Solicitor:
                                sb.Append(BuildOutstandingItemsPerParty(itemsForEmail, "Solicitor / Conveyancer").ToString());
                                break;
                            default:
                                sb.Append(BuildOutstandingItemsPerParty(itemsForEmail, "").ToString());
                                break;
                        }
                    }
                }
                else
                {
                    if (outstandingResponsiblePartyTypeEnum != null)
                    {
                        sb.Append($"<p><span style=\"font-size: 16px; font-family: Calibri;\"><u>The following items remain outstanding.</u></span><br/><br/>");
                        int itemIndex = 1;

                        foreach (var item in model.BookingDetails.OutstandingItems.Where(i => i.ResponsiblePartyId == (int)outstandingResponsiblePartyTypeEnum))
                        {
                            sb.Append($"<span style=\"font-size: 16px; text-indent: 2em; font-family: Calibri;\";>&emsp;{itemIndex}.&emsp;{EscapeHtml(item.OutstandingItemName)}");
                            if (!string.IsNullOrEmpty(item.IssueDetails))
                            {
                                sb.Append($"&nbsp;&nbsp;&ndash;&nbsp;&nbsp;{EscapeHtml(item.IssueDetails)}");
                            }
                            if (!string.IsNullOrEmpty(item.SecurityDetails))
                            {
                                sb.Append($"&nbsp;{item.SecurityDetails}");
                            }
                            sb.Append("</span><br/>");
                            itemIndex++;
                        }
                        sb.Append("</p>");
                    }
                    else
                    {
                        sb.Append($"<p><span style=\"font-size: 16px; font-family: Calibri;\"><u>The following items remain outstanding.</u></span><br/><br/>");
                        int itemIndex = 1;
                        foreach (var item in model.BookingDetails.OutstandingItems)
                        {
                            sb.Append($"<span style=\"font-size: 16px; text-indent: 2em; font-family: Calibri;\";>&emsp;{itemIndex}.&emsp;{EscapeHtml(item.OutstandingItemName)}");
                            if (!string.IsNullOrEmpty(item.IssueDetails))
                            {
                                sb.Append($"&nbsp;&nbsp;&ndash;&nbsp;&nbsp;{EscapeHtml(item.IssueDetails)}");
                            }
                            if (!string.IsNullOrEmpty(item.SecurityDetails))
                            {
                                sb.Append($"&nbsp;{item.SecurityDetails}");
                            }
                            sb.Append("</span><br/>");
                            itemIndex++;
                        }
                        sb.Append("</p>");
                    }

                }


            }

            if (model.BookingDetails.OutstandingItems.Count() > 0)
            {
                string timeRequired = $"12pm, {model.BookingDetails.SettlementClearanceDays ?? 3} days";
                if (model.LenderName.ToUpper().Contains("THINK TANK"))
                {
                    timeRequired = "2pm, 2 days";
                }
                //sb.Append($"<p><span style=\"font-size: 16px; font-family: Calibri;\">The above items must be provided prior to booking in settlement. <b>Please note bookings must be made before <u>{timeRequired} prior to the intended settlement date.</u></b></span></p>");
            }

            if (includeSecondaryPayouts)
            {
                sb.Append($"<p style='font-size:16px; font-family: Calibri;'><u>IMPORTANT - Payouts for Secondary Loans required</u></p>");
                sb.Append(BuildSecondaryLoansEmailBody(model.MatterId, true, uow));
            }

            if (!string.IsNullOrEmpty(model.FastRefiDetails))
            {
                sb.Append($"<span style='font-size:16px; font-family: Calibri;'>" + model.FastRefiDetails + "</span>");
            }


            sb.Append(BuldNextStepString(model.BookingDetails.SettlementClearanceDays ?? 3));


            if (includeGrid)
            {
                sb.Append($"<p><span style=\"font-size: 16px; font-family: Calibri;\">Current{(model.SettlementDate == null? " indicative"  : "" )} disbursements for this matter are as follows:</span></p>");
                sb.Append(uow.GetEmailsRepositoryInstance().BuildFinancialGridPlaceholder(model.MatterId));
            }

            //sb.Append($"<p><span style=\"font-size: 16px; font-family: Calibri;\"></span></p>");

            //sb.Append($"<p><span style=\"font-size: 16px; font-family: Calibri;\">Should you have any questions, please do not hesitate to contact me.</span></p>");
            sb.Append(BuildContactUsString(model));
            return sb.ToString();
        }


        public static string BuldNextStepString(int settlementClearanceDays)
        {
            string nextSteps = "";

            nextSteps += $"<p>&emsp;</p><p><span style=\"font-size: 16px; font-family: Calibri;\"><b>NEXT STEPS:</b></span></p>";

            nextSteps += $"<p><span style=\"font-size: 16px; font-family: Calibri;\"><u>Outstanding Items / QA</u></span></p>";

            nextSteps += $"<p><span style=\"font-size: 16px; font-family: Calibri;\">In addition to the items listed above, please provide MSA any additional information relevant to the booking, including <u>specific settlement date required</u>, or any linked settlements.  </span></p>";
            nextSteps += $"<p><span style=\"font-size: 16px; font-family: Calibri;\">Once all outstanding requirements have been satisfied by all parties, the file will be submitted to our Quality Assurance (QA) team for final review.</span></p>";
            nextSteps += $"<p><span style=\"font-size: 16px; font-family: Calibri;\">On occasion the QA process may identify additional items required prior to sign off. Once the QA process is complete the file status will move to ready to book.</span></p>";

            nextSteps += $"<p><span style=\"font-size: 16px; font-family: Calibri;\"><u>Ready to Book</u></span></p>";

            nextSteps += $"<p><span style=\"font-size: 16px; font-family: Calibri;\">Once the file is ready to book we require {settlementClearanceDays.ToString()} business days’ notice for a settlement booking.</span></p>";


            return nextSteps;
        }

        public static string BuildContactUsString(EmailEntities.EmailModel model)
        {
            string contactUs = "";

            contactUs += $"<p><span style=\"font-size: 16px; font-family: Calibri;\">Should you have any questions, please do not hesitate to contact me.</span></p>";

            //contactUs += $"<p><span style=\"font-size: 16px; font-family: Calibri;\"><b>Enquiries:</b></span></p>";
            //contactUs += $"<p><span style=\"font-size: 16px; font-family: Calibri;\">Name : {model.FileOwnerName}</span></p>";
            //contactUs += $"<p><span style=\"font-size: 16px; font-family: Calibri;\">Contact Number : {model.FileOwnerNumber}</span></p>";
            //contactUs += $"<p><span style=\"font-size: 16px; font-family: Calibri;\">Email : {model.FileOwnerEmail}</span></p>";
            return contactUs;

        }

        //similar email but used by the drag drop document functionality
        public static string BuildOutstandingsEmailAdhoc(EmailEntities.EmailModel model, List<string> resolvedItems, bool splitResponsibilities = false)
        {
            var sb = new StringBuilder();

            //sb.Append("<style type =\'text/css\'>*{font-family: Calibri; font-size: 16px;} </style>");
            sb.Append($"<p><span style=\"font-size: 18px; font-family: Calibri; color: #0070C0;\">Securities for Matter {model.SecuritiesAppended}</span></p>");

            using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadUncommitted))
            {

                sb.Append(BuildNominatedDatePlaceholder(uow, model));
            }


            sb.Append($"<p><span style=\"font-size: 16px; font-family: Calibri;\">Good {GetTimeOfDay(DateTime.Now)},</span></p>");

            if (resolvedItems.Any())
            {
                if(resolvedItems.Count() > 1)
                sb.Append($"<p><span style=\"font-size: 16px; font-family: Calibri;\"><b><u>We acknowledge receipt of the following items:</b></u></span></p>");
                else sb.Append($"<p><span style=\"font-size: 16px; font-family: Calibri;\"><b><u>We acknowledge receipt of the following item:</b></u></span></p>");

                int itemIndex = 1;
                foreach (var item in resolvedItems)
                {
                    sb.Append($"<span style=\"font-size: 16px; text-indent: 2em; font-family: Calibri;\">&emsp;{itemIndex}.&emsp;{item}");
                    sb.Append("</span><br/>");
                    itemIndex++;
                }
                
            }
            if (model.BookingDetails.OutstandingItems != null && model.BookingDetails.OutstandingItems.Any())
            {
                if (splitResponsibilities)
                {
                    foreach (var party in model.BookingDetails.OutstandingItems.Where(i => i.Resolved == false).Select(x => x.ResponsiblePartyId).Distinct()) //find which different party types we have selected - only email about unresolved items
                    {
                        var itemsForEmail = model.BookingDetails.OutstandingItems.Where(x => x.ResponsiblePartyId == party && x.Resolved == false);
                        switch (party)
                        {
                            case (int)OutstandingResponsiblePartyTypeEnum.Borrower:
                                sb.Append(BuildOutstandingItemsPerParty(itemsForEmail, "Borrower").ToString());
                                break;
                            case (int)OutstandingResponsiblePartyTypeEnum.Broker:
                                sb.Append(BuildOutstandingItemsPerParty(itemsForEmail, model.LenderId == 1 ?"RAMS Home Loan Manager" : "Broker").ToString());
                                break;
                            case (int)OutstandingResponsiblePartyTypeEnum.Lender:
                                sb.Append(BuildOutstandingItemsPerParty(itemsForEmail, "Lender").ToString());
                                break;
                            case (int)OutstandingResponsiblePartyTypeEnum.MortMgr:
                                if (!String.IsNullOrEmpty(model.MortMgrName))
                                {
                                    sb.Append(BuildOutstandingItemsPerParty(itemsForEmail, model.MortMgrName).ToString());
                                }
                                else
                                {
                                    sb.Append(BuildOutstandingItemsPerParty(itemsForEmail, "Mortgage Manager").ToString());
                                }
                                break;
                            case (int)OutstandingResponsiblePartyTypeEnum.MSA:
                                sb.Append(BuildOutstandingItemsPerParty(itemsForEmail, "MSA National").ToString());
                                break;
                            case (int)OutstandingResponsiblePartyTypeEnum.Solicitor:
                                sb.Append(BuildOutstandingItemsPerParty(itemsForEmail, "Solicitor / Conveyancer").ToString());
                                break;
                            default:
                                sb.Append(BuildOutstandingItemsPerParty(itemsForEmail, "").ToString());
                                break;
                        }
                    }
                }
                else
                {
                    if(model.BookingDetails.OutstandingItems.Count() > 1)
                    sb.Append($"<p><span style=\"font-size: 16px; font-family: Calibri;\"><b><u>The following items remain outstanding.</u></b></span><br/><br/>");
                    else sb.Append($"<p><span style=\"font-size: 16px; font-family: Calibri;\"><b><u>The following item remains outstanding.</u></b></span><br/><br/>");

                    int itemIndex = 1;
                    foreach (var item in model.BookingDetails.OutstandingItems)
                    {
                        sb.Append($"<span style=\"font-size: 16px; text-indent: 2em; font-family: Calibri;\";>&emsp;{itemIndex}.&emsp;{item.OutstandingItemName}");
                        if (!string.IsNullOrEmpty(item.IssueDetails))
                        {
                            
                            sb.Append($"&nbsp;&nbsp;&ndash;&nbsp;&nbsp;{item.IssueDetails}");
                        }
                        if (!string.IsNullOrEmpty(item.SecurityDetails))
                        {
                            sb.Append($"&nbsp;{item.SecurityDetails}");
                        }
                        switch (item.ResponsiblePartyId)
                        {
                            case (int)OutstandingResponsiblePartyTypeEnum.Borrower:
                                sb.Append($"&nbsp;(Responsible - Borrower)");
                                break;
                            case (int)OutstandingResponsiblePartyTypeEnum.Broker:
                                sb.Append($"&nbsp;(Responsible - {(model.LenderId == 1 ? "RAMS Home Loan Manager" : "Broker")})");
                                break;
                            case (int)OutstandingResponsiblePartyTypeEnum.Lender:
                                sb.Append($"&nbsp;(Responsible - {model.LenderName})");
                                break;
                            case (int)OutstandingResponsiblePartyTypeEnum.MortMgr:
                                if (!String.IsNullOrEmpty(model.MortMgrName))
                                {
                                    sb.Append($"&nbsp;(Responsible - {model.MortMgrName})");
                                }
                                else
                                {
                                    sb.Append($"&nbsp;(Responsible - Mortgage Manager)");
                                }
                                break;
                            case (int)OutstandingResponsiblePartyTypeEnum.MSA:
                                sb.Append($"&nbsp;(Responsible - MSA National)");
                                break;
                            case (int)OutstandingResponsiblePartyTypeEnum.Solicitor:
                                sb.Append($"&nbsp;(Responsible - Solicitor / Conveyancer)");
                                break;
                            default:
                                break;
                        }
                        sb.Append("</span><br/>");
                        itemIndex++;
                    }
                    sb.Append("</p>");
                }
            }
            if (model.BookingDetails.OutstandingItems.Count() > 0)
            {
                string timeRequired = "12pm, 3 days";
                if (model.LenderName.ToUpper().Contains("THINK TANK"))
                {
                    timeRequired = "2pm, 2 days";
                }
                //sb.Append($"<p><span style=\"font-size: 16px; font-family: Calibri;\">The above items must be provided prior to booking in settlement. <b>Please note bookings must be made before <u>{timeRequired} prior to the intended settlement date.</u></b></span></p>");

            }

            sb.Append(BuldNextStepString(model.BookingDetails.SettlementClearanceDays ?? 3));


            //sb.Append($"<p><span style=\"font-size: 16px; font-family: Calibri;\"></span></p>");
            //sb.Append($"<p><span style=\"font-size: 16px; font-family: Calibri;\">Should you have any questions, please do not hesitate to contact me.</span></p>");

            sb.Append(BuildContactUsString(model));

            return sb.ToString();
        }

        public static StringBuilder BuildOutstandingItemsPerParty(IEnumerable<OutstandingItemView> items, string partyName)
        {
            StringBuilder partyItemStringBuilder = new StringBuilder();

            for (int i=0; i < items.Count(); ++i)
            {
                if (i == 0)
                {
                    partyItemStringBuilder.Append($"<p><span style=\"font-size: 16px; font-family: Calibri;\"><u>The following <strong>{partyName} </strong>items remain outstanding.</u></span><br/><br/>");
                }
                partyItemStringBuilder.Append($"<span style=\"font-size: 16px; text-indent: 2em; font-family: Calibri;\";>&emsp;{i+1}.&emsp;{EscapeHtml(items.ElementAt(i).OutstandingItemName)}");
                if (!string.IsNullOrEmpty(items.ElementAt(i).IssueDetails))
                {
                    partyItemStringBuilder.Append($"&nbsp;&nbsp;&ndash;&nbsp;&nbsp;{EscapeHtml(items.ElementAt(i).IssueDetails)}");
                }
                if (!string.IsNullOrEmpty(items.ElementAt(i).SecurityDetails))
                {
                    partyItemStringBuilder.Append($"&nbsp;{EscapeHtml(items.ElementAt(i).SecurityDetails)}");
                }
                partyItemStringBuilder.Append("</span><br/>");
            }
            partyItemStringBuilder.Append("</p>");

            return partyItemStringBuilder;
        }


        


        private static string GetToEmailAddress(ref EmailEntities.EmailPlaceHolderModel model, bool isSMS = false, bool appendSMS = true)
        {
            var toAddresses = new StringBuilder();
            
            var appendToAddress = isSMS ? GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_MSASMSAddress) : string.Empty;

            if (!appendSMS)
            {
                appendToAddress = string.Empty;
            }
            
            if ((!string.IsNullOrEmpty(model.EmailMobiles.Broker) || (!string.IsNullOrEmpty(model.EmailMobiles.BrokerMobile))))
            {
                    if (!isSMS && !String.IsNullOrEmpty(model.EmailMobiles.Broker))
                    {
                        toAddresses.AppendFormat("{0}{1}{2}", model.EmailMobiles.Broker, appendToAddress, ";");
                    }
                    else if(isSMS)
                    {
                        if (!string.IsNullOrEmpty(model.EmailMobiles.Broker) && model.EmailMobiles.Broker.Length > 2 && model.EmailMobiles.Broker.Substring(0, 2) == "04")
                        {
                            toAddresses.AppendFormat("{0}{1}{2}", model.EmailMobiles.Broker, appendToAddress, ";");
                        }
                        else if (!String.IsNullOrEmpty(model.EmailMobiles.BrokerMobile))
                        {
                            toAddresses.AppendFormat("{0}{1}", model.EmailMobiles.BrokerMobile, ";");
                        }
                    }
            }



            if(isSMS && !string.IsNullOrEmpty(model.EmailMobiles.BorrowerMobiles))
            {
                foreach(var addr in model.EmailMobiles.BorrowerMobiles.Split(';'))
                {
                    toAddresses.AppendFormat("{0}{1}", addr, ";");
                }
            }


            if (!string.IsNullOrEmpty(model.EmailMobiles.SecondaryContacts))
            {
                toAddresses.AppendFormat("{0}{1}{2}", model.EmailMobiles.SecondaryContacts, appendToAddress, ";");
            }

            if (!string.IsNullOrEmpty(model.EmailMobiles.RelationshipManager))
            {
                toAddresses.AppendFormat("{0}{1}{2}", model.EmailMobiles.RelationshipManager, appendToAddress, ";");
            }

            if (!string.IsNullOrEmpty(model.EmailMobiles.FileOwner))
            {
                toAddresses.AppendFormat("{0}{1}{2}", model.EmailMobiles.FileOwner, appendToAddress, ";");
            }
                
            if (!string.IsNullOrEmpty(model.MortMgrName) && !string.IsNullOrEmpty(model.EmailMobiles.MortMgr))
            {
                toAddresses.AppendFormat("{0}{1}{2}", model.EmailMobiles.MortMgr, appendToAddress, ";");
            }
                
            if (!string.IsNullOrEmpty(model.LenderName) && !string.IsNullOrEmpty(model.EmailMobiles.Lender))
            {
                if (!isSMS)
                {
                    toAddresses.AppendFormat("{0}{1}{2}", model.EmailMobiles.Lender, appendToAddress, ";");
                }
                else if(model.EmailMobiles.Lender.Length>2 && model.EmailMobiles.Lender.Substring(0,2) == "04")
                {
                    toAddresses.AppendFormat("{0}{1}{2}", model.EmailMobiles.Lender, appendToAddress, ";");
                }
            }

            if (!isSMS && !string.IsNullOrEmpty(model.BorrowerName) && !string.IsNullOrEmpty(model.EmailMobiles.Borrower))
            {
                toAddresses.AppendFormat("{0}{1}{2}",  model.EmailMobiles.Borrower, appendToAddress, ";");
            }

            if (!string.IsNullOrEmpty(model.EmailMobiles.Other))
            {

                toAddresses.AppendFormat("{0}{1}{2}", model.EmailMobiles.Other, appendToAddress, ";");
            }
            if (!string.IsNullOrEmpty(model.EmailMobiles.OtherParty))
            {
                toAddresses.AppendFormat("{0}{1}{2}", model.EmailMobiles.OtherParty, appendToAddress, ";");
            }

            if (toAddresses.Length == 0 && !isSMS)
            {
                if (!string.IsNullOrEmpty(model.EmailMobiles.CCEmails))
                {
                    toAddresses.AppendLine(model.EmailMobiles.CCEmails);
                    model.EmailMobiles.CCEmails = null;
                }
                else if (!string.IsNullOrEmpty(model.EmailMobiles.CurrentUser))
                {
                    toAddresses.AppendLine(model.EmailMobiles.CurrentUser);
                }
                else return null;
            }



            if (toAddresses.Length > 0)
            {
                var returnTo = toAddresses.ToString();
                if (returnTo[returnTo.Length - 1] == ';')
                {
                    returnTo = returnTo.Substring(0, returnTo.Length - 1);
                }
                return returnTo;
            }
            else return null;
        }

        public static void ReplaceEmailPlaceHolders(int? partyIndex = null)
        {
            isFinishedEmail = false;
            endlessLoopCheck = 0;
            HookupPlaceHoldersToValues(partyIndex);
            ReplaceEmailPlaceHolder(startAt:0, textToReplace: ref emailSubject);
            ReplaceEmailPlaceHolder(startAt: 0, textToReplace: ref emailBody);
        }

        public static void ReplaceEmailPlaceHolders(EmailEntities.EmailPlaceHolderModel emailModel, ref string manualEmailSubject, ref string manualEmailBody, int? partyIndex = null)
        {
            model = emailModel;
            isFinishedEmail = false;
            endlessLoopCheck = 0;
            HookupPlaceHoldersToValues(partyIndex);
            ReplaceEmailPlaceHolder(startAt: 0, textToReplace: ref manualEmailSubject);
            ReplaceEmailPlaceHolder(startAt: 0, textToReplace: ref manualEmailBody);
        }

        private static void ReplaceEmailPlaceHolder(int startAt, ref string textToReplace)
        {
            string placeHolder = string.Empty;
            isFinishedEmail = false;
            endlessLoopCheck = 0;
            while (!isFinishedEmail)
            {
                if (endlessLoopCheck > 500)
                {
                    throw new Exception($"Endless loop detected in ReplaceEmailPlaceHolder - last placeholder {placeHolder} last startIndex {startAt}");
                }
                    
                endlessLoopCheck++;
                int index = textToReplace.IndexOf("{{", startAt);
                if (index > -1)
                {
                    int endIndex = textToReplace.IndexOf("}}", index);
                    placeHolder = textToReplace.Substring(index + 2, endIndex - index-2);
                    ReplaceEmailPlaceHolder(placeHolder);

                    textToReplace = textToReplace.Replace(string.Format("{0}{1}{2}", "{{", placeHolder, "}}"), replaceValue);
                    startAt = endIndex - (placeHolder.Length+2) + replaceValue.Length; //Body length has changed so realign index.
                }
                else isFinishedEmail = true;
            }
        }

        public static string BuildLockedPdfEmail(string password, int matterId)
        {           
            var sb = new StringBuilder();
            sb.AppendLine($"{password}");

            return sb.ToString();
        }
        public static string BuildLockedPdfEmailOld(string password, int matterId)
        {
            string office = null;
            using (var uow = new UnitOfWork())
            {
                office = uow.Context.Matters.FirstOrDefault(x => x.MatterId == matterId)?.State?.Offices?.FirstOrDefault()?.Phone;
            }
            if (string.IsNullOrEmpty(office))
            {
                office = "MSA Office";
            }
            else
            {
                office = $"our office on {office}";
            }

            var sb = new StringBuilder();
            sb.Append($"<p><span style='font-size: 11pt; font-family: Calibri;'>Hi<br/><br/>");
            sb.Append($"We have recently sent you an email enclosing your loan document package in PDF format.<br/><br/>");
            sb.Append($"Your loan documents are encrypted and password protected for your security.<br/><br/>");
            sb.Append($"Please do not share your password with anyone. Please note that passwords are case sensitive.<br/><br/>");
            sb.Append($"</span></p>");

            sb.Append($"<p><span style='font-size: 11pt; font-family: Calibri;'><b>PASSWORD:[ </span>");
            sb.Append($"<span style='color:blue; font-size: 16px; font-family: Calibri;'<b>{password}</b></span>");
            sb.Append($"<span style='font-size: 16px; font-family: Calibri;'> <b> ]</b><br/><br/></span></p>");

            sb.Append($"<p><span style='font-size: 16px; font-family: Calibri; font-weight: normal'>If you experience any issues accessing your loan document package, please contact {office}.<br/><br/></span></p>");

            sb.Append($"<p><span style='font-size: 16px; font-family: Calibri;'><b>PLEASE NOTE:  Your password is for your personal use only. MSA National does not accept any responsibility for any sharing/misuse of your password.</b>");
            sb.Append($"</span></p>");

            return sb.ToString();
        }
        public static string BuildLockedDocsEmail(int matterId)
        {
            MatterCustomEntities.MatterFileOwnerView view = null;
            Office office = null;
            
            using (var uow = new UnitOfWork())
            {
                view = uow.GetMatterRepositoryInstance().GetFileOwnerDetails(matterId);
                office = uow.Context.Matters.FirstOrDefault(x => x.MatterId == matterId)?.State?.Offices?.FirstOrDefault();
            }

            string phoneText = office?.Phone ?? view?.Phone;
            string faxText = string.IsNullOrEmpty(office?.Fax) ? string.Empty : $" or Fax: {office?.Fax}";
            string emailText = string.IsNullOrEmpty(view?.Email) ? string.Empty : $" or {view?.Email}";

            var sb = new StringBuilder();
            sb.Append($"<p><span style='font-size: 11pt; font-family: Calibri;'>Good {EmailsService.GetTimeOfDay(DateTime.Now)},<br/><br/>");
            sb.Append($"We have been authorised to email loan and mortgage documents to you.<br/><br/>");
            sb.Append($"The documents are password protected, and the password will be sent to you separately.<br/><br/>");
            sb.Append($"Please ensure the documents are printed single sided on A4 white paper and the original executed documents are to be returned to our office.<br/><br/>");
            sb.Append($"</span></p>");

            sb.Append($"<p><span style='font-size: 16px; font-family: Calibri; font-weight: normal'>If you have any questions please contact {view?.FullName}"); 
            sb.Append($" on <b>{phoneText}</b> {faxText}{emailText}.<br/><br/></span></p>");

            return sb.ToString();
        }

        private static void ReplaceEmailPlaceHolder(string placeHolder)
        {
            if (emailPlaceHolderActions.ContainsKey(placeHolder))
            {
                var executeAction = emailPlaceHolderActions.First(epa => epa.Key == placeHolder).Value;
                executeAction();
                if (replaceValue == null) replaceValue = string.Empty;
            }
            else replaceValue = string.Empty;
        }

        private static string RetrieveMarketing()
        {
            if (File.Exists(GlobalVars.GetGlobalTxtVar(DomainConstants.MarketingFile)))
                return File.ReadAllText(GlobalVars.GetGlobalTxtVar(DomainConstants.MarketingFile));
            else
                return string.Empty;
        }
        public static string GetCapitals(string input)
        {
            return string.Concat(input.Where(c => char.IsUpper(c)));
        }
        private static void HookupPlaceHoldersToValues(int? partyIndex = null)
        {
            emailPlaceHolderActions = new Dictionary<string, Action>();

            string initials = (model.FileOwnerInitials ?? GetCapitals(model.FileOwner))?.Trim() + " ";
            
            emailPlaceHolderActions.Add(DomainConstants.EmailMatterId, () => { replaceValue = initials + model.MatterId; });
            emailPlaceHolderActions.Add(DomainConstants.EmailMilestone, () => { replaceValue = model.Milestone; });
            emailPlaceHolderActions.Add(DomainConstants.EmailMatterType, () => { replaceValue = model.MatterType; });
            emailPlaceHolderActions.Add(DomainConstants.EmailCurrentUser, () => { replaceValue = model.CurrentUser; });

            emailPlaceHolderActions.Add(DomainConstants.EmailTodaysDate, () => { replaceValue = model.TodaysDate; });
            emailPlaceHolderActions.Add(DomainConstants.EmailMatterDetails, () => { replaceValue = model.MatterDetails; });
            emailPlaceHolderActions.Add(DomainConstants.EmailMatterDescription, () => { replaceValue = model.MatterDescription; });
            emailPlaceHolderActions.Add(DomainConstants.EmailOtherSideReference, () => { replaceValue = model.OtherSideReference; });
            emailPlaceHolderActions.Add(DomainConstants.EmailParties, () => { replaceValue = model.Parties; });
            emailPlaceHolderActions.Add(DomainConstants.EmailSecurities, () => { replaceValue = model.SecurityAddress; });
            emailPlaceHolderActions.Add(DomainConstants.EmailSecuritySuburbs, () => { replaceValue = model.SecuritySuburbs; });

            emailPlaceHolderActions.Add(DomainConstants.EmailFastRefiDetails, () => { replaceValue = model.FastRefiDetails; });
            emailPlaceHolderActions.Add(DomainConstants.EmailDisclosureDate, () => { replaceValue = model.DisclosureDateStr; });
            emailPlaceHolderActions.Add(DomainConstants.EmailLEXPDate, () => { replaceValue = model.LEXPDateStr; });

            emailPlaceHolderActions.Add(DomainConstants.EmailFileOwner, () => { replaceValue = model.FileOwner; });
            emailPlaceHolderActions.Add(DomainConstants.EmailFileOwnerEmail, () => { replaceValue = model.FileOwnerEmail; });
            emailPlaceHolderActions.Add(DomainConstants.EmailFileOwnerPhone, () => { replaceValue = model.FileOwnerPhone; });
            emailPlaceHolderActions.Add(DomainConstants.EmailLender, () => { replaceValue = model.LenderName; });
            emailPlaceHolderActions.Add(DomainConstants.EmailBorrower, () => { replaceValue = model.BorrowerName; });
            emailPlaceHolderActions.Add(DomainConstants.EmailBroker, () => { replaceValue = model.BrokerName; });
            emailPlaceHolderActions.Add(DomainConstants.EmailMortMgr, () => { replaceValue = model.MortMgrName; });
            emailPlaceHolderActions.Add(DomainConstants.EmailLoanAmt, () => { replaceValue = model.LoanAmount; });
            emailPlaceHolderActions.Add(DomainConstants.EmailLenderRefNo, () => { replaceValue = model.LenderRefNo;});
            emailPlaceHolderActions.Add(DomainConstants.EmailSecondaryRefNo, () => { replaceValue = model.SecondaryRefNo; });
            emailPlaceHolderActions.Add(DomainConstants.EmailSettlementDate, () => { replaceValue = model.SettlementDate; });
            emailPlaceHolderActions.Add(DomainConstants.EmailSettlementTime, () => { replaceValue = model.SettlementTime; });
            emailPlaceHolderActions.Add(DomainConstants.EmailSettlementVenue, () => { replaceValue = model.SettlementVenue; });
            emailPlaceHolderActions.Add(DomainConstants.EmailSettlementNotes, () => { replaceValue = model.SettlementNotes; });
            emailPlaceHolderActions.Add(DomainConstants.EmailChequeCollectionBranch, () => { replaceValue = model.ChequeCollectionBranch; });
            emailPlaceHolderActions.Add(DomainConstants.EmailFinancialGrid, () => { replaceValue = model.MatterFinancialGrid; });
            emailPlaceHolderActions.Add(DomainConstants.EmailTitleReferences, () => { replaceValue = model.TitleReferences; });
            emailPlaceHolderActions.Add(DomainConstants.EmailSendProcDocsDetails, () => { replaceValue = BuildSendProcDocsEmail(model.SendProcDocsDetails); });
            emailPlaceHolderActions.Add(DomainConstants.EmailPaperPexa, () => { replaceValue = model.SettlementType; });
            emailPlaceHolderActions.Add(DomainConstants.EmailPexaNomDate, () => { replaceValue = model.PexaNominatedDate; });
            emailPlaceHolderActions.Add(DomainConstants.EmailPexaNomDateNew, () => { replaceValue = model.PexaNominatedDateNew; });
            emailPlaceHolderActions.Add(DomainConstants.EmailDischargePayoutRequirements, () => { replaceValue = model.DischargePayoutRequirements; });
            emailPlaceHolderActions.Add(DomainConstants.EmailPartyFullnames, () => { replaceValue = model.PartyFullnames; });
            emailPlaceHolderActions.Add(DomainConstants.EmailPartyFirstnames, () => { replaceValue = partyIndex.HasValue ? model.PartyFirstNames[partyIndex.Value] : model.PartyFirstNamesStr; });

            //emailPlaceHolderActions.Add(DomainConstants.EmailDischargePayouts, () => { replaceValue = model.DischargePayouts  });
            emailPlaceHolderActions.Add(DomainConstants.EmailDisbursements, () => { replaceValue = model.MatterDisbursements; });
            emailPlaceHolderActions.Add(DomainConstants.EmailDisbursementsTotal, () => { replaceValue = model.DisbursementsTotal; });


            emailPlaceHolderActions.Add(DomainConstants.EmailAnticipatedDate, () => { replaceValue = model.AnticipatedDate; });
            emailPlaceHolderActions.Add(DomainConstants.EmailDischargePayout, () => { replaceValue = model.DischargePayoutStr; });
            emailPlaceHolderActions.Add(DomainConstants.EmailGreeting, () => { replaceValue = "Good " + EmailsService.GetTimeOfDay(DateTime.Now); });
            emailPlaceHolderActions.Add(DomainConstants.EmailFHOGYesNo, () => { replaceValue = model.FHOGAnswer; });
            emailPlaceHolderActions.Add(DomainConstants.EmailWorkTypes, () => { replaceValue = model.WorkTypes; });
            emailPlaceHolderActions.Add(DomainConstants.EmailDischargeType, () => { replaceValue = model.DischargeType ?? ""; });

        }

        public static string BuildSendProcDocsEmail(List<EmailEntities.EmailSendProcDocsModel> model)
        {
            if (model == null) return string.Empty;
            var sb = new StringBuilder();

            var classForFont = GetFontSizeUsingClass(DomainConstants.EmailSendProcDocsDetails);

            foreach (var item in model)
            {
                if (!item.DeliveryMethodId.HasValue) continue;

                sb.Append($"<table><tr><u style = 'font-size: 12pt;'>{item.DocsSentToLabel} The following documents have been issued via {item.DeliveryMethod}:</u></tr>");
                sb.Append($"<tr height=30px></tr></table>");
                sb.Append($"<table><tr><b>Recipient: </b></td><td>{item.NameDocsSentTo}</td></tr></table>");

                if (item.DeliveryMethodId == (int) Enums.DocumentDeliveryTypeEnum.Email || item.DeliveryMethodId == (int) Enums.DocumentDeliveryTypeEnum.DigiDocs)
                {
                    sb.Append($"<table><tr>{item.EmailDocsSentTo}</tr></table>");
                }
                else if (item.DeliveryMethodId == (int) Enums.DocumentDeliveryTypeEnum.ExpressPostReturnEnvelope || item.DeliveryMethodId == (int) Enums.DocumentDeliveryTypeEnum.ExpressPostNoReturnEnvelope)
                {
                    sb.Append($"<table><tr>");
                    
                    sb.Append($"<tr height=20px></tr>");
                    if (!string.IsNullOrWhiteSpace(item.ExpressPostSentTracking))
                    {
                        sb.Append($"<tr><td><b>Express Post to Recipient Tracking: </b></td><td>{item.ExpressPostSentTracking}</td></tr>");
                        sb.Append($"<tr height=20px></tr>");
                    }
                    if (!string.IsNullOrWhiteSpace(item.ExpressPostReceiveTracking))
                    {
                        sb.Append($"<tr><td><b>Reply Paid Express Post Tracking: </b></td><td>{item.ExpressPostReceiveTracking}</td></tr>");                     
                    }
                    sb.Append("</table>");
                }
                else if (item.DeliveryMethodId == (int) Enums.DocumentDeliveryTypeEnum.ForCollection)
                {
                    sb.Append($"<table><tr>Collection from MSA National</tr></table>");
                }
                
                sb.Append($"<br/><br/>");
            }

            return sb.ToString();
        }


        public class SignatureData
        {
            public string Signature { get; set; }
            public List<string> Filepaths { get; set; }
        }

        /// <summary>
        /// WIP - send email via exchange rather than outlook interop
        /// </summary>
        public static void SendEmailViaExchange(string emailTo, string emailCC, string emailSubject, string emailBody, List<string> attachments = null)
        {
            
            //if(GlobalVars.CurrentUser.ExchangeWebService != null)
            //{
            //    var sig = GetUserOutlookSignature();
            //    emailBody = emailBody + sig.Signature;
            //    GlobalVars.CurrentUser.ExchangeWebService.SendEmail(emailTo, emailSubject, emailBody, attachments: attachments, inlineAttachments: sig.Filepaths, ccAddressCsv: emailCC);
            //}
            
        }





        public static SignatureData CreateCustomSignature(string customSignaturePath, Entities.OfficeView officeDetails, bool isNoReply = false, bool isForOutlook = false)
        {
            var sig = new SignatureData();

            string signature = "";
            string sigDirectory = customSignaturePath;
            List<string> filePaths = new List<string>();




            if (Directory.Exists(sigDirectory))
            {
                var sigFiles = Directory.GetFiles(sigDirectory, "*.htm", SearchOption.TopDirectoryOnly);
                if (sigFiles.Any())
                {
                    signature = File.ReadAllText(sigFiles.FirstOrDefault());

                    foreach (var subDir in Directory.GetDirectories(sigDirectory))
                    {
                        var attachIndex = 0;
                        foreach (var file in Directory.GetFiles(subDir))
                        {
                            var extension = Path.GetExtension(file)?.ToLower();
                            List<string> validFileTypes = new List<string>() { ".png", ".jpg", ".bmp", ".gif", ".tiff", ".jpeg", ".svg" };

                            if (validFileTypes.Contains(extension))
                            {
                                filePaths.Add(file);
                                string toReplace = $"src=\"{subDir.Replace(sigDirectory + "\\", "")}\\{Path.GetFileName(file)}\"";
                                signature = signature.Replace(toReplace, isForOutlook ? $"src=\"{file}\"" : $"src=\"cid:img-{attachIndex}\"");

                                string toReplace2 = $"src=\"{subDir.Replace(sigDirectory + "\\", "")}/{Path.GetFileName(file)}\"";
                                signature = signature.Replace(toReplace2, isForOutlook ? $"src=\"{file}\"" : $"src=\"cid:img-{attachIndex}\"");

                                attachIndex++;
                            }
                        }
                    }

                    Dictionary<String, String> placeholders = new Dictionary<string, string>
                    {
                        { "{{UserName}}", isNoReply ? "MSA National" : GlobalVars.CurrentUser.FullName },
                        { "{{UserPhone}}", isNoReply ? officeDetails?.Phone : GlobalVars.CurrentUser.Phone },
                        { "{{UserFax}}", isNoReply ? officeDetails?.Fax : GlobalVars.CurrentUser.Fax },
                        { "{{UserMobile}}", isNoReply ? "": GlobalVars.CurrentUser.Mobile },
                        { "{{UserEmail}}", isNoReply ? "Support@msanational.com.au" : GlobalVars.CurrentUser.Email },
                        { "{{UserRole}}", isNoReply ? "Panel Solicitor" : GlobalVars.CurrentUser.Notes ?? " " }
                    };

                    foreach (var placeholder in placeholders)
                    {
                        signature = signature.Replace(placeholder.Key, placeholder.Value);
                    }

                    sig.Filepaths = filePaths;
                 
                    sig.Signature = signature;

                    return sig;
                }


            }
            return null;

        }
        public static void SendUserEmailViaSMTP(string emailTo, string emailCC, string emailBCC, string emailSubject, string emailBody, bool isSms,  List<string> attachments = null, bool createCustomSignature = false, string customSignaturePath = null)
        {

           
            var sig = isSms ? null : (createCustomSignature && customSignaturePath != null && Directory.Exists(customSignaturePath) ? CreateCustomSignature(customSignaturePath, null) : GetUserOutlookSignature());
            emailBody = emailBody + sig?.Signature;
            MailMessage message = new MailMessage();


            AlternateView avHtml = AlternateView.CreateAlternateViewFromString(emailBody, null, System.Net.Mime.MediaTypeNames.Text.Html);


            SmtpClient smtpClient = new SmtpClient(GlobalVars.GetGlobalTxtVar("MailSMTPServer"))
            {
                EnableSsl = true
            };

          
            var cred = CredentialManager.ReadCredential(GlobalVars.CurrentUser.CredentialStore);

            smtpClient.UseDefaultCredentials = false;
            
            if (cred != null && cred.Password != null)
            {
                //var test = System.Net.CredentialCache.DefaultNetworkCredentials;

                //System.Net.CredentialCache.DefaultNetworkCredentials.UserName = GlobalVars.CurrentUser.Email;
                smtpClient.Credentials = new System.Net.NetworkCredential($"{Environment.UserName}@msanational.com.au", cred.Password);
            }
            else
            {
                var credStore = $"MicrosoftOffice15_Data:SSPI:{Environment.UserName}@msanational.com.au";

                cred = CredentialManager.ReadCredential(credStore);
                if (cred != null && cred.Password != null)
                {
                    smtpClient.Credentials = new System.Net.NetworkCredential($"{Environment.UserName}@msanational.com.au", cred.Password);
                    CredentialManager.DeleteCredential("SLICK");
                    CredentialManager.WriteCredential("SLICK", Environment.UserName, cred.Password, CredentialPersistence.Enterprise);

                }
                else
                { 
                    throw (new Exception($"No credentials found to send SMTP email - looking for credential: {GlobalVars.CurrentUser.CredentialStore}"));
                }
            }


            if (GlobalVars.GetGlobalTxtVar("MailSMTPport") != null)
                smtpClient.Port = Convert.ToInt32(GlobalVars.GetGlobalTxtVar("MailSMTPport"));


            MailAddress fromMail = new MailAddress($"{Environment.UserName}@msanational.com.au");

            message.From = fromMail;


            if(emailTo == null)
            {
                emailTo = "";
            }
            if(emailCC == null)
            {
                emailCC = "";
            }
            if(emailBCC == null)
            {
                emailBCC = "";
            }

            if (emailTo.Contains(';'))
            {
                emailTo = emailTo.Replace(';', ',');
            }
            foreach(var to in emailTo.Split(','))
            {
                if (!string.IsNullOrEmpty(to))
                {
                    message.To.Add(to.Trim());
                }
            }
            if (emailCC.Contains(';'))
            {
                emailCC = emailCC.Replace(';', ',');
            }
            if (emailBCC?.Contains(';') == true)
            {
                emailBCC = emailBCC.Replace(';', ',');
            }

            foreach (var cc in emailCC.Split(','))
            {
                if (!string.IsNullOrEmpty(cc))
                {
                    message.CC.Add(cc.Trim());
                }
            }

            foreach (var bcc in emailBCC.Split(','))
            {
                if (!string.IsNullOrEmpty(bcc))
                {
                    message.Bcc.Add(bcc.Trim());
                }
            }

            message.Subject = emailSubject?.Trim()?.Replace('\r', ' ')?.Replace('\n', ' ');
            message.IsBodyHtml = true;
            message.Body = emailBody;

            int attachIndex = 0;

            if (sig.Filepaths != null)
            {
                foreach (var inline in sig.Filepaths)
                {

                    if (System.IO.Path.GetExtension(inline)?.ToUpper() != ".XML")
                    {
                        //Attachment att = new Attachment(inline);
                        //att.ContentDisposition.Inline = true;
                        //message.Attachments.Add(att);
                        //message.Attachments[attachIndex].ContentId = $"img-{attachIndex}";
                        string mimeType = System.Web.MimeMapping.GetMimeMapping(inline);

                        LinkedResource linked = new LinkedResource(inline, mimeType);
                        linked.ContentId = $"img-{attachIndex}";
                        avHtml.LinkedResources.Add(linked);
                        attachIndex++;
                    }
                }
            }
            message.AlternateViews.Add(avHtml);

            if (attachments != null)
            {
                foreach (var doc in attachments)
                {
                    Attachment att = new Attachment(doc);
                    message.Attachments.Add(att);
                }
            }
            smtpClient.Send(message);

        }



        private static SignatureData GetUserOutlookSignature()
        {
            string signature = "";
            string sigDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Signatures");
            List<string> filePaths = new List<string>();

            if (Directory.Exists(sigDirectory))
            {
                var sigFiles = Directory.GetFiles(sigDirectory, "*.htm", SearchOption.TopDirectoryOnly);
                if (sigFiles.Any())
                {
                    signature = File.ReadAllText(sigFiles.FirstOrDefault());

                    foreach(var subDir in Directory.GetDirectories(sigDirectory))
                    {
                        var attachIndex = 0;
                        foreach (var file in Directory.GetFiles(subDir))
                        {
                            var extension = Path.GetExtension(file)?.ToLower();
                            List<string> validFileTypes = new List<string>() { ".png", ".jpg", ".bmp", ".gif", ".tiff", ".jpeg" };

                            if (validFileTypes.Contains(extension))
                            {
                                filePaths.Add(file);
                                string toReplace = $"src=\"{subDir.Replace(sigDirectory + "\\", "")}\\{Path.GetFileName(file)}\"";
                                signature = signature.Replace(toReplace, $"src=\"cid:img-{attachIndex}\"");

                                string toReplace2 = $"src=\"{subDir.Replace(sigDirectory + "\\", "")}/{Path.GetFileName(file)}\"";
                                signature = signature.Replace(toReplace2, $"src=\"cid:img-{attachIndex}\"");

                                attachIndex++;
                            }
                        }
                    }
                }


            }





            return new SignatureData() { Signature = signature, Filepaths = filePaths};

        }


        /// <summary>
        /// Uses the inbuilt class of the placeholder - to populate to other spans in the email contents
        /// </summary>
        /// <param name="emailPlaceHolder"></param>
        /// <returns></returns>
        private static string GetFontSizeUsingClass(string emailPlaceHolder)
        {
            if (string.IsNullOrEmpty(emailBody)) return string.Empty;

            try
            {
                var placeholderIndex = -1;
                var spanIndex = -1;
                var classIndex = -1;
                var classInfo = string.Empty;
                var classString = string.Empty;

                placeholderIndex = emailBody.IndexOf(emailPlaceHolder);
                if (placeholderIndex != -1)
                    spanIndex = emailBody.LastIndexOf("<span", placeholderIndex, 100);

                if (spanIndex != -1)
                    classIndex = emailBody.IndexOf("class=", spanIndex);

                if (classIndex != -1)
                    classInfo = emailBody.Substring(classIndex, 30);
               
                if (!string.IsNullOrEmpty(classInfo))
                {
                    string doubleQuote = "\"";
                    var startClass = classInfo.IndexOf(doubleQuote);
                    var endClass = classInfo.IndexOf(doubleQuote, startClass + 1);
                    classString = classInfo.Substring(startClass, endClass - startClass + 1);
                }
               
                return $"class={classString}";
            }
            catch (Exception ex)
            {
                Handlers.ErrorHandler.LogError(ex);
                return string.Empty;
            }
        }
        #endregion

        #region dMailApi
        //public static int AddDMailTemplate(string subject, string body, string currentUser, int teamTypeId = (int)TeamTypeEnum.General)
        public static int AddTemplate(EmailEntities.DMailTemplateView template, string currentUser)
        {
            int newTemplateId;
            
            using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadCommitted))
            {
                
                UserRepository userRep = new UserRepository(uow.Context);
                EmailsRepository emailRep = new EmailsRepository(uow.Context);
                newTemplateId = emailRep.AddTemplate(template.TemplateSubject, template.TemplateBody, userRep.GetUserByUserName(currentUser).UserId, (template.TeamTypeId ?? (int)TeamTypeEnum.General));
                uow.CommitTransaction();
            }
            return newTemplateId;
        }
        public static int AddCustomTemplate(int templateId, string subject, string body, string currentUser)
        {
            int customTemplateId;

            using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadCommitted))
            {
                UserRepository userRep = new UserRepository(uow.Context);
                EmailsRepository emailRep = new EmailsRepository(uow.Context);
                customTemplateId = emailRep.AddCustomTemplate(templateId, subject, body, userRep.GetUserByUserName(currentUser).UserId);
                uow.CommitTransaction();
            }
            return customTemplateId;
        }

        public static EmailEntities.DMailTemplateView GetTemplate(int templateId)
        {
            EmailEntities.DMailTemplateView template;

            using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadUncommitted))
            {
                EmailsRepository emailRep = new EmailsRepository(uow.Context);
                template = emailRep.GetTemplate(templateId);
            }
            return template;
        }

        /*
        public static IEnumerable<EmailEntities.DMailTemplateView> GetTeamDMailTemplates(int? teamTypeId = null)
        {
            IEnumerable<EmailEntities.DMailTemplateView> templates;

            using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadUncommitted))
            {
                EmailsRepository emailRep = new EmailsRepository(uow.Context);
                templates = emailRep.GetTeamDMailTemplates(teamTypeId);
            }
            return templates;
        }*/

        public static IEnumerable<EmailEntities.DMailTemplateView> GetTeamTemplates(IEnumerable<int> teamTypeId = null)
        {
            IEnumerable<EmailEntities.DMailTemplateView> templates;

            using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadUncommitted))
            {
                EmailsRepository emailRep = new EmailsRepository(uow.Context);
                templates = emailRep.GetTeamTemplates(teamTypeId);
            }
            return templates;
        }

        public static IEnumerable<EmailEntities.DMailTemplateView> GetTemplates(IEnumerable<int> templateIds)
        {
            IEnumerable<EmailEntities.DMailTemplateView> templates;

            using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadUncommitted))
            {
                EmailsRepository emailRep = new EmailsRepository(uow.Context);
                templates = emailRep.GetTemplates(templateIds);
            }
            return templates;
        }

        public static IEnumerable<EmailEntities.DMailTemplateView> GetCustomTemplates(IEnumerable<int> templateIds, string currentUser)
        {
            IEnumerable<EmailEntities.DMailTemplateView> templates;

            using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadUncommitted))
            {
                UserRepository userRep = new UserRepository(uow.Context);
                EmailsRepository emailRep = new EmailsRepository(uow.Context);
                templates = emailRep.GetCustomTemplates(templateIds, userRep.GetUserByUserName(currentUser).UserId);
            }
            return templates;
        }

        #endregion

        //HACK: TODO: CP
        //The following methods are copies of what is in Outlook Helper in SLICK_UI
        // Ideally want to move those methods into here instead. - currently there are Message boxes for Errors, Subscriptions etc. which we don't want here though.
        // Areas Commented out Need changing - So will get to this later.
        #region "OutlookHelper Methods"
        private static void ReleaseOutlook()
        {
            if (_mail != null)
            {
                Marshal.ReleaseComObject(_mail);
                _mail = null;
                _outlookUnavailable = false;
                _outlookUnavailableMessageDisplayed = false;
                _sendSubscribed = false;
                
            }
        }

        private static Outlook.MailItem _mail = null;
        private static Outlook.Application _outlookApp = null;
        private static bool _sendSubscribed = false;
        private static bool _outlookUnavailable = false;
        private static bool _outlookUnavailableMessageDisplayed = false;
        private static string MatterIDUserProperty = "SLICKMatterId";
        //static SynchronizationContext _uiThread = SynchronizationContext.Current;

        public static Outlook.Application GetOutlookApplication()
        {
            try
            {
                ReleaseOutlook();
                if (Process.GetProcessesByName("OUTLOOK").Count() > 0)
                {
                    try
                    {
                        return Marshal.GetActiveObject("Outlook.Application") as Outlook.Application;
                    }
                    catch(Exception e)
                    {
                        if (e.Message.Contains("Operation unavailable (Exception from HRESULT: 0x800401E3 (MK_E_UNAVAILABLE))"))
                        {
                        }
                    }
                }
                else
                {
                    var application = new Outlook.Application();
                    Outlook.NameSpace nameSpace = application.GetNamespace("MAPI");
                    nameSpace.Logon("", "", Missing.Value, Missing.Value);
                    return application;
                }
            }

            catch (Exception ex)
            {
                _outlookUnavailable = true;
                if (ex.Message.Contains("Operation unavailable"))
                {
                    if (!_outlookUnavailableMessageDisplayed)
                    {
                        throw;
                        //SlickMessageBoxes.ShowWarningMessage("Outlook not open", "Please open outlook first to send the message via Slick", 1);
                        //_outlookUnavailableMessageDisplayed = true;
                    }
                }
                else
                {
                    throw;
                }
            }

            return null;
        }

        public static bool StartOutlook(string fileName = null)
        {
            var outlookApp = GetOutlookApplication();
            if (outlookApp == null) return false;
            _outlookApp = outlookApp;

            Outlook.MailItem mail = fileName != null ? outlookApp.CreateItemFromTemplate(fileName) : outlookApp.CreateItem(Outlook.OlItemType.olMailItem)
                                        as Outlook.MailItem;

            mail.Save();
            mail.Display(true);
            return true;
        }

        public static bool StartOutlookAndSendMail(string subject, int matterId)
        {
            var outlookApp = GetOutlookApplication();
            if (outlookApp == null) return false;
            _outlookApp = outlookApp;
            _mail = outlookApp.CreateItem(Outlook.OlItemType.olMailItem) as Outlook.MailItem;
            if (!string.IsNullOrEmpty(subject)) _mail.Subject = subject;
            if (!_sendSubscribed)
            {
                ((Outlook.ItemEvents_10_Event) _mail).Send += new Outlook.ItemEvents_10_SendEventHandler(OnSendEmail);
            }

            _mail.UserProperties.Add(MatterIDUserProperty, Outlook.OlUserPropertyType.olNumber, null, null);
            _mail.UserProperties.Find(MatterIDUserProperty).Value = matterId;
            _mail.Display(false);

            return true;
        }
        public static bool StartOutlookAndCreatePopupEmail(string subject, string body, string emailTo, string emailCC, string emailBCC,  List<Tuple<string,string>> attachments, int matterId, bool isSMS, bool createCustomSignature, string customSignatureDirectory)
        {
            var outlookApp = GetOutlookApplication();
            if (outlookApp == null) return false;

            _outlookApp = outlookApp;
            _mail = outlookApp.CreateItem(Outlook.OlItemType.olMailItem) as Outlook.MailItem;

            if (!string.IsNullOrEmpty(subject)) _mail.Subject = subject;
            if (!_sendSubscribed)
            {
                ((Outlook.ItemEvents_10_Event)_mail).Send += new Outlook.ItemEvents_10_SendEventHandler(OnSendEmailSaveDocAndNote);
            }

            _mail.UserProperties.Add(MatterIDUserProperty, Outlook.OlUserPropertyType.olNumber, null, null);
            _mail.UserProperties.Find(MatterIDUserProperty).Value = matterId;

            _mail.Subject = subject;
            if (isSMS)
            {
                _mail.HTMLBody = body;
            }
            else
            {
                if (createCustomSignature)
                {
                    BuildEmailBodyWithCustomSignature(body, customSignatureDirectory);
                }
                else
                {
                    BuildEmailBodyWithSignature(body);
                }
            }
            _mail.To = emailTo;
            _mail.CC = emailCC;
            _mail.BCC = emailBCC;

            if (attachments != null)
            {
                foreach (var attachment in attachments)
                {
                    _mail.Attachments.Add(attachment.Item2, Outlook.OlAttachmentType.olByValue, 1, attachment.Item1);
                    try
                    {
                        if (!attachment.Item2.ToUpper().Contains("SLICK_RW"))
                        {
                            File.Delete(attachment.Item2);
                        }
                    }
                    catch(Exception)
                    {
                        //will be cleared when temp dir gets cleared on signout
                    }
                    //_mail.Attachments.Add(path);
                }
            }
            _mail.Display(true);
            return true;
        }
        public static void OnSendEmail(ref bool Cancel)
        {
            try
            {
                if (_sendSubscribed)
                {
                    ((Outlook.ItemEvents_10_Event) _mail).Send -= new Outlook.ItemEvents_10_SendEventHandler(OnSendEmail);
                }
                int matterId = Convert.ToInt32(_mail.UserProperties.Find(MatterIDUserProperty).Value);


                //DocumentInfo doc = null;
                //MatterDocument matterDoc = null;

                //SaveEmailMessageToDocuments(matterId, ref doc, ref matterDoc);


            }
            catch (Exception ex)
            {
                Slick_Domain.Handlers.ErrorHandler.LogError(ex);
            }
        }
        public static void OnSendEmailSaveDocAndNote(ref bool Cancel)
        {
            try
            {
                if (_sendSubscribed)
                {
                    ((Outlook.ItemEvents_10_Event)_mail).Send -= new Outlook.ItemEvents_10_SendEventHandler(OnSendEmail);
                }
                int matterId = Convert.ToInt32(_mail.UserProperties.Find(MatterIDUserProperty).Value);


                DocumentInfo doc = null;
                MatterDocument matterDoc = null;

                SaveEmailMessageToDocuments(matterId, ref doc, ref matterDoc);
                SaveEmailMessageToNotes(matterId);

            }
            catch (Exception ex)
            {
                Slick_Domain.Handlers.ErrorHandler.LogError(ex);
            }
        }

        private static string ToCleanFileName(string filename)
        {
            return System.IO.Path.GetInvalidFileNameChars().Aggregate(filename, (current, c) => current.Replace(c.ToString(), string.Empty));
        }
        private static bool SaveEmailMessageToDocuments(int matterId, ref DocumentInfo doc, ref MatterDocument matterDoc)
        {
            var fileName = ToCleanFileName(Slick_Domain.Helpers.DocumentHelper.TruncateDocumentName($"{_mail.Subject}.msg", "msg"));
            string temp = Path.Combine(Path.GetTempPath(), fileName);

            Helpers.DocumentHelper.DeleteFile(temp);

            _mail.SaveAs(temp);
            var data = File.ReadAllBytes(temp);
            File.Delete(temp);

            doc = new DocumentInfo
            {
                DocumentDisplayAreaType = Slick_Domain.Enums.DocumentDisplayAreaEnum.Attachments,
                FileName = fileName,
                FileType = "msg",
                MatterId = matterId,
                ModDate = DateTime.Now,
                Data = data
            };

            bool savedToDb = false;
            using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadCommitted))
            {
                try
                {
                    if (uow.GetDocumentsRepositoryInstance().SaveDocument(ref doc, ref matterDoc))
                    {
                        uow.CommitTransaction();
                        savedToDb = true;
                    }
                }
                catch (Exception)
                {
                    uow.RollbackTransaction();
                    throw;
                }
            }
            if (savedToDb)
            {
                Helpers.DocumentHelper.SaveDocumentToFileSystem(matterId, doc);
            }
            return savedToDb;

        }

        private static void SaveEmailMessageToNotes(int matterId)
        {
          
            int? noteId = null;

            string noteBody = _mail.HTMLBody;
            int signatureIndex = noteBody.IndexOf(Slick_Domain.Constants.GlobalConstants.EmailSignatureStartString);
            if (signatureIndex > -1)
            {
                noteBody = noteBody.Substring(0, signatureIndex);
            }
            else
            {
                int originalMessageIndex = noteBody.IndexOf(Slick_Domain.Constants.GlobalConstants.EmailOriginalMessageStartString);
                if(originalMessageIndex > -1)
                {
                    noteBody = noteBody.Substring(0, originalMessageIndex);
                }
            }

            //Get rid of tables from the notes
            var pattern = @"(\</?TABLE(.*?)/?\>)";
            noteBody = Regex.Replace(noteBody, pattern, string.Empty, RegexOptions.IgnoreCase);
            pattern = @"(\</?TR(.*?)/?\>)";
            noteBody = Regex.Replace(noteBody, pattern, string.Empty, RegexOptions.IgnoreCase);
            pattern = @"(\</?TD(.*?)/?\>)";
            noteBody = Regex.Replace(noteBody, pattern, string.Empty, RegexOptions.IgnoreCase);
            //spooooooky


            string subject = $"Mail sent to: {_mail.To}";

            MatterNote newNote = new MatterNote()
            {

                MatterId = matterId,
                NoteBody = noteBody,
                NoteHeader = subject,
                HighPriority = false,
                IsDeleted = false,
                IsPinned = false,
                
                IsPublic = true,
                MatterNoteTypeId = (int)MatterNoteTypeEnum.Other,
                UpdatedByUserId =  GlobalVars.CurrentUser.UserId,
                UpdatedDate = DateTime.Now

            };

            using(var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadCommitted))
            {
                uow.Context.MatterNotes.Add(newNote);
                uow.Save();
                uow.CommitTransaction();
            }
            
        }

        private static System.Timers.Timer _crashCheckTimer = null;
        private static int _secondsElapsed = 0;
        private static bool _emailSent = false;
        internal static void CreateNewMailItem()
        {
            var outlookApp = GetOutlookApplication();
            if (outlookApp == null) return;
            _outlookApp = outlookApp;


            _crashCheckTimer = new System.Timers.Timer(30000);
            _crashCheckTimer.Enabled = true;
            _crashCheckTimer.AutoReset = false;
            _secondsElapsed = 0;
            _emailSent = false;

            _crashCheckTimer.Elapsed += CrashCheckTimer_Tick;
            _crashCheckTimer.Start();


            _mail = outlookApp.CreateItem(Outlook.OlItemType.olMailItem) as Outlook.MailItem;
        }

        private static string GetProcessUser(Process process)
        {
            IntPtr processHandle = IntPtr.Zero;
            try
            {
                OpenProcessToken(process.Handle, 8, out processHandle);
                WindowsIdentity wi = new WindowsIdentity(processHandle);
                string user = wi.Name;
                return user.Contains(@"\") ? user.Substring(user.IndexOf(@"\") + 1) : user;
            }
            catch
            {
                return null;
            }
            finally
            {
                if (processHandle != IntPtr.Zero)
                {
                    CloseHandle(processHandle);
                }
            }
        }

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);
        //honestly? I don't know if this will work.
        //Try determine if outlook has frozen and commit seppuku to save every other database connection.
        //Die with honour, glorious Slick. Die with honour.
        public static void CrashCheckTimer_Tick(object sender, EventArgs e)
        {

            if (_crashCheckTimer != null)
            {
                _crashCheckTimer.Stop();
                _crashCheckTimer.Enabled = false;
                _crashCheckTimer.Elapsed -= CrashCheckTimer_Tick;
                _crashCheckTimer = null;
            }
            _secondsElapsed = 0;
           

            List<string> processes = Process.GetProcesses().Select(x => x.ProcessName).ToList();

            var test = processes.Where(x => x.ToUpper().Contains("SLICK"));
            string username = GlobalVars.CurrentUser.Username.ToUpper();
            bool killSlick = false;
            if (!_emailSent)
            {
                foreach (var proc in Process.GetProcessesByName("OUTLOOK"))
                {
                    string procOwner = GetProcessUser(proc)?.ToUpper();

                    if (procOwner == username && (_mail != null || !proc.Responding))
                    {

                        if (_mail != null) Marshal.ReleaseComObject(_mail);
                        if (_outlookApp != null) Marshal.ReleaseComObject(_outlookApp);
                        try
                        {
                            proc.Kill();
                        }
                        catch (Exception)
                        {
                            //don't have access to kill this process :(
                        }
                        killSlick = true;
                    }
                }
            }
            if (killSlick)
            {
                foreach (var slickProc in Process.GetProcessesByName("Slick_UI"))
                {
                    string slickProcOwner = GetProcessUser(slickProc)?.ToUpper();
                    if (slickProcOwner == username)
                    {
                        try
                        {
                            slickProc.Kill();
                        }
                        catch (Exception)
                        {
                            //don't have access to kill this process :( 
                        }
                    }
                }
                //if we're still alive: a captain always goes down with their ship
                if (System.Windows.Application.Current != null)
                {
                    System.Windows.Application.Current.Dispatcher.InvokeShutdown();
                }
            }

            if (_outlookApp != null)
            {
                Marshal.ReleaseComObject(_outlookApp);
                _outlookApp = null;
            }
            if(_mail != null)
            {
                Marshal.ReleaseComObject(_mail);
                _mail = null;
            }
            

        }


        public static void AddMailAttachment(string subject, string path)
        {
            if (_mail == null)
            {
                CreateNewMailItem();
                if (_mail == null) return;
            }
            _mail.Attachments.Add(path);
            if (!string.IsNullOrEmpty(subject)) _mail.Subject = subject;
        }

        public static void DisplayMail()
        {
            if (_mail == null) return;
            _mail.Display(false);
            _mail = null;
        }

        public static void SendEmail(string subject, string body, string to, bool isHTML, bool sendAutomatically = false)
        {
            CreateNewMailItem();
            if (_mail == null) return;

            _mail.Subject = subject;
            if (isHTML)
            {

                BuildEmailBodyWithSignature(body);
            }
            else
            {
                _mail.Body = body;
            }

            _mail.To = to;
            DisplayMail();
            if (sendAutomatically)
            {
                _mail.Send();
            }
        }
        public static void BuildEmailBodyWithCustomSignature(string body, string customSignatureDirectory)
        {
            if (!string.IsNullOrEmpty(body))
            {
                if (!Directory.Exists(customSignatureDirectory))
                {
                    BuildEmailBodyWithSignature(body);
                    return;
                }


                _mail.GetInspector.Activate();


                //TODO: Check how to manipulate this also
                var sigData = CreateCustomSignature(customSignatureDirectory, null, false, isForOutlook: true);
                if (sigData != null)
                {
                    _mail.HTMLBody = null;
                    if (sigData.Filepaths != null)
                    {
                        foreach (var file in sigData?.Filepaths)
                        {

                        }
                    }
                    _mail.HTMLBody = $"<p>{body}</p>" + sigData.Signature;
                    
                }
                else
                {
                    BuildEmailBodyWithSignature(body);
                    return;
                }

            }
            else
            {
                _mail.GetInspector.Activate();
            }
        }
        public static void BuildEmailBodyWithSignature(string body)
        {
            if (!string.IsNullOrEmpty(body))
            {
                _mail.GetInspector.Activate();
                
                var signature = _mail.HTMLBody;
                //yeah idk why the email signatures are set up with an acre of whitespace but let's get rid of that.
                var shittyWhitespace = "<p class=MsoNormal><o:p>&nbsp;</o:p></p><p class=MsoNormal><o:p>&nbsp;</o:p></p><p class=MsoNormal><a name=\"_MailAutoSig\"><span style='font-size:10.0pt;font-family:\"Arial\",sans-serif;mso-fareast-font-family:\"Times New Roman\";mso-fareast-theme-font:minor-fareast;color:black;mso-fareast-language:EN-AU;mso-no-proof:yes'><br><br>&nbsp;<o:p></o:p></span></a></p>";
                signature = signature.Replace(shittyWhitespace, "");
                _mail.HTMLBody = signature;
                //moving on
                var bodyIndex = signature.IndexOf("<body");
                var endBodyIndex = signature.IndexOf(">", bodyIndex);

                //TODO: Check how to manipulate this also
                _mail.HTMLBody = body;
                var bodyHtml = _mail.HTMLBody;

                var bodyParagraph = $"<p>{body}</p><br>";
                _mail.HTMLBody = $"{signature.Substring(0, bodyIndex)}{bodyParagraph}{signature.Substring(bodyIndex)}";
            }
            else
            {
                _mail.GetInspector.Activate();
            }
        }
      
        public static void SendAutomatedEmail(string subject, string body, string toEmail, bool noReply = false)
        {
            try
            {
                if (noReply)
                {

                }
                else
                {
                    CreateNewMailItem();

                    CheckAndSendEmailIfOutlookUnavailable(subject, body, toEmail);

                    if (_mail == null) return;

                    _mail.Subject = subject;

                    BuildEmailBodyWithSignature(body);

                    _mail.To = toEmail;

                    var testingEmails = GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmails)?.ToUpper() == "TRUE";
                    if (testingEmails)
                    {
                        _mail.To = GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmail);
                        _mail.Subject += $" | WOULD HAVE BEEN SENT TO {toEmail}";
                    }


                    _mail.Send();

                    ReleaseOutlook();
                }

            }
            catch (Exception ex)
            {
                Handlers.ErrorHandler.LogError(ex);
                CheckAndSendEmailIfOutlookUnavailable(subject, body, toEmail);
            }
        }

        //SendAutomatedEmail(String subject, String body, EmailPlaceHolderModel emailModel, String DocsToAttach, Nullable`1 matterId, Boolean sendEmail, Boolean sendSms, Boolean noReply)
        

        public static void SendLinkedFileManagerExceptionEmail(string exceptionReason, int lenderId, int newMatterId, List<int> linkedMatterIds)
        {
            string subject = $"ALERT: File Managers could not be resolved for {newMatterId}";
            string body = $"<span style='font-family: Calibri; font-size: 11pt;'> <p>Hi team,</p>{newMatterId} has been created and linked to matter/s: <br/>- {String.Join("<br/>- ", linkedMatterIds)}"
                + $"<p>However, the File Manager could not be automatically assigned for these matter/s and requires manual amendment.<br/><b>REASON:</b> {exceptionReason}</p>Kind Regards,</br>Alfred";
            List<string> recipients = new List<string>();

            using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadUncommitted))
            {
                var teams = uow.Context.Teams.Where(t => t.TeamLenders.Any(l => l.LenderId == lenderId) && t.TeamTypeId == (int)TeamTypeEnum.DocPrep && t.Enabled);
                if (!teams.Any())
                {
                    recipients.Add("george.kogios@msanational.com.au");
                }
                else
                {
                    foreach (var team in teams)
                    {
                        recipients = recipients.Concat(uow.GetTeamRepositoryInstance().GetUserListForTeam(team.TeamId).Select(x => new { x.Email, x.IsQA }).Where(x => x.IsQA).Select(x => x.Email).ToList()).ToList();
                    }

                    if (!recipients.Any())
                    {
                        recipients.Add("george.kogios@msanational.com.au");
                    }
                }

                if(GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmails, uow.Context).ToUpper() == "TRUE")
                {
                    subject += $" - WOULD HAVE BEEN SENT TO {string.Join(", ", recipients)}";
                    recipients = new List<string> { "george.kogios@msanational.com.au", "robert.quinn@msanational.com.au" };
                }
            }

            SendEmail(subject, body, string.Join(",", recipients), true);
        }
        public static void SendPossibleLinkedFileManagerExceptionEmail(string exceptionReason, int lenderId, int newMatterId, List<int> linkedMatterIds, SlickContext context)
        {
            string subject = $"ALERT: File Managers could not be resolved for {newMatterId}";

            string body = $"<span style='font-family: Calibri; font-size: 11pt;'> <p>Hi team,</p><p>{newMatterId} has been linked to matter/s: <br/>- {String.Join("<br/>- ", linkedMatterIds)}</p>"
                + $"<p>However, the File Manager could not be automatically assigned for these matter/s and requires manual amendment.<br/><b>REASON:</b> {exceptionReason}</p>";

            List<string> recipients = new List<string>();

            var teams = context.Teams.Where(t => t.TeamLenders.Any(l => l.LenderId == lenderId) && t.TeamTypeId == (int)TeamTypeEnum.DocPrep && t.Enabled);
            if (!teams.Any())
            {
                recipients.Add("george.kogios@msanational.com.au");
            }
            else
            {
                foreach (var team in teams)
                {
                    recipients = recipients.Concat(new TeamRepository(context).GetUserListForTeam(team.TeamId).Select(x => new { x.Email, x.IsQA }).Where(x => x.IsQA).Select(x => x.Email).ToList()).ToList();
                }

                if (!recipients.Any())
                {
                    recipients.Add("george.kogios@msanational.com.au");
                }
            }

            //if (GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmails, context).ToUpper() == "TRUE")
            //{
            //    subject += $" - WOULD HAVE BEEN SENT TO {string.Join(", ", recipients)}";
            //    recipients = new List<string> { "george.kogios@msanational.com.au", "robert.quinn@msanational.com.au" };
            //}
            try
            {
                SendAutomatedEmail(subject, body, string.Join(";", recipients));
            }
            catch(Exception)
            {
                SendAutomatedEmail(subject, body, string.Join(",", recipients), noReply:true);
            }
        }
        public static void SendPossibleLinkedFileManagerReRunDocPrepEmail(int lenderId, int matterId, SlickContext context)
        {
            string subject = $"ALERT: File Manager has Changed for {matterId}";
            string body = $"<span style='font-family: Calibri; font-size: 11pt;'> <p>Hi team,</p><p>{matterId} has had its File Manager changed due to being linked to another matter.</p>"
                + $"<p>Please re-run documents for this matter to correctly note the new file manager.";

            List<string> recipients = new List<string>();

            var teams = context.Teams.Where(t => t.TeamLenders.Any(l => l.LenderId == lenderId) && t.TeamTypeId == (int)TeamTypeEnum.DocPrep && t.Enabled);
            if (!teams.Any())
            {
                recipients.Add("george.kogios@msanational.com.au");
            }
            else
            {
                foreach (var team in teams)
                {
                    recipients = recipients.Concat(new TeamRepository(context).GetUserListForTeam(team.TeamId).Select(x => new { x.Email, x.IsQA }).Where(x => x.IsQA).Select(x => x.Email).ToList()).ToList();
                }

                if (!recipients.Any())
                {
                    recipients.Add("george.kogios@msanational.com.au");
                }
            }

            try
            {
                SendAutomatedEmail(subject, body, string.Join(";", recipients));
            }
            catch(Exception)
            {
                SendAutomatedEmail(subject, body, string.Join(",", recipients), noReply:true);
            }
        }



        public static IEnumerable<string> Split(string str, int n)
        {
            if (String.IsNullOrEmpty(str) || n < 1)
            {
                throw new ArgumentException();
            }

            return Enumerable.Range(0, str.Length / n)
                            .Select(i => str.Substring(i * n, n));
        }


        public static List<string> SplitSMS(string fullSMS, int chunkMaxLength = 450)
        {

            List<string> chunks = new List<string>();

            int fullSMSLength = fullSMS.Length;
            int currLength = 0;

            int chunkStartIndex = 0;
            int chunkEndIndex = 0;

            

            while (fullSMS?.Length > 0)
            {
                fullSMS = fullSMS?.Substring(chunkStartIndex)?.Trim();
                if (!string.IsNullOrEmpty(fullSMS))
                {
                    string subStr = "";

                    if (fullSMS.Length > chunkMaxLength)
                    {
                        subStr = fullSMS.Substring(0, chunkMaxLength);
                    }
                    else
                    {
                        subStr = fullSMS;
                    }

                    chunkEndIndex = subStr.LastIndexOfAny(new char[] { '.', ':', ';', ',' });
                    if (chunkEndIndex < 1 || chunkEndIndex < subStr.Length * 0.70)
                    {
                        chunkEndIndex = subStr.LastIndexOf(' ');
                        if (chunkEndIndex < 1)
                        {
                            chunkEndIndex = chunkMaxLength - 1;
                        }
                    }

                    chunkEndIndex += 1;

                    subStr = subStr.Substring(0, chunkEndIndex);
                    chunks.Add(subStr.Trim());

                    currLength = chunks.Sum(c => c.Length);

                    chunkStartIndex = chunkEndIndex;
                }
            }

            return chunks.Where(s=>!string.IsNullOrEmpty(s)).ToList();
        }


        public static string SendAutomatedEmail(string subject, string body, EmailEntities.EmailPlaceHolderModel emailModel, string docsToAttach = null, int? matterId = null, bool sendEmail = true, bool sendSms = false, bool noReply = false, SlickContext existingContext = null, bool isPopUpEmail = false, List<EmailEntities.LenderDocsToAttach> lenderDocs = null, bool useCustomSignature = false, string customSignatureDirectory = null, int? updatedByUser = null, MatterCustomEntities.MatterWFComponentView comp = null, int? partyIndex = null)
        {

            if(sendSms && matterId != null && existingContext != null)
            {
                var mortMgrName = existingContext.Matters.Where(m => m.MatterId == matterId).Select(m => m.MortMgr.MortMgrName).FirstOrDefault();
                


                if (mortMgrName != null && mortMgrName.ToUpper().Contains("TICTOC"))
                {
                    _context = existingContext;
                    var recipientStr = GetToEmailAddress(ref emailModel, true, false);
                    if (string.IsNullOrEmpty(recipientStr)) { return null; }
                    string smsTo = string.Join("", recipientStr.Where(c => char.IsNumber(c) || c == ';'));

                    var testingEmails = GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmails, existingContext)?.ToUpper() == "TRUE";
                    if (testingEmails)
                    {
                        body += $" PROD RECIPIENT: {smsTo}";
                        smsTo = GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_FallbackPhone, existingContext);
                    }

                    if(smsTo == null)
                    {
                        return null;
                    }

                    if (smsTo != null)
                    {
                        ReplaceEmailPlaceHolders(emailModel, ref subject, ref body, partyIndex);

                        List<string> splitSmsAddresses = smsTo.Split(';').ToList();

                        foreach (var mobile in splitSmsAddresses)
                        {
                            try
                            {
                                var loggingEmails = GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_LoggingEmails, existingContext)?.ToUpper() == "TRUE";
                                string fullBody = System.Web.HttpUtility.UrlEncode(subject) + ":%0a" + CommonMethods.ConvertToPlainText(body);
                                if (fullBody.Length > 459)
                                {
                                    var splits = SplitSMS(fullBody);
                                    for(int i = 0; i < splits.Count(); i++)
                                    {
                                        var split = splits[i];
                                        SmsService.SendMacquarieNowSms(string.Join("", mobile.Where(c => char.IsNumber(c))), split, null, sender: Constants.GlobalConstants.TicTocSmsName);
                                        if (loggingEmails)
                                        {
                                            GeneralCustomEntities.EmailLogView log = new GeneralCustomEntities.EmailLogView($"{i+1}/{splits.Count()} : {subject}", split, mobile, emailModel, updatedByUser);
                                            LogEmailData(log);
                                        }
                                    }
                                }
                                else
                                {
                                    SmsService.SendMacquarieNowSms(string.Join("", mobile.Where(c => char.IsNumber(c))), CommonMethods.ConvertToPlainText(body), subject, sender: Constants.GlobalConstants.TicTocSmsName);
                                    if (loggingEmails)
                                    {
                                        GeneralCustomEntities.EmailLogView log = new GeneralCustomEntities.EmailLogView(subject, body, mobile, emailModel, updatedByUser);
                                        LogEmailData(log);
                                    }
                                }
                            }
                               
                            
                            catch (Exception)
                            {
                                var loggingEmails = GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_LoggingEmails, existingContext)?.ToUpper() == "TRUE";

                                if (loggingEmails)
                                {
                                    GeneralCustomEntities.EmailLogView log = new GeneralCustomEntities.EmailLogView(subject, body, mobile, emailModel, updatedByUser) { LogNotes = "EXCEPTION SENDING API SMS" };
                                    LogEmailData(log);
                                }
                            }
                        }
                    }

                    return smsTo;

                }
            }





            string allEmailRecipients = "";
            emailSubject = subject;
            emailBody = body;
            model = emailModel;
            _context = existingContext;
            if(attachments != null) attachments.Clear();
            if (!string.IsNullOrEmpty(docsToAttach))
            {
                docsToAttach.Trim();
            }

            List<string> replyToList = new List<string>();

            if (existingContext != null && matterId.HasValue)
            {
                var mtGroupTypeId = existingContext.Matters.Where(m => m.MatterId == matterId.Value).Select(m => m.MatterGroupTypeId).FirstOrDefault();

                if (mtGroupTypeId == (int)MatterGroupTypeEnum.NewLoan)
                {
                    var mwfRep = new MatterWFRepository(existingContext);
                    var eRep = new EmailsRepository(existingContext);
                    //if (!mwfRep.HasMilestoneCompleted(matterId.Value, (int)WFComponentEnum.DocsReturned) && !mwfRep.HasMilestoneCompleted(matterId.Value, (int)WFComponentEnum.CheckReturnedDocs))
                    //{
                    //    replyToList.Add(GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_NoReplyDocPrepContactAddress, existingContext));
                    //}
                    //else if (existingContext.Matters.FirstOrDefault(m => m.MatterId == matterId.Value).Settled)
                    //{
                    //    replyToList.Add(GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_NoReplyDocPrepContactAddress, existingContext));
                    //}
                    replyToList = eRep.GetReplyAddressesForMatter(matterId.Value);
                }
            }
            else
            {
                replyToList = new List<string>(){ emailModel.FileOwnerEmail };
            }

            MailMessage message = new MailMessage();

            foreach(var addr in replyToList)
            {
                message.ReplyToList.Add(addr);
            }
            if (isPopUpEmail)
            {
                if (noReply)
                {
                    return null;
                }
                if (noReply == false) //using outlook, send email from current user
                {

                    string mailTo = "";
                    string mailCC = "";
                    string mailBCC = "";
                    List<Tuple<string, string>> toAttach = new List<Tuple<string, string>>();
                    if (sendEmail)
                        mailTo = GetToEmailAddress(ref emailModel);

                    if (sendSms)
                    {
                        string smsDetails = GetToEmailAddress(ref emailModel, true);
                        if (!String.IsNullOrEmpty(smsDetails)) _mail.To += ";" + smsDetails;
                    }

                    if (string.IsNullOrEmpty(mailTo))
                    {
                        if (!String.IsNullOrEmpty(emailModel.EmailMobiles.CCEmails))
                        {
                            mailTo = emailModel.EmailMobiles.CCEmails;
                            emailModel.EmailMobiles.CCEmails = null;
                        }
                    }





                    if (!string.IsNullOrEmpty(emailModel.EmailMobiles.CCEmails))
                    {
                        mailCC = emailModel.EmailMobiles.CCEmails;
                    }


                    if (!string.IsNullOrEmpty(emailModel.CCEmail))
                    {
                        mailCC = emailModel.CCEmail + ";" + mailCC;
                    }
                    mailBCC = emailModel.BCCEmail;



                    if(lenderDocs?.Any() == true)
                    {
                        foreach(var doc in lenderDocs)
                        {
                            var sourceFilePath = Helpers.DocumentHelper.GetLenderDocPath(doc.LenderId, doc.LenderDocumentMasterId, doc.LenderDocumentVersionId, doc.DocType, false);
                            if (File.Exists(sourceFilePath))
                            {
                                var pathBase = System.IO.Path.GetTempPath() + "\\" + doc.LenderDocumentMasterId + "\\" + doc.LenderDocumentVersionId + "\\" + DateTime.Now.Millisecond + "\\";
                                doc.DocName = doc.DocName.ToSafeFileName();
                                if(doc.DocName.Length > 50)
                                {
                                    doc.DocName = doc.DocName.Substring(0, 45);
                                }

                                if (!Directory.Exists(pathBase))
                                {
                                    Directory.CreateDirectory(pathBase);
                                }

                                var tempFilePath = pathBase + doc.DocName +  "." + doc.DocType;

                                int dupIndex = 1;

                                while (File.Exists(tempFilePath))
                                {
                                    tempFilePath = pathBase + "_" + dupIndex + "." + doc.DocType;
                                    dupIndex++;
                                }

                                File.Copy(sourceFilePath, tempFilePath);

                                if (File.Exists(tempFilePath))
                                {
                                    toAttach.Add(new Tuple<string, string>(doc.DocName + "." + doc.DocType, tempFilePath));
                                    //_mail.Attachments.Add(tempFilePath, Outlook.OlAttachmentType.olByValue, 1, doc.DocName);   
                                }
                            }
                        }
                    }


                    if (!string.IsNullOrEmpty(docsToAttach) && matterId != null)
                    {
                        attachments = docsToAttach.Split(',').ToList();
                        if (_context == null)
                        {
                            using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadUncommitted))
                            {
                                var docsRep = new DocumentsRepository(uow.Context);
                                var matterDocs = docsRep.GetDocuments(matterId.Value);

                                

                                foreach (var attachment in attachments)
                                {
                                    string currentDocName = "";
                                    try
                                    {
                                        var docs = matterDocs.Where(d => d.DocName.Trim().ToUpper().Contains(attachment.Trim().ToUpper()) && !d.IsDeleted).OrderByDescending(u => u.UpdatedDate).ToList();

                                        if (attachment?.ToUpper()?.Contains("VOI REPORT") != true && attachment?.ToUpper()?.Contains("IDYOU") != true)
                                        {
                                            docs = docs.Take(1).ToList();
                                        }
                                        foreach (var doc in docs)
                                        {
                                            currentDocName = doc.DocName;
                                            string filePath = Path.Combine(GlobalVars.GetGlobalTxtVar(DomainConstants.MatterDocumentsDirectory, uow.Context),
                                                              matterId.ToString(), string.Format("{0}.{1}", doc.DocumentId.ToString(), string.IsNullOrEmpty(doc.DocType) ? "txt" : doc.DocType));
                                            if (File.Exists(filePath))
                                            {
                                                string tempPath = Path.Combine(Path.GetTempPath(), matterId.ToString());
                                                Directory.CreateDirectory(tempPath);

                                                string tempFilePath = tempPath + "\\" + doc.DocName + "." + (string.IsNullOrEmpty(doc.DocType) ? "txt" : doc.DocType);
                                                string origFilePath = tempFilePath; //in case we convert to pdf, so we can still delete the original as well.
                                                if (File.Exists(tempFilePath)) File.Delete(tempFilePath);
                                                File.Copy(filePath, tempFilePath);

                                                if (Helpers.DocumentHelper.IsWordFileExtension(doc.DocType) && emailModel.ConvertAttachmentsToPDF) //we need to convert to pdf first
                                                {

                                                    tempFilePath = Helpers.DocumentHelper.ExportToPDF(tempFilePath, doc.DocName, doc.DocType, tempPath);
                                                }

                                                if (File.Exists(tempFilePath))
                                                {

                                                    try
                                                    {
                                                        toAttach.Add(new Tuple<string, string>(doc.DocName, tempFilePath));

                                                        //_mail.Attachments.Add(tempFilePath, Outlook.OlAttachmentType.olByValue, 1, doc.DocName);
                                                    }
                                                    catch (IOException)
                                                    {
                                                        toAttach.Add(new Tuple<string, string>(doc.DocName, origFilePath));

                                                        //_mail.Attachments.Add(origFilePath, Outlook.OlAttachmentType.olByValue, 1, doc.DocName);

                                                    }
                                                    try
                                                    {
                                                        File.Delete(tempFilePath);
                                                        if (File.Exists(origFilePath)) File.Delete(origFilePath);
                                                        Directory.Delete(tempPath, true);
                                                    }
                                                    catch (IOException e)
                                                    {
                                                        //not the end of the world honestly.
                                                    }
                                                }
                                                else
                                                {
                                                    throw new FileNotFoundException();
                                                }
                                            }
                                            else
                                            {
                                                throw new FileNotFoundException();
                                            }
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        Handlers.ErrorHandler.LogError(e, $"NON-FATAL: Could not find document {currentDocName} to attach to auto-email for {matterId}"); //if document doesn't exist, don't worry, but still log it.
                                    }
                                }
                            }
                        }
                        else
                        {
                            var docsRep = new DocumentsRepository(_context);
                            var matterDocs = docsRep.GetDocuments(matterId.Value).OrderByDescending(u => u.UpdatedDate);

                            foreach (var attachment in attachments)
                            {
                                string currentDocName = "";
                                try
                                {
                                    var docs = matterDocs.Where(d => d.DocName.Trim().ToUpper().Contains(attachment.Trim().ToUpper()) && !d.IsDeleted)
                                         .OrderByDescending(u => u.UpdatedDate).ToList();
                                    if (attachment?.ToUpper()?.Contains("VOI REPORT") != true && attachment?.ToUpper()?.Contains("IDYOU") != true)
                                    {
                                        docs = docs.Take(1).ToList();
                                    }
                                    foreach (var doc in docs)
                                    {
                                      
                                        if (doc == null) continue;
                                        currentDocName = doc.DocName;
                                        string filePath = Path.Combine(GlobalVars.GetGlobalTxtVar(DomainConstants.MatterDocumentsDirectory, _context),
                                                          matterId.ToString(), string.Format("{0}.{1}", doc.DocumentId.ToString(), string.IsNullOrEmpty(doc.DocType) ? "txt" : doc.DocType));
                                        if (File.Exists(filePath))
                                        {
                                            string tempPath = Path.Combine(Path.GetTempPath(), matterId.ToString());
                                            Directory.CreateDirectory(tempPath);

                                            string tempFilePath = tempPath + "\\" + doc.DocName + "." + (string.IsNullOrEmpty(doc.DocType) ? "txt" : doc.DocType);
                                            string origFilePath = tempFilePath; //in case we convert to pdf, so we can still delete the original as well.
                                            if (File.Exists(tempFilePath)) File.Delete(tempFilePath);
                                            File.Copy(filePath, tempFilePath);

                                            if (Helpers.DocumentHelper.IsWordFileExtension(doc.DocType) && emailModel.ConvertAttachmentsToPDF) //we need to convert to pdf first
                                            {

                                                tempFilePath = Helpers.DocumentHelper.ExportToPDF(tempFilePath, doc.DocName, doc.DocType, tempPath);
                                            }

                                            if (File.Exists(tempFilePath))
                                            {
                                                toAttach.Add(new Tuple<string, string>(doc.DocName, tempFilePath));

                                                //_mail.Attachments.Add(tempFilePath, Outlook.OlAttachmentType.olByValue, 1, doc.DocName);
                                                //File.Delete(tempFilePath);
                                                //if (File.Exists(origFilePath)) File.Delete(origFilePath);
                                                //Directory.Delete(tempPath, true);
                                            }
                                            else
                                            {
                                                throw new FileNotFoundException();
                                            }
                                        }
                                        else
                                        {
                                            throw new FileNotFoundException();
                                        }
                                    }
                                }
                                catch (Exception e)
                                {
                                    Handlers.ErrorHandler.LogError(e, $"NON-FATAL: Could not find document {currentDocName} to attach to auto-email for {matterId}"); //if document doesn't exist, don't worry, but still log it.
                                }
                            }
                        }
                    }
                    else
                    {
                        attachments = null;
                    }

                    ReplaceEmailPlaceHolders(partyIndex);


                   

                    var loggingEmails = _context == null ? GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_LoggingEmails)?.ToUpper() == "TRUE" : GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_LoggingEmails, _context)?.ToUpper() == "TRUE";
                    if (loggingEmails)
                    {
                        GeneralCustomEntities.EmailLogView log = new GeneralCustomEntities.EmailLogView(emailBody, emailSubject, mailTo, mailCC, mailBCC, emailModel, updatedByUser: updatedByUser, popup: true)
                        {
                            ModelNoReply = noReply
                        };
                        LogEmailData(log);
                    }

                    var testingEmails = _context == null ? GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmails)?.ToUpper() == "TRUE" :
                        GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmails, _context)?.ToUpper() == "TRUE" ||
                        _context.Matters.FirstOrDefault(m => m.MatterId == matterId).IsTestFile;

                    if (testingEmails)
                    {
                        var toAddress = mailTo;
                        var ccAddress = mailCC;
                        var bccAddress = mailBCC;
                        mailTo = _context == null ? GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmail) : GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmail, _context);
                        if (String.IsNullOrEmpty(mailCC) && string.IsNullOrEmpty(mailBCC))
                        {
                            emailSubject += $" TESTING - Would be Sent to {toAddress}";
                        }
                        else if (String.IsNullOrEmpty(mailBCC))
                        {
                            mailCC = _context == null ? GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmail) : GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmail, _context);
                            emailSubject += $" TESTING - Would be Sent to {toAddress} and CC'd to {ccAddress}";
                        }
                        else if(String.IsNullOrEmpty(mailCC))
                        {
                            mailBCC = _context == null ? GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmail) : GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmail, _context);
                            emailSubject += $" TESTING - Would be Sent to {toAddress} and BCC'd to {bccAddress}";
                        }
                        else 
                        {
                            mailCC = _context == null ? GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmail) : GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmail, _context);
                            mailBCC = mailCC;
                            emailSubject += $" TESTING - Would be Sent to {toAddress} and CC'd to {ccAddress} and BCC'd to {bccAddress}";
                        }
                    }

                    if (matterId.HasValue)
                    {
                        if (!string.IsNullOrEmpty(customSignatureDirectory))
                        {
                            customSignatureDirectory = Path.Combine(GlobalVars.GetGlobalTxtVar("CustomSignatureDirectory", existingContext), customSignatureDirectory);
                        }
                        StartOutlookAndCreatePopupEmail(emailSubject, emailBody, mailTo, mailCC, mailBCC, toAttach, matterId.Value, sendSms, createCustomSignature: useCustomSignature, customSignatureDirectory: customSignatureDirectory);
                        return mailTo + (mailCC != null ? ", " + mailCC : "");
                    }
                }
            }
            else
            {
                try
                {
                    bool sent = false;

                    var sendUserEmailViaSmtp = existingContext == null ? GlobalVars.GetGlobalTxtVar("SendUserEmailViaSMTP").ToUpper() : GlobalVars.GetGlobalTxtVar("SendUserEmailViaSMTP", existingContext).ToUpper();

                    if (noReply == false && GlobalVars.CurrentUser != null && sendUserEmailViaSmtp == "TRUE")
                    {

                        string mailTo = "";
                        string mailCC = "";
                        string mailBCC = "";
                        List<string> mailAttachments = new List<string>();
                        string mailSubject = "";
                        string mailBody = "";


                        if (sendEmail)
                            mailTo = GetToEmailAddress(ref emailModel).Replace(";", ",");

                        if (sendSms)
                        {
                            string smsDetails = GetToEmailAddress(ref emailModel, true);
                            if (!String.IsNullOrEmpty(smsDetails))
                            {
                                mailTo += "," + smsDetails;
                            }
                            else
                            {
                                mailTo = smsDetails;
                            }
                        }

                        if (string.IsNullOrEmpty(mailTo))
                        {
                            if (!String.IsNullOrEmpty(emailModel.EmailMobiles.CCEmails))
                            {
                                mailTo = emailModel.EmailMobiles.CCEmails;
                                emailModel.EmailMobiles.CCEmails = null;
                            }
                            else
                            {
                                return null;
                            }
                        }

                        if (!string.IsNullOrEmpty(emailModel.EmailMobiles.CCEmails))
                        {
                            mailCC = emailModel.EmailMobiles.CCEmails;
                        }

                        if (lenderDocs?.Any() == true)
                        {
                            foreach (var doc in lenderDocs)
                            {
                                if(doc.DocName.Length > 50)
                                {
                                    doc.DocName = doc.DocName.Substring(0, 49);
                                }

                                var sourceFilePath = Helpers.DocumentHelper.GetLenderDocPath(doc.LenderId, doc.LenderDocumentMasterId, doc.LenderDocumentVersionId, doc.DocType, false);
                                if (File.Exists(sourceFilePath))
                                {
                                    var safeDocName = doc.DocName.ToSafeFileName();
                                    var pathBase = System.IO.Path.GetTempPath() + "\\" + safeDocName;

                                    var tempFilePath = pathBase + "." + doc.DocType;

                                    int dupIndex = 1;

                                    while (File.Exists(tempFilePath))
                                    {
                                        tempFilePath = pathBase + "_" + dupIndex + "." + doc.DocType;
                                        dupIndex++;
                                    }

                                    File.Copy(sourceFilePath, tempFilePath);

                                    if (File.Exists(tempFilePath))
                                    {
                                        mailAttachments.Add(tempFilePath);
                                    }
                                }
                            }
                        }
                        if (!string.IsNullOrEmpty(docsToAttach) && matterId != null)
                        {
                            attachments = docsToAttach.Split(',').Where(x => !string.IsNullOrEmpty(x)).ToList();
                            if (_context == null)
                            {
                                using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadUncommitted))
                                {
                                    var docsRep = new DocumentsRepository(uow.Context);
                                    var matterDocs = docsRep.GetDocuments(matterId.Value);

                                    foreach (var attachment in attachments)
                                    {
                                        if (string.IsNullOrWhiteSpace(attachment))
                                        {
                                            continue;
                                        }
                                        string currentDocName = "";
                                        try
                                        {
                                            var docs = matterDocs.Where(d => d.DocName.Trim().ToUpper().Contains(attachment.Trim().ToUpper()) && !d.IsDeleted).OrderByDescending(u => u.UpdatedDate).ToList();

                                            if (attachment?.ToUpper()?.Contains("VOI REPORT") != true && attachment?.ToUpper()?.Contains("IDYOU") != true)
                                            {
                                                docs = docs.Take(1).ToList();
                                            }
                                            foreach (var doc in docs)
                                            {
                                                currentDocName = doc.DocName;
                                                string filePath = Path.Combine(GlobalVars.GetGlobalTxtVar(DomainConstants.MatterDocumentsDirectory, uow.Context),
                                                                  matterId.ToString(), string.Format("{0}.{1}", doc.DocumentId.ToString(), string.IsNullOrEmpty(doc.DocType) ? "txt" : doc.DocType));
                                                if (!File.Exists(filePath) && doc.DocType.ToUpper() == "DOC")
                                                {
                                                    filePath = Path.Combine(GlobalVars.GetGlobalTxtVar(DomainConstants.MatterDocumentsDirectory, uow.Context),
                                                               matterId.ToString(), string.Format("{0}.{1}", doc.DocumentId.ToString(), string.IsNullOrEmpty(doc.DocType) ? "txt" : "docx"));

                                                }
                                                else if (!File.Exists(filePath) && doc.DocType.ToUpper() == "DOCX")
                                                {
                                                    filePath = Path.Combine(GlobalVars.GetGlobalTxtVar(DomainConstants.MatterDocumentsDirectory, uow.Context),
                                                               matterId.ToString(), string.Format("{0}.{1}", doc.DocumentId.ToString(), string.IsNullOrEmpty(doc.DocType) ? "txt" : "doc"));
                                                }
                                                if (File.Exists(filePath))
                                                {
                                                    string tempPath = Path.Combine(Path.GetTempPath(), matterId.ToString());
                                                    Directory.CreateDirectory(tempPath);

                                                    string tempFilePath = tempPath + "\\" + doc.DocName + "." + (string.IsNullOrEmpty(doc.DocType) ? "txt" : doc.DocType);
                                                    string origFilePath = tempFilePath; //in case we convert to pdf, so we can still delete the original as well.
                                                    if (File.Exists(tempFilePath)) File.Delete(tempFilePath);
                                                    File.Copy(filePath, tempFilePath);

                                                    if (Helpers.DocumentHelper.IsWordFileExtension(doc.DocType) && emailModel.ConvertAttachmentsToPDF) //we need to convert to pdf first
                                                    {

                                                        tempFilePath = Helpers.DocumentHelper.ExportToPDF(tempFilePath, doc.DocName, doc.DocType, tempPath);
                                                    }

                                                    if (File.Exists(tempFilePath))
                                                    {

                                                        try
                                                        {
                                                            mailAttachments.Add(tempFilePath);
                                                        }
                                                        catch (IOException)
                                                        {
                                                            mailAttachments.Add(origFilePath);
                                                        }
                                                    }
                                                    else
                                                    {
                                                        throw new FileNotFoundException();
                                                    }
                                                }
                                                else
                                                {
                                                    throw new FileNotFoundException();
                                                }
                                            }
                                        }
                                        catch (Exception e)
                                        {
                                            Handlers.ErrorHandler.LogError(e, $"NON-FATAL: Could not find document {currentDocName} to attach to auto-email for {matterId}"); //if document doesn't exist, don't worry, but still log it.
                                        }
                                    }
                                }
                            }
                            else
                            {
                                var docsRep = new DocumentsRepository(_context);
                                var matterDocs = docsRep.GetDocuments(matterId.Value).OrderByDescending(u => u.UpdatedDate);

                                foreach (var attachment in attachments)
                                {
                                    string currentDocName = "";
                                    try
                                    {
                                        var doc = matterDocs.Where(d => d.DocName.Trim().ToUpper().Contains(attachment.Trim().ToUpper()) && !d.IsDeleted)
                                            .OrderByDescending(u => u.UpdatedDate).FirstOrDefault();
                                        if (doc == null) continue;
                                        currentDocName = doc.DocName;
                                        string filePath = Path.Combine(GlobalVars.GetGlobalTxtVar(DomainConstants.MatterDocumentsDirectory, _context),
                                                          matterId.ToString(), string.Format("{0}.{1}", doc.DocumentId.ToString(), string.IsNullOrEmpty(doc.DocType) ? "txt" : doc.DocType));
                                        if (File.Exists(filePath))
                                        {
                                            string tempPath = Path.Combine(Path.GetTempPath(), matterId.ToString());
                                            Directory.CreateDirectory(tempPath);

                                            string tempFilePath = tempPath + "\\" + doc.DocName + "." + (string.IsNullOrEmpty(doc.DocType) ? "txt" : doc.DocType);
                                            string origFilePath = tempFilePath; //in case we convert to pdf, so we can still delete the original as well.
                                            if (File.Exists(tempFilePath)) File.Delete(tempFilePath);
                                            File.Copy(filePath, tempFilePath);

                                            if (Helpers.DocumentHelper.IsWordFileExtension(doc.DocType) && emailModel.ConvertAttachmentsToPDF) //we need to convert to pdf first
                                            {
                                                tempFilePath = Helpers.DocumentHelper.ExportToPDF(tempFilePath, doc.DocName, doc.DocType, tempPath);
                                            }

                                            if (File.Exists(tempFilePath))
                                            {
                                                mailAttachments.Add(tempFilePath);
                                            }
                                            else
                                            {
                                                throw new FileNotFoundException();
                                            }
                                        }
                                        else
                                        {
                                            throw new FileNotFoundException();
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        Handlers.ErrorHandler.LogError(e, $"NON-FATAL: Could not find document {currentDocName} to attach to auto-email for {matterId}"); //if document doesn't exist, don't worry, but still log it.
                                    }
                                }
                            }
                        }
                        else
                        {
                            attachments = null;
                        }

                        ReplaceEmailPlaceHolders(partyIndex);

                        mailSubject = emailSubject;

                        mailBody = emailBody;

                        if (!string.IsNullOrEmpty(emailModel.CCEmail))
                        {
                            mailCC = emailModel.CCEmail + ";" + mailCC;
                        }
                        mailBCC = emailModel.BCCEmail;


                        var loggingEmails = existingContext == null ? GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_LoggingEmails)?.ToUpper() == "TRUE" : GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_LoggingEmails, existingContext)?.ToUpper() == "TRUE";
                        if (loggingEmails)
                        {
                            GeneralCustomEntities.EmailLogView log = new GeneralCustomEntities.EmailLogView(mailBody, mailSubject, mailTo, mailCC, mailBCC, emailModel);

                            LogEmailData(log);
                        }

                        var testingEmails = _context == null ? GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmails)?.ToUpper() == "TRUE"
                            : GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmails, _context)?.ToUpper() == "TRUE" || _context.Matters.FirstOrDefault(m => m.MatterId == matterId).IsTestFile;

                        if (testingEmails)
                        {
                            var toAddress = mailTo;
                            var ccAddress = mailCC;
                            var bccAddress = mailBCC;
                            mailTo = _context == null ? GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmail) : GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmail, _context);


                            if (String.IsNullOrEmpty(mailCC) && string.IsNullOrEmpty(mailBCC))
                            {
                                mailSubject += $" TESTING - Would be Sent to {toAddress}";
                            }
                            else if (String.IsNullOrEmpty(mailBCC))
                            {
                                mailCC = _context == null ? GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmail) : GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmail, _context);
                                mailSubject += $" TESTING - Would be Sent to {toAddress} and CC'd to {ccAddress}";
                            }
                            else if (String.IsNullOrEmpty(mailCC))
                            {
                                mailBCC = _context == null ? GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmail) : GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmail, _context);
                                mailSubject += $" TESTING - Would be Sent to {toAddress} and BCC'd to {bccAddress}";
                            }
                            else
                            {
                                mailCC = _context == null ? GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmail) : GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmail, _context);
                                mailBCC = mailCC;
                                mailSubject += $" TESTING - Would be Sent to {toAddress} and CC'd to {ccAddress} and BCC'd to {bccAddress}";
                            }

                        }
                        try
                        {
                            if (!string.IsNullOrEmpty(customSignatureDirectory))
                            {
                                customSignatureDirectory = Path.Combine(GlobalVars.GetGlobalTxtVar("CustomSignatureDirectory", existingContext), customSignatureDirectory);
                            }
                            SendUserEmailViaSMTP(mailTo, mailCC, mailBCC, mailSubject, mailBody, isSms: sendSms, attachments: mailAttachments, createCustomSignature: useCustomSignature, customSignaturePath: customSignatureDirectory);
                            sent = true;
                        }
                        catch (Exception e)
                        {
                            sent = false;
                            Slick_Domain.Handlers.ErrorHandler.LogError(e);
                        }


                        foreach (var attachment in mailAttachments)
                        {
                            try
                            {
                                File.Delete(attachment);
                            }
                            catch (Exception) { }
                        }

                        if (sent)
                        {
                            return mailTo + (mailCC != null ? ", " + mailCC : "");
                        }
                    }


                    if (noReply == false) //using outlook, send email from current user
                    {

                        CheckAndSendEmailIfOutlookUnavailable(subject, body, emailModel);
                        CreateNewMailItem();

                        if (_mail == null) return null;

                        if (sendEmail)
                            _mail.To = GetToEmailAddress(ref emailModel);

                        if (sendSms)
                        {
                            string smsDetails = GetToEmailAddress(ref emailModel, true);
                            if (!String.IsNullOrEmpty(smsDetails)) _mail.To += ";" + smsDetails;
                        }

                        if (string.IsNullOrEmpty(_mail.To))
                        {
                            if (!String.IsNullOrEmpty(emailModel.EmailMobiles.CCEmails))
                            {
                                _mail.To = emailModel.EmailMobiles.CCEmails;
                                emailModel.EmailMobiles.CCEmails = null;
                            }
                            else
                            {
                                ReleaseOutlook();
                                return null;
                            }
                        }

                        if (!string.IsNullOrEmpty(emailModel.EmailMobiles.CCEmails))
                        {
                            _mail.CC = emailModel.EmailMobiles.CCEmails;
                        }
                        if (string.IsNullOrEmpty(emailModel.CCEmail))
                        {
                            _mail.CC = emailModel.CCEmail + _mail.CC;
                        }
                        if (string.IsNullOrEmpty(emailModel.BCCEmail))
                        {
                            _mail.BCC = emailModel.BCCEmail;
                        }

                        if (lenderDocs?.Any() == true)
                        {
                            foreach (var doc in lenderDocs)
                            {
                                var sourceFilePath = Helpers.DocumentHelper.GetLenderDocPath(doc.LenderId, doc.LenderDocumentMasterId, doc.LenderDocumentVersionId, doc.DocType, false);
                                if (File.Exists(sourceFilePath))
                                {
                                    var pathBase = System.IO.Path.GetTempPath() + "\\" + doc.LenderDocumentVersionId;

                                    var tempFilePath = pathBase + "." + doc.DocType;

                                    int dupIndex = 1;

                                    while (File.Exists(tempFilePath))
                                    {
                                        tempFilePath = pathBase + "_" + dupIndex + "." + doc.DocType;
                                        dupIndex++;
                                    }

                                    File.Copy(sourceFilePath, tempFilePath);

                                    if (File.Exists(tempFilePath))
                                    {
                                        _mail.Attachments.Add(tempFilePath, Outlook.OlAttachmentType.olByValue, 1, doc.DocName);
                                    }
                                }
                            }
                        }


                        if (!string.IsNullOrEmpty(docsToAttach) && matterId != null)
                        {
                            attachments = docsToAttach.Split(',').ToList();
                            if (_context == null)
                            {
                                using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadUncommitted))
                                {
                                    var docsRep = new DocumentsRepository(uow.Context);
                                    var matterDocs = docsRep.GetDocuments(matterId.Value);

                                    foreach (var attachment in attachments)
                                    {
                                        string currentDocName = "";
                                        try
                                        {
                                            var docs = matterDocs.Where(d => d.DocName.Trim().ToUpper().Contains(attachment.Trim().ToUpper()) && !d.IsDeleted).OrderByDescending(u => u.UpdatedDate).ToList();

                                            if (attachment?.ToUpper()?.Contains("VOI REPORT") != true && attachment?.ToUpper()?.Contains("IDYOU") != true)
                                            {
                                                docs = docs.Take(1).ToList();
                                            }
                                            foreach (var doc in docs)
                                            {
                                                currentDocName = doc.DocName;
                                                string filePath = Path.Combine(GlobalVars.GetGlobalTxtVar(DomainConstants.MatterDocumentsDirectory, uow.Context),
                                                                  matterId.ToString(), string.Format("{0}.{1}", doc.DocumentId.ToString(), string.IsNullOrEmpty(doc.DocType) ? "txt" : doc.DocType));
                                                if (File.Exists(filePath))
                                                {
                                                    string tempPath = Path.Combine(Path.GetTempPath(), matterId.ToString());
                                                    Directory.CreateDirectory(tempPath);

                                                    string tempFilePath = tempPath + "\\" + doc.DocName + "." + (string.IsNullOrEmpty(doc.DocType) ? "txt" : doc.DocType);
                                                    string origFilePath = tempFilePath; //in case we convert to pdf, so we can still delete the original as well.
                                                    if (File.Exists(tempFilePath)) File.Delete(tempFilePath);
                                                    File.Copy(filePath, tempFilePath);

                                                    if (Helpers.DocumentHelper.IsWordFileExtension(doc.DocType) && emailModel.ConvertAttachmentsToPDF) //we need to convert to pdf first
                                                    {

                                                        tempFilePath = Helpers.DocumentHelper.ExportToPDF(tempFilePath, doc.DocName, doc.DocType, tempPath);
                                                    }

                                                    if (File.Exists(tempFilePath))
                                                    {

                                                        try
                                                        {
                                                            _mail.Attachments.Add(tempFilePath, Outlook.OlAttachmentType.olByValue, 1, doc.DocName);
                                                        }
                                                        catch (IOException)
                                                        {
                                                            _mail.Attachments.Add(origFilePath, Outlook.OlAttachmentType.olByValue, 1, doc.DocName);

                                                        }
                                                        try
                                                        {
                                                            File.Delete(tempFilePath);
                                                            if (File.Exists(origFilePath)) File.Delete(origFilePath);
                                                            Directory.Delete(tempPath, true);
                                                        }
                                                        catch (IOException e)
                                                        {
                                                            //not the end of the world honestly.
                                                        }
                                                    }
                                                    else
                                                    {
                                                        throw new FileNotFoundException();
                                                    }
                                                }
                                                else
                                                {
                                                    throw new FileNotFoundException();
                                                }
                                            }
                                        }
                                        catch (Exception e)
                                        {
                                            Handlers.ErrorHandler.LogError(e, $"NON-FATAL: Could not find document {currentDocName} to attach to auto-email for {matterId}"); //if document doesn't exist, don't worry, but still log it.
                                        }
                                    }
                                }
                            }
                            else
                            {
                                var docsRep = new DocumentsRepository(_context);
                                var matterDocs = docsRep.GetDocuments(matterId.Value).OrderByDescending(u => u.UpdatedDate);

                                foreach (var attachment in attachments)
                                {
                                    string currentDocName = "";
                                    try
                                    {
                                        var docs = matterDocs.Where(d => d.DocName.Trim().ToUpper().Contains(attachment.Trim().ToUpper()) && !d.IsDeleted).OrderByDescending(u => u.UpdatedDate).ToList();

                                        if (attachment?.ToUpper()?.Contains("VOI REPORT") != true && attachment?.ToUpper()?.Contains("IDYOU") != true)
                                        {
                                            docs = docs.Take(1).ToList();
                                        }
                                        foreach (var doc in docs)
                                        {
                                            if (doc == null) continue;
                                            currentDocName = doc.DocName;
                                            string filePath = Path.Combine(GlobalVars.GetGlobalTxtVar(DomainConstants.MatterDocumentsDirectory, _context),
                                                              matterId.ToString(), string.Format("{0}.{1}", doc.DocumentId.ToString(), string.IsNullOrEmpty(doc.DocType) ? "txt" : doc.DocType));
                                            if (File.Exists(filePath))
                                            {
                                                string tempPath = Path.Combine(Path.GetTempPath(), matterId.ToString());
                                                Directory.CreateDirectory(tempPath);

                                                string tempFilePath = tempPath + "\\" + doc.DocName + "." + (string.IsNullOrEmpty(doc.DocType) ? "txt" : doc.DocType);
                                                string origFilePath = tempFilePath; //in case we convert to pdf, so we can still delete the original as well.
                                                if (File.Exists(tempFilePath)) File.Delete(tempFilePath);
                                                File.Copy(filePath, tempFilePath);

                                                if (Helpers.DocumentHelper.IsWordFileExtension(doc.DocType) && emailModel.ConvertAttachmentsToPDF) //we need to convert to pdf first
                                                {

                                                    tempFilePath = Helpers.DocumentHelper.ExportToPDF(tempFilePath, doc.DocName, doc.DocType, tempPath);
                                                }

                                                if (File.Exists(tempFilePath))
                                                {
                                                    _mail.Attachments.Add(tempFilePath, Outlook.OlAttachmentType.olByValue, 1, doc.DocName);
                                                    File.Delete(tempFilePath);
                                                    if (File.Exists(origFilePath)) File.Delete(origFilePath);
                                                    Directory.Delete(tempPath, true);
                                                }
                                                else
                                                {
                                                    throw new FileNotFoundException();
                                                }
                                            }
                                            else
                                            {
                                                throw new FileNotFoundException();
                                            }
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        Handlers.ErrorHandler.LogError(e, $"NON-FATAL: Could not find document {currentDocName} to attach to auto-email for {matterId}"); //if document doesn't exist, don't worry, but still log it.
                                    }
                                }
                            }
                        }
                        else
                        {
                            attachments = null;
                        }

                        ReplaceEmailPlaceHolders(partyIndex);

                        _mail.Subject = emailSubject;

                        if (sendEmail)
                        {
                            BuildEmailBodyWithSignature(emailBody);
                        }
                        else
                        {
                            _mail.Body = emailBody;
                        }

                        var loggingEmails = _context == null ? GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_LoggingEmails)?.ToUpper() == "TRUE" : GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_LoggingEmails, _context)?.ToUpper() == "TRUE";
                        if (loggingEmails)
                        {
                            GeneralCustomEntities.EmailLogView log = new GeneralCustomEntities.EmailLogView(_mail, emailModel)
                            {
                                ModelNoReply = noReply
                            };
                            LogEmailData(log);
                        }

                        var testingEmails = _context == null ? GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmails)?.ToUpper() == "TRUE" :
                            GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmails, _context)?.ToUpper() == "TRUE" ||
                            _context.Matters.FirstOrDefault(m => m.MatterId == matterId).IsTestFile;

                        if (testingEmails)
                        {
                            var toAddress = _mail.To;
                            var ccAddress = _mail.CC;
                            var bccAddress = _mail.BCC;
                            _mail.To = _context == null ? GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmail) : GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmail, _context);
                            if (String.IsNullOrEmpty(_mail.CC) && string.IsNullOrEmpty(_mail.BCC))
                            {
                                _mail.Subject += $" TESTING - Would be Sent to {toAddress}";
                            }
                            else if (String.IsNullOrEmpty(_mail.BCC))
                            {
                                _mail.CC = _context == null ? GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmail) : GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmail, _context);
                                _mail.Subject += $" TESTING - Would be Sent to {toAddress} and CC'd to {ccAddress}";
                            }
                            else if (String.IsNullOrEmpty(_mail.CC))
                            {
                                _mail.BCC = _context == null ? GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmail) : GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmail, _context);
                                _mail.Subject += $" TESTING - Would be Sent to {toAddress} and BCC'd to {bccAddress}";
                            }
                            else
                            {
                                _mail.CC = _context == null ? GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmail) : GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmail, _context);
                                _mail.BCC = _mail.CC;
                                _mail.Subject += $" TESTING - Would be Sent to {toAddress} and CC'd to {ccAddress} and BCC'd to {bccAddress}";
                            }

                        }

                        _mail.Send();
                        ReleaseOutlook();
                        return _mail.To + (_mail.CC != null ? ", " + _mail.CC : "");
                    }
                    else if (noReply == true) //we will use SMTP to send from the no reply email acc
                    {
                        string mailTo = "";
                        string mailCC = "";
                        string mailBCC = "";

                        string mailSubject = subject;
                        string mailBody = body;

                        if (sendEmail)
                            mailTo = GetToEmailAddress(ref emailModel);

                        if (sendSms)
                        {
                            string smsDetails = GetToEmailAddress(ref emailModel, true);
                            if (!String.IsNullOrEmpty(smsDetails))
                            {
                                if (!String.IsNullOrEmpty(mailTo) && mailTo.Last() != ',')
                                {
                                    mailTo += "," + smsDetails;
                                }
                                else
                                {
                                    mailTo += smsDetails;
                                }
                            }
                        }
                        if (string.IsNullOrEmpty(mailTo)) return null;

                        if (emailModel.EmailMobiles != null && !string.IsNullOrEmpty(emailModel.EmailMobiles.CCEmails))
                        {
                            mailCC = emailModel.EmailMobiles.CCEmails;

                            if (string.IsNullOrEmpty(mailTo) && !string.IsNullOrEmpty(mailCC))
                            {
                                mailTo = mailCC;
                                mailCC = null;
                            }

                        }

                        if (!string.IsNullOrEmpty(emailModel.CCEmail))
                        {
                            mailCC = string.IsNullOrEmpty(mailCC) ? emailModel.CCEmail : mailCC + ", " + emailModel.CCEmail;
                        }


                        if (!string.IsNullOrEmpty(emailModel.BCCEmail))
                        {
                            mailBCC = emailModel.BCCEmail;
                        }

                        ReplaceEmailPlaceHolders(model, ref mailSubject, ref mailBody);

                        string toAddress = "";
                        string ccAddress = "";
                        string bccAddress = "";
                        // check if testing emails currently on
                        var testingEmails = _context == null ? GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmails)?.ToUpper() == "TRUE" : GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmails, _context)?.ToUpper() == "TRUE";

                        if (testingEmails)
                        {
                            toAddress = mailTo;
                            ccAddress = mailCC;
                            bccAddress = mailBCC;

                            mailTo = _context == null ? GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmail) : GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmail, _context);
                            if (String.IsNullOrEmpty(mailCC) && string.IsNullOrEmpty(mailBCC))
                            {
                                mailSubject += $" TESTING - Would be Sent to {toAddress}";
                            }
                            else if (String.IsNullOrEmpty(mailBCC))
                            {
                                mailCC = _context == null ? GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmail) : GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmail, _context);
                                mailSubject += $" TESTING - Would be Sent to {toAddress} and CC'd to {ccAddress}";
                            }
                            else if (String.IsNullOrEmpty(mailCC))
                            {
                                mailBCC = _context == null ? GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmail) : GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmail, _context);
                                mailSubject += $" TESTING - Would be Sent to {toAddress} and BCC'd to {bccAddress}";
                            }
                            else
                            {
                                mailCC = _context == null ? GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmail) : GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmail, _context);
                                mailBCC = mailCC;
                                mailSubject += $" TESTING - Would be Sent to {toAddress} and CC'd to {ccAddress} and BCC'd to {bccAddress}";
                            }
                        }

                        SmtpClient smtpClient = new SmtpClient(_context == null ? GlobalVars.GetGlobalTxtVar("MailSMTPServer") : GlobalVars.GetGlobalTxtVar("MailSMTPServer", _context))
                        {
                            EnableSsl = true
                        };

                        if (_context == null ? GlobalVars.GetGlobalTxtVar("MailSMTPport") != null : GlobalVars.GetGlobalTxtVar("MailSMTPport", _context) != null)
                            smtpClient.Port = Convert.ToInt32(_context == null ? GlobalVars.GetGlobalTxtVar("MailSMTPport") : GlobalVars.GetGlobalTxtVar("MailSMTPport", _context));

                        if (_context == null ? GlobalVars.GetGlobalTxtVar("MailSMTPServer") == null : GlobalVars.GetGlobalTxtVar("MailSMTPServer", _context) == null)
                            smtpClient.UseDefaultCredentials = true;
                        else
                            smtpClient.Credentials = new System.Net.NetworkCredential(_context == null ? GlobalVars.GetGlobalTxtVar("MailCredUser") : GlobalVars.GetGlobalTxtVar("MailCredUser", _context),
                                _context == null ? GlobalVars.GetGlobalTxtVar("MailCredPass") : GlobalVars.GetGlobalTxtVar("MailCredPass", _context));

                        MailAddress fromMail = new MailAddress(_context == null ? GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_FallbackEmail) : GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_FallbackEmail, _context));


                        message.From = fromMail;

                        mailTo = mailTo.Replace(";", ",").Replace("\r", "").Replace("\n", "").Replace("\t", "");

                        if (mailTo.Last() == ',')
                        {
                            mailTo = mailTo.Substring(0, mailTo.Length - 1);
                        }

                        message.To.Add(mailTo);

                        if (!string.IsNullOrEmpty(mailCC))
                        {
                            mailCC = mailCC.Replace(";", ",").Replace("\r", "").Replace("\n", "").Replace("\t", "");

                            if (mailCC.Last() == ',')
                            {
                                mailCC = mailCC.Substring(0, mailTo.Length - 1);
                            }

                            message.CC.Add(mailCC);
                        }

                        if (!string.IsNullOrEmpty(mailBCC))
                        {
                            foreach (var bcc in mailBCC.Split(';'))
                            {
                                message.Bcc.Add(bcc);
                            }
                        }

                        message.Subject = mailSubject.Replace("\r", "").Replace("\n", "").Replace("\t", "");


                        if (lenderDocs?.Any() == true)
                        {
                            foreach (var doc in lenderDocs)
                            {
                                if(doc.DocName.Length > 50)
                                {
                                    doc.DocName = doc.DocName.Substring(0, 49);
                                }
                                var sourceFilePath = Helpers.DocumentHelper.GetLenderDocPath(doc.LenderId, doc.LenderDocumentMasterId, doc.LenderDocumentVersionId, doc.DocType, false);
                                if (File.Exists(sourceFilePath))
                                {
                                    var safeDocName = doc.DocName.ToSafeFileName();
                                    var pathBase = System.IO.Path.GetTempPath() + "\\" + safeDocName;

                                    var tempFilePath = pathBase + "." + doc.DocType;

                                    int dupIndex = 1;

                                    while (File.Exists(tempFilePath))
                                    {
                                        tempFilePath = pathBase + "_" + dupIndex + "." + doc.DocType;
                                        dupIndex++;
                                    }

                                    File.Copy(sourceFilePath, tempFilePath);

                                    if (File.Exists(tempFilePath))
                                    {
                                        message.Attachments.Add(new Attachment(tempFilePath));
                                    }
                                }
                            }
                        }

                        if (!string.IsNullOrEmpty(docsToAttach) && matterId != null)
                        {
                            attachments = docsToAttach.Split(',').ToList();
                            if (_context == null)
                            {
                                using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadUncommitted))
                                {
                                    var docsRep = new DocumentsRepository(uow.Context);
                                    var matterDocs = docsRep.GetDocuments(matterId.Value);

                                    foreach (var attachment in attachments)
                                    {
                                        string currentDocName = "";
                                        try
                                        {
                                            var docs = matterDocs.Where(d => d.DocName.Trim().ToUpper().Contains(attachment.Trim().ToUpper()) && !d.IsDeleted).OrderByDescending(u => u.UpdatedDate).ToList();

                                            if (attachment?.ToUpper()?.Contains("VOI REPORT") != true && attachment?.ToUpper()?.Contains("IDYOU") != true)
                                            {
                                                docs = docs.Take(1).ToList();
                                            }
                                            foreach(var doc in docs)
                                            { 
                                                currentDocName = doc.DocName;
                                                string filePath = Path.Combine(GlobalVars.GetGlobalTxtVar(DomainConstants.MatterDocumentsDirectory, uow.Context),
                                                                  matterId.ToString(), string.Format("{0}.{1}", doc.DocumentId.ToString(), string.IsNullOrEmpty(doc.DocType) ? "txt" : doc.DocType));
                                                if (File.Exists(filePath))
                                                {
                                                    string tempPath = Path.Combine(Path.GetTempPath(), matterId.ToString());
                                                    Directory.CreateDirectory(tempPath);

                                                    string tempFilePath = tempPath + "\\" + doc.DocName + "." + (string.IsNullOrEmpty(doc.DocType) ? "txt" : doc.DocType);
                                                    string origFilePath = tempFilePath; //in case we convert to pdf, so we can still delete the original as well.
                                                    if (File.Exists(tempFilePath)) File.Delete(tempFilePath);
                                                    File.Copy(filePath, tempFilePath);

                                                    if (Helpers.DocumentHelper.IsWordFileExtension(doc.DocType) && emailModel.ConvertAttachmentsToPDF) //we need to convert to pdf first
                                                    {

                                                        tempFilePath = Helpers.DocumentHelper.ExportToPDF(tempFilePath, doc.DocName, doc.DocType, tempPath);
                                                    }

                                                    if (File.Exists(tempFilePath))
                                                    {

                                                        message.Attachments.Add(new Attachment(tempFilePath));



                                                    }
                                                    else
                                                    {
                                                        throw new FileNotFoundException();
                                                    }
                                                }
                                                else
                                                {
                                                    throw new FileNotFoundException();
                                                }
                                            }
                                        }
                                        catch (Exception e)
                                        {
                                            Handlers.ErrorHandler.LogError(e, $"NON-FATAL: Could not find document {currentDocName} to attach to auto-email for {matterId}"); //if document doesn't exist, don't worry, but still log it.
                                        }
                                    }
                                }
                            }
                            else
                            {

                                var docsRep = new DocumentsRepository(_context);
                                var matterDocs = docsRep.GetDocuments(matterId.Value);

                                foreach (var attachment in attachments)
                                {
                                    string currentDocName = "";
                                    try
                                    {
                                        var docs = matterDocs.Where(d => d.DocName.Trim().ToUpper().Contains(attachment.Trim().ToUpper()) && !d.IsDeleted).OrderByDescending(u => u.UpdatedDate).ToList();

                                        if (attachment?.ToUpper()?.Contains("VOI REPORT") != true && attachment?.ToUpper()?.Contains("IDYOU") != true)
                                        {
                                            docs = docs.Take(1).ToList();
                                        }
                                        foreach (var doc in docs)
                                        {
                                            currentDocName = doc.DocName;
                                            string filePath = Path.Combine(GlobalVars.GetGlobalTxtVar(DomainConstants.MatterDocumentsDirectory, _context),
                                                                matterId.ToString(), string.Format("{0}.{1}", doc.DocumentId.ToString(), string.IsNullOrEmpty(doc.DocType) ? "txt" : doc.DocType));
                                            if (File.Exists(filePath))
                                            {
                                                string tempPath = Path.Combine(Path.GetTempPath(), matterId.ToString());
                                                Directory.CreateDirectory(tempPath);

                                                string tempFilePath = tempPath + "\\" + doc.DocName + "." + (string.IsNullOrEmpty(doc.DocType) ? "txt" : doc.DocType);
                                                string origFilePath = tempFilePath; //in case we convert to pdf, so we can still delete the original as well.
                                                if (File.Exists(tempFilePath)) File.Delete(tempFilePath);
                                                File.Copy(filePath, tempFilePath);

                                                if (Helpers.DocumentHelper.IsWordFileExtension(doc.DocType) && emailModel.ConvertAttachmentsToPDF) //we need to convert to pdf first
                                                {

                                                    tempFilePath = Helpers.DocumentHelper.ExportToPDF(tempFilePath, doc.DocName, doc.DocType, tempPath);
                                                }

                                                if (File.Exists(tempFilePath))
                                                {

                                                    message.Attachments.Add(new Attachment(tempFilePath));



                                                }
                                                else
                                                {
                                                    throw new FileNotFoundException();
                                                }
                                            }
                                            else
                                            {
                                                throw new FileNotFoundException();
                                            }
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        Handlers.ErrorHandler.LogError(e, $"NON-FATAL: Could not find document {currentDocName} to attach to auto-email for {matterId}"); //if document doesn't exist, don't worry, but still log it.
                                    }
                                }

                            }
                        }



                        bool customSignatureApplied = false;

                        if (noReply)
                        {
                            body += "<p><span style='font-family:Calibri;font-size:10pt;color:Grey;'><i><u>Please note, this is an automated email and replies to this inbox are not monitored.</u></i></span></p>";
                        }

                        if (useCustomSignature && existingContext != null && comp != null)
                        {
                            if (!string.IsNullOrEmpty(customSignatureDirectory))
                            {
                                customSignatureDirectory = Path.Combine(GlobalVars.GetGlobalTxtVar("CustomSignatureDirectory", existingContext), customSignatureDirectory);
                            }
                            var officeDetails = new OfficeRepository(existingContext).GetOfficeView(comp.StateId, comp.LenderId);
                            var sigData = CreateCustomSignature(customSignatureDirectory, officeDetails, true);
                            if (sigData != null)
                            {


                                try
                                {
                                    AlternateView plainTextAV = AlternateView.CreateAlternateViewFromString(CommonMethods.ConvertToPlainText(mailBody), null, System.Net.Mime.MediaTypeNames.Text.Plain);

                                    message.AlternateViews.Add(plainTextAV);


                                    mailBody = mailBody += sigData.Signature;
                                    AlternateView avHtml = AlternateView.CreateAlternateViewFromString(mailBody, null, System.Net.Mime.MediaTypeNames.Text.Html);
                                    message.IsBodyHtml = true;
                                    int attachIndex = 0;

                                    if (sigData.Filepaths != null)
                                    {
                                        foreach (var inline in sigData.Filepaths)
                                        {

                                            if (System.IO.Path.GetExtension(inline)?.ToUpper() != ".XML")
                                            {
                                                //Attachment att = new Attachment(inline);
                                                //att.ContentDisposition.Inline = true;
                                                //message.Attachments.Add(att);
                                                //message.Attachments[attachIndex].ContentId = $"img-{attachIndex}";
                                                string mimeType = System.Web.MimeMapping.GetMimeMapping(inline);

                                                LinkedResource linked = new LinkedResource(inline, mimeType);
                                                linked.ContentId = $"img-{attachIndex}";
                                                avHtml.LinkedResources.Add(linked);
                                                attachIndex++;
                                            }
                                        }
                                    }
                                    message.AlternateViews.Add(avHtml);
                                    customSignatureApplied = true;
                                }
                                catch(Exception e)
                                {
                                    Handlers.ErrorHandler.LogError(e);
                                    customSignatureApplied = false;
                                    message?.AlternateViews?.Clear();
                                }

                            }
                        }
                        if(!customSignatureApplied)
                        {
                            message.Body = mailBody; /*+ "<hr><p style=\"font-size: 11pt; font-family: Calibri;\"><i>Replies to this inbox are not monitored.</i></p>";*/

                            message.IsBodyHtml = true;
                        }

                        if (matterId.HasValue)
                        {
                            List<string> couldNotAttach = new List<string>();
                            if (attachments != null)
                            {
                                couldNotAttach = attachments.Where(a => !message.Attachments.Any(x => x.Name.Contains(a) || a.Contains(x.Name))).ToList();
                            }
                            byte[] messageBytes = Encoding.ASCII
                                .GetBytes($"<head><title>{message.Subject}</title></head><body><b><u>MATTER ID:</u></b> {matterId.Value.ToString()}<br/><b><u>SUBJECT:</u></b> {message.Subject}<br/><b><u>FROM:</u></b> msa.national@msanational.com.au<b><br/><u>TO:</u></b> {message.To}<br/>"
                                + (message.CC.Any() ? $"<b><u>CC:</u></b> {message.CC}<br/>" : "")
                                + (message.Bcc.Any() ? $"<b><u>BCC:</u></b> {message.Bcc}<br/>" : "")

                                + (message.Attachments.Any() ? $"<b><u>ATTACHED:</u></b> {String.Join(", ", message.Attachments.Select(x => x.Name).ToList())}<br/>" : "")
                                + (couldNotAttach.Any() ? $"<span style='color: Red;'><b><u>COULD NOT ATTACH:</u></b></span> {String.Join(", ", couldNotAttach)}<br/>" : "")
                                + $"<b><u>TIME:</u> </b>{DateTime.Now.ToString()} {TimeZone.CurrentTimeZone.StandardName}<hr>" + (customSignatureApplied ? mailBody : message.Body) + "</body>");
                            _context.SaveChanges();
                            SaveEmailAsFile(messageBytes, (sendSms ? "Text Message Sent - " : "No-Reply Email - ") + message.Subject.ToSafeFileName() + ".html", GlobalVars.CurrentUser.UserId, matterId.Value, DocumentDisplayAreaEnum.Attachments, _context);
                        }

                        smtpClient.Send(message);

                        var loggingEmails = _context == null ? GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_LoggingEmails)?.ToUpper() == "TRUE" : GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_LoggingEmails, _context)?.ToUpper() == "TRUE";
                        if (loggingEmails)
                        {
                            GeneralCustomEntities.EmailLogView log = new GeneralCustomEntities.EmailLogView(message, emailModel)
                            {
                                ModelNoReply = noReply
                            };
                            if (testingEmails)
                            {
                                log.EmailTo = toAddress;
                                log.EmailCC = ccAddress;
                            }
                            LogEmailData(log);
                        }
                        if (message != null)
                        {
                            message.Attachments.Dispose();
                            message.Dispose();
                            attachments = null;
                            message = null;
                        }
                        return mailTo + (mailCC != null ? ", " + mailCC : "") + (mailBCC != null ? ", " + mailBCC : "");
                    }
                }
                catch (Exception ex)
                {

                    var loggingEmails = _context == null ? GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_LoggingEmails)?.ToUpper() == "TRUE" : GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_LoggingEmails, _context)?.ToUpper() == "TRUE";
                    if (loggingEmails)
                    {
                        if (_mail == null && message != null)
                        {
                            GeneralCustomEntities.EmailLogView log = new GeneralCustomEntities.EmailLogView(message, emailModel, ex)
                            {
                                ModelNoReply = noReply
                            };
                            LogEmailData(log);
                        }
                        else if (_mail != null)
                        {
                            GeneralCustomEntities.EmailLogView log = new GeneralCustomEntities.EmailLogView(_mail, emailModel, ex)
                            {
                                ModelNoReply = noReply
                            };
                            LogEmailData(log);

                        }
                        else
                        {
                            GeneralCustomEntities.EmailLogView log = new GeneralCustomEntities.EmailLogView(emailModel, ex)
                            {
                                ModelNoReply = noReply,
                                FromNoReply = noReply,
                                EmailSubject = subject
                            };
                            log.Body += body;
                            LogEmailData(log);
                        }
                    }

                    if (message != null)
                    {
                        message = null;
                    }

                    ReleaseOutlook();

                    Handlers.ErrorHandler.LogError(ex);
                    CheckAndSendEmailIfOutlookUnavailable(subject, body, emailModel);
                    return null;
                }
            }
            return null;
        }


        public static bool SaveEmailAsFile(byte[] buffer, string filename, int userId, int matterId, DocumentDisplayAreaEnum DocType, SlickContext context)
        {
            try
            {
               
                int lastdocId = context.Documents.OrderByDescending(d => d.DocumentId).FirstOrDefault().DocumentId;
                int pos = filename.LastIndexOf('.');
                DocumentInfo docInfo = new DocumentInfo
                {
                    ID = lastdocId++,
                    FileType = pos < 0 ? "txt" : filename.Substring(pos + 1),
                    FileName = filename,
                    ModDate = DateTime.Now,
                    Data = buffer,
                    MatterId = matterId
                };

                SaveDocumentDetails(docInfo, DocType, matterId, userId, context);
                
                return true;

            }
            catch (Exception)
            {
                return false;
            }
        }

        public static void SaveDocumentDetails(DocumentInfo doc, DocumentDisplayAreaEnum displayArea, int matterId, int userId, SlickContext context)
        {
          
            MatterDocument matterDoc = null;
            doc.DocumentDisplayAreaType = displayArea;
            doc.FileName = Helpers.DocumentHelper.TruncateDocumentName(doc.FileName, doc.FileType);
            if (new DocumentsRepository(context).SaveDocumentForUser(ref doc, ref matterDoc, userId) > 0)
            {
                Slick_Domain.Helpers.DocumentHelper.SaveDocumentToFileSystem(matterId, doc, context);
            }
            
        }





        public static void LogEmailData(GeneralCustomEntities.EmailLogView log)
        {

            if (_context == null)
            {
                using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadCommitted))
                {
                    var eRep = uow.GetEmailsRepositoryInstance();
                    eRep.LogEmail(log);
                    uow.Save();
                    uow.CommitTransaction();
                }
            }
            else
            {
                var eRep = new EmailsRepository(_context);
                eRep.LogEmail(log);
                _context.SaveChanges();
            }
        }
      

        public static void SendAccountsItemSplitEmail(string toEmail, AccountsCustomEntities.TrustTransactionItemView ttiView, MatterCustomEntities.MatterView mtView, decimal amountExpected, decimal amountReceived)
        {
            try
            {
                bool matterIsOverdrawn = false;
                decimal overDrawnAmount = (decimal)amountExpected - amountReceived;
                if (overDrawnAmount > 0) matterIsOverdrawn = true;
                //using (var uow = new UnitOfWork())
                //{

                    //var arep = uow.GetAccountsRepository();

                    //if (mtView.MatterTypeId == (int)Enums.MatterGroupTypeEnum.NewLoan)
                    //{
                        //var matterLedgerItems = arep.GetMatterLedgerViewForMatter(ttiView.MatterId);

                        //var funding = arep.GetMatterFinFunding(ttiView.MatterId, false);
                        //var disbs = arep.GetMatterFinDisbursements(ttiView.MatterId, false);



                        //overDrawnAmount = (funding.Sum(m => m.Amount) - diffInAmounts) - disbs.Sum(m => m.Amount);

                        //if (overDrawnAmount < 0)
                        //{
                        //    matterIsOverDrawn = true;
                        //}

                        //overDrawnAmount *= -1;
                    //}
                    //else
                    //{

                    //}
                //}

                CreateNewMailItem();

                if (_mail == null) return;

                _mail.Subject = $"ACTION REQUIRED {mtView.MatterDescription} : Matter {mtView.MatterId} : Ref - {mtView.LenderRefNo}";

                _mail.To = toEmail;


                string timeOfDay = "Morning";

                if (DateTime.Now.Hour > 12) timeOfDay = "Afternoon";
                
                
                string body =  $"<font face=\"Arial\" size=\"10pt\"> <p>Good {timeOfDay},</p>";
                       body += $"<p>Directions provided to accounts for the funding item <i>\"{ttiView.Description}\"</i> for Matter {ttiView.MatterId} were incorrect.</p>";
                       body += $"</p><p><font color=\"red\"><b>Funds expected</b> - {amountExpected.ToString("C")}</font></p>";
                       body += $"<p><font color=\"green\"><b>Funds received</b> - {amountReceived.ToString("C")}</font></p>";

                if (matterIsOverdrawn)
                {
                       body += $"</p><p>Currently, disbursements for this matter will be <b>overdrawn by {overDrawnAmount.ToString("C")}</b></p>";
                }
                else
                {
                       body += $"</p>";
                }

                body += $"<p>Please confirm additional funding is being sent.</p>";
                body += $"Alternatively, please remove / amend disbursements accordingly.</font>";
                

                BuildEmailBodyWithSignature(body);

                var testingEmails = GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmails)?.ToUpper() == "TRUE";
                if (testingEmails)
                {
                    var toAddress = _mail.To;
                    var ccAddress = _mail.CC;
                    _mail.To = GlobalVars.GetGlobalTxtVar(DomainConstants.GlobalVars_TestingEmail);
                    _mail.Subject += $" (Would have been sent to {toAddress})";
                }


                ReleaseOutlook();
          

                GC.Collect();
                GC.WaitForPendingFinalizers();

            }
            catch (Exception ex)
            {
                Handlers.ErrorHandler.LogError(ex);
                ReleaseOutlook();
            }
        }

        

        private static void CheckAndSendEmailIfOutlookUnavailable(string subject, string body, string toAddress)
        {
            try
            {
                if (_outlookUnavailable)
                {
                    SendEmail(subject, body, toAddress, true);
                }
            }
            catch (Exception ex)
            {
                Handlers.ErrorHandler.LogError(ex);
            }
        }

        public static string BuildSecondaryLoansEmailBody(int matterId, bool isOutstandings, UnitOfWork uow)
        {
            string body = "";
            DateTime? settlementDate = null;
            List<MatterCustomEntities.MatterSecondaryLoanView> accs = new List<MatterCustomEntities.MatterSecondaryLoanView>();
            
            accs = uow.Context.MatterSecondaryLoans.Where(m => m.MatterId == matterId && m.SecondaryLoanTypeId != (int)SecondaryLoanTypeEnum.Mortgage)
                .Select(x => new MatterCustomEntities.MatterSecondaryLoanView() { AccountNo = x.SecondaryLoanAccountNo, OFI = x.OFI, LoanType = x.SecondaryLoanType.SecondaryLoanTypeName })
                .ToList();
            settlementDate = uow.Context.Matters.FirstOrDefault(m => m.MatterId == matterId).SettlementSchedule?.SettlementDate;
            if (settlementDate.HasValue)
            {
                settlementDate += uow.Context.Matters.FirstOrDefault(m => m.MatterId == matterId).SettlementSchedule?.SettlementTime;
            }
            

            if (settlementDate.HasValue)
            {
                body += "<p style='font-family: Calibri; font-size: 11pt;'>Please provide payouts for the following loans:</p>";
            }
            else
            {
                if (isOutstandings)
                {
                    body += "<p style='font-family: Calibri; font-size: 11pt;'><u>Once settlement is booked,</u> our office will require payouts for the following loans:</p>";
                }
                else
                {
                    body += "<p style='font-family: Calibri; font-size: 11pt;'>Please provide payouts for the following loans:</p>";
                }
            }

            for (int i = 0; i < accs.Count; i++)
            {
                var thisAcc = accs.ElementAt(i);
                body += $"<p style='font-family: Calibri; font-size: 11pt;'><u>Payout {i + 1}</u></p>";
                body += "<p style='font-family: Calibri; font-size: 11pt;'><b>Statement Required to Support Payout: </b>Yes<br></br>";
                body += "<b>Bank: </b>" + thisAcc.OFI + "<br></br>";
                body += "<b>Account Number: </b>" + thisAcc.AccountNo + "<br></br>";
                body += "<b>Type: </b>" + thisAcc.LoanType + "<br></br>";
                if (!isOutstandings) body += "<b>Amount: </b>$ </p>";
            }
            return body;
        }


        public static string BuildShortfallEmailBody(int matterId)
        {
            try
            {

                MatterCustomEntities.MatterView mtView = null;
                EmailEntities.EmailModel model;

                string trustAccName = "";
                string trustAccNumber = "";
                string trustAccBank = "";
                string trustAccBSB = "";


                decimal totalShortfallAmount = 0.0m;

                using (var uow = new UnitOfWork())
                {
                    mtView = uow.GetMatterRepositoryInstance().GetMatterView(matterId);
                    model = uow.GetEmailsRepositoryInstance().BuildReadyToBookEmailModel(matterId, (int)Enums.WFComponentEnum.BookSettlement, null, null);
                    if (uow.Context.MatterLedgerItems
                        .Any(m => m.MatterId == matterId &&
                                    m.MatterLedgerItemStatusTypeId == (int)Enums.MatterLedgerItemStatusTypeEnum.Ready &&
                                    m.Description == "Shortfall"))
                    {
                        totalShortfallAmount = uow.Context.MatterLedgerItems
                        .Where(m => m.MatterId == matterId &&
                                    m.MatterLedgerItemStatusTypeId == (int)Enums.MatterLedgerItemStatusTypeEnum.Ready &&
                                    m.Description == "Shortfall")
                        .Sum(x => x.Amount);
                    }

                    if (mtView.StateId == (int)StateIdEnum.TAS)
                    {
                        var bankDetails = uow.Context.TrustAccounts.Where(i => i.BankStateId == (int)StateIdEnum.TAS).Select(x => new { BankName = x.Bank, AccountName = x.AccountName, BSB = x.BSB, AccountNumber = x.AccountNo }).FirstOrDefault();
                        trustAccBank = bankDetails.BankName;
                        trustAccName = bankDetails.AccountName;
                        trustAccBSB = bankDetails.BSB;
                        trustAccNumber = bankDetails.AccountNumber;
                    }
                    else
                    {
                        var bankDetails = uow.Context.TrustAccounts.Where(i => i.BankStateId == (int)StateIdEnum.NSW).Select(x => new { BankName = x.Bank, AccountName = x.AccountName, BSB = x.BSB, AccountNumber = x.AccountNo }).FirstOrDefault();
                        trustAccBank = bankDetails.BankName;
                        trustAccName = bankDetails.AccountName;
                        trustAccBSB = bankDetails.BSB;
                        trustAccNumber = bankDetails.AccountNumber;
                    }
                }


                string timeOfDay = GetTimeOfDay(DateTime.Now);

                string body  = $"<span style='font-size: 11pt; font-family: Calibri;'>Good {timeOfDay},</span><p/>";

                if (mtView.SettlementDate.HasValue)
                {
                    body += $"<span style='font-size: 11pt; font-family: Calibri;'>Unfortunately we have encountered a shortfall of funds for settlement due <b>{mtView.SettlementDate.Value.DayOfWeek.ToString()}, {mtView.SettlementDate.Value.ToString("dd/MM/yyyy")}.</b></span><p/>";
                }
                else
                {
                    body += $"<span style='font-size: 11pt; font-family: Calibri;'>Unfortunately we have encountered a shortfall for this matter.</span><br>";
                }

                body += $"<span style='font-size: 11pt; font-family: Calibri;'>The amount required to be deposited into our trust account is <span style = 'font-size: 12pt; font-family: Calibri; color: red;'><b>${totalShortfallAmount}</b>.</span></span><p/>"
                     + $"<span style='font-size: 11pt; font-family: Calibri;'>Please ensure that the deposit is for the exact amount and that a receipt of the transaction is provided. If the amount differs,"
                     +  " please notify our office immediately, as we will need to update our accounts team. </span><p/>";
                body += $"<span style='font-size: 11pt; font-family: Calibri;'><b>Trust account details for the deposit are as follows:</b></span><br>";
                body +=     $"<div style='font-size: 12pt; font-family: Calibri; text-indent: 2em;'><span style='color: #0070C0; font-size: 12pt; font-family: Calibri;'><b>BANK:</b></span> {trustAccBank}</div>";
                body +=     $"<div style='font-size: 12pt; font-family: Calibri; text-indent: 2em;'><span style='color: #0070C0; font-size: 12pt; font-family: Calibri;'><b>NAME:</b></span> {trustAccName}</div>";
                body +=     $"<div style='font-size: 12pt; font-family: Calibri; text-indent: 2em;'><span style='color: #0070C0; font-size: 12pt; font-family: Calibri;'><b>BSB:</b></span> {trustAccBSB}</div>";
                body +=     $"<div style='font-size: 12pt; font-family: Calibri; text-indent: 2em;'><span style='color: #0070C0; font-size: 12pt; font-family: Calibri;'><b>ACCOUNT NUMBER:</b></span> {trustAccNumber}</div>";
                body +=     $"<div style='font-size: 12pt; font-family: Calibri; text-indent: 2em;'><span style='color: #0070C0; font-size: 12pt; font-family: Calibri;'><b>REFERENCE:</b></span> {mtView.MatterId}</div><p/>";
                body += $"<span style='font-size: 11pt; font-family: Calibri;'>Please ensure that the transfer is completed as a <b><u>CASH DEPOSIT, AT A WESTPAC BRANCH</u></b> or <b><u>SAME DAY TRANSFER / OSKO PAYMENT</u></b> so that funds are received in time for settlement.</span><p/>";
                body += $"<span style='font-size: 11pt; font-family: Calibri;'><b><u>Unfortunately, should funds not arrive in time for settlement, we cannot accept responsibility for settlement cancellation.</u></b></span><p/>";
                body += $"<span style='font-size: 11pt; font-family: Calibri;'>Once this payment has been made, please provide an update so that we can update our accounts and settlement teams accordingly.</span><p/>";

                body += BuildGridForEmail(model);

                return body;
               

            }
            catch (Exception ex)
            {
                Handlers.ErrorHandler.LogError(ex);
                return null;
            }
        }
        public static string BuildAdditionalRedrawEmailBody(int matterId)
        {
            try
            {

                MatterCustomEntities.MatterView mtView = null;
                EmailEntities.EmailModel model;

                string trustAccName = "";
                string trustAccNumber = "";
                string trustAccBank = "";
                string trustAccBSB = "";

                List<decimal> redrawAmounts = new List<decimal>();

                decimal totalShortfallAmount = 0.0m;

                using (var uow = new UnitOfWork())
                {
                    mtView = uow.GetMatterRepositoryInstance().GetMatterView(matterId);
                    model = uow.GetEmailsRepositoryInstance().BuildReadyToBookEmailModel(matterId, (int)Enums.WFComponentEnum.BookSettlement, null, null);
                    redrawAmounts = uow.GetAccountsRepository().GetMatterFinFunding(matterId, false).Where(f => f.FundsTransferredTypeId == (int)FundsTransferredTypeEnum.UnPaid && f.Description.ToUpper().Contains("Additional Redraw from Lender".ToUpper())).Select(a => a.Amount).ToList();


                    if (mtView.StateId == (int)StateIdEnum.TAS)
                    {
                        var bankDetails = uow.Context.TrustAccounts.Where(i => i.BankStateId == (int)StateIdEnum.TAS).Select(x => new { BankName = x.Bank, AccountName = x.AccountName, BSB = x.BSB, AccountNumber = x.AccountNo }).FirstOrDefault();
                        trustAccBank = bankDetails.BankName;
                        trustAccName = bankDetails.AccountName;
                        trustAccBSB = bankDetails.BSB;
                        trustAccNumber = bankDetails.AccountNumber;
                    }
                    else
                    {
                        var bankDetails = uow.Context.TrustAccounts.Where(i => i.BankStateId == (int)StateIdEnum.NSW).Select(x => new { BankName = x.Bank, AccountName = x.AccountName, BSB = x.BSB, AccountNumber = x.AccountNo }).FirstOrDefault();
                        trustAccBank = bankDetails.BankName;
                        trustAccName = bankDetails.AccountName;
                        trustAccBSB = bankDetails.BSB;
                        trustAccNumber = bankDetails.AccountNumber;
                    }
                }


                string timeOfDay = GetTimeOfDay(DateTime.Now);

                string body = $"<span style='font-size: 11pt; font-family: Calibri;'><p>Hi team,</p></span>";

                if (mtView.SettlementDate.HasValue)
                {
                    if (mtView.SettlementDate.Value.Date == DateTime.Today.Date)
                    {
                        body += $"<span style='font-size: 11pt; font-family: Calibri;'><p>Matter certified to settle today: <b>{mtView.SettlementDate.Value.DayOfWeek.ToString()}, {mtView.SettlementDate.Value.ToString("dd/MM/yyyy")}.</b></p></span>";
                    }
                    else
                    {
                        body += $"<span style='font-size: 11pt; font-family: Calibri;'><p>Matter certified to settle on <b>{mtView.SettlementDate.Value.DayOfWeek.ToString()}, {mtView.SettlementDate.Value.ToString("dd/MM/yyyy")}.</b></p></span>";
                    }
                }


                if(redrawAmounts.Count() == 1)
                {
                    body += $"<span style='font-size: 11pt; font-family: Calibri;'><p>Please arrange for <u>{redrawAmounts.FirstOrDefault().ToString("c")}</u> to be transferred from client's nominated account into our trust account in readiness for settlement.</p></span>";
                }
                else
                {
                    body += $"<span style='font-size: 11pt; font-family: Calibri;'><p>Please arrange for the below amounts to be transferred from client's nominated account into our trust account in readiness for settlement.";

                    foreach (var amount in redrawAmounts)
                    {
                        body += $" - <u><b>{amount.ToString("c")}</b></u>";
                    }

                    body += "</p></span>";
                }


                body += $"<span style='font-size: 11pt; font-family: Calibri;'><p><b>{trustAccName}</b><br><b>{trustAccBank}</b><br>BSB - <b>{trustAccBSB}</b><br>Account Number - <b>{trustAccNumber}</b><br>Reference - <b>{matterId}</b></p></span>";

                body += $"<span style='font-size: 11pt; font-family: Calibri;'><p><i>**Please ensure no zeroes are added to the front of the trust account number noted above as this will cause delays in funds arriving in our account.</i></p></span>";

                if (mtView.LenderId == 41)
                {
                    body += $"<span style='font-size: 11pt; font-family: Calibri;'><p>Please refer to signed settlement authority form attached to APP case</p></span>";
                }



                return body;


            }
            catch (Exception ex)
            {
                Handlers.ErrorHandler.LogError(ex);
                return null;
            }
        }
        public static string BuildGridForEmail(EmailEntities.EmailModel model)
        {
            var sb = new StringBuilder();

            sb.Append($"<p><span style='font-size: 14pt; font-family: Calibri; color: #00B050;'><b>Loan Amount = {(model.BookingDetails.LoanAmount.ToString("C"))}</b></span></p>");

            //sb.Append("<table width='650'>");
            //sb.Append($"<tr><td style='font-size: 11pt; font-family: Calibri;' width='450';><u>Total Amount</u></td>");
            //sb.Append($"<td style='font-size: 11pt; font-family: Calibri;'width='200'; align='right'><b>{(model.BookingDetails.LoanAmount.ToString("C"))}</b></td></tr>");
            //sb.Append("</table><br/>");

            //Lender Items
            if (model.BookingDetails.LenderRetainedItems.Any())
            {
                sb.Append("<table style='margin - left:10;' width='650'>");
                sb.Append("<tr><td width='30'>&nbsp;</td><td colspan=2 style='font-size: 12pt; font-family: Calibri; color: #0070C0;'><b>Less Amounts Retained by Lender</b></th></tr>");
                foreach (var lri in model.BookingDetails.LenderRetainedItems)
                {
                    sb.Append($"<tr><td>&nbsp;</td><td style='font-size: 12pt; font-family: Calibri;' width=370>{lri.Description}</td>");
                    sb.Append($"<td style='font-size: 12pt; font-family: Calibri;' width='250' align='right'>{(lri.Amount.Value.ToString("C"))}</td></tr>");
                }
                sb.Append($"<tr><td>&nbsp;</td><td style='font-size: 12pt; font-family: Calibri;' align='right'><b>Total = </b></td>");
                sb.Append($"<td style='font-size: 12pt; font-family: Calibri;' align=right><b>{(model.BookingDetails.LenderRetainedItems.Sum(x => x.Amount.Value).ToString("C"))}</b></td></tr>");
                sb.Append("</table><br/>");
            }

            //Fund Items
            if (model.BookingDetails.ExpectedFundsItems.Any())
            {
                sb.Append("<table style='margin-left:10;' width='650'>");
                sb.Append("<tr><td width='30'>&nbsp;</td><td colspan=2 style='font-size: 12pt; font-family: Calibri; color: #0070C0;'><b>Funding Amounts</b></th></tr>");

                foreach (var lri in model.BookingDetails.ExpectedFundsItems)
                {
                    sb.Append($"<tr><td>&nbsp;</td><td style='font-size: 12pt; font-family: Calibri;' width='455'>{lri.Description}</td>");
                    sb.Append($"<td style='font-size: 12pt; font-family: Calibri;' width='165' align='right'>{(lri.Amount.Value.ToString("C"))}</td></tr>");
                }
                sb.Append($"<tr><td>&nbsp;</td><td style='font-size: 12pt; font-family: Calibri;' align='right'><b>Funding Amounts Total =</b></td>");
                sb.Append($"<td style='font-size: 12pt; font-family: Calibri;' align='right'><b>{(model.BookingDetails.ExpectedFundsItems.Sum(x => x.Amount.Value).ToString("C"))}</b></td></tr>");
                sb.Append("</table><br/>");
            }

            //Fee Deductions
            sb.Append("<table style='margin-left:10;' width='650'>");
            sb.Append("<tr><td width='30'>&nbsp;</td><td colspan=4 style='font-size: 12pt; font-family: Calibri; color: #0070C0'><b>Deductions Amounts</b></th></tr>");
            int liIndex = 0;
            foreach (var li in model.LedgerItems)
            {
                liIndex++;
                sb.Append($"<tr><td width='30'>&nbsp;</td><td width='25' style='font-size: 12pt; font-family: Calibri;'>{liIndex}.</td>");
                sb.Append($"<td width='250' style='font-size: 12pt; font-family: Calibri;'>{li.Description}</td>");
                sb.Append($"<td style='font-size: 12pt; font-family: Calibri;' width='200'>{li.PayableTo}</td>");
                sb.Append($"<td style='font-size: 12pt; font-family: Calibri;' align='right' width='175'>{li.AmountString}</td></tr>");
            }
            sb.Append($"<tr><td colspan='4' style='font-size: 12pt; font-family: Calibri;' align='right'><b>Deductions Total =</b></td>");
            sb.Append($"<td style='font-size: 12pt; font-family: Calibri;' align='right'><b>{model.LedgerItemsTotal}</b></td></tr>");
            sb.Append("</table><br/>");


            //Total
            var total = (model.BookingDetails.ExpectedFundsItems.Any() ? model.BookingDetails.ExpectedFundsItems.Sum(x => x.Amount.Value) : model.BookingDetails.LoanAmount)
                - model.LedgerItems.Sum(x => x.Amount.Value);

            sb.Append("<table width='650' style='margin-left:10;'>");
            sb.Append($"<tr><td width='30'>&nbsp;</td><td style='font-size: 16pt; font-family: Calibri; color: #00B050;' width='455' align='right'><b>Available Funds =</b></td>");
            sb.Append($"<td style='font-size: 16pt; font-family: Calibri; color: #00B050;' width='165' align='right'><b>{(total.ToString("C"))}</b></td></tr>");
            sb.Append("</table><br/>");


            //if (model.IsPurchase && model.BookingDetails.IsPaper)
            //{
            //    sb.Append($"<p><span style='font-size: 20px; font-family: Calibri;'><b>Note</b></span></p>");
            //    sb.Append($"<p><span style='font-size: 11pt; font-family: Calibri;'>The first {model.BookingDetails.NumberOfFreeCheques} bank cheques directed by you are free.");
            //    if (model.BookingDetails.BankChequeFee.HasValue)
            //    {
            //        sb.Append($" Each subsequent cheque is {model.BookingDetails.BankChequeFee.Value.ToString("c")}.</span></p>");
            //    }
            //    sb.Append($"<p><span style='font-size: 11pt; font-family: Calibri;'>The Direction to Pay must be in writing sent to us by fax or email <b>4.00 pm two (2) days prior to settlement.</b></span>");
            //    sb.Append($"<p><span style='font-size: 11pt; font-family: Calibri;'>Any shortfalls are to be organised with your client directly.  Authority to debit your client's account is not available.</span></p>");
            //}

            return sb.ToString();
        }


        private static void CheckAndSendEmailIfOutlookUnavailable(string subject, string body, EmailEntities.EmailPlaceHolderModel emailModel)
        {
            try
            {
                if (_outlookUnavailable)
                {
                    SendEmail(subject, body, ref emailModel);
                    return;
                }
            }
            catch (Exception ex)
            {
                Handlers.ErrorHandler.LogError(ex);
            }
        }

            #endregion
    }
}
