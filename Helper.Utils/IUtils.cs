using System.Collections.Generic;
using System.Threading.Tasks;

namespace Helper.Utils
{
    public interface ICosineSimiliarityUtils
    {
        double CalculateCosineSimilarity(string str);
    }

    public interface ITextModificationUtils
    {
        List<string> Tokenize(string input);
        Dictionary<int, string> ExtractLines(string text);
        Dictionary<string, int> CreateTermFrequencyDictionary(List<string> words);
        public IDictionary<string, HashSet<string>> mainDictFunc();


    }

    public interface IBridgeCommunicationUtils
    {
        Task<string> DispatchJsonToCloud(string jsonString);
    }
}