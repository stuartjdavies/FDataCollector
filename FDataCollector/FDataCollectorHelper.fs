module FDataCollectorHelper

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
open System.IO
open System.Text;

let processGetDataMessage (blobConnectionString : string) 
                          (url : string)
                          (containerPath : string)
                          (fileName : string) =         
        let mutable data = String.Empty
        
        data <- Http.RequestString url
       
        let blobClient = CloudStorageAccount.Parse(blobConnectionString).CreateCloudBlobClient();       
        let cloudBlobContainer = blobClient.GetContainerReference containerPath
        cloudBlobContainer.CreateIfNotExists() |> ignore        

        let uploadData = Encoding.ASCII.GetBytes(data)

        cloudBlobContainer.GetBlockBlobReference(fileName)
                          .UploadFromByteArray(uploadData, 0, uploadData.Length)
