touch logging.txt
move logging.txt logging.previous.txt
VoteCounter.exe areacodetable.csv C:\GitHub\Pilpres2014Portal 16 > logging.txt
TotalTrendAggregator.exe c:\GitHub\Pilpres2014Portal C:\GitHub\Pilpres2014Portal\totaltrend.tsv
FeedsMetadataGenerator.exe C:\GitHub\Pilpres2014Portal C:\GitHub\Pilpres2014Portal\feedsources.json

pushd C:\GitHub\Pilpres2014Portal
git add KPU-Feeds*
git add totaltrend.tsv
git add feedsources.json
git commit -m "new data"
git push
popd