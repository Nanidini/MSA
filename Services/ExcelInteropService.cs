using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Excel = Microsoft.Office.Interop.Excel;
using System.Data;
using Slick_Domain.Entities;
using Slick_Domain.Enums;
using static Slick_Domain.Entities.ExcelReportTableCustomEntities;
using System.Windows.Controls;
using System.Runtime.InteropServices;

namespace Slick_Domain.Services
{
    public class ExcelInteropService
    {
        public static DataTable ConvertToDataTable<T>(List<T> models)
        {
            // creating a data table instance and typed it as our incoming model   
            // as I make it generic, if you want, you can make it the model typed you want.  
            if(models == null || !models.Any())
            {
                return null;
            }
            var type = typeof(T).Name;
            DataTable dataTable = new DataTable(type);
            var Props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(x => (NotVisibleAttribute)Attribute.GetCustomAttribute(x, typeof(NotVisibleAttribute)) == null);
            // Loop through all the properties              
            // Adding Column name to our datatable  
            foreach (var prop in Props)
            {
                var headerAttribute = (HeaderAttribute)Attribute.GetCustomAttribute(prop, typeof(HeaderAttribute));
                dataTable.Columns.Add(headerAttribute?.Header ?? prop.Name);
            }
            // Adding Row and its value to our dataTable  
            foreach (T item in models)
            {
                var values = new List<object>();
                foreach (var prop in Props)
                {
                    values.Add(prop.GetValue(item, null));
                }
                // Finally add value to datatable
                dataTable.Rows.Add(values.ToArray());
            }
            return dataTable;
        }

        private static Excel.Workbook _activeWorkbook;
        public static void GenerateExcel<T>(List<List<T>> excelSheetData, bool makeDataTable, string path, List<string> sheetName, bool autoFit = true, int maxColumnWidth = 50)
        {
            Excel.Application excelApp = new Excel.Application();
            //excelApp.Visible = true;
            _activeWorkbook = excelApp.Workbooks.Add();
            int sheetCount = 0;
            foreach (var reportData in excelSheetData)
            {
               
                GenerateExcelSheets(reportData, makeDataTable, sheetName[sheetCount], autoFit, maxColumnWidth, sheetCount);
                sheetCount++;
            }

            ExcelInteropService.SaveCloseWorkbook(excelApp, _activeWorkbook, path);
        }

        public static void GenerateExcelExistingWb<T>(List<List<T>> excelSheetData, string wbPath, bool makeDataTable, string path, List<string> sheetName, bool autoFit = true, int maxColumnWidth = 50,
                                                        int sheetCount = 0, int outputRow = 1, int outputColumn = 1)
        {
            Excel.Application excelApp = new Excel.Application();
            //excelApp.Visible = true;
            _activeWorkbook = excelApp.Workbooks.Open(wbPath);
            sheetCount = 0;
            foreach (var reportData in excelSheetData)
            {
                GenerateExcelSheets(reportData, makeDataTable, "", autoFit, maxColumnWidth, sheetCount, excelApp.Worksheets[sheetCount+1], outputRow, outputColumn);
                sheetCount++;
            }

            ExcelInteropService.SaveCloseWorkbook(excelApp, _activeWorkbook, path);
        }

