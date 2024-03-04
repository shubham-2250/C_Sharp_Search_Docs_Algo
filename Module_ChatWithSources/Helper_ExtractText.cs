using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using HtmlAgilityPack;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Helper.Utils;
using System.Runtime.CompilerServices;

namespace Module_ChatWithSources
{
    public  interface IHelper_ExtractText
    {
        public List<ChunkParams> ExtractText(IEnumerable<string> sources);
    }
    public static class ThreadSafeOperations
    {
        static object syncLock = new object();
        public static void AddThreadSafe(this List<ChunkParams> itemList, ChunkParams itemToBeAdded)
        {
            lock (syncLock)
            {
                itemList.Add(itemToBeAdded);
            }

        }
    }
    class Helper_ExtractText : IHelper_ExtractText
    {
        public List<ChunkParams> ExtractText(IEnumerable<string> sources)
        {
            if (sources == null)
            {
                return Enumerable.Empty<ChunkParams>().ToList();
            }

            var chunks = new List<ChunkParams>();
            object syncLock = new object();
            Parallel.ForEach(sources, source =>
            {
                if (source.StartsWith("http") && source.EndsWith(".pdf"))
                {
                    try
                    {
                        var pdfFile = DownloadPdf(source);
                        var pdfChunks = ExtractFromPdf(pdfFile);

                        foreach (var chunk in pdfChunks)
                            chunk.sourceName = source;
                        lock (syncLock)
                        {
                            chunks.AddRange(pdfChunks);
                        }
                        
                        DeleteFile(pdfFile);
                    }
                    catch (Exception e) { }
                }
                else if (source.StartsWith("http"))
                {
                    var urlChunks = ExtractFromUrl(source);
                    lock (syncLock)
                    {
                        chunks.AddRange(urlChunks);
                    }
                }
                else
                {
                    var ext = Path.GetExtension(source).ToLower();

                    switch (ext)
                    {
                        case ".pdf":
                            var pdfChunks = ExtractFromPdf(source);
                            lock (syncLock)
                            {
                                chunks.AddRange(pdfChunks);
                            }
                            break;
                        case ".xlsx":
                            var excelChunks = ExtractFromExcel(source);
                            lock (syncLock)
                            {
                                chunks.AddRange(excelChunks);
                            }
                            break;
                        case ".csv":
                            var csvChunks = ConvertCSVtoDataTable(source);
                            lock (syncLock)
                            {
                                chunks.AddRange(csvChunks);
                            }
                            break;
                        case ".pptx":
                            var pptxChunks = ConvertPPTXToChunks(source);
                            lock (syncLock)
                            {
                                chunks.AddRange(pptxChunks);
                            }
                            break;
                        case ".docx":
                            var docxChunks = ConvertDocxToChunks(source);
                            lock (syncLock)
                            {
                                chunks.AddRange(docxChunks);
                            }
                            break;
                        default:
                            break;
                    }
                }
            });
            chunks.ForEach(chunk => { chunk.lineText = chunk.lineText.Trim(); });
            return chunks;
        }
        string DownloadPdf(string url)
        {
            var file = Path.GetTempFileName();

            // Create a cancellation token source with a timeout of 3 seconds
            using (var cts = new CancellationTokenSource())
            {
                // Set up a task to download the file
                var downloadTask = DownloadFileAsync(url, file);

                // Use Task.WhenAny to wait for the first task to complete or the cancellation token to be triggered
                var completedTask = Task.WhenAny(downloadTask, Task.Delay(TimeSpan.FromSeconds(10), cts.Token)).Result;

                // Check if the download completed or was canceled due to a timeout
                if (completedTask == downloadTask)
                {
                    Console.WriteLine("Download completed successfully.");
                }
                else
                {
                    // Cancel the download task and handle the cancellation exception
                    cts.Cancel();

                }
            }


            return file;
        }
        async Task DownloadFileAsync(string url, string file)
        {
            using (var client = new WebClient())
            {
                // Download the file asynchronously
                await client.DownloadFileTaskAsync(url, file);
            }
        }
        
