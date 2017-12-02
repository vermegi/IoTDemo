// Write your Javascript code.

var app = angular.module('IotDemoApp', ['ui.bootstrap']);

app.run(function () { });

app.controller('DeviceEmulatorController', ['$rootScope', '$scope', '$http', '$timeout', function ($rootScope, $scope, $http, $timeout) {

    $scope.refresh = function () {
        $http.get('api/Devices')
            .then(function (data, status) {
                $scope.creatingDevices = data.data.creatingDevices;
                $scope.sendingData = data.data.sendingData;
            });
    };

    $scope.toggleCreateDevices = function () {
        $http.put('api/Devices/ToggleCreate')
            .then(function () {
                $scope.refresh();
            });
    };

    $scope.toggleSendDataFromDevices = function () {
        $http.put('api/Devices/togglesend')
            .then(function () {
                $scope.refresh();
            });
    };
}]);
