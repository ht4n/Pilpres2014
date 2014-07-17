///<reference path="Scripts/typings/jquery/jquery.d.ts"/>
///<reference path="Scripts/typings/knockout/knockout.d.ts"/>
var VoteEntry = (function () {
    function VoteEntry() {
        this.counter1 = ko.observable(0);
        this.counter1Percentage = ko.observable(0);
        this.counter2 = ko.observable(0);
        this.counter2Percentage = ko.observable(0);
        this.total = ko.observable(0);
        this.label = ko.observable("");
    }
    return VoteEntry;
})();

var Pilpres2014 = (function () {
    function Pilpres2014() {
        this.url = ko.observable("https://github.com/ht4n/Pilpres2014");
        this.provinces = ko.observableArray([]);
        this.totalVotes1 = ko.observable(0);
        this.totalVotes2 = ko.observable(0);
        this.percentageVotes1 = ko.observable("");
        this.percentageVotes2 = ko.observable("");
        this.totalVotes = ko.observable(0);
        this.voteEntries = ko.observableArray([]);
        this.showProvinceDetails = ko.observable(false);

        var baseFeedUrl = "https://github.com/ht4n/Pilpres2014/blob/master/KPU-Feeds-";
        this.historicalFeeds = ko.observableArray([
            { "datetime": "2014-07-17-03-AM", "url": baseFeedUrl },
            { "datetime": "2014-07-17-04-AM", "url": baseFeedUrl },
            { "datetime": "2014-07-17-08-AM", "url": baseFeedUrl },
            { "datetime": "2014-07-17-09-AM", "url": baseFeedUrl }
        ]);

        // Sets the current one
        var historicalFeedsLength = this.historicalFeeds().length;
        this.datetime = this.historicalFeeds()[historicalFeedsLength - 1];
        ;

        this.toggleProvinceText = ko.observable("Show Details");
    }
    Pilpres2014.prototype.toggleProvinceDetails = function () {
        if (this.showProvinceDetails()) {
            this.showProvinceDetails(false);
            this.toggleProvinceText("Show Details");
        } else {
            this.showProvinceDetails(true);
            this.toggleProvinceText("Hide Details");

            var self = this;
            var provinceCallback = function (data, status) {
                console.log("response:" + status);
                if (status !== "success") {
                    return;
                }

                var dataJson = JSON.parse(data);
                self.voteEntries.removeAll();
                dataJson.forEach(function (entry) {
                    var voteEntry = new VoteEntry();
                    voteEntry.counter1(entry.PrabowoHattaVotes);
                    voteEntry.counter1Percentage(entry.PrabowoHattaPercentage);
                    voteEntry.counter2(entry.PrabowoHattaVotes);
                    voteEntry.counter2Percentage(entry.JokowiKallaPercentage);
                    voteEntry.total(entry.Total);
                    voteEntry.label(entry.Province);

                    self.voteEntries.push(voteEntry);
                });
            };

            this.query("KPU-Feeds-" + this.datetime + "-province.json", null, provinceCallback);
        }
    };

    Pilpres2014.prototype.refresh = function () {
        var self = this;

        var totalCallback = function (data, status) {
            console.log("response:" + status);
            if (status !== "success") {
                return;
            }

            var dataJson = JSON.parse(data);
            dataJson.forEach(function (entry) {
                var voteEntry = new VoteEntry();
                voteEntry.counter1 = entry.PrabowoHattaVotes;
                voteEntry.counter1Percentage = entry.PrabowoHattaPercentage;
                voteEntry.counter2 = entry.JokowiKallaVotes;
                voteEntry.counter2Percentage = entry.JokowiKallaPercentage;

                self.totalVotes(entry.Total);
                self.totalVotes1(entry.PrabowoHattaVotes);
                self.totalVotes2(entry.JokowiKallaVotes);
                self.percentageVotes1(entry.PrabowoHattaPercentage + "%");
                self.percentageVotes2(entry.JokowiKallaPercentage + "%");
            });
        };

        this.query("KPU-Feeds-" + this.datetime + "-total.json", null, totalCallback);
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

window.onload = function () {
    var pilpres2014ViewModel = new Pilpres2014();
    ko.applyBindings(pilpres2014ViewModel);

    pilpres2014ViewModel.refresh();
};
//# sourceMappingURL=app.js.map
