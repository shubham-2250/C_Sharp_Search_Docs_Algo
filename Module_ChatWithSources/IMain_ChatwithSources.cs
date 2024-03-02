namespace Module_ChatWithSources
{
    public interface IMain_ChatwithSources
    {
        public (string, List<string>, Dictionary<int, string>) CWSEntrypoint(List<(List<string> sources, string message, string model, long tokenLimit, bool forceChunkify)> sourceParamsList);
    }
}