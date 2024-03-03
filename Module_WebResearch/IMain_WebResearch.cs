namespace Module_WebResearch
{
    public interface IMain_WebResearch
    {
        public Task<List<List<string>>> Get_WebResearchURLs(List<string> queries);
    }
}