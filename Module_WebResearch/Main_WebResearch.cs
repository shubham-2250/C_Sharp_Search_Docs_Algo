using DocumentFormat.OpenXml.Office2010.ExcelAc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Module_WebResearch
{
    public class Main_WebResearch : IMain_WebResearch
    {
        IHelper_WebResearch helper_WebResearh;
        public Main_WebResearch() 
        {
            helper_WebResearh = new Helper_WebResearch();
        }
        
        public async Task<List<List<string>>> Get_WebResearchURLs(List<string> queries)
        {
            return (await helper_WebResearh.Helper_WebResearch_List(queries));
        }
    }
}
