using LemmaSharp.Classes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Helper.Utils
{
    public class CosineSimiliarityUtils : ICosineSimiliarityUtils
    {
        static Lemmatizer lemmatizer;
        string QueryString;
        Dictionary<string, int> vector2;
        ITextModificationUtils TextModificationUtilsObj;
        public CosineSimiliarityUtils(string queryString, ITextModificationUtils textModificationUtils) 
        {
            QueryString = queryString;
            TextModificationUtilsObj = textModificationUtils;
            var words = TextModificationUtilsObj.Tokenize(queryString);
            vector2 = TextModificationUtilsObj.CreateTermFrequencyDictionary(words);
            if (lemmatizer==null)
            {
                string exePath = Assembly.GetExecutingAssembly().Location;
                string projectFolderPath = Path.GetDirectoryName(exePath);
                projectFolderPath = Path.Combine(projectFolderPath, "mlteast-en.lem");
                using (var stream = File.OpenRead(projectFolderPath))
                {
                    lemmatizer = new Lemmatizer(stream);
                }
            }
                      
        }
        static object lockLemmatizer = new object();
        public static void FillLemmatizer()
        {
            lock(lockLemmatizer)
            {
                if (lemmatizer == null)
                {
                    string exePath = Assembly.GetExecutingAssembly().Location;
                    string projectFolderPath = Path.GetDirectoryName(exePath);
                    projectFolderPath = Path.Combine(projectFolderPath, "mlteast-en.lem");
                    using (var stream = File.OpenRead(projectFolderPath))
                    {
                        lemmatizer = new Lemmatizer(stream);
                    }
                }
            }

            
        }
        // Function to calculate the dot product of two vectors
        private double DotProduct(Dictionary<string, int> vector1, Dictionary<string, int> vector2)
        {
            Helper(ref vector1, ref vector2);
            return vector1.Sum(entry => entry.Value * (vector2.ContainsKey(entry.Key) ? vector2[entry.Key] : 0));
        }

        void Helper(ref Dictionary<string, int> vector11, ref Dictionary<string, int> vector22)
        {

            Dictionary<string, int> vector1 = new Dictionary<string, int>();
            Dictionary<string, int> vector2 = new Dictionary<string, int>();
            foreach (var item in vector11)
            {
                var lemmatizedKey = lemmatizer.Lemmatize(item.Key);
                if(vector1.ContainsKey(lemmatizedKey))
                {
                    vector1[lemmatizedKey] += item.Value;
                }
                else
                {
                    vector1.Add(lemmatizedKey, item.Value);
                }
            }
            foreach (var item in vector22)
            {
                var lemmatizedKey = lemmatizer.Lemmatize(item.Key);
                if (vector2.ContainsKey(lemmatizedKey))
                {
                    vector2[lemmatizedKey] += item.Value;
                }
                else
                {
                    vector2.Add(lemmatizedKey, item.Value);
                }
            }
            Dictionary<string, int> vector1New = new Dictionary<string, int>();
            Dictionary<string, int> vector2New = new Dictionary<string, int>();
            foreach (var item in vector1)
            {
                if (TextModificationUtilsObj.mainDictFunc().ContainsKey(item.Key))
                {
                    var synonymsOfCurrentKey = TextModificationUtilsObj.mainDictFunc()[item.Key];
                    var res = IfSynonymsPresent(vector1New, synonymsOfCurrentKey);
                    if (res!=null)
                    {
                        vector1New[res.Item1] += item.Value;
                    }
                    else
                    {
                        if(!vector1New.ContainsKey(item.Key))
                        vector1New.Add(item.Key, item.Value);
                    }
                }
            }
            foreach (var item in vector2)
            {
                if (TextModificationUtilsObj.mainDictFunc().ContainsKey(item.Key))
                {
                    var synonymsOfCurrentKey = TextModificationUtilsObj.mainDictFunc()[item.Key];
                    var res = IfSynonymsPresent(vector2New, synonymsOfCurrentKey);
                    if (res != null)
                    {
                        vector2New[res.Item1] += item.Value;
                    }
                    else
                    {
                        if (!vector2New.ContainsKey(item.Key))
                            vector2New.Add(item.Key, item.Value);
                    }
                }
            }

            //Dictionary<string, int> vector1NewNew = new Dictionary<string, int>();
            Dictionary<string, int> vector2NewNew = new Dictionary<string, int>();

            foreach (var item in vector2New)
            {
                var syn = TextModificationUtilsObj.mainDictFunc()[item.Key];
                    var res = IfSynonymsPresent(vector1New,syn);
                if(res!=null)
                {
                    if(!vector2NewNew.ContainsKey(res.Item1))
                    vector2NewNew.Add(res.Item1, item.Value);
                }
                else
                {
                    if (!vector2NewNew.ContainsKey(item.Key))
                        vector2NewNew.Add(item.Key, item.Value);
                }
            }
            vector1 = vector1New;
            vector2 = vector2NewNew;


        }

        Tuple<string,int> IfSynonymsPresent(Dictionary<string, int> vector1New, HashSet<string> synonyms)
        {
            foreach (var item in vector1New)
            {
               if(synonyms.Contains(item.Key))
               {
                    return new Tuple<string, int>(item.Key, item.Value);
               }

            }
            return null;
        }

        // Function to calculate the magnitude of a vector
        public double Magnitude(Dictionary<string, int> vector)
        {
            return Math.Sqrt(vector.Values.Sum(value => value * value));
        }
        // Function to calculate cosine similarity between two vectors
        public double CalculateCosineSimilarity(string str)
        {

                Dictionary<string, int> vector1 = TextModificationUtilsObj.CreateTermFrequencyDictionary(TextModificationUtilsObj.Tokenize(str));
                double dotProduct = DotProduct(vector1, vector2);
                double magnitude1 = Magnitude(vector1);
                double magnitude2 = Magnitude(vector2);

                if (magnitude1 == 0 || magnitude2 == 0)
                {
                    return 0; // Handle zero magnitude (avoid division by zero)
                }

                return dotProduct / (magnitude1 * magnitude2);
            
            
        }


    }
}
