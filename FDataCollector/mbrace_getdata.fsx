#load @"ThespianCluster.fsx"

#r @"..\packages\FSharp.Data\lib\net40\FSharp.Data.dll"
#r @"..\packages\FSharp.Data\lib\net40\FSharp.Data.DesignTime.dll"
#r @"..\packages\WindowsAzure.Storage\lib\net40\Microsoft.WindowsAzure.Storage.dll"
#r @"..\packages\WindowsAzure.ServiceBus\lib\net40-full\Microsoft.ServiceBus.dll"
#r @"System.Xml.Serialization.dll"
#r @"System.Xml.Linq.dll"
#r @"System.Xml.dll"
#r @"System.Runtime.Serialization.dll"

#load "lib/utils.fsx"

open System
open System.IO
open MBrace.Core
open MBrace.Flow
open System.Net
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

let connectionString = "DefaultEndpointsProtocol=https;AccountName=portalvhds1jhr3n92p7g65;AccountKey=vhwN5tVHdCTfafsluPt/7fSqLid6KxuKCkAeCbI5FRuylZv+CDqJWUKC3MHBclzwmmHSL35Mh0qLRHVQqWiuDg=="
let storageAccount = CloudStorageAccount.Parse(connectionString)
let queueClient = storageAccount.CreateCloudQueueClient()

let queue = queueClient.GetQueueReference("getdata")

let messages = "http://www.asx.com.au/asx/research/ASXListedCompanies.csv"
               |> Http.RequestString
               |> (fun s -> s.Split('\r').[3..])                                                                                                                       
               |> Array.map(fun item -> item.Split(','))
               |> Array.filter(fun item -> item.Length > 1)
               |> Array.map(fun item -> item.[1])   
               |> Array.take 10
               |> Array.map(fun asxCode -> (sprintf "http://ichart.finance.yahoo.com/table.csv?s=%s.AX" asxCode),
                                           "asx", (sprintf "%s.csv" asxCode))                                                    

let cluster = Config.GetCluster() 
let fs = cluster.Store.CloudFileSystem

let processMsgs (url, asxCode, fileName) = 
    local {
        let webClient = new WebClient()
        let! text = webClient.AsyncDownloadString(Uri(url)) |> Cloud.OfAsync
        do! CloudFile.Delete(sprintf "pages/%s" fileName)
        let! file = CloudFile.WriteAllText(path = sprintf "pages/%s" fileName, text = text)
        return file
    }

let filesTask = messages 
                |> Array.map processMsgs
                |> Cloud.ParallelBalanced
                |> cluster.CreateProcess

// Check on progress...
filesTask.ShowInfo()

// Get the result of the job
let files = filesTask.Result

// Read the files we just downloaded
let getLastRecords = 
    files
    |> Array.map (fun file ->
        local { let line = fs.File.ReadAllText(file.Path).Split('\n').[1].Split(',')  
                return Array.append [| file.Path |] line  })
    |> Cloud.ParallelBalanced
    |> cluster.Run