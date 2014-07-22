///<reference path="Scripts/typings/jquery/jquery.d.ts"/>
///<reference path="Scripts/typings/knockout/knockout.d.ts"/>
var VoteEntry = (function () {
    function VoteEntry() {
        this.totalVotes1Raw = ko.observable(0);
        this.totalVotes2Raw = ko.observable(0);
        this.totalVotesRaw = ko.observable(0);
        this.totalVotes1 = ko.observable("");
        this.percentageVotes1 = ko.observable("");
        this.totalVotes2 = ko.observable("");
        this.percentageVotes2 = ko.observable("");
        this.total = ko.observable("");
        this.label = ko.observable("");
        this.status1 = ko.observable("");
        this.status2 = ko.observable("");
    }
    return VoteEntry;
})();

var Pilpres2014 = (function () {
    function Pilpres2014() {
        var _this = this;
        this.suffix = "-total.json";
        this.provinceSuffix = "-province.json";
        this.selectedRekapLevel = ko.observable("DA1");

        this.showProvinceDetails = ko.observable(false);
        this.showHistoricalData = ko.observable(false);
        this.showTotalVoteEntries = ko.observable(false);

        var self = this;
        this.url = ko.observable("https://github.com/ht4n/Pilpres2014");
        this.provinces = ko.observableArray([]);
        this.totalVotes1 = ko.observable("");
        this.totalVotes2 = ko.observable("");
        this.percentageVotes1 = ko.observable("");
        this.percentageVotes2 = ko.observable("");
        this.status1 = ko.observable("");
        this.status2 = ko.observable("");
        this.totalVotes = ko.observable("");
        this.voteEntries = ko.observableArray([]);
        this.provinceVoteEntries = ko.observableArray([]);
        this.totalVoteEntries = ko.observableArray([]);
        this.baseFeedUrl = "https://github.com/ht4n/Pilpres2014Portal/blob/master/KPU-Feeds-";
        this.historicalFeeds = ko.observableArray([]);
        this.selectedDataFeed = ko.observable(null);
        this.lastUpdatedTime = ko.observable("");

        this.query("feedsources.json", null, function (data, status) {
            console.log("response:" + status);
            if (status !== "success") {
                return;
            }

            var dataJson = JSON.parse(data);
            dataJson.forEach(function (entry) {
                self.historicalFeeds.push(entry);
            });

            // Sets the current feed as the first one
            var historicalFeedsLength = _this.historicalFeeds().length;
            var currentFeedItem = _this.historicalFeeds()[0];
            _this.selectedDataFeed(currentFeedItem);
            _this.lastUpdatedTime(_this.selectedDataFeed().datetime);

            _this.refreshMainTicker(_this.selectedDataFeed().datetime);
        });

        this.toggleHistoricalText = ko.observable("Expand");
        this.toggleProvinceText = ko.observable("Expand");
    }
    Pilpres2014.prototype.updateVoteByDate = function (data, event) {
        var vm = ko.contextFor(event.currentTarget);
        vm.$root.refreshMainTicker(data.datetime);
    };

    Pilpres2014.prototype.selectRecap = function (value) {
        if (value === "DA1") {
            this.selectedRekapLevel("DA1");
            this.suffix = "-total.json";
            this.provinceSuffix = "-province.json";
        } else if (value === "DB1") {
            this.selectedRekapLevel("DB1");
            this.suffix = "-total.db1.json";
            this.provinceSuffix = "-province.db1.json";
        } else if (value == "DC1") {
            this.selectedRekapLevel("DC1");
            this.suffix = "-total.dc1.json";
            this.provinceSuffix = "-province.dc1.json";
        } else {
            console.error("Invalid rekap level value " + value);
            return;
        }

        this.refreshMainTicker(this.lastUpdatedTime());
    };

    Pilpres2014.prototype.toggleHistoricalData = function () {
        if (this.showHistoricalData()) {
            this.showHistoricalData(false);
            this.toggleHistoricalText("Expand");
        } else {
            this.showHistoricalData(true);
            this.toggleHistoricalText("Collapse");
            var self = this;
            var voteEntries = [];
            var dataCount = 0;

            // Use this workaround until DB1/DC1 catching up to 36 entries
            var maxHistoricalEntries = Math.min((this.selectedRekapLevel() === "DA1" ? 36 : 1), this.historicalFeeds().length);

            var historicalDataCallback = function (data, status) {
                console.log("response:" + status);
                if (status !== "success") {
                    return;
                }

                var dataJson = JSON.parse(data);

                for (var i = 0; i < dataJson.length; ++i) {
                    var entry = dataJson[i];

                    var context = this;
                    var voteEntry = new VoteEntry();
                    voteEntry.totalVotes1(parseInt(entry.PrabowoHattaVotes).toLocaleString());
                    voteEntry.status1(parseFloat(entry.PrabowoHattaPercentage) > 50.0 ? "win" : "");
                    voteEntry.percentageVotes1(parseFloat(entry.PrabowoHattaPercentage).toFixed(2) + "%");

                    voteEntry.totalVotes2(parseInt(entry.JokowiKallaVotes).toLocaleString());
                    voteEntry.status2(parseFloat(entry.JokowiKallaPercentage) > 50.0 ? "win" : "");
                    voteEntry.percentageVotes2(parseFloat(entry.JokowiKallaPercentage).toFixed(2) + "%");

                    voteEntry.total(entry.Total);
                    voteEntry.label(context.datetime);

                    voteEntries[context.id] = voteEntry;
                }
                ;

                ++dataCount;
                if (dataCount == maxHistoricalEntries) {
                    self.voteEntries(voteEntries);
                }
            };

            for (var i = 0; i < maxHistoricalEntries; ++i) {
                var value = this.historicalFeeds()[i];
                this.query("KPU-Feeds-" + value.datetime + this.suffix, { "datetime": value.datetime, "id": i }, historicalDataCallback);
            }
        }
    };

    Pilpres2014.prototype.toggleProvinceDetails = function () {
        if (this.showProvinceDetails()) {
            this.showProvinceDetails(false);
            this.toggleProvinceText("Expand");
        } else {
            this.showProvinceDetails(true);
            this.toggleProvinceText("Collapse");
        }

        if (this.showProvinceDetails()) {
            this.refreshProvinceDetails();
        }
    };

    Pilpres2014.prototype.refreshProvinceDetails = function () {
        var self = this;

        var provinceCallback = function (data, status) {
            console.log("response:" + status);
            if (status !== "success") {
                return;
            }

            var dataJson = JSON.parse(data);
            self.provinceVoteEntries.removeAll();
            dataJson.forEach(function (entry) {
                var voteEntry = new VoteEntry();
                voteEntry.totalVotes1Raw(parseInt(entry.PrabowoHattaVotes));
                voteEntry.totalVotes2Raw(parseInt(entry.JokowiKallaVotes));
                voteEntry.totalVotesRaw(parseInt(entry.Total));

                voteEntry.totalVotes1(parseInt(entry.PrabowoHattaVotes).toLocaleString());
                voteEntry.percentageVotes1(parseFloat(entry.PrabowoHattaPercentage).toFixed(2));
                voteEntry.totalVotes2(parseInt(entry.JokowiKallaVotes).toLocaleString());
                voteEntry.percentageVotes2(parseFloat(entry.JokowiKallaPercentage).toFixed(2));
                voteEntry.total(parseInt(entry.Total).toLocaleString());
                voteEntry.label(entry.Province);

                self.provinceVoteEntries.push(voteEntry);
            });
        };

        this.query("KPU-Feeds-" + this.lastUpdatedTime() + this.provinceSuffix, null, provinceCallback);
    };

    Pilpres2014.prototype.sortProvinceData = function (field) {
        var self = this;
        switch (field) {
            case 1:
                self.provinceVoteEntries.sort(function (a, b) {
                    return parseFloat(b.percentageVotes1()) - parseFloat(a.percentageVotes1());
                });
                break;
            case 2:
                self.provinceVoteEntries.sort(function (a, b) {
                    return parseFloat(b.percentageVotes2()) - parseFloat(a.percentageVotes2());
                });
                break;
            case 3:
                self.provinceVoteEntries.sort(function (a, b) {
                    return b.totalVotes1Raw() - a.totalVotes1Raw();
                });
                break;
            case 4:
                self.provinceVoteEntries.sort(function (a, b) {
                    return b.totalVotes2Raw() - a.totalVotes2Raw();
                });
                break;
            case 5:
                self.provinceVoteEntries.sort(function (a, b) {
                    return b.totalVotesRaw() - a.totalVotesRaw();
                });
                break;
        }
    };

    Pilpres2014.prototype.toggleShowTotalVoteEntries = function () {
        var self = this;

        // if all three entries are not null, show the total vote entries
        var showTotal = true;
        var i = 0;
        while (showTotal && i < 3) {
            showTotal = self.totalVoteEntries()[i] != null;
            i++;
        }
        if (showTotal) {
            self.showTotalVoteEntries(true);
        }
    };

    Pilpres2014.prototype.refreshMainTicker = function (datetime) {
        var self = this;
        self.voteEntries.removeAll();
        self.totalVoteEntries.removeAll();
        self.showTotalVoteEntries(false);

        self.totalVoteEntries([null, null, null]);
        var da1Callback = function (data, status) {
            totalCallback(data, status, 0);
            self.toggleShowTotalVoteEntries();
        };
        var db1Callback = function (data, status) {
            totalCallback(data, status, 1);
        };
        var dc1Callback = function (data, status) {
            totalCallback(data, status, 2);
        };

        var totalCallback = function (data, status, idx) {
            console.log("response:" + status);
            if (status !== "success") {
                self.totalVoteEntries()[idx] = new VoteEntry();
                return;
            }

            var dataJson = JSON.parse(data);

            for (var i = 0; i < dataJson.length; ++i) {
                var entry = dataJson[i];

                var context = this;
                var voteEntry = new VoteEntry();
                voteEntry.totalVotes1(parseInt(entry.PrabowoHattaVotes).toLocaleString());
                voteEntry.status1(parseFloat(entry.PrabowoHattaPercentage) > 50.0 ? "win " : "bigScore");
                voteEntry.percentageVotes1(parseFloat(entry.PrabowoHattaPercentage).toFixed(2) + "%");

                voteEntry.totalVotes2(parseInt(entry.JokowiKallaVotes).toLocaleString());
                voteEntry.status2(parseFloat(entry.JokowiKallaPercentage) > 50.0 ? "bigScore win" : "bigScore");
                voteEntry.percentageVotes2(parseFloat(entry.JokowiKallaPercentage).toFixed(2) + "%");

                voteEntry.total(entry.Total);
                switch (idx) {
                    case 0:
                        voteEntry.label("DA1: ");
                        break;
                    case 1:
                        voteEntry.label("DB1: ");
                        break;
                    case 2:
                        voteEntry.label(" (DC1)");
                        break;
                }

                self.totalVoteEntries()[idx] = voteEntry;
                self.toggleShowTotalVoteEntries();

                self.totalVoteEntries.notifySubscribers();

                self.percentageVotes1(voteEntry.percentageVotes1());
                self.percentageVotes2(voteEntry.percentageVotes2());
                self.totalVotes1(voteEntry.totalVotes1());
                self.totalVotes2(voteEntry.totalVotes2());
                self.totalVotes(voteEntry.total());
                self.status1("bigScore " + voteEntry.status1());
                self.status2("bigScore " + voteEntry.status2());
                break;
            }
            ;
        };
        var suffix;

        suffix = "-total.db1.json";
        this.query("KPU-Feeds-" + this.lastUpdatedTime() + suffix, this.lastUpdatedTime(), db1Callback);
        suffix = "-total.json";
        this.query("KPU-Feeds-" + this.lastUpdatedTime() + suffix, this.lastUpdatedTime(), da1Callback);
        suffix = "-total.dc1.json";
        this.query("KPU-Feeds-" + this.lastUpdatedTime() + suffix, this.lastUpdatedTime(), dc1Callback);
    };

    Pilpres2014.prototype.query = function (url, context, callback, statusCallback) {
        $.ajax({
            type: 'GET',
            url: url,
            dataType: 'text',
            contentType: 'application/json',
            context: context,
            statusCode: statusCallback
        }).always(function (data, status, jqXHR) {
            if (callback) {
                callback.call(this, data, status, jqXHR);
            }
        });
    };
    return Pilpres2014;
})();

var pilpres2014ViewModel;

window.onload = function () {
    pilpres2014ViewModel = new Pilpres2014();
    ko.applyBindings(pilpres2014ViewModel);
};
//# sourceMappingURL=app.js.map
