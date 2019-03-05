namespace Company.Function
open System
open System.IO
open System.Threading.Tasks
open Microsoft.AspNetCore.Mvc
open Microsoft.Azure.WebJobs
open Microsoft.Azure.WebJobs.Extensions.Http
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging
open Newtonsoft.Json



module HttpTriggerFSharp =

    [<FunctionName("HttpTriggerFSharp")>]
    let Run([<HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)>]req: HttpRequest, log: ILogger) =
        log.LogInformation("C# HTTP trigger function processed a request.")
        let name = req.Query.["name"]
        name