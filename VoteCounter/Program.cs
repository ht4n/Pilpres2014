/*

Copyright 2014 Henry Tan Setiawan

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.

DISCLAIMER: The author of this program is not officially affiliated with any 
political party or KPU.go.id. This is a third party code to help people
consume the KPU feeds for data analysis. Any damage indirectly or directly
is not the author responsibility.
*/

using System;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading;
using System.Net;
using System.Web;
using System.IO;

namespace Pilpres2014
{
    
    using ThreeLevelDictionary = Dictionary<String, Dictionary<String, Dictionary<String, BinaryTally>>>;
    using TwoLevelDictionary = Dictionary<String, Dictionary<String, BinaryTally>>;

    public class BinaryTally
    {
        public UInt64 Counter1 = 0;
        public UInt64 Counter2 = 0;
        public UInt64 Total = 0;

        public String KecamatanName { get; set; }
        public String KecamatanCode { get; set; }
        public String KabupatenName { get; set; }
        public String KabupatenCode { get; set; }
        public String ProvinceName { get; set; }
        public String ProvinceCode { get; set; }
    }
    
    public class HierarchicalTally : BinaryTally
    {
        public ThreeLevelDictionary votingTable = new ThreeLevelDictionary();
        public ThreeLevelDictionary VotingTable { get { return this.votingTable; } }

        public void Add(BinaryTally tally, String provinceCode, String kabupatenCode, String kecamatanCode)
        {
            lock (this.votingTable)
            {                
                base.Counter1 += tally.Counter1;
                base.Counter2 += tally.Counter2;
                base.Total += tally.Counter1 + tally.Counter2;

                TwoLevelDictionary kabupatenDictionary;
                if (!this.votingTable.TryGetValue(provinceCode, out kabupatenDictionary))
                {
                    kabupatenDictionary = new TwoLevelDictionary();
                    this.votingTable.Add(provinceCode, kabupatenDictionary);
                }

                Dictionary<String, BinaryTally> kecamatanDictionary;
                if (!kabupatenDictionary.TryGetValue(kabupatenCode, out kecamatanDictionary))
                {
                    kecamatanDictionary = new Dictionary<string, BinaryTally>();
                    kabupatenDictionary.Add(kabupatenCode, kecamatanDictionary);
                }

                kecamatanDictionary.Add(kecamatanCode, tally);
            }
        }
    }

    public class VoteCounter
    {
        static readonly int s_retryCount = 5;
        static UInt64 s_invalidVoteCount = 0;
        static UInt64 s_validVoteCount = 0;
        static HierarchicalTally s_tallyTable = new HierarchicalTally();
        static int s_outstandingWorkItems = -1;
        static int s_threadCount = 4;
        static ManualResetEvent[] s_events;
        static int[] s_threadIds;

        static bool ParseCandidateVoteCount(String filterStart, String filterEnd, String resultPage, out UInt64 voteCount)
        {
            voteCount = 0;

            Regex rgx = new Regex(filterStart, RegexOptions.Singleline);
            Match m = rgx.Match(resultPage);
            if (!m.Success)
            {
                return false;
            }

            String payload = m.Value;
            int pos = payload.IndexOf(filterEnd);
            payload = payload.Substring(0, pos);

            rgx = new Regex("<td.*>(?<VoteCount>\\d*)</td>");
            MatchCollection matches = rgx.Matches(payload);
            foreach (Match match in matches)
            {
                // The last column has the total and that's what we are getting here
                voteCount = match.Groups["VoteCount"].Value == "" ? 0 : UInt64.Parse(match.Groups["VoteCount"].Value);                
            }

            ++s_validVoteCount;
            return true;
        }

        static void ParseVoteCount(String resultPage, BinaryTally tally)
        {
            tally.Counter1 = 0;
            tally.Counter2 = 0;
            UInt64 voteCount = 0;
            if (!ParseCandidateVoteCount(
                     "(<nobr>H\\. Prabowo Subianto - Ir\\. M\\. H\\. Hatta Rajasa</nobr>.*(<td.*>\\d*</td>)+)",
                     "</tr>",
                     resultPage, 
                     out voteCount))
            {
                ++s_invalidVoteCount;
                throw new InvalidOperationException("Failed to parse page");
            }
            tally.Counter1 = voteCount;

            voteCount = 0;
            if (!ParseCandidateVoteCount(
                    "(<nobr>Ir\\. H\\. Joko Widodo - Drs\\. H\\. M\\. Jusuf Kalla</nobr>.*(<td.*>\\d*</td>)+)", 
                    "</tr>",
                    resultPage, 
                    out voteCount))
            {
                ++s_invalidVoteCount;
                throw new InvalidOperationException("Failed to parse page");
            }            

            tally.Counter2 = voteCount;
            tally.Total = (tally.Counter1 + tally.Counter2);
        }

