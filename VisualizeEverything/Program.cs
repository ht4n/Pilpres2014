using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.IO;
using System.Globalization;

/*
{
    "name": "Pilpres2014",
    "children": [
    {
        "name": "@prabowo08",
        "children": [
            {
                "name": "ACEH",
                "children": [
                    {"name": "AgglomerativeCluster", "size": 3938},
                    {"name": "CommunityStructure", "size": 3812},
                    {"name": "HierarchicalCluster", "size": 6714},
                    {"name": "MergeEdge", "size": 743}
                ]
            }
        ]
    },
    {
        "name": "@jokowi_do2",
        "children": [
            {
                "name": "ACEH",
                "children": [
                    {"name": "AgglomerativeCluster", "size": 3938},
                    {"name": "CommunityStructure", "size": 3812},
                    {"name": "HierarchicalCluster", "size": 6714},
                    {"name": "MergeEdge", "size": 743}
                ]
            }
        ]
    }]
}

 */
namespace VisualizeEverything
{
    /* 
     #HEADER:ProvinceCode,ProvinceName,KabupatenCode,KabupatenName,KecamatanCode,KecamatanName,PrabowoHattaVotes,JokowiKallaVotes,TotalVotes
     1,ACEH,2,ACEH SELATAN,236,LABUHAN HAJI TIMUR,0,0,0
    */
    class DescendedDateComparer : IComparer<DateTime>
    {
        public int Compare(DateTime x, DateTime y)
        {
            // use the default comparer to do the original comparison for datetimes
            int ascendingResult = Comparer<DateTime>.Default.Compare(x, y);

            // turn the result around
            return 0 - ascendingResult;
        }
    }

    class Program
    {
        static String lastKabupaten = "";
        static String lastProvince = "";
        static String maxDepth = "Province";

        static void PrintTab(StreamWriter sw, int level)
        {
            for (int i = 0; i < level * 4; ++i)
            {
                sw.Write(" ");
            }
        }

        static String CountKecamatans(StreamReader sr, String line, int columnId, out UInt32 sum)
        {
            String[] tokens = line.Split(',');
            sum = 0;
            
            while (!sr.EndOfStream && !String.IsNullOrEmpty(line))
            {
                sum += UInt32.Parse(tokens[columnId]);
                
                line = sr.ReadLine();
                tokens = line.Split(',');

                if (lastKabupaten != tokens[3])
                {
                    return line;
                }
            }

            return line;
        }

        static String WriteKecamatans(StreamWriter sw, StreamReader sr, String line, int columnId, int level)
        {
            String[] tokens = line.Split(',');                
            int offset = 0;
            while (!sr.EndOfStream && !String.IsNullOrEmpty(line))
            {
                if (offset++ != 0)
                {
                    sw.WriteLine(",");
                }

                PrintTab(sw, level + 1);
                sw.Write("{{ \"name\":\"{0}\", \"size\":\"{1}\" }}", tokens[5], tokens[columnId]);
                                
                line = sr.ReadLine();
                tokens = line.Split(',');
                                
                if (lastKabupaten != tokens[3])
                {
                    return line;
                }
            }

            return line;
        }

        static String CountKabupatens(StreamReader sr, String line, int columnId, out UInt32 sum)
        {
            String[] tokens = line.Split(',');
            sum = 0;

            while (!sr.EndOfStream && !String.IsNullOrEmpty(line))
            {
                sum += UInt32.Parse(tokens[columnId]);

                line = sr.ReadLine();
                tokens = line.Split(',');

                if (lastProvince != tokens[1])
                {
                    return line;
                }
            }

            return line;
        }

        static String WriteKabupatens(StreamWriter sw, StreamReader sr, String line, int columnId, int level)
        {
            String[] tokens = line.Split(',');
            int offset = 0;
            while (!sr.EndOfStream && !String.IsNullOrEmpty(line))
            {
                if (offset++ != 0)
                {
                    sw.WriteLine(",");
                }
                
                String kabupatenName = tokens[3];

                // Remember current kabupaten
                lastKabupaten = tokens[3];

                if (maxDepth.Equals("Kabupaten", StringComparison.InvariantCultureIgnoreCase))
                {
                    UInt32 curSum = 0;
                    line = CountKecamatans(sr, line, columnId, out curSum);
        
                    PrintTab(sw, level + 1);
                    sw.Write("{{ \"name\":\"{0}\", \"size\":\"{1}\" }}", kabupatenName, curSum);
                }
                else
                {
                    PrintTab(sw, level + 1);
                    sw.WriteLine("{");
                    PrintTab(sw, level + 2);
                    sw.WriteLine("\"name\":\"{0}\",", kabupatenName);
                    PrintTab(sw, level + 2);
                    sw.WriteLine("\"children\": [");

                    line = WriteKecamatans(sw, sr, line, columnId, level + 2);

                    sw.WriteLine("");
                    PrintTab(sw, level + 2);
                    sw.WriteLine("]");
                    PrintTab(sw, level + 1);
                    sw.Write("}");
                }

                tokens = line.Split(',');
                if (lastProvince != tokens[1])
                {
                    return line;
                } 
            }

            return line;
        }

