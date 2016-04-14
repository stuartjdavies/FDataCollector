#load @"ThespianCluster.fsx"

#r @"..\packages\FSharp.Data\lib\net40\FSharp.Data.dll"
#r @"..\packages\FSharp.Data\lib\net40\FSharp.Data.DesignTime.dll"
#r @"..\packages\WindowsAzure.Storage\lib\net40\Microsoft.WindowsAzure.Storage.dll"
#r @"..\packages\WindowsAzure.ServiceBus\lib\net40-full\Microsoft.ServiceBus.dll"
#r @"System.Xml.Serialization.dll"
#r @"System.Xml.Linq.dll"
#r @"System.Xml.dll"
#r @"System.Runtime.Serialization.dll"
#load "Setup.fsx"

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
open System.Text;
open Setup

let storageAccount = CloudStorageAccount.Parse(FsAppSettings.AzureWebJobsStorage)
let queueClient = storageAccount.CreateCloudQueueClient()

let queue = queueClient.GetQueueReference("getdata")

"http://www.asx.com.au/asx/research/ASXListedCompanies.csv"
|> Http.RequestString |> (fun s -> File.WriteAllText(@"c:\tmp\ASXListedCompanies.csv", s))

let getDataMessages = "http://www.asx.com.au/asx/research/ASXListedCompanies.csv"
                      |> Http.RequestString
                      |> (fun s -> s.Split('\r').[3..])                                                                                                                       
                      |> Array.map(fun item -> item.Split(','))
                      |> Array.filter(fun item -> item.Length > 1)
                      |> Array.map(fun item -> item.[1])   
                      |> Array.take 10
                      |> Array.map(fun asxCode -> (sprintf "http://ichart.finance.yahoo.com/table.csv?s=%s.AX" asxCode),
                                                  "asx", (sprintf "%s.csv" asxCode))                                                    

let cluster = Config.GetCluster() 
//let fs = cluster.Store.CloudFileSystem

let processMsgs (url, asxCode, fileName) = 
    local {
        let webClient = new WebClient()
        let! data = webClient.AsyncDownloadString(Uri(url)) |> Cloud.OfAsync
        
        let blobClient = CloudStorageAccount.Parse(FsAppSettings.AzureWebJobsStorage).CreateCloudBlobClient();       
        let cloudBlobContainer = blobClient.GetContainerReference "asx"
        cloudBlobContainer.CreateIfNotExists() |> ignore        

        let uploadData = Encoding.ASCII.GetBytes(data)

        cloudBlobContainer.GetBlockBlobReference(fileName)
                          .UploadFromByteArray(uploadData, 0, uploadData.Length)          
        return fileName
    }

let filesTask = getDataMessages 
                |> Array.map processMsgs
                |> Cloud.ParallelBalanced
                |> cluster.CreateProcess

// Get the result of the job
let files = filesTask.Result

filesTask.ShowInfo()

let blobClient = CloudStorageAccount.Parse(FsAppSettings.AzureWebJobsStorage).CreateCloudBlobClient()  
let c = blobClient.GetContainerReference("asx")
                                
let processListOfBlobs (blobFileName) = 
    local {
        let blobClient = CloudStorageAccount.Parse(FsAppSettings.AzureWebJobsStorage).CreateCloudBlobClient()  
        let c = blobClient.GetContainerReference("asx")
        let blobRef = c.GetBlockBlobReference(blobFileName)        
        return sprintf "%s,%s" blobFileName (blobRef.DownloadText().Split('\n').[1])         
    }
                                              
let headerRowsTask = files                    
                     |> Array.map processListOfBlobs
                     |> Cloud.ParallelBalanced
                     |> cluster.CreateProcess

let headerRows = headerRowsTask.Result

// Check on progress...
headerRowsTask.ShowInfo()

// Attach companies 
let companyRows = "http://www.asx.com.au/asx/research/ASXListedCompanies.csv" 
                  |> Http.RequestString                  
                  |> (fun s -> s.Replace("\"",String.Empty).Split('\n').[3..])                  
                  |> Array.filter(fun line -> line <> String.Empty)
                  |> Array.map(fun line -> line.Split(','))
                  
let headerRowsWithCompanyInfo = [| for hr in headerRows do
                                     let items = hr.Split(',')
                                     for cr in companyRows do                    
                                        if cr.[1] = items.[0].Replace(".csv", String.Empty) then          
                                            yield Array.append cr items |] 

// 1. Get the company history start and end.
// 2. Get the difference stock price close price diffence:
//          - Per day
//          - Per week
//          - Per month
//          - Per year
// 3. Get the most volitile stocks:
//          - Per week
//          - Per month
//          - Per year

