using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.IO;

namespace FeedsMetadataGenerator
{
    // Program to scan the crawl output files and sort them and produce metadata file
    // [
    //     { "datetime": "2014-07-19-04-PM" },
    //     { "datetime": "2014-07-19-02-PM" },
    //     { "datetime": "2014-07-19-12-PM" },
    //     { "datetime": "2014-07-19-10-AM" },
    // ]

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
        static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine("Syntax: FeedsMetadataGenerator.exe <root-dir> <metadata-output>");
                return;
            }

            String rootDir = args[0];
            String metadataFile = args[1];
            using (StreamWriter sw = new StreamWriter(metadataFile))
            {
                IEnumerable<String> files = Directory.EnumerateFiles(rootDir, "*-total.json");
                SortedSet<DateTime> dateSorter = new SortedSet<DateTime>(new DescendedDateComparer());

                foreach (String file in files)
                {
                    Regex rgx = new Regex("KPU-Feeds-(?<YEAR>\\d+)-(?<MONTH>\\d+)-(?<DAY>\\d+)-(?<HOUR>\\d+)-(?<AMPM>\\w+)-total.json");
                    Match dtmatch = rgx.Match(file);
                    if (!dtmatch.Success)
                    {
                        throw new InvalidDataException("Invalid filename. Expecting 'KPU-Feeds-YYYY-MM-DD-HH-AMPM-total.json");
                    }

                    int hour = dtmatch.Groups["AMPM"].Value == "AM" ? int.Parse(dtmatch.Groups["HOUR"].Value) : ((int.Parse(dtmatch.Groups["HOUR"].Value) + 12) % 24);
                    
                    DateTime dt = new DateTime(
                        int.Parse(dtmatch.Groups["YEAR"].Value),
                        int.Parse(dtmatch.Groups["MONTH"].Value),
                        int.Parse(dtmatch.Groups["DAY"].Value),
                        hour,
                        0,
                        0);

                    dateSorter.Add(dt);
                }

                sw.WriteLine("[");
                Console.WriteLine("[");
                int counter = 0;
                foreach (DateTime dt in dateSorter)
                {
                    if (counter++ == 0)
                    {
                        Console.WriteLine("");
                        sw.WriteLine("");
                    }
                    else
                    {
                        Console.WriteLine(",");
                        sw.WriteLine(",");
                    }

                    String payload = String.Format("    {{ \"datetime\":\"{0}\" }}", dt.ToString("yyyy-MM-dd-hh-tt"));
                    Console.Write(payload);
                    sw.Write(payload);
                }
                Console.WriteLine("\n]");
                sw.WriteLine("\n]");
            }
        }
    }
}
