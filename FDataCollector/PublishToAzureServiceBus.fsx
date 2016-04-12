#load "Setup.fsx"

open FSharp.Data
open Microsoft.WindowsAzure
open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Auth
open Microsoft.WindowsAzure.Storage.Blob
open System.Xml
open System.Runtime.Serialization
open Microsoft.ServiceBus.Messaging
open System.Runtime.Serialization
open Microsoft.ServiceBus.Messaging
open System
open Microsoft.ServiceBus
open Microsoft.ServiceBus.Channels
open Microsoft.ServiceBus.Messaging
open System.Runtime.Serialization
open Setup

let url = "http://www.asx.com.au/asx/research/ASXListedCompanies.csv"
let fileName = "ASXListedCompanies.csv"

let namespaceManager = NamespaceManager.CreateFromConnectionString(FsAppSettings.AzureWebJobsServiceBus);

namespaceManager.DeleteQueue("getdata") |> ignore

if namespaceManager.QueueExists("getdata") = false then 
    namespaceManager.CreateQueue("getdata") |> ignore

let queueClient = QueueClient.CreateFromConnectionString(FsAppSettings.AzureWebJobsServiceBus, "getdata");

"http://www.asx.com.au/asx/research/ASXListedCompanies.csv"
|> Http.RequestString
|> (fun s -> s.Split('\r').[3..])                                                                                                                       
|> Array.map(fun item -> item.Split(','))
|> Array.filter(fun item -> item.Length > 1)
|> Array.map(fun item -> item.[1])   
|> Array.map(fun asxCode -> sprintf "%s,%s,%s" (sprintf "http://ichart.finance.yahoo.com/table.csv?s=%s.AX" asxCode)
                                            "asx" (sprintf "%s.csv" asxCode))                                   
|> Array.map(fun body -> new BrokeredMessage(body))
|> Array.take(10)
|> Array.chunkBySize(100)
|> Array.iter(fun ms -> queueClient.SendBatch(ms))

let queueDesc = namespaceManager.GetQueue("getdata")
queueDesc.MessageCount

let blobClient = CloudStorageAccount.Parse(FsAppSettings.AzureBlobContainterName).CreateCloudBlobClient()  
let c = blobClient.GetContainerReference("asx")
c.ListBlobs() |> Seq.length

c.Delete()

