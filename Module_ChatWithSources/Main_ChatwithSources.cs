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

        public (string,List<string>, Dictionary<int, string>) CWSEntrypoint(List<(List<string> sources, string message, string model, long tokenLimit, bool forceChunkify)> sourceParamsList)
        {
            Dictionary<string,int> sourcesToInt = new Dictionary<string,int>();
            Dictionary<int,string> intToSources = new Dictionary<int,string>();

            List<ChunkParams> chunks = new List<ChunkParams>();
            object syncLock = new object();
            Parallel.ForEach(sourceParamsList, sourceParam =>
            {
                var temp = helper_ChatWithSources.CreateChunk(sourceParam);
                lock (syncLock)
                {
                    chunks.AddRange(temp);
                }
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
            var tuple_string_ListOfString = GetCWSString(sourcesToInt, chunks);
            return (tuple_string_ListOfString.Item1, tuple_string_ListOfString.Item2, intToSources);
        }

        (string,List<string>) GetCWSString( Dictionary<string, int> sourcesToInt, List<ChunkParams> chunks)
        {
            StringBuilder stringBuilder = new StringBuilder();
            List<string> cwsStringList = new List<string>();
            foreach (var chunk in chunks)
            {
                var str = "";
                if (chunk.sourceName.ToLower().StartsWith("http"))
                {
                    str = $"<source id = \"{sourcesToInt[chunk.sourceName]}\" lineNumber = \"{chunk.lineNumber}\" > {chunk.OriginalText}  </source>";
                    stringBuilder.Append(str + "\n");
                    cwsStringList.Add(str);
                }
                else if (chunk.sourceName.ToLower().EndsWith(".xlsx"))
                {
                    str = $"<source id = \"{sourcesToInt[chunk.sourceName]}\" SheetNumber = \"{chunk.pageNumber}\" rowNumber = \"{chunk.lineNumber}\" > {chunk.OriginalText}  </source>";
                    stringBuilder.Append(str + "\n");
                    cwsStringList.Add(str);

                }
                else if (chunk.sourceName.ToLower().EndsWith(".docx"))
                {

                    str = $"<source id = \"{sourcesToInt[chunk.sourceName]}\" page = \"{chunk.pageNumber}\" line = \"{chunk.lineNumber}\" > {chunk.OriginalText}  </source>";
                    stringBuilder.Append(str + "\n");
                    cwsStringList.Add(str);

                }
                else if (chunk.sourceName.ToLower().EndsWith(".pptx"))
                {

                    str = $"<source id = \"{sourcesToInt[chunk.sourceName]}\" slideNumber = \"{chunk.pageNumber}\" > {chunk.OriginalText}  </source>\n";
                    stringBuilder.Append(str + "\n");
                    cwsStringList.Add(str);
                }
                else if (chunk.sourceName.ToLower().EndsWith(".csv"))
                {

                    str = $"<source id = \"{sourcesToInt[chunk.sourceName]}\" SheetNumber = \"{chunk.pageNumber}\" rowNumber = \"{chunk.lineNumber}\" > {chunk.OriginalText}  </source>";
                    stringBuilder.Append(str + "\n");
                    cwsStringList.Add(str);
                }
                else if (chunk.sourceName.ToLower().EndsWith(".pdf"))
                {

                    str = $"<source id = \"{sourcesToInt[chunk.sourceName]}\" pageNumber = \"{chunk.pageNumber}\" lineNumber = \"{chunk.lineNumber}\" > {chunk.OriginalText}  </source>";
                    stringBuilder.Append(str + "\n");
                    cwsStringList.Add(str);
                }
                else
                {

                    str = $"<source id = \"{sourcesToInt[chunk.sourceName]}\" > {chunk.OriginalText}  </source>";
                    stringBuilder.Append(str + "\n");
                    cwsStringList.Add(str);
                }
            }
               
            return (stringBuilder.ToString(),cwsStringList);
        }

    }
}
