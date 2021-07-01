# LNDStreamingBackup 


## Overview
This is a bare bones .NET Core Channel Backup utility to use LND gRPC steams to provide live channel backup to Azure Storage Service. 
It will upload a new channel.backup file every time it gets notified via the gRPC stream of a change.


## How to use
```./LNDStreamingBackup <path to tls.cert> <path to macaroon> <Azure storage container connection string> <blob container to save to>```
