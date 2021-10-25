using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RestSharp;
using System.IO.Compression;

namespace Slick_Domain.Services
{
    public class SmartVidReportingService
    {
        public static string smartVidDirectory = @"P:\RPA\Slick Reporting\SmartVids\";
        //string lenderkey = "ac91a34d8598a069aadd54cd8a2bf0e4";
        DateTime startDate = DateTime.Today.AddDays(-7);
        DateTime endDate = DateTime.Today.AddDays(-1);
        

        public static void DownloadSmartVidReports(DateTime startDate, DateTime endDate, string lenderkey, string lenderName)
        {
            var smartVidUrl = @"https://sydney.personalisedvideolab.com/admin/report/writer/[%22vhReport%22,%22wvReport%22,%22uwReport%22,%22enReport%22,%22pdReport%22,%22plReport%22,%22ipReport%22,%22vcReport%22,%22bcReport%22,%22ecReport%22]/";
            smartVidUrl = smartVidUrl + lenderkey + "/1035/";
            string startDateString = startDate.ToString("dd-MM-yyyy");
            string endDateString = endDate.ToString("dd-MM-yyyy");
            smartVidUrl = smartVidUrl + startDateString + "/" + endDateString + "/true?";
            var client = new RestClient(smartVidUrl);
            client.Timeout = 60000;
            var request = new RestRequest(Method.GET);
            var reportsResponse = client.Execute(request);
            string saveLocation = smartVidDirectory  + lenderName + @" Downloaded Reports.zip";
            string extractPath = smartVidDirectory + lenderName + @" Downloaded Reports\";
            string pastVideoPath = smartVidDirectory + @"Past Downloaded Reports";
            DirectoryInfo di = new DirectoryInfo(extractPath);
            foreach (FileInfo file in di.GetFiles())
            {
                file.Delete();
            }
            if (reportsResponse.StatusCode == System.Net.HttpStatusCode.OK)
            {
                //var downloadedDocs = reportsResponse.ContentType;
                File.WriteAllBytes(saveLocation, reportsResponse.RawBytes);

            }
            ZipFile.ExtractToDirectory(saveLocation, extractPath);
            //ZipFile.ExtractToDirectory(saveLocation, pastVideoPath);
            foreach (var file in Directory.GetFiles(extractPath))
            {
                File.Copy(file, Path.Combine(pastVideoPath, Path.GetFileName(file)), true);
            }
            

        }


    }
}
