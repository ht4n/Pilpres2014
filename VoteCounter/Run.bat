REM %1 should be the deployment repo setup for deployment to Azure

@echo off
REM initiate crawling of KPU.go.id
move logging.txt logging.previous.txt
VoteCounter.exe areacodetable.csv %1 16 > logging.txt

REM Generate totaltrend.tsv for difference chart
TotalTrendAggregator.exe %1 %1\totaltrend.tsv

REM GEnerate feedsources.json to automatically update the Vote ticker with the latest timestamp
FeedsMetadataGenerator.exe %1 %1\feedsources.json

REM Generate 3 kinds of data/table aggregation at Province, Kabupaten, Kecamatan level
VisualizeEverything.exe %1 %1\pilpres2014.json Province
VisualizeEverything.exe %1 %1\midpilpres2014.json Kabupaten
VisualizeEverything.exe %1 %1\fullpilpres2014.json Kecamatan

REM Commit new changes to GitHub
pushd %1
git add KPU-Feeds* >> logging.txt
git add totaltrend.tsv >> logging.txt
git add pilpres2014.json >> logging.txt
git add midpilpres2014.json >> logging.txt
git add fullpilpres2014.json >> logging.txt
git add feedsources.json  >> logging.txt
git commit -m "new data"  >> logging.txt
REM git push  >> logging.txt
popd