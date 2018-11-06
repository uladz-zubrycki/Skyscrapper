#r "bin\Debug\Skyscrapper.Notifier.dll"

open System.IO
open Skyscrapper.Notifications.Formatter
open Skyscrapper.Data
open Skyscrapper.Notifications
open Skyscrapper.Notifications.Domain

let useLocal = false
let tripName = "smart-milan"
let connectionString = 
    if useLocal then @"Server=WATISLOW-PC;Database=Skyscrapper;Integrated Security=SSPI"
    else @"Server=b233415f-b406-43a3-ab87-a7330129acb1.sqlserver.sequelizer.com;Database=dbb233415fb40643a3ab87a7330129acb1;User ID=ztdcilunkgkaanfh;Password=b8KXzHwn4JwAuT74mksVFWGZR686Uk5eLFpSpjmvw4jSewmNqCXYnyTnyTfBJyzS"

let trip = Storage.loadTrip connectionString tripName |> Trip.create
let flights = Storage.loadLatestFlights connectionString tripName |> List.map Flight.create
let message = Formatter.formatMessage (fun s -> "https://goo.gl/Jbs2Xr") trip flights Loader.startPlaces

File.WriteAllText("D:\\message.txt", message)