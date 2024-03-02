using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Reflection;
using System.Collections.Concurrent;

namespace Helper.Utils
{
    public class TextModificationUtils : ITextModificationUtils
    {
        protected List<string> ListOfSeparatorsForLines = new List<string> { Environment.NewLine, "\n", ". ", "\r" };
        protected List<string> ListOfSeparatorsForWords = new List<string> { "\t", " ", ",", "|", "-", "_", Environment.NewLine, "\n", ". " };
        public static ConcurrentDictionary<string, HashSet<string>> mainDict = new ConcurrentDictionary<string, HashSet<string>>();
        static object lockFillDict = new object();
        public static void FillDictionary()
        {
            lock(lockFillDict)
            {
                if (mainDict.Count() == 0)
                {
                    string exePath = Assembly.GetExecutingAssembly().Location;
                    string projectFolderPath = Path.GetDirectoryName(exePath);
                    projectFolderPath = Path.Combine(projectFolderPath, "synonyms.csv");
                    using (var reader = new StreamReader(projectFolderPath))
                    {
                        List<string> listA = new List<string>();
                        List<string> listB = new List<string>();
                        IDictionary<string, HashSet<string>> synonymsMap = new Dictionary<string, HashSet<string>>();
                        while (!reader.EndOfStream)
                        {
                            var line = reader.ReadLine();
                            var values = line.Split(',');

                            listA.Add(values[0]);
                            string[] synonyms = (values[1].Split('|'));
                            HashSet<string> hashset = new HashSet<string>(synonyms);
                            if (mainDict.ContainsKey(values[0]))
                            {
                                mainDict.GetValueOrDefault(values[0]).UnionWith(hashset);
                            }
                            else
                            {
                                _ = mainDict.TryAdd(values[0], hashset);
                            }
                        }
                    }
                }
            }
            
        }

        IList<string> PhrasesToBeRemoved = new List<string>() { "how much", "how many", "why not", "the", "is", "of", "to", "and", "in", "that", "with", "on", "for", "who", "what", "when", "where", "why", "how", "whose", "which", "isn't", "aren't", "wasn't", "weren't" };
        // Function to tokenize a string into individual words (terms)
        public List<string> Tokenize(string input)
        {
            if (mainDict.Count == 0)
            {
                FillDictionary();
            }
            input = input.Trim();
            input = input.ToLower();
            input = RemovePhrases(input);
            input = RemovePunctuation(input);
            // Convert List<string> of separators to a single string
            string separators = string.Join("", ListOfSeparatorsForWords);

            // Split the string into words using the allowed separators
            List<string> words = input.Split(separators.ToCharArray(), StringSplitOptions.RemoveEmptyEntries).ToList();


            return words;
        }

        string RemovePunctuation(string text)
        {
            // Remove punctuation using a regular expression
            string cleanedText = Regex.Replace(text, @"[\p{P}-[.]]+", "");

            return cleanedText;
        }
        public string Remove(string text)
        {
            // Create a regular expression pattern to match the words
            string pattern = @"\b" + Regex.Escape(string.Join("|", PhrasesToBeRemoved)) + @"(\W|$)";

            // Replace all occurrences of the words with an empty string
            return Regex.Replace(text, pattern, "");
        }
        string RemovePhrases(string text)
        {
            foreach (string phrase in PhrasesToBeRemoved)
            {
                // Escape special characters in the phrase and use a word boundary regex pattern
                string regexPattern = @"\b" + Regex.Escape(phrase) + @"\b";

                // Remove phrase using regex with word boundaries
                text = Regex.Replace(text, regexPattern, string.Empty, RegexOptions.IgnoreCase);
            }

            return text;
        }

        public Dictionary<int, string> ExtractLines(string text)
        {
            // Split the text into lines using the allowed separators
            List<string> lines = text.Split(ListOfSeparatorsForLines.ToArray(), StringSplitOptions.RemoveEmptyEntries).ToList();

            // Create a dictionary to store line number and line text
            Dictionary<int, string> lineDictionary = new Dictionary<int, string>();

            // Populate the dictionary with line number and line text
            for (int i = 0; i < lines.Count; i++)
            {
                lineDictionary[i + 1] = lines[i];
            }

            return lineDictionary;
        }

        // Function to create the term frequency dictionary from a list of words
        public IDictionary<string, HashSet<string>> mainDictFunc()
            {
                return mainDict;
            }
        public Dictionary<string, int> CreateTermFrequencyDictionary(List<string> words)
        {
            Dictionary<string, int> termFrequencyDict = new Dictionary<string, int>();

            foreach (string word in words)
            {
                if (termFrequencyDict.ContainsKey(word))
                {
                    termFrequencyDict[word]++;
                }
                else
                {
                    termFrequencyDict[word] = 1;
                }
            }

            return termFrequencyDict;
        }
    }
}
