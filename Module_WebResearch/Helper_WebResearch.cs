using System.Text.RegularExpressions;

namespace Module_WebResearch
{
    public class Helper_WebResearch : IHelper_WebResearch
    {
        public Helper_WebResearch() { }
        public async Task<List<List<string>>> Helper_WebResearch_List(List<string> queryList) 
        {
            List<List<string>> result = new List<List<string>>();
            await Parallel.ForEachAsync(queryList, async (query, ct) => {
                result.Add(await GoogleSearch(query));
            });
            return result;
        }
        async Task<List<string>> GoogleSearch(string query)
        {
            query = query.Replace(" ", "+");
            List<string> urls = new List<string>();
            string url = "https://www.google.com/search?q=" + query + "&ie=utf-8&oe=utf-8";

            HttpClient client = new HttpClient();
            string html = await client.GetStringAsync(url);

            Regex regex = new Regex("<a\\s+(?:[^>]*?\\s+)?href=([\"'])(.*?)\\1");
            MatchCollection matches = regex.Matches(html);

            foreach (Match match in matches)
            {
                string urlResult = match.Groups[2].Value;
                if (urlResult.StartsWith("/url?q=") && urls.Count() < 7)
                {
                    urlResult = ExtractUrlFromQueryString(urlResult);
                    if (!string.IsNullOrWhiteSpace(urlResult) && !urls.Contains(urlResult) && urlResult.StartsWith("http"))
                        urls.Add(urlResult);
                }
            }
            urls.RemoveAll(x => x.Contains("https://maps.google.com/"));

            return urls;
        }
        string ExtractUrlFromQueryString(string input)
        {
            // Define a regular expression pattern to extract the URL
            string pattern = @"(?:\/url\?q=)(.*?)(?=&|$)";

            // Use Regex.Match to find the URL in the input string
            Match match = Regex.Match(input, pattern);

            // Check if a match is found
            if (match.Success)
            {
                // Extract the captured group value (URL)
                string url = match.Groups[1].Value;

                // Decode the URL to handle special characters
                url = System.Web.HttpUtility.UrlDecode(url);

                return url;
            }

            // Return an empty string if no match is found
            return string.Empty;
        }
    }

    public interface IHelper_WebResearch
    {
        public Task<List<List<string>>> Helper_WebResearch_List(List<string> queryList);
    }
}
