using Slick_Domain.Common;
using System.IO;
using Telerik.Windows.Documents.Flow.Model;
using Telerik.Windows.Documents.Spreadsheet.FormatProviders.OpenXml.Xlsx;
using Telerik.Windows.Documents.Spreadsheet.Model;
using FlowFormat = Telerik.Windows.Documents.Flow.FormatProviders.Pdf;
using XlsFormat = Telerik.Windows.Documents.Spreadsheet.FormatProviders.Pdf;
using System.Collections.Generic;
using Telerik.Windows.Documents.Flow.Model.Styles;
using Telerik.Windows.Documents.Spreadsheet.Theming;
using Codaxy.WkHtmlToPdf;

namespace Slick_Domain.Services
{
    /// <summary>
    /// Document Export Class. 
    /// </summary>
    public static class DocumentExport
    {
        /// <summary>
        /// The document name to import before transformation.
        /// </summary>
        private static string _docNameToImport;
        /// <summary>
        /// The document name to export.
        /// </summary>
        private static string _docNameToExport;
        /// <summary>
        /// The currrent document type to be converting from
        /// </summary>
        private static string _docType;
        /// <summary>
        /// The fully qualified file path to the current file.
        /// </summary>
        private static string _pathIn;
        /// <summary>
        /// The fully qualified path to the output file
        /// </summary>
        private static string _pathOut;
        /// <summary>
        /// Export a document to pdf.
        /// </summary>
        /// <param name="docNameIn">The current name of the document coming in to be transformed.</param>
        /// <param name="docNameOut">The name of the new file to be exported.</param>
        /// <param name="docType">The current type of the document.</param>
        /// <param name="pathIn">The path to the current file.</param>
        /// <param name="pathOut">The path to the new file to be created.</param>
        /// <returns>The full path of the new file.</returns>
        public static string ExportToPDF(string docNameIn, string docNameOut, string docType, string pathIn, string pathOut = null, bool overrideFonts = false)
        {
            string newPath = null;
            _docNameToExport = docNameOut.ToSafeFileName();
            _docNameToImport = docNameIn;
            _docType = docType;
            _pathIn = pathIn;
            _pathOut = string.IsNullOrEmpty(pathOut) ? pathIn : pathOut;

            switch (docType.ToLower())
            {
                case "html":
                    return ExportHtmlToPdf();
                case "doc":
                case "docx":
                    return ExportWordToPDF(); //NOT Formatting correctly
                    
                case "rtf":
                    return ExportRtfToPDF(overrideFonts);

                case "xls":
                case "xlsx":
                    return ExportExcelToPDF();

                case "txt":
                case null:
                    docType = "txt";
                    return ExportTxtToPDF();

                default: // Unknown Doc type - can't transform.
                    break;
            }
            return newPath;
        }
        /// <summary>
        /// Convert a rich text file to text and then pdf through <see cref="ExportFlowToPDF(RadFlowDocument)"/>.
        /// </summary>
        /// <returns>The full path of the file.</returns>
        private static string ExportRtfToPDF(bool overrideFonts = false)
        {
            var fileProvider = new Telerik.Windows.Documents.Flow.FormatProviders.Rtf.RtfFormatProvider();
            
            RadFlowDocument document = null;
            using (Stream input = File.OpenRead(Path.Combine(_pathIn, $"{_docNameToImport}.{_docType}")))
            {
                document = fileProvider.Import(input);
                if (overrideFonts)
                {
                    var currTheme = document.Theme;
                    var fontScheme = currTheme.FontScheme;
                    var newFontSceme = new Telerik.Windows.Documents.Spreadsheet.Theming.ThemeFontScheme("Courier", "Courier", "Courier", "Courier", "Courier", "Courier", "Courier");

                    document.Theme = new DocumentTheme("PlainText", currTheme.ColorScheme, newFontSceme);
                    var test = document.StyleRepository;


                    var newStyles = new List<Style>();
                    foreach (var style in test.Styles)
                    {
                        style.CharacterProperties.FontFamily.LocalValue = new ThemableFontFamily("Courier");
                        style.CharacterProperties.FontFamily.SetValueAsObject(new ThemableFontFamily("Courier"));
                       newStyles.Add(style);
                    }

                    document.StyleRepository.Clear();

                    foreach (var newStyle in newStyles)
                    {
                        document.StyleRepository.Add(newStyle);
                    }
                    document.DefaultStyle.CharacterProperties.FontFamily.LocalValue = new ThemableFontFamily("Courier");
                    document.DefaultStyle.CharacterProperties.FontFamily.SetValueAsObject(new ThemableFontFamily("Courier"));
                    
                }
            }

            return ExportFlowToPDF(document, overrideFonts);
        }