        static BinaryTally CountVotes(
            String provinceCode,
            String provinceName, 
            String kabupatenCode,
            String kabupatenName, 
            String kecamatanCode, 
            String kecamatanName)
        {
            String baseUrl = "http://pilpres2014.kpu.go.id/da1.php?cmd=select&grandparent={0}&parent={1}";

            StringBuilder sb = new StringBuilder();

            String url = String.Format(baseUrl, kabupatenCode, kecamatanCode);
            for (int i = 0; i < s_retryCount; ++i)
            {
                try
                {
                    // NOTES: this can be better performance if we use the WebClient pool
                    // but given that we do not need a super high throughput we can backtrack
                    // to a conventional method of instantiating WebClient each time
                    WebClient client = new WebClient();
                    sb.Append(client.DownloadString(url));

                    BinaryTally tally = new BinaryTally();
                    ParseVoteCount(sb.ToString(), tally);
                    tally.ProvinceCode = provinceCode;
                    tally.ProvinceName = provinceName;
                    tally.KabupatenCode = kabupatenCode;
                    tally.KabupatenName = kabupatenName;
                    tally.KecamatanCode = kecamatanCode;
                    tally.KecamatanName = kecamatanName;

                    return tally;
                }
                catch (WebException ex)
                {
                    Console.WriteLine("> Failed to fetch a page from {0}", url);
                    Console.WriteLine(ex);
                    Console.WriteLine("> Retry {0}", i);
                    Thread.Sleep(10000);
                }
            }
                       
            return null;
        }
        
        static void CountVotesHelper(
            String level, 
            ref String rprovinceName,
            ref String rprovinceCode,
            ref String rkabupatenName,
            ref String rkabupatenCode, 
            string levelCategory, 
            string areaCode, 
            string areaName, 
            StreamWriter sw)
        {
            String provinceName = rprovinceName;
            String provinceCode = rprovinceCode;
            String kabupatenName = rkabupatenName;
            String kabupatenCode = rkabupatenCode;

            // Roll tally at Province level
            if (level == "0")
            {
                rprovinceCode = areaCode;
                rprovinceName = areaName;
            }

            // Roll tally at Kabupaten level
            if (level == "1")
            {
                rkabupatenCode = areaCode;
                rkabupatenName = areaName;
            }

            // Process tally at Kecamatan level
            if (level == "2")
            {
                String kecamatanCode = areaCode;
                String kecamatanName = areaName;

                Interlocked.Increment(ref s_outstandingWorkItems);

                // Spin untils the number of outstanding work is less than max thread count
                SpinWait.SpinUntil(() => { return (s_outstandingWorkItems < s_threadCount); });

                {
                    ThreadPool.QueueUserWorkItem((Object threadContext) =>
                    {
                        Console.WriteLine("[ProcessingThreadStart:{0},{1},{2},{3},{4},{5}]",
                            provinceCode,
                            provinceName,
                            kabupatenCode,
                            kabupatenName,
                            kecamatanCode,
                            kecamatanName);

                        // Let it sleep a bit before becoming active ...
                        Thread.Sleep(new Random().Next(5000));

                        BinaryTally tally = CountVotes(
                            provinceCode, 
                            provinceName, 
                            kabupatenCode, 
                            kabupatenName, 
                            kecamatanCode,
                            kecamatanName);

                        if (tally == null)
                        {
                            Console.WriteLine("Tally cannot be obtained for {0} {1} {2} {3}", 
                                provinceCode, 
                                kabupatenCode, 
                                kecamatanCode, 
                                kecamatanName);
                        }
                        else
                        {
                            s_tallyTable.Add(tally, provinceCode, kabupatenCode, areaCode);
                        }

                        Console.WriteLine("{0},{1},{2},{3},{4},{5},{6},{7},{8}",
                                provinceCode,
                                provinceName,
                                kabupatenCode,
                                kabupatenName,
                                kecamatanCode,
                                kecamatanName,
                                tally.Counter1,
                                tally.Counter2,
                                tally.Total);

                        sw.WriteLine("{0},{1},{2},{3},{4},{5},{6},{7},{8}",
                                provinceCode,
                                provinceName,
                                kabupatenCode,
                                kabupatenName,
                                kecamatanCode,
                                kecamatanName,
                                tally.Counter1,
                                tally.Counter2,
                                tally.Total);

                        // Sets completion signal for event tid
                        Interlocked.Decrement(ref s_outstandingWorkItems);
                    });
                }
            }
        }

