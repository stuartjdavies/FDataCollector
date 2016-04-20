#load @"ThespianCluster.fsx"
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

let writeBlob containerRef fileName (data : string) = 
        let blobClient = CloudStorageAccount.Parse(FsAppSettings.AzureWebJobsStorage).CreateCloudBlobClient();       
        let cloudBlobContainer = blobClient.GetContainerReference containerRef
        cloudBlobContainer.CreateIfNotExists() |> ignore        

        let uploadData = Encoding.ASCII.GetBytes(data)

        cloudBlobContainer.GetBlockBlobReference(fileName)
                          .UploadFromByteArray(uploadData, 0, uploadData.Length)     

//
// Download ASX Company list to file
//
"http://www.asx.com.au/asx/research/ASXListedCompanies.csv"
|> Http.RequestString 
|> (fun s -> File.WriteAllText(@"c:\tmp\ASXListedCompanies.csv", s))

//
// Upload ASX Company list to blob
//
"http://www.asx.com.au/asx/research/ASXListedCompanies.csv"
|> Http.RequestString 
|> (fun data -> writeBlob "asx" "ASXListedCompanies.csv" data)

//
// Get data from Yahoo and store in the Azure blob
//  
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

let uploadFileToBlob (url, asxCode, fileName) = 
    local {
        let webClient = new WebClient()
        let! data = webClient.AsyncDownloadString(Uri(url)) |> Cloud.OfAsync
        
        writeBlob "asx" fileName data

        return fileName
    }

let filesTask = getDataMessages 
                |> Array.map uploadFileToBlob
                |> Cloud.ParallelBalanced
                |> cluster.CreateProcess

// Get the result of the job
let files = filesTask.Result

filesTask.ShowInfo()

//
// Get List of asx companies
// 
let companyRows = "http://www.asx.com.au/asx/research/ASXListedCompanies.csv" 
                  |> Http.RequestString                  
                  |> (fun s -> s.Replace("\"",String.Empty).Split('\n').[3..])                  
                  |> Array.filter(fun line -> line <> String.Empty)
                  |> Array.map(fun line -> line.Split(','))

// 
// Get Stock info from file
//
let getStockInfo (blobFileName) = 
    local {
        let blobClient = CloudStorageAccount.Parse(FsAppSettings.AzureWebJobsStorage).CreateCloudBlobClient()  
        let c = blobClient.GetContainerReference("asx")
        let blobRef = c.GetBlockBlobReference(blobFileName)        
        
        let lines = blobRef.DownloadText().Split('\n')

        let firstObs =  Some(lines.[1])      
        let numberOfRows = lines.Length
        let secondObs = if lines.Length > 2 then Some(lines.[2]) else None      
        let lastObs = Some(lines.[numberOfRows - 1])
        let sevenObsAgo = if lines.Length > 7 then Some(lines.[7]) else None
        let thirtyObsAgo = if lines.Length > 30 then Some(lines.[30]) else None
        let oneYearObsAgo = if lines.Length > 365 then Some(lines.[365]) else None
        
        return (blobFileName, firstObs, numberOfRows, secondObs, lastObs, sevenObsAgo, thirtyObsAgo, oneYearObsAgo)     
    }

let getStockInfoTask = files                    
                       |> Array.map getStockInfo
                       |> Cloud.ParallelBalanced
                       |> cluster.CreateProcess

let stockInfos = getStockInfoTask.Result
getStockInfoTask.ShowInfo()
                
// 
// Combine with Company Info
//
let getObsRowAsString obs = (match obs with | Some(x) -> x | None -> ",,,,,,,")
let stockInfoWithCompanyInfo = [| for stockInfo in stockInfos do                                     
                                        let (blobFileName, firstObs, numberOfRows, secondObs, lastObs, sevenObsAgo, thirtyObsAgo, oneYearObsAgo) = stockInfo
                                        for cr in companyRows do                                          
                                            if cr.[1] = blobFileName.Replace(".csv", String.Empty) then          
                                                yield [| String.Join(",", cr.[0], cr.[1], cr.[2], blobFileName, numberOfRows, "First", getObsRowAsString firstObs)
                                                         String.Join(",", cr.[0], cr.[1], cr.[2], blobFileName, numberOfRows, "Second", getObsRowAsString secondObs)
                                                         String.Join(",", cr.[0], cr.[1], cr.[2], blobFileName, numberOfRows, "Last", getObsRowAsString lastObs)
                                                         String.Join(",", cr.[0], cr.[1], cr.[2], blobFileName, numberOfRows, "Seven", getObsRowAsString sevenObsAgo)
                                                         String.Join(",", cr.[0], cr.[1], cr.[2], blobFileName, numberOfRows, "Thirty", getObsRowAsString thirtyObsAgo)
                                                         String.Join(",", cr.[0], cr.[1], cr.[2], blobFileName, numberOfRows, "365", getObsRowAsString oneYearObsAgo) |] |]
                                |> Array.concat
                                                          
stockInfoWithCompanyInfo |> Array.append [| "Company name,ASX code,GICS industry group,BlobFileName, NumberOfRows,Obs,Date,Open,High,Low,Close,Volume,Adj Close" |]
                         |> (fun line -> String.Join("\n", line))   
                         |> writeBlob "asx" "StockInfoWithCompanyInfo.csv"                    
                              
stockInfoWithCompanyInfo |> Array.length





