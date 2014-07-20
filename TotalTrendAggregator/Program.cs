using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.IO;
using System.Globalization;

namespace TotalTrendAggregator
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine("Syntax: TotalTrendAggregator.exe <root> <output>");
                return;
            }
            String root = args[0];
            String output = args[1];
            CultureInfo provider = CultureInfo.InvariantCulture;

            using (StreamWriter sw = new StreamWriter(output))
            {
                sw.WriteLine("date\t@prabowo08\t@jokowi_do2");
                SortedSet<DateTime> dateSorter = new SortedSet<DateTime>();
                IEnumerable<String> files = Directory.EnumerateFiles(root, "*-total.json");
                foreach (String file in files)
                {
                    Regex rgx = new Regex("KPU-Feeds-(?<DATETIME>\\d{4}-\\d{2}-\\d{2}-\\d{2}-\\w{2})-total.json");
                    Match dtmatch = rgx.Match(file);
                    if (!dtmatch.Success)
                    {
                        throw new InvalidDataException("Invalid filename. Expecting 'KPU-Feeds-YYYY-MM-DD-HH-AMPM-total.json");
                    }

                    DateTime datetime = DateTime.ParseExact(dtmatch.Groups["DATETIME"].Value, "yyyy-MM-dd-hh-tt", provider);
                    dateSorter.Add(datetime);
                }

                foreach (DateTime dt in dateSorter)
                {
                    String filename = Path.Combine(root, String.Format("KPU-Feeds-{0}-total.json", dt.ToString("yyyy-MM-dd-hh-tt")));
                    using (StreamReader sr = new StreamReader(filename))
                    {
                        String match1Value = "";
                        String match2Value = "";
                        String line = sr.ReadLine();
                        while (!sr.EndOfStream && !String.IsNullOrEmpty(line))
                        {
                            try
                            {
                                if (line.StartsWith("[") || line.StartsWith("]") || line.StartsWith("{") || line.StartsWith("}"))
                                {
                                    continue;
                                }

                                Regex rgx = new Regex("\"PrabowoHattaPercentage\":\"(?<VALUE1PERCENTAGE>.+)\"");
                                Match m = rgx.Match(line);
                                if (m.Success)
                                {
                                    match1Value = m.Groups["VALUE1PERCENTAGE"].Value;
                                }

                                rgx = new Regex("\"JokowiKallaPercentage\":\"(?<VALUE2PERCENTAGE>.+)\"");
                                m = rgx.Match(line);
                                if (m.Success)
                                {
                                    match2Value = m.Groups["VALUE2PERCENTAGE"].Value;
                                }

                                if (!String.IsNullOrEmpty(match1Value) && !String.IsNullOrEmpty(match2Value))
                                {
                                    String msgPayload = String.Format("{0}\t{1}\t{2}", dt.ToString("yyyyMMddHH"), match1Value, match2Value);
                                    Console.WriteLine(msgPayload);
                                    sw.WriteLine(msgPayload);
                                    sw.Flush();

                                    match1Value = null;
                                    match2Value = null;
                                }
                            }
                            finally
                            {
                                line = sr.ReadLine();
                            }
                        }
                    }
                }
            }
        }
    }
}
