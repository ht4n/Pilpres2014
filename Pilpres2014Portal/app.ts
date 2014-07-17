///<reference path="Scripts/typings/jquery/jquery.d.ts"/>
///<reference path="Scripts/typings/knockout/knockout.d.ts"/>

class VoteEntry {
    counter1: KnockoutObservable<number>;
    counter1Percentage: KnockoutObservable<number>;
    counter2: KnockoutObservable<number>;
    counter2Percentage: KnockoutObservable<number>;
    total: KnockoutObservable<number>;
    label: KnockoutObservable<string>;

    constructor() {
        this.counter1 = ko.observable(0);
        this.counter1Percentage = ko.observable(0);
        this.counter2 = ko.observable(0);
        this.counter2Percentage = ko.observable(0);
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

    constructor() {
        this.url = ko.observable("https://github.com/ht4n/Pilpres2014");
        this.provinces = ko.observableArray([]);        
        this.totalVotes1 = ko.observable(0);
        this.totalVotes2 = ko.observable(0);
        this.percentageVotes1 = ko.observable("");
        this.percentageVotes2 = ko.observable("");
        this.totalVotes = ko.observable(0);
        this.voteEntries = ko.observableArray([]);
    }

    refresh() {
        var self = this;

        var totalCallback = function (data, status) {
            console.log("response:" + status);
            if (status !== "success") {
                return;
            }

            var dataJson = JSON.parse(data);
            dataJson.forEach((entry) => {
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
        }

        var date = "2014-07-17";
        var time = "-09-AM";
        this.query("KPU-Feeds-" + date + time + "-total.json", null, totalCallback);

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
                voteEntry.counter1Percentage(entry.PrabowoHattaPercentage);
                voteEntry.counter2(entry.PrabowoHattaVotes);
                voteEntry.counter2Percentage(entry.JokowiKallaPercentage);
                voteEntry.total(entry.Total);
                voteEntry.label(entry.Province);

                self.voteEntries.push(voteEntry);
            });
        }

        this.query("KPU-Feeds-" + date + time + "-province.json", null, provinceCallback);
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

    pilpres2014ViewModel.refresh();
};