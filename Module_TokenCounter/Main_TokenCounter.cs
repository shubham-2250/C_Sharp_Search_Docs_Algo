using System.Reflection;
using System.Text;

namespace Module_TokenCounter
{
    public class Main_TokenCounter : IMain_TokenCounter
    {
        Dictionary<string, long> OnlyPlaceToChangeModel;
        Dictionary<string, long> modelToToken;
        public Main_TokenCounter()
        {
            //gpt-4, gpt-3.5-turbo, text-embedding-ada-002, text-embedding-3-small, text-embedding-3-large
            OnlyPlaceToChangeModel = new Dictionary<string, long>() {
                { "gpt-3.5-turbo", 16000 },
                {"gpt-4",50000 }
            };
            modelToToken = new Dictionary<string, long>() {
                { "gpt-3.5-turbo", (OnlyPlaceToChangeModel["gpt-3.5-turbo"]*3)/4 },
                {"gpt-4",(OnlyPlaceToChangeModel["gpt-4"]*3)/4 }
            };
        }
        public async static void Initialize()
        {
            var encoding = Tiktoken.Encoding.ForModel("gpt-4");
            var tokens = encoding.Encode("input"); // [15339, 1917]
            var text = encoding.Decode(tokens); // hello world
            var numberOfTokens = encoding.CountTokens(text);
        }
        public long LongTokenCounter(string input, string model)
        {
            if (string.IsNullOrEmpty(input)) return 0;
            var encoding = Tiktoken.Encoding.ForModel(model);
            var tokens = encoding.Encode(input); // [15339, 1917]
            var text = encoding.Decode(tokens); // hello world
            var numberOfTokens = encoding.CountTokens(text); // 2
            return numberOfTokens;
        }
        public long LongTokenCounter(List<string> inputList, string model)
        {
            StringBuilder stringBuilder = new StringBuilder();
            foreach (var item in inputList)
                stringBuilder.Append(item + "\n");
            var input = stringBuilder.ToString();


            return LongTokenCounter(input, model);
        }
    }

    public interface IMain_TokenCounter
    {
        public long LongTokenCounter(string input, string model);
        public long LongTokenCounter(List<string> inputList, string model);
    }
}
