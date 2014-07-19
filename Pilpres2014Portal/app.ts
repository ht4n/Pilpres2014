///<reference path="Scripts/typings/jquery/jquery.d.ts"/>
///<reference path="Scripts/typings/knockout/knockout.d.ts"/>

class VoteEntry {
    totalVotes1: KnockoutObservable<string>;
    percentageVotes1: KnockoutObservable<string>;
    totalVotes2: KnockoutObservable<string>;
    percentageVotes2: KnockoutObservable<string>;
    total: KnockoutObservable<string>;
    label: KnockoutObservable<string>;
    status1: KnockoutObservable<string>;
    status2: KnockoutObservable<string>;

    constructor() {
        this.totalVotes1 = ko.observable("");
        this.percentageVotes1 = ko.observable("");
        this.totalVotes2 = ko.observable("");
        this.percentageVotes2 = ko.observable("");
        this.total = ko.observable("");
        this.label = ko.observable("");
        this.status1 = ko.observable("");
        this.status2 = ko.observable("");
    }
}

class Pilpres2014 {
    provinces: KnockoutObservableArray<string>;
    url: KnockoutObservable<string>;
    status1: KnockoutObservable<string>;
    status2: KnockoutObservable<string>;
    totalVotes1: KnockoutObservable<string>;
    totalVotes2: KnockoutObservable<string>;
    percentageVotes1: KnockoutObservable<string>;
    percentageVotes2: KnockoutObservable<string>;
    totalVotes: KnockoutObservable<string>;
    voteEntries: KnockoutObservableArray<VoteEntry>;
    provinceVoteEntries: KnockoutObservableArray<VoteEntry>;
    showProvinceDetails: KnockoutObservable<boolean>;
    toggleProvinceText: KnockoutObservable<string>;
    historicalFeeds: KnockoutObservableArray<any>;
    selectedDataFeed: KnockoutObservable<{ datetime: string; }>;
    lastUpdatedTime: KnockoutObservable<string>;
    baseFeedUrl: string;

    constructor() {
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
        this.showProvinceDetails = ko.observable(false);

        this.baseFeedUrl = "https://github.com/ht4n/Pilpres2014/blob/master/KPU-Feeds-";
        this.historicalFeeds = ko.observableArray([]);
        this.selectedDataFeed = ko.observable(null);
        this.lastUpdatedTime = ko.observable("");

        this.query("feedsources.json", null, (data, status) => {
            console.log("response:" + status);
            if (status !== "success") {
                return;
            }

            var dataJson = JSON.parse(data);
            dataJson.forEach((entry) => {
                self.historicalFeeds.push(entry);
            });

            // Sets the current feed as the first one
            var historicalFeedsLength = this.historicalFeeds().length;
            var currentFeedItem = this.historicalFeeds()[0];
            this.selectedDataFeed(currentFeedItem);
            this.lastUpdatedTime(this.selectedDataFeed().datetime);

            this.refresh(this.selectedDataFeed().datetime);
        });

        this.toggleProvinceText = ko.observable("Show votes by province");
    }

    updateVoteByDate(data: { datetime: string; url: string; }, event: Event) {
        var vm = ko.contextFor(event.currentTarget);
        vm.$root.refresh(data.datetime);
    }

    toggleProvinceDetails() {
        if (this.showProvinceDetails()) {
            this.showProvinceDetails(false);
            this.toggleProvinceText("Show votes by province");
        }
        else {
            this.showProvinceDetails(true);
            this.toggleProvinceText("Hide votes by province");

            var self = this;
            var provinceCallback = function (data, status) {
                console.log("response:" + status);
                if (status !== "success") {
                    return;
                }

                var dataJson = JSON.parse(data);
                self.provinceVoteEntries.removeAll();
                dataJson.forEach((entry) => {
                    var voteEntry = new VoteEntry();
                    voteEntry.totalVotes1(entry.PrabowoHattaVotes);
                    voteEntry.percentageVotes1(parseFloat(entry.PrabowoHattaPercentage).toFixed(2));
                    voteEntry.totalVotes2(entry.PrabowoHattaVotes);
                    voteEntry.percentageVotes2(parseFloat(entry.JokowiKallaPercentage).toFixed(2));
                    voteEntry.total(entry.Total);
                    voteEntry.label(entry.Province);

                    self.provinceVoteEntries.push(voteEntry);
                });
            }

            this.query("KPU-Feeds-" + this.selectedDataFeed().datetime + "-province.json", null, provinceCallback);
        }
    }

    refresh(datetime: string) {
        var self = this;
        self.voteEntries.removeAll();
        
        var totalCallback = function (data, status) {
            console.log("response:" + status);
            if (status !== "success") {
                return;
            }

            var dataJson = JSON.parse(data);
            dataJson.forEach((entry: { PrabowoHattaVotes: string; JokowiKallaVotes: string; PrabowoHattaPercentage: string; JokowiKallaPercentage: string; Total: string }) => {
                var context = this;
                var voteEntry = new VoteEntry();
                voteEntry.totalVotes1(entry.PrabowoHattaVotes);
                voteEntry.status1(parseFloat(entry.PrabowoHattaPercentage) > 50.0 ? "win" : "");
                voteEntry.percentageVotes1(parseFloat(entry.PrabowoHattaPercentage).toFixed(2) + "%");

                voteEntry.totalVotes2(entry.JokowiKallaVotes);
                voteEntry.status2(parseFloat(entry.JokowiKallaPercentage) > 50.0 ? "win" : "");
                voteEntry.percentageVotes2(parseFloat(entry.JokowiKallaPercentage).toFixed(2) + "%");

                voteEntry.total(entry.Total);
                voteEntry.label(context);

                self.voteEntries.push(voteEntry);
            });

            if (self.voteEntries().length > 0) {
                var firstEntry = self.voteEntries()[0];

                self.percentageVotes1(firstEntry.percentageVotes1());
                self.percentageVotes2(firstEntry.percentageVotes2());
                self.totalVotes1(firstEntry.totalVotes1());
                self.totalVotes2(firstEntry.totalVotes2());
                self.totalVotes(firstEntry.total());
                self.status1("bigScore " + firstEntry.status1());
                self.status2("bigScore " + firstEntry.status2());
            }
        }

        this.historicalFeeds().forEach((value) => {
            this.query("KPU-Feeds-" + value.datetime + "-total.json", value.datetime, totalCallback);
        });

    }

    query(url, context?, callback?, statusCallback?) {
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
    }
}

window.onload = () => {
    var pilpres2014ViewModel = new Pilpres2014();
    ko.applyBindings(pilpres2014ViewModel);
};