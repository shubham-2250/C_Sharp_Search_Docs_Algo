using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.Office2021.DocumentTasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Helper.Utils;
using Task = System.Threading.Tasks.Task;

namespace Module_ChatWithSources
{
    public class Main_ChatwithSources : IMain_ChatwithSources
    {
        IHelper_ChatWithSources helper_ChatWithSources;
        public Main_ChatwithSources()
        {
            TextModificationUtils.FillDictionary();
            CosineSimiliarityUtils.FillLemmatizer();
            helper_ChatWithSources = new Helper_ChatWithSources();
        }

        public static async void CWSInitialize()
        {
            TextModificationUtils.FillDictionary();
            CosineSimiliarityUtils.FillLemmatizer();
        }

        public (string, Dictionary<int, string>) CWSEntrypoint(List<(List<string> sources, string message, string model, long tokenLimit, bool forceChunkify)> sourceParamsList)
        {
            Dictionary<string,int> sourcesToInt = new Dictionary<string,int>();
            Dictionary<int,string> intToSources = new Dictionary<int,string>();

            List<ChunkParams> chunks = new List<ChunkParams>();

            Parallel.ForEach(sourceParamsList, sourceParam =>
            {
                    chunks.AddRange(helper_ChatWithSources.CreateChunk(sourceParam));
            });
            int index = 1;
            foreach(ChunkParams chunkParam in chunks) 
            {
                index += Convert.ToInt32(sourcesToInt.TryAdd(chunkParam.sourceName,index));
                
            }
            foreach(var item in sourcesToInt)
            {
                intToSources.Add(item.Value,item.Key);
            }
            return (GetCWSString(sourcesToInt,chunks), intToSources);
        }

        string GetCWSString( Dictionary<string, int> sourcesToInt, List<ChunkParams> chunks)
        {
            StringBuilder stringBuilder = new StringBuilder();
            foreach (var chunk in chunks)
            {
                if(chunk.sourceName.ToLower().StartsWith("http"))
                {
                    stringBuilder.Append($"<source id = \"{sourcesToInt[chunk.sourceName]}\" lineNumber = \"{chunk.lineNumber}\" > {chunk.OriginalText}  </source>\n");
                }
                else if(chunk.sourceName.ToLower().EndsWith(".xlsx"))
                {
                    stringBuilder.Append($"<source id = \"{sourcesToInt[chunk.sourceName]}\" SheetNumber = \"{chunk.pageNumber}\" rowNumber = \"{chunk.lineNumber}\" > {chunk.OriginalText}  </source>\n");
                }
                else if(chunk.sourceName.ToLower().EndsWith(".docx"))
                {
                    stringBuilder.Append($"<source id = \"{sourcesToInt[chunk.sourceName]}\" page = \"{chunk.pageNumber}\" line = \"{chunk.lineNumber}\" > {chunk.OriginalText}  </source>\n");
                }
                else if(chunk.sourceName.ToLower().EndsWith(".pptx"))
                {
                    stringBuilder.Append($"<source id = \"{sourcesToInt[chunk.sourceName]}\" slideNumber = \"{chunk.pageNumber}\" > {chunk.OriginalText}  </source>\n");
                }
                else if(chunk.sourceName.ToLower().EndsWith(".csv"))
                {
                    stringBuilder.Append($"<source id = \"{sourcesToInt[chunk.sourceName]}\" SheetNumber = \"{chunk.pageNumber}\" rowNumber = \"{chunk.lineNumber}\" > {chunk.OriginalText}  </source>\n");
                }
                else if(chunk.sourceName.ToLower().EndsWith(".pdf"))
                {
                    stringBuilder.Append($"<source id = \"{sourcesToInt[chunk.sourceName]}\" pageNumber = \"{chunk.pageNumber}\" lineNumber = \"{chunk.lineNumber}\" > {chunk.OriginalText}  </source>\n");
                }
                else
                {
                    stringBuilder.Append($"<source id = \"{sourcesToInt[chunk.sourceName]}\" > {chunk.OriginalText}  </source>\n");
                }
            }
               
            return stringBuilder.ToString();
        }

    }
}