        /// <summary>
        /// Exports Html to PDF using Wk HTML
        /// </summary>
        /// <returns>The path of the new file name</returns>
        private static string ExportHtmlToPdf()
        {

            //var fileProvider = new Telerik.Windows.Documents.Flow.FormatProviders.Txt.TxtFormatProvider();
            //RadFlowDocument document = null;
            /*
            using (Stream input = File.OpenRead(Path.Combine(_pathIn, $"{_docNameToImport}.{_docType}")))
            {
                document = fileProvider.Import(input);
            }*/
            var outputFilePath = Path.Combine(_pathOut, $"{_docNameToExport}.pdf");
            //extraParam.Key, extraParam.Valu
            PdfConvert.ConvertHtmlToPdf(
                new PdfDocument
                {
                    Html = File.ReadAllText(Path.Combine(_pathIn, $"{_docNameToImport}.{_docType}")),
                    ExtraParams = new Dictionary<string,string>() { { "print-media-type", "" }, { "image-quality", "100" }, { "load-error-handling", "ignore" } }
                    
                },
                new PdfConvertEnvironment
                {
                    WkHtmlToPdfPath = @"D:\Projects\Certification\wkhtmltox\bin\wkhtmltopdf.exe",
                    Timeout = 100000
                },
                new PdfOutput
                {
                    OutputFilePath = outputFilePath
                }

                );
            return outputFilePath;
        }

        /// <summary>
        /// Export text file to pdf using <see cref="ExportFlowToPDF(RadFlowDocument)"/>.
        /// </summary>
        /// <returns>The fully qualified path of the new filename.</returns>
        private static string ExportTxtToPDF()
        {
            var fileProvider = new Telerik.Windows.Documents.Flow.FormatProviders.Txt.TxtFormatProvider();
            RadFlowDocument document = null;
            using (Stream input = File.OpenRead(Path.Combine(_pathIn, $"{_docNameToImport}.{_docType}")))
            {
                document = fileProvider.Import(input);
            }

            return ExportFlowToPDF(document);
        }


        private static string ExportWordToPDF()
        {
            // DOES Not appear to be formatting correctly - using SaveAs PDF instead.
            var fileProvider = new Telerik.Windows.Documents.Flow.FormatProviders.Docx.DocxFormatProvider();
            RadFlowDocument document = null;
            string docName = Path.Combine(_pathIn, $"{_docNameToImport}.{_docType}");
            using (Stream input = File.OpenRead(docName)) {
                document = fileProvider.Import(input);
            }

            return ExportFlowToPDF(document);
        }
        /// <summary>
        /// Converts and exports an excel file to pdf.
        /// </summary>
        /// <returns>The fully qualfied path of the new file.</returns>
        private static string ExportExcelToPDF()
        {
            var fileProvider = new XlsxFormatProvider();
            Workbook workbook = null;
            using (Stream input = File.OpenRead(Path.Combine(_pathIn, $"{_docNameToImport}.{_docType}")))
            {
                workbook = fileProvider.Import(input);
            }

            var pathOut = Path.Combine(_pathOut, $"{_docNameToExport}.pdf");
            var pdfProvider = new XlsFormat.PdfFormatProvider();
            using (Stream output = File.OpenWrite(pathOut))
            {
                pdfProvider.Export(workbook, output);
            }
            return pathOut;
        }
        /// <summary>
        /// Converts and Exports a Rad Flow Document to pdf.
        /// </summary>
        /// <param name="document">The document to be converted.</param>
        /// <returns>The fully qualified path of new file.</returns>
        private static string ExportFlowToPDF(RadFlowDocument document, bool overrideFonts = false)
        {
            var pdfProvider = new FlowFormat.PdfFormatProvider();
            var pathOut = Path.Combine(_pathOut, $"{_docNameToExport}.pdf");
            using (Stream output = File.OpenWrite(pathOut))
            {
                
                pdfProvider.Export(document, output);
            }
            return pathOut;
        }

        /// <summary>
        /// Converts a document to pdf by Stream.
        /// </summary>
        /// <param name="docNameIn"></param>
        /// <param name="docType"></param>
        /// <param name="pathIn"></param>
        /// <returns></returns>
        public static byte[] StreamToPDF(string docNameIn, string docType, string pathIn)
        {
            string docName = Path.Combine(pathIn, $"{docNameIn}.{docType}");
            var pdfProvider = new FlowFormat.PdfFormatProvider();
            RadFlowDocument document = null;

            switch (docType.ToLower())
            {
                case "doc":
                case "docx":
                    var fileProviderDoc = new Telerik.Windows.Documents.Flow.FormatProviders.Docx.DocxFormatProvider();
              
                    using (Stream input = File.OpenRead(docName))
                    {
                        document = fileProviderDoc.Import(input);
                        return pdfProvider.Export(document);
                    }

                case "rtf":
                    var fileProviderRTF = new Telerik.Windows.Documents.Flow.FormatProviders.Rtf.RtfFormatProvider();
                    using (Stream input = File.OpenRead(docName))
                        document = fileProviderRTF.Import(input);
                    return pdfProvider.Export(document);

                case "xls":
                case "xlsx":
                    var fileProviderXLS = new XlsxFormatProvider();
                    var pdfProviderXLS = new XlsFormat.PdfFormatProvider();
                    Workbook workbook = null;
                    using (Stream input = File.OpenRead(docName))
                        workbook = fileProviderXLS.Import(input);

                    return pdfProviderXLS.Export(workbook);

                case "txt":
                case null:
                    var fileProviderTXT = new Telerik.Windows.Documents.Flow.FormatProviders.Txt.TxtFormatProvider();
                    using (Stream input = File.OpenRead(docName))
                        document = fileProviderTXT.Import(input);
                    return pdfProvider.Export(document);

                case "pdf":
                    return File.ReadAllBytes(docName);

                default: // Unknown Doc type - can't transform.
                    return null;
            }
        }



    }
}
