namespace Skyscrapper.Notifications
open Skyscrapper.Services
open Skyscrapper.Notifications.Domain
open Skyscrapper.Notifications.Formatter
open Skyscrapper.Data

module Notifier = 
    let sendNotifications (tripName: string, dialogId: DialogId, config: Config) =
        let trip = Storage.loadTrip config.ConnectionString tripName |> Trip.create
        let flights = Storage.loadLatestFlights config.ConnectionString tripName |> List.map Flight.create
        let getShortFlightUrl url = 
            let url = BitlyClient.shortenUrl config.BitlyApiKey ("https://www.skyscanner.net" + url)
            let schemelessUrl = url.Substring(url.IndexOf('b'))
            schemelessUrl

        let message = Formatter.formatMessage getShortFlightUrl trip flights Loader.startPlaces
        VkClient.sendMessage config.VkAccessToken dialogId message 
