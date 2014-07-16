Pilpres2014
===========

Code to help clean and honest democracy process in Indonesia 2014

A code to get the area code and name from KPU site by automatic crawling KPU site.

WARNING: do not spam KPU.go.id website by running this code. You really need to get the table that I have published (unless there is some changes) in https://github.com/ht4n/Pilpres2014/blob/master/AreaCodeTable.csv.
     
KPU.go.id provides web service in http://pilpres2014.kpu.go.id/da1.php
The crawler will look and parse the area code/name from the HTML tag <select name="wilayah_id"><option value="AREA-CODE">AREA-NAME</option>...</select>.
It will recursively download the area code/name at Province (Provinsi), City (Kabupaten/Kota), County (Kecamatan) and dump it into a table.
     
Once you have this table it is very easy to get data from KPU.go.id.
     
KPU.go.id API:
     
     Province level:
     http://pilpres2014.kpu.go.id/da1.php?cmd=select&grandparent=0&parent=<Provinsi-Code>
     
     Example 1: ACEH(1)
     http://pilpres2014.kpu.go.id/da1.php?cmd=select&grandparent=0&parent=1
     
     Kabupatent level:
     grandparent=<Provinsi-Code>
     parent=<Kabupatent-Code>
     http://pilpres2014.kpu.go.id/da1.php?cmd=select&grandparent=<Provinsi-Code>&parent=<Kabupaten-Code>
     
     Example 1: BALI(53241)|TABANAN(53299)
     http://pilpres2014.kpu.go.id/da1.php?cmd=select&grandparent=53241&parent=53299
     
     
     Kecamatan level:
     grandparent=<Kabupaten-Code>
     parent=<Kecamatan-Code>
     http://pilpres2014.kpu.go.id/da1.php?cmd=select&grandparent=<Kabupaten-Code>&parent=<Kecamatan-Code>
     
     Example1: ACEH(1)|ACEH-SELATAN(2)|TRUMON(148)
     http://pilpres2014.kpu.go.id/da1.php?cmd=select&grandparent=2&parent=148
     
     Example2: BALI(53241)|JEMBRANA(53242)|MENDOYO(53256)
     http://pilpres2014.kpu.go.id/da1.php?cmd=select&grandparent=53242&parent=53256
     
     </summary>
