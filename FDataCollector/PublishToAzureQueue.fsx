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
open Microsoft.WindowsAzure.Storage; 
open Microsoft.WindowsAzure.Storage.Queue;
open Setup

let storageAccount = CloudStorageAccount.Parse(FsAppSettings.AzureWebJobsStorage)
let queueClient = storageAccount.CreateCloudQueueClient()

let queue = queueClient.GetQueueReference("getdata")

"http://www.asx.com.au/asx/research/ASXListedCompanies.csv"
|> Http.RequestString
|> (fun s -> s.Split('\r').[3..])                                                                                                                       
|> Array.map(fun item -> item.Split(','))
|> Array.filter(fun item -> item.Length > 1)
|> Array.map(fun item -> item.[1])   
|> Array.map(fun asxCode -> sprintf "%s,%s,%s" (sprintf "http://ichart.finance.yahoo.com/table.csv?s=%s.AX" asxCode)
                                            "asx" (sprintf "%s.csv" asxCode))                                   
|> Array.iter(fun s -> queue.AddMessage(new CloudQueueMessage(s)))   

queue.ApproximateMessageCount
//queue.Clear()

let blobClient = CloudStorageAccount.Parse(FsAppSettings.AzureWebJobsStorage).CreateCloudBlobClient()  
let c = blobClient.GetContainerReference("asx")
c.ListBlobs() |> Seq.length
                 
 
