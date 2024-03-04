using System.Net;
using System.Xml;
using Helper.Utils;
using HtmlAgilityPack;
using System.Data;
using Microsoft.VisualBasic;
using System.Collections.Generic;
using HtmlAgilityPack;
using System.Linq;
using System;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Helper.Utils;
using System.Collections.Generic;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using System.Data;
using System.Data.SqlTypes;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.Office2010.Excel;
using Module_TokenCounter;
using System.Security.Policy;
using NPOI.SS.Formula.Functions;


namespace Module_ChatWithSources
{
    public class Helper_ChatWithSources : IHelper_ChatWithSources
    {
        private IHelper_ExtractText ExtractTextHelper;
        private IMain_TokenCounter Main_TokenCounter;
        public Helper_ChatWithSources()
        {
            ExtractTextHelper = new Helper_ExtractText();
            Main_TokenCounter = new Main_TokenCounter();
        }
        public List<ChunkParams> CreateChunk((IEnumerable<string> sources, string message, string model, long tokenLimit, bool forceChunkify) BigChunkParams)
        {
            IEnumerable<string> sources = BigChunkParams.sources; string message = BigChunkParams.message; string model = BigChunkParams.model; long tokenLimit = BigChunkParams.tokenLimit; bool forceChunkify= BigChunkParams.forceChunkify;

            IEnumerable<ChunkParams> chunks = new List<ChunkParams>();
            if (forceChunkify==false)
            {
                try
                {
                    List<ChunkParams> tempChunks = new List<ChunkParams>();
                    object syncLock = new object();
                    Parallel.ForEach(sources, source =>
                    {
                        var tempChunk = new List<ChunkParams>();
                        if (source.ToLower().StartsWith("http"))
                        {
                            tempChunk = ExtractTextFromURL(source);

                           
                        }
                        else if (source.ToLower().EndsWith(".pdf"))
                        {
                            tempChunk = ExtractTextFromPDF(source);
                            
                        }
                        else
                        {
                            forceChunkify = true;
                        }
                        lock (syncLock)
                        {
                            tempChunks.AddRange(tempChunk);
                        }
                    });
                    tempChunks.RemoveAll(x => x.lineText.Trim().Length <= 4);
                    if (CountTokens(tempChunks,model)>tokenLimit)
                    {
                        forceChunkify = true;
                    }
                    else 
                    { 
                        return tempChunks; 
                    }
                }
                catch(Exception e)
                {
                    forceChunkify = true;
                }
                
            }
            if (forceChunkify == true)
            {
                //TODO: SV : Extracting text from each resource can be multithreaded
                chunks = ExtractTextHelper.ExtractText(sources);
                
                chunks = chunks.Where(x => x.lineText.Trim().Length > 4);
                if(CountTokens(chunks, model)>tokenLimit)
                {
                    
                    SetCosineSimilarity(message, chunks);
                    
                    chunks = SortByCosineSimilarity(chunks, model, tokenLimit);
                    while (CountTokens(chunks,model) > tokenLimit)
                    {
                        chunks = chunks.Take((3 * chunks.Count()) / 4);
                    }
                            
                }
            }
            return chunks.ToList();
            
        }
        void SetCosineSimilarity(string message, IEnumerable<ChunkParams> chunkParams)
        {
            ////TODO: SV : Setting CSI can also be multi threaded
            object syncLock = new object();
            Parallel.ForEach(chunkParams, item =>
            {
                SetCosineSimilarity(message, item);
            });
        }
        IEnumerable<ChunkParams> SortByCosineSimilarity(IEnumerable<ChunkParams> chunkParamsOld, string model, long tokenLimit)
        {
            var chunkParams = chunkParamsOld.ToList();
            var sortedSet = new SortedSet<ChunkParams>(new CustomizedComparator());

            var sorted = sortedSet;

            foreach (var item in chunkParams)
            {
                if (item.lineText.Split(" ").Length > 1)
                    sortedSet.Add(item);
            }
            sorted = sortedSet;
            List<ChunkParams> chunkParams1 = new List<ChunkParams>(sorted);
            if (chunkParams1.Count() > 4 && chunkParams1[4].CSI == 0)
            {
                chunkParams.RemoveAll(x => x.OriginalText.Count() < 10);
                return SortByRandomOrder(chunkParams, model, tokenLimit);
            }
            return sorted;
        }
        IEnumerable<ChunkParams> SortByRandomOrder(IEnumerable<ChunkParams> chunkParams, string model, long tokenLimit)
        {
            var sortedSet = new SortedSet<ChunkParams>(new CustomizedComparator());
            var limit = tokenLimit;//8000-1000;
            var sorted = sortedSet;
            long cnt = 1;
            long tokenCnt = 0;
            StringBuilder stringBuilder =   new StringBuilder();
            foreach (var item in chunkParams)
            {
                stringBuilder.Append( item.lineText);
            }
            string str = stringBuilder.ToString();
            tokenCnt = Main_TokenCounter.LongTokenCounter(str,model);

            cnt = long.Max(cnt, (tokenCnt / limit));
            var chunkParamsList = chunkParams.ToList();
            long currtokenCnt = 0;
            str = string.Empty;
            if (cnt <= 0)
                cnt = 1;
            for (int i = 0; (i < chunkParamsList.Count()); i += (int)cnt)
            {
                sortedSet.Add(chunkParamsList[i]);
                str += chunkParamsList[i].lineText;
            }

            return sortedSet;
        }
        void SetCosineSimilarity(string message, ChunkParams chunkParams)
        {
            ITextModificationUtils textModificationUtilsObj = new TextModificationUtils();
            ICosineSimiliarityUtils cosineSimiliarityUtils = new CosineSimiliarityUtils(message, textModificationUtilsObj);
            List<string> countryCodes = new List<string>
        {
            "+1",   // United States
            "+1",   // Canada
            "+44",  // United Kingdom
            "+61",  // Australia
            "+49",  // Germany
            "+33",  // France
            "+86",  // China
            "+81",  // Japan
            "+91",  // India
            "+55",  // Brazil
            "+7",   // Russia
            "+27",  // South Africa
            "+52",  // Mexico
            "+34",  // Spain
            "+39",  // Italy
            "+82",  // South Korea
            "+90",  // Turkey
            "+234", // Nigeria
            "+20",  // Egypt
            "+54","@"   // Argentina
        };
            if (countryCodes.Any(chunkParams.OriginalText.Contains))
                chunkParams.CSI = 1;

            chunkParams.CSI = cosineSimiliarityUtils.CalculateCosineSimilarity(chunkParams.OriginalText);
        }
        List<ChunkParams> ExtractTextFromURL(string url)
        {
            string text = string.Empty;
            using (HttpClient httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.3");
                try
                {
                    var timeoutInSeconds = 7;
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
                            }
                        }

                    }

                }
                catch (Exception ex)
                {

                }
            }
            var lst = text.Replace(". ", ".\n ").Split('\n', '\r').ToList();
            List < ChunkParams > chunks = new List < ChunkParams >();
            for (int i=0;i<lst.Count();i++)
            {
                var temporiginaltext = string.Concat(i - 1 >= 0 ? lst[i - 1] : "", lst[i], (i + 1) < lst.Count() ? lst[i + 1] : "");
                chunks.Add(new ChunkParams() { sourceName = url, lineNumber = (i + 1).ToString(), lineText = lst[i], OriginalText = temporiginaltext });
            }
            return chunks;
        }
        List<ChunkParams> ExtractTextFromPDF(string filePath)
        {
            List<ChunkParams> chunks = new List<ChunkParams>();
            UglyToad.PdfPig.PdfDocument document = UglyToad.PdfPig.PdfDocument.Open(filePath);
            List<string> text = new List<string>();
            for (int pageNumber = 1; pageNumber <= document.NumberOfPages; pageNumber++)
            {
                // Get the PDF page.
                var page = document.GetPage(pageNumber);
                // Extract text from the page.
                var lst = page.Text.Replace(". ",".\n ").Split('\n', '\r').ToList();

                for (int i = 0; i < lst.Count(); i++)
                {
                    var temporiginaltext = string.Concat(i-1>=0 ? lst[i-1]:"", lst[i], (i+1)<lst.Count()?lst[i+1]:"");
                    chunks.Add(new ChunkParams() { sourceName = filePath, pageNumber = pageNumber.ToString(), lineNumber = (i + 1).ToString(), lineText = lst[i], OriginalText = temporiginaltext });
                }
            }
            return chunks;
        }
        long CountTokens(IEnumerable<ChunkParams> chunks, string model)
        {
            List<string>  strings  = new List<string>();
            foreach(var chunk in chunks)
                strings.Add($"<source id = \"x\" page = \"{chunk.pageNumber}\" line = \"{chunk.lineNumber}\" > {chunk.lineText}  </source>");
            return Main_TokenCounter.LongTokenCounter(strings, model);
        }
    }

    public interface IHelper_ChatWithSources
    {
        public List<ChunkParams> CreateChunk((IEnumerable<string> sources, string message, string model, long tokenLimit, bool forceChunkify) BigChunk);
    }

    public class ChunkParams
    {
        public string sourceName { get; set; } = string.Empty;
        public string pageNumber { get; set; } = string.Empty;
        public string lineNumber { get; set; } = string.Empty;
        public string chunkNumber { get; set; } = string.Empty;

        public string lineText { get; set; } = string.Empty;
        public string OriginalText { get; set; } = string.Empty;

        public double CSI { get; set; }
    }
    public class CustomizedComparator : IComparer<ChunkParams>
    {

        public int Compare(ChunkParams x, ChunkParams y)
        {
            if (x.CSI == y.CSI)
            {
                return 1;
            }
            return y.CSI.CompareTo(x.CSI);
        }
    }
}
