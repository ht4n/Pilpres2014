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

        // A simple check that Counter1 + Counter2 must equates to Total
        public bool CheckVoteIntegrity()
        {
            if ((Counter1 + Counter2) != Total)
            {
                return false;
            }

            return true;
        }
    }
    
    public class HierarchicalTallyDa1 : BinaryTally
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

    public class HierarchicalTallyDb1 : BinaryTally
    {
        public TwoLevelDictionary votingTable = new TwoLevelDictionary();
        public TwoLevelDictionary VotingTable { get { return this.votingTable; } }

        public void Add(BinaryTally tally, String provinceCode, String kabupatenCode)
        {
            lock (this.votingTable)
            {
                base.Counter1 += tally.Counter1;
                base.Counter2 += tally.Counter2;
                base.Total += tally.Counter1 + tally.Counter2;

                Dictionary<String, BinaryTally> kabupatenDictionary;
                if (!this.votingTable.TryGetValue(provinceCode, out kabupatenDictionary))
                {
                    kabupatenDictionary = new Dictionary<String, BinaryTally>();
                    this.votingTable.Add(provinceCode, kabupatenDictionary);
                }
                
                kabupatenDictionary.Add(kabupatenCode, tally);
            }
        }
    }

    public class HierarchicalTallyDc1 : BinaryTally
    {
        public Dictionary<String, BinaryTally> votingTable = new Dictionary<String, BinaryTally>();
        public Dictionary<String, BinaryTally> VotingTable { get { return this.votingTable; } }

        public void Add(BinaryTally tally, String provinceCode)
        {
            lock (this.votingTable)
            {
                base.Counter1 += tally.Counter1;
                base.Counter2 += tally.Counter2;
                base.Total += tally.Counter1 + tally.Counter2;
                
                if (!this.votingTable.ContainsKey(provinceCode))
                {
                    this.votingTable.Add(provinceCode, tally);
                }
                else
                {
                    throw new InvalidDataException(String.Format("Duplicate key {0} found", provinceCode));
                }
            }
        }
    }

    public class VoteCounter
    {
        static readonly int s_retryCount = 5;
        static UInt64 s_invalidVoteCount = 0;
        static UInt64 s_validVoteCount = 0;
        static HierarchicalTallyDa1 s_tallyTableDa1 = new HierarchicalTallyDa1();
        static HierarchicalTallyDb1 s_tallyTableDb1 = new HierarchicalTallyDb1();
        static HierarchicalTallyDc1 s_tallyTableDc1 = new HierarchicalTallyDc1();

        static int s_outstandingWorkItems = -1;
        static int s_threadCount = 4;
        static ManualResetEvent[] s_events;
        static int[] s_threadIds;
        static int s_itemCount = 0;
        static List<String> s_badDataList = new List<string>();

        // This variable is for debugging. If it is not for debugging set this to UINT32_MAX
        static UInt32 s_maxItems = UInt32.MaxValue;

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

        static BinaryTally CountVotesDa1(
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

        static BinaryTally CountVotesDb1(
            String provinceCode,
            String provinceName,
            String kabupatenCode,
            String kabupatenName)
        {
            String baseUrl = "http://pilpres2014.kpu.go.id/db1.php?cmd=select&grandparent={0}&parent={1}";

            StringBuilder sb = new StringBuilder();

            String url = String.Format(baseUrl, provinceCode, kabupatenCode);
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

        static BinaryTally CountVotesDc1(
            String provinceCode,
            String provinceName)
        {
            String baseUrl = "http://pilpres2014.kpu.go.id/dc1.php?cmd=select&grandparent=0&parent={0}";

            StringBuilder sb = new StringBuilder();

            String url = String.Format(baseUrl, provinceCode);
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
        
        static void CountVotesHelperDa1(
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
                    WaitCallback callback = (Object threadContext) =>
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

                        BinaryTally tally = CountVotesDa1(
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
                            s_tallyTableDa1.Add(tally, provinceCode, kabupatenCode, areaCode);
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
                    };

                    ThreadPool.QueueUserWorkItem(callback);
                }
            }
        }

        static void CountVotesHelperDb1(
           String level,
           ref String rprovinceName,
           ref String rprovinceCode,
           string levelCategory,
           string areaCode,
           string areaName,
           StreamWriter sw)
        {
            String provinceName = rprovinceName;
            String provinceCode = rprovinceCode;
            
            // Roll tally at Province level
            if (level == "0")
            {
                rprovinceCode = areaCode;
                rprovinceName = areaName;
            }

            // Roll tally at Kabupaten level
            if (level == "1")
            {
                String kabupatenCode = areaCode;
                String kabupatenName = areaName;
            
                Interlocked.Increment(ref s_outstandingWorkItems);

                // Spin untils the number of outstanding work is less than max thread count
                SpinWait.SpinUntil(() => { return (s_outstandingWorkItems < s_threadCount); });

                {
                    WaitCallback callback = (Object threadContext) =>
                    {
                        Console.WriteLine("[ProcessingThreadStart:{0},{1},{2},{3}]",
                            provinceCode,
                            provinceName,
                            kabupatenCode,
                            kabupatenName);

                        // Let it sleep a bit before becoming active ...
                        Thread.Sleep(new Random().Next(5000));

                        BinaryTally tally = CountVotesDb1(
                            provinceCode,
                            provinceName,
                            kabupatenCode,
                            kabupatenName);

                        if (tally == null)
                        {
                            Console.WriteLine("Tally cannot be obtained for {0} {1} {2} {3}",
                                provinceCode,
                                kabupatenCode);
                        }
                        else
                        {
                            s_tallyTableDb1.Add(tally, provinceCode, kabupatenCode);
                        }

                        Console.WriteLine("{0},{1},{2},{3},{4},{5},{6}",
                                provinceCode,
                                provinceName,
                                kabupatenCode,
                                kabupatenName,
                                tally.Counter1,
                                tally.Counter2,
                                tally.Total);

                        sw.WriteLine("{0},{1},{2},{3},{4},{5},{6}",
                                provinceCode,
                                provinceName,
                                kabupatenCode,
                                kabupatenName,
                                tally.Counter1,
                                tally.Counter2,
                                tally.Total);

                        // Sets completion signal for event tid
                        Interlocked.Decrement(ref s_outstandingWorkItems);
                    };

                    ThreadPool.QueueUserWorkItem(callback);
                }
            }
        }

        static void CountVotesHelperDc1(
          String level,
          string levelCategory,
          string areaCode,
          string areaName,
          StreamWriter sw)
        {            
            // Roll tally at Province level
            if (level == "0")
            {
                String provinceCode = areaCode;
                String provinceName = areaName;
         
                Interlocked.Increment(ref s_outstandingWorkItems);

                // Spin untils the number of outstanding work is less than max thread count
                SpinWait.SpinUntil(() => { return (s_outstandingWorkItems < s_threadCount); });

                {
                    WaitCallback callback = (Object threadContext) =>
                    {
                        Console.WriteLine("[ProcessingThreadStart:{0},{1}]",
                            provinceCode,
                            provinceName);

                        // Let it sleep a bit before becoming active ...
                        Thread.Sleep(new Random().Next(5000));

                        BinaryTally tally = CountVotesDc1(
                            provinceCode,
                            provinceName);

                        if (tally == null)
                        {
                            Console.WriteLine("Tally cannot be obtained for {0}", provinceCode);
                        }
                        else
                        {
                            s_tallyTableDc1.Add(tally, provinceCode);
                        }

                        Console.WriteLine("{0},{1},{2},{3},{4}",
                                provinceCode,
                                provinceName,
                                tally.Counter1,
                                tally.Counter2,
                                tally.Total);

                        sw.WriteLine("{0},{1},{2},{3},{4}",
                                provinceCode,
                                provinceName,
                                tally.Counter1,
                                tally.Counter2,
                                tally.Total);

                        // Sets completion signal for event tid
                        Interlocked.Decrement(ref s_outstandingWorkItems);
                    };

                    ThreadPool.QueueUserWorkItem(callback);
                }
            }
        }

        static void CountVotesDa1(String districtTableFile, String outputFile, int threadCount)
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

            Stopwatch timer = new Stopwatch();
            timer.Start();
            String tmpOutputFile = Path.Combine(Path.GetDirectoryName(outputFile), Path.GetFileNameWithoutExtension(outputFile) + ".dba1.tmp");
            using (StreamWriter sw = new StreamWriter(tmpOutputFile))
            {
                sw.AutoFlush = true;
                sw.WriteLine("#HEADER:ProvinceCode,ProvinceName,KabupatenCode,KabupatenName,KecamatanCode,KecamatanName,PrabowoHattaVotes,JokowiKallaVotes,TotalVotes");

                // Assumptions:
                // The table is sorted by columns from left-to-right
                using (StreamReader sr = new StreamReader(districtTableFile))
                {
                    string line = sr.ReadLine();
                    while (!sr.EndOfStream && !string.IsNullOrWhiteSpace(line) && s_itemCount++ < s_maxItems)
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

                        CountVotesHelperDa1(
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

                    timer.Stop();
                    Console.WriteLine("> Crawler has finished iterating all provinces/kabupatens/kecamatans in {0} minutes", timer.Elapsed.Minutes);
                    Console.WriteLine("> Waiting until all outstanding worker threads completed their jobs ... (timeout in 30 secs)");
                    SpinWait.SpinUntil(() => { return s_outstandingWorkItems == -1; }, 10000);
                }
            }

            GenerateSummaryDa1(outputFile);
        }

        static void CountVotesDb1(String districtTableFile, String outputFile, int threadCount)
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

            Stopwatch timer = new Stopwatch();
            timer.Start();
            String tmpOutputFile = Path.Combine(Path.GetDirectoryName(outputFile), Path.GetFileNameWithoutExtension(outputFile) + ".db1.tmp");
            using (StreamWriter sw = new StreamWriter(tmpOutputFile))
            {
                sw.AutoFlush = true;
                sw.WriteLine("#HEADER:ProvinceCode,ProvinceName,KabupatenCode,KabupatenName,PrabowoHattaVotes,JokowiKallaVotes,TotalVotes");

                // Assumptions:
                // The table is sorted by columns from left-to-right
                using (StreamReader sr = new StreamReader(districtTableFile))
                {
                    string line = sr.ReadLine();
                    while (!sr.EndOfStream && !string.IsNullOrWhiteSpace(line) && s_itemCount++ < s_maxItems)
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

                        CountVotesHelperDb1(
                            tokens[0],
                            ref provinceCode,
                            ref provinceName,
                            tokens[1],
                            tokens[2],
                            tokens[3],
                            sw);

                        line = sr.ReadLine();
                    }

                    timer.Stop();
                    Console.WriteLine("> Crawler has finished iterating all provinces/kabupatens/kecamatans in {0} minutes", timer.Elapsed.Minutes);
                    Console.WriteLine("> Waiting until all outstanding worker threads completed their jobs ... (timeout in 30 secs)");
                    SpinWait.SpinUntil(() => { return s_outstandingWorkItems == -1; }, 10000);
                }
            }

            GenerateSummaryDb1(outputFile);
        }

        static void CountVotesDc1(String districtTableFile, String outputFile, int threadCount)
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

            Stopwatch timer = new Stopwatch();
            timer.Start();
            String tmpOutputFile = Path.Combine(Path.GetDirectoryName(outputFile), Path.GetFileNameWithoutExtension(outputFile) + ".dc1.tmp");
            using (StreamWriter sw = new StreamWriter(tmpOutputFile))
            {
                sw.AutoFlush = true;
                sw.WriteLine("#HEADER:ProvinceCode,ProvinceName,PrabowoHattaVotes,JokowiKallaVotes,TotalVotes");

                // Assumptions:
                // The table is sorted by columns from left-to-right
                using (StreamReader sr = new StreamReader(districtTableFile))
                {
                    string line = sr.ReadLine();
                    while (!sr.EndOfStream && !string.IsNullOrWhiteSpace(line) && s_itemCount++ < s_maxItems)
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

                        CountVotesHelperDc1(
                            tokens[0],
                            tokens[1],
                            tokens[2],
                            tokens[3],
                            sw);

                        line = sr.ReadLine();
                    }

                    timer.Stop();
                    Console.WriteLine("> Crawler has finished iterating all provinces/kabupatens/kecamatans in {0} minutes", timer.Elapsed.Minutes);
                    Console.WriteLine("> Waiting until all outstanding worker threads completed their jobs ... (timeout in 30 secs)");
                    SpinWait.SpinUntil(() => { return s_outstandingWorkItems == -1; }, 10000);
                }
            }

            GenerateSummaryDc1(outputFile);
        }

        static void GenerateSummaryDa1(String outputFile)
        {
            String logPayload = null;
            String stringFormatNation = "{{\n    \"PrabowoHattaVotes\":\"{0}\",\n    \"PrabowoHattaPercentage\":\"{1:N4}\",\n    \"JokowiKallaVotes\":\"{2}\",\n    \"JokowiKallaPercentage\":\"{3:N4}\",\n    \"Total\":\"{4}\"\n}}";
            String stringFormatProvince = "{{\n    \"Province\":\"{0}\",\n    \"PrabowoHattaVotes\":\"{1}\",\n    \"PrabowoHattaPercentage\":\"{2:N4}\",\n    \"JokowiKallaVotes\":\"{3}\",\n    \"JokowiKallaPercentage\":\"{4:N4}\",\n    \"Total\":\"{5}\"\n}}";
            String stringFormatKabupaten = "{{\n    \"ProvinceCode\":\"{0}\",\n    \"ProvinceName\":\"{1}\",\n    \"KabupatenCode\":\"{2}\",\n    \"KabupatenName\":\"{3}\",\n    \"KecamatanCode\":\"{4}\",\n    \"KecamatanName\":\"{5}\",\n    \"PrabowoHattaVotes\":\"{6}\",\n    \"PrabowoHattaPercentage\":\"{7:N4}\",\n    \"JokowiKallaVotes\":\"{8}\",\n    \"JokowiKallaPercentage\":\"{9:N4}\",\n    \"Total\":\"{10}\"\n}}";
            bool[] firstItems = new bool[2];
            for (int i = 0; i < 2; ++i)
            {
                firstItems[i] = true;
            }

            String totalOutputFile = Path.Combine(Path.GetDirectoryName(outputFile), Path.GetFileNameWithoutExtension(outputFile) + "-total.json");
            String totalProvinceOutputFile = Path.Combine(Path.GetDirectoryName(outputFile), Path.GetFileNameWithoutExtension(outputFile) + "-province.json");
            String totalKabupatenOutputFile = Path.Combine(Path.GetDirectoryName(outputFile), Path.GetFileNameWithoutExtension(outputFile) + "-kabupaten.json");

            using (StreamWriter swCSVTotal = new StreamWriter(Path.Combine(Path.GetDirectoryName(outputFile), Path.GetFileNameWithoutExtension(outputFile) + ".csv")))
            {
                swCSVTotal.WriteLine("#HEADER:ProvinceCode,ProvinceName,KabupatenCode,KabupatenName,KecamatanCode,KecamatanName,PrabowoHattaVotes,JokowiKallaVotes,TotalVotes");

                using (StreamWriter swtotal = new StreamWriter(totalOutputFile))
                {
                    BinaryTally nationTally = new BinaryTally();
                    swtotal.WriteLine("[");

                    using (StreamWriter swProvince = new StreamWriter(totalProvinceOutputFile))
                    {
                        swProvince.WriteLine("[");

                        ThreeLevelDictionary.Enumerator provinceEnumeerator = s_tallyTableDa1.VotingTable.GetEnumerator();
                        while (provinceEnumeerator.MoveNext())
                        {
                            BinaryTally provinceTally = new BinaryTally();
                            String provinceName = "";

                            using (StreamWriter swKabupaten = new StreamWriter(totalKabupatenOutputFile))
                            {
                                swKabupaten.WriteLine("[");

                                TwoLevelDictionary.Enumerator kabupatenEnumerator = provinceEnumeerator.Current.Value.GetEnumerator();
                                while (kabupatenEnumerator.MoveNext())
                                {
                                    BinaryTally kabupatenTally = new BinaryTally();
                                    Dictionary<String, BinaryTally>.Enumerator kecamatanEnumerator = kabupatenEnumerator.Current.Value.GetEnumerator();
                                    while (kecamatanEnumerator.MoveNext())
                                    {
                                        provinceName = kecamatanEnumerator.Current.Value.ProvinceName;

                                        // Dump complete raw data to CSV
                                        swCSVTotal.WriteLine("{0},{1},{2},{3},{4},{5},{6},{7},{8}",
                                            kecamatanEnumerator.Current.Value.ProvinceCode,
                                            kecamatanEnumerator.Current.Value.ProvinceName,
                                            kecamatanEnumerator.Current.Value.KabupatenCode,
                                            kecamatanEnumerator.Current.Value.KabupatenName,
                                            kecamatanEnumerator.Current.Value.KecamatanCode,
                                            kecamatanEnumerator.Current.Value.KecamatanName,
                                            kecamatanEnumerator.Current.Value.Counter1,
                                            kecamatanEnumerator.Current.Value.Counter2,
                                            kecamatanEnumerator.Current.Value.Total);

                                        logPayload = String.Format(stringFormatKabupaten,
                                            kecamatanEnumerator.Current.Value.ProvinceCode,
                                            kecamatanEnumerator.Current.Value.ProvinceName,
                                            kecamatanEnumerator.Current.Value.KabupatenCode,
                                            kecamatanEnumerator.Current.Value.KabupatenName,
                                            kecamatanEnumerator.Current.Value.KecamatanCode,
                                            kecamatanEnumerator.Current.Value.KecamatanName,
                                            kecamatanEnumerator.Current.Value.Counter1,
                                            kecamatanEnumerator.Current.Value.Total == 0 ? 0 : ((float)kecamatanEnumerator.Current.Value.Counter1 / kecamatanEnumerator.Current.Value.Total) * 100,
                                            kecamatanEnumerator.Current.Value.Counter2,
                                            kecamatanEnumerator.Current.Value.Total == 0 ? 0 : ((float)kecamatanEnumerator.Current.Value.Counter2 / kecamatanEnumerator.Current.Value.Total) * 100,
                                            kecamatanEnumerator.Current.Value.Total);

                                        if (firstItems[1] == true)
                                        {
                                            firstItems[1] = false;
                                        }
                                        else
                                        {
                                            Console.WriteLine(",");
                                            swKabupaten.WriteLine(",");
                                        }

                                        Console.Write(logPayload);
                                        swKabupaten.Write(logPayload);

                                        if (!kecamatanEnumerator.Current.Value.CheckVoteIntegrity())
                                        {
                                            // Moves on but logs these data for reporting
                                            String log = String.Format("Bad data integrity for Kecamatan {0}:{1}, vote1 {2} plus vote2 {3} does not equate to total {4}",
                                                kecamatanEnumerator.Current.Value.KecamatanCode,
                                                kecamatanEnumerator.Current.Value.KecamatanName,
                                                kecamatanEnumerator.Current.Value.Counter1,
                                                kecamatanEnumerator.Current.Value.Counter2,
                                                kecamatanEnumerator.Current.Value.Total);

                                            Console.WriteLine(log);
                                            s_badDataList.Add(log);
                                        }

                                        // Sum each kabupaten to this province
                                        kabupatenTally.Counter1 += kecamatanEnumerator.Current.Value.Counter1;
                                        kabupatenTally.Counter2 += kecamatanEnumerator.Current.Value.Counter2;
                                        kabupatenTally.Total += kecamatanEnumerator.Current.Value.Total;
                                    }

                                    if (!kabupatenTally.CheckVoteIntegrity())
                                    {
                                        // Moves on but logs these data for reporting
                                        String log = String.Format("Bad data integrity for Kabupaten {0}:{1}, vote1 {2} plus vote2 {3} does not equate to total {4}",
                                            kecamatanEnumerator.Current.Value.KabupatenCode,
                                            kecamatanEnumerator.Current.Value.KabupatenName,
                                            kabupatenTally.Counter1,
                                            kabupatenTally.Counter2,
                                            kabupatenTally.Total);

                                        Console.WriteLine(log);
                                        s_badDataList.Add(log);
                                    }

                                    provinceTally.Counter1 += kabupatenTally.Counter1;
                                    provinceTally.Counter2 += kabupatenTally.Counter2;
                                    provinceTally.Total += kabupatenTally.Total;
                                }

                                swKabupaten.WriteLine("]");
                            }

                            logPayload = String.Format(stringFormatProvince,
                                                       provinceName,
                                                       provinceTally.Counter1,
                                                       provinceTally.Total == 0 ? 0 : ((float)provinceTally.Counter1 / provinceTally.Total) * 100,
                                                       provinceTally.Counter2,
                                                       provinceTally.Total == 0 ? 0 : ((float)provinceTally.Counter2 / provinceTally.Total) * 100,
                                                       provinceTally.Total);

                            if (firstItems[0] == true)
                            {
                                firstItems[0] = false;
                            }
                            else
                            {
                                Console.WriteLine(",");
                                swProvince.WriteLine(",");
                            }

                            Console.Write(logPayload);
                            swProvince.Write(logPayload);

                            firstItems[0] = false;

                            if (!provinceTally.CheckVoteIntegrity())
                            {
                                // Moves on but log for reporting
                                String log = String.Format("Data for Province {0} is invalid the sum of the vote1 {1} and vote2 {2} does not equate to {3}",
                                        provinceName,
                                        provinceTally.Counter1,
                                        provinceTally.Counter2,
                                        provinceTally.Total);

                                Console.WriteLine(log);
                                s_badDataList.Add(log);
                            }

                            // Sum each province to total
                            nationTally.Counter1 += provinceTally.Counter1;
                            nationTally.Counter2 += provinceTally.Counter2;
                            nationTally.Total += provinceTally.Total;
                        }

                        swProvince.WriteLine("]");
                    }


                    logPayload = String.Format(stringFormatNation,
                            nationTally.Counter1,
                            nationTally.Total == 0 ? 0 : ((float)nationTally.Counter1 / nationTally.Total) * 100,
                            nationTally.Counter2,
                            nationTally.Total == 0 ? 0 : ((float)nationTally.Counter2 / nationTally.Total) * 100,
                            nationTally.Total);

                    if (!nationTally.CheckVoteIntegrity())
                    {
                        // Moves on but log for reporting
                        String log = String.Format("Data for Nation level is invalid the sum of the vote1 {0} and vote2 {1} does not equate to {2}",
                                nationTally.Counter1,
                                nationTally.Counter2,
                                nationTally.Total);

                        Console.WriteLine(log);
                        s_badDataList.Add(log);
                    }

                    Console.WriteLine(logPayload);
                    swtotal.WriteLine(logPayload);
                    swtotal.WriteLine("]");
                }
            }               


            // Do bad data reportings
            Console.WriteLine("========================== DA1 BAD DATA REPORTING ============================");
            foreach (String badData in s_badDataList)
            {
                Console.WriteLine("> BAD DATA: {0}", badData);
                s_badDataList.Add(badData);
            }
            Console.WriteLine("======================== END OF BAD DATA REPORTING ============================");
        }

        static void GenerateSummaryDb1(String outputFile)
        {
            String logPayload = null;
            String stringFormatNation = "{{\n    \"PrabowoHattaVotes\":\"{0}\",\n    \"PrabowoHattaPercentage\":\"{1:N4}\",\n    \"JokowiKallaVotes\":\"{2}\",\n    \"JokowiKallaPercentage\":\"{3:N4}\",\n    \"Total\":\"{4}\"\n}}";
            String stringFormatProvince = "{{\n    \"Province\":\"{0}\",\n    \"PrabowoHattaVotes\":\"{1}\",\n    \"PrabowoHattaPercentage\":\"{2:N4}\",\n    \"JokowiKallaVotes\":\"{3}\",\n    \"JokowiKallaPercentage\":\"{4:N4}\",\n    \"Total\":\"{5}\"\n}}";
            bool[] firstItems = new bool[1];
            for (int i = 0; i < 1; ++i)
            {
                firstItems[i] = true;
            }

            String totalOutputFile = Path.Combine(Path.GetDirectoryName(outputFile), Path.GetFileNameWithoutExtension(outputFile) + "-total.db1.json");
            String totalProvinceOutputFile = Path.Combine(Path.GetDirectoryName(outputFile), Path.GetFileNameWithoutExtension(outputFile) + "-province.db1.json");
            String totalKabupatenOutputFile = Path.Combine(Path.GetDirectoryName(outputFile), Path.GetFileNameWithoutExtension(outputFile) + "-kabupaten.db1.json");

            using (StreamWriter swCSVTotal = new StreamWriter(Path.Combine(Path.GetDirectoryName(outputFile), Path.GetFileNameWithoutExtension(outputFile) + ".db1.csv")))
            {
                swCSVTotal.WriteLine("#HEADER:ProvinceCode,ProvinceName,KabupatenCode,KabupatenName,PrabowoHattaVotes,JokowiKallaVotes,TotalVotes");

                using (StreamWriter swtotal = new StreamWriter(totalOutputFile))
                {
                    BinaryTally nationTally = new BinaryTally();
                    swtotal.WriteLine("[");

                    using (StreamWriter swProvince = new StreamWriter(totalProvinceOutputFile))
                    {
                        swProvince.WriteLine("[");

                        TwoLevelDictionary.Enumerator provinceEnumeerator = s_tallyTableDb1.VotingTable.GetEnumerator();
                        while (provinceEnumeerator.MoveNext())
                        {
                            String provinceName = "";
                            BinaryTally provinceTally = new BinaryTally();

                            Dictionary<String, BinaryTally>.Enumerator kabupatenEnumerator = provinceEnumeerator.Current.Value.GetEnumerator();
                            while (kabupatenEnumerator.MoveNext())
                            {
                                provinceName = kabupatenEnumerator.Current.Value.ProvinceName;

                                // Dump complete raw data to CSV
                                swCSVTotal.WriteLine("{0},{1},{2},{3},{4},{5},{6}",
                                    kabupatenEnumerator.Current.Value.ProvinceCode,
                                    kabupatenEnumerator.Current.Value.ProvinceName,
                                    kabupatenEnumerator.Current.Value.KabupatenCode,
                                    kabupatenEnumerator.Current.Value.KabupatenName,
                                    kabupatenEnumerator.Current.Value.Counter1,
                                    kabupatenEnumerator.Current.Value.Counter2,
                                    kabupatenEnumerator.Current.Value.Total);

                                provinceTally.Counter1 += kabupatenEnumerator.Current.Value.Counter1;
                                provinceTally.Counter2 += kabupatenEnumerator.Current.Value.Counter2;
                                provinceTally.Total += kabupatenEnumerator.Current.Value.Total;
                            }


                            logPayload = String.Format(stringFormatProvince,
                                                       provinceName,
                                                       provinceTally.Counter1,
                                                       provinceTally.Total == 0 ? 0 : ((float)provinceTally.Counter1 / provinceTally.Total) * 100,
                                                       provinceTally.Counter2,
                                                       provinceTally.Total == 0 ? 0 : ((float)provinceTally.Counter2 / provinceTally.Total) * 100,
                                                       provinceTally.Total);

                            if (firstItems[0] == true)
                            {
                                firstItems[0] = false;
                            }
                            else
                            {
                                Console.WriteLine(",");
                                swProvince.WriteLine(",");
                            }

                            Console.Write(logPayload);
                            swProvince.Write(logPayload);

                            firstItems[0] = false;

                            if (!provinceTally.CheckVoteIntegrity())
                            {
                                // Moves on but log for reporting
                                String log = String.Format("Data for Province {0} is invalid the sum of the vote1 {1} and vote2 {2} does not equate to {3}",
                                        provinceName,
                                        provinceTally.Counter1,
                                        provinceTally.Counter2,
                                        provinceTally.Total);

                                Console.WriteLine(log);
                                s_badDataList.Add(log);
                            }

                            // Sum each province to total
                            nationTally.Counter1 += provinceTally.Counter1;
                            nationTally.Counter2 += provinceTally.Counter2;
                            nationTally.Total += provinceTally.Total;
                        }

                        swProvince.WriteLine("]");
                    }


                    logPayload = String.Format(stringFormatNation,
                            nationTally.Counter1,
                            nationTally.Total == 0 ? 0 : ((float)nationTally.Counter1 / nationTally.Total) * 100,
                            nationTally.Counter2,
                            nationTally.Total == 0 ? 0 : ((float)nationTally.Counter2 / nationTally.Total) * 100,
                            nationTally.Total);

                    if (!nationTally.CheckVoteIntegrity())
                    {
                        // Moves on but log for reporting
                        String log = String.Format("Data for Nation level is invalid the sum of the vote1 {0} and vote2 {1} does not equate to {2}",
                                nationTally.Counter1,
                                nationTally.Counter2,
                                nationTally.Total);

                        Console.WriteLine(log);
                        s_badDataList.Add(log);
                    }

                    Console.WriteLine(logPayload);
                    swtotal.WriteLine(logPayload);
                    swtotal.WriteLine("]");
                }
            }

            // Do bad data reportings
            Console.WriteLine("========================== DB1 BAD DATA REPORTING ============================");
            foreach (String badData in s_badDataList)
            {
                Console.WriteLine("> BAD DATA: {0}", badData);
                s_badDataList.Add(badData);
            }
            Console.WriteLine("======================== END OF BAD DATA REPORTING ============================");
        }

        static void GenerateSummaryDc1(String outputFile)
        {
            String logPayload = null;
            String stringFormatNation = "{{\n    \"PrabowoHattaVotes\":\"{0}\",\n    \"PrabowoHattaPercentage\":\"{1:N4}\",\n    \"JokowiKallaVotes\":\"{2}\",\n    \"JokowiKallaPercentage\":\"{3:N4}\",\n    \"Total\":\"{4}\"\n}}";
            String stringFormatProvince = "{{\n    \"Province\":\"{0}\",\n    \"PrabowoHattaVotes\":\"{1}\",\n    \"PrabowoHattaPercentage\":\"{2:N4}\",\n    \"JokowiKallaVotes\":\"{3}\",\n    \"JokowiKallaPercentage\":\"{4:N4}\",\n    \"Total\":\"{5}\"\n}}";
            bool[] firstItems = new bool[2];
            for (int i = 0; i < 2; ++i)
            {
                firstItems[i] = true;
            }

            String totalOutputFile = Path.Combine(Path.GetDirectoryName(outputFile), Path.GetFileNameWithoutExtension(outputFile) + "-total.dc1.json");
            String totalProvinceOutputFile = Path.Combine(Path.GetDirectoryName(outputFile), Path.GetFileNameWithoutExtension(outputFile) + "-province.dc1.json");

            using (StreamWriter swCSVTotal = new StreamWriter(Path.Combine(Path.GetDirectoryName(outputFile), Path.GetFileNameWithoutExtension(outputFile) + ".dc1.csv")))
            {
                swCSVTotal.WriteLine("#HEADER:ProvinceCode,ProvinceName,PrabowoHattaVotes,JokowiKallaVotes,TotalVotes");

                using (StreamWriter swtotal = new StreamWriter(totalOutputFile))
                {
                    BinaryTally nationTally = new BinaryTally();
                    swtotal.WriteLine("[");

                    using (StreamWriter swProvince = new StreamWriter(totalProvinceOutputFile))
                    {
                        swProvince.WriteLine("[");

                        Dictionary<String, BinaryTally>.Enumerator provinceEnumerator = s_tallyTableDc1.VotingTable.GetEnumerator();
                        while (provinceEnumerator.MoveNext())
                        {
                            BinaryTally provinceTally = new BinaryTally();
                            String provinceName = "";

                            provinceName = provinceEnumerator.Current.Value.ProvinceName;

                            // Dump complete raw data to CSV
                            swCSVTotal.WriteLine("{0},{1},{2},{3},{4}",
                                provinceEnumerator.Current.Value.ProvinceCode,
                                provinceEnumerator.Current.Value.ProvinceName,
                                provinceEnumerator.Current.Value.Counter1,
                                provinceEnumerator.Current.Value.Counter2,
                                provinceEnumerator.Current.Value.Total);

                            logPayload = String.Format(stringFormatProvince,
                                provinceEnumerator.Current.Value.ProvinceName,
                                provinceEnumerator.Current.Value.Counter1,
                                provinceEnumerator.Current.Value.Total == 0 ? 0 : ((float)provinceEnumerator.Current.Value.Counter1 / provinceEnumerator.Current.Value.Total) * 100,
                                provinceEnumerator.Current.Value.Counter2,
                                provinceEnumerator.Current.Value.Total == 0 ? 0 : ((float)provinceEnumerator.Current.Value.Counter2 / provinceEnumerator.Current.Value.Total) * 100,
                                provinceEnumerator.Current.Value.Total);

                            if (firstItems[1] == true)
                            {
                                firstItems[1] = false;
                            }
                            else
                            {
                                Console.WriteLine(",");
                                swProvince.WriteLine(",");
                            }

                            Console.Write(logPayload);
                            swProvince.Write(logPayload);

                            if (!provinceEnumerator.Current.Value.CheckVoteIntegrity())
                            {
                                // Moves on but logs these data for reporting
                                String log = String.Format("Bad data integrity for Province {0}:{1}, vote1 {2} plus vote2 {3} does not equate to total {4}",
                                    provinceEnumerator.Current.Value.ProvinceCode,
                                    provinceEnumerator.Current.Value.ProvinceName,
                                    provinceEnumerator.Current.Value.Counter1,
                                    provinceEnumerator.Current.Value.Counter2,
                                    provinceEnumerator.Current.Value.Total);

                                Console.WriteLine(log);
                                s_badDataList.Add(log);
                            }

                            // Sum each province to total
                            nationTally.Counter1 += provinceEnumerator.Current.Value.Counter1;
                            nationTally.Counter2 += provinceEnumerator.Current.Value.Counter2;
                            nationTally.Total += provinceEnumerator.Current.Value.Total;
                        }

                        swProvince.WriteLine("]");
                    }

                    logPayload = String.Format(stringFormatNation,
                            nationTally.Counter1,
                            nationTally.Total == 0 ? 0 : ((float)nationTally.Counter1 / nationTally.Total) * 100,
                            nationTally.Counter2,
                            nationTally.Total == 0 ? 0 : ((float)nationTally.Counter2 / nationTally.Total) * 100,
                            nationTally.Total);

                    if (!nationTally.CheckVoteIntegrity())
                    {
                        // Moves on but log for reporting
                        String log = String.Format("Data for Nation level is invalid the sum of the vote1 {0} and vote2 {1} does not equate to {2}",
                                nationTally.Counter1,
                                nationTally.Counter2,
                                nationTally.Total);

                        Console.WriteLine(log);
                        s_badDataList.Add(log);
                    }

                    Console.WriteLine(logPayload);
                    swtotal.WriteLine(logPayload);
                    swtotal.WriteLine("]");
                }
            }

            // Do bad data reportings
            Console.WriteLine("========================== DC1 BAD DATA REPORTING ============================");
            foreach (String badData in s_badDataList)
            {
                Console.WriteLine("> BAD DATA: {0}", badData);
                s_badDataList.Add(badData);
            }
            Console.WriteLine("======================== END OF BAD DATA REPORTING ============================");
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

            String outputFile = Path.Combine(outputDir, string.Format("KPU-Feeds-{0:yyyy-MM-dd-hh-tt}.csv", DateTime.Now));
            Console.WriteLine("> Output path: {0}", outputFile);

            Stopwatch sw = new Stopwatch();
            sw.Start();
            CountVotesDc1(tableFile, outputFile, threadCount);
            CountVotesDb1(tableFile, outputFile, threadCount);
            CountVotesDa1(tableFile, outputFile, threadCount);
            sw.Stop();
            Console.WriteLine("> Completed crawling in {0} minutes. Press any key to continue ...", sw.Elapsed.Minutes);
        }
    }
}
