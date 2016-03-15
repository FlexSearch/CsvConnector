namespace CsvConnector.Tests

open FlexSearch.Api.Model
open FlexSearch.Api.Api
open System

module Sample =
    // We assume we already have a 'contact' index with the following fields:
    // - name - text
    // - age - int
    // - origin- text
    //
    // For this example I will be using the sample 'test.csv' file present in this project

    let request = new CsvIndexingRequest(IndexName = "contact",
                                         HasHeaderRecord = true,
                                         Path = AppDomain.CurrentDomain.BaseDirectory + @"\test.csv")
    
    // Create a client to send the CSV request
    let api = new CommonApi("http://localhost:9800")

    // Send the actual request
    let result = api.Csv(request, "contact")

    // Function that extracts the job ID
    let jobId (result : CsvIndexingResponse) = 
        if result.Error |> isNull then result.Data |> Some
        else None

    // Function that checks the status
    let status (result : CsvIndexingResponse) =
        match jobId result with
        | Some(id) -> 
            let jobApi = new JobsApi("http://localhost:9800")
            printfn "Job Status: %A" <| jobApi.GetJob(id).Data.JobStatus
        | None -> printfn "Oops"

    // Execute the job status check
    //status result


    //--------------------------------------------------
    // Example of request where HasHeaderRecord = false
    // -------------------------------------------------

    let requestWithHeader = new CsvIndexingRequest(IndexName = "contact",
                                                   HasHeaderRecord = false,
                                                   Headers = [| "id"; "name"; "age"; "origin" |],
                                                   Path = AppDomain.CurrentDomain.BaseDirectory + @"\test-without-header.csv")

    let doWork =
        // Execute the request
        api.Csv(requestWithHeader, "contact")
        // Then check the job status
        |> status 