        static String WriteProvinces(StreamWriter sw, StreamReader sr, String line, int columnId, int level)
        {
            String[] tokens = line.Split(',');
            int offset = 0;
            while (!sr.EndOfStream && !String.IsNullOrEmpty(line))
            {
                if (offset++ != 0)
                {
                    sw.WriteLine(",");
                }

                // Remember current province
                lastProvince = tokens[1];

                if (maxDepth.Equals("Province", StringComparison.InvariantCultureIgnoreCase))
                {
                    UInt32 sum = 0;
                    line = CountKabupatens(sr, line, columnId, out sum);
                    PrintTab(sw, level + 1);
                    sw.Write("{{ \"name\":\"{0}\", \"size\":\"{1}\" }}", tokens[1], sum);
                }
                else
                {
                    PrintTab(sw, level + 1);
                    sw.WriteLine("{");
                    PrintTab(sw, level + 2);
                    sw.WriteLine("\"name\":\"{0}\",", tokens[1]);
                    PrintTab(sw, level + 2);
                    sw.WriteLine("\"children\": [");
                
                    line = WriteKabupatens(sw, sr, line, columnId, level + 2);

                    sw.WriteLine("");
                    PrintTab(sw, level + 2);
                    sw.WriteLine("]");
                    PrintTab(sw, level + 1);
                    sw.Write("}");
                }

                tokens = line.Split(',');                
            }

            return line;
        }

        static void WriteDistricts(StreamWriter sw, String inputCsv, int columnId)
        {            
            using (StreamReader sr = new StreamReader(inputCsv))
            {
                String line = sr.ReadLine();
                while (!sr.EndOfStream && !String.IsNullOrEmpty(line))
                {
                    if (line.StartsWith("#"))
                    {
                        line = sr.ReadLine();
                        continue;
                    }

                    line = WriteProvinces(sw, sr, line, columnId, 3);
                }
            }            
        }

        static String GetLatestTimestampFile(String rootDir)
        {
            IEnumerable<String> files = Directory.EnumerateFiles(rootDir, "*.csv");
            SortedSet<DateTime> dateSorter = new SortedSet<DateTime>(new DescendedDateComparer());
            CultureInfo provider = CultureInfo.InvariantCulture;
            foreach (String file in files)
            {
                Regex rgx = new Regex("KPU-Feeds-(?<DATETIME>\\d{4}-\\d{2}-\\d{2}-\\d{2}-\\w{2}).csv");
                Match dtmatch = rgx.Match(file);
                if (!dtmatch.Success)
                {
                    continue;
                }

                DateTime dt = DateTime.ParseExact(dtmatch.Groups["DATETIME"].Value, "yyyy-MM-dd-hh-tt", provider);
                dateSorter.Add(dt);
            }

            foreach (DateTime dt in dateSorter)
            {
                return String.Format("KPU-Feeds-{0}.csv", dt.ToString("yyyy-MM-dd-hh-tt"));
            }

            throw new InvalidDataException("Cannot found any valid file to process");
        }

        static void Main(string[] args)
        {
            if (args.Length != 3)
            {
                Console.WriteLine("Syntax: VisualizeEverything.exe <root> <output> <filter:Province|Kabupaten|Kecamatan>");
                return;
            }

            String root = args[0];
            String inputCsv = Path.Combine(root, GetLatestTimestampFile(root));
            String output = args[1];
            maxDepth = args[2];
            using (StreamWriter sw = new StreamWriter(output))
            {
                sw.WriteLine("{");
                sw.WriteLine("    \"name\":\"Pilpres2014\",");
                sw.WriteLine("    \"children\": [");
                sw.WriteLine("        {");
                sw.WriteLine("            \"name\":\"@prabowo08\",");
                sw.WriteLine("            \"children\": [");
                WriteDistricts(sw, inputCsv, 6);
                sw.WriteLine("");
                sw.WriteLine("            ]");
                sw.WriteLine("        },");
                sw.WriteLine("        {");
                sw.WriteLine("            \"name\":\"@jokowi_do2\",");
                sw.WriteLine("            \"children\": [");
                WriteDistricts(sw, inputCsv, 7);
                sw.WriteLine("");
                sw.WriteLine("            ]");
                sw.WriteLine("        }");
                sw.WriteLine("    ]");
                sw.WriteLine("}");
            }
        }
    }
}