        public static void GenerateExcelSheets<T>(List<T> reportData, bool makeDataTable = false, string sheetName = "Sheet", bool autoFit = true, int maxColumnWidth = 50, int sheetCount = 0, 
                                                    Excel.Worksheet xlWorksheet = null, int outputRow = 0, int outputColumn = 0)
        {
            if (_activeWorkbook == null)
            {
                //instantiate active workbook somehow?
                Excel.Application excelApp = new Excel.Application();
                excelApp.Visible = true;
                _activeWorkbook = excelApp.Workbooks.Add();
            }
            if (reportData.Count == 0)
            {
                if (sheetCount > 0)
                {
                    Excel.Worksheet excelWorkSheet = _activeWorkbook.Sheets.Add(After: _activeWorkbook.Sheets[_activeWorkbook.Sheets.Count]);
                }
                xlWorksheet = _activeWorkbook.Sheets[_activeWorkbook.Worksheets.Count];
                Excel.Range xlRange = xlWorksheet.UsedRange;
            }
            else
            {
                var dataTable = ConvertToDataTable(reportData);
                DataSet dataSet = new DataSet();
                dataSet.Tables.Add(dataTable);
                if (sheetCount > 0)
                {
                    Excel.Worksheet excelWorkSheet = _activeWorkbook.Sheets.Add(After: _activeWorkbook.Sheets[_activeWorkbook.Sheets.Count]);
                }
                if (xlWorksheet == null)
                {
                    xlWorksheet = _activeWorkbook.Sheets[_activeWorkbook.Worksheets.Count];
                }
                Excel.Range xlRange = xlWorksheet.UsedRange;
                // create a excel app along side with workbook and worksheet and give a name to it  

                foreach (DataTable table in dataSet.Tables)
                {
                    //Add a new worksheet to workbook with the Datatable name  
                    //Excel.Worksheet excelWorkSheet = excelWorkBook.Sheets.Add();
                    if (sheetName == "Sheet")
                    {
                        xlWorksheet.Name = table.TableName + "(" + _activeWorkbook.Worksheets.Count + ")";
                    }
                    else if (sheetName == "")
                    {

                    }
                    else
                    {
                        bool found = false;
                        foreach (Excel.Worksheet sheet in _activeWorkbook.Sheets)
                        {
                            if (sheet.Name == sheetName)
                            {
                                found = true;
                                break;
                            }
                        }
                        if (found)
                        {
                            xlWorksheet.Name = sheetName + "(" + _activeWorkbook.Worksheets.Count + ")";
                        }
                        else
                        {
                            xlWorksheet.Name = sheetName;
                        }
                    }
                    //xlWorksheet.Name = table.TableName;
                    var type = typeof(T).Name;
                    var Props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(x => (NotVisibleAttribute)Attribute.GetCustomAttribute(x, typeof(NotVisibleAttribute)) == null);
                    // add all the columns 
                    for (int i = 1; i < table.Columns.Count + 1; i++)
                    {
                        if (outputRow == 0)
                        {
                            outputRow = 1;
                        }
                        if (outputColumn == 0)
                        {
                            outputColumn = 1;
                        }
                        xlWorksheet.Cells[outputRow, i + outputColumn-1] = table.Columns[i - 1].ColumnName;
                    }
                    // add all the rows  
                    for (int j = 0; j < table.Rows.Count; j++)
                    {
                        for (int k = 0; k < table.Columns.Count; k++)
                        {
                            xlWorksheet.Cells[j + 1 + outputRow, k + outputColumn] = table.Rows[j].ItemArray[k].ToString();
                        }
                    }
                    //Excel.Range headerRng = ExcelInteropService.HeaderRange(xlWorksheet);
                    //headerRng.Interior.Color = Excel.XlRgbColor.rgbLightGray;
                    //headerRng.Font.Size = 14;
                    //headerRng.Font.Bold = true;
                    //headerRng.Cells.AutoFilter();
                    for (int i = 1; i < table.Columns.Count + 1; i++)
                    {
                        if (autoFit)
                        {
                            xlWorksheet.Columns[i+outputColumn-1].AutoFit();
                            if (xlWorksheet.Columns[i+outputColumn-1].ColumnWidth > maxColumnWidth) xlWorksheet.Columns[i+outputColumn-1].ColumnWidth = maxColumnWidth;
                        }
                    }
                    int y = 1;
                    foreach (var prop in Props)
                    {
                        var dateFormatAttribute = (DateFormatAttribute)Attribute.GetCustomAttribute(prop, typeof(DateFormatAttribute));
                        if (dateFormatAttribute != null) xlWorksheet.Columns[y+outputColumn-1].EntireColumn.NumberFormat = dateFormatAttribute.dateFormat;
                        y++;
                    }
                    if (makeDataTable)
                    {
                        SetTable(xlWorksheet, xlWorksheet.UsedRange, "Table " + xlWorksheet.Name, "TableStyleMedium2");
                    }
                }
            }            
        }
        public static Excel.Range HeaderRange(Excel.Worksheet ws)
        {
            ws.Cells[1, 1].EntireRow.Font.Bold = true;
            Excel.Range last = ws.Cells.SpecialCells(Excel.XlCellType.xlCellTypeLastCell, Type.Missing);
            Excel.Range range = ws.get_Range("A1", last);
            int lastUsedColumn = last.Column;
            Excel.Range rng = ws.Range[ws.Cells[1, 1], ws.Cells[1, lastUsedColumn]];
            return rng;
        }
        public static void SaveCloseWorkbook(Excel.Application excel, Excel.Workbook wb, string savePath)
        {
            excel.DisplayAlerts = false;
            wb.SaveAs(savePath, Type.Missing, Type.Missing, Type.Missing, Type.Missing, Type.Missing, Excel.XlSaveAsAccessMode.xlNoChange, Type.Missing, Type.Missing, Type.Missing,
              Type.Missing, Type.Missing);
            excel.DisplayAlerts = true;
            wb.Close();
            excel.Quit();
            System.Runtime.InteropServices.Marshal.ReleaseComObject(excel);
        }
        public static void SetTable(Excel.Worksheet ws, Excel.Range tRng, string tableName = "Table1", string tableStyle = "TableStyleMedium2")
        {
            ws.ListObjects.Add(Excel.XlListObjectSourceType.xlSrcRange, tRng,
                Type.Missing, Excel.XlYesNoGuess.xlYes, Type.Missing).Name = tableName;
            ws.ListObjects[tableName].TableStyle = tableStyle;
        }
        public static List<ExcelReportTableCustomEntities.MatterWfComponentCompletion> GetWfComponentCompletion(List<int> compIds)
        {
            List<ExcelReportTableCustomEntities.MatterWfComponentCompletion> matterWfCompletedDates = new List<MatterWfComponentCompletion>();
            using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadUncommitted))
            {
                matterWfCompletedDates = uow.Context.MatterWFComponents.Where(m => compIds.Contains(m.WFComponentId))
                    .Select(x => new
                    {
                        MatterId = x.MatterId,
                        MatterWfCompletedDate = x.MatterEvents.OrderByDescending(o => o.EventDate).Where(e => e.MatterEventTypeId == (int)MatterEventTypeList.MilestoneComplete && e.MatterEventStatusTypeId == (int)MatterEventStatusTypeEnum.Good).Select(d => d.EventDate).FirstOrDefault()
                    }).ToList().Select
                    (
                        x => new ExcelReportTableCustomEntities.MatterWfComponentCompletion()
                        {
                            MatterId = x.MatterId,
                            MatterWfCompletedDate = Convert.ToDouble(x.MatterWfCompletedDate)
                        }
                    )
                    .ToList();
            }
            return matterWfCompletedDates;
        }
        public static void CreatePivotTable<T>(Excel.Workbook wb, Excel.Worksheet ws, Excel.Range pvtRange, Excel.Range dataRange, List<PivotTableType> PivotTableFields)
        {
            //excel.Visible = true;
            //Excel.Worksheet wss = (Excel.Worksheet)wb.Worksheets.get_Item(1);
            Excel.Range oRange = ws.UsedRange;
            Excel.PivotCache oPivotCache = (Excel.PivotCache)wb.PivotCaches().Add(Excel.XlPivotTableSourceType.xlDatabase, oRange);
            Excel.PivotCaches pch = wb.PivotCaches();
            pch.Add(Excel.XlPivotTableSourceType.xlDatabase, pvtRange).CreatePivotTable(pvtRange, "PivTbl_1", Type.Missing, Type.Missing);
            Excel.PivotTable pvt = ws.PivotTables("PivTbl_1") as Excel.PivotTable;
            pvt.Format(Excel.XlPivotFormatType.xlReport2);
            pvt.InGridDropZones = false;
            pvt.SmallGrid = false;
            pvt.ShowTableStyleRowStripes = true;
            pvt.TableStyle2 = "PivotStyleLight2";
            pvt.ShowDrillIndicators = false;
            foreach (var field in PivotTableFields)
            {
                Excel.PivotField fld = ((Excel.PivotField)pvt.PivotFields(field.FieldName));
                if (field.FieldName == "DataPivotField")
                {
                    fld = ((Excel.PivotField)pvt.DataPivotField);
                    fld.Orientation = field.Orientation;
                }
                else
                {
                    fld.Orientation = field.Orientation;
                    fld.Function = field.Function;
                    fld.Name = field.StringName;
                    fld.NumberFormat = field.NumberFormat;
                }
            }
        }
        public static void CloseExcelProcesses()
        {
            var processes = System.Diagnostics.Process.GetProcessesByName("EXCEL");
            if (processes != null)
            {
                foreach (var process in processes)
                {
                    try
                    {
                        process.Kill();
                    }
                    catch (Exception)
                    { }
                }
            }
        }
        public static System.Data.DataTable CreateDataTableFromXml(string XmlFile)
        {

            System.Data.DataTable Dt = new System.Data.DataTable();
            try
            {
                DataSet ds = new DataSet();
                ds.ReadXml(XmlFile, 0);
                Dt.Load(ds.CreateDataReader());

            }
            catch (Exception ex)
            {

            }
            return Dt;
        }
        public static void ExportDataTableToExcel(System.Data.DataTable table, string Xlfile)
        {

            Excel.Application excel = new Microsoft.Office.Interop.Excel.Application();
            Excel.Workbook book = excel.Application.Workbooks.Add(Type.Missing);
            excel.Visible = false;
            excel.DisplayAlerts = false;
            Excel.Worksheet excelWorkSheet = (Microsoft.Office.Interop.Excel.Worksheet)book.ActiveSheet;
            if (table.TableName != "")
            {
                excelWorkSheet.Name = table.TableName;
            }
            
            int progress = table.Columns.Count;
            for (int i = 1; i < table.Columns.Count + 1; i++) // Creating Header Column In Excel  
            {
                excelWorkSheet.Cells[1, i] = table.Columns[i - 1].ColumnName;
            }
            for (int j = 0; j < table.Rows.Count; j++) // Exporting Rows in Excel  
            {
                for (int k = 0; k < table.Columns.Count; k++)
                {
                    excelWorkSheet.Cells[j + 2, k + 1] = table.Rows[j].ItemArray[k].ToString();
                }
            }


            book.SaveAs(Xlfile);
            book.Close(true);
            excel.Quit();

            Marshal.ReleaseComObject(book);
            Marshal.ReleaseComObject(book);
            Marshal.ReleaseComObject(excel);

        }
        public static List<DateTime> PublicHolidays(int? stateId)
        {
            List<DateTime> holidayList = new List<DateTime>();
            using (var uow = new UnitOfWork(isolation: System.Data.IsolationLevel.ReadUncommitted))
            {
                holidayList = uow.Context.Holidays.Where(x => x.IsNational == true || (stateId.HasValue && x.HolidayStates.Any(s => s.StateId == stateId.Value)))
                    .Select(x => x.HolidayDate).ToList();
            }
            return holidayList;
        }
    }
}