        static void CountVotes(String districtTableFile, String outputFile, int threadCount)
        {
            s_threadCount = threadCount;
            s_threadIds = new int[s_threadCount];
            for (int i = 0; i < s_threadCount; ++i)
            {
                s_threadIds[i] = i;
            }

            s_events = new ManualResetEvent[s_threadCount];
            for (int i = 0; i < s_threadCount; ++i)
            {
                s_events[i] = new ManualResetEvent(false);
            }

            String provinceCode = "";
            String provinceName = "";
            String kabupatenCode = "";
            String kabupatenName = "";

            using (StreamWriter sw = new StreamWriter(outputFile))
            {
                sw.AutoFlush = true;
                sw.WriteLine("#HEADER:ProvinceCode,ProvinceName,KabupatenCode,KabupatenName,KecamatanCode,KecamatanName,PrabowoHattaVotes,JokowiKallaVotes,TotalVotes");
                // Assumptions:
                // The table is sorted by columns from left-to-right
                using (StreamReader sr = new StreamReader(districtTableFile))
                {
                    string line = sr.ReadLine();
                    while (!sr.EndOfStream && !string.IsNullOrWhiteSpace(line))
                    {
                        if (line.StartsWith("#"))
                        {
                            line = sr.ReadLine();
                            continue;
                        }

                        String[] tokens = line.Split(',');
                        if (tokens.Length != 4)
                        {
                            Console.WriteLine("> !!Invalid input with more than 4 columns!: {0}", line);
                            continue;
                        }

                        CountVotesHelper(
                            tokens[0], 
                            ref provinceCode,
                            ref provinceName,
                            ref kabupatenCode,
                            ref kabupatenName, 
                            tokens[1], 
                            tokens[2], 
                            tokens[3], 
                            sw);
                       
                        line = sr.ReadLine();
                    }
                }
            }

            Dictionary<String, BinaryTally> provinceTallyMap = new Dictionary<string, BinaryTally>();
            Dictionary<String, BinaryTally> kabupatenTallyMap = new Dictionary<string, BinaryTally>();
            String logPayload = null;

            using (StreamWriter swtotal = new StreamWriter(outputFile + "-total.csv"))
            {
                BinaryTally totalTally = new BinaryTally();
                swtotal.WriteLine("#HEADER:PrabowoHattaVotes,PrabowoHattaVotesPercentage,JokowiKallaVotes,JokowiKallaVotesPercentage,TotalVotes");
                Console.WriteLine("#TOTAL:PrabowoHattaVotes,PrabowoHattaVotesPercentage,JokowiKallaVotes,JokowiKallaVotesPercentage,TotalVotes");

                ThreeLevelDictionary.Enumerator totalTallyEnumerator = s_tallyTable.VotingTable.GetEnumerator();
                while (totalTallyEnumerator.MoveNext())
                {
                    using (StreamWriter swProvince = new StreamWriter(outputFile + "-province.csv"))
                    {
                        TwoLevelDictionary.Enumerator provinceTallyEnumerator = totalTallyEnumerator.Current.Value.GetEnumerator();
                        swProvince.WriteLine("#HEADER:PrabowoHattaVotes,PrabowoHattaVotesPercentage,JokowiKallaVotes,JokowiKallaVotesPercentage,TotalVotes");
                        Console.WriteLine("#PROVINCE:PrabowoHattaVotes,PrabowoHattaVotesPercentage,JokowiKallaVotes,JokowiKallaVotesPercentage,TotalVotes");
                        BinaryTally provinceTally = new BinaryTally();

                        while (provinceTallyEnumerator.MoveNext())
                        {
                            using (StreamWriter swKabupaten = new StreamWriter(outputFile + "-kabupaten.csv"))
                            {
                                swKabupaten.WriteLine("#HEADER:PrabowoHattaVotes,PrabowoHattaVotesPercentage,JokowiKallaVotes,JokowiKallaVotesPercentage,TotalVotes");
                                swKabupaten.WriteLine("#KABUPATEN:PrabowoHattaVotes,PrabowoHattaVotesPercentage,JokowiKallaVotes,JokowiKallaVotesPercentage,TotalVotes");
                                Dictionary<String, BinaryTally>.Enumerator kabupatenTallyEnumerator = provinceTallyEnumerator.Current.Value.GetEnumerator();
                                
                                while (kabupatenTallyEnumerator.MoveNext())
                                {
                                    BinaryTally kabupatenTally = new BinaryTally();

                                    kabupatenTally.Counter1 += kabupatenTallyEnumerator.Current.Value.Counter1;
                                    kabupatenTally.Counter2 += kabupatenTallyEnumerator.Current.Value.Counter2;
                                    kabupatenTally.Total += kabupatenTallyEnumerator.Current.Value.Total;
                                    
                                    kabupatenTallyMap.Add(kabupatenTallyEnumerator.Current.Key, kabupatenTally);

                                    logPayload = String.Format("{0},{1:N2},{2},{3:N2},{4}",
                                        kabupatenTally.Counter1,
                                        kabupatenTally.Total == 0 ? 0 : (float)kabupatenTally.Counter1 / kabupatenTally.Total,
                                        kabupatenTally.Counter2,
                                        kabupatenTally.Total == 0 ? 0 : (float)kabupatenTally.Counter2 / kabupatenTally.Total,
                                        kabupatenTally.Total);
                                    Console.WriteLine(logPayload);
                                    swKabupaten.WriteLine(logPayload);

                                    // Sum each kabupaten to this province
                                    provinceTally.Counter1 += kabupatenTally.Counter1;
                                    provinceTally.Counter2 += kabupatenTally.Counter2;
                                    provinceTally.Total += kabupatenTally.Total;
                                }
                            }

                            provinceTallyMap.Add(provinceTallyEnumerator.Current.Key, provinceTally);

                            // Sum each province to total
                            totalTally.Counter1 += provinceTally.Counter1;
                            totalTally.Counter2 += provinceTally.Counter2;
                            totalTally.Total += provinceTally.Total;
                        }

                        logPayload = String.Format("{0},{1:N2},{2},{3:N2},{4}",
                                                    provinceTally.Counter1,
                                                    provinceTally.Total == 0 ? 0 : (float)provinceTally.Counter1 / provinceTally.Total,
                                                    provinceTally.Counter2,
                                                    provinceTally.Total == 0 ? 0 : (float)provinceTally.Counter2 / provinceTally.Total,
                                                    provinceTally.Total);

                        Console.WriteLine(logPayload);
                        swProvince.WriteLine(logPayload);
                    }
                }

                logPayload = String.Format("{0},{1:N2},{2},{3:N2},{4}",
                        totalTally.Counter1,
                        totalTally.Total == 0 ? 0 : (float)totalTally.Counter1 / totalTally.Total,
                        totalTally.Counter2,
                        totalTally.Total == 0 ? 0 : (float)totalTally.Counter2 / totalTally.Total,
                        totalTally.Total);

                Console.WriteLine(logPayload);
                swtotal.WriteLine(logPayload);                
            }
        }

        static void Main(string[] args)
        {
            if (args.Length != 3)
            {
                Console.WriteLine("Syntax: VoteCounter.exe <AreaCodeTable.csv> <OutputDir> <ThreadCount>");
                return;
            }

            String tableFile = args[0];
            String outputDir = args[1];
            int threadCount = int.Parse(args[2]);
            if (File.Exists(tableFile) == false)
            {
                Console.WriteLine("Table file {0} does not exist", tableFile);
                return;
            }

            if (!Directory.Exists(outputDir))
            {
                Console.WriteLine("> Output directory {0} does not exist", outputDir);
                Console.WriteLine("> Creating new directory {0}", outputDir);
                Directory.CreateDirectory(outputDir);
            }

            String outputFile = Path.Combine(outputDir, string.Format("KPU-Feeds-{0:yyyy-MM-dd_hh-mm-ss-tt}.csv", DateTime.Now));
            Console.WriteLine("> Output path: {0}", outputFile);

            Stopwatch sw = new Stopwatch();
            CountVotes(tableFile, outputFile, threadCount);

            Console.WriteLine("> Completed crawling in {0} minutes. Press any key to continue ...", sw.Elapsed.Minutes);
            Console.ReadLine();
        }
    }
}
