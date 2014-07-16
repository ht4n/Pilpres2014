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

*/
namespace Pilpres2014
{
    using System;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using System.Web;
    using System.Net;
    using System.Threading;
    using System.IO;

    /// <summary>
    /// A code to get the area code and name from KPU site by automatic crawling KPU site
    /// 
    /// KPU.go.id provides web service in http://pilpres2014.kpu.go.id/da1.php
    /// The crawler will look and parse the area code/name from the HTML tag <select name="wilayah_id"><option value="AREA-CODE">AREA-NAME</option>...</select>.
    /// It will recursively download the area code/name at Province (Provinsi), City (Kabupaten/Kota), County (Kecamatan) and dump it into a table.
    /// 
    /// Once you have this table it is very easy to get data from KPU.go.id.
    /// 
    /// Here is their API:
    /// 
    /// Province level:
    /// http://pilpres2014.kpu.go.id/da1.php?cmd=select&grandparent=0&parent=<Provinsi-Code>
    /// 
    /// /// Example 1: ACEH(1)
    /// http://pilpres2014.kpu.go.id/da1.php?cmd=select&grandparent=0&parent=1
    /// 
    /// Kabupatent level:
    /// grandparent=<Provinsi-Code>
    /// parent=<Kabupatent-Code>
    /// http://pilpres2014.kpu.go.id/da1.php?cmd=select&grandparent=<Provinsi-Code>&parent=<Kabupaten-Code>
    /// 
    /// Example 1: BALI(53241)|TABANAN(53299)
    /// http://pilpres2014.kpu.go.id/da1.php?cmd=select&grandparent=53241&parent=53299
    /// 
    /// 
    /// Kecamatan level:
    /// grandparent=<Kabupaten-Code>
    /// parent=<Kecamatan-Code>
    /// http://pilpres2014.kpu.go.id/da1.php?cmd=select&grandparent=<Kabupaten-Code>&parent=<Kecamatan-Code>
    /// 
    /// Example1: ACEH(1)|ACEH-SELATAN(2)|TRUMON(148)
    /// http://pilpres2014.kpu.go.id/da1.php?cmd=select&grandparent=2&parent=148
    /// 
    /// Example2: BALI(53241)|JEMBRANA(53242)|MENDOYO(53256)
    /// http://pilpres2014.kpu.go.id/da1.php?cmd=select&grandparent=53242&parent=53256
    /// 
    /// </summary>
    class KPUAreaCodeCrawler
    {
        static readonly String baseUrl = "http://pilpres2014.kpu.go.id/da1.php";
        static readonly String selectUrl = "http://pilpres2014.kpu.go.id/da1.php?cmd=select&grandparent=0&parent={0}";
        static readonly String outputFile = "AreaCodeTable.csv";
        static readonly int sleepInMs = 1000;

        String GetLevelName(int level)
        {
            switch(level)
            {
                case 0:
                    return "PROVINSI";
                case 1:
                    return "KABUPATEN/KOTA";
                case 2:
                    return "KECAMATAN";
                default:
                    return "Level" + level;
            }
        }

        void Download(String url, StreamWriter sw)
        {
            Download(url, 0, sw);
        }

        void Download(String url, int level, StreamWriter sw)
        {
            Regex rgx = new Regex("<select.*name=\"wilayah_id\".*(<option\\s+value=\".*\">.*</option>)+", RegexOptions.Multiline);
            StringBuilder sb = new StringBuilder();

            WebClient client = new WebClient();
            sb.Append(client.DownloadString(url));

            Match match = rgx.Match(sb.ToString());
            Regex subRgx = new Regex("<option\\s+value=\"(?<Code>\\d+)\"\\s*>(?<AreaName>(\\w|\\s)+)</option>");
            MatchCollection matches = subRgx.Matches(match.Value);
            foreach (Match m in matches)
            {
                String areaCode = m.Groups["Code"].Value;
                string areaName = m.Groups["AreaName"].Value;

                Console.WriteLine("{0},{1},{2},{3}", 
                    level, 
                    GetLevelName(level), 
                    m.Groups["Code"].Value, 
                    m.Groups["AreaName"].Value);

                sw.WriteLine("{0},{1},{2},{3}", level, GetLevelName(level), areaCode, areaName);
                sw.Flush();

                String subUrl = String.Format(selectUrl, m.Groups["Code"].Value);

                if (level < 2)
                {
                    // Sleep in between downloads
                    Thread.Sleep(sleepInMs);

                    Download(subUrl, level + 1, sw);
                }
            }
        }

        static void Main(string[] args)
        {
            KPUAreaCodeCrawler crawler = new KPUAreaCodeCrawler();

            using (StreamWriter sw = new StreamWriter(outputFile))
            {
                sw.WriteLine("#HEADER:Level,LevelCategory,AreaCode,AreaName");                
                crawler.Download(baseUrl, sw);
            }
        }
    }
}
