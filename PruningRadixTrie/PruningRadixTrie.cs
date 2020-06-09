using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace PruningRadixTrie
{
    /// <summary>
    /// Summary description for Trie
    /// </summary>
    public class PruningRadixTrie
    {
        private long termCount = 0;
        private long termCountLoaded = 0;

        //Trie node class
        private class Node
        {
            public List<(string key, Node node)> Children;

            //Does this node represent the last character in a word? 
            //0: no word; >0: is word (termFrequencyCount)
            public long TermFrequencyCount { get; set; }
            public long TermFrequencyCountChildMax { get; set; }

            public Node(long termfrequencyCount)
            {
                TermFrequencyCount = termfrequencyCount;
            }
        }

        //The trie
        private readonly Node trie;

        public PruningRadixTrie()
        {
            trie = new Node(0);
        }

        // Insert a word into the trie
        public void AddTerm(string term, long termFrequencyCount)
        {
            List<Node> nodeList = new List<Node>();
            AddTerm(trie, term, termFrequencyCount, 0, 0, nodeList);
        }

        private void UpdateMaxCounts(IEnumerable<Node> nodeList, long termFrequencyCount)
        {
            foreach (Node node in nodeList)
            {
                if (termFrequencyCount > node.TermFrequencyCountChildMax) 
                    node.TermFrequencyCountChildMax = termFrequencyCount;
            }
        }
        
        private void AddTerm(Node curr, string term, long termFrequencyCount, int id, int level, List<Node> nodeList)
        {
            try
            {
                nodeList.Add(curr);

                //test for common prefix (with possibly different suffix)
                int common = 0;
                if (curr.Children != null)
                { 
                    for (int j = 0; j < curr.Children.Count; j++)
                    {
                        (string key, Node node) = curr.Children[j];

                        for (int i = 0; i < Math.Min(term.Length, key.Length); i++) if (term[i] == key[i]) common = i + 1; else break;

                        if (common > 0)
                        {
                            //term already existed
                            //existing ab
                            //new      ab
                            if ((common == term.Length) && (common == key.Length))
                            {
                                if (node.TermFrequencyCount == 0) termCount++;
                                node.TermFrequencyCount += termFrequencyCount;
                                UpdateMaxCounts(nodeList, node.TermFrequencyCount);
                            }
                            //new is subkey
                            //existing abcd
                            //new      ab
                            //if new is shorter (== common), then node(count) and only 1. children add (clause2)
                            else if (common == term.Length)
                            {
                                //insert second part of oldKey as child 
                                Node child = new Node(termFrequencyCount);
                                child.Children = new List<(string, Node)>
                                {
                                    (key.Substring(common), node)
                                };
                                child.TermFrequencyCountChildMax = Math.Max(node.TermFrequencyCountChildMax, node.TermFrequencyCount);
                                UpdateMaxCounts(nodeList, termFrequencyCount);

                                //insert first part as key, overwrite old node
                                curr.Children[j] = (term.Substring(0, common), child);
                                //sort children descending by termFrequencyCountChildMax to start lookup with most promising branch
                                curr.Children.Sort((x, y) => y.Item2.TermFrequencyCountChildMax.CompareTo(x.Item2.TermFrequencyCountChildMax));
                                //increment termcount by 1
                                termCount++;
                            }
                            //if oldkey shorter (==common), then recursive addTerm (clause1)
                            //existing: te
                            //new:      test
                            else if (common == key.Length)
                            {
                                AddTerm(node, term.Substring(common), termFrequencyCount, id, level + 1, nodeList);
                            }
                            //old and new have common substrings
                            //existing: test
                            //new:      team
                            else
                            {
                                //insert second part of oldKey and of s as child 
                                Node child = new Node(0);//count       
                                child.Children = new List<(string, Node)>
                                {
                                    (key.Substring(common), node) ,
                                    (term.Substring(common), new Node(termFrequencyCount))
                                };
                                child.TermFrequencyCountChildMax = Math.Max(node.TermFrequencyCountChildMax, Math.Max(termFrequencyCount, node.TermFrequencyCount));
                                UpdateMaxCounts(nodeList, termFrequencyCount);

                                //insert first part as key. overwrite old node
                                curr.Children[j] = (term.Substring(0, common), child);
                                //sort children descending by termFrequencyCountChildMax to start lookup with most promising branch
                                curr.Children.Sort((x, y) => y.Item2.TermFrequencyCountChildMax.CompareTo(x.Item2.TermFrequencyCountChildMax));
                                //increment termcount by 1 
                                termCount++;
                            }
                            return;
                        }
                    }
                }

                // initialize dictionary if first key is inserted 
                if (curr.Children == null)
                {
                    curr.Children = new List<(string, Node)>
                    {
                        ( term, new Node(termFrequencyCount) )
                    };
                }
                else
                {
                    curr.Children.Add((term, new Node(termFrequencyCount)));
                    //sort children descending by termFrequencyCountChildMax to start lookup with most promising branch
                    curr.Children.Sort((x, y) => y.Item2.TermFrequencyCountChildMax.CompareTo(x.Item2.TermFrequencyCountChildMax));
                }
                termCount++;
                UpdateMaxCounts(nodeList, termFrequencyCount);
            }
            catch (Exception e) { Console.WriteLine("exception: " + term + " " + e.Message); }
        }

        private void FindAllChildTerms(string prefix, int topK, ref long termFrequencyCountPrefix, string prefixString, List<(string term, long termFrequencyCount)> results, bool pruning)
        {
            FindAllChildTerms(prefix, trie, topK, ref termFrequencyCountPrefix, prefixString, results, null, pruning);
        }

        private void FindAllChildTerms(string prefix, Node curr, int topK, ref long termfrequencyCountPrefix, string prefixString, List<(string term, long termFrequencyCount)> results, System.IO.StreamWriter file, bool pruning)
        {
            try
            {
                //pruning/early termination in radix trie lookup
                if (pruning && (topK > 0) && (results.Count == topK) && (curr.TermFrequencyCountChildMax <= results[topK - 1].termFrequencyCount)) return;

                //test for common prefix (with possibly different suffix)
                bool noPrefix = string.IsNullOrEmpty(prefix);

                if (curr.Children != null)
                {
                    foreach ((string key, Node node) in curr.Children)
                    {                     
                        //pruning/early termination in radix trie lookup
                        if (pruning && (topK > 0) && (results.Count == topK) && (node.TermFrequencyCount <= results[topK - 1].termFrequencyCount) && (node.TermFrequencyCountChildMax <= results[topK - 1].termFrequencyCount))
                        {
                            if (!noPrefix) break; else continue;
                        }                     

                        if (noPrefix || key.StartsWith(prefix))
                        {
                            if (node.TermFrequencyCount > 0)
                            {
                                if (prefix == key) termfrequencyCountPrefix = node.TermFrequencyCount;

                                //candidate                              
                                if (file != null) file.WriteLine(prefixString + key + "\t" + node.TermFrequencyCount.ToString());
                                else
                                if (topK > 0) AddTopKSuggestion(prefixString + key, node.TermFrequencyCount, topK, ref results); else results.Add((prefixString + key, node.TermFrequencyCount));                               
                            }

                            if ((node.Children != null) && (node.Children.Count > 0)) FindAllChildTerms("", node, topK, ref termfrequencyCountPrefix, prefixString + key, results, file, pruning);
                            if (!noPrefix) break;
                        }
                        else if (prefix.StartsWith(key))
                        {

                            if ((node.Children != null) && (node.Children.Count > 0)) FindAllChildTerms(prefix.Substring(key.Length), node, topK, ref termfrequencyCountPrefix, prefixString + key, results, file, pruning);
                            break;
                        }
                    }
                }
            }
            catch (Exception e) { Console.WriteLine("exception: " + prefix + " " + e.Message); }
        }

        public IEnumerable<(string term, long termFrequencyCount)> GetTopkTermsForPrefix(string prefix, int topK, out long termFrequencyCountPrefix, bool pruning=true)
        {
            List<(string term, long termFrequencyCount)> results = new List<(string term, long termFrequencyCount)>();

            //termFrequency of prefix, if it exists in the dictionary (even if not returned in the topK results due to low termFrequency)
            termFrequencyCountPrefix = 0;

            // At the end of the prefix, find all child words
            FindAllChildTerms(prefix, topK, ref termFrequencyCountPrefix, "", results,pruning);

            return results;
        }

        public void Serialize(string path)
        {
            //save only if new terms were added
            if (termCountLoaded == termCount) return;
            try
            {
                using (System.IO.StreamWriter file = new System.IO.StreamWriter(path))
                {
                    long prefixCount = 0;
                    FindAllChildTerms("", trie, 0, ref prefixCount, "", null, file,true);
                }
                Console.WriteLine(termCount.ToString("N0") + " terms written.");
            }
            catch (Exception e)
            {
                Console.WriteLine("Writing terms exception: " + e.Message);
            }
        }

        public void Load(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException(path);
            }
            if (!System.IO.File.Exists(path))
            {
                throw new FileNotFoundException(path);
            }

            using Stream corpusStream = System.IO.File.OpenRead(path);
            using StreamReader sr = new System.IO.StreamReader(corpusStream, System.Text.Encoding.UTF8, false);
            string line;

            //process a single line at a time only for memory efficiency
            while ((line = sr.ReadLine()) != null)
            {
                string[] columns = line.Split("\t");
                if (columns.Length == 2)
                {
                    if (long.TryParse(columns[1], out long count))
                    {
                        this.AddTerm(columns[0], count);
                    }
                }
            }
        }

        private class BinarySearchComparer : IComparer<(string term, long termFrequencyCount)>
        {
            public int Compare((string term, long termFrequencyCount) f1, (string term, long termFrequencyCount) f2)
            {
                return Comparer<long>.Default.Compare(f2.termFrequencyCount, f1.termFrequencyCount);//descending
            }
        }

        private void AddTopKSuggestion(string term, long termFrequencyCount, int topK, ref List<(string term, long termFrequencyCount)> results)
        {
            //at the end/highest index is the lowest value
            // >  : old take precedence for equal rank   
            // >= : new take precedence for equal rank 
            if ((results.Count < topK) || (termFrequencyCount >= results[topK - 1].termFrequencyCount))
            {
                int index = results.BinarySearch((term, termFrequencyCount), new BinarySearchComparer());
                if (index < 0) results.Insert(~index, (term, termFrequencyCount)); else results.Insert(index, (term, termFrequencyCount));

                if (results.Count > topK) results.RemoveAt(topK);
            }

        }

    }
}