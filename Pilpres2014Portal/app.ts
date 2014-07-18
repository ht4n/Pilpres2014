///<reference path="Scripts/typings/jquery/jquery.d.ts"/>
///<reference path="Scripts/typings/knockout/knockout.d.ts"/>

class VoteEntry {
    counter1: KnockoutObservable<number>;
    counter1Percentage: KnockoutObservable<string>;
    counter2: KnockoutObservable<number>;
    counter2Percentage: KnockoutObservable<string>;
    total: KnockoutObservable<number>;
    label: KnockoutObservable<string>;

    constructor() {
        this.counter1 = ko.observable(0);
        this.counter1Percentage = ko.observable("");
        this.counter2 = ko.observable(0);
        this.counter2Percentage = ko.observable("");
        this.total = ko.observable(0);
        this.label = ko.observable("");
    }
}

class Pilpres2014 {
    provinces: KnockoutObservableArray<string>;
    url: KnockoutObservable<string>;
    totalVotes1: KnockoutObservable<number>;
    totalVotes2: KnockoutObservable<number>;
    percentageVotes1: KnockoutObservable<string>;
    percentageVotes2: KnockoutObservable<string>;
    totalVotes: KnockoutObservable<number>;
    voteEntries: KnockoutObservableArray<VoteEntry>;
    showProvinceDetails: KnockoutObservable<boolean>;
    toggleProvinceText: KnockoutObservable<string>;
    historicalFeeds: KnockoutObservableArray<any>;
    datetime: string;

    constructor() {
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
        this.datetime = this.historicalFeeds()[historicalFeedsLength - 1].datetime;

        this.toggleProvinceText = ko.observable("Show Details");

        this.refresh(this.datetime);
    }

    updateVoteByDate(data: { datetime: string; url: string; }, event: Event) {
        var vm = ko.contextFor(event.currentTarget);
        vm.$root.refresh(data.datetime);
    }

    toggleProvinceDetails() {
        if (this.showProvinceDetails()) {
            this.showProvinceDetails(false);
            this.toggleProvinceText("Show Details");
        }
        else {
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
                dataJson.forEach((entry) => {
                    var voteEntry = new VoteEntry();
                    voteEntry.counter1(entry.PrabowoHattaVotes);
                    voteEntry.counter1Percentage(parseFloat(entry.PrabowoHattaPercentage).toFixed(2));
                    voteEntry.counter2(entry.PrabowoHattaVotes);
                    voteEntry.counter2Percentage(parseFloat(entry.JokowiKallaPercentage).toFixed(2));
                    voteEntry.total(entry.Total);
                    voteEntry.label(entry.Province);

                    self.voteEntries.push(voteEntry);
                });
            }

            this.query("KPU-Feeds-" + this.datetime + "-province.json", null, provinceCallback);
        }
    }

    refresh(datetime: string) {
        var self = this;

        var totalCallback = function (data, status) {
            console.log("response:" + status);
            if (status !== "success") {
                return;
            }

            var dataJson = JSON.parse(data);
            dataJson.forEach((entry) => {
                self.totalVotes(entry.Total);
                self.totalVotes1(entry.PrabowoHattaVotes);
                self.totalVotes2(entry.JokowiKallaVotes);
                self.percentageVotes1(parseFloat(entry.PrabowoHattaPercentage).toFixed(2) + "%");
                self.percentageVotes2(parseFloat(entry.JokowiKallaPercentage).toFixed(2) + "%");
            });
        }

        this.query("KPU-Feeds-" + datetime + "-total.json", null, totalCallback);
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