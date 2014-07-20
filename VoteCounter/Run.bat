touch logging.txt
move logging.txt logging.previous.txt
VoteCounter.exe areacodetable.csv C:\GitHub\Pilpres2014Portal 16 > logging.txt

TotalTrendAggregator.exe c:\GitHub\Pilpres2014Portal C:\GitHub\Pilpres2014Portal\totaltrend.tsv

pushd C:\GitHub\Pilpres2014Portal
git add KPU-Feeds*
git add totaltrend.tsv
git commit -m "new data"

FeedsMetadataGenerator.exe C:\GitHub\Pilpres2014Portal C:\GitHub\Pilpres2014Portal\feedsources.json
git add feedsources.json

git push
popd