        List<ChunkParams> ExtractFromPdf(string filePath)
        {
            var document = UglyToad.PdfPig.PdfDocument.Open(filePath);

            string prevLineText = string.Empty;

            

            List<ChunkParams> chunks = new List<ChunkParams>();
            object syncLock = new object();
            

            Parallel.For(1, document.NumberOfPages, pageNumber =>
            {
                ITextModificationUtils TextModificationUtilsObj = new TextModificationUtils();
                var page = document.GetPage(pageNumber);

                var pageText = page.Text;

                if (string.IsNullOrEmpty(pageText))
                {
                    return;
                }

                var extractedLines = TextModificationUtilsObj.ExtractLines(pageText);
                ChunkParams prevChunkParam = null;
                foreach (var extractedLine in extractedLines)
                {
                    int lineNumber = extractedLine.Key;

                    var chunkParams = new ChunkParams()
                    {
                        OriginalText = prevLineText + extractedLine.Value,
                        sourceName = filePath,
                        pageNumber = pageNumber.ToString(),
                        lineNumber = lineNumber.ToString(),
                        CSI = int.MinValue,
                        lineText = extractedLine.Value
                    };
                    lock (syncLock)
                    {
                        chunks.Add(chunkParams);
                    }


                    if (prevChunkParam != null)
                    {
                        prevChunkParam.OriginalText += extractedLine.Value;
                    }

                    prevLineText = extractedLine.Value ?? "";
                    prevChunkParam = chunkParams;
                }
            });

            return chunks;
        }
        void DeleteFile(string filePath)
        {
            // Check if the file exists before attempting to delete
            if (File.Exists(filePath))
            {
                // Delete the file
                File.Delete(filePath);
            }
            else
            {
                throw new FileNotFoundException($"The file '{filePath}' does not exist.");
            }
        }
        List<ChunkParams> ExtractFromUrl(string url)
        {
            List<ChunkParams> chunks = new List<ChunkParams>();

            var listOfParagraphs = ExtractHTMLContent(url);

            if (listOfParagraphs == null)
                return chunks;

            string prevLineText = string.Empty;

            ChunkParams prevChunkParam = null;

            ITextModificationUtils TextModificationUtilsObj = new TextModificationUtils();

            for (int paraNumber = 1; paraNumber < listOfParagraphs.Count + 1; paraNumber++)
            {
                var para = listOfParagraphs[paraNumber - 1];

                var extractedLines = TextModificationUtilsObj.ExtractLines(para);

                foreach (var extractedLine in extractedLines)
                {
                    int lineNumber = extractedLine.Key;

                    ChunkParams chunkParams = new ChunkParams()
                    {
                        OriginalText = prevLineText + extractedLine.Value,
                        sourceName = url,
                        chunkNumber = paraNumber.ToString(),
                        lineNumber = lineNumber.ToString(),
                        lineText = extractedLine.Value
                    };

                    chunks.Add(chunkParams);

                    if (prevChunkParam != null)
                    {
                        prevChunkParam.OriginalText += extractedLine.Value;
                    }

                    prevLineText = extractedLine.Value;
                    prevChunkParam = chunkParams;
                }
            }

            return chunks;
        }
        List<string> ExtractHTMLContent(string url)
        {
            List<String> stringArr = null;

            string text = string.Empty;

            using (HttpClient httpClient = new HttpClient())
            {
                try
                {
                    httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.3");
                    var timeoutInSeconds = 7;
                    if (!url.EndsWith(".pdf")) timeoutInSeconds = 4;
                    using (var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutInSeconds)))
                    {
                        Task<HttpResponseMessage> longRunningTask = httpClient.GetAsync(url);

                        var completedTask = Task.WhenAny(longRunningTask, Task.Delay(-1, cancellationTokenSource.Token)).Result;

                        if (completedTask == longRunningTask)
                        {
                            var response = longRunningTask.Result;

                            if (response.IsSuccessStatusCode)
                            {
                                text = response.Content.ReadAsStringAsync().Result;
                                text = WebUtility.HtmlDecode(text);
                                stringArr = ExtractParagraphs(text);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {

                }
            }

            return stringArr;
        }
        List<string> ExtractParagraphs(string htmlContent)
        {
            var textChunks = new List<string>();

            var doc = new HtmlDocument();
            doc.LoadHtml(htmlContent);

            foreach (var paragraphNode in doc.DocumentNode.SelectNodes("//p | //h1 | //h2 | //div | //span | //blockquote | //pre | //ul"))
            {
                string chunkText = paragraphNode.InnerText.Trim();
                if (!string.IsNullOrWhiteSpace(chunkText))
                    textChunks.Add(chunkText);
            }

            return textChunks;
        }


        List<ChunkParams> ExtractFromExcel(string filePath)
        {
            int numberOfSheets = 0;
            List<ChunkParams> chunks = new List<ChunkParams>();

            var dataTable = Excel_To_DataTable(filePath, 0, ref numberOfSheets);
            var dataTableHeader = ExtractHeaders(dataTable);
            int id = 0;
            foreach (DataRow row in dataTable.Rows)
            {
                id++;
                var result = "";
                // Iterate through each column in the row
                foreach (DataColumn column in dataTable.Columns)
                {
                    // Append the value of each cell to the result string
                    result += row[column].ToString() + "\t";
                }
                if (string.IsNullOrWhiteSpace(dataTableHeader))
                    dataTableHeader = result;
                var chunkParams = new ChunkParams()
                {
                    OriginalText = dataTableHeader + "\n" + result,
                    sourceName = filePath,
                    pageNumber = $" 1",
                    lineNumber = id.ToString(),
                    CSI = int.MinValue,
                    lineText = dataTableHeader + "\n" + result
                };
                chunks.Add(chunkParams);
            }
            for (int i = 1; i < numberOfSheets; i++)
            {
                int tmp = 0;
                var dataTable1 = Excel_To_DataTable(filePath, i, ref tmp);
                var dataTableHeader1 = ExtractHeaders(dataTable);
                id = 0;
                foreach (DataRow row in dataTable1.Rows)
                {
                    id++;

                    var result = "";
                    // Iterate through each column in the row
                    foreach (DataColumn column in dataTable1.Columns)
                    {
                        // Append the value of each cell to the result string
                        result += row[column].ToString() + "\t";
                    }
                    if (string.IsNullOrWhiteSpace(dataTableHeader1))
                        dataTableHeader1 = result;
                    var chunkParams = new ChunkParams()
                    {
                        OriginalText = dataTableHeader1 + "\n" + result,
                        sourceName = filePath,
                        pageNumber = $" {i + 1}",
                        lineNumber = id.ToString(),
                        CSI = int.MinValue,
                        lineText = dataTableHeader1 + "\n" + result
                    };
                    chunks.Add(chunkParams);
                }
            }

            return chunks;
        }
        DataTable Excel_To_DataTable(string pFilePath, int pSheetIndex, ref int numberOfSheets)
        {
            // --------------------------------- //
            /* REFERENCIAS:
             * NPOI.dll
             * NPOI.OOXML.dll
             * NPOI.OpenXml4Net.dll */
            // --------------------------------- //
            /* USING:
             * using NPOI.SS.UserModel;
             * using NPOI.HSSF.UserModel;
             * using NPOI.XSSF.UserModel; */
            // AUTOR: Ing. Jhollman Chacon R. 2015
            // --------------------------------- //
            DataTable Tabla = null;
            try
            {
                if (System.IO.File.Exists(pFilePath))
                {

                    IWorkbook workbook = null;  //IWorkbook determina si es xls o xlsx              
                    ISheet worksheet = null;
                    string first_sheet_name = "";

                    using (FileStream FS = new FileStream(pFilePath, FileMode.Open, FileAccess.Read))
                    {
                        workbook = WorkbookFactory.Create(FS);          //Abre tanto XLS como XLSX
                        numberOfSheets = workbook.NumberOfSheets;
                        worksheet = workbook.GetSheetAt(pSheetIndex);    //Obtener Hoja por indice
                        first_sheet_name = worksheet.SheetName;         //Obtener el nombre de la Hoja

                        Tabla = new DataTable(first_sheet_name);
                        Tabla.Rows.Clear();
                        Tabla.Columns.Clear();

                        // Leer Fila por fila desde la primera
                        for (int rowIndex = 0; rowIndex <= worksheet.LastRowNum; rowIndex++)
                        {
                            DataRow NewReg = null;
                            IRow row = worksheet.GetRow(rowIndex);
                            IRow row2 = null;
                            IRow row3 = null;

                            if (rowIndex == 0)
                            {
                                row2 = worksheet.GetRow(rowIndex + 1); //Si es la Primera fila, obtengo tambien la segunda para saber el tipo de datos
                                row3 = worksheet.GetRow(rowIndex + 2); //Y la tercera tambien por las dudas
                            }

                            if (row != null) //null is when the row only contains empty cells 
                            {
                                if (rowIndex > 0) NewReg = Tabla.NewRow();

                                int colIndex = 0;
                                //Leer cada Columna de la fila
                                foreach (ICell cell in row.Cells)
                                {
                                    object valorCell = null;
                                    string cellType = "";
                                    string[] cellType2 = new string[2];

                                    if (rowIndex == 0) //Asumo que la primera fila contiene los titlos:
                                    {
                                        for (int i = 0; i < 2; i++)
                                        {
                                            ICell cell2 = null;
                                            if (i == 0) { cell2 = row2.GetCell(cell.ColumnIndex); }
                                            else { cell2 = row3.GetCell(cell.ColumnIndex); }

                                            if (cell2 != null)
                                            {
                                                switch (cell2.CellType)
                                                {
                                                    case CellType.Blank: break;
                                                    case CellType.Boolean: cellType2[i] = "System.Boolean"; break;
                                                    case CellType.String: cellType2[i] = "System.String"; break;
                                                    case CellType.Numeric:
                                                        if (HSSFDateUtil.IsCellDateFormatted(cell2)) { cellType2[i] = "System.DateTime"; }
                                                        else
                                                        {
                                                            cellType2[i] = "System.Double";  //valorCell = cell2.NumericCellValue;
                                                        }
                                                        break;

                                                    case CellType.Formula:
                                                        bool continuar = true;
                                                        switch (cell2.CachedFormulaResultType)
                                                        {
                                                            case CellType.Boolean: cellType2[i] = "System.Boolean"; break;
                                                            case CellType.String: cellType2[i] = "System.String"; break;
                                                            case CellType.Numeric:
                                                                if (HSSFDateUtil.IsCellDateFormatted(cell2)) { cellType2[i] = "System.DateTime"; }
                                                                else
                                                                {
                                                                    try
                                                                    {
                                                                        //DETERMINAR SI ES BOOLEANO
                                                                        if (cell2.CellFormula == "TRUE()") { cellType2[i] = "System.Boolean"; continuar = false; }
                                                                        if (continuar && cell2.CellFormula == "FALSE()") { cellType2[i] = "System.Boolean"; continuar = false; }
                                                                        if (continuar) { cellType2[i] = "System.Double"; continuar = false; }
                                                                    }
                                                                    catch { }
                                                                }
                                                                break;
                                                        }
                                                        break;
                                                    default:
                                                        cellType2[i] = "System.String"; break;
                                                }
                                            }
                                        }

                                        //Resolver las diferencias de Tipos
                                        if (cellType2[0] == cellType2[1]) { cellType = cellType2[0]; }
                                        else
                                        {
                                            if (cellType2[0] == null) cellType = cellType2[1];
                                            if (cellType2[1] == null) cellType = cellType2[0];
                                            if (string.IsNullOrWhiteSpace(cellType)) cellType = "System.String";
                                        }

                                        //Obtener el nombre de la Columna
                                        string colName = "Column_{0}";
                                        try { colName = cell.StringCellValue; }
                                        catch { colName = string.Format(colName, colIndex); }

                                        //Verificar que NO se repita el Nombre de la Columna
                                        foreach (DataColumn col in Tabla.Columns)
                                        {
                                            if (col.ColumnName == colName) colName = string.Format("{0}_{1}", colName, colIndex);
                                        }
                                        if (string.IsNullOrWhiteSpace(cellType)) cellType = "System.String";
                                        //Agregar el campos de la tabla:
                                        DataColumn codigo = new DataColumn(colName, System.Type.GetType(cellType));
                                        Tabla.Columns.Add(codigo); colIndex++;
                                    }
                                    else
                                    {
                                        //Las demas filas son registros:
                                        switch (cell.CellType)
                                        {
                                            case CellType.Blank: valorCell = DBNull.Value; break;
                                            case CellType.Boolean: valorCell = cell.BooleanCellValue; break;
                                            case CellType.String: valorCell = cell.StringCellValue; break;
                                            case CellType.Numeric:
                                                if (HSSFDateUtil.IsCellDateFormatted(cell)) { valorCell = cell.DateCellValue; }
                                                else { valorCell = cell.NumericCellValue; }
                                                break;
                                            case CellType.Formula:
                                                switch (cell.CachedFormulaResultType)
                                                {
                                                    case CellType.Blank: valorCell = DBNull.Value; break;
                                                    case CellType.String: valorCell = cell.StringCellValue; break;
                                                    case CellType.Boolean: valorCell = cell.BooleanCellValue; break;
                                                    case CellType.Numeric:
                                                        if (HSSFDateUtil.IsCellDateFormatted(cell)) { valorCell = cell.DateCellValue; }
                                                        else { valorCell = cell.NumericCellValue; }
                                                        break;
                                                }
                                                break;
                                            default: valorCell = cell.StringCellValue; break;
                                        }
                                        //Agregar el nuevo Registro
                                        if (cell.ColumnIndex <= Tabla.Columns.Count - 1) NewReg[cell.ColumnIndex] = valorCell;
                                    }
                                }
                            }
                            if (rowIndex > 0) Tabla.Rows.Add(NewReg);
                        }
                        Tabla.AcceptChanges();
                    }
                }
                else
                {
                    throw new Exception("ERROR 404: El archivo especificado NO existe.");
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return Tabla;
        }
        List<ChunkParams> ConvertCSVtoDataTable(string filePath)
        {
            List<ChunkParams> chunks = new List<ChunkParams>();
            DataTable dataTable = new DataTable();

            try
            {
                using (StreamReader sr = new StreamReader(filePath))
                {
                    string[] headers = sr.ReadLine().Split(',');
                    foreach (string header in headers)
                    {
                        dataTable.Columns.Add(header);
                    }

                    while (!sr.EndOfStream)
                    {
                        string[] rows = sr.ReadLine().Split(',');
                        DataRow dataRow = dataTable.NewRow();
                        for (int i = 0; i < headers.Length; i++)
                        {
                            dataRow[i] = rows[i];
                        }
                        dataTable.Rows.Add(dataRow);
                    }
                }
            }
            catch (Exception ex)
            {
                // handle any exception that occurred during the conversion
                Console.WriteLine($"An error occurred while converting CSV to DataTable: {ex.Message}");
            }
            int id = 0;
            var dataTableHeader = ExtractHeaders(dataTable);
            foreach (DataRow row in dataTable.Rows)
            {
                id++;
                var result = "";
                // Iterate through each column in the row
                foreach (DataColumn column in dataTable.Columns)
                {
                    // Append the value of each cell to the result string
                    result += row[column].ToString() + "\t";
                }
                if (string.IsNullOrWhiteSpace(dataTableHeader))
                    dataTableHeader = result;
                var chunkParams = new ChunkParams()
                {
                    OriginalText = dataTableHeader + "\n" + result,
                    sourceName = filePath,
                    pageNumber = $"Sheet 1",
                    lineNumber = id.ToString(),
                    CSI = int.MinValue,
                    lineText = dataTableHeader + "\n" + result
                };
                chunks.Add(chunkParams);
            }
            return chunks;
        }
        string ExtractHeaders(DataTable dataTable)
        {
            // Use LINQ to extract column names
            var headers = string.Join("\t", dataTable.Columns.Cast<DataColumn>()
                                           .Select(column => column.ColumnName)
                                           .ToArray());

            return headers;
        }
        List<ChunkParams> ConvertPPTXToChunks(string filePath)
        {
            List<ChunkParams> chunks = new List<ChunkParams>();
            ITextModificationUtils TextModificationUtilsObj = new TextModificationUtils();

            using (PresentationDocument presentationDocument = PresentationDocument.Open(filePath, false))
            {
                var sourceName = filePath;
                PresentationPart presentationPart = presentationDocument.PresentationPart;
                if (presentationPart != null && presentationPart.Presentation != null)
                {
                    // Iterate through all the slides in the presentation
                    int slideNumber = 0;
                    foreach (SlidePart slidePart in presentationPart.SlideParts)
                    {
                        slideNumber++;
                        if (slidePart.Slide != null)
                        {
                            // Extract the text from each slide
                            var textElements = slidePart.Slide.Descendants<DocumentFormat.OpenXml.Drawing.Text>();
                            string text = "";
                            foreach (var textElement in textElements)
                            {
                                text += "\n" + textElement.Text;
                            }
                            if (string.IsNullOrWhiteSpace(text))
                                continue;
                            ChunkParams chunkParams = new ChunkParams()
                            {
                                OriginalText = text,
                                sourceName = sourceName,
                                chunkNumber = slideNumber.ToString(),
                                pageNumber = slideNumber.ToString(),
                                CSI = int.MinValue,
                                lineText = text

                            };
                            chunks.Add(chunkParams);
                        }
                    }
                }
            }

            return chunks;
        }
        List<ChunkParams> ConvertDocxToChunks(string docxPath)
        {
            List<ChunkParams> chunks = new List<ChunkParams>();
            ITextModificationUtils TextModificationUtilsObj = new TextModificationUtils();
            WordprocessingDocument myDocument = WordprocessingDocument.Open(docxPath, false);
            var paragraphs = myDocument.MainDocumentPart.Document.Body.Elements<Paragraph>();
            var sourceName = docxPath;
            var listOfParagraphs = new List<string>();
            foreach (var paragraph in paragraphs)
            {
                if (!string.IsNullOrWhiteSpace(paragraph.InnerText))
                    listOfParagraphs.Add(paragraph.InnerText);
            }

            if (listOfParagraphs == null) return chunks;
            string prevLineText = string.Empty;
            ChunkParams prevChunkParam = null;
            for (int paraNumber = 1; paraNumber < listOfParagraphs.Count + 1; paraNumber++)
            {
                var para = listOfParagraphs[paraNumber - 1];
                var extractedLines = TextModificationUtilsObj.ExtractLines(para);
                foreach (var extractedLine in extractedLines)
                {
                    int lineNumber = extractedLine.Key;
                    ChunkParams chunkParams = new ChunkParams()
                    {
                        sourceName = sourceName,
                        chunkNumber = paraNumber.ToString(),
                        pageNumber = paraNumber.ToString(),
                        lineNumber = lineNumber.ToString(),
                        CSI = int.MinValue,
                        lineText = extractedLine.Value ?? "",
                        OriginalText = prevLineText + extractedLine.Value,
                    };

                    chunks.Add(chunkParams);
                    if (prevChunkParam != null)
                    {
                        prevChunkParam.OriginalText += extractedLine.Value;
                    }
                    prevLineText = extractedLine.Value ?? "";
                    prevChunkParam = chunkParams;
                }
            }
            return chunks;
        }
    }

    
}
