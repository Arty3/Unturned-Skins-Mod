using System;
using System.Linq;
using System.Collections.Generic;

using Unturned.SystemEx;
using SDG.Provider;

using static SkinsModule.ModuleLogger;

namespace SkinsModule
{
    /*
        BKTree class for efficient fuzzy string search.
        For the econInfo dict which has a constrain of < 3000 entries (probably),
        the search time is O(log n) so decently fast while balancing matching features
    */
    public class BKTree
    {
        private class Node
        {
            public UnturnedEconInfo         Info;
            public Dictionary<int, Node>    Children;

            public Node(UnturnedEconInfo info)
            {
                Info        = info;
                Children    = new Dictionary<int, Node>();
            }
        }

        private class SearchResult
        {
            public UnturnedEconInfo Info    { get; set; }
            public double           Score   { get; set; }
        }

        private Node         root;
        private HashSet<int> foundItems;

        public BKTree(IEnumerable<UnturnedEconInfo> data)
        {
			Log("Building EconInfo BKTree...");
			foreach (var info in data)
                if (!EconInfoLoader.isAchievementItem(info.itemdefid))
                    Add(info);

            foundItems = new HashSet<int>();
        }

        private int LevenshteinDistance(string a, string b)
        {
            int[,] dp = new int[a.Length + 1, b.Length + 1];

            for (int i = 0; i <= a.Length; ++i) dp[i, 0] = i;
            for (int j = 0; j <= b.Length; ++j) dp[0, j] = j;

            for (int i = 1; i <= a.Length; ++i)
                for (int j = 1; j <= b.Length; ++j)
                    dp[i, j] = Math.Min(
                        Math.Min(
                            dp[i - 1, j] + 1, 
                            dp[i, j - 1] + 1),
                        dp[i - 1, j - 1] + (
                        (a[i - 1] == b[j - 1]) ? 0 : 1)
                    );

            return dp[a.Length, b.Length];
        }

        public void Add(UnturnedEconInfo info)
        {
            if (root == null)
            {
                root = new Node(info);
                return;
            }

            Node current = root;
            while (true)
            {
                int distance = LevenshteinDistance(info.name, current.Info.name);
                if (!current.Children.ContainsKey(distance))
                {
                    current.Children[distance] = new Node(info);
                    break;
                }
                current = current.Children[distance];
            }
        }

        private double CalculateScore(string query, string name, int distance)
        {
            return 1.0 - ((double)distance / Math.Max(query.Length, name.Length));
        }

        private List<SearchResult> FastPrefixSearch(string query, int desiredResults)
        {
            List<SearchResult>  results = new List<SearchResult>();
            Stack<Node>         stack   = new Stack<Node>();

            stack.Push(root);

            while (stack.Count > 0 && results.Count < desiredResults)
            {
                Node node = stack.Pop();
                if (!foundItems.Contains(node.Info.itemdefid) &&
                    node.Info.name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(new SearchResult
                    {
                        Info = node.Info,
                        Score = 1.0 + (1.0 / node.Info.name.Length)
                    });

                    foundItems.Add(node.Info.itemdefid);
                }

                foreach (Node child in node.Children.Values)
                    stack.Push(child);
            }

            return results;
        }

        private bool IsGoodMatch(string query, string name, int maxDistance, out int distance)
        {
            name = name.ToLowerInvariant();
            distance = maxDistance + 1;

            if (name.Contains(query, StringComparison.OrdinalIgnoreCase))
                return true;

            string[] words = name.Split(' ');
            foreach (var word in words)
            {
                distance = LevenshteinDistance(query, word);
                int allowedDistance = query.Length <= 4 ? 1 : maxDistance;

                if (distance <= allowedDistance &&
                    Math.Abs(word.Length - query.Length) <= maxDistance + 1)
                    return true;
            }

            return false;
        }

        public List<UnturnedEconInfo> Search(string query, int maxDistance, int itemsPerPage)
        {
            Log("Searching EconInfo BKTree...");

            if (root == null || string.IsNullOrEmpty(query))
                return new List<UnturnedEconInfo>();

            query = query.ToLowerInvariant();

            List<SearchResult> results = FastPrefixSearch(query, itemsPerPage);

            if (results.Count >= itemsPerPage)
                return results.OrderByDescending(r => r.Score)
                              .Take(itemsPerPage)
                              .Select(r => r.Info)
                              .ToList();

            Stack<Node> stack = new Stack<Node>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                Node node = stack.Pop();

                if (foundItems.Contains(node.Info.itemdefid))
                    continue;

                string name = node.Info.name.ToLowerInvariant();

                if (IsGoodMatch(query, name, maxDistance, out int distance))
                {
                    results.Add(new SearchResult
                    {
                        Info = node.Info,
                        Score = CalculateScore(query, name, distance)
                    });

                    foundItems.Add(node.Info.itemdefid);
                }

                foreach (var pair in node.Children)
                        stack.Push(pair.Value);
            }

            foundItems.Clear();

            return results.OrderByDescending(r => r.Score)
                          .Select(r => r.Info)
                          .ToList();
        }
    }
}
