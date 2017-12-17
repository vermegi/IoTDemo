// Write your Javascript code.

var app = angular.module('IotDemoApp', ['ui.bootstrap']);

app.run(function () { });

app.controller('DeviceEmulatorController', ['$rootScope', '$scope', '$http', '$timeout', function ($rootScope, $scope, $http, $timeout) {

    $scope.refresh = function () {
        $scope.temptoSend = 40;
        $http.get('api/Devices')
            .then(function (data, status) {
                $scope.creatingDevices = data.data.creatingDevices;
                $scope.sendingData = data.data.sendingData;
            });
        getActorList();
    };

    $scope.toggleCreateDevices = function () {
        $http.put('api/Devices/ToggleCreate')
            .then(function () {
                $scope.refresh();
            });
    };

    $scope.toggleSendDataFromDevices = function () {
        $http.put('api/Devices/togglesend?temperature=' + $scope.temptoSend)
            .then(function () {
                $scope.refresh();
            });
    };

    var getActorList = function () {
        $http.get('api/Actors')
            .then(function (data, status) {
                $scope.numberOfActors = data.data.actorCount;
                $scope.numberOfPartitions = data.data.partitionCount;
                $scope.actors = data.data.actors;
            });
    };
}]);
