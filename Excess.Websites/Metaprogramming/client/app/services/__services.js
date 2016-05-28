
            var xsServices = angular.module('xs.Services', []);
            
            xsServices.service('Home', ['$http', '$q', function($http, $q)
            {
                
            this.Transpile = function (text)
            {
                var deferred = $q.defer();

                $http.post('/' + this.__ID + '/Transpile', {
                    text : text,

                }).then(function(response) {
                    deferred.resolve(response);
                }, function(ex){
                    deferred.reject(ex);
                });

                return deferred.promise;
            }


                this.__ID = 'ce2afa92-5c34-4ead-b484-83031b80f0f3';
            }])