using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.IO;

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

            using (StreamWriter sw = new StreamWriter(output))
            {
                sw.WriteLine("date\t@prabowo08\t@jokowi_do2");

                IEnumerable<String> files = Directory.EnumerateFiles(root, "*-total.json");
                foreach (String file in files)
                {
                    Regex rgx = new Regex("KPU-Feeds-(?<YEAR>\\d+)-(?<MONTH>\\d+)-(?<DAY>\\d+)-(?<HOUR>\\d+)-(?<AMPM>\\w+)-total.json");
                    Match dtmatch = rgx.Match(file);
                    if (!dtmatch.Success)
                    {
                        throw new InvalidDataException("Invalid filename. Expecting 'KPU-Feeds-YYYY-MM-DD-HH-AMPM-total.json");
                    }

                    int hour = dtmatch.Groups["AMPM"].Value == "AM" ? int.Parse(dtmatch.Groups["HOUR"].Value) : (int.Parse(dtmatch.Groups["HOUR"].Value) + 12);
                    String datetime = String.Format(
                        "{0}{1}{2}{3}",
                        dtmatch.Groups["YEAR"].Value,
                        dtmatch.Groups["MONTH"].Value,
                        dtmatch.Groups["DAY"].Value,
                        hour);

                    using (StreamReader sr = new StreamReader(file))
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

                                rgx = new Regex("\"PrabowoHattaPercentage\":\"(?<VALUE1PERCENTAGE>.+)\"");
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
                                    String msgPayload = String.Format("{0}\t{1}\t{2}", datetime, match1Value, match2Value);
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
