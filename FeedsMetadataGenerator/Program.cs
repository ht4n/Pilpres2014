using System;
using System.Collections.Generic;
using System.Globalization;
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
                CultureInfo provider = CultureInfo.InvariantCulture;
                foreach (String file in files)
                {
                    Regex rgx = new Regex("KPU-Feeds-(?<DATETIME>\\d{4}-\\d{2}-\\d{2}-\\d{2}-\\w{2})-total.json");
                    Match dtmatch = rgx.Match(file);
                    if (!dtmatch.Success)
                    {
                        throw new InvalidDataException("Invalid filename. Expecting 'KPU-Feeds-YYYY-MM-DD-HH-AMPM-total.json");
                    }

                    DateTime dt = DateTime.ParseExact(dtmatch.Groups["DATETIME"].Value, "yyyy-MM-dd-hh-tt", provider);                    
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
