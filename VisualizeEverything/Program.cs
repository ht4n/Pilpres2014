using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

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

    class Program
    {
        static String lastKabupaten = "";
        static String lastProvince = "";
        
        static void PrintTab(StreamWriter sw, int level)
        {
            for (int i = 0; i < level * 4; ++i)
            {
                sw.Write(" ");
            }
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

                PrintTab(sw, level + 1);
                sw.WriteLine("{");
                PrintTab(sw, level + 2);
                sw.WriteLine("\"name\":\"{0}\",", tokens[3]);
                PrintTab(sw, level + 2);
                sw.WriteLine("\"children\": [");

                // Remember current kabupaten
                lastKabupaten = tokens[3];

                line = WriteKecamatans(sw, sr, line, columnId, level + 2);

                sw.WriteLine("");
                PrintTab(sw, level + 2);
                sw.WriteLine("]");
                PrintTab(sw, level + 1);
                sw.Write("}");

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

                PrintTab(sw, level + 1);
                sw.WriteLine("{");
                PrintTab(sw, level + 2);
                sw.WriteLine("\"name\":\"{0}\",", tokens[1]);
                PrintTab(sw, level + 2);
                sw.WriteLine("\"children\": [");

                // Remember current province
                lastProvince = tokens[1];
                
                line = WriteKabupatens(sw, sr, line, columnId, level + 2);
                tokens = line.Split(',');

                sw.WriteLine("");
                PrintTab(sw, level + 2);
                sw.WriteLine("]");
                PrintTab(sw, level + 1);
                sw.Write("}");
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

        static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine("Syntax: VisualizeEverything.exe <input> <output>");
                return;
            }

            String inputCsv = args[0];
            String output = args[1];
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
