#r @"..\packages\FSharp.Data\lib\net40\FSharp.Data.dll"
#r @"..\packages\FSharp.Data\lib\net40\FSharp.Data.DesignTime.dll"
#r @"..\packages\WindowsAzure.Storage\lib\net40\Microsoft.WindowsAzure.Storage.dll"
#r @"..\packages\WindowsAzure.ServiceBus\lib\net40-full\Microsoft.ServiceBus.dll"
#r @"System.Xml.Serialization.dll"
#r @"System.Xml.Linq.dll"
#r @"System.Xml.dll"
#r @"System.Runtime.Serialization.dll"

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

open FSharp.Data

type FsAppSettingsSchema = JsonProvider<".\FsScriptSettings.json">

let FsAppSettings = FsAppSettingsSchema.Load(@"C:\Users\stuart\Documents\FsScriptSettings.json")

